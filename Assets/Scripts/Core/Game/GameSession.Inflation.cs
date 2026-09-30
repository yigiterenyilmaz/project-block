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
//     BankRoundScore splits it, and two of its three parts are deflated by
//     original bar / inflated bar:
//       - the round's OWN share (up to its own bar): reaching an inflated 3000 on a round that
//         began at 1000 pays 1000. With a debt open it goes nowhere, as it never does.
//       - a credit MINIMUM (the band between the own bar and the pass bar) is NOT deflated: it is
//         a fixed payment on a real debt, not inflated money.
//       - OVERTIME (past the pass bar): every point at the same rate, so 600 earned past a bar
//         inflated 1000 -> 3000 pays 200. The bar stops moving once it is passed, so the rate is
//         the one the round ended its normal play at.
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

        /// <summary>The credit minimum's band, paid as it stood (never deflated).</summary>
        public long MinimumPaid;

        /// <summary>What overtime earned past the bar, and what it paid once deflated.</summary>
        public long OvertimeEarned;
        public long OvertimePaid;

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
            long meter = Math.Max(0L, (long)round.RoundScore);
            long ownNow = Math.Max(0L, (long)round.OwnBar);
            long ownThen = Math.Max(0L, (long)round.UninflatedOwnBar);
            long installment = Math.Max(0L, (long)round.CreditInstallment);

            long ownEarned = Math.Min(meter, ownNow);
            long band = Math.Max(0L, Math.Min(meter, ownNow + installment) - ownNow);
            long overtime = Math.Max(0L, meter - ownNow - installment);

            long ownWorth = Deflate(ownEarned, ownThen, ownNow);
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
                MinimumPaid = band,
                OvertimeEarned = overtime,
                OvertimePaid = overtimeWorth
            };
            GrantCurrency(ownPaid + band + overtimeWorth);
            LastInflationSettlement = settlement;
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
