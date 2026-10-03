// PURPOSE: How well the duel's computer plays ("Blackjack") - measured, not asserted. Each deal
// is a half-deck like the one the duel hands the computer (twelve cards off the Classic shapes,
// a third of them elemental: fire, water, obsidian, gold, dynamite, fox, mechanical, ghost,
// transparent, an alchemical fire/water), played out to the end on a 7x7 arena by four players:
//   planner  - the real one (two plies)
//   shallow  - the planner a single ply deep (its position value, no follow-up)
//   greedy   - whatever pays most on the spot
//   random   - any legal move
// The same deals for all four. `dotnet run --project Tools/CoreTests -- duelbench [deals]` prints
// the averages and the time a decision takes; the assertion suite runs a small version of it.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using ProjectBlock.Core;

public static class DuelBench
{
    public enum Policy { Planner, Shallow, Greedy, Random }

    internal sealed class Result
    {
        public double Mean;
        public double Min = double.MaxValue;
        public double Max;
        public double MeanTurnMs;
        public double MaxTurnMs;
        public double MeanSimulations;
    }

    private static readonly BlockElement[][] ElementMix =
    {
        new[] { BlockElement.Fire },
        new[] { BlockElement.Water },
        new[] { BlockElement.Obsidian },
        new[] { BlockElement.Gold },
        new[] { BlockElement.Dynamite },
        new[] { BlockElement.Fox },
        new[] { BlockElement.Mechanical },
        new[] { BlockElement.Ghost },
        new[] { BlockElement.Transparent },
        new[] { BlockElement.Fire, BlockElement.Water }
    };

    /// <summary>The cards of one deal: twelve Classic shapes, four of them elemental.</summary>
    internal static List<BlockCard> Deal(int seed)
    {
        var random = new Random(seed * 7919 + 13);
        var shapes = new List<BlockShape>(DeckLibrary.Classic.FixedShapes);
        for (int i = shapes.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            BlockShape swap = shapes[i];
            shapes[i] = shapes[j];
            shapes[j] = swap;
        }
        var cards = new List<BlockCard>();
        for (int i = 0; i < 12; i++)
        {
            BlockElement[] elements = i % 3 == 0 ? ElementMix[random.Next(ElementMix.Length)] : null;
            cards.Add(elements == null
                ? new BlockCard(1000 + i, shapes[i])
                : new BlockCard(1000 + i, shapes[i], elements));
        }
        return cards;
    }

    internal static IScoreCalculator Scorer()
    {
        var scorer = new DefaultScoreCalculator(new ScoringConfig());
        scorer.Threshold = () => 135;
        return scorer;
    }

    /// <summary>Plays one deal out with one policy; returns the points banked (logical).</summary>
    internal static double PlayOut(int seed, Policy policy, DuelWeights weights, Result timing)
    {
        IScoreCalculator scorer = Scorer();
        var config = new RoundConfig(1, 7, 7, 135);
        RoundEngine side = RoundEngine.CreateDuelSide(config, Deal(seed), scorer);
        var planner = new DuelPlanner(weights ?? new DuelWeights());
        var random = new Random(seed * 31 + 7);
        int turns = 0;
        while (side.Hand.Count > 0 && turns < 40)
        {
            var clock = Stopwatch.StartNew();
            DuelMove move = Pick(side, policy, planner, random);
            clock.Stop();
            if (move == null)
            {
                break;
            }
            if (timing != null && (policy == Policy.Planner || policy == Policy.Shallow))
            {
                double ms = clock.Elapsed.TotalMilliseconds;
                timing.MeanTurnMs += ms;
                timing.MaxTurnMs = Math.Max(timing.MaxTurnMs, ms);
                timing.MeanSimulations += planner.LastSimulations;
            }
            if (move.ApplyTo(side) == null)
            {
                throw new InvalidOperationException("A chosen move was refused: " + move);
            }
            turns++;
        }
        if (timing != null && turns > 0)
        {
            timing.MeanTurnMs /= turns;
            timing.MeanSimulations /= turns;
        }
        return side.RoundScore / (double)scorer.ScoreScale;
    }

    private static DuelMove Pick(RoundEngine side, Policy policy, DuelPlanner planner, Random random)
    {
        if (policy == Policy.Planner || policy == Policy.Shallow)
        {
            return planner.Choose(side);
        }
        List<DuelMove> moves = DuelPlanner.LegalMoves(side);
        if (moves.Count == 0)
        {
            return null;
        }
        if (policy == Policy.Random)
        {
            return moves[random.Next(moves.Count)];
        }
        DuelMove best = null;
        double bestGain = double.NegativeInfinity;
        foreach (DuelMove move in moves)
        {
            RoundEngine copy = side.CloneForPlanning();
            if (move.ApplyTo(copy) == null)
            {
                continue;
            }
            double gain = copy.RoundScore - side.RoundScore;
            if (gain > bestGain)
            {
                bestGain = gain;
                best = move;
            }
        }
        return best;
    }

    internal static Result Run(Policy policy, int deals, DuelWeights weights)
    {
        return Run(policy, deals, weights, 1);
    }

    internal static Result Run(Policy policy, int deals, DuelWeights weights, int firstSeed)
    {
        var result = new Result();
        double sum = 0.0;
        double turnMs = 0.0;
        double sims = 0.0;
        for (int seed = firstSeed; seed < firstSeed + deals; seed++)
        {
            var timing = new Result();
            DuelWeights w = weights;
            if (policy == Policy.Shallow)
            {
                w = (weights ?? new DuelWeights()).Copy();
                w.Depth = 1;
            }
            double score = PlayOut(seed, policy, w, timing);
            sum += score;
            result.Min = Math.Min(result.Min, score);
            result.Max = Math.Max(result.Max, score);
            turnMs += timing.MeanTurnMs;
            result.MaxTurnMs = Math.Max(result.MaxTurnMs, timing.MaxTurnMs);
            sims += timing.MeanSimulations;
        }
        result.Mean = sum / deals;
        result.MeanTurnMs = turnMs / deals;
        result.MeanSimulations = sims / deals;
        return result;
    }

    /// <summary>
    /// Coordinate search over the evaluator's weights, one at a time, keeping a change only when
    /// it raises the single-ply planner's mean over the same deals (the evaluator is all a single
    /// ply has, so that is where its weights show). Prints the path it took.
    /// </summary>
    public static string Tune(int deals, int rounds)
    {
        return Tune(deals, rounds, Policy.Shallow, 1);
    }

    /// <summary>As Tune, for either depth, on the deals from <paramref name="firstSeed"/>.</summary>
    public static string Tune(int deals, int rounds, Policy policy, int firstSeed)
    {
        var sb = new StringBuilder();
        DuelWeights best = new DuelWeights();
        best.Depth = policy == Policy.Shallow ? 1 : 2;
        double bestScore = Run(policy, deals, best, firstSeed).Mean;
        Console.Out.Write("start " + bestScore.ToString("0.0") + "\n");
        sb.Append("start ").Append(bestScore.ToString("0.0")).Append('\n');
        string[] names = { "Ready", "Streak", "StreakGuard", "Lines", "Theta", "Uncoverable", "DeadHeld",
            "DeadFuture", "Mobility", "DeadCell", "Gold", "Dynamite" };
        double[] factors = { 0.5, 0.75, 1.33, 2.0 };
        for (int round = 0; round < rounds; round++)
        {
            foreach (string name in names)
            {
                var field = typeof(DuelWeights).GetField(name);
                double current = (double)field.GetValue(best);
                foreach (double factor in factors)
                {
                    DuelWeights trial = best.Copy();
                    double value = current == 0.0 ? factor - 0.5 : current * factor;
                    if (name == "Theta" && value >= 0.98) value = 0.95;
                    field.SetValue(trial, value);
                    double score = Run(policy, deals, trial, firstSeed).Mean;
                    if (score > bestScore + 0.5)
                    {
                        bestScore = score;
                        best = trial;
                        sb.Append(name).Append(" = ").Append(value.ToString("0.###"))
                            .Append("  -> ").Append(score.ToString("0.0")).Append('\n');
                        Console.Out.Write(name + " = " + value.ToString("0.###") + "  -> "
                            + score.ToString("0.0") + "\n");
                    }
                }
            }
        }
        sb.Append("final ").Append(bestScore.ToString("0.0")).Append('\n');
        foreach (string name in names)
        {
            double value = (double)typeof(DuelWeights).GetField(name).GetValue(best);
            sb.Append(name).Append(' ').Append(value.ToString("0.###")).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>The tuned weights against the starting ones, on deals the tuner never saw.</summary>
    public static string Validate(int deals, int firstSeed, string tuned)
    {
        var sb = new StringBuilder();
        DuelWeights start = new DuelWeights();
        DuelWeights candidate = Parse(tuned);
        foreach (Policy policy in new[] { Policy.Shallow, Policy.Planner })
        {
            Result a = Run(policy, deals, start, firstSeed);
            Result b = Run(policy, deals, candidate, firstSeed);
            sb.Append(policy).Append(": start ").Append(a.Mean.ToString("0.0"))
                .Append("  tuned ").Append(b.Mean.ToString("0.0"))
                .Append("  (").Append(b.MeanTurnMs.ToString("0.0")).Append(" ms)\n");
        }
        return sb.ToString();
    }

    /// <summary>"Name=value;Name=value" onto a fresh set of weights.</summary>
    internal static DuelWeights Parse(string text)
    {
        var weights = new DuelWeights();
        foreach (string pair in text.Split(';'))
        {
            string[] kv = pair.Split('=');
            if (kv.Length != 2)
            {
                continue;
            }
            var field = typeof(DuelWeights).GetField(kv[0].Trim());
            if (field != null && field.FieldType == typeof(double))
            {
                field.SetValue(weights, double.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (field != null && field.FieldType == typeof(int))
            {
                field.SetValue(weights, int.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        return weights;
    }

    public static string Report(int deals)
    {
        var sb = new StringBuilder();
        sb.Append("duel bench: ").Append(deals).Append(" deals of 12 cards, 7x7\n");
        foreach (Policy policy in new[] { Policy.Random, Policy.Greedy, Policy.Shallow, Policy.Planner })
        {
            Result r = Run(policy, deals, null);
            sb.Append(policy.ToString().PadRight(8))
                .Append("  mean ").Append(r.Mean.ToString("0.0").PadLeft(7))
                .Append("  min ").Append(r.Min.ToString("0").PadLeft(5))
                .Append("  max ").Append(r.Max.ToString("0").PadLeft(5));
            if (policy == Policy.Planner || policy == Policy.Shallow)
            {
                sb.Append("  turn ").Append(r.MeanTurnMs.ToString("0.0")).Append(" ms (max ")
                    .Append(r.MaxTurnMs.ToString("0")).Append(" ms, ")
                    .Append(r.MeanSimulations.ToString("0")).Append(" sims)");
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
