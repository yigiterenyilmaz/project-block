// PURPOSE: What a player-activated joker/power was pointed at - an optional hand
// index, board cell, or a pair of rows/columns to swap. Built via the factories.

using System.Collections.Generic;

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

        /// <summary>"Öteki dünya": aim this activation at the MIRROR world instead of the main
        /// one. False everywhere else, and meaningless when no mirror is open. Carried on the
        /// target rather than passed alongside it so every existing call site keeps working.</summary>
        public readonly bool OnMirrorWorld;

        /// <summary>The OTHER card in hand ("Lehimleme" solders two together).</summary>
        public readonly int? SecondHandIndex;

        /// <summary>Where the second card sits relative to the first, in shape coordinates
        /// ("Lehimleme"). Also the anchor offset of a picked sub-shape.</summary>
        public readonly GridPos? Offset;

        /// <summary>A set of SHAPE OFFSETS rather than board cells - the cubes the player picked
        /// out of a card in hand ("Neşter" choosing where to cut). Null unless that is the
        /// targeting mode.</summary>
        public readonly IReadOnlyList<GridPos> CellSet;

        /// <summary>"Neşter": a card and the cubes picked out of it.</summary>
        public static ActivationTarget CardCubes(int handIndex, IReadOnlyList<GridPos> picked)
        {
            return new ActivationTarget(handIndex, null, null, null, null, false, null, null,
                picked);
        }

        /// <summary>"Lehimleme": two cards and where the second sits against the first.</summary>
        public static ActivationTarget TwoCards(int first, int second, GridPos offset)
        {
            return new ActivationTarget(first, null, null, null, null, false, second, offset,
                null);
        }

        /// <summary>"Gen nakli": a cube on the board and the card that takes its element.</summary>
        public static ActivationTarget CellAndCard(GridPos cell, int handIndex)
        {
            return new ActivationTarget(handIndex, cell);
        }

        public ActivationTarget(int? handIndex, GridPos? cell)
            : this(handIndex, cell, null, null, null)
        {
        }

        public ActivationTarget(int? handIndex, GridPos? cell, LineAxis? axis,
            int? lineA, int? lineB)
            : this(handIndex, cell, axis, lineA, lineB, false)
        {
        }

        public ActivationTarget(int? handIndex, GridPos? cell, LineAxis? axis,
            int? lineA, int? lineB, bool onMirrorWorld)
            : this(handIndex, cell, axis, lineA, lineB, onMirrorWorld, null, null, null)
        {
        }

        public ActivationTarget(int? handIndex, GridPos? cell, LineAxis? axis,
            int? lineA, int? lineB, bool onMirrorWorld, int? secondHandIndex, GridPos? offset,
            IReadOnlyList<GridPos> cellSet)
        {
            HandIndex = handIndex;
            Cell = cell;
            Axis = axis;
            LineA = lineA;
            LineB = lineB;
            OnMirrorWorld = onMirrorWorld;
            SecondHandIndex = secondHandIndex;
            Offset = offset;
            CellSet = cellSet;
        }

        /// <summary>The same target, aimed at the other world.</summary>
        public ActivationTarget OnWorld(bool mirror)
        {
            return new ActivationTarget(HandIndex, Cell, Axis, LineA, LineB, mirror,
                SecondHandIndex, Offset, CellSet);
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
