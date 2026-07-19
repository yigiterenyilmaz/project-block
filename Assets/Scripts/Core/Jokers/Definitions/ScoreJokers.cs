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
}
