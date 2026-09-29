// PURPOSE: "Snake" - a live thing loose in the arena. A long snake is laid across the board and
// at the end of every turn it picks a direction at random and slides that way until something
// stops it. A wall stops it. A BLOCK stops it too - and it eats that block, whatever kind it is,
// and grows a segment longer for it.
//
// Its segments are cubes like any others in one respect and unlike them in another: they fill
// their cells, so a row or column they complete DOES explode - but the explosion cannot break
// them. What cuts the snake down is explosions: every line that goes off ANYWHERE costs the snake
// one segment, taken off the tail, and so does any other destruction that turn (one more). Kill the whole snake and
// the round is over and won, for the full threshold.
//
// So the round is a hunt with a moving target: you have to complete lines THROUGH a thing that
// will not be where it was, while it eats the board you were building with.
//
// Two rulings worth knowing:
//  - the score bar does NOT win this round (ThresholdDoesNotWin): points still bank, but killing
//    the snake is the only door. A turn that cuts it also holds it still - a wounded snake
//    does not slide that turn. Shuffle erosion is suspended (SuspendsBoardErosion), so the
//    arena never shrinks under a round that has no other way to end.
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
                "A snake is loose in the arena. Every turn it slides toward a block it can reach "
                    + "(or a random way if there is none) until a wall or a block stops it - and "
                    + "it EATS that block, whatever kind, and grows. Its segments cannot be "
                    + "broken, but EVERY explosion anywhere on the board cuts one off its tail, "
                    + "and a wounded snake does not move that turn. Reaching the score bar does "
                    + "not end the round - only killing the snake does, for the full threshold.",
                "Oyun alanına bir yılan salınır. Her tur ulaşabildiği bir bloğa doğru (yoksa "
                    + "rastgele bir yöne), bir duvara ya da bir bloğa çarpana kadar ilerler - "
                    + "çarptığı bloğu türü ne olursa olsun YER ve bir uzar. Küpleri yok edilemez, "
                    + "ama alanın neresinde olursa olsun HER patlama kuyruğundan bir küp koparır "
                    + "ve yaralanan yılan o tur kıpırdamaz. Eşiğe ulaşmak raundu bitirmez - "
                    + "yalnızca yılanı tümüyle yok edersen raundu eşik puanıyla geçersin.");
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

        /// <summary>THE LAST TURN AS IT HAPPENED, for the View to play: the cuts in order, whether
        /// it had anywhere to go, the body after every single cell of the slide, and what it ate.
        /// Presentation only - replaced every turn, and nothing in the rules reads it (see
        /// SnakeVisuals).</summary>
        [field: NotSaved]
        public SnakeTurnVisuals LastTurn { get; private set; }

        private int turnsPlayed;

        /// <summary>Only killing it wins: the score bar is not a way out of this round.</summary>
        public override bool ThresholdDoesNotWin
        {
            get { return true; }
        }

        public override bool SuspendsBoardErosion
        {
            get { return true; }
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
        /// most of a 7x7 board and all of the fun of an 11x11 one, so it is scaled down for the
        /// smaller bands: a snake has to leave the player somewhere to play.</summary>
        private int StartingLength(GameBoard board)
        {
            int edge = board.Width < board.Height ? board.Width : board.Height;
            int wanted = edge <= 5 ? 6 : (edge <= 7 ? 9 : MaxStartLength);
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
            LastTurn = null;
            turnsPlayed = 0;
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
            var visuals = new SnakeTurnVisuals(++turnsPlayed, body);
            LastTurn = visuals;
            // 1. Every line that went off with the snake standing in it takes a segment off its
            //    tail. The line could not break the segments themselves, so they are still there
            //    to be counted.
            int cuts = CountCutsFrom(turn.Report, round.Board, visuals);
            for (int i = 0; i < cuts && body.Count > 0; i++)
            {
                CutTail(round, visuals);
            }
            if (cuts > 0)
            {
                // Segments are LIFTED, not broken: nothing here may reach the destruction log.
                round.NoteBoardRearranged();
            }
            if (body.Count == 0)
            {
                visuals.NoteDefeated();
                visuals.NoteBodyAfter(body);
                round.DeclareRoundWon();
                return;
            }
            // 2. And then it moves - unless it was just cut: a wounded snake holds still that turn.
            if (cuts > 0)
            {
                visuals.NoteBodyAfter(body);
                return;
            }
            Slide(round, turn.Rng, visuals);
            visuals.NoteBodyAfter(body);
            round.NoteBoardRearranged();
        }

        /// <summary>
        /// How many segments this turn's explosions cost the snake.
        ///
        /// MIND THE COORDINATES. TurnReport.ExplodedRows/Columns are 0-BASED ARRAY INDICES (see
        /// LineExplosionResult), while the snake's body remembers ABSOLUTE cells. The two only
        /// agree while the board's origin is at 0,0, and an inflation power pushes MinX/MinY
        /// negative - so without the shift below the snake would stop being cut on an inflated
        /// arena, and the round could no longer be won by killing it.
        /// </summary>
        private int CountCutsFrom(TurnReport report, GameBoard board, SnakeTurnVisuals visuals)
        {
            // EVERY explosion hurts it, wherever it went off: one segment per exploded line...
            int cuts = 0;
            for (int i = 0; i < report.ExplodedRows.Count; i++)
            {
                cuts++;
                // Which line did it, so the View can start the cut where the explosion was.
                visuals.NoteCutLine(true, board.MinY + report.ExplodedRows[i]);
            }
            for (int i = 0; i < report.ExplodedColumns.Count; i++)
            {
                cuts++;
                visuals.NoteCutLine(false, board.MinX + report.ExplodedColumns[i]);
            }
            // ...and one more if anything else blew cubes up this turn (a power, fire, TNT...):
            // more destroyed than the lines account for.
            if (report.DestroyedCubes.Count > report.CubesExploded)
            {
                cuts++;
            }
            return cuts;
        }

        /// <summary>Takes the last segment off the tail. Forced, because a snake segment refuses
        /// every ordinary destruction - this rule is the only thing that may remove one.</summary>
        private void CutTail(RoundEngine round, SnakeTurnVisuals visuals)
        {
            GridPos tail = body[body.Count - 1];
            body.RemoveAt(body.Count - 1);
            round.Board.DestroyCubeForced(tail);
            segmentsEaten++;
            visuals.NoteTailCut(tail, body);
        }

        /// <summary>
        /// One move: a random direction, then as far as it goes. It stops at a wall, at its own
        /// body, or at a block - and a block it stops at is EATEN, which is what makes the snake
        /// grow. Directions it cannot move in at all are not offered, so a boxed-in snake simply
        /// stays put instead of picking a wall four times over.
        /// </summary>
        private void Slide(RoundEngine round, IRandomSource rng, SnakeTurnVisuals visuals)
        {
            GameBoard board = round.Board;
            List<GridPos> open = OpenDirections(board, body);
            if (open.Count == 0 && body.Count > 1)
            {
                // COILED INTO ITSELF: the head is walled in by its own body. Left alone it would
                // never move again, so it turns round - the tail becomes the head - and tries
                // from there.
                body.Reverse();
                open = OpenDirections(board, body);
            }
            if (open.Count == 0)
            {
                visuals.NoteStuck();
                return; // nowhere to go: it waits
            }
            // HUNGRY FIRST: a direction whose slide ends on a block beats one that ends on a wall.
            var feeding = new List<GridPos>();
            for (int i = 0; i < open.Count; i++)
            {
                if (SlideEndsOnFood(board, open[i]))
                {
                    feeding.Add(open[i]);
                }
            }
            List<GridPos> pool = feeding.Count > 0 ? feeding : open;
            GridPos direction = pool[rng.NextInt(0, pool.Count)];
            visuals.NoteDirection(direction);
            // Bounded by the board: it can never take more steps than there are cells in a line.
            int guard = board.Width + board.Height;
            while (guard-- > 0)
            {
                GridPos next = Add(body[0], direction);
                if (!CanEnter(board, body, next))
                {
                    return; // a wall, or its own flank
                }
                Cube? standing = board.GetCube(next);
                // Its own tail cell still holds a segment until Advance lifts it - that is not food.
                bool food = standing.HasValue && standing.Value.Kind != CubeKind.Snake;
                if (food)
                {
                    // Taken BEFORE the rules remove it: the View shows that block, with its own
                    // face, being pulled into the mouth.
                    visuals.NoteEaten(next, standing.Value);
                    // The segments moved so far are not casualties, so the diff is re-baselined
                    // before the one destruction that IS real goes through the engine.
                    round.NoteBoardRearranged();
                    // Whatever it is, it goes - scorelessly, uncounted, and unresisted (forced),
                    // exactly like a cell eaten by erosion.
                    round.DestroyCubes(new List<GridPos> { next }, false, true);
                    blocksEaten++;
                }
                Advance(board, next, food);
                // One cell of the slide, written down as the rules made it.
                visuals.NoteStep(body);
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
            if (!grow)
            {
                // The tail leaves FIRST: the head may be moving into the cell it is vacating.
                GridPos tail = body[body.Count - 1];
                body.RemoveAt(body.Count - 1);
                board.DestroyCubeForced(tail);
            }
            body.Insert(0, next);
            board.SetCubeAt(next, new Cube(CubeKind.Snake, SnakeCardId));
        }

        /// <summary>Whether the head of <paramref name="snake"/> may step into a cell. Its own
        /// TAIL does not block it (on a snake of three or more): that cell is empty by the time
        /// the head arrives, so a snake that has closed a loop can still chase its tail round.</summary>
        private static bool CanEnter(GameBoard board, List<GridPos> snake, GridPos cell)
        {
            if (!board.IsInside(cell))
            {
                return false;
            }
            Cube? standing = board.GetCube(cell);
            if (standing.HasValue && CubeRules.IsAnchored(standing.Value))
            {
                return false; // "Kara Delik": a hole is a wall even to the snake
            }
            int blocking = snake.Count > 2 ? snake.Count - 1 : snake.Count;
            for (int i = 0; i < blocking; i++)
            {
                if (snake[i].X == cell.X && snake[i].Y == cell.Y)
                {
                    return false;
                }
            }
            return true;
        }

        private static List<GridPos> OpenDirections(GameBoard board, List<GridPos> snake)
        {
            var open = new List<GridPos>();
            for (int i = 0; i < Directions.Length; i++)
            {
                if (CanEnter(board, snake, Add(snake[0], Directions[i])))
                {
                    open.Add(Directions[i]);
                }
            }
            return open;
        }

        /// <summary>Dry run of a slide on a copy of the body: does it stop on a block?</summary>
        private bool SlideEndsOnFood(GameBoard board, GridPos direction)
        {
            var ghost = new List<GridPos>(body);
            int guard = board.Width + board.Height;
            while (guard-- > 0)
            {
                GridPos next = Add(ghost[0], direction);
                if (!CanEnter(board, ghost, next))
                {
                    return false;
                }
                Cube? standing = board.GetCube(next);
                if (standing.HasValue && standing.Value.Kind != CubeKind.Snake)
                {
                    return true;
                }
                ghost.RemoveAt(ghost.Count - 1);
                ghost.Insert(0, next);
            }
            return false;
        }

        private static GridPos Add(GridPos a, GridPos b)
        {
            return new GridPos(a.X + b.X, a.Y + b.Y);
        }
    }
}
