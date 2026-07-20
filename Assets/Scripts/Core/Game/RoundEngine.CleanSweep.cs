// PURPOSE: RoundEngine clean sweep - THE single central clean-sweep event (at most
// once per turn, only when this turn's destruction emptied a non-empty board) and
// the between-turn external destruction path feeding it.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
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
            if (currentReport == null)
            {
                // Between turns: only "Genel temizlik" makes a power/joker board-clear count.
                // A slimmer sweep than the in-turn one - it counts, pays the bonus and
                // recharges every power, but does not run the per-turn joker hooks.
                if (forced || !Rules.CountExternalSweeps || !externalClearReady
                    || !Board.IsCleanForSweep())
                {
                    return false;
                }
                externalClearReady = false;
                CleanSweepCount++;
                AddScoreOutsideTurn(scorer.ScoreCleanSweep());
                if (session != null)
                {
                    session.Powers.RechargeAll();
                }
                return true;
            }
            if (sweepResolvedThisTurn)
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

                // Winning this overtime pays an escalating bonus scaled to the threshold
                // (ContinueCount is the 1-based overtime level). It goes through the score
                // pipeline - BaseOvertimeBonus before finalization, a late add after - so joker
                // multipliers raise it just like any other score ("point upgrades" apply).
                int winBonus = scorer.ScoreOvertimeWinBonus(Config.ScoreThreshold, ContinueCount);
                if (winBonus > 0)
                {
                    if (scoreFinalized)
                    {
                        AddLateTurnScore(winBonus, "base.overtime");
                    }
                    else
                    {
                        breakdown.BaseOvertimeBonus += winBonus;
                    }
                    currentReport.OvertimeWinBonus += winBonus;
                }
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
        /// <summary>Clears the between-turn destruction log so the next power use captures only
        /// its own destroyed cells. The View calls this right before running a power, then reads
        /// ExternalDestructionLog to blast whatever the power destroyed (board powers destroy
        /// board-dependent cells that PreviewCells cannot predict).</summary>
        public void BeginExternalCapture()
        {
            externalDestructionLog.Clear();
        }

        /// <summary>Cells destroyed by between-turn effects since the last BeginExternalCapture
        /// (see the field). Read by the View for the explosion FX; empty otherwise.</summary>
        public IReadOnlyList<GridPos> ExternalDestructionLog
        {
            get { return externalDestructionLog; }
        }

        internal IReadOnlyList<GridPos> DestroyCubes(IEnumerable<GridPos> cells, bool countsForSweep)
        {
            return DestroyCubes(cells, countsForSweep, false);
        }

        /// <summary>As above; forced ignores cube-kind indestructibility ("elmas kazma").</summary>
        internal IReadOnlyList<GridPos> DestroyCubes(IEnumerable<GridPos> cells, bool countsForSweep,
            bool forced)
        {
            bool wasClean = Board.IsCleanForSweep();
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
            // A between-turn destruction never lands in a TurnReport (LogDestruction no-ops with
            // no currentReport), so remember the cells for the View to blast.
            if (currentReport == null)
            {
                externalDestructionLog.AddRange(destroyed);
            }
            // A between-turn destruction that emptied a non-empty board is a candidate for a
            // "Genel temizlik" sweep (only sweep-counting destructions qualify).
            if (currentReport == null && countsForSweep && destroyed.Count > 0
                && !wasClean && Board.IsCleanForSweep())
            {
                externalClearReady = true;
            }
            return destroyed;
        }
    }
}
