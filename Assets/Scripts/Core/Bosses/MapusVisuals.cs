// PURPOSE: What "Mapus" did with its locks this turn, written down for the VIEW. Reporting only:
// nothing here decides anything, nothing here is saved, and the rules read exactly the same with
// it as without it.
//
// It exists because the locks' presentation turns on facts the View must never work out for
// itself. Which locks STAND (there are one or two at a time - one goes up every second turn and
// each lives three); whether one went UP this turn (the boss's biggest animation) or the same ones
// simply stand another turn (its smallest); whose time ran OUT, which is the one turn the player
// has to finish the line it was denying and has to read as the warden letting go; and how close a
// locked cell's row and column are to completion, which tells the suppression how hard to press -
// "this line is being held by exactly this cell" is a real state, and a View that guessed it
// would be inventing a second definition of a full line beside the one the explosion rule uses.
//
// The boss keeps its copy [NotSaved]: it is rebuilt by the next turn, means nothing across a load,
// and is not state. See SnakeTurnVisuals, PressCompressionVisuals - the same bargain.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One lock as it stands after the boss's turn.</summary>
    public struct MapusSeal
    {
        public GridPos Cell;

        /// <summary>Turns this lock still stands, counting the one about to be played. The count
        /// is the rules' own (MapusBoss.SealTurns), never a View clock.</summary>
        public int TurnsLeft;

        /// <summary>True for the lock that went up THIS turn.</summary>
        public bool IsNew;

        /// <summary>How many cubes the locked cell's row and column still want, or -1 when that
        /// line can never explode anyway (GameBoard.RowGapCount / ColumnGapCount). 1 means this
        /// lock is the only thing holding the line - which the suppression presses harder for.
        /// </summary>
        public int RowGaps;

        public int ColumnGaps;

        /// <summary>True when the lock is the LAST thing standing between the player and that
        /// line: every other required cell of it is filled. Worked out by the rules, from the same
        /// count the explosion uses.</summary>
        public bool RowHeldByTheSealAlone;

        public bool ColumnHeldByTheSealAlone;
    }

    /// <summary>
    /// One turn of the locks, from the boss's own turn end. Every field is a fact the rules already
    /// had; the View plays it and adds nothing.
    /// </summary>
    public sealed class MapusSealVisuals
    {
        /// <summary>Every lock standing now, oldest first. Empty when the board breathes.</summary>
        public readonly List<MapusSeal> Seals = new List<MapusSeal>();

        /// <summary>Cells whose lock ran out of turns this turn. Each is open again, and may not
        /// be re-locked by this same turn's pick - the player's window, and the one release the
        /// animation should make something of.</summary>
        public readonly List<GridPos> Expired = new List<GridPos>();

        /// <summary>True when a lock went up this turn; it is the last entry of Seals.</summary>
        public bool Placed;

        /// <summary>True when the new lock was aimed by the CARDS - a line the hand and the coming
        /// draws could clear while it stands. False when no line could go off in time and it fell
        /// back to the board alone (the cell whose lines are nearest completion).</summary>
        public bool AimedByCards;

        /// <summary>What the new lock's row and column measured, 0..1 (LineChance.Urgency): how
        /// surely and how soon the player could have cleared each. Zero when nothing was placed.
        /// </summary>
        public double PlacedRowThreat;

        public double PlacedColumnThreat;

        /// <summary>Turns until the next lock is due. 0 means one was due now and there was
        /// nowhere to put it - it is still owed.</summary>
        public int TurnsToNextSeal;

        /// <summary>How long a lock stands (MapusBoss.SealTurns).</summary>
        public int SealTurns;

        public void Clear()
        {
            Seals.Clear();
            Expired.Clear();
            Placed = false;
            AimedByCards = false;
            PlacedRowThreat = 0.0;
            PlacedColumnThreat = 0.0;
            TurnsToNextSeal = 0;
            SealTurns = 0;
        }
    }
}
