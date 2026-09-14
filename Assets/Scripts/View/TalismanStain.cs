// PURPOSE: "Tılsım"'s CURSE STAIN - the dark, cell-shaped ground a claim lies on, baked from the
// EXACT reclaimed cells and from nothing else.
//
// WHY THIS REPLACED THE MEMBRANE. The layer before this one was a single field over the claim's
// bounding box, softened and wobbled at its rim. On paper that is the same idea; on the board it
// was a big transparent rounded rectangle with vines lying on it - a UI panel behind a bouquet.
// Two things did it. The rim wobble is a decoration ON a rectangle, so the rectangle survives it;
// and a field that covers a bounding box says nothing about WHICH cells were taken, which is the
// one thing this layer has to say.
//
// So nobody tries to draw an organic shape any more. It is ASSEMBLED from four dull pieces that
// know only the gameplay set, and the organism is what they add up to:
//
//   PATCH    one per reclaimed cell. A rounded irregular square a hair under a cell across -
//            not a tile: no bevel, no border, no highlight, no surface. A stain.
//   BRIDGE   one per 4-adjacent PAIR. Wide enough that two patches read as one mass with a waist
//            rather than as two blobs touching.
//   MERGE    one per 2x2, a turned blob in the shared corner, because four patches and four
//            bridges still leave a pinhole in the middle.
//   BLEED    tongues over any edge with no reclaimed neighbour, rooted INSIDE the body so they
//            swell out of the mass instead of sitting on its rim like beads. Uneven on purpose -
//            one wide, one a sliver, one barely there - and that unevenness is what actually
//            kills the rectangle.
//
// SO AN L COMES OUT L-SHAPED, A SPARSE CLAIM COMES OUT AS ISLANDS, AND A 3x3 WITH A HOLE COMES
// OUT AS A RING. None of those three is coded for anywhere: they follow from building only what
// the rules handed over.
//
// THE THREE RULES THE BAKE ITSELF FOLLOWS:
//
//  1. PARTS COMBINE WITH MAX, NEVER BY BEING DRAWN OVER EACH OTHER. Twenty semi-transparent quads
//     double-blend wherever they overlap, and the bright/dark banding lands exactly on the cell
//     boundaries - the seam this system exists to remove. One field, one renderer, no overlap.
//  2. DEPTH COMES FROM A BLUR OF THE FINISHED UNION, never from the part that drew the texel.
//     Per-part depth reads as lumps: you can see which oval was a bridge and which circle was a
//     cell straight through the tone.
//  3. THE WHOLE ANIMATION IS A CHANNEL, not a timeline. Every texel carries WHEN it belongs to
//     the claim, so one float plays the growth - patches opening from their middles, bridges
//     closing from both ends at once, tongues last - and the same float run backwards is the
//     unseal, with the tongues going first and the patches shrinking to their centres. The
//     retraction is not written down anywhere.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>One claim's baked curse stain: the field, and the two renderers' worth of numbers
    /// that make it look like something.</summary>
    public sealed class TalismanStain
    {
        /// <summary>
        /// EVERY NUMBER THE DARKNESS IS MADE OF - shape and tone together, in ONE place.
        ///
        /// They are here rather than in TalismanView.Style because they are only meaningful
        /// together: the tone numbers were arrived at by measuring what the shape does to the
        /// screen, and splitting them leaves two halves nobody can check against each other.
        /// </summary>
        public static class Style
        {
            // ---- THE PATCH ----
            /// <summary>Across, in cells. Just under one, so the mass stops short of the cell's
            /// own boundary everywhere except where a tongue reaches past it.</summary>
            public static float PatchSize = 0.98f;

            /// <summary>2 is a circle, large is a square. Three is a rounded square that still
            /// follows the cell it belongs to - a circle here reads as a bubble, and a square
            /// reads as a tile.</summary>
            public static float PatchRound = 3f;

            /// <summary>How far the outline wanders off that square. LOW: this is what makes the
            /// edge organic, and high frequency here is slime rather than rot.</summary>
            public static float PatchWobble = 0.085f;

            /// <summary>The soft edge, in cells. A hard-edged dark patch is a dark TILE; the same
            /// patch with a real gradient is shadow under something standing there.</summary>
            public static float PatchFeather = 0.11f;

            // ---- THE BRIDGE ----
            /// <summary>Along the join and across it. The width is the one that matters: at 0.72
            /// two adjacent cells read as two blobs kissing, and the cell boundary is visible in
            /// the SILHOUETTE even when it is invisible in the tone.</summary>
            public static float BridgeLong = 0.46f;

            public static float BridgeWide = 0.82f;

            public static float BridgeFeather = 0.1f;

            // ---- THE CORNER MERGE ----
            public static float MergeSize = 0.34f;

            public static float MergeFeather = 0.09f;

            // ---- THE EDGE BLEED ----
            public static int BleedMin = 2;

            public static int BleedMax = 4;

            /// <summary>How far past the cell boundary the furthest tongue gets.</summary>
            public static float BleedReach = 0.17f;

            public static float BleedWide = 0.42f;

            public static float BleedFeather = 0.1f;

            /// <summary>How far back INSIDE the body a tongue is rooted. Without this they are
            /// beads stuck to the rim; with it they are swellings of the mass.</summary>
            public static float BleedRoot = 0.26f;

            // ---- THE DEPTH ----
            /// <summary>Radius of the blur the depth is read from, in cells, and the window of it
            /// that becomes 0-1. Wide enough that a bridge is as deep as the cells it joins.
            /// </summary>
            public static float DepthBlur = 0.34f;

            public static float DepthFrom = 0.42f;

            public static float DepthTo = 0.8f;

            // ---- THE TONE ----
            /// <summary>
            /// WHAT THE GROUND IS MULTIPLIED TOWARD, and how hard. This is the COLOUR DRAIN, and
            /// it is the half of the job an alpha overlay cannot do at any opacity.
            ///
            /// The board renders in LINEAR colour. Over the backdrop behind a claim, a near-black
            /// quad at the 0.28-0.38 the design asks for lands at about 0.87 of the background's
            /// sRGB luminance - which is precisely the "the darkness does not show up" this layer
            /// has failed at three times. The design's own tone hierarchy wants 0.65-0.72 through
            /// the mass, and that needs the LINEAR value at about 0.43. Multiply gets there; alpha
            /// does not, and no amount of arguing about the opacity number changes it.
            /// </summary>
            public static readonly Color Drain = new Color(0.46f, 0.6f, 0.58f);

            public static float DrainStrength = 0.92f;

            /// <summary>The stain over the top - DARKER than the board, never paler and never
            /// grey. The drain takes the light out; this is what makes what is left cold and
            /// dirty rather than merely dim.</summary>
            public static readonly Color Stain = new Color(0.02f, 0.052f, 0.05f);

            /// <summary>
            /// MEASURED, NOT GUESSED. Against the real backdrop these put the claim at 0.67 of
            /// the background's sRGB luminance through the mass and 0.75 at its rim - the design
            /// asks for 0.65-0.72 and about 0.80. The numbers look far too strong written down,
            /// and that is the linear-space trap above: read them off the screen, never off the
            /// page.
            /// </summary>
            public static float Centre = 0.42f;

            public static float Edge = 0.17f;

            // ---- THE GROWTH, in seconds off the vine that caused it ----
            /// <summary>The colour goes first, and the stain follows it. Ahead of the vine the
            /// darkness is a hole in the board; behind it, it is the vine putting the light out.
            /// </summary>
            public static float DrainLead = 0.045f;

            public static float PatchDelay = 0.06f;

            /// <summary>How long a patch takes to open from its middle to its edges.</summary>
            public static float PatchOpen = 0.09f;

            /// <summary>A bridge closes from BOTH ends at once and merges in the middle.</summary>
            public static float BridgeClose = 0.08f;

            public static float MergeDelay = 0.05f;

            public static float BleedDelay = 0.11f;

            public static float BleedOpen = 0.12f;

            /// <summary>How softly the front arrives, in seconds.</summary>
            public static float Band = 0.05f;

            /// <summary>Texels per cell in the baked field. Enough for the feather to be a
            /// gradient; small enough that a nine-cell claim is a 130px texture.</summary>
            public static int Resolution = 28;

            /// <summary>How far past the cells the texture has to reach - the furthest tongue
            /// plus its own soft edge.</summary>
            public static float Overhang = 0.78f;
        }

        /// <summary>Which parts the bake is allowed to lay down. The lab's isolation entries -
        /// bridges only, bleed only - are these, and they are the reason the bake takes them
        /// rather than the painter hiding renderers afterwards: there is only one renderer.
        /// </summary>
        public struct Parts
        {
            public bool Patches;
            public bool Bridges;
            public bool Merges;
            public bool Bleed;

            public static Parts All
            {
                get
                {
                    return new Parts { Patches = true, Bridges = true, Merges = true, Bleed = true };
                }
            }
        }

        public Sprite Art;

        public Texture2D Tex;

        /// <summary>Where the field sits in world space, and how big it is.</summary>
        public Vector2 At;

        public Vector2 Size;

        /// <summary>What the growth channel was divided by - so the painter turns a clock in
        /// SECONDS into the front this field was baked against.</summary>
        public float Scale = 1f;

        public void Dispose()
        {
            if (Art != null)
            {
                Object.Destroy(Art);
                Art = null;
            }
            if (Tex != null)
            {
                Object.Destroy(Tex);
                Tex = null;
            }
        }

        /// <summary>
        /// Builds one component's field.
        ///
        /// <paramref name="arrival"/> is WHEN THE VINE REACHED each cell, in the claim's own
        /// clock - not a distance, not an index. Everything the darkness does is hung off it, so
        /// the ground going cold is genuinely the vine's doing rather than a second animation
        /// running next to it on a similar timer.
        /// </summary>
        /// <param name="packed">False when the stain shader is missing. The animation channels
        /// are then pointless - nothing can read them - so the body is written plain white and
        /// the field can still be drawn by an ordinary sprite material, all at once instead of
        /// crawling. Writing the channels anyway would tint the fallback by the growth map.
        /// </param>
        public static TalismanStain Bake(List<GridPos> cells, List<float> arrival, float cellSize,
            System.Func<GridPos, Vector2> toWorld, Parts show, bool packed)
        {
            if (cells == null || cells.Count == 0 || toWorld == null || cellSize <= 0f)
            {
                return null;
            }
            var stain = new TalismanStain();
            float margin = (Style.Overhang + Style.BleedFeather) * cellSize;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2 w = toWorld(cells[i]);
                minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x);
                minY = Mathf.Min(minY, w.y); maxY = Mathf.Max(maxY, w.y);
            }
            var lo = new Vector2(minX - margin, minY - margin);
            var hi = new Vector2(maxX + margin, maxY + margin);
            stain.At = (lo + hi) * 0.5f;
            stain.Size = hi - lo;

            int w2 = Mathf.Clamp(
                Mathf.RoundToInt(stain.Size.x / cellSize * Style.Resolution), 8, 1024);
            int h2 = Mathf.Clamp(
                Mathf.RoundToInt(stain.Size.y / cellSize * Style.Resolution), 8, 1024);
            var field = new float[w2 * h2];
            var growth = new float[w2 * h2];
            var cls = new byte[w2 * h2];
            for (int i = 0; i < growth.Length; i++)
            {
                growth[i] = float.MaxValue;
            }

            // Everything below works in CELLS, with the cell grid's own origin, so no number in
            // Style has to know what a world unit is.
            float px = w2 / Mathf.Max(stain.Size.x, 1e-4f) * cellSize;   // texels per cell
            var origin = new Vector2(lo.x, lo.y);
            var at = new Vector2[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                at[i] = (toWorld(cells[i]) - origin) / cellSize;
            }
            float When(int i)
            {
                return arrival != null && i < arrival.Count ? arrival[i] : 0f;
            }

            var canvas = new Canvas
            {
                Field = field, Growth = growth, Class = cls, W = w2, H = h2, Px = px
            };

            if (show.Patches)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    Patch(canvas, at[i], cells[i], When(i));
                }
            }
            if (show.Bridges)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    for (int k = 0; k < cells.Count; k++)
                    {
                        int dx = cells[k].X - cells[i].X;
                        int dy = cells[k].Y - cells[i].Y;
                        if ((dx == 1 && dy == 0) || (dx == 0 && dy == 1))
                        {
                            Bridge(canvas, at[i], at[k], When(i), When(k));
                        }
                    }
                }
            }
            if (show.Merges)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    int right = IndexOf(cells, cells[i].X + 1, cells[i].Y);
                    int up = IndexOf(cells, cells[i].X, cells[i].Y + 1);
                    int far = IndexOf(cells, cells[i].X + 1, cells[i].Y + 1);
                    if (right < 0 || up < 0 || far < 0)
                    {
                        continue;
                    }
                    float last = Mathf.Max(Mathf.Max(When(i), When(right)),
                        Mathf.Max(When(up), When(far)));
                    Merge(canvas, at[i] + new Vector2(0.5f, 0.5f), cells[i],
                        last + Style.MergeDelay);
                }
            }
            if (show.Bleed)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = k == 0 ? 1 : k == 1 ? -1 : 0;
                        int ny = k == 2 ? 1 : k == 3 ? -1 : 0;
                        if (IndexOf(cells, cells[i].X + nx, cells[i].Y + ny) >= 0)
                        {
                            continue;
                        }
                        Bleed(canvas, at[i], cells[i], nx, ny, When(i) + Style.BleedDelay);
                    }
                }
            }

            // THE DEPTH, from a blur of the finished union rather than from any one part.
            float[] depth = Blur(field, w2, h2, Mathf.Max(1, Mathf.RoundToInt(Style.DepthBlur * px)));

            float span = 0f;
            for (int i = 0; i < growth.Length; i++)
            {
                if (field[i] > 0.02f && growth[i] < float.MaxValue)
                {
                    span = Mathf.Max(span, growth[i]);
                }
            }
            stain.Scale = Mathf.Max(span, 0.05f);

            // LINEAR, and this one is not a preference. Three of the four channels are DATA -
            // depth, growth, and which part drew the texel - and the board renders in linear
            // colour, so a texture left in its sRGB default has every one of them gamma-decoded
            // on the way into the shader: the growth map alone would stretch a claim's first half
            // over two thirds of its run. Alpha is exempt from that decode, which is exactly why
            // the old Alpha8 membrane never hit this.
            var tex = new Texture2D(w2, h2, TextureFormat.RGBA32, false, true)
            {
                name = "TalismanStain",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[w2 * h2];
            for (int i = 0; i < pixels.Length; i++)
            {
                float d = Mathf.Clamp01((depth[i] - Style.DepthFrom)
                    / Mathf.Max(Style.DepthTo - Style.DepthFrom, 1e-4f));
                d = d * d * (3f - 2f * d);
                float g = growth[i] >= float.MaxValue ? 1f
                    : Mathf.Clamp01(growth[i] / stain.Scale);
                pixels[i] = new Color32(
                    packed ? (byte)Mathf.RoundToInt(Mathf.Min(d, field[i]) * 255f) : (byte)255,
                    packed ? (byte)Mathf.RoundToInt(g * 255f) : (byte)255,
                    packed ? (byte)Mathf.RoundToInt(cls[i] / 8f * 255f) : (byte)255,
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(field[i]) * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            stain.Tex = tex;
            stain.Art = Sprite.Create(tex, new Rect(0f, 0f, w2, h2), new Vector2(0.5f, 0.5f),
                w2 / Mathf.Max(stain.Size.x, 1e-4f));
            stain.Art.hideFlags = HideFlags.HideAndDontSave;
            return stain;
        }

        // =================================================================== the parts

        private sealed class Canvas
        {
            public float[] Field;
            public float[] Growth;
            public byte[] Class;
            public int W;
            public int H;

            /// <summary>Texels per cell.</summary>
            public float Px;
        }

        private static int IndexOf(List<GridPos> cells, int x, int y)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].X == x && cells[i].Y == y)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Lays one part down over the texels its own box covers - never the whole texture, which
        /// is the difference between a bake that costs a millisecond and one that costs fifty on
        /// a nine-cell claim.
        ///
        /// The field takes the MAX and the growth takes the MIN: darkness belongs to the earliest
        /// thing that claimed it, and to the deepest.
        /// </summary>
        private delegate void Sample(float x, float y, out float f, out float when);

        private static void Stamp(Canvas c, Vector2 centre, Vector2 half, byte cls, Sample sample)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((centre.x - half.x) * c.Px));
            int x1 = Mathf.Min(c.W - 1, Mathf.CeilToInt((centre.x + half.x) * c.Px));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((centre.y - half.y) * c.Px));
            int y1 = Mathf.Min(c.H - 1, Mathf.CeilToInt((centre.y + half.y) * c.Px));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float f, when;
                    sample((x + 0.5f) / c.Px, (y + 0.5f) / c.Px, out f, out when);
                    if (f <= 0.002f)
                    {
                        continue;
                    }
                    int i = y * c.W + x;
                    if (f > c.Field[i])
                    {
                        c.Field[i] = f;
                        c.Class[i] = cls;
                    }
                    if (f > 0.05f && when < c.Growth[i])
                    {
                        c.Growth[i] = when;
                    }
                }
            }
        }

        private static float Soft(float d, float feather)
        {
            float f = Mathf.Clamp01(d / Mathf.Max(feather, 1e-4f) + 0.5f);
            return f * f * (3f - 2f * f);
        }

        /// <summary>A rounded square: |x|^n + |y|^n. Two is a circle, three is the cell.</summary>
        private static float Squircle(float dx, float dy, float rx, float ry, float n)
        {
            return Mathf.Pow(Mathf.Pow(Mathf.Abs(dx) / rx, n) + Mathf.Pow(Mathf.Abs(dy) / ry, n),
                1f / n);
        }

        private static float Capsule(float x, float y, Vector2 a, Vector2 b, float r, float taper)
        {
            Vector2 d = b - a;
            float len = d.sqrMagnitude;
            float t = len < 1e-9f ? 0f
                : Mathf.Clamp01(((x - a.x) * d.x + (y - a.y) * d.y) / len);
            float qx = x - (a.x + d.x * t);
            float qy = y - (a.y + d.y * t);
            return r * (1f - taper * t) - Mathf.Sqrt(qx * qx + qy * qy);
        }

        private static void Patch(Canvas c, Vector2 p, GridPos cell, float when)
        {
            float r = Style.PatchSize * 0.5f;
            float p1 = Random01(cell, 1) * 6.2832f;
            float p2 = Random01(cell, 8) * 6.2832f;
            float reach = r * (1f + Style.PatchWobble) + Style.PatchFeather;
            Stamp(c, p, new Vector2(reach, reach), 1, delegate (float x, float y,
                out float f, out float at)
            {
                float dx = x - p.x, dy = y - p.y;
                float ang = Mathf.Atan2(dy, dx);
                float wob = 1f + Style.PatchWobble
                    * (Mathf.Sin(2f * ang + p1) * 0.6f + Mathf.Sin(3f * ang + p2) * 0.4f);
                float d = Squircle(dx, dy, r, r, Style.PatchRound) * r;
                f = Soft(r * wob - d, Style.PatchFeather);
                // IT OPENS FROM ITS MIDDLE. A patch that arrives whole is a tile appearing.
                at = when + Style.PatchDelay
                    + Style.PatchOpen * Mathf.Clamp01(d / Mathf.Max(r, 1e-4f));
            });
        }

        private static void Bridge(Canvas c, Vector2 a, Vector2 b, float whenA, float whenB)
        {
            Vector2 mid = (a + b) * 0.5f;
            bool horiz = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.y - a.y);
            float rx = (horiz ? Style.BridgeLong : Style.BridgeWide) * 0.5f;
            float ry = (horiz ? Style.BridgeWide : Style.BridgeLong) * 0.5f;
            float thin = Mathf.Min(rx, ry);
            Stamp(c, mid, new Vector2(rx + Style.BridgeFeather, ry + Style.BridgeFeather), 2,
                delegate (float x, float y, out float f, out float at)
            {
                float d = Squircle(x - mid.x, y - mid.y, rx, ry, 2.4f);
                f = Soft((1f - d) * thin, Style.BridgeFeather);
                // FROM BOTH ENDS AT ONCE, meeting in the middle - which is also why the unseal
                // opens it from the middle without a line of code saying so.
                float u = horiz ? Mathf.Clamp01((x - a.x) / Mathf.Max(b.x - a.x, 1e-4f))
                    : Mathf.Clamp01((y - a.y) / Mathf.Max(b.y - a.y, 1e-4f));
                at = Mathf.Min(whenA + Style.BridgeClose * u,
                    whenB + Style.BridgeClose * (1f - u));
            });
        }

        private static void Merge(Canvas c, Vector2 p, GridPos salt, float when)
        {
            float rx = Style.MergeSize * (0.9f + 0.25f * Random01(salt, 21));
            float ry = Style.MergeSize * (0.9f + 0.25f * Random01(salt, 22));
            float thin = Mathf.Min(rx, ry);
            const float turn = Mathf.PI * 0.25f;
            float ca = Mathf.Cos(turn), sa = Mathf.Sin(turn);
            float reach = Mathf.Max(rx, ry) + Style.MergeFeather;
            Stamp(c, p, new Vector2(reach, reach), 3, delegate (float x, float y,
                out float f, out float at)
            {
                float dx = x - p.x, dy = y - p.y;
                float d = Squircle(dx * ca + dy * sa, -dx * sa + dy * ca, rx, ry, 2.6f);
                f = Soft((1f - d) * thin, Style.MergeFeather);
                at = when;
            });
        }

        private static void Bleed(Canvas c, Vector2 p, GridPos cell, int nx, int ny, float when)
        {
            int salt = 17 + nx * 3 + ny * 7;
            int n = Style.BleedMin
                + (int)(Random01(cell, salt) * (Style.BleedMax - Style.BleedMin + 1));
            n = Mathf.Clamp(n, Style.BleedMin, Style.BleedMax);
            float tx = -ny, ty = nx;
            for (int i = 0; i < n; i++)
            {
                float along = (Random01(cell, salt + i * 11) - 0.5f) * 0.66f;
                float reach = Style.BleedReach
                    * (0.08f + 0.92f * Mathf.Pow(Random01(cell, salt + i * 11 + 5), 1.5f));
                float wide = Style.BleedWide
                    * (0.4f + 0.8f * Random01(cell, salt + i * 11 + 9));
                // SOME ARE TONGUES AND SOME ARE BUMPS. A rim of equal round lobes is a cloud,
                // which is the other silhouette this must never have.
                float taper = 0.15f + 0.7f * Random01(cell, salt + i * 11 + 13);
                var o = new Vector2(p.x + tx * along, p.y + ty * along);
                var root = new Vector2(o.x + nx * (0.5f - Style.BleedRoot),
                    o.y + ny * (0.5f - Style.BleedRoot));
                var tip = new Vector2(o.x + nx * (0.5f + reach - wide * 0.5f),
                    o.y + ny * (0.5f + reach - wide * 0.5f));
                Vector2 mid = (root + tip) * 0.5f;
                float half = (root - tip).magnitude * 0.5f + wide * 0.5f + Style.BleedFeather;
                float r = wide * 0.5f;
                float step = Style.BleedOpen;
                Stamp(c, mid, new Vector2(half, half), 4, delegate (float x, float y,
                    out float f, out float at)
                {
                    f = Soft(Capsule(x, y, root, tip, r, taper), Style.BleedFeather);
                    float u = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), root)
                        / Mathf.Max((tip - root).magnitude, 1e-4f));
                    at = when + step * u;
                });
            }
        }

        /// <summary>A separable box blur - two running sums, which is what keeps the depth pass
        /// off the profiler however wide it is set.</summary>
        private static float[] Blur(float[] src, int w, int h, int r)
        {
            var tmp = new float[src.Length];
            var dst = new float[src.Length];
            float norm = 1f / (2 * r + 1);
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                float sum = 0f;
                for (int x = -r; x <= r; x++)
                {
                    sum += src[row + Mathf.Clamp(x, 0, w - 1)];
                }
                for (int x = 0; x < w; x++)
                {
                    tmp[row + x] = sum * norm;
                    sum -= src[row + Mathf.Clamp(x - r, 0, w - 1)];
                    sum += src[row + Mathf.Clamp(x + r + 1, 0, w - 1)];
                }
            }
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int y = -r; y <= r; y++)
                {
                    sum += tmp[Mathf.Clamp(y, 0, h - 1) * w + x];
                }
                for (int y = 0; y < h; y++)
                {
                    dst[y * w + x] = sum * norm;
                    sum -= tmp[Mathf.Clamp(y - r, 0, h - 1) * w + x];
                    sum += tmp[Mathf.Clamp(y + r + 1, 0, h - 1) * w + x];
                }
            }
            return dst;
        }

        private static float Random01(GridPos cell, int salt)
        {
            uint v = (uint)(cell.X * 73856093 ^ cell.Y * 19349663 ^ salt * 83492791);
            v ^= v >> 13;
            v *= 1274126177u;
            v ^= v >> 16;
            return (v & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }
}
