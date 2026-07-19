// PURPOSE: Two score jokers that react to a board/deck event rather than to the block
// played: bereket (permanent growth) and Harcama bonusu (payout when the draw pile runs
// out).
//
// CONFIRMED RULES:
//  - bereket's "+ shaped explosion" is read as: at least one ROW and at least one COLUMN
//    exploded in the same turn. On a rectangular board those always cross, which is
//    exactly the plus. The growth is PERMANENT while the joker is held: every later turn
//    gets the accumulated bonus, including the turn that triggered the growth.
//  - Harcama bonusu pays whenever a draw attempt finds the pile empty - which can happen
//    several times in a round before the threshold, because the discard is recycled then.
//    It also pays in overtime, where that same event is the loss condition; the points are
//    a consolation, not a rescue.
// All numbers are BALANCE PLACEHOLDERS.

namespace ProjectBlock.Core
{
    /// <summary>"bereket" - every plus-shaped explosion permanently raises the score this
    /// joker adds to every turn.</summary>
    public sealed class BereketJoker : Joker
    {
        /// <summary>Points added to each turn per stack collected.</summary>
        public int PointsPerStack = 5;

        /// <summary>Plus-shaped explosions seen so far. Permanent while the joker is held.</summary>
        public int Stacks { get; private set; }

        public BereketJoker()
            : base("bereket", "Bereket")
        {
            SetDescription(
                "Whenever a row and a column explode in the same turn, your score gains grow permanently.",
                "Aynı turda satır ve sütun birlikte patlarsa kazandığın puan kalıcı olarak artar.");
            BaseSellValue = 50;
        }

        public override string StatusText
        {
            get { return Loc.Pick("+" + (Stacks * PointsPerStack) + "/turn", "+" + (Stacks * PointsPerStack) + "/tur"); }
        }

        public override void ModifyScore(TurnContext turn)
        {
            bool plusExplosion = turn.Report.ExplodedRows.Count > 0
                && turn.Report.ExplodedColumns.Count > 0;
            if (plusExplosion)
            {
                Stacks++;
            }
            if (Stacks > 0)
            {
                turn.Score.AddFlat(Stacks * PointsPerStack, DefId);
            }
        }
    }

    /// <summary>"Harcama bonusu" - pays out every time the draw pile runs dry.</summary>
    public sealed class HarcamaBonusuJoker : Joker
    {
        public int PointsPerEmptyDrawPile = 60;

        /// <summary>Times it paid out this round, for the UI.</summary>
        public int TriggeredThisRound { get; private set; }

        public HarcamaBonusuJoker()
            : base("harcama_bonusu", "Harcama Bonusu")
        {
            SetDescription(
                "You gain points every time the draw pile runs out.",
                "Çekme destesi her tükendiğinde puan kazanırsın.");
            BaseSellValue = 40;
        }

        public override string StatusText
        {
            get { return Loc.Pick(TriggeredThisRound + " times", TriggeredThisRound + " kez"); }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            TriggeredThisRound = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (!turn.Report.DrawPileEmptiedThisTurn)
            {
                return;
            }
            TriggeredThisRound++;
            // Granted at end of turn but BEFORE the threshold check, so it can push the
            // round over the line on the very turn the deck ran out.
            turn.AddFlatScore(PointsPerEmptyDrawPile, DefId);
        }
    }

    /// <summary>"Tutumluluk" - a thin deck pays. Every turn adds a flat bonus that grows the
    /// FEWER cards the owned deck holds, dropping to nothing at ReferenceDeckSize and up.</summary>
    public sealed class TutumlulukJoker : Joker
    {
        /// <summary>Deck size at (or above) which the joker pays nothing.</summary>
        public int ReferenceDeckSize = 20;

        /// <summary>Points per card the deck is under the reference size.</summary>
        public int PointsPerCardUnder = 3;

        public TutumlulukJoker()
            : base("tutumluluk", "Tutumluluk")
        {
            SetDescription(
                "The fewer cards your deck holds, the more points you score each turn.",
                "Destendeki kart sayısı ne kadar az ise her tur o kadar fazla puan kazanırsın.");
            BaseSellValue = 45;
        }

        public override string StatusText
        {
            get { return Loc.Pick("+" + BonusFor(lastDeckSize) + "/turn", "+" + BonusFor(lastDeckSize) + "/tur"); }
        }

        private int lastDeckSize = -1;

        public override void ModifyScore(TurnContext turn)
        {
            lastDeckSize = turn.Session.OwnedCards.Count;
            int bonus = BonusFor(lastDeckSize);
            if (bonus > 0)
            {
                turn.Score.AddFlat(bonus, DefId);
            }
        }

        private int BonusFor(int deckSize)
        {
            if (deckSize < 0)
            {
                return 0;
            }
            int under = ReferenceDeckSize - deckSize;
            return under > 0 ? under * PointsPerCardUnder : 0;
        }
    }

    /// <summary>"Genel temizlik" - board-clears caused by a joker or a power between turns
    /// count as real clean sweeps. Normally they do not, because they happen with no placement
    /// resolving; this flips the central RoundRules switch that lets them through.</summary>
    public sealed class GenelTemizlikJoker : Joker
    {
        public GenelTemizlikJoker()
            : base("genel_temizlik", "Genel Temizlik")
        {
            SetDescription(
                "Clean sweeps triggered by jokers and powers count as sweeps too - normally a "
                    + "joker or power that clears the last block does not count, because it "
                    + "happens between placements.",
                "Jokerler ve güçler tarafından tetiklenen temizlikler de temizlik sayılır - "
                    + "normalde son bloğu temizleyen bir joker veya güç, blok koymadan önce "
                    + "gerçekleştiği için sayılmaz.");
            BaseSellValue = 55;
        }

        public override void OnAcquired(SessionContext ctx)
        {
            ctx.Rules.CountExternalSweeps = true;
        }

        public override void OnRemoved(SessionContext ctx)
        {
            ctx.Rules.CountExternalSweeps = false;
        }

        // RoundRules is shared and another effect may have reset it; re-assert each round.
        public override void OnRoundStarted(RoundContext ctx)
        {
            ctx.Rules.CountExternalSweeps = true;
        }
    }
}
