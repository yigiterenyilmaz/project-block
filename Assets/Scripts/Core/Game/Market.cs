// PURPOSE: The between-rounds market stock ("market"). Holds the current list of
// offers; GameSession restocks it on every market entry and reroll. The buy/sell and
// pricing rules live in GameSession/MarketConfig, not here.
// EXTENSION POINTS:
//  - Per-round pricing events ("ihale", "Damlaya damlaya"): market state lives here,
//    rules in GameSession/MarketConfig.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Current market stock. Restocked by GameSession on every market entry.</summary>
    public sealed class Market
    {
        private readonly List<MarketOffer> offers = new List<MarketOffer>();

        public IReadOnlyList<MarketOffer> Offers
        {
            get { return offers; }
        }

        internal void SetOffers(IEnumerable<MarketOffer> newOffers)
        {
            offers.Clear();
            offers.AddRange(newOffers);
        }
    }
}
