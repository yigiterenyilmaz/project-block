// PURPOSE: Top-level phase of a run - playing a round, in the market, or game over.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Top-level phase of the run.</summary>
    public enum GamePhase
    {
        /// <summary>A round is being played (see CurrentRound.Status for detail).</summary>
        Round = 0,

        /// <summary>Between rounds; leave via LeaveMarket().</summary>
        Market = 1,

        /// <summary>The run is over (see CurrentRound.Loss).</summary>
        GameOver = 2
    }
}
