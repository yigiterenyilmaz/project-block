// PURPOSE: The few small silhouettes "Harcama bonusu" is made of, baked once.
//
// FIVE SHAPES AND THREE OF THEM ARE SPECKS, but the first two carry the whole identity and both
// are defined as much by what they must NOT look like:
//
//   THE STRIP is a voucher ribbon. It must not be a CARD - this effect happens on top of the
//   card layer, beside a real draw pile, and a portrait rounded rectangle there reads as another
//   card rather than as a payment. So it is three times wider than it is tall, its short sides
//   taper slightly inward, and it carries a small NOTCH RHYTHM down them. It must not be a
//   RECEIPT either: no paper texture, no tear teeth, no barcode, no printed lines. The notches
//   are a hint at perforation, four of them, shallow.
//
//   THE CORE is a rebate token. It must not be a COIN - a gold disc is a currency icon and this
//   game has no currency icon - and it must not be a soft round dot, which is the exact mistake
//   Midas made once and which reads as an unexplained yellow blur on the board. So it is a flat
//   LOZENGE with notched ends: the strip, folded down to a seed, still recognisably the same
//   object.
//
// EVERY TEXTURE IS SQUARE AND EACH SHAPE'S OWN PROPORTION IS BAKED INTO IT, so one sprite is
// one world unit in BOTH axes and a scale in the view is a size in world units. Non-square
// textures would make localScale.x and localScale.y mean different things - a ribbon written as
// (width, height) would silently come out at the wrong aspect and nobody reading the view would
// see why. The cost is some wasted texture around a wide shape, which at 96 texels is nothing.
// Drawn with soft edges in the ALPHA rather than with a blur.

using UnityEngine;

namespace ProjectBlock.View
{
    public static class RebateShapes
    {
        private const int Size = 96;

        /// <summary>How many times wider than tall the receipt is. Three: clearly a voucher
        /// ribbon, and nothing like the portrait silhouette of a card.</summary>
        public const float StripAspect = 3f;

        private static Sprite strip;
        private static Sprite core;
        private static Sprite bar;
        private static Sprite ring;
        private static Sprite trail;
        private static Sprite[] flecks;

        /// <summary>The receipt: a wide voucher ribbon with a shallow notch rhythm.</summary>
        public static Sprite Strip
        {
            get
            {
                if (strip == null)
                {
                    strip = Bake(StripAt);
                }
                return strip;
            }
        }

        /// <summary>The folded receipt: a notched lozenge, never a coin.</summary>
        public static Sprite Core
        {
            get
            {
                if (core == null)
                {
                    core = Bake(CoreAt);
                }
                return core;
            }
        }

        /// <summary>The waking gold line, and anything else that wants a soft-ended bar.
        /// </summary>
        public static Sprite Bar
        {
            get
            {
                if (bar == null)
                {
                    bar = Bake(BarAt);
                }
                return bar;
            }
        }

        /// <summary>The flight's short tapered ribbon: fat at the head, nothing at the tail, so
        /// it reads as something having passed rather than as a beam.</summary>
        public static Sprite Trail
        {
            get
            {
                if (trail == null)
                {
                    trail = Bake(TrailAt);
                }
                return trail;
            }
        }

        /// <summary>A thin open rectangle - the empty slot's void rim, and the dev anchors.
        /// </summary>
        public static Sprite Ring
        {
            get
            {
                if (ring == null)
                {
                    ring = Bake(RingAt);
                }
                return ring;
            }
        }

        /// <summary>One of three chips: a small rectangle, a diamond, a triangle. Three rather
        /// than one because a burst of identical specks reads as a particle system, and three
        /// rather than eight because this is a COMMON joker.</summary>
        public static Sprite Fleck(int index)
        {
            if (flecks == null)
            {
                flecks = new[]
                {
                    Bake((u, v) => Box(u, v, 0.40f, 0.24f)),
                    Bake((u, v) => Diamond(u, v, 0.42f)),
                    Bake(Chip)
                };
            }
            return flecks[((index % flecks.Length) + flecks.Length) % flecks.Length];
        }

        // ---- the shapes themselves -------------------------------------------------------------

        /// <summary>u and v run -0.5..0.5 from the middle. Returns alpha.</summary>
        private delegate float Field(float u, float v);

        private static float StripAt(float u, float v)
        {
            // Full width, a third of the height: the ribbon proportion, baked.
            float half = 0.5f / StripAspect;
            // t runs -0.5..0.5 ACROSS the ribbon's own height, so the end profile is written in
            // its own coordinates. Writing it in v - which only spans a sixth of the texture -
            // is what grew EARS on the first bake: the cosine covered half a period and pushed
            // the half-width past 0.5, straight out of the box.
            float t = Mathf.Clamp(v / (2f * half), -0.5f, 0.5f);
            // A TICKET END: concave through the middle of its height, never wider than the box.
            float halfW = 0.5f - 0.055f * Mathf.Cos(t * Mathf.PI);
            // Three shallow dips down each end - a hint of perforation, not teeth.
            halfW -= 0.016f * (0.5f + 0.5f * Mathf.Cos(t * Mathf.PI * 6f));
            float a = Soft(halfW - Mathf.Abs(u), 0.014f);
            return Mathf.Min(a, Soft(half - Mathf.Abs(v), 0.016f));
        }

        private static float CoreAt(float u, float v)
        {
            // THE STRIP FOLDED TO A SEED - the SAME ticket silhouette, chunkier. The first bake
            // made it a bowed ellipse, which is a COIN; a soft round blob would be worse still,
            // being the unexplained yellow dot Midas already drew once.
            const float halfH = 0.19f;
            float t = Mathf.Clamp(v / (2f * halfH), -0.5f, 0.5f);
            // Concave ends, exactly like the strip's, so the fold reads as the same object.
            float halfW = 0.34f - 0.075f * Mathf.Cos(t * Mathf.PI);
            float a = Soft(halfW - Mathf.Abs(u), 0.030f);
            // Long sides very slightly bowed OUT, so it has weight without becoming a disc.
            float bow = halfH * (1f + 0.10f * Mathf.Cos(Mathf.Clamp(u / 0.34f, -1f, 1f)
                * Mathf.PI));
            return Mathf.Min(a, Soft(bow - Mathf.Abs(v), 0.030f));
        }

        private static float BarAt(float u, float v)
        {
            // Soft at both ends and across, so a scaled bar never shows a hard cap.
            return Soft(0.5f - Mathf.Abs(u), 0.14f) * Soft(0.22f - Mathf.Abs(v), 0.16f);
        }

        private static float TrailAt(float u, float v)
        {
            // Tapered along its length: full at +u (the head), nothing at -u.
            float along = Mathf.Clamp01(u + 0.5f);
            float across = Soft(0.22f * along - Mathf.Abs(v), 0.10f);
            return across * along;
        }

        private static float RingAt(float u, float v)
        {
            float outer = Mathf.Min(0.5f - Mathf.Abs(u), 0.5f - Mathf.Abs(v));
            return Mathf.Clamp01(Soft(outer, 0.02f) - Soft(outer - 0.035f, 0.02f));
        }

        private static float Box(float u, float v, float halfW, float halfH)
        {
            return Mathf.Min(Soft(halfW - Mathf.Abs(u), 0.06f),
                Soft(halfH - Mathf.Abs(v), 0.06f));
        }

        private static float Diamond(float u, float v, float half)
        {
            return Soft(half - (Mathf.Abs(u) + Mathf.Abs(v)), 0.07f);
        }

        private static float Chip(float u, float v)
        {
            // A small triangle: two half-planes and a base.
            float a = Soft(0.30f - (v * 0.86f + Mathf.Abs(u) * 0.9f), 0.07f);
            return Mathf.Min(a, Soft(v + 0.34f, 0.06f));
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
                    px[y * Size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(field(u, v)));
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            // Pixels per unit = the texture's size, so the sprite is ONE unit SQUARE and a scale
            // in the view is a size in world units rather than a number nobody can read.
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
