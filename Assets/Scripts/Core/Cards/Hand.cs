// PURPOSE: The cards currently held by the player. The hand has NO capacity of its
// own on purpose: the target size lives in RoundRules.HandSize and is read live each
// refill, so future jokers ("Seri tetik", "İmitasyon", "Dezenformasyon") can change
// hand size mid-round by mutating the shared RoundRules instance.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Ordered list of held cards. Mutated only by RoundEngine.</summary>
    public sealed class Hand
    {
        private readonly List<BlockCard> cards = new List<BlockCard>();

        public int Count
        {
            get { return cards.Count; }
        }

        public BlockCard this[int index]
        {
            get { return cards[index]; }
        }

        public IReadOnlyList<BlockCard> Cards
        {
            get { return cards; }
        }

        internal void Add(BlockCard card)
        {
            cards.Add(card);
        }

        /// <summary>Puts a card at a specific slot - used when a card is swapped in place
        /// ("Iade") so the rest of the hand does not shuffle around under the player.</summary>
        internal void Insert(int index, BlockCard card)
        {
            cards.Insert(index, card);
        }

        internal BlockCard RemoveAt(int index)
        {
            BlockCard card = cards[index];
            cards.RemoveAt(index);
            return card;
        }
    }
}
