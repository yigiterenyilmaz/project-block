// PURPOSE: The narrow surface RoundEngine calls into during a turn. JokerInventory is the
// only implementation; the engine never sees a Joker, only this interface.
//
// WHY NOT A C# EVENT: the in-turn hooks run in the MIDDLE of ResolvePlacement and may
// change the turn (destroy cubes, add score, trigger a sweep). A multicast delegate gives
// no ordering guarantee and cannot be reasoned about deterministically, so the engine
// takes an explicit collaborator instead. TurnResolved/StatusChanged stay plain events
// because they are after-the-fact notifications for the UI.
//
// EXTENSION POINT: add a method here when a new turn moment appears, then implement it in
// JokerInventory (dispatch to jokers in inventory order) - never call jokers from the
// engine directly.

namespace ProjectBlock.Core
{
    /// <summary>In-turn callbacks the round engine raises. See Joker for the semantics.</summary>
    public interface ITurnHooks
    {
        void AfterLineExplosion(TurnContext turn);

        void AfterCleanSweep(TurnContext turn);

        void ModifyScore(TurnContext turn);

        void AfterTurnScored(TurnContext turn);
    }

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
    }
}
