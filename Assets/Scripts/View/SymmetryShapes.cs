// PURPOSE: The few soft shapes "Simetri"'s payout is lit with, baked once into small white textures
// (the colour is the renderer's): a round GLOW, a four-point crystal FLARE (the "lock"), a thin RING
// and a short SPARK. Generated rather than painted because they are light, not objects - and the
// strip meshes (traces, beams, threads) need no texture at all: their soft edges are written into
// their vertex alphas.
//
// The flare is the one shape with a rule in it: long thin rays on the two axes and short faint ones
// on the diagonals, never a disc - a disc in the middle of a board cell is a status light, and this
// has to read as two things snapping together.

using UnityEngine;

namespace ProjectBlock.View
{
    public static class SymmetryShapes
    {
        private static Sprite glow;
        private static Sprite flare;
        private static Sprite ring;
        private static Sprite spark;

        public static Sprite Glow
        {
            get { return glow ?? (glow = Bake("SymGlow", 64, (x, y) => Mathf.Exp(-(x * x + y * y) / 0.11f))); }
        }

        public static Sprite Flare
        {
            get
            {
                return flare ?? (flare = Bake("SymFlare", 128, (x, y) =>
                {
                    float r2 = x * x + y * y;
                    float core = Mathf.Exp(-r2 / 0.006f) + 0.45f * Mathf.Exp(-r2 / 0.03f);
                    // the two long rays, thin across, fading along
                    float rayH = Mathf.Exp(-(y * y) / 0.0006f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(x)), 2.2f);
                    float rayV = Mathf.Exp(-(x * x) / 0.0006f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y)), 2.2f);
                    // and two short faint ones on the diagonals
                    float u = (x + y) * 0.7071f;
                    float v = (x - y) * 0.7071f;
                    float diag = 0.35f * (Mathf.Exp(-(u * u) / 0.0005f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v) * 2.2f), 2f)
                        + Mathf.Exp(-(v * v) / 0.0005f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u) * 2.2f), 2f));
                    return Mathf.Clamp01(core + rayH + rayV + diag);
                }));
            }
        }

        public static Sprite Ring
        {
            get
            {
                return ring ?? (ring = Bake("SymRing", 128, (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    float d = (r - 0.8f) / 0.055f;
                    return Mathf.Exp(-d * d) + 0.12f * Mathf.Exp(-((r - 0.62f) / 0.18f) * ((r - 0.62f) / 0.18f));
                }));
            }
        }

        public static Sprite Spark
        {
            get { return spark ?? (spark = Bake("SymSpark", 64, (x, y) => Mathf.Exp(-(x * x) / 0.12f - (y * y) / 0.004f))); }
        }

        /// <summary>A white texture of <paramref name="size"/> pixels whose alpha is
        /// <paramref name="shape"/>(x, y) over -1..1, as a sprite one world unit across.</summary>
        private static Sprite Bake(string name, int size, System.Func<float, float, float> shape)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float x = (px + 0.5f) / size * 2f - 1f;
                    float y = (py + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(shape(x, y));
                    // the very edge of the texture is always clear, so nothing shows a seam
                    float edge = Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(x), Mathf.Abs(y))) * size * 0.5f);
                    pixels[py * size + px] = new Color32(255, 255, 255, (byte)(a * edge * 255f + 0.5f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            Sprite s = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            s.name = name;
            return s;
        }
    }
}
