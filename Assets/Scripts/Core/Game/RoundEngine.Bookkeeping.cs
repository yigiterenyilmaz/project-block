// PURPOSE: RoundEngine bookkeeping - the destruction-log diff and turn-start snapshots,
// late score/multiplier routing, the draw-with-rules loss logic, hand refill, and the
// no-playable-move check.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
        /// <summary>Diffs the board against the snapshot and records everything that vanished,
        /// then re-snapshots. Called after every destruction so that jokers see the cube KIND
        /// and SOURCE CARD, both of which the board itself no longer holds.</summary>
        private void LogDestruction()
        {
            if (currentReport == null)
            {
                ResyncSnapshot();
                return;
            }
            List<int> touchedCards = null;
            foreach (KeyValuePair<GridPos, Cube> entry in boardSnapshot)
            {
                if (Board.GetCube(entry.Key).HasValue)
                {
                    continue;
                }
                destroyedThisTurn.Add(new DestroyedCube(entry.Key, entry.Value));
                int cardId = entry.Value.SourceCardId;
                if (touchedCards == null)
                {
                    touchedCards = new List<int>();
                }
                if (!touchedCards.Contains(cardId))
                {
                    touchedCards.Add(cardId);
                }
            }
            if (touchedCards != null)
            {
                foreach (int cardId in touchedCards)
                {
                    if (Board.CountCubesOf(cardId) > 0 || cardsFullyDestroyedThisTurn.Contains(cardId))
                    {
                        continue;
                    }
                    // "In one go" means the block was still whole when this turn began.
                    int atTurnStart;
                    int placed;
                    if (cardCubesAtTurnStart.TryGetValue(cardId, out atTurnStart)
                        && cardPlacedSize.TryGetValue(cardId, out placed)
                        && atTurnStart == placed)
                    {
                        cardsFullyDestroyedThisTurn.Add(cardId);
                    }
                }
            }
            ResyncSnapshot();
        }

        /// <summary>Re-reads the board into the snapshot WITHOUT logging. Used after water
        /// settles, where cubes move rather than die.</summary>
        private void ResyncSnapshot()
        {
            Board.SnapshotInto(boardSnapshot);
        }

        private void CaptureTurnStartCardCounts()
        {
            cardCubesAtTurnStart.Clear();
            foreach (KeyValuePair<GridPos, Cube> entry in boardSnapshot)
            {
                int cardId = entry.Value.SourceCardId;
                int count;
                cardCubesAtTurnStart.TryGetValue(cardId, out count);
                cardCubesAtTurnStart[cardId] = count + 1;
            }
        }

        /// <summary>Routes a joker's flat score into this turn - before finalization it joins
        /// the multiplied pool, after it is added on top. Called via TurnContext.</summary>
        internal void AddLateTurnScore(int amount, string source)
        {
            if (currentReport == null)
            {
                throw new InvalidOperationException("Score can only be granted while a turn is resolving.");
            }
            if (!scoreFinalized)
            {
                breakdown.AddFlat(amount, source);
                return;
            }
            breakdown.AddLateFlat(amount, source);
            // amount is logical; Total scales LateFlat by ScoreScale, so keep RoundScore in step.
            RoundScore += amount * scorer.ScoreScale;
            currentReport.ScoreGained = breakdown.Total;
            currentReport.RoundScoreAfter = RoundScore;
        }

        /// <summary>Called via TurnContext. Multipliers are only meaningful before the score
        /// is finalized, so a late call is a bug in the joker, not a silent no-op.</summary>
        internal void AddTurnMultiplier(double factor, string source)
        {
            if (currentReport == null || scoreFinalized)
            {
                throw new InvalidOperationException(
                    "Multipliers can only be added from ModifyScore, before the turn score is finalized.");
            }
            breakdown.AddMultiplier(factor, source);
        }

        /// <summary>
        /// The ONE way a card ever leaves the draw pile (hand refills AND bonus burns).
        /// Deck-based loss rules live here:
        ///  - overtime (threshold passed): an empty draw pile on any draw attempt is a
        ///    loss; the discard is NOT recycled anymore (only clean sweeps recycle it).
        ///  - before the threshold: an empty draw pile recycles the discard first;
        ///    null is returned only when no card exists in either pile.
        /// </summary>
        private BlockCard DrawWithRules()
        {
            BlockCard card = Deck.DrawTop();
            if (card != null)
            {
                NoteCardDrawn();
                return card;
            }
            if (currentReport != null)
            {
                currentReport.DrawPileEmptiedThisTurn = true;
            }
            if (ThresholdPassed)
            {
                Loss = LossReason.DrawPileEmptyAfterThreshold;
                return null;
            }
            if (Rules.DrawOnlyAvailableNoReshuffle)
            {
                return null; // "İmitasyon": never auto-recycles the discard on a draw
            }
            if (Deck.DiscardCount > 0)
            {
                Deck.ShuffleDiscardIntoDraw();
                BlockCard recycled = Deck.DrawTop();
                if (recycled != null)
                {
                    NoteCardDrawn();
                }
                return recycled;
            }
            return null;
        }

        /// <summary>"Büyüteç": the reveal is a CONSUMABLE peek at the top of the draw pile, so
        /// each card actually drawn uncovers one fewer (2 -> 1 -> 0). No-op when nothing is
        /// revealed, so the base game (RevealedDrawCount always 0) is left byte-identical.</summary>
        private void NoteCardDrawn()
        {
            if (Rules.RevealedDrawCount > 0)
            {
                Rules.RevealedDrawCount--;
            }
        }

        /// <summary>
        /// Draws until the hand reaches Rules.HandSize. Confirmed rule: if the hand cannot
        /// be refilled to full size (no card left in either pile), that is a loss - even
        /// if some cards remain in hand.
        /// </summary>
        private void RefillHand()
        {
            while (Hand.Count < Rules.HandSize)
            {
                BlockCard card = DrawWithRules();
                if (card == null)
                {
                    // "İmitasyon": a hand that cannot be topped up is fine, not a loss - it
                    // just holds fewer cards until the next draw source appears.
                    if (Loss == null && !Rules.DrawOnlyAvailableNoReshuffle)
                    {
                        Loss = LossReason.HandCannotBeRefilled;
                    }
                    return;
                }
                Hand.Add(card);
            }
        }

        /// <summary>Base lose condition: no held block (hand or bonus) fits the board.</summary>
        private void CheckForNoPlayableMove()
        {
            for (int i = 0; i < Hand.Count; i++)
            {
                if (CanPlayCardAnywhere(Hand[i]))
                {
                    return;
                }
            }
            foreach (BonusSlot slot in bonusHand)
            {
                if (CanPlayCardAnywhere(slot.Card))
                {
                    return;
                }
            }
            // DEAD END. Before the round ends, effects that can open a gap get their turn:
            // first jokers, automatically ("Deprem"); then, if the player holds a rescue
            // power ("Kentsel Dönüşüm"), the round PAUSES in AwaitingRescue so they can use
            // it. Loss is set either way, so declining the offer simply confirms it.
            Loss = LossReason.NoPlayableMove;

            if (session != null && hooks.TryRescueFromDeadEnd(new RoundContext(session, rng, this)))
            {
                Loss = null;
                CheckForNoPlayableMove(); // the rescue may not have been enough
                return;
            }

            if (session != null && session.Powers.HasUsableDeadEndRescue())
            {
                SetStatus(RoundStatus.AwaitingRescue);
                return;
            }

            SetStatus(RoundStatus.Lost);
        }

        /// <summary>Runs the dead-end check from outside a placement. Tests and the UI use it;
        /// the engine itself reaches CheckForNoPlayableMove through the normal turn flow.</summary>
        internal void DebugCheckForDeadEnd()
        {
            CheckForNoPlayableMove();
        }

        /// <summary>Test/UI entry point for declining the rescue offer.</summary>
        internal void DebugDeclineRescue()
        {
            DeclineRescue();
        }

        /// <summary>Declines the rescue offer and takes the loss (the UI's "give up" path).</summary>
        internal void DeclineRescue()
        {
            if (Status != RoundStatus.AwaitingRescue)
            {
                return;
            }
            SetStatus(RoundStatus.Lost);
        }

        /// <summary>A rescue power opened a gap: clear the pending loss and re-check. If the
        /// board still has no room the normal dead-end path runs again, which may offer
        /// another rescue or finally end the round.</summary>
        internal void ResumeAfterRescue()
        {
            if (Status != RoundStatus.AwaitingRescue)
            {
                return;
            }
            Loss = null;
            SetStatus(RoundStatus.InProgress);
            CheckForNoPlayableMove();
        }

        /// <summary>No-move check that accounts for the card's options: mechanical
        /// blocks may rotate into a fit, fox blocks may reshape into any deck shape.</summary>
        private bool CanPlayCardAnywhere(BlockCard card)
        {
            bool ghost = card.Has(BlockElement.Ghost);
            BlockShape shape = EffectiveShape(card);
            if (Board.AnyPlacementExists(shape, ghost))
            {
                return true;
            }
            if (card.Has(BlockElement.Mechanical))
            {
                BlockShape rotated = shape;
                for (int i = 0; i < 3; i++)
                {
                    rotated = rotated.RotatedClockwise();
                    if (Board.AnyPlacementExists(rotated, ghost))
                    {
                        return true;
                    }
                }
            }
            if (card.Has(BlockElement.Fox))
            {
                foreach (BlockShape deckShape in AllRoundShapes())
                {
                    if (Board.AnyPlacementExists(deckShape, ghost))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private IEnumerable<BlockShape> AllRoundShapes()
        {
            for (int i = 0; i < Hand.Count; i++)
            {
                yield return Hand[i].Shape;
            }
            foreach (BlockCard card in Deck.DrawPile)
            {
                yield return card.Shape;
            }
            foreach (BlockCard card in Deck.DiscardPile)
            {
                yield return card.Shape;
            }
            foreach (BlockCard card in Deck.RemovedFromRound)
            {
                yield return card.Shape;
            }
        }

        private void SetStatus(RoundStatus newStatus)
        {
            if (Status == newStatus)
            {
                return;
            }
            Status = newStatus;
            if (StatusChanged != null)
            {
                StatusChanged(newStatus);
            }
        }
    }
}
