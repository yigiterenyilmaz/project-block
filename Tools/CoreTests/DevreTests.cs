// PURPOSE: "Devre" (2026-10-02): the circuit NEVER leaves the arena. The arena can shrink under a
// standing circuit mid-round - the rim eroding, a deflate, "Tamagotchi" eating cells - and a
// circuit with a cell off the board can never be completed and is drawn hanging off the edge. The
// traced route is SQUEEZED into what is left at once (DevreJoker.Squeeze, through
// Joker.OnBoardReshaped), unfolds back when the ground returns, survives a save, and a circuit
// that cannot be squeezed in at all is withdrawn and traced afresh inside the board.

using System.Collections.Generic;
using ProjectBlock.Core;

public static partial class JokerTests
{
    private static void RunDevreTests()
    {
        Devre_SqueezePullsTheRouteIntoAShrunkBoard();
        Devre_RimErosionKeepsTheCircuitInside();
        Devre_ADeflateSqueezesItAtOnceAndAnInflateUnfoldsIt();
        Devre_AnEatenCellWithdrawsItAndANewOneIsTracedInside();
        Devre_TheSqueezedCircuitSurvivesASave();
    }

    /// <summary>What is wrong with a circuit on this board, or null when nothing is: every cell
    /// real play area, one straight step apart, never doubling back along its axis, and winding.
    /// With <paramref name="edgeToEdge"/> it must also start and end on the board's two edges.</summary>
    private static string CircuitFault(IReadOnlyList<GridPos> path, bool horizontal,
        GameBoard board, bool edgeToEdge)
    {
        if (path.Count == 0)
        {
            return "empty";
        }
        bool wound = false;
        for (int i = 0; i < path.Count; i++)
        {
            if (!board.IsInside(path[i]))
            {
                return "cell " + path[i].X + "," + path[i].Y + " is off the board";
            }
            if (i == 0)
            {
                continue;
            }
            int dx = path[i].X - path[i - 1].X;
            int dy = path[i].Y - path[i - 1].Y;
            if (System.Math.Abs(dx) + System.Math.Abs(dy) != 1)
            {
                return "a jump at " + i;
            }
            if ((horizontal ? dx : dy) < 0)
            {
                return "doubles back at " + i;
            }
            if ((horizontal ? dy : dx) != 0)
            {
                wound = true;
            }
        }
        if (!wound)
        {
            return "a plain row or column";
        }
        if (edgeToEdge)
        {
            int lo = horizontal ? board.MinX : board.MinY;
            int hi = lo + (horizontal ? board.Width : board.Height) - 1;
            int first = horizontal ? path[0].X : path[0].Y;
            int last = horizontal ? path[path.Count - 1].X : path[path.Count - 1].Y;
            if (first != lo || last != hi)
            {
                return "runs " + first + ".." + last + ", the board " + lo + ".." + hi;
            }
        }
        return null;
    }

    private static List<GridPos> Transposed(IReadOnlyList<GridPos> cells)
    {
        var flipped = new List<GridPos>();
        for (int i = 0; i < cells.Count; i++)
        {
            flipped.Add(new GridPos(cells[i].Y, cells[i].X));
        }
        return flipped;
    }

    /// <summary>A circuit with a session that has one standing, or null when the seed never
    /// traced one.</summary>
    private static DevreJoker ArmedDevre(GameSession session)
    {
        var joker = (DevreJoker)session.Jokers.Add(new DevreJoker());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        PlayTurns(session, joker.MaxArmTurn + 2);
        return joker.HasCircuit ? joker : null;
    }

    private static void Devre_SqueezePullsTheRouteIntoAShrunkBoard()
    {
        Section("devre / the squeeze pulls a route into a shrunk board, cell by cell");
        // A left-to-right route on a 7x7 that dips to the bottom row and climbs to row 2.
        var route = new List<GridPos>
        {
            new GridPos(0, 0),
            new GridPos(1, 0), new GridPos(1, 1), new GridPos(1, 2),
            new GridPos(2, 2),
            new GridPos(3, 2), new GridPos(3, 1), new GridPos(3, 0),
            new GridPos(4, 0),
            new GridPos(5, 0), new GridPos(5, 1),
            new GridPos(6, 1)
        };
        var full = new GameBoard(7, 7);
        Check(SameCells(DevreJoker.Squeeze(route, true, full), route),
            "on the board it was traced on, the route comes back exactly as it is");

        // The rim's second step: the bottom row and the left column go.
        GameBoard shrunk = GameBoard.CreateResized(full, -1, 0, -1, 0);
        List<GridPos> squeezed = DevreJoker.Squeeze(route, true, shrunk);
        var expected = new List<GridPos>
        {
            new GridPos(1, 1), new GridPos(1, 2),
            new GridPos(2, 2),
            new GridPos(3, 2), new GridPos(3, 1),
            new GridPos(4, 1),
            new GridPos(5, 1),
            new GridPos(6, 1)
        };
        Check(SameCells(squeezed, expected),
            "the left step is dropped and every bend in the lost row is pulled onto row 1");
        Check(squeezed != null && CircuitFault(squeezed, true, shrunk, true) == null,
            "and it is still one winding line from the new left edge to the right one",
            squeezed == null ? "null" : CircuitFault(squeezed, true, shrunk, true));

        // The same thing with the axes swapped, so a top-to-bottom circuit is not a second case.
        List<GridPos> down = DevreJoker.Squeeze(Transposed(route), false,
            GameBoard.CreateResized(full, -1, 0, -1, 0));
        Check(SameCells(down, Transposed(expected)), "a vertical circuit squeezes the same way");

        // Squeezed flat: only row 1 is left, so the circuit would be a plain row.
        GameBoard sliver = GameBoard.CreateResized(full, 0, 0, -1, -5);
        Check(DevreJoker.Squeeze(route, true, sliver) == null,
            "a route the squeeze would flatten into a plain row is refused");

        // An eaten cell on the route: there is no way to complete it any more.
        var bitten = new GameBoard(7, 7);
        bitten.MarkDead(new[] { new GridPos(3, 1) });
        Check(DevreJoker.Squeeze(route, true, bitten) == null,
            "a route over an eaten cell is refused");
    }

    private static void Devre_RimErosionKeepsTheCircuitInside()
    {
        Section("devre / the rim erodes under a standing circuit and it stays on the board");
        int circuits = 0;
        int squeezed = 0;
        int unfolded = 0;
        string fault = null;
        bool keptAtRoundStart = true;
        for (int seed = 7200; seed < 7240 && fault == null; seed++)
        {
            GameSession session = NewErodingSession(seed, 7, 40, ShuffleErosion.FromOutside, 1);
            DevreJoker joker = ArmedDevre(session);
            if (joker == null)
            {
                continue;
            }
            circuits++;
            RoundEngine round = session.CurrentRound;
            var traced = new List<GridPos>(joker.Path);
            int widthBefore = round.Board.Width;
            for (int step = 0; step < 6 && fault == null; step++)
            {
                round.DebugForceDeckRecycle();
                if (round.Loss != null)
                {
                    break;
                }
                if (joker.HasCircuit)
                {
                    fault = CircuitFault(joker.Path, joker.PathIsHorizontal, round.Board, true);
                }
            }
            if (fault != null || !joker.HasCircuit || round.Board.Width == widthBefore)
            {
                continue;
            }
            squeezed++;
            // A round boundary on the same (shrunk) board keeps the squeezed circuit rather
            // than rerolling it: it is the same circuit, laid on the arena as it is.
            var before = new List<GridPos>(joker.Path);
            joker.OnRoundStarted(new RoundContext(session, session.Rng, round));
            if (!joker.HasCircuit || !SameCells(joker.Path, before))
            {
                keptAtRoundStart = false;
            }
            // Overtime gives the eroded rim back: the circuit unfolds with the ground.
            round.RestoreErodedArena();
            if (joker.HasCircuit && SameCells(joker.Path, traced))
            {
                unfolded++;
            }
        }
        Check(fault == null, "every circuit stays on the board, connected, edge to edge, winding",
            fault ?? "");
        Check(circuits > 20, "over many seeds", "circuits " + circuits);
        Check(squeezed > 5, "and many of them were really squeezed by a smaller arena",
            "squeezed " + squeezed);
        Check(keptAtRoundStart, "a squeezed circuit is carried into the next round, not rerolled");
        Check(unfolded == squeezed,
            "and when overtime gives the rim back, each unfolds to exactly what was traced",
            unfolded + " of " + squeezed);
    }

    private static void Devre_ADeflateSqueezesItAtOnceAndAnInflateUnfoldsIt()
    {
        Section("devre / a deflate between turns squeezes it at once; the ground coming back "
            + "unfolds it");
        int tried = 0;
        int unfolded = 0;
        int survived = 0;
        string fault = null;
        for (int seed = 7300; seed < 7330 && fault == null; seed++)
        {
            GameSession session = NewSession(seed, 7, 1000000, 40, 1);
            DevreJoker joker = ArmedDevre(session);
            if (joker == null)
            {
                continue;
            }
            tried++;
            RoundEngine round = session.CurrentRound;
            var traced = new List<GridPos>(joker.Path);
            bool horizontal = joker.PathIsHorizontal;

            // Between turns, no turn resolved after it: the circuit must already be inside.
            round.ShrinkBoardPushingInward(1, 1, 1, 1);
            if (!joker.HasCircuit)
            {
                continue; // squeezed flat - withdrawn, which another test covers
            }
            survived++;
            fault = CircuitFault(joker.Path, horizontal, round.Board, true);
            if (fault != null)
            {
                break;
            }
            round.ReshapeBoard(1, 1, 1, 1);
            if (joker.HasCircuit && SameCells(joker.Path, traced))
            {
                unfolded++;
            }
        }
        Check(fault == null, "after the deflate every cell is on the board, with no turn played",
            fault ?? "");
        Check(survived > 10, "for most circuits", survived + " of " + tried);
        Check(unfolded == survived,
            "and the inflate gives each of them back exactly as it was traced",
            unfolded + " of " + survived);
    }

    private static void Devre_AnEatenCellWithdrawsItAndANewOneIsTracedInside()
    {
        Section("devre / a cell eaten off the circuit withdraws it, and the next one avoids the "
            + "dead ground");
        GameSession session = NewSession(7350, 7, 1000000, 40, 1);
        DevreJoker joker = ArmedDevre(session);
        if (joker == null)
        {
            Check(false, "no circuit was traced");
            return;
        }
        RoundEngine round = session.CurrentRound;
        GridPos victim = joker.Path[joker.Path.Count / 2];
        round.EatCellsForGood(new[] { victim });
        Check(round.Board.IsDead(victim), "the cell is dead");
        Check(!joker.HasCircuit, "a circuit that can never be completed is withdrawn at once");

        PlayTurns(session, 3);
        Check(joker.HasCircuit, "a new circuit is traced within a turn or two");
        if (joker.HasCircuit)
        {
            string fault = CircuitFault(joker.Path, joker.PathIsHorizontal, round.Board, true);
            Check(fault == null, "on the board as it now stands, around the eaten cell",
                fault ?? "");
        }
    }

    private static void Devre_TheSqueezedCircuitSurvivesASave()
    {
        Section("devre / a squeezed circuit is the same circuit after a save");
        for (int seed = 7400; seed < 7430; seed++)
        {
            GameSession session = NewSession(seed, 7, 1000000, 40, 1);
            DevreJoker joker = ArmedDevre(session);
            if (joker == null)
            {
                continue;
            }
            var traced = new List<GridPos>(joker.Path);
            session.CurrentRound.ShrinkBoardPushingInward(1, 1, 1, 1);
            if (!joker.HasCircuit || SameCells(joker.Path, traced))
            {
                continue;
            }
            var squeezed = new List<GridPos>(joker.Path);

            GameSession back = SaveGame.Load(SaveGame.Save(session), new GameConfig());
            DevreJoker loaded = null;
            foreach (Joker held in back.Jokers.Jokers)
            {
                if (held is DevreJoker) { loaded = (DevreJoker)held; }
            }
            Check(loaded != null && loaded.HasCircuit, "the circuit came back");
            if (loaded == null)
            {
                return;
            }
            Check(SameCells(loaded.Path, squeezed),
                "squeezed into the loaded arena exactly as it was before the save",
                squeezed.Count + " -> " + loaded.Path.Count);
            back.CurrentRound.ReshapeBoard(1, 1, 1, 1);
            Check(SameCells(loaded.Path, traced),
                "and the route it was traced as was saved too, so it still unfolds");
            return;
        }
        Check(false, "no seed gave a circuit the deflate squeezed");
    }
}
