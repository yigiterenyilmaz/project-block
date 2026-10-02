// PURPOSE: GameSession - the PURSE at the duel table ("Blackjack"). The duel is the one boss stage
// played for the player's own money: the bet comes out of the purse, a won hand pays it back
// twice, and the stage is beaten when the purse has doubled. The round's own points never reach
// the purse there (BossRound.RoundScoreIsNotMoney) - they only decide who wins each hand.
//
// THE DUEL'S MONEY IS THE PURSE, NEVER THE DEBT. A bet is taken from TotalScore and a payout is
// put straight back into it, booked as taken / granted by an effect so the books balance. It does
// not go through Receive: a payout paying a "Kredi kartı" debt first would leave a player in debt
// unable ever to double a purse that stays at zero. (The purse can therefore stand above zero
// while a debt is open during this stage - the one exception to that invariant.)

namespace ProjectBlock.Core
{
    partial class GameSession
    {
        /// <summary>The duel boss of the stage being played, or null.</summary>
        public BlackjackBoss DuelBoss
        {
            get { return CurrentRound != null ? CurrentRound.Boss as BlackjackBoss : null; }
        }

        /// <summary>
        /// "Blackjack": stakes <paramref name="amount"/> of the purse on the next hand and deals it.
        /// False - and nothing changes - when there is no duel waiting for a bet, or the amount is
        /// not between 1 and what the purse holds.
        /// </summary>
        public bool PlaceDuelBet(long amount)
        {
            BlackjackBoss duel = DuelBoss;
            if (duel == null || Phase != GamePhase.Round || CurrentRound.Status != RoundStatus.InProgress)
            {
                return false;
            }
            return duel.PlaceBet(new RoundContext(this, rng, CurrentRound), amount);
        }

        /// <summary>Takes a stake out of the purse. False when the purse cannot cover it.</summary>
        internal bool TakeDuelStake(long amount)
        {
            if (amount <= 0 || amount > TotalScore)
            {
                return false;
            }
            TotalScore -= amount;
            CurrencyTakenByEffects += amount;
            return true;
        }

        /// <summary>TEST SEAM: puts a card into the run's deck, as a market purchase would but at
        /// no price - the duel's tests need elemental cards in the deck it cuts.</summary>
        internal void AddOwnedCardForTests(BlockCard card)
        {
            ownedCards.Add(card);
        }

        /// <summary>Puts duel money straight into the purse (a payout, the house's starter).</summary>
        internal void PayDuel(long amount)
        {
            if (amount <= 0)
            {
                return;
            }
            TotalScore += amount;
            CurrencyGrantedByEffects += amount;
        }
    }
}
