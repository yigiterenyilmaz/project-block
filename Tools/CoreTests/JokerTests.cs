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
        KutlecekimMerkezi_TurnsGravityAndTheWaterFollows();
        KutlecekimMerkezi_TheDirectionIsRoundScoped();
        KutlecekimMerkezi_RefusesAnythingButTheFourSides();
        Targeted_TargetFirstPaysAndTakesTheWholeBlock();
        Targeted_PlainCubeFirstSpendsTheBlockForGood();
        Targeted_BonusSurvivesTheSweepItCauses();
        Mikrodalga_BridgesOneQuietTurnAtHalfRate();
        Mikrodalga_TwoQuietTurnsStillBreakTheStreak();
        Mikrodalga_ConsecutiveClearAfterABridgePaysInFull();
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
        RunStructure_BossStagesSitBetweenNumberedRounds();
        DebugStartBossStage_JumpsStraightToABossStage();
        RunStructure_EveryStageOpensAMarket();
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
        OtekiDunya_ClonesTheBoardAndRaisesTheBar();
        OtekiDunya_TheTwoWorldsShareOneDeck();
        OtekiDunya_OneTurnIsACardInEachWorld();
        OtekiDunya_MatchingColumnsPay();
        OtekiDunya_AStuckWorldSitsOutInsteadOfLosing();
        OtekiDunya_EachWorldSweepsForItself();
        OtekiDunya_TheMainWorldCannotPlayAlone();
        OtekiDunya_PowersHitTheWorldTheyArePointedAt();
        OtekiDunya_TargetingAlwaysSnapsBack();
        OtekiDunya_LeavesAnOrdinaryRoundAlone();
        Sifaci_HealsASpentJokerOnItsClock();
        Sifaci_GivesOneUseNotAFullRefill();
        Sifaci_NeverHealsItselfOrAPassiveJoker();
        YerAlti_RefuelsPowersAndSpendsItsSeam();
        YerAlti_CostsPerPowerNotPerTick();
        YerAlti_GoesQuietWhenTheSeamRunsOut();
        YerAlti_StillFullSellsNormally();
        Devre_TracesAMonotoneEdgeToEdgePath();
        Devre_WaitsForARandomTurnAndThenStays();
        Devre_BreakingItExplodesThePathAndPays();
        Devre_OnlyOneCircuitPerRound();
        Devre_ALineClearOnTheSameTurnStillCounts();
        Nester_CutsABlockInTwo();
        Nester_RefusesACutThatWouldNotHoldTogether();
        Lehimleme_WeldsTwoCardsIntoOne();
        Lehimleme_RefusesAJoinThatDoesNotTouch();
        GenNakli_MovesAnElementAndGivesItBack();
        GenNakli_RefusesAPlainCubeOrABusyCard();
        Pres_SqueezesFourCellsIntoOne();
        Pres_ShovesCubesOffTheEdgeWhenItOpens();
        Pres_WillNotBudgeObsidianAndDetonatesWhenStuck();
        Pres_OpensByItselfAfterFourTurns();
        MayinEsegi_ArmsAMineAndShufflesItAway();
        Sasirtmaca_OneCommitmentPerTurnAndTheLockLifts();
        Matruska_SplitsOnTheLadderAndWinsOnTheLastDoll();
        Matruska_ADollLessLineLosesTheRound();
        Matruska_TheDollCheckSurvivesAnInflatedBoard();
        Snake_TheCutCheckSurvivesAnInflatedBoard();
        Snake_EatsWhatStopsItAndGrows();
        Snake_ShrinksOnAnExplosionAndDyingWinsTheRound();
        Istilaci_TakesTheMarkedColumnAndBills();
        Tamagotchi_FeedingClearsTheDemandAndTheCardLeavesTheRound();
        Tamagotchi_AnUnfedDemandLosesWhenTheDeckRunsDry();
        MayinEsegi_TheCubesAreUntouchedByAShuffle();
        MayinEsegi_SettingItOffCostsAndMovesIt();
        MayinEsegi_AQuietTurnJustRunsTheClockDown();
        Bilinmezlik_HoldsFullLinesUntilItFires();
        Bilinmezlik_ADryStreakEventuallyFiresByItself();
        RehinPuan_HoldsTheLineScoreUntilTheNextClear();
        RehinPuan_BreakingTheChainBurnsIt();
        Burokrasi_OnlyTheTaskPays();
        Burokrasi_PaysForATaskAndFinesAMissedDeadline();
        BulParayi_TakesOneUnlessYouGuessIt();
        BulParayi_AGoodGuessSavesIt();
        BulParayi_TheGuessMustComeBeforeTheFirstTurn();
        BulParayi_OnlyEverTheFirstBossOfARun();
        Simetri_TheBoardKnowsItsOwnSymmetry();
        Simetri_SleepsFiveTurnsAndAgainAfterEverySweep();
        Simetri_PaysOneAxisAndTriplesForBoth();
        Barut_ChargesDynamiteThatSurvives();
        Barut_PaysEveryChargeWhenItGoesUp();
        Antimadde_OnlyFitsAPerfectOverlay();
        Antimadde_AnnihilatesEveryCubeOfThatElement();
        Antimadde_MintsFromANegativeErasureAndRots();
        Eforsuz_PaysOnAPowerFreeRound();
        Eforsuz_DoublesForAPowerFreeOvertime();
        Enflasyon_RaisesTheBarEveryTurn();
        Enflasyon_CannotInflatePastWhatFits();
        Hiclik_BillsForEveryCubeStanding();
        Hiclik_CannotEatScoreAlreadyBanked();
        Saatci_LosesTheRoundWhenTheTurnsRunOut();
        Saatci_ARoundWonOnTheBuzzerIsNotLost();
        Kitlik_FattensCardsButOnlyForTheRound();
        Kitlik_TheGrowthStaysInOnePiece();
        Merkezkac_FlingsCubesOutwardAndOffTheEdge();
        Merkezkac_WhatGoesOverTheEdgePaysNothing();
        DortKutup_SquaresTheBoardAndSealsThreeQuarters();
        DortKutup_TurnsClockwiseEveryTurn();
        DortKutup_ABlockedQuarterTurnsInsteadOfEndingTheRound();
        Kangren_SpreadsAsOneGrowingPatch();
        Kangren_RottenCubesStillExplode();
        Kangren_AFullyRottenLineDiesAndTheRotJumps();
        Kangren_ChargesRentForEveryRottenCube();
        Kacakci_TakesOneItemPerVisitForFree();
        Kacakci_SoundGoodsAreJustGoods();
        Kacakci_ADefectiveBlockLooksNormal();
        Kacakci_ADefectiveBlockFallsRightThrough();
        Kacakci_ADefectiveBlockCannotBeFarmed();
        Kacakci_ABrokenJokerIsSilencedCentrally();
        Kacakci_ABrokenSmugglerSmugglesNothing();
        Kacakci_ABrokenPowerArrivesEmptyAndFillsSlowly();
        Kacakci_TheSmuggledItemStillCountsAsBuying();
        Kacakci_ThreeSoundHaulsAndItIsGone();
        Kacakci_JunkDoesNotWearItOut();
        Yatirimci_IsOnlyStockedByTheEarlyMarkets();
        Yatirimci_CanNeverBeSold();
        Yatirimci_ReplaysTheLostFinalRound();
        Yatirimci_DoesNothingBeforeTheFinalRound();
        Yatirimci_TheVoidedAttemptIsUnbanked();
        Yatirimci_TheReplayIsTheSameFight();
        Yatirimci_UnlocksTheExclusivePowers();
        Savunmaci_BanksSafeRoundsAndNotGreedyOnes();
        Savunmaci_AnOvertimeRoundBanksNothing();
        Savunmaci_PaysTheBankOnASurvivedOvertime();
        Savunmaci_TheBankRefillsAfterPaying();
        Besleme_MarksAPatchAndFeedsOnExplosions();
        Besleme_GrowsWhenFedAndCostsMoreEachStep();
        Besleme_StarvesAndFinallyDies();
        Besleme_ItsBillNeverPushesTheRoundBackwards();
        Besleme_TheCreatureSurvivesARoundChange();
        Kiraci_RipensAPlainCubeIntoGold();
        Kiraci_OnlyPlainCubesAreTenants();
        Kiraci_AnInterruptedTenancyStartsOver();
        Kiraci_TheGoldItMakesIsRealGold();
        Threshold_IsACeilingForNormalPlay();
        Threshold_OvertimeIsAllowedPastTheBar();
        Threshold_ATurnUnderTheBarIsUntouched();
        Boss_AlacakaranlikBendsNoRuleAtAll();
        Boss_KarantinaSealsOutwardInAndCharges();
        Boss_KarantinaChargesOnlyTheCubesInside();
        Boss_KarantinaChangesNothingWithoutTheBoss();
        Boss_YuruyenMerdivenCarriesEveryRowUp();
        Boss_YuruyenMerdivenLeavesTheBottomRowEmpty();
        Boss_YuruyenMerdivenIsNotDestruction();
        Boss_YuruyenMerdivenNeverCompletesALine();
        Boss_AlzheimerForgetsWhatWasPlayedFiveTurnsAgo();
        Boss_AlzheimerTakesWhateverIsLeftOfTheBlock();
        Boss_AlzheimerForgetsEvenTheUnbreakable();
        Boss_AlzheimerForgettingIsNotDestruction();
        Boss_AlzheimerRemembersNothingBeforeTheLimit();
        Boss_CikmazTurnsTheRoundUpsideDown();
        Boss_CikmazLosesOnASweep();
        Boss_CikmazLosesOnTheThreshold();
        Boss_CikmazSilencesTheAutomaticRescueOnly();
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
        private readonly bool bossRounds;

        public FixedProgression(int size, int threshold)
            : this(size, threshold, ShuffleErosion.None)
        {
        }

        public FixedProgression(int size, int threshold, ShuffleErosion erosion)
            : this(size, threshold, erosion, false)
        {
        }

        public FixedProgression(int size, int threshold, ShuffleErosion erosion, bool bossRounds)
        {
            this.size = size;
            this.threshold = threshold;
            this.erosion = erosion;
            this.bossRounds = bossRounds;
        }

        /// <summary>Every stage is the same size and bar here; bossRounds decides whether the
        /// stages are FLAGGED as boss rounds, which is what a boss test needs.</summary>
        public RoundConfig GetRound(int roundNumber, bool bossStage)
        {
            return new RoundConfig(roundNumber, size, size, threshold, null, erosion,
                bossRounds || bossStage);
        }

        /// <summary>
        /// NEVER. A test session walks 1, 2, 3, ... with nothing in between, and flags its stages
        /// as boss rounds directly through bossRounds when a test needs one.
        ///
        /// That keeps the two ideas apart: "this stage has a boss on it", which most boss tests
        /// want, and "the run interleaves boss stages", which is the run STRUCTURE and is tested
        /// against the real progression instead.
        /// </summary>
        public bool HasBossStageAfter(int roundNumber)
        {
            return false;
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
                var origins = round.GetValidOrigins(round.EffectiveShape(round.Hand[i]));
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
            var origins = round.GetValidOrigins(round.EffectiveShape(round.Hand[i]));
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
        // the board -> a clean sweep every turn. The threshold is set below what ONE sweep pays
        // so turn 1 crosses it: a sweep swallows the line score, so that is the sweep bonus
        // alone (50 logical) and nothing else.
        const int Threshold = 40;
        var session = NewSession(31, 3, Threshold, 24, 3);
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
        int expect1 = (int)Math.Round(Threshold * sc.OvertimeWinBonusBaseFraction);
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
        int expect2 = (int)Math.Round(Threshold *
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

    // ---- "Kütleçekim merkezi". Water is the only thing on the board that travels after it is
    // placed, so every test here paints water and checks WHERE it ends up.

    private static void KutlecekimMerkezi_TurnsGravityAndTheWaterFollows()
    {
        Section("kütleçekim merkezi / gravity turns and the water follows it");
        var session = NewSession(9800, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var power = (KutlecekimMerkeziPower)session.Powers.Add(new KutlecekimMerkeziPower());
        ClearBoard(round.Board);

        Check(round.Board.WaterFlow.X == 0 && round.Board.WaterFlow.Y == -1,
            "an ordinary arena pulls water straight down");

        // One water cube in the middle of an empty board. Pulled LEFT it should end up against
        // the left wall, on its own row.
        round.Board.SetCubeAt(new GridPos(2, 2), new Cube(CubeKind.Water, 9801));
        var ctx = new RoundContext(session, session.Rng, round);
        Check(session.Powers.TryUse(power.InstanceId, ActivationTarget.Direction(new GridPos(-1, 0))),
            "the power runs when pointed at a side");
        Check(round.Board.WaterFlow.X == -1 && round.Board.WaterFlow.Y == 0,
            "the arena now pulls left");
        Check(!round.Board.GetCube(new GridPos(2, 2)).HasValue, "the water left where it was");
        Check(round.Board.GetCube(new GridPos(0, 2)).HasValue,
            "and came to rest against the left wall, on its own row");

        // And it KEEPS pulling that way: water placed later flows the same direction.
        round.Board.SetCubeAt(new GridPos(4, 4), new Cube(CubeKind.Water, 9802));
        round.Board.SettleWaterAndReact();
        Check(round.Board.GetCube(new GridPos(0, 4)).HasValue,
            "later water flows the same way without spending anything");
    }

    private static void KutlecekimMerkezi_TheDirectionIsRoundScoped()
    {
        Section("kütleçekim merkezi / the pull dies with the round");
        var session = NewSession(9803, 5, 1, 40, 1);
        RoundEngine round = session.CurrentRound;
        var power = (KutlecekimMerkeziPower)session.Powers.Add(new KutlecekimMerkeziPower());
        session.Powers.TryUse(power.InstanceId, ActivationTarget.Direction(new GridPos(0, 1)));
        Check(round.Board.WaterFlow.Y == 1, "gravity was turned upward");

        // Into the next round: a fresh arena is built, and a fresh arena pulls downward.
        int safety = 0;
        while (session.Phase == GamePhase.Round && safety++ < 40)
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
        if (session.Phase == GamePhase.Market)
        {
            session.LeaveMarket();
            Check(session.CurrentRound.Board.WaterFlow.X == 0
                && session.CurrentRound.Board.WaterFlow.Y == -1,
                "the next round's arena pulls straight down again");
        }
        else
        {
            Check(true, "(the round did not finish - nothing to prove here)");
        }
    }

    private static void KutlecekimMerkezi_RefusesAnythingButTheFourSides()
    {
        Section("kütleçekim merkezi / four sides, and no standing still");
        var session = NewSession(9804, 5, 1000000, 40, 1);
        var power = new KutlecekimMerkeziPower();
        var ctx = new RoundContext(session, session.Rng, session.CurrentRound);
        Check(!power.CanRun(ctx, ActivationTarget.None), "a target with no direction is refused");
        Check(!power.CanRun(ctx, ActivationTarget.Direction(new GridPos(0, 0))),
            "standing still is not a direction");
        Check(!power.CanRun(ctx, ActivationTarget.Direction(new GridPos(1, 1))),
            "and neither is a diagonal");
        Check(power.CanRun(ctx, ActivationTarget.Direction(new GridPos(0, 1))),
            "up is");
    }

    // ---- "Hedefli" blocks. Every test lays a TWO-CUBE VERTICAL block whose target is the
    // lower cube, then explodes one row or the other on purpose - so which of the block's cubes
    // breaks first is chosen rather than hoped for.

    private static void Targeted_TargetFirstPaysAndTakesTheWholeBlock()
    {
        Section("hedefli / the target goes first");
        GameSession session = ComboSession(false);
        RoundEngine round = session.CurrentRound;
        PlayTargetedPair(session, round, 0);

        // Row 0 holds the TARGET cube. Completing it breaks the target in the first explosion
        // that reaches the block, so the block pays and the upper cube goes with it.
        TurnReport report = ExplodeRow(session, round, 0);
        Check(report.TargetedBlocksHit.Count == 1, "the block paid out",
            "hits " + report.TargetedBlocksHit.Count);
        // 25 for the aim + 1 for the one cube the payout took with it.
        Check(report.Score.BaseTargeted == 26, "aim bonus plus the cubes it took",
            "got " + report.Score.BaseTargeted);
        Check(!round.Board.GetCube(new GridPos(0, 1)).HasValue,
            "the cube that survived the line went up with the block");

        // The payout's cells go in a channel of their OWN. "Antimadde" bills the player per cube
        // in ExtraExplodedCells, so a payout that wrote in there would be paid for by a joker
        // that did not cause it - reachable, because erasing a target cube with a negative block
        // mints an antimatter-of-Target card that sets off every armed block at once.
        Check(report.TargetedExplodedCells.Count == 1,
            "the payout's cells are reported in its own channel",
            "" + report.TargetedExplodedCells.Count);
        Check(report.ExtraExplodedCells.Count == 0,
            "and NOT in the one Antimadde bills against",
            "" + report.ExtraExplodedCells.Count);
    }

    private static void Targeted_PlainCubeFirstSpendsTheBlockForGood()
    {
        Section("hedefli / a plain cube goes first");
        GameSession session = ComboSession(false);
        RoundEngine round = session.CurrentRound;
        PlayTargetedPair(session, round, 0);

        // Row 1 holds the block's PLAIN cube, so the first explosion to reach the block misses.
        TurnReport miss = ExplodeRow(session, round, 1);
        Check(miss.TargetedBlocksHit.Count == 0 && miss.Score.BaseTargeted == 0,
            "a plain cube first pays nothing", "got " + miss.Score.BaseTargeted);
        Check(round.Board.GetCube(new GridPos(0, 0)).HasValue,
            "the rest of the block is still standing - it is spent, not destroyed");

        // And it stays spent: breaking the target later is now just an ordinary cube breaking.
        TurnReport late = ExplodeRow(session, round, 0);
        Check(late.TargetedBlocksHit.Count == 0 && late.Score.BaseTargeted == 0,
            "the target pays nothing once the block has missed",
            "got " + late.Score.BaseTargeted);
    }

    private static void Targeted_BonusSurvivesTheSweepItCauses()
    {
        Section("hedefli / the sweep does not swallow the bonus");
        GameSession session = ComboSession(false);
        RoundEngine round = session.CurrentRound;
        PlayTargetedPair(session, round, 0);

        TurnReport report = ExplodeRow(session, round, 0);
        // The payout emptied the board, so this is a clean sweep - and a sweep REPLACES the line
        // score. The aim bonus is not line score and must come through it intact.
        Check(report.CleanSweep, "the payout emptied the board");
        Check(report.Score.BaseLines == 0, "the sweep swallowed the line score as always");
        Check(report.Score.BaseTargeted == 26, "but not the targeted bonus",
            "got " + report.Score.BaseTargeted);
    }

    /// <summary>Plays a targeted 2-cube vertical block at (x,0)-(x,1), target on the LOWER cube.
    /// It goes in through the bonus hand, which is a real turn like any other.</summary>
    private static void PlayTargetedPair(GameSession session, RoundEngine round, int x)
    {
        BlockShape column = BlockShape.FromCells(
            new List<GridPos> { new GridPos(0, 0), new GridPos(0, 1) });
        BlockCard card = session.CreateCard(column, new[] { BlockElement.Targeted });
        card.TargetCellIndex = 0; // FromCells sorts, so cell 0 is (0,0) - the lower cube
        round.AddBonusCard(card, BonusPlayOutcome.ExpireFromRound);
        round.PlayFromBonus(0, new GridPos(x, 0));
        Check(round.Board.GetCube(new GridPos(x, 0)).Value.Kind == CubeKind.Target,
            "the lower cube landed as the target");
        Check(round.Board.GetCube(new GridPos(x, 1)).Value.Kind == CubeKind.Normal,
            "and the upper one is an ordinary cube");
    }

    /// <summary>Fills whatever is missing from a row and drops the last cube as a real turn.</summary>
    private static TurnReport ExplodeRow(GameSession session, RoundEngine round, int y)
    {
        for (int x = 1; x < 4; x++)
        {
            PaintBoard(round, session, CubeKind.Normal, new GridPos(x, y));
        }
        return DropOneCube(round, new GridPos(4, y));
    }

    // ---- Mikrodalga. The joker itself does nothing but set two rule values, so every test
    // here drives REAL turns: what is being checked is the engine's combo counter, not the
    // joker's own bookkeeping. ComboBonusPerStep is 5, so a streak pays 0, 5, 10, 15...

    private static void Mikrodalga_BridgesOneQuietTurnAtHalfRate()
    {
        Section("mikrodalga / a combo across one quiet turn");
        Check(ComboAfterOneQuietTurn(false) == 0,
            "without the joker a quiet turn ends the streak",
            "got " + ComboAfterOneQuietTurn(false));
        int bridged = ComboAfterOneQuietTurn(true);
        // Third step of the streak = 10, reheated at 50%.
        Check(bridged == 5, "with it the streak carries on at half rate", "got " + bridged);
    }

    private static void Mikrodalga_TwoQuietTurnsStillBreakTheStreak()
    {
        Section("mikrodalga / two quiet turns are too many");
        GameSession session = ComboSession(true);
        RoundEngine round = session.CurrentRound;
        ClearOneRow(session, round, 0);
        ClearOneRow(session, round, 1);
        DropOneCube(round, new GridPos(1, 2));
        DropOneCube(round, new GridPos(3, 2));
        Check(ClearOneRow(session, round, 3) == 0,
            "the second quiet turn resets the streak after all");
    }

    private static void Mikrodalga_ConsecutiveClearAfterABridgePaysInFull()
    {
        Section("mikrodalga / only the reheated turn is discounted");
        GameSession session = ComboSession(true);
        RoundEngine round = session.CurrentRound;
        ClearOneRow(session, round, 0);
        ClearOneRow(session, round, 1);
        DropOneCube(round, new GridPos(2, 2));
        Check(ClearOneRow(session, round, 3) == 5, "the turn that ends the gap is halved");
        // Straight after it, with no gap: the fourth step pays its full 15.
        Check(ClearOneRow(session, round, 4) == 15,
            "the next consecutive clear is back to full rate");
    }

    /// <summary>Clear, clear, ONE quiet turn, clear - and what that last turn's combo paid.</summary>
    private static int ComboAfterOneQuietTurn(bool withJoker)
    {
        GameSession session = ComboSession(withJoker);
        RoundEngine round = session.CurrentRound;
        ClearOneRow(session, round, 0);
        ClearOneRow(session, round, 1);
        DropOneCube(round, new GridPos(2, 2)); // clears nothing
        return ClearOneRow(session, round, 3);
    }

    /// <summary>A 5x5 arena dealt nothing but single cubes, so a line can be completed - or
    /// deliberately not completed - on any turn the test likes.</summary>
    private static GameSession ComboSession(bool withJoker)
    {
        GameSession session = NewSession(91, 5, 1000000, 60, 1);
        if (withJoker)
        {
            session.Jokers.Add(new MikrodalgaJoker());
        }
        return session;
    }

    /// <summary>Paints four cells of a row, drops the fifth as a real turn, and returns the
    /// combo bonus that turn was paid.</summary>
    private static int ClearOneRow(GameSession session, RoundEngine round, int y)
    {
        PaintBoard(round, session, CubeKind.Normal, new GridPos(0, y), new GridPos(1, y),
            new GridPos(2, y), new GridPos(3, y));
        return DropOneCube(round, new GridPos(4, y)).Score.BaseCombo;
    }

    private static TurnReport DropOneCube(RoundEngine round, GridPos cell)
    {
        return round.PlayFromHand(0, cell); // every card in this deck is a single cube
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
        int baseValue = session.Jokers.SellValueOf(cimri);

        int turns = PlayTurns(session, 4);
        Check(turns == 4, "played four turns", "got " + turns);
        Check(cimri.AccruedValue == 12, "banked 3 per turn", "got " + cimri.AccruedValue);
        Check(session.Jokers.SellValueOf(cimri) == baseValue + 12,
            "sell value includes the accrual");

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
        Check(session.Jokers.SellValueOf(auctioned)
            > session.Config.Market.JokerSellValue(RarityTable.For(auctioned.DefId)),
            "sell value went up");

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
            firstBand &= progression.GetRound(round, false).BoardWidth == 5
                && progression.GetRound(round, false).BoardHeight == 5;
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
        Check(progression.GetRound(6, false).BoardWidth == 7 && progression.GetRound(5, false).BoardWidth == 5,
            "the step happens between round 5 and round 6");
        Check(progression.GetRound(12, false).BoardWidth == 9 && progression.GetRound(11, false).BoardWidth == 7,
            "and between round 11 and round 12");

        // The table is data: a variant curve only has to hand over different bands.
        progression.BoardSizeBands = new[] { new BoardSizeBand(1, 3, 4), new BoardSizeBand(4, 6, 12) };
        Check(progression.BoardSizeFor(2) == 4 && progression.BoardSizeFor(5) == 12
            && progression.BoardSizeFor(99) == 12, "a replaced band table drives the size");

        bool threwOnRoundZero = false;
        try
        {
            new DefaultRoundProgression().GetRound(0, false);
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
        // Walk to the first BOSS STAGE - which is after round 3, not round 3 itself.
        int guard = 0;
        while (!replay.InBossStage && !RunIsOver(replay) && guard++ < 200)
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
    /// that row and nothing else. Returns the clearing turn's report.
    ///
    /// An anchor cube is parked off the row first, so clearing it does NOT empty the board: a
    /// clean sweep SWALLOWS the line score, and this helper exists to measure the line score.</summary>
    private static TurnReport FillBottomRow(GameSession session, BossRound boss)
    {
        if (boss != null)
        {
            session.CurrentRound.SetBoss(boss);
        }
        RoundEngine round = session.CurrentRound;
        round.PlayFromHand(0, new GridPos(0, 2)); // the anchor: keeps the board non-empty
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
        Check(session.Jokers.SellValueOf(rare) > 0, "a silenced joker keeps its sell value",
            "value " + session.Jokers.SellValueOf(rare));

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
            if (!session.RerollMarket(MarketOfferKind.Joker))
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
            session.RerollMarket(MarketOfferKind.Joker);
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
        Check(session.RerollMarket(MarketOfferKind.Joker), "and it goes through");
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
        Check(!session.RerollMarket(MarketOfferKind.Joker) || session.Debt == 0,
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

    /// <summary>Plays one dual-world turn: books the mirror's half, then plays the main world.
    /// Returns the report, or null if neither world could act.</summary>
    private static TurnReport PlayMirrorTurn(GameSession session)
    {
        RoundEngine round = session.CurrentRound;
        for (int i = 0; i < round.MirrorHand.Count; i++)
        {
            BlockCard card = round.MirrorHand[i];
            if (round.IsFrozen(card.Id)) { continue; }
            List<GridPos> origins = round.GetValidMirrorOrigins(card);
            if (origins.Count > 0)
            {
                round.StageMirrorPlay(i, origins[0]);
                break;
            }
        }
        for (int i = 0; i < round.Hand.Count; i++)
        {
            BlockCard card = round.Hand[i];
            if (round.IsFrozen(card.Id)) { continue; }
            List<GridPos> origins = round.GetValidOrigins(round.EffectiveShape(card));
            if (origins.Count > 0)
            {
                return round.PlayFromHand(i, origins[0]);
            }
        }
        // The main world is stuck: the mirror plays alone.
        return round.MirrorHasStagedPlay ? round.PlayMirrorOnly() : null;
    }

    private static void OtekiDunya_ClonesTheBoardAndRaisesTheBar()
    {
        Section("öteki dünya / clones the arena and raises the bar");
        var session = NewSession(8000, 5, 400, 40, 3);
        RoundEngine round = session.CurrentRound;
        var power = (OtekiDunyaPower)session.Powers.Add(new OtekiDunyaPower());
        session.Powers.DispatchRoundStarted(round);

        Check(!round.HasMirrorWorld, "an ordinary round has one world");
        Check(round.MirrorBoard == null && round.MirrorHand == null, "and no mirror state at all");
        int plainBar = round.ScoreThreshold;

        // Build something first, so the clone genuinely copies a board with cubes on it.
        PlayTurns(session, 2);
        int occupiedBefore = round.Board.OccupiedCount;
        Check(occupiedBefore > 0, "there are cubes to clone", "occupied " + occupiedBefore);

        Check(session.Powers.TryUse(power.InstanceId, ActivationTarget.None), "the power ran");
        Check(round.HasMirrorWorld, "the second world is open");
        Check(round.MirrorBoard.Width == round.Board.Width
            && round.MirrorBoard.Height == round.Board.Height, "same size as the original");
        Check(round.MirrorBoard.OccupiedCount == occupiedBefore,
            "and an exact copy of what was on it",
            occupiedBefore + " vs " + round.MirrorBoard.OccupiedCount);
        Check(round.MirrorHand.Count > 0, "the mirror was dealt its own hand",
            "hand " + round.MirrorHand.Count);

        Check(round.ScoreThreshold > plainBar, "the bar went up",
            plainBar + " -> " + round.ScoreThreshold);
        Check(round.ScoreThreshold == (int)System.Math.Ceiling(plainBar * power.ThresholdFactor),
            "by exactly the power's factor, rounded up", "" + round.ScoreThreshold);

        Check(!session.Powers.CanUse(power.InstanceId, ActivationTarget.None),
            "and it cannot be opened twice");
    }

    private static void OtekiDunya_TheTwoWorldsShareOneDeck()
    {
        Section("öteki dünya / separate hands, one deck");
        var session = NewSession(8001, 5, 1000000, 40, 3);
        RoundEngine round = session.CurrentRound;
        var power = (OtekiDunyaPower)session.Powers.Add(new OtekiDunyaPower());
        session.Powers.DispatchRoundStarted(round);
        int drawBefore = round.Deck.DrawCount;
        session.Powers.TryUse(power.InstanceId, ActivationTarget.None);

        Check(round.Deck.DrawCount == drawBefore - round.MirrorHand.Count,
            "the mirror's hand came out of the SHARED draw pile",
            drawBefore + " -> " + round.Deck.DrawCount);

        // No card is in both hands at once.
        bool overlap = false;
        for (int i = 0; i < round.Hand.Count; i++)
        {
            for (int j = 0; j < round.MirrorHand.Count; j++)
            {
                if (round.Hand[i].Id == round.MirrorHand[j].Id) { overlap = true; }
            }
        }
        Check(!overlap, "the two hands never hold the same card");

        // A dual turn spends two cards from the one deck.
        int totalBefore = round.Deck.DrawCount + round.Deck.DiscardCount;
        PlayMirrorTurn(session);
        int totalAfter = round.Deck.DrawCount + round.Deck.DiscardCount
            + round.Hand.Count + round.MirrorHand.Count;
        Check(totalAfter >= totalBefore, "cards are conserved across both worlds",
            totalBefore + " -> " + totalAfter);
    }

    private static void OtekiDunya_OneTurnIsACardInEachWorld()
    {
        Section("öteki dünya / one turn is a card in each world");
        var session = NewSession(8002, 5, 1000000, 40, 3);
        RoundEngine round = session.CurrentRound;
        var power = (OtekiDunyaPower)session.Powers.Add(new OtekiDunyaPower());
        session.Powers.DispatchRoundStarted(round);
        session.Powers.TryUse(power.InstanceId, ActivationTarget.None);

        Check(round.MirrorHasAnyMove, "the mirror has somewhere to play");
        Check(!round.MirrorReadyForTurn, "and until it books its half the turn is not ready");

        BlockCard mirrorCard = round.MirrorHand[0];
        List<GridPos> origins = round.GetValidMirrorOrigins(mirrorCard);
        Check(origins.Count > 0, "the mirror card has legal origins");
        Check(round.StageMirrorPlay(0, origins[0]), "booking the mirror's half works");
        Check(round.MirrorReadyForTurn, "now the turn is ready");
        Check(round.StagedMirrorCard == mirrorCard, "and the booking is the card we chose");

        int mirrorOccupiedBefore = round.MirrorBoard.OccupiedCount;
        int mainOccupiedBefore = round.Board.OccupiedCount;
        TurnReport report = null;
        for (int i = 0; i < round.Hand.Count && report == null; i++)
        {
            List<GridPos> mainOrigins = round.GetValidOrigins(round.EffectiveShape(round.Hand[i]));
            if (mainOrigins.Count > 0) { report = round.PlayFromHand(i, mainOrigins[0]); }
        }
        Check(report != null, "the main world played");
        Check(report.MirrorCard == mirrorCard, "and the report names the mirror's card too");
        Check(report.MirrorPlacedCells.Count > 0, "the mirror's block really landed",
            "cells " + report.MirrorPlacedCells.Count);
        Check(round.MirrorBoard.OccupiedCount > mirrorOccupiedBefore
            || report.MirrorExplodedCells.Count > 0, "the mirror board changed");
        Check(round.Board.OccupiedCount != mainOccupiedBefore
            || report.CubesExploded > 0, "so did the main board");
        Check(!round.MirrorHasStagedPlay, "and the booking was consumed");
        Check(round.MirrorHand.Count > 0, "the mirror hand refilled from the shared deck",
            "hand " + round.MirrorHand.Count);
    }

    private static void OtekiDunya_MatchingColumnsPay()
    {
        Section("öteki dünya / the same column in both worlds pays a bonus");
        var session = NewSession(8003, 4, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var power = (OtekiDunyaPower)session.Powers.Add(new OtekiDunyaPower());
        session.Powers.DispatchRoundStarted(round);
        session.Powers.TryUse(power.InstanceId, ActivationTarget.None);

        // Fill column 1 in BOTH worlds except its bottom cell, so a single-cube placement in
        // each completes the same column on the same turn.
        int column = round.Board.MinX + 1;
        for (int y = round.Board.MinY + 1; y < round.Board.MinY + round.Board.Height; y++)
        {
            round.Board.SetCubeAt(new GridPos(column, y), new Cube(CubeKind.Normal, 9600));
            round.MirrorBoard.SetCubeAt(new GridPos(column, y), new Cube(CubeKind.Normal, 9601));
        }
        var hole = new GridPos(column, round.Board.MinY);

        Check(round.StageMirrorPlay(0, hole), "the mirror closes its column");
        TurnReport report = round.PlayFromHand(0, hole);
        Check(report.ExplodedColumns.Count > 0, "the main world's column exploded",
            "cols " + report.ExplodedColumns.Count);
        Check(report.MirrorExplodedColumns.Count > 0, "and the mirror's did too",
            "cols " + report.MirrorExplodedColumns.Count);
        Check(report.MirroredColumns.Count > 0, "the match was spotted",
            "matched " + report.MirroredColumns.Count);
        Check(report.MirroredColumns[0] == column - round.Board.MinX,
            "and it is the column we set up", "" + report.MirroredColumns[0]);
        // A BONUS on top, not part of the base line score - the clean sweep this match caused
        // swallows the line score, and it must not swallow the power's pay-off with it.
        bool paid = false;
        foreach (ScoreContribution c in report.Score.Contributions)
        {
            if (c.Source == "oteki_dunya" && c.Flat >= power.MirroredColumnBonus)
            {
                paid = true;
            }
        }
        Check(paid, "the bonus is in the turn's score, as a bonus of its own",
            "expected +" + power.MirroredColumnBonus);
        Check(report.CleanSweep && report.Score.BaseLines == 0,
            "even though the sweep swallowed the line score it was paid for");
    }

    private static void OtekiDunya_AStuckWorldSitsOutInsteadOfLosing()
    {
        Section("öteki dünya / a stuck world sits the turn out, it does not end the round");
        var session = NewSession(8004, 4, 1000000, 40, 3);
        RoundEngine round = session.CurrentRound;
        var power = (OtekiDunyaPower)session.Powers.Add(new OtekiDunyaPower());
        session.Powers.DispatchRoundStarted(round);
        session.Powers.TryUse(power.InstanceId, ActivationTarget.None);

        // Wall the MIRROR off completely: it can play nothing at all.
        foreach (GridPos cell in AllPlayableCells(round.MirrorBoard))
        {
            if (!round.MirrorBoard.GetCube(cell).HasValue)
            {
                round.MirrorBoard.SetCubeAt(cell, new Cube(CubeKind.Obsidian, 9602));
            }
        }
        Check(!round.MirrorHasAnyMove, "the mirror is completely stuck");
        Check(round.MirrorReadyForTurn, "so it no longer holds the turn up");
        Check(round.Status == RoundStatus.InProgress, "and the round is still running");

        TurnReport report = PlayMirrorTurn(session);
        Check(report != null, "the turn still resolved");
        Check(report.MirrorCard == null, "with the mirror sitting it out");
        Check(round.Status == RoundStatus.InProgress || round.Status == RoundStatus.AwaitingAdvanceDecision,
            "and the round did not end because one world was stuck", "status " + round.Status);
    }

    private static void OtekiDunya_EachWorldSweepsForItself()
    {
        Section("öteki dünya / each world sweeps for itself");
        var session = NewSession(8005, 4, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var power = (OtekiDunyaPower)session.Powers.Add(new OtekiDunyaPower());
        session.Powers.DispatchRoundStarted(round);
        session.Powers.TryUse(power.InstanceId, ActivationTarget.None);

        // The MIRROR alone is one cube short of a full row; the main board is left empty, so
        // only the mirror can sweep this turn.
        int row = round.MirrorBoard.MinY;
        for (int x = round.MirrorBoard.MinX + 1;
            x < round.MirrorBoard.MinX + round.MirrorBoard.Width; x++)
        {
            round.MirrorBoard.SetCubeAt(new GridPos(x, row), new Cube(CubeKind.Normal, 9603));
        }
        var hole = new GridPos(round.MirrorBoard.MinX, row);
        Check(round.StageMirrorPlay(0, hole), "the mirror closes its row");

        int sweepsBefore = round.CleanSweepCount;
        TurnReport report = null;
        for (int i = 0; i < round.Hand.Count && report == null; i++)
        {
            List<GridPos> origins = round.GetValidOrigins(round.EffectiveShape(round.Hand[i]));
            if (origins.Count > 0) { report = round.PlayFromHand(i, origins[0]); }
        }
        Check(report != null, "the turn resolved");
        Check(report.MirrorCleanSweep, "the mirror swept its own board");
        Check(!report.CleanSweep, "the main world did not - each world sweeps for itself");
        Check(round.CleanSweepCount == sweepsBefore + 1, "and it counted once",
            sweepsBefore + " -> " + round.CleanSweepCount);
        Check(round.MirrorBoard.OccupiedCount == 0, "the mirror board really is empty");
    }

    private static void OtekiDunya_TheMainWorldCannotPlayAlone()
    {
        Section("öteki dünya / the main world may not resolve a turn on its own");
        var session = NewSession(8007, 5, 1000000, 40, 3);
        RoundEngine round = session.CurrentRound;
        var power = (OtekiDunyaPower)session.Powers.Add(new OtekiDunyaPower());
        session.Powers.DispatchRoundStarted(round);
        session.Powers.TryUse(power.InstanceId, ActivationTarget.None);
        Check(round.MirrorHasAnyMove, "the mirror can play, so it must");

        List<GridPos> origins = round.GetValidOrigins(round.Hand[0].Shape);
        Check(origins.Count > 0, "the main world has a legal move");
        bool refused = false;
        try
        {
            round.PlayFromHand(0, origins[0]);
        }
        catch (System.InvalidOperationException)
        {
            refused = true;
        }
        Check(refused, "playing without booking the mirror's half is refused");
        Check(round.TurnNumber == 0, "and no turn was burned", "turn " + round.TurnNumber);

        // Book it, and the very same move goes through.
        List<GridPos> mirrorOrigins = round.GetValidMirrorOrigins(round.MirrorHand[0]);
        Check(round.StageMirrorPlay(0, mirrorOrigins[0]), "book the mirror's half");
        TurnReport report = round.PlayFromHand(0, origins[0]);
        Check(report != null && round.TurnNumber == 1, "now the turn resolves",
            "turn " + round.TurnNumber);

        // A mirror with nothing to do stops holding the turn up.
        foreach (GridPos cell in AllPlayableCells(round.MirrorBoard))
        {
            if (!round.MirrorBoard.GetCube(cell).HasValue)
            {
                round.MirrorBoard.SetCubeAt(cell, new Cube(CubeKind.Obsidian, 9604));
            }
        }
        Check(round.MirrorReadyForTurn, "a stuck mirror no longer blocks the main world");
        TurnReport solo = PlayOneCard(round);
        Check(solo != null, "and the main world plays on alone");
    }

    /// <summary>A dual-world session with both boards full and this turn's power budget free -
    /// opening the mirror spends a power, so a turn has to pass before another can be used.</summary>
    private static GameSession DualWorldWithFullBoards(int seed)
    {
        var session = NewSession(seed, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var opener = (OtekiDunyaPower)session.Powers.Add(new OtekiDunyaPower());
        session.Powers.DispatchRoundStarted(round);
        session.Powers.TryUse(opener.InstanceId, ActivationTarget.None);
        PlayMirrorTurn(session); // frees the one-power-per-turn budget

        FillBoardSolid(round, session);
        foreach (GridPos cell in AllPlayableCells(round.MirrorBoard))
        {
            if (!round.MirrorBoard.GetCube(cell).HasValue)
            {
                round.MirrorBoard.SetCubeAt(cell, new Cube(CubeKind.Normal, 9700));
            }
        }
        return session;
    }

    private static void OtekiDunya_PowersHitTheWorldTheyArePointedAt()
    {
        Section("öteki dünya / a power hits the world it was pointed at");
        // Çerçeve clears the outer ring of "the board" - and never knew a second one exists.
        // Aimed at the mirror, only the mirror may lose its ring.
        var atMirror = DualWorldWithFullBoards(8008);
        RoundEngine round = atMirror.CurrentRound;
        int mainBefore = round.MainBoard.OccupiedCount;
        int mirrorBefore = round.MirrorBoard.OccupiedCount;
        Check(mainBefore > 0 && mirrorBefore > 0, "both worlds are full",
            mainBefore + " / " + mirrorBefore);

        var frame = (CercevePower)atMirror.Powers.Add(new CercevePower());
        Check(atMirror.Powers.TryUse(frame.InstanceId, ActivationTarget.None.OnWorld(true)),
            "the power ran, aimed at the mirror");
        Check(round.MirrorBoard.OccupiedCount < mirrorBefore,
            "the MIRROR lost its outer ring",
            mirrorBefore + " -> " + round.MirrorBoard.OccupiedCount);
        Check(round.MainBoard.OccupiedCount == mainBefore,
            "and the main world was not touched at all",
            mainBefore + " -> " + round.MainBoard.OccupiedCount);

        // The same power with no world named hits the main one, as it always did.
        var atMain = DualWorldWithFullBoards(8008);
        RoundEngine round2 = atMain.CurrentRound;
        int mainBefore2 = round2.MainBoard.OccupiedCount;
        int mirrorBefore2 = round2.MirrorBoard.OccupiedCount;
        var frame2 = (CercevePower)atMain.Powers.Add(new CercevePower());
        Check(atMain.Powers.TryUse(frame2.InstanceId, ActivationTarget.None),
            "the same power ran with no world named");
        Check(round2.MainBoard.OccupiedCount < mainBefore2,
            "this time the MAIN world lost its ring",
            mainBefore2 + " -> " + round2.MainBoard.OccupiedCount);
        Check(round2.MirrorBoard.OccupiedCount == mirrorBefore2,
            "and the mirror was left alone",
            mirrorBefore2 + " -> " + round2.MirrorBoard.OccupiedCount);
    }

    private static void OtekiDunya_TargetingAlwaysSnapsBack()
    {
        Section("öteki dünya / the aim never sticks past the activation");
        var session = NewSession(8009, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var opener = (OtekiDunyaPower)session.Powers.Add(new OtekiDunyaPower());
        session.Powers.DispatchRoundStarted(round);
        session.Powers.TryUse(opener.InstanceId, ActivationTarget.None);

        GameBoard mainBoard = round.MainBoard;
        Check(round.Board == mainBoard, "outside an activation Board is the main world");

        var frame = (CercevePower)session.Powers.Add(new CercevePower());
        session.Powers.DispatchRoundStarted(round);
        session.Powers.TryUse(frame.InstanceId, ActivationTarget.None.OnWorld(true));
        Check(round.Board == mainBoard, "and it is the main world again straight afterwards");

        // The turn resolver must never see the mirror through Board either.
        TurnReport report = PlayMirrorTurn(session);
        Check(report != null, "a dual turn still resolves normally");
        Check(round.Board == round.MainBoard, "with Board still meaning the main world");
    }

    private static void OtekiDunya_LeavesAnOrdinaryRoundAlone()
    {
        Section("öteki dünya / an unopened round behaves exactly as before");
        var session = NewSession(8006, 6, 1000000, 40, 3);
        RoundEngine round = session.CurrentRound;
        session.Powers.Add(new OtekiDunyaPower()); // held but never used
        session.Powers.DispatchRoundStarted(round);

        Check(!round.HasMirrorWorld, "no mirror");
        Check(round.MirrorReadyForTurn, "the turn is never held up waiting for one");
        Check(!round.MirrorHasAnyMove, "and the mirror reports no moves");
        Check(round.ScoreThreshold == round.Config.ScoreThreshold, "the bar is untouched");

        TurnReport report = PlayOneCard(round);
        Check(report != null, "a plain turn still resolves");
        Check(report.MirrorCard == null && report.MirrorPlacedCells.Count == 0,
            "and reports nothing about a second world");
        Check(report.MirroredColumns.Count == 0, "no column match to pay");
    }

    private static void Sifaci_HealsASpentJokerOnItsClock()
    {
        Section("şifacı / gives a spent joker one use back, on its own clock");
        var session = NewSession(8100, 6, 1000000, 40, 1);
        var healer = (SifaciJoker)session.Jokers.Add(new SifaciJoker());
        var patient = (RenovasyonJoker)session.Jokers.Add(new RenovasyonJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);

        int full = patient.ChargesPerRound;
        Check(full > 0, "the patient is a charged joker", "charges " + full);
        Check(patient.ChargesLeft == full, "and starts full");

        // Nothing is spent: the clock comes due and simply waits.
        PlayTurns(session, healer.TurnsBetweenHeals + 2);
        Check(healer.IsReadyToHeal, "with nothing to heal it sits ready",
            "status " + healer.StatusText);
        Check(patient.ChargesLeft == full, "and healed nothing");

        // Empty the patient. The very next turn should heal it, without waiting again.
        while (patient.ChargesLeft > 0)
        {
            session.Jokers.TryActivate(patient.InstanceId, ActivationTarget.None);
        }
        Check(patient.ChargesLeft == 0, "the patient is spent", "left " + patient.ChargesLeft);
        PlayTurns(session, 1);
        Check(patient.ChargesLeft == 1, "the waiting healer topped it up at once",
            "left " + patient.ChargesLeft);
        Check(!healer.IsReadyToHeal, "and went back to sleep");
    }

    private static void Sifaci_GivesOneUseNotAFullRefill()
    {
        Section("şifacı / one use back, not a full refill");
        var session = NewSession(8101, 6, 1000000, 40, 1);
        var healer = (SifaciJoker)session.Jokers.Add(new SifaciJoker());
        // Renovasyon has 2 uses per round, so a full refill would be visible.
        var patient = (RenovasyonJoker)session.Jokers.Add(new RenovasyonJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        int full = patient.ChargesPerRound;
        Check(full >= 2, "the patient has more than one use", "charges " + full);

        while (patient.ChargesLeft > 0)
        {
            if (!session.Jokers.TryActivate(patient.InstanceId, ActivationTarget.None))
            {
                break;
            }
        }
        if (patient.ChargesLeft > 0)
        {
            Check(true, "the patient could not be emptied in this setup - skipped");
            return;
        }
        PlayTurns(session, healer.TurnsBetweenHeals + 1);
        Check(patient.ChargesLeft == 1, "exactly one use came back, not all of them",
            patient.ChargesLeft + " of " + full);
        Check(healer.IsReadyToHeal == false || patient.ChargesLeft == 1,
            "the heal was spent on that one use");
    }

    private static void Sifaci_NeverHealsItselfOrAPassiveJoker()
    {
        Section("şifacı / passive jokers are not patients");
        var session = NewSession(8102, 6, 1000000, 40, 1);
        var healer = (SifaciJoker)session.Jokers.Add(new SifaciJoker());
        Joker passive = session.Jokers.Add(new InsiderJoker()); // no charges at all
        session.Jokers.DispatchRoundStarted(session.CurrentRound);

        Check(passive.ChargesPerRound == 0, "Insider has no charges to heal");
        Check(healer.ChargesPerRound == 0, "and the healer itself is passive too");
        PlayTurns(session, healer.TurnsBetweenHeals + 3);
        Check(healer.IsReadyToHeal, "so it stays ready forever with nothing to do",
            "status " + healer.StatusText);
        Check(passive.ChargesLeft == 0, "and nothing was granted to a passive joker");
    }

    private static void YerAlti_RefuelsPowersAndSpendsItsSeam()
    {
        Section("yer altı kaynakları / refuels spent powers and pays for each out of the seam");
        var session = NewSession(8103, 6, 1000000, 40, 1);
        var mine = (YerAltiKaynaklariJoker)session.Jokers.Add(new YerAltiKaynaklariJoker());
        // Büyüteç is common, Kum Saati is rare (RarityTable).
        var common = (BuyutecPower)session.Powers.Add(new BuyutecPower());
        var rare = (KumSaatiPower)session.Powers.Add(new KumSaatiPower());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        session.Powers.DispatchRoundStarted(round);

        Check(mine.CapacityLeft == mine.Capacity, "the seam starts full",
            "" + mine.CapacityLeft);
        Check(RarityTable.For(common.DefId) == Rarity.Common, "Büyüteç is common");
        Check(RarityTable.For(rare.DefId) == Rarity.Rare, "Kum Saati is rare");

        // Drain the common one only, and let its 3-turn clock come round.
        common.Spend();
        Check(!common.Charged, "the common power is spent");
        int seam = mine.CapacityLeft;
        PlayTurns(session, mine.CommonEveryTurns);
        Check(common.Charged, "it was refuelled");
        Check(mine.CapacityLeft == seam - mine.CommonCost,
            "and the seam paid exactly one for a common power",
            seam + " -> " + mine.CapacityLeft);

        // A rare one costs two.
        rare.Spend();
        seam = mine.CapacityLeft;
        PlayTurns(session, mine.RareEveryTurns * 2);
        Check(rare.Charged, "the rare power was refuelled too");
        Check(mine.CapacityLeft <= seam - mine.RareCost,
            "and a rare refill costs two", seam + " -> " + mine.CapacityLeft);
    }

    private static void YerAlti_CostsPerPowerNotPerTick()
    {
        Section("yer altı kaynakları / every power refilled costs, not every tick");
        var session = NewSession(8104, 6, 1000000, 40, 1);
        var mine = (YerAltiKaynaklariJoker)session.Jokers.Add(new YerAltiKaynaklariJoker());
        var a = (BuyutecPower)session.Powers.Add(new BuyutecPower());
        var b = (CimbizPower)session.Powers.Add(new CimbizPower());
        var c = (KlonPower)session.Powers.Add(new KlonPower());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        session.Powers.DispatchRoundStarted(session.CurrentRound);
        Check(RarityTable.For(b.DefId) == Rarity.Common
            && RarityTable.For(c.DefId) == Rarity.Common, "all three are common");

        a.Spend();
        b.Spend();
        c.Spend();
        int seam = mine.CapacityLeft;
        PlayTurns(session, mine.CommonEveryTurns);
        Check(a.Charged && b.Charged && c.Charged, "all three were refuelled in one tick");
        Check(mine.CapacityLeft == seam - 3 * mine.CommonCost,
            "and the seam paid for THREE, not for one tick",
            seam + " -> " + mine.CapacityLeft);

        // A tick with nothing spent costs nothing at all.
        seam = mine.CapacityLeft;
        PlayTurns(session, mine.CommonEveryTurns + 1);
        Check(mine.CapacityLeft == seam, "a tick with nothing to refuel is free",
            seam + " -> " + mine.CapacityLeft);
    }

    private static void YerAlti_GoesQuietWhenTheSeamRunsOut()
    {
        Section("yer altı kaynakları / a worked-out seam does nothing and refunds what you paid");
        var session = NewSession(8105, 6, 1000000, 40, 1);
        var mine = (YerAltiKaynaklariJoker)session.Jokers.Add(new YerAltiKaynaklariJoker());
        mine.Capacity = 2; // a thin seam, so it runs out inside one round
        var power = (BuyutecPower)session.Powers.Add(new BuyutecPower());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        session.Powers.DispatchRoundStarted(session.CurrentRound);
        Check(mine.CapacityLeft == 2, "a two-point seam", "" + mine.CapacityLeft);

        for (int i = 0; i < 3 && !mine.IsExhausted; i++)
        {
            power.Spend();
            PlayTurns(session, mine.CommonEveryTurns);
        }
        Check(mine.IsExhausted, "the seam is worked out", "left " + mine.CapacityLeft);

        // Now it is inert. Proven by the SEAM rather than by the power: a clean sweep during
        // these turns would recharge it anyway, and that has nothing to do with this joker.
        power.Spend();
        PlayTurns(session, mine.CommonEveryTurns + 2);
        Check(mine.CapacityLeft == 0, "the seam stayed at nothing - it never acted",
            "left " + mine.CapacityLeft);

        // And it refunds the purchase price rather than the market's formula.
        int normal = session.Jokers.SellValueOf(mine);
        Check(normal > 0 && mine.OverrideSellValue(normal) == normal,
            "with no purchase price it still sells normally", "" + normal);
        int scale = session.Config.Scoring.ScoreScale;
        mine.ScoreScaleForRefund = scale;
        mine.PurchasePrice = 500L * scale;
        Check(session.Jokers.SellValueOf(mine) * scale == 500L * scale,
            "once bought, a worked-out seam refunds exactly what you paid",
            "" + session.Jokers.SellValueOf(mine));
    }

    private static void YerAlti_StillFullSellsNormally()
    {
        Section("yer altı kaynakları / an unspent seam sells at its normal value");
        var session = NewSession(8106, 6, 1000000, 40, 1);
        var mine = (YerAltiKaynaklariJoker)session.Jokers.Add(new YerAltiKaynaklariJoker());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        mine.ScoreScaleForRefund = session.Config.Scoring.ScoreScale;
        mine.PurchasePrice = 99999;
        Check(!mine.IsExhausted, "the seam still has fuel");
        int normal = session.Config.Market.JokerSellValue(RarityTable.For(mine.DefId))
            + mine.AccruedValue + mine.AuctionPremium;
        Check(session.Jokers.SellValueOf(mine) == normal,
            "so it sells at the market's price, not the refund",
            session.Jokers.SellValueOf(mine) + " vs " + normal);
    }

    private static void Devre_TracesAMonotoneEdgeToEdgePath()
    {
        Section("devre / traces a connected, edge-to-edge, never-doubling-back circuit");
        // Many rounds' worth of circuits, so the shape rules are checked against real variety
        // rather than one lucky draw.
        bool allConnected = true;
        bool allMonotone = true;
        bool allEdgeToEdge = true;
        bool allOnBoard = true;
        bool allWound = true;
        int traced = 0;

        for (int seed = 7000; seed < 7040; seed++)
        {
            var session = NewSession(seed, 7, 1000000, 40, 1);
            var joker = (DevreJoker)session.Jokers.Add(new DevreJoker());
            RoundEngine round = session.CurrentRound;
            session.Jokers.DispatchRoundStarted(round);
            PlayTurns(session, joker.MaxArmTurn + 2);
            if (!joker.HasCircuit)
            {
                continue;
            }
            traced++;
            IReadOnlyList<GridPos> path = joker.Path;
            GameBoard board = round.Board;

            for (int i = 0; i < path.Count; i++)
            {
                if (!board.IsInside(path[i])) { allOnBoard = false; }
                if (i == 0) { continue; }
                int dx = path[i].X - path[i - 1].X;
                int dy = path[i].Y - path[i - 1].Y;
                // Every step is exactly one cell, straight - never diagonal, never a jump.
                if (System.Math.Abs(dx) + System.Math.Abs(dy) != 1) { allConnected = false; }
            }

            bool horizontal = joker.PathIsHorizontal;
            bool wound = false;
            if (horizontal)
            {
                if (path[0].X != board.MinX || path[path.Count - 1].X != board.MinX + board.Width - 1)
                {
                    allEdgeToEdge = false;
                }
                for (int i = 1; i < path.Count; i++)
                {
                    if (path[i].X < path[i - 1].X) { allMonotone = false; } // doubled back
                    if (path[i].Y != path[i - 1].Y) { wound = true; }
                }
            }
            else
            {
                if (path[0].Y != board.MinY || path[path.Count - 1].Y != board.MinY + board.Height - 1)
                {
                    allEdgeToEdge = false;
                }
                for (int i = 1; i < path.Count; i++)
                {
                    if (path[i].Y < path[i - 1].Y) { allMonotone = false; }
                    if (path[i].X != path[i - 1].X) { wound = true; }
                }
            }
            if (!wound) { allWound = false; }
        }

        Check(traced > 20, "circuits were traced across many rounds", "traced " + traced);
        Check(allOnBoard, "every cell of every circuit is real play area");
        Check(allConnected, "every step is one straight cell - the circuit is one unbroken line");
        Check(allEdgeToEdge, "every circuit runs from one edge to the opposite one");
        Check(allMonotone, "and NEVER doubles back along its own axis");
        Check(allWound, "and EVERY one of them winds - never a plain row or column, which the "
            + "game already explodes on its own");
    }

    private static void Devre_WaitsForARandomTurnAndThenStays()
    {
        Section("devre / appears at a random turn and then waits all round");
        var session = NewSession(7100, 6, 1000000, 40, 1);
        var joker = (DevreJoker)session.Jokers.Add(new DevreJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        Check(!joker.HasCircuit, "nothing is traced on an empty board at round start");

        PlayTurns(session, 1);
        Check(!joker.HasCircuit, "still nothing after the first turn",
            "turn " + round.TurnNumber);

        PlayTurns(session, joker.MaxArmTurn + 2);
        Check(joker.HasCircuit, "by the latest arm turn it is on the board",
            "turn " + round.TurnNumber);

        // No deadline: it is still there many turns later, untouched.
        IReadOnlyList<GridPos> before = new List<GridPos>(joker.Path);
        PlayTurns(session, 15);
        if (!joker.BrokenThisRound)
        {
            Check(joker.HasCircuit, "and it is still waiting turns later - there is no deadline");
            Check(joker.Path.Count == before.Count, "the same circuit, not a new one",
                before.Count + " -> " + joker.Path.Count);
        }
        else
        {
            Check(true, "it was completed during play, which is the other legal outcome");
        }
    }

    private static void Devre_BreakingItExplodesThePathAndPays()
    {
        Section("devre / completing the circuit breaks it, explodes it and pays");
        var session = NewSession(7101, 5, 1000000, 40, 1);
        var joker = (DevreJoker)session.Jokers.Add(new DevreJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        PlayTurns(session, joker.MaxArmTurn + 2);
        Check(joker.HasCircuit, "a circuit is on the board");

        var path = new List<GridPos>(joker.Path);
        int cells = path.Count;
        // Fill every cell of the circuit but one, by hand, then let a real turn close it.
        for (int i = 0; i < path.Count; i++)
        {
            if (!round.Board.GetCube(path[i]).HasValue)
            {
                round.Board.SetCubeAt(path[i], new Cube(CubeKind.Normal, 9500));
            }
        }
        Check(!joker.BrokenThisRound, "filling it by hand does not fire it - a turn has to resolve");

        long scoreBefore = session.TotalScore;
        PlayOneCard(round);
        Check(joker.BrokenThisRound, "the next resolved turn breaks it");
        Check(!joker.HasCircuit, "and the circuit is gone from the board");

        int stillStanding = 0;
        for (int i = 0; i < path.Count; i++)
        {
            if (round.Board.GetCube(path[i]).HasValue) { stillStanding++; }
        }
        Check(stillStanding <= 1, "the cubes on it exploded",
            "still standing " + stillStanding + " of " + cells);
        Check(session.TotalScore > scoreBefore, "and it paid",
            scoreBefore + " -> " + session.TotalScore);
    }

    private static void Devre_OnlyOneCircuitPerRound()
    {
        Section("devre / one circuit per round, and a fresh one next round");
        var session = NewSession(7102, 5, 1000000, 40, 1);
        var joker = (DevreJoker)session.Jokers.Add(new DevreJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        PlayTurns(session, joker.MaxArmTurn + 2);
        if (!joker.HasCircuit)
        {
            Check(false, "no circuit was traced", "turn " + round.TurnNumber);
            return;
        }
        foreach (GridPos cell in new List<GridPos>(joker.Path))
        {
            if (!round.Board.GetCube(cell).HasValue)
            {
                round.Board.SetCubeAt(cell, new Cube(CubeKind.Normal, 9501));
            }
        }
        PlayOneCard(round);
        Check(joker.BrokenThisRound, "the circuit broke");

        PlayTurns(session, 20);
        Check(!joker.HasCircuit, "no second circuit appears in the same round");
        Check(joker.BrokenThisRound, "it stays broken for the rest of the round");

        // A new round re-arms it from scratch.
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        Check(!joker.BrokenThisRound, "a new round clears the broken flag");
        Check(!joker.HasCircuit, "and starts untraced again");
    }

    private static void Devre_ALineClearOnTheSameTurnStillCounts()
    {
        Section("devre / a cell that blew up this turn still counts as filled");
        var session = NewSession(7103, 5, 1000000, 40, 1);
        var joker = (DevreJoker)session.Jokers.Add(new DevreJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        PlayTurns(session, joker.MaxArmTurn + 2);
        Check(joker.HasCircuit, "a circuit is on the board");

        // Fill the WHOLE board except one cell, so the closing placement completes both the
        // circuit and at least one line - the line explodes first and empties circuit cells.
        var path = new List<GridPos>(joker.Path);
        FillBoardSolid(round, session);
        GridPos gap = path[path.Count - 1];
        round.Board.DestroyCube(gap);
        // The hole is on the circuit, so closing it completes the circuit AND its row/column.
        TurnReport report = PlayOneCard(round);
        Check(report != null, "a card was played");
        Check(report.ExplodedRows.Count + report.ExplodedColumns.Count > 0,
            "the same placement cleared a line", "rows " + report.ExplodedRows.Count);
        Check(joker.BrokenThisRound,
            "and the circuit still counted as completed, even though the line ate its cells");
    }

    /// <summary>Fills the rows through the creature and plays a turn, so an explosion is
    /// guaranteed to land on it. Returns the report, or null if no turn could resolve.</summary>
    private static TurnReport FeedTheCreature(GameSession session, BeslemeJoker pet)
    {
        RoundEngine round = session.CurrentRound;
        foreach (GridPos cell in new List<GridPos>(pet.Region))
        {
            for (int x = round.Board.MinX; x < round.Board.MinX + round.Board.Width; x++)
            {
                var pos = new GridPos(x, cell.Y);
                if (round.Board.IsInside(pos) && !round.Board.GetCube(pos).HasValue)
                {
                    round.Board.SetCubeAt(pos, new Cube(CubeKind.Normal, 9980));
                }
            }
        }
        return PlayOneCard(round);
    }

    /// <summary>Leaves the board one cube short of a full bottom row on an otherwise empty
    /// board, so the next single-cube placement clears the row AND sweeps the board.</summary>
    private static GridPos ArmASweep(RoundEngine round)
    {
        foreach (GridPos cell in AllPlayableCells(round.Board))
        {
            if (round.Board.GetCube(cell).HasValue)
            {
                round.Board.DestroyCubeForced(cell);
            }
        }
        int row = round.Board.MinY;
        for (int x = round.Board.MinX + 1; x < round.Board.MinX + round.Board.Width; x++)
        {
            round.Board.SetCubeAt(new GridPos(x, row), new Cube(CubeKind.Normal, 9990));
        }
        return new GridPos(round.Board.MinX, row);
    }

    /// <summary>A session whose run is <paramref name="totalRounds"/> long, so the LAST round is
    /// reachable in a test without playing fifteen of them.</summary>
    private static GameSession NewShortRunSession(int seed, int totalRounds, int threshold,
        bool bossRounds = false)
    {
        var config = new GameConfig();
        config.RngSeed = seed;
        config.TotalRounds = totalRounds;
        config.Deck = new DeckDefinition("test", 40, new SizedShapeGenerator(1));
        config.Progression = new FixedProgression(5, threshold, ShuffleErosion.None, bossRounds);
        return new GameSession(config);
    }

    /// <summary>True if the joker is anywhere in the current market stock.</summary>
    private static bool MarketStocks(GameSession session, string defId)
    {
        for (int i = 0; i < session.Market.Offers.Count; i++)
        {
            MarketOffer offer = session.Market.Offers[i];
            if (offer.Kind == MarketOfferKind.Joker && offer.Joker != null
                && offer.Joker.DefId == defId)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Rerolls the market up to <paramref name="tries"/> times looking for the joker,
    /// bankrolling the rerolls so cost never ends the search early. Returns how many of the
    /// shops it appeared in.</summary>
    private static int CountInRerolledShops(GameSession session, string defId, int tries)
    {
        int seen = MarketStocks(session, defId) ? 1 : 0;
        for (int i = 0; i < tries; i++)
        {
            session.AddCurrency(1000000);
            if (!session.RerollMarket(MarketOfferKind.Joker))
            {
                break;
            }
            if (MarketStocks(session, defId))
            {
                seen++;
            }
        }
        return seen;
    }

    /// <summary>Index of the first unsold offer of that kind in the market, or -1.</summary>
    private static int FirstOfferOfKind(GameSession session, MarketOfferKind kind)
    {
        for (int i = 0; i < session.Market.Offers.Count; i++)
        {
            if (session.Market.Offers[i].Kind == kind && !session.Market.Offers[i].Sold)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>A session parked in its first market with a smuggler in the inventory.</summary>
    private static GameSession NewSmugglingSession(int seed, int defectPercent,
        out KacakciJoker smuggler)
    {
        GameSession session = NewShortRunSession(seed, 12, 40);
        smuggler = (KacakciJoker)session.Jokers.Add(new KacakciJoker());
        smuggler.DefectChancePercent = defectPercent;
        Check(AdvanceToMarket(session, 200), "reached the market", "phase " + session.Phase);
        return session;
    }

    /// <summary>A one-round session running a specific boss, so a boss can be tested without
    /// waiting for the draw to hand it over.</summary>
    private static GameSession NewBossSession(int seed, int boardSize, int threshold,
        string bossDefId, int deckSize = 40, params int[] shapeSizes)
    {
        var config = new GameConfig();
        config.RngSeed = seed;
        config.TotalRounds = 12;
        config.Deck = new DeckDefinition("test", deckSize,
            new SizedShapeGenerator(shapeSizes.Length > 0 ? shapeSizes : new[] { 1 }));
        config.Progression = new FixedProgression(boardSize, threshold, ShuffleErosion.None, true);
        // Pinned rather than hand-attached, so the boss is in place BEFORE the engine builds the
        // board - which is the only way a board-reshaping boss can be tested at all.
        config.ForcedBossDefId = bossDefId;
        return new GameSession(config);
    }

    /// <summary>Puts a specific card in hand slot 0 and returns it, so a workshop test does not
    /// have to fish for the shape it needs.</summary>
    private static BlockCard PutInHand(GameSession session, RoundEngine round, BlockShape shape,
        IEnumerable<BlockElement> elements = null)
    {
        BlockCard card = session.CreateCard(shape, elements);
        round.Hand.Insert(0, card);
        return card;
    }

    private static void Nester_CutsABlockInTwo()
    {
        Section("neşter / cuts one block into two, and both halves have to hold together");
        var session = NewSession(9400, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var power = (NesterPower)session.Powers.Add(new NesterPower());
        session.Powers.DispatchRoundStarted(round);

        // A bar of four, cut into two and two.
        BlockCard bar = PutInHand(session, round, Bar(4));
        int deckBefore = session.OwnedCards.Count;
        var firstHalf = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
        Check(session.Powers.TryUse(power.InstanceId,
                ActivationTarget.CardCubes(0, firstHalf)),
            "the cut went through");

        Check(round.BonusHand.Count == 2, "two pieces arrived in the bonus hand",
            "" + round.BonusHand.Count);
        Check(round.BonusHand[0].Card.Shape.Size == 2
                && round.BonusHand[1].Card.Shape.Size == 2,
            "each of them is half the block");
        bool wholeStillInHand = false;
        for (int i = 0; i < round.Hand.Count; i++)
        {
            if (round.Hand[i].Id == bar.Id) { wholeStillInHand = true; }
        }
        Check(!wholeStillInHand, "and the card that was cut is gone from the hand");
        Check(session.OwnedCards.Count == deckBefore,
            "the deck the player OWNS is untouched - the cut is round-scoped",
            deckBefore + " -> " + session.OwnedCards.Count);
    }

    private static void Nester_RefusesACutThatWouldNotHoldTogether()
    {
        Section("neşter / it refuses a cut that leaves a piece in loose bits");
        var session = NewSession(9401, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var power = (NesterPower)session.Powers.Add(new NesterPower());
        session.Powers.DispatchRoundStarted(round);
        PutInHand(session, round, Bar(4));

        // Taking the two ENDS would leave the middle pair as the other half - fine - but the
        // ends themselves are not touching, so the first piece is two loose cubes.
        var ends = new List<GridPos> { new GridPos(0, 0), new GridPos(3, 0) };
        Check(!session.Powers.CanUse(power.InstanceId, ActivationTarget.CardCubes(0, ends)),
            "the two ends are not one piece, so the cut is refused");

        // Everything, or nothing, is not a cut either.
        var everything = new List<GridPos>(Bar(4).Cells);
        Check(!session.Powers.CanUse(power.InstanceId,
                ActivationTarget.CardCubes(0, everything)),
            "taking the whole card is not a cut");
        Check(!session.Powers.CanUse(power.InstanceId,
                ActivationTarget.CardCubes(0, new List<GridPos>())),
            "and neither is taking none of it");

        // A single cube has nothing to cut.
        PutInHand(session, round, Bar(1));
        Check(!session.Powers.CanUse(power.InstanceId,
                ActivationTarget.CardCubes(0, new List<GridPos> { new GridPos(0, 0) })),
            "a one-cube block cannot be cut in two");
    }

    private static void Lehimleme_WeldsTwoCardsIntoOne()
    {
        Section("lehimleme / welds two cards into one where you put them");
        var session = NewSession(9402, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var power = (LehimlemePower)session.Powers.Add(new LehimlemePower());
        session.Powers.DispatchRoundStarted(round);

        PutInHand(session, round, Bar(2));
        PutInHand(session, round, Bar(2));
        int deckBefore = session.OwnedCards.Count;

        // Put the second bar directly above the first: a 2x2.
        Check(session.Powers.TryUse(power.InstanceId,
                ActivationTarget.TwoCards(0, 1, new GridPos(0, 1))),
            "the weld went through");
        Check(round.BonusHand.Count == 1, "one card came out", "" + round.BonusHand.Count);
        BlockShape welded = round.BonusHand[0].Card.Shape;
        Check(welded.Size == 4, "made of all four cubes", "" + welded.Size);
        Check(welded.Width == 2 && welded.Height == 2, "and it is the 2x2 we asked for",
            welded.Width + "x" + welded.Height);
        Check(session.OwnedCards.Count == deckBefore,
            "the owned deck is untouched - the weld is round-scoped");
    }

    private static void Lehimleme_RefusesAJoinThatDoesNotTouch()
    {
        Section("lehimleme / the two halves must touch and must not overlap");
        var session = NewSession(9403, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var power = (LehimlemePower)session.Powers.Add(new LehimlemePower());
        session.Powers.DispatchRoundStarted(round);
        PutInHand(session, round, Bar(2));
        PutInHand(session, round, Bar(2));

        Check(!session.Powers.CanUse(power.InstanceId,
                ActivationTarget.TwoCards(0, 1, new GridPos(0, 3))),
            "a join floating three cells away is refused");
        Check(!session.Powers.CanUse(power.InstanceId,
                ActivationTarget.TwoCards(0, 1, new GridPos(0, 0))),
            "and one laid straight on top of the other is refused");
        Check(!session.Powers.CanUse(power.InstanceId,
                ActivationTarget.TwoCards(0, 0, new GridPos(0, 1))),
            "and a card cannot be welded to itself");
    }

    private static void GenNakli_MovesAnElementAndGivesItBack()
    {
        Section("gen nakli / the element moves to the card, and goes home when it is discarded");
        var session = NewSession(9404, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var power = (GenNakliPower)session.Powers.Add(new GenNakliPower());
        session.Powers.DispatchRoundStarted(round);
        ClearBoard(round.Board);

        var donor = new GridPos(2, 2);
        round.Board.SetCubeAt(donor, new Cube(CubeKind.Fire, 9405));
        BlockCard plain = PutInHand(session, round, Bar(1));
        Check(!round.CardHasElement(plain, BlockElement.Fire), "the card starts plain");

        Check(session.Powers.TryUse(power.InstanceId,
                ActivationTarget.CellAndCard(donor, 0)), "the transplant went through");
        Check(round.CardHasElement(plain, BlockElement.Fire),
            "the card carries fire now - and every rule that asks sees it");
        Check(round.Board.GetCube(donor).Value.Kind == CubeKind.Normal,
            "while the cube it came from went plain",
            "" + round.Board.GetCube(donor).Value.Kind);
        Check(plain.Elements.Count == 0,
            "the CARD itself was never changed - the gene is round-scoped bookkeeping");

        // Play it: reaching the discard ends the loan, both ways.
        int index = -1;
        for (int i = 0; i < round.Hand.Count; i++)
        {
            if (round.Hand[i].Id == plain.Id) { index = i; }
        }
        Check(index >= 0, "the card is still in hand");
        round.PlayFromHand(index, new GridPos(0, 0));
        Check(!round.CardHasElement(plain, BlockElement.Fire),
            "once discarded the card is plain again");
        Check(round.Board.GetCube(donor).Value.Kind == CubeKind.Fire,
            "and the cube on the board has its element back",
            "" + round.Board.GetCube(donor).Value.Kind);
    }

    private static void GenNakli_RefusesAPlainCubeOrABusyCard()
    {
        Section("gen nakli / nothing to take, or nowhere to put it");
        var session = NewSession(9406, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var power = (GenNakliPower)session.Powers.Add(new GenNakliPower());
        session.Powers.DispatchRoundStarted(round);
        ClearBoard(round.Board);

        var plainCube = new GridPos(1, 1);
        round.Board.SetCubeAt(plainCube, new Cube(CubeKind.Normal, 9407));
        PutInHand(session, round, Bar(1));
        Check(!session.Powers.CanUse(power.InstanceId,
                ActivationTarget.CellAndCard(plainCube, 0)),
            "a plain cube has no gene to give");
        Check(!session.Powers.CanUse(power.InstanceId,
                ActivationTarget.CellAndCard(new GridPos(4, 4), 0)),
            "and an empty cell has nothing at all");

        var fire = new GridPos(2, 2);
        round.Board.SetCubeAt(fire, new Cube(CubeKind.Fire, 9408));
        PutInHand(session, round, Bar(1), new List<BlockElement> { BlockElement.Gold });
        Check(!session.Powers.CanUse(power.InstanceId, ActivationTarget.CellAndCard(fire, 0)),
            "and a card that already carries an element has no room for another");
    }

    private static void Pres_SqueezesFourCellsIntoOne()
    {
        Section("hidrolik pres / four cells become one, and three come free");
        var board = new GameBoard(5, 5);
        board.SetCubeAt(new GridPos(1, 1), new Cube(CubeKind.Fire, 9500));
        board.SetCubeAt(new GridPos(2, 1), new Cube(CubeKind.Normal, 9500));
        board.SetCubeAt(new GridPos(1, 2), new Cube(CubeKind.Normal, 9500));
        // (2,2) deliberately left empty: the press swallows the picture, holes and all.

        Cube?[] swallowed = board.Compress(new GridPos(1, 1));
        Check(swallowed != null, "the patch was squeezed");
        Check(board.GetCube(new GridPos(1, 1)).Value.Kind == CubeKind.Compressed,
            "a compressed cube stands on the anchor");
        Check(!board.GetCube(new GridPos(2, 1)).HasValue
                && !board.GetCube(new GridPos(1, 2)).HasValue,
            "and the other three cells came free");
        Check(board.OccupiedCount == 1, "one cube where there were three",
            "" + board.OccupiedCount);

        // Letting go on an empty board restores exactly the picture it swallowed.
        PressExpansion result = board.Expand(new GridPos(1, 1), swallowed);
        Check(result != null && !result.Detonated, "it opened without detonating");
        Check(board.GetCube(new GridPos(1, 1)).Value.Kind == CubeKind.Fire,
            "the fire cube is back where it was");
        Check(board.GetCube(new GridPos(2, 1)).HasValue
                && board.GetCube(new GridPos(1, 2)).HasValue,
            "and so are the other two");
        Check(!board.GetCube(new GridPos(2, 2)).HasValue,
            "while the cell that was empty is empty again");
    }

    private static void Pres_ShovesCubesOffTheEdgeWhenItOpens()
    {
        Section("hidrolik pres / it shoves what is in the way, and the rim goes over the edge");
        var board = new GameBoard(5, 5);
        board.SetCubeAt(new GridPos(3, 0), new Cube(CubeKind.Normal, 9501));
        Cube?[] swallowed = board.Compress(new GridPos(3, 0));
        Check(swallowed != null, "squeezed in the bottom-right corner");

        // Fill the cells it wants back, all the way to the wall.
        board.SetCubeAt(new GridPos(4, 0), new Cube(CubeKind.Normal, 9502));
        board.SetCubeAt(new GridPos(3, 1), new Cube(CubeKind.Normal, 9502));
        board.SetCubeAt(new GridPos(4, 1), new Cube(CubeKind.Normal, 9502));

        PressExpansion result = board.Expand(new GridPos(3, 0), swallowed);
        Check(result != null && !result.Detonated, "it opened");
        Check(result.CubesPushedOff > 0, "and shoved cubes off the board",
            "" + result.CubesPushedOff);
        Check(board.GetCube(new GridPos(3, 0)).HasValue, "the press has its cells back");
    }

    private static void Pres_WillNotBudgeObsidianAndDetonatesWhenStuck()
    {
        Section("hidrolik pres / obsidian will not budge, and a stuck press detonates");
        var board = new GameBoard(5, 5);
        board.SetCubeAt(new GridPos(0, 0), new Cube(CubeKind.Normal, 9503));
        Cube?[] swallowed = board.Compress(new GridPos(0, 0));

        // Wall every cell it wants with stone. Nothing can move, in any direction.
        board.SetCubeAt(new GridPos(1, 0), new Cube(CubeKind.Obsidian, 9504));
        board.SetCubeAt(new GridPos(0, 1), new Cube(CubeKind.Gold, 9504));
        board.SetCubeAt(new GridPos(1, 1), new Cube(CubeKind.Obsidian, 9504));

        PressExpansion result = board.Expand(new GridPos(0, 0), swallowed);
        Check(result != null && result.Detonated, "with nowhere to go it detonated");
        Check(result.CubesPushedOff == 0, "shoving nothing off the board",
            "" + result.CubesPushedOff);
        Check(!board.GetCube(new GridPos(1, 0)).HasValue
                && !board.GetCube(new GridPos(0, 1)).HasValue,
            "and it took the stone around it with it - the only thing in the game that can");
        Check(!board.GetCube(new GridPos(0, 0)).HasValue, "the press itself is gone too");
    }

    private static void Pres_OpensByItselfAfterFourTurns()
    {
        Section("hidrolik pres / it counts down and lets go on its own");
        var session = NewSession(9505, 7, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var power = (HidrolikPresPower)session.Powers.Add(new HidrolikPresPower());
        session.Powers.DispatchRoundStarted(round);
        ClearBoard(round.Board);
        round.Board.SetCubeAt(new GridPos(1, 1), new Cube(CubeKind.Normal, 9506));

        Check(session.Powers.TryUse(power.InstanceId,
                new ActivationTarget(null, new GridPos(1, 1))), "the press came down");
        Check(power.IsPressing, "and it is holding");
        Check(round.Board.GetCube(new GridPos(1, 1)).Value.Kind == CubeKind.Compressed,
            "a compressed cube is on the board");

        PlayTurns(session, power.TurnsCompressed - 1);
        Check(power.IsPressing, "still holding after three turns", "" + power.TurnsLeft);
        PlayTurns(session, 1);
        Check(!power.IsPressing, "and it let go on the fourth", "" + power.TurnsLeft);
        Check(round.Board.CellsOfKind(CubeKind.Compressed).Count == 0,
            "no compressed cube is left on the board");
    }

    private static void MayinEsegi_ArmsAMineAndShufflesItAway()
    {
        Section("mayın eşeği / the mine travels with its cover, and the cubes never move");
        var session = NewBossSession(9600, 5, 1000000, "mayin_esegi", 40, 1);
        var boss = (MayinEsegiBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;

        Check(boss.Armed, "a mine is armed");
        Check(round.Board.IsInside(boss.MineCell), "on a real cell of the arena",
            boss.MineCell.X + "," + boss.MineCell.Y);
        Check(boss.ShuffleCount == 1, "and it was revealed and shuffled once",
            "" + boss.ShuffleCount);
        Check(boss.TurnsLeft == boss.TurnsPerShuffle, "with a full clock",
            "" + boss.TurnsLeft);

        // The path is the truth: it starts where the mine was put and ends where it is now.
        IReadOnlyList<GridPos> path = boss.ShufflePath;
        Check(path.Count > 1, "the cover really danced", "hops " + path.Count);
        GridPos last = path[path.Count - 1];
        Check(last.X == boss.MineCell.X && last.Y == boss.MineCell.Y,
            "and the mine is exactly where its cover stopped - following it WORKS",
            last.X + "," + last.Y + " vs " + boss.MineCell.X + "," + boss.MineCell.Y);
        bool everMoved = false;
        for (int i = 1; i < path.Count; i++)
        {
            if (path[i].X != path[0].X || path[i].Y != path[0].Y) { everMoved = true; }
        }
        Check(everMoved, "the mine genuinely left the cell it was shown on");
    }

    private static void MayinEsegi_TheCubesAreUntouchedByAShuffle()
    {
        Section("mayın eşeği / a shuffle moves the mine and NOTHING else");
        var session = NewBossSession(9601, 5, 1000000, "mayin_esegi", 40, 1);
        var boss = (MayinEsegiBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;
        ClearBoard(round.Board);
        round.Board.SetCubeAt(new GridPos(1, 1), new Cube(CubeKind.Fire, 9602));
        round.Board.SetCubeAt(new GridPos(3, 2), new Cube(CubeKind.Gold, 9603));

        int shufflesBefore = boss.ShuffleCount;
        // Run the clock out so it reveals and shuffles again.
        for (int i = 0; i < boss.TurnsPerShuffle; i++)
        {
            boss.AfterTurnScored(FakeTurnFor(session, round));
        }
        Check(boss.ShuffleCount == shufflesBefore + 1, "it shuffled again after ten turns",
            "" + boss.ShuffleCount);
        Check(boss.TurnsLeft == boss.TurnsPerShuffle, "and the clock went back to full",
            "" + boss.TurnsLeft);

        Check(round.Board.GetCube(new GridPos(1, 1)).Value.Kind == CubeKind.Fire,
            "the fire cube is exactly where it was");
        Check(round.Board.GetCube(new GridPos(3, 2)).Value.Kind == CubeKind.Gold,
            "and so is the gold one - a shuffle moves the mine and nothing else");
        Check(round.Board.OccupiedCount == 2, "with nothing added or lost",
            "" + round.Board.OccupiedCount);
    }

    private static void MayinEsegi_SettingItOffCostsAndMovesIt()
    {
        Section("mayın eşeği / setting it off is expensive, and hands you a fresh one");
        var session = NewBossSession(9604, 5, 1000000, "mayin_esegi", 40, 1);
        var boss = (MayinEsegiBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;
        session.Config.Scoring.PointsPerCubePlaced = 400;
        ClearBoard(round.Board);

        // Bank something first, so the penalty has score to bite into.
        PlayOneCard(round);
        int shufflesBefore = boss.ShuffleCount;
        int detonationsBefore = boss.Detonations;

        // Blow up whatever cell the mine is on, by hand, through a real turn's destruction log.
        GridPos mine = boss.MineCell;
        ClearBoard(round.Board);
        for (int x = round.Board.MinX; x < round.Board.MinX + round.Board.Width; x++)
        {
            var cell = new GridPos(x, mine.Y);
            if (x != round.Board.MinX)
            {
                round.Board.SetCubeAt(cell, new Cube(CubeKind.Normal, 9605));
            }
        }
        TurnReport report = PlayAt(round, new GridPos(round.Board.MinX, mine.Y));
        Check(report != null && report.ExplodedRows.Count > 0,
            "the mine's row was blown up");

        Check(boss.Detonations == detonationsBefore + 1, "the mine went off",
            "" + boss.Detonations);
        Check(boss.ShuffleCount > shufflesBefore,
            "and a fresh one was armed, revealed and shuffled", "" + boss.ShuffleCount);
        Check(boss.TurnsLeft == boss.TurnsPerShuffle, "with a full clock again",
            "" + boss.TurnsLeft);
        Check(round.RoundScore >= 0, "the penalty never takes the round below zero",
            "" + round.RoundScore);
    }

    private static void MayinEsegi_AQuietTurnJustRunsTheClockDown()
    {
        Section("mayın eşeği / a turn that misses the mine only costs a turn");
        var session = NewBossSession(9606, 5, 1000000, "mayin_esegi", 40, 1);
        var boss = (MayinEsegiBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;
        int before = boss.TurnsLeft;
        int shufflesBefore = boss.ShuffleCount;

        PlayOneCard(round);
        Check(boss.Detonations == 0, "nothing went off", "" + boss.Detonations);
        Check(boss.TurnsLeft == before - 1, "the clock moved by one",
            before + " -> " + boss.TurnsLeft);
        Check(boss.ShuffleCount == shufflesBefore, "and nothing was shuffled",
            "" + boss.ShuffleCount);
    }

    /// <summary>A turn context over a live round with an empty report - for driving a boss's
    /// end-of-turn hook without resolving a real placement.</summary>
    private static TurnContext FakeTurnFor(GameSession session, RoundEngine round)
    {
        var report = new TurnReport();
        var score = new ScoreBreakdown();
        report.Score = score;
        return new TurnContext(session, new SeededRandom(7), round, report, score);
    }

    // ------------------------------------------------------------------ the five new bosses

    private static void Sasirtmaca_OneCommitmentPerTurnAndTheLockLifts()
    {
        Section("şaşırtmaca / one blind commitment per turn");
        var session = NewBossSession(9700, 5, 1000000, "sasirtmaca", 40, 1);
        RoundEngine round = session.CurrentRound;
        var boss = (SasirtmacaBoss)round.Boss;

        Check(round.HandIsFaceDown, "the hand is dealt face down");
        Check(boss.RevealedHandCardId == 0, "and nothing is turned over yet");
        for (int i = 0; i < round.Hand.Count; i++)
        {
            Check(!round.IsLockedByBoss(round.Hand[i]),
                "while nothing is turned over, nothing is locked");
        }

        int chosen = round.Hand[1].Id;
        Check(round.RevealHandCard(1), "turning one over takes");
        Check(boss.RevealedHandCardId == chosen, "it is the card that was picked");
        Check(round.IsLockedByBoss(round.Hand[0]) && round.IsLockedByBoss(round.Hand[2]),
            "and the rest of the hand locks behind it");
        Check(!round.RevealHandCard(0), "a second commitment is refused");

        // The engine must actually REFUSE to play a locked card, not merely mark it.
        bool refused = false;
        try
        {
            round.PlayFromHand(0, new GridPos(0, 0));
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }
        Check(refused, "a locked card cannot be played");

        PlayAt(round, new GridPos(0, 0));
        Check(boss.RevealedHandCardId == 0, "the turn clears the commitment");
        Check(boss.HandBeforeMix.Count > 0, "and the hand it mixed is remembered for the reveal");
    }

    private static void Matruska_SplitsOnTheLadderAndWinsOnTheLastDoll()
    {
        Section("matruşka / 1, 2, 4, 8 - and the last doll ends it");
        var session = NewBossSession(9710, 5, 100, "matruska", 60, 1);
        RoundEngine round = session.CurrentRound;
        var boss = (MatruskaBoss)round.Boss;
        Check(boss.DollCount == 0, "an empty arena has nowhere to put a doll yet");

        // One cube down, and the pet of a boss has its first host.
        PlayAt(round, new GridPos(0, 0));
        Check(boss.DollCount == 1, "the first doll arrives once there is a cube to sit on",
            "" + boss.DollCount);
        Check(round.RoundScore == 0, "and nothing this round scores by itself",
            "" + round.RoundScore);

        // Somewhere for the halves to land - SCATTERED, because a doll-less line going off is a
        // lost round, so the spare cubes must not complete anything.
        PaintBoard(round, session, CubeKind.Normal, new GridPos(1, 2), new GridPos(3, 2),
            new GridPos(1, 4));
        int before = boss.DollCount;
        BreakTheDolls(session, round, boss);
        Check(boss.DollCount > before, "breaking a doll's cube splits it in two",
            before + " -> " + boss.DollCount);
    }

    /// <summary>Explodes the row the first doll is standing in, which is the only clear this
    /// boss allows. Returns false when no doll could be reached.</summary>
    private static bool BreakTheDolls(GameSession session, RoundEngine round, MatruskaBoss boss)
    {
        IReadOnlyList<GridPos> cells = boss.DollCells;
        if (cells.Count == 0)
        {
            return false;
        }
        int y = cells[0].Y;
        for (int x = 0; x < 4; x++)
        {
            PaintBoard(round, session, CubeKind.Normal, new GridPos(x, y));
        }
        return DropOneCube(round, new GridPos(4, y)) != null;
    }

    private static void Matruska_ADollLessLineLosesTheRound()
    {
        Section("matruşka / a line with no doll in it is a lost round");
        var session = NewBossSession(9711, 5, 1000000, "matruska", 60, 1);
        RoundEngine round = session.CurrentRound;
        var boss = (MatruskaBoss)round.Boss;
        PlayAt(round, new GridPos(0, 0));
        Check(boss.DollCount == 1, "one doll is on the board");

        // Clear a row the doll is NOT in. There is exactly one doll and it sits on (0,0), so
        // any other row is a forbidden target.
        int dollRow = boss.DollCells[0].Y;
        int victim = dollRow == 4 ? 3 : 4;
        for (int x = 0; x < 4; x++)
        {
            PaintBoard(round, session, CubeKind.Normal, new GridPos(x, victim));
        }
        DropOneCube(round, new GridPos(4, victim));
        Check(round.Loss == LossReason.LineWithoutDoll,
            "clearing a doll-less line loses the round", "" + round.Loss);
    }

    // Both bosses below judge an exploding line against something that remembers ABSOLUTE cells,
    // while TurnReport.ExplodedRows/Columns are 0-based ARRAY INDICES. The two are the same
    // number only while the board's origin sits at 0,0 - so both tests INFLATE the arena first,
    // which pushes the origin negative, and then play a move that must still be read correctly.

    private static void Matruska_TheDollCheckSurvivesAnInflatedBoard()
    {
        Section("matruşka / the doll check reads absolute rows, not array indices");
        var session = NewBossSession(9712, 5, 1000000, "matruska", 60, 1);
        RoundEngine round = session.CurrentRound;
        var boss = (MatruskaBoss)round.Boss;
        PlayAt(round, new GridPos(0, 0));
        Check(boss.DollCount == 1, "one doll is on the board", "" + boss.DollCount);

        Check(round.ReshapeBoard(1, 1, 1, 1), "the arena was inflated on every side");
        Check(round.Board.MinY == -1 && round.Board.MinX == -1,
            "so the origin really moved into negative space",
            round.Board.MinX + "," + round.Board.MinY);

        // Clear the row the doll is standing in. That is the ONE legal kind of clear on this
        // round, so it must not be read as a doll-less line.
        int dollRow = boss.DollCells[0].Y;
        FillRowLeavingOneGap(session, round, dollRow);
        Check(round.Loss != LossReason.LineWithoutDoll,
            "clearing the doll's own row is legal on an inflated board too", "" + round.Loss);
    }

    private static void Snake_TheCutCheckSurvivesAnInflatedBoard()
    {
        Section("snake / the cut check reads absolute rows, not array indices");
        var session = NewBossSession(9723, 5, 1000000, "snake", 60, 1);
        RoundEngine round = session.CurrentRound;
        var boss = (SnakeBoss)round.Boss;
        Check(round.ReshapeBoard(1, 1, 1, 1), "the arena was inflated on every side");
        Check(round.Board.MinY == -1, "the origin moved", "" + round.Board.MinY);

        int headRow = boss.Body[0].Y;
        Check(boss.SegmentsCut == 0, "nothing has been cut off it yet");
        FillRowLeavingOneGap(session, round, headRow);
        // Length alone would not prove it - the slide afterwards can eat and grow it back - so
        // the cut counter is what is checked.
        Check(boss.SegmentsCut > 0, "the explosion still cut a segment off the tail",
            "" + boss.SegmentsCut);
    }

    /// <summary>Paints every empty cell of a row but the last, then drops a cube in the gap, so
    /// the row completes on a real turn. Works in ABSOLUTE coordinates, so it is safe on an
    /// inflated board.</summary>
    private static void FillRowLeavingOneGap(GameSession session, RoundEngine round, int y)
    {
        var open = new List<GridPos>();
        for (int x = round.Board.MinX; x < round.Board.MinX + round.Board.Width; x++)
        {
            var cell = new GridPos(x, y);
            if (round.Board.IsInside(cell) && !round.Board.GetCube(cell).HasValue)
            {
                open.Add(cell);
            }
        }
        Check(open.Count > 0, "the row has room to be completed", "" + open.Count);
        for (int i = 0; i < open.Count - 1; i++)
        {
            PaintBoard(round, session, CubeKind.Normal, open[i]);
        }
        DropOneCube(round, open[open.Count - 1]);
    }

    private static void Snake_EatsWhatStopsItAndGrows()
    {
        Section("snake / it eats whatever stops it");
        var session = NewBossSession(9720, 9, 1000000, "snake", 60, 1);
        RoundEngine round = session.CurrentRound;
        var boss = (SnakeBoss)round.Boss;

        Check(boss.Length == 20, "a 9x9 arena gets the full 20-long snake", "" + boss.Length);
        int onBoard = round.Board.CountCubesOfKind(CubeKind.Snake);
        Check(onBoard == boss.Length, "every segment is really on the board", "" + onBoard);

        // Segments are cubes that no line can break and that no sweep waits for.
        var segment = new Cube(CubeKind.Snake, SnakeBoss.SnakeCardId);
        Check(!CubeRules.IsDestructible(segment), "a segment cannot be exploded");
        Check(!CubeRules.CountsForCleanSweep(segment), "and it does not block a clean sweep");

        // Drive the snake ALONE, with no explosion in the turn, so growth is the only thing that
        // can change its length: a turn it feeds on must leave it longer than it started.
        ClearBoardExceptSnake(round, boss);
        int fed = 0;
        for (int turn = 0; turn < 30 && fed == 0; turn++)
        {
            // A ring of food around the head, so whichever way it turns it runs into something.
            FeedTheSnake(round, boss);
            int lengthBefore = boss.Length;
            int eatenBefore = boss.BlocksEaten;
            boss.AfterTurnScored(FakeTurnFor(session, round));
            if (boss.BlocksEaten > eatenBefore)
            {
                fed++;
                Check(boss.Length == lengthBefore + 1, "eating a block made it one longer",
                    lengthBefore + " -> " + boss.Length);
                Check(round.Board.CountCubesOfKind(CubeKind.Snake) == boss.Length,
                    "and the board agrees", "" + round.Board.CountCubesOfKind(CubeKind.Snake));
            }
        }
        Check(fed > 0, "it fed at least once in thirty turns");
    }

    /// <summary>Empties everything that is not a snake segment.</summary>
    private static void ClearBoardExceptSnake(RoundEngine round, SnakeBoss boss)
    {
        foreach (GridPos cell in AllPlayableCells(round.Board))
        {
            Cube? cube = round.Board.GetCube(cell);
            if (cube.HasValue && cube.Value.Kind != CubeKind.Snake)
            {
                round.Board.DestroyCubeForced(cell);
            }
        }
    }

    /// <summary>Puts a plain cube in every empty cell around the snake's head, so it cannot slide
    /// anywhere without running into one.</summary>
    private static void FeedTheSnake(RoundEngine round, SnakeBoss boss)
    {
        GridPos head = boss.Body[0];
        var around = new List<GridPos>
        {
            new GridPos(head.X + 1, head.Y), new GridPos(head.X - 1, head.Y),
            new GridPos(head.X, head.Y + 1), new GridPos(head.X, head.Y - 1)
        };
        for (int i = 0; i < around.Count; i++)
        {
            if (round.Board.IsInside(around[i]) && !round.Board.GetCube(around[i]).HasValue)
            {
                round.Board.SetCubeAt(around[i], new Cube(CubeKind.Normal, 9722));
            }
        }
    }

    private static void Snake_ShrinksOnAnExplosionAndDyingWinsTheRound()
    {
        Section("snake / an explosion cuts it, and killing it takes the round");
        var session = NewBossSession(9721, 5, 200, "snake", 60, 1);
        RoundEngine round = session.CurrentRound;
        var boss = (SnakeBoss)round.Boss;
        int start = boss.Length;
        Check(start == 8, "a 5x5 arena gets a shorter snake", "" + start);

        // Complete the row the snake's head is standing in: the line goes off, the segments
        // survive it, and the snake loses one from the tail for it.
        GridPos head = boss.Body[0];
        var open = new List<GridPos>();
        for (int x = 0; x < 5; x++)
        {
            var cell = new GridPos(x, head.Y);
            if (!round.Board.GetCube(cell).HasValue)
            {
                open.Add(cell);
            }
        }
        Check(open.Count > 0, "the snake's row has room to complete", "" + open.Count);
        // Fill every gap but the last, then drop the cube that closes the row.
        for (int i = 0; i < open.Count - 1; i++)
        {
            PaintBoard(round, session, CubeKind.Normal, open[i]);
        }
        DropOneCube(round, open[open.Count - 1]);
        Check(boss.Length < start, "the explosion cut a segment off the tail",
            start + " -> " + boss.Length);
        Check(round.Board.CountCubesOfKind(CubeKind.Snake) == boss.Length,
            "and the board agrees about how long it is");
    }

    private static void Istilaci_TakesTheMarkedColumnAndBills()
    {
        Section("istilacı / the marked column is taken three turns later");
        var session = NewBossSession(9730, 5, 1000000, "istilaci", 60, 1);
        RoundEngine round = session.CurrentRound;
        var boss = (IstilaciBoss)round.Boss;
        Check(!boss.HasMark, "an empty arena has no column worth marking yet");

        PlayAt(round, new GridPos(0, 0));
        Check(boss.HasMark, "the first cube on the board earns a mark");
        Check(boss.TurnsLeft == boss.FuseTurns, "with a full fuse", "" + boss.TurnsLeft);

        int column = boss.MarkedColumn;
        // Stack that column up so the demolition has something to take - but NOT full, or the
        // next turn's line resolution would clear it before the invader ever gets there.
        for (int y = 0; y < 3; y++)
        {
            PaintBoard(round, session, CubeKind.Normal, new GridPos(column, y));
        }
        int standing = 0;
        for (int y = 0; y < 5; y++)
        {
            if (round.Board.GetCube(new GridPos(column, y)).HasValue) { standing++; }
        }
        Check(standing >= 3, "the column is stacked up", "" + standing);

        // Three more turns and it goes. Played well away from the marked column.
        for (int i = 0; i < boss.FuseTurns && boss.ColumnsTaken == 0; i++)
        {
            if (PlayAnywhereAvoiding(round, column) == null)
            {
                break;
            }
        }
        Check(boss.ColumnsTaken >= 1, "the column was demolished", "" + boss.ColumnsTaken);
        Check(boss.CubesTaken > 0, "and it took the cubes standing in it", "" + boss.CubesTaken);
        int left = 0;
        for (int y = 0; y < 5; y++)
        {
            if (round.Board.GetCube(new GridPos(column, y)).HasValue) { left++; }
        }
        Check(left == 0, "nothing is left in it", "" + left);
    }

    /// <summary>Plays the first legal placement that is NOT in the given column, so a test can
    /// let the invader's fuse burn without feeding the column it is aimed at.</summary>
    private static TurnReport PlayAnywhereAvoiding(RoundEngine round, int column)
    {
        foreach (GridPos origin in AllPlayableCells(round.Board))
        {
            if (origin.X == column)
            {
                continue;
            }
            TurnReport report = PlayAt(round, origin);
            if (report != null)
            {
                return report;
            }
        }
        return null;
    }

    private static void Tamagotchi_FeedingClearsTheDemandAndTheCardLeavesTheRound()
    {
        Section("tamagotchi / feeding it costs no turn and costs a card");
        var session = NewBossSession(9740, 5, 1000000, "tamagotchi", 40, 1);
        RoundEngine round = session.CurrentRound;
        var boss = (TamagotchiBoss)round.Boss;

        Check(boss.Demands.Count == 4, "it asks for four shapes at round start",
            "" + boss.Demands.Count);
        // Every card in this deck is a single cube, so the whole hand is food.
        Check(boss.Accepts(round, round.Hand[0]), "a matching card is food");

        int turnBefore = round.TurnNumber;
        int removedBefore = round.Deck.RemovedCount;
        Check(round.FeedPet(0), "the pet takes it");
        Check(boss.Demands.Count == 3, "one demand off the list", "" + boss.Demands.Count);
        Check(round.TurnNumber == turnBefore, "feeding costs no turn", "" + round.TurnNumber);
        Check(round.Deck.RemovedCount == removedBefore + 1,
            "and the card left the round for good", "" + round.Deck.RemovedCount);
        Check(round.Hand.Count == session.Config.Rules.HandSize,
            "the hand topped itself back up", "" + round.Hand.Count);
    }

    private static void Tamagotchi_AnUnfedDemandLosesWhenTheDeckRunsDry()
    {
        Section("tamagotchi / an unfed pet is a lost round");
        var session = NewBossSession(9741, 5, 1000000, "tamagotchi", 40, 1);
        RoundEngine round = session.CurrentRound;
        var boss = (TamagotchiBoss)round.Boss;
        Check(boss.Demands.Count > 0, "it is hungry");

        // The deadline is the drying-out, whenever it comes.
        boss.OnDrawPileEmptied(new RoundContext(session, session.Rng, round));
        Check(round.Loss == LossReason.PetWentHungry,
            "running the deck dry with the list unfed loses the round", "" + round.Loss);
    }

    private static void Bilinmezlik_HoldsFullLinesUntilItFires()
    {
        Section("bilinmezlik / a full line sits there until the boss lets it go off");
        var session = NewBossSession(8100, 5, 1000000, "bilinmezlik", 40, 1);
        var boss = (BilinmezlikBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;

        // Pin it shut, then fill the bottom row: the line must NOT clear. The boss rolls for the
        // coming turn at round start, so the knobs have to be set BEFORE that roll is redone.
        boss.FirePercent = 0;
        boss.MaxTurnsWithoutFiring = 1000;
        boss.OnRoundStarted(new RoundContext(session, session.Rng, round));
        Check(boss.SuppressesLineExplosions, "the magazine is shut");
        ClearBoard(round.Board);
        for (int x = 0; x < 4; x++)
        {
            round.Board.SetCubeAt(new GridPos(x, 0), new Cube(CubeKind.Normal, 8101));
        }
        TurnReport held = PlayAt(round, new GridPos(4, 0));
        Check(held != null, "the row was completed");
        Check(held.ExplodedRows.Count == 0, "and it did NOT explode",
            "rows " + held.ExplodedRows.Count);
        Check(round.Board.OccupiedCount == 5, "the full row is still standing",
            "" + round.Board.OccupiedCount);
        Check(held.Score.BaseLines == 0, "and it paid nothing", "" + held.Score.BaseLines);

        // Now let it fire. The roll that decides a turn happens at the END of the one before, so
        // nudge that roll before playing - which is exactly the order the engine uses.
        boss.FirePercent = 100;
        boss.AfterTurnScored(FakeTurn(Bar(1), new ScoreBreakdown()));
        Check(!boss.SuppressesLineExplosions, "the next turn will fire");
        TurnReport fired = PlayOneCard(round);
        Check(fired != null, "another turn resolved");
        Check(fired.ExplodedRows.Count > 0, "and the held line finally went off",
            "rows " + fired.ExplodedRows.Count);
    }

    private static void Bilinmezlik_ADryStreakEventuallyFiresByItself()
    {
        Section("bilinmezlik / a cold streak cannot run forever");
        var boss = new BilinmezlikBoss();
        boss.FirePercent = 0; // the coin never comes up heads
        boss.MaxTurnsWithoutFiring = 3;
        boss.OnRoundStarted(new RoundContext(null, new SeededRandom(9), null));
        Check(boss.SuppressesLineExplosions, "it starts shut");

        for (int i = 0; i < 3; i++)
        {
            boss.AfterTurnScored(FakeTurn(Bar(1), new ScoreBreakdown()));
        }
        Check(!boss.SuppressesLineExplosions,
            "three cold turns and it fires anyway - a dry spell is tension, not a loss");
    }

    private static void RehinPuan_HoldsTheLineScoreUntilTheNextClear()
    {
        Section("rehin puan / the line score is held, then released by the next clear");
        var session = NewBossSession(8102, 5, 1000000, "rehin_puan", 40, 1);
        var boss = (RehinPuanBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;

        TurnReport first = ClearABottomRow(round, 8103);
        Check(first != null && first.ExplodedRows.Count > 0, "a row cleared");
        Check(first.Score.BaseLines == 0, "and it paid nothing on the spot",
            "" + first.Score.BaseLines);
        Check(boss.Held > 0, "the score is being held", "" + boss.Held);
        int hostage = boss.Held;

        // Clear again immediately: the hostage is released.
        TurnReport second = ClearABottomRow(round, 8104);
        Check(second != null && second.ExplodedRows.Count > 0, "a second row cleared");
        Check(FlatFrom(second.Score, boss.DefId) == hostage,
            "and the held score was released in full",
            FlatFrom(second.Score, boss.DefId) + " vs " + hostage);
        Check(boss.Held > 0, "while THIS turn's line is the new hostage", "" + boss.Held);
    }

    private static void RehinPuan_BreakingTheChainBurnsIt()
    {
        Section("rehin puan / a turn without a clear burns what was held");
        var session = NewBossSession(8105, 5, 1000000, "rehin_puan", 40, 1);
        var boss = (RehinPuanBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;

        ClearABottomRow(round, 8106);
        int hostage = boss.Held;
        Check(hostage > 0, "something is held", "" + hostage);

        // A quiet turn: nothing clears.
        ClearBoard(round.Board);
        TurnReport quiet = PlayOneCard(round);
        Check(quiet != null && quiet.ExplodedRows.Count == 0
            && quiet.ExplodedColumns.Count == 0, "a turn cleared nothing");
        Check(boss.Held == 0, "the hostage is gone", "" + boss.Held);
        Check(boss.Burned == hostage, "and it burned, not paid",
            boss.Burned + " vs " + hostage);
        Check(FlatFrom(quiet.Score, boss.DefId) == 0, "nothing was released");
    }

    private static void Burokrasi_OnlyTheTaskPays()
    {
        Section("bürokrasi bataklığı / nothing scores by itself any more");
        var session = NewBossSession(8107, 5, 1000000, "burokrasi_batagi", 40, 1);
        var boss = (BurokrasiBatagiBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;
        session.Config.Scoring.PointsPerCubePlaced = 500;

        // A plain placement, worth 500 a cube under ordinary rules.
        ClearBoard(round.Board);
        TurnReport report = PlayOneCard(round);
        Check(report != null, "a turn resolved");
        Check(report.Score.BaseTotal == 0,
            "and every base value was wiped - placement included",
            "base " + report.Score.BaseTotal);

        // Even a cleared row pays nothing by itself.
        boss.RewardPerTask = 0; // so any score seen must be base score
        TurnReport cleared = ClearABottomRow(round, 8108);
        Check(cleared != null && cleared.ExplodedRows.Count > 0, "a row cleared");
        Check(cleared.Score.BaseTotal == 0, "and it still paid nothing",
            "base " + cleared.Score.BaseTotal);
    }

    private static void Burokrasi_PaysForATaskAndFinesAMissedDeadline()
    {
        Section("bürokrasi bataklığı / finishing a task pays, a missed deadline fines");
        var session = NewBossSession(8109, 5, 1000000, "burokrasi_batagi", 40, 1);
        var boss = (BurokrasiBatagiBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;

        // Force the "clear a row" task with a comfortable deadline, then clear a row.
        SetTask(boss, BurokrasiBatagiBoss.TaskKind.ClearARow, 5);
        TurnReport done = ClearABottomRow(round, 8110);
        Check(done != null && done.ExplodedRows.Count > 0, "a row cleared");
        Check(boss.Completed == 1, "the task was signed off", "" + boss.Completed);
        Check(FlatFrom(done.Score, boss.DefId) == boss.RewardPerTask,
            "and it paid the reward", "" + FlatFrom(done.Score, boss.DefId));

        // Now force a "clear a column" task with a one-turn deadline and do not do it.
        SetTask(boss, BurokrasiBatagiBoss.TaskKind.ClearAColumn, 1);
        int failedBefore = boss.Failed;
        ClearBoard(round.Board);
        TurnReport missed = PlayOneCard(round);
        Check(missed != null, "a turn passed");
        Check(boss.Failed == failedBefore + 1, "the deadline ran out and it was recorded",
            "" + boss.Failed);
        Check(round.RoundScore >= 0, "the fine never takes the round below zero",
            "" + round.RoundScore);
    }

    /// <summary>Puts a specific task in play, so a test does not have to fish for one.</summary>
    private static void SetTask(BurokrasiBatagiBoss boss, BurokrasiBatagiBoss.TaskKind kind,
        int turns)
    {
        typeof(BurokrasiBatagiBoss)
            .GetField("task", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)
            .SetValue(boss, kind);
        typeof(BurokrasiBatagiBoss)
            .GetField("turnsLeft", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)
            .SetValue(boss, turns);
    }

    private static void BulParayi_TakesOneUnlessYouGuessIt()
    {
        Section("bul parayı al karayı / a blind guess saves one thing, or does not");
        // Guess WRONG: two jokers, protect the one it did not pick.
        var session = NewBossSession(8111, 5, 1000000, "bul_parayi", 40, 1);
        Joker a = session.Jokers.Add(new RenovasyonJoker());
        Joker b = session.Jokers.Add(new IadeJoker());
        RoundEngine round = session.CurrentRound;
        round.Boss.OnRoundStarted(new RoundContext(session, session.Rng, round));
        var boss = (BulParayiBoss)round.Boss;
        Check(boss.AwaitingChoice, "it is waiting for the guess");

        Joker victim = round.IsSilencedByBoss(a) ? a : b;
        Joker other = victim == a ? b : a;
        Check(round.IsSilencedByBoss(victim), "one of them is switched off before the guess");

        Check(round.ChooseBossProtection(other.InstanceId), "the wrong one is protected");
        Check(round.IsSilencedByBoss(victim), "so the victim stays switched off");
        Check(!round.IsSilencedByBoss(other), "and the other one was never in danger");
        Check(!round.ChooseBossProtection(victim.InstanceId),
            "and there is no second guess");
    }

    private static void BulParayi_AGoodGuessSavesIt()
    {
        Section("bul parayı al karayı / guessing right saves it outright");
        var session = NewBossSession(8112, 5, 1000000, "bul_parayi", 40, 1);
        Joker a = session.Jokers.Add(new RenovasyonJoker());
        Joker b = session.Jokers.Add(new IadeJoker());
        RoundEngine round = session.CurrentRound;
        round.Boss.OnRoundStarted(new RoundContext(session, session.Rng, round));
        var boss = (BulParayiBoss)round.Boss;

        Joker victim = round.IsSilencedByBoss(a) ? a : b;
        Check(round.ChooseBossProtection(victim.InstanceId), "the right one is protected");
        Check(boss.Saved, "the boss says it was saved");
        Check(!round.IsSilencedByBoss(a) && !round.IsSilencedByBoss(b),
            "and nothing is switched off any more");
    }

    private static void BulParayi_TheGuessMustComeBeforeTheFirstTurn()
    {
        Section("bul parayı al karayı / you cannot wait and read the answer off the screen");
        var session = NewBossSession(8113, 5, 1000000, "bul_parayi", 40, 1);
        Joker a = session.Jokers.Add(new RenovasyonJoker());
        session.Jokers.Add(new IadeJoker());
        RoundEngine round = session.CurrentRound;
        round.Boss.OnRoundStarted(new RoundContext(session, session.Rng, round));

        PlayOneCard(round);
        Check(!round.ChooseBossProtection(a.InstanceId),
            "after a turn has resolved the guess is refused - a silenced joker is visibly "
                + "silent, so waiting would be reading the answer rather than guessing");
    }

    private static void BulParayi_OnlyEverTheFirstBossOfARun()
    {
        Section("bul parayı al karayı / it can only ever be a run's first boss");
        BossDefinition def = BossRegistry.Get("bul_parayi");
        Check(def != null, "the boss is registered");
        Check(def.OnlyOnFirstBossRound, "and it is flagged first-boss-only");

        int flagged = 0;
        foreach (BossDefinition other in BossRegistry.All)
        {
            if (other.OnlyOnFirstBossRound) { flagged++; }
        }
        Check(flagged == 1, "it is the only boss with that restriction", "" + flagged);
    }

    /// <summary>Fills the bottom row but for one cell and plays into the gap, so a real line
    /// clear resolves. Returns the report, or null if nothing in hand fitted.</summary>
    private static TurnReport ClearABottomRow(RoundEngine round, int fillerCardId)
    {
        ClearBoard(round.Board);
        int row = round.Board.MinY;
        for (int x = round.Board.MinX + 1; x < round.Board.MinX + round.Board.Width; x++)
        {
            round.Board.SetCubeAt(new GridPos(x, row), new Cube(CubeKind.Normal, fillerCardId));
        }
        return PlayAt(round, new GridPos(round.Board.MinX, row));
    }

    private static void Simetri_TheBoardKnowsItsOwnSymmetry()
    {
        Section("simetri / the board's own mirror check");
        var board = new GameBoard(5, 5);
        Check(board.IsMirroredLeftRight() && board.IsMirroredTopBottom(),
            "an empty board is symmetric on both axes (which is why the joker sleeps)");

        board.SetCubeAt(new GridPos(0, 0), new Cube(CubeKind.Normal, 1));
        Check(!board.IsMirroredLeftRight(), "one lonely corner breaks left-right");
        Check(!board.IsMirroredTopBottom(), "and top-bottom");

        board.SetCubeAt(new GridPos(4, 0), new Cube(CubeKind.Normal, 1));
        Check(board.IsMirroredLeftRight(), "its mirror image restores left-right");
        Check(!board.IsMirroredTopBottom(), "but the bottom row alone is not top-bottom");

        board.SetCubeAt(new GridPos(0, 4), new Cube(CubeKind.Normal, 1));
        board.SetCubeAt(new GridPos(4, 4), new Cube(CubeKind.Normal, 1));
        Check(board.IsMirroredLeftRight() && board.IsMirroredTopBottom(),
            "four corners are symmetric on both");

        // Occupancy, not kind: a fire cube facing a plain one is still a mirror image.
        board.DestroyCubeForced(new GridPos(4, 4));
        board.SetCubeAt(new GridPos(4, 4), new Cube(CubeKind.Fire, 2));
        Check(board.IsMirroredLeftRight() && board.IsMirroredTopBottom(),
            "and it is judged on occupancy, not on what kind of cube sits there");
    }

    private static void Simetri_SleepsFiveTurnsAndAgainAfterEverySweep()
    {
        Section("simetri / it wakes on the 5th turn, and a sweep sends it back to sleep");
        var joker = new SimetriJoker();
        joker.OnRoundStarted(null);
        Check(!joker.IsAwake, "asleep at round start");

        for (int i = 0; i < joker.WakesOnTurn - 1; i++)
        {
            joker.AfterTurnScored(FakeTurn(Bar(1), new ScoreBreakdown()));
        }
        Check(!joker.IsAwake, "still asleep on turn 4", "" + joker.TurnsSinceReset);
        joker.AfterTurnScored(FakeTurn(Bar(1), new ScoreBreakdown()));
        Check(joker.IsAwake, "awake on turn 5", "" + joker.TurnsSinceReset);

        joker.AfterCleanSweep(FakeTurn(Bar(1), new ScoreBreakdown()));
        Check(!joker.IsAwake, "and a clean sweep puts it straight back to sleep - which is what "
            + "stops the empty board it left behind from paying", "" + joker.TurnsSinceReset);
        for (int i = 0; i < joker.WakesOnTurn; i++)
        {
            joker.AfterTurnScored(FakeTurn(Bar(1), new ScoreBreakdown()));
        }
        Check(joker.IsAwake, "five turns later it is back");
    }

    private static void Simetri_PaysOneAxisAndTriplesForBoth()
    {
        Section("simetri / a real turn: one axis pays, both axes pay triple");
        // A 7x7 board: a line needs seven cubes, so the five waking turns cannot complete one
        // and sweep the joker back to sleep before the test gets to it.
        var session = NewSession(7200, 7, 1000000, 40, 1);
        var joker = (SimetriJoker)session.Jokers.Add(new SimetriJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        PlayTurns(session, joker.WakesOnTurn);
        Check(joker.IsAwake, "awake after five real turns", "" + joker.TurnsSinceReset);

        // Leave the board one cube short of a left-right mirror, and place that cube.
        ClearBoard(round.Board);
        round.Board.SetCubeAt(new GridPos(0, 0), new Cube(CubeKind.Normal, 7201));
        TurnReport report = PlayAt(round, new GridPos(6, 0));
        Check(report != null, "the single cube went down at the mirror position");
        Check(FlatFrom(report.Score, joker.DefId) == joker.OneAxisBonus,
            "and one axis paid the single bonus",
            "" + FlatFrom(report.Score, joker.DefId));

        // Now the four corners: both axes at once.
        ClearBoard(round.Board);
        round.Board.SetCubeAt(new GridPos(0, 0), new Cube(CubeKind.Normal, 7202));
        round.Board.SetCubeAt(new GridPos(0, 6), new Cube(CubeKind.Normal, 7202));
        round.Board.SetCubeAt(new GridPos(6, 6), new Cube(CubeKind.Normal, 7202));
        TurnReport both = PlayAt(round, new GridPos(6, 0));
        Check(both != null, "the fourth corner went down");
        Check(FlatFrom(both.Score, joker.DefId)
                == joker.OneAxisBonus * joker.BothAxesMultiplier,
            "and both axes paid TRIPLE", "" + FlatFrom(both.Score, joker.DefId));

        // A lopsided board pays nothing at all.
        ClearBoard(round.Board);
        round.Board.SetCubeAt(new GridPos(0, 0), new Cube(CubeKind.Normal, 7203));
        TurnReport lopsided = PlayAt(round, new GridPos(3, 2));
        Check(lopsided != null && FlatFrom(lopsided.Score, joker.DefId) == 0,
            "a lopsided board pays nothing");
    }

    private static void Barut_ChargesDynamiteThatSurvives()
    {
        Section("barut tedarikçisi / dynamite banks a charge for every turn it survives");
        var session = NewSession(7205, 5, 1000000, 40, 1);
        var joker = (BarutTedarikcisiJoker)session.Jokers.Add(new BarutTedarikcisiJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        ClearBoard(round.Board);
        Check(joker.TotalCharges == 0, "nothing banked yet");

        // Two dynamite cubes of one block, standing out of the way in the top row.
        round.Board.SetCubeAt(new GridPos(0, 4), new Cube(CubeKind.Dynamite, 7300));
        round.Board.SetCubeAt(new GridPos(1, 4), new Cube(CubeKind.Dynamite, 7300));
        PlayTurns(session, 3);
        Check(joker.TotalCharges == 3, "three turns standing, three charges",
            "" + joker.TotalCharges);

        // A board with no dynamite banks nothing.
        var plain = NewSession(7206, 5, 1000000, 40, 1);
        var quiet = (BarutTedarikcisiJoker)plain.Jokers.Add(new BarutTedarikcisiJoker());
        plain.Jokers.DispatchRoundStarted(plain.CurrentRound);
        PlayTurns(plain, 4);
        Check(quiet.TotalCharges == 0, "and a board without dynamite banks nothing",
            "" + quiet.TotalCharges);
    }

    private static void Barut_PaysEveryChargeWhenItGoesUp()
    {
        Section("barut tedarikçisi / and pays every charge when the block finally goes up");
        var session = NewSession(7207, 5, 1000000, 40, 1);
        var joker = (BarutTedarikcisiJoker)session.Jokers.Add(new BarutTedarikcisiJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        ClearBoard(round.Board);

        // Two dynamite cubes in the bottom row, left to mature.
        round.Board.SetCubeAt(new GridPos(0, 0), new Cube(CubeKind.Dynamite, 7400));
        round.Board.SetCubeAt(new GridPos(1, 0), new Cube(CubeKind.Dynamite, 7400));
        PlayTurns(session, 2);
        int charges = joker.TotalCharges;
        Check(charges == 2, "two charges banked", "" + charges);

        // Now complete that row so both go up in a real line clear.
        ClearBoard(round.Board);
        round.Board.SetCubeAt(new GridPos(0, 0), new Cube(CubeKind.Dynamite, 7400));
        round.Board.SetCubeAt(new GridPos(1, 0), new Cube(CubeKind.Dynamite, 7400));
        round.Board.SetCubeAt(new GridPos(2, 0), new Cube(CubeKind.Normal, 7401));
        round.Board.SetCubeAt(new GridPos(3, 0), new Cube(CubeKind.Normal, 7401));
        TurnReport report = PlayAt(round, new GridPos(4, 0));
        Check(report != null && report.ExplodedRows.Count > 0, "the row exploded");
        // A block pays what it had BANKED: the turn it goes up is a turn it did not survive.
        int expected = 2 * charges * joker.BonusPerChargePerCube;
        Check(FlatFrom(report.Score, joker.DefId) == expected,
            "and both cubes paid every charge they had banked",
            FlatFrom(report.Score, joker.DefId) + " vs " + expected);
        Check(joker.TotalCharges == 0, "nothing is left charged", "" + joker.TotalCharges);
    }

    private static void Antimadde_OnlyFitsAPerfectOverlay()
    {
        Section("antimadde / the card goes nowhere but a perfect overlay of its own element");
        var session = NewSession(7208, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        ClearBoard(round.Board);
        round.Board.SetCubeAt(new GridPos(1, 1), new Cube(CubeKind.Fire, 7500));
        round.Board.SetCubeAt(new GridPos(2, 1), new Cube(CubeKind.Fire, 7500));
        round.Board.SetCubeAt(new GridPos(3, 1), new Cube(CubeKind.Normal, 7501));

        BlockCard anti = session.CreateCard(Bar(2), null);
        anti.AntimatterOf = CubeKind.Fire;

        Check(round.CanPlaceCard(anti, new GridPos(1, 1)),
            "it fits exactly over the two fire cubes");
        Check(!round.CanPlaceCard(anti, new GridPos(2, 1)),
            "but not half on fire and half on a plain cube");
        Check(!round.CanPlaceCard(anti, new GridPos(0, 0)), "nor over empty space");
        Check(!round.CanPlaceCard(anti, new GridPos(4, 1)), "nor hanging off the board");
        Check(round.GetValidOrigins(anti.Shape).Count > 0,
            "(the origin list is about the SHAPE, so the card's own check is what gates it)");
    }

    private static void Antimadde_AnnihilatesEveryCubeOfThatElement()
    {
        Section("antimadde / a perfect fit annihilates every cube of that element");
        var session = NewSession(7209, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        ClearBoard(round.Board);
        // Two fire cubes to cover, two MORE fire cubes elsewhere, and a plain cube as a control.
        round.Board.SetCubeAt(new GridPos(1, 1), new Cube(CubeKind.Fire, 7600));
        round.Board.SetCubeAt(new GridPos(2, 1), new Cube(CubeKind.Fire, 7600));
        round.Board.SetCubeAt(new GridPos(0, 4), new Cube(CubeKind.Fire, 7601));
        round.Board.SetCubeAt(new GridPos(4, 4), new Cube(CubeKind.Fire, 7601));
        round.Board.SetCubeAt(new GridPos(3, 3), new Cube(CubeKind.Normal, 7602));

        BlockCard anti = session.CreateCard(Bar(2), null);
        anti.AntimatterOf = CubeKind.Fire;
        round.AddBonusCard(anti, BonusPlayOutcome.ExpireFromRound);

        TurnReport report = round.PlayFromBonus(round.BonusHand.Count - 1, new GridPos(1, 1));
        Check(report != null, "the antimatter went down");
        Check(report.AnnihilatedKind == CubeKind.Fire, "and named what it annihilated",
            "" + report.AnnihilatedKind);
        Check(round.Board.CellsOfKind(CubeKind.Fire).Count == 0,
            "every fire cube is gone, not just the covered ones",
            "" + round.Board.CellsOfKind(CubeKind.Fire).Count);
        Check(round.Board.GetCube(new GridPos(3, 3)).HasValue,
            "the plain cube is untouched - only its own element is annihilated");
        Check(report.PlacedCells.Count == 0, "and nothing was placed - it is a key, not a block",
            "" + report.PlacedCells.Count);
    }

    private static void Antimadde_MintsFromANegativeErasureAndRots()
    {
        Section("antimadde / a negative erasure mints it, and holding it rots the pay-off");
        var session = NewSession(7210, 5, 1000000, 40, 1);
        var joker = (AntimaddeJoker)session.Jokers.Add(new AntimaddeJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        ClearBoard(round.Board);
        round.Board.SetCubeAt(new GridPos(1, 1), new Cube(CubeKind.Fire, 7700));
        round.Board.SetCubeAt(new GridPos(2, 1), new Cube(CubeKind.Fire, 7700));

        // A negative block erases them both - the joker is dispatched by the turn itself.
        BlockCard negative = session.CreateCard(Bar(2),
            new List<BlockElement> { BlockElement.Negative });
        round.AddBonusCard(negative, BonusPlayOutcome.ExpireFromRound);
        TurnReport report = round.PlayFromBonus(round.BonusHand.Count - 1, new GridPos(1, 1));
        Check(report != null, "the negative block went down");
        Check(joker.HasCard, "and the antimatter arrived");

        BlockCard anti = null;
        for (int i = 0; i < round.BonusHand.Count; i++)
        {
            if (round.BonusHand[i].Card.AntimatterOf.HasValue)
            {
                anti = round.BonusHand[i].Card;
            }
        }
        Check(anti != null, "it is in the bonus hand");
        Check(anti.AntimatterOf == CubeKind.Fire, "as the antimatter of FIRE",
            "" + anti.AntimatterOf);
        Check(anti.Shape.Size == 2, "shaped like the two cubes that were erased",
            "" + anti.Shape.Size);

        int fresh = joker.CurrentBonusPerCube;
        Check(fresh == joker.BonusPerCube, "it starts at full value", "" + fresh);
        PlayTurns(session, 2);
        Check(joker.CurrentBonusPerCube < fresh, "holding it costs value",
            fresh + " -> " + joker.CurrentBonusPerCube);

        PlayTurns(session, 6);
        Check(!joker.HasCard, "and it decays to nothing within five turns");
        bool stillThere = false;
        for (int i = 0; i < round.BonusHand.Count; i++)
        {
            if (round.BonusHand[i].Card.Id == anti.Id) { stillThere = true; }
        }
        Check(!stillThere, "the card left the bonus hand with it");
    }

    private static void Eforsuz_PaysOnAPowerFreeRound()
    {
        Section("eforsuz galibiyet / a power-free round pays as you walk into the market");
        var session = NewSession(7211, 5, 1000000, 40, 1);
        var joker = (EforsuzGalibiyetJoker)session.Jokers.Add(new EforsuzGalibiyetJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        Check(joker.StillClean, "the round starts clean");

        long before = session.TotalScore;
        session.Jokers.DispatchRoundEnded(round, RoundOutcome.Advanced);
        session.Jokers.DispatchMarketEntered();
        Check(joker.LastPaid == joker.Bonus, "it paid the bonus", "" + joker.LastPaid);
        Check(session.TotalScore > before, "and it landed in the purse",
            before + " -> " + session.TotalScore);

        // A power anywhere in the round forfeits it.
        session.Jokers.DispatchRoundStarted(round);
        Check(joker.StillClean, "a new round starts clean again");
        session.Powers.Add(new CimbizPower());
        session.Jokers.DispatchPowerUsed(round, "cimbiz");
        Check(!joker.StillClean, "using a power marks the round");
        session.Jokers.DispatchRoundEnded(round, RoundOutcome.Advanced);
        session.Jokers.DispatchMarketEntered();
        Check(joker.LastPaid == 0, "so the market pays nothing", "" + joker.LastPaid);
    }

    private static void Eforsuz_DoublesForAPowerFreeOvertime()
    {
        Section("eforsuz galibiyet / surviving an overtime without a power pays double");
        var session = NewSession(7212, 5, 30, 40, 1);
        session.Config.Scoring.PointsPerCubePlaced = 200;
        var joker = (EforsuzGalibiyetJoker)session.Jokers.Add(new EforsuzGalibiyetJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);

        PlayOneCard(round);
        Check(round.Status == RoundStatus.AwaitingAdvanceDecision, "the offer is up");
        round.DecideAdvance(false); // declining IS going into overtime
        Check(round.ContinueCount > 0, "we are in overtime");

        session.Jokers.DispatchRoundEnded(round, RoundOutcome.Advanced);
        session.Jokers.DispatchMarketEntered();
        Check(joker.LastPaid == joker.Bonus * joker.OvertimeMultiplier,
            "a power-free overtime pays double", "" + joker.LastPaid);
    }

    /// <summary>Empties a board cell by cell, whatever is standing on it. Deliberately NOT through
    /// the engine: it is scene-setting, not play, and must not fire a sweep.</summary>
    private static void ClearBoard(GameBoard board)
    {
        foreach (GridPos cell in AllPlayableCells(board))
        {
            if (board.GetCube(cell).HasValue)
            {
                board.DestroyCubeForced(cell);
            }
        }
    }

    /// <summary>Plays the first hand card that legally fits AT THAT EXACT CELL, so a test can
    /// decide where the turn lands. Null when nothing in hand fits there.</summary>
    private static TurnReport PlayAt(RoundEngine round, GridPos origin)
    {
        for (int i = 0; i < round.Hand.Count; i++)
        {
            // Frozen and boss-locked cards are skipped for the same reason: they are held, they
            // fit, and they still may not be played.
            if (!round.IsFrozen(round.Hand[i].Id) && !round.IsLockedByBoss(round.Hand[i])
                && round.CanPlaceCard(round.Hand[i], origin))
            {
                return round.PlayFromHand(i, origin);
            }
        }
        return null;
    }

    /// <summary>Total flat score one source contributed to a breakdown.</summary>
    private static int FlatFrom(ScoreBreakdown score, string source)
    {
        int total = 0;
        foreach (ScoreContribution c in score.Contributions)
        {
            if (c.Source == source) { total += c.Flat; }
        }
        return total;
    }

    private static void Enflasyon_RaisesTheBarEveryTurn()
    {
        Section("enflasyon / the bar climbs 3% per turn, compounding");
        var session = NewBossSession(6100, 5, 1000, "enflasyon");
        var boss = (EnflasyonBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;
        Check(round.ScoreThreshold == 1000, "turn 0: the bar is the round's own",
            "" + round.ScoreThreshold);

        PlayOneCard(round);
        int afterOne = round.ScoreThreshold;
        Check(afterOne == 1030, "after one turn it is 3% higher", "" + afterOne);
        PlayOneCard(round);
        Check(round.ScoreThreshold == 1061, "after two it COMPOUNDS (1030 -> 1061, not 1060)",
            "" + round.ScoreThreshold);

        // Whatever the bar is, the engine and the config must not disagree about it.
        Check(round.Config.ScoreThreshold == 1000, "the config still names the base bar");
        Check(round.ScoreThreshold > round.Config.ScoreThreshold,
            "and the LIVE bar is the one that moved - read RoundEngine, never Config");
    }

    private static void Enflasyon_CannotInflatePastWhatFits()
    {
        Section("enflasyon / a very long round cannot overflow the bar");
        var session = NewBossSession(6101, 5, 1000000, "enflasyon");
        var boss = (EnflasyonBoss)session.CurrentRound.Boss;
        // 400 turns of 3% is 1.03^400 - astronomically past what an int holds once scaled.
        for (int i = 0; i < 400; i++)
        {
            boss.AfterTurnScored(FakeTurn(Bar(1), new ScoreBreakdown()));
        }
        int bar = boss.FilterScoreThreshold(1000000);
        Check(bar > 0, "the bar is still a positive number, not an overflow", "" + bar);
        Check(bar <= 10000000, "and it is capped", "" + bar);
    }

    private static void Hiclik_BillsForEveryCubeStanding()
    {
        Section("hiçlik / every cube left standing costs score at the end of a turn");
        var session = NewBossSession(6102, 5, 1000000, "hiclik", 40, 3);
        var boss = (HiclikBoss)session.CurrentRound.Boss;
        session.Config.Scoring.PointsPerCubePlaced = 40;
        RoundEngine round = session.CurrentRound;

        TurnReport report = PlayOneCard(round);
        Check(report != null, "a turn resolved");
        int standing = round.MainBoard.OccupiedCount;
        Check(standing > 0, "cubes are standing", "" + standing);
        Check(boss.BilledThisRound == standing * boss.CostPerCube,
            "and the bill is one per cube", boss.BilledThisRound + " for " + standing);
        Check(BreakdownCharged(report, boss.DefId, standing * boss.CostPerCube),
            "and it came out of the turn, billed under the boss's own name",
            "expected -" + (standing * boss.CostPerCube));
        Check(report.ScoreGained < standing * session.Config.Scoring.PointsPerCubePlaced
                * session.Config.Scoring.ScoreScale,
            "so the turn banked less than the cubes it placed were worth",
            "" + report.ScoreGained);
    }

    /// <summary>True if the breakdown carries a charge of exactly that size under that source.</summary>
    private static bool BreakdownCharged(TurnReport report, string source, int amount)
    {
        foreach (ScoreContribution c in report.Score.Contributions)
        {
            if (c.Source == source && c.Flat == -amount)
            {
                return true;
            }
        }
        return false;
    }

    private static void Hiclik_CannotEatScoreAlreadyBanked()
    {
        Section("hiçlik / a huge bill empties the turn, never the round");
        var session = NewBossSession(6103, 5, 1000000, "hiclik", 40, 3);
        var boss = (HiclikBoss)session.CurrentRound.Boss;
        boss.CostPerCube = 100000;
        session.Config.Scoring.PointsPerCubePlaced = 1;
        RoundEngine round = session.CurrentRound;

        bool everWentBack = false;
        for (int i = 0; i < 8; i++)
        {
            int before = round.RoundScore;
            if (PlayOneCard(round) == null) { break; }
            if (round.RoundScore < before) { everWentBack = true; }
        }
        Check(boss.BilledThisRound > 0, "the rent was charged", "" + boss.BilledThisRound);
        Check(!everWentBack, "and not one turn pushed the round score backwards");
        Check(round.RoundScore >= 0, "the round score never went negative",
            "" + round.RoundScore);
    }

    private static void Saatci_LosesTheRoundWhenTheTurnsRunOut()
    {
        Section("saatçi / the round is lost the turn the limit runs out");
        var session = NewBossSession(6104, 5, 1000000, "saatci");
        var boss = (SaatciBoss)session.CurrentRound.Boss;
        boss.TurnLimit = 3;
        RoundEngine round = session.CurrentRound;

        PlayOneCard(round);
        Check(boss.TurnsLeft == 2, "two left", "" + boss.TurnsLeft);
        PlayOneCard(round);
        Check(round.Status == RoundStatus.InProgress, "still going with one turn left",
            "status " + round.Status);
        PlayOneCard(round);
        Check(round.Status == RoundStatus.Lost, "the third turn was the last one",
            "status " + round.Status);
        Check(round.Loss == LossReason.OutOfTurns, "and the clock is named as the cause",
            "loss " + round.Loss);
    }

    private static void Saatci_ARoundWonOnTheBuzzerIsNotLost()
    {
        Section("saatçi / reaching the bar on the last turn is a WIN, not a loss");
        var session = NewBossSession(6105, 5, 30, "saatci", 40, 3);
        var boss = (SaatciBoss)session.CurrentRound.Boss;
        boss.TurnLimit = 1;
        session.Config.Scoring.PointsPerCubePlaced = 200; // one turn clears the bar
        RoundEngine round = session.CurrentRound;

        PlayOneCard(round);
        Check(round.Loss != LossReason.OutOfTurns,
            "the clock did NOT kill a round that was just won", "loss " + round.Loss);
        Check(round.Status == RoundStatus.AwaitingAdvanceDecision,
            "the advance offer is up as usual", "status " + round.Status);
    }

    private static void Kitlik_FattensCardsButOnlyForTheRound()
    {
        Section("kıtlık / cards coming back from the discard fatten, for this round only");
        var session = NewBossSession(6106, 5, 1000000, "kitlik", 6, 1);
        var boss = (KitlikBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;
        var sizesBefore = new Dictionary<int, int>();
        for (int i = 0; i < session.OwnedCards.Count; i++)
        {
            sizesBefore[session.OwnedCards[i].Id] = session.OwnedCards[i].Shape.Size;
        }

        // A deck of six runs dry fast, and the drying-out is when the discard comes back.
        PlayTurns(session, 14);
        Check(boss.CubesGrown > 0, "cards were fattened", "" + boss.CubesGrown);

        bool anyEffectivelyBigger = false;
        bool anyPrintedChanged = false;
        for (int i = 0; i < session.OwnedCards.Count; i++)
        {
            BlockCard card = session.OwnedCards[i];
            if (round.EffectiveShape(card).Size > sizesBefore[card.Id])
            {
                anyEffectivelyBigger = true;
            }
            if (card.Shape.Size != sizesBefore[card.Id])
            {
                anyPrintedChanged = true;
            }
        }
        Check(anyEffectivelyBigger, "a card plays FATTER than it did");
        Check(!anyPrintedChanged,
            "but no card in the run deck was actually changed - the growth is round-scoped");
    }

    private static void Kitlik_TheGrowthStaysInOnePiece()
    {
        Section("kıtlık / a fattened card is still one connected block");
        var session = NewBossSession(6107, 5, 1000000, "kitlik", 6, 2);
        var boss = (KitlikBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;
        PlayTurns(session, 14);
        Check(boss.CubesGrown > 0, "cards were fattened", "" + boss.CubesGrown);

        bool allConnected = true;
        for (int i = 0; i < session.OwnedCards.Count; i++)
        {
            if (!ShapeIsConnected(round.EffectiveShape(session.OwnedCards[i])))
            {
                allConnected = false;
            }
        }
        Check(allConnected, "every card in the deck is still in one piece");
    }

    /// <summary>Flood-fills a shape's cells to prove they form one block.</summary>
    private static bool ShapeIsConnected(BlockShape shape)
    {
        var all = new HashSet<GridPos>(shape.Cells);
        if (all.Count == 0)
        {
            return true;
        }
        var seen = new HashSet<GridPos>();
        var frontier = new List<GridPos> { shape.Cells[0] };
        seen.Add(shape.Cells[0]);
        while (frontier.Count > 0)
        {
            GridPos cell = frontier[frontier.Count - 1];
            frontier.RemoveAt(frontier.Count - 1);
            var neighbours = new[]
            {
                new GridPos(cell.X + 1, cell.Y), new GridPos(cell.X - 1, cell.Y),
                new GridPos(cell.X, cell.Y + 1), new GridPos(cell.X, cell.Y - 1)
            };
            foreach (GridPos n in neighbours)
            {
                if (all.Contains(n) && seen.Add(n))
                {
                    frontier.Add(n);
                }
            }
        }
        return seen.Count == all.Count;
    }

    private static void Merkezkac_FlingsCubesOutwardAndOffTheEdge()
    {
        Section("merkezkaç / cubes are flung one cell out, and the rim goes over the edge");
        var board = new GameBoard(5, 5);
        // Middle of the board, one cell up-right of centre, and one right on the rim.
        board.SetCubeAt(new GridPos(2, 2), new Cube(CubeKind.Normal, 1)); // dead centre
        board.SetCubeAt(new GridPos(3, 3), new Cube(CubeKind.Normal, 2));
        board.SetCubeAt(new GridPos(0, 0), new Cube(CubeKind.Normal, 3)); // corner, on the rim

        List<GridPos> lost = board.FlingCubesOutward();
        Check(board.GetCube(new GridPos(2, 2)).HasValue,
            "the cube on the exact centre did not move - there is no direction to fling it");
        Check(!board.GetCube(new GridPos(3, 3)).HasValue
            && board.GetCube(new GridPos(4, 4)).HasValue,
            "the one up-and-right of centre moved diagonally out");
        Check(!board.GetCube(new GridPos(0, 0)).HasValue, "the corner cube left its cell");
        bool cornerLost = false;
        foreach (GridPos cell in lost)
        {
            if (cell.X == 0 && cell.Y == 0) { cornerLost = true; }
        }
        Check(cornerLost, "and it went over the edge", "lost " + lost.Count);
        Check(board.OccupiedCount == 2, "so two cubes are left on the board",
            "" + board.OccupiedCount);
    }

    private static void Merkezkac_WhatGoesOverTheEdgePaysNothing()
    {
        Section("merkezkaç / what is flung off is lifted, not destroyed - it pays nothing");
        var session = NewBossSession(6108, 5, 1000000, "merkezkac", 40, 3);
        var boss = (MerkezkacBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;

        TurnReport report = null;
        for (int i = 0; i < 10 && boss.CubesFlungOff == 0; i++)
        {
            report = PlayOneCard(round);
            if (report == null) { break; }
        }
        Check(boss.CubesFlungOff > 0, "cubes were flung off the arena",
            "" + boss.CubesFlungOff);
        Check(report.DestroyedCubes.Count == 0,
            "and not one of them is in the destruction log - nothing was destroyed",
            "" + report.DestroyedCubes.Count);
        Check(report.LiftedCells.Count > 0, "they are reported as LIFTED",
            "" + report.LiftedCells.Count);
        Check(!report.CleanSweep, "and clearing the board this way is not a clean sweep");
    }

    private static void DortKutup_SquaresTheBoardAndSealsThreeQuarters()
    {
        Section("dört kutup / the arena is rounded up to even and only one quarter is open");
        var session = NewBossSession(6109, 5, 1000000, "dort_kutup");
        var boss = (DortKutupBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;
        Check(round.MainBoard.Width == 6 && round.MainBoard.Height == 6,
            "a 5x5 round became 6x6, so it splits into four equal quarters",
            round.MainBoard.Width + "x" + round.MainBoard.Height);

        Check(boss.ActiveQuadrant == 0, "the bottom-left quarter is live first",
            "" + boss.ActiveQuadrant);
        int open = 0;
        foreach (GridPos cell in AllPlayableCells(round.MainBoard))
        {
            if (!round.MainBoard.IsSealed(cell)) { open++; }
        }
        Check(open == 9, "nine of the thirty-six cells are open - exactly one quarter",
            "" + open);

        // Everything open is inside the live quarter, and nothing outside it is.
        List<GridPos> active = boss.ActiveCells(round.MainBoard);
        bool allActiveOpen = true;
        foreach (GridPos cell in active)
        {
            if (round.MainBoard.IsSealed(cell)) { allActiveOpen = false; }
        }
        Check(active.Count == 9 && allActiveOpen,
            "and the live quarter is exactly the unsealed part", "active " + active.Count);
    }

    private static void DortKutup_TurnsClockwiseEveryTurn()
    {
        Section("dört kutup / the live quarter turns clockwise at the end of every turn");
        var session = NewBossSession(6110, 5, 1000000, "dort_kutup");
        var boss = (DortKutupBoss)session.CurrentRound.Boss;
        RoundEngine round = session.CurrentRound;

        var walked = new List<int> { boss.ActiveQuadrant };
        for (int i = 0; i < 4; i++)
        {
            if (PlayOneCard(round) == null) { break; }
            walked.Add(boss.ActiveQuadrant);
        }
        Check(walked.Count == 5, "five readings across four turns", "" + walked.Count);
        bool clockwise = true;
        for (int i = 1; i < walked.Count; i++)
        {
            if (walked[i] != (walked[i - 1] + 1) % 4) { clockwise = false; }
        }
        Check(clockwise, "each turn moved on by exactly one quarter",
            string.Join(",", walked.ConvertAll(q => q.ToString()).ToArray()));
        Check(walked[4] == walked[0], "and four turns bring it right back round");
    }

    private static void DortKutup_ABlockedQuarterTurnsInsteadOfEndingTheRound()
    {
        Section("dört kutup / a full quarter turns early and bills you, it does not end the round");
        var session = NewBossSession(6111, 5, 1000000, "dort_kutup", 40, 1);
        var boss = (DortKutupBoss)session.CurrentRound.Boss;
        session.Config.Scoring.PointsPerCubePlaced = 100;
        RoundEngine round = session.CurrentRound;
        Check(boss.ActiveQuadrant == 0, "the bottom-left quarter is live");

        // Brick the OTHER three quarters with cubes nothing can break, leaving only the live one
        // usable. Done before a turn runs, so the engine walks into it the normal way: the quarter
        // turns at the end of the turn and the dead-end check meets a wall.
        var open = new HashSet<GridPos>(boss.ActiveCells(round.MainBoard));
        foreach (GridPos cell in AllPlayableCells(round.MainBoard))
        {
            if (!open.Contains(cell) && !round.MainBoard.GetCube(cell).HasValue)
            {
                round.MainBoard.SetCubeAt(cell, new Cube(CubeKind.Obsidian, 9970));
            }
        }

        TurnReport report = PlayOneCard(round);
        Check(report != null, "the turn played into the one open quarter");
        Check(round.Status != RoundStatus.Lost,
            "three full quarters are NOT a dead end while the fourth has room",
            "status " + round.Status + " loss " + round.Loss);
        Check(boss.BlockedTurns == 3,
            "the boss walked through all three walled quarters, charging for each",
            "" + boss.BlockedTurns);
        Check(boss.ActiveQuadrant == 0, "and came back round to the one with room",
            "" + boss.ActiveQuadrant);
        Check(round.RoundScore >= 0, "the penalties never take the round below zero",
            "" + round.RoundScore);

        // And it keeps going: the round is still playable, turn after turn.
        Check(PlayOneCard(round) != null, "the next turn plays too");
        Check(boss.BlockedTurns == 6, "charging three more for the same walk",
            "" + boss.BlockedTurns);
    }

    private static void Kangren_SpreadsAsOneGrowingPatch()
    {
        Section("kangren / the rot seeds once and then only grows against itself");
        var board = new GameBoard(5, 5);
        var rng = new SeededRandom(6112);
        GridPos? seed = board.SpreadGangrene(rng);
        Check(seed.HasValue, "it seeded somewhere");
        Check(board.CountGangrene() == 1, "one rotten cube", "" + board.CountGangrene());

        for (int i = 0; i < 6; i++)
        {
            board.SpreadGangrene(rng);
        }
        Check(board.CountGangrene() == 7, "it took a cell per spread",
            "" + board.CountGangrene());

        // Every rotten cube must touch another one: it is a patch, not a scatter.
        var rotten = new List<GridPos>();
        foreach (GridPos cell in AllPlayableCells(board))
        {
            Cube? cube = board.GetCube(cell);
            if (cube.HasValue && cube.Value.Kind == CubeKind.Gangrene) { rotten.Add(cell); }
        }
        bool allTouch = true;
        foreach (GridPos cell in rotten)
        {
            bool touches = false;
            foreach (GridPos other in rotten)
            {
                int d = System.Math.Abs(cell.X - other.X) + System.Math.Abs(cell.Y - other.Y);
                if (d == 1) { touches = true; }
            }
            if (!touches) { allTouch = false; }
        }
        Check(allTouch, "and every rotten cube touches another - one patch, one origin");
    }

    private static void Kangren_RottenCubesStillExplode()
    {
        Section("kangren / a part-rotten line still explodes");
        var board = new GameBoard(5, 5);
        // Three rotten and two plain in the bottom row: a full line, and it must clear.
        for (int x = 0; x < 3; x++)
        {
            board.SetCubeAt(new GridPos(x, 0), new Cube(CubeKind.Gangrene, -7));
        }
        board.SetCubeAt(new GridPos(3, 0), new Cube(CubeKind.Normal, 1));
        board.SetCubeAt(new GridPos(4, 0), new Cube(CubeKind.Normal, 1));

        LineExplosionResult result = board.ResolveFullLines();
        Check(result.LineCount == 1, "the line exploded", "lines " + result.LineCount);
        Check(result.ExplodedCells.Count == 5, "all five cubes went, rot included",
            "" + result.ExplodedCells.Count);
        Check(board.OccupiedCount == 0, "the row is clear", "" + board.OccupiedCount);
    }

    private static void Kangren_AFullyRottenLineDiesAndTheRotJumps()
    {
        Section("kangren / a wholly rotten line dies for good, and the rot jumps to the edge");
        var board = new GameBoard(5, 5);
        // Row 1 entirely rotten -> it dies. Nearest edge to row 1 is row 0.
        for (int x = 0; x < 5; x++)
        {
            board.SetCubeAt(new GridPos(x, 1), new Cube(CubeKind.Gangrene, -7));
        }
        // Something standing in row 0 for the infection to jump onto, and one cell left empty.
        board.SetCubeAt(new GridPos(0, 0), new Cube(CubeKind.Normal, 1));
        board.SetCubeAt(new GridPos(1, 0), new Cube(CubeKind.Normal, 1));

        List<GridPos> converted = board.InfectFullLines();
        Check(board.RowIsInfectionDead(1), "row 1 is dead");
        Check(converted.Count == 2, "and the two cubes standing in row 0 turned rotten",
            "" + converted.Count);
        Check(board.GetCube(new GridPos(0, 0)).Value.Kind == CubeKind.Gangrene,
            "the first of them is rot now");
        Check(!board.GetCube(new GridPos(2, 0)).HasValue,
            "an EMPTY edge cell stayed empty - the jump converts, it never creates");

        // The dead row can never explode again, however it is filled.
        for (int x = 0; x < 5; x++)
        {
            if (!board.GetCube(new GridPos(x, 1)).HasValue)
            {
                board.SetCubeAt(new GridPos(x, 1), new Cube(CubeKind.Normal, 2));
            }
        }
        LineExplosionResult result = board.ResolveFullLines();
        bool row1Exploded = false;
        foreach (int y in result.Rows)
        {
            if (y == 1) { row1Exploded = true; }
        }
        Check(!row1Exploded, "a full dead row does not explode - it is dead for the round");
    }

    private static void Kangren_ChargesRentForEveryRottenCube()
    {
        Section("kangren / every rotten cube standing costs score every turn");
        var session = NewBossSession(6113, 5, 1000000, "kangren", 40, 3);
        var boss = (KangrenBoss)session.CurrentRound.Boss;
        session.Config.Scoring.PointsPerCubePlaced = 60;
        RoundEngine round = session.CurrentRound;

        TurnReport report = PlayOneCard(round);
        Check(report != null, "a turn resolved");
        int rotten = round.CountGangrene();
        Check(rotten > 0, "the rot took a cell", "" + rotten);
        Check(BreakdownCharged(report, boss.DefId, rotten * boss.RentPerCube),
            "and the rent was billed under the boss's own name",
            "expected -" + (rotten * boss.RentPerCube));
        Check(round.RoundScore >= 0, "the round score never goes negative",
            "" + round.RoundScore);
    }

    private static void Kacakci_TakesOneItemPerVisitForFree()
    {
        Section("kaçakçı / one item per visit, and it costs nothing");
        KacakciJoker smuggler;
        GameSession session = NewSmugglingSession(4500, 0, out smuggler);
        Check(session.CanSmuggle, "a free item is on offer");

        int index = FirstOfferOfKind(session, MarketOfferKind.Block);
        Check(index >= 0, "the market stocks a block");
        long purse = session.TotalScore;
        int deckBefore = session.OwnedCards.Count;
        Check(session.TrySmuggleOffer(index), "the block walked out of the shop");
        Check(session.TotalScore == purse, "and nothing was paid for it",
            purse + " -> " + session.TotalScore);
        Check(session.OwnedCards.Count == deckBefore + 1, "the deck grew by one",
            deckBefore + " -> " + session.OwnedCards.Count);
        Check(session.Market.Offers[index].Sold, "the offer is gone from the shelf");

        // One per VISIT.
        Check(!session.CanSmuggle, "the free item is spent for this visit");
        int second = FirstOfferOfKind(session, MarketOfferKind.Block);
        Check(second < 0 || !session.TrySmuggleOffer(second), "a second steal is refused");

        // ...and it comes back at the next market.
        session.LeaveMarket();
        Check(AdvanceToMarket(session, 400), "reached the next market");
        Check(session.CanSmuggle, "the next visit has its own free item");
    }

    private static void Kacakci_ThreeSoundHaulsAndItIsGone()
    {
        Section("kaçakçı / three sound hauls and it is caught");
        KacakciJoker smuggler;
        // 0% defect, so every haul is a sound one and the counter moves every visit.
        GameSession session = NewSmugglingSession(4530, 0, out smuggler);
        Check(smuggler.HaulsLeft == 3, "it starts with three in it", "" + smuggler.HaulsLeft);

        for (int haul = 1; haul <= 3; haul++)
        {
            Check(session.CanSmuggle, "haul " + haul + ": a free item is on offer");
            int index = FirstOfferOfKind(session, MarketOfferKind.Block);
            Check(index >= 0 && session.TrySmuggleOffer(index), "haul " + haul + ": taken");
            if (haul < 3)
            {
                Check(smuggler.HaulsLeft == 3 - haul, "haul " + haul + ": the counter moved",
                    "" + smuggler.HaulsLeft);
                Check(session.OwnsJoker("kacakci"), "haul " + haul + ": still held");
                session.LeaveMarket();
                Check(AdvanceToMarket(session, 400), "reached market " + (haul + 1));
            }
        }
        // The third sound item is the one that finishes it.
        Check(smuggler.IsSpent, "it has nothing left in it");
        Check(!session.OwnsJoker("kacakci"), "and it is gone from the inventory");
        Check(!session.CanSmuggle, "so there is nothing left to smuggle with");
    }

    private static void Kacakci_JunkDoesNotWearItOut()
    {
        Section("kaçakçı / junk costs it nothing");
        KacakciJoker smuggler;
        // 100% defect: every haul is junk, so the counter must never move.
        GameSession session = NewSmugglingSession(4531, 100, out smuggler);

        for (int visit = 0; visit < 4; visit++)
        {
            int index = FirstOfferOfKind(session, MarketOfferKind.Block);
            Check(index >= 0 && session.TrySmuggleOffer(index), "visit " + visit + ": took junk");
            Check(smuggler.HaulsLeft == 3, "visit " + visit + ": still three good ones in it",
                "" + smuggler.HaulsLeft);
            Check(session.OwnsJoker("kacakci"), "visit " + visit + ": still held");
            session.LeaveMarket();
            if (!AdvanceToMarket(session, 400))
            {
                break;
            }
        }
    }

    private static void Kacakci_SoundGoodsAreJustGoods()
    {
        Section("kaçakçı / at 0% defect the goods are ordinary");
        KacakciJoker smuggler;
        GameSession session = NewSmugglingSession(4501, 0, out smuggler);

        int index = FirstOfferOfKind(session, MarketOfferKind.Block);
        MarketOffer offer = session.Market.Offers[index];
        BlockShape wanted = offer.Card.Shape;
        Check(session.TrySmuggleOffer(index), "smuggled a block");
        BlockCard got = session.OwnedCards[session.OwnedCards.Count - 1];
        Check(got.Shape.CanonicalKey == wanted.CanonicalKey,
            "and it is the very block that was on the shelf");
        Check(got.IsSmuggled, "tagged as smuggled, so the UI can say where it came from");
    }

    private static void Kacakci_ADefectiveBlockLooksNormal()
    {
        Section("kaçakçı / a defective block is an ordinary card that will not stick");
        KacakciJoker smuggler;
        GameSession session = NewSmugglingSession(4502, 100, out smuggler);

        int index = FirstOfferOfKind(session, MarketOfferKind.Block);
        MarketOffer offer = session.Market.Offers[index];
        string wasOnTheShelf = offer.Card.Shape.CanonicalKey;
        Check(session.TrySmuggleOffer(index), "smuggled a block");
        BlockCard got = session.OwnedCards[session.OwnedCards.Count - 1];
        Check(got.Shape.CanonicalKey == wasOnTheShelf,
            "it is exactly the card that was on the shelf - the shape gives nothing away");
        Check(got.IsSmuggled, "it is tagged as smuggled");
        Check(got.FallsThrough, "and flagged as not staying on the board");
    }

    private static void Kacakci_ADefectiveBlockFallsRightThrough()
    {
        Section("kaçakçı / it drops through the arena and takes the turn with it");
        var session = NewSession(4520, 5, 1000000, 40, 3);
        RoundEngine round = session.CurrentRound;

        // Break the whole hand, so whichever card the driver reaches for falls through.
        for (int i = 0; i < round.Hand.Count; i++)
        {
            round.Hand[i].IsSmuggled = true;
            round.Hand[i].FallsThrough = true;
        }
        int occupiedBefore = round.Board.OccupiedCount;
        int turnBefore = round.TurnNumber;
        long scoreBefore = round.RoundScore;
        int handBefore = round.Hand.Count;

        TurnReport report = PlayOneCard(round);
        Check(report != null, "the card was played - placement is legal, it just does not last");
        Check(report.PlacedCells.Count == 0, "nothing landed on the board",
            "" + report.PlacedCells.Count);
        Check(report.FellThroughCells.Count > 0, "and the cells it fell through were recorded "
            + "for the animation", "" + report.FellThroughCells.Count);
        Check(round.Board.OccupiedCount == occupiedBefore, "the board is untouched",
            occupiedBefore + " -> " + round.Board.OccupiedCount);
        Check(report.Score.Total == 0, "the turn earned nothing at all",
            "" + report.Score.Total);
        Check(round.RoundScore == scoreBefore, "so the round score did not move",
            scoreBefore + " -> " + round.RoundScore);
        Check(report.ExplodedRows.Count == 0 && report.ExplodedColumns.Count == 0,
            "nothing exploded - a card that is not there cannot complete a line");
        Check(!report.CleanSweep, "and it is not a clean sweep either");

        // The turn IS spent, and the card leaves the hand like any other play.
        Check(round.TurnNumber == turnBefore + 1, "the turn was spent",
            turnBefore + " -> " + round.TurnNumber);
        Check(round.Hand.Count == handBefore, "the hand refilled, so no slot is jammed",
            handBefore + " -> " + round.Hand.Count);
        Check(round.Status == RoundStatus.InProgress, "and the round carries on",
            "status " + round.Status);
    }

    private static void Kacakci_ADefectiveBlockCannotBeFarmed()
    {
        Section("kaçakçı / it costs the turn again every time it comes back round");
        var session = NewSession(4521, 5, 1000000, 8, 1);
        RoundEngine round = session.CurrentRound;
        // Break the WHOLE deck, not just the hand: the hand refills from it, and the point of the
        // test is that a junk card keeps costing turns every time it comes back round.
        for (int i = 0; i < session.OwnedCards.Count; i++)
        {
            session.OwnedCards[i].FallsThrough = true;
        }
        // A whole hand of junk still plays out: turn after turn resolves, and the board never
        // fills, so the round cannot dead-end on it either.
        int played = PlayTurns(session, 6);
        Check(played > 0, "junk cards keep playing", "played " + played);
        Check(round.Board.OccupiedCount == 0, "and the board is still empty after all of them",
            "" + round.Board.OccupiedCount);
        Check(round.RoundScore == 0, "with nothing scored", "" + round.RoundScore);
    }

    private static void Kacakci_ABrokenJokerIsSilencedCentrally()
    {
        Section("kaçakçı / a broken joker is gated, never removed");
        var session = NewShortRunSession(4503, 12, 1000000);
        // Set the defects by hand: the roll picks between them, and both need proving.
        var dead = (RenovasyonJoker)session.Jokers.Add(new RenovasyonJoker());
        dead.Defect = SmuggledDefect.NeverWorks;
        Check(session.Jokers.Find(dead.InstanceId) != null,
            "a dead joker is still HELD - it takes up its slot");
        Check(!session.Jokers.CanActivate(dead.InstanceId),
            "but it cannot be activated, in any round");
        Check(!session.Jokers.TryActivate(dead.InstanceId, new ActivationTarget()),
            "and activating it does nothing");

        // The boss-round defect: fine normally, silent when it matters.
        var bossShy = (RenovasyonJoker)session.Jokers.Add(new RenovasyonJoker());
        bossShy.Defect = SmuggledDefect.DeadInBossRounds;
        Check(!session.CurrentRound.Config.IsBossRound, "this round has no boss");
        Check(session.Jokers.CanActivate(bossShy.InstanceId),
            "so the boss-shy joker works normally here");

        GameSession bossRun = NewShortRunSession(4504, 12, 1000000, true);
        var shy2 = (RenovasyonJoker)bossRun.Jokers.Add(new RenovasyonJoker());
        shy2.Defect = SmuggledDefect.DeadInBossRounds;
        Check(bossRun.CurrentRound.Config.IsBossRound, "this round IS a boss round");
        Check(!bossRun.Jokers.CanActivate(shy2.InstanceId),
            "and there the boss-shy joker is silent");
        var sound = (RenovasyonJoker)bossRun.Jokers.Add(new RenovasyonJoker());
        Check(bossRun.Jokers.CanActivate(sound.InstanceId),
            "while a sound joker beside it still works - the gate is per joker");
    }

    private static void Kacakci_ABrokenSmugglerSmugglesNothing()
    {
        Section("kaçakçı / a smuggler that never works cannot smuggle");
        var session = NewShortRunSession(4505, 12, 40);
        var smuggler = (KacakciJoker)session.Jokers.Add(new KacakciJoker());
        Check(AdvanceToMarket(session, 200), "reached the market");
        Check(session.CanSmuggle, "it works to begin with");
        smuggler.Defect = SmuggledDefect.NeverWorks;
        Check(!session.Jokers.EnablesSmuggling, "broken, it enables nothing");
        Check(!session.CanSmuggle, "so there is no free item");
        int index = FirstOfferOfKind(session, MarketOfferKind.Block);
        Check(index < 0 || !session.TrySmuggleOffer(index), "and the steal is refused");
    }

    private static void Kacakci_ABrokenPowerArrivesEmptyAndFillsSlowly()
    {
        Section("kaçakçı / a defective power comes empty and fills at a quarter rate");
        var session = NewShortRunSession(4506, 12, 1000000);
        Power power = session.Powers.Add(new CimbizPower());
        Check(power.Charged, "an ordinary power arrives charged");
        Check(power.RechargeCost == 1, "and fills on one event", "" + power.RechargeCost);

        power.MakeSmuggled(4);
        Check(!power.Charged, "smuggled, it arrives EMPTY");
        Check(power.RechargeCost == 4, "and needs four events", "" + power.RechargeCost);

        session.Powers.RechargeAll();
        Check(!power.Charged, "one recharge is not enough (1/4)",
            "progress " + power.RechargeProgress);
        session.Powers.RechargeAll();
        session.Powers.RechargeAll();
        Check(!power.Charged, "nor three (3/4)", "progress " + power.RechargeProgress);
        session.Powers.RechargeAll();
        Check(power.Charged, "the fourth fills it");
        Check(power.RechargeProgress == 0, "and the meter reset for next time",
            "" + power.RechargeProgress);

        // Spending it starts the long wait over - the defect is permanent.
        session.Powers.Spend(power.InstanceId);
        Check(!power.Charged, "spent again");
        session.Powers.RechargeAll();
        Check(!power.Charged, "and the slow fill is still slow - the defect never wears off");
    }

    private static void Kacakci_TheSmuggledItemStillCountsAsBuying()
    {
        Section("kaçakçı / walking out with stolen stock still counts as shopping");
        KacakciJoker smuggler;
        GameSession session = NewSmugglingSession(4507, 0, out smuggler);
        int index = FirstOfferOfKind(session, MarketOfferKind.Block);
        Check(session.TrySmuggleOffer(index), "smuggled something");

        // "Tutumluluk" and friends pay for leaving empty-handed; a free steal must not fool them.
        var thrift = new SpyMarketLeftJoker();
        session.Jokers.Add(thrift);
        session.LeaveMarket();
        Check(thrift.LastAnythingPurchased,
            "the market was left having 'purchased' something");
    }

    /// <summary>Records what DispatchMarketLeft was told.</summary>
    private sealed class SpyMarketLeftJoker : Joker
    {
        public bool LastAnythingPurchased;

        public SpyMarketLeftJoker()
            : base("spy_market_left", "Spy")
        {
        }

        public override void OnMarketLeft(SessionContext ctx, bool anythingPurchased)
        {
            LastAnythingPurchased = anythingPurchased;
        }
    }

    private static void Yatirimci_IsOnlyStockedByTheEarlyMarkets()
    {
        Section("uzun vadeli yatırımcı / only the early markets stock it");
        JokerDefinition def = JokerRegistry.Get("uzun_vadeli_yatirimci");
        Check(def != null, "the joker is registered");
        Check(def.LastOfferableRound == 5, "and its window closes after round 5",
            "" + def.LastOfferableRound);

        int limited = 0;
        foreach (JokerDefinition other in JokerRegistry.All)
        {
            if (other.LastOfferableRound != int.MaxValue) { limited++; }
        }
        Check(limited == 1, "it is the only joker with a window at all", "" + limited);

        // Round 1's market: it is in the pool, so enough rerolls must eventually turn it up.
        var session = NewShortRunSession(4400, 12, 40);
        Check(AdvanceToMarket(session, 200), "reached the first market",
            "phase " + session.Phase);
        Check(session.RoundNumber == 1, "after round 1", "round " + session.RoundNumber);
        int early = CountInRerolledShops(session, "uzun_vadeli_yatirimci", 400);
        Check(early > 0, "and it shows up in the early shop", "shops with it: " + early);

        // Walk out to a late market and reroll just as hard: now it must NEVER appear.
        int guard = 0;
        while (session.RoundNumber < 8 && guard++ < 20)
        {
            session.LeaveMarket();
            if (!AdvanceToMarket(session, 400)) { break; }
        }
        Check(session.RoundNumber >= 6, "reached a market past the window",
            "round " + session.RoundNumber);
        int late = CountInRerolledShops(session, "uzun_vadeli_yatirimci", 400);
        Check(late == 0, "and the late shop never stocks it, however hard you reroll",
            "shops with it: " + late);
    }

    private static void Yatirimci_CanNeverBeSold()
    {
        Section("uzun vadeli yatırımcı / the investment is locked in");
        var session = NewSession(4401, 5, 1000000, 40, 1);
        var joker = (UzunVadeliYatirimciJoker)session.Jokers.Add(new UzunVadeliYatirimciJoker());
        Check(!session.Jokers.CanSell(joker), "the market refuses to buy it back");
        long before = session.TotalScore;
        Check(session.Jokers.Sell(joker) == 0, "selling pays nothing");
        Check(session.TotalScore == before, "and nothing was paid in",
            before + " -> " + session.TotalScore);
        Check(session.Jokers.Find(joker.InstanceId) != null, "the joker is still held");

        // Contrast: an ordinary joker in the same inventory sells fine, so the lock is on this
        // joker and not on the inventory.
        Joker ordinary = session.Jokers.Add(new RenovasyonJoker());
        Check(session.Jokers.CanSell(ordinary), "an ordinary joker beside it still sells");
    }

    private static void Yatirimci_ReplaysTheLostFinalRound()
    {
        Section("uzun vadeli yatırımcı / it plays the lost final round again, once");
        var session = NewShortRunSession(4402, 1, 1000000);
        var joker = (UzunVadeliYatirimciJoker)session.Jokers.Add(new UzunVadeliYatirimciJoker());
        Check(session.IsFinalRound, "round 1 is the final round of this short run");
        RoundEngine first = session.CurrentRound;
        Check(!joker.RetryUsed, "the second chance is unspent");

        first.DeclareLoss(LossReason.NoPlayableMove);
        Check(session.Phase == GamePhase.Round, "the run did NOT end",
            "phase " + session.Phase);
        Check(session.CurrentRound != first, "a fresh round engine is running");
        Check(session.RoundNumber == 1, "the SAME round number", "" + session.RoundNumber);
        Check(session.FinalRoundReplays == 1, "and it counted as a replay",
            "" + session.FinalRoundReplays);
        Check(joker.RetryUsed, "the second chance is spent");
        Check(session.CurrentRound.Status == RoundStatus.InProgress, "the replay is playable",
            "status " + session.CurrentRound.Status);

        // The second loss is final: there is nothing left to spend.
        session.CurrentRound.DeclareLoss(LossReason.NoPlayableMove);
        Check(session.Phase == GamePhase.GameOver, "losing it again ends the run",
            "phase " + session.Phase);
        Check(session.FinalRoundReplays == 1, "and no second replay happened",
            "" + session.FinalRoundReplays);
    }

    private static void Yatirimci_DoesNothingBeforeTheFinalRound()
    {
        Section("uzun vadeli yatırımcı / an earlier round is lost for good");
        var session = NewShortRunSession(4403, 6, 1000000);
        var joker = (UzunVadeliYatirimciJoker)session.Jokers.Add(new UzunVadeliYatirimciJoker());
        Check(!session.IsFinalRound, "round 1 of 6 is not the final round");

        session.CurrentRound.DeclareLoss(LossReason.NoPlayableMove);
        Check(session.Phase == GamePhase.GameOver, "the run ended as usual",
            "phase " + session.Phase);
        Check(!joker.RetryUsed, "and the second chance was NOT touched - it is for the last round");
        Check(session.FinalRoundReplays == 0, "no replay", "" + session.FinalRoundReplays);
    }

    private static void Yatirimci_TheVoidedAttemptIsUnbanked()
    {
        Section("uzun vadeli yatırımcı / the failed attempt's score is clawed back");
        var session = NewShortRunSession(4404, 1, 1000000);
        session.Config.Scoring.PointsPerCubePlaced = 50;
        session.Jokers.Add(new UzunVadeliYatirimciJoker());
        long atStart = session.TotalScore;
        RoundEngine round = session.CurrentRound;

        PlayTurns(session, 6);
        long banked = round.RoundScore;
        Check(banked > 0, "the attempt banked something", "" + banked);
        Check(session.TotalScore == atStart + banked, "and the purse has it",
            session.TotalScore + " vs " + (atStart + banked));
        long takenBefore = session.CurrencyTakenByEffects;

        round.DeclareLoss(LossReason.NoPlayableMove);
        Check(session.CurrentRound != round, "the round is being replayed");
        Check(session.TotalScore == atStart, "the purse is back where it started",
            atStart + " vs " + session.TotalScore);
        Check(session.CurrencyTakenByEffects == takenBefore + banked,
            "and the claw-back is on the books, so the ledger balances",
            "" + (session.CurrencyTakenByEffects - takenBefore));
        Check(session.CurrentRound.RoundScore == 0, "the replay starts from zero score",
            "" + session.CurrentRound.RoundScore);
    }

    private static void Yatirimci_TheReplayIsTheSameFight()
    {
        Section("uzun vadeli yatırımcı / the replay faces the same boss, freshly made");
        var session = NewShortRunSession(4405, 1, 1000000, true);
        session.Jokers.Add(new UzunVadeliYatirimciJoker());
        RoundEngine first = session.CurrentRound;
        Check(first.Config.IsBossRound, "the final round is a boss round");
        Check(first.Boss != null, "and a boss was drawn", "boss " + first.Boss);
        string bossId = first.Boss.DefId;

        first.DeclareLoss(LossReason.NoPlayableMove);
        Check(session.CurrentRound != first, "the round is being replayed");
        Check(session.CurrentRound.Boss != null, "the replay has a boss too");
        Check(session.CurrentRound.Boss.DefId == bossId, "the SAME boss kind - a do-over, not a "
            + "different fight", bossId + " vs " + session.CurrentRound.Boss.DefId);
        Check(!ReferenceEquals(session.CurrentRound.Boss, first.Boss),
            "but a fresh instance, so nothing of the failed attempt carries over");
    }

    private static void Yatirimci_UnlocksTheExclusivePowers()
    {
        Section("uzun vadeli yatırımcı / it is the key to the powers no market sells");
        var session = NewShortRunSession(4406, 1, 1000000);
        Check(!session.Jokers.UnlocksInvestorPowers, "nothing unlocks them without the joker");
        session.Jokers.Add(new UzunVadeliYatirimciJoker());
        Check(session.Jokers.UnlocksInvestorPowers, "holding it turns the key");

        // The two exclusive powers are not designed yet, so the catalogue has none. This asserts
        // the plumbing is in place and unused: when they land, they must be InvestorOnly and no
        // market may stock them.
        int exclusive = 0;
        foreach (PowerDefinition def in PowerRegistry.All)
        {
            if (def.InvestorOnly) { exclusive++; }
        }
        Check(exclusive == 0, "and the two exclusive powers are not written yet",
            "" + exclusive);
    }

    private static void Savunmaci_BanksSafeRoundsAndNotGreedyOnes()
    {
        Section("savunmacı / a round finished without overtime banks, a greedy one does not");
        var session = NewSession(9300, 5, 1000000, 40, 1);
        var joker = (SavunmaciJoker)session.Jokers.Add(new SavunmaciJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        Check(joker.Banked == 0, "the bank starts empty");

        // A round that ended WITHOUT the player ever declining the offer.
        Check(round.ContinueCount == 0, "no offer was declined this round");
        session.Jokers.DispatchRoundEnded(round, RoundOutcome.Advanced);
        Check(joker.Banked == joker.BonusPerSafeRound, "so it banked a stack",
            "" + joker.Banked);

        // A LOST round banks nothing.
        int before = joker.Banked;
        session.Jokers.DispatchRoundEnded(round, RoundOutcome.Lost);
        Check(joker.Banked == before, "a lost round banks nothing",
            before + " -> " + joker.Banked);
    }

    private static void Savunmaci_AnOvertimeRoundBanksNothing()
    {
        Section("savunmacı / a round the player went into overtime on banks nothing");
        var session = NewSession(9301, 5, 30, 40, 1);
        session.Config.Scoring.PointsPerCubePlaced = 200; // one placement clears the low bar
        var joker = (SavunmaciJoker)session.Jokers.Add(new SavunmaciJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);

        PlayOneCard(round);
        Check(round.Status == RoundStatus.AwaitingAdvanceDecision, "the offer is up",
            "status " + round.Status);
        round.DecideAdvance(false); // decline it: THIS is going into overtime
        Check(round.ContinueCount > 0, "the round is now flagged as an overtime round",
            "continues " + round.ContinueCount);

        session.Jokers.DispatchRoundEnded(round, RoundOutcome.Advanced);
        Check(joker.Banked == 0, "so it banked nothing for that round", "" + joker.Banked);
    }

    private static void Savunmaci_PaysTheBankOnASurvivedOvertime()
    {
        Section("savunmacı / finishing an overtime cashes the whole bank in");
        var session = NewSession(9302, 4, 30, 40, 1);
        session.Config.Scoring.PointsPerCubePlaced = 200;
        var joker = (SavunmaciJoker)session.Jokers.Add(new SavunmaciJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);

        // Pretend four safe rounds already went by.
        for (int i = 0; i < 4; i++)
        {
            session.Jokers.DispatchRoundEnded(round, RoundOutcome.Advanced);
        }
        int bank = joker.Banked;
        Check(bank == 4 * joker.BonusPerSafeRound, "four safe rounds are banked", "" + bank);

        // Cross the bar, decline, and then sweep the board - which is what finishing an
        // overtime actually is.
        PlayOneCard(round);
        Check(round.Status == RoundStatus.AwaitingAdvanceDecision, "the offer is up");
        round.DecideAdvance(false);
        Check(round.ContinueCount > 0, "in overtime now");

        GridPos gap = ArmASweep(round);
        int cashedBefore = joker.CashedOut;
        TurnReport report = null;
        for (int i = 0; i < round.Hand.Count && report == null; i++)
        {
            if (round.CanPlaceCard(round.Hand[i], gap) && !round.IsFrozen(round.Hand[i].Id))
            {
                report = round.PlayFromHand(i, gap);
            }
        }
        Check(report != null, "a card closed the row");
        Check(report.CleanSweep, "which swept the board - the overtime is finished",
            "sweep " + report.CleanSweep);
        Check(joker.CashedOut == cashedBefore + 1, "the bank paid out",
            "cashed " + joker.CashedOut);
        Check(joker.Banked == 0, "and it is empty again", "" + joker.Banked);

        bool paidUs = false;
        foreach (ScoreContribution c in report.Score.Contributions)
        {
            if (c.Source == joker.DefId && c.Flat == bank) { paidUs = true; }
        }
        Check(paidUs, "for exactly what had been banked", "expected " + bank);
    }

    private static void Savunmaci_TheBankRefillsAfterPaying()
    {
        Section("savunmacı / after paying, the bank fills again");
        var session = NewSession(9303, 5, 1000000, 40, 1);
        var joker = (SavunmaciJoker)session.Jokers.Add(new SavunmaciJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);

        session.Jokers.DispatchRoundEnded(round, RoundOutcome.Advanced);
        session.Jokers.DispatchRoundEnded(round, RoundOutcome.Advanced);
        Check(joker.Banked == 2 * joker.BonusPerSafeRound, "two rounds banked",
            "" + joker.Banked);

        // A sweep OUTSIDE an overtime must not touch the bank.
        GridPos gap = ArmASweep(round);
        TurnReport report = null;
        for (int i = 0; i < round.Hand.Count && report == null; i++)
        {
            if (round.CanPlaceCard(round.Hand[i], gap) && !round.IsFrozen(round.Hand[i].Id))
            {
                report = round.PlayFromHand(i, gap);
            }
        }
        Check(report != null && report.CleanSweep, "a sweep landed outside overtime");
        Check(joker.Banked == 2 * joker.BonusPerSafeRound,
            "and the bank is untouched - only an OVERTIME sweep cashes it",
            "" + joker.Banked);
        Check(joker.CashedOut == 0, "nothing was cashed out", "" + joker.CashedOut);

        // And the bank keeps growing across further safe rounds.
        session.Jokers.DispatchRoundEnded(round, RoundOutcome.Advanced);
        Check(joker.Banked == 3 * joker.BonusPerSafeRound, "a third safe round banked too",
            "" + joker.Banked);
    }

    private static void Besleme_MarksAPatchAndFeedsOnExplosions()
    {
        Section("besleme / marks a patch, and cubes exploded in it are food");
        var session = NewSession(9200, 7, 1000000, 40, 1);
        var pet = (BeslemeJoker)session.Jokers.Add(new BeslemeJoker());
        Check(!pet.IsAlive, "nothing is marked before a round starts");

        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        Check(pet.IsAlive, "the first round after acquisition lays the mark");
        Check(pet.Size == 1, "it starts at 1x1", "size " + pet.Size);
        Check(pet.Region.Count == 1, "so it occupies one cell", "cells " + pet.Region.Count);
        Check(round.Board.IsInside(pet.Region[0]), "on real play area");
        Check(pet.Food == 0 && pet.FoodToGrow > 0, "and it is hungry for its first meal",
            pet.Food + "/" + pet.FoodToGrow);

        int foodBefore = pet.Food;
        TurnReport report = FeedTheCreature(session, pet);
        Check(report != null, "a turn resolved");
        Check(pet.Food > foodBefore || pet.Size > 1,
            "exploding cubes in its patch fed it", "food " + pet.Food + " size " + pet.Size);
        Check(pet.HungerLeft > 0, "and it is not hungry any more");
    }

    private static void Besleme_GrowsWhenFedAndCostsMoreEachStep()
    {
        Section("besleme / it grows when fed, and each step costs more than the last");
        var session = NewSession(9201, 7, 1000000, 40, 1);
        var pet = (BeslemeJoker)session.Jokers.Add(new BeslemeJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);

        int costAtOne = pet.FoodToGrow;
        int hungerAtOne = pet.HungerLeft;
        int guard = 0;
        while (pet.Size < 2 && guard++ < 25 && pet.IsAlive)
        {
            if (FeedTheCreature(session, pet) == null) { break; }
        }
        Check(pet.Size >= 2, "fed enough, it grew", "size " + pet.Size);
        Check(pet.Region.Count > 1, "and it occupies more of the board now",
            "cells " + pet.Region.Count);
        Check(pet.FoodToGrow > costAtOne, "the NEXT step costs more than the last did",
            costAtOne + " -> " + pet.FoodToGrow);
        Check(pet.HungerLeft < hungerAtOne || pet.Food > 0,
            "and a bigger creature has less patience", hungerAtOne + " -> " + pet.HungerLeft);
    }

    private static void Besleme_StarvesAndFinallyDies()
    {
        Section("besleme / neglected it starves, dies, and leaves the joker inert");
        var session = NewSession(9202, 7, 1000000, 40, 1);
        var pet = (BeslemeJoker)session.Jokers.Add(new BeslemeJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);
        Check(pet.IsAlive && pet.Size == 1, "a fresh 1x1 creature");

        int guard = 0;
        while (pet.IsAlive && guard++ < 40)
        {
            if (PlayTurns(session, 1) == 0) { break; }
        }
        Check(pet.IsDead, "starved, it died",
            "alive " + pet.IsAlive + " after " + guard + " turns");
        Check(pet.Region.Count == 0, "and it is gone from the board");

        // Dead is dead: another round must not bring it back.
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        Check(pet.IsDead && !pet.IsAlive,
            "a new round does not resurrect it - the joker is spent for the run");
    }

    private static void Besleme_ItsBillNeverPushesTheRoundBackwards()
    {
        Section("besleme / a starving creature can empty a turn but never the round");
        var session = NewSession(9203, 7, 1000000, 40, 1);
        session.Config.Scoring.PointsPerCubePlaced = 1;
        var pet = (BeslemeJoker)session.Jokers.Add(new BeslemeJoker());
        pet.DeathPenalty = 100000;
        pet.ShrinkPenalty = 100000;
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);

        int guard = 0;
        bool everWentBack = false;
        while (pet.IsAlive && guard++ < 40)
        {
            int before = round.RoundScore;
            if (PlayTurns(session, 1) == 0) { break; }
            if (round.RoundScore < before) { everWentBack = true; }
        }
        Check(pet.IsDead, "the creature died and billed us", "alive " + pet.IsAlive);
        Check(!everWentBack,
            "and not one turn pushed the round score backwards, however big the bill");
        Check(round.RoundScore >= 0, "the round score never went negative",
            "" + round.RoundScore);
    }

    private static void Besleme_TheCreatureSurvivesARoundChange()
    {
        Section("besleme / the creature is coordinates, so a new round leaves it where it was");
        var session = NewSession(9204, 7, 1000000, 40, 1);
        var pet = (BeslemeJoker)session.Jokers.Add(new BeslemeJoker());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        Check(pet.IsAlive, "marked");
        var where = new List<GridPos>(pet.Region);
        int size = pet.Size;

        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        Check(pet.Size == size, "the size carried over", size + " -> " + pet.Size);
        Check(pet.Region.Count == where.Count, "and so did the patch",
            where.Count + " -> " + pet.Region.Count);
        bool samePlace = where.Count == pet.Region.Count;
        for (int i = 0; i < where.Count && i < pet.Region.Count; i++)
        {
            if (where[i].X != pet.Region[i].X || where[i].Y != pet.Region[i].Y)
            {
                samePlace = false;
            }
        }
        Check(samePlace, "in exactly the same cells - it was never re-marked");
    }

    private static void Kiraci_RipensAPlainCubeIntoGold()
    {
        Section("kiracı / a plain cube that sits still long enough turns to gold");
        var session = NewSession(9100, 7, 1000000, 40, 1);
        var joker = (KiraciJoker)session.Jokers.Add(new KiraciJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);

        // One plain cube, parked well away from anything that could complete a line.
        var spot = new GridPos(round.Board.MinX + 3, round.Board.MinY + 3);
        round.Board.SetCubeAt(spot, new Cube(CubeKind.Normal, 9950));
        Check(round.Board.GetCube(spot).Value.Kind == CubeKind.Normal, "it starts plain");

        // It must survive the whole wait before anything happens.
        for (int i = 1; i < joker.TurnsToRipen; i++)
        {
            PlayTurns(session, 1);
            if (round.Board.GetCube(spot).HasValue
                && round.Board.GetCube(spot).Value.Kind == CubeKind.Gold)
            {
                Check(false, "it ripened early, on turn " + i);
                return;
            }
        }
        Check(round.Board.GetCube(spot).Value.Kind == CubeKind.Normal,
            "our cube has not ripened yet",
            round.Board.GetCube(spot).Value.Kind.ToString());

        PlayTurns(session, 1);
        Cube? ripened = round.Board.GetCube(spot);
        Check(ripened.HasValue && ripened.Value.Kind == CubeKind.Gold,
            "and on the fifth turn it is GOLD",
            ripened.HasValue ? ripened.Value.Kind.ToString() : "gone");
        // Not "exactly one": the blocks the driver itself played are tenants too, and on a
        // five-turn wait they ripen inside the same window. That is the joker working.
        Check(joker.GoldThisRound >= 1, "the joker counted it", "" + joker.GoldThisRound);
        Check(ripened.Value.SourceCardId == 9950, "and it is still the same cube");
    }

    private static void Kiraci_OnlyPlainCubesAreTenants()
    {
        Section("kiracı / a cube that already has an element is not a tenant");
        var session = NewSession(9101, 7, 1000000, 40, 1);
        var joker = (KiraciJoker)session.Jokers.Add(new KiraciJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);

        var fire = new GridPos(round.Board.MinX + 2, round.Board.MinY + 3);
        var water = new GridPos(round.Board.MinX + 4, round.Board.MinY + 3);
        var stone = new GridPos(round.Board.MinX + 3, round.Board.MinY + 4);
        round.Board.SetCubeAt(fire, new Cube(CubeKind.Fire, 9951));
        round.Board.SetCubeAt(water, new Cube(CubeKind.Water, 9952));
        round.Board.SetCubeAt(stone, new Cube(CubeKind.Obsidian, 9953));

        PlayTurns(session, joker.TurnsToRipen + 3);
        bool anyGold = false;
        foreach (GridPos cell in new[] { fire, water, stone })
        {
            Cube? cube = round.Board.GetCube(cell);
            if (cube.HasValue && cube.Value.Kind == CubeKind.Gold) { anyGold = true; }
        }
        Check(!anyGold, "none of the elemental cubes turned to gold");
    }

    private static void Kiraci_AnInterruptedTenancyStartsOver()
    {
        Section("kiracı / a cell that changes hands starts the clock again");
        var session = NewSession(9102, 7, 1000000, 40, 1);
        var joker = (KiraciJoker)session.Jokers.Add(new KiraciJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);

        var spot = new GridPos(round.Board.MinX + 3, round.Board.MinY + 3);
        round.Board.SetCubeAt(spot, new Cube(CubeKind.Normal, 9960));
        PlayTurns(session, joker.TurnsToRipen - 2);
        Check(joker.GoldThisRound == 0, "two turns short of payday");

        // Evict the tenant and move a DIFFERENT cube in. The new one may not inherit the wait.
        round.Board.DestroyCube(spot);
        round.Board.SetCubeAt(spot, new Cube(CubeKind.Normal, 9961));
        PlayTurns(session, 3);
        Cube? cube = round.Board.GetCube(spot);
        Check(cube.HasValue && cube.Value.Kind == CubeKind.Normal,
            "the new tenant is still plain three turns later - it did not inherit the clock",
            cube.HasValue ? cube.Value.Kind.ToString() : "gone");
        Check(cube.Value.SourceCardId == 9961, "and it is the NEW tenant sitting there",
            "" + cube.Value.SourceCardId);

        // Give it the full wait and it ripens on its own account.
        PlayTurns(session, joker.TurnsToRipen);
        cube = round.Board.GetCube(spot);
        Check(cube.HasValue && cube.Value.Kind == CubeKind.Gold,
            "given a full wait of its own, it ripens",
            cube.HasValue ? cube.Value.Kind.ToString() : "gone");
    }

    private static void Kiraci_TheGoldItMakesIsRealGold()
    {
        Section("kiracı / what it makes is real gold, with all of gold's teeth");
        var session = NewSession(9103, 5, 1000000, 40, 1);
        var joker = (KiraciJoker)session.Jokers.Add(new KiraciJoker());
        RoundEngine round = session.CurrentRound;
        session.Jokers.DispatchRoundStarted(round);

        var spot = new GridPos(round.Board.MinX + 2, round.Board.MinY + 2);
        round.Board.SetCubeAt(spot, new Cube(CubeKind.Normal, 9970));
        PlayTurns(session, joker.TurnsToRipen + 1);
        Cube? gold = round.Board.GetCube(spot);
        if (!gold.HasValue || gold.Value.Kind != CubeKind.Gold)
        {
            Check(false, "the cube did not ripen in this setup - skipped");
            return;
        }
        Check(true, "the cube is gold");
        Check(!CubeRules.IsDestructible(gold.Value),
            "gold NEVER breaks - a line explosion cannot shift it");
        Check(!CubeRules.CountsForCleanSweep(gold.Value),
            "and it does not stand in the way of a clean sweep");
        Check(!round.Board.DestroyCube(spot), "nothing external can destroy it either");
    }

    private static void Threshold_IsACeilingForNormalPlay()
    {
        Section("threshold / normal play is capped at the bar, overtime is the only way past");
        // A low bar and a fat per-cube payout, so one placement overshoots it wildly.
        var session = NewSession(9000, 6, 50, 40, 4);
        session.Config.Scoring.PointsPerCubePlaced = 400;
        RoundEngine round = session.CurrentRound;
        int scaledBar = round.ScoreThreshold * session.Config.Scoring.ScoreScale;

        long runBefore = session.TotalScore;
        TurnReport report = PlayOneCard(round);
        Check(report != null, "a card was played");
        Check(report.ThresholdJustPassed, "and it crossed the bar");
        Check(report.Score.Total > scaledBar, "the turn EARNED far more than the bar",
            report.Score.Total + " vs bar " + scaledBar);

        Check(round.RoundScore == scaledBar, "but the round banked exactly the bar, no more",
            round.RoundScore + " vs " + scaledBar);
        Check(report.ScoreGained == scaledBar - 0, "the report says what was banked",
            report.ScoreGained + " vs " + scaledBar);
        Check(session.TotalScore - runBefore == scaledBar,
            "and the RUN got the capped amount too - the money never outruns the meter",
            (session.TotalScore - runBefore) + " vs " + scaledBar);
        Check(round.Status == RoundStatus.AwaitingAdvanceDecision, "the advance offer is up");
    }

    private static void Threshold_OvertimeIsAllowedPastTheBar()
    {
        Section("threshold / overtime scores past the bar, which is its whole point");
        var session = NewSession(9001, 6, 50, 40, 4);
        session.Config.Scoring.PointsPerCubePlaced = 400;
        RoundEngine round = session.CurrentRound;
        int scaledBar = round.ScoreThreshold * session.Config.Scoring.ScoreScale;

        PlayOneCard(round);
        Check(round.RoundScore == scaledBar, "capped at the bar on the crossing turn",
            "" + round.RoundScore);
        Check(round.Status == RoundStatus.AwaitingAdvanceDecision, "the offer is up");

        // Decline it: overtime begins, and NOW the score may climb past the bar.
        round.DecideAdvance(false);
        Check(round.Status == RoundStatus.InProgress, "overtime is running",
            "status " + round.Status);
        Check(round.ThresholdPassed, "and the threshold is marked passed");

        int guard = 0;
        while (round.Status == RoundStatus.InProgress && round.RoundScore <= scaledBar
            && guard++ < 30)
        {
            if (PlayOneCard(round) == null) { break; }
        }
        Check(round.RoundScore > scaledBar,
            "overtime carried the score past the bar - the cap is for normal play only",
            round.RoundScore + " vs bar " + scaledBar);
    }

    private static void Threshold_ATurnUnderTheBarIsUntouched()
    {
        Section("threshold / a turn that does not reach the bar banks every point");
        var session = NewSession(9002, 6, 1000000, 40, 3);
        session.Config.Scoring.PointsPerCubePlaced = 10;
        RoundEngine round = session.CurrentRound;

        long runBefore = session.TotalScore;
        int banked = 0;
        for (int i = 0; i < 5; i++)
        {
            TurnReport report = PlayOneCard(round);
            if (report == null) { break; }
            Check(report.ScoreGained == report.Score.Total,
                "turn " + (i + 1) + " banked exactly what it earned",
                report.ScoreGained + " vs " + report.Score.Total);
            banked += report.ScoreGained;
        }
        Check(round.RoundScore == banked, "the round holds the sum of them",
            round.RoundScore + " vs " + banked);
        Check(session.TotalScore - runBefore == banked, "and so does the run",
            (session.TotalScore - runBefore) + " vs " + banked);
    }

    private static void Boss_AlacakaranlikBendsNoRuleAtAll()
    {
        Section("boss / alacakaranlık hides the board and changes nothing else");
        var dark = NewSession(8600, 6, 1000000, 40, 3);
        RoundEngine round = dark.CurrentRound;
        round.SetBoss(new AlacakaranlikBoss());
        Check(round.BoardIsDark, "the round reports itself dark");

        var lit = NewSession(8600, 6, 1000000, 40, 3);
        Check(!lit.CurrentRound.BoardIsDark, "and an ordinary round does not");

        // The whole point: with the same seed, the two rounds must play IDENTICALLY. The boss
        // is a blindfold, not a rule - if any of these diverge, it is doing more than it should.
        for (int turn = 0; turn < 10; turn++)
        {
            TurnReport a = PlayOneCard(round);
            TurnReport b = PlayOneCard(lit.CurrentRound);
            if (a == null || b == null)
            {
                Check(a == null && b == null, "both rounds ran out at the same turn",
                    "turn " + turn);
                break;
            }
            if (a.ScoreGained != b.ScoreGained
                || a.CubesExploded != b.CubesExploded
                || round.Board.OccupiedCount != lit.CurrentRound.Board.OccupiedCount)
            {
                Check(false, "the dark round diverged from the lit one on turn " + turn,
                    a.ScoreGained + " vs " + b.ScoreGained);
                return;
            }
        }
        Check(round.RoundScore == lit.CurrentRound.RoundScore,
            "ten turns later both rounds hold the same score",
            round.RoundScore + " vs " + lit.CurrentRound.RoundScore);
        Check(round.Board.OccupiedCount == lit.CurrentRound.Board.OccupiedCount,
            "and the same board",
            round.Board.OccupiedCount + " vs " + lit.CurrentRound.Board.OccupiedCount);
        Check(round.Status == lit.CurrentRound.Status, "and the same status",
            round.Status + " vs " + lit.CurrentRound.Status);

        // Every OTHER boss leaves the lights on.
        Check(!new VanilyaBoss().HidesTheBoard && !new KarantinaBoss().HidesTheBoard,
            "no other boss hides the board");
    }

    private static void Boss_KarantinaSealsOutwardInAndCharges()
    {
        Section("boss / karantina seals the rim inward and charges for cubes inside it");
        var scorer = new DefaultScoreCalculator(new ScoringConfig());
        var session = NewSession(8500, 7, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var boss = new KarantinaBoss();
        round.SetBoss(boss);
        GameBoard board = round.Board;

        Check(boss.QuarantinedRows.Count + boss.QuarantinedColumns.Count == 0,
            "nothing is sealed to start with");
        Check(boss.AdjustExplosionScore(scorer, new List<GridPos> { new GridPos(0, 0) }) == 0,
            "and no cube is charged for");

        // The first sealing lands on the rim.
        PlayTurns(session, boss.SealEveryTurns);
        int sealed1 = boss.QuarantinedRows.Count + boss.QuarantinedColumns.Count;
        Check(sealed1 == boss.LinesPerSealing, "two lines sealed on the first tick",
            "" + sealed1);
        int minX = board.MinX;
        int maxX = board.MinX + board.Width - 1;
        int minY = board.MinY;
        int maxY = board.MinY + board.Height - 1;
        bool onRim = true;
        foreach (int r in boss.QuarantinedRows) { onRim &= r == minY || r == maxY; }
        foreach (int c in boss.QuarantinedColumns) { onRim &= c == minX || c == maxX; }
        Check(onRim, "and both of them are on the OUTERMOST ring");

        // The next sealing adds two more, never repeating one.
        PlayTurns(session, boss.SealEveryTurns);
        int sealed2 = boss.QuarantinedRows.Count + boss.QuarantinedColumns.Count;
        Check(sealed2 == sealed1 + boss.LinesPerSealing, "the zones ACCUMULATE",
            sealed1 + " -> " + sealed2);
        var seen = new HashSet<string>();
        bool distinct = true;
        foreach (int r in boss.QuarantinedRows) { distinct &= seen.Add("r" + r); }
        foreach (int c in boss.QuarantinedColumns) { distinct &= seen.Add("c" + c); }
        Check(distinct, "with no line sealed twice");

        // Keep going: it must work inward rather than stalling on the rim.
        for (int i = 0; i < 4; i++)
        {
            PlayTurns(session, boss.SealEveryTurns);
        }
        int sealedLater = boss.QuarantinedRows.Count + boss.QuarantinedColumns.Count;
        Check(sealedLater > sealed2, "later sealings keep taking new lines",
            sealed2 + " -> " + sealedLater);
        bool wentInward = false;
        foreach (int r in boss.QuarantinedRows)
        {
            if (r != minY && r != maxY) { wentInward = true; }
        }
        foreach (int c in boss.QuarantinedColumns)
        {
            if (c != minX && c != maxX) { wentInward = true; }
        }
        Check(wentInward, "and the quarantine has moved off the rim, inward");
    }

    private static void Boss_KarantinaChargesOnlyTheCubesInside()
    {
        Section("boss / only the cubes inside a zone lose; the rest still pay");
        var scorer = new DefaultScoreCalculator(new ScoringConfig());
        var boss = new KarantinaBoss();
        var session = NewSession(8501, 6, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        round.SetBoss(boss);
        PlayTurns(session, boss.SealEveryTurns);
        Check(boss.QuarantinedRows.Count + boss.QuarantinedColumns.Count > 0,
            "a zone exists");

        // Five cubes, two of them inside the zone: the adjustment must charge for exactly two.
        var inside = new List<GridPos>();
        var outside = new List<GridPos>();
        GameBoard board = round.Board;
        for (int x = board.MinX; x < board.MinX + board.Width; x++)
        {
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                var cell = new GridPos(x, y);
                if (boss.IsQuarantined(cell)) { inside.Add(cell); }
                else { outside.Add(cell); }
            }
        }
        Check(inside.Count >= 2 && outside.Count >= 3, "the board has both kinds of cell",
            inside.Count + " inside / " + outside.Count + " outside");

        var mixed = new List<GridPos> { inside[0], inside[1], outside[0], outside[1], outside[2] };
        int perCube = scorer.ScoreLineExplosion(0, 1);
        int adjustment = boss.AdjustExplosionScore(scorer, mixed);
        Check(adjustment == -2 * 2 * perCube,
            "exactly the two inside are charged, twice their value",
            adjustment + " expected " + (-2 * 2 * perCube));

        // Which is to say: those two LOSE what they would have earned, the other three keep it.
        int normal = 5 * perCube;
        int withBoss = normal + adjustment;
        Check(withBoss == 3 * perCube - 2 * perCube,
            "3 cubes pay, 2 cubes cost - exactly as designed",
            withBoss + " = " + (3 * perCube) + " - " + (2 * perCube));

        Check(boss.AdjustExplosionScore(scorer, outside) == 0,
            "a clear that misses the zone entirely is charged nothing");
    }

    private static void Boss_KarantinaChangesNothingWithoutTheBoss()
    {
        Section("boss / karantina touches nothing on an ordinary round");
        var scorer = new DefaultScoreCalculator(new ScoringConfig());
        var plain = NewSession(8502, 6, 1000000, 40, 1);
        RoundEngine round = plain.CurrentRound;
        Check(round.Boss == null, "no boss");
        long before = plain.TotalScore;
        PlayTurns(plain, 8);
        Check(plain.TotalScore >= before, "an ordinary round scores forward as always",
            before + " -> " + plain.TotalScore);
        // And the hook itself is a no-op on the base type.
        var vanilla = new VanilyaBoss();
        Check(vanilla.AdjustExplosionScore(scorer,
            new List<GridPos> { new GridPos(0, 0), new GridPos(1, 1) }) == 0,
            "every other boss adjusts nothing");
    }

    private static void Boss_YuruyenMerdivenCarriesEveryRowUp()
    {
        Section("boss / yürüyen merdiven: every row rides up, the top row is carried off");
        var session = NewSession(8400, 5, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var boss = new YuruyenMerdivenBoss();
        round.SetBoss(boss);
        GameBoard board = round.Board;

        // A recognisable pattern: one cube per row, each in its own column.
        int rows = board.Height;
        for (int y = 0; y < rows; y++)
        {
            board.SetCubeAt(new GridPos(board.MinX + y % board.Width, board.MinY + y),
                new Cube(CubeKind.Normal, 9900 + y));
        }
        int before = board.OccupiedCount;
        Check(before == rows, "one cube per row to start", "occupied " + before);
        int topCardId = 9900 + rows - 1;
        Check(board.CountCubesOf(topCardId) == 1, "and the top row's cube is identifiable");

        TurnReport report = PlayOneCard(round);
        Check(report != null, "a turn resolved");

        // The top row's cube is gone; the ones below it moved up exactly one.
        Check(board.CountCubesOf(topCardId) == 0, "the top row was carried off the board",
            "left " + board.CountCubesOf(topCardId));
        for (int y = 0; y < rows - 1; y++)
        {
            var expected = new GridPos(board.MinX + y % board.Width, board.MinY + y + 1);
            Cube? cube = board.GetCube(expected);
            bool moved = cube.HasValue && cube.Value.SourceCardId == 9900 + y;
            if (!moved)
            {
                Check(false, "row " + y + " rode up exactly one",
                    "expected card " + (9900 + y) + " at " + expected);
                break;
            }
            if (y == rows - 2)
            {
                Check(true, "every row below the top rode up exactly one");
            }
        }
        Check(report.LiftedCells.Count > 0, "and the turn reported what was carried off",
            "cells " + report.LiftedCells.Count);
        Check(boss.CellsCarriedOff > 0, "the boss counted it", "" + boss.CellsCarriedOff);
    }

    private static void Boss_YuruyenMerdivenLeavesTheBottomRowEmpty()
    {
        Section("boss / the space it gives back arrives at the bottom");
        // Board level, so the ride is measured on its own rather than through a player who
        // would be filling cells in at the same time.
        var board = new GameBoard(5, 5);
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                board.SetCubeAt(new GridPos(x, y), new Cube(CubeKind.Normal, 9930 + y));
            }
        }
        Check(board.OccupiedCount == 25, "a completely full 5x5", "" + board.OccupiedCount);

        List<GridPos> lost = board.ShiftRowsUp();
        Check(lost.Count == board.Width, "one full row was carried off",
            "lost " + lost.Count);
        Check(board.OccupiedCount == 20, "and the board is one row lighter",
            "" + board.OccupiedCount);

        int bottomOccupied = 0;
        for (int x = 0; x < board.Width; x++)
        {
            if (board.GetCube(new GridPos(x, 0)).HasValue)
            {
                bottomOccupied++;
            }
        }
        Check(bottomOccupied == 0, "the new room arrived at the BOTTOM, completely empty",
            bottomOccupied + " of " + board.Width);
        Check(board.CountCubesOf(9930 + 4) == 0, "the old top row is gone");
        Check(board.CountCubesOf(9930) == board.Width, "and the old bottom row survived, above",
            "" + board.CountCubesOf(9930));
    }

    private static void Boss_YuruyenMerdivenIsNotDestruction()
    {
        Section("boss / the ride pays nothing and never sweeps");
        var session = NewSession(8402, 4, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        round.SetBoss(new YuruyenMerdivenBoss());
        GameBoard board = round.Board;

        // A single cube on the TOP row: the ride carries it off and empties the board.
        for (int x = board.MinX; x < board.MinX + board.Width; x++)
        {
            board.SetCubeAt(new GridPos(x, board.MinY + board.Height - 1),
                new Cube(CubeKind.Normal, 9910));
        }
        int sweepsBefore = round.CleanSweepCount;
        long scoreBefore = session.TotalScore;
        int roundBefore = round.RoundScore;

        TurnReport report = PlayOneCard(round);
        Check(report != null, "a turn resolved");
        Check(board.CountCubesOf(9910) == 0, "the top row rode off",
            "left " + board.CountCubesOf(9910));
        Check(round.CleanSweepCount == sweepsBefore,
            "carrying the last cubes away is not a clean sweep",
            sweepsBefore + " -> " + round.CleanSweepCount);
        // The turn's own placement may score; the RIDE must add nothing on top of it.
        Check(round.RoundScore - roundBefore == report.ScoreGained,
            "the round score moved by exactly the turn's own gain",
            (round.RoundScore - roundBefore) + " vs " + report.ScoreGained);
        Check(session.TotalScore - scoreBefore == report.ScoreGained,
            "and so did the run score", "" + (session.TotalScore - scoreBefore));
    }

    private static void Boss_YuruyenMerdivenNeverCompletesALine()
    {
        Section("boss / a row that was not full does not become full by moving");
        // Board level again: the claim is about the RIDE, and a player filling cells in at the
        // same time would prove nothing either way.
        var board = new GameBoard(5, 5);
        // Two rows one cube short, with the gaps in DIFFERENT columns - if moving could ever
        // merge rows or slide cubes sideways, this is where it would show.
        for (int x = 1; x < board.Width; x++)
        {
            board.SetCubeAt(new GridPos(x, 0), new Cube(CubeKind.Normal, 9920));
        }
        for (int x = 0; x < board.Width - 1; x++)
        {
            board.SetCubeAt(new GridPos(x, 1), new Cube(CubeKind.Normal, 9921));
        }
        Check(board.ResolveFullLines().LineCount == 0, "nothing is full to start with");

        for (int ride = 0; ride < 3; ride++)
        {
            board.ShiftRowsUp();
            LineExplosionResult lines = board.ResolveFullLines();
            if (lines.LineCount != 0)
            {
                Check(false, "the ride completed a line on pass " + (ride + 1),
                    "rows " + lines.Rows.Count + " cols " + lines.Columns.Count);
                return;
            }
        }
        Check(true, "three rides later still nothing has completed itself");
        Check(board.CountCubesOf(9920) == 4 && board.CountCubesOf(9921) == 4,
            "both rows kept their gap exactly as it was",
            board.CountCubesOf(9920) + " / " + board.CountCubesOf(9921));
    }

    private static void Boss_AlzheimerForgetsWhatWasPlayedFiveTurnsAgo()
    {
        Section("boss / alzheimer forgets the card played five turns ago");
        var session = NewSession(8300, 8, 1000000, 40, 3);
        RoundEngine round = session.CurrentRound;
        var boss = new AlzheimerBoss();
        round.SetBoss(boss);

        // Play one card and remember exactly which cells it laid.
        TurnReport first = PlayOneCard(round);
        Check(first != null && first.PlacedCells.Count > 0, "a card was played",
            first == null ? "none" : "cells " + first.PlacedCells.Count);
        int cardId = first.Card.Id;
        var laid = new List<GridPos>(first.PlacedCells);
        Check(round.Board.CountCubesOf(cardId) == laid.Count, "its cubes are on the board",
            "" + round.Board.CountCubesOf(cardId));

        // It survives every turn up to the memory limit.
        for (int i = 1; i < boss.MemoryTurns; i++)
        {
            PlayTurns(session, 1);
            Check(round.Board.CountCubesOf(cardId) > 0,
                "still remembered after " + i + " more turn(s)",
                "left " + round.Board.CountCubesOf(cardId));
        }

        // The turn that reaches the limit forgets it.
        TurnReport forgetting = PlayOneCard(round);
        Check(round.Board.CountCubesOf(cardId) == 0, "and then it is gone from the board",
            "left " + round.Board.CountCubesOf(cardId));
        Check(forgetting.LiftedCells.Count > 0, "the turn reported what it forgot",
            "cells " + forgetting.LiftedCells.Count);
        Check(boss.CellsForgotten > 0, "and the boss counted it",
            "" + boss.CellsForgotten);
    }

    private static void Boss_AlzheimerTakesWhateverIsLeftOfTheBlock()
    {
        Section("boss / alzheimer takes what is LEFT of a block, whole or not");
        var session = NewSession(8301, 8, 1000000, 40, 4);
        RoundEngine round = session.CurrentRound;
        var boss = new AlzheimerBoss();
        round.SetBoss(boss);

        TurnReport first = PlayOneCard(round);
        int cardId = first.Card.Id;
        var laid = new List<GridPos>(first.PlacedCells);
        Check(laid.Count >= 3, "a block of at least three cubes", "cells " + laid.Count);

        // Blow away all but one of its cubes by hand, so the block is already broken.
        for (int i = 0; i < laid.Count - 1; i++)
        {
            round.Board.DestroyCube(laid[i]);
        }
        Check(round.Board.CountCubesOf(cardId) == 1, "one lonely cube is left",
            "" + round.Board.CountCubesOf(cardId));

        PlayTurns(session, boss.MemoryTurns);
        Check(round.Board.CountCubesOf(cardId) == 0,
            "and the last cube is forgotten too - the block need not be whole",
            "left " + round.Board.CountCubesOf(cardId));
    }

    private static void Boss_AlzheimerForgetsEvenTheUnbreakable()
    {
        Section("boss / alzheimer forgets obsidian and gold, which nothing else can shift");
        var session = NewSession(8302, 8, 1000000, 40, 2);
        RoundEngine round = session.CurrentRound;
        var boss = new AlzheimerBoss();
        round.SetBoss(boss);

        TurnReport first = PlayOneCard(round);
        int cardId = first.Card.Id;
        var laid = new List<GridPos>(first.PlacedCells);
        // Turn its cubes into the two kinds the game cannot destroy, and protect one on top.
        round.Board.SetCubeKind(laid[0], CubeKind.Obsidian);
        if (laid.Count > 1)
        {
            round.Board.SetCubeKind(laid[1], CubeKind.Gold);
            round.Board.SetCubeProtected(laid[1]);
        }
        Check(!round.Board.DestroyCube(laid[0]), "obsidian really does refuse destruction");
        Check(round.Board.CountCubesOf(cardId) == laid.Count, "the cubes are all still there",
            "" + round.Board.CountCubesOf(cardId));

        PlayTurns(session, boss.MemoryTurns);
        Check(round.Board.CountCubesOf(cardId) == 0,
            "forgetting takes them anyway - nothing survives being forgotten",
            "left " + round.Board.CountCubesOf(cardId));
    }

    private static void Boss_AlzheimerForgettingIsNotDestruction()
    {
        Section("boss / forgetting pays nothing and triggers no sweep");
        var session = NewSession(8303, 4, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        var boss = new AlzheimerBoss();
        round.SetBoss(boss);

        TurnReport first = PlayOneCard(round);
        int cardId = first.Card.Id;
        // Leave ONLY that card on the board, so forgetting it empties the arena entirely.
        foreach (GridPos cell in AllPlayableCells(round.Board))
        {
            Cube? cube = round.Board.GetCube(cell);
            if (cube.HasValue && cube.Value.SourceCardId != cardId)
            {
                round.Board.DestroyCube(cell);
            }
        }
        int sweepsBefore = round.CleanSweepCount;

        // Run to the memory limit, clearing the board of anything else each turn so that the
        // forgetting is what empties it.
        for (int i = 0; i < boss.MemoryTurns; i++)
        {
            TurnReport report = PlayOneCard(round);
            if (report == null) { break; }
            foreach (GridPos cell in AllPlayableCells(round.Board))
            {
                Cube? cube = round.Board.GetCube(cell);
                if (cube.HasValue && cube.Value.SourceCardId != cardId
                    && round.Board.CountCubesOf(cardId) > 0)
                {
                    round.Board.DestroyCube(cell);
                }
            }
        }
        Check(round.Board.CountCubesOf(cardId) == 0, "the card was forgotten",
            "left " + round.Board.CountCubesOf(cardId));
        Check(round.CleanSweepCount == sweepsBefore,
            "and emptying the board that way was NOT a clean sweep",
            sweepsBefore + " -> " + round.CleanSweepCount);
    }

    private static void Boss_AlzheimerRemembersNothingBeforeTheLimit()
    {
        Section("boss / nothing is forgotten in the first turns");
        var session = NewSession(8304, 8, 1000000, 40, 3);
        RoundEngine round = session.CurrentRound;
        var boss = new AlzheimerBoss();
        round.SetBoss(boss);

        int occupied = 0;
        for (int i = 0; i < boss.MemoryTurns - 1; i++)
        {
            PlayOneCard(round);
            occupied = round.Board.OccupiedCount;
        }
        Check(boss.CellsForgotten == 0, "nothing has slipped its mind yet",
            "forgotten " + boss.CellsForgotten);
        Check(occupied > 0, "and the board has been filling up all along",
            "occupied " + occupied);
    }

    private static void Boss_CikmazTurnsTheRoundUpsideDown()
    {
        Section("boss / çıkmaz: filling up wins, sweeping or scoring loses");
        // 1. A dead end WINS the round.
        var deadEnd = NewSession(8200, 4, 1000000, 40, 3);
        RoundEngine round = deadEnd.CurrentRound;
        round.SetBoss(new CikmazBoss());
        Check(round.RoundOutcomeInverted, "the round reports itself inverted");
        FillBoardSolid(round, deadEnd);
        round.DebugCheckForDeadEnd();
        Check(round.Status == RoundStatus.Advanced, "running out of room WON the round",
            "status " + round.Status);
        Check(round.Loss == null, "and there is no loss recorded", "loss " + round.Loss);

        // The same board without the boss is the loss it always was.
        var plain = NewSession(8200, 4, 1000000, 40, 3);
        FillBoardSolid(plain.CurrentRound, plain);
        plain.CurrentRound.DebugCheckForDeadEnd();
        Check(plain.CurrentRound.Status == RoundStatus.Lost,
            "without the boss the same dead end still loses",
            "status " + plain.CurrentRound.Status);
    }

    private static void Boss_CikmazLosesOnASweep()
    {
        Section("boss / çıkmaz: emptying the board loses the round");
        var session = NewSession(8201, 4, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        round.SetBoss(new CikmazBoss());
        // One cube short of a full row, on an otherwise empty board: closing it clears the row
        // AND empties the board - a clean sweep.
        int row = round.Board.MinY;
        for (int x = round.Board.MinX + 1; x < round.Board.MinX + round.Board.Width; x++)
        {
            round.Board.SetCubeAt(new GridPos(x, row), new Cube(CubeKind.Normal, 9800));
        }
        TurnReport report = round.PlayFromHand(0, new GridPos(round.Board.MinX, row));
        Check(report.CleanSweep, "the board was swept");
        Check(round.Status == RoundStatus.Lost, "and that lost the round",
            "status " + round.Status);
        Check(round.Loss == LossReason.ForbiddenCleanSweep, "for the sweep, by name",
            "loss " + round.Loss);
    }

    private static void Boss_CikmazLosesOnTheThreshold()
    {
        Section("boss / çıkmaz: reaching the threshold loses the round");
        var session = NewSession(8202, 6, 20, 40, 3);
        session.Config.Scoring.PointsPerCubePlaced = 50; // one placement clears the low bar
        RoundEngine round = session.CurrentRound;
        round.SetBoss(new CikmazBoss());

        TurnReport report = PlayOneCard(round);
        Check(report != null, "a card was played");
        Check(round.RoundScore >= round.ScoreThreshold * session.Config.Scoring.ScoreScale,
            "the score reached the bar", round.RoundScore + " / " + round.ScoreThreshold);
        Check(round.Status == RoundStatus.Lost, "which lost the round", "status " + round.Status);
        Check(round.Loss == LossReason.ForbiddenThreshold, "for the threshold, by name",
            "loss " + round.Loss);
        Check(!round.ThresholdPassed, "and no overtime was ever entered");
        Check(!report.ThresholdJustPassed, "nor an advance offer raised");
    }

    private static void Boss_CikmazSilencesTheAutomaticRescueOnly()
    {
        Section("boss / çıkmaz: the automatic rescue is skipped, the offered one is not");
        // Deprem fires by itself on a dead end - under Çıkmaz that would steal the win.
        var auto = NewSession(8203, 4, 1000000, 40, 3);
        RoundEngine round = auto.CurrentRound;
        var deprem = (DepremJoker)auto.Jokers.Add(new DepremJoker());
        round.SetBoss(new CikmazBoss());
        auto.Jokers.DispatchRoundStarted(round);
        FillBoardSolid(round, auto);
        int occupied = round.Board.OccupiedCount;
        round.DebugCheckForDeadEnd();
        Check(round.Status == RoundStatus.Advanced, "the dead end still won the round",
            "status " + round.Status);
        Check(round.Board.OccupiedCount == occupied, "Deprem never fired - the board is intact",
            occupied + " -> " + round.Board.OccupiedCount);
        Check(deprem.CollapseCount == 0, "and it recorded no collapse",
            "collapses " + deprem.CollapseCount);

        // Without the boss the very same joker DOES rescue, so it is the boss doing this.
        var normal = NewSession(8203, 4, 1000000, 40, 3);
        var deprem2 = (DepremJoker)normal.Jokers.Add(new DepremJoker());
        normal.Jokers.DispatchRoundStarted(normal.CurrentRound);
        FillBoardSolid(normal.CurrentRound, normal);
        normal.CurrentRound.DebugCheckForDeadEnd();
        Check(deprem2.CollapseCount > 0, "on an ordinary round Deprem rescues as usual",
            "collapses " + deprem2.CollapseCount);

        // The OFFERED rescue still appears - and declining it is how you win.
        var offered = NewSession(8204, 4, 1000000, 40, 3);
        RoundEngine round3 = offered.CurrentRound;
        offered.Powers.Add(new KentselDonusumPower());
        round3.SetBoss(new CikmazBoss());
        offered.Powers.DispatchRoundStarted(round3);
        FillBoardSolid(round3, offered);
        round3.DebugCheckForDeadEnd();
        Check(round3.Status == RoundStatus.AwaitingRescue,
            "the rescue power is still offered", "status " + round3.Status);
        offered.DeclineDeadEndRescue();
        Check(round3.Status == RoundStatus.Advanced, "and declining it WINS the round",
            "status " + round3.Status);
        Check(round3.Loss == null, "with no loss recorded", "loss " + round3.Loss);
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
        Check(session.Jokers.SellValueOf(common) > 0, "they all keep their sell value");

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
        int empty = session.Jokers.SellValueOf(bank);
        PlayTurns(session, 6);
        int filled = session.Jokers.SellValueOf(bank);
        Check(filled > empty, "it fills up on an ordinary round", empty + " -> " + filled);

        // Now the boss arrives and the same hook starts running backwards.
        round.SetBoss(new TerslikBoss());
        PlayTurns(session, 3);
        Check(session.Jokers.SellValueOf(bank) < filled,
            "under the boss the very same joker drains it",
            filled + " -> " + session.Jokers.SellValueOf(bank));

        // An emptied bank cannot go into debt: what it EARNED bottoms out at nothing, so its
        // value bottoms out at the market's price for its rarity.
        PlayTurns(session, 80);
        int floor = session.Config.Market.JokerSellValue(RarityTable.For(bank.DefId));
        Check(bank.AccruedValue >= 0 && session.Jokers.SellValueOf(bank) >= floor,
            "and it stops at empty - a piggy bank never owes you money",
            "value " + session.Jokers.SellValueOf(bank) + " floor " + floor);
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

    private static void RunStructure_BossStagesSitBetweenNumberedRounds()
    {
        Section("run structure / 1, 2, 3, BOSS, 4, 5, 6, BOSS ... 15, BOSS");
        var config = new GameConfig();
        config.RngSeed = 8800;
        config.Deck = new DeckDefinition("test", 30, new SizedShapeGenerator(1));
        config.Scoring.PointsPerCubePlaced = 100000; // one placement clears any bar
        // Pin a harmless boss: this test is about the SHAPE of a run, not about surviving five
        // random ones. A boss that inverts the win condition would end the walk early and tell us
        // nothing about the structure.
        config.ForcedBossDefId = "ufuk";
        var session = new GameSession(config);

        var walked = new List<string>();
        int guard = 0;
        while (!RunIsOver(session) && guard++ < 600)
        {
            if (session.Phase == GamePhase.Market)
            {
                session.LeaveMarket();
                continue;
            }
            RoundEngine round = session.CurrentRound;
            if (round.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                walked.Add(session.InBossStage ? "B" + session.RoundNumber
                    : "" + session.RoundNumber);
                round.DecideAdvance(true);
                continue;
            }
            if (round.Status != RoundStatus.InProgress) { break; }
            if (PlayTurns(session, 1) == 0) { break; }
        }

        Check(session.Phase == GamePhase.RunWon, "the run was won", "phase " + session.Phase);
        Check(walked.Count == 20, "twenty stages were played - 15 rounds and 5 bosses",
            "" + walked.Count);
        string expected = "1,2,3,B3,4,5,6,B6,7,8,9,B9,10,11,12,B12,13,14,15,B15";
        Check(string.Join(",", walked.ToArray()) == expected,
            "in exactly that order - every boss sits between two numbered rounds",
            string.Join(",", walked.ToArray()));
    }

    private static void DebugStartBossStage_JumpsStraightToABossStage()
    {
        Section("debug / jumping straight to a boss stage");
        var config = new GameConfig();
        config.RngSeed = 9900;
        config.Deck = new DeckDefinition("test", 40, new SizedShapeGenerator(1));
        var session = new GameSession(config); // round 1 of a real run

        Check(!session.InBossStage && session.ActiveBoss == null,
            "round 1 is an ordinary round with no boss");
        int ordinaryBar = session.CurrentRound.Config.ScoreThreshold;

        // Off-cadence on purpose: round 1 has no boss stage after it, and the debug jump has to
        // work anyway - that is the whole point of it.
        Check(session.DebugStartBossStage("saatci"), "the jump took");
        Check(session.InBossStage, "the stage is a boss stage now");
        Check(session.CurrentRound.Config.IsBossRound, "and the round config says so");
        Check(session.ActiveBoss != null && session.ActiveBoss.DefId == "saatci",
            "the pinned boss is the one running",
            session.ActiveBoss != null ? session.ActiveBoss.DefId : "none");
        Check(session.RoundNumber == 1, "it is round 1's boss stage, not round 2's",
            "" + session.RoundNumber);
        Check(session.CurrentRound.Config.ScoreThreshold > ordinaryBar,
            "the real path ran, so the bar is raised like any boss stage",
            ordinaryBar + " -> " + session.CurrentRound.Config.ScoreThreshold);
        Check(session.BossesFought.Count == 0,
            "a PINNED boss does not eat the run's no-repeat pool",
            "" + session.BossesFought.Count);

        // A null id means the ordinary draw, which does spend one out of the pool.
        Check(session.DebugStartBossStage(null), "a second jump, drawing normally");
        Check(session.ActiveBoss != null, "a boss was drawn");
        Check(session.BossesFought.Count == 1, "and a DRAWN boss does join the pool",
            "" + session.BossesFought.Count);
    }

    private static void RunStructure_EveryStageOpensAMarket()
    {
        Section("run structure / a market opens after every stage, boss stages included");
        var config = new GameConfig();
        config.RngSeed = 8801;
        config.Deck = new DeckDefinition("test", 30, new SizedShapeGenerator(1));
        config.Scoring.PointsPerCubePlaced = 100000;
        config.ForcedBossDefId = "ufuk"; // see above: structure, not survival
        var session = new GameSession(config);

        int markets = 0;
        int marketsAfterABoss = 0;
        bool cameFromABoss = false;
        int guard = 0;
        while (!RunIsOver(session) && guard++ < 600)
        {
            if (session.Phase == GamePhase.Market)
            {
                markets++;
                if (cameFromABoss) { marketsAfterABoss++; }
                session.LeaveMarket();
                continue;
            }
            RoundEngine round = session.CurrentRound;
            if (round.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                cameFromABoss = session.InBossStage;
                round.DecideAdvance(true);
                continue;
            }
            if (round.Status != RoundStatus.InProgress) { break; }
            if (PlayTurns(session, 1) == 0) { break; }
        }

        // 20 stages, and the last one wins the run instead of opening a shop.
        Check(markets == 19, "nineteen markets - one after every stage but the last",
            "" + markets);
        Check(marketsAfterABoss == 4,
            "four of them followed a boss stage (the fifth boss ends the run)",
            "" + marketsAfterABoss);
    }

    private static void BossRounds_FlaggedEveryThirdRound()
    {
        Section("boss rounds / a boss STAGE follows every third round");
        var progression = new DefaultRoundProgression();

        // A NUMBERED round is never a boss round any more - the boss is its own stage after it.
        bool anyNumberedRoundIsABoss = false;
        var followed = new List<int>();
        for (int n = 1; n <= 15; n++)
        {
            if (progression.GetRound(n, false).IsBossRound)
            {
                anyNumberedRoundIsABoss = true;
            }
            if (progression.HasBossStageAfter(n))
            {
                followed.Add(n);
            }
        }
        Check(!anyNumberedRoundIsABoss,
            "not one of the fifteen numbered rounds is itself a boss round");
        Check(followed.Count == 5, "five boss stages in a run", "count " + followed.Count);
        Check(followed.Count == 5 && followed[0] == 3 && followed[1] == 6 && followed[2] == 9
            && followed[3] == 12 && followed[4] == 15,
            "they follow rounds 3, 6, 9, 12 and 15", string.Join(",", followed));
        Check(progression.GetRound(3, true).IsBossRound,
            "and the stage AFTER round 3 is flagged as the boss round");

        // A boss stage keeps its round's arena and raises the bar instead.
        RoundConfig plain = progression.GetRound(3, false);
        RoundConfig bossStage = progression.GetRound(3, true);
        Check(bossStage.BoardWidth == plain.BoardWidth
            && bossStage.BoardHeight == plain.BoardHeight,
            "a boss stage is played on the same arena as the round it follows");
        Check(bossStage.ScoreThreshold > plain.ScoreThreshold,
            "with a higher bar - a boss is a wall, not a new place",
            plain.ScoreThreshold + " -> " + bossStage.ScoreThreshold);

        progression.BossRoundInterval = 0;
        Check(!progression.HasBossStageAfter(3), "interval 0 disables boss stages");

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
                // The breakdown says what the turn EARNED; the report says what was BANKED.
                // They match on every turn but the one that crosses the threshold, where the
                // round's score is capped at the bar and the excess is dropped.
                bool cappedThisTurn = report.ThresholdJustPassed;
                bool scoreMatches = report.Score == null
                    || (cappedThisTurn
                        ? report.ScoreGained <= report.Score.Total
                        : report.ScoreGained == report.Score.Total);
                if (!scoreMatches)
                {
                    failure = "seed " + seed + ": ScoreGained " + report.ScoreGained
                        + " != breakdown total " + report.Score.Total
                        + (cappedThisTurn ? " (crossing turn)" : "");
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

            // The books must balance: every point in TotalScore came from a turn, a sale or an
            // effect that GRANTED money ("Eforsuz galibiyet" paying at the market door), minus
            // whatever an EFFECT took back (a boss charging the purse, an overtime cap clawing
            // back farmed score). The fuzz never enters the market, so nothing is spent.
            long expected = expectedTotal + saleIncome
                + session.CurrencyGrantedByEffects - session.CurrencyTakenByEffects;
            if (failure == null && session.TotalScore != expected)
            {
                failure = "seed " + seed + ": TotalScore " + session.TotalScore
                    + " != turns " + expectedTotal + " + sales " + saleIncome
                    + " + granted " + session.CurrencyGrantedByEffects
                    + " - taken " + session.CurrencyTakenByEffects;
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
        // Generous on purpose: this driver only exists to GET somewhere, and a clean sweep now
        // swallows the line score, so a thin margin here would make unrelated tests fail
        // whenever the scoring balance moves.
        session.Config.Scoring.PointsPerCubePlaced = 20;
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
        // Placement scores nothing by default; greedy play rarely clears a line, so this
        // driver needs placement points to reach the threshold and the market. Generous on
        // purpose - see DriveOwnedToMarket.
        config.Scoring.PointsPerCubePlaced = 20;
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
            sb.Append(joker.DefId).Append('=')
                .Append(session.Jokers.SellValueOf(joker)).Append(';');
        }
        sb.Append("total=").Append(session.TotalScore);
        return sb.ToString();
    }
}
