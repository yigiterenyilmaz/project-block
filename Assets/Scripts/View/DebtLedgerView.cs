// PURPOSE: "Kredi kartı" - the BLACK LEDGER (KARA DEFTER): the debt contract panel that stands on
// screen for as long as a loan is open, and every event of the loan told on it. A rare joker gets a
// screen element of its own; a debt is not a number squeezed into a status line.
//
// THE PANEL is a thin, wide statement strip (about 3.2:1) beside the board - never over the grid -
// in near-black charcoal with a burgundy undertone, ONE antique-brass trim, a stylized card-chip
// motif on its left, and four facts in a strict hierarchy: the DEBT (hero, heaviest), the MINIMUM
// this stage owes, the TERM left, the RATE (smallest). Under them the LEDGER BAND - a sunken groove
// with a wine fill whose end is clipped at an angle - carries a brass NOTCH where the stage's
// minimum payment ends; the part of the fill between the notch and its end is the minimum still
// owed, and it warms from burgundy toward amber-brass as it is paid. Along the bottom edge the
// MATURITY TRACK: one embossed document tab per stage of the term, brass while unused, burgundy
// while in use, charcoal once spent, and the last one amber-red when it is the last.
//
// THE MOTION SAYS WHICH WAY THE BOOKS MOVED. A payment's digits slide DOWN, interest's slide UP,
// and nothing pops. A payment is a cream/brass chip that flies in from where the money came from
// and is absorbed (1 -> 0.4 -> 0) as the band retracts; payments landing within 150 ms are one
// chip. Interest is red ink creeping in from the panel's right edge, a "FAİZ +X" line, the band
// growing back, a scaleX of 1.008 and a darker undertone for a breath - never a shake. Opening is
// a contract being laid down (a dark sliver widening, the trim closing 80 ms behind it, the number
// rolling in, a burgundy seal pressed on); settling is the band draining, the undertone going
// neutral, a warm KAPANDI seal, and - when the bank paid a bonus - a warm token to the TOTAL.
//
// TENSION is the term, and it is Core's (GameSession.CreditDeadline): SAFE is still; PRESSURE lets
// the burgundy through; WARNING creeps ink up the rim and breathes a slow pulse along the trim every
// few seconds; FINAL DUE puts a foreclosure seal on the border, turns the last tab amber-red and
// gives the debt a slow heavy beat. No flash anywhere, no confetti, no siren.
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
        // =================================================================== tuning
        public static class Style
        {
            public static float Width = 2.3f;
            public static float Height = 0.72f;
            public static int Order = 60;

            public static readonly Color Charcoal = new Color(0.085f, 0.078f, 0.085f);
            public static readonly Color Undertone = new Color(0.2f, 0.055f, 0.08f);
            public static readonly Color Brass = new Color(0.69f, 0.56f, 0.34f);
            public static readonly Color BrassDim = new Color(0.36f, 0.3f, 0.2f);
            public static readonly Color Cream = new Color(0.97f, 0.92f, 0.82f);
            public static readonly Color CreamWarm = new Color(1f, 0.82f, 0.72f);
            public static readonly Color Wine = new Color(0.36f, 0.06f, 0.1f);
            public static readonly Color Burgundy = new Color(0.5f, 0.1f, 0.14f);
            public static readonly Color Amber = new Color(0.88f, 0.62f, 0.3f);
            public static readonly Color Graphite = new Color(0.045f, 0.045f, 0.05f);
            public static readonly Color Crimson = new Color(0.64f, 0.17f, 0.18f);
            public static readonly Color GoldIvory = new Color(1f, 0.9f, 0.62f);
            public static readonly Color Ink = new Color(0.16f, 0.06f, 0.07f);

            // the four facts, character size at font 90 (TextMesh: world height ~ 9 * size)
            public static float DebtSize = 0.024f;
            public static float LabelSize = 0.0085f;
            public static float MinimumSize = 0.0125f;
            public static float TermSize = 0.0108f;
            public static float RateSize = 0.0086f;

            public static float OpenDuration = 0.21f;
            public static float TrimDelay = 0.08f;
            public static float OpenRoll = 0.14f;
            public static float PaymentTravel = 0.23f;
            public static float PaymentRoll = 0.22f;
            public static float Coalesce = 0.15f;
            public static float InterestCreep = 0.3f;
            public static float InterestRoll = 0.22f;
            public static float SettleDrain = 0.32f;
            public static float ExitDelay = 0.4f;
            public static float ExitDuration = 0.25f;

            public static float WarningPulseMin = 3f;
            public static float WarningPulseMax = 5f;
            public static float FinalBeatMin = 1.8f;
            public static float FinalBeatMax = 2.5f;
            public static float SheenMin = 5f;
            public static float SheenMax = 8f;
        }

        /// <summary>The lab's switches. The FEATURES (marker, progress, track) are on by default;
        /// the OVERLAYS (bounds, raw values, deadline state, interest preview) are off.</summary>
        public static class Layers
        {
            public static bool ShowDebtPanelBounds;
            public static bool ShowDebtValue;
            public static bool ShowMinimumMarker = true;
            public static bool ShowMinimumProgress = true;
            public static bool ShowMaturityTrack = true;
            public static bool ShowDeadlineState;
            public static bool ShowInterestPreview;

            public static void Reset()
            {
                ShowDebtPanelBounds = false;
                ShowDebtValue = false;
                ShowMinimumMarker = true;
                ShowMinimumProgress = true;
                ShowMaturityTrack = true;
                ShowDeadlineState = false;
                ShowInterestPreview = false;
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
            FinalDue,
            Settled,
            Bonus,
            Close
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

        // =================================================================== state

        private Transform panel;
        private SpriteRenderer body;
        private SpriteRenderer trim;
        private SpriteRenderer chip;
        private SpriteRenderer groove;
        private SpriteRenderer fill;
        private SpriteRenderer fillEnd;
        private SpriteRenderer fillHighlight;
        private SpriteRenderer minimumSegment;
        private SpriteRenderer marker;
        private SpriteRenderer check;
        private SpriteRenderer inkBottom;
        private SpriteRenderer inkLeft;
        private SpriteRenderer inkRight;
        private SpriteRenderer inkCreep;
        private SpriteRenderer dueSeal;
        private SpriteRenderer sheen;
        private SpriteRenderer openSeal;
        private readonly List<SpriteRenderer> notches = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> bounds = new List<SpriteRenderer>();
        private TextMesh debtLabel;
        private TextMesh minimumText;
        private TextMesh termText;
        private TextMesh rateText;
        private TextMesh devText;
        private Odometer debtNumber;

        private Transform targetChip;
        private SpriteRenderer targetChipRim;
        private SpriteRenderer targetChipPlate;
        private SpriteRenderer targetChipIcon;
        private TextMesh targetChipText;

        private State state;
        private float shownFraction;
        private long peakDebt;
        private float openWidth;
        private float openAlpha;
        private float trimAlpha;
        private float undertoneBoost;
        private float scalePulse;
        private float beatScale = 1f;
        private float warningPulse;
        private int running;
        private float nextPulse = -1f;
        private float nextBeat = -1f;
        private float nextSheen = -1f;
        private float sheenClock = -1f;
        private float clock;
        private long pendingPayment;
        private Vector2 pendingFrom;
        private State pendingAfter;
        private bool paymentQueued;
        private bool minimumShownSatisfied;
        private bool sealPressed;
        private bool built;

        private float W
        {
            get { return Style.Width; }
        }

        private float H
        {
            get { return Style.Height; }
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
            body = Sprite(panel, "Body", DebtLedgerShapes.Panel, o);
            trim = Sprite(panel, "Trim", DebtLedgerShapes.Trim, o + 1);
            chip = Sprite(panel, "Chip", DebtLedgerShapes.Chip, o + 2);
            groove = Sprite(panel, "Groove", ViewUtil.WhiteSprite, o + 2);
            fill = Sprite(panel, "Fill", ViewUtil.WhiteSprite, o + 3);
            fillEnd = Sprite(panel, "FillEnd", DebtLedgerShapes.BandEnd, o + 3);
            fillHighlight = Sprite(panel, "FillHighlight", ViewUtil.WhiteSprite, o + 4);
            minimumSegment = Sprite(panel, "MinimumSegment", ViewUtil.WhiteSprite, o + 4);
            marker = Sprite(panel, "Marker", ViewUtil.WhiteSprite, o + 5);
            check = Sprite(panel, "Check", DebtLedgerShapes.Seal, o + 6);
            inkBottom = Sprite(panel, "InkBottom", ViewUtil.WhiteSprite, o + 2);
            inkLeft = Sprite(panel, "InkLeft", ViewUtil.WhiteSprite, o + 2);
            inkRight = Sprite(panel, "InkRight", ViewUtil.WhiteSprite, o + 2);
            inkCreep = Sprite(panel, "InkCreep", ViewUtil.WhiteSprite, o + 5);
            dueSeal = Sprite(panel, "DueSeal", DebtLedgerShapes.Seal, o + 6);
            sheen = Sprite(panel, "Sheen", DebtLedgerShapes.Soft, o + 7);
            openSeal = Sprite(panel, "OpenSeal", DebtLedgerShapes.Seal, o + 7);

            debtLabel = Text(panel, "DebtLabel", Loc.Pick("DEBT", "BORÇ"), Style.LabelSize,
                Style.BrassDim, o + 6, TextAnchor.LowerLeft);
            minimumText = Text(panel, "Minimum", string.Empty, Style.MinimumSize, Style.Cream,
                o + 6, TextAnchor.MiddleRight);
            termText = Text(panel, "Term", string.Empty, Style.TermSize, Style.Cream, o + 6,
                TextAnchor.MiddleRight);
            rateText = Text(panel, "Rate", string.Empty, Style.RateSize, Style.BrassDim, o + 6,
                TextAnchor.MiddleRight);
            devText = Text(panel, "Dev", string.Empty, 0.0075f, new Color(0.6f, 1f, 0.9f), o + 9,
                TextAnchor.LowerCenter);
            debtNumber = new Odometer(panel, "Debt", Style.DebtSize, o + 6, 0.2f * H);

            for (int i = 0; i < 4; i++)
            {
                bounds.Add(Sprite(panel, "Bound" + i, ViewUtil.WhiteSprite, o + 9));
            }

            targetChip = new GameObject("DebtTargetChip").transform;
            targetChip.SetParent(transform, false);
            targetChipRim = Sprite(targetChip, "Rim", ViewUtil.RoundedSprite, o);
            targetChipPlate = Sprite(targetChip, "Plate", ViewUtil.RoundedSprite, o + 1);
            targetChipIcon = Sprite(targetChip, "Icon", DebtLedgerShapes.Chip, o + 2);
            targetChipText = Text(targetChip, "Text", string.Empty, 0.011f, Style.Cream, o + 2,
                TextAnchor.MiddleLeft);
            targetChip.gameObject.SetActive(false);

            Layout();
            panel.gameObject.SetActive(false);
        }

        /// <summary>Where the panel stands, and how wide it is. Its height follows the width.</summary>
        public void SetPlacement(Vector2 centre, float width)
        {
            transform.localPosition = new Vector3(centre.x, centre.y, 0f);
            float k = width / Style.Width;
            transform.localScale = new Vector3(k, k, 1f);
        }

        /// <summary>The panel's own world position of the debt number (a payment's target).</summary>
        public Vector2 DebtNumberWorld
        {
            get { return panel.TransformPoint(new Vector3(-W * 0.5f + 0.34f + 0.3f, 0.02f, 0f)); }
        }

        /// <summary>The panel's centre in the world.</summary>
        public Vector2 CentreWorld
        {
            get { return transform.position; }
        }

        private void Layout()
        {
            Place(body, Vector2.zero, W, H);
            Place(trim, Vector2.zero, W, H);
            Place(chip, new Vector2(-W * 0.5f + 0.17f, 0.1f), 0.2f, 0.15f);
            debtLabel.transform.localPosition = new Vector3(-W * 0.5f + 0.34f, 0.17f, 0f);
            debtNumber.SetOrigin(new Vector2(-W * 0.5f + 0.34f, 0.04f));
            minimumText.transform.localPosition = new Vector3(W * 0.5f - 0.1f, 0.2f, 0f);
            termText.transform.localPosition = new Vector3(W * 0.5f - 0.1f, 0.07f, 0f);
            rateText.transform.localPosition = new Vector3(W * 0.5f - 0.1f, -0.04f, 0f);
            devText.transform.localPosition = new Vector3(0f, H * 0.5f + 0.04f, 0f);
            float bandY = -0.15f;
            Place(groove, new Vector2(0f, bandY), BandLength + 0.02f, BandHeight + 0.02f);
            Place(openSeal, new Vector2(-W * 0.5f + 0.34f + 0.72f, 0.07f), 0.13f, 0.13f);
            Place(dueSeal, new Vector2(W * 0.5f - 0.07f, H * 0.5f - 0.08f), 0.12f, 0.12f);
        }

        private float BandLength
        {
            get { return W - 0.24f; }
        }

        private float BandHeight
        {
            get { return 0.07f; }
        }

        private float BandLeft
        {
            get { return -BandLength * 0.5f; }
        }

        private const float BandY = -0.15f;

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
                Place(openSeal, new Vector2(-W * 0.5f + 1.06f, 0.07f), 0.13f, 0.13f);
            }
            peakDebt = Math.Max(peakDebt, s.Debt);
            if (running == 0)
            {
                debtNumber.Snap(s.Debt);
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

        /// <summary>The debt carried into a new stage: a dark slip leaves the ledger for the
        /// TOTAL, which is where the debt now sits.</summary>
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

        /// <summary>The last stage of the term has begun.</summary>
        public void PlayFinalDueStamp()
        {
            Build();
            if (!IsOpen)
            {
                return;
            }
            StartCoroutine(SmallStamp(Loc.Pick("FINAL DUE", "SON VADE"), Style.Crimson, 0.95f,
                Cue.FinalDue));
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

        /// <summary>The debt chip beside the round target: "+500" under a small ledger mark. Null
        /// hides it. Placed every frame by the controller, because the target is canvas text.</summary>
        public void SetTargetChip(Vector2? leftCentre, long installment)
        {
            Build();
            if (!leftCentre.HasValue || installment <= 0)
            {
                targetChip.gameObject.SetActive(false);
                return;
            }
            targetChip.gameObject.SetActive(true);
            Vector3 local = transform.InverseTransformPoint(leftCentre.Value);
            string text = "+" + Money(installment);
            SetText(targetChipText, text);
            float w = 0.3f + 0.075f * text.Length;
            float h = 0.3f;
            targetChip.localPosition = new Vector3(local.x + w * 0.5f, local.y, 0f);
            Place(targetChipRim, Vector2.zero, w + 0.03f, h + 0.03f);
            targetChipRim.color = Style.Brass;
            Place(targetChipPlate, Vector2.zero, w, h);
            targetChipPlate.color = Style.Wine;
            Place(targetChipIcon, new Vector2(-w * 0.5f + 0.1f, 0f), 0.11f, 0.08f);
            targetChipIcon.color = Style.Brass;
            targetChipText.transform.localPosition = new Vector3(-w * 0.5f + 0.19f, 0f, 0f);
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
                float scale = Mathf.Lerp(1.5f, 1f, EaseOut(u));
                Place(openSeal, new Vector2(-W * 0.5f + 1.06f, 0.07f), 0.13f * scale, 0.13f * scale);
                openSeal.color = WithAlpha(Style.Burgundy, Mathf.Clamp01(k / 0.04f) * 0.95f);
                yield return null;
            }
            sealPressed = true;
            running--;
        }

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
                0.011f, Style.Ink, Style.Order + 13, TextAnchor.MiddleCenter);
            float plateW = 0.22f + 0.062f * label.text.Length;
            Place(plate, Vector2.zero, plateW, 0.24f);
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
            // Contact: absorbed, the band retracts, the digits slide DOWN.
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
                yield return null;
            }
            Destroy(chipGo.gameObject);
            shownFraction = toFraction;
            if (after.MinimumSatisfied && !minimumShownSatisfied)
            {
                yield return MinimumSatisfied();
            }
            running--;
        }

        private IEnumerator MinimumSatisfied()
        {
            running++;
            minimumShownSatisfied = true;
            Emit(Cue.MinimumSatisfied);
            float t = 0f;
            while (t < 0.26f)
            {
                t += Dt;
                float u = Mathf.Clamp01(t / 0.26f);
                // dim -> warm -> a breath of cream-gold
                Color c = u < 0.5f ? Color.Lerp(Style.BrassDim, Style.Amber, u / 0.5f)
                    : Color.Lerp(Style.GoldIvory, Style.Brass, (u - 0.5f) / 0.5f);
                markerFlash = c;
                markerFlashK = 1f - u * 0.3f;
                yield return null;
            }
            markerFlashK = 0f;
            yield return SmallStamp(Loc.Pick("MINIMUM", "ASGARİ"), Style.Brass, 0.55f, null);
            running--;
        }

        private Color markerFlash;
        private float markerFlashK;

        private IEnumerator Interest(long previous, long interest, State after, bool isInterest)
        {
            running++;
            if (isInterest)
            {
                Emit(Cue.Interest);
            }
            peakDebt = Math.Max(peakDebt, after.Debt);
            float fromFraction = Fraction(previous);
            float toFraction = Fraction(after.Debt);
            // "FAİZ +250" over the band's right end ("BORÇ +250" for a new purchase).
            TextMesh label = Text(panel, "InterestLabel",
                (isInterest ? Loc.Pick("INTEREST +", "FAİZ +") : Loc.Pick("DEBT +", "BORÇ +")) + Money(interest),
                0.0105f, isInterest ? Style.Crimson : Style.Cream, Style.Order + 9, TextAnchor.LowerRight);
            label.transform.localPosition = new Vector3(W * 0.5f - 0.12f, BandY + 0.06f, 0f);
            float t = 0f;
            bool rolled = false;
            while (t < Style.InterestCreep + Style.InterestRoll + 0.5f)
            {
                t += Dt;
                float creep = Mathf.Clamp01(t / Style.InterestCreep);
                // red ledger ink creeping in from the right edge toward the fill's end
                float reach = Mathf.Lerp(0f, BandLength * Mathf.Max(0.12f, 1f - toFraction + 0.1f),
                    EaseOut(creep));
                float fade = t > Style.InterestCreep + Style.InterestRoll ? 1f
                    - (t - Style.InterestCreep - Style.InterestRoll) / 0.5f : 1f;
                Place(inkCreep, new Vector2(BandLength * 0.5f - reach * 0.5f, BandY), reach, BandHeight * 1.4f);
                inkCreep.color = WithAlpha(Style.Burgundy, isInterest ? 0.7f * Mathf.Clamp01(fade) : 0f);
                if (!rolled && t >= Style.InterestCreep * 0.6f)
                {
                    rolled = true;
                    debtNumber.Roll(previous, after.Debt, false, Style.InterestRoll);
                    state = after;
                }
                if (rolled)
                {
                    float u = Mathf.Clamp01((t - Style.InterestCreep * 0.6f) / Style.InterestRoll);
                    shownFraction = Mathf.Lerp(fromFraction, toFraction, EaseOut(u));
                    if (isInterest)
                    {
                        scalePulse = 0.008f * Mathf.Sin(u * Mathf.PI);
                        undertoneBoost = 0.35f * Mathf.Sin(u * Mathf.PI);
                    }
                }
                float lf = Mathf.Clamp01(fade);
                ViewUtil.SetTextColor(label, WithAlpha(isInterest ? Style.Crimson : Style.Cream,
                    Mathf.Clamp01(t / 0.08f) * lf));
                yield return null;
            }
            inkCreep.color = Color.clear;
            scalePulse = 0f;
            undertoneBoost = 0f;
            shownFraction = toFraction;
            Destroy(label.gameObject);
            running--;
        }

        private IEnumerator Carry(long debt)
        {
            running++;
            Emit(Cue.Carry);
            Vector2 from = DebtNumberWorld;
            Vector2 to = ScoreAnchor != null ? ScoreAnchor() : from + new Vector2(-3f, 1.5f);
            var slip = new GameObject("CarrySlip").transform;
            slip.SetParent(transform.parent, false);
            SpriteRenderer plate = Sprite(slip, "Plate", ViewUtil.RoundedSprite, Style.Order + 12);
            TextMesh label = Text(slip, "Label", "-" + Money(debt), 0.012f, Style.Cream,
                Style.Order + 13, TextAnchor.MiddleCenter);
            Place(plate, Vector2.zero, 0.24f + 0.07f * label.text.Length, 0.26f);
            plate.color = Style.Wine;
            Vector2 ctrl = (from + to) * 0.5f + new Vector2(0f, 0.7f);
            float t = 0f;
            const float travel = 0.34f;
            while (t < travel + 0.18f)
            {
                t += Dt;
                float u = EaseInOut(Mathf.Clamp01(t / travel));
                slip.position = Bezier(from, ctrl, to, u);
                float fade = t > travel ? 1f - (t - travel) / 0.18f : 1f;
                plate.color = WithAlpha(Style.Wine, Mathf.Clamp01(fade));
                ViewUtil.SetTextColor(label, WithAlpha(Style.Cream, Mathf.Clamp01(fade)));
                // pressure: the debt settles a pixel as the stage turns
                debtNumber.Nudge(state.Deadline >= CreditDeadline.Pressure ? -0.008f * Mathf.Sin(Mathf.Clamp01(t / travel) * Mathf.PI) : 0f);
                yield return null;
            }
            debtNumber.Nudge(0f);
            Destroy(slip.gameObject);
            running--;
        }

        private IEnumerator Settle(long bonus)
        {
            running++;
            float fromFraction = shownFraction;
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
            TextMesh label = Text(token, "Label", "+" + Money(bonus), 0.014f, Style.GoldIvory,
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
            _ = label;
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

        /// <summary>A small legal stamp pressed on the panel's face: scale 1.6 -> 0.95 -> 1, a
        /// hold, and gone. The letters are the stamp's; the ink is the tint.</summary>
        private IEnumerator SmallStamp(string text, Color ink, float hold, Cue? cue)
        {
            running++;
            var root = new GameObject("Stamp").transform;
            root.SetParent(panel, false);
            root.localPosition = new Vector3(0.18f, 0.02f, 0f);
            root.localRotation = Quaternion.Euler(0f, 0f, -5f);
            SpriteRenderer plate = Sprite(root, "Plate", DebtLedgerShapes.StampSmall, Style.Order + 10);
            TextMesh label = Text(root, "Label", text, 0.012f, Style.Cream, Style.Order + 11,
                TextAnchor.MiddleCenter);
            float w = 0.26f + 0.078f * text.Length;
            Place(plate, Vector2.zero, w, w * 0.5f);
            if (cue.HasValue)
            {
                Emit(cue.Value);
            }
            float t = 0f;
            float total = 0.18f + hold + 0.25f;
            while (t < total)
            {
                t += Dt;
                float s = t < 0.1f ? Mathf.Lerp(1.6f, 0.95f, EaseOut(t / 0.1f))
                    : t < 0.18f ? Mathf.Lerp(0.95f, 1f, (t - 0.1f) / 0.08f) : 1f;
                root.localScale = new Vector3(s, s, 1f);
                float a = t < 0.05f ? t / 0.05f : t > 0.18f + hold ? 1f - (t - 0.18f - hold) / 0.25f : 1f;
                plate.color = WithAlpha(ink, 0.92f * Mathf.Clamp01(a));
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
            TickTension(dt);
            Paint();
        }

        private void TickTension(float dt)
        {
            // WARNING: a slow, low burgundy pulse along the trim every few seconds.
            if (state.Deadline == CreditDeadline.Warning || state.Deadline == CreditDeadline.FinalDue)
            {
                if (nextPulse < 0f)
                {
                    nextPulse = clock + Mathf.Lerp(Style.WarningPulseMin, Style.WarningPulseMax, Hash(1));
                }
                float p = clock - nextPulse;
                warningPulse = p >= 0f && p < 1.2f ? 0.5f * Mathf.Sin(p / 1.2f * Mathf.PI) : 0f;
                if (p >= 1.2f)
                {
                    nextPulse = clock + Mathf.Lerp(Style.WarningPulseMin, Style.WarningPulseMax,
                        Hash((int)(clock * 7f)));
                }
            }
            else
            {
                warningPulse = 0f;
                nextPulse = -1f;
            }
            // FINAL DUE: a slow heavy beat on the debt.
            if (state.Deadline == CreditDeadline.FinalDue)
            {
                if (nextBeat < 0f || clock >= nextBeat + 0.3f)
                {
                    nextBeat = clock + Mathf.Lerp(Style.FinalBeatMin, Style.FinalBeatMax, Hash((int)(clock * 3f)));
                }
                float b = clock - nextBeat;
                beatScale = b >= 0f && b < 0.3f ? 1f + 0.025f * Mathf.Sin(b / 0.3f * Mathf.PI) : 1f;
            }
            else
            {
                beatScale = 1f;
                nextBeat = -1f;
            }
            // SAFE / PRESSURE: now and then a very faint brass sheen - never on the last stages.
            if (state.Deadline == CreditDeadline.Safe || state.Deadline == CreditDeadline.Pressure)
            {
                if (nextSheen < 0f)
                {
                    nextSheen = clock + Mathf.Lerp(Style.SheenMin, Style.SheenMax, Hash(7));
                }
                if (sheenClock < 0f && clock >= nextSheen)
                {
                    sheenClock = 0f;
                    nextSheen = clock + Mathf.Lerp(Style.SheenMin, Style.SheenMax, Hash((int)(clock * 5f)));
                }
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
            float tension = stage <= 1 ? 0f : stage == 2 ? 0.35f : stage == 3 ? 0.55f : 0.7f;
            float sx = openWidth * (1f + scalePulse);
            if (sealPressed)
            {
                openSeal.color = WithAlpha(Style.Burgundy, 0.95f * openAlpha);
            }
            panel.localScale = new Vector3(sx, 1f, 1f);

            Color undertone = Color.Lerp(Style.Charcoal, Style.Undertone,
                Mathf.Clamp01(tension + undertoneBoost));
            body.color = WithAlpha(undertone, openAlpha);
            Color trimColour = Color.Lerp(Style.Brass, Style.Burgundy, warningPulse);
            trim.color = WithAlpha(trimColour, trimAlpha * 0.9f);
            chip.color = WithAlpha(Style.Brass, trimAlpha * 0.85f);
            groove.color = WithAlpha(Style.Graphite, openAlpha);
            ViewUtil.SetTextColor(debtLabel, WithAlpha(Style.BrassDim, trimAlpha));

            // The debt: ivory, warming toward cream-red as the term runs out.
            Color debtColour = Color.Lerp(Style.Cream, Style.CreamWarm, tension);
            debtNumber.SetColour(WithAlpha(debtColour, openAlpha));
            debtNumber.SetScale(beatScale);

            // the band
            float fraction = Mathf.Clamp01(shownFraction);
            float fillLen = BandLength * fraction;
            Place(fill, new Vector2(BandLeft + fillLen * 0.5f, BandY), fillLen, BandHeight);
            fill.color = WithAlpha(Style.Wine, openAlpha);
            Place(fillEnd, new Vector2(BandLeft + fillLen + 0.02f, BandY), 0.05f, BandHeight);
            fillEnd.color = WithAlpha(Style.Wine, fillLen > 0.01f ? openAlpha : 0f);
            Place(fillHighlight, new Vector2(BandLeft + fillLen * 0.5f, BandY + BandHeight * 0.42f),
                fillLen, 0.008f);
            fillHighlight.color = WithAlpha(Style.Amber, 0.35f * openAlpha);

            // the minimum: a brass notch where the stage's minimum ends, and the part of the fill
            // beyond it (still owed of the minimum) warming toward amber as it is paid.
            float markerFraction = MarkerFraction();
            bool hasMinimum = state.MinimumDue > 0 && markerFraction >= 0f;
            float markerX = BandLeft + BandLength * Mathf.Clamp01(markerFraction);
            Place(marker, new Vector2(markerX, BandY), 0.014f, BandHeight * 1.9f);
            Color markerColour = markerFlashK > 0f ? markerFlash
                : minimumShownSatisfied ? Style.Brass : Style.BrassDim;
            marker.color = WithAlpha(markerColour,
                hasMinimum && Layers.ShowMinimumMarker ? openAlpha : 0f);
            float segLen = Mathf.Max(0f, fillLen - (markerX - BandLeft));
            float paid = state.MinimumDue > 0 ? Mathf.Clamp01(state.MinimumPaid / (float)state.MinimumDue) : 0f;
            Place(minimumSegment, new Vector2(markerX + segLen * 0.5f, BandY), segLen, BandHeight * 0.7f);
            minimumSegment.color = WithAlpha(Color.Lerp(Style.Burgundy, Style.Amber, paid),
                hasMinimum && Layers.ShowMinimumProgress && segLen > 0.001f ? 0.8f * openAlpha : 0f);
            Place(check, new Vector2(markerX, BandY + 0.1f), 0.07f, 0.07f);
            check.color = WithAlpha(Style.GoldIvory, minimumShownSatisfied && hasMinimum ? 0.9f * openAlpha : 0f);

            // the facts
            string minimum = minimumShownSatisfied && state.InRound
                ? Loc.Pick("MINIMUM PAID", "ASGARİ TAMAM")
                : (state.InRound ? Loc.Pick("MINIMUM ", "ASGARİ ") : Loc.Pick("NEXT MINIMUM ", "SONRAKİ ASGARİ "))
                    + Money(state.MinimumDue);
            SetText(minimumText, state.MinimumDue > 0 ? minimum : string.Empty);
            ViewUtil.SetTextColor(minimumText, WithAlpha(minimumShownSatisfied ? Style.Brass : Style.Cream, trimAlpha));
            SetText(termText, Loc.Pick("TERM ", "VADE ") + Mathf.Max(0, state.TermLeft));
            ViewUtil.SetTextColor(termText, WithAlpha(stage >= 4 ? Style.CreamWarm : Style.Cream, trimAlpha));
            SetText(rateText, Loc.Pick("RATE ", "FAİZ ") + Rate(state.InterestPermille));
            ViewUtil.SetTextColor(rateText, WithAlpha(Style.BrassDim, trimAlpha));

            // warning ink up the rim; the foreclosure seal on the border at the last stage
            float ink = stage == 3 ? 0.35f : stage >= 4 ? 0.7f : 0f;
            Place(inkBottom, new Vector2(0f, -H * 0.5f + 0.035f), W * 0.9f * ink, 0.012f);
            inkBottom.color = WithAlpha(Style.Burgundy, ink > 0f ? 0.75f * openAlpha : 0f);
            Place(inkLeft, new Vector2(-W * 0.5f + 0.035f, -H * 0.5f + H * 0.45f * ink), 0.012f, H * 0.9f * ink);
            inkLeft.color = WithAlpha(Style.Burgundy, ink > 0f ? 0.6f * openAlpha : 0f);
            Place(inkRight, new Vector2(W * 0.5f - 0.035f, -H * 0.5f + H * 0.45f * ink), 0.012f, H * 0.9f * ink);
            inkRight.color = WithAlpha(Style.Burgundy, ink > 0f ? 0.6f * openAlpha : 0f);
            dueSeal.color = WithAlpha(Style.Burgundy, stage >= 4 ? 0.95f * openAlpha : 0f);

            // the sheen
            if (sheenClock >= 0f)
            {
                float u = Mathf.Clamp01(sheenClock / 0.9f);
                Place(sheen, new Vector2(Mathf.Lerp(-W * 0.5f, W * 0.5f, u), 0f), 0.35f, H * 0.95f);
                sheen.color = WithAlpha(Style.Brass, 0.07f * Mathf.Sin(u * Mathf.PI) * openAlpha);
            }
            else
            {
                sheen.color = Color.clear;
            }

            PaintTrack(stage);
            PaintDebug(stage);
        }

        private void PaintTrack(int stage)
        {
            int total = Mathf.Max(0, state.TermTotal);
            while (notches.Count < total)
            {
                notches.Add(Sprite(panel, "Notch" + notches.Count, DebtLedgerShapes.Notch, Style.Order + 3));
            }
            int spent = Mathf.Clamp(total - state.TermLeft, 0, total);
            float spacing = 0.13f;
            float left = -(total - 1) * spacing * 0.5f;
            for (int i = 0; i < notches.Count; i++)
            {
                SpriteRenderer n = notches[i];
                if (i >= total || !Layers.ShowMaturityTrack)
                {
                    n.color = Color.clear;
                    continue;
                }
                Place(n, new Vector2(left + i * spacing, -H * 0.5f + 0.085f), 0.09f, 0.045f);
                Color c;
                bool current = state.InRound && i == spent;
                if (i < spent)
                {
                    c = new Color(0.12f, 0.11f, 0.12f);
                }
                else if (i == total - 1 && (stage >= 4 || state.TermLeft <= 1))
                {
                    c = Color.Lerp(Style.Amber, Style.Crimson, 0.55f);
                }
                else if (current)
                {
                    c = Style.Burgundy;
                }
                else
                {
                    c = Style.Brass;
                }
                n.color = WithAlpha(c, trimAlpha);
            }
        }

        private void PaintDebug(int stage)
        {
            bool show = Layers.ShowDebtPanelBounds;
            for (int i = 0; i < bounds.Count; i++)
            {
                SpriteRenderer b = bounds[i];
                const float line = 0.01f;
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
            if (Layers.ShowInterestPreview)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(Loc.Pick("next interest +", "sonraki faiz +")).Append(Money(state.NextInterest));
            }
            SetText(devText, (UnityEngine.Debug.isDebugBuild || Application.isEditor) ? sb.ToString() : string.Empty);
            _ = stage;
        }

        /// <summary>Where the minimum notch stands on the band, as a share of it: in a round,
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

            public Odometer(Transform parent, string name, float size, int order, float line)
            {
                a = Text(parent, name + "A", "0", size, Color.white, order, TextAnchor.MiddleLeft);
                b = Text(parent, name + "B", "0", size, Color.white, order, TextAnchor.MiddleLeft);
                SetText(b, string.Empty); // only ever shows mid-roll
                lineHeight = line;
            }

            public void SetOrigin(Vector2 at)
            {
                origin = at;
                Layout(0f);
            }

            public void Snap(long value)
            {
                Value = value;
                t = -1f;
                SetText(a, Money(value));
                SetText(b, string.Empty);
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
                    SetText(b, string.Empty);
                    Layout(0f);
                }
            }

            private void Layout(float u)
            {
                float dir = down ? -1f : 1f;
                float e = EaseOut(u);
                if (t < 0f)
                {
                    a.transform.localPosition = new Vector3(origin.x, origin.y + nudge, 0f);
                    ViewUtil.SetTextColor(a, colour);
                    return;
                }
                // the old number leaves in the direction of travel, the new one arrives from the
                // other side
                a.transform.localPosition = new Vector3(origin.x, origin.y + nudge + dir * lineHeight * e, 0f);
                b.transform.localPosition = new Vector3(origin.x, origin.y + nudge - dir * lineHeight * (1f - e), 0f);
                ViewUtil.SetTextColor(a, WithAlpha(colour, 1f - e));
                ViewUtil.SetTextColor(b, WithAlpha(colour, e));
                _ = scale;
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
            var go = new GameObject("Fleck");
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
