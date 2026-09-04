// PURPOSE: One purchasable item in the market. Carries exactly one payload
// (block card / joker / power) per Kind, plus its price and sold flag.
// GameSession.TryBuyOffer consumes it.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One purchasable item in the market. A block offer carries a Card, a joker
    /// offer a Joker definition, a power offer a Power definition. Exactly one payload is
    /// set, per Kind.</summary>
    public sealed class MarketOffer
    {
        public MarketOfferKind Kind { get; }

        /// <summary>The block on sale, or null for a joker/power offer.</summary>
        public BlockCard Card { get; }

        /// <summary>The joker on sale, or null for any other offer kind.</summary>
        public JokerDefinition Joker { get; }

        /// <summary>The power on sale, or null for any other offer kind.</summary>
        public PowerDefinition Power { get; }

        /// <summary>What this item was STOCKED at - its rarity, its size, the visit's discount.
        /// Fixed for as long as the offer stands, and the number the save file keeps.</summary>
        public int BasePrice { get; }

        /// <summary>
        /// What it costs RIGHT NOW: the base price plus the shelf's current surcharge, which
        /// tracks the reroll counter (see Market.PriceSurcharge). Live rather than baked in, so
        /// rerolling ANY shelf immediately reprices every offer in the market - including the
        /// ones that were not refreshed.
        ///
        /// Every buyer, tooltip and affordability check reads this, so the price the player is
        /// shown and the price they are charged cannot come apart.
        /// </summary>
        public int Price
        {
            get { return BasePrice + (market != null ? market.PriceSurcharge : 0); }
        }

        public bool Sold { get; internal set; }

        private Market market;

        /// <summary>Binds the offer to the shelf it is standing on, so it can read the current
        /// surcharge. Called by Market.SetOffers - an offer is on exactly one market.</summary>
        internal void AttachTo(Market owner)
        {
            market = owner;
        }

        internal MarketOffer(BlockCard card, int price)
        {
            Kind = MarketOfferKind.Block;
            Card = card;
            BasePrice = price;
        }

        internal MarketOffer(JokerDefinition joker, int price)
        {
            Kind = MarketOfferKind.Joker;
            Joker = joker;
            BasePrice = price;
        }

        internal MarketOffer(PowerDefinition power, int price)
        {
            Kind = MarketOfferKind.Power;
            Power = power;
            BasePrice = price;
        }

        /// <summary>Short label for logs, independent of the offer kind.</summary>
        public override string ToString()
        {
            switch (Kind)
            {
                case MarketOfferKind.Joker:
                    return "Joker " + (Joker != null ? Joker.DisplayName : "?");
                case MarketOfferKind.Power:
                    return "Power " + (Power != null ? Power.DisplayName : "?");
                default:
                    return "Block " + Card;
            }
        }
    }
}
