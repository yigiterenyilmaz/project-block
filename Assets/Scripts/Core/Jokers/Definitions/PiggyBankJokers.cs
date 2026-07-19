// PURPOSE: The three "kumbara" jokers. They do not score - they make THEMSELVES more
// valuable, and the player cashes that in by selling them. Each one banks on a different
// rhythm, so they reward different play:
//   Domuz Kumbarası - per round survived   (slow, safe)
//   Cimri Kumbara   - per turn held        (fast, rewards long rounds)
//   Altın Kumbara   - turns + sweeps + rounds (rewards clearing the board)
//
// These work TODAY even though the market does not exist yet: value accrues into
// Joker.AccruedValue and JokerInventory.Sell already pays it out as run currency.
//
// CONFIRMED RULES:
//  - "Raunt sonu" means a round the player ADVANCED from. Losing the run pays nothing.
//  - Bonus-hand plays are turns (the engine resolves them as full turns), so they bank too.
//  - They keep banking in overtime: staying in overtime is a risk the player already pays
//    for with the deck, so it is a legitimate (and intended) way to farm them.
// All numbers are BALANCE PLACEHOLDERS.

namespace ProjectBlock.Core
{
    /// <summary>"Domuz Kumbarası" - gains value at the end of every round survived.</summary>
    public sealed class DomuzKumbarasiJoker : Joker
    {
        public int ValuePerRound = 25;

        public DomuzKumbarasiJoker()
            : base("domuz_kumbarasi", "Domuz Kumbarası")
        {
            SetDescription(
                "Its sell value grows after every round you complete.",
                "Tamamladığın her raunt sonunda satış değeri artar.");
            BaseSellValue = 20;
        }

        public override void OnRoundEnded(RoundContext ctx, RoundOutcome outcome)
        {
            if (outcome == RoundOutcome.Advanced)
            {
                Accrue(ValuePerRound);
            }
        }
    }

    /// <summary>"Cimri Kumbara" - gains value every turn it is held.</summary>
    public sealed class CimriKumbaraJoker : Joker
    {
        public int ValuePerTurn = 3;

        public CimriKumbaraJoker()
            : base("cimri_kumbara", "Cimri Kumbara")
        {
            SetDescription(
                "Its sell value grows a little for every turn you hold it.",
                "Elde tutulduğu her turda satış değeri biraz artar.");
            BaseSellValue = 15;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            Accrue(ValuePerTurn);
        }
    }

    /// <summary>"Altın Kumbara" - banks on turns, clean sweeps and finished rounds.</summary>
    public sealed class AltinKumbaraJoker : Joker
    {
        public int ValuePerTurn = 2;
        public int ValuePerCleanSweep = 30;
        public int ValuePerRound = 20;

        public AltinKumbaraJoker()
            : base("altin_kumbara", "Altın Kumbara")
        {
            SetDescription(
                "Every turn, every clean sweep and every completed round grows its sell value.",
                "Her tur, her temizlik ve tamamladığın her raunt satış değerini artırır.");
            BaseSellValue = 30;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            Accrue(ValuePerTurn);
        }

        public override void AfterCleanSweep(TurnContext turn)
        {
            Accrue(ValuePerCleanSweep);
        }

        public override void OnRoundEnded(RoundContext ctx, RoundOutcome outcome)
        {
            if (outcome == RoundOutcome.Advanced)
            {
                Accrue(ValuePerRound);
            }
        }
    }
}
