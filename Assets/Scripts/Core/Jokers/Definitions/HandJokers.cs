// PURPOSE: Jokers that act on the hand and the draw pile: Renovasyon, İade, Insider.
// The first two are the game's first PLAYER-ACTIVATED jokers - they spend a per-round
// charge and call an engine primitive; they consume no turn.
//
// OVERTIME, AND WHY THE TWO DIFFER (open design question, see docs/jokers-plan.md):
//  - Renovasyon is OFF in overtime. It calls RoundEngine.RedrawHand, which always
//    reshuffles the discard into the draw pile. In overtime the discard is otherwise
//    never recycled, so leaving it on would hand the player a free deck refill and defuse
//    the deck-out loss entirely - strictly better than paying the continue cost.
//  - İade stays ON: ReplaceHandCard draws through the normal rules, so in overtime an
//    empty draw pile is a loss like any other draw. Using it there is a real gamble.
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
            SetDescription(
                "Twice per round, discard your whole hand and draw a fresh one. "
                    + "Costs no turn; disabled in overtime.",
                "Raunt başına 2 kez tüm elini ıskartaya atıp yeni el çekersin. "
                    + "Tur harcamaz, uzatmada çalışmaz.");
            ChargesPerRound = 2;
        }

        /// <summary>See the file header: a free discard recycle would break overtime.</summary>
        public override bool DisabledInOvertime
        {
            get { return true; }
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
            SetDescription(
                "Twice per round, return a single held block and draw a replacement. Costs no turn.",
                "Raunt başına 2 kez elindeki tek bir bloğu iade edip yenisini çekersin. Tur harcamaz.");
            ChargesPerRound = 2;
        }

        /// <summary>The player picks which held block to send back.</summary>
        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.HandCard; }
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
            SetDescription(
                "You see the top card of the draw pile.",
                "Çekme destesinin en üstündeki kartı görürsün.");
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

    /// <summary>"Hafıza" - bonus-hand cards you did not play carry over into the next round.
    /// If more carry over than the round-start hand size, the excess is trimmed at random.
    /// Only round-scoped bonus cards (copies) carry; owned deck cards would duplicate the
    /// ones already reshuffled into the draw pile, so they are left behind.</summary>
    public sealed class HafizaJoker : Joker
    {
        private readonly System.Collections.Generic.List<BlockCard> carried =
            new System.Collections.Generic.List<BlockCard>();
        private int baseHandSize = -1;
        private int cardsCarriedTotal;

        public HafizaJoker()
            : base("hafiza", "Hafıza")
        {
            SetDescription(
                "Bonus-hand cards you did not play carry over to the next round. Any beyond "
                    + "your round-start hand size are discarded at random.",
                "Elinde kalan bonus kartlar sonraki raunda taşınır. Raunt başındaki el "
                    + "büyüklüğünü aşan kartlar rastgele atılır.");
        }

        /// <summary>Cards this joker has carried across a round boundary over the whole run.
        /// The LIFETIME total, not what it is holding - that is what says whether owning it has
        /// been worth a slot.</summary>
        public int CardsCarriedTotal
        {
            get { return cardsCarriedTotal; }
        }

        /// <summary>
        /// NO proc statistics: how many rounds it happened to fire in says nothing about this
        /// joker. The status line says the two things that do, each in a full sentence - a bare
        /// "0 carried  ·  total 2" read as a riddle. Between rounds it names what is WAITING to
        /// come over; otherwise it names what it has brought over in the whole run.
        /// </summary>
        public override string StatusText
        {
            get
            {
                if (carried.Count > 0)
                {
                    return Loc.Pick(carried.Count + " card(s) waiting for next round",
                        "sonraki rauntta " + carried.Count + " kart gelecek");
                }
                if (cardsCarriedTotal > 0)
                {
                    return Loc.Pick(cardsCarriedTotal + " card(s) carried so far",
                        "şimdiye dek " + cardsCarriedTotal + " kart taşıdı");
                }
                return Loc.Pick("nothing carried yet", "henüz kart taşımadı");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            baseHandSize = ctx.Rules.HandSize;
            for (int i = 0; i < carried.Count; i++)
            {
                ctx.Round.AddBonusCard(carried[i], BonusPlayOutcome.ExpireFromRound);
            }
            carried.Clear();
        }

        public override void OnRoundEnded(RoundContext ctx, RoundOutcome outcome)
        {
            carried.Clear();
            System.Collections.Generic.IReadOnlyList<BonusSlot> bonus = ctx.Round.BonusHand;
            for (int i = 0; i < bonus.Count; i++)
            {
                // Owned deck cards are already back in the piles; carrying them would clone.
                if (!IsOwned(ctx.Session, bonus[i].Card.Id))
                {
                    carried.Add(bonus[i].Card);
                }
            }
            int cap = baseHandSize > 0 ? baseHandSize : ctx.Rules.HandSize;
            while (carried.Count > cap)
            {
                carried.RemoveAt(ctx.Rng.NextInt(0, carried.Count));
            }
            // Counted AFTER the trim, so the total is what really crossed the boundary and not
            // what was picked up before the excess was thrown away.
            cardsCarriedTotal += carried.Count;
        }

        private static bool IsOwned(GameSession session, int cardId)
        {
            System.Collections.Generic.IReadOnlyList<BlockCard> owned = session.OwnedCards;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].Id == cardId)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
