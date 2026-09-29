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
    /// <summary>
    /// "Kütleçekim merkezi" - the arena's gravity turns. Water stops falling downward and falls
    /// towards whichever of the four sides you choose, for the rest of the round.
    ///
    /// Everything else on the board is unmoved: this is not a way to rearrange your blocks, it is
    /// a way to aim your WATER. That is what makes it a puzzle piece rather than a broom - water
    /// is the one thing on the board that travels after it has been placed, and this decides
    /// where it travels to. Pull it sideways and a water block laid on the left edge fills the
    /// gap on the right; pull it upward and water climbs into the hole a clear just opened.
    ///
    /// The water already standing on the board obeys the new pull AT ONCE (RoundEngine
    /// .SetWaterFlow), so spending the charge always does something you can see, and any line the
    /// flow completes explodes under the ordinary between-turn rules.
    ///
    /// ROUND-SCOPED, because the direction lives on the BOARD and the board is built fresh every
    /// round. A clean sweep recharges it like any other power, so a round with sweeps in it is a
    /// round where the gravity can be re-aimed several times.
    /// </summary>
    public sealed class KutlecekimMerkeziPower : Power
    {
        public KutlecekimMerkeziPower()
            : base("kutlecekim_merkezi", "Kütleçekim Merkezi")
        {
            SetDescription(
                "Choose a side: every block on the board falls that way at once, and water "
                    + "keeps falling that way for the rest of the round instead of down. Any line "
                    + "the fall completes explodes.",
                "Bir yön seç: alandaki bütün bloklar hemen o yöne düşer ve su raunt boyunca "
                    + "aşağı yerine o yöne akar. Düşüşün tamamladığı satır ya da sütun "
                    + "patlar.");
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.Direction; }
        }

        public override string StatusText
        {
            get { return null; }
        }

        /// <summary>Only the four sides, and never a no-op: a direction has to be exactly one
        /// cell along one axis.</summary>
        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            if (!target.Offset.HasValue)
            {
                return false;
            }
            GridPos step = target.Offset.Value;
            int ax = step.X < 0 ? -step.X : step.X;
            int ay = step.Y < 0 ? -step.Y : step.Y;
            return ax + ay == 1;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            if (!CanRun(ctx, target))
            {
                return false;
            }
            // (2026-09-29, designer's call) The moment it is used, EVERYTHING falls that way -
            // not only the water. After that only water keeps following the new gravity.
            ctx.Round.SetWaterFlow(target.Offset.Value, true);
            return true;
        }
    }

    /// <summary>"Çaprazlama" - blows up a plus-shaped area around a chosen cell.</summary>
    public sealed class CaprazlamaPower : Power
    {
        /// <summary>How far each arm of the plus reaches from the centre.</summary>
        /// <summary>How far each arm reaches. Longer than any board, so the cross is the WHOLE
        /// row and the WHOLE column through the centre (cells off the board are simply skipped).
        /// </summary>
        public int ArmLength = 32;

        public CaprazlamaPower()
            : base("caprazlama", "Çaprazlama")
        {
            SetDescription(
                "Blows up every block in the row AND the column through a chosen cell.",
                "Seçtiğin karenin bulunduğu satırdaki VE sütundaki tüm blokları patlatır.");
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
        }

        /// <summary>Usable only while there is something ON THE RIM - the same question Run asks.
        /// It used to ask "is anything on the board at all", so the bar showed it ready on a board
        /// with an empty rim and the click did nothing.</summary>
        /// <summary>What the last usability check found, so the bar can say WHY it is not
        /// ready. Display only, rebuilt every time the bar asks.</summary>
        [NotSaved]
        private bool rimEmpty;

        public override string StatusText
        {
            get { return rimEmpty ? Loc.Pick("rim is empty", "kenar boş") : string.Empty; }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            GameBoard board = ctx.Round.Board;
            foreach (GridPos cell in board.GetOccupiedCells())
            {
                if (board.IsOnEdge(cell))
                {
                    rimEmpty = false;
                    return true;
                }
            }
            rimEmpty = true;
            return false;
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
                // A replayed explosion is a power's destruction: it pays only under "Genel
                // temizlik".
                if (ctx.Round.ExternalDestructionScores)
                {
                    int paid = echoed.Count * PointsPerEchoedCube;
                    // Only "Genel temizlik" makes a replayed explosion pay, so the points are
                    // part of what that joker has been worth - see
                    // RoundEngine.ExternalScoreCredited.
                    ctx.Round.CreditExternalScore(paid);
                    ctx.Round.AddScoreOutsideTurn(paid);
                }
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
            LastRecorded = new object();
        }

        /// <summary>The cells the echo remembers, for the View.</summary>
        public IReadOnlyList<GridPos> Memory
        {
            get { return memory; }
        }

        /// <summary>A NEW object each time an explosion is recorded, so the View plays the "heard
        /// it" beat once (matched by identity). Reporting only, [NotSaved].</summary>
        [field: NotSaved]
        public object LastRecorded { get; private set; }
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
                "Blows up the block of a chosen cube. Dropped on an empty cell it arms a mine "
                    + "that blows up the whole block that lands on it.",
                "Seçtiğin küpün bloğunu patlatır. Boş kareye koyarsan üstüne gelen bloğun "
                    + "tamamını patlatır.");
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
            Cube? hit = board.GetCube(cell);
            if (hit.HasValue)
            {
                // The WHOLE block goes (2026-09-29, designer's call): every cube of the same card
                // connected to the one picked.
                ctx.Round.DestroyCubes(BlockAt(board, cell, hit.Value.SourceCardId), true);
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

        /// <summary>The block a cube belongs to: every 4-connected cube from the same card.</summary>
        public static List<GridPos> BlockAt(GameBoard board, GridPos start, int cardId)
        {
            var found = new List<GridPos>();
            var seen = new HashSet<GridPos>();
            var open = new Stack<GridPos>();
            open.Push(start);
            while (open.Count > 0)
            {
                GridPos p = open.Pop();
                if (!seen.Add(p) || !board.IsInside(p))
                {
                    continue;
                }
                Cube? cube = board.GetCube(p);
                if (!cube.HasValue || cube.Value.SourceCardId != cardId)
                {
                    continue;
                }
                found.Add(p);
                open.Push(new GridPos(p.X + 1, p.Y));
                open.Push(new GridPos(p.X - 1, p.Y));
                open.Push(new GridPos(p.X, p.Y + 1));
                open.Push(new GridPos(p.X, p.Y - 1));
            }
            return found;
        }
    }

    /// <summary>
    /// "Buldozer" - flattens a two-wide band: either two neighbouring rows or two
    /// neighbouring columns. The player picks a cell and then which way (see Run).
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
                "Flattens two neighbouring rows or two columns - you pick where and which way. Crushes even "
                    + "obsidian and gold. Pays no points and never counts as a clean sweep.",
                "Seçtiğin yerdeki ardışık 2 satırı ya da 2 sütunu siler. Obsidyeni ve altını "
                    + "bile ezer. Puan vermez, temizlik sayılmaz.");
        }

        /// <summary>Aimed: the player picks a cell and then ROWS or COLUMNS; the band is that
        /// line and the next one (moved back one when the pick is the last line).</summary>
        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.BoardCell; }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            GameBoard board = ctx.Round.Board;
            return board.OccupiedCount > 0
                && (board.Height >= BandWidth || board.Width >= BandWidth);
        }

        /// <summary>Both possible bands through the cell, while the axis is not chosen yet.
        /// </summary>
        public override IReadOnlyList<GridPos> PreviewCells(ActivationTarget target)
        {
            if (!target.Cell.HasValue)
            {
                return System.Array.Empty<GridPos>();
            }
            GridPos c = target.Cell.Value;
            var cells = new List<GridPos>();
            for (int i = -16; i <= 16; i++)
            {
                for (int d = 0; d < BandWidth; d++)
                {
                    if (!target.Axis.HasValue || target.Axis.Value == LineAxis.Row)
                    {
                        cells.Add(new GridPos(c.X + i, c.Y + d));
                    }
                    if (!target.Axis.HasValue || target.Axis.Value == LineAxis.Column)
                    {
                        cells.Add(new GridPos(c.X + d, c.Y + i));
                    }
                }
            }
            return cells;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            GameBoard board = ctx.Round.Board;
            bool rowsFit = board.Height >= BandWidth;
            bool colsFit = board.Width >= BandWidth;
            bool useRows = target.Axis.HasValue ? target.Axis.Value == LineAxis.Row : rowsFit;
            if ((useRows && !rowsFit) || (!useRows && !colsFit) || !target.Cell.HasValue)
            {
                return false;
            }
            GridPos pick = target.Cell.Value;
            lastFlattened.Clear();
            if (useRows)
            {
                int first = System.Math.Min(System.Math.Max(pick.Y, board.MinY),
                    board.MinY + board.Height - BandWidth);
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
                int first = System.Math.Min(System.Math.Max(pick.X, board.MinX),
                    board.MinX + board.Width - BandWidth);
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

    /// <summary>
    /// "Öteki dünya" - opens a SECOND board beneath the first and plays the rest of the round
    /// across both.
    ///
    /// The mirror is a CLONE of the arena as it stands the moment this is cast, so WHEN you use
    /// it matters as much as whether you do: cast it on a clean board and you get two clean
    /// boards, cast it on a full one and you get two full ones. It lasts until the round ends.
    ///
    /// The two worlds share the deck and the discard - only the hands are separate - so a turn
    /// now costs two cards out of the same piles. A world with nowhere to play sits the turn out
    /// rather than ending the round; only both being stuck at once loses it. Each world sweeps
    /// for itself, and the same COLUMN exploding in both on the same turn is the pay-off.
    ///
    /// The round's threshold rises to match, which is the price of the second board.
    ///
    /// All the machinery is in RoundEngine.Mirror - this class only opens the door.
    /// </summary>
    public sealed class OtekiDunyaPower : Power
    {
        /// <summary>What the round's score threshold is multiplied by while both worlds run.</summary>
        public double ThresholdFactor = 1.5;

        /// <summary>Bonus per column that explodes in BOTH worlds on the same turn.</summary>
        public int MirroredColumnBonus = 90;

        public OtekiDunyaPower()
            : base("oteki_dunya", "Öteki Dünya")
        {
            SetDescription(
                "Clones the board and opens the copy beneath it. From then on a turn is one card "
                    + "in each world, drawn from the same deck. Explode the same column in both "
                    + "worlds on one turn for a big bonus. The round's threshold rises by half.",
                "Oyun alanını klonlar ve kopyasını altına açar. Bundan sonra bir tur, aynı "
                    + "desteden çekilen iki karttır - her dünyaya bir tane. Aynı sütunu iki "
                    + "dünyada birden patlatırsan büyük bonus alırsın. Rauntun eşiği yarı "
                    + "yarıya yükselir.");
        }

        public override string StatusText
        {
            get
            {
                return Loc.Pick("threshold x" + ThresholdFactor, "eşik x" + ThresholdFactor);
            }
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            // Once per round, and only while there is still a round left to reshape.
            return ctx.Round != null && !ctx.Round.HasMirrorWorld
                && ctx.Round.Status == RoundStatus.InProgress;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Round.OpenMirrorWorld(ThresholdFactor, MirroredColumnBonus,
                ctx.Rules.HandSize);
        }
    }
}
