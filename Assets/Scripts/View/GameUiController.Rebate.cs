// PURPOSE: "Harcama bonusu"'s cashback, wired up - the one place the payout animation is reached
// from, by the game and by the animation lab alike.
//
// THREE THINGS LIVE HERE AND NOTHING ELSE. WHERE the money comes FROM (the real draw pile's own
// position, asked of CardLayerView, never a constant); WHERE it is going (the real score line in
// the HUD, converted to world space - the same anchor Midas uses, and for the same reason: a
// written-down corner is a payout that flies off the edge of a phone); and HOW the score answers
// when two different effects are both warming it.
//
// The animation itself is RebateView and what it draws is RebateVisuals - the joker's own report.
// Nothing here works out an amount and nothing here decides whether it happened.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private RebateView rebate;

        /// <summary>True while the pile is being drawn as spent for a payout, so it is given back
        /// exactly once however the animation ends.</summary>
        private bool rebateShowingEmpty;

        /// <summary>
        /// The joker's cashback, asked every repaint and keyed on the report's SERIAL.
        ///
        /// The serial is doing real work here, more than anywhere else in the game: the rules pay
        /// ONCE for a turn in which the draw pile ran dry, however many times it actually dried.
        /// A View that watched TurnReport.DrawPileEmptiedThisTurn would be watching the right
        /// fact and still be wrong, because it would have no way to know the payment had already
        /// been made. So it watches the PAYMENT.
        /// </summary>
        private void SyncRebate(RoundEngine round)
        {
            if (session == null || session.Jokers == null || cardLayer == null)
            {
                return;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var bonus = owned[i] as HarcamaBonusuJoker;
                if (bonus != null && bonus.LastRebate != null)
                {
                    PlayRebate(bonus.LastRebate);
                }
            }
        }

        /// <summary>The seam the game and the lab share. The lab fabricates only the REPORT.
        /// </summary>
        private void PlayRebate(RebateVisuals paid)
        {
            if (paid == null)
            {
                return;
            }
            EnsureRebate();
            bool fresh = !rebate.Busy;
            rebate.Play(paid, CardLayerView.DrawPilePos, ScoreWorldAnchor(),
                CardLayerView.PileWorldWidth);
            // THE PILE'S OWN BEAT belongs to the card layer, which owns that transform - see
            // CardLayerView.PlayDrawPileEmptyBeat. Asked for only when the payout actually
            // started, so a repaint during the animation cannot make the slot twitch again.
            if (fresh && rebate.Busy && RebateView.Layers.ShowEmptyBeat)
            {
                cardLayer.PlayDrawPileEmptyBeat();
                // AND THE PILE IS SHOWN SPENT for as long as the receipt is out. This is the
                // CAUSE the whole effect is the consequence of: a payout over a full-looking
                // stack of twenty-odd cards states no cause at all, and its count text lands in
                // the same place as the value. Presentation only - the recycle behind it runs on
                // its own schedule and is never waited for.
                cardLayer.SetDrawPileShownEmpty(true);
                rebateShowingEmpty = true;
            }
        }

        private void EnsureRebate()
        {
            if (rebate == null)
            {
                var go = new GameObject("RebatePayout");
                go.transform.SetParent(transform, false);
                rebate = go.AddComponent<RebateView>();
            }
        }

        private void StopRebate()
        {
            if (rebate != null)
            {
                rebate.Stop();
            }
            ReleaseRebatePile();
        }

        /// <summary>
        /// Gives the pile back the moment the RECEIPT is gone - which is well before the token
        /// lands, on purpose: by then the cashback has left the pile entirely and the deck has
        /// usually been recycled, so holding the slot empty through the flight would be showing
        /// the player a lie about their deck for half a second.
        /// </summary>
        private void TickRebatePile()
        {
            if (!rebateShowingEmpty || rebate == null)
            {
                return;
            }
            if (!rebate.Busy || rebate.ReceiptGone)
            {
                ReleaseRebatePile();
            }
        }

        private void ReleaseRebatePile()
        {
            if (rebateShowingEmpty)
            {
                rebateShowingEmpty = false;
                if (cardLayer != null)
                {
                    cardLayer.SetDrawPileShownEmpty(false);
                }
            }
        }

        /// <summary>
        /// THE SCORE LINE'S ANSWER, and why it is one method.
        ///
        /// Two effects can warm the same label - Midas's essence landing and this cashback - and
        /// each one used to write the scale and colour itself. Whichever ticked second won, and
        /// the one that had finished would helpfully reset the label to normal WHILE the other
        /// was still mid-punch. So the warmth is a number each view publishes and this is the
        /// single place the HUD is written: the strongest claim wins, and the label is only put
        /// back when nothing is warm at all.
        /// </summary>
        private void TickScoreResponse()
        {
            if (totalText == null)
            {
                return;
            }
            TickRebatePile();
            float midasWarm = midasPayout != null ? midasPayout.ScoreWarm : 0f;
            float rebateWarm = rebate != null ? rebate.ScoreWarm : 0f;
            if (!midasScoreRead)
            {
                midasScoreInk = totalText.color;
                midasScoreRead = true;
            }
            float warm = Mathf.Max(midasWarm, rebateWarm);
            // "Hazine" writes its own shape (a gain punches, an inverted loss dips and goes red),
            // and takes the label only while its claim is the strongest.
            float hazineClaim = hazine != null ? hazine.ScoreClaim : 0f;
            if (hazineClaim > warm && hazineClaim > 0.001f)
            {
                float hs = hazine.ScoreScale;
                totalText.rectTransform.localScale = new Vector3(hs, hs, 1f);
                totalText.color = Color.Lerp(midasScoreInk, hazine.ScoreInk, hazineClaim * 0.8f);
                return;
            }
            if (warm <= 0.001f)
            {
                totalText.rectTransform.localScale = Vector3.one;
                totalText.color = midasScoreInk;
                return;
            }
            // Each effect keeps its own punch depth and its own warmth colour; the cashback's is
            // deliberately the smaller of the two, because the receipt already showed the number
            // and a second celebration of the same points is a duplicate reward.
            bool midasLeads = midasWarm >= rebateWarm;
            float punch = 1f + (midasLeads
                ? MidasPayoutView.Style.Punch
                : RebateView.Style.ScorePunch) * warm;
            Color gold = midasLeads ? MidasPayoutView.Style.Gold : RebateView.Style.Amber;
            totalText.rectTransform.localScale = new Vector3(punch, punch, 1f);
            totalText.color = Color.Lerp(midasScoreInk, gold, warm * 0.8f);
        }
    }
}
