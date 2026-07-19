// PURPOSE: Powers that act on the cards rather than the board: Cımbız, Klon, Büyüteç,
// Transfer, Hologram, Hızlı çekim şarjörü.
//
// None of them costs a turn (the central power rule), so they are all about setting up the
// placement you are ABOUT to make.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Cımbız" - rotate any held block, not just a mechanical one. The engine
    /// already knows how to rotate; this power just lifts the "mechanical only" gate.</summary>
    public sealed class CimbizPower : Power
    {
        public CimbizPower()
            : base("cimbiz", "Cımbız")
        {
            SetDescription(
                "Rotates a held block of your choice (it does not need to be mechanical).",
                "Elindeki seçtiğin bloğu çevirir (mekanik blok olması gerekmez).");
            BaseSellValue = 35;
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.HandCard; }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return target.HandIndex.HasValue
                && target.HandIndex.Value >= 0
                && target.HandIndex.Value < ctx.Round.Hand.Count;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            ctx.Round.RotateCard(target.HandIndex.Value, true);
            return true;
        }
    }

    /// <summary>"Klon" - two throwaway copies of a held block land in the bonus hand.</summary>
    public sealed class KlonPower : Power
    {
        public int CopyCount = 2;

        public KlonPower()
            : base("klon", "Klon")
        {
            SetDescription(
                "Adds 2 copies of a held card of your choice to your bonus hand.",
                "Elindeki seçtiğin kartın 2 kopyasını bonus eline ekler.");
            BaseSellValue = 45;
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.HandCard; }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return target.HandIndex.HasValue
                && target.HandIndex.Value >= 0
                && target.HandIndex.Value < ctx.Round.Hand.Count;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            BlockCard source = ctx.Round.Hand[target.HandIndex.Value];
            for (int i = 0; i < CopyCount; i++)
            {
                // Fresh ids: the copies are their own cards, so the board can tell their
                // cubes apart from the original's (fire chains, "Kazı çalışması"...).
                BlockCard copy = ctx.Session.CreateCard(source.Shape, source.Elements);
                ctx.Round.AddBonusCard(copy, BonusPlayOutcome.ExpireFromRound);
            }
            return true;
        }
    }

    /// <summary>"Büyüteç" - the top two cards of the draw pile turn face-up. Pure
    /// information: the draw order itself is untouched.</summary>
    public sealed class BuyutecPower : Power
    {
        public int RevealCount = 2;

        public BuyutecPower()
            : base("buyutec", "Büyüteç")
        {
            SetDescription(
                "Reveals the top two cards of the draw pile.",
                "Çekme destesinin en üstteki iki kartını açığa çıkarır.");
            BaseSellValue = 30;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            if (ctx.Rules.RevealedDrawCount < RevealCount)
            {
                ctx.Rules.RevealedDrawCount = RevealCount;
            }
            return true;
        }

        /// <summary>The reveal lasts for the round; a new round starts blind again.</summary>
        public override void OnRoundStarted(RoundContext ctx)
        {
            ctx.Rules.RevealedDrawCount = 0;
        }

        public override void OnRemoved(SessionContext ctx)
        {
            ctx.Rules.RevealedDrawCount = 0;
        }
    }

    /// <summary>"Transfer" - the card most recently discarded swaps places with the top of
    /// the draw pile. The card that came off the draw pile lands FACE-UP in the discard, so
    /// the player finds out what they just gave away.</summary>
    public sealed class TransferPower : Power
    {
        public TransferPower()
            : base("transfer", "Transfer")
        {
            SetDescription(
                "Swaps the last discarded card with the top of the draw pile; "
                    + "you see what you gave away.",
                "Iskartadaki son kart ile çekme destesinin üstündeki kart yer "
                    + "değiştirir; verdiğin kartı görürsün.");
            BaseSellValue = 35;
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Round.Deck.DiscardCount > 0 && ctx.Round.Deck.DrawCount > 0;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Round.Deck.SwapDiscardTopWithDrawTop();
        }
    }

    /// <summary>"Hologram" - sends a bonus-hand card to the discard, which folds it back
    /// into the round's pile economy instead of letting it expire unused.</summary>
    public sealed class HologramPower : Power
    {
        public HologramPower()
            : base("hologram", "Hologram")
        {
            SetDescription(
                "Moves a bonus-hand card into the discard, folding it back into the piles.",
                "Bonus elindeki bir kartı ıskartaya çıkarıp desteye katar.");
            BaseSellValue = 30;
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.HandCard; }
        }

        /// <summary>The index counts into the BONUS hand here, which is how the UI presents
        /// bonus cards: as extra slots after the normal hand.</summary>
        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            int index = BonusIndexOf(ctx.Round, target);
            return index >= 0;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            int index = BonusIndexOf(ctx.Round, target);
            return index >= 0 && ctx.Round.MoveBonusCardToDiscard(index);
        }

        private static int BonusIndexOf(RoundEngine round, ActivationTarget target)
        {
            if (!target.HandIndex.HasValue || round.BonusHand.Count == 0)
            {
                return -1;
            }
            int slot = target.HandIndex.Value;
            // The UI numbers bonus cards after the hand; accept either numbering.
            int index = slot >= round.Hand.Count ? slot - round.Hand.Count : slot;
            return index >= 0 && index < round.BonusHand.Count ? index : -1;
        }
    }

    /// <summary>"Hızlı çekim şarjörü" - dumps whatever is left of the draw pile into the
    /// discard and shuffles everything back, which is the fastest way to reach the cards you
    /// buried. In overtime the reshuffle is the point: nothing else recycles the discard.</summary>
    public sealed class HizliCekimSarjoruPower : Power
    {
        public HizliCekimSarjoruPower()
            : base("hizli_cekim_sarjoru", "Hızlı Çekim Şarjörü")
        {
            SetDescription(
                "Instantly empties the draw pile and forces the discard to reshuffle in.",
                "Çekme destesini anında bitirir ve ıskartanın karılmasını sağlar.");
            BaseSellValue = 45;
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Round.Deck.DrawCount + ctx.Round.Deck.DiscardCount > 0;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            RoundDeck deck = ctx.Round.Deck;
            deck.DumpDrawPileIntoDiscard();
            deck.ShuffleDiscardIntoDraw();
            return true;
        }
    }

    /// <summary>Shared helper for the powers that drop throwaway copies of pile cards into the
    /// bonus hand (Aşırma, Yedekleme, Soğuk füzyon). Copies get fresh ids so the board can
    /// tell their cubes apart from the original's.</summary>
    internal static class BonusCopyHelper
    {
        public static void AddCopies(RoundContext ctx, BlockCard source, int count)
        {
            for (int i = 0; i < count; i++)
            {
                BlockCard copy = ctx.Session.CreateCard(source.Shape, source.Elements);
                ctx.Round.AddBonusCard(copy, BonusPlayOutcome.ExpireFromRound);
            }
        }

        public static BlockCard RandomFrom(RoundContext ctx, IReadOnlyList<BlockCard> pile)
        {
            return pile.Count == 0 ? null : pile[ctx.Rng.NextInt(0, pile.Count)];
        }
    }

    /// <summary>"Aşırma" - drops 2 copies of a random DRAW-pile card into the bonus hand.</summary>
    public sealed class AsirmaPower : Power
    {
        public int CopyCount = 2;

        public AsirmaPower()
            : base("asirma", "Aşırma")
        {
            SetDescription(
                "Adds 2 copies of a random card from the draw pile to your bonus hand.",
                "Çekme destesinden rastgele bir kartın 2 kopyasını bonus eline ekler.");
            BaseSellValue = 40;
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Round.Deck.DrawCount > 0;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            BlockCard source = BonusCopyHelper.RandomFrom(ctx, ctx.Round.Deck.DrawPile);
            if (source == null)
            {
                return false;
            }
            BonusCopyHelper.AddCopies(ctx, source, CopyCount);
            return true;
        }
    }

    /// <summary>"Yedekleme" - drops 2 copies of a random DISCARD card into the bonus hand.</summary>
    public sealed class YedeklemePower : Power
    {
        public int CopyCount = 2;

        public YedeklemePower()
            : base("yedekleme", "Yedekleme")
        {
            SetDescription(
                "Adds 2 copies of a random card from the discard pile to your bonus hand.",
                "Iskartadan rastgele bir kartın 2 kopyasını bonus eline ekler.");
            BaseSellValue = 40;
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Round.Deck.DiscardCount > 0;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            BlockCard source = BonusCopyHelper.RandomFrom(ctx, ctx.Round.Deck.DiscardPile);
            if (source == null)
            {
                return false;
            }
            BonusCopyHelper.AddCopies(ctx, source, CopyCount);
            return true;
        }
    }

    /// <summary>"Soğuk füzyon" - copies one DISCARD card and one DRAW card into the bonus
    /// hand (whichever piles have a card; needs at least one).</summary>
    public sealed class SogukFuzyonPower : Power
    {
        public SogukFuzyonPower()
            : base("soguk_fuzyon", "Soğuk Füzyon")
        {
            SetDescription(
                "Copies one card from the discard and one from the draw pile into your bonus hand.",
                "Iskartadan bir kartın ve çekme elinden bir kartın kopyasını bonus ele koyar.");
            BaseSellValue = 45;
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Round.Deck.DrawCount > 0 || ctx.Round.Deck.DiscardCount > 0;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            BlockCard fromDiscard = BonusCopyHelper.RandomFrom(ctx, ctx.Round.Deck.DiscardPile);
            BlockCard fromDraw = BonusCopyHelper.RandomFrom(ctx, ctx.Round.Deck.DrawPile);
            if (fromDiscard == null && fromDraw == null)
            {
                return false;
            }
            if (fromDiscard != null)
            {
                BonusCopyHelper.AddCopies(ctx, fromDiscard, 1);
            }
            if (fromDraw != null)
            {
                BonusCopyHelper.AddCopies(ctx, fromDraw, 1);
            }
            return true;
        }
    }
}
