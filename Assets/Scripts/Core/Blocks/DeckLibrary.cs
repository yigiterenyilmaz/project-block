// PURPOSE: The built-in deck archetypes and shared shape sets. Static catalogue;
// GameConfig.Deck defaults to one of these.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
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
