// PURPOSE: The default deterministic IRandomSource, seeded from an int, and the one thing
// that makes a mid-run save possible: it can be RESTORED to any earlier position.
//
// WHY REPLAY RATHER THAN A NEW PRNG. System.Random will not tell you where it is, so a save
// cannot simply write its state. The obvious fix - swap in a PRNG whose state IS readable -
// would change every random draw in the game: the baseline regression trace would have to be
// re-cut and every shared seed would produce a different run. So instead this records the
// SHAPE of the draws made so far and, on load, re-runs that many draws from the seed. The
// random stream is untouched, byte for byte.
//
// The log is stored RUN-LENGTH ENCODED (kind + how many in a row). Shuffles make thousands of
// consecutive NextInt calls, so a whole run compresses to a handful of runs - the log costs
// almost nothing in memory or in the save file.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Default IRandomSource backed by System.Random with a known seed.</summary>
    public sealed class SeededRandom : IRandomSource
    {
        /// <summary>Draw kinds in the log. Both consume exactly one internal sample from a
        /// seeded System.Random, but they are told apart anyway so the replay never has to
        /// rely on that being true.</summary>
        private const int DrawInt = 0;
        private const int DrawDouble = 1;

        private System.Random random;

        private readonly List<int> runKinds = new List<int>();
        private readonly List<int> runLengths = new List<int>();

        public int Seed { get; }

        public SeededRandom(int seed)
        {
            Seed = seed;
            random = new System.Random(seed);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            // A range wider than int.MaxValue makes System.Random consume TWO internal samples
            // instead of one, which the replay below could not reproduce. Nothing in the game
            // asks for one (every range is a collection count), so it is refused outright
            // rather than left as a silent way to corrupt a restored run.
            if ((long)maxExclusive - minInclusive > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException("maxExclusive",
                    "Range is too wide to be replayable by a save.");
            }
            Note(DrawInt);
            return random.Next(minInclusive, maxExclusive);
        }

        public double NextDouble()
        {
            Note(DrawDouble);
            return random.NextDouble();
        }

        /// <summary>How many draws have been taken since the seed. Diagnostics only.</summary>
        public int DrawCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < runLengths.Count; i++)
                {
                    total += runLengths[i];
                }
                return total;
            }
        }

        private void Note(int kind)
        {
            int last = runKinds.Count - 1;
            if (last >= 0 && runKinds[last] == kind)
            {
                runLengths[last]++;
                return;
            }
            runKinds.Add(kind);
            runLengths.Add(1);
        }

        /// <summary>Writes the draw log. The seed goes with it so a restore can prove it is
        /// rebuilding the same stream.</summary>
        internal void Save(SaveWriter w, string key)
        {
            w.Write(key + ".seed", Seed);
            w.Write(key + ".runs", runKinds.Count);
            for (int i = 0; i < runKinds.Count; i++)
            {
                w.Write(key + ".kind." + i, runKinds[i]);
                w.Write(key + ".len." + i, runLengths[i]);
            }
        }

        /// <summary>Rebuilds a source sitting exactly where the saved one was: a fresh
        /// System.Random on the saved seed, wound forward by re-taking every logged draw. The
        /// values are thrown away - only the position matters.</summary>
        internal static SeededRandom Load(SaveReader r, string key)
        {
            int seed = r.ReadInt(key + ".seed");
            var restored = new SeededRandom(seed);
            int runs = r.ReadInt(key + ".runs");
            for (int i = 0; i < runs; i++)
            {
                int kind = r.ReadInt(key + ".kind." + i);
                int length = r.ReadInt(key + ".len." + i);
                for (int n = 0; n < length; n++)
                {
                    if (kind == DrawDouble)
                    {
                        restored.NextDouble();
                    }
                    else
                    {
                        // Any small range consumes the same single sample the original did.
                        restored.NextInt(0, 2);
                    }
                }
            }
            return restored;
        }
    }
}
