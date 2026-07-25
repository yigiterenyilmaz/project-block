// PURPOSE: One row of the board-size table - "rounds A through B are played on an NxN board".
// The board no longer creeps up a cell at a time; it steps between a few fixed sizes at fixed
// round numbers, so every run sees the same three arenas at the same points.
// EXTENSION POINT: a boss round or a variant curve is a different band table handed to an
// IRoundProgression - nothing that reads RoundConfig has to change.

namespace ProjectBlock.Core
{
    /// <summary>A range of rounds that share one board size. BOTH bounds are inclusive.</summary>
    public sealed class BoardSizeBand
    {
        public BoardSizeBand(int firstRound, int lastRound, int size)
        {
            FirstRound = firstRound;
            LastRound = lastRound;
            Size = size;
        }

        /// <summary>First round played on this size (inclusive).</summary>
        public int FirstRound { get; }

        /// <summary>Last round played on this size (inclusive).</summary>
        public int LastRound { get; }

        /// <summary>Edge length of the board. The base board is always square.</summary>
        public int Size { get; }

        public bool Covers(int roundNumber)
        {
            return roundNumber >= FirstRound && roundNumber <= LastRound;
        }
    }
}
