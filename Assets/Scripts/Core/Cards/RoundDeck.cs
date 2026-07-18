// PURPOSE: The three card piles that exist during a round:
//   - draw pile  ("çekme destesi")  : face-down, drawn from the top (= last list index)
//   - discard    ("ıskarta")        : played / burned cards
//   - removed    ("out of the round"): cards temporarily out of play until round end
//     (overtime clean-sweep removals, expired bonus cards; future: Dolly, trashing).
// The owned deck itself lives in GameSession; RoundDeck only shuffles COPIES of the
// card references into piles, so round-end reset is automatic (new round = new RoundDeck).
// NOTE ON VISIBILITY: the core exposes full pile contents; "face-down / face-up" is a
// UI concern. Future reveal jokers (Insider, Büyüteç) just change what the UI shows.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Draw/discard/removed piles for one round.</summary>
    public sealed class RoundDeck
    {
        private readonly List<BlockCard> drawPile = new List<BlockCard>();
        private readonly List<BlockCard> discardPile = new List<BlockCard>();
        private readonly List<BlockCard> removedFromRound = new List<BlockCard>();
        private readonly IRandomSource rng;

        public RoundDeck(IEnumerable<BlockCard> cards, IRandomSource rng)
        {
            this.rng = rng;
            drawPile.AddRange(cards);
            rng.Shuffle(drawPile);
        }

        public int DrawCount
        {
            get { return drawPile.Count; }
        }

        public int DiscardCount
        {
            get { return discardPile.Count; }
        }

        public int RemovedCount
        {
            get { return removedFromRound.Count; }
        }

        /// <summary>Top of the pile = last element.</summary>
        public IReadOnlyList<BlockCard> DrawPile
        {
            get { return drawPile; }
        }

        /// <summary>Most recently discarded = last element.</summary>
        public IReadOnlyList<BlockCard> DiscardPile
        {
            get { return discardPile; }
        }

        public IReadOnlyList<BlockCard> RemovedFromRound
        {
            get { return removedFromRound; }
        }

        /// <summary>Times the discard has been shuffled into the draw pile this round
        /// (lets observers detect that a reshuffle happened during an action).</summary>
        public int ShuffleCount { get; private set; }

        /// <summary>Takes the top card of the draw pile, or null if it is empty.</summary>
        public BlockCard DrawTop()
        {
            if (drawPile.Count == 0)
            {
                return null;
            }
            BlockCard card = drawPile[drawPile.Count - 1];
            drawPile.RemoveAt(drawPile.Count - 1);
            return card;
        }

        public void Discard(BlockCard card)
        {
            discardPile.Add(card);
        }

        /// <summary>Shuffles the discard pile together with what is left of the draw pile.</summary>
        public void ShuffleDiscardIntoDraw()
        {
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            rng.Shuffle(drawPile);
            ShuffleCount++;
        }

        /// <summary>
        /// Removes up to <paramref name="count"/> random cards from the draw pile until round end
        /// (overtime clean-sweep rule). Returns the removed cards.
        /// </summary>
        public IReadOnlyList<BlockCard> RemoveRandomFromDraw(int count)
        {
            var removed = new List<BlockCard>();
            int toRemove = Math.Min(count, drawPile.Count);
            for (int i = 0; i < toRemove; i++)
            {
                int index = rng.NextInt(0, drawPile.Count);
                removed.Add(drawPile[index]);
                drawPile.RemoveAt(index);
            }
            removedFromRound.AddRange(removed);
            return removed;
        }

        /// <summary>Puts a card (already outside all piles) into the removed zone.</summary>
        public void RemoveFromRound(BlockCard card)
        {
            removedFromRound.Add(card);
        }
    }
}
