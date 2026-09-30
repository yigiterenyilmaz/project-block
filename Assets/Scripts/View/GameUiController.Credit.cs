// PURPOSE: "Kredi kartı" on screen - the controller half of the BLACK LEDGER (DebtLedgerView) and
// HACİZ (ForeclosureView). The rules are GameSession.Credit.cs; this reads them and tells them.
//
//   PLACEMENT      the ledger stands in the gap between the board and the joker column during a
//                  round, in the free strip under the market panel during the market, and over
//                  the board on a phone. Never over the grid.
//   EVENTS         are READ, never inferred from arithmetic: a payment is Core's own
//                  DebtRepaidThisStage moving, a loan is Debt rising, a new stage is a new round
//                  engine, and a stage's settlement is the CreditStatement object Core wrote
//                  (matched by identity). The statement is told IN ORDER - the last payment, the
//                  interest, the foreclosure, the settlement or the bank's thanks - each waiting
//                  for the one before it.
//   THE BARS       live on the overlay canvas, so their panels' world positions are remembered
//                  every frame by instance id: by the time a foreclosure is told, Core has already
//                  taken the jokers, and the proxies have to leave from where they stood.
//   THE TARGET     the round's bar grows by the minimum payment, so a small debt chip "+500" sits
//                  beside the score line (canvas text) to say why.
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

        /// <summary>The statement last told. Identity, not value.</summary>
        private CreditStatement lastCreditShown;

        private GameSession creditSessionSeen;
        private RoundEngine creditRoundSeen;
        private long creditDebtSeen;
        private long creditRepaidShown;
        private bool creditPresenting;

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
            ledgerView.ScoreAnchor = ScoreWorldAnchor;
            ledgerView.Sounded = OnLedgerCue;

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
                creditPresenting = false;
                ledgerView.Close(false);
                foreclosureView.Stop();
            }
            if (!creditPresenting)
            {
                CacheBarAnchors();
            }
            if (creditLabOwnsViews)
            {
                return;
            }
            PlaceLedger();
            if (screen != AppScreen.Playing)
            {
                // A menu owns the frame: the ledger steps out of it without stopping anything.
                ledgerView.SetPlacement(ledgerView.CentreWorld, 0.0001f);
            }
            bool inRound = session.Phase == GamePhase.Round && session.CurrentRound != null;
            DebtLedgerView.State state = CreditLedgerState();
            ledgerView.SetTargetChip(inRound && session.CurrentRound.CreditInstallment > 0 && screen == AppScreen.Playing
                ? TargetChipAnchor() : (Vector2?)null,
                inRound ? session.CurrentRound.CreditInstallment : 0);
            foreclosureView.SetFinalDueAtmosphere(inRound && state.Deadline == CreditDeadline.FinalDue ? 1f : 0f);
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

            // A new stage: the debt carried into it.
            if (inRound && !ReferenceEquals(session.CurrentRound, creditRoundSeen))
            {
                creditRoundSeen = session.CurrentRound;
                creditRepaidShown = session.DebtRepaidThisStage;
                creditDebtSeen = session.Debt;
                if (session.Debt > 0)
                {
                    ledgerView.ShowState(state);
                    ledgerView.PlayCarry(state);
                    if (state.Deadline == CreditDeadline.FinalDue)
                    {
                        ledgerView.PlayFinalDueStamp();
                    }
                }
                return;
            }

            // Money in: Core's own count of what this stage has paid moved.
            long repaid = session.DebtRepaidThisStage;
            if (repaid > creditRepaidShown && ledgerView.IsOpen)
            {
                ledgerView.PlayPayment(repaid - creditRepaidShown,
                    inRound ? ScoreWorldAnchor() : MarketPaymentAnchor(), state);
            }
            creditRepaidShown = repaid;

            // Money borrowed: a new contract, or more on the open one.
            if (session.Debt > creditDebtSeen)
            {
                if (!ledgerView.IsOpen)
                {
                    ledgerView.PlayOpen(state);
                }
                else
                {
                    ledgerView.PlayBorrow(creditDebtSeen, state);
                }
            }
            creditDebtSeen = session.Debt;

            // Paid off without a statement (mid-stage, or by a sale): the clean close.
            if (session.Debt <= 0 && ledgerView.IsOpen && !ledgerView.Busy)
            {
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
                ledgerView.PlayPayment(unshown, ScoreWorldAnchor(), paid);
                yield return WaitForLedger();
            }
            creditRepaidShown = 0;
            // 2. the interest
            if (statement.Interest > 0)
            {
                DebtLedgerView.State charged = CreditLedgerState();
                charged.Debt = statement.DebtBeforeInterest + statement.Interest;
                ledgerView.PlayInterest(statement.DebtBeforeInterest, statement.Interest, charged);
                yield return WaitForLedger();
            }
            // 3. the bailiff
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
            // 4. settled - on time, with the bank's thanks, or after the bailiff
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

        // ------------------------------------------------------------------ where things are

        private float CanvasToWorld
        {
            get
            {
                float halfW = cam.orthographicSize * cam.aspect;
                return 2f * halfW / Mathf.Max(1f, UiLayout.Active.CanvasReference.x);
            }
        }

        private void PlaceLedger()
        {
            UiLayout layout = UiLayout.Active;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector2 c = cam.transform.position;
            if (session.Phase == GamePhase.Market)
            {
                // the free strip under the market panel, between the piles
                float width = Mathf.Min(2.5f, halfW * 1.1f);
                ledgerView.SetPlacement(new Vector2(c.x, c.y - halfH + 0.44f), width);
                return;
            }
            if (layout.BarsAsRow)
            {
                float width = Mathf.Min(2.6f, halfW * 1.5f);
                ledgerView.SetPlacement(new Vector2(MainBoardCenter.x,
                    MainBoardCenter.y + MainBoardWorldSize * 0.5f + 0.45f), width);
                return;
            }
            // the gap between the board's right edge and the joker column
            float column = (layout.JokerColumns * layout.JokerPanel.x
                + (layout.JokerColumns - 1) * layout.JokerGap + layout.CornerInset) * CanvasToWorld;
            float left = MainBoardCenter.x + MainBoardWorldSize * 0.5f + 0.15f;
            float right = c.x + halfW - column - 0.15f;
            float w = Mathf.Clamp(right - left, 1.5f, 2.6f);
            ledgerView.SetPlacement(new Vector2((left + right) * 0.5f,
                MainBoardCenter.y + MainBoardWorldSize * 0.5f - 0.55f), w);
        }

        /// <summary>The right end of the score line, where the round target ends.</summary>
        private Vector2 TargetChipAnchor()
        {
            var corners = new Vector3[4];
            totalText.rectTransform.GetWorldCorners(corners);
            float scale = totalText.rectTransform.lossyScale.x;
            float centreX = (corners[0].x + corners[2].x) * 0.5f;
            float right = centreX + totalText.preferredWidth * scale * 0.5f + 14f * scale;
            float y = corners[1].y - totalText.fontSize * scale * 0.62f;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(right, y, Mathf.Abs(cam.transform.position.z)));
            return new Vector2(world.x, world.y);
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

        /// <summary>The ledger's moments onto the sounds the game has. The dedicated paper and
        /// stamp clips do not exist yet; each of these is the hook one goes on.</summary>
        private void OnLedgerCue(DebtLedgerView.Cue cue)
        {
            if (sfx == null)
            {
                return;
            }
            switch (cue)
            {
                // Open is silent: the purchase that opened the loan already swiped the card.
                case DebtLedgerView.Cue.Seal: sfx.Drum(0.8f); break;
                case DebtLedgerView.Cue.Payment: sfx.Pickup(); break;
                case DebtLedgerView.Cue.MinimumSatisfied: sfx.Pluck(1.3f); break;
                case DebtLedgerView.Cue.Interest: sfx.Drum(0.6f); break;
                case DebtLedgerView.Cue.FinalDue: sfx.Drum(0.5f); break;
                case DebtLedgerView.Cue.Settled: sfx.DebtPaid(); break;
                case DebtLedgerView.Cue.Bonus: sfx.Chime(1.2f); break;
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
