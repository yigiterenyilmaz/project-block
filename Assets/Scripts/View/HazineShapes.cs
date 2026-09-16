// PURPOSE: The small generated pieces "Hazine"'s finds are dressed in - everything AROUND the two
// drawn bursts, baked once.
//
// THE DRAWINGS ARE THE HERO; THESE ARE SUPPORT, and each one is shaped by what it must not become.
// The LIGHT is irregular and fades to zero at its own edge, because a clean disc behind a burst is
// a halo sticker and a square is a tinted cell (CLAUDE.md: light is a gradient that dies at its own
// edge, never a plate). A MOTE is a point with a soft falloff, never a coin. A GLINT is a thin
// four-point star whose diagonals are much fainter than its cross, or it reads as an asterisk.
// A FRAGMENT is an angular chip with no round edge anywhere - round debris is confetti. A PUFF is a
// lumpy blob built from a few offset lobes, so no two read as the same circle. The SCORCH is soot:
// a dark irregular stain with a few radial streaks and a softer, slightly brighter heart where the
// fire burnt itself out, and it stays inside one cell.
//
// Every texture is square and filled to its own box, so one sprite is one world unit and a scale
// in the view means what it says. Colours are white (tinted by the view) except the scorch and the
// fragments' charred edge, which are baked.

using UnityEngine;

namespace ProjectBlock.View
{
    public static class HazineShapes
    {
        private const int Size = 96;

        public const int LightVariants = 3;
        public const int FragmentVariants = 4;
        public const int PuffVariants = 3;

        private static Sprite[] lights;
        private static Sprite mote;
        private static Sprite glint;
        private static Sprite[] fragments;
        private static Sprite[] puffs;
        private static Sprite scorch;
        private static Sprite essence;
        private static Sprite ring;
        private static Sprite frame;

        public static Sprite Light(int variant)
        {
            if (lights == null)
            {
                lights = new Sprite[LightVariants];
                for (int i = 0; i < LightVariants; i++)
                {
                    float seed = 0.7f + i * 1.91f;
                    lights[i] = Bake((u, v) => White(LightAt(u, v, seed)));
                }
            }
            return lights[Wrap(variant, LightVariants)];
        }

        public static Sprite Mote
        {
            get
            {
                if (mote == null)
                {
                    mote = Bake((u, v) =>
                    {
                        float r = Mathf.Sqrt(u * u + v * v) / 0.5f;
                        float core = Mathf.Clamp01(1f - r / 0.28f);
                        float falloff = Mathf.Clamp01(1f - r);
                        return White(Mathf.Max(core, falloff * falloff * 0.55f));
                    });
                }
                return mote;
            }
        }

        public static Sprite Glint
        {
            get
            {
                if (glint == null)
                {
                    glint = Bake((u, v) =>
                    {
                        float au = Mathf.Abs(u) / 0.5f;
                        float av = Mathf.Abs(v) / 0.5f;
                        // The cross: long, thin, tapering to a point.
                        float cross = Mathf.Max(Ray(au, av), Ray(av, au));
                        // The diagonals: short and much fainter.
                        float du = Mathf.Abs(u + v) * 0.7071f / 0.5f;
                        float dv = Mathf.Abs(u - v) * 0.7071f / 0.5f;
                        float diag = Mathf.Max(Ray(du * 1.8f, dv), Ray(dv * 1.8f, du)) * 0.35f;
                        float r = Mathf.Sqrt(u * u + v * v) / 0.5f;
                        float heart = Mathf.Clamp01(1f - r / 0.16f);
                        return White(Mathf.Max(Mathf.Max(cross, diag), heart));
                    });
                }
                return glint;
            }
        }

        public static Sprite Fragment(int variant)
        {
            if (fragments == null)
            {
                fragments = new Sprite[FragmentVariants];
                for (int i = 0; i < FragmentVariants; i++)
                {
                    int corners = 4 + i % 2;
                    float seed = 1.3f + i * 2.17f;
                    fragments[i] = Bake((u, v) => FragmentAt(u, v, corners, seed));
                }
            }
            return fragments[Wrap(variant, FragmentVariants)];
        }

        public static Sprite Puff(int variant)
        {
            if (puffs == null)
            {
                puffs = new Sprite[PuffVariants];
                for (int i = 0; i < PuffVariants; i++)
                {
                    float seed = 0.4f + i * 2.63f;
                    puffs[i] = Bake((u, v) => White(PuffAt(u, v, seed)));
                }
            }
            return puffs[Wrap(variant, PuffVariants)];
        }

        /// <summary>Soot, baked in its own colours: tint only its alpha.</summary>
        public static Sprite Scorch
        {
            get
            {
                if (scorch == null)
                {
                    scorch = Bake(ScorchAt);
                }
                return scorch;
            }
        }

        /// <summary>A soft pointed drop - the found thing's value on its way somewhere. Points
        /// along +x.</summary>
        public static Sprite Essence
        {
            get
            {
                if (essence == null)
                {
                    essence = Bake((u, v) =>
                    {
                        // A lozenge stretched forward with a round back: never a coin.
                        float x = u / 0.5f;
                        float y = v / 0.5f;
                        float half = x > 0f ? 0.42f * (1f - x) : 0.42f * Mathf.Sqrt(Mathf.Max(0f, 1f - x * x * 1.6f));
                        float body = Soft(half - Mathf.Abs(y), 0.10f);
                        float core = Soft(half * 0.45f - Mathf.Abs(y), 0.06f) * Soft(0.55f - Mathf.Abs(x + 0.1f), 0.2f);
                        return White(Mathf.Clamp01(body * 0.75f + core));
                    });
                }
                return essence;
            }
        }

        public static Sprite Ring
        {
            get
            {
                if (ring == null)
                {
                    ring = Bake((u, v) =>
                    {
                        float r = Mathf.Sqrt(u * u + v * v) / 0.5f;
                        float band = 1f - Mathf.Abs(r - 0.8f) / 0.12f;
                        return White(Mathf.Clamp01(band) * Mathf.Clamp01(band));
                    });
                }
                return ring;
            }
        }

        /// <summary>A thin square outline, for the lab's debug marks only.</summary>
        public static Sprite Frame
        {
            get
            {
                if (frame == null)
                {
                    frame = Bake((u, v) =>
                    {
                        float d = 0.5f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                        return White(d < 0.035f ? 1f : 0f);
                    });
                }
                return frame;
            }
        }

        // ---- the fields ------------------------------------------------------------------------

        private delegate Color Field(float u, float v);

        private static float LightAt(float u, float v, float seed)
        {
            float r = Mathf.Sqrt(u * u + v * v) / 0.5f;
            float a = Mathf.Atan2(v, u);
            // Three awkward frequencies: two at a simple ratio repeat into a flower.
            float edge = 0.86f + 0.07f * Mathf.Sin(a * 3f + seed) + 0.045f * Mathf.Sin(a * 5f - seed * 1.7f)
                + 0.03f * Mathf.Sin(a * 8f + seed * 2.9f);
            float t = Mathf.Clamp01(1f - r / edge);
            // Smooth all the way to zero at the rim, brightest in the middle.
            return t * t * (3f - 2f * t) * t;
        }

        private static float Ray(float along, float across)
        {
            if (along >= 1f)
            {
                return 0f;
            }
            float width = 0.07f * (1f - along);
            return Soft(width - across, 0.03f) * (1f - along * along);
        }

        private static Color FragmentAt(float u, float v, int corners, float seed)
        {
            // A convex chip: the min over a few half-planes at uneven angles and distances.
            float inside = 1f;
            float edgeDist = 1f;
            for (int i = 0; i < corners; i++)
            {
                float ang = (i + 0.35f * Mathf.Sin(seed * (i + 1) * 1.7f)) / corners * Mathf.PI * 2f + seed;
                float reach = 0.30f + 0.15f * Mathf.Abs(Mathf.Sin(seed * 3.1f + i * 2.3f));
                float d = reach - (u * Mathf.Cos(ang) + v * Mathf.Sin(ang));
                edgeDist = Mathf.Min(edgeDist, d);
            }
            inside = Soft(edgeDist, 0.012f);
            if (inside <= 0f)
            {
                return new Color(0f, 0f, 0f, 0f);
            }
            // Charred body, a hot edge on the lower rim that the view can fade out.
            float ember = Mathf.Clamp01(1f - edgeDist / 0.05f) * Mathf.Clamp01(0.5f - v * 1.5f);
            Color body = new Color(0.16f, 0.11f, 0.08f);
            Color hot = new Color(0.95f, 0.45f, 0.14f);
            Color c = Color.Lerp(body, hot, ember * 0.8f);
            c.a = inside;
            return c;
        }

        private static float PuffAt(float u, float v, float seed)
        {
            float best = 0f;
            for (int i = 0; i < 5; i++)
            {
                float ang = seed * 2.1f + i * 1.37f;
                float off = i == 0 ? 0f : 0.14f + 0.05f * Mathf.Sin(seed + i);
                float cx = Mathf.Cos(ang) * off;
                float cy = Mathf.Sin(ang) * off;
                float rad = i == 0 ? 0.28f : 0.18f + 0.04f * Mathf.Cos(seed * 1.3f + i);
                float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
                float t = Mathf.Clamp01(1f - d / rad);
                best = Mathf.Max(best, t * t * (3f - 2f * t));
            }
            return best;
        }

        private static Color ScorchAt(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v) / 0.5f;
            float a = Mathf.Atan2(v, u);
            float edge = 0.78f + 0.08f * Mathf.Sin(a * 4f + 0.6f) + 0.05f * Mathf.Sin(a * 7f - 1.2f)
                + 0.03f * Mathf.Sin(a * 11f + 2.2f);
            float body = Mathf.Clamp01(1f - r / edge);
            body = body * body * (3f - 2f * body);
            // Radial streaks: soot thrown out along a few directions.
            float streak = Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 6.5f + 0.4f)), 12f)
                * Mathf.Clamp01(1f - Mathf.Abs(r - 0.7f) / 0.25f) * 0.6f;
            float alpha = Mathf.Clamp01(body * 0.85f + streak * 0.5f);
            // A slightly warmer, lighter heart where the fire burnt itself out.
            float heart = Mathf.Clamp01(1f - r / 0.28f);
            Color soot = new Color(0.07f, 0.055f, 0.05f);
            Color burnt = new Color(0.28f, 0.17f, 0.10f);
            Color c = Color.Lerp(soot, burnt, heart * 0.7f);
            c.a = alpha * (1f - heart * 0.35f);
            return c;
        }

        private static Color White(float a)
        {
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }

        private static float Soft(float d, float width)
        {
            return Mathf.Clamp01(d / Mathf.Max(width, 0.0001f));
        }

        private static int Wrap(int i, int n)
        {
            return ((i % n) + n) % n;
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
