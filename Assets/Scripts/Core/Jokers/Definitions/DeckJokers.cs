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
            IsLegendary = true;
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

    /// <summary>"Dezenformasyon" - once the round's opening hand is dealt, the draw pile is
    /// split into two equal, individually-kept halves: one is the draw pile, the other the
    /// discard. The two halves are never poured together. Each turn their ROLES swap: the pile
    /// you drew from this turn becomes the one you discard into next turn, and vice versa - so
    /// played cards always land in the CURRENT discard and draws come from the CURRENT draw
    /// pile, even as those roles flip every turn. Hand size +1.</summary>
    public sealed class DezenformasyonJoker : Joker
    {
        public int ExtraHandSize = 1;

        private bool applied;

        /// <summary>Turns played with this joker; the parity is the current draw/discard flow.</summary>
        public int TurnsSeen { get; private set; }

        public DezenformasyonJoker()
            : base("dezenformasyon", "Dezenformasyon")
        {
            SetDescription(
                "After the opening hand is dealt, the draw pile is split into two equal piles "
                    + "- one draw, one discard - kept separate and never mixed. Each turn the "
                    + "two roles swap: you still discard into the current discard and draw from "
                    + "the current draw pile, but which pile is which flips every turn. The card "
                    + "you play always stays in the discard - it never comes straight back. Hand +1.",
                "Açılış eli dağıtıldıktan sonra çekme destesi iki eşit, ayrı desteye bölünür - "
                    + "biri çekme biri ıskarta - asla karışmazlar. Her tur roller yer değiştirir: "
                    + "yine mevcut ıskartaya atar, mevcut çekme destesinden çekersin, ama hangi "
                    + "destenin hangisi olduğu her tur değişir. Oynadığın kart hep ıskartada kalır, "
                    + "sıradaki turda geri gelmez. El +1.");
            BaseSellValue = 60;
            IsLegendary = true;
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
            Apply(ctx.Rules);
            TurnsSeen = 0;
            // The engine has already dealt the opening hand; split what remains into halves.
            ctx.Round.Deck.SplitDrawIntoDiscard();
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            // Keep the card just played OUT of next turn's draw. It was discarded during this
            // turn (after the refill already drew from the draw pile), so moving it onto the draw
            // pile now means the swap below carries it into the discard - where it stays instead
            // of being handed straight back next turn. Bonus cards that expired never hit a pile.
            if (!turn.Report.PlayedCardExpired)
            {
                turn.Round.Deck.MoveDiscardedCardToDrawTop(turn.Report.Card);
            }
            // Roles swap for next turn: this turn's discard becomes next turn's draw pile and
            // vice versa. Just the swap - the two halves are never poured together or shuffled.
            turn.Round.Deck.SwapPiles();
            TurnsSeen++;
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
    /// holds, at least one. Whatever is left in hand at end of turn is thrown onto the discard,
    /// which is what makes the hand grow. The refill only ever draws what the draw pile
    /// actually holds - it never auto-recycles the discard and a short pile is never a loss;
    /// the discard is reshuffled in ONLY when a card is played into an already-empty draw pile.</summary>
    public sealed class ImitasyonJoker : Joker
    {
        /// <summary>Upper bound on the mirrored hand size. High by default (the "draw only
        /// what exists" rule already keeps the actual hand small); mostly a UI/sanity guard.</summary>
        public int MaxHandSize = 100;

        private int originalHandSize = -1;

        public ImitasyonJoker()
            : base("imitasyon", "İmitasyon")
        {
            SetDescription(
                "Your hand size equals the discard pile's card count (min 1); leftover hand "
                    + "cards are discarded each turn. Refills draw only what the draw pile "
                    + "holds - it never reshuffles to fill up, and the discard is recycled only "
                    + "when you play into an empty draw pile.",
                "El boyutun ıskartadaki kart sayısına eşittir (en az 1); tur sonunda elde "
                    + "kalanlar ıskartaya gider. El sadece çekme destesinde olanı çeker - "
                    + "dolmak için karmaz; ıskarta yalnızca boş desteye kart oynadığında geri "
                    + "karılır.");
            BaseSellValue = 60;
            IsLegendary = true;
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
            ctx.Rules.DrawOnlyAvailableNoReshuffle = true;
            ctx.Rules.SkipStandardRefill = true;
            // The next round opens with an empty discard, so the opening hand is one card.
            // This must be set BEFORE the round's engine fills its opening hand.
            ctx.Rules.HandSize = 1;
        }

        public override void OnRemoved(SessionContext ctx)
        {
            if (originalHandSize >= 0)
            {
                ctx.Rules.HandSize = originalHandSize;
                originalHandSize = -1;
            }
            ctx.Rules.DrawOnlyAvailableNoReshuffle = false;
            ctx.Rules.SkipStandardRefill = false;
        }

        /// <summary>Reset the shared hand size to one BEFORE the next round is built, so its
        /// engine deals a one-card opening hand rather than a giant one left over from this
        /// round's mirror.</summary>
        public override void OnRoundEnded(RoundContext ctx, RoundOutcome outcome)
        {
            ctx.Rules.HandSize = 1;
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            ctx.Rules.DrawOnlyAvailableNoReshuffle = true;
            ctx.Rules.SkipStandardRefill = true;
            // The round starts with an empty discard, so the hand starts at the minimum.
            Resize(ctx.Rules, ctx.Round.Deck.DiscardCount);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            RoundEngine round = turn.Round;
            // Dump the leftover hand onto the discard (this is what grows the mirror).
            round.DiscardWholeHand();
            // The ONLY reshuffle: a card was just played and the draw pile is now empty.
            if (round.Deck.DrawCount == 0)
            {
                round.Deck.ShuffleDiscardIntoDraw();
            }
            // Size to the discard, then refill from whatever the draw pile actually holds.
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
                    + "split in two; you may then inspect half the discard by clicking it. "
                    + "You may swap the piles once until the next split - after a swap the "
                    + "discard is hidden again.",
                "Raunt başında ve deste bitince desteler karılıp ikiye bölünür; sonra "
                    + "ıskartaya tıklayarak yarısını inceleyebilirsin. Sonraki bölünmeye kadar "
                    + "desteleri bir kez takas edebilirsin - takastan sonra ıskarta yine gizlenir.");
            ChargesPerRound = 0; // charges are per RESHUFFLE here, not per round
            BaseSellValue = 65;
            IsLegendary = true;
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
            ctx.Rules.HideDiscardTop = false;
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
            // After a swap the discard is hidden: no inspection and no visible top card,
            // until the next reshuffle (Split) reveals it again.
            ctx.Rules.RevealedDiscardCount = 0;
            ctx.Rules.HideDiscardTop = true;
            return true;
        }

        private void Split(RoundEngine round, RoundRules rules)
        {
            round.Deck.MergeAndSplitHalves();
            rules.RevealedDiscardCount = round.Deck.DiscardCount / 2;
            rules.HideDiscardTop = false;
            SwapAvailable = true;
        }
    }
}
