// PURPOSE: What "Harcama bonusu" paid this turn, written down for the VIEW. Reporting only:
// the payout itself is unchanged.
//
// IT EXISTS FOR TWO REASONS, and the second is the one that matters.
//
// THE AMOUNT IS CORE'S. PointsPerEmptyDrawPile is a balance placeholder and will move; a View
// that draws "+60" is a View that will one day be lying about the score the player just got.
// The number on the receipt is this report's Payout and nothing else.
//
// AND THE GRANULARITY IS CORE'S. The joker does not count how many times the pile ran dry - it
// reads a BOOL (TurnReport.DrawPileEmptiedThisTurn) and pays ONCE for the turn. A turn where the
// pile emptied twice pays once, so it must ANIMATE once. A View that hung its animation off "the
// draw pile just emptied" would play it twice and promise a payment that never arrives. So the
// report is written exactly where the payment is made, and carries a SERIAL: one turn, one
// serial, one receipt.
//
// THE TONE IS ALSO A FACT HERE. This joker pays for the same event that eats the arena
// (RoundEngine.NoteDeckRecycled) and that, past the threshold, is the LOSS condition. So the
// report carries ThresholdPassed - not so the View can cancel anything, but so it can play the
// same payout without celebrating. The joker's own comment calls it a consolation, not a rescue,
// and the picture has to agree.

namespace ProjectBlock.Core
{
    /// <summary>
    /// One turn's cashback. A new SERIAL every time, so a repaint during the animation cannot
    /// restart it - and a turn that paid nothing writes nothing.
    /// </summary>
    public sealed class RebateVisuals
    {
        public int Serial;

        /// <summary>What was actually added to the turn's score. Never re-derived in the View.
        /// </summary>
        public int Payout;

        /// <summary>How many times it has paid this ROUND, counting this one. The receipt does
        /// not print it - it is there so the lab and the debug overlay can show what the rules
        /// think, beside what the picture is doing.</summary>
        public int TimesThisRound;

        /// <summary>True when the round is already past its bar. The same event that paid this
        /// is the loss condition there, so the payout plays without a victory note.</summary>
        public bool ThresholdPassed;
    }
}
