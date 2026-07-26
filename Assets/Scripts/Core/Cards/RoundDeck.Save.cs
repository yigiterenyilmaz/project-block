// PURPOSE: RoundDeck save/load (partial). The piles hold the SAME BlockCard instances as the
// owned deck, so they are written as bare ids and rebuilt from the save's card table - see
// CoreSerializers for why sharing those references matters.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundDeck
    {
        /// <summary>Builds an EMPTY deck for a load to fill. The normal constructor deals and
        /// shuffles, which would both consume rng draws and destroy the saved pile order.</summary>
        internal RoundDeck(IRandomSource rng)
        {
            this.rng = rng;
        }

        internal void Save(SaveWriter w, string key, CardTable cards)
        {
            cards.WriteRefs(w, key + ".draw", drawPile);
            cards.WriteRefs(w, key + ".discard", discardPile);
            cards.WriteRefs(w, key + ".removed", removedFromRound);
            w.Write(key + ".shuffles", ShuffleCount);
        }

        internal void Load(SaveReader r, string key, CardTable cards)
        {
            drawPile.Clear();
            discardPile.Clear();
            removedFromRound.Clear();
            drawPile.AddRange(cards.ReadRefs(r, key + ".draw"));
            discardPile.AddRange(cards.ReadRefs(r, key + ".discard"));
            removedFromRound.AddRange(cards.ReadRefs(r, key + ".removed"));
            ShuffleCount = r.ReadInt(key + ".shuffles");
        }

        /// <summary>Every card the piles are holding, so the save's card table can collect the
        /// round-scoped ones that never joined the owned deck (a "Kara delik" void block).</summary>
        internal IEnumerable<BlockCard> AllCards()
        {
            for (int i = 0; i < drawPile.Count; i++)
            {
                yield return drawPile[i];
            }
            for (int i = 0; i < discardPile.Count; i++)
            {
                yield return discardPile[i];
            }
            for (int i = 0; i < removedFromRound.Count; i++)
            {
                yield return removedFromRound[i];
            }
        }
    }
}
