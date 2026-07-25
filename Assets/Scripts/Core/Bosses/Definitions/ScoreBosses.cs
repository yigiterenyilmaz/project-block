// PURPOSE: The three score bosses. "Ufuk" pays for rows only, "Kule" for columns only, and
// "Titizlik" for nothing but a clean sweep. All three are pure score filters: they destroy
// nothing, take nothing, and only rewrite what an action is worth.
//
// They rewrite the BASE values only. A joker's own bonuses still land on top, exactly as they
// do on an ordinary round - these bosses beat your board, not your build.
//
// The bonus multipliers are BALANCE PLACEHOLDERS (public fields, tune freely).

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Ufuk" - only horizontal clears pay, and they pay a little more.</summary>
    public sealed class UfukBoss : BossRound
    {
        /// <summary>What a scoring row is worth relative to normal.</summary>
        public double RowBonus = 1.35;

        public UfukBoss()
            : base("ufuk", "Ufuk")
        {
            SetDescription(
                "Only horizontal clears score - a column that explodes pays nothing. Rows pay "
                    + "a little more than usual.",
                "Sadece yatay patlamalar puan verir - patlayan bir sütun hiçbir şey ödemez. "
                    + "Satırlar normalden biraz fazla puan getirir.");
        }

        public override int ScoreLineExplosion(IScoreCalculator scorer, LineExplosionScore lines)
        {
            if (lines.Rows <= 0)
            {
                return 0; // a columns-only clear earns nothing at all
            }
            // Priced as if the rows had exploded alone: their own lines, their own cubes.
            return (int)(scorer.ScoreLineExplosion(lines.Rows, lines.RowCubes) * RowBonus);
        }
    }

    /// <summary>"Kule" - only vertical clears pay, and they pay a little more.</summary>
    public sealed class KuleBoss : BossRound
    {
        /// <summary>What a scoring column is worth relative to normal.</summary>
        public double ColumnBonus = 1.35;

        public KuleBoss()
            : base("kule", "Kule")
        {
            SetDescription(
                "Only vertical clears score - a row that explodes pays nothing. Columns pay "
                    + "a little more than usual.",
                "Sadece dikey patlamalar puan verir - patlayan bir satır hiçbir şey ödemez. "
                    + "Sütunlar normalden biraz fazla puan getirir.");
        }

        public override int ScoreLineExplosion(IScoreCalculator scorer, LineExplosionScore lines)
        {
            if (lines.Columns <= 0)
            {
                return 0; // a rows-only clear earns nothing at all
            }
            return (int)(scorer.ScoreLineExplosion(lines.Columns, lines.ColumnCubes) * ColumnBonus);
        }
    }

    /// <summary>
    /// "Titizlik" - nothing is good enough but a spotless board. Placing blocks, clearing lines,
    /// the combo and the gold upkeep all pay nothing; only a CLEAN SWEEP scores, and it pays a
    /// little more than usual to make up for it.
    ///
    /// It kills the line score through ScoreLineExplosion rather than leaving it to the engine's
    /// wipe, so a clear that lands OUTSIDE a placement (an inflation deflate, a board power) is
    /// silenced by the same rule - a line is a line, whoever completed it.
    /// </summary>
    public sealed class TitizlikBoss : BossRound
    {
        /// <summary>What a clean sweep is worth relative to normal.</summary>
        public double SweepBonus = 1.2;

        public TitizlikBoss()
            : base("titizlik", "Titizlik")
        {
            SetDescription(
                "Only a clean sweep scores - placing, clearing lines, combos and gold all pay "
                    + "nothing. Sweeps pay a little more than usual.",
                "Sadece temizlik puan verir - blok koymak, satır patlatmak, kombo ve altın hiçbir "
                    + "şey ödemez. Temizlikler normalden biraz fazla puan getirir.");
        }

        public override bool OnlyCleanSweepsScore
        {
            get { return true; }
        }

        public override int ScoreLineExplosion(IScoreCalculator scorer, LineExplosionScore lines)
        {
            return 0;
        }

        public override int ScoreCleanSweep(IScoreCalculator scorer)
        {
            return (int)(scorer.ScoreCleanSweep() * SweepBonus);
        }
    }

    /// <summary>
    /// "Karantina" - the arena is sealed off a ring at a time. Every few turns two more of the
    /// OUTERMOST lines not yet quarantined are marked, in one of three shapes: a row and a
    /// column, two rows, or two columns.
    ///
    /// A cube that explodes while standing in a quarantined line does not merely fail to pay -
    /// it LOSES exactly what it would have earned. Only those cubes: a five-cube row clear with
    /// two of them inside a zone still pays full price for the other three, so a clear that
    /// clips a zone is a trade rather than a disaster.
    ///
    /// The zones ACCUMULATE and work inward: the rim first, then the ring behind it, and so on,
    /// until there is barely a safe square left. That is the clock this boss runs on.
    ///
    /// Lines are held in ABSOLUTE board coordinates, so a line that erosion later carries off
    /// the board simply stops matching, exactly as it should - that row is gone.
    /// </summary>
    public sealed class KarantinaBoss : BossRound
    {
        /// <summary>Turns between one sealing and the next.</summary>
        public int SealEveryTurns = 4;

        /// <summary>Lines sealed each time.</summary>
        public int LinesPerSealing = 2;

        private readonly List<int> rows = new List<int>();
        private readonly List<int> columns = new List<int>();
        private int turnsSinceSealing;

        public KarantinaBoss()
            : base("karantina", "Karantina")
        {
            SetDescription(
                "Every 4 turns two more of the outermost rows or columns are quarantined, "
                    + "working inward. A cube that explodes inside a zone loses exactly what it "
                    + "would have earned - the cubes outside still pay in full.",
                "Her 4 turda en dıştaki iki satır ya da sütun daha karantinaya alınır ve "
                    + "içeri doğru ilerler. Karantinada patlayan küp, kazandıracağı kadar "
                    + "kaybettirir - dışarıdaki küpler tam puanını vermeye devam eder.");
        }

        /// <summary>Quarantined rows and columns, in ABSOLUTE board coordinates, for the UI.</summary>
        public IReadOnlyList<int> QuarantinedRows
        {
            get { return rows; }
        }

        public IReadOnlyList<int> QuarantinedColumns
        {
            get { return columns; }
        }

        public override string StatusText
        {
            get
            {
                int sealed_ = rows.Count + columns.Count;
                return sealed_ > 0
                    ? sealed_ + Loc.Pick(" lines sealed", " hat kapalı")
                    : Loc.Pick("clean", "temiz");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            rows.Clear();
            columns.Clear();
            turnsSinceSealing = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            turnsSinceSealing++;
            if (turnsSinceSealing < SealEveryTurns)
            {
                return;
            }
            turnsSinceSealing = 0;
            Seal(turn.Round.Board, turn.Rng);
        }

        /// <summary>Seals two more lines: a row and a column, two rows, or two columns, drawn
        /// from the outermost that are still clean. Falls back to whatever is left when one axis
        /// runs out, so the sealing never silently does nothing while lines remain.</summary>
        private void Seal(GameBoard board, IRandomSource rng)
        {
            int shape = rng.NextInt(0, 3); // 0 = row + column, 1 = two rows, 2 = two columns
            for (int taken = 0; taken < LinesPerSealing; taken++)
            {
                bool wantRow = shape == 1 || (shape == 0 && taken == 0);
                if (!TrySealLine(board, rng, wantRow) && !TrySealLine(board, rng, !wantRow))
                {
                    return; // the whole board is sealed - nothing left to take
                }
            }
        }

        /// <summary>Seals the outermost clean line on one axis, from whichever end the rng
        /// picks (falling back to the other end when that one is already sealed).</summary>
        private bool TrySealLine(GameBoard board, IRandomSource rng, bool row)
        {
            List<int> taken = row ? rows : columns;
            int min = row ? board.MinY : board.MinX;
            int count = row ? board.Height : board.Width;
            int low = -1;
            int high = -1;
            for (int i = 0; i < count; i++)
            {
                if (!taken.Contains(min + i)) { low = min + i; break; }
            }
            for (int i = count - 1; i >= 0; i--)
            {
                if (!taken.Contains(min + i)) { high = min + i; break; }
            }
            if (low < 0)
            {
                return false; // every line on this axis is already sealed
            }
            int pick = low == high ? low : (rng.NextInt(0, 2) == 0 ? low : high);
            taken.Add(pick);
            return true;
        }

        /// <summary>True if that cell stands in a quarantined row or column.</summary>
        public bool IsQuarantined(GridPos cell)
        {
            return rows.Contains(cell.Y) || columns.Contains(cell.X);
        }

        public override int AdjustExplosionScore(IScoreCalculator scorer,
            IReadOnlyList<GridPos> cells)
        {
            if (cells == null || (rows.Count == 0 && columns.Count == 0))
            {
                return 0;
            }
            int inside = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (IsQuarantined(cells[i]))
                {
                    inside++;
                }
            }
            if (inside == 0)
            {
                return 0;
            }
            // The normal price already paid for these cubes, so taking TWICE their value turns
            // that payment into a loss of the same size - "it costs what it would have earned".
            int perCube = scorer.ScoreLineExplosion(0, 1);
            return -2 * inside * perCube;
        }
    }
}
