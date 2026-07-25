// PURPOSE: One row of the board-size table - "rounds A through B are played on an NxN board,
// and this is how that board erodes when the deck keeps recycling". The board no longer creeps
// up a cell at a time; it steps between a few fixed sizes at fixed round numbers, so every run
// sees the same three arenas at the same points.
// EXTENSION POINT: a boss round or a variant curve is a different band table handed to an
// IRoundProgression - nothing that reads RoundConfig has to change.

namespace ProjectBlock.Core
{
    /// <summary>A range of rounds that share one board size and one erosion style. BOTH
    /// bounds are inclusive.</summary>
    public sealed class BoardSizeBand
    {
        public BoardSizeBand(int firstRound, int lastRound, int size)
            : this(firstRound, lastRound, size, ShuffleErosion.None)
        {
        }

        public BoardSizeBand(int firstRound, int lastRound, int size, ShuffleErosion erosion)
        {
            FirstRound = firstRound;
            LastRound = lastRound;
            Size = size;
            Erosion = erosion;
        }

        /// <summary>First round played on this size (inclusive).</summary>
        public int FirstRound { get; }

        /// <summary>Last round played on this size (inclusive).</summary>
        public int LastRound { get; }

        /// <summary>Edge length of the board. The base board is always square.</summary>
        public int Size { get; }

        /// <summary>How this band's arena is eaten away once the draw pile keeps running dry.
        /// Board size and erosion style go together, so one table decides both.</summary>
        public ShuffleErosion Erosion { get; }

        public bool Covers(int roundNumber)
        {
            return roundNumber >= FirstRound && roundNumber <= LastRound;
        }
    }
}
