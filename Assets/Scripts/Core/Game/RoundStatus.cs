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
        Lost = 3,

        /// <summary>The board filled up and nothing in hand fits, but the player holds a
        /// rescue power ("Kentsel Dönüşüm") that could still open a gap. The round pauses
        /// here instead of ending: use the power to carry on, or decline and take the loss.
        /// Loss is already set, so declining just confirms it.</summary>
        AwaitingRescue = 4
    }
}
