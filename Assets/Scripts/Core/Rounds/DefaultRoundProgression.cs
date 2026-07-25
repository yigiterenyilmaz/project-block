// PURPOSE: The placeholder difficulty curve - board grows to a cap, threshold grows
// geometrically, and every third round is flagged as a boss round. TUNABLE PLACEHOLDER,
// not confirmed design. Run LENGTH is not decided here: that is GameConfig.TotalRounds.

using System;

namespace ProjectBlock.Core
{
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

        /// <summary>Every n-th round is a boss round ("patron raundu"): 3 means 3, 6, 9, 12, 15.
        /// 0 disables them. This is the ONLY place that decides which rounds are boss rounds -
        /// everything else reads RoundConfig.IsBossRound. The boss mechanics themselves are not
        /// written yet; the flag exists so they have one thing to hang off.</summary>
        public int BossRoundInterval = 3;

        public RoundConfig GetRound(int roundNumber)
        {
            if (roundNumber < 1)
            {
                throw new ArgumentException("Round numbers are 1-based.");
            }
            int size = Math.Min(BaseBoardSize + (roundNumber - 1) / GrowBoardEveryNRounds, MaxBoardSize);
            double rawThreshold = BaseThreshold * Math.Pow(ThresholdGrowthFactor, roundNumber - 1);
            int threshold = (int)(Math.Ceiling(rawThreshold / 5.0) * 5.0);
            bool boss = BossRoundInterval > 0 && roundNumber % BossRoundInterval == 0;
            return new RoundConfig(roundNumber, size, size, threshold, null, boss);
        }
    }
}
