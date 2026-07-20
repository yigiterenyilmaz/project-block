// PURPOSE: A starting-deck recipe: either a fixed list of shapes or a size plus a
// shape generator. GameConfig picks one; the market also draws from its shape source.

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
}
