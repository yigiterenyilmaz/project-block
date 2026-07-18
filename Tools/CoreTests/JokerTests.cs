// Targeted tests for the joker framework and the wave-1 jokers. Compiled INTO the Core
// assembly, so internal members (TurnReport setters, engine internals) are reachable.

using System;
using System.Collections.Generic;
using System.Text;
using ProjectBlock.Core;

public static class JokerTests
{
    private static int passed;
    private static int failed;
    private static readonly StringBuilder log = new StringBuilder();

    public static int RunAll()
    {
        ScorePipeline_NoJokers_MatchesBase();
        ScorePipeline_FlatThenMultiplier_InInventoryOrder();
        Streak_Cig_PaysFromMinStreakAndGrows();
        Streak_Cig_EqualSizeRestartsRun();
        Streak_Dondurma_Decreasing();
        Streak_Siyam_SameShapeOnly();
        Streak_ResetsEachRound();
        Bereket_PlusExplosionStacksPermanently();
        Insider_FlagFollowsOwnership();
        Renovasyon_SpendsChargesAndResetsPerRound();
        Renovasyon_OvertimeDoesNotRecycleDiscard();
        Iade_SwapsOneCardInPlace();
        Kumbara_AccrueAndSell();
        Water_ExplodesInPlaceBeforeFalling();
        Market_StocksAndSellsJokers();
        Market_RefusesJokerWhenSlotsFull();
        Overtime_GatedJokerIsSkipped();
        HarcamaBonusu_PaysWhenDrawPileEmpties();
        FullRun_WithEveryJoker_IsDeterministic();
        CleanSweep_FiresOnceAndOnlyOnRealSweep();
        Fuzz_RandomJokerSets_HoldInvariants();

        Console.Out.Write(log.ToString());
        Console.Out.WriteLine("---- " + passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------ helpers

    private static void Check(bool condition, string name, string detail = "")
    {
        if (condition)
        {
            passed++;
            log.Append("  ok   ").Append(name).Append('\n');
        }
        else
        {
            failed++;
            log.Append("  FAIL ").Append(name);
            if (detail.Length > 0)
            {
                log.Append("  <- ").Append(detail);
            }
            log.Append('\n');
        }
    }

    private static void Section(string name)
    {
        log.Append(name).Append('\n');
    }

    /// <summary>Shape generator that hands out a scripted, repeating list of cube counts.</summary>
    private sealed class SizedShapeGenerator : IShapeGenerator
    {
        private readonly int[] sizes;
        private int index;

        public SizedShapeGenerator(params int[] sizes)
        {
            this.sizes = sizes;
        }

        public BlockShape NextShape(IRandomSource rng)
        {
            int size = sizes[index++ % sizes.Length];
            var cells = new List<GridPos>();
            for (int i = 0; i < size; i++)
            {
                cells.Add(new GridPos(i, 0)); // horizontal bar, always placeable on an empty row
            }
            return BlockShape.FromCells(cells);
        }
    }

    private sealed class FixedProgression : IRoundProgression
    {
        private readonly int size;
        private readonly int threshold;

        public FixedProgression(int size, int threshold)
        {
            this.size = size;
            this.threshold = threshold;
        }

        public RoundConfig GetRound(int roundNumber)
        {
            return new RoundConfig(roundNumber, size, size, threshold);
        }
    }

    private static GameSession NewSession(int seed, int boardSize, int threshold, int deckSize,
        params int[] shapeSizes)
    {
        var config = new GameConfig();
        config.RngSeed = seed;
        config.Deck = new DeckDefinition("test", deckSize,
            new SizedShapeGenerator(shapeSizes.Length > 0 ? shapeSizes : new[] { 1 }));
        config.Progression = new FixedProgression(boardSize, threshold);
        return new GameSession(config);
    }

    /// <summary>Synthetic mid-turn context for unit-testing a joker's scoring hook.</summary>
    private static TurnContext FakeTurn(BlockShape played, ScoreBreakdown score,
        int explodedRows = 0, int explodedColumns = 0)
    {
        var report = new TurnReport();
        report.Card = new BlockCard(1, played);
        report.Score = score;
        var rows = new List<int>();
        for (int i = 0; i < explodedRows; i++)
        {
            rows.Add(i);
        }
        var cols = new List<int>();
        for (int i = 0; i < explodedColumns; i++)
        {
            cols.Add(i);
        }
        report.ExplodedRows = rows;
        report.ExplodedColumns = cols;
        return new TurnContext(null, new SeededRandom(1), null, report, score);
    }

    private static BlockShape Bar(int size)
    {
        var cells = new List<GridPos>();
        for (int i = 0; i < size; i++)
        {
            cells.Add(new GridPos(i, 0));
        }
        return BlockShape.FromCells(cells);
    }

    /// <summary>Plays greedily until the round leaves InProgress or the cap is hit.</summary>
    private static int PlayTurns(GameSession session, int maxTurns)
    {
        int played = 0;
        while (played < maxTurns
            && session.Phase == GamePhase.Round
            && session.CurrentRound.Status == RoundStatus.InProgress)
        {
            RoundEngine round = session.CurrentRound;
            int handIndex = -1;
            GridPos origin = new GridPos(0, 0);
            for (int i = 0; i < round.Hand.Count && handIndex < 0; i++)
            {
                var origins = round.GetValidOrigins(round.Hand[i].Shape);
                if (origins.Count > 0)
                {
                    handIndex = i;
                    origin = origins[0];
                }
            }
            if (handIndex < 0)
            {
                break;
            }
            round.PlayFromHand(handIndex, origin);
            played++;
        }
        return played;
    }

    // -------------------------------------------------------------------- tests

    private static void ScorePipeline_NoJokers_MatchesBase()
    {
        Section("score pipeline / no jokers");
        var breakdown = new ScoreBreakdown();
        breakdown.BasePlacement = 4;
        breakdown.BaseLines = 16;
        breakdown.BaseSweep = 150;
        Check(breakdown.Total == 170, "empty pipeline equals the base sum",
            "got " + breakdown.Total);
        Check(breakdown.Multiplier == 1.0, "multiplier starts at 1");
    }

    private static void ScorePipeline_FlatThenMultiplier_InInventoryOrder()
    {
        Section("score pipeline / ordering");
        var breakdown = new ScoreBreakdown();
        breakdown.BasePlacement = 10;
        breakdown.AddFlat(5, "a");
        breakdown.AddFlat(5, "b");
        breakdown.AddMultiplier(2.0, "c");
        breakdown.AddMultiplier(1.5, "d");
        // (10 + 10) * 2 * 1.5 = 60 - flats never get multiplied twice, multipliers compose.
        Check(breakdown.Total == 60, "flats add, multipliers compose, floored once",
            "got " + breakdown.Total);
        Check(breakdown.Contributions.Count == 4, "every contribution is logged");

        breakdown.AddLateFlat(7, "late");
        Check(breakdown.Total == 67, "late flat is added after the multiplier stage",
            "got " + breakdown.Total);
    }

    private static void Streak_Cig_PaysFromMinStreakAndGrows()
    {
        Section("cig / increasing streak");
        var joker = new CigJoker();
        joker.MinStreak = 3;
        joker.PointsPerStreakStep = 15;

        var b1 = new ScoreBreakdown();
        joker.ModifyScore(FakeTurn(Bar(1), b1));
        Check(b1.FlatBonus == 0, "first placement pays nothing", "got " + b1.FlatBonus);

        var b2 = new ScoreBreakdown();
        joker.ModifyScore(FakeTurn(Bar(2), b2));
        Check(b2.FlatBonus == 0, "streak 2 is still below MinStreak", "got " + b2.FlatBonus);

        var b3 = new ScoreBreakdown();
        joker.ModifyScore(FakeTurn(Bar(3), b3));
        Check(b3.FlatBonus == 15, "streak 3 pays one step", "got " + b3.FlatBonus);

        var b4 = new ScoreBreakdown();
        joker.ModifyScore(FakeTurn(Bar(4), b4));
        Check(b4.FlatBonus == 30, "streak 4 pays two steps", "got " + b4.FlatBonus);
        Check(joker.Streak == 4, "streak counter tracks the run", "got " + joker.Streak);
    }

    private static void Streak_Cig_EqualSizeRestartsRun()
    {
        Section("cig / equal size restarts");
        var joker = new CigJoker();
        joker.MinStreak = 3;
        joker.ModifyScore(FakeTurn(Bar(2), new ScoreBreakdown()));
        joker.ModifyScore(FakeTurn(Bar(3), new ScoreBreakdown()));
        joker.ModifyScore(FakeTurn(Bar(3), new ScoreBreakdown())); // equal -> restart
        Check(joker.Streak == 1, "equal size restarts the run at 1", "got " + joker.Streak);

        var next = new ScoreBreakdown();
        joker.ModifyScore(FakeTurn(Bar(4), next));
        Check(next.FlatBonus == 0, "run has to be rebuilt from scratch", "got " + next.FlatBonus);
    }

    private static void Streak_Dondurma_Decreasing()
    {
        Section("dondurma / decreasing streak");
        var joker = new DondurmaJoker();
        joker.MinStreak = 3;
        joker.PointsPerStreakStep = 10;
        joker.ModifyScore(FakeTurn(Bar(5), new ScoreBreakdown()));
        joker.ModifyScore(FakeTurn(Bar(4), new ScoreBreakdown()));
        var b = new ScoreBreakdown();
        joker.ModifyScore(FakeTurn(Bar(3), b));
        Check(b.FlatBonus == 10, "three decreasing blocks pay one step", "got " + b.FlatBonus);

        var up = new ScoreBreakdown();
        joker.ModifyScore(FakeTurn(Bar(9), up));
        Check(up.FlatBonus == 0 && joker.Streak == 1, "a bigger block breaks it");
    }

    private static void Streak_Siyam_SameShapeOnly()
    {
        Section("siyam / identical shape");
        var joker = new SiyamJoker();
        joker.MinStreak = 2;
        joker.PointsPerStreakStep = 25;

        joker.ModifyScore(FakeTurn(Bar(3), new ScoreBreakdown()));
        var same = new ScoreBreakdown();
        joker.ModifyScore(FakeTurn(Bar(3), same));
        Check(same.FlatBonus == 25, "second identical shape pays", "got " + same.FlatBonus);

        // Same cube count, different shape: an L of 3 is not a bar of 3.
        BlockShape ell = BlockShape.FromCells(new[]
        {
            new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1)
        });
        var other = new ScoreBreakdown();
        joker.ModifyScore(FakeTurn(ell, other));
        Check(other.FlatBonus == 0, "same size but different shape breaks it",
            "got " + other.FlatBonus);
        Check(Bar(3).CanonicalKey != ell.CanonicalKey, "canonical keys differ for different shapes");
    }

    private static void Streak_ResetsEachRound()
    {
        Section("streak / per-round reset");
        var session = NewSession(7, 6, 40, 24, 1, 2, 3);
        var joker = (CigJoker)session.Jokers.Add(new CigJoker());
        joker.ModifyScore(FakeTurn(Bar(1), new ScoreBreakdown()));
        joker.ModifyScore(FakeTurn(Bar(2), new ScoreBreakdown()));
        Check(joker.Streak == 2, "streak built up", "got " + joker.Streak);

        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        Check(joker.Streak == 0, "round start clears the streak", "got " + joker.Streak);
    }

    private static void Bereket_PlusExplosionStacksPermanently()
    {
        Section("bereket / plus explosion");
        var joker = new BereketJoker();
        joker.PointsPerStack = 5;

        var noPlus = new ScoreBreakdown();
        joker.ModifyScore(FakeTurn(Bar(1), noPlus, explodedRows: 2, explodedColumns: 0));
        Check(joker.Stacks == 0 && noPlus.FlatBonus == 0, "rows alone are not a plus");

        var plus = new ScoreBreakdown();
        joker.ModifyScore(FakeTurn(Bar(1), plus, explodedRows: 1, explodedColumns: 1));
        Check(joker.Stacks == 1, "row + column is a plus", "stacks " + joker.Stacks);
        Check(plus.FlatBonus == 5, "the triggering turn already gets the bonus",
            "got " + plus.FlatBonus);

        var later = new ScoreBreakdown();
        joker.ModifyScore(FakeTurn(Bar(1), later));
        Check(later.FlatBonus == 5, "the bonus is permanent, not one-shot",
            "got " + later.FlatBonus);
    }

    private static void Insider_FlagFollowsOwnership()
    {
        Section("insider / reveal flag");
        var session = NewSession(3, 6, 40, 24, 1);
        Check(!session.Config.Rules.RevealTopDrawCard, "flag starts off");
        Joker insider = session.Jokers.Add(new InsiderJoker());
        Check(session.Config.Rules.RevealTopDrawCard, "acquiring turns it on");
        session.Jokers.Remove(insider);
        Check(!session.Config.Rules.RevealTopDrawCard, "removing turns it off again");
    }

    private static void Renovasyon_SpendsChargesAndResetsPerRound()
    {
        Section("renovasyon / charges");
        var session = NewSession(11, 6, 40, 24, 1, 2);
        var joker = (RenovasyonJoker)session.Jokers.Add(new RenovasyonJoker());
        RoundEngine round = session.CurrentRound;

        Check(joker.ChargesLeft == 2, "starts with 2 charges", "got " + joker.ChargesLeft);
        int turnBefore = round.TurnNumber;
        int handBefore = round.Hand.Count;

        Check(session.Jokers.TryActivate(joker.InstanceId, ActivationTarget.None), "first use works");
        Check(joker.ChargesLeft == 1, "charge spent", "got " + joker.ChargesLeft);
        Check(round.TurnNumber == turnBefore, "redraw consumes no turn");
        Check(round.Hand.Count == handBefore, "hand is refilled to full size");

        Check(session.Jokers.TryActivate(joker.InstanceId, ActivationTarget.None), "second use works");
        Check(!session.Jokers.TryActivate(joker.InstanceId, ActivationTarget.None),
            "third use is refused");
        Check(joker.ChargesLeft == 0, "charges exhausted");

        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        Check(joker.ChargesLeft == 2, "charges reset at round start", "got " + joker.ChargesLeft);
    }

    private static void Renovasyon_OvertimeDoesNotRecycleDiscard()
    {
        Section("renovasyon / disabled in overtime");
        // Threshold 1 so the very first placement puts the round into overtime.
        var session = NewSession(5, 6, 1, 24, 1);
        var joker = (RenovasyonJoker)session.Jokers.Add(new RenovasyonJoker());
        Check(session.Jokers.CanActivate(joker.InstanceId), "usable before the threshold");

        PlayTurns(session, 1);
        RoundEngine round = session.CurrentRound;
        Check(round.ThresholdPassed, "threshold passed on turn 1");
        if (round.Status == RoundStatus.AwaitingAdvanceDecision)
        {
            round.DecideAdvance(false);
        }

        // RoundEngine.RedrawHand always recycles the discard. In overtime nothing else
        // does, so letting the joker run there would defuse the deck-out loss entirely.
        Check(!session.Jokers.CanActivate(joker.InstanceId), "refused once in overtime");
        int shufflesBefore = round.Deck.ShuffleCount;
        Check(!session.Jokers.TryActivate(joker.InstanceId, ActivationTarget.None),
            "activation is rejected");
        Check(round.Deck.ShuffleCount == shufflesBefore,
            "no free discard recycle happened", "shuffles moved");
        Check(joker.ChargesLeft == joker.ChargesPerRound,
            "a refused activation spends no charge", "left " + joker.ChargesLeft);
    }

    private static void Iade_SwapsOneCardInPlace()
    {
        Section("iade / single card swap");
        var session = NewSession(13, 6, 40, 24, 1, 2, 3);
        var joker = (IadeJoker)session.Jokers.Add(new IadeJoker());
        RoundEngine round = session.CurrentRound;

        int handCount = round.Hand.Count;
        BlockCard kept0 = round.Hand[0];
        BlockCard swapped = round.Hand[1];
        BlockCard kept2 = round.Hand[2];
        int discardBefore = round.Deck.DiscardCount;

        Check(session.Jokers.TryActivate(joker.InstanceId, ActivationTarget.Hand(1)), "swap runs");
        Check(round.Hand.Count == handCount, "hand size unchanged");
        Check(ReferenceEquals(round.Hand[0], kept0), "slot 0 untouched");
        Check(ReferenceEquals(round.Hand[2], kept2), "slot 2 untouched");
        Check(!ReferenceEquals(round.Hand[1], swapped), "slot 1 holds a different card");
        Check(round.Deck.DiscardCount == discardBefore + 1,
            "exactly one card went to the discard", "got " + round.Deck.DiscardCount);
        Check(round.TurnNumber == 0, "swap consumes no turn");
        Check(!session.Jokers.TryActivate(joker.InstanceId, ActivationTarget.None),
            "a swap without a target is refused");
    }

    private static void Kumbara_AccrueAndSell()
    {
        Section("kumbara / value accrual and sale");
        var session = NewSession(17, 6, 40, 24, 1);
        var cimri = (CimriKumbaraJoker)session.Jokers.Add(new CimriKumbaraJoker());
        cimri.ValuePerTurn = 3;
        int baseValue = cimri.SellValue;

        int turns = PlayTurns(session, 4);
        Check(turns == 4, "played four turns", "got " + turns);
        Check(cimri.AccruedValue == 12, "banked 3 per turn", "got " + cimri.AccruedValue);
        Check(cimri.SellValue == baseValue + 12, "sell value includes the accrual");

        long before = session.TotalScore;
        int paid = session.Jokers.Sell(cimri);
        Check(paid == baseValue + 12, "sale pays base + accrued", "got " + paid);
        Check(session.TotalScore == before + paid, "currency went up by the sale price");
        Check(session.Jokers.Count == 0, "joker left the inventory");

        var domuz = (DomuzKumbarasiJoker)session.Jokers.Add(new DomuzKumbarasiJoker());
        session.Jokers.DispatchRoundEnded(session.CurrentRound, RoundOutcome.Lost);
        Check(domuz.AccruedValue == 0, "a lost round pays nothing");
        session.Jokers.DispatchRoundEnded(session.CurrentRound, RoundOutcome.Advanced);
        Check(domuz.AccruedValue == domuz.ValuePerRound, "an advanced round pays",
            "got " + domuz.AccruedValue);
    }

    private sealed class OvertimeOnlyProbe : Joker
    {
        public int Calls;

        public OvertimeOnlyProbe()
            : base("probe_gated", "Probe")
        {
        }

        public override bool DisabledInOvertime
        {
            get { return true; }
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            Calls++;
        }
    }

    private static void Overtime_GatedJokerIsSkipped()
    {
        Section("overtime / central gating");
        var session = NewSession(23, 6, 1, 24, 1);
        var probe = (OvertimeOnlyProbe)session.Jokers.Add(new OvertimeOnlyProbe());

        PlayTurns(session, 1);
        Check(probe.Calls == 1, "hook ran before the threshold", "got " + probe.Calls);
        Check(session.CurrentRound.ThresholdPassed, "now in overtime");
        if (session.CurrentRound.Status == RoundStatus.AwaitingAdvanceDecision)
        {
            session.CurrentRound.DecideAdvance(false);
        }
        PlayTurns(session, 3);
        Check(probe.Calls == 1, "hook is skipped entirely in overtime", "got " + probe.Calls);
    }

    private static void HarcamaBonusu_PaysWhenDrawPileEmpties()
    {
        Section("harcama bonusu / empty draw pile");
        // Deck of 6, hand of 3: the pile runs dry within a few turns.
        var session = NewSession(29, 8, 100000, 6, 1);
        var joker = (HarcamaBonusuJoker)session.Jokers.Add(new HarcamaBonusuJoker());
        joker.PointsPerEmptyDrawPile = 60;

        long scoreBefore = session.CurrentRound.RoundScore;
        PlayTurns(session, 8);
        Check(joker.TriggeredThisRound > 0, "the draw pile ran dry at least once",
            "triggered " + joker.TriggeredThisRound);
        int expectedBonus = joker.TriggeredThisRound * 60;
        Check(session.CurrentRound.RoundScore >= scoreBefore + expectedBonus,
            "round score contains the bonus", "round score " + session.CurrentRound.RoundScore);
    }

    private static void CleanSweep_FiresOnceAndOnlyOnRealSweep()
    {
        Section("clean sweep / central event");
        // 3x3 board, 3-cube bars: three placements fill the board and clear every row.
        var session = NewSession(31, 3, 100000, 30, 3);
        int sweeps = 0;
        session.CurrentRound.TurnResolved += r =>
        {
            if (r.CleanSweep)
            {
                sweeps++;
            }
        };
        int turns = PlayTurns(session, 12);
        Check(turns > 0, "played some turns", "got " + turns);
        Check(sweeps > 0, "a real sweep fired", "got " + sweeps);
        Check(session.CurrentRound.CleanSweepCount == sweeps,
            "engine counter agrees with the reports",
            session.CurrentRound.CleanSweepCount + " vs " + sweeps);
    }

    private static void FullRun_WithEveryJoker_IsDeterministic()
    {
        Section("determinism / full run with all jokers");
        string first = RunWithAllJokers(101);
        string second = RunWithAllJokers(101);
        string other = RunWithAllJokers(102);
        Check(first == second, "same seed produces the identical run");
        Check(first != other, "a different seed produces a different run");
        Check(first.Length > 0, "the run produced output");
    }

    /// <summary>Plays many runs with random joker sets, random advance/continue decisions and
    /// random joker activations, checking the invariants that must hold no matter what.</summary>
    private static void Fuzz_RandomJokerSets_HoldInvariants()
    {
        Section("fuzz / random joker sets");
        int runs = 0;
        int turns = 0;
        int activations = 0;
        int sweeps = 0;
        int overtimeRounds = 0;
        string failure = null;

        for (int seed = 1; seed <= 60 && failure == null; seed++)
        {
            var picker = new SeededRandom(seed * 7919);
            var config = new GameConfig();
            config.RngSeed = seed;
            if (seed % 2 == 0)
            {
                // Half the runs use a cramped board so clean sweeps actually occur - the
                // default 6x6 with the Classic deck almost never empties under greedy play.
                config.Deck = new DeckDefinition("fuzz", 20, new SizedShapeGenerator(1, 2, 3));
                config.Progression = new FixedProgression(3, 120);
            }
            var session = new GameSession(config);

            foreach (JokerDefinition definition in JokerRegistry.All)
            {
                if (picker.NextInt(0, 2) == 0)
                {
                    session.Jokers.Add(definition.Create());
                }
            }

            long expectedTotal = 0;
            long saleIncome = 0;
            int lastRoundScore = 0;
            bool localFailure = false;

            Action<TurnReport> auditor = report =>
            {
                if (localFailure)
                {
                    return;
                }
                if (report.Score != null && report.ScoreGained != report.Score.Total)
                {
                    failure = "seed " + seed + ": ScoreGained " + report.ScoreGained
                        + " != breakdown total " + report.Score.Total;
                    localFailure = true;
                    return;
                }
                if (report.ScoreGained < 0)
                {
                    failure = "seed " + seed + ": negative turn score " + report.ScoreGained;
                    localFailure = true;
                    return;
                }
                if (report.RoundScoreAfter < lastRoundScore)
                {
                    failure = "seed " + seed + ": round score went backwards";
                    localFailure = true;
                    return;
                }
                lastRoundScore = report.RoundScoreAfter;
                expectedTotal += report.ScoreGained;
                if (report.CleanSweep)
                {
                    sweeps++;
                }
                turns++;
            };

            RoundEngine subscribed = session.CurrentRound;
            subscribed.TurnResolved += auditor;

            int safety = 0;
            while (session.Phase != GamePhase.GameOver && safety++ < 500 && failure == null)
            {
                if (!ReferenceEquals(subscribed, session.CurrentRound))
                {
                    subscribed = session.CurrentRound;
                    subscribed.TurnResolved += auditor;
                    lastRoundScore = 0;
                }

                if (session.Phase == GamePhase.Market)
                {
                    if (session.RoundNumber >= 5)
                    {
                        break;
                    }
                    session.LeaveMarket();
                    continue;
                }

                RoundEngine round = session.CurrentRound;
                if (round.Status == RoundStatus.AwaitingAdvanceDecision)
                {
                    bool advance = picker.NextInt(0, 3) > 0; // sometimes gamble on overtime
                    if (!advance)
                    {
                        overtimeRounds++;
                    }
                    round.DecideAdvance(advance);
                    continue;
                }
                if (round.Status != RoundStatus.InProgress)
                {
                    break;
                }

                // Randomly poke an activated joker before placing.
                if (session.Jokers.Count > 0 && picker.NextInt(0, 4) == 0)
                {
                    Joker joker = session.Jokers.Jokers[picker.NextInt(0, session.Jokers.Count)];
                    if (session.Jokers.CanActivate(joker.InstanceId))
                    {
                        ActivationTarget target = joker.Targeting == JokerTargeting.HandCard
                            ? ActivationTarget.Hand(picker.NextInt(0, Math.Max(1, round.Hand.Count)))
                            : ActivationTarget.None;
                        if (session.Jokers.TryActivate(joker.InstanceId, target))
                        {
                            activations++;
                        }
                    }
                    if (round.Status != RoundStatus.InProgress)
                    {
                        continue;
                    }
                }

                // Occasionally sell one, exercising OnRemoved and the currency path.
                if (session.Jokers.Count > 0 && picker.NextInt(0, 25) == 0)
                {
                    Joker victim = session.Jokers.Jokers[picker.NextInt(0, session.Jokers.Count)];
                    saleIncome += session.Jokers.Sell(victim);
                }

                if (PlayTurns(session, 1) == 0)
                {
                    break;
                }
            }

            if (failure == null && session.TotalScore != expectedTotal + saleIncome)
            {
                failure = "seed " + seed + ": TotalScore " + session.TotalScore
                    + " != turns " + expectedTotal + " + sales " + saleIncome;
            }
            runs++;
        }

        Check(failure == null, "invariants held across " + runs + " fuzzed runs",
            failure ?? string.Empty);
        Check(turns > 500, "the fuzz actually played a lot of turns", "turns " + turns);
        Check(activations > 0, "activated jokers were exercised", "activations " + activations);
        Check(sweeps > 0, "clean sweeps happened", "sweeps " + sweeps);
        Check(overtimeRounds > 0, "overtime was entered", "overtime " + overtimeRounds);
    }

    private static void Water_ExplodesInPlaceBeforeFalling()
    {
        Section("water / explosion before fall");
        var normalLeft = new BlockCard(1, Bar(1));
        var normalRight = new BlockCard(2, Bar(1));
        var water = new BlockCard(3, Bar(1), new[] { BlockElement.Water });

        // Row y=1 is one cube short at column 1, and the cell below it (1,0) is empty.
        // Dropping the water there would leave the row incomplete; exploding first clears it.
        GameBoard Build()
        {
            var b = new GameBoard(3, 3);
            b.Place(normalLeft, new GridPos(0, 1));
            b.Place(normalRight, new GridPos(2, 1));
            b.Place(water, new GridPos(1, 1));
            return b;
        }

        GameBoard explodeFirst = Build();
        LineExplosionResult inPlace = explodeFirst.ResolveFullLines();
        Check(inPlace.LineCount == 1 && inPlace.ExplodedCells.Count == 3,
            "water completing a line explodes in place", "lines " + inPlace.LineCount);

        GameBoard settleFirst = Build();
        settleFirst.SettleWaterAndReact(null);
        LineExplosionResult afterFall = settleFirst.ResolveFullLines();
        Check(afterFall.LineCount == 0,
            "settling first would drop the water and miss the line", "lines " + afterFall.LineCount);
    }

    private static void Market_StocksAndSellsJokers()
    {
        Section("market / joker offers");
        GameSession session = DriveToMarket(101);
        Check(session.Phase == GamePhase.Market, "reached the market", "phase " + session.Phase);
        if (session.Phase != GamePhase.Market)
        {
            return;
        }

        int index = FirstJokerOffer(session);
        Check(index >= 0, "the market stocks at least one joker offer");
        if (index < 0)
        {
            return;
        }

        MarketOffer offer = session.Market.Offers[index];
        Check(offer.Joker != null && offer.Card == null, "a joker offer carries a joker, not a card");

        // Same seed -> same joker line-up (the joker rng derives from the run seed).
        string here = JokerOffersKey(session);
        string twin = JokerOffersKey(DriveToMarket(101));
        Check(here.Length > 0 && here == twin, "joker offers are deterministic for a seed",
            here + " vs " + twin);

        int before = session.Jokers.Count;
        long money = session.TotalScore;
        session.AddCurrency(offer.Price); // guarantee affordability regardless of round score
        bool bought = session.TryBuyOffer(index);
        Check(bought, "the joker offer can be bought");
        Check(session.Jokers.Count == before + 1, "buying a joker adds it to the inventory",
            "count " + session.Jokers.Count);
        Check(offer.Sold, "the bought offer is marked sold");
        Check(session.TotalScore == money, "exactly the price was paid", "total " + session.TotalScore);
        Check(!session.TryBuyOffer(index), "a sold offer cannot be bought again");
    }

    private static void Market_RefusesJokerWhenSlotsFull()
    {
        Section("market / joker slot cap");
        GameSession session = DriveToMarket(202);
        if (session.Phase != GamePhase.Market)
        {
            Check(false, "reached the market", "phase " + session.Phase);
            return;
        }
        int index = FirstJokerOffer(session);
        if (index < 0)
        {
            Check(false, "the market stocks a joker offer");
            return;
        }

        session.Jokers.MaxSlots = session.Jokers.Count; // leave no free slot
        session.AddCurrency(session.Market.Offers[index].Price);
        Check(!session.TryBuyOffer(index), "a full inventory refuses a joker purchase");
        Check(!session.Market.Offers[index].Sold, "the refused offer stays available");
    }

    /// <summary>Plays greedily, always taking the advance offer, until the first market.</summary>
    private static GameSession DriveToMarket(int seed)
    {
        var config = new GameConfig();
        config.RngSeed = seed;
        var session = new GameSession(config);
        int safety = 0;
        while (session.Phase != GamePhase.GameOver && safety++ < 400)
        {
            if (session.Phase == GamePhase.Market)
            {
                break;
            }
            RoundEngine round = session.CurrentRound;
            if (round.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                round.DecideAdvance(true);
                continue;
            }
            if (round.Status != RoundStatus.InProgress || PlayTurns(session, 1) == 0)
            {
                break;
            }
        }
        return session;
    }

    private static int FirstJokerOffer(GameSession session)
    {
        IReadOnlyList<MarketOffer> offers = session.Market.Offers;
        for (int i = 0; i < offers.Count; i++)
        {
            if (offers[i].Kind == MarketOfferKind.Joker)
            {
                return i;
            }
        }
        return -1;
    }

    private static string JokerOffersKey(GameSession session)
    {
        var sb = new StringBuilder();
        foreach (MarketOffer offer in session.Market.Offers)
        {
            if (offer.Kind == MarketOfferKind.Joker)
            {
                sb.Append(offer.Joker.DefId).Append(';');
            }
        }
        return sb.ToString();
    }

    private static string RunWithAllJokers(int seed)
    {
        var config = new GameConfig();
        config.RngSeed = seed;
        var session = new GameSession(config);
        foreach (JokerDefinition definition in JokerRegistry.All)
        {
            session.Jokers.Add(definition.Create());
        }

        var sb = new StringBuilder();
        int safety = 0;
        while (session.Phase != GamePhase.GameOver && safety++ < 400)
        {
            if (session.Phase == GamePhase.Market)
            {
                if (session.RoundNumber >= 4)
                {
                    break;
                }
                session.LeaveMarket();
                continue;
            }
            RoundEngine round = session.CurrentRound;
            if (round.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                round.DecideAdvance(true);
                continue;
            }
            if (round.Status != RoundStatus.InProgress)
            {
                break;
            }
            if (PlayTurns(session, 1) == 0)
            {
                break;
            }
            sb.Append(round.TurnNumber).Append(':').Append(round.RoundScore).Append(';');
        }
        foreach (Joker joker in session.Jokers.Jokers)
        {
            sb.Append(joker.DefId).Append('=').Append(joker.SellValue).Append(';');
        }
        sb.Append("total=").Append(session.TotalScore);
        return sb.ToString();
    }
}
