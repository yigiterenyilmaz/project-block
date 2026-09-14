// PURPOSE: What "Mapus" did with its seal this turn, written down for the VIEW. Reporting only:
// nothing here decides anything, nothing here is saved, and the rules read exactly the same with
// it as without it.
//
// It exists because the seal's presentation turns on facts the View must never work out for
// itself. Whether the seal MOVED (a whole prison is torn down and rebuilt elsewhere) or STAYED
// (the same one holds for another turn) is the difference between the boss's biggest animation and
// its smallest; whether the cap just RELEASED a cell is the one turn the player has to finish the
// line it was denying, and that has to read as the warden letting go rather than as the seal
// wandering off; and how close the sealed cell's row and column are to completion is what tells
// the suppression how hard to press - "this line is being held by exactly this cell" is a real
// state, and a View that guessed it would be inventing a second definition of a full line beside
// the one the explosion rule uses.
//
// The boss keeps its copy [NotSaved]: it is rebuilt by the next turn, means nothing across a load,
// and is not state. See SnakeTurnVisuals, PressCompressionVisuals - the same bargain.

namespace ProjectBlock.Core
{
    /// <summary>
    /// One turn of the seal, from the boss's own retarget. Every field is a fact the rules already
    /// had; the View plays it and adds nothing.
    /// </summary>
    public sealed class MapusSealVisuals
    {
        /// <summary>Where the seal stood before this retarget, and whether there was one at all.
        /// </summary>
        public GridPos PreviousCell;

        public bool HadPrevious;

        /// <summary>Where it stands now. HasSeal is false when the board had too few free cells,
        /// or when the cap released the only cell worth taking - the board breathes that turn.
        /// </summary>
        public GridPos Cell;

        public bool HasSeal;

        /// <summary>True when this retarget put the seal somewhere else. False means the SAME cell
        /// is held for another turn, which must not play the whole deploy again.</summary>
        public bool Moved;

        /// <summary>How many turns running the seal has now held this cell, and the most it may.
        /// The count is the rules' own (MapusBoss.MaxTurnsOnOneCell), never a View clock.</summary>
        public int TurnsHeld;

        public int MaxTurns;

        /// <summary>True on the turn the CAP fired: the cell was let go because it had been held
        /// too long, not because somewhere else was worse. That is the player's window, and it is
        /// the one release the animation should make something of.</summary>
        public bool Released;

        /// <summary>How many cubes the sealed cell's row and column still want, or -1 when that
        /// line can never explode anyway (GameBoard.RowGapCount / ColumnGapCount). 1 means this
        /// seal is the only thing holding the line - which the suppression presses harder for.
        /// </summary>
        public int RowGaps;

        public int ColumnGaps;

        /// <summary>True when the seal is the LAST thing standing between the player and that
        /// line: every other required cell of it is filled. Worked out by the rules, from the same
        /// count the explosion uses.</summary>
        public bool RowHeldByTheSealAlone;

        public bool ColumnHeldByTheSealAlone;

        public void Clear()
        {
            HadPrevious = false;
            HasSeal = false;
            Moved = false;
            Released = false;
            TurnsHeld = 0;
            MaxTurns = 0;
            RowGaps = -1;
            ColumnGaps = -1;
            RowHeldByTheSealAlone = false;
            ColumnHeldByTheSealAlone = false;
        }
    }
}
