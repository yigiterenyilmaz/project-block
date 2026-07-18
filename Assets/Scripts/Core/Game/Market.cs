// PURPOSE: The between-rounds market ("market"). Confirmed design: jokers, powers and
// block cards can be bought, held jokers/powers can be sold, and the currency is the
// run-wide TotalScore (same points the player scores with).
// BASE GAME SCOPE: only block-card offers. Buying permanently adds the card to the
// owned deck (it joins the shuffle from the next round on). GameSession owns the
// money and the deck, so purchases go through GameSession.TryBuyOffer.
// EXTENSION POINTS:
//  - Joker/power offers: give MarketOffer a payload kind instead of assuming a card,
//    and branch in GameSession.TryBuyOffer.
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

        /// <summary>Elements the market can roll. ONLY add elements whose behavior is
        /// implemented (see BlockElement docs).</summary>
        public List<BlockElement> ElementPool = new List<BlockElement>
        {
            BlockElement.Fire,
            BlockElement.Obsidian,
            BlockElement.Gold,
            BlockElement.Dynamite,
            BlockElement.PiggyBank
        };
    }

    /// <summary>One purchasable item in the market.</summary>
    public sealed class MarketOffer
    {
        public BlockCard Card { get; }
        public int Price { get; }
        public bool Sold { get; internal set; }

        internal MarketOffer(BlockCard card, int price)
        {
            Card = card;
            Price = price;
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
