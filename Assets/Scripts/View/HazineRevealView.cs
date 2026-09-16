// PURPOSE: "Hazine"'s finds - the hidden treasure and the hidden dynamite - drawn the moment the
// player blows one open, from the joker's own report.
//
// TWO DRAWN BURSTS ARE THE HERO LAYER, AND THEY ARE NOT THE WHOLE EFFECT. The eight-frame sheets
// (Resources/Art/Fx/hazine_treasure_sheet, hazine_dynamite_sheet, packed by Tools/ArtPrep) play
// at the found cell on the animator's own per-frame timing. Everything that makes a drawing belong
// to THIS board is code around it, laid on particular drawn frames rather than on a timer beside
// them: a beat of anticipation in the gap between the cube breaking and the reveal, a local light
// that follows the drawing's own brightness frame by frame and dies at its own edge, a few motes
// and glints (treasure) or charred chips, smoke and a soot mark that outlives the burst (dynamite),
// the RULES' number or verdict stamped at the peak frame, and the value carried as one small
// essence to wherever the effect actually went. A sheet played on its own is a sticker.
//
// THE VIEW DECIDES NOTHING. What was found, what it did and by how much are HazineVisuals: most
// treasure rewards and every dynamite penalty are not score, so the label is the report's effect
// (a discount percent, a refilled power, a bonus card, a drained power, a frozen card, a discarded
// hand, or nothing), and only the explosion bonus carries a number into the score - the MEASURED
// change, which runs negative under an inverted round and is drawn red and dipping when it does.
// Where the essence goes is the controller's answer (the score label, the power's panel, the card
// it froze or minted, the hand); with nowhere real to go it does not fly at all.
//
// IT NEVER POINTS AT THE OTHER MARK. Finding one removes the other, and where the other one lay
// is information the player never earned - the report does not carry it, and nothing here draws
// anywhere but the found cells. What says "the other one is gone too" is a single symbolic ember
// (or mote) that appears INSIDE the found burst's own light and is consumed there.
//
// BOTH AT ONCE CANCEL. Both bursts start and are cut short, a core leaves each cell for the point
// between them, and they meet in one small NEUTRAL burst - the treasure drawing's own tail, taken
// down to ivory - with a quiet label. No score, no reward, no penalty colour.
//
// THE DYNAMITE MOVES THE ARENA, NOT THE CAMERA: one board-local knock at the peak frame, handed to
// BoardView as its own term so it composes with a quake tremor and the overtime squeeze instead
// of fighting them. The treasure moves nothing.
//
// Keyed on the report's IDENTITY (CLAUDE.md), on the scaled clock so the lab's time scale slows
// the drawings and the code together, and every beat is announced through Sounded / Haptic.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class HazineRevealView : MonoBehaviour
    {
        public static class Style
        {
            // ---- the drawings, and the animator's own timing (ms per frame)
            public static readonly float[] TreasureMs = { 70f, 80f, 90f, 100f, 120f, 100f, 90f, 90f };
            public static readonly float[] DynamiteMs = { 70f, 70f, 65f, 80f, 90f, 100f, 100f, 90f };

            /// <summary>Drawn size in CELLS. The packed frames reach 0.98 of their half-box at
            /// the peak, so these are the burst's diameter at its widest.</summary>
            public static float TreasureSize = 1.40f;
            public static float DynamiteSize = 1.40f;

            /// <summary>Which drawn frame is the PEAK - the number lands on it, and so does the
            /// dynamite's knock and the treasure's glint.</summary>
            public const int TreasurePeakFrame = 4;
            public const int DynamitePeakFrame = 3;

            // ---- the gap between the cube breaking and the reveal
            public static float TreasureGap = 0.050f;
            /// <summary>The dynamite's is its TENSION: a beat of pressure before it goes.</summary>
            public static float DynamiteGap = 0.055f;
            public static float TensionPinch = 0.975f;

            // ---- the local light, following the drawing's brightness frame by frame
            public static readonly float[] TreasureLight = { 0.05f, 0.12f, 0.25f, 0.45f, 1f, 0.75f, 0.35f, 0.10f };
            public static readonly float[] DynamiteLight = { 0.10f, 0.30f, 0.55f, 1f, 0.80f, 0.50f, 0.30f, 0.12f };
            public static float TreasureLightPeak = 0.18f;
            public static float DynamiteLightPeak = 0.24f;
            /// <summary>Diameter in cells. It dies at its own edge, so it can be wider than the
            /// drawing without ever reading as a plate.</summary>
            public static float LightSize = 2.3f;
            public static Color Amber = new Color(1f, 0.74f, 0.32f);
            public static Color Blast = new Color(1f, 0.52f, 0.20f);

            // ---- treasure support
            public static int MotesMin = 4;
            public static int MotesMax = 8;
            public static float MoteSize = 0.10f;
            public static float MoteRise = 0.38f;
            public static float MoteLife = 0.62f;
            public static float GlintSize = 0.46f;
            public static float GlintLife = 0.20f;

            // ---- dynamite support
            public static int FragmentsMin = 3;
            public static int FragmentsMax = 7;
            public static float FragmentSpeedMin = 1.6f;
            public static float FragmentSpeedMax = 2.7f;
            public static float FragmentGravity = 4.2f;
            public static float FragmentLife = 0.46f;
            public static int PuffsMin = 2;
            public static int PuffsMax = 4;
            public static float PuffLife = 0.90f;
            public static float PuffAlpha = 0.34f;
            public static Color Smoke = new Color(0.36f, 0.31f, 0.28f);
            /// <summary>Soot, in cells. It stays inside the cell it burnt.</summary>
            public static float ScorchSize = 0.92f;
            public static float ScorchAlpha = 0.55f;
            public static float ScorchHold = 0.30f;
            public static float ScorchFade = 1.00f;
            /// <summary>World units: a couple of pixels. Board-local, never the camera.</summary>
            public static float Impulse = 0.022f;
            public static float ImpulseLife = 0.20f;

            // ---- the verdict
            public static float NumberHeight = 0.42f;
            public static float WordHeight = 0.30f;
            /// <summary>Above the cell, clear of the burst's own bright peak - gold on a gold
            /// frame is no number at all.</summary>
            public static float NumberLift = 0.95f;
            public static float StampFrom = 0.70f;
            public static float StampPunch = 1.12f;
            public static float StampTime = 0.16f;
            public static float NumberHold = 0.14f;
            public static float GatherTime = 0.12f;
            public static float PenaltyDrop = 0.10f;
            public static int TextFont = 64;
            public static Color Gold = new Color(1f, 0.80f, 0.30f);
            public static Color GoldShade = new Color(0.30f, 0.16f, 0.04f);
            public static Color Loss = new Color(0.88f, 0.24f, 0.19f);
            public static Color Burnt = new Color(0.94f, 0.49f, 0.17f);
            public static Color BurntShade = new Color(0.18f, 0.07f, 0.03f);
            public static Color Neutral = new Color(0.82f, 0.80f, 0.74f);
            public static Color NeutralShade = new Color(0.14f, 0.13f, 0.12f);
            public static float TextBacking = 0.34f;

            // ---- the essence
            public static float EssenceSize = 0.20f;
            public static float EssenceFlight = 0.50f;
            public static float EssenceLift = 0.9f;
            public static int TrailCount = 3;
            public static float ArriveRing = 0.40f;
            public static float ArriveTime = 0.30f;

            // ---- the score's answer (only for a score that really moved)
            public static float ScorePunch = 0.07f;
            public static float ScoreDip = 0.04f;
            public static float ScoreTime = 0.36f;

            // ---- the cancel
            public const int CancelCutFrame = 3;
            public static float CancelFlight = 0.26f;
            public static float CancelSize = 0.90f;
            public static readonly float[] CancelMs = { 70f, 80f, 90f };
            public static Color CancelTint = new Color(0.86f, 0.84f, 0.78f, 0.85f);
            public static float CancelLabelTime = 0.55f;

            // ---- the counterpart
            public static float CounterpartLife = 0.26f;
            public static float CounterpartSize = 0.16f;
        }

        public static class Layers
        {
            public static bool ShowAnticipation = true;
            public static bool ShowSheet = true;
            public static bool ShowLight = true;
            public static bool ShowSparkle = true;   // motes + glints
            public static bool ShowDebris = true;    // fragments
            public static bool ShowSmoke = true;
            public static bool ShowScorch = true;
            public static bool ShowVerdict = true;
            public static bool ShowEssence = true;
            public static bool ShowImpulse = true;
            public static bool ShowTargetResponse = true;
            public static bool ShowCounterpart = true;

            // debug
            public static bool ShowFrameDebug;
            public static bool ShowCellDebug;

            public static void AllOn()
            {
                ShowAnticipation = ShowSheet = ShowLight = ShowSparkle = ShowDebris = true;
                ShowSmoke = ShowScorch = ShowVerdict = ShowEssence = ShowImpulse = true;
                ShowTargetResponse = ShowCounterpart = true;
                ShowFrameDebug = ShowCellDebug = false;
            }
        }

        public static System.Action<string> Sounded;
        public static System.Action<string> Haptic;

        public const string SoundTreasureReveal = "hazine.treasure.reveal";
        public const string SoundTreasurePeak = "hazine.treasure.peak";
        public const string SoundDynamiteTension = "hazine.dynamite.tension";
        public const string SoundDynamiteBlast = "hazine.dynamite.blast";
        public const string SoundVerdict = "hazine.verdict";
        public const string SoundEssenceArrive = "hazine.essence.arrive";
        public const string SoundCancelCollide = "hazine.cancel.collide";
        public const string HapticBlast = "hazine.haptic.blast";
        public const string HapticTreasure = "hazine.haptic.treasure";

        private static readonly FrameSequenceFx.Sheet TreasureSheet = new FrameSequenceFx.Sheet(
            "Art/Fx/hazine_treasure_sheet", 4, 2, Style.TreasureMs);
        private static readonly FrameSequenceFx.Sheet DynamiteSheet = new FrameSequenceFx.Sheet(
            "Art/Fx/hazine_dynamite_sheet", 4, 2, Style.DynamiteMs);
        /// <summary>The treasure drawing's own tail, as the cancel's neutral burst.</summary>
        private static readonly FrameSequenceFx.Sheet CancelSheet = new FrameSequenceFx.Sheet(
            "Art/Fx/hazine_treasure_sheet", 4, 2, 5, Style.CancelMs);

        private const int ScorchOrder = 12;
        private const int LightOrder = 40;
        private const int SheetOrder = 42;
        private const int ParticleOrder = 43;
        private const int SmokeOrder = 44;
        private const int BackingOrder = 90;
        private const int ShadeOrder = 91;
        private const int TextOrder = 92;
        private const int EssenceOrder = 93;
        private const int DebugOrder = 95;

        /// <summary>Where the found value goes, in world space - the controller's answer.
        /// </summary>
        public struct Destination
        {
            public bool Has;
            public Vector2 Point;
            public bool IsScore;
            public System.Action OnArrive;
        }

        // ---- inputs
        private HazineVisuals report;
        private HazineVisuals lastPlayed;
        private System.Func<GridPos, Vector2> cellWorld;
        private float cell;
        private float[] breakAt;
        private Vector2 boardCentre;
        private Destination destination;
        private Rect clampRect;

        /// <summary>Board-local knock, handed to BoardView as its own term.</summary>
        public System.Action<Vector2> Impulse;

        // ---- state
        private float clock;
        private float revealAt;
        private bool active;
        private FrameSequenceFx sheets;
        private readonly List<FrameSequenceFx.Play> bursts = new List<FrameSequenceFx.Play>();
        private FrameSequenceFx.Play cancelBurst;
        private readonly List<SpriteRenderer> lights = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> cues = new List<SpriteRenderer>();
        private readonly List<Bit> bits = new List<Bit>();
        private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        private SpriteRenderer scorch;
        private SpriteRenderer backing;
        private TextMesh verdict;
        private TextMesh verdictShade;
        private TextMesh debug;
        private SpriteRenderer essence;
        private readonly SpriteRenderer[] trail = new SpriteRenderer[4];
        private SpriteRenderer arriveRing;
        private readonly List<SpriteRenderer> debugFrames = new List<SpriteRenderer>();
        private readonly HashSet<string> said = new HashSet<string>();
        private float verdictAt;
        private float launchAt;
        private float arrivedAt = -1f;
        private Vector2 verdictPos;
        private float impulseAt = -1f;
        private Vector2 impulseDir;
        private float cancelLaunchAt;

        /// <summary>The score label's answer, published for the controller's single writer
        /// (TickScoreResponse). 1 = untouched.</summary>
        public float ScoreScale { get; private set; }

        /// <summary>How strongly the label should take ScoreInk (0 = not at all).</summary>
        public float ScoreClaim { get; private set; }

        public Color ScoreInk { get; private set; }

        public bool Busy
        {
            get { return active; }
        }

        /// <summary>For the lab's frame debug: the frame each burst is on.</summary>
        public string DebugState { get; private set; }

        private sealed class Bit
        {
            public SpriteRenderer R;
            public float Born;
            public float Life;
            public Vector2 From;
            public Vector2 Velocity;
            public float Gravity;
            public float Spin;
            public float Size0;
            public float Size1;
            public Color Tint;
            public float Alpha;
            public float Rise;
            public int Kind; // 0 mote, 1 glint, 2 fragment, 3 puff, 4 counterpart
        }

        private void Awake()
        {
            ScoreScale = 1f;
            sheets = new FrameSequenceFx(transform, SheetOrder);
        }

        /// <summary>
        /// Plays a find. <paramref name="breakDelays"/> is when each discovery's cube breaks on
        /// screen (same order as the report's discoveries), measured by the controller off the
        /// destruction that took it - the reveal follows that, never a timer of its own.
        /// </summary>
        public void Play(HazineVisuals find, System.Func<GridPos, Vector2> toWorld, float cellSize,
            IReadOnlyList<float> breakDelays, Vector2 centre, Destination dest, Rect visible)
        {
            if (find == null || find.Discoveries.Count == 0 || toWorld == null)
            {
                return;
            }
            if (ReferenceEquals(find, lastPlayed))
            {
                return;
            }
            Stop();
            lastPlayed = find;
            report = find;
            cellWorld = toWorld;
            cell = cellSize;
            boardCentre = centre;
            destination = dest;
            clampRect = visible;
            breakAt = new float[find.Discoveries.Count];
            float latest = 0f;
            for (int i = 0; i < breakAt.Length; i++)
            {
                breakAt[i] = breakDelays != null && i < breakDelays.Count ? Mathf.Max(0f, breakDelays[i]) : 0f;
                latest = Mathf.Max(latest, breakAt[i]);
            }
            float gap = find.IsPenalty ? Style.DynamiteGap : Style.TreasureGap;
            revealAt = latest + gap;
            clock = 0f;
            active = true;
            said.Clear();
            arrivedAt = -1f;
            impulseAt = -1f;
            ScoreScale = 1f;
            ScoreClaim = 0f;

            var rng = new Lcg(find.Seed);
            for (int i = 0; i < find.Discoveries.Count; i++)
            {
                HazineDiscovery d = find.Discoveries[i];
                FrameSequenceFx.Play burst = sheets.Start(d.IsTreasure ? TreasureSheet : DynamiteSheet,
                    toWorld(d.Cell), (d.IsTreasure ? Style.TreasureSize : Style.DynamiteSize) * cell);
                burst.Clock = -revealAt;
                burst.Degrees = rng.Range(-14f, 14f);
                burst.Mirror = rng.Next() < 0.5f;
                if (find.Result == HazineResult.BothCancelled)
                {
                    burst.StopAt = Style.CancelCutFrame;
                    burst.FadeOut = 0.08f;
                }
                bursts.Add(burst);
                SpriteRenderer light = Rent(LightOrder);
                light.sprite = HazineShapes.Light(rng.Int(0, HazineShapes.LightVariants));
                light.transform.rotation = Quaternion.Euler(0f, 0f, rng.Range(0f, 360f));
                lights.Add(light);
                SpriteRenderer cue = Rent(ParticleOrder);
                cue.sprite = HazineShapes.Mote;
                cues.Add(cue);
            }
            sheets.Visible = Layers.ShowSheet;

            HazineDiscovery first = find.Discoveries[0];
            Vector2 at = toWorld(first.Cell);
            if (find.Result == HazineResult.BothCancelled)
            {
                cancelLaunchAt = revealAt + TreasureSheet.StartOf(2) + 0.02f;
                verdictAt = cancelLaunchAt + Style.CancelFlight;
                launchAt = float.MaxValue;
            }
            else
            {
                int peak = first.IsTreasure ? Style.TreasurePeakFrame : Style.DynamitePeakFrame;
                FrameSequenceFx.Sheet sheet = first.IsTreasure ? TreasureSheet : DynamiteSheet;
                // The verdict lands on the TREASURE's peak drawing; the dynamite's comes one frame
                // after its blast, once the fireball has something to say.
                verdictAt = revealAt + sheet.StartOf(first.IsTreasure ? peak : peak + 1);
                launchAt = revealAt + sheet.Duration + Style.NumberHold;
                if (!first.IsTreasure)
                {
                    impulseAt = revealAt + sheet.StartOf(peak);
                    Vector2 away = at - boardCentre;
                    impulseDir = away.sqrMagnitude > 0.0001f ? -away.normalized : Vector2.down;
                }
            }
            verdictPos = ClampToView(at + new Vector2(0f, Style.NumberLift * cell));
            Plan(find, rng);
            Tick(0f);
        }

        /// <summary>Particles are decided up front from the report's seed, so a replay at a
        /// different time scale is the same effect.</summary>
        private void Plan(HazineVisuals find, Lcg rng)
        {
            for (int i = 0; i < find.Discoveries.Count; i++)
            {
                HazineDiscovery d = find.Discoveries[i];
                Vector2 at = cellWorld(d.Cell);
                bool cancelled = find.Result == HazineResult.BothCancelled;
                if (d.IsTreasure)
                {
                    int motes = cancelled ? 2 : rng.Int(Style.MotesMin, Style.MotesMax + 1);
                    float moteAt = revealAt + TreasureSheet.StartOf(2);
                    for (int m = 0; m < motes; m++)
                    {
                        float ang = rng.Range(0f, Mathf.PI * 2f);
                        float rad = rng.Range(0.18f, 0.45f) * cell;
                        AddBit(0, moteAt + m * 0.03f, Style.MoteLife * rng.Range(0.8f, 1.15f),
                            at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad,
                            new Vector2(rng.Range(-0.08f, 0.08f) * cell, 0f), 0f, 0f,
                            Style.MoteSize * cell * rng.Range(0.7f, 1.2f), Style.MoteSize * cell * 0.4f,
                            Color.Lerp(Style.Amber, Color.white, rng.Range(0.2f, 0.6f)), 1f,
                            Style.MoteRise * cell * rng.Range(0.7f, 1.2f), HazineShapes.Mote);
                    }
                    if (!cancelled)
                    {
                        int glints = 1 + (rng.Next() < 0.6f ? 1 : 0);
                        for (int g = 0; g < glints; g++)
                        {
                            float ang = rng.Range(0f, Mathf.PI * 2f);
                            Vector2 p = at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rng.Range(0.22f, 0.38f) * cell;
                            float born = revealAt + TreasureSheet.StartOf(Style.TreasurePeakFrame - 1 + g) + 0.02f;
                            AddBit(1, born, Style.GlintLife, p, Vector2.zero, 0f, rng.Range(-60f, 60f),
                                Style.GlintSize * cell, Style.GlintSize * cell, Color.white, 0.95f, 0f,
                                HazineShapes.Glint);
                        }
                        if (find.Discoveries.Count == 1)
                        {
                            // The dynamite is gone too - said INSIDE this light, never at its cell.
                            AddBit(4, revealAt + TreasureSheet.StartOf(5), Style.CounterpartLife,
                                at + new Vector2(rng.Range(-0.1f, 0.1f), rng.Range(-0.05f, 0.1f)) * cell,
                                Vector2.zero, 0f, 0f, Style.CounterpartSize * cell, 0f, Style.Blast, 0.75f,
                                0f, HazineShapes.Mote);
                        }
                    }
                }
                else
                {
                    if (!cancelled)
                    {
                        float blast = revealAt + DynamiteSheet.StartOf(Style.DynamitePeakFrame);
                        int chips = rng.Int(Style.FragmentsMin, Style.FragmentsMax + 1);
                        for (int f = 0; f < chips; f++)
                        {
                            float ang = (f + rng.Range(-0.3f, 0.3f)) / chips * Mathf.PI * 2f;
                            float speed = rng.Range(Style.FragmentSpeedMin, Style.FragmentSpeedMax) * cell;
                            SpriteRenderer chip = AddBit(2, blast, Style.FragmentLife * rng.Range(0.8f, 1.1f), at,
                                new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) + 0.35f) * speed,
                                Style.FragmentGravity * cell, rng.Range(-540f, 540f),
                                rng.Range(0.10f, 0.17f) * cell, rng.Range(0.06f, 0.10f) * cell,
                                Color.white, 1f, 0f, HazineShapes.Fragment(rng.Int(0, HazineShapes.FragmentVariants))).R;
                            chip.transform.rotation = Quaternion.Euler(0f, 0f, rng.Range(0f, 360f));
                        }
                        int puffs = rng.Int(Style.PuffsMin, Style.PuffsMax + 1);
                        float smokeAt = revealAt + DynamiteSheet.StartOf(5);
                        for (int p = 0; p < puffs; p++)
                        {
                            Vector2 off = new Vector2(rng.Range(-0.2f, 0.2f), rng.Range(-0.12f, 0.15f)) * cell;
                            AddBit(3, smokeAt + p * 0.05f, Style.PuffLife * rng.Range(0.85f, 1.15f), at + off,
                                new Vector2(rng.Range(-0.1f, 0.1f) * cell, 0f), 0f, rng.Range(-25f, 25f),
                                rng.Range(0.45f, 0.55f) * cell, rng.Range(0.8f, 1.0f) * cell,
                                Style.Smoke, Style.PuffAlpha, 0.25f * cell,
                                HazineShapes.Puff(rng.Int(0, HazineShapes.PuffVariants)));
                        }
                        if (find.Discoveries.Count == 1)
                        {
                            // The treasure went with it - a single gold mote, eaten by the fire.
                            AddBit(4, revealAt + DynamiteSheet.StartOf(4), Style.CounterpartLife,
                                at + new Vector2(rng.Range(-0.1f, 0.1f), rng.Range(-0.05f, 0.1f)) * cell,
                                Vector2.zero, 0f, 0f, Style.CounterpartSize * cell, 0f, Style.Amber, 0.7f,
                                0f, HazineShapes.Mote);
                        }
                    }
                }
            }
            if (find.Result == HazineResult.BothCancelled)
            {
                Vector2 mid = Midpoint();
                float collide = cancelLaunchAt + Style.CancelFlight;
                for (int m = 0; m < 4; m++)
                {
                    float ang = (m + 0.5f) / 4f * Mathf.PI * 2f + rng.Range(-0.3f, 0.3f);
                    AddBit(0, collide + 0.02f, Style.MoteLife * 0.8f, mid,
                        new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.5f * cell, 0f, 0f,
                        Style.MoteSize * cell, Style.MoteSize * cell * 0.3f, Style.Neutral, 0.8f, 0.1f * cell,
                        HazineShapes.Mote);
                }
            }
        }

        private Bit AddBit(int kind, float born, float life, Vector2 from, Vector2 velocity, float gravity,
            float spin, float size0, float size1, Color tint, float alpha, float rise, Sprite sprite)
        {
            var bit = new Bit
            {
                Kind = kind, Born = born, Life = life, From = from, Velocity = velocity,
                Gravity = gravity, Spin = spin, Size0 = size0, Size1 = size1, Tint = tint,
                Alpha = alpha, Rise = rise
            };
            bit.R = Rent(kind == 3 ? SmokeOrder : ParticleOrder);
            bit.R.sprite = sprite;
            bit.R.enabled = false;
            bits.Add(bit);
            return bit;
        }

        private Vector2 Midpoint()
        {
            Vector2 a = cellWorld(report.Discoveries[0].Cell);
            Vector2 b = cellWorld(report.Discoveries[report.Discoveries.Count - 1].Cell);
            return (a + b) * 0.5f;
        }

        private void Update()
        {
            if (active)
            {
                Tick(Time.deltaTime);
            }
        }

        private void Tick(float dt)
        {
            clock += dt;
            sheets.Visible = Layers.ShowSheet;
            sheets.Tick(dt);
            bool cancelled = report.Result == HazineResult.BothCancelled;

            var state = new System.Text.StringBuilder();
            for (int i = 0; i < report.Discoveries.Count; i++)
            {
                HazineDiscovery d = report.Discoveries[i];
                Vector2 at = cellWorld(d.Cell);
                FrameSequenceFx.Play burst = bursts[i];
                burst.Position = at;
                float through;
                int frame = burst.Sheet.FrameAt(clock - revealAt, out through);
                PaintAnticipation(i, d, at);
                PaintLight(i, d, at, frame, through, cancelled);
                if (d.IsTreasure && frame == 0) { Say(Sounded, SoundTreasureReveal); }
                if (d.IsTreasure && frame == Style.TreasurePeakFrame)
                {
                    Say(Sounded, SoundTreasurePeak);
                    Say(Haptic, HapticTreasure);
                }
                if (!d.IsTreasure && clock >= breakAt[i]) { Say(Sounded, SoundDynamiteTension); }
                if (!d.IsTreasure && frame == Style.DynamitePeakFrame && !cancelled)
                {
                    Say(Sounded, SoundDynamiteBlast);
                    Say(Haptic, HapticBlast);
                }
                if (Layers.ShowFrameDebug)
                {
                    float ms = (clock - revealAt) * 1000f;
                    state.Append(d.IsTreasure ? "T" : "D").Append(" F")
                        .Append(frame < 0 ? "-" : frame >= burst.Sheet.Count ? "end" : frame.ToString())
                        .Append(" ").Append(Mathf.RoundToInt(ms)).Append("ms  ");
                }
                if (!d.IsTreasure && !cancelled)
                {
                    PaintScorch(at);
                }
            }
            // The essence renderers are shared with the cancel's two cores, so they are cleared
            // FIRST and the cancel lights the ones it needs after.
            PaintEssence();
            if (cancelled)
            {
                PaintCancel();
            }
            PaintBits(dt);
            PaintImpulse();
            PaintVerdict();
            PaintScore();
            DebugState = state.ToString();
            PaintDebug();

            if (clock > EndTime())
            {
                Stop();
            }
        }

        private float EndTime()
        {
            float end = revealAt + DynamiteSheet.Duration + Style.ScorchHold + Style.ScorchFade;
            end = Mathf.Max(end, revealAt + TreasureSheet.Duration + 0.2f);
            if (report.Result == HazineResult.BothCancelled)
            {
                end = Mathf.Max(end, verdictAt + Style.CancelLabelTime + 0.2f);
            }
            else if (arrivedAt >= 0f)
            {
                end = Mathf.Max(end, arrivedAt + Mathf.Max(Style.ArriveTime, Style.ScoreTime) + 0.05f);
            }
            else if (EssenceWillFly())
            {
                end = float.MaxValue;
            }
            else
            {
                end = Mathf.Max(end, launchAt + Style.GatherTime + 0.1f);
            }
            for (int i = 0; i < bits.Count; i++)
            {
                end = Mathf.Max(end, bits[i].Born + bits[i].Life);
            }
            return end;
        }

        // ---- beats -----------------------------------------------------------------------------

        private void PaintAnticipation(int i, HazineDiscovery d, Vector2 at)
        {
            SpriteRenderer cue = cues[i];
            float span = revealAt - breakAt[i];
            float k = span > 0f ? (clock - breakAt[i]) / span : -1f;
            if (!Layers.ShowAnticipation || k < 0f || clock > revealAt + 0.03f)
            {
                cue.enabled = false;
                return;
            }
            cue.enabled = true;
            k = Mathf.Clamp01(k);
            float size = Mathf.Lerp(0.05f, 0.30f, k) * cell;
            if (!d.IsTreasure)
            {
                // TENSION: it swells, then pinches just before it goes.
                size *= k > 0.7f ? Mathf.Lerp(1f, Style.TensionPinch, (k - 0.7f) / 0.3f) : 1f;
            }
            cue.transform.position = at;
            cue.transform.localScale = new Vector3(size, size, 1f);
            Color c = d.IsTreasure ? Style.Amber : Style.Blast;
            c.a = Mathf.Lerp(0.2f, 0.9f, k);
            cue.color = c;
        }

        private void PaintLight(int i, HazineDiscovery d, Vector2 at, int frame, float through, bool cancelled)
        {
            SpriteRenderer light = lights[i];
            float[] curve = d.IsTreasure ? Style.TreasureLight : Style.DynamiteLight;
            float level = 0f;
            if (frame >= 0 && frame < curve.Length)
            {
                float next = frame + 1 < curve.Length ? curve[frame + 1] : 0f;
                level = Mathf.Lerp(curve[frame], next, through);
            }
            if (cancelled)
            {
                // Cut with the drawing, fading over its own fade.
                float cut = (d.IsTreasure ? TreasureSheet : DynamiteSheet).StartOf(Style.CancelCutFrame) + revealAt;
                if (clock > cut)
                {
                    level = curve[Style.CancelCutFrame - 1] * (1f - Mathf.Clamp01((clock - cut) / 0.1f));
                }
            }
            if (!Layers.ShowLight || level <= 0.001f)
            {
                light.enabled = false;
                return;
            }
            light.enabled = true;
            float peak = d.IsTreasure ? Style.TreasureLightPeak : Style.DynamiteLightPeak;
            float size = Style.LightSize * cell * (0.8f + 0.2f * level);
            light.transform.position = at;
            light.transform.localScale = new Vector3(size, size, 1f);
            Color c = d.IsTreasure ? Style.Amber : Style.Blast;
            c.a = level * peak;
            light.color = c;
        }

        private void PaintScorch(Vector2 at)
        {
            float from = revealAt + DynamiteSheet.StartOf(4);
            float hold = revealAt + DynamiteSheet.Duration + Style.ScorchHold;
            if (!Layers.ShowScorch || clock < from)
            {
                if (scorch != null) { scorch.enabled = false; }
                return;
            }
            if (scorch == null)
            {
                scorch = Rent(ScorchOrder);
                scorch.sprite = HazineShapes.Scorch;
                scorch.transform.rotation = Quaternion.Euler(0f, 0f, (report.Seed % 360u));
            }
            float a = clock < from + 0.1f ? (clock - from) / 0.1f
                : clock < hold ? 1f
                : 1f - Mathf.Clamp01((clock - hold) / Style.ScorchFade);
            scorch.enabled = a > 0f;
            float s = Style.ScorchSize * cell;
            scorch.transform.position = at;
            scorch.transform.localScale = new Vector3(s, s, 1f);
            scorch.color = new Color(1f, 1f, 1f, a * Style.ScorchAlpha);
        }

        private void PaintBits(float dt)
        {
            for (int i = 0; i < bits.Count; i++)
            {
                Bit b = bits[i];
                float age = clock - b.Born;
                bool layer = b.Kind == 0 || b.Kind == 1 ? Layers.ShowSparkle
                    : b.Kind == 2 ? Layers.ShowDebris
                    : b.Kind == 3 ? Layers.ShowSmoke
                    : Layers.ShowCounterpart;
                if (!layer || age < 0f || age > b.Life)
                {
                    b.R.enabled = false;
                    continue;
                }
                float k = age / b.Life;
                b.R.enabled = true;
                Vector2 p = b.From + b.Velocity * age + new Vector2(0f, -0.5f * b.Gravity * age * age)
                    + new Vector2(0f, b.Rise * Ease(k));
                b.R.transform.position = p;
                float size;
                float alpha;
                switch (b.Kind)
                {
                    case 1: // glint: grows, holds, shrinks
                        size = b.Size0 * Mathf.Sin(k * Mathf.PI);
                        alpha = b.Alpha;
                        b.R.transform.rotation = Quaternion.Euler(0f, 0f, b.Spin * k);
                        break;
                    case 2: // chip: flies, darkens, fades late
                        size = Mathf.Lerp(b.Size0, b.Size1, k);
                        alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                        b.R.transform.rotation *= Quaternion.Euler(0f, 0f, b.Spin * dt);
                        b.Tint = Color.Lerp(Color.white, new Color(0.55f, 0.52f, 0.5f), k);
                        break;
                    case 3: // puff: swells and thins
                        size = Mathf.Lerp(b.Size0, b.Size1, Ease(k));
                        alpha = b.Alpha * (k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f);
                        b.R.transform.rotation = Quaternion.Euler(0f, 0f, b.Spin * k);
                        break;
                    case 4: // the counterpart: appears and is eaten where it stands
                        size = b.Size0 * (1f - k);
                        alpha = b.Alpha * (k < 0.25f ? k / 0.25f : 1f);
                        break;
                    default: // mote
                        size = Mathf.Lerp(b.Size0, b.Size1, k);
                        alpha = b.Alpha * (k < 0.12f ? k / 0.12f : 1f - (k - 0.12f) / 0.88f);
                        break;
                }
                b.R.transform.localScale = new Vector3(size, size, 1f);
                Color c = b.Tint;
                c.a *= alpha;
                b.R.color = c;
            }
        }

        private void PaintImpulse()
        {
            if (Impulse == null || impulseAt < 0f)
            {
                return;
            }
            float age = clock - impulseAt;
            if (age < 0f)
            {
                return;
            }
            if (!Layers.ShowImpulse || age > Style.ImpulseLife)
            {
                Impulse(Vector2.zero);
                impulseAt = Layers.ShowImpulse ? -1f : impulseAt;
                return;
            }
            // A fast shove away from the blast, one soft overshoot back, and still.
            float k = age / Style.ImpulseLife;
            float shape = age < 0.03f ? age / 0.03f
                : Mathf.Cos((k - 0.15f) / 0.85f * Mathf.PI * 1.5f) * (1f - k);
            Impulse(impulseDir * Style.Impulse * shape);
        }

        private string VerdictText(out Color ink, out Color shade, out bool isNumber)
        {
            isNumber = false;
            ink = Style.Gold;
            shade = Style.GoldShade;
            switch (report.Effect)
            {
                case HazineEffect.ExplosionBonus:
                    isNumber = true;
                    if (report.ScoreDelta < 0)
                    {
                        ink = Style.Loss;
                        return "-" + (-report.ScoreDelta);
                    }
                    return "+" + report.ScoreDelta;
                case HazineEffect.MarketDiscount:
                    return Loc.Pick(report.Amount + "% OFF", "%" + report.Amount + " İNDİRİM");
                case HazineEffect.PowerRefilled:
                    return Loc.Pick("POWER +1", "GÜÇ +1");
                case HazineEffect.BonusCard:
                    return Loc.Pick("+1 CARD", "+1 KART");
            }
            ink = Style.Burnt;
            shade = Style.BurntShade;
            switch (report.Effect)
            {
                case HazineEffect.PowerDrained:
                    return Loc.Pick("POWER -1", "GÜÇ -1");
                case HazineEffect.CardFrozen:
                    return Loc.Pick("FROZEN " + report.Amount, report.Amount + " TUR DONDU");
                case HazineEffect.HandDiscarded:
                    return Loc.Pick("HAND -" + report.Amount, "EL -" + report.Amount);
            }
            ink = Style.Neutral;
            shade = Style.NeutralShade;
            return report.Effect == HazineEffect.Fizzled
                ? Loc.Pick("NO EFFECT", "ETKİSİZ")
                : Loc.Pick("CANCELLED", "İPTAL");
        }

        private void PaintVerdict()
        {
            if (!Layers.ShowVerdict || clock < verdictAt)
            {
                HideVerdict();
                return;
            }
            Color ink;
            Color shade;
            bool isNumber;
            string text = VerdictText(out ink, out shade, out isNumber);
            bool cancelled = report.Result == HazineResult.BothCancelled;
            float age = clock - verdictAt;
            float scale;
            if (age < Style.StampTime * 0.55f)
            {
                scale = Mathf.Lerp(Style.StampFrom, Style.StampPunch, Ease(age / (Style.StampTime * 0.55f)));
            }
            else
            {
                scale = Mathf.Lerp(Style.StampPunch, 1f,
                    Ease(Mathf.Clamp01((age - Style.StampTime * 0.55f) / (Style.StampTime * 0.45f))));
            }
            float alpha = 1f;
            Vector2 pos = cancelled ? ClampToView(Midpoint() + new Vector2(0f, 0.5f * cell)) : verdictPos;
            if (report.IsPenalty)
            {
                pos.y -= Style.PenaltyDrop * cell * Ease(Mathf.Clamp01(age / 0.4f));
            }
            float end = cancelled ? verdictAt + Style.CancelLabelTime : launchAt;
            if (clock > end)
            {
                float g = Mathf.Clamp01((clock - end) / Style.GatherTime);
                scale *= Mathf.Lerp(1f, 0.5f, g);
                alpha = 1f - g;
            }
            if (alpha <= 0f)
            {
                HideVerdict();
                return;
            }
            if (verdict == null)
            {
                backing = Rent(BackingOrder);
                backing.sprite = HazineShapes.Light(0);
                verdictShade = ViewUtil.MakeText3D(transform, "HazineVerdictShade", Vector2.zero,
                    string.Empty, Style.TextFont, 0.03f, shade, ShadeOrder, TextAnchor.MiddleCenter);
                verdict = ViewUtil.MakeText3D(transform, "HazineVerdict", Vector2.zero, string.Empty,
                    Style.TextFont, 0.03f, ink, TextOrder, TextAnchor.MiddleCenter);
            }
            if (!said.Contains(SoundVerdict)) { Say(Sounded, SoundVerdict); }
            float height = (isNumber ? Style.NumberHeight : Style.WordHeight) * cell
                * (cancelled ? 0.8f : 1f) * scale;
            verdict.gameObject.SetActive(true);
            verdictShade.gameObject.SetActive(true);
            verdict.text = text;
            verdictShade.text = text;
            verdict.characterSize = Mathf.Max(height * 10f / Style.TextFont, 0.0001f);
            verdictShade.characterSize = verdict.characterSize;
            verdict.transform.position = pos;
            float drop = height * 0.06f;
            verdictShade.transform.position = pos + new Vector2(drop, -drop);
            ink.a = alpha;
            shade.a = alpha * 0.85f;
            verdict.color = ink;
            verdictShade.color = shade;
            // A soft dark pool under the words, so they read over a lit neighbour too. It fades
            // to nothing at its own edge - never a label plate.
            backing.enabled = true;
            backing.transform.position = pos;
            float w = height * Mathf.Max(2.2f, text.Length * 0.62f);
            backing.transform.localScale = new Vector3(w, height * 2.2f, 1f);
            backing.color = new Color(0f, 0f, 0f, Style.TextBacking * alpha);
        }

        private void HideVerdict()
        {
            if (verdict != null)
            {
                verdict.gameObject.SetActive(false);
                verdictShade.gameObject.SetActive(false);
                backing.enabled = false;
            }
        }

        private bool EssenceWillFly()
        {
            if (!destination.Has || report.Result == HazineResult.BothCancelled)
            {
                return false;
            }
            return report.Effect != HazineEffect.Fizzled && report.Effect != HazineEffect.None;
        }

        private void PaintEssence()
        {
            if (essence == null)
            {
                essence = Rent(EssenceOrder);
                essence.sprite = HazineShapes.Essence;
                for (int t = 0; t < trail.Length; t++)
                {
                    trail[t] = Rent(EssenceOrder - 1);
                    trail[t].sprite = HazineShapes.Mote;
                }
                arriveRing = Rent(EssenceOrder);
                arriveRing.sprite = HazineShapes.Ring;
            }
            essence.enabled = false;
            for (int t = 0; t < trail.Length; t++)
            {
                trail[t].enabled = false;
            }
            arriveRing.enabled = false;
            if (!EssenceWillFly() || clock < launchAt)
            {
                return;
            }
            Color tint = report.IsReward
                ? (report.ScoreDelta < 0 ? Style.Loss : Style.Amber)
                : Style.Burnt;
            float flight = clock - launchAt;
            if (flight <= Style.EssenceFlight)
            {
                if (!Layers.ShowEssence)
                {
                    return;
                }
                float k = Ease(flight / Style.EssenceFlight);
                Vector2 p = Bezier(k);
                Vector2 ahead = Bezier(Mathf.Min(1f, k + 0.02f));
                Vector2 dir = ahead - p;
                essence.enabled = true;
                essence.transform.position = p;
                essence.transform.rotation = Quaternion.Euler(0f, 0f,
                    dir.sqrMagnitude > 0f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : 90f);
                float s = Style.EssenceSize * cell * Mathf.Lerp(1f, 0.75f, k);
                essence.transform.localScale = new Vector3(s * 1.4f, s, 1f);
                essence.color = tint;
                // The trail is always SMALLER than the token: a trail longer than what it
                // follows is an arrow, which is what "Harcama bonusu" shipped once.
                for (int t = 0; t < Style.TrailCount && t < trail.Length; t++)
                {
                    float back = Mathf.Max(0f, k - 0.05f * (t + 1));
                    trail[t].enabled = true;
                    trail[t].transform.position = Bezier(back);
                    float ts = s * (0.7f - 0.15f * t);
                    trail[t].transform.localScale = new Vector3(ts, ts, 1f);
                    Color tc = tint;
                    tc.a = 0.45f - 0.12f * t;
                    trail[t].color = tc;
                }
                return;
            }
            if (arrivedAt < 0f)
            {
                arrivedAt = launchAt + Style.EssenceFlight;
                Say(Sounded, SoundEssenceArrive);
                if (Layers.ShowTargetResponse && destination.OnArrive != null)
                {
                    destination.OnArrive();
                }
            }
            float r = (clock - arrivedAt) / Style.ArriveTime;
            if (Layers.ShowTargetResponse && r <= 1f)
            {
                arriveRing.enabled = true;
                arriveRing.transform.position = destination.Point;
                float rs = Style.ArriveRing * Mathf.Lerp(0.5f, 1.2f, Ease(r));
                arriveRing.transform.localScale = new Vector3(rs, rs, 1f);
                Color rc = tint;
                rc.a = 0.7f * (1f - r);
                arriveRing.color = rc;
            }
        }

        /// <summary>Lifted straight up off both ends, built only from the two anchors - never
        /// from the board, so the flight can cross it but never aim at it.</summary>
        private Vector2 Bezier(float k)
        {
            Vector2 a = verdictPos;
            Vector2 d = destination.Point;
            float lift = Style.EssenceLift;
            Vector2 b = a + new Vector2(0f, lift * 0.6f);
            Vector2 c = d + new Vector2(0f, lift * (d.y > a.y ? -0.2f : 0.5f));
            float u = 1f - k;
            return u * u * u * a + 3f * u * u * k * b + 3f * u * k * k * c + k * k * k * d;
        }

        private void PaintScore()
        {
            ScoreScale = 1f;
            ScoreClaim = 0f;
            if (!destination.IsScore || arrivedAt < 0f || report.Effect != HazineEffect.ExplosionBonus
                || report.ScoreDelta == 0 || !Layers.ShowTargetResponse)
            {
                return;
            }
            float k = (clock - arrivedAt) / Style.ScoreTime;
            if (k > 1f)
            {
                return;
            }
            bool loss = report.ScoreDelta < 0;
            // 1 -> 1.07 -> 0.99 -> 1 for a gain, 1 -> 0.96 -> 1.02 -> 1 for a loss.
            float amp = loss ? -Style.ScoreDip : Style.ScorePunch;
            float wave = k < 0.3f ? Ease(k / 0.3f)
                : k < 0.7f ? Mathf.Lerp(1f, -0.15f, Ease((k - 0.3f) / 0.4f))
                : Mathf.Lerp(-0.15f, 0f, Ease((k - 0.7f) / 0.3f));
            if (loss)
            {
                wave = k < 0.3f ? Ease(k / 0.3f)
                    : k < 0.7f ? Mathf.Lerp(1f, -0.5f, Ease((k - 0.3f) / 0.4f))
                    : Mathf.Lerp(-0.5f, 0f, Ease((k - 0.7f) / 0.3f));
            }
            ScoreScale = 1f + amp * wave;
            ScoreClaim = 1f - k;
            ScoreInk = loss ? Style.Loss : Style.Gold;
        }

        private void PaintCancel()
        {
            Vector2 mid = Midpoint();
            float flight = clock - cancelLaunchAt;
            // Two cores leave their cells for the point between them.
            for (int i = 0; i < report.Discoveries.Count && i < 2; i++)
            {
                SpriteRenderer core = trail[i + 2 < trail.Length ? i + 2 : i];
                if (flight < 0f || flight > Style.CancelFlight || !Layers.ShowEssence)
                {
                    continue;
                }
                Vector2 from = cellWorld(report.Discoveries[i].Cell);
                float k = flight / Style.CancelFlight;
                k = k * k; // they accelerate into each other
                core.enabled = true;
                core.transform.position = Vector2.Lerp(from, mid, k);
                float s = Style.EssenceSize * cell;
                core.transform.localScale = new Vector3(s, s, 1f);
                Color c = report.Discoveries[i].IsTreasure ? Style.Amber : Style.Blast;
                c.a = 0.9f;
                core.color = c;
            }
            if (flight >= Style.CancelFlight && cancelBurst == null)
            {
                Say(Sounded, SoundCancelCollide);
                cancelBurst = sheets.Start(CancelSheet, mid, Style.CancelSize * cell);
                cancelBurst.Tint = Style.CancelTint;
                cancelBurst.FadeOut = 0.12f;
            }
            if (cancelBurst != null)
            {
                cancelBurst.Position = mid;
                if (arriveRing != null)
                {
                    float r = (clock - (cancelLaunchAt + Style.CancelFlight)) / 0.35f;
                    if (r <= 1f)
                    {
                        arriveRing.enabled = true;
                        arriveRing.transform.position = mid;
                        float rs = Mathf.Lerp(0.3f, 0.95f, Ease(r)) * cell;
                        arriveRing.transform.localScale = new Vector3(rs, rs, 1f);
                        Color rc = Style.Neutral;
                        rc.a = 0.55f * (1f - r);
                        arriveRing.color = rc;
                    }
                }
            }
        }

        private void PaintDebug()
        {
            bool frames = Layers.ShowFrameDebug;
            if (frames)
            {
                if (debug == null)
                {
                    debug = ViewUtil.MakeText3D(transform, "HazineDebug", Vector2.zero, string.Empty,
                        40, 0.03f, Color.white, DebugOrder, TextAnchor.UpperCenter);
                }
                debug.gameObject.SetActive(true);
                debug.text = DebugState + "\n" + report.Result + " / " + report.Effect
                    + (report.ScoreDelta != 0 ? " / score " + report.ScoreDelta : "");
                debug.characterSize = 0.16f * cell * 10f / 40f;
                debug.transform.position = cellWorld(report.Discoveries[0].Cell) - new Vector2(0f, 0.6f * cell);
            }
            else if (debug != null)
            {
                debug.gameObject.SetActive(false);
            }
            // The cells the REPORT names, outlined - and nothing else, which is the hidden-info test.
            while (debugFrames.Count < report.Discoveries.Count)
            {
                SpriteRenderer f = Rent(DebugOrder);
                f.sprite = HazineShapes.Frame;
                debugFrames.Add(f);
            }
            for (int i = 0; i < debugFrames.Count; i++)
            {
                bool on = Layers.ShowCellDebug && i < report.Discoveries.Count;
                debugFrames[i].enabled = on;
                if (on)
                {
                    debugFrames[i].transform.position = cellWorld(report.Discoveries[i].Cell);
                    debugFrames[i].transform.localScale = new Vector3(cell, cell, 1f);
                    debugFrames[i].color = report.Discoveries[i].IsTreasure
                        ? new Color(1f, 0.85f, 0.2f, 0.9f)
                        : new Color(1f, 0.35f, 0.2f, 0.9f);
                }
            }
        }

        // ---- housekeeping ----------------------------------------------------------------------

        /// <summary>How many renderers the find is using right now - the lab prints it beside
        /// the report, which is the hidden-information test in one line.</summary>
        public int DrawnCellCount
        {
            get
            {
                if (!active || report == null)
                {
                    return 0;
                }
                return report.Discoveries.Count;
            }
        }

        public void Stop()
        {
            if (Impulse != null && impulseAt >= 0f)
            {
                Impulse(Vector2.zero);
            }
            impulseAt = -1f;
            active = false;
            sheets.Clear();
            bursts.Clear();
            cancelBurst = null;
            foreach (SpriteRenderer r in lights) { Return(r); }
            lights.Clear();
            foreach (SpriteRenderer r in cues) { Return(r); }
            cues.Clear();
            foreach (Bit b in bits) { Return(b.R); }
            bits.Clear();
            foreach (SpriteRenderer r in debugFrames) { Return(r); }
            debugFrames.Clear();
            Return(scorch);
            scorch = null;
            HideVerdict();
            if (essence != null)
            {
                essence.enabled = false;
                arriveRing.enabled = false;
                for (int t = 0; t < trail.Length; t++)
                {
                    trail[t].enabled = false;
                }
            }
            if (debug != null)
            {
                debug.gameObject.SetActive(false);
            }
            ScoreScale = 1f;
            ScoreClaim = 0f;
            DebugState = string.Empty;
        }

        /// <summary>The lab replays the same report object: forget it so it plays again.</summary>
        public void Forget()
        {
            lastPlayed = null;
        }

        private void OnDisable()
        {
            Stop();
        }

        private SpriteRenderer Rent(int order)
        {
            SpriteRenderer r;
            if (pool.Count > 0)
            {
                r = pool.Pop();
                r.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("HazineBit");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
            }
            r.sortingOrder = order;
            r.enabled = false;
            r.transform.rotation = Quaternion.identity;
            r.color = Color.white;
            return r;
        }

        private void Return(SpriteRenderer r)
        {
            if (r == null)
            {
                return;
            }
            r.enabled = false;
            r.gameObject.SetActive(false);
            pool.Push(r);
        }

        private void Say(System.Action<string> channel, string what)
        {
            if (said.Add(what) && channel != null)
            {
                channel(what);
            }
        }

        private Vector2 ClampToView(Vector2 p)
        {
            if (clampRect.width <= 0f)
            {
                return p;
            }
            return new Vector2(Mathf.Clamp(p.x, clampRect.xMin, clampRect.xMax),
                Mathf.Clamp(p.y, clampRect.yMin, clampRect.yMax));
        }

        private static float Ease(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        /// <summary>Deterministic per report; never UnityEngine.Random.</summary>
        private struct Lcg
        {
            private uint state;

            public Lcg(uint seed)
            {
                state = seed == 0 ? 0x9E3779B9u : seed;
            }

            public float Next()
            {
                state = state * 1664525u + 1013904223u;
                return (state >> 8) / 16777216f;
            }

            public float Range(float a, float b)
            {
                return Mathf.Lerp(a, b, Next());
            }

            public int Int(int a, int bExclusive)
            {
                return Mathf.Min(bExclusive - 1, a + (int)(Next() * (bExclusive - a)));
            }
        }
    }
}
