// PURPOSE: The boss that strips blocks of their identity ("Vanilya") and the two that tax the
// run deck ("Harcama vergisi", "Özel tüketim vergisi").
//
// The taxes are the only bosses that reach OUTSIDE their round: the cards they take are gone
// from GameSession.OwnedCards for the rest of the RUN, not just this round. That is the whole
// point of them, and it is why they go through GameSession.TaxOwnedCards, which refuses to
// shrink the deck below a playable size.

namespace ProjectBlock.Core
{
    /// <summary>"Vanilya" - for this round every block is a plain block: no fire, no gold, no
    /// ghost overhang, no rotation, no dynamite. The cards keep their elements (and their
    /// market value); the round simply ignores them.</summary>
    public sealed class VanilyaBoss : BossRound
    {
        public VanilyaBoss()
            : base("vanilya", "Vanilya")
        {
            SetDescription(
                "Every block loses its element for this round - no fire, gold, ghost, gears or "
                    + "dynamite. They all place as plain blocks.",
                "Bu raunt boyunca tüm bloklar element özelliklerini yitirir - ateş, altın, "
                    + "hayalet, çark, dinamit yok. Hepsi sıradan blok olarak koyulur.");
        }

        public override bool IgnoresBlockElements
        {
            get { return true; }
        }
    }

    /// <summary>"Harcama vergisi" - every time the draw pile runs dry, the run deck permanently
    /// loses cards. Cycling the deck fast is normally free; here it costs.</summary>
    public sealed class HarcamaVergisiBoss : BossRound
    {
        /// <summary>Cards taken out of the run deck each time the draw pile empties.</summary>
        public int CardsPerEmptying = 2;

        private int taxed;

        public HarcamaVergisiBoss()
            : base("harcama_vergisi", "Harcama Vergisi")
        {
            SetDescription(
                "Every time your draw pile runs out, 2 cards leave your deck permanently - for "
                    + "the rest of the run, not just this round.",
                "Çekme destesi her bittiğinde destenden kalıcı olarak 2 kart çıkar - sadece bu "
                    + "raunt için değil, oyunun kalanı için.");
        }

        /// <summary>Cards this boss has taken so far, for the UI.</summary>
        public int TaxedCards
        {
            get { return taxed; }
        }

        public override string StatusText
        {
            get { return taxed > 0 ? taxed + Loc.Pick(" cards gone", " kart gitti") : null; }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            taxed = 0;
        }

        public override void OnDrawPileEmptied(RoundContext ctx)
        {
            taxed += ctx.Session.TaxOwnedCards(CardsPerEmptying, ctx.Rng);
        }
    }

    /// <summary>"Özel tüketim vergisi" - excise: using a power permanently costs the run deck a
    /// card. Powers stay free to use, but never free to own.</summary>
    public sealed class OzelTuketimVergisiBoss : BossRound
    {
        /// <summary>Cards taken out of the run deck per power used.</summary>
        public int CardsPerPowerUse = 1;

        private int taxed;

        public OzelTuketimVergisiBoss()
            : base("ozel_tuketim_vergisi", "Özel Tüketim Vergisi")
        {
            SetDescription(
                "Using a power costs you a card from your deck, permanently - for the rest of "
                    + "the run, not just this round.",
                "Güç kullanmak destenden kalıcı olarak bir kart götürür - sadece bu raunt için "
                    + "değil, oyunun kalanı için.");
        }

        public int TaxedCards
        {
            get { return taxed; }
        }

        public override string StatusText
        {
            get { return taxed > 0 ? taxed + Loc.Pick(" cards gone", " kart gitti") : null; }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            taxed = 0;
        }

        public override void OnPowerUsed(RoundContext ctx, string powerId)
        {
            taxed += ctx.Session.TaxOwnedCards(CardsPerPowerUse, ctx.Rng);
        }
    }
}
