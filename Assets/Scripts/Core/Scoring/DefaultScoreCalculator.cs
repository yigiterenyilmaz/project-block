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
        /// The multiplier the n-th consecutive line-clearing turn applies to its own score.
        ///
        /// A MULTIPLIER, NOT A FLAT BONUS (2026-09-16, designer's call), and this is the second
        /// retune in a row for the same underlying reason. A flat bonus is worth least exactly
        /// when the turn is biggest - a streak built on four-line clears with a scoring build
        /// behind it got the same handful of points as one built on single rows - so a streak
        /// never actually felt like it was compounding. A multiplier makes the streak worth
        /// whatever the turns inside it are worth, which is what a combo is supposed to mean.
        ///
        /// A SHORT LADDER, from ScoringConfig.ComboMultipliers: x1, x1.5, x3, and the last entry
        /// holds for every turn past it. Getting to three is the reward; grinding to eleven is
        /// not. The streak itself keeps counting past the table, because jokers and the popup
        /// still care how long it really is.
        ///
        /// MIND WHAT A MULTIPLIER MULTIPLIES. It is applied through ScoreBreakdown.AddMultiplier,
        /// so it scales the base values AND every joker flat bonus on the turn - which is a real
        /// power increase over the flat version, and deliberate. It also means the combo is NOT
        /// trickled in overtime the way the old flat bonus was (see OvertimeRegularScoreFactor):
        /// multipliers were always exempt, so a streak is now worth full value there.
        /// </summary>
        public double ComboMultiplier(int comboCount)
        {
            // The FIRST clearing turn is not a combo - it is just a clear. The multiplier starts
            // on the second consecutive clearing turn, which is also where the "COMBO x2" popup
            // starts, so what the player sees and what they are paid line up.
            double[] table = config.ComboMultipliers;
            if (comboCount < 2 || table == null || table.Length == 0)
            {
                return 1.0;
            }
            // The table is indexed by streak, 1-based: entry 0 is the first clearing turn. Past
            // the end the last entry holds, which is what caps the ladder.
            int index = comboCount - 1;
            if (index >= table.Length)
            {
                index = table.Length - 1;
            }
            double factor = table[index];
            return factor > 0.0 ? factor : 1.0;
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

        public int WaterFallBonusPercent
        {
            get { return config.WaterFallBonusPercent; }
        }

        public int RetroPlacementBonus
        {
            get { return config.RetroPlacementBonus; }
        }
    }
}
