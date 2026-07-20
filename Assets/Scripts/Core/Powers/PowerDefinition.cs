// PURPOSE: A catalogue entry for one power kind - its DefId, display name, rarity,
// and a factory that mints fresh (charged) instances.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Name, description and factory for one power kind.</summary>
    public sealed class PowerDefinition
    {
        public string DefId { get; }
        public string DisplayName { get; }

        /// <summary>Read LIVE off a sample instance, so it follows the Loc language.</summary>
        public string Description
        {
            get { return sample.Description; }
        }

        /// <summary>Graded rarity (from the rarity grader, via RarityTable). Drives market
        /// price and shop appearance odds; keyed by DefId.</summary>
        public Rarity Rarity
        {
            get { return RarityTable.For(DefId); }
        }

        private readonly Power sample;
        private readonly Func<Power> factory;

        public PowerDefinition(Power sample, Func<Power> factory)
        {
            DefId = sample.DefId;
            DisplayName = sample.DisplayName;
            this.sample = sample;
            this.factory = factory;
        }

        public Power Create()
        {
            return factory();
        }
    }
}
