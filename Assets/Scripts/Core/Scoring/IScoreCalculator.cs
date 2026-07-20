// PURPOSE: Score computation isolated behind an interface, so score-modifying
// systems can decorate or replace it without editing RoundEngine.

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
}
