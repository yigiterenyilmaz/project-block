// PURPOSE: The soft light the arena's grid gives off in overtime. BoardView already tints the
// plate its lines are cut out of, which colours them; this is what makes them look LIT.
//
// WHY A SECOND LAYER AT ALL. Tinting the plate is a flat swap: every line the same colour, every
// edge as hard as the cell that cuts it, the whole board changing at once. That reads as a filter
// laid over the arena rather than as light, and the two things it is missing are the two things
// light always has - it SPILLS past what emits it, and it FALLS OFF with distance.
//
// SO THIS IS DRAWN OVER THE CELLS, not under them. Under, the cells would clip it back to exactly
// the hard-edged gaps the plate already fills and nothing would be gained. Over, at a low alpha,
// the glow bleeds a little way onto the blocks either side - which is what a lit line does to the
// things next to it, and the whole reason it stops looking cut out.
//
// THE PROFILE IS TWO LOBES, and that is what makes it read as light rather than as a coloured
// band. One gaussian gives a single width: wide enough to spill and the grid is a wash, narrow
// enough to stay a line and nothing spills. Light does both at once - a small bright CORE and a
// wide faint HALO around it - so this is a tight lobe plus a broad weak one, summed.
//
// THE CORE IS A FIXED WIDTH, deliberately NOT the gap it sits in. The board draws an empty cell
// at 92% of its square and a filled one at 98%, so the gap between empty cells is EIGHT TIMES
// wider - and a glow tied to the gap was correspondingly eight times fatter on an empty board
// than on a full one, which is exactly what it looked like. The light is its own thing now: the
// same filament down the middle of the groove whether the groove is wide or hairline.
//
// It is generated per board size into one texture. The value at a pixel is g(min(dx, dy)) over
// the distances to the nearest line on each axis, and because g only ever decreases that equals
// max(g(dx), g(dy)) - so two 1D profiles are built and combined, and the resolution can be high
// enough for a thin core without the cost of evaluating the lobes a million times.
//
// IT HAS A SOURCE: the two bottom corners, where the fire actually stands. The light falls off
// with distance from them, so the near corners are lit and the far top is barely touched. A
// vertical ramp was here first and it is not the same thing - a ramp still lights the whole width
// evenly, which no lamp does.
//
// THE JUNCTIONS ARE ROUNDED, and that needs a soft minimum rather than a plain one. The distance
// to the nearest line is min(dx, dy), and a plain min has a CREASE along the diagonal where the
// nearest line changes from the vertical one to the horizontal one - which is exactly a cell's
// corner, so every crossing came out as a hard square-shouldered plus. A soft min rounds that
// changeover, and the light turns the corner in a curve the way light does.
//
// AND IT IS UNEVEN, which is the part that stops it looking printed. Every cell had an identical,
// perfectly symmetric falloff before, and a grid of identical falloffs reads as a stencil laid
// over the board - the giveaway was a dark X in the middle of every single cell, the same one
// every time. Two octaves of value noise now vary the brightness along the lines, so some
// stretches are lit and some are not, and no two cells are the same.
//
// The texture is rebuilt only when the arena changes size, which is once a round.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The overtime glow on the board's grid lines. Owned and fed by BoardView.</summary>
    public sealed class BoardLineGlowView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the glow LOOKS, in one place.</summary>
        public static class Style
        {
            /// <summary>Whether the glow is drawn at all. Off for now: the whole overtime look
            /// is being reconsidered from scratch, and this is the fourth of its four switches -
            /// the other three are DrawFlames, DrawEmbers and DrawDrift in FlameStreakView.
            /// Nothing below has been removed or changed; turning this back on restores it
            /// exactly. The texture is not even generated while this is false.</summary>
            public static bool Enabled = false;

            /// <summary>Colour of the light. Warmer and paler than the tint under it - the core
            /// of a lit line reads hotter than its edges.</summary>
            public static Color Tint = new Color(1f, 0.93f, 0.62f);

            /// <summary>The bright CORE: its width in cells, and its share of the peak. Thin -
            /// it is the filament - and it has to be narrower than the widest groove it sits in
            /// (0.08 cells, between two empty cells) or it fills the groove and we are back to
            /// colouring a band. Its weight is well under 1 on purpose: a core at full strength
            /// is a hard white wire, and no amount of halo around it softens that.</summary>
            public static float CoreWidth = 0.048f;

            public static float CoreWeight = 0.62f;

            /// <summary>The HALO: how far the spill reaches into the cells, in cells, and its
            /// share of the peak. This is the "seeping into the blocks" half. It cannot simply be
            /// widened to seep further - past about 0.2 the falloffs from the four sides of a
            /// cell meet in its middle and every cell grows the same dark diamond, which is
            /// exactly the printed look this is trying to avoid. Widen it WITH NoiseAmount.</summary>
            public static float HaloWidth = 0.13f;

            public static float HaloWeight = 0.15f;

            /// <summary>How far, in cells, the corner rounding reaches. This is the softness of
            /// the soft minimum: at 0 it is a plain min and every crossing has square shoulders,
            /// and much past a tenth of a cell the junctions swell into blobs.</summary>
            public static float CornerRounding = 0.055f;

            /// <summary>Peak alpha at the first overtime level and at the deepest. Low, and lower
            /// than it looks like it should be: this lies on top of everything on the board, and
            /// the arena has to stay readable through it while it is at its brightest. Trimmed
            /// when DimFloor arrived, which lifts the average brightness on its own.</summary>
            public static float AlphaLow = 0.09f;

            public static float AlphaHigh = 0.22f;

            /// <summary>How far the light reaches from a bottom corner, as a fraction of the
            /// board's SHORT side, and how sharply it gives out. Written against the board rather
            /// than in cells so a 7x7 and an 11x11 are lit the same way.</summary>
            public static float SourceReach = 0.95f;

            public static float SourceFalloff = 1.6f;

            /// <summary>How much of the brightness the noise takes away and gives back, 0..1, and
            /// the size of its coarse octave in cells. This is the dial for "it looks printed":
            /// at 0 every cell is identical and it does, at 0.5 the grid has bright stretches and
            /// dim ones and it stops.</summary>
            public static float NoiseAmount = 0.50f;

            public static float NoisePeriod = 2.4f;

            /// <summary>The FLOOR under the source falloff and the noise together, as a fraction
            /// of full. Nothing on the grid is ever darker than this while the light is up.
            ///
            /// It exists because the two modulations multiply, and multiplying two things that
            /// each dip to a third leaves stretches of line at a fifth of full - which against
            /// this board is simply black, so the grid came out with pieces missing. Measured on
            /// the lines themselves the pair ran 0.20 to 1.17, a spread of nearly six; with the
            /// floor it is under two, and the dim stretches are dim rather than absent.</summary>
            public static float DimFloor = 0.34f;

            /// <summary>Pixels per cell in the generated texture. Enough to draw the core
            /// cleanly without making the noise and the source falloff - which are per pixel and
            /// cannot be separated out - cost more than a round start can afford.</summary>
            public static int Resolution = 64;
        }

        // =================================================================== internals

        /// <summary>Over the cells (1) and their previews (2), under the destruction flash (10).
        /// Over is the entire point - see the header.</summary>
        private const int SortingOrder = 4;

        private SpriteRenderer glow;
        private Texture2D texture;
        private int builtWidth;
        private int builtHeight;

        /// <summary>Rebuilds the glow for an arena. Called from BoardView.Rebuild, which is the
        /// only place the board's size or position can change.</summary>
        public void Build(Vector2 center, int cellsWide, int cellsHigh, float cellSize,
            float overhang)
        {
            if (glow == null)
            {
                var go = new GameObject("Glow");
                go.transform.SetParent(transform, false);
                glow = go.AddComponent<SpriteRenderer>();
                glow.sortingOrder = SortingOrder;
                // Off until SetGlow says otherwise, which it does on the same frame if the round
                // is already in overtime. Only here, never on a later Build: a board rebuilt
                // mid-overtime would otherwise blink the light off for a frame.
                glow.enabled = false;
            }
            glow.transform.localPosition = new Vector3(center.x, center.y, 0f);

            float w = cellsWide * cellSize + overhang;
            float h = cellsHigh * cellSize + overhang;
            if (!Style.Enabled)
            {
                // Not even generated while the look is off - it is the expensive part.
                return;
            }
            if (texture == null || builtWidth != cellsWide || builtHeight != cellsHigh)
            {
                Regenerate(cellsWide, cellsHigh, overhang / cellSize);
                builtWidth = cellsWide;
                builtHeight = cellsHigh;
            }
            // The sprite is one unit across whatever its pixel size, so the plate's world size
            // goes on the transform and the texture never has to know about cellSize.
            glow.transform.localScale = new Vector3(w, h, 1f);
        }

        /// <summary>Sets the glow's strength, 0..1 within the level's own reach. BoardView drives
        /// this from the same pulse that tints the plate, so the two move together.</summary>
        public void SetGlow(int level, float wave)
        {
            if (glow == null)
            {
                return;
            }
            if (level <= 0 || !Style.Enabled)
            {
                glow.enabled = false;
                return;
            }
            float depth = Mathf.Clamp01(level / 6f);
            Color c = Style.Tint;
            c.a = Mathf.Lerp(Style.AlphaLow, Style.AlphaHigh, depth) * Mathf.Clamp01(wave);
            glow.enabled = true;
            glow.color = c;
        }

        /// <summary>Draws the light. Works in CELL units throughout, so the same code covers
        /// every arena and every width stays a constant fraction of a cell.
        ///
        /// The two per-axis exponentials the SOFT MINIMUM needs depend on one coordinate each,
        /// so they are built once per row and column rather than per pixel; the profile itself
        /// is a lookup table, since after the soft min it is a function of one distance. What is
        /// left per pixel is a log, two exponentials for the lamps, and the noise.</summary>
        private void Regenerate(int cellsWide, int cellsHigh, float overhangInCells)
        {
            int px = Mathf.Max(8, Style.Resolution);
            float halfPad = overhangInCells * 0.5f;
            // The plate in cell units: the grid plus half the overhang on every side.
            float spanX = cellsWide + overhangInCells;
            float spanY = cellsHigh + overhangInCells;
            int w = Mathf.Clamp(Mathf.RoundToInt(spanX * px), 16, 1024);
            int h = Mathf.Clamp(Mathf.RoundToInt(spanY * px), 16, 1024);

            if (texture != null)
            {
                Destroy(texture);
            }
            texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;

            float k = Mathf.Max(0.001f, Style.CornerRounding);
            var expX = new float[w];
            var cellX = new float[w];
            for (int x = 0; x < w; x++)
            {
                cellX[x] = (x + 0.5f) / w * spanX - halfPad;
                expX[x] = Mathf.Exp(-DistanceToLine(cellX[x], cellsWide) / k);
            }

            // The profile after the soft min is a function of one distance, so it is tabulated.
            const int lut = 256;
            const float lutSpan = 0.6f;   // cells; past this the profile is already nothing
            var profile = new float[lut];
            for (int i = 0; i < lut; i++)
            {
                profile[i] = Profile(i / (float)(lut - 1) * lutSpan);
            }

            // The lamps: the two BOTTOM corners, where the fire stands.
            float reach = Mathf.Max(0.01f,
                Style.SourceReach * Mathf.Min(cellsWide, cellsHigh));
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float cy = (y + 0.5f) / h * spanY - halfPad;
                float expY = Mathf.Exp(-DistanceToLine(cy, cellsHigh) / k);
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    // Soft minimum of the two distances. A plain min creases along the diagonal
                    // where the nearest line changes, which IS the cell's corner; this rounds it.
                    float d = -k * Mathf.Log(expX[x] + expY);
                    int i = Mathf.Clamp(
                        Mathf.RoundToInt(Mathf.Max(0f, d) / lutSpan * (lut - 1)), 0, lut - 1);

                    float cx = cellX[x];
                    float lit = Mathf.Max(
                        Lamp(cx, cy, 0f, reach),
                        Lamp(cx, cy, cellsWide, reach));
                    // Floored TOGETHER, not one at a time: it is the product of the two that
                    // reaches black, and flooring either alone would not stop it.
                    float mod = Mathf.Lerp(Style.DimFloor, 1f,
                        Mathf.Clamp01(lit * Unevenness(cx, cy)));
                    float a = profile[i] * mod;
                    pixels[row + x] = new Color32(255, 255, 255,
                        (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            glow.sprite = Sprite.Create(texture, new Rect(0f, 0f, w, h),
                new Vector2(0.5f, 0.5f), Mathf.Max(w, h));
        }

        /// <summary>How lit a point is by a lamp sitting on the board's floor at `lampX`.</summary>
        private static float Lamp(float cx, float cy, float lampX, float reach)
        {
            float dx = cx - lampX;
            float r = Mathf.Sqrt(dx * dx + cy * cy) / reach;
            return Mathf.Exp(-Mathf.Pow(r, Style.SourceFalloff));
        }

        /// <summary>The multiplier that stops every cell looking the same: two octaves of value
        /// noise around 1. The second is finer and weaker, which is what keeps the coarse one
        /// from reading as blobs of its own.</summary>
        private static float Unevenness(float cx, float cy)
        {
            float n = ValueNoise(cx, cy, Style.NoisePeriod) * 0.65f
                + ValueNoise(cx + 31.7f, cy - 17.3f, Style.NoisePeriod * 0.42f) * 0.35f;
            return 1f - Style.NoiseAmount + Style.NoiseAmount * 2f * n;
        }

        /// <summary>Value noise on a unit lattice, smoothstepped between corners. Deterministic
        /// from the hash, so an arena looks the same every time it is drawn - the unevenness is
        /// meant to look natural, not to be different on every rebuild.</summary>
        private static float ValueNoise(float cx, float cy, float period)
        {
            float fx = cx / Mathf.Max(0.01f, period);
            float fy = cy / Mathf.Max(0.01f, period);
            int ix = Mathf.FloorToInt(fx);
            int iy = Mathf.FloorToInt(fy);
            float tx = fx - ix;
            float ty = fy - iy;
            float sx = tx * tx * (3f - 2f * tx);
            float sy = ty * ty * (3f - 2f * ty);
            float a = Hash01(ix, iy);
            float b = Hash01(ix + 1, iy);
            float c = Hash01(ix, iy + 1);
            float d = Hash01(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        private static float Hash01(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h % 100000u) / 100000f;
        }

        /// <summary>The two-lobe falloff at a distance from a line, in cells: a thin bright core
        /// plus a wide faint halo. Capped at 1 so the two lobes cannot sum past full where they
        /// overlap at the line itself.</summary>
        private static float Profile(float d)
        {
            float core = Style.CoreWeight
                * Mathf.Exp(-(d * d) / Mathf.Max(1e-6f, Style.CoreWidth * Style.CoreWidth));
            float halo = Style.HaloWeight
                * Mathf.Exp(-(d * d) / Mathf.Max(1e-6f, Style.HaloWidth * Style.HaloWidth));
            return Mathf.Min(1f, core + halo);
        }

        /// <summary>The generated texture is marked HideAndDontSave, so nothing else will ever
        /// collect it - and this component is destroyed and remade every time the board is
        /// rebuilt, which is once a round. Without this each arena would leave its texture
        /// behind for the rest of the run.</summary>
        private void OnDestroy()
        {
            if (texture != null)
            {
                Destroy(texture);
                texture = null;
            }
        }

        /// <summary>Distance from a cell-space coordinate to the nearest grid line, in cells.
        /// Lines sit at every integer from 0 to `cells`, so this is the distance to the closest
        /// of those - clamped at the ends, which is what makes the outer frame glow and fade
        /// outward rather than repeating past the edge.</summary>
        private static float DistanceToLine(float v, int cells)
        {
            float nearest = Mathf.Clamp(Mathf.Round(v), 0f, cells);
            return Mathf.Abs(v - nearest);
        }
    }
}
