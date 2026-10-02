// PURPOSE: The silhouettes "Rüzgar" is drawn with, baked once and shared by the aim, the storm and
// every reaction. Each is defined by what it must NOT be:
//   ARC       a thin curved stroke with tapered ends - the pressure knot's arcs and the endpoint's
//             "pressure nose". Never a crosshair, never an arrowhead.
//   WISP      a short air stroke that thins to nothing behind its head - never a line with ends.
//   SHARD     the storm's mote: a soft sliver, 1-3 px on screen. Not a round dot, not a leaf.
//   SCRAP     a torn piece of flame - a bulb that tapers to a bent point. The brief's own rule:
//             no round particle dots for fire.
//   DROPLET   a teardrop, for the water's tail and its arrival.
//   EDGE      a light that lies along ONE side of a cube and dies inside it - the windward edge a
//             passing front catches. Clipped to the cube's own rounded square, so it can never
//             spill onto a neighbour.
//   ESSENCE   the piece of infection the wind tears off: a lobed dark-green nucleus with a lime
//             rim, baked in colour. Organic, never a glowing orb.
//   BRANCH    a thin stain that grows from a contact point - rooted at its own left end.
//   SMEAR     a soft long ellipse: warm air behind an ember, wet air behind water.
// Everything is white with its shape in alpha (the essence aside), so one sprite takes any tint.

using UnityEngine;

namespace ProjectBlock.View
{
    internal static class WindShapes
    {
        private static Sprite arc;
        private static Sprite wisp;
        private static Sprite wispRooted;
        private static Sprite shard;
        private static Sprite scrap;
        private static Sprite scrapRooted;
        private static Sprite droplet;
        private static Sprite edge;
        private static Sprite essence;
        private static Sprite branch;
        private static Sprite smear;
        private static Sprite dot;
        private static Sprite ring;
        private static Sprite band;
        private static Sprite pixel;

        /// <summary>A quarter-turn of thin curve, bulging toward +x, one unit across.</summary>
        public static Sprite Arc
        {
            get
            {
                if (arc == null)
                {
                    arc = Bake("WindArc", 96, 96, 0.5f, 0.5f, delegate (float x, float y)
                    {
                        float r = Mathf.Sqrt(x * x + y * y);
                        float angle = Mathf.Atan2(y, x); // 0 at +x
                        float span = Mathf.Clamp01(1f - Mathf.Abs(angle) / 0.95f);
                        // thick in the middle of the stroke, thinning to nothing at its ends
                        float thick = 0.035f + 0.05f * span;
                        float line = Mathf.Clamp01(1f - Mathf.Abs(r - 0.8f) / thick);
                        return line * Mathf.SmoothStep(0f, 1f, span * 2.2f);
                    });
                }
                return arc;
            }
        }

        /// <summary>One unit long (head at +x), a quarter unit thick, centred.</summary>
        public static Sprite Wisp
        {
            get
            {
                if (wisp == null)
                {
                    wisp = Bake("WindWisp", 128, 32, 0.5f, 0.5f, WispField, 128f);
                }
                return wisp;
            }
        }

        /// <summary>The same stroke, pivoted on its TAIL - it grows out of a point.</summary>
        public static Sprite WispRooted
        {
            get
            {
                if (wispRooted == null)
                {
                    wispRooted = Bake("WindWispRooted", 128, 32, 0f, 0.5f, WispField, 128f);
                }
                return wispRooted;
            }
        }

        private static float WispField(float x, float y)
        {
            // x -1..1 tail to head, y -1..1 across (the texture is 4:1, so y is the short axis)
            float u = x * 0.5f + 0.5f;
            float along = Mathf.SmoothStep(0f, 1f, u / 0.8f)
                * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.86f) / 0.14f));
            float taper = Mathf.Lerp(0.3f, 0.75f, u);
            return along * Mathf.Exp(-(y * y) / (taper * taper) * 2.2f);
        }

        /// <summary>A soft sliver, twice as long as it is wide.</summary>
        public static Sprite Shard
        {
            get
            {
                if (shard == null)
                {
                    shard = Bake("WindShard", 32, 32, 0.5f, 0.5f, delegate (float x, float y)
                    {
                        float d = Mathf.Abs(x) * 0.95f + Mathf.Abs(y) * 2.3f;
                        return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1.4f - d);
                    });
                }
                return shard;
            }
        }

        /// <summary>A torn piece of flame: bulb at -x, bent point at +x. Centred.</summary>
        public static Sprite Scrap
        {
            get
            {
                if (scrap == null)
                {
                    scrap = Bake("WindScrap", 64, 64, 0.5f, 0.5f, ScrapField);
                }
                return scrap;
            }
        }

        /// <summary>The same flame pivoted on its bulb - a tongue standing on a surface.</summary>
        public static Sprite ScrapRooted
        {
            get
            {
                if (scrapRooted == null)
                {
                    scrapRooted = Bake("WindScrapRooted", 64, 64, 0.08f, 0.5f, ScrapField);
                }
                return scrapRooted;
            }
        }

        private static float ScrapField(float x, float y)
        {
            float u = x * 0.5f + 0.5f; // 0 at the bulb, 1 at the point
            float width = 0.46f * Mathf.Pow(Mathf.Clamp01(1f - u), 0.75f) * Mathf.SmoothStep(0f, 1f, u / 0.16f);
            float bend = 0.16f * Mathf.Sin(u * Mathf.PI * 1.25f);
            float d = Mathf.Abs(y - bend);
            return width <= 0.001f ? 0f : 1f - Step(width * 0.55f, width, d);
        }

        /// <summary>A teardrop: round at +x, a tail behind. Centred.</summary>
        public static Sprite Droplet
        {
            get
            {
                if (droplet == null)
                {
                    droplet = Bake("WindDroplet", 48, 48, 0.5f, 0.5f, delegate (float x, float y)
                    {
                        float head = Mathf.Sqrt((x - 0.38f) * (x - 0.38f) + y * y);
                        float inHead = 1f - Step(0.34f, 0.46f, head);
                        float u = Mathf.Clamp01((x + 0.9f) / 1.28f);
                        float w = 0.4f * u * u;
                        float inTail = x < 0.38f ? 1f - Step(w * 0.6f, w + 0.02f, Mathf.Abs(y)) : 0f;
                        return Mathf.Max(inHead, inTail * Mathf.SmoothStep(0f, 1f, u * 3f));
                    });
                }
                return droplet;
            }
        }

        /// <summary>Light along the +x side of a rounded square, dying a third of the way in.</summary>
        public static Sprite Edge
        {
            get
            {
                if (edge == null)
                {
                    edge = Bake("WindEdge", 64, 64, 0.5f, 0.5f, delegate (float x, float y)
                    {
                        // the cube's own rounded square
                        float qx = Mathf.Max(Mathf.Abs(x) - 0.72f, 0f);
                        float qy = Mathf.Max(Mathf.Abs(y) - 0.72f, 0f);
                        float inside = 1f - Step(0.2f, 0.27f, Mathf.Sqrt(qx * qx + qy * qy));
                        float lit = Step(0.25f, 0.98f, x);
                        return inside * lit * lit;
                    });
                }
                return edge;
            }
        }

        /// <summary>The torn piece of infection, in its own colours.</summary>
        public static Sprite Essence
        {
            get
            {
                if (essence == null)
                {
                    essence = BakeColour("WindEssence", 64, delegate (float x, float y)
                    {
                        // three fused lobes: organic, off-round, never an orb
                        float f = Blob(x, y, -0.08f, 0.04f, 0.5f) + Blob(x, y, 0.3f, -0.16f, 0.34f)
                            + Blob(x, y, 0.12f, 0.34f, 0.3f);
                        float body = Step(0.85f, 1.05f, f);
                        float core = Step(1.25f, 1.9f, f);
                        Color lime = WindFx.Style.SporeLime;
                        Color deep = WindFx.Style.SporeDeep;
                        Color c = Color.Lerp(lime, deep, core);
                        c.a = body;
                        return c;
                    });
                }
                return essence;
            }
        }

        private static float Blob(float x, float y, float cx, float cy, float r)
        {
            float d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
            return r * r / Mathf.Max(d2, 0.0001f);
        }

        /// <summary>A thin wavering stain, rooted at its left end, thinning to its tip.</summary>
        public static Sprite Branch
        {
            get
            {
                if (branch == null)
                {
                    branch = Bake("WindBranch", 96, 32, 0f, 0.5f, delegate (float x, float y)
                    {
                        float u = x * 0.5f + 0.5f;
                        float w = 0.55f * (1f - u) + 0.08f;
                        float bend = 0.3f * Mathf.Sin(u * 5.2f) * u;
                        float d = Mathf.Abs(y - bend);
                        return (1f - Step(w * 0.45f, w, d)) * (1f - Step(0.82f, 1f, u));
                    }, 96f);
                }
                return branch;
            }
        }

        /// <summary>A soft long ellipse, three times as long as it is wide.</summary>
        public static Sprite Smear
        {
            get
            {
                if (smear == null)
                {
                    smear = Bake("WindSmear", 96, 32, 0.5f, 0.5f, delegate (float x, float y)
                    {
                        float g = Mathf.Exp(-(x * x) * 2.4f - (y * y) * 2.4f);
                        return g * g;
                    }, 96f);
                }
                return smear;
            }
        }

        public static Sprite Dot
        {
            get
            {
                if (dot == null)
                {
                    dot = Bake("WindDot", 32, 32, 0.5f, 0.5f, delegate (float x, float y)
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
                    });
                }
                return dot;
            }
        }

        public static Sprite Ring
        {
            get
            {
                if (ring == null)
                {
                    ring = Bake("WindRing", 64, 64, 0.5f, 0.5f, delegate (float x, float y)
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        return Mathf.Clamp01(1f - Mathf.Abs(d - 0.84f) / 0.07f);
                    });
                }
                return ring;
            }
        }

        /// <summary>The corridor without its shader: one unit square, soft on all four sides.</summary>
        public static Sprite Band
        {
            get
            {
                if (band == null)
                {
                    band = Bake("WindBand", 64, 64, 0.5f, 0.5f, delegate (float x, float y)
                    {
                        float across = 1f - Mathf.SmoothStep(0f, 1f, (Mathf.Abs(y) - 0.5f) / 0.5f);
                        float along = 1f - Mathf.SmoothStep(0f, 1f, (Mathf.Abs(x) - 0.82f) / 0.18f);
                        return across * along;
                    });
                }
                return band;
            }
        }

        /// <summary>A plain white unit, for the debug lines.</summary>
        public static Sprite Pixel
        {
            get
            {
                if (pixel == null)
                {
                    pixel = Bake("WindPixel", 4, 4, 0.5f, 0.5f, delegate { return 1f; });
                }
                return pixel;
            }
        }

        // ------------------------------------------------------------------ baking

        /// <summary>0 below <paramref name="edge0"/>, 1 above <paramref name="edge1"/>, smooth
        /// between - the shader's smoothstep. NOT Mathf.SmoothStep, whose arguments are (from, to,
        /// t): written the shader's way, every soft edge in here baked as a hard-edged box.</summary>
        private static float Step(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(0.00001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private delegate float Field(float x, float y);

        private delegate Color ColourField(float x, float y);

        /// <summary>Bakes a white sprite whose alpha is <paramref name="field"/> over x, y in
        /// -1..1. One sprite is ONE UNIT along its longer side (ppu defaults to its width).</summary>
        private static Sprite Bake(string name, int w, int h, float pivotX, float pivotY, Field field,
            float ppu = 0f)
        {
            var tex = NewTexture(name, w, h);
            var pixels = new Color32[w * h];
            for (int j = 0; j < h; j++)
            {
                float y = -1f + (j + 0.5f) * 2f / h;
                for (int i = 0; i < w; i++)
                {
                    float x = -1f + (i + 0.5f) * 2f / w;
                    pixels[j * w + i] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(field(x, y)) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            Sprite made = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(pivotX, pivotY),
                ppu > 0f ? ppu : w);
            made.hideFlags = HideFlags.HideAndDontSave;
            return made;
        }

        private static Sprite BakeColour(string name, int size, ColourField field)
        {
            var tex = NewTexture(name, size, size);
            var pixels = new Color32[size * size];
            for (int j = 0; j < size; j++)
            {
                float y = -1f + (j + 0.5f) * 2f / size;
                for (int i = 0; i < size; i++)
                {
                    float x = -1f + (i + 0.5f) * 2f / size;
                    pixels[j * size + i] = field(x, y);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            Sprite made = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            made.hideFlags = HideFlags.HideAndDontSave;
            return made;
        }

        private static Texture2D NewTexture(string name, int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }
}
