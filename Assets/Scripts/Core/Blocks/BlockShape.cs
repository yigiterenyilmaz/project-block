// PURPOSE: The geometry of a block - an immutable set of cube offsets.
// Shapes are always normalized (minimum X and Y are 0) so that two equal shapes
// produce the same CanonicalKey (needed later by shape-comparing jokers like "Siyam").
// Rotation/mirroring exist already because several planned mechanics need them
// (Cimbiz power, mechanical blocks, mirror blocks) - the BASE rules never call them.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectBlock.Core
{
    /// <summary>Immutable, normalized set of cube offsets that make up one block.</summary>
    public sealed class BlockShape
    {
        private readonly GridPos[] cells;

        /// <summary>Normalized cube offsets, sorted bottom-to-top then left-to-right.</summary>
        public IReadOnlyList<GridPos> Cells
        {
            get { return cells; }
        }

        /// <summary>Number of cubes in the block.</summary>
        public int Size
        {
            get { return cells.Length; }
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>Stable identity string for "same shape" comparisons.</summary>
        public string CanonicalKey { get; }

        private BlockShape(GridPos[] normalizedSortedCells)
        {
            cells = normalizedSortedCells;
            int maxX = 0;
            int maxY = 0;
            var keyBuilder = new StringBuilder();
            foreach (GridPos c in cells)
            {
                if (c.X > maxX) maxX = c.X;
                if (c.Y > maxY) maxY = c.Y;
                keyBuilder.Append(c.X).Append(',').Append(c.Y).Append(';');
            }
            Width = maxX + 1;
            Height = maxY + 1;
            CanonicalKey = keyBuilder.ToString();
        }

        /// <summary>
        /// Builds a shape from any cell set; offsets are normalized so the minimum X/Y become 0.
        /// Connectivity is NOT enforced here: the random generator guarantees it, and future
        /// block types / powers may legitimately produce disconnected shapes.
        /// </summary>
        public static BlockShape FromCells(IEnumerable<GridPos> anyCells)
        {
            var unique = new HashSet<GridPos>(anyCells);
            if (unique.Count == 0)
            {
                throw new ArgumentException("A block shape needs at least one cell.");
            }
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            foreach (GridPos c in unique)
            {
                if (c.X < minX) minX = c.X;
                if (c.Y < minY) minY = c.Y;
            }
            GridPos[] normalized = unique.Select(c => new GridPos(c.X - minX, c.Y - minY)).ToArray();
            Array.Sort(normalized, CompareCells);
            return new BlockShape(normalized);
        }

        private static int CompareCells(GridPos a, GridPos b)
        {
            return a.Y != b.Y ? a.Y - b.Y : a.X - b.X;
        }

        /// <summary>90 degrees clockwise rotation. Unused by base rules (see file header).</summary>
        public BlockShape RotatedClockwise()
        {
            return FromCells(cells.Select(c => new GridPos(c.Y, -c.X)));
        }

        /// <summary>Horizontal mirror. Unused by base rules (see file header).</summary>
        public BlockShape MirroredHorizontally()
        {
            return FromCells(cells.Select(c => new GridPos(-c.X, c.Y)));
        }

        /// <summary>Multi-line ASCII picture (top row first) for logs and debugging.</summary>
        public string ToAscii()
        {
            var occupied = new bool[Width, Height];
            foreach (GridPos c in cells)
            {
                occupied[c.X, c.Y] = true;
            }
            var sb = new StringBuilder();
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    sb.Append(occupied[x, y] ? '#' : '.');
                }
                if (y > 0) sb.Append('\n');
            }
            return sb.ToString();
        }

        public override string ToString()
        {
            return "Shape[" + Size + " cubes, " + Width + "x" + Height + "]";
        }
    }
}
