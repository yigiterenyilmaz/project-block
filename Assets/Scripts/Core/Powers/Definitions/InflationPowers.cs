// PURPOSE: The three inflation powers. They are the only things in the game that resize the
// board WHILE a round is running, which is why RoundEngine grew ReshapeBoard /
// ShrinkBoardPushingInward for them.
//
// CONFIRMED RULES:
//  - the board grows by one cell on each affected side, and holds that size for 3 turns;
//  - when it deflates, cubes standing in the vanishing bands are PUSHED INWARD to the
//    nearest free cell on their row/column. If the squeeze happens to complete a line, the
//    normal rules take over and it explodes - the engine resolves that right after the
//    shrink, so nothing special is needed here.
//
// COORDINATES DO NOT MOVE. Growing on the left or bottom pushes the board's origin into
// negative space rather than renumbering cells, so a cube at (2,3) is still at (2,3) while
// the board is inflated. "Kum saati" can therefore rewind across an inflation and an "eko"
// recorded before one still replays onto the right cells.
//
// All numbers are BALANCE PLACEHOLDERS.

namespace ProjectBlock.Core
{
    /// <summary>Shared body of the three inflation powers.</summary>
    public abstract class InflationPower : Power
    {
        protected InflationPower(string defId, string displayName, int horizontal, int vertical)
            : base(defId, displayName)
        {
            Horizontal = horizontal;
            Vertical = vertical;
        }

        /// <summary>Cells added to the left AND right.</summary>
        public int Horizontal { get; }

        /// <summary>Cells added to the bottom AND top.</summary>
        public int Vertical { get; }

        /// <summary>How long the board stays inflated.</summary>
        public int DurationTurns = 3;

        /// <summary>Turns left before the board snaps back, or 0 when not inflated.</summary>
        public int TurnsLeft { get; private set; }

        public bool IsInflated
        {
            get { return TurnsLeft > 0; }
        }

        public override string StatusText
        {
            get { return IsInflated ? TurnsLeft + " tur şişkin" : "hazır"; }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return !IsInflated;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            if (!ctx.Round.ReshapeBoard(Horizontal, Horizontal, Vertical, Vertical))
            {
                return false;
            }
            TurnsLeft = DurationTurns;
            return true;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (!IsInflated)
            {
                return;
            }
            TurnsLeft--;
            if (TurnsLeft > 0)
            {
                return;
            }
            // Deflate: the engine pushes the doomed bands inward and then lets the normal
            // line rules run on the tighter board.
            turn.Round.ShrinkBoardPushingInward(Horizontal, Horizontal, Vertical, Vertical);
        }

        /// <summary>A new round builds a fresh board at the normal size, so the inflation is
        /// simply forgotten rather than un-applied.</summary>
        public override void OnRoundStarted(RoundContext ctx)
        {
            TurnsLeft = 0;
        }
    }

    /// <summary>"Yatay enflasyon" - one extra column on the left and on the right.</summary>
    public sealed class YatayEnflasyonPower : InflationPower
    {
        public YatayEnflasyonPower()
            : base("yatay_enflasyon", "Yatay Enflasyon", 1, 0)
        {
            Description = "Oyun alanının genişliğini sağdan ve soldan birer artırır. "
                + "3 tur sürer, bitince bloklar içeri ittirilir.";
            BaseSellValue = 55;
        }
    }

    /// <summary>"Dikey enflasyon" - one extra row at the bottom and at the top.</summary>
    public sealed class DikeyEnflasyonPower : InflationPower
    {
        public DikeyEnflasyonPower()
            : base("dikey_enflasyon", "Dikey Enflasyon", 0, 1)
        {
            Description = "Oyun alanının yüksekliğini alttan ve üstten birer artırır. "
                + "3 tur sürer, bitince bloklar içeri ittirilir.";
            BaseSellValue = 55;
        }
    }

    /// <summary>"Hiper enflasyon" - both at once.</summary>
    public sealed class HiperEnflasyonPower : InflationPower
    {
        public HiperEnflasyonPower()
            : base("hiper_enflasyon", "Hiper Enflasyon", 1, 1)
        {
            Description = "Oyun alanını her yönden birer artırır. 3 tur sürer, "
                + "bitince bloklar içeri ittirilir.";
            BaseSellValue = 70;
        }
    }
}
