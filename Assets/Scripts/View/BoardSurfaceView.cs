// PURPOSE: The arena's own surface - the plate the cells sit in, generated as one texture. It
// replaces the flat dark rectangle the board used to be drawn on, which read as a debug grid:
// a plain fill with the cell squares leaving gaps, and the gaps WERE the grid lines.
//
// EVERYTHING HERE IS PROCEDURAL. No asset arrives with it, and it is rebuilt only when the arena
// changes SIZE - a placement never touches it, and neither does anything else per turn.
//
// WHAT IS IN THE IMAGE, and why each part is there:
//
//   THE PLATE. A rounded rectangle with a small radius - visible, but the board stays square and
//   geometric. Around its edge, a bevel: the outer faces pointing up-left take a touch of light
//   and those pointing down-right take a touch of shadow, which is all a "thickness" is. Outside
//   it, a soft drop shadow, so the board sits ON the backdrop rather than being cut out of it.
//
//   THE SURFACE. Not one flat colour: a low-contrast gradient lifts the middle a few points over
//   the rim, and a fine grain sits under everything. Neither is meant to be noticed - together
//   they are the difference between a painted panel and a fill.
//
//   THE SLOTS. Each cell is a rounded-square RECESS. Its floor is a few tones darker than the
//   surface, its top-left inner wall takes shadow and its bottom-right inner wall a weak bounce,
//   and the land just outside it catches a thin lip of light. Which is also where the "grid" now
//   comes from: there are no lines any more, only the material between the slots.
//
// LIGHT COMES FROM THE UPPER LEFT, once, for the plate bevel and for every slot. One direction
// for the whole board is what stops it reading as a collage.
//
// THE DEPTH IS DELIBERATELY SMALL. A block sits IN a slot and has to win: if the recess is deep
// enough to notice on its own, the empty board competes with the pieces on it.
//
// IT KNOWS THE CELL SPRITE. BoardView draws an empty cell as a hard-edged square at EmptyFill,
// and that square has to land on the slot's flat FLOOR - inside the walls, and inside the corner
// radius. That is a real constraint between two files: raising EmptyFill or lowering SlotHalf
// far enough will push the square's corners out through the slot's rounded ones.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The generated board surface. Owned and fed by BoardView.</summary>
    public sealed class BoardSurfaceView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the board LOOKS, in one place. Lengths are in
        /// CELLS, so a 7x7 and an 11x11 are the same board at different sizes.</summary>
        public static class Style
        {
            /// <summary>The board material, and the floor of a slot. The floor is darker: that
            /// difference is most of what says "recessed" before any shading is added.</summary>
            public static Color Surface = new Color(0.150f, 0.160f, 0.190f);

            public static Color Slot = new Color(0.108f, 0.117f, 0.143f);

            /// <summary>Corner radius of the whole plate. Small but visible - enough to take the
            /// machined edge off, not enough to make it a lozenge.</summary>
            public static float PlateRadius = 0.34f;

            /// <summary>How far in from the plate's edge the bevel reaches, and how hard it
            /// lights and shades. Kept unequal on purpose: a dark edge carries a form at a lower
            /// contrast than a bright one, so matching them makes the light side shout.</summary>
            public static float FrameWidth = 0.155f;

            public static float FrameLight = 0.16f;

            public static float FrameDark = 0.26f;

            /// <summary>Half the width of a slot's opening, and its corner radius, in cells. The
            /// land between two slots is therefore 1 - 2 * SlotHalf wide, and that land is what
            /// now does the job the grid lines used to.</summary>
            public static float SlotHalf = 0.450f;

            public static float SlotRadius = 0.115f;

            /// <summary>The slot's inner wall: how far up it the shading reaches, and how hard
            /// the shadowed and the lit side are. The lit side is much weaker - it is a bounce
            /// off the floor, not a second lamp.</summary>
            public static float WallWidth = 0.080f;

            public static float WallDark = 0.34f;

            public static float WallLight = 0.12f;

            /// <summary>The lip of land immediately OUTSIDE a slot, on the side facing the light.
            /// This is the edge the slot is cut into catching light, and it is what makes the
            /// recess read as cut rather than painted.</summary>
            public static float LipWidth = 0.055f;

            public static float LipLight = 0.10f;

            /// <summary>How much the middle of the plate is lifted over its rim, and how strong
            /// the grain is. Both are meant to be invisible one at a time.</summary>
            public static float Gradient = 0.055f;

            public static float Grain = 0.010f;

            /// <summary>The drop shadow outside the plate: how far it reaches, and how dark it
            /// starts. What separates the board from the backdrop without a line around it.</summary>
            public static float ShadowWidth = 0.28f;

            public static float ShadowAlpha = 0.42f;

            /// <summary>Transparent margin around the plate, in cells - room for the shadow to
            /// fall into. Too small and the shadow is cut off square.</summary>
            public static float Margin = 0.30f;

            /// <summary>Pixels per cell in the generated texture. The image is all soft falloff,
            /// so this is about the smallest that keeps the plate's corner and the slot walls
            /// from stepping.</summary>
            public static int Resolution = 64;
        }

        /// <summary>The light, once, for the whole board: from the upper left.</summary>
        private static readonly Vector2 LightDir = new Vector2(-0.7071f, 0.7071f);

        // =================================================================== internals

        /// <summary>Under everything: the cells are at 1, their previews at 2.</summary>
        private const int SortingOrder = 0;

        private SpriteRenderer plate;
        private Texture2D texture;
        private int builtWidth;
        private int builtHeight;

        /// <summary>(Re)builds the surface for an arena. Called from BoardView.Rebuild, the only
        /// place the board's size or position changes. The texture itself is regenerated only
        /// when the CELL COUNT changes - everything in it is measured in cells.</summary>
        public void Build(Vector2 center, int cellsWide, int cellsHigh, float cellSize,
            float overhang)
        {
            if (plate == null)
            {
                var go = new GameObject("Surface");
                go.transform.SetParent(transform, false);
                plate = go.AddComponent<SpriteRenderer>();
                plate.sortingOrder = SortingOrder;
            }
            plate.transform.localPosition = new Vector3(center.x, center.y, 0f);

            float overhangInCells = overhang / cellSize;
            if (texture == null || builtWidth != cellsWide || builtHeight != cellsHigh)
            {
                Regenerate(cellsWide, cellsHigh, overhangInCells);
                builtWidth = cellsWide;
                builtHeight = cellsHigh;
            }
            // The texture covers the plate PLUS the shadow margin, so the world size it is drawn
            // at has to include the margin too or the board would come out short.
            float span = (cellsWide + overhangInCells + 2f * Style.Margin) * cellSize;
            float spanY = (cellsHigh + overhangInCells + 2f * Style.Margin) * cellSize;
            plate.transform.localScale = new Vector3(span, spanY, 1f);
        }

        private void OnDestroy()
        {
            if (texture != null)
            {
                Destroy(texture);
                texture = null;
            }
        }

        private void Regenerate(int cellsWide, int cellsHigh, float overhangInCells)
        {
            int px = Mathf.Max(8, Style.Resolution);
            float spanX = cellsWide + overhangInCells + 2f * Style.Margin;
            float spanY = cellsHigh + overhangInCells + 2f * Style.Margin;
            int w = Mathf.Clamp(Mathf.RoundToInt(spanX * px), 32, 1024);
            int h = Mathf.Clamp(Mathf.RoundToInt(spanY * px), 32, 1024);

            if (texture != null)
            {
                Destroy(texture);
            }
            texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;

            // Cell space: the grid's own bottom-left corner is (0, 0).
            float originX = Style.Margin + overhangInCells * 0.5f;
            float originY = Style.Margin + overhangInCells * 0.5f;
            float plateHalfX = (cellsWide + overhangInCells) * 0.5f;
            float plateHalfY = (cellsHigh + overhangInCells) * 0.5f;

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float cy = (y + 0.5f) / h * spanY - originY;
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    float cx = (x + 0.5f) / w * spanX - originX;
                    pixels[row + x] = Shade(cx, cy, cellsWide, cellsHigh,
                        plateHalfX, plateHalfY);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            plate.sprite = Sprite.Create(texture, new Rect(0f, 0f, w, h),
                new Vector2(0.5f, 0.5f), Mathf.Max(w, h));
        }

        /// <summary>One pixel, in cell space with the grid's bottom-left corner at the origin.</summary>
        private static Color32 Shade(float cx, float cy, int cellsWide, int cellsHigh,
            float plateHalfX, float plateHalfY)
        {
            // ---- the plate, measured from its middle
            float mx = cx - cellsWide * 0.5f;
            float my = cy - cellsHigh * 0.5f;
            float plate = RoundedBox(mx, my, plateHalfX, plateHalfY, Style.PlateRadius);

            if (plate > 0f)
            {
                // Outside: nothing but the drop shadow, fading out.
                float s = Mathf.Clamp01(1f - plate / Mathf.Max(0.0001f, Style.ShadowWidth));
                float a = Mathf.Pow(s, 1.8f) * Style.ShadowAlpha;
                return new Color32(0, 0, 0, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
            }

            // ---- the surface: a lift towards the middle, plus grain
            float radial = Mathf.Sqrt(mx * mx + my * my)
                / Mathf.Max(0.0001f, Mathf.Max(plateHalfX, plateHalfY) * 1.42f);
            float lift = 1f + Style.Gradient * (1f - Mathf.Clamp01(radial) * 2f);
            float grain = (Hash01(Mathf.FloorToInt(cx * 7.3f * 16f),
                Mathf.FloorToInt(cy * 7.3f * 16f)) - 0.5f) * 2f * Style.Grain;
            var col = new Vector3(
                Style.Surface.r * lift + grain,
                Style.Surface.g * lift + grain,
                Style.Surface.b * lift + grain);

            // ---- the slot for whichever cell this pixel is in
            int ix = Mathf.FloorToInt(cx);
            int iy = Mathf.FloorToInt(cy);
            if (ix >= 0 && ix < cellsWide && iy >= 0 && iy < cellsHigh)
            {
                float lx = cx - ix - 0.5f;
                float ly = cy - iy - 0.5f;
                float slot = RoundedBox(lx, ly, Style.SlotHalf, Style.SlotHalf,
                    Style.SlotRadius);
                Vector2 n = RoundedBoxNormal(lx, ly, Style.SlotHalf, Style.SlotHalf,
                    Style.SlotRadius);
                // Inside a recess, the wall we see faces INWARD, so the lighting is the
                // outward normal negated.
                float facing = -(n.x * LightDir.x + n.y * LightDir.y);

                if (slot < 0f)
                {
                    col = new Vector3(Style.Slot.r, Style.Slot.g, Style.Slot.b);
                    float band = Mathf.Clamp01(
                        1f - Mathf.Min(-slot, Style.WallWidth) / Mathf.Max(0.0001f, Style.WallWidth));
                    if (facing < 0f)
                    {
                        col = Mix(col, Vector3.zero, -facing * Style.WallDark * band);
                    }
                    else
                    {
                        col = Mix(col, Vector3.one, facing * Style.WallLight * band);
                    }
                }
                else
                {
                    // The land just outside the slot, on the lit side: the cut edge.
                    float lip = Mathf.Clamp01(
                        1f - Mathf.Min(slot, Style.LipWidth) / Mathf.Max(0.0001f, Style.LipWidth));
                    if (facing > 0f)
                    {
                        col = Mix(col, Vector3.one, facing * Style.LipLight * lip);
                    }
                }
            }

            // ---- the plate's own bevel, at its outer edge
            float depth = -plate;
            if (depth < Style.FrameWidth)
            {
                Vector2 fn = RoundedBoxNormal(mx, my, plateHalfX, plateHalfY, Style.PlateRadius);
                float facing = fn.x * LightDir.x + fn.y * LightDir.y;
                float band = Mathf.Clamp01(
                    1f - depth / Mathf.Max(0.0001f, Style.FrameWidth));
                col = facing > 0f
                    ? Mix(col, Vector3.one, facing * Style.FrameLight * band)
                    : Mix(col, Vector3.zero, -facing * Style.FrameDark * band);
            }

            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(col.x * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(col.y * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(col.z * 255f), 0, 255),
                255);
        }

        private static Vector3 Mix(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return a + (b - a) * t;
        }

        /// <summary>Signed distance to a rounded box centred on the origin; negative inside.</summary>
        private static float RoundedBox(float px, float py, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px) - (hx - r);
            float qy = Mathf.Abs(py) - (hy - r);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f)
                + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>The outward normal of that box, analytically rather than by sampling the
        /// distance four more times - which is what this used to cost per pixel, on a texture
        /// with half a million of them.</summary>
        private static Vector2 RoundedBoxNormal(float px, float py, float hx, float hy, float r)
        {
            float sx = px < 0f ? -1f : 1f;
            float sy = py < 0f ? -1f : 1f;
            float qx = Mathf.Abs(px) - (hx - r);
            float qy = Mathf.Abs(py) - (hy - r);
            if (qx > 0f && qy > 0f)
            {
                // In a corner: the normal points out of the corner's arc centre.
                float len = Mathf.Sqrt(qx * qx + qy * qy);
                if (len < 1e-6f)
                {
                    return new Vector2(sx, 0f);
                }
                return new Vector2(qx / len * sx, qy / len * sy);
            }
            return qx > qy ? new Vector2(sx, 0f) : new Vector2(0f, sy);
        }

        private static float Hash01(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h % 100000u) / 100000f;
        }
    }
}
