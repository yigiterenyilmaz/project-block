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
    }
}
