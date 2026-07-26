// PURPOSE: "Kredi kartı" - the joker that lets the player shop in the market with points they
// have not earned. Everything it does is enforced by GameSession, because debt is an economy
// rule and the economy is the session's: this class is the switch, not the machinery.
//
// CONFIRMED RULES:
//  - buying past your score is allowed while this is held; the shortfall becomes DEBT
//    (your own points are spent first - it is a credit card, not a blank cheque);
//  - the debt compounds by InterestPercent at the END OF EVERY ROUND;
//  - repaying is MANUAL, in the market (GameSession.RepayDebt). Earnings never settle it by
//    themselves - carrying the debt one more round is the decision this joker is built around;
//  - a BOSS ROUND that ends with the debt still open ends the RUN
//    (LossReason.DebtNotRepaid), even if that boss round was the last one of the run;
//  - this joker CANNOT BE SOLD while the debt is open, so the debt can never be walked away
//    from (JokerInventory.CanSell).
//
// Practical consequence of manual repayment, by design rather than by accident: paying only
// happens in the market, so the real deadline is the market BEFORE the boss round. What you
// earn during the boss round itself cannot save you.
//
// All numbers are BALANCE PLACEHOLDERS.

namespace ProjectBlock.Core
{
    /// <summary>"Kredi kartı" - shop on credit, pay compound interest, settle before a boss.</summary>
    public sealed class KrediKartiJoker : Joker
    {
        /// <summary>Interest added to the outstanding debt at the end of every round.</summary>
        public int InterestPercent = 10;

        public KrediKartiJoker()
            : base("kredi_karti", "Kredi Kartı")
        {
            SetDescription(
                "Buy anything in the market, even with points you do not have. What you borrow "
                    + "gains 10% interest at the end of every round, and if a boss round ends "
                    + "with the debt still open you lose the run. It cannot be sold until you "
                    + "have paid.",
                "Markette puanın yetmese de her şeyi alabilirsin. Aldığın borç her raunt sonunda "
                    + "%10 bileşik faiz işler; borcunu kapatmadan bir patron raundu biterse oyunu "
                    + "kaybedersin. Borcun varken bu joker satılamaz.");
        }

        public override bool GrantsMarketCredit
        {
            get { return true; }
        }

        public override int MarketCreditInterestPercent
        {
            get { return InterestPercent; }
        }

        /// <summary>The debt as of the last hook that could see it. StatusText takes no context,
        /// so the panel cannot read the session live - it refreshes at the moments that matter
        /// (round start/end, entering and leaving the market). The HUD carries the live number.</summary>
        private long lastKnownDebt;

        public override string StatusText
        {
            get
            {
                return lastKnownDebt > 0
                    ? Loc.Pick("owes " + lastKnownDebt, "borç " + lastKnownDebt)
                    : Loc.Pick("clear", "borçsuz");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            lastKnownDebt = ctx.Session.Debt;
        }

        public override void OnRoundEnded(RoundContext ctx, RoundOutcome outcome)
        {
            lastKnownDebt = ctx.Session.Debt;
        }

        public override void OnMarketEntered(SessionContext ctx)
        {
            lastKnownDebt = ctx.Session.Debt;
        }

        public override void OnMarketLeft(SessionContext ctx, bool anythingPurchased)
        {
            lastKnownDebt = ctx.Session.Debt;
        }
    }
}
