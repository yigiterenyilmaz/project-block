// PURPOSE: "Kara Delik" - the few small generated sprites its effects are drawn with: a soft dot (motes,
// glows, dust, the gravity corridor), a soft thin ring (the collapse's gravitational break, the voided
// pile's collapsed residue) and a dull shard (the collapse's material fragments). Baked once, linear,
// no texture assets. The hole itself is procedural (Resources/Shaders/BlackHole) and needs none.

using UnityEngine;

namespace ProjectBlock.View
{
    public static class BlackHoleShapes
    {
        private static Sprite dot;
        private static Sprite ring;
        private static Sprite shard;
        private static Sprite quad;

        /// <summary>A round gaussian blob, one unit across, fading to nothing at its edge.</summary>
        public static Sprite Dot
        {
            get
            {
                if (dot == null)
                {
                    dot = Bake(64, delegate(float x, float y)
                    {
                        float r = Mathf.Sqrt(x * x + y * y) * 2f;
                        return Mathf.Clamp01(Mathf.Exp(-r * r * 4.5f) * 1.02f - 0.02f);
                    });
                }
                return dot;
            }
        }

        /// <summary>A thin soft ring on the unit square's rim (radius 0.44, width ~0.05).</summary>
        public static Sprite Ring
        {
            get
            {
                if (ring == null)
                {
                    ring = Bake(256, delegate(float x, float y)
                    {
                        float r = Mathf.Sqrt(x * x + y * y);
                        float d = (r - 0.44f) / 0.028f;
                        return Mathf.Exp(-d * d);
                    });
                }
                return ring;
            }
        }

        /// <summary>A small dull angular fragment - a quad with one corner knocked in.</summary>
        public static Sprite Shard
        {
            get
            {
                if (shard == null)
                {
                    shard = Bake(32, delegate(float x, float y)
                    {
                        // Inside a rhombus-ish polygon: |x| + |y| * 1.3 < 0.46, cut by one diagonal.
                        float body = 0.46f - (Mathf.Abs(x) * 1.1f + Mathf.Abs(y) * 0.9f);
                        float cut = 0.30f - (x + y);
                        return Mathf.Clamp01(Mathf.Min(body, cut) * 40f);
                    });
                }
                return shard;
            }
        }

        /// <summary>A plain white quad whose object space is exactly the unit square around the
        /// centre - the hole's shader measures positions off it.</summary>
        public static Sprite Quad
        {
            get
            {
                if (quad == null)
                {
                    quad = Bake(4, delegate(float x, float y) { return 1f; });
                }
                return quad;
            }
        }

        private static Sprite Bake(int size, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    float x = (i + 0.5f) / size - 0.5f;
                    float y = (j + 0.5f) / size - 0.5f;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y)) * 255f);
                    pixels[j * size + i] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size,
                0, SpriteMeshType.FullRect);
        }
    }
}
