// PURPOSE: The closed play grid of one round (partial: state, construction, resizing,
// and basic cell access). Owns placement validation, line explosions, element
// settling and the clean-sweep check across its partial files. Pure state + rules,
// no scoring. Usually a rectangle, but Kentsel Donusum / Tilsim make it a bounding
// box plus a playable mask; inflation powers grow it via CreateResized.

using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectBlock.Core
{
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
    /// THE ONE EXCEPTION: a cell EATEN by shuffle erosion (MarkDead) is not merely skipped - it
    /// kills its row and its column outright, because an unfillable cell sits inside the line.
    /// A hole that was never board (bounding-box filler around bolted-on cells) does NOT do
    /// that, or adding a cell to the board would kill the rows it stretched the box across.
    ///
    /// Cells added through the CONSTRUCTOR must be non-negative (that path keeps the origin
    /// at 0,0). Mid-round inflation goes through CreateResized instead, which grows on any
    /// side by moving MinX/MinY - so existing coordinates never change.
    /// </summary>
    public sealed partial class GameBoard
    {
        private readonly Cube?[,] cells;

        /// <summary>Which cells of the bounding box are real play area.</summary>
        private readonly bool[,] playable;

        /// <summary>Cells that were play area and have been EATEN AWAY mid-round (shuffle
        /// erosion). They are not playable any more, and unlike an ordinary hole in the bounding
        /// box they also KILL their row and column: an unfillable cell sits in the middle of the
        /// line, so that line can never be full again. That is the whole point of hollowing the
        /// board out from the centre - see ShuffleErosion.</summary>
        private readonly bool[,] dead;

        /// <summary>Coordinate of the leftmost column / bottom row. Normally 0, but a board
        /// inflated on its left or bottom side extends into NEGATIVE coordinates instead of
        /// renumbering everything - so a cube that sat at (2,3) still sits at (2,3) after the
        /// board grows around it. Only the internal array is 0-based; GridPos never is.</summary>
        public int MinX { get; }

        public int MinY { get; }

        /// <summary>Playable cells in total - the honest "size" of an irregular board. Shrinks
        /// as erosion eats cells away.</summary>
        public int PlayableCellCount { get; private set; }

        /// <summary>Cells erosion has eaten. 0 on every untouched board.</summary>
        public int DeadCellCount { get; private set; }

        /// <summary>GHOST RULE: cubes placed outside the grid persist here (visible as a
        /// ghostly trace; the future Tılsım power converts their space into board).
        /// They take no part in lines, sweeps or explosions.</summary>
        private readonly Dictionary<GridPos, Cube> outsideCubes = new Dictionary<GridPos, Cube>();

        public IReadOnlyDictionary<GridPos, Cube> OutsideCubes
        {
            get { return outsideCubes; }
        }

        /// <summary>Cells a block may not be placed on right now, though they are empty and
        /// part of the play area ("Mapus" seals one per turn). Deliberately invisible to
        /// everything except placement: a sealed cell still counts as an empty cell of its
        /// row/column, so a line holding one simply cannot be completed while the seal lasts.</summary>
        private readonly List<GridPos> sealedCells = new List<GridPos>();

        public IReadOnlyList<GridPos> SealedCells
        {
            get { return sealedCells; }
        }

        /// <summary>True if placement is currently forbidden on this cell.</summary>
        public bool IsSealed(GridPos pos)
        {
            for (int i = 0; i < sealedCells.Count; i++)
            {
                if (sealedCells[i].X == pos.X && sealedCells[i].Y == pos.Y)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Forbids placement on a cell. Goes through RoundEngine.SealBoardCell.</summary>
        internal void SealCell(GridPos pos)
        {
            if (!IsSealed(pos))
            {
                sealedCells.Add(pos);
            }
        }

        /// <summary>Lifts every seal (a boss re-picks its cell each turn).</summary>
        internal void ClearSeals()
        {
            sealedCells.Clear();
        }

        /// <summary>While true every placed cube is a plain one, whatever its card's element
        /// says ("Vanilya"). Set by RoundEngine.SetBoss and carried across a resize, so an
        /// inflation power cannot accidentally hand the elements back mid-round.</summary>
        internal bool IgnoreElements { get; set; }

        /// <summary>
        /// Which way water falls on this arena, as a one-cell step. (0,-1) - straight down - on
        /// every ordinary board; the "Kütleçekim merkezi" power turns it to any of the four
        /// sides for the rest of the round.
        ///
        /// It lives on the BOARD rather than in RoundRules because it is a property of the arena,
        /// and the arena is rebuilt every round - which is exactly the round-scoping the power
        /// wants. Carried across CreateResized and CreateClone, so an inflation power cannot
        /// quietly stand the gravity back up and the mirror world inherits the same pull.
        /// </summary>
        public GridPos WaterFlow { get; private set; } = new GridPos(0, -1);

        /// <summary>Turns the arena's gravity. Only ever called through RoundEngine.SetWaterFlow,
        /// which also makes the water already on the board obey it at once.</summary>
        internal void SetWaterFlow(GridPos direction)
        {
            WaterFlow = direction;
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
            dead = new bool[boxWidth, boxHeight];

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

        /// <summary>Board built from explicit masks; only CreateResized uses this.</summary>
        private GameBoard(int minX, int minY, int width, int height, bool[,] mask,
            bool[,] deadMask, int playableCount, int deadCount)
        {
            MinX = minX;
            MinY = minY;
            Width = width;
            Height = height;
            cells = new Cube?[width, height];
            playable = mask;
            dead = deadMask;
            PlayableCellCount = playableCount;
            DeadCellCount = deadCount;
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
            var deadMask = new bool[newWidth, newHeight];
            int count = 0;
            int deadCount = 0;
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
                    // Eaten cells stay eaten across a resize. A band that erosion removed
                    // wholesale left the bounding box, so it is simply not here any more; only
                    // interior kills survive as dead cells.
                    deadMask[ix, iy] = inSource && source.dead[sx, sy];
                    if (mask[ix, iy])
                    {
                        count++;
                    }
                    if (deadMask[ix, iy])
                    {
                        deadCount++;
                    }
                }
            }

            var board = new GameBoard(newMinX, newMinY, newWidth, newHeight, mask, deadMask,
                count, deadCount);
            board.IgnoreElements = source.IgnoreElements;
            board.WaterFlow = source.WaterFlow;
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
            // Seals are absolute coordinates like everything else, so they survive a resize -
            // except any that the shrink pushed off the board entirely.
            for (int i = 0; i < source.sealedCells.Count; i++)
            {
                if (board.IsInside(source.sealedCells[i]))
                {
                    board.sealedCells.Add(source.sealedCells[i]);
                }
            }
            return board;
        }

        /// <summary>
        /// An exact copy of a board: same size, same origin, same holes, same eaten cells, same
        /// cubes, same ghost traces. "Öteki dünya" clones the arena the moment it is cast, so the
        /// mirror world starts as whatever the player had built - which is why WHEN you cast it
        /// matters as much as whether you do.
        ///
        /// Placement seals are NOT copied: a seal belongs to the boss's one chosen cell on one
        /// board, and duplicating it would seal a cell the boss never picked.
        /// </summary>
        public static GameBoard CreateClone(GameBoard source)
        {
            var mask = new bool[source.Width, source.Height];
            var deadMask = new bool[source.Width, source.Height];
            for (int x = 0; x < source.Width; x++)
            {
                for (int y = 0; y < source.Height; y++)
                {
                    mask[x, y] = source.playable[x, y];
                    deadMask[x, y] = source.dead[x, y];
                }
            }
            var clone = new GameBoard(source.MinX, source.MinY, source.Width, source.Height,
                mask, deadMask, source.PlayableCellCount, source.DeadCellCount);
            for (int x = 0; x < source.Width; x++)
            {
                for (int y = 0; y < source.Height; y++)
                {
                    Cube? cube = source.cells[x, y];
                    if (cube.HasValue)
                    {
                        clone.cells[x, y] = cube.Value;
                        clone.OccupiedCount++;
                    }
                }
            }
            foreach (KeyValuePair<GridPos, Cube> ghost in source.outsideCubes)
            {
                clone.outsideCubes[ghost.Key] = ghost.Value;
            }
            clone.IgnoreElements = source.IgnoreElements;
            clone.WaterFlow = source.WaterFlow;
            return clone;
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

        /// <summary>True for a cell erosion has EATEN. Never playable, and it kills the row and
        /// the column it sits in. The View draws these differently from a plain hole.</summary>
        public bool IsDead(GridPos pos)
        {
            int ix = pos.X - MinX;
            int iy = pos.Y - MinY;
            return ix >= 0 && ix < Width && iy >= 0 && iy < Height && dead[ix, iy];
        }

        /// <summary>
        /// EROSION: eats the given cells. They stop being play area and become DEAD, which also
        /// kills their rows and columns for line purposes (see the `dead` field).
        ///
        /// Any cube still standing on an eaten cell goes with it, protection and
        /// indestructibility included - the cell itself ceases to exist, so nothing can stay
        /// there. The caller is expected to have destroyed what it could through the engine
        /// first (so the destruction log and the per-card bookkeeping stay honest); this only
        /// clears the stragglers. Cells that are already dead, or were never playable, are
        /// skipped. Returns the cells actually eaten.
        /// </summary>
        internal List<GridPos> MarkDead(IEnumerable<GridPos> targets)
        {
            var eaten = new List<GridPos>();
            foreach (GridPos pos in targets)
            {
                int ix = pos.X - MinX;
                int iy = pos.Y - MinY;
                if (ix < 0 || ix >= Width || iy < 0 || iy >= Height)
                {
                    continue;
                }
                if (dead[ix, iy] || !playable[ix, iy])
                {
                    continue;
                }
                if (cells[ix, iy].HasValue)
                {
                    cells[ix, iy] = null;
                    OccupiedCount--;
                }
                playable[ix, iy] = false;
                dead[ix, iy] = true;
                PlayableCellCount--;
                DeadCellCount++;
                eaten.Add(pos);
            }
            return eaten;
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
    }
}
