// PURPOSE: The baked silhouettes of "Kredi kartı"'s BLACK LEDGER - the debt contract panel, its
// brass trim, the card-chip motif, the legal stamps (HACİZ, SON VADE, KAPANDI, ASGARİ), the seal
// dot, the angled band end, the foreclosure ribbon, the appraisal corners, the paper fleck and the
// screen vignette. Generated once, white, and tinted by whoever draws them, so the palette lives
// in the views' Style and a shape can never smuggle a colour in.
//
// The panel is a rounded rectangle whose corners are CLIPPED a hair before they are rounded - a
// document edge rather than a UI card. A stamp's rim is ROUGHENED by a few low-frequency
// cosines (never noise): stylized ink on stylized paper, not a photograph of either. Every
// texture is made at the aspect it is drawn at, so a scale written as (width, height) is the
// shape's own proportion.

using UnityEngine;

namespace ProjectBlock.View
{
    public static class DebtLedgerShapes
    {
        private static Sprite panel;
        private static Sprite trim;
        private static Sprite chip;
        private static Sprite stamp;
        private static Sprite stampSmall;
        private static Sprite seal;
        private static Sprite bandEnd;
        private static Sprite ribbon;
        private static Sprite corner;
        private static Sprite fleck;
        private static Sprite vignette;
        private static Sprite soft;
        private static Sprite notch;

        /// <summary>The contract panel's body, 4:1, with a raised central face baked into its
        /// value (a touch brighter through the middle, darker to the rim).</summary>
        public static Sprite Panel
        {
            get
            {
                if (panel == null)
                {
                    panel = Bake(256, 64, (x, y) =>
                    {
                        // d is in quarter-height units (see ClippedBox): one texel is 1/128.
                        float d = ClippedBox(x, y, 4f, 0.2f, 0.09f);
                        float a = Mathf.Clamp01(0.5f - d * 128f);
                        float v = 0.86f + 0.14f * Mathf.Clamp01(1f - Mathf.Abs(y) * 1.2f)
                            * Mathf.Clamp01(1f - Mathf.Abs(x) * 0.9f);
                        return new Color(v, v, v, a);
                    });
                }
                return panel;
            }
        }

        /// <summary>The thin brass line just inside the panel's edge.</summary>
        public static Sprite Trim
        {
            get
            {
                if (trim == null)
                {
                    trim = Bake(256, 64, (x, y) =>
                    {
                        // One line, a little over a texel wide, 0.06 inside the edge.
                        float d = ClippedBox(x, y, 4f, 0.2f, 0.09f);
                        float inner = Mathf.Abs(d + 0.06f);
                        float a = Mathf.Clamp01((0.009f - inner) * 128f + 0.5f);
                        return new Color(1f, 1f, 1f, a);
                    });
                }
                return trim;
            }
        }

        /// <summary>A stylized card chip: a rounded plate with four engraved lines - never a flat
        /// gold rectangle, and nothing that belongs to a real card scheme.</summary>
        public static Sprite Chip
        {
            get
            {
                if (chip == null)
                {
                    chip = Bake(64, 48, (x, y) =>
                    {
                        float d = RoundBox(x, y * 0.75f, 0.92f, 0.68f, 0.22f);
                        float a = Mathf.Clamp01(0.5f - d * 40f);
                        float line = 0f;
                        line = Mathf.Max(line, Groove(y * 0.75f - 0.2f) * Step(Mathf.Abs(x) < 0.8f));
                        line = Mathf.Max(line, Groove(y * 0.75f + 0.2f) * Step(Mathf.Abs(x) < 0.8f));
                        line = Mathf.Max(line, Groove(x + 0.3f) * Step(Mathf.Abs(y) < 0.55f));
                        line = Mathf.Max(line, Groove(x - 0.3f) * Step(Mathf.Abs(y) < 0.55f));
                        float v = 1f - 0.45f * line + 0.1f * y;
                        return new Color(v, v, v, a);
                    });
                }
                return chip;
            }
        }

        /// <summary>A wide legal stamp: a roughened rectangular seal, a heavy rim and a lighter
        /// ground inside it (the lettering goes on top). 3:1.</summary>
        public static Sprite Stamp
        {
            get
            {
                if (stamp == null)
                {
                    stamp = BakeStamp(384, 128, 3f);
                }
                return stamp;
            }
        }

        /// <summary>The same stamp at 2:1, for the small ones (ASGARİ, KAPANDI, SON VADE).</summary>
        public static Sprite StampSmall
        {
            get
            {
                if (stampSmall == null)
                {
                    stampSmall = BakeStamp(192, 96, 2f);
                }
                return stampSmall;
            }
        }

        /// <summary>A pressed seal: a disc with a raised ring and a dimple.</summary>
        public static Sprite Seal
        {
            get
            {
                if (seal == null)
                {
                    seal = Bake(48, 48, (x, y) =>
                    {
                        float r = Mathf.Sqrt(x * x + y * y);
                        float edge = r + 0.04f * Mathf.Cos(Mathf.Atan2(y, x) * 9f);
                        float a = Mathf.Clamp01((0.92f - edge) * 24f);
                        float ring = Mathf.Exp(-Mathf.Pow((r - 0.7f) / 0.08f, 2f));
                        float v = 0.78f + 0.22f * ring - 0.12f * Mathf.Clamp01(1f - r / 0.3f)
                            + 0.08f * y;
                        return new Color(v, v, v, a);
                    });
                }
                return seal;
            }
        }

        /// <summary>The debt band's right end: a clipped, angled edge.</summary>
        public static Sprite BandEnd
        {
            get
            {
                if (bandEnd == null)
                {
                    bandEnd = Bake(32, 16, (x, y) =>
                    {
                        // Left half solid, right edge slanted.
                        float edge = 0.35f - 0.55f * y;
                        float a = Mathf.Clamp01((edge - x) * 16f);
                        return new Color(1f, 1f, 1f, a);
                    });
                }
                return bandEnd;
            }
        }

        /// <summary>The foreclosure ribbon: a band with notched swallowtail ends. 8:1.</summary>
        public static Sprite Ribbon
        {
            get
            {
                if (ribbon == null)
                {
                    ribbon = Bake(256, 32, (x, y) =>
                    {
                        float ax = Mathf.Abs(x);
                        float notchAt = 0.93f + 0.06f * Mathf.Abs(y);
                        float a = Mathf.Clamp01((notchAt - ax) * 80f) * Mathf.Clamp01((0.9f - Mathf.Abs(y)) * 12f);
                        float v = 0.9f + 0.1f * Mathf.Cos(y * 3f);
                        return new Color(v, v, v, a);
                    });
                }
                return ribbon;
            }
        }

        /// <summary>One appraisal corner: an L of brass, drawn top-left; the view turns it.</summary>
        public static Sprite Corner
        {
            get
            {
                if (corner == null)
                {
                    corner = Bake(32, 32, (x, y) =>
                    {
                        float u = x * 0.5f + 0.5f;
                        float v = y * 0.5f + 0.5f;
                        bool horizontal = v > 0.8f && u < 0.9f;
                        bool vertical = u < 0.2f && v > 0.1f;
                        float a = horizontal || vertical ? 1f : 0f;
                        return new Color(1f, 1f, 1f, a);
                    });
                }
                return corner;
            }
        }

        /// <summary>A torn paper fleck - an irregular quad, never a coin.</summary>
        public static Sprite Fleck
        {
            get
            {
                if (fleck == null)
                {
                    fleck = Bake(16, 16, (x, y) =>
                    {
                        float d = Mathf.Max(Mathf.Abs(x * 0.9f + y * 0.3f), Mathf.Abs(y * 1.1f - x * 0.2f));
                        return new Color(1f, 1f, 1f, Mathf.Clamp01((0.8f - d) * 10f));
                    });
                }
                return fleck;
            }
        }

        /// <summary>A small embossed maturity notch - a document tab, not a progress dot.</summary>
        public static Sprite Notch
        {
            get
            {
                if (notch == null)
                {
                    notch = Bake(32, 16, (x, y) =>
                    {
                        float d = ClippedBox(x, y, 2f, 0.3f, 0.2f);
                        float a = Mathf.Clamp01(0.5f - d * 16f);
                        float v = 0.8f + 0.2f * y;
                        return new Color(v, v, v, a);
                    });
                }
                return notch;
            }
        }

        /// <summary>A soft round glow falloff, for the brief highlights and the sheen.</summary>
        public static Sprite Soft
        {
            get
            {
                if (soft == null)
                {
                    soft = Bake(32, 32, (x, y) =>
                    {
                        float k = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y));
                        return new Color(1f, 1f, 1f, k * k);
                    });
                }
                return soft;
            }
        }

        /// <summary>A screen vignette: clear in the middle, dark toward the edges.</summary>
        public static Sprite Vignette
        {
            get
            {
                if (vignette == null)
                {
                    vignette = Bake(128, 128, (x, y) =>
                    {
                        float r = Mathf.Sqrt(x * x * 0.8f + y * y);
                        float k = Mathf.Clamp01((r - 0.45f) / 0.75f);
                        return new Color(1f, 1f, 1f, k * k * (3f - 2f * k));
                    });
                }
                return vignette;
            }
        }

        // ------------------------------------------------------------------ bakers

        private static Sprite BakeStamp(int w, int h, float aspect)
        {
            return Bake(w, h, (x, y) =>
            {
                // A little rough on its rim: three low cosines round the outline, so the edge
                // wanders like pressed ink without ever looking like noise.
                float wobble = 0.018f * Mathf.Cos(x * 9f + y * 3f) + 0.012f * Mathf.Cos(x * 23f - y * 7f)
                    + 0.01f * Mathf.Cos(y * 17f + x * 5f);
                float d = RoundBox(x, y, 0.94f, 0.86f, 0.1f) + wobble;
                float a = Mathf.Clamp01(0.5f - d * 90f);
                float rim = Mathf.Clamp01((d + 0.14f) * 30f);
                float inner = RoundBox(x, y, 0.8f, 0.62f, 0.06f) + wobble * 0.6f;
                float innerRim = Mathf.Exp(-Mathf.Pow(inner / 0.02f, 2f));
                // The rim and an inner line are solid ink; the ground inside is a lighter wash, so
                // the lettering laid over it reads.
                float v = Mathf.Max(rim, innerRim);
                float alpha = a * Mathf.Lerp(0.62f, 1f, v);
                return new Color(1f, 1f, 1f, alpha);
            });
        }

        /// <summary>Signed distance (in the shape's own units, x in -1..1) to a box of the given
        /// aspect whose corners are clipped by <paramref name="clip"/> and then rounded.</summary>
        private static float ClippedBox(float x, float y, float aspect, float clip, float round)
        {
            // Work in height units: x spans -aspect..aspect.
            float px = Mathf.Abs(x) * aspect;
            float py = Mathf.Abs(y);
            float hx = aspect - round;
            float hy = 1f - round;
            float qx = px - hx;
            float qy = py - hy;
            float box = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                + Mathf.Min(Mathf.Max(qx, qy), 0f) - round;
            // The clip: a 45 degree cut across each corner.
            float cut = (px + py - (aspect + 1f - clip)) * 0.7071f;
            return Mathf.Max(box, cut) / aspect;
        }

        private static float RoundBox(float x, float y, float hx, float hy, float round)
        {
            float qx = Mathf.Abs(x) - hx + round;
            float qy = Mathf.Abs(y) - hy + round;
            return Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                + Mathf.Min(Mathf.Max(qx, qy), 0f) - round;
        }

        private static float Groove(float d)
        {
            return Mathf.Exp(-(d * d) / (0.05f * 0.05f));
        }

        private static float Step(bool on)
        {
            return on ? 1f : 0f;
        }

        private static Sprite Bake(int w, int h, System.Func<float, float, Color> shade)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float fx = (x + 0.5f) / w * 2f - 1f;
                    float fy = (y + 0.5f) / h * 2f - 1f;
                    px[y * w + x] = shade(fx, fy);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Mathf.Max(w, h));
        }
    }
}
