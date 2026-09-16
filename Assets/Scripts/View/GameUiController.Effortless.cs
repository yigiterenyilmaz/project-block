// PURPOSE: "Eforsuz galibiyet" being paid - the one place ConfettiRainView is reached from.
//
// IT FIRES ON THE PHASE CHANGE, not on a turn. The joker pays in OnMarketEntered, because a round
// is only truly finished once you are standing in the shop; there is no TurnReport at that moment
// and no board on screen, so none of the turn-feedback seams apply. The phase change into Market
// is the moment, and it is watched rather than called from the round flow - the same bargain the
// boss looks make.
//
// MATCHED ON THE PAYOUT, NOT ON THE PHASE. Entering the market is not the event - being PAID for
// entering it is, and a joker that forfeited its bonus by using a power enters exactly the same
// market. So the trigger is LastPaid having a value, and it is disarmed by the next round starting
// (OnMarketEntered zeroes it), which is what stops a repaint or a re-entered shop raining twice.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private ConfettiRainView confetti;

        /// <summary>The payout already celebrated, so a rebuild of the market HUD does not rain
        /// again. Reset when there is nothing to celebrate, which is every round start.</summary>
        private int confettiPaidFor;

        private EforsuzGalibiyetJoker FindEffortless()
        {
            if (session == null || session.Jokers == null)
            {
                return null;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var found = owned[i] as EforsuzGalibiyetJoker;
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>
        /// Rains if the effortless bonus was just paid. Called when the market is shown; cheap and
        /// idempotent, so it does not matter how many times the shop is rebuilt.
        /// </summary>
        private void CheckEffortlessPayout()
        {
            EforsuzGalibiyetJoker joker = FindEffortless();
            if (joker == null || session == null || session.Phase != GamePhase.Market)
            {
                return;
            }
            if (joker.LastPaid <= 0)
            {
                // Nothing paid this market - forfeited, or the joker was just bought. Disarm, so
                // the next real payout is seen as new even if it is the same number.
                confettiPaidFor = 0;
                return;
            }
            if (confettiPaidFor == joker.LastPaid)
            {
                return;
            }
            confettiPaidFor = joker.LastPaid;
            EnsureConfetti();
            // The seed is the payout and the run's round, so the same celebration is the same
            // every time it is replayed - nothing here uses UnityEngine.Random.
            confetti.Play(joker.LastPaidWasOvertime,
                joker.LastPaid * 31 + session.RoundNumber);
            sfx.Buy();
            jokerBar.ProcJoker(joker.InstanceId);
            long shown = (long)joker.LastPaid * session.Config.Scoring.ScoreScale;
            FloatingTextFx.Spawn(transform, new Vector2(0f, 1.6f),
                (joker.LastPaidWasOvertime
                    ? Loc.Pick("EFFORTLESS  -  OVERTIME!", "EFORSUZ  -  UZATMA!")
                    : Loc.Pick("EFFORTLESS!", "EFORSUZ GALİBİYET!"))
                    + "  +" + shown,
                new Color(1f, 0.86f, 0.42f), 60, 0.09f);
        }

        private void EnsureConfetti()
        {
            if (confetti != null)
            {
                return;
            }
            var go = new GameObject("ConfettiRain");
            // The CONTROLLER's transform, not the board's: this falls over the whole screen and
            // the arena is not even on it.
            go.transform.SetParent(transform, false);
            confetti = go.AddComponent<ConfettiRainView>();
            confetti.Build(cam);
        }

        /// <summary>Takes the rain down - leaving the market, a reset, a new run.</summary>
        private void ClearConfetti()
        {
            if (confetti != null)
            {
                confetti.Clear();
            }
            confettiPaidFor = 0;
        }
    }
}
