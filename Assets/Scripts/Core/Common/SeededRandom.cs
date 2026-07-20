// PURPOSE: The default deterministic IRandomSource, seeded from an int.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
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
}
