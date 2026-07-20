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
    /// Cells added through the CONSTRUCTOR must be non-negative (that path keeps the origin
    /// at 0,0). Mid-round inflation goes through CreateResized instead, which grows on any
    /// side by moving MinX/MinY - so existing coordinates never change.
    /// </summary>
    public sealed partial class GameBoard
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
    }
}
