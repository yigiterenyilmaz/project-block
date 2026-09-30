// PURPOSE: What an "Antimadde" key annihilated and what the joker paid for it, written down for the
// VIEW. Reporting only: which cubes go, what they pay and how the key rots are all unchanged.
//
// IT EXISTS BECAUSE THE PAYOFF HAS A NUMBER ON IT. The annihilation is the ENGINE's (the turn
// resolver empties every cube of the kind when the key lands) and the TurnReport already names the
// kind and the cells; the bill is the JOKER's, paid in AfterTurnScored at whatever the rot left of
// the price. The view prints "+TOTAL" at the end of the event, and that total may not be worked
// out there - "60 a cube" is a balance placeholder, the rot is the joker's arithmetic, and an
// inverted round ("Terslik") runs a joker's flat backwards. So the points are MEASURED off the
// turn's own breakdown around the payment (flat and late flat, at the score's scale), exactly as
// "Elmas Kazma" measures its own.
//
// A NEW OBJECT PER LANDED KEY, matched by IDENTITY in the View. Seed is presentation only (ghost
// offsets, beam angles, particle scatter) and comes from the cells.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    public sealed class AntimatterVisuals
    {
        /// <summary>The cube kind the key annihilated.</summary>
        public CubeKind Kind;

        /// <summary>The cells the annihilation emptied, in the order the engine reported them.
        /// </summary>
        public readonly List<GridPos> Cells = new List<GridPos>();

        /// <summary>The cube that stood in each, same order, from the turn's destruction log.</summary>
        public readonly List<Cube> Cubes = new List<Cube>();

        /// <summary>What the joker paid for all of them, at the score's scale (signed - an
        /// inverted round reports what it really did).</summary>
        public int Points;

        /// <summary>What ONE cube was worth when the key landed, after the rot, at the score's
        /// scale and before any inversion.</summary>
        public int PointsPerCube;

        /// <summary>How many turns the key rotted in hand before it landed - the decay stage.
        /// </summary>
        public int TurnsHeld;

        /// <summary>The share of the full price the rot left, in percent (100 = a fresh key).
        /// </summary>
        public int RewardPercent;

        public uint Seed;

        public int Count
        {
            get { return Cells.Count; }
        }
    }
}
