// PURPOSE: Decides the setup (board size + threshold) for any round number.
// Must be deterministic. New difficulty curves / boss rounds are new implementations.

using System;

namespace ProjectBlock.Core
{
    /// <summary>Provides the setup for any round number. Must be deterministic.</summary>
    public interface IRoundProgression
    {
        RoundConfig GetRound(int roundNumber);
    }
}
