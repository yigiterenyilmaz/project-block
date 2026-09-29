// PURPOSE: "Kredi kartı" on screen (2026-09-29, designer's call).
//   PAYING DOWN  gold coins stream from the score (where the money is) into a stamp that reads
//                PAID, green - the debt going away is something the player watches happen.
//   FORECLOSED   the run lost to an open debt gets its own ending instead of the plain summary
//                cut: the screen goes red at its edges, a heavy HACİZ stamp slams down in the
//                middle, and the score DRAINS - coins fall out of it and off the bottom of the
//                screen. The summary waits for it (Playing).

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class DebtFxView : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.82f, 0.36f);
        private static readonly Color Paid = new Color(0.45f, 0.95f, 0.55f);
        private static readonly Color Red = new Color(0.9f, 0.18f, 0.16f);
        private const int Order = 140;

        private int running;

        public bool Playing
        {
            get { return running > 0; }
        }

        public void PlayRepay(Vector2 from, Vector2 to, long amount)
        {
            StartCoroutine(Repay(from, to, amount));
        }

        public void PlayForeclosure(Vector2 centre, Vector2 score, float halfWidth, float halfHeight)
        {
            StartCoroutine(Foreclose(centre, score, halfWidth, halfHeight));
        }

        private IEnumerator Repay(Vector2 from, Vector2 to, long amount)
        {
            running++;
            const int Coins = 10;
            var coins = new List<SpriteRenderer>();
            for (int i = 0; i < Coins; i++)
            {
                coins.Add(Make(ViewUtil.GlowSprite, Order, from, 0.22f));
            }
            const float Flight = 0.55f, Stagger = 0.04f;
            for (float t = 0f; t < Flight + Stagger * Coins; t += Time.deltaTime)
            {
                for (int i = 0; i < Coins; i++)
                {
                    float k = Mathf.Clamp01((t - i * Stagger) / Flight);
                    float e = k * k * (3f - 2f * k);
                    Vector2 p = Vector2.Lerp(from, to, e) + Vector2.up * Mathf.Sin(e * Mathf.PI) * (0.9f + 0.1f * (i % 3));
                    Place(coins[i], p, 0.22f * (1f - 0.4f * e));
                    coins[i].color = Tint(Gold, k > 0f && k < 1f ? 1f : 0f);
                }
                yield return null;
            }
            foreach (SpriteRenderer c in coins) Destroy(c.gameObject);
            FloatingTextFx.Spawn(transform, to, Loc.Pick("PAID  -", "ÖDENDİ  -") + amount, Paid, 58, 0.08f);
            var ring = Make(ViewUtil.GlowSprite, Order, to, 0.6f);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                Place(ring, to, 0.6f + t * 4f);
                ring.color = Tint(Paid, 0.6f * (1f - t / 0.35f));
                yield return null;
            }
            Destroy(ring.gameObject);
            running--;
        }

        private IEnumerator Foreclose(Vector2 centre, Vector2 score, float halfW, float halfH)
        {
            running++;
            // Red edges: four soft bars closing in a little.
            var edges = new List<SpriteRenderer>();
            for (int i = 0; i < 4; i++)
            {
                edges.Add(Make(ViewUtil.GlowSprite, Order - 1, centre, 1f));
            }
            var stamp = ViewUtil.MakeText3D(transform, "Foreclosed", centre, Loc.Pick("FORECLOSED", "HACİZ"),
                120, 0.09f, Red, Order + 2, TextAnchor.MiddleCenter);
            var sub = ViewUtil.MakeText3D(transform, "ForeclosedSub", centre + new Vector2(0f, -1.1f),
                Loc.Pick("the debt was never paid", "borç ödenmedi"), 60, 0.05f,
                new Color(1f, 0.8f, 0.75f), Order + 2, TextAnchor.MiddleCenter);
            var coins = new List<SpriteRenderer>();
            var coinV = new List<Vector2>();
            const float Total = 2.2f;
            float nextCoin = 0.3f;
            for (float t = 0f; t < Total; t += Time.deltaTime)
            {
                float k = t / Total;
                float edgeA = Mathf.Clamp01(t / 0.3f) * (1f - Mathf.Clamp01((t - Total + 0.4f) / 0.4f));
                for (int i = 0; i < 4; i++)
                {
                    bool horizontal = i < 2;
                    float sign = i % 2 == 0 ? 1f : -1f;
                    Vector2 at = horizontal ? centre + new Vector2(0f, sign * halfH) : centre + new Vector2(sign * halfW, 0f);
                    Vector2 unit = edges[i].sprite.bounds.size;
                    float thick = 1.6f + 0.3f * Mathf.Sin(t * 9f);
                    edges[i].transform.localPosition = at;
                    edges[i].transform.localScale = horizontal
                        ? new Vector3(halfW * 2.4f / unit.x, thick / unit.y, 1f)
                        : new Vector3(thick / unit.x, halfH * 2.4f / unit.y, 1f);
                    edges[i].color = Tint(Red, 0.55f * edgeA);
                }
                // THE STAMP: slams from big to its size with a jolt.
                float sk = Mathf.Clamp01(t / 0.18f);
                float scale = Mathf.Lerp(2.6f, 1f, sk * sk) * (sk >= 1f ? 1f + 0.04f * Mathf.Sin(t * 30f) * Mathf.Exp(-(t - 0.18f) * 8f) : 1f);
                stamp.transform.localScale = Vector3.one * scale;
                stamp.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
                stamp.color = Tint(Red, Mathf.Clamp01(t / 0.1f) * (1f - Mathf.Clamp01((t - Total + 0.3f) / 0.3f)));
                sub.color = Tint(sub.color, Mathf.Clamp01((t - 0.35f) / 0.25f) * (1f - Mathf.Clamp01((t - Total + 0.3f) / 0.3f)));
                // THE SCORE DRAINS: coins spill out of it and fall off the screen.
                if (t >= nextCoin && t < Total - 0.6f)
                {
                    nextCoin += 0.05f;
                    coins.Add(Make(ViewUtil.GlowSprite, Order, score, 0.2f));
                    coinV.Add(new Vector2(((coins.Count * 37) % 10 - 5) * 0.12f, 0.8f));
                }
                for (int i = 0; i < coins.Count; i++)
                {
                    coinV[i] += Vector2.down * 9f * Time.deltaTime;
                    coins[i].transform.localPosition += (Vector3)(coinV[i] * Time.deltaTime);
                    coins[i].color = Tint(Gold, 0.9f);
                }
                yield return null;
            }
            foreach (SpriteRenderer e in edges) Destroy(e.gameObject);
            foreach (SpriteRenderer c in coins) Destroy(c.gameObject);
            Destroy(stamp.gameObject);
            Destroy(sub.gameObject);
            running--;
        }

        private SpriteRenderer Make(Sprite sprite, int order, Vector2 at, float size)
        {
            var go = new GameObject("DebtFx");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = new Color(1f, 1f, 1f, 0f);
            Place(r, at, size);
            return r;
        }

        private static void Place(SpriteRenderer r, Vector2 at, float size)
        {
            r.transform.localPosition = at;
            Vector2 unit = r.sprite.bounds.size;
            r.transform.localScale = new Vector3(size / Mathf.Max(unit.x, 1e-4f), size / Mathf.Max(unit.y, 1e-4f), 1f);
        }

        private static Color Tint(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
