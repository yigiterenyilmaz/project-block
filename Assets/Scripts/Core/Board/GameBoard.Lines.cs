// PURPOSE: GameBoard destruction - water settling, retro/Tetris row-collapse, full
// row/column resolution and explosion (fire chains), and single-cube destruction.

using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectBlock.Core
{
    partial class GameBoard
    {
        public bool SettleWaterAndReact()
        {
            return SettleWaterAndReact(null);
        }

        /// <summary>
        /// WATER RULE (confirmed 2026-07-18): water simply DROPS straight down until it
        /// rests, one cell per pass. Each pass's moves are appended to fallFrames (when
        /// given) so the UI can show the fall step by step. Afterwards any fire touching
        /// water turns to obsidian (the water persists). Returns true if anything changed.
        /// </summary>
        public bool SettleWaterAndReact(List<IReadOnlyList<WaterMove>> fallFrames)
        {
            bool anyChange = false;
            bool moved = true;
            int guard = Height + 2;
            while (moved && guard-- > 0)
            {
                moved = false;
                List<WaterMove> frame = null;
                for (int y = 1; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        Cube? cube = cells[x, y];
                        if (!cube.HasValue || cube.Value.Kind != CubeKind.Water)
                        {
                            continue;
                        }
                        if (cells[x, y - 1].HasValue)
                        {
                            continue;
                        }
                        cells[x, y - 1] = cube;
                        cells[x, y] = null;
                        moved = true;
                        anyChange = true;
                        if (fallFrames != null)
                        {
                            if (frame == null)
                            {
                                frame = new List<WaterMove>();
                            }
                            frame.Add(new WaterMove(new GridPos(x + MinX, y + MinY), new GridPos(x + MinX, y - 1 + MinY)));
                        }
                    }
                }
                if (frame != null)
                {
                    fallFrames.Add(frame);
                }
            }
            // douse: fire adjacent to water becomes obsidian
            var doused = new List<GridPos>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Cube? cube = cells[x, y];
                    if (!cube.HasValue || cube.Value.Kind != CubeKind.Fire)
                    {
                        continue;
                    }
                    if (IsWaterAt(x + 1, y) || IsWaterAt(x - 1, y)
                        || IsWaterAt(x, y + 1) || IsWaterAt(x, y - 1))
                    {
                        doused.Add(new GridPos(x + MinX, y + MinY));
                    }
                }
            }
            foreach (GridPos pos in doused)
            {
                cells[pos.X - MinX, pos.Y - MinY] = new Cube(CubeKind.Obsidian, cells[pos.X - MinX, pos.Y - MinY].Value.SourceCardId);
                anyChange = true;
            }
            return anyChange;
        }

        /// <summary>
        /// RETRO/TETRIS line collapse. Given the rows that just cleared (0-based array indices,
        /// exactly as returned in LineExplosionResult.Rows), removes those now-empty rows and
        /// drops every row ABOVE them straight down to close the gap - classic Tetris gravity,
        /// where a locked block never falls into a hole beneath it; only whole rows collapse and
        /// the blocks resting above ride down together. A "cleared" row that still holds a cube
        /// (an indestructible survivor - obsidian/gold/Parazit host) is kept in place and acts as
        /// floor, so no cube is ever lost. Cube count is conserved. Returns true if anything moved.
        /// NOTE: works in whole rows, so it assumes the retro board is a plain rectangle (it is -
        /// retro grows the base rectangle by DeadZoneRows full-width rows on top).
        /// </summary>
        public bool CollapseClearedRows(IReadOnlyList<int> clearedRows)
        {
            if (clearedRows == null || clearedRows.Count == 0)
            {
                return false;
            }
            var cleared = new HashSet<int>(clearedRows);
            // Rows to keep, bottom to top: every row that was not cleared, plus any cleared row
            // that still holds a cube (an indestructible survivor stays put and becomes floor).
            var keep = new List<int>(Height);
            for (int y = 0; y < Height; y++)
            {
                if (!cleared.Contains(y) || RowHasCube(y))
                {
                    keep.Add(y);
                }
            }
            if (keep.Count == Height)
            {
                return false; // no cleared row was actually emptied - nothing collapses
            }
            // Compact the kept rows to the bottom in order. keep is ascending and write grows
            // one per step, so read >= write always: a source row is never clobbered before it
            // is copied. Cube count is unchanged, so OccupiedCount needs no adjustment.
            bool moved = false;
            int write = 0;
            for (int k = 0; k < keep.Count; k++)
            {
                int read = keep[k];
                if (read != write)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        cells[x, write] = cells[x, read];
                    }
                    moved = true;
                }
                write++;
            }
            // Empty the vacated top rows (their cubes were copied down to the compacted stack).
            for (int y = write; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    cells[x, y] = null;
                }
            }
            return moved;
        }

        private bool RowHasCube(int y)
        {
            for (int x = 0; x < Width; x++)
            {
                if (cells[x, y].HasValue)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsWaterAt(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return false;
            }
            Cube? cube = cells[x, y];
            return cube.HasValue && cube.Value.Kind == CubeKind.Water;
        }


        /// <summary>Detects all full rows and columns, explodes their destructible cubes.</summary>
        public LineExplosionResult ResolveFullLines()
        {
            return ResolveFullLines(false);
        }

        /// <summary>As above; when <paramref name="rowsOnly"/> is true, vertical (column) clears are
        /// skipped - the retro/tetris rule, where only full rows explode.</summary>
        public LineExplosionResult ResolveFullLines(bool rowsOnly)
        {
            var fullRows = new List<int>();
            for (int y = 0; y < Height; y++)
            {
                bool full = false;
                for (int x = 0; x < Width; x++)
                {
                    if (!playable[x, y])
                    {
                        continue; // a hole is not part of the line
                    }
                    if (!cells[x, y].HasValue)
                    {
                        full = false;
                        break;
                    }
                    full = true; // at least one playable cell so far, and it is occupied
                }
                if (full) fullRows.Add(y);
            }
            var fullColumns = new List<int>();
            for (int x = 0; !rowsOnly && x < Width; x++)
            {
                bool full = false;
                for (int y = 0; y < Height; y++)
                {
                    if (!playable[x, y])
                    {
                        continue;
                    }
                    if (!cells[x, y].HasValue)
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

            var exploded = new List<GridPos>();
            var seen = new HashSet<GridPos>(); // row/column intersections explode once
            var fireBlockIds = new HashSet<int>();
            foreach (int y in fullRows)
            {
                for (int x = 0; x < Width; x++)
                {
                    ExplodeCell(new GridPos(x + MinX, y + MinY), seen, exploded, fireBlockIds);
                }
            }
            foreach (int x in fullColumns)
            {
                for (int y = 0; y < Height; y++)
                {
                    ExplodeCell(new GridPos(x + MinX, y + MinY), seen, exploded, fireBlockIds);
                }
            }
            // FIRE RULE: when one cube of a fire block explodes, its whole block explodes.
            // One pass suffices: chained cubes always belong to an already-collected block.
            if (fireBlockIds.Count > 0)
            {
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        Cube? cube = cells[x, y];
                        if (cube.HasValue && fireBlockIds.Contains(cube.Value.SourceCardId))
                        {
                            ExplodeCell(new GridPos(x + MinX, y + MinY), seen, exploded, fireBlockIds);
                        }
                    }
                }
            }
            return new LineExplosionResult(fullRows, fullColumns, exploded);
        }

        private void ExplodeCell(GridPos pos, HashSet<GridPos> seen, List<GridPos> exploded,
            HashSet<int> fireBlockIds)
        {
            if (!seen.Add(pos))
            {
                return;
            }
            Cube? cube = cells[pos.X - MinX, pos.Y - MinY];
            if (!cube.HasValue || !CubeRules.IsDestructible(cube.Value))
            {
                return;
            }
            if (cube.Value.Kind == CubeKind.Fire)
            {
                fireBlockIds.Add(cube.Value.SourceCardId);
            }
            cells[pos.X - MinX, pos.Y - MinY] = null;
            OccupiedCount--;
            exploded.Add(pos);
        }

        /// <summary>Dynamite: destroys every destructible cube on the board.</summary>
        public List<GridPos> DestroyAllDestructible()
        {
            var destroyed = new List<GridPos>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Cube? cube = cells[x, y];
                    if (cube.HasValue && CubeRules.IsExternallyDestructible(cube.Value))
                    {
                        cells[x, y] = null;
                        OccupiedCount--;
                        destroyed.Add(new GridPos(x + MinX, y + MinY));
                    }
                }
            }
            return destroyed;
        }

        /// <summary>Destroys ONE cube outside a line explosion, if the cell holds a
        /// destructible one. Returns false for empty cells and indestructible cubes.
        /// EXTENSION POINT: joker/power effects (Robot supurge, Buldozer, Enfeksiyon,
        /// Kara delik's void cube) go through here so cube-kind rules stay in one place.</summary>
        public bool DestroyCube(GridPos pos)
        {
            if (!IsInside(pos))
            {
                return false;
            }
            Cube? cube = cells[pos.X - MinX, pos.Y - MinY];
            if (!cube.HasValue || !CubeRules.IsExternallyDestructible(cube.Value))
            {
                return false;
            }
            cells[pos.X - MinX, pos.Y - MinY] = null;
            OccupiedCount--;
            return true;
        }

        /// <summary>Destroys a cube even if its kind is normally indestructible. Only for
        /// effects that explicitly break that rule ("elmas kazma" cracking obsidian). A
        /// Parazit host cube still resists - only a player line explosion takes it out.</summary>
        public bool DestroyCubeForced(GridPos pos)
        {
            if (!IsInside(pos))
            {
                return false;
            }
            Cube? cube = cells[pos.X - MinX, pos.Y - MinY];
            if (!cube.HasValue || cube.Value.Protected)
            {
                return false;
            }
            cells[pos.X - MinX, pos.Y - MinY] = null;
            OccupiedCount--;
            return true;
        }
    }
}
