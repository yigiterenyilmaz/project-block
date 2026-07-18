// PURPOSE: The turn-by-turn state machine of ONE round. This file is the heart of the
// game rules - read it top to bottom to understand the confirmed game design.
//
// TURN RESOLUTION ORDER (keep this order stable; mechanics depend on it):
//   1. place cubes on the board, score the placement
//   2. explode full rows/columns, score them
//   3. clean-sweep check + bonus; if in overtime: reshuffle discard into draw and
//      queue a new advance offer
//   4. played card leaves the hand -> discard (bonus cards: expire + burn top of draw)
//   5. refill the hand (loss rules live here, see RefillHand)
//   6. threshold check: first time the round score reaches the threshold ->
//      reshuffle discard into draw, queue advance offer
//   7. status update: pending advance offer outranks a same-turn loss (the player who
//      just earned an offer may escape by advancing; if they continue, the loss hits)
//   8. otherwise: lose if no held block fits the board
//
// CONTINUING HAS A PRICE (confirmed 2026-07-18): declining an advance offer shuffles
// the whole hand back into the draw pile, removes Rules.CardsRemovedPerContinue
// random cards face-down for the round, and draws a fresh hand - see DecideAdvance.
//
// EXTENSION POINTS:
//  - Powers ("güçler") = new public methods here (they act on the round state).
//  - Jokers = subscribers of TurnResolved/StatusChanged + decorators of IScoreCalculator
//    + mutations of RoundRules. Avoid hardcoding joker logic into this class.
//  - Bonus-hand sources (Klon, Dolly, Olta, Kara delik) call AddBonusCard.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Runs one round. Created fresh by GameSession for every round.</summary>
    public sealed class RoundEngine
    {
        public RoundConfig Config { get; }
        public RoundRules Rules { get; }
        public GameBoard Board { get; }
        public RoundDeck Deck { get; }
        public Hand Hand { get; }

        private readonly List<BonusSlot> bonusHand = new List<BonusSlot>();

        /// <summary>Extra playable cards outside the hand. Empty in the base game.</summary>
        public IReadOnlyList<BonusSlot> BonusHand
        {
            get { return bonusHand; }
        }

        public int TurnNumber { get; private set; }
        public int RoundScore { get; private set; }

        /// <summary>True once RoundScore has reached the threshold; enables overtime rules.</summary>
        public bool ThresholdPassed { get; private set; }

        /// <summary>Clean sweeps ("temizlik") triggered this round. Drives the escalating
        /// UI/sound feedback; future jokers (Batak, Kayıt defteri...) will also read it.</summary>
        public int CleanSweepCount { get; private set; }

        /// <summary>Advance offers declined this round; raises the next continue's price.</summary>
        public int ContinueCount { get; private set; }

        /// <summary>Cards the NEXT continue would remove (the price escalates per continue).</summary>
        public int NextContinueCost
        {
            get { return Rules.CardsRemovedPerContinue + Rules.ContinueCostEscalation * ContinueCount; }
        }

        /// <summary>Draw-pile size right after a continue (hand + discard reshuffled in,
        /// the continue cost removed, a fresh hand drawn). Negative means the continue
        /// would immediately deck-out. The UI shows this on the advance offer.</summary>
        public int PredictDrawCountAfterContinue()
        {
            return Deck.DrawCount + Deck.DiscardCount + Hand.Count
                - NextContinueCost - Rules.HandSize;
        }

        public RoundStatus Status { get; private set; }

        /// <summary>Set when Status is Lost (may be set earlier if an advance offer is
        /// pending and would let the player escape the loss).</summary>
        public LossReason? Loss { get; private set; }

        private readonly IRandomSource rng;
        private readonly IScoreCalculator scorer;

        /// <summary>Accrued value per piggy-bank card currently on the board.</summary>
        private readonly Dictionary<int, int> piggyBanks = new Dictionary<int, int>();

        /// <summary>Fires after every resolved placement. Jokers and UI subscribe here.</summary>
        public event Action<TurnReport> TurnResolved;

        /// <summary>Fires on every Status change. NOTE: not fired for a loss detected during
        /// construction - the creator must check Status right after constructing.</summary>
        public event Action<RoundStatus> StatusChanged;

        public RoundEngine(RoundConfig config, RoundRules rules, IEnumerable<BlockCard> ownedCards,
            IRandomSource rng, IScoreCalculator scorer)
        {
            Config = config;
            Rules = rules;
            this.rng = rng;
            this.scorer = scorer;
            Board = new GameBoard(config.BoardWidth, config.BoardHeight);
            Deck = new RoundDeck(ownedCards, rng);
            Hand = new Hand();
            Status = RoundStatus.InProgress;
            RefillHand();
            if (Loss == null)
            {
                CheckForNoPlayableMove();
            }
            if (Loss != null)
            {
                Status = RoundStatus.Lost; // no event during construction, see StatusChanged docs
            }
        }

        /// <summary>Convenience for UI: legal origins of a shape on the current board.</summary>
        public List<GridPos> GetValidOrigins(BlockShape shape)
        {
            return Board.GetValidOrigins(shape);
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
            if (!Board.CanPlace(card.Shape, origin))
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
            if (!Board.CanPlace(slot.Card.Shape, origin))
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

        private bool AllCellsEmpty(IReadOnlyList<GridPos> positions)
        {
            foreach (GridPos pos in positions)
            {
                if (Board.GetCube(pos).HasValue)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Piggy banks accrue value each turn they survive; destroyed ones pay
        /// out their accumulated value. Returns the total payout this turn.</summary>
        private int ProcessPiggyBanks(TurnReport report)
        {
            int payout = 0;
            var trackedIds = new List<int>(piggyBanks.Keys);
            foreach (int cardId in trackedIds)
            {
                if (Board.HasCubesOf(cardId))
                {
                    piggyBanks[cardId] += scorer.PiggyBankGainPerTurn();
                }
                else
                {
                    payout += piggyBanks[cardId];
                    piggyBanks.Remove(cardId);
                }
            }
            report.PiggyBankPayout = payout;
            return payout;
        }

        private void DiscardHandAndReshuffle()
        {
            while (Hand.Count > 0)
            {
                Deck.Discard(Hand.RemoveAt(Hand.Count - 1));
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

        private TurnReport ResolvePlacement(BlockCard card, GridPos origin, bool fromBonus,
            BonusPlayOutcome bonusOutcome)
        {
            TurnNumber++;
            int shufflesBeforeTurn = Deck.ShuffleCount;
            var report = new TurnReport();
            report.TurnNumber = TurnNumber;
            report.Card = card;
            report.PlayedFromBonusHand = fromBonus;
            report.Origin = origin;

            // 1. place + score
            report.PlacedCells = Board.Place(card, origin);
            if (card.Has(BlockElement.PiggyBank) && !piggyBanks.ContainsKey(card.Id))
            {
                piggyBanks[card.Id] = 0;
            }
            int scoreGained = scorer.ScorePlacement(report.PlacedCells.Count);

            // 2. explode full lines + score (fire chains resolve inside the board)
            LineExplosionResult explosion = Board.ResolveFullLines();
            report.ExplodedRows = explosion.Rows;
            report.ExplodedColumns = explosion.Columns;
            int cubesExploded = explosion.ExplodedCells.Count;

            // DYNAMITE RULE: whole block exploded at once on its own placement turn
            // -> the entire board is cleared (indestructibles excepted).
            if (explosion.LineCount > 0 && card.Has(BlockElement.Dynamite)
                && AllCellsEmpty(report.PlacedCells))
            {
                cubesExploded += Board.DestroyAllDestructible().Count;
                report.DynamiteTriggered = true;
            }
            report.CubesExploded = cubesExploded;
            if (explosion.LineCount > 0)
            {
                scoreGained += scorer.ScoreLineExplosion(explosion.LineCount, cubesExploded);
            }

            // 3. clean sweep. Requires at least one explosion this turn so that future
            // sweep-exempt cubes (obsidian/gold) cannot make an untouched board "sweep"
            // on every placement.
            bool offerAdvance = false;
            if (explosion.LineCount > 0 && Board.IsCleanForSweep())
            {
                report.CleanSweep = true;
                CleanSweepCount++;
                scoreGained += scorer.ScoreCleanSweep();
                if (ThresholdPassed)
                {
                    // Overtime reward: a fresh draw pile and a new chance to leave.
                    Deck.ShuffleDiscardIntoDraw();
                    offerAdvance = true;
                }
            }

            // element upkeep: gold pays while it sits on the board, piggy banks accrue/pay
            int goldCubes = Board.CountCubesOfKind(CubeKind.Gold);
            if (goldCubes > 0)
            {
                report.GoldBonus = scorer.ScoreGoldBonus(goldCubes);
                scoreGained += report.GoldBonus;
            }
            if (piggyBanks.Count > 0)
            {
                scoreGained += ProcessPiggyBanks(report);
            }

            RoundScore += scoreGained;
            report.ScoreGained = scoreGained;
            report.RoundScoreAfter = RoundScore;

            // 4. card disposition
            if (fromBonus)
            {
                if (bonusOutcome == BonusPlayOutcome.ToDiscard)
                {
                    Deck.Discard(card);
                }
                else
                {
                    Deck.RemoveFromRound(card);
                }
                // Burn: the next available card is flipped face-up into the discard.
                // "Next available" follows the normal draw rules (confirmed design):
                // before the threshold an empty draw pile recycles the discard first;
                // in overtime an empty pile on any draw attempt is a loss.
                report.BurnedCard = DrawWithRules();
                if (report.BurnedCard != null)
                {
                    Deck.Discard(report.BurnedCard);
                }
                // Bonus plays do not refill the hand - the hand was not touched.
            }
            else
            {
                Deck.Discard(card);
                RefillHand();
            }

            // 6. threshold check (first pass only)
            if (!ThresholdPassed && RoundScore >= Config.ScoreThreshold)
            {
                ThresholdPassed = true;
                report.ThresholdJustPassed = true;
                Deck.ShuffleDiscardIntoDraw();
                offerAdvance = true;
            }

            // 7./8. status update - see file header for why the offer outranks the loss.
            if (offerAdvance)
            {
                SetStatus(RoundStatus.AwaitingAdvanceDecision);
            }
            else if (Loss != null)
            {
                SetStatus(RoundStatus.Lost);
            }
            else
            {
                CheckForNoPlayableMove();
            }
            report.StatusAfter = Status;
            report.DiscardWasReshuffled = Deck.ShuffleCount != shufflesBeforeTurn;

            if (TurnResolved != null)
            {
                TurnResolved(report);
            }
            return report;
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
                return card;
            }
            if (ThresholdPassed)
            {
                Loss = LossReason.DrawPileEmptyAfterThreshold;
                return null;
            }
            if (Deck.DiscardCount > 0)
            {
                Deck.ShuffleDiscardIntoDraw();
                return Deck.DrawTop();
            }
            return null;
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
                    if (Loss == null)
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
                if (Board.AnyPlacementExists(Hand[i].Shape))
                {
                    return;
                }
            }
            foreach (BonusSlot slot in bonusHand)
            {
                if (Board.AnyPlacementExists(slot.Card.Shape))
                {
                    return;
                }
            }
            Loss = LossReason.NoPlayableMove;
            SetStatus(RoundStatus.Lost);
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
