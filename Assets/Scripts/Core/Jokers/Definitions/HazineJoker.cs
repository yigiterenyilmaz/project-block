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
// WHAT COUNTS AS BLOWING A MARK OPEN: ANY destruction of the cube standing on it, from any source
// and at any time - a line, a fire chain, a joker or the boss at the end of a turn, a quake
// rescuing a dead end, a power used between turns. It used to be read off the TURN's destruction
// log from this joker's own end-of-turn hook, and three things slipped past that: a power used
// between turns (no turn, no log - the cube was gone and the mark stayed buried), anything that
// destroyed AFTER this hook in the same turn (a joker further right, the boss, "Deprem"), and the
// order of the jokers deciding which of those counted. Now the engine's destruction feed is read
// from a cursor at every settle point (Joker.OnDestructionSettled), so none of them can.
//
// WHAT THE PLAYER SEES comes from LastFind (HazineVisuals): the marks that were blown open and
// the effect that was really applied, written beside the code that applied it. Most of what the
// treasure pays and everything the dynamite takes is NOT score, and the report says so.
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

        /// <summary>
        /// This turn's find, for the View: a NEW object per find, matched by identity, never
        /// saved, and never carrying the location of a mark that was not found. See HazineVisuals.
        /// </summary>
        [field: NotSaved]
        public HazineVisuals LastFind { get; private set; }

        /// <summary>How far into RoundEngine.DestructionFeed this joker has read. Not saved: the
        /// feed itself is not, and a loaded round starts both from nothing.</summary>
        [NotSaved]
        private int feedCursor;

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
            LastFind = null;
            LastOutcome = null;
            Arm(ctx);
        }

        /// <summary>Confirmed: playing on into overtime buries a fresh pair.
        ///
        /// It does NOT forget the last find. Overtime opens on the very turn that crossed the
        /// threshold, a step after that turn's find was paid - clearing it here threw away the
        /// reveal (and the outcome text) of exactly the find that happened on the crossing turn,
        /// so the reward or the penalty was applied and the player was shown nothing. The View
        /// matches a find by identity, so keeping it replays nothing.</summary>
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
            // Cubes lost before the marks were buried cannot have uncovered them.
            feedCursor = ctx.Round.DestructionFeed.Count;

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
        /// Checked whenever destruction comes to rest (see the file header): everything lost
        /// since the last check, from any source, is what the marks are tested against. Cubes
        /// lost in one settle are one "explosion" for the both-at-once rule.
        /// </summary>
        public override void OnDestructionSettled(RoundContext ctx)
        {
            IReadOnlyList<DestroyedCube> feed = ctx.Round.DestructionFeed;
            if (feedCursor > feed.Count)
            {
                feedCursor = 0; // a feed that started over (never across a live round)
            }
            var lost = new List<DestroyedCube>();
            for (int i = feedCursor; i < feed.Count; i++)
            {
                lost.Add(feed[i]);
            }
            feedCursor = feed.Count;
            if (lost.Count == 0)
            {
                return;
            }
            bool treasureHit = TreasureCell.HasValue && WasDestroyed(lost, TreasureCell.Value);
            bool dynamiteHit = DynamiteCell.HasValue && WasDestroyed(lost, DynamiteCell.Value);

            if (!treasureHit && !dynamiteHit)
            {
                return;
            }
            // Written before the marks are cleared, and holding ONLY the marks that were hit.
            var find = new HazineVisuals
            {
                Result = treasureHit && dynamiteHit ? HazineResult.BothCancelled
                    : treasureHit ? HazineResult.TreasureOnly : HazineResult.DynamiteOnly
            };
            if (treasureHit)
            {
                find.Discoveries.Add(Discovery(lost, TreasureCell.Value, true));
            }
            if (dynamiteHit)
            {
                find.Discoveries.Add(Discovery(lost, DynamiteCell.Value, false));
            }
            find.Seed = SeedOf(find);
            LastFind = find;

            if (treasureHit && dynamiteHit)
            {
                // Both in one explosion: they cancel, and the hunt is over for this round.
                TreasureCell = null;
                DynamiteCell = null;
                LastOutcome = Loc.Pick("cancelled out", "birbirini götürdü");
                find.Effect = HazineEffect.None;
                return;
            }
            if (treasureHit)
            {
                // Finding one clears the other - that is the confirmed rule.
                TreasureCell = null;
                DynamiteCell = null;
                GrantReward(ctx, find);
                return;
            }
            TreasureCell = null;
            DynamiteCell = null;
            ApplyPenalty(ctx, find);
            if (find.Effect == HazineEffect.HandDiscarded || find.Effect == HazineEffect.CardFrozen)
            {
                // Between turns nothing else will ask whether the new hand still has a move.
                ctx.Round.RecheckDeadEndBetweenTurns();
            }
        }

        // ------------------------------------------------------------------ rewards

        private void GrantReward(RoundContext turn, HazineVisuals find)
        {
            // Four rewards; the power refill re-rolls when there is nothing to refill, so the
            // benefit is never wasted (confirmed 2026-07-23).
            var choices = new List<int> { 0, 1, 2, 3 };
            while (choices.Count > 0)
            {
                int pick = choices[turn.Rng.NextInt(0, choices.Count)];
                choices.Remove(pick);
                if (TryReward(turn, pick, find))
                {
                    return;
                }
            }
        }

        private bool TryReward(RoundContext ctx, int which, HazineVisuals find)
        {
            switch (which)
            {
                case 0:
                {
                    // 1.5x the EXPLOSION's score: the base line value is already banked, so
                    // paying the extra half of it lands the turn on one and a half. Only inside
                    // the turn whose line it was - between turns there is no explosion to scale.
                    TurnContext turn = ctx.Round.CurrentTurnContext;
                    ScoreBreakdown score = turn != null ? turn.Report.Score : null;
                    int lines = score != null ? score.BaseLines : 0;
                    if (lines <= 0)
                    {
                        return false; // nothing exploded to multiply - try another reward
                    }
                    // Half of what the line really BANKED: in overtime the line itself is taxed
                    // down to a trickle (RegularScoreFactor), and half of the untaxed value paid
                    // five times what the line did.
                    double banked = lines * score.RegularScoreFactor;
                    int bonus = (int)System.Math.Round(banked * TreasureScoreBonus);
                    if (bonus < 1)
                    {
                        bonus = 1;
                    }
                    // MEASURED around the payment, so an inversion, the score scale and the floor
                    // are all in the number the player is shown.
                    int before = ctx.Round.RoundScore;
                    turn.AddFlatScore(bonus, DefId);
                    find.Effect = HazineEffect.ExplosionBonus;
                    find.ScoreDelta = ctx.Round.RoundScore - before;
                    LastOutcome = Loc.Pick("treasure: 1.5x explosion", "hazine: patlama 1.5x");
                    return true;
                }
                case 1:
                {
                    double span = MaxDiscount - MinDiscount;
                    double discount = MinDiscount + ctx.Rng.NextDouble() * span;
                    ctx.Session.AddMarketDiscount(discount);
                    int percent = (int)(discount * 100);
                    find.Effect = HazineEffect.MarketDiscount;
                    find.Amount = percent;
                    LastOutcome = Loc.Pick("treasure: " + percent + "% market discount",
                        "hazine: markette %" + percent + " indirim");
                    return true;
                }
                case 2:
                {
                    IReadOnlyList<Power> before = ctx.Session.Powers.Powers;
                    var spent = new List<Power>();
                    for (int i = 0; i < before.Count; i++)
                    {
                        if (!before[i].Charged)
                        {
                            spent.Add(before[i]);
                        }
                    }
                    if (!ctx.Session.Powers.RechargeOne())
                    {
                        return false; // nothing spent to refill - the benefit re-rolls
                    }
                    find.Effect = HazineEffect.PowerRefilled;
                    // Which one: the power that was spent and is not any more. Read, never drawn.
                    for (int i = 0; i < spent.Count; i++)
                    {
                        if (spent[i].Charged)
                        {
                            find.PowerId = spent[i].InstanceId;
                            find.PowerName = spent[i].DisplayName;
                            break;
                        }
                    }
                    LastOutcome = Loc.Pick("treasure: a power refilled", "hazine: bir güç doldu");
                    return true;
                }
                default:
                {
                    BlockCard source = RandomOwnedCard(ctx);
                    if (source == null)
                    {
                        return false;
                    }
                    BlockCard copy = ctx.Session.CreateCard(source.Shape, source.Elements);
                    copy.KeepChoiceOf(source);
                    ctx.Round.AddBonusCard(copy, BonusPlayOutcome.ExpireFromRound);
                    find.Effect = HazineEffect.BonusCard;
                    find.CardId = copy.Id;
                    LastOutcome = Loc.Pick("treasure: a bonus card", "hazine: bonus kart");
                    return true;
                }
            }
        }

        // ----------------------------------------------------------------- penalties

        private void ApplyPenalty(RoundContext turn, HazineVisuals find)
        {
            var choices = new List<int> { 0, 1, 2 };
            while (choices.Count > 0)
            {
                int pick = choices[turn.Rng.NextInt(0, choices.Count)];
                choices.Remove(pick);
                if (TryPenalty(turn, pick, find))
                {
                    return;
                }
            }
            find.Effect = HazineEffect.Fizzled;
            LastOutcome = Loc.Pick("dynamite: no effect", "dinamit: etkisiz");
        }

        private bool TryPenalty(RoundContext turn, int which, HazineVisuals find)
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
                    find.Effect = HazineEffect.PowerDrained;
                    find.PowerId = victim.InstanceId;
                    find.PowerName = victim.DisplayName;
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
                    find.Effect = HazineEffect.CardFrozen;
                    find.CardId = victim.Id;
                    find.Amount = FreezeTurns;
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
                    find.Effect = HazineEffect.HandDiscarded;
                    find.Amount = round.Hand.Count;
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

        private static HazineDiscovery Discovery(IReadOnlyList<DestroyedCube> destroyed,
            GridPos cell, bool treasure)
        {
            var found = new HazineDiscovery { Cell = cell, IsTreasure = treasure };
            for (int i = 0; i < destroyed.Count; i++)
            {
                if (destroyed[i].Pos.Equals(cell))
                {
                    found.Cube = destroyed[i].Cube;
                    break;
                }
            }
            return found;
        }

        /// <summary>Presentation seed from the found cells only - never the round's random
        /// source, which the reward draw is still to spend.</summary>
        private static uint SeedOf(HazineVisuals find)
        {
            uint seed = 2166136261u;
            for (int i = 0; i < find.Discoveries.Count; i++)
            {
                GridPos c = find.Discoveries[i].Cell;
                seed = (seed ^ unchecked((uint)(c.X * 73856093))) * 16777619u;
                seed = (seed ^ unchecked((uint)(c.Y * 19349663))) * 16777619u;
            }
            return seed;
        }

        private static BlockCard RandomOwnedCard(RoundContext turn)
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
