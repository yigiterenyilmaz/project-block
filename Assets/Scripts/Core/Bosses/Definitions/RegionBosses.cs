// PURPOSE: "Dört kutup" - the boss that takes away WHERE you may play. The arena is cut into four
// quadrants and only one of them accepts blocks at a time; the live quadrant turns clockwise every
// turn, so the whole round is played one corner at a time, in a fixed rhythm you can plan around
// but never escape.
//
// THREE THINGS MAKE IT WORK, and each reuses machinery that was already there:
//
//  1. AN EVEN BOARD. Four equal quadrants need an even edge, so this is the one boss that reshapes
//     the round: 5x5 becomes 6x6, 7x7 becomes 8x8, 9x9 becomes 10x10. That happens through
//     BossRound.FilterRoundConfig, before the engine builds the board, via RoundConfig.WithBoard -
//     never a hand-written new RoundConfig.
//
//  2. THE SEALS. The three sleeping quadrants are SEALED, which is exactly the "empty but
//     unplaceable" state Mapus already introduced, and which GameBoard.CanPlace already honours -
//     so placement, the valid-origin list, the UI preview and the dead-end check all agree without
//     a single extra check anywhere. Note the consequence, which is deliberate: a sealed cell is
//     still an EMPTY cell of its row and column, so a line can only be completed when the cells it
//     is missing all sit in the live quadrant.
//
//  3. THE FORCED PASS. If nothing you hold fits the live quadrant, the round must NOT die on it -
//     the rest of the arena might be wide open. TryEscapeDeadEnd turns to the next quadrant and
//     bills you for the turn you could not use. The engine re-checks and may ask again, so the boss
//     will work through all four before a genuinely stuck board is a real dead end.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Dört kutup" - only one quadrant of the board accepts blocks, and it rotates.</summary>
    public sealed class DortKutupBoss : BossRound
    {
        /// <summary>Score lost when the live quadrant has no room for anything held, and the boss
        /// has to turn early. The turn is gone either way; this is what it costs.</summary>
        public int BlockedTurnPenalty = 30;

        /// <summary>0 = bottom-left, 1 = top-left, 2 = top-right, 3 = bottom-right. Turning
        /// CLOCKWISE means walking that order, which is why it is written this way round.</summary>
        private int activeQuadrant;

        private int blockedTurns;

        public DortKutupBoss()
            : base("dort_kutup", "Dört Kutup")
        {
            SetDescription(
                "The arena is squared off and cut into four. Only ONE quadrant takes blocks, and "
                    + "it turns clockwise every turn - so a line only completes when the cells it "
                    + "is missing are all in the live quarter.",
                "Oyun alanı çift sayıya tamamlanıp dörde bölünür. Sadece BİR bölge blok kabul "
                    + "eder ve her tur saat yönünde döner - yani bir hat, ancak eksik kareleri "
                    + "aktif bölgede kaldığında tamamlanabilir.");
        }

        /// <summary>Which quarter is live right now, for the UI.</summary>
        public int ActiveQuadrant
        {
            get { return activeQuadrant; }
        }

        /// <summary>Turns the player could not use because the live quarter was full.</summary>
        public int BlockedTurns
        {
            get { return blockedTurns; }
        }

        public override string StatusText
        {
            get
            {
                string where = QuadrantName(activeQuadrant);
                return blockedTurns > 0
                    ? where + " · " + blockedTurns + Loc.Pick(" wasted", " boşa")
                    : where;
            }
        }

        private static string QuadrantName(int quadrant)
        {
            switch (quadrant)
            {
                case 0: return Loc.Pick("bottom-left", "sol alt");
                case 1: return Loc.Pick("top-left", "sol üst");
                case 2: return Loc.Pick("top-right", "sağ üst");
                default: return Loc.Pick("bottom-right", "sağ alt");
            }
        }

        /// <summary>Rounds the arena UP to an even edge, so it splits into four equal quarters.
        /// Never down: the boss makes the board harder to use, not smaller.</summary>
        public override RoundConfig FilterRoundConfig(RoundConfig config)
        {
            int width = config.BoardWidth + (config.BoardWidth % 2);
            int height = config.BoardHeight + (config.BoardHeight % 2);
            if (width == config.BoardWidth && height == config.BoardHeight)
            {
                return config;
            }
            return config.WithBoard(width, height, config.ExtraPlayableCells);
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            activeQuadrant = 0;
            blockedTurns = 0;
            ApplySeals(ctx.Round);
        }

        /// <summary>The boss moves last: the quarter turns at the END of a turn, so the player
        /// always sees which one they are about to play into.</summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            Turn(turn.Round);
        }

        /// <summary>Nothing fits the live quarter. Turning to the next one is the fix, and it costs
        /// the player - the turn they could not use is not free. Always returns true: the engine
        /// decides whether it helped, and asks again if not.</summary>
        public override bool TryEscapeDeadEnd(RoundContext ctx)
        {
            blockedTurns++;
            ctx.Round.ChargeScore(BlockedTurnPenalty, DefId);
            Turn(ctx.Round);
            return true;
        }

        private void Turn(RoundEngine round)
        {
            activeQuadrant = (activeQuadrant + 1) % 4;
            ApplySeals(round);
        }

        /// <summary>Seals everything outside the live quarter, on the MAIN board. Re-done from
        /// scratch every turn, exactly as Mapus re-picks its cell, so the seals can never drift out
        /// of step with which quarter is live.</summary>
        private void ApplySeals(RoundEngine round)
        {
            if (round == null)
            {
                return;
            }
            round.ClearBoardSeals();
            GameBoard board = round.MainBoard;
            int midX = board.MinX + board.Width / 2;
            int midY = board.MinY + board.Height / 2;
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                for (int y = board.MinY; y < board.MinY + board.Height; y++)
                {
                    var cell = new GridPos(x, y);
                    if (board.IsInside(cell) && QuadrantOf(cell, midX, midY) != activeQuadrant)
                    {
                        round.SealBoardCell(cell);
                    }
                }
            }
        }

        /// <summary>Which quarter a cell belongs to, in the clockwise order the rotation walks.</summary>
        private static int QuadrantOf(GridPos cell, int midX, int midY)
        {
            bool right = cell.X >= midX;
            bool top = cell.Y >= midY;
            if (!right)
            {
                return top ? 1 : 0; // left column of quarters: bottom-left, then top-left
            }
            return top ? 2 : 3; // right column: top-right, then bottom-right
        }

        /// <summary>The cells of the live quarter, for the UI to light up.</summary>
        public List<GridPos> ActiveCells(GameBoard board)
        {
            var cells = new List<GridPos>();
            if (board == null)
            {
                return cells;
            }
            int midX = board.MinX + board.Width / 2;
            int midY = board.MinY + board.Height / 2;
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                for (int y = board.MinY; y < board.MinY + board.Height; y++)
                {
                    var cell = new GridPos(x, y);
                    if (board.IsInside(cell) && QuadrantOf(cell, midX, midY) == activeQuadrant)
                    {
                        cells.Add(cell);
                    }
                }
            }
            return cells;
        }
    }

    /// <summary>
    /// "Kangren" - the arena rots. Every turn the infection takes one more cell, growing outward
    /// from wherever it started, and every gangrene cube standing on the board costs you score for
    /// as long as it is there. Cutting it out is not optional.
    ///
    /// A gangrene cube is an ORDINARY cube: it breaks with a completed line like anything else, so
    /// three of them in a row of five still leave a line you can finish and blow up. The danger is
    /// the line it takes ENTIRELY - that line dies for the rest of the round, can never explode
    /// again, and the infection JUMPS to the board's nearest edge line and turns every cube standing
    /// there. Which can kill that line too. Let it reach a whole row and the rot starts eating the
    /// arena from the outside in.
    ///
    /// All of the geometry lives in GameBoard.Gangrene - this class decides WHEN, and what it costs.
    ///
    /// The rate and the rent are BALANCE PLACEHOLDERS.
    /// </summary>
    public sealed class KangrenBoss : BossRound
    {
        /// <summary>Score lost per gangrene cube standing at the end of a turn.</summary>
        public int RentPerCube = 5;

        private int deadLines;
        private int billedThisRound;

        public KangrenBoss()
            : base("kangren", "Kangren")
        {
            SetDescription(
                "Every turn the rot takes one more cell, and every rotten cube costs you score "
                    + "while it stands. Rotten cubes still explode - but a row or column the rot "
                    + "takes WHOLE dies for good, and the infection jumps to the nearest edge.",
                "Her tur kangren bir kare daha ele geçirir ve tahtada duran her kangren küpü "
                    + "durduğu sürece puan yer. Kangren küpleri patlatılabilir - ama kangrenin "
                    + "TAMAMEN ele geçirdiği satır ya da sütun kalıcı olarak ölür ve enfeksiyon "
                    + "en yakın kenara atlar.");
        }

        /// <summary>Lines the infection has killed this round, for the UI.</summary>
        public int DeadLines
        {
            get { return deadLines; }
        }

        public override string StatusText
        {
            get
            {
                if (deadLines > 0)
                {
                    return deadLines + Loc.Pick(" dead lines", " ölü hat");
                }
                return billedThisRound > 0
                    ? "-" + billedThisRound
                    : Loc.Pick("spreading", "yayılıyor");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            deadLines = 0;
            billedThisRound = 0;
        }

        /// <summary>The boss moves last: the rot spreads after the player's turn has resolved, so
        /// what they built counted before it was eaten - and the rent is charged on the board as it
        /// stands AFTER the spread, so a fresh cube pays from its first turn.</summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            RoundEngine round = turn.Round;
            round.SpreadGangrene();
            deadLines = round.MainBoard.InfectionDeadRows.Count
                + round.MainBoard.InfectionDeadColumns.Count;
            int standing = round.CountGangrene();
            if (standing <= 0)
            {
                return;
            }
            int rent = standing * RentPerCube;
            billedThisRound += rent;
            round.ChargeScore(rent, DefId);
        }
    }
}
