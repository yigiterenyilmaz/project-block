// PURPOSE: The mutable "current rules of play" shared by GameSession and RoundEngine.
// EXTENSION POINT: jokers and powers that bend the rules (hand size changes,
// redraw rights, altered overtime costs...) should mutate THIS object at runtime -
// RoundEngine always reads it live instead of caching values.

namespace ProjectBlock.Core
{
    /// <summary>Live rule values. Defaults are the confirmed base-game rules.</summary>
    public sealed class RoundRules
    {
        /// <summary>Cards the hand is refilled to after every normal placement.</summary>
        public int HandSize = 3;

        /// <summary>Confirmed rule (2026-07-18 feedback): declining an advance offer
        /// ("continue") removes this many random cards from the draw pile for the rest of
        /// the round, on top of the mandatory hand redraw.</summary>
        public int CardsRemovedPerContinue = 2;
    }
}
