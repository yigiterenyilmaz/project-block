// PURPOSE: "Uzun vadeli yatırımcı" - a bet on the whole run. It is stocked ONLY in the early
// market visits, does absolutely nothing for the twelve rounds that follow, and pays out in one
// place: the LAST round. There it is an extra life (lose it once and you play it again) and the
// key that unlocks the two powers nothing else can reach.
//
// WHY IT HAS NO HOOKS. Every part of it is a central rule, not a per-turn effect:
//   - stocked early only -> Joker.LastOfferableRound, read by GameSession.AddJokerOffers
//   - never sellable     -> Joker.NeverSellable, read by JokerInventory.CanSell
//   - the extra life     -> JokerInventory.TryConsumeFinalRoundRetry, called by GameSession
//                           when the final round is lost
//   - the two powers     -> Joker.UnlocksInvestorPowers, read by GameSession when the final
//                           round starts
// So the class is state plus four answers. That is deliberate: the joker must never ask "is this
// the last round?" itself, exactly as no joker asks "is there a boss?".
//
// THE INVESTMENT IS REAL. It cannot be sold once bought, so the slot it occupies is spent for the
// rest of the run whatever happens - and if you never reach the last round, you paid for nothing.
// That is the whole point of the name.
//
// EXTENSION POINT: the two exclusive powers are content, not plumbing. Any Power that overrides
// InvestorOnly is kept out of the market entirely and handed over for the final round when this
// joker is held. Registering it in PowerRegistry is all it takes - there is nothing to wire here.
//
// All numbers are BALANCE PLACEHOLDERS.

namespace ProjectBlock.Core
{
    /// <summary>"Uzun vadeli yatırımcı" - bought early, pays out only on the final round.</summary>
    public sealed class UzunVadeliYatirimciJoker : Joker
    {
        /// <summary>Last round whose market may stock it. 5 is the end of the first board-size
        /// band, which is the game's own definition of "early".</summary>
        public int LastMarketRound = 5;

        private bool retryUsed;

        public UzunVadeliYatirimciJoker()
            : base("uzun_vadeli_yatirimci", "Uzun Vadeli Yatırımcı")
        {
            SetDescription(
                "Sold only in the first markets of a run, and does nothing until the very last "
                    + "round. There it gives you ONE second chance - lose that round and you play "
                    + "it again from the start - and unlocks two powers you cannot get any other "
                    + "way. It can never be sold, so if you do not reach the last round you paid "
                    + "for nothing.",
                "Sadece koşunun ilk marketlerinde satılır ve son raunda kadar hiçbir şey yapmaz. "
                    + "Orada sana BİR ikinci şans verir - o raundu kaybedersen baştan tekrar "
                    + "oynarsın - ve başka hiçbir yolla alamayacağın iki gücü açar. Asla "
                    + "satılamaz, yani son raunda ulaşamazsan boşa para verdin.");
        }

        /// <summary>True once the second chance has been spent.</summary>
        public bool RetryUsed
        {
            get { return retryUsed; }
        }

        public override string StatusText
        {
            get
            {
                return retryUsed
                    ? Loc.Pick("cashed in", "kullanıldı")
                    : Loc.Pick("holding", "vadeli");
            }
        }

        /// <summary>The investment is locked in: it cannot be sold, ever.</summary>
        public override bool NeverSellable
        {
            get { return true; }
        }

        /// <summary>Only the early markets stock it.</summary>
        public override int LastOfferableRound
        {
            get { return LastMarketRound; }
        }

        /// <summary>Holding it is the key to the InvestorOnly powers on the final round.</summary>
        public override bool UnlocksInvestorPowers
        {
            get { return true; }
        }

        /// <summary>The second chance, offered while it is still unspent. Central code decides
        /// WHEN to ask (only a lost final round); the joker only knows whether it still has one.
        /// </summary>
        internal override bool ConsumeFinalRoundRetry()
        {
            if (retryUsed)
            {
                return false;
            }
            retryUsed = true;
            return true;
        }
    }
}
