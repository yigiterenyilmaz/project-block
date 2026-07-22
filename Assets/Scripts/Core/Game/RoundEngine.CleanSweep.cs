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
            return ResolveCleanSweep(false, true);
        }

        /// <summary>The player's own placement line-clear sweep. Unlike a joker/power sweep it
        /// always "counts" - it pays the sweep bonus and recharges powers regardless of
        /// "Genel temizlik". Called only from ResolvePlacement step 3.</summary>
        internal bool ResolvePlacementSweep()
        {
            return ResolveCleanSweep(false, false);
        }

        /// <param name="external">True for a sweep triggered by a joker or power rather than by
        /// the player's placement. An external sweep still clears the board and plays the sweep
        /// FX, but pays no bonus and recharges no power unless "Genel temizlik" is held. Kayıt
        /// defteri passes false: its counter is the player's sweep once the natural one is off.</param>
        private bool ResolveCleanSweep(bool forced, bool external)
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

            // Does this sweep "count"? The player's placement clear and Kayıt defteri's counter
            // always do (external == false); a joker/power-triggered sweep only counts while
            // "Genel temizlik" is held. An uncounted sweep still clears the board and plays the
            // sweep FX - it just pays no bonus, no overtime win bonus, and recharges no power.
            bool counts = !external || Rules.CountExternalSweeps;

            if (counts)
            {
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
            }

            if (ThresholdPassed)
            {
                // Overtime reward: a fresh draw pile and a new chance to leave.
                Deck.ShuffleDiscardIntoDraw();
                pendingAdvanceOffer = true;

                // Winning this overtime pays an escalating bonus scaled to the threshold
                // (ContinueCount is the 1-based overtime level). It goes through the score
                // pipeline - BaseOvertimeBonus before finalization, a late add after - so joker
                // multipliers raise it just like any other score ("point upgrades" apply). An
                // uncounted external sweep pays none of it.
                int winBonus = counts
                    ? scorer.ScoreOvertimeWinBonus(Config.ScoreThreshold, ContinueCount)
                    : 0;
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
                // The sweep is the powers' economy: a counting sweep puts every charge back.
                // An uncounted (external, no "Genel temizlik") sweep still runs the per-power
                // hooks (Olta, Batak) but grants no recharge.
                session.Powers.DispatchCleanSweep(currentTurn, counts);
            }
            return true;
        }

        /// <summary>Destroys cubes outside the normal line explosion (joker/power effects).
        /// Scoreless by itself - the caller decides whether to award points. Feeds the
        /// central sweep pre-condition, so a joker can empty the board and TryResolveCleanSweep
        /// will accept it. EXTENSION POINT for Robot supurge, Deprem, Enfeksiyon.</summary>
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
