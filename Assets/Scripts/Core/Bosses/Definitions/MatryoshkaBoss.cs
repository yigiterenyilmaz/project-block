// PURPOSE: "Matruşka" - the round where score stops mattering and the dolls are the whole game.
//
// A doll is set on one cube of the arena. Break that cube and the doll SPLITS: two smaller dolls
// are set down on two other cubes, at random. Break those and each splits again - 1, 2, 4, 8 -
// and the eighth generation splits no further, so cracking the last of the eight ENDS the round:
// the player banks the round's threshold and walks out.
//
// Nothing else in the round pays at all (SuppressesAllBaseScore), so the dolls are not a
// side-quest, they are the task. Two ways to fail it:
//
//  - EXPLODE A LINE WITH NO DOLL IN IT and the round is lost. Every clear has to be aimed at a
//    doll; clearing lines for their own sake is what loses this round.
//  - LEAVE THE DOLLS NOWHERE TO GO and the round is lost. A doll needs a cube to sit on, so
//    emptying the arena while eight dolls are still splitting is its own kind of defeat.
//
// The two failures pull against each other on purpose: you must keep clearing (to split the
// dolls) and you must keep building (to house them).
//
// A doll RIDES a cube rather than being one. It fills no cell, blocks nothing, and takes no part
// in a line - it is a mark on somebody else's cube, which is why an ordinary line clear is what
// cracks it. Round-scoped like every boss: the dolls die with the engine.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Matruşka" - break the dolls, in the order they make you.</summary>
    public sealed class MatruskaBoss : BossRound
    {
        /// <summary>How many times a doll may split. Four generations is 1 -> 2 -> 4 -> 8, and
        /// the eighth generation splits no further.</summary>
        public int Generations = 4;

        /// <summary>Dolls each broken one leaves behind.</summary>
        public int SplitInto = 2;

        /// <summary>One doll: the cube it rides and how many splits it has left.</summary>
        private struct Doll
        {
            public GridPos Cell;
            public int Generation;

            public Doll(GridPos cell, int generation)
            {
                Cell = cell;
                Generation = generation;
            }
        }

        private readonly List<Doll> dolls = new List<Doll>();
        private int dollsCracked;
        private bool started;

        public MatruskaBoss()
            : base("matruska", "Matruşka")
        {
            SetDescription(
                "A doll sits on one cube. Break that cube and it splits in two, again and again "
                    + "- 1, 2, 4, 8. Nothing scores this round: crack every doll and you pass it "
                    + "for the full threshold. Explode a line with NO doll in it and you lose - "
                    + "and so you do if a doll has no cube left to sit on.",
                "Bir küpün üstüne bir matruşka bebeği konur. O küp patlayınca bebek ikiye "
                    + "bölünür, tekrar tekrar - 1, 2, 4, 8. Bu raunt puan kazandırmaz: bütün "
                    + "bebekleri patlatırsan raundu eşik puanıyla geçersin. İçinde bebek OLMAYAN "
                    + "bir satır ya da sütun patlatırsan kaybedersin - bebeğe oturacak küp "
                    + "kalmazsa da öyle.");
        }

        /// <summary>Where the dolls are sitting, for the View to draw them on.</summary>
        public IReadOnlyList<GridPos> DollCells
        {
            get
            {
                var cells = new List<GridPos>(dolls.Count);
                for (int i = 0; i < dolls.Count; i++)
                {
                    cells.Add(dolls[i].Cell);
                }
                return cells;
            }
        }

        /// <summary>How many splits the doll on this cell has left, or 0 when there is none.
        /// The View sizes the doll by it, so a nearly-spent doll reads as a small one.</summary>
        public int GenerationsLeftAt(GridPos cell)
        {
            for (int i = 0; i < dolls.Count; i++)
            {
                if (dolls[i].Cell.X == cell.X && dolls[i].Cell.Y == cell.Y)
                {
                    return Generations - dolls[i].Generation + 1;
                }
            }
            return 0;
        }

        public int DollCount
        {
            get { return dolls.Count; }
        }

        public int DollsCracked
        {
            get { return dollsCracked; }
        }

        public override string StatusText
        {
            get
            {
                return dolls.Count + Loc.Pick(" dolls left", " bebek kaldı");
            }
        }

        /// <summary>Nothing pays by itself this round - the dolls are the only income, and they
        /// pay once, at the end, through DeclareRoundWon. A joker's own bonuses still land, as
        /// they do under every other score boss.</summary>
        public override bool SuppressesAllBaseScore
        {
            get { return true; }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            dolls.Clear();
            dollsCracked = 0;
            started = false;
            // The arena is empty at round start, so there is nothing to set a doll on yet: the
            // first one arrives at the end of the first turn that leaves a cube standing.
            SeedFirstDoll(ctx.Round, ctx.Rng);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            RoundEngine round = turn.Round;
            if (round == null)
            {
                return;
            }
            if (!started)
            {
                SeedFirstDoll(round, turn.Rng);
                return; // the first doll is not cracked by the turn that placed it
            }
            // 1. Was every line that went off aimed at a doll? Judged against the dolls as they
            //    stood BEFORE this turn's splits, which is exactly what the list still holds.
            if (AnyLineHadNoDoll(turn.Report))
            {
                round.DeclareLoss(LossReason.LineWithoutDoll);
                return; // a lost round is not also a won one - see DeclareRoundWon
            }
            // 2. Split every doll whose cube was destroyed this turn.
            if (!SplitBrokenDolls(round, turn))
            {
                return; // nowhere to put the halves: the dolls have overrun the board
            }
            // 3. A doll whose cube MOVED rather than broke (settling water) is re-homed instead
            //    of being cracked - nothing exploded under it.
            if (!ReHomeOrphans(round, turn.Rng))
            {
                return;
            }
            // 4. Nothing left to break: the round is over and it is won.
            if (dolls.Count == 0)
            {
                round.DeclareRoundWon();
            }
        }

        /// <summary>Sets the very first doll down, as soon as there is a cube to set it on.</summary>
        private void SeedFirstDoll(RoundEngine round, IRandomSource rng)
        {
            GameBoard board = round != null ? round.Board : null;
            if (board == null)
            {
                return;
            }
            GridPos? host = FreeHost(board, rng);
            if (!host.HasValue)
            {
                return;
            }
            dolls.Add(new Doll(host.Value, 1));
            started = true;
        }

        /// <summary>True if any row or column that exploded this turn held no doll at all.</summary>
        private bool AnyLineHadNoDoll(TurnReport report)
        {
            for (int i = 0; i < report.ExplodedRows.Count; i++)
            {
                if (!AnyDollInRow(report.ExplodedRows[i]))
                {
                    return true;
                }
            }
            for (int i = 0; i < report.ExplodedColumns.Count; i++)
            {
                if (!AnyDollInColumn(report.ExplodedColumns[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private bool AnyDollInRow(int y)
        {
            for (int i = 0; i < dolls.Count; i++)
            {
                if (dolls[i].Cell.Y == y)
                {
                    return true;
                }
            }
            return false;
        }

        private bool AnyDollInColumn(int x)
        {
            for (int i = 0; i < dolls.Count; i++)
            {
                if (dolls[i].Cell.X == x)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Cracks every doll whose cube was destroyed this turn and sets its halves down
        /// elsewhere. Returns false when there was nowhere to put them - the round is lost.</summary>
        private bool SplitBrokenDolls(RoundEngine round, TurnContext turn)
        {
            var broken = new List<Doll>();
            for (int i = dolls.Count - 1; i >= 0; i--)
            {
                if (WasDestroyed(turn.Report, dolls[i].Cell))
                {
                    broken.Add(dolls[i]);
                    dolls.RemoveAt(i);
                }
            }
            for (int i = 0; i < broken.Count; i++)
            {
                dollsCracked++;
                if (broken[i].Generation >= Generations)
                {
                    continue; // the smallest doll of all: it holds nothing, so nothing comes out
                }
                for (int half = 0; half < SplitInto; half++)
                {
                    GridPos? host = FreeHost(round.Board, turn.Rng);
                    if (!host.HasValue)
                    {
                        round.DeclareLoss(LossReason.NoRoomForDoll);
                        return false;
                    }
                    dolls.Add(new Doll(host.Value, broken[i].Generation + 1));
                }
            }
            return true;
        }

        /// <summary>Moves any doll left sitting on an empty cell back onto a real cube. Its cube
        /// was not destroyed (that was handled above) - it slid out from under it.</summary>
        private bool ReHomeOrphans(RoundEngine round, IRandomSource rng)
        {
            for (int i = 0; i < dolls.Count; i++)
            {
                if (round.Board.GetCube(dolls[i].Cell).HasValue)
                {
                    continue;
                }
                GridPos? host = FreeHost(round.Board, rng);
                if (!host.HasValue)
                {
                    round.DeclareLoss(LossReason.NoRoomForDoll);
                    return false;
                }
                dolls[i] = new Doll(host.Value, dolls[i].Generation);
            }
            return true;
        }

        private static bool WasDestroyed(TurnReport report, GridPos cell)
        {
            IReadOnlyList<DestroyedCube> gone = report.DestroyedCubes;
            if (gone == null)
            {
                return false;
            }
            for (int i = 0; i < gone.Count; i++)
            {
                if (gone[i].Pos.X == cell.X && gone[i].Pos.Y == cell.Y)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A random occupied cell with no doll on it already. Null when the arena has
        /// nothing left to offer - one doll per cube, so eight dolls need eight cubes.</summary>
        private GridPos? FreeHost(GameBoard board, IRandomSource rng)
        {
            var hosts = new List<GridPos>();
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                for (int y = board.MinY; y < board.MinY + board.Height; y++)
                {
                    var cell = new GridPos(x, y);
                    if (board.IsInside(cell) && board.GetCube(cell).HasValue && !HasDoll(cell))
                    {
                        hosts.Add(cell);
                    }
                }
            }
            if (hosts.Count == 0)
            {
                return null;
            }
            return hosts[rng.NextInt(0, hosts.Count)];
        }

        private bool HasDoll(GridPos cell)
        {
            for (int i = 0; i < dolls.Count; i++)
            {
                if (dolls[i].Cell.X == cell.X && dolls[i].Cell.Y == cell.Y)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
