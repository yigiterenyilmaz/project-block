// PURPOSE: "Snake" - a live thing loose in the arena. A long snake is laid across the board and
// at the end of every turn it picks a direction at random and slides that way until something
// stops it. A wall stops it. A BLOCK stops it too - and it eats that block, whatever kind it is,
// and grows a segment longer for it.
//
// Its segments are cubes like any others in one respect and unlike them in another: they fill
// their cells, so a row or column they complete DOES explode - but the explosion cannot break
// them. What cuts the snake down is the explosion itself: every line that goes off with the
// snake standing in it costs the snake one segment, taken off the tail. Kill the whole snake and
// the round is over and won, for the full threshold.
//
// So the round is a hunt with a moving target: you have to complete lines THROUGH a thing that
// will not be where it was, while it eats the board you were building with.
//
// Two rulings worth knowing:
//  - the round can still be won the ordinary way, at the score bar. Killing the snake is a
//    second door, not the only one.
//  - what the snake eats is simply gone: no score, no clean-sweep credit, no ledger entry - the
//    same terms as shuffle erosion. Obsidian and gold are no exception; a snake does not care.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Snake" - kill it and the round is yours.</summary>
    public sealed class SnakeBoss : BossRound
    {
        /// <summary>Source card id stamped on a snake segment. Negative, so it can never collide
        /// with a real card and no card-counting effect claims it.</summary>
        public const int SnakeCardId = -11;

        /// <summary>Starting length on the biggest arena. BALANCE PLACEHOLDER; smaller boards get
        /// a shorter snake (see StartingLength) or there would be no room to play at all.</summary>
        public int MaxStartLength = 20;

        /// <summary>Body cells, head FIRST and tail last.</summary>
        private readonly List<GridPos> body = new List<GridPos>();

        private int segmentsEaten;
        private int blocksEaten;

        private static readonly GridPos[] Directions =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1)
        };

        public SnakeBoss()
            : base("snake", "Snake")
        {
            SetDescription(
                "A snake is loose in the arena. Every turn it slides off in a random direction "
                    + "until a wall or a block stops it - and it EATS that block, whatever kind, "
                    + "and grows. Its segments cannot be broken, but every line that explodes "
                    + "with the snake in it cuts one off its tail. Kill it and you take the "
                    + "round for the full threshold.",
                "Oyun alanına bir yılan salınır. Her tur rastgele bir yöne, bir duvara ya da bir "
                    + "bloğa çarpana kadar ilerler - çarptığı bloğu türü ne olursa olsun YER ve "
                    + "bir uzar. Küpleri yok edilemez, ama yılanın içinde bulunduğu her patlama "
                    + "kuyruğundan bir küp koparır. Yılanı tümüyle yok edersen raundu eşik "
                    + "puanıyla geçersin.");
        }

        /// <summary>The snake's cells, head first, for the View.</summary>
        public IReadOnlyList<GridPos> Body
        {
            get { return body; }
        }

        public int Length
        {
            get { return body.Count; }
        }

        /// <summary>Segments cut off the tail so far, for the UI.</summary>
        public int SegmentsCut
        {
            get { return segmentsEaten; }
        }

        /// <summary>Blocks the snake has eaten this round, for the UI.</summary>
        public int BlocksEaten
        {
            get { return blocksEaten; }
        }

        public override string StatusText
        {
            get
            {
                return body.Count > 0
                    ? Loc.Pick("snake ", "yılan ") + body.Count
                    : Loc.Pick("dead", "öldü");
            }
        }

        /// <summary>How long the snake starts on this arena. The design number is 20, which is
        /// most of a 5x5 board and all of the fun of a 9x9 one, so it is scaled down for the
        /// smaller bands: a snake has to leave the player somewhere to play.</summary>
        private int StartingLength(GameBoard board)
        {
            int edge = board.Width < board.Height ? board.Width : board.Height;
            int wanted = edge <= 5 ? 8 : (edge <= 7 ? 12 : MaxStartLength);
            // Never the whole arena: at least a few cells have to stay free or the round is
            // over before it starts.
            int ceiling = board.PlayableCellCount - 4;
            return wanted < ceiling ? wanted : (ceiling > 1 ? ceiling : 1);
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            body.Clear();
            segmentsEaten = 0;
            blocksEaten = 0;
            RoundEngine round = ctx.Round;
            if (round == null)
            {
                return;
            }
            Coil(round.Board, StartingLength(round.Board));
        }

        /// <summary>Lays the snake down in a serpentine from the bottom-left, so it always fits
        /// and always starts in the same shape for a given board.</summary>
        private void Coil(GameBoard board, int length)
        {
            for (int y = board.MinY; y < board.MinY + board.Height && body.Count < length; y++)
            {
                // Alternate rows run the other way, so the body is one unbroken line.
                bool leftToRight = ((y - board.MinY) % 2) == 0;
                for (int i = 0; i < board.Width && body.Count < length; i++)
                {
                    int x = leftToRight
                        ? board.MinX + i
                        : board.MinX + board.Width - 1 - i;
                    var cell = new GridPos(x, y);
                    if (!board.IsInside(cell) || board.GetCube(cell).HasValue)
                    {
                        continue;
                    }
                    board.SetCubeAt(cell, new Cube(CubeKind.Snake, SnakeCardId));
                    body.Add(cell);
                }
            }
            // Laid tail-first, so the last cell placed is the head.
            body.Reverse();
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            RoundEngine round = turn.Round;
            if (round == null || body.Count == 0)
            {
                return;
            }
            // 1. Every line that went off with the snake standing in it takes a segment off its
            //    tail. The line could not break the segments themselves, so they are still there
            //    to be counted.
            int cuts = CountCutsFrom(turn.Report, round.Board);
            for (int i = 0; i < cuts && body.Count > 0; i++)
            {
                CutTail(round);
            }
            if (cuts > 0)
            {
                // Segments are LIFTED, not broken: nothing here may reach the destruction log.
                round.NoteBoardRearranged();
            }
            if (body.Count == 0)
            {
                round.DeclareRoundWon();
                return;
            }
            // 2. And then it moves.
            Slide(round, turn.Rng);
            round.NoteBoardRearranged();
        }

        /// <summary>
        /// How many of this turn's exploding lines the snake was standing in.
        ///
        /// MIND THE COORDINATES. TurnReport.ExplodedRows/Columns are 0-BASED ARRAY INDICES (see
        /// LineExplosionResult), while the snake's body remembers ABSOLUTE cells. The two only
        /// agree while the board's origin is at 0,0, and an inflation power pushes MinX/MinY
        /// negative - so without the shift below the snake would stop being cut on an inflated
        /// arena, and the round could no longer be won by killing it.
        /// </summary>
        private int CountCutsFrom(TurnReport report, GameBoard board)
        {
            int cuts = 0;
            for (int i = 0; i < report.ExplodedRows.Count; i++)
            {
                if (OccupiesRow(board.MinY + report.ExplodedRows[i]))
                {
                    cuts++;
                }
            }
            for (int i = 0; i < report.ExplodedColumns.Count; i++)
            {
                if (OccupiesColumn(board.MinX + report.ExplodedColumns[i]))
                {
                    cuts++;
                }
            }
            return cuts;
        }

        private bool OccupiesRow(int y)
        {
            for (int i = 0; i < body.Count; i++)
            {
                if (body[i].Y == y)
                {
                    return true;
                }
            }
            return false;
        }

        private bool OccupiesColumn(int x)
        {
            for (int i = 0; i < body.Count; i++)
            {
                if (body[i].X == x)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Takes the last segment off the tail. Forced, because a snake segment refuses
        /// every ordinary destruction - this rule is the only thing that may remove one.</summary>
        private void CutTail(RoundEngine round)
        {
            GridPos tail = body[body.Count - 1];
            body.RemoveAt(body.Count - 1);
            round.Board.DestroyCubeForced(tail);
            segmentsEaten++;
        }

        /// <summary>
        /// One move: a random direction, then as far as it goes. It stops at a wall, at its own
        /// body, or at a block - and a block it stops at is EATEN, which is what makes the snake
        /// grow. Directions it cannot move in at all are not offered, so a boxed-in snake simply
        /// stays put instead of picking a wall four times over.
        /// </summary>
        private void Slide(RoundEngine round, IRandomSource rng)
        {
            GameBoard board = round.Board;
            var open = new List<GridPos>();
            for (int i = 0; i < Directions.Length; i++)
            {
                GridPos ahead = Add(body[0], Directions[i]);
                if (board.IsInside(ahead) && !IsBody(ahead))
                {
                    open.Add(Directions[i]);
                }
            }
            if (open.Count == 0)
            {
                return; // nowhere to go: it waits
            }
            GridPos direction = open[rng.NextInt(0, open.Count)];
            // Bounded by the board: it can never take more steps than there are cells in a line.
            int guard = board.Width + board.Height;
            while (guard-- > 0)
            {
                GridPos next = Add(body[0], direction);
                if (!board.IsInside(next) || IsBody(next))
                {
                    return; // a wall, or its own flank
                }
                bool food = board.GetCube(next).HasValue;
                if (food)
                {
                    // The segments moved so far are not casualties, so the diff is re-baselined
                    // before the one destruction that IS real goes through the engine.
                    round.NoteBoardRearranged();
                    // Whatever it is, it goes - scorelessly, uncounted, and unresisted (forced),
                    // exactly like a cell eaten by erosion.
                    round.DestroyCubes(new List<GridPos> { next }, false, true);
                    blocksEaten++;
                }
                Advance(board, next, food);
                if (food)
                {
                    return; // it stops where it fed
                }
            }
        }

        /// <summary>Moves the head one cell. Growing keeps the tail where it is; otherwise the
        /// tail cell is given back to the arena.</summary>
        private void Advance(GameBoard board, GridPos next, bool grow)
        {
            body.Insert(0, next);
            board.SetCubeAt(next, new Cube(CubeKind.Snake, SnakeCardId));
            if (grow)
            {
                return;
            }
            GridPos tail = body[body.Count - 1];
            body.RemoveAt(body.Count - 1);
            board.DestroyCubeForced(tail);
        }

        private bool IsBody(GridPos cell)
        {
            for (int i = 0; i < body.Count; i++)
            {
                if (body[i].X == cell.X && body[i].Y == cell.Y)
                {
                    return true;
                }
            }
            return false;
        }

        private static GridPos Add(GridPos a, GridPos b)
        {
            return new GridPos(a.X + b.X, a.Y + b.Y);
        }
    }
}
