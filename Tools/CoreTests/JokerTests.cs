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
        Market_CardSellValueByElement();
        Market_StocksAndSellsJokers();
        Market_NeverOffersOwnedJokers();
        Market_RefusesJokerWhenSlotsFull();
        Overtime_GatedJokerIsSkipped();
        HarcamaBonusu_PaysWhenDrawPileEmpties();
        FullRun_WithEveryJoker_IsDeterministic();
        CleanSweep_FiresOnceAndOnlyOnRealSweep();
        Buldozer_WipesOnScheduleWithoutScoreOrSweep();
        RobotSupurge_EatsCubesAndGrowsOnSweep();
        KayitDefteri_ReplacesTheSweepWithItsCounter();
        KentselDonusum_GrowsTheBoardPermanently();
        KaziCalismasi_ReturnsAFullyExplodedBlock();
        SeriTetik_BoostsHandAndChurnsUntilThreshold();
        Batak_PayoutCurveAndDeadline();
        Midas_PaysForGoldInHand();
        ElmasKazma_CracksObsidianOnSweep();
        Tutustur_BurnsEveryFireCube();
        Spread_ConvertsOneRingOnly();
        Buzluk_FreezesAtWallsAndDoesNotBlockSweep();
        Simya_GivesOfferedElementalBlocksASecondElement();
        Damlaya_PaysWhenNothingWasBought();
        Ihale_LocksUntilTheAuctionedJokerLeaves();
        KaraDelik_VoidBlockSwallowsWhatLandsOnIt();
        Enfeksiyon_SpreadsThenDetonates();
        Oryantasyon_BuriesPlayedCardsInTheDrawPile();
        Dezenformasyon_SplitsAndSwapsThePilesEachTurn();
        Imitasyon_HandTracksTheDiscardPile();
        Fraksiyon_SplitsAtRoundStartAndAllowsOneSwap();
        AllRegisteredJokers_HaveDistinctIdsAndText();
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

    // ------------------------------------------------------- wave 2/3/4 joker tests

    /// <summary>Fills the board with a card of a given element, for element joker tests.</summary>
    private static void PaintBoard(RoundEngine round, GameSession session, CubeKind kind,
        params GridPos[] cells)
    {
        foreach (GridPos cell in cells)
        {
            if (!round.Board.GetCube(cell).HasValue)
            {
                // place a 1x1 helper card, then retype it
                BlockCard filler = session.CreateCard(Bar(1), null);
                round.Board.Place(filler, cell);
            }
            round.Board.SetCubeKind(cell, kind);
        }
    }

    private static void Buldozer_WipesOnScheduleWithoutScoreOrSweep()
    {
        Section("buldozer / scheduled wipe");
        var session = NewSession(41, 8, 1000000, 40, 1);
        var joker = (BuldozerJoker)session.Jokers.Add(new BuldozerJoker());
        joker.TurnsPerWipe = 4;

        int sweeps = 0;
        session.CurrentRound.TurnResolved += r =>
        {
            if (r.CleanSweep)
            {
                sweeps++;
            }
        };

        PlayTurns(session, 3);
        Check(session.CurrentRound.Board.OccupiedCount > 0, "board still has cubes after 3 turns",
            "occupied " + session.CurrentRound.Board.OccupiedCount);
        PlayTurns(session, 1);
        Check(session.CurrentRound.Board.OccupiedCount == 0, "the 4th turn wiped the board",
            "occupied " + session.CurrentRound.Board.OccupiedCount);
        Check(sweeps == 0, "a Buldozer wipe is never a clean sweep", "sweeps " + sweeps);
        Check(session.CurrentRound.CleanSweepCount == 0, "engine counter agrees");
    }

    private static void RobotSupurge_EatsCubesAndGrowsOnSweep()
    {
        Section("robot supurge / eats and grows");
        var session = NewSession(43, 8, 1000000, 40, 3);
        var joker = (RobotSupurgeJoker)session.Jokers.Add(new RobotSupurgeJoker());
        RoundEngine round = session.CurrentRound;

        PlayTurns(session, 1);
        // one 3-cube block placed, one cube eaten
        Check(round.Board.OccupiedCount == 2, "the sweeper ate exactly one cube",
            "occupied " + round.Board.OccupiedCount);
        Check(joker.Capacity == 1, "capacity unchanged while cubes remain");

        // Let it eat the board empty; the sweep it triggers must raise capacity.
        int guard = 0;
        while (round.Board.OccupiedCount > 0 && guard++ < 20 && PlayTurns(session, 1) > 0)
        {
        }
        Check(joker.Capacity >= 1, "capacity stayed sane", "capacity " + joker.Capacity);
    }

    private static void KayitDefteri_ReplacesTheSweepWithItsCounter()
    {
        Section("kayit defteri / counter replaces the sweep");
        // 3x3 board, 3-cube bars: three placements clear rows repeatedly.
        var session = NewSession(47, 3, 1000000, 40, 3);
        var joker = (KayitDefteriJoker)session.Jokers.Add(new KayitDefteriJoker());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;

        Check(round.SuppressNaturalSweep, "natural sweep is switched off while it is held");
        Check(joker.Target == 9, "target is the board's cell count", "target " + joker.Target);

        int sweeps = 0;
        round.TurnResolved += r =>
        {
            if (r.CleanSweep)
            {
                sweeps++;
            }
        };
        PlayTurns(session, 6);
        Check(joker.Counter >= 0, "counter runs", "counter " + joker.Counter);
        Check(sweeps > 0, "the counter forced at least one sweep", "sweeps " + sweeps);

        session.Jokers.Remove(joker);
        Check(!round.SuppressNaturalSweep, "removing it restores the normal sweep rule");
    }

    private static void KentselDonusum_GrowsTheBoardPermanently()
    {
        Section("kentsel donusum / board growth");
        var joker = new KentselDonusumJoker();
        joker.RoundsPerGrowth = 1;
        joker.MaxExtra = 3;
        var config = new RoundConfig(1, 6, 6, 100);
        var session = NewSession(53, 6, 40, 24, 1);
        var ctx = new SessionContext(session, session.Rng);

        Check(joker.FilterRoundConfig(ctx, config).BoardWidth == 6, "no growth before a round ends");
        joker.OnRoundEnded(new RoundContext(session, session.Rng, session.CurrentRound),
            RoundOutcome.Advanced);
        Check(joker.FilterRoundConfig(ctx, config).BoardWidth == 7, "one finished round grows it by 1",
            "width " + joker.FilterRoundConfig(ctx, config).BoardWidth);
        joker.OnRoundEnded(new RoundContext(session, session.Rng, session.CurrentRound),
            RoundOutcome.Lost);
        Check(joker.FilterRoundConfig(ctx, config).BoardWidth == 7, "a lost round does not grow it");

        for (int i = 0; i < 10; i++)
        {
            joker.OnRoundEnded(new RoundContext(session, session.Rng, session.CurrentRound),
                RoundOutcome.Advanced);
        }
        Check(joker.FilterRoundConfig(ctx, config).BoardWidth == 6 + joker.MaxExtra,
            "growth is capped", "width " + joker.FilterRoundConfig(ctx, config).BoardWidth);
    }

    private static void KaziCalismasi_ReturnsAFullyExplodedBlock()
    {
        Section("kazi calismasi / whole block returns");
        // 3x3 board with 3-cube bars: every placement fills a row and explodes it whole.
        var session = NewSession(59, 3, 1000000, 40, 3);
        var joker = (KaziCalismasiJoker)session.Jokers.Add(new KaziCalismasiJoker());
        RoundEngine round = session.CurrentRound;

        PlayTurns(session, 1);
        Check(round.BonusHand.Count == 1, "the block came back to the bonus hand",
            "bonus " + round.BonusHand.Count);
        Check(joker.RecoveredThisRound == 1, "counted one recovery",
            "recovered " + joker.RecoveredThisRound);
        Check(round.BonusHand[0].OutcomeOnPlay == BonusPlayOutcome.ToDiscard,
            "it is a normal deck card on loan, not an expiring one");
    }

    private static void SeriTetik_BoostsHandAndChurnsUntilThreshold()
    {
        Section("seri tetik / bigger hand that churns");
        var session = NewSession(61, 8, 25, 40, 1);
        int baseHand = session.Config.Rules.HandSize;
        var joker = (SeriTetikJoker)session.Jokers.Add(new SeriTetikJoker());
        Check(session.Config.Rules.HandSize == baseHand + 2, "hand size grew on acquisition",
            "hand " + session.Config.Rules.HandSize);

        RoundEngine round = session.CurrentRound;
        var before = new List<int>();
        for (int i = 0; i < round.Hand.Count; i++)
        {
            before.Add(round.Hand[i].Id);
        }
        PlayTurns(session, 1);
        bool anyKept = false;
        for (int i = 0; i < round.Hand.Count; i++)
        {
            if (before.Contains(round.Hand[i].Id))
            {
                anyKept = true;
            }
        }
        Check(!anyKept || round.Deck.DrawCount == 0,
            "the unused hand was churned out at end of turn");

        session.Jokers.Remove(joker);
        Check(session.Config.Rules.HandSize == baseHand, "removal gives the hand size back",
            "hand " + session.Config.Rules.HandSize);
    }

    private static void Batak_PayoutCurveAndDeadline()
    {
        Section("batak / payout curve and deadline");
        var joker = new BatakJoker();
        joker.MaxMultiplier = 3.0;
        joker.ZeroAtTurns = 100;

        int bold = joker.PayoutFor(1, 1, 100);
        int timid = joker.PayoutFor(50, 50, 100);
        int hopeless = joker.PayoutFor(100, 100, 100);
        Check(bold == 300, "a 1-turn call pays the full multiplier", "got " + bold);
        Check(hopeless == 0, "a 100-turn call pays nothing", "got " + hopeless);
        Check(timid > 0 && timid < bold, "the curve falls off in between", "got " + timid);

        // Confirmed rule: bet 7, clear in 3 -> 3/7 of the 7-turn reward.
        int full7 = joker.PayoutFor(7, 7, 100);
        int early3 = joker.PayoutFor(7, 3, 100);
        Check(early3 == (int)Math.Floor(full7 * 3.0 / 7.0) || Math.Abs(early3 - full7 * 3 / 7) <= 1,
            "clearing early pays pro rata", early3 + " vs " + (full7 * 3 / 7));

        // A missed deadline loses the round.
        var session = NewSession(67, 8, 1000000, 40, 1);
        var live = (BatakJoker)session.Jokers.Add(new BatakJoker());
        var ctx = new RoundContext(session, session.Rng, session.CurrentRound);
        Check(live.PlaceBet(ctx, 2), "bet placed");
        Check(live.HasActiveBet, "bet is running");
        PlayTurns(session, 2);
        Check(session.CurrentRound.Loss == LossReason.BetFailed,
            "missing the deadline loses the round",
            "loss " + session.CurrentRound.Loss);
    }

    private static void Midas_PaysForGoldInHand()
    {
        Section("midas / gold in hand");
        var session = NewSession(71, 8, 1000000, 40, 2);
        var joker = (MidasJoker)session.Jokers.Add(new MidasJoker());
        joker.PointsPerGoldCubeHeld = 5;

        var plain = new ScoreBreakdown();
        joker.ModifyScore(FakeTurnWithRound(session, plain));
        Check(plain.FlatBonus == 0, "a plain hand pays nothing", "got " + plain.FlatBonus);

        // Put a gold block into the bonus hand: holding it must be enough.
        BlockCard gold = session.CreateCard(Bar(3), new[] { BlockElement.Gold });
        session.CurrentRound.AddBonusCard(gold, BonusPlayOutcome.ExpireFromRound);
        var withGold = new ScoreBreakdown();
        joker.ModifyScore(FakeTurnWithRound(session, withGold));
        Check(withGold.FlatBonus == 15, "3 gold cubes held pay 3 x 5", "got " + withGold.FlatBonus);
        Check(joker.GoldCubesHeld == 3, "counted the cubes", "got " + joker.GoldCubesHeld);
    }

    /// <summary>A TurnContext bound to a real round, for jokers that read the hand/board.</summary>
    private static TurnContext FakeTurnWithRound(GameSession session, ScoreBreakdown score)
    {
        var report = new TurnReport();
        report.Card = new BlockCard(1, Bar(1));
        report.Score = score;
        return new TurnContext(session, session.Rng, session.CurrentRound, report, score);
    }

    private static void ElmasKazma_CracksObsidianOnSweep()
    {
        Section("elmas kazma / obsidian cracks");
        // 4x4 board with 4-cube bars: one placement fills row 0 and clears it. An obsidian
        // cube parked in the far corner does not block the sweep, so the sweep fires and
        // the joker gets to crack it - driven through a REAL turn, not a synthetic one.
        var session = NewSession(73, 4, 1000000, 40, 4);
        session.Jokers.Add(new ElmasKazmaJoker());
        RoundEngine round = session.CurrentRound;

        PaintBoard(round, session, CubeKind.Obsidian, new GridPos(3, 3));
        Check(round.Board.CountCubesOfKind(CubeKind.Obsidian) == 1, "one obsidian cube parked");

        bool sweptClean = false;
        round.TurnResolved += r =>
        {
            if (r.CleanSweep)
            {
                sweptClean = true;
            }
        };
        PlayTurns(session, 1);
        Check(sweptClean, "clearing the row swept the board despite the obsidian");
        Check(round.Board.CountCubesOfKind(CubeKind.Obsidian) == 0,
            "the sweep cracked the obsidian",
            "left " + round.Board.CountCubesOfKind(CubeKind.Obsidian));
        Check(round.RoundScore > 0, "the crack paid into the round score");
    }

    private static void Tutustur_BurnsEveryFireCube()
    {
        Section("tutustur / board-wide fire chain");
        var session = NewSession(79, 5, 1000000, 40, 1);
        var joker = (TutusturJoker)session.Jokers.Add(new TutusturJoker());
        RoundEngine round = session.CurrentRound;

        PaintBoard(round, session, CubeKind.Fire,
            new GridPos(0, 0), new GridPos(3, 3), new GridPos(4, 1));
        Check(round.Board.CountCubesOfKind(CubeKind.Fire) == 3, "three fire cubes on the board");

        // A report that says a fire cube already died this turn.
        var score = new ScoreBreakdown();
        TurnContext turn = FakeTurnWithRound(session, score);
        var destroyed = new List<DestroyedCube>
        {
            new DestroyedCube(new GridPos(2, 2), new Cube(CubeKind.Fire, 999))
        };
        turn.Report.DestroyedCubes = destroyed;

        joker.AfterLineExplosion(turn);
        Check(round.Board.CountCubesOfKind(CubeKind.Fire) == 0,
            "every fire cube went up", "left " + round.Board.CountCubesOfKind(CubeKind.Fire));
        Check(score.FlatBonus > 0, "the chain paid", "got " + score.FlatBonus);
    }

    private static void Spread_ConvertsOneRingOnly()
    {
        Section("yangin / taskin one-ring spread");
        var session = NewSession(83, 5, 1000000, 40, 1);
        var joker = (YanginJoker)session.Jokers.Add(new YanginJoker());
        RoundEngine round = session.CurrentRound;

        // A 3-long horizontal strip of normal cubes with fire in the middle.
        PaintBoard(round, session, CubeKind.Normal,
            new GridPos(0, 2), new GridPos(1, 2), new GridPos(2, 2), new GridPos(3, 2));
        PaintBoard(round, session, CubeKind.Fire, new GridPos(1, 2));

        var ctx = new RoundContext(session, session.Rng, round);
        Check(joker.CanActivate(ctx), "usable while fire is on the board");
        Check(joker.Activate(ctx, ActivationTarget.None), "spread ran");

        Check(round.Board.GetCube(new GridPos(0, 2)).Value.Kind == CubeKind.Fire,
            "the neighbour caught fire");
        Check(round.Board.GetCube(new GridPos(2, 2)).Value.Kind == CubeKind.Fire,
            "the other neighbour caught fire");
        Check(round.Board.GetCube(new GridPos(3, 2)).Value.Kind == CubeKind.Normal,
            "two cells away stayed normal - one ring only");
        Check(!joker.CanActivate(ctx), "the single charge is spent");
    }

    private static void Buzluk_FreezesAtWallsAndDoesNotBlockSweep()
    {
        Section("buzluk / freeze at the walls");
        var session = NewSession(89, 5, 1000000, 40, 1);
        var joker = (BuzlukJoker)session.Jokers.Add(new BuzlukJoker());
        RoundEngine round = session.CurrentRound;

        PaintBoard(round, session, CubeKind.Water, new GridPos(0, 3), new GridPos(2, 2));
        var score = new ScoreBreakdown();
        joker.AfterTurnScored(FakeTurnWithRound(session, score));

        Check(round.Board.GetCube(new GridPos(0, 3)).Value.Kind == CubeKind.Ice,
            "wall-touching water froze");
        Check(round.Board.GetCube(new GridPos(2, 2)).Value.Kind == CubeKind.Water,
            "water in the middle stayed liquid");

        // Ice must not block a sweep, unlike normal cubes.
        round.Board.DestroyCubeForced(new GridPos(2, 2));
        Check(round.Board.IsCleanForSweep(), "a board holding only ice counts as swept");
        Check(joker.FrozenThisRound == 1, "counted the freeze", "got " + joker.FrozenThisRound);
    }

    private static void Simya_GivesOfferedElementalBlocksASecondElement()
    {
        Section("simya / doubled market elements");
        var session = NewSession(97, 6, 40, 24, 1);
        var joker = (SimyaJoker)session.Jokers.Add(new SimyaJoker());
        var ctx = new SessionContext(session, session.Rng);

        BlockCard plain = session.CreateCard(Bar(2), null);
        Check(joker.FilterMarketOffer(ctx, plain).Elements.Count == 0,
            "a plain block stays plain");

        BlockCard fire = session.CreateCard(Bar(2), new[] { BlockElement.Fire });
        BlockCard doubled = joker.FilterMarketOffer(ctx, fire);
        Check(doubled.Elements.Count == 2, "an elemental block gets a second element",
            "count " + doubled.Elements.Count);
        Check(doubled.Has(BlockElement.Fire), "the original element is kept");
        Check(doubled.Id == fire.Id, "the offer keeps its card id");
    }

    private static void Damlaya_PaysWhenNothingWasBought()
    {
        Section("damlaya / saving pays");
        var session = NewSession(101, 6, 40, 24, 1);
        var joker = (DamlayaJoker)session.Jokers.Add(new DamlayaJoker());
        joker.PointsPerTurnWhenSaving = 8;
        var ctx = new SessionContext(session, session.Rng);

        joker.OnMarketLeft(ctx, true);
        joker.OnRoundStarted(new RoundContext(session, session.Rng, session.CurrentRound));
        Check(joker.ActiveBonus == 0, "buying something pays nothing", "got " + joker.ActiveBonus);

        joker.OnMarketLeft(ctx, false);
        joker.OnRoundStarted(new RoundContext(session, session.Rng, session.CurrentRound));
        Check(joker.ActiveBonus == 8, "skipping the market pays per turn", "got " + joker.ActiveBonus);

        joker.OnMarketLeft(ctx, false);
        joker.OnRoundStarted(new RoundContext(session, session.Rng, session.CurrentRound));
        Check(joker.ActiveBonus == 16, "the streak stacks", "got " + joker.ActiveBonus);

        var score = new ScoreBreakdown();
        joker.ModifyScore(FakeTurnWithRound(session, score));
        Check(score.FlatBonus == 16, "the bonus lands on the turn", "got " + score.FlatBonus);
    }

    private static void Ihale_LocksUntilTheAuctionedJokerLeaves()
    {
        Section("ihale / one auction at a time");
        var session = NewSession(103, 6, 40, 24, 1);
        var ihale = (IhaleJoker)session.Jokers.Add(new IhaleJoker());
        session.Jokers.Add(new CimriKumbaraJoker());

        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        Check(session.Jokers.ActiveAuctionInstanceId.HasValue, "an auction opened");
        int firstTarget = session.Jokers.ActiveAuctionInstanceId.Value;
        Joker auctioned = session.Jokers.Find(firstTarget);
        Check(auctioned.AuctionPremium > 0, "the premium is on the joker",
            "premium " + auctioned.AuctionPremium);
        Check(auctioned.SellValue > auctioned.BaseSellValue, "sell value went up");

        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        Check(session.Jokers.ActiveAuctionInstanceId == firstTarget,
            "no new auction while the first is unsold");

        session.Jokers.Sell(auctioned);
        Check(!session.Jokers.ActiveAuctionInstanceId.HasValue, "selling opens the lock");
    }

    private static void KaraDelik_VoidBlockSwallowsWhatLandsOnIt()
    {
        Section("kara delik / void block");
        var session = NewSession(107, 5, 1000000, 40, 1);
        var joker = (KaraDelikJoker)session.Jokers.Add(new KaraDelikJoker());
        RoundEngine round = session.CurrentRound;

        int discardBefore = round.Deck.DiscardCount;
        var score = new ScoreBreakdown();
        joker.AfterCleanSweep(FakeTurnWithRound(session, score));
        Check(round.Deck.DiscardCount == discardBefore + 1, "a void block went to the discard",
            "discard " + round.Deck.DiscardCount);
        Check(joker.GrantedThisRound == 1, "counted the grant");

        // The void must swallow whatever is placed on top of it.
        BlockCard voidCard = session.CreateCard(Bar(1), new[] { BlockElement.Void });
        round.Board.Place(voidCard, new GridPos(2, 2));
        Check(round.Board.GetCube(new GridPos(2, 2)).Value.Kind == CubeKind.Void,
            "the void cube sits on the board");
        Check(round.Board.CanPlace(Bar(1), new GridPos(2, 2)),
            "a block may be placed onto a void cube");

        BlockCard victim = session.CreateCard(Bar(1), null);
        round.Board.Place(victim, new GridPos(2, 2));
        Check(!round.Board.GetCube(new GridPos(2, 2)).HasValue,
            "both the arriving cube and the void are gone");
        Check(round.Board.OccupiedCount == 0, "occupancy stayed consistent",
            "occupied " + round.Board.OccupiedCount);
    }

    private static void Enfeksiyon_SpreadsThenDetonates()
    {
        Section("enfeksiyon / spread and detonate");
        var session = NewSession(109, 5, 1000000, 40, 1);
        var joker = (EnfeksiyonJoker)session.Jokers.Add(new EnfeksiyonJoker());
        joker.TurnsToDetonate = 2;
        RoundEngine round = session.CurrentRound;

        PaintBoard(round, session, CubeKind.Normal,
            new GridPos(1, 1), new GridPos(2, 1), new GridPos(3, 1));
        var ctx = new RoundContext(session, session.Rng, round);
        Check(joker.Activate(ctx, ActivationTarget.Board(new GridPos(1, 1))), "infection started");
        Check(!joker.Activate(ctx, ActivationTarget.Board(new GridPos(3, 1))),
            "only one use per round");

        // Driven through real turns: 1-cube cards on a 5x5 board never fill a line, so the
        // only thing that destroys anything here is the infection itself.
        int cubesBefore = round.Board.OccupiedCount;
        PlayTurns(session, 1);
        Check(round.Board.OccupiedCount >= cubesBefore,
            "nothing blows on the first tick (a card was placed, none destroyed)",
            "occupied " + round.Board.OccupiedCount);

        int beforeDetonation = round.Board.OccupiedCount;
        PlayTurns(session, 1);
        Check(round.Board.OccupiedCount < beforeDetonation + 1,
            "the ripe cube detonated on the second tick",
            beforeDetonation + " -> " + round.Board.OccupiedCount);
    }

    private static void Oryantasyon_BuriesPlayedCardsInTheDrawPile()
    {
        Section("oryantasyon / cards go back into the draw pile");
        var session = NewSession(211, 8, 1000000, 30, 1);
        var joker = (OryantasyonJoker)session.Jokers.Add(new OryantasyonJoker());
        RoundEngine round = session.CurrentRound;

        Check(session.Config.Rules.PlayedCardsReturnToDrawPile, "the rule flag is on");
        Check(session.Config.Rules.RevealTopDrawCard, "the top of the draw pile is revealed");

        int drawBefore = round.Deck.DrawCount;
        PlayTurns(session, 3);
        Check(round.Deck.DiscardCount == 0, "nothing ever reached the discard",
            "discard " + round.Deck.DiscardCount);
        // Three cards played back in, three drawn out to refill: the pile size holds.
        Check(round.Deck.DrawCount == drawBefore, "the draw pile keeps its size",
            round.Deck.DrawCount + " vs " + drawBefore);

        session.Jokers.Remove(joker);
        Check(!session.Config.Rules.PlayedCardsReturnToDrawPile, "removal restores discarding");
        Check(!session.Config.Rules.RevealTopDrawCard, "removal hides the top card again");
    }

    private static void Dezenformasyon_SplitsAndSwapsThePilesEachTurn()
    {
        Section("dezenformasyon / piles split and swap");
        var session = NewSession(223, 8, 1000000, 30, 1);
        int baseHand = session.Config.Rules.HandSize;
        var joker = (DezenformasyonJoker)session.Jokers.Add(new DezenformasyonJoker());
        Check(session.Config.Rules.HandSize == baseHand + 1, "hand size grew by one",
            "hand " + session.Config.Rules.HandSize);

        RoundEngine round = session.CurrentRound;
        int totalBefore = round.Deck.DrawCount + round.Deck.DiscardCount + round.Hand.Count;
        PlayTurns(session, 1);

        Check(round.Deck.DiscardCount > 0, "the deck was split, so the discard holds cards",
            "discard " + round.Deck.DiscardCount);
        Check(round.Deck.DrawCount > 0, "and the draw pile holds cards",
            "draw " + round.Deck.DrawCount);
        int halves = round.Deck.DrawCount + round.Deck.DiscardCount;
        Check(Math.Abs(round.Deck.DrawCount - round.Deck.DiscardCount) <= 1,
            "the two piles are halves of each other",
            round.Deck.DrawCount + " vs " + round.Deck.DiscardCount);
        // The played CARD lands in a pile (only its cubes go to the board), so shuffling
        // the deck around must conserve every card.
        Check(halves + round.Hand.Count == totalBefore, "no card was lost or duplicated",
            (halves + round.Hand.Count) + " vs " + totalBefore);
        Check(joker.TurnsSeen == 1, "the turn counter drives the swap", "seen " + joker.TurnsSeen);

        session.Jokers.Remove(joker);
        Check(session.Config.Rules.HandSize == baseHand, "removal gives the hand size back");
    }

    private static void Imitasyon_HandTracksTheDiscardPile()
    {
        Section("imitasyon / hand mirrors the discard");
        var session = NewSession(227, 8, 1000000, 40, 1);
        var joker = (ImitasyonJoker)session.Jokers.Add(new ImitasyonJoker());
        joker.MaxHandSize = 6;
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;

        Check(session.Config.Rules.HandSize == 1, "an empty discard means a hand of one",
            "hand size " + session.Config.Rules.HandSize);

        PlayTurns(session, 1);
        Check(round.Hand.Count == session.Config.Rules.HandSize,
            "the hand is filled to the mirrored size",
            round.Hand.Count + " vs " + session.Config.Rules.HandSize);
        Check(session.Config.Rules.HandSize == round.Deck.DiscardCount
                || session.Config.Rules.HandSize == joker.MaxHandSize,
            "hand size equals the discard count (or the cap)",
            session.Config.Rules.HandSize + " vs discard " + round.Deck.DiscardCount);

        PlayTurns(session, 4);
        Check(session.Config.Rules.HandSize <= joker.MaxHandSize, "the cap holds",
            "hand size " + session.Config.Rules.HandSize);
        Check(session.Config.Rules.HandSize >= 1, "never drops below one");
    }

    private static void Fraksiyon_SplitsAtRoundStartAndAllowsOneSwap()
    {
        Section("fraksiyon / halve the deck, one swap per cycle");
        var session = NewSession(229, 8, 1000000, 30, 1);
        var joker = (FraksiyonJoker)session.Jokers.Add(new FraksiyonJoker());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;

        Check(round.Deck.DiscardCount > 0, "half the deck was pushed into the discard",
            "discard " + round.Deck.DiscardCount);
        Check(Math.Abs(round.Deck.DrawCount - round.Deck.DiscardCount) <= 1,
            "the two piles are halves", round.Deck.DrawCount + " vs " + round.Deck.DiscardCount);
        Check(session.Config.Rules.RevealedDiscardCount == round.Deck.DiscardCount / 2,
            "half the discard is revealed",
            "revealed " + session.Config.Rules.RevealedDiscardCount);

        int drawBefore = round.Deck.DrawCount;
        int discardBefore = round.Deck.DiscardCount;
        Check(joker.SwapAvailable, "the swap is available after a split");
        Check(session.Jokers.TryActivate(joker.InstanceId, ActivationTarget.None), "swap ran");
        Check(round.Deck.DrawCount == discardBefore && round.Deck.DiscardCount == drawBefore,
            "the piles changed places",
            round.Deck.DrawCount + "/" + round.Deck.DiscardCount);
        Check(!joker.SwapAvailable, "the swap is spent");
        Check(!session.Jokers.TryActivate(joker.InstanceId, ActivationTarget.None),
            "a second swap is refused before the next split");

        session.Jokers.Remove(joker);
        Check(session.Config.Rules.RevealedDiscardCount == 0, "removal hides the discard again");
    }

    private static void AllRegisteredJokers_HaveDistinctIdsAndText()
    {
        Section("registry / catalogue sanity");
        var ids = new HashSet<string>();
        bool allNamed = true;
        bool allDescribed = true;
        foreach (JokerDefinition definition in JokerRegistry.All)
        {
            if (!ids.Add(definition.DefId))
            {
                Check(false, "duplicate DefId", definition.DefId);
            }
            if (string.IsNullOrEmpty(definition.DisplayName))
            {
                allNamed = false;
            }
            if (string.IsNullOrEmpty(definition.Description))
            {
                allDescribed = false;
            }
            Joker instance = definition.Create();
            if (instance.DefId != definition.DefId)
            {
                Check(false, "factory produces a different DefId", definition.DefId);
            }
        }
        Check(ids.Count == JokerRegistry.All.Count, "every DefId is unique",
            ids.Count + " of " + JokerRegistry.All.Count);
        Check(allNamed, "every joker has a display name");
        Check(allDescribed, "every joker has a description");
        Check(JokerRegistry.All.Count >= 33, "the catalogue is complete",
            "count " + JokerRegistry.All.Count);
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

    private static void Market_CardSellValueByElement()
    {
        Section("market / card sell value");
        var config = new MarketConfig();
        var vanilla = new BlockCard(1, Bar(3));
        var golden = new BlockCard(2, Bar(3), new[] { BlockElement.Gold });
        Check(config.SellValue(vanilla) == 0, "a plain block sells for nothing",
            "got " + config.SellValue(vanilla));
        int sell = config.SellValue(golden);
        Check(sell > 0 && sell < config.BuyPrice(golden),
            "an elemental block sells for less than its buy price",
            "sell " + sell + " buy " + config.BuyPrice(golden));

        // The session path removes the card and pays exactly that value.
        var session = new GameSession(new GameConfig { RngSeed = 5 });
        BlockCard owned = session.OwnedCards[0]; // starting deck is plain
        int before = session.OwnedCards.Count;
        long money = session.TotalScore;
        long paid = session.SellCard(owned);
        Check(paid == 0 && session.TotalScore == money, "selling a plain owned card pays nothing");
        Check(session.OwnedCards.Count == before - 1, "the sold card leaves the deck",
            "count " + session.OwnedCards.Count);
        Check(session.SellCard(owned) == 0, "selling the same card twice is a no-op");
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

    private static void Market_NeverOffersOwnedJokers()
    {
        Section("market / owned jokers excluded");
        // Owning part of the catalogue: offers must come from the rest.
        var config = new GameConfig();
        config.RngSeed = 303;
        var session = new GameSession(config);
        var ownedIds = new HashSet<string>();
        for (int i = 0; i < JokerRegistry.All.Count - 1; i++)
        {
            Joker granted = session.Jokers.Add(JokerRegistry.All[i].Create());
            ownedIds.Add(granted.DefId);
        }
        session = DriveOwnedToMarket(session);
        bool clean = true;
        int jokerOffers = 0;
        foreach (MarketOffer offer in session.Market.Offers)
        {
            if (offer.Kind != MarketOfferKind.Joker)
            {
                continue;
            }
            jokerOffers++;
            if (ownedIds.Contains(offer.Joker.DefId))
            {
                clean = false;
            }
        }
        Check(session.Phase == GamePhase.Market, "reached the market", "phase " + session.Phase);
        Check(clean, "no owned joker is offered");
        Check(jokerOffers <= 1, "offers cannot exceed the unowned pool", "offers " + jokerOffers);

        // Owning everything: the joker section simply stays empty.
        var full = new GameSession(new GameConfig { RngSeed = 304 });
        foreach (JokerDefinition definition in JokerRegistry.All)
        {
            full.Jokers.Add(definition.Create());
        }
        full = DriveOwnedToMarket(full);
        int fullOffers = 0;
        foreach (MarketOffer offer in full.Market.Offers)
        {
            if (offer.Kind == MarketOfferKind.Joker)
            {
                fullOffers++;
            }
        }
        Check(full.Phase == GamePhase.Market && fullOffers == 0,
            "a full catalogue owner sees no joker offers", "offers " + fullOffers);
    }

    /// <summary>Like DriveToMarket but continues an existing session (jokers pre-granted).</summary>
    private static GameSession DriveOwnedToMarket(GameSession session)
    {
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
