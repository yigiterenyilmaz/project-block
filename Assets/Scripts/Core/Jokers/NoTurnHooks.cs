// PURPOSE: The do-nothing ITurnHooks used when a round runs without a session
// (tests, simulations), so the engine behaves exactly as the base game.

namespace ProjectBlock.Core
{
    /// <summary>Null-object hooks: used whenever a round runs without a session
    /// (unit tests, simulations). Keeps RoundEngine free of null checks.</summary>
    public sealed class NoTurnHooks : ITurnHooks
    {
        public static readonly NoTurnHooks Instance = new NoTurnHooks();

        private NoTurnHooks()
        {
        }

        public void AfterLineExplosion(TurnContext turn)
        {
        }

        public void AfterCleanSweep(TurnContext turn)
        {
        }

        public void ModifyScore(TurnContext turn)
        {
        }

        public void AfterTurnScored(TurnContext turn)
        {
        }

        public bool TryRescueFromDeadEnd(RoundContext ctx)
        {
            return false;
        }
    }
}
