// PURPOSE: "Kara Delik" - a real black hole on the board, and a gravity that sometimes loses control.
//
// THE HOLE (designer's call, 2026-09-16). Every clean sweep puts a 1x1 void card into the discard
// (at most MaxLiveVoidBlocks unplayed at once). Laid down - on an empty cell OR over a cube, which it
// swallows - it becomes a BLACK HOLE that never leaves: nothing removes it, moves it, retypes it or
// covers it (CubeRules.IsAnchored, enforced by the board itself), a block that lands on it loses that
// cube into it, it fills its cell for lines, and it never stands in the way of a sweep. The card is
// spent: the hole IS the card now.
//
// ITS GRAVITY, every turn, after the line explosions and before the sweep check (AfterLineExplosion):
//   1. EAT   - every cube in the ring right around a hole (Chebyshev distance 1) is swallowed;
//   2. PULL  - every cube in the ring after that (distance 2) is dragged one step in, onto a free cell
//              of ring 1, so it is eaten on the NEXT turn. Nothing further out feels it.
// Gold and obsidian are swallowed too; a "Parazit" host, the snake, a mine and a press capsule are not.
// Every swallowed cube (placement swallows included) pays PointsPerCube and is COUNTED. When the
// round's count reaches the arena's size (its play cells), the hole COLLAPSES the board: every other
// cube goes - each paying PointsPerCube - and the clean sweep is set off, however full the board was.
// The count then starts again.
//
// THE PRICE: at the end of a turn the gravity may lose control (DeckSwallowChancePercent) and swallow
// a whole pile of cards - always the SMALLER non-empty one (a tie takes the discard) - which sits out
// the rest of the round. At most MaxDeckSwallowsPerRound times a round, and maybe not at all. A
// swallowed draw pile means the next draw recycles the discard (or, past the threshold, loses the
// round - overtime's own rule).
//
// What happened is reported for the View (LastTurn, LastDeckSwallow); nothing reads them back.
// All numbers are BALANCE PLACEHOLDERS.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Kara Delik" - a black hole that pulls, eats and, fed enough, collapses the board.</summary>
    public sealed class KaraDelikJoker : Joker
    {
        /// <summary>How many unplayed void cards may exist at the same time within one round.</summary>
        public int MaxLiveVoidBlocks = 2;

        /// <summary>Score (logical, before the score scale) per swallowed cube and per cube the
        /// collapse destroys.</summary>
        public int PointsPerCube = 2;

        /// <summary>How far the pull reaches, in rings. Ring 1 is eaten, the rings beyond it are
        /// pulled one step in. The design says two.</summary>
        public const int Reach = 2;

        /// <summary>Chance (percent) at the end of each turn that the gravity loses control.</summary>
        public int DeckSwallowChancePercent = 6;

        public int MaxDeckSwallowsPerRound = 2;

        /// <summary>Void cards handed out this round.</summary>
        public int GrantedThisRound { get; private set; }

        /// <summary>Cubes swallowed this round since the last collapse.</summary>
        public int SwallowedThisRound { get; private set; }

        /// <summary>Times the gravity swallowed a pile this round.</summary>
        public int DeckSwallowsThisRound { get; private set; }

        private readonly List<int> liveVoidCardIds = new List<int>();

        /// <summary>What the holes did on the last turn they did anything. New object per turn.</summary>
        [field: NotSaved]
        public BlackHoleVisuals LastTurn { get; private set; }

        /// <summary>The last pile the gravity swallowed. New object per event.</summary>
        [field: NotSaved]
        public DeckSwallowVisuals LastDeckSwallow { get; private set; }

        public KaraDelikJoker()
            : base("kara_delik", "Kara Delik")
        {
            SetDescription(
                "Every clean sweep adds a 1x1 black hole to your discard. It can go on any cell, "
                    + "even a filled one, and nothing can ever remove it. Each turn it eats the "
                    + "blocks next to it and pulls in the ring beyond; every block swallowed scores. "
                    + "Swallow as many blocks as the arena has cells in a round and the board "
                    + "collapses into a clean sweep. But now and then its gravity slips and swallows "
                    + "your smaller card pile for the rest of the round (at most twice).",
                "Her temizlikte ıskartana 1x1 kara delik ekler. Dolu hücre dahil her yere konur "
                    + "ve hiçbir şey onu kaldıramaz. Her tur yanındaki blokları yutar, bir "
                    + "ötesindekileri kendine çeker; yutulan her blok puan verir. Bir rauntta oyun "
                    + "alanı kadar blok yutarsa alan çöker ve temizlik tetiklenir. Ama bazen "
                    + "kütleçekimi sapıtır ve küçük olan kart desteni o raunt için yutar (en fazla 2 kez).");
        }

        public override string StatusText
        {
            get
            {
                return Loc.Pick(SwallowedThisRound + " swallowed", SwallowedThisRound + " yutuldu");
            }
        }

        /// <summary>What the count has to reach: the arena's play cells.</summary>
        public static int GoalFor(GameBoard board)
        {
            int cells = 0;
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                for (int x = board.MinX; x < board.MinX + board.Width; x++)
                {
                    if (board.IsInside(new GridPos(x, y)))
                    {
                        cells++;
                    }
                }
            }
            return cells;
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            GrantedThisRound = 0;
            SwallowedThisRound = 0;
            DeckSwallowsThisRound = 0;
            liveVoidCardIds.Clear();
            LastTurn = null;
            LastDeckSwallow = null;
        }

        // ------------------------------------------------------------------ the hole

        public override void AfterCleanSweep(TurnContext turn)
        {
            PruneSpent(turn.Round);
            if (liveVoidCardIds.Count >= MaxLiveVoidBlocks)
            {
                return;
            }
            BlockCard card = MakeVoidCard(turn.Session);
            liveVoidCardIds.Add(card.Id);
            GrantedThisRound++;
            // Into the discard, so it joins the pile economy and can be drawn later. In
            // overtime the sweep reshuffles the discard right after this, which is the
            // deliberate reward: the void block goes straight into the fresh draw pile.
            turn.Round.Deck.Discard(card);
        }

        /// <summary>The gravity. See the file header for the order.</summary>
        public override void AfterLineExplosion(TurnContext turn)
        {
            RoundEngine round = turn.Round;
            GameBoard board = round.MainBoard;
            var report = new BlackHoleVisuals();
            report.Holes.AddRange(board.CellsOfKind(CubeKind.Void));
            report.PlacementSwallows.AddRange(turn.Report.VoidSwallows);
            report.Goal = GoalFor(board);
            int paid = report.PlacementSwallows.Count;

            DestroyWith destroy = delegate(List<GridPos> cells)
            {
                return round.DestroyCubes(cells, true, true);
            };
            if (report.Holes.Count > 0)
            {
                Eat(board, report, destroy);
                paid += report.Bites.Count;
                if (Pull(board, report))
                {
                    round.NoteBoardRearranged(); // a pulled cube is not a dead one
                }
            }
            report.SwallowedBefore = SwallowedThisRound;
            SwallowedThisRound += report.SwallowedThisTurn;
            int collapsedCubes = 0;
            if (report.Goal > 0 && SwallowedThisRound >= report.Goal)
            {
                collapsedCubes = Collapse(board, report, destroy);
                // A counted sweep, like the player's own: it pays, recharges, and goes off even on
                // a board that was full a moment ago.
                report.SweepFired = round.ForceCleanSweep();
                SwallowedThisRound = 0;
            }
            int logical = (paid + collapsedCubes) * PointsPerCube;
            if (logical > 0)
            {
                turn.AddFlatScore(logical, DefId);
            }
            report.SwallowedAfter = SwallowedThisRound;
            // The turn's score is still open here (multipliers are still to come), so these are the
            // flat payments at the score's scale, before any multiplier.
            report.PointsEach = PointsPerCube * turn.Session.Config.Scoring.ScoreScale;
            report.Points = logical * turn.Session.Config.Scoring.ScoreScale;
            if (report.SwallowedThisTurn > 0 || report.Pulls.Count > 0 || report.Collapsed)
            {
                LastTurn = report;
            }
        }

        /// <summary>How a gravity step destroys: through the engine in a round, straight on the
        /// board in the animation lab. Returns the cells that really went.</summary>
        public delegate IReadOnlyList<GridPos> DestroyWith(List<GridPos> cells);

        /// <summary>
        /// THE GRAVITY ON ANY BOARD - eat ring 1, pull ring 2 - written into <paramref name="report"/>
        /// (whose Holes it fills from the board). The joker runs exactly this in a round; the
        /// animation lab runs it on a board of its own, with the board's own forced destroy, so what
        /// the lab shows is what the rules do. Returns true when anything was pulled (the caller
        /// re-baselines its destruction diff).
        /// </summary>
        public static bool RunGravity(GameBoard board, BlackHoleVisuals report, DestroyWith destroy)
        {
            if (report.Holes.Count == 0)
            {
                report.Holes.AddRange(board.CellsOfKind(CubeKind.Void));
            }
            if (report.Holes.Count == 0)
            {
                return false;
            }
            Eat(board, report, destroy);
            return Pull(board, report);
        }

        /// <summary>The collapse on any board (see RunGravity). The sweep is the caller's.</summary>
        public static int RunCollapse(GameBoard board, BlackHoleVisuals report, DestroyWith destroy)
        {
            return Collapse(board, report, destroy);
        }

        /// <summary>Which of a hole's rings <paramref name="cell"/> is in: 1 or 2 inside the reach,
        /// 0 for the hole itself and for everything further out.</summary>
        public static int RingOf(GridPos hole, GridPos cell)
        {
            int ring = Chebyshev(hole, cell);
            return ring >= 1 && ring <= Reach ? ring : 0;
        }

        /// <summary>The nearest ring <paramref name="cell"/> is in of any hole on the board (1 or 2),
        /// and which hole that is; 0 when no hole reaches it. What the View's lensing follows.</summary>
        public static int InfluenceAt(IReadOnlyList<GridPos> holes, GridPos cell, out GridPos hole)
        {
            int best = 0;
            hole = default(GridPos);
            for (int i = 0; i < holes.Count; i++)
            {
                int ring = RingOf(holes[i], cell);
                if (ring > 0 && (best == 0 || ring < best))
                {
                    best = ring;
                    hole = holes[i];
                }
            }
            return best;
        }

        /// <summary>Ring 1 of every hole is swallowed, each cube credited to the first hole (in
        /// board order) that reaches it.</summary>
        private static void Eat(GameBoard board, BlackHoleVisuals report, DestroyWith destroy)
        {
            var cells = new List<GridPos>();
            var bites = new List<BlackHoleBite>();
            foreach (GridPos hole in report.Holes)
            {
                foreach (GridPos cell in Ring(hole, 1))
                {
                    if (!board.IsInside(cell) || Contains(cells, cell))
                    {
                        continue;
                    }
                    Cube? cube = board.GetCube(cell);
                    if (cube.HasValue && CubeRules.CanBeSwallowed(cube.Value))
                    {
                        cells.Add(cell);
                        bites.Add(new BlackHoleBite(hole, new DestroyedCube(cell, cube.Value)));
                    }
                }
            }
            if (cells.Count == 0)
            {
                return;
            }
            IReadOnlyList<GridPos> gone = destroy(cells);
            foreach (BlackHoleBite bite in bites)
            {
                if (Contains(gone, bite.Cube.Pos))
                {
                    report.Bites.Add(bite);
                }
            }
        }

        /// <summary>The rings past 1 (up to Reach) are dragged one step in, onto the free cell of
        /// the next ring in that is nearest the hole. A cube moves at most once a turn.</summary>
        private static bool Pull(GameBoard board, BlackHoleVisuals report)
        {
            var moved = new List<GridPos>(); // destinations, so a cube is never pulled twice
            for (int ring = 2; ring <= Reach; ring++)
            {
                foreach (GridPos hole in report.Holes)
                {
                    foreach (GridPos from in Ring(hole, ring))
                    {
                        if (!board.IsInside(from) || Contains(moved, from))
                        {
                            continue;
                        }
                        Cube? cube = board.GetCube(from);
                        if (!cube.HasValue || !CubeRules.CanBeSwallowed(cube.Value))
                        {
                            continue;
                        }
                        GridPos? to = PullTarget(board, hole, from, ring - 1);
                        if (to.HasValue && board.MoveCube(from, to.Value))
                        {
                            moved.Add(to.Value);
                            report.Pulls.Add(new BlackHolePull(hole, from, to.Value, cube.Value));
                        }
                    }
                }
            }
            return report.Pulls.Count > 0;
        }

        /// <summary>The free neighbour of <paramref name="from"/> on ring <paramref name="ring"/>
        /// of the hole, nearest the hole (straight in before sideways), or null.</summary>
        private static GridPos? PullTarget(GameBoard board, GridPos hole, GridPos from, int ring)
        {
            GridPos? best = null;
            int bestDistance = int.MaxValue;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }
                    var to = new GridPos(from.X + dx, from.Y + dy);
                    if (Chebyshev(to, hole) != ring || !board.IsInside(to) || board.IsSealed(to)
                        || board.GetCube(to).HasValue)
                    {
                        continue;
                    }
                    int ex = to.X - hole.X;
                    int ey = to.Y - hole.Y;
                    int distance = ex * ex + ey * ey;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = to;
                    }
                }
            }
            return best;
        }

        /// <summary>The count reached the arena's size: everything a hole can take goes, and the
        /// sweep is set off whatever is left standing. Returns how many cubes went.</summary>
        private static int Collapse(GameBoard board, BlackHoleVisuals report, DestroyWith destroy)
        {
            report.Collapsed = true;
            var cells = new List<GridPos>();
            var cubes = new List<DestroyedCube>();
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                for (int x = board.MinX; x < board.MinX + board.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    Cube? cube = board.IsInside(pos) ? board.GetCube(pos) : null;
                    if (cube.HasValue && CubeRules.CanBeSwallowed(cube.Value))
                    {
                        cells.Add(pos);
                        cubes.Add(new DestroyedCube(pos, cube.Value));
                    }
                }
            }
            IReadOnlyList<GridPos> gone = cells.Count > 0
                ? destroy(cells)
                : (IReadOnlyList<GridPos>)new List<GridPos>();
            foreach (DestroyedCube cube in cubes)
            {
                if (Contains(gone, cube.Pos))
                {
                    report.CollapseCubes.Add(cube);
                }
            }
            return report.CollapseCubes.Count;
        }

        // ------------------------------------------------------------------ the price

        public override void AfterTurnScored(TurnContext turn)
        {
            if (DeckSwallowsThisRound >= MaxDeckSwallowsPerRound)
            {
                return;
            }
            if (turn.Rng.NextInt(0, 100) >= DeckSwallowChancePercent)
            {
                return;
            }
            RoundDeck deck = turn.Round.Deck;
            int draw = deck.DrawCount;
            int discard = deck.DiscardCount;
            if (draw == 0 && discard == 0)
            {
                return; // nothing to swallow - and nothing is spent
            }
            // Always the SMALLER pile that has anything in it; a tie takes the discard.
            bool fromDraw = discard == 0 || (draw > 0 && draw < discard);
            DeckSwallowsThisRound++;
            var report = new DeckSwallowVisuals
            {
                FromDrawPile = fromDraw,
                Number = DeckSwallowsThisRound,
                OtherPileCount = fromDraw ? discard : draw
            };
            foreach (BlockCard card in deck.SwallowPile(fromDraw))
            {
                report.CardIds.Add(card.Id);
            }
            LastDeckSwallow = report;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The cells at exactly Chebyshev distance <paramref name="ring"/>, row by row.</summary>
        private static List<GridPos> Ring(GridPos centre, int ring)
        {
            var cells = new List<GridPos>();
            for (int dy = -ring; dy <= ring; dy++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == ring)
                    {
                        cells.Add(new GridPos(centre.X + dx, centre.Y + dy));
                    }
                }
            }
            return cells;
        }

        private static int Chebyshev(GridPos a, GridPos b)
        {
            return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        private static bool Contains(IReadOnlyList<GridPos> cells, GridPos cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].Equals(cell))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Forgets void cards that are no longer anywhere in the round's piles or hand,
        /// so the cap counts cards that can still be played rather than cards ever made.</summary>
        private void PruneSpent(RoundEngine round)
        {
            for (int i = liveVoidCardIds.Count - 1; i >= 0; i--)
            {
                if (!IsStillAround(round, liveVoidCardIds[i]))
                {
                    liveVoidCardIds.RemoveAt(i);
                }
            }
        }

        private static bool IsStillAround(RoundEngine round, int cardId)
        {
            for (int i = 0; i < round.Hand.Count; i++)
            {
                if (round.Hand[i].Id == cardId)
                {
                    return true;
                }
            }
            for (int i = 0; i < round.BonusHand.Count; i++)
            {
                if (round.BonusHand[i].Card.Id == cardId)
                {
                    return true;
                }
            }
            foreach (BlockCard card in round.Deck.DrawPile)
            {
                if (card.Id == cardId)
                {
                    return true;
                }
            }
            foreach (BlockCard card in round.Deck.DiscardPile)
            {
                if (card.Id == cardId)
                {
                    return true;
                }
            }
            return false;
        }

        private static BlockCard MakeVoidCard(GameSession session)
        {
            BlockShape single = BlockShape.FromCells(new[] { new GridPos(0, 0) });
            return session.CreateCard(single, new[] { BlockElement.Void });
        }
    }
}
