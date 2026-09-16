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

        /// <summary>
        /// "kombo": what the n-th consecutive line-clearing turn MULTIPLIES its own score by,
        /// indexed from the first clearing turn. x1, x1.5, x3 (2026-09-16, designer's call) - and
        /// the LAST ENTRY HOLDS for every turn past it, which is what caps the ladder without a
        /// separate cap field to keep in step with the table.
        ///
        /// A turn that clears no line resets the streak. These are multipliers, so the global
        /// ScoreScale does not apply to them - they scale whatever the turn was already worth,
        /// base values and joker flat bonuses alike.
        ///
        /// Lengthening the table is how the ladder gets longer; changing the last entry is how
        /// its ceiling moves. BALANCE PLACEHOLDERS, like everything else here.
        /// </summary>
        public double[] ComboMultipliers = { 1.0, 1.5, 3.0 };

        /// <summary>Score per cube destroyed by a line explosion.</summary>
        public int PointsPerCubeExploded = 1;

        /// <summary>What one OBSIDIAN cube pays for standing in a line that goes off. It is a
        /// rent, not a payout: the obsidian survives the clear, so the same cube pays again every
        /// time the row or column through it is completed - which is the whole point of it, and
        /// the reason this is worth many times PointsPerCubeExploded. An obsidian at the crossing
        /// of a cleared row AND a cleared column is paid for both, exactly as the base line score
        /// prices each axis as though it had exploded alone. Logical (small); the global
        /// ScoreScale lifts it like every other score. BALANCE PLACEHOLDER.</summary>
        public int PointsPerObsidianInLine = 18;

        /// <summary>"Hedefli": flat bonus for breaking a targeted block's TARGET cube in the
        /// first explosion that touches it. Flat rather than per-cube on purpose - what is being
        /// paid for is the aim, and a big block is already easier to hit. The cubes the payout
        /// then takes with it are priced as an ordinary explosion on top. Logical (small); the
        /// global ScoreScale lifts it like every other score.</summary>
        public int TargetedBlockBonus = 25;

        /// <summary>Extra score per line beyond the first when several explode at once.</summary>
        public int MultiLineBonusPerExtraLine = 10;

        /// <summary>Flat bonus for a clean sweep ("temizlik" - board fully emptied). LOGICAL, so
        /// the player sees it x ScoreScale: 50 here reads as 500 on screen.
        /// Rebalanced 2026-07-18: 150 dwarfed early thresholds and made overtime
        /// farming explode (1600+ points in round 1). Rebalanced again 2026-07-25: 75 (750 on
        /// screen) still paid too well against a 60-point first threshold.</summary>
        public int CleanSweepBonus = 50;

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
        /// n-th overtime win pays (Base + Step*(n-1)) * threshold: 0.20 / 0.35 / 0.50 / 0.65 for
        /// wins 1 / 2 / 3 / 4, still summing to ~1.05x threshold across three - roughly a second
        /// baseline, which is the calibration this pair exists to hold.
        ///
        /// Retuned 2026-09-06 from 0.25 / 0.10 to 0.20 / 0.15: the FIRST overtime pays a little
        /// less and every one after it a little more, so the reward accumulates rather than
        /// front-loading. Taking one lap and leaving used to be most of the value; the cost of a
        /// continue escalates (RoundRules.ContinueCostEscalation) and now the payout escalates
        /// harder than it does, which is what makes going deeper the interesting decision.
        /// The bonus flows through the score pipeline, so score jokers scale it.</summary>
        public double OvertimeWinBonusBaseFraction = 0.20;

        /// <summary>Growth per sequential overtime win (see OvertimeWinBonusBaseFraction).</summary>
        public double OvertimeWinBonusStepFraction = 0.15;
    }
}
