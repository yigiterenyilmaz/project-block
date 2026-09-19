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

        /// <summary>Bonus for the obsidian cubes STANDING IN the lines that went off. They are
        /// not destroyed - obsidian never is - so this is separate from ScoreLineExplosion, whose
        /// cube count only ever means cubes that broke (ScoringConfig.PointsPerObsidianInLine).
        /// </summary>
        int ScoreObsidianInLines(int obsidianCubesInLines);

        /// <summary>Bonus for emptying the board ("temizlik").</summary>
        int ScoreCleanSweep();

        /// <summary>
        /// The MULTIPLIER the <paramref name="comboCount"/>-th consecutive line-clearing turn
        /// applies to that turn's score (the "kombo" streak). Count is 1-based, and a combo
        /// STARTS ON THE SECOND clearing turn: anything below 2 returns 1.0, which is no
        /// multiplier at all.
        ///
        /// It is a MULTIPLIER rather than a flat bonus (2026-09-16, designer's call): a streak
        /// is supposed to make the turns inside it worth more, and a flat bonus is worth least
        /// exactly when the turn is biggest. See DefaultScoreCalculator for the table.
        /// </summary>
        double ComboMultiplier(int comboCount);

        /// <summary>Per-turn bonus for gold cubes sitting on the board.</summary>
        int ScoreGoldBonus(int goldCubesOnBoard);

        /// <summary>Flat bonus for breaking a "Hedefli" block's target cube first
        /// (ScoringConfig.TargetedBlockBonus).</summary>
        int ScoreTargetedBlock();

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

        /// <summary>Extra percent a clear (and its sweep) pays when water FELL into place to
        /// make it (ScoringConfig.WaterFallBonusPercent).</summary>
        int WaterFallBonusPercent { get; }
    }
}
