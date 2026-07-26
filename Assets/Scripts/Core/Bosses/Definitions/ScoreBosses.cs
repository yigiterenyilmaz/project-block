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

    /// <summary>
    /// "Enflasyon" - the bar will not hold still. Every turn you take, the score threshold rises
    /// 3%, compounding, so a round you drift through gets away from you: ten turns in it is a third
    /// higher than it started, twenty turns in it is nearly double.
    ///
    /// It is a pure THRESHOLD filter - it destroys nothing, takes nothing and pays nothing
    /// differently. The pressure is entirely on the clock: score fast or do not score at all.
    ///
    /// The rise is counted in turns TAKEN, so the first turn is already measured against a raised
    /// bar - the boss moves before the threshold check, which is what makes the pressure real.
    /// Read live off RoundEngine.ScoreThreshold, so the bar on screen is always the bar the rules
    /// use.
    ///
    /// The rate is a BALANCE PLACEHOLDER.
    /// </summary>
    public sealed class EnflasyonBoss : BossRound
    {
        /// <summary>How much the bar climbs per turn, in percent, compounding.</summary>
        public double PercentPerTurn = 3.0;

        private int turnsTaken;

        public EnflasyonBoss()
            : base("enflasyon", "Enflasyon")
        {
            SetDescription(
                "The score threshold rises 3% with every turn you take, compounding. Take your "
                    + "time and the bar runs away from you.",
                "Puan eşiği attığın her turda %3 yükselir, bileşik olarak. Oyalanırsan eşik "
                    + "senden kaçar.");
        }

        /// <summary>Turns taken so far, for the UI.</summary>
        public int TurnsTaken
        {
            get { return turnsTaken; }
        }

        public override string StatusText
        {
            get
            {
                if (turnsTaken == 0)
                {
                    return Loc.Pick("+3%/turn", "tur başına %3");
                }
                int percent = (int)System.Math.Round((Multiplier - 1.0) * 100.0);
                return "+" + percent + "%";
            }
        }

        private double Multiplier
        {
            get { return System.Math.Pow(1.0 + PercentPerTurn / 100.0, turnsTaken); }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            turnsTaken = 0;
        }

        /// <summary>Rounded UP, so the bar always actually moves - a small threshold must not be
        /// immune to inflation. Capped, because compounding has no natural ceiling: a round that
        /// drags on for hundreds of turns would otherwise inflate the bar past what an int can
        /// hold once RoundEngine scales it, and an overflowed threshold is a bar of nonsense
        /// rather than a hard one. The cap is far beyond reachable either way.</summary>
        public override int FilterScoreThreshold(int threshold)
        {
            double inflated = System.Math.Ceiling(threshold * Multiplier);
            return inflated > MaxThreshold ? MaxThreshold : (int)inflated;
        }

        /// <summary>Ceiling on the inflated bar, low enough that scaling it cannot overflow.</summary>
        private const int MaxThreshold = 10000000;

        public override void AfterTurnScored(TurnContext turn)
        {
            turnsTaken++;
        }
    }

    /// <summary>
    /// "Hiçlik" - the board itself bills you. At the end of every turn you lose score for every
    /// cube left standing, so a board you let fill up bleeds you dry while it sits there.
    ///
    /// GOLD IS NOT EXEMPT (confirmed design): a gold cube still pays its upkeep bonus, and still
    /// costs its rent here. It earns and it bleeds at the same time, which is exactly what makes
    /// leaving one lying around a decision rather than a free win.
    ///
    /// The bill lands through AddLateTurnScore, AFTER the turn's score is finalized, so the
    /// central round-score clamp (turn step 8.6) covers it: a turn may be emptied by this, never
    /// pushed below where it started. A big board cannot take back score you already banked.
    ///
    /// The rate is a BALANCE PLACEHOLDER.
    /// </summary>
    public sealed class HiclikBoss : BossRound
    {
        /// <summary>Score lost per cube left standing at the end of a turn.</summary>
        public int CostPerCube = 4;

        private int billedThisRound;

        public HiclikBoss()
            : base("hiclik", "Hiçlik")
        {
            SetDescription(
                "At the end of every turn you lose score for every cube still standing on the "
                    + "board. Gold cubes pay their bonus AND their rent - they bleed you like all "
                    + "the rest.",
                "Her tur sonunda tahtada duran her küp için puan kaybedersin. Altın küpler "
                    + "bonusunu da verir kirasını da alır - onlar da diğerleri gibi kanatır.");
        }

        /// <summary>Total billed this round, for the UI.</summary>
        public int BilledThisRound
        {
            get { return billedThisRound; }
        }

        public override string StatusText
        {
            get
            {
                return billedThisRound > 0
                    ? "-" + billedThisRound
                    : Loc.Pick("-" + CostPerCube + "/cube", "küp başına -" + CostPerCube);
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            billedThisRound = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            // The MAIN board only: "Öteki dünya" opening a second arena must not double the rent
            // on a boss that was balanced against one.
            int standing = turn.Round.MainBoard.OccupiedCount;
            if (standing <= 0)
            {
                return;
            }
            int bill = standing * CostPerCube;
            billedThisRound += bill;
            turn.Round.AddLateTurnScore(-bill, DefId);
        }
    }
}
