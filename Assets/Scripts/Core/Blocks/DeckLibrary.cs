// PURPOSE: Deck archetypes - the design plan's "different deck types". A deck is a
// named recipe: either a FIXED list of shapes (static decks - the same composition
// every run, confirmed 2026-07-18) or a generator-sampled random deck (Chaos).
// The ShapeGenerator is always present: market offers draw shapes from it.
// EXTENSION POINT: unlockable decks are new DeckDefinition entries in DeckLibrary.

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

        /// <summary>Shape source for market offers (and for the deck itself when random).</summary>
        public IShapeGenerator ShapeGenerator { get; }

        /// <summary>Exact starting-deck composition; null = random deck sampled from
        /// ShapeGenerator. Static decks are identical every run (order still shuffles).</summary>
        public IReadOnlyList<BlockShape> FixedShapes { get; }

        /// <summary>Random deck: Size cards sampled from the generator each run.</summary>
        public DeckDefinition(string name, int size, IShapeGenerator shapeGenerator)
        {
            Name = name;
            Size = size;
            ShapeGenerator = shapeGenerator;
        }

        /// <summary>Static deck: exactly these shapes, every run.</summary>
        public DeckDefinition(string name, IReadOnlyList<BlockShape> fixedShapes,
            IShapeGenerator marketShapeGenerator)
        {
            Name = name;
            FixedShapes = fixedShapes;
            Size = fixedShapes.Count;
            ShapeGenerator = marketShapeGenerator;
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
        private static readonly BlockShape[] ClassicShapes = BuildClassicShapes();
        private static readonly BlockShape[] SmallShapes = BuildSmallShapes();
        private static readonly BlockShape[] BigShapes = BuildBigShapes();

        /// <summary>Curated set of familiar pieces - one of each, the default deck.</summary>
        public static readonly DeckDefinition Classic = new DeckDefinition(
            "Classic", ClassicShapes, new ShapePoolGenerator(ClassicShapes));

        /// <summary>Static deck of 1-3 cube pieces.</summary>
        public static readonly DeckDefinition SmallBlocks = new DeckDefinition(
            "Small Blocks", SmallShapes, new ShapePoolGenerator(SmallShapes));

        /// <summary>Static deck of 4-5 cube pieces (tetrominoes + pentominoes).</summary>
        public static readonly DeckDefinition BigBlocks = new DeckDefinition(
            "Big Blocks", BigShapes, new ShapePoolGenerator(BigShapes));

        /// <summary>The one intentionally random deck ("kartların tamamıyla rastgele
        /// geldiği"): fresh random polyominoes every run.</summary>
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

        private static void Add(List<BlockShape> list, int copies, BlockShape shape)
        {
            for (int i = 0; i < copies; i++)
            {
                list.Add(shape);
            }
        }

        private static BlockShape[] BuildClassicShapes()
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

        private static BlockShape[] BuildSmallShapes()
        {
            var list = new List<BlockShape>();
            Add(list, 4, Shape("#"));
            Add(list, 4, Shape("##"));
            Add(list, 4, Shape("#",
                               "#"));
            Add(list, 3, Shape("###"));
            Add(list, 3, Shape("#",
                               "#",
                               "#"));
            Add(list, 2, Shape("#.",
                               "##"));
            Add(list, 2, Shape(".#",
                               "##"));
            Add(list, 2, Shape("##",
                               "#."));
            Add(list, 2, Shape("##",
                               ".#"));
            return list.ToArray();
        }

        private static BlockShape[] BuildBigShapes()
        {
            return new[]
            {
                Shape("##",
                      "##"),
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
                Shape(".##",
                      "##."),
                Shape("##.",
                      ".##"),
                Shape("#.",
                      "#.",
                      "##"),
                Shape(".#",
                      ".#",
                      "##"),
                Shape("#####"),
                Shape("#",
                      "#",
                      "#",
                      "#",
                      "#"),
                Shape(".#.",
                      "###",
                      ".#."),
                Shape("#.#",
                      "###"),
                Shape("###",
                      ".#.",
                      ".#."),
                Shape("#.",
                      "#.",
                      "#.",
                      "##"),
                Shape(".#",
                      "##",
                      ".#",
                      ".#"),
                Shape("##",
                      "##",
                      "#."),
                Shape("#..",
                      "##.",
                      ".##"),
                Shape("##.",
                      ".#.",
                      ".##"),
                Shape(".##",
                      "##.",
                      ".#."),
                Shape("#..",
                      "#..",
                      "###")
            };
        }
    }
}
