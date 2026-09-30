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
                    + "negative. Every point you earn pays the debt first. A stage that starts in "
                    + "debt must also earn 25% of it on top of its bar. What is left gains 12.5% "
                    + "interest after every stage. Clear it within 3 stages or the bailiff takes "
                    + "your jokers, powers and blocks, most valuable first, at half price. Clear "
                    + "it in the first stage and the bank rewards you. Cannot be sold while you owe.",
                "Puanın yetmese de marketten her şeyi alabilirsin - cüzdanın eksiye düşer. "
                    + "Kazandığın her puan önce borca gider. Borçla başlayan aşamada eşiğe ek olarak "
                    + "borcun %25'ini (asgari ödeme) de kazanman gerekir. Kalan borca her aşama "
                    + "sonunda %12,5 faiz işler. 3 aşama içinde kapatamazsan haciz gelir: en "
                    + "değerliden başlayarak joker, güç ve bloklarına yarı fiyatına el konur. İlk "
                    + "aşamada faize girmeden kapatırsan banka ödül verir. Borcun varken satılamaz.");
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
