// PURPOSE: The between-rounds market stock ("market"). Holds the current list of
// offers; GameSession restocks it on every market entry and reroll. The buy/sell and
// pricing rules live in GameSession/MarketConfig, not here.
// EXTENSION POINTS:
//  - Per-round pricing events ("ihale", "Kapalı Ekonomi"): market state lives here,
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

        /// <summary>
        /// What every offer on this shelf costs OVER its stocked price, in the scaled economy.
        /// It tracks the reroll counter (GameSession.RefreshMarketPrices), so churning the market
        /// makes the market itself more expensive - the same escalation the reroll button pays,
        /// charged on the goods as well.
        ///
        /// It lives on the MARKET rather than on each offer because it is a property of the
        /// visit, not of the item: a reroll of the block shelf reprices the jokers too, which is
        /// the same reasoning that gave the three shelves one shared reroll counter.
        /// </summary>
        public int PriceSurcharge { get; internal set; }

        internal void SetOffers(IEnumerable<MarketOffer> newOffers)
        {
            offers.Clear();
            offers.AddRange(newOffers);
            // Every offer is bound to the shelf it stands on, here rather than at each of the
            // three construction sites, so a new kind of offer cannot forget to be priced.
            for (int i = 0; i < offers.Count; i++)
            {
                offers[i].AttachTo(this);
            }
        }
    }
}
