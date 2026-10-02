// PURPOSE: Overtime puts the eroded arena back (2026-10-02). The rim shuffle erosion ate grows
// back on every side it lost - each cell as what it was, empty - and the dead zone lifts, the
// moment the player continues into overtime. Nothing erodes it again there: past the threshold a
// dry draw pile is a loss, never a recycle.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;

public static partial class JokerTests
{
    private static void RunOvertimeArenaTests()
    {
        OvertimeArena_TheRimGrowsBackWhenThePlayerContinues();
        OvertimeArena_AdvancingLeavesTheBoardAlone();
        OvertimeArena_TheDeadZoneLiftsWhenThePlayerContinues();
        OvertimeArena_HolesAndBonusGroundComeBackAsWhatTheyWere();
        OvertimeArena_TheMemorySurvivesASave();
    }

    private static void OvertimeArena_TheRimGrowsBackWhenThePlayerContinues()
    {
        Section("overtime arena / the eroded rim grows back, empty, where it was");
        var session = NewErodingSession(7301, 5, 40, ShuffleErosion.FromOutside, 1);
        RoundEngine round = session.CurrentRound;
        int minX = round.Board.MinX;
        int minY = round.Board.MinY;
        for (int i = 0; i < 4; i++)
        {
            round.DebugForceDeckRecycle(); // two free, then top+right, then bottom+left
        }
        Check(round.Board.Width == 3 && round.Board.Height == 3, "eroded to 3x3 first",
            round.Board.Width + "x" + round.Board.Height);
        Check(round.HasErodedRim, "and the round knows the rim is owed back");

        var kept = new GridPos(minX + 2, minY + 2);
        round.Board.SetCubeAt(kept, new Cube(CubeKind.Normal, 9301));
        int erosions = round.BoardErosionCount;

        Check(round.DebugEnterOvertime(), "the round went into overtime");
        Check(round.ThresholdPassed && round.Status == RoundStatus.InProgress,
            "and is being played on", round.Status.ToString());
        Check(round.Board.Width == 5 && round.Board.Height == 5, "the arena is 5x5 again",
            round.Board.Width + "x" + round.Board.Height);
        Check(round.Board.MinX == minX && round.Board.MinY == minY, "exactly where it started",
            "min " + round.Board.MinX + "," + round.Board.MinY);
        Check(round.Board.PlayableCellCount == 25 && round.Board.DeadCellCount == 0,
            "every cell is play area again", round.Board.PlayableCellCount + " playable");
        Check(round.Board.GetCube(kept).HasValue, "a cube the erosion spared is still there");
        Check(round.Board.OccupiedCount == 1, "and the regrown ground came back empty",
            round.Board.OccupiedCount + " occupied");
        Check(!round.HasErodedRim, "nothing is owed any more");
        Check(round.BoardErosionCount == erosions, "the clock's history is left as it was");

        GameBoard before = round.Board;
        Check(!round.RestoreErodedArena() && round.Board == before,
            "asked again (a later continue), it finds nothing more to give back");
    }

    private static void OvertimeArena_AdvancingLeavesTheBoardAlone()
    {
        Section("overtime arena / taking the market instead changes nothing");
        var session = NewErodingSession(7302, 5, 40, ShuffleErosion.FromOutside, 1);
        RoundEngine round = session.CurrentRound;
        for (int i = 0; i < 3; i++)
        {
            round.DebugForceDeckRecycle();
        }
        round.AddScoreOutsideTurn(1000000);
        Check(round.Status == RoundStatus.AwaitingAdvanceDecision, "the advance offer is up",
            round.Status.ToString());
        Check(round.Board.Width == 4, "the board is still eroded while the offer stands",
            round.Board.Width + "x" + round.Board.Height);
        round.DecideAdvance(true);
        Check(round.Board.Width == 4, "and advancing does not touch it");
    }

    private static void OvertimeArena_TheDeadZoneLiftsWhenThePlayerContinues()
    {
        Section("overtime arena / the dead zone lifts when the player continues");
        var session = NewErodingSession(7303, 7, 40, ShuffleErosion.FromCenter, 1);
        RoundEngine round = session.CurrentRound;
        for (int i = 0; i < 4; i++)
        {
            round.DebugForceDeckRecycle();
        }
        Check(round.Board.BlightedCellCount == 9, "a 3x3 dead zone first",
            round.Board.BlightedCellCount.ToString());
        Check(round.DebugEnterOvertime(), "the round went into overtime");
        Check(round.Board.BlightedCellCount == 0, "and the dead zone is gone",
            round.Board.BlightedCellCount.ToString());
        Check(round.Board.Width == 7 && round.Board.PlayableCellCount == 49,
            "the arena itself was never touched");
    }

    private static void OvertimeArena_HolesAndBonusGroundComeBackAsWhatTheyWere()
    {
        Section("overtime arena / a hole comes back a hole and bonus ground comes back optional");
        var session = NewErodingSession(7304, 5, 40, ShuffleErosion.FromOutside, 1);
        RoundEngine round = session.CurrentRound;
        // "Tılsım"-style bonus ground off the right edge: the bounding box grows to 6x5, with ONE
        // optional cell in its new column and holes above and below it.
        var bonus = new GridPos(5, 2);
        round.GrantBonusGround(new[] { bonus });
        Check(round.Board.Width == 6 && round.Board.IsOptional(bonus), "a 6x5 box with bonus ground",
            round.Board.Width + "x" + round.Board.Height);
        int playable = round.Board.PlayableCellCount;

        for (int i = 0; i < 4; i++)
        {
            round.DebugForceDeckRecycle(); // the bonus column goes with the first step
        }
        Check(!round.Board.IsInside(bonus), "the erosion took the bonus column");

        Check(round.DebugEnterOvertime(), "the round went into overtime");
        Check(round.Board.Width == 6 && round.Board.Height == 5, "the 6x5 box is back",
            round.Board.Width + "x" + round.Board.Height);
        Check(round.Board.IsOptional(bonus), "the bonus cell is bonus ground again");
        Check(!round.Board.IsInside(new GridPos(5, 0)) && !round.Board.IsInside(new GridPos(5, 4)),
            "the holes beside it are holes again, not new ground");
        Check(round.Board.PlayableCellCount == playable, "not one cell more or less than before",
            round.Board.PlayableCellCount + " vs " + playable);
    }

    private static void OvertimeArena_TheMemorySurvivesASave()
    {
        Section("overtime arena / what the rim lost survives a save");
        var session = NewErodingSession(7305, 5, 40, ShuffleErosion.FromOutside, 1);
        RoundEngine round = session.CurrentRound;
        for (int i = 0; i < 4; i++)
        {
            round.DebugForceDeckRecycle();
        }
        GameSession loaded = SaveGame.Load(SaveGame.Save(session), new GameConfig());
        RoundEngine back = loaded.CurrentRound;
        Check(back.HasErodedRim && back.Board.Width == 3, "the loaded round is still eroded",
            back.Board.Width + "x" + back.Board.Height);
        Check(back.DebugEnterOvertime(), "and goes into overtime");
        Check(back.Board.Width == 5 && back.Board.Height == 5
            && back.Board.MinX == 0 && back.Board.MinY == 0, "where it grows the whole rim back",
            back.Board.Width + "x" + back.Board.Height + " at " + back.Board.MinX + "," + back.Board.MinY);
    }
}
