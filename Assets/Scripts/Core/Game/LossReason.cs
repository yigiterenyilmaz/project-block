// PURPOSE: Why a round was lost - reported on the TurnReport for UI/debug.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Why a round was lost.</summary>
    public enum LossReason
    {
        /// <summary>No held block (hand or bonus hand) fits anywhere on the board.</summary>
        NoPlayableMove = 0,

        /// <summary>Confirmed rule: before the threshold, the hand could not be refilled to
        /// full size because both draw and discard piles were empty.</summary>
        HandCannotBeRefilled = 1,

        /// <summary>"Batak": the player bet on clearing the board within N turns and the
        /// deadline passed without a clean sweep.</summary>
        BetFailed = 3,

        /// <summary>Overtime rule: after the threshold was passed, the draw pile ran dry
        /// before a clean sweep re-shuffled it.</summary>
        DrawPileEmptyAfterThreshold = 2,

        /// <summary>Retro top-out: a block reached the very top row, so there is no room to drop
        /// the next piece from above (even if lower rows still have gaps) - like Tetris.</summary>
        RetroTopOut = 4
    }
}
