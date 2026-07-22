// PURPOSE: Powers that act on the board: Çaprazlama, Çerçeve, Bardağın boş tarafı, Mayın,
// eko, Buldozer.
// Every one of them destroys or rewrites cubes through RoundEngine, never through GameBoard
// directly, so the destruction log, the countable tally and the clean-sweep pre-condition
// all stay correct - the same rule the board jokers follow.
//
// A power that empties the board asks the engine for a sweep check; it never decides on its
// own that a temizlik happened.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Çaprazlama" - blows up a plus-shaped area around a chosen cell.</summary>
    public sealed class CaprazlamaPower : Power
    {
        /// <summary>How far each arm of the plus reaches from the centre.</summary>
        public int ArmLength = 2;

        public CaprazlamaPower()
            : base("caprazlama", "Çaprazlama")
        {
            SetDescription(
                "Blows up the blocks in a plus-shaped area around a chosen centre.",
                "Seçtiğin merkezden + şeklinde bir alandaki blokları patlatır.");
            BaseSellValue = 35;
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.BoardCell; }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return target.Cell.HasValue && ctx.Round.Board.IsInside(target.Cell.Value);
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            ctx.Round.DestroyCubes(PlusCells(target.Cell.Value), true);
            ctx.Round.TryResolveCleanSweep();
            return true;
        }

        public override IReadOnlyList<GridPos> PreviewCells(ActivationTarget target)
        {
            return target.Cell.HasValue ? PlusCells(target.Cell.Value) : System.Array.Empty<GridPos>();
        }

        private List<GridPos> PlusCells(GridPos centre)
        {
            var cells = new List<GridPos> { centre };
            for (int step = 1; step <= ArmLength; step++)
            {
                cells.Add(new GridPos(centre.X + step, centre.Y));
                cells.Add(new GridPos(centre.X - step, centre.Y));
                cells.Add(new GridPos(centre.X, centre.Y + step));
                cells.Add(new GridPos(centre.X, centre.Y - step));
            }
            return cells;
        }
    }

    /// <summary>"Çerçeve" - clears the outermost ring of the board.</summary>
    public sealed class CercevePower : Power
    {
        public CercevePower()
            : base("cerceve", "Çerçeve")
        {
            SetDescription(
                "Clears the blocks on the outermost ring of the board.",
                "Oyun alanının en dış katmanındaki blokları temizler.");
            BaseSellValue = 40;
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Round.Board.OccupiedCount > 0;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            GameBoard board = ctx.Round.Board;
            var edge = new List<GridPos>();
            foreach (GridPos cell in board.GetOccupiedCells())
            {
                if (board.IsOnEdge(cell))
                {
                    edge.Add(cell);
                }
            }
            if (edge.Count == 0)
            {
                return false; // nothing on the rim; do not waste the charge
            }
            ctx.Round.DestroyCubes(edge, true);
            ctx.Round.TryResolveCleanSweep();
            return true;
        }
    }

    /// <summary>"Bardağın boş tarafı" - inverts the board: every filled cell empties and
    /// every empty cell fills. The new cubes are plain, with no element.
    /// NO SAFETY NET, by design: inverting a nearly-empty board can bury the player, so the
    /// power is only worth using when the board is crowded. That is the tactical point.</summary>
    public sealed class BardaginBosTarafiPower : Power
    {
        public BardaginBosTarafiPower()
            : base("bardagin_bos_tarafi", "Bardağın Boş Tarafı")
        {
            SetDescription(
                "Filled and empty cells on the board swap places (new cubes carry no element). "
                    + "Any row or column the new cubes complete explodes.",
                "Oyun alanındaki dolu ve boş kareler yer değiştirir (yeni küpler elementsizdir). "
                    + "Yeni küplerin tamamladığı satır veya sütun patlar.");
            BaseSellValue = 45;
        }

        /// <summary>Refused on an empty board: there would be nothing to invert away, and the
        /// fill would just bury the player. It only makes sense with cubes on the board.</summary>
        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Round.Board.OccupiedCount > 0;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            GameBoard board = ctx.Round.Board;
            var filled = new List<GridPos>();
            var empty = new List<GridPos>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    if (board.GetCube(pos).HasValue)
                    {
                        filled.Add(pos);
                    }
                    else
                    {
                        empty.Add(pos);
                    }
                }
            }
            // Destroy through the engine so the swap counts like any other destruction,
            // then fill what used to be empty with plain cubes.
            ctx.Round.DestroyCubes(filled, true, true);
            foreach (GridPos pos in empty)
            {
                board.SetCubeAt(pos, new Cube(CubeKind.Normal, InvertedCubeCardId));
            }
            // The freshly filled cubes can complete rows/columns; those explode and score
            // like any other line, and an emptied board still offers a sweep check.
            ctx.Round.ResolveFullLinesOutsideTurn();
            ctx.Round.TryResolveCleanSweep();
            return true;
        }

        /// <summary>Source card id stamped on cubes conjured out of nothing. Negative so it
        /// can never collide with a real card and be mistaken for "that block exploded".</summary>
        public const int InvertedCubeCardId = -1;
    }

    /// <summary>
    /// "eko" - the first use starts listening; the next explosion is memorised; the use after
    /// that replays it. Memory is wiped at the start of every round.
    ///
    /// CONFIRMED READING: it replays the same CELLS, not the same line. Whatever sits on those
    /// coordinates when the echo fires is what goes up - even if only two of the six cells are
    /// occupied now. That way the power always does something.
    /// </summary>
    public sealed class EkoPower : Power
    {
        /// <summary>Points per cube the echo takes. The replay pays like a real explosion.</summary>
        public int PointsPerEchoedCube = 6;

        private readonly List<GridPos> memory = new List<GridPos>();
        private bool listening;

        public EkoPower()
            : base("eko", "Eko")
        {
            SetDescription(
                "The first use (free) memorises the next explosion; using it again replays "
                    + "it on the same squares. Memory resets every round.",
                "İlk kullanım (bedava) sonraki patlamayı hafızaya alır, tekrar kullandığında "
                    + "o patlamayı aynı karelerde tekrar eder. Hafıza her raunt sıfırlanır.");
            BaseSellValue = 50;
        }

        public bool HasMemory
        {
            get { return memory.Count > 0; }
        }

        public override string StatusText
        {
            get
            {
                if (HasMemory)
                {
                    return Loc.Pick(memory.Count + " squares ready", memory.Count + " kare hazır");
                }
                return listening ? Loc.Pick("listening", "dinliyor") : Loc.Pick("empty", "boş");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            memory.Clear();
            listening = false;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            if (!HasMemory)
            {
                listening = true; // arm: the next explosion gets recorded
                KeepChargeAfterUse = true; // memorising is free; only the replay costs a charge
                return true;
            }
            IReadOnlyList<GridPos> echoed = ctx.Round.DestroyCubes(memory, true);
            memory.Clear();
            if (echoed.Count > 0)
            {
                ctx.Round.AddScoreOutsideTurn(echoed.Count * PointsPerEchoedCube);
                ctx.Round.TryResolveCleanSweep();
            }
            return true;
        }

        /// <summary>Records the turn's destruction once, while armed. Reads the whole turn's
        /// log, so fire chains and dynamite are part of the echo too.</summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            if (!listening || HasMemory)
            {
                return;
            }
            IReadOnlyList<DestroyedCube> destroyed = turn.Report.DestroyedCubes;
            if (destroyed.Count == 0)
            {
                return;
            }
            for (int i = 0; i < destroyed.Count; i++)
            {
                memory.Add(destroyed[i].Pos);
            }
            listening = false;
        }
    }

    /// <summary>"Mayın" - pops one chosen cube. Dropped on an EMPTY cell it instead leaves a
    /// mine there, which detonates the cube that later lands on it.
    /// Using it costs no turn, like every power.</summary>
    public sealed class MayinPower : Power
    {
        public MayinPower()
            : base("mayin", "Mayın")
        {
            SetDescription(
                "Pops a chosen cube. Dropped on an empty cell it arms a mine that "
                    + "detonates the cube that lands on it.",
                "Seçtiğin küpü patlatır. Boş kareye koyarsan üstüne küp geldiğinde patlar.");
            BaseSellValue = 40;
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.BoardCell; }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return target.Cell.HasValue && ctx.Round.Board.IsInside(target.Cell.Value);
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            GridPos cell = target.Cell.Value;
            GameBoard board = ctx.Round.Board;
            if (board.GetCube(cell).HasValue)
            {
                ctx.Round.DestroyCubes(new[] { cell }, true);
                ctx.Round.TryResolveCleanSweep();
                return true;
            }
            // Empty cell: arm a mine. It behaves like the Kara delik void trap, except it
            // destroys the arriving cube rather than swallowing it silently.
            board.SetCubeAt(cell, new Cube(CubeKind.Mine, MineCardId));
            return true;
        }

        /// <summary>Source card id for armed mines - negative, so nothing mistakes a mine for
        /// part of a real block.</summary>
        public const int MineCardId = -2;
    }

    /// <summary>
    /// "Buldozer" - flattens a two-wide band: either two neighbouring rows or two
    /// neighbouring columns, picked at random. The player aims nothing; the machine decides.
    ///
    /// It CRUSHES EVERYTHING in the band, obsidian and gold included - a bulldozer does not
    /// care what a cube is made of, and it is the only way besides "elmas kazma" to shift
    /// obsidian at all.
    ///
    /// In exchange it is completely inert as far as the score economy is concerned: no
    /// points, nothing added to the "Kayıt defteri" ledger, and it can never trigger a clean
    /// sweep - not even by emptying the board. It buys space and nothing else.
    /// </summary>
    public sealed class BuldozerPower : Power
    {
        /// <summary>How many neighbouring lines go at once.</summary>
        public int BandWidth = 2;

        /// <summary>Cells flattened by the most recent use, for the UI's blast.</summary>
        private readonly List<GridPos> lastFlattened = new List<GridPos>();

        public IReadOnlyList<GridPos> LastFlattenedCells
        {
            get { return lastFlattened; }
        }

        public BuldozerPower()
            : base("buldozer", "Buldozer")
        {
            SetDescription(
                "Flattens two neighbouring rows or columns, chosen at random. Crushes even "
                    + "obsidian and gold. Pays no points and never counts as a clean sweep.",
                "Rastgele seçilen ardışık 2 satırı ya da 2 sütunu siler. Obsidyeni ve altını "
                    + "bile ezer. Puan vermez, temizlik sayılmaz.");
            BaseSellValue = 50;
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            GameBoard board = ctx.Round.Board;
            // Needs room for a band on at least one axis, and something to flatten.
            return board.OccupiedCount > 0
                && (board.Height >= BandWidth || board.Width >= BandWidth);
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            GameBoard board = ctx.Round.Board;
            bool rowsFit = board.Height >= BandWidth;
            bool colsFit = board.Width >= BandWidth;
            if (!rowsFit && !colsFit)
            {
                return false;
            }
            // Random axis - unless only one of them has room for the band.
            bool useRows = rowsFit && (!colsFit || ctx.Rng.NextInt(0, 2) == 0);

            lastFlattened.Clear();
            if (useRows)
            {
                int first = board.MinY + ctx.Rng.NextInt(0, board.Height - BandWidth + 1);
                for (int y = first; y < first + BandWidth; y++)
                {
                    for (int x = board.MinX; x < board.MinX + board.Width; x++)
                    {
                        AddIfOccupied(board, new GridPos(x, y));
                    }
                }
            }
            else
            {
                int first = board.MinX + ctx.Rng.NextInt(0, board.Width - BandWidth + 1);
                for (int x = first; x < first + BandWidth; x++)
                {
                    for (int y = board.MinY; y < board.MinY + board.Height; y++)
                    {
                        AddIfOccupied(board, new GridPos(x, y));
                    }
                }
            }
            if (lastFlattened.Count == 0)
            {
                return false; // the band was empty - do not waste the charge
            }
            // forced: crushes indestructible cubes too. countsForSweep: false - the wipe is
            // inert, so it feeds no counter and TryResolveCleanSweep is deliberately NOT called.
            ctx.Round.DestroyCubes(lastFlattened, false, true);
            return true;
        }

        private void AddIfOccupied(GameBoard board, GridPos pos)
        {
            if (board.IsInside(pos) && board.GetCube(pos).HasValue)
            {
                lastFlattened.Add(pos);
            }
        }
    }
}
