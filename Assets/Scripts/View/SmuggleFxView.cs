// PURPOSE: "Kaçakçı" - taking goods off the shelf WITHOUT PAYING (2026-09-29, designer's call). A
// smuggle used to play the ordinary buy, which said "you bought this"; it has to say "you slipped
// this out the back". And the goods that turn out to be JUNK get a beat of their own, so the
// player sees the coin come down on the wrong side instead of discovering it later.
//
//   THE SMUGGLE  the offer goes into shadow (a dark puff over the tile), a purple KAÇAK stamp
//                lands on it, and a dark BUNDLE slips out low - dipping under the straight line
//                rather than flying over it, the way something carried out of sight moves -
//                leaving a few faint footprints, to wherever the goods go.
//   THE DEFECT   where the bundle arrives: it lands with a jolt, a red-grey flash, a few jagged
//                cracks spread from it, sparks spit off, and a KUSURLU stamp in dull red. Sound
//                goods simply land with a small glint.
//
// THE VIEW DECIDES NOTHING: whether the goods were junk is GameSession.LastSmuggleDefective.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class SmuggleFxView : MonoBehaviour
    {
        private static readonly Color Shadow = new Color(0.1f, 0.05f, 0.14f);
        private static readonly Color Contraband = new Color(0.62f, 0.4f, 0.9f);
        private static readonly Color Junk = new Color(0.86f, 0.28f, 0.22f);
        private const int Order = 130;

        public System.Action OnDefect;
        public System.Action OnLand;

        public void Play(Vector2 from, Vector2 to, bool defective)
        {
            StartCoroutine(Run(from, to, defective));
        }

        private IEnumerator Run(Vector2 from, Vector2 to, bool defective)
        {
            // Into shadow: a dark puff over the offer.
            var puffs = new List<SpriteRenderer>();
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.047f;
                var p = Make(ViewUtil.GlowSprite, Order, from + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.25f, 0.9f);
                puffs.Add(p);
            }
            FloatingTextFx.Spawn(transform, from + new Vector2(0f, 0.6f), Loc.Pick("SMUGGLED", "KAÇAK"),
                Contraband, 54, 0.07f);
            var bundle = Make(ViewUtil.RoundedSprite, Order + 2, from, 0.42f);
            var trail = new List<SpriteRenderer>();
            float t = 0f;
            const float Puff = 0.22f, Flight = 0.6f;
            float nextPrint = Puff;
            int step = 0;
            while (t < Puff + Flight)
            {
                t += Time.deltaTime;
                float pk = Mathf.Clamp01(t / Puff);
                for (int i = 0; i < puffs.Count; i++)
                {
                    float a = i * 1.047f;
                    Vector2 at = from + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.25f + 0.35f * pk);
                    Place(puffs[i], at, 0.9f + 0.5f * pk);
                    puffs[i].color = Tint(Shadow, 0.6f * (1f - Mathf.Clamp01((t - Puff * 0.5f) / (Puff + Flight * 0.6f))));
                }
                float fk = Mathf.Clamp01((t - Puff) / Flight);
                float e = fk * fk * (3f - 2f * fk);
                // LOW: dips under the straight line, out of sight.
                Vector2 pos = Vector2.Lerp(from, to, e) + Vector2.down * Mathf.Sin(e * Mathf.PI) * 1.4f;
                Place(bundle, pos, 0.42f * (1f - 0.3f * Mathf.Sin(e * Mathf.PI)));
                bundle.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 24f) * 8f * (fk > 0f ? 1f : 0f));
                bundle.color = Tint(Color.Lerp(Shadow, Contraband, 0.35f), fk > 0f ? 0.95f : pk);
                if (t >= nextPrint && fk > 0f && fk < 0.95f)
                {
                    nextPrint += 0.07f;
                    Vector2 side = new Vector2(-(to - from).normalized.y, (to - from).normalized.x) * (step % 2 == 0 ? 0.09f : -0.09f);
                    var print = Make(ViewUtil.GlowSprite, Order + 1, pos + side, 0.12f);
                    print.color = Tint(Contraband, 0.45f);
                    trail.Add(print);
                    step++;
                }
                for (int i = 0; i < trail.Count; i++)
                {
                    Color c = trail[i].color;
                    c.a = Mathf.Max(0f, c.a - Time.deltaTime * 0.6f);
                    trail[i].color = c;
                }
                yield return null;
            }
            foreach (SpriteRenderer p in puffs) Destroy(p.gameObject);
            Destroy(bundle.gameObject);
            if (defective)
            {
                yield return Defect(to);
            }
            else
            {
                if (OnLand != null) OnLand();
                var glint = Make(ViewUtil.GlowSprite, Order + 2, to, 0.5f);
                for (float g = 0f; g < 0.3f; g += Time.deltaTime)
                {
                    Place(glint, to, 0.5f + g * 2f);
                    glint.color = Tint(Contraband, 0.7f * (1f - g / 0.3f));
                    yield return null;
                }
                Destroy(glint.gameObject);
            }
            float fade = 0.4f;
            while (fade > 0f)
            {
                fade -= Time.deltaTime;
                foreach (SpriteRenderer p in trail)
                {
                    Color c = p.color;
                    c.a = Mathf.Max(0f, c.a - Time.deltaTime);
                    p.color = c;
                }
                yield return null;
            }
            foreach (SpriteRenderer p in trail) Destroy(p.gameObject);
        }

        /// <summary>The junk reveal: jolt, flash, cracks, sparks, stamp.</summary>
        private IEnumerator Defect(Vector2 at)
        {
            if (OnDefect != null) OnDefect();
            var flash = Make(ViewUtil.GlowSprite, Order + 2, at, 1.1f);
            var cracks = new List<SpriteRenderer>();
            var crackLen = new List<float>();
            for (int i = 0; i < 5; i++)
            {
                var c = Make(ViewUtil.WhiteSprite, Order + 3, at, 1f);
                float a = i * 72f + 17f * (i % 2);
                c.transform.localRotation = Quaternion.Euler(0f, 0f, a);
                cracks.Add(c);
                crackLen.Add(0.35f + 0.18f * ((i * 7) % 3));
            }
            var sparks = new List<SpriteRenderer>();
            var sparkV = new List<Vector2>();
            for (int i = 0; i < 9; i++)
            {
                float a = i * 0.7f + 0.3f;
                sparks.Add(Make(ViewUtil.GlowSprite, Order + 3, at, 0.1f));
                sparkV.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a) + 0.5f) * (1.6f + (i % 3) * 0.6f));
            }
            FloatingTextFx.Spawn(transform, at + new Vector2(0f, 0.55f), Loc.Pick("DEFECTIVE!", "KUSURLU!"),
                Junk, 60, 0.08f);
            const float Life = 0.75f;
            for (float t = 0f; t < Life; t += Time.deltaTime)
            {
                float k = t / Life;
                // A jolt: the spot shudders, hard then dying.
                Vector2 jolt = new Vector2(Mathf.Sin(t * 60f), Mathf.Cos(t * 47f)) * 0.06f * (1f - k);
                Place(flash, at + jolt, 1.1f + 0.4f * k);
                flash.color = Tint(Color.Lerp(Junk, new Color(0.45f, 0.45f, 0.48f), k), 0.7f * (1f - k));
                float grow = Mathf.Clamp01(t / 0.18f);
                for (int i = 0; i < cracks.Count; i++)
                {
                    float len = crackLen[i] * grow;
                    float ang = cracks[i].transform.localEulerAngles.z * Mathf.Deg2Rad;
                    cracks[i].transform.localPosition = at + jolt + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len * 0.5f;
                    Vector2 unit = cracks[i].sprite.bounds.size;
                    cracks[i].transform.localScale = new Vector3(len / unit.x, 0.03f / unit.y, 1f);
                    cracks[i].color = Tint(new Color(1f, 0.85f, 0.8f), 0.9f * (1f - k * k));
                }
                for (int i = 0; i < sparks.Count; i++)
                {
                    sparkV[i] += Vector2.down * 5f * Time.deltaTime;
                    sparks[i].transform.localPosition += (Vector3)(sparkV[i] * Time.deltaTime);
                    sparks[i].color = Tint(Color.Lerp(new Color(1f, 0.8f, 0.4f), Junk, k), 1f - k);
                }
                yield return null;
            }
            Destroy(flash.gameObject);
            foreach (SpriteRenderer c in cracks) Destroy(c.gameObject);
            foreach (SpriteRenderer s in sparks) Destroy(s.gameObject);
        }

        private SpriteRenderer Make(Sprite sprite, int order, Vector2 at, float size)
        {
            var go = new GameObject("Smuggle");
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
