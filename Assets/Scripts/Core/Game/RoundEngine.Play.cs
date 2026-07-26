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
            // "Antimadde": it is not a block, it is a key. It only goes where every one of its
            // cubes covers a cube of the kind it annihilates - nothing hanging off, nothing over
            // an empty cell, nothing over the wrong kind. Refusing it everywhere else is what
            // makes "cuk oturtmak" the whole puzzle, and it keeps the UI, the origin list and the
            // dead-end check agreeing without a single extra check anywhere.
            if (card.AntimatterOf.HasValue)
            {
                return AntimatterFits(card, origin);
            }
            return Board.CanPlace(EffectiveShape(card), origin, Has(card, BlockElement.Ghost),
                Has(card, BlockElement.Negative));
        }

        /// <summary>True when every cube of an antimatter card would land on a cube of its kind.</summary>
        private bool AntimatterFits(BlockCard card, GridPos origin)
        {
            CubeKind kind = card.AntimatterOf.Value;
            foreach (GridPos offset in EffectiveShape(card).Cells)
            {
                GridPos pos = origin + offset;
                if (!Board.IsInside(pos))
                {
                    return false;
                }
                Cube? occupant = Board.GetCube(pos);
                if (!occupant.HasValue || occupant.Value.Kind != kind)
                {
                    return false;
                }
            }
            return true;
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

        /// <summary>
        /// Grows a card by ONE cube, in a random spot against what it already has, so the block
        /// stays in one piece and simply gets fatter ("Kıtlık" fattening every card that comes back
        /// from the discard). Returns the new shape, or null when it could not grow.
        ///
        /// ROUND-SCOPED BY CONSTRUCTION: it writes to the same per-round override store the fox
        /// reshape uses, so the card in GameSession.OwnedCards is never touched and the growth dies
        /// with this engine. A boss must not leak into the next round, and this one cannot.
        /// </summary>
        internal BlockShape GrowCardShape(BlockCard card, IRandomSource growRng)
        {
            if (card == null || growRng == null)
            {
                return null;
            }
            BlockShape shape = EffectiveShape(card);
            var occupied = new HashSet<GridPos>();
            foreach (GridPos cell in shape.Cells)
            {
                occupied.Add(cell);
            }
            // Every empty cell touching the block, in a stable order so the pick is deterministic.
            var candidates = new List<GridPos>();
            var seen = new HashSet<GridPos>();
            foreach (GridPos cell in shape.Cells)
            {
                TryOfferGrowthCell(new GridPos(cell.X + 1, cell.Y), occupied, seen, candidates);
                TryOfferGrowthCell(new GridPos(cell.X - 1, cell.Y), occupied, seen, candidates);
                TryOfferGrowthCell(new GridPos(cell.X, cell.Y + 1), occupied, seen, candidates);
                TryOfferGrowthCell(new GridPos(cell.X, cell.Y - 1), occupied, seen, candidates);
            }
            if (candidates.Count == 0)
            {
                return null;
            }
            var grown = new List<GridPos>(shape.Cells);
            grown.Add(candidates[growRng.NextInt(0, candidates.Count)]);
            BlockShape next = BlockShape.FromCells(grown);
            foxShapes[card.Id] = next;
            return next;
        }

        private static void TryOfferGrowthCell(GridPos cell, HashSet<GridPos> occupied,
            HashSet<GridPos> seen, List<GridPos> candidates)
        {
            if (!occupied.Contains(cell) && seen.Add(cell))
            {
                candidates.Add(cell);
            }
        }

        /// <summary>The board cells a shape at that origin would occupy, in shape order. Only the
        /// cells that are real play area, so a caller never has to filter. Used for a placement
        /// that is never going to happen: a defective smuggled card falling through the arena.
        /// </summary>
        internal List<GridPos> CellsCovered(BlockShape shape, GridPos origin)
        {
            var covered = new List<GridPos>(shape.Size);
            foreach (GridPos offset in shape.Cells)
            {
                GridPos cell = origin + offset;
                if (Board.IsInside(cell))
                {
                    covered.Add(cell);
                }
            }
            return covered;
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
            if (!ignoreMechanicalRequirement && !Has(card, BlockElement.Mechanical))
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
            if (!Has(card, BlockElement.Fox))
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
            EnsureMirrorReady();
            if (handIndex < 0 || handIndex >= Hand.Count)
            {
                throw new ArgumentOutOfRangeException("handIndex");
            }
            BlockCard card = Hand[handIndex];
            if (IsFrozen(card.Id))
            {
                throw new InvalidOperationException("Card " + card.Id + " is frozen.");
            }
            if (!CanPlaceCard(card, origin))
            {
                throw new InvalidOperationException("Illegal placement of " + card + " at " + origin + ".");
            }
            Hand.RemoveAt(handIndex);
            TurnReport report = ResolvePlacement(card, origin, false,
                BonusPlayOutcome.ExpireFromRound);
            report.HandIndex = handIndex;
            return report;
        }

        /// <summary>
        /// "Öteki dünya": resolves a turn in which the MAIN world sits out - it has nowhere left
        /// to put anything - and only the mirror plays. Refused unless that is genuinely the
        /// case, so it can never be used to skip a turn the main world could have played.
        /// The mirror's half must already be staged.
        /// </summary>
        public TurnReport PlayMirrorOnly()
        {
            EnsurePlacingAllowed();
            if (!HasMirrorWorld || !MirrorHasStagedPlay)
            {
                throw new InvalidOperationException("No staged mirror play to resolve.");
            }
            if (MainWorldHasAnyMove)
            {
                throw new InvalidOperationException(
                    "The main world can still play - it may not sit the turn out.");
            }
            return ResolvePlacement(null, default(GridPos), false,
                BonusPlayOutcome.ExpireFromRound);
        }

        /// <summary>Plays a bonus-hand card. Counts as the turn's placement (confirmed rule).</summary>
        public TurnReport PlayFromBonus(int bonusIndex, GridPos origin)
        {
            EnsurePlacingAllowed();
            EnsureMirrorReady();
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
            // A redraw resolves no turn, so nothing else would apply the erosion its refill may
            // have earned - and the dead-end check below has to see the eroded board.
            ApplyPendingBoardErosion();
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

        /// <summary>"Öteki dünya": a turn is a card in EACH world, so the main world may not
        /// resolve one until the mirror has booked its half - unless the mirror has nowhere to
        /// play at all, in which case it sits the turn out. Enforced here rather than in the UI:
        /// it is a rule of the round, not a courtesy of the screen.</summary>
        private void EnsureMirrorReady()
        {
            if (!MirrorReadyForTurn)
            {
                throw new InvalidOperationException(
                    "The mirror world has not played yet - a turn is a card in each world.");
            }
        }
    }
}
