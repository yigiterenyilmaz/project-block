// PURPOSE: The difficulty curve - board size comes from a fixed table of round ranges, the
// threshold grows geometrically, and every third round is flagged as a boss round.
//
// CONFIRMED DESIGN: the board-size table. A run is 15 rounds, numbered 1-15, and the table
// covers exactly that: rounds 1-5 on 5x5, 6-11 on 7x7, 12-15 on 9x9. A round past the table
// keeps the last band's size, so nothing breaks if the run length ever grows.
//
// Each band also names the erosion that punishes a stalling round (ShuffleErosion): the small
// arena loses its rim, the middle one is hollowed out from the centre, and the last one suffers
// both at once.
// TUNABLE PLACEHOLDER: the threshold numbers and the boss interval.
//
// Run LENGTH is not decided here: that is GameConfig.TotalRounds. This table is written to
// cover exactly that many rounds, so the two are meant to be changed together.

using System;

namespace ProjectBlock.Core
{
    /// <summary>
    /// Board size steps through BoardSizeBands; the threshold grows geometrically and is
    /// rounded up to a multiple of 5.
    /// </summary>
    public sealed class DefaultRoundProgression : IRoundProgression
    {
        /// <summary>The board-size table, in round order. Ranges are inclusive on both ends and
        /// together they cover the whole 15-round run.</summary>
        public BoardSizeBand[] BoardSizeBands =
        {
            new BoardSizeBand(1, 5, 5, ShuffleErosion.FromOutside),
            new BoardSizeBand(6, 11, 7, ShuffleErosion.FromCenter),
            new BoardSizeBand(12, 15, 9, ShuffleErosion.Both)
        };

        public int BaseThreshold = 60;
        public double ThresholdGrowthFactor = 1.5;

        /// <summary>Every n-th round is a boss round ("patron raundu"): 3 means 3, 6, 9, 12, 15.
        /// 0 disables them. This is the ONLY place that decides which rounds are boss rounds -
        /// everything else reads RoundConfig.IsBossRound, and GameSession draws the boss itself.</summary>
        public int BossRoundInterval = 3;

        /// <summary>
        /// The setup for one STAGE. A boss stage keeps the arena of the round it follows - same
        /// board, same erosion - and raises the bar instead: a boss is a wall, not a new place.
        /// </summary>
        public RoundConfig GetRound(int roundNumber, bool bossStage)
        {
            if (roundNumber < 1)
            {
                throw new ArgumentException("Round numbers are 1-based.");
            }
            BoardSizeBand band = BandFor(roundNumber);
            double rawThreshold = BaseThreshold * Math.Pow(ThresholdGrowthFactor, roundNumber - 1);
            if (bossStage)
            {
                rawThreshold *= BossThresholdFactor;
            }
            int threshold = (int)(Math.Ceiling(rawThreshold / 5.0) * 5.0);
            return new RoundConfig(roundNumber, band.Size, band.Size, threshold, null,
                band.Erosion, bossStage);
        }

        /// <summary>True when a BOSS STAGE follows that numbered round - after 3, 6, 9, 12 and
        /// 15. The boss is its own stage between two numbered rounds, not one of them.</summary>
        public bool HasBossStageAfter(int roundNumber)
        {
            return BossRoundInterval > 0 && roundNumber % BossRoundInterval == 0;
        }

        /// <summary>What a boss stage's threshold is worth relative to the round it follows. A
        /// boss stage is a WALL: same arena, a much higher bar. Balance placeholder.</summary>
        public double BossThresholdFactor = 1.5;

        /// <summary>Board edge length for a round.</summary>
        public int BoardSizeFor(int roundNumber)
        {
            return BandFor(roundNumber).Size;
        }

        /// <summary>How a round's arena erodes when the deck keeps recycling.</summary>
        public ShuffleErosion ErosionFor(int roundNumber)
        {
            return BandFor(roundNumber).Erosion;
        }

        /// <summary>The band that covers a round, or - for a round the table does not reach -
        /// the newest band the round has already passed. So the curve never falls off the end
        /// and a gap in the table holds the previous band instead of jumping to the last one.</summary>
        public BoardSizeBand BandFor(int roundNumber)
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
                    return band;
                }
                if (roundNumber > band.LastRound)
                {
                    fallback = band;
                }
            }
            return fallback;
        }
    }
}
