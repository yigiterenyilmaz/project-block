// PURPOSE: The three score bosses. "Ufuk" pays for rows only, "Kule" for columns only, and
// "Titizlik" for nothing but a clean sweep. All three are pure score filters: they destroy
// nothing, take nothing, and only rewrite what an action is worth.
//
// They rewrite the BASE values only. A joker's own bonuses still land on top, exactly as they
// do on an ordinary round - these bosses beat your board, not your build.
//
// The bonus multipliers are BALANCE PLACEHOLDERS (public fields, tune freely).

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
}
