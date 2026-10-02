// PURPOSE: "Rüzgar"'s gust - the band of board a drawn stroke blows across - and the report
// one use of the power hands the View. THE ONE PLACE that decides which cells are in the wind,
// how far ahead of a cell another one lies, and which cells water slides through: the power,
// its preview and the animation all ask it, so the band the player aimed with is the band the
// rules blew down.
//
// EXTENSION POINT: anything else a wind should carry (a joker's marks on the board) answers
// Joker.CanRideWind / RideWind and writes what it carried into Carries.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>
    /// A gust: a stroke turned into a band of board. Starts on the CENTRE of the cell the stroke
    /// began in, runs at whatever angle it was drawn, never longer than nearly the arena's
    /// diagonal, and is always <see cref="Width"/> cells wide. A cell is in the wind when its
    /// centre is inside that band.
    /// </summary>
    public sealed class WindGust
    {
        /// <summary>How wide the band is, in cells - always (designer's call, 2026-10-02).</summary>
        public const float Width = 3f;

        /// <summary>A stroke shorter than this is a tap, not a wind.</summary>
        public const float MinLength = 1f;

        /// <summary>The longest gust as a share of the diagonal from the top-right cell to the
        /// bottom-left one: "nearly corner to corner". Balance placeholder.</summary>
        public const float MaxLengthShare = 0.9f;

        /// <summary>A cell is DOWNWIND of another when it lies more than this much further along
        /// the gust - half a cell, so the cubes side by side across the band are never ahead of
        /// each other.</summary>
        public const float AheadMargin = 0.5f;

        // A hair inside the band's edge, so a centre that lands exactly on it (a diagonal at an
        // unlucky angle) is decided the same way every time.
        private const float EdgeEpsilon = 0.001f;

        public float StartX { get; }
        public float StartY { get; }
        public float EndX { get; }
        public float EndY { get; }

        /// <summary>Unit direction the wind blows.</summary>
        public float DirX { get; }
        public float DirY { get; }

        /// <summary>Start to end, after the clamp.</summary>
        public float Length { get; }

        /// <summary>The longest gust this board allows.</summary>
        public float MaxLength { get; }

        /// <summary>False for a stroke too short to be a wind.</summary>
        public bool Valid { get; }

        /// <summary>Every play cell in the wind, ordered by how far along the gust it lies, then
        /// across it, then by position - the fixed order every random draw walks.</summary>
        public IReadOnlyList<GridPos> Cells
        {
            get { return cells; }
        }

        private readonly List<GridPos> cells = new List<GridPos>();

        /// <summary>What the board's own riders (a joker's marks) carried on this gust, written by
        /// the jokers themselves (Joker.RideWind). Reporting only.</summary>
        public readonly List<WindCarry> Carries = new List<WindCarry>();

        private WindGust(float sx, float sy, float dx, float dy, float length, float max, bool valid)
        {
            StartX = sx;
            StartY = sy;
            DirX = dx;
            DirY = dy;
            Length = length;
            MaxLength = max;
            Valid = valid;
            EndX = sx + dx * length;
            EndY = sy + dy * length;
        }

        /// <summary>The longest stroke a board allows: nearly its diagonal.</summary>
        public static float MaxLengthFor(GameBoard board)
        {
            float w = Math.Max(1, board.Width) - 1;
            float h = Math.Max(1, board.Height) - 1;
            return MaxLengthShare * (float)Math.Sqrt(w * w + h * h);
        }

        /// <summary>The cell centre a stroke starting at this point starts from.</summary>
        public static GridPos StartCell(BoardStroke stroke)
        {
            return new GridPos((int)Math.Round(stroke.FromX, MidpointRounding.AwayFromZero),
                (int)Math.Round(stroke.FromY, MidpointRounding.AwayFromZero));
        }

        /// <summary>Turns a drawn stroke into the gust it blows on <paramref name="board"/>.</summary>
        public static WindGust From(GameBoard board, BoardStroke stroke)
        {
            GridPos start = StartCell(stroke);
            float vx = stroke.ToX - start.X;
            float vy = stroke.ToY - start.Y;
            float len = (float)Math.Sqrt(vx * vx + vy * vy);
            float max = MaxLengthFor(board);
            bool valid = len >= MinLength && board.IsInside(start);
            float dx = len > 0.0001f ? vx / len : 1f;
            float dy = len > 0.0001f ? vy / len : 0f;
            var gust = new WindGust(start.X, start.Y, dx, dy, Math.Min(len, Math.Max(MinLength, max)),
                max, valid);
            if (valid)
            {
                gust.Collect(board);
            }
            return gust;
        }

        /// <summary>How far along the gust a cell's centre lies (0 at the start).</summary>
        public float Along(GridPos cell)
        {
            return (cell.X - StartX) * DirX + (cell.Y - StartY) * DirY;
        }

        /// <summary>How far to the side of the gust's centre line a cell's centre lies.</summary>
        public float Across(GridPos cell)
        {
            return (cell.X - StartX) * DirY - (cell.Y - StartY) * DirX;
        }

        /// <summary>Is this cell's centre inside the band? (Play area or not.)</summary>
        public bool InBand(GridPos cell)
        {
            float along = Along(cell);
            float across = Across(cell);
            return along >= -0.5f && along <= Length + 0.5f
                && Math.Abs(across) < Width * 0.5f - EdgeEpsilon;
        }

        /// <summary>Is <paramref name="cell"/> in the wind (a play cell inside the band)?</summary>
        public bool Covers(GridPos cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].X == cell.X && cells[i].Y == cell.Y)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Does <paramref name="cell"/> lie downwind of <paramref name="from"/>?</summary>
        public bool IsAhead(GridPos from, GridPos cell)
        {
            return Along(cell) > Along(from) + AheadMargin;
        }

        /// <summary>
        /// The cells a thing pushed by the wind passes through from <paramref name="from"/>, in
        /// order, as EDGE-ADJACENT steps (a grid walk along the gust's own direction): water never
        /// squeezes diagonally between two blocks, it has to go round by a free side. Unbounded by
        /// the band - the caller stops at the first cell it cannot enter. <paramref name="limit"/>
        /// caps the walk.
        /// </summary>
        public List<GridPos> Walk(GridPos from, int limit)
        {
            var path = new List<GridPos>();
            int x = from.X;
            int y = from.Y;
            int stepX = DirX > 0.0001f ? 1 : DirX < -0.0001f ? -1 : 0;
            int stepY = DirY > 0.0001f ? 1 : DirY < -0.0001f ? -1 : 0;
            float ax = Math.Abs(DirX);
            float ay = Math.Abs(DirY);
            // Distance along the ray to the next vertical / horizontal cell boundary.
            float tMaxX = stepX != 0 ? 0.5f / ax : float.MaxValue;
            float tMaxY = stepY != 0 ? 0.5f / ay : float.MaxValue;
            float tDeltaX = stepX != 0 ? 1f / ax : float.MaxValue;
            float tDeltaY = stepY != 0 ? 1f / ay : float.MaxValue;
            while (path.Count < limit)
            {
                // A tie is a corner: cross the X boundary first, then the Y one - two edge steps,
                // never one diagonal.
                if (tMaxX <= tMaxY + 0.0001f)
                {
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    y += stepY;
                    tMaxY += tDeltaY;
                }
                path.Add(new GridPos(x, y));
            }
            return path;
        }

        private void Collect(GameBoard board)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var cell = new GridPos(x + board.MinX, y + board.MinY);
                    if (board.IsInside(cell) && InBand(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }
            cells.Sort(CompareCells);
        }

        private int CompareCells(GridPos a, GridPos b)
        {
            int c = Along(a).CompareTo(Along(b));
            if (c != 0)
            {
                return c;
            }
            c = Math.Abs(Across(a)).CompareTo(Math.Abs(Across(b)));
            if (c != 0)
            {
                return c;
            }
            c = a.Y.CompareTo(b.Y);
            return c != 0 ? c : a.X.CompareTo(b.X);
        }
    }

    /// <summary>One ember a fire in the wind threw. Target is the cube it set alight, or null
    /// when it flew off - nothing in its way, or the gust simply carried it past.</summary>
    public sealed class WindEmber
    {
        public GridPos Source;
        public GridPos? Target;
    }

    /// <summary>One water cube the wind pushed: where it stood, every cell it slid through and
    /// where it stopped (before gravity took it - the fall is in the engine's water frames).</summary>
    public sealed class WindPush
    {
        public GridPos From;
        public GridPos To;
        public Cube Cube;
        public readonly List<GridPos> Path = new List<GridPos>();
    }

    /// <summary>Something a joker keeps on the board that the wind took somewhere else
    /// ("Enfeksiyon"'s infection: the source keeps it, the target catches it too).</summary>
    public sealed class WindCarry
    {
        public GridPos From;
        public GridPos To;

        /// <summary>The joker that owns what was carried.</summary>
        public string CarrierId;
    }

    /// <summary>
    /// One use of "Rüzgar", for the View. A NEW object per use, matched by identity. Reporting
    /// only: rebuilt by the next gust, meaningless across a load, never saved.
    /// </summary>
    public sealed class WindVisuals
    {
        public WindVisuals(WindGust gust)
        {
            Gust = gust;
        }

        public WindGust Gust { get; }

        /// <summary>Every ember thrown, caught or not, in the order they were thrown.</summary>
        public readonly List<WindEmber> Embers = new List<WindEmber>();

        /// <summary>The cubes the embers set alight, each with the face it had before and the
        /// fires that reached it (SpreadVisuals' own shape).</summary>
        public readonly List<SpreadIgnition> Ignitions = new List<SpreadIgnition>();

        /// <summary>The water the wind pushed, front first.</summary>
        public readonly List<WindPush> Pushes = new List<WindPush>();

        /// <summary>The push as animation frames: frame k holds every cube's k-th step.</summary>
        public readonly List<IReadOnlyList<WaterMove>> PushFrames = new List<IReadOnlyList<WaterMove>>();

        public bool Any
        {
            get { return Embers.Count > 0 || Pushes.Count > 0 || Gust.Carries.Count > 0; }
        }
    }
}
