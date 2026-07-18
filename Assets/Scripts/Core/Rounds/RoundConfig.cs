// PURPOSE: The static definition of one round: its board size and the score
// threshold ("eşik") the player must reach to earn the right to advance.

namespace ProjectBlock.Core
{
    /// <summary>Immutable setup values for one round.</summary>
    public sealed class RoundConfig
    {
        /// <summary>1-based round index.</summary>
        public int RoundNumber { get; }

        public int BoardWidth { get; }
        public int BoardHeight { get; }

        /// <summary>Round score needed to be offered advancement to the next round.</summary>
        public int ScoreThreshold { get; }

        public RoundConfig(int roundNumber, int boardWidth, int boardHeight, int scoreThreshold)
        {
            RoundNumber = roundNumber;
            BoardWidth = boardWidth;
            BoardHeight = boardHeight;
            ScoreThreshold = scoreThreshold;
        }
    }
}
