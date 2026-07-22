// PURPOSE: The turn-by-turn state machine of ONE round - the heart of the game rules
// (partial: run state, construction, and the board-reshaping powers). The turn
// resolution order and the central rules live across this class's partial files:
//   .Turn        - ResolvePlacement, the ordered turn resolver
//   .CleanSweep  - the one central clean-sweep event + external destruction
//   .Scoring     - disposal, overtime, dead-zone scoring, retro collapse, loss
//   .Bookkeeping - destruction log, snapshots, draw/refill, no-move check
// Powers = new public methods here; jokers = ITurnHooks + RoundRules mutations.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Runs one round. Created fresh by GameSession for every round.</summary>
    public sealed partial class RoundEngine
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

        /// <summary>Round score in the SCALED economy (every banked turn is multiplied by
        /// ScoringConfig.ScoreScale), so it is compared against ScaledThreshold, not the raw
        /// Config.ScoreThreshold. The overtime win bonus still reads the raw threshold, because
        /// it is a logical amount that the score pipeline scales on the way in.</summary>
        public int RoundScore { get; private set; }

        /// <summary>Config.ScoreThreshold lifted into the same scaled units as RoundScore.</summary>
        private int ScaledThreshold
        {
            get { return Config.ScoreThreshold * scorer.ScoreScale; }
        }

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

        /// <summary>Consecutive line-clearing turns this round (the "kombo" streak). Each turn
        /// that explodes >=1 line increments it and pays comboCount*ComboBonusPerStep; a turn
        /// that clears nothing resets it to 0. Lives for the round - a fresh RoundEngine per
        /// round starts it at 0 - and RedrawHand never touches it (it resolves no turn).</summary>
        private int comboCount;

        // ---- destruction tracking: the board is diffed against a snapshot, so every
        // source (lines, fire chains, dynamite, joker effects) is captured the same way ----
        private readonly Dictionary<GridPos, Cube> boardSnapshot = new Dictionary<GridPos, Cube>();
        private readonly Dictionary<int, int> cardCubesAtTurnStart = new Dictionary<int, int>();
        private readonly List<DestroyedCube> destroyedThisTurn = new List<DestroyedCube>();
        private readonly List<int> cardsFullyDestroyedThisTurn = new List<int>();

        /// <summary>Cells destroyed by BETWEEN-TURN effects (board powers like "Bardağın boş
        /// tarafı" / "Çerçeve") since the last BeginExternalCapture. Those destructions never
        /// reach a TurnReport, so the View reads this to play the explosion FX on them.</summary>
        private readonly List<GridPos> externalDestructionLog = new List<GridPos>();

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
            LineExplosionResult lines = Board.ResolveFullLines(Rules.RetroMode);
            if (lines.LineCount == 0)
            {
                return;
            }
            cubesDestroyedThisTurn += lines.ExplodedCells.Count;
            LogDestruction();
            // Make the late clear visible: a reshape squeeze (inflation deflate) or a board
            // power lands after the placement's own explosion was already drawn, so the View
            // needs the cells to blast + play the boom (see TurnReport.ExtraExplodedCells).
            if (currentReport != null)
            {
                currentReport.AddExtraExplodedCells(lines.ExplodedCells);
            }
            // This is an EXTERNAL (non-placement) clear, so it only scores while "Genel temizlik"
            // is held - the same rule the sweep bonus follows. Without it the board still clears
            // and the FX still play, but no points are gained.
            if (Rules.CountExternalSweeps)
            {
                AddScoreOutsideTurn(scorer.ScoreLineExplosion(lines.LineCount, lines.ExplodedCells.Count));
            }
            TryResolveCleanSweep();
        }

        /// <summary>Moves cubes out of the bands that are about to disappear. Works in ABSOLUTE
        /// coordinates: inflation pushed the board's origin (MinX/MinY) into negative space, so
        /// the doomed bands are the OUTER columns/rows relative to that origin, not to (0,0).</summary>
        private void PushInward(int left, int right, int bottom, int top)
        {
            int maxX = Board.MinX + Board.Width - 1;
            int maxY = Board.MinY + Board.Height - 1;
            for (int band = 0; band < left; band++)
            {
                ShiftColumnInward(Board.MinX + band, +1);
            }
            for (int band = 0; band < right; band++)
            {
                ShiftColumnInward(maxX - band, -1);
            }
            for (int band = 0; band < bottom; band++)
            {
                ShiftRowInward(Board.MinY + band, +1);
            }
            for (int band = 0; band < top; band++)
            {
                ShiftRowInward(maxY - band, -1);
            }
        }

        private void ShiftColumnInward(int x, int step)
        {
            int minY = Board.MinY;
            int maxY = Board.MinY + Board.Height - 1;
            int minX = Board.MinX;
            int maxX = Board.MinX + Board.Width - 1;
            for (int y = minY; y <= maxY; y++)
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
                for (int scan = x + step; scan >= minX && scan <= maxX; scan += step)
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

        private void ShiftRowInward(int y, int step)
        {
            int minX = Board.MinX;
            int maxX = Board.MinX + Board.Width - 1;
            int minY = Board.MinY;
            int maxY = Board.MinY + Board.Height - 1;
            for (int x = minX; x <= maxX; x++)
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
                for (int scan = y + step; scan >= minY && scan <= maxY; scan += step)
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

        /// <summary>Set when a BETWEEN-TURN destruction (a power/joker between placements)
        /// emptied a board that was not already empty. "Genel temizlik" turns these into real
        /// clean sweeps; without it they are ignored, which is the base-game behaviour.</summary>
        private bool externalClearReady;

        /// <summary>Cards frozen in hand -> turns of freeze left. A frozen card cannot be
        /// played and does NOT count as a playable move, so freezing the wrong card can
        /// genuinely end a round ("Hazine" dynamite penalty). Cleared at round start.</summary>
        private readonly Dictionary<int, int> frozenCards = new Dictionary<int, int>();

        /// <summary>Turns of freeze left on a held card, or 0 when it is free to play.</summary>
        public int FreezeTurnsLeft(int cardId)
        {
            int turns;
            return frozenCards.TryGetValue(cardId, out turns) ? turns : 0;
        }

        public bool IsFrozen(int cardId)
        {
            return FreezeTurnsLeft(cardId) > 0;
        }

        /// <summary>Freezes a held card for a number of turns. Returns false if the card is
        /// not in hand or is already frozen.</summary>
        internal bool FreezeHandCard(int cardId, int turns)
        {
            if (turns < 1 || IsFrozen(cardId))
            {
                return false;
            }
            for (int i = 0; i < Hand.Count; i++)
            {
                if (Hand[i].Id == cardId)
                {
                    frozenCards[cardId] = turns;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Counts one turn off every freeze; expired entries are dropped. Called once
        /// per resolved placement, at the end of the turn.</summary>
        private void TickFreezes()
        {
            if (frozenCards.Count == 0)
            {
                return;
            }
            var ids = new List<int>(frozenCards.Keys);
            foreach (int id in ids)
            {
                int left = frozenCards[id] - 1;
                if (left <= 0)
                {
                    frozenCards.Remove(id);
                }
                else
                {
                    frozenCards[id] = left;
                }
            }
        }

        /// <summary>Set when a NEGATIVE block already sampled the sweep pre-condition, so the
        /// normal explosion path does not re-sample it on a board the erasure just changed.</summary>
        private bool cleanSampleLocked;

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
            // "Hileli zar": pull the preset cards to the top so they are the opening hand.
            if (session != null)
            {
                IReadOnlyList<int> preset = session.TakePendingOpeningHand();
                if (preset != null)
                {
                    Deck.MoveToTop(preset);
                }
            }
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
    }
}
