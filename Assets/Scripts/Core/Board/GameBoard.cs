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

    /// <summary>One water cube dropping one cell (for the UI's fall animation).</summary>
    public readonly struct WaterMove
    {
        public readonly GridPos From;
        public readonly GridPos To;

        public WaterMove(GridPos from, GridPos to)
        {
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// The play grid. Usually a plain rectangle, but NOT necessarily: "Kentsel Dönüşüm" and
    /// "Tılsım" bolt extra cells onto it, so the board is really a bounding box plus a mask
    /// of which cells are actually playable.
    ///
    /// Width/Height are the BOUNDING BOX. Everything that asks "is this a real cell?" goes
    /// through IsInside, which consults the mask - a plain rectangular board simply has every
    /// cell playable, which is why the base game behaves exactly as before.
    ///
    /// A line is full when every PLAYABLE cell of that row/column is occupied, so an added
    /// cell genuinely extends the row it sits in. Rows with no playable cells never explode.
    ///
    /// Cells added through the CONSTRUCTOR must be non-negative (that path keeps the origin
    /// at 0,0). Mid-round inflation goes through CreateResized instead, which grows on any
    /// side by moving MinX/MinY - so existing coordinates never change.
    /// </summary>
    public sealed class GameBoard
    {
        private readonly Cube?[,] cells;

        /// <summary>Which cells of the bounding box are real play area.</summary>
        private readonly bool[,] playable;

        /// <summary>Coordinate of the leftmost column / bottom row. Normally 0, but a board
        /// inflated on its left or bottom side extends into NEGATIVE coordinates instead of
        /// renumbering everything - so a cube that sat at (2,3) still sits at (2,3) after the
        /// board grows around it. Only the internal array is 0-based; GridPos never is.</summary>
        public int MinX { get; }

        public int MinY { get; }

        /// <summary>Playable cells in total - the honest "size" of an irregular board.</summary>
        public int PlayableCellCount { get; }

        /// <summary>GHOST RULE: cubes placed outside the grid persist here (visible as a
        /// ghostly trace; the future Tılsım power converts their space into board).
        /// They take no part in lines, sweeps or explosions.</summary>
        private readonly Dictionary<GridPos, Cube> outsideCubes = new Dictionary<GridPos, Cube>();

        public IReadOnlyDictionary<GridPos, Cube> OutsideCubes
        {
            get { return outsideCubes; }
        }

        public int Width { get; }
        public int Height { get; }
        public int OccupiedCount { get; private set; }

        public GameBoard(int width, int height)
            : this(width, height, null)
        {
        }

        /// <summary>Board with extra playable cells bolted onto the base rectangle. The
        /// bounding box stretches to cover them; everything not in the rectangle and not in
        /// the extra set stays a hole.</summary>
        public GameBoard(int width, int height, IEnumerable<GridPos> extraCells)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentException("Board must be at least 1x1.");
            }
            var extra = new List<GridPos>();
            int boxWidth = width;
            int boxHeight = height;
            if (extraCells != null)
            {
                foreach (GridPos cell in extraCells)
                {
                    if (cell.X < 0 || cell.Y < 0)
                    {
                        continue; // the board only grows right and up - see the class docs
                    }
                    extra.Add(cell);
                    if (cell.X >= boxWidth) boxWidth = cell.X + 1;
                    if (cell.Y >= boxHeight) boxHeight = cell.Y + 1;
                }
            }

            MinX = 0;
            MinY = 0;
            Width = boxWidth;
            Height = boxHeight;
            cells = new Cube?[boxWidth, boxHeight];
            playable = new bool[boxWidth, boxHeight];

            int count = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    playable[x, y] = true;
                    count++;
                }
            }
            foreach (GridPos cell in extra)
            {
                if (!playable[cell.X, cell.Y])
                {
                    playable[cell.X, cell.Y] = true;
                    count++;
                }
            }
            PlayableCellCount = count;
        }

        /// <summary>Board built from an explicit mask; only CreateResized uses this.</summary>
        private GameBoard(int minX, int minY, int width, int height, bool[,] mask,
            int playableCount)
        {
            MinX = minX;
            MinY = minY;
            Width = width;
            Height = height;
            cells = new Cube?[width, height];
            playable = mask;
            PlayableCellCount = playableCount;
        }

        /// <summary>
        /// A copy of <paramref name="source"/> grown (positive) or shrunk (negative) on each
        /// side, with every surviving cube carried across. The inflation powers use this to
        /// resize the board MID-ROUND.
        ///
        /// Cubes standing in a band that is being removed are simply dropped - the caller is
        /// expected to have pushed them inward first (RoundEngine.ShrinkBoardPushingInward).
        /// Returns null if the requested size would not be a board at all.
        /// </summary>
        public static GameBoard CreateResized(GameBoard source, int left, int right,
            int bottom, int top)
        {
            int newWidth = source.Width + left + right;
            int newHeight = source.Height + bottom + top;
            if (newWidth < 1 || newHeight < 1)
            {
                return null;
            }
            // Growing on the left/bottom pushes the ORIGIN out instead of renumbering cells,
            // so every existing cube keeps the coordinate it already had.
            int newMinX = source.MinX - left;
            int newMinY = source.MinY - bottom;

            var mask = new bool[newWidth, newHeight];
            int count = 0;
            for (int ix = 0; ix < newWidth; ix++)
            {
                for (int iy = 0; iy < newHeight; iy++)
                {
                    int worldX = newMinX + ix;
                    int worldY = newMinY + iy;
                    int sx = worldX - source.MinX;
                    int sy = worldY - source.MinY;
                    bool inSource = sx >= 0 && sx < source.Width && sy >= 0 && sy < source.Height;
                    // Inside the old board: keep its mask, holes and all. Outside it: this is
                    // freshly inflated ground, so it is play area.
                    mask[ix, iy] = inSource ? source.playable[sx, sy] : true;
                    if (mask[ix, iy])
                    {
                        count++;
                    }
                }
            }

            var board = new GameBoard(newMinX, newMinY, newWidth, newHeight, mask, count);
            for (int sx = 0; sx < source.Width; sx++)
            {
                for (int sy = 0; sy < source.Height; sy++)
                {
                    Cube? cube = source.cells[sx, sy];
                    if (!cube.HasValue)
                    {
                        continue;
                    }
                    var at = new GridPos(source.MinX + sx, source.MinY + sy);
                    if (board.IsInside(at))
                    {
                        board.cells[at.X - board.MinX, at.Y - board.MinY] = cube.Value;
                        board.OccupiedCount++;
                    }
                }
            }
            foreach (KeyValuePair<GridPos, Cube> ghost in source.outsideCubes)
            {
                if (!board.IsInside(ghost.Key))
                {
                    board.outsideCubes[ghost.Key] = ghost.Value;
                }
            }
            return board;
        }

        /// <summary>True for a real play cell. On a plain rectangle this is just a bounds
        /// check; on a grown board it also rejects the holes in the bounding box.</summary>
        public bool IsInside(GridPos pos)
        {
            int ix = pos.X - MinX;
            int iy = pos.Y - MinY;
            return ix >= 0 && ix < Width && iy >= 0 && iy < Height && playable[ix, iy];
        }

        /// <summary>Same as IsInside; named for callers that are asking about the SHAPE of
        /// the board (the UI deciding which cells to draw) rather than about a position.</summary>
        public bool IsPlayable(GridPos pos)
        {
            return IsInside(pos);
        }

        public Cube? GetCube(GridPos pos)
        {
            if (!IsInside(pos))
            {
                Cube outside;
                return outsideCubes.TryGetValue(pos, out outside) ? (Cube?)outside : null;
            }
            return cells[pos.X - MinX, pos.Y - MinY];
        }

        /// <summary>True if every cell of the shape (anchored at origin) is inside and
        /// empty - or holds a transparent cube, which placements may cover (it gets
        /// replaced, confirmed 2026-07-18).</summary>
        public bool CanPlace(BlockShape shape, GridPos origin)
        {
            foreach (GridPos offset in shape.Cells)
            {
                GridPos pos = origin + offset;
                if (!IsInside(pos))
                {
                    return false;
                }
                Cube? occupant = cells[pos.X - MinX, pos.Y - MinY];
                if (occupant.HasValue && occupant.Value.Kind != CubeKind.Transparent && occupant.Value.Kind != CubeKind.Void
                    && occupant.Value.Kind != CubeKind.Mine)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Ghost placement check: cubes may hang outside the grid (onto free
        /// outside space), but at least one cube must land inside.</summary>
        public bool CanPlace(BlockShape shape, GridPos origin, bool allowOutside)
        {
            if (!allowOutside)
            {
                return CanPlace(shape, origin);
            }
            int insideCount = 0;
            foreach (GridPos offset in shape.Cells)
            {
                GridPos pos = origin + offset;
                if (IsInside(pos))
                {
                    Cube? occupant = cells[pos.X - MinX, pos.Y - MinY];
                    if (occupant.HasValue && occupant.Value.Kind != CubeKind.Transparent && occupant.Value.Kind != CubeKind.Void
                    && occupant.Value.Kind != CubeKind.Mine)
                    {
                        return false;
                    }
                    insideCount++;
                }
                else if (outsideCubes.ContainsKey(pos))
                {
                    return false;
                }
            }
            return insideCount >= 1;
        }

        /// <summary>Places the card's cubes. Caller must have validated with CanPlace.
        /// Transparent cubes underneath are replaced.</summary>
        public IReadOnlyList<GridPos> Place(BlockCard card, GridPos origin)
        {
            return Place(card, origin, false);
        }

        /// <summary>Placement with optional ghost overhang (cubes outside the grid
        /// persist in OutsideCubes).</summary>
        public IReadOnlyList<GridPos> Place(BlockCard card, GridPos origin, bool allowOutside)
        {
            return Place(card, card.Shape, origin, allowOutside);
        }

        /// <summary>Placement of an explicit shape (mechanical rotation / fox reshape
        /// place a transformed shape on behalf of the card).</summary>
        public IReadOnlyList<GridPos> Place(BlockCard card, BlockShape shape, GridPos origin,
            bool allowOutside)
        {
            if (!CanPlace(shape, origin, allowOutside))
            {
                throw new InvalidOperationException("Illegal placement of " + card + " at " + origin + ".");
            }
            var placed = new List<GridPos>(shape.Size);
            CubeKind kind = CubeRules.KindForCard(card);
            foreach (GridPos offset in shape.Cells)
            {
                GridPos pos = origin + offset;
                if (IsInside(pos))
                {
                    Cube? occupant = cells[pos.X - MinX, pos.Y - MinY];
                    if (occupant.HasValue && (occupant.Value.Kind == CubeKind.Void
                        || occupant.Value.Kind == CubeKind.Mine))
                    {
                        // Traps: "Kara delik" swallows the arriving cube, "Mayın" blows it up.
                        // Either way both are gone, so nothing is placed.
                        cells[pos.X - MinX, pos.Y - MinY] = null;
                        OccupiedCount--;
                        continue;
                    }
                    if (!occupant.HasValue)
                    {
                        OccupiedCount++; // replaced transparents were already counted
                    }
                    cells[pos.X - MinX, pos.Y - MinY] = new Cube(kind, card.Id);
                }
                else
                {
                    outsideCubes[pos] = new Cube(kind, card.Id);
                }
                placed.Add(pos);
            }
            return placed;
        }

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
            for (int x = 0; x < Width; x++)
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
                    if (cube.HasValue && CubeRules.IsDestructible(cube.Value))
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
            if (!cube.HasValue || !CubeRules.IsDestructible(cube.Value))
            {
                return false;
            }
            cells[pos.X - MinX, pos.Y - MinY] = null;
            OccupiedCount--;
            return true;
        }

        /// <summary>Destroys a cube even if its kind is normally indestructible. Only for
        /// effects that explicitly break that rule ("elmas kazma" cracking obsidian).</summary>
        public bool DestroyCubeForced(GridPos pos)
        {
            if (!IsInside(pos) || !cells[pos.X - MinX, pos.Y - MinY].HasValue)
            {
                return false;
            }
            cells[pos.X - MinX, pos.Y - MinY] = null;
            OccupiedCount--;
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
