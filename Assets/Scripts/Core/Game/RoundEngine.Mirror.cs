// PURPOSE: RoundEngine's SECOND WORLD - the mirror board "Öteki dünya" opens beneath the main
// one. Everything that makes the round dual-world lives here, so the single-world turn resolver
// stays readable and an ordinary round never touches a line of it.
//
// THE SHAPE OF A DUAL-WORLD TURN. A turn is still ONE resolved placement, but it now carries a
// second one: the player STAGES a mirror play, then plays in the main world, and both land in
// the same turn. That ordering is deliberate - it keeps ResolvePlacement the one turn resolver
// instead of inventing a second, parallel one that would have to duplicate every step.
//
// CONFIRMED RULES:
//  - the mirror is a CLONE of the main board at the moment the power is cast, and it lives
//    until the round ends. Casting it on a full board gives you two full boards;
//  - the two worlds SHARE the deck and the discard. Only the hands are separate;
//  - a world with no legal move sits the turn out; the other world plays on alone. Neither
//    world can lose the round on its own - the loss check asks whether BOTH are stuck;
//  - each world sweeps for ITSELF: emptying either board is a clean sweep;
//  - the same COLUMN exploding in both worlds on the same turn pays a bonus;
//  - the round's threshold is multiplied while the mirror is open.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
        /// <summary>The second board, or null on an ordinary round.</summary>
        public GameBoard MirrorBoard { get; private set; }

        /// <summary>The mirror world's own hand. Drawn from the SHARED deck.</summary>
        public Hand MirrorHand { get; private set; }

        /// <summary>True while the round is being played across two worlds.</summary>
        public bool HasMirrorWorld
        {
            get { return MirrorBoard != null; }
        }

        /// <summary>What the threshold is multiplied by while the mirror is open.</summary>
        public double MirrorThresholdFactor { get; private set; } = 1.0;

        /// <summary>Bonus paid per column that explodes in BOTH worlds on the same turn.</summary>
        private int mirrorColumnBonus;

        /// <summary>Columns that exploded in both worlds this turn - the pay-off the power is
        /// built around. Reset every turn; read by the View for its own celebration.</summary>
        private readonly List<int> mirroredColumnsThisTurn = new List<int>();

        public IReadOnlyList<int> MirroredColumnsThisTurn
        {
            get { return mirroredColumnsThisTurn; }
        }

        // ---- the staged mirror play, valid between StageMirrorPlay and the turn resolving ----
        private BlockCard stagedMirrorCard;
        private GridPos stagedMirrorOrigin;
        private bool mirrorStaged;

        /// <summary>The card waiting to be played in the mirror world, or null.</summary>
        public BlockCard StagedMirrorCard
        {
            get { return stagedMirrorCard; }
        }

        /// <summary>
        /// Opens the second world: clones the board as it stands, deals the mirror its own hand
        /// out of the shared deck, and raises the round's bar. Returns false if a mirror is
        /// already open or the round is not running - this is a once-per-round door.
        /// </summary>
        internal bool OpenMirrorWorld(double thresholdFactor, int columnBonus, int handSize)
        {
            if (HasMirrorWorld || Status != RoundStatus.InProgress)
            {
                return false;
            }
            MirrorBoard = GameBoard.CreateClone(Board);
            MirrorHand = new Hand();
            MirrorThresholdFactor = thresholdFactor > 0 ? thresholdFactor : 1.0;
            mirrorColumnBonus = columnBonus;
            // The mirror draws from the SAME piles, so opening the world costs real cards.
            for (int i = 0; i < handSize; i++)
            {
                BlockCard card = Deck.DrawTop();
                if (card == null)
                {
                    break; // an empty pile is not a loss here - the mirror simply starts short
                }
                MirrorHand.Add(card);
            }
            return true;
        }

        /// <summary>True if that card could be placed there in the mirror world.</summary>
        public bool CanPlaceMirrorCard(BlockCard card, GridPos origin)
        {
            if (!HasMirrorWorld || card == null || IsFrozen(card.Id))
            {
                return false;
            }
            return MirrorBoard.CanPlace(EffectiveShape(card), origin,
                Has(card, BlockElement.Ghost), Has(card, BlockElement.Negative));
        }

        /// <summary>Legal origins for a held mirror card (the UI's placement preview).</summary>
        public List<GridPos> GetValidMirrorOrigins(BlockCard card)
        {
            if (!HasMirrorWorld || card == null || IsFrozen(card.Id))
            {
                return new List<GridPos>();
            }
            var origins = new List<GridPos>();
            GameBoard board = MirrorBoard;
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var origin = new GridPos(x + board.MinX, y + board.MinY);
                    if (CanPlaceMirrorCard(card, origin))
                    {
                        origins.Add(origin);
                    }
                }
            }
            return origins;
        }

        /// <summary>True while the mirror world still has somewhere to put something. A world
        /// that answers false sits the turn out rather than ending the round.</summary>
        public bool MirrorHasAnyMove
        {
            get
            {
                if (!HasMirrorWorld)
                {
                    return false;
                }
                for (int i = 0; i < MirrorHand.Count; i++)
                {
                    BlockCard card = MirrorHand[i];
                    if (IsFrozen(card.Id))
                    {
                        continue;
                    }
                    if (GetValidMirrorOriginsExists(card))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private bool GetValidMirrorOriginsExists(BlockCard card)
        {
            GameBoard board = MirrorBoard;
            bool ghost = Has(card, BlockElement.Ghost);
            bool negative = Has(card, BlockElement.Negative);
            return board.AnyPlacementExists(EffectiveShape(card), ghost, negative);
        }

        /// <summary>
        /// Books the mirror world's half of the turn. Nothing lands yet: the placement resolves
        /// when the main world plays, so both halves belong to the same turn. Staging again
        /// before the turn resolves simply replaces the booking, which is what lets the UI let
        /// the player change their mind.
        /// </summary>
        public bool StageMirrorPlay(int handIndex, GridPos origin)
        {
            if (!HasMirrorWorld || Status != RoundStatus.InProgress)
            {
                return false;
            }
            if (handIndex < 0 || handIndex >= MirrorHand.Count)
            {
                return false;
            }
            BlockCard card = MirrorHand[handIndex];
            if (!CanPlaceMirrorCard(card, origin))
            {
                return false;
            }
            stagedMirrorCard = card;
            stagedMirrorOrigin = origin;
            mirrorStaged = true;
            return true;
        }

        /// <summary>Throws away the staged mirror play without resolving anything.</summary>
        public void ClearStagedMirrorPlay()
        {
            stagedMirrorCard = null;
            mirrorStaged = false;
        }

        /// <summary>True when the main world may resolve a turn: either the mirror has booked its
        /// half, or the mirror has nothing it could legally do and sits this one out.</summary>
        public bool MirrorReadyForTurn
        {
            get { return !HasMirrorWorld || mirrorStaged || !MirrorHasAnyMove; }
        }

        /// <summary>True once the mirror has booked its half of the turn.</summary>
        public bool MirrorHasStagedPlay
        {
            get { return mirrorStaged; }
        }

        /// <summary>True while the MAIN world still has somewhere to play. The mirror's
        /// counterpart of MirrorHasAnyMove, so the UI can tell which world is stuck.</summary>
        public bool MainWorldHasAnyMove
        {
            get
            {
                for (int i = 0; i < Hand.Count; i++)
                {
                    if (CanPlayCardAnywhere(Hand[i]))
                    {
                        return true;
                    }
                }
                foreach (BonusSlot slot in BonusHand)
                {
                    if (CanPlayCardAnywhere(slot.Card))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>Lands the staged mirror placement. Called from the turn resolver right after
        /// the main world's own placement, so the two land together.</summary>
        private IReadOnlyList<GridPos> ApplyStagedMirrorPlacement(TurnReport report)
        {
            if (!HasMirrorWorld || !mirrorStaged || stagedMirrorCard == null)
            {
                return NoCells;
            }
            BlockCard card = stagedMirrorCard;
            IReadOnlyList<GridPos> placed;
            if (Has(card, BlockElement.Negative))
            {
                // A negative block erases what it covers here too; it scores through the normal
                // mirror explosion path below rather than the main world's placement score.
                var targets = new List<GridPos>();
                foreach (GridPos offset in EffectiveShape(card).Cells)
                {
                    GridPos cell = stagedMirrorOrigin + offset;
                    if (MirrorBoard.GetCube(cell).HasValue)
                    {
                        targets.Add(cell);
                    }
                }
                foreach (GridPos cell in targets)
                {
                    MirrorBoard.DestroyCube(cell);
                }
                placed = NoCells;
            }
            else
            {
                placed = MirrorBoard.Place(card, EffectiveShape(card), stagedMirrorOrigin,
                    Has(card, BlockElement.Ghost));
            }
            RemoveFromMirrorHand(card);
            DisposeCard(card); // shared piles: a mirror card goes back to the same discard
            report.MirrorPlacedCells = placed;
            report.MirrorCard = card;
            ClearStagedMirrorPlay();
            return placed;
        }

        private void RemoveFromMirrorHand(BlockCard card)
        {
            for (int i = 0; i < MirrorHand.Count; i++)
            {
                if (MirrorHand[i].Id == card.Id)
                {
                    MirrorHand.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>Tops the mirror hand back up out of the shared deck. Unlike the main hand a
        /// short mirror hand is NOT a loss: the mirror simply plays with what it has, and a world
        /// with nothing playable sits the turn out.</summary>
        private void RefillMirrorHand()
        {
            if (!HasMirrorWorld)
            {
                return;
            }
            while (MirrorHand.Count < Rules.HandSize)
            {
                BlockCard card = Deck.DrawTop();
                if (card == null)
                {
                    if (Deck.DiscardCount == 0 || ThresholdPassed
                        || Rules.DrawOnlyAvailableNoReshuffle)
                    {
                        return;
                    }
                    Deck.ShuffleDiscardIntoDraw();
                    NoteDeckRecycled();
                    card = Deck.DrawTop();
                    if (card == null)
                    {
                        return;
                    }
                }
                MirrorHand.Add(card);
            }
        }

        /// <summary>
        /// Resolves the mirror world's own line explosions and sweep, and pays the cross-world
        /// column bonus. Runs inside the turn, right after the main world's explosion, so both
        /// worlds' clears belong to the same turn and the column match can be seen at all.
        /// </summary>
        private void ResolveMirrorExplosions(TurnReport report)
        {
            mirroredColumnsThisTurn.Clear();
            if (!HasMirrorWorld)
            {
                return;
            }
            MirrorBoard.SettleWaterAndReact();
            bool wasClean = MirrorBoard.IsCleanForSweep();
            LineExplosionResult lines = MirrorBoard.ResolveFullLines(Rules.RetroMode);
            report.MirrorExplodedRows = lines.Rows;
            report.MirrorExplodedColumns = lines.Columns;
            report.MirrorExplodedCells = lines.ExplodedCells;
            if (lines.LineCount > 0)
            {
                breakdown.BaseLines += PriceLines(
                    BuildLineScore(lines, lines.ExplodedCells.Count, false));
            }

            // Each world sweeps for itself (confirmed): emptying the mirror is its own sweep.
            if (lines.ExplodedCells.Count > 0 && !wasClean && MirrorBoard.IsCleanForSweep())
            {
                report.MirrorCleanSweep = true;
                CleanSweepCount++;
                breakdown.BaseSweep += PriceCleanSweep();
                if (session != null && !PowerRechargeBlocked)
                {
                    session.Powers.RechargeAll();
                }
            }

            PayMirroredColumns(report, lines);
        }

        /// <summary>The pay-off: a column that exploded in BOTH worlds on the same turn.</summary>
        private void PayMirroredColumns(TurnReport report, LineExplosionResult mirrorLines)
        {
            if (mirrorColumnBonus <= 0 || mirrorLines.Columns.Count == 0)
            {
                return;
            }
            IReadOnlyList<int> mainColumns = report.ExplodedColumns;
            for (int i = 0; i < mirrorLines.Columns.Count; i++)
            {
                int column = mirrorLines.Columns[i];
                for (int j = 0; j < mainColumns.Count; j++)
                {
                    if (mainColumns[j] == column)
                    {
                        mirroredColumnsThisTurn.Add(column);
                        break;
                    }
                }
            }
            if (mirroredColumnsThisTurn.Count > 0)
            {
                breakdown.BaseLines += mirrorColumnBonus * mirroredColumnsThisTurn.Count;
                report.MirroredColumns = mirroredColumnsThisTurn;
            }
        }

        private static readonly GridPos[] NoCells = new GridPos[0];
    }
}
