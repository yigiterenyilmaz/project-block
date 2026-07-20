// PURPOSE: GameBoard queries & bookkeeping - cube writes/retyping, snapshots and
// restore, neighbours/edges, cube counts, explosion prediction, sweep/placement
// existence checks, and ASCII debug rendering.

using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectBlock.Core
{
    partial class GameBoard
    {
        /// <summary>Marks the cube at a cell as a Parazit host (sweep-exempt, immune to
        /// external destruction). Returns false if the cell is empty. The caller is expected
        /// to have confirmed it is the right cube.</summary>
        public bool SetCubeProtected(GridPos pos)
        {
            if (!IsInside(pos))
            {
                return false;
            }
            Cube? cube = cells[pos.X - MinX, pos.Y - MinY];
            if (!cube.HasValue)
            {
                return false;
            }
            cells[pos.X - MinX, pos.Y - MinY] = cube.Value.AsProtected();
            return true;
        }

        /// <summary>Writes a cube into a cell, replacing whatever was there. For effects that
        /// conjure cubes rather than place a card ("Bardağın boş tarafı" inverting the board,
        /// "Mayın" arming an empty cell).</summary>
        public void SetCubeAt(GridPos pos, Cube cube)
        {
            if (!IsInside(pos))
            {
                return;
            }
            if (!cells[pos.X - MinX, pos.Y - MinY].HasValue)
            {
                OccupiedCount++;
            }
            cells[pos.X - MinX, pos.Y - MinY] = cube;
        }

        /// <summary>Retypes an existing cube, keeping its source card ("Taskin" turning
        /// neighbours to water, "Yangin" to fire, "Buzluk" freezing water into ice).</summary>
        public bool SetCubeKind(GridPos pos, CubeKind kind)
        {
            if (!IsInside(pos))
            {
                return false;
            }
            Cube? cube = cells[pos.X - MinX, pos.Y - MinY];
            if (!cube.HasValue || cube.Value.Kind == kind)
            {
                return false;
            }
            cells[pos.X - MinX, pos.Y - MinY] = new Cube(kind, cube.Value.SourceCardId);
            return true;
        }

        /// <summary>Every cell holding a cube of this kind, in the same fixed order as
        /// GetOccupiedCells (determinism: joker effects pick from a stable list).</summary>
        public List<GridPos> CellsOfKind(CubeKind kind)
        {
            var found = new List<GridPos>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Cube? cube = cells[x, y];
                    if (cube.HasValue && cube.Value.Kind == kind)
                    {
                        found.Add(new GridPos(x + MinX, y + MinY));
                    }
                }
            }
            return found;
        }

        /// <summary>Puts the board back to a snapshot ("Kum saati" rewinding time). Only the
        /// cubes are restored - the shape of the board is not part of a snapshot, so a board
        /// that grew in between keeps its new cells (they simply come back empty).</summary>
        public void RestoreFrom(Dictionary<GridPos, Cube> snapshot)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    cells[x, y] = null;
                }
            }
            OccupiedCount = 0;
            foreach (KeyValuePair<GridPos, Cube> entry in snapshot)
            {
                if (!IsInside(entry.Key))
                {
                    continue; // the cell no longer exists on this board
                }
                cells[entry.Key.X - MinX, entry.Key.Y - MinY] = entry.Value;
                OccupiedCount++;
            }
        }

        /// <summary>Turns ghost traces into real play area and hands back the cells that were
        /// converted ("Tılsım"). The outside cubes themselves are dropped - the power explodes
        /// them - and the caller feeds the cells into the next board through RoundConfig.</summary>
        public List<GridPos> TakeOutsideCellsForConversion()
        {
            var converted = new List<GridPos>(outsideCubes.Keys);
            outsideCubes.Clear();
            return converted;
        }

        /// <summary>Copies the whole board into a map, for diffing what a turn destroyed.</summary>
        public void SnapshotInto(Dictionary<GridPos, Cube> target)
        {
            target.Clear();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Cube? cube = cells[x, y];
                    if (cube.HasValue)
                    {
                        target[new GridPos(x + MinX, y + MinY)] = cube.Value;
                    }
                }
            }
        }

        /// <summary>The 4-neighbourhood of a cell, clipped to the board.</summary>
        public List<GridPos> Neighbours(GridPos pos)
        {
            var found = new List<GridPos>(4);
            AddIfInside(found, new GridPos(pos.X + 1, pos.Y));
            AddIfInside(found, new GridPos(pos.X - 1, pos.Y));
            AddIfInside(found, new GridPos(pos.X, pos.Y + 1));
            AddIfInside(found, new GridPos(pos.X, pos.Y - 1));
            return found;
        }

        private void AddIfInside(List<GridPos> target, GridPos pos)
        {
            if (IsInside(pos))
            {
                target.Add(pos);
            }
        }

        /// <summary>True if the cell touches an outer wall - meaning at least one of its four
        /// neighbours is not play area. On an irregular board that includes the rim of every
        /// bolted-on piece, which is what "Buzluk" and "Çerçeve" both want.</summary>
        public bool IsOnEdge(GridPos pos)
        {
            if (!IsInside(pos))
            {
                return false;
            }
            return !IsInside(new GridPos(pos.X + 1, pos.Y))
                || !IsInside(new GridPos(pos.X - 1, pos.Y))
                || !IsInside(new GridPos(pos.X, pos.Y + 1))
                || !IsInside(new GridPos(pos.X, pos.Y - 1));
        }

        /// <summary>Every occupied cell, in a fixed left-to-right, bottom-to-top order.
        /// Deterministic on purpose: joker effects that pick a random cube must draw from a
        /// stable list, or replays break.</summary>
        public List<GridPos> GetOccupiedCells()
        {
            var occupied = new List<GridPos>(OccupiedCount);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (cells[x, y].HasValue)
                    {
                        occupied.Add(new GridPos(x + MinX, y + MinY));
                    }
                }
            }
            return occupied;
        }

        /// <summary>Does any cube of this card remain on the board? (piggy banks, fire...)</summary>
        public bool HasCubesOf(int cardId)
        {
            return CountCubesOf(cardId) > 0;
        }

        /// <summary>Cubes of this card remaining on (or hanging off) the board.</summary>
        public int CountCubesOf(int cardId)
        {
            int count = 0;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Cube? cube = cells[x, y];
                    if (cube.HasValue && cube.Value.SourceCardId == cardId)
                    {
                        count++;
                    }
                }
            }
            foreach (KeyValuePair<GridPos, Cube> entry in outsideCubes)
            {
                if (entry.Value.SourceCardId == cardId)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Number of cubes of a kind on the board (gold bonus...).</summary>
        public int CountCubesOfKind(CubeKind kind)
        {
            int count = 0;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Cube? cube = cells[x, y];
                    if (cube.HasValue && cube.Value.Kind == kind)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Pure query: which rows/columns WOULD become full if the shape were placed at
        /// origin. Never mutates the board - the UI calls this every frame to highlight
        /// the lines a placement would explode. Assumes the placement is legal.
        /// </summary>
        public LineExplosionResult PredictExplosions(BlockShape shape, GridPos origin)
        {
            var shapeCells = new HashSet<GridPos>();
            foreach (GridPos offset in shape.Cells)
            {
                shapeCells.Add(origin + offset);
            }
            var fullRows = new List<int>();
            for (int y = 0; y < Height; y++)
            {
                bool full = false;
                for (int x = 0; x < Width; x++)
                {
                    if (!playable[x, y])
                    {
                        continue;
                    }
                    if (!cells[x, y].HasValue && !shapeCells.Contains(new GridPos(x + MinX, y + MinY)))
                    {
                        full = false;
                        break;
                    }
                    full = true;
                }
                if (full) fullRows.Add(y);
            }
            var fullColumns = new List<int>();
            for (int x = 0; x < Width; x++)
            {
                bool full = false;
                for (int y = 0; y < Height; y++)
                {
                    if (!playable[x, y])
                    {
                        continue;
                    }
                    if (!cells[x, y].HasValue && !shapeCells.Contains(new GridPos(x + MinX, y + MinY)))
                    {
                        full = false;
                        break;
                    }
                    full = true;
                }
                if (full) fullColumns.Add(x);
            }
            if (fullRows.Count == 0 && fullColumns.Count == 0)
            {
                return LineExplosionResult.None;
            }
            var lineCells = new List<GridPos>();
            var seen = new HashSet<GridPos>();
            foreach (int y in fullRows)
            {
                for (int x = 0; x < Width; x++)
                {
                    var pos = new GridPos(x + MinX, y + MinY);
                    if (seen.Add(pos)) lineCells.Add(pos);
                }
            }
            foreach (int x in fullColumns)
            {
                for (int y = 0; y < Height; y++)
                {
                    var pos = new GridPos(x + MinX, y + MinY);
                    if (seen.Add(pos)) lineCells.Add(pos);
                }
            }
            return new LineExplosionResult(fullRows, fullColumns, lineCells);
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

        /// <summary>Legal-origin check with optional ghost overhang.</summary>
        public bool AnyPlacementExists(BlockShape shape, bool allowOutside)
        {
            if (!allowOutside)
            {
                return AnyPlacementExists(shape);
            }
            for (int x = 1 - shape.Width; x < Width; x++)
            {
                for (int y = 1 - shape.Height; y < Height; y++)
                {
                    if (CanPlace(shape, new GridPos(x + MinX, y + MinY), true))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Is there at least one legal origin for this shape?</summary>
        public bool AnyPlacementExists(BlockShape shape)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (CanPlace(shape, new GridPos(x + MinX, y + MinY)))
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
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var origin = new GridPos(x + MinX, y + MinY);
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
