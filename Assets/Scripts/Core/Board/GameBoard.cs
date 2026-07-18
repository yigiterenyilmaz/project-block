// PURPOSE: The closed play grid of one round. Owns placement validation, full
// row/column explosions, and the clean-sweep check. Pure state + rules, no scoring
// (scoring reads the results from RoundEngine/TurnReport).
// EXTENSION POINTS:
//  - Elemental behavior on explosion (fire chain, water flow...) belongs in
//    ResolveFullLines / a future post-placement resolution step; the loop already
//    consults CubeRules.IsDestructible per cube.
//  - Board resizing powers (yatay/dikey/hiper enflasyon) should create a new board
//    and copy cubes, or generalize this class - do NOT assume Width/Height are
//    round-constant elsewhere.
//  - Ghost blocks (partially outside the board) will need CanPlace to relax its
//    bounds check behind a rule flag.

using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectBlock.Core
{
    /// <summary>Result of resolving full rows/columns after a placement.</summary>
    public sealed class LineExplosionResult
    {
        public static readonly LineExplosionResult None = new LineExplosionResult(
            Array.Empty<int>(), Array.Empty<int>(), Array.Empty<GridPos>());

        public IReadOnlyList<int> Rows { get; }
        public IReadOnlyList<int> Columns { get; }
        public IReadOnlyList<GridPos> ExplodedCells { get; }

        public int LineCount
        {
            get { return Rows.Count + Columns.Count; }
        }

        public LineExplosionResult(IReadOnlyList<int> rows, IReadOnlyList<int> columns,
            IReadOnlyList<GridPos> explodedCells)
        {
            Rows = rows;
            Columns = columns;
            ExplodedCells = explodedCells;
        }
    }

    /// <summary>Closed rectangular grid the player places blocks on.</summary>
    public sealed class GameBoard
    {
        private readonly Cube?[,] cells;

        public int Width { get; }
        public int Height { get; }
        public int OccupiedCount { get; private set; }

        public GameBoard(int width, int height)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentException("Board must be at least 1x1.");
            }
            Width = width;
            Height = height;
            cells = new Cube?[width, height];
        }

        public bool IsInside(GridPos pos)
        {
            return pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;
        }

        public Cube? GetCube(GridPos pos)
        {
            return cells[pos.X, pos.Y];
        }

        /// <summary>True if every cell of the shape (anchored at origin) is inside and empty.</summary>
        public bool CanPlace(BlockShape shape, GridPos origin)
        {
            foreach (GridPos offset in shape.Cells)
            {
                GridPos pos = origin + offset;
                if (!IsInside(pos) || cells[pos.X, pos.Y].HasValue)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Places the card's cubes. Caller must have validated with CanPlace.</summary>
        public IReadOnlyList<GridPos> Place(BlockCard card, GridPos origin)
        {
            if (!CanPlace(card.Shape, origin))
            {
                throw new InvalidOperationException("Illegal placement of " + card + " at " + origin + ".");
            }
            var placed = new List<GridPos>(card.Shape.Size);
            foreach (GridPos offset in card.Shape.Cells)
            {
                GridPos pos = origin + offset;
                // Future: elemental cards will decide the CubeKind per cube here.
                cells[pos.X, pos.Y] = new Cube(CubeKind.Normal, card.Id);
                placed.Add(pos);
            }
            OccupiedCount += placed.Count;
            return placed;
        }

        /// <summary>Detects all full rows and columns, explodes their destructible cubes.</summary>
        public LineExplosionResult ResolveFullLines()
        {
            var fullRows = new List<int>();
            for (int y = 0; y < Height; y++)
            {
                bool full = true;
                for (int x = 0; x < Width; x++)
                {
                    if (!cells[x, y].HasValue)
                    {
                        full = false;
                        break;
                    }
                }
                if (full) fullRows.Add(y);
            }
            var fullColumns = new List<int>();
            for (int x = 0; x < Width; x++)
            {
                bool full = true;
                for (int y = 0; y < Height; y++)
                {
                    if (!cells[x, y].HasValue)
                    {
                        full = false;
                        break;
                    }
                }
                if (full) fullColumns.Add(x);
            }
            if (fullRows.Count == 0 && fullColumns.Count == 0)
            {
                return LineExplosionResult.None;
            }

            var exploded = new List<GridPos>();
            var seen = new HashSet<GridPos>(); // row/column intersections explode once
            foreach (int y in fullRows)
            {
                for (int x = 0; x < Width; x++)
                {
                    ExplodeCell(new GridPos(x, y), seen, exploded);
                }
            }
            foreach (int x in fullColumns)
            {
                for (int y = 0; y < Height; y++)
                {
                    ExplodeCell(new GridPos(x, y), seen, exploded);
                }
            }
            return new LineExplosionResult(fullRows, fullColumns, exploded);
        }

        private void ExplodeCell(GridPos pos, HashSet<GridPos> seen, List<GridPos> exploded)
        {
            if (!seen.Add(pos))
            {
                return;
            }
            Cube? cube = cells[pos.X, pos.Y];
            if (!cube.HasValue || !CubeRules.IsDestructible(cube.Value))
            {
                return;
            }
            cells[pos.X, pos.Y] = null;
            OccupiedCount--;
            exploded.Add(pos);
        }

        /// <summary>Clean-sweep check: no remaining cube that counts (obsidian/gold later won't).</summary>
        public bool IsCleanForSweep()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Cube? cube = cells[x, y];
                    if (cube.HasValue && CubeRules.CountsForCleanSweep(cube.Value))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>Is there at least one legal origin for this shape?</summary>
        public bool AnyPlacementExists(BlockShape shape)
        {
            for (int x = 0; x <= Width - shape.Width; x++)
            {
                for (int y = 0; y <= Height - shape.Height; y++)
                {
                    if (CanPlace(shape, new GridPos(x, y)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>All legal origins for this shape (used by UI previews and simulations).</summary>
        public List<GridPos> GetValidOrigins(BlockShape shape)
        {
            var origins = new List<GridPos>();
            for (int x = 0; x <= Width - shape.Width; x++)
            {
                for (int y = 0; y <= Height - shape.Height; y++)
                {
                    var origin = new GridPos(x, y);
                    if (CanPlace(shape, origin))
                    {
                        origins.Add(origin);
                    }
                }
            }
            return origins;
        }

        /// <summary>ASCII picture (top row first) for logs: '#' cube, '.' empty.</summary>
        public string ToAscii()
        {
            var sb = new StringBuilder();
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    sb.Append(cells[x, y].HasValue ? '#' : '.');
                }
                if (y > 0) sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
