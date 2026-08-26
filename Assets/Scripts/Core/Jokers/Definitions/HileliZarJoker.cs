// PURPOSE: "Hileli zar" - the joker that lets the player deal their own opening hand. Like
// "Kredi kartı" this class is the SWITCH, not the machinery: GameSession owns the pending
// opening hand (TryPickOpeningHand / TakePendingOpeningHand) and the market UI drives the
// picking, because this is the one joker whose whole effect happens between rounds.
//
// CONFIRMED RULES:
//  - it works in the MARKET only, and once per market visit: the pick is armed again by
//    OnMarketEntered, so every shop gives exactly one deal;
//  - the chosen cards are guaranteed onto the TOP of the next round's fresh draw pile, which
//    is what makes them the opening hand (RoundEngine consumes the preset once);
//  - it has no in-round hooks at all - nothing to gate in overtime, nothing for a boss to
//    silence beyond the central JokerInventory check.
//
// It used to be a POWER (defId unchanged, so its rarity grade and old picks still match).
// A power's single charge only ever amounted to one use per market anyway - it refused to
// run in-round and refilled at every round start - so the once-per-market rule below is the
// same deal in the system where it belongs.

namespace ProjectBlock.Core
{
    /// <summary>"Hileli zar" - in the market, choose the cards that make up the next round's
    /// opening hand. Once per market visit.</summary>
    public sealed class HileliZarJoker : Joker
    {
        public HileliZarJoker()
            : base("hileli_zar", "Hileli Zar")
        {
            SetDescription(
                "In the market, choose the cards that make up your next round's opening hand. "
                    + "Once per market.",
                "Market fazında sonraki rauntun başlangıç elini seçebilmeni sağlar. "
                    + "Her markette bir kez.");
        }

        /// <summary>True once this market's pick has been made. Cleared on entering a market,
        /// so a fresh shop always offers the deal.</summary>
        private bool usedThisMarket;

        /// <summary>True while the player may still deal themselves a hand in this market.
        /// The market UI asks this to decide whether a click opens the picker or sells the
        /// joker; GameSession.TryPickOpeningHand is what actually spends it.</summary>
        public bool CanPickOpeningHand
        {
            get { return !usedThisMarket; }
        }

        public override string StatusText
        {
            get
            {
                return usedThisMarket
                    ? Loc.Pick("used", "kullanıldı")
                    : Loc.Pick("ready", "hazır");
            }
        }

        /// <summary>Marks this market's pick as spent. Called by GameSession once the cards are
        /// actually pending - never by the UI, which does not get to decide when it is used.</summary>
        internal void NotePicked()
        {
            usedThisMarket = true;
        }

        public override void OnMarketEntered(SessionContext ctx)
        {
            usedThisMarket = false;
        }
    }
}
