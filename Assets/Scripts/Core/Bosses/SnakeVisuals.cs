// PURPOSE: What the VIEW is told about one of "Snake"'s turns. REPORTING ONLY - every number in
// here is a copy of something the rules already did, taken as they did it, and nothing in the rules
// ever reads it back.
//
// The snake is the one boss whose turn is a SEQUENCE rather than a state change: lines the player
// cleared cut segments off its tail, and then it slides, possibly through several cells, possibly
// eating what stopped it. A View given only "the body was here, now it is there" would have to
// invent the middle - which way it went, where it stopped, what it ate, which segment left the
// tail - and every one of those guesses is a way for the animation to disagree with the rules.
//
// So the boss writes down, in order: the body before the cuts, each tail cell that was cut and the
// body after each cut, whether that killed it, then whether it had anywhere to go, which way it
// went, THE BODY AFTER EVERY SINGLE CELL OF THE SLIDE, and what it ate if it ate. The View plays
// exactly that.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One snake turn as it happened, for the View. Reporting only.</summary>
    public sealed class SnakeTurnVisuals
    {
        internal SnakeTurnVisuals(int turn, IReadOnlyList<GridPos> bodyBeforeCuts)
        {
            Turn = turn;
            BodyBeforeCuts = Copy(bodyBeforeCuts);
            BodyAfter = BodyBeforeCuts;
        }

        /// <summary>Which turn of the round this is, so the View can tell a new report from one it
        /// has already played.</summary>
        public int Turn { get; private set; }

        // ---- the cuts, which the rules do FIRST ------------------------------------------

        /// <summary>The whole snake, head first, as it stood when the turn ended.</summary>
        public IReadOnlyList<GridPos> BodyBeforeCuts { get; private set; }

        /// <summary>How many segments the exploding lines took. The View must never count lines
        /// itself: this is the number the rules used.</summary>
        public int CutCount
        {
            get { return removedTailCells.Count; }
        }

        private readonly List<int> cutRows = new List<int>();

        private readonly List<int> cutColumns = new List<int>();

        /// <summary>The ABSOLUTE rows that exploded with the snake standing in them - one cut each,
        /// and where the player's line actually crossed it.</summary>
        public IReadOnlyList<int> CutRows
        {
            get { return cutRows; }
        }

        public IReadOnlyList<int> CutColumns
        {
            get { return cutColumns; }
        }

        private readonly List<GridPos> removedTailCells = new List<GridPos>();

        /// <summary>The tail cells that were cut, in the order they went.</summary>
        public IReadOnlyList<GridPos> RemovedTailCells
        {
            get { return removedTailCells; }
        }

        private readonly List<IReadOnlyList<GridPos>> bodyAfterEachCut =
            new List<IReadOnlyList<GridPos>>();

        /// <summary>The whole snake after each of those cuts, so a multi-cut turn can be played as
        /// the sequence it was.</summary>
        public IReadOnlyList<IReadOnlyList<GridPos>> BodyAfterEachCut
        {
            get { return bodyAfterEachCut; }
        }

        /// <summary>The last cut finished it: the round is won, and this turn has no move.</summary>
        public bool Defeated { get; private set; }

        // ---- and then the move ----------------------------------------------------------

        /// <summary>It had nowhere to go at all and stayed where it was.</summary>
        public bool WasStuck { get; private set; }

        /// <summary>The one-cell step it slid along, or (0,0) if it did not move.</summary>
        public GridPos MoveStep { get; private set; }

        private readonly List<IReadOnlyList<GridPos>> stepSnapshots =
            new List<IReadOnlyList<GridPos>>();

        /// <summary>The whole snake after EACH cell of the slide - the movement the rules really
        /// made, cell by cell, for the View to follow instead of interpolating between two states.</summary>
        public IReadOnlyList<IReadOnlyList<GridPos>> StepSnapshots
        {
            get { return stepSnapshots; }
        }

        /// <summary>The cell it stopped in because something was standing there - the cell it ate.</summary>
        public GridPos? EatenCell { get; private set; }

        /// <summary>What was standing there, taken before the rules removed it, so the View can show
        /// that block being swallowed rather than a stand-in.</summary>
        public Cube? EatenCube { get; private set; }

        /// <summary>It ate, so it kept its tail and is one segment longer.</summary>
        public bool GrowthOccurred { get; private set; }

        /// <summary>The snake as it stands now, head first.</summary>
        public IReadOnlyList<GridPos> BodyAfter { get; private set; }

        // ---- what the boss writes into it ----------------------------------------------

        internal void NoteCutLine(bool isRow, int absolute)
        {
            (isRow ? cutRows : cutColumns).Add(absolute);
        }

        internal void NoteTailCut(GridPos tail, IReadOnlyList<GridPos> bodyAfter)
        {
            removedTailCells.Add(tail);
            bodyAfterEachCut.Add(Copy(bodyAfter));
        }

        internal void NoteDefeated()
        {
            Defeated = true;
        }

        internal void NoteStuck()
        {
            WasStuck = true;
        }

        internal void NoteDirection(GridPos step)
        {
            MoveStep = step;
        }

        internal void NoteStep(IReadOnlyList<GridPos> bodyNow)
        {
            stepSnapshots.Add(Copy(bodyNow));
        }

        internal void NoteEaten(GridPos cell, Cube cube)
        {
            EatenCell = cell;
            EatenCube = cube;
            GrowthOccurred = true;
        }

        internal void NoteBodyAfter(IReadOnlyList<GridPos> bodyNow)
        {
            BodyAfter = Copy(bodyNow);
        }

        private static IReadOnlyList<GridPos> Copy(IReadOnlyList<GridPos> cells)
        {
            var copy = new List<GridPos>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                copy.Add(cells[i]);
            }
            return copy;
        }
    }
}
