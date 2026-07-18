// PURPOSE: One turn's score, assembled in pieces so several jokers can modify it without
// fighting over a single int. RoundEngine fills the Base* fields at their existing points
// in the turn; jokers then add flat bonuses and multipliers through TurnContext.
//
// CONFIRMED ORDERING RULE (the "chips then mult" of this game):
//   1. base values from IScoreCalculator
//   2. all flat additions, in joker inventory order (left to right = acquisition order)
//   3. all multipliers, in inventory order (they multiply each other, never overwrite)
//   4. floor ONCE at the very end - never round per contribution
//
// EXTENSION POINT: Contributions is a per-source log kept for score popups in the UI and
// for debugging joker interactions. Add new base fields here (not new int locals in
// RoundEngine) when a new scoring moment appears.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One (source, amount) entry of a turn's score, for UI popups and debugging.</summary>
    public readonly struct ScoreContribution
    {
        /// <summary>Joker DefId, or one of the "base." sources produced by the engine.</summary>
        public readonly string Source;

        /// <summary>Flat points added, or 0 for multiplier entries.</summary>
        public readonly int Flat;

        /// <summary>Multiplier applied, or 1.0 for flat entries.</summary>
        public readonly double Multiplier;

        public ScoreContribution(string source, int flat, double multiplier)
        {
            Source = source;
            Flat = flat;
            Multiplier = multiplier;
        }
    }

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

        /// <summary>Per-turn payout of the gold cubes sitting on the board.</summary>
        public int BaseGold { get; internal set; }

        /// <summary>Piggy-bank blocks that paid out this turn.</summary>
        public int BasePiggyBank { get; internal set; }

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
            get { return BasePlacement + BaseLines + BaseSweep + BaseGold + BasePiggyBank; }
        }

        /// <summary>Final score of the turn. Floored once, per the ordering rule above.</summary>
        public int Total
        {
            get { return (int)System.Math.Floor((BaseTotal + FlatBonus) * Multiplier) + LateFlat; }
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
            BaseGold = 0;
            BasePiggyBank = 0;
            FlatBonus = 0;
            Multiplier = 1.0;
            LateFlat = 0;
        }
    }
}
