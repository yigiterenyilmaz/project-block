// PURPOSE: Base-game scoring, driven entirely by a (mutable) ScoringConfig, so
// jokers can buff values live by mutating that config.

namespace ProjectBlock.Core
{
    /// <summary>Base-game scoring driven entirely by ScoringConfig.</summary>
    public sealed class DefaultScoreCalculator : IScoreCalculator
    {
        private readonly ScoringConfig config;

        public DefaultScoreCalculator(ScoringConfig config)
        {
            this.config = config;
        }

        public int ScorePlacement(int cubesPlaced)
        {
            return cubesPlaced * config.PointsPerCubePlaced;
        }

        public int ScoreLineExplosion(int lineCount, int cubesExploded)
        {
            int score = lineCount * config.PointsPerLine
                + cubesExploded * config.PointsPerCubeExploded;
            if (lineCount > 1)
            {
                score += (lineCount - 1) * config.MultiLineBonusPerExtraLine;
            }
            return score;
        }

        public int ScoreObsidianInLines(int obsidianCubesInLines)
        {
            return obsidianCubesInLines * config.PointsPerObsidianInLine;
        }

        public int ScoreCleanSweep()
        {
            return config.CleanSweepBonus;
        }

        /// <summary>
        /// The combo bonus for the n-th consecutive line-clearing turn.
        ///
        /// A SHORT LADDER THAT CLIMBS HARD (2026-09-16, designer's call). It used to be flat -
        /// (n-1) * step, forever - so every turn of a streak was worth the same small amount more
        /// than the last and a streak's value was all in its LENGTH. Now it caps at
        /// ScoringConfig.MaxComboTier and the rungs below the cap ACCELERATE, so the reward is in
        /// getting to three rather than in grinding to eleven.
        ///
        /// Triangular over the capped tier: rung n is worth n-1 steps on top of everything below
        /// it, so tier 2 pays one step and tier 3 pays three. Past the cap the bonus simply stops
        /// growing - the streak itself keeps counting, because jokers and the popup still care
        /// about how long it really is.
        /// </summary>
        public int ScoreCombo(int comboCount)
        {
            // The FIRST clearing turn is not a combo - it is just a clear. The bonus starts on
            // the second consecutive clearing turn, which is also where the "COMBO x2" popup
            // starts, so what the player sees and what they are paid line up.
            if (comboCount < 2)
            {
                return 0;
            }
            int tier = comboCount;
            if (config.MaxComboTier > 0 && tier > config.MaxComboTier)
            {
                tier = config.MaxComboTier;
            }
            int steps = (tier - 1) * tier / 2;
            return steps * config.ComboBonusPerStep;
        }

        public int ScoreTargetedBlock()
        {
            return config.TargetedBlockBonus;
        }

        public int ScoreGoldBonus(int goldCubesOnBoard)
        {
            return goldCubesOnBoard * config.GoldPointsPerCubePerTurn;
        }

        public double OvertimeRegularScoreFactor
        {
            get { return config.OvertimeRegularScoreFactor; }
        }

        public int ScoreOvertimeWinBonus(int roundThreshold, int overtimeLevel)
        {
            if (overtimeLevel < 1)
            {
                return 0;
            }
            double fraction = config.OvertimeWinBonusBaseFraction
                + config.OvertimeWinBonusStepFraction * (overtimeLevel - 1);
            return (int)System.Math.Round(roundThreshold * fraction);
        }

        public int ScoreScale
        {
            get { return config.ScoreScale; }
        }

        public int RetroPlacementBonus
        {
            get { return config.RetroPlacementBonus; }
        }
    }
}
