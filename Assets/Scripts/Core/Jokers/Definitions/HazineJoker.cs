// PURPOSE: "Hazine" - the gamble joker. Two cells of the board are secretly marked at the
// start of every round, one with treasure and one with dynamite. The player cannot see
// which; they find out by exploding cubes on top of them.
//
// WHY CELLS AND NOT CUBES: a round begins with an EMPTY board, so there are no blocks to
// mark yet (confirmed 2026-07-23). The marks therefore sit on coordinates and wait - a mark
// the player never builds on simply never pays out, which is part of the gamble.
//
// THE THREE RULES THAT MAKE IT A GAMBLE:
//  - hitting BOTH in the same explosion cancels out: no reward, no penalty;
//  - hitting the treasure removes the dynamite, and vice versa - one find ends the hunt;
//  - overtime re-arms both marks, so carrying on rolls the dice again.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Hazine" - a hidden treasure and a hidden stick of dynamite on the board.</summary>
    public sealed class HazineJoker : Joker
    {
        /// <summary>Extra share of the explosion's score the treasure pays (0.5 = 1.5x).</summary>
        public double TreasureScoreBonus = 0.5;

        /// <summary>Smallest and largest market discount the treasure can grant.</summary>
        public double MinDiscount = 0.10;
        public double MaxDiscount = 0.30;

        /// <summary>Turns a card stays frozen when the dynamite calls for it.</summary>
        public int FreezeTurns = 3;

        /// <summary>Where the two marks sit, or null once that mark is gone.</summary>
        public GridPos? TreasureCell { get; private set; }
        public GridPos? DynamiteCell { get; private set; }

        /// <summary>What the last trigger did, for the UI to announce.</summary>
        public string LastOutcome { get; private set; }

        public HazineJoker()
            : base("hazine", "Hazine")
        {
            SetDescription(
                "Every round one hidden cell gets treasure and another gets dynamite. Explode "
                    + "the treasure for a reward, the dynamite for a penalty - hitting both at "
                    + "once cancels out, and finding either removes the other.",
                "Her raunt gizlice bir kareye hazine, bir kareye dinamit konur. Hazineyi "
                    + "patlatırsan ödül, dinamiti patlatırsan ceza alırsın - ikisine birden "
                    + "denk gelirsen birbirini götürür, birini bulmak diğerini kaldırır.");
            BaseSellValue = 60;
        }

        public override string StatusText
        {
            get
            {
                if (!TreasureCell.HasValue && !DynamiteCell.HasValue)
                {
                    return Loc.Pick("found", "bulundu");
                }
                if (!TreasureCell.HasValue)
                {
                    return Loc.Pick("dynamite left", "dinamit kaldı");
                }
                if (!DynamiteCell.HasValue)
                {
                    return Loc.Pick("treasure left", "hazine kaldı");
                }
                return Loc.Pick("buried", "gömülü");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            Arm(ctx);
        }

        /// <summary>Confirmed: playing on into overtime buries a fresh pair.</summary>
        public override void OnOvertimeStarted(RoundContext ctx)
        {
            Arm(ctx);
        }

        /// <summary>Picks two distinct cells of the board. Needs at least two playable cells,
        /// which every real board has.</summary>
        private void Arm(RoundContext ctx)
        {
            TreasureCell = null;
            DynamiteCell = null;
            LastOutcome = null;

            List<GridPos> cells = PlayableCells(ctx.Round.Board);
            if (cells.Count < 2)
            {
                return;
            }
            int a = ctx.Rng.NextInt(0, cells.Count);
            int b = ctx.Rng.NextInt(0, cells.Count - 1);
            if (b >= a)
            {
                b++; // pick a different cell without looping
            }
            TreasureCell = cells[a];
            DynamiteCell = cells[b];
        }

        /// <summary>
        /// Checked once per turn, after everything that could destroy a cube has run. Reads
        /// the turn's destruction log, so line explosions, fire chains, dynamite blocks and
        /// joker/power effects all count as "hitting" a mark.
        /// </summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            bool treasureHit = TreasureCell.HasValue
                && WasDestroyed(turn.Report.DestroyedCubes, TreasureCell.Value);
            bool dynamiteHit = DynamiteCell.HasValue
                && WasDestroyed(turn.Report.DestroyedCubes, DynamiteCell.Value);

            if (!treasureHit && !dynamiteHit)
            {
                return;
            }
            if (treasureHit && dynamiteHit)
            {
                // Both in one explosion: they cancel, and the hunt is over for this round.
                TreasureCell = null;
                DynamiteCell = null;
                LastOutcome = Loc.Pick("cancelled out", "birbirini götürdü");
                return;
            }
            if (treasureHit)
            {
                // Finding one clears the other - that is the confirmed rule.
                TreasureCell = null;
                DynamiteCell = null;
                GrantReward(turn);
                return;
            }
            TreasureCell = null;
            DynamiteCell = null;
            ApplyPenalty(turn);
        }

        // ------------------------------------------------------------------ rewards

        private void GrantReward(TurnContext turn)
        {
            // Four rewards; the power refill re-rolls when there is nothing to refill, so the
            // benefit is never wasted (confirmed 2026-07-23).
            var choices = new List<int> { 0, 1, 2, 3 };
            while (choices.Count > 0)
            {
                int pick = choices[turn.Rng.NextInt(0, choices.Count)];
                choices.Remove(pick);
                if (TryReward(turn, pick))
                {
                    return;
                }
            }
        }

        private bool TryReward(TurnContext turn, int which)
        {
            switch (which)
            {
                case 0:
                {
                    // 1.5x the EXPLOSION's score: the base line value is already banked, so
                    // paying the extra half of it lands the turn on one and a half.
                    int lines = turn.Report.Score != null ? turn.Report.Score.BaseLines : 0;
                    if (lines <= 0)
                    {
                        return false; // nothing exploded to multiply - try another reward
                    }
                    turn.AddFlatScore((int)(lines * TreasureScoreBonus), DefId);
                    LastOutcome = Loc.Pick("treasure: 1.5x explosion", "hazine: patlama 1.5x");
                    return true;
                }
                case 1:
                {
                    double span = MaxDiscount - MinDiscount;
                    double discount = MinDiscount + turn.Rng.NextDouble() * span;
                    turn.Session.AddMarketDiscount(discount);
                    int percent = (int)(discount * 100);
                    LastOutcome = Loc.Pick("treasure: " + percent + "% market discount",
                        "hazine: markette %" + percent + " indirim");
                    return true;
                }
                case 2:
                {
                    if (!turn.Session.Powers.RechargeOne())
                    {
                        return false; // nothing spent to refill - the benefit re-rolls
                    }
                    LastOutcome = Loc.Pick("treasure: a power refilled", "hazine: bir güç doldu");
                    return true;
                }
                default:
                {
                    BlockCard source = RandomOwnedCard(turn);
                    if (source == null)
                    {
                        return false;
                    }
                    BlockCard copy = turn.Session.CreateCard(source.Shape, source.Elements);
                    turn.Round.AddBonusCard(copy, BonusPlayOutcome.ExpireFromRound);
                    LastOutcome = Loc.Pick("treasure: a bonus card", "hazine: bonus kart");
                    return true;
                }
            }
        }

        // ----------------------------------------------------------------- penalties

        private void ApplyPenalty(TurnContext turn)
        {
            var choices = new List<int> { 0, 1, 2 };
            while (choices.Count > 0)
            {
                int pick = choices[turn.Rng.NextInt(0, choices.Count)];
                choices.Remove(pick);
                if (TryPenalty(turn, pick))
                {
                    return;
                }
            }
            LastOutcome = Loc.Pick("dynamite: no effect", "dinamit: etkisiz");
        }

        private bool TryPenalty(TurnContext turn, int which)
        {
            switch (which)
            {
                case 0:
                {
                    // Burn a charged power without using it: it waits for its normal recharge.
                    IReadOnlyList<Power> powers = turn.Session.Powers.Powers;
                    var charged = new List<Power>();
                    for (int i = 0; i < powers.Count; i++)
                    {
                        if (powers[i].Charged)
                        {
                            charged.Add(powers[i]);
                        }
                    }
                    if (charged.Count == 0)
                    {
                        return false;
                    }
                    Power victim = charged[turn.Rng.NextInt(0, charged.Count)];
                    turn.Session.Powers.BurnCharge(victim);
                    LastOutcome = Loc.Pick("dynamite: " + victim.DisplayName + " drained",
                        "dinamit: " + victim.DisplayName + " tükendi");
                    return true;
                }
                case 1:
                {
                    RoundEngine round = turn.Round;
                    var thawed = new List<BlockCard>();
                    for (int i = 0; i < round.Hand.Count; i++)
                    {
                        if (!round.IsFrozen(round.Hand[i].Id))
                        {
                            thawed.Add(round.Hand[i]);
                        }
                    }
                    if (thawed.Count == 0)
                    {
                        return false;
                    }
                    BlockCard victim = thawed[turn.Rng.NextInt(0, thawed.Count)];
                    round.FreezeHandCard(victim.Id, FreezeTurns);
                    LastOutcome = Loc.Pick("dynamite: a card frozen for " + FreezeTurns,
                        "dinamit: bir kart " + FreezeTurns + " tur dondu");
                    return true;
                }
                default:
                {
                    RoundEngine round = turn.Round;
                    if (round.Hand.Count == 0)
                    {
                        return false;
                    }
                    round.DiscardWholeHand();
                    round.RefillHandToSize();
                    LastOutcome = Loc.Pick("dynamite: hand discarded", "dinamit: el ıskartaya gitti");
                    return true;
                }
            }
        }

        // ------------------------------------------------------------------- helpers

        private static bool WasDestroyed(IReadOnlyList<DestroyedCube> destroyed, GridPos cell)
        {
            for (int i = 0; i < destroyed.Count; i++)
            {
                if (destroyed[i].Pos.Equals(cell))
                {
                    return true;
                }
            }
            return false;
        }

        private static BlockCard RandomOwnedCard(TurnContext turn)
        {
            IReadOnlyList<BlockCard> owned = turn.Session.OwnedCards;
            return owned.Count == 0 ? null : owned[turn.Rng.NextInt(0, owned.Count)];
        }

        private static List<GridPos> PlayableCells(GameBoard board)
        {
            var cells = new List<GridPos>();
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                for (int x = board.MinX; x < board.MinX + board.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    if (board.IsInside(pos))
                    {
                        cells.Add(pos);
                    }
                }
            }
            return cells;
        }
    }
}
