// PURPOSE: The main game's BACKGROUND - "DARK PLAYROOM / LIVING GAME TABLE" (2026-09-30). The
// surface the board, the blocks, the cream cards and the gold score line all sit on, built so they
// read as ONE art world instead of a dark board laid on a blue-grey wallpaper. It replaced
// BackdropView as what the player sees; BackdropView stays, untouched, as the LEGACY look.
//
// THE CORRECTIVE PASS (same day). The first pass got the colour right - deep petrol-teal - and the
// depth wrong: on screen it read as ONE flat teal wall. The colour direction is kept and what is
// added is STRUCTURE (see GameBackgroundLook.Corrective and GameBackgroundBake's note): a colder,
// deeper left, a navy right with a breath of plum, a warmer card zone, darker corners, organic edge
// depth, a soft lift behind the board, a pigment cloud that can be SEEN, a board that is SEATED
// (an environment stage, a tight contact shadow, an aura past both) and cards that are grounded and
// warmed. The first pass survives as a look for the lab's A/B.
//
// THE LAYERS (all below the legacy backdrop's orders and every boss atmosphere, so a boss look
// still replaces it; only the vignette sits where the old vignette sat):
//   BaseField        -239  baked, OPAQUE, in perceptual colour: petrol, the lift, the edge depth,
//                          the lower warmth, the corner pocket, the streams, the mid pigment, the
//                          score line's fade
//   BaseGrey         -238  its grey twin - the SATURATION multiplier, exact in linear space
//   MaterialMottle   -237  the large pigment cloud, drifting a few pixels
//   ColorMass_Left/Right -236 the two negative spaces' masses, drifting (18 s, ~15 px, +-2.5%)
//   PlumUndertone    -235  upper right, never a colour anyone names
//   WarmAccent_*     -234  honey by the board's lower corners, in the card band and behind the deck
//   BoardAmbientAura -233  a desaturated teal lift past the stage - separation, never a glow
//   BoardStageShadow -232  the ENVIRONMENT shadow: 60-110 px wider than the board, soft, heavier below
//   BoardContactShadow -231 the OBJECT shadow: tight, dark, a hair below the plate
//   CardGrounding    -230  a band behind the hand; DeckContact_* under both piles
//   Grain            -229  static, one texel per screen pixel
//   AmbientMotePool  -228  6-10 motes, never over the board or the UI
//   Vignette         -205  deep navy-petrol, deeper left and right than top and bottom
//
// NOTHING HERE READS GAMEPLAY. It is told where the board, the hand and the piles are (SetStage)
// and where motes may not be born (SetSafeRects); moods arrive as MULTIPLIERS (SetMood) from
// whoever owns a state - "debt" from the debt pressure today. The global multipliers (brightness,
// saturation, warmth, teal strength, plum strength, vignette, mote strength, motion) are the lab's.
//
// LOD: High (everything), Medium (half the motion, fewer motes), Low (static layers and the stage).
// No shader, no RenderTexture, no blur, no bloom dependence; the bake is a tenth of a second on a
// resize and never per frame.

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

        /// <summary>What is drawn: this background, the legacy one, the legacy one split against
        /// this (legacy left, this right), or COMPARE - this on the right and, on the left, whatever
        /// the lab puts outside the split mask (its first-pass copy).</summary>
        public enum ViewMode
        {
            New,
            Legacy,
            Split,
            Compare
        }

        /// <summary>A set of multipliers, all 1 at rest. Moods from different owners multiply.</summary>
        public struct Mood
        {
            public float Brightness;
            public float Saturation;
            public float Warmth;
            public float TealStrength;
            public float PlumStrength;
            public float Vignette;
            public float MoteStrength;
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
                        PlumStrength = 1f,
                        Vignette = 1f,
                        MoteStrength = 1f,
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
                    PlumStrength = PlumStrength * o.PlumStrength,
                    Vignette = Vignette * o.Vignette,
                    MoteStrength = MoteStrength * o.MoteStrength,
                    MotionStrength = MotionStrength * o.MotionStrength
                };
            }
        }

        /// <summary>The lab's switches. All on by default.</summary>
        public static class Layers
        {
            public static bool ShowBackgroundBase = true;
            public static bool ShowCenterLift = true;
            public static bool ShowEdgeDepth = true;
            public static bool ShowSideMasses = true;
            public static bool ShowFlows = true;
            public static bool ShowPlum = true;
            public static bool ShowWarmAccents = true;
            public static bool ShowBackgroundMottle = true;
            public static bool ShowBackgroundGrain = true;
            public static bool ShowBoardStage = true;
            public static bool ShowBoardAura = true;
            public static bool ShowBoardContact = true;
            public static bool ShowCardGrounding = true;
            public static bool ShowCardWarmth = true;
            public static bool ShowAmbientMotes = true;
            public static bool ShowVignette = true;

            public static void AllOn()
            {
                Set(true);
            }

            /// <summary>Everything off - for the lab's "X only" scenes, which then turn one on.</summary>
            public static void AllOff()
            {
                Set(false);
            }

            private static void Set(bool on)
            {
                ShowBackgroundBase = ShowCenterLift = ShowEdgeDepth = ShowSideMasses = ShowFlows = on;
                ShowPlum = ShowWarmAccents = ShowBackgroundMottle = ShowBackgroundGrain = on;
                ShowBoardStage = ShowBoardAura = ShowBoardContact = on;
                ShowCardGrounding = ShowCardWarmth = ShowAmbientMotes = ShowVignette = on;
            }

            /// <summary>The switches that change what is BAKED, as one key.</summary>
            public static int BakeKey
            {
                get
                {
                    return (ShowBackgroundBase ? 1 : 0) | (ShowCenterLift ? 2 : 0) | (ShowEdgeDepth ? 4 : 0)
                        | (ShowFlows ? 8 : 0) | (ShowWarmAccents ? 16 : 0) | (ShowBackgroundMottle ? 32 : 0);
                }
            }
        }

        /// <summary>The lab's global multipliers.</summary>
        public static Mood Global = Mood.Neutral;

        public Quality CurrentQuality { get; private set; }

        public ViewMode Mode { get; private set; }

        /// <summary>Every number this background is drawn with.</summary>
        public GameBackgroundLook Look { get; private set; }

        /// <summary>Motion stopped (the lab's "static" scenes). Everything holds where it is.</summary>
        public bool Frozen;

        /// <summary>The lab's aspect preview: laid out for a window of this shape inside the real
        /// one (bars fill the rest). Null is the real window.</summary>
        public float? PreviewAspect { get; private set; }

        /// <summary>The lab's small-window preview: pixel-sized things drawn as a window this many
        /// lines tall would draw them. Null is the real window.</summary>
        public int? PreviewLines { get; private set; }

        // =================================================================== state

        private Camera cam;
        private BackdropView legacy;
        private bool secondary;
        private SpriteMaskInteraction maskRole = SpriteMaskInteraction.None;
        private Transform screenRoot;
        private Transform worldRoot;
        private SpriteRenderer baseField;
        private SpriteRenderer baseGrey;
        private SpriteRenderer mottle;
        private SpriteRenderer massLeft;
        private SpriteRenderer massRight;
        private SpriteRenderer plum;
        private SpriteRenderer warmA;
        private SpriteRenderer warmB;
        private SpriteRenderer cardWarm;
        private SpriteRenderer deckWarm;
        private SpriteRenderer deckWarmB;
        private SpriteRenderer aura;
        private SpriteRenderer stage;
        private SpriteRenderer contact;
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
        private Texture2D mottleTex;
        private Texture2D stageTex;
        private Texture2D auraTex;
        private Texture2D contactTex;
        private Sprite blob;
        private Sprite moteDot;
        private Sprite moteLong;
        private Sprite white;
        private readonly List<Mote> motes = new List<Mote>();
        private readonly Dictionary<string, Mood> moods = new Dictionary<string, Mood>();
        private readonly List<Rect> safeRects = new List<Rect>();
        private readonly System.Random rng = new System.Random(20260930);

        private Rect boardRect;
        private Rect handRect;
        private Vector2 pileA;
        private Vector2 pileB;
        private bool stageKnown;
        private bool runVisible = true;

        private float builtHalfW = -1f;
        private float builtHalfH = -1f;
        private Vector2 builtBoard = new Vector2(float.NaN, float.NaN);
        private float builtBoardHalf = -1f;
        private Vector2 builtStageSize = new Vector2(-1f, -1f);
        private int builtKey = -1;
        private int builtLines = -1;
        private GameBackgroundLook builtLook;

        private float clock;
        private float auraCycleStart;
        private float auraCyclePeriod = 9f;
        private float nextMote;

        private const float Cover = 1.18f;
        private const int BaseOrder = -239;
        private const int GreyOrder = -238;
        private const int MottleOrder = -237;
        private const int MassOrder = -236;
        private const int PlumOrder = -235;
        private const int WarmOrder = -234;
        private const int AuraOrder = -233;
        private const int StageOrder = -232;
        private const int ContactOrder = -231;
        private const int GroundOrder = -230;
        private const int GrainOrder = -229;
        private const int MoteOrder = -228;
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

        /// <summary>Builds it. <paramref name="legacyBackdrop"/> is the old backdrop it hides and
        /// splits against; a SECONDARY instance (the lab's A/B copy) passes null and never touches
        /// the camera or the legacy look.</summary>
        public void Build(Camera camera, BackdropView legacyBackdrop, bool asSecondary = false)
        {
            cam = camera != null ? camera : Camera.main;
            legacy = legacyBackdrop;
            secondary = asSecondary;
            Look = GameBackgroundLook.Corrective();
            screenRoot = new GameObject(asSecondary ? "GameBackgroundRoot_Compare" : "GameBackgroundRoot").transform;
            screenRoot.SetParent(cam != null ? cam.transform : transform, false);
            screenRoot.localPosition = new Vector3(0f, 0f, 2f);
            worldRoot = new GameObject("GameBackgroundWorld").transform;
            worldRoot.SetParent(transform, false);

            white = MakeSprite(GameBackgroundBake.NewTexture(4, 4, FilterMode.Point, TextureWrapMode.Clamp), true);
            blob = MakeSprite(GameBackgroundBake.Blob(128), false);
            baseField = Layer(screenRoot, "BaseField", BaseOrder);
            baseGrey = Layer(screenRoot, "BaseGrey", GreyOrder);
            mottle = Layer(screenRoot, "MaterialMottle", MottleOrder);
            massLeft = Layer(screenRoot, "ColorMass_Left", MassOrder, blob);
            massRight = Layer(screenRoot, "ColorMass_Right", MassOrder, blob);
            plum = Layer(screenRoot, "PlumUndertone", PlumOrder, blob);
            warmA = Layer(worldRoot, "WarmAccent_BoardLeft", WarmOrder, blob);
            warmB = Layer(worldRoot, "WarmAccent_BoardRight", WarmOrder, blob);
            aura = Layer(worldRoot, "BoardAmbientAura", AuraOrder);
            stage = Layer(worldRoot, "BoardStageShadow", StageOrder);
            contact = Layer(worldRoot, "BoardContactShadow", ContactOrder);
            Sprite band = MakeSprite(GameBackgroundBake.Grounding(), false);
            grounding = Layer(worldRoot, "CardGrounding", GroundOrder, band);
            cardWarm = Layer(worldRoot, "WarmAccent_Cards", WarmOrder, band);
            deckWarm = Layer(worldRoot, "WarmAccent_Deck", WarmOrder, blob);
            deckWarmB = Layer(worldRoot, "WarmAccent_Discard", WarmOrder, blob);
            deckA = Layer(worldRoot, "DeckContact_Draw", GroundOrder, blob);
            deckB = Layer(worldRoot, "DeckContact_Discard", GroundOrder, blob);
            vignette = Layer(screenRoot, "Vignette", VignetteOrder);
            barLeft = Layer(screenRoot, "PreviewBarLeft", BarOrder, white);
            barRight = Layer(screenRoot, "PreviewBarRight", BarOrder, white);
            splitLine = Layer(screenRoot, "SplitLine", SplitLineOrder, white);

            var grainGo = new GameObject("Grain");
            grainGo.transform.SetParent(screenRoot, false);
            grain = grainGo.AddComponent<SpriteRenderer>();
            grain.sortingOrder = GrainOrder;

            moteDot = MakeSprite(GameBackgroundBake.Mote(false), false);
            moteLong = MakeSprite(GameBackgroundBake.Mote(true), false);
            for (int i = 0; i < 10; i++)
            {
                var m = new Mote { Renderer = Layer(worldRoot, "Mote" + i, MoteOrder, moteDot) };
                m.Renderer.enabled = false;
                motes.Add(m);
            }

            var maskGo = new GameObject("SplitMask");
            maskGo.transform.SetParent(screenRoot, false);
            splitMask = maskGo.AddComponent<SpriteMask>();
            splitMask.sprite = white;
            splitMask.isCustomRangeActive = true;
            splitMask.backSortingOrder = -241;
            splitMask.frontSortingOrder = -200;
            splitMask.enabled = false;

            CurrentQuality = Application.isMobilePlatform ? Quality.Medium : Quality.High;
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

        /// <summary>The rectangles a mote must never be born in, in the world.</summary>
        public void SetSafeRects(IList<Rect> rects)
        {
            safeRects.Clear();
            if (rects != null)
            {
                safeRects.AddRange(rects);
            }
        }

        /// <summary>The run's own layers go with the run (the title screen has no board to seat).</summary>
        public void SetRunVisible(bool visible)
        {
            runVisible = visible;
        }

        /// <summary>A mood from an owner of a state. Multipliers, all 1 at rest; moods from
        /// different owners multiply. Null clears the owner's.</summary>
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

        public void SetLook(GameBackgroundLook look)
        {
            Look = look ?? GameBackgroundLook.Corrective();
        }

        public void SetMode(ViewMode mode)
        {
            Mode = mode;
            bool showNew = mode != ViewMode.Legacy;
            screenRoot.gameObject.SetActive(showNew);
            worldRoot.gameObject.SetActive(showNew);
            if (legacy != null && !secondary)
            {
                legacy.SetVisible(mode == ViewMode.Legacy || mode == ViewMode.Split);
                legacy.SetMaskInteraction(mode == ViewMode.Split
                    ? SpriteMaskInteraction.VisibleOutsideMask : SpriteMaskInteraction.None);
            }
            bool split = mode == ViewMode.Split || mode == ViewMode.Compare;
            maskRole = split ? SpriteMaskInteraction.VisibleInsideMask
                : secondary ? maskRole : SpriteMaskInteraction.None;
            ApplyMaskRole();
            splitMask.enabled = split;
            if (cam != null && !secondary)
            {
                cam.backgroundColor = mode == ViewMode.Legacy ? BackdropView.Style.Ground
                    : GameBackgroundBake.Palette.Petrol;
            }
        }

        /// <summary>The lab's A/B copy draws only OUTSIDE the main instance's split mask.</summary>
        public void SetMaskRole(SpriteMaskInteraction role)
        {
            maskRole = role;
            ApplyMaskRole();
        }

        private void ApplyMaskRole()
        {
            foreach (SpriteRenderer r in AllLayers())
            {
                r.maskInteraction = maskRole;
            }
        }

        public void SetPreview(float? aspect, int? lines)
        {
            PreviewAspect = aspect;
            PreviewLines = lines;
        }

        /// <summary>Forces every baked texture to be redone on the next frame.</summary>
        public void Invalidate()
        {
            builtHalfW = -1f;
            builtStageSize = new Vector2(-1f, -1f);
            builtLook = null;
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
            float boardHalf = stageKnown ? Mathf.Max(boardRect.width, boardRect.height) * 0.5f : 3.3f;

            int key = Layers.BakeKey;
            if (!Mathf.Approximately(builtHalfW, halfW) || !Mathf.Approximately(builtHalfH, halfH)
                || builtKey != key || (boardLocal - builtBoard).sqrMagnitude > 0.04f
                || Mathf.Abs(boardHalf - builtBoardHalf) > 0.1f || builtLines != lines || builtLook != Look)
            {
                Bake(halfW, halfH, boardLocal, boardHalf, key, lines);
            }
            Mood mood = Combined;
            PaintScreen(halfW, halfH, realHalfW, mood, lines);
            PaintStage(mood);
            TickMotes(halfW, halfH, camPos, mood, lines, dt);
        }

        private void Bake(float halfW, float halfH, Vector2 boardLocal, float boardHalf, int key, int lines)
        {
            float coverW = halfW * Cover;
            float coverH = halfH * Cover;
            // A quarter of the screen's own resolution is plenty for a field whose finest baked
            // feature (the mid pigment) is ~65 px across; the grain carries the per-pixel detail.
            int th = Mathf.Clamp(Mathf.RoundToInt(lines * Cover / 4f), 120, 360);
            int tw = Mathf.Clamp(Mathf.RoundToInt(th * coverW / coverH), 120, 1100);
            if (baseTex == null || baseTex.width != tw || baseTex.height != th)
            {
                DestroyTex(ref baseTex);
                DestroyTex(ref greyTex);
                DestroyTex(ref vignetteTex);
                DestroyTex(ref mottleTex);
                baseTex = GameBackgroundBake.NewTexture(tw, th, FilterMode.Bilinear, TextureWrapMode.Clamp);
                greyTex = GameBackgroundBake.NewTexture(tw, th, FilterMode.Bilinear, TextureWrapMode.Clamp);
                vignetteTex = GameBackgroundBake.NewTexture(Mathf.Max(32, tw / 2), Mathf.Max(32, th / 2),
                    FilterMode.Bilinear, TextureWrapMode.Clamp);
                mottleTex = GameBackgroundBake.NewTexture(Mathf.Max(32, tw / 2), Mathf.Max(32, th / 2),
                    FilterMode.Bilinear, TextureWrapMode.Clamp);
            }
            GameBackgroundLook look = Look;
            var parts = new GameBackgroundBake.Parts
            {
                Base = Layers.ShowBackgroundBase,
                Lift = Layers.ShowCenterLift,
                Edge = Layers.ShowEdgeDepth,
                Flows = Layers.ShowFlows,
                Warm = Layers.ShowWarmAccents,
                Texture = Layers.ShowBackgroundMottle
            };
            if (look.FirstPassField)
            {
                // the first pass baked its masses and its texture as one; its switches follow suit
                parts.Lift = Layers.ShowCenterLift || Layers.ShowSideMasses;
            }
            GameBackgroundBake.BakeBase(baseTex, greyTex, coverW, coverH, halfW, halfH, boardLocal, boardHalf,
                parts, look);
            GameBackgroundBake.BakeVignette(vignetteTex, coverW, coverH, halfW, halfH, look.Vignette,
                look.VignetteAsymmetry, look.FirstPassField);
            // the pigment cloud is drawn a little larger than the screen so it can drift
            GameBackgroundBake.BakeMottle(mottleTex, coverW * 1.06f, coverH * 1.06f, halfH,
                look.LargeMottleScale, look.LargeMottle);
            SetSprite(baseField, baseTex);
            SetSprite(baseGrey, greyTex);
            SetSprite(vignette, vignetteTex);
            SetSprite(mottle, mottleTex);
            FitScreen(baseField, coverW * 2f, coverH * 2f);
            FitScreen(baseGrey, coverW * 2f, coverH * 2f);
            FitScreen(vignette, coverW * 2f, coverH * 2f);
            FitScreen(mottle, coverW * 2.12f, coverH * 2.12f);

            // the grain: one texel per (preview) screen pixel
            if (grain.sprite == null || builtLines != lines || builtLook != look)
            {
                if (grain.sprite != null)
                {
                    Destroy(grain.sprite.texture);
                    Destroy(grain.sprite);
                }
                Texture2D g = GameBackgroundBake.Grain(256, look.Grain);
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
            builtBoardHalf = boardHalf;
            builtKey = key;
            builtLines = lines;
            builtLook = look;
        }

        /// <summary>The screen-anchored layers: brightness, saturation, the drifting pigment and
        /// masses, the plum, the grain, the vignette, the preview bars and the split.</summary>
        private void PaintScreen(float halfW, float halfH, float realHalfW, Mood mood, int lines)
        {
            GameBackgroundLook look = Look;
            float bright = Mathf.Pow(Mathf.Max(0f, mood.Brightness), 2.2f);
            baseField.color = new Color(bright, bright, bright, 1f);
            baseField.enabled = true;
            baseGrey.color = new Color(bright, bright, bright, Mathf.Clamp01(1f - mood.Saturation));
            baseGrey.enabled = mood.Saturation < 0.999f;

            float motion = MotionFactor(mood);
            float px = 2f * halfH / 1080f;
            float aspect = halfW / halfH;

            // THE PIGMENT CLOUD: its own layer, drifting a few pixels over many seconds
            mottle.enabled = Layers.ShowBackgroundMottle && look.LargeMottle > 0f;
            if (mottle.enabled)
            {
                float t = clock / 40f;
                Vector2 drift = new Vector2(GameBackgroundBake.Noise1(t, 61), GameBackgroundBake.Noise1(t, 67))
                    * look.MottleDrift * 6f * px * motion;
                mottle.transform.localPosition = new Vector3(drift.x, drift.y, 0f);
                mottle.color = new Color(1f, 1f, 1f, 1f);
            }

            // THE SIDE MASSES: the colder left, the navy right - drifting on smooth noise
            float teal = Layers.ShowSideMasses ? Mathf.Clamp01(mood.TealStrength) : 0f;
            if (look.FirstPassField)
            {
                // the first pass: a navy mass upper right, a deep-teal one right of the board
                PaintMass(massRight, new Vector2(aspect * 0.86f, 0.78f) * halfH, 1.35f * halfH * 2f, 1.35f * halfH * 2f,
                    GameBackgroundBake.Palette.FirstNavy, look.RightMass * teal, 31, motion, px, look);
                Vector2 camPos = cam.transform.position;
                PaintMass(massLeft, new Vector2((stageKnown ? boardRect.xMax - camPos.x : 3.3f) + 1.2f,
                        (stageKnown ? boardRect.center.y - camPos.y : 0.9f) - 0.2f),
                    1.1f * halfH * 2f, 1.1f * halfH * 2f, GameBackgroundBake.Palette.FirstDeepTeal,
                    look.LeftMass * teal, 47, motion, px, look);
            }
            else
            {
                PaintMass(massLeft, new Vector2(-aspect * 0.80f, 0.10f) * halfH, 1.05f * halfH * 2f, 1.25f * halfH * 2f,
                    GameBackgroundBake.Palette.LeftNavy, look.LeftMass * teal, 47, motion, px, look);
                PaintMass(massRight, new Vector2(aspect * 0.80f, 0.20f) * halfH, 1.05f * halfH * 2f, 1.30f * halfH * 2f,
                    GameBackgroundBake.Palette.RightNavy, look.RightMass * teal, 31, motion, px, look);
            }
            // THE PLUM: upper right, never a colour anyone could name
            plum.enabled = Layers.ShowPlum && look.Plum > 0f;
            if (plum.enabled)
            {
                FitScreen(plum, 0.95f * halfH * 2f, 0.85f * halfH * 2f);
                plum.transform.localPosition = new Vector3(aspect * 0.78f * halfH, 0.55f * halfH, 0f);
                plum.color = WithAlpha(GameBackgroundBake.Palette.Plum, GameBackgroundBake.LiftAlpha(look.Plum,
                    GameBackgroundBake.Palette.Plum, GameBackgroundBake.Palette.RightNavy) * Mathf.Max(0f, mood.PlumStrength));
            }

            grain.enabled = Layers.ShowBackgroundGrain;
            vignette.enabled = Layers.ShowVignette;
            Color ink = look.FirstPassField ? GameBackgroundBake.Palette.FirstVignette : GameBackgroundBake.Palette.VignetteInk;
            vignette.color = new Color(ink.r, ink.g, ink.b,
                Mathf.Clamp01(GameBackgroundBake.LinearAlpha(look.Vignette) * mood.Vignette));

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
            // the side-by-side: whatever is outside the mask on the left, this one on the right
            if (Mode == ViewMode.Split || Mode == ViewMode.Compare)
            {
                splitMask.transform.localPosition = new Vector3(realHalfW * 0.6f, 0f, 0f);
                splitMask.transform.localScale = new Vector3(realHalfW * 1.2f / SpriteWorld(white).x,
                    halfH * 2.6f / SpriteWorld(white).y, 1f);
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

        private void PaintMass(SpriteRenderer r, Vector2 at, float w, float h, Color colour, float strength,
            int seed, float motion, float px, GameBackgroundLook look)
        {
            if (strength <= 0.001f)
            {
                r.enabled = false;
                return;
            }
            r.enabled = true;
            float period = look.DriftDuration * Mathf.Lerp(0.8f, 1.3f, GameBackgroundBake.Hash(seed, 1, 3));
            float t = clock / period;
            Vector2 shift = new Vector2(GameBackgroundBake.Noise1(t, seed), GameBackgroundBake.Noise1(t, seed + 7))
                * look.DriftDistance * px * motion;
            float scale = 1f + look.DriftScale * motion * GameBackgroundBake.Noise1(t * 0.8f, seed + 13);
            float fade = 1f + look.DriftOpacity * motion * GameBackgroundBake.Noise1(t * 1.3f, seed + 19);
            r.transform.localPosition = new Vector3(at.x + shift.x, at.y + shift.y, 0f);
            FitScreen(r, w * scale, h * scale);
            // LinearAlpha is exact only for a BLACK overlay; a navy over the petrol wants the
            // luminance solve, or the mass lands darker than the tone it was tuned to. The first
            // pass keeps its own conversion so the A/B shows what it really drew.
            float alpha = look.FirstPassField ? GameBackgroundBake.LinearAlpha(strength)
                : GameBackgroundBake.LiftAlpha(strength, colour, GameBackgroundBake.Palette.Petrol);
            r.color = new Color(colour.r, colour.g, colour.b, Mathf.Clamp01(alpha * fade));
        }

        /// <summary>The board- and card-anchored layers.</summary>
        private void PaintStage(Mood mood)
        {
            GameBackgroundLook look = Look;
            bool show = runVisible && stageKnown;
            Vector2 size = boardRect.size;
            if (show && ((size - builtStageSize).sqrMagnitude > 0.0004f || builtLook != look || stageTex == null))
            {
                DestroyTex(ref stageTex);
                DestroyTex(ref auraTex);
                DestroyTex(ref contactTex);
                // the environment stage: 60-110 px wider than the board, then a long feather
                stageTex = GameBackgroundBake.SoftBox(size.x, size.y, StageSolid(look), StageReach(look) - StageSolid(look),
                    Rounding(look, size, StageReach(look), 1.2f), 24);
                // the aura: past the stage, a lift with no edge
                auraTex = GameBackgroundBake.SoftBox(size.x, size.y, AuraSolid(look), AuraReach(look) - AuraSolid(look),
                    Rounding(look, size, AuraReach(look), 1.2f), 16);
                // the contact: tight, dark, sharp-ish
                contactTex = GameBackgroundBake.SoftBox(size.x, size.y, 0.02f, 0.28f, 0.35f, 40);
                SetSprite(stage, stageTex);
                SetSprite(aura, auraTex);
                SetSprite(contact, contactTex);
                builtStageSize = size;
            }
            float teal = Mathf.Clamp01(mood.TealStrength);

            // THE AURA: separation, never a glow - with a breath too slow to be seen as one
            aura.enabled = show && Layers.ShowBoardAura && look.Aura > 0f;
            if (aura.enabled)
            {
                float reach = AuraReach(look);
                Place(aura, boardRect.center, size.x + 2f * reach, size.y + 2f * reach);
                aura.color = WithAlpha(GameBackgroundBake.Palette.Aura,
                    GameBackgroundBake.LiftAlpha(look.Aura, GameBackgroundBake.Palette.Aura, GameBackgroundBake.Palette.Petrol)
                    * AuraBreath(mood) * teal);
            }

            // THE STAGE: the environment shadow the board sits in, heavier underneath
            stage.enabled = show && Layers.ShowBoardStage && look.Stage > 0f;
            if (stage.enabled)
            {
                float reach = StageReach(look);
                Place(stage, boardRect.center + new Vector2(0f, -look.StageDrop), size.x + 2f * reach,
                    size.y + 2f * reach + look.StageDrop);
                stage.color = WithAlpha(GameBackgroundBake.Palette.StageInk, GameBackgroundBake.LinearAlpha(look.Stage));
            }

            // THE CONTACT: the object's own shadow, tight and a hair below it
            contact.enabled = show && Layers.ShowBoardContact && look.Contact > 0f;
            if (contact.enabled)
            {
                const float reach = 0.3f;
                Place(contact, boardRect.center + new Vector2(0f, -0.06f), size.x + 2f * reach, size.y + 2f * reach);
                contact.color = WithAlpha(GameBackgroundBake.Palette.StageInk, GameBackgroundBake.LinearAlpha(look.Contact));
            }

            // THE WARMTH by the board's lower corners - atmosphere, never an orange patch
            float warmth = Mathf.Max(0f, mood.Warmth);
            float warmAlpha = GameBackgroundBake.LiftAlpha(look.WarmBoard, GameBackgroundBake.Palette.Amber,
                GameBackgroundBake.Palette.Petrol) * warmth;
            bool warm = show && Layers.ShowWarmAccents && look.WarmBoard > 0f;
            warmA.enabled = warm;
            warmB.enabled = warm && !look.FirstPassField;
            if (warm)
            {
                if (look.FirstPassField)
                {
                    Place(warmA, new Vector2(boardRect.xMin - 0.6f, boardRect.yMin + 0.4f), 6.0f, 4.6f);
                }
                else
                {
                    Place(warmA, new Vector2(boardRect.xMin, boardRect.yMin + 0.3f), 5.2f, 4.0f);
                    Place(warmB, new Vector2(boardRect.xMax, boardRect.yMin + 0.1f), 4.4f, 3.4f);
                    warmB.color = WithAlpha(GameBackgroundBake.Palette.Amber, Mathf.Clamp01(warmAlpha * 0.8f));
                }
                warmA.color = WithAlpha(GameBackgroundBake.Palette.Amber, Mathf.Clamp01(warmAlpha));
            }

            // THE CARD ROW: a grounding band behind the hand, and the honey in it
            bool cards = show && handRect.width > 0.01f;
            float bandW = handRect.width * 1.25f + 1.6f;
            float bandH = (handRect.height + look.CardGroundingHeight) * 2.1f;
            grounding.enabled = cards && Layers.ShowCardGrounding && look.CardGrounding > 0f;
            if (grounding.enabled)
            {
                Place(grounding, handRect.center, bandW, bandH);
                grounding.color = WithAlpha(GameBackgroundBake.Palette.StageInk,
                    GameBackgroundBake.LinearAlpha(look.CardGrounding));
            }
            cardWarm.enabled = cards && Layers.ShowCardWarmth && look.CardWarmth > 0f;
            if (cardWarm.enabled)
            {
                Place(cardWarm, handRect.center, bandW, bandH);
                cardWarm.color = WithAlpha(GameBackgroundBake.Palette.Amber, Mathf.Clamp01(
                    GameBackgroundBake.LiftAlpha(look.CardWarmth, GameBackgroundBake.Palette.Amber,
                        GameBackgroundBake.Palette.Petrol) * warmth));
            }
            // the piles: a contact under each, and a faint warm reflection behind the deck - no spotlight
            bool decks = show && Layers.ShowCardGrounding;
            deckA.enabled = decks;
            deckB.enabled = decks;
            if (decks)
            {
                float a = GameBackgroundBake.LinearAlpha(look.DeckContact);
                Place(deckA, pileA + new Vector2(0f, -0.12f), 2.1f, 2.5f);
                deckA.color = WithAlpha(GameBackgroundBake.Palette.StageInk, a);
                Place(deckB, pileB + new Vector2(0f, -0.12f), 2.1f, 2.5f);
                deckB.color = WithAlpha(GameBackgroundBake.Palette.StageInk, a);
            }
            deckWarm.enabled = show && Layers.ShowCardWarmth && look.DeckWarmth > 0f;
            deckWarmB.enabled = deckWarm.enabled && !look.FirstPassField;
            if (deckWarm.enabled)
            {
                Color honey = WithAlpha(GameBackgroundBake.Palette.Amber, Mathf.Clamp01(
                    GameBackgroundBake.LiftAlpha(look.DeckWarmth, GameBackgroundBake.Palette.Amber,
                        GameBackgroundBake.Palette.Petrol) * warmth));
                if (look.FirstPassField)
                {
                    Place(deckWarm, pileA + new Vector2(0f, 0.45f), 4.6f, 3.8f);
                }
                else
                {
                    Place(deckWarm, pileA + new Vector2(0f, 0.5f), 3.8f, 3.2f);
                    Place(deckWarmB, pileB + new Vector2(0f, 0.5f), 3.8f, 3.2f);
                    deckWarmB.color = honey;
                }
                deckWarm.color = honey;
            }
        }

        // The first pass seated the board in a narrower stage and a wide aura; the corrective one
        // widens the stage (60-110 px) and keeps the aura just past it (20-50 px).
        private static float StageSolid(GameBackgroundLook look)
        {
            return look.StagePadding * (look.FirstPassField ? 0.35f : 0.3f);
        }

        private static float StageReach(GameBackgroundLook look)
        {
            return look.StagePadding + look.StageFeather;
        }

        private static float AuraSolid(GameBackgroundLook look)
        {
            return look.FirstPassField ? 0.2f : look.StagePadding;
        }

        private static float AuraReach(GameBackgroundLook look)
        {
            return look.FirstPassField ? 0.2f + look.AuraPadding : look.StagePadding + look.AuraPadding + 0.6f;
        }

        /// <summary>The first pass rounded its fields by the box and the reach; the corrective
        /// pass uses one generous radius.</summary>
        private static float Rounding(GameBackgroundLook look, Vector2 size, float reach, float corrective)
        {
            return look.FirstPassField ? Mathf.Min(size.x, size.y) * 0.12f + reach * 0.5f : corrective;
        }

        /// <summary>1 -> 1+swing -> 1-swing*0.8 -> 1, eased, over the look's period - a new length
        /// every breath, so it never settles into a rhythm.</summary>
        private float AuraBreath(Mood mood)
        {
            float motion = MotionFactor(mood);
            if (motion <= 0f)
            {
                return 1f;
            }
            GameBackgroundLook look = Look;
            float u = (clock - auraCycleStart) / auraCyclePeriod;
            if (u >= 1f)
            {
                auraCycleStart = clock;
                auraCyclePeriod = Mathf.Lerp(look.AuraPeriodMin, look.AuraPeriodMax, (float)rng.NextDouble());
                u = 0f;
            }
            float up = 1f + look.AuraSwing;
            float down = 1f - look.AuraSwing * 0.8f;
            float k = u < 0.35f ? Mathf.Lerp(1f, up, Ease(u / 0.35f))
                : u < 0.75f ? Mathf.Lerp(up, down, Ease((u - 0.35f) / 0.4f))
                : Mathf.Lerp(down, 1f, Ease((u - 0.75f) / 0.25f));
            return 1f + (k - 1f) * motion;
        }

        // =================================================================== motes

        private void TickMotes(float halfW, float halfH, Vector2 camPos, Mood mood, int lines, float dt)
        {
            int wanted = MoteBudget();
            if (!runVisible || !Layers.ShowAmbientMotes || mood.MoteStrength <= 0f)
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
                m.Position += m.Velocity * dt;
                float u = m.Age / m.Life;
                float envelope = u < 0.2f ? Ease(u / 0.2f) : u > 0.7f ? 1f - Ease((u - 0.7f) / 0.3f) : 1f;
                m.Renderer.transform.position = new Vector3(m.Position.x, m.Position.y, 0f);
                m.Renderer.color = WithAlpha(m.Colour, m.Alpha * envelope * Mathf.Clamp01(mood.MoteStrength));
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
                nextMote = clock + (Look.MoteLifeMin + Look.MoteLifeMax) * 0.5f / Mathf.Max(1, wanted);
            }
        }

        private int MoteBudget()
        {
            int full = Mathf.Min(motes.Count, Look.MoteCount);
            switch (CurrentQuality)
            {
                case Quality.High:
                    return Application.isMobilePlatform || UiLayout.Active.IsPortrait ? Mathf.Min(full, 6) : full;
                case Quality.Medium:
                    return Mathf.Max(1, full / 2);
                default:
                    return 0;
            }
        }

        private void Spawn(Mote m, float halfW, float halfH, Vector2 camPos, float px, float linesPx)
        {
            GameBackgroundLook look = Look;
            Vector2 at = Vector2.zero;
            bool found = false;
            Rect board = stageKnown
                ? new Rect(boardRect.xMin - 0.6f, boardRect.yMin - 0.6f, boardRect.width + 1.2f, boardRect.height + 1.2f)
                : new Rect();
            for (int attempt = 0; attempt < 16 && !found; attempt++)
            {
                at = camPos + new Vector2(((float)rng.NextDouble() * 2f - 1f) * halfW * 0.96f,
                    ((float)rng.NextDouble() * 2f - 1f) * halfH * 0.94f);
                found = !board.Contains(at);
                for (int i = 0; found && i < safeRects.Count; i++)
                {
                    if (safeRects[i].Contains(at))
                    {
                        found = false;
                    }
                }
            }
            if (!found)
            {
                return;
            }
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            m.Life = Mathf.Lerp(look.MoteLifeMin, look.MoteLifeMax, (float)rng.NextDouble());
            // it wanders 10-30 px over its whole life, and the motion strength scales that
            float distance = Mathf.Lerp(look.MoteDistanceMin, look.MoteDistanceMax, (float)rng.NextDouble()) * px
                * Mathf.Max(0.25f, Mathf.Clamp(Combined.MotionStrength, 0f, 2f));
            double roll = rng.NextDouble();
            // 70% pale teal, 20% grey-aqua, 10% muted gold
            Color colour = roll < 0.7 ? GameBackgroundBake.Palette.MoteTeal
                : roll < 0.9 ? GameBackgroundBake.Palette.MoteAqua : GameBackgroundBake.Palette.MoteGold;
            bool elongated = roll < 0.25;
            // 1-3 px, one in eight a softer 4 px
            float sizePx = rng.NextDouble() < 0.125 ? 4f : Mathf.Lerp(1.2f, 3f, (float)rng.NextDouble());
            m.Renderer.sprite = elongated ? moteLong : moteDot;
            float size = sizePx * linesPx * 2.2f; // the texture's soft edge takes about half of it
            m.Renderer.transform.localScale = new Vector3(size * (elongated ? 2f : 1f) / SpriteWorld(m.Renderer.sprite).x,
                size / SpriteWorld(m.Renderer.sprite).y, 1f);
            m.Renderer.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            m.Colour = colour;
            float perceptual = Mathf.Lerp(look.MoteOpacityMin, look.MoteOpacityMax, (float)rng.NextDouble());
            m.Alpha = GameBackgroundBake.LiftAlpha(perceptual, colour, GameBackgroundBake.Palette.Petrol);
            m.Position = at;
            m.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (distance / m.Life);
            m.Age = 0f;
            m.Alive = true;
            m.Renderer.enabled = true;
            m.Renderer.color = WithAlpha(colour, 0f);
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
            yield return mottle;
            yield return massLeft;
            yield return massRight;
            yield return plum;
            yield return warmA;
            yield return warmB;
            yield return cardWarm;
            yield return deckWarm;
            yield return deckWarmB;
            yield return aura;
            yield return stage;
            yield return contact;
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

        /// <summary>FullRect, always: a tight mesh would trace the outline of every soft texture
        /// on the CPU for nothing, and a flat white one has no outline to trace.</summary>
        private static Sprite MakeSprite(Texture2D tex, bool fillWhite)
        {
            if (fillWhite)
            {
                var px = new Color32[tex.width * tex.height];
                for (int i = 0; i < px.Length; i++)
                {
                    px[i] = new Color32(255, 255, 255, 255);
                }
                tex.SetPixels32(px);
                tex.Apply(false);
            }
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                Mathf.Max(tex.width, tex.height), 0, SpriteMeshType.FullRect);
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
            r.sprite = MakeSprite(tex, false);
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
            DestroyTex(ref mottleTex);
            DestroyTex(ref stageTex);
            DestroyTex(ref auraTex);
            DestroyTex(ref contactTex);
            if (screenRoot != null)
            {
                Destroy(screenRoot.gameObject);
            }
        }
    }
}
