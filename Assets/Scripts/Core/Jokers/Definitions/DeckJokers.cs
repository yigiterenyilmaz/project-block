// PURPOSE: Jokers that rewrite how the two piles work: Oryantasyon, Dezenformasyon,
// İmitasyon, Fraksiyon. They are the deck-economy counterpart of the board jokers, and
// they all lean on RoundDeck primitives rather than touching the piles by hand.
//
// WHY THESE ARE DANGEROUS TO BALANCE: the loss conditions of the game are deck-based
// (the hand must always refill; in overtime an empty draw pile ends the run). Every joker
// here moves cards between the piles, so each one either softens or sharpens those losses:
//   Oryantasyon  - the discard stays empty and cards keep coming back: the deck effectively
//                  never runs out, which nearly removes the deck-out loss.
//   Dezenformasyon - the discard becomes a live draw source every other turn, so overtime
//                  loses its "the discard never comes back" bite.
//   İmitasyon    - hand size tracks the discard, so it can demand more cards than the draw
//                  pile holds. That is a real way to lose, hence MaxHandSize.
//   Fraksiyon    - halves the draw pile on purpose, then lets the player swap the piles once.
//
// All numbers are BALANCE PLACEHOLDERS.

namespace ProjectBlock.Core
{
    /// <summary>"Oryantasyon" - played cards are buried at a random depth in the draw pile
    /// instead of going to the discard, and the top of the draw pile is always visible.</summary>
    public sealed class OryantasyonJoker : Joker
    {
        public OryantasyonJoker()
            : base("oryantasyon", "Oryantasyon")
        {
            SetDescription(
                "Played cards are buried at a random depth of the draw pile instead of "
                    + "being discarded. The pile's top card is always visible.",
                "Kartlar ıskartaya değil, çekme destesinin rastgele bir yerine "
                    + "girer. Destenin en üstteki kartı hep görünür.");
            BaseSellValue = 55;
        }

        public override void OnAcquired(SessionContext ctx)
        {
            Apply(ctx.Rules);
        }

        public override void OnRemoved(SessionContext ctx)
        {
            ctx.Rules.PlayedCardsReturnToDrawPile = false;
            ctx.Rules.RevealTopDrawCard = false;
        }

        /// <summary>Re-asserted every round: RoundRules is shared, and another joker may
        /// have switched the reveal off in between.</summary>
        public override void OnRoundStarted(RoundContext ctx)
        {
            Apply(ctx.Rules);
        }

        private static void Apply(RoundRules rules)
        {
            rules.PlayedCardsReturnToDrawPile = true;
            rules.RevealTopDrawCard = true;
        }
    }

    /// <summary>"Dezenformasyon" - every turn the whole deck is poured together, shuffled and
    /// dealt back out as two halves, and the roles of the piles swap: the pile you drew from
    /// last turn is the one you discard into this turn. Hand size +1.</summary>
    public sealed class DezenformasyonJoker : Joker
    {
        public int ExtraHandSize = 1;

        private bool applied;

        /// <summary>Turns played with this joker; the parity is what swaps the piles.</summary>
        public int TurnsSeen { get; private set; }

        public DezenformasyonJoker()
            : base("dezenformasyon", "Dezenformasyon")
        {
            SetDescription(
                "Every turn the draw and discard piles swap roles, then everything merges, "
                    + "shuffles and splits in two. Hand size +1.",
                "Her tur deste ikiye bölünüp karılır ve çekme/ıskarta rolleri yer "
                    + "değiştirir. El boyutu +1.");
            BaseSellValue = 60;
        }

        public override string StatusText
        {
            get
            {
                bool normal = TurnsSeen % 2 == 0;
                return Loc.Pick(normal ? "normal flow" : "reversed flow",
                    (normal ? "normal" : "ters") + " yön");
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

        public override void OnRoundStarted(RoundContext ctx)
        {
            TurnsSeen = 0;
            Apply(ctx.Rules);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            TurnsSeen++;
            // Swap first (this turn's discard becomes next turn's draw pile), then pour both
            // together and re-deal the halves, which is the "her turun sonunda karılır" part.
            turn.Round.Deck.SwapPiles();
            turn.Round.Deck.MergeAndSplitHalves();
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
        }
    }

    /// <summary>"İmitasyon" - the hand mirrors the discard: as many cards as the discard
    /// holds, at least one. Whatever is left in hand at end of turn is thrown onto the
    /// discard, which is what makes the hand grow.</summary>
    public sealed class ImitasyonJoker : Joker
    {
        /// <summary>Safety cap. Without it the hand can demand more cards than exist and
        /// the round is lost to "hand cannot be refilled" through no fault of the player.</summary>
        public int MaxHandSize = 8;

        private int originalHandSize = -1;

        public ImitasyonJoker()
            : base("imitasyon", "İmitasyon")
        {
            SetDescription(
                "Your hand size equals the discard pile's card count (min 1). Cards left "
                    + "in hand at end of turn are discarded too.",
                "El boyutun ıskartadaki kart sayısına eşit olur (en az 1). Tur "
                    + "sonunda elde kalanlar da ıskartaya gider.");
            BaseSellValue = 60;
        }

        public override string StatusText
        {
            get
            {
                return originalHandSize >= 0
                    ? Loc.Pick("hand = discard", "el = ıskarta")
                    : Loc.Pick("off", "kapalı");
            }
        }

        public override void OnAcquired(SessionContext ctx)
        {
            if (originalHandSize < 0)
            {
                originalHandSize = ctx.Rules.HandSize;
            }
        }

        public override void OnRemoved(SessionContext ctx)
        {
            if (originalHandSize >= 0)
            {
                ctx.Rules.HandSize = originalHandSize;
                originalHandSize = -1;
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            // The round starts with an empty discard, so the hand starts at the minimum.
            Resize(ctx.Rules, ctx.Round.Deck.DiscardCount);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            RoundEngine round = turn.Round;
            // Order matters: dumping the hand is what fills the discard, and the discard is
            // what the new hand size is read from. Dump first, then size, then refill.
            round.DiscardWholeHand();
            Resize(turn.Rules, round.Deck.DiscardCount);
            round.RefillHandToSize();
        }

        private void Resize(RoundRules rules, int discardCount)
        {
            int wanted = discardCount < 1 ? 1 : discardCount;
            if (wanted > MaxHandSize)
            {
                wanted = MaxHandSize;
            }
            rules.HandSize = wanted;
        }
    }

    /// <summary>"Fraksiyon" - at round start and whenever the draw pile runs dry, the piles
    /// are merged, shuffled and split in half, and half the discard is turned face-up. Until
    /// the next such reshuffle the player may swap the two piles once.</summary>
    public sealed class FraksiyonJoker : Joker
    {
        public FraksiyonJoker()
            : base("fraksiyon", "Fraksiyon")
        {
            SetDescription(
                "At round start and whenever the draw pile empties, the piles shuffle and "
                    + "split in two; half the discard is revealed. You may swap the piles "
                    + "once until the next split.",
                "Raunt başında ve deste bitince desteler karılıp ikiye bölünür, "
                    + "ıskartanın yarısı görünür olur. Sonraki bölünmeye kadar desteleri "
                    + "bir kez takas edebilirsin.");
            ChargesPerRound = 0; // charges are per RESHUFFLE here, not per round
            BaseSellValue = 65;
        }

        /// <summary>True while the player still holds this cycle's swap.</summary>
        public bool SwapAvailable { get; private set; }

        public override string StatusText
        {
            get
            {
                return SwapAvailable
                    ? Loc.Pick("swap ready", "takas hazır")
                    : Loc.Pick("no swap", "takas yok");
            }
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.None; }
        }

        public override void OnRemoved(SessionContext ctx)
        {
            ctx.Rules.RevealedDiscardCount = 0;
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            Split(ctx.Round, ctx.Rules);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            // The engine flags the moment a draw attempt found the pile empty.
            if (turn.Report.DrawPileEmptiedThisTurn)
            {
                Split(turn.Round, turn.Rules);
            }
        }

        public override bool CanActivate(RoundContext ctx)
        {
            return SwapAvailable && ctx.Round.Status == RoundStatus.InProgress;
        }

        public override bool Activate(RoundContext ctx, ActivationTarget target)
        {
            if (!CanActivate(ctx))
            {
                return false;
            }
            SwapAvailable = false;
            ctx.Round.Deck.SwapPiles();
            ctx.Rules.RevealedDiscardCount = ctx.Round.Deck.DiscardCount / 2;
            return true;
        }

        private void Split(RoundEngine round, RoundRules rules)
        {
            round.Deck.MergeAndSplitHalves();
            rules.RevealedDiscardCount = round.Deck.DiscardCount / 2;
            SwapAvailable = true;
        }
    }
}
