// PURPOSE: The between-rounds market ("market"). Confirmed design: jokers, powers and
// block cards can be bought, held jokers/powers can be sold, and the currency is the
// run-wide TotalScore (same points the player scores with).
// SCOPE: block-card offers and joker offers. Buying a block permanently adds the card to
// the owned deck (it joins the shuffle from the next round on); buying a joker adds it to
// the inventory. GameSession owns the money, deck and inventory, so purchases go through
// GameSession.TryBuyOffer.
// EXTENSION POINTS:
//  - Power offers: add a MarketOfferKind member and branch in GameSession.TryBuyOffer.
//  - Selling, rerolls, per-round pricing events ("ihale", "Damlaya damlaya"): market
//    state lives here, rules in GameSession/MarketConfig.
// Prices in MarketConfig are BALANCE PLACEHOLDERS, not confirmed design.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Tunable market values.</summary>
    public sealed class MarketConfig
    {
        /// <summary>Block-card offers shown per market visit.</summary>
        public int BlockOfferCount = 3;

        /// <summary>Block price = base + per-cube * cube count (+ element surcharge).</summary>
        public int BlockBasePrice = 10;
        public int BlockPricePerCube = 6;
        public int ElementPriceSurcharge = 12;

        /// <summary>Chance a market block rolls an element ("bloklar markette çeşitli
        /// türlerle çıkabilir").</summary>
        public double ElementChance = 0.45;

        /// <summary>Joker offers shown per market visit (drawn from JokerRegistry).</summary>
        public int JokerOfferCount = 2;

        /// <summary>Flat price of a joker offer. Balance placeholder.</summary>
        public int JokerPrice = 40;

        /// <summary>Elements the market can roll. ONLY add elements whose behavior is
        /// implemented (see BlockElement docs).</summary>
        public List<BlockElement> ElementPool = new List<BlockElement>
        {
            BlockElement.Fire,
            BlockElement.Water,
            BlockElement.Obsidian,
            BlockElement.Gold,
            BlockElement.Transparent,
            BlockElement.Ghost,
            BlockElement.Dynamite,
            BlockElement.Mechanical,
            BlockElement.Fox
        };
    }

    /// <summary>What a market offer sells.</summary>
    public enum MarketOfferKind
    {
        Block = 0,
        Joker = 1
    }

    /// <summary>One purchasable item in the market. A block offer carries a Card; a joker
    /// offer carries a Joker definition. Exactly one payload is set, per Kind.</summary>
    public sealed class MarketOffer
    {
        public MarketOfferKind Kind { get; }

        /// <summary>The block on sale, or null for a joker offer.</summary>
        public BlockCard Card { get; }

        /// <summary>The joker on sale, or null for a block offer.</summary>
        public JokerDefinition Joker { get; }

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

        /// <summary>Short label for logs, independent of the offer kind.</summary>
        public override string ToString()
        {
            return Kind == MarketOfferKind.Joker
                ? "Joker " + (Joker != null ? Joker.DisplayName : "?")
                : "Block " + Card;
        }
    }

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
