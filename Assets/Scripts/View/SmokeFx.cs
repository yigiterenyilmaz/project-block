// PURPOSE: The SMOKE a dynamite board clear leaves hanging over the screen. Everything blew
// up at once, so for a moment you cannot see the arena at all - it billows in over the whole
// camera, hangs, drifts up and thins out. Spawn-and-forget, like FloatingTextFx.
//
// Blocky on purpose, like the rest of this layer: it is a bank of flat hard-edged squares on
// the same white sprite as everything else, drifting and growing, in three flat greys. No
// soft texture and no gradient - what makes it read as smoke is that dozens of squares of
// different sizes overlap at different alphas, not that any one of them is soft.
//
// It draws ABOVE the cards (they are part of what the smoke covers) but below the popups, so
// "DYNAMITE!" is still readable through it, and it has no collider - the drag code works off
// positions rather than raycasts, so nothing here can swallow a click.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>A screenful of drifting square smoke puffs. See SmokeFx.Cover.</summary>
    public sealed class SmokeFx : MonoBehaviour
    {
        /// <summary>How long one puff takes to billow in, hang and thin out.</summary>
        private const float PuffLife = 0.85f;

        /// <summary>How late the last puff may start. Staggering them is what makes the smoke
        /// roll in rather than appear.</summary>
        private const float MaxDelay = 0.3f;

        /// <summary>Fraction of a puff's life spent billowing IN. Short: it arrives fast and
        /// leaves slowly, which is what smoke does.</summary>
        private const float RiseShare = 0.16f;

        private const float MaxAlpha = 0.78f;

        /// <summary>Three flat greys and nothing in between, so the cloud stays a limited
        /// palette like everything else on screen.</summary>
        private static readonly Color[] Greys =
        {
            new Color(0.74f, 0.72f, 0.70f),
            new Color(0.54f, 0.52f, 0.51f),
            new Color(0.35f, 0.34f, 0.34f)
        };

        private struct Puff
        {
            public SpriteRenderer Renderer;
            public Vector2 Origin;
            public Vector2 Drift;
            public float Delay;
            public float FromSize;
            public float ToSize;
            public float Alpha;
        }

        private Puff[] puffs;
        private float age;
        private float lifetime;

        /// <summary>
        /// Fills <paramref name="area"/> (the camera's world rect) with smoke. Puffs are laid on
        /// a jittered grid and grown well past their spacing, so they overlap into one cloud
        /// instead of reading as a scatter of squares.
        /// </summary>
        public static void Cover(Transform parent, Rect area)
        {
            if (area.width <= 0f || area.height <= 0f)
            {
                return;
            }
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            SmokeFx fx = go.AddComponent<SmokeFx>();

            // Spacing is set off the SHORT side, so the cloud is as dense on a tall screen as on
            // a wide one; the margin puts a ring of puffs just outside the frame, so the smoke
            // has no visible edge.
            float step = area.height / 3.5f;
            float margin = step * 0.6f;
            int columns = Mathf.CeilToInt((area.width + margin * 2f) / step);
            int rows = Mathf.CeilToInt((area.height + margin * 2f) / step);
            var list = new System.Collections.Generic.List<Puff>(columns * rows * 2);
            for (int cx = 0; cx < columns; cx++)
            {
                for (int cy = 0; cy < rows; cy++)
                {
                    var at = new Vector2(
                        area.xMin - margin + (cx + 0.5f) * step + Random.Range(-0.35f, 0.35f) * step,
                        area.yMin - margin + (cy + 0.5f) * step + Random.Range(-0.35f, 0.35f) * step);
                    list.Add(fx.MakePuff(go.transform, list.Count, at, step, 1f));
                    // A second, smaller and faster puff on the same spot breaks up the grid the
                    // first one is standing on - two sizes is all it takes to stop reading as a
                    // pattern.
                    if (Random.value < 0.55f)
                    {
                        list.Add(fx.MakePuff(go.transform, list.Count,
                            at + new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f)) * step,
                            step, 0.6f));
                    }
                }
            }
            fx.puffs = list.ToArray();
            for (int i = 0; i < fx.puffs.Length; i++)
            {
                fx.lifetime = Mathf.Max(fx.lifetime, fx.puffs[i].Delay + PuffLife);
            }
        }

        private Puff MakePuff(Transform parent, int index, Vector2 at, float step, float scale)
        {
            var go = new GameObject("Puff_" + index);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(at.x, at.y, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = ViewUtil.WhiteSprite;
            renderer.color = Greys[Random.Range(0, Greys.Length)];
            // Over the cards (they are under the smoke too) but under the popups at 45.
            renderer.sortingOrder = 40 + Random.Range(0, 2);
            renderer.enabled = false;
            return new Puff
            {
                Renderer = renderer,
                Origin = at,
                // Up and slightly sideways: the whole cloud lifts, but not as one slab.
                Drift = new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(0.5f, 1.1f)) * step,
                Delay = Random.Range(0f, MaxDelay),
                FromSize = step * 0.35f * scale,
                ToSize = step * Random.Range(1.5f, 2.4f) * scale,
                Alpha = MaxAlpha * Random.Range(0.75f, 1f)
            };
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
            for (int i = 0; i < puffs.Length; i++)
            {
                Paint(puffs[i], age - puffs[i].Delay);
            }
        }

        private static void Paint(Puff puff, float puffAge)
        {
            if (puffAge < 0f || puffAge >= PuffLife)
            {
                puff.Renderer.enabled = false;
                return;
            }
            puff.Renderer.enabled = true;
            float t = puffAge / PuffLife;
            // Grows fast and then keeps swelling slowly - a puff that stopped growing would read
            // as a box sitting there.
            float grow = 1f - (1f - t) * (1f - t) * (1f - t);
            float size = Mathf.Lerp(puff.FromSize, puff.ToSize, grow);
            puff.Renderer.transform.localPosition = new Vector3(
                puff.Origin.x + puff.Drift.x * t,
                puff.Origin.y + puff.Drift.y * t,
                0f);
            puff.Renderer.transform.localScale = new Vector3(size, size, 1f);
            float alpha = t < RiseShare
                ? t / RiseShare
                : 1f - Mathf.Pow((t - RiseShare) / (1f - RiseShare), 1.6f);
            Color color = puff.Renderer.color;
            color.a = Mathf.Clamp01(alpha) * puff.Alpha;
            puff.Renderer.color = color;
        }
    }
}
