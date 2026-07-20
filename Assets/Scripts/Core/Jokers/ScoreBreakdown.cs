// PURPOSE: One turn's score, assembled in pieces so several jokers can modify it
// without fighting over a single int. CONFIRMED ORDERING: base values -> all flat
// additions (inventory order) -> all multipliers (inventory order) -> floor ONCE,
// then scale. RoundEngine fills the Base* fields; jokers add via TurnContext.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Mutable score accumulator for exactly one turn.</summary>
    public sealed class ScoreBreakdown
    {
        private readonly List<ScoreContribution> contributions = new List<ScoreContribution>();

        /// <summary>Points for the cubes placed this turn.</summary>
        public int BasePlacement { get; internal set; }

        /// <summary>Points for full rows/columns exploded this turn.</summary>
        public int BaseLines { get; internal set; }

        /// <summary>Clean-sweep bonus, if the sweep fired this turn.</summary>
        public int BaseSweep { get; internal set; }

        /// <summary>"kombo" bonus for a consecutive line-clearing turn (0 when no line cleared
        /// or the streak just reset). A regular base field, so overtime trickles it.</summary>
        public int BaseCombo { get; internal set; }

        /// <summary>Per-turn payout of the gold cubes sitting on the board.</summary>
        public int BaseGold { get; internal set; }

        /// <summary>Bonus for winning an overtime this turn (0 otherwise). Unlike the regular
        /// base fields it is NOT scaled by RegularScoreFactor - overtime taxes the
        /// regular play, not the win reward - but it IS subject to joker multipliers, so
        /// "point upgrades" raise it. See RoundEngine's overtime clean-sweep handling.</summary>
        public int BaseOvertimeBonus { get; internal set; }

        /// <summary>Multiplier on the REGULAR base fields (placement/lines/sweep/combo/gold).
        /// 1.0 normally; set below 1 during overtime so regular actions pay almost nothing
        /// (ScoringConfig.OvertimeRegularScoreFactor). Never touches BaseOvertimeBonus,
        /// FlatBonus or the multiplier stage.</summary>
        public double RegularScoreFactor { get; internal set; } = 1.0;

        /// <summary>Global economy multiplier (ScoringConfig.ScoreScale) applied to the whole
        /// turn total. 1 by default so a directly-constructed breakdown is unscaled; the engine
        /// sets it from the scorer each turn. Multipliers stay ratios - only the final points
        /// are scaled - so the "chips then mult" ordering is untouched.</summary>
        public int ScoreScale { get; internal set; } = 1;

        /// <summary>Sum of every flat bonus added by jokers (may be negative).</summary>
        public int FlatBonus { get; private set; }

        /// <summary>Product of every multiplier added by jokers. Starts at 1.</summary>
        public double Multiplier { get; private set; } = 1.0;

        /// <summary>Points granted AFTER the score was finalized (end-of-turn effects such as
        /// "Harcama bonusu"). They are not multiplied - the multiplier stage is already over -
        /// but they still belong to this turn and still count toward the threshold.</summary>
        public int LateFlat { get; private set; }

        /// <summary>Per-source log, in the order the contributions were applied.</summary>
        public IReadOnlyList<ScoreContribution> Contributions
        {
            get { return contributions; }
        }

        /// <summary>Everything before jokers touched it.</summary>
        public int BaseTotal
        {
            get { return BasePlacement + BaseLines + BaseSweep + BaseCombo + BaseGold; }
        }

        /// <summary>Final score of the turn. Floored once, per the ordering rule above, then
        /// multiplied by the global ScoreScale. Overtime taxes only the regular base
        /// (placement/lines/sweep/combo/gold via RegularScoreFactor); the overtime win bonus and
        /// all joker contributions keep their
        /// full weight. Late flats are scaled too so they stay in the same units as the rest.</summary>
        public int Total
        {
            get
            {
                double regular = BaseTotal * RegularScoreFactor;
                return (int)System.Math.Floor((regular + BaseOvertimeBonus + FlatBonus)
                    * Multiplier * ScoreScale) + LateFlat * ScoreScale;
            }
        }

        public void AddFlat(int amount, string source)
        {
            if (amount == 0)
            {
                return;
            }
            FlatBonus += amount;
            contributions.Add(new ScoreContribution(source, amount, 1.0));
        }

        /// <summary>Multiplies the running multiplier - a joker can never overwrite another's.</summary>
        public void AddMultiplier(double factor, string source)
        {
            if (factor == 1.0)
            {
                return;
            }
            Multiplier *= factor;
            contributions.Add(new ScoreContribution(source, 0, factor));
        }

        /// <summary>Adds points after finalization. RoundEngine routes here automatically -
        /// jokers always just call TurnContext.AddFlatScore.</summary>
        internal void AddLateFlat(int amount, string source)
        {
            if (amount == 0)
            {
                return;
            }
            LateFlat += amount;
            contributions.Add(new ScoreContribution(source, amount, 1.0));
        }

        internal void Reset()
        {
            contributions.Clear();
            BasePlacement = 0;
            BaseLines = 0;
            BaseSweep = 0;
            BaseCombo = 0;
            BaseGold = 0;
            BaseOvertimeBonus = 0;
            RegularScoreFactor = 1.0;
            ScoreScale = 1;
            FlatBonus = 0;
            Multiplier = 1.0;
            LateFlat = 0;
        }
    }
}
