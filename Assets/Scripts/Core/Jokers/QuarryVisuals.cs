// PURPOSE: What "Elmas Kazma" broke on a clean sweep, written down for the VIEW. Reporting only:
// which obsidian goes and what it pays are unchanged.
//
// IT EXISTS BECAUSE THE BREAK WAS INVISIBLE. The joker destroys the obsidian through the engine's
// forced destruction, which never reaches an explosion list, so the cubes simply vanished with
// the repaint that the sweep's own wave was drawn over - and the player could not tell the
// sweep's cubes from the stone the pickaxe took. The View must not work out which cubes those
// were ("every obsidian on the board" is not it: a destroy can be refused), so the report
// carries exactly the cells DestroyCubes RETURNED, and the cube that stood in each, taken BEFORE
// it went, because the whole animation is about that cube.
//
// THE POINTS ARE MEASURED. What the joker paid is read off the turn's own breakdown around the
// payment (flat and late flat, at the score's scale), so an inverted round reports what it
// really did. A clean sweep usually resolves before the turn's score is finalised, and the
// turn's multipliers then apply to the whole turn - this is the joker's share going in, which is
// what its own number on the board should say.
//
// A NEW OBJECT PER SWEEP, matched by IDENTITY in the View. Seed is presentation only (crack
// shapes, strike angles, shard scatter) and comes from the cells.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    public sealed class QuarryVisuals
    {
        /// <summary>The obsidian cells the engine actually emptied, in board order.</summary>
        public readonly List<GridPos> Cells = new List<GridPos>();

        /// <summary>The cube that stood in each, same order, taken before it went.</summary>
        public readonly List<Cube> Cubes = new List<Cube>();

        /// <summary>What the joker paid for all of them, at the score's scale (signed).</summary>
        public int Points;

        public uint Seed;

        public int Count
        {
            get { return Cells.Count; }
        }

        /// <summary>One cube's share. The payment is linear in the count, so this is exact.
        /// </summary>
        public int PointsEach
        {
            get { return Cells.Count == 0 ? 0 : Points / Cells.Count; }
        }
    }
}
