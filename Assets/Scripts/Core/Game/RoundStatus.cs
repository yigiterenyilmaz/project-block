// PURPOSE: The state of a round in progress (in progress, awaiting the advance
// decision, advanced to market, or lost). Drives GameSession's phase wiring.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Lifecycle state of a round.</summary>
    public enum RoundStatus
    {
        /// <summary>Player can place blocks.</summary>
        InProgress = 0,

        /// <summary>Threshold passed or overtime clean sweep: player must choose
        /// advance-to-market or continue via RoundEngine.DecideAdvance.</summary>
        AwaitingAdvanceDecision = 1,

        /// <summary>Round finished successfully; session moves to the market.</summary>
        Advanced = 2,

        /// <summary>Round lost; the run is over (see RoundEngine.Loss).</summary>
        Lost = 3
    }
}
