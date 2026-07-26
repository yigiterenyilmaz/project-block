// PURPOSE: "Kaçakçı" - one item per market visit, free. The catch is that free goods come off the
// back of a lorry, and roughly half of them are defective.
//
// WHY IT HAS NO HOOKS. Smuggling is a SESSION rule, exactly like the market credit "Kredi kartı"
// turns on: GameSession owns the taking (TrySmuggleOffer), the roll and the spoiling, and this
// joker is only the switch that turns it on and names the odds. Its three answers:
//   EnablesSmuggling            -> there is a free item to take this visit
//   SmuggleDefectChancePercent  -> how often the goods are junk
//   SmuggledPowerRechargeCost   -> how slowly a defective power fills
// So no joker anywhere has to know what smuggling is, and the defects are enforced centrally:
// JokerInventory.IsGated silences a broken joker, and Power.Recharge counts a broken power's
// charge events. Nothing is added, removed or undone.
//
// WHAT DEFECTIVE MEANS, per kind of goods:
//   BLOCK    -> it looks completely ordinary, and it will not stay on the board: you place it
//               legally, it drops straight through the arena and off the screen, and nothing
//               lands. No score, no line, no sweep - the turn is simply gone. It is in your deck
//               for the run, so it costs you that turn again every time it comes back round.
//   JOKER    -> either dead in every BOSS round, or dead outright. You keep it either way.
//   POWER    -> it arrives EMPTY and fills at a quarter of the rate.
//
// You find out immediately - the defect is on the card, on the joker's status line, on the power's
// meter. There is no hidden state here: hidden state reads as a bug, and the gamble is already in
// the taking, not in the finding out.
//
// All numbers are BALANCE PLACEHOLDERS.

namespace ProjectBlock.Core
{
    /// <summary>"Kaçakçı" - one free market item per visit, and it may well be defective.</summary>
    public sealed class KacakciJoker : Joker
    {
        /// <summary>Chance the goods are junk, in percent.</summary>
        public int DefectChancePercent = 50;

        /// <summary>Recharge events a defective smuggled power needs instead of one.</summary>
        public int BrokenPowerRechargeCost = 4;

        public KacakciJoker()
            : base("kacakci", "Kaçakçı")
        {
            SetDescription(
                "Take ONE market item per visit for free - but smuggled goods are defective about "
                    + "half the time. A defective block falls straight through the board and off "
                    + "the screen, wasting the turn; a broken joker is dead in boss rounds or dead "
                    + "outright; a broken power arrives empty and fills four times slower. You "
                    + "keep whatever you took.",
                "Her market ziyaretinde BİR ürünü bedavaya al - ama kaçak malın yarısı defolu "
                    + "çıkar. Defolu blok tahtaya tutunmaz, aşağı düşüp ekrandan çıkar ve turu "
                    + "boşa harcarsın; defolu joker patron rauntlarında ya da hiçbir zaman "
                    + "çalışmaz; defolu güç boş gelir ve dört kat yavaş dolar. Aldığın senin kalır.");
        }

        /// <summary>Free goods this visit are available while it is held (and not itself broken -
        /// JokerInventory checks that, not this).</summary>
        public override bool EnablesSmuggling
        {
            get { return true; }
        }

        public override int SmuggleDefectChancePercent
        {
            get { return DefectChancePercent; }
        }

        public override int SmuggledPowerRechargeCost
        {
            get { return BrokenPowerRechargeCost; }
        }

        public override string StatusText
        {
            get
            {
                return Loc.Pick(DefectChancePercent + "% junk",
                    "%" + DefectChancePercent + " defolu");
            }
        }
    }
}
