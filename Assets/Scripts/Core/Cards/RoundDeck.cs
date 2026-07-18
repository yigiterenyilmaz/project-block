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

        /// <summary>Buries a card at a random depth in the draw pile instead of discarding it
        /// ("Oryantasyon"). The card can come back up at any time, which is the point.</summary>
        public void InsertRandomIntoDraw(BlockCard card)
        {
            drawPile.Insert(rng.NextInt(0, drawPile.Count + 1), card);
        }

        /// <summary>Swaps the two piles wholesale: what you drew from becomes what you
        /// discard to and vice versa ("Dezenformasyon", "Fraksiyon").</summary>
        public void SwapPiles()
        {
            swapBuffer.Clear();
            swapBuffer.AddRange(drawPile);
            drawPile.Clear();
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            discardPile.AddRange(swapBuffer);
            swapBuffer.Clear();
        }

        /// <summary>Pours both piles together, shuffles, and deals them back out as two
        /// halves ("Dezenformasyon" every turn, "Fraksiyon" on every reshuffle). Counts as a
        /// shuffle, so observers animate it.</summary>
        public void MergeAndSplitHalves()
        {
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            rng.Shuffle(drawPile);
            ShuffleCount++;

            int keep = drawPile.Count / 2;
            while (drawPile.Count > keep)
            {
                int last = drawPile.Count - 1;
                discardPile.Add(drawPile[last]);
                drawPile.RemoveAt(last);
            }
        }

        private readonly List<BlockCard> swapBuffer = new List<BlockCard>();

        /// <summary>"Transfer": the most recently discarded card and the top of the draw
        /// pile change places. Returns false if either pile is empty.</summary>
        public bool SwapDiscardTopWithDrawTop()
        {
            if (drawPile.Count == 0 || discardPile.Count == 0)
            {
                return false;
            }
            int drawTop = drawPile.Count - 1;
            int discardTop = discardPile.Count - 1;
            BlockCard fromDraw = drawPile[drawTop];
            drawPile[drawTop] = discardPile[discardTop];
            discardPile[discardTop] = fromDraw;
            return true;
        }

        /// <summary>"Hızlı çekim şarjörü": empties the draw pile into the discard without
        /// drawing any of it. Pair it with ShuffleDiscardIntoDraw to recycle everything.</summary>
        public void DumpDrawPileIntoDiscard()
        {
            discardPile.AddRange(drawPile);
            drawPile.Clear();
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

        /// <summary>Pulls a specific card out of the draw or discard pile and returns it, or
        /// null if it is in neither (it may be in hand, on the board, or removed from the
        /// round). "Kazı çalışması" uses this to fetch a card back wherever it drifted to
        /// after a reshuffle.</summary>
        public BlockCard TakeCard(int cardId)
        {
            for (int i = 0; i < drawPile.Count; i++)
            {
                if (drawPile[i].Id == cardId)
                {
                    BlockCard card = drawPile[i];
                    drawPile.RemoveAt(i);
                    return card;
                }
            }
            for (int i = 0; i < discardPile.Count; i++)
            {
                if (discardPile[i].Id == cardId)
                {
                    BlockCard card = discardPile[i];
                    discardPile.RemoveAt(i);
                    return card;
                }
            }
            return null;
        }

        /// <summary>Puts a card (already outside all piles) into the removed zone.</summary>
        public void RemoveFromRound(BlockCard card)
        {
            removedFromRound.Add(card);
        }
    }
}
