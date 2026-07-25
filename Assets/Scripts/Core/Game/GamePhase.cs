// PURPOSE: Top-level phase of a run - playing a round, in the market, or finished
// (won or lost). GameOver is a LOSS; winning the final round is RunWon.

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

        /// <summary>The run was LOST (see CurrentRound.Loss).</summary>
        GameOver = 2,

        /// <summary>The run was WON: the final round (GameConfig.TotalRounds) was survived and
        /// the player advanced out of it. Terminal like GameOver - there is no market after it
        /// and CurrentRound.Loss is null. Anything that waits for a run to finish must accept
        /// BOTH this and GameOver.</summary>
        RunWon = 3
    }
}
