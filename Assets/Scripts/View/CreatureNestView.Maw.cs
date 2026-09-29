// PURPOSE: "Besleme"'s creature as a GAPING MOUTH (2026-09-29, designer's call). The nest's gel
// pool is the flesh; this puts a JAW on it: a dark THROAT that deepens toward the middle of the
// region and a row of FANGS along every outer edge of it, pointing inward - so the patch reads as
// something open and hungry rather than a warm puddle. The teeth follow the region exactly (an L
// gets an L of teeth, a bigger creature a bigger maw), breathe slowly open and closed while it
// waits, and CHOMP inward when it eats (PlayFeed).
//
// Teeth sit on the region's boundary band, small and on the edge, so a cube standing in the cell
// is never hidden - the middle of the cell is where a block lands and is left alone.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class CreatureNestView
    {
        private static class MawStyle
        {
            public static float ToothLength = 0.26f;   // share of a cell
            public static float ToothWidth = 0.17f;
            public static int TeethPerEdge = 2;
            public static float BreathPeriod = 2.6f;
            public static float BreathAmount = 0.16f;
            public static float ChompSeconds = 0.28f;
            public static float ChompReach = 0.12f;   // share of a cell, inward
            public static float ThroatAlpha = 0.34f;
            public static Color Tooth = new Color(0.95f, 0.9f, 0.8f);
            public static Color Throat = new Color(0.12f, 0.03f, 0.07f);
        }

        private const int ThroatOrder = 6;
        private const int ToothOrder = 8;

        private sealed class Tooth
        {
            public SpriteRenderer R;
            public Vector2 Root;     // on the region's edge
            public Vector2 Inward;   // unit, toward the inside of the mouth
            public float Length;     // its own length factor
            public float Phase;
        }

        private readonly List<Tooth> teeth = new List<Tooth>();
        private readonly List<SpriteRenderer> throats = new List<SpriteRenderer>();
        private float chompAt = -10f;

        private static Sprite fangSprite;

        private void BuildMaw(System.Func<GridPos, Vector2> toWorld)
        {
            ClearMaw();
            if (cells.Count == 0 || toWorld == null)
            {
                return;
            }
            var set = new HashSet<GridPos>(cells);
            float c = cellSize;
            // The throat: one dark soft body per cell, deepest where the cell is furthest in.
            foreach (GridPos cell in cells)
            {
                int inside = 0;
                if (set.Contains(new GridPos(cell.X + 1, cell.Y))) inside++;
                if (set.Contains(new GridPos(cell.X - 1, cell.Y))) inside++;
                if (set.Contains(new GridPos(cell.X, cell.Y + 1))) inside++;
                if (set.Contains(new GridPos(cell.X, cell.Y - 1))) inside++;
                var r = MakeMawRenderer(ViewUtil.GlowSprite, ThroatOrder);
                Vector2 at = toWorld(cell);
                r.transform.localPosition = at;
                Vector2 unit = r.sprite.bounds.size;
                float s = c * (0.9f + 0.1f * inside);
                r.transform.localScale = new Vector3(s / unit.x, s / unit.y, 1f);
                Color col = MawStyle.Throat;
                col.a = MawStyle.ThroatAlpha * (0.45f + 0.14f * inside);
                r.color = col;
                throats.Add(r);
            }
            // The fangs: along every edge of the region that faces OUT, pointing in.
            var sides = new[] { new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1) };
            foreach (GridPos cell in cells)
            {
                Vector2 centre = toWorld(cell);
                foreach (GridPos side in sides)
                {
                    if (set.Contains(new GridPos(cell.X + side.X, cell.Y + side.Y)))
                    {
                        continue;
                    }
                    var outward = new Vector2(side.X, side.Y);
                    var along = new Vector2(-outward.y, outward.x);
                    Vector2 edge = centre + outward * (c * 0.5f);
                    for (int t = 0; t < MawStyle.TeethPerEdge; t++)
                    {
                        float u = (t + 0.5f) / MawStyle.TeethPerEdge - 0.5f;
                        var tooth = new Tooth
                        {
                            R = MakeMawRenderer(Fang, ToothOrder),
                            Root = edge + along * (u * c * 0.78f),
                            Inward = -outward,
                            // Alternating long and short, like a real row of teeth.
                            Length = ((cell.X + cell.Y + t) % 2 == 0) ? 1f : 0.72f,
                            Phase = (cell.X * 0.37f + cell.Y * 0.61f + t * 0.5f),
                        };
                        tooth.R.color = MawStyle.Tooth;
                        teeth.Add(tooth);
                    }
                }
            }
            TickMaw();
        }

        private SpriteRenderer MakeMawRenderer(Sprite sprite, int order)
        {
            var go = new GameObject("Maw");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        private void ChompMaw()
        {
            chompAt = clock;
        }

        private void TickMaw()
        {
            if (teeth.Count == 0)
            {
                return;
            }
            float c = cellSize;
            // Slowly gaping open and closed while it waits.
            float breath = 0.5f + 0.5f * Mathf.Sin(clock * Mathf.PI * 2f / MawStyle.BreathPeriod);
            float age = clock - chompAt;
            float chomp = age >= 0f && age < MawStyle.ChompSeconds
                ? Mathf.Sin(age / MawStyle.ChompSeconds * Mathf.PI) : 0f;
            foreach (Tooth t in teeth)
            {
                float jitter = 0.04f * Mathf.Sin(clock * 1.7f + t.Phase);
                float len = c * MawStyle.ToothLength * t.Length
                    * (1f - MawStyle.BreathAmount * breath + 0.25f * chomp + jitter);
                // The tooth's root slides in with the bite, and out a hair as it gapes.
                Vector2 root = t.Root + t.Inward * (c * (MawStyle.ChompReach * chomp - 0.02f * breath));
                t.R.transform.localPosition = root;
                float angle = Mathf.Atan2(t.Inward.y, t.Inward.x) * Mathf.Rad2Deg - 90f;
                t.R.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                Vector2 unit = t.R.sprite.bounds.size;
                t.R.transform.localScale = new Vector3(c * MawStyle.ToothWidth / unit.x, len / unit.y, 1f);
            }
        }

        private void ClearMaw()
        {
            foreach (Tooth t in teeth)
            {
                if (t.R != null) Destroy(t.R.gameObject);
            }
            teeth.Clear();
            foreach (SpriteRenderer r in throats)
            {
                if (r != null) Destroy(r.gameObject);
            }
            throats.Clear();
        }

        /// <summary>A fang, pivoted on its ROOT (bottom middle) and pointing up: a slightly
        /// curved tapering triangle, ivory, with the shading darker toward the gum.</summary>
        private static Sprite Fang
        {
            get
            {
                if (fangSprite != null)
                {
                    return fangSprite;
                }
                const int w = 32, h = 48;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int y = 0; y < h; y++)
                {
                    float v = (y + 0.5f) / h;              // 0 root .. 1 tip
                    float half = 0.5f * (1f - v) * (1f - 0.15f * v);
                    float bend = 0.08f * v * v;            // a slight hook
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w - 0.5f - bend;
                        float d = half - Mathf.Abs(u);
                        float a = Mathf.Clamp01(d * w * 0.9f);
                        float shade = Mathf.Lerp(0.62f, 1f, v) * (u < 0f ? 1f : 0.86f);
                        tex.SetPixel(x, y, new Color(shade, shade, shade, a));
                    }
                }
                tex.Apply();
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                fangSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h);
                return fangSprite;
            }
        }
    }
}
