// PURPOSE: All randomness in the core goes through IRandomSource so a whole game
// can be replayed deterministically from a single seed (debugging, tests, replays,
// and future features like the "Kum saati" rewind power depend on this).
// RULE FOR AGENTS: never use UnityEngine.Random or an un-seeded System.Random
// inside ProjectBlock.Core - always take an IRandomSource.

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

    /// <summary>Default IRandomSource backed by System.Random with a known seed.</summary>
    public sealed class SeededRandom : IRandomSource
    {
        private readonly System.Random random;

        public int Seed { get; }

        public SeededRandom(int seed)
        {
            Seed = seed;
            random = new System.Random(seed);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return random.Next(minInclusive, maxExclusive);
        }

        public double NextDouble()
        {
            return random.NextDouble();
        }
    }

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
