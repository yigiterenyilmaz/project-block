// PURPOSE: Score computation, isolated behind an interface on purpose.
// EXTENSION POINT (MAIN JOKER HOOK): score-modifying jokers ("çığ", "dondurma",
// "Siyam", "bereket", "Batak" payouts...) should DECORATE or replace this interface
// rather than editing RoundEngine. When jokers land, wrap DefaultScoreCalculator in a
// pipeline that sees the whole TurnReport-in-progress; extend the method signatures
// then - they are intentionally minimal for the base game.

namespace ProjectBlock.Core
{
    /// <summary>Computes score for the three base scoring moments of a turn.</summary>
    public interface IScoreCalculator
    {
        /// <summary>Score for placing a block of the given cube count.</summary>
        int ScorePlacement(int cubesPlaced);

        /// <summary>Score for exploding full lines (lineCount = rows + columns).</summary>
        int ScoreLineExplosion(int lineCount, int cubesExploded);

        /// <summary>Bonus for emptying the board ("temizlik").</summary>
        int ScoreCleanSweep();

        /// <summary>Bonus for the <paramref name="comboCount"/>-th consecutive line-clearing
        /// turn (the "kombo" streak). Count is 1-based; count &lt; 1 pays 0.</summary>
        int ScoreCombo(int comboCount);

        /// <summary>Per-turn bonus for gold cubes sitting on the board.</summary>
        int ScoreGoldBonus(int goldCubesOnBoard);

        /// <summary>Multiplier applied to the regular base score of a turn played in overtime
        /// (1.0 before the threshold). See ScoringConfig.OvertimeRegularScoreFactor.</summary>
        double OvertimeRegularScoreFactor { get; }

        /// <summary>Bonus for winning the <paramref name="overtimeLevel"/>-th sequential
        /// overtime, scaled to the round threshold. Level is 1-based; level &lt; 1 pays 0.</summary>
        int ScoreOvertimeWinBonus(int roundThreshold, int overtimeLevel);

        /// <summary>Global economy multiplier (ScoringConfig.ScoreScale). Applied by the engine
        /// to banked score and threshold checks, and by GameSession to prices and sells.</summary>
        int ScoreScale { get; }

        /// <summary>Flat bonus added to a placement's score while retro (tetris) mode is on
        /// (ScoringConfig.RetroPlacementBonus). The engine adds it in ResolvePlacement.</summary>
        int RetroPlacementBonus { get; }
    }

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
            if (comboCount < 1)
            {
                return 0;
            }
            return comboCount * config.ComboBonusPerStep;
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
