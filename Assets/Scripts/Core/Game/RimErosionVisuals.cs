// PURPOSE: What the anti-stalling clock's RIM EROSION took off the arena, written down for the
// VIEW. Reporting only: the erosion itself is unchanged and nothing here is read back by a rule.
//
// IT EXISTS BECAUSE THE ARENA USED TO SIMPLY SNAP. The rim goes in one call (the bands' cubes
// destroyed, the board object replaced at its new size) and the View rebuilt the grid at the new
// cell count on the same frame - one row and one column gone with no picture at all. To play the
// rim crumbling away the View needs to know three things it must not work out for itself: the
// rectangle the arena had, the one it has now (several steps can land on one turn, and which
// sides each step takes is the engine's rule), and which cubes the bands were holding.
//
// A NEW OBJECT PER EROSION, and the View keys on that identity (see CLAUDE.md, "a per-turn report
// is matched by identity"). The centre's dead zone is not in it: it grows in place and takes no
// ground away.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One application of the rim erosion: the arena before and after, and what stood in
    /// the bands it lost.</summary>
    public sealed class RimErosionVisuals
    {
        /// <summary>The board's bounding box before the erosion.</summary>
        public int BeforeMinX;
        public int BeforeMinY;
        public int BeforeWidth;
        public int BeforeHeight;

        /// <summary>And after it.</summary>
        public int AfterMinX;
        public int AfterMinY;
        public int AfterWidth;
        public int AfterHeight;

        /// <summary>Erosion steps this application took (usually one).</summary>
        public int Steps;

        /// <summary>The cells whose cubes the engine actually destroyed in the doomed bands - the
        /// return of RoundEngine.DestroyCubes, never the list asked for.</summary>
        public readonly List<GridPos> DestroyedCells = new List<GridPos>();

        /// <summary>The cube that stood in each of those cells, same order, taken before it went -
        /// the board the View still shows may not have it yet (a block placed into the band on
        /// the very turn the band went).</summary>
        public readonly List<Cube> DestroyedCubes = new List<Cube>();

        /// <summary>True when the cell lay inside the arena before and outside it now.</summary>
        public bool WasRemoved(GridPos cell)
        {
            return InBefore(cell) && !InAfter(cell);
        }

        public bool InBefore(GridPos cell)
        {
            return cell.X >= BeforeMinX && cell.X < BeforeMinX + BeforeWidth
                && cell.Y >= BeforeMinY && cell.Y < BeforeMinY + BeforeHeight;
        }

        public bool InAfter(GridPos cell)
        {
            return cell.X >= AfterMinX && cell.X < AfterMinX + AfterWidth
                && cell.Y >= AfterMinY && cell.Y < AfterMinY + AfterHeight;
        }
    }
}
