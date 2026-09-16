// PURPOSE: "Kara Delik" - SINGULARITY / EVENT HORIZON. The black hole as a living physical anomaly on the
// board, and everything it does: bending the cubes around it, pulling, swallowing, filling up, the
// critical collapse, the rogue gravity that devours a card pile, and refusing to be moved.
//
// THREE LAYERS OR IT DOES NOT READ AS A HOLE: an EVENT HORIZON (no light in it at all, a hair of bent
// light on its edge), an ACCRETION DISK (dark, cold, flowing, uneven, two bands at two speeds, a warm
// highlight only where it is densest), and a GRAVITATIONAL DISTORTION FIELD - the cubes in the hole's
// ring 1 and ring 2 are drawn stretched toward it, squeezed across it, dragged a pixel or two and
// darkened on the near side (Resources/Shaders/BlackHoleMatter, written THROUGH the board's own cell
// renderers the way "Buzluk" writes ice). Ring 3 is untouched - the reach is Core's
// (KaraDelikJoker.InfluenceAt), never a radius invented here. Rendering only: no cell, hitbox or input
// moves. The hole itself is one procedural quad (Resources/Shaders/BlackHole). Without the shaders the
// board keeps drawing the void tile and nothing here runs.
//
// WHAT HAPPENED IS CORE'S (KaraDelikJoker.LastTurn / LastDeckSwallow / GameBoard.AnchorRefusals).
// The repaint after a turn has already emptied the bitten cells, moved the pulled cubes and wiped the
// collapse, so that repaint raises proxies and holds the pull destinations (Prepare); the gravity
// plays from PlayExplosionFeedback, after the turn's own lines (Begin):
//   GRIP (disk quickens, the targets lean in) -> PULL (the real from -> to, accelerating, a pixel of
//   overshoot) and SWALLOW (lock, a short spiral, spaghettification - long toward the hole, thin
//   across it, drained and darkened last - and the part past the horizon is simply not drawn; then a
//   mass pulse in the disk, never an outward burst) -> COLLAPSE when the count filled the arena:
//   saturation, a breath of silence, a thin dark-to-amber break running out over the board, every
//   remaining cube compressing and then bursting in its OWN colour, the disk back to idle.
// The DEVOUR is its own story: the disk goes unstable, a dark bent corridor opens to the chosen pile
// (and only that pile), the stack leans and thins, cards peel off it and are spaghettified into the
// horizon, and the slot is left as a sunken lens residue for as long as it stays empty this round.
//
// Pooled; on the scaled clock (the lab's time scale slows all of it); announced through Sounded.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class BlackHoleView : MonoBehaviour
    {
        public static class Style
        {
            // ---- the hole
            public static float Span = 2.4f;                 // quad, in cells
            public static float CoreRadius = 0.18f;          // cells (36% of a cell across)
            public static float DiskSpeed = 28f;             // deg / s, inner band
            public static float OuterRatio = 0.75f;          // outer band speed
            public static float BreathAmount = 0.015f;
            public static float BreathRate = 0.35f;          // Hz
            public static float VariationMin = 2f;
            public static float VariationMax = 4f;
            public static float VariationAmount = 0.07f;
            public static float MoteMin = 2f;
            public static float MoteMax = 5f;
            public static float MoteLife = 0.95f;

            // ---- placement
            public static float PinchEnd = 0.08f;
            public static float OpenFrom = 0.08f;
            public static float OpenEnd = 0.22f;
            public static float IgniteFrom = 0.15f;
            public static float IgniteEnd = 0.32f;
            public static float LensFrom = 0.25f;
            public static float LensEnd = 0.40f;

            // ---- lensing (pixels at the reference screen, scaled per frame)
            public static float Ring1Shift = 2.2f;
            public static float Ring1Radial = 1.045f;
            public static float Ring1Tangent = 0.98f;
            public static float Ring1Bend = 1.6f;
            public static float Ring1Drag = 0.022f;
            public static float Ring1Dark = 0.12f;
            public static float Ring2Shift = 1.0f;
            public static float Ring2Radial = 1.018f;
            public static float Ring2Tangent = 0.993f;
            public static float Ring2Bend = 0.5f;
            public static float Ring2Drag = 0.008f;
            public static float Ring2Dark = 0.045f;

            // ---- a turn
            public static float Grip = 0.10f;
            public static float GripSpeed = 1.25f;
            public static float GripBright = 0.13f;
            public static float PreDragRing1 = 2.0f;         // px
            public static float PreDragRing2 = 0.9f;
            public static float PullOne = 0.21f;
            public static float PullTwo = 0.31f;
            public static float PullOvershoot = 1.5f;        // px
            public static float PullSettle = 0.065f;
            public static float SwallowLock = 0.095f;
            public static float SwallowLockPx = 3f;
            public static float Swallow = 0.50f;             // whole, per cube
            public static float SwallowStagger = 0.05f;
            public static float SwallowSpan = 0.32f;         // the stagger is squeezed into this
            public static float EntryJitter = 28f;           // deg
            public static float PulseLength = 0.10f;
            public static float PulseBright = 0.20f;
            public static float PulseSpeed = 1.4f;
            public static float TickDelay = 0.07f;
            public static int PerBlockTickMax = 3;

            // ---- the collapse
            public static float Saturation = 0.15f;
            public static float SaturationSpeed = 2.5f;
            public static float SaturationBright = 0.35f;
            public static float SaturationScale = 0.78f;
            public static float Silence = 0.09f;
            public static float TugPx = 2f;
            public static float Wave = 0.28f;
            public static float Compress = 0.05f;
            public static float Burst = 0.32f;
            public static float Recover = 0.8f;
            public static int FragmentsMin = 3;
            public static int FragmentsMax = 6;
            public static float WaveAlpha = 0.20f;

            // ---- the devour
            public static float RogueWarning = 0.15f;
            public static float RogueSpeed = 1.65f;
            public static float CorridorFrom = 0.10f;
            public static float CorridorAlpha = 0.07f;
            public static float CorridorWidthPx = 22f;
            public static float CompressFrom = 0.15f;
            public static float CompressEnd = 0.35f;
            public static float PeelFrom = 0.30f;
            public static float PeelStagger = 0.055f;
            public static float CardFlight = 0.45f;
            public static int ProxyCardsMin = 3;
            public static int ProxyCardsMax = 6;
            public static float ResidueIn = 0.25f;
            public static float ResidueOut = 0.22f;

            // ---- refusal
            public static float Blocked = 0.20f;

            public static Color Ivory = new Color(1f, 0.93f, 0.82f);
            public static Color ScoreEdge = new Color(0.32f, 0.20f, 0.42f);
            public static Color MoteColour = new Color(0.46f, 0.42f, 0.66f);
            public static Color WaveDark = new Color(0.05f, 0.03f, 0.09f);
            public static Color WaveAmber = new Color(0.86f, 0.62f, 0.34f);
            public static Color Residue = new Color(0.05f, 0.05f, 0.08f);
            public static Color ResidueRing = new Color(0.30f, 0.26f, 0.48f);
        }

        public static class Layers
        {
            public static bool ShowHorizon = true;
            public static bool ShowDisk = true;
            public static bool ShowLensing = true;
            public static bool ShowMass = true;
            public static bool ShowMotes = true;
            public static bool ShowGrip = true;
            public static bool ShowPull = true;
            public static bool ShowSwallow = true;
            public static bool ShowSpaghetti = true;
            public static bool ShowOcclusion = true;
            public static bool ShowScore = true;
            public static bool ShowCollapse = true;
            public static bool ShowDevour = true;
            public static bool ShowCorridor = true;
            public static bool ShowResidue = true;
            public static bool ShowBlocked = true;

            // debug
            public static bool ShowInfluence;
            public static bool ShowPaths;
            public static bool ShowHorizonMask;
            public static bool ShowMassDebug;
            public static bool ShowTimeline;

            public static void AllOn()
            {
                ShowHorizon = ShowDisk = ShowLensing = ShowMass = ShowMotes = true;
                ShowGrip = ShowPull = ShowSwallow = ShowSpaghetti = ShowOcclusion = true;
                ShowScore = ShowCollapse = ShowDevour = ShowCorridor = ShowResidue = ShowBlocked = true;
                ShowInfluence = ShowPaths = ShowHorizonMask = ShowMassDebug = ShowTimeline = false;
            }
        }

        public static System.Action<string> Sounded;
        public const string SoundSpawn = "karadelik.spawn";
        public const string SoundGrip = "karadelik.grip";
        public const string SoundSwallow = "karadelik.swallow";
        public const string SoundSaturate = "karadelik.saturate";
        public const string SoundBreak = "karadelik.break";
        public const string SoundRogue = "karadelik.rogue";
        public const string SoundDevour = "karadelik.devour";
        public const string SoundHold = "karadelik.hold";

        public delegate bool FaceOf(GridPos cell, Cube cube, out Sprite tile, out Color colour);

        /// <summary>World units per screen pixel.</summary>
        public System.Func<float> Pixel;
        public FaceOf Face;

        private const int HoleOrder = 3;
        private const int ProxyOrder = 4;
        private const int FragmentOrder = 5;
        private const int MoteOrder = 4;
        private const int WaveOrder = 3;
        private const int TextOrder = 30;
        private const int DebugOrder = 90;
        private const int PileOrder = 50;

        private static Material holeMaterial;
        private static Material matterMaterial;
        private static bool holeMissing;
        private static bool matterMissing;

        private static readonly int SpanId = Shader.PropertyToID("_Span");
        private static readonly int ClockId = Shader.PropertyToID("_Clock");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int CoreRId = Shader.PropertyToID("_CoreR");
        private static readonly int OpenId = Shader.PropertyToID("_Open");
        private static readonly int PinchId = Shader.PropertyToID("_Pinch");
        private static readonly int IgniteId = Shader.PropertyToID("_Ignite");
        private static readonly int AngleId = Shader.PropertyToID("_Angle");
        private static readonly int Angle2Id = Shader.PropertyToID("_Angle2");
        private static readonly int BrightId = Shader.PropertyToID("_Bright");
        private static readonly int DiskScaleId = Shader.PropertyToID("_DiskScale");
        private static readonly int SilenceId = Shader.PropertyToID("_Silence");
        private static readonly int RimId = Shader.PropertyToID("_Rim");
        private static readonly int WarmId = Shader.PropertyToID("_Warm");
        private static readonly int LensDarkId = Shader.PropertyToID("_LensDark");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int DebugId = Shader.PropertyToID("_Debug");

        private static readonly int PivotId = Shader.PropertyToID("_Pivot");
        private static readonly int DirId = Shader.PropertyToID("_Dir");
        private static readonly int RadialId = Shader.PropertyToID("_Radial");
        private static readonly int TangentId = Shader.PropertyToID("_Tangent");
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");
        private static readonly int EdgeBendId = Shader.PropertyToID("_EdgeBend");
        private static readonly int UVDragId = Shader.PropertyToID("_UVDrag");
        private static readonly int DarkenId = Shader.PropertyToID("_Darken");
        private static readonly int DesatId = Shader.PropertyToID("_Desat");
        private static readonly int HoleId = Shader.PropertyToID("_Hole");
        private static readonly int HoleSoftId = Shader.PropertyToID("_HoleSoft");
        private static readonly int OccludeId = Shader.PropertyToID("_Occlude");
        private static readonly int StreakId = Shader.PropertyToID("_Streak");
        private static readonly string[] WarpNames =
        {
            "_WarpAmp", "_WarpFreq", "_WarpSpeed", "_WarpDrift", "_EdgeHold", "_EdgeSoft", "_Swirl"
        };
        private static readonly int[] WarpIds =
        {
            Shader.PropertyToID("_WarpAmp"), Shader.PropertyToID("_WarpFreq"),
            Shader.PropertyToID("_WarpSpeed"), Shader.PropertyToID("_WarpDrift"),
            Shader.PropertyToID("_EdgeHold"), Shader.PropertyToID("_EdgeSoft"),
            Shader.PropertyToID("_Swirl")
        };

        /// <summary>True when both shaders are there - the board then leaves hole cells to this view.</summary>
        public static bool Available
        {
            get { return HoleMaterial != null && MatterMaterial != null; }
        }

        private static Material HoleMaterial
        {
            get
            {
                if (holeMaterial == null && !holeMissing)
                {
                    Shader shader = Shader.Find("ProjectBlock/BlackHole");
                    if (shader == null)
                    {
                        shader = Resources.Load<Shader>("Shaders/BlackHole");
                    }
                    if (shader == null)
                    {
                        holeMissing = true;
                        Debug.LogWarning("[block_bonk] BlackHole shader missing - holes stay void tiles");
                    }
                    else
                    {
                        holeMaterial = new Material(shader);
                    }
                }
                return holeMaterial;
            }
        }

        private static Material MatterMaterial
        {
            get
            {
                if (matterMaterial == null && !matterMissing)
                {
                    Shader shader = Shader.Find("ProjectBlock/BlackHoleMatter");
                    if (shader == null)
                    {
                        shader = Resources.Load<Shader>("Shaders/BlackHoleMatter");
                    }
                    if (shader == null)
                    {
                        matterMissing = true;
                        Debug.LogWarning("[block_bonk] BlackHoleMatter shader missing - no lensing, no spaghettification");
                    }
                    else
                    {
                        matterMaterial = new Material(shader);
                    }
                }
                return matterMaterial;
            }
        }

        // ================================================================ state

        private sealed class Hole
        {
            public GridPos Cell;
            public bool Transient;          // a devour's own horizon when no hole stands on the board
            public Vector2 TransientWorld;
            public float TransientEnd;
            public SpriteRenderer Quad;
            public float Seed;
            public float Angle;
            public float Angle2;
            public float Born = float.NegativeInfinity;
            public float NextVariation;
            public float VariationFrom;
            public float VariationTo;
            public float VariationAt;
            public float NextMote;
            public float GripAt = -99f;
            public float SatAt = -99f;
            public float BreakAt = -99f;
            public float RogueAt = -99f;
            public float BigPulseAt = -99f;
            public float BlockedAt = -99f;
            public readonly List<float> Pulses = new List<float>();
            public float LensScale;         // this frame, 0..1+
        }

        private enum ProxyKind { Bite, Placement, Pull, Collapse, Card }

        private sealed class Proxy
        {
            public ProxyKind Kind;
            public SpriteRenderer R;
            public SpriteRenderer Inner;     // a card's face panel
            public GridPos Cell;
            public Hole Hole;
            public Cube Cube;
            public Sprite Tile;
            public Color Colour;
            public float[] Warp;
            public float Start;
            public float Duration;
            public Vector2 From;             // local (board) or world (card)
            public Vector2 To;
            public int Ring;
            public float Theta0;
            public float Radius0;
            public float Turns;
            public float Spin;
            public float Size;               // local cube size / card height (world)
            public Vector2 CardSize;
            public bool Absorbed;
            public bool Ticked;
            public float HitAt = -1f;
            public bool Burst;
            public Vector2 Control;          // card bezier control (world)
            public float Lean;
        }

        private sealed class Bit
        {
            public SpriteRenderer R;
            public bool World;
            public float Born;
            public float Life;
            public Vector2 From;
            public Vector2 To;
            public float Size;
            public float EndSize;
            public float Spin;
            public Color Colour;
            public float Alpha;
            public Vector2 Orbit;            // x = start angle, y = turns (motes), 0 = straight
            public Vector2 Centre;
        }

        private sealed class Tick
        {
            public TextMesh Text;
            public TextMesh Shadow;
            public float Born;
            public float Life;
            public Vector2 At;
            public int Value;
            public bool Rolling;
        }

        private sealed class Residue
        {
            public bool DrawPile;
            public Vector2 World;
            public float Scale;
            public SpriteRenderer Inset;
            public SpriteRenderer Ring;
            public SpriteRenderer Core;
            public float Target;             // 0 or 1
            public float Amount;
        }

        private BoardView owner;
        private GameBoard seenModel;
        private float clock;
        private readonly Dictionary<GridPos, Hole> holes = new Dictionary<GridPos, Hole>();
        private readonly List<Hole> transients = new List<Hole>();
        private readonly List<GridPos> holeCells = new List<GridPos>();
        private readonly Dictionary<GridPos, SpriteRenderer> lensed = new Dictionary<GridPos, SpriteRenderer>();
        private readonly List<GridPos> lensGone = new List<GridPos>();
        private readonly List<Proxy> proxies = new List<Proxy>();
        private readonly List<Bit> bits = new List<Bit>();
        private readonly List<Tick> ticks = new List<Tick>();
        private readonly List<GridPos> held = new List<GridPos>();
        private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        private readonly Stack<TextMesh> textPool = new Stack<TextMesh>();
        private readonly Dictionary<bool, Residue> residues = new Dictionary<bool, Residue>();
        private readonly List<SpriteRenderer> debugMarks = new List<SpriteRenderer>();
        private MaterialPropertyBlock block;
        private Transform worldRoot;

        private int massSwallowed;
        private int massGoal = 1;
        private float shownMass;             // swallowed count being displayed
        private float massPulseAt = -99f;
        private bool massHeld;               // an event is animating the count itself

        private BlackHoleVisuals prepared;
        private BlackHoleVisuals begun;
        private DeckSwallowVisuals devourPrepared;
        private DeckSwallowVisuals devourBegun;
        private float collapseFrom = -1f;
        private Tick rolling;
        private int rollingValue;
        private int anchorGeneration = -1;
        private int anchorSeen;
        private float turnEnd;

        public bool Busy
        {
            get { return proxies.Count > 0 || bits.Count > 0; }
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            var root = new GameObject("BlackHoleWorld");
            worldRoot = root.transform;
        }

        private void OnDestroy()
        {
            if (worldRoot != null)
            {
                Destroy(worldRoot.gameObject);
            }
        }

        // ================================================================ presence

        /// <summary>The count the mass layer shows and what fills it, from the joker. Held while an
        /// event is animating the count itself.</summary>
        public void SetMass(int swallowed, int goal)
        {
            massSwallowed = Mathf.Max(0, swallowed);
            massGoal = Mathf.Max(1, goal);
            if (!massHeld)
            {
                shownMass = Mathf.MoveTowards(shownMass, massSwallowed, massGoal);
            }
        }

        /// <summary>Follows the board after a repaint: the holes standing on it, and the lensing on
        /// every cube inside their reach. Called by BoardView at the end of Refresh.</summary>
        public void Sync(BoardView board)
        {
            owner = board;
            GameBoard model = board != null ? board.Board : null;
            if (model == null || !Available)
            {
                return;
            }
            bool fresh = !ReferenceEquals(model, seenModel);
            seenModel = model;
            holeCells.Clear();
            holeCells.AddRange(model.CellsOfKind(CubeKind.Void));

            // holes that are gone (a new board) and holes that are new
            lensGone.Clear();
            foreach (KeyValuePair<GridPos, Hole> e in holes)
            {
                if (!holeCells.Contains(e.Key))
                {
                    lensGone.Add(e.Key);
                }
            }
            foreach (GridPos cell in lensGone)
            {
                Return(holes[cell].Quad);
                holes.Remove(cell);
            }
            foreach (GridPos cell in holeCells)
            {
                if (!holes.ContainsKey(cell))
                {
                    Hole h = MakeHole(cell);
                    // A hole that was already there when this board appeared (a load, a rebuilt
                    // arena) is simply there; one that appears on a board we have been watching
                    // has just been laid.
                    h.Born = fresh ? float.NegativeInfinity : clock;
                    if (!fresh)
                    {
                        Say(SoundSpawn);
                    }
                    holes[cell] = h;
                }
            }

            // the anchor refusals since we last looked
            AnchorRefusalLog log = model.AnchorRefusals;
            if (fresh || log.Generation != anchorGeneration)
            {
                anchorGeneration = log.Generation;
                anchorSeen = fresh ? log.Count : 0;
            }
            for (int i = anchorSeen; i < log.Count; i++)
            {
                AnchorRefusal refusal = log.Refusals[i];
                Hole h;
                if (holes.TryGetValue(refusal.Cell, out h) && clock - h.BlockedAt > Style.Blocked * 0.6f)
                {
                    h.BlockedAt = clock;
                    Say(SoundHold);
                }
            }
            anchorSeen = log.Count;

            PaintLensing();
        }

        /// <summary>Puts every lensed cube back on its own material and takes the holes down.</summary>
        public void Stop()
        {
            StopEvents();
            ClearLensing();
            foreach (KeyValuePair<GridPos, Hole> e in holes)
            {
                Return(e.Value.Quad);
            }
            holes.Clear();
            seenModel = null;
            foreach (KeyValuePair<bool, Residue> e in residues)
            {
                ReturnResidue(e.Value);
            }
            residues.Clear();
        }

        /// <summary>Ends every event (proxies, bits, texts, holds) but keeps the holes and lensing.</summary>
        public void StopEvents()
        {
            if (held.Count > 0 && owner != null)
            {
                owner.ReleaseCells(new List<GridPos>(held));
            }
            held.Clear();
            foreach (Proxy p in proxies)
            {
                Return(p.R);
                Return(p.Inner);
            }
            proxies.Clear();
            foreach (Bit b in bits)
            {
                Return(b.R);
            }
            bits.Clear();
            foreach (Tick t in ticks)
            {
                ReturnText(t.Text);
                ReturnText(t.Shadow);
            }
            ticks.Clear();
            rolling = null;
            foreach (Hole h in transients)
            {
                Return(h.Quad);
            }
            transients.Clear();
            foreach (KeyValuePair<GridPos, Hole> e in holes)
            {
                e.Value.Pulses.Clear();
                e.Value.GripAt = e.Value.SatAt = e.Value.BreakAt = -99f;
                e.Value.RogueAt = e.Value.BigPulseAt = e.Value.BlockedAt = -99f;
            }
            massHeld = false;
            shownMass = massSwallowed;
            collapseFrom = -1f;
            HideDebug();
        }

        /// <summary>Forget what has been played, so the lab can play the same report again.</summary>
        public void Forget()
        {
            prepared = begun = null;
            devourPrepared = devourBegun = null;
        }

        /// <summary>Marks reports as already played (a RESET must not replay the last turn).</summary>
        public void MarkPlayed(BlackHoleVisuals turn, DeckSwallowVisuals devour)
        {
            if (turn != null)
            {
                prepared = begun = turn;
            }
            if (devour != null)
            {
                devourPrepared = devourBegun = devour;
            }
        }

        private Hole MakeHole(GridPos cell)
        {
            var h = new Hole
            {
                Cell = cell,
                Seed = (Hash(cell) % 1000) / 97f,
                Angle = (Hash(cell) % 628) / 100f,
            };
            h.Angle2 = h.Angle * 1.7f;
            h.Quad = Rent(transform, HoleOrder);
            h.Quad.sprite = BlackHoleShapes.Quad;
            h.Quad.sharedMaterial = HoleMaterial;
            h.NextVariation = clock + Random.Range(Style.VariationMin, Style.VariationMax);
            h.NextMote = clock + Random.Range(Style.MoteMin, Style.MoteMax);
            h.VariationFrom = h.VariationTo = 0f;
            return h;
        }

        // ================================================================ lensing

        private void PaintLensing()
        {
            if (owner == null || owner.Board == null)
            {
                return;
            }
            GameBoard model = owner.Board;
            lensGone.Clear();
            foreach (KeyValuePair<GridPos, SpriteRenderer> e in lensed)
            {
                lensGone.Add(e.Key);
            }
            if (Layers.ShowLensing && holeCells.Count > 0 && !owner.IsDark)
            {
                foreach (GridPos hole in holeCells)
                {
                    for (int dy = -KaraDelikJoker.Reach; dy <= KaraDelikJoker.Reach; dy++)
                    {
                        for (int dx = -KaraDelikJoker.Reach; dx <= KaraDelikJoker.Reach; dx++)
                        {
                            var cell = new GridPos(hole.X + dx, hole.Y + dy);
                            GridPos nearest;
                            int ring = KaraDelikJoker.InfluenceAt(holeCells, cell, out nearest);
                            if (ring == 0 || !nearest.Equals(hole))
                            {
                                continue;
                            }
                            Cube? cube = model.IsInside(cell) ? model.GetCube(cell) : null;
                            if (!cube.HasValue || cube.Value.Kind == CubeKind.Ice
                                || CubeRules.IsAnchored(cube.Value) || owner.IsHeld(cell))
                            {
                                continue;
                            }
                            SpriteRenderer r = owner.CellRendererAt(cell);
                            if (r == null || r.sprite == null)
                            {
                                continue;
                            }
                            lensGone.Remove(cell);
                            if (!lensed.ContainsKey(cell))
                            {
                                lensed[cell] = r;
                            }
                            LensOne(r, cell, ring, holes.ContainsKey(hole) ? holes[hole] : null);
                        }
                    }
                }
            }
            foreach (GridPos cell in lensGone)
            {
                ClearLens(cell);
                lensed.Remove(cell);
            }
        }

        private void LensOne(SpriteRenderer r, GridPos cell, int ring, Hole hole)
        {
            float amount = hole != null ? hole.LensScale : 1f;
            Material own = ViewUtil.TileMaterial(r.sprite);
            if (r.sharedMaterial != MatterMaterial)
            {
                r.sharedMaterial = MatterMaterial;
            }
            r.GetPropertyBlock(block);
            CopyWarp(own, block);
            Vector2 pivot = r.transform.position;
            Vector2 holeWorld = transform.TransformPoint(owner.CellToWorld(hole != null ? hole.Cell : cell));
            Vector2 dir = holeWorld - pivot;
            dir = dir.sqrMagnitude > 0.000001f ? dir.normalized : Vector2.right;
            float px = PixelWorld();
            float halfSize = owner.CubeWorldSize * transform.lossyScale.x * 0.5f;
            bool one = ring == 1;
            float shift = (one ? Style.Ring1Shift : Style.Ring2Shift) * px * amount;
            float radial = 1f + ((one ? Style.Ring1Radial : Style.Ring2Radial) - 1f) * amount;
            float tangent = 1f - (1f - (one ? Style.Ring1Tangent : Style.Ring2Tangent)) * amount;
            // The idle is not still: the field breathes with the disk, very slightly.
            float breathe = 1f + 0.12f * Mathf.Sin(clock * 1.3f + (Hash(cell) % 100) * 0.06f);
            block.SetVector(PivotId, new Vector4(pivot.x, pivot.y, halfSize, 0f));
            block.SetVector(DirId, new Vector4(dir.x, dir.y, 0f, 0f));
            block.SetFloat(RadialId, radial);
            block.SetFloat(TangentId, tangent);
            block.SetVector(OffsetId, dir * shift * breathe);
            block.SetFloat(EdgeBendId, (one ? Style.Ring1Bend : Style.Ring2Bend) * px * amount);
            block.SetFloat(UVDragId, (one ? Style.Ring1Drag : Style.Ring2Drag) * amount);
            block.SetFloat(DarkenId, (one ? Style.Ring1Dark : Style.Ring2Dark) * amount);
            block.SetFloat(DesatId, 0f);
            block.SetFloat(OccludeId, 0f);
            block.SetFloat(StreakId, 0f);
            r.SetPropertyBlock(block);
            if (Layers.ShowInfluence)
            {
                r.color = one ? new Color(1f, 0.55f, 0.55f) : new Color(1f, 0.9f, 0.5f);
            }
        }

        private void ClearLens(GridPos cell)
        {
            SpriteRenderer r;
            if (!lensed.TryGetValue(cell, out r) || r == null)
            {
                return;
            }
            r.SetPropertyBlock(null);
            Material own = ViewUtil.TileMaterial(r.sprite);
            r.sharedMaterial = own != null ? own : ViewUtil.PlainSpriteMaterial;
        }

        private void ClearLensing()
        {
            foreach (KeyValuePair<GridPos, SpriteRenderer> e in lensed)
            {
                ClearLens(e.Key);
            }
            lensed.Clear();
        }

        private static void CopyWarp(Material own, MaterialPropertyBlock into)
        {
            for (int i = 0; i < WarpIds.Length; i++)
            {
                float v = 0f;
                if (own != null && own.HasProperty(WarpIds[i]))
                {
                    v = own.GetFloat(WarpIds[i]);
                }
                else if (i == 1)
                {
                    v = 6f;
                }
                else if (i == 5)
                {
                    v = 0.16f;
                }
                else if (i == 4)
                {
                    v = 1f;
                }
                into.SetFloat(WarpIds[i], v);
            }
        }

        private static float[] WarpOf(Sprite tile)
        {
            Material own = ViewUtil.TileMaterial(tile);
            var w = new float[WarpIds.Length];
            for (int i = 0; i < WarpIds.Length; i++)
            {
                w[i] = own != null && own.HasProperty(WarpIds[i]) ? own.GetFloat(WarpIds[i])
                    : i == 1 ? 6f : i == 5 ? 0.16f : i == 4 ? 1f : 0f;
            }
            return w;
        }

        // ================================================================ a turn

        /// <summary>The repaint's half: raise what the gravity took, hold where it pulled to.</summary>
        public void Prepare(BlackHoleVisuals report)
        {
            if (report == null || ReferenceEquals(report, prepared) || owner == null || !Available)
            {
                return;
            }
            StopTurnProxies();
            prepared = report;
            massHeld = true;
            shownMass = report.SwallowedBefore;
            float cube = owner.CubeWorldSize;
            foreach (DestroyedCube c in report.PlacementSwallows)
            {
                Proxy p = MakeCubeProxy(ProxyKind.Placement, c.Pos, c.Cube, cube);
                p.Hole = HoleAt(c.Pos);
            }
            foreach (BlackHoleBite bite in report.Bites)
            {
                Proxy p = MakeCubeProxy(ProxyKind.Bite, bite.Cube.Pos, bite.Cube.Cube, cube);
                p.Hole = HoleAt(bite.Hole);
                p.Ring = 1;
            }
            foreach (BlackHolePull pull in report.Pulls)
            {
                Proxy p = MakeCubeProxy(ProxyKind.Pull, pull.From, pull.Cube, cube);
                p.Hole = HoleAt(pull.Hole);
                p.To = owner.CellToWorld(pull.To);
                p.Ring = 2;
                held.Add(pull.To);
            }
            foreach (DestroyedCube c in report.CollapseCubes)
            {
                Proxy p = MakeCubeProxy(ProxyKind.Collapse, c.Pos, c.Cube, cube);
                p.Hole = NearestHole(owner.CellToWorld(c.Pos));
            }
            if (held.Count > 0)
            {
                owner.HoldCells(held);
            }
            // Until Begin, everything stands where the board last showed it.
            foreach (Proxy p in proxies)
            {
                if (p.Kind != ProxyKind.Card)
                {
                    p.Start = float.MaxValue;
                    PaintCubeProxy(p, p.From, 1f, 1f, 0f, 0f, 0f, false, 0f, 0f);
                }
            }
        }

        /// <summary>The turn's half: after <paramref name="delay"/> (the turn's own lines), the grip,
        /// the pulls, the swallows and, if it came, the collapse.</summary>
        public void Begin(BlackHoleVisuals report, float delay)
        {
            if (report == null || ReferenceEquals(report, begun) || !Available)
            {
                return;
            }
            if (!ReferenceEquals(report, prepared))
            {
                Prepare(report);
            }
            begun = report;
            float t0 = clock + Mathf.Max(0f, delay);
            float gripEnd = t0 + (Layers.ShowGrip ? Style.Grip : 0f);
            foreach (KeyValuePair<GridPos, Hole> e in holes)
            {
                if (report.Bites.Count + report.Pulls.Count + report.PlacementSwallows.Count > 0)
                {
                    e.Value.GripAt = t0;
                }
            }
            if (report.SwallowedThisTurn + report.Pulls.Count > 0)
            {
                Say(SoundGrip);
            }

            // swallows: staggered, in the order of their entry angle, squeezed into the span
            var swallows = new List<Proxy>();
            foreach (Proxy p in proxies)
            {
                if (p.Kind == ProxyKind.Bite || p.Kind == ProxyKind.Placement)
                {
                    swallows.Add(p);
                }
            }
            swallows.Sort((a, b) => AngleOf(a).CompareTo(AngleOf(b)));
            float stagger = swallows.Count > 1
                ? Mathf.Min(Style.SwallowStagger, Style.SwallowSpan / (swallows.Count - 1)) : 0f;
            float lastAbsorb = gripEnd;
            for (int i = 0; i < swallows.Count; i++)
            {
                Proxy p = swallows[i];
                p.Start = gripEnd + i * stagger;
                p.Duration = Style.Swallow;
                SetupSpiral(p, i);
                lastAbsorb = Mathf.Max(lastAbsorb, p.Start + p.Duration * 0.88f);
            }
            foreach (Proxy p in proxies)
            {
                if (p.Kind == ProxyKind.Pull)
                {
                    p.Start = gripEnd + 0.02f;
                    float cells = (p.To - p.From).magnitude / Mathf.Max(owner.CellWorldSize, 0.0001f);
                    p.Duration = cells > 1.2f ? Style.PullTwo : Style.PullOne;
                    lastAbsorb = Mathf.Max(lastAbsorb, p.Start + p.Duration);
                }
            }

            turnEnd = lastAbsorb;
            if (report.Collapsed && Layers.ShowCollapse)
            {
                collapseFrom = lastAbsorb + 0.05f;
                ScheduleCollapse(report, collapseFrom);
            }
            else
            {
                foreach (Proxy p in proxies)
                {
                    if (p.Kind == ProxyKind.Collapse)
                    {
                        p.Start = clock;
                        p.HitAt = clock;
                    }
                }
            }
        }

        private void ScheduleCollapse(BlackHoleVisuals report, float from)
        {
            float breakAt = from + Style.Saturation + Style.Silence;
            foreach (KeyValuePair<GridPos, Hole> e in holes)
            {
                e.Value.SatAt = from;
                e.Value.BreakAt = breakAt;
            }
            // the wave: board-space, from every hole, reaching each cube at its own distance
            float maxDist = 0.001f;
            foreach (Proxy p in proxies)
            {
                if (p.Kind == ProxyKind.Collapse && p.Hole != null)
                {
                    maxDist = Mathf.Max(maxDist, Vector2.Distance(p.From, HoleLocal(p.Hole)));
                }
            }
            foreach (Proxy p in proxies)
            {
                if (p.Kind != ProxyKind.Collapse)
                {
                    continue;
                }
                p.Start = from;
                float d = p.Hole != null ? Vector2.Distance(p.From, HoleLocal(p.Hole)) : maxDist;
                // distance BUCKETS, not a per-cell queue: a wavefront
                float bucket = Mathf.Round(d / Mathf.Max(owner.CellWorldSize, 0.0001f));
                float span = Mathf.Max(1f, Mathf.Round(maxDist / Mathf.Max(owner.CellWorldSize, 0.0001f)));
                p.HitAt = breakAt + Style.Wave * (bucket / span);
            }
            foreach (KeyValuePair<GridPos, Hole> e in holes)
            {
                SpawnWave(e.Value, breakAt, maxDist);
            }
            turnEnd = breakAt + Style.Wave + Style.Burst;
        }

        private float AngleOf(Proxy p)
        {
            if (p.Hole == null)
            {
                return 0f;
            }
            Vector2 d = p.From - HoleLocal(p.Hole);
            return Mathf.Atan2(d.y, d.x);
        }

        private void SetupSpiral(Proxy p, int index)
        {
            Vector2 centre = p.Hole != null ? HoleLocal(p.Hole) : p.From;
            Vector2 d = p.From - centre;
            if (d.sqrMagnitude < 0.0001f)
            {
                // A cube that landed ON the hole enters from its cell's edge, at an angle of its own.
                float a = (Hash(p.Cell) % 628) / 100f + index * 1.9f;
                d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * owner.CellWorldSize * 0.42f;
                p.From = centre + d;
            }
            p.Radius0 = d.magnitude;
            p.Theta0 = Mathf.Atan2(d.y, d.x);
            float jitter = ((Hash(p.Cell) + (uint)index * 7919u) % 1000) / 1000f * 2f - 1f;
            p.Theta0 += jitter * Style.EntryJitter * Mathf.Deg2Rad * 0.15f;
            float cells = p.Radius0 / Mathf.Max(owner.CellWorldSize, 0.0001f);
            p.Turns = Mathf.Lerp(0.35f, 0.70f, Mathf.Clamp01((cells - 0.4f) / 1.2f))
                + jitter * 0.05f;
            p.Spin = Mathf.Lerp(60f, 180f, Mathf.Clamp01(cells / 1.5f)) * (jitter >= 0f ? 1f : 0.85f);
        }

        private Proxy MakeCubeProxy(ProxyKind kind, GridPos cell, Cube cube, float size)
        {
            var p = new Proxy { Kind = kind, Cell = cell, Cube = cube, Size = size };
            Sprite tile;
            Color colour;
            if (Face == null || !Face(cell, cube, out tile, out colour))
            {
                tile = ViewUtil.CubeTile(cube.Kind);
                colour = Color.white;
            }
            p.Tile = tile;
            p.Colour = colour;
            p.Warp = WarpOf(tile);
            p.From = owner.CellToWorld(cell);
            p.To = p.From;
            p.R = Rent(transform, kind == ProxyKind.Collapse ? ProxyOrder : ProxyOrder + 1);
            p.R.sprite = tile;
            p.R.sharedMaterial = MatterMaterial;
            p.R.color = colour;
            p.R.enabled = true;
            proxies.Add(p);
            return p;
        }

        private void StopTurnProxies()
        {
            if (held.Count > 0 && owner != null)
            {
                owner.ReleaseCells(new List<GridPos>(held));
            }
            held.Clear();
            for (int i = proxies.Count - 1; i >= 0; i--)
            {
                if (proxies[i].Kind != ProxyKind.Card)
                {
                    Return(proxies[i].R);
                    Return(proxies[i].Inner);
                    proxies.RemoveAt(i);
                }
            }
        }

        // ================================================================ the devour

        /// <summary>
        /// The gravity lost control and swallowed a pile. <paramref name="pileWorld"/> and
        /// <paramref name="pileScale"/> are the pile's own place and size; <paramref name="cardColour"/>
        /// what its cards look like from where the player sits (a back, or the discard's faces).
        /// </summary>
        public void Devour(DeckSwallowVisuals report, Vector2 pileWorld, float pileScale,
            Color cardColour, Color innerColour, float delay)
        {
            if (report == null || ReferenceEquals(report, devourBegun) || !Available || !Layers.ShowDevour)
            {
                return;
            }
            devourPrepared = devourBegun = report;
            float t0 = clock + Mathf.Max(0f, delay);
            Hole hole = NearestHoleWorld(pileWorld);
            Vector2 holeWorld;
            if (hole == null)
            {
                // No hole on the board: the rogue gravity opens a horizon of its own at the board's
                // edge nearest the pile, for the length of the event.
                holeWorld = BoardEdgeToward(pileWorld);
                hole = MakeHole(new GridPos(int.MinValue, int.MinValue));
                hole.Transient = true;
                hole.TransientWorld = holeWorld;
                hole.Born = t0 - Style.OpenEnd;
                hole.TransientEnd = t0 + 1.35f;
                hole.Quad.transform.SetParent(worldRoot, false);
                transients.Add(hole);
            }
            else
            {
                holeWorld = transform.TransformPoint(HoleLocal(hole));
            }
            hole.RogueAt = t0;
            hole.BigPulseAt = t0 + Style.PeelFrom + Style.CardFlight + 0.2f;
            Say(SoundRogue);

            Vector2 cardSize = new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight) * pileScale;
            int count = report.CardIds.Count;
            int n = Mathf.Clamp(count, Mathf.Min(count, Style.ProxyCardsMin), Style.ProxyCardsMax);
            Vector2 toHole = holeWorld - pileWorld;
            Vector2 side = new Vector2(-toHole.y, toHole.x).normalized;
            float bend = toHole.magnitude * 0.18f * (side.y >= 0f ? 1f : -1f);
            Vector2 control = (pileWorld + holeWorld) * 0.5f + side * bend;
            float lastEnd = t0 + Style.PeelFrom;
            for (int i = 0; i < n; i++)
            {
                var p = new Proxy
                {
                    Kind = ProxyKind.Card,
                    Hole = hole,
                    From = pileWorld + new Vector2(i, i) * 0.035f * pileScale,
                    To = holeWorld,
                    Control = control,
                    CardSize = cardSize,
                    Size = cardSize.y,
                    Start = t0 + Style.PeelFrom + (n - 1 - i) * Style.PeelStagger,
                    Duration = Style.CardFlight,
                    Lean = 0f,
                    Colour = cardColour
                };
                p.R = Rent(worldRoot, PileOrder + i * 2);
                p.R.sprite = ViewUtil.RoundedSprite;
                p.R.sharedMaterial = MatterMaterial;
                p.R.color = cardColour;
                p.Inner = Rent(worldRoot, PileOrder + i * 2 + 1);
                p.Inner.sprite = ViewUtil.RoundedSprite;
                p.Inner.sharedMaterial = MatterMaterial;
                p.Inner.color = innerColour;
                p.Warp = WarpOf(null);
                p.Tile = null;
                p.Cell = new GridPos(i, 0);
                proxies.Add(p);
                lastEnd = Mathf.Max(lastEnd, p.Start + p.Duration);
                p.R.enabled = p.Inner.enabled = true;
            }
            // the tail: two or three short card-coloured streaks behind the last card
            for (int k = 0; k < 3 && n > 0; k++)
            {
                float born = lastEnd - Style.CardFlight * 0.55f + k * 0.03f;
                var b = new Bit
                {
                    World = true,
                    Born = born,
                    Life = Style.CardFlight * 0.5f,
                    From = pileWorld + side * (k - 1) * cardSize.x * 0.15f,
                    To = holeWorld,
                    Centre = control,
                    Size = cardSize.x * 0.10f,
                    EndSize = cardSize.x * 0.03f,
                    Colour = Color.Lerp(cardColour, Color.white, 0.2f),
                    Alpha = 0.45f,
                    Orbit = new Vector2(-1f, 0f)  // bezier through Centre
                };
                b.R = Rent(worldRoot, PileOrder + 20);
                b.R.sprite = BlackHoleShapes.Dot;
                bits.Add(b);
            }
            // the corridor
            if (Layers.ShowCorridor)
            {
                float px = PixelWorld();
                int dots = 9;
                for (int k = 0; k < dots; k++)
                {
                    float s = (k + 0.5f) / dots;
                    var b = new Bit
                    {
                        World = true,
                        Born = t0 + Style.CorridorFrom,
                        Life = lastEnd - (t0 + Style.CorridorFrom) + 0.15f,
                        From = Bezier(pileWorld, control, holeWorld, s),
                        To = Bezier(pileWorld, control, holeWorld, s),
                        Size = Style.CorridorWidthPx * px * 1.6f,
                        EndSize = Style.CorridorWidthPx * px * 1.6f,
                        Colour = Style.WaveDark,
                        Alpha = Style.CorridorAlpha,
                        Orbit = new Vector2(-2f, s)  // corridor: breathes, fades in and out
                    };
                    b.R = Rent(worldRoot, PileOrder - 2);
                    b.R.sprite = BlackHoleShapes.Dot;
                    bits.Add(b);
                    var edge = new Bit
                    {
                        World = true,
                        Born = b.Born,
                        Life = b.Life,
                        From = b.From + side * Style.CorridorWidthPx * px * 0.55f,
                        To = b.From + side * Style.CorridorWidthPx * px * 0.55f,
                        Size = 2.2f * px,
                        EndSize = 2.2f * px,
                        Colour = new Color(0.66f, 0.66f, 0.90f),
                        Alpha = 0.10f,
                        Orbit = new Vector2(-2f, s)
                    };
                    edge.R = Rent(worldRoot, PileOrder - 1);
                    edge.R.sprite = BlackHoleShapes.Dot;
                    bits.Add(edge);
                }
            }
            Residue res = ResidueFor(report.FromDrawPile, pileWorld, pileScale);
            res.Target = 0f;
            devourResidueAt = lastEnd;
            devourResiduePile = report.FromDrawPile;
            Say(SoundDevour);
        }

        private float devourResidueAt = -1f;
        private bool devourResiduePile;

        /// <summary>The voided look of a pile slot: shown while the rules keep it empty this round,
        /// shrinking away when it fills again or the round ends. Called every repaint.</summary>
        public void SetPileResidue(bool drawPile, bool show, Vector2 pileWorld, float pileScale)
        {
            if (!Available)
            {
                return;
            }
            Residue res;
            if (!residues.TryGetValue(drawPile, out res))
            {
                if (!show)
                {
                    return;
                }
                res = ResidueFor(drawPile, pileWorld, pileScale);
            }
            res.World = pileWorld;
            res.Scale = pileScale;
            bool waiting = devourResidueAt > clock && devourResiduePile == drawPile;
            res.Target = show && Layers.ShowResidue && !waiting ? 1f : 0f;
        }

        private Residue ResidueFor(bool drawPile, Vector2 world, float scale)
        {
            Residue res;
            if (residues.TryGetValue(drawPile, out res))
            {
                return res;
            }
            res = new Residue { DrawPile = drawPile, World = world, Scale = scale };
            res.Inset = Rent(worldRoot, PileOrder - 6);
            res.Inset.sprite = ViewUtil.RoundedSprite;
            res.Ring = Rent(worldRoot, PileOrder - 5);
            res.Ring.sprite = BlackHoleShapes.Ring;
            res.Core = Rent(worldRoot, PileOrder - 4);
            res.Core.sprite = BlackHoleShapes.Dot;
            residues[drawPile] = res;
            return res;
        }

        private void ReturnResidue(Residue res)
        {
            Return(res.Inset);
            Return(res.Ring);
            Return(res.Core);
        }

        private void PaintResidues(float dt)
        {
            List<bool> drop = null;
            foreach (KeyValuePair<bool, Residue> e in residues)
            {
                Residue res = e.Value;
                if (devourResidueAt > 0f && clock >= devourResidueAt && res.DrawPile == devourResiduePile)
                {
                    res.Target = Layers.ShowResidue ? 1f : 0f;
                }
                float speed = res.Target > res.Amount ? 1f / Style.ResidueIn : 1f / Style.ResidueOut;
                res.Amount = Mathf.MoveTowards(res.Amount, res.Target, dt * speed);
                float a = res.Amount;
                bool on = a > 0.001f;
                res.Inset.enabled = res.Ring.enabled = res.Core.enabled = on;
                if (!on)
                {
                    if (res.Target <= 0f)
                    {
                        (drop ?? (drop = new List<bool>())).Add(e.Key);
                    }
                    continue;
                }
                var size = new Vector2(CardVisual.BodyWidth - 0.10f, CardVisual.BodyHeight - 0.10f) * res.Scale;
                // Shrinks away rather than fading: the residue closes up when the slot comes back.
                float close = Mathf.SmoothStep(0f, 1f, a);
                res.Inset.transform.position = res.World;
                res.Inset.transform.localScale = new Vector3(size.x, size.y, 1f);
                res.Inset.color = new Color(Style.Residue.r, Style.Residue.g, Style.Residue.b, 0.78f * close);
                float ringSize = size.x * 0.46f * Mathf.Lerp(0.3f, 1f, close);
                res.Ring.transform.position = res.World;
                res.Ring.transform.localScale = new Vector3(ringSize, ringSize, 1f);
                res.Ring.transform.localRotation = Quaternion.Euler(0f, 0f, clock * 6f);
                res.Ring.color = new Color(Style.ResidueRing.r, Style.ResidueRing.g, Style.ResidueRing.b,
                    0.30f * close);
                float coreSize = size.x * 0.34f * close;
                res.Core.transform.position = res.World;
                res.Core.transform.localScale = new Vector3(coreSize, coreSize, 1f);
                res.Core.color = new Color(0f, 0f, 0.01f, 0.85f * close);
            }
            if (drop != null)
            {
                foreach (bool k in drop)
                {
                    ReturnResidue(residues[k]);
                    residues.Remove(k);
                }
            }
        }

        // ================================================================ the frame

        private void LateUpdate()
        {
            if (!Available)
            {
                return;
            }
            float dt = Time.deltaTime;
            clock += dt;
            if (!massHeld)
            {
                shownMass = Mathf.MoveTowards(shownMass, massSwallowed, dt * massGoal * 1.5f);
            }
            foreach (KeyValuePair<GridPos, Hole> e in holes)
            {
                PaintHole(e.Value, dt);
            }
            for (int i = transients.Count - 1; i >= 0; i--)
            {
                PaintHole(transients[i], dt);
                if (clock > transients[i].TransientEnd + 0.4f)
                {
                    Return(transients[i].Quad);
                    transients.RemoveAt(i);
                }
            }
            if (lensed.Count > 0 || holeCells.Count > 0)
            {
                PaintLensing();
            }
            PaintProxies();
            PaintBits();
            PaintTicks();
            PaintResidues(dt);
            if (massHeld && proxies.Count == 0 && clock > turnEnd)
            {
                massHeld = false;
            }
            PaintDebug();
        }

        private static float Span(float t, float a, float b)
        {
            return b <= a ? (t >= b ? 1f : 0f) : Mathf.Clamp01((t - a) / (b - a));
        }

        private static float Smooth(float x)
        {
            return x * x * (3f - 2f * x);
        }

        private static float Bump(float t, float at, float length)
        {
            float u = (t - at) / Mathf.Max(length, 0.0001f);
            return u < 0f || u > 1f ? 0f : Mathf.Sin(u * Mathf.PI);
        }

        private void PaintHole(Hole h, float dt)
        {
            float age = clock - h.Born;
            float pinch = 0f, open = 1f, ignite = 1f, lens = 1f;
            if (age < Style.LensEnd)
            {
                pinch = age < Style.OpenEnd ? Mathf.Sin(Span(age, 0f, Style.OpenEnd) * Mathf.PI) : 0f;
                open = Smooth(Span(age, Style.OpenFrom, Style.OpenEnd));
                ignite = Smooth(Span(age, Style.IgniteFrom, Style.IgniteEnd));
                lens = Mathf.Max(Span(age, 0f, Style.PinchEnd) * 0.25f,
                    Smooth(Span(age, Style.LensFrom, Style.LensEnd)));
            }

            // idle variation
            if (clock >= h.NextVariation)
            {
                h.VariationFrom = h.VariationTo;
                h.VariationTo = Random.Range(-1f, 1f) * Style.VariationAmount;
                h.VariationAt = clock;
                h.NextVariation = clock + Random.Range(Style.VariationMin, Style.VariationMax);
            }
            float variation = Mathf.Lerp(h.VariationFrom, h.VariationTo, Smooth(Span(clock, h.VariationAt, h.VariationAt + 1.2f)));
            float breath = 1f + Style.BreathAmount * Mathf.Sin(clock * Style.BreathRate * 2f * Mathf.PI + h.Seed);

            // events
            float grip = Layers.ShowGrip ? Mathf.Max(Span(clock, h.GripAt, h.GripAt + Style.Grip) * (1f - Span(clock, h.GripAt + Style.Grip + 0.35f, h.GripAt + Style.Grip + 0.7f)), 0f) : 0f;
            float pulse = 0f;
            for (int i = h.Pulses.Count - 1; i >= 0; i--)
            {
                pulse = Mathf.Max(pulse, Bump(clock, h.Pulses[i], Style.PulseLength));
                if (clock > h.Pulses[i] + Style.PulseLength)
                {
                    h.Pulses.RemoveAt(i);
                }
            }
            float sat = 0f, silence = 0f;
            if (h.SatAt > -50f)
            {
                sat = Smooth(Span(clock, h.SatAt, h.SatAt + Style.Saturation))
                    * (1f - Smooth(Span(clock, h.BreakAt, h.BreakAt + Style.Recover)));
                silence = Span(clock, h.SatAt + Style.Saturation * 0.7f, h.SatAt + Style.Saturation)
                    * (1f - Smooth(Span(clock, h.BreakAt + 0.05f, h.BreakAt + Style.Recover)));
            }
            float rogue = h.RogueAt > -50f ? Span(clock, h.RogueAt, h.RogueAt + Style.RogueWarning)
                * (1f - Span(clock, h.RogueAt + 0.9f, h.RogueAt + 1.3f)) : 0f;
            float big = Bump(clock, h.BigPulseAt, 0.16f);
            float blocked = Layers.ShowBlocked ? Bump(clock, h.BlockedAt, Style.Blocked) : 0f;
            float progress = Mathf.Clamp01(shownMass / massGoal);
            float full = Smooth(Span(progress, 0.9f, 1f));

            float speed = 1f + (Style.GripSpeed - 1f) * grip + (Style.PulseSpeed - 1f) * pulse
                + (Style.SaturationSpeed - 1f) * sat + (Style.RogueSpeed - 1f) * rogue
                + 0.6f * big + 0.18f * full;
            // a rogue disk is unsteady: its speed stutters
            speed += rogue * 0.35f * Mathf.Sin(clock * 23f + h.Seed);
            float omega = Style.DiskSpeed * Mathf.Deg2Rad * speed;
            h.Angle += omega * dt;
            h.Angle2 += omega * Style.OuterRatio * dt;

            float bright = 1f + variation + Style.GripBright * grip + Style.PulseBright * pulse
                + Style.SaturationBright * sat + 0.35f * big + 0.08f * blocked + 0.1f * rogue;
            float diskScale = 1f - (1f - Style.SaturationScale) * sat - 0.04f * full - 0.05f * blocked;
            h.LensScale = lens * (1f + 0.6f * silence + 0.25f * grip);

            Vector2 local = h.Transient ? (Vector2)transform.InverseTransformPoint(h.TransientWorld) : HoleLocal(h);
            float cell = owner != null ? owner.CellWorldSize : 1f;
            SpriteRenderer q = h.Quad;
            if (h.Transient)
            {
                q.transform.position = h.TransientWorld;
                float lossy = transform.lossyScale.x;
                q.transform.localScale = new Vector3(cell * lossy * Style.Span, cell * lossy * Style.Span, 1f);
            }
            else
            {
                q.transform.localPosition = local;
                q.transform.localScale = new Vector3(cell * Style.Span, cell * Style.Span, 1f);
            }
            q.enabled = Layers.ShowHorizon || Layers.ShowDisk || Layers.ShowMass;
            float fade = 1f;
            if (h.Transient)
            {
                fade = 1f - Span(clock, h.TransientEnd, h.TransientEnd + 0.35f);
            }
            q.GetPropertyBlock(block);
            block.SetFloat(SpanId, Style.Span);
            block.SetFloat(ClockId, clock);
            block.SetFloat(SeedId, h.Seed);
            block.SetFloat(CoreRId, Style.CoreRadius * breath * (1f - 0.06f * blocked));
            block.SetFloat(OpenId, Layers.ShowHorizon ? open : 0f);
            block.SetFloat(PinchId, Layers.ShowHorizon ? pinch : 0f);
            block.SetFloat(IgniteId, ignite);
            block.SetFloat(AngleId, h.Angle);
            block.SetFloat(Angle2Id, h.Angle2);
            block.SetFloat(BrightId, Layers.ShowDisk ? bright : 0f);
            block.SetFloat(DiskScaleId, diskScale);
            block.SetFloat(SilenceId, Layers.ShowDisk ? silence : 1f);
            block.SetFloat(RimId, 1f + 0.8f * blocked + 0.9f * silence + 0.6f * rogue);
            block.SetFloat(WarmId, 1f);
            block.SetFloat(LensDarkId, 1f + 0.8f * silence + 0.3f * sat);
            block.SetFloat(ProgressId, Layers.ShowMass && !h.Transient ? progress : 0f);
            block.SetFloat(PulseId, Layers.ShowMass ? Bump(clock, massPulseAt, 0.11f) : 0f);
            block.SetFloat(FadeId, fade);
            block.SetFloat(DebugId, Layers.ShowHorizonMask ? 1f : Layers.ShowMassDebug ? 3f : 0f);
            q.SetPropertyBlock(block);

            // a rare mote of ambient matter falling in
            if (!h.Transient && Layers.ShowMotes && clock >= h.NextMote && age > Style.LensEnd)
            {
                h.NextMote = clock + Random.Range(Style.MoteMin, Style.MoteMax);
                float a = Random.Range(0f, Mathf.PI * 2f);
                var b = new Bit
                {
                    Born = clock,
                    Life = Style.MoteLife,
                    Centre = local,
                    Orbit = new Vector2(a, 0.8f),
                    From = local + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * cell * 1.15f,
                    To = local,
                    Size = cell * 0.05f,
                    EndSize = cell * 0.012f,
                    Colour = Style.MoteColour,
                    Alpha = 0.32f
                };
                b.R = Rent(transform, MoteOrder);
                b.R.sprite = BlackHoleShapes.Dot;
                bits.Add(b);
            }
        }

        private void PaintProxies()
        {
            if (proxies.Count == 0)
            {
                return;
            }
            float px = PixelWorld() / Mathf.Max(transform.lossyScale.x, 0.0001f);
            for (int i = proxies.Count - 1; i >= 0; i--)
            {
                Proxy p = proxies[i];
                bool done;
                switch (p.Kind)
                {
                    case ProxyKind.Pull:
                        done = PaintPull(p, px);
                        break;
                    case ProxyKind.Collapse:
                        done = PaintCollapse(p, px);
                        break;
                    case ProxyKind.Card:
                        done = PaintCard(p);
                        break;
                    default:
                        done = PaintSwallow(p, px);
                        break;
                }
                if (done)
                {
                    Return(p.R);
                    Return(p.Inner);
                    proxies.RemoveAt(i);
                }
            }
            if (held.Count > 0)
            {
                bool pulling = false;
                foreach (Proxy p in proxies)
                {
                    pulling |= p.Kind == ProxyKind.Pull;
                }
                if (!pulling && owner != null)
                {
                    owner.ReleaseCells(new List<GridPos>(held));
                    held.Clear();
                }
            }
        }

        private bool PaintPull(Proxy p, float px)
        {
            Vector2 centre = p.Hole != null ? HoleLocal(p.Hole) : p.To;
            Vector2 dir = (centre - p.From);
            dir = dir.sqrMagnitude > 0.000001f ? dir.normalized : Vector2.right;
            if (clock < p.Start)
            {
                float lean = p.Start == float.MaxValue ? 0f
                    : Smooth(Span(clock, p.Start - Style.Grip, p.Start)) * Style.PreDragRing2 * px;
                PaintCubeProxy(p, p.From + dir * lean, 1.01f, 0.995f, 0.004f, 0.03f, 0f, false, 0f, 0f);
                return false;
            }
            float t = Span(clock, p.Start, p.Start + p.Duration);
            // nonlinear: slow off the mark, faster and faster as the hole takes it
            float e = t * t * (1.6f - 0.6f * t);
            Vector2 pos = Vector2.Lerp(p.From, p.To, e);
            float closeness = 1f - Mathf.Clamp01(Vector2.Distance(pos, centre) / (owner.CellWorldSize * 2.2f));
            float settle = Span(clock, p.Start + p.Duration, p.Start + p.Duration + Style.PullSettle);
            float overshoot = settle > 0f ? Mathf.Sin(settle * Mathf.PI) * Style.PullOvershoot * px : 0f;
            pos += dir * overshoot;
            p.R.transform.localRotation = Quaternion.Euler(0f, 0f, SignedTurn(p.From, centre) * 2.5f * e * (1f - settle));
            if (!Layers.ShowPull)
            {
                pos = settle > 0f ? p.To : p.From;
            }
            PaintCubeProxy(p, pos, 1f + 0.05f * closeness, 1f - 0.02f * closeness, 0.012f * closeness,
                0.1f * closeness, 0f, false, 0f, 0f);
            return settle >= 1f;
        }

        private float SignedTurn(Vector2 from, Vector2 centre)
        {
            Vector2 d = centre - from;
            return d.x >= 0f ? -1f : 1f;
        }

        private bool PaintSwallow(Proxy p, float px)
        {
            if (p.Hole == null)
            {
                return true;
            }
            Vector2 centre = HoleLocal(p.Hole);
            Vector2 d0 = p.From - centre;
            Vector2 dir0 = d0.sqrMagnitude > 0.000001f ? -d0.normalized : Vector2.right;
            if (clock < p.Start)
            {
                float lean = p.Start == float.MaxValue ? 0f
                    : Smooth(Span(clock, p.Start - Style.Grip, p.Start)) * Style.PreDragRing1 * px;
                PaintCubeProxy(p, p.From + dir0 * lean, 1.02f, 0.99f, 0.01f, 0.05f, 0f, false, 0f, 0f);
                return false;
            }
            float t = clock - p.Start;
            float horizon = Style.CoreRadius * owner.CellWorldSize;
            if (t < Style.SwallowLock || !Layers.ShowSwallow)
            {
                // LOCK: caught - a short, hard lean into the hole, still wholly itself.
                float k = Smooth(Span(t, 0f, Style.SwallowLock));
                Vector2 at = p.From + dir0 * (Style.PreDragRing1 * px + k * Style.SwallowLockPx * px);
                PaintCubeProxy(p, at, 1f + 0.04f * k, 1f - 0.03f * k, 0.02f, 0.1f, 0f, true, 0f, horizon);
                if (!Layers.ShowSwallow && t >= Style.SwallowLock)
                {
                    Absorb(p);
                    return true;
                }
                return false;
            }
            float u = Mathf.Clamp01((t - Style.SwallowLock) / Mathf.Max(p.Duration - Style.SwallowLock, 0.01f));
            // the radius falls away faster and faster; the angle runs ahead as it nears
            float fall = u * u * (1.25f - 0.25f * u);
            float r = Mathf.Lerp(p.Radius0 + Style.SwallowLockPx * px * 0f, 0f, fall);
            float theta = p.Theta0 + p.Turns * Mathf.PI * 2f * Mathf.Pow(u, 1.6f);
            Vector2 pos = centre + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * r;
            Vector2 toward = centre - pos;
            Vector2 dir = toward.sqrMagnitude > 0.000001f ? toward.normalized : dir0;
            p.R.transform.localRotation = Quaternion.Euler(0f, 0f, p.Spin * Smooth(u));

            // SPAGHETTIFICATION: the form is kept until the last quarter
            float radial = 1f, tangent = 1f;
            if (Layers.ShowSpaghetti)
            {
                radial = u < 0.5f ? Mathf.Lerp(1f, 1.10f, u / 0.5f) : Mathf.Lerp(1.10f, 1.40f, (u - 0.5f) / 0.5f);
                tangent = u < 0.4f ? Mathf.Lerp(1f, 0.82f, u / 0.4f)
                    : u < 0.75f ? Mathf.Lerp(0.82f, 0.55f, (u - 0.4f) / 0.35f)
                    : Mathf.Lerp(0.55f, 0.25f, (u - 0.75f) / 0.25f);
            }
            float late = Span(u, 0.7f, 1f);
            float darken = u < 0.7f ? Mathf.Lerp(0.1f, 0.25f, u / 0.7f) : Mathf.Lerp(0.4f, 0.92f, late);
            float desat = late * 0.85f;
            float streak = Span(u, 0.82f, 1f) * (1f - Span(u, 0.97f, 1f));
            PaintCubeProxy(p, pos, radial, tangent, 0.03f + 0.05f * u, darken, desat, Layers.ShowOcclusion,
                streak, horizon, dir);
            if (!p.Absorbed && r <= horizon * 0.85f)
            {
                Absorb(p);
            }
            if (p.Absorbed && !p.Ticked && clock >= p.HitAt + Style.TickDelay)
            {
                p.Ticked = true;
                ScoreTick(centre);
            }
            return u >= 1f && p.Ticked;
        }

        private void Absorb(Proxy p)
        {
            p.Absorbed = true;
            p.HitAt = clock;
            if (p.Hole != null)
            {
                p.Hole.Pulses.Add(clock);
            }
            shownMass = Mathf.Min(shownMass + 1f, massGoal);
            massPulseAt = clock;
            Say(SoundSwallow);
        }

        private void ScoreTick(Vector2 centre)
        {
            if (!Layers.ShowScore || prepared == null || prepared.PointsEach <= 0)
            {
                return;
            }
            int total = prepared.SwallowedThisTurn;
            if (total <= Style.PerBlockTickMax)
            {
                SpawnTick(centre, prepared.PointsEach, false);
                return;
            }
            // four or more: one rolling subtotal instead of a text per cube
            if (rolling == null || !ticks.Contains(rolling))
            {
                rollingValue = 0;
                rolling = SpawnTick(centre, 0, true);
            }
            rollingValue += prepared.PointsEach;
            rolling.Value = rollingValue;
            rolling.Born = clock;
        }

        private Tick SpawnTick(Vector2 at, int value, bool rollingTick)
        {
            float cell = owner.CellWorldSize;
            var tick = new Tick
            {
                Text = RentText(TextOrder + 1),
                Shadow = RentText(TextOrder),
                Born = clock,
                Life = rollingTick ? 0.9f : 0.62f,
                At = at + new Vector2(0f, cell * 0.30f),
                Value = value,
                Rolling = rollingTick
            };
            tick.Text.color = Style.Ivory;
            tick.Shadow.color = Style.ScoreEdge;
            float size = cell * (rollingTick ? 0.009f : 0.0075f);
            tick.Text.characterSize = size;
            tick.Shadow.characterSize = size;
            ticks.Add(tick);
            return tick;
        }

        private bool PaintCollapse(Proxy p, float px)
        {
            Vector2 centre = p.Hole != null ? HoleLocal(p.Hole) : p.From;
            Vector2 dir = centre - p.From;
            dir = dir.sqrMagnitude > 0.000001f ? dir.normalized : Vector2.up;
            if (p.HitAt < 0f || clock < p.Start || p.Start == float.MaxValue)
            {
                PaintCubeProxy(p, p.From, 1f, 1f, 0f, 0f, 0f, false, 0f, 0f);
                return false;
            }
            if (clock < p.HitAt)
            {
                // SILENCE: every cube left on the board feels a tug toward the hole
                float breakAt = p.Hole != null ? p.Hole.BreakAt : p.HitAt;
                float tug = Smooth(Span(clock, breakAt - Style.Silence, breakAt)) * Style.TugPx * px;
                PaintCubeProxy(p, p.From + dir * tug, 1.01f, 0.99f, 0.01f, 0.05f, 0f, false, 0f, 0f);
                return false;
            }
            float t = clock - p.HitAt;
            if (t < Style.Compress)
            {
                // the break reaches it: a brief inward compression
                float k = Mathf.Sin(Span(t, 0f, Style.Compress) * Mathf.PI);
                PaintCubeProxy(p, p.From + dir * (Style.TugPx + k) * px, 1f - 0.06f * k, 0.92f,
                    0.01f, 0.1f * k, 0f, false, 0f, 0f);
                return false;
            }
            if (!p.Burst)
            {
                p.Burst = true;
                BurstCube(p, dir);
                if (!p.Ticked && prepared != null && Layers.ShowScore && prepared.PointsEach > 0)
                {
                    p.Ticked = true;
                    if (rolling == null || !ticks.Contains(rolling))
                    {
                        rollingValue = 0;
                        rolling = SpawnTick(centre, 0, true);
                    }
                    rollingValue += prepared.PointsEach;
                    rolling.Value = rollingValue;
                    rolling.Born = clock;
                }
            }
            return true;
        }

        private void BurstCube(Proxy p, Vector2 towardHole)
        {
            if (!Layers.ShowCollapse)
            {
                return;
            }
            Color material = ViewUtil.CubeMaterialColor(p.Cube);
            float cube = p.Size;
            uint h = Hash(p.Cell);
            int count = Style.FragmentsMin + (int)(h % (uint)(Style.FragmentsMax - Style.FragmentsMin + 1));
            if (proxies.Count > 24)
            {
                count = Style.FragmentsMin;
            }
            Vector2 away = -towardHole;
            for (int k = 0; k < count; k++)
            {
                float a = Mathf.Atan2(away.y, away.x) + ((h >> (k * 3)) % 100 / 100f - 0.5f) * 2.6f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float reach = cube * (0.35f + ((h >> (k * 5)) % 100) / 100f * 0.35f);
                var b = new Bit
                {
                    Born = clock,
                    Life = Style.Burst * (0.8f + 0.4f * (((h >> k) & 7) / 7f)),
                    From = p.From + d * cube * 0.1f,
                    To = p.From + d * reach,
                    Size = cube * (0.16f + ((h >> (k * 2)) % 10) / 100f),
                    EndSize = cube * 0.05f,
                    Spin = ((h >> (k + 4)) % 2 == 0 ? 1f : -1f) * 260f,
                    Colour = Color.Lerp(material, Color.white, 0.08f),
                    Alpha = 1f
                };
                b.R = Rent(transform, FragmentOrder);
                b.R.sprite = BlackHoleShapes.Shard;
                bits.Add(b);
            }
            // a local glow in its own colour, and a little dust
            var glow = new Bit
            {
                Born = clock,
                Life = Style.Burst * 0.8f,
                From = p.From,
                To = p.From,
                Size = cube * 1.1f,
                EndSize = cube * 1.4f,
                Colour = material,
                Alpha = 0.30f
            };
            glow.R = Rent(transform, FragmentOrder - 1);
            glow.R.sprite = BlackHoleShapes.Dot;
            bits.Add(glow);
            for (int k = 0; k < 3; k++)
            {
                float a = (h % 628) / 100f + k * 2.1f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var dust = new Bit
                {
                    Born = clock,
                    Life = Style.Burst * 1.2f,
                    From = p.From,
                    To = p.From + d * cube * 0.55f,
                    Size = cube * 0.05f,
                    EndSize = cube * 0.02f,
                    Colour = Color.Lerp(material, new Color(0.6f, 0.58f, 0.66f), 0.5f),
                    Alpha = 0.5f
                };
                dust.R = Rent(transform, FragmentOrder);
                dust.R.sprite = BlackHoleShapes.Dot;
                bits.Add(dust);
            }
            Say(SoundBreak);
        }

        private void SpawnWave(Hole h, float at, float reach)
        {
            float cell = owner.CellWorldSize;
            Vector2 centre = HoleLocal(h);
            for (int k = 0; k < 2; k++)
            {
                var b = new Bit
                {
                    Born = at + k * 0.02f,
                    Life = Style.Wave + 0.06f,
                    From = centre,
                    To = centre,
                    Size = cell * 0.4f,
                    EndSize = (reach + cell) * 2.2f,
                    Colour = k == 0 ? Style.WaveDark : Style.WaveAmber,
                    Alpha = k == 0 ? Style.WaveAlpha : Style.WaveAlpha * 0.45f,
                    Orbit = new Vector2(-3f, 0f)  // a ring: fades as it grows
                };
                b.R = Rent(transform, WaveOrder + k);
                b.R.sprite = BlackHoleShapes.Ring;
                bits.Add(b);
            }
            Say(SoundSaturate);
        }

        private bool PaintCard(Proxy p)
        {
            if (p.Hole == null)
            {
                return true;
            }
            Vector2 holeWorld = p.Hole.Transient ? p.Hole.TransientWorld
                : (Vector2)transform.TransformPoint(HoleLocal(p.Hole));
            p.To = holeWorld;
            float startCompress = p.Start - Style.PeelFrom + Style.CompressFrom;
            Vector2 toHole = holeWorld - p.From;
            Vector2 dir = toHole.sqrMagnitude > 0.000001f ? toHole.normalized : Vector2.right;
            float horizon = Style.CoreRadius * (owner != null ? owner.CellWorldSize : 1f) * transform.lossyScale.x;
            if (clock < p.Start)
            {
                // warning: the stack slides toward the hole; then it leans and thins
                float warn = Span(clock, startCompress - Style.CompressFrom, startCompress - Style.CompressFrom + Style.RogueWarning);
                float squeeze = Smooth(Span(clock, startCompress, p.Start - Style.PeelFrom + Style.CompressEnd));
                float px = PixelWorld();
                Vector2 at = p.From + dir * (warn * 2f * px + squeeze * 3f * px);
                float lean = Vector2.SignedAngle(Vector2.up, dir) > 0f ? 4f : -4f;
                PaintCardProxy(p, at, dir, 1f, 1f - 0.12f * squeeze, 1f - 0.08f * squeeze, lean * squeeze,
                    0f, 0f, 0f, horizon);
                return false;
            }
            float u = Span(clock, p.Start, p.Start + p.Duration);
            float e = u * u * (1.4f - 0.4f * u);
            Vector2 pos = Bezier(p.From, p.Control, holeWorld, e);
            Vector2 tangentDir = Bezier(p.From, p.Control, holeWorld, Mathf.Min(1f, e + 0.02f)) - pos;
            tangentDir = tangentDir.sqrMagnitude > 0.0000001f ? tangentDir.normalized : dir;
            float stretch = u < 0.6f ? Mathf.Lerp(1f, 1.12f, u / 0.6f) : Mathf.Lerp(1.12f, 1.35f, (u - 0.6f) / 0.4f);
            float thin = u < 0.4f ? Mathf.Lerp(1f, 0.85f, u / 0.4f) : Mathf.Lerp(0.85f, 0.22f, (u - 0.4f) / 0.6f);
            float late = Span(u, 0.6f, 1f);
            PaintCardProxy(p, pos, tangentDir, stretch, thin, 0.88f * Mathf.Lerp(1f, 0.9f, u), 0f,
                late * 0.8f, 0.15f + late * 0.75f, Span(u, 0.85f, 1f), horizon);
            if (!p.Absorbed && Vector2.Distance(pos, holeWorld) <= horizon * 0.9f)
            {
                p.Absorbed = true;
                p.Hole.Pulses.Add(clock);
            }
            return u >= 1f;
        }

        private void PaintCardProxy(Proxy p, Vector2 at, Vector2 dir, float radial, float tangent,
            float heightScale, float leanDeg, float desat, float darken, float streak, float horizon)
        {
            Vector2 size = p.CardSize;
            foreach (SpriteRenderer r in new[] { p.R, p.Inner })
            {
                if (r == null)
                {
                    continue;
                }
                bool inner = r == p.Inner;
                Vector2 s = inner ? size - new Vector2(0.10f, 0.10f) * (size.y / CardVisual.BodyHeight) : size;
                r.transform.position = at;
                r.transform.localRotation = Quaternion.Euler(0f, 0f, leanDeg);
                r.transform.localScale = new Vector3(s.x, s.y * heightScale, 1f);
                r.enabled = true;
                r.GetPropertyBlock(block);
                CopyWarp(null, block);
                block.SetFloat(WarpIds[0], 0f);
                block.SetFloat(WarpIds[4], 0f);
                block.SetVector(PivotId, new Vector4(at.x, at.y, size.y * 0.5f, 0f));
                block.SetVector(DirId, new Vector4(dir.x, dir.y, 0f, 0f));
                block.SetFloat(RadialId, radial);
                block.SetFloat(TangentId, tangent);
                block.SetVector(OffsetId, Vector4.zero);
                block.SetFloat(EdgeBendId, 0f);
                block.SetFloat(UVDragId, 0f);
                block.SetFloat(DarkenId, darken);
                block.SetFloat(DesatId, desat);
                Vector2 hole = p.To;
                block.SetVector(HoleId, new Vector4(hole.x, hole.y, horizon, 0f));
                block.SetFloat(HoleSoftId, horizon * 0.08f);
                block.SetFloat(OccludeId, Layers.ShowOcclusion ? 1f : 0f);
                block.SetFloat(StreakId, streak);
                r.SetPropertyBlock(block);
            }
        }

        private void PaintCubeProxy(Proxy p, Vector2 local, float radial, float tangent, float drag,
            float darken, float desat, bool occlude, float streak, float horizonLocal)
        {
            Vector2 centre = p.Hole != null ? HoleLocal(p.Hole) : local;
            Vector2 d = centre - local;
            PaintCubeProxy(p, local, radial, tangent, drag, darken, desat, occlude, streak, horizonLocal,
                d.sqrMagnitude > 0.000001f ? d.normalized : Vector2.right);
        }

        private void PaintCubeProxy(Proxy p, Vector2 local, float radial, float tangent, float drag,
            float darken, float desat, bool occlude, float streak, float horizonLocal, Vector2 dirLocal)
        {
            SpriteRenderer r = p.R;
            if (r == null)
            {
                return;
            }
            r.enabled = true;
            ViewUtil.ApplyTile(r, p.Tile, p.Size);
            r.sharedMaterial = MatterMaterial;
            r.color = p.Colour;
            r.transform.localPosition = local;
            float lossy = transform.lossyScale.x;
            Vector2 pivot = transform.TransformPoint(local);
            Vector2 holeWorld = p.Hole != null ? (Vector2)transform.TransformPoint(HoleLocal(p.Hole)) : pivot;
            Vector2 dirWorld = transform.TransformDirection(dirLocal);
            r.GetPropertyBlock(block);
            for (int i = 0; i < WarpIds.Length; i++)
            {
                block.SetFloat(WarpIds[i], p.Warp != null ? p.Warp[i] : 0f);
            }
            block.SetVector(PivotId, new Vector4(pivot.x, pivot.y, p.Size * lossy * 0.5f, 0f));
            block.SetVector(DirId, new Vector4(dirWorld.x, dirWorld.y, 0f, 0f));
            block.SetFloat(RadialId, radial);
            block.SetFloat(TangentId, tangent);
            block.SetVector(OffsetId, Vector4.zero);
            block.SetFloat(EdgeBendId, 0f);
            block.SetFloat(UVDragId, drag);
            block.SetFloat(DarkenId, darken);
            block.SetFloat(DesatId, desat);
            float horizon = horizonLocal * lossy;
            block.SetVector(HoleId, new Vector4(holeWorld.x, holeWorld.y, horizon, 0f));
            block.SetFloat(HoleSoftId, Mathf.Max(horizon * 0.06f, 0.0005f));
            block.SetFloat(OccludeId, occlude ? 1f : 0f);
            block.SetFloat(StreakId, streak);
            r.SetPropertyBlock(block);
        }

        private void PaintBits()
        {
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                Bit b = bits[i];
                float t = (clock - b.Born) / Mathf.Max(b.Life, 0.0001f);
                if (t < 0f)
                {
                    b.R.enabled = false;
                    continue;
                }
                if (t >= 1f)
                {
                    Return(b.R);
                    bits.RemoveAt(i);
                    continue;
                }
                Vector2 pos;
                float alpha;
                if (b.Orbit.x == -2f)
                {
                    // corridor: in, hold, out, and a slow shimmer along it
                    pos = b.From;
                    alpha = Mathf.Min(Span(t, 0f, 0.15f), 1f - Span(t, 0.8f, 1f))
                        * (0.8f + 0.2f * Mathf.Sin(clock * 9f + b.Orbit.y * 12f));
                }
                else if (b.Orbit.x == -3f)
                {
                    // the break: a ring that grows and thins out as it goes
                    pos = b.From;
                    alpha = Span(t, 0f, 0.1f) * (1f - Smooth(t));
                }
                else if (b.Orbit.x == -1f)
                {
                    // a card-coloured streak along the devour's bezier
                    float e = t * t;
                    pos = Bezier(b.From, b.Centre, b.To, e);
                    alpha = (1f - Span(t, 0.7f, 1f));
                }
                else if (b.Orbit.y > 0f)
                {
                    // a mote: spirals in and is gone at the horizon
                    float e = t * t;
                    float r0 = (b.From - b.Centre).magnitude;
                    float a = b.Orbit.x + b.Orbit.y * Mathf.PI * 2f * e;
                    float r = Mathf.Lerp(r0, owner != null ? Style.CoreRadius * owner.CellWorldSize * 0.9f : 0f, e);
                    pos = b.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    alpha = Span(t, 0f, 0.2f) * (1f - Span(t, 0.85f, 1f));
                }
                else
                {
                    float e = 1f - (1f - t) * (1f - t);
                    pos = Vector2.Lerp(b.From, b.To, e);
                    alpha = 1f - Smooth(Span(t, 0.35f, 1f));
                }
                float size = Mathf.Lerp(b.Size, b.EndSize, t);
                b.R.enabled = true;
                if (b.World)
                {
                    b.R.transform.position = pos;
                }
                else
                {
                    b.R.transform.localPosition = pos;
                }
                b.R.transform.localScale = new Vector3(size, size, 1f);
                if (b.Spin != 0f)
                {
                    b.R.transform.localRotation = Quaternion.Euler(0f, 0f, b.Spin * t);
                }
                b.R.color = new Color(b.Colour.r, b.Colour.g, b.Colour.b, b.Alpha * alpha);
            }
        }

        private void PaintTicks()
        {
            for (int i = ticks.Count - 1; i >= 0; i--)
            {
                Tick tk = ticks[i];
                float t = (clock - tk.Born) / tk.Life;
                if (t >= 1f)
                {
                    ReturnText(tk.Text);
                    ReturnText(tk.Shadow);
                    ticks.RemoveAt(i);
                    if (ReferenceEquals(tk, rolling))
                    {
                        rolling = null;
                    }
                    continue;
                }
                float cell = owner != null ? owner.CellWorldSize : 1f;
                float rise = (tk.Rolling ? 0.04f : 0.10f) * cell * Smooth(Mathf.Clamp01(t * 1.5f));
                float alpha = tk.Rolling ? 1f - Span(t, 0.6f, 1f) : 1f - Span(t, 0.45f, 1f);
                alpha *= Span(t, 0f, 0.08f);
                string text = "+" + tk.Value;
                tk.Text.text = text;
                tk.Shadow.text = text;
                Vector2 at = tk.At + new Vector2(0f, rise);
                float off = cell * 0.012f;
                tk.Text.transform.localPosition = at;
                tk.Shadow.transform.localPosition = at + new Vector2(off, -off);
                tk.Text.color = new Color(Style.Ivory.r, Style.Ivory.g, Style.Ivory.b, alpha);
                tk.Shadow.color = new Color(Style.ScoreEdge.r, Style.ScoreEdge.g, Style.ScoreEdge.b, alpha * 0.9f);
            }
        }

        // ================================================================ debug

        private void PaintDebug()
        {
            HideDebug();
            if (owner == null || owner.Board == null)
            {
                return;
            }
            int used = 0;
            if (Layers.ShowInfluence)
            {
                GameBoard model = owner.Board;
                for (int y = model.MinY; y < model.MinY + model.Height; y++)
                {
                    for (int x = model.MinX; x < model.MinX + model.Width; x++)
                    {
                        var cell = new GridPos(x, y);
                        GridPos hole;
                        int ring = KaraDelikJoker.InfluenceAt(holeCells, cell, out hole);
                        if (ring == 0 || !model.IsInside(cell))
                        {
                            continue;
                        }
                        SpriteRenderer m = DebugMark(used++);
                        m.transform.localPosition = owner.CellToWorld(cell);
                        float s = owner.CellWorldSize * 0.9f;
                        m.transform.localScale = new Vector3(s, s, 1f);
                        m.color = ring == 1 ? new Color(1f, 0.3f, 0.3f, 0.22f) : new Color(1f, 0.85f, 0.2f, 0.16f);
                    }
                }
            }
            if (Layers.ShowPaths)
            {
                foreach (Proxy p in proxies)
                {
                    if (p.Kind == ProxyKind.Card || p.Hole == null)
                    {
                        continue;
                    }
                    Vector2 end = p.Kind == ProxyKind.Pull ? p.To : HoleLocal(p.Hole);
                    for (int k = 0; k <= 6; k++)
                    {
                        SpriteRenderer m = DebugMark(used++);
                        m.transform.localPosition = Vector2.Lerp(p.From, end, k / 6f);
                        float s = owner.CellWorldSize * 0.06f;
                        m.transform.localScale = new Vector3(s, s, 1f);
                        m.color = p.Kind == ProxyKind.Pull ? new Color(0.3f, 0.8f, 1f, 0.9f)
                            : p.Kind == ProxyKind.Collapse ? new Color(1f, 0.6f, 0.2f, 0.9f)
                            : new Color(0.8f, 0.4f, 1f, 0.9f);
                    }
                }
            }
        }

        private SpriteRenderer DebugMark(int index)
        {
            while (debugMarks.Count <= index)
            {
                SpriteRenderer m = Rent(transform, DebugOrder);
                m.sprite = BlackHoleShapes.Quad;
                debugMarks.Add(m);
            }
            debugMarks[index].enabled = true;
            return debugMarks[index];
        }

        private void HideDebug()
        {
            for (int i = 0; i < debugMarks.Count; i++)
            {
                debugMarks[i].enabled = false;
            }
        }

        // ================================================================ helpers

        private Vector2 HoleLocal(Hole h)
        {
            if (h.Transient)
            {
                return transform.InverseTransformPoint(h.TransientWorld);
            }
            return owner != null ? owner.CellToWorld(h.Cell) : Vector2.zero;
        }

        private Hole HoleAt(GridPos cell)
        {
            Hole h;
            if (holes.TryGetValue(cell, out h))
            {
                return h;
            }
            // Not seen yet (the repaint that laid it is this one): make it now.
            if (owner != null && owner.Board != null && owner.Board.IsInside(cell))
            {
                Cube? cube = owner.Board.GetCube(cell);
                if (cube.HasValue && CubeRules.IsAnchored(cube.Value))
                {
                    h = MakeHole(cell);
                    h.Born = clock;
                    holes[cell] = h;
                    return h;
                }
            }
            return null;
        }

        private Hole NearestHole(Vector2 local)
        {
            Hole best = null;
            float bestD = float.MaxValue;
            foreach (KeyValuePair<GridPos, Hole> e in holes)
            {
                float d = (HoleLocal(e.Value) - local).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = e.Value;
                }
            }
            return best;
        }

        private Hole NearestHoleWorld(Vector2 world)
        {
            return NearestHole(transform.InverseTransformPoint(world));
        }

        private Vector2 BoardEdgeToward(Vector2 world)
        {
            if (owner == null || owner.Board == null)
            {
                return world;
            }
            Rect rect = owner.WorldRect;
            Vector2 local = transform.InverseTransformPoint(world);
            float inset = owner.CellWorldSize * 0.5f;
            var clamped = new Vector2(Mathf.Clamp(local.x, rect.xMin + inset, rect.xMax - inset),
                Mathf.Clamp(local.y, rect.yMin + inset, rect.yMax - inset));
            return transform.TransformPoint(clamped);
        }

        private float PixelWorld()
        {
            return Pixel != null ? Pixel() : 0.01f;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        private static uint Hash(GridPos cell)
        {
            unchecked
            {
                uint h = (uint)(cell.X * 73856093) ^ (uint)(cell.Y * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h;
            }
        }

        private readonly Dictionary<string, float> lastSaid = new Dictionary<string, float>();

        private void Say(string id)
        {
            if (Sounded == null)
            {
                return;
            }
            float last;
            if (lastSaid.TryGetValue(id, out last) && clock - last < 0.035f)
            {
                return;
            }
            lastSaid[id] = clock;
            Sounded(id);
        }

        private SpriteRenderer Rent(Transform parent, int order)
        {
            SpriteRenderer r;
            if (pool.Count > 0)
            {
                r = pool.Pop();
                r.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("BlackHolePart");
                r = go.AddComponent<SpriteRenderer>();
            }
            r.transform.SetParent(parent, false);
            r.sortingOrder = order;
            r.enabled = false;
            r.color = Color.white;
            r.sharedMaterial = ViewUtil.PlainSpriteMaterial;
            r.SetPropertyBlock(null);
            r.transform.localPosition = Vector3.zero;
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = Vector3.one;
            return r;
        }

        private void Return(SpriteRenderer r)
        {
            if (r == null)
            {
                return;
            }
            r.enabled = false;
            r.SetPropertyBlock(null);
            r.transform.SetParent(transform, false);
            r.gameObject.SetActive(false);
            pool.Push(r);
        }

        private TextMesh RentText(int order)
        {
            TextMesh t;
            if (textPool.Count > 0)
            {
                t = textPool.Pop();
                t.gameObject.SetActive(true);
            }
            else
            {
                t = ViewUtil.MakeText3D(transform, "BlackHoleScore", Vector2.zero, "", 90, 0.01f,
                    Color.white, order, TextAnchor.MiddleCenter);
            }
            t.GetComponent<MeshRenderer>().sortingOrder = order;
            t.transform.localScale = Vector3.one;
            return t;
        }

        private void ReturnText(TextMesh t)
        {
            if (t == null)
            {
                return;
            }
            t.gameObject.SetActive(false);
            textPool.Push(t);
        }

        private void OnDisable()
        {
            StopEvents();
        }
    }
}
