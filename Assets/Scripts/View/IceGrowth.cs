// PURPOSE: The field a freezing cube is drawn from - baked once, four variants, one texture.
//
// "Buzluk"'s first pass was a directional tint wipe: one progress value, one lerp of the whole
// face toward one ice colour behind a moving line. It read as the water being RECOLOURED, which
// is the one thing the effect must not say. What has to be shown is a CRUST GROWING: frost buds
// on the wall, crystal fingers reaching into the water, a thin film closing the gaps between
// them, and the last liquid trapped in a shrinking irregular pocket until it too goes.
//
// None of that can be a formula in a fragment shader, so it is BAKED, the way TalismanStain bakes
// its claim and CryoSublimationView bakes its masks. Four channels, and each one is a WHEN rather
// than a what:
//
//   R  FINGER ARRIVAL   when a crystal finger reaches this texel (1 = never)
//   G  FILM ARRIVAL     when the thin ice film seals it
//   B  CLOUD            low-frequency milkiness, static - the ice body's own thickness variation
//   A  RIM              the frosted crystal edge, and THICKER on the wall side
//
// So the animation is three thresholds against three progress values, and the LIQUID POCKET is
// not drawn at all: it is whatever the film has not reached yet. That is why it comes out
// irregular and off-centre instead of a shrinking disc - nobody chose its shape.
//
// G IS A GEODESIC OUT OF THE FINGER FIELD, not a blur: the film has to close the gaps between
// fingers from their sides, at a steady rate, the way ice actually spreads across water. A blur
// would fade it in everywhere at once.
//
// BAKED FOR ONE WALL - on the LEFT, fingers growing +u. Every other wall is a uv swizzle in the
// shader, and a CORNER samples two tiles and takes MIN of the two arrivals, so two crystal fields
// grow and meet in the middle without either being coded for.
//
// Three things were got wrong on paper and fixed by rendering them (the mock is in the
// scratchpad; ice_field / ice_growth / ice_final.png):
//   * THE GEODESIC MUST NOT WRAP. Written with a periodic roll, the film front left by one edge
//     of the tile and came back in the other side. The tile's borders are the cube's borders.
//   * A FINGER'S HEADING NEEDS MEMORY. A plain random walk wanders through two radians over
//     seventy steps and the finger spirals back on itself, so the heading is pulled toward "into
//     the cube" every step.
//   * THE SOFT EDGE MUST BE BOUNDED. Writing arrival as a plain distance conflates "soft edge"
//     with "grows forever": at the end of the freeze every finger was a quarter of the cell wide
//     and the picture was soft lobes advancing, with no filaments left in it.

using UnityEngine;

namespace ProjectBlock.View
{
    public static class IceGrowth
    {
        /// <summary>Texels per tile. Small on purpose - this is a field, not artwork, and it is
        /// sampled across one cell.</summary>
        public const int Size = 96;

        /// <summary>How many different crystal fields exist. Chosen per cube from its own
        /// position, so a wall of ice is never the same drawing four times.</summary>
        public const int Variants = 4;

        /// <summary>How much of a finger's arrival range its soft edge spends. The shader
        /// thresholds with the same number, which is what makes the edge soft rather than
        /// stepped.</summary>
        public const float EdgeBand = 0.055f;

        /// <summary>The share of the timeline the SEED BUD occupies - the root of every finger,
        /// revealed by its own progress before the fingers grow.</summary>
        public const float SeedShare = 0.14f;

        private const float SoftTexels = 2.0f;

        /// <summary>
        /// How much further in the frost reaches on the WALL side. Was 1.42, which put a twelfth
        /// of the cell of near-white along that edge and made the cube read as a larger block
        /// than its neighbour - which is the one thing a frozen cube must not do.
        /// </summary>
        private const float WallRimBias = 1.28f;

        /// <summary>
        /// The frost rim, as a share of the cell. Was 0.085 - about 5px on a 64px cell, and with
        /// the wall bias on top of it 7px down one side.
        ///
        /// IT NEVER SCALED ANYTHING: the rim is drawn inside the tile's own uv, so it cannot
        /// spill past the sprite by a single pixel. It still made the cube look bigger, because
        /// a bright band all the way round a dark cube is read as bulk. That is why this is a
        /// SILHOUETTE number and not a decoration one.
        /// </summary>
        private const float RimThickness = 0.050f;

        private static Texture2D field;

        /// <summary>
        /// The baked field, four variants side by side (Size*Variants x Size).
        ///
        /// LINEAR, because all four channels are DATA and not colour - the same trap
        /// TalismanStain records. An sRGB decode here would bend every arrival time and the
        /// freeze would run at the wrong rate through its own middle.
        /// </summary>
        public static Texture2D Field
        {
            get
            {
                if (field == null)
                {
                    field = Bake();
                }
                return field;
            }
        }

        private static Texture2D Bake()
        {
            int w = Size * Variants;
            var tex = new Texture2D(w, Size, TextureFormat.RGBA32, false, true)
            {
                name = "IceGrowthField",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[w * Size];
            for (int v = 0; v < Variants; v++)
            {
                float[] finger = new float[Size * Size];
                for (int i = 0; i < finger.Length; i++)
                {
                    finger[i] = 1f;
                }
                var rnd = new Lcg(9871u + (uint)v * 7919u);
                GrowFingers(finger, rnd);

                float[] film = Geodesic(finger);
                Wander(film, v);

                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float u = (x + 0.5f) / Size;
                        float vv = (y + 0.5f) / Size;
                        int at = y * Size + x;
                        pixels[y * w + v * Size + x] = new Color(
                            finger[at], film[at], Cloud(u, vv, v), Rim(u, vv, v));
                    }
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        // ---- the fingers ---------------------------------------------------------------------

        private static void GrowFingers(float[] arrival, Lcg rnd)
        {
            int seeds = 4 + (int)(rnd.Next() * 2.99f);
            int hero = (int)(rnd.Next() * seeds);
            for (int i = 0; i < seeds; i++)
            {
                float sy = (i + 0.5f) / seeds + (rnd.Next() - 0.5f) * 0.18f;
                sy = Mathf.Clamp(sy, 0.06f, 0.94f);
                // ONE HERO reaches most of the way and the rest stop short (spec 19). Fingers of
                // one length read as a comb, which is what a fan of equal spokes always is.
                float length = i == hero
                    ? 0.80f + rnd.Next() * 0.14f
                    : 0.30f + rnd.Next() * 0.32f;
                Walk(arrival, rnd, 0f, sy * Size, (rnd.Next() - 0.5f) * 0.95f,
                    length, Size * 0.018f, SeedShare, 1f, 0);
            }
        }

        private static void Walk(float[] arrival, Lcg rnd, float x, float y, float ang,
            float length, float width, float t0, float t1, int depth)
        {
            float curve = (rnd.Next() - 0.5f) * 0.010f;
            int steps = Mathf.Max(6, (int)(length * Size));
            int forkAt = depth < 1 && steps > 14
                ? (int)(steps * (0.36f + rnd.Next() * 0.22f))
                : -1;
            for (int i = 0; i < steps; i++)
            {
                float s = i / (float)(steps - 1);
                // PULLED BACK TOWARD THE INTERIOR every step. Without this the heading has no
                // memory and a long finger spirals - see the file header.
                ang += curve + (rnd.Next() - 0.5f) * 0.020f - ang * 0.018f;
                x += Mathf.Cos(ang);
                y += Mathf.Sin(ang);
                if (x < 0f || x >= Size || y < 0f || y >= Size)
                {
                    return;
                }
                // Wide at the root - that swelling IS the frost seed - and a hair at the tip.
                float bud = 1f + 0.85f * Mathf.Max(0f, 1f - s / SeedShare);
                Stamp(arrival, x, y, width * (1f - 0.86f * s) * bud, t0 + (t1 - t0) * s);
                if (i == forkAt)
                {
                    // ONE branch. Two is a root and three is a fern - the same budget the
                    // talisman's shadow tendrils are held to.
                    Walk(arrival, rnd, x, y,
                        ang + (0.5f + rnd.Next() * 0.5f) * (rnd.Next() > 0.5f ? 1f : -1f),
                        length * (0.34f + rnd.Next() * 0.22f), width * 0.55f,
                        t0 + (t1 - t0) * s, t1, depth + 1);
                }
            }
        }

        /// <summary>One step of a finger. Arrival is t on the spine, ramps over SoftTexels, and
        /// then STOPS - past that this texel is not finger at all and the film is what covers it.
        /// Min-combined, so a crossing keeps the earlier arrival.</summary>
        private static void Stamp(float[] arrival, float cx, float cy, float radius, float t)
        {
            float far = radius + SoftTexels;
            int x0 = Mathf.Max(0, (int)(cx - far - 1));
            int x1 = Mathf.Min(Size, (int)(cx + far + 2));
            int y0 = Mathf.Max(0, (int)(cy - far - 1));
            int y1 = Mathf.Min(Size, (int)(cy + far + 2));
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > far)
                    {
                        continue;
                    }
                    float val = t + Mathf.Clamp01((d - radius) / SoftTexels) * EdgeBand;
                    int at = y * Size + x;
                    if (val < arrival[at])
                    {
                        arrival[at] = val;
                    }
                }
            }
        }

        // ---- the film ------------------------------------------------------------------------

        /// <summary>
        /// WHEN THE FILM SEALS EACH TEXEL: a geodesic out of the finger field at a steady cost,
        /// by chamfer sweeps rather than by iteration - three passes each way is close enough for
        /// a field sampled across one cell, and it is two orders of magnitude cheaper.
        ///
        /// It must not wrap: the tile's borders are the cube's, and a front that leaves one side
        /// and comes back in the other seals the far corner from the wrong direction.
        /// </summary>
        private static float[] Geodesic(float[] finger)
        {
            var g = (float[])finger.Clone();
            const float Step = 1.15f / Size;
            float diag = Step * 1.41421f;
            for (int pass = 0; pass < 3; pass++)
            {
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        int at = y * Size + x;
                        float best = g[at];
                        if (x > 0) { best = Mathf.Min(best, g[at - 1] + Step); }
                        if (y > 0) { best = Mathf.Min(best, g[at - Size] + Step); }
                        if (x > 0 && y > 0) { best = Mathf.Min(best, g[at - Size - 1] + diag); }
                        if (x < Size - 1 && y > 0)
                        {
                            best = Mathf.Min(best, g[at - Size + 1] + diag);
                        }
                        g[at] = best;
                    }
                }
                for (int y = Size - 1; y >= 0; y--)
                {
                    for (int x = Size - 1; x >= 0; x--)
                    {
                        int at = y * Size + x;
                        float best = g[at];
                        if (x < Size - 1) { best = Mathf.Min(best, g[at + 1] + Step); }
                        if (y < Size - 1) { best = Mathf.Min(best, g[at + Size] + Step); }
                        if (x < Size - 1 && y < Size - 1)
                        {
                            best = Mathf.Min(best, g[at + Size + 1] + diag);
                        }
                        if (x > 0 && y < Size - 1)
                        {
                            best = Mathf.Min(best, g[at + Size - 1] + diag);
                        }
                        g[at] = best;
                    }
                }
            }
            return g;
        }

        /// <summary>Leans the sealing away from the wall and WANDERS it, then renormalises. The
        /// wander is what breaks the last liquid into a pocket: the geodesic on its own seals the
        /// far side last in a straight band, which reads as a second wipe.</summary>
        private static void Wander(float[] film, int variant)
        {
            float lo = float.MaxValue;
            float hi = float.MinValue;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size;
                    float v = (y + 0.5f) / Size;
                    int at = y * Size + x;
                    film[at] += u * 0.09f + (Noise(u, v, 3f, variant * 31 + 5) - 0.5f) * 0.34f;
                    if (film[at] < lo) { lo = film[at]; }
                    if (film[at] > hi) { hi = film[at]; }
                }
            }
            float span = Mathf.Max(hi - lo, 0.0001f);
            for (int i = 0; i < film.Length; i++)
            {
                film[i] = (film[i] - lo) / span;
            }
        }

        // ---- the static channels ---------------------------------------------------------------

        private static float Cloud(float u, float v, int variant)
        {
            return 0.6f * Noise(u, v, 3f, variant * 17 + 1)
                + 0.4f * Noise(u, v, 6f, variant * 17 + 2);
        }

        /// <summary>
        /// The frosted crystal edge, on all four sides but THICKER ON THE WALL (spec 7): even in
        /// the finished cube the side the cold came from has to stay readable.
        ///
        /// Each edge's roughness runs along its OWN axis with THREE non-harmonic frequencies.
        /// Two at a simple ratio repeat, and a repeating edge along a straight run is a row of
        /// saw teeth - learned twice now, once in the shader and once in the bake.
        /// </summary>
        private static float Rim(float u, float v, int variant)
        {
            float edge = Mathf.Min(
                Mathf.Min(u / WallRimBias + Crust(v, variant + 0f),
                    (1f - u) + Crust(v, variant + 1.7f)),
                Mathf.Min(v + Crust(u, variant + 3.1f),
                    (1f - v) + Crust(u, variant + 4.9f)));
            float rim = Mathf.Clamp01(1f - edge / RimThickness);
            return rim * rim * (3f - 2f * rim);
        }

        private static float Crust(float t, float seed)
        {
            return (Mathf.Sin(t * 9f + seed * 6.283f) * 0.5f
                + Mathf.Sin(t * 14.3f - seed * 2.7f) * 0.32f
                + Mathf.Sin(t * 23.7f + seed * 4.1f) * 0.18f) * (RimThickness * 0.30f);
        }

        /// <summary>Low-frequency value noise. Clouds, never circles.</summary>
        private static float Noise(float u, float v, float freq, int seed)
        {
            float x = u * freq;
            float y = v * freq;
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float fx = x - xi;
            float fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi, seed);
            float b = Hash(xi + 1, yi, seed);
            float c = Hash(xi, yi + 1, seed);
            float d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffffff) / (float)0xffffff;
        }

        /// <summary>Deterministic, and NOT UnityEngine.Random: the field must be the same field
        /// on every machine and every run, or a saved board comes back wearing different ice.
        /// </summary>
        private sealed class Lcg
        {
            private uint state;

            public Lcg(uint seed)
            {
                state = seed;
            }

            public float Next()
            {
                state = state * 1664525u + 1013904223u;
                return state / (float)uint.MaxValue;
            }
        }
    }
}
