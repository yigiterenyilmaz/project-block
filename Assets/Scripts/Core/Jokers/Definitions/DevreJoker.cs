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
// THE CIRCUIT NEVER LEAVES THE ARENA. The arena can shrink under it mid-round - erosion taking
// the rim, a deflate, "Tamagotchi" eating cells - and a circuit with a cell off the board can never
// be completed, and is drawn hanging off the edge. So two circuits are kept: the route as it was
// TRACED (saved), and the circuit as it stands on the board NOW, which is that route SQUEEZED into
// whatever the arena has become (Squeeze) - rebuilt on every reshape (Joker.OnBoardReshaped),
// every turn and after a load. When the arena grows back (the next round's fresh board) the same
// route unfolds back to what was traced. Only when even a squeeze cannot place it does the circuit
// go, and a new one is traced on the remaining board a turn later.
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

        /// <summary>The circuit as it was TRACED, in route order - on the board it was traced on,
        /// so after the arena shrinks some of it may lie off the board. Never played directly:
        /// everything reads <see cref="live"/>.</summary>
        private readonly List<GridPos> path = new List<GridPos>();

        /// <summary>The circuit as it stands on the board RIGHT NOW: the traced route squeezed
        /// into the arena as it is (Squeeze). The completion check, the break and the View all
        /// read this one. It is a pure function of the route and the board, rebuilt on every
        /// reshape and after a load, so it is not state and is not saved.</summary>
        [NotSaved]
        private readonly List<GridPos> live = new List<GridPos>();

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

        /// <summary>The circuit's cells IN ROUTE ORDER, for the UI to draw - as it stands on the
        /// board now, so every cell is play area however far the arena has shrunk. Empty when
        /// nothing is traced. Consecutive entries are always one cell apart, never diagonal.
        /// (Between a load in the market and the next round there is no board to fit it to, and
        /// this is the route as traced.)</summary>
        public IReadOnlyList<GridPos> Path
        {
            get { return live.Count > 0 ? live : path; }
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

        /// <summary>It keeps proc statistics, so the tooltip prints its count even at zero. That
        /// zero is the point for THIS joker in particular: a circuit has no deadline, so a player
        /// who has never managed to finish one has a card that looks busy - it traces, it waits,
        /// its status line counts cells - while having paid nothing all run.</summary>
        public override bool TracksProcs
        {
            get { return true; }
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
                return Path.Count + Loc.Pick(" cells", " kare");
            }
        }

        /// <summary>
        /// A NEW round does not mean a new circuit. One that is still standing is kept exactly
        /// where it is - the joker's promise is that it waits for you, and a circuit rerolled
        /// every round was really a one-round deadline wearing a different hat.
        ///
        /// It is laid on the new board the way it is laid on a shrunk one: the traced route,
        /// squeezed into the arena (Squeeze). On a fresh full arena that is the route itself, so a
        /// circuit the rim squeezed last round unfolds back to what was traced. Only a board it
        /// cannot be squeezed into at all ("Dört kutup" resizing the arena round a hole) forces a
        /// redraw - a circuit that cannot be completed is worse than no circuit at all.
        /// </summary>
        public override void OnRoundStarted(RoundContext ctx)
        {
            brokenThisRound = false;
            if (armed && Fit(ctx.Round.MainBoard))
            {
                return;
            }
            path.Clear();
            live.Clear();
            armed = false;
            int span = MaxArmTurn - MinArmTurn + 1;
            armOnTurn = MinArmTurn + (span > 1 ? ctx.Rng.NextInt(0, span) : 0);
        }

        /// <summary>The arena changed shape mid-round (erosion, a deflate, a "Tamagotchi" bite,
        /// an inflation): the circuit is squeezed into what is left of it at once, so the player
        /// never sees it - or is asked to fill it - off the board. Runs silenced or not, and
        /// draws nothing from the rng (see Joker.OnBoardReshaped).</summary>
        public override void OnBoardReshaped(RoundContext ctx)
        {
            if (!HasCircuit || ctx.Round == null)
            {
                return;
            }
            if (!Fit(ctx.Round.MainBoard))
            {
                Withdraw(ctx.Round.TurnNumber);
            }
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
            // Fitted again before it is judged, whatever the reshape hook already did: the
            // completion check must be asked of the circuit on THIS board, never of one that
            // runs off it - and a board that grew back since (overtime) unfolds it here.
            if (!Fit(turn.Round.MainBoard))
            {
                Withdraw(turn.Round.TurnNumber);
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
            if (live.Count == 0)
            {
                return false;
            }
            GameBoard board = turn.Round.Board;
            for (int i = 0; i < live.Count; i++)
            {
                GridPos cell = live[i];
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
            int cells = live.Count;

            IReadOnlyList<GridPos> blown = turn.Round.DestroyCubes(live, true);
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
            int perCube = cells * turn.Scoring.PointsPerCubeExploded;
            int circuitBonus = BreakBonus + cells * BonusPerCell;
            turn.AddFlatScore(perCube, DefId);
            turn.AddFlatScore(circuitBonus, DefId);
            // ONE proc for the BREAK - the joker's whole event, and its only one. Worth both
            // halves of what it just paid, because the per-cube rate is only paid here at all
            // BECAUSE the circuit broke: splitting it off would undercount what holding this
            // joker has actually been worth.
            NoteProc(perCube + circuitBonus, turn);
            path.Clear();
            live.Clear();
            armed = false;
        }

        /// <summary>Lays the circuit on <paramref name="board"/> as it stands: the traced route,
        /// squeezed into it. False when it cannot be placed there at all.</summary>
        private bool Fit(GameBoard board)
        {
            List<GridPos> squeezed = Squeeze(path, pathIsHorizontal, board);
            live.Clear();
            if (squeezed == null)
            {
                return false;
            }
            live.AddRange(squeezed);
            return true;
        }

        /// <summary>The arena changed under the circuit and it cannot be squeezed into what is
        /// left (a cell of it was eaten, or it would come out a plain row or column). A circuit
        /// that can never be completed is worse than none, so it goes - and a new one is traced on
        /// the board as it then stands at the end of the NEXT turn, where tracing always happens,
        /// because the reshape hook may not draw from the rng.</summary>
        private void Withdraw(int turnNumber)
        {
            path.Clear();
            live.Clear();
            armed = false;
            armOnTurn = turnNumber + 1;
        }

        /// <summary>
        /// The traced route, squeezed into <paramref name="board"/>. Along the circuit's own axis
        /// the steps that fell off the board are dropped, so it starts and ends on the new edges;
        /// across it, every cell is pulled in to the nearest row (or column) still there. A route
        /// is one contiguous run per step with neighbouring runs sharing a cell, and pulling every
        /// cell in by the same rule keeps both, so what comes out is still one unbroken line that
        /// never doubles back - only flatter where the rim took its bends. A route that already
        /// fits comes back exactly as it is.
        ///
        /// Null when it cannot be done: nothing of it is left, a cell of it lands on a hole or an
        /// eaten cell, or the squeeze flattened it into a plain row or column (which the game
        /// already explodes on its own, see TryTrace).
        /// </summary>
        internal static List<GridPos> Squeeze(IReadOnlyList<GridPos> route, bool horizontal,
            GameBoard board)
        {
            if (board == null || route == null || route.Count == 0)
            {
                return null;
            }
            int alongMin = horizontal ? board.MinX : board.MinY;
            int alongMax = alongMin + (horizontal ? board.Width : board.Height) - 1;
            int acrossMin = horizontal ? board.MinY : board.MinX;
            int acrossMax = acrossMin + (horizontal ? board.Height : board.Width) - 1;
            var squeezed = new List<GridPos>(route.Count);
            for (int i = 0; i < route.Count; i++)
            {
                int along = horizontal ? route[i].X : route[i].Y;
                if (along < alongMin || along > alongMax)
                {
                    continue;
                }
                int across = Clamp(horizontal ? route[i].Y : route[i].X, acrossMin, acrossMax);
                GridPos cell = horizontal ? new GridPos(along, across) : new GridPos(across, along);
                if (squeezed.Count > 0 && squeezed[squeezed.Count - 1].Equals(cell))
                {
                    continue; // two cells of a bend pulled onto the same one
                }
                if (!board.IsInside(cell))
                {
                    return null;
                }
                squeezed.Add(cell);
            }
            if (squeezed.Count == 0)
            {
                return null;
            }
            GridPos first = squeezed[0];
            GridPos last = squeezed[squeezed.Count - 1];
            int span = horizontal ? last.X - first.X + 1 : last.Y - first.Y + 1;
            if (squeezed.Count <= span)
            {
                return null; // one cell per step: it never leaves its lane any more
            }
            return squeezed;
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
            live.Clear();
            live.AddRange(traced);
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
