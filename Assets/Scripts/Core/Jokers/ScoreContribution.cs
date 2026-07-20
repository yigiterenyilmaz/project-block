// PURPOSE: One (source, amount) entry of a turn's score - a joker DefId or a "base."
// source, plus the flat points and/or multiplier it applied. Kept as a per-source
// log for UI score popups and debugging joker interactions.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One (source, amount) entry of a turn's score, for UI popups and debugging.</summary>
    public readonly struct ScoreContribution
    {
        /// <summary>Joker DefId, or one of the "base." sources produced by the engine.</summary>
        public readonly string Source;

        /// <summary>Flat points added, or 0 for multiplier entries.</summary>
        public readonly int Flat;

        /// <summary>Multiplier applied, or 1.0 for flat entries.</summary>
        public readonly double Multiplier;

        public ScoreContribution(string source, int flat, double multiplier)
        {
            Source = source;
            Flat = flat;
            Multiplier = multiplier;
        }
    }
}
