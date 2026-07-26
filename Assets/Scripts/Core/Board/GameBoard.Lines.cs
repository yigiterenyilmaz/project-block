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

        /// <summary>
        /// A row with an EATEN cell in it is dead: that cell can never hold a cube, so the row
        /// can never be full and never explodes again. This is deliberately different from a
        /// plain hole in the bounding box (a cell that was never board, e.g. the filler around
        /// what "Kentsel Dönüşüm" and "Tılsım" bolt on) - those are simply skipped, so adding a
        /// cell to the board must never kill the rows it stretches the bounding box across.
        /// </summary>
        internal bool RowIsKilled(int y)
        {
            if (DeadCellCount == 0)
            {
                return false; // fast path: no erosion has happened on this board
            }
            for (int x = 0; x < Width; x++)
            {
                if (dead[x, y])
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Column counterpart of RowIsKilled.</summary>
        internal bool ColumnIsKilled(int x)
        {
            if (DeadCellCount == 0)
            {
                return false;
            }
            for (int y = 0; y < Height; y++)
            {
                if (dead[x, y])
                {
                    return true;
                }
            }
            return false;
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
                if (RowIsKilled(y))
                {
                    continue; // erosion ate a cell of this row - it can never be full again
                }
                if (RowIsInfectionDead(y + MinY))
                {
                    continue; // "Kangren" took this row whole - it can never explode again
                }
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
                if (ColumnIsKilled(x))
                {
                    continue;
                }
                if (ColumnIsInfectionDead(x + MinX))
                {
                    continue; // "Kangren" took this column whole
                }
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

        /// <summary>
        /// FORGETTING, which is not destruction. Lifts every cube of one card off the board
        /// whatever kind it is - obsidian, gold and a Parazit host included - because the board
        /// is not breaking them, it is ceasing to remember they were ever laid ("Alzheimer").
        /// Ghost traces hanging outside the grid go with them.
        ///
        /// Nothing here feeds the destruction bookkeeping: the caller re-baselines the snapshot
        /// afterwards, which is what keeps these cubes out of the turn's tally, its score and
        /// the clean-sweep pre-condition. Returns the cells that were cleared.
        /// </summary>
        public List<GridPos> ForgetCard(int cardId)
        {
            var cleared = new List<GridPos>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Cube? cube = cells[x, y];
                    if (cube.HasValue && cube.Value.SourceCardId == cardId)
                    {
                        cells[x, y] = null;
                        OccupiedCount--;
                        cleared.Add(new GridPos(x + MinX, y + MinY));
                    }
                }
            }
            var ghosts = new List<GridPos>();
            foreach (KeyValuePair<GridPos, Cube> ghost in outsideCubes)
            {
                if (ghost.Value.SourceCardId == cardId)
                {
                    ghosts.Add(ghost.Key);
                }
            }
            for (int i = 0; i < ghosts.Count; i++)
            {
                outsideCubes.Remove(ghosts[i]);
                cleared.Add(ghosts[i]);
            }
            return cleared;
        }

        /// <summary>
        /// THE ESCALATOR ("Yürüyen merdiven"): every row rides up one. The top row leaves the
        /// board entirely and a fresh empty row arrives at the bottom.
        ///
        /// Like forgetting, this is NOT destruction - the board is carrying cubes away, not
        /// breaking them - so nothing here resists it and the caller re-baselines the snapshot
        /// so the turn's diff never sees it. A cube that rides into a cell which is not play
        /// area (a hole, or one erosion ate) goes with the rest: there is nowhere to stand.
        ///
        /// Rows keep their contents, so a row that was not full does not become full by moving:
        /// the escalator never completes a line. Returns the cells whose cubes were carried off.
        /// </summary>
        public List<GridPos> ShiftRowsUp()
        {
            var lost = new List<GridPos>();
            for (int x = 0; x < Width; x++)
            {
                // The top row rides off the end.
                if (cells[x, Height - 1].HasValue)
                {
                    lost.Add(new GridPos(x + MinX, Height - 1 + MinY));
                }
                for (int y = Height - 1; y >= 1; y--)
                {
                    cells[x, y] = cells[x, y - 1];
                }
                cells[x, 0] = null; // a fresh empty row arrives underneath
            }
            // Anything that rode into a cell that is not play area has nowhere to stand.
            int occupied = 0;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (!cells[x, y].HasValue)
                    {
                        continue;
                    }
                    if (!playable[x, y])
                    {
                        cells[x, y] = null;
                        lost.Add(new GridPos(x + MinX, y + MinY));
                        continue;
                    }
                    occupied++;
                }
            }
            OccupiedCount = occupied;
            return lost;
        }

        /// <summary>
        /// "Merkezkaç kuvveti" (boss round): every cube is flung ONE cell further from the middle
        /// of the arena, and whatever is flung off the edge is gone. Returns the absolute cells
        /// that were lost - nothing was destroyed in the scoring sense, so the caller reports them
        /// as LIFTED (no score, no sweep tally), exactly like the escalator carrying a row off.
        ///
        /// DIRECTION: each cube moves by the SIGN of its offset from the centre, on both axes at
        /// once - so a cube up and to the right goes diagonally up and right, one straight above the
        /// middle goes straight up. A cube sitting exactly ON the centre of an odd board has no
        /// direction to be flung in and stays put; that is the one still point.
        ///
        /// ORDER: cubes are moved OUTSIDE-IN (furthest from the centre first), so a cube never
        /// lands on one that has not moved yet. Anything landing on a hole or a dead cell has
        /// nowhere to stand and is lost with the rest.
        /// </summary>
        public List<GridPos> FlingCubesOutward()
        {
            var lost = new List<GridPos>();
            // Centre in half-cell units, so an even board's centre falls between cells and an odd
            // board's lands on one. Compared doubled to keep it integer arithmetic.
            int centreX2 = Width - 1;
            int centreY2 = Height - 1;

            // Every occupied cell, furthest-from-centre first. Chebyshev distance doubled, which
            // is the number of steps this fling actually takes.
            var order = new List<GridPos>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (cells[x, y].HasValue)
                    {
                        order.Add(new GridPos(x, y));
                    }
                }
            }
            order.Sort((a, b) =>
            {
                int da = System.Math.Max(System.Math.Abs(a.X * 2 - centreX2),
                    System.Math.Abs(a.Y * 2 - centreY2));
                int db = System.Math.Max(System.Math.Abs(b.X * 2 - centreX2),
                    System.Math.Abs(b.Y * 2 - centreY2));
                if (da != db)
                {
                    return db.CompareTo(da); // furthest first
                }
                // Stable tie-break, so the same board always flings the same way.
                return a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y);
            });

            foreach (GridPos from in order)
            {
                Cube? cube = cells[from.X, from.Y];
                if (!cube.HasValue)
                {
                    continue; // already moved away by an earlier step
                }
                int dx = System.Math.Sign(from.X * 2 - centreX2);
                int dy = System.Math.Sign(from.Y * 2 - centreY2);
                if (dx == 0 && dy == 0)
                {
                    continue; // dead centre of an odd board: nothing to fling it away from
                }
                int tx = from.X + dx;
                int ty = from.Y + dy;
                cells[from.X, from.Y] = null;
                bool offBoard = tx < 0 || tx >= Width || ty < 0 || ty >= Height;
                if (offBoard || !playable[tx, ty] || cells[tx, ty].HasValue)
                {
                    // Off the edge, onto a hole, or into something that could not move: gone.
                    lost.Add(new GridPos(from.X + MinX, from.Y + MinY));
                    continue;
                }
                cells[tx, ty] = cube;
            }

            int occupied = 0;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (cells[x, y].HasValue)
                    {
                        occupied++;
                    }
                }
            }
            OccupiedCount = occupied;
            return lost;
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
        /// EXTENSION POINT: joker/power effects (Robot supurge, Deprem, Enfeksiyon,
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
