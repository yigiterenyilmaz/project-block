// PURPOSE: Shared helpers over IRandomSource (Fisher-Yates shuffle, etc.) so every
// caller shuffles the same unbiased way.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Shared random helpers.</summary>
    public static class RandomSourceExtensions
    {
        /// <summary>In-place Fisher-Yates shuffle.</summary>
        public static void Shuffle<T>(this IRandomSource rng, IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
