// PURPOSE: A SessionContext that also carries the current RoundEngine, for hooks
// that fire while a round is running (round start/end, activation).

namespace ProjectBlock.Core
{
    /// <summary>SessionContext plus the round that is currently being played.</summary>
    public sealed class RoundContext : SessionContext
    {
        public RoundEngine Round { get; }

        public RoundContext(GameSession session, IRandomSource rng, RoundEngine round)
            : base(session, rng)
        {
            Round = round;
        }

        /// <summary>True once the threshold was passed - "uzatma" (overtime) is running.</summary>
        public bool Overtime
        {
            get { return Round.ThresholdPassed; }
        }
    }
}
