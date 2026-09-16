// PURPOSE: The silhouettes "Deprem"'s collapse is made of, baked once.
//
// THE CRACK TOOK THREE BAKES AND THE FIRST TWO FAILED THE SAME WAY. A jagged shape with ONE flat
// edge reads, in 2D, as a SKYLINE: the first bake was a symmetric wedge with a single tip and
// stepped sides - a Christmas tree - and the second, an opening rising off a straight lip, was a
// mountain range. Neither is a crack, and both would have been read as an object sitting in the
// cell. A crack in the ground has NO flat edge: it is two broken plates pulling apart, ragged on
// BOTH sides, pointed at both ends, widest somewhere off its middle, with a few hairlines running
// on from it. That is Crack. See quake_cracks2.png / quake_collapse2.png in the scratchpad.
//
// IT IS NOT BLACK, NOT ROUND AND NOT A PORTAL. The inside is a deep warm charcoal, and what makes
// it visible at all on a dark slate cell is the FAR plate's edge catching the light - a thin pale
// dust rim. The first render left that out and the crack simply was not there.
//
// Every texture is square with the shape's own proportion baked in, so one sprite is one world unit
// in both axes and a scale in the view means what it says - the lesson "Harcama bonusu"'s receipt
// already paid for. Soft edges live in the alpha, and the colours are baked, so nothing here is
// tinted except the particles and the stress lines, which take the cube's own colour.

using UnityEngine;

namespace ProjectBlock.View
{
    public static class QuakeShapes
    {
        private const int Size = 128;

        public const int CrackVariants = 4;

        /// <summary>How far the crack's interior reaches from its axis at full opening, in its
        /// own box. The view reads it to put the ground line exactly on the near edge.</summary>
        public const float CrackHalfOpening = 0.11f;

        private static readonly Color CrackInside = new Color(0.045f, 0.043f, 0.042f);
        private static readonly Color FarRim = new Color(0.42f, 0.38f, 0.33f);
        private static readonly Color LedgeBody = new Color(0.19f, 0.17f, 0.155f);

        private static Sprite[] cracks;
        private static Sprite ledge;
        private static Sprite[] stress;
        private static Sprite dust;
        private static Sprite[] crumbs;
        private static Sprite shadow;
        private static Sprite ring;

        public static Sprite Crack(int variant)
        {
            if (cracks == null)
            {
                cracks = new Sprite[CrackVariants];
                for (int i = 0; i < CrackVariants; i++)
                {
                    float seed = 0.3f + i * 1.37f;
                    cracks[i] = Bake((u, v) => CrackAt(u, v, seed), true);
                }
            }
            return cracks[((variant % CrackVariants) + CrackVariants) % CrackVariants];
        }

        /// <summary>The ground's near edge riding up the cube's face: a dark ledge under a pale
        /// ragged lip, fading off at both ends. Its lip is at v = 0 of its box.</summary>
        public static Sprite Ledge
        {
            get
            {
                if (ledge == null)
                {
                    ledge = Bake(LedgeAt, true);
                }
                return ledge;
            }
        }

        public static Sprite Stress(int variant)
        {
            if (stress == null)
            {
                stress = new[] { Bake((u, v) => White(StressAt(u, v, 0.4f)), true),
                    Bake((u, v) => White(StressAt(u, v, 2.3f)), true) };
            }
            return stress[((variant % 2) + 2) % 2];
        }

        public static Sprite Dust
        {
            get
            {
                if (dust == null)
                {
                    dust = Bake((u, v) => White(DustAt(u, v)), true);
                }
                return dust;
            }
        }

        public static Sprite Crumb(int variant)
        {
            if (crumbs == null)
            {
                crumbs = new[]
                {
                    Bake((u, v) => White(Mathf.Min(Soft(0.30f - Mathf.Abs(u), 0.05f),
                        Soft(0.26f - Mathf.Abs(v), 0.05f))), true),
                    Bake((u, v) => White(Soft(0.36f - (Mathf.Abs(u) * 1.2f + Mathf.Abs(v) * 0.85f
                        + 0.05f * Mathf.Sin(u * 17f)), 0.05f)), true),
                    Bake((u, v) => White(Mathf.Min(Soft(0.30f - (v * 0.9f + Mathf.Abs(u) * 1.1f), 0.05f),
                        Soft(v + 0.30f, 0.05f))), true)
                };
            }
            return crumbs[((variant % 3) + 3) % 3];
        }

        /// <summary>A soft rounded square, for the cube's contact shadow.</summary>
        public static Sprite Shadow
        {
            get
            {
                if (shadow == null)
                {
                    shadow = Bake((u, v) =>
                    {
                        float d = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                        return White(Soft(0.5f - d, 0.14f));
                    }, true);
                }
                return shadow;
            }
        }

        /// <summary>A thin square outline, for the lab's debug marks only.</summary>
        public static Sprite Ring
        {
            get
            {
                if (ring == null)
                {
                    ring = Bake((u, v) =>
                    {
                        float d = 0.5f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                        return White(Mathf.Clamp01(Soft(d, 0.01f) - Soft(d - 0.05f, 0.01f)));
                    }, false);
                }
                return ring;
            }
        }

        // ---- the fields --------------------------------------------------------------------------

        private delegate Color Field(float u, float v);

        /// <summary>Three frequencies at awkward ratios: two at a simple one repeat, and a repeating
        /// ragged edge is a row of saw teeth - learned in the frost rim, then in the receipt.</summary>
        private static float Wig(float t, float seed)
        {
            return Mathf.Sin(t * 19f + seed * 6.28f) * 0.5f
                + Mathf.Sin(t * 33.7f - seed * 2.7f) * 0.32f
                + Mathf.Sin(t * 57.1f + seed * 4.1f) * 0.18f;
        }

        private static Color CrackAt(float u, float v, float seed)
        {
            // Along its own axis, turned a little per variant so a board of cracks is not ruled.
            float ang = ((seed * 37f) % 24f - 12f) * Mathf.Deg2Rad;
            float cu = u * Mathf.Cos(ang) + v * Mathf.Sin(ang);
            float cv = -u * Mathf.Sin(ang) + v * Mathf.Cos(ang);
            const float halfLength = 0.31f;
            float k = Mathf.Clamp01(Mathf.Abs(cu - 0.06f * Mathf.Sin(seed * 3.3f)) / halfLength);
            // Widest OFF its middle, and ragged on BOTH sides - the whole point (see the header).
            float peak = 0.5f + 0.5f * Mathf.Sin(cu * 4.1f + seed * 2f);
            float th = CrackHalfOpening * (0.55f + 0.45f * peak) * (1f - Mathf.Pow(k, 1.6f));
            float top = th * (0.85f + Wig(cu, seed) * 0.35f);
            float bot = th * (0.80f + Wig(cu, seed + 2.1f) * 0.35f);
            float inside = k < 1f ? Mathf.Min(Soft(top - cv, 0.006f), Soft(cv + bot, 0.006f)) : 0f;

            float hair = 0f;
            var rs = new System.Random((int)(seed * 7919f));
            for (int i = 0; i < 4; i++)
            {
                float end = rs.NextDouble() < 0.5 ? -1f : 1f;
                float x0 = end * halfLength * (0.55f + 0.4f * (float)rs.NextDouble());
                float a2 = ang + (float)(rs.NextDouble() * 110.0 - 55.0) * Mathf.Deg2Rad
                    + (end > 0f ? 0f : Mathf.PI);
                float length = 0.07f + 0.08f * (float)rs.NextDouble();
                float dx = Mathf.Cos(a2);
                float dy = Mathf.Sin(a2);
                float px = u - x0 * Mathf.Cos(ang);
                float py = v - x0 * Mathf.Sin(ang);
                float along = px * dx + py * dy;
                float across = Mathf.Abs(px * dy - py * dx + 0.012f * Mathf.Sin(along * 60f + i));
                if (along > 0f && along < length)
                {
                    float tt = along / length;
                    hair = Mathf.Max(hair, Soft(0.009f * (1f - tt) - across, 0.003f));
                }
            }
            float body = Mathf.Max(inside, hair);
            // The FAR plate's edge catches the light. Without it the crack is not visible at all.
            float rim = k < 1f && inside < 0.5f
                ? Soft(0.018f - Mathf.Abs(cv - top - 0.012f), 0.012f) * 0.6f
                : 0f;
            // Composite rim under body, premultiplied into one colour + alpha.
            float a = body + rim * (1f - body);
            if (a <= 0.0001f)
            {
                return new Color(0f, 0f, 0f, 0f);
            }
            Color c = (CrackInside * body + FarRim * rim * (1f - body)) / a;
            c.a = a;
            return c;
        }

        private static Color LedgeAt(float u, float v)
        {
            // Its lip at v = 0, the ledge below it, both ragged, both fading at the ends.
            float wob = 0.006f * Mathf.Sin(u * 31f) + 0.004f * Mathf.Sin(u * 53f + 1.3f);
            float ends = Soft(0.5f - Mathf.Abs(u), 0.06f);
            float lip = Soft(0.006f - Mathf.Abs(v - wob), 0.004f);
            float body = Mathf.Min(Soft(wob - v, 0.004f), Soft(v - (wob - 0.035f), 0.012f));
            float a = Mathf.Max(lip * 0.9f, body * 0.85f) * ends;
            Color c = lip > body ? FarRim : LedgeBody;
            c.a = a;
            return c;
        }

        private static float StressAt(float u, float v, float seed)
        {
            // A short PRESSURE mark, not a glass crack: tapered at both ends, bending twice.
            float along = Mathf.Clamp01((u + 0.42f) / 0.84f);
            float path = 0.05f * Mathf.Sin(along * 5.1f + seed) + 0.025f * Mathf.Sin(along * 11.3f - seed);
            float width = 0.03f * Mathf.Sin(along * Mathf.PI);
            return Mathf.Abs(u) < 0.42f ? Soft(width - Mathf.Abs(v - path), 0.012f) : 0f;
        }

        private static float DustAt(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float ang = Mathf.Atan2(v, u);
            float edge = 0.40f + 0.05f * Mathf.Sin(ang * 3f + 0.7f) + 0.03f * Mathf.Sin(ang * 5f - 1.1f);
            float a = Mathf.Clamp01(1f - r / edge);
            return a * a;
        }

        private static Color White(float a)
        {
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }

        private static float Soft(float d, float width)
        {
            return Mathf.Clamp01(d / Mathf.Max(width, 0.0001f));
        }

        private static Sprite Bake(Field field, bool bilinear)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = bilinear ? FilterMode.Bilinear : FilterMode.Point
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
