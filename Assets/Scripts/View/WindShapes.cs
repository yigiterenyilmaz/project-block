// PURPOSE: The silhouettes "Rüzgar" is drawn with, baked once and shared by the aim and the gust:
// a STREAK (a gust line - bright at its head, thinning to nothing behind, so it reads as air
// moving and never as a laser), the BAND (the lane the wind takes - soft on every edge, because a
// hard-edged plate over the grid is a selection box), a thin RING for the cells the wind will
// touch, and a CHEVRON that says which way it blows.

using UnityEngine;

namespace ProjectBlock.View
{
    internal static class WindShapes
    {
        private static Sprite streak;
        private static Sprite band;
        private static Sprite ring;
        private static Sprite chevron;

        /// <summary>One unit long (x, head at +x), a quarter unit thick.</summary>
        public static Sprite Streak
        {
            get
            {
                if (streak == null)
                {
                    streak = Bake("WindStreak", 128, 32, delegate (float u, float v)
                    {
                        // u 0..1 tail to head, v -1..1 across.
                        float along = Mathf.SmoothStep(0f, 1f, u / 0.8f)
                            * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.86f) / 0.14f));
                        // thinner toward the tail, so it tapers rather than ends
                        float taper = Mathf.Lerp(0.3f, 0.75f, u);
                        return along * Mathf.Exp(-(v * v) / (taper * taper) * 2.2f);
                    });
                }
                return streak;
            }
        }

        /// <summary>The lane: one unit square, soft on all four sides.</summary>
        public static Sprite Band
        {
            get
            {
                if (band == null)
                {
                    band = Bake("WindBand", 64, 64, delegate (float u, float v)
                    {
                        float across = 1f - Mathf.SmoothStep(0f, 1f, (Mathf.Abs(v) - 0.5f) / 0.5f);
                        float along = 1f - Mathf.SmoothStep(0f, 1f, (Mathf.Abs(u * 2f - 1f) - 0.82f) / 0.18f);
                        return across * along;
                    });
                }
                return band;
            }
        }

        /// <summary>A thin ring, one unit across.</summary>
        public static Sprite Ring
        {
            get
            {
                if (ring == null)
                {
                    ring = Bake("WindRing", 64, 64, delegate (float u, float v)
                    {
                        float x = u * 2f - 1f;
                        float d = Mathf.Sqrt(x * x + v * v);
                        return Mathf.Clamp01(1f - Mathf.Abs(d - 0.84f) / 0.09f);
                    });
                }
                return ring;
            }
        }

        /// <summary>An open arrowhead pointing +x, one unit across.</summary>
        public static Sprite Chevron
        {
            get
            {
                if (chevron == null)
                {
                    chevron = Bake("WindChevron", 64, 64, delegate (float u, float v)
                    {
                        float x = u * 2f - 1f;
                        // two strokes meeting at (0.55, 0): a ">" with rounded ends
                        float d = Mathf.Min(Segment(x, v, -0.45f, 0.75f, 0.55f, 0f),
                            Segment(x, v, -0.45f, -0.75f, 0.55f, 0f));
                        return Mathf.Clamp01(1f - (d - 0.1f) / 0.08f);
                    });
                }
                return chevron;
            }
        }

        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax;
            float vy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy));
            float dx = px - (ax + vx * t);
            float dy = py - (ay + vy * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private delegate float Field(float u, float v);

        private static Sprite Bake(string name, int w, int h, Field field)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[w * h];
            for (int j = 0; j < h; j++)
            {
                float v = -1f + (j + 0.5f) * 2f / h;
                for (int i = 0; i < w; i++)
                {
                    float u = (i + 0.5f) / w;
                    pixels[j * w + i] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(field(u, v)) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            Sprite made = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), w);
            made.hideFlags = HideFlags.HideAndDontSave;
            return made;
        }
    }
}
