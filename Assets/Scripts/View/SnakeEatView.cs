// PURPOSE: "Yılan" EATING A BLOCK - the boss's hero moment, and the one place in the game where
// something is taken apart rather than removed.
//
// THE WHOLE DESIGN IS ONE DISTINCTION. We are not carrying the block to the mouth; we are carrying
// the block's MATTER to the mouth. A block that shrinks and slides into a mouth reads as a tile
// being deleted with a tween on it. A block whose face comes away from the side the snake is on,
// leaves along a few threads of its own colour, empties out, keeps a last dense core and is
// finally drunk - that reads as being eaten. Every choice below follows from that sentence, and if
// the result ever looks like "the block got smaller and some coloured lines appeared over it",
// something here has been broken.
//
// WHAT COMES FROM CORE, AND WHAT IS OURS. Which block, which cell, what the snake's body is after,
// and whether it grew are all Core's (SnakeBoss.LastTurn -> SnakeView.TurnScene). The block's
// FACE comes with the report, taken before the rules removed it, so a gold block still looks like
// gold here. Everything in this file is timing, colour and light: it decides nothing.
//
// THE COLOURS ARE THE BLOCK'S OWN. There is no table of "red block -> red effect" anywhere. The
// three levels the effect uses are derived from the face the report handed us (Colours()):
//
//     MATERIAL  the block's own colour, barely touched
//     ENERGY    the same colour with its saturation up - what the matter becomes
//     HOT CORE  a lighter version of it, for the last moment inside the mouth
//
// So gold eats gold, obsidian eats a deep violet, water eats blue - one eating language, the
// nuance carried entirely by the colour it was already wearing. Nothing is ever taken to white:
// a white flash would throw away the one thing that makes each bite specific.
//
// THE PIECES, and why each exists:
//
//   THE PROXY       the block itself, on Resources/Shaders/SnakeEat - its silhouette is taken from
//                   the snake-facing side in a few big soft lobes, with a thin rim of its own
//                   energy colour where the front works, and the light drawn out of what is left.
//                   Never scaled down. Never faded out.
//   THE FILAMENTS   pooled strip meshes on Resources/Shaders/SnakeFilament: one main thread and a
//                   few finer ones, each a bezier from a point on the block's face, lifting a
//                   little, curving into the mouth. The flow along them is baked into the mesh
//                   colours, so it reads as energy moving rather than a scrolling texture. They
//                   are BORN FROM THE EXTRACTION: how many and how bright follows how fast the
//                   block is actually coming apart, which is what sells the mass as conserved.
//   THE MOTES       a few specks, from around the block, accelerating into the mouth. Everything
//                   moves INWARD - there is nothing here that scatters, because this is not a
//                   burst.
//   THE MOUTH       a small, compact concentration of the block's colour, kept INSIDE the mouth
//                   cavity, peaking as the last core goes in.
//   THE RESIDUE     a breath of the block's colour left on the cell it stood in, then nothing.
//
// The gulp is NOT here: the colour has to travel through the snake's own surface, so SnakeEatView
// says how strong it is and what colour it is (GulpShare, FoodColour) and SnakeView carries it
// along the body through the skin material. That is the same split as everywhere else - this file
// owns the block, SnakeView owns the snake.
//
// AUDIO. Nine moments are announced through Sounded so sound can be hung on them later without
// anything in here having to know about it.
using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectBlock.Core;

namespace ProjectBlock.View
{
    /// <summary>The block being eaten: its matter taken off it and drawn into the snake's mouth.</summary>
    public sealed class SnakeEatView : MonoBehaviour
    {
        // =================================================================== the knobs
        //
        // THE TIMELINE OVERLAPS ON PURPOSE. Each phase has a START as well as a length, because a
        // bite that ran its phases one after another would read as a row of separate clips: the
        // mouth begins to open while the head is still coiling, the last of the matter is still
        // leaving as the jaw closes on it, and the gulp starts before the mouth has finished. The
        // starts are all measured from the moment the bite begins.

        public static class Style
        {
            // ---- approach and lock ----

            /// <summary>The head finding its target before anything else happens. Short on
            /// purpose: this is a beat, not a pause.</summary>
            public static float TargetLockDuration = 0.07f;

            /// <summary>The colour-matched sheen that appears on the block's snake-facing edge
            /// during the lock - "this is about to be taken".</summary>
            public static float TargetLockReflection = 0.22f;

            // ---- the coil ----

            public static float CoilStart = 0.02f;

            public static float CoilDuration = 0.06f;

            /// <summary>How far the head draws back, in cells. Two pixels at the board's scale.</summary>
            public static float CoilDistance = 0.034f;

            /// <summary>How much the neck gathers behind it.</summary>
            public static float NeckCompression = 0.038f;

            // ---- the mouth ----

            public static float MouthOpenStart = 0.05f;

            public static float MouthOpenDuration = 0.07f;

            // ---- the lunge ----

            public static float LungeStart = 0.1f;

            public static float LungeDuration = 0.1f;

            /// <summary>How far the head drives in, in cells - it closes on the block, it does not
            /// arrive in its cell.</summary>
            public static float LungeDistance = 0.2f;

            // ---- first contact ----

            /// <summary>How many points on the block's face light up as the jaw lands. Not sparks:
            /// the places its surface is starting to come apart.</summary>
            public static int GripPointCount = 3;

            public static float GripPointStrength = 0.5f;

            public static float GripPointDuration = 0.14f;

            // ---- the near face waking up ----

            public static float SurfaceActivationStart = 0.17f;

            public static float SurfaceActivationDuration = 0.07f;

            public static float SurfaceActivationStrength = 0.55f;

            // ---- the extraction ----

            public static float ExtractionStart = 0.22f;

            public static float ExtractionDuration = 0.26f;

            /// <summary>How far the front gets before the residual core takes over: the last of
            /// the block is not wiped away, it is compressed and drunk.</summary>
            public static float ExtractionBeforeCore = 0.8f;

            /// <summary>How hard the front leans toward the side the snake is on. 1 is straight
            /// off that face.</summary>
            public static float ExtractionDirectionBias = 1f;

            /// <summary>The softness of the boundary. Enough to be anti-aliased and no more: this
            /// is not a noise dissolve.</summary>
            public static float ExtractionSoftness = 0.1f;

            /// <summary>How deep the lobes are - the difference between matter coming away and an
            /// alpha wipe sliding across the face.</summary>
            public static float ExtractionLobeDepth = 0.17f;

            /// <summary>How many lobe layouts there are to pick from, so two blocks never come
            /// apart identically.</summary>
            public static int ExtractionVariationCount = 5;

            public static float ExtractionEdgeLightStrength = 0.6f;

            /// <summary>The rim at the working front, as a share of the face. One or two pixels -
            /// any wider and it is a neon dissolve.</summary>
            public static float ExtractionEdgeWidth = 0.038f;

            /// <summary>How much light is drawn out of the mass still standing.</summary>
            public static float ExtractionDrain = 0.75f;

            // ---- the filaments ----

            /// <summary>The main stream's width where it leaves the block, in cells. It tapers
            /// to a third of this at the mouth. At 5% it was three pixels - a stroke, not a
            /// stream, and no amount of colour fixes a stroke.</summary>
            public static float MainFilamentWidth = 0.105f;

            public static float MainFilamentOpacity = 0.95f;

            public static int SecondaryFilamentCount = 4;

            /// <summary>And a few very short, very thin threads, for the detail between them.</summary>
            public static int MicroFilamentCount = 3;

            public static float MicroFilamentWidth = 0.019f;

            public static float MicroFilamentOpacity = 0.4f;

            public static float SecondaryFilamentWidth = 0.045f;

            public static float SecondaryFilamentOpacity = 0.62f;

            /// <summary>How far a filament lifts away from the straight line to the mouth. This is
            /// what keeps it from being a beam.</summary>
            public static float FilamentCurveStrength = 0.34f;

            /// <summary>How much of the path they stay apart over. They converge only in the last
            /// stretch - five lines running side by side the whole way is the same failure as one
            /// line, and it is what makes a flow look like a decal.</summary>
            public static float FilamentConvergeShare = 0.2f;

            /// <summary>How fast the light travels along a filament, in filament lengths a second.</summary>
            public static float FilamentFlowSpeed = 2.6f;

            /// <summary>How much faster the flow runs as it nears the mouth.</summary>
            public static float FilamentMouthAcceleration = 0.55f;

            public static float FilamentLifetime = 0.2f;

            /// <summary>How deep into the head everything ends, in cells - the jaw's own
            /// opening, not the middle of its face. Short of that the light gathered inside the
            /// mouth sits on the snout and reads as a head painted the colour of its lunch.</summary>
            public static float FilamentMouthDepth = 0.34f;

            // ---- the motes ----

            public static int MoteCount = 6;

            public static float MoteSpeed = 1.5f;

            public static float MoteLifetime = 0.26f;

            public static float MoteMouthAcceleration = 1.9f;

            public static float MoteSize = 0.055f;

            // ---- the last of it ----

            public static float ResidualCoreStart = 0.42f;

            public static float ResidualCoreSize = 0.42f;

            public static float ResidualCoreStrength = 0.9f;

            /// <summary>The last mass holding on for a moment before it goes. A very small piece of
            /// tension, and the reason the end of the bite has a shape.</summary>
            public static float ResidualCoreHoldDuration = 0.035f;

            public static float FinalStrandDuration = 0.09f;

            // ---- inside the mouth ----

            /// <summary>A little of the block's own colour on the lower jaw while it drinks.
            /// Local reflected light - the head is never tinted.</summary>
            public static float HeadReflectionStrength = 0.22f;

            public static float MouthEnergyStrength = 0.62f;

            public static float MouthEnergyRadius = 0.17f;

            public static float MouthCloseStart = 0.48f;

            public static float MouthCloseDuration = 0.12f;

            // ---- the gulp, carried by SnakeView through the snake's own surface ----

            public static float GulpStart = 0.57f;

            public static float GulpDuration = 0.15f;

            public static float GulpHeadStrength = 1f;

            public static float GulpNeckStrength = 0.7f;

            public static float GulpBodyStrength = 0.45f;

            /// <summary>How much of the eaten block's colour the gulp carries. Low: a tint under
            /// the skin, never a painted segment.</summary>
            public static float GulpColorStrength = 0.5f;

            public static float GulpSegmentDelay = 0.055f;

            /// <summary>How wide the swallowed packet is inside a segment, as a share of it.</summary>
            public static float GulpCoreWidth = 0.35f;

            // ---- the swelling that follows ----

            public static float SwollenStart = 0.68f;

            public static float SwollenOvershoot = 0.07f;

            public static float SwollenRelaxDuration = 0.18f;

            public static float SwollenColorResidue = 0.25f;

            public static float SwollenSurfaceTension = 0.5f;

            // ---- what is left on the board ----

            public static float CellResidueStrength = 0.2f;

            public static float CellResidueDuration = 0.09f;

            /// <summary>The whole bite, end to end. Derived, so the phases above cannot drift out
            /// of the length SnakeView gives the beat.</summary>
            public static float Total
            {
                get
                {
                    return Mathf.Max(SwollenStart + SwollenRelaxDuration,
                        Mathf.Max(GulpStart + GulpDuration,
                            MouthCloseStart + MouthCloseDuration));
                }
            }
        }

        // =================================================================== what can be switched off

        public static class Layers
        {
            /// <summary>Draw the extraction front as it is being computed.</summary>
            public static bool ShowExtractionMask;

            /// <summary>Draw every filament's bezier and its control points.</summary>
            public static bool ShowFilamentPaths;

            /// <summary>Mark where the light currently is along each filament.</summary>
            public static bool ShowFilamentFlow;

            public static bool ShowMotes = true;

            /// <summary>Draw the mouth cavity the filaments have to end inside.</summary>
            public static bool ShowMouthMask;

            public static bool ShowGulpPackage;

            /// <summary>Show the three colours taken off the block's own face.</summary>
            public static bool ShowBlockColorSampling;

            /// <summary>The thin energy rim at the dissolving boundary.</summary>
            public static bool ShowErosionEdge = true;

            public static bool ShowMainRibbon = true;

            public static bool ShowSecondaryRibbons = true;

            public static bool ShowMouthCollector = true;

            public static bool ShowHeadReflection = true;

            public static bool ShowFinalCore = true;

            public static void Defaults()
            {
                ShowExtractionMask = false;
                ShowFilamentPaths = false;
                ShowFilamentFlow = false;
                ShowMotes = true;
                ShowMouthMask = false;
                ShowGulpPackage = false;
                ShowBlockColorSampling = false;
                ShowErosionEdge = true;
                ShowMainRibbon = true;
                ShowSecondaryRibbons = true;
                ShowMouthCollector = true;
                ShowHeadReflection = true;
                ShowFinalCore = true;
            }
        }

        // =================================================================== the moments audio wants

        public enum Beat
        {
            Lock,
            MouthOpen,
            Contact,
            ExtractionStart,
            ExtractionPeak,
            FinalCore,
            MouthClose,
            Gulp,
            SwollenSettle
        }

        /// <summary>Every moment of the bite, as it happens, with where it happened. Nothing in
        /// the View listens: this is the seam sound will hang on.</summary>
        public event Action<Beat, Vector2> Sounded;

        // =================================================================== state

        private const int StripPoints = 18;

        // BEHIND the head, over the block. A filament's end has to disappear INTO the mouth,
        // and the only thing that reads as "into" in a top-down 2D board is the head covering it.
        // Over the head it would be a line drawn across the snout, which is the one thing the
        // energy must never look like.
        private const int FilamentOrder = 5;

        private const int MoteOrder = 5;

        private const int ProxyOrder = 4;         // under the body, so the head occludes it

        // The one thing that stays in FRONT: the light gathered inside the open jaw. It is
        // small and it sits in the mouth's own dark interior, so in front reads as "inside".
        private const int MouthOrder = 7;

        private const int BoardOrder = 2;

        private sealed class Strip
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
        }

        /// <summary>One thread of the block's matter on its way to the mouth.</summary>
        private sealed class Filament
        {
            public Strip Strip;
            public Vector2 Source;        // where on the block's face it comes from
            public Vector2 Lift;          // the control point that stops it being a beam
            public Vector2 End;           // its own endpoint inside the mouth cavity
            public float Born;
            public float Life;
            public float Width;
            public float Opacity;
            public float Phase;           // so they do not all pulse together
            public float Along;           // where its lift point sits, so no two share a curve
            public bool Main;
            public bool Micro;
        }

        private sealed class Mote
        {
            public SpriteRenderer Renderer;
            public Vector2 From;
            public Vector2 Lift;
            public float Born;
            public float Life;
            public float Size;
        }

        /// <summary>The default sprite material, taken off the first renderer we make. Never
        /// null: see Rent().</summary>
        private static Material plainMaterial;

        private static Material extractMaterial;
        private static Material filamentMaterial;
        private static bool materialsLooked;

        private BoardView view;
        private bool playing;
        private float clock;
        private float cellSize;
        private uint seed;
        private int variant;

        private ClusterBurstView.Look food;
        private Vector2 cell;
        private Vector2 mouth;
        private Vector2 facing;

        private Color material;
        private Color energy;
        private Color hotCore;

        private SpriteRenderer proxy;
        private SpriteRenderer mouthEnergy;
        private SpriteRenderer residue;
        private SpriteRenderer core;
        private SpriteRenderer headLight;
        private MaterialPropertyBlock block;

        private readonly List<Filament> filaments = new List<Filament>();
        private readonly List<Mote> motes = new List<Mote>();
        private readonly List<SpriteRenderer> grips = new List<SpriteRenderer>();
        private readonly Stack<Strip> spareStrips = new Stack<Strip>();
        private readonly Stack<SpriteRenderer> spareSprites = new Stack<SpriteRenderer>();
        private readonly List<Vector3> stripVertices = new List<Vector3>(StripPoints * 2);
        private readonly List<Color> stripColours = new List<Color>(StripPoints * 2);

        private float extraction;         // 0..1, how much of the block has come away
        private float extractionRate;     // how fast, which is what the filaments answer to
        private bool saidLock;
        private bool saidMouth;
        private bool saidContact;
        private bool saidStart;
        private bool saidPeak;
        private bool saidCore;
        private bool saidClose;
        private bool saidGulp;
        private bool saidSwollen;

        private static readonly int DirId = Shader.PropertyToID("_Dir");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int LobesId = Shader.PropertyToID("_Lobes");
        private static readonly int VariantId = Shader.PropertyToID("_Variant");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        private static readonly int EdgeLightId = Shader.PropertyToID("_EdgeLight");
        private static readonly int EnergyColourId = Shader.PropertyToID("_EnergyColour");
        private static readonly int DrainId = Shader.PropertyToID("_Drain");
        private static readonly int ActivationId = Shader.PropertyToID("_Activation");

        // =================================================================== what SnakeView asks

        public bool Playing
        {
            get { return playing; }
        }

        /// <summary>The colour of what was eaten, for the gulp SnakeView carries down the body.</summary>
        public Color FoodColour
        {
            get { return energy; }
        }

        /// <summary>How far the swallow has travelled, 0 before it starts and 1 when it is in the
        /// body. SnakeView turns this into the packet moving from head to neck to first segment.</summary>
        public float GulpShare
        {
            get { return Window(clock, Style.GulpStart, Style.GulpDuration); }
        }

        /// <summary>How much of the block has come away - the bite's own progress, for anything
        /// that wants to follow it.</summary>
        public float ExtractionShare
        {
            get { return extraction; }
        }

        /// <summary>Whether the mouth should be open right now. The head's sprite is SnakeView's,
        /// but WHEN is the bite's, so the two cannot drift apart.</summary>
        public bool MouthOpen
        {
            get
            {
                return playing && clock >= Style.MouthOpenStart
                    && clock < Style.MouthCloseStart + Style.MouthCloseDuration;
            }
        }

        // =================================================================== the timeline

        /// <summary>How far through a phase the clock is: 0 before it, 1 after it.</summary>
        private static float Window(float t, float start, float length)
        {
            return Mathf.Clamp01((t - start) / Mathf.Max(length, 1e-4f));
        }

        private static float Smooth(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        /// <summary>The head's offset through the bite, in cells behind the block's centre - the
        /// coil, then the lunge. SnakeView asks for this so the head, the mouth and the matter are
        /// all on one clock.</summary>
        public static float HeadOffset(float t, float parked)
        {
            float coil = Mathf.Sin(Mathf.PI * Window(t, Style.CoilStart, Style.CoilDuration));
            float lunge = Smooth(Window(t, Style.LungeStart, Style.LungeDuration));
            float swallow = Smooth(Window(t, Style.MouthCloseStart,
                Style.MouthCloseDuration + Style.GulpDuration * 0.5f));
            float closed = parked - Style.LungeDistance;
            return Mathf.Lerp(parked + Style.CoilDistance * coil, closed, lunge) * (1f - swallow);
        }

        /// <summary>How much the neck gathers behind the coil.</summary>
        public static float NeckGather(float t)
        {
            return Style.NeckCompression
                * Mathf.Sin(Mathf.PI * Window(t, Style.CoilStart, Style.CoilDuration));
        }

        // =================================================================== beginning and ending

        /// <summary>Set up for one bite: the block's own face, its cell, and the three colours
        /// taken off it.</summary>
        public void Begin(BoardView owner, ClusterBurstView.Look eaten, Vector2 blockCentre,
            uint dice)
        {
            Stop();
            view = owner;
            if (view == null)
            {
                return;
            }
            transform.SetParent(view.transform, false);
            cellSize = view.CellWorldSize;
            food = eaten;
            cell = blockCentre;
            mouth = blockCentre;
            facing = new Vector2(1f, 0f);
            seed = dice == 0u ? 1u : dice;
            variant = (int)(seed % (uint)Mathf.Max(Style.ExtractionVariationCount, 1));
            Colours(eaten, out material, out energy, out hotCore);
            // AND THE DEFEAT REMEMBERS IT. Presentation only: it scores nothing and is cleared
            // with the round, but it is what lets the boss's end be about what it took.
            SnakeDefeatView.Remember(material, energy, hotCore);
            clock = 0f;
            extraction = 0f;
            extractionRate = 0f;
            saidLock = saidMouth = saidContact = saidStart = saidPeak = false;
            saidCore = saidClose = saidGulp = saidSwollen = false;
            playing = true;
        }

        /// <summary>Everything down, nothing left running.</summary>
        public void Stop()
        {
            playing = false;
            clock = 0f;
            for (int i = 0; i < filaments.Count; i++)
            {
                ReturnStrip(filaments[i].Strip);
            }
            filaments.Clear();
            for (int i = 0; i < motes.Count; i++)
            {
                Return(motes[i].Renderer);
            }
            motes.Clear();
            for (int i = 0; i < grips.Count; i++)
            {
                Return(grips[i]);
            }
            grips.Clear();
            Return(proxy);
            proxy = null;
            Return(mouthEnergy);
            mouthEnergy = null;
            Return(residue);
            residue = null;
            Return(core);
            core = null;
            Return(headLight);
            headLight = null;
        }

        // =================================================================== the block's own colours

        /// <summary>The three levels the whole effect is built from, taken off the FACE the report
        /// handed us - never from a table of block types. Normalised a little for readability (a
        /// nearly-black obsidian face has to give a colour you can see light in) but never pushed
        /// toward white, which is what would make every block eat the same.</summary>
        public static void Colours(ClusterBurstView.Look look, out Color materialColour,
            out Color energyColour, out Color coreColour)
        {
            // THE BLOCK, NOT ITS TINT. A painted tile's tint is white, so reading the palette
            // off Colour made every block eat the same washed-out nothing - and a protected one
            // eat magenta. Paint is what the block is made of.
            Color c = look.Paint.a > 0f ? look.Paint : look.Colour;
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            // A very dark face (obsidian) is lifted until there is something to carry light, and a
            // very grey one is given back a little of whatever hue it does have.
            if (max < 0.34f)
            {
                float lift = 0.34f / Mathf.Max(max, 0.04f);
                c = new Color(c.r * lift, c.g * lift, c.b * lift, 1f);
                max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            }
            materialColour = new Color(c.r, c.g, c.b, 1f);
            // ENERGY: the same hue, further from grey. Saturation up, not brightness up.
            float mid = (max + min) * 0.5f;
            energyColour = new Color(
                Mathf.Clamp01(mid + (c.r - mid) * 1.45f),
                Mathf.Clamp01(mid + (c.g - mid) * 1.45f),
                Mathf.Clamp01(mid + (c.b - mid) * 1.45f), 1f);
            // HOT CORE: a lighter version of that, and deliberately short of white - a quarter of
            // the way, so it still reads as the block's own colour at its brightest.
            coreColour = new Color(
                Mathf.Lerp(energyColour.r, 1f, 0.3f),
                Mathf.Lerp(energyColour.g, 1f, 0.3f),
                Mathf.Lerp(energyColour.b, 1f, 0.3f), 1f);
        }

        // =================================================================== the frame

        /// <summary>One frame of the bite. <paramref name="t"/> is the bite's own clock, and the
        /// mouth is wherever SnakeView's head has got to - so the filaments always end in the
        /// mouth that is actually on screen, whichever way it came from.</summary>
        public void Paint(float t, Vector2 mouthAt, Vector2 towards, float dt)
        {
            if (!playing || view == null)
            {
                return;
            }
            clock = t;
            mouth = mouthAt;
            facing = towards.sqrMagnitude > 1e-6f ? towards.normalized : facing;
            cellSize = view.CellWorldSize;

            float before = extraction;
            extraction = Extraction(t);
            extractionRate = dt > 1e-5f ? (extraction - before) / dt : 0f;

            Announce(t);
            PaintProxy(t);
            PaintGrips(t);
            Spawn(t, dt);
            PaintFilaments(t, dt);
            PaintMotes(t, dt);
            PaintMouth(t);
            PaintHeadLight(t);
            PaintCore(t);
            PaintResidue(t);
            PaintDebug(t);
            if (t >= Style.Total)
            {
                Stop();
            }
        }

        /// <summary>How much of the block has come away by now. The shape of this curve IS the
        /// rhythm of the bite: a small first pull, the main flow, the block emptying quickly, then
        /// the last core - which holds for a moment before it goes.</summary>
        private static float Extraction(float t)
        {
            float main = Window(t, Style.ExtractionStart, Style.ExtractionDuration);
            // The crescendo: slow in, hardest through the middle, and easing as the block empties.
            float shaped = main < 0.2f ? Mathf.Lerp(0f, 0.12f, main / 0.2f)
                : main < 0.65f ? Mathf.Lerp(0.12f, 0.62f, (main - 0.2f) / 0.45f)
                : Mathf.Lerp(0.62f, 1f, Smooth((main - 0.65f) / 0.35f));
            float bulk = shaped * Style.ExtractionBeforeCore;
            // And the last of it, after a moment of holding on.
            float coreAt = Style.ResidualCoreStart + Style.ResidualCoreHoldDuration;
            float last = Smooth(Window(t, coreAt, Style.FinalStrandDuration));
            return Mathf.Max(bulk, Mathf.Lerp(bulk, 1f, last));
        }

        private void Announce(float t)
        {
            Say(ref saidLock, t >= 0f, Beat.Lock, cell);
            Say(ref saidMouth, t >= Style.MouthOpenStart, Beat.MouthOpen, mouth);
            Say(ref saidContact, t >= Style.LungeStart + Style.LungeDuration * 0.35f,
                Beat.Contact, Face());
            Say(ref saidStart, t >= Style.ExtractionStart, Beat.ExtractionStart, Face());
            Say(ref saidPeak, t >= Style.ExtractionStart + Style.ExtractionDuration * 0.55f,
                Beat.ExtractionPeak, cell);
            Say(ref saidCore, t >= Style.ResidualCoreStart, Beat.FinalCore, cell);
            Say(ref saidClose, t >= Style.MouthCloseStart, Beat.MouthClose, mouth);
            Say(ref saidGulp, t >= Style.GulpStart, Beat.Gulp, mouth);
            Say(ref saidSwollen, t >= Style.SwollenStart, Beat.SwollenSettle, mouth);
        }

        private void Say(ref bool already, bool now, Beat beat, Vector2 at)
        {
            if (already || !now)
            {
                return;
            }
            already = true;
            if (Sounded != null)
            {
                Sounded(beat, at);
            }
        }

        /// <summary>The middle of the block's snake-facing face - where the matter leaves from.</summary>
        private Vector2 Face()
        {
            return cell - facing * (cellSize * 0.5f * Style.ExtractionDirectionBias);
        }

        /// <summary>Inside the mouth cavity, behind the snout - where everything ends up, and what
        /// the head's own sprite then covers.</summary>
        private Vector2 Cavity()
        {
            return mouth + facing * (cellSize * Style.FilamentMouthDepth);
        }

        // =================================================================== the block

        private void PaintProxy(float t)
        {
            if (food.Tile == null)
            {
                return;
            }
            if (proxy == null)
            {
                proxy = Rent(food.Tile, ProxyOrder);
            }
            Material shader = ExtractMaterial();
            float size = view.CubeWorldSize;
            proxy.sprite = food.Tile;
            proxy.color = food.Colour;
            proxy.transform.localPosition = new Vector3(cell.x, cell.y, 0f);
            proxy.transform.localRotation = Quaternion.identity;
            proxy.transform.localScale = SpriteScale(food.Tile, size, size);
            if (shader == null)
            {
                if (plainMaterial != null)
                {
                    proxy.sharedMaterial = plainMaterial;
                }
                // No shader: the block cannot come apart, so it goes in the old way rather than
                // standing there untouched - smaller and into the mouth.
                float k = Smooth(extraction);
                proxy.transform.localPosition = Vector3.Lerp(
                    new Vector3(cell.x, cell.y, 0f), (Vector3)(Vector2)Cavity(), k);
                proxy.transform.localScale = SpriteScale(food.Tile, size * (1f - 0.6f * k),
                    size * (1f - 0.6f * k));
                Color fade = food.Colour;
                fade.a = 1f - Smooth(Mathf.InverseLerp(0.75f, 1f, extraction));
                proxy.color = fade;
                return;
            }
            proxy.sharedMaterial = shader;
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            block.Clear();
            // TOWARD THE MOUTH, which is the OPPOSITE of the way the snake is heading: the
            // snake comes along `facing` into the block, so it is standing on the block's
            // -facing side and that is the face its matter has to leave from. Handing the shader
            // `facing` ate the block from the far side, with the snake watching.
            block.SetVector(DirId, new Vector4(-facing.x, -facing.y, 0f, 0f));
            block.SetFloat(ProgressId, Mathf.Clamp01(extraction));
            block.SetFloat(SoftnessId, Mathf.Max(Style.ExtractionSoftness, 0.01f));
            block.SetFloat(LobesId, Style.ExtractionLobeDepth);
            block.SetFloat(VariantId, variant);
            block.SetFloat(EdgeWidthId, Style.ExtractionEdgeWidth);
            // The rim only burns where the front is working: a hint during the lock, full once the
            // matter is actually leaving.
            float lock1 = Window(t, 0f, Style.TargetLockDuration) * Style.TargetLockReflection;
            float working = extraction > 0f && extraction < 1f ? 1f : 0f;
            block.SetFloat(EdgeLightId, Layers.ShowErosionEdge
                ? Mathf.Max(lock1, working * Style.ExtractionEdgeLightStrength) : 0f);
            block.SetColor(EnergyColourId, energy);
            block.SetFloat(DrainId, Style.ExtractionDrain * Smooth(Mathf.InverseLerp(
                Style.ExtractionStart, Style.ExtractionStart + Style.ExtractionDuration, t)));
            block.SetFloat(ActivationId, Style.SurfaceActivationStrength
                * Window(t, Style.SurfaceActivationStart, Style.SurfaceActivationDuration)
                * (1f - Smooth(extraction * 1.6f)));
            proxy.SetPropertyBlock(block);
        }

        // =================================================================== first contact

        private void PaintGrips(float t)
        {
            float k = Window(t, Style.LungeStart + Style.LungeDuration * 0.35f,
                Style.GripPointDuration);
            if (k <= 0f || k >= 1f)
            {
                for (int i = 0; i < grips.Count; i++)
                {
                    Return(grips[i]);
                }
                grips.Clear();
                return;
            }
            int want = Mathf.Max(Style.GripPointCount, 0);
            while (grips.Count < want)
            {
                grips.Add(Rent(SnakeVfxController.SoftDot(), FilamentOrder));
            }
            var dice = new Dice(seed ^ 0x51ed270bu);
            Vector2 side = new Vector2(-facing.y, facing.x);
            Vector2 face = Face();
            for (int i = 0; i < grips.Count; i++)
            {
                float off = dice.Range(-0.3f, 0.3f);
                float depth = dice.Range(0.02f, 0.16f);
                Vector2 at = face + side * (off * cellSize) + facing * (depth * cellSize);
                float show = Mathf.Sin(Mathf.PI * k) * Style.GripPointStrength;
                Place(grips[i], at, cellSize * 0.09f, cellSize * 0.09f,
                    Tint(energy, show), FilamentOrder);
            }
        }

        // =================================================================== birth

        /// <summary>Filaments and motes are born FROM THE EXTRACTION: the faster the block is
        /// coming apart, the more there is on its way to the mouth. That link is what makes the
        /// mass read as conserved instead of as beams laid over a block that is still standing.</summary>
        private void Spawn(float t, float dt)
        {
            if (dt <= 0f || extraction >= 1f)
            {
                return;
            }
            bool flowing = extraction > 0f;
            if (!flowing)
            {
                return;
            }
            float rate = Mathf.Clamp01(extractionRate / 3.2f);
            int wantMain = 1;
            int wantSecondary = Mathf.RoundToInt(Style.SecondaryFilamentCount
                * Mathf.Clamp01(0.35f + rate));
            int wantMicro = Mathf.RoundToInt(Style.MicroFilamentCount * Mathf.Clamp01(rate * 1.4f));
            int mains = 0;
            int seconds = 0;
            int micros = 0;
            for (int i = 0; i < filaments.Count; i++)
            {
                if (filaments[i].Main)
                {
                    mains++;
                }
                else if (filaments[i].Micro)
                {
                    micros++;
                }
                else
                {
                    seconds++;
                }
            }
            var dice = new Dice(seed ^ (uint)(Mathf.FloorToInt(t * 120f) * 2654435761u));
            if (mains < wantMain && Layers.ShowMainRibbon)
            {
                Add(t, true, false, dice);
            }
            if (seconds < wantSecondary && Layers.ShowSecondaryRibbons)
            {
                Add(t, false, false, dice);
            }
            if (micros < wantMicro && Layers.ShowSecondaryRibbons)
            {
                Add(t, false, true, dice);
            }
            if (Layers.ShowMotes && motes.Count < Style.MoteCount && rate > 0.1f)
            {
                AddMote(t, dice);
            }
        }

        private void Add(float t, bool main, bool micro, Dice dice)
        {
            Strip strip = RentStrip(FilamentOrder);
            if (strip == null)
            {
                return;
            }
            Vector2 side = new Vector2(-facing.y, facing.x);
            // FROM THE MATTER THAT IS STILL THERE. What is left of the block lies beyond the
            // front, on the far side, so a thread of it starts there, crosses the part that has
            // already gone, and ends in the mouth. Sourcing it from the near face instead gave
            // the filaments nowhere to travel - the mouth is parked ON that face - and it would
            // have been a lie anyway: that matter has left already.
            float across = dice.Range(-0.34f, 0.34f);
            // AT THE BOUNDARY THAT IS DISSOLVING. Not anywhere in the remaining mass and never in
            // a region that has already gone: the light has to come off the place the matter is
            // actually leaving, and that place moves inward as the block empties.
            float depth = Mathf.Lerp(extraction, Mathf.Min(extraction + 0.3f, 1f),
                dice.Range(0f, 1f));
            Vector2 source = cell + facing * ((depth - 0.5f) * cellSize)
                + side * (across * cellSize);
            Vector2 straight = Cavity() - source;
            // Each one bends at a DIFFERENT fraction of its path, so no two share a curve and
            // they cannot run parallel.
            float along = dice.Range(0.3f, 0.58f);
            // THE BEND IS IN PROPORTION TO THE RUN. A fixed lateral offset on a short path curls
            // the ribbon back over itself - a squiggle, which reads worse than the straight line
            // it was meant to avoid. So it is scaled by how far the ribbon actually has to go.
            float reach = Mathf.Clamp01(straight.magnitude / Mathf.Max(cellSize, 1e-4f));
            Vector2 lift = source + straight * along
                + side * (dice.Range(-1f, 1f) * Style.FilamentCurveStrength * reach * cellSize);
            var f = new Filament
            {
                Strip = strip,
                Source = source,
                Lift = lift,
                // They converge in the LAST stretch, not along the way.
                End = Cavity() + side * (dice.Range(-1f, 1f) * Style.FilamentConvergeShare
                    * 0.35f * cellSize),
                Born = t,
                Life = Style.FilamentLifetime * dice.Range(0.85f, 1.2f)
                    * (micro ? 0.55f : 1f),
                Width = (main ? Style.MainFilamentWidth
                    : micro ? Style.MicroFilamentWidth : Style.SecondaryFilamentWidth) * cellSize,
                Opacity = main ? Style.MainFilamentOpacity
                    : micro ? Style.MicroFilamentOpacity : Style.SecondaryFilamentOpacity,
                Phase = dice.Range(0f, 1f),
                Along = along,
                Main = main,
                Micro = micro
            };
            filaments.Add(f);
        }

        private void AddMote(float t, Dice dice)
        {
            Vector2 side = new Vector2(-facing.y, facing.x);
            // Motes come off the mass as well, not out of thin air in front of it.
            float depth = Mathf.Lerp(extraction, 1f, dice.Range(0f, 1f));
            Vector2 from = cell + facing * ((depth - 0.5f) * cellSize)
                + side * (dice.Range(-0.5f, 0.5f) * cellSize);
            motes.Add(new Mote
            {
                Renderer = Rent(SnakeVfxController.SoftDot(), MoteOrder),
                From = from,
                Lift = Vector2.Lerp(from, Cavity(), 0.5f)
                    + side * (dice.Range(-0.22f, 0.22f) * cellSize),
                Born = t,
                // How long it takes is how far it has to go at the speed it travels - capped by
                // its own lifetime, so a mote from the far side of the block is not still on
                // screen when the mouth has closed.
                Life = Mathf.Min(Style.MoteLifetime * dice.Range(0.8f, 1.25f),
                    (Cavity() - from).magnitude / Mathf.Max(Style.MoteSpeed * cellSize, 1e-3f)),
                Size = Style.MoteSize * dice.Range(0.7f, 1.3f) * cellSize
            });
        }

        // =================================================================== the filaments

        private void PaintFilaments(float t, float dt)
        {
            for (int i = filaments.Count - 1; i >= 0; i--)
            {
                Filament f = filaments[i];
                float age = t - f.Born;
                if (age >= f.Life || extraction >= 1f && age > f.Life * 0.5f)
                {
                    ReturnStrip(f.Strip);
                    filaments.RemoveAt(i);
                    continue;
                }
                // Its endpoint follows the mouth, because the head is still moving.
                f.End = Vector2.Lerp(f.End, Cavity(), Mathf.Clamp01(dt * 18f));
                Build(f, age / f.Life, t);
            }
        }

        /// <summary>One filament's strip. The LIGHT ALONG IT is what carries the flow: a packet
        /// travelling source to mouth, the source end giving out behind it and the filament
        /// shortening as it is drunk. Baked into the mesh colours - nothing scrolls a texture.</summary>
        private void Build(Filament f, float k, float t)
        {
            if (f.Strip == null)
            {
                return;
            }
            stripVertices.Clear();
            stripColours.Clear();
            // The packet's head, running from the source to the mouth and speeding up as it goes.
            float flow = (t * Style.FilamentFlowSpeed + f.Phase) % 1f;
            float packet = flow + Style.FilamentMouthAcceleration * flow * flow * (1f - flow);
            // ROOTED IN THE MATTER IT CAME FROM. The source end is consumed only at the end
            // of its life - eaten from the first frame, all that was left on screen was a dash
            // near the mouth with a gap behind it, which is the "beams over a block that is still
            // standing" the whole design exists to avoid.
            float from = Smooth(Mathf.Clamp01((k - 0.6f) / 0.4f)) * 0.9f;
            for (int p = 0; p < StripPoints; p++)
            {
                float s = p / (float)(StripPoints - 1);
                float along = Mathf.Lerp(from, 1f, s);
                Vector2 at = Bezier(f.Source, f.Lift, f.End, along);
                Vector2 next = Bezier(f.Source, f.Lift, f.End, Mathf.Min(along + 0.02f, 1f));
                Vector2 dir = next - at;
                Vector2 side = dir.sqrMagnitude > 1e-9f
                    ? new Vector2(-dir.y, dir.x).normalized : new Vector2(-facing.y, facing.x);
                // Wide where it leaves the block, tapering into the mouth: matter becoming light.
                // Wide where it leaves the block, a third of that at the mouth - and blended
                // INTO the block's surface at the source rather than starting with a blunt end.
                float taper = Mathf.Lerp(1f, 0.33f, along)
                    * Smooth(Mathf.Clamp01(s / 0.14f))
                    * Mathf.Lerp(1f, 0.55f, Smooth(Mathf.Clamp01((s - 0.8f) / 0.2f)));
                float w = f.Width * taper;
                stripVertices.Add(at - side * (w * 0.5f));
                stripVertices.Add(at + side * (w * 0.5f));
                // MATERIAL where it leaves, ENERGY along the way, HOT CORE as it reaches the mouth.
                Color colour = along < 0.5f
                    ? Color.Lerp(material, energy, along / 0.5f)
                    : Color.Lerp(energy, hotCore, (along - 0.5f) / 0.5f);
                float travel = 1f - Mathf.Clamp01(Mathf.Abs(along - packet) / 0.46f);
                float lit = 0.6f + 0.4f * travel * travel;
                float ends = Smooth(Mathf.Clamp01(s / 0.12f))
                    * Smooth(Mathf.Clamp01((1f - s) / 0.08f + 0.4f));
                float fade = 1f - Smooth(Mathf.Clamp01((k - 0.7f) / 0.3f));
                colour.a = f.Opacity * lit * ends * fade;
                stripColours.Add(colour);
                stripColours.Add(colour);
            }
            Mesh mesh = f.Strip.Mesh;
            mesh.SetVertices(stripVertices);
            mesh.SetColors(stripColours);
            mesh.RecalculateBounds();
            f.Strip.Renderer.enabled = true;
        }

        /// <summary>A quadratic through the lift point: enough curve that it is never a beam, not
        /// so much that it leaves the board's own plane.</summary>
        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float s)
        {
            float u = 1f - s;
            return u * u * a + 2f * u * s * b + s * s * c;
        }

        // =================================================================== the motes

        private void PaintMotes(float t, float dt)
        {
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                Mote m = motes[i];
                float k = (t - m.Born) / Mathf.Max(m.Life, 1e-3f);
                if (k >= 1f || !Layers.ShowMotes)
                {
                    Return(m.Renderer);
                    motes.RemoveAt(i);
                    continue;
                }
                // Everything goes INWARD, and faster the nearer it gets: this is suction, not a
                // burst. Nothing in the whole effect travels away from the mouth.
                float eased = k + Style.MoteMouthAcceleration * 0.25f * k * k * (1f - k);
                Vector2 at = Bezier(m.From, m.Lift, Cavity(), Mathf.Clamp01(eased));
                float show = Smooth(Mathf.Clamp01(k / 0.18f)) * (1f - Smooth(
                    Mathf.Clamp01((k - 0.6f) / 0.4f)));
                float size = m.Size * Mathf.Lerp(1f, 0.55f, k);
                Place(m.Renderer, at, size, size,
                    Tint(Color.Lerp(material, hotCore, k), show), MoteOrder);
            }
        }

        // =================================================================== inside the mouth

        private void PaintMouth(float t)
        {
            // It builds as the matter arrives and peaks as the last core goes in - then it is shut
            // in, not released. Kept small and inside the cavity: no light comes back out.
            float build = Mathf.Clamp01(extraction * 1.15f);
            float shut = Smooth(Window(t, Style.MouthCloseStart + Style.MouthCloseDuration * 0.5f,
                Style.MouthCloseDuration * 0.6f));
            float show = Style.MouthEnergyStrength * build * (1f - shut);
            if (!Layers.ShowMouthCollector || show <= 0.002f)
            {
                Return(mouthEnergy);
                mouthEnergy = null;
                return;
            }
            if (mouthEnergy == null)
            {
                mouthEnergy = Rent(SnakeVfxController.SoftDot(), MouthOrder);
            }
            float r = Style.MouthEnergyRadius * cellSize * Mathf.Lerp(0.7f, 1f, build);
            Place(mouthEnergy, Cavity(), r, r * 0.86f,
                Tint(Color.Lerp(energy, hotCore, build), show), MouthOrder);
        }

        /// <summary>A little of the block's own colour on the lower jaw while it is drinking -
        /// local reflected light, and the ONLY thing the head takes from what it eats. A tinted
        /// head is the failure this exists instead of.</summary>
        private void PaintHeadLight(float t)
        {
            float show = Style.HeadReflectionStrength * Mathf.Clamp01(extraction * 1.3f)
                * (1f - Smooth(Window(t, Style.MouthCloseStart, Style.MouthCloseDuration)));
            if (!Layers.ShowHeadReflection || show <= 0.002f)
            {
                Return(headLight);
                headLight = null;
                return;
            }
            if (headLight == null)
            {
                headLight = Rent(SnakeVfxController.SoftDot(), MouthOrder);
            }
            // Just behind the jaw, under the snout: where light in the mouth would fall.
            Vector2 at = mouth + facing * (cellSize * 0.14f)
                - new Vector2(facing.y, -facing.x) * (cellSize * 0.1f);
            Place(headLight, at, cellSize * 0.3f, cellSize * 0.22f,
                Tint(Color.Lerp(material, energy, 0.5f), show), MouthOrder);
        }

        private void PaintCore(float t)
        {
            // The last of the block: smaller, denser, and holding on for a moment before the final
            // strand takes it. This is the shape at the end of the bite.
            float from = Style.ExtractionBeforeCore;
            float k = Mathf.InverseLerp(from, 1f, extraction);
            bool alive = Layers.ShowFinalCore && extraction >= from * 0.98f && extraction < 1f;
            if (!alive)
            {
                Return(core);
                core = null;
                return;
            }
            if (core == null)
            {
                core = Rent(SnakeVfxController.SoftDot(), FilamentOrder);
            }
            // It sits in what is left of the block, on the far side of the front.
            Vector2 at = cell + facing * (cellSize * Mathf.Lerp(0.12f, -0.05f, k));
            float r = Style.ResidualCoreSize * cellSize * Mathf.Lerp(1f, 0.45f, k);
            float show = Style.ResidualCoreStrength * Mathf.Lerp(0.7f, 1f, k);
            Place(core, at, r, r, Tint(Color.Lerp(energy, hotCore, k), show), FilamentOrder);
        }

        private void PaintResidue(float t)
        {
            // A breath of the block's colour on the cell it stood in - and never enough to read as
            // a cell with something in it.
            float k = Window(t, Style.MouthCloseStart, Style.CellResidueDuration);
            float show = Style.CellResidueStrength * (1f - Smooth(k)) * Smooth(extraction);
            if (show <= 0.002f || extraction < 0.9f)
            {
                Return(residue);
                residue = null;
                return;
            }
            if (residue == null)
            {
                residue = Rent(SnakeVfxController.SoftDot(), BoardOrder);
            }
            Place(residue, cell, cellSize * 0.52f, cellSize * 0.4f, Tint(material, show),
                BoardOrder);
        }

        // =================================================================== the switches

        private void PaintDebug(float t)
        {
            if (!Layers.ShowFilamentPaths && !Layers.ShowFilamentFlow && !Layers.ShowMouthMask
                && !Layers.ShowExtractionMask && !Layers.ShowBlockColorSampling)
            {
                return;
            }
            if (Layers.ShowExtractionMask)
            {
                Vector2 face = Face();
                Vector2 side = new Vector2(-facing.y, facing.x);
                Vector2 front = face + facing * (extraction * cellSize);
                Debug.DrawLine(front - side * (cellSize * 0.5f), front + side * (cellSize * 0.5f),
                    energy);
            }
            if (Layers.ShowMouthMask)
            {
                Vector2 c = Cavity();
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f;
                    float b = (i + 1) / 12f * Mathf.PI * 2f;
                    float r = Style.MouthEnergyRadius * cellSize;
                    Debug.DrawLine(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r,
                        c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * r, Color.cyan);
                }
            }
            for (int i = 0; i < filaments.Count; i++)
            {
                Filament f = filaments[i];
                if (Layers.ShowFilamentPaths)
                {
                    Vector2 prev = f.Source;
                    for (int p = 1; p <= 10; p++)
                    {
                        Vector2 at = Bezier(f.Source, f.Lift, f.End, p / 10f);
                        Debug.DrawLine(prev, at, f.Main ? Color.yellow : Color.green);
                        prev = at;
                    }
                    Debug.DrawLine(f.Lift, f.Lift + Vector2.up * (cellSize * 0.1f), Color.grey);
                }
                if (Layers.ShowFilamentFlow)
                {
                    float flow = (t * Style.FilamentFlowSpeed + f.Phase) % 1f;
                    Vector2 at = Bezier(f.Source, f.Lift, f.End, flow);
                    Debug.DrawLine(at + Vector2.left * (cellSize * 0.04f),
                        at + Vector2.right * (cellSize * 0.04f), Color.white);
                }
            }
            if (Layers.ShowBlockColorSampling)
            {
                Vector2 at = cell + Vector2.up * (cellSize * 0.7f);
                float w = cellSize * 0.18f;
                Debug.DrawLine(at, at + Vector2.right * w, material);
                Debug.DrawLine(at + Vector2.right * w, at + Vector2.right * (w * 2f), energy);
                Debug.DrawLine(at + Vector2.right * (w * 2f), at + Vector2.right * (w * 3f),
                    hotCore);
            }
        }

        // =================================================================== pools and plumbing

        private static Material ExtractMaterial()
        {
            LookUpMaterials();
            return extractMaterial;
        }

        private static Material FilamentMaterial()
        {
            LookUpMaterials();
            return filamentMaterial;
        }

        private static void LookUpMaterials()
        {
            if (materialsLooked)
            {
                return;
            }
            materialsLooked = true;
            extractMaterial = Find("ProjectBlock/SnakeEat");
            filamentMaterial = Find("ProjectBlock/SnakeFilament");
        }

        private static Material Find(string name)
        {
            Shader shader = Shader.Find(name);
            if (shader == null)
            {
                shader = Resources.Load<Shader>("Shaders/" + name.Substring(name.IndexOf('/') + 1));
            }
            if (shader == null || !shader.isSupported)
            {
                return null;
            }
            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
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
                var go = new GameObject("Eat");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plainMaterial == null)
                {
                    // THE DEFAULT SPRITE MATERIAL, KEPT. A SpriteRenderer with NO material is not
                    // drawn with the default one - Unity draws it with the MAGENTA ERROR material,
                    // which ignores the tint entirely. Assigning null here is what made every
                    // light, mote, core and collector in this effect come out flat pink whatever
                    // block was being eaten, and no amount of colour work upstream could show
                    // through it.
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
            r.SetPropertyBlock(null);
            r.enabled = false;
            spareSprites.Push(r);
        }

        /// <summary>A pooled strip: its layout never changes, only its vertices and colours.</summary>
        private Strip RentStrip(int order)
        {
            Material mat = FilamentMaterial();
            if (mat == null)
            {
                return null;   // no shader, no filaments: the motes carry the bite alone
            }
            Strip s;
            if (spareStrips.Count > 0)
            {
                s = spareStrips.Pop();
            }
            else
            {
                var go = new GameObject("Filament");
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
            r.transform.localScale = SpriteScale(r.sprite, width, height);
        }

        private static Vector3 SpriteScale(Sprite sprite, float width, float height)
        {
            if (sprite == null)
            {
                return Vector3.one;
            }
            Vector2 size = sprite.bounds.size;
            return new Vector3(width / Mathf.Max(size.x, 1e-4f),
                height / Mathf.Max(size.y, 1e-4f), 1f);
        }

        private static Color Tint(Color c, float alpha)
        {
            c.a = Mathf.Clamp01(alpha);
            return c;
        }

        /// <summary>The same xorshift the rest of the snake uses, so a bite looks the same every
        /// time the same bite happens - and the lab can be compared frame for frame.</summary>
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
