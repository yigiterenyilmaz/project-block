// PURPOSE: "Yer altı kaynakları"'s refuel, wired up - the one place the power bar's fill is
// reached from, by the game and by the animation lab alike.
//
// TWO THINGS LIVE HERE AND NOTHING ELSE. WHICH powers the seam fuelled, which is Core's answer
// and never re-derived (see SeamRefuelVisuals: a power going from spent to charged happens on a
// clean sweep, on a new round and under "Hazine" as well, all through the same Recharge, so a
// View watching the charge flag would draw the seam's animation on four events and be wrong on
// three); and WHEN the fill is allowed to start, which is after the bar has been refreshed,
// because the fill UNCOVERS a card the bar has already painted charged.
//
// The animation itself is PowerBarView.RefuelPower and what it draws is the joker's own report.
// Nothing here works out a capacity and nothing here decides whether a refuel happened.

using System.Collections.Generic;
using ProjectBlock.Core;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>
        /// The report last drawn, matched BY IDENTITY.
        ///
        /// Never by a serial: a serial restarts at 1 with every new joker while this controller
        /// outlives a run, so a new run's first refuel would be silently skipped whenever the run
        /// before it had pumped exactly as many times. Identity is right in all three cases that
        /// matter - a repaint hands back the same object, a new delivery writes a new one, and a
        /// loaded save has none at all.
        /// </summary>
        private SeamRefuelVisuals lastSeamPlayed;

        /// <summary>
        /// The seam's delivery, asked every repaint. MUST be called after powerBar.Refresh: the
        /// fill reveals the charged card from the bottom up, so the charged card has to be there
        /// underneath it first.
        /// </summary>
        private void SyncSeam()
        {
            if (session == null || session.Jokers == null || powerBar == null)
            {
                return;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var seam = owned[i] as YerAltiKaynaklariJoker;
                if (seam != null && seam.LastRefuel != null)
                {
                    PlaySeamRefuel(seam.LastRefuel);
                }
            }
        }

        /// <summary>The seam the game and the lab share. The lab fabricates only the REPORT.
        /// </summary>
        private void PlaySeamRefuel(SeamRefuelVisuals delivered)
        {
            if (delivered == null || ReferenceEquals(delivered, lastSeamPlayed)
                || powerBar == null)
            {
                return;
            }
            lastSeamPlayed = delivered;
            // Exhausted is the REPORT's answer, not a comparison done here: the seam's cost rules
            // are the joker's and the View owns no copy of them.
            powerBar.RefuelPowers(delivered.PowerInstanceIds, delivered.Exhausted);
        }

        private void StopSeamRefuels()
        {
            if (powerBar != null)
            {
                powerBar.StopRefuels();
            }
        }
    }
}
