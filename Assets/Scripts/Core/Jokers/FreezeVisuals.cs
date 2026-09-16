// PURPOSE: What "Buzluk" froze at the end of a turn, written down for the VIEW. Reporting only:
// the freeze itself is unchanged.
//
// IT EXISTS BECAUSE THE ANIMATION HAS TO TELL THE RULE, and the rule is not "some water turned
// blue". It is:
//
//   THE WATER FROZE BECAUSE IT REACHED A WALL. That is the whole joker, and it is the one thing
//   a picture can say and a number cannot. So the report carries WHICH SIDES of the cell are wall
//   (Sides), and the freeze is drawn coming IN FROM THOSE SIDES. A freeze that grows out of the
//   middle of the cube is teaching a rule the game does not have.
//   A CORNER TOUCHES TWO WALLS, and that is carried too - two fronts, meeting in the middle,
//   rather than one picked arbitrarily.
//   IT HAPPENED AFTER THE WATER SETTLED. Water that only touched a wall in passing is not caught
//   (BuzlukJoker.AfterTurnScored), so nothing in flight may ever be drawn freezing.
//
// THE OLD CUBE IS CARRIED WITH IT. The board holds ICE by the time anything is drawn, so the View
// cannot ask it what used to be there - and "it was water a moment ago" is the whole first half
// of the animation.
//
// A WALL IS NOT THE RECTANGLE'S RIM. GameBoard.EdgeSidesOf asks IsInside, which reads the
// PLAYABLE mask - so a cell beside a hole, or beside a cell the erosion clock has eaten, is
// against a wall as surely as one on the board's outer edge. Neither rule was written for the
// other; this is what falls out of the two of them, and the picture has to follow it.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Which sides of a cell are wall. A flags set, because a corner has two and the
    /// animation draws a front per side.</summary>
    [System.Flags]
    public enum BoardSides
    {
        None = 0,
        Left = 1,
        Right = 2,
        Down = 4,
        Up = 8
    }

    /// <summary>One cube freezing: where it is, what it WAS, and which walls it is against.
    /// </summary>
    public sealed class FrozenCell
    {
        public GridPos Cell;

        /// <summary>The cube as it stood before the freeze - the face the animation starts from.
        /// The board no longer has it.</summary>
        public Cube Was;

        /// <summary>The walls this cell touches. Never None: a cell with no wall beside it is not
        /// one Buzluk freezes.</summary>
        public BoardSides Sides;
    }

    /// <summary>
    /// One turn's freezing. A new SERIAL every time, so a repaint during the animation cannot
    /// restart it - and an empty report (a turn with no edge water on the board) is still a
    /// report, so the View can tell "nothing froze" from "nothing has happened yet".
    /// </summary>
    public sealed class FreezeVisuals
    {
        public int Serial;

        public readonly List<FrozenCell> Cells = new List<FrozenCell>();

        public bool Any
        {
            get { return Cells.Count > 0; }
        }
    }
}
