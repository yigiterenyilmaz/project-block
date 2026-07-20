// PURPOSE: One cube removed from the board during a turn, carrying the KIND and
// SOURCE CARD it held - both gone from the board by the time a joker hook runs.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One cube that was removed from the board during a turn, with the value it
    /// held. Jokers need the KIND (was it fire? ice?) and the SOURCE CARD, both of which
    /// are gone from the board by the time a hook runs.</summary>
    public readonly struct DestroyedCube
    {
        public readonly GridPos Pos;
        public readonly Cube Cube;

        public DestroyedCube(GridPos pos, Cube cube)
        {
            Pos = pos;
            Cube = cube;
        }
    }
}
