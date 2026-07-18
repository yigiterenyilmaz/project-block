// PURPOSE: The static definition of one round: its board size and the score
// threshold ("eşik") the player must reach to earn the right to advance.
// EXTENSION POINT: ExtraPlayableCells is how a joker or power hands the round a board that
// is bigger than a plain rectangle ("Kentsel Dönüşüm", "Tılsım"). Jokers rewrite this
// through Joker.FilterRoundConfig, which runs before the board is built.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Immutable setup values for one round.</summary>
    public sealed class RoundConfig
    {
        private static readonly GridPos[] NoExtraCells = new GridPos[0];

        /// <summary>1-based round index.</summary>
        public int RoundNumber { get; }

        public int BoardWidth { get; }
        public int BoardHeight { get; }

        /// <summary>Round score needed to be offered advancement to the next round.</summary>
        public int ScoreThreshold { get; }

        /// <summary>Cells bolted onto the base rectangle, making the board irregular.
        /// Empty for a normal round. Coordinates must be non-negative: the board grows right
        /// and up, never left or down (see GameBoard).</summary>
        public IReadOnlyList<GridPos> ExtraPlayableCells { get; }

        public RoundConfig(int roundNumber, int boardWidth, int boardHeight, int scoreThreshold)
            : this(roundNumber, boardWidth, boardHeight, scoreThreshold, null)
        {
        }

        public RoundConfig(int roundNumber, int boardWidth, int boardHeight, int scoreThreshold,
            IReadOnlyList<GridPos> extraPlayableCells)
        {
            RoundNumber = roundNumber;
            BoardWidth = boardWidth;
            BoardHeight = boardHeight;
            ScoreThreshold = scoreThreshold;
            ExtraPlayableCells = extraPlayableCells ?? NoExtraCells;
        }
    }
}
