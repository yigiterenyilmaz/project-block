// PURPOSE: Tunable market ("market") values - offer counts, block/joker/power pricing,
// rarity weights and price multipliers, reroll and sell economics. All BALANCE
// PLACEHOLDERS. Read live by GameSession when it stocks and prices the market.

using System;
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
        /// times its base price (before the global ScoreScale). Fractional by design - the
        /// caller rounds the multiplied base price BEFORE applying ScoreScale, so a x1.5 rare
        /// stays a round number in the scaled economy. Balance placeholders.</summary>
        public double CommonPriceMultiplier = 1.0;
        public double RarePriceMultiplier = 1.5;
        public double LegendaryPriceMultiplier = 2.0;

        /// <summary>Relative shop-appearance weights per rarity: commoner items are far likelier
        /// to be offered, legendaries seldom. Balance placeholders.</summary>
        public int CommonWeight = 100;
        public int RareWeight = 35;
        public int LegendaryWeight = 8;

        /// <summary>Price multiplier for a rarity tier.</summary>
        public double PriceMultiplier(Rarity rarity)
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

        /// <summary>Fraction of a joker's / power's buy price returned when it is sold - the
        /// SINGLE global sell rate for both (confirmed design 2026-07-25). Sell value used to be
        /// a hand-written number per joker/power, which drifted ABOVE the flat buy price and made
        /// buy-then-sell a money printer; deriving it from the buy price keeps sell < buy at every
        /// rarity by construction. Earned value (kumbara accrual, "ihale" premium) is added on top
        /// at full value - the cut applies to the base price only. Balance placeholder.</summary>
        public double ContentSellFraction = 0.6;

        /// <summary>What a joker of this rarity costs, before the global ScoreScale.</summary>
        public int JokerBuyPrice(Rarity rarity)
        {
            return (int)Math.Round(JokerPrice * PriceMultiplier(rarity));
        }

        /// <summary>What a power of this rarity costs, before the global ScoreScale.</summary>
        public int PowerBuyPrice(Rarity rarity)
        {
            return (int)Math.Round(PowerPrice * PriceMultiplier(rarity));
        }

        /// <summary>The base sell value of a joker of this rarity (accrual and auction premium
        /// are added by JokerInventory), before the global ScoreScale.</summary>
        public int JokerSellValue(Rarity rarity)
        {
            return (int)Math.Round(JokerBuyPrice(rarity) * ContentSellFraction);
        }

        /// <summary>The sell value of a power of this rarity, before the global ScoreScale.</summary>
        public int PowerSellValue(Rarity rarity)
        {
            return (int)Math.Round(PowerBuyPrice(rarity) * ContentSellFraction);
        }

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
