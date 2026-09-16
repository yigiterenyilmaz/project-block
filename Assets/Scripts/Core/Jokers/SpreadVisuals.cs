// PURPOSE: What a SPREAD joker did - "Yangın" turning a ring of cubes to fire, "Taşkın" to
// water - written down for the VIEW. Reporting only: the conversion itself is unchanged.
//
// IT EXISTS BECAUSE THE ANIMATION HAS TO TELL THE RULE, and the rule is not "some cubes became
// fire". It is:
//
//   THE SOURCES are the cubes that were ALREADY that kind when the joker was used. They are the
//   ones the fire comes OUT of, and nothing else may be drawn spreading.
//   THE TARGETS are their four-neighbours, collected BEFORE any of them was converted - which is
//   why a single use spreads exactly ONE RING. A cube that catches does not go on to light its
//   own neighbours, and an animation that shows it doing so is teaching a rule the game does not
//   have.
//   THE DIRECTIONS matter: a target reached from the left has to catch on its LEFT edge. Without
//   this the View has to guess, and a guess puts the scorch on the wrong side of the cube.
//
// A target may be reached from MORE THAN ONE source, and that is carried too - the View draws one
// transformation with an extra mark per extra side, rather than stacking two whole animations on
// one cube.
//
// THE OLD CUBE IS CARRIED WITH IT. The board has already been converted by the time anything is
// drawn, so the View cannot ask it what used to be there - and "what used to be there" is the
// whole first half of the animation.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One cube catching: where it is, what it WAS, and which side(s) it caught from.
    /// </summary>
    public sealed class SpreadIgnition
    {
        public GridPos Cell;

        /// <summary>The cube as it stood before the conversion - the face the animation starts
        /// from. The board no longer has it.</summary>
        public Cube Was;

        /// <summary>The source cells that reached it, in board order. One for an ordinary catch,
        /// more where two fires met on the same cube.</summary>
        public readonly List<GridPos> From = new List<GridPos>();
    }

    /// <summary>
    /// One use of a spread joker. A new SERIAL every time, so a repaint during the animation
    /// cannot restart it.
    /// </summary>
    public sealed class SpreadVisuals
    {
        public int Serial;

        /// <summary>What everything is turning into.</summary>
        public CubeKind Kind;

        /// <summary>The cubes that were already that kind when the joker was used. ONLY these
        /// spread; see the file header.</summary>
        public readonly List<GridPos> Sources = new List<GridPos>();

        public readonly List<SpreadIgnition> Targets = new List<SpreadIgnition>();

        public bool Any
        {
            get { return Targets.Count > 0; }
        }
    }
}
