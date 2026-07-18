// PURPOSE: The turn-by-turn state machine of ONE round. This file is the heart of the
// game rules - read it top to bottom to understand the confirmed game design.
//
// TURN RESOLUTION ORDER (keep this order stable; mechanics depend on it):
//   1. place cubes on the board, score the placement
//   2. explode full rows/columns, score them   -> hook: AfterLineExplosion
//   3. clean-sweep check + bonus               -> hook: AfterCleanSweep
//      if in overtime: reshuffle discard into draw, remove N random cards from the draw
//      pile for the round, queue advance offer
//   4. finalize the score                      -> hook: ModifyScore (jokers add here)
//      the turn's points are banked into RoundScore exactly once, floored once
//   5. played card leaves the hand -> discard (bonus cards: expire + burn top of draw)
//   6. refill the hand (loss rules live here, see RefillHand)
//   7. end-of-turn effects                     -> hook: AfterTurnScored
//      score granted here still counts toward the threshold (checked next)
//   8. threshold check: first time the round score reaches the threshold ->
//      reshuffle discard into draw, queue advance offer
//   9. status update: pending advance offer outranks a same-turn loss (the player who
//      just earned an offer may escape by advancing; if they continue, the loss hits)
//  10. otherwise: lose if no held block fits the board
//
// CLEAN SWEEP IS ONE CENTRAL EVENT (confirmed rule). TryResolveCleanSweep is the only
// place it can fire, at most once per turn, and it requires the board to have gone from
// "not clean" to "clean" through an actual destruction this turn. That guard matters for
// future sweep-exempt cubes (ice/obsidian/gold): without it, once only exempt cubes are
// left, EVERY later explosion would re-detect a sweep on an already-clean board and farm
// the overtime reward. Joker-triggered sweeps must call TryResolveCleanSweep too, never
// re-implement the check.
//
// EXTENSION POINTS:
//  - Powers ("gucler") = new public methods here (they act on the round state).
//  - Jokers = ITurnHooks (see JokerInventory) + mutations of RoundRules. Never hardcode
//    joker logic into this class.
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

        /// <summary>Clean sweeps ("temizlik") resolved so far this round.</summary>
        public int CleanSweepCount { get; private set; }

        public RoundStatus Status { get; private set; }

        /// <summary>Set when Status is Lost (may be set earlier if an advance offer is
        /// pending and would let the player escape the loss).</summary>
        public LossReason? Loss { get; private set; }

        private readonly IRandomSource rng;
        private readonly IScoreCalculator scorer;
        private readonly GameSession session;
        private readonly ITurnHooks hooks;

        // ---- per-turn state, valid only while a placement is resolving ----
        private readonly ScoreBreakdown breakdown = new ScoreBreakdown();
        private TurnReport currentReport;
        private TurnContext currentTurn;
        private bool scoreFinalized;
        private bool sweepResolvedThisTurn;
        private bool boardCleanBeforeExplosion;
        private int cubesDestroyedThisTurn;
        private bool pendingAdvanceOffer;

        /// <summary>Fires after every resolved placement. The UI subscribes here.
        /// Jokers do NOT - they get ordered, mid-turn callbacks through ITurnHooks.</summary>
        public event Action<TurnReport> TurnResolved;

        /// <summary>Fires on every Status change. NOTE: not fired for a loss detected during
        /// construction - the creator must check Status right after constructing.</summary>
        public event Action<RoundStatus> StatusChanged;

        public RoundEngine(RoundConfig config, RoundRules rules, IEnumerable<BlockCard> ownedCards,
            IRandomSource rng, IScoreCalculator scorer)
            : this(config, rules, ownedCards, rng, scorer, null, null)
        {
        }

        /// <summary>Full constructor. session/hooks are null when a round is driven directly
        /// by a test or a simulation; the engine then behaves exactly as the base game.</summary>
        public RoundEngine(RoundConfig config, RoundRules rules, IEnumerable<BlockCard> ownedCards,
            IRandomSource rng, IScoreCalculator scorer, GameSession session, ITurnHooks hooks)
        {
            Config = config;
            Rules = rules;
            this.rng = rng;
            this.scorer = scorer;
            this.session = session;
            this.hooks = hooks ?? NoTurnHooks.Instance;
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
        /// continuing resumes play under overtime rules.</summary>
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
            CheckForNoPlayableMove();
        }

        /// <summary>Adds a card to the bonus hand. Base game: unused. Future powers
        /// (Klon, Dolly, Olta, Kara delik...) and tests are the callers.</summary>
        public void AddBonusCard(BlockCard card, BonusPlayOutcome outcomeOnPlay)
        {
            bonusHand.Add(new BonusSlot(card, outcomeOnPlay));
        }

        /// <summary>
        /// Discards the entire hand and draws a fresh one. Does NOT consume a turn.
        /// Bound to a debug key in the UI and used by the "Renovasyon" joker.
        /// CONFIRMED RULE: before the threshold the discard is recycled first (so the hand
        /// can always be refilled); in overtime it is NOT - only a clean sweep recycles the
        /// discard there, which makes redrawing in overtime a deliberate gamble.
        /// </summary>
        public void RedrawHand()
        {
            EnsurePlacingAllowed();
            while (Hand.Count > 0)
            {
                Deck.Discard(Hand.RemoveAt(Hand.Count - 1));
            }
            if (!ThresholdPassed)
            {
                Deck.ShuffleDiscardIntoDraw();
            }
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
        /// consume a turn. Follows the normal draw rules, so in overtime an empty draw pile
        /// is a loss - exactly like any other draw.
        /// </summary>
        public BlockCard ReplaceHandCard(int handIndex)
        {
            EnsurePlacingAllowed();
            if (handIndex < 0 || handIndex >= Hand.Count)
            {
                throw new ArgumentOutOfRangeException("handIndex");
            }
            BlockCard returned = Hand.RemoveAt(handIndex);
            Deck.Discard(returned);
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

            breakdown.Reset();
            var report = new TurnReport();
            report.TurnNumber = TurnNumber;
            report.Card = card;
            report.PlayedFromBonusHand = fromBonus;
            report.Origin = origin;
            report.Score = breakdown;

            currentReport = report;
            currentTurn = new TurnContext(session, rng, this, report, breakdown);
            scoreFinalized = false;
            sweepResolvedThisTurn = false;
            cubesDestroyedThisTurn = 0;
            pendingAdvanceOffer = false;

            // 1. place + score
            report.PlacedCells = Board.Place(card, origin);
            breakdown.BasePlacement = scorer.ScorePlacement(report.PlacedCells.Count);

            // Sampled AFTER the placement on purpose: the cubes just placed are what make the
            // board "not clean", which is the pre-condition a real sweep has to clear.
            boardCleanBeforeExplosion = Board.IsCleanForSweep();

            // 2. explode full lines + score
            LineExplosionResult explosion = Board.ResolveFullLines();
            report.ExplodedRows = explosion.Rows;
            report.ExplodedColumns = explosion.Columns;
            report.CubesExploded = explosion.ExplodedCells.Count;
            cubesDestroyedThisTurn += explosion.ExplodedCells.Count;
            if (explosion.LineCount > 0)
            {
                breakdown.BaseLines = scorer.ScoreLineExplosion(explosion.LineCount, explosion.ExplodedCells.Count);
            }
            hooks.AfterLineExplosion(currentTurn);

            // 3. clean sweep (single central event - see the file header)
            TryResolveCleanSweep();

            // 4. finalize the score
            hooks.ModifyScore(currentTurn);
            scoreFinalized = true;
            RoundScore += breakdown.Total;
            report.ScoreGained = breakdown.Total;
            report.RoundScoreAfter = RoundScore;

            // 5. card disposition
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
                // 6. refill
                RefillHand();
            }

            // 7. end-of-turn effects (may still add score - see step 8)
            hooks.AfterTurnScored(currentTurn);

            // 8. threshold check (first pass only)
            if (!ThresholdPassed && RoundScore >= Config.ScoreThreshold)
            {
                ThresholdPassed = true;
                report.ThresholdJustPassed = true;
                Deck.ShuffleDiscardIntoDraw();
                pendingAdvanceOffer = true;
            }

            // 9./10. status update - see file header for why the offer outranks the loss.
            if (pendingAdvanceOffer)
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

            currentReport = null;
            currentTurn = null;

            if (TurnResolved != null)
            {
                TurnResolved(report);
            }
            return report;
        }

        /// <summary>
        /// THE clean-sweep check. Fires at most once per turn and only when this turn's
        /// destruction actually emptied a board that was not already empty. Joker effects
        /// that can trigger a sweep (Robot supurge's last cube, Kayit defteri's counter)
        /// call this instead of testing the board themselves. Returns true if it fired.
        /// </summary>
        internal bool TryResolveCleanSweep()
        {
            if (sweepResolvedThisTurn || currentReport == null)
            {
                return false;
            }
            if (cubesDestroyedThisTurn <= 0 || boardCleanBeforeExplosion || !Board.IsCleanForSweep())
            {
                return false;
            }

            sweepResolvedThisTurn = true;
            CleanSweepCount++;
            currentReport.CleanSweep = true;

            int sweepBonus = scorer.ScoreCleanSweep();
            if (scoreFinalized)
            {
                // A sweep triggered by an end-of-turn effect still belongs to this turn.
                AddLateTurnScore(sweepBonus, "base.sweep");
            }
            else
            {
                breakdown.BaseSweep += sweepBonus;
            }

            if (ThresholdPassed)
            {
                // Overtime reward+price: fresh draw pile, minus N random cards, new offer.
                Deck.ShuffleDiscardIntoDraw();
                currentReport.CardsRemovedForRound =
                    Deck.RemoveRandomFromDraw(Rules.OvertimeCardsRemovedPerCleanSweep);
                pendingAdvanceOffer = true;
            }

            hooks.AfterCleanSweep(currentTurn);
            return true;
        }

        /// <summary>Destroys cubes outside the normal line explosion (joker/power effects).
        /// Scoreless by itself - the caller decides whether to award points. Feeds the
        /// central sweep pre-condition, so a joker can empty the board and TryResolveCleanSweep
        /// will accept it. EXTENSION POINT for Robot supurge, Buldozer, Enfeksiyon, Tutustur.</summary>
        internal IReadOnlyList<GridPos> DestroyCubes(IEnumerable<GridPos> cells, bool countsForSweep)
        {
            var destroyed = new List<GridPos>();
            foreach (GridPos pos in cells)
            {
                if (Board.DestroyCube(pos))
                {
                    destroyed.Add(pos);
                }
            }
            if (countsForSweep)
            {
                cubesDestroyedThisTurn += destroyed.Count;
            }
            return destroyed;
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
            RoundScore += amount;
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

        /// <summary>Discards the whole hand and draws a full new one, without recycling the
        /// discard. "Seri tetik" uses this at end of turn. Consumes no turn.</summary>
        internal void CycleHandWithoutReshuffle()
        {
            while (Hand.Count > 0)
            {
                Deck.Discard(Hand.RemoveAt(Hand.Count - 1));
            }
            RefillHand();
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
