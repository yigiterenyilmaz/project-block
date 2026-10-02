// PURPOSE: "Rüzgar" (2026-10-02): a gust drawn across the board at any angle, three cells wide,
// nearly corner to corner at most. Fire in it throws embers that set SOME blocks ahead alight
// (never an empty cell), water is pushed until it hits something and then falls, an infection is
// carried to the next block downwind, and a gust that would touch nothing is refused.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;

public static partial class JokerTests
{
    private static void RunRuzgarTests()
    {
        Ruzgar_TheGustIsThreeCellsWideAtAnyAngle();
        Ruzgar_EmbersOnlyBurnBlocksAhead();
        Ruzgar_EmbersAreTheSameForTheSameSeed();
        Ruzgar_WaterIsPushedUntilItHitsSomething();
        Ruzgar_PushedWaterFallsAndPutsOutAFire();
        Ruzgar_CarriesAnInfectionToTheNextBlockDownwind();
        Ruzgar_RefusesAGustThatTouchesNothing();
    }

    private static ActivationTarget Gust(float fx, float fy, float tx, float ty)
    {
        return ActivationTarget.Swipe(new BoardStroke(fx, fy, tx, ty));
    }

    private static bool KindAt(GameBoard board, int x, int y, CubeKind kind)
    {
        Cube? cube = board.GetCube(new GridPos(x, y));
        return cube.HasValue && cube.Value.Kind == kind;
    }

    private static void Ruzgar_TheGustIsThreeCellsWideAtAnyAngle()
    {
        Section("rüzgar / the band is three cells wide, starts on a cell centre, and is capped");
        var session = NewSession(9900, 7, 1000000, 40, 1);
        GameBoard board = session.CurrentRound.Board;

        WindGust flat = WindGust.From(board, new BoardStroke(0.3f, 3.2f, 6f, 3f));
        Check(flat.Valid, "a stroke across the board is a wind");
        Check(flat.StartX == 0f && flat.StartY == 3f, "it starts on the centre of the cell pressed",
            flat.StartX + "," + flat.StartY);
        var rows = new HashSet<int>();
        foreach (GridPos cell in flat.Cells)
        {
            rows.Add(cell.Y);
        }
        Check(rows.Count == 3 && rows.Contains(2) && rows.Contains(3) && rows.Contains(4),
            "a level gust covers its own row and one either side", string.Join(",", rows));
        Check(flat.Cells.Count == 21, "seven cells along, three across", "" + flat.Cells.Count);

        float max = WindGust.MaxLengthFor(board);
        Check(Math.Abs(max - 0.9f * (float)Math.Sqrt(72)) < 0.001f,
            "the longest gust is nearly the corner-to-corner diagonal", "" + max);
        WindGust diagonal = WindGust.From(board, new BoardStroke(6f, 6f, 0f, 0f));
        Check(Math.Abs(diagonal.Length - max) < 0.001f, "a stroke past it is cut to it",
            "" + diagonal.Length);
        Check(diagonal.Covers(new GridPos(6, 6)) && diagonal.Covers(new GridPos(1, 1)),
            "a diagonal runs from the top-right corner nearly to the bottom-left one");
        Check(!diagonal.Covers(new GridPos(0, 0)), "nearly - the last corner is just out of reach");
        Check(diagonal.Covers(new GridPos(4, 6)) && !diagonal.Covers(new GridPos(3, 6)),
            "and it is three cells wide measured ACROSS it, not along a row");

        WindGust angled = WindGust.From(board, new BoardStroke(0f, 0f, 6f, 2.5f));
        Check(angled.Valid && angled.DirY > 0.3f && angled.DirY < 0.45f,
            "any angle is a direction, not only the eight", angled.DirX + "," + angled.DirY);

        Check(!WindGust.From(board, new BoardStroke(3f, 3f, 3.4f, 3.2f)).Valid,
            "a tap is not a wind");
    }

    private static void Ruzgar_EmbersOnlyBurnBlocksAhead()
    {
        Section("rüzgar / embers land on blocks ahead of the fire, never on empty ground");
        var session = NewSession(9901, 7, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;
        ClearBoard(board);
        var power = (RuzgarPower)session.Powers.Add(new RuzgarPower());
        power.EmbersPerFire = 6;
        power.EmberCatchPercent = 100;
        board.SetCubeAt(new GridPos(2, 3), new Cube(CubeKind.Fire, 1));
        board.SetCubeAt(new GridPos(0, 3), new Cube(CubeKind.Normal, 2)); // behind the fire
        board.SetCubeAt(new GridPos(4, 3), new Cube(CubeKind.Normal, 3)); // ahead
        board.SetCubeAt(new GridPos(5, 2), new Cube(CubeKind.Normal, 4)); // ahead, in the band
        board.SetCubeAt(new GridPos(3, 3), new Cube(CubeKind.Obsidian, 5)); // stone does not burn
        board.SetCubeAt(new GridPos(5, 6), new Cube(CubeKind.Normal, 6)); // outside the band

        Check(session.Powers.TryUse(power.InstanceId, Gust(0f, 3f, 6f, 3f)), "the gust blows");
        WindVisuals gust = power.LastGust;
        Check(gust != null && gust.Embers.Count == 6, "the fire threw its embers",
            gust == null ? "no report" : "" + gust.Embers.Count);
        Check(gust.Ignitions.Count > 0, "and some of them set a block alight");
        foreach (SpreadIgnition lit in gust.Ignitions)
        {
            Check((lit.Cell.X == 4 && lit.Cell.Y == 3) || (lit.Cell.X == 5 && lit.Cell.Y == 2),
                "every block lit is a plain block ahead of the fire, in the band",
                lit.Cell.X + "," + lit.Cell.Y);
            Check(lit.Was.Kind == CubeKind.Normal, "and the report keeps the face it had");
        }
        Check(KindAt(board, 0, 3, CubeKind.Normal), "the block behind the fire did not catch");
        Check(KindAt(board, 3, 3, CubeKind.Obsidian), "the stone did not catch");
        Check(KindAt(board, 5, 6, CubeKind.Normal), "the block outside the band did not catch");
        Check(KindAt(board, 2, 3, CubeKind.Fire), "the fire itself is still burning");
        int occupied = board.GetOccupiedCells().Count;
        Check(occupied == 6, "no ember ever made a fire out of empty ground", "" + occupied);
        Check(!power.Charged, "and the use spent the charge");
    }

    private static int[] EmberRun(int seed)
    {
        var session = NewSession(seed, 7, 1000000, 40, 1);
        GameBoard board = session.CurrentRound.Board;
        ClearBoard(board);
        var power = (RuzgarPower)session.Powers.Add(new RuzgarPower());
        board.SetCubeAt(new GridPos(1, 3), new Cube(CubeKind.Fire, 1));
        board.SetCubeAt(new GridPos(1, 2), new Cube(CubeKind.Fire, 1));
        for (int x = 2; x < 7; x++)
        {
            board.SetCubeAt(new GridPos(x, 2), new Cube(CubeKind.Normal, 10 + x));
            board.SetCubeAt(new GridPos(x, 4), new Cube(CubeKind.Normal, 20 + x));
        }
        session.Powers.TryUse(power.InstanceId, Gust(0f, 3f, 6f, 3f));
        var lit = new List<int>();
        foreach (SpreadIgnition ignition in power.LastGust.Ignitions)
        {
            lit.Add(ignition.Cell.X * 10 + ignition.Cell.Y);
        }
        return lit.ToArray();
    }

    private static void Ruzgar_EmbersAreTheSameForTheSameSeed()
    {
        Section("rüzgar / where the embers land is random, and the run's own random");
        int[] first = EmberRun(9902);
        int[] second = EmberRun(9902);
        Check(string.Join(",", first) == string.Join(",", second),
            "the same run lights the same blocks", string.Join(",", first) + " / " + string.Join(",", second));
        Check(first.Length > 0 && first.Length < 10, "and lights SOME of what is ahead, not all of it",
            "" + first.Length);
    }

    private static void Ruzgar_WaterIsPushedUntilItHitsSomething()
    {
        Section("rüzgar / water slides the way the wind blows until it hits something");
        var session = NewSession(9903, 7, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;
        ClearBoard(board);
        var power = (RuzgarPower)session.Powers.Add(new RuzgarPower());
        // Both standing on something, so gravity has nothing to add: one on the floor with a
        // block in its way past the end of the band, one on a shelf with a block in its lane.
        board.SetCubeAt(new GridPos(1, 0), new Cube(CubeKind.Water, 1));
        board.SetCubeAt(new GridPos(5, 0), new Cube(CubeKind.Normal, 2));
        for (int x = 0; x < 6; x++)
        {
            board.SetCubeAt(new GridPos(x, 1), new Cube(CubeKind.Normal, 10 + x)); // the shelf
        }
        board.SetCubeAt(new GridPos(0, 2), new Cube(CubeKind.Water, 3));
        board.SetCubeAt(new GridPos(4, 2), new Cube(CubeKind.Normal, 4));

        Check(session.Powers.TryUse(power.InstanceId, Gust(0f, 1f, 2.5f, 1f)), "the gust blows");
        Check(KindAt(board, 4, 0, CubeKind.Water) && !board.GetCube(new GridPos(1, 0)).HasValue,
            "the water slid on past the end of the band and stopped against the block");
        Check(KindAt(board, 3, 2, CubeKind.Water) && !board.GetCube(new GridPos(0, 2)).HasValue,
            "the water on the shelf was stopped by the block in its own lane");
        Check(power.LastGust.Pushes.Count == 2, "both pushes are reported",
            "" + power.LastGust.Pushes.Count);
        Check(round.ExternalWaterFrames.Count >= 3, "and the slide is handed to the View as frames",
            "" + round.ExternalWaterFrames.Count);
    }

    private static void Ruzgar_PushedWaterFallsAndPutsOutAFire()
    {
        Section("rüzgar / pushed water still falls, and puts out a fire it ends up beside");
        var session = NewSession(9904, 7, 1000000, 40, 1);
        GameBoard board = session.CurrentRound.Board;
        ClearBoard(board);
        var power = (RuzgarPower)session.Powers.Add(new RuzgarPower());
        board.SetCubeAt(new GridPos(0, 5), new Cube(CubeKind.Water, 1));
        board.SetCubeAt(new GridPos(4, 0), new Cube(CubeKind.Fire, 2));

        Check(session.Powers.TryUse(power.InstanceId, Gust(0f, 5f, 4f, 5f)), "the gust blows");
        Check(KindAt(board, 6, 0, CubeKind.Water),
            "the water crossed to the wall and then fell to the floor");

        var second = NewSession(9905, 7, 1000000, 40, 1);
        GameBoard floor = second.CurrentRound.Board;
        ClearBoard(floor);
        var gust = (RuzgarPower)second.Powers.Add(new RuzgarPower());
        floor.SetCubeAt(new GridPos(0, 0), new Cube(CubeKind.Water, 1));
        floor.SetCubeAt(new GridPos(4, 0), new Cube(CubeKind.Fire, 2));
        Check(second.Powers.TryUse(gust.InstanceId, Gust(0f, 0f, 2f, 0f)), "a gust along the floor");
        Check(KindAt(floor, 3, 0, CubeKind.Water), "the water stopped against the fire");
        Check(KindAt(floor, 4, 0, CubeKind.Obsidian), "and the fire beside it went out to stone");
    }

    private static void Ruzgar_CarriesAnInfectionToTheNextBlockDownwind()
    {
        Section("rüzgar / an infection rides the gust to the next block downwind");
        var session = NewSession(9906, 7, 1000000, 40, 1);
        RoundEngine round = session.CurrentRound;
        GameBoard board = round.Board;
        ClearBoard(board);
        var joker = (EnfeksiyonJoker)session.Jokers.Add(new EnfeksiyonJoker());
        session.Jokers.DispatchRoundStarted(round);
        var power = (RuzgarPower)session.Powers.Add(new RuzgarPower());
        board.SetCubeAt(new GridPos(1, 3), new Cube(CubeKind.Normal, 1));
        board.SetCubeAt(new GridPos(4, 3), new Cube(CubeKind.Normal, 2));
        board.SetCubeAt(new GridPos(6, 3), new Cube(CubeKind.Normal, 3));
        Check(session.Jokers.TryActivate(joker.InstanceId, ActivationTarget.Board(new GridPos(1, 3))),
            "the cube is infected");
        Check(joker.InfectedCells.Count == 1, "one infection");

        Check(session.Powers.TryUse(power.InstanceId, Gust(0f, 3f, 6f, 3f)),
            "a gust over nothing but an infection still blows - it has something to carry");
        var cells = new HashSet<GridPos>();
        foreach (InfectedCell cell in joker.InfectedCells)
        {
            cells.Add(cell.Cell);
        }
        Check(cells.Contains(new GridPos(1, 3)), "the source keeps its infection");
        Check(cells.Contains(new GridPos(4, 3)), "and the first block downwind caught it");
        Check(!cells.Contains(new GridPos(6, 3)), "only the first - it is one more use, not a plague");
        Check(power.LastGust.Gust.Carries.Count == 1, "the carry is reported for the View");
    }

    private static void Ruzgar_RefusesAGustThatTouchesNothing()
    {
        Section("rüzgar / a gust that would touch nothing is refused and keeps its charge");
        var session = NewSession(9907, 7, 1000000, 40, 1);
        GameBoard board = session.CurrentRound.Board;
        ClearBoard(board);
        var power = (RuzgarPower)session.Powers.Add(new RuzgarPower());
        board.SetCubeAt(new GridPos(3, 3), new Cube(CubeKind.Normal, 1));
        Check(!session.Powers.TryUse(power.InstanceId, Gust(0f, 3f, 6f, 3f)),
            "plain blocks alone: nothing rides the wind");
        board.SetCubeAt(new GridPos(5, 3), new Cube(CubeKind.Fire, 2));
        Check(!session.Powers.TryUse(power.InstanceId, Gust(0f, 3f, 6f, 3f)),
            "a fire with nothing ahead of it to burn: still nothing");
        board.SetCubeAt(new GridPos(6, 0), new Cube(CubeKind.Water, 3));
        Check(!session.Powers.TryUse(power.InstanceId, Gust(0f, 0f, 6f, 0f)),
            "water already against the wall it is blown at: nothing");
        Check(!session.Powers.TryUse(power.InstanceId, Gust(5f, 3f, 5.3f, 3f)), "a tap: nothing");
        Check(power.Charged, "and the charge is still there");
        Check(session.Powers.TryUse(power.InstanceId, Gust(6f, 3f, 0f, 3f)),
            "turned the other way, the fire has a block ahead and the gust blows");
    }
}
