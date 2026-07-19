// PURPOSE: All scoring tunables in one mutable object. RoundEngine reads these live
// through DefaultScoreCalculator, so future jokers can buff values mid-game
// (e.g. "bereket" permanently raises gained score) by mutating this instance.
// The numbers are BALANCE GUESSES, not confirmed design - tune freely.

namespace ProjectBlock.Core
{
    /// <summary>Tunable scoring values. See file header - numbers are placeholders.</summary>
    public sealed class ScoringConfig
    {
        /// <summary>Global multiplier on ALL earned score and the whole run economy - the one
        /// knob that makes the numbers "juicy" (2026-07-19). Every point a turn banks, every
        /// threshold, every market price and every sell value is multiplied by this, so the
        /// balance is unchanged while the numbers read ~10x bigger (600 instead of 60). Kept
        /// as a single scale rather than baking x10 into each field so the per-effect balance
        /// placeholders stay legible. Applied centrally: ScoreBreakdown.Total, the threshold
        /// checks, GameSession market prices, and the sell paths.</summary>
        public int ScoreScale = 10;

        /// <summary>Score per cube of a placed block. 0 by design (2026-07-19): merely
        /// placing blocks grants nothing - only clearing lines / sweeping scores. A dedicated
        /// joker re-grants points for placement, so this stays the baseline.</summary>
        public int PointsPerCubePlaced = 0;

        /// <summary>"retro" tetris mode: flat bonus added to a placement's score for every block
        /// placed while RoundRules.RetroMode is on - the reward for steering a falling piece.
        /// Logical (small); the global ScoreScale lifts it like every other score.</summary>
        public int RetroPlacementBonus = 3;

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

        /// <summary>Per-turn bonus per gold cube sitting on the board.</summary>
        public int GoldPointsPerCubePerTurn = 1;

        // ---- Overtime ("uzatma") scoring. Confirmed design (2026-07-19): once the threshold
        // is passed, regular actions must pay almost nothing, and the real reward is an
        // escalating bonus for each overtime WON (a clean sweep survived, then continued).
        // Calibrated so ~3 overtime wins roughly DOUBLE a round's baseline threshold. ----

        /// <summary>Multiplier applied to the REGULAR base score (placement, lines, base
        /// sweep, gold) on every turn once the threshold has been passed. Joker flat bonuses
        /// and multipliers are NOT trickled - "point upgrades" still pay in overtime. A small
        /// value here is the "regular actions give very little in overtime" rule.</summary>
        public double OvertimeRegularScoreFactor = 0.1;

        /// <summary>Bonus for WINNING an overtime, as a fraction of the round threshold. The
        /// n-th overtime win pays (Base + Step*(n-1)) * threshold. Defaults 0.25 / 0.35 / 0.45
        /// for wins 1 / 2 / 3, summing to ~1.05x threshold across three - roughly a second
        /// baseline. The bonus flows through the score pipeline, so score jokers scale it.</summary>
        public double OvertimeWinBonusBaseFraction = 0.25;

        /// <summary>Growth per sequential overtime win (see OvertimeWinBonusBaseFraction).</summary>
        public double OvertimeWinBonusStepFraction = 0.10;
    }
}
