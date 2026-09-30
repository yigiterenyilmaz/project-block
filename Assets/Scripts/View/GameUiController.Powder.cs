// PURPOSE: "Barut tedarikçisi" wired up - the one place PowderMagazineView is reached from.
//
// TWO MOMENTS, the pickaxe's way:
//   THE REPAINT   (SyncPowder) - the turn's charges are restated as one magazine per block, and a
//                 block that just took powder plays its load cycle. If the powder also PAID this
//                 turn, the blocks that went up are raised again as copies of their packs FIRST
//                 (Prepare): the repaint has already emptied their cells, and without the copies the
//                 cook-off would have nothing to fire.
//   THE EXPLOSION (PlayPowderPayout, from PlayExplosionFeedback) - each block cooks off when the
//                 turn's line reaches its first cube (the same line-front / cluster timing Hazine
//                 reveals on), and blows.
//
// THE SOUND IS BUDGETED: one fuse per turn at the ripest block's pitch rather than one per block -
// the load cycle's own ChargeLoad cue is heard once a turn. The magazine announces six moments
// (PowderMagazineView.Cue); the ones with no sound of their own yet are left silent on purpose and
// are the hooks a dedicated clip goes on.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private PowderMagazineView magazine;

        /// <summary>The last reports actually played, matched BY IDENTITY - never by a serial. A
        /// repaint hands back the same object and must not re-fire; a new turn writes a new one;
        /// a loaded save has none. (The serial comparison is the bug recorded in CLAUDE.md: a
        /// serial restarts with every new joker while this view outlives a run.)</summary>
        private PowderVisuals lastPowderPlayed;

        private PowderPayoutVisuals lastPowderPrepared;

        private PowderPayoutVisuals lastPowderPayout;

        /// <summary>So a turn's many ChargeLoad cues make one fuse.</summary>
        private int powderFuseFrame = -1;

        private BarutTedarikcisiJoker FindPowderJoker()
        {
            if (session == null || session.Jokers == null)
            {
                return null;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var found = owned[i] as BarutTedarikcisiJoker;
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>
        /// Restates the powder on the board. Called from the repaint, so the packs follow the cubes
        /// that are actually standing: a block that went up this turn is simply not in the report
        /// any more - and if it paid, it is handed to its cook-off copy on this same frame.
        /// </summary>
        private void SyncPowder()
        {
            BarutTedarikcisiJoker joker = FindPowderJoker();
            if (joker == null || boardView == null)
            {
                if (magazine != null)
                {
                    magazine.Clear();
                }
                lastPowderPlayed = null;
                return;
            }
            EnsureMagazine();
            magazine.PlaybackRate = 1f;
            PowderPayoutVisuals payout = joker.LastPayout;
            if (payout != null && !ReferenceEquals(payout, lastPowderPrepared))
            {
                lastPowderPrepared = payout;
                magazine.Prepare(payout);
            }
            PowderVisuals report = joker.LastCharge;
            bool fresh = report != null && !ReferenceEquals(report, lastPowderPlayed);
            magazine.Show(report, fresh);
            if (fresh)
            {
                lastPowderPlayed = report;
            }
            else if (report == null)
            {
                lastPowderPlayed = null;
            }
        }

        /// <summary>
        /// The powder going up, from PlayExplosionFeedback - the moment the turn's cubes break,
        /// not the repaint before it. Each block cooks off when the line reaches its first cube.
        /// </summary>
        private void PlayPowderPayout(RoundEngine round, TurnReport report)
        {
            BarutTedarikcisiJoker joker = FindPowderJoker();
            PowderPayoutVisuals payout = joker != null ? joker.LastPayout : null;
            if (payout == null || ReferenceEquals(payout, lastPowderPayout) || boardView == null)
            {
                return;
            }
            lastPowderPayout = payout;
            EnsureMagazine();
            if (!ReferenceEquals(payout, lastPowderPrepared))
            {
                // No repaint came between the payout and its explosion: prepare it now.
                lastPowderPrepared = payout;
                magazine.PlaybackRate = 1f;
                magazine.Prepare(payout);
            }
            magazine.Begin(payout, PowderBreakDelays(round, report, payout));
        }

        /// <summary>When each payout cube breaks on screen - the line front, or the cluster burst.
        /// </summary>
        private static List<float> PowderBreakDelays(RoundEngine round, TurnReport report,
            PowderPayoutVisuals payout)
        {
            GameBoard board = round != null ? round.Board : null;
            var delays = new List<float>();
            for (int i = 0; i < payout.Count; i++)
            {
                delays.Add(HazineBreakDelay(board, report != null ? report.ExplodedRows : null,
                    report != null ? report.ExplodedColumns : null, payout.Cells[i]));
            }
            return delays;
        }

        private void EnsureMagazine()
        {
            if (magazine != null)
            {
                return;
            }
            var go = new GameObject("PowderMagazine");
            // UNDER THE BOARD's transform: the arena is scaled and moved (overtime pressure, the
            // quake's tremor, a knock) and a pack drawn in the controller's space would sit still
            // while the block under it moved.
            go.transform.SetParent(boardView.transform, false);
            magazine = go.AddComponent<PowderMagazineView>();
            magazine.Build(boardView);
            magazine.ScoreAnchor = ScoreWorldAnchor;
            magazine.Sounded = OnPowderCue;
        }

        /// <summary>The magazine's six moments, onto the sounds the game has. A charge is the old
        /// fuse, quiet and pitched by fullness - once a turn; the cap is the fuse at its highest;
        /// the blast is the explosion, a touch heavier the more powder it held. The ignition, the
        /// cook-off's build and the reward have no clip yet and stay silent.</summary>
        private void OnPowderCue(PowderMagazineView.Cue cue, float fullness)
        {
            if (sfx == null)
            {
                return;
            }
            switch (cue)
            {
                case PowderMagazineView.Cue.ChargeLoad:
                    if (powderFuseFrame != Time.frameCount)
                    {
                        powderFuseFrame = Time.frameCount;
                        sfx.Fuse(fullness);
                    }
                    break;
                case PowderMagazineView.Cue.Maxed:
                    sfx.Fuse(1f);
                    break;
                case PowderMagazineView.Cue.Detonation:
                    sfx.Explode(fullness >= 0.99f ? 3 : fullness >= 0.6f ? 2 : 1, 0);
                    break;
            }
        }
    }
}
