// PURPOSE: "Parazit" on the JOKER BAR - the card of a joker that is riding a block wears the
// same thing its host cube wears on the board: a thin dead-mauve membrane over the card, veins
// running out of a nest near the top of the icon, and every few seconds the card straining under
// it (the film thins, the nest grips harder, it settles back). It is the bar's half of one
// statement - "this joker lives on a cube, and dies with it" - and it reads the same way as the
// harness ParasiteHostView draws on the cube, in the same palette, so the two are recognisably
// the same organism.
//
// ONE baked texture (generated once, shared by every card): the membrane's alpha is a few
// low-frequency lobes so its edge is never the card's rectangle, the veins are curved strokes out
// of the nest, and the nest itself is a lobed knot with a lit rim. No shader, no particles - it
// sits in the card's own UI hierarchy, so it moves, scales and hides with the card for free.

using UnityEngine;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    public sealed class ParasiteCardFx : MonoBehaviour
    {
        private const int TexW = 96;
        private const int TexH = 128;

        /// <summary>Seconds between two struggles, and how long one lasts.</summary>
        private const float StrugglePeriod = 4.2f;
        private const float StruggleLength = 0.9f;

        private static Texture2D texture;

        private RawImage film;
        private float phase;

        /// <summary>Puts the effect on (or takes it off) a bar card's root.</summary>
        public static void Apply(GameObject cardRoot, bool on)
        {
            if (cardRoot == null)
            {
                return;
            }
            ParasiteCardFx fx = cardRoot.GetComponentInChildren<ParasiteCardFx>(true);
            if (!on)
            {
                if (fx != null)
                {
                    fx.gameObject.SetActive(false);
                }
                return;
            }
            if (fx == null)
            {
                var go = new GameObject("ParasiteFx", typeof(RectTransform));
                go.transform.SetParent(cardRoot.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(3f, 3f);
                rect.offsetMax = new Vector2(-3f, -3f);
                fx = go.AddComponent<ParasiteCardFx>();
                fx.film = go.AddComponent<RawImage>();
                fx.film.texture = Texture;
                fx.film.raycastTarget = false;
                // Seeded from the card, so two infected cards never struggle in step.
                fx.phase = (cardRoot.GetInstanceID() & 1023) / 1023f * StrugglePeriod;
            }
            fx.gameObject.SetActive(true);
            // Over the painting and its text: the membrane is ON the card, not under it.
            fx.transform.SetAsLastSibling();
        }

        private void Update()
        {
            if (film == null)
            {
                return;
            }
            float t = Time.unscaledTime + phase;
            // A slow breath of the film's opacity - never a flash.
            float breath = 0.86f + 0.08f * Mathf.Sin(t * 1.3f);
            // THE STRUGGLE: the card pushes, the film thins over it, and it is pressed back.
            float s = Mathf.Repeat(t, StrugglePeriod);
            float push = s < StruggleLength ? Mathf.Sin(s / StruggleLength * Mathf.PI) : 0f;
            film.color = new Color(1f, 1f, 1f, breath * (1f - 0.35f * push));
            float scale = 1f + 0.018f * push;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>The baked membrane, veins and nest. Built once.</summary>
        private static Texture2D Texture
        {
            get
            {
                if (texture != null)
                {
                    return texture;
                }
                texture = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                var film = new Color(0.46f, 0.30f, 0.44f);
                var vein = new Color(0.22f, 0.10f, 0.20f);
                var nest = new Color(0.52f, 0.22f, 0.44f);
                var rim = new Color(0.86f, 0.62f, 0.80f);
                var nestAt = new Vector2(0.56f, 0.70f);
                var pixels = new Color[TexW * TexH];
                for (int y = 0; y < TexH; y++)
                {
                    for (int x = 0; x < TexW; x++)
                    {
                        float u = (x + 0.5f) / TexW;
                        float v = (y + 0.5f) / TexH;
                        // The film: low lobes so its edge wanders in from the card's edge,
                        // heavier in the middle, never the rectangle it is drawn on.
                        float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                        float wander = 0.05f + 0.035f * Mathf.Sin(u * 9.1f + v * 3.3f)
                            + 0.025f * Mathf.Sin(v * 11.7f - u * 5.2f);
                        float cover = Mathf.SmoothStep(0f, 1f, (edge - wander) / 0.07f);
                        float thick = 0.5f + 0.5f * Mathf.Sin(u * 5.3f + 1.1f) * Mathf.Sin(v * 4.1f + 0.4f);
                        Color c = film;
                        float a = cover * (0.16f + 0.14f * thick);

                        // Veins: curved strokes running out of the nest.
                        var p = new Vector2(u, v);
                        Vector2 d = p - nestAt;
                        float angle = Mathf.Atan2(d.y, d.x);
                        float r = d.magnitude;
                        float veinField = Mathf.Abs(Mathf.Sin(angle * 2.5f + r * 7f + 0.6f));
                        float veinLine = Mathf.Clamp01(1f - veinField / 0.09f)
                            * Mathf.Clamp01(1f - r / 0.62f) * cover;
                        if (veinLine > 0f)
                        {
                            c = Color.Lerp(c, vein, veinLine);
                            a = Mathf.Max(a, 0.55f * veinLine);
                        }

                        // The nest: a lobed knot with a lit rim.
                        float lobes = 0.085f + 0.018f * Mathf.Sin(angle * 3f + 0.9f)
                            + 0.01f * Mathf.Sin(angle * 5f);
                        float inside = Mathf.Clamp01((lobes - r) / 0.012f);
                        if (inside > 0f)
                        {
                            float rimK = Mathf.Clamp01(1f - Mathf.Abs(r - lobes * 0.8f) / 0.02f);
                            Color knot = Color.Lerp(nest, rim, rimK * 0.7f);
                            c = Color.Lerp(c, knot, inside);
                            a = Mathf.Lerp(a, 0.92f, inside);
                        }
                        pixels[y * TexW + x] = new Color(c.r, c.g, c.b, a);
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                return texture;
            }
        }
    }
}
