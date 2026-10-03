// PURPOSE: What one "Çığ" did, written down for the VIEW. Reporting only: nothing here is read
// back by a rule.
//
// THE VIEW MUST NOT WORK ANY OF THIS OUT. Which heaps on the line could slide, how far each
// column came down before something stopped it, which cubes were in the way and which of those
// actually paid (snow run over by snow does not) are all the rules' answers - and by the time
// anything is drawn the board has already settled, so the heaps are no longer where they slid
// from and the cubes they crushed are gone.
//
// A NEW OBJECT PER AVALANCHE, matched by identity (see CLAUDE.md, "a per-turn report is matched
// by identity").

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One avalanche: the plan the rules carried out and what it paid.</summary>
    public sealed class AvalancheVisuals
    {
        /// <summary>Column by column: the heap cube that slid, the cells it covered (nearest
        /// first) and the cube that stood in each one that held any.</summary>
        public AvalanchePlan Plan;

        /// <summary>The cells that took a packed layer, BEFORE the settle that followed.</summary>
        public readonly List<GridPos> Laid = new List<GridPos>();

        /// <summary>What each crushed cube that paid was worth, logical (CigPower.PointsPerCrushedCube).</summary>
        public int PointsPerCube;

        /// <summary>Crushed cubes that paid (everything but snow).</summary>
        public int CubesPaid;

        /// <summary>What the round's score actually moved by, at the score's own scale.</summary>
        public int Points;
    }
}
