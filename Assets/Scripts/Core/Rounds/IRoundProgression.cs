// PURPOSE: Decides how rounds get harder. The design only says "rounds get
// progressively harder" and "each round has its own board size and threshold";
// the concrete curve below is a TUNABLE PLACEHOLDER, not confirmed design.
// EXTENSION POINT: special rounds/events, boss rounds, or joker-modified
// difficulty are new IRoundProgression implementations.

using System;

namespace ProjectBlock.Core
{
    /// <summary>Provides the setup for any round number. Must be deterministic.</summary>
    public interface IRoundProgression
    {
        RoundConfig GetRound(int roundNumber);
    }

    /// <summary>
    /// Placeholder curve: board grows from 6x6 by +1 every N rounds up to 10x10;
    /// threshold grows geometrically and is rounded up to a multiple of 5.
    /// </summary>
    public sealed class DefaultRoundProgression : IRoundProgression
    {
        public int BaseBoardSize = 6;
        public int MaxBoardSize = 10;
        public int GrowBoardEveryNRounds = 2;
        public int BaseThreshold = 60;
        public double ThresholdGrowthFactor = 1.5;

        public RoundConfig GetRound(int roundNumber)
        {
            if (roundNumber < 1)
            {
                throw new ArgumentException("Round numbers are 1-based.");
            }
            int size = Math.Min(BaseBoardSize + (roundNumber - 1) / GrowBoardEveryNRounds, MaxBoardSize);
            double rawThreshold = BaseThreshold * Math.Pow(ThresholdGrowthFactor, roundNumber - 1);
            int threshold = (int)(Math.Ceiling(rawThreshold / 5.0) * 5.0);
            return new RoundConfig(roundNumber, size, size, threshold);
        }
    }
}
