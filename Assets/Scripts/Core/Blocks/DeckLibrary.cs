// PURPOSE: Deck archetypes - the design plan's "different deck types" ("küçük blokların
// ağırlıklı olduğu, büyük blokların ağırlıklı olduğu, tamamen rastgele..."). A deck is
// a named recipe: how many cards and which shape source. GameConfig.Deck picks one.
// EXTENSION POINT: unlockable decks are new DeckDefinition entries in DeckLibrary;
// per-deck special rules would extend DeckDefinition. Sizes/weights are balance
// placeholders.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>A named starting-deck recipe.</summary>
    public sealed class DeckDefinition
    {
        public string Name { get; }

        /// <summary>Cards in the starting deck. Must be at least the hand size.</summary>
        public int Size { get; }

        public IShapeGenerator ShapeGenerator { get; }

        public DeckDefinition(string name, int size, IShapeGenerator shapeGenerator)
        {
            Name = name;
            Size = size;
            ShapeGenerator = shapeGenerator;
        }
    }

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

    /// <summary>The built-in decks.</summary>
    public static class DeckLibrary
    {
        /// <summary>Curated pool of familiar pieces (dominoes, triominoes, tetrominoes
        /// with their rotations) - the friendliest deck, and the default.</summary>
        public static readonly DeckDefinition Classic = new DeckDefinition(
            "Classic", 24, new ShapePoolGenerator(ClassicPool()));

        /// <summary>Random polyominoes weighted toward 1-3 cubes.</summary>
        public static readonly DeckDefinition SmallBlocks = new DeckDefinition(
            "Small Blocks", 26, new RandomPolyominoGenerator(new[]
            {
                new RandomPolyominoGenerator.SizeWeight(1, 18),
                new RandomPolyominoGenerator.SizeWeight(2, 30),
                new RandomPolyominoGenerator.SizeWeight(3, 32),
                new RandomPolyominoGenerator.SizeWeight(4, 16),
                new RandomPolyominoGenerator.SizeWeight(5, 4)
            }));

        /// <summary>Random polyominoes weighted toward 4-5 cubes, no singles.</summary>
        public static readonly DeckDefinition BigBlocks = new DeckDefinition(
            "Big Blocks", 22, new RandomPolyominoGenerator(new[]
            {
                new RandomPolyominoGenerator.SizeWeight(2, 4),
                new RandomPolyominoGenerator.SizeWeight(3, 16),
                new RandomPolyominoGenerator.SizeWeight(4, 40),
                new RandomPolyominoGenerator.SizeWeight(5, 40)
            }));

        /// <summary>Fully random polyominoes, every size equally likely.</summary>
        public static readonly DeckDefinition Chaos = new DeckDefinition(
            "Chaos", 24, new RandomPolyominoGenerator(new[]
            {
                new RandomPolyominoGenerator.SizeWeight(1, 1),
                new RandomPolyominoGenerator.SizeWeight(2, 1),
                new RandomPolyominoGenerator.SizeWeight(3, 1),
                new RandomPolyominoGenerator.SizeWeight(4, 1),
                new RandomPolyominoGenerator.SizeWeight(5, 1)
            }));

        public static readonly IReadOnlyList<DeckDefinition> All = new[]
        {
            Classic, SmallBlocks, BigBlocks, Chaos
        };

        /// <summary>Builds a shape from an ASCII picture: '#' = cube, first string = TOP row.</summary>
        private static BlockShape Shape(params string[] rows)
        {
            var cells = new List<GridPos>();
            for (int r = 0; r < rows.Length; r++)
            {
                int y = rows.Length - 1 - r;
                for (int x = 0; x < rows[r].Length; x++)
                {
                    if (rows[r][x] == '#')
                    {
                        cells.Add(new GridPos(x, y));
                    }
                }
            }
            return BlockShape.FromCells(cells);
        }

        private static IEnumerable<BlockShape> ClassicPool()
        {
            return new[]
            {
                Shape("#"),
                Shape("##"),
                Shape("#",
                      "#"),
                Shape("###"),
                Shape("#",
                      "#",
                      "#"),
                Shape("#.",
                      "##"),
                Shape(".#",
                      "##"),
                Shape("##",
                      "#."),
                Shape("##",
                      ".#"),
                Shape("##",
                      "##"),
                Shape("####"),
                Shape("#",
                      "#",
                      "#",
                      "#"),
                Shape("###",
                      ".#."),
                Shape(".#.",
                      "###"),
                Shape("#.",
                      "##",
                      "#."),
                Shape(".#",
                      "##",
                      ".#"),
                Shape(".##",
                      "##."),
                Shape("#.",
                      "##",
                      ".#"),
                Shape("##.",
                      ".##"),
                Shape(".#",
                      "##",
                      "#."),
                Shape("#.",
                      "#.",
                      "##"),
                Shape("###",
                      "#.."),
                Shape(".#",
                      ".#",
                      "##"),
                Shape("###",
                      "..#")
            };
        }
    }
}
