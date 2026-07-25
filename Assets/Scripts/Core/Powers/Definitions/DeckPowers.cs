// PURPOSE: Powers that act on the cards rather than the board: CÄ±mbÄ±z, Klon, BÃ¼yÃ¼teÃ§,
// Transfer, Hologram, HÄ±zlÄ± Ã§ekim ÅŸarjÃ¶rÃ¼.
//
// None of them costs a turn (the central power rule), so they are all about setting up the
// placement you are ABOUT to make.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"CÄ±mbÄ±z" - rotate any held block, not just a mechanical one. The engine
    /// already knows how to rotate; this power just lifts the "mechanical only" gate.</summary>
    public sealed class CimbizPower : Power
    {
        public CimbizPower()
            : base("cimbiz", "CÄ±mbÄ±z")
        {
            SetDescription(
                "Rotates a held block of your choice (it does not need to be mechanical).",
                "Elindeki seÃ§tiÄŸin bloÄŸu Ã§evirir (mekanik blok olmasÄ± gerekmez).");
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
                "Elindeki seÃ§tiÄŸin kartÄ±n 2 kopyasÄ±nÄ± bonus eline ekler.");
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
                // cubes apart from the original's (fire chains, "KazÄ± Ã§alÄ±ÅŸmasÄ±"...).
                BlockCard copy = ctx.Session.CreateCard(source.Shape, source.Elements);
                ctx.Round.AddBonusCard(copy, BonusPlayOutcome.ExpireFromRound);
            }
            return true;
        }
    }

    /// <summary>"BÃ¼yÃ¼teÃ§" - the top two cards of the draw pile turn face-up. Pure information
    /// (the draw order itself is untouched) and CONSUMABLE: every card drawn uncovers one fewer
    /// (2 -> 1 -> 0), decremented centrally in RoundEngine.NoteCardDrawn.</summary>
    public sealed class BuyutecPower : Power
    {
        public int RevealCount = 2;

        public BuyutecPower()
            : base("buyutec", "BÃ¼yÃ¼teÃ§")
        {
            SetDescription(
                "Reveals the top two cards of the draw pile. As you draw, one fewer stays "
                    + "revealed (2, then 1, then none).",
                "Ã‡ekme destesinin en Ã¼stteki iki kartÄ±nÄ± aÃ§Ä±ÄŸa Ã§Ä±karÄ±r. Kart Ã§ektikÃ§e biri daha "
                    + "az gÃ¶rÃ¼nÃ¼r (2, sonra 1, sonra hiÃ§).");
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
                "Iskartadaki son kart ile Ã§ekme destesinin Ã¼stÃ¼ndeki kart yer "
                    + "deÄŸiÅŸtirir; verdiÄŸin kartÄ± gÃ¶rÃ¼rsÃ¼n.");
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
                "Bonus elindeki bir kartÄ± Ä±skartaya Ã§Ä±karÄ±p desteye katar.");
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

    /// <summary>"HÄ±zlÄ± Ã§ekim ÅŸarjÃ¶rÃ¼" - dumps whatever is left of the draw pile into the
    /// discard and shuffles everything back, which is the fastest way to reach the cards you
    /// buried. In overtime the reshuffle is the point: nothing else recycles the discard.</summary>
    public sealed class HizliCekimSarjoruPower : Power
    {
        public HizliCekimSarjoruPower()
            : base("hizli_cekim_sarjoru", "HÄ±zlÄ± Ã‡ekim ÅarjÃ¶rÃ¼")
        {
            SetDescription(
                "Instantly empties the draw pile and forces the discard to reshuffle in.",
                "Ã‡ekme destesini anÄ±nda bitirir ve Ä±skartanÄ±n karÄ±lmasÄ±nÄ± saÄŸlar.");
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
    /// bonus hand (AÅŸÄ±rma, Yedekleme, SoÄŸuk fÃ¼zyon). Copies get fresh ids so the board can
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

    /// <summary>"Hileli zar" - MARKET-PHASE power: choose which owned cards make up the next
    /// round's opening hand. Driven entirely by the market UI (it guarantees those cards onto
    /// the top of the next fresh draw pile), so the in-round use path refuses it.</summary>
    public sealed class HileliZarPower : Power
    {
        public HileliZarPower()
            : base("hileli_zar", "Hileli Zar")
        {
            SetDescription(
                "In the market, choose the cards that make up your next round's opening hand.",
                "Market fazÄ±nda sonraki rauntun baÅŸlangÄ±Ã§ elini seÃ§ebilmeni saÄŸlar.");
        }

        // Used from the market, not the in-round power path, so it never runs here.
        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return false;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            return false;
        }
    }

    /// <summary>"AÅŸÄ±rma" - drops 2 copies of a random DRAW-pile card into the bonus hand.</summary>
    public sealed class AsirmaPower : Power
    {
        public int CopyCount = 2;

        public AsirmaPower()
            : base("asirma", "AÅŸÄ±rma")
        {
            SetDescription(
                "Adds 2 copies of a random card from the draw pile to your bonus hand.",
                "Ã‡ekme destesinden rastgele bir kartÄ±n 2 kopyasÄ±nÄ± bonus eline ekler.");
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
                "Iskartadan rastgele bir kartÄ±n 2 kopyasÄ±nÄ± bonus eline ekler.");
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

    /// <summary>"BÃ¼kÃ¼lme" - marks a held card and drops a copy in the bonus hand. For the rest
    /// of the round, every time that marked card is drawn back INTO the hand another copy is
    /// added. A clean sweep recharges the power, so it can be re-used to mark a new card.</summary>
    public sealed class BukulmePower : Power
    {
        private int? markedCardId;
        private bool markedInHand;

        public BukulmePower()
            : base("bukulme", "BÃ¼kÃ¼lme")
        {
            SetDescription(
                "Mark a held card and copy it to your bonus hand. For the rest of the round, "
                    + "each time the marked card is drawn into your hand another copy is added. "
                    + "A clean sweep lets you re-mark.",
                "Elindeki bir kartÄ± iÅŸaretler ve kopyasÄ±nÄ± bonus ele koyarsÄ±n. O raunt boyunca "
                    + "iÅŸaretli kart her ele Ã§ekildiÄŸinde bir kopyasÄ± daha eklenir. Temizlik "
                    + "yaptÄ±ÄŸÄ±nda yeniden iÅŸaretleyebilirsin.");
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.HandCard; }
        }

        public override string StatusText
        {
            get
            {
                return markedCardId.HasValue
                    ? Loc.Pick("marked #" + markedCardId.Value, "iÅŸaretli #" + markedCardId.Value)
                    : Loc.Pick("idle", "boÅŸta");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            markedCardId = null;
            markedInHand = false;
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return target.HandIndex.HasValue
                && target.HandIndex.Value >= 0
                && target.HandIndex.Value < ctx.Round.Hand.Count;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            BlockCard card = ctx.Round.Hand[target.HandIndex.Value];
            markedCardId = card.Id;
            markedInHand = true;
            AddCopy(ctx.Session, ctx.Round, card);
            return true;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (!markedCardId.HasValue)
            {
                return;
            }
            BlockCard inHand = FindInHand(turn.Round, markedCardId.Value);
            bool present = inHand != null;
            // A fresh entry into the hand (it was out last turn) spawns another copy.
            if (present && !markedInHand)
            {
                AddCopy(turn.Session, turn.Round, inHand);
            }
            markedInHand = present;
        }

        private static BlockCard FindInHand(RoundEngine round, int cardId)
        {
            for (int i = 0; i < round.Hand.Count; i++)
            {
                if (round.Hand[i].Id == cardId)
                {
                    return round.Hand[i];
                }
            }
            return null;
        }

        private static void AddCopy(GameSession session, RoundEngine round, BlockCard source)
        {
            BlockCard copy = session.CreateCard(source.Shape, source.Elements);
            round.AddBonusCard(copy, BonusPlayOutcome.ExpireFromRound);
        }
    }

    /// <summary>"SoÄŸuk fÃ¼zyon" - copies one DISCARD card and one DRAW card into the bonus
    /// hand (whichever piles have a card; needs at least one).</summary>
    public sealed class SogukFuzyonPower : Power
    {
        public SogukFuzyonPower()
            : base("soguk_fuzyon", "SoÄŸuk FÃ¼zyon")
        {
            SetDescription(
                "Copies one card from the discard and one from the draw pile into your bonus hand.",
                "Iskartadan bir kartÄ±n ve Ã§ekme elinden bir kartÄ±n kopyasÄ±nÄ± bonus ele koyar.");
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
