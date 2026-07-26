// PURPOSE: "İstilacı" - the invader. One column of the arena is marked for demolition; three
// turns later everything standing in it is swept away and the player is billed for every cube
// that was in it. Then the next column is marked, and it begins again.
//
// The threat is a DEADLINE ON A PLACE, which is what makes it different from every other
// harasser: nothing is taken from you now, so the whole round is spent deciding whether that
// column is worth building in for three more turns. Filling it is not punished by the loss of
// the cubes - it is punished by the bill, which grows with exactly how much you left there.
//
// Three rulings the implementation carries:
//  - SEQUENTIAL, never overlapping. One column at a time, and the next is chosen only once the
//    last has been taken, so the player is never asked to watch two clocks.
//  - THE DEMOLITION IS NOT DESTRUCTION IN THE SCORING SENSE. Nothing it takes pays anything,
//    counts toward a clean sweep or feeds a ledger - the same terms as shuffle erosion and
//    Buldozer - and nothing resists it, obsidian and gold included. It is the arena being
//    cleared, not blocks being broken.
//  - THE BILL OBEYS THE TURN FLOOR. It goes through RoundEngine.ChargeScore from the end-of-turn
//    hook, so it can empty the turn that fed the column but can never push the round backwards.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"İstilacı" - a column is marked, and three turns later it is taken.</summary>
    public sealed class IstilaciBoss : BossRound
    {
        /// <summary>Turns between marking a column and demolishing it.</summary>
        public int FuseTurns = 3;

        /// <summary>Score charged per cube standing in the column when it goes. BALANCE
        /// PLACEHOLDER, logical (the global ScoreScale lifts it like every other value).</summary>
        public int PenaltyPerCube = 3;

        private bool hasMark;
        private int markedColumn;
        private int turnsLeft;
        private int columnsTaken;
        private int cubesTaken;

        private readonly List<int> candidateColumns = new List<int>();

        public IstilaciBoss()
            : base("istilaci", "İstilacı")
        {
            SetDescription(
                "One column is marked for demolition. Three turns later everything in it is "
                    + "swept away and you are charged for every cube that was standing there - "
                    + "then the next column is marked.",
                "Bir sütun yıkım için işaretlenir. Üç tur sonra içindeki her şey silinip "
                    + "süpürülür ve orada duran her küp için puan ödersin - ardından sıradaki "
                    + "sütun işaretlenir.");
        }

        /// <summary>The column under the mark, in absolute board coordinates. Meaningless
        /// while HasMark is false - the View reads both.</summary>
        public int MarkedColumn
        {
            get { return markedColumn; }
        }

        public bool HasMark
        {
            get { return hasMark; }
        }

        /// <summary>Turns before the marked column is taken.</summary>
        public int TurnsLeft
        {
            get { return turnsLeft; }
        }

        /// <summary>Columns demolished so far this round, for the UI.</summary>
        public int ColumnsTaken
        {
            get { return columnsTaken; }
        }

        /// <summary>Cubes the invader has taken this round, for the UI.</summary>
        public int CubesTaken
        {
            get { return cubesTaken; }
        }

        public override string StatusText
        {
            get
            {
                if (!hasMark)
                {
                    return Loc.Pick("choosing a column", "sütun seçiyor");
                }
                return Loc.Pick("column ", "sütun ") + markedColumn
                    + Loc.Pick(" in ", " - ") + turnsLeft + Loc.Pick(" turns", " tur");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            hasMark = false;
            columnsTaken = 0;
            cubesTaken = 0;
            // Asked now, but an empty arena has no column worth invading and Mark says so, so in
            // practice the first column is chosen at the end of the first turn that leaves
            // something standing.
            Mark(ctx.Round, ctx.Rng);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (!hasMark)
            {
                Mark(turn.Round, turn.Rng);
                return; // a column marked this turn gets its full fuse, not a turn less
            }
            turnsLeft--;
            if (turnsLeft > 0)
            {
                return;
            }
            Demolish(turn.Round);
            Mark(turn.Round, turn.Rng);
        }

        /// <summary>Puts the mark on a random column that actually holds something. Does nothing
        /// while the arena is empty - there would be nothing to threaten.</summary>
        private void Mark(RoundEngine round, IRandomSource rng)
        {
            hasMark = false;
            GameBoard board = round != null ? round.Board : null;
            if (board == null)
            {
                return;
            }
            candidateColumns.Clear();
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                if (ColumnCubeCount(board, x) > 0)
                {
                    candidateColumns.Add(x);
                }
            }
            if (candidateColumns.Count == 0)
            {
                return;
            }
            markedColumn = candidateColumns[rng.NextInt(0, candidateColumns.Count)];
            turnsLeft = FuseTurns;
            hasMark = true;
        }

        /// <summary>Takes the marked column: everything in it goes, scorelessly, and the player
        /// pays for what was there.</summary>
        private void Demolish(RoundEngine round)
        {
            hasMark = false;
            GameBoard board = round != null ? round.Board : null;
            if (board == null)
            {
                return;
            }
            var doomed = new List<GridPos>();
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                var cell = new GridPos(markedColumn, y);
                if (board.IsInside(cell) && board.GetCube(cell).HasValue)
                {
                    doomed.Add(cell);
                }
            }
            if (doomed.Count == 0)
            {
                columnsTaken++;
                return;
            }
            // countsForSweep: false and forced: true - the same terms as erosion. It pays
            // nothing, counts toward no sweep, feeds no ledger, and nothing resists it.
            int taken = round.DestroyCubes(doomed, false, true).Count;
            columnsTaken++;
            cubesTaken += taken;
            round.ChargeScore(taken * PenaltyPerCube, DefId);
        }

        private static int ColumnCubeCount(GameBoard board, int x)
        {
            int count = 0;
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                var cell = new GridPos(x, y);
                if (board.IsInside(cell) && board.GetCube(cell).HasValue)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
