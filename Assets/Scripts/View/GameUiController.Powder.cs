// PURPOSE: "Barut tedarikçisi" wired up - the one place PowderChargeView is reached from.
//
// ONE MOMENT, unlike the pickaxe's two: charging is not destruction, so there is nothing to hold
// proxies through and nothing to wait for. The charges are banked in AfterTurnScored, and the
// repaint that follows is where the embers are restated and the new sparks fire.
//
// THE SOUND GOES WITH THE SPARK and is BUDGETED: one sizzle per turn at the ripest block's pitch,
// not one per cube and not one per block. A four-cube block charging would otherwise fire four
// identical clips in the same frame, which is a buzz rather than a fuse - and with several blocks
// standing it becomes the loudest thing in the round. One is enough to say "the powder took", and
// the pitch says how ripe the ripest of them is.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private PowderChargeView powder;

        /// <summary>The last report actually played, matched BY IDENTITY - never by a serial. A
        /// repaint hands back the same object and must not re-fire; a new turn writes a new one;
        /// a loaded save has none. (The serial comparison is the bug recorded in CLAUDE.md: a
        /// serial restarts with every new joker while this view outlives a run.)</summary>
        private PowderVisuals lastPowderPlayed;

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
        /// Restates the powder on the board. Called from the repaint, so the embers follow the
        /// cubes that are actually standing: a block that went up this turn is simply not in the
        /// report any more and its mark goes with it.
        /// </summary>
        private void SyncPowder()
        {
            BarutTedarikcisiJoker joker = FindPowderJoker();
            if (joker == null || boardView == null)
            {
                if (powder != null)
                {
                    powder.Clear();
                }
                lastPowderPlayed = null;
                return;
            }
            PowderVisuals report = joker.LastCharge;
            EnsurePowder();
            powder.Show(report);
            if (report != null && !ReferenceEquals(report, lastPowderPlayed))
            {
                lastPowderPlayed = report;
                // A turn in which nothing actually took a charge is silent: every standing block
                // is reported every turn now (so its ember survives), and sounding for all of
                // them would sizzle once a turn forever once a block capped.
                if (report.AnyGained)
                {
                    PlayPowderSizzle(report);
                }
            }
            else if (report == null)
            {
                lastPowderPlayed = null;
            }
        }

        /// <summary>One sizzle per turn, pitched by the RIPEST block that charged - see the file
        /// header for why it is not one per cube.</summary>
        private void PlayPowderSizzle(PowderVisuals report)
        {
            if (sfx == null || report.Count == 0)
            {
                return;
            }
            float ripest = 0f;
            for (int i = 0; i < report.Count; i++)
            {
                if (report.Gained[i] && report.Fullness[i] > ripest)
                {
                    ripest = report.Fullness[i];
                }
            }
            sfx.Fuse(ripest);
        }

        private void EnsurePowder()
        {
            if (powder != null)
            {
                return;
            }
            var go = new GameObject("PowderCharge");
            // UNDER THE BOARD's transform, not the controller's: the arena is scaled and moved
            // (overtime pressure, the quake's tremor) and a mark drawn in the controller's space
            // would sit still while the block under it moved.
            go.transform.SetParent(boardView.transform, false);
            powder = go.AddComponent<PowderChargeView>();
            powder.Build(boardView);
        }
    }
}
