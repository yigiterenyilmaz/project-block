// PURPOSE: The difficulty curve - board size comes from a fixed table of round ranges, the
// threshold grows geometrically.
//
// CONFIRMED DESIGN: the board-size table. Rounds 0-5 are played on 5x5, 6-11 on 7x7 and
// 12-15 on 9x9. Round numbers are 1-BASED (there is no round 0 to play), so in practice the
// first band covers rounds 1-5. A run that outlives the table keeps the last band's size.
// TUNABLE PLACEHOLDER: the threshold numbers.

using System;

namespace ProjectBlock.Core
{
    /// <summary>
    /// Board size steps through BoardSizeBands; the threshold grows geometrically and is
    /// rounded up to a multiple of 5.
    /// </summary>
    public sealed class DefaultRoundProgression : IRoundProgression
    {
        /// <summary>The board-size table, in round order. Ranges are inclusive on both ends.</summary>
        public BoardSizeBand[] BoardSizeBands =
        {
            new BoardSizeBand(0, 5, 5),
            new BoardSizeBand(6, 11, 7),
            new BoardSizeBand(12, 15, 9)
        };

        public int BaseThreshold = 60;
        public double ThresholdGrowthFactor = 1.5;

        public RoundConfig GetRound(int roundNumber)
        {
            if (roundNumber < 1)
            {
                throw new ArgumentException("Round numbers are 1-based.");
            }
            int size = BoardSizeFor(roundNumber);
            double rawThreshold = BaseThreshold * Math.Pow(ThresholdGrowthFactor, roundNumber - 1);
            int threshold = (int)(Math.Ceiling(rawThreshold / 5.0) * 5.0);
            return new RoundConfig(roundNumber, size, size, threshold);
        }

        /// <summary>Board edge length for a round: the band that covers it, or - for a round the
        /// table does not reach - the size of the newest band the round has already passed. So
        /// the curve never falls off the end and a gap in the table holds the previous size
        /// instead of jumping to the biggest board.</summary>
        public int BoardSizeFor(int roundNumber)
        {
            if (BoardSizeBands == null || BoardSizeBands.Length == 0)
            {
                throw new InvalidOperationException(
                    "DefaultRoundProgression needs at least one board-size band.");
            }
            BoardSizeBand fallback = BoardSizeBands[0];
            for (int i = 0; i < BoardSizeBands.Length; i++)
            {
                BoardSizeBand band = BoardSizeBands[i];
                if (band.Covers(roundNumber))
                {
                    return band.Size;
                }
                if (roundNumber > band.LastRound)
                {
                    fallback = band;
                }
            }
            return fallback.Size;
        }
    }
}
