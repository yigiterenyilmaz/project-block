// PURPOSE: The ordered mid-turn callbacks RoundEngine fires during a placement
// (after line explosion, clean sweep, score modification, end of turn).
// JokerInventory is the real implementation.

namespace ProjectBlock.Core
{
    /// <summary>In-turn callbacks the round engine raises. See Joker for the semantics.</summary>
    public interface ITurnHooks
    {
        void AfterLineExplosion(TurnContext turn);

        void AfterCleanSweep(TurnContext turn);

        void ModifyScore(TurnContext turn);

        void AfterTurnScored(TurnContext turn);

        /// <summary>The board filled up and nothing fits. Jokers get one chance to open a gap
        /// ("Deprem"). Return true if something acted - the engine then re-checks for a move
        /// instead of ending the round.</summary>
        bool TryRescueFromDeadEnd(RoundContext ctx);
    }
}
