// PURPOSE: The duel's computer player ("Blackjack"). It decides each turn's card, how to wear it
// and where to put it - deterministically, and without ever looking at the order of its own pile.
//
// HOW IT DECIDES. Every legal move (DuelOptions: each held card, each element an alchemical card
// can be, each form a fox can take, each turn a mechanical block can make, each cell it may go)
// is PLAYED on a copy of the computer's round (RoundEngine.CloneForPlanning). The rules
// themselves say what the move earns - the line it clears, the fire chain it lights, the water it
// drops, the obsidian it runs a line through, the dynamite it sets off, the combo multiplier that
// clear gets - so there is no second model of any element that could disagree with the first.
// What the move LEAVES is weighed by DuelEvaluator (the next hand's best clear, the lines in
// reach, the cards that will have nowhere to go...). That is the first ply: move points + the
// value of the position.
//
// The best few first moves (Weights.PlyTwoWidth, plus the best one for each held card, so a
// card is never written off on a shallow look) are then looked at a second move deep: on each of
// those copies, every move of the cards the computer KNOWS it still holds is played too, and the
// first move is scored by its own points plus the best follow-up's points and position. That is
// where a setup is seen for what it is - a placement that clears nothing now but lets the card
// still in hand clear two lines next turn, under a combo multiplier.
//
// WHAT IT KNOWS. Its own hand, and the CONTENTS of its pile. The card it draws after a move is
// part of the unknown pile in every evaluation, and the second ply only plays the cards it held
// before the move - a copy of the round does know the real top card, and using it would be
// cheating at a table where the player cannot do the same.
//
// DETERMINISTIC: the same round always gets the same move. Candidates are generated in a fixed
// order and ties keep the earlier one.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    public sealed class DuelPlanner
    {
        internal DuelWeights Weights { get; }

        private readonly DuelEvaluator evaluator;

        /// <summary>Turns resolved on copies by the last Choose - for the tests and the lab.</summary>
        internal int LastSimulations { get; private set; }

        public DuelPlanner()
            : this(new DuelWeights())
        {
        }

        internal DuelPlanner(DuelWeights weights)
        {
            Weights = weights ?? new DuelWeights();
            evaluator = new DuelEvaluator(Weights);
        }

        private sealed class Candidate
        {
            public DuelMove Move;
            public RoundEngine After;
            public int Order;
        }

        /// <summary>The move to make in <paramref name="side"/>, or null when nothing it holds
        /// fits anywhere. <paramref name="side"/> is never changed.</summary>
        public DuelMove Choose(RoundEngine side)
        {
            LastSimulations = 0;
            if (side == null || side.Hand.Count == 0 || side.Status != RoundStatus.InProgress)
            {
                return null;
            }
            evaluator.BeginDecision();
            var known = new HashSet<int>();
            for (int i = 0; i < side.Hand.Count; i++)
            {
                known.Add(side.Hand[i].Id);
            }

            // ---- ply 1: every legal move, played and weighed
            List<DuelMove> moves = Moves(side, known);
            var scored = new List<Candidate>(moves.Count);
            for (int i = 0; i < moves.Count; i++)
            {
                DuelMove move = moves[i];
                RoundEngine after = Play(side, move);
                if (after == null)
                {
                    continue;
                }
                var rest = new HashSet<int>(known);
                rest.Remove(move.CardId);
                move.Gain = after.RoundScore - side.RoundScore;
                move.Value = move.Gain + evaluator.Evaluate(after, rest);
                scored.Add(new Candidate { Move = move, After = after, Order = i });
            }
            if (scored.Count == 0)
            {
                return null;
            }
            scored.Sort(ByValue);

            if (Weights.Depth < 2)
            {
                return scored[0].Move;
            }

            // ---- ply 2: the best few, a second move deep with the cards still known.
            // How many is a BUDGET, counted in moves and never in time (a plan must be the same
            // on every machine): a fox in hand is a few hundred moves on its own, and looking
            // six first moves deep through all of them would cost seconds.
            int followUps = Math.Max(1, moves.Count * Math.Max(1, known.Count - 1) / Math.Max(1, known.Count));
            int width = Math.Max(2, Math.Min(Weights.PlyTwoWidth, Weights.PlyTwoBudget / followUps));
            var deep = new List<Candidate>();
            var bestPerCard = new HashSet<int>();
            for (int i = 0; i < scored.Count; i++)
            {
                bool wide = deep.Count < width;
                bool firstOfCard = bestPerCard.Add(scored[i].Move.CardId);
                if (wide || (firstOfCard && deep.Count < width + known.Count))
                {
                    deep.Add(scored[i]);
                }
            }
            Candidate best = null;
            double bestValue = double.NegativeInfinity;
            for (int i = 0; i < deep.Count; i++)
            {
                Candidate c = deep[i];
                var rest = new HashSet<int>(known);
                rest.Remove(c.Move.CardId);
                double value = c.Move.Value;
                if (rest.Count > 0)
                {
                    double follow = BestFollowUp(c.After, rest);
                    if (!double.IsNegativeInfinity(follow))
                    {
                        value = c.Move.Gain + follow;
                    }
                }
                if (value > bestValue || (value == bestValue && best != null && c.Order < best.Order))
                {
                    best = c;
                    bestValue = value;
                }
            }
            best.Move.Value = bestValue;
            return best.Move;
        }

        /// <summary>The best second move with the cards the planner already knew, from the round a
        /// first move left: its points plus the position it leaves. -inf when none of them fits.</summary>
        private double BestFollowUp(RoundEngine after, HashSet<int> knownLeft)
        {
            List<DuelMove> moves = Moves(after, knownLeft);
            double best = double.NegativeInfinity;
            for (int i = 0; i < moves.Count; i++)
            {
                RoundEngine next = Play(after, moves[i]);
                if (next == null)
                {
                    continue;
                }
                var rest = new HashSet<int>(knownLeft);
                rest.Remove(moves[i].CardId);
                double value = (next.RoundScore - after.RoundScore) + evaluator.Evaluate(next, rest);
                if (value > best)
                {
                    best = value;
                }
            }
            return best;
        }

        private RoundEngine Play(RoundEngine from, DuelMove move)
        {
            RoundEngine copy = from.CloneForPlanning();
            LastSimulations++;
            return move.ApplyTo(copy) != null ? copy : null;
        }

        /// <summary>Every distinct legal move of the held cards in <paramref name="only"/>, in a
        /// fixed order. Two cards that play exactly alike are tried once.</summary>
        /// <summary>Every distinct legal move of everything <paramref name="round"/> holds.</summary>
        internal static List<DuelMove> LegalMoves(RoundEngine round)
        {
            var all = new HashSet<int>();
            for (int i = 0; i < round.Hand.Count; i++)
            {
                all.Add(round.Hand[i].Id);
            }
            return Moves(round, all);
        }

        private static List<DuelMove> Moves(RoundEngine round, ICollection<int> only)
        {
            var moves = new List<DuelMove>();
            var seen = new HashSet<string>();
            var origins = new List<GridPos>();
            for (int h = 0; h < round.Hand.Count; h++)
            {
                BlockCard card = round.Hand[h];
                if (!only.Contains(card.Id) || round.IsFrozen(card.Id))
                {
                    continue;
                }
                string type = DuelOptions.TypeKey(card);
                List<CardOption> options = DuelOptions.Of(round, card);
                for (int o = 0; o < options.Count; o++)
                {
                    CardOption option = options[o];
                    DuelOptions.Origins(round, card, option, origins);
                    for (int i = 0; i < origins.Count; i++)
                    {
                        string key = type + "#" + (option.Element.HasValue ? (int)option.Element.Value : -1)
                            + "#" + option.Shape.CanonicalKey + "@" + origins[i].X + "," + origins[i].Y;
                        if (!seen.Add(key))
                        {
                            continue;
                        }
                        moves.Add(new DuelMove
                        {
                            CardId = card.Id,
                            Element = option.Element,
                            FoxShape = option.Fox,
                            Rotation = option.Rotation,
                            Origin = origins[i],
                            Shape = option.Shape
                        });
                    }
                }
            }
            return moves;
        }

        private static int ByValue(Candidate a, Candidate b)
        {
            int byValue = b.Move.Value.CompareTo(a.Move.Value);
            return byValue != 0 ? byValue : a.Order.CompareTo(b.Order);
        }
    }
}
