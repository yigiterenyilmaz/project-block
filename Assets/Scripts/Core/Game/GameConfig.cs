// PURPOSE: Everything needed to start a run, with the pluggable systems exposed as
// interfaces so variants can be swapped without touching game code.
// EXTENSION POINT: starting jokers/powers, unlockable deck archetypes ("small blocks
// weighted" etc.), and market configuration belong here later.

namespace ProjectBlock.Core
{
    /// <summary>Run setup. Plain mutable object: build one, tweak, hand to GameSession.</summary>
    public sealed class GameConfig
    {
        /// <summary>Seed for the whole run; null = time-based random seed.</summary>
        public int? RngSeed = null;

        /// <summary>Starting-deck recipe (see DeckLibrary for the built-in archetypes).
        /// Its Size must be at least Rules.HandSize or the first refill loses instantly.
        /// Market offers also draw from this deck's shape source.</summary>
        public DeckDefinition Deck = DeckLibrary.Classic;

        public RoundRules Rules = new RoundRules();
        public ScoringConfig Scoring = new ScoringConfig();
        public MarketConfig Market = new MarketConfig();
        public IRoundProgression Progression = new DefaultRoundProgression();
    }
}
