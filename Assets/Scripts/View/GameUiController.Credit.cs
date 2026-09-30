// PURPOSE: "Kredi kartı" on screen - the controller half of the BLACK LEDGER (DebtLedgerView), the
// DEBT PRESSURE (DebtPressurePresentationController) and HACİZ (ForeclosureView). The rules are
// GameSession.Credit.cs; this reads them and tells them.
//
//   PLACEMENT      the ledger stands at the board's upper right during a round - its top a
//                  hair under the board's top edge, its left edge just clear of the corner
//                  bracket - and takes whatever form the room there allows: WIDE when the gap
//                  to the joker column holds it, STACKED when it does not (a 16:9 screen, where
//                  that gap is 264 px). In the market it is the wide strip under the panel; on a
//                  phone the wide strip over the board. Never over the grid. SolveLedgerPlacement
//                  is the one place that decides, and the lab asks it about screens that are not
//                  the one in front of it.
//   EVENTS         are READ, never inferred from arithmetic: a payment is Core's own
//                  DebtRepaidThisStage moving, a loan is Debt rising, a new stage is a new round
//                  engine, and a stage's settlement is the CreditStatement object Core wrote
//                  (matched by identity). The statement is told IN ORDER - the last payment, the
//                  interest, the term's tick (or, when it ran out, the contract closing), the
//                  foreclosure, the settlement or the bank's thanks - each waiting for the one
//                  before it.
//   THE CARRY      a stage that starts in debt does not open with the TOTAL already negative:
//                  it shows the purse, the ledger sends the debt to it as a slip, and the number
//                  rolls down when the slip lands (creditCarryRoll). The debt drags the score
//                  under; the player does not simply "start at minus".
//   PRESSURE       every frame the pressure controller gets the rules' state (the deadline, how
//                  much of the loan is left, whether the minimum is paid) and where the board and
//                  the score line are; it presses the screen's edges, the backdrop, the board's
//                  surroundings and the header, and hands the ledger its tension.
//   THE BARS       live on the overlay canvas, so their panels' world positions are remembered
//                  every frame by instance id: by the time a foreclosure is told, Core has already
//                  taken the jokers, and the proxies have to leave from where they stood.
//   INPUT          the market waits while the bailiff works (CreditPresentationBlocksInput).
// The market's message line still carries the statement in words (CreditStatementLine).

using System.Collections;
using System.Collections.Generic;
using System.Text;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private DebtLedgerView ledgerView;
        private ForeclosureView foreclosureView;
        private DebtPressurePresentationController debtPressure;

        /// <summary>The statement last told. Identity, not value.</summary>
        private CreditStatement lastCreditShown;

        private GameSession creditSessionSeen;
        private RoundEngine creditRoundSeen;
        private long creditDebtSeen;
        private long creditRepaidShown;
        private bool creditPresenting;

        /// <summary>What the loan opened at (grown by further borrowing) - the reference the
        /// pressure's "how much is left" is measured against. Presentation only: after a load it
        /// is simply the debt as it stands.</summary>
        private long creditLoanBase;

        /// <summary>How much of the debt the TOTAL shows, 0..1: 0 while a stage's carried debt is
        /// still on its way to it, rolling to 1 as it lands. 1 whenever nothing is being told.</summary>
        private float creditCarryRoll = 1f;
        private bool creditCarryLanded;
        private float creditCarryWaited;

        private readonly Dictionary<int, Vector2> jokerPanelWorld = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, Vector2> powerPanelWorld = new Dictionary<int, Vector2>();

        /// <summary>The lab drives the views itself and switches the game's watcher off.</summary>
        private bool creditLabOwnsViews;

        private void EnsureCreditViews()
        {
            if (ledgerView != null)
            {
                return;
            }
            var go = new GameObject("DebtLedger");
            go.transform.SetParent(transform, false);
            ledgerView = go.AddComponent<DebtLedgerView>();
            ledgerView.Build();
            ledgerView.ScoreAnchor = CreditTotalAnchor;
            ledgerView.Sounded = OnLedgerCue;

            var pgo = new GameObject("DebtPressure");
            pgo.transform.SetParent(transform, false);
            debtPressure = pgo.AddComponent<DebtPressurePresentationController>();
            debtPressure.Build(cam);
            debtPressure.Sounded = OnPressureCue;
            debtPressure.Desaturate = delegate (float k)
            {
                if (backdrop != null)
                {
                    backdrop.SetDesaturation(k);
                }
            };

            var fgo = new GameObject("Foreclosure");
            fgo.transform.SetParent(transform, false);
            foreclosureView = fgo.AddComponent<ForeclosureView>();
            foreclosureView.Build(cam);
            foreclosureView.Sounded = OnForeclosureCue;
            foreclosureView.Impulse = delegate (float a) { ShakeCamera(a, 0.12f, 2.6f); };
            foreclosureView.BarsAlpha = delegate (float a)
            {
                jokerBar.SetPresentationAlpha(a);
                powerBar.SetPresentationAlpha(a);
            };
            foreclosureView.DebtMoved = delegate (long debt) { ledgerView.RollDebtTo(debt); };
        }

        /// <summary>True while the bailiff is at work - the market waits.</summary>
        private bool CreditPresentationBlocksInput()
        {
            return foreclosureView != null && foreclosureView.Playing;
        }

        /// <summary>Every frame, from the top of Update.</summary>
        private void TickCreditPresentation()
        {
            if (session == null || cam == null || jokerBar == null)
            {
                if (debtPressure != null)
                {
                    // no run: nothing presses
                    debtPressure.SetFrame(new DebtPressurePresentationController.Frame());
                }
                return;
            }
            EnsureCreditViews();
            if (!ReferenceEquals(session, creditSessionSeen))
            {
                // A new run or a loaded one: nothing that happened before is an event now.
                creditSessionSeen = session;
                creditRoundSeen = session.CurrentRound;
                creditDebtSeen = session.Debt;
                creditRepaidShown = session.DebtRepaidThisStage;
                lastCreditShown = session.LastCreditStatement;
                creditLoanBase = session.Debt;
                creditPresenting = false;
                creditPressureHeld = false;
                creditCarryRoll = 1f;
                ledgerView.Close(false);
                foreclosureView.Stop();
                debtPressure.Settle();
            }
            if (!creditPresenting)
            {
                CacheBarAnchors();
            }
            TickCarryRoll();
            if (creditLabOwnsViews)
            {
                // The lab's own numbers, on the real screen's anchors.
                FeedPressure(creditLabPressure);
                return;
            }
            if (screen != AppScreen.Playing)
            {
                // A menu owns the frame: the ledger steps out of it without stopping anything.
                ledgerView.transform.localScale = new Vector3(0.0001f, 0.0001f, 1f);
            }
            else
            {
                PlaceLedger();
            }
            bool inRound = session.Phase == GamePhase.Round && session.CurrentRound != null;
            DebtLedgerView.State state = CreditLedgerState();
            // While a stage's statement is being told, the screen keeps the weight it had: Core
            // has already moved the term on and charged the interest, and the pressure follows
            // them when the ledger has told them - the tick, not the market's first frame.
            PressureFacts facts = SessionPressure();
            bool telling = creditPresenting || !ReferenceEquals(session.LastCreditStatement, lastCreditShown);
            if (telling && creditPressureHeld)
            {
                facts.Active = facts.Active || creditPressureKept.Active;
                facts.Deadline = creditPressureKept.Deadline;
                facts.DebtShare = creditPressureKept.DebtShare;
            }
            else
            {
                creditPressureKept = facts;
                creditPressureHeld = true;
            }
            FeedPressure(facts);
            if (creditPresenting)
            {
                return;
            }

            // A stage just settled: tell its statement, in order.
            CreditStatement statement = session.LastCreditStatement;
            if (!ReferenceEquals(statement, lastCreditShown))
            {
                lastCreditShown = statement;
                if (statement != null && (session.Phase == GamePhase.Market || session.Phase == GamePhase.RunWon))
                {
                    StartCoroutine(PresentStatement(statement));
                    return;
                }
            }

            // A new stage: the debt carried into it drags the TOTAL down.
            if (inRound && !ReferenceEquals(session.CurrentRound, creditRoundSeen))
            {
                creditRoundSeen = session.CurrentRound;
                creditRepaidShown = session.DebtRepaidThisStage;
                creditDebtSeen = session.Debt;
                if (session.Debt > 0)
                {
                    ledgerView.ShowState(state);
                    if (screen == AppScreen.Playing)
                    {
                        creditCarryRoll = 0f;
                        creditCarryLanded = false;
                        creditCarryWaited = 0f;
                        UpdateScoreHud();
                        ledgerView.PlayCarry(state);
                        if (state.Deadline == CreditDeadline.FinalDue)
                        {
                            ledgerView.PlayFinalDueStamp();
                        }
                    }
                }
                return;
            }

            // Money in: Core's own count of what this stage has paid moved. In a round it is the
            // meter above the round's own bar that pays, so the chip leaves the round's target.
            long repaid = session.DebtRepaidThisStage;
            if (repaid > creditRepaidShown && ledgerView.IsOpen)
            {
                ledgerView.PlayPayment(repaid - creditRepaidShown,
                    inRound ? CreditRoundAnchor() : MarketPaymentAnchor(), state);
            }
            creditRepaidShown = repaid;

            // Money borrowed: a new contract, or more on the open one.
            if (session.Debt > creditDebtSeen)
            {
                if (!ledgerView.IsOpen)
                {
                    creditLoanBase = session.Debt;
                    ledgerView.PlayOpen(state);
                }
                else
                {
                    creditLoanBase += session.Debt - creditDebtSeen;
                    ledgerView.PlayBorrow(creditDebtSeen, state);
                }
            }
            creditDebtSeen = session.Debt;

            // Paid off without a statement (mid-stage, or by a sale): the clean close.
            if (session.Debt <= 0 && ledgerView.IsOpen && !ledgerView.Busy)
            {
                creditLoanBase = 0;
                ledgerView.PlaySettled(0);
            }
            if (!ledgerView.Busy)
            {
                ledgerView.ShowState(state);
            }
        }

        /// <summary>A stage's settlement, told in the order it happened.</summary>
        private IEnumerator PresentStatement(CreditStatement statement)
        {
            creditPresenting = true;
            // 1. what the stage's last turn paid, if the ledger has not shown it yet
            long unshown = statement.Repaid - creditRepaidShown;
            if (unshown > 0 && ledgerView.IsOpen)
            {
                DebtLedgerView.State paid = CreditLedgerState();
                paid.Debt = statement.DebtBeforeInterest > 0 ? statement.DebtBeforeInterest
                    : statement.DebtAtStart - statement.Repaid;
                paid.InRound = true;
                ledgerView.PlayPayment(unshown, CreditRoundAnchor(), paid);
                yield return WaitForLedger();
            }
            creditRepaidShown = 0;
            // 2. the interest - the debt grew by itself
            if (statement.Interest > 0)
            {
                DebtLedgerView.State charged = CreditLedgerState();
                charged.Debt = statement.DebtBeforeInterest + statement.Interest;
                ledgerView.PlayInterest(statement.DebtBeforeInterest, statement.Interest, charged);
                yield return WaitForLedger();
            }
            // 3. the term: a stage of it is spent (Core's numbers, before and after) - or, when it
            //    ran out with money owed, the contract closes a beat before the bailiff arrives
            int termBefore = statement.TermStages - statement.CarriedBefore;
            if (statement.Foreclosed && ledgerView.IsOpen)
            {
                ledgerView.PlayContractLock(termBefore);
                yield return WaitForLedger();
            }
            else if (statement.DebtAfter > 0 && statement.TermLeft < termBefore && ledgerView.IsOpen)
            {
                ledgerView.PlayTermTick(termBefore, statement.TermLeft);
                yield return WaitForLedger();
            }
            // 4. the bailiff
            if (statement.Foreclosed)
            {
                foreclosureView.PlaybackRate = 1f;
                foreclosureView.Play(statement, ForeclosureAssets(statement), ForeclosureLedgerAt(),
                    ForeclosureLedgerScale());
                while (foreclosureView.Playing)
                {
                    yield return null;
                }
                jokerBar.Refresh(session, null);
                powerBar.Refresh(session, null);
            }
            // 5. settled - on time, with the bank's thanks, or after the bailiff
            long bonus = statement.Reward == BankRewardKind.Points ? statement.RewardPoints : 0;
            if (session.Debt <= 0 && ledgerView.IsOpen)
            {
                ledgerView.PlaySettled(bonus);
                yield return WaitForLedger();
            }
            else if (bonus > 0)
            {
                ledgerView.PlayBonusOnly(bonus);
            }
            if (statement.Reward == BankRewardKind.Campaign && statement.CampaignOffer != null)
            {
                PlayCampaignNote(statement);
            }
            creditDebtSeen = session.Debt;
            creditRepaidShown = session.DebtRepaidThisStage;
            if (session.Debt <= 0)
            {
                creditLoanBase = 0;
            }
            ledgerView.ShowState(CreditLedgerState());
            creditPresenting = false;
        }

        private IEnumerator WaitForLedger()
        {
            float guard = 0f;
            yield return null;
            while (ledgerView.Busy && guard < 6f)
            {
                guard += Time.deltaTime;
                yield return null;
            }
        }

        private void PlayCampaignNote(CreditStatement statement)
        {
            sfx.DebtPaid();
            int index = -1;
            for (int i = 0; i < session.Market.Offers.Count; i++)
            {
                if (ReferenceEquals(session.Market.Offers[i], statement.CampaignOffer))
                {
                    index = i;
                }
            }
            Vector2? at = index >= 0 ? marketView.OfferWorldCenter(index) : null;
            FloatingTextFx.Spawn(transform, (at ?? new Vector2(0f, 1.6f)) + new Vector2(0f, 0.8f),
                Loc.Pick("BANK CAMPAIGN  -" + statement.CampaignPercent + "%",
                    "BANKA KAMPANYASI  -%" + statement.CampaignPercent),
                new Color(0.45f, 0.9f, 1f), 56, 0.07f);
        }

        /// <summary>The ledger's facts, straight from the session's queries.</summary>
        private DebtLedgerView.State CreditLedgerState()
        {
            bool inRound = session.Phase == GamePhase.Round && session.CurrentRound != null;
            return new DebtLedgerView.State
            {
                Debt = session.Debt,
                StageStartDebt = inRound ? session.DebtAtStageStart : 0,
                MinimumDue = inRound ? session.CurrentRound.CreditInstallment : session.NextStageMinimumPayment,
                MinimumPaid = inRound ? session.MinimumPaidThisStage : 0,
                MinimumSatisfied = inRound && session.MinimumSatisfied,
                InRound = inRound,
                InterestPermille = session.CreditInterestPermille,
                NextInterest = session.NextInterest,
                TermLeft = session.CreditTermLeft,
                TermTotal = session.CreditTermStages,
                Deadline = session.CreditDeadline
            };
        }

        // ------------------------------------------------------------------ the pressure

        /// <summary>The rules' side of a pressure frame: the facts, with nothing about where
        /// anything is. The lab fills one of these with numbers of its own.</summary>
        private struct PressureFacts
        {
            public bool Active;
            public CreditDeadline Deadline;
            public float DebtShare;
            public bool MinimumOwed;
            public long OwnBar;
            public long Installment;
            public long RoundScore;
            public bool MinimumSatisfied;
            public bool InRound;
        }

        /// <summary>What the lab is showing, while it owns the views.</summary>
        private PressureFacts creditLabPressure;

        /// <summary>The pressure's facts as they stood before a statement began to be told.</summary>
        private PressureFacts creditPressureKept;
        private bool creditPressureHeld;

        private PressureFacts SessionPressure()
        {
            RoundEngine round = session.CurrentRound;
            bool inRound = session.Phase == GamePhase.Round && round != null;
            long debt = session.Debt;
            long loanBase = System.Math.Max(creditLoanBase, 1L);
            return new PressureFacts
            {
                Active = debt > 0 && screen == AppScreen.Playing,
                Deadline = session.CreditDeadline,
                DebtShare = debt / (float)loanBase,
                MinimumOwed = inRound && round.CreditInstallment > 0 && !session.MinimumSatisfied,
                OwnBar = inRound ? round.OwnBar : 0,
                Installment = inRound ? round.CreditInstallment : 0,
                RoundScore = inRound ? round.RoundScore : 0,
                MinimumSatisfied = inRound && session.MinimumSatisfied,
                InRound = inRound
            };
        }

        /// <summary>One pressure frame: the facts, plus where the board and the score line
        /// actually are on screen right now. Also hands the ledger its tension.</summary>
        private void FeedPressure(PressureFacts facts)
        {
            var frame = new DebtPressurePresentationController.Frame
            {
                Active = facts.Active,
                InRound = facts.InRound,
                Deadline = facts.Deadline,
                DebtShare = facts.DebtShare,
                MinimumOwed = facts.MinimumOwed,
                OwnBar = facts.OwnBar,
                Installment = facts.Installment,
                RoundScore = facts.RoundScore,
                MinimumSatisfied = facts.MinimumSatisfied
            };
            if (boardView != null && facts.InRound)
            {
                // The VISIBLE board: the cells plus the plate's own overhang, following the
                // overtime squeeze - the brackets frame what the player actually sees.
                Rect arena = boardView.ArenaRect;
                float over = BoardView.BorderOverhang * 0.5f;
                frame.Board = new Rect(arena.xMin - over, arena.yMin - over,
                    arena.width + over * 2f, arena.height + over * 2f);
            }
            Rect number = new Rect();
            Rect roundPart = new Rect();
            float limit = 0f;
            frame.Header = facts.InRound && screen == AppScreen.Playing
                && ScoreHeaderRects(out number, out roundPart, out limit);
            if (frame.Header)
            {
                frame.TotalNumber = number;
                frame.RoundPart = roundPart;
                frame.HeaderRightLimit = limit;
            }
            debtPressure.SetFrame(frame);
            ledgerView.SetTension(debtPressure.Pressure01);
        }

        /// <summary>The carried debt reaches the TOTAL: it rolls from the purse down to the
        /// balance over a third of a second (and never waits forever for a slip that is not
        /// coming - a lab, a menu, a skipped stage).</summary>
        private void TickCarryRoll()
        {
            if (creditCarryRoll >= 1f)
            {
                return;
            }
            creditCarryWaited += Time.deltaTime;
            if (creditCarryLanded || creditCarryWaited > 1.6f)
            {
                creditCarryRoll = Mathf.Min(1f, creditCarryRoll + Time.deltaTime / 0.35f);
            }
            UpdateScoreHud();
        }

        /// <summary>The balance the TOTAL prints: the real one, except while a stage's carried
        /// debt is still on its way to it (or rolling in), when it is the purse and then part of
        /// the debt - the score is dragged down rather than snapped.</summary>
        private long CreditShownBalance()
        {
            long balance = session.Balance;
            if (!ReferenceEquals(session, creditSessionSeen) || session.Debt <= 0)
            {
                return balance;
            }
            bool newStage = session.Phase == GamePhase.Round && session.CurrentRound != null
                && !ReferenceEquals(session.CurrentRound, creditRoundSeen) && screen == AppScreen.Playing
                && !creditLabOwnsViews;
            float roll = newStage ? 0f : creditCarryRoll;
            if (roll >= 1f)
            {
                return balance;
            }
            float eased = 1f - (1f - roll) * (1f - roll) * (1f - roll);
            return session.TotalScore - (long)System.Math.Round(session.Debt * (double)eased);
        }

        // ------------------------------------------------------------------ where things are

        private float CanvasToWorld
        {
            get
            {
                float halfW = cam.orthographicSize * cam.aspect;
                return 2f * halfW / Mathf.Max(1f, UiLayout.Active.CanvasReference.x);
            }
        }

        /// <summary>Where the ledger goes, in what form, at what size.</summary>
        private struct LedgerPlacement
        {
            public DebtLedgerView.Form Form;
            public float Width;
            public Vector2 Centre;
            public float Scale;
            /// <summary>The room it was given (world units), for the lab's readout.</summary>
            public float Room;
        }

        /// <summary>The ledger's gap from the board's visible edge, and its top below the board's
        /// top: clear of the corner bracket, and 12-24 px under the corner it answers to.</summary>
        private const float LedgerBoardGap = 0.3f;
        private const float LedgerTopDrop = 0.13f;
        private const float LedgerColumnGap = 0.1f;

        /// <summary>
        /// THE ONE PLACE THE LEDGER'S PLACE IS DECIDED - for the screen in front of us, or (the
        /// lab) for one that is not: <paramref name="halfW"/> x <paramref name="halfH"/> is the
        /// visible half-extent in world units, and everything else is the layout's own numbers.
        /// </summary>
        private LedgerPlacement SolveLedgerPlacement(float halfW, float halfH, bool market)
        {
            UiLayout layout = UiLayout.Active;
            Vector2 c = cam.transform.position;
            var p = new LedgerPlacement();
            float wideW = DebtLedgerView.Style.WideWidth;
            float wideH = DebtLedgerView.Style.WideHeight;
            if (market)
            {
                // the free strip under the market panel
                float strip = layout.MarketBottomReserve;
                p.Form = DebtLedgerView.Form.Wide;
                p.Scale = Mathf.Clamp((strip - 0.06f) / wideH, 0.6f, 1f);
                p.Scale = Mathf.Min(p.Scale, halfW * 1.9f / wideW);
                p.Centre = new Vector2(c.x, c.y - halfH + strip * 0.5f + 0.01f);
                p.Room = strip;
                return p;
            }
            float boardHalf = MainBoardWorldSize * 0.5f + BoardView.BorderOverhang * 0.5f;
            Vector2 board = MainBoardCenter;
            float boardTop = board.y + boardHalf;
            if (layout.BarsAsRow)
            {
                // a phone: the wide strip over the board, under the bar rows, as big as the room
                // between them allows (and never smaller than reads)
                int jokers = session != null ? session.Jokers.Count : 0;
                int powers = session != null ? session.Powers.Count : 0;
                float hudBottom = c.y + halfH - layout.HudBottomWorld(jokers, powers);
                float free = hudBottom - boardTop - 0.1f;
                p.Form = DebtLedgerView.Form.Wide;
                p.Scale = Mathf.Clamp(free / wideH, 0.7f, 1f);
                p.Scale = Mathf.Min(p.Scale, halfW * 1.88f / wideW);
                p.Centre = new Vector2(board.x, boardTop + 0.06f + wideH * p.Scale * 0.5f);
                p.Room = free;
                return p;
            }
            // the desktop: in the gap between the board's right edge and the joker column
            float canvasToWorld = 2f * halfW / Mathf.Max(1f, layout.CanvasReference.x);
            float column = (layout.JokerColumns * layout.JokerPanel.x
                + (layout.JokerColumns - 1) * layout.JokerGap + layout.CornerInset) * canvasToWorld;
            float left = board.x + boardHalf + LedgerBoardGap;
            float right = c.x + halfW - column - LedgerColumnGap;
            float room = right - left;
            p.Room = room;
            if (room >= wideW * 0.86f)
            {
                p.Form = DebtLedgerView.Form.Wide;
                p.Width = wideW;
                p.Scale = Mathf.Min(1f, room / wideW);
            }
            else
            {
                p.Form = DebtLedgerView.Form.Stacked;
                p.Width = Mathf.Clamp(room / 0.88f, DebtLedgerView.Style.StackedMinWidth,
                    DebtLedgerView.Style.StackedMaxWidth);
                // A screen too narrow to hold even the narrow form keeps it readable and lets it
                // run under the joker column rather than shrink to nothing.
                p.Scale = Mathf.Clamp(room / p.Width, 0.62f, 1f);
            }
            Vector2 size = DebtLedgerView.LocalSize(p.Form, p.Width) * p.Scale;
            p.Centre = new Vector2(left + size.x * 0.5f, boardTop - LedgerTopDrop - size.y * 0.5f);
            return p;
        }

        private void PlaceLedger()
        {
            float halfH = cam.orthographicSize;
            LedgerPlacement p = SolveLedgerPlacement(halfH * cam.aspect, halfH,
                session.Phase == GamePhase.Market);
            ledgerView.SetPlacement(p.Form, p.Width, p.Centre, p.Scale);
        }

        /// <summary>
        /// WHERE THE SCORE LINE'S PARTS ARE, in the world: the TOTAL's number and the round's
        /// "raunt 0 / 1548". Measured off the real label with its own text generator, because
        /// it is canvas text centred on a rect and the parts move whenever a number changes.
        /// False when there is no line to measure.
        /// </summary>
        private bool ScoreHeaderRects(out Rect number, out Rect roundPart, out float rightLimit)
        {
            number = roundPart = new Rect();
            rightLimit = 0f;
            if (totalText == null || !totalText.gameObject.activeInHierarchy
                || string.IsNullOrEmpty(scoreHudNumber) || string.IsNullOrEmpty(scoreHudRound))
            {
                return false;
            }
            TextGenerator gen = totalText.cachedTextGeneratorForLayout;
            TextGenerationSettings settings = totalText.GetGenerationSettings(Vector2.zero);
            float ppu = Mathf.Max(0.0001f, totalText.pixelsPerUnit);
            float scale = totalText.rectTransform.lossyScale.x;
            string lead = scoreHudLead + scoreHudNumber;
            float wLead = gen.GetPreferredWidth(lead, settings) / ppu;
            float wNumber = gen.GetPreferredWidth(scoreHudNumber, settings) / ppu;
            float wFull = gen.GetPreferredWidth(totalText.text, settings) / ppu;
            float wRound = gen.GetPreferredWidth(scoreHudRound, settings) / ppu;
            var corners = new Vector3[4];
            totalText.rectTransform.GetWorldCorners(corners);
            float centreX = (corners[0].x + corners[2].x) * 0.5f;
            float leftX = centreX - wFull * scale * 0.5f;
            float font = totalText.fontSize * scale;
            float top = corners[1].y - font * 0.12f;
            float bottom = corners[1].y - font * 1.02f;
            number = ScreenRectToWorld(leftX + (wLead - wNumber) * scale, bottom, leftX + wLead * scale, top);
            float roundRight = leftX + wFull * scale;
            roundPart = ScreenRectToWorld(roundRight - wRound * scale, bottom, roundRight, top);
            UiLayout layout = UiLayout.Active;
            Rect view = cam.pixelRect;
            float limitPx;
            if (layout.BarsAsRow)
            {
                limitPx = view.xMax - 16f * scale;
            }
            else
            {
                float column = layout.JokerColumns * layout.JokerPanel.x
                    + (layout.JokerColumns - 1) * layout.JokerGap + layout.CornerInset;
                limitPx = view.xMax - (column + 16f) * scale;
            }
            rightLimit = cam.ScreenToWorldPoint(new Vector3(limitPx, bottom,
                Mathf.Abs(cam.transform.position.z))).x;
            return wFull > 1f;
        }

        private Rect ScreenRectToWorld(float x0, float y0, float x1, float y1)
        {
            float z = Mathf.Abs(cam.transform.position.z);
            Vector3 a = cam.ScreenToWorldPoint(new Vector3(x0, y0, z));
            Vector3 b = cam.ScreenToWorldPoint(new Vector3(x1, y1, z));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>The TOTAL's NUMBER, where a carried debt lands and the bank's thanks go -
        /// measured, falling back to the label's anchor when there is no line to measure.</summary>
        private Vector2 CreditTotalAnchor()
        {
            Rect number, roundPart;
            float limit;
            return ScoreHeaderRects(out number, out roundPart, out limit) ? number.center : ScoreWorldAnchor();
        }

        /// <summary>The round's target on the score line, where a round's payment leaves from.</summary>
        private Vector2 CreditRoundAnchor()
        {
            Rect number, roundPart;
            float limit;
            return ScoreHeaderRects(out number, out roundPart, out limit) ? roundPart.center : ScoreWorldAnchor();
        }

        /// <summary>Where market money comes from on screen - the purse line of the shelf.</summary>
        private Vector2 MarketPaymentAnchor()
        {
            return (Vector2)cam.transform.position + new Vector2(0f, -1.5f);
        }

        private Vector2 ForeclosureLedgerAt()
        {
            UiLayout layout = UiLayout.Active;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector2 c = cam.transform.position;
            if (layout.BarsAsRow)
            {
                return new Vector2(c.x, c.y - halfH + 2.1f);
            }
            float column = (layout.PowerColumns * layout.PowerPanel.x + layout.CornerInset) * CanvasToWorld;
            return new Vector2(c.x - halfW + column + 0.15f + ForeclosureView.Style.LedgerWidth * 0.5f,
                c.y - 0.2f);
        }

        private float ForeclosureLedgerScale()
        {
            return UiLayout.Active.BarsAsRow ? 0.8f : 1f;
        }

        /// <summary>Remembers where every joker's and power's panel stands, by instance id - the
        /// bailiff's proxies leave from there after Core has already taken the thing.</summary>
        private void CacheBarAnchors()
        {
            if (session.Jokers == null)
            {
                return;
            }
            float z = Mathf.Abs(cam.transform.position.z);
            for (int i = 0; i < session.Jokers.Count; i++)
            {
                Vector2? screenPos = jokerBar.PanelScreenCenter(i);
                if (screenPos.HasValue)
                {
                    Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPos.Value.x, screenPos.Value.y, z));
                    jokerPanelWorld[session.Jokers.Jokers[i].InstanceId] = w;
                }
            }
            for (int i = 0; i < session.Powers.Count; i++)
            {
                Vector2? screenPos = powerBar.PanelScreenCenter(i);
                if (screenPos.HasValue)
                {
                    Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPos.Value.x, screenPos.Value.y, z));
                    powerPanelWorld[session.Powers.Powers[i].InstanceId] = w;
                }
            }
        }

        /// <summary>The statement's seizures, in Core's order, each with where it stood.</summary>
        private List<ForeclosureView.Asset> ForeclosureAssets(CreditStatement statement)
        {
            var list = new List<ForeclosureView.Asset>();
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector2 c = cam.transform.position;
            foreach (SeizedItem item in statement.Seized)
            {
                Vector2 source;
                if (item.Kind == SeizedKind.Joker)
                {
                    source = jokerPanelWorld.TryGetValue(item.JokerInstanceId, out Vector2 j)
                        ? j : c + new Vector2(halfW - 1.6f, halfH - 1.3f);
                }
                else if (item.Kind == SeizedKind.Power)
                {
                    source = powerPanelWorld.TryGetValue(item.PowerInstanceId, out Vector2 p)
                        ? p : c + new Vector2(-halfW + 1f, halfH - 1.2f);
                }
                else
                {
                    // blocks come out of the deck
                    source = CardLayerView.DrawPilePos;
                }
                list.Add(new ForeclosureView.Asset { Item = item, Source = source });
            }
            return list;
        }

        // ------------------------------------------------------------------ sound

        /// <summary>The ledger's moments onto the sounds - and onto the pressure, which answers
        /// the same moments on the rest of the screen.</summary>
        private void OnLedgerCue(DebtLedgerView.Cue cue)
        {
            if (debtPressure != null)
            {
                switch (cue)
                {
                    case DebtLedgerView.Cue.Payment: debtPressure.Relieve(ledgerView.LastPaymentShare); break;
                    case DebtLedgerView.Cue.MinimumSatisfied: debtPressure.MinimumRelief(); break;
                    case DebtLedgerView.Cue.Interest: debtPressure.Tighten(ledgerView.LastInterestShare); break;
                    case DebtLedgerView.Cue.CarryLanded:
                        creditCarryLanded = true;
                        debtPressure.CarryImpact();
                        break;
                }
            }
            if (sfx == null)
            {
                return;
            }
            switch (cue)
            {
                // Open is silent: the purchase that opened the loan already swiped the card.
                case DebtLedgerView.Cue.Seal: sfx.Drum(0.8f); break;
                case DebtLedgerView.Cue.Payment: sfx.Pickup(); break;
                case DebtLedgerView.Cue.MinimumSatisfied: sfx.DebtClick(); break;
                case DebtLedgerView.Cue.Interest: sfx.Drum(0.6f); break;
                // The stamp's dry thud. The design's "one medium-light dry tap" of haptics goes on
                // this same cue once the game has a haptics layer.
                case DebtLedgerView.Cue.FinalDue: sfx.DebtThud(); break;
                case DebtLedgerView.Cue.TermTick: sfx.DebtTick(); break;
                case DebtLedgerView.Cue.Settled: sfx.DebtPaid(); break;
                case DebtLedgerView.Cue.Bonus: sfx.Chime(1.2f); break;
            }
        }

        private void OnPressureCue(DebtPressurePresentationController.Cue cue)
        {
            if (sfx == null)
            {
                return;
            }
            switch (cue)
            {
                case DebtPressurePresentationController.Cue.Creak: sfx.DebtCreak(); break;
                case DebtPressurePresentationController.Cue.Release: sfx.DebtRelease(); break;
            }
        }

        private void OnForeclosureCue(ForeclosureView.Cue cue)
        {
            if (sfx == null)
            {
                return;
            }
            switch (cue)
            {
                case ForeclosureView.Cue.Stamp: sfx.Foreclose(); break;
                case ForeclosureView.Cue.Appraise: sfx.Pickup(); break;
                case ForeclosureView.Cue.Ribbon: sfx.Squish(); break;
                case ForeclosureView.Cue.Clack: sfx.Drum(1.2f); break;
                case ForeclosureView.Cue.Transfer: sfx.Pluck(1.1f); break;
                case ForeclosureView.Cue.Cleared: sfx.DebtPaid(); break;
            }
        }

        // ------------------------------------------------------------------ the words

        /// <summary>The loan in one line: debt, term left, minimum payment, interest.</summary>
        private string CreditDebtLine()
        {
            var sb = new StringBuilder();
            sb.Append(Loc.Pick("DEBT ", "BORÇ ")).Append(session.Debt);
            int left = session.CreditTermLeft;
            if (left <= 1)
            {
                sb.Append(Loc.Pick("  ·  LAST STAGE BEFORE FORECLOSURE", "  ·  SON AŞAMA, SONRA HACİZ"));
            }
            else
            {
                sb.Append(Loc.Pick("  ·  term " + left + " stages", "  ·  vade " + left + " aşama"));
            }
            RoundEngine round = session.CurrentRound;
            if (session.Phase == GamePhase.Market)
            {
                sb.Append(Loc.Pick("  ·  next stage's minimum ", "  ·  sonraki asgari "))
                    .Append(session.NextStageMinimumPayment);
            }
            else if (round != null && round.CreditInstallment > 0)
            {
                sb.Append(Loc.Pick("  ·  minimum ", "  ·  asgari ")).Append(round.CreditInstallment);
            }
            sb.Append(Loc.Pick("  ·  interest ", "  ·  faiz ")).Append(DebtLedgerView.Rate(session.CreditInterestPermille));
            return sb.ToString();
        }

        /// <summary>True when this statement is the one for the market that is open now - the
        /// stage it settled is the stage the market follows.</summary>
        private bool StatementIsForThisMarket(CreditStatement statement)
        {
            return statement != null && session != null && session.Phase == GamePhase.Market
                && statement.RoundNumber == session.RoundNumber
                && statement.BossStage == session.InBossStage;
        }

        /// <summary>What the stage settled, in one line for the market's message area, or empty.</summary>
        private string CreditStatementLine(CreditStatement statement)
        {
            if (!StatementIsForThisMarket(statement))
            {
                return string.Empty;
            }
            if (statement.Foreclosed)
            {
                int jokers = 0, powers = 0, blocks = 0;
                long fetched = 0;
                foreach (SeizedItem item in statement.Seized)
                {
                    fetched += item.Credited;
                    switch (item.Kind)
                    {
                        case SeizedKind.Joker: jokers++; break;
                        case SeizedKind.Power: powers++; break;
                        default: blocks++; break;
                    }
                }
                string line = Loc.Pick(
                    "FORECLOSED: " + jokers + " jokers, " + powers + " powers, " + blocks
                        + " blocks taken for " + fetched,
                    "HACİZ: " + jokers + " joker, " + powers + " güç, " + blocks + " blok "
                        + fetched + " karşılığında alındı");
                if (statement.WrittenOff > 0)
                {
                    line += Loc.Pick("  ·  " + statement.WrittenOff + " written off",
                        "  ·  kalan " + statement.WrittenOff + " silindi");
                }
                return line;
            }
            switch (statement.Reward)
            {
                case BankRewardKind.Points:
                    return Loc.Pick("BANK: paid on time, bonus +" + statement.RewardPoints,
                        "BANKA: zamanında ödeme bonusu +" + statement.RewardPoints);
                case BankRewardKind.Campaign:
                    return statement.CampaignOffer != null
                        ? Loc.Pick("BANK CAMPAIGN: " + OfferName(statement.CampaignOffer)
                                + " is " + statement.CampaignPercent + "% off on the card",
                            "BANKA KAMPANYASI: " + OfferName(statement.CampaignOffer)
                                + " kartla %" + statement.CampaignPercent + " indirimli")
                        : string.Empty;
            }
            if (statement.DebtAfter <= 0)
            {
                return Loc.Pick("Debt cleared", "Borç kapandı");
            }
            return Loc.Pick(
                "The stage paid " + statement.Repaid + "  ·  interest +" + statement.Interest
                    + "  ·  owed " + statement.DebtAfter,
                "Aşama " + statement.Repaid + " ödedi  ·  faiz +" + statement.Interest
                    + "  ·  kalan borç " + statement.DebtAfter);
        }

        private static string OfferName(MarketOffer offer)
        {
            switch (offer.Kind)
            {
                case MarketOfferKind.Joker:
                    return offer.Joker != null ? offer.Joker.DisplayName : "?";
                case MarketOfferKind.Power:
                    return offer.Power != null ? offer.Power.DisplayName : "?";
                default:
                    return Loc.Pick("a block", "bir blok");
            }
        }
    }
}
