// PURPOSE: A starting-deck recipe: either a fixed list of shapes or a size plus a
// shape generator. GameConfig picks one. The MARKET draws from a second, separate source
// (MarketShapeGenerator) so a deck's identity is what it starts with, not a cage around
// what it can ever be sold.

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

        /// <summary>Shape source for the deck itself when it is random, and for every card the
        /// run mints outside the market (a joker's void block, a debug bonus card).</summary>
        public IShapeGenerator ShapeGenerator { get; }

        /// <summary>
        /// Shape source for MARKET offers, which is deliberately not the same question as what
        /// the deck is made of. A shop that can only sell you what you already own is a shop
        /// with nothing to offer - the Big Blocks player could never buy a single cube, and the
        /// Small Blocks player never saw anything but the pieces already in their hand.
        ///
        /// Falls back to ShapeGenerator when a deck names no market source of its own, which is
        /// what keeps Chaos honest: a random deck buys from the same randomness it was dealt.
        /// </summary>
        public IShapeGenerator MarketShapeGenerator
        {
            get { return marketShapeGenerator ?? ShapeGenerator; }
        }

        private readonly IShapeGenerator marketShapeGenerator;

        /// <summary>Exact starting-deck composition; null = random deck sampled from
        /// ShapeGenerator. Static decks are identical every run (order still shuffles).</summary>
        public IReadOnlyList<BlockShape> FixedShapes { get; }

        /// <summary>Random deck: Size cards sampled from the generator each run.</summary>
        public DeckDefinition(string name, int size, IShapeGenerator shapeGenerator)
            : this(name, size, shapeGenerator, null)
        {
        }

        /// <summary>Random deck that buys from a different pool than it was dealt from.</summary>
        public DeckDefinition(string name, int size, IShapeGenerator shapeGenerator,
            IShapeGenerator marketGenerator)
        {
            Name = name;
            Size = size;
            ShapeGenerator = shapeGenerator;
            marketShapeGenerator = marketGenerator;
        }

        /// <summary>Static deck: exactly these shapes, every run.</summary>
        public DeckDefinition(string name, IReadOnlyList<BlockShape> fixedShapes,
            IShapeGenerator shapeGenerator)
            : this(name, fixedShapes, shapeGenerator, null)
        {
        }

        /// <summary>Static deck with its own market pool - the curated decks all take this one,
        /// because what they are made of and what their shop stocks are separate decisions.</summary>
        public DeckDefinition(string name, IReadOnlyList<BlockShape> fixedShapes,
            IShapeGenerator shapeGenerator, IShapeGenerator marketGenerator)
        {
            Name = name;
            FixedShapes = fixedShapes;
            Size = fixedShapes.Count;
            ShapeGenerator = shapeGenerator;
            marketShapeGenerator = marketGenerator;
        }
    }
}
