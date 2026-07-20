// PURPOSE: An IShapeGenerator that draws uniformly from a fixed pool of shapes.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Picks uniformly from a fixed pool of shapes (used by curated decks).</summary>
    public sealed class ShapePoolGenerator : IShapeGenerator
    {
        private readonly BlockShape[] pool;

        public ShapePoolGenerator(IEnumerable<BlockShape> shapes)
        {
            pool = new List<BlockShape>(shapes).ToArray();
            if (pool.Length == 0)
            {
                throw new ArgumentException("Shape pool cannot be empty.");
            }
        }

        public BlockShape NextShape(IRandomSource rng)
        {
            return pool[rng.NextInt(0, pool.Length)];
        }
    }
}
