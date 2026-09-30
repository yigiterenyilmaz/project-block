// PURPOSE: The baked half of the main-game background ("DARK PLAYROOM / LIVING GAME TABLE") - the
// palette, the noise, and every texture GameBackgroundPresentationController draws. Pure maths,
// run once per screen shape (and once per board size for the stage textures), never per frame.
//
// THE BASE FIELD IS BAKED IN PERCEPTUAL (sRGB) SPACE AND DRAWN OPAQUE. That is the whole reason it
// is a bake and not a stack of translucent layers: this project renders in LINEAR colour, where a
// soft colour laid over the petrol at "0.05" lands as something else entirely (BackdropView's note -
// a turquoise went grey under a white pool, and a dark tone previewed right reached the screen as
// black). An opaque texture whose texels ARE the colours the screen should show cannot drift. What
// goes in it, in order: the deep petrol ground; the static colour masses (a teal lift behind the
// board, a warmer teal low on the left, a plum undertone in the far corner - radii in HALF-HEIGHT
// units on both axes, so they stay round at any aspect); two broad S-curve streams of lighter and
// deeper tone (never a pattern - they are only there so the field is not one gradient); texture A
// (a low-frequency cloud, a quarter of the screen across) and texture B (a mottle about 50 px
// across) as brightness modulation; and the darkened strip the score line sits on.
//
// Everything that MOVES or is SCALED by a mood is not in it: the two drifting masses, the warm
// pockets, the aura, the stage, the card grounding, the vignette, the grain and the motes are
// sprites with textures from here, blended in linear space with alphas chosen for that.
//
// NOTHING IS A PHOTOGRAPH: no paper, no concrete, no scratches, no stars. Matte pigment.
// Tuned against a render of the whole screen (bg_new.png in the session scratchpad), board,
// blocks, cream cards and gold score line in place, before a line of this was written.

using UnityEngine;

namespace ProjectBlock.View
{
    public static class GameBackgroundBake
    {
        // =================================================================== the palette (sRGB)
        public static class Palette
        {
            public static Color Petrol = new Color(0.080f, 0.198f, 0.216f);
            public static Color Lift = new Color(0.118f, 0.266f, 0.278f);
            public static Color Navy = new Color(0.052f, 0.086f, 0.128f);
            public static Color Plum = new Color(0.098f, 0.074f, 0.112f);
            public static Color WarmTeal = new Color(0.112f, 0.214f, 0.198f);
            public static Color FlowLight = new Color(0.100f, 0.228f, 0.240f);
            public static Color FlowDark = new Color(0.058f, 0.130f, 0.156f);
            public static Color DeepTeal = new Color(0.050f, 0.120f, 0.140f);
            public static Color VignetteInk = new Color(0.020f, 0.036f, 0.052f);
            public static Color Amber = new Color(0.50f, 0.36f, 0.17f);
            public static Color StageInk = new Color(0.012f, 0.032f, 0.040f);
            public static Color Aura = new Color(0.150f, 0.285f, 0.296f);
            public static Color MoteTeal = new Color(0.56f, 0.70f, 0.70f);
            public static Color MoteGold = new Color(0.78f, 0.64f, 0.38f);
            /// <summary>What the base is drawn in when its own switch is off, so a layer can be
            /// judged alone against something neutral.</summary>
            public static Color Neutral = new Color(0.15f, 0.16f, 0.17f);
        }

        // =================================================================== the bake's numbers
        public static class Style
        {
            public static float LiftStrength = 0.8f;
            /// <summary>Radius of the lift behind the board, in half-heights.</summary>
            public static float LiftRadius = 1.75f;
            public static float PlumStrength = 0.45f;
            public static float PlumRadius = 1.25f;
            public static float WarmTealStrength = 0.6f;
            public static float WarmTealRadius = 1.3f;
            public static float FlowStrength = 0.24f;
            /// <summary>Texture A: size of a cloud in half-heights, and how far it moves the value.</summary>
            public static float CloudScale = 0.9f;
            public static float CloudStrength = 0.06f;
            /// <summary>Texture B: about 46 px at 1080 lines.</summary>
            public static float MottleScale = 0.085f;
            public static float MottleStrength = 0.05f;
            /// <summary>The strip the score line sits on: how much darker, and how far down.</summary>
            public static float TopStrip = 0.08f;
            public static float TopStripDepth = 1.15f;
            /// <summary>Where the vignette is centred (world y, the camera at 0) - between the
            /// board and the screen's middle - and how dark it gets at the corners (PERCEPTUAL).</summary>
            public static float VignetteCentreY = 0.45f;
            public static float Vignette = 0.12f;
            /// <summary>Texture C: the static grain, PERCEPTUAL amplitude (0.010 is ~2.5 RGB).</summary>
            public static float Grain = 0.010f;
        }

        /// <summary>Which parts go into a bake - the lab's switches.</summary>
        public struct Parts
        {
            public bool Base;
            public bool Masses;
            public bool Texture;
        }

        // =================================================================== the base field

        /// <summary>
        /// Bakes the base field and its grey twin (the same pixels at their own linear luminance,
        /// for the saturation seam) over an area of <paramref name="halfW"/> x
        /// <paramref name="halfH"/> world units round the camera. <paramref name="screenHalfH"/>
        /// is the camera's own half-height (the masses are sized by it), and
        /// <paramref name="board"/> is the board's centre relative to the camera.
        /// </summary>
        public static void BakeBase(Texture2D colour, Texture2D grey, float halfW, float halfH,
            float screenHalfW, float screenHalfH, Vector2 board, Parts parts)
        {
            int tw = colour.width;
            int th = colour.height;
            var px = new Color32[tw * th];
            var gx = new Color32[tw * th];
            float aspect = screenHalfW / Mathf.Max(0.001f, screenHalfH);
            Vector2 bq = board / screenHalfH;
            for (int j = 0; j < th; j++)
            {
                float y = ((j + 0.5f) / th * 2f - 1f) * halfH;
                for (int i = 0; i < tw; i++)
                {
                    float x = ((i + 0.5f) / tw * 2f - 1f) * halfW;
                    Color c = FieldAt(x, y, screenHalfH, aspect, bq, parts);
                    px[j * tw + i] = c;
                    Color lin = c.linear;
                    float l = Mathf.LinearToGammaSpace(0.2126f * lin.r + 0.7152f * lin.g + 0.0722f * lin.b);
                    gx[j * tw + i] = new Color(l, l, l, 1f);
                }
            }
            colour.SetPixels32(px);
            colour.Apply(false);
            grey.SetPixels32(gx);
            grey.Apply(false);
        }

        /// <summary>One texel of the base field, in sRGB.</summary>
        private static Color FieldAt(float x, float y, float screenHalfH, float aspect, Vector2 bq, Parts parts)
        {
            if (!parts.Base)
            {
                Color flat = Palette.Neutral;
                if (parts.Texture)
                {
                    flat = Texture(flat, x / screenHalfH, y / screenHalfH);
                }
                return flat;
            }
            float qx = x / screenHalfH;
            float qy = y / screenHalfH;
            Color c = Palette.Petrol;
            if (parts.Masses)
            {
                // A: the lift behind the board - a touch rounder than tall
                c = Color.Lerp(c, Palette.Lift, Style.LiftStrength
                    * Biweight(Hypot(qx - bq.x, (qy - bq.y) * 1.1f) / Style.LiftRadius));
                // D: the plum undertone, far top-left, never seen as a colour
                c = Color.Lerp(c, Palette.Plum, Style.PlumStrength
                    * Biweight(Hypot(qx + aspect * 1.02f, qy - 1.05f) / Style.PlumRadius));
                // B: warmer teal low on the left, toward the cards
                c = Color.Lerp(c, Palette.WarmTeal, Style.WarmTealStrength
                    * Biweight(Hypot(qx + aspect * 0.78f, qy + 0.72f) / Style.WarmTealRadius));
                // two broad streams, one lighter, one deeper - S-curves, never a drawn line
                float f1 = 0.34f * Mathf.Sin(qx * 1.15f + 0.6f) + 0.18f * Mathf.Sin(qx * 0.47f - 1.1f) - 0.35f;
                float d1 = (qy - f1) / 0.42f;
                c = Color.Lerp(c, Palette.FlowLight, Style.FlowStrength * Mathf.Exp(-d1 * d1)
                    * (0.6f + 0.4f * Noise(qx * 0.8f, qy * 0.8f, 71)));
                float f2 = 0.28f * Mathf.Sin(qx * 0.9f - 1.9f) + 0.55f;
                float d2 = (qy - f2) / 0.36f;
                c = Color.Lerp(c, Palette.FlowDark, Style.FlowStrength * Mathf.Exp(-d2 * d2)
                    * (0.6f + 0.4f * Noise(qx * 0.8f + 5f, qy * 0.8f, 73)));
            }
            if (parts.Texture)
            {
                c = Texture(c, qx, qy);
            }
            // the strip the score line sits on
            float strip = 1f - Style.TopStrip * SmoothStep(screenHalfH - Style.TopStripDepth, screenHalfH, y);
            return new Color(c.r * strip, c.g * strip, c.b * strip, 1f);
        }

        /// <summary>Texture A (cloud) and B (mottle), as brightness - pigment, not a pattern.</summary>
        private static Color Texture(Color c, float qx, float qy)
        {
            float cloud = Fbm(qx / Style.CloudScale, qy / Style.CloudScale, 11, 3) - 0.5f;
            float mottle = Fbm(qx / Style.MottleScale, qy / Style.MottleScale, 29, 2) - 0.5f;
            float k = 1f + Style.CloudStrength * 2f * cloud + Style.MottleStrength * 2f * mottle;
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        // =================================================================== sprites' textures

        /// <summary>The vignette's alpha over the same area as the base: 0 in the middle, 1 at the
        /// far corners, already shaped for linear blending (the renderer's alpha is the peak).</summary>
        public static void BakeVignette(Texture2D tex, float halfW, float halfH, float screenHalfW, float screenHalfH)
        {
            int tw = tex.width;
            int th = tex.height;
            var px = new Color32[tw * th];
            float peak = LinearAlpha(Style.Vignette);
            for (int j = 0; j < th; j++)
            {
                float y = ((j + 0.5f) / th * 2f - 1f) * halfH;
                for (int i = 0; i < tw; i++)
                {
                    float x = ((i + 0.5f) / tw * 2f - 1f) * halfW;
                    float d = Hypot(x / (screenHalfW * 1.02f), (y - Style.VignetteCentreY) / (screenHalfH * 1.1f));
                    float a = LinearAlpha(Style.Vignette * SmoothStep(0.45f, 1.3f, d)) / peak;
                    px[j * tw + i] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        /// <summary>A soft round blob: solid in the middle, (1 - d^2)^2 to nothing - no edge to
        /// find anywhere. White; the renderer tints it.</summary>
        public static Texture2D Blob(int size)
        {
            var tex = NewTexture(size, size, FilterMode.Bilinear, TextureWrapMode.Clamp);
            var px = new Color32[size * size];
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    float x = (i + 0.5f) / size * 2f - 1f;
                    float y = (j + 0.5f) / size * 2f - 1f;
                    px[j * size + i] = new Color(1f, 1f, 1f, Biweight(Mathf.Sqrt(x * x + y * y)));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        /// <summary>A mote: a soft dot, or stretched 2:1 for the elongated variant.</summary>
        public static Texture2D Mote(bool elongated)
        {
            int w = elongated ? 32 : 16;
            const int h = 16;
            var tex = NewTexture(w, h, FilterMode.Bilinear, TextureWrapMode.Clamp);
            var px = new Color32[w * h];
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    float x = (i + 0.5f) / w * 2f - 1f;
                    float y = (j + 0.5f) / h * 2f - 1f;
                    float k = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y));
                    px[j * w + i] = new Color(1f, 1f, 1f, k * k);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        /// <summary>
        /// A soft field round a box of <paramref name="boxW"/> x <paramref name="boxH"/>: solid
        /// out to <paramref name="solid"/> past the box's edge, then falling to nothing over
        /// <paramref name="feather"/> (all world units). The texture covers the box plus both, so
        /// a renderer scaled to (boxW + 2 * (solid + feather), ...) lands it exactly. The
        /// corners are rounded - it never copies the board's own outline.
        /// </summary>
        public static Texture2D SoftBox(float boxW, float boxH, float solid, float feather, int res)
        {
            float reach = solid + feather;
            float spanW = boxW + 2f * reach;
            float spanH = boxH + 2f * reach;
            int tw = Mathf.Clamp(Mathf.RoundToInt(spanW * res), 16, 256);
            int th = Mathf.Clamp(Mathf.RoundToInt(spanH * res), 16, 256);
            var tex = NewTexture(tw, th, FilterMode.Bilinear, TextureWrapMode.Clamp);
            var px = new Color32[tw * th];
            float round = Mathf.Min(boxW, boxH) * 0.12f + reach * 0.5f;
            for (int j = 0; j < th; j++)
            {
                float y = ((j + 0.5f) / th - 0.5f) * spanH;
                for (int i = 0; i < tw; i++)
                {
                    float x = ((i + 0.5f) / tw - 0.5f) * spanW;
                    float d = RoundBox(x, y, boxW * 0.5f + solid, boxH * 0.5f + solid, round);
                    float k = 1f - SmoothStep(0f, feather, d);
                    px[j * tw + i] = new Color(1f, 1f, 1f, k);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        /// <summary>The card row's grounding: dark through the middle of its height and gone at
        /// both edges, soft at both ends - a zone, never a panel.</summary>
        public static Texture2D Grounding()
        {
            const int w = 128;
            const int h = 48;
            var tex = NewTexture(w, h, FilterMode.Bilinear, TextureWrapMode.Clamp);
            var px = new Color32[w * h];
            for (int j = 0; j < h; j++)
            {
                float gy = ((j + 0.5f) / h * 2f - 1f) * 2.1f;
                for (int i = 0; i < w; i++)
                {
                    float gx = Mathf.Abs((i + 0.5f) / w * 2f - 1f);
                    float a = Mathf.Exp(-gy * gy * 1.4f) * (1f - SmoothStep(0.62f, 1f, gx));
                    px[j * w + i] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        /// <summary>
        /// Texture C: the static grain, tiled at one texel per screen pixel. Light and dark specks
        /// carry DIFFERENT alphas, because in linear blending a light speck over this dark ground
        /// moves the screen many times further than a dark one at the same alpha - one alpha for
        /// both would read as a white sprinkle. A quarter of the grain comes in 2 px clumps, so it
        /// is 1-3 px rather than a single-pixel hiss. Never animated: it is pigment, not film.
        /// </summary>
        public static Texture2D Grain(int size, float perceptual)
        {
            var tex = NewTexture(size, size, FilterMode.Point, TextureWrapMode.Repeat);
            var px = new Color32[size * size];
            // at a ground of about 0.2 (sRGB): what each sign needs to move it by `perceptual`
            float v = 0.2f;
            float lin = Mathf.GammaToLinearSpace(v);
            Color light = new Color(0.50f, 0.56f, 0.56f);
            float linLight = light.linear.g;
            float lightAlpha = (Mathf.GammaToLinearSpace(v + perceptual) - lin) / Mathf.Max(0.001f, linLight - lin);
            float darkAlpha = 1f - Mathf.GammaToLinearSpace(Mathf.Max(0f, v - perceptual)) / lin;
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    float n = 0.75f * Hash(i, j, 5) + 0.25f * Hash(i >> 1, j >> 1, 6) - 0.5f;
                    float m = Mathf.Clamp01(Mathf.Abs(n) * 2f);
                    Color c = n >= 0f
                        ? new Color(light.r, light.g, light.b, lightAlpha * m)
                        : new Color(0f, 0f, 0f, darkAlpha * m);
                    px[j * size + i] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        // =================================================================== maths

        /// <summary>The alpha a DARK overlay needs in linear blending to darken the screen as much
        /// as <paramref name="perceptual"/> would in an sRGB compositor: 1 - (1 - a)^2.2.</summary>
        public static float LinearAlpha(float perceptual)
        {
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(perceptual), 2.2f);
        }

        /// <summary>The alpha a LIGHTER overlay of <paramref name="over"/> needs in linear
        /// blending over <paramref name="under"/> to lift the screen's luminance as much as a
        /// perceptual lerp of <paramref name="perceptual"/> would. Luminance only - the hue of a
        /// small overlay follows on its own.</summary>
        public static float LiftAlpha(float perceptual, Color over, Color under)
        {
            float lo = Luminance(over.linear);
            float lu = Luminance(under.linear);
            Color target = Color.Lerp(under, over, Mathf.Clamp01(perceptual));
            float lt = Luminance(target.linear);
            float span = lo - lu;
            return Mathf.Abs(span) < 1e-5f ? 0f : Mathf.Clamp01((lt - lu) / span);
        }

        private static float Luminance(Color lin)
        {
            return 0.2126f * lin.r + 0.7152f * lin.g + 0.0722f * lin.b;
        }

        public static float Hash(int ix, int iy, int seed)
        {
            unchecked
            {
                uint h = (uint)(ix * 374761393 + iy * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h / 4294967296f;
            }
        }

        /// <summary>Value noise, cubic-smoothed, 0..1.</summary>
        public static float Noise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            float ux = fx * fx * (3f - 2f * fx);
            float uy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy, seed);
            float b = Hash(ix + 1, iy, seed);
            float c = Hash(ix, iy + 1, seed);
            float d = Hash(ix + 1, iy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        /// <summary>One-dimensional smooth noise in -1..1 - the drift's curve. Never a sine:
        /// a sine is a metronome, and a metronome is noticed.</summary>
        public static float Noise1(float t, int seed)
        {
            return Noise(t, seed * 0.37f, seed) * 2f - 1f;
        }

        public static float Fbm(float x, float y, int seed, int octaves)
        {
            float sum = 0f;
            float amp = 0.5f;
            float f = 1f;
            float norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Noise(x * f, y * f, seed + i * 17);
                norm += amp;
                amp *= 0.5f;
                f *= 2.03f;
            }
            return sum / norm;
        }

        public static float Biweight(float d)
        {
            float k = Mathf.Clamp01(1f - d * d);
            return k * k;
        }

        public static float SmoothStep(float a, float b, float v)
        {
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static float Hypot(float x, float y)
        {
            return Mathf.Sqrt(x * x + y * y);
        }

        private static float RoundBox(float px, float py, float hx, float hy, float r)
        {
            r = Mathf.Min(r, Mathf.Min(hx, hy));
            float qx = Mathf.Abs(px) - (hx - r);
            float qy = Mathf.Abs(py) - (hy - r);
            return Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        public static Texture2D NewTexture(int w, int h, FilterMode filter, TextureWrapMode wrap)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = filter;
            tex.wrapMode = wrap;
            // Generated, never inspected, never worth serialising into a scene.
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }
    }
}
