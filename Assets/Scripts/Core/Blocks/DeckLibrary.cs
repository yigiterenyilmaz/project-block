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

        /// <summary>
        /// What the MARKET may stock, for every curated deck: one of each distinct piece the
        /// three of them are built from, 1 cube up to 4. Deliberately the same shelf whichever
        /// deck you started with - the deck decides what you are DEALT, and the shop is where
        /// you go to be dealt something else. Selling it only what it already owns was what made
        /// the block shelf feel like it had nothing on it.
        ///
        /// Uniform per shape rather than weighted by how often a deck holds it: the shop is a
        /// catalogue, not a second draw pile, and the run's purchase limit (half the starting
        /// deck) is what keeps it from rewriting the archetype.
        /// </summary>
        private static readonly BlockShape[] MarketShapes =
            Distinct(SmallShapes, ClassicShapes, BigShapes);

        private static readonly ShapePoolGenerator MarketPool =
            new ShapePoolGenerator(MarketShapes);

        /// <summary>The middle deck and the default: mostly small and medium pieces with a
        /// handful of tetrominoes on top. Sits between Small Blocks and Big Blocks by design -
        /// it used to BE the big deck in all but name.</summary>
        public static readonly DeckDefinition Classic = new DeckDefinition(
            "Classic", ClassicShapes, new ShapePoolGenerator(ClassicShapes), MarketPool);

        /// <summary>The small, quick deck: 1-3 cube pieces and few of them, so the draw pile
        /// comes round often.</summary>
        public static readonly DeckDefinition SmallBlocks = new DeckDefinition(
            "Small Blocks", SmallShapes, new ShapePoolGenerator(SmallShapes), MarketPool);

        /// <summary>The awkward deck: tetromino-heavy, with only a few small pieces to bail you
        /// out of a corner.</summary>
        public static readonly DeckDefinition BigBlocks = new DeckDefinition(
            "Big Blocks", BigShapes, new ShapePoolGenerator(BigShapes), MarketPool);

        /// <summary>The one intentionally random deck ("kartların tamamıyla rastgele
        /// geldiği"): fresh random polyominoes every run - and it names no market pool, so it
        /// buys from the same randomness it was dealt.</summary>
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

        /// <summary>
        /// CLASSIC (24 cards): the deck the game hands you if you do not choose. Two thirds of
        /// it is 1-3 cubes and the rest is a selection of tetrominoes - one T, one S, one Z, one
        /// L, one J, the bars and a pair of squares - so a hand almost always holds something
        /// that fits a gap.
        ///
        /// Retuned 2026-09-06. It used to be fifteen tetrominoes and every rotation of each,
        /// which read as a deck of awkward pieces rather than a default; that composition is
        /// what Big Blocks is now, and this sits between it and Small Blocks.
        /// </summary>
        private static BlockShape[] BuildClassicShapes()
        {
            var list = new List<BlockShape>();
            Add(list, 2, Shape("#"));
            Add(list, 2, Shape("##"));
            Add(list, 2, Shape("#",
                               "#"));
            Add(list, 2, Shape("###"));
            Add(list, 2, Shape("#",
                               "#",
                               "#"));
            // one of each corner, so no direction is the awkward one
            Add(list, 1, Shape("#.",
                               "##"));
            Add(list, 1, Shape(".#",
                               "##"));
            Add(list, 1, Shape("##",
                               "#."));
            Add(list, 1, Shape("##",
                               ".#"));
            // the four-cube half: one of each familiar piece, two squares
            Add(list, 2, Shape("##",
                               "##"));
            Add(list, 1, Shape("####"));
            Add(list, 1, Shape("#",
                               "#",
                               "#",
                               "#"));
            Add(list, 1, Shape("###",
                               ".#."));
            Add(list, 1, Shape("#.",
                               "##",
                               "#."));
            Add(list, 1, Shape(".##",
                               "##."));
            Add(list, 1, Shape("##.",
                               ".##"));
            Add(list, 1, Shape("###",
                               "#.."));
            Add(list, 1, Shape("###",
                               "..#"));
            return list.ToArray();
        }

        /// <summary>
        /// SMALL BLOCKS (18 cards): nothing over three cubes, and a SHORT deck - shortened from
        /// 26 on 2026-09-06. Its identity is that the draw pile comes round quickly, which a
        /// long deck of tiny pieces was quietly denying it; the erosion clock that punishes a
        /// stalling round bites here first, and that is the trade.
        /// </summary>
        private static BlockShape[] BuildSmallShapes()
        {
            var list = new List<BlockShape>();
            Add(list, 4, Shape("#"));
            Add(list, 3, Shape("##"));
            Add(list, 3, Shape("#",
                               "#"));
            Add(list, 2, Shape("###"));
            Add(list, 2, Shape("#",
                               "#",
                               "#"));
            Add(list, 1, Shape("#.",
                               "##"));
            Add(list, 1, Shape(".#",
                               "##"));
            Add(list, 1, Shape("##",
                               "#."));
            Add(list, 1, Shape("##",
                               ".#"));
            return list.ToArray();
        }

        /// <summary>
        /// BIG BLOCKS (24 cards): what Classic used to be - one of every tetromino in every
        /// orientation, with a few small pieces underneath them. Rebuilt 2026-09-06: the
        /// pentominoes it used to carry (the plus, the U, the 5-bar, the staircases) were the
        /// "huge and weird" end of the game and are gone from every starting deck. Four cubes
        /// is the ceiling now; Chaos is the only place a five-cube piece is still dealt.
        /// </summary>
        private static BlockShape[] BuildBigShapes()
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

        /// <summary>One of each distinct shape across these pools, in first-appearance order.
        /// Order is part of the answer, not decoration: a shop offer is an index into this array
        /// and the run trace has to be reproducible from the seed.</summary>
        private static BlockShape[] Distinct(params BlockShape[][] pools)
        {
            var seen = new HashSet<string>();
            var list = new List<BlockShape>();
            foreach (BlockShape[] pool in pools)
            {
                foreach (BlockShape shape in pool)
                {
                    if (seen.Add(shape.CanonicalKey))
                    {
                        list.Add(shape);
                    }
                }
            }
            return list.ToArray();
        }
    }
}
