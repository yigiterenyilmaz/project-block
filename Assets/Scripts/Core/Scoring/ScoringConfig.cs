// PURPOSE: All scoring tunables in one mutable object. RoundEngine reads these live
// through DefaultScoreCalculator, so future jokers can buff values mid-game
// (e.g. "bereket" permanently raises gained score) by mutating this instance.
// The numbers are BALANCE GUESSES, not confirmed design - tune freely.

namespace ProjectBlock.Core
{
    /// <summary>Tunable scoring values. See file header - numbers are placeholders.</summary>
    public sealed class ScoringConfig
    {
        /// <summary>Score per cube of a placed block.</summary>
        public int PointsPerCubePlaced = 1;

        /// <summary>Base score per exploded full row/column.</summary>
        public int PointsPerLine = 10;

        /// <summary>Score per cube destroyed by a line explosion.</summary>
        public int PointsPerCubeExploded = 1;

        /// <summary>Extra score per line beyond the first when several explode at once.</summary>
        public int MultiLineBonusPerExtraLine = 10;

        /// <summary>Flat bonus for a clean sweep ("temizlik" - board fully emptied).
        /// Rebalanced 2026-07-18: 150 dwarfed early thresholds and made overtime
        /// farming explode (1600+ points in round 1).</summary>
        public int CleanSweepBonus = 75;
    }
}
