// PURPOSE: What a player-activated joker/power was pointed at - an optional hand
// index, board cell, a pair of rows/columns to swap, or a free-angle stroke across the
// board ("Rüzgar"). Built via the factories.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Which way a line runs, for effects that act on whole rows or columns.</summary>
    public enum LineAxis
    {
        Row = 0,
        Column = 1
    }

    /// <summary>
    /// A line drawn across the board, in BOARD units: a cell's centre sits at its own GridPos
    /// coordinates, so (2, 3) is the middle of cell (2, 3) and (2.5, 3) the edge it shares with
    /// (3, 3). Free-angle on purpose - "Rüzgar" blows wherever it was drawn - so it is floats,
    /// not cells. What the stroke MEANS (where it snaps, how long it may be) is the power's rule,
    /// never the UI's.
    /// </summary>
    public readonly struct BoardStroke
    {
        public readonly float FromX;
        public readonly float FromY;
        public readonly float ToX;
        public readonly float ToY;

        public BoardStroke(float fromX, float fromY, float toX, float toY)
        {
            FromX = fromX;
            FromY = fromY;
            ToX = toX;
            ToY = toY;
        }
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

        /// <summary>A free-angle line drawn across the board ("Rüzgar"). Null unless that is the
        /// targeting mode.</summary>
        public readonly BoardStroke? Stroke;

        /// <summary>"Rüzgar": where the gust starts and the way it was drawn.</summary>
        public static ActivationTarget Swipe(BoardStroke stroke)
        {
            return new ActivationTarget(null, null, null, null, null, false, null, null, null,
                stroke);
        }

        /// <summary>"Neşter": a card and the cubes picked out of it.</summary>
        public static ActivationTarget CardCubes(int handIndex, IReadOnlyList<GridPos> picked)
        {
            return new ActivationTarget(handIndex, null, null, null, null, false, null, null,
                picked);
        }

        /// <summary>"Kütleçekim merkezi": one of the four sides, as a one-cell step. It rides in
        /// Offset because a direction is exactly that shape of value - a relative step - and
        /// giving it a field of its own would only be the same thing under another name.</summary>
        public static ActivationTarget Direction(GridPos step)
        {
            return new ActivationTarget(null, null, null, null, null, false, null, step, null);
        }

        /// <summary>"Lehimleme": two cards and where the second sits against the first.</summary>
        public static ActivationTarget TwoCards(int first, int second, GridPos offset)
        {
            return new ActivationTarget(first, null, null, null, null, false, second, offset,
                null);
        }

        /// <summary>"Hidrolik pres": the 2x2 patch, named by its bottom-left cell, and WHICH of
        /// its four cells keeps the pressed cube. The second pick rides in Offset as a 0/1 step
        /// from the anchor - the patch is only two cells wide, so a step is all it takes, and it
        /// is the same shape of value Lehimleme's offset already is.</summary>
        public static ActivationTarget BoardArea(GridPos anchor, GridPos offset)
        {
            return new ActivationTarget(null, anchor, null, null, null, false, null, offset,
                null);
        }

        /// <summary>"Gen nakli" aimed at the BOARD: the block the element leaves (named by any of
        /// its cells) and the block that takes it (likewise). The second is an ABSOLUTE board cell
        /// riding in Offset - the one GridPos slot a two-cell pick has left.</summary>
        public static ActivationTarget CellToCell(GridPos from, GridPos to)
        {
            return new ActivationTarget(null, from, null, null, null, false, null, to, null);
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
            : this(handIndex, cell, axis, lineA, lineB, onMirrorWorld, secondHandIndex, offset,
                cellSet, null)
        {
        }

        public ActivationTarget(int? handIndex, GridPos? cell, LineAxis? axis,
            int? lineA, int? lineB, bool onMirrorWorld, int? secondHandIndex, GridPos? offset,
            IReadOnlyList<GridPos> cellSet, BoardStroke? stroke)
        {
            Stroke = stroke;
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
                SecondHandIndex, Offset, CellSet, Stroke);
        }

        public static readonly ActivationTarget None = new ActivationTarget(null, null);

        public static ActivationTarget Hand(int handIndex)
        {
            return new ActivationTarget(handIndex, null);
        }

        /// <summary>Another POWER, by instance id ("Powerbank"'s pick). It rides in HandIndex:
        /// a power that asks for this asks for nothing else.</summary>
        public static ActivationTarget PowerChoice(int powerInstanceId)
        {
            return new ActivationTarget(powerInstanceId, null);
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
