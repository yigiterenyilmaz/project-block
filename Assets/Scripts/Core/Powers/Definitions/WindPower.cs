// PURPOSE: "Rüzgar" - the player draws a gust across the board and it carries what can be
// carried. Fire throws embers downwind that set some of the blocks ahead alight, water is pushed
// until it hits something, and a joker's marks on the board ride it too ("Enfeksiyon"'s
// infection reaches the next block downwind - as if the joker had been used twice).
//
// The band itself is WindGust's; this file only decides what the wind does to what it touches.
// The board-only half (embers and water) is a public static so the ANIMATION LAB can blow it on a
// board of its own and get exactly what a round would get.
//
// All numbers are BALANCE PLACEHOLDERS.
//
// EXTENSION POINT: other things a wind should move (ghost traces, the rot) go in BlowOn or answer
// Joker.CanRideWind / RideWind.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>
    /// "Rüzgar" - a gust drawn across the board, three cells wide, at any angle.
    ///
    /// The player picks where it starts and drags toward where it blows (designer's call,
    /// 2026-10-02). Three things ride it:
    ///  - FIRE throws embers downwind. Each one flies to a RANDOM block ahead of it in the band and
    ///    sets it alight, or is carried off - so a gust over a fire lights SOME of what is ahead,
    ///    never an empty cell (an ember needs something to burn).
    ///  - WATER is pushed the way the wind blows until it hits something - a block, the edge, a
    ///    hole - even past the end of the band; then it falls under the arena's own gravity as
    ///    water always does, and a fire it ends up beside goes out to obsidian.
    ///  - A JOKER's marks on the board ride it when that joker says so ("Enfeksiyon").
    /// A gust that would touch nothing is refused, so the charge is never spent on empty air.
    /// </summary>
    public sealed class RuzgarPower : Power
    {
        /// <summary>Embers each fire cube in the wind throws.</summary>
        public int EmbersPerFire = 2;

        /// <summary>Chance, out of 100, that an ember finds a block to burn rather than being
        /// carried off.</summary>
        public int EmberCatchPercent = 60;

        /// <summary>The last gust, for the View. A new object per use, matched by identity.</summary>
        [field: NotSaved]
        public WindVisuals LastGust { get; private set; }

        public RuzgarPower()
            : base("ruzgar", "Rüzgar")
        {
            SetEnglishName("Gust");
            SetDescription(
                "Draw a gust across the board (3 cells wide, at any angle). Fire in it throws "
                    + "embers that set some of the blocks ahead alight, water in it is pushed until "
                    + "it hits something, and an infection in it is carried to the next block "
                    + "downwind.",
                "Alana bir rüzgar çiz (3 kare genişliğinde, istediğin açıda). İçindeki ateş "
                    + "kıvılcım savurur ve öndeki bazı blokları tutuşturur, su bir engele çarpana "
                    + "kadar sürüklenir, enfeksiyon da rüzgar yönündeki ilk bloğa taşınır.");
        }

        public override ActivationTargeting Targeting
        {
            get { return ActivationTargeting.Stroke; }
        }

        /// <summary>The gust a target blows on <paramref name="board"/>, or null without a
        /// stroke. The View asks this for its preview, so it aims with the rules' own band.</summary>
        public static WindGust GustFor(GameBoard board, ActivationTarget target)
        {
            return board != null && target.Stroke.HasValue
                ? WindGust.From(board, target.Stroke.Value)
                : null;
        }

        /// <summary>Can an ember set this cube alight? Only a plain block: the stone does not
        /// burn, water and ice put it out, and the special cubes (a target, a charge, a capsule, a
        /// trap, the rot, a parasite's host) keep what makes them special.</summary>
        public static bool CanCatchEmber(Cube cube)
        {
            return cube.Kind == CubeKind.Normal && !cube.Protected;
        }

        /// <summary>Does the wind push this cube? Water - unless a parasite holds it down.</summary>
        public static bool IsBlownWater(Cube cube)
        {
            return cube.Kind == CubeKind.Water && !cube.Protected;
        }

        /// <summary>Would this gust do anything on the board itself - a fire with a block ahead
        /// to burn, or water with somewhere to go?</summary>
        public static bool TouchesBoard(GameBoard board, WindGust gust)
        {
            if (board == null || gust == null || !gust.Valid)
            {
                return false;
            }
            IReadOnlyList<GridPos> cells = gust.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                if (ThrowsEmbers(board, gust, cells[i]))
                {
                    return true;
                }
            }
            return WaterThatMoves(board, gust).Count > 0;
        }

        /// <summary>Is <paramref name="cell"/> a fire in the gust with a block ahead to burn?
        /// (The aim marks these.)</summary>
        public static bool ThrowsEmbers(GameBoard board, WindGust gust, GridPos cell)
        {
            Cube? cube = board.GetCube(cell);
            return cube.HasValue && cube.Value.Kind == CubeKind.Fire && gust.Covers(cell)
                && FuelAhead(board, gust, cell).Count > 0;
        }

        /// <summary>
        /// The water this gust REALLY moves, by the cell it stands on now. Asked of a COPY of the
        /// board, pushed and then left to fall exactly as the round would, with each pushed cube
        /// followed to where it comes to rest - because water blown against the arena's gravity
        /// slides away and falls straight back where it was, and a gust that does only that has
        /// moved nothing and must not spend the charge. (The aim marks these.)
        /// </summary>
        public static List<GridPos> WaterThatMoves(GameBoard board, WindGust gust)
        {
            var moved = new List<GridPos>();
            if (board == null || gust == null || !gust.Valid)
            {
                return moved;
            }
            GameBoard copy = GameBoard.CreateClone(board);
            foreach (GridPos sealedCell in board.SealedCells)
            {
                copy.SealCell(sealedCell); // the clone does not carry seals, and water stops at one
            }
            var report = new WindVisuals(gust);
            PushWater(copy, gust, report);
            if (report.Pushes.Count == 0)
            {
                return moved;
            }
            var frames = new List<IReadOnlyList<WaterMove>>();
            copy.SettleWaterAndReact(frames);
            foreach (WindPush push in report.Pushes)
            {
                GridPos at = push.To;
                foreach (IReadOnlyList<WaterMove> frame in frames)
                {
                    for (int i = 0; i < frame.Count; i++)
                    {
                        if (frame[i].From.Equals(at))
                        {
                            at = frame[i].To;
                            break;
                        }
                    }
                }
                if (!at.Equals(push.From))
                {
                    moved.Add(push.From);
                }
            }
            return moved;
        }

        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            WindGust gust = GustFor(ctx.Round.Board, target);
            return gust != null && gust.Valid && Touches(ctx, gust);
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            WindGust gust = GustFor(ctx.Round.Board, target);
            if (gust == null || !gust.Valid || !Touches(ctx, gust))
            {
                return false;
            }
            WindVisuals report = BlowOn(ctx.Round.Board, gust, ctx.Rng, EmbersPerFire,
                EmberCatchPercent);
            // The pushed water falls, a fire beside water goes out, and a line the wind completed
            // goes off - the board's own between-turn rules, run once.
            ctx.Round.SettleAfterWind(report.PushFrames);
            // What jokers keep ON the board rides the gust last, onto the board as it settled.
            // Their marks belong to the main world, so a gust in the mirror carries none of them.
            if (OnMainWorld(ctx))
            {
                ctx.Session.Jokers.DispatchWind(ctx.Round, gust);
            }
            LastGust = report;
            return true;
        }

        /// <summary>
        /// THE GUST ON THE BOARD, on any board - embers first, from the board as the wind found it
        /// (collect first, burn after, so a block lit by this gust throws nothing in it), then the
        /// water, front first so the cubes behind slide into the room it leaves. Gravity is NOT
        /// run here: the round does it through the engine (RoundEngine.SettleAfterWind), the lab
        /// with GameBoard.SettleWaterAndReact.
        /// </summary>
        public static WindVisuals BlowOn(GameBoard board, WindGust gust, IRandomSource rng,
            int embersPerFire, int catchPercent)
        {
            var report = new WindVisuals(gust);
            if (board == null || gust == null || !gust.Valid)
            {
                return report;
            }
            ThrowEmbers(board, gust, rng, embersPerFire, catchPercent, report);
            PushWater(board, gust, report);
            return report;
        }

        private static void ThrowEmbers(GameBoard board, WindGust gust, IRandomSource rng,
            int embersPerFire, int catchPercent, WindVisuals report)
        {
            var sources = new List<GridPos>();
            IReadOnlyList<GridPos> cells = gust.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                Cube? cube = board.GetCube(cells[i]);
                if (cube.HasValue && cube.Value.Kind == CubeKind.Fire)
                {
                    sources.Add(cells[i]);
                }
            }
            var lit = new Dictionary<GridPos, SpreadIgnition>();
            foreach (GridPos source in sources)
            {
                List<GridPos> fuel = FuelAhead(board, gust, source);
                for (int e = 0; e < embersPerFire; e++)
                {
                    var ember = new WindEmber { Source = source };
                    report.Embers.Add(ember);
                    // Nothing ahead to burn: it is carried off, and no die is rolled for it.
                    if (fuel.Count == 0 || rng.NextInt(0, 100) >= catchPercent)
                    {
                        continue;
                    }
                    GridPos target = fuel[rng.NextInt(0, fuel.Count)];
                    ember.Target = target;
                    SpreadIgnition ignition;
                    if (!lit.TryGetValue(target, out ignition))
                    {
                        ignition = new SpreadIgnition { Cell = target, Was = board.GetCube(target).Value };
                        lit[target] = ignition;
                        report.Ignitions.Add(ignition);
                    }
                    if (!ignition.From.Contains(source))
                    {
                        ignition.From.Add(source);
                    }
                }
            }
            foreach (SpreadIgnition ignition in report.Ignitions)
            {
                board.SetCubeKind(ignition.Cell, CubeKind.Fire);
            }
        }

        /// <summary>The blocks downwind of a fire, in the band, that an ember could burn.</summary>
        private static List<GridPos> FuelAhead(GameBoard board, WindGust gust, GridPos source)
        {
            var fuel = new List<GridPos>();
            IReadOnlyList<GridPos> cells = gust.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                if (!gust.IsAhead(source, cells[i]))
                {
                    continue;
                }
                Cube? cube = board.GetCube(cells[i]);
                if (cube.HasValue && CanCatchEmber(cube.Value))
                {
                    fuel.Add(cells[i]);
                }
            }
            return fuel;
        }

        private static void PushWater(GameBoard board, WindGust gust, WindVisuals report)
        {
            IReadOnlyList<GridPos> cells = gust.Cells;
            int limit = 2 * (board.Width + board.Height);
            // Front first: the cube furthest along goes, and the ones behind follow into its room.
            for (int i = cells.Count - 1; i >= 0; i--)
            {
                Cube? cube = board.GetCube(cells[i]);
                if (!cube.HasValue || !IsBlownWater(cube.Value))
                {
                    continue;
                }
                var push = new WindPush { From = cells[i], To = cells[i], Cube = cube.Value };
                foreach (GridPos next in gust.Walk(cells[i], limit))
                {
                    if (!board.MoveCube(push.To, next))
                    {
                        break; // a block, the edge, a hole or a sealed cell: it stops here
                    }
                    push.Path.Add(next);
                    push.To = next;
                }
                if (push.Path.Count > 0)
                {
                    report.Pushes.Add(push);
                }
            }
            // Frame k is every cube's k-th step, so the whole push moves together.
            int longest = 0;
            foreach (WindPush push in report.Pushes)
            {
                longest = System.Math.Max(longest, push.Path.Count);
            }
            for (int k = 0; k < longest; k++)
            {
                var frame = new List<WaterMove>();
                foreach (WindPush push in report.Pushes)
                {
                    if (k < push.Path.Count)
                    {
                        GridPos from = k == 0 ? push.From : push.Path[k - 1];
                        frame.Add(new WaterMove(from, push.Path[k]));
                    }
                }
                report.PushFrames.Add(frame);
            }
        }

        private static bool Touches(RoundContext ctx, WindGust gust)
        {
            if (TouchesBoard(ctx.Round.Board, gust))
            {
                return true;
            }
            return OnMainWorld(ctx) && ctx.Session.Jokers.AnyRidesWind(ctx.Round, gust);
        }

        private static bool OnMainWorld(RoundContext ctx)
        {
            return ctx.Round.Board == ctx.Round.MainBoard;
        }
    }
}
