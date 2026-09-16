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

        /// <summary>Writes a cell directly, empty (null) included, keeping OccupiedCount
        /// honest. Internal: only whole-board rearrangements use it ("Kentsel Dönüşüm"
        /// swapping two lines), where cubes move rather than appear or die.</summary>
        private void SetCellRaw(GridPos pos, Cube? cube)
        {
            if (!IsInside(pos))
            {
                return;
            }
            bool had = cells[pos.X - MinX, pos.Y - MinY].HasValue;
            cells[pos.X - MinX, pos.Y - MinY] = cube;
            if (had && !cube.HasValue)
            {
                OccupiedCount--;
            }
            else if (!had && cube.HasValue)
            {
                OccupiedCount++;
            }
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
            return EdgeSidesOf(pos) != BoardSides.None;
        }

        /// <summary>
        /// WHICH walls the cell touches, rather than merely whether it touches one.
        ///
        /// IsOnEdge is this question with the answer thrown away, so it defers to this rather
        /// than repeating the four tests - a second copy of "what counts as a wall" is a second
        /// copy that can disagree, and "Buzluk"'s whole animation is built on this answer being
        /// the same one the rule used.
        ///
        /// A wall is any neighbour that is not play area: the board's outer rim, a hole in the
        /// bounding box, or a cell the shuffle erosion has eaten. None of those is a special
        /// case here - IsInside already knows the difference and this asks it.
        /// </summary>
        public BoardSides EdgeSidesOf(GridPos pos)
        {
            if (!IsInside(pos))
            {
                return BoardSides.None;
            }
            BoardSides sides = BoardSides.None;
            if (!IsInside(new GridPos(pos.X - 1, pos.Y))) { sides |= BoardSides.Left; }
            if (!IsInside(new GridPos(pos.X + 1, pos.Y))) { sides |= BoardSides.Right; }
            if (!IsInside(new GridPos(pos.X, pos.Y - 1))) { sides |= BoardSides.Down; }
            if (!IsInside(new GridPos(pos.X, pos.Y + 1))) { sides |= BoardSides.Up; }
            return sides;
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

        /// <summary>Where this card's cubes are standing right now ("Hedefli" taking the rest of
        /// a block with the target). Play area only: a ghost trace hanging outside the grid takes
        /// no part in explosions, so it takes no part in this either.</summary>
        public List<GridPos> CellsOfCard(int cardId)
        {
            var found = new List<GridPos>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Cube? cube = cells[x, y];
                    if (cube.HasValue && cube.Value.SourceCardId == cardId)
                    {
                        found.Add(new GridPos(x + MinX, y + MinY));
                    }
                }
            }
            return found;
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
                if (RowIsKilled(y))
                {
                    continue; // same rule as ResolveFullLines: an eaten cell kills the line
                }
                bool full = false;
                bool breaks = false;
                for (int x = 0; x < Width; x++)
                {
                    if (!playable[x, y])
                    {
                        continue;
                    }
                    bool placedHere = shapeCells.Contains(new GridPos(x + MinX, y + MinY));
                    if (!cells[x, y].HasValue && !placedHere)
                    {
                        if (optional[x, y])
                        {
                            continue; // bonus ground never holds a line up
                        }
                        full = false;
                        break;
                    }
                    full = full || !optional[x, y];
                    // A cube about to be placed is assumed breakable: the shape arrives without
                    // its card, so its element is not knowable here. Completing an all-gold line
                    // WITH gold is the one case this over-predicts.
                    breaks = breaks || placedHere
                        || CubeRules.IsDestructible(cells[x, y].Value);
                }
                if (full && breaks) fullRows.Add(y);
            }
            var fullColumns = new List<int>();
            for (int x = 0; x < Width; x++)
            {
                if (ColumnIsKilled(x))
                {
                    continue;
                }
                bool full = false;
                bool breaks = false;
                for (int y = 0; y < Height; y++)
                {
                    if (!playable[x, y])
                    {
                        continue;
                    }
                    bool placedHere = shapeCells.Contains(new GridPos(x + MinX, y + MinY));
                    if (!cells[x, y].HasValue && !placedHere)
                    {
                        if (optional[x, y])
                        {
                            continue; // see the row loop
                        }
                        full = false;
                        break;
                    }
                    full = full || !optional[x, y];
                    breaks = breaks || placedHere
                        || CubeRules.IsDestructible(cells[x, y].Value);
                }
                if (full && breaks) fullColumns.Add(x);
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
                    if (optional[x, y])
                    {
                        // Bonus ground ("Tılsım") is exempt, exactly as it is exempt from the
                        // fullness check: ground the power gave you must never cost you a sweep
                        // it would otherwise have allowed. One rule, not a special case.
                        continue;
                    }
                    Cube? cube = cells[x, y];
                    if (cube.HasValue && CubeRules.CountsForCleanSweep(cube.Value))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Is the board a mirror image of itself across the vertical middle ("Simetri")? Judged on
        /// OCCUPANCY only - a cell holds a cube or it does not - so a fire cube facing a plain one
        /// still reads as symmetric. Kind-aware symmetry would be nearly impossible to build on
        /// purpose, and this joker is about the SHAPE you leave behind.
        ///
        /// Cells that are not play area (a hole, an eroded cell) are skipped on both sides: a board
        /// the erosion clock has chewed is judged on what is left of it.
        /// </summary>
        public bool IsMirroredLeftRight()
        {
            for (int x = 0; x < Width / 2; x++)
            {
                int mirrored = Width - 1 - x;
                for (int y = 0; y < Height; y++)
                {
                    if (!ColumnPairComparable(x, mirrored, y))
                    {
                        continue;
                    }
                    if (cells[x, y].HasValue != cells[mirrored, y].HasValue)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>The horizontal-axis twin of IsMirroredLeftRight: top against bottom.</summary>
        public bool IsMirroredTopBottom()
        {
            for (int y = 0; y < Height / 2; y++)
            {
                int mirrored = Height - 1 - y;
                for (int x = 0; x < Width; x++)
                {
                    if (!RowPairComparable(x, y, mirrored))
                    {
                        continue;
                    }
                    if (cells[x, y].HasValue != cells[x, mirrored].HasValue)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>Both halves have to be real play area for the comparison to mean anything.</summary>
        /// <summary>
        /// Is the board the same turned UPSIDE DOWN - cell (x, y) matching (W-1-x, H-1-y)?
        ///
        /// THIS IS A THIRD KIND OF SYMMETRY AND IT IS NOT EITHER MIRROR. A board can be perfectly
        /// symmetric to the eye - the shape a player deliberately built - and fail both mirror
        /// tests, because a rotation is not a reflection. "Simetri" recognised only reflections
        /// and so read a rotationally symmetric arena as no symmetry at all, which is the one
        /// thing a joker about the board's shape must never do.
        ///
        /// Note the two mirrors together IMPLY this one, so a caller that pays for both must not
        /// also pay for this - see SimetriJoker, which takes the best single answer.
        /// </summary>
        public bool IsRotationallySymmetric()
        {
            return RotationBreaks() == 0;
        }

        /// <summary>Cells breaking the 180-degree rotation - 0 exactly when
        /// IsRotationallySymmetric is true. Same reporting job as the mirror counts.</summary>
        public int RotationBreaks()
        {
            int breaks = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int mx = Width - 1 - x;
                    int my = Height - 1 - y;
                    // Each PAIR is looked at once - without this every disagreement is counted
                    // twice and the "how far off" readout reads double.
                    if (my * Width + mx <= y * Width + x)
                    {
                        continue;
                    }
                    if (!playable[x, y] || dead[x, y] || !playable[mx, my] || dead[mx, my])
                    {
                        continue;
                    }
                    if (cells[x, y].HasValue != cells[mx, my].HasValue)
                    {
                        breaks++;
                    }
                }
            }
            return breaks;
        }

        /// <summary>
        /// HOW MANY CELLS ARE BREAKING the left-right mirror - 0 exactly when IsMirroredLeftRight
        /// is true. It exists so "Simetri" can say how close the board is instead of only saying
        /// no: a joker that pays for a board shape and reports nothing but "watching" gives the
        /// player no way to tell a board one cube short from a board nowhere near.
        ///
        /// Counts CELLS, not pairs: two cells disagree, and the player has to change one of them.
        /// </summary>
        public int MirrorBreaksLeftRight()
        {
            int breaks = 0;
            for (int x = 0; x < Width / 2; x++)
            {
                int mirrored = Width - 1 - x;
                for (int y = 0; y < Height; y++)
                {
                    if (ColumnPairComparable(x, mirrored, y)
                        && cells[x, y].HasValue != cells[mirrored, y].HasValue)
                    {
                        breaks++;
                    }
                }
            }
            return breaks;
        }

        /// <summary>The horizontal-axis twin of MirrorBreaksLeftRight.</summary>
        public int MirrorBreaksTopBottom()
        {
            int breaks = 0;
            for (int y = 0; y < Height / 2; y++)
            {
                int mirrored = Height - 1 - y;
                for (int x = 0; x < Width; x++)
                {
                    if (RowPairComparable(x, y, mirrored)
                        && cells[x, y].HasValue != cells[x, mirrored].HasValue)
                    {
                        breaks++;
                    }
                }
            }
            return breaks;
        }

        private bool ColumnPairComparable(int x, int mirroredX, int y)
        {
            return playable[x, y] && !dead[x, y]
                && playable[mirroredX, y] && !dead[mirroredX, y];
        }

        private bool RowPairComparable(int x, int y, int mirroredY)
        {
            return playable[x, y] && !dead[x, y]
                && playable[x, mirroredY] && !dead[x, mirroredY];
        }

        /// <summary>Legal-origin check with optional ghost overhang.</summary>
        public bool AnyPlacementExists(BlockShape shape, bool allowOutside)
        {
            return AnyPlacementExists(shape, allowOutside, false);
        }

        /// <summary>Is there anywhere this shape could go? The negative flag matters here as
        /// much as in CanPlace: a negative block lands on occupied cells, so a board that is
        /// a dead end for every other card may still have room for it.</summary>
        public bool AnyPlacementExists(BlockShape shape, bool allowOutside, bool negative)
        {
            int fromX = allowOutside ? 1 - shape.Width : 0;
            int fromY = allowOutside ? 1 - shape.Height : 0;
            for (int x = fromX; x < Width; x++)
            {
                for (int y = fromY; y < Height; y++)
                {
                    if (CanPlace(shape, new GridPos(x + MinX, y + MinY), allowOutside, negative))
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

        /// <summary>
        /// HOW CLOSE A ROW IS TO EXPLODING: how many REQUIRED cells of row
        /// <paramref name="absoluteY"/> are still empty, or -1 when the row can never explode
        /// again at all - erosion killed it, "Kangren" took it whole, or it has no required cell
        /// in it to begin with.
        ///
        /// It counts exactly what ResolveFullLines waits for and NOTHING else: a hole in the
        /// bounding box was never part of the line, and bonus ground ("Tılsım") never holds one
        /// up (nor, by the same token, conjures one). So anything reasoning about "the player is
        /// one cube away from this row" - a boss picking where to hurt, a hint, a heuristic - is
        /// reasoning about the same line the explosion rule does, rather than about a second
        /// definition of "full" that can drift away from it.
        ///
        /// A pure query: nothing here touches the board.
        /// </summary>
        public int RowGapCount(int absoluteY)
        {
            int iy = absoluteY - MinY;
            if (iy < 0 || iy >= Height || RowIsKilled(iy) || RowIsInfectionDead(absoluteY))
            {
                return -1;
            }
            int gaps = 0;
            bool anyRequired = false;
            for (int x = 0; x < Width; x++)
            {
                if (!playable[x, iy] || optional[x, iy])
                {
                    continue;
                }
                anyRequired = true;
                if (!cells[x, iy].HasValue)
                {
                    gaps++;
                }
            }
            return anyRequired ? gaps : -1;
        }

        /// <summary>Column counterpart of RowGapCount.</summary>
        public int ColumnGapCount(int absoluteX)
        {
            int ix = absoluteX - MinX;
            if (ix < 0 || ix >= Width || ColumnIsKilled(ix) || ColumnIsInfectionDead(absoluteX))
            {
                return -1;
            }
            int gaps = 0;
            bool anyRequired = false;
            for (int y = 0; y < Height; y++)
            {
                if (!playable[ix, y] || optional[ix, y])
                {
                    continue;
                }
                anyRequired = true;
                if (!cells[ix, y].HasValue)
                {
                    gaps++;
                }
            }
            return anyRequired ? gaps : -1;
        }

        /// <summary>
        /// Exchanges the contents of two whole rows, or two whole columns ("Kentsel Dönüşüm").
        /// Coordinates are board coordinates, so they respect MinX/MinY.
        ///
        /// On an irregular board a cell is only swapped when BOTH ends are real play area -
        /// a cube can never be pushed into a hole. Cells where only one end is playable are
        /// left exactly as they are.
        /// </summary>
        public bool SwapLines(LineAxis axis, int lineA, int lineB)
        {
            if (lineA == lineB)
            {
                return false;
            }
            if (axis == LineAxis.Row)
            {
                if (!IsRowInBounds(lineA) || !IsRowInBounds(lineB))
                {
                    return false;
                }
                for (int x = MinX; x < MinX + Width; x++)
                {
                    SwapCells(new GridPos(x, lineA), new GridPos(x, lineB));
                }
                return true;
            }
            if (!IsColumnInBounds(lineA) || !IsColumnInBounds(lineB))
            {
                return false;
            }
            for (int y = MinY; y < MinY + Height; y++)
            {
                SwapCells(new GridPos(lineA, y), new GridPos(lineB, y));
            }
            return true;
        }

        private bool IsRowInBounds(int y)
        {
            return y >= MinY && y < MinY + Height;
        }

        private bool IsColumnInBounds(int x)
        {
            return x >= MinX && x < MinX + Width;
        }

        private void SwapCells(GridPos a, GridPos b)
        {
            if (!IsInside(a) || !IsInside(b))
            {
                return; // never move a cube into a hole
            }
            Cube? ca = GetCube(a);
            Cube? cb = GetCube(b);
            SetCellRaw(a, cb);
            SetCellRaw(b, ca);
        }
    }
}
