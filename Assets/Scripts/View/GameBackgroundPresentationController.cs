// PURPOSE: The main game's BACKGROUND - "DARK PLAYROOM / LIVING GAME TABLE" (2026-09-30). The
// surface the board, the blocks, the cream cards and the gold score line all sit on, built so they
// read as ONE art world instead of a dark board laid on a blue-grey wallpaper. It replaces
// BackdropView as what the player sees; BackdropView stays, untouched, as the LEGACY look the lab
// compares against (and as the fallback with this switched off).
//
// WHAT IT IS: a deep PETROL-TEAL ground (never blue-grey, never neon, never green), a slightly
// lighter teal lift behind the board, a cooler navy toward the top right, a warmer teal low on the
// left, a plum undertone in a far corner that nobody should ever name, and a breath of warm honey
// near the board's lower left and behind the deck - the cream cards and the gold score belong to
// the same palette because of it. The material is MATTE PIGMENT: a large cloud, a mid mottle and a
// static grain, all low. No paper, no concrete, no scratches, no metal, no stars, no white centre.
//
// THE LAYERS (orders all below the legacy backdrop's, and below every boss atmosphere, so a boss
// look still replaces it exactly as it replaced the old one - only the vignette sits where the old
// vignette sat, framing whatever is under it):
//   BaseField        -229  baked, OPAQUE, in perceptual colour (GameBackgroundBake)
//   BaseGrey         -228  its grey twin - the SATURATION multiplier, exact in linear space
//   ColorMass_Navy/Deep -227 two soft masses that DRIFT (upper right navy, deep teal right of the board)
//   WarmAccent_A/B   -226  honey haze by the board's lower left and behind the deck
//   BoardAmbientAura -225  a desaturated teal lift round the board - separation, never a glow
//   BoardStageShadow -224  the environment shadow the board sits in, heavier underneath
//   CardGrounding    -223  a dark zone behind the hand; DeckContact_* under both piles
//   Grain            -222  static, one texel per screen pixel
//   AmbientMotePool  -221  6-12 motes, never over the board, never over the UI
//   Vignette         -205  very dark petrol, centred between the board and the screen's middle
//
// NOTHING HERE READS GAMEPLAY. The controller is told where the board, the hand and the piles are
// (SetStage) and which rectangles to keep motes out of (SetSafeRects); moods come in as
// MULTIPLIERS from whoever owns a state (SetMood - "debt" from the debt pressure today). The global
// multipliers (brightness, saturation, warmth, teal strength, vignette, motion) are the lab's.
//
// MOTION IS THE LAST 5-10%. Two masses drift a few pixels on smooth noise (12-20 s, never a sine),
// the aura breathes 1 -> 1.08 -> 0.98 -> 1 over 6-9 s, and a handful of motes wander. None of it
// should be noticed in the first seconds; with it all stopped the screen must still be the screen.
//
// LOD: High (everything, 10 motes; 6 on a phone), Medium (static layers, half the drift, 5 motes),
// Low (static layers and the stage only). No RenderTexture, no blur, no bloom dependence.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class GameBackgroundPresentationController : MonoBehaviour
    {
        public enum Quality
        {
            High,
            Medium,
            Low
        }

        /// <summary>What is drawn: the new background, the legacy one, or both split down the
        /// middle (legacy left, new right) for the lab's side-by-side.</summary>
        public enum ViewMode
        {
            New,
            Legacy,
            Split
        }

        /// <summary>A set of multipliers, all 1 at rest. Moods from different owners multiply.</summary>
        public struct Mood
        {
            public float Brightness;
            public float Saturation;
            public float Warmth;
            public float TealStrength;
            public float Vignette;
            public float MotionStrength;

            public static Mood Neutral
            {
                get
                {
                    return new Mood
                    {
                        Brightness = 1f,
                        Saturation = 1f,
                        Warmth = 1f,
                        TealStrength = 1f,
                        Vignette = 1f,
                        MotionStrength = 1f
                    };
                }
            }

            public Mood Times(Mood o)
            {
                return new Mood
                {
                    Brightness = Brightness * o.Brightness,
                    Saturation = Saturation * o.Saturation,
                    Warmth = Warmth * o.Warmth,
                    TealStrength = TealStrength * o.TealStrength,
                    Vignette = Vignette * o.Vignette,
                    MotionStrength = MotionStrength * o.MotionStrength
                };
            }
        }

        // =================================================================== tuning
        public static class Style
        {
            // sprite layers: PERCEPTUAL strengths, converted for linear blending where they are used
            public static float NavyMass = 0.42f;
            public static float DeepMass = 0.24f;
            public static float WarmAccent = 0.045f;
            public static float Aura = 0.07f;
            public static float AuraPadding = 2.0f;
            public static float Stage = 0.16f;
            public static float StagePadding = 0.45f;
            public static float StageFeather = 0.85f;
            /// <summary>How far the stage sits BELOW the board's centre - it is heavier underneath.</summary>
            public static float StageDrop = 0.18f;
            public static float CardGrounding = 0.075f;
            public static float DeckContact = 0.2f;

            // motion
            public static float DriftDurationMin = 12f;
            public static float DriftDurationMax = 20f;
            /// <summary>In reference pixels (1080 lines).</summary>
            public static float DriftDistance = 16f;
            public static float DriftScale = 0.03f;
            public static float DriftOpacity = 0.07f;
            public static float AuraPeriodMin = 6f;
            public static float AuraPeriodMax = 9f;

            // motes
            public static int MoteCountDesktop = 10;
            public static int MoteCountMobile = 6;
            public static int MoteCountMedium = 5;
            /// <summary>In reference pixels per second.</summary>
            public static float MoteSpeedMin = 4f;
            public static float MoteSpeedMax = 15f;
            /// <summary>PERCEPTUAL alpha of a mote at its brightest.</summary>
            public static float MoteOpacityMin = 0.05f;
            public static float MoteOpacityMax = 0.14f;
            public static float MoteLifeMin = 7f;
            public static float MoteLifeMax = 14f;
            /// <summary>How much of the board (plus a margin) refuses a mote - most, not all.</summary>
            public static float BoardMoteRejection = 0.85f;
            public static float BoardMoteMargin = 0.6f;
        }

        /// <summary>The lab's switches (144). All on by default.</summary>
        public static class Layers
        {
            public static bool ShowBackgroundBase = true;
            public static bool ShowBackgroundColorMasses = true;
            public static bool ShowBackgroundMottle = true;
            public static bool ShowBackgroundGrain = true;
            public static bool ShowBoardStage = true;
            public static bool ShowBoardAura = true;
            public static bool ShowCardGrounding = true;
            public static bool ShowWarmAccents = true;
            public static bool ShowAmbientMotes = true;
            public static bool ShowVignette = true;

            public static void AllOn()
            {
                ShowBackgroundBase = ShowBackgroundColorMasses = ShowBackgroundMottle = true;
                ShowBackgroundGrain = ShowBoardStage = ShowBoardAura = ShowCardGrounding = true;
                ShowWarmAccents = ShowAmbientMotes = ShowVignette = true;
            }

            /// <summary>Everything off - for the lab's "X only" scenes, which then turn one on.</summary>
            public static void AllOff()
            {
                ShowBackgroundBase = ShowBackgroundColorMasses = ShowBackgroundMottle = false;
                ShowBackgroundGrain = ShowBoardStage = ShowBoardAura = ShowCardGrounding = false;
                ShowWarmAccents = ShowAmbientMotes = ShowVignette = false;
            }
        }

        /// <summary>The lab's global multipliers (144: BackgroundBrightness ... MotionStrength).</summary>
        public static Mood Global = Mood.Neutral;

        public Quality CurrentQuality { get; private set; }

        public ViewMode Mode { get; private set; }

        /// <summary>Motion stopped (the lab's "static" scenes). Everything holds where it is.</summary>
        public bool Frozen;

        /// <summary>The lab's aspect preview: the background laid out for a window of this shape
        /// inside the real one (bars fill the rest). Null is the real window.</summary>
        public float? PreviewAspect { get; private set; }

        /// <summary>The lab's small-window preview: pixel-sized things (the grain, the motes) as a
        /// window this many lines tall would draw them. Null is the real window.</summary>
        public int? PreviewLines { get; private set; }

        // =================================================================== state

        private Camera cam;
        private BackdropView legacy;
        private Transform screenRoot;
        private Transform worldRoot;
        private SpriteRenderer baseField;
        private SpriteRenderer baseGrey;
        private SpriteRenderer navyMass;
        private SpriteRenderer deepMass;
        private SpriteRenderer warmA;
        private SpriteRenderer warmB;
        private SpriteRenderer aura;
        private SpriteRenderer stage;
        private SpriteRenderer grounding;
        private SpriteRenderer deckA;
        private SpriteRenderer deckB;
        private SpriteRenderer grain;
        private SpriteRenderer vignette;
        private SpriteRenderer barLeft;
        private SpriteRenderer barRight;
        private SpriteRenderer splitLine;
        private SpriteMask splitMask;
        private Texture2D baseTex;
        private Texture2D greyTex;
        private Texture2D vignetteTex;
        private Texture2D stageTex;
        private Texture2D auraTex;
        private Sprite blob;
        private Sprite moteDot;
        private Sprite moteLong;
        private Sprite groundingSprite;
        private readonly List<Mote> motes = new List<Mote>();
        private readonly Dictionary<string, Mood> moods = new Dictionary<string, Mood>();
        private readonly List<Rect> safeRects = new List<Rect>();
        private System.Random rng = new System.Random(20260930);

        private Rect boardRect;
        private Rect handRect;
        private Vector2 pileA;
        private Vector2 pileB;
        private bool stageKnown;
        private bool runVisible = true;

        private float builtHalfW = -1f;
        private float builtHalfH = -1f;
        private Vector2 builtBoard = new Vector2(float.NaN, float.NaN);
        private Vector2 builtStageSize = new Vector2(-1f, -1f);
        private int builtParts = -1;
        private int builtLines = -1;

        private float clock;
        private float auraCycleStart;
        private float auraCyclePeriod = 7f;
        private float nextMote;

        private const float Cover = 1.18f;
        private const int BaseOrder = -229;
        private const int GreyOrder = -228;
        private const int MassOrder = -227;
        private const int WarmOrder = -226;
        private const int AuraOrder = -225;
        private const int StageOrder = -224;
        private const int GroundOrder = -223;
        private const int GrainOrder = -222;
        private const int MoteOrder = -221;
        private const int VignetteOrder = -205;
        private const int BarOrder = -200;
        private const int SplitLineOrder = -199;

        private sealed class Mote
        {
            public SpriteRenderer Renderer;
            public bool Alive;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Alpha;
            public Color Colour;
        }

        // =================================================================== building

        public void Build(Camera camera, BackdropView legacyBackdrop)
        {
            cam = camera != null ? camera : Camera.main;
            legacy = legacyBackdrop;
            screenRoot = new GameObject("GameBackgroundRoot").transform;
            screenRoot.SetParent(cam != null ? cam.transform : transform, false);
            screenRoot.localPosition = new Vector3(0f, 0f, 2f);
            worldRoot = new GameObject("GameBackgroundWorld").transform;
            worldRoot.SetParent(transform, false);

            baseField = Layer(screenRoot, "BaseField", BaseOrder);
            baseGrey = Layer(screenRoot, "BaseGrey", GreyOrder);
            blob = MakeSprite(GameBackgroundBake.Blob(128));
            navyMass = Layer(screenRoot, "ColorMass_Navy", MassOrder, blob);
            deepMass = Layer(screenRoot, "ColorMass_Deep", MassOrder, blob);
            warmA = Layer(worldRoot, "WarmAccent_A", WarmOrder, blob);
            warmB = Layer(worldRoot, "WarmAccent_B", WarmOrder, blob);
            aura = Layer(worldRoot, "BoardAmbientAura", AuraOrder);
            stage = Layer(worldRoot, "BoardStageShadow", StageOrder);
            groundingSprite = MakeSprite(GameBackgroundBake.Grounding());
            grounding = Layer(worldRoot, "CardGrounding", GroundOrder, groundingSprite);
            deckA = Layer(worldRoot, "DeckContact_Draw", GroundOrder, blob);
            deckB = Layer(worldRoot, "DeckContact_Discard", GroundOrder, blob);
            vignette = Layer(screenRoot, "Vignette", VignetteOrder);
            barLeft = Layer(screenRoot, "PreviewBarLeft", BarOrder, MakeSprite(Texture2D.whiteTexture));
            barRight = Layer(screenRoot, "PreviewBarRight", BarOrder, barLeft.sprite);
            splitLine = Layer(screenRoot, "SplitLine", SplitLineOrder, barLeft.sprite);

            var grainGo = new GameObject("Grain");
            grainGo.transform.SetParent(screenRoot, false);
            grain = grainGo.AddComponent<SpriteRenderer>();
            grain.sortingOrder = GrainOrder;

            moteDot = MakeSprite(GameBackgroundBake.Mote(false));
            moteLong = MakeSprite(GameBackgroundBake.Mote(true));
            for (int i = 0; i < 12; i++)
            {
                var m = new Mote { Renderer = Layer(worldRoot, "Mote" + i, MoteOrder, moteDot) };
                m.Renderer.enabled = false;
                motes.Add(m);
            }

            var maskGo = new GameObject("SplitMask");
            maskGo.transform.SetParent(screenRoot, false);
            splitMask = maskGo.AddComponent<SpriteMask>();
            splitMask.sprite = barLeft.sprite;
            splitMask.isCustomRangeActive = true;
            splitMask.backSortingOrder = -231;
            splitMask.frontSortingOrder = -200;
            splitMask.enabled = false;

            CurrentQuality = Application.isMobilePlatform ? Quality.Medium : Quality.High;
            if (cam != null)
            {
                cam.backgroundColor = GameBackgroundBake.Palette.Petrol;
            }
            SetMode(ViewMode.New);
        }

        // =================================================================== driving it

        /// <summary>Where the board (its VISIBLE rect), the hand and the two piles are, in the
        /// world. Every frame; nothing here is hard-coded to a centre.</summary>
        public void SetStage(Rect board, Rect hand, Vector2 drawPile, Vector2 discardPile)
        {
            boardRect = board;
            handRect = hand;
            pileA = drawPile;
            pileB = discardPile;
            stageKnown = board.width > 0.01f;
        }

        /// <summary>The rectangles a mote must never be born in (the score line, the ledger, the
        /// bars, the hand, the piles), in the world.</summary>
        public void SetSafeRects(IList<Rect> rects)
        {
            safeRects.Clear();
            if (rects != null)
            {
                safeRects.AddRange(rects);
            }
        }

        /// <summary>The run's own layers - the stage, the grounding, the contacts, the motes - go
        /// with the run (the title screen has no board to seat).</summary>
        public void SetRunVisible(bool visible)
        {
            runVisible = visible;
        }

        /// <summary>A mood from an owner of a state (the debt pressure, a boss...). Multipliers,
        /// all 1 at rest; moods from different owners multiply. Null clears the owner's.</summary>
        public void SetMood(string owner, Mood? mood)
        {
            if (mood.HasValue)
            {
                moods[owner] = mood.Value;
            }
            else
            {
                moods.Remove(owner);
            }
        }

        public void SetQuality(Quality quality)
        {
            CurrentQuality = quality;
        }

        public void SetMode(ViewMode mode)
        {
            Mode = mode;
            bool showNew = mode != ViewMode.Legacy;
            screenRoot.gameObject.SetActive(showNew);
            worldRoot.gameObject.SetActive(showNew);
            if (legacy != null)
            {
                legacy.SetVisible(mode != ViewMode.New);
                legacy.SetMaskInteraction(mode == ViewMode.Split
                    ? SpriteMaskInteraction.VisibleOutsideMask : SpriteMaskInteraction.None);
            }
            SpriteMaskInteraction mine = mode == ViewMode.Split
                ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
            foreach (SpriteRenderer r in AllLayers())
            {
                r.maskInteraction = mine;
            }
            splitMask.enabled = mode == ViewMode.Split;
            if (cam != null)
            {
                cam.backgroundColor = mode == ViewMode.Legacy ? BackdropView.Style.Ground
                    : GameBackgroundBake.Palette.Petrol;
            }
        }

        public void SetPreview(float? aspect, int? lines)
        {
            PreviewAspect = aspect;
            PreviewLines = lines;
        }

        /// <summary>Forces every baked texture to be redone on the next frame (a lab switch that
        /// changes what is in the bake).</summary>
        public void Invalidate()
        {
            builtHalfW = -1f;
            builtStageSize = new Vector2(-1f, -1f);
        }

        /// <summary>The combined multipliers in force: the moods' product and the lab's globals.</summary>
        public Mood Combined
        {
            get
            {
                Mood m = Global;
                foreach (KeyValuePair<string, Mood> kv in moods)
                {
                    m = m.Times(kv.Value);
                }
                return m;
            }
        }

        // =================================================================== the frame

        private void LateUpdate()
        {
            if (cam == null || screenRoot == null || Mode == ViewMode.Legacy)
            {
                return;
            }
            float dt = Frozen ? 0f : Time.deltaTime;
            clock += dt;
            float realHalfH = cam.orthographicSize;
            float realHalfW = realHalfH * cam.aspect;
            float halfH = realHalfH;
            float halfW = PreviewAspect.HasValue ? realHalfH * PreviewAspect.Value : realHalfW;
            int lines = PreviewLines ?? Mathf.Max(1, cam.pixelHeight);
            Vector2 camPos = cam.transform.position;
            Vector2 boardLocal = stageKnown ? boardRect.center - camPos : new Vector2(0f, 0.9f);

            int parts = (Layers.ShowBackgroundBase ? 1 : 0) | (Layers.ShowBackgroundColorMasses ? 2 : 0)
                | (Layers.ShowBackgroundMottle ? 4 : 0);
            if (!Mathf.Approximately(builtHalfW, halfW) || !Mathf.Approximately(builtHalfH, halfH)
                || builtParts != parts || (boardLocal - builtBoard).sqrMagnitude > 0.04f || builtLines != lines)
            {
                Bake(halfW, halfH, boardLocal, parts, lines);
            }
            Mood mood = Combined;
            PaintScreen(halfW, halfH, realHalfW, mood, lines);
            PaintStage(mood, dt);
            TickMotes(halfW, halfH, camPos, mood, lines, dt);
        }

        private void Bake(float halfW, float halfH, Vector2 boardLocal, int parts, int lines)
        {
            float coverW = halfW * Cover;
            float coverH = halfH * Cover;
            // A quarter of the screen's own resolution is plenty for a field whose finest feature
            // (the mottle) is ~46 px across - a dozen texels - and the grain carries the per-pixel
            // detail. It keeps the bake to a few tens of milliseconds, on a resize and never per frame.
            int th = Mathf.Clamp(Mathf.RoundToInt(lines * Cover / 4f), 120, 360);
            int tw = Mathf.Clamp(Mathf.RoundToInt(th * coverW / coverH), 120, 1100);
            if (baseTex == null || baseTex.width != tw || baseTex.height != th)
            {
                DestroyTex(ref baseTex);
                DestroyTex(ref greyTex);
                DestroyTex(ref vignetteTex);
                baseTex = GameBackgroundBake.NewTexture(tw, th, FilterMode.Bilinear, TextureWrapMode.Clamp);
                greyTex = GameBackgroundBake.NewTexture(tw, th, FilterMode.Bilinear, TextureWrapMode.Clamp);
                int vw = Mathf.Max(32, tw / 2);
                int vh = Mathf.Max(32, th / 2);
                vignetteTex = GameBackgroundBake.NewTexture(vw, vh, FilterMode.Bilinear, TextureWrapMode.Clamp);
            }
            var bakeParts = new GameBackgroundBake.Parts
            {
                Base = (parts & 1) != 0,
                Masses = (parts & 2) != 0,
                Texture = (parts & 4) != 0
            };
            GameBackgroundBake.BakeBase(baseTex, greyTex, coverW, coverH, halfW, halfH, boardLocal, bakeParts);
            GameBackgroundBake.BakeVignette(vignetteTex, coverW, coverH, halfW, halfH);
            SetSprite(baseField, baseTex);
            SetSprite(baseGrey, greyTex);
            SetSprite(vignette, vignetteTex);
            FitScreen(baseField, coverW * 2f, coverH * 2f);
            FitScreen(baseGrey, coverW * 2f, coverH * 2f);
            FitScreen(vignette, coverW * 2f, coverH * 2f);

            // the grain: one texel per (preview) screen pixel
            if (grain.sprite == null || builtLines != lines)
            {
                if (grain.sprite != null)
                {
                    Destroy(grain.sprite.texture);
                    Destroy(grain.sprite);
                }
                Texture2D g = GameBackgroundBake.Grain(256, GameBackgroundBake.Style.Grain);
                float ppu = lines / (2f * halfH);
                grain.sprite = Sprite.Create(g, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f), ppu, 0,
                    SpriteMeshType.FullRect);
                grain.drawMode = SpriteDrawMode.Tiled;
                grain.tileMode = SpriteTileMode.Continuous;
            }
            grain.size = new Vector2(coverW * 2f, coverH * 2f);

            builtHalfW = halfW;
            builtHalfH = halfH;
            builtBoard = boardLocal;
            builtParts = parts;
            builtLines = lines;
        }

        /// <summary>The screen-anchored layers: brightness, saturation, the drifting masses, the
        /// grain, the vignette, the preview bars and the split.</summary>
        private void PaintScreen(float halfW, float halfH, float realHalfW, Mood mood, int lines)
        {
            float bright = Mathf.Pow(Mathf.Max(0f, mood.Brightness), 2.2f);
            baseField.color = new Color(bright, bright, bright, 1f);
            baseGrey.color = new Color(bright, bright, bright, Mathf.Clamp01(1f - mood.Saturation));
            baseGrey.enabled = mood.Saturation < 0.999f;

            // THE DRIFT: smooth noise, never a sine - a few pixels, a few percent, a few percent of
            // opacity, on its own slow clock per mass.
            float motion = MotionFactor(mood);
            float px = 2f * halfH / 1080f;
            float aspect = halfW / halfH;
            float masses = Layers.ShowBackgroundColorMasses ? Mathf.Clamp01(mood.TealStrength) : 0f;
            PaintMass(navyMass, new Vector2(aspect * 0.86f, 0.78f) * halfH, 1.35f * halfH * 2f,
                GameBackgroundBake.Palette.Navy, Style.NavyMass * masses, 31, motion, px);
            PaintMass(deepMass, new Vector2((stageKnown ? boardRect.xMax - cam.transform.position.x : 3.3f) + 1.2f,
                    (stageKnown ? boardRect.center.y - cam.transform.position.y : 0.9f) - 0.2f),
                1.1f * halfH * 2f, GameBackgroundBake.Palette.DeepTeal, Style.DeepMass * masses, 47, motion, px);

            grain.enabled = Layers.ShowBackgroundGrain;
            vignette.enabled = Layers.ShowVignette;
            vignette.color = new Color(GameBackgroundBake.Palette.VignetteInk.r, GameBackgroundBake.Palette.VignetteInk.g,
                GameBackgroundBake.Palette.VignetteInk.b,
                Mathf.Clamp01(GameBackgroundBake.LinearAlpha(GameBackgroundBake.Style.Vignette) * mood.Vignette));

            // the lab's aspect preview: bars where the previewed window would end
            bool bars = PreviewAspect.HasValue && halfW < realHalfW - 0.01f;
            barLeft.enabled = bars;
            barRight.enabled = bars;
            if (bars)
            {
                float w = realHalfW - halfW;
                FitScreen(barLeft, w, halfH * 2.4f);
                barLeft.transform.localPosition = new Vector3(-halfW - w * 0.5f, 0f, 0f);
                FitScreen(barRight, w, halfH * 2.4f);
                barRight.transform.localPosition = new Vector3(halfW + w * 0.5f, 0f, 0f);
                barLeft.color = new Color(0.02f, 0.02f, 0.025f, 1f);
                barRight.color = barLeft.color;
            }
            // the side-by-side: legacy left, new right, a hairline between them
            if (Mode == ViewMode.Split)
            {
                splitMask.transform.localPosition = new Vector3(realHalfW * 0.6f, 0f, 0f);
                splitMask.transform.localScale = new Vector3(realHalfW * 1.2f / SpriteWorld(splitMask.sprite).x,
                    halfH * 2.6f / SpriteWorld(splitMask.sprite).y, 1f);
                splitLine.enabled = true;
                FitScreen(splitLine, 2f * halfH / Mathf.Max(1, lines), halfH * 2.4f);
                splitLine.transform.localPosition = Vector3.zero;
                splitLine.color = new Color(0.9f, 0.85f, 0.7f, 0.35f);
            }
            else
            {
                splitLine.enabled = false;
            }
        }

        private void PaintMass(SpriteRenderer r, Vector2 at, float size, Color colour, float strength, int seed,
            float motion, float px)
        {
            if (strength <= 0.001f)
            {
                r.enabled = false;
                return;
            }
            r.enabled = true;
            float period = Mathf.Lerp(Style.DriftDurationMin, Style.DriftDurationMax,
                GameBackgroundBake.Hash(seed, 1, 3));
            float t = clock / period;
            Vector2 shift = new Vector2(GameBackgroundBake.Noise1(t, seed), GameBackgroundBake.Noise1(t, seed + 7))
                * Style.DriftDistance * px * motion;
            float scale = 1f + Style.DriftScale * motion * (0.5f + 0.5f * GameBackgroundBake.Noise1(t * 0.8f, seed + 13));
            float fade = 1f + Style.DriftOpacity * motion * GameBackgroundBake.Noise1(t * 1.3f, seed + 19);
            r.transform.localPosition = new Vector3(at.x + shift.x, at.y + shift.y, 0f);
            FitScreen(r, size * scale, size * scale);
            r.color = new Color(colour.r, colour.g, colour.b,
                Mathf.Clamp01(GameBackgroundBake.LinearAlpha(strength) * fade));
        }

        /// <summary>The board-anchored layers: the aura (with its breath), the stage, the warm
        /// pockets, the card grounding and the deck contacts.</summary>
        private void PaintStage(Mood mood, float dt)
        {
            bool show = runVisible && stageKnown;
            Vector2 stageSize = boardRect.size;
            if (show && (stageSize - builtStageSize).sqrMagnitude > 0.0004f)
            {
                DestroyTex(ref stageTex);
                DestroyTex(ref auraTex);
                stageTex = GameBackgroundBake.SoftBox(stageSize.x, stageSize.y, Style.StagePadding * 0.35f,
                    Style.StageFeather + Style.StagePadding * 0.65f, 24);
                auraTex = GameBackgroundBake.SoftBox(stageSize.x, stageSize.y, 0.2f, Style.AuraPadding, 16);
                SetSprite(stage, stageTex);
                SetSprite(aura, auraTex);
                builtStageSize = stageSize;
            }
            Color under = GameBackgroundBake.Palette.Lift;

            // THE AURA: separation, never a glow - and a breath too slow to be seen as one
            aura.enabled = show && Layers.ShowBoardAura;
            if (aura.enabled)
            {
                float reach = 0.2f + Style.AuraPadding;
                Place(aura, boardRect.center, stageSize.x + 2f * reach, stageSize.y + 2f * reach);
                aura.color = WithAlpha(GameBackgroundBake.Palette.Aura,
                    GameBackgroundBake.LiftAlpha(Style.Aura, GameBackgroundBake.Palette.Aura, under)
                    * AuraBreath() * Mathf.Clamp01(mood.TealStrength));
            }

            // THE STAGE: the environment shadow the board sits in, heavier underneath
            stage.enabled = show && Layers.ShowBoardStage;
            if (stage.enabled)
            {
                float reach = Style.StagePadding * 0.35f + Style.StageFeather + Style.StagePadding * 0.65f;
                Place(stage, boardRect.center + new Vector2(0f, -Style.StageDrop), stageSize.x + 2f * reach,
                    stageSize.y + 2f * reach + Style.StageDrop);
                stage.color = WithAlpha(GameBackgroundBake.Palette.StageInk, GameBackgroundBake.LinearAlpha(Style.Stage));
            }

            // THE WARM POCKETS: a breath of honey by the board's lower left and behind the deck
            bool warm = show && Layers.ShowWarmAccents;
            warmA.enabled = warm;
            warmB.enabled = warm;
            if (warm)
            {
                float w = Mathf.Max(0f, mood.Warmth);
                float a = GameBackgroundBake.LiftAlpha(Style.WarmAccent, GameBackgroundBake.Palette.Amber,
                    GameBackgroundBake.Palette.Petrol) * w;
                Place(warmA, new Vector2(boardRect.xMin - 0.6f, boardRect.yMin + 0.4f), 6.0f, 4.6f);
                warmA.color = WithAlpha(GameBackgroundBake.Palette.Amber, Mathf.Clamp01(a));
                Place(warmB, pileA + new Vector2(0f, 0.45f), 4.6f, 3.8f);
                warmB.color = WithAlpha(GameBackgroundBake.Palette.Amber, Mathf.Clamp01(a * 0.85f));
            }

            // THE CARD GROUNDING: a darker zone behind the hand - never a panel under it
            grounding.enabled = show && Layers.ShowCardGrounding && handRect.width > 0.01f;
            if (grounding.enabled)
            {
                Place(grounding, handRect.center, handRect.width * 1.25f + 1.6f, (handRect.height + 0.56f) * 2.1f);
                grounding.color = WithAlpha(GameBackgroundBake.Palette.StageInk,
                    GameBackgroundBake.LinearAlpha(Style.CardGrounding));
            }
            bool decks = show && Layers.ShowCardGrounding;
            deckA.enabled = decks;
            deckB.enabled = decks;
            if (decks)
            {
                float a = GameBackgroundBake.LinearAlpha(Style.DeckContact);
                Place(deckA, pileA + new Vector2(0f, -0.1f), 2.1f, 2.5f);
                deckA.color = WithAlpha(GameBackgroundBake.Palette.StageInk, a);
                Place(deckB, pileB + new Vector2(0f, -0.1f), 2.1f, 2.5f);
                deckB.color = WithAlpha(GameBackgroundBake.Palette.StageInk, a);
            }
        }

        /// <summary>1 -> 1.08 -> 0.98 -> 1, eased, over 6-9 s - a new length every breath, so it
        /// never settles into a rhythm.</summary>
        private float AuraBreath()
        {
            float motion = MotionFactor(Combined);
            if (motion <= 0f)
            {
                return 1f;
            }
            float u = (clock - auraCycleStart) / auraCyclePeriod;
            if (u >= 1f)
            {
                auraCycleStart = clock;
                auraCyclePeriod = Mathf.Lerp(Style.AuraPeriodMin, Style.AuraPeriodMax, (float)rng.NextDouble());
                u = 0f;
            }
            float k;
            if (u < 0.35f)
            {
                k = Mathf.Lerp(1f, 1.08f, Ease(u / 0.35f));
            }
            else if (u < 0.75f)
            {
                k = Mathf.Lerp(1.08f, 0.98f, Ease((u - 0.35f) / 0.4f));
            }
            else
            {
                k = Mathf.Lerp(0.98f, 1f, Ease((u - 0.75f) / 0.25f));
            }
            return 1f + (k - 1f) * motion;
        }

        // =================================================================== motes

        private void TickMotes(float halfW, float halfH, Vector2 camPos, Mood mood, int lines, float dt)
        {
            int wanted = MoteBudget();
            if (!runVisible || !Layers.ShowAmbientMotes)
            {
                wanted = 0;
            }
            float px = 2f * halfH / 1080f;
            float linesPx = 2f * halfH / Mathf.Max(1, lines);
            int alive = 0;
            for (int i = 0; i < motes.Count; i++)
            {
                Mote m = motes[i];
                if (!m.Alive)
                {
                    continue;
                }
                m.Age += dt;
                if (m.Age >= m.Life || i >= wanted)
                {
                    m.Alive = false;
                    m.Renderer.enabled = false;
                    continue;
                }
                alive++;
                m.Position += m.Velocity * dt * Mathf.Max(0.25f, MotionFactor(mood));
                float u = m.Age / m.Life;
                float envelope = u < 0.2f ? Ease(u / 0.2f) : u > 0.7f ? 1f - Ease((u - 0.7f) / 0.3f) : 1f;
                m.Renderer.transform.position = new Vector3(m.Position.x, m.Position.y, 0f);
                m.Renderer.color = WithAlpha(m.Colour, m.Alpha * envelope);
                m.Renderer.enabled = true;
            }
            // one at a time, spread over a lifetime - never a burst
            if (alive < wanted && clock >= nextMote)
            {
                for (int i = 0; i < motes.Count && i < wanted; i++)
                {
                    if (!motes[i].Alive)
                    {
                        Spawn(motes[i], halfW, halfH, camPos, px, linesPx);
                        break;
                    }
                }
                nextMote = clock + Mathf.Lerp(Style.MoteLifeMin, Style.MoteLifeMax, 0.5f) / Mathf.Max(1, wanted);
            }
        }

        private int MoteBudget()
        {
            switch (CurrentQuality)
            {
                case Quality.High:
                    return Application.isMobilePlatform || UiLayout.Active.IsPortrait
                        ? Style.MoteCountMobile : Style.MoteCountDesktop;
                case Quality.Medium:
                    return Style.MoteCountMedium;
                default:
                    return 0;
            }
        }

        private void Spawn(Mote m, float halfW, float halfH, Vector2 camPos, float px, float linesPx)
        {
            Vector2 at = Vector2.zero;
            bool found = false;
            for (int attempt = 0; attempt < 16 && !found; attempt++)
            {
                at = camPos + new Vector2(((float)rng.NextDouble() * 2f - 1f) * halfW * 0.96f,
                    ((float)rng.NextDouble() * 2f - 1f) * halfH * 0.94f);
                found = true;
                Rect board = stageKnown
                    ? new Rect(boardRect.xMin - Style.BoardMoteMargin, boardRect.yMin - Style.BoardMoteMargin,
                        boardRect.width + 2f * Style.BoardMoteMargin, boardRect.height + 2f * Style.BoardMoteMargin)
                    : new Rect();
                if (board.Contains(at) && rng.NextDouble() < Style.BoardMoteRejection)
                {
                    found = false;
                    continue;
                }
                for (int i = 0; i < safeRects.Count; i++)
                {
                    if (safeRects[i].Contains(at))
                    {
                        found = false;
                        break;
                    }
                }
            }
            if (!found)
            {
                return;
            }
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float speed = Mathf.Lerp(Style.MoteSpeedMin, Style.MoteSpeedMax, (float)rng.NextDouble()) * px;
            double roll = rng.NextDouble();
            bool gold = roll < 0.25;
            bool elongated = !gold && roll < 0.5;
            // 1-3 px, one in eight a softer 4 px
            float sizePx = rng.NextDouble() < 0.125 ? 4f : Mathf.Lerp(1.2f, 3f, (float)rng.NextDouble());
            m.Renderer.sprite = elongated ? moteLong : moteDot;
            float size = sizePx * linesPx * 2.2f; // the texture's soft edge takes about half of it
            m.Renderer.transform.localScale = new Vector3(size * (elongated ? 2f : 1f) / SpriteWorld(m.Renderer.sprite).x,
                size / SpriteWorld(m.Renderer.sprite).y, 1f);
            m.Renderer.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            m.Colour = gold ? GameBackgroundBake.Palette.MoteGold : GameBackgroundBake.Palette.MoteTeal;
            float perceptual = Mathf.Lerp(Style.MoteOpacityMin, Style.MoteOpacityMax, (float)rng.NextDouble());
            m.Alpha = GameBackgroundBake.LiftAlpha(perceptual, m.Colour, GameBackgroundBake.Palette.Petrol);
            m.Position = at;
            m.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            m.Age = 0f;
            m.Life = Mathf.Lerp(Style.MoteLifeMin, Style.MoteLifeMax, (float)rng.NextDouble());
            m.Alive = true;
            m.Renderer.enabled = true;
            m.Renderer.color = WithAlpha(m.Colour, 0f);
        }

        /// <summary>How much of the motion is on: the quality's share, the mood, and whether the
        /// lab has stopped it.</summary>
        private float MotionFactor(Mood mood)
        {
            if (Frozen)
            {
                return 0f;
            }
            float q = CurrentQuality == Quality.High ? 1f : CurrentQuality == Quality.Medium ? 0.5f : 0f;
            return q * Mathf.Max(0f, mood.MotionStrength);
        }

        // =================================================================== plumbing

        private IEnumerable<SpriteRenderer> AllLayers()
        {
            yield return baseField;
            yield return baseGrey;
            yield return navyMass;
            yield return deepMass;
            yield return warmA;
            yield return warmB;
            yield return aura;
            yield return stage;
            yield return grounding;
            yield return deckA;
            yield return deckB;
            yield return grain;
            yield return vignette;
            for (int i = 0; i < motes.Count; i++)
            {
                yield return motes[i].Renderer;
            }
        }

        private SpriteRenderer Layer(Transform parent, string name, int order, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        private static Sprite MakeSprite(Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                Mathf.Max(tex.width, tex.height));
        }

        private void SetSprite(SpriteRenderer r, Texture2D tex)
        {
            if (r.sprite != null && r.sprite.texture == tex)
            {
                return;
            }
            if (r.sprite != null)
            {
                Destroy(r.sprite);
            }
            r.sprite = MakeSprite(tex);
        }

        private static Vector2 SpriteWorld(Sprite s)
        {
            return s != null ? (Vector2)s.bounds.size : Vector2.one;
        }

        /// <summary>Scales a camera-parented layer to (w, h) in the camera's own units.</summary>
        private static void FitScreen(SpriteRenderer r, float w, float h)
        {
            Vector2 unit = SpriteWorld(r.sprite);
            r.transform.localScale = new Vector3(w / Mathf.Max(unit.x, 1e-4f), h / Mathf.Max(unit.y, 1e-4f), 1f);
        }

        /// <summary>Places a world layer's centre and size.</summary>
        private static void Place(SpriteRenderer r, Vector2 at, float w, float h)
        {
            r.transform.position = new Vector3(at.x, at.y, 0f);
            Vector2 unit = SpriteWorld(r.sprite);
            r.transform.localScale = new Vector3(w / Mathf.Max(unit.x, 1e-4f), h / Mathf.Max(unit.y, 1e-4f), 1f);
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private static float Ease(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        private static void DestroyTex(ref Texture2D tex)
        {
            if (tex != null)
            {
                Destroy(tex);
                tex = null;
            }
        }

        private void OnDestroy()
        {
            DestroyTex(ref baseTex);
            DestroyTex(ref greyTex);
            DestroyTex(ref vignetteTex);
            DestroyTex(ref stageTex);
            DestroyTex(ref auraTex);
        }
    }
}
