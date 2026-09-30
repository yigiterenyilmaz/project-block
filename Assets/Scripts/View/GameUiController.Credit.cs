// PURPOSE: "Kredi kartı" on screen, as a LOAN (GameSession.Credit.cs). Three things:
//   THE DEBT LINE   what is owed, how many stages are left on the term, and what the minimum
//                   payment is - this stage's in a round, the next stage's in the market. The last
//                   stage before the bailiff says so in capitals.
//   THE STATEMENT   what the stage that just ended settled (CreditStatement): what it paid off,
//                   the interest, the bank's thanks (points, or a campaign on an offer in this very
//                   market), or the bailiff's list. Written into the market's message line for as
//                   long as that market is open.
//   THE MOMENT      the statement is PLAYED once - matched by identity, never by value, so a shop
//                   rebuilt a dozen times (every purchase rebuilds it) plays it once, and a new
//                   statement with the same numbers still plays. A foreclosure gets the HACİZ stamp
//                   (DebtFxView, which used to be the run-ending debt's own ending), a reward the
//                   relief sound and a line over what it went to.
// The View decides nothing: every number is the statement's or the session's.

using System.Text;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>The statement last played. Identity, not value (see the header).</summary>
        private CreditStatement lastCreditShown;

        /// <summary>The loan in one line: debt, term left, minimum payment, interest.</summary>
        private string CreditDebtLine()
        {
            MarketConfig terms = session.Config.Market;
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
                long next = (session.Debt * terms.CreditMinimumPaymentPercent + 99) / 100;
                sb.Append(Loc.Pick("  ·  next stage's minimum ", "  ·  sonraki asgari ")).Append(next);
            }
            else if (round != null && round.CreditInstallment > 0)
            {
                sb.Append(Loc.Pick("  ·  minimum ", "  ·  asgari ")).Append(round.CreditInstallment);
            }
            string rate = (terms.CreditInterestPermille / 10) + (terms.CreditInterestPermille % 10 != 0
                ? Loc.Pick(".", ",") + (terms.CreditInterestPermille % 10) : string.Empty);
            sb.Append(Loc.Pick("  ·  interest " + rate + "%", "  ·  faiz %" + rate));
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

        /// <summary>Plays the statement for the market that just opened, once.</summary>
        private void CheckCreditStatement()
        {
            CreditStatement statement = session != null ? session.LastCreditStatement : null;
            if (ReferenceEquals(statement, lastCreditShown) || !StatementIsForThisMarket(statement))
            {
                return;
            }
            lastCreditShown = statement;
            if (statement.Foreclosed)
            {
                foreach (SeizedItem item in statement.Seized)
                {
                    Debug.Log("[block_bonk] Foreclosed " + item.Kind + " " + item.Name + " (worth "
                        + item.Value + ") for " + item.Credited);
                }
                EnsureDebtFx();
                sfx.Foreclose();
                float halfH = cam.orthographicSize;
                debtFx.PlayForeclosure(Vector2.zero, ScoreWorldAnchor(), halfH * cam.aspect, halfH);
                // What was taken is gone from the bars too.
                jokerBar.Refresh(session, null);
                powerBar.Refresh(session, null);
                return;
            }
            if (statement.Reward == BankRewardKind.Points)
            {
                sfx.DebtPaid();
                FloatingTextFx.Spawn(transform, new Vector2(0f, 1.6f),
                    Loc.Pick("PAID ON TIME  +", "ZAMANINDA ÖDEME  +") + statement.RewardPoints,
                    new Color(0.45f, 0.95f, 0.55f), 58, 0.08f);
                return;
            }
            if (statement.Reward == BankRewardKind.Campaign && statement.CampaignOffer != null)
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
                return;
            }
            if (statement.DebtAfter <= 0)
            {
                sfx.DebtPaid();
                FloatingTextFx.Spawn(transform, new Vector2(0f, 1.6f),
                    Loc.Pick("DEBT CLEARED", "BORÇ KAPANDI"),
                    new Color(0.45f, 0.95f, 0.55f), 58, 0.08f);
            }
        }
    }
}
