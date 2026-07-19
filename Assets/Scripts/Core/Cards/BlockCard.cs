// PURPOSE: One card in the player's collection. A card is the deck-side identity of a
// block; the board only stores cubes (with the source card id) once a card is played.
// Elements come from the market ("bloklar markette çeşitli türlerle çıkabilir");
// starting-deck cards are plain. Usually 0-1 elements; "Simya" (future joker) deals 2.
// EXTENSION POINT: attached joker cubes ("Parazit") and upgrade data belong here later.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>A block card owned by the player.</summary>
    public sealed class BlockCard
    {
        private static readonly BlockElement[] NoElements = new BlockElement[0];

        /// <summary>Unique per session; also stamped onto every cube this card places.</summary>
        public int Id { get; }

        public BlockShape Shape { get; }

        private readonly BlockElement[] elements;

        /// <summary>The card's block types; empty for a plain block.</summary>
        public IReadOnlyList<BlockElement> Elements
        {
            get { return elements; }
        }

        /// <summary>True for a player-designed block ("Karakter oluşturma"). Purely an identity
        /// marker so the UI can tag it "custom"; it does not change any rule.</summary>
        public bool IsCustom { get; }

        public BlockCard(int id, BlockShape shape)
            : this(id, shape, null, false)
        {
        }

        public BlockCard(int id, BlockShape shape, IEnumerable<BlockElement> cardElements)
            : this(id, shape, cardElements, false)
        {
        }

        public BlockCard(int id, BlockShape shape, IEnumerable<BlockElement> cardElements,
            bool isCustom)
        {
            Id = id;
            Shape = shape;
            IsCustom = isCustom;
            elements = cardElements == null
                ? NoElements
                : new List<BlockElement>(cardElements).ToArray();
        }

        public bool Has(BlockElement element)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i] == element)
                {
                    return true;
                }
            }
            return false;
        }

        public override string ToString()
        {
            string suffix = elements.Length > 0 ? " " + string.Join("+", elements) : string.Empty;
            return "Card#" + Id + "(" + Shape.Size + " cubes" + suffix + ")";
        }
    }
}
