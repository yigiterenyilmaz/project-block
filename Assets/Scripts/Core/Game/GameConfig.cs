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

        /// <summary>Number of random block cards the starting deck is built from.
        /// Must be at least Rules.HandSize or the first refill loses instantly.</summary>
        public int StartingDeckSize = 24;

        public RoundRules Rules = new RoundRules();
        public ScoringConfig Scoring = new ScoringConfig();
        public MarketConfig Market = new MarketConfig();
        public IShapeGenerator ShapeGenerator = new RandomPolyominoGenerator();
        public IRoundProgression Progression = new DefaultRoundProgression();
    }
}
