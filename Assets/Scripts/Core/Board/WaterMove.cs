// PURPOSE: One water cube dropping one cell, recorded so the View can animate the
// fall step by step.

using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectBlock.Core
{
    /// <summary>One water cube dropping one cell (for the UI's fall animation).</summary>
    public readonly struct WaterMove
    {
        public readonly GridPos From;
        public readonly GridPos To;

        public WaterMove(GridPos from, GridPos to)
        {
            From = from;
            To = to;
        }
    }
}
