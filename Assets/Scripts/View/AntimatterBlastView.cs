// PURPOSE: "Antimadde" - the ANNIHILATION EVENT. The key landed on a perfect overlay and the rules
// emptied every cube of one element; this is what the player sees happen to them. It is the
// hardest single thing in the game to land (a perfect fit, on a shape you did not choose, before it
// rots), and it used to be drawn as a purple charge, a purple flash and a ring - which is a power
// effect, not a legendary one.
//
// THE CUBES DO NOT EXPLODE. MATTER MEETS ITS ANTIMATTER AND STOPS BEING MATTER. So nothing here
// throws debris, smoke or fire, and the ordinary cluster burst does not run on these cells (the
// controller keeps them out of FlashCells). The motion language is the whole design:
//
//     INWARD -> INWARD -> INWARD -> SILENCE -> MASSIVE OUTWARD ENERGY -> OUTWARD SHOCK -> SPIRAL
//     INWARD RESIDUE
//
// Per cube, on one clock (Style), staggered a few ms per cell from a hash so it is one event and
// never a domino:
//   NOTICE     reality notices: the cube dims a touch, loses a little colour, and a hair of
//              violet chromatic split appears on its edge. Nothing moves. Nothing shakes.
//   GHOST      its ANTIMATTER TWIN separates from it - the same silhouette in ultraviolet, offset
//              2-5 px along a per-cell direction, transparent in the middle so the real matter
//              still reads warm through it. Another physical state of the same cube, not a block.
//   FILAMENTS  thin broken lavender tension lines between NEAR targets only (1-2 per cube, a
//              global cap): energy balancing, never a web over the board.
//   CHARGE     a small violet singularity core in each cube, compressing its brightness steadily
//              (no pulse); a ring in the element's own colour closes from the cube's EDGE on its
//              centre, slow first and then accelerating; the cube's face is pulled into its middle
//              (a radial uv pull in the shader, 1-3 px, 5 on hero cells) and it gives a few percent.
//   COLLISION  the twin comes back - 5 px, 2, 1, 0 - brightening as it comes; on contact a 1-2 px
//              lavender line runs round the cube's real outline.
//   IMPLOSION  the silent collapse: 0.95 -> 0.74, the face pulled hard into the middle, the
//              element's colour briefly OVER-intense (gold goes deep gold) and then sinking into
//              the centre. The sound has already dropped out.
//   PEAK       the matter is gone: a small, extremely bright near-white LAVENDER core per cell for
//              a few frames. Never white.
//   BEAMS      annihilation rays from a spatially spread set of HERO cells (never all of them):
//              tapered, turbulent, layered (bloom / violet body / lavender core / cyan-magenta
//              fringe), the element's colour at the root. Straight paths, broken light - never a
//              laser and never a zig-zag.
//   GLOBAL     at the event's centroid (a board-space VFX anchor, possibly an empty cell): a crown
//              of short blades, a hot central core, a brief soft light column, an energy-release
//              LENS (violet-black, expanding, gone), a three-layer SHOCKWAVE across the arena
//              (lavender pressure edge / refractive band / faint violet rim) that lights the rims
//              of the cubes that stay as it passes (reflection, never damage), a single pressure
//              IMPULSE of the arena (never the camera), a faint violet fringe at the screen's
//              edges, and the background dimmed a few percent through the buildup.
//   AFTERMATH  spectral motes thrown out 8-20 px curve back and spiral into their cell or the
//              centroid; each emptied cell shows the NEGATIVE of the cube that stood there, with
//              diffraction, grain and a swim, dissolving from its centre outward. Then it is an
//              ordinary empty cell - no residue stays.
//   PAYOFF     one "+TOTAL" in ivory with a lavender-violet fringe, popped at the centroid, held,
//              gathered into a lavender core and carried on a short curved path to the TOTAL,
//              which answers 1 -> 1.10 -> 0.98 -> 1 in lavender-gold. Two or three cubes also show
//              their own small share; more than that is aggregate only.
//
// THE VIEW DECIDES NOTHING. The cells, the kind, the faces (the board's last look, so a blind
// round stays blind) and the points are handed in (Request); the points are the joker's MEASURED
// payment and are never worked out here - the rot changes the payoff, never the size of the blast.
// Hero cells, filament pairs, beam angles and particle paths are a visual graph off a hash of the
// cells and create no topology the rules know about.
//
// It lives OUTSIDE the board's transform (BoardView.Rebuild destroys the board's children) and
// follows it every frame instead, so the overtime squeeze and the impulse still carry it along.
// Everything the event draws is pooled; nothing is allocated per frame.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class AntimatterBlastView : MonoBehaviour
    {
        public enum Quality
        {
            High,
            Medium,
            Low
        }

        /// <summary>The moments the audio (and one day the haptics) hang off. The implosion's
        /// sound ends a few tens of ms before the peak on purpose - that hole is what makes the
        /// peak land.</summary>
        public enum Beat
        {
            RealityDistort,
            Ghost,
            Charge,
            Contact,
            Implosion,
            Peak,
            Beam,
            Shockwave,
            Aftermath,
            Reward
        }

        /// <summary>Every timing (seconds at 1x) and every amount. Sizes are SCREEN PIXELS unless
        /// they say otherwise, so the event keeps its weight on any board size.</summary>
        public static class Style
        {
            public static float Notice = 0.10f;

            public static float GhostStart = 0.08f;
            public static float GhostDuration = 0.13f;
            public static float GhostAlpha = 0.65f;
            public static float GhostOffsetMin = 2f;
            public static float GhostOffsetMax = 5f;

            public static float FilamentStart = 0.12f;
            public static float FilamentBuild = 0.16f;
            public static float FilamentWidth = 1.4f;
            public static float FilamentAlpha = 0.8f;
            public static int MaxFilaments = 18;

            public static float ChargeStart = 0.20f;
            public static float ChargeDuration = 0.22f;
            /// <summary>The singularity core's starting radius, as a share of the cube's width.
            /// </summary>
            public static float CoreStartRadius = 0.12f;
            public static float CoreBrightness = 0.9f;

            public static float RingStart = 0.22f;
            public static float RingDuration = 0.22f;
            public static float UvPull = 3f;
            public static float UvPullHero = 5f;

            public static float CollisionStart = 0.36f;
            public static float CollisionDuration = 0.12f;
            public static float ContactDuration = 0.045f;

            public static float ImplosionStart = 0.49f;
            public static float ImplosionDuration = 0.085f;
            public static float ImplosionScale = 0.74f;

            public static float Peak = 0.58f;
            public static float PeakDuration = 0.035f;

            public static int BeamCap = 16;
            /// <summary>Hero ray lengths, in CELLS; a rare one reaches BeamLengthRare.</summary>
            public static float BeamLengthMin = 0.8f;
            public static float BeamLengthMax = 3.0f;
            public static float BeamLengthRare = 4.0f;
            /// <summary>The core's width, px. The ray's soft body and bloom run well past it.</summary>
            public static float BeamWidthMin = 5f;
            public static float BeamWidthMax = 12f;
            public static float BeamGrow = 0.022f;
            public static float BeamHold = 0.03f;
            public static float BeamRetract = 0.04f;
            public static float BeamAngleVariance = 20f;
            public static float BeamCoreBrightness = 0.9f;

            /// <summary>The lens's largest radius, as a share of the board's larger side.</summary>
            public static float LensRadius = 0.36f;
            public static float LensDuration = 0.14f;
            public static float CrownDuration = 0.07f;
            public static float CentralCoreDuration = 0.10f;
            public static float ColumnDuration = 0.08f;
            public static float ColumnAlpha = 0.14f;
            public static float ColumnWidth = 40f;
            public static float EdgeDuration = 0.11f;

            public static float ShockwaveStart = 0.60f;
            public static float ShockwaveDuration = 0.22f;
            public static float BoardImpulse = 3f;
            public static float ImpulseDuration = 0.13f;

            public static float AfterimageStart = 0.66f;
            public static float AfterimageDuration = 0.28f;
            public static float AfterimageAlpha = 0.36f;

            public static float ParticleBurst = 0.09f;
            public static float SpiralDuration = 0.30f;
            public static int ParticleCap = 56;
            public static float SpiralTurnsMin = 0.25f;
            public static float SpiralTurnsMax = 0.6f;

            /// <summary>Each cell's own clock is shifted by up to this much either way.</summary>
            public static float Stagger = 0.025f;
            /// <summary>How far the board is dimmed through the buildup, perceptually.</summary>
            public static float BoardDim = 0.08f;
            public static float BackgroundDim = 1f;
            public static float BackgroundReturn = 0.18f;

            public static float ScoreAt = 1.0f;
            public static float ScorePop = 0.24f;
            public static float RewardHold = 0.15f;
            public static float ScoreCollect = 0.09f;
            public static float ScoreTravel = 0.36f;
            public static float ScoreLand = 0.3f;

            /// <summary>A clean sweep the annihilation caused waits this long after the shockwave
            /// has crossed the arena, so the two big events never play at once.</summary>
            public static float CleanupBreath = 0.12f;

            public static Quality Level = Quality.High;

            /// <summary>Lab only: freeze the event's clock here (negative = never).</summary>
            public static float HoldAt = -1f;

            /// <summary>Lab only: hold each ghost this many CELLS off its matter (0 = the design).
            /// </summary>
            public static float GhostApart;

            /// <summary>When after Begin a clean sweep the event caused should start.</summary>
            public static float CleanupDelay
            {
                get { return ShockwaveStart + ShockwaveDuration + CleanupBreath; }
            }
        }

        /// <summary>Each layer on its own, so the lab can show one at a time; and the debug views.
        /// A switch set before Prepare decides what is rented at all.</summary>
        public static class Layers
        {
            public static bool Notice = true;
            public static bool Ghosts = true;
            public static bool Filaments = true;
            public static bool Cores = true;
            public static bool InwardRings = true;
            public static bool UvPull = true;
            public static bool Implosion = true;
            public static bool Contact = true;
            public static bool PeakCores = true;
            public static bool Beams = true;
            public static bool LocalWaves = true;
            public static bool Lens = true;
            public static bool Shockwave = true;
            public static bool Crown = true;
            public static bool CentralCore = true;
            public static bool Column = true;
            public static bool EdgeFringe = true;
            public static bool Afterimages = true;
            public static bool Particles = true;
            public static bool Reflections = true;
            public static bool BoardDim = true;
            public static bool BackgroundDim = true;
            public static bool BoardImpulse = true;
            public static bool Score = true;

            public static bool ShowTargets;
            public static bool ShowGhosts;
            public static bool ShowFilaments;
            public static bool ShowCores;
            public static bool ShowInwardRings;
            public static bool ShowUvPull;
            public static bool ShowImplosionScale;
            public static bool ShowHeroBeamOrigins;
            public static bool ShowBeamDirections;
            public static bool ShowCentroid;
            public static bool ShowLens;
            public static bool ShowShockwave;
            public static bool ShowAfterimages;
            public static bool ShowSpiralPaths;
            public static bool ShowScoreAnchor;
            public static bool ShowBudget;

            public static void AllOn()
            {
                SetAll(true);
            }

            /// <summary>Every EFFECT layer off (the matter proxy still stands), for the lab's
            /// "only" scenes, which then turn on the one they show.</summary>
            public static void AllOff()
            {
                SetAll(false);
            }

            private static void SetAll(bool on)
            {
                Notice = on; Ghosts = on; Filaments = on; Cores = on; InwardRings = on; UvPull = on;
                Implosion = on; Contact = on; PeakCores = on; Beams = on; LocalWaves = on; Lens = on;
                Shockwave = on; Crown = on; CentralCore = on; Column = on; EdgeFringe = on;
                Afterimages = on; Particles = on; Reflections = on; BoardDim = on; BackgroundDim = on;
                BoardImpulse = on; Score = on;
            }

            public static void DebugOff()
            {
                ShowTargets = false; ShowGhosts = false; ShowFilaments = false; ShowCores = false;
                ShowInwardRings = false; ShowUvPull = false; ShowImplosionScale = false;
                ShowHeroBeamOrigins = false; ShowBeamDirections = false; ShowCentroid = false;
                ShowLens = false; ShowShockwave = false; ShowAfterimages = false;
                ShowSpiralPaths = false; ShowScoreAnchor = false; ShowBudget = false;
            }
        }

        /// <summary>One annihilated cube: where it was, and the face and material it wore.</summary>
        public struct Target
        {
            public GridPos Cell;
            public Sprite Tile;
            public Color Tint;
            /// <summary>What the block is MADE of (ViewUtil.CubeMaterialColor) - never the tint,
            /// which is white on a tile that paints itself.</summary>
            public Color Matter;
        }

        /// <summary>Everything the event needs, all of it the rules' answer.</summary>
        public sealed class Request
        {
            public CubeKind Kind;
            public readonly List<Target> Targets = new List<Target>();
            /// <summary>The joker's measured payment at the score's scale; 0 = nothing to show.
            /// </summary>
            public int Points;
            public int PointsPerCube;
            public uint Seed;
        }

        // ---- wiring, asked every time rather than stored ----
        public System.Func<Transform> Arena;
        public System.Func<GridPos, Vector2> CellLocal;
        public System.Func<float> CellSize;
        public System.Func<float> CubeSize;
        public System.Func<float> PixelWorld;
        public System.Func<Rect> BoardLocal;
        public System.Action<List<Vector2>> Bystanders;
        public System.Func<Rect> ScreenRect;
        public System.Func<Vector2> ScoreAnchor;
        public System.Action<Beat> Sounded;
        /// <summary>(dim 0..1, violet 0..1) for the background; (0, 0) hands it back.</summary>
        public System.Action<float, float> Mood;
        /// <summary>The arena's impulse term, in its parent's units; zero hands it back.</summary>
        public System.Action<Vector2> Impulse;

        // ---- the TOTAL's answer, read by the controller's one writer of the score line ----
        public float ScoreScale = 1f;
        public float ScoreClaim;
        public Color ScoreInk = new Color(0.95f, 0.84f, 0.93f);

        private static readonly Color Violet = new Color(0.58f, 0.32f, 1f);
        private static readonly Color Lavender = new Color(0.9f, 0.82f, 1f);
        private static readonly Color HotLavender = new Color(0.93f, 0.88f, 1f);
        private static readonly Color Magenta = new Color(1f, 0.35f, 0.85f);
        private static readonly Color PaleCyan = new Color(0.55f, 0.92f, 1f);
        private static readonly Color VoidDark = new Color(0.05f, 0.01f, 0.10f);
        private static readonly Color Ivory = new Color(1f, 0.96f, 0.88f);

        private const int DimOrder = 8;
        private const int ReflectionOrder = 9;
        private const int LensDarkOrder = 14;
        private const int LensShadeOrder = 15;
        private const int LensRimOrder = 16;
        private const int ShockShadeOrder = 16;
        private const int ShockBandOrder = 17;
        private const int ShockEdgeOrder = 18;
        private const int EchoOrder = 19;
        private const int MatterOrder = 20;
        private const int GhostOrder = 21;
        private const int RingSoftOrder = 22;
        private const int RingOrder = 23;
        private const int CoreHaloOrder = 24;
        private const int CoreDarkOrder = 25;
        private const int FlareOrder = 26;
        private const int FilamentOrder = 27;
        private const int WaveOrder = 28;
        private const int RayOrder = 29;
        private const int ColumnOrder = 30;
        private const int PeakHaloOrder = 31;
        private const int PeakOrder = 32;
        private const int CrownOrder = 33;
        private const int CentralHaloOrder = 34;
        private const int CentralOrder = 35;
        private const int MoteOrder = 36;
        private const int TinyOrder = 38;
        private const int ValueOrder = 42;
        private const int ValueCoreOrder = 43;
        private const int EdgeOrder = 45;
        private const int DebugOrder = 60;

        private const int FilamentSegments = 14;

        private struct Flavor
        {
            public Color Light;
            public Color Fringe;
            public float Twist;
            public float Flare;
            public float Bend;
            public bool HotStreaks;
        }

        private sealed class Cube
        {
            public Target T;
            public Vector2 At;
            public float Stagger;
            public float H1;
            public float H2;
            public float H3;
            public Vector2 GhostDir;
            public float GhostPx;
            public bool Hero;
            public int Degree;
            public float Scale = 1f;
            public float PullPx;
            public Vector4 UvMap;
            public Vector4 UvClamp;
            public Flavor F;
            public SpriteRenderer Matter;
            public SpriteRenderer Ghost;
            public SpriteRenderer Echo;
            public SpriteRenderer CoreDark;
            public SpriteRenderer CoreHalo;
            public SpriteRenderer RingA;
            public SpriteRenderer RingB;
            public SpriteRenderer PeakCore;
            public SpriteRenderer PeakHalo;
            public SpriteRenderer Wave;
            public SpriteRenderer Flare;
            public SpriteRenderer FringeC;
            public SpriteRenderer FringeM;
            public TextMesh Tiny;
        }

        private sealed class Ray
        {
            public MeshRenderer R;
            public Vector2 From;
            public float Angle;
            public float Length;
            public float Width;
            public float Start;
            public float Grow;
            public float Hold;
            public float Retract;
            public float Seed;
            public float Strength;
            public Color Body;
            public Color Core;
            public Color Root;
            public Color FringeA;
            public Color FringeB;
            public Vector4 Shape;
            public bool Hero;
            public int Owner = -1;
        }

        private sealed class Filament
        {
            public int A;
            public int B;
            public float Seed;
            public MeshRenderer R;
            public Mesh M;
            public readonly Vector2[] P = new Vector2[FilamentSegments + 1];
            public readonly Color[] PC = new Color[FilamentSegments + 1];
            public readonly Vector3[] V = new Vector3[(FilamentSegments + 1) * 2];
            public readonly Vector2[] Uv = new Vector2[(FilamentSegments + 1) * 2];
            public readonly Color[] C = new Color[(FilamentSegments + 1) * 2];
            public readonly int[] Tri = new int[FilamentSegments * 6];
        }

        private sealed class Mote
        {
            public SpriteRenderer R;
            public Vector2 From;
            public Vector2 Dir;
            public Vector2 Dest;
            public float Out;
            public float Start;
            public float Turns;
            public float Size;
            public Color A;
            public Color B;
            public bool Streak;
            public Vector2 Last;
        }

        private sealed class Glint
        {
            public SpriteRenderer R;
            public Vector2 At;
            public float PassAt;
        }

        private readonly List<Cube> cubes = new List<Cube>();
        private readonly List<Ray> rays = new List<Ray>();
        private readonly List<Filament> filaments = new List<Filament>();
        private readonly List<Mote> motes = new List<Mote>();
        private readonly List<Glint> glints = new List<Glint>();
        private readonly List<Vector2> bystanderScratch = new List<Vector2>();

        private readonly Stack<SpriteRenderer> spritePool = new Stack<SpriteRenderer>();
        private readonly Stack<MeshRenderer> beamPool = new Stack<MeshRenderer>();
        private readonly Stack<Filament> filamentPool = new Stack<Filament>();
        private readonly List<SpriteRenderer> debugMarks = new List<SpriteRenderer>();
        private int debugUsed;
        private TextMesh debugText;

        private MaterialPropertyBlock block;
        private Transform arena;

        private object preparedKey;
        private object begunKey;
        private Request req;
        private float clock;
        private float preparedAt;
        private bool holdPrepared;
        private float beginAt = float.PositiveInfinity;
        private int said;
        private Vector2 centroid;
        private float reach;
        private Rect boardRect;
        private float cell = 1f;
        private float cubeSize = 1f;
        private float pxL = 0.01f;
        private float pxW = 0.01f;
        private int heroCount;
        private float endAt;
        private bool impulseLive;
        private bool moodLive;
        private float arrivedAt = -1f;

        private SpriteRenderer dimPlate;
        private SpriteRenderer lensDark;
        private SpriteRenderer lensShade;
        private SpriteRenderer lensRim;
        private SpriteRenderer shockEdge;
        private SpriteRenderer shockBand;
        private SpriteRenderer shockShade;
        private SpriteRenderer shockOuter;
        private SpriteRenderer centralCore;
        private SpriteRenderer centralHalo;
        private SpriteRenderer edgeViolet;
        private SpriteRenderer edgeCyan;
        private TextMesh value;
        private TextMesh valueViolet;
        private TextMesh valueLavender;
        private SpriteRenderer valueCore;
        private readonly SpriteRenderer[] trail = new SpriteRenderer[8];
        private Vector2 valueFrom;

        private static Material matterMat;
        private static Material glowMat;
        private static Material beamMat;
        private static Material plainMat;
        private static bool matterTried;
        private static bool glowTried;
        private static bool beamTried;

        private static readonly int PMode = Shader.PropertyToID("_Mode");
        private static readonly int PUvMap = Shader.PropertyToID("_UvMap");
        private static readonly int PUvClamp = Shader.PropertyToID("_UvClamp");
        private static readonly int PPull = Shader.PropertyToID("_Pull");
        private static readonly int PTwist = Shader.PropertyToID("_Twist");
        private static readonly int PDesat = Shader.PropertyToID("_Desat");
        private static readonly int PBright = Shader.PropertyToID("_Bright");
        private static readonly int PMatter = Shader.PropertyToID("_Matter");
        private static readonly int PIntensify = Shader.PropertyToID("_Intensify");
        private static readonly int PSink = Shader.PropertyToID("_Sink");
        private static readonly int PRim = Shader.PropertyToID("_Rim");
        private static readonly int PRimColor = Shader.PropertyToID("_RimColor");
        private static readonly int PEdge = Shader.PropertyToID("_Edge");
        private static readonly int PChroma = Shader.PropertyToID("_Chroma");
        private static readonly int PNoise = Shader.PropertyToID("_Noise");
        private static readonly int PSwim = Shader.PropertyToID("_Swim");
        private static readonly int PDissolve = Shader.PropertyToID("_Dissolve");
        private static readonly int PPx = Shader.PropertyToID("_Px");
        private static readonly int PClock = Shader.PropertyToID("_Clock");
        private static readonly int PAlpha = Shader.PropertyToID("_Alpha");
        private static readonly int PBody = Shader.PropertyToID("_BodyColor");
        private static readonly int PCore = Shader.PropertyToID("_CoreColor");
        private static readonly int PRoot = Shader.PropertyToID("_RootColor");
        private static readonly int PFringeA = Shader.PropertyToID("_FringeA");
        private static readonly int PFringeB = Shader.PropertyToID("_FringeB");
        private static readonly int PParams = Shader.PropertyToID("_Params");
        private static readonly int PShape = Shader.PropertyToID("_Shape");
        private static readonly int PAspect = Shader.PropertyToID("_Aspect");

        // ================================================================== lifecycle

        public void Build()
        {
            block = new MaterialPropertyBlock();
            if (Application.isMobilePlatform && Style.Level == Quality.High)
            {
                Style.Level = Quality.Medium;
            }
            var go = new GameObject("AntimatterArena");
            go.transform.SetParent(transform, false);
            arena = go.transform;
        }

        /// <summary>True once this key has played (or been marked seen): a repaint that hands the
        /// same report back must not raise it again.</summary>
        public bool HasSeen(object key)
        {
            return key == null || ReferenceEquals(key, begunKey) || ReferenceEquals(key, preparedKey);
        }

        /// <summary>A prepared event nobody began is dropped after this long - a repaint whose
        /// explosions were never drawn must not leave proxies standing on the board.</summary>
        private const float PrepareTimeout = 8f;

        /// <summary>The repaint that emptied the cells: raise every target as a proxy of the cube
        /// it was and HOLD it, so nothing blinks out before the event is drawn. The lab passes
        /// <paramref name="hold"/> to keep an unbegun event standing for as long as it likes.</summary>
        public void Prepare(object key, Request request, bool hold = false)
        {
            if (key == null || request == null || request.Targets.Count == 0 || HasSeen(key))
            {
                return;
            }
            Stop();
            preparedKey = key;
            req = request;
            preparedAt = clock;
            holdPrepared = hold;
            beginAt = float.PositiveInfinity;
            said = 0;
            arrivedAt = -1f;
            FollowArena();
            Plan();
            Tick(0f);
        }

        /// <summary>The turn's explosions are being drawn now: the event starts.</summary>
        public void Begin(object key, Request request)
        {
            if (key == null || ReferenceEquals(key, begunKey))
            {
                return;
            }
            if (!ReferenceEquals(key, preparedKey))
            {
                Prepare(key, request);
            }
            if (req == null || !ReferenceEquals(key, preparedKey))
            {
                return;
            }
            begunKey = key;
            beginAt = clock;
            GatherReflections();
            Tick(0f);
        }

        /// <summary>After a reset: this key is not still to come.</summary>
        public void MarkSeen(object key)
        {
            if (key != null)
            {
                begunKey = key;
            }
        }

        public void Forget()
        {
            begunKey = null;
            preparedKey = null;
        }

        /// <summary>True while an event is prepared or playing.</summary>
        public bool Busy
        {
            get { return req != null; }
        }

        /// <summary>Takes everything down at once - a reset, the lab, a new run.</summary>
        public void Stop()
        {
            foreach (Cube c in cubes)
            {
                Return(ref c.Matter); Return(ref c.Ghost); Return(ref c.Echo);
                Return(ref c.CoreDark); Return(ref c.CoreHalo); Return(ref c.RingA); Return(ref c.RingB);
                Return(ref c.PeakCore); Return(ref c.PeakHalo); Return(ref c.Wave); Return(ref c.Flare);
                Return(ref c.FringeC); Return(ref c.FringeM);
                if (c.Tiny != null)
                {
                    Destroy(c.Tiny.gameObject);
                    c.Tiny = null;
                }
            }
            cubes.Clear();
            foreach (Ray r in rays)
            {
                ReturnBeam(r.R);
            }
            rays.Clear();
            foreach (Filament f in filaments)
            {
                f.R.enabled = false;
                filamentPool.Push(f);
            }
            filaments.Clear();
            foreach (Mote m in motes)
            {
                Return(ref m.R);
            }
            motes.Clear();
            foreach (Glint g in glints)
            {
                Return(ref g.R);
            }
            glints.Clear();
            Return(ref dimPlate); Return(ref lensDark); Return(ref lensShade); Return(ref lensRim);
            Return(ref shockEdge); Return(ref shockBand); Return(ref shockShade); Return(ref shockOuter);
            Return(ref centralCore); Return(ref centralHalo); Return(ref edgeViolet); Return(ref edgeCyan);
            Return(ref valueCore);
            for (int i = 0; i < trail.Length; i++)
            {
                Return(ref trail[i]);
            }
            DestroyValue();
            if (impulseLive && Impulse != null)
            {
                Impulse(Vector2.zero);
            }
            impulseLive = false;
            if (moodLive && Mood != null)
            {
                Mood(0f, 0f);
            }
            moodLive = false;
            HideDebug(0);
            if (debugText != null)
            {
                debugText.gameObject.SetActive(false);
            }
            ScoreScale = 1f;
            ScoreClaim = 0f;
            req = null;
            holdPrepared = false;
            preparedKey = null;
            beginAt = float.PositiveInfinity;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void LateUpdate()
        {
            FollowArena();
        }

        /// <summary>The event draws in the BOARD's own space without living under the board: its
        /// root copies the board's transform every frame.</summary>
        private void FollowArena()
        {
            Transform board = Arena != null ? Arena() : null;
            if (board == null || arena == null)
            {
                return;
            }
            arena.position = board.position;
            arena.rotation = board.rotation;
            arena.localScale = board.lossyScale;
        }

        // ================================================================== planning

        private void Plan()
        {
            cell = CellSize != null ? CellSize() : 1f;
            cubeSize = CubeSize != null ? CubeSize() : cell;
            pxW = PixelWorld != null ? PixelWorld() : 0.01f;
            float scale = arena != null ? Mathf.Max(1e-4f, arena.lossyScale.x) : 1f;
            pxL = pxW / scale;
            boardRect = BoardLocal != null ? BoardLocal() : new Rect(-3f, -3f, 6f, 6f);
            Quality q = Style.Level;
            int n = req.Targets.Count;

            // The cubes, their hashes and their faces.
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                Target t = req.Targets[i];
                var c = new Cube { T = t };
                c.At = CellLocal != null ? CellLocal(t.Cell) : Vector2.zero;
                c.H1 = Hash01(req.Seed, t.Cell.X, t.Cell.Y, 1);
                c.H2 = Hash01(req.Seed, t.Cell.X, t.Cell.Y, 2);
                c.H3 = Hash01(req.Seed, t.Cell.X, t.Cell.Y, 3);
                c.Stagger = (c.H1 * 2f - 1f) * Style.Stagger;
                float angle = c.H2 * Mathf.PI * 2f;
                c.GhostDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                c.GhostPx = Mathf.Lerp(Style.GhostOffsetMin, Style.GhostOffsetMax, c.H3);
                c.F = FlavorOf(req.Kind, t.Matter);
                UvOf(t.Tile, out c.UvMap, out c.UvClamp);
                sum += c.At;
                cubes.Add(c);
            }
            centroid = sum / Mathf.Max(1, n);
            reach = 0f;
            Vector2[] corners =
            {
                new Vector2(boardRect.xMin, boardRect.yMin), new Vector2(boardRect.xMax, boardRect.yMin),
                new Vector2(boardRect.xMin, boardRect.yMax), new Vector2(boardRect.xMax, boardRect.yMax)
            };
            foreach (Vector2 corner in corners)
            {
                reach = Mathf.Max(reach, (corner - centroid).magnitude);
            }
            reach += cell * 0.3f;

            ChooseHeroes(q);
            PlanRays(q);
            PlanFilaments(q);
            PlanMotes(q);
            RentEverything(q);

            endAt = Mathf.Max(Style.AfterimageStart + Style.AfterimageDuration + Style.Stagger,
                Style.Peak + Style.Stagger + 0.05f + Style.ParticleBurst + Style.SpiralDuration);
            endAt = Mathf.Max(endAt, Style.ShockwaveStart + Style.ShockwaveDuration + 0.1f);
            endAt = Mathf.Max(endAt, Style.Peak + Style.BackgroundReturn + 0.1f);
            if (HasScore)
            {
                endAt = Mathf.Max(endAt, Style.ScoreAt + Style.ScorePop + Style.RewardHold
                    + Style.ScoreCollect + Style.ScoreTravel + Style.ScoreLand + 0.05f);
            }
        }

        private bool HasScore
        {
            get { return req != null && req.Points != 0 && Layers.Score; }
        }

        /// <summary>Hero cells are spread over the event, never bunched: farthest-point sampling
        /// from the cell furthest from the centroid, so the light comes off the top, the bottom,
        /// the sides and the middle of what went.</summary>
        private void ChooseHeroes(Quality q)
        {
            int n = cubes.Count;
            int want = n <= 3 ? n : n <= 5 ? 3 : n <= 8 ? 4 : n <= 20 ? 5 : 6;
            if (q == Quality.Medium)
            {
                want = Mathf.Min(want, 4);
            }
            else if (q == Quality.Low)
            {
                want = Mathf.Min(n, 5);
            }
            heroCount = 0;
            if (n == 0)
            {
                return;
            }
            int first = 0;
            float far = -1f;
            for (int i = 0; i < n; i++)
            {
                float d = (cubes[i].At - centroid).sqrMagnitude + cubes[i].H1 * 1e-4f;
                if (d > far)
                {
                    far = d;
                    first = i;
                }
            }
            cubes[first].Hero = true;
            heroCount = 1;
            while (heroCount < want)
            {
                int best = -1;
                float bestD = -1f;
                for (int i = 0; i < n; i++)
                {
                    if (cubes[i].Hero)
                    {
                        continue;
                    }
                    float near = float.MaxValue;
                    for (int j = 0; j < n; j++)
                    {
                        if (cubes[j].Hero)
                        {
                            near = Mathf.Min(near, (cubes[i].At - cubes[j].At).sqrMagnitude);
                        }
                    }
                    near += cubes[i].H2 * 1e-4f;
                    if (near > bestD)
                    {
                        bestD = near;
                        best = i;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                cubes[best].Hero = true;
                heroCount++;
            }
        }

        /// <summary>The rays. A few from each hero cell, radial-ish off the centroid with a
        /// deterministic spread, one of them long; short local rays from a few other cells while
        /// the count is small. Capped - twenty cubes never make a hundred beams.</summary>
        private void PlanRays(Quality q)
        {
            int n = cubes.Count;
            int cap = q == Quality.High ? Style.BeamCap : q == Quality.Medium ? 10 : 6;
            // How many rays each hero cell throws.
            int perHero = n == 1 ? 4 : n <= 3 ? 3 : 2;
            if (q == Quality.Low)
            {
                perHero = 1;
            }
            else if (q == Quality.Medium)
            {
                perHero = Mathf.Min(perHero, n == 1 ? 3 : 2);
            }
            for (int i = 0; i < n && rays.Count < cap; i++)
            {
                Cube c = cubes[i];
                if (!c.Hero)
                {
                    continue;
                }
                float baseAngle = BaseAngle(c);
                for (int k = 0; k < perHero && rays.Count < cap; k++)
                {
                    float offset = RayOffset(perHero, k);
                    float jitter = (Hash01(req.Seed, c.T.Cell.X, c.T.Cell.Y, 20 + k) * 2f - 1f)
                        * Style.BeamAngleVariance;
                    float hl = Hash01(req.Seed, c.T.Cell.X, c.T.Cell.Y, 40 + k);
                    bool longOne = k == 0 || (n == 1 && k == 1);
                    float cells;
                    if (q == Quality.Low)
                    {
                        cells = Mathf.Lerp(1.4f, 2.4f, hl);
                    }
                    else if (longOne)
                    {
                        cells = Mathf.Lerp(Mathf.Max(Style.BeamLengthMin, 2f), Style.BeamLengthMax, hl);
                        if (Hash01(req.Seed, c.T.Cell.X, c.T.Cell.Y, 60 + k) < 0.15f)
                        {
                            cells = Mathf.Lerp(Style.BeamLengthMax, Style.BeamLengthRare, hl);
                        }
                    }
                    else
                    {
                        cells = Mathf.Lerp(Style.BeamLengthMin, 1.6f, hl);
                    }
                    rays.Add(MakeRay(c, i, baseAngle + offset + jitter, cells,
                        longOne ? Mathf.Lerp(8f, Style.BeamWidthMax, hl) : Mathf.Lerp(Style.BeamWidthMin, 8f, hl),
                        true, k));
                }
            }
            // Short local rays off a few of the others, while there are few enough to read.
            if (q == Quality.High && n >= 4 && n <= 8)
            {
                for (int i = 0; i < n && rays.Count < cap; i++)
                {
                    Cube c = cubes[i];
                    if (c.Hero || c.H3 > 0.6f)
                    {
                        continue;
                    }
                    float hl = Hash01(req.Seed, c.T.Cell.X, c.T.Cell.Y, 70);
                    rays.Add(MakeRay(c, i, BaseAngle(c) + (c.H2 * 2f - 1f) * 60f,
                        Mathf.Lerp(0.5f, 1.2f, hl), Mathf.Lerp(Style.BeamWidthMin, 7f, hl), false, 0));
                }
            }
        }

        private float BaseAngle(Cube c)
        {
            Vector2 d = c.At - centroid;
            if (d.sqrMagnitude < cell * cell * 0.09f)
            {
                return c.H2 * 360f;
            }
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        /// <summary>How the rays of one cell fan out: the first outward, the rest wide of it -
        /// one up and right, one to the side, one down - so a single cell throws light every way
        /// without being a starburst.</summary>
        private static float RayOffset(int count, int k)
        {
            switch (count)
            {
                case 1: return 0f;
                case 2: return k == 0 ? 0f : 145f;
                case 3: return k == 0 ? 0f : k == 1 ? 128f : -112f;
                default: return k * (360f / count) + (k % 2 == 0 ? 0f : 12f);
            }
        }

        private Ray MakeRay(Cube c, int owner, float degrees, float cells, float corePx, bool hero, int k)
        {
            var r = new Ray
            {
                From = c.At,
                Angle = degrees,
                Length = cells * cell,
                Width = corePx / 0.14f * pxL,
                Start = Style.Peak + c.Stagger + Hash01(req.Seed, c.T.Cell.X, c.T.Cell.Y, 80 + k) * 0.012f,
                Grow = Style.BeamGrow,
                Hold = Style.BeamHold,
                Retract = Style.BeamRetract,
                Seed = Hash01(req.Seed, c.T.Cell.X, c.T.Cell.Y, 90 + k),
                Strength = hero ? 1f : 0.75f,
                Body = Violet,
                Core = HotLavender * Style.BeamCoreBrightness,
                Root = new Color(c.F.Light.r, c.F.Light.g, c.F.Light.b, hero ? 0.3f : 0.45f),
                FringeA = c.F.Fringe,
                FringeB = Magenta,
                Shape = new Vector4(0.14f, hero ? 0.18f : 0.3f, 0.6f, 0.55f),
                Hero = hero,
                Owner = owner
            };
            return r;
        }

        /// <summary>A visual graph only: each cube reaches for its nearest target, a second one now
        /// and then, never more than two, never across the board, and the shortest links win the
        /// global cap. The rules know nothing about it.</summary>
        private void PlanFilaments(Quality q)
        {
            int cap = q == Quality.High ? Style.MaxFilaments : q == Quality.Medium ? 10 : 0;
            int n = cubes.Count;
            if (cap <= 0 || n < 2)
            {
                return;
            }
            float maxLink = cell * 2.6f;
            var edges = new List<KeyValuePair<float, int>>();
            for (int i = 0; i < n; i++)
            {
                // nearest and second nearest
                int a = -1;
                int b = -1;
                float da = float.MaxValue;
                float db = float.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }
                    float d = (cubes[i].At - cubes[j].At).magnitude + cubes[j].H3 * 1e-3f;
                    if (d < da)
                    {
                        b = a;
                        db = da;
                        a = j;
                        da = d;
                    }
                    else if (d < db)
                    {
                        b = j;
                        db = d;
                    }
                }
                if (a >= 0 && da <= maxLink)
                {
                    edges.Add(new KeyValuePair<float, int>(da, Mathf.Min(i, a) * 4096 + Mathf.Max(i, a)));
                }
                if (b >= 0 && db <= maxLink && cubes[i].H1 > 0.5f)
                {
                    edges.Add(new KeyValuePair<float, int>(db, Mathf.Min(i, b) * 4096 + Mathf.Max(i, b)));
                }
            }
            edges.Sort((x, y) => x.Key.CompareTo(y.Key));
            var taken = new HashSet<int>();
            foreach (KeyValuePair<float, int> e in edges)
            {
                if (filaments.Count >= cap || taken.Contains(e.Value))
                {
                    continue;
                }
                int i = e.Value / 4096;
                int j = e.Value % 4096;
                if (cubes[i].Degree >= 2 || cubes[j].Degree >= 2)
                {
                    continue;
                }
                taken.Add(e.Value);
                cubes[i].Degree++;
                cubes[j].Degree++;
                Filament f = filamentPool.Count > 0 ? filamentPool.Pop() : NewFilament();
                f.A = i;
                f.B = j;
                f.Seed = Hash01(req.Seed, i, j, 5);
                filaments.Add(f);
            }
        }

        /// <summary>Spectral motes: many per cube while there are few cubes, one or two when there
        /// are many, and a global cap either way.</summary>
        private void PlanMotes(Quality q)
        {
            int n = cubes.Count;
            if (q == Quality.Low)
            {
                return;
            }
            int per = n <= 3 ? 7 : n <= 8 ? 4 : n <= 20 ? 2 : 1;
            int cap = Style.ParticleCap;
            if (q == Quality.Medium)
            {
                per = Mathf.Max(1, Mathf.RoundToInt(per * 0.6f));
                cap = 30;
            }
            for (int i = 0; i < n; i++)
            {
                Cube c = cubes[i];
                int count = per + (n > 20 && c.Hero ? 1 : 0);
                for (int k = 0; k < count && motes.Count < cap; k++)
                {
                    float h1 = Hash01(req.Seed, c.T.Cell.X, c.T.Cell.Y, 100 + k);
                    float h2 = Hash01(req.Seed, c.T.Cell.X, c.T.Cell.Y, 130 + k);
                    float h3 = Hash01(req.Seed, c.T.Cell.X, c.T.Cell.Y, 160 + k);
                    float angle = h1 * Mathf.PI * 2f;
                    Vector2 outward = c.At - centroid;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    if (outward.sqrMagnitude > 1e-6f && h2 < 0.5f)
                    {
                        dir = (dir + outward.normalized).normalized;
                    }
                    var m = new Mote
                    {
                        From = c.At + dir * cubeSize * 0.12f,
                        Dir = dir,
                        Out = Mathf.Lerp(8f, 20f, h2) * pxL,
                        Start = Style.Peak + c.Stagger + 0.01f + h3 * 0.03f,
                        Turns = Mathf.Lerp(Style.SpiralTurnsMin, Style.SpiralTurnsMax, h1) * (h3 < 0.5f ? 1f : -1f),
                        Streak = h1 > 0.6f,
                        Size = Mathf.Lerp(3f, 5f, h3) * pxL
                    };
                    m.Dest = n > 1 && h2 > 0.6f ? centroid : c.At;
                    float pick = Hash01(req.Seed, c.T.Cell.X, c.T.Cell.Y, 190 + k);
                    if (c.F.HotStreaks && pick < 0.35f)
                    {
                        // Fire: an ultra-hot streak that cools to violet as it goes.
                        m.A = new Color(1f, 0.32f, 0.16f);
                        m.B = Violet;
                        m.Streak = true;
                    }
                    else if (pick < 0.5f)
                    {
                        m.A = c.F.Light;
                        m.B = Color.Lerp(c.F.Light, Lavender, 0.5f);
                    }
                    else if (pick < 0.85f)
                    {
                        m.A = pick < 0.68f ? Violet : Lavender;
                        m.B = m.A;
                    }
                    else
                    {
                        m.A = PaleCyan;
                        m.B = Lavender;
                    }
                    m.Last = m.From;
                    motes.Add(m);
                }
            }
        }

        /// <summary>Everything the event draws, rented now and hidden, so nothing is made
        /// mid-flight.</summary>
        private void RentEverything(Quality q)
        {
            Material matter = MatterMaterial;
            Material glow = GlowMaterial;
            Material plain = PlainMaterial;
            int n = cubes.Count;
            for (int i = 0; i < n; i++)
            {
                Cube c = cubes[i];
                Sprite tile = c.T.Tile != null ? c.T.Tile : ViewUtil.WhiteSprite;
                c.Matter = Rent(tile, matter != null ? matter : TileFallback(tile), MatterOrder, arena);
                c.Matter.enabled = true;
                if (Layers.Ghosts && q != Quality.Low)
                {
                    c.Ghost = Rent(tile, matter != null ? matter : plain, GhostOrder, arena);
                }
                if (Layers.Afterimages)
                {
                    c.Echo = Rent(tile, matter != null ? matter : plain, EchoOrder, arena);
                }
                if (Layers.Cores)
                {
                    c.CoreHalo = Rent(AntimatterShapes.Glow, glow, CoreHaloOrder, arena);
                    c.CoreDark = Rent(AntimatterShapes.Dot, plain, CoreDarkOrder, arena);
                }
                if (Layers.InwardRings)
                {
                    c.RingA = Rent(AntimatterShapes.SquareRing, glow, RingOrder, arena);
                    c.RingB = Rent(AntimatterShapes.SquareSoft, glow, RingSoftOrder, arena);
                }
                if (Layers.PeakCores)
                {
                    c.PeakHalo = Rent(AntimatterShapes.Glow, glow, PeakHaloOrder, arena);
                    c.PeakCore = Rent(AntimatterShapes.Glow, glow, PeakOrder, arena);
                }
                if (Layers.LocalWaves && q != Quality.Low && (n <= 8 || c.Hero))
                {
                    c.Wave = Rent(AntimatterShapes.RingLocal, glow, WaveOrder, arena);
                }
                if (Layers.Implosion && c.F.Flare > 0f)
                {
                    c.Flare = Rent(AntimatterShapes.Glow, glow, FlareOrder, arena);
                }
                if (Layers.Notice)
                {
                    c.FringeC = Rent(AntimatterShapes.SquareRing, glow, RingSoftOrder, arena);
                    c.FringeM = Rent(AntimatterShapes.SquareRing, glow, RingSoftOrder, arena);
                }
                if (HasScore && n >= 2 && n <= 3 && req.PointsPerCube != 0)
                {
                    int each = req.Points < 0 ? -Mathf.Abs(req.PointsPerCube) : req.PointsPerCube;
                    c.Tiny = ViewUtil.MakeText3D(arena, "AntimatterShare", c.At,
                        (each < 0 ? "-" : "+") + Mathf.Abs(each), 90, cell * 0.034f, Lavender,
                        TinyOrder, TextAnchor.MiddleCenter);
                    c.Tiny.gameObject.SetActive(false);
                }
            }
            Material beam = BeamMaterial;
            if (beam != null && Layers.Beams)
            {
                foreach (Ray r in rays)
                {
                    r.R = RentBeam(beam, RayOrder);
                }
            }
            else
            {
                rays.Clear();
            }
            if (beam != null && Layers.Crown && q != Quality.Low)
            {
                AddCrown(q);
            }
            if (beam != null && Layers.Column && q == Quality.High)
            {
                AddColumn();
            }
            if (beam != null && Layers.Filaments)
            {
                foreach (Filament f in filaments)
                {
                    f.R.sharedMaterial = beam;
                    f.R.sortingOrder = FilamentOrder;
                    f.R.transform.SetParent(arena, false);
                    f.R.enabled = false;
                }
            }
            else
            {
                foreach (Filament f in filaments)
                {
                    filamentPool.Push(f);
                }
                filaments.Clear();
            }
            if (Layers.Particles)
            {
                foreach (Mote m in motes)
                {
                    m.R = Rent(m.Streak ? AntimatterShapes.Streak : AntimatterShapes.Glow, glow, MoteOrder, arena);
                }
            }
            else
            {
                motes.Clear();
            }
            if (Layers.BoardDim)
            {
                dimPlate = Rent(ViewUtil.WhiteSprite, plain, DimOrder, arena);
            }
            if (Layers.Lens && q != Quality.Low)
            {
                lensDark = Rent(AntimatterShapes.Dot, plain, LensDarkOrder, arena);
                lensShade = Rent(AntimatterShapes.RingSoft, plain, LensShadeOrder, arena);
                lensRim = Rent(AntimatterShapes.RingThin, glow, LensRimOrder, arena);
            }
            if (Layers.Shockwave)
            {
                shockShade = Rent(AntimatterShapes.RingSoft, plain, ShockShadeOrder, arena);
                shockBand = Rent(AntimatterShapes.RingSoft, glow, ShockBandOrder, arena);
                shockOuter = Rent(AntimatterShapes.RingSoft, glow, ShockBandOrder, arena);
                shockEdge = Rent(AntimatterShapes.RingThin, glow, ShockEdgeOrder, arena);
            }
            if (Layers.CentralCore)
            {
                centralHalo = Rent(AntimatterShapes.Glow, glow, CentralHaloOrder, arena);
                centralCore = Rent(AntimatterShapes.Glow, glow, CentralOrder, arena);
            }
            if (Layers.EdgeFringe && q == Quality.High)
            {
                edgeViolet = Rent(AntimatterShapes.ScreenEdge, glow, EdgeOrder, transform);
                edgeCyan = Rent(AntimatterShapes.ScreenEdge, glow, EdgeOrder, transform);
            }
        }

        private void AddCrown(Quality q)
        {
            int count = q == Quality.Medium ? 6 : 6 + Mathf.FloorToInt(Hash01(req.Seed, 7, 7, 7) * 7f);
            count = Mathf.Clamp(count, 6, 12);
            Material beam = BeamMaterial;
            for (int k = 0; k < count; k++)
            {
                float h = Hash01(req.Seed, k, 11, 300);
                var r = new Ray
                {
                    From = centroid,
                    Angle = k * 360f / count + (h * 2f - 1f) * 12f + Hash01(req.Seed, 3, 3, 301) * 360f,
                    Length = Mathf.Lerp(30f, 100f, Hash01(req.Seed, k, 12, 302)) * pxL,
                    Width = Mathf.Lerp(2.5f, 4f, h) / 0.14f * pxL,
                    Start = Style.Peak,
                    Grow = Style.CrownDuration * 0.22f,
                    Hold = Style.CrownDuration * 0.35f,
                    Retract = Style.CrownDuration * 0.43f,
                    Seed = h,
                    Strength = 0.85f,
                    Body = Color.Lerp(Violet, Lavender, 0.35f),
                    Core = HotLavender * 0.85f,
                    Root = new Color(0f, 0f, 0f, 0f),
                    FringeA = PaleCyan,
                    FringeB = Magenta,
                    Shape = new Vector4(0.16f, 0.08f, 0.3f, 0.3f),
                    R = RentBeam(beam, CrownOrder)
                };
                rays.Add(r);
            }
        }

        /// <summary>A soft vertical light, up and down out of the centroid: the energy leaving
        /// the arena as well. Faint and brief - an anime ultimate is exactly what it must not be.
        /// </summary>
        private void AddColumn()
        {
            Material beam = BeamMaterial;
            for (int k = 0; k < 2; k++)
            {
                var r = new Ray
                {
                    From = centroid,
                    Angle = k == 0 ? 90f : 270f,
                    Length = cell * 14f,
                    Width = Style.ColumnWidth / 0.3f * pxL,
                    Start = Style.Peak,
                    Grow = Style.ColumnDuration * 0.25f,
                    Hold = Style.ColumnDuration * 0.3f,
                    Retract = Style.ColumnDuration * 0.45f,
                    Seed = 0.37f + k * 0.21f,
                    Strength = Style.ColumnAlpha,
                    Body = Lavender,
                    Core = Lavender,
                    Root = new Color(0f, 0f, 0f, 0f),
                    FringeA = PaleCyan,
                    FringeB = Magenta,
                    Shape = new Vector4(0.3f, 0.7f, 0.2f, 1f),
                    R = RentBeam(beam, ColumnOrder)
                };
                rays.Add(r);
            }
        }

        /// <summary>The cubes that STAY, so the shockwave can light their rims as it passes.
        /// Asked at Begin, when the board already shows what the turn left.</summary>
        private void GatherReflections()
        {
            if (!Layers.Reflections || Style.Level == Quality.Low || Bystanders == null || req == null)
            {
                return;
            }
            bystanderScratch.Clear();
            Bystanders(bystanderScratch);
            Material glow = GlowMaterial;
            foreach (Vector2 at in bystanderScratch)
            {
                float d = (at - centroid).magnitude;
                if (d > reach || glints.Count >= 64)
                {
                    continue;
                }
                float k = Mathf.Clamp01(d / reach);
                var g = new Glint
                {
                    At = at,
                    PassAt = Style.ShockwaveStart + Style.ShockwaveDuration * (1f - Mathf.Sqrt(1f - k)),
                    R = Rent(AntimatterShapes.SquareSoft, glow, ReflectionOrder, arena)
                };
                glints.Add(g);
            }
        }

        // ================================================================== the clock

        private void Tick(float dt)
        {
            clock += dt;
            if (req == null)
            {
                return;
            }
            float t = float.IsPositiveInfinity(beginAt) ? -1f : clock - beginAt;
            if (t < 0f && !holdPrepared && clock - preparedAt > PrepareTimeout)
            {
                object seen = preparedKey;
                Stop();
                begunKey = seen;
                return;
            }
            if (Style.HoldAt >= 0f && t > Style.HoldAt)
            {
                t = Style.HoldAt;
                beginAt = clock - t;
            }
            Beats(t);
            PoseCubes(t);
            PoseFilaments(t);
            PoseRays(t);
            PoseGlobal(t);
            PoseMotes(t);
            PoseGlints(t);
            PoseScore(t);
            PoseImpulse(t);
            PoseMood(t);
            PaintDebug(t);
            if (t > endAt && Style.HoldAt < 0f)
            {
                object seen = begunKey;
                Stop();
                begunKey = seen;
            }
        }

        private void Beats(float t)
        {
            if (t < 0f)
            {
                return;
            }
            SayAt(t, 0f, Beat.RealityDistort);
            if (Layers.Ghosts && Style.Level != Quality.Low)
            {
                SayAt(t, Style.GhostStart, Beat.Ghost);
            }
            SayAt(t, Style.ChargeStart - 0.02f, Beat.Charge);
            SayAt(t, Style.CollisionStart + Style.CollisionDuration - 0.01f, Beat.Contact);
            // Early enough that the suck (75 ms) is over ~30 ms before the peak: the hole the
            // peak lands in.
            SayAt(t, Style.ImplosionStart - 0.015f, Beat.Implosion);
            SayAt(t, Style.Peak, Beat.Peak);
            if (rays.Count > 0)
            {
                SayAt(t, Style.Peak + 0.006f, Beat.Beam);
            }
            SayAt(t, Style.ShockwaveStart, Beat.Shockwave);
            SayAt(t, Style.AfterimageStart + 0.03f, Beat.Aftermath);
        }

        private void SayAt(float t, float at, Beat beat)
        {
            int bit = 1 << (int)beat;
            if (t >= at && (said & bit) == 0)
            {
                said |= bit;
                if (Sounded != null)
                {
                    Sounded(beat);
                }
            }
        }

        // ================================================================== per cube

        private void PoseCubes(float t)
        {
            int n = cubes.Count;
            for (int i = 0; i < n; i++)
            {
                Cube c = cubes[i];
                float lt = t < 0f ? -1f : t - c.Stagger;
                PoseMatter(c, lt);
                PoseGhost(c, lt);
                PoseCore(c, lt);
                PoseRing(c, lt);
                PosePeak(c, lt);
                PoseEcho(c, lt);
                PoseTiny(c, lt);
            }
        }

        private void PoseMatter(Cube c, float lt)
        {
            if (c.Matter == null)
            {
                return;
            }
            bool gone = lt >= Style.Peak;
            c.Matter.enabled = !gone;
            if (c.FringeC != null)
            {
                c.FringeC.enabled = false;
                c.FringeM.enabled = false;
            }
            if (c.Flare != null)
            {
                c.Flare.enabled = false;
            }
            if (gone)
            {
                return;
            }
            float noticed = Layers.Notice ? Smooth(Ramp(lt, 0f, Style.Notice)) : 0f;
            float charge = Ramp(lt, Style.ChargeStart, Style.ChargeDuration);
            float implode = Layers.Implosion ? Ramp(lt, Style.ImplosionStart, Style.ImplosionDuration) : 0f;
            float s = 1f - 0.015f * Smooth(Mathf.Clamp01(charge * 2f)) - 0.035f * Smooth(Mathf.Clamp01(charge * 2f - 1f));
            s = Mathf.Lerp(s, Style.ImplosionScale, EaseIn(implode));
            c.Scale = s;
            float pullPx = 0f;
            if (Layers.UvPull)
            {
                pullPx = Style.UvPull * EaseIn(charge);
                if (c.Hero && charge > 0.7f)
                {
                    pullPx += (Style.UvPullHero - Style.UvPull) * (charge - 0.7f) / 0.3f;
                }
            }
            // The implosion drags the edges in hard - its own, not the charge's gentle pull.
            pullPx += 6f * EaseIn(implode);
            c.PullPx = pullPx;
            float intensify = implode < 0.35f ? Smooth(implode / 0.35f) : Mathf.Lerp(1f, 0.4f, Smooth((implode - 0.35f) / 0.65f));
            if (!Layers.Implosion || implode <= 0f)
            {
                intensify = 0f;
            }
            float sink = Smooth(Mathf.Clamp01((implode - 0.35f) / 0.65f));
            float size = cubeSize * s;
            float pxObj = pxL / Mathf.Max(size, 1e-5f);
            float contact = Layers.Contact
                ? Bump(Ramp(lt, Style.CollisionStart + Style.CollisionDuration - 0.01f, Style.ContactDuration))
                : 0f;
            Place(c.Matter, c.At, size);
            c.Matter.color = c.T.Tint;
            if (MatterMaterial == null)
            {
                // No shader: tint and scale, and let the cube go dim and violet toward the end.
                c.Matter.color = Color.Lerp(c.T.Tint, Violet, 0.25f * noticed + 0.5f * implode);
                return;
            }
            c.Matter.GetPropertyBlock(block);
            block.SetFloat(PMode, 0f);
            block.SetVector(PUvMap, c.UvMap);
            block.SetVector(PUvClamp, c.UvClamp);
            block.SetFloat(PPull, pullPx * pxObj);
            block.SetFloat(PTwist, c.F.Twist * (0.25f * charge + 0.75f * implode));
            block.SetFloat(PDesat, 0.08f * noticed - 0.6f * intensify);
            block.SetFloat(PBright, 1f - 0.07f * noticed + c.F.Flare * 0.3f * intensify);
            block.SetColor(PMatter, c.T.Matter);
            block.SetFloat(PIntensify, intensify);
            block.SetFloat(PSink, sink);
            block.SetFloat(PRim, contact);
            block.SetColor(PRimColor, Lavender);
            block.SetFloat(PEdge, 0.3f * noticed * (1f - implode));
            block.SetFloat(PChroma, (noticed * 1f + c.F.Bend * 1.5f * implode) * pxObj);
            block.SetFloat(PNoise, 0f);
            block.SetFloat(PSwim, 0f);
            block.SetFloat(PDissolve, -1f);
            block.SetFloat(PPx, pxObj);
            block.SetFloat(PClock, clock);
            block.SetFloat(PAlpha, 1f);
            c.Matter.SetPropertyBlock(block);

            // Reality noticing, just outside the cube: a pixel of cyan one way and magenta the
            // other, faint, gone once the charge takes over.
            if (c.FringeC != null && noticed > 0f)
            {
                float f = noticed * (1f - Smooth(charge));
                if (f > 0.01f)
                {
                    float fs = cubeSize * 1.02f / AntimatterShapes.RingBand;
                    Vector2 off = new Vector2(1.2f, 0.45f) * pxL;
                    c.FringeC.enabled = true;
                    c.FringeM.enabled = true;
                    Place(c.FringeC, c.At - off, fs);
                    Place(c.FringeM, c.At + off, fs);
                    c.FringeC.color = new Color(PaleCyan.r, PaleCyan.g, PaleCyan.b, 0.22f * f);
                    c.FringeM.color = new Color(Violet.r, Violet.g, Violet.b, 0.3f * f);
                }
            }
            // Gold's brief metallic flare before it goes: its own light, a breath, never white.
            if (c.Flare != null && implode > 0f)
            {
                float fl = Bump(Mathf.Clamp01(implode / 0.6f));
                c.Flare.enabled = fl > 0.01f;
                Place(c.Flare, c.At, cubeSize * 1.25f);
                c.Flare.color = new Color(c.F.Light.r, c.F.Light.g, c.F.Light.b, 0.55f * fl);
            }
        }

        private void PoseGhost(Cube c, float lt)
        {
            if (c.Ghost == null)
            {
                return;
            }
            float born = Ramp(lt, Style.GhostStart, Style.GhostDuration);
            float collide = Ramp(lt, Style.CollisionStart, Style.CollisionDuration);
            float merged = Ramp(lt, Style.CollisionStart + Style.CollisionDuration, 0.04f);
            float alpha = Style.GhostAlpha * Smooth(born) * (1f - merged);
            if (alpha <= 0.002f || lt >= Style.Peak)
            {
                c.Ghost.enabled = false;
                return;
            }
            c.Ghost.enabled = true;
            float offPx = c.GhostPx * Smooth(born) * (1f - EaseIn(collide));
            if (Style.GhostApart > 0f)
            {
                offPx = Style.GhostApart * cell / Mathf.Max(pxL, 1e-6f) * (1f - EaseIn(collide));
            }
            // A half-pixel wander while it waits - only through the buildup.
            offPx += 0.5f * Mathf.Sin(clock * 9f + c.H1 * 6.283f) * (1f - collide) * Smooth(born);
            float size = cubeSize * c.Scale;
            float pxObj = pxL / Mathf.Max(size, 1e-5f);
            Place(c.Ghost, c.At + c.GhostDir * offPx * pxL, size);
            c.Ghost.color = Color.white;
            if (MatterMaterial == null)
            {
                c.Ghost.color = new Color(Violet.r, Violet.g, Violet.b, alpha * 0.6f);
                return;
            }
            c.Ghost.GetPropertyBlock(block);
            block.SetFloat(PMode, 1f);
            block.SetVector(PUvMap, c.UvMap);
            block.SetVector(PUvClamp, c.UvClamp);
            block.SetFloat(PPull, c.PullPx * pxObj);
            block.SetFloat(PTwist, 0f);
            block.SetFloat(PDesat, 0f);
            block.SetFloat(PBright, 1f + 0.3f * collide);
            block.SetColor(PMatter, c.T.Matter);
            block.SetFloat(PIntensify, 0f);
            block.SetFloat(PSink, 0f);
            block.SetFloat(PRim, 0f);
            block.SetFloat(PEdge, 0f);
            block.SetFloat(PChroma, (1f + c.F.Bend) * pxObj);
            block.SetFloat(PNoise, 0.35f);
            block.SetFloat(PSwim, 0f);
            block.SetFloat(PDissolve, -1f);
            block.SetFloat(PPx, pxObj);
            block.SetFloat(PClock, clock);
            block.SetFloat(PAlpha, alpha);
            c.Ghost.SetPropertyBlock(block);
        }

        private void PoseCore(Cube c, float lt)
        {
            if (c.CoreDark == null)
            {
                return;
            }
            float k = Ramp(lt, Style.ChargeStart, Style.Peak - Style.ChargeStart);
            bool on = k > 0f && lt < Style.Peak;
            c.CoreDark.enabled = on;
            c.CoreHalo.enabled = on;
            if (!on)
            {
                return;
            }
            // No pulse: the brightness compresses steadily while the core draws in.
            float appear = Smooth(Mathf.Clamp01(k * 5f));
            float radius = cubeSize * Mathf.Lerp(Style.CoreStartRadius, Style.CoreStartRadius * 0.6f, k);
            Place(c.CoreDark, c.At, radius * 2f);
            c.CoreDark.color = new Color(VoidDark.r, VoidDark.g, VoidDark.b, 0.9f * appear);
            Place(c.CoreHalo, c.At, radius * 5.2f);
            float glow = Mathf.Lerp(0.15f, Style.CoreBrightness, Mathf.Pow(k, 1.5f)) * appear;
            c.CoreHalo.color = new Color(0.86f, 0.45f, 1f, glow);
        }

        private void PoseRing(Cube c, float lt)
        {
            if (c.RingA == null)
            {
                return;
            }
            float r = Ramp(lt, Style.RingStart, Style.RingDuration);
            bool on = r > 0f && r < 1f && lt < Style.Peak;
            c.RingA.enabled = on;
            c.RingB.enabled = on;
            if (!on)
            {
                return;
            }
            // Slow off the edge, then accelerating into the core.
            float e = Mathf.Pow(r, 2.2f);
            float half = Mathf.Lerp(cubeSize * 0.5f, cubeSize * 0.07f, e);
            float a = 0.9f * Mathf.Clamp01(r * 6f) * (1f - Smooth(Mathf.Clamp01((r - 0.85f) / 0.15f)));
            Place(c.RingA, c.At, 2f * half / AntimatterShapes.RingBand);
            c.RingA.color = new Color(c.F.Light.r, c.F.Light.g, c.F.Light.b, a);
            Place(c.RingB, c.At, 2f * half * 1.1f / AntimatterShapes.RingBand);
            c.RingB.color = new Color(Violet.r, Violet.g, Violet.b, 0.6f * a);
        }

        private void PosePeak(Cube c, float lt)
        {
            if (c.PeakCore != null)
            {
                float p = Ramp(lt, Style.Peak - 0.004f, Style.PeakDuration + 0.035f);
                bool on = p > 0f && p < 1f;
                c.PeakCore.enabled = on;
                c.PeakHalo.enabled = on;
                if (on)
                {
                    float flash = p < 0.3f ? Smooth(p / 0.3f) : 1f - Smooth((p - 0.3f) / 0.7f);
                    float hero = c.Hero ? 1.15f : 1f;
                    Place(c.PeakCore, c.At, cubeSize * Mathf.Lerp(0.45f, 0.8f, p) * hero);
                    c.PeakCore.color = new Color(0.9f, 0.83f, 1f, flash);
                    Place(c.PeakHalo, c.At, cubeSize * Mathf.Lerp(1.0f, 1.8f, p) * hero);
                    c.PeakHalo.color = new Color(0.8f, 0.35f, 1f, 0.5f * flash);
                }
            }
            if (c.Wave != null)
            {
                float w = Ramp(lt, Style.Peak, 0.24f);
                bool on = w > 0f && w < 1f;
                c.Wave.enabled = on;
                if (on)
                {
                    float radius = cubeSize * Mathf.Lerp(0.3f, 1.25f, EaseOut(w));
                    Place(c.Wave, c.At, 2f * radius / AntimatterShapes.RingBand);
                    c.Wave.color = new Color(0.8f, 0.7f, 1f, 0.6f * Mathf.Pow(1f - w, 1.5f));
                }
            }
        }

        private void PoseEcho(Cube c, float lt)
        {
            if (c.Echo == null)
            {
                return;
            }
            float e = Ramp(lt, Style.AfterimageStart, Style.AfterimageDuration);
            bool on = e > 0f && e < 1f;
            c.Echo.enabled = on;
            if (!on)
            {
                return;
            }
            float s = e < 0.3f ? Mathf.Lerp(1f, 1.04f, Smooth(e / 0.3f)) : Mathf.Lerp(1.04f, 0.98f, Smooth((e - 0.3f) / 0.7f));
            float size = cubeSize * s;
            float pxObj = pxL / Mathf.Max(size, 1e-5f);
            float alpha = Style.AfterimageAlpha * Smooth(Mathf.Clamp01(e * 6f));
            Place(c.Echo, c.At, size);
            c.Echo.color = c.T.Tint;
            if (MatterMaterial == null)
            {
                Color tint = c.T.Tint;
                c.Echo.color = new Color(1f - tint.r * 0.6f, 1f - tint.g * 0.6f, 1f, alpha * (1f - e));
                return;
            }
            bool simple = Style.Level == Quality.Low;
            c.Echo.GetPropertyBlock(block);
            block.SetFloat(PMode, 2f);
            block.SetVector(PUvMap, c.UvMap);
            block.SetVector(PUvClamp, c.UvClamp);
            block.SetFloat(PPull, 0f);
            block.SetFloat(PTwist, 0f);
            block.SetFloat(PDesat, 0f);
            block.SetFloat(PBright, 1f);
            block.SetColor(PMatter, c.T.Matter);
            block.SetFloat(PIntensify, 0f);
            block.SetFloat(PSink, 0f);
            block.SetFloat(PRim, 0f);
            block.SetFloat(PEdge, 0f);
            block.SetFloat(PChroma, simple ? 0f : 1.2f * pxObj);
            block.SetFloat(PNoise, simple ? 0f : 0.4f);
            block.SetFloat(PSwim, simple ? 0f : 0.6f * pxObj);
            block.SetFloat(PDissolve, Mathf.Lerp(-0.15f, 1.2f, Smooth(Mathf.Clamp01((e - 0.2f) / 0.8f))));
            block.SetFloat(PPx, pxObj);
            block.SetFloat(PClock, clock);
            block.SetFloat(PAlpha, alpha);
            c.Echo.SetPropertyBlock(block);
        }

        private void PoseTiny(Cube c, float lt)
        {
            if (c.Tiny == null)
            {
                return;
            }
            float k = Ramp(lt, Style.Peak + 0.12f, 0.38f);
            bool on = k > 0f && k < 1f;
            c.Tiny.gameObject.SetActive(on);
            if (!on)
            {
                return;
            }
            c.Tiny.transform.localPosition = c.At + new Vector2(0f, cell * (0.1f + 0.25f * EaseOut(k)));
            float a = k < 0.2f ? k / 0.2f : 1f - Smooth((k - 0.55f) / 0.45f);
            ViewUtil.SetTextColor(c.Tiny, new Color(Lavender.r, Lavender.g, Lavender.b, Mathf.Clamp01(a)));
        }

        // ================================================================== the light

        private void PoseFilaments(float t)
        {
            if (filaments.Count == 0)
            {
                return;
            }
            float build = Ramp(t, Style.FilamentStart, Style.FilamentBuild);
            float tension = Ramp(t, Style.FilamentStart, Style.Peak - Style.FilamentStart);
            float snap = Ramp(t, Style.Peak - 0.01f, 0.035f);
            float strength = Style.FilamentAlpha * Smooth(build) * (1f - snap) * (0.45f + 0.55f * Intensity(t));
            float implode = Ramp(t, Style.ImplosionStart, Style.ImplosionDuration);
            float width = Style.FilamentWidth / 0.22f * pxL;
            foreach (Filament f in filaments)
            {
                bool on = t >= 0f && strength > 0.004f;
                f.R.enabled = on;
                if (!on)
                {
                    continue;
                }
                Cube a = cubes[f.A];
                Cube b = cubes[f.B];
                Vector2 d = b.At - a.At;
                float len = d.magnitude;
                if (len < 1e-5f)
                {
                    f.R.enabled = false;
                    continue;
                }
                Vector2 dir = d / len;
                var normal = new Vector2(-dir.y, dir.x);
                Vector2 pa = a.At + dir * cubeSize * 0.36f * a.Scale;
                Vector2 pb = b.At - dir * cubeSize * 0.36f * b.Scale;
                float span = (pb - pa).magnitude;
                float sag = span * 0.10f * Mathf.Pow(1f - tension, 1.5f) * (f.Seed < 0.5f ? 1f : -1f);
                float shiver = implode > 0f && snap <= 0f ? 0.4f * pxL * Mathf.Sin(clock * 90f + f.Seed * 20f) : 0f;
                for (int i = 0; i <= FilamentSegments; i++)
                {
                    float u = i / (float)FilamentSegments;
                    Vector2 p = Vector2.Lerp(pa, pb, u);
                    float bend = sag * 4f * u * (1f - u) + shiver * Mathf.Sin(u * Mathf.PI * 3f)
                        + 0.3f * pxL * Mathf.Sin(u * 6f + clock * 4f + f.Seed * 9f);
                    f.P[i] = p + normal * bend;
                    // Grown from both ends at once, meeting in the middle.
                    float fromEnd = Mathf.Min(u, 1f - u) * 2f;
                    float grown = Mathf.Clamp01((build * 1.15f - fromEnd) / 0.1f);
                    // A few broken sections.
                    float broken = Hash01(req.Seed, f.A * 31 + i, f.B, 7) < 0.18f ? 0.15f : 1f;
                    Color col = Color.Lerp(Lavender, Color.Lerp(Violet, Lavender, 0.3f), 0.5f + 0.5f * Mathf.Sin(u * 7f + f.Seed * 5f));
                    col.a = strength * grown * broken;
                    f.PC[i] = col;
                }
                AntimatterShapes.WriteStrip(f.M, f.P, f.PC, FilamentSegments + 1, width, f.V, f.Uv, f.C, f.Tri);
                f.R.GetPropertyBlock(block);
                block.SetColor(PBody, new Color(0.55f, 0.35f, 1f));
                block.SetColor(PCore, Lavender);
                block.SetColor(PRoot, new Color(0f, 0f, 0f, 0f));
                block.SetColor(PFringeA, PaleCyan);
                block.SetColor(PFringeB, Magenta);
                block.SetVector(PParams, new Vector4(1f, 0f, 1f, f.Seed));
                block.SetVector(PShape, new Vector4(0.22f, 1f, 0.35f, 0.35f));
                block.SetFloat(PAspect, span / Mathf.Max(width, 1e-5f));
                block.SetFloat(PClock, clock);
                f.R.SetPropertyBlock(block);
            }
        }

        private void PoseRays(float t)
        {
            foreach (Ray r in rays)
            {
                if (r.R == null)
                {
                    continue;
                }
                float bt = t - r.Start;
                float total = r.Grow + r.Hold + r.Retract;
                bool on = t >= 0f && bt >= 0f && bt <= total;
                r.R.enabled = on;
                if (!on)
                {
                    continue;
                }
                float grow = bt < r.Grow ? EaseOut(bt / r.Grow) : 1f;
                float retract = bt > r.Grow + r.Hold ? EaseIn((bt - r.Grow - r.Hold) / r.Retract) : 0f;
                Transform tr = r.R.transform;
                tr.localPosition = r.From;
                tr.localRotation = Quaternion.Euler(0f, 0f, r.Angle);
                tr.localScale = new Vector3(r.Length, r.Width, 1f);
                r.R.GetPropertyBlock(block);
                block.SetColor(PBody, r.Body);
                block.SetColor(PCore, r.Core);
                block.SetColor(PRoot, r.Root);
                block.SetColor(PFringeA, r.FringeA);
                block.SetColor(PFringeB, r.FringeB);
                block.SetVector(PParams, new Vector4(grow, retract * 0.98f, r.Strength * (1f - 0.3f * retract), r.Seed));
                block.SetVector(PShape, r.Shape);
                block.SetFloat(PAspect, r.Length / Mathf.Max(r.Width, 1e-5f));
                block.SetFloat(PClock, clock);
                r.R.SetPropertyBlock(block);
            }
        }

        private void PoseGlobal(float t)
        {
            if (dimPlate != null)
            {
                float dim = BoardDimCurve(t) * Style.BoardDim;
                dimPlate.enabled = dim > 0.002f;
                if (dimPlate.enabled)
                {
                    float pad = cell * 0.08f;
                    dimPlate.transform.localPosition = boardRect.center;
                    dimPlate.transform.localScale = new Vector3(boardRect.width + pad * 2f, boardRect.height + pad * 2f, 1f);
                    // The number is perceptual; the board blends in linear colour, where the same
                    // alpha is a much smaller step (the Tılsım trap), so it is converted.
                    dimPlate.color = new Color(0.05f, 0.03f, 0.09f, LinearAlpha(dim));
                }
            }
            float side = Mathf.Max(boardRect.width, boardRect.height);
            if (lensDark != null)
            {
                float l = Ramp(t, Style.Peak, Style.LensDuration);
                bool on = l > 0f && l < 1f;
                lensDark.enabled = on;
                lensShade.enabled = on;
                lensRim.enabled = on;
                if (on)
                {
                    float radius = Mathf.Lerp(side * 0.04f, side * Style.LensRadius, EaseOut(l));
                    float fade = Mathf.Pow(1f - l, 1.3f);
                    Place(lensDark, centroid, radius * 1.84f);
                    lensDark.color = new Color(0.04f, 0.01f, 0.08f, 0.5f * fade);
                    Place(lensShade, centroid, 2f * radius * 0.9f / AntimatterShapes.RingBand);
                    lensShade.color = new Color(0.02f, 0f, 0.05f, 0.35f * fade);
                    Place(lensRim, centroid, 2f * radius / AntimatterShapes.RingBand);
                    lensRim.color = new Color(0.8f, 0.72f, 1f, 0.5f * (1f - l));
                }
            }
            if (shockEdge != null)
            {
                float s = Ramp(t, Style.ShockwaveStart, Style.ShockwaveDuration);
                bool on = s > 0f && s < 1f;
                shockEdge.enabled = on;
                shockBand.enabled = on;
                shockShade.enabled = on;
                shockOuter.enabled = on;
                if (on)
                {
                    float radius = Mathf.Lerp(cell * 0.3f, reach, EaseOut(s));
                    float fade = Mathf.Pow(1f - s, 0.8f);
                    // Three layers: the pressure edge, the refractive band behind it (a pale band
                    // and a dark one just inside - what bent light looks like without a grab
                    // pass), and a faint outer rim.
                    Place(shockEdge, centroid, 2f * radius / AntimatterShapes.RingBand);
                    shockEdge.color = new Color(0.86f, 0.78f, 1f, 0.65f * fade);
                    Place(shockBand, centroid, 2f * radius * 0.93f / AntimatterShapes.RingBand);
                    shockBand.color = new Color(0.62f, 0.55f, 0.95f, 0.16f * fade);
                    Place(shockShade, centroid, 2f * radius * 0.84f / AntimatterShapes.RingBand);
                    shockShade.color = new Color(0.02f, 0f, 0.06f, 0.26f * fade);
                    Place(shockOuter, centroid, 2f * radius * 1.07f / AntimatterShapes.RingBand);
                    shockOuter.color = new Color(0.5f, 0.25f, 0.95f, 0.3f * fade);
                }
            }
            if (centralCore != null)
            {
                float k = Ramp(t, Style.Peak, Style.CentralCoreDuration);
                bool on = k > 0f && k < 1f;
                centralCore.enabled = on;
                centralHalo.enabled = on;
                if (on)
                {
                    float flash = k < 0.2f ? Smooth(k / 0.2f) : 1f - Smooth((k - 0.2f) / 0.8f);
                    float px = Mathf.Lerp(14f, 26f, EaseOut(Mathf.Clamp01(k * 3f)));
                    Place(centralCore, centroid, px * 2f * pxL);
                    centralCore.color = new Color(0.94f, 0.9f, 1f, flash);
                    Place(centralHalo, centroid, px * 6.4f * pxL);
                    centralHalo.color = new Color(0.62f, 0.3f, 1f, 0.55f * flash);
                }
            }
            if (edgeViolet != null)
            {
                float e = Ramp(t, Style.Peak, Style.EdgeDuration);
                bool on = e > 0f && e < 1f && ScreenRect != null;
                edgeViolet.enabled = on;
                edgeCyan.enabled = on;
                if (on)
                {
                    float a = e < 0.15f ? Smooth(e / 0.15f) : 1f - Smooth((e - 0.15f) / 0.85f);
                    Rect screen = ScreenRect();
                    edgeViolet.transform.position = screen.center;
                    edgeViolet.transform.localScale = new Vector3(screen.width, screen.height, 1f);
                    edgeViolet.color = new Color(0.55f, 0.28f, 1f, 0.2f * a);
                    edgeCyan.transform.position = screen.center;
                    edgeCyan.transform.localScale = new Vector3(screen.width * 0.985f, screen.height * 0.975f, 1f);
                    edgeCyan.color = new Color(0.35f, 0.85f, 1f, 0.07f * a);
                }
            }
        }

        private void PoseMotes(float t)
        {
            float life = Style.ParticleBurst + Style.SpiralDuration;
            foreach (Mote m in motes)
            {
                if (m.R == null)
                {
                    continue;
                }
                float mt = t - m.Start;
                bool on = t >= 0f && mt >= 0f && mt <= life;
                m.R.enabled = on;
                if (!on)
                {
                    continue;
                }
                Vector2 burstEnd = m.From + m.Dir * m.Out;
                Vector2 p;
                float fade = 1f;
                if (mt < Style.ParticleBurst)
                {
                    p = m.From + m.Dir * m.Out * EaseOut(mt / Style.ParticleBurst);
                    fade = Mathf.Clamp01(mt / 0.03f);
                }
                else
                {
                    // Curved back and drawn in: the angle turns while the radius closes, faster
                    // and faster, into the core.
                    float k = (mt - Style.ParticleBurst) / Style.SpiralDuration;
                    Vector2 rel = burstEnd - m.Dest;
                    float a0 = Mathf.Atan2(rel.y, rel.x);
                    float angle = a0 + m.Turns * Mathf.PI * 2f * Smooth(k);
                    float radius = rel.magnitude * (1f - EaseIn(k));
                    p = m.Dest + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    fade = k > 0.7f ? 1f - (k - 0.7f) / 0.3f : 1f;
                }
                Color col = Color.Lerp(m.A, m.B, Mathf.Clamp01(mt / life));
                col.a = 0.9f * fade;
                m.R.color = col;
                Transform tr = m.R.transform;
                tr.localPosition = p;
                if (m.Streak)
                {
                    Vector2 v = p - m.Last;
                    float speed = v.magnitude;
                    if (speed > 1e-6f)
                    {
                        tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
                    }
                    float length = Mathf.Clamp(m.Size * 2.2f + speed * 2.5f, m.Size * 2f, m.Size * 6f);
                    tr.localScale = new Vector3(length, length, 1f);
                }
                else
                {
                    tr.localScale = new Vector3(m.Size * 2f, m.Size * 2f, 1f);
                }
                m.Last = p;
            }
        }

        private void PoseGlints(float t)
        {
            foreach (Glint g in glints)
            {
                float k = Ramp(t, g.PassAt - 0.01f, 0.08f);
                bool on = k > 0f && k < 1f;
                g.R.enabled = on;
                if (on)
                {
                    // A lavender edge where the wave's light falls on it - a reflection, never damage.
                    Place(g.R, g.At, cubeSize / AntimatterShapes.RingBand);
                    g.R.color = new Color(0.85f, 0.78f, 1f, 0.09f * Bump(k));
                }
            }
        }

        // ================================================================== the payoff

        private void PoseScore(float t)
        {
            if (!HasScore)
            {
                return;
            }
            float sa = t - Style.ScoreAt;
            float popEnd = Style.ScorePop;
            float holdEnd = popEnd + Style.RewardHold;
            float collectEnd = holdEnd + Style.ScoreCollect;
            float travelEnd = collectEnd + Style.ScoreTravel;
            if (t < 0f || sa < 0f)
            {
                SetValueActive(false);
                return;
            }
            if (value == null)
            {
                BuildValue();
            }
            float scale;
            float alpha = 1f;
            if (sa < popEnd)
            {
                scale = Keys(sa / popEnd, 0.55f, 1.16f, 0.98f, 1f);
            }
            else if (sa < holdEnd)
            {
                scale = 1f;
            }
            else
            {
                float c = Mathf.Clamp01((sa - holdEnd) / Style.ScoreCollect);
                scale = Mathf.Lerp(1f, 0.25f, EaseIn(c));
                alpha = 1f - c;
            }
            bool textOn = sa < collectEnd;
            SetValueActive(textOn);
            if (textOn)
            {
                float glyph = CellWorld() * 0.55f;
                float cs = glyph / 9f;
                SetValueSize(cs * scale);
                ViewUtil.SetTextColor(value, new Color(Ivory.r, Ivory.g, Ivory.b, alpha));
                valueViolet.color = new Color(Violet.r, Violet.g, Violet.b, 0.85f * alpha);
                valueLavender.color = new Color(Lavender.r, Lavender.g, Lavender.b, 0.5f * alpha);
            }
            // The value gathers into a small lavender core and goes to the TOTAL.
            Vector2 target = ScoreAnchor != null ? ScoreAnchor() : valueFrom + Vector2.up * 3f;
            float gather = Mathf.Clamp01((sa - holdEnd) / Style.ScoreCollect);
            float flight = Mathf.Clamp01((sa - collectEnd) / Style.ScoreTravel);
            bool coreOn = sa >= holdEnd && sa <= travelEnd;
            if (valueCore != null)
            {
                valueCore.enabled = coreOn;
                if (coreOn)
                {
                    Vector2 at = Bezier(valueFrom, target, EaseInOut(flight));
                    float size = CellWorld() * Mathf.Lerp(0.2f, 0.34f, gather) * Mathf.Lerp(1f, 0.7f, flight);
                    valueCore.transform.position = at;
                    valueCore.transform.localScale = new Vector3(size, size, 1f);
                    valueCore.color = new Color(0.9f, 0.8f, 1f, 0.95f);
                }
            }
            for (int i = 0; i < trail.Length; i++)
            {
                SpriteRenderer r = trail[i];
                if (r == null)
                {
                    continue;
                }
                bool on = sa >= collectEnd && sa <= travelEnd;
                r.enabled = on;
                if (!on)
                {
                    continue;
                }
                bool spark = i >= 5;
                float back = Mathf.Max(0f, EaseInOut(flight) - (spark ? 0.02f * (i - 4) : 0.035f * (i + 1)));
                Vector2 at = Bezier(valueFrom, target, back);
                float size = CellWorld() * (spark ? 0.07f : Mathf.Lerp(0.2f, 0.08f, i / 5f));
                if (spark)
                {
                    at += new Vector2(Mathf.Sin(clock * 31f + i * 2.1f), Mathf.Cos(clock * 27f + i * 1.3f)) * CellWorld() * 0.08f;
                }
                r.transform.position = at;
                r.transform.localScale = new Vector3(size, size, 1f);
                Color col = spark && cubes.Count > 0 ? cubes[0].F.Light : Lavender;
                r.color = new Color(col.r, col.g, col.b, spark ? 0.8f : 0.55f * (1f - i / 5f));
            }
            if (sa >= travelEnd && arrivedAt < 0f)
            {
                arrivedAt = t;
                if (Sounded != null)
                {
                    Sounded(Beat.Reward);
                }
            }
            if (arrivedAt >= 0f)
            {
                float a = (t - arrivedAt) / Style.ScoreLand;
                if (a <= 1f)
                {
                    ScoreScale = Keys(a, 1f, 1.10f, 0.98f, 1f);
                    ScoreClaim = 1f - a;
                }
                else
                {
                    ScoreScale = 1f;
                    ScoreClaim = 0f;
                }
            }
        }

        private void BuildValue()
        {
            Vector2 at = arena != null ? (Vector2)arena.TransformPoint(centroid) : centroid;
            valueFrom = at + new Vector2(0f, CellWorld() * 0.2f);
            string text = (req.Points < 0 ? "-" : "+") + Mathf.Abs(req.Points);
            value = ViewUtil.MakeText3D(transform, "AntimatterValue", valueFrom, text, 90, 0.05f, Ivory,
                ValueOrder, TextAnchor.MiddleCenter);
            valueViolet = PlainText(text, ValueOrder - 2, new Vector2(2.2f, -2.2f));
            valueLavender = PlainText(text, ValueOrder - 2, new Vector2(-1.6f, 1.6f));
            valueCore = Rent(AntimatterShapes.Glow, GlowMaterial, ValueCoreOrder, transform);
            Material glow = GlowMaterial;
            for (int i = 0; i < trail.Length; i++)
            {
                trail[i] = Rent(AntimatterShapes.Glow, glow, ValueCoreOrder, transform);
            }
        }

        private TextMesh PlainText(string text, int order, Vector2 offsetPx)
        {
            var go = new GameObject("AntimatterValueFringe");
            go.transform.SetParent(value.transform, false);
            go.transform.localPosition = Vector3.zero;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = value.font;
            mesh.fontSize = value.fontSize;
            mesh.characterSize = value.characterSize;
            mesh.anchor = value.anchor;
            mesh.alignment = value.alignment;
            mesh.text = text;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.material = value.font.material;
            renderer.sortingOrder = order;
            fringeOffsets[mesh] = offsetPx;
            return mesh;
        }

        private readonly Dictionary<TextMesh, Vector2> fringeOffsets = new Dictionary<TextMesh, Vector2>();

        private void SetValueSize(float characterSize)
        {
            value.transform.position = valueFrom;
            value.characterSize = characterSize;
            for (int i = 0; i < value.transform.childCount; i++)
            {
                var copy = value.transform.GetChild(i).GetComponent<TextMesh>();
                if (copy == null)
                {
                    continue;
                }
                copy.characterSize = characterSize;
                Vector2 off;
                if (fringeOffsets.TryGetValue(copy, out off))
                {
                    copy.transform.localPosition = off * pxW;
                }
                else
                {
                    // The dark outline ViewUtil laid under it scales with the glyph.
                    Vector3 p = copy.transform.localPosition;
                    float glyph = characterSize * copy.fontSize * 0.1f * 0.06f;
                    copy.transform.localPosition = new Vector3(Mathf.Sign(p.x) * (Mathf.Abs(p.x) > 1e-6f ? glyph : 0f),
                        Mathf.Sign(p.y) * (Mathf.Abs(p.y) > 1e-6f ? glyph : 0f), 0f);
                }
            }
        }

        private void SetValueActive(bool on)
        {
            if (value != null && value.gameObject.activeSelf != on)
            {
                value.gameObject.SetActive(on);
            }
        }

        private void DestroyValue()
        {
            if (value != null)
            {
                fringeOffsets.Clear();
                Destroy(value.gameObject);
                value = null;
                valueViolet = null;
                valueLavender = null;
            }
        }

        private float CellWorld()
        {
            return cell * (arena != null ? arena.lossyScale.x : 1f);
        }

        /// <summary>A cubic lifted straight up off both anchors - built from the two anchors alone,
        /// so it never detours over the board toward some third point.</summary>
        private static Vector2 Bezier(Vector2 a, Vector2 b, float k)
        {
            float lift = Mathf.Max(0.8f, (b - a).magnitude * 0.35f);
            Vector2 c1 = a + new Vector2(0f, lift);
            Vector2 c2 = b + new Vector2(0f, lift * 0.6f);
            float u = 1f - k;
            return u * u * u * a + 3f * u * u * k * c1 + 3f * u * k * k * c2 + k * k * k * b;
        }

        // ================================================================== the arena and the world

        private void PoseImpulse(float t)
        {
            if (!Layers.BoardImpulse || Impulse == null)
            {
                return;
            }
            float u = Ramp(t, Style.Peak + 0.004f, Style.ImpulseDuration);
            if (u > 0f && u < 1f)
            {
                // One pressure impulse and a small settle - never a vibration.
                float shape = u < 0.2f ? EaseOut(u / 0.2f)
                    : u < 0.55f ? Mathf.Lerp(1f, -0.22f, Smooth((u - 0.2f) / 0.35f))
                    : Mathf.Lerp(-0.22f, 0f, Smooth((u - 0.55f) / 0.45f));
                Vector2 away = boardRect.center - centroid;
                Vector2 dir = away.sqrMagnitude > cell * cell * 0.25f ? away.normalized : Vector2.down;
                Impulse(dir * Style.BoardImpulse * pxW * shape);
                impulseLive = true;
            }
            else if (impulseLive)
            {
                Impulse(Vector2.zero);
                impulseLive = false;
            }
        }

        private void PoseMood(float t)
        {
            if (!Layers.BackgroundDim || Mood == null)
            {
                return;
            }
            float dim = BoardDimCurve(t) * Style.BackgroundDim;
            float violet = Bump(Ramp(t, Style.Peak - 0.02f, 0.25f));
            if (dim > 0.002f || violet > 0.002f)
            {
                Mood(dim, violet);
                moodLive = true;
            }
            else if (moodLive)
            {
                Mood(0f, 0f);
                moodLive = false;
            }
        }

        /// <summary>The buildup darkens with the event's energy and lets go after the peak.</summary>
        private static float BoardDimCurve(float t)
        {
            if (t < 0f)
            {
                return 0f;
            }
            if (t < Style.Peak)
            {
                return Intensity(t);
            }
            return 1f - Smooth(Ramp(t, Style.Peak + 0.05f, Style.BackgroundReturn));
        }

        /// <summary>The event's energy curve, knotted so its maximum lands on the peak and falls
        /// away within a few frames of it: a long bright peak kills the impact.</summary>
        public static float Intensity(float t)
        {
            float x = t / Mathf.Max(1e-4f, Style.Peak / 0.82f);
            float[] xs = { 0f, 0.15f, 0.30f, 0.50f, 0.70f, 0.82f, 0.87f, 1.0f };
            float[] ys = { 0f, 0.15f, 0.30f, 0.55f, 0.85f, 1.0f, 0.30f, 0f };
            if (x <= 0f || x >= 1f)
            {
                return 0f;
            }
            for (int i = 1; i < xs.Length; i++)
            {
                if (x <= xs[i])
                {
                    return Mathf.Lerp(ys[i - 1], ys[i], (x - xs[i - 1]) / (xs[i] - xs[i - 1]));
                }
            }
            return 0f;
        }

        // ================================================================== debug

        private static bool DebugAllowed
        {
            get { return Application.isEditor || Debug.isDebugBuild; }
        }

        private void PaintDebug(float t)
        {
            debugUsed = 0;
            if (!DebugAllowed || req == null)
            {
                HideDebug(0);
                return;
            }
            float dot = cubeSize * 0.12f;
            foreach (Cube c in cubes)
            {
                if (Layers.ShowTargets)
                {
                    Mark(c.At, cubeSize / AntimatterShapes.RingBand, AntimatterShapes.SquareRing,
                        new Color(c.T.Matter.r, c.T.Matter.g, c.T.Matter.b, 0.9f));
                }
                if (Layers.ShowGhosts)
                {
                    Vector2 g = c.At + c.GhostDir * c.GhostPx * pxL * 4f;
                    Line(c.At, g, pxL * 1.5f, new Color(0.6f, 0.35f, 1f, 0.9f));
                    Mark(g, dot, AntimatterShapes.Dot, new Color(0.6f, 0.35f, 1f, 0.9f));
                }
                if (Layers.ShowCores)
                {
                    Mark(c.At, dot * 0.8f, AntimatterShapes.Dot, new Color(1f, 0.3f, 0.9f, 0.9f));
                }
                if (Layers.ShowInwardRings)
                {
                    float r = Ramp(t, Style.RingStart + c.Stagger, Style.RingDuration);
                    float half = Mathf.Lerp(cubeSize * 0.5f, cubeSize * 0.07f, Mathf.Pow(r, 2.2f));
                    Mark(c.At, 2f * half / AntimatterShapes.RingBand, AntimatterShapes.SquareRing,
                        new Color(c.F.Light.r, c.F.Light.g, c.F.Light.b, 0.9f));
                }
                if (Layers.ShowImplosionScale)
                {
                    Mark(c.At, cubeSize * c.Scale / AntimatterShapes.RingBand, AntimatterShapes.SquareRing,
                        new Color(1f, 1f, 1f, 0.8f));
                }
                if (Layers.ShowUvPull && c.PullPx > 0f)
                {
                    float inset = c.PullPx * pxL;
                    Mark(c.At, (cubeSize * c.Scale - inset * 2f) / AntimatterShapes.RingBand,
                        AntimatterShapes.SquareRing, new Color(1f, 0.6f, 0.2f, 0.9f));
                }
                if (Layers.ShowAfterimages)
                {
                    Color tint = c.T.Tint;
                    Mark(c.At, cubeSize * 0.9f / AntimatterShapes.RingBand, AntimatterShapes.SquareRing,
                        new Color(1f - tint.r, 1f - tint.g, 1f - tint.b, 0.85f));
                }
                if (Layers.ShowHeroBeamOrigins && c.Hero)
                {
                    Mark(c.At, dot * 1.4f, AntimatterShapes.Dot, new Color(0.2f, 1f, 1f, 0.95f));
                }
            }
            if (Layers.ShowFilaments)
            {
                foreach (Filament f in filaments)
                {
                    Line(cubes[f.A].At, cubes[f.B].At, pxL * 1.5f, new Color(0.7f, 0.4f, 1f, 0.9f));
                }
            }
            if (Layers.ShowBeamDirections)
            {
                foreach (Ray r in rays)
                {
                    float a = r.Angle * Mathf.Deg2Rad;
                    Vector2 end = r.From + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r.Length;
                    Line(r.From, end, pxL * 1.5f, r.Hero ? new Color(0.2f, 1f, 1f, 0.85f) : new Color(0.3f, 0.5f, 1f, 0.85f));
                }
            }
            if (Layers.ShowCentroid)
            {
                Mark(centroid, dot * 1.6f, AntimatterShapes.Dot, new Color(1f, 0.92f, 0.1f, 1f));
            }
            float side = Mathf.Max(boardRect.width, boardRect.height);
            if (Layers.ShowLens)
            {
                Mark(centroid, 2f * side * Style.LensRadius / AntimatterShapes.RingBand, AntimatterShapes.RingThin,
                    new Color(0.7f, 0.4f, 1f, 0.8f));
            }
            if (Layers.ShowShockwave)
            {
                Mark(centroid, 2f * reach / AntimatterShapes.RingBand, AntimatterShapes.RingThin, new Color(1f, 1f, 1f, 0.6f));
                float s = Ramp(t, Style.ShockwaveStart, Style.ShockwaveDuration);
                if (s > 0f && s < 1f)
                {
                    float radius = Mathf.Lerp(cell * 0.3f, reach, EaseOut(s));
                    Mark(centroid, 2f * radius / AntimatterShapes.RingBand, AntimatterShapes.RingThin, new Color(1f, 0.5f, 0.2f, 0.9f));
                }
            }
            if (Layers.ShowSpiralPaths)
            {
                foreach (Mote m in motes)
                {
                    Vector2 end = m.From + m.Dir * m.Out;
                    Mark(m.Dest, dot * 0.7f, AntimatterShapes.Dot, new Color(1f, 0.92f, 0.1f, 0.8f));
                    Vector2 rel = end - m.Dest;
                    float a0 = Mathf.Atan2(rel.y, rel.x);
                    for (int i = 0; i <= 6; i++)
                    {
                        float k = i / 6f;
                        float angle = a0 + m.Turns * Mathf.PI * 2f * Smooth(k);
                        Vector2 p = m.Dest + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rel.magnitude * (1f - EaseIn(k));
                        Mark(p, dot * 0.35f, AntimatterShapes.Dot, new Color(0.9f, 0.9f, 1f, 0.7f));
                    }
                }
            }
            if (Layers.ShowScoreAnchor && arena != null)
            {
                Vector2 from = (Vector2)arena.TransformPoint(centroid) + new Vector2(0f, CellWorld() * 0.2f);
                MarkWorld(from, CellWorld() * 0.14f, new Color(1f, 0.92f, 0.1f, 1f));
                if (ScoreAnchor != null)
                {
                    MarkWorld(ScoreAnchor(), CellWorld() * 0.14f, new Color(0.2f, 1f, 1f, 1f));
                }
            }
            HideDebug(debugUsed);
            bool budget = Layers.ShowBudget || Layers.ShowUvPull || Layers.ShowImplosionScale;
            if (budget)
            {
                if (debugText == null)
                {
                    debugText = ViewUtil.MakeText3D(transform, "AntimatterDebug", Vector2.zero, string.Empty, 60,
                        0.03f, Color.white, DebugOrder, TextAnchor.UpperCenter);
                }
                debugText.gameObject.SetActive(true);
                Cube first = cubes.Count > 0 ? cubes[0] : null;
                int beams = 0;
                foreach (Ray r in rays)
                {
                    if (r.Hero || r.Owner >= 0)
                    {
                        beams++;
                    }
                }
                debugText.text = "targets " + cubes.Count + "  heroes " + heroCount + "  beams " + beams + "/" + Style.BeamCap
                    + "  filaments " + filaments.Count + "/" + Style.MaxFilaments
                    + "\nmotes " + motes.Count + "/" + Style.ParticleCap + "  reflections " + glints.Count
                    + "  quality " + Style.Level + "  t " + (t < 0f ? "-" : t.ToString("0.000"))
                    + (first != null ? "\npull " + first.PullPx.ToString("0.0") + "px  scale " + first.Scale.ToString("0.000") : "");
                Vector2 top = arena != null
                    ? (Vector2)arena.TransformPoint(new Vector2(boardRect.center.x, boardRect.yMin))
                    : Vector2.zero;
                debugText.transform.position = top - new Vector2(0f, CellWorld() * 0.25f);
            }
            else if (debugText != null)
            {
                debugText.gameObject.SetActive(false);
            }
        }

        private void Mark(Vector2 at, float size, Sprite sprite, Color colour)
        {
            SpriteRenderer r = NextMark(sprite, arena);
            r.transform.localPosition = at;
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = new Vector3(size, size, 1f);
            r.color = colour;
        }

        private void MarkWorld(Vector2 at, float size, Color colour)
        {
            SpriteRenderer r = NextMark(AntimatterShapes.Dot, transform);
            r.transform.position = at;
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = new Vector3(size, size, 1f);
            r.color = colour;
        }

        private void Line(Vector2 a, Vector2 b, float width, Color colour)
        {
            SpriteRenderer r = NextMark(ViewUtil.WhiteSprite, arena);
            Vector2 d = b - a;
            r.transform.localPosition = (a + b) * 0.5f;
            r.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(d.magnitude, width, 1f);
            r.color = colour;
        }

        private SpriteRenderer NextMark(Sprite sprite, Transform parent)
        {
            SpriteRenderer r;
            if (debugUsed < debugMarks.Count)
            {
                r = debugMarks[debugUsed];
            }
            else
            {
                var go = new GameObject("AntimatterDebug");
                r = go.AddComponent<SpriteRenderer>();
                r.sharedMaterial = PlainMaterial;
                r.sortingOrder = DebugOrder;
                debugMarks.Add(r);
            }
            if (r.transform.parent != parent)
            {
                r.transform.SetParent(parent, false);
            }
            r.sprite = sprite;
            r.enabled = true;
            debugUsed++;
            return r;
        }

        private void HideDebug(int from)
        {
            for (int i = from; i < debugMarks.Count; i++)
            {
                debugMarks[i].enabled = false;
            }
        }

        // ================================================================== pools and helpers

        private SpriteRenderer Rent(Sprite sprite, Material material, int order, Transform parent)
        {
            SpriteRenderer r = null;
            while (spritePool.Count > 0 && r == null)
            {
                r = spritePool.Pop();
            }
            if (r == null)
            {
                var go = new GameObject("Antimatter");
                r = go.AddComponent<SpriteRenderer>();
            }
            r.transform.SetParent(parent != null ? parent : transform, false);
            r.transform.localRotation = Quaternion.identity;
            r.sprite = sprite;
            r.sharedMaterial = material != null ? material : PlainMaterial;
            r.sortingOrder = order;
            r.color = Color.white;
            r.SetPropertyBlock(null);
            r.enabled = false;
            return r;
        }

        private void Return(ref SpriteRenderer r)
        {
            if (r != null)
            {
                r.enabled = false;
                r.SetPropertyBlock(null);
                spritePool.Push(r);
                r = null;
            }
        }

        private MeshRenderer RentBeam(Material material, int order)
        {
            MeshRenderer r = null;
            while (beamPool.Count > 0 && r == null)
            {
                r = beamPool.Pop();
            }
            if (r == null)
            {
                var go = new GameObject("AntimatterRay");
                go.AddComponent<MeshFilter>().sharedMesh = AntimatterShapes.BeamQuad;
                r = go.AddComponent<MeshRenderer>();
            }
            r.transform.SetParent(arena, false);
            r.sharedMaterial = material;
            r.sortingOrder = order;
            r.SetPropertyBlock(null);
            r.enabled = false;
            return r;
        }

        private void ReturnBeam(MeshRenderer r)
        {
            if (r != null)
            {
                r.enabled = false;
                beamPool.Push(r);
            }
        }

        private Filament NewFilament()
        {
            var f = new Filament();
            var go = new GameObject("AntimatterFilament");
            go.transform.SetParent(arena, false);
            f.M = new Mesh { name = "AntimatterFilament" };
            f.M.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = f.M;
            f.R = go.AddComponent<MeshRenderer>();
            f.R.enabled = false;
            return f;
        }

        private static void Place(SpriteRenderer r, Vector2 at, float size)
        {
            r.transform.localPosition = at;
            r.transform.localScale = new Vector3(size, size, 1f);
        }

        private static Flavor FlavorOf(CubeKind kind, Color matter)
        {
            var f = new Flavor
            {
                Light = LightOf(matter),
                Fringe = new Color(0.45f, 0.9f, 1f),
                Twist = 0f,
                Flare = 0f,
                Bend = 0f,
                HotStreaks = false
            };
            // A small identity each, never a different animation: the antimatter's language wins.
            switch (kind)
            {
                case CubeKind.Fire:
                    f.HotStreaks = true;
                    f.Light = new Color(1f, 0.42f, 0.2f);
                    break;
                case CubeKind.Water:
                    f.Twist = 0.9f;
                    f.Fringe = new Color(0.35f, 0.95f, 0.95f);
                    f.Light = new Color(0.35f, 0.85f, 1f);
                    break;
                case CubeKind.Gold:
                    f.Flare = 1f;
                    f.Light = new Color(1f, 0.82f, 0.32f);
                    break;
                case CubeKind.Obsidian:
                    f.Bend = 1f;
                    f.Light = new Color(0.55f, 0.36f, 0.95f);
                    break;
            }
            return f;
        }

        /// <summary>A material colour brought up to a colour LIGHT can be: the hue kept, the
        /// brightest channel lifted to 0.95. Obsidian's near-black would otherwise be no light at
        /// all.</summary>
        private static Color LightOf(Color c)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (m < 0.001f)
            {
                return Violet;
            }
            float k = 0.95f / m;
            return new Color(Mathf.Min(1f, c.r * k), Mathf.Min(1f, c.g * k), Mathf.Min(1f, c.b * k));
        }

        /// <summary>Where the tile's own pixels are, for the shader's pull: the uv at the body's
        /// centre, the uv per object unit, and the tile's uv rectangle.</summary>
        private static void UvOf(Sprite s, out Vector4 map, out Vector4 clamp)
        {
            map = new Vector4(0.5f, 0.5f, 1f, 1f);
            clamp = new Vector4(0f, 0f, 1f, 1f);
            if (s == null || s.texture == null)
            {
                return;
            }
            float w = s.texture.width;
            float h = s.texture.height;
            Rect r;
            Vector2 off;
            try
            {
                r = s.textureRect;
                off = s.textureRectOffset;
            }
            catch (System.Exception)
            {
                r = s.rect;
                off = Vector2.zero;
            }
            float cx = r.x - off.x + s.pivot.x;
            float cy = r.y - off.y + s.pivot.y;
            map = new Vector4(cx / w, cy / h, s.pixelsPerUnit / w, s.pixelsPerUnit / h);
            clamp = new Vector4(r.xMin / w, r.yMin / h, r.xMax / w, r.yMax / h);
        }

        private static Material TileFallback(Sprite tile)
        {
            Material m = ViewUtil.TileMaterial(tile);
            return m != null ? m : PlainMaterial;
        }

        private static Material MatterMaterial
        {
            get { return Load("AntimatterMatter", ref matterTried, ref matterMat); }
        }

        private static Material GlowMaterial
        {
            get
            {
                Material m = Load("AntimatterGlow", ref glowTried, ref glowMat);
                return m != null ? m : PlainMaterial;
            }
        }

        private static Material BeamMaterial
        {
            get { return Load("AntimatterBeam", ref beamTried, ref beamMat); }
        }

        private static Material PlainMaterial
        {
            get
            {
                if (plainMat == null)
                {
                    plainMat = new Material(Shader.Find("Sprites/Default"));
                }
                return plainMat;
            }
        }

        private static Material Load(string name, ref bool tried, ref Material slot)
        {
            if (slot != null || tried)
            {
                return slot;
            }
            tried = true;
            Shader shader = Shader.Find("ProjectBlock/" + name);
            if (shader == null)
            {
                shader = Resources.Load<Shader>("Shaders/" + name);
            }
            if (shader != null && shader.isSupported)
            {
                slot = new Material(shader);
            }
            return slot;
        }

        private static float Hash01(uint seed, int a, int b, int salt)
        {
            unchecked
            {
                uint h = seed ^ (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                h *= 0x846ca68bu;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static float Ramp(float t, float start, float duration)
        {
            return duration <= 0f ? (t >= start ? 1f : 0f) : Mathf.Clamp01((t - start) / duration);
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static float EaseIn(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x;
        }

        private static float EaseOut(float x)
        {
            x = Mathf.Clamp01(x);
            return 1f - (1f - x) * (1f - x);
        }

        private static float EaseInOut(float x)
        {
            return Smooth(x);
        }

        private static float Bump(float x)
        {
            return x <= 0f || x >= 1f ? 0f : Mathf.Sin(x * Mathf.PI);
        }

        /// <summary>Four keys at 0, 0.4, 0.75 and 1, eased between: a pop and its settle.</summary>
        private static float Keys(float x, float a, float b, float c, float d)
        {
            x = Mathf.Clamp01(x);
            if (x < 0.4f)
            {
                return Mathf.Lerp(a, b, EaseOut(x / 0.4f));
            }
            if (x < 0.75f)
            {
                return Mathf.Lerp(b, c, Smooth((x - 0.4f) / 0.35f));
            }
            return Mathf.Lerp(c, d, Smooth((x - 0.75f) / 0.25f));
        }

        /// <summary>A perceptual darkening turned into the alpha that produces it when the board
        /// blends in linear colour.</summary>
        private static float LinearAlpha(float a)
        {
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(a), 2.2f);
        }
    }
}
