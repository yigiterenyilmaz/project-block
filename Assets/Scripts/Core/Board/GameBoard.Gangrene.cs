// PURPOSE: The board side of "Kangren" (boss round) - the infection that spreads across the arena,
// and the DEAD LINES it leaves behind.
//
// TWO STATES, and the difference is the whole mechanic:
//   - A GANGRENE CUBE is an ordinary destructible cube in every respect. It fills a cell, it
//     blocks a clean sweep, and a completed line takes it out like anything else. Three gangrene
//     cubes in a row of five still leave a line you can finish with a two-block and blow up.
//   - A DEAD LINE is a row or column the infection took ENTIRELY. That line can never explode
//     again, for the rest of the round, whatever ends up standing in it. Deliberately permanent,
//     exactly like a cell the erosion clock ate.
//
// And when a line dies, the infection JUMPS: every cube standing in the board's nearest edge line
// (top or bottom for a row, left or right for a column) turns gangrene too. That can kill the edge
// line in turn, so it cascades - which is why InfectFullLines loops until nothing more changes.
//
// Note what it does NOT do: the jump converts cubes, it never creates them. An empty edge cell
// stays empty. That is what keeps the cascade finite and the board recoverable.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class GameBoard
    {
        /// <summary>Rows the infection took entirely. They can never explode again this round.</summary>
        private readonly List<int> infectionDeadRows = new List<int>();
        private readonly List<int> infectionDeadColumns = new List<int>();

        /// <summary>Absolute row indices the infection killed, for the UI.</summary>
        public IReadOnlyList<int> InfectionDeadRows
        {
            get { return infectionDeadRows; }
        }

        public IReadOnlyList<int> InfectionDeadColumns
        {
            get { return infectionDeadColumns; }
        }

        /// <summary>True if the infection has taken this row whole. Read by ResolveFullLines.</summary>
        public bool RowIsInfectionDead(int absoluteY)
        {
            return infectionDeadRows.Contains(absoluteY);
        }

        public bool ColumnIsInfectionDead(int absoluteX)
        {
            return infectionDeadColumns.Contains(absoluteX);
        }

        /// <summary>Gangrene cubes standing right now - what the boss bills the player for.</summary>
        public int CountGangrene()
        {
            int count = 0;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (cells[x, y].HasValue && cells[x, y].Value.Kind == CubeKind.Gangrene)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Spreads the infection by one cell and returns where it went, or null when there is
        /// nowhere left. With no gangrene on the board yet it SEEDS at a random playable cell;
        /// after that it only ever spreads to a cell TOUCHING what it already holds, so the
        /// infection is one growing patch from one random origin rather than a scatter of spots.
        ///
        /// It takes the cell whether it is empty or occupied: an empty cell becomes a gangrene
        /// cube, an occupied one is converted where it stands. Cubes nothing can break (obsidian,
        /// gold, a void trap, a parasite host) are the one thing it cannot touch.
        /// </summary>
        internal GridPos? SpreadGangrene(IRandomSource rng)
        {
            if (rng == null)
            {
                return null;
            }
            var candidates = new List<GridPos>();
            bool anyGangrene = CountGangrene() > 0;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var cell = new GridPos(x + MinX, y + MinY);
                    if (!CanBeInfected(x, y))
                    {
                        continue;
                    }
                    if (!anyGangrene || TouchesGangrene(x, y))
                    {
                        candidates.Add(cell);
                    }
                }
            }
            if (candidates.Count == 0)
            {
                return null;
            }
            GridPos target = candidates[rng.NextInt(0, candidates.Count)];
            SetGangreneAt(target);
            return target;
        }

        /// <summary>Can the infection take this cell? Play area, not already gangrene, and not
        /// holding something nothing can break.</summary>
        private bool CanBeInfected(int x, int y)
        {
            if (!playable[x, y] || dead[x, y])
            {
                return false;
            }
            Cube? occupant = cells[x, y];
            if (!occupant.HasValue)
            {
                return true; // an empty cell simply grows a gangrene cube
            }
            return occupant.Value.Kind != CubeKind.Gangrene
                && CubeRules.IsExternallyDestructible(occupant.Value);
        }

        private bool TouchesGangrene(int x, int y)
        {
            return IsGangreneAt(x + 1, y) || IsGangreneAt(x - 1, y)
                || IsGangreneAt(x, y + 1) || IsGangreneAt(x, y - 1);
        }

        private bool IsGangreneAt(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height
                && cells[x, y].HasValue && cells[x, y].Value.Kind == CubeKind.Gangrene;
        }

        /// <summary>Puts a gangrene cube in an absolute cell, keeping OccupiedCount honest.
        /// A converted cube keeps its source card id, so anything counting a card's cubes still
        /// finds them.</summary>
        private void SetGangreneAt(GridPos absolute)
        {
            int x = absolute.X - MinX;
            int y = absolute.Y - MinY;
            Cube? occupant = cells[x, y];
            int sourceCardId = occupant.HasValue ? occupant.Value.SourceCardId : GangreneCardId;
            if (!occupant.HasValue)
            {
                OccupiedCount++;
            }
            cells[x, y] = new Cube(CubeKind.Gangrene, sourceCardId);
        }

        /// <summary>Source card id stamped on a gangrene cube that grew out of nothing. Negative,
        /// so it can never collide with a real card and no card-counting effect claims it.</summary>
        public const int GangreneCardId = -7;

        /// <summary>
        /// Kills every line the infection now holds entirely, and lets it JUMP: each newly dead
        /// line turns every cube in the board's nearest edge line (top or bottom for a row, left or
        /// right for a column) to gangrene. That can kill the edge line too, so this loops until
        /// the board stops changing.
        ///
        /// Returns the cells the jump converted, for the UI. A tie in distance goes to the LOW edge
        /// (bottom / left) - an arbitrary choice, but a fixed one, so the same board always rots
        /// the same way.
        /// </summary>
        internal List<GridPos> InfectFullLines()
        {
            var converted = new List<GridPos>();
            // Bounded by the number of lines there are: every pass must kill at least one new line
            // to continue, and there are only Width + Height of them.
            for (int pass = 0; pass < Width + Height; pass++)
            {
                bool killedSomething = false;
                for (int y = 0; y < Height; y++)
                {
                    int absoluteY = y + MinY;
                    if (RowIsInfectionDead(absoluteY) || !RowIsAllGangrene(y))
                    {
                        continue;
                    }
                    infectionDeadRows.Add(absoluteY);
                    killedSomething = true;
                    // The infection jumps to whichever horizontal edge of the board is nearer.
                    int edgeY = y <= Height - 1 - y ? 0 : Height - 1;
                    ConvertRow(edgeY, converted);
                }
                for (int x = 0; x < Width; x++)
                {
                    int absoluteX = x + MinX;
                    if (ColumnIsInfectionDead(absoluteX) || !ColumnIsAllGangrene(x))
                    {
                        continue;
                    }
                    infectionDeadColumns.Add(absoluteX);
                    killedSomething = true;
                    int edgeX = x <= Width - 1 - x ? 0 : Width - 1;
                    ConvertColumn(edgeX, converted);
                }
                if (!killedSomething)
                {
                    break;
                }
            }
            return converted;
        }

        /// <summary>True when every cell of the row that could hold anything holds gangrene. A hole
        /// or an eroded cell is not part of the line, exactly as in ResolveFullLines. An entirely
        /// unusable row is NOT "all gangrene" - there is nothing there to rot.</summary>
        private bool RowIsAllGangrene(int y)
        {
            bool any = false;
            for (int x = 0; x < Width; x++)
            {
                if (!playable[x, y] || dead[x, y])
                {
                    continue;
                }
                if (!IsGangreneAt(x, y))
                {
                    return false;
                }
                any = true;
            }
            return any;
        }

        private bool ColumnIsAllGangrene(int x)
        {
            bool any = false;
            for (int y = 0; y < Height; y++)
            {
                if (!playable[x, y] || dead[x, y])
                {
                    continue;
                }
                if (!IsGangreneAt(x, y))
                {
                    return false;
                }
                any = true;
            }
            return any;
        }

        /// <summary>Turns every cube standing in a row to gangrene. Converts only - an empty cell
        /// stays empty, which is what keeps the cascade finite.</summary>
        private void ConvertRow(int y, List<GridPos> converted)
        {
            for (int x = 0; x < Width; x++)
            {
                if (CanBeInfected(x, y) && cells[x, y].HasValue)
                {
                    var cell = new GridPos(x + MinX, y + MinY);
                    SetGangreneAt(cell);
                    converted.Add(cell);
                }
            }
        }

        private void ConvertColumn(int x, List<GridPos> converted)
        {
            for (int y = 0; y < Height; y++)
            {
                if (CanBeInfected(x, y) && cells[x, y].HasValue)
                {
                    var cell = new GridPos(x + MinX, y + MinY);
                    SetGangreneAt(cell);
                    converted.Add(cell);
                }
            }
        }
    }
}
