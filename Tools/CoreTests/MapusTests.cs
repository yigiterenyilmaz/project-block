// PURPOSE: "Mapus" as the designer re-cut it (2026-10-02): a lock goes up every second turn and
// stands three, so one or two stand at a time; it is aimed by the player's CARDS - the hand and
// the coming draws, in the order they really come - at the line they could clear soonest, and
// first of all at the crossing of a row and a column; with no line clearable in time it falls back
// to the board alone; and a cell whose lock ran out is the player's for a whole turn.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;

public static partial class JokerTests
{
    private static void RunMapusTests()
    {
        Mapus_LocksEverySecondTurnAndEachStandsThree();
        Mapus_ALockedCellIsNotAPlaceToPlay();
        Mapus_ReadsTheHandNotJustTheBoard();
        Mapus_ReadsTheComingCardsInTheOrderTheyCome();
        Mapus_GoesFirstWhereARowAndAColumnCross();
        Mapus_TwoLocksDenyTwoDifferentLines();
        Mapus_ALiftedCellIsThePlayersForATurn();
        Mapus_NeverTakesTheLastHole();
        Mapus_StaysInStepWithTheBoardThroughRealTurns();
        Mapus_ALoadedRunStillHasALockReport();
    }

    private static MapusBoss StartMapus(GameSession session)
    {
        RoundEngine round = session.CurrentRound;
        var boss = new MapusBoss();
        round.SetBoss(boss);
        boss.OnRoundStarted(new RoundContext(session, session.Rng, round));
        return boss;
    }

    private static string MapusCells(MapusBoss boss)
    {
        var sb = new System.Text.StringBuilder();
        foreach (GridPos cell in boss.SealedCells)
        {
            sb.Append(cell.X).Append(',').Append(cell.Y).Append('(')
                .Append(boss.TurnsLeftOn(cell)).Append(") ");
        }
        return sb.Length > 0 ? sb.ToString() : "none";
    }

    private static bool MapusHolds(MapusBoss boss, int x, int y)
    {
        return boss.TurnsLeftOn(new GridPos(x, y)) > 0;
    }

    /// <summary>
    /// THE RHYTHM. One lock as the round starts, another after every second turn, each standing
    /// three turns - so the turns are played round 1, 1, 2, 1, 2, 1, 2 locks.
    /// </summary>
    private static void Mapus_LocksEverySecondTurnAndEachStandsThree()
    {
        Section("boss / mapus locks every second turn, each for three");
        var session = NewSession(5162, 6, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        MapusBoss boss = StartMapus(session);

        Check(boss.SealedCells.Count == 1, "one cell is locked as the round starts",
            MapusCells(boss));
        GridPos first = boss.SealedCells[0];
        Check(boss.TurnsLeftOn(first) == 3, "and it will stand three turns",
            "left " + boss.TurnsLeftOn(first));
        Check(boss.LastSeal != null && boss.LastSeal.Placed && boss.LastSeal.Seals.Count == 1
                && boss.LastSeal.Seals[0].IsNew,
            "the View is told a lock went up");

        // After each turn end: how many stand for the turn about to be played.
        int[] expected = { 1, 2, 1, 2, 1, 2 };
        bool rhythm = true;
        bool inStep = true;
        string seen = "1";
        for (int turn = 0; turn < expected.Length; turn++)
        {
            boss.AfterTurnScored(FakeTurnFor(session, round));
            seen += " " + boss.SealedCells.Count;
            rhythm &= boss.SealedCells.Count == expected[turn];
            inStep &= round.Board.SealedCells.Count == boss.SealedCells.Count;
            if (turn == 1)
            {
                Check(boss.TurnsLeftOn(first) == 1,
                    "the first lock is on its last turn when the second goes up",
                    MapusCells(boss));
            }
            if (turn == 2)
            {
                Check(!round.Board.IsSealed(first) && boss.TurnsLeftOn(first) == 0,
                    "after three turns the first lock is gone", MapusCells(boss));
                Check(boss.LastSeal.Expired.Count == 1 && boss.LastSeal.Expired[0].Equals(first)
                        && !boss.LastSeal.Placed,
                    "and the View is told whose time ran out");
            }
        }
        Check(rhythm, "one lock, then two, then one, then two", seen);
        Check(inStep, "and the board carries exactly the locks the boss counts");
    }

    private static void Mapus_ALockedCellIsNotAPlaceToPlay()
    {
        Section("boss / mapus: a locked cell refuses a block");
        var session = NewSession(5162, 6, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        MapusBoss boss = StartMapus(session);
        GridPos locked = boss.SealedCells[0];

        Check(round.Board.IsSealed(locked), "the board knows the cell is locked",
            locked.X + "," + locked.Y);
        Check(!round.Board.GetCube(locked).HasValue, "a locked cell holds no cube");
        Check(!round.CanPlaceCard(round.Hand[0], locked), "a block cannot be placed on it");
        Check(!round.Board.CanPlace(round.Hand[0].Shape, locked), "CanPlace refuses it directly");
        List<GridPos> origins = round.GetValidOrigins(round.Hand[0].Shape);
        bool offered = false;
        for (int i = 0; i < origins.Count; i++)
        {
            offered |= origins[i].Equals(locked);
        }
        Check(!offered, "the locked cell is not offered as a legal origin");
        Check(origins.Count == round.Board.PlayableCellCount - 1,
            "every OTHER empty cell is still legal",
            "origins " + origins.Count + " cells " + round.Board.PlayableCellCount);
    }

    /// <summary>
    /// A 7x7 board with two lines open: row 0 is ONE cube from full, but its gap is a pocket only
    /// a 1x1 fits and the player holds nothing but four-long bars; row 5 is FOUR cubes from full,
    /// and a four-long bar drops straight into it.
    /// </summary>
    private static GameSession MapusPocketAndSlot(int seed)
    {
        var session = NewSession(seed, 7, 1000000, 40, 4);
        PaintBoard(session.CurrentRound, session, CubeKind.Normal,
            new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0), new GridPos(4, 0),
            new GridPos(5, 0), new GridPos(6, 0), new GridPos(3, 1),
            new GridPos(4, 5), new GridPos(5, 5), new GridPos(6, 5));
        return session;
    }

    /// <summary>
    /// IT READS YOUR HAND. The board alone says row 0 - one cube from full - is the line to deny,
    /// and that is where the old Mapus sat. But the hand cannot fill that pocket and CAN clear
    /// row 5 this very turn, so that is the line the lock is laid against.
    /// </summary>
    private static void Mapus_ReadsTheHandNotJustTheBoard()
    {
        Section("boss / mapus reads the hand, not just the board");
        GameSession session = MapusPocketAndSlot(5170);
        RoundEngine round = session.CurrentRound;
        Check(round.Board.RowGapCount(0) == 1 && round.Board.RowGapCount(5) == 4,
            "row 0 is one cube from full and row 5 four",
            round.Board.RowGapCount(0) + "/" + round.Board.RowGapCount(5));

        MapusBoss boss = StartMapus(session);
        GridPos locked = boss.SealedCells[0];
        Check(locked.Y == 5 && locked.X <= 3,
            "the lock lands in the row the HAND can clear, not the fullest one",
            locked.X + "," + locked.Y);
        Check(boss.LastSeal.AimedByCards, "and the report says the cards aimed it");
        Check(Math.Abs(boss.LastSeal.PlacedRowThreat - 1.0) < 0.001,
            "that row could go off on the very next turn: the threat is whole",
            boss.LastSeal.PlacedRowThreat.ToString("0.000"));

        // The same board with no cards to read: the boss's own fallback is the board alone, and
        // the board alone says the pocket.
        GameSession blind = MapusPocketAndSlot(5170);
        var blindBoss = new MapusBoss();
        MapusSealVisuals report = blindBoss.StartOn(blind.CurrentRound.Board, null,
            new SeededRandom(3));
        Check(MapusHolds(blindBoss, 3, 0),
            "with no cards to read it falls back to the cell nearest completion",
            MapusCells(blindBoss));
        Check(report.Placed && !report.AimedByCards, "and says the board aimed that one");
    }

    /// <summary>The pocket alone on the board, a hand of four-long bars, and a draw pile cut down
    /// to <paramref name="pileSize"/> cards.</summary>
    private static GameSession MapusPocket(int seed, int pileSize)
    {
        var session = NewSession(seed, 7, 1000000, 40, 4);
        RoundEngine round = session.CurrentRound;
        PaintBoard(round, session, CubeKind.Normal,
            new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0), new GridPos(4, 0),
            new GridPos(5, 0), new GridPos(6, 0), new GridPos(3, 1));
        while (round.Deck.DrawCount > pileSize)
        {
            round.Deck.Discard(round.Deck.DrawTop());
        }
        return session;
    }

    /// <summary>
    /// IT READS WHAT YOU ARE ABOUT TO DRAW, IN ORDER. Nothing in hand fits the pocket. With the
    /// one block that does on TOP of the draw pile the row goes off on the second turn, and the
    /// lock is laid against it; with that same block three cards down it cannot arrive while a
    /// lock would stand, and the cards have nothing to say.
    /// </summary>
    private static void Mapus_ReadsTheComingCardsInTheOrderTheyCome()
    {
        Section("boss / mapus reads the coming cards in the order they come");

        GameSession next = MapusPocket(5171, 8);
        next.CurrentRound.Deck.PutOnTopOfDraw(next.CreateCard(Bar(1), null));
        MapusBoss soon = StartMapus(next);
        Check(MapusHolds(soon, 3, 0), "the block that fits is the NEXT card: the pocket is locked",
            MapusCells(soon));
        Check(soon.LastSeal.AimedByCards, "aimed by the cards");
        Check(Math.Abs(soon.LastSeal.PlacedRowThreat - 2.0 / 3.0) < 0.001,
            "a line that goes off on the second of three turns is two thirds of a threat",
            soon.LastSeal.PlacedRowThreat.ToString("0.000"));

        GameSession buried = MapusPocket(5171, 8);
        buried.CurrentRound.Deck.PutOnTopOfDraw(buried.CreateCard(Bar(1), null));
        for (int i = 0; i < 3; i++)
        {
            buried.CurrentRound.Deck.PutOnTopOfDraw(buried.CreateCard(Bar(4), null));
        }
        MapusBoss late = StartMapus(buried);
        Check(!late.LastSeal.AimedByCards && late.LastSeal.PlacedRowThreat == 0.0,
            "the same block three cards down cannot come in time: the cards say nothing",
            late.LastSeal.PlacedRowThreat.ToString("0.000"));
        Check(MapusHolds(late, 3, 0), "so the board alone decides", MapusCells(late));

        // The boss only READS: the hand and both piles are as they were.
        GameSession a = MapusPocket(5172, 8);
        GameSession b = MapusPocket(5172, 8);
        string before = MeydanState(a.CurrentRound);
        MapusBoss first = StartMapus(a);
        Check(MeydanState(a.CurrentRound) == before,
            "measuring leaves the board's cubes, the hand and both piles untouched");
        MapusBoss second = StartMapus(b);
        Check(MapusCells(first) == MapusCells(second),
            "and the same state reaches the same lock", MapusCells(first));
    }

    /// <summary>
    /// A 6x6 board of one-cube cards: row 0 and column 2 are each one cube from full and share
    /// their gap, (2,0); row 4 is one cube from full on its own, at (5,4).
    /// </summary>
    private static GameSession MapusCrossing(int seed)
    {
        var session = NewSession(seed, 6, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;
        var cells = new List<GridPos>();
        for (int x = board.MinX; x < board.MinX + board.Width; x++)
        {
            if (x != 2)
            {
                cells.Add(new GridPos(x, 0));
            }
            if (x != 5)
            {
                cells.Add(new GridPos(x, 4));
            }
        }
        for (int y = board.MinY; y < board.MinY + board.Height; y++)
        {
            if (y != 0 && y != 4)
            {
                cells.Add(new GridPos(2, y));
            }
        }
        PaintBoard(round, session, CubeKind.Normal, cells.ToArray());
        return session;
    }

    /// <summary>A cell where a ROW and a COLUMN could both go off outranks one that is the last
    /// gap of a row alone: the two together pay more, so that is where the lock goes first.
    /// </summary>
    private static void Mapus_GoesFirstWhereARowAndAColumnCross()
    {
        Section("boss / mapus goes first where a row and a column cross");
        GameSession session = MapusCrossing(5164);
        GameBoard board = session.CurrentRound.Board;
        Check(board.RowGapCount(0) == 1 && board.ColumnGapCount(2) == 1
                && board.RowGapCount(4) == 1,
            "two rows and a column are each one cube from full",
            board.RowGapCount(0) + "/" + board.ColumnGapCount(2) + "/" + board.RowGapCount(4));

        MapusBoss boss = StartMapus(session);
        Check(MapusHolds(boss, 2, 0), "it locks where the two threats CROSS, not the lone one",
            MapusCells(boss));
        Check(boss.LastSeal.PlacedRowThreat > 0.99 && boss.LastSeal.PlacedColumnThreat > 0.99,
            "both lines through it could have gone off on the next turn",
            boss.LastSeal.PlacedRowThreat.ToString("0.00") + "/"
                + boss.LastSeal.PlacedColumnThreat.ToString("0.00"));
        MapusSeal seal = boss.LastSeal.Seals[0];
        Check(seal.RowHeldByTheSealAlone && seal.ColumnHeldByTheSealAlone,
            "and the View is told this one cell is holding both");
    }

    /// <summary>
    /// A line a standing lock already holds shut is worth nothing to the next one, so the second
    /// lock denies something ELSE. And when the first runs out its cell is open for a whole turn -
    /// then taken back, if the player did not use it.
    /// </summary>
    private static void Mapus_TwoLocksDenyTwoDifferentLines()
    {
        Section("boss / mapus: two locks deny two different lines");
        GameSession session = MapusCrossing(5164);
        RoundEngine round = session.CurrentRound;
        MapusBoss boss = StartMapus(session);

        boss.AfterTurnScored(FakeTurnFor(session, round));
        Check(boss.SealedCells.Count == 1 && !boss.LastSeal.Placed,
            "no new lock after one turn", MapusCells(boss));
        boss.AfterTurnScored(FakeTurnFor(session, round));
        Check(MapusHolds(boss, 2, 0) && MapusHolds(boss, 5, 4),
            "the second lock takes the other row - the crossing is already held",
            MapusCells(boss));

        boss.AfterTurnScored(FakeTurnFor(session, round));
        Check(!round.Board.IsSealed(new GridPos(2, 0)) && MapusHolds(boss, 5, 4),
            "the first runs out and the crossing is open again", MapusCells(boss));
        boss.AfterTurnScored(FakeTurnFor(session, round));
        Check(MapusHolds(boss, 2, 0) && MapusHolds(boss, 5, 4),
            "the player did not take the window, so it is taken back", MapusCells(boss));
    }

    /// <summary>
    /// THE GUARD. Even on the tightest rhythm there is - a lock a turn, each standing one turn -
    /// the cell a lock has just let go of is never the cell the same turn's pick takes, though it
    /// is the most dangerous cell on the board. A line denied for ever has no answer in it.
    /// </summary>
    private static void Mapus_ALiftedCellIsThePlayersForATurn()
    {
        Section("boss / mapus: a cell just let go sits out that turn's pick");
        GameSession session = MapusCrossing(5164);
        RoundEngine round = session.CurrentRound;
        var boss = new MapusBoss { TurnsBetweenSeals = 1, SealTurns = 1 };
        round.SetBoss(boss);
        boss.OnRoundStarted(new RoundContext(session, session.Rng, round));
        Check(MapusHolds(boss, 2, 0), "it starts on the crossing", MapusCells(boss));

        boss.AfterTurnScored(FakeTurnFor(session, round));
        Check(!round.Board.IsSealed(new GridPos(2, 0)),
            "its time is up and the crossing is NOT re-locked on the spot", MapusCells(boss));
        Check(MapusHolds(boss, 5, 4), "the new lock goes to the next worst place",
            MapusCells(boss));
        boss.AfterTurnScored(FakeTurnFor(session, round));
        Check(MapusHolds(boss, 2, 0) && !round.Board.IsSealed(new GridPos(5, 4)),
            "a turn later the crossing may be taken again", MapusCells(boss));
    }

    private static void Mapus_NeverTakesTheLastHole()
    {
        Section("boss / mapus never takes the last hole");
        var session = NewSession(5173, 6, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;
        var cells = new List<GridPos>();
        for (int x = board.MinX; x < board.MinX + board.Width; x++)
        {
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                if (!(y == 3 && (x == 1 || x == 4)))
                {
                    cells.Add(new GridPos(x, y));
                }
            }
        }
        PaintBoard(round, session, CubeKind.Normal, cells.ToArray());

        MapusBoss boss = StartMapus(session);
        Check(boss.SealedCells.Count == 1, "two cells free: one may be locked", MapusCells(boss));
        boss.AfterTurnScored(FakeTurnFor(session, round));
        boss.AfterTurnScored(FakeTurnFor(session, round));
        Check(boss.SealedCells.Count == 1 && !boss.LastSeal.Placed,
            "the second lock is due, but the last free cell is never taken", MapusCells(boss));
        Check(boss.TurnsToNextSeal == 0, "it stays owed", "next in " + boss.TurnsToNextSeal);
    }

    /// <summary>Real turns, real cards: whatever the round does, the board's seals are exactly the
    /// boss's locks, never more than two, and never on a cube.</summary>
    private static void Mapus_StaysInStepWithTheBoardThroughRealTurns()
    {
        Section("boss / mapus stays in step with the board through real turns");
        var session = NewSession(5174, 7, 1000000, 40, 1, 2, 3, 4);
        RoundEngine round = session.CurrentRound;
        MapusBoss boss = StartMapus(session);
        bool inStep = true;
        bool neverOnACube = true;
        int most = 0;
        int turns = 0;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        for (; turns < 24 && round.Status == RoundStatus.InProgress; turns++)
        {
            bool played = false;
            for (int i = 0; i < round.Hand.Count && !played; i++)
            {
                List<GridPos> origins = round.GetValidOrigins(round.Hand[i].Shape);
                if (origins.Count > 0)
                {
                    round.PlayFromHand(i, origins[(turns * 7) % origins.Count]);
                    played = true;
                }
            }
            if (!played)
            {
                break;
            }
            most = Math.Max(most, boss.SealedCells.Count);
            inStep &= round.Board.SealedCells.Count == boss.SealedCells.Count;
            foreach (GridPos cell in boss.SealedCells)
            {
                inStep &= round.Board.IsSealed(cell);
                neverOnACube &= !round.Board.GetCube(cell).HasValue;
            }
        }
        timer.Stop();
        Console.WriteLine("        " + turns + " real turns under Mapus: " + timer.ElapsedMilliseconds
            + " ms, most locks at once " + most);
        Check(turns >= 8, "the round was actually played", "turns " + turns);
        Check(inStep, "the board's seals are the boss's locks, turn after turn");
        Check(neverOnACube, "and no lock ever stands on a cube");
        Check(most == 2, "one or two stand at a time, never more", "most " + most);
    }

    /// <summary>
    /// The per-turn report is not saved, the locks are. So a loaded run has locks on the board and
    /// no report to draw them from - and ReportFor writes one from the locks as they stand, with
    /// nothing "placed" in it, so the View shows the prisons without replaying anything.
    /// </summary>
    private static void Mapus_ALoadedRunStillHasALockReport()
    {
        Section("boss / mapus: a loaded run still has a report of its locks");
        GameSession session = MapusCrossing(5164);
        RoundEngine round = session.CurrentRound;
        MapusBoss boss = StartMapus(session);
        boss.AfterTurnScored(FakeTurnFor(session, round));
        boss.AfterTurnScored(FakeTurnFor(session, round));
        Check(boss.SealedCells.Count == 2, "two locks stand", MapusCells(boss));
        Check(ReferenceEquals(boss.ReportFor(round.Board), boss.LastSeal),
            "with a turn's report in hand, that is the one handed over");

        // What a load leaves behind: the state, and no report.
        System.Reflection.FieldInfo field = typeof(MapusBoss).GetField(
            "<LastSeal>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Check(field != null && field.IsDefined(typeof(NotSavedAttribute), false),
            "the report is marked as not saved");
        field.SetValue(boss, null);

        MapusSealVisuals rebuilt = boss.ReportFor(round.Board);
        Check(rebuilt != null && rebuilt.Seals.Count == 2, "a report is written from the locks",
            rebuilt == null ? "null" : "seals " + rebuilt.Seals.Count);
        Check(!rebuilt.Placed && rebuilt.Expired.Count == 0 && !rebuilt.Seals[1].IsNew,
            "with nothing placed and nothing expired in it - nothing to replay");
        Check(rebuilt.Seals[0].TurnsLeft == 1 && rebuilt.Seals[1].TurnsLeft == 3,
            "and each lock's own turns left",
            rebuilt.Seals[0].TurnsLeft + "/" + rebuilt.Seals[1].TurnsLeft);
    }
}
