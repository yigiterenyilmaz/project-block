// PURPOSE: "Hafıza" keeping bonus cards across the round break (2026-09-29, designer's call). The
// card does not simply vanish at the end of one round and appear at the start of the next: it is
// SAVED - it breaks into a grid of pale motes that drift up and away - and next round it is
// RECALLED - motes gather out of the air into the card's shape, and the card fades in over them.
// The motes wear a cool memory lilac, never the card's own colours: they are the memory of the
// card, not a piece of it.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class MemoryDustView : MonoBehaviour
    {
        private static readonly Color Memory = new Color(0.78f, 0.74f, 1f);
        private const int Cols = 6, Rows = 8;

        /// <summary>A card at <paramref name="centre"/> (world) breaks into dust and drifts up.</summary>
        public void Dissolve(Vector2 centre, Vector2 size, int order)
        {
            StartCoroutine(RunDissolve(centre, size, order));
        }

        /// <summary>Dust gathers into <paramref name="card"/>'s shape; then the card fades in.</summary>
        public void Reform(CardVisual card, float delay, int order)
        {
            StartCoroutine(RunReform(card, delay, order));
        }

        private IEnumerator RunDissolve(Vector2 centre, Vector2 size, int order)
        {
            var motes = Grid(centre, size, order);
            var drift = new List<Vector2>();
            for (int i = 0; i < motes.Count; i++)
            {
                float r = Hash(i);
                drift.Add(new Vector2((r - 0.5f) * 1.2f, 1.2f + Hash(i + 99) * 1.4f));
            }
            const float Life = 0.9f;
            for (float t = 0f; t < Life; t += Time.deltaTime)
            {
                float k = t / Life;
                for (int i = 0; i < motes.Count; i++)
                {
                    // Each mote lets go a beat after its neighbour, top row first.
                    float go = Mathf.Clamp01((t - (1f - (i / Cols) / (float)Rows) * 0.25f) / 0.6f);
                    Vector2 home = GridPoint(centre, size, i);
                    motes[i].transform.position = home + drift[i] * go * go
                        + new Vector2(Mathf.Sin(t * 6f + i) * 0.05f * go, 0f);
                    Color c = Memory;
                    c.a = (1f - go) * 0.95f;
                    motes[i].color = c;
                }
                yield return null;
            }
            foreach (SpriteRenderer m in motes) Destroy(m.gameObject);
        }

        private IEnumerator RunReform(CardVisual card, float delay, int order)
        {
            if (card != null)
            {
                card.SetAlpha(0f);
            }
            yield return new WaitForSeconds(delay);
            if (card == null)
            {
                yield break;
            }
            Vector2 centre = card.transform.position;
            Vector2 size = new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight) * card.transform.lossyScale.x;
            var motes = Grid(centre, size, order);
            var from = new List<Vector2>();
            for (int i = 0; i < motes.Count; i++)
            {
                float a = Hash(i) * Mathf.PI * 2f;
                from.Add(GridPoint(centre, size, i) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.8f + Hash(i + 7) * 1.2f));
            }
            const float Gather = 0.6f, FadeIn = 0.3f;
            for (float t = 0f; t < Gather + FadeIn; t += Time.deltaTime)
            {
                if (card == null)
                {
                    break;
                }
                centre = card.transform.position;
                for (int i = 0; i < motes.Count; i++)
                {
                    float k = Mathf.Clamp01((t - Hash(i + 31) * 0.2f) / (Gather - 0.2f));
                    float e = 1f - (1f - k) * (1f - k);
                    motes[i].transform.position = Vector2.Lerp(from[i], GridPoint(centre, size, i), e);
                    Color c = Memory;
                    c.a = Mathf.Clamp01(k * 3f) * (1f - Mathf.Clamp01((t - Gather) / FadeIn));
                    motes[i].color = c;
                }
                card.SetAlpha(Mathf.Clamp01((t - Gather * 0.8f) / FadeIn));
                yield return null;
            }
            if (card != null)
            {
                card.SetAlpha(1f);
            }
            foreach (SpriteRenderer m in motes) Destroy(m.gameObject);
        }

        private List<SpriteRenderer> Grid(Vector2 centre, Vector2 size, int order)
        {
            var list = new List<SpriteRenderer>();
            float s = Mathf.Min(size.x / Cols, size.y / Rows) * 0.9f;
            for (int i = 0; i < Cols * Rows; i++)
            {
                var go = new GameObject("MemoryDust");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = ViewUtil.GlowSprite;
                r.sortingOrder = order;
                Vector2 unit = r.sprite.bounds.size;
                go.transform.localScale = new Vector3(s / unit.x, s / unit.y, 1f);
                go.transform.position = GridPoint(centre, size, i);
                r.color = new Color(1f, 1f, 1f, 0f);
                list.Add(r);
            }
            return list;
        }

        private static Vector2 GridPoint(Vector2 centre, Vector2 size, int i)
        {
            int x = i % Cols, y = i / Cols;
            return centre + new Vector2(((x + 0.5f) / Cols - 0.5f) * size.x, ((y + 0.5f) / Rows - 0.5f) * size.y);
        }

        private static float Hash(int i)
        {
            unchecked
            {
                uint h = (uint)i * 2654435761u;
                h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}
