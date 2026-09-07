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
            // Priced as if the rows had exploded alone: their own lines, their own cubes, and
            // the obsidian standing on THEM - a stone in a cleared column pays nothing here,
            // for the same reason the column itself does not.
            return (int)((scorer.ScoreLineExplosion(lines.Rows, lines.RowCubes)
                + scorer.ScoreObsidianInLines(lines.RowObsidian)) * RowBonus);
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
            // Its own lines, its own cubes, and the obsidian standing in THEM (see Ufuk).
            return (int)((scorer.ScoreLineExplosion(lines.Columns, lines.ColumnCubes)
                + scorer.ScoreObsidianInLines(lines.ColumnObsidian)) * ColumnBonus);
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
    /// "Karantina" - a patch of the arena is sealed off, and it will not stay still. Every three
    /// turns the patch is LIFTED AND RELAID somewhere else, one cell larger than it was.
    ///
    /// A cube that explodes while standing in the patch does not merely fail to pay - it LOSES
    /// exactly what it would have earned. Only those cubes: a five-cube row clear with two of
    /// them inside the patch still pays full price for the other three, so a clear that clips
    /// the zone is a trade rather than a disaster.
    ///
    /// TWO RULES KEEP IT PLAYABLE, and both were learned the hard way. It used to seal whole
    /// ROWS AND COLUMNS, two of them every four turns, accumulating - and since a row and a
    /// column together poison a cross, three sealings covered two thirds of a 7x7 board and the
    /// round was simply over. So:
    ///   - it is CELLS, never lines, and it never covers more than HALF the playable board
    ///     (MaxCoverage). The other half is always somewhere to play;
    ///   - it MOVES rather than accumulates. Every relaying is a fresh set of cells, so no square
    ///     is lost for good and the board you learn is the board you have for three turns.
    /// Growth is one cell per relaying, which on a 7x7 board means the cap is a long way off -
    /// the pressure comes from the patch moving under your plans, not from running out of room.
    ///
    /// Cells are held in ABSOLUTE board coordinates, and a relaying only ever draws from cells
    /// that are on the board right now, so erosion cannot leave the zone hanging over nothing.
    /// </summary>
    public sealed class KarantinaBoss : BossRound
    {
        /// <summary>Turns between one relaying and the next.</summary>
        public int MoveEveryTurns = 3;

        /// <summary>Cells the zone covers when the round opens.</summary>
        public int StartingCells = 3;

        /// <summary>Cells the zone gains each time it is relaid.</summary>
        public int GrowPerMove = 1;

        /// <summary>The most of the playable board the zone may ever cover. Half, and that is a
        /// design promise rather than a balance knob: the player must always have as much board
        /// to work with as the boss has taken.</summary>
        public double MaxCoverage = 0.5;

        private readonly List<GridPos> cells = new List<GridPos>();
        private int size;
        private int turnsSinceMove;

        public KarantinaBoss()
            : base("karantina", "Karantina")
        {
            SetDescription(
                "A patch of the board is quarantined. Every 3 turns it moves somewhere else and "
                    + "grows by one cell, and it never covers more than half the arena. A cube "
                    + "that explodes inside it loses exactly what it would have earned - the "
                    + "cubes outside still pay in full.",
                "Alanın bir bölgesi karantinaya alınır. Her 3 turda bölge başka bir yere taşınır "
                    + "ve bir kare büyür; alanın yarısından fazlasını asla kaplamaz. Karantinada "
                    + "patlayan küp, kazandıracağı kadar kaybettirir - dışarıdaki küpler tam "
                    + "puanını vermeye devam eder.");
        }

        /// <summary>The quarantined cells, in ABSOLUTE board coordinates, for the UI.</summary>
        public IReadOnlyList<GridPos> QuarantinedCells
        {
            get { return cells; }
        }

        /// <summary>Turns until the zone moves again - what the badge counts down.</summary>
        public int TurnsUntilMove
        {
            get { return MoveEveryTurns - turnsSinceMove; }
        }

        public override string StatusText
        {
            get
            {
                if (cells.Count == 0)
                {
                    return Loc.Pick("clean", "temiz");
                }
                return Loc.Pick(
                    cells.Count + " cells sealed, moves in " + TurnsUntilMove,
                    cells.Count + " kare kapalı, " + TurnsUntilMove + " turda taşınır");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            cells.Clear();
            turnsSinceMove = 0;
            size = StartingCells;
            // Laid at once rather than after the first three turns: the zone is small to begin
            // with, and a boss whose whole rule only appears on turn four spends its opening
            // pretending to be an ordinary round.
            Relay(ctx.Round.Board, ctx.Rng);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            turnsSinceMove++;
            if (turnsSinceMove < MoveEveryTurns)
            {
                return;
            }
            turnsSinceMove = 0;
            // The floor covers a boss that was attached to a round already in progress (the
            // debug key, a test) and so never saw OnRoundStarted - it should still be the size
            // it was designed to be rather than a single cell.
            size = (size < StartingCells ? StartingCells : size) + GrowPerMove;
            Relay(turn.Round.Board, turn.Rng);
        }

        /// <summary>
        /// Picks a fresh set of cells. Every playable cell on the board is a candidate and the
        /// draw is without replacement (a partial Fisher-Yates over the candidate list), so the
        /// zone is scattered rather than clustered - a solid block would just be a smaller board,
        /// while scattered cells are something to place AROUND.
        /// </summary>
        private void Relay(GameBoard board, IRandomSource rng)
        {
            cells.Clear();
            if (board == null)
            {
                return;
            }
            var candidates = new List<GridPos>();
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                for (int x = board.MinX; x < board.MinX + board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (board.IsInside(cell))
                    {
                        candidates.Add(cell);
                    }
                }
            }
            int cap = (int)(candidates.Count * MaxCoverage);
            int take = size;
            if (take > cap) { take = cap; }
            if (take > candidates.Count) { take = candidates.Count; }
            for (int i = 0; i < take; i++)
            {
                int pick = i + rng.NextInt(0, candidates.Count - i);
                GridPos chosen = candidates[pick];
                candidates[pick] = candidates[i];
                candidates[i] = chosen;
                cells.Add(chosen);
            }
        }

        /// <summary>True if that cell stands in the quarantine.</summary>
        public bool IsQuarantined(GridPos cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].X == cell.X && cells[i].Y == cell.Y)
                {
                    return true;
                }
            }
            return false;
        }

        public override int AdjustExplosionScore(IScoreCalculator scorer,
            IReadOnlyList<GridPos> cells)
        {
            if (cells == null || this.cells.Count == 0)
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
