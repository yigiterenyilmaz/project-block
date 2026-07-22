// PURPOSE: Tunable market ("market") values - offer counts, block/joker/power pricing,
// rarity weights and price multipliers, reroll and sell economics. All BALANCE
// PLACEHOLDERS. Read live by GameSession when it stocks and prices the market.

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

        /// <summary>Market reroll cost = RerollBaseCost + RerollCostStep * rerolls-done-this-visit
        /// (before the global ScoreScale). One reroll refreshes EVERY offer at once; the price
        /// escalates within a visit and resets on the next. Balance placeholders.</summary>
        public int RerollBaseCost = 5;
        public int RerollCostStep = 5;

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
            BlockElement.Fox,
            BlockElement.Negative
        };
    }
}
