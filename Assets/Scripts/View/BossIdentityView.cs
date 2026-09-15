// PURPOSE: BOSS STAGE IDENTITY - what makes a boss stage look like a threat. Two halves, both
// chosen per boss by its THEME (BossThemes) and previewable on their own in the F7 lab
// (GameUiController.BossIdentity):
//
//   INTRO    - a one-shot sequence when the boss stage opens (BossIntroStyle), ending on a title
//              card that waits for a click or key.
//   AMBIENCE - a persistent look that stays on for the whole stage (BossAmbience), so a player
//              who glances at the screen mid-round still knows they are in a boss fight.
//
// LAYOUT OF THE CODE:
//   BossIdentityView.cs            - lifecycle, the intro runner, generated sprites, helpers
//   BossIdentityView.Rigs.cs       - tape, seal, cage, eclipse, aura, letterbox + the ambience poser
//   BossIdentityView.Intros.cs     - Alarm, Eclipse, Seal, Lockdown, Cinematic
//   BossIdentityView.Worlds.cs     - Orbital / Gravity well and Lava lake / Eruption
//   BossIdentityView.Atmosphere.cs - the red backdrop and sparks under every look
//
// HOW AN INTRO IS WRITTEN. Every intro is a POSE FUNCTION of absolute time plus a list of
// one-shot CUES (shake, sound). Nothing is tweened incrementally, so skipping is a jump to the end
// and the lab's slow-motion setting cannot drift anything out of step.
//
// TWO ROOTS. Board-anchored pieces (the seal, the cage, the corona) live in WORLD space and shake
// with the world. Screen pieces (veils, bands, title text, letterbox) live on a root that follows
// the camera's REST position, so they stay nailed to the screen while the world shakes under them.
//
// The intro dims the HUD canvas through a callback (HudAlpha) rather than drawing over it: the
// HUD is a screen-space overlay canvas and no world sorting order can cover it.
//
// Rules never live here - this reads a board rectangle and a few strings, nothing else.
// EXTENSION POINT: a new style is one enum value, one Build*/Start* method and one switch case.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The one-shot sequence that opens a boss stage.</summary>
    public enum BossIntroStyle
    {
        None,
        Alarm,
        Eclipse,
        Seal,
        Lockdown,
        Cinematic,
        Orbit,
        Eruption
    }

    /// <summary>The persistent look a boss stage keeps while it is being played.</summary>
    public enum BossAmbience
    {
        None,
        HazardTape,
        BloodEclipse,
        RuneCircle,
        IronCage,
        Letterbox,
        Aura,
        Orbital,
        LavaLake
    }

    /// <summary>Which synthesized sting an intro asks for (see SoundFx.BossSting).</summary>
    public enum BossSting
    {
        Siren,
        Boom,
        Gong,
        Clank,
        Drone,
        Magma
    }

    /// <summary>Boss stage intro + ambience prototypes. Build once; drive from the controller.</summary>
    public sealed partial class BossIdentityView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            /// <summary>The threat red - the boss badge's own mark colour, so every style speaks
            /// the language the badge already started.</summary>
            public static Color Threat = new Color(0.90f, 0.20f, 0.26f);

            public static Color Ink = new Color(0.07f, 0.04f, 0.05f);
            public static Color Bone = new Color(0.97f, 0.92f, 0.87f);
            public static Color Maroon = new Color(0.22f, 0.02f, 0.06f);
            public static Color Corona = new Color(1f, 0.34f, 0.18f);
            public static Color Arcane = new Color(0.96f, 0.27f, 0.46f);
            public static Color Iron = new Color(0.24f, 0.25f, 0.29f);
            public static Color IronLight = new Color(0.50f, 0.52f, 0.58f);
            public static Color IronDark = new Color(0.07f, 0.07f, 0.09f);

            /// <summary>How far the blood eclipse pulls the backdrop toward maroon.</summary>
            public static float EclipseTint = 0.72f;

            /// <summary>Seconds for an ambience to come up (or go) - also the length of the
            /// crossfade with the intro that hands over to it.</summary>
            public static float AmbienceFade = 0.45f;

            /// <summary>Peak darkness of the veil an intro lays over the world.</summary>
            public static float VeilDarkness = 0.55f;

            /// <summary>How far the HUD fades while a title card is up.</summary>
            public static float HudDim = 0.18f;
        }

        // Sorting. Behind the board plate (BoardSurfaceView is -3) for everything the board should
        // sit ON; above the overtime vignette (60) for everything an intro lays over the screen.
        private const int TintOrder = -208;
        private const int HaloOrder = -60;
        private const int SealGlowOrder = -58;
        private const int SealLineOrder = -56;
        private const int RuneOrder = -54;
        private const int FlashBehindOrder = -52;
        private const int EmberOrder = -50;
        private const int AuraOrder = -45;
        private const int FrameOrder = -2;
        private const int AmbientBarOrder = -6;
        private const int VeilOrder = 64;
        private const int FxOrder = 68;
        private const int BandOrder = 70;
        private const int CinemaBarOrder = 74;
        private const int TextShadowOrder = 76;
        private const int TextOrder = 77;

        // =================================================================== wiring
        /// <summary>(amplitude, duration) - the controller's camera shake.</summary>
        public Action<float, float> Shake;

        /// <summary>0..1 - the HUD canvas's alpha.</summary>
        public Action<float> HudAlpha;

        public Action<BossSting> Sting;

        /// <summary>Where the camera sits when nothing is shaking it.</summary>
        public Func<Vector3> CameraRest;

        /// <summary>The arena's world rectangle, or a stand-in when no board is up.</summary>
        public Func<Rect> BoardRect;

        /// <summary>Lab switch: intros may kick the camera.</summary>
        public bool CameraKick = true;

        /// <summary>Lab switch: playback speed for watching an intro frame by frame.</summary>
        public float Speed = 1f;

        private Camera cam;
        private Transform worldRoot;
        private Transform screenRoot;
        private Transform introWorld;
        private Transform introScreen;
        private bool visible = true;

        // Read once per frame and shared by every pose.
        private Rect rect;
        private float halfW;
        private float halfH;

        /// <summary>The board's centre relative to the camera's rest position - where a title
        /// card goes on the screen root.</summary>
        private Vector2 boardOnScreen;

        public void Build(Camera camera)
        {
            cam = camera != null ? camera : Camera.main;
            worldRoot = new GameObject("BossIdentityWorld").transform;
            worldRoot.SetParent(transform, false);
            screenRoot = new GameObject("BossIdentityScreen").transform;
            screenRoot.SetParent(transform, false);
            introWorld = new GameObject("IntroWorld").transform;
            introWorld.SetParent(worldRoot, false);
            introScreen = new GameObject("IntroScreen").transform;
            introScreen.SetParent(screenRoot, false);
            ambWorld = new GameObject("AmbienceWorld").transform;
            ambWorld.SetParent(worldRoot, false);
            ambScreen = new GameObject("AmbienceScreen").transform;
            ambScreen.SetParent(screenRoot, false);
            BuildAtmosphere();
        }

        /// <summary>Hides everything (a menu or another full-screen tool owns the frame). An
        /// intro cannot survive being hidden - it is skipped, so the HUD comes back.</summary>
        public void SetVisible(bool on)
        {
            if (visible == on)
            {
                return;
            }
            visible = on;
            if (!on && run != null)
            {
                SkipIntro();
            }
            if (worldRoot != null)
            {
                worldRoot.gameObject.SetActive(on);
                screenRoot.gameObject.SetActive(on);
            }
        }

        private void LateUpdate()
        {
            if (cam == null || worldRoot == null)
            {
                return;
            }
            Vector3 rest = CameraRest != null ? CameraRest() : cam.transform.position;
            screenRoot.position = new Vector3(rest.x, rest.y, rest.z + cam.nearClipPlane + 1f);
            halfH = cam.orthographicSize;
            halfW = halfH * cam.aspect;
            rect = BoardRect != null ? BoardRect() : new Rect(-3.25f, -2.35f, 6.5f, 6.5f);
            boardOnScreen = rect.center - new Vector2(rest.x, rest.y);
            if (!visible)
            {
                return;
            }
            float dt = Time.deltaTime * Mathf.Max(0.01f, Speed);
            orbitClock += dt;
            TickReactions(dt);
            TickIntro(dt);
            TickAmbience(dt);
            TickAtmosphere(dt);
            PoseReactions();
        }

        // =================================================================== the intro runner

        private sealed class Cue
        {
            public float At;
            public Action Fire;
            public bool Done;
        }

        private sealed class IntroRun
        {
            public float Duration;

            /// <summary>When the intro starts handing the screen over to the ambience.</summary>
            public float HandOver;

            public Action<float> Pose;
            public readonly List<Cue> Cues = new List<Cue>();
            public Action Done;
            public float T;

            /// <summary>The moment the whole title card (name AND rule) is on screen. With
            /// HoldTitle the intro stops here until the player presses something.</summary>
            public float HoldAt = float.MaxValue;

            /// <summary>When the title card's backing panel fades in (x..y) and out (x..y). An
            /// intro that brings its own plate (Alarm) leaves it at MaxValue: no panel.</summary>
            public Vector2 CardIn = new Vector2(float.MaxValue, float.MaxValue);

            public Vector2 CardOut = new Vector2(float.MaxValue, float.MaxValue);

            public bool Released;
        }

        /// <summary>Lab switch: the title card waits at HoldAt for a click or key, so the rule
        /// can be read at the player's own pace. Moving the pointer does not count.</summary>
        public bool HoldTitle = true;

        /// <summary>True while the intro is parked on its title card waiting for a press.</summary>
        public bool IntroWaiting
        {
            get { return run != null && HoldTitle && !run.Released && run.T >= run.HoldAt; }
        }

        /// <summary>What a press does to the intro: before the card is complete it jumps to the
        /// complete card (dropping the cues on the way, so nothing shakes late); while the card
        /// waits it lets it go; after that it skips the rest.</summary>
        public void AdvanceIntro()
        {
            if (run == null)
            {
                return;
            }
            if (HoldTitle && !run.Released && run.T < run.HoldAt)
            {
                run.T = run.HoldAt;
                for (int i = 0; i < run.Cues.Count; i++)
                {
                    if (run.Cues[i].At <= run.T)
                    {
                        run.Cues[i].Done = true;
                    }
                }
                run.Pose(run.T);
                PoseCard();
                return;
            }
            if (HoldTitle && !run.Released)
            {
                run.Released = true;
                return;
            }
            SkipIntro();
        }

        private IntroRun run;

        public bool IntroPlaying
        {
            get { return run != null; }
        }

        /// <summary>Plays one intro. Any intro already running is cut. <paramref name="done"/> runs
        /// when it finishes OR is skipped.</summary>
        public void PlayIntro(BossIntroStyle style, string bossName, string rule, string caption,
            Action done)
        {
            if (run != null)
            {
                SkipIntro();
            }
            if (style == BossIntroStyle.None || worldRoot == null)
            {
                if (done != null)
                {
                    done();
                }
                return;
            }
            // The numbers the first pose needs, in case the intro starts before LateUpdate has run.
            if (cam != null)
            {
                halfH = cam.orthographicSize;
                halfW = halfH * cam.aspect;
            }
            run = new IntroRun();
            run.Done = done;
            string name = string.IsNullOrEmpty(bossName) ? "BOSS" : bossName;
            string text = ViewUtil.WrapText(rule ?? string.Empty, 58, 3);
            switch (style)
            {
                case BossIntroStyle.Alarm: StartAlarm(name, text); break;
                case BossIntroStyle.Eclipse: StartEclipse(name, text, caption); break;
                case BossIntroStyle.Seal: StartSeal(name, text, caption); break;
                case BossIntroStyle.Lockdown: StartLockdown(name, text, caption); break;
                case BossIntroStyle.Cinematic: StartCinematic(name, text, caption); break;
                case BossIntroStyle.Orbit: StartOrbit(name, text, caption); break;
                case BossIntroStyle.Eruption: StartEruption(name, text, caption); break;
            }
            // EVERY intro waits on its title card: one that forgot to name its hold point waits
            // just before it starts handing the screen over.
            if (run.HoldAt == float.MaxValue)
            {
                run.HoldAt = Mathf.Max(0f, run.HandOver - 0.3f);
            }
            BuildCard(name, text);
            prompt = Label(introScreen, "Prompt",
                Loc.Pick("click to continue", "devam etmek için tıkla"), 0.17f, TextOrder, false);
            run.Pose(0f);
        }

        private TextMesh prompt;

        // ---- the title card's backing panel ----
        //
        // The title text used to sit straight on the board, where a 7x7 grid of slots runs right
        // through the rule line. Every intro now puts it on one dark rounded panel sized to the
        // text, shown on the intro's own card timing (IntroRun.CardIn/CardOut).

        private SpriteRenderer cardFill;
        private SpriteRenderer cardEdge;
        private float cardWidth;

        private static readonly Color CardFillColour = new Color(0.045f, 0.025f, 0.035f);
        private const float CardFillAlpha = 0.88f;
        private const float CardTop = 1.12f;
        private const float CardBottom = -1.95f;

        private void BuildCard(string bossName, string rule)
        {
            TextMesh measure = Label(introScreen, "Measure", string.Empty, 0.8f, TextOrder, true);
            float width = TextWidth(measure, Upper(bossName)) * 1.1f;
            TextMesh measureRule = Label(introScreen, "MeasureRule", string.Empty, 0.17f, TextOrder, false);
            foreach (string line in rule.Split('\n'))
            {
                width = Mathf.Max(width, TextWidth(measureRule, line));
            }
            cardWidth = width + 1.0f;
            cardEdge = SlicedPart(introScreen, "CardEdge", TextShadowOrder - 3);
            cardFill = SlicedPart(introScreen, "CardFill", TextShadowOrder - 2);
        }

        private static SpriteRenderer SlicedPart(Transform parent, string name, int order)
        {
            SpriteRenderer sr = Part(parent, name, ViewUtil.RoundedSprite, order);
            sr.drawMode = SpriteDrawMode.Sliced;
            return sr;
        }

        private void PoseCard()
        {
            if (cardFill == null || run == null)
            {
                return;
            }
            float a = run.CardIn.x >= float.MaxValue
                ? 0f
                : Env(run.T, run.CardIn.x, run.CardIn.y, run.CardOut.x, run.CardOut.y);
            float width = Mathf.Clamp(cardWidth, 4f, Mathf.Max(4f, rect.width * 0.96f));
            var size = new Vector2(width, CardTop - CardBottom);
            var centre = boardOnScreen + new Vector2(0f, (CardTop + CardBottom) * 0.5f);
            PlaceSliced(cardEdge, centre, size + Vector2.one * 0.06f, Style.Threat, 0.55f * a);
            PlaceSliced(cardFill, centre, size, CardFillColour, CardFillAlpha * a);
        }

        private static void PlaceSliced(SpriteRenderer sr, Vector2 centre, Vector2 size, Color c, float alpha)
        {
            sr.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
            sr.transform.localScale = Vector3.one;
            sr.size = size;
            c.a = Mathf.Clamp01(alpha);
            sr.color = c;
        }

        /// <summary>Says the card is waiting - blinking, under the rule text, only while held.</summary>
        private void PosePrompt()
        {
            if (prompt == null)
            {
                return;
            }
            float a = IntroWaiting ? 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f) : 0f;
            PutLabel(prompt, boardOnScreen + new Vector2(0f, -1.6f), 1f, Style.Bone, a);
        }

        /// <summary>Ends the running intro at once: its pieces go, the HUD comes back, the cues it
        /// had not fired yet are dropped (a skipped intro must not shake the screen later).</summary>
        public void SkipIntro()
        {
            if (run == null)
            {
                return;
            }
            FinishIntro();
        }

        private void TickIntro(float dt)
        {
            if (run == null)
            {
                return;
            }
            run.T += dt;
            if (HoldTitle && !run.Released && run.T > run.HoldAt)
            {
                run.T = run.HoldAt;
            }
            for (int i = 0; i < run.Cues.Count; i++)
            {
                Cue cue = run.Cues[i];
                if (!cue.Done && run.T >= cue.At)
                {
                    cue.Done = true;
                    cue.Fire();
                }
            }
            if (run.T >= run.Duration)
            {
                FinishIntro();
                return;
            }
            run.Pose(run.T);
            PoseCard();
            PosePrompt();
        }

        private void FinishIntro()
        {
            Action done = run.Done;
            run = null;
            ClearChildren(introWorld);
            ClearChildren(introScreen);
            SetHud(1f);
            if (done != null)
            {
                done();
            }
        }

        /// <summary>True while an intro holds the screen and the ambience should wait under it.</summary>
        private bool IntroHoldsScreen
        {
            get { return run != null && run.T < run.HandOver; }
        }

        private void AddCue(float at, Action fire)
        {
            run.Cues.Add(new Cue { At = at, Fire = fire });
        }

        private void CueShake(float at, float amplitude, float duration)
        {
            AddCue(at, delegate
            {
                if (CameraKick && Shake != null)
                {
                    Shake(amplitude, duration);
                }
            });
        }

        private void CueSting(float at, BossSting sting)
        {
            AddCue(at, delegate
            {
                if (Sting != null)
                {
                    Sting(sting);
                }
            });
        }

        private void SetHud(float alpha)
        {
            if (HudAlpha != null)
            {
                HudAlpha(Mathf.Clamp01(alpha));
            }
        }

        // =================================================================== easing

        private static float Seg(float t, float a, float b)
        {
            return Mathf.Clamp01((t - a) / Mathf.Max(0.0001f, b - a));
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static float OutCubic(float x)
        {
            x = 1f - Mathf.Clamp01(x);
            return 1f - x * x * x;
        }

        private static float InCubic(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * x;
        }

        private static float OutBack(float x)
        {
            x = Mathf.Clamp01(x) - 1f;
            const float s = 1.6f;
            return 1f + x * x * ((s + 1f) * x + s);
        }

        /// <summary>A rise-then-fall envelope: 0 before <paramref name="inA"/>, 1 between the
        /// two ramps, 0 after <paramref name="outB"/>.</summary>
        private static float Env(float t, float inA, float inB, float outA, float outB)
        {
            return Smooth(Seg(t, inA, inB)) * (1f - Smooth(Seg(t, outA, outB)));
        }

        /// <summary>A sharp hit that decays: 0 before <paramref name="at"/>, 1 on it.</summary>
        private static float Hit(float t, float at, float decay)
        {
            return t < at ? 0f : Mathf.Exp(-(t - at) * decay);
        }

        /// <summary>Stable 0..1 noise per index, so nothing here ever calls Random.</summary>
        private static float Hash(int i, int salt)
        {
            unchecked
            {
                uint h = (uint)(i * 374761393 + salt * 668265263 + 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h / 4294967295f;
            }
        }

        // =================================================================== part helpers

        private static SpriteRenderer Part(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = Color.clear;
            return sr;
        }

        /// <summary>Places a sprite by CENTRE and WORLD SIZE - the sprites here are all one unit
        /// across, but it divides by the real bounds anyway so a stray asset cannot lie.</summary>
        private static void Put(SpriteRenderer sr, Vector2 position, Vector2 size, float degrees,
            Color colour, float alpha)
        {
            Transform t = sr.transform;
            t.localPosition = new Vector3(position.x, position.y, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, degrees);
            Vector2 bounds = sr.sprite.bounds.size;
            t.localScale = new Vector3(size.x / Mathf.Max(0.0001f, bounds.x),
                size.y / Mathf.Max(0.0001f, bounds.y), 1f);
            colour.a *= Mathf.Clamp01(alpha);
            sr.color = colour;
        }

        private static SpriteRenderer TiledPart(Transform parent, string name, Sprite tile, int order)
        {
            SpriteRenderer sr = Part(parent, name, tile, order);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            return sr;
        }

        private static void PutTiled(SpriteRenderer sr, Vector2 position, Vector2 size,
            float degrees, float alpha)
        {
            Transform t = sr.transform;
            t.localPosition = new Vector3(position.x, position.y, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, degrees);
            t.localScale = Vector3.one;
            sr.size = new Vector2(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y));
            sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        private const int LabelFontSize = 110;

        private static TextMesh Label(Transform parent, string name, string text, float height,
            int order, bool bold)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            Font font = bold ? ViewUtil.UiFontBold : ViewUtil.UiFont;
            tm.font = font;
            tm.fontSize = LabelFontSize;
            // Unity draws a TextMesh glyph characterSize * fontSize / 10 tall (see
            // DeckOverlayView.EstimateTextWidth), so this makes `height` mean world units.
            tm.characterSize = height * 10f / LabelFontSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.text = text;
            tm.color = Color.clear;
            var mr = go.GetComponent<MeshRenderer>();
            mr.material = font.material;
            mr.sortingOrder = order;
            return tm;
        }

        private static void PutLabel(TextMesh tm, Vector2 position, float scale, Color colour,
            float alpha)
        {
            tm.transform.localPosition = new Vector3(position.x, position.y, 0f);
            tm.transform.localScale = new Vector3(scale, scale, 1f);
            colour.a *= Mathf.Clamp01(alpha);
            tm.color = colour;
        }

        /// <summary>A label and a soft dark copy under it - the veil is not always dark enough
        /// behind a title for bone-white text to hold its edge.</summary>
        private sealed class Title
        {
            public TextMesh Text;
            public TextMesh Shadow;

            public void Put(Vector2 at, float scale, Color colour, float alpha)
            {
                PutLabel(Shadow, at + new Vector2(0.035f, -0.04f) * scale, scale,
                    new Color(0f, 0f, 0f, 0.75f), alpha);
                PutLabel(Text, at, scale, colour, alpha);
            }

            public void SetText(string s)
            {
                Text.text = s;
                Shadow.text = s;
            }
        }

        private static Title MakeTitle(Transform parent, string name, string text, float height,
            bool bold)
        {
            return new Title
            {
                Shadow = Label(parent, name + "Shadow", text, height, TextShadowOrder, bold),
                Text = Label(parent, name, text, height, TextOrder, bold)
            };
        }

        /// <summary>World width of a string as this label would draw it, from the font's own
        /// advances (a TextMesh only knows its bounds a frame late).</summary>
        private static float TextWidth(TextMesh tm, string s)
        {
            Font font = tm.font;
            font.RequestCharactersInTexture(s, tm.fontSize, FontStyle.Normal);
            float px = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                CharacterInfo info;
                if (font.GetCharacterInfo(s[i], out info, tm.fontSize))
                {
                    px += info.advance;
                }
            }
            return px * tm.characterSize / 10f;
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }

        /// <summary>Boss names are Turkish, so they are upper-cased the Turkish way - "Saatçi"
        /// must become SAATÇİ, not SAATÇI.</summary>
        private static string Upper(string s)
        {
            try
            {
                return s.ToUpper(new System.Globalization.CultureInfo("tr-TR"));
            }
            catch (System.Globalization.CultureNotFoundException)
            {
                return s.ToUpperInvariant();
            }
        }

        // =================================================================== generated sprites
        //
        // All one world unit across (pixels-per-unit = width), white or baked, built once.

        private static Sprite softDot;
        private static Sprite disc;
        private static Sprite hardRing;
        private static Sprite edgeGlow;
        private static Sprite glowBox;
        private static Sprite softRing;
        private static Sprite corona;
        private static Sprite fadeUp;
        private static Sprite[] stripes;

        private static Texture2D NewTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        private static Sprite Finish(Texture2D tex, Color[] px)
        {
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), tex.width);
        }

        /// <summary>Radial white falloff with no rim anywhere: (1 - d^2)^2.</summary>
        private static Sprite SoftDot
        {
            get
            {
                if (softDot == null)
                {
                    softDot = Radial(64, delegate (float r)
                    {
                        float k = Mathf.Clamp01(1f - r * r * 4f);
                        return k * k;
                    });
                }
                return softDot;
            }
        }

        private static Sprite Disc
        {
            get
            {
                if (disc == null)
                {
                    disc = Radial(128, delegate (float r) { return Mathf.Clamp01((0.5f - r) * 128f + 0.5f); });
                }
                return disc;
            }
        }

        /// <summary>A crisp annulus (the padlock's shackle).</summary>
        private static Sprite HardRing
        {
            get
            {
                if (hardRing == null)
                {
                    hardRing = Radial(128, delegate (float r)
                    {
                        float outer = Mathf.Clamp01((0.5f - r) * 128f + 0.5f);
                        float inner = Mathf.Clamp01((r - 0.34f) * 128f + 0.5f);
                        return outer * inner;
                    });
                }
                return hardRing;
            }
        }

        /// <summary>A thin bright ring at radius 0.40 with a wide faint halo round it.</summary>
        private static Sprite SoftRing
        {
            get
            {
                if (softRing == null)
                {
                    softRing = Radial(384, delegate (float r)
                    {
                        float core = Mathf.Exp(-Sq((r - 0.40f) / 0.010f));
                        float halo = 0.38f * Mathf.Exp(-Sq((r - 0.40f) / 0.05f));
                        return Mathf.Clamp01(core + halo);
                    });
                }
                return softRing;
            }
        }

        /// <summary>Solid to radius 0.30, then a long corona falloff to the edge.</summary>
        private static Sprite Corona
        {
            get
            {
                if (corona == null)
                {
                    corona = Radial(256, delegate (float r)
                    {
                        if (r <= 0.30f)
                        {
                            return 1f;
                        }
                        float k = Mathf.Clamp01(1f - (r - 0.30f) / 0.20f);
                        return Mathf.Pow(k, 2.6f);
                    });
                }
                return corona;
            }
        }

        private static Sprite Radial(int n, Func<float, float> alphaAtRadius)
        {
            Texture2D tex = NewTexture(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f;
                    float dy = (y + 0.5f) / n - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * n + x] = new Color(1f, 1f, 1f, alphaAtRadius(r));
                }
            }
            return Finish(tex, px);
        }

        /// <summary>Clear in the middle, rising to the screen's edges and strongest in the corners.</summary>
        private static Sprite EdgeGlow
        {
            get
            {
                if (edgeGlow == null)
                {
                    const int n = 128;
                    Texture2D tex = NewTexture(n, n);
                    var px = new Color[n * n];
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            float u = Mathf.Abs((x + 0.5f) / n * 2f - 1f);
                            float v = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                            float fx = Sq(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 1f, u)));
                            float fy = Sq(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 1f, v)));
                            px[y * n + x] = new Color(1f, 1f, 1f, 1f - (1f - fx) * (1f - fy));
                        }
                    }
                    edgeGlow = Finish(tex, px);
                }
                return edgeGlow;
            }
        }

        /// <summary>A soft glow round a rounded square that fills 0.60 of the sprite - scale the
        /// sprite to boardSize / 0.60 and the square lands on the board's edge.</summary>
        private const float GlowBoxFill = 0.60f;

        private static Sprite GlowBox
        {
            get
            {
                if (glowBox == null)
                {
                    const int n = 256;
                    Texture2D tex = NewTexture(n, n);
                    var px = new Color[n * n];
                    float half = GlowBoxFill * 0.5f;
                    const float radius = 0.04f;
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            float qx = Mathf.Abs((x + 0.5f) / n - 0.5f) - (half - radius);
                            float qy = Mathf.Abs((y + 0.5f) / n - 0.5f) - (half - radius);
                            float d = Mathf.Sqrt(Sq(Mathf.Max(qx, 0f)) + Sq(Mathf.Max(qy, 0f)))
                                + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                            float a = d <= 0f ? 1f : Mathf.Pow(Mathf.Clamp01(1f - d / 0.20f), 2.2f);
                            px[y * n + x] = new Color(1f, 1f, 1f, a);
                        }
                    }
                    glowBox = Finish(tex, px);
                }
                return glowBox;
            }
        }

        /// <summary>A vertical ramp: opaque at the bottom row, clear at the top.</summary>
        private static Sprite FadeUp
        {
            get
            {
                if (fadeUp == null)
                {
                    const int n = 64;
                    Texture2D tex = NewTexture(n, n);
                    var px = new Color[n * n];
                    for (int y = 0; y < n; y++)
                    {
                        float a = Sq(1f - (y + 0.5f) / n);
                        for (int x = 0; x < n; x++)
                        {
                            px[y * n + x] = new Color(1f, 1f, 1f, a);
                        }
                    }
                    fadeUp = Finish(tex, px);
                }
                return fadeUp;
            }
        }

        private const int StripeTexels = 64;

        /// <summary>World size of one hazard tile.</summary>
        private const float StripeTileWorld = 0.36f;

        /// <summary>Hazard stripes in 64 phases, one texel apart. A tiled renderer cannot scroll
        /// its UVs, so marching is done by swapping the tile - the texture is tiny and seamless
        /// in both directions because the stripe period divides the tile. Each red stripe is
        /// lighter through its middle and a touch deeper toward its edges, and carries a faint
        /// wear that repeats with the tile, so the tape reads as a printed material, not a fill.</summary>
        private static Sprite StripeFrame(float time, float texelsPerSecond)
        {
            if (stripes == null)
            {
                stripes = new Sprite[StripeTexels];
                Color red = Style.Threat;
                Color deep = new Color(red.r * 0.72f, red.g * 0.6f, red.b * 0.62f);
                Color ink = new Color(0.055f, 0.035f, 0.042f);
                float halfPeriod = StripeTexels * 0.5f;
                for (int f = 0; f < StripeTexels; f++)
                {
                    Texture2D tex = NewTexture(StripeTexels, StripeTexels);
                    tex.wrapMode = TextureWrapMode.Repeat;
                    var px = new Color[StripeTexels * StripeTexels];
                    for (int y = 0; y < StripeTexels; y++)
                    {
                        for (int x = 0; x < StripeTexels; x++)
                        {
                            float v = Mathf.Repeat(x + y + 1f + f, StripeTexels);
                            float sd = v < halfPeriod
                                ? Mathf.Min(v, halfPeriod - v)
                                : -Mathf.Min(v - halfPeriod, StripeTexels - v);
                            float k = Mathf.Clamp01(0.5f + sd / 1.1f);
                            // Lighter through the middle of a red stripe, deeper at its edges.
                            float centre = Mathf.Clamp01(sd / (halfPeriod * 0.5f));
                            Color stripe = Color.Lerp(deep, red, Mathf.Sqrt(centre));
                            // Wear keyed to the TILE position (x, y), not the phase, so it does
                            // not shimmer as the stripes march.
                            float wear = 0.93f + 0.07f * Hash(x * 64 + y, 7);
                            Color c = Color.Lerp(ink, stripe, k);
                            px[y * StripeTexels + x] = new Color(c.r * wear, c.g * wear, c.b * wear, 1f);
                        }
                    }
                    tex.SetPixels(px);
                    tex.Apply();
                    stripes[f] = Sprite.Create(tex, new Rect(0f, 0f, StripeTexels, StripeTexels),
                        new Vector2(0.5f, 0.5f), StripeTexels / StripeTileWorld, 0,
                        SpriteMeshType.FullRect);
                }
            }
            int index = Mathf.FloorToInt(time * texelsPerSecond) % StripeTexels;
            return stripes[index < 0 ? index + StripeTexels : index];
        }

        private static float Sq(float x)
        {
            return x * x;
        }

        /// <summary>Gives the HUD back only if an intro was holding it dimmed. On a scene teardown
        /// nothing was, and the HUD may already be gone.</summary>
        private void OnDestroy()
        {
            if (run != null)
            {
                run = null;
                SetHud(1f);
            }
        }
    }
}
