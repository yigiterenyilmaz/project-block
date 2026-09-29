// PURPOSE: "Tutumluluk" paying (2026-09-29, designer's call): THRIFT, drawn as putting money in a
// coin bank. A small piggy bank pops up beside the score, a gold coin drops into the slot on its
// back, the bank jiggles with the weight and the joker's percentage stamps over it, then it sinks
// away. It fires on every scoring turn the joker boosts, so it is SHORT (under a second) and small
// - a sign next to the score, never a show over the board. Built from generated shapes: a rounded
// body, two ears, a snout with nostrils, four stubby legs, the coin slot, and the coin.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class PiggyBankFx : MonoBehaviour
    {
        private static readonly Color Pink = new Color(0.96f, 0.62f, 0.72f);
        private static readonly Color PinkDark = new Color(0.78f, 0.42f, 0.54f);
        private static readonly Color Coin = new Color(1f, 0.82f, 0.3f);
        private const int Order = 125;

        private bool playing;

        public void Play(Vector2 at, string label)
        {
            if (playing)
            {
                return; // one bank at a time; a turn that procs twice is one deposit
            }
            StartCoroutine(Run(at, label));
        }

        private IEnumerator Run(Vector2 at, string label)
        {
            playing = true;
            var root = new GameObject("PiggyBank").transform;
            root.SetParent(transform, false);
            root.position = at;
            var parts = new List<SpriteRenderer>();
            SpriteRenderer Part(Sprite sprite, Vector2 pos, Vector2 size, Color c, int order)
            {
                var go = new GameObject("Piggy");
                go.transform.SetParent(root, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = sprite;
                r.sortingOrder = order;
                r.color = c;
                Vector2 unit = sprite.bounds.size;
                go.transform.localPosition = pos;
                go.transform.localScale = new Vector3(size.x / unit.x, size.y / unit.y, 1f);
                parts.Add(r);
                return r;
            }
            Sprite round = ViewUtil.RoundedSprite;
            Sprite dot = ViewUtil.GlowSprite;
            for (int i = 0; i < 4; i++)
            {
                Part(round, new Vector2(-0.24f + i * 0.16f, -0.22f), new Vector2(0.09f, 0.14f), PinkDark, Order);
            }
            Part(round, Vector2.zero, new Vector2(0.72f, 0.5f), Pink, Order + 1);                  // body
            Part(round, new Vector2(-0.16f, 0.25f), new Vector2(0.1f, 0.12f), PinkDark, Order);     // ear
            Part(round, new Vector2(0.04f, 0.26f), new Vector2(0.1f, 0.12f), PinkDark, Order);      // ear
            Part(round, new Vector2(0.37f, -0.02f), new Vector2(0.14f, 0.16f), PinkDark, Order + 2); // snout
            Part(dot, new Vector2(0.37f, 0.01f), new Vector2(0.04f, 0.04f), new Color(0.35f, 0.15f, 0.22f), Order + 3);
            Part(dot, new Vector2(0.37f, -0.05f), new Vector2(0.04f, 0.04f), new Color(0.35f, 0.15f, 0.22f), Order + 3);
            Part(dot, new Vector2(0.18f, 0.08f), new Vector2(0.05f, 0.05f), new Color(0.2f, 0.1f, 0.14f), Order + 3); // eye
            Part(round, new Vector2(-0.06f, 0.24f), new Vector2(0.2f, 0.035f), new Color(0.3f, 0.12f, 0.18f), Order + 3); // slot
            SpriteRenderer coin = Part(round, new Vector2(-0.06f, 0.95f), new Vector2(0.17f, 0.17f), Coin, Order + 2);
            SpriteRenderer shine = Part(dot, new Vector2(-0.09f, 0.98f), new Vector2(0.06f, 0.06f), new Color(1f, 1f, 0.85f), Order + 3);

            const float Pop = 0.14f, Drop = 0.22f, Jiggle = 0.25f, Sink = 0.22f;
            bool clinked = false;
            for (float t = 0f; t < Pop + Drop + Jiggle + Sink; t += Time.deltaTime)
            {
                float pop = Mathf.Clamp01(t / Pop);
                float scale = pop < 1f ? Mathf.Lerp(0.3f, 1.08f, pop) : 1f;
                float td = t - Pop;
                // The coin falls into the slot, narrowing as it turns edge-on into it.
                float dk = Mathf.Clamp01(td / Drop);
                float cy = Mathf.Lerp(0.95f, 0.24f, dk * dk);
                coin.transform.localPosition = new Vector2(-0.06f, cy);
                Vector2 cu = coin.sprite.bounds.size;
                float edge = dk > 0.75f ? Mathf.Lerp(1f, 0.1f, (dk - 0.75f) / 0.25f) : 1f;
                coin.transform.localScale = new Vector3(0.17f * edge / cu.x, 0.17f / cu.y, 1f);
                coin.color = dk >= 1f ? new Color(0f, 0f, 0f, 0f) : Coin;
                shine.transform.localPosition = new Vector2(-0.09f, cy + 0.03f);
                shine.color = dk >= 1f ? new Color(0f, 0f, 0f, 0f) : new Color(1f, 1f, 0.85f, 0.9f);
                if (!clinked && dk >= 1f)
                {
                    clinked = true;
                    FloatingTextFx.Spawn(transform, at + new Vector2(0f, 0.55f), label, Coin, 44, 0.06f);
                }
                // The jiggle with the weight going in.
                float tj = td - Drop;
                float jig = tj > 0f && tj < Jiggle ? Mathf.Sin(tj / Jiggle * Mathf.PI * 3f) * (1f - tj / Jiggle) : 0f;
                float ts = t - Pop - Drop - Jiggle;
                float sink = Mathf.Clamp01(ts / Sink);
                root.localScale = new Vector3(scale * (1f + 0.08f * jig), scale * (1f - 0.08f * jig), 1f) * (1f - sink);
                root.localRotation = Quaternion.Euler(0f, 0f, 6f * jig);
                yield return null;
            }
            Destroy(root.gameObject);
            playing = false;
        }
    }
}
