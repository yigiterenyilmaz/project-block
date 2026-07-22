// PURPOSE: "Meydan Okuma" - the dare joker. A few blocks into a round it marks one row or
// column and challenges the player to clear it within a deadline for a bonus. Miss it and
// it re-marks somewhere else for half the bonus; miss that and it halves again; miss the
// third and it gives up for the round. Land any one of them and it is done for the round.
//
// The whole event happens once per round: once it pays out OR runs out of attempts, it
// stays quiet until the next round.
//
// The marked line is tracked as a 0-BASED row/column index, the same space TurnReport uses
// for ExplodedRows / ExplodedColumns, so "did the marked line explode" is a direct lookup.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Meydan Okuma" - marks a line and dares the player to clear it for a bonus.</summary>
    public sealed class MeydanOkumaJoker : Joker
    {
        /// <summary>The mark is not laid until the board has this many turns of blocks on it,
        /// so the player is not asked to fill a line on an empty board.</summary>
        public int ArmAfterTurns = 3;

        /// <summary>Bonus for clearing the FIRST mark. Halves on every miss.</summary>
        public int BaseBonus = 150;

        /// <summary>Floor for the deadline, in turns: max(3, empty cell count).</summary>
        public int MinDeadline = 3;

        private const int MaxAttempts = 3;

        private bool resolved;         // paid out or ran out of attempts this round
        private int attemptsMade;      // marks laid so far (1..3)
        private int currentBonus;
        private int turnsLeft;
        private bool markIsRow;
        private int markedLine;        // 0-based row (Y) or column (X) index
        private bool hasMark;

        public MeydanOkumaJoker()
            : base("meydan_okuma", "Meydan Okuma")
        {
            SetDescription(
                "A few turns in, it dares you to clear a marked row or column within a "
                    + "deadline for a bonus. Miss it and it moves and halves the bonus, up to "
                    + "three tries; clear any one and it is done for the round.",
                "Birkaç tur sonra işaretlediği bir satırı ya da sütunu süre dolmadan "
                    + "patlatman için bonus vaat eder. Tutturamazsan yer değiştirip bonusu "
                    + "yarıya böler, en fazla üç deneme; birini tutturursan o raunt biter.");
            BaseSellValue = 55;
        }

        /// <summary>True once the event is over for the round (paid out or three misses).</summary>
        public bool IsResolved
        {
            get { return resolved; }
        }

        /// <summary>Marked line, for the UI to highlight. Null when nothing is challenged.</summary>
        public bool HasActiveMark
        {
            get { return hasMark && !resolved; }
        }

        public bool MarkIsRow
        {
            get { return markIsRow; }
        }

        public int MarkedLine
        {
            get { return markedLine; }
        }

        public int TurnsLeft
        {
            get { return turnsLeft; }
        }

        public int CurrentBonus
        {
            get { return currentBonus; }
        }

        public override string StatusText
        {
            get
            {
                if (resolved)
                {
                    return Loc.Pick("done", "bitti");
                }
                if (!hasMark)
                {
                    return Loc.Pick("waiting", "bekliyor");
                }
                string what = markIsRow ? Loc.Pick("row", "satır") : Loc.Pick("col", "sütun");
                return what + " " + markedLine + " · " + turnsLeft + Loc.Pick("t", "t");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            resolved = false;
            attemptsMade = 0;
            hasMark = false;
            currentBonus = 0;
            turnsLeft = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (resolved)
            {
                return;
            }
            if (!hasMark)
            {
                // Lay the first mark once enough blocks are down.
                if (turn.Round.TurnNumber >= ArmAfterTurns)
                {
                    currentBonus = BaseBonus;
                    LayMark(turn);
                }
                return;
            }

            // The mark is live: did the player clear it this turn?
            if (MarkedLineExploded(turn.Report))
            {
                turn.AddFlatScore(currentBonus, DefId);
                resolved = true;
                hasMark = false;
                return;
            }

            // Not this turn - the deadline ticks.
            turnsLeft--;
            if (turnsLeft > 0)
            {
                return;
            }
            // Missed. Re-mark for half, or give up after the third attempt.
            if (attemptsMade >= MaxAttempts)
            {
                resolved = true;
                hasMark = false;
                return;
            }
            currentBonus /= 2;
            LayMark(turn);
        }

        /// <summary>Marks a random row or column that has playable cells, and refreshes the
        /// deadline from the CURRENT empty-cell count - so a fuller board gives less time.</summary>
        private void LayMark(TurnContext turn)
        {
            GameBoard board = turn.Round.Board;
            var rows = new List<int>();
            var cols = new List<int>();
            for (int y = 0; y < board.Height; y++)
            {
                if (LineHasPlayableCell(board, false, y))
                {
                    rows.Add(y);
                }
            }
            for (int x = 0; x < board.Width; x++)
            {
                if (LineHasPlayableCell(board, true, x))
                {
                    cols.Add(x);
                }
            }
            if (rows.Count == 0 && cols.Count == 0)
            {
                return; // degenerate board - nothing to mark, try again next turn
            }

            // Pick an axis that actually has a line, then a line on it.
            bool pickRow = cols.Count == 0 || (rows.Count > 0 && turn.Rng.NextInt(0, 2) == 0);
            List<int> lines = pickRow ? rows : cols;
            markIsRow = pickRow;
            markedLine = lines[turn.Rng.NextInt(0, lines.Count)];
            hasMark = true;
            attemptsMade++;

            int empty = board.PlayableCellCount - board.OccupiedCount;
            turnsLeft = empty > MinDeadline ? empty : MinDeadline;
        }

        private bool MarkedLineExploded(TurnReport report)
        {
            IReadOnlyList<int> exploded = markIsRow ? report.ExplodedRows : report.ExplodedColumns;
            for (int i = 0; i < exploded.Count; i++)
            {
                if (exploded[i] == markedLine)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool LineHasPlayableCell(GameBoard board, bool column, int index)
        {
            if (column)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (board.IsInside(new GridPos(index + board.MinX, y + board.MinY)))
                    {
                        return true;
                    }
                }
                return false;
            }
            for (int x = 0; x < board.Width; x++)
            {
                if (board.IsInside(new GridPos(x + board.MinX, index + board.MinY)))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
