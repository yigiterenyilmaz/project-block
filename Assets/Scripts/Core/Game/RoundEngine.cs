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

        /// <summary>The threshold this round actually asks for, in logical points. Normally
        /// Config.ScoreThreshold; a boss may ask for less ("Taş ve sopa"). Read live, so the UI
        /// and the rules can never disagree about what the bar is.</summary>
        public int ScoreThreshold
        {
            get
            {
                int threshold = Config.ScoreThreshold;
                if (Boss != null)
                {
                    threshold = Boss.FilterScoreThreshold(threshold);
                }
                if (HasMirrorWorld)
                {
                    // "Öteki dünya" doubles your board and raises the bar to match. Rounded UP,
                    // so opening the second world always costs something.
                    threshold = (int)System.Math.Ceiling(threshold * MirrorThresholdFactor);
                }
                return threshold;
            }
        }

        /// <summary>ScoreThreshold lifted into the same scaled units as RoundScore.</summary>
        private int ScaledThreshold
        {
            get { return ScoreThreshold * scorer.ScoreScale; }
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

        /// <summary>This round's boss ("patron"), or null on an ordinary round. Round-scoped:
        /// it dies with this engine, which is what keeps a boss's rule bends from leaking into
        /// the next round. Assigned once by GameSession before the round's first turn.</summary>
        public BossRound Boss { get; private set; }

        /// <summary>Attaches the boss GameSession drew for this round. Called once, right
        /// after construction and before any hook runs.</summary>
        internal void SetBoss(BossRound boss)
        {
            Boss = boss;
            // The board stamps cubes itself, so it needs the answer too ("Vanilya"). Carried
            // across by CreateResized, so an inflation power cannot lose it mid-round.
            Board.IgnoreElements = ElementsIgnored;
        }

        /// <summary>True while every block must behave as a plain block ("Vanilya"): no fire,
        /// no ghost overhang, no rotation, no dynamite - the element is simply not there.</summary>
        public bool ElementsIgnored
        {
            get { return Boss != null && Boss.IgnoresBlockElements; }
        }

        /// <summary>Does this card carry that element RIGHT NOW? The one place the engine asks,
        /// so a boss that suppresses elements suppresses ALL of them consistently - placement,
        /// rotation, the no-move check and the cube kinds alike.</summary>
        private bool Has(BlockCard card, BlockElement element)
        {
            return !ElementsIgnored && card.Has(element);
        }

        /// <summary>Public form of the above, for the UI: it must not offer a rotation or a
        /// reshape that the engine would refuse.</summary>
        public bool CardHasElement(BlockCard card, BlockElement element)
        {
            return card != null && Has(card, element);
        }

        /// <summary>True if this round's boss has silenced that joker ("Anarşi", "Oburluk"):
        /// every hook is skipped and it cannot be activated, exactly like the overtime gate.
        /// Public so the UI can grey the panel out for the same reason the rules ignore it.</summary>
        public bool IsSilencedByBoss(Joker joker)
        {
            return Boss != null && joker != null && Boss.DisablesJoker(joker);
        }

        /// <summary>As above, for a power: it cannot be used and its hooks are skipped.</summary>
        public bool IsSilencedByBoss(Power power)
        {
            return Boss != null && power != null && Boss.DisablesPower(power);
        }

        /// <summary>True while nothing may put a charge back into a power this round
        /// ("Tükenmişlik"). The round-start charge is already in place by then.</summary>
        public bool PowerRechargeBlocked
        {
            get { return Boss != null && Boss.BlocksPowerRecharge; }
        }

        /// <summary>True while every joker pays the player BACKWARDS ("Terslik"): the points and
        /// the sell value it grants itself become losses of the same size. Read live by
        /// JokerInventory, which opens the inversion window around joker dispatch only - powers,
        /// the base score and the engine's own bookkeeping are never inverted.</summary>
        public bool InvertsJokerScore
        {
            get { return Boss != null && Boss.InvertsJokerScore; }
        }

        /// <summary>Forbids placement on one empty cell ("Mapus"). Board mutations go through
        /// the engine, so the seal and the no-playable-move check can never disagree.</summary>
        internal void SealBoardCell(GridPos cell)
        {
            Board.SealCell(cell);
        }

        /// <summary>Lifts every placement seal (a boss re-picks its cell each turn).</summary>
        internal void ClearBoardSeals()
        {
            Board.ClearSeals();
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
                // Priced through the boss too ("Ufuk"/"Kule" govern every line clear), but with
                // no dead-zone adjustment - that is this path's long-standing behaviour.
                AddScoreOutsideTurn(PriceLines(
                    BuildLineScore(lines, lines.ExplodedCells.Count, false)));
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

        // ---- anti-stalling clock: the arena erodes once the deck keeps recycling ----

        /// <summary>Times the DRAW PILE RAN DRY this round and the discard was poured back into
        /// it. Deliberately not RoundDeck.ShuffleCount: that also counts the reshuffles the rules
        /// and the jokers order (the threshold recycle, a hand redraw, "Dezenformasyon" every
        /// single turn), and none of those mean "you have run out of cards".</summary>
        public int DeckRecycleCount { get; private set; }

        /// <summary>Erosions already applied to this round's board.</summary>
        public int BoardErosionCount { get; private set; }

        /// <summary>Recycles left before the board starts eroding, or 0 once it has started.
        /// The UI shows this as the round's shot clock.</summary>
        public int FreeDeckRecyclesLeft
        {
            get
            {
                int left = Rules.FreeDeckRecycles - DeckRecycleCount;
                return left > 0 ? left : 0;
            }
        }

        /// <summary>Called from the ONE place a dry draw pile is refilled from the discard
        /// (DrawWithRules). The erosion it earns is applied later, at a safe point in the turn -
        /// never in the middle of a draw.</summary>
        private void NoteDeckRecycled()
        {
            DeckRecycleCount++;
        }

        /// <summary>TEST/DEBUG seam: pretends the draw pile ran dry once and settles whatever
        /// erosion that earns, so the clock can be driven without dealing out a whole deck.
        /// Real play goes through DrawWithRules -> NoteDeckRecycled.</summary>
        internal void DebugForceDeckRecycle()
        {
            NoteDeckRecycled();
            ApplyPendingBoardErosion();
        }

        /// <summary>
        /// Eats as much of the board as the recycles so far have earned. Idempotent: it only ever
        /// applies the difference between what is owed and what has already been applied, so it is
        /// safe to call from several places and safe to call every turn.
        ///
        /// Deliberately NOT called from inside DrawWithRules: a draw happens in the middle of a
        /// refill loop, and reshaping the board there would move the ground under the placement
        /// being resolved. The turn resolver calls this after the end-of-turn hooks instead, so
        /// the erosion still lands on the same turn that ran the deck dry, before the threshold
        /// check and before the dead-end check - which is what lets erosion legitimately end a
        /// round.
        /// </summary>
        private void ApplyPendingBoardErosion()
        {
            ShuffleErosion mode = Config.Erosion;
            if (mode == ShuffleErosion.None)
            {
                return;
            }
            int owed = DeckRecycleCount - Rules.FreeDeckRecycles;
            if (owed <= BoardErosionCount)
            {
                return;
            }
            bool changed = false;
            while (BoardErosionCount < owed)
            {
                BoardErosionCount++;
                changed |= ErodeOnce(BoardErosionCount, mode);
            }
            if (changed)
            {
                // Shrinking makes the surviving rows shorter, so the squeeze can complete a line
                // that was one cell short. Same treatment as the inflation deflate: it explodes
                // under the normal rules and only scores while "Genel temizlik" is held.
                ResolveFullLinesOutsideTurn();
            }
        }

        /// <summary>One erosion step. The step number drives BOTH which sides the rim loses and
        /// how big the centre hole is, so the two styles stay in lockstep in the "Both" band.</summary>
        private bool ErodeOnce(int step, ShuffleErosion mode)
        {
            bool changed = false;
            if (mode == ShuffleErosion.FromOutside || mode == ShuffleErosion.Both)
            {
                changed |= ErodeRim(step);
            }
            if (mode == ShuffleErosion.FromCenter || mode == ShuffleErosion.Both)
            {
                changed |= ErodeCentre(step);
            }
            return changed;
        }

        /// <summary>
        /// Takes one row and one column off the rim, ALTERNATING sides: odd steps take the top
        /// row and the right column, even steps the bottom row and the left column. Over two
        /// steps that is symmetric, so the arena stays where the player is looking instead of
        /// creeping into a corner.
        ///
        /// Cubes standing in the doomed bands are destroyed, scorelessly and without counting
        /// toward a clean sweep or "Kayıt defteri" - the same terms as Buldozer and Deprem.
        /// </summary>
        private bool ErodeRim(int step)
        {
            bool topRight = (step % 2) == 1;
            int left = topRight ? 0 : 1;
            int right = topRight ? 1 : 0;
            int bottom = topRight ? 0 : 1;
            int top = topRight ? 1 : 0;
            if (Board.Width - left - right < 1 || Board.Height - bottom - top < 1)
            {
                return false; // nothing left to take - the board is already a sliver
            }
            DestroyCubes(RimCells(left, right, bottom, top), false, true);
            return ReshapeBoard(-left, -right, -bottom, -top);
        }

        /// <summary>Every cell of the bands about to be removed, in absolute coordinates.</summary>
        private List<GridPos> RimCells(int left, int right, int bottom, int top)
        {
            int minX = Board.MinX;
            int minY = Board.MinY;
            int maxX = Board.MinX + Board.Width - 1;
            int maxY = Board.MinY + Board.Height - 1;
            var cells = new List<GridPos>();
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (x < minX + left || x > maxX - right || y < minY + bottom || y > maxY - top)
                    {
                        cells.Add(new GridPos(x, y));
                    }
                }
            }
            return cells;
        }

        /// <summary>
        /// Hollows the board out from the middle: a step x step square of DEAD cells centred on
        /// the current board. Step 1 is the single centre cell, step 2 a 2x2, step 3 a 3x3.
        ///
        /// An even-sided square cannot sit exactly in the middle of an odd-sided board, so it is
        /// biased toward the lower coordinates ((size - n) / 2 truncates). Odd steps land dead
        /// centre, and because the centre is recomputed on the CURRENT board every step, the dead
        /// region is simply the union of the squares placed so far - cells never come back.
        ///
        /// The cells are eaten, not just emptied: they kill their rows and columns for good.
        /// </summary>
        private bool ErodeCentre(int step)
        {
            int w = step < Board.Width ? step : Board.Width;
            int h = step < Board.Height ? step : Board.Height;
            if (w < 1 || h < 1)
            {
                return false;
            }
            int startX = Board.MinX + (Board.Width - w) / 2;
            int startY = Board.MinY + (Board.Height - h) / 2;
            var doomed = new List<GridPos>();
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    doomed.Add(new GridPos(startX + x, startY + y));
                }
            }
            // Destroy through the engine first so the destruction log and the per-card
            // bookkeeping see it; MarkDead then clears anything that resisted (a Parazit host
            // cannot squat on a cell that no longer exists) and eats the cells themselves.
            DestroyCubes(doomed, false, true);
            List<GridPos> eaten = Board.MarkDead(doomed);
            if (eaten.Count == 0)
            {
                return false;
            }
            LogDestruction();
            return true;
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

        /// <summary>True once the draw pile has been reported dry and no card has been drawn
        /// since. Keeps "the deck ran out" a single event per drying-out, however many draw
        /// attempts hit the empty pile ("Harcama vergisi" taxes per event, not per attempt).</summary>
        private bool drawPileReportedEmpty;

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
