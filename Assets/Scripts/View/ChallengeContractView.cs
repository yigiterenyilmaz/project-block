// PURPOSE: "Meydan Okuma"'s dare drawn as a CONTRACT held on the board - two rails of plates along
// the dared line, a seal clamp at each end, a faint current running through it, and the wager as
// a token beside it - and everything that happens to that contract: laid, pressed by the clock,
// paid, missed and moved, or run out.
//
// THE MOTION LANGUAGE IS THE POINT, because the player must read the result before the number.
//   SUCCESS goes OUTWARD and OPENS: the rails brighten as the line goes, the clamps recoil away
//   from the line, the rails break into gold plates thrown off the line, the token punches, folds
//   to a seed and the seed flies to the score.
//   A MISS goes INWARD and CUTS: the rails tense, the clamps let go, each clamp REELS IN its own
//   rail (the top one grew from one end, the bottom one from the other, and each goes back where
//   it came from), a cut runs down the token, the halves part, one half goes out, the other slides
//   back and the token reforms smaller and duller around the NEW value; then it travels to the new
//   line and the contract is laid again there. Retargeting is never an instant swap.
//   RUNNING OUT is the miss with nowhere to go: the remaining half collapses into bronze dust.
//
// THE VIEW DECIDES NOTHING. Which line, how long, how much, which attempt and whether it was made
// are ChallengeVisuals (per event) and the joker's live state (per repaint); the urgency looks
// come from ChallengeVisuals.UrgencyOf. There is no "/ 2" anywhere here: the new value on the
// token is the report's, and a miss with no honest line yet PARKS the token on its NextBonus until
// the joker lays one. Where a line runs, where its ends are and where the token fits are asked of
// the controller (Locate) every frame, so the contract follows the arena's knock and squeeze.
//
// IT NEVER TOUCHES THE BLOCKS. No tint, no fill, no plate over the line: the rails sit in the
// seams, the only light inside the line is the plates' own faint inward glow and a few motes at
// a tenth of an opacity, and the clamps and the token stand outside the grid.
//
// Keyed on the event's IDENTITY; on the scaled clock; pooled; every beat announced through
// Sounded / Haptic.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class ChallengeContractView : MonoBehaviour
    {
        public static class Style
        {
            // ---- palette (the rail is grey, tinted by these)
            public static Color Calm = new Color(0.86f, 0.68f, 0.34f);
            public static Color Tension = new Color(0.93f, 0.63f, 0.27f);
            public static Color Final = new Color(0.98f, 0.56f, 0.22f);
            public static Color ClampHeat = new Color(1f, 0.82f, 0.64f);
            public static Color IvoryGold = new Color(1f, 0.94f, 0.72f);
            public static Color Current = new Color(1f, 0.80f, 0.45f);
            /// <summary>The token's rim/face tint per attempt: rich, a little muted, antique.
            /// Worn, never grey.</summary>
            public static readonly Color[] AttemptTint =
            {
                Color.white, new Color(0.90f, 0.86f, 0.80f), new Color(0.78f, 0.70f, 0.60f)
            };
            public static readonly float[] AttemptScale = { 1f, 0.93f, 0.86f };
            public static Color Ink = new Color(1f, 0.93f, 0.80f);
            public static Color InkShade = new Color(0.08f, 0.04f, 0.02f);

            // ---- geometry, in cells (pixels where the brief speaks in pixels)
            /// <summary>Where the clamp's box centre sits past the line's end edge.</summary>
            public static float ClampOut = 0.24f;
            /// <summary>The token box in cells; the drawn medallion is 0.8 of it, so 0.72 cell wide.</summary>
            public static float TokenWidth = 0.90f;
            public static float TextHeight = 0.22f;
            public static readonly float[] InsetPx = { 0f, 1.5f, 3f };
            public static readonly float[] ClampInPx = { 0f, 1f, 2f };

            // ---- the build
            public static float NodeTime = 0.06f;
            public static float GrowFrom = 0.04f;
            public static float GrowTime = 0.20f;
            public static float LockPunch = 1.06f;
            public static float LockTime = 0.09f;

            // ---- the current
            public static readonly int[] MoteCount = { 2, 2, 3 };
            public static readonly float[] MoteSpeed = { 0.32f, 0.52f, 0.80f };
            public static readonly float[] MoteAlpha = { 0.08f, 0.10f, 0.12f };

            // ---- idle
            public static float HeartbeatEvery = 1.0f;
            public static float HeartbeatTime = 0.18f;
            public static float HeartbeatScale = 1.025f;
            public static float GlintEveryMin = 3.2f;
            public static float GlintEveryMax = 4.6f;
            public static float GlintSweep = 0.35f;

            // ---- success
            public static float ChargeTime = 0.09f;
            public static float ChargeBoost = 0.25f;
            public static float RecoilPx = 3f;
            public static int Shards = 6;
            public static float TokenPunch = 1.15f;
            public static float ReadHold = 0.55f;
            public static float Compress = 0.15f;
            public static float Flight = 0.45f;
            public static float ScorePunch = 0.08f;
            public static float ScoreTime = 0.36f;

            // ---- miss
            public static float TenseTime = 0.06f;
            public static float ReleasePx = 2.5f;
            public static float RetractFrom = 0.07f;
            public static float RetractTime = 0.15f;
            public static float CutFrom = 0.15f;
            public static float CutTime = 0.09f;
            public static float SplitPx = 3f;
            public static float ReformAt = 0.42f;
            public static float TravelFrom = 0.50f;
            public static float TravelTime = 0.28f;
            public static float RebuildDelay = 0.04f;
            public static int Dust = 5;
        }

        public static class Layers
        {
            public static bool ShowRails = true;
            public static bool ShowClamps = true;
            public static bool ShowCurrent = true;
            public static bool ShowToken = true;
            public static bool ShowParticles = true;
            public static bool ShowEssence = true;

            // debug (never in a release build)
            public static bool ShowChallengeTarget;
            public static bool ShowRailBounds;
            public static bool ShowClampAnchors;
            public static bool ShowBonusTokenAnchor;
            public static bool ShowRemainingTurns;
            public static bool ShowAttemptIndex;
            public static bool ShowEnergyCurrent;
            public static bool ShowSuccessLink;
            public static bool ShowRetargetPath;

            public static void AllOn()
            {
                ShowRails = ShowClamps = ShowCurrent = ShowToken = ShowParticles = ShowEssence = true;
                ShowChallengeTarget = ShowRailBounds = ShowClampAnchors = ShowBonusTokenAnchor = false;
                ShowRemainingTurns = ShowAttemptIndex = ShowEnergyCurrent = false;
                ShowSuccessLink = ShowRetargetPath = false;
            }

            public static bool AnyDebug
            {
                get
                {
                    return ShowChallengeTarget || ShowRailBounds || ShowClampAnchors
                        || ShowBonusTokenAnchor || ShowRemainingTurns || ShowAttemptIndex
                        || ShowEnergyCurrent || ShowSuccessLink || ShowRetargetPath;
                }
            }
        }

        public static System.Action<string> Sounded;
        public static System.Action<string> Haptic;

        public const string SoundLock = "meydan.lock";
        public const string SoundTick = "meydan.tick";
        public const string SoundSuccess = "meydan.success";
        public const string SoundRewardLaunch = "meydan.reward.launch";
        public const string SoundRewardArrive = "meydan.reward.arrive";
        public const string SoundFail = "meydan.fail";
        public const string SoundBonusCut = "meydan.bonus.cut";
        public const string SoundRelocate = "meydan.relocate";
        public const string SoundExpire = "meydan.expire";
        public const string HapticSuccess = "meydan.haptic.success";
        public const string HapticFail = "meydan.haptic.fail";

        /// <summary>Where a line is on screen - the controller's answer, asked every frame.
        /// </summary>
        public struct Line
        {
            public bool Valid;
            /// <summary>Centres of the line's first and last play cells, world.</summary>
            public Vector2 A;
            public Vector2 B;
            public Vector2 Along;
            public Vector2 Across;
            public float Cell;
            /// <summary>World units per screen pixel.</summary>
            public float Pixel;
            /// <summary>Where the wager token sits for this line.</summary>
            public Vector2 Token;
            public int Cells;
        }

        /// <summary>The contract as it stands - the joker's live state, or a report's.</summary>
        public struct Contract
        {
            public bool Has;
            public bool IsRow;
            public int Line;
            public int Bonus;
            public int Attempt;
            public int TurnsLeft;
            public int InitialTurns;

            public ChallengeUrgency Urgency
            {
                get { return ChallengeVisuals.UrgencyOf(TurnsLeft, InitialTurns); }
            }

            public bool SameLine(Contract other)
            {
                return Has && other.Has && IsRow == other.IsRow && Line == other.Line;
            }
        }

        public System.Func<bool, int, Line> Locate;
        public System.Func<Vector2> ScoreAnchor;

        // ---- the contract being drawn
        private Contract live;
        private float liveBuiltAt = float.NegativeInfinity;
        private Contract releasing;
        private bool releaseIsSuccess;
        private float releaseAt = float.NegativeInfinity;

        // ---- the token
        private bool tokenShown;
        private int tokenValue;
        private int tokenAttempt = 1;
        private bool parked;
        private Vector2 parkedAt;
        private Vector2 lastTokenAt;

        // ---- the event being played
        private ChallengeVisuals playing;
        private ChallengeVisuals lastPlayed;
        private float eventAt;
        private float clock;
        private float nextGlint;
        private float arrivedAt = -1f;
        private Vector2 launchFrom;
        private readonly HashSet<string> said = new HashSet<string>();
        private float shownInset;
        private float shownClampIn;
        private float shownHeat;
        private float tickAt = float.NegativeInfinity;
        private float lockedFor = float.NaN;
        private float lastUnit = 0.9f;

        // ---- renderers
        private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        private readonly List<SpriteRenderer> railsLive = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> railsOld = new List<SpriteRenderer>();
        private readonly SpriteRenderer[] clampsLive = new SpriteRenderer[2];
        private readonly SpriteRenderer[] clampsOld = new SpriteRenderer[2];
        private readonly SpriteRenderer[] motes = new SpriteRenderer[3];
        private SpriteRenderer tokenLeft;
        private SpriteRenderer tokenRight;
        private SpriteRenderer tokenGlint;
        private SpriteRenderer cutLine;
        private SpriteRenderer essence;
        private readonly SpriteRenderer[] trail = new SpriteRenderer[3];
        private TextMesh text;
        private TextMesh textShade;
        private TextMesh debugText;
        private readonly List<Bit> bits = new List<Bit>();
        private readonly List<SpriteRenderer> debugMarks = new List<SpriteRenderer>();

        private const int RailOrder = 22;
        private const int CurrentOrder = 21;
        private const int ClampOrder = 24;
        private const int TokenOrder = 60;
        private const int TextOrder = 62;
        private const int ParticleOrder = 26;
        private const int EssenceOrder = 93;
        private const int DebugOrder = 96;

        private sealed class Bit
        {
            public SpriteRenderer R;
            public float Born;
            public float Life;
            public Vector2 From;
            public Vector2 Velocity;
            public float Gravity;
            public float Spin;
            public float Size;
            public Color Tint;
            public int Kind; // 0 shard, 1 glint, 2 dust
        }

        public float ScoreScale { get; private set; }
        public float ScoreClaim { get; private set; }
        public Color ScoreInk { get { return ChallengeShapes.Gold; } }

        public bool Busy
        {
            get { return playing != null; }
        }

        public bool HasPlayed(ChallengeVisuals report)
        {
            return report == null || ReferenceEquals(report, lastPlayed);
        }

        private void Awake()
        {
            ScoreScale = 1f;
            nextGlint = 2f;
            for (int i = 0; i < 2; i++)
            {
                clampsLive[i] = Make(ClampOrder, ChallengeShapes.Clamp);
                clampsOld[i] = Make(ClampOrder, ChallengeShapes.Clamp);
            }
            for (int i = 0; i < motes.Length; i++)
            {
                motes[i] = Make(CurrentOrder, HazineShapes.Mote);
            }
            tokenLeft = Make(TokenOrder, ChallengeShapes.TokenHalf(true));
            tokenRight = Make(TokenOrder, ChallengeShapes.TokenHalf(false));
            tokenGlint = Make(TokenOrder + 1, HazineShapes.Glint);
            cutLine = Make(TokenOrder + 1, ViewUtil.WhiteSprite);
            essence = Make(EssenceOrder, ChallengeShapes.Seed);
            for (int i = 0; i < trail.Length; i++)
            {
                trail[i] = Make(EssenceOrder - 1, HazineShapes.Mote);
            }
            textShade = ViewUtil.MakeText3D(transform, "ChallengeValueShade", Vector2.zero, string.Empty,
                64, 0.03f, Style.InkShade, TextOrder - 1, TextAnchor.MiddleCenter);
            text = ViewUtil.MakeText3D(transform, "ChallengeValue", Vector2.zero, string.Empty,
                64, 0.03f, Style.Ink, TextOrder, TextAnchor.MiddleCenter);
            text.gameObject.SetActive(false);
            textShade.gameObject.SetActive(false);
        }

        // ================================================================ inputs

        /// <summary>
        /// The joker's LIVE state, every repaint. Only honoured when no event is waiting to be
        /// played (the controller checks) and none is playing - otherwise the new state would be
        /// on screen before the turn that caused it had been shown.
        /// </summary>
        public void SetSteady(Contract state, int pendingBonus)
        {
            if (playing != null)
            {
                return;
            }
            if (state.Has)
            {
                if (!live.SameLine(state))
                {
                    // A contract we have not seen being laid (a load, a new round): lay it now.
                    live = state;
                    liveBuiltAt = clock;
                    ShowToken(state.Bonus, state.Attempt);
                    parked = false;
                }
                live = state;
                return;
            }
            live = default(Contract);
            if (pendingBonus > 0)
            {
                // Missed, and no honest line yet: the token waits where it was.
                if (!parked)
                {
                    parked = true;
                    parkedAt = lastTokenAt;
                }
                ShowToken(pendingBonus, tokenAttempt);
                return;
            }
            parked = false;
            tokenShown = false;
        }

        /// <summary>For the lab: a contract already standing, no build.</summary>
        public void SetStanding(Contract state)
        {
            Stop();
            live = state;
            liveBuiltAt = float.NegativeInfinity;
            ShowToken(state.Bonus, state.Attempt);
            Line line = LineOf(live);
            lastTokenAt = line.Token;
        }

        /// <summary>For the lab: park a token at a point, as after a miss with no new line.</summary>
        public void SetParked(Vector2 at, int bonus, int attempt)
        {
            Stop();
            parked = true;
            parkedAt = at;
            lastTokenAt = at;
            ShowToken(bonus, attempt);
        }

        public void Play(ChallengeVisuals ev)
        {
            if (ev == null || ReferenceEquals(ev, lastPlayed))
            {
                return;
            }
            FinishEvent();
            lastPlayed = ev;
            playing = ev;
            eventAt = clock;
            arrivedAt = -1f;
            said.Clear();
            switch (ev.Event)
            {
                case ChallengeEvent.Started:
                    live = Target(ev);
                    if (parked)
                    {
                        launchFrom = parkedAt;
                        liveBuiltAt = clock + Style.TravelTime + Style.RebuildDelay;
                    }
                    else
                    {
                        liveBuiltAt = clock;
                        ShowToken(ev.Bonus, ev.Attempt);
                    }
                    tokenValue = ev.Bonus;
                    break;
                case ChallengeEvent.Ticked:
                    live = Target(ev);
                    tickAt = clock;
                    Say(Sounded, SoundTick);
                    break;
                case ChallengeEvent.Succeeded:
                    releasing = Old(ev);
                    releaseIsSuccess = true;
                    releaseAt = clock;
                    live = default(Contract);
                    tokenValue = ev.OldBonus;
                    tokenAttempt = ev.OldAttempt;
                    tokenShown = true;
                    Say(Sounded, SoundSuccess);
                    Say(Haptic, HapticSuccess);
                    break;
                default: // Failed, Expired
                    releasing = Old(ev);
                    releaseIsSuccess = false;
                    releaseAt = clock;
                    live = default(Contract);
                    tokenValue = ev.OldBonus;
                    tokenAttempt = ev.OldAttempt;
                    tokenShown = true;
                    launchFrom = LineOf(releasing).Token;
                    if (ev.Event == ChallengeEvent.Failed && ev.HasTarget)
                    {
                        live = Target(ev);
                        liveBuiltAt = clock + Style.TravelFrom + Style.TravelTime + Style.RebuildDelay;
                    }
                    Say(Sounded, ev.Event == ChallengeEvent.Expired ? SoundExpire : SoundFail);
                    Say(Haptic, HapticFail);
                    break;
            }
            parked = false;
            Tick(0f);
        }

        public void Stop()
        {
            FinishEvent();
            live = default(Contract);
            releasing = default(Contract);
            releaseAt = float.NegativeInfinity;
            tokenShown = false;
            parked = false;
            foreach (Bit b in bits) { Return(b.R); }
            bits.Clear();
            Paint();
        }

        /// <summary>After a reset: treat this report as already shown.</summary>
        public void MarkPlayed(ChallengeVisuals report)
        {
            if (report != null)
            {
                lastPlayed = report;
            }
        }

        /// <summary>The lab replays reports; forget the last so the same object plays again.
        /// </summary>
        public void Forget()
        {
            lastPlayed = null;
        }

        private void FinishEvent()
        {
            if (playing == null)
            {
                return;
            }
            ChallengeVisuals ev = playing;
            playing = null;
            releasing = default(Contract);
            releaseAt = float.NegativeInfinity;
            ScoreScale = 1f;
            ScoreClaim = 0f;
            switch (ev.Event)
            {
                case ChallengeEvent.Succeeded:
                case ChallengeEvent.Expired:
                    tokenShown = false;
                    break;
                case ChallengeEvent.Failed:
                    tokenValue = ev.HasTarget ? ev.Bonus : ev.NextBonus;
                    tokenAttempt = ev.HasTarget ? ev.Attempt : ev.OldAttempt + 1;
                    if (!ev.HasTarget)
                    {
                        parked = true;
                        parkedAt = launchFrom;
                    }
                    break;
                case ChallengeEvent.Started:
                    tokenValue = ev.Bonus;
                    tokenAttempt = ev.Attempt;
                    tokenShown = true;
                    break;
            }
        }

        private static Contract Target(ChallengeVisuals ev)
        {
            return new Contract
            {
                Has = ev.HasTarget, IsRow = ev.IsRow, Line = ev.Line, Bonus = ev.Bonus,
                Attempt = ev.Attempt, TurnsLeft = ev.TurnsLeft, InitialTurns = ev.InitialTurns
            };
        }

        private static Contract Old(ChallengeVisuals ev)
        {
            return new Contract
            {
                Has = true, IsRow = ev.OldIsRow, Line = ev.OldLine, Bonus = ev.OldBonus,
                Attempt = ev.OldAttempt, TurnsLeft = 0, InitialTurns = 1
            };
        }

        private void ShowToken(int value, int attempt)
        {
            tokenShown = true;
            tokenValue = value;
            tokenAttempt = Mathf.Max(1, attempt);
        }

        private Line LineOf(Contract c)
        {
            if (!c.Has || Locate == null)
            {
                return new Line();
            }
            return Locate(c.IsRow, c.Line);
        }

        // ================================================================ clock

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void Tick(float dt)
        {
            clock += dt;
            if (playing != null && clock - eventAt > EventLength(playing))
            {
                FinishEvent();
            }
            Paint();
        }

        private float EventLength(ChallengeVisuals ev)
        {
            switch (ev.Event)
            {
                case ChallengeEvent.Started:
                    return (liveBuiltAt - eventAt) + Style.GrowFrom + Style.GrowTime + Style.LockTime + 0.05f;
                case ChallengeEvent.Ticked:
                    return 0.25f;
                case ChallengeEvent.Succeeded:
                    if (arrivedAt >= 0f)
                    {
                        return arrivedAt - eventAt + Style.ScoreTime + 0.05f;
                    }
                    return Layers.ShowEssence && ScoreAnchor != null ? float.MaxValue
                        : Style.ReadHold + Style.Compress + 0.1f;
                case ChallengeEvent.Expired:
                    return 0.9f;
                default:
                    return ev.HasTarget
                        ? (liveBuiltAt - eventAt) + Style.GrowFrom + Style.GrowTime + Style.LockTime + 0.05f
                        : Style.ReformAt + 0.15f;
            }
        }

        // ================================================================ painting

        private void Paint()
        {
            int urgency = live.Has ? (int)live.Urgency : 0;
            float target = Style.InsetPx[urgency];
            float k = Mathf.Clamp01(Time.deltaTime * 8f);
            shownInset = Mathf.Lerp(shownInset, target, k);
            shownClampIn = Mathf.Lerp(shownClampIn, Style.ClampInPx[urgency], k);
            shownHeat = Mathf.Lerp(shownHeat, urgency * 0.5f, k);

            // The live contract.
            Line line = LineOf(live);
            if (line.Valid)
            {
                lastUnit = line.Cell;
            }
            float build = clock - liveBuiltAt;
            if (live.Has && line.Valid && build >= 0f)
            {
                float grow = Ease01((build - Style.GrowFrom) / Style.GrowTime);
                float node = Mathf.Clamp01(build / Style.NodeTime);
                float scale = node < 1f ? Mathf.Lerp(0f, 0.9f, node) : LockScale(build);
                if (urgency == (int)ChallengeUrgency.Final && build > 0.5f)
                {
                    scale *= Heartbeat();
                }
                float tick = clock - tickAt;
                if (tick >= 0f && tick < 0.12f)
                {
                    scale *= 1f + 0.03f * Mathf.Sin(tick / 0.12f * Mathf.PI);
                }
                if (build >= Style.GrowFrom + Style.GrowTime && lockedFor != liveBuiltAt)
                {
                    // Once per contract laid - a standing one (load, lab) locks silently.
                    lockedFor = liveBuiltAt;
                    if (!float.IsNegativeInfinity(liveBuiltAt))
                    {
                        if (Sounded != null) { Sounded(SoundLock); }
                        Glint(line.A - line.Along * (0.5f + Style.ClampOut) * line.Cell, line.Cell);
                        Glint(line.B + line.Along * (0.5f + Style.ClampOut) * line.Cell, line.Cell);
                    }
                }
                Color tint = UrgencyColour(urgency);
                PaintRails(railsLive, line, grow, shownInset, tint, 1f, 1f);
                PaintClamps(clampsLive, line, scale, -shownClampIn * line.Pixel, 1f, shownHeat);
                float currentIn = Mathf.Clamp01((build - 0.3f) / 0.25f);
                PaintCurrent(line, urgency, currentIn);
            }
            else
            {
                HideAll(railsLive);
                clampsLive[0].enabled = clampsLive[1].enabled = false;
                PaintCurrent(line, 0, 0f);
            }

            // The contract being let go.
            PaintRelease();
            PaintToken(line);
            PaintEssence();
            PaintBits();
            PaintDebug(line);
        }

        private void PaintRelease()
        {
            float t = clock - releaseAt;
            Line old = LineOf(releasing);
            if (!releasing.Has || !old.Valid || t < 0f)
            {
                HideAll(railsOld);
                clampsOld[0].enabled = clampsOld[1].enabled = false;
                return;
            }
            Color tint = UrgencyColour((int)ChallengeUrgency.Final);
            if (releaseIsSuccess)
            {
                // OUTWARD: charge, recoil, break.
                float charge = 1f + Style.ChargeBoost * Mathf.Clamp01(t / Style.ChargeTime);
                float railAlpha = 1f - Mathf.Clamp01((t - Style.ChargeTime) / 0.06f);
                PaintRails(railsOld, old, 1f, shownInset, tint, charge, railAlpha);
                float recoil = Style.RecoilPx * Ease01((t - Style.ChargeTime) / 0.06f);
                float clampAlpha = 1f - Mathf.Clamp01((t - 0.15f) / 0.15f);
                PaintClamps(clampsOld, old, 1f, recoil * old.Pixel, clampAlpha, 1f);
                if (t >= Style.ChargeTime && Say(null, "shards"))
                {
                    Shatter(old);
                }
            }
            else
            {
                // INWARD: tense, let go, reel the rails back in.
                float tense = Mathf.Clamp01(t / Style.TenseTime);
                float extra = t < Style.RetractFrom ? tense : 1f - Mathf.Clamp01((t - Style.RetractFrom) / 0.1f);
                float grow = 1f - Ease01((t - Style.RetractFrom) / Style.RetractTime);
                PaintRails(railsOld, old, grow, shownInset + extra, tint, 1f + 0.2f * extra, 1f);
                float release = Style.ReleasePx * Ease01((t - Style.TenseTime) / 0.04f);
                float clampAlpha = 1f - Mathf.Clamp01((t - 0.20f) / 0.10f);
                PaintClamps(clampsOld, old, 1f, release * old.Pixel, clampAlpha, 1f);
            }
        }

        /// <summary>
        /// One rail per side of the line, one plate per cell. The rail on +Across grows from the
        /// A end and the one on -Across from the B end, so a retract (grow running back to 0)
        /// has each clamp reeling in its OWN rail.
        /// </summary>
        private void PaintRails(List<SpriteRenderer> set, Line line, float grow, float insetPx,
            Color tint, float bright, float alpha)
        {
            int n = line.Valid ? line.Cells : 0;
            while (set.Count < n * 2)
            {
                set.Add(Rent(RailOrder));
            }
            float baseAngle = Mathf.Atan2(line.Along.y, line.Along.x) * Mathf.Rad2Deg;
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? 1f : -1f;
                float offset = line.Cell * 0.5f - insetPx * line.Pixel;
                float covered = grow * n;
                for (int i = 0; i < set.Count / 2; i++)
                {
                    SpriteRenderer r = set[side * (set.Count / 2) + i];
                    if (i >= n || !Layers.ShowRails || alpha <= 0f)
                    {
                        r.enabled = false;
                        continue;
                    }
                    float vis = Mathf.Clamp01(covered - i);
                    if (vis <= 0f)
                    {
                        r.enabled = false;
                        continue;
                    }
                    // Cell index counted from the rail's own origin end.
                    int k = side == 0 ? i : n - 1 - i;
                    Vector2 centre = line.A + line.Along * (k * line.Cell);
                    float dir = side == 0 ? 1f : -1f; // growth direction along the line
                    Vector2 start = centre - line.Along * (dir * line.Cell * 0.5f);
                    Vector2 mid = start + line.Along * (dir * line.Cell * vis * 0.5f);
                    r.enabled = true;
                    r.sprite = ChallengeShapes.Rail(((k * 5 + line.Cells) % 7) == 3 ? 1 : 0);
                    r.transform.position = mid + line.Across * (sign * offset);
                    // The plate's inside light is on its +v: turn it to face into the line.
                    r.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle + (sign > 0f ? 180f : 0f));
                    r.transform.localScale = new Vector3(line.Cell * vis, line.Cell, 1f);
                    Color c = tint * bright;
                    c.a = alpha;
                    r.color = c;
                }
            }
        }

        private void PaintClamps(SpriteRenderer[] pair, Line line, float scale, float outward, float alpha,
            float heat)
        {
            float baseAngle = Mathf.Atan2(line.Along.y, line.Along.x) * Mathf.Rad2Deg;
            for (int i = 0; i < 2; i++)
            {
                SpriteRenderer r = pair[i];
                if (!Layers.ShowClamps || !line.Valid || alpha <= 0f || scale <= 0f)
                {
                    r.enabled = false;
                    continue;
                }
                float sign = i == 0 ? -1f : 1f;
                Vector2 end = i == 0 ? line.A : line.B;
                // The box centre sits ClampOut past the line's edge, which puts the spine
                // (ChallengeShapes.SpineU off the centre) right on the edge and the hooks over the
                // rail ends.
                float out_ = (0.5f + Style.ClampOut) * line.Cell + outward;
                r.enabled = true;
                r.transform.position = end + line.Along * (sign * out_);
                r.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle + (i == 0 ? 180f : 0f));
                float s = line.Cell * scale;
                r.transform.localScale = new Vector3(s, s, 1f);
                Color c = Color.Lerp(Color.white, Style.ClampHeat, Mathf.Clamp01(heat));
                c.a = alpha;
                r.color = c;
            }
        }

        private void PaintCurrent(Line line, int urgency, float strength)
        {
            int count = Style.MoteCount[urgency];
            for (int i = 0; i < motes.Length; i++)
            {
                SpriteRenderer m = motes[i];
                if (!Layers.ShowCurrent || !line.Valid || i >= count || strength <= 0f)
                {
                    m.enabled = false;
                    continue;
                }
                float length = Mathf.Max(line.Cells, 1) * line.Cell;
                float speed = Style.MoteSpeed[urgency];
                float u = Mathf.Repeat(clock * speed + i / (float)count + 0.13f * i, 1f);
                // Enter and leave at the ends - never pop.
                float fade = Mathf.Sin(u * Mathf.PI);
                Vector2 from = line.A - line.Along * (line.Cell * 0.5f);
                Vector2 p = from + line.Along * (u * length)
                    + line.Across * ((i % 2 == 0 ? 0.12f : -0.10f) * line.Cell);
                m.enabled = true;
                m.transform.position = p;
                m.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(line.Along.y, line.Along.x) * Mathf.Rad2Deg);
                m.transform.localScale = new Vector3(line.Cell * 0.45f, line.Cell * 0.14f, 1f);
                Color c = Style.Current;
                c.a = Style.MoteAlpha[urgency] * fade * strength;
                m.color = c;
            }
        }

        // ---- the token -------------------------------------------------------------------------

        private void PaintToken(Line liveLine)
        {
            float t = clock - eventAt;
            ChallengeVisuals ev = playing;
            bool show = tokenShown && Layers.ShowToken;
            Vector2 at;
            Line cell = liveLine.Valid ? liveLine : LineOf(releasing);
            float unit = cell.Valid ? cell.Cell : 0.9f;
            float px = cell.Valid ? cell.Pixel : 0.01f;
            float scale = Style.AttemptScale[Mathf.Clamp(tokenAttempt - 1, 0, 2)];
            Color tint = Style.AttemptTint[Mathf.Clamp(tokenAttempt - 1, 0, 2)];
            float alpha = 1f;
            float split = 0f;
            float leftAlpha = 1f;
            float rightAlpha = 1f;
            float rightScale = 1f;
            float squeeze = 1f;
            float cut = 0f;
            float textAlpha = 1f;
            float textScale = 1f;
            int shownValue = tokenValue;

            if (ev != null && (ev.Event == ChallengeEvent.Failed || ev.Event == ChallengeEvent.Expired))
            {
                at = launchFrom;
                bool expired = ev.Event == ChallengeEvent.Expired;
                // The cut runs down the face, the halves part, one half goes out.
                cut = t < Style.CutFrom ? 0f : t < Style.CutFrom + Style.CutTime
                    ? (t - Style.CutFrom) / Style.CutTime
                    : 1f - Mathf.Clamp01((t - Style.CutFrom - Style.CutTime) / 0.06f) + 0.0001f;
                if (t >= Style.CutFrom) { Say(Sounded, SoundBonusCut); }
                float part = Ease01((t - 0.24f) / 0.06f);
                float gone = Ease01((t - 0.30f) / 0.12f);
                float back = Ease01((t - 0.32f) / 0.10f);
                split = part * Style.SplitPx * px;
                rightAlpha = 1f - gone;
                rightScale = Mathf.Lerp(1f, 0.65f, gone);
                float leftSplit = split * (1f - back);
                if (expired)
                {
                    float collapse = Ease01((t - 0.36f) / 0.16f);
                    squeeze = 1f - collapse;
                    leftAlpha = 1f - collapse;
                    if (collapse >= 1f && Say(null, "dust"))
                    {
                        Dust(at, unit);
                    }
                }
                else if (t >= Style.ReformAt)
                {
                    // Reformed around the NEW value, smaller and duller.
                    int nextAttempt = ev.HasTarget ? ev.Attempt : ev.OldAttempt + 1;
                    float reform = Ease01((t - Style.ReformAt) / 0.06f);
                    rightAlpha = reform;
                    rightScale = 1f;
                    split *= 1f - reform; // the returning half closes the seam
                    float nextScale = Style.AttemptScale[Mathf.Clamp(nextAttempt - 1, 0, 2)];
                    scale = Mathf.Lerp(scale, nextScale, reform);
                    tint = Color.Lerp(tint, Style.AttemptTint[Mathf.Clamp(nextAttempt - 1, 0, 2)], reform);
                    shownValue = ev.HasTarget ? ev.Bonus : ev.NextBonus;
                    float nt = t - Style.ReformAt;
                    textScale = nt < 0.06f ? Mathf.Lerp(0.8f, 1.05f, nt / 0.06f)
                        : Mathf.Lerp(1.05f, 1f, Mathf.Clamp01((nt - 0.06f) / 0.06f));
                    textAlpha = Mathf.Clamp01(nt / 0.05f);
                }
                if (t < Style.ReformAt)
                {
                    // The old value fades and shrinks as the cut goes in.
                    float f = Mathf.Clamp01((t - Style.CutFrom) / 0.08f);
                    textAlpha = 1f - f;
                    textScale = 1f - 0.2f * f;
                }
                if (!expired && ev.HasTarget)
                {
                    Line to = LineOf(live);
                    float travel = (t - Style.TravelFrom) / Style.TravelTime;
                    if (travel > 0f && to.Valid)
                    {
                        Say(Sounded, SoundRelocate);
                        float k = Ease01(travel);
                        at = Arc(launchFrom, to.Token, k, unit);
                        PaintTrail(launchFrom, to.Token, k, unit, ChallengeShapes.BronzeLit, travel < 1f);
                        if (travel >= 1f)
                        {
                            at = to.Token;
                            scale *= Land(t - Style.TravelFrom - Style.TravelTime);
                        }
                    }
                }
                PaintTokenAt(at, unit, scale, tint, alpha, split, leftSplit, leftAlpha, rightAlpha,
                    rightScale, squeeze, cut, shownValue, textAlpha, textScale, show);
                lastTokenAt = at;
                return;
            }

            if (ev != null && ev.Event == ChallengeEvent.Succeeded)
            {
                Line old = LineOf(releasing);
                at = old.Valid ? old.Token : lastTokenAt;
                // Punch, a peak of ivory-gold, a read, then the edges fold into the seed.
                float p = t - 0.12f;
                float s = p < 0f ? 1f
                    : p < 0.07f ? Mathf.Lerp(1f, Style.TokenPunch, Ease01(p / 0.07f))
                    : p < 0.15f ? Mathf.Lerp(Style.TokenPunch, 0.98f, Ease01((p - 0.07f) / 0.08f))
                    : Mathf.Lerp(0.98f, 1f, Ease01((p - 0.15f) / 0.07f));
                scale *= s;
                float peak = Mathf.Clamp01(1f - Mathf.Abs(t - 0.16f) / 0.12f);
                tint = Color.Lerp(tint, Style.IvoryGold, peak);
                if (p >= 0.02f && Say(null, "punch-glint"))
                {
                    Glint(at + new Vector2(-0.22f, 0.10f) * unit, unit);
                    Glint(at + new Vector2(0.25f, -0.06f) * unit, unit * 0.8f);
                }
                float fold = Ease01((t - Style.ReadHold) / Style.Compress);
                squeeze = 1f - fold;
                textAlpha = 1f - Mathf.Clamp01(fold * 1.5f);
                show = show && fold < 1f;
                PaintTokenAt(at, unit, scale, tint, alpha, 0f, 0f, 1f, 1f, 1f, squeeze, 0f, tokenValue,
                    textAlpha, 1f, show);
                launchFrom = at;
                return;
            }

            if (ev != null && ev.Event == ChallengeEvent.Started && ReferenceEquals(ev, playing))
            {
                Line to = LineOf(live);
                if (liveBuiltAt > eventAt + 0.001f)
                {
                    // Travelling in from where it waited.
                    float travel = t / Style.TravelTime;
                    float k = Ease01(travel);
                    at = to.Valid ? Arc(launchFrom, to.Token, k, unit) : launchFrom;
                    PaintTrail(launchFrom, to.Valid ? to.Token : launchFrom, k, unit,
                        ChallengeShapes.BronzeLit, travel < 1f);
                    if (travel >= 1f)
                    {
                        scale *= Land(t - Style.TravelTime);
                    }
                    Say(Sounded, SoundRelocate);
                }
                else
                {
                    at = to.Valid ? to.Token : lastTokenAt;
                    scale *= t < 0.08f ? Mathf.Lerp(0.8f, 1.05f, t / 0.08f)
                        : Mathf.Lerp(1.05f, 1f, Mathf.Clamp01((t - 0.08f) / 0.07f));
                    alpha = Mathf.Clamp01(t / 0.08f);
                }
            }
            else if (parked)
            {
                at = parkedAt;
            }
            else
            {
                at = liveLine.Valid ? liveLine.Token : lastTokenAt;
            }
            PaintTrail(at, at, 1f, unit, Color.clear, false);
            PaintTokenAt(at, unit, scale, tint, alpha, 0f, 0f, 1f, 1f, 1f, 1f, 0f, shownValue, 1f, 1f, show);
            PaintIdleGlint(at, unit, scale, show && playing == null);
            lastTokenAt = at;
        }

        private void PaintTokenAt(Vector2 at, float unit, float scale, Color tint, float alpha, float split,
            float leftSplit, float leftAlpha, float rightAlpha, float rightScale, float squeeze, float cut,
            int value, float textAlpha, float textScale, bool show)
        {
            float size = Style.TokenWidth * unit * scale;
            if (!show || alpha <= 0f)
            {
                tokenLeft.enabled = tokenRight.enabled = cutLine.enabled = false;
                text.gameObject.SetActive(false);
                textShade.gameObject.SetActive(false);
                return;
            }
            tokenLeft.enabled = leftAlpha > 0f && squeeze > 0f;
            tokenRight.enabled = rightAlpha > 0f && squeeze > 0f;
            // A squeeze folds both halves toward the middle: x shrinks, y a little.
            tokenLeft.transform.position = at - new Vector2(leftSplit, 0f);
            tokenLeft.transform.localScale = new Vector3(size * squeeze, size * Mathf.Lerp(0.6f, 1f, squeeze), 1f);
            tokenRight.transform.position = at + new Vector2(split + (1f - rightScale) * size * 0.2f, 0f);
            tokenRight.transform.localScale = new Vector3(size * squeeze * rightScale,
                size * rightScale * Mathf.Lerp(0.6f, 1f, squeeze), 1f);
            Color l = tint;
            l.a = alpha * leftAlpha;
            tokenLeft.color = l;
            Color r = tint;
            r.a = alpha * rightAlpha;
            tokenRight.color = r;

            // The cut: a thin warm-bronze line drawn down the face along the halves' own seam.
            cutLine.enabled = cut > 0f;
            if (cutLine.enabled)
            {
                float h = ChallengeShapes.TokenHalfHeight * 2f * size;
                float drawn = Mathf.Clamp01(cut * 1.2f);
                cutLine.transform.position = at + new Vector2(0f, h * 0.5f * (1f - drawn));
                cutLine.transform.rotation = Quaternion.Euler(0f, 0f, -Mathf.Atan(0.18f) * Mathf.Rad2Deg);
                cutLine.transform.localScale = new Vector3(Mathf.Max(size * 0.02f, 0.004f), h * drawn, 1f);
                Color c = Style.Final;
                c.a = Mathf.Clamp01(cut * 2f) * alpha;
                cutLine.color = c;
            }

            bool words = textAlpha > 0.001f && squeeze > 0.05f;
            text.gameObject.SetActive(words);
            textShade.gameObject.SetActive(words);
            if (words)
            {
                // THE NUMBER IS CORE'S.
                string s = "+" + value;
                text.text = s;
                textShade.text = s;
                float h = Style.TextHeight * unit * scale * textScale;
                text.characterSize = Mathf.Max(h * 10f / 64f, 0.0001f);
                textShade.characterSize = text.characterSize;
                text.transform.position = at - new Vector2(leftSplit, 0f);
                textShade.transform.position = at - new Vector2(leftSplit, 0f) + new Vector2(h * 0.05f, -h * 0.05f);
                Color ink = Style.Ink;
                ink.a = textAlpha * alpha;
                text.color = ink;
                Color sh = Style.InkShade;
                sh.a = textAlpha * alpha * 0.8f;
                textShade.color = sh;
            }
        }

        /// <summary>Every 3-5 s one small warm glint crosses the token's face. Never a loop of
        /// glow, never while anything else is happening to it.</summary>
        private void PaintIdleGlint(Vector2 at, float unit, float scale, bool allowed)
        {
            if (!allowed)
            {
                tokenGlint.enabled = false;
                return;
            }
            if (clock >= nextGlint)
            {
                glintStart = clock;
                float seed = Mathf.Repeat(Mathf.Sin(clock * 12.9898f) * 43758.5453f, 1f);
                nextGlint = clock + Mathf.Lerp(Style.GlintEveryMin, Style.GlintEveryMax, seed);
            }
            float age = clock - glintStart;
            if (age > Style.GlintSweep)
            {
                tokenGlint.enabled = false;
                return;
            }
            float k = age / Style.GlintSweep;
            float width = Style.TokenWidth * unit * scale * 0.6f;
            tokenGlint.enabled = true;
            tokenGlint.transform.position = at + new Vector2(Mathf.Lerp(-0.5f, 0.5f, k) * width, 0.02f * unit);
            float gs = unit * 0.16f * Mathf.Sin(k * Mathf.PI);
            tokenGlint.transform.localScale = new Vector3(gs, gs, 1f);
            tokenGlint.color = new Color(1f, 0.95f, 0.8f, 0.5f * Mathf.Sin(k * Mathf.PI));
        }

        private float glintStart = -99f;

        /// <summary>For the lab's token-idle scene: glint now.</summary>
        public void GlintSoon()
        {
            nextGlint = clock;
        }

        // ---- the reward ------------------------------------------------------------------------

        private void PaintEssence()
        {
            essence.enabled = false;
            ScoreScale = 1f;
            ScoreClaim = 0f;
            ChallengeVisuals ev = playing;
            if (ev == null || ev.Event != ChallengeEvent.Succeeded)
            {
                return;
            }
            Vector2 target = ScoreAnchor != null ? ScoreAnchor() : launchFrom;
            float launch = eventAt + Style.ReadHold + Style.Compress;
            float flight = (clock - launch) / Style.Flight;
            if (flight < 0f)
            {
                return;
            }
            Say(Sounded, SoundRewardLaunch);
            float unit = lastUnit;
            if (flight <= 1f)
            {
                if (!Layers.ShowEssence)
                {
                    return;
                }
                float k = Ease01(flight);
                Vector2 p = Arc(launchFrom, target, k, unit * 1.4f);
                essence.enabled = true;
                essence.transform.position = p;
                float s = unit * 0.34f * Mathf.Lerp(1f, 0.8f, k);
                essence.transform.localScale = new Vector3(s, s, 1f);
                essence.color = Color.white;
                PaintTrail(launchFrom, target, k, unit * 1.4f, ChallengeShapes.Gold, true);
                return;
            }
            PaintTrail(launchFrom, target, 1f, unit, Color.clear, false);
            if (arrivedAt < 0f)
            {
                arrivedAt = launch + Style.Flight;
                Say(Sounded, SoundRewardArrive);
            }
            float a = (clock - arrivedAt) / Style.ScoreTime;
            if (a > 1f || ev.ScoreDelta == 0)
            {
                return;
            }
            // 1 -> 1.08 -> 0.99 -> 1, warm gold.
            float wave = a < 0.3f ? Ease01(a / 0.3f)
                : a < 0.7f ? Mathf.Lerp(1f, -0.12f, Ease01((a - 0.3f) / 0.4f))
                : Mathf.Lerp(-0.12f, 0f, Ease01((a - 0.7f) / 0.3f));
            float amp = ev.ScoreDelta > 0 ? Style.ScorePunch : -Style.ScorePunch * 0.5f;
            ScoreScale = 1f + amp * wave;
            ScoreClaim = 1f - a;
        }

        /// <summary>A short ribbon behind a moving token: always smaller than what it follows.
        /// </summary>
        private void PaintTrail(Vector2 from, Vector2 to, float k, float unit, Color tint, bool on)
        {
            for (int i = 0; i < trail.Length; i++)
            {
                if (!on || !Layers.ShowEssence)
                {
                    trail[i].enabled = false;
                    continue;
                }
                float back = Mathf.Max(0f, k - 0.04f * (i + 1));
                trail[i].enabled = true;
                trail[i].transform.position = Arc(from, to, back, unit);
                float s = unit * (0.16f - 0.035f * i);
                trail[i].transform.localScale = new Vector3(s, s, 1f);
                Color c = tint;
                c.a = 0.45f - 0.12f * i;
                trail[i].color = c;
            }
        }

        /// <summary>Clean shortest curve: a quadratic bowed a little off the straight line, built
        /// from the two ends only.</summary>
        private static Vector2 Arc(Vector2 a, Vector2 b, float k, float unit)
        {
            Vector2 d = b - a;
            Vector2 n = new Vector2(-d.y, d.x);
            if (n.sqrMagnitude > 0.0001f)
            {
                n = n.normalized;
            }
            if (n.y < 0f)
            {
                n = -n; // bow upward
            }
            Vector2 c = (a + b) * 0.5f + n * Mathf.Min(d.magnitude * 0.18f, unit * 1.2f);
            float u = 1f - k;
            return u * u * a + 2f * u * k * c + k * k * b;
        }

        private static float Land(float t)
        {
            if (t < 0f)
            {
                return 1f;
            }
            return t < 0.04f ? Mathf.Lerp(1f, 1.05f, t / 0.04f)
                : t < 0.08f ? Mathf.Lerp(1.05f, 0.97f, (t - 0.04f) / 0.04f)
                : Mathf.Lerp(0.97f, 1f, Mathf.Clamp01((t - 0.08f) / 0.04f));
        }

        private float LockScale(float build)
        {
            float t = build - (Style.GrowFrom + Style.GrowTime);
            if (t < 0f)
            {
                return 0.9f;
            }
            float half = Style.LockTime * 0.5f;
            return t < half ? Mathf.Lerp(0.9f, Style.LockPunch, t / half)
                : Mathf.Lerp(Style.LockPunch, 1f, Mathf.Clamp01((t - half) / half));
        }

        private float Heartbeat()
        {
            float t = Mathf.Repeat(clock, Style.HeartbeatEvery);
            if (t > Style.HeartbeatTime)
            {
                return 1f;
            }
            return 1f + (Style.HeartbeatScale - 1f) * Mathf.Sin(t / Style.HeartbeatTime * Mathf.PI);
        }

        private static Color UrgencyColour(int urgency)
        {
            return urgency == 2 ? Style.Final : urgency == 1 ? Style.Tension : Style.Calm;
        }

        // ---- particles -------------------------------------------------------------------------

        private void Shatter(Line line)
        {
            if (!Layers.ShowParticles)
            {
                return;
            }
            for (int i = 0; i < Style.Shards; i++)
            {
                float along = (i + 0.5f) / Style.Shards;
                float side = i % 2 == 0 ? 1f : -1f;
                Vector2 p = line.A + (line.B - line.A) * along + line.Across * (side * line.Cell * 0.5f);
                Vector2 v = line.Across * (side * line.Cell * (1.6f + 0.5f * Mathf.Sin(i * 2.3f)))
                    + line.Along * (line.Cell * 0.4f * Mathf.Sin(i * 1.7f));
                AddBit(0, p, v, 3.2f * line.Cell, 360f * (i % 3 - 1), line.Cell * 0.16f, Color.white, 0.42f);
            }
        }

        private void Dust(Vector2 at, float unit)
        {
            if (!Layers.ShowParticles)
            {
                return;
            }
            for (int i = 0; i < Style.Dust; i++)
            {
                float ang = (i + 0.3f) / Style.Dust * Mathf.PI * 2f;
                AddBit(2, at, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) + 0.3f) * unit * 0.45f, 0.6f * unit, 0f,
                    unit * 0.09f, ChallengeShapes.BronzeLit, 0.55f);
            }
        }

        private void Glint(Vector2 at, float unit)
        {
            if (!Layers.ShowParticles)
            {
                return;
            }
            AddBit(1, at, Vector2.zero, 0f, 40f, unit * 0.34f, new Color(1f, 0.93f, 0.75f), 0.18f);
        }

        private void AddBit(int kind, Vector2 from, Vector2 velocity, float gravity, float spin, float size,
            Color tint, float life)
        {
            SpriteRenderer r = Rent(kind == 1 ? ParticleOrder + 1 : ParticleOrder);
            r.sprite = kind == 0 ? ChallengeShapes.Shard : kind == 1 ? HazineShapes.Glint : HazineShapes.Mote;
            bits.Add(new Bit
            {
                R = r, Born = clock, Life = life, From = from, Velocity = velocity, Gravity = gravity,
                Spin = spin, Size = size, Tint = tint, Kind = kind
            });
        }

        private void PaintBits()
        {
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                Bit b = bits[i];
                float age = clock - b.Born;
                if (age > b.Life)
                {
                    Return(b.R);
                    bits.RemoveAt(i);
                    continue;
                }
                float k = age / b.Life;
                b.R.enabled = true;
                b.R.transform.position = b.From + b.Velocity * age + new Vector2(0f, -0.5f * b.Gravity * age * age);
                b.R.transform.rotation = Quaternion.Euler(0f, 0f, b.Spin * age);
                float s = b.Kind == 1 ? b.Size * Mathf.Sin(k * Mathf.PI) : b.Size * Mathf.Lerp(1f, 0.6f, k);
                b.R.transform.localScale = new Vector3(s, s, 1f);
                Color c = b.Tint;
                c.a = b.Kind == 1 ? 0.9f : (k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                b.R.color = c;
            }
        }

        // ---- debug -----------------------------------------------------------------------------

        private void PaintDebug(Line line)
        {
            bool allowed = Application.isEditor || Debug.isDebugBuild;
            int used = 0;
            if (allowed && Layers.AnyDebug)
            {
                Line shown = line.Valid ? line : LineOf(releasing);
                if (shown.Valid)
                {
                    if (Layers.ShowChallengeTarget)
                    {
                        for (int i = 0; i < shown.Cells; i++)
                        {
                            Mark(ref used, shown.A + shown.Along * (i * shown.Cell), shown.Cell,
                                HazineShapes.Frame, new Color(1f, 0.85f, 0.2f, 0.7f));
                        }
                    }
                    if (Layers.ShowRailBounds)
                    {
                        for (int s = -1; s <= 1; s += 2)
                        {
                            Vector2 off = shown.Across * (s * shown.Cell * 0.5f);
                            Mark(ref used, shown.A - shown.Along * shown.Cell * 0.5f + off, shown.Cell * 0.12f,
                                HazineShapes.Mote, Color.cyan);
                            Mark(ref used, shown.B + shown.Along * shown.Cell * 0.5f + off, shown.Cell * 0.12f,
                                HazineShapes.Mote, Color.cyan);
                        }
                    }
                    if (Layers.ShowClampAnchors)
                    {
                        float o = (0.5f + Style.ClampOut) * shown.Cell;
                        Mark(ref used, shown.A - shown.Along * o, shown.Cell * 0.5f, HazineShapes.Ring, Color.magenta);
                        Mark(ref used, shown.B + shown.Along * o, shown.Cell * 0.5f, HazineShapes.Ring, Color.magenta);
                    }
                    if (Layers.ShowBonusTokenAnchor)
                    {
                        Mark(ref used, shown.Token, shown.Cell * 0.6f, HazineShapes.Ring, Color.green);
                    }
                    if (Layers.ShowEnergyCurrent)
                    {
                        for (int i = 0; i <= shown.Cells * 2; i++)
                        {
                            Mark(ref used, shown.A - shown.Along * shown.Cell * 0.5f
                                + shown.Along * (i * shown.Cell * 0.5f), shown.Cell * 0.05f,
                                HazineShapes.Mote, new Color(1f, 0.6f, 0.2f));
                        }
                    }
                }
                if (Layers.ShowSuccessLink && ScoreAnchor != null)
                {
                    Vector2 target = ScoreAnchor();
                    for (int i = 0; i <= 12; i++)
                    {
                        Mark(ref used, Arc(lastTokenAt, target, i / 12f, 1.26f), 0.06f, HazineShapes.Mote,
                            new Color(1f, 0.9f, 0.3f));
                    }
                }
                if (Layers.ShowRetargetPath && playing != null && playing.HasTarget
                    && playing.Event != ChallengeEvent.Started)
                {
                    Line to = LineOf(live);
                    for (int i = 0; to.Valid && i <= 12; i++)
                    {
                        Mark(ref used, Arc(launchFrom, to.Token, i / 12f, to.Cell), 0.06f, HazineShapes.Mote,
                            new Color(0.4f, 1f, 0.6f));
                    }
                }
            }
            for (int i = used; i < debugMarks.Count; i++)
            {
                debugMarks[i].enabled = false;
            }
            bool label = allowed && (Layers.ShowRemainingTurns || Layers.ShowAttemptIndex || Layers.ShowChallengeTarget);
            if (label)
            {
                if (debugText == null)
                {
                    debugText = ViewUtil.MakeText3D(transform, "ChallengeDebug", Vector2.zero, string.Empty,
                        40, 0.03f, Color.white, DebugOrder, TextAnchor.UpperLeft);
                }
                debugText.gameObject.SetActive(true);
                var sb = new System.Text.StringBuilder();
                if (Layers.ShowAttemptIndex) { sb.Append("Attempt: ").Append(live.Has ? live.Attempt : tokenAttempt).Append('\n'); }
                if (Layers.ShowRemainingTurns)
                {
                    sb.Append("Remaining: ").Append(live.Has ? live.TurnsLeft.ToString() : "-")
                        .Append(" / ").Append(live.InitialTurns).Append("  ").Append(live.Has ? live.Urgency.ToString() : "")
                        .Append('\n');
                }
                sb.Append("Bonus: ").Append(tokenShown ? tokenValue.ToString() : "-").Append('\n');
                sb.Append("Target: ").Append(live.Has ? (live.IsRow ? "ROW " : "COL ") + live.Line : parked ? "(waiting)" : "-");
                if (playing != null) { sb.Append("\nEvent: ").Append(playing.Event); }
                debugText.text = sb.ToString();
                float unit = line.Valid ? line.Cell : 0.9f;
                debugText.characterSize = unit * 0.16f * 10f / 40f;
                debugText.transform.position = lastTokenAt + new Vector2(-0.4f * unit, -0.35f * unit);
            }
            else if (debugText != null)
            {
                debugText.gameObject.SetActive(false);
            }
        }

        private void Mark(ref int used, Vector2 at, float size, Sprite sprite, Color c)
        {
            if (used >= debugMarks.Count)
            {
                debugMarks.Add(Rent(DebugOrder));
            }
            SpriteRenderer r = debugMarks[used++];
            r.enabled = true;
            r.sprite = sprite;
            r.transform.position = at;
            r.transform.rotation = Quaternion.identity;
            r.transform.localScale = new Vector3(size, size, 1f);
            r.color = c;
        }

        // ---- housekeeping ----------------------------------------------------------------------

        private bool Say(System.Action<string> channel, string what)
        {
            if (!said.Add(what))
            {
                return false;
            }
            if (channel != null)
            {
                channel(what);
            }
            return true;
        }

        private static void HideAll(List<SpriteRenderer> set)
        {
            for (int i = 0; i < set.Count; i++)
            {
                set[i].enabled = false;
            }
        }

        private SpriteRenderer Make(int order, Sprite sprite)
        {
            SpriteRenderer r = Rent(order);
            r.sprite = sprite;
            return r;
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
                var go = new GameObject("ContractPart");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
            }
            r.sortingOrder = order;
            r.enabled = false;
            r.color = Color.white;
            r.transform.rotation = Quaternion.identity;
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

        private static float Ease01(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - (1f - k) * (1f - k) * (1f - k); // fast start, smooth settle
        }
    }
}
