// PURPOSE: GameSession - a round whose money is HELD BACK and paid out DEFLATED ("Enflasyon",
// designer's call 2026-09-30). The boss inflates the bar turn by turn; this is the other half of
// inflation: what the round earns is worth less by the time it is paid.
//
//   HELD, NOT BANKED. While the round's boss says so (BossRound.DefersRoundPayout, asked through
//     RoundEngine.DefersPayout), what the meter banks goes neither to the purse nor to a debt.
//     Every place that moves money along with the meter leaves the purse alone for such a round
//     (BankRoundScore holds it; the charges, the halving, the caps and the final-round replay
//     skip their claw-back), so the METER is the one thing that says what the round earned.
//   PAID WHEN IT ENDS, AT THE ORIGINAL BAR'S VALUE. On the round's end (RoundStatus.Advanced -
//     reaching the bar, or a boss beaten on its own terms) the meter is split exactly as
//     BankRoundScore splits it, and ALL THREE parts are deflated by original bar / inflated bar:
//       - the round's OWN share (up to its own bar): reaching an inflated 3000 on a round that
//         began at 1000 pays 1000. With a debt open it goes nowhere, as it never does.
//       - a credit MINIMUM (the band between the own bar and the pass bar): the band itself is a
//         FIXED addition on the meter (never inflated, never feeding the rise - EnflasyonBoss),
//         but the points earned in it are inflated points like any other, so they pay the debt
//         their REAL worth (designer's call, 2026-09-30): a 300 minimum earned on a bar inflated
//         1000 -> 3000 pays 100 off the debt. The minimum is then only partly paid in money -
//         which nothing in the rules reads; the ledger simply tells it.
//       - OVERTIME (past the pass bar): every point at the same rate, so 600 earned past a bar
//         inflated 1000 -> 3000 pays 200.
//     One rate for all three: the bar stops rising the moment the OWN bar is reached, so the
//     inflation "up to then" is the inflation at the end.
//     Rounded DOWN - inflation never pays a point more than it should.
//   LOST, FORFEITED. A round that is lost pays nothing; there was never anything in the purse to
//     take back.
//   THE BOOKS BALANCE. What the round holds is booked as taken by an effect as it is banked, and
//     the payout is granted back through GrantCurrency (which pays a debt first) - so TotalScore
//     is still "every turn + sales + grants - taken" at every moment, mid-round included.
//
// Nothing here is saved: what is held is the round's meter, which the round saves itself.

using System;

namespace ProjectBlock.Core
{
    /// <summary>What a round that held its money back paid out when it ended - reporting only, a
    /// NEW object per settlement so the View can match it by identity, never saved.</summary>
    public sealed class InflationSettlement
    {
        public int RoundNumber;
        public bool BossStage;

        /// <summary>The meter the round ended on (scaled).</summary>
        public long Meter;

        /// <summary>The round's own bar before and after inflation (scaled).</summary>
        public long OwnBarThen;
        public long OwnBarNow;

        /// <summary>The round's own share of the meter, and what it paid (0 while in debt).</summary>
        public long OwnEarned;
        public long OwnPaid;

        /// <summary>What the credit minimum's band earned on the meter, and what it paid off the
        /// debt once deflated.</summary>
        public long MinimumEarned;
        public long MinimumPaid;

        /// <summary>What overtime earned past the bar, and what it paid once deflated.</summary>
        public long OvertimeEarned;
        public long OvertimePaid;

        /// <summary>What the settlement actually took off the DEBT (the minimum and overtime pay it
        /// first; more than is owed goes on to the purse), and the inflated points that paid it -
        /// what the View shows leaving the round before the inflation is burned off it.</summary>
        public long DebtRepaid;
        public long DebtRepaidEarned;

        /// <summary>Everything the round paid out.</summary>
        public long TotalPaid
        {
            get { return OwnPaid + MinimumPaid + OvertimePaid; }
        }
    }

    partial class GameSession
    {
        /// <summary>The last settlement of a round that held its money back, or null.</summary>
        public InflationSettlement LastInflationSettlement { get; private set; }

        /// <summary>A round that holds its money back banks nothing: the amount is booked as taken
        /// by the effect (net - a negative amount gives the booking back), and the meter keeps
        /// the count.</summary>
        private void HoldRoundScore(long amount)
        {
            CurrencyTakenByEffects += amount;
        }

        /// <summary>Pays out a round that held its money back, deflated to what the round's
        /// original bar was worth. Does nothing for an ordinary round.</summary>
        private void SettleHeldRoundScore()
        {
            RoundEngine round = CurrentRound;
            if (round == null || !round.DefersPayout)
            {
                return;
            }
            // "Blackjack": the meter was never money. It was held like any deferred round (so
            // the books balance) and is forfeited - the duel paid the purse in bets.
            if (round.Boss != null && round.Boss.RoundScoreIsNotMoney)
            {
                return;
            }
            long meter = Math.Max(0L, (long)round.RoundScore);
            long ownNow = Math.Max(0L, (long)round.OwnBar);
            long ownThen = Math.Max(0L, (long)round.UninflatedOwnBar);
            long installment = Math.Max(0L, (long)round.CreditInstallment);

            long ownEarned = Math.Min(meter, ownNow);
            long band = Math.Max(0L, Math.Min(meter, ownNow + installment) - ownNow);
            long overtime = Math.Max(0L, meter - ownNow - installment);

            // One rate for all three parts - the inflation up to the moment the own bar was
            // reached, which is the inflation at the end (the bar stops rising there).
            long ownWorth = Deflate(ownEarned, ownThen, ownNow);
            long bandWorth = Deflate(band, ownThen, ownNow);
            long overtimeWorth = Deflate(overtime, ownThen, ownNow);
            // The round's own share goes nowhere while a debt is open - the same rule as every
            // other round (BankRoundScore); the minimum and overtime pay the debt first.
            long ownPaid = Debt <= 0 ? ownWorth : 0;

            var settlement = new InflationSettlement
            {
                RoundNumber = RoundNumber,
                BossStage = InBossStage,
                Meter = meter,
                OwnBarThen = ownThen,
                OwnBarNow = ownNow,
                OwnEarned = ownEarned,
                OwnPaid = ownPaid,
                MinimumEarned = band,
                MinimumPaid = bandWorth,
                OvertimeEarned = overtime,
                OvertimePaid = overtimeWorth
            };
            long debtBefore = Debt;
            GrantCurrency(ownPaid + bandWorth + overtimeWorth);
            // While in debt only the minimum and overtime pay (the own share goes nowhere), so
            // what the debt lost is theirs; the inflated points behind it follow in proportion
            // when the debt was smaller than what they were worth.
            long repaid = Math.Max(0L, debtBefore - Debt);
            long net = bandWorth + overtimeWorth;
            long gross = band + overtime;
            settlement.DebtRepaid = repaid;
            settlement.DebtRepaidEarned = repaid >= net ? gross
                : net > 0 ? (gross * repaid + net - 1) / net : 0;
            LastInflationSettlement = settlement;
        }

        /// <summary>THE LAB'S SEAM, and the one definition of the deflation: what
        /// <paramref name="amount"/> points earned against a bar inflated from
        /// <paramref name="barThen"/> to <paramref name="barNow"/> are really worth.</summary>
        public static long DeflatedWorth(long amount, long barThen, long barNow)
        {
            return Deflate(amount, barThen, barNow);
        }

        /// <summary>What <paramref name="amount"/> inflated points are worth at the original bar,
        /// rounded down. A bar that never moved (or a zero bar) deflates nothing.</summary>
        private static long Deflate(long amount, long barThen, long barNow)
        {
            if (amount <= 0 || barNow <= 0 || barThen >= barNow)
            {
                return Math.Max(0L, amount);
            }
            return amount * barThen / barNow;
        }
    }
}
