// PURPOSE: What "Deprem" brought down, written down for the VIEW. Reporting only: the collapse
// itself is unchanged.
//
// IT EXISTS BECAUSE THE VIEW MUST NOT WORK ANY OF THIS OUT. "A quarter of the destructible cubes,
// chosen at random" is the rule, and every part of it is a trap for a picture that tries to
// re-derive it: which cubes count as destructible (a protected host does not; obsidian and gold
// do not), how the quarter rounds, and above all WHICH ones the random draw took. So the report
// carries exactly the cells the engine actually emptied - the return of RoundEngine.DestroyCubes,
// not the list that was asked for, because a destroy can be refused - and the cube that stood in
// each one, snapshotted BEFORE it went, because by the time anything is drawn the board is empty
// there and "what fell" is the whole subject of the animation.
//
// IT IS NOT AN EXPLOSION AND THE REPORT SAYS SO BY BEING ITS OWN TYPE. The quake pays nothing and
// is never a clean sweep, and it must never be drawn through the explosion channel - a cluster
// burst over these cells would tell the player a joker blew them up for points.
//
// A NEW OBJECT PER QUAKE, and the View keys on that identity (see CLAUDE.md, "a per-turn report
// is matched by identity"). The Seed is presentation only - it decides the wave order, the fissure
// shapes and the tilt - and comes from the cells themselves, never from the round's random source.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One quake: what came down, in the order the engine emptied it.</summary>
    public sealed class QuakeVisuals
    {
        /// <summary>The cells the engine actually emptied.</summary>
        public readonly List<GridPos> Cells = new List<GridPos>();

        /// <summary>The cube that stood in each of those cells, same order, taken before it went.
        /// </summary>
        public readonly List<Cube> Cubes = new List<Cube>();

        /// <summary>Presentation seed, derived from the cells. Never the round's random source.
        /// </summary>
        public uint Seed;

        public bool Any
        {
            get { return Cells.Count > 0; }
        }
    }
}
