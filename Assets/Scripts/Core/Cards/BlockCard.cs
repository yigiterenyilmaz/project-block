// PURPOSE: One card in the player's collection. A card is the deck-side identity of a
// block; the board only stores cubes (with the source card id) once a card is played.
// Elements come from the market ("bloklar markette çeşitli türlerle çıkabilir");
// starting-deck cards are plain. Usually 0-1 elements; "Simya" deals 2, and a weld can join two.
//
// A CARD WITH TWO ELEMENTS IS ONE OF THEM AT A TIME, AND THE PLAYER PICKS WHICH (designer's call,
// 2026-09-16). It used to be both and neither: the cube took whichever element came first in a
// fixed priority (fire beat water, water beat gold...) while dynamite, ghost and mechanical, which
// are asked of the card rather than the cube, all applied on top - so a FIRE+WATER card was simply
// fire with a lie on its label, and a WATER+DYNAMITE card looked like water and blew up. Now such a
// card is ALCHEMICAL: Elements still lists everything it carries (pricing, the save, a copy), but
// Has() answers only for the ACTIVE choice, and every rule in the game asks Has() - placement, the
// cube kind, fire chains, dynamite, ghost, mechanical, fox, "Midas" in the hand. "Hedefli" is a mark
// on one cube rather than what the block is made of, so it is never one of the choices and always
// applies.
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

        /// <summary>
        /// True for a card that was TAKEN OFF THE MARKET SHELF (bought, or smuggled - the goods
        /// changed hands either way), false for every card that was already yours: the starting
        /// deck, a designed block, anything a joker or a power handed you.
        ///
        /// It is what the market's purchase limit counts (GameSession.PurchasedCardCount), which
        /// is why it is a property of the CARD and not a running total on the session: selling a
        /// starting-deck card must not buy you another slot, and the only way to keep those two
        /// apart is to know which cards came from the shelf. The sell screen shows it too.
        /// </summary>
        public bool IsPurchased { get; internal set; }

        /// <summary>True for goods that came off the back of a lorry ("Kaçakçı"), sound or not.
        /// Purely an identity marker so the UI can tag it; it changes no rule by itself.</summary>
        public bool IsSmuggled { get; internal set; }

        /// <summary>
        /// True for a DEFECTIVE smuggled card: an ordinary-looking block that will not stay on the
        /// board. You place it legally, and it falls straight through the arena and out of the
        /// frame - so nothing lands, nothing scores and nothing explodes, and the turn is gone.
        ///
        /// It is read in ONE place, at the top of the turn resolver (and its mirror twin), which
        /// simply never lets the placement happen. The card is discarded normally, so unlike a
        /// shape that cannot be placed it never jams a hand slot: it costs you the turn you wasted
        /// on it, every time it comes round again.
        /// </summary>
        public bool FallsThrough { get; internal set; }

        /// <summary>
        /// The cube kind this card is the ANTIMATTER of ("Antimadde"), or null for every ordinary
        /// card. Such a card places nothing: it may only be dropped where EVERY one of its cubes
        /// lands on a cube of that kind, and doing so annihilates every cube of that kind on the
        /// board.
        ///
        /// Read in two places, both central: RoundEngine.CanPlaceCard refuses it anywhere else, and
        /// the turn resolver does the annihilating instead of a placement. The joker that made it
        /// pays for it and lets it rot; the rule itself is the engine's.
        /// </summary>
        public CubeKind? AntimatterOf { get; internal set; }

        /// <summary>
        /// Which cube of a "Hedefli" block is its TARGET - an index into the cell list of the
        /// shape being placed - or -1 on every ordinary card. Rolled once when the card is minted
        /// and never re-rolled, so a targeted block always shows the player the same cube.
        ///
        /// Deliberately an index into the EFFECTIVE shape rather than a fixed offset: a block that
        /// rotates (mechanical, or anything at all in retro mode) or is reshaped (fox, "Kıtlık")
        /// re-sorts its cells, and an index cannot fall out of the list the way a remembered
        /// coordinate can fall off the block. The card preview reads the same index off the same
        /// shape, so what the player sees marked is always what lands marked.
        /// </summary>
        public int TargetCellIndex { get; internal set; } = -1;

        /// <summary>The target's index within <paramref name="shape"/>, or -1 when this card has
        /// no target. Clamped into range, so a reshape can never leave it pointing at nothing.</summary>
        public int TargetIndexIn(BlockShape shape)
        {
            if (TargetCellIndex < 0 || shape == null || shape.Cells.Count == 0)
            {
                return -1;
            }
            return TargetCellIndex < shape.Cells.Count ? TargetCellIndex : shape.Cells.Count - 1;
        }

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

        /// <summary>What an alchemical card may be: its elements, less "Hedefli" (a mark, not a
        /// material). Empty when the card is not alchemical.</summary>
        public IReadOnlyList<BlockElement> ElementChoices
        {
            get
            {
                var choices = new List<BlockElement>();
                if (cellElements == null)
                {
                    for (int i = 0; i < elements.Length; i++)
                    {
                        if (elements[i] != BlockElement.Targeted)
                        {
                            choices.Add(elements[i]);
                        }
                    }
                }
                return choices.Count >= 2 ? choices : (IReadOnlyList<BlockElement>)NoElements;
            }
        }

        /// <summary>True when the card carries two or more elements and behaves as ONE of them -
        /// the player's choice. Per-cube designed blocks are never alchemical: each cube already
        /// is exactly one thing.</summary>
        public bool IsAlchemical
        {
            get { return ElementChoices.Count >= 2; }
        }

        /// <summary>Index into ElementChoices of the element the card is being. 0 - the card's
        /// original element, the one Simya added the second to - until the player picks.</summary>
        public int ActiveChoice { get; internal set; }

        /// <summary>The element an alchemical card is being right now; null on any other card.
        /// </summary>
        public BlockElement? ActiveElement
        {
            get
            {
                IReadOnlyList<BlockElement> choices = ElementChoices;
                if (choices.Count < 2)
                {
                    return null;
                }
                return choices[ActiveChoice >= 0 && ActiveChoice < choices.Count ? ActiveChoice : 0];
            }
        }

        public bool Has(BlockElement element)
        {
            BlockElement? active = ActiveElement;
            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i] == element)
                {
                    // An alchemical card is only the element it is being.
                    return !active.HasValue || element == BlockElement.Targeted || element == active.Value;
                }
            }
            return false;
        }

        /// <summary>Makes this card the element <paramref name="element"/>. False when the card is
        /// not alchemical or cannot be that element.</summary>
        internal bool Choose(BlockElement element)
        {
            IReadOnlyList<BlockElement> choices = ElementChoices;
            for (int i = 0; i < choices.Count; i++)
            {
                if (choices[i] == element)
                {
                    ActiveChoice = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>A copy, a cut piece or anything else minted from <paramref name="source"/>
        /// keeps being what the source was being - a player who made a water block of a
        /// fire+water card does not get fire back by duplicating it.</summary>
        internal void KeepChoiceOf(BlockCard source)
        {
            BlockElement? active = source != null ? source.ActiveElement : null;
            if (active.HasValue)
            {
                Choose(active.Value);
            }
        }

        public override string ToString()
        {
            string suffix = elements.Length > 0 ? " " + string.Join("+", elements) : string.Empty;
            return "Card#" + Id + "(" + Shape.Size + " cubes" + suffix + ")";
        }
    }
}
