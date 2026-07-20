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

        /// <summary>Per-cube elements for a player-designed block, aligned index-for-index to
        /// Shape.Cells (a null entry is a plain cube). null when the card is NOT per-cube - then
        /// the whole block shares one element set (Elements), the normal market/deck case.</summary>
        private readonly BlockElement?[] cellElements;

        /// <summary>The card's block types; empty for a plain block. For a per-cube designed
        /// block this is the DISTINCT set of its cube elements, so pricing / Has() / the card
        /// badge keep working; the actual per-cube layout lives in CellElement.</summary>
        public IReadOnlyList<BlockElement> Elements
        {
            get { return elements; }
        }

        /// <summary>True when each cube may carry its own element (a "Karakter oluşturma" design).
        /// Placement then stamps each cube from CellElement instead of one card-wide kind.</summary>
        public bool HasPerCubeElements
        {
            get { return cellElements != null; }
        }

        /// <summary>The element of the cube at Shape.Cells[cellIndex], or null for a plain cube
        /// (also null when the card is not per-cube). Placement and the UI read this to give each
        /// cube its own kind/colour.</summary>
        public BlockElement? CellElement(int cellIndex)
        {
            if (cellElements == null || cellIndex < 0 || cellIndex >= cellElements.Length)
            {
                return null;
            }
            return cellElements[cellIndex];
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
            cellElements = null;
        }

        private BlockCard(int id, BlockShape shape, BlockElement[] distinctElements,
            BlockElement?[] perCube, bool isCustom)
        {
            Id = id;
            Shape = shape;
            IsCustom = isCustom;
            elements = distinctElements;
            cellElements = perCube;
        }

        /// <summary>Builds a per-cube designed block ("Karakter oluşturma"): perCubeElements is
        /// aligned to shape.Cells (index i is cube i; a null entry is a plain cube). The block-wide
        /// Elements list is set to the DISTINCT non-null elements so pricing / Has() / the "custom"
        /// badge keep working, while each cube keeps its own element for placement and the UI.</summary>
        public static BlockCard Designed(int id, BlockShape shape,
            IReadOnlyList<BlockElement?> perCubeElements)
        {
            var perCube = new BlockElement?[shape.Cells.Count];
            var distinct = new List<BlockElement>();
            for (int i = 0; i < perCube.Length; i++)
            {
                BlockElement? e = perCubeElements != null && i < perCubeElements.Count
                    ? perCubeElements[i]
                    : null;
                perCube[i] = e;
                if (e.HasValue && !distinct.Contains(e.Value))
                {
                    distinct.Add(e.Value);
                }
            }
            return new BlockCard(id, shape,
                distinct.Count == 0 ? NoElements : distinct.ToArray(), perCube, true);
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
