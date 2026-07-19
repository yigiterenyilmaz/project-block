// PURPOSE: The between-rounds market ("market"). Confirmed design: jokers, powers and
// block cards can be bought, held jokers/powers can be sold, and the currency is the
// run-wide TotalScore (same points the player scores with).
// SCOPE: block-card, joker and power offers. Buying a block permanently adds the card to
// the owned deck (it joins the shuffle from the next round on); buying a joker or a power
// adds it to its inventory. GameSession owns the money, deck and inventories, so purchases
// go through GameSession.TryBuyOffer.
// EXTENSION POINTS:
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

        /// <summary>Elemental market blocks never come smaller than this many cubes. A 1x1
        /// fire / dynamite / water / gold block contradicts most element behaviours (fire
        /// chains, "whole block explodes", per-cube bonuses), so an elemental offer re-rolls
        /// its shape until it is at least this big. Balance placeholder.</summary>
        public int MinElementalBlockSize = 2;

        /// <summary>Joker offers shown per market visit (drawn from JokerRegistry).</summary>
        public int JokerOfferCount = 2;

        /// <summary>Flat price of a joker offer. Balance placeholder.</summary>
        public int JokerPrice = 40;

        /// <summary>Power offers shown per market visit (drawn from PowerRegistry).</summary>
        public int PowerOfferCount = 2;

        /// <summary>Flat price of a power offer. Balance placeholder.</summary>
        public int PowerPrice = 50;

        /// <summary>Rarity price multipliers: a rare/legendary joker or power costs this many
        /// times its base price (before the global ScoreScale). Balance placeholders.</summary>
        public int CommonPriceMultiplier = 1;
        public int RarePriceMultiplier = 2;
        public int LegendaryPriceMultiplier = 3;

        /// <summary>Relative shop-appearance weights per rarity: commoner items are far likelier
        /// to be offered, legendaries seldom. Balance placeholders.</summary>
        public int CommonWeight = 100;
        public int RareWeight = 35;
        public int LegendaryWeight = 8;

        /// <summary>Price multiplier for a rarity tier.</summary>
        public int PriceMultiplier(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return RarePriceMultiplier;
                case Rarity.Legendary: return LegendaryPriceMultiplier;
                default: return CommonPriceMultiplier;
            }
        }

        /// <summary>Shop-appearance weight for a rarity tier.</summary>
        public int Weight(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return RareWeight;
                case Rarity.Legendary: return LegendaryWeight;
                default: return CommonWeight;
            }
        }

        /// <summary>Fraction of a card's buy price returned when it is sold. Balance
        /// placeholder; sell is always below buy.</summary>
        public double CardSellFraction = 0.5;

        /// <summary>The market price of a block card: base + per-cube + per-element.</summary>
        public int BuyPrice(BlockCard card)
        {
            return BlockBasePrice
                + BlockPricePerCube * card.Shape.Size
                + ElementPriceSurcharge * card.Elements.Count;
        }

        /// <summary>What selling a card pays. Plain blocks are worth nothing; elemental
        /// blocks return a fraction of their buy price (always less than buying one).</summary>
        public int SellValue(BlockCard card)
        {
            if (card.Elements.Count == 0)
            {
                return 0;
            }
            return (int)(BuyPrice(card) * CardSellFraction);
        }

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
        Joker = 1,
        Power = 2
    }

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
