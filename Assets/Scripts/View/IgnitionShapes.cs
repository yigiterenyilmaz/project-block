// PURPOSE: What "Tutuştur"'s burnout is drawn with that nothing else already bakes: the BURN MASK
// the IgnitionBurn shader reads, and a small flame LICK for the heat surge.
//
// THE MASK decides where a burning cube chars first. It is value noise at two scales (big patches,
// then smaller bites), pulled low toward the square's outline, so the char comes in from the edges
// and in a few irregular patches at once and the middle holds its heat longest - never a perfect
// radial wipe, which reads as a spotlight closing. It is DATA, so the texture is linear, and it is
// sampled in object space by the shader. Three variants, turned a quarter at a time per cube.
//
// THE LICK is a short tapered tongue, fat at its root and pointed at its tip, tinted by the view -
// small, because a burning block throws a flicker off its rim, not a bonfire.
// Smoke puffs, embers and ash reuse HazineShapes.Puff / Mote and QuarryShapes.Dust.

using UnityEngine;

namespace ProjectBlock.View
{
    public static class IgnitionShapes
    {
        private const int MaskSize = 64;
        public const int MaskVariants = 3;

        private static Texture2D[] masks;
        private static Sprite lick;

        public static Texture2D Mask(int variant)
        {
            if (masks == null)
            {
                masks = new Texture2D[MaskVariants];
                for (int i = 0; i < MaskVariants; i++)
                {
                    masks[i] = BakeMask(1301 + i * 977);
                }
            }
            return masks[((variant % MaskVariants) + MaskVariants) % MaskVariants];
        }

        public static Sprite Lick
        {
            get
            {
                if (lick == null)
                {
                    const int size = 48;
                    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    {
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear
                    };
                    var px = new Color[size * size];
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float u = (x + 0.5f) / size - 0.5f;
                            float v = (y + 0.5f) / size; // 0 root, 1 tip
                            float half = 0.26f * Mathf.Pow(1f - v, 0.8f) * Mathf.Clamp01(v * 6f);
                            float bend = 0.08f * Mathf.Sin(v * 3.2f);
                            float d = Mathf.Abs(u - bend);
                            float a = Mathf.Clamp01((half - d) / 0.05f);
                            float core = Mathf.Clamp01((half * 0.45f - d) / 0.04f) * (1f - v);
                            Color c = Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.9f, 0.55f), core);
                            c.a = a;
                            px[y * size + x] = c;
                        }
                    }
                    tex.SetPixels(px);
                    tex.Apply();
                    lick = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0f), size);
                }
                return lick;
            }
        }

        private static Texture2D BakeMask(int seed)
        {
            var rng = new System.Random(seed);
            float[,] big = Lattice(rng, 4);
            float[,] small = Lattice(rng, 9);
            var values = new float[MaskSize * MaskSize];
            float lo = float.MaxValue;
            float hi = float.MinValue;
            for (int y = 0; y < MaskSize; y++)
            {
                for (int x = 0; x < MaskSize; x++)
                {
                    float u = (x + 0.5f) / MaskSize;
                    float v = (y + 0.5f) / MaskSize;
                    float n = 0.6f * Sample(big, 4, u, v) + 0.4f * Sample(small, 9, u, v);
                    values[y * MaskSize + x] = n;
                    lo = Mathf.Min(lo, n);
                    hi = Mathf.Max(hi, n);
                }
            }
            var tex = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color[MaskSize * MaskSize];
            for (int y = 0; y < MaskSize; y++)
            {
                for (int x = 0; x < MaskSize; x++)
                {
                    float u = (x + 0.5f) / MaskSize - 0.5f;
                    float v = (y + 0.5f) / MaskSize - 0.5f;
                    float n = (values[y * MaskSize + x] - lo) / Mathf.Max(hi - lo, 0.0001f);
                    float edge = 1f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) / 0.5f; // 0 at the outline
                    float m = Mathf.Clamp01(n * 0.55f + edge * 0.45f);
                    px[y * MaskSize + x] = new Color(m, m, m, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static float[,] Lattice(System.Random rng, int cells)
        {
            var g = new float[cells + 1, cells + 1];
            for (int i = 0; i <= cells; i++)
            {
                for (int j = 0; j <= cells; j++)
                {
                    g[i, j] = (float)rng.NextDouble();
                }
            }
            return g;
        }

        private static float Sample(float[,] g, int cells, float u, float v)
        {
            float x = u * cells;
            float y = v * cells;
            int i = Mathf.Min((int)x, cells - 1);
            int j = Mathf.Min((int)y, cells - 1);
            float fx = x - i;
            float fy = y - j;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Mathf.Lerp(g[j, i], g[j, i + 1], fx);
            float b = Mathf.Lerp(g[j + 1, i], g[j + 1, i + 1], fx);
            return Mathf.Lerp(a, b, fy);
        }
    }
}
