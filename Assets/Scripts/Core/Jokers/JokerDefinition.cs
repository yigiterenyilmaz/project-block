// PURPOSE: A catalogue entry for one joker kind - its DefId, display name, rarity,
// and a factory that mints fresh instances for the market/debug grants.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Name, description and factory for one joker kind.</summary>
    public sealed class JokerDefinition
    {
        public string DefId { get; }
        public string DisplayName { get; }

        /// <summary>Whether this joker kind is legendary (at most one held at a time).</summary>
        public bool IsLegendary
        {
            get { return sample.IsLegendary; }
        }

        /// <summary>Graded rarity (from the rarity grader, via RarityTable). Drives market
        /// price and shop appearance odds; keyed by DefId.</summary>
        public Rarity Rarity
        {
            get { return RarityTable.For(DefId); }
        }

        /// <summary>Read LIVE off a sample instance, so it follows the Loc language.</summary>
        public string Description
        {
            get { return sample.Description; }
        }

        private readonly Joker sample;
        private readonly Func<Joker> factory;

        public JokerDefinition(Joker sample, Func<Joker> factory)
        {
            DefId = sample.DefId;
            DisplayName = sample.DisplayName;
            this.sample = sample;
            this.factory = factory;
        }

        public Joker Create()
        {
            return factory();
        }
    }
}
