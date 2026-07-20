// PURPOSE: RoundEngine scoring & round-flow helpers - card disposal, between-turn and
// overtime score, dead-zone-aware line scoring, retro/Tetris collapse, top-out, and
// the loss/force-advance/force-sweep entry points.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
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
            // Between turns there is no breakdown to scale, so lift the logical amount into the
            // scaled economy here before it touches RoundScore and the run currency.
            int scaled = amount * scorer.ScoreScale;
            RoundScore += scaled;
            if (session != null)
            {
                session.AddCurrency(scaled);
            }
            if (!ThresholdPassed && RoundScore >= ScaledThreshold)
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

        /// <summary>"Pull the earned score to the threshold": the round's contribution to the
        /// run-wide TotalScore is capped at the threshold and the local meter drops to it.
        /// Used by "İkinci şans" and "Totem" in overtime, where the score is above threshold.</summary>
        internal void CapRoundScoreAtThreshold()
        {
            int excess = RoundScore - ScaledThreshold;
            if (excess <= 0)
            {
                return;
            }
            RoundScore = ScaledThreshold;
            if (session != null)
            {
                // excess is already in scaled units (RoundScore is scaled), so no extra scale.
                session.AddCurrency(-excess); // remove the overtime-farmed excess from the run
            }
        }

        /// <summary>Lowest y that is part of the retro dead zone (the top DeadZoneRows rows), or
        /// int.MaxValue when there is no dead zone. Rows at or above this are the overflow zone.</summary>
        private int DeadZoneFloor
        {
            get
            {
                return Rules.DeadZoneRows > 0
                    ? Board.MinY + Board.Height - Rules.DeadZoneRows
                    : int.MaxValue;
            }
        }

        private bool IsDeadRow(int y)
        {
            return y >= DeadZoneFloor;
        }

        /// <summary>Score for a line explosion, with the retro dead zone excluded: rows and cubes
        /// in the top overflow zone earn nothing (a clear up there is survival, not points). Plain
        /// scoring when there is no dead zone.</summary>
        private int ScoreLineExplosionScored(LineExplosionResult explosion, int cubesExploded)
        {
            int scoredLines = explosion.LineCount;
            int scoredCubes = cubesExploded;
            if (Rules.DeadZoneRows > 0)
            {
                int deadRows = 0;
                for (int i = 0; i < explosion.Rows.Count; i++)
                {
                    if (IsDeadRow(explosion.Rows[i]))
                    {
                        deadRows++;
                    }
                }
                int deadCubes = 0;
                for (int i = 0; i < explosion.ExplodedCells.Count; i++)
                {
                    if (IsDeadRow(explosion.ExplodedCells[i].Y))
                    {
                        deadCubes++;
                    }
                }
                scoredLines = System.Math.Max(0, explosion.LineCount - deadRows);
                scoredCubes = System.Math.Max(0, cubesExploded - deadCubes);
            }
            return scorer.ScoreLineExplosion(scoredLines, scoredCubes);
        }

        /// <summary>Retro/Tetris gravity, run after a placement's full rows have cleared: the rows
        /// ABOVE each cleared row drop straight down (classic Tetris - a locked block never falls
        /// into a gap, only whole rows collapse and everything resting above rides down together).
        /// Re-bases the destruction snapshot so the moved cubes are not mistaken for destroyed ones,
        /// exactly like a water settle. No cascade: a row-collapse cannot complete a new line, so
        /// nothing re-explodes and no extra score is granted. Only called in retro mode.</summary>
        private void CollapseRetroLines(IReadOnlyList<int> clearedRows)
        {
            if (Board.CollapseClearedRows(clearedRows))
            {
                ResyncSnapshot(); // cubes moved, nothing died - re-baseline the destruction diff
            }
        }

        /// <summary>True if any cube sits in the retro dead zone. The retro toggle reads this - it
        /// refuses to turn off while the dead zone still holds cubes.</summary>
        public bool DeadZoneOccupied
        {
            get
            {
                if (Rules.DeadZoneRows <= 0)
                {
                    return false;
                }
                int top = Board.MinY + Board.Height - 1;
                for (int y = DeadZoneFloor; y <= top; y++)
                {
                    for (int x = Board.MinX; x <= Board.MinX + Board.Width - 1; x++)
                    {
                        if (Board.GetCube(new GridPos(x, y)).HasValue)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        /// <summary>Retro top-out: any cube reached the very top row, so a new piece can no longer
        /// drop in from above (even if lower rows still have gaps) - the Tetris loss. False when
        /// there is no dead zone (i.e. not in retro).</summary>
        private bool IsToppedOut()
        {
            if (Rules.DeadZoneRows <= 0)
            {
                return false;
            }
            int topY = Board.MinY + Board.Height - 1;
            for (int x = Board.MinX; x <= Board.MinX + Board.Width - 1; x++)
            {
                if (Board.GetCube(new GridPos(x, topY)).HasValue)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Clears every destructible cube WITHOUT scoring or triggering a sweep
        /// ("İkinci şans"). Indestructible cubes (obsidian/gold/Parazit host) stay.</summary>
        internal void ClearBoardScoreless()
        {
            var cells = new List<GridPos>(Board.GetOccupiedCells());
            DestroyCubes(cells, false);
        }

        /// <summary>Ends the round straight to the market ("Totem"), with no advance offer.</summary>
        internal void ForceAdvanceToMarket()
        {
            SetStatus(RoundStatus.Advanced);
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
    }
}
