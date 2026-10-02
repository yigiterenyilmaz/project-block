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

        /// <summary>A copy of this deck - the same cards in the same piles and order - drawing
        /// its randomness from <paramref name="cloneRng"/>. The duel planner plays moves out on
        /// copies of a round (RoundEngine.CloneForPlanning); the cards themselves are shared,
        /// exactly as the piles share them with the owned deck.</summary>
        internal RoundDeck CloneWith(IRandomSource cloneRng)
        {
            var clone = new RoundDeck(cloneRng);
            clone.drawPile.AddRange(drawPile);
            clone.discardPile.AddRange(discardPile);
            clone.removedFromRound.AddRange(removedFromRound);
            clone.ShuffleCount = ShuffleCount;
            clone.PileRolesAlternate = PileRolesAlternate;
            clone.PilesSwapped = PilesSwapped;
            return clone;
        }

        /// <summary>"Blackjack": empties every pile and lays <paramref name="cards"/> as the draw
        /// pile in the order given (the LAST is the top card) - no shuffle, the duel has already
        /// shuffled them. The shuffle counter and the pile roles are left alone.</summary>
        internal void ResetTo(IEnumerable<BlockCard> cards)
        {
            drawPile.Clear();
            discardPile.Clear();
            removedFromRound.Clear();
            if (cards != null)
            {
                drawPile.AddRange(cards);
            }
        }

        internal void Save(SaveWriter w, string key, CardTable cards)
        {
            cards.WriteRefs(w, key + ".draw", drawPile);
            cards.WriteRefs(w, key + ".discard", discardPile);
            cards.WriteRefs(w, key + ".removed", removedFromRound);
            w.Write(key + ".shuffles", ShuffleCount);
            w.Write(key + ".rolesAlternate", PileRolesAlternate);
            w.Write(key + ".pilesSwapped", PilesSwapped);
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
            PileRolesAlternate = r.ReadBool(key + ".rolesAlternate");
            PilesSwapped = r.ReadBool(key + ".pilesSwapped");
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
