// PURPOSE: Everything that happened during one resolved turn, in order, so the UI can
// animate it and future jokers can react to it. RoundEngine fills this; nothing else
// writes to it. If you add a mechanic that does something new in a turn, add a field
// here instead of making the UI re-derive state.

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

    /// <summary>Why a round was lost.</summary>
    public enum LossReason
    {
        /// <summary>No held block (hand or bonus hand) fits anywhere on the board.</summary>
        NoPlayableMove = 0,

        /// <summary>Confirmed rule: before the threshold, the hand could not be refilled to
        /// full size because both draw and discard piles were empty.</summary>
        HandCannotBeRefilled = 1,

        /// <summary>Overtime rule: after the threshold was passed, the draw pile ran dry
        /// before a clean sweep re-shuffled it.</summary>
        DrawPileEmptyAfterThreshold = 2
    }

    /// <summary>Immutable-after-resolution record of one turn.</summary>
    public sealed class TurnReport
    {
        public int TurnNumber { get; internal set; }
        public BlockCard Card { get; internal set; }
        public bool PlayedFromBonusHand { get; internal set; }
        public GridPos Origin { get; internal set; }
        public IReadOnlyList<GridPos> PlacedCells { get; internal set; }

        public IReadOnlyList<int> ExplodedRows { get; internal set; }
        public IReadOnlyList<int> ExplodedColumns { get; internal set; }
        public int CubesExploded { get; internal set; }

        /// <summary>True if this turn emptied the board ("temizlik").</summary>
        public bool CleanSweep { get; internal set; }

        public int ScoreGained { get; internal set; }
        public int RoundScoreAfter { get; internal set; }

        /// <summary>Bonus-hand plays only: draw pile card flipped face-up into the discard.</summary>
        public BlockCard BurnedCard { get; internal set; }

        /// <summary>True the one time the round score first reached the threshold.</summary>
        public bool ThresholdJustPassed { get; internal set; }

        /// <summary>Overtime clean sweeps only: cards removed face-down from the draw pile
        /// until round end. The player must NOT be shown which cards these are.</summary>
        public IReadOnlyList<BlockCard> CardsRemovedForRound { get; internal set; }

        public RoundStatus StatusAfter { get; internal set; }

        internal TurnReport()
        {
            PlacedCells = Array.Empty<GridPos>();
            ExplodedRows = Array.Empty<int>();
            ExplodedColumns = Array.Empty<int>();
            CardsRemovedForRound = Array.Empty<BlockCard>();
        }
    }
}
