// PURPOSE: SNOW and "Çığ" (2026-10-02). The snow block falls like water and melts after five
// turns; snow that lands on snow is absorbed by the heap under it, which gains power and takes
// the melt time the designer's table gives; the "Çığ" power sends a row's heaps down as many rows
// as they have power, crushing what is under them (gold and obsidian too) for points and leaving
// packed layers that melt in three turns, never merge with each other and cannot slide again
// until fresh snow lands on them. The market sells snow as one-row bars only, and the "Çığ"
// joker is gone.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;

public static partial class JokerTests
{
    private static void RunSnowTests()
    {
        Snow_MeltTableIsTheDesigners();
        Snow_FallsLikeWaterAndMeltsAfterFiveTurns();
        Snow_LandingOnSnowIsAbsorbedAndTheHeapGainsPower();
        Snow_APartialLandingFeedsTheWholeHeapAndTheRestJoinsIt();
        Snow_AbsorbedMeltTimeFollowsTheTable();
        Snow_FollowsTheArenasGravity();
        Cig_SendsAHeapDownItsPowerAndPaysForWhatItCrushes();
        Cig_PackedLayersStayApartUntilFreshSnowLands();
        Cig_StopsAtTheEdgeAndRefusesALineThatCannotSlide();
        Cig_IsAPowerNowAndTheJokerIsGone();
        Snow_TheMarketSellsOneRowBarsOnly();
        Snow_SurvivesASaveWithItsNumbers();
        Strata_LaterSnowFeedsTheTopLayerAndTheSeamHolds();
        Strata_AnotherAvalanchesLayerMergesAsUsual();
        Snow_TheMergeIsReportedWithItsBeforeAndAfter();
        Cig_ThePlanSaysWhyALineIsRefused();
    }

    private static int snowCardId = 9300;

    /// <summary>Plays a block of <paramref name="length"/> cubes in a row from the bonus hand at
    /// <paramref name="origin"/> - a real turn. Snow when asked, plain otherwise.</summary>
    private static TurnReport PlayBar(RoundEngine round, int length, bool snow, GridPos origin)
    {
        var elements = snow ? new List<BlockElement> { BlockElement.Snow } : null;
        var card = new BlockCard(snowCardId++, Bar(length), elements);
        round.AddBonusCard(card, BonusPlayOutcome.ExpireFromRound);
        return round.PlayFromBonus(round.BonusHand.Count - 1, origin);
    }

    private static Cube? SnowAtCell(GameBoard board, int x, int y)
    {
        Cube? cube = board.GetCube(new GridPos(x, y));
        return cube.HasValue && cube.Value.Kind == CubeKind.Snow ? cube : null;
    }

    private static bool IsSnowWith(GameBoard board, int x, int y, int power, int melt)
    {
        Cube? snow = SnowAtCell(board, x, y);
        return snow.HasValue && snow.Value.SnowPower == power && snow.Value.SnowMelt == melt;
    }

    private static string SnowText(GameBoard board, int x, int y)
    {
        Cube? cube = board.GetCube(new GridPos(x, y));
        if (!cube.HasValue)
        {
            return "empty";
        }
        return cube.Value.Kind + " p" + cube.Value.SnowPower + " m" + cube.Value.SnowMelt
            + (cube.Value.SnowPacked ? " packed" : "");
    }

    private static int CountSnow(GameBoard board)
    {
        return board.CountCubesOfKind(CubeKind.Snow);
    }

    private static void Snow_MeltTableIsTheDesigners()
    {
        Section("kar / the melt time of a heap that absorbed another");
        Check(SnowRules.MergedMelt(3, 5) == 5, "three left, five arrives: five");
        Check(SnowRules.MergedMelt(3, 4) == 4, "four arrives: four");
        Check(SnowRules.MergedMelt(3, 3) == 4, "three arrives: FOUR");
        Check(SnowRules.MergedMelt(3, 2) == 3, "two arrives: still three");
        Check(SnowRules.MergedMelt(3, 1) == 3, "one arrives: still three");
        Check(SnowRules.MergedMelt(5, 5) == 5, "and never past the full melt time");
    }

    private static void Snow_FallsLikeWaterAndMeltsAfterFiveTurns()
    {
        Section("kar / falls like water, melts five turns after it was placed");
        var session = NewSession(9801, 7, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;

        PlayBar(round, 1, true, new GridPos(3, 5));
        Check(IsSnowWith(board, 3, 0, 1, 5), "a snow cube dropped to the floor with one power and five turns",
            SnowText(board, 3, 0));
        Check(board.GetCube(new GridPos(3, 5)) == null, "and left the cell it was placed in");

        TurnReport last = null;
        for (int turn = 1; turn <= 4; turn++)
        {
            last = PlayBar(round, 1, false, new GridPos(6, turn));
            Check(IsSnowWith(board, 3, 0, 1, 5 - turn), "turn " + turn + ": " + (5 - turn) + " left",
                SnowText(board, 3, 0));
        }
        Check(last.SnowMelted.Count == 0, "nothing has melted yet");
        int scoreBefore = round.RoundScore;
        last = PlayBar(round, 1, false, new GridPos(6, 5));
        Check(board.GetCube(new GridPos(3, 0)) == null, "the fifth turn after it melts it",
            SnowText(board, 3, 0));
        Check(last.SnowMelted.Count == 1 && last.SnowMelted[0].Pos.Equals(new GridPos(3, 0)),
            "and the turn reports where it melted");
        Check(last.DestroyedCubes.Count == 0, "melting is not a destruction");
        Check(round.CleanSweepCount == 0, "and never a sweep");
    }

    private static void Snow_LandingOnSnowIsAbsorbedAndTheHeapGainsPower()
    {
        Section("kar / snow landing on snow is absorbed; the heap under it gains power");
        var session = NewSession(9802, 7, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;

        PlayBar(round, 3, true, new GridPos(1, 0));
        Check(CountSnow(board) == 3 && IsSnowWith(board, 1, 0, 1, 5) && IsSnowWith(board, 3, 0, 1, 5),
            "a 1x3 bar is a heap of three with one power");

        PlayBar(round, 3, true, new GridPos(1, 4));
        Check(CountSnow(board) == 3, "a second bar dropped on it is absorbed - still three cubes",
            "" + CountSnow(board));
        Check(IsSnowWith(board, 1, 0, 2, 5) && IsSnowWith(board, 2, 0, 2, 5) && IsSnowWith(board, 3, 0, 2, 5),
            "and the whole heap has two power and the fresh bar's five turns",
            SnowText(board, 1, 0) + " | " + SnowText(board, 2, 0) + " | " + SnowText(board, 3, 0));
        Check(board.OccupiedCount == 3, "the board's own count agrees", "" + board.OccupiedCount);

        PlayBar(round, 3, true, new GridPos(1, 1));
        Check(CountSnow(board) == 3 && IsSnowWith(board, 2, 0, 3, 5),
            "a third, laid straight on top, is absorbed too: three power", SnowText(board, 2, 0));
    }

    private static void Snow_APartialLandingFeedsTheWholeHeapAndTheRestJoinsIt()
    {
        Section("kar / a bar that half lands on a heap feeds it once; the rest falls and joins it");
        var session = NewSession(9803, 7, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;

        PlayBar(round, 3, true, new GridPos(1, 0));   // heap on x 1..3
        PlayBar(round, 2, true, new GridPos(3, 3));   // x 3 lands on it, x 4 falls beside it
        Check(CountSnow(board) == 4, "three cubes of heap plus the one that fell beside it",
            "" + CountSnow(board));
        Check(IsSnowWith(board, 1, 0, 2, 5) && IsSnowWith(board, 3, 0, 2, 5),
            "the heap gained the bar's power ONCE", SnowText(board, 1, 0));
        Check(IsSnowWith(board, 4, 0, 2, 5),
            "and the cube beside it is part of the same heap now - one power for all of it",
            SnowText(board, 4, 0));
        Check(board.SnowHeapAt(new GridPos(2, 0)).Count == 4, "one heap, four wide");

        // A heap split by a gap is two heaps: only the one that was landed on gains.
        var other = NewSession(9804, 7, 1000000, 40, 1);
        RoundEngine r2 = other.CurrentRound;
        PlayBar(r2, 1, true, new GridPos(0, 0));
        PlayBar(r2, 1, true, new GridPos(2, 0));
        PlayBar(r2, 1, true, new GridPos(0, 3));
        Check(IsSnowWith(r2.Board, 0, 0, 2, 5), "the heap that was landed on has two power",
            SnowText(r2.Board, 0, 0));
        Check(SnowAtCell(r2.Board, 2, 0).HasValue && SnowAtCell(r2.Board, 2, 0).Value.SnowPower == 1,
            "the one across the gap still has one", SnowText(r2.Board, 2, 0));
    }

    private static void Snow_AbsorbedMeltTimeFollowsTheTable()
    {
        Section("kar / the merged heap's melt time, on the board");
        int[] arriving = { 5, 4, 3, 2 };
        int[] expected = { 5, 4, 4, 3 };
        for (int i = 0; i < arriving.Length; i++)
        {
            var board = new GameBoard(5, 5);
            board.SetCubeAt(new GridPos(2, 0), new Cube(CubeKind.Snow, 1, false, 1, 3, 0));
            board.SetCubeAt(new GridPos(2, 2), new Cube(CubeKind.Snow, 2, false, 1, arriving[i], 0));
            board.SettleWaterAndReact();
            Check(IsSnowWith(board, 2, 0, 2, expected[i]),
                "three left, " + arriving[i] + " arrives: " + expected[i], SnowText(board, 2, 0));
            Check(board.OccupiedCount == 1, "one cube left on the board");
        }
    }

    private static void Snow_FollowsTheArenasGravity()
    {
        Section("kar / with gravity turned, snow falls that way and a heap is a COLUMN");
        var board = new GameBoard(5, 5);
        board.SetWaterFlow(new GridPos(-1, 0));
        board.SetCubeAt(new GridPos(3, 1), Cube.FreshSnow(1));
        board.SetCubeAt(new GridPos(3, 2), Cube.FreshSnow(1));
        board.SettleWaterAndReact();
        Check(IsSnowWith(board, 0, 1, 1, 5) && IsSnowWith(board, 0, 2, 1, 5),
            "both slid to the left wall and stand side by side across the flow, one heap",
            SnowText(board, 0, 1) + " | " + SnowText(board, 0, 2));
        board.SetCubeAt(new GridPos(4, 2), Cube.FreshSnow(2));
        board.SettleWaterAndReact();
        Check(CountSnow(board) == 2 && IsSnowWith(board, 0, 1, 2, 5) && IsSnowWith(board, 0, 2, 2, 5),
            "a cube arriving along the flow is absorbed and the whole column-heap gains",
            SnowText(board, 0, 1) + " | " + SnowText(board, 0, 2));
        AvalanchePlan plan = board.PlanAvalanche(new GridPos(0, 2));
        Check(!plan.Any, "against the wall there is nowhere to slide");
    }

    /// <summary>A 7x7 round with a 1x3 heap of the given power on row 4 (x 1..3) standing on three
    /// rows of plain cubes, one of them gold; row 0 under them is empty.</summary>
    private static GameSession AvalancheSetup(int seed, int power, out CigPower cig)
    {
        var session = NewSession(seed, 7, 1000000, 40, 1);
        cig = (CigPower)session.Powers.Add(new CigPower());
        GameBoard board = session.CurrentRound.Board;
        for (int x = 1; x <= 3; x++)
        {
            for (int y = 1; y <= 3; y++)
            {
                board.SetCubeAt(new GridPos(x, y),
                    new Cube(x == 2 && y == 2 ? CubeKind.Gold : CubeKind.Normal, 100 + x * 10 + y));
            }
            board.SetCubeAt(new GridPos(x, 4), new Cube(CubeKind.Snow, 500, false, power, 5, 0));
        }
        session.CurrentRound.NoteBoardRearranged();
        return session;
    }

    private static void Cig_SendsAHeapDownItsPowerAndPaysForWhatItCrushes()
    {
        Section("çığ / a heap comes down as many rows as it has power and pays for what it crushes");
        CigPower cig;
        GameSession session = AvalancheSetup(9810, 3, out cig);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;
        int scale = session.Config.Scoring.ScoreScale;

        AvalanchePlan plan = round.PlanAvalanche(new GridPos(5, 4));
        Check(plan.Columns.Count == 3 && plan.CoveredCells().Count == 9 && plan.CrushedCells().Count == 9,
            "any cell of the row aims it: three columns, three rows each, nine cubes in the way",
            plan.Columns.Count + " / " + plan.CoveredCells().Count + " / " + plan.CrushedCells().Count);

        int before = round.RoundScore;
        round.BeginExternalCapture();
        Check(session.Powers.TryUse(cig.InstanceId, ActivationTarget.Board(new GridPos(2, 4))),
            "the power runs on the heap's row");
        Check(round.RoundScore - before == 9 * cig.PointsPerCrushedCube * scale,
            "nine crushed cubes paid, the gold one included",
            (round.RoundScore - before) + " vs " + 9 * cig.PointsPerCrushedCube * scale);
        Check(cig.LastAvalanche != null && cig.LastAvalanche.CubesPaid == 9
            && cig.LastAvalanche.Points == round.RoundScore - before,
            "and the report carries what was paid");
        Check(board.CountCubesOfKind(CubeKind.Gold) == 0 && board.CountCubesOfKind(CubeKind.Normal) == 0,
            "nothing it ran over is left - gold does not stop an avalanche");
        Check(board.GetCube(new GridPos(2, 4)) == null, "the heap left its row");
        // Rows 1..3 took the snow, and row 0 under them was empty: the layers fell one more.
        Check(CountSnow(board) == 9, "nine cells of snow", "" + CountSnow(board));
        bool settled = true;
        for (int x = 1; x <= 3; x++)
        {
            for (int y = 0; y <= 2; y++)
            {
                Cube? snow = SnowAtCell(board, x, y);
                settled &= snow.HasValue && snow.Value.SnowPower == 1 && snow.Value.SnowPacked
                    && snow.Value.SnowMelt == SnowRules.AvalancheMeltTurns;
            }
        }
        Check(settled, "packed, one power a cell, three turns to melt - and they fell without merging",
            SnowText(board, 2, 0) + " | " + SnowText(board, 2, 1) + " | " + SnowText(board, 2, 2));
        Check(round.ExternalWaterFrames.Count > 0, "the fall after it was handed to the View");
        Check(round.CleanSweepCount == 0, "crushing is not a sweep: the snow is standing there");
    }

    private static void Cig_PackedLayersStayApartUntilFreshSnowLands()
    {
        Section("çığ / packed layers cannot slide again until fresh snow lands on them");
        CigPower cig;
        GameSession session = AvalancheSetup(9811, 3, out cig);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;
        session.Powers.TryUse(cig.InstanceId, ActivationTarget.Board(new GridPos(2, 4)));

        Check(!round.PlanAvalanche(new GridPos(2, 2)).Any && !round.PlanAvalanche(new GridPos(2, 1)).Any,
            "no packed row can be triggered again");

        // Fresh snow on the TOP layer (row 2): absorbed by it, and only by it.
        PlayBar(round, 1, true, new GridPos(2, 5));
        Check(CountSnow(board) == 9, "the fresh cube was absorbed", "" + CountSnow(board));
        Check(IsSnowWith(board, 1, 2, 2, 5) && IsSnowWith(board, 3, 2, 2, 5),
            "the top layer is a heap of two power with the fresh cube's five turns",
            SnowText(board, 1, 2));
        Check(IsSnowWith(board, 2, 1, 1, SnowRules.AvalancheMeltTurns - 1),
            "the layer under it is untouched (and a turn nearer melting)", SnowText(board, 2, 1));

        AvalanchePlan again = round.PlanAvalanche(new GridPos(2, 2));
        Check(again.Any && again.CoveredCells().Count == 6,
            "and now that row can slide: two power, two rows, three columns",
            "" + again.CoveredCells().Count);
        int before = round.RoundScore;
        cig.Recharge();
        Check(session.Powers.TryUse(cig.InstanceId, ActivationTarget.Board(new GridPos(2, 2))),
            "it runs a second time");
        Check(round.RoundScore == before, "snow buried under snow pays nothing",
            "" + (round.RoundScore - before));
        Check(CountSnow(board) == 6 && board.GetCube(new GridPos(2, 2)) == null,
            "the top layer came down over the two under it", "" + CountSnow(board));
    }

    private static void Cig_StopsAtTheEdgeAndRefusesALineThatCannotSlide()
    {
        Section("çığ / stops at the edge of the arena; a line that cannot slide keeps the charge");
        var session = NewSession(9812, 7, 1000000, 40, 1);
        var cig = (CigPower)session.Powers.Add(new CigPower());
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;
        board.SetCubeAt(new GridPos(2, 0), new Cube(CubeKind.Normal, 77));
        board.SetCubeAt(new GridPos(2, 1), new Cube(CubeKind.Snow, 500, false, 4, 5, 0));
        board.SetCubeAt(new GridPos(5, 0), new Cube(CubeKind.Snow, 501, false, 3, 5, 0));
        round.NoteBoardRearranged();

        Check(!session.Powers.CanUse(cig.InstanceId, ActivationTarget.Board(new GridPos(5, 0))),
            "a heap already on the floor has nowhere to go");
        Check(!session.Powers.TryUse(cig.InstanceId, ActivationTarget.Board(new GridPos(0, 3)))
            && cig.Charged, "a row with no snow is refused and the charge is kept");

        Check(session.Powers.TryUse(cig.InstanceId, ActivationTarget.Board(new GridPos(2, 1))),
            "four power, one row of room: it still comes down");
        Check(IsSnowWith(board, 2, 0, 1, SnowRules.AvalancheMeltTurns) && CountSnow(board) == 2,
            "one row was covered and the rest of the snow is lost", SnowText(board, 2, 0));
        Check(board.GetCube(new GridPos(2, 1)) == null, "the heap left its row");

        // A black hole in the way stops the column on it; so does a "Parazit" host.
        var other = NewSession(9813, 7, 1000000, 40, 1);
        GameBoard b2 = other.CurrentRound.Board;
        b2.SetCubeAt(new GridPos(3, 2), new Cube(CubeKind.Void, 1));
        b2.SetCubeAt(new GridPos(3, 4), new Cube(CubeKind.Snow, 2, false, 3, 5, 0));
        AvalanchePlan plan = b2.PlanAvalanche(new GridPos(3, 4));
        Check(plan.Columns.Count == 1 && plan.Columns[0].Covered.Count == 1,
            "a black hole is never covered: the column ends above it",
            plan.Columns.Count > 0 ? "" + plan.Columns[0].Covered.Count : "no column");
    }

    private static void Cig_IsAPowerNowAndTheJokerIsGone()
    {
        Section("çığ / the joker is gone, the power is in the catalogue");
        Check(JokerRegistry.Get("cig") == null, "no joker answers to cig any more");
        Check(PowerRegistry.Get("cig") != null && PowerRegistry.Create("cig") is CigPower,
            "the power does");
        Check(new CigPower().Targeting == ActivationTargeting.BoardCell, "and it is aimed at a cell");
    }

    private static void Snow_TheMarketSellsOneRowBarsOnly()
    {
        Section("kar / the market sells snow as 1x1..1x4 bars, never two rows");
        var seen = new HashSet<int>();
        int offers = 0;
        for (int seed = 0; seed < 12; seed++)
        {
            var config = new GameConfig();
            config.RngSeed = 9820 + seed;
            // Tetromino-sized shapes: anything the snow rule did not flatten would show.
            config.Deck = new DeckDefinition("test", 6, new SizedShapeGenerator(new[] { 3 }),
                new RandomPolyominoGenerator());
            config.Progression = new FixedProgression(3, 10);
            config.Market.ElementChance = 1.0;
            config.Market.SnowOfferChance = 1.0;
            var session = new GameSession(config);
            if (!ReachMarket(session))
            {
                continue;
            }
            foreach (MarketOffer offer in session.Market.Offers)
            {
                if (offer.Kind != MarketOfferKind.Block || offer.Card == null)
                {
                    continue;
                }
                if (!offer.Card.Has(BlockElement.Snow))
                {
                    // Only a "Hedefli" roll is left alone when snow is certain.
                    Check(offer.Card.Has(BlockElement.Targeted), "what is not snow is a targeted block");
                    continue;
                }
                offers++;
                BlockShape shape = offer.Card.Shape;
                bool oneRow = true;
                foreach (GridPos cell in shape.Cells)
                {
                    oneRow &= cell.Y == shape.Cells[0].Y;
                }
                Check(offer.Card.Has(BlockElement.Snow) && oneRow && shape.Size >= 1
                    && shape.Size <= SnowRules.MaxBarLength,
                    "a snow offer is one row of 1..4", shape.Size + " cubes, one row " + oneRow);
                seen.Add(shape.Size);
            }
        }
        Check(offers > 0, "snow offers were actually rolled", "" + offers);
        Check(seen.Count >= 3, "in more than one length", string.Join(",", seen));
    }

    private static void Strata_LaterSnowFeedsTheTopLayerAndTheSeamHolds()
    {
        Section("kar / strata: snow landing on the top layer feeds it; the seam under it holds");
        CigPower cig;
        GameSession session = AvalancheSetup(9840, 3, out cig);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;
        session.Powers.TryUse(cig.InstanceId, ActivationTarget.Board(new GridPos(2, 4)));
        // Layers stand on rows 0, 1 and 2 (they fell a row after the avalanche).
        Check(board.SnowSeamBelow(new GridPos(2, 2)) && board.SnowSeamBelow(new GridPos(2, 1)),
            "a seam under the top two layers");
        Check(!board.SnowSeamBelow(new GridPos(2, 0)), "none under the floor layer");
        int stratum = board.GetCube(new GridPos(2, 2)).Value.SnowStratum;
        Check(stratum != 0 && board.GetCube(new GridPos(2, 0)).Value.SnowStratum == stratum,
            "one avalanche, one stratum");

        PlayBar(round, 3, true, new GridPos(1, 5));
        Check(IsSnowWith(board, 2, 2, 2, 5), "the top layer took the fresh bar: power 2, five turns",
            SnowText(board, 2, 2));
        Check(board.GetCube(new GridPos(2, 2)).Value.SnowStratum == stratum,
            "and kept its stratum");
        Check(board.SnowSeamBelow(new GridPos(2, 2)), "so the seam under it is still there");
        Check(SnowAtCell(board, 2, 1).HasValue && SnowAtCell(board, 2, 1).Value.SnowPower == 1,
            "and the layer under it did not take anything", SnowText(board, 2, 1));
        Check(CountSnow(board) == 9, "nine cells of snow, three layers", "" + CountSnow(board));
    }

    private static void Strata_AnotherAvalanchesLayerMergesAsUsual()
    {
        Section("kar / strata: a layer of ANOTHER avalanche is not a barrier");
        var board = new GameBoard(5, 5);
        board.SetCubeAt(new GridPos(2, 0), new Cube(CubeKind.Snow, 1, false, 1, 3, 1));
        board.SetCubeAt(new GridPos(2, 3), new Cube(CubeKind.Snow, 2, false, 1, 3, 2));
        board.SettleWaterAndReact();
        Check(board.OccupiedCount == 1 && IsSnowWith(board, 2, 0, 2, 4),
            "two strata merge: power 2, three and three make four", SnowText(board, 2, 0));
        var same = new GameBoard(5, 5);
        same.SetCubeAt(new GridPos(2, 0), new Cube(CubeKind.Snow, 1, false, 1, 3, 7));
        same.SetCubeAt(new GridPos(2, 3), new Cube(CubeKind.Snow, 2, false, 1, 3, 7));
        same.SettleWaterAndReact();
        Check(same.OccupiedCount == 2 && IsSnowWith(same, 2, 1, 1, 3),
            "the same stratum stacks instead", SnowText(same, 2, 1));
    }

    private static void Snow_TheMergeIsReportedWithItsBeforeAndAfter()
    {
        Section("kar / every merge is reported: the heap, the cubes it took, power and melt before and after");
        var board = new GameBoard(5, 5);
        board.SetCubeAt(new GridPos(1, 0), new Cube(CubeKind.Snow, 1, false, 2, 3, 0));
        board.SetCubeAt(new GridPos(2, 0), new Cube(CubeKind.Snow, 1, false, 2, 3, 0));
        board.SetCubeAt(new GridPos(1, 3), Cube.FreshSnow(2));
        board.SetCubeAt(new GridPos(2, 3), Cube.FreshSnow(2));
        var frames = new List<IReadOnlyList<WaterMove>>();
        board.SettleWaterAndReact(frames);
        Check(board.SnowMerges.Count == 1, "one heap fed one heap: one merge", "" + board.SnowMerges.Count);
        if (board.SnowMerges.Count == 1)
        {
            SnowMerge m = board.SnowMerges[0];
            Check(m.PowerBefore == 2 && m.PowerAfter == 3, "power 2 -> 3", m.PowerBefore + " -> " + m.PowerAfter);
            Check(m.MeltBefore == 3 && m.MeltAfter == 5 && m.Refreshed, "melt 3 -> 5, a refresh");
            Check(m.Heap.Count == 2 && m.Absorbed.Count == 2, "the two-wide heap took both cubes");
            bool inFrames = false;
            foreach (IReadOnlyList<WaterMove> frame in frames)
            {
                foreach (WaterMove move in frame)
                {
                    inFrames |= move.From.Equals(m.Absorbed[0].From) && move.To.Equals(m.Absorbed[0].To);
                }
            }
            Check(inFrames, "and the absorbing move is in the frames the View plays");
        }
        board.ClearSnowReports();
        Check(board.SnowMerges.Count == 0, "cleared on request");
        var quiet = new GameBoard(5, 5);
        quiet.SetCubeAt(new GridPos(1, 0), new Cube(CubeKind.Snow, 1, false, 1, 5, 0));
        quiet.SetCubeAt(new GridPos(1, 3), new Cube(CubeKind.Snow, 2, false, 1, 2, 0));
        quiet.SettleWaterAndReact(new List<IReadOnlyList<WaterMove>>());
        Check(quiet.SnowMerges.Count == 1 && !quiet.SnowMerges[0].Refreshed,
            "older snow landing on fresher snow is a merge without a refresh");
    }

    private static void Cig_ThePlanSaysWhyALineIsRefused()
    {
        Section("çığ / the plan names why a line cannot avalanche");
        var board = new GameBoard(5, 5);
        Check(board.PlanAvalanche(new GridPos(0, 2)).Blocked == AvalancheBlock.NoSnow, "no snow");
        board.SetCubeAt(new GridPos(2, 2), new Cube(CubeKind.Snow, 1, false, 1, 3, 4));
        board.SetCubeAt(new GridPos(2, 1), new Cube(CubeKind.Normal, 3));
        board.SetCubeAt(new GridPos(2, 0), new Cube(CubeKind.Normal, 3));
        AvalanchePlan spent = board.PlanAvalanche(new GridPos(0, 2));
        Check(spent.Blocked == AvalancheBlock.Spent && spent.SpentCells.Count == 1, "a spent layer");
        var floor = new GameBoard(5, 5);
        floor.SetCubeAt(new GridPos(2, 0), new Cube(CubeKind.Snow, 1, false, 3, 5, 0));
        Check(floor.PlanAvalanche(new GridPos(0, 0)).Blocked == AvalancheBlock.NoRoom, "nowhere to go");
        var ok = new GameBoard(5, 5);
        ok.SetCubeAt(new GridPos(1, 3), new Cube(CubeKind.Snow, 1, false, 2, 5, 0));
        ok.SetCubeAt(new GridPos(3, 3), new Cube(CubeKind.Snow, 1, false, 1, 5, 0));
        AvalanchePlan two = ok.PlanAvalanche(new GridPos(0, 3));
        Check(two.Blocked == AvalancheBlock.None && two.Columns.Count == 2 && two.Line == 3 && two.Depth == 2,
            "two heaps, two groups, two rows deep");
        Check(two.Columns[0].Heap != two.Columns[1].Heap && two.Columns[0].Power == 2,
            "each column says which heap it is and that heap's power");
    }

    private static void Snow_SurvivesASaveWithItsNumbers()
    {
        Section("kar / a save carries every cube's snow numbers");
        var config = new GameConfig();
        config.RngSeed = 9830;
        config.Deck = new DeckDefinition("test", 40, new SizedShapeGenerator(new[] { 1 }));
        config.Progression = new FixedProgression(7, 1000000);
        var session = new GameSession(config);
        GameBoard board = session.CurrentRound.Board;
        board.SetCubeAt(new GridPos(1, 0), new Cube(CubeKind.Snow, 11, false, 3, 2, 1));
        board.SetCubeAt(new GridPos(4, 0), new Cube(CubeKind.Snow, 12, false, 1, 5, 0));
        session.CurrentRound.NoteBoardRearranged();

        var template = new GameConfig();
        template.RngSeed = 9830;
        template.Deck = new DeckDefinition("test", 40, new SizedShapeGenerator(new[] { 1 }));
        template.Progression = new FixedProgression(7, 1000000);
        GameSession back = SaveGame.Load(SaveGame.Save(session), template);
        Cube? packed = back.CurrentRound.Board.GetCube(new GridPos(1, 0));
        Cube? fresh = back.CurrentRound.Board.GetCube(new GridPos(4, 0));
        Check(packed.HasValue && packed.Value.Kind == CubeKind.Snow && packed.Value.SnowPower == 3
            && packed.Value.SnowMelt == 2 && packed.Value.SnowPacked, "the packed heap came back as it was");
        Check(fresh.HasValue && fresh.Value.SnowPower == 1 && fresh.Value.SnowMelt == 5
            && !fresh.Value.SnowPacked, "and so did the fresh cube");
    }
}
