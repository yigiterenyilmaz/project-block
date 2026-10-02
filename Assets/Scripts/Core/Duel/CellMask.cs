// PURPOSE: A set of board cells as 128 bits - the duel planner's arithmetic. Every question the
// planner asks a thousand times a turn ("does this placement fill every gap of that row?", "can
// anything still reach this cell?") is one AND and one popcount on these, instead of a walk over
// a list of GridPos.
//
// A cell's bit is its index in the board's bounding box: (x - MinX) + (y - MinY) * Width. 128
// bits covers an 11x11 arena; a board larger than that is not planned with masks at all
// (CellMask.Fits says so and the evaluator falls back to what it can say without them).

namespace ProjectBlock.Core
{
    /// <summary>128 board cells as bits. A value type: copying it is copying the set.</summary>
    internal struct CellMask
    {
        public ulong Lo;
        public ulong Hi;

        public static readonly CellMask Empty = new CellMask();

        /// <summary>True when a board of this bounding box can be described in 128 bits.</summary>
        public static bool Fits(GameBoard board)
        {
            return board.Width * board.Height <= 128;
        }

        public static int IndexOf(GameBoard board, GridPos cell)
        {
            return (cell.X - board.MinX) + (cell.Y - board.MinY) * board.Width;
        }

        public void Set(int index)
        {
            if (index < 64)
            {
                Lo |= 1UL << index;
            }
            else
            {
                Hi |= 1UL << (index - 64);
            }
        }

        public bool Has(int index)
        {
            return index < 64 ? (Lo & (1UL << index)) != 0 : (Hi & (1UL << (index - 64))) != 0;
        }

        public bool IsEmpty
        {
            get { return Lo == 0 && Hi == 0; }
        }

        public int Count
        {
            get { return PopCount(Lo) + PopCount(Hi); }
        }

        public static CellMask operator &(CellMask a, CellMask b)
        {
            return new CellMask { Lo = a.Lo & b.Lo, Hi = a.Hi & b.Hi };
        }

        public static CellMask operator |(CellMask a, CellMask b)
        {
            return new CellMask { Lo = a.Lo | b.Lo, Hi = a.Hi | b.Hi };
        }

        /// <summary>a minus b.</summary>
        public static CellMask Without(CellMask a, CellMask b)
        {
            return new CellMask { Lo = a.Lo & ~b.Lo, Hi = a.Hi & ~b.Hi };
        }

        /// <summary>True when every cell of <paramref name="part"/> is in <paramref name="whole"/>.</summary>
        public static bool Covers(CellMask whole, CellMask part)
        {
            return (part.Lo & ~whole.Lo) == 0 && (part.Hi & ~whole.Hi) == 0;
        }

        /// <summary>The set moved <paramref name="n"/> bits up - a shape's cells at its origin
        /// moved to another origin in the same row-major box (the caller keeps it from wrapping
        /// a row's edge).</summary>
        public CellMask ShiftedUp(int n)
        {
            if (n <= 0)
            {
                return this;
            }
            if (n >= 128)
            {
                return Empty;
            }
            if (n >= 64)
            {
                return new CellMask { Lo = 0, Hi = Lo << (n - 64) };
            }
            return new CellMask { Lo = Lo << n, Hi = (Hi << n) | (Lo >> (64 - n)) };
        }

        public static int OverlapCount(CellMask a, CellMask b)
        {
            return PopCount(a.Lo & b.Lo) + PopCount(a.Hi & b.Hi);
        }

        /// <summary>Bits set in a word. Written out rather than taken from the framework: Core
        /// compiles against Unity's profile too, where the intrinsic is not there.</summary>
        public static int PopCount(ulong v)
        {
            v = v - ((v >> 1) & 0x5555555555555555UL);
            v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
            v = (v + (v >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((v * 0x0101010101010101UL) >> 56);
        }
    }
}
