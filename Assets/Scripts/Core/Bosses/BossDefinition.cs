// PURPOSE: A catalogue entry for one boss kind - its DefId, display name, and a factory
// that mints fresh instances. Mirrors JokerDefinition; bosses have no rarity and no price,
// because they are never bought.

using System;

namespace ProjectBlock.Core
{
    /// <summary>Name, description and factory for one boss round kind.</summary>
    public sealed class BossDefinition
    {
        public string DefId { get; }
        public string DisplayName { get; }

        /// <summary>Read LIVE off a sample instance, so it follows the Loc language.</summary>
        public string Description
        {
            get { return sample.Description; }
        }

        private readonly BossRound sample;
        private readonly Func<BossRound> factory;

        public BossDefinition(BossRound sample, Func<BossRound> factory)
        {
            DefId = sample.DefId;
            DisplayName = sample.DisplayName;
            this.sample = sample;
            this.factory = factory;
        }

        /// <summary>A fresh instance. One is minted per boss round, never reused.</summary>
        /// <summary>True for a kind that may only be a run's FIRST boss. Read off the sample,
        /// like Description.</summary>
        public bool OnlyOnFirstBossRound
        {
            get { return sample.OnlyOnFirstBossRound; }
        }

        public BossRound Create()
        {
            return factory();
        }
    }
}
