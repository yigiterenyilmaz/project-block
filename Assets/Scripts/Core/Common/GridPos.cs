// PURPOSE: Integer coordinate on the game board / inside a block shape.
// Convention used across the whole project: X = column (0 = leftmost),
// Y = row (0 = bottom row, +Y goes up, matching Unity's 2D world space).

using System;

namespace ProjectBlock.Core
{
    /// <summary>Immutable integer grid coordinate. See file header for axis conventions.</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static GridPos operator +(GridPos a, GridPos b)
        {
            return new GridPos(a.X + b.X, a.Y + b.Y);
        }

        public bool Equals(GridPos other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPos other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (X * 397) ^ Y;
        }

        public override string ToString()
        {
            return "(" + X + "," + Y + ")";
        }
    }
}
