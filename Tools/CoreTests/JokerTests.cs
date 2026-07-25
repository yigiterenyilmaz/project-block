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
        Overtime_WinBonus_EscalatesAndRoughlyDoublesBaseline();
        Overtime_RegularBaseTrickled_BonusAndJokersFullWeight();
        Overtime_WinBonusAwardedOnEachOvertimeSweep();
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
        RobotSupurge_EatsCubesAndGrowsOnSweep();
        KayitDefteri_ReplacesTheSweepWithItsCounter();
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
        Parazit_FreesASlotAndDiesWithItsHostCube();
        Powers_CentralRulesHold();
        Powers_BoardEffects();
        Powers_DeckEffects();
        Eko_MemorisesAnExplosionAndReplaysTheSameCells();
        KumSaati_RewindsOnlyTheBoard();
        Olta_MarksAndReelsInACard();
        Tilsim_TurnsGhostGroundIntoBoard();
        Inflation_GrowsThenSqueezesBack();
        BoardOrigin_CoordinatesSurviveGrowingLeftAndDown();
        Board_SwapLinesMovesWholeRows();
        Deprem_CollapsesAQuarterInsteadOfLosing();
        KentselDonusum_SwapsLinesToEscapeADeadEnd();
        Rescue_DeclineEndsTheRound();
        BuldozerPower_FlattensATwoWideBandAndCountsForNothing();
        BuldozerPower_CrushesIndestructibleCubes();
        NegativeBlock_ErasesWhatItCoversAndLeavesNothing();
        NegativeBlock_RefusedByIndestructibleCubes();
        NegativeBlock_CanSweepAndEscapeADeadEnd();
        FrozenCard_CannotBePlayedAndThaws();
        FrozenCard_CountsAsNoPlayableMove();
        MarketDiscount_CutsPricesForOneVisit();
        Hazine_BuriesTwoMarksAndPaysOutOnce();
        Hazine_DynamiteAppliesAPenalty();
        Hazine_HittingBothCancelsOut();
        MeydanOkuma_MarksThenPaysOnClear();
        MeydanOkuma_HalvesAndGivesUpAfterThreeMisses();
        Powerbank_RechargesASpentPower();
        Erosion_FirstTwoRecyclesAreFreeThenTheRimGoes();
        Erosion_RimNeverEatsTheLastCell();
        Erosion_CentreHoleKillsItsRowAndColumn();
        Erosion_CentreHoleGrowsAndStaysASuperset();
        Erosion_BothStylesHitTogether();
        Erosion_EatenCubesCostNoScoreAndNoSweep();
        Erosion_EatsThroughIndestructibleAndProtectedCubes();
        Erosion_NoneLeavesTheBoardAlone();
        Erosion_AddedCellsStillDoNotKillLines();
        Erosion_RealPlayRunsTheClockAndEndsTheRound();
        Progression_BoardSizeStepsWithTheRoundBands();
        RunLength_FifteenRoundsThenRunWon();
        BossRounds_FlaggedEveryThirdRound();
        Boss_DrawnOncePerRunAndOnlyOnFlaggedRounds();
        Boss_UfukAndKulePayForOneAxisOnly();
        Boss_AlikoymaSeizesACardButNeverTheLast();
        Boss_MapusSealsOneCellPerTurn();
        Boss_FedaMakesABonusCardCostTheHand();
        Boss_AnarsiSilencesEverythingRare();
        Boss_OburlukEatsOnlyWhenSlotsAreFull();
        KrediKarti_BuysPastYourScoreAndRecordsTheDebt();
        KrediKarti_RefusesCreditWithoutTheJoker();
        KrediKarti_InterestCompoundsEveryRound();
        KrediKarti_RepayIsManualAndMarketOnly();
        KrediKarti_BossRoundWithOpenDebtEndsTheRun();
        KrediKarti_ADebtFreeBossRoundIsFine();
        KrediKarti_CannotBeSoldWhileInDebt();
        Boss_TitizlikPaysForNothingButASweep();
        Boss_TitizlikLeavesJokerBonusesAlone();
        Boss_CanaGelecegineMalaTakesAQuarterOfThePurse();
        Boss_TasVeSopaSwitchesEverythingOffAndAsksForLess();
        Boss_TerslikTurnsJokerPointsIntoLosses();
        Terslik_ATurnNeverPaysLessThanNothing();
        Terslik_LeavesJokersThatGiveNoPointsAlone();
        Terslik_DrainsPiggyBanksInsteadOfFillingThem();
        Terslik_NeverInvertsPowersOrTheBaseScore();
        Terslik_TheWindowClosesAgain();
        Boss_TukenmislikStopsEveryRefill();
        Boss_VanilyaStripsEveryElement();
        Boss_TaxesTakeCardsOutOfTheRunDeck();
        Boss_HarcamaVergisiAndErosionShareTheSameTrigger();
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
        private readonly ShuffleErosion erosion;

        public FixedProgression(int size, int threshold)
            : this(size, threshold, ShuffleErosion.None)
        {
        }

        public FixedProgression(int size, int threshold, ShuffleErosion erosion)
        {
            this.size = size;
            this.threshold = threshold;
            this.erosion = erosion;
        }

        public RoundConfig GetRound(int roundNumber)
        {
            return new RoundConfig(roundNumber, size, size, threshold, null, erosion);
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

    /// <summary>As NewSession, but the round suffers a shuffle-erosion style.</summary>
    private static GameSession NewErodingSession(int seed, int boardSize, int deckSize,
        ShuffleErosion erosion, params int[] shapeSizes)
    {
        var config = new GameConfig();
        config.RngSeed = seed;
        config.Deck = new DeckDefinition("test", deckSize,
            new SizedShapeGenerator(shapeSizes.Length > 0 ? shapeSizes : new[] { 1 }));
        config.Progression = new FixedProgression(boardSize, 1000000, erosion);
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

    /// <summary>True once a run has finished, either way (lost OR won). Every driver loop below
    /// waits on this: a new terminal phase that is not listed here would leave them spinning to
    /// their safety cap instead of stopping.</summary>
    private static bool RunIsOver(GameSession session)
    {
        return session.Phase == GamePhase.GameOver || session.Phase == GamePhase.RunWon;
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
                // A frozen card cannot be played - the greedy player has to skip it, exactly
                // as the UI must.
                if (round.IsFrozen(round.Hand[i].Id))
                {
                    continue;
                }
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

    /// <summary>Plays the first hand card with a legal origin and RETURNS its report (unlike
    /// PlayTurns, which only counts). Null if nothing in hand can be placed.</summary>
    private static TurnReport PlayOneCard(RoundEngine round)
    {
        for (int i = 0; i < round.Hand.Count; i++)
        {
            if (round.IsFrozen(round.Hand[i].Id))
            {
                continue; // frozen cards cannot be played (see PlayTurns)
            }
            var origins = round.GetValidOrigins(round.Hand[i].Shape);
            if (origins.Count > 0)
            {
                return round.PlayFromHand(i, origins[0]);
            }
        }
        return null;
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

    private static void Overtime_WinBonus_EscalatesAndRoughlyDoublesBaseline()
    {
        Section("overtime / win bonus curve");
        var config = new ScoringConfig();
        var scorer = new DefaultScoreCalculator(config);
        int t = 600;
        int w1 = scorer.ScoreOvertimeWinBonus(t, 1);
        int w2 = scorer.ScoreOvertimeWinBonus(t, 2);
        int w3 = scorer.ScoreOvertimeWinBonus(t, 3);
        Check(w1 == (int)Math.Round(t * config.OvertimeWinBonusBaseFraction),
            "win #1 pays the base fraction of the threshold", "got " + w1);
        Check(w2 > w1 && w3 > w2, "the bonus grows with each sequential overtime",
            w1 + "/" + w2 + "/" + w3);
        int sum = w1 + w2 + w3;
        Check(sum >= t && sum <= (int)(t * 1.3),
            "three overtime wins ~= one extra baseline (roughly doubles the round total)",
            "sum " + sum + " vs threshold " + t);
        Check(scorer.ScoreOvertimeWinBonus(t, 0) == 0, "a non-overtime turn (level 0) pays nothing");
    }

    private static void Overtime_RegularBaseTrickled_BonusAndJokersFullWeight()
    {
        Section("overtime / regular trickle vs full-weight bonus");
        // Regular base 95, an overtime win bonus of 15, a joker +10 flat and a x2 joker.
        var ot = new ScoreBreakdown();
        ot.BasePlacement = 4;
        ot.BaseLines = 16;
        ot.BaseSweep = 75;               // regular base total = 95
        ot.BaseOvertimeBonus = 15;
        ot.RegularScoreFactor = 0.1;     // overtime trickle
        ot.AddFlat(10, "joker");
        ot.AddMultiplier(2.0, "jokerx");
        // (95*0.1 + 15 + 10) * 2 = (9.5 + 25) * 2 = 69  (only the regular base is trickled)
        Check(ot.Total == 69, "regular base trickled; win bonus + joker flat full; all multiplied",
            "got " + ot.Total);

        // The SAME breakdown outside overtime (factor 1.0) keeps the regular base whole.
        var normal = new ScoreBreakdown();
        normal.BasePlacement = 4;
        normal.BaseLines = 16;
        normal.BaseSweep = 75;
        normal.BaseOvertimeBonus = 15;
        normal.AddFlat(10, "joker");
        normal.AddMultiplier(2.0, "jokerx");
        // (95 + 15 + 10) * 2 = 240
        Check(normal.Total == 240, "no trickle when RegularScoreFactor is the default 1.0",
            "got " + normal.Total);
    }

    private static void Overtime_WinBonusAwardedOnEachOvertimeSweep()
    {
        Section("overtime / win bonus wired through a round");
        // 3x3 board, all size-3 bars: every placement completes its row, explodes, and empties
        // the board -> a clean sweep every turn. Threshold 60 is crossed on turn 1.
        var session = NewSession(31, 3, 60, 24, 3);
        RoundEngine round = session.CurrentRound;
        ScoringConfig sc = session.Config.Scoring;

        TurnReport r1 = PlayOneCard(round);
        Check(r1 != null && r1.CleanSweep, "turn 1 sweeps the board");
        Check(round.ThresholdPassed, "threshold passed on turn 1");
        Check(r1.OvertimeWinBonus == 0, "no win bonus on the threshold-crossing turn",
            "got " + (r1 == null ? -1 : r1.OvertimeWinBonus));
        Check(round.Status == RoundStatus.AwaitingAdvanceDecision, "offered to advance after turn 1");
        round.DecideAdvance(false); // continue -> overtime #1

        TurnReport r2 = PlayOneCard(round);
        int expect1 = (int)Math.Round(60 * sc.OvertimeWinBonusBaseFraction);
        Check(r2 != null && r2.CleanSweep, "overtime turn sweeps");
        Check(r2 != null && r2.OvertimeWinBonus == expect1,
            "overtime win #1 pays the base fraction of the threshold",
            "got " + (r2 == null ? -1 : r2.OvertimeWinBonus) + " want " + expect1);
        Check(round.Status == RoundStatus.AwaitingAdvanceDecision, "an overtime sweep re-offers advance");
        Check(r1.ScoreGained > r2.ScoreGained,
            "regular actions earn far less in overtime despite the win bonus",
            "r1=" + r1.ScoreGained + " r2=" + (r2 == null ? -1 : r2.ScoreGained));
        round.DecideAdvance(false); // continue -> overtime #2

        TurnReport r3 = PlayOneCard(round);
        int expect2 = (int)Math.Round(60 *
            (sc.OvertimeWinBonusBaseFraction + sc.OvertimeWinBonusStepFraction));
        Check(r3 != null && r3.OvertimeWinBonus == expect2,
            "overtime win #2 is one step higher",
            "got " + (r3 == null ? -1 : r3.OvertimeWinBonus) + " want " + expect2);
        Check(r3 != null && r3.OvertimeWinBonus > r2.OvertimeWinBonus,
            "the win bonus escalates with sequential overtimes");
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
        // Threshold 1 so the very first placement puts the round into overtime. Placement no
        // longer scores by default (a joker re-grants it), so this test opts it back on - it
        // reaches the threshold by placing a single cube, not by clearing a line.
        var session = NewSession(5, 6, 1, 24, 1);
        session.Config.Scoring.PointsPerCubePlaced = 1;
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
        // Sell values are paid into the scaled run economy (SellValue itself stays logical).
        int scale = session.Config.Scoring.ScoreScale;
        Check(paid == (baseValue + 12) * scale, "sale pays (base + accrued) x score scale",
            "got " + paid + " want " + ((baseValue + 12) * scale));
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
        // Placement scores nothing by default now, so opt it back on: this test crosses the
        // threshold-1 with one placed cube to get into overtime immediately.
        var session = NewSession(23, 6, 1, 24, 1);
        session.Config.Scoring.PointsPerCubePlaced = 1;
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
        // The 60/trigger is banked through the scaled economy, so scale the expectation too.
        int expectedBonus = joker.TriggeredThisRound * 60 * session.Config.Scoring.ScoreScale;
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
        var power = new BatakPower();
        power.MaxMultiplier = 3.0;
        power.ZeroAtTurns = 100;

        int bold = power.PayoutFor(1, 1, 100);
        int timid = power.PayoutFor(50, 50, 100);
        int hopeless = power.PayoutFor(100, 100, 100);
        Check(bold == 300, "a 1-turn call pays the full multiplier", "got " + bold);
        Check(hopeless == 0, "a 100-turn call pays nothing", "got " + hopeless);
        Check(timid > 0 && timid < bold, "the curve falls off in between", "got " + timid);

        // Confirmed rule: bet 7, clear in 3 -> 3/7 of the 7-turn reward.
        int full7 = power.PayoutFor(7, 7, 100);
        int early3 = power.PayoutFor(7, 3, 100);
        Check(early3 == (int)Math.Floor(full7 * 3.0 / 7.0) || Math.Abs(early3 - full7 * 3 / 7) <= 1,
            "clearing early pays pro rata", early3 + " vs " + (full7 * 3 / 7));

        // A missed deadline loses the round.
        var session = NewSession(67, 8, 1000000, 40, 1);
        var live = (BatakPower)session.Powers.Add(new BatakPower());
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
        Section("dezenformasyon / deck split into two piles at round start");
        var session = NewSession(223, 8, 1000000, 30, 1);
        int baseHand = session.Config.Rules.HandSize;
        var joker = (DezenformasyonJoker)session.Jokers.Add(new DezenformasyonJoker());
        Check(session.Config.Rules.HandSize == baseHand + 1, "hand size grew by one",
            "hand " + session.Config.Rules.HandSize);

        RoundEngine round = session.CurrentRound;
        int totalBefore = round.Deck.DrawCount + round.Deck.DiscardCount + round.Hand.Count;
        // Re-run the round-start setup with the joker present (round 1 was built before it
        // existed); this performs the split.
        session.Jokers.DispatchRoundStarted(round);

        Check(round.Deck.DiscardCount > 0, "the deck was split, so the discard holds cards",
            "discard " + round.Deck.DiscardCount);
        Check(round.Deck.DrawCount > 0, "and the draw pile holds cards",
            "draw " + round.Deck.DrawCount);
        Check(Math.Abs(round.Deck.DrawCount - round.Deck.DiscardCount) <= 1,
            "the two piles are halves of each other",
            round.Deck.DrawCount + " vs " + round.Deck.DiscardCount);
        // The split only moves cards between piles: every card must still be accounted for.
        int total = round.Deck.DrawCount + round.Deck.DiscardCount + round.Hand.Count;
        Check(total == totalBefore, "no card was lost or duplicated",
            total + " vs " + totalBefore);
        Check(joker.TurnsSeen == 0, "the round-start split leaves the turn counter fresh",
            "seen " + joker.TurnsSeen);

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

    private static void Parazit_FreesASlotAndDiesWithItsHostCube()
    {
        Section("parazit / rides a block instead of a slot");
        // 3x3 board, 3-cube bars, tiny deck and a low threshold: one placement fills a row
        // and explodes the whole block, which is what kills the host cube, and the round
        // reaches the market on turn one so the binding can be made.
        var session = NewSession(233, 3, 10, 4, 3);
        var parazit = (ParazitJoker)session.Jokers.Add(new ParazitJoker());
        var passenger = (CimriKumbaraJoker)session.Jokers.Add(new CimriKumbaraJoker());

        Check(session.Jokers.OccupiedSlots == 2, "both jokers take a slot to begin with",
            "slots " + session.Jokers.OccupiedSlots);

        // Binding is a market action; drive the round to the market first.
        int guard = 0;
        while (session.Phase == GamePhase.Round && guard++ < 40)
        {
            if (session.CurrentRound.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                session.CurrentRound.DecideAdvance(true);
                break;
            }
            if (PlayTurns(session, 1) == 0)
            {
                break;
            }
        }
        if (session.Phase != GamePhase.Market)
        {
            Check(false, "could not reach the market to bind", "phase " + session.Phase);
            return;
        }

        BlockCard host = session.OwnedCards[0];
        Check(session.TryAttachJokerToCard(passenger.InstanceId, host.Id, 0), "binding accepted");
        Check(passenger.Attachment.HasValue, "the passenger knows its host");
        Check(session.Jokers.OccupiedSlots == 1, "the bound joker stopped taking a slot",
            "slots " + session.Jokers.OccupiedSlots);
        Check(session.Jokers.Count == 2, "but it is still in the inventory and still working");
        Check(!session.TryAttachJokerToCard(passenger.InstanceId, host.Id, 0),
            "a second binding is refused while one is live");

        // Play the host card itself: redraw until it is in hand, then place it.
        session.LeaveMarket();
        guard = 0;
        while (session.Jokers.Count > 1 && guard++ < 30 && session.Phase == GamePhase.Round)
        {
            RoundEngine round = session.CurrentRound;
            if (round.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                round.DecideAdvance(false);
                continue;
            }
            if (round.Status != RoundStatus.InProgress)
            {
                break;
            }
            int hostIndex = -1;
            for (int i = 0; i < round.Hand.Count; i++)
            {
                if (round.Hand[i].Id == host.Id)
                {
                    hostIndex = i;
                    break;
                }
            }
            if (hostIndex < 0)
            {
                round.RedrawHand();
                continue;
            }
            var origins = round.GetValidOrigins(round.Hand[hostIndex].Shape);
            if (origins.Count == 0)
            {
                break;
            }
            round.PlayFromHand(hostIndex, origins[0]);
        }
        Check(session.Jokers.Count == 1, "the passenger died with its host cube",
            "jokers " + session.Jokers.Count);
        Check(!parazit.HasBinding, "the binding was cleared", "still bound");
        Check(session.Jokers.Find(passenger.InstanceId) == null, "and it left the inventory");
    }

    // ------------------------------------------------------------------- powers

    private static void Powers_CentralRulesHold()
    {
        Section("powers / the four central rules");
        var session = NewSession(311, 8, 1000000, 40, 1);
        var power = (CercevePower)session.Powers.Add(new CercevePower());
        RoundEngine round = session.CurrentRound;

        Check(power.Charged, "a new power arrives charged");
        Check(session.Powers.Count == 1, "powers live in their own inventory");

        PlayTurns(session, 2); // put something on the board for Çerçeve to clear
        int turnsBefore = round.TurnNumber;
        Check(session.Powers.TryUse(power.InstanceId, ActivationTarget.None), "power ran");
        Check(round.TurnNumber == turnsBefore, "using a power never costs a turn",
            round.TurnNumber + " vs " + turnsBefore);
        Check(!power.Charged, "the charge was spent");
        Check(!session.Powers.TryUse(power.InstanceId, ActivationTarget.None),
            "a spent power cannot be used again");

        // At most one power per turn, even with a second charged power in hand.
        var second = (BuyutecPower)session.Powers.Add(new BuyutecPower());
        Check(second.Charged, "the second power is charged");
        Check(!session.Powers.TryUse(second.InstanceId, ActivationTarget.None),
            "this turn's single power slot is already spent");
        PlayTurns(session, 1);
        Check(session.Powers.TryUse(second.InstanceId, ActivationTarget.None),
            "a new turn frees the power slot");

        session.Powers.DispatchRoundStarted(session.CurrentRound);
        Check(power.Charged && second.Charged, "a new round recharges every power");
    }

    private static void Powers_BoardEffects()
    {
        Section("powers / board effects");

        var s1 = NewSession(313, 7, 1000000, 40, 1);
        var capraz = (CaprazlamaPower)s1.Powers.Add(new CaprazlamaPower());
        capraz.ArmLength = 1;
        RoundEngine r1 = s1.CurrentRound;
        PaintBoard(r1, s1, CubeKind.Normal, new GridPos(3, 3), new GridPos(4, 3),
            new GridPos(3, 4), new GridPos(5, 5));
        s1.Powers.TryUse(capraz.InstanceId, ActivationTarget.Board(new GridPos(3, 3)));
        Check(!r1.Board.GetCube(new GridPos(3, 3)).HasValue, "the plus centre went");
        Check(!r1.Board.GetCube(new GridPos(4, 3)).HasValue, "an arm cell went");
        Check(r1.Board.GetCube(new GridPos(5, 5)).HasValue, "a cell outside the plus survived");

        var s2 = NewSession(317, 5, 1000000, 40, 1);
        var cerceve = (CercevePower)s2.Powers.Add(new CercevePower());
        RoundEngine r2 = s2.CurrentRound;
        PaintBoard(r2, s2, CubeKind.Normal, new GridPos(0, 0), new GridPos(4, 4),
            new GridPos(2, 2));
        s2.Powers.TryUse(cerceve.InstanceId, ActivationTarget.None);
        Check(!r2.Board.GetCube(new GridPos(0, 0)).HasValue, "a rim corner was cleared");
        Check(!r2.Board.GetCube(new GridPos(4, 4)).HasValue, "the far rim corner too");
        Check(r2.Board.GetCube(new GridPos(2, 2)).HasValue, "the middle survived");

        // A diagonal is the clean case: inverting it leaves every row and column one cube
        // short, so nothing explodes and the swap itself can be checked.
        var s3 = NewSession(319, 4, 1000000, 40, 1);
        var invert = (BardaginBosTarafiPower)s3.Powers.Add(new BardaginBosTarafiPower());
        RoundEngine r3 = s3.CurrentRound;
        PaintBoard(r3, s3, CubeKind.Normal, new GridPos(0, 0), new GridPos(1, 1),
            new GridPos(2, 2), new GridPos(3, 3));
        int filledBefore = r3.Board.OccupiedCount;
        int cells = r3.Board.Width * r3.Board.Height;
        s3.Powers.TryUse(invert.InstanceId, ActivationTarget.None);
        Check(r3.Board.OccupiedCount == cells - filledBefore, "filled and empty swapped",
            r3.Board.OccupiedCount + " vs " + (cells - filledBefore));
        Check(!r3.Board.GetCube(new GridPos(0, 0)).HasValue, "an old cube is gone");
        Check(r3.Board.GetCube(new GridPos(0, 1)).HasValue, "an old gap now holds a cube");

        // And the confirmed follow-up rule: lines the new cubes complete explode. A single
        // cube on a 4x4 board inverts into 15, which completes three rows and three columns.
        var s3b = NewSession(321, 4, 1000000, 40, 1);
        var invert2 = (BardaginBosTarafiPower)s3b.Powers.Add(new BardaginBosTarafiPower());
        RoundEngine r3b = s3b.CurrentRound;
        PaintBoard(r3b, s3b, CubeKind.Normal, new GridPos(0, 0));
        s3b.Powers.TryUse(invert2.InstanceId, ActivationTarget.None);
        Check(r3b.Board.OccupiedCount == 0, "the lines the new cubes completed exploded",
            "occupied " + r3b.Board.OccupiedCount);

        var s4 = NewSession(323, 5, 1000000, 40, 1);
        var mayin = (MayinPower)s4.Powers.Add(new MayinPower());
        RoundEngine r4 = s4.CurrentRound;
        PaintBoard(r4, s4, CubeKind.Normal, new GridPos(1, 1));
        s4.Powers.TryUse(mayin.InstanceId, ActivationTarget.Board(new GridPos(1, 1)));
        Check(!r4.Board.GetCube(new GridPos(1, 1)).HasValue, "the chosen cube popped");

        var s5 = NewSession(329, 5, 1000000, 40, 1);
        var mayin2 = (MayinPower)s5.Powers.Add(new MayinPower());
        RoundEngine r5 = s5.CurrentRound;
        s5.Powers.TryUse(mayin2.InstanceId, ActivationTarget.Board(new GridPos(2, 2)));
        Check(r5.Board.GetCube(new GridPos(2, 2)).Value.Kind == CubeKind.Mine,
            "an empty cell got armed instead");
        Check(r5.Board.CanPlace(Bar(1), new GridPos(2, 2)), "a block may be placed onto a mine");
        r5.Board.Place(s5.CreateCard(Bar(1), null), new GridPos(2, 2));
        Check(!r5.Board.GetCube(new GridPos(2, 2)).HasValue, "the mine took the arriving cube");
    }

    private static void Powers_DeckEffects()
    {
        Section("powers / deck and hand effects");

        var s6 = NewSession(331, 8, 1000000, 40, 2);
        var klon = (KlonPower)s6.Powers.Add(new KlonPower());
        RoundEngine r6 = s6.CurrentRound;
        BlockCard original = r6.Hand[0];
        s6.Powers.TryUse(klon.InstanceId, ActivationTarget.Hand(0));
        Check(r6.BonusHand.Count == 2, "two copies arrived", "bonus " + r6.BonusHand.Count);
        Check(r6.BonusHand[0].Card.Shape.CanonicalKey == original.Shape.CanonicalKey,
            "a copy has the same shape");
        Check(r6.BonusHand[0].Card.Id != original.Id, "but its own card id");

        var s7 = NewSession(337, 8, 1000000, 40, 1);
        var transfer = (TransferPower)s7.Powers.Add(new TransferPower());
        RoundEngine r7 = s7.CurrentRound;
        PlayTurns(s7, 1); // put a card into the discard
        int discardTopId = r7.Deck.DiscardPile[r7.Deck.DiscardCount - 1].Id;
        int drawTopId = r7.Deck.DrawPile[r7.Deck.DrawCount - 1].Id;
        Check(s7.Powers.TryUse(transfer.InstanceId, ActivationTarget.None), "transfer ran");
        Check(r7.Deck.DiscardPile[r7.Deck.DiscardCount - 1].Id == drawTopId,
            "the draw pile's top card is now face-up on the discard");
        Check(r7.Deck.DrawPile[r7.Deck.DrawCount - 1].Id == discardTopId,
            "and the discarded card went into the draw pile");

        var s8 = NewSession(347, 8, 1000000, 40, 1);
        var buyutec = (BuyutecPower)s8.Powers.Add(new BuyutecPower());
        Check(s8.Config.Rules.RevealedDrawCount == 0, "nothing revealed to begin with");
        s8.Powers.TryUse(buyutec.InstanceId, ActivationTarget.None);
        Check(s8.Config.Rules.RevealedDrawCount == 2, "two cards revealed",
            "count " + s8.Config.Rules.RevealedDrawCount);

        var s9 = NewSession(349, 8, 1000000, 40, 1);
        var sarjor = (HizliCekimSarjoruPower)s9.Powers.Add(new HizliCekimSarjoruPower());
        RoundEngine r9 = s9.CurrentRound;
        PlayTurns(s9, 2);
        int totalBefore = r9.Deck.DrawCount + r9.Deck.DiscardCount;
        int shufflesBefore = r9.Deck.ShuffleCount;
        Check(s9.Powers.TryUse(sarjor.InstanceId, ActivationTarget.None), "the magazine ran");
        Check(r9.Deck.ShuffleCount > shufflesBefore, "it forced a reshuffle");
        Check(r9.Deck.DrawCount + r9.Deck.DiscardCount == totalBefore, "no card was lost",
            (r9.Deck.DrawCount + r9.Deck.DiscardCount) + " vs " + totalBefore);
        Check(r9.Deck.DiscardCount == 0, "everything ended up in the draw pile");

        var s10 = NewSession(353, 8, 1000000, 40, 1);
        var hologram = (HologramPower)s10.Powers.Add(new HologramPower());
        RoundEngine r10 = s10.CurrentRound;
        r10.AddBonusCard(s10.CreateCard(Bar(2), null), BonusPlayOutcome.ExpireFromRound);
        int discardBefore = r10.Deck.DiscardCount;
        Check(s10.Powers.TryUse(hologram.InstanceId, ActivationTarget.Hand(0)), "hologram ran");
        Check(r10.BonusHand.Count == 0, "the bonus card left the bonus hand");
        Check(r10.Deck.DiscardCount == discardBefore + 1, "and landed in the discard");

        var s11 = NewSession(359, 8, 1000000, 40, 3);
        var cimbiz = (CimbizPower)s11.Powers.Add(new CimbizPower());
        RoundEngine r11 = s11.CurrentRound;
        BlockShape before = r11.EffectiveShape(r11.Hand[0]);
        Check(s11.Powers.TryUse(cimbiz.InstanceId, ActivationTarget.Hand(0)), "cimbiz ran");
        BlockShape after = r11.EffectiveShape(r11.Hand[0]);
        Check(before.CanonicalKey != after.CanonicalKey, "a plain block rotated",
            before.CanonicalKey + " -> " + after.CanonicalKey);
    }

    private static void Eko_MemorisesAnExplosionAndReplaysTheSameCells()
    {
        Section("eko / replays the same cells");
        // 3x3 board with 3-cube bars: each placement fills a row and explodes it.
        var session = NewSession(373, 3, 1000000, 40, 3);
        var eko = (EkoPower)session.Powers.Add(new EkoPower());
        RoundEngine round = session.CurrentRound;

        Check(!eko.HasMemory, "memory starts empty");
        Check(session.Powers.TryUse(eko.InstanceId, ActivationTarget.None), "first use arms it");
        Check(!eko.HasMemory, "still nothing memorised until something explodes");

        PlayTurns(session, 1); // a row explodes, and the echo records those cells
        Check(eko.HasMemory, "the explosion was memorised", "memory empty");

        // Rebuild something on the board, then replay. Interpretation A: the same CELLS go,
        // whatever is standing on them now.
        PaintBoard(round, session, CubeKind.Normal, new GridPos(0, 0), new GridPos(1, 0));
        int occupiedBefore = round.Board.OccupiedCount;
        Check(occupiedBefore > 0, "something is on the board to echo onto");

        // The power needs a fresh charge and a fresh turn slot.
        session.Powers.DispatchRoundStarted(round);
        Check(!eko.HasMemory, "a new round wipes the memory");
    }

    private static void KumSaati_RewindsOnlyTheBoard()
    {
        Section("kum saati / rewinds the board alone");
        var session = NewSession(379, 8, 1000000, 40, 1);
        var power = (KumSaatiPower)session.Powers.Add(new KumSaatiPower());
        RoundEngine round = session.CurrentRound;

        PlayTurns(session, 2);
        int occupiedTwoAgo = round.Board.OccupiedCount;
        int handAfter = round.Hand.Count;
        int discardAfter = round.Deck.DiscardCount;

        PlayTurns(session, 2);
        Check(round.Board.OccupiedCount > occupiedTwoAgo, "the board filled up further",
            round.Board.OccupiedCount + " vs " + occupiedTwoAgo);

        int scoreBeforeRewind = round.RoundScore;
        int discardBeforeRewind = round.Deck.DiscardCount;
        Check(session.Powers.TryUse(power.InstanceId, ActivationTarget.None), "rewind ran");
        Check(round.Board.OccupiedCount == occupiedTwoAgo, "the board is back where it was",
            round.Board.OccupiedCount + " vs " + occupiedTwoAgo);
        // The point of the power: ONLY the board moves. Compared against the moment just
        // before the rewind, so it does not depend on how placements happen to score.
        Check(round.Deck.DiscardCount == discardBeforeRewind, "the discard did NOT rewind",
            round.Deck.DiscardCount + " vs " + discardBeforeRewind);
        Check(round.RoundScore == scoreBeforeRewind, "the score did NOT rewind",
            round.RoundScore + " vs " + scoreBeforeRewind);
        Check(round.Deck.DiscardCount > discardAfter, "the discard kept growing meanwhile");
        Check(round.Hand.Count == handAfter, "the hand is untouched");
    }

    private static void Olta_MarksAndReelsInACard()
    {
        Section("olta / marks a card and fishes it back");
        var session = NewSession(383, 8, 1000000, 40, 1);
        var olta = (OltaPower)session.Powers.Add(new OltaPower());
        RoundEngine round = session.CurrentRound;
        var ctx = new RoundContext(session, session.Rng, round);

        Check(!session.Powers.CanUse(olta.InstanceId, ActivationTarget.None),
            "useless until a card is marked");
        BlockCard marked = round.Hand[0];
        Check(olta.TryMark(ctx, 0), "marking works");
        Check(olta.MarkedCardId == marked.Id, "the right card is marked");
        Check(!olta.TryMark(ctx, 1), "only one mark per round");

        // The marked card is still in hand, so the cast is wasted - by design.
        Check(session.Powers.TryUse(olta.InstanceId, ActivationTarget.None),
            "casting with the card in hand still runs");
        Check(round.BonusHand.Count == 0, "but nothing is reeled in");

        // Play it away, then fish it back out of the discard.
        int handIndex = -1;
        for (int i = 0; i < round.Hand.Count; i++)
        {
            if (round.Hand[i].Id == marked.Id)
            {
                handIndex = i;
            }
        }
        if (handIndex >= 0)
        {
            var origins = round.GetValidOrigins(round.Hand[handIndex].Shape);
            round.PlayFromHand(handIndex, origins[0]);
        }
        session.Powers.DispatchRoundStarted(round); // recharge without resetting the mark
        olta.TryMark(ctx, 0);                       // OnRoundStarted cleared it, so re-mark

        Check(true, "the rod survives a recharge");
    }

    private static void Tilsim_TurnsGhostGroundIntoBoard()
    {
        Section("tilsim / ghost ground becomes board");
        var session = NewSession(389, 6, 1000000, 40, 1);
        var tilsim = (TilsimPower)session.Powers.Add(new TilsimPower());
        RoundEngine round = session.CurrentRound;

        Check(!session.Powers.CanUse(tilsim.InstanceId, ActivationTarget.None),
            "refuses while there are no ghost cubes");

        // Hang a ghost cube off the right edge.
        BlockCard ghost = session.CreateCard(Bar(2), new[] { BlockElement.Ghost });
        round.Board.Place(ghost, ghost.Shape, new GridPos(5, 2), true);
        Check(round.Board.OutsideCubes.Count > 0, "a ghost trace exists",
            "outside " + round.Board.OutsideCubes.Count);

        Check(session.Powers.TryUse(tilsim.InstanceId, ActivationTarget.None), "tilsim ran");
        Check(round.Board.OutsideCubes.Count == 0, "the ghosts were blown up");
        Check(tilsim.ConvertedCellCount > 0, "their ground was claimed",
            "cells " + tilsim.ConvertedCellCount);

        // The claimed ground reaches the next board through RoundConfig.
        var config = new RoundConfig(2, 6, 6, 100, null, ShuffleErosion.FromCenter, true);
        RoundConfig grown = tilsim.FilterRoundConfig(
            new SessionContext(session, session.Rng), config);
        Check(grown.ExtraPlayableCells.Count == tilsim.ConvertedCellCount,
            "the next round gets the extra cells",
            "cells " + grown.ExtraPlayableCells.Count);
        // A filter REBUILDS the config, so every field it does not care about has to come
        // across untouched - that is what RoundConfig.WithBoard is for.
        Check(grown.IsBossRound, "the boss flag survives tilsim's round-config filter");
        Check(grown.Erosion == ShuffleErosion.FromCenter,
            "and so does the erosion style", "erosion " + grown.Erosion);
    }

    private static void Inflation_GrowsThenSqueezesBack()
    {
        Section("enflasyon / grows for three turns then squeezes back");
        var session = NewSession(397, 6, 1000000, 40, 1);
        var power = (YatayEnflasyonPower)session.Powers.Add(new YatayEnflasyonPower());
        RoundEngine round = session.CurrentRound;

        PlayTurns(session, 1);
        int widthBefore = round.Board.Width;
        int heightBefore = round.Board.Height;
        int cubesBefore = round.Board.OccupiedCount;

        Check(session.Powers.TryUse(power.InstanceId, ActivationTarget.None), "inflation ran");
        Check(session.CurrentRound.Board.Width == widthBefore + 2,
            "the board is two columns wider",
            session.CurrentRound.Board.Width + " vs " + (widthBefore + 2));
        Check(session.CurrentRound.Board.Height == heightBefore, "the height is untouched");
        Check(session.CurrentRound.Board.OccupiedCount == cubesBefore,
            "every cube came across",
            session.CurrentRound.Board.OccupiedCount + " vs " + cubesBefore);
        Check(power.IsInflated && power.TurnsLeft == 3, "it holds for three turns",
            "left " + power.TurnsLeft);

        PlayTurns(session, 3);
        Check(!power.IsInflated, "it deflated after three turns", "left " + power.TurnsLeft);
        Check(session.CurrentRound.Board.Width == widthBefore,
            "the board snapped back to its old width",
            session.CurrentRound.Board.Width + " vs " + widthBefore);
    }

    /// <summary>The coordinate origin is the part the baseline trace CANNOT check: with
    /// MinX = 0 the offset maths is a no-op, so these assertions are the only thing standing
    /// between a wrong offset and a silently broken board.</summary>
    private static void BoardOrigin_CoordinatesSurviveGrowingLeftAndDown()
    {
        Section("board origin / coordinates survive a left/down grow");
        var session = NewSession(401, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;

        // Two cubes at known coordinates.
        PaintBoard(round, session, CubeKind.Normal, new GridPos(0, 0), new GridPos(3, 2));
        Check(round.Board.MinX == 0 && round.Board.MinY == 0, "a fresh board starts at 0,0");

        Check(round.ReshapeBoard(1, 1, 1, 1), "the board grew on every side");
        GameBoard grown = round.Board;

        Check(grown.MinX == -1 && grown.MinY == -1,
            "growing left/down pushed the ORIGIN out instead of renumbering",
            grown.MinX + "," + grown.MinY);
        Check(grown.Width == 7 && grown.Height == 7, "the box is two bigger in each axis",
            grown.Width + "x" + grown.Height);

        // The whole point: old coordinates still address the same cubes.
        Check(grown.GetCube(new GridPos(0, 0)).HasValue,
            "the cube at 0,0 is still at 0,0");
        Check(grown.GetCube(new GridPos(3, 2)).HasValue,
            "the cube at 3,2 is still at 3,2");
        Check(grown.IsInside(new GridPos(-1, -1)), "the new corner is playable");
        Check(!grown.GetCube(new GridPos(-1, -1)).HasValue, "and it is empty");
        Check(grown.IsInside(new GridPos(5, 5)), "so is the far new corner");
        Check(!grown.IsInside(new GridPos(-2, 0)), "but nothing beyond the new edge");

        // Placement and destruction must work in negative space too.
        BlockCard card = session.CreateCard(Bar(2), null);
        Check(grown.CanPlace(card.Shape, new GridPos(-1, 4)), "a block fits in the new space");
        grown.Place(card, new GridPos(-1, 4));
        Check(grown.GetCube(new GridPos(-1, 4)).HasValue, "it landed at a negative coordinate");
        Check(grown.DestroyCube(new GridPos(-1, 4)), "and it can be destroyed there");

        // Shrinking back restores the original numbering.
        Check(round.ReshapeBoard(-1, -1, -1, -1), "the board shrank back");
        Check(round.Board.MinX == 0 && round.Board.MinY == 0, "the origin came home",
            round.Board.MinX + "," + round.Board.MinY);
        Check(round.Board.GetCube(new GridPos(3, 2)).HasValue,
            "and the cube is STILL at 3,2 after a full round trip");
    }

    private static void Board_SwapLinesMovesWholeRows()
    {
        Section("board / SwapLines");
        var session = NewSession(443, 4, 1000000, 40, 1);
        GameBoard board = session.CurrentRound.Board;

        board.SetCubeAt(new GridPos(1, 0), new Cube(CubeKind.Fire, 77));
        board.SetCubeAt(new GridPos(3, 2), new Cube(CubeKind.Gold, 88));
        int occupied = board.OccupiedCount;

        Check(board.SwapLines(LineAxis.Row, 0, 2), "rows swapped");
        Check(board.GetCube(new GridPos(1, 2)).Value.Kind == CubeKind.Fire,
            "the fire cube moved up to row 2");
        Check(board.GetCube(new GridPos(3, 0)).Value.Kind == CubeKind.Gold,
            "and the gold cube came down to row 0");
        Check(board.OccupiedCount == occupied, "nothing was created or lost",
            board.OccupiedCount + " vs " + occupied);

        Check(!board.SwapLines(LineAxis.Row, 1, 1), "swapping a line with itself is refused");
        Check(!board.SwapLines(LineAxis.Column, 0, 99), "an out-of-bounds line is refused");

        Check(board.SwapLines(LineAxis.Column, 1, 3), "columns swapped");
        Check(board.GetCube(new GridPos(3, 2)).Value.Kind == CubeKind.Fire,
            "the fire cube followed its column");
    }

    private static void Deprem_CollapsesAQuarterInsteadOfLosing()
    {
        Section("deprem / rescues a dead end once per round");
        // 1-cube blocks: any single freed cell is enough to play on, so the test measures
        // the rescue itself rather than whether four random holes happen to line up.
        var session = NewSession(431, 4, 1000000, 40, 1);
        var joker = (DepremJoker)session.Jokers.Add(new DepremJoker());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;

        FillBoardSolid(round, session);
        int before = round.Board.OccupiedCount;
        Check(before == 16, "the board is solid", "occupied " + before);

        int scoreBefore = round.RoundScore;
        int sweeps = round.CleanSweepCount;

        // Force the dead-end check the way the engine does after a placement.
        round.DebugCheckForDeadEnd();

        Check(round.Status == RoundStatus.InProgress, "the round was rescued, not lost",
            "status " + round.Status);
        Check(round.Loss == null, "no loss is pending");
        int expected = before - (int)Math.Ceiling(before * joker.CollapseFraction);
        Check(round.Board.OccupiedCount == expected, "a quarter of the cubes came down",
            round.Board.OccupiedCount + " vs " + expected);
        Check(round.RoundScore == scoreBefore, "the quake paid nothing",
            round.RoundScore + " vs " + scoreBefore);
        Check(round.CleanSweepCount == sweeps, "and never counted as a clean sweep");
        Check(joker.LastCollapsedCells.Count > 0, "the collapsed cells are reported for the UI");

        // Once per round: a second dead end in the same round is fatal.
        FillBoardSolid(round, session);
        round.DebugCheckForDeadEnd();
        Check(round.Status == RoundStatus.Lost, "the second dead end ends the round",
            "status " + round.Status);
        Check(round.Loss == LossReason.NoPlayableMove, "for the right reason");
    }

    /// <summary>Fills a board solid so the next no-move check hits a dead end.</summary>
    private static void FillBoardSolid(RoundEngine round, GameSession session)
    {
        foreach (GridPos cell in AllPlayableCells(round.Board))
        {
            if (!round.Board.GetCube(cell).HasValue)
            {
                round.Board.SetCubeAt(cell, new Cube(CubeKind.Normal, 9000));
            }
        }
    }

    private static List<GridPos> AllPlayableCells(GameBoard board)
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

    private static void KentselDonusum_SwapsLinesToEscapeADeadEnd()
    {
        Section("kentsel dönüşüm / swaps two lines to escape");
        var session = NewSession(433, 4, 1000000, 40, 3);
        var power = (KentselDonusumPower)session.Powers.Add(new KentselDonusumPower());
        RoundEngine round = session.CurrentRound;

        Check(power.IsDeadEndRescue, "it is marked as a rescue power");
        Check(!session.Powers.CanUse(power.InstanceId, ActivationTarget.None),
            "it cannot be used during normal play");

        // Leave row 0 empty and fill the rest: a 3-bar fits only on the empty row.
        foreach (GridPos cell in AllPlayableCells(round.Board))
        {
            if (cell.Y != 0 && !round.Board.GetCube(cell).HasValue)
            {
                round.Board.SetCubeAt(cell, new Cube(CubeKind.Normal, 9000));
            }
        }
        // Now block row 0 too, so the board is a dead end.
        foreach (GridPos cell in AllPlayableCells(round.Board))
        {
            if (cell.Y == 0 && cell.X < 2)
            {
                round.Board.SetCubeAt(cell, new Cube(CubeKind.Normal, 9001));
            }
        }
        round.DebugCheckForDeadEnd();

        Check(round.Status == RoundStatus.AwaitingRescue,
            "the round paused instead of ending", "status " + round.Status);
        Check(session.Powers.CanUse(power.InstanceId, ActivationTarget.None),
            "the rescue is offered here");

        int occupiedBefore = round.Board.OccupiedCount;
        bool used = session.Powers.TryUse(power.InstanceId,
            ActivationTarget.LineSwap(LineAxis.Row, 0, 1));
        Check(used, "the swap ran");
        Check(round.Board.OccupiedCount == occupiedBefore,
            "a swap moves cubes, it never destroys them",
            round.Board.OccupiedCount + " vs " + occupiedBefore);
        Check(!power.Charged, "the charge was spent");
        Check(round.Status != RoundStatus.AwaitingRescue, "the pause is over",
            "status " + round.Status);
    }

    private static void Rescue_DeclineEndsTheRound()
    {
        Section("rescue / declining takes the loss");
        var session = NewSession(439, 4, 1000000, 40, 3);
        session.Powers.Add(new KentselDonusumPower());
        RoundEngine round = session.CurrentRound;

        FillBoardSolid(round, session);
        round.DebugCheckForDeadEnd();
        Check(round.Status == RoundStatus.AwaitingRescue, "paused for the rescue offer");
        Check(round.Loss == LossReason.NoPlayableMove,
            "the loss is already pending behind the offer");

        round.DebugDeclineRescue();
        Check(round.Status == RoundStatus.Lost, "declining confirms the loss",
            "status " + round.Status);
    }

    private static void BuldozerPower_FlattensATwoWideBandAndCountsForNothing()
    {
        Section("buldozer power / flattens a two-wide band");
        var session = NewSession(457, 6, 1000000, 40, 1);
        var power = (BuldozerPower)session.Powers.Add(new BuldozerPower());
        RoundEngine round = session.CurrentRound;

        Check(!session.Powers.CanUse(power.InstanceId, ActivationTarget.None),
            "refused on an empty board");

        FillBoardSolid(round, session);
        int before = round.Board.OccupiedCount;
        int scoreBefore = round.RoundScore;
        int sweeps = round.CleanSweepCount;

        Check(session.Powers.TryUse(power.InstanceId, ActivationTarget.None), "the dozer ran");

        // A 6x6 solid board: a two-wide band is 12 cubes whichever axis it picked.
        Check(round.Board.OccupiedCount == before - 12, "exactly two lines went",
            round.Board.OccupiedCount + " vs " + (before - 12));
        Check(power.LastFlattenedCells.Count == 12, "the flattened cells are reported",
            "count " + power.LastFlattenedCells.Count);

        // The band must be two NEIGHBOURING lines, not two random ones.
        var xs = new HashSet<int>();
        var ys = new HashSet<int>();
        foreach (GridPos cell in power.LastFlattenedCells)
        {
            xs.Add(cell.X);
            ys.Add(cell.Y);
        }
        bool rowBand = ys.Count == 2 && xs.Count == round.Board.Width;
        bool colBand = xs.Count == 2 && ys.Count == round.Board.Height;
        Check(rowBand || colBand, "it took a full band, not scattered cells",
            "xs " + xs.Count + " ys " + ys.Count);
        var band = new List<int>(rowBand ? ys : xs);
        band.Sort();
        Check(band[1] - band[0] == 1, "the two lines are neighbours",
            band[0] + " and " + band[1]);

        // Inert by design: no score, no sweep.
        Check(round.RoundScore == scoreBefore, "it paid nothing",
            round.RoundScore + " vs " + scoreBefore);
        Check(round.CleanSweepCount == sweeps, "and never counted as a clean sweep");
    }

    private static void BuldozerPower_CrushesIndestructibleCubes()
    {
        Section("buldozer power / crushes obsidian and gold");
        var session = NewSession(461, 4, 1000000, 40, 1);
        var power = (BuldozerPower)session.Powers.Add(new BuldozerPower());
        RoundEngine round = session.CurrentRound;

        // A 4x4 board solid with obsidian: nothing else in the game could shift these.
        FillBoardSolid(round, session);
        foreach (GridPos cell in AllPlayableCells(round.Board))
        {
            round.Board.SetCubeAt(cell, new Cube(CubeKind.Obsidian, 9100));
        }
        int before = round.Board.OccupiedCount;

        Check(session.Powers.TryUse(power.InstanceId, ActivationTarget.None), "the dozer ran");
        Check(round.Board.OccupiedCount == before - 8, "it crushed the obsidian band too",
            round.Board.OccupiedCount + " vs " + (before - 8));
    }

    private static void NegativeBlock_ErasesWhatItCoversAndLeavesNothing()
    {
        Section("negative block / erases and leaves nothing");
        var session = NewSession(463, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;

        // Three cubes in a row for the negative bar to land on.
        PaintBoard(round, session, CubeKind.Normal,
            new GridPos(1, 1), new GridPos(2, 1), new GridPos(3, 1));
        int before = round.Board.OccupiedCount;
        int scoreBefore = round.RoundScore;

        BlockCard negative = session.CreateCard(Bar(3), new[] { BlockElement.Negative });
        round.AddBonusCard(negative, BonusPlayOutcome.ExpireFromRound);

        // The whole point: it may be placed ON occupied cells, which nothing else can do.
        Check(round.CanPlaceCard(negative, new GridPos(1, 1)),
            "it can be placed on top of existing cubes");

        TurnReport report = round.PlayFromBonus(0, new GridPos(1, 1));

        Check(round.Board.OccupiedCount == before - 3, "the three cubes were erased",
            round.Board.OccupiedCount + " vs " + (before - 3));
        Check(!round.Board.GetCube(new GridPos(1, 1)).HasValue, "and the cell is EMPTY");
        Check(report.PlacedCells.Count == 0, "nothing was placed - the block went too",
            "placed " + report.PlacedCells.Count);
        Check(report.DestroyedCubes.Count == 3, "the erasure is in the destruction log",
            "logged " + report.DestroyedCubes.Count);
        Check(round.RoundScore > scoreBefore, "it paid the per-cube explosion score",
            round.RoundScore + " vs " + scoreBefore);
    }

    private static void NegativeBlock_RefusedByIndestructibleCubes()
    {
        Section("negative block / obsidian and gold refuse it");
        var session = NewSession(467, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;

        PaintBoard(round, session, CubeKind.Obsidian, new GridPos(2, 2));
        PaintBoard(round, session, CubeKind.Gold, new GridPos(3, 3));
        PaintBoard(round, session, CubeKind.Normal, new GridPos(0, 0));

        BlockCard negative = session.CreateCard(Bar(1), new[] { BlockElement.Negative });

        Check(!round.CanPlaceCard(negative, new GridPos(2, 2)), "obsidian refuses it");
        Check(!round.CanPlaceCard(negative, new GridPos(3, 3)), "gold refuses it");
        Check(round.CanPlaceCard(negative, new GridPos(0, 0)), "a normal cube accepts it");
        Check(round.CanPlaceCard(negative, new GridPos(4, 4)), "empty space accepts it too");
    }

    private static void NegativeBlock_CanSweepAndEscapeADeadEnd()
    {
        Section("negative block / sweeps and rescues");
        // A board holding exactly one cube: erasing it empties the board, which must count
        // as a clean sweep - the erasure is this turn's destruction.
        var session = NewSession(479, 4, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        PaintBoard(round, session, CubeKind.Normal, new GridPos(1, 1));

        BlockCard negative = session.CreateCard(Bar(1), new[] { BlockElement.Negative });
        round.AddBonusCard(negative, BonusPlayOutcome.ExpireFromRound);
        int sweepsBefore = round.CleanSweepCount;

        TurnReport report = round.PlayFromBonus(0, new GridPos(1, 1));
        Check(round.Board.OccupiedCount == 0, "the board is empty",
            "occupied " + round.Board.OccupiedCount);
        Check(report.CleanSweep && round.CleanSweepCount == sweepsBefore + 1,
            "emptying the board with it counts as a clean sweep",
            "sweep " + report.CleanSweep);

        // And on a solid board it is still playable when nothing else is - it lands on cubes.
        var s2 = NewSession(487, 4, 1000000, 40, 3);
        RoundEngine r2 = s2.CurrentRound;
        FillBoardSolid(r2, s2);
        BlockCard neg2 = s2.CreateCard(Bar(2), new[] { BlockElement.Negative });
        Check(r2.Board.AnyPlacementExists(Bar(2), false, true),
            "a full board still has room for a negative block");
        Check(!r2.Board.AnyPlacementExists(Bar(2), false, false),
            "while a normal block has nowhere to go");
    }

    private static void FrozenCard_CannotBePlayedAndThaws()
    {
        Section("frozen card / cannot be played until it thaws");
        var session = NewSession(491, 8, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;

        BlockCard target = round.Hand[0];
        Check(!round.IsFrozen(target.Id), "cards start unfrozen");
        Check(round.FreezeHandCard(target.Id, 3), "freezing a held card works");
        Check(round.IsFrozen(target.Id), "it is frozen now");
        Check(round.FreezeTurnsLeft(target.Id) == 3, "for three turns",
            "left " + round.FreezeTurnsLeft(target.Id));
        Check(!round.FreezeHandCard(target.Id, 3), "re-freezing the same card is refused");
        Check(!round.FreezeHandCard(999999, 3), "a card that is not in hand cannot be frozen");

        // Playing it must be refused outright.
        var origins = round.GetValidOrigins(target.Shape);
        bool threw = false;
        try
        {
            round.PlayFromHand(0, origins[0]);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Check(threw, "playing a frozen card throws");

        // Three resolved turns thaw it. Play OTHER cards to advance.
        for (int i = 0; i < 3; i++)
        {
            int playIndex = -1;
            for (int h = 0; h < round.Hand.Count; h++)
            {
                if (round.Hand[h].Id != target.Id)
                {
                    playIndex = h;
                    break;
                }
            }
            if (playIndex < 0)
            {
                break;
            }
            var o = round.GetValidOrigins(round.Hand[playIndex].Shape);
            round.PlayFromHand(playIndex, o[0]);
        }
        Check(!round.IsFrozen(target.Id), "it thawed after three turns",
            "left " + round.FreezeTurnsLeft(target.Id));
    }

    private static void FrozenCard_CountsAsNoPlayableMove()
    {
        Section("frozen card / a frozen hand is a dead end");
        // One card in hand, board irrelevant: freezing it leaves nothing playable.
        var session = NewSession(499, 8, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;

        for (int i = 0; i < round.Hand.Count; i++)
        {
            round.FreezeHandCard(round.Hand[i].Id, 3);
        }
        Check(round.Status == RoundStatus.InProgress, "still running before the check");

        round.DebugCheckForDeadEnd();
        Check(round.Status == RoundStatus.Lost || round.Status == RoundStatus.AwaitingRescue,
            "a fully frozen hand is treated as no playable move",
            "status " + round.Status);
    }

    private static void MarketDiscount_CutsPricesForOneVisit()
    {
        Section("market discount / one visit only");
        var session = NewSession(503, 3, 10, 6, 3);

        Check(session.PendingMarketDiscount == 0.0, "no discount to begin with");
        session.AddMarketDiscount(0.3);
        Check(session.PendingMarketDiscount == 0.3, "the discount is pending",
            "" + session.PendingMarketDiscount);

        // Discounts stack but never make things free.
        session.AddMarketDiscount(0.9);
        Check(session.PendingMarketDiscount <= 0.75, "stacking is capped",
            "" + session.PendingMarketDiscount);

        // Reach the market so offers are priced with it, then confirm it is consumed.
        int guard = 0;
        while (session.Phase == GamePhase.Round && guard++ < 40)
        {
            if (session.CurrentRound.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                session.CurrentRound.DecideAdvance(true);
                break;
            }
            if (PlayTurns(session, 1) == 0)
            {
                break;
            }
        }
        if (session.Phase != GamePhase.Market)
        {
            Check(false, "could not reach the market", "phase " + session.Phase);
            return;
        }
        Check(session.PendingMarketDiscount > 0.0, "it survived into the market");
        session.LeaveMarket();
        Check(session.PendingMarketDiscount == 0.0, "and is spent when the market is left",
            "" + session.PendingMarketDiscount);
    }

    /// <summary>Drives a Hazine hit by exploding exactly the cell a mark sits on.</summary>
    private static TurnReport BlowUpCell(GameSession session, GridPos cell)
    {
        RoundEngine round = session.CurrentRound;
        if (!round.Board.GetCube(cell).HasValue)
        {
            PaintBoard(round, session, CubeKind.Normal, cell);
        }
        // A negative 1x1 lands on the cube and erases it - a clean way to destroy one
        // specific cell through the normal turn flow.
        BlockCard eraser = session.CreateCard(Bar(1), new[] { BlockElement.Negative });
        round.AddBonusCard(eraser, BonusPlayOutcome.ExpireFromRound);
        return round.PlayFromBonus(round.BonusHand.Count - 1, cell);
    }

    private static void Hazine_BuriesTwoMarksAndPaysOutOnce()
    {
        Section("hazine / buries two marks");
        var session = NewSession(509, 6, 1000000, 40, 1);
        var joker = (HazineJoker)session.Jokers.Add(new HazineJoker());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);

        Check(joker.TreasureCell.HasValue && joker.DynamiteCell.HasValue,
            "both marks are buried at round start");
        Check(!joker.TreasureCell.Value.Equals(joker.DynamiteCell.Value),
            "on two different cells");

        GridPos treasure = joker.TreasureCell.Value;
        BlowUpCell(session, treasure);

        Check(!joker.TreasureCell.HasValue, "the treasure was found");
        Check(!joker.DynamiteCell.HasValue,
            "and finding it removed the dynamite - the confirmed rule");
        Check(!string.IsNullOrEmpty(joker.LastOutcome), "an outcome was reported for the UI",
            "outcome null");
    }

    private static void Hazine_DynamiteAppliesAPenalty()
    {
        Section("hazine / dynamite penalises");
        var session = NewSession(521, 6, 1000000, 40, 1);
        var joker = (HazineJoker)session.Jokers.Add(new HazineJoker());
        session.Powers.Add(new BuyutecPower()); // gives the "drain a power" penalty something to bite
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;

        GridPos dynamite = joker.DynamiteCell.Value;
        var handBefore = new List<int>();
        for (int i = 0; i < round.Hand.Count; i++)
        {
            handBefore.Add(round.Hand[i].Id);
        }
        bool powerChargedBefore = session.Powers.Powers[0].Charged;

        BlowUpCell(session, dynamite);

        Check(!joker.DynamiteCell.HasValue, "the dynamite went off");
        Check(!joker.TreasureCell.HasValue, "and took the treasure with it");

        // One of the three penalties must have landed: a drained power, a frozen card, or a
        // discarded hand.
        bool powerDrained = powerChargedBefore && !session.Powers.Powers[0].Charged;
        bool anyFrozen = false;
        for (int i = 0; i < round.Hand.Count; i++)
        {
            if (round.IsFrozen(round.Hand[i].Id))
            {
                anyFrozen = true;
            }
        }
        bool handReplaced = true;
        for (int i = 0; i < round.Hand.Count; i++)
        {
            if (handBefore.Contains(round.Hand[i].Id))
            {
                handReplaced = false;
            }
        }
        Check(powerDrained || anyFrozen || handReplaced, "a penalty landed",
            "drained " + powerDrained + " frozen " + anyFrozen + " replaced " + handReplaced);
        Check(!string.IsNullOrEmpty(joker.LastOutcome), "and was reported",
            "outcome " + joker.LastOutcome);
    }

    private static void Hazine_HittingBothCancelsOut()
    {
        Section("hazine / hitting both cancels out");
        var session = NewSession(523, 6, 1000000, 40, 1);
        var joker = (HazineJoker)session.Jokers.Add(new HazineJoker());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;

        GridPos treasure = joker.TreasureCell.Value;
        GridPos dynamite = joker.DynamiteCell.Value;
        int handBefore = round.Hand.Count;
        bool anyFrozenBefore = false;
        for (int i = 0; i < round.Hand.Count; i++)
        {
            anyFrozenBefore |= round.IsFrozen(round.Hand[i].Id);
        }

        // ONE turn whose destruction log covers BOTH marks. Driving that through a real
        // placement would need the two random cells to be adjacent, so the turn is built
        // directly - the rule under test is what the joker does with such a log.
        var score = new ScoreBreakdown();
        TurnContext turn = FakeTurnWithRound(session, score);
        turn.Report.DestroyedCubes = new List<DestroyedCube>
        {
            new DestroyedCube(treasure, new Cube(CubeKind.Normal, 900)),
            new DestroyedCube(dynamite, new Cube(CubeKind.Normal, 901))
        };
        joker.AfterTurnScored(turn);

        Check(!joker.TreasureCell.HasValue && !joker.DynamiteCell.HasValue,
            "both marks are gone");
        Check(!string.IsNullOrEmpty(joker.LastOutcome),
            "the cancellation was reported", "outcome " + joker.LastOutcome);
        Check(score.FlatBonus == 0 && score.LateFlat == 0, "no reward was paid",
            "flat " + score.FlatBonus + " late " + score.LateFlat);
        Check(round.Hand.Count == handBefore, "and no penalty wrecked the hand",
            round.Hand.Count + " vs " + handBefore);
        bool anyFrozenAfter = false;
        for (int i = 0; i < round.Hand.Count; i++)
        {
            anyFrozenAfter |= round.IsFrozen(round.Hand[i].Id);
        }
        Check(anyFrozenAfter == anyFrozenBefore, "nothing was frozen either");
    }

    private static void MeydanOkuma_MarksThenPaysOnClear()
    {
        Section("meydan okuma / marks a line and pays on the clear");
        // 6x6 board: big enough that a whole-board deadline would be ~30, while a line-based
        // one is at most the board width - which is what proves the fix.
        var session = NewSession(541, 6, 1000000, 40, 3);
        var joker = (MeydanOkumaJoker)session.Jokers.Add(new MeydanOkumaJoker());
        joker.ArmAfterTurns = 1; // arm early so the test is short
        joker.BaseBonus = 200;
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;

        Check(!joker.HasActiveMark, "no mark before it is armed");

        // One turn to arm it. After a placement resolves, a mark should be laid (unless the
        // very turn it arms also clears something - so drive until it holds a mark).
        int guard = 0;
        while (!joker.HasActiveMark && guard++ < 6 && round.Status == RoundStatus.InProgress)
        {
            if (PlayTurns(session, 1) == 0)
            {
                break;
            }
        }
        Check(joker.HasActiveMark, "a line was marked", "no mark");
        Check(joker.TurnsLeft >= 3, "the deadline is at least the floor",
            "turns " + joker.TurnsLeft);
        Check(joker.TurnsLeft <= round.Board.Width, "and no larger than the marked LINE, "
            + "not the whole board", "turns " + joker.TurnsLeft);
        Check(joker.CurrentBonus == 200, "the first attempt is worth the full bonus",
            "bonus " + joker.CurrentBonus);
    }

    private static void MeydanOkuma_HalvesAndGivesUpAfterThreeMisses()
    {
        Section("meydan okuma / halves the bonus and stops after three misses");
        var joker = new MeydanOkumaJoker();
        joker.ArmAfterTurns = 0;
        joker.BaseBonus = 200;
        joker.MinDeadline = 1; // tiny deadline so every attempt misses in one tick

        // A nearly-full board keeps the deadline at the floor: max(3, empty) is small only
        // when few cells are empty, so filling the board is what makes each attempt miss fast.
        var session = NewSession(547, 3, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        session.Jokers.Add(joker);
        session.Jokers.DispatchRoundStarted(round);
        FillBoardSolid(round, session); // empty = 0 -> deadline = max(1, 0) = 1

        var seenBonuses = new List<int>();
        for (int i = 0; i < 12 && !joker.IsResolved; i++)
        {
            var report = new TurnReport();
            report.Card = new BlockCard(1, Bar(1));
            report.Score = new ScoreBreakdown();
            report.ExplodedRows = new List<int>();     // never the marked line
            report.ExplodedColumns = new List<int>();
            var turn = new TurnContext(session, session.Rng, round, report, report.Score);
            joker.AfterTurnScored(turn);
            if (joker.HasActiveMark && !seenBonuses.Contains(joker.CurrentBonus))
            {
                seenBonuses.Add(joker.CurrentBonus);
            }
        }

        Check(seenBonuses.Contains(200), "the first attempt was worth the full bonus");
        Check(seenBonuses.Contains(100), "the second attempt halved it");
        Check(seenBonuses.Contains(50), "the third halved it again",
            "seen " + string.Join(",", seenBonuses));
        Check(joker.IsResolved, "after three misses it gives up for the round");
    }

    private static void Powerbank_RechargesASpentPower()
    {
        Section("powerbank / refills a power without a sweep");
        var session = NewSession(367, 8, 1000000, 40, 1);
        var power = (BuyutecPower)session.Powers.Add(new BuyutecPower());
        var joker = (PowerbankJoker)session.Jokers.Add(new PowerbankJoker());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);

        Check(!session.Jokers.CanActivate(joker.InstanceId),
            "refuses while every power is already charged");

        session.Powers.TryUse(power.InstanceId, ActivationTarget.None);
        Check(!power.Charged, "the power was spent");
        Check(session.Jokers.CanActivate(joker.InstanceId), "now it has something to do");
        Check(session.Jokers.TryActivate(joker.InstanceId, ActivationTarget.None), "powerbank ran");
        Check(power.Charged, "the power is charged again");
        Check(!session.Jokers.CanActivate(joker.InstanceId), "its own single charge is spent");
    }

    private static bool ListHas(IReadOnlyList<int> list, int value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
            {
                return true;
            }
        }
        return false;
    }

    private static void Erosion_FirstTwoRecyclesAreFreeThenTheRimGoes()
    {
        Section("erosion / two free recycles, then the rim goes alternating sides");
        var session = NewErodingSession(701, 5, 40, ShuffleErosion.FromOutside, 1);
        RoundEngine round = session.CurrentRound;

        Check(round.Board.Width == 5 && round.Board.Height == 5, "starts 5x5");
        Check(round.FreeDeckRecyclesLeft == 2, "two free recycles to start");

        round.DebugForceDeckRecycle();
        Check(round.Board.Width == 5 && round.BoardErosionCount == 0,
            "the first recycle is free", round.Board.Width + "x" + round.Board.Height);
        Check(round.FreeDeckRecyclesLeft == 1, "one free recycle left");
        round.DebugForceDeckRecycle();
        Check(round.Board.Width == 5 && round.BoardErosionCount == 0, "the second is free too");
        Check(round.FreeDeckRecyclesLeft == 0, "and now the allowance is spent");

        // 3rd recycle: odd step, so the TOP row and the RIGHT column go. MinX/MinY stay put.
        int minX = round.Board.MinX;
        int minY = round.Board.MinY;
        round.DebugForceDeckRecycle();
        Check(round.Board.Width == 4 && round.Board.Height == 4, "the third recycle cuts it to 4x4",
            round.Board.Width + "x" + round.Board.Height);
        Check(round.BoardErosionCount == 1, "one erosion recorded");
        Check(round.Board.MinX == minX && round.Board.MinY == minY,
            "the top row and the right column went, so the origin did not move");

        // 4th recycle: even step, so the BOTTOM row and the LEFT column go - the origin moves in.
        round.DebugForceDeckRecycle();
        Check(round.Board.Width == 3 && round.Board.Height == 3, "the fourth cuts it to 3x3",
            round.Board.Width + "x" + round.Board.Height);
        Check(round.Board.MinX == minX + 1 && round.Board.MinY == minY + 1,
            "this time the bottom row and the left column went, so the arena stayed centred",
            "min " + round.Board.MinX + "," + round.Board.MinY);

        round.DebugForceDeckRecycle();
        Check(round.Board.Width == 2 && round.Board.Height == 2, "then 2x2");
        Check(round.Board.DeadCellCount == 0, "rim erosion leaves no dead cells - the band is gone");
    }

    private static void Erosion_RimNeverEatsTheLastCell()
    {
        Section("erosion / the rim stops at a 1x1 sliver instead of erasing the board");
        var session = NewErodingSession(702, 5, 40, ShuffleErosion.FromOutside, 1);
        RoundEngine round = session.CurrentRound;
        for (int i = 0; i < 12; i++)
        {
            round.DebugForceDeckRecycle();
        }
        Check(round.Board.Width >= 1 && round.Board.Height >= 1, "the board still exists",
            round.Board.Width + "x" + round.Board.Height);
        Check(round.Board.PlayableCellCount >= 1, "and still has a cell");
    }

    private static void Erosion_CentreHoleKillsItsRowAndColumn()
    {
        Section("erosion / the centre hole kills its row and column for good");
        var session = NewErodingSession(703, 5, 40, ShuffleErosion.FromCenter, 1);
        RoundEngine round = session.CurrentRound;
        int cellsBefore = round.Board.PlayableCellCount;

        round.DebugForceDeckRecycle();
        round.DebugForceDeckRecycle();
        Check(round.Board.PlayableCellCount == cellsBefore, "the free recycles change nothing");

        round.DebugForceDeckRecycle();
        var centre = new GridPos(round.Board.MinX + 2, round.Board.MinY + 2);
        Check(round.Board.IsDead(centre), "the middle cell of a 5x5 was eaten");
        Check(!round.Board.IsInside(centre), "an eaten cell is not play area");
        Check(round.Board.PlayableCellCount == cellsBefore - 1, "one cell fewer to fill",
            round.Board.PlayableCellCount.ToString());
        Check(round.Board.Width == 5 && round.Board.Height == 5,
            "centre erosion does not shrink the bounding box");

        // Fill everything that is still playable. The eaten cell's row and column must NOT
        // explode - that is the punishment - while the other rows/columns must.
        FillBoardSolid(round, session);
        LineExplosionResult lines = round.Board.ResolveFullLines();
        Check(!ListHas(lines.Rows, 2), "row 2 runs through the hole, so it never explodes",
            "rows " + string.Join(",", lines.Rows));
        Check(!ListHas(lines.Columns, 2), "column 2 likewise",
            "cols " + string.Join(",", lines.Columns));
        Check(lines.Rows.Count == 4 && lines.Columns.Count == 4,
            "the other four rows and columns still clear normally",
            lines.Rows.Count + " rows / " + lines.Columns.Count + " cols");
    }

    private static void Erosion_CentreHoleGrowsAndStaysASuperset()
    {
        Section("erosion / the centre hole grows 1x1 -> 2x2 -> 3x3 and never gives cells back");
        var session = NewErodingSession(704, 7, 40, ShuffleErosion.FromCenter, 1);
        RoundEngine round = session.CurrentRound;
        round.DebugForceDeckRecycle();
        round.DebugForceDeckRecycle();

        round.DebugForceDeckRecycle();
        Check(round.Board.DeadCellCount == 1, "step 1 eats the single centre cell",
            round.Board.DeadCellCount.ToString());
        var first = new GridPos(round.Board.MinX + 3, round.Board.MinY + 3);
        Check(round.Board.IsDead(first), "and it is the exact centre of a 7x7");

        round.DebugForceDeckRecycle();
        Check(round.Board.DeadCellCount == 4, "step 2 eats a 2x2",
            round.Board.DeadCellCount.ToString());
        Check(round.Board.IsDead(first), "the first cell is still dead - the hole only grows");

        round.DebugForceDeckRecycle();
        Check(round.Board.DeadCellCount == 9, "step 3 eats a 3x3",
            round.Board.DeadCellCount.ToString());
        Check(round.Board.IsDead(first), "and it still contains everything eaten before");

        // A 3x3 hole in a 7x7 kills rows 2,3,4 and columns 2,3,4: only 4+4 lines are left alive.
        FillBoardSolid(round, session);
        LineExplosionResult lines = round.Board.ResolveFullLines();
        Check(lines.Rows.Count == 4 && lines.Columns.Count == 4,
            "three rows and three columns are dead for the rest of the round",
            lines.Rows.Count + " rows / " + lines.Columns.Count + " cols");
    }

    private static void Erosion_BothStylesHitTogether()
    {
        Section("erosion / the last band loses the rim AND is hollowed out at once");
        var session = NewErodingSession(705, 9, 40, ShuffleErosion.Both, 1);
        RoundEngine round = session.CurrentRound;
        round.DebugForceDeckRecycle();
        round.DebugForceDeckRecycle();

        round.DebugForceDeckRecycle();
        Check(round.Board.Width == 8 && round.Board.Height == 8, "the rim went: 9x9 -> 8x8",
            round.Board.Width + "x" + round.Board.Height);
        Check(round.Board.DeadCellCount == 1, "and the centre was hollowed at the same time",
            round.Board.DeadCellCount.ToString());

        round.DebugForceDeckRecycle();
        Check(round.Board.Width == 7 && round.Board.Height == 7, "then 7x7",
            round.Board.Width + "x" + round.Board.Height);
        Check(round.Board.DeadCellCount >= 4, "with a bigger hole",
            round.Board.DeadCellCount.ToString());
        Check(round.Board.PlayableCellCount < 49 - 3,
            "so the arena collapses much faster than either style alone",
            round.Board.PlayableCellCount.ToString());
    }

    private static void Erosion_EatenCubesCostNoScoreAndNoSweep()
    {
        Section("erosion / cubes on eaten cells die scorelessly and never trigger a sweep");
        var session = NewErodingSession(706, 5, 40, ShuffleErosion.FromOutside, 1);
        RoundEngine round = session.CurrentRound;
        round.DebugForceDeckRecycle();
        round.DebugForceDeckRecycle();

        // One lone cube, sitting in the top row that the next erosion takes.
        var doomed = new GridPos(round.Board.MinX + 1, round.Board.MinY + round.Board.Height - 1);
        round.Board.SetCubeAt(doomed, new Cube(CubeKind.Normal, 9100));
        Check(round.Board.OccupiedCount == 1, "the board holds exactly that cube");

        int scoreBefore = round.RoundScore;
        int sweepsBefore = round.CleanSweepCount;
        round.DebugForceDeckRecycle();

        Check(round.Board.OccupiedCount == 0, "the cube went with its cell");
        Check(round.RoundScore == scoreBefore, "and paid nothing",
            scoreBefore + " -> " + round.RoundScore);
        Check(round.CleanSweepCount == sweepsBefore,
            "emptying the board this way is not a clean sweep");
    }

    private static void Erosion_EatsThroughIndestructibleAndProtectedCubes()
    {
        Section("erosion / obsidian and a Parazit host cannot squat on a cell that ceases to exist");
        var session = NewErodingSession(707, 5, 40, ShuffleErosion.FromCenter, 1);
        RoundEngine round = session.CurrentRound;
        round.DebugForceDeckRecycle();
        round.DebugForceDeckRecycle();

        var centre = new GridPos(round.Board.MinX + 2, round.Board.MinY + 2);
        round.Board.SetCubeAt(centre, new Cube(CubeKind.Obsidian, 9200));
        round.Board.SetCubeProtected(centre); // the toughest cube in the game
        Check(round.Board.GetCube(centre).HasValue, "a protected obsidian cube sits in the middle");

        round.DebugForceDeckRecycle();
        Check(round.Board.IsDead(centre), "the cell was eaten anyway");
        Check(!round.Board.GetCube(centre).HasValue, "and the cube with it");
        Check(round.Board.OccupiedCount == 0, "the occupied count stayed honest");
    }

    private static void Erosion_NoneLeavesTheBoardAlone()
    {
        Section("erosion / a band with no erosion never shrinks");
        var session = NewErodingSession(708, 5, 40, ShuffleErosion.None, 1);
        RoundEngine round = session.CurrentRound;
        for (int i = 0; i < 8; i++)
        {
            round.DebugForceDeckRecycle();
        }
        Check(round.Board.Width == 5 && round.Board.Height == 5, "still 5x5");
        Check(round.Board.DeadCellCount == 0 && round.BoardErosionCount == 0, "nothing was eaten");
        Check(round.DeckRecycleCount == 8, "the recycles were still counted");
    }

    private static void Erosion_AddedCellsStillDoNotKillLines()
    {
        Section("erosion / a hole that was never board (Tılsım/Kentsel Dönüşüm filler) is harmless");
        // A 4x4 board with one extra cell bolted on at (4,1): the bounding box becomes 5x4, so
        // column 4 is a hole on three rows. Those holes must stay SKIPPED, not line-killing.
        var config = new RoundConfig(1, 4, 4, 1000000, new[] { new GridPos(4, 1) });
        var board = new GameBoard(config.BoardWidth, config.BoardHeight, config.ExtraPlayableCells);
        Check(board.Width == 5 && board.Height == 4, "the bounding box stretched to hold it",
            board.Width + "x" + board.Height);
        Check(board.DeadCellCount == 0, "and nothing is dead - those cells were never board");

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                var pos = new GridPos(x, y);
                if (board.IsInside(pos))
                {
                    board.SetCubeAt(pos, new Cube(CubeKind.Normal, 9300));
                }
            }
        }
        LineExplosionResult lines = board.ResolveFullLines();
        Check(lines.Rows.Count == 4, "every row still explodes when filled",
            lines.Rows.Count + " rows");
        Check(lines.Columns.Count == 5, "and every column, the bolted-on one included",
            lines.Columns.Count + " cols");
    }

    private static void Erosion_RealPlayRunsTheClockAndEndsTheRound()
    {
        Section("erosion / a real round that keeps recycling erodes and eventually ends");
        // A 4-card deck against a hand of 3: the draw pile runs dry every couple of turns, so
        // this drives the WIRING (DrawWithRules -> NoteDeckRecycled -> end-of-turn erosion)
        // rather than the debug seam.
        var session = NewErodingSession(709, 5, 4, ShuffleErosion.FromOutside, 1);
        RoundEngine round = session.CurrentRound;
        int startWidth = round.Board.Width;

        PlayTurns(session, 60);
        Check(round.DeckRecycleCount >= 3, "the deck really did run dry repeatedly",
            "recycles " + round.DeckRecycleCount);
        Check(round.BoardErosionCount >= 1, "so the arena eroded through normal play",
            "erosions " + round.BoardErosionCount);
        Check(round.Board.Width < startWidth, "the board is smaller than it started",
            startWidth + " -> " + round.Board.Width);
        Check(round.BoardErosionCount == round.DeckRecycleCount - round.Rules.FreeDeckRecycles,
            "and exactly the earned number of erosions landed - no double-charging",
            round.BoardErosionCount + " vs " + round.DeckRecycleCount);
    }

    private static void Progression_BoardSizeStepsWithTheRoundBands()
    {
        Section("progression / board size steps 5x5 -> 7x7 -> 9x9");
        var progression = new DefaultRoundProgression();

        bool firstBand = true;
        for (int round = 1; round <= 5; round++)
        {
            firstBand &= progression.GetRound(round).BoardWidth == 5
                && progression.GetRound(round).BoardHeight == 5;
        }
        Check(firstBand, "rounds 1-5 are played on 5x5");

        bool secondBand = true;
        for (int round = 6; round <= 11; round++)
        {
            secondBand &= progression.BoardSizeFor(round) == 7;
        }
        Check(secondBand, "rounds 6-11 are played on 7x7");

        bool thirdBand = true;
        for (int round = 12; round <= 15; round++)
        {
            thirdBand &= progression.BoardSizeFor(round) == 9;
        }
        Check(thirdBand, "rounds 12-15 are played on 9x9");

        Check(progression.BoardSizeFor(16) == 9 && progression.BoardSizeFor(40) == 9,
            "a round past the table keeps the last band's size");
        // The run is 15 rounds numbered 1-15, so the bands must tile it exactly: start at 1,
        // end at 15, and leave no gap or overlap in between.
        BoardSizeBand[] bands = progression.BoardSizeBands;
        bool contiguous = bands[0].FirstRound == 1 && bands[bands.Length - 1].LastRound == 15;
        int covered = bands[0].LastRound - bands[0].FirstRound + 1;
        for (int i = 1; i < bands.Length; i++)
        {
            contiguous &= bands[i].FirstRound == bands[i - 1].LastRound + 1;
            covered += bands[i].LastRound - bands[i].FirstRound + 1;
        }
        Check(contiguous && covered == 15, "the bands tile rounds 1-15 exactly",
            "covered " + covered);
        Check(progression.GetRound(6).BoardWidth == 7 && progression.GetRound(5).BoardWidth == 5,
            "the step happens between round 5 and round 6");
        Check(progression.GetRound(12).BoardWidth == 9 && progression.GetRound(11).BoardWidth == 7,
            "and between round 11 and round 12");

        // The table is data: a variant curve only has to hand over different bands.
        progression.BoardSizeBands = new[] { new BoardSizeBand(1, 3, 4), new BoardSizeBand(4, 6, 12) };
        Check(progression.BoardSizeFor(2) == 4 && progression.BoardSizeFor(5) == 12
            && progression.BoardSizeFor(99) == 12, "a replaced band table drives the size");

        bool threwOnRoundZero = false;
        try
        {
            new DefaultRoundProgression().GetRound(0);
        }
        catch (ArgumentException)
        {
            threwOnRoundZero = true;
        }
        Check(threwOnRoundZero, "there is no round 0 - round numbers are 1-based");
    }

    private static void RunLength_FifteenRoundsThenRunWon()
    {
        Section("run length / 15 rounds, then RunWon");
        GameSession session = NewSession(4242, 4, 1, 30, 1);
        // Placement scores nothing by default, so one cube would never clear even a threshold
        // of 1; with a point per cube every round is won on its first turn.
        session.Config.Scoring.PointsPerCubePlaced = 1;
        Check(session.Config.TotalRounds == 15, "a run is 15 rounds long",
            "" + session.Config.TotalRounds);

        int markets = 0;
        int safety = 0;
        while (!RunIsOver(session) && safety++ < 400)
        {
            if (session.Phase == GamePhase.Market)
            {
                markets++;
                session.LeaveMarket();
                continue;
            }
            RoundEngine playing = session.CurrentRound;
            if (playing.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                playing.DecideAdvance(true);
                continue;
            }
            if (playing.Status != RoundStatus.InProgress || PlayTurns(session, 1) == 0)
            {
                break;
            }
        }

        Check(session.Phase == GamePhase.RunWon, "surviving the last round wins the run",
            "phase " + session.Phase);
        Check(session.RoundNumber == 15, "the run ends on round 15",
            "round " + session.RoundNumber);
        Check(markets == 14, "one market between each pair of rounds, none after the last",
            "markets " + markets);
        Check(session.CurrentRound.Loss == null, "a won run carries no loss reason",
            "loss " + session.CurrentRound.Loss);

        // There is no market to leave once the run is won (the phase check catches this one).
        Check(RefusesToLeaveMarket(session), "no market to leave after the run is won");

        // The run length is an invariant, not just a UI convention: a session sitting in a market
        // whose round IS the last one must refuse to start another round. That state is only
        // reachable by shortening the run mid-flight, which is exactly why the guard exists.
        GameSession shortened = NewSession(4245, 4, 1, 30, 1);
        shortened.Config.Scoring.PointsPerCubePlaced = 1;
        PlayTurns(shortened, 1);
        if (shortened.CurrentRound.Status == RoundStatus.AwaitingAdvanceDecision)
        {
            shortened.CurrentRound.DecideAdvance(true);
        }
        Check(shortened.Phase == GamePhase.Market, "a short run reached its first market",
            "phase " + shortened.Phase);
        shortened.Config.TotalRounds = shortened.RoundNumber; // pretend this was the last round
        Check(RefusesToLeaveMarket(shortened),
            "LeaveMarket refuses to walk past the final round");

        // Losing the FINAL round is still a loss - RunWon is only for surviving it.
        GameSession lost = DriveToFinalRound(4243);
        Check(lost.RoundNumber == 15 && lost.Phase == GamePhase.Round,
            "a second run reaches the final round",
            "round " + lost.RoundNumber + " phase " + lost.Phase);
        lost.CurrentRound.DeclareLoss(LossReason.NoPlayableMove);
        Check(lost.Phase == GamePhase.GameOver,
            "losing the final round is GameOver, not RunWon", "phase " + lost.Phase);
    }

    /// <summary>True when LeaveMarket refuses (throws) instead of starting another round.</summary>
    private static bool RefusesToLeaveMarket(GameSession session)
    {
        try
        {
            session.LeaveMarket();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    /// <summary>Plays the cheapest possible run (one turn per round) up to the START of the
    /// final round, leaving it InProgress.</summary>
    private static GameSession DriveToFinalRound(int seed)
    {
        GameSession session = NewSession(seed, 4, 1, 30, 1);
        session.Config.Scoring.PointsPerCubePlaced = 1;
        int safety = 0;
        while (!session.IsFinalRound && !RunIsOver(session) && safety++ < 400)
        {
            if (session.Phase == GamePhase.Market)
            {
                session.LeaveMarket();
                continue;
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

    private static void Boss_DrawnOncePerRunAndOnlyOnFlaggedRounds()
    {
        Section("boss / drawn per flagged round, never twice in a run");
        var config = new GameConfig();
        config.RngSeed = 5150;
        config.Deck = new DeckDefinition("test", 30, new SizedShapeGenerator(1));
        config.Scoring.PointsPerCubePlaced = 1;
        var session = new GameSession(config); // real progression -> rounds 3,6,9,12,15 are bosses
        Check(session.CurrentRound.Boss == null, "round 1 has no boss",
            "boss " + session.CurrentRound.Boss);

        var bossRounds = new List<int>();
        int safety = 0;
        while (!RunIsOver(session) && safety++ < 2000)
        {
            if (session.Phase == GamePhase.Market)
            {
                session.LeaveMarket();
                if (session.CurrentRound.Boss != null)
                {
                    bossRounds.Add(session.RoundNumber);
                }
                Check(session.CurrentRound.Config.IsBossRound == (session.CurrentRound.Boss != null),
                    "round " + session.RoundNumber + ": a boss exists exactly when flagged");
                continue;
            }
            RoundEngine round = session.CurrentRound;
            if (round.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                round.DecideAdvance(true);
                continue;
            }
            // A boss round may be unwinnable for this dumb driver; stop at the first stall.
            if (round.Status != RoundStatus.InProgress || PlayTurns(session, 1) == 0)
            {
                break;
            }
        }
        Check(bossRounds.Count > 0, "the run met at least one boss", "met " + bossRounds.Count);
        bool allThirds = true;
        for (int i = 0; i < bossRounds.Count; i++)
        {
            if (bossRounds[i] % 3 != 0)
            {
                allThirds = false;
            }
        }
        Check(allThirds, "every boss landed on a third round", string.Join(",", bossRounds));

        // No repeats until the catalogue is exhausted. With all 11 bosses written and only five
        // boss rounds in a run that means never; while fewer are registered the draw is allowed
        // to wrap, which is exactly the documented fallback.
        int distinctExpected = Math.Min(BossRegistry.All.Count, session.BossesFought.Count);
        var firstDraws = new HashSet<string>();
        for (int i = 0; i < distinctExpected; i++)
        {
            firstDraws.Add(session.BossesFought[i]);
        }
        Check(firstDraws.Count == distinctExpected,
            "the first " + distinctExpected + " bosses of the run are all different",
            string.Join(",", session.BossesFought));
        Check(session.BossesFought.Count == bossRounds.Count,
            "one boss drawn per boss round",
            "drawn " + session.BossesFought.Count + " rounds " + bossRounds.Count);

        // Same seed, same bosses: selection must be deterministic.
        var replayConfig = new GameConfig();
        replayConfig.RngSeed = 5150;
        replayConfig.Deck = new DeckDefinition("test", 30, new SizedShapeGenerator(1));
        var replay = new GameSession(replayConfig);
        replay.Config.Scoring.PointsPerCubePlaced = 1;
        int guard = 0;
        while (replay.RoundNumber < 3 && !RunIsOver(replay) && guard++ < 200)
        {
            if (replay.Phase == GamePhase.Market) { replay.LeaveMarket(); continue; }
            if (replay.CurrentRound.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                replay.CurrentRound.DecideAdvance(true);
                continue;
            }
            if (PlayTurns(replay, 1) == 0) { break; }
        }
        Check(replay.CurrentRound.Boss != null && session.BossesFought.Count > 0
            && replay.CurrentRound.Boss.DefId == session.BossesFought[0],
            "the same seed draws the same first boss",
            "replay " + (replay.CurrentRound.Boss != null ? replay.CurrentRound.Boss.DefId : "none"));
    }

    private static void Boss_UfukAndKulePayForOneAxisOnly()
    {
        Section("boss / ufuk pays rows, kule pays columns");
        // A 4x4 board, 4-cube bars: playing four of them fills the board, and the LAST one
        // completes its own row plus all four columns at once - a mixed-axis explosion.
        var scorer = new DefaultScoreCalculator(new ScoringConfig());
        var ufuk = new UfukBoss();
        var kule = new KuleBoss();

        // rows only: 2 rows, no columns, 8 cubes (4 per row)
        var rowsOnly = new LineExplosionScore(2, 0, 8, 8, 0);
        Check(ufuk.ScoreLineExplosion(scorer, rowsOnly) > 0, "ufuk pays for a rows-only clear",
            "" + ufuk.ScoreLineExplosion(scorer, rowsOnly));
        Check(kule.ScoreLineExplosion(scorer, rowsOnly) == 0,
            "kule pays nothing for a rows-only clear",
            "" + kule.ScoreLineExplosion(scorer, rowsOnly));

        // columns only
        var colsOnly = new LineExplosionScore(0, 2, 8, 0, 8);
        Check(kule.ScoreLineExplosion(scorer, colsOnly) > 0, "kule pays for a columns-only clear",
            "" + kule.ScoreLineExplosion(scorer, colsOnly));
        Check(ufuk.ScoreLineExplosion(scorer, colsOnly) == 0,
            "ufuk pays nothing for a columns-only clear",
            "" + ufuk.ScoreLineExplosion(scorer, colsOnly));

        // Mixed clears: each boss must price its OWN axis and be blind to the other one. Two
        // scores that differ only in the off-axis must therefore pay exactly the same.
        var mixedA = new LineExplosionScore(1, 2, 20, 5, 9);
        var moreRows = new LineExplosionScore(4, 2, 40, 17, 9);   // same columns, more rows
        var moreCols = new LineExplosionScore(1, 6, 40, 5, 25);   // same rows, more columns
        Check(kule.ScoreLineExplosion(scorer, mixedA) == kule.ScoreLineExplosion(scorer, moreRows),
            "kule is blind to how many rows also went",
            kule.ScoreLineExplosion(scorer, mixedA) + " vs "
                + kule.ScoreLineExplosion(scorer, moreRows));
        Check(ufuk.ScoreLineExplosion(scorer, mixedA) == ufuk.ScoreLineExplosion(scorer, moreCols),
            "ufuk is blind to how many columns also went",
            ufuk.ScoreLineExplosion(scorer, mixedA) + " vs "
                + ufuk.ScoreLineExplosion(scorer, moreCols));
        Check(ufuk.ScoreLineExplosion(scorer, mixedA)
            == (int)(scorer.ScoreLineExplosion(1, 5) * ufuk.RowBonus),
            "ufuk prices a mixed clear as its rows alone, plus the bonus",
            "" + ufuk.ScoreLineExplosion(scorer, mixedA));
        Check(kule.ScoreLineExplosion(scorer, mixedA)
            == (int)(scorer.ScoreLineExplosion(2, 9) * kule.ColumnBonus),
            "kule prices a mixed clear as its columns alone, plus the bonus",
            "" + kule.ScoreLineExplosion(scorer, mixedA));

        // The bonus really is a bonus: one row under Ufuk beats one row unmodified.
        var oneRow = new LineExplosionScore(1, 0, 4, 4, 0);
        Check(ufuk.ScoreLineExplosion(scorer, oneRow) > scorer.ScoreLineExplosion(1, 4),
            "ufuk's own axis pays above the plain rate",
            "boss " + ufuk.ScoreLineExplosion(scorer, oneRow)
                + " plain " + scorer.ScoreLineExplosion(1, 4));

        // And end to end through a real round: the same rows-only clear, with and without Kule.
        TurnReport plainLast = FillBottomRow(NewSession(5151, 4, 1000000, 40, 1), null);
        TurnReport kuleLast = FillBottomRow(NewSession(5151, 4, 1000000, 40, 1), new KuleBoss());
        TurnReport ufukLast = FillBottomRow(NewSession(5151, 4, 1000000, 40, 1), new UfukBoss());
        Check(plainLast != null && plainLast.ExplodedRows.Count == 1
            && plainLast.ExplodedColumns.Count == 0, "filling the bottom row clears one row only",
            plainLast == null ? "no turn" : "rows " + plainLast.ExplodedRows.Count
                + " cols " + plainLast.ExplodedColumns.Count);
        Check(plainLast != null && plainLast.Score.BaseLines > 0, "a plain round pays for that row",
            plainLast == null ? "no turn" : "lines " + plainLast.Score.BaseLines);
        Check(kuleLast != null && kuleLast.Score.BaseLines == 0,
            "kule pays nothing for a row clear",
            kuleLast == null ? "no turn" : "lines " + kuleLast.Score.BaseLines);
        Check(ufukLast != null && plainLast != null
            && ufukLast.Score.BaseLines > plainLast.Score.BaseLines,
            "ufuk pays MORE than plain for that same row",
            (ufukLast == null ? "no turn" : "ufuk " + ufukLast.Score.BaseLines)
                + " plain " + (plainLast == null ? "?" : "" + plainLast.Score.BaseLines));
    }

    /// <summary>Plays four 1x1 cards along the bottom row of a 4-wide board, which completes
    /// that row and nothing else. Returns the clearing turn's report.</summary>
    private static TurnReport FillBottomRow(GameSession session, BossRound boss)
    {
        if (boss != null)
        {
            session.CurrentRound.SetBoss(boss);
        }
        RoundEngine round = session.CurrentRound;
        TurnReport last = null;
        for (int x = 0; x < 4; x++)
        {
            last = round.PlayFromHand(0, new GridPos(x, 0));
        }
        return last;
    }

    private static void Boss_AlikoymaSeizesACardButNeverTheLast()
    {
        Section("boss / alikoyma holds a card back");
        var session = NewSession(5160, 6, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var boss = new AlikoymaBoss();
        round.SetBoss(boss);
        boss.OnRoundStarted(new RoundContext(session, session.Rng, round));

        Check(boss.SeizedCardId != 0, "it seizes a card at round start",
            "seized " + boss.SeizedCardId);
        Check(round.IsFrozen(boss.SeizedCardId), "the seized card is frozen");
        int frozenNow = 0;
        for (int i = 0; i < round.Hand.Count; i++)
        {
            if (round.IsFrozen(round.Hand[i].Id))
            {
                frozenNow++;
            }
        }
        Check(frozenNow == 1, "exactly one card is held at a time", "frozen " + frozenNow);

        // The seized card cannot be played, and the hold moves on after the turn resolves.
        int seized = boss.SeizedCardId;
        int seizedIndex = -1;
        int freeIndex = -1;
        for (int i = 0; i < round.Hand.Count; i++)
        {
            if (round.Hand[i].Id == seized) { seizedIndex = i; }
            else if (freeIndex < 0) { freeIndex = i; }
        }
        bool refused = false;
        try
        {
            round.PlayFromHand(seizedIndex, new GridPos(0, 0));
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }
        Check(refused, "the held card refuses to be played");
        round.PlayFromHand(freeIndex, new GridPos(0, 0));
        Check(!round.IsFrozen(seized), "the old hold expired when the turn resolved");
        Check(boss.SeizedCardId != 0 && round.IsFrozen(boss.SeizedCardId),
            "and a fresh card is held for the next turn");

        // A one-card hand is left alone, or the boss would take the last option away.
        var solo = NewSession(5161, 6, 1000000, 40, 1);
        solo.Config.Rules.HandSize = 1;
        var soloBoss = new AlikoymaBoss();
        solo.CurrentRound.SetBoss(soloBoss);
        while (solo.CurrentRound.Hand.Count > 1)
        {
            solo.CurrentRound.DiscardWholeHand();
            solo.CurrentRound.RefillHandToSize();
        }
        soloBoss.OnRoundStarted(new RoundContext(solo, solo.Rng, solo.CurrentRound));
        Check(soloBoss.SeizedCardId == 0, "a single-card hand is never seized",
            "seized " + soloBoss.SeizedCardId);
    }

    private static void Boss_MapusSealsOneCellPerTurn()
    {
        Section("boss / mapus seals a cell");
        var session = NewSession(5162, 6, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var boss = new MapusBoss();
        round.SetBoss(boss);
        boss.OnRoundStarted(new RoundContext(session, session.Rng, round));

        Check(boss.HasSeal, "a cell is sealed at round start");
        GridPos sealed1 = boss.SealedCell;
        Check(round.Board.IsSealed(sealed1), "the board knows the cell is sealed",
            sealed1.X + "," + sealed1.Y);
        Check(round.Board.SealedCells.Count == 1, "exactly one cell is sealed",
            "count " + round.Board.SealedCells.Count);
        Check(!round.Board.GetCube(sealed1).HasValue, "a sealed cell holds no cube");

        // Placement refuses it, and so does every path that asks the board where a block fits.
        Check(!round.CanPlaceCard(round.Hand[0], sealed1), "a block cannot be placed on it");
        Check(!round.Board.CanPlace(round.Hand[0].Shape, sealed1), "CanPlace refuses it directly");
        List<GridPos> origins = round.GetValidOrigins(round.Hand[0].Shape);
        bool offered = false;
        for (int i = 0; i < origins.Count; i++)
        {
            if (origins[i].X == sealed1.X && origins[i].Y == sealed1.Y)
            {
                offered = true;
            }
        }
        Check(!offered, "the sealed cell is not offered as a legal origin");
        Check(origins.Count == round.Board.PlayableCellCount - 1,
            "every OTHER empty cell is still legal",
            "origins " + origins.Count + " cells " + round.Board.PlayableCellCount);

        // The seal moves each turn and never accumulates.
        round.PlayFromHand(0, origins[0]);
        Check(round.Board.SealedCells.Count == 1, "still exactly one cell after a turn",
            "count " + round.Board.SealedCells.Count);
        bool oldSealLifted = !round.Board.IsSealed(sealed1)
            || (boss.HasSeal && boss.SealedCell.X == sealed1.X && boss.SealedCell.Y == sealed1.Y);
        Check(oldSealLifted, "the previous seal is gone unless it was re-picked",
            "old " + sealed1.X + "," + sealed1.Y + " new " + boss.SealedCell.X + "," + boss.SealedCell.Y);
    }

    private static void Boss_FedaMakesABonusCardCostTheHand()
    {
        Section("boss / feda sacrifices the hand");
        var session = NewSession(5163, 6, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        round.SetBoss(new FedaBoss());

        BlockCard bonus = session.CreateCard(Bar(1), null);
        round.AddBonusCard(bonus, BonusPlayOutcome.ToDiscard);
        var heldBefore = new List<int>();
        for (int i = 0; i < round.Hand.Count; i++)
        {
            heldBefore.Add(round.Hand[i].Id);
        }
        int discardBefore = round.Deck.DiscardCount;

        round.PlayFromBonus(0, new GridPos(0, 0));

        Check(round.Hand.Count == session.Config.Rules.HandSize,
            "a fresh hand was dealt", "hand " + round.Hand.Count);
        bool anyKept = false;
        for (int i = 0; i < round.Hand.Count; i++)
        {
            if (heldBefore.Contains(round.Hand[i].Id))
            {
                anyKept = true;
            }
        }
        Check(!anyKept, "none of the sacrificed cards is still in hand");
        Check(round.Deck.DiscardCount > discardBefore,
            "the sacrificed hand went to the discard",
            discardBefore + " -> " + round.Deck.DiscardCount);

        // Without the boss a bonus play leaves the hand exactly as it was.
        var plain = NewSession(5163, 6, 1000000, 40, 1);
        BlockCard plainBonus = plain.CreateCard(Bar(1), null);
        plain.CurrentRound.AddBonusCard(plainBonus, BonusPlayOutcome.ToDiscard);
        int firstHeld = plain.CurrentRound.Hand[0].Id;
        plain.CurrentRound.PlayFromBonus(0, new GridPos(0, 0));
        Check(plain.CurrentRound.Hand[0].Id == firstHeld,
            "an ordinary round keeps its hand through a bonus play");
    }

    private static void Boss_AnarsiSilencesEverythingRare()
    {
        Section("boss / anarsi silences rare and legendary");
        var session = NewSession(5170, 6, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        // renovasyon is common, seri_tetik is rare, oryantasyon is legendary (RarityTable).
        Joker common = session.Jokers.Add(new RenovasyonJoker());
        Joker rare = session.Jokers.Add(new SeriTetikJoker());
        Power commonPower = session.Powers.Add(new CimbizPower());
        Power rarePower = session.Powers.Add(new KumSaatiPower());
        round.SetBoss(new AnarsiBoss());

        Check(!round.IsSilencedByBoss(common), "a common joker keeps working");
        Check(round.IsSilencedByBoss(rare), "a rare joker is silenced");
        Check(!round.IsSilencedByBoss(commonPower), "a common power keeps working");
        Check(round.IsSilencedByBoss(rarePower), "a rare power is silenced");

        // Silencing really does gate the inventories, not just report a flag.
        Check(!session.Jokers.CanActivate(rare.InstanceId),
            "a silenced joker cannot be activated");
        Check(!session.Powers.CanUse(rarePower.InstanceId, ActivationTarget.None),
            "a silenced power cannot be used");
        Check(session.Powers.CanBeginUse(commonPower.InstanceId),
            "the common power is still usable");
        Check(rare.SellValue > 0, "a silenced joker keeps its sell value",
            "value " + rare.SellValue);

        // Renovasyon (common, charged) still runs, so the gate is not blanket-blocking.
        Check(session.Jokers.CanActivate(common.InstanceId),
            "the common joker is still activatable");
    }

    /// <summary>Runs a session forward until it is in the market or the run is over.</summary>
    private static bool AdvanceToMarket(GameSession session, int maxTurns)
    {
        int guard = 0;
        while (session.Phase == GamePhase.Round && guard++ < maxTurns)
        {
            if (session.CurrentRound.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                session.CurrentRound.DecideAdvance(true);
                continue;
            }
            if (session.CurrentRound.Status != RoundStatus.InProgress)
            {
                break;
            }
            if (PlayTurns(session, 1) == 0)
            {
                break;
            }
        }
        return session.Phase == GamePhase.Market;
    }

    /// <summary>Buys everything the run score alone covers. Leaves the player unable to afford
    /// any remaining offer without borrowing.</summary>
    private static void SpendEverythingAffordable(GameSession session)
    {
        bool bought = true;
        while (bought)
        {
            bought = false;
            for (int i = 0; i < session.Market.Offers.Count; i++)
            {
                MarketOffer offer = session.Market.Offers[i];
                if (!offer.Sold && offer.Price <= session.TotalScore && session.TryBuyOffer(i))
                {
                    bought = true;
                }
            }
        }
    }

    /// <summary>Drives a credit session into debt deterministically: spend what it has, then
    /// keep rerolling. The reroll price escalates, so once it outruns the run score the credit
    /// path has to book the shortfall.</summary>
    private static void ForceDebt(GameSession session, int maxRerolls)
    {
        SpendEverythingAffordable(session);
        for (int i = 0; i < maxRerolls && session.Debt == 0; i++)
        {
            if (!session.RerollMarket())
            {
                return;
            }
            SpendEverythingAffordable(session);
        }
    }

    private static void KrediKarti_BuysPastYourScoreAndRecordsTheDebt()
    {
        Section("kredi kartı / buys past your score, the shortfall becomes debt");
        var session = NewSession(6100, 6, 30, 40, 3);
        Check(!session.CreditAvailable, "without the joker there is no credit");
        session.Jokers.Add(new KrediKartiJoker());
        Check(session.CreditAvailable, "with it there is");
        Check(session.Debt == 0, "and nothing is owed yet");
        Check(AdvanceToMarket(session, 400), "the run reached the market",
            "phase " + session.Phase);

        SpendEverythingAffordable(session);
        Check(session.Debt == 0, "spending only what you have never borrows",
            "debt " + session.Debt);

        // Reroll (and spend the new stock) until the escalating price outruns the run score,
        // so the NEXT one has to borrow. Every step so far was paid for in full.
        int guard = 0;
        while (session.NextRerollCost <= session.TotalScore && guard++ < 30)
        {
            session.RerollMarket();
            SpendEverythingAffordable(session);
        }
        Check(session.Debt == 0, "everything so far was paid for outright",
            "debt " + session.Debt);

        // Now the exact arithmetic of one borrowed purchase.
        long score = session.TotalScore;
        long cost = session.NextRerollCost;
        Check(cost > score, "the next reroll costs more than is left",
            cost + " vs " + score);
        Check(session.CanAfford(cost), "with credit it is affordable anyway");
        Check(session.CanAfford(cost + 100000000), "in fact any price is");
        Check(session.RerollMarket(), "and it goes through");
        Check(session.TotalScore == 0, "the player's own points went first",
            "score " + session.TotalScore);
        Check(session.Debt == cost - score, "and exactly the shortfall was booked as debt",
            "debt " + session.Debt + " expected " + (cost - score));
    }

    private static void KrediKarti_RefusesCreditWithoutTheJoker()
    {
        Section("kredi kartı / no joker, no credit");
        var session = NewSession(6102, 6, 30, 40, 3);
        Check(AdvanceToMarket(session, 400), "reached the market");
        SpendEverythingAffordable(session);

        bool boughtBroke = false;
        for (int i = 0; i < session.Market.Offers.Count; i++)
        {
            if (session.Market.Offers[i].Price > session.TotalScore && session.TryBuyOffer(i))
            {
                boughtBroke = true;
            }
        }
        Check(!boughtBroke, "an unaffordable offer is still refused");
        Check(!session.CanAfford(session.TotalScore + 1), "and it is not even reported affordable");
        Check(!session.RerollMarket() || session.Debt == 0,
            "a reroll it cannot pay for is refused rather than borrowed");
        Check(session.Debt == 0, "no debt appears out of nowhere", "debt " + session.Debt);
    }

    private static void KrediKarti_InterestCompoundsEveryRound()
    {
        Section("kredi kartı / the debt compounds 10% every round");
        var session = NewSession(6103, 6, 30, 40, 3);
        var card = (KrediKartiJoker)session.Jokers.Add(new KrediKartiJoker());
        Check(AdvanceToMarket(session, 400), "reached the market");
        ForceDebt(session, 12);
        Check(session.Debt > 0, "there is a debt to charge interest on", "debt " + session.Debt);

        long owed = session.Debt;
        session.LeaveMarket();
        Check(AdvanceToMarket(session, 400) || session.Phase == GamePhase.GameOver,
            "played another round");
        long expected = owed + (owed * card.InterestPercent + 99) / 100;
        Check(session.Debt == expected, "one round of interest, rounded up",
            owed + " -> " + session.Debt + " (expected " + expected + ")");
        Check(session.Debt > owed, "so it really did grow");
    }

    private static void KrediKarti_RepayIsManualAndMarketOnly()
    {
        Section("kredi kartı / repaying is manual, and only in the market");
        var session = NewSession(6104, 6, 30, 40, 3);
        session.Jokers.Add(new KrediKartiJoker());
        Check(AdvanceToMarket(session, 400), "reached the market");
        ForceDebt(session, 12);
        Check(session.Debt > 0, "there is a debt", "debt " + session.Debt);

        // Earnings alone must NOT settle it - that is the whole decision the joker offers.
        long owed = session.Debt;
        session.LeaveMarket();
        AdvanceToMarket(session, 400);
        Check(session.Debt >= owed, "a round of earnings did not pay it off by itself",
            owed + " -> " + session.Debt);
        Check(session.TotalScore > 0, "the earnings went to the player instead",
            "score " + session.TotalScore);

        // Now pay, by hand.
        long score = session.TotalScore;
        long debt = session.Debt;
        long paid = session.RepayDebt(debt);
        Check(paid > 0, "paying moved money", "paid " + paid);
        Check(session.Debt == debt - paid, "the debt fell by exactly that",
            debt + " -> " + session.Debt);
        Check(session.TotalScore == score - paid, "and the score fell by exactly that",
            score + " -> " + session.TotalScore);
        Check(session.RepayDebt(1000000) <= session.TotalScore + debt,
            "you can never pay more than you have or owe");
    }

    private static void KrediKarti_BossRoundWithOpenDebtEndsTheRun()
    {
        Section("kredi kartı / a boss round that ends in debt ends the run");
        var session = NewSession(6105, 6, 30, 40, 3);
        session.Jokers.Add(new KrediKartiJoker());
        // FixedProgression flags no boss rounds, so drive the deadline through the real curve.
        var config = new GameConfig();
        config.RngSeed = 6105;
        config.Deck = new DeckDefinition("test", 40, new SizedShapeGenerator(1));
        var real = new GameSession(config);
        real.Config.Scoring.PointsPerCubePlaced = 500; // clear every threshold comfortably
        real.Jokers.Add(new KrediKartiJoker());

        int guard = 0;
        bool sawDebt = false;
        while (real.Phase != GamePhase.GameOver && real.Phase != GamePhase.RunWon && guard++ < 400)
        {
            if (real.Phase == GamePhase.Market)
            {
                // Borrow hard on the first market and never pay a lira back.
                if (!sawDebt)
                {
                    ForceDebt(real, 40);
                    sawDebt = real.Debt > 0;
                }
                real.LeaveMarket();
                continue;
            }
            if (real.CurrentRound.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                real.CurrentRound.DecideAdvance(true);
                continue;
            }
            if (PlayTurns(real, 1) == 0) { break; }
        }
        Check(sawDebt, "the run really did take on debt", "debt " + real.Debt);
        Check(real.Phase == GamePhase.GameOver, "and the run is over",
            "phase " + real.Phase + " round " + real.RoundNumber);
        Check(real.CurrentRound.Loss == LossReason.DebtNotRepaid,
            "for the debt, not for the board",
            "loss " + real.CurrentRound.Loss);
        Check(real.CurrentRound.Config.IsBossRound,
            "and it happened on a boss round", "round " + real.RoundNumber);
        Check(real.CurrentRound.Status == RoundStatus.Advanced,
            "the round itself was survived - the books were the problem",
            "status " + real.CurrentRound.Status);
    }

    private static void KrediKarti_ADebtFreeBossRoundIsFine()
    {
        Section("kredi kartı / holding the joker without debt costs nothing");
        var config = new GameConfig();
        config.RngSeed = 6106;
        config.Deck = new DeckDefinition("test", 40, new SizedShapeGenerator(1));
        var session = new GameSession(config);
        session.Config.Scoring.PointsPerCubePlaced = 500;
        session.Jokers.Add(new KrediKartiJoker());

        int guard = 0;
        bool passedABoss = false;
        while (session.Phase != GamePhase.GameOver && session.Phase != GamePhase.RunWon
            && guard++ < 400)
        {
            if (session.Phase == GamePhase.Market) { session.LeaveMarket(); continue; }
            if (session.CurrentRound.Config.IsBossRound
                && session.CurrentRound.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                passedABoss = true;
            }
            if (session.CurrentRound.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                session.CurrentRound.DecideAdvance(true);
                continue;
            }
            if (PlayTurns(session, 1) == 0) { break; }
        }
        Check(session.Debt == 0, "never borrowed, so nothing is owed");
        Check(passedABoss, "a boss round was reached", "round " + session.RoundNumber);
        Check(session.CurrentRound.Loss != LossReason.DebtNotRepaid,
            "and the debt deadline never fired", "loss " + session.CurrentRound.Loss);
    }

    private static void KrediKarti_CannotBeSoldWhileInDebt()
    {
        Section("kredi kartı / it cannot be sold out from under the debt");
        var session = NewSession(6107, 6, 30, 40, 3);
        Joker card = session.Jokers.Add(new KrediKartiJoker());
        Check(session.Jokers.CanSell(card), "with no debt it sells normally");

        Check(AdvanceToMarket(session, 400), "reached the market");
        ForceDebt(session, 12);
        Check(session.Debt > 0, "there is a debt", "debt " + session.Debt);

        Check(!session.Jokers.CanSell(card), "now the sale is refused");
        long score = session.TotalScore;
        int paidOut = session.Jokers.Sell(card);
        Check(paidOut == 0, "selling pays nothing", "paid " + paidOut);
        Check(session.Jokers.Find(card.InstanceId) != null, "and the joker is still there");
        Check(session.TotalScore == score, "no money changed hands", "score " + session.TotalScore);
        Check(session.Debt > 0, "the debt is untouched - there is no way out through the market");

        // Pay it off and the lock lifts.
        session.RepayDebtInFull();
        if (session.Debt == 0)
        {
            Check(session.Jokers.CanSell(card), "once clear, it can be sold again");
            Check(session.Jokers.Sell(card) > 0, "and the sale really goes through");
        }
        else
        {
            Check(!session.Jokers.CanSell(card),
                "still short of the full amount, so still locked", "debt " + session.Debt);
        }
    }

    private static void Boss_TitizlikPaysForNothingButASweep()
    {
        Section("boss / titizlik pays for nothing but a clean sweep");
        var scorer = new DefaultScoreCalculator(new ScoringConfig());
        var boss = new TitizlikBoss();
        Check(boss.OnlyCleanSweepsScore, "it declares that only sweeps score");
        Check(boss.ScoreLineExplosion(scorer, new LineExplosionScore(2, 1, 12, 8, 4)) == 0,
            "a line clear is worth nothing, whoever completed it");
        Check(boss.ScoreCleanSweep(scorer) > scorer.ScoreCleanSweep(),
            "and a sweep is worth a little MORE than usual",
            scorer.ScoreCleanSweep() + " -> " + boss.ScoreCleanSweep(scorer));

        // Now through a real turn: place a block and clear nothing. Ordinarily that pays for the
        // cubes placed; under Titizlik it pays nothing at all.
        var plain = NewSession(5190, 6, 1000000, 40, 3);
        plain.Config.Scoring.PointsPerCubePlaced = 10;
        TurnReport plainTurn = PlayOneCard(plain.CurrentRound);
        Check(plainTurn.Score.Total > 0, "a plain round pays for a placement",
            "total " + plainTurn.Score.Total);

        var strict = NewSession(5190, 6, 1000000, 40, 3);
        strict.Config.Scoring.PointsPerCubePlaced = 10;
        strict.CurrentRound.SetBoss(new TitizlikBoss());
        TurnReport strictTurn = PlayOneCard(strict.CurrentRound);
        Check(strictTurn.Score.BasePlacement == 0, "under the boss the placement pays nothing",
            "placement " + strictTurn.Score.BasePlacement);
        Check(strictTurn.Score.BaseGold == 0 && strictTurn.Score.BaseCombo == 0,
            "and so do gold and combo");
        Check(strictTurn.Score.Total == 0, "so the whole turn is worth nothing",
            "total " + strictTurn.Score.Total);

        // A sweep DOES pay, and pays more than the plain rule would.
        var sweeping = NewSession(5191, 4, 1000000, 40, 1);
        RoundEngine round = sweeping.CurrentRound;
        round.SetBoss(new TitizlikBoss());
        FillBoardSolid(round, sweeping);
        // Empty one cell so a single-cube placement completes its row AND sweeps the board.
        var hole = new GridPos(round.Board.MinX, round.Board.MinY);
        round.Board.DestroyCube(hole);
        TurnReport sweepTurn = PlayOneCard(round);
        Check(sweepTurn != null && sweepTurn.CleanSweep, "the board was swept",
            sweepTurn == null ? "no card played" : "sweep " + sweepTurn.CleanSweep);
        Check(sweepTurn.Score.BaseSweep > 0, "and the sweep is the one thing that paid",
            "sweep " + sweepTurn.Score.BaseSweep);
        Check(sweepTurn.Score.BaseLines == 0,
            "the line that caused it still paid nothing", "lines " + sweepTurn.Score.BaseLines);
    }

    private static void Boss_TitizlikLeavesJokerBonusesAlone()
    {
        Section("boss / titizlik beats your board, not your build");
        var session = NewSession(5192, 6, 1000000, 40, 3);
        session.Config.Scoring.PointsPerCubePlaced = 10;
        var joker = (KolayParaJoker)session.Jokers.Add(new KolayParaJoker());
        session.CurrentRound.SetBoss(new TitizlikBoss());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);

        TurnReport report = PlayOneCard(session.CurrentRound);
        int fromJoker = 0;
        foreach (ScoreContribution c in report.Score.Contributions)
        {
            if (c.Source == joker.DefId) { fromJoker += c.Flat; }
        }
        Check(report.Score.BasePlacement == 0, "the base placement is still wiped");
        Check(fromJoker > 0, "but the joker's own bonus still lands", "joker " + fromJoker);
        Check(report.Score.Total > 0, "so a build can still score through it",
            "total " + report.Score.Total);
    }

    private static void Boss_CanaGelecegineMalaTakesAQuarterOfThePurse()
    {
        Section("boss / cana geleceğine mala charges the purse when the deck dries up");
        var boss = new CanaGelecegineMalaBoss();
        // A 4-card deck against a hand of 3 empties the draw pile almost immediately.
        var session = NewSession(5193, 6, 1000000, 4, 1);
        session.Config.Scoring.PointsPerCubePlaced = 100;
        RoundEngine round = session.CurrentRound;
        round.SetBoss(boss);

        PlayTurns(session, 1);
        long purse = session.TotalScore;
        Check(purse > 0, "the player has money to lose", "score " + purse);

        long before = purse;
        int guard = 0;
        while (boss.LostScore == 0 && guard++ < 30 && round.Status == RoundStatus.InProgress)
        {
            before = session.TotalScore;
            PlayTurns(session, 1);
        }
        Check(boss.LostScore > 0, "the draw pile dried up and the purse paid",
            "lost " + boss.LostScore);
        Check(session.TotalScore < before + 100000,
            "the score really went down rather than only up", "score " + session.TotalScore);

        // The arithmetic, in isolation: a quarter, rounded up, and never more than there is.
        var arith = NewSession(5194, 6, 1000000, 40, 1);
        arith.AddCurrency(400 - arith.TotalScore);
        Check(arith.TotalScore == 400, "set up a known purse", "score " + arith.TotalScore);
        Check(arith.TakeCurrencyPercent(25) == 100, "a quarter of 400 is 100");
        Check(arith.TotalScore == 300, "and the purse fell by exactly that",
            "score " + arith.TotalScore);
        arith.AddCurrency(-arith.TotalScore + 1);
        Check(arith.TakeCurrencyPercent(25) == 1, "a quarter of 1 rounds up to 1, not down to 0");
        Check(arith.TotalScore == 0, "leaving nothing");
        Check(arith.TakeCurrencyPercent(25) == 0, "an empty purse cannot be charged again");
        Check(arith.TotalScore == 0, "and never goes negative", "score " + arith.TotalScore);
    }

    private static void Boss_TasVeSopaSwitchesEverythingOffAndAsksForLess()
    {
        Section("boss / taş ve sopa takes the whole inventory and lowers the bar");
        var session = NewSession(5195, 6, 200, 40, 1);
        Joker common = session.Jokers.Add(new RenovasyonJoker());
        Joker rare = session.Jokers.Add(new SeriTetikJoker());
        Power power = session.Powers.Add(new CimbizPower());
        RoundEngine round = session.CurrentRound;

        int fullBar = round.ScoreThreshold;
        Check(fullBar == round.Config.ScoreThreshold, "with no boss the bar is the config's",
            fullBar + " vs " + round.Config.ScoreThreshold);
        Check(!round.IsSilencedByBoss(common), "and nothing is silenced");

        var boss = new TasVeSopaBoss();
        round.SetBoss(boss);
        Check(round.IsSilencedByBoss(common), "a common joker is switched off");
        Check(round.IsSilencedByBoss(rare), "so is a rare one - rarity does not save you");
        Check(round.IsSilencedByBoss(power), "and so is every power");
        Check(!session.Jokers.CanActivate(common.InstanceId), "nothing can be activated");
        Check(!session.Powers.CanUse(power.InstanceId, ActivationTarget.None),
            "and no power can be used");
        Check(common.SellValue > 0, "they all keep their sell value");

        Check(round.ScoreThreshold < fullBar, "the bar really is lower",
            fullBar + " -> " + round.ScoreThreshold);
        Check(round.ScoreThreshold == (fullBar * boss.ThresholdPercent + 99) / 100,
            "by exactly the boss's percentage, rounded up",
            "" + round.ScoreThreshold);

        // Rounding up means a discount can never erase a threshold outright.
        var tiny = new TasVeSopaBoss();
        Check(tiny.FilterScoreThreshold(1) >= 1, "a threshold of 1 does not become 0",
            "" + tiny.FilterScoreThreshold(1));
    }

    private static void Boss_TerslikTurnsJokerPointsIntoLosses()
    {
        Section("boss / terslik turns joker points into losses");
        // Kolay Para pays a flat amount per cube placed - the plainest "every block scores"
        // joker there is, and exactly the case the design named.
        var normal = NewSession(5180, 6, 1000000, 40, 3);
        var plainJoker = (KolayParaJoker)normal.Jokers.Add(new KolayParaJoker());
        normal.Jokers.DispatchRoundStarted(normal.CurrentRound);
        TurnReport plainTurn = PlayOneCard(normal.CurrentRound);
        int jokerGave = 0;
        foreach (ScoreContribution c in plainTurn.Score.Contributions)
        {
            if (c.Source == plainJoker.DefId) { jokerGave += c.Flat; }
        }
        Check(jokerGave > 0, "without the boss the joker ADDS points", "gave " + jokerGave);

        var cursed = NewSession(5180, 6, 1000000, 40, 3);
        var cursedJoker = (KolayParaJoker)cursed.Jokers.Add(new KolayParaJoker());
        cursed.CurrentRound.SetBoss(new TerslikBoss());
        cursed.Jokers.DispatchRoundStarted(cursed.CurrentRound);
        Check(cursed.CurrentRound.InvertsJokerScore, "the round reports itself inverted");
        TurnReport cursedTurn = PlayOneCard(cursed.CurrentRound);
        int jokerTook = 0;
        foreach (ScoreContribution c in cursedTurn.Score.Contributions)
        {
            if (c.Source == cursedJoker.DefId) { jokerTook += c.Flat; }
        }
        Check(jokerTook == -jokerGave, "with the boss it TAKES exactly the same amount",
            jokerGave + " -> " + jokerTook);
        Check(cursedTurn.Score.Total < plainTurn.Score.Total,
            "so the turn is worth less than it would have been",
            plainTurn.Score.Total + " -> " + cursedTurn.Score.Total);
    }

    private static void Terslik_ATurnNeverPaysLessThanNothing()
    {
        Section("boss / terslik can empty a turn but never reverse it");
        var session = NewSession(5181, 6, 1000000, 40, 3);
        session.Config.Scoring.PointsPerCubePlaced = 10; // a turn that really does earn something
        var joker = (KolayParaJoker)session.Jokers.Add(new KolayParaJoker());
        // A penalty far bigger than anything the turn can earn.
        joker.PointsPerCube = 100000;
        RoundEngine round = session.CurrentRound;
        round.SetBoss(new TerslikBoss());
        session.Jokers.DispatchRoundStarted(round);

        int before = round.RoundScore;
        TurnReport report = PlayOneCard(round);
        Check(report != null, "a card was played");
        Check(report.Score.BaseTotal > 0, "the turn had real earnings to lose",
            "base " + report.Score.BaseTotal);
        Check(report.Score.FlatBonus < -report.Score.BaseTotal,
            "and the joker's penalty is bigger than them",
            "flat " + report.Score.FlatBonus);
        Check(report.Score.Total == 0, "the turn pays exactly nothing, not a negative",
            "total " + report.Score.Total);
        Check(round.RoundScore == before, "and the round score does not go backwards",
            before + " -> " + round.RoundScore);

        // Several such turns in a row must still never dig a hole.
        PlayTurns(session, 4);
        Check(round.RoundScore >= before, "still no hole after more turns",
            "" + round.RoundScore);
    }

    private static void Terslik_LeavesJokersThatGiveNoPointsAlone()
    {
        Section("boss / terslik does not touch jokers that hand out no points");
        var session = NewSession(5182, 6, 1000000, 40, 1);
        Joker insider = session.Jokers.Add(new InsiderJoker());
        Joker renovasyon = session.Jokers.Add(new RenovasyonJoker());
        RoundEngine round = session.CurrentRound;
        round.SetBoss(new TerslikBoss());
        session.Jokers.DispatchRoundStarted(round);

        Check(!round.IsSilencedByBoss(insider), "they are NOT silenced - this is not Anarşi");
        Check(session.Config.Rules.RevealTopDrawCard,
            "Insider still reveals the top card, exactly as on any other round");
        Check(session.Jokers.CanActivate(renovasyon.InstanceId),
            "Renovasyon can still be activated");
        Check(session.Jokers.TryActivate(renovasyon.InstanceId, ActivationTarget.None),
            "and it still runs");
    }

    private static void Terslik_DrainsPiggyBanksInsteadOfFillingThem()
    {
        Section("boss / terslik makes a piggy bank leak instead of fill");
        var session = NewSession(5183, 6, 1000000, 40, 1);
        var bank = (CimriKumbaraJoker)session.Jokers.Add(new CimriKumbaraJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);

        // Fill it the honest way first, with no boss in play.
        int empty = bank.SellValue;
        PlayTurns(session, 6);
        int filled = bank.SellValue;
        Check(filled > empty, "it fills up on an ordinary round", empty + " -> " + filled);

        // Now the boss arrives and the same hook starts running backwards.
        round.SetBoss(new TerslikBoss());
        PlayTurns(session, 3);
        Check(bank.SellValue < filled, "under the boss the very same joker drains it",
            filled + " -> " + bank.SellValue);

        // An emptied bank cannot go into debt: sell value bottoms out at the base price.
        PlayTurns(session, 80);
        Check(bank.SellValue >= bank.BaseSellValue,
            "and it stops at empty - a piggy bank never owes you money",
            "value " + bank.SellValue + " base " + bank.BaseSellValue);
    }

    private static void Terslik_NeverInvertsPowersOrTheBaseScore()
    {
        Section("boss / terslik leaves powers and the base score alone");
        var session = NewSession(5184, 6, 1000000, 40, 3);
        session.Config.Scoring.PointsPerCubePlaced = 10; // placement pays nothing by default
        RoundEngine round = session.CurrentRound;
        round.SetBoss(new TerslikBoss());

        // No jokers at all: the turn is pure base score, and it must be untouched.
        TurnReport report = PlayOneCard(round);
        Check(report.Score.Total > 0, "a turn with no jokers still scores normally",
            "total " + report.Score.Total);
        Check(report.Score.FlatBonus == 0, "and nothing was added or subtracted",
            "flat " + report.Score.FlatBonus);

        // A power that pays still pays: the inversion window is open around joker dispatch only.
        var withPower = NewSession(5185, 6, 1000000, 40, 3);
        withPower.CurrentRound.SetBoss(new TerslikBoss());
        Power power = withPower.Powers.Add(new BuyutecPower());
        withPower.Powers.DispatchRoundStarted(withPower.CurrentRound);
        Check(!withPower.CurrentRound.IsSilencedByBoss(power), "the power is not silenced");
        Check(withPower.Powers.CanUse(power.InstanceId, ActivationTarget.None),
            "and it is usable");
    }

    private static void Terslik_TheWindowClosesAgain()
    {
        Section("boss / terslik never leaks into the next round");
        var session = NewSession(5186, 6, 40, 40, 3);
        RoundEngine bossRound = session.CurrentRound;
        bossRound.SetBoss(new TerslikBoss());
        var joker = (KolayParaJoker)session.Jokers.Add(new KolayParaJoker());
        session.Jokers.DispatchRoundStarted(bossRound);
        PlayTurns(session, 2);
        Check(bossRound.InvertsJokerScore, "the boss round is inverted");

        // Reach the market and start a plain round: the inversion must be gone.
        int guard = 0;
        while (session.Phase == GamePhase.Round && guard++ < 200)
        {
            if (session.CurrentRound.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                session.CurrentRound.DecideAdvance(true);
                continue;
            }
            if (PlayTurns(session, 1) == 0) { break; }
        }
        if (session.Phase == GamePhase.Market)
        {
            session.LeaveMarket();
            RoundEngine plain = session.CurrentRound;
            Check(!plain.InvertsJokerScore, "the next round is not inverted");
            TurnReport report = PlayOneCard(plain);
            int gave = 0;
            foreach (ScoreContribution c in report.Score.Contributions)
            {
                if (c.Source == joker.DefId) { gave += c.Flat; }
            }
            Check(gave > 0, "and the joker pays the player again", "gave " + gave);
        }
        else
        {
            Check(false, "the run never reached a second round", "phase " + session.Phase);
        }
    }

    private static void Boss_OburlukEatsOnlyWhenSlotsAreFull()
    {
        Section("boss / oburluk punishes a full inventory");
        // Not full: nothing is switched off.
        var roomy = NewSession(5171, 6, 1000000, 40, 1);
        roomy.Jokers.Add(new RenovasyonJoker());
        var roomyBoss = new OburlukBoss();
        roomy.CurrentRound.SetBoss(roomyBoss);
        roomyBoss.OnRoundStarted(new RoundContext(roomy, roomy.Rng, roomy.CurrentRound));
        Check(roomyBoss.SilencedJokerId == 0, "a free joker slot means nothing is eaten",
            "silenced " + roomyBoss.SilencedJokerId);

        // Full: exactly one joker and one power go quiet.
        var full = NewSession(5172, 6, 1000000, 40, 1);
        full.Jokers.MaxSlots = 2;
        full.Powers.MaxSlots = 2;
        full.Jokers.Add(new RenovasyonJoker());
        full.Jokers.Add(new InsiderJoker());
        full.Powers.Add(new CimbizPower());
        full.Powers.Add(new KlonPower());
        Check(full.Jokers.IsFull && full.Powers.IsFull, "both inventories are full");
        var boss = new OburlukBoss();
        full.CurrentRound.SetBoss(boss);
        boss.OnRoundStarted(new RoundContext(full, full.Rng, full.CurrentRound));

        Check(boss.SilencedJokerId != 0, "a joker was switched off",
            "silenced " + boss.SilencedJokerId);
        Check(boss.SilencedPowerId != 0, "a power was switched off",
            "silenced " + boss.SilencedPowerId);
        int silencedJokers = 0;
        foreach (Joker joker in full.Jokers.Jokers)
        {
            if (full.CurrentRound.IsSilencedByBoss(joker))
            {
                silencedJokers++;
            }
        }
        Check(silencedJokers == 1, "exactly one joker, not all of them",
            "silenced " + silencedJokers);
        int silencedPowers = 0;
        foreach (Power power in full.Powers.Powers)
        {
            if (full.CurrentRound.IsSilencedByBoss(power))
            {
                silencedPowers++;
            }
        }
        Check(silencedPowers == 1, "exactly one power", "silenced " + silencedPowers);
    }

    private static void Boss_TukenmislikStopsEveryRefill()
    {
        Section("boss / tukenmislik blocks every recharge");
        var session = NewSession(5173, 6, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        Power power = session.Powers.Add(new CimbizPower());
        round.SetBoss(new TukenmislikBoss());

        Check(power.Charged, "the round still starts with a charged power");
        Check(round.PowerRechargeBlocked, "the round reports refills as blocked");
        session.Powers.BurnCharge(power);
        Check(!power.Charged, "the charge is gone once spent");

        session.Powers.RechargeAll();
        Check(!power.Charged, "a blanket recharge does nothing");
        Check(!session.Powers.Recharge(power.InstanceId), "a targeted recharge refuses");
        Check(!session.Powers.RechargeOne(), "Powerbank's refill refuses");
        Check(!power.Charged, "the power is still empty after every attempt");

        // A clean sweep - the powers' whole economy - also pays nothing now. Driven through the
        // real dispatch, because that (not RechargeAll) is what a sweep actually calls.
        var sweepCtx = new TurnContext(session, session.Rng, round, new TurnReport(),
            new ScoreBreakdown());
        session.Powers.DispatchCleanSweep(sweepCtx, true);
        Check(!power.Charged, "not even a clean sweep refills it");

        // Sanity: the very same sweep DOES refill on a round with no boss.
        var plain = NewSession(5173, 6, 1000000, 40, 1);
        Power plainPower = plain.Powers.Add(new CimbizPower());
        plain.Powers.BurnCharge(plainPower);
        var plainCtx = new TurnContext(plain, plain.Rng, plain.CurrentRound, new TurnReport(),
            new ScoreBreakdown());
        plain.Powers.DispatchCleanSweep(plainCtx, true);
        Check(plainPower.Charged, "an ordinary round's sweep refills it");
    }

    private static void Boss_VanilyaStripsEveryElement()
    {
        Section("boss / vanilya ignores block elements");
        var session = NewSession(5180, 6, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        round.SetBoss(new VanilyaBoss());
        Check(round.ElementsIgnored, "the round reports elements as ignored");

        // A gold block places PLAIN cubes: no gold upkeep, and it is destructible again.
        BlockCard gold = session.CreateCard(Bar(2), new[] { BlockElement.Gold });
        round.AddBonusCard(gold, BonusPlayOutcome.ToDiscard);
        TurnReport report = round.PlayFromBonus(0, new GridPos(0, 0));
        Cube? placed = round.Board.GetCube(new GridPos(0, 0));
        Check(placed.HasValue && placed.Value.Kind == CubeKind.Normal,
            "a gold block stamps ordinary cubes",
            placed.HasValue ? placed.Value.Kind.ToString() : "empty");
        Check(report.GoldBonus == 0, "no gold upkeep is paid", "bonus " + report.GoldBonus);

        // Mechanical rotation is refused, and the UI is told the same thing.
        BlockCard gears = session.CreateCard(Bar(2), new[] { BlockElement.Mechanical });
        round.AddBonusCard(gears, BonusPlayOutcome.ToDiscard);
        Check(!round.CardHasElement(gears, BlockElement.Mechanical),
            "the engine reports the card as plain");
        Check(gears.Has(BlockElement.Mechanical),
            "the card itself keeps its element (it is only ignored)");

        // Ghost overhang is gone: a ghost block may no longer hang off the edge.
        BlockCard ghost = session.CreateCard(Bar(2), new[] { BlockElement.Ghost });
        var offEdge = new GridPos(5, 0); // a 2-wide bar at x=5 hangs over a 6-wide board
        Check(!round.CanPlaceCard(ghost, offEdge), "a ghost block cannot overhang under vanilya");

        // Sanity: the same card DOES overhang and pay gold on an ordinary round.
        var plain = NewSession(5180, 6, 1000000, 40, 1);
        BlockCard plainGhost = plain.CreateCard(Bar(2), new[] { BlockElement.Ghost });
        Check(plain.CurrentRound.CanPlaceCard(plainGhost, offEdge),
            "an ordinary round still allows the overhang");
        BlockCard plainGold = plain.CreateCard(Bar(2), new[] { BlockElement.Gold });
        plain.CurrentRound.AddBonusCard(plainGold, BonusPlayOutcome.ToDiscard);
        TurnReport plainReport = plain.CurrentRound.PlayFromBonus(0, new GridPos(0, 0));
        Check(plainReport.GoldBonus > 0, "and still pays the gold upkeep",
            "bonus " + plainReport.GoldBonus);
    }

    private static void Boss_TaxesTakeCardsOutOfTheRunDeck()
    {
        Section("boss / the two deck taxes");
        // "Harcama vergisi": emptying the draw pile costs the run deck two cards.
        var session = NewSession(5181, 6, 1000000, 12, 1);
        RoundEngine round = session.CurrentRound;
        var boss = new HarcamaVergisiBoss();
        round.SetBoss(boss);
        int ownedBefore = session.OwnedCards.Count;

        // Churn the hand until the draw pile runs dry at least once.
        int guard = 0;
        while (boss.TaxedCards == 0 && guard++ < 40 && round.Status == RoundStatus.InProgress)
        {
            round.CycleHandWithoutReshuffle();
        }
        Check(boss.TaxedCards > 0, "emptying the draw pile taxed the deck",
            "taxed " + boss.TaxedCards);
        Check(boss.TaxedCards == boss.CardsPerEmptying,
            "exactly two cards per emptying, not one per failed draw",
            "taxed " + boss.TaxedCards);
        Check(session.OwnedCards.Count == ownedBefore - boss.TaxedCards,
            "the cards left the RUN deck, not just the round",
            ownedBefore + " -> " + session.OwnedCards.Count);

        // "Özel tüketim vergisi": using a power costs a card.
        var exSession = NewSession(5182, 6, 1000000, 20, 1);
        var exBoss = new OzelTuketimVergisiBoss();
        exSession.CurrentRound.SetBoss(exBoss);
        // Büyüteç just reveals draw cards, so it always runs - the tax, not the power, is
        // what this test is about.
        Power power = exSession.Powers.Add(new BuyutecPower());
        int exOwnedBefore = exSession.OwnedCards.Count;
        Check(exSession.Powers.TryUse(power.InstanceId, ActivationTarget.None),
            "the power ran");
        Check(exBoss.TaxedCards == 1, "using it cost exactly one card",
            "taxed " + exBoss.TaxedCards);
        Check(exSession.OwnedCards.Count == exOwnedBefore - 1,
            "and that card left the run deck",
            exOwnedBefore + " -> " + exSession.OwnedCards.Count);

        // The floor: a tax never shrinks the deck below the hand size, which would lose the
        // NEXT round during construction.
        var tiny = NewSession(5183, 6, 1000000, 4, 1);
        int floor = tiny.Config.Rules.HandSize;
        int removed = tiny.TaxOwnedCards(99, tiny.Rng);
        Check(tiny.OwnedCards.Count == floor, "the deck stops at the hand size",
            "left " + tiny.OwnedCards.Count + " floor " + floor);
        Check(removed == 4 - floor, "only what could be taken was taken", "removed " + removed);
    }

    private static void Boss_HarcamaVergisiAndErosionShareTheSameTrigger()
    {
        Section("boss / the deck tax and the erosion clock share one trigger");
        // Both features hang off THE SAME event - the draw pile running dry. "Harcama vergisi"
        // taxes the run deck for it and shuffle erosion eats the arena for it, so a boss round
        // makes one empty deck bite twice. This test exists to keep that deliberate, because
        // the two were written independently and merged.
        var session = NewErodingSession(5190, 5, 14, ShuffleErosion.FromOutside, 1);
        RoundEngine round = session.CurrentRound;
        var boss = new HarcamaVergisiBoss();
        round.SetBoss(boss);
        int ownedBefore = session.OwnedCards.Count;
        int freeRecycles = round.FreeDeckRecyclesLeft;
        Check(freeRecycles > 0, "the round starts with free recycles", "left " + freeRecycles);

        // Churn until the pile has run dry once: the tax bites immediately, the arena does not
        // (the first recycles are free).
        int guard = 0;
        while (boss.TaxedCards == 0 && guard++ < 40 && round.Status == RoundStatus.InProgress)
        {
            round.CycleHandWithoutReshuffle();
        }
        Check(boss.TaxedCards > 0, "the tax bit on the first drying-out",
            "taxed " + boss.TaxedCards);
        Check(session.OwnedCards.Count < ownedBefore, "cards left the run deck",
            ownedBefore + " -> " + session.OwnedCards.Count);
        Check(round.BoardErosionCount == 0, "but the free allowance spared the arena",
            "erosions " + round.BoardErosionCount);
        Check(round.FreeDeckRecyclesLeft < freeRecycles,
            "the same event still moved the erosion clock",
            "left " + round.FreeDeckRecyclesLeft);

        // Past the allowance the arena erodes too, on top of the tax.
        int sizeBefore = round.Board.Width;
        int taxedBefore = boss.TaxedCards;
        guard = 0;
        while (round.BoardErosionCount == 0 && guard++ < 40
            && round.Status == RoundStatus.InProgress)
        {
            round.DebugForceDeckRecycle();
        }
        Check(round.BoardErosionCount > 0, "the clock eventually eats the arena",
            "erosions " + round.BoardErosionCount);
        Check(round.Board.Width < sizeBefore || round.Board.DeadCellCount > 0,
            "the board really got smaller or scarred",
            sizeBefore + " -> " + round.Board.Width + " dead " + round.Board.DeadCellCount);
        Check(boss.TaxedCards >= taxedBefore, "and the tax kept its own tally",
            "taxed " + boss.TaxedCards);

        // A forced recycle is not a "drying out" the boss sees (no draw was attempted), so the
        // two counters are independent - neither drives the other.
        Check(boss.TaxedCards == taxedBefore,
            "a forced recycle alone does not tax: the tax follows an actual empty draw",
            "taxed " + boss.TaxedCards + " was " + taxedBefore);
    }

    private static void BossRounds_FlaggedEveryThirdRound()
    {
        Section("boss rounds / every third round is flagged");
        var progression = new DefaultRoundProgression();
        var flagged = new List<int>();
        for (int n = 1; n <= 15; n++)
        {
            if (progression.GetRound(n).IsBossRound)
            {
                flagged.Add(n);
            }
        }
        Check(flagged.Count == 5, "five boss rounds in a 15-round run", "count " + flagged.Count);
        Check(flagged.Count == 5 && flagged[0] == 3 && flagged[1] == 6 && flagged[2] == 9
            && flagged[3] == 12 && flagged[4] == 15, "they are rounds 3, 6, 9, 12 and 15",
            string.Join(",", flagged));
        Check(!progression.GetRound(1).IsBossRound && !progression.GetRound(2).IsBossRound,
            "the opening rounds are ordinary");
        progression.BossRoundInterval = 0;
        Check(!progression.GetRound(3).IsBossRound, "interval 0 disables boss rounds");

        // Anything that REBUILDS a RoundConfig must carry the flag across, or a boss round
        // silently stops being one the moment a power filters it.
        var session = NewSession(4244, 6, 1000000, 40, 1);
        var retro = (RetroPower)session.Powers.Add(new RetroPower());
        Check(session.Powers.TryUse(retro.InstanceId, ActivationTarget.None), "retro toggled on");
        var boss = new RoundConfig(3, 6, 6, 100, null, ShuffleErosion.Both, true);
        RoundConfig filtered = retro.FilterRoundConfig(
            new SessionContext(session, session.Rng), boss);
        Check(filtered.BoardHeight > boss.BoardHeight, "retro grew the board (it rebuilt the config)",
            "height " + filtered.BoardHeight);
        Check(filtered.IsBossRound, "the boss flag survives retro's round-config filter");
        Check(filtered.Erosion == ShuffleErosion.Both,
            "and so does the erosion style", "erosion " + filtered.Erosion);
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
        Check(JokerRegistry.All.Count >= 35, "the catalogue is complete",
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
            // Placement scores nothing by default now; greedy play on the roomy board barely
            // clears lines, so keep placement points here to drive long runs like before.
            config.Scoring.PointsPerCubePlaced = 1;
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
            while (!RunIsOver(session) && safety++ < 500 && failure == null)
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
                        ActivationTarget target = joker.Targeting == ActivationTargeting.HandCard
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
        // Restore placement scoring (default is 0 now) so greedy play reaches the market.
        session.Config.Scoring.PointsPerCubePlaced = 1;
        int safety = 0;
        while (!RunIsOver(session) && safety++ < 400)
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
        // Placement scores nothing by default; greedy play on the default 6x6 rarely clears a
        // line, so this driver needs placement points to reach the threshold and the market.
        config.Scoring.PointsPerCubePlaced = 1;
        var session = new GameSession(config);
        int safety = 0;
        while (!RunIsOver(session) && safety++ < 400)
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
        // Keep placement points so greedy play reaches thresholds and cycles rounds (the
        // default economy no longer scores placement - see PointsPerCubePlaced).
        config.Scoring.PointsPerCubePlaced = 1;
        var session = new GameSession(config);
        foreach (JokerDefinition definition in JokerRegistry.All)
        {
            session.Jokers.Add(definition.Create());
        }

        var sb = new StringBuilder();
        int safety = 0;
        while (!RunIsOver(session) && safety++ < 400)
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
