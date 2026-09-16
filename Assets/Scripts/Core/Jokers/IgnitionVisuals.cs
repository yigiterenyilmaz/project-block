// PURPOSE: What "Tutuştur" burned, written down for the VIEW. Reporting only: which fire goes and
// whether it pays are unchanged.
//
// IT EXISTS BECAUSE THE CHAIN WAS INVISIBLE. The joker takes every fire cube on the board through
// RoundEngine.DestroyCubes the moment a fire cube goes up, and that destruction reaches no
// explosion list - so the far fires simply vanished with the repaint while only the line that
// set them off was drawn. The View must not work the chain out again ("every fire cube on the
// board" is not it: a destroy can be refused, and which fire set it off is the turn's own log), so
// the report carries exactly the cells DestroyCubes RETURNED and the cube in each, taken BEFORE
// it went, plus the fire cells that had already gone up this turn - the ones that lit the fuse.
//
// THE POINTS ARE MEASURED. The chain pays only under "Genel temizlik", and when it does, what it
// paid is read off the turn's breakdown around the payment at the score's scale - so the report
// says 0 when it paid nothing, and an inverted round's sign when it ran backwards.
//
// A NEW OBJECT PER CHAIN, matched by IDENTITY in the View. Seed is presentation only (row jitter,
// smoke, embers) and comes from the cells.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    public sealed class IgnitionVisuals
    {
        /// <summary>The fire cells that had already gone up this turn - what lit the chain.</summary>
        public readonly List<GridPos> SourceCells = new List<GridPos>();

        /// <summary>The fire cells the chain actually emptied, in board order.</summary>
        public readonly List<GridPos> Cells = new List<GridPos>();

        /// <summary>The cube that stood in each, same order, taken before it went.</summary>
        public readonly List<Cube> Cubes = new List<Cube>();

        /// <summary>What the chain paid, at the score's scale (signed; 0 when it paid nothing).
        /// </summary>
        public int Points;

        public uint Seed;

        public int Count
        {
            get { return Cells.Count; }
        }

        public int PointsEach
        {
            get { return Cells.Count == 0 ? 0 : Points / Cells.Count; }
        }
    }
}
