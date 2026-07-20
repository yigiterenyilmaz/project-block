// PURPOSE: How a round ended, as seen by jokers' OnRoundEnded (advanced vs lost).

namespace ProjectBlock.Core
{
    /// <summary>How a round ended, for OnRoundEnded.</summary>
    public enum RoundOutcome
    {
        /// <summary>Player took the advance offer; the run continues in the market.</summary>
        Advanced = 0,

        /// <summary>Round lost; the run is over.</summary>
        Lost = 1
    }
}
