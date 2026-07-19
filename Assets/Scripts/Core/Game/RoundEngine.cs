// PURPOSE: The turn-by-turn state machine of ONE round. This file is the heart of the
// game rules - read it top to bottom to understand the confirmed game design.
//
// TURN RESOLUTION ORDER (keep this order stable; mechanics depend on it):
//   1. place cubes on the board, score the placement
//   2. explode full rows/columns, score them       -> hook: AfterLineExplosion
//   3. clean-sweep check + bonus                   -> hook: AfterCleanSweep
//      if in overtime: reshuffle discard into draw and queue a new advance offer
//   4. element upkeep: gold pays per turn while it sits on the board
//   5. finalize the score                          -> hook: ModifyScore (jokers add here)
//      the turn's points are banked into RoundScore exactly once, floored once
//   6. played card leaves the hand -> discard (bonus cards: expire + burn top of draw)
//   7. refill the hand (loss rules live here, see RefillHand)
//   8. end-of-turn effects                         -> hook: AfterTurnScored
//      score granted here still counts toward the threshold (checked next)
//   9. threshold check: first time the round score reaches the threshold ->
//      reshuffle discard into draw, queue advance offer
//  10. status update: pending advance offer outranks a same-turn loss (the player who
//      just earned an offer may escape by advancing; if they continue, the loss hits)
//  11. otherwise: lose if no held block fits the board
//
// CONTINUING HAS A PRICE (confirmed 2026-07-18): declining an advance offer shuffles
// the whole hand back into the draw pile, removes Rules.CardsRemovedPerContinue
// random cards face-down for the round, and draws a fresh hand - see DecideAdvance.
//
// CLEAN SWEEP IS ONE CENTRAL EVENT. TryResolveCleanSweep is the only place it can fire,
// at most once per turn, and it requires the board to have gone from "not clean" to
// "clean" through an actual destruction this turn. Note this is STRICTER than a plain
// "a line exploded" test: a full line made only of indestructible cubes destroys nothing,
// so it no longer re-triggers a sweep every turn once obsidian/gold sit on the board.
// Joker effects that can trigger a sweep call this; they never re-check the board.
//
// EXTENSION POINTS:
//  - Powers ("güçler") = new public methods here (they act on the round state).
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
        /// <summary>Replaced wholesale by the inflation powers, which resize it mid-round.</summary>
        public GameBoard Board { get; private set; }
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
        private readonly GameSession session;
        private readonly ITurnHooks hooks;

        private sealed class DynamiteState
        {
            public int FullSize;
            public int RemainingAtTurnStart;

            /// <summary>Turn the block was placed. TNT only clears the board if the whole
            /// block explodes on this same turn (confirmed rule) - a block that survives to a
            /// later turn and then goes whole no longer detonates.</summary>
            public int PlacementTurn;
        }

        /// <summary>Dynamite blocks on the board (confirmed rule: they trigger on ANY
        /// turn where the still-intact block explodes at once, not just placement turn).</summary>
        private readonly Dictionary<int, DynamiteState> dynamiteBlocks =
            new Dictionary<int, DynamiteState>();

        /// <summary>Fox reshape choices and mechanical rotation steps, per card id.</summary>
        private readonly Dictionary<int, BlockShape> foxShapes = new Dictionary<int, BlockShape>();
        private readonly Dictionary<int, int> rotations = new Dictionary<int, int>();

        /// <summary>Cubes each card put on the board, so "the whole block went at once"
        /// can be told apart from "its last surviving cube went" ("Kazı çalışması").</summary>
        private readonly Dictionary<int, int> cardPlacedSize = new Dictionary<int, int>();

        // ---- destruction tracking: the board is diffed against a snapshot, so every
        // source (lines, fire chains, dynamite, joker effects) is captured the same way ----
        private readonly Dictionary<GridPos, Cube> boardSnapshot = new Dictionary<GridPos, Cube>();
        private readonly Dictionary<int, int> cardCubesAtTurnStart = new Dictionary<int, int>();
        private readonly List<DestroyedCube> destroyedThisTurn = new List<DestroyedCube>();
        private readonly List<int> cardsFullyDestroyedThisTurn = new List<int>();

        /// <summary>Board state as it stood at the START of recent turns, newest last.
        /// "Kum saati" rewinds into this. Only a few turns are kept - the power reaches two
        /// back and nothing needs more.</summary>
        private readonly List<Dictionary<GridPos, Cube>> boardHistory =
            new List<Dictionary<GridPos, Cube>>();

        private const int BoardHistoryDepth = 4;

        /// <summary>How many past board states are available to rewind into.</summary>
        internal int BoardHistoryCount
        {
            get { return boardHistory.Count; }
        }

        /// <summary>Rewinds ONLY the board to how it looked <paramref name="turns"/> turns ago.
        /// The hand, the piles and the score deliberately stay where they are - that is the
        /// confirmed rule for "Kum saati". Returns false when the history is too short.</summary>
        internal bool RewindBoard(int turns)
        {
            if (turns < 1 || boardHistory.Count < turns)
            {
                return false;
            }
            Dictionary<GridPos, Cube> past = boardHistory[boardHistory.Count - turns];
            Board.RestoreFrom(past);
            // Everything that remembers board positions is now talking about a board that no
            // longer exists, so the destruction bookkeeping is re-based on what is there now.
            ResyncSnapshot();
            CaptureTurnStartCardCounts();
            return true;
        }

        /// <summary>
        /// Grows (positive) or shrinks (negative) the board on each side, mid-round. Every
        /// surviving cube comes across; the board object itself is replaced, which the UI
        /// notices because it compares board references.
        ///
        /// Coordinates do NOT move: growing on the left or bottom pushes the board's origin
        /// into negative space instead of renumbering cells, so a cube at (2,3) is still at
        /// (2,3) afterwards. That is why nothing here has to be invalidated - the rewind
        /// history, the echo memory and every other remembered position stay valid.
        /// </summary>
        internal bool ReshapeBoard(int left, int right, int bottom, int top)
        {
            GameBoard resized = GameBoard.CreateResized(Board, left, right, bottom, top);
            if (resized == null)
            {
                return false;
            }
            Board = resized;
            ResyncSnapshot();
            CaptureTurnStartCardCounts();
            return true;
        }

        /// <summary>
        /// Shrinks the board, first pushing any cube standing in a doomed band inward to the
        /// nearest free cell on its row/column ("blokları geri ittirir"). A cube with nowhere
        /// to go is destroyed. Afterwards the normal line rules run on the tighter board, so a
        /// row that the squeeze happened to complete explodes on its own.
        /// </summary>
        internal bool ShrinkBoardPushingInward(int left, int right, int bottom, int top)
        {
            PushInward(left, right, bottom, top);
            if (!ReshapeBoard(-left, -right, -bottom, -top))
            {
                return false;
            }
            ResolveFullLinesOutsideTurn();
            return true;
        }

        /// <summary>Resolves any full lines created OUTSIDE a placement - a power that reshaped
        /// or filled the board ("Bardağın boş tarafı", inflation deflate). Scores and logs them
        /// like a normal explosion and offers a sweep check. Safe to call between turns.</summary>
        internal void ResolveFullLinesOutsideTurn()
        {
            LineExplosionResult lines = Board.ResolveFullLines();
            if (lines.LineCount == 0)
            {
                return;
            }
            cubesDestroyedThisTurn += lines.ExplodedCells.Count;
            LogDestruction();
            AddScoreOutsideTurn(scorer.ScoreLineExplosion(lines.LineCount, lines.ExplodedCells.Count));
            TryResolveCleanSweep();
        }

        /// <summary>Moves cubes out of the bands that are about to disappear.</summary>
        private void PushInward(int left, int right, int bottom, int top)
        {
            for (int band = 0; band < left; band++)
            {
                ShiftColumnInward(band, +1, left);
            }
            for (int band = 0; band < right; band++)
            {
                ShiftColumnInward(Board.Width - 1 - band, -1, right);
            }
            for (int band = 0; band < bottom; band++)
            {
                ShiftRowInward(band, +1, bottom);
            }
            for (int band = 0; band < top; band++)
            {
                ShiftRowInward(Board.Height - 1 - band, -1, top);
            }
        }

        private void ShiftColumnInward(int x, int step, int bandWidth)
        {
            for (int y = 0; y < Board.Height; y++)
            {
                var from = new GridPos(x, y);
                Cube? cube = Board.GetCube(from);
                if (!cube.HasValue)
                {
                    continue;
                }
                // A protected (Parazit) cube refuses the forced pickup, so relocating it
                // would duplicate it - leave it in place instead.
                if (!Board.DestroyCubeForced(from))
                {
                    continue;
                }
                for (int scan = x + step; scan >= 0 && scan < Board.Width; scan += step)
                {
                    var to = new GridPos(scan, y);
                    if (Board.IsInside(to) && !Board.GetCube(to).HasValue)
                    {
                        Board.SetCubeAt(to, cube.Value);
                        break;
                    }
                }
            }
        }

        private void ShiftRowInward(int y, int step, int bandHeight)
        {
            for (int x = 0; x < Board.Width; x++)
            {
                var from = new GridPos(x, y);
                Cube? cube = Board.GetCube(from);
                if (!cube.HasValue)
                {
                    continue;
                }
                // A protected (Parazit) cube refuses the forced pickup, so relocating it
                // would duplicate it - leave it in place instead.
                if (!Board.DestroyCubeForced(from))
                {
                    continue;
                }
                for (int scan = y + step; scan >= 0 && scan < Board.Height; scan += step)
                {
                    var to = new GridPos(x, scan);
                    if (Board.IsInside(to) && !Board.GetCube(to).HasValue)
                    {
                        Board.SetCubeAt(to, cube.Value);
                        break;
                    }
                }
            }
        }

        /// <summary>"Kayıt defteri": while true, emptying the board is no longer a sweep.
        /// Only ForceCleanSweep can raise the event.</summary>
        internal bool SuppressNaturalSweep { get; set; }

        /// <summary>Powers used since the last placement. The confirmed rule is at most ONE
        /// power per turn; using one never costs a turn, so this is the only thing limiting
        /// them. Reset when a placement resolves.</summary>
        public int PowersUsedThisTurn { get; private set; }

        internal void NotePowerUsed()
        {
            PowersUsedThisTurn++;
        }

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
            Board = new GameBoard(config.BoardWidth, config.BoardHeight, config.ExtraPlayableCells);
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

        private TurnReport ResolvePlacement(BlockCard card, GridPos origin, bool fromBonus,
            BonusPlayOutcome bonusOutcome)
        {
            TurnNumber++;
            int shufflesBeforeTurn = Deck.ShuffleCount;

            // Remember the board as it stands BEFORE this placement, so "Kum saati" can
            // rewind into it later. Oldest entries fall off the front.
            var turnStartBoard = new Dictionary<GridPos, Cube>();
            Board.SnapshotInto(turnStartBoard);
            boardHistory.Add(turnStartBoard);
            if (boardHistory.Count > BoardHistoryDepth)
            {
                boardHistory.RemoveAt(0);
            }

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
            destroyedThisTurn.Clear();
            cardsFullyDestroyedThisTurn.Clear();
            report.DestroyedCubes = destroyedThisTurn;
            report.CardsFullyDestroyed = cardsFullyDestroyedThisTurn;

            // 1. place + score
            report.PlacedCells = Board.Place(card, EffectiveShape(card), origin,
                card.Has(BlockElement.Ghost));
            if (card.Has(BlockElement.Dynamite))
            {
                var state = new DynamiteState();
                state.FullSize = report.PlacedCells.Count;
                state.RemainingAtTurnStart = report.PlacedCells.Count;
                state.PlacementTurn = TurnNumber;
                dynamiteBlocks[card.Id] = state;
            }
            breakdown.BasePlacement = scorer.ScorePlacement(report.PlacedCells.Count);
            var waterFrames = new List<IReadOnlyList<WaterMove>>();

            cardPlacedSize[card.Id] = report.PlacedCells.Count;

            // 2. explode full lines + score (fire chains resolve inside the board).
            // WATER RULE (confirmed 2026-07-19): a freshly placed water block that completes a
            // line explodes IN PLACE, before it would drop into any empty space beneath it.
            // Only when the placement triggers no explosion does the water settle and we
            // re-check the lines it may complete after falling.
            // boardCleanBeforeExplosion is sampled right before the destruction we score, so a
            // sweep still sees the pre-explosion board. Water only moves cubes (a fall never
            // changes the clean check), but a fire->obsidian douse can, hence the resample.
            // The destruction snapshot baselines here, before the first explosion attempt,
            // and is resynced after every settle - moved water must not read as destroyed.
            boardCleanBeforeExplosion = Board.IsCleanForSweep();
            ResyncSnapshot();
            CaptureTurnStartCardCounts();
            LineExplosionResult explosion = Board.ResolveFullLines();
            if (explosion.LineCount == 0)
            {
                Board.SettleWaterAndReact(waterFrames); // nothing exploded in place -> water falls
                ResyncSnapshot(); // water moved, nothing died - re-baseline the destruction diff
                boardCleanBeforeExplosion = Board.IsCleanForSweep();
                explosion = Board.ResolveFullLines();
            }
            // Frames appended after this point are post-explosion falls; the UI plays the
            // boom between the two batches.
            report.WaterFramesBeforeExplosion = waterFrames.Count;
            report.ExplodedRows = explosion.Rows;
            report.ExplodedColumns = explosion.Columns;
            int cubesExploded = explosion.ExplodedCells.Count;

            // DYNAMITE RULE (confirmed 2026-07-18): any dynamite block that was intact at
            // turn start and got fully exploded in one shot clears the entire board.
            if (explosion.LineCount > 0 && dynamiteBlocks.Count > 0)
            {
                bool boom = false;
                var trackedIds = new List<int>(dynamiteBlocks.Keys);
                foreach (int id in trackedIds)
                {
                    DynamiteState state = dynamiteBlocks[id];
                    int remaining = Board.CountCubesOf(id);
                    if (remaining == 0)
                    {
                        dynamiteBlocks.Remove(id);
                        // Only the block placed THIS turn detonates the board (confirmed):
                        // a still-whole block that lingers to a later turn just explodes.
                        if (state.RemainingAtTurnStart == state.FullSize
                            && state.PlacementTurn == TurnNumber)
                        {
                            boom = true;
                        }
                    }
                    else
                    {
                        state.RemainingAtTurnStart = remaining;
                    }
                }
                if (boom)
                {
                    cubesExploded += Board.DestroyAllDestructible().Count;
                    report.DynamiteTriggered = true;
                    // blocks wiped by the clear must not delayed-trigger next turn
                    foreach (int id in new List<int>(dynamiteBlocks.Keys))
                    {
                        if (Board.CountCubesOf(id) == 0)
                        {
                            dynamiteBlocks.Remove(id);
                        }
                    }
                }
            }
            report.CubesExploded = cubesExploded;
            cubesDestroyedThisTurn += cubesExploded;
            // Logged BEFORE the post-explosion settle: settling MOVES water, and a moved
            // cube would otherwise look like a destroyed one to the snapshot diff.
            LogDestruction();
            if (explosion.LineCount > 0)
            {
                breakdown.BaseLines = scorer.ScoreLineExplosion(explosion.LineCount, cubesExploded);
                Board.SettleWaterAndReact(waterFrames); // explosions pull the floor out from water
                ResyncSnapshot();
            }
            report.WaterFallFrames = waterFrames;
            hooks.AfterLineExplosion(currentTurn);

            // 3. clean sweep (single central event - see the file header)
            TryResolveCleanSweep();

            // 4. element upkeep: gold pays while it sits on the board
            int goldCubes = Board.CountCubesOfKind(CubeKind.Gold);
            if (goldCubes > 0)
            {
                report.GoldBonus = scorer.ScoreGoldBonus(goldCubes);
                breakdown.BaseGold = report.GoldBonus;
            }

            // 5. finalize the score
            hooks.ModifyScore(currentTurn);
            scoreFinalized = true;
            RoundScore += breakdown.Total;
            report.ScoreGained = breakdown.Total;
            report.RoundScoreAfter = RoundScore;

            // 6. card disposition
            if (fromBonus)
            {
                if (bonusOutcome == BonusPlayOutcome.ToDiscard)
                {
                    DisposeCard(card);
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
                    DisposeCard(report.BurnedCard);
                }
                // Bonus plays do not refill the hand - the hand was not touched.
            }
            else
            {
                DisposeCard(card);
                // 7. refill
                RefillHand();
            }

            // 8. end-of-turn effects (may still add score - see step 9)
            hooks.AfterTurnScored(currentTurn);
            if (session != null)
            {
                session.Powers.DispatchAfterTurnScored(currentTurn);
            }

            // 9. threshold check (first pass only)
            if (!ThresholdPassed && RoundScore >= Config.ScoreThreshold)
            {
                ThresholdPassed = true;
                report.ThresholdJustPassed = true;
                Deck.ShuffleDiscardIntoDraw();
                pendingAdvanceOffer = true;
                EnterOvertime();
            }

            // 10./11. status update - see file header for why the offer outranks the loss.
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

            // A new turn begins: its single power slot is free again.
            PowersUsedThisTurn = 0;
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
            return ResolveCleanSweep(false);
        }

        private bool ResolveCleanSweep(bool forced)
        {
            if (sweepResolvedThisTurn || currentReport == null)
            {
                return false;
            }
            if (!forced)
            {
                if (SuppressNaturalSweep)
                {
                    return false;
                }
                if (cubesDestroyedThisTurn <= 0 || boardCleanBeforeExplosion
                    || !Board.IsCleanForSweep())
                {
                    return false;
                }
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
                // Overtime reward: a fresh draw pile and a new chance to leave.
                Deck.ShuffleDiscardIntoDraw();
                pendingAdvanceOffer = true;
            }

            hooks.AfterCleanSweep(currentTurn);
            if (session != null)
            {
                // The sweep is the powers' economy: it puts every charge back.
                session.Powers.DispatchCleanSweep(currentTurn);
            }
            return true;
        }

        /// <summary>Destroys cubes outside the normal line explosion (joker/power effects).
        /// Scoreless by itself - the caller decides whether to award points. Feeds the
        /// central sweep pre-condition, so a joker can empty the board and TryResolveCleanSweep
        /// will accept it. EXTENSION POINT for Robot supurge, Buldozer, Enfeksiyon.</summary>
        internal IReadOnlyList<GridPos> DestroyCubes(IEnumerable<GridPos> cells, bool countsForSweep)
        {
            return DestroyCubes(cells, countsForSweep, false);
        }

        /// <summary>As above; forced ignores cube-kind indestructibility ("elmas kazma").</summary>
        internal IReadOnlyList<GridPos> DestroyCubes(IEnumerable<GridPos> cells, bool countsForSweep,
            bool forced)
        {
            var destroyed = new List<GridPos>();
            foreach (GridPos pos in cells)
            {
                bool gone = forced ? Board.DestroyCubeForced(pos) : Board.DestroyCube(pos);
                if (gone)
                {
                    destroyed.Add(pos);
                }
            }
            if (countsForSweep)
            {
                cubesDestroyedThisTurn += destroyed.Count;
            }
            LogDestruction();
            return destroyed;
        }

        /// <summary>THE way a card leaves play into a pile. Normally the discard; with
        /// "Oryantasyon" it is buried at a random depth in the draw pile instead, which is
        /// why every disposal in this class goes through here.</summary>
        private void DisposeCard(BlockCard card)
        {
            if (Rules.PlayedCardsReturnToDrawPile)
            {
                Deck.InsertRandomIntoDraw(card);
                return;
            }
            Deck.Discard(card);
        }

        /// <summary>"Hologram": moves a bonus-hand card into the discard, folding it back
        /// into the round's pile economy instead of letting it expire unused.</summary>
        internal bool MoveBonusCardToDiscard(int bonusIndex)
        {
            if (bonusIndex < 0 || bonusIndex >= bonusHand.Count)
            {
                return false;
            }
            BlockCard card = bonusHand[bonusIndex].Card;
            bonusHand.RemoveAt(bonusIndex);
            Deck.Discard(card);
            return true;
        }

        /// <summary>Banks score from something that happened BETWEEN turns - a power, which
        /// never costs a turn and therefore has no TurnReport to attach to. Crossing the
        /// threshold here raises the advance offer exactly as it would mid-turn.</summary>
        internal void AddScoreOutsideTurn(int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            if (currentReport != null)
            {
                AddLateTurnScore(amount, "power");
                return;
            }
            RoundScore += amount;
            if (session != null)
            {
                session.AddCurrency(amount);
            }
            if (!ThresholdPassed && RoundScore >= Config.ScoreThreshold)
            {
                ThresholdPassed = true;
                Deck.ShuffleDiscardIntoDraw();
                EnterOvertime();
                SetStatus(RoundStatus.AwaitingAdvanceDecision);
            }
        }

        /// <summary>The one place overtime "begins". Wipes the board-rewind history so "Kum
        /// saati" cannot reach back across the threshold, and lets jokers react to the
        /// transition (Seri Tetik takes its hand bonus back). Also invoked when the player
        /// continues an overtime, so the history restarts each continue.</summary>
        private void EnterOvertime()
        {
            boardHistory.Clear();
            if (session != null)
            {
                session.Jokers.DispatchOvertimeStarted(this);
            }
        }

        /// <summary>Cubes destroyed this turn by sources that COUNT (line explosions, fire
        /// chains, dynamite, joker effects that opted in). Buldozer's scoreless wipe is
        /// excluded, which is what keeps it from feeding "Kayıt defteri".</summary>
        internal int CubesDestroyedThisTurn
        {
            get { return cubesDestroyedThisTurn; }
        }

        /// <summary>Fetches a played card back out of the draw/discard piles, or null if it
        /// is not in either. Used by "Kazı çalışması" to hand a block back to the player.</summary>
        internal BlockCard TakeCardFromPiles(int cardId)
        {
            return Deck.TakeCard(cardId);
        }

        /// <summary>Ends the round with a joker-defined reason ("Batak" losing its bet).
        /// Obeys the standing rule that a pending advance offer outranks a same-turn loss.</summary>
        internal void DeclareLoss(LossReason reason)
        {
            if (Loss == null)
            {
                Loss = reason;
            }
            if (!pendingAdvanceOffer && currentReport == null)
            {
                SetStatus(RoundStatus.Lost);
            }
        }

        /// <summary>Raises the clean sweep from a joker rule rather than from an empty board
        /// ("Kayıt defteri" hitting its cube count). Still at most one sweep per turn.</summary>
        internal bool ForceCleanSweep()
        {
            return ResolveCleanSweep(true);
        }

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
            Loss = LossReason.NoPlayableMove;
            SetStatus(RoundStatus.Lost);
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
