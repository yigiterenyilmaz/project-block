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

        public int ScoreCleanSweep()
        {
            return config.CleanSweepBonus;
        }

        public int ScoreCombo(int comboCount)
        {
            // The FIRST clearing turn is not a combo - it is just a clear. The bonus starts on
            // the second consecutive clearing turn, which is also where the "COMBO x2" popup
            // starts, so what the player sees and what they are paid line up.
            if (comboCount < 2)
            {
                return 0;
            }
            return (comboCount - 1) * config.ComboBonusPerStep;
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
