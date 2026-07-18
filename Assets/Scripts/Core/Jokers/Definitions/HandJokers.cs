// PURPOSE: Jokers that act on the hand and the draw pile: Renovasyon, İade, Insider.
// The first two are the game's first PLAYER-ACTIVATED jokers - they spend a per-round
// charge and call an engine primitive; they consume no turn.
//
// CONFIRMED OVERTIME RULE (both redraw jokers): they stay usable after the threshold, but
// the engine does not recycle the discard for them there. Redrawing in overtime is
// therefore a deliberate gamble on the remaining draw pile - see RoundEngine.RedrawHand
// and RoundEngine.ReplaceHandCard.
//
// All numbers below are BALANCE PLACEHOLDERS.

namespace ProjectBlock.Core
{
    /// <summary>"Renovasyon" - discard the whole hand and draw a new one, twice per round.</summary>
    public sealed class RenovasyonJoker : Joker
    {
        public RenovasyonJoker()
            : base("renovasyon", "Renovasyon")
        {
            Description = "Raunt başına 2 kez tüm elini ıskartaya atıp yeni el çekersin. Tur harcamaz.";
            ChargesPerRound = 2;
            BaseSellValue = 30;
        }

        public override bool CanActivate(RoundContext ctx)
        {
            return ChargesLeft > 0 && ctx.Round.Status == RoundStatus.InProgress;
        }

        public override bool Activate(RoundContext ctx, ActivationTarget target)
        {
            if (!CanActivate(ctx) || !TrySpendCharge())
            {
                return false;
            }
            ctx.Round.RedrawHand();
            return true;
        }
    }

    /// <summary>"İade" - swap ONE held card for the top of the draw pile, twice per round.
    /// Only the returned card goes to the discard; the rest of the hand is untouched.</summary>
    public sealed class IadeJoker : Joker
    {
        public IadeJoker()
            : base("iade", "İade")
        {
            Description = "Raunt başına 2 kez elindeki tek bir bloğu iade edip yenisini çekersin. Tur harcamaz.";
            ChargesPerRound = 2;
            BaseSellValue = 30;
        }

        /// <summary>The player picks which held block to send back.</summary>
        public override JokerTargeting Targeting
        {
            get { return JokerTargeting.HandCard; }
        }

        public override bool CanActivate(RoundContext ctx)
        {
            return ChargesLeft > 0
                && ctx.Round.Status == RoundStatus.InProgress
                && ctx.Round.Hand.Count > 0;
        }

        public override bool Activate(RoundContext ctx, ActivationTarget target)
        {
            if (!CanActivate(ctx) || !target.HandIndex.HasValue)
            {
                return false;
            }
            int index = target.HandIndex.Value;
            if (index < 0 || index >= ctx.Round.Hand.Count)
            {
                return false;
            }
            if (!TrySpendCharge())
            {
                return false;
            }
            ctx.Round.ReplaceHandCard(index);
            return true;
        }
    }

    /// <summary>"Insider" - the top card of the draw pile is shown face-up. Pure information:
    /// it flips a UI flag and never touches the draw order, so the run stays deterministic.</summary>
    public sealed class InsiderJoker : Joker
    {
        public InsiderJoker()
            : base("insider", "Insider")
        {
            Description = "Çekme destesinin en üstündeki kartı görürsün.";
            BaseSellValue = 35;
        }

        public override void OnAcquired(SessionContext ctx)
        {
            ctx.Rules.RevealTopDrawCard = true;
        }

        public override void OnRemoved(SessionContext ctx)
        {
            ctx.Rules.RevealTopDrawCard = false;
        }

        /// <summary>Re-asserted every round: RoundRules is shared and another joker (or a
        /// future power) may have flipped it off in between.</summary>
        public override void OnRoundStarted(RoundContext ctx)
        {
            ctx.Rules.RevealTopDrawCard = true;
        }
    }
}
