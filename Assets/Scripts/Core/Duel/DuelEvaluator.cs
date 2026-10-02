// PURPOSE: What a POSITION is worth to the duel's computer side, in points still to come - the
// half of the planner that looks past the move. The planner gets the move's own points from the
// rules (it plays the move on a copy of the round); this says what the board it leaves, with the
// cards still to play, is likely to earn before the hand is over. Every term is in the round's
// own prices (the scorer the round pays with), so a weight says "how much of that to believe",
// never "how many points a thing is worth".
//
// WHAT IT KNOWS. The cards still in the computer's hand that it held BEFORE the move (they are
// known), and the multiset of everything else it has left - the card it just drew and the rest of
// its pile. Never the ORDER of the pile: the next draw is "one of these", and the formulas below
// take the expectation over that, so no plan can lean on knowing what comes next.
//
// THE TERMS (all scaled by ScoreScale at the end):
//   READY   - the best clear the NEXT turn's hand can make in one placement, in expectation over
//             the unknown draws, times the combo multiplier that clear would get. The combo
//             ladder (x1.5, then x3 for every clearing turn after) makes "always have a clear
//             ready" the strongest single habit in this game, and this is where it lives.
//   STREAK  - what a streak PROMISES: if the next hand can clear, the clear after it is paid
//             one rung higher on the ladder. Only ever a credit - a streak that cannot go on
//             simply promises nothing. (Written first as a charge for a streak the next hand
//             could not continue, it taught the house to put off a clean sweep it could take
//             now, because taking it "started a streak it would lose" - while the clear put off
//             never started one. A promise not kept is not a loss.)
//   LINES   - every live row and column, by how many placements its gaps still need (a greedy
//             cover over the remaining cards' legal placements) and whether every gap can be
//             reached at all, times what the line pays (cubes, obsidian in it).
//   MOBILITY- a card with nowhere to go is a turn thrown away; a card with few places is close to
//             it. Charged per remaining card, harder for a held one.
//   DEAD    - empty cells nothing left can ever reach: every line through one is stuck.
//   GOLD    - gold pays every turn it stands, for the turns that are left.
//   DYNAMITE- a dynamite block still to play clears the board if it goes up whole on the turn
//             it is laid; worth the cubes standing now, at the odds of setting that up.
// All of it is read off the board with the round's own queries (RowGapCount and friends, so a
// gold-locked or dead line is the rules' answer, not a second definition).

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>How much of each term to believe. Tuned by playing whole hands (BlackjackTests).</summary>
    internal sealed class DuelWeights
    {
        public double Ready = 1.0;
        public double Streak = 0.5;
        public double Lines = 0.35;
        public double Theta = 0.55;
        public double Uncoverable = 0.03;
        public double DeadHeld = 1.2;
        public double DeadFuture = 0.5;
        public double Mobility = 2.0;
        public double DeadCell = 0.12;
        public double Gold = 1.0;
        public double Dynamite = 0.8;

        /// <summary>How many first moves are looked at a second move deep.</summary>
        public int PlyTwoWidth = 6;

        /// <summary>Second-move simulations one decision may spend, roughly: the width above
        /// shrinks (to two at least) when the moves to look through are many.</summary>
        public int PlyTwoBudget = 1200;

        /// <summary>1 weighs every move by itself and the position it leaves; 2 also plays the
        /// best of them a second move deep. Only the tests turn it down.</summary>
        public int Depth = 2;

        public DuelWeights Copy()
        {
            return (DuelWeights)MemberwiseClone();
        }
    }

    internal sealed class DuelEvaluator
    {
        private readonly DuelWeights w;

        public DuelEvaluator(DuelWeights weights)
        {
            w = weights ?? new DuelWeights();
        }

        private sealed class Line
        {
            public CellMask Cells;   // its required cells
            public CellMask Gaps;    // the required cells still empty
            public int GapCount;
            public double Value;     // what it pays alone, logical
            public int Obsidian;
        }

        private sealed class CardType
        {
            public BlockCard Card;
            public string Key;
            public int Count;
            public int Known;
            public readonly List<CellMask> Placements = new List<CellMask>();
            public CellMask Union;
            public double BestClear;        // best one-placement clear, logical, before combo
            public bool Dynamite;
            public bool CanDetonate;        // a placement exists that takes the whole block with it
            public int Free;                // scratch for PlacementsToFill
        }

        // scratch, reused between calls (the evaluator is single-threaded)
        private readonly List<Line> lines = new List<Line>();
        private readonly List<CardType> types = new List<CardType>();
        private readonly Dictionary<string, CardType> typeByKey = new Dictionary<string, CardType>();
        private readonly List<GridPos> origins = new List<GridPos>();
        private readonly List<double> pool = new List<double>();
        private readonly List<int> lineHits = new List<int>();
        private readonly List<CellMask> placements = new List<CellMask>();

        /// <summary>The most cells any remaining placement covers (a greedy cover stops looking
        /// once it has found one that big).</summary>
        private int largestPlacement;

        /// <summary>The cells a plain cube may land in on the board being evaluated.</summary>
        private CellMask landable;

        private static readonly BlockShape OneCube = BlockShape.FromCells(new[] { new GridPos(0, 0) });

        /// <summary>The ways each card type can be worn, for the decision being made. A fox's
        /// forms are every shape the round holds, and a round's cards only move between its own
        /// piles, so for the length of one decision they are fixed (BeginDecision drops them).</summary>
        private readonly Dictionary<string, List<CardOption>> optionCache =
            new Dictionary<string, List<CardOption>>();

        /// <summary>Card id and choice -> its type key; a card's shape and elements never change.</summary>
        private readonly Dictionary<long, string> keyCache = new Dictionary<long, string>();

        /// <summary>Called by the planner before every decision.</summary>
        public void BeginDecision()
        {
            optionCache.Clear();
        }

        private string KeyOf(BlockCard card)
        {
            long id = ((long)card.Id << 8) | (uint)(card.ActiveChoice & 0xFF);
            string key;
            if (!keyCache.TryGetValue(id, out key))
            {
                key = DuelOptions.TypeKey(card);
                keyCache[id] = key;
            }
            return key;
        }

        /// <summary>The value of the position <paramref name="s"/> leaves, in the round's scaled
        /// points. <paramref name="knownIds"/> are the held cards the planner knew before the
        /// move; everything else still to play is an unknown draw.</summary>
        public double Evaluate(RoundEngine s, ICollection<int> knownIds)
        {
            GameBoard board = s.Board;
            IScoreCalculator scorer = s.Scorer;
            int remaining = s.Hand.Count + s.Deck.DrawCount;
            if (remaining == 0)
            {
                return 0.0; // the hand is over: nothing more to earn
            }
            if (!CellMask.Fits(board))
            {
                return 0.0; // too big to plan with masks - the move's own points decide
            }
            double rho = 0.8 * scorer.ScoreLineExplosion(1, board.Width);
            BuildLines(board, scorer);
            landable = LandableMask(board);
            BuildTypes(s, knownIds);

            largestPlacement = 0;
            for (int t = 0; t < types.Count; t++)
            {
                List<CellMask> list = types[t].Placements;
                for (int p = 0; p < list.Count; p++)
                {
                    int size = list[p].Count;
                    if (size > largestPlacement)
                    {
                        largestPlacement = size;
                    }
                }
            }

            // ---- MOBILITY: a card with nowhere to go is a turn thrown away
            CellMask reach = CellMask.Empty;
            bool anyPlaceable = false;
            double mobility = 0.0;
            for (int t = 0; t < types.Count; t++)
            {
                CardType type = types[t];
                reach = reach | type.Union;
                int m = type.Placements.Count;
                if (m == 0)
                {
                    mobility -= rho * (type.Known * w.DeadHeld + (type.Count - type.Known) * w.DeadFuture);
                    continue;
                }
                anyPlaceable = true;
                mobility -= rho * w.Mobility * type.Count / (1.0 + m);
            }
            if (!anyPlaceable)
            {
                return 0.0; // nothing left fits anywhere: the hand is over for this side
            }

            // ---- DEAD cells: empty play area nothing left can ever reach
            CellMask empty = EmptyMask(board);
            int deadCells = CellMask.Without(empty, reach).Count;
            double dead = -deadCells * w.DeadCell * rho;

            // ---- READY and STREAK: the next hand's best clear, in expectation
            int handSize = Math.Max(1, s.Rules.HandSize);
            int knownCount = 0;
            double knownBest = 0.0;
            pool.Clear();
            for (int t = 0; t < types.Count; t++)
            {
                CardType type = types[t];
                knownCount += type.Known;
                if (type.Known > 0 && type.BestClear > knownBest)
                {
                    knownBest = type.BestClear;
                }
                for (int i = type.Known; i < type.Count; i++)
                {
                    pool.Add(type.BestClear);
                }
            }
            int draws = Math.Min(pool.Count, Math.Max(0, handSize - knownCount));
            double expectedBest;
            double clearChance;
            ExpectedMax(knownBest, pool, draws, out expectedBest, out clearChance);
            int streak = s.ComboStreak;
            double nextMultiplier = scorer.ComboMultiplier(streak + 1);
            double ready = expectedBest * nextMultiplier * w.Ready;
            double streakPromise = 0.0;
            if (remaining > 1)
            {
                double nextRung = scorer.ComboMultiplier(streak + 2) - 1.0;
                streakPromise = clearChance * rho * nextRung * w.Streak;
            }

            // ---- LINES: every live line, by the placements its gaps still need
            double linePotential = 0.0;
            for (int l = 0; l < lines.Count; l++)
            {
                Line line = lines[l];
                if (line.GapCount == 0)
                {
                    continue;
                }
                double p;
                if (!CellMask.Covers(reach, line.Gaps))
                {
                    p = w.Uncoverable;
                }
                else
                {
                    int needed = PlacementsToFill(line.Gaps);
                    p = needed > remaining ? 0.0 : Math.Pow(w.Theta, needed);
                }
                linePotential += p * line.Value;
            }

            // ---- GOLD: it pays every turn it stands, for the turns that are left
            double gold = scorer.ScoreGoldBonus(board.CountCubesOfKind(CubeKind.Gold)) * remaining * w.Gold;

            // ---- DYNAMITE: a whole-block detonation clears what stands now
            double dynamite = 0.0;
            int destructible = DestructibleCount(board);
            for (int t = 0; t < types.Count; t++)
            {
                CardType type = types[t];
                if (!type.Dynamite)
                {
                    continue;
                }
                double odds = type.CanDetonate ? (type.Known > 0 ? 0.85 : 0.45) : 0.15;
                double worth = destructible * scorer.ScoreLineExplosion(0, 1) + scorer.ScoreCleanSweep() * 0.5;
                dynamite += odds * worth * type.Count * w.Dynamite;
            }

            double total = ready + streakPromise + linePotential * w.Lines + mobility + dead + gold + dynamite;
            return total * scorer.ScoreScale;
        }

        // ------------------------------------------------------------------ the board

        private void BuildLines(GameBoard board, IScoreCalculator scorer)
        {
            lines.Clear();
            for (int iy = 0; iy < board.Height; iy++)
            {
                int y = board.MinY + iy;
                if (board.RowGapCount(y) < 0)
                {
                    continue;
                }
                var line = new Line();
                for (int ix = 0; ix < board.Width; ix++)
                {
                    AddCell(board, line, new GridPos(board.MinX + ix, y));
                }
                FinishLine(line, scorer);
            }
            for (int ix = 0; ix < board.Width; ix++)
            {
                int x = board.MinX + ix;
                if (board.ColumnGapCount(x) < 0)
                {
                    continue;
                }
                var line = new Line();
                for (int iy = 0; iy < board.Height; iy++)
                {
                    AddCell(board, line, new GridPos(x, board.MinY + iy));
                }
                FinishLine(line, scorer);
            }
        }

        private static void AddCell(GameBoard board, Line line, GridPos cell)
        {
            if (!board.IsInside(cell) || board.IsOptional(cell))
            {
                return; // a hole or bonus ground never holds a line up
            }
            int index = CellMask.IndexOf(board, cell);
            line.Cells.Set(index);
            Cube? cube = board.GetCube(cell);
            if (!cube.HasValue)
            {
                line.Gaps.Set(index);
                line.GapCount++;
            }
            else if (cube.Value.Kind == CubeKind.Obsidian)
            {
                line.Obsidian++;
            }
        }

        private void FinishLine(Line line, IScoreCalculator scorer)
        {
            if (line.Cells.IsEmpty)
            {
                return;
            }
            int cubes = line.Cells.Count - line.Obsidian;
            line.Value = scorer.ScoreLineExplosion(1, cubes) + scorer.ScoreObsidianInLines(line.Obsidian);
            lines.Add(line);
        }

        /// <summary>Every cell a plain cube may land in - asked of GameBoard.CanPlace itself, one
        /// cell at a time, so a sealed cell, a hole, a standing cube, transparent ground and a
        /// trap are all the board's own answer.</summary>
        private static CellMask LandableMask(GameBoard board)
        {
            var mask = new CellMask();
            for (int ix = 0; ix < board.Width; ix++)
            {
                for (int iy = 0; iy < board.Height; iy++)
                {
                    var cell = new GridPos(board.MinX + ix, board.MinY + iy);
                    if (board.CanPlace(OneCube, cell, false, false))
                    {
                        mask.Set(CellMask.IndexOf(board, cell));
                    }
                }
            }
            return mask;
        }

        /// <summary>Every plain landing of <paramref name="shape"/>, in the order the board's
        /// own origins come in (x, then y): the shape's cells as a mask at the box's corner, slid
        /// one origin at a time, kept where it lies wholly on landable cells.</summary>
        private void SlidePlacements(GameBoard board, BlockShape shape, List<CellMask> into)
        {
            int w = board.Width;
            var corner = new CellMask();
            IReadOnlyList<GridPos> cells = shape.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                corner.Set(cells[i].X + cells[i].Y * w);
            }
            for (int ox = 0; ox + shape.Width <= w; ox++)
            {
                for (int oy = 0; oy + shape.Height <= board.Height; oy++)
                {
                    CellMask placed = corner.ShiftedUp(ox + oy * w);
                    if (CellMask.Covers(landable, placed))
                    {
                        into.Add(placed);
                    }
                }
            }
        }

        private static CellMask EmptyMask(GameBoard board)
        {
            var mask = new CellMask();
            for (int ix = 0; ix < board.Width; ix++)
            {
                for (int iy = 0; iy < board.Height; iy++)
                {
                    var cell = new GridPos(board.MinX + ix, board.MinY + iy);
                    if (board.IsInside(cell) && !board.GetCube(cell).HasValue)
                    {
                        mask.Set(CellMask.IndexOf(board, cell));
                    }
                }
            }
            return mask;
        }

        private static int DestructibleCount(GameBoard board)
        {
            int count = 0;
            for (int ix = 0; ix < board.Width; ix++)
            {
                for (int iy = 0; iy < board.Height; iy++)
                {
                    var cell = new GridPos(board.MinX + ix, board.MinY + iy);
                    Cube? cube = board.IsInside(cell) ? board.GetCube(cell) : null;
                    if (cube.HasValue && CubeRules.IsDestructible(cube.Value))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        // ------------------------------------------------------------------ the cards

        private void BuildTypes(RoundEngine s, ICollection<int> knownIds)
        {
            types.Clear();
            typeByKey.Clear();
            for (int i = 0; i < s.Hand.Count; i++)
            {
                AddCard(s, s.Hand[i], knownIds != null && knownIds.Contains(s.Hand[i].Id));
            }
            IReadOnlyList<BlockCard> pile = s.Deck.DrawPile;
            for (int i = 0; i < pile.Count; i++)
            {
                AddCard(s, pile[i], false);
            }
            // In a fixed order of their own, never the order they were met in: that order is the
            // pile's, and a tie broken by it would let the pile's order leak into the value.
            types.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        }

        private void AddCard(RoundEngine s, BlockCard card, bool known)
        {
            string key = KeyOf(card);
            CardType type;
            if (!typeByKey.TryGetValue(key, out type))
            {
                type = new CardType { Card = card, Key = key };
                typeByKey[key] = type;
                types.Add(type);
                Enumerate(s, type);
            }
            type.Count++;
            if (known)
            {
                type.Known++;
            }
        }

        private void Enumerate(RoundEngine s, CardType type)
        {
            GameBoard board = s.Board;
            List<CardOption> options;
            if (!optionCache.TryGetValue(type.Key, out options))
            {
                options = DuelOptions.Of(s, type.Card);
                optionCache[type.Key] = options;
            }
            for (int o = 0; o < options.Count; o++)
            {
                CardOption option = options[o];
                if (option.Dynamite)
                {
                    type.Dynamite = true;
                }
                placements.Clear();
                if (option.Ghost || option.Negative || option.Void || option.Antimatter)
                {
                    // the rare rules: asked of the board and the round, cell by cell
                    DuelOptions.Origins(s, type.Card, option, origins);
                    for (int i = 0; i < origins.Count; i++)
                    {
                        placements.Add(DuelOptions.MaskOf(board, option.Shape, origins[i]));
                    }
                }
                else
                {
                    // a plain landing: the shape's mask slid over the cells a cube may land in -
                    // the same answer GameBoard.CanPlace gives, at two ANDs an origin
                    SlidePlacements(board, option.Shape, placements);
                }
                for (int i = 0; i < placements.Count; i++)
                {
                    CellMask placed = placements[i];
                    type.Placements.Add(placed);
                    type.Union = type.Union | placed;
                    if (!option.CanComplete)
                    {
                        continue;
                    }
                    double clear = ClearValue(s, placed, option.Dynamite, out bool whole);
                    if (clear > type.BestClear)
                    {
                        type.BestClear = clear;
                    }
                    if (option.Dynamite && whole)
                    {
                        type.CanDetonate = true;
                    }
                }
            }
        }

        /// <summary>What a placement covering <paramref name="placed"/> pays in lines on the spot
        /// (logical, before the combo), and whether every one of its cells goes with them.</summary>
        private double ClearValue(RoundEngine s, CellMask placed, bool dynamite, out bool whole)
        {
            whole = false;
            lineHits.Clear();
            CellMask cleared = CellMask.Empty;
            int obsidian = 0;
            for (int l = 0; l < lines.Count; l++)
            {
                Line line = lines[l];
                if (line.GapCount > 0 && CellMask.Covers(placed, line.Gaps))
                {
                    lineHits.Add(l);
                    cleared = cleared | line.Cells;
                    obsidian += line.Obsidian;
                }
            }
            if (lineHits.Count == 0)
            {
                return 0.0;
            }
            IScoreCalculator scorer = s.Scorer;
            whole = CellMask.Covers(cleared, placed);
            int cubes = cleared.Count - obsidian;
            return scorer.ScoreLineExplosion(lineHits.Count, cubes) + scorer.ScoreObsidianInLines(obsidian);
        }

        /// <summary>Placements a line's gaps still need: a greedy cover over every remaining card's
        /// legal placements, each card used once. 99 when they cannot be covered.</summary>
        private int PlacementsToFill(CellMask gaps)
        {
            CellMask left = gaps;
            int used = 0;
            // how many of each type are still free to use in this cover
            for (int t = 0; t < types.Count; t++)
            {
                types[t].Free = types[t].Count;
            }
            while (!left.IsEmpty && used < 5)
            {
                int bestOverlap = 0;
                CardType bestType = null;
                CellMask bestPlacement = CellMask.Empty;
                // nothing can cover more than what is left, or more than the biggest piece
                int ceiling = Math.Min(left.Count, largestPlacement);
                for (int t = 0; t < types.Count && bestOverlap < ceiling; t++)
                {
                    CardType type = types[t];
                    if (type.Free <= 0)
                    {
                        continue;
                    }
                    List<CellMask> placements = type.Placements;
                    for (int p = 0; p < placements.Count; p++)
                    {
                        int overlap = CellMask.OverlapCount(placements[p], left);
                        if (overlap > bestOverlap)
                        {
                            bestOverlap = overlap;
                            bestType = type;
                            bestPlacement = placements[p];
                            if (bestOverlap >= ceiling)
                            {
                                break;
                            }
                        }
                    }
                }
                if (bestType == null)
                {
                    return 99;
                }
                bestType.Free--;
                left = CellMask.Without(left, bestPlacement);
                used++;
            }
            return left.IsEmpty ? used : 99;
        }

        /// <summary>
        /// E[max(known, X1..Xd)] for d draws from the pool (with replacement - a pile of a dozen
        /// cards drawn once or twice is close enough to it), and the chance that the max is above
        /// zero, i.e. that the next hand has a clear in it at all.
        /// </summary>
        private static void ExpectedMax(double known, List<double> pool, int draws,
            out double expected, out double anyChance)
        {
            if (draws <= 0 || pool.Count == 0)
            {
                expected = known;
                anyChance = known > 0 ? 1.0 : 0.0;
                return;
            }
            pool.Sort();
            double n = pool.Count;
            expected = 0.0;
            double previousCdf = 0.0;
            int i = 0;
            while (i < pool.Count)
            {
                double v = pool[i];
                int j = i;
                while (j < pool.Count && pool[j] == v)
                {
                    j++;
                }
                double cdf = Math.Pow(j / n, draws);
                // P(max of the draws is exactly v) times the max of that and the known best
                expected += (cdf - previousCdf) * Math.Max(v, known);
                previousCdf = cdf;
                i = j;
            }
            double zeroShare = 0.0;
            for (int k = 0; k < pool.Count && pool[k] <= 0.0; k++)
            {
                zeroShare += 1.0;
            }
            anyChance = known > 0 ? 1.0 : 1.0 - Math.Pow(zeroShare / n, draws);
        }
    }
}
