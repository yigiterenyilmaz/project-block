// PURPOSE: "Kredi kartı" - the joker that lets the player shop in the market with points they
// have not earned. It is the SWITCH, not the machinery: the loan's terms (the negative purse,
// earnings paying the debt first, the minimum payment on the next stage's bar, interest, the term,
// foreclosure and the bank's rewards) are session rules - GameSession.Credit.cs - because debt is
// an economy rule and the economy is the session's. Their numbers are MarketConfig's.
//
// It CANNOT BE SOLD while anything is owed (JokerInventory.CanSell), so the loan can never be
// walked away from. The bailiff, on the other hand, takes it like anything else.

namespace ProjectBlock.Core
{
    /// <summary>"Kredi kartı" - borrow without limit; pay back on time or lose your things.</summary>
    public sealed class KrediKartiJoker : Joker
    {
        public KrediKartiJoker()
            : base("kredi_karti", "Kredi Kartı")
        {
            SetDescription(
                "Buy anything in the market even with points you do not have - your purse goes "
                    + "negative and stays empty until you are clear. While you owe, what you earn "
                    + "for a stage's threshold only passes it; 25% of the debt on top is the minimum "
                    + "payment, and anything you earn in overtime pays the debt too. What is left "
                    + "gains 12.5% interest after every "
                    + "stage. Clear it within 4 stages or the bailiff takes your jokers, powers and "
                    + "blocks, most valuable first, at half price. Clear it in the first stage and "
                    + "the bank rewards you. Cannot be sold while you owe.",
                "Puanın yetmese de marketten her şeyi alabilirsin - cüzdanın eksiye düşer ve borç "
                    + "bitene kadar boş kalır. Borçluyken normal eşik için kazandığın puan ne "
                    + "cüzdana ne borca gider, sadece aşamayı geçirir; üstüne borcun %25'ini (asgari "
                    + "ödeme) kazanman gerekir, o borca gider. Uzatmada kazandığın puan da borca "
                    + "gider. Kalan borca her aşama sonunda %12,5 faiz işler. 4 aşama içinde "
                    + "kapatamazsan haciz gelir: en değerliden başlayarak joker, güç ve bloklarına "
                    + "yarı fiyatına el konur. İlk aşamada faize girmeden kapatırsan banka ödül "
                    + "verir. Borcun varken satılamaz.");
        }

        public override bool GrantsMarketCredit
        {
            get { return true; }
        }

        /// <summary>The debt and the term as of the last hook that could see them. StatusText
        /// takes no context, so the panel cannot read the session live - it refreshes at the
        /// moments that matter (round start/end, entering and leaving the market). The HUD
        /// carries the live numbers.</summary>
        private long lastKnownDebt;

        private int lastKnownTermLeft;

        public override string StatusText
        {
            get
            {
                return lastKnownDebt > 0
                    ? Loc.Pick("owes " + lastKnownDebt + " · " + lastKnownTermLeft + " left",
                        "borç " + lastKnownDebt + " · vade " + lastKnownTermLeft)
                    : Loc.Pick("clear", "borçsuz");
            }
        }

        private void Remember(GameSession session)
        {
            lastKnownDebt = session.Debt;
            lastKnownTermLeft = session.CreditTermLeft;
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            Remember(ctx.Session);
        }

        public override void OnRoundEnded(RoundContext ctx, RoundOutcome outcome)
        {
            Remember(ctx.Session);
        }

        public override void OnMarketEntered(SessionContext ctx)
        {
            Remember(ctx.Session);
        }

        public override void OnMarketLeft(SessionContext ctx, bool anythingPurchased)
        {
            Remember(ctx.Session);
        }
    }
}
