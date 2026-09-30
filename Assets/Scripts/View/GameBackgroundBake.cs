// PURPOSE: The baked half of the main-game background ("DARK PLAYROOM / LIVING GAME TABLE") - the
// palette, the LOOK (every number the background is drawn with, as a preset), the noise, and
// every texture GameBackgroundPresentationController draws. Pure maths, run once per screen shape
// (and once per board size for the stage textures), never per frame.
//
// THE BASE FIELD IS BAKED IN PERCEPTUAL (sRGB) SPACE AND DRAWN OPAQUE. This project renders in
// LINEAR colour, where a soft tone laid over the petrol at "0.05" lands as something else entirely
// (BackdropView's note: a turquoise went grey under a white pool, a dark tone previewed right
// reached the screen as black). An opaque texture whose texels ARE the colours the screen shows
// cannot drift. Only what MOVES or answers a mood is a sprite, with its alpha converted.
//
// THE CORRECTIVE PASS (2026-09-30). The first pass was right about the colour and wrong about the
// depth: on screen it read as ONE flat teal surface - the left and right negative spaces the same
// tone, a texture nobody could see, no warmth, no grounding. The colour direction is kept; what is
// added is STRUCTURE, measured against the ground beside the board (a render of the whole screen,
// c_c.png in the session scratchpad): the left space ~0.75 of it and colder, the right ~0.8 and
// navy with a breath of plum, the far corners ~0.65, the card zone ~1.2 and warmer, a soft lift
// behind the board. The edge depth is ORGANIC - deeper on the left, navy to the upper right, the
// bottom corners darker - never a round black mask. Texture is now at a strength that reads: a
// large pigment cloud (~225 px), a mid pigment (~65 px), a static grain - matte, and still never a
// pattern, a stone, a paper or a metal. The first pass survives as a LOOK (FirstPass) for the
// lab's A/B and nothing else.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>
    /// Every number the background is drawn with. Strengths are PERCEPTUAL (what a layer at that
    /// opacity would do in an sRGB compositor); whoever draws a sprite converts them for linear
    /// blending. A preset, so the lab can put the first pass beside the corrective one.
    /// </summary>
    public sealed class GameBackgroundLook
    {
        public string Name;
        /// <summary>The first pass's base field and layer set, for the lab's A/B.</summary>
        public bool FirstPassField;

        // ---- colour masses (baked unless noted)
        public float CenterLift;
        /// <summary>The lift's ellipse, in board half-sizes (1.2-1.5x the board's bounds).</summary>
        public float CenterLiftRx;
        public float CenterLiftRy;
        public float EdgeDepth;
        public float LowerWarm;
        public float CornerPocket;
        /// <summary>Sprites: they drift.</summary>
        public float LeftMass;
        public float RightMass;
        /// <summary>A sprite: it answers PlumStrength.</summary>
        public float Plum;

        // ---- material
        /// <summary>The large pigment cloud - its own drifting layer (~225 px).</summary>
        public float LargeMottle;
        public float LargeMottleScale;
        /// <summary>The mid pigment (~65 px), baked.</summary>
        public float MidMottle;
        public float MidMottleScale;
        public float Flows;
        public float FlowWidth;
        public float Grain;
        public float TopFade;
        public float TopFadeDepth;

        // ---- warmth (sprites)
        public float WarmBoard;
        public float CardWarmth;
        public float DeckWarmth;

        // ---- board staging (sprites)
        public float Stage;
        /// <summary>How much wider than the board the stage is, and its feather (world).</summary>
        public float StagePadding;
        public float StageFeather;
        public float StageDrop;
        public float Aura;
        /// <summary>How far past the stage the aura reaches (world).</summary>
        public float AuraPadding;
        public float Contact;

        // ---- cards (sprites)
        public float CardGrounding;
        /// <summary>How much taller than the card row the grounding band is (world).</summary>
        public float CardGroundingHeight;
        public float DeckContact;

        // ---- vignette
        public float Vignette;
        /// <summary>0 is round; more makes the far left and right deeper than the top and bottom.</summary>
        public float VignetteAsymmetry;

        // ---- motion
        public float DriftDuration;
        /// <summary>Reference pixels (1080 lines).</summary>
        public float DriftDistance;
        public float DriftScale;
        public float DriftOpacity;
        public float MottleDrift;
        public float AuraPeriodMin;
        public float AuraPeriodMax;
        public float AuraSwing;
        public int MoteCount;
        public float MoteLifeMin;
        public float MoteLifeMax;
        /// <summary>How far a mote wanders over its life (reference pixels).</summary>
        public float MoteDistanceMin;
        public float MoteDistanceMax;
        public float MoteOpacityMin;
        public float MoteOpacityMax;

        /// <summary>The corrective pass - what the game draws.</summary>
        public static GameBackgroundLook Corrective()
        {
            return new GameBackgroundLook
            {
                Name = "corrective",
                CenterLift = 0.38f,
                CenterLiftRx = 1.45f,
                CenterLiftRy = 1.30f,
                EdgeDepth = 0.25f,
                LowerWarm = 0.28f,
                CornerPocket = 0.35f,
                LeftMass = 0.40f,
                RightMass = 0.40f,
                Plum = 0.10f,
                LargeMottle = 0.034f,
                LargeMottleScale = 0.48f,
                MidMottle = 0.016f,
                MidMottleScale = 0.12f,
                Flows = 0.022f,
                FlowWidth = 0.30f,
                Grain = 0.010f,
                TopFade = 0.07f,
                TopFadeDepth = 1.0f,
                WarmBoard = 0.028f,
                CardWarmth = 0.022f,
                DeckWarmth = 0.03f,
                Stage = 0.20f,
                StagePadding = 0.75f,
                StageFeather = 1.0f,
                StageDrop = 0.15f,
                Aura = 0.05f,
                AuraPadding = 0.35f,
                Contact = 0.32f,
                CardGrounding = 0.06f,
                CardGroundingHeight = 0.46f,
                DeckContact = 0.22f,
                Vignette = 0.12f,
                VignetteAsymmetry = 0.35f,
                DriftDuration = 18f,
                DriftDistance = 15f,
                DriftScale = 0.025f,
                DriftOpacity = 0.06f,
                MottleDrift = 3f,
                AuraPeriodMin = 7f,
                AuraPeriodMax = 11f,
                AuraSwing = 0.06f,
                MoteCount = 8,
                MoteLifeMin = 5f,
                MoteLifeMax = 12f,
                MoteDistanceMin = 10f,
                MoteDistanceMax = 30f,
                MoteOpacityMin = 0.03f,
                MoteOpacityMax = 0.10f
            };
        }

        /// <summary>The first pass - the flat teal the corrective pass was written against.</summary>
        public static GameBackgroundLook FirstPass()
        {
            GameBackgroundLook l = Corrective();
            l.Name = "first pass";
            l.FirstPassField = true;
            l.LeftMass = 0.24f;   // it had a deep-teal mass right of the board...
            l.RightMass = 0.42f;  // ...and a navy one upper right
            l.Plum = 0f;
            l.LargeMottle = 0f;   // its texture was baked in, and faint
            l.Flows = 0f;
            l.WarmBoard = 0.045f;
            l.CardWarmth = 0f;
            l.DeckWarmth = 0.045f; // one haze, behind the draw pile only
            l.Stage = 0.16f;
            l.StagePadding = 0.45f;
            l.StageFeather = 0.85f;
            l.StageDrop = 0.18f;
            l.Aura = 0.07f;
            l.AuraPadding = 2.0f;
            l.Contact = 0f;
            l.CardGrounding = 0.075f;
            l.CardGroundingHeight = 0.56f;
            l.DeckContact = 0.2f;
            l.VignetteAsymmetry = 0f;
            l.DriftDistance = 16f;
            l.DriftDuration = 16f;
            l.AuraSwing = 0.08f;
            l.MoteCount = 10;
            l.MoteLifeMin = 7f;
            l.MoteLifeMax = 14f;
            l.MoteOpacityMin = 0.05f;
            l.MoteOpacityMax = 0.14f;
            return l;
        }
    }

    public static class GameBackgroundBake
    {
        // =================================================================== the palette (sRGB)
        public static class Palette
        {
            public static Color Petrol = new Color(0.080f, 0.198f, 0.216f);
            public static Color Lift = new Color(0.215f, 0.345f, 0.352f);
            public static Color LeftNavy = new Color(0.030f, 0.085f, 0.115f);
            public static Color RightNavy = new Color(0.034f, 0.066f, 0.118f);
            public static Color Plum = new Color(0.215f, 0.105f, 0.215f);
            public static Color WarmTeal = new Color(0.150f, 0.220f, 0.180f);
            public static Color Pocket = new Color(0.020f, 0.045f, 0.060f);
            public static Color Edge = new Color(0.040f, 0.085f, 0.105f);
            public static Color Amber = new Color(0.56f, 0.39f, 0.18f);
            public static Color StageInk = new Color(0.012f, 0.030f, 0.042f);
            public static Color Aura = new Color(0.20f, 0.33f, 0.34f);
            public static Color VignetteInk = new Color(0.028f, 0.050f, 0.068f);
            public static Color MottleLight = new Color(0.30f, 0.45f, 0.46f);
            public static Color MottleDark = new Color(0.020f, 0.050f, 0.062f);
            public static Color MoteTeal = new Color(0.56f, 0.70f, 0.70f);
            public static Color MoteAqua = new Color(0.62f, 0.68f, 0.70f);
            public static Color MoteGold = new Color(0.78f, 0.64f, 0.38f);
            /// <summary>What the base is drawn in when its own switch is off, so a layer can be
            /// judged alone against something neutral.</summary>
            public static Color Neutral = new Color(0.15f, 0.16f, 0.17f);

            // the first pass's own tones (the A/B)
            public static Color FirstLift = new Color(0.118f, 0.266f, 0.278f);
            public static Color FirstPlum = new Color(0.098f, 0.074f, 0.112f);
            public static Color FirstWarmTeal = new Color(0.112f, 0.214f, 0.198f);
            public static Color FirstFlowLight = new Color(0.100f, 0.228f, 0.240f);
            public static Color FirstFlowDark = new Color(0.058f, 0.130f, 0.156f);
            public static Color FirstNavy = new Color(0.052f, 0.086f, 0.128f);
            public static Color FirstDeepTeal = new Color(0.050f, 0.120f, 0.140f);
            public static Color FirstVignette = new Color(0.020f, 0.036f, 0.052f);
        }

        /// <summary>Which parts go into a bake - the lab's switches.</summary>
        public struct Parts
        {
            public bool Base;
            /// <summary>The soft lift behind the board (A).</summary>
            public bool Lift;
            /// <summary>The organic edge depth and the corner pocket (E).</summary>
            public bool Edge;
            /// <summary>The lower card zone's warmer deep teal (D).</summary>
            public bool Warm;
            /// <summary>The broad S-curve streams.</summary>
            public bool Flows;
            /// <summary>The baked mid pigment.</summary>
            public bool Texture;
        }

        // =================================================================== the base field

        /// <summary>
        /// Bakes the base field and its grey twin (the same pixels at their own linear luminance,
        /// for the saturation seam) over an area of <paramref name="halfW"/> x
        /// <paramref name="halfH"/> world units round the camera. The screen's own half-extents
        /// size the masses; <paramref name="board"/> is the board's centre relative to the
        /// camera and <paramref name="boardHalf"/> its half-size.
        /// </summary>
        public static void BakeBase(Texture2D colour, Texture2D grey, float halfW, float halfH,
            float screenHalfW, float screenHalfH, Vector2 board, float boardHalf, Parts parts,
            GameBackgroundLook look)
        {
            int tw = colour.width;
            int th = colour.height;
            var px = new Color32[tw * th];
            var gx = new Color32[tw * th];
            float aspect = screenHalfW / Mathf.Max(0.001f, screenHalfH);
            for (int j = 0; j < th; j++)
            {
                float y = ((j + 0.5f) / th * 2f - 1f) * halfH;
                for (int i = 0; i < tw; i++)
                {
                    float x = ((i + 0.5f) / tw * 2f - 1f) * halfW;
                    Color c = look.FirstPassField
                        ? FirstPassFieldAt(x, y, screenHalfH, aspect, board / screenHalfH, parts)
                        : FieldAt(x, y, screenHalfH, aspect, board, boardHalf, parts, look);
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

        /// <summary>One texel of the corrective field, in sRGB.</summary>
        private static Color FieldAt(float x, float y, float screenHalfH, float aspect, Vector2 board,
            float boardHalf, Parts parts, GameBackgroundLook look)
        {
            float qx = x / screenHalfH;
            float qy = y / screenHalfH;
            Color c = parts.Base ? Palette.Petrol : Palette.Neutral;
            if (parts.Base && parts.Warm)
            {
                // D: the lower card zone, warmer deep teal
                c = Color.Lerp(c, Palette.WarmTeal, look.LowerWarm
                    * Biweight(Hypot(qx / (aspect * 0.95f), (qy + 0.92f) / 0.55f)));
            }
            if (parts.Base && parts.Edge)
            {
                // E: a darker pocket in the bottom-left corner
                c = Color.Lerp(c, Palette.Pocket, look.CornerPocket
                    * Biweight(Hypot((qx + aspect) / 0.75f, (qy + 1f) / 0.65f)));
                // EDGE DEPTH, organic: deeper on the left, the bottom and top a little, broken up
                // by a slow cloud so it never reads as a mask
                float ex = Mathf.Abs(qx) / aspect * (qx < 0f ? 1.06f : 1f);
                float edge = SmoothStep(0.55f, 1.05f, ex) * 0.8f + SmoothStep(0.75f, 1.05f, Mathf.Abs(qy)) * 0.5f;
                c = Color.Lerp(c, Palette.Edge, look.EdgeDepth * Mathf.Clamp01(edge)
                    * (0.85f + 0.3f * Fbm(qx * 0.7f, qy * 0.7f, 91, 2)));
            }
            if (parts.Base && parts.Lift)
            {
                // A: the soft teal lift behind the board - an ellipse 1.2-1.5x its bounds
                Vector2 bq = board / screenHalfH;
                float bh = boardHalf / screenHalfH;
                c = Color.Lerp(c, Palette.Lift, look.CenterLift * Biweight(Hypot(
                    (qx - bq.x) / (bh * look.CenterLiftRx * 1.55f), (qy - bq.y) / (bh * look.CenterLiftRy * 1.55f))));
            }
            if (parts.Base && parts.Flows)
            {
                // two broad S-curve streams (~160 px wide), one lighter, one deeper
                float f1 = 0.30f * Mathf.Sin(qx * 1.1f + 0.6f) + 0.14f * Mathf.Sin(qx * 0.43f - 1.1f) - 0.45f;
                float d1 = (qy - f1) / look.FlowWidth;
                float f2 = 0.26f * Mathf.Sin(qx * 0.85f - 1.9f) + 0.62f;
                float d2 = (qy - f2) / (look.FlowWidth * 0.8f);
                float flow = look.Flows * (Mathf.Exp(-d1 * d1) - 0.8f * Mathf.Exp(-d2 * d2)) * 0.8f;
                c = new Color(c.r + flow, c.g + flow, c.b + flow, 1f);
            }
            if (parts.Texture)
            {
                // the mid pigment (~65 px) as an absolute pigment offset - the large cloud is its
                // own drifting layer
                float mm = Mathf.Clamp01((Fbm(qx / look.MidMottleScale, qy / look.MidMottleScale, 29, 2) - 0.5f)
                    * 2.4f + 0.5f) - 0.5f;
                float t = look.MidMottle * mm;
                c = new Color(c.r + t * 0.8f, c.g + t, c.b + t, 1f);
            }
            // the score line's fade
            float fade = 1f - look.TopFade * SmoothStep(screenHalfH - look.TopFadeDepth, screenHalfH, y);
            return new Color(c.r * fade, c.g * fade, c.b * fade, 1f);
        }

        /// <summary>The FIRST pass's field, unchanged - kept only so the lab can show what the
        /// corrective pass was written against.</summary>
        private static Color FirstPassFieldAt(float x, float y, float screenHalfH, float aspect, Vector2 bq, Parts parts)
        {
            float qx = x / screenHalfH;
            float qy = y / screenHalfH;
            Color c = parts.Base ? Palette.Petrol : Palette.Neutral;
            if (parts.Base && parts.Lift)
            {
                c = Color.Lerp(c, Palette.FirstLift, 0.8f * Biweight(Hypot(qx - bq.x, (qy - bq.y) * 1.1f) / 1.75f));
                c = Color.Lerp(c, Palette.FirstPlum, 0.45f * Biweight(Hypot(qx + aspect * 1.02f, qy - 1.05f) / 1.25f));
                c = Color.Lerp(c, Palette.FirstWarmTeal, 0.6f * Biweight(Hypot(qx + aspect * 0.78f, qy + 0.72f) / 1.3f));
                float f1 = 0.34f * Mathf.Sin(qx * 1.15f + 0.6f) + 0.18f * Mathf.Sin(qx * 0.47f - 1.1f) - 0.35f;
                float d1 = (qy - f1) / 0.42f;
                c = Color.Lerp(c, Palette.FirstFlowLight, 0.24f * Mathf.Exp(-d1 * d1)
                    * (0.6f + 0.4f * Noise(qx * 0.8f, qy * 0.8f, 71)));
                float f2 = 0.28f * Mathf.Sin(qx * 0.9f - 1.9f) + 0.55f;
                float d2 = (qy - f2) / 0.36f;
                c = Color.Lerp(c, Palette.FirstFlowDark, 0.24f * Mathf.Exp(-d2 * d2)
                    * (0.6f + 0.4f * Noise(qx * 0.8f + 5f, qy * 0.8f, 73)));
            }
            if (parts.Texture)
            {
                float cloud = Fbm(qx / 0.9f, qy / 0.9f, 11, 3) - 0.5f;
                float mottle = Fbm(qx / 0.085f, qy / 0.085f, 29, 2) - 0.5f;
                float k = 1f + 0.06f * 2f * cloud + 0.05f * 2f * mottle;
                c = new Color(c.r * k, c.g * k, c.b * k, 1f);
            }
            float strip = 1f - 0.08f * SmoothStep(screenHalfH - 1.15f, screenHalfH, y);
            return new Color(c.r * strip, c.g * strip, c.b * strip, 1f);
        }

        // =================================================================== sprites' textures

        /// <summary>
        /// The large pigment cloud, as its own layer so it can drift a few pixels: each texel
        /// lighter or deeper than the ground by up to <paramref name="strength"/> (perceptual), the
        /// two signs at DIFFERENT alphas - in linear blending a lighter tone over this dark ground
        /// moves the screen far more per unit of alpha than a darker one does.
        /// </summary>
        public static void BakeMottle(Texture2D tex, float halfW, float halfH, float screenHalfH,
            float scale, float strength)
        {
            int tw = tex.width;
            int th = tex.height;
            var px = new Color32[tw * th];
            Color under = Palette.Petrol;
            for (int j = 0; j < th; j++)
            {
                float y = ((j + 0.5f) / th * 2f - 1f) * halfH / screenHalfH;
                for (int i = 0; i < tw; i++)
                {
                    float x = ((i + 0.5f) / tw * 2f - 1f) * halfW / screenHalfH;
                    float m = Mathf.Clamp01((Fbm(x / scale, y / scale, 11, 2) - 0.5f) * 2.4f + 0.5f) - 0.5f;
                    float o = strength * m;
                    Color c;
                    if (o >= 0f)
                    {
                        Color target = new Color(under.r + o * 0.8f, under.g + o, under.b + o);
                        c = WithAlpha(Palette.MottleLight, AlphaFor(under, target, Palette.MottleLight));
                    }
                    else
                    {
                        Color target = new Color(under.r + o * 0.8f, under.g + o, under.b + o);
                        c = WithAlpha(Palette.MottleDark, AlphaFor(under, target, Palette.MottleDark));
                    }
                    px[j * tw + i] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        /// <summary>The alpha that takes <paramref name="under"/> to <paramref name="target"/> by
        /// laying <paramref name="over"/> on it in linear space - by luminance.</summary>
        private static float AlphaFor(Color under, Color target, Color over)
        {
            float lu = Luminance(under.linear);
            float lt = Luminance(target.linear);
            float lo = Luminance(over.linear);
            float span = lo - lu;
            return Mathf.Abs(span) < 1e-5f ? 0f : Mathf.Clamp01((lt - lu) / span);
        }

        /// <summary>The vignette's alpha over the same area as the base: 0 in the middle, 1 at the
        /// far edges, deeper left and right than top and bottom by <paramref name="asymmetry"/>,
        /// already shaped for linear blending (the renderer's alpha is the peak).</summary>
        public static void BakeVignette(Texture2D tex, float halfW, float halfH, float screenHalfW,
            float screenHalfH, float strength, float asymmetry, bool firstPass)
        {
            int tw = tex.width;
            int th = tex.height;
            var px = new Color32[tw * th];
            float peak = LinearAlpha(strength);
            for (int j = 0; j < th; j++)
            {
                float y = ((j + 0.5f) / th * 2f - 1f) * halfH;
                for (int i = 0; i < tw; i++)
                {
                    float x = ((i + 0.5f) / tw * 2f - 1f) * halfW;
                    float d = firstPass
                        ? Hypot(x / (screenHalfW * 1.02f), (y - 0.45f) / (screenHalfH * 1.1f))
                        : Hypot(x / screenHalfW, (y - 0.45f) / screenHalfH * (1f - asymmetry));
                    float a = LinearAlpha(strength * SmoothStep(firstPass ? 0.45f : 0.5f, firstPass ? 1.3f : 1.25f, d))
                        / Mathf.Max(0.0001f, peak);
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
        public static Texture2D SoftBox(float boxW, float boxH, float solid, float feather, float round, int res)
        {
            float reach = solid + feather;
            float spanW = boxW + 2f * reach;
            float spanH = boxH + 2f * reach;
            int tw = Mathf.Clamp(Mathf.RoundToInt(spanW * res), 16, 256);
            int th = Mathf.Clamp(Mathf.RoundToInt(spanH * res), 16, 256);
            var tex = NewTexture(tw, th, FilterMode.Bilinear, TextureWrapMode.Clamp);
            var px = new Color32[tw * th];
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
            return AlphaFor(under, Color.Lerp(under, over, Mathf.Clamp01(perceptual)), over);
        }

        private static float Luminance(Color lin)
        {
            return 0.2126f * lin.r + 0.7152f * lin.g + 0.0722f * lin.b;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
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
