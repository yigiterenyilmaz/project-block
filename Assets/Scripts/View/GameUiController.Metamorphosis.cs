// PURPOSE: "Metamorfoz" wired up - the one place MetamorphosisView is reached from.
//
// ONE MOMENT, like the powder's: ripening is not destruction, so there is nothing to hold proxies
// through. The clock is advanced in AfterTurnScored and the repaint that follows restates the
// marks and blooms whatever turned.
//
// The joker's card flashes through the ordinary proc channel (it calls NoteProc), so nothing here
// has to arrange that - which is the point of that channel existing.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private MetamorphosisView metamorphosis;

        /// <summary>The last report played, matched BY IDENTITY - never by a serial. A repaint
        /// hands back the same object and must not re-fire.</summary>
        private MetamorphosisVisuals lastMetamorphosisPlayed;

        private MetamorfozJoker FindMetamorfoz()
        {
            if (session == null || session.Jokers == null)
            {
                return null;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var found = owned[i] as MetamorfozJoker;
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private void SyncMetamorphosis()
        {
            MetamorfozJoker joker = FindMetamorfoz();
            if (joker == null || boardView == null)
            {
                if (metamorphosis != null)
                {
                    metamorphosis.Clear();
                }
                lastMetamorphosisPlayed = null;
                return;
            }
            MetamorphosisVisuals report = joker.LastChange;
            EnsureMetamorphosis();
            metamorphosis.Show(report);
            if (report != null && !ReferenceEquals(report, lastMetamorphosisPlayed))
            {
                lastMetamorphosisPlayed = report;
                // The change has a sound only when something actually changed: the marks are
                // restated every turn and a chime for the standing clock would ring forever.
                if (report.Turned.Count > 0)
                {
                    sfx.Buy();
                }
            }
            else if (report == null)
            {
                lastMetamorphosisPlayed = null;
            }
        }

        private void EnsureMetamorphosis()
        {
            if (metamorphosis != null)
            {
                return;
            }
            var go = new GameObject("Metamorphosis");
            // UNDER THE BOARD's transform: the arena is scaled and moved (overtime pressure, a
            // quake) and a mark in the controller's space would sit still while its cube moved.
            go.transform.SetParent(boardView.transform, false);
            metamorphosis = go.AddComponent<MetamorphosisView>();
            metamorphosis.Build(boardView);
        }
    }
}
