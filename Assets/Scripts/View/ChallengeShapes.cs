// PURPOSE: The pieces "Meydan Okuma"'s contract is built from, baked once: a rail PLATE, the seal
// CLAMP at a line's end, the two HALVES of the wager token, a gold value SEED and a bronze SHARD.
//
// THE FIRST MOCK WAS A YELLOW RECTANGLE, and that is the one thing this effect must never be. Two
// continuous thin rails and a thin bridge across each end are a bounding box round the row,
// however nicely coloured. Three things undid it, and all three are here:
//   the rail is a CHAIN OF PLATES - one per cell, brightest in its middle, a hair of gap and a
//   bronze bead at every cell boundary - so it reads as a made thing lying in the seam;
//   the rail throws a faint warm light to ONE side only, the inside of the line (baked on +v; the
//   view turns the plate so +v faces in), which is what says "this line is held" without a fill;
//   and the ends are HEAVY - a ] bracket whose hooks reach back over the rail ends, with the seal
//   on its spine - so the weight is at the ends and the long sides stay fine. A frame of one
//   weight all the way round is an outline; a frame with heavy clamps is a mechanism.
// See challenge_mock*.png in the session scratchpad.
//
// THE TOKEN IS DARK WITH A GOLD RIM, not gold with an ivory number. Ivory on gold measures about
// 1.3:1 - the exact failure "Harcama bonusu"'s ivory stamp shipped with - so the face is a deep
// burnished cocoa, the rim carries the gold, and the ivory number reads at well over 10:1. It is
// baked as TWO halves cut along a slightly slanted, slightly ragged line, drawn side by side, so
// halving the wager is those halves physically coming apart rather than a number changing.
//
// Every texture is square with its shape's proportion baked in: one sprite is one world unit in
// both axes, and the view scales by a CELL. The rail is grey (tinted by urgency); the clamp and
// the token carry their own colours (the clamp is warmed by a tint).

using UnityEngine;

namespace ProjectBlock.View
{
    public static class ChallengeShapes
    {
        private const int Size = 128;

        /// <summary>Half the rail's thickness, in cells. 0.026 of a ~90 px cell is ~4.7 px total.
        /// </summary>
        public const float RailHalf = 0.026f;

        /// <summary>Where the clamp's spine sits in its own box, off its centre (a RIGHT-end clamp;
        /// the line is on its -u side). The view places the box so this lands just past the edge.
        /// </summary>
        public const float SpineU = -0.24f;

        /// <summary>The token's half extents in its box.</summary>
        public const float TokenHalfWidth = 0.40f;
        public const float TokenHalfHeight = 0.20f;

        public static readonly Color Bronze = new Color(0.34f, 0.21f, 0.11f);
        public static readonly Color BronzeLit = new Color(0.66f, 0.46f, 0.24f);
        public static readonly Color Gold = new Color(0.95f, 0.76f, 0.36f);
        public static readonly Color Ivory = new Color(1f, 0.93f, 0.74f);
        public static readonly Color Face = new Color(0.20f, 0.12f, 0.07f);
        public static readonly Color Rim = new Color(0.93f, 0.73f, 0.34f);

        private static Sprite[] rails;
        private static Sprite clamp;
        private static Sprite tokenLeft;
        private static Sprite tokenRight;
        private static Sprite seed;
        private static Sprite shard;

        /// <summary>One cell of rail; variant 1 carries a short break in its plate.</summary>
        public static Sprite Rail(int variant)
        {
            if (rails == null)
            {
                rails = new[] { Bake((u, v) => RailAt(u, v, false)), Bake((u, v) => RailAt(u, v, true)) };
            }
            return rails[((variant % 2) + 2) % 2];
        }

        public static Sprite Clamp
        {
            get
            {
                if (clamp == null)
                {
                    clamp = Bake(ClampAt);
                }
                return clamp;
            }
        }

        public static Sprite TokenHalf(bool left)
        {
            if (tokenLeft == null)
            {
                tokenLeft = Bake((u, v) => TokenAt(u, v, 1));
                tokenRight = Bake((u, v) => TokenAt(u, v, 2));
            }
            return left ? tokenLeft : tokenRight;
        }

        /// <summary>A small symmetrical gold seed: the paid wager on its way to the score.</summary>
        public static Sprite Seed
        {
            get
            {
                if (seed == null)
                {
                    seed = Bake((u, v) =>
                    {
                        float d = Mathf.Abs(u) + Mathf.Abs(v) * 1.35f;
                        float body = Soft(0.26f - d, 0.03f);
                        float halo = Mathf.Clamp01(1f - d / 0.48f);
                        float heart = Soft(0.09f - d, 0.03f);
                        float a = Mathf.Max(body, halo * halo * 0.35f);
                        Color c = Color.Lerp(Gold, Ivory, heart);
                        c.a = a;
                        return c;
                    });
                }
                return seed;
            }
        }

        /// <summary>A small angular gold-bronze plate chip, for the rail breaking.</summary>
        public static Sprite Shard
        {
            get
            {
                if (shard == null)
                {
                    shard = Bake((u, v) =>
                    {
                        float d = Mathf.Min(Mathf.Min(0.30f - (u * 0.9f + v * 0.5f), 0.26f - (-u * 0.8f + v * 0.7f)),
                            Mathf.Min(0.18f - Mathf.Abs(v), 0.36f - Mathf.Abs(u)));
                        float a = Soft(d, 0.02f);
                        Color c = Color.Lerp(Bronze, Gold, Mathf.Clamp01(0.5f + v * 3f));
                        c.a = a;
                        return c;
                    });
                }
                return shard;
            }
        }

        // ---- fields ----------------------------------------------------------------------------

        private delegate Color Field(float u, float v);

        private static Color RailAt(float u, float v, bool broken)
        {
            float d = Mathf.Abs(v);
            float plateLength = Soft(0.5f - 0.055f - Mathf.Abs(u), 0.012f);
            if (broken)
            {
                plateLength *= Soft(Mathf.Abs(u - 0.12f) - 0.05f, 0.01f);
            }
            float along = 0.86f + 0.14f * Mathf.Cos(u * Mathf.PI * 1.6f);
            float across = Mathf.Clamp01(1f - d / RailHalf);
            float core = Soft(RailHalf - d, 0.007f) * plateLength;
            float lum = (0.45f + 0.55f * Mathf.Pow(across, 1.3f)) * along;
            // A dark keel under the plate, so it separates from a bright cube beside it.
            float keel = Soft(RailHalf + 0.012f - d, 0.006f) * plateLength * 0.85f;
            // The inside light: +v only, fading well before the middle of the cell.
            float inner = v > 0f ? Mathf.Pow(Mathf.Clamp01(1f - v / 0.20f), 2.2f) * 0.20f
                * Soft(0.5f - Mathf.Abs(u), 0.05f) : 0f;
            float beadR = Mathf.Sqrt((Mathf.Abs(u) - 0.5f) * (Mathf.Abs(u) - 0.5f) + v * v);
            float bead = Soft(0.036f - beadR, 0.008f);
            float beadLum = 0.55f + 0.45f * Mathf.Clamp01(1f - beadR / 0.036f);

            Color c = new Color(1f, 1f, 1f, inner);
            c = Over(new Color(0.05f, 0.03f, 0.02f, keel), c);
            c = Over(new Color(lum, lum, lum, core), c);
            c = Over(new Color(beadLum, beadLum, beadLum, bead), c);
            return c;
        }

        private static float RoundedBox(float u, float v, float cx, float cy, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(u - cx) - (hw - r);
            float qy = Mathf.Abs(v - cy) - (hh - r);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                + Mathf.Min(Mathf.Max(qx, qy), 0f);
            return r - outside; // > 0 inside
        }

        private static Color Metal(float sd, float v)
        {
            float shade = Mathf.Clamp01(0.5f + v / 0.5f);
            Color body = Bronze * (0.75f + 0.6f * shade);
            float rim = Mathf.Clamp01(1f - sd / 0.02f);
            return body + (BronzeLit - Bronze) * rim;
        }

        private static Color ClampAt(float u, float v)
        {
            Color c = new Color(0f, 0f, 0f, 0f);
            // The spine: thick at the seal, thinner toward the hooks.
            float halfW = 0.030f + 0.020f * Mathf.Clamp01(1f - Mathf.Abs(v) / 0.5f);
            float spine = Mathf.Min(halfW - Mathf.Abs(u - SpineU), 0.52f - Mathf.Abs(v));
            // The hooks: short arms at the two rails, reaching back over the rail ends.
            float hook = Mathf.Max(RoundedBox(u, v, SpineU - 0.12f, 0.5f, 0.15f, 0.036f, 0.034f),
                RoundedBox(u, v, SpineU - 0.12f, -0.5f, 0.15f, 0.036f, 0.034f));
            float frame = Mathf.Max(spine, hook);
            c = Over(WithAlpha(Metal(frame, v), Soft(frame, 0.007f)), c);
            // The seal: a pad on the spine with a drop shadow, a gold slit and an ivory core.
            float px = SpineU + 0.02f;
            float pad = RoundedBox(u, v, px, 0f, 0.115f, 0.19f, 0.07f);
            c = Over(new Color(0.05f, 0.03f, 0.02f, Soft(pad + 0.018f, 0.01f) * 0.6f), c);
            c = Over(WithAlpha(Metal(pad, v), Soft(pad, 0.007f)), c);
            float slit = Soft(0.02f - Mathf.Abs(u - px), 0.006f) * Soft(0.12f - Mathf.Abs(v), 0.01f);
            c = Over(WithAlpha(Gold, slit), c);
            float r = Mathf.Sqrt((u - px) * (u - px) + v * v);
            c = Over(WithAlpha(Ivory, Soft(0.04f - r, 0.01f)), c);
            return c;
        }

        /// <summary>The cut the token comes apart along, as u at height v.</summary>
        public static float CutU(float v)
        {
            return 0.012f * Mathf.Sin(v * 40f) + v * 0.18f;
        }

        private static Color TokenAt(float u, float v, int half)
        {
            const float point = 0.13f;
            float edge = TokenHalfHeight * Mathf.Clamp01((TokenHalfWidth - Mathf.Abs(u)) / point);
            float sd = Mathf.Min(edge - Mathf.Abs(v), TokenHalfWidth - Mathf.Abs(u));
            float a = Soft(sd, 0.008f);
            float cut = CutU(v);
            a *= half == 1 ? Soft(cut - u, 0.004f) : Soft(u - cut, 0.004f);
            if (a <= 0f)
            {
                return new Color(0f, 0f, 0f, 0f);
            }
            float rim = Mathf.Clamp01(1f - sd / 0.03f);
            float inset = Mathf.Clamp01(1f - Mathf.Abs(sd - 0.05f) / 0.006f) * 0.6f;
            float grad = 0.85f + 0.4f * Mathf.Clamp01(v / 0.2f + 0.5f);
            Color c = Color.Lerp(Face * grad, Rim, rim);
            c = Color.Lerp(c, Rim * 0.7f, inset);
            c.a = a;
            return c;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        /// <summary>Straight-alpha "over".</summary>
        private static Color Over(Color top, Color bottom)
        {
            float a = top.a + bottom.a * (1f - top.a);
            if (a <= 0.00001f)
            {
                return new Color(0f, 0f, 0f, 0f);
            }
            Color c = (top * top.a + bottom * bottom.a * (1f - top.a)) / a;
            c.a = a;
            return c;
        }

        private static float Soft(float d, float width)
        {
            return Mathf.Clamp01(d / Mathf.Max(width, 0.0001f));
        }

        private static Sprite Bake(Field field)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size - 0.5f;
                    float v = (y + 0.5f) / Size - 0.5f;
                    px[y * Size + x] = field(u, v);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
