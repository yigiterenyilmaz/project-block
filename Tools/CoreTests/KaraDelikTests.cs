// PURPOSE: "Kara Delik" as a real black hole (2026-09-16): a void card may be laid over a cube, the
// hole never leaves, it eats ring 1 and pulls ring 2 every turn, the round's swallowed count
// collapses the arena into a sweep when it reaches the arena's size, and the gravity sometimes
// swallows the smaller card pile - at most twice a round.

using System;
using System.Collections.Generic;
using System.Reflection;
using ProjectBlock.Core;

public static partial class JokerTests
{
    /// <summary>Lays a void card through a real turn (from the bonus hand).</summary>
    private static TurnReport LayHole(GameSession session, GridPos cell)
    {
        RoundEngine round = session.CurrentRound;
        BlockCard hole = session.CreateCard(Bar(1), new[] { BlockElement.Void });
        round.AddBonusCard(hole, BonusPlayOutcome.ToDiscard);
        return round.PlayFromBonus(round.BonusHand.Count - 1, cell);
    }

    private static bool IsHole(GameBoard board, GridPos cell)
    {
        Cube? cube = board.GetCube(cell);
        return cube.HasValue && cube.Value.Kind == CubeKind.Void;
    }

    private static void SetSwallowed(KaraDelikJoker joker, int value)
    {
        typeof(KaraDelikJoker).GetProperty("SwallowedThisRound")
            .SetValue(joker, value, BindingFlags.NonPublic | BindingFlags.Instance, null, null, null);
    }

    private static void KaraDelik_AVoidCardSwallowsAFilledCell()
    {
        Section("kara delik / a void card goes over a filled cell");
        var session = NewSession(601, 9, 1000000, 40, 1);
        var joker = (KaraDelikJoker)session.Jokers.Add(new KaraDelikJoker());
        joker.DeckSwallowChancePercent = 0;
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;
        var cell = new GridPos(0, 0);
        PaintBoard(round, session, CubeKind.Gold, cell);
        BlockCard hole = session.CreateCard(Bar(1), new[] { BlockElement.Void });
        Check(round.CanPlaceCard(hole, cell), "a void card may be aimed at a cube - even gold");
        Check(!round.CanPlaceCard(session.CreateCard(Bar(1), null), cell),
            "an ordinary block still may not");

        round.AddBonusCard(hole, BonusPlayOutcome.ToDiscard);
        int scoreBefore = round.RoundScore;
        TurnReport report = round.PlayFromBonus(round.BonusHand.Count - 1, cell);
        Check(IsHole(round.Board, cell), "the hole stands where the gold was");
        Check(report.VoidSwallows.Count == 1 && report.VoidSwallows[0].Cube.Kind == CubeKind.Gold,
            "the report says it swallowed the gold");
        bool logged = false;
        foreach (DestroyedCube dead in report.DestroyedCubes)
        {
            logged |= dead.Pos.Equals(cell) && dead.Cube.Kind == CubeKind.Gold;
        }
        Check(logged, "and the gold went through the engine's destruction log");
        Check(joker.SwallowedThisRound == 1, "one swallowed", "" + joker.SwallowedThisRound);
        Check(round.RoundScore - scoreBefore >= joker.PointsPerCube * session.Config.Scoring.ScoreScale,
            "and it paid", (round.RoundScore - scoreBefore).ToString());
        Check(!ContainsCard(round.Deck.DiscardPile, hole.Id) && !ContainsCard(round.Deck.DrawPile, hole.Id),
            "the card is spent - the hole IS the card now");

        BlockCard onHole = session.CreateCard(Bar(1), null);
        Check(!round.CanPlaceCard(session.CreateCard(Bar(1), new[] { BlockElement.Void }), cell),
            "a hole is never laid on a hole");
        Check(round.CanPlaceCard(onHole, cell), "a block may be laid on it");
        round.AddBonusCard(onHole, BonusPlayOutcome.ToDiscard);
        report = round.PlayFromBonus(round.BonusHand.Count - 1, cell);
        Check(IsHole(round.Board, cell) && report.VoidSwallows.Count == 1
                && report.VoidSwallows[0].Cube.SourceCardId == onHole.Id,
            "a block that lands on it falls in, and the hole stays");
        Check(joker.SwallowedThisRound == 2, "that counts too", "" + joker.SwallowedThisRound);
    }

    private static bool ContainsCard(IReadOnlyList<BlockCard> pile, int id)
    {
        foreach (BlockCard card in pile)
        {
            if (card.Id == id)
            {
                return true;
            }
        }
        return false;
    }

    private static void KaraDelik_EatsRingOnePullsRingTwoAndNothingFurther()
    {
        Section("kara delik / eats ring 1, pulls ring 2, leaves ring 3");
        var session = NewSession(603, 9, 1000000, 40, 1);
        var joker = (KaraDelikJoker)session.Jokers.Add(new KaraDelikJoker());
        joker.DeckSwallowChancePercent = 0;
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;
        var hole = new GridPos(4, 4);
        LayHole(session, hole);

        var ring1 = new GridPos(4, 5);
        var ring1Obsidian = new GridPos(3, 3);
        var ring2 = new GridPos(6, 4);
        var ring2Corner = new GridPos(2, 6);
        var ring3 = new GridPos(7, 4);
        PaintBoard(round, session, CubeKind.Normal, ring1, ring2, ring2Corner, ring3);
        PaintBoard(round, session, CubeKind.Obsidian, ring1Obsidian);
        Cube ring2Cube = board.GetCube(ring2).Value;
        int swallowedBefore = joker.SwallowedThisRound;

        TurnReport report = LayCube(session, new GridPos(0, 8)); // far away, touches nothing
        Check(!board.GetCube(ring1).HasValue && !board.GetCube(ring1Obsidian).HasValue,
            "ring 1 was eaten - obsidian too");
        Check(!board.GetCube(ring2).HasValue && board.GetCube(new GridPos(5, 4)).HasValue
                && board.GetCube(new GridPos(5, 4)).Value.SourceCardId == ring2Cube.SourceCardId,
            "ring 2 was pulled one step straight in");
        Check(!board.GetCube(ring2Corner).HasValue && board.GetCube(new GridPos(3, 5)).HasValue,
            "a ring-2 corner is pulled diagonally in");
        Check(board.GetCube(ring3).HasValue, "ring 3 does not feel it");
        Check(IsHole(board, hole), "the hole is still there");
        Check(joker.SwallowedThisRound - swallowedBefore == 2, "two swallowed this turn",
            "" + (joker.SwallowedThisRound - swallowedBefore));
        BlackHoleVisuals seen = joker.LastTurn;
        Check(seen != null && seen.Bites.Count == 2 && seen.Pulls.Count == 2
                && seen.Holes.Count == 1 && seen.Holes[0].Equals(hole),
            "the report names the bites, the pulls and the hole",
            seen == null ? "null" : seen.Bites.Count + " / " + seen.Pulls.Count);
        int flat = 0;
        foreach (ScoreContribution entry in report.Score.Contributions)
        {
            if (entry.Source == "kara_delik")
            {
                flat += entry.Flat;
            }
        }
        Check(flat == 2 * joker.PointsPerCube, "each swallowed cube paid", "flat " + flat);
        bool destroyedLogged = false;
        foreach (DestroyedCube dead in report.DestroyedCubes)
        {
            destroyedLogged |= dead.Pos.Equals(ring1);
        }
        Check(destroyedLogged, "the bites are real destruction");
        bool pulledLogged = false;
        foreach (DestroyedCube dead in report.DestroyedCubes)
        {
            pulledLogged |= dead.Pos.Equals(ring2);
        }
        Check(!pulledLogged, "a pulled cube is not a dead one");

        LayCube(session, new GridPos(0, 7));
        Check(!board.GetCube(new GridPos(5, 4)).HasValue && !board.GetCube(new GridPos(3, 5)).HasValue,
            "next turn the pulled cubes are eaten");
        Check(board.GetCube(ring3).HasValue, "and ring 3 is still untouched");
    }

    private static void KaraDelik_NothingMovesOrRemovesAHole()
    {
        Section("kara delik / nothing moves or removes a hole");
        var session = NewSession(607, 7, 1000000, 40, 1);
        var joker = (KaraDelikJoker)session.Jokers.Add(new KaraDelikJoker());
        joker.DeckSwallowChancePercent = 0;
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;
        var hole = new GridPos(6, 6);
        LayHole(session, hole);

        Check(!board.DestroyCube(hole), "a destroy is refused");
        board.AnchorRefusals.Clear();
        Check(!board.DestroyCubeForced(hole), "a forced destroy is refused");
        Check(board.AnchorRefusals.Count == 1 && board.AnchorRefusals.Refusals[0].Cell.Equals(hole),
            "and the refusal is reported for the View's hold beat");
        board.DestroyAllDestructible();
        Check(IsHole(board, hole), "dynamite's wipe leaves it");
        Check(!board.SetCubeKind(hole, CubeKind.Water), "it cannot be retyped (flood, fire, ice)");
        board.SetCubeAt(hole, new Cube(CubeKind.Normal, 1));
        Check(IsHole(board, hole), "nothing is written over it");
        Check(!board.SetCubeProtected(hole), "a parasite cannot take it as a host");
        round.ForgetCard(board.GetCube(hole).Value.SourceCardId);
        Check(IsHole(board, hole), "forgetting its card does not lift it");
        Check(!board.CanCompressAt(new GridPos(5, 5)), "the press cannot take a patch with it in");
        board.MarkDead(new[] { hole });
        Check(board.IsInside(hole) && IsHole(board, hole), "erosion cannot eat its ground");

        // a line through it goes off around it
        for (int x = 0; x < 6; x++)
        {
            PaintBoard(round, session, CubeKind.Normal, new GridPos(x, 6));
        }
        // (the hole will eat (5,6) at the end of this turn anyway, after the line has gone)
        TurnReport lineTurn = LayCube(session, new GridPos(0, 0));
        Check(IsHole(board, hole), "a line explosion through it leaves it");

        // the escalator rides everything up; a cube riding into the hole falls in
        var below = new GridPos(3, 2);
        var holeLow = new GridPos(3, 3);
        var session2 = NewSession(609, 7, 1000000, 40, 1);
        RoundEngine r2 = session2.CurrentRound;
        LayHole(session2, holeLow);
        PaintBoard(r2, session2, CubeKind.Normal, below, new GridPos(0, 0));
        int before = r2.Board.OccupiedCount;
        IReadOnlyList<GridPos> lost = r2.EscalateBoards();
        Check(IsHole(r2.Board, holeLow), "the escalator does not carry it");
        bool heldUp = false;
        foreach (AnchorRefusal refusal in r2.Board.AnchorRefusals.Refusals)
        {
            heldUp |= refusal.Cell.Equals(holeLow) && refusal.Step.Equals(new GridPos(0, 1));
        }
        Check(heldUp, "the refusal says which way the escalator pushed");
        Check(Contains(lost, below) && !r2.Board.GetCube(new GridPos(3, 4)).HasValue,
            "the cube that rode into it fell in",
            string.Join(",", lost));
        Check(r2.Board.GetCube(new GridPos(0, 1)).HasValue, "others rode up as usual");
        Check(r2.Board.OccupiedCount == before - 1, "occupancy is honest",
            before + " -> " + r2.Board.OccupiedCount);

        IReadOnlyList<GridPos> flung = r2.FlingBoardsOutward();
        Check(IsHole(r2.Board, holeLow), "the centrifuge does not fling it");

        var snapshot = new Dictionary<GridPos, Cube>();
        r2.Board.SnapshotInto(snapshot);
        snapshot.Remove(holeLow);
        r2.Board.RestoreFrom(snapshot);
        Check(IsHole(r2.Board, holeLow), "rewinding time does not undo it");

        Check(r2.Board.SwapLines(LineAxis.Row, 3, 0) && IsHole(r2.Board, holeLow),
            "swapping its line leaves it where it is");
    }

    private static bool Contains(IReadOnlyList<GridPos> cells, GridPos cell)
    {
        foreach (GridPos c in cells)
        {
            if (c.Equals(cell))
            {
                return true;
            }
        }
        return false;
    }

    private static void KaraDelik_TheViewReadsTheSameRings()
    {
        Section("kara delik / the reach the view lenses is the rules' own");
        var holes = new List<GridPos> { new GridPos(3, 3), new GridPos(6, 3) };
        GridPos near;
        Check(KaraDelikJoker.InfluenceAt(holes, new GridPos(4, 4), out near) == 1 && near.Equals(holes[0]),
            "a diagonal neighbour is ring 1");
        Check(KaraDelikJoker.InfluenceAt(holes, new GridPos(1, 5), out near) == 2, "two out is ring 2");
        Check(KaraDelikJoker.InfluenceAt(holes, new GridPos(0, 0), out near) == 0, "three out is nothing");
        Check(KaraDelikJoker.InfluenceAt(holes, new GridPos(5, 3), out near) == 1 && near.Equals(holes[1]),
            "between two holes, the nearer ring wins");
        Check(KaraDelikJoker.InfluenceAt(holes, holes[0], out near) == 0, "the hole itself is not lensed");

        // the lab's gravity is the joker's gravity
        var board = new GameBoard(7, 7);
        board.SetCubeAt(new GridPos(3, 3), new Cube(CubeKind.Void, 1));
        board.SetCubeAt(new GridPos(4, 3), new Cube(CubeKind.Normal, 2));
        board.SetCubeAt(new GridPos(5, 3), new Cube(CubeKind.Normal, 3));
        var report = new BlackHoleVisuals();
        KaraDelikJoker.RunGravity(board, report, delegate(List<GridPos> cells)
        {
            var gone = new List<GridPos>();
            foreach (GridPos c in cells)
            {
                if (board.DestroyCubeForced(c)) { gone.Add(c); }
            }
            return gone;
        });
        Check(report.Bites.Count == 1 && report.Pulls.Count == 1
                && report.Pulls[0].To.Equals(new GridPos(4, 3)),
            "RunGravity on a bare board eats ring 1 and pulls ring 2 in, like the joker");
    }

    private static void KaraDelik_FeedingTheArenaCollapsesItIntoASweep()
    {
        Section("kara delik / swallow the arena's size and it collapses into a sweep");
        var session = NewSession(611, 5, 1000000, 40, 1);
        var joker = (KaraDelikJoker)session.Jokers.Add(new KaraDelikJoker());
        joker.DeckSwallowChancePercent = 0;
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;
        Check(KaraDelikJoker.GoalFor(board) == 25, "the goal is the arena's cells",
            "" + KaraDelikJoker.GoalFor(board));
        var hole = new GridPos(0, 0);
        LayHole(session, hole);

        // a nearly full board with a gap in every row and column (so no line can go off and do
        // the collapse's work for it): the diagonal is empty, and so are (0,2) and (2,0), because
        // the hole itself fills its cell for row 0 and column 0
        var filled = new List<GridPos>();
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                var p = new GridPos(x, y);
                if (x == y || p.Equals(new GridPos(0, 2)) || p.Equals(new GridPos(2, 0))
                    || p.Equals(new GridPos(1, 0)))
                {
                    continue;
                }
                filled.Add(p);
            }
        }
        PaintBoard(round, session, CubeKind.Normal, filled.ToArray());
        PaintBoard(round, session, CubeKind.Gold, new GridPos(4, 2));
        PaintBoard(round, session, CubeKind.Normal, new GridPos(1, 0)); // ring 1: the 25th bite
        SetSwallowed(joker, 24);
        int sweepsBefore = round.CleanSweepCount;
        int scoreBefore = round.RoundScore;

        TurnReport report = LayCube(session, new GridPos(2, 2)); // completes nothing
        BlackHoleVisuals seen = joker.LastTurn;
        Check(seen != null && seen.Collapsed && seen.SweepFired, "the count reached 25 and it collapsed",
            seen == null ? "null" : seen.Collapsed + " " + seen.SweepFired);
        Check(report.CleanSweep && round.CleanSweepCount == sweepsBefore + 1,
            "the clean sweep went off");
        int left = 0;
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                if (board.GetCube(new GridPos(x, y)).HasValue)
                {
                    left++;
                }
            }
        }
        Check(left == 1 && IsHole(board, hole), "every block went - gold too - and only the hole stands",
            "left " + left);
        Check(report.ExplodedRows.Count + report.ExplodedColumns.Count == 0
                && seen != null && seen.CollapseCubes.Count >= 15, "the collapse took the rest of the board",
            seen == null ? "" : "" + seen.CollapseCubes.Count);
        Check(joker.SwallowedThisRound == 0, "the count starts again", "" + joker.SwallowedThisRound);
        Check(round.RoundScore > scoreBefore, "and it all paid");
        Check(joker.GrantedThisRound >= 1, "the sweep handed out a new hole card");
    }

    private static void KaraDelik_GravitySlipsAndSwallowsTheSmallerPile()
    {
        Section("kara delik / the gravity swallows the smaller pile, at most twice");
        var session = NewSession(613, 9, 1000000, 40, 1);
        var joker = (KaraDelikJoker)session.Jokers.Add(new KaraDelikJoker());
        joker.DeckSwallowChancePercent = 100;
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;
        // a few turns so the discard fills up but stays smaller than the draw pile
        LayCube(session, new GridPos(0, 0));
        DeckSwallowVisuals first = joker.LastDeckSwallow;
        Check(first != null && first.Number == 1, "it slipped");
        if (first == null)
        {
            return;
        }
        Check(!first.FromDrawPile || round.Deck.DiscardCount == 0,
            "the discard went when it was the smaller non-empty pile",
            "fromDraw " + first.FromDrawPile + " other " + first.OtherPileCount);
        foreach (int id in first.CardIds)
        {
            Check(ContainsCard(round.Deck.RemovedFromRound, id)
                    && !ContainsCard(round.Deck.DrawPile, id) && !ContainsCard(round.Deck.DiscardPile, id),
                "a swallowed card sits the round out");
        }

        LayCube(session, new GridPos(1, 0));
        DeckSwallowVisuals second = joker.LastDeckSwallow;
        Check(second != null && second.Number == 2 && !ReferenceEquals(second, first), "it slipped again");
        if (second != null)
        {
            Check(second.CardIds.Count > 0, "and took a real pile", "" + second.CardIds.Count);
            Check(second.OtherPileCount == 0 || second.CardIds.Count < second.OtherPileCount
                    || (second.CardIds.Count == second.OtherPileCount && !second.FromDrawPile),
                "always the smaller one (a tie takes the discard)",
                second.CardIds.Count + " vs " + second.OtherPileCount + " fromDraw " + second.FromDrawPile);
        }
        LayCube(session, new GridPos(2, 0));
        Check(ReferenceEquals(joker.LastDeckSwallow, second) && joker.DeckSwallowsThisRound == 2,
            "never a third time in a round");

        // the choice itself, on piles set up by hand
        var s3 = NewSession(617, 9, 1000000, 40, 1);
        var j3 = (KaraDelikJoker)s3.Jokers.Add(new KaraDelikJoker());
        j3.DeckSwallowChancePercent = 100;
        s3.Jokers.DispatchRoundStarted(s3.CurrentRound);
        RoundEngine r3 = s3.CurrentRound;
        for (int i = 0; i < 60; i++)
        {
            r3.Deck.Discard(s3.CreateCard(Bar(1), null));
        }
        LayCube(s3, new GridPos(0, 0));
        DeckSwallowVisuals pick = j3.LastDeckSwallow;
        Check(pick != null && pick.FromDrawPile && pick.CardIds.Count < pick.OtherPileCount,
            "with the discard the bigger pile, the draw pile goes",
            pick == null ? "null" : "fromDraw " + pick.FromDrawPile + " " + pick.CardIds.Count + " vs " + pick.OtherPileCount);

        var s4 = NewSession(619, 9, 1000000, 40, 1);
        var j4 = (KaraDelikJoker)s4.Jokers.Add(new KaraDelikJoker());
        j4.DeckSwallowChancePercent = 0;
        s4.Jokers.DispatchRoundStarted(s4.CurrentRound);
        for (int i = 0; i < 10; i++)
        {
            LayCube(s4, new GridPos(i % 9, 0));
        }
        Check(j4.LastDeckSwallow == null && j4.DeckSwallowsThisRound == 0, "at 0% it never slips");
    }
}
