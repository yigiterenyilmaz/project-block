// PURPOSE: "Devre" - a winding circuit is traced across the board at a random moment, and stays
// there across rounds until it is completed. Fill every cell of it and the circuit BREAKS: those
// cubes blow up and the joker pays a bonus on top.
//
// THE PATH. It runs from one edge to the OPPOSITE edge and is monotone along that axis: on a
// left-to-right circuit it winds up and down as much as it likes but never doubles back to the
// left. That is what keeps it honest - a wandering path could cross itself, could be arbitrarily
// long, and could not be checked for reachability. Concretely it is built column by column: each
// column holds one contiguous vertical run, and consecutive runs touch, so the whole thing is a
// single 4-connected line. A vertical circuit is the same construction with the axes swapped.
//
// CONFIRMED RULES:
//  - one circuit AT A TIME, laid at a random turn (not at round start - a circuit on an empty
//    board is a chore, not a challenge). A circuit still standing when the round ends is CARRIED
//    OVER to the next one rather than redrawn: it is a standing offer with no deadline, and
//    rerolling it every round quietly turned "no deadline" into "one round";
//  - NO deadline. It waits until it is completed or the round ends, which is exactly what keeps
//    it different from "Meydan Okuma";
//  - completing it explodes the cubes on it. That is a real destruction: it goes through
//    RoundEngine.DestroyCubes, counts toward a clean sweep and toward "Kayıt defteri", and pays
//    the normal per-cube explosion rate before the circuit bonus is added.
//
// A cell counts as filled if it is filled NOW or was filled this turn and has already blown up.
// Without that, a placement that completed the circuit AND a row would lose the circuit to its
// own line clear - the player did the work either way.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Devre" - trace the circuit, break it, get paid.</summary>
    public sealed class DevreJoker : Joker
    {
        /// <summary>Earliest and latest turn the circuit can appear on. Drawn once per round.</summary>
        public int MinArmTurn = 2;
        public int MaxArmTurn = 6;

        /// <summary>How far the circuit may wander perpendicular to its axis between one step
        /// along it and the next. 0 would be a straight line.</summary>
        public int MaxWind = 2;

        /// <summary>Flat bonus for breaking the circuit, on top of the normal explosion score.
        /// Rebalanced 2026-09-06 from 120: a typical circuit on a 7x7 board is 10-14 cells, so
        /// the old pair paid around 230 logical - a third of a mid-run threshold out of one
        /// joker, before joker multipliers touched it.</summary>
        public int BreakBonus = 40;

        /// <summary>Extra bonus per cell of the circuit - a longer circuit is worth more.
        /// Rebalanced 2026-09-06 from 8.</summary>
        public int BonusPerCell = 3;

        private const int GenerationAttempts = 12;

        private readonly List<GridPos> path = new List<GridPos>();
        private int armOnTurn;
        private bool armed;
        private bool brokenThisRound;
        private bool pathIsHorizontal;

        public DevreJoker()
            : base("devre", "Devre")
        {
            SetDescription(
                "A winding circuit is traced from one edge of the board to the other. Fill "
                    + "every cell of it and the circuit breaks: those blocks explode and you are "
                    + "paid a bonus. It waits as long as it takes - a circuit you do not finish "
                    + "is still there next round.",
                "Oyun alanının bir kenarından diğerine kıvrımlı bir devre çizilir. Devrenin "
                    + "bütün karelerini doldurursan devre kırılır: o bloklar patlar ve ekstra "
                    + "puan alırsın. Ne kadar sürerse sürsün bekler - bitiremediğin devre "
                    + "sonraki rauntta da yerinde durur.");
        }

        /// <summary>The circuit's cells IN ROUTE ORDER, for the UI to draw. Empty when nothing is
        /// traced. Consecutive entries are always one cell apart, never diagonal.</summary>
        public IReadOnlyList<GridPos> Path
        {
            get { return path; }
        }

        /// <summary>True when the circuit runs left-to-right, false when it runs top-to-bottom.
        /// That axis is the one it may never double back along.</summary>
        public bool PathIsHorizontal
        {
            get { return pathIsHorizontal; }
        }

        /// <summary>True while a circuit is on the board waiting to be completed.</summary>
        public bool HasCircuit
        {
            get { return armed && !brokenThisRound; }
        }

        /// <summary>True once this round's circuit has been broken.</summary>
        public bool BrokenThisRound
        {
            get { return brokenThisRound; }
        }

        public override string StatusText
        {
            get
            {
                if (brokenThisRound)
                {
                    return Loc.Pick("broken", "kırıldı");
                }
                if (!armed)
                {
                    return Loc.Pick("tracing...", "çiziliyor...");
                }
                return path.Count + Loc.Pick(" cells", " kare");
            }
        }

        /// <summary>
        /// A NEW round does not mean a new circuit. One that is still standing is kept exactly
        /// where it is - the joker's promise is that it waits for you, and a circuit rerolled
        /// every round was really a one-round deadline wearing a different hat.
        ///
        /// The one thing that forces a redraw is the board changing under it: a boss that
        /// resizes the arena ("Dört kutup"), or erosion from the round just played, can leave a
        /// cell of the path off the board, and a circuit that cannot be completed is worse than
        /// no circuit at all.
        /// </summary>
        public override void OnRoundStarted(RoundContext ctx)
        {
            brokenThisRound = false;
            if (armed && PathFitsBoard(ctx.Round.Board))
            {
                return;
            }
            path.Clear();
            armed = false;
            int span = MaxArmTurn - MinArmTurn + 1;
            armOnTurn = MinArmTurn + (span > 1 ? ctx.Rng.NextInt(0, span) : 0);
        }

        /// <summary>True when every cell of the standing circuit is still on the board.</summary>
        private bool PathFitsBoard(GameBoard board)
        {
            if (board == null || path.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < path.Count; i++)
            {
                if (!board.IsInside(path[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (brokenThisRound)
            {
                return;
            }
            if (!armed)
            {
                if (turn.Round.TurnNumber >= armOnTurn)
                {
                    Trace(turn.Round.Board, turn.Rng);
                }
                return;
            }
            if (!IsComplete(turn))
            {
                return;
            }
            Break(turn);
        }

        /// <summary>Every cell of the circuit holds a cube - or held one this turn and has
        /// already exploded, which counts just the same (see the file header).</summary>
        private bool IsComplete(TurnContext turn)
        {
            if (path.Count == 0)
            {
                return false;
            }
            GameBoard board = turn.Round.Board;
            for (int i = 0; i < path.Count; i++)
            {
                GridPos cell = path[i];
                if (board.GetCube(cell).HasValue)
                {
                    continue;
                }
                if (!WasDestroyedThisTurn(turn, cell))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool WasDestroyedThisTurn(TurnContext turn, GridPos cell)
        {
            IReadOnlyList<DestroyedCube> destroyed = turn.Report.DestroyedCubes;
            for (int i = 0; i < destroyed.Count; i++)
            {
                if (destroyed[i].Pos.X == cell.X && destroyed[i].Pos.Y == cell.Y)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Breaks the circuit: the cubes still standing on it explode through the engine
        /// (so the log, the sweep pre-condition and "Kayıt defteri" all stay right), the normal
        /// per-cube rate is paid for them, and the circuit bonus goes on top.</summary>
        private void Break(TurnContext turn)
        {
            brokenThisRound = true;
            int cells = path.Count;

            IReadOnlyList<GridPos> blown = turn.Round.DestroyCubes(path, true);
            // Reported for the view only - it has no other way to know this happened, and
            // nothing in Core reads it back. Scoring below is untouched.
            turn.Report.AddCircuitExplodedCells(blown);
            if (blown.Count > 0)
            {
                turn.Round.TryResolveCleanSweep();
            }
            // NOT gated by "Genel temizlik" (RoundEngine.ExternalDestructionScores): the circuit
            // only breaks when the PLAYER has filled every cell of it by placing blocks, so this
            // explosion is the player's own, like a completed line. The cubes that had already
            // gone this turn were part of the circuit too, so the whole circuit is paid for.
            turn.AddFlatScore(cells * turn.Scoring.PointsPerCubeExploded, DefId);
            turn.AddFlatScore(BreakBonus + cells * BonusPerCell, DefId);
            path.Clear();
            armed = false;
        }

        /// <summary>
        /// Traces a fresh circuit. Runs edge to edge along a random axis, monotone along it: each
        /// step advances one cell, and between steps the circuit may wander up to MaxWind cells
        /// sideways, filling in the run so the line stays connected.
        ///
        /// A board with holes in it (erosion, bolted-on cells) can defeat an attempt, so it tries
        /// a few times and gives up quietly - the joker simply tries again next turn.
        /// </summary>
        private void Trace(GameBoard board, IRandomSource rng)
        {
            for (int attempt = 0; attempt < GenerationAttempts; attempt++)
            {
                bool horizontal = rng.NextInt(0, 2) == 0;
                if (TryTrace(board, rng, horizontal))
                {
                    armed = true;
                    return;
                }
            }
        }

        private bool TryTrace(GameBoard board, IRandomSource rng, bool horizontal)
        {
            int alongCount = horizontal ? board.Width : board.Height;
            int acrossCount = horizontal ? board.Height : board.Width;
            if (alongCount < 1 || acrossCount < 1)
            {
                return false;
            }
            var traced = new List<GridPos>();
            int across = rng.NextInt(0, acrossCount);
            for (int along = 0; along < alongCount; along++)
            {
                // The last step lands where it stands: the circuit must finish ON the far edge,
                // not wander past it.
                // Drawn from the range that actually FITS rather than clamped into it. Clamping
                // pins the circuit to a wall: at the edge every out-of-range offset collapses
                // onto the same cell, so a circuit that touches the side tends to hug it and
                // come out as a plain straight line.
                int lo = across - MaxWind;
                int hi = across + MaxWind;
                if (lo < 0) { lo = 0; }
                if (hi > acrossCount - 1) { hi = acrossCount - 1; }
                int next = along == alongCount - 1
                    ? across
                    : lo + rng.NextInt(0, hi - lo + 1);
                // Walked in the direction of TRAVEL, not lowest-first: the list is the route in
                // order, so the UI can draw it as a line and every neighbouring pair really is
                // one step apart.
                int step = next >= across ? 1 : -1;
                for (int a = across; ; a += step)
                {
                    GridPos cell = horizontal
                        ? new GridPos(along + board.MinX, a + board.MinY)
                        : new GridPos(a + board.MinX, along + board.MinY);
                    if (!board.IsInside(cell))
                    {
                        return false; // the circuit would run through a hole - try again
                    }
                    traced.Add(cell);
                    if (a == next)
                    {
                        break;
                    }
                }
                across = next;
            }
            // A circuit that never left its lane is just a row or a column, which the game
            // already explodes on its own - reject it and trace another.
            if (traced.Count == 0 || traced.Count <= alongCount)
            {
                return false;
            }
            path.Clear();
            path.AddRange(traced);
            pathIsHorizontal = horizontal;
            return true;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
