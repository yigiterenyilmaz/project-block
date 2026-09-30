// PURPOSE: "Barut tedarikçisi" - POWDER MAGAZINE / PRESSURIZED CHARGE PACK. A dynamite block that
// is left standing takes powder every turn, and this is what makes that READ: the block is one
// explosive pack whose chambers fill one by one, whose inside visibly fills with warm powder, whose
// strap holds more heat under it every turn, and which, when it finally goes, cooks off through every
// chamber it filled before it blows.
//
// THREE PASSES ARE BURIED HERE, and the second is the one to learn from.
//   1. An orange glow, a fuse spark and a heat rim over EACH cube (deleted): a grid of red lights
//      getting more orange, the fog swallowing the red casing, stage 4 and 5 told apart by glare.
//   2. The first magazine: straps, chambers, seams - and the charge told through STRESS. The seams
//      got crisp broken hairlines (they read as CRACKS), a heat haze wobbled the whole cube frame and
//      all (it read as the block TREMBLING), the chambers were six pixels, and the light the first
//      version had was gone. Stage 1 and stage 5 were the same red block, one of them shaking.
//   3. This one: the accumulation is told with LIGHT THAT HAS A SOURCE. Every loaded chamber has an
//      INTERNAL AMBER POCKET in the casing beside it (DynamiteCasing's pocket field), so stage 1 is
//      one hot point with a small warm pocket, stage 3 three pockets that do not quite meet, stage 5
//      a connected fill under a still-red casing. Heat leaks out under the strap's lower edge, the
//      seams soften warm, a chamber glows in a ten-pixel halo with a short vein of heat running down
//      into the strap. Movement is almost nil: no jitter at stages 0-3, a rare pressure settle at 4,
//      a rare controlled pulse at 5. RED STAYS DOMINANT AT EVERY STAGE.
//
// ONE BLOCK, ONE MAGAZINE. Cells are grouped by the card they came from (Core reports it). A graphite
// bridge closes the gap between two cubes of a run so the strap reads as one; strap ends round off
// only where the block ends, never across an empty cell. The block's longest run carries one chamber
// per possible charge - the cap Core reports, five today - recessed brass sockets, amber when loaded,
// thin ember notches when the run is too short to hold them. The pockets are laid out per BLOCK and
// handed to each cube in its own uv, so they cross cube boundaries and stop where the block does.
//
// A CHARGE IS A LOAD CYCLE (~0.42s): dark powder grains in two or three clusters drawn in on curves,
// the chamber igniting (dark -> ember -> amber, a 1.12 punch), its pocket BLOOMING in the casing
// (0.7 -> 1.0), heat running a short way along the strap, a percent of pressure, settle. The first
// time a block reaches its cap it LOCKS: every chamber and pocket brighter for a breath, the strap
// one pixel tighter, one wisp of soot.
//
// THE COOK-OFF: the repaint that emptied the cells raises a copy of the pack (Prepare, before Show),
// and when the line reaches its first cube (Begin) the loaded chambers fire in physical order ~30 ms
// apart while the pockets are drawn IN toward the centre (0.75, brighter), the pack compresses, and
// it blows - amber core never white, powder, red casing fragments, torn strap slivers, a broken
// secondary ring at the top stages - then ONE "+TOTAL" with a few powder flecks, and for a full block
// the chambers' light gathers into a seed that flies to the score.
//
// THE VIEW DECIDES NOTHING: PowderVisuals says which cells charged, their card, the charge, the cap
// and what a cube is worth; PowderPayoutVisuals says what went up, with what charge, and what paid.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Draws "Barut tedarikçisi"'s powder as one magazine per dynamite block, and its
    /// cook-off. Owned by GameUiController, which hands it the reports.</summary>
    public sealed class PowderMagazineView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>The gunpowder* numbers the design names. Pixels are SCREEN pixels; seconds
        /// are seconds; stage tables run 0..5.</summary>
        public static class Style
        {
            // ---- the primer chambers ----
            public static float PrimerSizePixels = 7f;
            public static float PrimerMinPixels = 4f;
            public static float PrimerStrapShare = 0.82f;
            public static float PrimerJitter = 0.06f;
            public static float PrimerActiveBrightness = 1f;
            /// <summary>gunpowderPrimerHalo: radius in pixels and opacity.</summary>
            public static float PrimerHaloPixels = 10f;
            public static float PrimerHaloAlpha = 0.13f;
            public static float PrimerVeinAlpha = 0.45f;

            // ---- the load cycle ----
            public static float LoadDuration = 0.42f;
            public static int GrainCount = 8;
            public static float GrainTravel = 0.12f;
            public static float GrainStagger = 0.05f;
            public static float GrainRadiusMinPixels = 8f;
            public static float GrainRadiusMaxPixels = 18f;
            public static float IgnitionAt = 0.18f;
            public static float IgnitionDuration = 0.09f;
            public static float IgnitionPunch = 0.12f;
            /// <summary>The new pocket blooming after its chamber ignites.</summary>
            public static float PocketBloomDuration = 0.15f;
            public static float PocketBloomFrom = 0.7f;
            public static float StrapHeatAt = 0.22f;
            public static float StrapHeatDuration = 0.12f;
            public static float StrapHeatStrength = 0.16f;
            public static float StrapHeatReach = 1.5f;
            public static float PressureAt = 0.30f;
            public static float PressureDuration = 0.09f;
            public static float PressureSwell = 0.008f;
            public static float PressureCinch = 0.03f;

            // ---- the stage look (gunpowderInternalFillStageN, gunpowderPocketRadius, ...) ----
            public static float[] PocketStrength = { 0f, 0.05f, 0.08f, 0.13f, 0.18f, 0.22f };
            /// <summary>As a share of a cube: small and separate at 3, meeting at 4, one fill at 5.
            /// </summary>
            public static float[] PocketRadius = { 0f, 0.24f, 0.27f, 0.31f, 0.37f, 0.45f };
            public static float[] Fill = { 0f, 0f, 0f, 0.02f, 0.05f, 0.1f };
            public static float[] Embers = { 0f, 0.025f, 0.03f, 0.04f, 0.05f, 0.06f };
            public static float[] Underglow = { 0f, 0.05f, 0.08f, 0.11f, 0.15f, 0.18f };
            public static float[] SeamHeat = { 0f, 0.02f, 0.05f, 0.08f, 0.12f, 0.16f };
            public static float[] ChannelHeat = { 0f, 0.02f, 0.04f, 0.07f, 0.1f, 0.13f };
            public static float[] Soot = { 0f, 0f, 0.2f, 0.4f, 0.6f, 0.75f };
            public static float[] StrapTension = { 0f, 0.1f, 0.2f, 0.35f, 0.5f, 0.65f };
            public static float[] Warmth = { 0f, 0f, 0.01f, 0.02f, 0.03f, 0.04f };
            public static float[] Crease = { 0f, 0f, 0f, 0f, 0.5f, 0.8f };
            public static float[] HeatDistortionPixels = { 0f, 0f, 0f, 0f, 0.4f, 0.8f };

            // ---- idle: rare, and nothing trembles ----
            public static float FlickMin = 2.0f;
            public static float FlickMax = 3.5f;
            public static float FlickBoost = 0.10f;
            public static float SeamPulseMin = 4f;
            public static float SeamPulseMax = 6f;
            public static float EmberDriftMin = 2.5f;
            public static float EmberDriftMax = 4f;
            public static float SettleIntervalMin = 3f;
            public static float SettleIntervalMax = 5f;
            /// <summary>gunpowderPressurePulseInterval / Strength: stage 5's rare controlled pulse
            /// (scaleX 1.006, scaleY 0.997), stage 4's settle is half of it.</summary>
            public static float PressurePulseIntervalMin = 2.5f;
            public static float PressurePulseIntervalMax = 4f;
            public static float PressurePulseDuration = 0.15f;
            public static float PressurePulseStrength = 1f;
            public static float PressurePulseX = 0.006f;
            public static float PressurePulseY = 0.003f;
            public static float SweepSpacing = 0.03f;
            public static float MaxSmokeIntervalMin = 4f;
            public static float MaxSmokeIntervalMax = 7f;

            // ---- the max lock ----
            public static float LockDuration = 0.21f;
            public static float LockSwell = 0.01f;

            // ---- the cook-off ----
            public static float DetonationPrimerDelay = 0.03f;
            public static float DetonationCompressionTime = 0.08f;
            public static float DetonationCompression = 0.025f;
            public static float DetonationOvershoot = 0.015f;
            /// <summary>The heat drawn IN before the blast: the pockets' scale and brightness.</summary>
            public static float PocketCompression = 0.75f;
            public static float PocketCompressionBoost = 0.2f;
            public static float ExplosionSupportIntensity = 0.06f;
            public static float SecondaryRingDelay = 0.08f;
            public static float TextDelay = 0.1f;
            public static float RewardHold = 0.35f;
            public static float RewardFlight = 0.35f;
            public static float TextHold = 0.9f;

            // ---- the previous pass, for the lab's before/after ----
            public static float[] OldSeamShare = { 0f, 0.08f, 0.2f, 0.4f, 0.6f, 0.75f };
            public static float[] OldHazePixels = { 0f, 0f, 0f, 0f, 0.5f, 0.9f };

            public static readonly Color PrimerAmber = new Color(1f, 0.64f, 0.26f);
            public static readonly Color PrimerEmber = new Color(0.55f, 0.18f, 0.06f);
            public static readonly Color PrimerHot = new Color(1f, 0.8f, 0.45f);
            public static readonly Color Brass = new Color(0.52f, 0.41f, 0.24f);
            public static readonly Color SocketDark = new Color(0.09f, 0.07f, 0.06f);
            public static readonly Color Graphite = new Color(0.11f, 0.115f, 0.13f);
            public static readonly Color CasingRed = new Color(0.62f, 0.13f, 0.14f);
            public static readonly Color GrainDark = new Color(0.2f, 0.13f, 0.09f);
            public static readonly Color Smoke = new Color(0.22f, 0.18f, 0.16f);
            public static readonly Color BlastCore = new Color(1f, 0.84f, 0.52f);
            public static readonly Color TextIvory = new Color(1f, 0.95f, 0.84f);
            public static readonly Color TextAmber = new Color(1f, 0.7f, 0.28f);
            public static readonly Color TextShadow = new Color(0.2f, 0.09f, 0.04f);
            public static readonly Color ScoreInk = new Color(1f, 0.74f, 0.36f);
        }

        /// <summary>The lab's switches. FEATURES are on by default, OVERLAYS off. A switch that is
        /// off is off in the game too, which is the price of the lab driving the real view - "all
        /// back on" is the reset.</summary>
        public static class Layers
        {
            public static bool ShowPrimerSystem = true;
            public static bool ShowPockets = true;
            public static bool ShowStrapUnderglow = true;
            public static bool ShowSeamHeat = true;
            public static bool ShowStrapTension = true;
            public static bool ShowPowderGrains = true;
            public static bool ShowSootMask = true;
            public static bool ShowPressurePulse = true;
            public static bool ShowHeatDistortion = true;

            public static bool ShowGunpowderGroupBounds;
            public static bool ShowChargeStage;
            public static bool ShowPrimerStates;
            public static bool ShowDetonationEnergyFlow;
            public static bool ShowRewardValue;

            /// <summary>The lab's phase isolation for the load cycle.</summary>
            public static LoadPhase OnlyPhase = LoadPhase.All;

            public static void AllOn()
            {
                ShowPrimerSystem = true;
                ShowPockets = true;
                ShowStrapUnderglow = true;
                ShowSeamHeat = true;
                ShowStrapTension = true;
                ShowPowderGrains = true;
                ShowSootMask = true;
                ShowPressurePulse = true;
                ShowHeatDistortion = true;
                ShowGunpowderGroupBounds = false;
                ShowChargeStage = false;
                ShowPrimerStates = false;
                ShowDetonationEnergyFlow = false;
                ShowRewardValue = false;
                OnlyPhase = LoadPhase.All;
            }

            /// <summary>The features as they stand, so an "only" scene can put them back.</summary>
            public static bool[] CaptureFeatures()
            {
                return new[]
                {
                    ShowPrimerSystem, ShowPockets, ShowStrapUnderglow, ShowSeamHeat, ShowStrapTension,
                    ShowPowderGrains, ShowSootMask, ShowPressurePulse, ShowHeatDistortion
                };
            }

            public static void RestoreFeatures(bool[] f)
            {
                if (f == null || f.Length < 9)
                {
                    return;
                }
                ShowPrimerSystem = f[0];
                ShowPockets = f[1];
                ShowStrapUnderglow = f[2];
                ShowSeamHeat = f[3];
                ShowStrapTension = f[4];
                ShowPowderGrains = f[5];
                ShowSootMask = f[6];
                ShowPressurePulse = f[7];
                ShowHeatDistortion = f[8];
            }

            /// <summary>Every FEATURE off but one - the lab's "only" scenes.</summary>
            public static void Only(Feature feature)
            {
                ShowPrimerSystem = feature == Feature.Primers;
                ShowPockets = feature == Feature.Pockets;
                ShowStrapUnderglow = feature == Feature.Underglow;
                ShowSeamHeat = feature == Feature.SeamHeat;
                ShowPressurePulse = feature == Feature.PressurePulse;
                ShowHeatDistortion = feature == Feature.HeatDistortion;
                ShowStrapTension = feature == Feature.PressurePulse;
                ShowPowderGrains = false;
                ShowSootMask = false;
            }
        }

        public enum Feature
        {
            Primers,
            Pockets,
            Underglow,
            SeamHeat,
            PressurePulse,
            HeatDistortion
        }

        /// <summary>The load cycle's beats, for the lab's one-beat-at-a-time scenes.</summary>
        public enum LoadPhase
        {
            All,
            Grains,
            Ignition,
            StrapHeat,
            Pressure
        }

        private static bool PhaseOn(LoadPhase phase)
        {
            return Layers.OnlyPhase == LoadPhase.All || Layers.OnlyPhase == phase;
        }

        /// <summary>The moments audio can hang off.</summary>
        public enum Cue
        {
            ChargeLoad,
            PrimerIgnite,
            Maxed,
            DetonationPrep,
            Detonation,
            Reward
        }

        /// <summary>Fired on every Cue with how full the block was (0..1).</summary>
        public System.Action<Cue, float> Sounded;

        /// <summary>Where the score label is, in WORLD space - asked, never written down.</summary>
        public System.Func<Vector2> ScoreAnchor;

        /// <summary>The playback rate of what is started NEXT. The game always sets 1; the lab
        /// sets 0.5 / 0.25, so a slow lab run can never leak into a round.</summary>
        public float PlaybackRate = 1f;

        public float ScoreClaim { get; private set; }

        public float ScoreScale { get; private set; } = 1f;

        public Color ScoreInk
        {
            get { return Style.ScoreInk; }
        }

        public bool Busy
        {
            get
            {
                if (detonations.Count > 0 || texts.Count > 0 || rewardCore != null)
                {
                    return true;
                }
                foreach (Magazine m in magazines.Values)
                {
                    if (m.LoadClock >= 0f)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        // =================================================================== the tile's geometry
        // Measured off block_dynamite.png; the same numbers DynamiteCasing.shader carries.
        private const float BandMid = 0.504f;
        private const float BandHeight = 0.192f;
        private const float StrapWidth = 0.78f;
        private const float SeamA = 0.325f;
        private const float SeamB = 0.669f;

        // Sorting: the board's cubes are on 1.
        private const int ProxyOrder = 3;
        private const int BridgeOrder = 4;
        private const int VeinOrder = 4;
        private const int SocketOrder = 5;
        private const int CoreOrder = 6;
        private const int HaloOrder = 7;
        private const int BitOrder = 8;
        private const int BlastOrder = 18;
        private const int TextOrder = 24;
        private const int DebugOrder = 26;

        // =================================================================== state

        private sealed class Primer
        {
            public Vector2 At;
            public int Side;
            public SpriteRenderer Socket;
            public SpriteRenderer Core;
            public SpriteRenderer Halo;
            public SpriteRenderer Vein;
            public float Flick = -1f;
            public float FlickBoost;
        }

        private sealed class Bridge
        {
            public Vector2 At;
            public float Width;
            public SpriteRenderer R;
            public SpriteRenderer Heat;
        }

        /// <summary>One block's pack.</summary>
        private sealed class Magazine
        {
            public int CardId;
            public readonly List<GridPos> Cells = new List<GridPos>();
            public readonly HashSet<GridPos> CellSet = new HashSet<GridPos>();
            public int Charges;
            public int Cap;
            public bool Full;
            public long CubeValue;
            public Rect Bounds;
            public bool OldLook;

            public int RailRow;
            public float Spacing;
            public float PrimerSize;
            public bool Notch;
            public float StrapHeight;
            public readonly List<Primer> Primers = new List<Primer>();
            public readonly List<Bridge> Bridges = new List<Bridge>();
            public string LayoutKey;

            public float LoadClock = -1f;
            public int LoadPrimer;
            public bool LockPending;
            public float LockClock = -1f;
            public float Rate = 1f;

            public float FlickWait;
            public float StageWait;
            public float StageClock = -1f;
            public int StageTarget;
            public float SmokeWait;
            public int EventCount;

            public float Cool = -1f;

            public readonly List<SpriteRenderer> DebugBars = new List<SpriteRenderer>();
            public TextMesh Label;
        }

        /// <summary>One block going up.</summary>
        private sealed class Detonation
        {
            public int CardId;
            public readonly List<GridPos> Cells = new List<GridPos>();
            public readonly HashSet<GridPos> CellSet = new HashSet<GridPos>();
            public readonly List<SpriteRenderer> Proxies = new List<SpriteRenderer>();
            public readonly List<Primer> Primers = new List<Primer>();
            public readonly List<Bridge> Bridges = new List<Bridge>();
            public float PrimerSize;
            public bool Notch;
            public int Charges;
            public int Cap;
            public int Stage;
            public bool Full;
            public Vector2 Centre;
            public Rect Bounds;
            public float StrapHeight;
            public float Clock = -1f;
            public float Waiting;
            public float Trigger;
            public float BlastAt;
            public bool Blasted;
            public bool Prepped;
            public int Fired;
            public float Rate = 1f;
        }

        private sealed class Bit
        {
            public SpriteRenderer R;
            public float Age;
            public float Delay;
            public float Life;
            public Vector2 From;
            public Vector2 Ctrl;
            public Vector2 To;
            public Vector2 Velocity;
            public float Drag;
            public float Gravity;
            public float Size;
            public float EndSize;
            public float Aspect = 1f;
            public float Spin;
            public float Angle;
            public Color Colour;
            public Color EndColour;
            public float Alpha;
            public int Kind;
            public float Rate = 1f;
        }

        private const int KindCurve = 0;
        private const int KindBallistic = 1;
        private const int KindStill = 2;
        private const int KindRing = 3;
        private const int KindRise = 4;
        private const int KindDrift = 5;

        private sealed class FloatingTotal
        {
            public readonly List<TextMesh> Layers = new List<TextMesh>();
            public Transform Root;
            public float Age;
            public float Delay;
            public float Rate = 1f;
        }

        private sealed class RewardSeed
        {
            public SpriteRenderer R;
            public Vector2 Gather;
            public float Age;
            public float Rate = 1f;
        }

        private readonly Dictionary<int, Magazine> magazines = new Dictionary<int, Magazine>();
        private readonly List<Detonation> detonations = new List<Detonation>();
        private readonly List<Bit> bits = new List<Bit>();
        private readonly List<FloatingTotal> texts = new List<FloatingTotal>();
        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();
        private readonly HashSet<SpriteRenderer> casingWritten = new HashSet<SpriteRenderer>();
        private readonly HashSet<SpriteRenderer> casingThisFrame = new HashSet<SpriteRenderer>();
        private readonly List<int> scratchIds = new List<int>();
        private readonly List<Vector4> pockets = new List<Vector4>();
        private readonly Vector4[] cubePockets = new Vector4[5];
        private readonly HashSet<int> oldLookCards = new HashSet<int>();

        private BoardView board;
        private GameBoard lastBoard;
        private MaterialPropertyBlock block;
        private RewardSeed rewardCore;
        private float scoreClock = -1f;
        private float scoreDelay;
        private float scoreRate = 1f;
        private float knockAt = -1f;
        private float clockTime;
        private PowderPayoutVisuals pendingPayout;
        private long pendingPoints;
        private bool pendingFull;
        private int pendingStage;
        private readonly List<Vector2> fullPrimerSpots = new List<Vector2>();
        private readonly List<Vector2> blastCentres = new List<Vector2>();

        public void Build(BoardView boardView)
        {
            board = boardView;
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
        }

        private void Awake()
        {
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
        }

        // =================================================================== driving it

        /// <summary>
        /// Restates every block's magazine from the turn's report. <paramref name="fresh"/> is true
        /// the first time a report is seen (the caller matches by identity): only then does a block
        /// that GAINED play its load cycle. A block that is no longer reported and was not claimed
        /// by a cook-off cools down; a new board (a new round) takes everything down at once.
        /// </summary>
        public void Show(PowderVisuals report, bool fresh)
        {
            if (board == null)
            {
                return;
            }
            if (board.Board != lastBoard)
            {
                lastBoard = board.Board;
                DropAllMagazines();
            }
            scratchIds.Clear();
            if (report != null)
            {
                var groups = new Dictionary<int, List<int>>();
                for (int i = 0; i < report.Count; i++)
                {
                    int key = i < report.CardIds.Count ? report.CardIds[i] : -1;
                    if (key < 0)
                    {
                        key = -1000 - report.Charges[i];
                    }
                    List<int> list;
                    if (!groups.TryGetValue(key, out list))
                    {
                        list = new List<int>();
                        groups[key] = list;
                    }
                    list.Add(i);
                }
                foreach (KeyValuePair<int, List<int>> g in groups)
                {
                    Magazine m;
                    if (!magazines.TryGetValue(g.Key, out m))
                    {
                        m = new Magazine { CardId = g.Key };
                        magazines[g.Key] = m;
                        ScheduleIdle(m);
                    }
                    int first = g.Value[0];
                    m.Cells.Clear();
                    m.CellSet.Clear();
                    for (int k = 0; k < g.Value.Count; k++)
                    {
                        m.Cells.Add(report.Cells[g.Value[k]]);
                        m.CellSet.Add(report.Cells[g.Value[k]]);
                    }
                    m.Charges = report.Charges[first];
                    m.Cap = report.Cap > 0 ? report.Cap : Mathf.Max(1, m.Charges);
                    m.Full = report.Full[first];
                    m.CubeValue = first < report.CubeValues.Count ? report.CubeValues[first] : 0;
                    m.OldLook = oldLookCards.Contains(g.Key);
                    m.Cool = -1f;
                    Layout(m);
                    if (fresh && report.Gained[first])
                    {
                        StartLoad(m, report.Fullness[first]);
                    }
                    scratchIds.Add(g.Key);
                }
            }
            foreach (KeyValuePair<int, Magazine> entry in magazines)
            {
                if (!scratchIds.Contains(entry.Key) && entry.Value.Cool < 0f)
                {
                    entry.Value.Cool = 0f;
                    entry.Value.LoadClock = -1f;
                }
            }
        }

        /// <summary>
        /// THE COOK-OFF, part one - on the repaint that has already emptied the cells. Every block
        /// the payout names is raised again as a copy of its pack at the charge it went up with,
        /// and its standing magazine (if any) hands over to it at once.
        /// </summary>
        public void Prepare(PowderPayoutVisuals payout)
        {
            if (payout == null || board == null || payout.Count == 0)
            {
                return;
            }
            pendingPayout = payout;
            pendingPoints = payout.Points;
            pendingFull = payout.AnyFull;
            pendingStage = 0;
            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < payout.Count; i++)
            {
                int key = i < payout.CardIds.Count ? payout.CardIds[i] : -1;
                if (key < 0)
                {
                    key = -2000 - (i < payout.Charges.Count ? payout.Charges[i] : 0);
                }
                List<int> list;
                if (!groups.TryGetValue(key, out list))
                {
                    list = new List<int>();
                    groups[key] = list;
                }
                list.Add(i);
            }
            foreach (KeyValuePair<int, List<int>> g in groups)
            {
                int first = g.Value[0];
                var d = new Detonation
                {
                    CardId = g.Key,
                    Charges = first < payout.Charges.Count
                        ? payout.Charges[first]
                        : Mathf.RoundToInt(payout.Fullness[first] * 5f),
                    Cap = payout.Cap > 0 ? payout.Cap : 5,
                    Full = payout.Full[first],
                    Rate = PlaybackRate
                };
                d.Stage = StageIndex(d.Charges, d.Cap);
                pendingStage = Mathf.Max(pendingStage, d.Stage);
                for (int k = 0; k < g.Value.Count; k++)
                {
                    d.Cells.Add(payout.Cells[g.Value[k]]);
                    d.CellSet.Add(payout.Cells[g.Value[k]]);
                }
                BuildDetonation(d);
                detonations.Add(d);
                Magazine m;
                if (magazines.TryGetValue(g.Key, out m))
                {
                    DropMagazine(m);
                    magazines.Remove(g.Key);
                }
            }
        }

        /// <summary>
        /// THE COOK-OFF, part two - when the turn's explosion plays. <paramref name="delays"/> is
        /// when each payout cell's cube breaks on screen (same order as the payout): a block starts
        /// cooking off when the first of its cubes is reached.
        /// </summary>
        public void Begin(PowderPayoutVisuals payout, IReadOnlyList<float> delays)
        {
            if (payout == null || !ReferenceEquals(payout, pendingPayout))
            {
                return;
            }
            for (int i = 0; i < detonations.Count; i++)
            {
                Detonation d = detonations[i];
                if (d.Clock >= 0f)
                {
                    continue;
                }
                float trigger = float.MaxValue;
                for (int c = 0; c < payout.Count; c++)
                {
                    if (d.CellSet.Contains(payout.Cells[c]))
                    {
                        trigger = Mathf.Min(trigger,
                            delays != null && c < delays.Count ? delays[c] : 0f);
                    }
                }
                StartDetonation(d, trigger == float.MaxValue ? 0f : trigger);
            }
        }

        /// <summary>The lab's before/after only: these blocks are drawn the PREVIOUS pass's way.
        /// The game never calls this.</summary>
        public void SetOldLook(IEnumerable<int> cardIds)
        {
            oldLookCards.Clear();
            if (cardIds != null)
            {
                foreach (int id in cardIds)
                {
                    oldLookCards.Add(id);
                }
            }
        }

        /// <summary>The lab: every standing pack's next stage event and a chamber flick come almost
        /// at once, instead of in their rare natural seconds.</summary>
        public void ForceIdle()
        {
            foreach (Magazine m in magazines.Values)
            {
                m.StageWait = 0.15f;
                m.FlickWait = 0.4f;
                m.SmokeWait = 0.9f;
            }
        }

        /// <summary>The lab: the FULL MAGAZINE LOCK on every standing pack, alone.</summary>
        public void ForceLock()
        {
            foreach (Magazine m in magazines.Values)
            {
                m.LoadClock = -1f;
                m.LockPending = false;
                m.LockClock = 0f;
                m.Rate = PlaybackRate;
                Emit(Cue.Maxed, 1f);
                Smoke(m, m.Rate);
            }
        }

        /// <summary>Takes everything down - a new round, the lab's reset.</summary>
        public void Clear()
        {
            oldLookCards.Clear();
            DropAllMagazines();
            for (int i = 0; i < detonations.Count; i++)
            {
                DropDetonation(detonations[i]);
            }
            detonations.Clear();
            for (int i = 0; i < bits.Count; i++)
            {
                Return(bits[i].R);
            }
            bits.Clear();
            for (int i = 0; i < texts.Count; i++)
            {
                if (texts[i].Root != null)
                {
                    Destroy(texts[i].Root.gameObject);
                }
            }
            texts.Clear();
            DropReward();
            ClearCasing();
            scoreClock = -1f;
            ScoreClaim = 0f;
            ScoreScale = 1f;
            pendingPayout = null;
            fullPrimerSpots.Clear();
            blastCentres.Clear();
            if (knockAt >= 0f && board != null)
            {
                board.SetImpulse(Vector2.zero);
            }
            knockAt = -1f;
        }

        // =================================================================== layout

        private static int StageIndex(int charges, int cap)
        {
            if (cap <= 0 || charges <= 0)
            {
                return 0;
            }
            return Mathf.Clamp(Mathf.RoundToInt(charges * 5f / cap), 0, 5);
        }

        private float Cube
        {
            get { return board != null ? board.CubeWorldSize : 1f; }
        }

        private float CellSize
        {
            get { return board != null ? board.CellWorldSize : 1f; }
        }

        /// <summary>A number of screen pixels in this view's own units.</summary>
        private float Px(float pixels)
        {
            Camera cam = Camera.main;
            float scale = Mathf.Abs(transform.lossyScale.y);
            if (scale < 1e-5f)
            {
                scale = 1f;
            }
            if (cam == null || !cam.orthographic || Screen.height <= 0)
            {
                return pixels * Cube / 70f;
            }
            return pixels * 2f * cam.orthographicSize / Screen.height / scale;
        }

        private float StrapCentreY(int row)
        {
            return board.CellToWorld(new GridPos(0, row)).y + (BandMid - 0.5f) * Cube;
        }

        /// <summary>
        /// Where a block's straps and chambers go: a bridge over every gap inside a run, and the
        /// chamber rail on the LONGEST run - ties to the run nearest the block's middle row. Rebuilt
        /// only when the block's cells, its cap or the arena's geometry change.
        /// </summary>
        private void Layout(Magazine m)
        {
            Vector2 origin = board.CellToWorld(new GridPos(0, 0));
            string key = LayoutKeyOf(m.Cells, m.Cap) + "|" + CellSize.ToString("0.0000") + "|"
                + origin.x.ToString("0.000") + "," + origin.y.ToString("0.000")
                + (m.OldLook ? "|old" : "");
            if (key == m.LayoutKey)
            {
                return;
            }
            ClearLayout(m.Primers, m.Bridges);
            m.LayoutKey = key;
            float strapH = Cube * BandHeight * StrapWidth;
            m.StrapHeight = strapH;
            m.Bounds = BoundsOf(m.Cells);
            int row, x0, x1;
            ChooseRail(m.Cells, out row, out x0, out x1);
            m.RailRow = row;
            BuildBridges(m.Cells, m.Bridges);
            float spacing;
            float size;
            bool notch;
            PlacePrimers(m.Primers, row, x0, x1, m.Cap, strapH, m.CardId, m.OldLook, out spacing,
                out size, out notch);
            m.Spacing = spacing;
            m.PrimerSize = size;
            m.Notch = notch;
        }

        private static string LayoutKeyOf(List<GridPos> cells, int cap)
        {
            var sorted = new List<GridPos>(cells);
            sorted.Sort(delegate (GridPos a, GridPos b)
            {
                return a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
            });
            var sb = new System.Text.StringBuilder();
            sb.Append(cap).Append(':');
            for (int i = 0; i < sorted.Count; i++)
            {
                sb.Append(sorted[i].X).Append(',').Append(sorted[i].Y).Append(';');
            }
            return sb.ToString();
        }

        private Rect BoundsOf(List<GridPos> cells)
        {
            float half = Cube * 0.5f;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2 p = board.CellToWorld(cells[i]);
                minX = Mathf.Min(minX, p.x - half);
                minY = Mathf.Min(minY, p.y - half);
                maxX = Mathf.Max(maxX, p.x + half);
                maxY = Mathf.Max(maxY, p.y + half);
            }
            return cells.Count == 0 ? new Rect(0f, 0f, 0f, 0f)
                : new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private static void ChooseRail(List<GridPos> cells, out int row, out int x0, out int x1)
        {
            row = 0;
            x0 = 0;
            x1 = -1;
            if (cells.Count == 0)
            {
                return;
            }
            int minY = int.MaxValue, maxY = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                minY = Mathf.Min(minY, cells[i].Y);
                maxY = Mathf.Max(maxY, cells[i].Y);
            }
            float midRow = (minY + maxY) * 0.5f;
            int bestLen = -1;
            float bestDist = float.MaxValue;
            for (int y = minY; y <= maxY; y++)
            {
                var xs = new List<int>();
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].Y == y)
                    {
                        xs.Add(cells[i].X);
                    }
                }
                xs.Sort();
                int s = 0;
                while (s < xs.Count)
                {
                    int e = s;
                    while (e + 1 < xs.Count && xs[e + 1] == xs[e] + 1)
                    {
                        e++;
                    }
                    int len = e - s + 1;
                    float dist = Mathf.Abs(y - midRow);
                    if (len > bestLen || (len == bestLen && dist < bestDist))
                    {
                        bestLen = len;
                        bestDist = dist;
                        row = y;
                        x0 = xs[s];
                        x1 = xs[e];
                    }
                    s = e + 1;
                }
            }
        }

        private void BuildBridges(List<GridPos> cells, List<Bridge> bridges)
        {
            var set = new HashSet<GridPos>(cells);
            float gap = CellSize - Cube;
            if (gap <= 1e-5f)
            {
                return;
            }
            foreach (GridPos c in cells)
            {
                var right = new GridPos(c.X + 1, c.Y);
                if (!set.Contains(right))
                {
                    continue;
                }
                Vector2 a = board.CellToWorld(c);
                Vector2 b = board.CellToWorld(right);
                var br = new Bridge
                {
                    At = new Vector2((a.x + b.x) * 0.5f, StrapCentreY(c.Y)),
                    Width = gap + Cube * 0.05f
                };
                br.R = Rent(StrapBarSprite, BridgeOrder);
                bridges.Add(br);
            }
        }

        private void PlacePrimers(List<Primer> primers, int row, int x0, int x1, int cap,
            float strapH, int seedKey, bool old, out float spacing, out float size, out bool notch)
        {
            spacing = 0f;
            size = 0f;
            notch = false;
            if (x1 < x0 || cap <= 0)
            {
                return;
            }
            float left = board.CellToWorld(new GridPos(x0, row)).x - Cube * 0.5f + Cube * 0.1f;
            float right = board.CellToWorld(new GridPos(x1, row)).x + Cube * 0.5f - Cube * 0.1f;
            spacing = (right - left) / cap;
            float want = Px(old ? 6f : Style.PrimerSizePixels);
            size = Mathf.Min(want, strapH * (old ? 0.64f : Style.PrimerStrapShare), spacing * 0.64f);
            // SMALL MODE: a chamber too small to read, or crowded, becomes an ember notch.
            notch = size < Px(Style.PrimerMinPixels) || spacing < size * 1.3f;
            float y = StrapCentreY(row);
            for (int i = 0; i < cap; i++)
            {
                float jitter = (Hash(seedKey, 40 + i) - 0.5f) * 2f * Style.PrimerJitter * spacing;
                var p = new Primer
                {
                    At = new Vector2(left + spacing * (i + 0.5f) + jitter, y),
                    // The pockets go into the casing on alternate sides of the strap.
                    Side = (i + (int)(Hash(seedKey, 60) * 2f)) % 2 == 0 ? 1 : -1
                };
                p.Socket = Rent(notch ? NotchSprite : SocketSprite, SocketOrder);
                p.Core = Rent(notch ? NotchSprite : CoreSprite, CoreOrder);
                p.Halo = Rent(DotSprite, HaloOrder);
                if (!old)
                {
                    p.Vein = Rent(VeinSprite, VeinOrder);
                }
                primers.Add(p);
            }
        }

        private void ClearLayout(List<Primer> primers, List<Bridge> bridges)
        {
            for (int i = 0; i < primers.Count; i++)
            {
                Return(primers[i].Socket);
                Return(primers[i].Core);
                Return(primers[i].Halo);
                Return(primers[i].Vein);
            }
            primers.Clear();
            for (int i = 0; i < bridges.Count; i++)
            {
                Return(bridges[i].R);
                Return(bridges[i].Heat);
            }
            bridges.Clear();
        }

        private void DropMagazine(Magazine m)
        {
            ClearLayout(m.Primers, m.Bridges);
            for (int i = 0; i < m.DebugBars.Count; i++)
            {
                Return(m.DebugBars[i]);
            }
            m.DebugBars.Clear();
            if (m.Label != null)
            {
                Destroy(m.Label.gameObject);
                m.Label = null;
            }
            m.LayoutKey = null;
        }

        private void DropAllMagazines()
        {
            foreach (Magazine m in magazines.Values)
            {
                DropMagazine(m);
            }
            magazines.Clear();
        }

        // =================================================================== the load cycle

        private void StartLoad(Magazine m, float fullness)
        {
            m.Rate = PlaybackRate;
            m.LoadClock = 0f;
            m.LoadPrimer = Mathf.Clamp(m.Charges - 1, 0, Mathf.Max(0, m.Primers.Count - 1));
            m.LockPending = m.Full;
            m.StageClock = -1f;
            Emit(Cue.ChargeLoad, fullness);
            if (!Layers.ShowPowderGrains || !PhaseOn(LoadPhase.Grains) || m.Primers.Count == 0)
            {
                return;
            }
            // Two or three small clusters round the block, never one even ring.
            Vector2 target = m.Primers[m.LoadPrimer].At;
            Vector2 centre = m.Bounds.center;
            float halfDiag = Mathf.Max(m.Bounds.width, m.Bounds.height) * 0.5f;
            int clusters = 2 + (int)(Hash(m.CardId, 60 + m.Charges) * 2f);
            int count = Mathf.Clamp(Style.GrainCount, 6, 10);
            for (int g = 0; g < count; g++)
            {
                int cl = g % clusters;
                float baseA = Hash(m.CardId, 70 + cl + m.Charges * 3) * Mathf.PI * 2f;
                float a = baseA + (Hash(m.CardId, 90 + g) - 0.5f) * 0.6f;
                float r = halfDiag * 0.85f + Mathf.Lerp(Px(Style.GrainRadiusMinPixels),
                    Px(Style.GrainRadiusMaxPixels), Hash(m.CardId, 110 + g));
                Vector2 from = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                Vector2 to = target + new Vector2((Hash(m.CardId, 130 + g) - 0.5f) * m.Spacing * 0.8f,
                    (Hash(m.CardId, 150 + g) - 0.5f) * m.StrapHeight * 0.5f);
                Vector2 mid = (from + to) * 0.5f;
                Vector2 perp = new Vector2(-(to - from).y, (to - from).x).normalized;
                var b = NewBit(KindCurve, DotSprite, BitOrder, m.Rate);
                b.From = from;
                b.To = to;
                b.Ctrl = mid + perp * ((Hash(m.CardId, 170 + g) - 0.5f) * r * 0.7f);
                b.Delay = 0.02f + (g / (float)count) * Style.GrainStagger;
                b.Life = Style.GrainTravel * Mathf.Lerp(0.85f, 1.25f, Hash(m.CardId, 190 + g));
                b.Size = Px(Mathf.Lerp(1.4f, 3f, Hash(m.CardId, 210 + g)));
                b.EndSize = b.Size * 0.7f;
                bool hot = g % 4 == 1;
                b.Colour = hot ? Color.Lerp(Style.GrainDark, Style.PrimerAmber, 0.6f) : Style.GrainDark;
                b.EndColour = hot ? Style.PrimerAmber : Color.Lerp(Style.GrainDark, Style.PrimerEmber, 0.4f);
                b.Alpha = 0.95f;
            }
        }

        // =================================================================== idle

        private void ScheduleIdle(Magazine m)
        {
            m.FlickWait = Mathf.Lerp(Style.FlickMin, Style.FlickMax, Hash(m.CardId, 300 + m.EventCount));
            m.StageWait = 3f + Hash(m.CardId, 320 + m.EventCount) * 2f;
            m.SmokeWait = Mathf.Lerp(Style.MaxSmokeIntervalMin, Style.MaxSmokeIntervalMax,
                Hash(m.CardId, 340 + m.EventCount));
        }

        private float StageInterval(Magazine m, int stage)
        {
            float k = Hash(m.CardId, 400 + m.EventCount);
            switch (stage)
            {
                case 2: return Mathf.Lerp(Style.SeamPulseMin, Style.SeamPulseMax, k);
                case 3: return Mathf.Lerp(Style.EmberDriftMin, Style.EmberDriftMax, k);
                case 4: return Mathf.Lerp(Style.SettleIntervalMin, Style.SettleIntervalMax, k);
                default: return Mathf.Lerp(Style.PressurePulseIntervalMin, Style.PressurePulseIntervalMax, k);
            }
        }

        private static float StageEventLength(int stage)
        {
            return stage == 2 ? 0.5f : stage >= 4 ? Style.PressurePulseDuration + 0.16f : 0.3f;
        }

        // =================================================================== the clock

        private void Update()
        {
            float dt = Time.deltaTime;
            clockTime += dt;
            TickMagazines(dt);
            TickDetonations(dt);
            TickBits(dt);
            TickTexts(dt);
            TickReward(dt);
            TickScore(dt);
            TickKnock();
        }

        private void LateUpdate()
        {
            // AFTER the board's own repaint: the property blocks go on the renderers it just
            // painted, and whatever this view no longer owns is handed back clean.
            casingThisFrame.Clear();
            if (board != null && board.Board != null)
            {
                foreach (Magazine m in magazines.Values)
                {
                    PaintMagazine(m);
                }
            }
            for (int i = 0; i < detonations.Count; i++)
            {
                PaintDetonation(detonations[i]);
            }
            foreach (SpriteRenderer r in casingWritten)
            {
                if (r != null && !casingThisFrame.Contains(r))
                {
                    r.SetPropertyBlock(null);
                }
            }
            casingWritten.Clear();
            foreach (SpriteRenderer r in casingThisFrame)
            {
                casingWritten.Add(r);
            }
        }

        private void ClearCasing()
        {
            foreach (SpriteRenderer r in casingWritten)
            {
                if (r != null)
                {
                    r.SetPropertyBlock(null);
                }
            }
            casingWritten.Clear();
        }

        private void TickMagazines(float dt)
        {
            scratchIds.Clear();
            foreach (KeyValuePair<int, Magazine> entry in magazines)
            {
                Magazine m = entry.Value;
                float mdt = dt * m.Rate;
                if (m.Cool >= 0f)
                {
                    m.Cool += mdt;
                    if (m.Cool > 0.15f)
                    {
                        scratchIds.Add(entry.Key);
                    }
                    continue;
                }
                if (m.LoadClock >= 0f)
                {
                    float before = m.LoadClock;
                    m.LoadClock += mdt;
                    if (before < Style.IgnitionAt && m.LoadClock >= Style.IgnitionAt)
                    {
                        Emit(Cue.PrimerIgnite, m.Cap > 0 ? m.Charges / (float)m.Cap : 1f);
                        if (m.LoadPrimer < m.Primers.Count && PhaseOn(LoadPhase.Ignition))
                        {
                            Fleck(m.Primers[m.LoadPrimer].At, m.Rate);
                        }
                    }
                    if (m.LoadClock >= Style.LoadDuration)
                    {
                        m.LoadClock = -1f;
                        if (m.LockPending)
                        {
                            m.LockPending = false;
                            m.LockClock = 0f;
                            Emit(Cue.Maxed, 1f);
                            Smoke(m, m.Rate);
                        }
                    }
                    continue;
                }
                if (m.LockClock >= 0f)
                {
                    m.LockClock += mdt;
                    if (m.LockClock > Style.LockDuration)
                    {
                        m.LockClock = -1f;
                    }
                }
                TickIdle(m, mdt);
            }
            for (int i = 0; i < scratchIds.Count; i++)
            {
                DropMagazine(magazines[scratchIds[i]]);
                magazines.Remove(scratchIds[i]);
            }
        }

        private void TickIdle(Magazine m, float dt)
        {
            int stage = StageIndex(m.Charges, m.Cap);
            if (stage <= 0)
            {
                return;
            }
            // A loaded chamber flicks now and then - stable heat, never a pulse.
            m.FlickWait -= dt;
            if (m.FlickWait <= 0f)
            {
                m.EventCount++;
                m.FlickWait = Mathf.Lerp(Style.FlickMin, Style.FlickMax, Hash(m.CardId, 300 + m.EventCount));
                int active = Mathf.Min(m.Charges, m.Primers.Count);
                if (active > 0)
                {
                    Primer p = m.Primers[(int)(Hash(m.CardId, 500 + m.EventCount) * active) % active];
                    p.Flick = 0f;
                    p.FlickBoost = Style.FlickBoost;
                }
            }
            for (int i = 0; i < m.Primers.Count; i++)
            {
                Primer p = m.Primers[i];
                if (p.Flick >= 0f)
                {
                    p.Flick += dt;
                    if (p.Flick > 0.14f)
                    {
                        p.Flick = -1f;
                    }
                }
            }
            if (stage < 2)
            {
                return;
            }
            if (m.StageClock >= 0f)
            {
                m.StageClock += dt;
                if (m.StageClock > StageEventLength(stage))
                {
                    m.StageClock = -1f;
                }
            }
            else
            {
                m.StageWait -= dt;
                if (m.StageWait <= 0f)
                {
                    m.EventCount++;
                    m.StageWait = StageInterval(m, stage);
                    m.StageClock = 0f;
                    int active = Mathf.Max(1, Mathf.Min(m.Charges, m.Primers.Count));
                    m.StageTarget = (int)(Hash(m.CardId, 600 + m.EventCount) * active) % active;
                    if (stage >= 3)
                    {
                        EmberDrift(m);
                    }
                    if (stage == 4 && m.StageTarget < m.Primers.Count)
                    {
                        m.Primers[m.StageTarget].Flick = 0f;
                        m.Primers[m.StageTarget].FlickBoost = 0.15f;
                    }
                }
            }
            if (stage >= 5)
            {
                m.SmokeWait -= dt;
                if (m.SmokeWait <= 0f)
                {
                    m.EventCount++;
                    m.SmokeWait = Mathf.Lerp(Style.MaxSmokeIntervalMin, Style.MaxSmokeIntervalMax,
                        Hash(m.CardId, 700 + m.EventCount));
                    Smoke(m, m.Rate);
                }
            }
        }

        // =================================================================== painting a pack

        /// <summary>
        /// The pockets of a pack, in world space: one per loaded chamber, in the casing on its side
        /// of the strap, at the stage's size and strength. <paramref name="bloomIndex"/> is the
        /// chamber whose pocket is still blooming (0.7 -> 1.0), <paramref name="toward"/> and
        /// <paramref name="inward"/> draw them in for the cook-off.
        /// </summary>
        private void CollectPockets(List<Primer> primers, int active, int stage, int bloomIndex,
            float bloom, float boost, Vector2 toward, float inward, float strapH)
        {
            // Always collected, even with the pockets switched off: the underglow, the seam heat
            // and the haze all follow this field. _PocketLight alone decides whether it is LIT.
            pockets.Clear();
            float radius = Style.PocketRadius[stage] * Cube;
            float strength = Style.PocketStrength[stage] * (1f + boost);
            for (int i = 0; i < primers.Count && i < active; i++)
            {
                Primer p = primers[i];
                Vector2 at = p.At + new Vector2((Hash(i, 80 + stage) - 0.5f) * Cube * 0.12f,
                    p.Side * (strapH * 0.5f + Cube * 0.17f));
                float r = radius;
                float s = strength;
                if (i == bloomIndex)
                {
                    r *= Mathf.Lerp(Style.PocketBloomFrom, 1f, bloom);
                    s *= bloom;
                }
                if (inward > 0f)
                {
                    at = Vector2.Lerp(at, toward, inward * (1f - Style.PocketCompression));
                    r *= Mathf.Lerp(1f, Style.PocketCompression, inward);
                    s *= 1f + Style.PocketCompressionBoost * inward;
                }
                pockets.Add(new Vector4(at.x, at.y, r, s));
            }
        }

        private void PaintMagazine(Magazine m)
        {
            int stage = StageIndex(m.Charges, m.Cap);
            float cool = m.Cool >= 0f ? Mathf.Clamp01(1f - m.Cool / 0.15f) : 1f;
            int shownActive = Mathf.Min(m.Charges, m.Primers.Count);
            float ignite = 1f;
            bool igniteOn = PhaseOn(LoadPhase.Ignition);
            int bloomIndex = -1;
            float bloom = 1f;
            if (m.LoadClock >= 0f)
            {
                ignite = Mathf.Clamp01((m.LoadClock - Style.IgnitionAt) / Style.IgnitionDuration);
                if (m.LoadClock < Style.IgnitionAt || !igniteOn)
                {
                    shownActive = Mathf.Min(shownActive, m.LoadPrimer);
                }
                // The new pocket fills after its chamber ignites: "a new powder pocket filled".
                bloomIndex = m.LoadPrimer;
                bloom = Mathf.Clamp01((m.LoadClock - Style.IgnitionAt) / Style.PocketBloomDuration);
            }
            // The pack at the stage it SHOWS: during a load the new pocket and chamber arrive with
            // the ignition, so the stage steps up with them rather than the frame the report lands.
            int shownStage = m.LoadClock >= 0f && m.LoadClock < Style.IgnitionAt
                ? StageIndex(Mathf.Max(0, m.Charges - 1), m.Cap)
                : stage;
            float pressure = 0f;
            if (m.LoadClock >= Style.PressureAt && PhaseOn(LoadPhase.Pressure))
            {
                float k = Mathf.Clamp01((m.LoadClock - Style.PressureAt) / Style.PressureDuration);
                pressure = Mathf.Sin(k * Mathf.PI);
            }
            float lockK = m.LockClock >= 0f
                ? Mathf.Sin(Mathf.Clamp01(m.LockClock / Style.LockDuration) * Mathf.PI)
                : 0f;
            // The strap heat of a load, in world x.
            float heatLo = 0f, heatHi = 0f, heatK = 0f;
            if (m.LoadClock >= Style.StrapHeatAt && m.LoadPrimer < m.Primers.Count
                && PhaseOn(LoadPhase.StrapHeat))
            {
                float k = Mathf.Clamp01((m.LoadClock - Style.StrapHeatAt) / Style.StrapHeatDuration);
                float reach = m.Spacing * Style.StrapHeatReach * EaseOut(k);
                float x = m.Primers[m.LoadPrimer].At.x;
                heatLo = x - reach;
                heatHi = x + reach;
                heatK = Style.StrapHeatStrength * (1f - k * k);
            }
            // The rare controlled pressure pulse (stage 5) or settle (stage 4) - and at 5 the
            // chamber sweep that rides it. NEVER a jitter.
            float pulse = 0f;
            float sweepAt = -1f;
            if (stage >= 4 && m.StageClock >= 0f && Layers.ShowPressurePulse && !m.OldLook)
            {
                float k = Mathf.Clamp01(m.StageClock / Style.PressurePulseDuration);
                pulse = Mathf.Sin(k * Mathf.PI) * Style.PressurePulseStrength * (stage >= 5 ? 1f : 0.5f);
                if (stage >= 5)
                {
                    sweepAt = m.StageClock / Mathf.Max(Style.SweepSpacing, 0.001f);
                }
            }
            float seamPulse = stage == 2 && m.StageClock >= 0f
                ? Mathf.Sin(Mathf.Clamp01(m.StageClock / 0.5f) * Mathf.PI) * 0.04f
                : 0f;

            if (!m.OldLook)
            {
                CollectPockets(m.Primers, shownActive + (bloomIndex >= 0 && bloomIndex >= shownActive
                    && igniteOn ? 1 : 0), Mathf.Max(shownStage, 1), bloomIndex, bloom, 0.1f * lockK,
                    Vector2.zero, 0f, m.StrapHeight);
            }
            else
            {
                pockets.Clear();
            }
            var squash = new Vector2(1f + Style.PressurePulseX * pulse + Style.LockSwell * lockK
                + Style.PressureSwell * pressure,
                1f - Style.PressurePulseY * pulse + Style.LockSwell * lockK + Style.PressureSwell * pressure);

            for (int i = 0; i < m.Cells.Count; i++)
            {
                GridPos cell = m.Cells[i];
                SpriteRenderer r = board.CellRendererAt(cell);
                if (r == null || r.sprite == null || r.sprite != DynamiteTile)
                {
                    continue;
                }
                bool left = m.CellSet.Contains(new GridPos(cell.X - 1, cell.Y));
                bool right = m.CellSet.Contains(new GridPos(cell.X + 1, cell.Y));
                Vector2 at = board.CellToWorld(cell);
                Vector4 front = Vector4.zero;
                if (heatK > 0f && cell.Y == m.RailRow)
                {
                    float x0 = at.x - Cube * 0.5f;
                    front = new Vector4((heatLo - x0) / Cube, (heatHi - x0) / Cube, heatK, 0f);
                }
                WriteCasing(r, at, shownStage, cool, left, right, seamPulse, front, squash,
                    0.3f * pressure + 0.3f * lockK + 0.35f * pulse, 0f, Hash(cell, 3), m.OldLook);
            }

            // ---- bridges: the strap across the gaps, cinched a hair by pressure ----
            float tension = Layers.ShowStrapTension ? Style.StrapTension[shownStage] : 0f;
            float cinch = 1f - Style.PressureCinch * pressure - 0.02f * lockK - 0.03f * tension
                - 0.02f * pulse;
            float oldJitter = m.OldLook && stage >= 5 ? Mathf.Sin(clockTime * 9f) : 0f;
            for (int i = 0; i < m.Bridges.Count; i++)
            {
                Bridge br = m.Bridges[i];
                if (br.R == null)
                {
                    continue;
                }
                Place(br.R, br.At + new Vector2(0f, Px(0.7f) * oldJitter), br.Width, m.StrapHeight * cinch);
                br.R.color = new Color(1f, 1f, 1f, cool);
                float heat = heatK > 0f && br.At.x >= heatLo && br.At.x <= heatHi ? heatK : 0f;
                if (!m.OldLook && Layers.ShowStrapUnderglow)
                {
                    heat += Style.Underglow[shownStage] * 0.6f * StrapFieldAt(br.At.x, br.At.y);
                }
                PaintBridgeHeat(br, heat * cool, m.StrapHeight);
            }

            // ---- chambers ----
            for (int i = 0; i < m.Primers.Count; i++)
            {
                Primer p = m.Primers[i];
                bool active = i < shownActive;
                float boost = p.Flick >= 0f
                    ? p.FlickBoost * Mathf.Sin(Mathf.Clamp01(p.Flick / 0.14f) * Mathf.PI)
                    : 0f;
                if (sweepAt >= 0f)
                {
                    // The sweep: P1 +10%, 30 ms, P2 ... - powder flowing inside, not a progress bar.
                    boost = Mathf.Max(boost, 0.1f * Mathf.Clamp01(1f - Mathf.Abs(sweepAt - i)));
                }
                if (lockK > 0f)
                {
                    boost = Mathf.Max(boost, 0.15f * lockK);
                }
                float igniting = m.LoadClock >= Style.IgnitionAt && i == m.LoadPrimer && igniteOn
                    ? ignite
                    : -1f;
                PaintPrimer(p, active || igniting >= 0f, igniting, boost, m.PrimerSize, m.Notch,
                    m.StrapHeight, cool, m.OldLook);
            }

            PaintMagazineDebug(m, stage);
        }

        /// <summary>How much of the pocket field reaches the strap's middle line at a world x -
        /// what the underglow and the bridge heat follow.</summary>
        private float StrapFieldAt(float x, float y)
        {
            float sum = 0f;
            for (int i = 0; i < pockets.Count; i++)
            {
                Vector4 p = pockets[i];
                float d = new Vector2(x - p.x, y - p.y).magnitude;
                float k = Mathf.Clamp01(1f - d / Mathf.Max(p.z, 1e-4f));
                sum += k * k * (3f - 2f * k) * p.w;
            }
            return Mathf.Clamp01(sum * 4f);
        }

        private static readonly int StrapWidthId = Shader.PropertyToID("_StrapWidth");
        private static readonly int StrapEndsId = Shader.PropertyToID("_StrapEnds");
        private static readonly int StrapTensionId = Shader.PropertyToID("_StrapTension");
        private static readonly int ChannelHeatId = Shader.PropertyToID("_ChannelHeat");
        private static readonly int HeatFrontId = Shader.PropertyToID("_HeatFront");
        private static readonly int PocketLightId = Shader.PropertyToID("_PocketLight");
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int EmbersId = Shader.PropertyToID("_Embers");
        private static readonly int UnderglowId = Shader.PropertyToID("_Underglow");
        private static readonly int SeamHeatId = Shader.PropertyToID("_SeamHeat");
        private static readonly int WarmthId = Shader.PropertyToID("_CasingWarmth");
        private static readonly int SootId = Shader.PropertyToID("_SootAmount");
        private static readonly int CreaseId = Shader.PropertyToID("_Crease");
        private static readonly int HazeId = Shader.PropertyToID("_LocalHeatDistortion");
        private static readonly int SquashId = Shader.PropertyToID("_Squash");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int OldLookId = Shader.PropertyToID("_OldLook");
        private static readonly int RawId = Shader.PropertyToID("_Raw");

        private static readonly int[] PocketIds =
        {
            Shader.PropertyToID("_PocketA"), Shader.PropertyToID("_PocketB"),
            Shader.PropertyToID("_PocketC"), Shader.PropertyToID("_PocketD"),
            Shader.PropertyToID("_PocketE")
        };

        /// <summary>One cube's casing, through its renderer's property block. The pockets
        /// collected for the pack are handed over in THIS cube's uv - the nearest five that can
        /// reach it - so the fill crosses cube boundaries and stops where the block does.</summary>
        private void WriteCasing(SpriteRenderer r, Vector2 cubeCentre, int stage, float cool, bool left,
            bool right, float seamBoost, Vector4 front, Vector2 squash, float extraTension, float flash,
            float seed, bool old)
        {
            if (ViewUtil.DynamiteMaterial == null)
            {
                return;
            }
            float cube = Mathf.Max(Cube, 1e-4f);
            int n = 0;
            for (int i = 0; i < pockets.Count && n < cubePockets.Length; i++)
            {
                Vector4 p = pockets[i];
                Vector2 d = new Vector2(p.x - cubeCentre.x, p.y - cubeCentre.y);
                if (Mathf.Abs(d.x) > p.z + cube * 0.6f || Mathf.Abs(d.y) > p.z + cube * 0.6f)
                {
                    continue;
                }
                cubePockets[n++] = new Vector4(0.5f + d.x / cube, 0.5f + d.y / cube, p.z / cube,
                    p.w * cool);
            }
            for (int i = n; i < cubePockets.Length; i++)
            {
                cubePockets[i] = Vector4.zero;
            }
            r.GetPropertyBlock(block);
            block.SetFloat(StrapWidthId, StrapWidth);
            block.SetVector(StrapEndsId, new Vector4(left ? 1f : 0f, right ? 1f : 0f, 0f, 0f));
            float tension = Layers.ShowStrapTension ? Style.StrapTension[stage] : 0f;
            block.SetFloat(StrapTensionId, Mathf.Clamp01(tension + extraTension) * cool);
            block.SetVector(HeatFrontId, front);
            for (int i = 0; i < PocketIds.Length; i++)
            {
                block.SetVector(PocketIds[i], cubePockets[i]);
            }
            block.SetFloat(PocketLightId, Layers.ShowPockets ? 1f : 0f);
            block.SetFloat(OldLookId, old ? 1f : 0f);
            if (old)
            {
                // THE PREVIOUS PASS, for the lab's before/after: crisp hairlines and a whole-cube
                // wobble, no pockets, no underglow.
                block.SetFloat(ChannelHeatId, Style.ChannelHeat[stage] * cool);
                block.SetFloat(FillId, 0f);
                block.SetFloat(EmbersId, 0f);
                block.SetFloat(UnderglowId, 0f);
                block.SetFloat(SeamHeatId, Style.OldSeamShare[stage] / 5f * cool);
                block.SetFloat(CreaseId, 0f);
                float oldHaze = Style.OldHazePixels[stage];
                block.SetFloat(HazeId, oldHaze > 0f ? Px(oldHaze) / cube : 0f);
            }
            else
            {
                block.SetFloat(ChannelHeatId, Layers.ShowStrapUnderglow ? Style.ChannelHeat[stage] * cool : 0f);
                block.SetFloat(FillId, Layers.ShowPockets ? Style.Fill[stage] * cool : 0f);
                block.SetFloat(EmbersId, Layers.ShowPockets ? Style.Embers[stage] : 0f);
                block.SetFloat(UnderglowId, Layers.ShowStrapUnderglow ? Style.Underglow[stage] * cool : 0f);
                block.SetFloat(SeamHeatId, Layers.ShowSeamHeat ? (Style.SeamHeat[stage] + seamBoost) * cool : 0f);
                block.SetFloat(CreaseId, Layers.ShowSootMask ? Style.Crease[stage] * cool : 0f);
                float hazePx = Layers.ShowHeatDistortion ? Style.HeatDistortionPixels[stage] : 0f;
                block.SetFloat(HazeId, hazePx > 0f ? Px(hazePx) / cube * cool : 0f);
            }
            block.SetFloat(WarmthId, Style.Warmth[stage] * cool);
            block.SetFloat(SootId, Layers.ShowSootMask ? Style.Soot[stage] : 0f);
            block.SetVector(SquashId, new Vector4(squash.x, squash.y, 0f, 0f));
            block.SetFloat(SeedId, seed * 10f);
            block.SetFloat(FlashId, flash);
            block.SetFloat(RawId, 0f);
            r.SetPropertyBlock(block);
            casingThisFrame.Add(r);
        }

        private void PaintBridgeHeat(Bridge br, float heat, float strapH)
        {
            if (heat <= 0.005f)
            {
                if (br.Heat != null)
                {
                    br.Heat.color = Invisible;
                }
                return;
            }
            if (br.Heat == null)
            {
                br.Heat = Rent(HeatBarSprite, BridgeOrder);
            }
            Place(br.Heat, br.At + new Vector2(0f, -strapH * 0.36f), br.Width, strapH * 0.28f);
            Color c = Style.PrimerAmber;
            c.a = Mathf.Clamp01(heat * 1.6f);
            br.Heat.color = c;
        }

        /// <summary>One chamber: a recessed brass socket, its amber core, a halo of about ten
        /// pixels and a short vein of heat running down into the strap.</summary>
        private void PaintPrimer(Primer p, bool active, float igniting, float boost, float size,
            bool notch, float strapH, float alpha, bool old)
        {
            bool shown = Layers.ShowPrimerSystem;
            float w = notch ? size * 0.45f : size * 1.3f;
            float h = notch ? strapH * 0.72f : size * 1.3f;
            if (p.Socket != null)
            {
                Place(p.Socket, p.At, w, h);
                Color s = shown ? Color.white : Invisible;
                s.a *= alpha;
                p.Socket.color = s;
            }
            float glow = 0f;
            Color core = Style.PrimerAmber;
            float punch = 1f;
            if (igniting >= 0f)
            {
                float k = igniting;
                core = k < 0.35f ? Color.Lerp(Style.SocketDark, Style.PrimerEmber, k / 0.35f)
                    : k < 0.7f ? Color.Lerp(Style.PrimerEmber, Style.PrimerHot, (k - 0.35f) / 0.35f)
                    : Color.Lerp(Style.PrimerHot, Style.PrimerAmber, (k - 0.7f) / 0.3f);
                glow = Mathf.Clamp01(k * 1.4f);
                punch = 1f + Style.IgnitionPunch * Mathf.Sin(k * Mathf.PI);
            }
            else if (active)
            {
                glow = Style.PrimerActiveBrightness;
            }
            glow *= 1f + boost;
            bool lit = shown && (active || igniting >= 0f);
            if (p.Core != null)
            {
                Place(p.Core, p.At, (notch ? size * 0.3f : size * 0.8f) * punch,
                    (notch ? strapH * 0.55f : size * 0.8f) * punch);
                p.Core.color = lit
                    ? new Color(Mathf.Clamp01(core.r * glow), Mathf.Clamp01(core.g * glow),
                        Mathf.Clamp01(core.b * glow), alpha)
                    : Invisible;
            }
            if (p.Halo != null)
            {
                float halo = old ? Px(5f) * 2f + size : Px(Style.PrimerHaloPixels) * 2f;
                Place(p.Halo, p.At, halo, halo);
                Color c = Style.PrimerAmber;
                c.a = lit ? (old ? 0.28f : Style.PrimerHaloAlpha) * Mathf.Clamp01(glow) * alpha : 0f;
                p.Halo.color = c;
            }
            if (p.Vein != null)
            {
                // Heat running DOWN out of the chamber into the strap - transfer, not a circuit.
                float len = strapH * 0.42f + Px(2f);
                Place(p.Vein, p.At + new Vector2(Px(0.5f) * p.Side, -len * 0.5f - size * 0.25f),
                    Px(1.4f), len);
                Color c = Style.PrimerAmber;
                c.a = lit ? Style.PrimerVeinAlpha * Mathf.Clamp01(glow) * alpha : 0f;
                p.Vein.color = c;
            }
        }

        // =================================================================== the cook-off

        private void BuildDetonation(Detonation d)
        {
            d.StrapHeight = Cube * BandHeight * StrapWidth;
            d.Bounds = BoundsOf(d.Cells);
            d.Centre = d.Bounds.center;
            Sprite tile = DynamiteTile;
            for (int i = 0; i < d.Cells.Count; i++)
            {
                SpriteRenderer r = Rent(tile, ProxyOrder, ViewUtil.TileMaterial(tile));
                ViewUtil.ApplyTile(r, tile, Cube);
                Vector2 at = board.CellToWorld(d.Cells[i]);
                r.transform.localPosition = new Vector3(at.x, at.y, 0f);
                r.color = Color.white;
                d.Proxies.Add(r);
            }
            BuildBridges(d.Cells, d.Bridges);
            int row, x0, x1;
            ChooseRail(d.Cells, out row, out x0, out x1);
            float spacing, size;
            bool notch;
            PlacePrimers(d.Primers, row, x0, x1, d.Cap, d.StrapHeight, d.CardId, false, out spacing,
                out size, out notch);
            d.PrimerSize = size;
            d.Notch = notch;
            d.Waiting = 0f;
        }

        private void StartDetonation(Detonation d, float trigger)
        {
            d.Clock = 0f;
            d.Trigger = trigger;
            int n = Mathf.Clamp(d.Charges, 1, d.Primers.Count > 0 ? d.Primers.Count : 1);
            d.BlastAt = trigger + n * Style.DetonationPrimerDelay + Style.DetonationCompressionTime;
        }

        private void TickDetonations(float dt)
        {
            for (int i = detonations.Count - 1; i >= 0; i--)
            {
                Detonation d = detonations[i];
                if (d.Clock < 0f)
                {
                    // Prepared but not begun: if no explosion ever comes for it, go on our own.
                    d.Waiting += dt;
                    if (d.Waiting > 1.2f)
                    {
                        StartDetonation(d, 0f);
                    }
                    continue;
                }
                d.Clock += dt * d.Rate;
                if (!d.Prepped && d.Clock >= d.Trigger)
                {
                    d.Prepped = true;
                    Emit(Cue.DetonationPrep, d.Cap > 0 ? d.Charges / (float)d.Cap : 1f);
                }
                int n = Mathf.Min(d.Charges, d.Primers.Count);
                while (d.Fired < n && d.Clock >= d.Trigger + d.Fired * Style.DetonationPrimerDelay)
                {
                    FireChamber(d, d.Fired);
                    d.Fired++;
                }
                if (!d.Blasted && d.Clock >= d.BlastAt)
                {
                    d.Blasted = true;
                    Blast(d);
                }
                if (d.Blasted && d.Clock > d.BlastAt + 0.05f)
                {
                    DropDetonation(d);
                    detonations.RemoveAt(i);
                }
            }
        }

        private void FireChamber(Detonation d, int index)
        {
            Primer p = d.Primers[index];
            // Its heat races along the strap into the pack's centre - the stored charge gathering
            // INWARD before anything goes out. The debug switch only makes it louder.
            var b = NewBit(KindCurve, HeatBarSprite, BlastOrder - 1, d.Rate);
            b.From = p.At;
            b.To = d.Centre;
            b.Ctrl = (p.At + d.Centre) * 0.5f;
            b.Life = Mathf.Max(0.04f, d.BlastAt - d.Clock);
            b.Size = d.StrapHeight * 1.4f;
            b.EndSize = d.StrapHeight * 0.6f;
            b.Aspect = 0.35f;
            b.Colour = Style.PrimerAmber;
            b.EndColour = Style.PrimerHot;
            b.Alpha = Layers.ShowDetonationEnergyFlow ? 0.95f : 0.5f;
            float a = Mathf.PI * (0.25f + 0.5f * Hash(d.CardId, 900 + index));
            var s = NewBit(KindBallistic, DotSprite, BlastOrder, d.Rate);
            s.From = s.To = p.At;
            s.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Px(60f);
            s.Drag = 5f;
            s.Gravity = Px(40f);
            s.Life = 0.18f;
            s.Size = Px(1.8f);
            s.EndSize = Px(0.6f);
            s.Colour = Style.PrimerHot;
            s.EndColour = Style.PrimerEmber;
            s.Alpha = 1f;
        }

        private void PaintDetonation(Detonation d)
        {
            float t = d.Clock;
            float heat = 0f;
            float swell = 0f;
            float tension = 0.5f;
            float inward = 0f;
            if (t >= 0f && t >= d.Trigger)
            {
                float k = Mathf.Clamp01((t - d.Trigger) / Mathf.Max(d.BlastAt - d.Trigger, 0.01f));
                heat = 0.3f * k;
                tension = Mathf.Lerp(0.5f, 1f, k);
                inward = EaseOut(k);
                float compressStart = d.BlastAt - Style.DetonationCompressionTime;
                if (t >= compressStart)
                {
                    float c = Mathf.Clamp01((t - compressStart) / Style.DetonationCompressionTime);
                    swell = c < 0.6f
                        ? -Style.DetonationCompression * EaseOut(c / 0.6f)
                        : Mathf.Lerp(-Style.DetonationCompression, Style.DetonationOvershoot,
                            (c - 0.6f) / 0.4f);
                }
            }
            int n = Mathf.Min(d.Charges, d.Primers.Count);
            CollectPockets(d.Primers, n, Mathf.Max(d.Stage, 1), -1, 1f, 0f, d.Centre, inward,
                d.StrapHeight);
            var squash = new Vector2(1f + swell, 1f + swell);
            for (int i = 0; i < d.Proxies.Count; i++)
            {
                SpriteRenderer r = d.Proxies[i];
                if (r == null)
                {
                    continue;
                }
                GridPos cell = d.Cells[i];
                WriteCasing(r, board.CellToWorld(cell), d.Stage, 1f,
                    d.CellSet.Contains(new GridPos(cell.X - 1, cell.Y)),
                    d.CellSet.Contains(new GridPos(cell.X + 1, cell.Y)), 0f, Vector4.zero, squash,
                    tension, heat, Hash(cell, 3), false);
            }
            for (int i = 0; i < d.Bridges.Count; i++)
            {
                Bridge br = d.Bridges[i];
                Place(br.R, br.At, br.Width, d.StrapHeight * (1f - 0.08f * (tension - 0.5f) * 2f));
                br.R.color = Color.white;
                PaintBridgeHeat(br, heat * 0.6f + Style.Underglow[d.Stage] * 0.5f, d.StrapHeight);
            }
            for (int i = 0; i < d.Primers.Count; i++)
            {
                bool active = i < n;
                float boost = i < d.Fired ? 0.6f : 0f;
                PaintPrimer(d.Primers[i], active, -1f, boost, d.PrimerSize, d.Notch, d.StrapHeight,
                    1f, false);
            }
        }

        /// <summary>
        /// THE BLAST - the powder burst with layers that grow with what was stored, and never more
        /// than 1.3x the footprint: a core flash in amber, powder, red casing fragments, a few torn
        /// strap pieces, an ember ring at stage 3 and a broken secondary flare at 4-5.
        /// </summary>
        private void Blast(Detonation d)
        {
            for (int i = 0; i < d.Proxies.Count; i++)
            {
                if (d.Proxies[i] != null)
                {
                    d.Proxies[i].color = Invisible;
                }
            }
            if (d.Full)
            {
                for (int i = 0; i < d.Primers.Count; i++)
                {
                    fullPrimerSpots.Add(d.Primers[i].At);
                }
            }
            blastCentres.Add(d.Centre);
            ClearLayout(d.Primers, d.Bridges);
            float stage = Mathf.Max(1, d.Stage);
            float scale = 1f + Style.ExplosionSupportIntensity * (stage - 1f);
            float extent = (Mathf.Max(d.Bounds.width, d.Bounds.height) * 0.5f + Cube * 0.55f) * scale;
            float rate = d.Rate;

            var core = NewBit(KindStill, DotSprite, BlastOrder + 1, rate);
            core.From = core.To = d.Centre;
            core.Life = 0.3f;
            core.Size = extent * 1.1f;
            core.EndSize = extent * 1.6f;
            core.Colour = Style.BlastCore;
            core.EndColour = Style.PrimerAmber;
            core.Alpha = Mathf.Lerp(0.55f, 0.85f, (stage - 1f) / 4f);

            int powder = 6 + 3 * (int)stage;
            for (int i = 0; i < powder; i++)
            {
                float a = (i + Hash(d.CardId, 1000 + i) * 0.8f) / powder * Mathf.PI * 2f;
                var b = NewBit(KindBallistic, DotSprite, BlastOrder, rate);
                b.From = b.To = d.Centre;
                b.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * extent
                    * Mathf.Lerp(3.5f, 6f, Hash(d.CardId, 1030 + i));
                b.Drag = 5f;
                b.Gravity = extent * 2f;
                b.Life = Mathf.Lerp(0.3f, 0.5f, Hash(d.CardId, 1060 + i));
                b.Size = Px(Mathf.Lerp(2f, 3.5f, Hash(d.CardId, 1090 + i)));
                b.EndSize = b.Size * 0.3f;
                b.Aspect = 2.2f;
                b.Colour = Style.PrimerHot;
                b.EndColour = Style.PrimerEmber;
                b.Alpha = 1f;
            }
            int shards = 4 + (int)stage;
            for (int i = 0; i < shards; i++)
            {
                float a = Hash(d.CardId, 1200 + i) * Mathf.PI * 2f;
                var b = NewBit(KindBallistic, ViewUtil.WhiteSprite, BlastOrder, rate);
                b.From = b.To = d.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Cube * 0.2f;
                b.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a) + 0.5f) * extent
                    * Mathf.Lerp(2.2f, 3.6f, Hash(d.CardId, 1230 + i));
                b.Drag = 2.4f;
                b.Gravity = extent * 6f;
                b.Life = Mathf.Lerp(0.45f, 0.7f, Hash(d.CardId, 1260 + i));
                b.Size = Cube * Mathf.Lerp(0.09f, 0.15f, Hash(d.CardId, 1290 + i));
                b.EndSize = b.Size * 0.6f;
                b.Aspect = Mathf.Lerp(0.5f, 1.4f, Hash(d.CardId, 1320 + i));
                b.Spin = (Hash(d.CardId, 1350 + i) - 0.5f) * 900f;
                b.Colour = Style.CasingRed;
                b.EndColour = Color.Lerp(Style.CasingRed, Style.GrainDark, 0.6f);
                b.Alpha = 1f;
            }
            int straps = 2 + (int)(Hash(d.CardId, 1400) * 3f);
            for (int i = 0; i < straps; i++)
            {
                float a = (i / (float)straps + Hash(d.CardId, 1410 + i) * 0.3f) * Mathf.PI * 2f;
                var b = NewBit(KindBallistic, ViewUtil.WhiteSprite, BlastOrder, rate);
                b.From = b.To = d.Centre + new Vector2(Mathf.Cos(a) * Cube * 0.3f, 0f);
                b.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.4f + 0.3f) * extent * 2.4f;
                b.Drag = 3f;
                b.Gravity = extent * 5f;
                b.Life = 0.5f;
                b.Size = d.StrapHeight * 0.8f;
                b.EndSize = b.Size * 0.8f;
                b.Aspect = 3.2f;
                b.Spin = (Hash(d.CardId, 1440 + i) - 0.5f) * 700f;
                b.Colour = Style.Graphite;
                b.EndColour = Style.Graphite;
                b.Alpha = 1f;
            }
            if (stage >= 3)
            {
                Ring(d.Centre, extent * 0.6f, extent * 1.5f, 0f, 0.32f, 0.55f, RingSprite, rate);
            }
            if (stage >= 4)
            {
                Ring(d.Centre, extent * 0.9f, extent * 1.9f, Style.SecondaryRingDelay, 0.3f,
                    stage >= 5 ? 0.7f : 0.5f, BrokenRingSprite, rate);
            }
            if (d.Full)
            {
                knockAt = clockTime;
            }
            Emit(Cue.Detonation, d.Cap > 0 ? d.Charges / (float)d.Cap : 1f);
            ScheduleTotal(d);
        }

        private void Ring(Vector2 at, float from, float to, float delay, float life, float alpha,
            Sprite sprite, float rate)
        {
            var r = NewBit(KindRing, sprite, BlastOrder, rate);
            r.From = r.To = at;
            r.Delay = delay;
            r.Life = life;
            r.Size = from;
            r.EndSize = to;
            r.Colour = Style.PrimerAmber;
            r.EndColour = Style.PrimerEmber;
            r.Alpha = alpha;
            r.Angle = Hash(Mathf.RoundToInt(at.x * 97f) * 31 + Mathf.RoundToInt(at.y * 89f), 5) * 360f;
        }

        private void DropDetonation(Detonation d)
        {
            for (int i = 0; i < d.Proxies.Count; i++)
            {
                Return(d.Proxies[i]);
            }
            d.Proxies.Clear();
            ClearLayout(d.Primers, d.Bridges);
        }

        // =================================================================== the payout

        /// <summary>ONE total, however many blocks went up: it waits for the last blast of the
        /// payout and sits over the middle of all of them.</summary>
        private void ScheduleTotal(Detonation d)
        {
            for (int i = 0; i < detonations.Count; i++)
            {
                if (!detonations[i].Blasted)
                {
                    return;
                }
            }
            if (pendingPayout == null || pendingPoints <= 0)
            {
                fullPrimerSpots.Clear();
                blastCentres.Clear();
                pendingPayout = null;
                return;
            }
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < blastCentres.Count; i++)
            {
                sum += blastCentres[i];
            }
            Vector2 where = (blastCentres.Count > 0 ? sum / blastCentres.Count : d.Centre)
                + new Vector2(0f, Cube * 0.75f);
            SpawnTotal(where, "+" + pendingPoints, Style.TextDelay, d.Rate, pendingStage >= 5);
            if (pendingFull && fullPrimerSpots.Count > 0)
            {
                SpawnReward(where, d.Rate);
            }
            else
            {
                StartScorePunch(0.35f, d.Rate);
            }
            fullPrimerSpots.Clear();
            blastCentres.Clear();
            pendingPayout = null;
        }

        private void SpawnTotal(Vector2 at, string text, float delay, float rate, bool flecks)
        {
            var total = new FloatingTotal { Delay = delay, Rate = rate };
            var root = new GameObject("PowderTotal").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(at.x, at.y, 0f);
            total.Root = root;
            float size = 0.034f;
            TextMesh shadow = ViewUtil.MakeText3D(root, "Shadow", new Vector2(Px(1.5f), -Px(1.5f)),
                text, 96, size, Style.TextShadow, TextOrder, TextAnchor.MiddleCenter);
            TextMesh edge = ViewUtil.MakeText3D(root, "Edge", Vector2.zero, text, 96, size * 1.05f,
                Style.TextAmber, TextOrder + 1, TextAnchor.MiddleCenter);
            TextMesh face = ViewUtil.MakeText3D(root, "Face", Vector2.zero, text, 96, size,
                Style.TextIvory, TextOrder + 2, TextAnchor.MiddleCenter);
            total.Layers.Add(shadow);
            total.Layers.Add(edge);
            total.Layers.Add(face);
            root.localScale = Vector3.zero;
            texts.Add(total);
            if (flecks)
            {
                // A full magazine's payoff: a few tiny powder flecks round the number. No coins.
                for (int i = 0; i < 4; i++)
                {
                    float a = (i + 0.3f) / 4f * Mathf.PI * 2f;
                    var b = NewBit(KindBallistic, DotSprite, TextOrder - 1, rate);
                    b.From = b.To = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f) * Cube * 0.45f;
                    b.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a) + 0.5f) * Px(26f);
                    b.Drag = 3f;
                    b.Gravity = Px(18f);
                    b.Delay = delay + 0.05f;
                    b.Life = 0.45f;
                    b.Size = Px(2f);
                    b.EndSize = Px(0.8f);
                    b.Colour = Style.PrimerHot;
                    b.EndColour = Style.PrimerEmber;
                    b.Alpha = 0.9f;
                }
            }
        }

        private void TickTexts(float dt)
        {
            for (int i = texts.Count - 1; i >= 0; i--)
            {
                FloatingTotal t = texts[i];
                t.Age += dt * t.Rate;
                float a = t.Age - t.Delay;
                if (t.Root == null)
                {
                    texts.RemoveAt(i);
                    continue;
                }
                if (a < 0f)
                {
                    t.Root.localScale = Vector3.zero;
                    continue;
                }
                // 0.65 -> 1.14 -> 0.98 -> 1
                float s;
                if (a < 0.1f)
                {
                    s = Mathf.Lerp(0.65f, 1.14f, EaseOut(a / 0.1f));
                }
                else if (a < 0.2f)
                {
                    s = Mathf.Lerp(1.14f, 0.98f, (a - 0.1f) / 0.1f);
                }
                else if (a < 0.3f)
                {
                    s = Mathf.Lerp(0.98f, 1f, (a - 0.2f) / 0.1f);
                }
                else
                {
                    s = 1f;
                }
                t.Root.localScale = new Vector3(s, s, 1f);
                float fadeStart = 0.3f + Style.TextHold;
                float fade = a > fadeStart ? Mathf.Clamp01((a - fadeStart) / 0.3f) : 0f;
                if (fade > 0f)
                {
                    t.Root.localPosition += new Vector3(0f, Px(20f) * dt * t.Rate, 0f);
                }
                for (int k = 0; k < t.Layers.Count; k++)
                {
                    if (t.Layers[k] != null)
                    {
                        Color c = t.Layers[k].color;
                        c.a = 1f - fade;
                        ViewUtil.SetTextColor(t.Layers[k], c);
                    }
                }
                if (fade >= 1f)
                {
                    Destroy(t.Root.gameObject);
                    texts.RemoveAt(i);
                }
            }
        }

        /// <summary>A full magazine: its chambers' light gathers into one small powder seed, which
        /// then flies to the score on a short curve. Never a coin, never confetti.</summary>
        private void SpawnReward(Vector2 gather, float rate)
        {
            DropReward();
            rewardCore = new RewardSeed { Gather = gather, Rate = rate };
            for (int i = 0; i < fullPrimerSpots.Count && i < 5; i++)
            {
                var m = NewBit(KindCurve, DotSprite, TextOrder - 1, rate);
                m.From = fullPrimerSpots[i];
                m.To = gather;
                m.Ctrl = (m.From + gather) * 0.5f + new Vector2(0f, Cube * 0.3f);
                m.Delay = 0.05f + i * 0.02f;
                m.Life = 0.18f;
                m.Size = Px(3f);
                m.EndSize = Px(2f);
                m.Colour = Style.PrimerAmber;
                m.EndColour = Style.PrimerHot;
                m.Alpha = 0.95f;
            }
            rewardCore.R = Rent(CoreSprite, TextOrder - 1);
            rewardCore.R.color = Invisible;
        }

        private void TickReward(float dt)
        {
            if (rewardCore == null)
            {
                return;
            }
            RewardSeed r = rewardCore;
            r.Age += dt * r.Rate;
            float gathered = 0.05f + 0.02f * 4f + 0.18f;
            float flyAt = gathered + Style.RewardHold;
            Vector2 target = r.Gather;
            if (ScoreAnchor != null)
            {
                Vector3 local = transform.InverseTransformPoint(ScoreAnchor());
                target = new Vector2(local.x, local.y);
            }
            if (r.Age < gathered)
            {
                float k = Mathf.Clamp01((r.Age - 0.1f) / (gathered - 0.1f));
                Place(r.R, r.Gather, Px(4f + 3f * k), Px(4f + 3f * k));
                Color c = Style.PrimerAmber;
                c.a = k;
                r.R.color = c;
                return;
            }
            if (r.Age < flyAt)
            {
                Place(r.R, r.Gather, Px(7f), Px(7f));
                r.R.color = Style.PrimerHot;
                return;
            }
            float f = Mathf.Clamp01((r.Age - flyAt) / Style.RewardFlight);
            Vector2 ctrl = (r.Gather + target) * 0.5f + new Vector2(0f, Cube * 0.6f);
            Vector2 p = Bezier(r.Gather, ctrl, target, f * f * (3f - 2f * f));
            Place(r.R, p, Px(7f) * (1f - 0.4f * f), Px(7f) * (1f - 0.4f * f));
            r.R.color = Color.Lerp(Style.PrimerHot, Style.PrimerAmber, f);
            if (f >= 1f)
            {
                Emit(Cue.Reward, 1f);
                StartScorePunch(0f, r.Rate);
                DropReward();
            }
        }

        private void DropReward()
        {
            if (rewardCore != null)
            {
                Return(rewardCore.R);
                rewardCore = null;
            }
        }

        private void StartScorePunch(float delay, float rate)
        {
            scoreClock = 0f;
            scoreDelay = delay;
            scoreRate = rate;
        }

        /// <summary>TOPLAM: 1.00 -> 1.085 -> 0.985 -> 1, and warm while it happens.</summary>
        private void TickScore(float dt)
        {
            if (scoreClock < 0f)
            {
                ScoreClaim = 0f;
                ScoreScale = 1f;
                return;
            }
            scoreClock += dt * scoreRate;
            float a = scoreClock - scoreDelay;
            if (a < 0f)
            {
                return;
            }
            const float length = 0.32f;
            float k = Mathf.Clamp01(a / length);
            float s = k < 0.35f ? Mathf.Lerp(1f, 1.085f, EaseOut(k / 0.35f))
                : k < 0.7f ? Mathf.Lerp(1.085f, 0.985f, (k - 0.35f) / 0.35f)
                : Mathf.Lerp(0.985f, 1f, (k - 0.7f) / 0.3f);
            ScoreScale = s;
            ScoreClaim = 1f - k * k;
            if (k >= 1f)
            {
                scoreClock = -1f;
                ScoreClaim = 0f;
                ScoreScale = 1f;
            }
        }

        private void TickKnock()
        {
            if (knockAt < 0f || board == null)
            {
                return;
            }
            float age = clockTime - knockAt;
            const float dur = 0.2f;
            if (age >= dur)
            {
                board.SetImpulse(Vector2.zero);
                knockAt = -1f;
                return;
            }
            float u = age / dur;
            board.SetImpulse(Vector2.down * 0.045f * Mathf.Sin(u * Mathf.PI * 2.5f) * (1f - u) * (1f - u));
        }

        // =================================================================== small events

        private void Fleck(Vector2 at, float rate)
        {
            var b = NewBit(KindBallistic, DotSprite, BitOrder, rate);
            b.From = b.To = at;
            b.Velocity = new Vector2(Px(8f), Px(55f));
            b.Drag = 6f;
            b.Gravity = Px(30f);
            b.Life = 0.16f;
            b.Size = Px(1.6f);
            b.EndSize = Px(0.6f);
            b.Colour = Style.PrimerHot;
            b.EndColour = Style.PrimerEmber;
            b.Alpha = 1f;
        }

        /// <summary>One thin thread of warm-charcoal smoke out of a strap/casing seam.</summary>
        private void Smoke(Magazine m, float rate)
        {
            if (m.Cells.Count == 0)
            {
                return;
            }
            GridPos cell = m.Cells[(int)(Hash(m.CardId, 1500 + m.EventCount) * m.Cells.Count) % m.Cells.Count];
            Vector2 at = board.CellToWorld(cell);
            float seam = Hash(m.CardId, 1520 + m.EventCount) < 0.5f ? SeamA : SeamB;
            at.x += (seam - 0.5f) * Cube;
            at.y += (BandMid - 0.5f) * Cube + m.StrapHeight * 0.7f;
            var b = NewBit(KindRise, SmokeSprite, BitOrder, rate);
            b.From = b.To = at;
            b.Velocity = new Vector2(Px(3f), Px(22f));
            b.Life = 0.17f;
            b.Size = Px(3f);
            b.EndSize = Px(7f);
            b.Aspect = 0.55f;
            b.Colour = Style.Smoke;
            b.EndColour = Style.Smoke;
            b.Alpha = 0.35f;
        }

        /// <summary>One to three tiny embers drifting slowly through the powder - INSIDE a pocket,
        /// on a short curved path.</summary>
        private void EmberDrift(Magazine m)
        {
            int active = Mathf.Min(m.Charges, m.Primers.Count);
            if (active <= 0)
            {
                return;
            }
            int stage = StageIndex(m.Charges, m.Cap);
            int count = stage >= 5 ? 3 : stage >= 4 ? 2 : 1;
            for (int e = 0; e < count; e++)
            {
                Primer p = m.Primers[(int)(Hash(m.CardId, 1600 + m.EventCount * 3 + e) * active) % active];
                Vector2 pocket = p.At + new Vector2(0f, p.Side * (m.StrapHeight * 0.5f + Cube * 0.17f));
                Vector2 from = pocket + new Vector2((Hash(m.CardId, 1620 + e) - 0.5f) * Cube * 0.2f,
                    (Hash(m.CardId, 1640 + e) - 0.5f) * Cube * 0.12f);
                float len = Px(Mathf.Lerp(5f, 12f, Hash(m.CardId, 1660 + m.EventCount + e)));
                float a = Hash(m.CardId, 1680 + m.EventCount + e) * Mathf.PI * 2f;
                Vector2 to = from + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * len;
                var b = NewBit(KindDrift, DotSprite, BitOrder, m.Rate);
                b.From = from;
                b.To = to;
                b.Ctrl = (from + to) * 0.5f + new Vector2(-(to - from).y, (to - from).x) * 0.4f;
                b.Delay = e * 0.25f;
                b.Life = Mathf.Lerp(0.9f, 1.3f, Hash(m.CardId, 1700 + e));
                b.Size = Px(1.6f);
                b.EndSize = Px(1.1f);
                b.Colour = Style.PrimerAmber;
                b.EndColour = Style.PrimerEmber;
                b.Alpha = 0.75f;
            }
        }

        // =================================================================== bits

        private Bit NewBit(int kind, Sprite sprite, int order, float rate)
        {
            var b = new Bit { Kind = kind, R = Rent(sprite, order), Rate = rate };
            b.R.color = Invisible;
            bits.Add(b);
            return b;
        }

        private void TickBits(float dt)
        {
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                Bit b = bits[i];
                float bdt = dt * b.Rate;
                b.Age += bdt;
                float t = (b.Age - b.Delay) / Mathf.Max(b.Life, 0.0001f);
                if (t < 0f)
                {
                    b.R.color = Invisible;
                    continue;
                }
                if (t >= 1f)
                {
                    Return(b.R);
                    bits.RemoveAt(i);
                    continue;
                }
                Vector2 pos;
                float angle = b.Angle;
                switch (b.Kind)
                {
                    case KindCurve:
                        // Slow, then faster toward where it is going.
                        float e = t * t;
                        pos = Bezier(b.From, b.Ctrl, b.To, e);
                        Vector2 tan = Bezier(b.From, b.Ctrl, b.To, Mathf.Min(1f, e + 0.02f)) - pos;
                        if (tan.sqrMagnitude > 1e-10f)
                        {
                            angle = Mathf.Atan2(tan.y, tan.x) * Mathf.Rad2Deg;
                        }
                        break;
                    case KindDrift:
                        pos = Bezier(b.From, b.Ctrl, b.To, t * t * (3f - 2f * t));
                        break;
                    case KindBallistic:
                        b.Velocity *= Mathf.Exp(-b.Drag * bdt);
                        b.Velocity += Vector2.down * b.Gravity * bdt;
                        b.To += b.Velocity * bdt;
                        pos = b.To;
                        angle = b.Spin != 0f ? b.Angle + b.Spin * b.Age
                            : Mathf.Atan2(b.Velocity.y, b.Velocity.x) * Mathf.Rad2Deg;
                        break;
                    case KindRise:
                        b.To += b.Velocity * bdt;
                        pos = b.To;
                        break;
                    default:
                        pos = b.From;
                        break;
                }
                float size = Mathf.Lerp(b.Size, b.EndSize, b.Kind == KindRing ? EaseOut(t) : t);
                Color c = Color.Lerp(b.Colour, b.EndColour, t);
                float fade;
                if (b.Kind == KindCurve)
                {
                    fade = Mathf.Clamp01(t * 5f) * (1f - t * t * 0.3f);
                }
                else if (b.Kind == KindDrift)
                {
                    fade = Mathf.Sin(t * Mathf.PI);
                }
                else
                {
                    fade = (1f - t) * (1f - t);
                }
                c.a = b.Alpha * fade;
                b.R.color = c;
                b.R.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
                b.R.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                SetScale(b.R, size * b.Aspect, size);
            }
        }

        // =================================================================== overlays

        private void PaintMagazineDebug(Magazine m, int stage)
        {
            int want = Layers.ShowGunpowderGroupBounds ? 4 : 0;
            while (m.DebugBars.Count < want)
            {
                m.DebugBars.Add(Rent(ViewUtil.WhiteSprite, DebugOrder));
            }
            float line = Px(1f);
            Rect b = m.Bounds;
            for (int i = 0; i < m.DebugBars.Count; i++)
            {
                SpriteRenderer r = m.DebugBars[i];
                if (i >= want)
                {
                    r.color = Invisible;
                    continue;
                }
                switch (i)
                {
                    case 0: Place(r, new Vector2(b.center.x, b.yMin), b.width, line); break;
                    case 1: Place(r, new Vector2(b.center.x, b.yMax), b.width, line); break;
                    case 2: Place(r, new Vector2(b.xMin, b.center.y), line, b.height); break;
                    default: Place(r, new Vector2(b.xMax, b.center.y), line, b.height); break;
                }
                r.color = new Color(0.4f, 1f, 0.9f, 0.85f);
            }
            // DEVELOPMENT ONLY: the charge, what the pack is worth and whose it is.
            bool label = (Layers.ShowChargeStage || Layers.ShowRewardValue || Layers.ShowPrimerStates)
                && (UnityEngine.Debug.isDebugBuild || Application.isEditor);
            if (!label)
            {
                if (m.Label != null)
                {
                    Destroy(m.Label.gameObject);
                    m.Label = null;
                }
                return;
            }
            var sb = new System.Text.StringBuilder();
            if (Layers.ShowChargeStage)
            {
                sb.Append("Charge ").Append(m.Charges).Append(" / ").Append(m.Cap)
                    .Append("  (stage ").Append(stage).Append(')');
            }
            if (Layers.ShowRewardValue)
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                sb.Append("Accumulated ").Append(m.CubeValue * m.Cells.Count)
                    .Append("  Block #").Append(m.CardId);
            }
            if (Layers.ShowPrimerStates)
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                sb.Append("Primers ");
                for (int i = 0; i < m.Primers.Count; i++)
                {
                    sb.Append(i < m.Charges ? "1 " : "0 ");
                }
            }
            Vector2 at = new Vector2(b.center.x, b.yMax + Cube * 0.35f);
            if (m.Label == null)
            {
                m.Label = ViewUtil.MakeText3D(transform, "PowderDebug", at, sb.ToString(), 70,
                    0.018f, new Color(0.6f, 1f, 0.9f), DebugOrder, TextAnchor.LowerCenter);
            }
            m.Label.text = sb.ToString();
            m.Label.transform.localPosition = new Vector3(at.x, at.y, 0f);
        }

        // =================================================================== plumbing

        private static readonly Color Invisible = new Color(1f, 1f, 1f, 0f);

        private Material plainMaterial;

        private SpriteRenderer Rent(Sprite sprite, int order)
        {
            return Rent(sprite, order, null);
        }

        private SpriteRenderer Rent(Sprite sprite, int order, Material material)
        {
            SpriteRenderer r;
            if (spare.Count > 0)
            {
                r = spare.Pop();
                r.enabled = true;
            }
            else
            {
                var go = new GameObject("Powder");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plainMaterial == null)
                {
                    plainMaterial = r.sharedMaterial;
                }
            }
            Material wanted = material != null ? material : plainMaterial;
            if (wanted != null && r.sharedMaterial != wanted)
            {
                r.sharedMaterial = wanted;
            }
            r.SetPropertyBlock(null);
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = Invisible;
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
            spare.Push(r);
        }

        private static void Place(SpriteRenderer r, Vector2 at, float w, float h)
        {
            if (r == null)
            {
                return;
            }
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            SetScale(r, w, h);
        }

        private static void SetScale(SpriteRenderer r, float w, float h)
        {
            Vector2 unit = r.sprite != null ? (Vector2)r.sprite.bounds.size : Vector2.one;
            r.transform.localScale = new Vector3(w / Mathf.Max(unit.x, 1e-4f),
                h / Mathf.Max(unit.y, 1e-4f), 1f);
        }

        private void Emit(Cue cue, float fullness)
        {
            if (Sounded != null)
            {
                Sounded(cue, Mathf.Clamp01(fullness));
            }
        }

        private static float EaseOut(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - (1f - k) * (1f - k);
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static float Hash(int key, int salt)
        {
            unchecked
            {
                uint h = (uint)(key * 73856093) ^ (uint)(salt * 83492791) ^ 0x9E3779B9u;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static float Hash(GridPos cell, int salt)
        {
            return Hash(cell.X * 7919 + cell.Y * 104729, salt);
        }

        // =================================================================== sprites

        private static Sprite dot;
        private static Sprite socket;
        private static Sprite core;
        private static Sprite notch;
        private static Sprite vein;
        private static Sprite strapBar;
        private static Sprite heatBar;
        private static Sprite smoke;
        private static Sprite ring;
        private static Sprite brokenRing;

        private static Sprite DynamiteTile
        {
            get { return ViewUtil.CubeTile(CubeKind.Dynamite); }
        }

        private static Sprite DotSprite
        {
            get
            {
                if (dot == null)
                {
                    dot = Bake(32, 32, (x, y) =>
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        float k = 1f - Mathf.Clamp01(d);
                        return new Color(1f, 1f, 1f, k * k);
                    });
                }
                return dot;
            }
        }

        /// <summary>A RECESSED chamber: a muted gold-brass rim, a soot-dark centre, the inside's
        /// top edge in shadow (it is a hole) and its bottom catching a hair of light.</summary>
        private static Sprite SocketSprite
        {
            get
            {
                if (socket == null)
                {
                    socket = Bake(32, 32, (x, y) =>
                    {
                        float sq = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                        float d = Mathf.Lerp(Mathf.Sqrt(x * x + y * y), sq, 0.45f);
                        float body = 1f - Mathf.Clamp01((d - 0.86f) / 0.12f);
                        float rim = Mathf.Clamp01((d - 0.6f) / 0.2f);
                        Color inside = Color.Lerp(Style.SocketDark, Style.SocketDark * 1.6f,
                            Mathf.Clamp01(-y * 0.8f + 0.2f));
                        Color c = Color.Lerp(inside, Style.Brass, rim);
                        c *= 1f - 0.35f * Mathf.Clamp01(y) * (1f - rim);
                        c.a = body;
                        return c;
                    });
                }
                return socket;
            }
        }

        private static Sprite CoreSprite
        {
            get
            {
                if (core == null)
                {
                    core = Bake(32, 32, (x, y) =>
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        float body = 1f - Mathf.Clamp01((d - 0.72f) / 0.2f);
                        float hot = 1f - Mathf.Clamp01(d / 0.6f);
                        float v = Mathf.Lerp(0.78f, 1f, hot);
                        return new Color(v, v, v, body);
                    });
                }
                return core;
            }
        }

        private static Sprite NotchSprite
        {
            get
            {
                if (notch == null)
                {
                    notch = Bake(16, 32, (x, y) =>
                    {
                        float dx = Mathf.Abs(x);
                        float dy = Mathf.Max(0f, Mathf.Abs(y) - 0.6f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy * 4f);
                        float body = 1f - Mathf.Clamp01((d - 0.7f) / 0.25f);
                        return new Color(1f, 1f, 1f, body);
                    });
                }
                return notch;
            }
        }

        /// <summary>A short soft vertical line of heat, fading at both ends and at its sides.
        /// </summary>
        private static Sprite VeinSprite
        {
            get
            {
                if (vein == null)
                {
                    vein = Bake(8, 32, (x, y) =>
                    {
                        float across = 1f - Mathf.Clamp01(Mathf.Abs(x));
                        float along = 1f - Mathf.Clamp01(Mathf.Abs(y) * 1.1f);
                        return new Color(1f, 1f, 1f, across * across * along);
                    });
                }
                return vein;
            }
        }

        private static Sprite StrapBarSprite
        {
            get
            {
                if (strapBar == null)
                {
                    strapBar = Bake(4, 32, (x, y) =>
                    {
                        float sy = y * 0.5f + 0.5f;
                        float edge = 1f - Mathf.Clamp01(Mathf.Min(sy, 1f - sy) / 0.32f);
                        Color c = Color.Lerp(new Color(0.075f, 0.08f, 0.09f),
                            new Color(0.23f, 0.24f, 0.27f), edge);
                        if (sy > 0.84f && sy < 0.97f)
                        {
                            c += new Color(0.14f, 0.14f, 0.15f);
                        }
                        c *= 1f - 0.25f * (1f - Mathf.Clamp01(sy / 0.2f));
                        c.a = 1f;
                        return c;
                    });
                }
                return strapBar;
            }
        }

        private static Sprite HeatBarSprite
        {
            get
            {
                if (heatBar == null)
                {
                    heatBar = Bake(32, 8, (x, y) =>
                    {
                        float k = (1f - Mathf.Clamp01(Mathf.Abs(x))) * (1f - Mathf.Clamp01(Mathf.Abs(y)));
                        return new Color(1f, 1f, 1f, k * k);
                    });
                }
                return heatBar;
            }
        }

        private static Sprite SmokeSprite
        {
            get
            {
                if (smoke == null)
                {
                    smoke = Bake(24, 24, (x, y) =>
                    {
                        float d = Mathf.Sqrt(x * x + y * y * 0.7f);
                        float k = 1f - Mathf.Clamp01(d);
                        float wisp = 0.75f + 0.25f * Mathf.Sin(x * 7f + y * 3f);
                        return new Color(1f, 1f, 1f, k * k * wisp);
                    });
                }
                return smoke;
            }
        }

        private static Sprite RingSprite
        {
            get
            {
                if (ring == null)
                {
                    ring = Bake(64, 64, (x, y) =>
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        float k = 1f - Mathf.Clamp01(Mathf.Abs(d - 0.88f) / 0.07f);
                        return new Color(1f, 1f, 1f, k * k);
                    });
                }
                return ring;
            }
        }

        private static Sprite BrokenRingSprite
        {
            get
            {
                if (brokenRing == null)
                {
                    brokenRing = Bake(64, 64, (x, y) =>
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        float a = Mathf.Atan2(y, x);
                        float arcs = Mathf.Clamp01(Mathf.Sin(a * 5f + Mathf.Sin(a * 3f) * 1.3f) * 2.5f);
                        float k = (1f - Mathf.Clamp01(Mathf.Abs(d - 0.86f) / 0.06f)) * arcs;
                        return new Color(1f, 1f, 1f, k * k);
                    });
                }
                return brokenRing;
            }
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
