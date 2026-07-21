// PURPOSE: What a player-activated joker/power was pointed at - an optional hand
// index, board cell, or a pair of rows/columns to swap. Built via the factories.

namespace ProjectBlock.Core
{
    /// <summary>Which way a line runs, for effects that act on whole rows or columns.</summary>
    public enum LineAxis
    {
        Row = 0,
        Column = 1
    }

    /// <summary>What a player-activated joker was pointed at. All fields optional.</summary>
    public readonly struct ActivationTarget
    {
        /// <summary>Index into the hand (Iade picks the card to swap).</summary>
        public readonly int? HandIndex;

        /// <summary>A board cell (Enfeksiyon picks the cube to infect).</summary>
        public readonly GridPos? Cell;

        /// <summary>Set together with LineA/LineB when the player picked two whole lines
        /// to exchange ("Kentsel Dönüşüm").</summary>
        public readonly LineAxis? Axis;

        /// <summary>Board coordinates of the two lines - a row's Y, or a column's X.</summary>
        public readonly int? LineA;
        public readonly int? LineB;

        public ActivationTarget(int? handIndex, GridPos? cell)
            : this(handIndex, cell, null, null, null)
        {
        }

        public ActivationTarget(int? handIndex, GridPos? cell, LineAxis? axis,
            int? lineA, int? lineB)
        {
            HandIndex = handIndex;
            Cell = cell;
            Axis = axis;
            LineA = lineA;
            LineB = lineB;
        }

        public static readonly ActivationTarget None = new ActivationTarget(null, null);

        public static ActivationTarget Hand(int handIndex)
        {
            return new ActivationTarget(handIndex, null);
        }

        public static ActivationTarget Board(GridPos cell)
        {
            return new ActivationTarget(null, cell);
        }

        /// <summary>Two rows, or two columns, to exchange.</summary>
        public static ActivationTarget LineSwap(LineAxis axis, int lineA, int lineB)
        {
            return new ActivationTarget(null, null, axis, lineA, lineB);
        }
    }
}
