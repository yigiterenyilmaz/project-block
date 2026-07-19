// PURPOSE: The two jokers that trade safety for power: Seri tetik (a bigger hand that
// churns itself every turn) and Batak (an optional bet that can lose the run outright).
//
// Batak is the first joker that can END a round, through RoundEngine.DeclareLoss. It obeys
// the standing rule that a pending advance offer outranks a same-turn loss, so a player who
// earns an offer on the turn their bet expires can still escape by advancing.
//
// All numbers are BALANCE PLACEHOLDERS.

using System;

namespace ProjectBlock.Core
{
    /// <summary>"Seri tetik" - hand size +2, but every turn ends by throwing the unused hand
    /// away and drawing a fresh one. Off in overtime, where the discard is not recycled and
    /// churning the hand would just mill the player to death.</summary>
    public sealed class SeriTetikJoker : Joker
    {
        public int ExtraHandSize = 2;

        private bool applied;

        public SeriTetikJoker()
            : base("seri_tetik", "Seri Tetik")
        {
            SetDescription(
                "Hand size +2, but at the end of every turn the unused cards are discarded "
                    + "and replaced. Disabled in overtime.",
                "El boyutu +2, ama her tur sonunda kullanılmayan kartlar ıskartaya "
                    + "gidip yenileri gelir. Uzatmada çalışmaz.");
            BaseSellValue = 55;
        }

        public override bool DisabledInOvertime
        {
            get { return true; }
        }

        public override string StatusText
        {
            get
            {
                return applied
                    ? Loc.Pick("hand +" + ExtraHandSize, "el +" + ExtraHandSize)
                    : Loc.Pick("off", "kapalı");
            }
        }

        public override void OnAcquired(SessionContext ctx)
        {
            Apply(ctx.Rules);
        }

        public override void OnRemoved(SessionContext ctx)
        {
            Revert(ctx.Rules);
        }

        /// <summary>Safety re-assert (a no-op when already applied). The real re-arm happens
        /// in OnRoundEnded, because the engine fills the opening hand in its constructor -
        /// before this runs - so the bonus has to be back on the shared rules by then.</summary>
        public override void OnRoundStarted(RoundContext ctx)
        {
            Apply(ctx.Rules);
        }

        /// <summary>Overtime reverts the bonus mid-round; this puts it back for the NEXT
        /// round, before that round's engine is built and deals its first hand.</summary>
        public override void OnRoundEnded(RoundContext ctx, RoundOutcome outcome)
        {
            Apply(ctx.Rules);
        }

        /// <summary>Overtime turns off the hand-size bonus centrally. This fires exactly at
        /// the threshold crossing (before the joker's own hooks go silent), so the +2 never
        /// leaks into overtime.</summary>
        public override void OnOvertimeStarted(RoundContext ctx)
        {
            Revert(ctx.Rules);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            // A bonus-hand play never touched the hand, so there is nothing to churn.
            if (turn.Report.PlayedFromBonusHand)
            {
                return;
            }
            turn.Round.CycleHandWithoutReshuffle();
        }

        private void Apply(RoundRules rules)
        {
            if (applied)
            {
                return;
            }
            rules.HandSize += ExtraHandSize;
            applied = true;
        }

        private void Revert(RoundRules rules)
        {
            if (!applied)
            {
                return;
            }
            rules.HandSize -= ExtraHandSize;
            applied = false;
            // The engine never trims an oversized hand on purpose: the extra cards stay
            // playable and the hand shrinks naturally as they are used.
        }
    }

}
