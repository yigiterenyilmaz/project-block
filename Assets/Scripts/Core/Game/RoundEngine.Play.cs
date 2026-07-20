// PURPOSE: RoundEngine player-facing play API - shape/rotation/fox queries, playing a
// hand or bonus card, the advance decision, hand redraw/replace, and the hand
// discard/refill primitives jokers reuse.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
        /// <summary>Convenience for UI: legal origins of a shape on the current board.</summary>
        public List<GridPos> GetValidOrigins(BlockShape shape)
        {
            return Board.GetValidOrigins(shape);
        }

        /// <summary>The one placement-legality check for a specific card (ghost blocks
        /// may hang off the board). UI and play methods both use this.</summary>
        public bool CanPlaceCard(BlockCard card, GridPos origin)
        {
            return Board.CanPlace(EffectiveShape(card), origin, card.Has(BlockElement.Ghost));
        }

        /// <summary>The shape this card currently places: fox reshapes replace the base
        /// shape, mechanical rotations spin it. Plain cards return their own shape.</summary>
        public BlockShape EffectiveShape(BlockCard card)
        {
            BlockShape shape;
            if (!foxShapes.TryGetValue(card.Id, out shape))
            {
                shape = card.Shape;
            }
            int steps;
            if (rotations.TryGetValue(card.Id, out steps))
            {
                for (int i = 0; i < steps; i++)
                {
                    shape = shape.RotatedClockwise();
                }
            }
            return shape;
        }

        /// <summary>MECHANICAL RULE: rotates the block 90° clockwise (right-click in the
        /// UI). Only mechanical blocks rotate; the orientation persists for the round.</summary>
        public void RotateCard(int handIndex)
        {
            RotateCard(handIndex, false);
        }

        /// <summary>Rotation with an override for the "Cımbız" power, which grants a single
        /// turn of the mechanical block's ability to any held card.</summary>
        public void RotateCard(int handIndex, bool ignoreMechanicalRequirement)
        {
            EnsurePlacingAllowed();
            if (handIndex < 0 || handIndex >= Hand.Count)
            {
                throw new ArgumentOutOfRangeException("handIndex");
            }
            BlockCard card = Hand[handIndex];
            if (!ignoreMechanicalRequirement && !card.Has(BlockElement.Mechanical))
            {
                throw new InvalidOperationException("Only mechanical blocks can rotate.");
            }
            int steps;
            rotations.TryGetValue(card.Id, out steps);
            rotations[card.Id] = (steps + 1) % 4;
        }

        /// <summary>FOX RULE (confirmed): a fox block can take any shape that exists in
        /// the current deck. The UI offers only deck shapes; this trusts its caller.</summary>
        public void SetFoxShape(int handIndex, BlockShape shape)
        {
            EnsurePlacingAllowed();
            if (handIndex < 0 || handIndex >= Hand.Count)
            {
                throw new ArgumentOutOfRangeException("handIndex");
            }
            BlockCard card = Hand[handIndex];
            if (!card.Has(BlockElement.Fox))
            {
                throw new InvalidOperationException("Only fox blocks can be reshaped.");
            }
            foxShapes[card.Id] = shape;
        }

        /// <summary>Plays a hand card onto the board. Validate with Board.CanPlace first;
        /// an illegal call throws.</summary>
        public TurnReport PlayFromHand(int handIndex, GridPos origin)
        {
            EnsurePlacingAllowed();
            if (handIndex < 0 || handIndex >= Hand.Count)
            {
                throw new ArgumentOutOfRangeException("handIndex");
            }
            BlockCard card = Hand[handIndex];
            if (!CanPlaceCard(card, origin))
            {
                throw new InvalidOperationException("Illegal placement of " + card + " at " + origin + ".");
            }
            Hand.RemoveAt(handIndex);
            return ResolvePlacement(card, origin, false, BonusPlayOutcome.ExpireFromRound);
        }

        /// <summary>Plays a bonus-hand card. Counts as the turn's placement (confirmed rule).</summary>
        public TurnReport PlayFromBonus(int bonusIndex, GridPos origin)
        {
            EnsurePlacingAllowed();
            if (bonusIndex < 0 || bonusIndex >= bonusHand.Count)
            {
                throw new ArgumentOutOfRangeException("bonusIndex");
            }
            BonusSlot slot = bonusHand[bonusIndex];
            if (!CanPlaceCard(slot.Card, origin))
            {
                throw new InvalidOperationException("Illegal placement of " + slot.Card + " at " + origin + ".");
            }
            bonusHand.RemoveAt(bonusIndex);
            return ResolvePlacement(slot.Card, origin, true, slot.OutcomeOnPlay);
        }

        /// <summary>Resolves the pending advance offer. Advancing ends the round (-> market);
        /// continuing resumes play under overtime rules AND has a price: the hand is
        /// reshuffled into the draw pile, random cards leave the round face-down, and a
        /// fresh hand is drawn (see the file header).</summary>
        public void DecideAdvance(bool advanceToNextRound)
        {
            if (Status != RoundStatus.AwaitingAdvanceDecision)
            {
                throw new InvalidOperationException("No advance decision is pending.");
            }
            if (advanceToNextRound)
            {
                SetStatus(RoundStatus.Advanced);
                return;
            }
            if (Loss != null)
            {
                // The offer shielded a same-turn loss; continuing means accepting it.
                SetStatus(RoundStatus.Lost);
                return;
            }
            SetStatus(RoundStatus.InProgress);
            int continueCost = NextContinueCost; // escalates with every continue
            DiscardHandAndReshuffle();
            Deck.RemoveRandomFromDraw(continueCost);
            ContinueCount++;
            RefillHand();
            // Continuing an overtime restarts the board-rewind history too, so "Kum saati"
            // cannot reach back past the continue.
            boardHistory.Clear();
            if (Loss != null)
            {
                SetStatus(RoundStatus.Lost);
                return;
            }
            CheckForNoPlayableMove();
        }

        /// <summary>Adds a card to the bonus hand. Base game: unused. Future powers
        /// (Klon, Dolly, Olta, Kara delik...) and tests are the callers.</summary>
        public void AddBonusCard(BlockCard card, BonusPlayOutcome outcomeOnPlay)
        {
            bonusHand.Add(new BonusSlot(card, outcomeOnPlay));
        }

        /// <summary>
        /// Discards the entire hand, shuffles the discard pile into the draw pile, and
        /// draws a fresh hand. Does NOT consume a turn. Currently bound to a debug key in
        /// the UI; this is also the primitive the future "Renovasyon" joker will use.
        /// </summary>
        public void RedrawHand()
        {
            EnsurePlacingAllowed();
            DiscardHandAndReshuffle();
            RefillHand();
            if (Loss != null)
            {
                SetStatus(RoundStatus.Lost);
                return;
            }
            CheckForNoPlayableMove();
        }

        /// <summary>
        /// Swaps ONE card out of the hand for the top of the draw pile ("Iade" joker).
        /// The replacement lands in the same slot so the hand does not reorder. Does NOT
        /// consume a turn and does NOT reshuffle - it follows the normal draw rules, so in
        /// overtime an empty draw pile is a loss exactly like any other draw.
        /// </summary>
        public BlockCard ReplaceHandCard(int handIndex)
        {
            EnsurePlacingAllowed();
            if (handIndex < 0 || handIndex >= Hand.Count)
            {
                throw new ArgumentOutOfRangeException("handIndex");
            }
            BlockCard returned = Hand.RemoveAt(handIndex);
            DisposeCard(returned);
            BlockCard drawn = DrawWithRules();
            if (drawn == null)
            {
                if (Loss == null)
                {
                    Loss = LossReason.HandCannotBeRefilled;
                }
                SetStatus(RoundStatus.Lost);
                return null;
            }
            Hand.Insert(handIndex, drawn);
            CheckForNoPlayableMove();
            return drawn;
        }

        /// <summary>Discards the whole hand and draws a full new one WITHOUT recycling the
        /// discard. "Seri tetik" will use this at end of turn. Consumes no turn.</summary>
        internal void CycleHandWithoutReshuffle()
        {
            DiscardWholeHand();
            RefillHandToSize();
        }

        /// <summary>Sends the whole hand to the pile without drawing anything back.
        /// "İmitasyon" needs the two halves apart, because dumping the hand is what decides
        /// how big the next hand is allowed to be.</summary>
        internal void DiscardWholeHand()
        {
            while (Hand.Count > 0)
            {
                DisposeCard(Hand.RemoveAt(Hand.Count - 1));
            }
        }

        /// <summary>Draws up to the CURRENT Rules.HandSize, applying the normal loss rules.</summary>
        internal void RefillHandToSize()
        {
            RefillHand();
        }

        private void DiscardHandAndReshuffle()
        {
            while (Hand.Count > 0)
            {
                DisposeCard(Hand.RemoveAt(Hand.Count - 1));
            }
            Deck.ShuffleDiscardIntoDraw();
        }

        private void EnsurePlacingAllowed()
        {
            if (Status != RoundStatus.InProgress)
            {
                throw new InvalidOperationException("Cannot place a block while round status is " + Status + ".");
            }
        }
    }
}
