// PURPOSE: "Çığ" - the avalanche power. It used to be a streak JOKER (bigger block than last
// turn); the designer re-cut it on 2026-10-02 as the power that goes with the new SNOW block.
//
// CONFIRMED RULES (see GameBoard.Snow for the snow itself):
//  - the player picks a LINE across the arena's gravity - a row, while it points down - by
//    clicking any cell of it;
//  - every snow heap on that line comes down as many cells as it has POWER, the whole heap by
//    the same amount, and leaves its line;
//  - whatever is in the way is crushed and PAYS, gold and obsidian included;
//  - the snow it lays is PACKED: one power a cell, melting in SnowRules.AvalancheMeltTurns, and
//    its layers never merge back into one another. A packed layer cannot slide again until fresh
//    snow has landed on it.
//
// The number is a BALANCE PLACEHOLDER.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Çığ" - sends the snow heaps of one line down over what lies under them.</summary>
    public sealed class CigPower : Power
    {
        /// <summary>Points for each cube the avalanche crushes. A named reward of the power, so
        /// it pays whether or not "Genel temizlik" is held. Well above a line's own per-cube
        /// value on purpose: the heap took turns to build and melts if it is not used.</summary>
        public int PointsPerCrushedCube = 12;

        public CigPower()
            : base("cig", "Çığ")
        {
            SetEnglishName("Avalanche");
            SetDescription(
                "Pick a row: every snow heap on it slides down as many rows as it has power, "
                    + "crushing what is under it for points. The snow it leaves melts in 3 turns.",
                "Bir satır seç: o satırdaki her kar öbeği power'ı kadar satır aşağı iner ve "
                    + "altında kalan blokları ezip puan kazandırır. Bıraktığı kar 3 turda erir.");
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.BoardCell; }
        }

        /// <summary>The last avalanche, for the View to play once (matched by identity).
        /// Reporting only.</summary>
        [field: NotSaved]
        public AvalancheVisuals LastAvalanche { get; private set; }

        /// <summary>Display only: what the last bare usability check found, so the bar can say
        /// why the power has nothing to do.</summary>
        [NotSaved]
        private bool noSnow;

        public override string StatusText
        {
            get { return noSnow ? Loc.Pick("no snow to slide", "kayacak kar yok") : string.Empty; }
        }

        /// <summary>With a cell: is there a heap on that line that can come down at all? Without
        /// one (the bar asking whether the power has anything to do): is there such a line
        /// anywhere on the board?</summary>
        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            RoundEngine round = ctx.Round;
            if (target.Cell.HasValue)
            {
                return round.Board.IsInside(target.Cell.Value)
                    && round.PlanAvalanche(target.Cell.Value).Any;
            }
            noSnow = !AnyLineCanSlide(round.Board);
            return !noSnow;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            if (!target.Cell.HasValue)
            {
                return false;
            }
            AvalancheVisuals done = ctx.Round.TriggerAvalanche(target.Cell.Value, PointsPerCrushedCube);
            if (done == null)
            {
                return false; // nothing on that line can slide: keep the charge
            }
            LastAvalanche = done;
            return true;
        }

        /// <summary>The cells the avalanche would cover for this aim - what the preview lights.
        /// Empty when the line holds nothing that can slide. Asked of a BOARD, which a bare
        /// target does not carry (Power.PreviewCells cannot answer this one).</summary>
        public static IReadOnlyList<GridPos> PreviewOn(GameBoard board, GridPos cell)
        {
            if (board == null || !board.IsInside(cell))
            {
                return System.Array.Empty<GridPos>();
            }
            return board.PlanAvalanche(cell).CoveredCells();
        }

        private static bool AnyLineCanSlide(GameBoard board)
        {
            // One cell of every row and every column is enough to ask about every line, whichever
            // way the arena's gravity points.
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                if (board.PlanAvalanche(new GridPos(x, board.MinY)).Any)
                {
                    return true;
                }
            }
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                if (board.PlanAvalanche(new GridPos(board.MinX, y)).Any)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
