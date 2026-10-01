// PURPOSE: What "Simetri" saw and paid this turn, written down for the VIEW. Reporting only: the
// payout itself is unchanged, and nothing here is ever read back by a rule.
//
// THE VIEW DECIDES NOTHING ABOUT SYMMETRY. Which of the three shapes held (either mirror, or the
// board being the same turned upside down), which cells actually MATCH (an occupied cell whose
// partner is occupied, both of them real play area - exactly the comparison the joker paid on,
// GameBoard.CollectSymmetryPairs), and what the payment came to (MEASURED around the joker's own
// AddFlatScore, so a "Terslik" round reports - and draws - a payout that cost points). The
// animation groups those pairs into regions and plays them; it never asks the board again,
// because by the time it plays the boss and the end-of-turn effects may have moved it.
//
// A new object per payment, so the View matches it by IDENTITY like every other report.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>The three shapes "Simetri" recognises.</summary>
    public enum SymmetryKind
    {
        LeftRight,
        TopBottom,
        HalfTurn
    }

    /// <summary>One matched pair: a cell and its partner under one symmetry. A cell ON the axis
    /// (or the centre of a half turn) is its own partner.</summary>
    public struct SymmetryPair
    {
        public GridPos A;
        public GridPos B;

        public SymmetryPair(GridPos a, GridPos b)
        {
            A = a;
            B = b;
        }

        public bool Self
        {
            get { return A.Equals(B); }
        }
    }

    public sealed class SymmetryVisuals
    {
        public bool LeftRight;
        public bool TopBottom;
        public bool HalfTurn;

        /// <summary>What the joker asked for, in its own units (40 or 120).</summary>
        public int Bonus;

        /// <summary>What actually landed, at the score's scale (signed: "Terslik" inverts it).</summary>
        public int Points;

        /// <summary>The board's box at the moment it was judged.</summary>
        public int MinX;
        public int MinY;
        public int Width;
        public int Height;

        /// <summary>The matched pairs of every shape that HELD (an empty list for one that did not).
        /// Each pair is listed once, primary first: the left, the bottom, the earlier cell.</summary>
        public readonly List<SymmetryPair> LeftRightPairs = new List<SymmetryPair>();
        public readonly List<SymmetryPair> TopBottomPairs = new List<SymmetryPair>();
        public readonly List<SymmetryPair> HalfTurnPairs = new List<SymmetryPair>();

        /// <summary>Both mirrors: the triple, the "double verification".</summary>
        public bool BothMirrors
        {
            get { return LeftRight && TopBottom; }
        }

        public bool Any
        {
            get { return LeftRight || TopBottom || HalfTurn; }
        }

        /// <summary>The vertical axis, in board coordinates (a half when the width is even).</summary>
        public float AxisX
        {
            get { return MinX + (Width - 1) * 0.5f; }
        }

        /// <summary>The horizontal axis, in board coordinates.</summary>
        public float AxisY
        {
            get { return MinY + (Height - 1) * 0.5f; }
        }

        public List<SymmetryPair> PairsOf(SymmetryKind kind)
        {
            switch (kind)
            {
                case SymmetryKind.LeftRight: return LeftRightPairs;
                case SymmetryKind.TopBottom: return TopBottomPairs;
                default: return HalfTurnPairs;
            }
        }

        /// <summary>
        /// What the board looks like to "Simetri" right now: which shapes hold and the cells that
        /// match under each. The joker writes its report with this, and the animation lab asks it
        /// about boards of its own - one reading of the board, not two.
        /// </summary>
        public static SymmetryVisuals Describe(GameBoard board)
        {
            var v = new SymmetryVisuals();
            if (board == null)
            {
                return v;
            }
            v.MinX = board.MinX;
            v.MinY = board.MinY;
            v.Width = board.Width;
            v.Height = board.Height;
            v.LeftRight = board.MirrorBreaksLeftRight() == 0;
            v.TopBottom = board.MirrorBreaksTopBottom() == 0;
            v.HalfTurn = board.RotationBreaks() == 0;
            if (v.LeftRight)
            {
                board.CollectSymmetryPairs(SymmetryKind.LeftRight, v.LeftRightPairs);
            }
            if (v.TopBottom)
            {
                board.CollectSymmetryPairs(SymmetryKind.TopBottom, v.TopBottomPairs);
            }
            if (v.HalfTurn)
            {
                board.CollectSymmetryPairs(SymmetryKind.HalfTurn, v.HalfTurnPairs);
            }
            return v;
        }
    }
}
