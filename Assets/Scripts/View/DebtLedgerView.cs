// PURPOSE: "Kredi kartı" - the BLACK LEDGER (KARA DEFTER): the debt contract panel that stands on
// screen for as long as a loan is open, and every event of the loan told on it. A rare joker gets a
// screen element of its own; a debt is not a number squeezed into a status line.
//
// THE PANEL is near-black charcoal with a burgundy undertone and ONE antique-brass trim, and it
// holds four facts in a strict hierarchy - and the first pass lost that hierarchy to its own size:
// a 230 x 70 px strip whose right third stacked ASGARİ / VADE / FAİZ in 8 px type was a stat
// widget, however good its palette. So the facts now have ROOM, and each has a zone:
//   BORÇ    the hero. Nearly twice the digits it had, in the bold cut, fitted to its zone so a
//           seven-figure debt shrinks rather than runs into its neighbour. Under it the LEDGER
//           BAND: a sunken groove, a deep burgundy fill for what is still owed of the largest
//           the loan has been, a brass CAP on the fill's end and a dim tick where this stage's
//           minimum ends.
//   ASGARİ  the minimum this stage owes, the real "312 / 648", and a small brass ledger track -
//           never an HP bar. Paid, it clicks full and its stop flashes ivory-gold; no green tick.
//   VADE    the second thing a player in debt needs to see from the corner of an eye: a big
//           number and its unit. The unit is AŞAMA, not tur - the term counts STAGES, and "tur"
//           on this HUD is a turn; a deadline that reads "3 turns left" would be a lie.
//   FAİZ    the rate, smallest.
// Along the bottom the MATURITY TRACK runs the panel's full width: one embossed segment per stage
// of the term, brass while ahead, charcoal once spent.
//
// TWO FORMS, ONE PANEL. WIDE lays the three zones side by side (3.3:1) and is what the market
// strip, a wide window and a phone get. STACKED puts debt + term over minimum + rate and is what
// the gap beside the board gets on a 16:9 screen, where there are 264 px between the board and
// the joker column and a wide strip simply does not fit. Both are the same elements at different
// coordinates (Plan), so nothing is drawn twice. A third, LEGACY, is the first pass's own layout
// kept for the lab's before/after and drawn by this same view.
//
// THE MOTION SAYS WHICH WAY THE BOOKS MOVED. A payment's digits slide DOWN and the band's end
// takes a brief amber edge as it retracts; interest's slide UP behind red ink creeping in from the
// right, the RATE's value giving way to what it just cost ("FAİZ +150", written where the rate
// stands) and a breath of muted red on the number - never a shake.
// Opening is a contract laid down with a burgundy seal; settling drains the band into a warm
// KAPANDI. A stage turning is a TICK: one segment goes brass -> burgundy -> charcoal, the panel
// settles a pixel, the term's digit rolls. The last stage steps the panel forward under a SON
// VADE stamp, and when the term runs out the last segment expires, a breath passes and the
// digits sit down - the contract closing, a beat before the bailiff.
//
// TENSION has two inputs and neither is decided here. The NAMED state is Core's
// (GameSession.CreditDeadline) and picks what may move at all: SAFE only a rare sheen, PRESSURE a
// faint shimmer on the band's end, WARNING a slow swell on the term and ink walking up the trim
// ONCE, FINAL DUE a slow heavy beat on the debt and a rare pixel of inward squeeze. The
// CONTINUOUS pressure (SetTension, from DebtPressurePresentationController) sets how much
// burgundy comes through the charcoal, so paying the loan down lightens the panel with the rest
// of the screen. No flash anywhere, no confetti, no siren.
//
// THE VIEW DECIDES NOTHING: every number (debt, minimum, what is paid of it, the rate, the next
// interest, the term) arrives in State, filled from the session's own queries by the controller.
// The only thing it keeps is the band's SCALE (the largest debt this loan has shown), which is
// presentation and nothing else.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class DebtLedgerView : MonoBehaviour
    {
        /// <summary>How the panel is laid out. See the header.</summary>
        public enum Form
        {
            Wide,
            Stacked,
            /// <summary>The first pass's strip, for the lab's before/after only.</summary>
            Legacy
        }

        // =================================================================== tuning
        public static class Style
        {
            public const float WideWidth = 3.6f;
            public const float WideHeight = 1.06f;
            public const float StackedHeight = 1.68f;
            public const float StackedMinWidth = 1.75f;
            public const float StackedMaxWidth = 2.3f;
            public const float LegacyWidth = 2.3f;
            public const float LegacyHeight = 0.72f;
            public static int Order = 60;

            public static readonly Color Charcoal = new Color(0.085f, 0.078f, 0.085f);
            public static readonly Color Undertone = new Color(0.2f, 0.055f, 0.08f);
            public static readonly Color Brass = new Color(0.69f, 0.56f, 0.34f);
            public static readonly Color BrassDim = new Color(0.36f, 0.3f, 0.2f);
            public static readonly Color BrassDark = new Color(0.47f, 0.35f, 0.2f);
            public static readonly Color Cream = new Color(0.97f, 0.92f, 0.82f);
            public static readonly Color CreamWarm = new Color(1f, 0.82f, 0.72f);
            public static readonly Color Wine = new Color(0.36f, 0.06f, 0.1f);
            public static readonly Color Burgundy = new Color(0.5f, 0.1f, 0.14f);
            public static readonly Color Amber = new Color(0.88f, 0.62f, 0.3f);
            public static readonly Color AmberMuted = new Color(0.7f, 0.5f, 0.27f);
            public static readonly Color Graphite = new Color(0.045f, 0.045f, 0.05f);
            public static readonly Color Spent = new Color(0.13f, 0.12f, 0.13f);
            public static readonly Color Crimson = new Color(0.64f, 0.17f, 0.18f);
            public static readonly Color MutedRed = new Color(0.86f, 0.5f, 0.46f);
            public static readonly Color GoldIvory = new Color(1f, 0.9f, 0.62f);
            public static readonly Color Ink = new Color(0.16f, 0.06f, 0.07f);

            /// <summary>The debt's own mark: the tab on the panel's head and the underline under
            /// the TOTAL are this one colour, which is what says they are one system.</summary>
            public static readonly Color Notch = new Color(0.52f, 0.11f, 0.15f);

            // The facts, character size at font 90 (a TextMesh line is 9 * size tall, its digits
            // about 0.7 of that).
            public static float DebtSize = 0.044f;
            public static float LabelSize = 0.0165f;
            public static float MinimumSize = 0.025f;
            /// <summary>The term's number: about 60% of the debt's.</summary>
            public static float TermSize = 0.0265f;
            public static float UnitSize = 0.016f;
            public static float RateSize = 0.021f;
            public static float SubSize = 0.019f;

            public static float OpenDuration = 0.21f;
            public static float TrimDelay = 0.08f;
            public static float OpenRoll = 0.14f;
            public static float PaymentTravel = 0.23f;
            public static float PaymentRoll = 0.22f;
            public static float Coalesce = 0.15f;
            public static float InterestCreep = 0.3f;
            public static float InterestRoll = 0.22f;
            public static float NumberFlash = 0.18f;
            public static float SettleDrain = 0.32f;
            public static float ExitDelay = 0.4f;
            public static float ExitDuration = 0.25f;

            public static float TickDuration = 0.36f;
            /// <summary>How far the panel sits down on a term tick, in its own units (1-2 px).</summary>
            public static float TickSettle = 0.016f;
            public static float LockPause = 0.1f;
            public static float LockDrop = 0.014f;

            public static float StampFrom = 1.35f;
            public static float StampUnder = 0.94f;
            public static float StampRotation = -3f;
            public static float ForwardTime = 0.18f;
            public static float ForwardScale = 1.045f;

            // idle, per named state
            public static float SheenMin = 6f;
            public static float SheenMax = 10f;
            public static float ShimmerMin = 5f;
            public static float ShimmerMax = 8f;
            public static float TermSwellMin = 4f;
            public static float TermSwellMax = 6f;
            public static float TermSwell = 0.04f;
            public static float FinalBeatMin = 2.2f;
            public static float FinalBeatMax = 3.2f;
            public static float FinalBeat = 0.025f;
            public static float SqueezeMin = 7f;
            public static float SqueezeMax = 11f;
            public static float SqueezeTime = 0.15f;
            /// <summary>The final stage's inward squeeze, as a share of the panel's width - a
            /// pixel or two at the size it is drawn.</summary>
            public static float Squeeze = 0.008f;
            /// <summary>How fast the ink walks up the trim when the state changes (per second).
            /// It happens once, at the transition, and then stands.</summary>
            public static float InkRate = 0.55f;
        }

        /// <summary>The lab's switches. The FEATURES (marker, progress, track) are on by default;
        /// the OVERLAYS (bounds, raw values, deadline state, interest preview, the two readouts)
        /// are off.</summary>
        public static class Layers
        {
            public static bool ShowDebtPanelBounds;
            public static bool ShowDebtValue;
            public static bool ShowMinimumMarker = true;
            public static bool ShowMinimumProgress = true;
            public static bool ShowMaturityTrack = true;
            public static bool ShowDeadlineState;
            public static bool ShowInterestPreview;
            public static bool ShowMaturityProgress;
            public static bool ShowResponsiveLayout;

            public static void Reset()
            {
                ShowDebtPanelBounds = false;
                ShowDebtValue = false;
                ShowMinimumMarker = true;
                ShowMinimumProgress = true;
                ShowMaturityTrack = true;
                ShowDeadlineState = false;
                ShowInterestPreview = false;
                ShowMaturityProgress = false;
                ShowResponsiveLayout = false;
            }
        }

        /// <summary>Everything the panel shows, straight from the session's queries.</summary>
        public struct State
        {
            public long Debt;
            /// <summary>The debt the stage in progress started with; 0 in the market.</summary>
            public long StageStartDebt;
            /// <summary>This stage's minimum (in a round) or the next stage's (in the market).</summary>
            public long MinimumDue;
            public long MinimumPaid;
            public bool MinimumSatisfied;
            public bool InRound;
            public int InterestPermille;
            public long NextInterest;
            public int TermLeft;
            public int TermTotal;
            public CreditDeadline Deadline;
        }

        public enum Cue
        {
            Open,
            Seal,
            Payment,
            MinimumSatisfied,
            Interest,
            Carry,
            /// <summary>The carried debt's slip has reached the TOTAL.</summary>
            CarryLanded,
            FinalDue,
            Settled,
            Bonus,
            Close,
            /// <summary>A stage of the term is spent.</summary>
            TermTick,
            /// <summary>The term ran out with money owed: the contract closes.</summary>
            ContractLock,
            /// <summary>The final stage's rare squeeze - a paper creak goes on it.</summary>
            Squeeze
        }

        public Action<Cue> Sounded;

        /// <summary>The TOTAL's world position - asked, never written down.</summary>
        public Func<Vector2> ScoreAnchor;

        /// <summary>The rate everything started from now on plays at. The game sets 1.</summary>
        public float PlaybackRate = 1f;

        public bool IsOpen { get; private set; }

        /// <summary>True while an event is being told.</summary>
        public bool Busy
        {
            get { return running > 0; }
        }

        /// <summary>What the last payment took off the debt it landed on, and what the last
        /// interest added to it, as shares (0..1). The pressure controller reads them on the
        /// matching cue - how much the screen lets go follows how much was actually paid.</summary>
        public float LastPaymentShare { get; private set; }

        public float LastInterestShare { get; private set; }

        public Form CurrentForm
        {
            get { return form; }
        }

        /// <summary>The panel's size on screen, in world units.</summary>
        public Vector2 WorldSize
        {
            get { return new Vector2(plan.W, plan.H) * transform.lossyScale.x; }
        }

        // =================================================================== state

        /// <summary>Where everything on the panel goes, for one form at one width.</summary>
        private struct Plan
        {
            public float W, H;
            public Vector2 Chip, ChipSize;
            public Vector2 DebtLabel;
            public Vector2 Debt;
            public float DebtMax;
            public float BarLeft, BarLength, BarY, BarHeight;
            public Vector2 MinLabel, MinValue, MinProgress;
            public TextAnchor MinProgressAnchor;
            public float MinTrackLeft, MinTrackLength, MinTrackY, MinTrackHeight;
            public Vector2 TermLabel, Term;
            /// <summary>The widest the term's number and unit may be together.</summary>
            public float TermMax;
            /// <summary>STACKED: the debt shares its row with the term, so its room is whatever
            /// the term leaves (DebtMax is then the whole row).</summary>
            public bool DebtBesideTerm;
            /// <summary>The widest the minimum's label may be ("SONRAKİ ASGARİ" is long).</summary>
            public float MinLabelMax;
            public Vector2 RateLabel, Rate;
            /// <summary>The rate's label sits to the LEFT of its value on one line (the narrow
            /// stacked form, where the rate takes the head row's right end).</summary>
            public bool RateBeside;
            /// <summary>LEGACY: all three right-hand facts are "LABEL value" on one line.</summary>
            public bool Compact;
            public float TrackLeft, TrackLength, TrackY, TrackHeight;
            public bool Ticks;
            public float Tick1, Tick2, TickY, TickHeight;
            public Vector2 Notch;
            public Vector2 Seal;
            public float SealSize;
            public Vector2 Stamp;
            public float DebtScale, LabelScale, MinScale, TermScale, RateScale, MinLabelScale,
                TermLabelScale, RateLabelScale;
        }

        private Transform panel;
        private SpriteRenderer body;
        private SpriteRenderer trim;
        private SpriteRenderer notch;
        private SpriteRenderer chip;
        private SpriteRenderer groove;
        private SpriteRenderer fill;
        private SpriteRenderer fillEnd;
        private SpriteRenderer fillHighlight;
        private SpriteRenderer fillEdge;
        private SpriteRenderer fillCap;
        private SpriteRenderer marker;
        private SpriteRenderer minGroove;
        private SpriteRenderer minFill;
        private SpriteRenderer minStop;
        private SpriteRenderer tickA;
        private SpriteRenderer tickB;
        private SpriteRenderer inkBottom;
        private SpriteRenderer inkLeft;
        private SpriteRenderer inkRight;
        private SpriteRenderer inkCreep;
        private SpriteRenderer dueSeal;
        private SpriteRenderer sheen;
        private SpriteRenderer shimmer;
        private SpriteRenderer openSeal;
        private readonly List<SpriteRenderer> segments = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> bounds = new List<SpriteRenderer>();
        private TextMesh debtLabel;
        private TextMesh minLabel;
        private TextMesh minValue;
        private TextMesh minProgress;
        private TextMesh termLabel;
        private TextMesh termUnit;
        private TextMesh rateLabel;
        private TextMesh rateText;
        private TextMesh devText;
        private Odometer debtNumber;
        private Odometer termNumber;

        private Form form = Form.Stacked;
        private float stackedWidth = Style.StackedMaxWidth;
        private Plan plan;
        private bool planned;

        private State state;
        private float shownFraction;
        private long peakDebt;
        private float openWidth;
        private float openAlpha;
        private float trimAlpha;
        private float undertoneBoost;
        private float scalePulse;
        private float tension;
        private float brighten;
        private float forward;
        private float settleY;
        private float squeeze;
        private float numberFlash;
        private float edgeFlash;
        private float inkShown;
        private float beatScale = 1f;
        private float termSwell = 1f;
        private float minPulse;
        private int running;
        private float nextBeat = -1f;
        private float nextSwell = -1f;
        private float nextSheen = -1f;
        private float nextShimmer = -1f;
        private float nextSqueeze = -1f;
        private float sheenClock = -1f;
        private float shimmerClock = -1f;
        private float squeezeClock = -1f;
        private float clock;
        private long pendingPayment;
        private Vector2 pendingFrom;
        private State pendingAfter;
        private bool paymentQueued;
        private bool minimumShownSatisfied;
        private bool sealPressed;
        private bool built;
        private int tickingSegment = -1;
        private float tickingK;
        private int termShown = -1;
        private Color stopFlash;
        private float stopFlashK;

        private float W
        {
            get { return plan.W; }
        }

        private float H
        {
            get { return plan.H; }
        }

        // =================================================================== building

        public void Build()
        {
            if (built)
            {
                return;
            }
            built = true;
            panel = new GameObject("DebtLedgerPanel").transform;
            panel.SetParent(transform, false);
            int o = Style.Order;
            body = Sprite(panel, "Body", ViewUtil.WhiteSprite, o);
            trim = Sprite(panel, "Trim", ViewUtil.WhiteSprite, o + 1);
            notch = Sprite(panel, "Notch", ViewUtil.WhiteSprite, o + 2);
            chip = Sprite(panel, "Chip", DebtLedgerShapes.Chip, o + 2);
            groove = Sprite(panel, "Groove", ViewUtil.WhiteSprite, o + 2);
            fill = Sprite(panel, "Fill", ViewUtil.WhiteSprite, o + 3);
            fillEnd = Sprite(panel, "FillEnd", DebtLedgerShapes.BandEnd, o + 3);
            fillHighlight = Sprite(panel, "FillHighlight", ViewUtil.WhiteSprite, o + 4);
            fillEdge = Sprite(panel, "FillEdge", ViewUtil.WhiteSprite, o + 4);
            marker = Sprite(panel, "Marker", ViewUtil.WhiteSprite, o + 5);
            fillCap = Sprite(panel, "FillCap", DebtLedgerShapes.Cap, o + 6);
            minGroove = Sprite(panel, "MinGroove", ViewUtil.WhiteSprite, o + 2);
            minFill = Sprite(panel, "MinFill", ViewUtil.WhiteSprite, o + 3);
            minStop = Sprite(panel, "MinStop", DebtLedgerShapes.Cap, o + 5);
            tickA = Sprite(panel, "TickA", ViewUtil.WhiteSprite, o + 2);
            tickB = Sprite(panel, "TickB", ViewUtil.WhiteSprite, o + 2);
            inkBottom = Sprite(panel, "InkBottom", ViewUtil.WhiteSprite, o + 2);
            inkLeft = Sprite(panel, "InkLeft", ViewUtil.WhiteSprite, o + 2);
            inkRight = Sprite(panel, "InkRight", ViewUtil.WhiteSprite, o + 2);
            inkCreep = Sprite(panel, "InkCreep", ViewUtil.WhiteSprite, o + 5);
            dueSeal = Sprite(panel, "DueSeal", DebtLedgerShapes.Seal, o + 6);
            sheen = Sprite(panel, "Sheen", DebtLedgerShapes.Soft, o + 7);
            shimmer = Sprite(panel, "Shimmer", DebtLedgerShapes.Soft, o + 7);
            openSeal = Sprite(panel, "OpenSeal", DebtLedgerShapes.Seal, o + 7);

            debtLabel = Text(panel, "DebtLabel", Loc.Pick("DEBT", "BORÇ"), Style.LabelSize,
                Style.BrassDim, o + 6, TextAnchor.MiddleLeft);
            minLabel = Text(panel, "MinLabel", " ", Style.LabelSize, Style.BrassDim, o + 6,
                TextAnchor.MiddleLeft);
            minValue = Text(panel, "MinValue", " ", Style.MinimumSize, Style.Cream, o + 6,
                TextAnchor.MiddleLeft);
            minProgress = Text(panel, "MinProgress", " ", Style.SubSize, Style.BrassDim, o + 6,
                TextAnchor.MiddleLeft);
            termLabel = Text(panel, "TermLabel", Loc.Pick("TERM", "VADE"), Style.LabelSize,
                Style.BrassDim, o + 6, TextAnchor.MiddleRight);
            termUnit = Text(panel, "TermUnit", " ", Style.UnitSize, Style.Cream, o + 6,
                TextAnchor.MiddleRight);
            rateLabel = Text(panel, "RateLabel", Loc.Pick("RATE", "FAİZ"), Style.LabelSize,
                Style.BrassDim, o + 6, TextAnchor.MiddleRight);
            rateText = Text(panel, "Rate", " ", Style.RateSize, Style.BrassDim, o + 6,
                TextAnchor.MiddleRight);
            devText = Text(panel, "Dev", " ", 0.0085f, new Color(0.6f, 1f, 0.9f), o + 9,
                TextAnchor.LowerCenter);
            debtNumber = new Odometer(panel, "Debt", Style.DebtSize, o + 6, 0.2f,
                TextAnchor.MiddleLeft, true);
            termNumber = new Odometer(panel, "Term", Style.TermSize, o + 6, 0.12f,
                TextAnchor.MiddleRight, true);

            for (int i = 0; i < 4; i++)
            {
                bounds.Add(Sprite(panel, "Bound" + i, ViewUtil.WhiteSprite, o + 9));
            }

            ApplyPlan();
            panel.gameObject.SetActive(false);
        }

        /// <summary>The size a form is laid out at, in the panel's own units.
        /// <paramref name="width"/> only matters to the stacked form, whose width follows the
        /// room it is given.</summary>
        public static Vector2 LocalSize(Form form, float width)
        {
            switch (form)
            {
                case Form.Wide:
                    return new Vector2(Style.WideWidth, Style.WideHeight);
                case Form.Legacy:
                    return new Vector2(Style.LegacyWidth, Style.LegacyHeight);
                default:
                    return new Vector2(
                        Mathf.Clamp(width, Style.StackedMinWidth, Style.StackedMaxWidth),
                        Style.StackedHeight);
            }
        }

        /// <summary>Which form, how wide (stacked only), where its centre stands and how big a
        /// unit of it is drawn.</summary>
        public void SetPlacement(Form wanted, float width, Vector2 centre, float scale)
        {
            Build();
            float w = LocalSize(wanted, width).x;
            if (!planned || wanted != form || Mathf.Abs(w - plan.W) > 0.012f)
            {
                form = wanted;
                stackedWidth = w;
                ApplyPlan();
            }
            transform.localPosition = new Vector3(centre.x, centre.y, 0f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>The continuous pressure, 0..1 (DebtPressurePresentationController). How much
        /// burgundy comes through the charcoal - so paying the loan down lightens the panel.</summary>
        public void SetTension(float pressure01)
        {
            tension = Mathf.Clamp01(pressure01);
        }

        /// <summary>The panel's own world position of the debt number (a payment's target).</summary>
        public Vector2 DebtNumberWorld
        {
            get
            {
                Build();
                return panel.TransformPoint(new Vector3(plan.Debt.x + 0.3f, plan.Debt.y, 0f));
            }
        }

        /// <summary>The panel's centre in the world.</summary>
        public Vector2 CentreWorld
        {
            get { return transform.position; }
        }

        private void ApplyPlan()
        {
            plan = BuildPlan(form, stackedWidth);
            planned = true;
            body.sprite = DebtLedgerShapes.PanelBody(plan.W, plan.H);
            trim.sprite = DebtLedgerShapes.PanelTrim(plan.W, plan.H);
            Place(body, Vector2.zero, plan.W, plan.H);
            Place(trim, Vector2.zero, plan.W, plan.H);
            SetAnchor(minProgress, plan.MinProgressAnchor);
            TextAnchor side = plan.Compact ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            SetAnchor(minLabel, side);
            SetAnchor(minValue, side);
        }

        private static Plan BuildPlan(Form form, float width)
        {
            var p = new Plan();
            p.DebtScale = p.LabelScale = p.MinScale = p.TermScale = p.RateScale = 1f;
            p.MinLabelScale = p.TermLabelScale = p.RateLabelScale = 1f;
            p.MinProgressAnchor = TextAnchor.MiddleLeft;
            const float pad = 0.17f;
            if (form == Form.Legacy)
            {
                // The first pass, as it was: a 3.2:1 strip with the three lesser facts stacked
                // at its right edge in one small size each.
                p.W = Style.LegacyWidth;
                p.H = Style.LegacyHeight;
                float l = -p.W * 0.5f;
                float r = p.W * 0.5f - 0.1f;
                p.Chip = new Vector2(l + 0.17f, 0.1f);
                p.ChipSize = new Vector2(0.2f, 0.15f);
                p.DebtLabel = new Vector2(l + 0.34f, 0.215f);
                p.Debt = new Vector2(l + 0.34f, 0.04f);
                p.DebtMax = 0.95f;
                p.BarLength = p.W - 0.24f;
                p.BarLeft = -p.BarLength * 0.5f;
                p.BarY = -0.15f;
                p.BarHeight = 0.07f;
                p.Compact = true;
                p.MinValue = new Vector2(r, 0.2f);
                p.Term = new Vector2(r, 0.07f);
                p.Rate = new Vector2(r, -0.04f);
                p.TrackY = -p.H * 0.5f + 0.085f;
                p.TrackHeight = 0.045f;
                p.Seal = new Vector2(l + 1.06f, 0.07f);
                p.SealSize = 0.13f;
                p.Stamp = new Vector2(0.18f, 0.02f);
                p.Notch = new Vector2(-p.W * 0.25f, p.H * 0.5f); // unused: the first pass had no mark
                p.DebtScale = 0.024f / Style.DebtSize;
                p.LabelScale = 0.0085f / Style.LabelSize;
                p.MinScale = 0.0125f / Style.MinimumSize;
                p.MinLabelScale = 0.0125f / Style.LabelSize;
                p.TermScale = 0.0108f / Style.TermSize;
                p.TermLabelScale = 0.0108f / Style.LabelSize;
                p.RateScale = 0.0086f / Style.RateSize;
                p.RateLabelScale = 0.0086f / Style.LabelSize;
                return p;
            }
            if (form == Form.Wide)
            {
                // Three zones side by side: the hero takes 45%, then the minimum, then the term
                // over the rate. No rule between them - spacing and one dim brass tick. The
                // zones' widths were MEASURED in Fredoka at these sizes: 1.30 holds "125 900",
                // 0.94 holds "1 312 / 1 648", 0.78 holds "4 AŞAMA" and "4 STAGES".
                p.W = Style.WideWidth;
                p.H = Style.WideHeight;
                float x0 = -p.W * 0.5f + pad;
                float xr = p.W * 0.5f - pad;
                float centre = x0 + 1.30f + 0.12f;
                p.Chip = new Vector2(x0 + 0.085f, 0.385f);
                p.ChipSize = new Vector2(0.17f, 0.125f);
                p.DebtLabel = new Vector2(x0 + 0.22f, 0.385f);
                p.Debt = new Vector2(x0, 0.1f);
                p.DebtMax = 1.3f;
                p.BarLeft = x0;
                p.BarLength = 1.3f;
                p.BarY = -0.215f;
                p.BarHeight = 0.1f;
                p.Ticks = true;
                p.Tick1 = centre - 0.06f;
                p.Tick2 = centre + 0.94f + 0.06f;
                p.TickY = 0.085f;
                p.TickHeight = 0.62f;
                p.MinLabel = new Vector2(centre, 0.385f);
                p.MinValue = new Vector2(centre, 0.165f);
                p.MinProgress = new Vector2(centre, -0.045f);
                p.MinTrackLeft = centre;
                p.MinTrackLength = 0.94f;
                p.MinTrackY = -0.215f;
                p.MinTrackHeight = 0.085f;
                p.TermLabel = new Vector2(xr, 0.385f);
                p.Term = new Vector2(xr, 0.16f);
                p.TermMax = 0.78f;
                p.MinLabelMax = 0.94f;
                p.RateLabel = new Vector2(xr, -0.045f);
                p.Rate = new Vector2(xr, -0.215f);
                p.TrackLeft = x0;
                p.TrackLength = p.W - pad * 2f;
                p.TrackY = -0.4f;
                p.TrackHeight = 0.085f;
                p.Notch = new Vector2(x0 + 0.17f, p.H * 0.5f - 0.02f);
                p.Seal = new Vector2(x0 + 1.18f, 0.385f);
                p.SealSize = 0.12f;
                p.Stamp = new Vector2(0.1f, 0.06f);
                return p;
            }
            {
                // Debt + term over minimum + rate: the form the gap beside the board gets. When
                // the gap is NARROW (a 16:10 screen) the debt takes its whole row and the term
                // drops to the second row at the same size; the rate, smallest, moves up to the
                // head row's right end. Squeezing the debt beside the term there cost it a third
                // of its size, measured.
                p.W = Mathf.Clamp(width, Style.StackedMinWidth, Style.StackedMaxWidth);
                p.H = Style.StackedHeight;
                float x0 = -p.W * 0.5f + pad;
                float xr = p.W * 0.5f - pad;
                float inner = p.W - pad * 2f;
                bool narrow = p.W < 2.05f;
                p.Chip = new Vector2(x0 + 0.085f, 0.665f);
                p.ChipSize = new Vector2(0.17f, 0.125f);
                p.DebtLabel = new Vector2(x0 + 0.22f, 0.665f);
                p.Debt = new Vector2(x0, 0.405f);
                p.DebtMax = inner;
                p.DebtBesideTerm = !narrow;
                p.TermMax = 0.85f;
                p.MinLabelMax = inner * 0.62f;
                p.BarLeft = x0;
                p.BarLength = inner;
                p.BarY = 0.155f;
                p.BarHeight = 0.1f;
                p.MinLabel = new Vector2(x0, -0.04f);
                p.MinValue = new Vector2(x0, -0.225f);
                if (narrow)
                {
                    p.TermLabel = new Vector2(xr, -0.04f);
                    p.Term = new Vector2(xr, -0.225f);
                    p.Rate = new Vector2(xr, 0.665f);
                    p.RateBeside = true;
                    p.RateScale = Style.LabelSize / Style.RateSize;
                }
                else
                {
                    p.TermLabel = new Vector2(xr, 0.665f);
                    p.Term = new Vector2(xr, 0.41f);
                    p.RateLabel = new Vector2(xr, -0.04f);
                    p.Rate = new Vector2(xr, -0.225f);
                }
                p.MinTrackLeft = x0;
                p.MinTrackLength = inner * 0.55f;
                p.MinTrackY = -0.43f;
                p.MinTrackHeight = 0.085f;
                p.MinProgress = new Vector2(xr, -0.43f);
                p.MinProgressAnchor = TextAnchor.MiddleRight;
                p.TrackLeft = x0;
                p.TrackLength = inner;
                p.TrackY = -0.655f;
                p.TrackHeight = 0.085f;
                p.Notch = new Vector2(x0 + 0.17f, p.H * 0.5f - 0.02f);
                // pressed on the head, just past the tab - clear of whatever holds the row
                p.Seal = new Vector2(x0 + 0.46f, p.H * 0.5f - 0.04f);
                p.SealSize = 0.1f;
                p.Stamp = new Vector2(0f, 0.36f);
                return p;
            }
        }

        // =================================================================== driving it

        /// <summary>Restates the panel without an event - a repaint, a load, the lab's statics.
        /// Opens it (instantly) if it is shut and there is a debt; shuts it if there is none.</summary>
        public void ShowState(State s)
        {
            Build();
            if (s.Debt <= 0)
            {
                // Paid off: the panel stays until the controller plays its settlement - a debt
                // reaching zero is an event, not a repaint.
                if (IsOpen)
                {
                    state.MinimumDue = s.MinimumDue;
                    state.Deadline = s.Deadline;
                }
                return;
            }
            state = s;
            if (!IsOpen)
            {
                IsOpen = true;
                panel.gameObject.SetActive(true);
                openWidth = 1f;
                openAlpha = 1f;
                trimAlpha = 1f;
                peakDebt = s.Debt;
                sealPressed = true;
                // A panel that is simply THERE (a load, a repaint) wears the ink its state has
                // already earned; only a state that changes while it stands walks the ink in.
                inkShown = InkTarget(s.Deadline);
            }
            peakDebt = Math.Max(peakDebt, s.Debt);
            if (running == 0)
            {
                debtNumber.Snap(s.Debt);
                termNumber.Snap(Mathf.Max(0, s.TermLeft));
                termShown = s.TermLeft;
                shownFraction = Fraction(s.Debt);
                minimumShownSatisfied = s.MinimumSatisfied;
            }
        }

        /// <summary>THE CONTRACT OPENS: a dark sliver widening into the strip, the brass closing
        /// behind it, the number rolling in, and a burgundy seal pressed on. Never confetti.</summary>
        public void PlayOpen(State s)
        {
            Build();
            state = s;
            peakDebt = Math.Max(1L, s.Debt);
            IsOpen = true;
            inkShown = 0f;
            termNumber.Snap(Mathf.Max(0, s.TermLeft));
            termShown = s.TermLeft;
            panel.gameObject.SetActive(true);
            StartCoroutine(Open(s));
        }

        /// <summary>A payment landed (Core's amount). Coalesced: everything that lands inside
        /// Style.Coalesce is one chip.</summary>
        public void PlayPayment(long amount, Vector2 from, State after)
        {
            Build();
            if (amount <= 0)
            {
                ShowState(after);
                return;
            }
            pendingPayment += amount;
            pendingFrom = from;
            pendingAfter = after;
            if (!paymentQueued)
            {
                paymentQueued = true;
                StartCoroutine(Payment());
            }
        }

        /// <summary>Interest was charged (Core's numbers): ink creeps in from the right.</summary>
        public void PlayInterest(long previousDebt, long interest, State after)
        {
            Build();
            if (!IsOpen)
            {
                ShowState(after);
            }
            StartCoroutine(Interest(previousDebt, interest, after, true));
        }

        /// <summary>More was borrowed on an open loan: the number climbs and the band grows,
        /// the same upward direction as interest but without its ink - it was chosen.</summary>
        public void PlayBorrow(long previousDebt, State after)
        {
            Build();
            if (!IsOpen)
            {
                PlayOpen(after);
                return;
            }
            StartCoroutine(Interest(previousDebt, after.Debt - previousDebt, after, false));
        }

        /// <summary>The debt on the panel follows a foreclosure's dossier down, seizure by
        /// seizure (Core's numbers).</summary>
        public void RollDebtTo(long debt)
        {
            Build();
            if (!IsOpen)
            {
                return;
            }
            debtNumber.Roll(debtNumber.Value, Math.Max(0, debt), true, 0.14f);
            state.Debt = Math.Max(0, debt);
            shownFraction = Fraction(state.Debt);
        }

        /// <summary>The bank's bonus as a warm token to the TOTAL, whether or not the panel is
        /// still up (a loan paid off mid-stage has closed by the time the stage pays its thanks).</summary>
        public void PlayBonusOnly(long bonus)
        {
            Build();
            if (bonus > 0)
            {
                StartCoroutine(BonusToken(bonus));
            }
        }

        /// <summary>THE DEBT PULLS THE TOTAL DOWN. A new stage begins in debt: the ledger
        /// brightens for a moment, a dark-red slip with the debt on it leaves for the TOTAL,
        /// and CarryLanded says when it arrives - the score rolls down THEN, so the number is
        /// dragged under by the debt rather than starting the round already negative.</summary>
        public void PlayCarry(State s)
        {
            Build();
            if (!IsOpen)
            {
                ShowState(s);
            }
            state = s;
            StartCoroutine(Carry(s.Debt));
        }

        /// <summary>THE LAST STAGE OF THE TERM HAS BEGUN: the panel steps forward, SON VADE is
        /// stamped on it with a dry thud, and it goes back to its place.</summary>
        public void PlayFinalDueStamp()
        {
            Build();
            if (!IsOpen)
            {
                return;
            }
            StartCoroutine(FinalDue());
        }

        /// <summary>The minimum is paid (normally follows a payment by itself).</summary>
        public void PlayMinimumSatisfied()
        {
            Build();
            if (!IsOpen)
            {
                return;
            }
            StartCoroutine(MinimumSatisfied());
        }

        /// <summary>A STAGE OF THE TERM IS SPENT (Core's two numbers): its segment darkens
        /// brass -> burgundy -> charcoal, a dry tick, the panel sits down a pixel and the term's
        /// digit rolls. No popup.</summary>
        public void PlayTermTick(int fromLeft, int toLeft)
        {
            Build();
            if (!IsOpen)
            {
                return;
            }
            StartCoroutine(TermTick(fromLeft, toLeft, false));
        }

        /// <summary>THE TERM RAN OUT WITH MONEY OWED: the last segment expires, a breath of
        /// nothing, and the debt's digits sit down. The contract has closed; the bailiff is the
        /// next thing on screen, and this is what keeps it from arriving as a popup.</summary>
        public void PlayContractLock(int fromLeft)
        {
            Build();
            if (!IsOpen)
            {
                return;
            }
            StartCoroutine(TermTick(fromLeft, 0, true));
        }

        /// <summary>The loan is paid off. <paramref name="bonus"/> is what the bank paid for it
        /// being on time (0 when it was not, or when its thanks were a campaign).</summary>
        public void PlaySettled(long bonus)
        {
            Build();
            if (!IsOpen)
            {
                return;
            }
            StartCoroutine(Settle(bonus));
        }

        /// <summary>Shuts the panel.</summary>
        public void Close(bool animated)
        {
            if (!built)
            {
                return;
            }
            if (!animated)
            {
                StopAllCoroutines();
                ClearTransients();
                running = 0;
                paymentQueued = false;
                pendingPayment = 0;
                IsOpen = false;
                panel.gameObject.SetActive(false);
                peakDebt = 0;
                sealPressed = false;
                minimumShownSatisfied = false;
                return;
            }
            StartCoroutine(Exit());
        }

        /// <summary>Everything a stopped coroutine would have put back.</summary>
        private void ClearTransients()
        {
            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                Transform c = panel.GetChild(i);
                if (c.name == "Stamp")
                {
                    Destroy(c.gameObject);
                }
            }
            interestEntry = 0;
            interestEntryK = 0f;
            if (transform.parent != null)
            {
                for (int i = transform.parent.childCount - 1; i >= 0; i--)
                {
                    Transform c = transform.parent.GetChild(i);
                    if (c.name == "PaymentChip" || c.name == "CarrySlip" || c.name == "BonusToken"
                        || c.name == "LedgerFleck")
                    {
                        Destroy(c.gameObject);
                    }
                }
            }
            undertoneBoost = 0f;
            scalePulse = 0f;
            brighten = 0f;
            carrying = false;
            forward = 0f;
            settleY = 0f;
            squeeze = 0f;
            numberFlash = 0f;
            edgeFlash = 0f;
            stopFlashK = 0f;
            tickingSegment = -1;
            inkCreep.color = Color.clear;
            debtNumber.Nudge(0f);
        }

        // =================================================================== events

        private IEnumerator Open(State s)
        {
            running++;
            openWidth = 0.06f;
            openAlpha = 0f;
            trimAlpha = 0f;
            sealPressed = false;
            openSeal.color = Color.clear;
            debtNumber.Snap(0);
            shownFraction = 0f;
            Emit(Cue.Open);
            float t = 0f;
            float total = Style.OpenDuration + Style.TrimDelay + 0.1f;
            bool rolled = false;
            while (t < total)
            {
                t += Dt;
                openAlpha = Mathf.Clamp01(t / 0.06f);
                openWidth = Mathf.Lerp(0.06f, 1f, EaseOut(Mathf.Clamp01(t / Style.OpenDuration)));
                trimAlpha = Mathf.Clamp01((t - Style.TrimDelay) / Style.OpenDuration);
                if (!rolled && t >= Style.OpenDuration * 0.55f)
                {
                    rolled = true;
                    debtNumber.Roll(0, s.Debt, false, Style.OpenRoll);
                }
                if (rolled)
                {
                    shownFraction = Mathf.Lerp(0f, Fraction(s.Debt),
                        EaseOut(Mathf.Clamp01((t - Style.OpenDuration * 0.55f) / Style.OpenRoll)));
                }
                yield return null;
            }
            openWidth = 1f;
            openAlpha = 1f;
            trimAlpha = 1f;
            shownFraction = Fraction(s.Debt);
            // The seal - "tok" - locks the contract.
            Emit(Cue.Seal);
            float k = 0f;
            while (k < 0.16f)
            {
                k += Dt;
                float u = Mathf.Clamp01(k / 0.08f);
                sealScale = Mathf.Lerp(1.5f, 1f, EaseOut(u));
                openSeal.color = WithAlpha(Style.Burgundy, Mathf.Clamp01(k / 0.04f) * 0.95f);
                yield return null;
            }
            sealScale = 1f;
            sealPressed = true;
            running--;
        }

        private float sealScale = 1f;

        private IEnumerator Payment()
        {
            running++;
            float wait = 0f;
            while (wait < Style.Coalesce)
            {
                wait += Dt;
                yield return null;
            }
            long amount = pendingPayment;
            Vector2 from = pendingFrom;
            State after = pendingAfter;
            pendingPayment = 0;
            paymentQueued = false;
            if (!IsOpen)
            {
                ShowState(after);
            }

            // The chip: cream/brass, "-250 BORÇ", flying in on a short curve.
            var chipGo = new GameObject("PaymentChip").transform;
            chipGo.SetParent(transform.parent, false);
            SpriteRenderer plate = Sprite(chipGo, "Plate", ViewUtil.RoundedSprite, Style.Order + 12);
            TextMesh label = Text(chipGo, "Label", "-" + Money(amount) + " " + Loc.Pick("DEBT", "BORÇ"),
                0.016f, Style.Ink, Style.Order + 13, TextAnchor.MiddleCenter);
            float plateW = TextWidth(label, label.text) + 0.22f;
            Place(plate, Vector2.zero, plateW, 0.3f);
            plate.color = Style.Cream;
            Vector2 to = DebtNumberWorld;
            Vector2 ctrl = (from + to) * 0.5f + new Vector2(0f, 0.9f);
            float t = 0f;
            while (t < Style.PaymentTravel)
            {
                t += Dt;
                float u = EaseInOut(Mathf.Clamp01(t / Style.PaymentTravel));
                chipGo.position = Bezier(from, ctrl, to, u);
                yield return null;
            }
            // Contact: absorbed, the band retracts behind a brief amber edge, the digits slide
            // DOWN - and the share it took is published for whoever lets the screen breathe.
            long before = Math.Max(debtNumber.Value, after.Debt + amount);
            LastPaymentShare = before > 0 ? Mathf.Clamp01(amount / (float)before) : 1f;
            Emit(Cue.Payment);
            float fromFraction = shownFraction;
            state = after;
            debtNumber.Roll(debtNumber.Value, after.Debt, true, Style.PaymentRoll);
            Flecks(to, 2 + (int)(amount % 3), Style.Brass);
            float k = 0f;
            float toFraction = Fraction(after.Debt);
            while (k < Style.PaymentRoll)
            {
                k += Dt;
                float u = Mathf.Clamp01(k / Style.PaymentRoll);
                float s = u < 0.4f ? Mathf.Lerp(1f, 0.4f, u / 0.4f) : Mathf.Lerp(0.4f, 0f, (u - 0.4f) / 0.6f);
                chipGo.localScale = new Vector3(s, s, 1f);
                shownFraction = Mathf.Lerp(fromFraction, toFraction, EaseOut(u));
                edgeFlash = Mathf.Sin(u * Mathf.PI);
                yield return null;
            }
            Destroy(chipGo.gameObject);
            edgeFlash = 0f;
            shownFraction = toFraction;
            if (after.MinimumSatisfied && !minimumShownSatisfied)
            {
                yield return MinimumSatisfied();
            }
            running--;
        }

        /// <summary>THE MINIMUM IS PAID: the track is full, a mechanical click, and its stop
        /// goes dim -> warm -> ivory-gold for a breath. No green tick, no stamp - a bank does not
        /// congratulate anyone for the minimum.</summary>
        private IEnumerator MinimumSatisfied()
        {
            running++;
            minimumShownSatisfied = true;
            Emit(Cue.MinimumSatisfied);
            float t = 0f;
            while (t < 0.3f)
            {
                t += Dt;
                float u = Mathf.Clamp01(t / 0.3f);
                stopFlash = u < 0.45f ? Color.Lerp(Style.BrassDim, Style.Amber, u / 0.45f)
                    : Color.Lerp(Style.GoldIvory, Style.Brass, (u - 0.45f) / 0.55f);
                stopFlashK = 1f;
                yield return null;
            }
            stopFlashK = 0f;
            running--;
        }

        private IEnumerator Interest(long previous, long interest, State after, bool isInterest)
        {
            running++;
            LastInterestShare = previous > 0 ? Mathf.Clamp01(interest / (float)previous) : 0f;
            if (isInterest)
            {
                // Interest lands at a stage's end, a beat BEFORE the term ticks: the books the
                // controller hands over already carry the shorter term, and showing it here would
                // spend the segment before its tick is told.
                after.TermLeft = state.TermLeft;
                after.TermTotal = state.TermTotal;
                after.Deadline = state.Deadline;
                Emit(Cue.Interest);
            }
            peakDebt = Math.Max(peakDebt, after.Debt);
            float fromFraction = Fraction(previous);
            float toFraction = Fraction(after.Debt);
            // "FAİZ +250": the entry is written where the RATE stands - the rate's value gives
            // way to what it just cost, in muted red, and comes back. The message is that the
            // debt grew by itself. (A new purchase has no entry: it was chosen, and the number
            // climbing says enough.)
            interestEntry = isInterest ? interest : 0;
            float t = 0f;
            bool rolled = false;
            float total = Style.InterestCreep + Style.InterestRoll + 0.65f;
            while (t < total)
            {
                t += Dt;
                float creep = Mathf.Clamp01(t / Style.InterestCreep);
                // red ledger ink creeping in from the band's right end toward the fill's end
                float reach = Mathf.Lerp(0f, plan.BarLength * Mathf.Max(0.12f, 1f - toFraction + 0.1f),
                    EaseOut(creep));
                float fade = t > Style.InterestCreep + Style.InterestRoll ? 1f
                    - (t - Style.InterestCreep - Style.InterestRoll) / 0.65f : 1f;
                Place(inkCreep, new Vector2(plan.BarLeft + plan.BarLength - reach * 0.5f, plan.BarY),
                    reach, plan.BarHeight * 1.3f);
                inkCreep.color = WithAlpha(Style.Burgundy, isInterest ? 0.7f * Mathf.Clamp01(fade) : 0f);
                if (!rolled && t >= Style.InterestCreep * 0.6f)
                {
                    rolled = true;
                    debtNumber.Roll(previous, after.Debt, false, Style.InterestRoll);
                    state = after;
                }
                if (rolled)
                {
                    float since = t - Style.InterestCreep * 0.6f;
                    float u = Mathf.Clamp01(since / Style.InterestRoll);
                    shownFraction = Mathf.Lerp(fromFraction, toFraction, EaseOut(u));
                    if (isInterest)
                    {
                        scalePulse = 0.008f * Mathf.Sin(u * Mathf.PI);
                        undertoneBoost = 0.35f * Mathf.Sin(u * Mathf.PI);
                        // cream -> muted burgundy-red -> cream, about 180 ms
                        numberFlash = Mathf.Sin(Mathf.Clamp01(since / Style.NumberFlash) * Mathf.PI);
                    }
                }
                interestEntryK = isInterest ? Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01(fade) : 0f;
                yield return null;
            }
            inkCreep.color = Color.clear;
            scalePulse = 0f;
            undertoneBoost = 0f;
            numberFlash = 0f;
            interestEntryK = 0f;
            interestEntry = 0;
            shownFraction = toFraction;
            running--;
        }

        private long interestEntry;
        private float interestEntryK;

        private bool carrying;

        private IEnumerator Carry(long debt)
        {
            running++;
            carrying = true;
            Emit(Cue.Carry);
            // the ledger brightens for a moment - this is where the weight comes from
            float t = 0f;
            while (t < 0.16f)
            {
                t += Dt;
                brighten = Mathf.Sin(Mathf.Clamp01(t / 0.32f) * Mathf.PI);
                yield return null;
            }
            Vector2 from = DebtNumberWorld;
            Vector2 to = ScoreAnchor != null ? ScoreAnchor() : from + new Vector2(-3f, 1.5f);
            var slip = new GameObject("CarrySlip").transform;
            slip.SetParent(transform.parent, false);
            SpriteRenderer plate = Sprite(slip, "Plate", ViewUtil.RoundedSprite, Style.Order + 12);
            TextMesh label = Text(slip, "Label", "-" + Money(debt), 0.0185f, Style.Cream,
                Style.Order + 13, TextAnchor.MiddleCenter);
            Place(plate, Vector2.zero, TextWidth(label, label.text) + 0.26f, 0.34f);
            plate.color = Style.Wine;
            Vector2 ctrl = (from + to) * 0.5f + new Vector2(0f, 0.7f);
            const float travel = 0.34f;
            bool landed = false;
            t = 0f;
            while (t < travel + 0.18f)
            {
                t += Dt;
                brighten = Mathf.Sin(Mathf.Clamp01((t + 0.16f) / 0.32f) * Mathf.PI);
                float u = EaseInOut(Mathf.Clamp01(t / travel));
                slip.position = Bezier(from, ctrl, to, u);
                float fade = t > travel ? 1f - (t - travel) / 0.18f : 1f;
                plate.color = WithAlpha(Style.Wine, Mathf.Clamp01(fade));
                ViewUtil.SetTextColor(label, WithAlpha(Style.Cream, Mathf.Clamp01(fade)));
                if (!landed && t >= travel)
                {
                    landed = true;
                    Emit(Cue.CarryLanded);
                }
                // pressure: the debt settles a pixel as the stage turns
                debtNumber.Nudge(state.Deadline >= CreditDeadline.Pressure
                    ? -0.012f * Mathf.Sin(Mathf.Clamp01(t / travel) * Mathf.PI) : 0f);
                yield return null;
            }
            if (!landed)
            {
                Emit(Cue.CarryLanded);
            }
            brighten = 0f;
            debtNumber.Nudge(0f);
            Destroy(slip.gameObject);
            carrying = false;
            running--;
        }

        private IEnumerator FinalDue()
        {
            running++;
            // After the carry, never over it: the debt lands on the TOTAL first, then the stamp.
            while (carrying)
            {
                yield return null;
            }
            // 150-220 ms forward...
            float t = 0f;
            while (t < Style.ForwardTime)
            {
                t += Dt;
                forward = EaseOut(Mathf.Clamp01(t / Style.ForwardTime));
                yield return null;
            }
            forward = 1f;
            // ...the stamp: 1.35 -> 0.94 -> 1, turned three degrees, a dry thud...
            Coroutine stamp = StartCoroutine(SmallStamp(Loc.Pick("FINAL DUE", "SON VADE"),
                Style.Wine, 1.1f, Cue.FinalDue));
            float hold = 0f;
            while (hold < 0.34f)
            {
                hold += Dt;
                yield return null;
            }
            // ...and back to its place.
            t = 0f;
            while (t < 0.22f)
            {
                t += Dt;
                forward = 1f - EaseInOut(Mathf.Clamp01(t / 0.22f));
                yield return null;
            }
            forward = 0f;
            yield return stamp;
            running--;
        }

        private IEnumerator TermTick(int fromLeft, int toLeft, bool closing)
        {
            running++;
            int total = Mathf.Max(0, state.TermTotal);
            int segment = Mathf.Clamp(total - fromLeft, 0, Mathf.Max(0, total - 1));
            tickingSegment = total > 0 ? segment : -1;
            tickingK = 0f;
            termShown = fromLeft;
            bool ticked = false;
            float t = 0f;
            while (t < Style.TickDuration)
            {
                t += Dt;
                float u = Mathf.Clamp01(t / Style.TickDuration);
                tickingK = u;
                if (!ticked && u >= 0.4f)
                {
                    ticked = true;
                    Emit(Cue.TermTick);
                    if (!closing)
                    {
                        termNumber.Roll(fromLeft, Mathf.Max(0, toLeft), true, 0.2f);
                    }
                }
                // the panel sits down a pixel and comes most of the way back
                float w = Mathf.Clamp01((u - 0.4f) / 0.6f);
                settleY = u < 0.4f ? 0f : -Style.TickSettle * Mathf.Sin(w * Mathf.PI);
                yield return null;
            }
            settleY = 0f;
            state.TermLeft = Mathf.Max(0, toLeft);
            termShown = state.TermLeft;
            tickingSegment = -1;
            if (closing)
            {
                // 80-120 ms of nothing. No text.
                float pause = 0f;
                while (pause < Style.LockPause)
                {
                    pause += Dt;
                    yield return null;
                }
                // the digits sit down, and stay down: the contract has closed
                Emit(Cue.ContractLock);
                float k = 0f;
                while (k < 0.12f)
                {
                    k += Dt;
                    debtNumber.Nudge(-Style.LockDrop * EaseOut(Mathf.Clamp01(k / 0.12f)));
                    yield return null;
                }
                float rest = 0f;
                while (rest < 0.12f)
                {
                    rest += Dt;
                    yield return null;
                }
            }
            running--;
        }

        private IEnumerator Settle(long bonus)
        {
            running++;
            float fromFraction = shownFraction;
            debtNumber.Nudge(0f);
            debtNumber.Roll(debtNumber.Value, 0, true, Style.SettleDrain * 0.8f);
            float t = 0f;
            while (t < Style.SettleDrain)
            {
                t += Dt;
                float u = Mathf.Clamp01(t / Style.SettleDrain);
                shownFraction = Mathf.Lerp(fromFraction, 0f, EaseInOut(u));
                undertoneBoost = -u; // the burgundy drains back to neutral charcoal
                yield return null;
            }
            shownFraction = 0f;
            Emit(Cue.Settled);
            StartCoroutine(SmallStamp(Loc.Pick("SETTLED", "KAPANDI"), Style.GoldIvory, 0.9f, null));
            if (bonus > 0)
            {
                yield return BonusToken(bonus);
            }
            float hold = 0f;
            while (hold < Style.ExitDelay)
            {
                hold += Dt;
                yield return null;
            }
            running--;
            yield return Exit();
        }

        private IEnumerator BonusToken(long bonus)
        {
            Emit(Cue.Bonus);
            Vector2 from = CentreWorld;
            Vector2 to = ScoreAnchor != null ? ScoreAnchor() : from + new Vector2(-3f, 1.5f);
            var token = new GameObject("BonusToken").transform;
            token.SetParent(transform.parent, false);
            Text(token, "Label", "+" + Money(bonus), 0.02f, Style.GoldIvory,
                Style.Order + 13, TextAnchor.MiddleCenter);
            Flecks(from, 4, Style.Amber);
            Vector2 ctrl = (from + to) * 0.5f + new Vector2(0f, 0.8f);
            float t = 0f;
            const float travel = 0.36f;
            while (t < travel)
            {
                t += Dt;
                float u = EaseInOut(Mathf.Clamp01(t / travel));
                token.position = Bezier(from, ctrl, to, u);
                float s = Mathf.Lerp(1f, 0.6f, u);
                token.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            Destroy(token.gameObject);
        }

        private IEnumerator Exit()
        {
            running++;
            Emit(Cue.Close);
            float t = 0f;
            float fromWidth = openWidth;
            while (t < Style.ExitDuration)
            {
                t += Dt;
                float u = Mathf.Clamp01(t / Style.ExitDuration);
                openWidth = Mathf.Lerp(fromWidth, 0.04f, EaseInOut(u));
                openAlpha = 1f - u;
                trimAlpha = 1f - u;
                yield return null;
            }
            running--;
            IsOpen = false;
            panel.gameObject.SetActive(false);
            peakDebt = 0;
            undertoneBoost = 0f;
            minimumShownSatisfied = false;
            sealPressed = false;
        }

        /// <summary>A small legal stamp pressed on the panel's face: 1.35 -> 0.94 -> 1, turned a
        /// few degrees, a hold, and gone. The letters are the stamp's; the ink is the tint.</summary>
        private IEnumerator SmallStamp(string text, Color ink, float hold, Cue? cue)
        {
            running++;
            var root = new GameObject("Stamp").transform;
            root.SetParent(panel, false);
            root.localPosition = new Vector3(plan.Stamp.x, plan.Stamp.y, 0f);
            root.localRotation = Quaternion.Euler(0f, 0f, Style.StampRotation);
            SpriteRenderer plate = Sprite(root, "Plate", DebtLedgerShapes.StampSmall, Style.Order + 10);
            TextMesh label = Text(root, "Label", text, 0.019f, Style.Cream, Style.Order + 11,
                TextAnchor.MiddleCenter);
            float w = Mathf.Min(TextWidth(label, text) + 0.42f, plan.W * 0.92f);
            Place(plate, Vector2.zero, w, w * 0.5f);
            bool struck = false;
            float t = 0f;
            float total = 0.18f + hold + 0.25f;
            while (t < total)
            {
                t += Dt;
                float s = t < 0.1f ? Mathf.Lerp(Style.StampFrom, Style.StampUnder, EaseOut(t / 0.1f))
                    : t < 0.18f ? Mathf.Lerp(Style.StampUnder, 1f, (t - 0.1f) / 0.08f) : 1f;
                root.localScale = new Vector3(s, s, 1f);
                if (!struck && t >= 0.1f)
                {
                    struck = true;
                    if (cue.HasValue)
                    {
                        Emit(cue.Value);
                    }
                }
                float a = t < 0.05f ? t / 0.05f : t > 0.18f + hold ? 1f - (t - 0.18f - hold) / 0.25f : 1f;
                plate.color = WithAlpha(ink, 0.94f * Mathf.Clamp01(a));
                ViewUtil.SetTextColor(label, WithAlpha(Style.Cream, Mathf.Clamp01(a)));
                yield return null;
            }
            Destroy(root.gameObject);
            running--;
        }

        // =================================================================== the clock

        private void Update()
        {
            if (!built || !IsOpen)
            {
                return;
            }
            float dt = Dt;
            clock += dt;
            debtNumber.Tick(dt);
            termNumber.Tick(dt);
            TickTension(dt);
            Paint();
        }

        /// <summary>The ink a state has earned on the trim: none until the term is two stages
        /// from its end.</summary>
        private static float InkTarget(CreditDeadline deadline)
        {
            return deadline == CreditDeadline.Warning ? 0.4f
                : deadline == CreditDeadline.FinalDue ? 0.8f : 0f;
        }

        /// <summary>What may move at all is the named state's call - and nothing moves for a
        /// state the loan is not in. One slow thing at a time, never a blink.</summary>
        private void TickTension(float dt)
        {
            CreditDeadline d = state.Deadline;
            // The ink walks in when the state changes, and then stands.
            inkShown = Mathf.MoveTowards(inkShown, InkTarget(d), Style.InkRate * dt);

            // FINAL DUE: a slow heavy beat on the debt (1 -> 1.025 -> 1)...
            if (d == CreditDeadline.FinalDue)
            {
                if (nextBeat < 0f || clock >= nextBeat + 0.34f)
                {
                    nextBeat = clock + Mathf.Lerp(Style.FinalBeatMin, Style.FinalBeatMax,
                        Hash((int)(clock * 3f)));
                }
                float b = clock - nextBeat;
                beatScale = b >= 0f && b < 0.34f
                    ? 1f + Style.FinalBeat * Mathf.Sin(b / 0.34f * Mathf.PI) : 1f;
                // ...and, rarely, a pixel or two of squeeze from both sides.
                if (nextSqueeze < 0f)
                {
                    nextSqueeze = clock + Mathf.Lerp(Style.SqueezeMin, Style.SqueezeMax, Hash(11));
                }
                if (squeezeClock < 0f && clock >= nextSqueeze)
                {
                    squeezeClock = 0f;
                    nextSqueeze = clock + Mathf.Lerp(Style.SqueezeMin, Style.SqueezeMax,
                        Hash((int)(clock * 5f) + 3));
                    Emit(Cue.Squeeze);
                }
            }
            else
            {
                beatScale = 1f;
                nextBeat = -1f;
                nextSqueeze = -1f;
            }
            if (squeezeClock >= 0f)
            {
                squeezeClock += dt;
                squeeze = Style.Squeeze * Mathf.Sin(Mathf.Clamp01(squeezeClock / Style.SqueezeTime) * Mathf.PI);
                if (squeezeClock > Style.SqueezeTime)
                {
                    squeezeClock = -1f;
                    squeeze = 0f;
                }
            }

            // WARNING: the term's number swells a few percent every few seconds, and while the
            // minimum is still owed its stop warms and cools with it.
            if (d == CreditDeadline.Warning)
            {
                if (nextSwell < 0f || clock >= nextSwell + 0.5f)
                {
                    nextSwell = clock + Mathf.Lerp(Style.TermSwellMin, Style.TermSwellMax,
                        Hash((int)(clock * 7f)));
                }
                float w = clock - nextSwell;
                float k = w >= 0f && w < 0.5f ? Mathf.Sin(w / 0.5f * Mathf.PI) : 0f;
                termSwell = 1f + Style.TermSwell * k;
                minPulse = k;
            }
            else
            {
                termSwell = 1f;
                minPulse = 0f;
                nextSwell = -1f;
            }

            // PRESSURE: now and then a faint shimmer at the band's right end. Never a pulse.
            if (d == CreditDeadline.Pressure)
            {
                if (nextShimmer < 0f)
                {
                    nextShimmer = clock + Mathf.Lerp(Style.ShimmerMin, Style.ShimmerMax, Hash(5));
                }
                if (shimmerClock < 0f && clock >= nextShimmer)
                {
                    shimmerClock = 0f;
                    nextShimmer = clock + Mathf.Lerp(Style.ShimmerMin, Style.ShimmerMax,
                        Hash((int)(clock * 9f)));
                }
            }
            else
            {
                nextShimmer = -1f;
            }
            if (shimmerClock >= 0f)
            {
                shimmerClock += dt;
                if (shimmerClock > 0.8f)
                {
                    shimmerClock = -1f;
                }
            }

            // SAFE: a very faint brass sheen every six to ten seconds, and nothing else.
            if (d == CreditDeadline.Safe)
            {
                if (nextSheen < 0f)
                {
                    nextSheen = clock + Mathf.Lerp(Style.SheenMin, Style.SheenMax, Hash(7));
                }
                if (sheenClock < 0f && clock >= nextSheen)
                {
                    sheenClock = 0f;
                    nextSheen = clock + Mathf.Lerp(Style.SheenMin, Style.SheenMax,
                        Hash((int)(clock * 5f)));
                }
            }
            else
            {
                nextSheen = -1f;
            }
            if (sheenClock >= 0f)
            {
                sheenClock += dt;
                if (sheenClock > 0.9f)
                {
                    sheenClock = -1f;
                }
            }
        }

        private void Paint()
        {
            int stage = (int)state.Deadline;
            // ONE writer of the panel's transform: the opening's width, the interest's breath and
            // the final stage's squeeze on x; the step forward on both; the tick's settle on y.
            float fwd = 1f + (Style.ForwardScale - 1f) * forward;
            panel.localScale = new Vector3(openWidth * (1f + scalePulse - squeeze) * fwd, fwd, 1f);
            panel.localPosition = new Vector3(0f, settleY, 0f);

            // The burgundy follows the CONTINUOUS pressure - a loan paid down goes back toward
            // charcoal - and the carry's brightening lifts the whole face for a moment.
            Color undertone = Color.Lerp(Style.Charcoal, Style.Undertone,
                Mathf.Clamp01(tension * 0.85f + undertoneBoost));
            undertone = Color.Lerp(undertone, new Color(0.3f, 0.2f, 0.19f), brighten * 0.45f);
            body.color = WithAlpha(undertone, openAlpha);
            // charcoal + brass -> wine + brass -> deep burgundy + dark brass
            Color trimColour = Color.Lerp(Style.Brass, Style.BrassDark,
                stage >= 4 ? 0.6f : stage == 3 ? 0.3f : 0f);
            trimColour = Color.Lerp(trimColour, Style.GoldIvory, brighten * 0.4f);
            trim.color = WithAlpha(trimColour, trimAlpha * 0.9f);
            // the debt's own mark on the panel's head - the TOTAL's underline wears the same
            Place(notch, plan.Notch, 0.34f, 0.04f);
            notch.color = WithAlpha(Style.Notch, form == Form.Legacy ? 0f : trimAlpha);
            Place(chip, plan.Chip, plan.ChipSize.x, plan.ChipSize.y);
            chip.color = WithAlpha(Style.Brass, trimAlpha * 0.85f);
            Place(openSeal, plan.Seal, plan.SealSize * sealScale, plan.SealSize * sealScale);
            if (sealPressed)
            {
                openSeal.color = WithAlpha(Style.Burgundy, 0.95f * openAlpha);
            }

            PaintFacts(stage);
            PaintBand();
            PaintMinimum();

            // the zones' ticks: spacing does the separating, these only mark where
            tickA.color = Color.clear;
            tickB.color = Color.clear;
            if (plan.Ticks)
            {
                Place(tickA, new Vector2(plan.Tick1, plan.TickY), 0.012f, plan.TickHeight);
                Place(tickB, new Vector2(plan.Tick2, plan.TickY), 0.012f, plan.TickHeight);
                tickA.color = WithAlpha(Style.BrassDim, 0.45f * trimAlpha);
                tickB.color = WithAlpha(Style.BrassDim, 0.45f * trimAlpha);
            }

            // ink up the trim (it arrived when the state changed and stands since); the
            // foreclosure seal on the border at the last stage
            float inset = DebtLedgerShapes.TrimInset;
            Place(inkBottom, new Vector2(-W * 0.5f + inset + W * 0.45f * inkShown, -H * 0.5f + inset),
                W * 0.9f * inkShown, 0.024f);
            inkBottom.color = WithAlpha(Style.Burgundy, inkShown > 0f ? 0.8f * trimAlpha : 0f);
            Place(inkLeft, new Vector2(-W * 0.5f + inset, -H * 0.5f + inset + H * 0.42f * inkShown),
                0.024f, H * 0.84f * inkShown);
            inkLeft.color = WithAlpha(Style.Burgundy, inkShown > 0f ? 0.7f * trimAlpha : 0f);
            Place(inkRight, new Vector2(W * 0.5f - inset, -H * 0.5f + inset + H * 0.42f * inkShown),
                0.024f, H * 0.84f * inkShown);
            inkRight.color = WithAlpha(Style.Burgundy, inkShown > 0f ? 0.7f * trimAlpha : 0f);
            Place(dueSeal, new Vector2(W * 0.5f - 0.09f, H * 0.5f - 0.09f), 0.13f, 0.13f);
            dueSeal.color = WithAlpha(Style.Burgundy, stage >= 4 ? 0.95f * openAlpha : 0f);

            // SAFE's sheen across the face; PRESSURE's shimmer on the band's right end
            if (sheenClock >= 0f)
            {
                float u = Mathf.Clamp01(sheenClock / 0.9f);
                Place(sheen, new Vector2(Mathf.Lerp(-W * 0.5f, W * 0.5f, u), 0f), 0.4f, H * 0.95f);
                sheen.color = WithAlpha(Style.Brass, 0.07f * Mathf.Sin(u * Mathf.PI) * openAlpha);
            }
            else
            {
                sheen.color = Color.clear;
            }
            if (shimmerClock >= 0f)
            {
                float u = Mathf.Clamp01(shimmerClock / 0.8f);
                Place(shimmer, new Vector2(plan.BarLeft + plan.BarLength - 0.1f, plan.BarY),
                    0.3f, plan.BarHeight * 2.2f);
                shimmer.color = WithAlpha(Style.Burgundy, 0.22f * Mathf.Sin(u * Mathf.PI) * openAlpha);
            }
            else
            {
                shimmer.color = Color.clear;
            }

            PaintTrack(stage);
            PaintDebug();
        }

        /// <summary>The four facts, each in its zone.</summary>
        private void PaintFacts(int stage)
        {
            Color label = WithAlpha(Style.BrassDim, trimAlpha);
            PlaceText(debtLabel, plan.DebtLabel, plan.LabelScale);
            ViewUtil.SetTextColor(debtLabel, label);

            // The term's unit and width first: in the stacked form the debt has whatever room
            // the term leaves on their shared row.
            int shown = termShown >= 0 ? termShown : Mathf.Max(0, state.TermLeft);
            SetText(termUnit, plan.Compact ? " "
                : shown == 1 ? Loc.Pick("STAGE", "AŞAMA") : Loc.Pick("STAGES", "AŞAMA"));
            float unitWidth = TextWidth(termUnit, termUnit.text);
            float termPair = termNumber.NaturalWidth * plan.TermScale + 0.05f + unitWidth;
            float termFit = plan.Compact || termPair <= plan.TermMax ? 1f : plan.TermMax / termPair;

            // THE DEBT: warm ivory, heavy, fitted to its zone - a seven-figure debt shrinks
            // rather than runs into the term. It warms toward cream-red only as the term runs
            // out, and takes a breath of muted red when interest lands on it.
            float debtMax = plan.DebtBesideTerm ? plan.DebtMax - termPair * termFit - 0.12f : plan.DebtMax;
            float width = debtNumber.NaturalWidth * plan.DebtScale;
            float fit = width > debtMax ? debtMax / width : 1f;
            debtNumber.SetOrigin(plan.Debt);
            debtNumber.SetScale(plan.DebtScale * fit * beatScale);
            Color debtColour = Color.Lerp(Style.Cream, Style.CreamWarm,
                stage >= 4 ? 0.7f : stage == 3 ? 0.35f : 0f);
            debtColour = Color.Lerp(debtColour, Style.MutedRed, numberFlash);
            debtNumber.SetColour(WithAlpha(debtColour, openAlpha));

            // THE MINIMUM
            bool hasMinimum = state.MinimumDue > 0;
            bool paid = minimumShownSatisfied && state.InRound;
            Color minColour = paid ? Style.Brass : Style.Cream;
            SetText(minLabel, !hasMinimum ? " "
                : state.InRound ? Loc.Pick("MINIMUM", "ASGARİ") : Loc.Pick("NEXT MINIMUM", "SONRAKİ ASGARİ"));
            SetText(minValue, hasMinimum ? Money(state.MinimumDue) : " ");
            ViewUtil.SetTextColor(minLabel, label);
            ViewUtil.SetTextColor(minValue, WithAlpha(minColour, trimAlpha));
            bool showProgress = hasMinimum && state.InRound && !plan.Compact && Layers.ShowMinimumProgress;
            SetText(minProgress, showProgress
                ? Money(Math.Min(state.MinimumPaid, state.MinimumDue)) + " / " + Money(state.MinimumDue)
                : " ");
            PlaceText(minProgress, plan.MinProgress, 1f);
            ViewUtil.SetTextColor(minProgress, WithAlpha(paid ? Style.Brass : Style.BrassDim, trimAlpha));

            // THE TERM: a number the corner of an eye can find, and its real unit - stages.
            termNumber.SetScale(plan.TermScale * termFit * termSwell);
            Color termColour = stage >= 4 ? Color.Lerp(Style.Cream, Style.MutedRed, 0.45f)
                : stage == 3 ? Style.CreamWarm : Style.Cream;
            termNumber.SetColour(WithAlpha(termColour, trimAlpha));
            ViewUtil.SetTextColor(termUnit, WithAlpha(termColour, trimAlpha * 0.9f));
            ViewUtil.SetTextColor(termLabel, label);

            // THE RATE - or, while interest is landing, what it just cost
            bool entry = interestEntryK > 0.001f && interestEntry > 0;
            SetText(rateText, entry ? "+" + Money(interestEntry) : Rate(state.InterestPermille));
            ViewUtil.SetTextColor(rateText, entry
                ? WithAlpha(Style.MutedRed, trimAlpha * Mathf.Max(0.35f, interestEntryK))
                : WithAlpha(Style.BrassDim, trimAlpha));
            ViewUtil.SetTextColor(rateLabel, label);

            if (plan.Compact)
            {
                // LEGACY: three "LABEL value" lines at the right edge, as the first pass had them.
                const float gap = 0.045f;
                PlaceText(minValue, plan.MinValue, plan.MinScale);
                PlaceText(minLabel, plan.MinValue - new Vector2(
                    TextWidth(minValue, minValue.text) * plan.MinScale + gap, 0f), plan.MinLabelScale);
                termNumber.SetOrigin(plan.Term);
                PlaceText(termLabel, plan.Term - new Vector2(
                    termNumber.NaturalWidth * plan.TermScale + gap, 0f), plan.TermLabelScale);
                PlaceText(termUnit, plan.Term, 1f);
                PlaceText(rateText, plan.Rate, plan.RateScale);
                PlaceText(rateLabel, plan.Rate - new Vector2(
                    TextWidth(rateText, rateText.text) * plan.RateScale + gap, 0f), plan.RateLabelScale);
                return;
            }
            // "SONRAKİ ASGARİ" is the one label long enough to need fitting
            float minLabelWidth = TextWidth(minLabel, minLabel.text);
            PlaceText(minLabel, plan.MinLabel, minLabelWidth > plan.MinLabelMax
                ? plan.MinLabelMax / minLabelWidth : 1f);
            PlaceText(minValue, plan.MinValue, 1f);
            PlaceText(termLabel, plan.TermLabel, 1f);
            // the unit sits on the number's baseline, to its right
            PlaceText(termUnit, plan.Term + new Vector2(0f, -0.035f), termFit);
            termNumber.SetOrigin(plan.Term - new Vector2((unitWidth + 0.05f) * termFit, 0f));
            PlaceText(rateText, plan.Rate, plan.RateScale);
            PlaceText(rateLabel, plan.RateBeside
                ? plan.Rate - new Vector2(TextWidth(rateText, rateText.text) * plan.RateScale + 0.06f, 0f)
                : plan.RateLabel, 1f);
        }

        /// <summary>THE LEDGER BAND: the largest the loan has been as a dark groove, what is
        /// still owed as a deep burgundy fill, a brass cap on its end and a dim tick where this
        /// stage's minimum ends.</summary>
        private void PaintBand()
        {
            float h = plan.BarHeight;
            Vector2 centre = new Vector2(plan.BarLeft + plan.BarLength * 0.5f, plan.BarY);
            Place(groove, centre, plan.BarLength + 0.02f, h + 0.02f);
            groove.color = WithAlpha(Style.Graphite, openAlpha);
            float fraction = Mathf.Clamp01(shownFraction);
            float fillLen = plan.BarLength * fraction;
            Place(fill, new Vector2(plan.BarLeft + fillLen * 0.5f, plan.BarY), fillLen, h);
            fill.color = WithAlpha(Style.Wine, openAlpha);
            bool legacy = form == Form.Legacy;
            Place(fillEnd, new Vector2(plan.BarLeft + fillLen + 0.02f, plan.BarY), 0.05f, h);
            fillEnd.color = WithAlpha(Style.Wine, legacy && fillLen > 0.01f ? openAlpha : 0f);
            Place(fillHighlight, new Vector2(plan.BarLeft + fillLen * 0.5f, plan.BarY + h * 0.4f),
                fillLen, 0.012f);
            fillHighlight.color = WithAlpha(Style.Burgundy, 0.55f * openAlpha);
            // a payment's brief amber edge, on the end the debt is leaving from
            float edge = Mathf.Min(fillLen, 0.16f);
            Place(fillEdge, new Vector2(plan.BarLeft + fillLen - edge * 0.5f, plan.BarY), edge, h);
            fillEdge.color = WithAlpha(Style.Amber, 0.75f * edgeFlash * openAlpha);
            // the current debt's marker: a small brass cap, not an orb
            Place(fillCap, new Vector2(plan.BarLeft + fillLen, plan.BarY), 0.04f, h * 1.5f);
            fillCap.color = WithAlpha(Style.Brass, !legacy && fillLen > 0.005f ? openAlpha : 0f);

            float markerFraction = MarkerFraction();
            bool hasMinimum = state.MinimumDue > 0 && markerFraction >= 0f;
            float markerX = plan.BarLeft + plan.BarLength * Mathf.Clamp01(markerFraction);
            Place(marker, new Vector2(markerX, plan.BarY), 0.014f, h * (legacy ? 1.9f : 1.25f));
            marker.color = WithAlpha(minimumShownSatisfied ? Style.Brass : Style.BrassDim,
                hasMinimum && Layers.ShowMinimumMarker ? openAlpha : 0f);
        }

        /// <summary>THE MINIMUM'S TRACK: deep graphite, a muted amber for what is paid, a brass
        /// stop at its end. A small ledger track, never a health bar.</summary>
        private void PaintMinimum()
        {
            bool show = state.MinimumDue > 0 && state.InRound && !plan.Compact && Layers.ShowMinimumProgress;
            if (!show)
            {
                minGroove.color = Color.clear;
                minFill.color = Color.clear;
                minStop.color = Color.clear;
                return;
            }
            float h = plan.MinTrackHeight;
            float length = plan.MinTrackLength;
            if (plan.MinProgressAnchor == TextAnchor.MiddleRight)
            {
                // the stacked form: the track runs up to the "312 / 648" at its right, never under it
                float room = plan.MinProgress.x - plan.MinTrackLeft
                    - TextWidth(minProgress, minProgress.text) - 0.1f;
                length = Mathf.Clamp(room, 0.35f, length);
            }
            float paid = minimumShownSatisfied ? 1f
                : Mathf.Clamp01(state.MinimumPaid / (float)Math.Max(1L, state.MinimumDue));
            Place(minGroove, new Vector2(plan.MinTrackLeft + length * 0.5f, plan.MinTrackY),
                length + 0.02f, h + 0.02f);
            minGroove.color = WithAlpha(Style.Graphite, openAlpha);
            Place(minFill, new Vector2(plan.MinTrackLeft + length * paid * 0.5f, plan.MinTrackY),
                length * paid, h);
            minFill.color = WithAlpha(Style.AmberMuted, openAlpha);
            Place(minStop, new Vector2(plan.MinTrackLeft + length, plan.MinTrackY), 0.04f, h * 1.6f);
            // Unpaid it is dim; at WARNING it warms and cools slowly; paid, it is brass - and it
            // flashes ivory-gold on the click.
            Color stop = minimumShownSatisfied ? Style.Brass
                : Color.Lerp(Style.BrassDim, Style.Amber, minPulse * 0.6f);
            if (stopFlashK > 0f)
            {
                stop = stopFlash;
            }
            minStop.color = WithAlpha(stop, openAlpha);
        }

        /// <summary>THE MATURITY TRACK: one embossed segment per stage of the term, across the
        /// whole panel. Ahead is brass (warmer as the end comes), spent is charcoal, and the one
        /// being spent goes through dark burgundy on its way.</summary>
        private void PaintTrack(int stage)
        {
            int total = Mathf.Max(0, state.TermTotal);
            bool legacy = form == Form.Legacy;
            Sprite shape = legacy ? DebtLedgerShapes.Notch : DebtLedgerShapes.Segment;
            while (segments.Count < total)
            {
                segments.Add(Sprite(panel, "Segment" + segments.Count, shape, Style.Order + 3));
            }
            int left = termShown >= 0 ? termShown : state.TermLeft;
            int spent = Mathf.Clamp(total - left, 0, total);
            const float gap = 0.05f;
            float each = total > 0 ? (plan.TrackLength - gap * (total - 1)) / total : 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                SpriteRenderer n = segments[i];
                if (i >= total || !Layers.ShowMaturityTrack)
                {
                    n.color = Color.clear;
                    continue;
                }
                n.sprite = shape;
                if (legacy)
                {
                    const float spacing = 0.13f;
                    Place(n, new Vector2(-(total - 1) * spacing * 0.5f + i * spacing, plan.TrackY),
                        0.09f, plan.TrackHeight);
                }
                else
                {
                    Place(n, new Vector2(plan.TrackLeft + each * 0.5f + i * (each + gap), plan.TrackY),
                        each, plan.TrackHeight);
                }
                // The end of the term warms what is left of it: two segments, then one.
                Color ahead = stage >= 4 ? Color.Lerp(Style.Amber, Style.Crimson, 0.35f)
                    : stage == 3 ? Color.Lerp(Style.Brass, Style.Amber, 0.6f) : Style.Brass;
                Color c = i < spent ? Style.Spent : ahead;
                if (i == tickingSegment)
                {
                    c = tickingK < 0.45f ? Color.Lerp(ahead, Style.Burgundy, tickingK / 0.45f)
                        : Color.Lerp(Style.Burgundy, Style.Spent, (tickingK - 0.45f) / 0.55f);
                }
                n.color = WithAlpha(c, trimAlpha);
            }
        }

        private void PaintDebug()
        {
            bool show = Layers.ShowDebtPanelBounds;
            for (int i = 0; i < bounds.Count; i++)
            {
                SpriteRenderer b = bounds[i];
                const float line = 0.012f;
                switch (i)
                {
                    case 0: Place(b, new Vector2(0f, H * 0.5f), W, line); break;
                    case 1: Place(b, new Vector2(0f, -H * 0.5f), W, line); break;
                    case 2: Place(b, new Vector2(-W * 0.5f, 0f), line, H); break;
                    default: Place(b, new Vector2(W * 0.5f, 0f), line, H); break;
                }
                b.color = show ? new Color(0.4f, 1f, 0.9f, 0.85f) : Color.clear;
            }
            var sb = new System.Text.StringBuilder();
            if (Layers.ShowDebtValue)
            {
                sb.Append("debt ").Append(state.Debt).Append("  stageStart ").Append(state.StageStartDebt)
                    .Append("  min ").Append(state.MinimumPaid).Append('/').Append(state.MinimumDue);
            }
            if (Layers.ShowDeadlineState)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("deadline ").Append(state.Deadline).Append("  term ").Append(state.TermLeft)
                    .Append('/').Append(state.TermTotal);
            }
            if (Layers.ShowMaturityProgress)
            {
                if (sb.Length > 0) sb.Append('\n');
                int total = Mathf.Max(0, state.TermTotal);
                sb.Append("maturity ").Append(Mathf.Clamp(total - state.TermLeft, 0, total))
                    .Append(" spent / ").Append(total).Append("  ink ").Append(inkShown.ToString("0.00"));
            }
            if (Layers.ShowInterestPreview)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(Loc.Pick("next interest +", "sonraki faiz +")).Append(Money(state.NextInterest));
            }
            if (Layers.ShowResponsiveLayout)
            {
                if (sb.Length > 0) sb.Append('\n');
                // what the player actually gets: the panel and its digits in screen pixels
                float unit = transform.lossyScale.x * PixelsPerWorld();
                sb.Append(form).Append("  ").Append(Mathf.RoundToInt(W * unit)).Append(" x ")
                    .Append(Mathf.RoundToInt(H * unit)).Append(" px   digits ")
                    .Append(Mathf.RoundToInt(DigitHeight(Style.DebtSize) * plan.DebtScale * unit))
                    .Append(" / ").Append(Mathf.RoundToInt(DigitHeight(Style.MinimumSize) * plan.MinScale * unit))
                    .Append(" / ").Append(Mathf.RoundToInt(DigitHeight(Style.TermSize) * plan.TermScale * unit))
                    .Append(" px");
            }
            devText.transform.localPosition = new Vector3(0f, H * 0.5f + 0.04f, 0f);
            SetText(devText, (UnityEngine.Debug.isDebugBuild || Application.isEditor) ? sb.ToString() : " ");
        }

        /// <summary>How tall a digit is drawn for a character size, in the panel's own units: a
        /// line is 9 * size, and this face's digits stand about 0.7 of it.</summary>
        public static float DigitHeight(float characterSize)
        {
            return characterSize * 9f * 0.7f;
        }

        private static float PixelsPerWorld()
        {
            Camera cam = Camera.main;
            return cam != null && cam.orthographicSize > 0.001f
                ? cam.pixelHeight / (2f * cam.orthographicSize) : 108f;
        }

        /// <summary>Where the minimum tick stands on the band, as a share of it: in a round,
        /// where the fill will be once the stage's minimum is paid; in the market, the same for
        /// the next stage. -1 when there is no minimum to mark.</summary>
        private float MarkerFraction()
        {
            if (state.MinimumDue <= 0 || peakDebt <= 0)
            {
                return -1f;
            }
            long start = state.InRound && state.StageStartDebt > 0 ? state.StageStartDebt : state.Debt;
            return Mathf.Clamp01((start - state.MinimumDue) / (float)peakDebt);
        }

        private float Fraction(long debt)
        {
            return peakDebt > 0 ? Mathf.Clamp01(debt / (float)peakDebt) : 0f;
        }

        // =================================================================== the odometer

        /// <summary>A number whose digits slide - DOWN when the value falls, UP when it rises -
        /// counting through on the way, so the direction itself says better or worse.</summary>
        private sealed class Odometer
        {
            private readonly TextMesh a;
            private readonly TextMesh b;
            private readonly float lineHeight;
            private Vector2 origin;
            private float t = -1f;
            private float duration;
            private long from;
            private long to;
            private bool down;
            private Color colour = Color.white;
            private float nudge;
            private float scale = 1f;

            public long Value { get; private set; }

            public Odometer(Transform parent, string name, float size, int order, float line,
                TextAnchor anchor, bool bold)
            {
                a = Text(parent, name + "A", "0", size, Color.white, order, anchor);
                b = Text(parent, name + "B", "0", size, Color.white, order, anchor);
                if (bold)
                {
                    Embolden(a);
                    Embolden(b);
                }
                SetText(b, " "); // only ever shows mid-roll
                lineHeight = line;
            }

            /// <summary>The width of the settled number at scale 1, in the panel's units.</summary>
            public float NaturalWidth
            {
                get { return TextWidth(a, Money(Value)); }
            }

            public void SetOrigin(Vector2 at)
            {
                origin = at;
                Layout(t < 0f ? 0f : Mathf.Clamp01(t / duration));
            }

            public void Snap(long value)
            {
                Value = value;
                t = -1f;
                SetText(a, Money(value));
                SetText(b, " ");
                Layout(0f);
            }

            public void Roll(long start, long end, bool slideDown, float seconds)
            {
                from = start;
                to = end;
                down = slideDown;
                duration = Mathf.Max(0.01f, seconds);
                t = 0f;
                SetText(a, Money(start));
                Value = end;
            }

            public void Nudge(float y)
            {
                nudge = y;
                Layout(t < 0f ? 0f : Mathf.Clamp01(t / duration));
            }

            public void SetScale(float s)
            {
                scale = s;
                a.transform.localScale = new Vector3(s, s, 1f);
                b.transform.localScale = new Vector3(s, s, 1f);
            }

            public void SetColour(Color c)
            {
                colour = c;
                if (t < 0f)
                {
                    ViewUtil.SetTextColor(a, c);
                }
            }

            public void Tick(float dt)
            {
                if (t < 0f)
                {
                    return;
                }
                t += dt;
                float u = Mathf.Clamp01(t / duration);
                long shown = (long)Mathf.Lerp(from, to, EaseOut(u));
                SetText(b, Money(u >= 1f ? to : shown));
                Layout(u);
                if (u >= 1f)
                {
                    t = -1f;
                    SetText(a, Money(to));
                    SetText(b, " ");
                    Layout(0f);
                }
            }

            private void Layout(float u)
            {
                float dir = down ? -1f : 1f;
                float e = EaseOut(u);
                float line = lineHeight * scale;
                if (t < 0f)
                {
                    a.transform.localPosition = new Vector3(origin.x, origin.y + nudge, 0f);
                    ViewUtil.SetTextColor(a, colour);
                    return;
                }
                // the old number leaves in the direction of travel, the new one arrives from the
                // other side
                a.transform.localPosition = new Vector3(origin.x, origin.y + nudge + dir * line * e, 0f);
                b.transform.localPosition = new Vector3(origin.x, origin.y + nudge - dir * line * (1f - e), 0f);
                ViewUtil.SetTextColor(a, WithAlpha(colour, 1f - e));
                ViewUtil.SetTextColor(b, WithAlpha(colour, e));
            }
        }

        // =================================================================== plumbing

        private float Dt
        {
            get { return Time.deltaTime * PlaybackRate; }
        }

        private void Emit(Cue cue)
        {
            if (Sounded != null)
            {
                Sounded(cue);
            }
        }

        private void Flecks(Vector2 at, int count, Color colour)
        {
            for (int i = 0; i < count; i++)
            {
                StartCoroutine(Fleck(at, colour, i));
            }
        }

        private IEnumerator Fleck(Vector2 at, Color colour, int i)
        {
            var go = new GameObject("LedgerFleck");
            go.transform.SetParent(transform.parent, false);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = DebtLedgerShapes.Fleck;
            r.sortingOrder = Style.Order + 12;
            float angle = (0.3f + 0.4f * Hash(i * 13 + 5)) * Mathf.PI;
            Vector2 v = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (0.9f + 0.6f * Hash(i * 7 + 1));
            Vector2 p = at;
            float t = 0f;
            float life = 0.32f + 0.1f * Hash(i + 3);
            float spin = (Hash(i * 3 + 2) - 0.5f) * 600f;
            while (t < life)
            {
                float dt = Dt;
                t += dt;
                v *= Mathf.Exp(-4f * dt);
                v += Vector2.down * 2.2f * dt;
                p += v * dt;
                go.transform.position = new Vector3(p.x, p.y, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, spin * t);
                float s = 0.045f * (1f - 0.4f * t / life);
                go.transform.localScale = new Vector3(s / r.sprite.bounds.size.x, s / r.sprite.bounds.size.y, 1f);
                r.color = WithAlpha(colour, 1f - t / life);
                yield return null;
            }
            Destroy(go);
        }

        private static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = Color.clear;
            return r;
        }

        internal static TextMesh Text(Transform parent, string name, string text, float size,
            Color colour, int order, TextAnchor anchor)
        {
            return ViewUtil.MakeText3D(parent, name, Vector2.zero, string.IsNullOrEmpty(text) ? " " : text,
                90, size, colour, order, anchor);
        }

        /// <summary>Sets a world text AND its outline copies (MakeText3D's children).</summary>
        internal static void SetText(TextMesh text, string value)
        {
            if (text == null)
            {
                return;
            }
            string v = string.IsNullOrEmpty(value) ? " " : value;
            if (text.text == v)
            {
                return;
            }
            text.text = v;
            for (int i = 0; i < text.transform.childCount; i++)
            {
                TextMesh copy = text.transform.GetChild(i).GetComponent<TextMesh>();
                if (copy != null)
                {
                    copy.text = v;
                }
            }
        }

        /// <summary>Re-anchors a world text and its outline copies.</summary>
        private static void SetAnchor(TextMesh text, TextAnchor anchor)
        {
            if (text.anchor == anchor)
            {
                return;
            }
            text.anchor = anchor;
            for (int i = 0; i < text.transform.childCount; i++)
            {
                TextMesh copy = text.transform.GetChild(i).GetComponent<TextMesh>();
                if (copy != null)
                {
                    copy.anchor = anchor;
                }
            }
        }

        /// <summary>Puts a world text and its outline copies in the BOLD cut - the real one, not
        /// a dynamic font told to smear itself.</summary>
        internal static void Embolden(TextMesh text)
        {
            Font font = ViewUtil.UiFontBold;
            if (font == null)
            {
                return;
            }
            text.font = font;
            text.GetComponent<MeshRenderer>().material = font.material;
            for (int i = 0; i < text.transform.childCount; i++)
            {
                TextMesh copy = text.transform.GetChild(i).GetComponent<TextMesh>();
                if (copy != null)
                {
                    copy.font = font;
                    copy.GetComponent<MeshRenderer>().material = font.material;
                }
            }
        }

        private static void PlaceText(TextMesh text, Vector2 at, float scale)
        {
            text.transform.localPosition = new Vector3(at.x, at.y, 0f);
            text.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>How wide a string is set in a text's own face and size, in the text's parent
        /// units at scale 1 - measured off the font's advances, so it is right on the frame the
        /// string changes rather than a frame later.</summary>
        internal static float TextWidth(TextMesh text, string s)
        {
            if (text == null || string.IsNullOrEmpty(s) || text.font == null)
            {
                return 0f;
            }
            Font font = text.font;
            font.RequestCharactersInTexture(s, text.fontSize, FontStyle.Normal);
            float px = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                CharacterInfo info;
                if (font.GetCharacterInfo(s[i], out info, text.fontSize))
                {
                    px += info.advance;
                }
            }
            return px * text.characterSize / 10f;
        }

        internal static void Place(SpriteRenderer r, Vector2 at, float w, float h)
        {
            if (r == null || r.sprite == null)
            {
                return;
            }
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            Vector2 unit = r.sprite.bounds.size;
            r.transform.localScale = new Vector3(Mathf.Max(0f, w) / Mathf.Max(unit.x, 1e-4f),
                Mathf.Max(0f, h) / Mathf.Max(unit.y, 1e-4f), 1f);
        }

        /// <summary>A money figure with grouped thousands ("2 000").</summary>
        public static string Money(long value)
        {
            string s = Math.Abs(value).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ');
            return value < 0 ? "-" + s : s;
        }

        /// <summary>A rate in tenths of a percent, as the language writes it ("%12,5" / "12.5%").</summary>
        public static string Rate(int permille)
        {
            string whole = (permille / 10).ToString(CultureInfo.InvariantCulture);
            string tenth = permille % 10 != 0 ? Loc.Pick(".", ",") + (permille % 10) : string.Empty;
            return Loc.Pick(whole + tenth + "%", "%" + whole + tenth);
        }

        internal static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        internal static float EaseOut(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - (1f - k) * (1f - k);
        }

        internal static float EaseInOut(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        internal static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private static float Hash(int key)
        {
            unchecked
            {
                uint h = (uint)(key * 73856093) ^ 0x9E3779B9u;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
