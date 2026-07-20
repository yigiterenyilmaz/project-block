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

        public int Price { get; }
        public bool Sold { get; internal set; }

        internal MarketOffer(BlockCard card, int price)
        {
            Kind = MarketOfferKind.Block;
            Card = card;
            Price = price;
        }

        internal MarketOffer(JokerDefinition joker, int price)
        {
            Kind = MarketOfferKind.Joker;
            Joker = joker;
            Price = price;
        }

        internal MarketOffer(PowerDefinition power, int price)
        {
            Kind = MarketOfferKind.Power;
            Power = power;
            Price = price;
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
