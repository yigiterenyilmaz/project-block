// PURPOSE: The pieces "Taşkın"'s overflow is drawn with, baked once: the LIQUID TONGUE that crosses
// the border, the SWELL the water piles into at the edge before it goes, and a BROKEN RIPPLE for the
// new water settling.
//
// THE TONGUE IS NOT A BEAM. It is wide at its root, narrows, and ends in a round drop - a liquid
// finding its way over an edge - with a dark aqua rim and a lighter middle; parallel sides would be
// a laser and a flat rectangle a UI strip. THE SWELL is a soft dome, pale at its rim. THE RIPPLE is a
// thin oval broken in two or three places, because a perfect bright ring is a target marker.
// Colours are baked from the water tile's own family (deep aqua, turquoise, pale aqua) and the view
// tints them only for alpha.

using UnityEngine;

namespace ProjectBlock.View
{
    public static class FloodShapes
    {
        private const int Size = 64;

        public static readonly Color Deep = new Color(0.16f, 0.50f, 0.62f);
        public static readonly Color Turquoise = new Color(0.32f, 0.70f, 0.78f);
        public static readonly Color Pale = new Color(0.72f, 0.93f, 0.95f);

        private static Sprite tongue;
        private static Sprite swell;
        private static Sprite[] ripples;

        /// <summary>Root at -x, tip at +x; one unit long, one unit tall (width is the y scale).
        /// </summary>
        public static Sprite Tongue
        {
            get
            {
                if (tongue == null)
                {
                    tongue = Bake((u, v) =>
                    {
                        float x = u + 0.5f;                       // 0 root .. 1 tip
                        const float tipR = 0.30f;
                        float half = Mathf.Lerp(0.48f, 0.26f, Mathf.Clamp01(x / (1f - tipR * 0.9f)));
                        float body = x <= 1f - tipR ? Mathf.Clamp01((half - Mathf.Abs(v)) / 0.05f) : 0f;
                        float tipD = Mathf.Sqrt((x - (1f - tipR)) * (x - (1f - tipR)) + v * v);
                        float tip = Mathf.Clamp01((tipR - tipD) / 0.05f);
                        float a = Mathf.Max(body, tip) * Mathf.Clamp01(x / 0.06f);
                        float across = Mathf.Clamp01(Mathf.Abs(v) / Mathf.Max(half, 0.01f));
                        Color c = Color.Lerp(Turquoise, Deep, Mathf.Pow(across, 2.5f));
                        c = Color.Lerp(c, Pale, Mathf.Clamp01(1f - Mathf.Abs(v + 0.08f) / 0.08f) * 0.5f);
                        c.a = a * 0.92f;
                        return c;
                    });
                }
                return tongue;
            }
        }

        public static Sprite Swell
        {
            get
            {
                if (swell == null)
                {
                    swell = Bake((u, v) =>
                    {
                        float d = Mathf.Sqrt(u * u * 4f + v * v * 4f); // 1 at the ellipse rim
                        float a = Mathf.Clamp01((1f - d) / 0.12f);
                        Color c = Color.Lerp(Turquoise, Pale, Mathf.Clamp01((d - 0.6f) / 0.4f));
                        c.a = a * 0.85f;
                        return c;
                    });
                }
                return swell;
            }
        }

        public static Sprite Ripple(int variant)
        {
            if (ripples == null)
            {
                ripples = new Sprite[2];
                for (int i = 0; i < 2; i++)
                {
                    float seed = 0.8f + i * 2.1f;
                    int gaps = 2 + i;
                    ripples[i] = Bake((u, v) =>
                    {
                        float r = Mathf.Sqrt(u * u + v * v * 1.3f) / 0.5f;
                        float band = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.07f);
                        float ang = Mathf.Atan2(v, u);
                        float broken = Mathf.Clamp01((Mathf.Sin(ang * gaps + seed) + 0.55f) / 0.35f);
                        return new Color(Pale.r, Pale.g, Pale.b, band * band * broken);
                    });
                }
            }
            return ripples[((variant % 2) + 2) % 2];
        }

        private delegate Color Field(float u, float v);

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
