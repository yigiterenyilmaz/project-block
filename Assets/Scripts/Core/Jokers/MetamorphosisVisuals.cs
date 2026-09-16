// PURPOSE: "Metamorfoz" telling the View what is ripening and what just turned. REPORTING ONLY:
// the rules have already aged the cubes and changed the ones that were ready.
//
// THE BUILD-UP IS THE WHOLE MECHANIC AND IT WAS INVISIBLE. A plain cube that has stood six turns
// and one that landed this turn looked identical, and on the seventh one of them silently became
// gold - which is a block that never breaks and blocks a clean sweep. The player was being asked
// to plan around a clock they could not see, and then punished by it.
//
// So every tracked cube carries its PROGRESS, and the cells that turned this turn are named
// separately: the slow build and the moment of change are different events and the View draws
// them differently.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One turn of ripening. A NEW object per turn, matched by identity.</summary>
    public sealed class MetamorphosisVisuals
    {
        /// <summary>Every plain cube on the clock, in board order.</summary>
        public readonly List<GridPos> Ripening = new List<GridPos>();

        /// <summary>How far along each is, 0 to 1. Carried rather than derived so the View never
        /// needs to know how many turns ripening takes.</summary>
        public readonly List<float> Progress = new List<float>();

        /// <summary>Turns each still has to wait - what a readout prints.</summary>
        public readonly List<int> TurnsLeft = new List<int>();

        /// <summary>The cells that became GOLD this turn. Not in Ripening: they are done.</summary>
        public readonly List<GridPos> Turned = new List<GridPos>();

        public int Count
        {
            get { return Ripening.Count; }
        }

        public void AddRipening(GridPos cell, int turnsHeld, int turnsNeeded)
        {
            Ripening.Add(cell);
            Progress.Add(turnsNeeded > 0 ? turnsHeld / (float)turnsNeeded : 1f);
            TurnsLeft.Add(turnsNeeded - turnsHeld);
        }

        public void AddTurned(GridPos cell)
        {
            Turned.Add(cell);
        }
    }
}
