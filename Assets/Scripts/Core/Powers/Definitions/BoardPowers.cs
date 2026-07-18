// PURPOSE: Powers that act on the board: Çaprazlama, Çerçeve, Bardağın boş tarafı, Mayın.
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
            Description = "Seçtiğin merkezden + şeklinde bir alandaki blokları patlatır.";
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
            GridPos centre = target.Cell.Value;
            var cells = new List<GridPos> { centre };
            for (int step = 1; step <= ArmLength; step++)
            {
                cells.Add(new GridPos(centre.X + step, centre.Y));
                cells.Add(new GridPos(centre.X - step, centre.Y));
                cells.Add(new GridPos(centre.X, centre.Y + step));
                cells.Add(new GridPos(centre.X, centre.Y - step));
            }
            ctx.Round.DestroyCubes(cells, true);
            ctx.Round.TryResolveCleanSweep();
            return true;
        }
    }

    /// <summary>"Çerçeve" - clears the outermost ring of the board.</summary>
    public sealed class CercevePower : Power
    {
        public CercevePower()
            : base("cerceve", "Çerçeve")
        {
            Description = "Oyun alanının en dış katmanındaki blokları temizler.";
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
            Description = "Oyun alanındaki dolu ve boş kareler yer değiştirir. "
                + "Yeni küpler elementsizdir.";
            BaseSellValue = 45;
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
            Description = "İlk kullanımda sonraki patlamayı hafızaya alır, ikinci kullanımda "
                + "o patlamayı aynı karelerde tekrar eder. Hafıza her raunt sıfırlanır.";
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
                    return memory.Count + " kare hazır";
                }
                return listening ? "dinliyor" : "boş";
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
            Description = "Seçtiğin küpü patlatır. Boş kareye koyarsan üstüne küp geldiğinde patlar.";
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
}
