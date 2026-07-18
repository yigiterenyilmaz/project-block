// PURPOSE: Where block shapes come from. The base game uses fully random polyominoes
// (confirmed design choice: "the blocks must all be random").
// EXTENSION POINT: future deck types from the design plan ("small blocks weighted",
// "big blocks weighted", curated tetromino sets...) are new IShapeGenerator
// implementations or different SizeWeight tables - nothing else has to change.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Produces block shapes for new cards.</summary>
    public interface IShapeGenerator
    {
        BlockShape NextShape(IRandomSource rng);
    }

    /// <summary>
    /// Generates random connected polyominoes by growing from a single cube.
    /// Cube count is picked from a weighted table (default sizes 1-5).
    /// </summary>
    public sealed class RandomPolyominoGenerator : IShapeGenerator
    {
        /// <summary>One entry of the cube-count probability table.</summary>
        public readonly struct SizeWeight
        {
            public readonly int Size;
            public readonly int Weight;

            public SizeWeight(int size, int weight)
            {
                if (size < 1) throw new ArgumentException("Size must be >= 1.");
                if (weight < 1) throw new ArgumentException("Weight must be >= 1.");
                Size = size;
                Weight = weight;
            }
        }

        private static readonly GridPos[] Directions =
        {
            new GridPos(1, 0),
            new GridPos(-1, 0),
            new GridPos(0, 1),
            new GridPos(0, -1)
        };

        private readonly SizeWeight[] weights;
        private readonly int totalWeight;

        /// <summary>Default distribution; tune freely, it is game balance, not a rule.</summary>
        public RandomPolyominoGenerator()
            : this(new[]
            {
                new SizeWeight(1, 8),
                new SizeWeight(2, 16),
                new SizeWeight(3, 26),
                new SizeWeight(4, 32),
                new SizeWeight(5, 18)
            })
        {
        }

        public RandomPolyominoGenerator(IEnumerable<SizeWeight> sizeWeights)
        {
            weights = new List<SizeWeight>(sizeWeights).ToArray();
            if (weights.Length == 0)
            {
                throw new ArgumentException("At least one size weight is required.");
            }
            totalWeight = 0;
            foreach (SizeWeight w in weights)
            {
                totalWeight += w.Weight;
            }
        }

        public BlockShape NextShape(IRandomSource rng)
        {
            int size = PickSize(rng);
            var cellList = new List<GridPos> { new GridPos(0, 0) };
            var cellSet = new HashSet<GridPos> { new GridPos(0, 0) };
            while (cellList.Count < size)
            {
                GridPos anchor = cellList[rng.NextInt(0, cellList.Count)];
                GridPos candidate = anchor + Directions[rng.NextInt(0, Directions.Length)];
                if (cellSet.Add(candidate))
                {
                    cellList.Add(candidate);
                }
            }
            return BlockShape.FromCells(cellList);
        }

        private int PickSize(IRandomSource rng)
        {
            int roll = rng.NextInt(0, totalWeight);
            foreach (SizeWeight w in weights)
            {
                roll -= w.Weight;
                if (roll < 0)
                {
                    return w.Size;
                }
            }
            return weights[weights.Length - 1].Size;
        }
    }
}
