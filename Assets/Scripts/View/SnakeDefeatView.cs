// PURPOSE: "Yılan" BEATEN - the boss round won, and the only animation in the game whose subject
// is what the boss TOOK rather than what it is.
//
// THE WHOLE IDEA IS ONE SENTENCE, AND IT IS NOT "the snake dies in colour". The snake spent the
// round eating the player's blocks, and this says those colours were never destroyed: they were
// being HELD. So it does not burst. Its own teal and cream go quiet, the colours of the blocks it
// swallowed wake up UNDER its surface, run to the middle as its body folds in, gather into one
// dense knot - and the knot does not explode either. It UNCOILS: a few soft curved ribbons, each
// one a colour it actually ate this round, opening out and breaking into motes at their ends,
// with a very faint chromatic wave going out across the board behind them. If the result reads as
// "the snake was deleted and some particles came out", the point has been lost.
//
// WHERE THE COLOURS COME FROM. Every bite tells this class what it swallowed (Remember, called
// from SnakeEatView with the palette it derived off the block's own material), so a round in which
// the snake ate gold, purple and blue ends in gold, purple and blue ribbons. That history is
// PRESENTATION ONLY: it changes no score, no snake behaviour and nothing that is saved, it is
// cleared when a round ends, and if the snake was beaten without eating anything at all the
// palette falls back to the snake's own teal / cream / amber with a couple of controlled accents.
// There is no rainbow anywhere: a defeat that always showed six colours would be saying nothing
// about the round it ended.
//
// WHAT THIS OWNS AND WHAT SNAKEVIEW OWNS. The same split as the bite: SnakeView owns the snake, so
// the tension, the pockets under the skin and the non-uniform collapse are ITS sprites driven off
// this timeline (Style, Tension, PocketAt, Collapse); everything after the body is gone - the
// knot, the ribbons, the threads, the motes, the board's wave, the cell reflections and the last
// glint - is here. The board's own cells are never touched: the wave is drawn as its own pooled
// marks over them, so there is no cell state to restore afterwards.
//
// AUDIO. Six moments are announced through Sounded for sound to be hung on later.
using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectBlock.Core;

namespace ProjectBlock.View
{
    /// <summary>The snake beaten: the colours it swallowed let go of at last.</summary>
    public sealed class SnakeDefeatView : MonoBehaviour
    {
        // =================================================================== the knobs

        public static class Style
        {
            // ---- 1. the last piece tightens ----

            public static float FinalTensionDuration = 0.1f;

            /// <summary>How much it gathers, across the body. Two or three percent: something
            /// under pressure, not a balloon.</summary>
            public static float FinalTensionStrength = 0.035f;

            // ---- 2. the colours wake up underneath ----

            public static float ColorPocketStart = 0.07f;

            public static float ColorPocketDuration = 0.15f;

            /// <summary>How strongly a pocket shows through. Low: the snake's own artwork has to
            /// stay readable, or the colour is a sticker rather than something inside it.</summary>
            public static float ColorPocketStrength = 0.8f;

            public static float ColorPocketSoftness = 0.45f;

            public static int ColorPocketCount = 3;

            // ---- 3. and run to the middle ----

            public static float ColorFlowStart = 0.16f;

            public static float ColorFlowDuration = 0.18f;

            public static float ColorFlowSpeed = 1.1f;

            // ---- 4. the body folds in ----

            public static float CollapseStart = 0.25f;

            public static float CollapseDuration = 0.2f;

            /// <summary>How UNEVEN it is: the ends go before the middle. A uniform scale to zero
            /// is the thing this replaced.</summary>
            public static float CollapseAsymmetry = 0.55f;

            /// <summary>How far the snake's own teal and cream go quiet. Not to grey - just enough
            /// to make room for what is coming up underneath.</summary>
            public static float BaseColorDrain = 0.6f;

            public static float ShadowCollapseDuration = 0.14f;

            // ---- 5. the knot ----

            public static float KnotStart = 0.36f;

            public static float KnotSize = 0.34f;

            public static float KnotIntensity = 0.85f;

            /// <summary>The breath it holds before it opens. The peak of the whole animation is
            /// the moment after this.</summary>
            public static float KnotHold = 0.06f;

            // ---- 6. it uncoils ----

            public static float RibbonStart = 0.44f;

            public static int RibbonCountMin = 3;

            public static int RibbonCountMax = 6;

            public static float RibbonLengthMin = 1f;

            public static float RibbonLengthMax = 2.2f;

            public static float RibbonWidth = 0.17f;

            /// <summary>How far each one bows off its own straight line. Enough that no two read
            /// as the same arc; never a spiral.</summary>
            public static float RibbonCurveStrength = 0.62f;

            public static float RibbonDuration = 0.24f;

            /// <summary>The one-way brightening that runs outward along a ribbon as it opens.</summary>
            public static float RibbonFlowStrength = 0.5f;

            public static int SecondaryThreadCount = 7;

            public static float SecondaryThreadWidth = 0.042f;

            // ---- 7. their ends come apart ----

            public static int MoteCount = 18;

            public static float MoteSpeed = 1.2f;

            public static float MoteLifetime = 0.42f;

            public static float MoteDrag = 3.4f;

            public static float MoteSize = 0.07f;

            // ---- 8. and the board answers, faintly ----

            public static float BoardWaveStart = 0.52f;

            public static float BoardWaveRadius = 3f;

            public static float BoardWaveDuration = 0.36f;

            /// <summary>How long the wave takes to reach each further ring of cells.</summary>
            public static float BoardWaveCellDelay = 0.045f;

            public static float BoardWaveStrength = 0.055f;

            public static float CellReflectionStrength = 0.05f;

            // ---- 9. and it is over ----

            public static float FinalGlintStart = 0.86f;

            public static float FinalGlintStrength = 0.4f;

            public static float FinalGlintDuration = 0.07f;

            /// <summary>How many colours the palette may carry. Twenty blocks do not make twenty
            /// ribbons: near colours are one family.</summary>
            public static int MaxPaletteColors = 5;

            /// <summary>The whole thing, end to end - derived so no phase can outlive it.</summary>
            public static float Total
            {
                get
                {
                    return Mathf.Max(FinalGlintStart + FinalGlintDuration,
                        Mathf.Max(RibbonStart + RibbonDuration + MoteLifetime,
                            BoardWaveStart + BoardWaveDuration));
                }
            }
        }

        // =================================================================== what can be switched off

        public static class Layers
        {
            public static bool ShowColorPockets = true;

            public static bool ShowColorFlow = true;

            public static bool ShowCollapse = true;

            public static bool ShowKnot = true;

            public static bool ShowRibbons = true;

            public static bool ShowThreads = true;

            public static bool ShowMotes = true;

            public static bool ShowBoardWave = true;

            public static bool ShowCellReflections = true;

            public static bool ShowFinalGlint = true;

            public static void Defaults()
            {
                ShowColorPockets = true;
                ShowColorFlow = true;
                ShowCollapse = true;
                ShowKnot = true;
                ShowRibbons = true;
                ShowThreads = true;
                ShowMotes = true;
                ShowBoardWave = true;
                ShowCellReflections = true;
                ShowFinalGlint = true;
            }
        }

        // =================================================================== the moments audio wants

        public enum Beat
        {
            Tension,
            ColorsAwaken,
            ColorKnot,
            RibbonRelease,
            BoardWave,
            Complete
        }

        public event Action<Beat, Vector2> Sounded;

        // =================================================================== the colours it swallowed
        //
        // PRESENTATION ONLY. Nothing here is game state: it scores nothing, it changes no
        // behaviour, it is not saved, and it is dropped when the round ends. It exists so that a
        // round in which the snake ate gold, purple and blue can END in gold, purple and blue.

        /// <summary>One block's colours, as the bite derived them off its own material.</summary>
        public struct Swallowed
        {
            public Color Material;
            public Color Energy;
            public Color Hot;
        }

        private static readonly List<Swallowed> history = new List<Swallowed>();

        /// <summary>The bite telling us what went down. Called from SnakeEatView.</summary>
        public static void Remember(Color material, Color energy, Color hot)
        {
            if (history.Count > 64)
            {
                history.RemoveAt(0);   // a very long round cannot grow this without bound
            }
            history.Add(new Swallowed { Material = material, Energy = energy, Hot = hot });
        }

        /// <summary>A new round: the snake has eaten nothing yet.</summary>
        public static void ForgetHistory()
        {
            history.Clear();
        }

        public static int RememberedCount
        {
            get { return history.Count; }
        }

        /// <summary>What the lab hands in when it wants to see a particular palette.</summary>
        public static void OverrideHistory(IReadOnlyList<Color> colours)
        {
            history.Clear();
            if (colours == null)
            {
                return;
            }
            for (int i = 0; i < colours.Count; i++)
            {
                Color material;
                Color energy;
                Color hot;
                Levels(colours[i], out material, out energy, out hot);
                history.Add(new Swallowed { Material = material, Energy = energy, Hot = hot });
            }
        }

        /// <summary>The three levels of one colour - the same derivation the bite uses, so a
        /// palette handed in by hand behaves exactly like one that was eaten.</summary>
        public static void Levels(Color c, out Color material, out Color energy, out Color hot)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            if (max < 0.34f)
            {
                float lift = 0.34f / Mathf.Max(max, 0.04f);
                c = new Color(c.r * lift, c.g * lift, c.b * lift, 1f);
                max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            }
            material = new Color(c.r, c.g, c.b, 1f);
            float mid = (max + min) * 0.5f;
            energy = new Color(Mathf.Clamp01(mid + (c.r - mid) * 1.45f),
                Mathf.Clamp01(mid + (c.g - mid) * 1.45f),
                Mathf.Clamp01(mid + (c.b - mid) * 1.45f), 1f);
            hot = new Color(Mathf.Lerp(energy.r, 1f, 0.3f), Mathf.Lerp(energy.g, 1f, 0.3f),
                Mathf.Lerp(energy.b, 1f, 0.3f), 1f);
        }

        /// <summary>
        /// THE ROUND'S PALETTE: what it ate, grouped into families and put in order of how much of
        /// each went down, so the colour it took most of leads. Near colours are ONE family - two
        /// blues are blue, not two ribbons - which is what keeps twenty blocks from ending in
        /// twenty streaks, and what makes the result specific to the round instead of a rainbow.
        /// With nothing eaten it falls back to the snake's own colours plus two accents.
        /// </summary>
        public static void Palette(List<Color> into, List<float> weights)
        {
            into.Clear();
            if (weights != null)
            {
                weights.Clear();
            }
            var counts = new List<float>();
            for (int i = 0; i < history.Count; i++)
            {
                Color c = history[i].Energy;
                int found = -1;
                for (int j = 0; j < into.Count; j++)
                {
                    if (Near(into[j], c))
                    {
                        found = j;
                        break;
                    }
                }
                if (found >= 0)
                {
                    // The family keeps its own average, so a family of two blues is that blue.
                    float n = counts[found];
                    into[found] = Color.Lerp(into[found], c, 1f / (n + 1f));
                    counts[found] = n + 1f;
                }
                else
                {
                    into.Add(c);
                    counts.Add(1f);
                }
            }
            // Most-eaten first: that one gets the hero ribbon.
            for (int i = 1; i < into.Count; i++)
            {
                for (int j = i; j > 0 && counts[j] > counts[j - 1]; j--)
                {
                    Color c = into[j];
                    into[j] = into[j - 1];
                    into[j - 1] = c;
                    float n = counts[j];
                    counts[j] = counts[j - 1];
                    counts[j - 1] = n;
                }
            }
            while (into.Count > Mathf.Max(Style.MaxPaletteColors, 1))
            {
                into.RemoveAt(into.Count - 1);
                counts.RemoveAt(counts.Count - 1);
            }
            if (into.Count == 0)
            {
                // IT ATE NOTHING. The snake's own colours, and two accents off the game's own
                // palette - controlled, never random RGB.
                into.Add(new Color(0.24f, 0.78f, 0.62f));     // its teal
                into.Add(new Color(0.95f, 0.89f, 0.74f));     // its cream
                into.Add(new Color(1f, 0.8f, 0.25f));         // amber, the game's gold
                into.Add(new Color(0.35f, 0.6f, 1f));         // and its blue
                counts.Clear();
                for (int i = 0; i < into.Count; i++)
                {
                    counts.Add(1f);
                }
            }
            if (weights != null)
            {
                float top = 1f;
                for (int i = 0; i < counts.Count; i++)
                {
                    top = Mathf.Max(top, counts[i]);
                }
                for (int i = 0; i < counts.Count; i++)
                {
                    weights.Add(counts[i] / top);
                }
            }
        }

        /// <summary>Two colours that belong to the same family: close in hue, and neither one so
        /// grey that its hue means anything.</summary>
        private static bool Near(Color a, Color b)
        {
            float ha;
            float hb;
            float sa;
            float sb;
            float v;
            Color.RGBToHSV(a, out ha, out sa, out v);
            Color.RGBToHSV(b, out hb, out sb, out v);
            if (sa < 0.18f && sb < 0.18f)
            {
                return true;    // both effectively colourless
            }
            float d = Mathf.Abs(ha - hb);
            d = Mathf.Min(d, 1f - d);
            return d < 0.075f;
        }

        // =================================================================== state

        private const int StripPoints = 20;

        private const int KnotOrder = 7;

        private const int RibbonOrder = 6;

        private const int MoteOrder = 8;

        private const int BoardOrder = 2;

        private sealed class Strip
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
        }

        private sealed class Ribbon
        {
            public Strip Strip;
            public Vector2 Out;        // the direction it opens along
            public Vector2 Bow;        // and the control point that makes it an arc
            public float Length;
            public float Width;
            public float Born;
            public Color Colour;
            public Color Core;
            public bool Hero;
        }

        private sealed class Mote
        {
            public SpriteRenderer Renderer;
            public Vector2 At;
            public Vector2 Velocity;
            public float Born;
            public float Life;
            public float Size;
            public int Shape;          // 0 round, 1 fleck, 2 sliver
            public Color Colour;
        }

        private sealed class Ring
        {
            public SpriteRenderer Renderer;
            public Vector2 At;
            public float Delay;
            public Color Colour;
        }

        private static Sprite ringSprite;
        private static Material filamentMaterial;
        private static bool materialLooked;
        private static Material plainMaterial;

        private BoardView view;
        private bool playing;
        private float clock;
        private float cellSize;
        private uint seed;
        private Vector2 centre;

        private readonly List<Color> palette = new List<Color>();
        private readonly List<float> weights = new List<float>();
        private readonly List<Ribbon> ribbons = new List<Ribbon>();
        private readonly List<Mote> motes = new List<Mote>();
        private readonly List<Ring> rings = new List<Ring>();
        private readonly Stack<Strip> spareStrips = new Stack<Strip>();
        private readonly Stack<SpriteRenderer> spareSprites = new Stack<SpriteRenderer>();
        private readonly List<Vector3> stripVertices = new List<Vector3>(StripPoints * 2);
        private readonly List<Color> stripColours = new List<Color>(StripPoints * 2);
        private SpriteRenderer knot;
        private SpriteRenderer glint;
        private bool releasedRibbons;
        private bool sowedMotes;
        private bool sowedRings;
        private bool saidTension;
        private bool saidAwaken;
        private bool saidKnot;
        private bool saidRibbons;
        private bool saidWave;
        private bool saidDone;

        public bool Playing
        {
            get { return playing; }
        }

        /// <summary>The palette this defeat is running on, for SnakeView's pockets and the lab's
        /// readout. Index 0 is the colour the snake ate most of.</summary>
        public IReadOnlyList<Color> Colours
        {
            get { return palette; }
        }

        // =================================================================== the timeline

        private static float Window(float t, float start, float length)
        {
            return Mathf.Clamp01((t - start) / Mathf.Max(length, 1e-4f));
        }

        private static float Smooth(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        /// <summary>How tight the last piece is - the pressure before anything gives.</summary>
        public static float Tension(float t)
        {
            return Style.FinalTensionStrength
                * Mathf.Sin(Mathf.PI * Window(t, 0f, Style.FinalTensionDuration));
        }

        /// <summary>How much of the snake's own colour has gone quiet.</summary>
        public static float Drain(float t)
        {
            return Style.BaseColorDrain
                * Smooth(Window(t, Style.ColorPocketStart, Style.ColorPocketDuration));
        }

        /// <summary>How strongly the swallowed colours show through, and how far they have run
        /// toward the middle (0 at their own place, 1 gathered).</summary>
        public static void Pockets(float t, out float strength, out float gathered)
        {
            strength = Style.ColorPocketStrength
                * Smooth(Window(t, Style.ColorPocketStart, Style.ColorPocketDuration));
            gathered = Smooth(Window(t, Style.ColorFlowStart, Style.ColorFlowDuration)
                * Style.ColorFlowSpeed);
            // And they go out with the body, so nothing is left glowing on an empty cell.
            strength *= 1f - Smooth(Window(t, Style.CollapseStart + Style.CollapseDuration * 0.6f,
                Style.CollapseDuration * 0.4f));
        }

        /// <summary>How far a segment has folded in, 0 whole and 1 gone. UNEVEN along the body:
        /// the ends go first and the middle follows, because a uniform scale to zero is what this
        /// whole animation exists instead of.</summary>
        public static float Collapse(float t, int index, int count)
        {
            // ONE easing, not two. Smoothing the phase and then smoothing each segment's own
            // share of it again made the fold a switch: nothing for a third of it, then gone
            // between two frames.
            float k = Window(t, Style.CollapseStart, Style.CollapseDuration);
            if (count <= 1)
            {
                return Smooth(k);
            }
            // 0 at the middle of the body, 1 at either end. The ends START earlier; every
            // segment still FINISHES by the end of the phase. Scaling the speed instead (which is
            // what this did first) left the ends gone a third of the way in and the middle never
            // reaching zero at all - a lone teal segment sitting on an empty board while the
            // ribbons were already fading.
            float fromMiddle = Mathf.Abs(index - (count - 1) * 0.5f) / ((count - 1) * 0.5f);
            float lead = Style.CollapseAsymmetry * 0.5f;
            float start = (1f - fromMiddle) * lead;
            return Smooth(Mathf.Clamp01((k - start) / Mathf.Max(1f - start, 1e-3f)));
        }

        public static float ShadowLeft(float t)
        {
            return 1f - Smooth(Window(t, Style.CollapseStart, Style.ShadowCollapseDuration));
        }

        // =================================================================== beginning and ending

        public void Begin(BoardView owner, Vector2 finalCentre, uint dice)
        {
            Stop();
            view = owner;
            if (view == null)
            {
                return;
            }
            transform.SetParent(view.transform, false);
            cellSize = view.CellWorldSize;
            centre = finalCentre;
            seed = dice == 0u ? 7u : dice;
            Palette(palette, weights);
            clock = 0f;
            releasedRibbons = sowedMotes = sowedRings = false;
            saidTension = saidAwaken = saidKnot = saidRibbons = saidWave = saidDone = false;
            playing = true;
        }

        public void Stop()
        {
            playing = false;
            clock = 0f;
            for (int i = 0; i < ribbons.Count; i++)
            {
                ReturnStrip(ribbons[i].Strip);
            }
            ribbons.Clear();
            for (int i = 0; i < motes.Count; i++)
            {
                Return(motes[i].Renderer);
            }
            motes.Clear();
            for (int i = 0; i < rings.Count; i++)
            {
                Return(rings[i].Renderer);
            }
            rings.Clear();
            Return(knot);
            knot = null;
            Return(glint);
            glint = null;
        }

        // =================================================================== the frame

        public void Paint(float dt)
        {
            if (!playing || view == null)
            {
                return;
            }
            clock += dt;
            cellSize = view.CellWorldSize;
            Announce();
            PaintKnot();
            Release();
            PaintRibbons(dt);
            PaintMotes(dt);
            PaintWave();
            PaintGlint();
            if (clock >= Style.Total)
            {
                Stop();
            }
        }

        private void Announce()
        {
            Say(ref saidTension, clock >= 0f, Beat.Tension);
            Say(ref saidAwaken, clock >= Style.ColorPocketStart, Beat.ColorsAwaken);
            Say(ref saidKnot, clock >= Style.KnotStart, Beat.ColorKnot);
            Say(ref saidRibbons, clock >= Style.RibbonStart, Beat.RibbonRelease);
            Say(ref saidWave, clock >= Style.BoardWaveStart, Beat.BoardWave);
            Say(ref saidDone, clock >= Style.Total * 0.98f, Beat.Complete);
        }

        private void Say(ref bool already, bool now, Beat beat)
        {
            if (already || !now)
            {
                return;
            }
            already = true;
            if (Sounded != null)
            {
                Sounded(beat, centre);
            }
        }

        // =================================================================== the knot

        /// <summary>Not an orb. A few short strands of the colours wound into each other and
        /// squeezed - so it reads as the snake's held colour compressed, not as a magic ball. It
        /// gets DENSER as the body gets smaller: the colour is conserved even though the mass is
        /// going.</summary>
        private void PaintKnot()
        {
            float k = Window(clock, Style.KnotStart, Mathf.Max(Style.KnotHold, 1e-3f));
            float out1 = Smooth(Window(clock, Style.RibbonStart, Style.RibbonDuration * 0.4f));
            float show = Layers.ShowKnot ? Style.KnotIntensity * Smooth(k) * (1f - out1) : 0f;
            if (show <= 0.002f)
            {
                Return(knot);
                knot = null;
                return;
            }
            if (knot == null)
            {
                knot = Rent(SnakeVfxController.SoftDot(), KnotOrder);
            }
            // Squeezed as it forms, and the colour it shows is the round's dominant one with the
            // others still visible around it - drawn by the strands below, not by one flat dot.
            float r = Style.KnotSize * cellSize * Mathf.Lerp(1.35f, 0.85f, Smooth(k));
            Place(knot, centre, r, r * 0.92f, Tint(palette[0], show * 0.75f), KnotOrder);
            var dice = new Dice(seed ^ 0x9e3779b9u);
            int strands = Mathf.Min(palette.Count, 4);
            for (int i = 0; i < strands; i++)
            {
                float a = i / (float)strands * Mathf.PI * 2f + Smooth(k) * 1.1f;
                Vector2 at = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f)
                    * (r * 0.42f * (1f - Smooth(k) * 0.45f));
                Vector2 wob = new Vector2(dice.Range(-0.06f, 0.06f), dice.Range(-0.06f, 0.06f));
                SpriteRenderer s = Rent(SnakeVfxController.SoftDot(), KnotOrder);
                Place(s, at + wob * cellSize, r * 0.6f, r * 0.44f,
                    Tint(palette[i], show * 0.8f), KnotOrder);
                // One frame's use: they are re-rented every frame, which keeps the knot's shape
                // free to change without a pool of its own.
                spareSprites.Push(s);
            }
        }

        // =================================================================== the uncoiling

        /// <summary>The peak of the animation. The knot does not burst: each colour the snake ate
        /// leaves along its OWN arc, in its own direction, at its own length.</summary>
        private void Release()
        {
            if (releasedRibbons || clock < Style.RibbonStart || !Layers.ShowRibbons)
            {
                return;
            }
            releasedRibbons = true;
            var dice = new Dice(seed ^ 0x85ebca6bu);
            int want = Mathf.Clamp(palette.Count, Style.RibbonCountMin, Style.RibbonCountMax);
            // Spread over the circle, then pushed off it a little, so they are not a fan.
            float spin = dice.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < want; i++)
            {
                Color c = palette[i % palette.Count];
                float weight = i < weights.Count ? weights[i] : 0.6f;
                float a = spin + i / (float)want * Mathf.PI * 2f + dice.Range(-0.35f, 0.35f);
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.82f).normalized;
                float length = Mathf.Lerp(Style.RibbonLengthMin, Style.RibbonLengthMax,
                    Mathf.Clamp01(weight * dice.Range(0.7f, 1.15f))) * cellSize;
                Vector2 side = new Vector2(-dir.y, dir.x);
                Add(dir, side * (dice.Range(-1f, 1f) * Style.RibbonCurveStrength * cellSize),
                    length, Style.RibbonWidth * cellSize * Mathf.Lerp(0.8f, 1.25f, weight),
                    c, i == 0, dice);
            }
            if (Layers.ShowThreads)
            {
                for (int i = 0; i < Style.SecondaryThreadCount; i++)
                {
                    Color c = palette[dice.Next() < 0.5f ? 0 : (i + 1) % palette.Count];
                    float a = dice.Range(0f, Mathf.PI * 2f);
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.82f).normalized;
                    Vector2 side = new Vector2(-dir.y, dir.x);
                    Add(dir, side * (dice.Range(-1f, 1f) * Style.RibbonCurveStrength * 0.7f
                        * cellSize), Style.RibbonLengthMin * cellSize * dice.Range(0.55f, 0.9f),
                        Style.SecondaryThreadWidth * cellSize, c, false, dice);
                }
            }
        }

        private void Add(Vector2 dir, Vector2 bow, float length, float width, Color c, bool hero,
            Dice dice)
        {
            Strip strip = RentStrip(RibbonOrder);
            if (strip == null)
            {
                return;
            }
            Color material;
            Color energy;
            Color hot;
            Levels(c, out material, out energy, out hot);
            ribbons.Add(new Ribbon
            {
                Strip = strip,
                Out = dir,
                Bow = bow,
                Length = length,
                Width = width * (hero ? 1.25f : 1f),
                Born = clock + dice.Range(0f, 0.03f),
                Colour = energy,
                Core = hot,
                Hero = hero
            });
        }

        private void PaintRibbons(float dt)
        {
            for (int i = ribbons.Count - 1; i >= 0; i--)
            {
                Ribbon r = ribbons[i];
                float k = (clock - r.Born) / Mathf.Max(Style.RibbonDuration, 1e-3f);
                if (k >= 1f)
                {
                    ReturnStrip(r.Strip);
                    ribbons.RemoveAt(i);
                    continue;
                }
                if (k <= 0f)
                {
                    continue;
                }
                Build(r, k);
            }
        }

        /// <summary>One ribbon: a quadratic arc out of the knot, medium at its root, a touch wider
        /// just past it, thinner along the way and nothing at its tip. The brightening runs
        /// OUTWARD along it once - it does not loop, because a loop is a scrolling texture with
        /// extra steps.</summary>
        private void Build(Ribbon r, float k)
        {
            stripVertices.Clear();
            stripColours.Clear();
            float opened = Smooth(k);
            Vector2 tip = centre + r.Out * (r.Length * opened);
            Vector2 bow = centre + r.Out * (r.Length * opened * 0.5f) + r.Bow * opened;
            float head = opened;                           // where the light has got to
            for (int p = 0; p < StripPoints; p++)
            {
                float s = p / (float)(StripPoints - 1);
                Vector2 at = Bezier(centre, bow, tip, s);
                Vector2 next = Bezier(centre, bow, tip, Mathf.Min(s + 0.02f, 1f));
                Vector2 step = next - at;
                Vector2 side = step.sqrMagnitude > 1e-9f
                    ? new Vector2(-step.y, step.x).normalized : new Vector2(0f, 1f);
                // medium at the root, widest a fifth along, then away to nothing
                float w = r.Width * (s < 0.2f ? Mathf.Lerp(0.75f, 1.15f, s / 0.2f)
                    : Mathf.Lerp(1.15f, 0f, Mathf.Pow((s - 0.2f) / 0.8f, 0.85f)));
                stripVertices.Add(at - side * (w * 0.5f));
                stripVertices.Add(at + side * (w * 0.5f));
                Color c = Color.Lerp(r.Colour, r.Core,
                    Style.RibbonFlowStrength * (1f - Mathf.Clamp01(Mathf.Abs(s - head) / 0.3f)));
                float fade = Smooth(Mathf.Clamp01(k / 0.16f))
                    * (1f - Smooth(Mathf.Clamp01((k - 0.62f) / 0.38f)));
                c.a = fade * (r.Hero ? 1f : 0.82f) * Mathf.Lerp(1f, 0.35f, s);
                stripColours.Add(c);
                stripColours.Add(c);
            }
            Mesh mesh = r.Strip.Mesh;
            mesh.SetVertices(stripVertices);
            mesh.SetColors(stripColours);
            mesh.RecalculateBounds();
            r.Strip.Renderer.enabled = true;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float s)
        {
            float u = 1f - s;
            return u * u * a + 2f * u * s * b + s * s * c;
        }

        // =================================================================== their ends coming apart

        private void PaintMotes(float dt)
        {
            if (!sowedMotes && Layers.ShowMotes
                && clock >= Style.RibbonStart + Style.RibbonDuration * 0.45f)
            {
                sowedMotes = true;
                var dice = new Dice(seed ^ 0xc2b2ae35u);
                for (int i = 0; i < Style.MoteCount; i++)
                {
                    // FROM THE RIBBONS' ENDS, not out of the middle: they are what a ribbon comes
                    // apart into, so they inherit where it had got to and how fast it was going.
                    Ribbon from = ribbons.Count > 0
                        ? ribbons[(int)(dice.Next() * ribbons.Count) % ribbons.Count] : null;
                    Vector2 dir = from != null ? from.Out
                        : new Vector2(dice.Range(-1f, 1f), dice.Range(-1f, 1f)).normalized;
                    float along = from != null ? from.Length * dice.Range(0.72f, 1f)
                        : cellSize * dice.Range(0.6f, 1.4f);
                    Color c = from != null ? from.Colour : palette[i % palette.Count];
                    motes.Add(new Mote
                    {
                        Renderer = Rent(SnakeVfxController.SoftDot(), MoteOrder),
                        At = centre + dir * along
                            + new Vector2(dice.Range(-0.1f, 0.1f), dice.Range(-0.1f, 0.1f))
                                * cellSize,
                        Velocity = dir * (Style.MoteSpeed * cellSize * dice.Range(0.6f, 1.2f)),
                        Born = clock,
                        Life = Style.MoteLifetime * dice.Range(0.7f, 1.35f),
                        Size = Style.MoteSize * cellSize * dice.Range(0.6f, 1.2f),
                        Shape = (int)(dice.Next() * 3f) % 3,
                        Colour = c
                    });
                }
            }
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                Mote m = motes[i];
                float k = (clock - m.Born) / Mathf.Max(m.Life, 1e-3f);
                if (k >= 1f)
                {
                    Return(m.Renderer);
                    motes.RemoveAt(i);
                    continue;
                }
                // It carries the ribbon's speed, then gives it up and drifts.
                m.Velocity *= 1f / (1f + Style.MoteDrag * dt);
                m.At += m.Velocity * dt;
                float show = Smooth(Mathf.Clamp01(k / 0.12f))
                    * (1f - Smooth(Mathf.Clamp01((k - 0.45f) / 0.55f)));
                // three families, and not one of them a square
                float w = m.Size * (m.Shape == 2 ? 1.7f : 1f);
                float h = m.Size * (m.Shape == 1 ? 0.62f : m.Shape == 2 ? 0.34f : 1f);
                Place(m.Renderer, m.At, w, h, Tint(m.Colour, show), MoteOrder);
            }
        }

        // =================================================================== the board's answer

        /// <summary>Very faint, and drawn as marks of OUR OWN over the cells - the board's own
        /// cells are never touched, so there is no state to put back and no way for this to leave
        /// anything behind. It is punctuation, not the event.</summary>
        private void PaintWave()
        {
            if (!sowedRings && Layers.ShowBoardWave && clock >= Style.BoardWaveStart)
            {
                sowedRings = true;
                GameBoard board = view.Board;
                if (board != null)
                {
                    var dice = new Dice(seed ^ 0x27d4eb2fu);
                    int radius = Mathf.Max(Mathf.RoundToInt(Style.BoardWaveRadius), 1);
                    for (int x = 0; x < board.Width; x++)
                    {
                        for (int y = 0; y < board.Height; y++)
                        {
                            var cell = new GridPos(board.MinX + x, board.MinY + y);
                            Vector2 at = view.CellToWorld(cell);
                            float d = (at - centre).magnitude / Mathf.Max(cellSize, 1e-4f);
                            if (d > radius)
                            {
                                continue;
                            }
                            // MOSTLY THE ROUND'S DOMINANT COLOUR. Deterministic, and only
                            // every fourth cell takes one of the others: a different colour in
                            // every cell is a rainbow board, which is exactly what this may not
                            // turn into.
                            int pick = Mathf.Abs(cell.X * 7 + cell.Y * 13);
                            rings.Add(new Ring
                            {
                                Renderer = Rent(CellRim(), BoardOrder),
                                At = at,
                                Delay = d * Style.BoardWaveCellDelay,
                                Colour = pick % 4 == 0
                                    ? palette[pick % palette.Count] : palette[0]
                            });
                            dice.Next();
                        }
                    }
                }
            }
            for (int i = 0; i < rings.Count; i++)
            {
                Ring r = rings[i];
                float k = Window(clock, Style.BoardWaveStart + r.Delay,
                    Style.BoardWaveDuration * 0.45f);
                float show = Mathf.Sin(Mathf.PI * k) * Style.BoardWaveStrength;
                if (Layers.ShowCellReflections)
                {
                    show = Mathf.Max(show, Mathf.Sin(Mathf.PI * k) * Style.CellReflectionStrength);
                }
                // The cell's EDGE catching the colour for a moment - not the cell filling
                // with it. A filled cell is a tinted board; a rim is a reflection.
                Place(r.Renderer, r.At, cellSize * 0.96f, cellSize * 0.96f,
                    Tint(r.Colour, show), BoardOrder);
            }
        }

        private void PaintGlint()
        {
            float k = Window(clock, Style.FinalGlintStart, Style.FinalGlintDuration);
            float show = Layers.ShowFinalGlint
                ? Style.FinalGlintStrength * Mathf.Sin(Mathf.PI * k) : 0f;
            if (show <= 0.002f)
            {
                Return(glint);
                glint = null;
                return;
            }
            if (glint == null)
            {
                glint = Rent(SnakeVfxController.SoftDot(), KnotOrder);
            }
            Place(glint, centre, cellSize * 0.34f, cellSize * 0.28f,
                Tint(new Color(0.98f, 0.93f, 0.8f), show), KnotOrder);
        }

        // =================================================================== pools and plumbing

        /// <summary>A thin rounded ring: a cell's edge catching a colour. Generated once and
        /// cached, never per frame.</summary>
        private static Sprite CellRim()
        {
            if (ringSprite != null)
            {
                return ringSprite;
            }
            const int N = 64;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / N - 0.5f) * 2f;
                    float v = Mathf.Abs((y + 0.5f) / N - 0.5f) * 2f;
                    // A rounded square's outline, which is the shape the board's own cells are.
                    float d = Mathf.Max(u, v);
                    float corner = Mathf.Sqrt(Mathf.Max(u - 0.72f, 0f) * Mathf.Max(u - 0.72f, 0f)
                        + Mathf.Max(v - 0.72f, 0f) * Mathf.Max(v - 0.72f, 0f));
                    float edge = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.12f);
                    edge *= Mathf.Clamp01(1f - corner / 0.34f);
                    px[y * N + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(edge * edge * 255f));
                }
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels32(px);
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f), N);
            ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return ringSprite;
        }

        private static Material FilamentMaterial()
        {
            if (!materialLooked)
            {
                materialLooked = true;
                Shader shader = Shader.Find("ProjectBlock/SnakeFilament");
                if (shader == null)
                {
                    shader = Resources.Load<Shader>("Shaders/SnakeFilament");
                }
                if (shader != null && shader.isSupported)
                {
                    filamentMaterial = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }
            return filamentMaterial;
        }

        private SpriteRenderer Rent(Sprite sprite, int order)
        {
            SpriteRenderer r;
            if (spareSprites.Count > 0)
            {
                r = spareSprites.Pop();
            }
            else
            {
                var go = new GameObject("Defeat");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plainMaterial == null)
                {
                    // NEVER null: a SpriteRenderer with no material is drawn with Unity's MAGENTA
                    // error material, which ignores the tint - so every colour in the effect would
                    // come out flat pink whatever the round's palette was.
                    plainMaterial = r.sharedMaterial;
                }
            }
            r.sprite = sprite;
            if (plainMaterial != null && r.sharedMaterial != plainMaterial)
            {
                r.sharedMaterial = plainMaterial;
            }
            r.sortingOrder = order;
            r.enabled = true;
            r.color = Color.clear;
            r.transform.localRotation = Quaternion.identity;
            return r;
        }

        private void Return(SpriteRenderer r)
        {
            if (r == null)
            {
                return;
            }
            r.color = Color.clear;
            r.sprite = null;
            r.enabled = false;
            spareSprites.Push(r);
        }

        private Strip RentStrip(int order)
        {
            Material mat = FilamentMaterial();
            if (mat == null)
            {
                return null;
            }
            Strip s;
            if (spareStrips.Count > 0)
            {
                s = spareStrips.Pop();
            }
            else
            {
                var go = new GameObject("DefeatRibbon");
                go.transform.SetParent(transform, false);
                var filter = go.AddComponent<MeshFilter>();
                s = new Strip { Renderer = go.AddComponent<MeshRenderer>(), Mesh = new Mesh() };
                s.Mesh.hideFlags = HideFlags.HideAndDontSave;
                s.Mesh.MarkDynamic();
                var vertices = new Vector3[StripPoints * 2];
                var uvs = new Vector2[StripPoints * 2];
                var triangles = new int[(StripPoints - 1) * 6];
                for (int p = 0; p < StripPoints; p++)
                {
                    float along = p / (float)(StripPoints - 1);
                    uvs[p * 2] = new Vector2(0f, along);
                    uvs[p * 2 + 1] = new Vector2(1f, along);
                }
                for (int p = 0; p < StripPoints - 1; p++)
                {
                    int tri = p * 6;
                    int v = p * 2;
                    triangles[tri] = v;
                    triangles[tri + 1] = v + 2;
                    triangles[tri + 2] = v + 1;
                    triangles[tri + 3] = v + 1;
                    triangles[tri + 4] = v + 2;
                    triangles[tri + 5] = v + 3;
                }
                s.Mesh.vertices = vertices;
                s.Mesh.uv = uvs;
                s.Mesh.colors = new Color[StripPoints * 2];
                s.Mesh.triangles = triangles;
                filter.sharedMesh = s.Mesh;
                s.Renderer.sharedMaterial = mat;
                s.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.Renderer.receiveShadows = false;
            }
            s.Renderer.sortingOrder = order;
            s.Renderer.enabled = false;
            return s;
        }

        private void ReturnStrip(Strip s)
        {
            if (s == null)
            {
                return;
            }
            s.Renderer.enabled = false;
            spareStrips.Push(s);
        }

        private void Place(SpriteRenderer r, Vector2 at, float width, float height, Color tint,
            int order)
        {
            if (r == null || r.sprite == null)
            {
                return;
            }
            r.sortingOrder = order;
            r.color = tint;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            Vector2 size = r.sprite.bounds.size;
            r.transform.localScale = new Vector3(width / Mathf.Max(size.x, 1e-4f),
                height / Mathf.Max(size.y, 1e-4f), 1f);
        }

        private static Color Tint(Color c, float alpha)
        {
            c.a = Mathf.Clamp01(alpha);
            return c;
        }

        private struct Dice
        {
            private uint state;

            public Dice(uint s)
            {
                state = s == 0u ? 1u : s;
            }

            public float Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state & 0xFFFFFFu) / (float)0x1000000u;
            }

            public float Range(float a, float b)
            {
                return a + (b - a) * Next();
            }
        }
    }
}
