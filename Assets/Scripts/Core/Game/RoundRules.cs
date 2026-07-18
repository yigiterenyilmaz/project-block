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

        /// <summary>Overtime rule: cards removed face-down from the draw pile per clean sweep
        /// after the threshold has been passed.</summary>
        public int OvertimeCardsRemovedPerCleanSweep = 3;
    }
}
