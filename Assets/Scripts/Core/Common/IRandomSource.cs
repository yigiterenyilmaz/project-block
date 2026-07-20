// PURPOSE: The one seeded randomness abstraction for Core. Everything random in a
// run draws from an IRandomSource so playthroughs are deterministic and replayable.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Deterministic random number provider used by every core system.</summary>
    public interface IRandomSource
    {
        /// <summary>Returns an int in [minInclusive, maxExclusive).</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Returns a double in [0, 1).</summary>
        double NextDouble();
    }
}
