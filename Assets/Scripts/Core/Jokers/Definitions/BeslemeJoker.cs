// PURPOSE: "Besleme" - the joker that gives you a PET on the board. It marks a patch of the
// arena, and from then on that patch is a thing you keep alive: explode cubes inside it and it
// feeds and grows, neglect it and it starves, shrinks and finally dies.
//
// WHY IT IS A PATCH AND NOT A CUBE. The design says "marks a cube", but a cube that explodes is
// gone from the board - it cannot be the thing that persists. So what is marked is a REGION,
// and an explosion inside that region is what feeds the creature living there. The player farms
// the same square over and over: fill it, blow it up, fill it again.
//
// IT LIVES FOR THE WHOLE RUN. Marked once, in the first round after the joker is acquired, and
// it survives every round change (the board is rebuilt but the region is coordinates). Which
// also means its DEATH IS PERMANENT: a dead creature leaves the joker inert for the rest of the
// run, taking up a slot until it is sold. That is the bet.
//
// THE TENSION. Growing pays better and pays more per cube - but a bigger creature needs more
// food to grow again, starves FASTER, and costs far more when it shrinks. Feeding it as hard as
// you can is a trap; the skill is knowing what size you can actually sustain.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Besleme" - a marked patch of board you keep alive by exploding cubes in it.</summary>
    public sealed class BeslemeJoker : Joker
    {
        /// <summary>Food needed to grow one step, per current size: size 1 needs this much,
        /// size 2 twice as much, and so on. Every cube exploded inside the region is one food.</summary>
        public int FoodPerGrowthStep = 4;

        /// <summary>Hungry turns a creature of size 1 survives. Every size step past the first
        /// takes one turn off that patience, never below the floor below.</summary>
        public int BaseHungerTolerance = 5;
        public int MinHungerTolerance = 2;

        /// <summary>Bonus per cube exploded inside the region, multiplied by the current size -
        /// a bigger creature pays much better, which is what makes growing worth the risk.</summary>
        public int BonusPerFedCube = 6;

        /// <summary>Penalty when it shrinks, multiplied by the size it FELL FROM. Losing a big
        /// creature's outer ring hurts far more than losing a small one's.</summary>
        public int ShrinkPenalty = 40;

        /// <summary>Penalty when it dies outright.</summary>
        public int DeathPenalty = 150;

        // ---- the creature. All of it survives round changes; only death ends it. ----
        private bool marked;
        private bool dead;
        private GridPos anchor;
        private int size = 1;
        private int food;
        private int hungryTurns;
        private int timesFed;

        private readonly List<GridPos> region = new List<GridPos>();

        public BeslemeJoker()
            : base("besleme", "Besleme")
        {
            SetDescription(
                "Marks a patch of the board and puts something alive in it. Every cube you "
                    + "explode inside it feeds it; a turn with none starves it. Fed enough it "
                    + "GROWS and pays much more per cube - but a bigger creature starves faster "
                    + "and costs far more when it shrinks. Starve it to death and this joker is "
                    + "finished for the run.",
                "Oyun alanında bir bölgeyi işaretler ve içine canlı bir şey koyar. O bölgede "
                    + "patlattığın her küp onu besler; hiç patlatmadığın tur onu aç bırakır. "
                    + "Yeterince beslenirse BÜYÜR ve küp başına çok daha fazla öder - ama büyük "
                    + "yaratık daha çabuk açlıktan ölür ve küçüldüğünde çok daha pahalıya gelir. "
                    + "Açlıktan öldürürsen bu joker koşunun kalanı için biter.");
            BaseSellValue = 70;
        }

        // ------------------------------------------------------------------ state, for the UI

        /// <summary>The cells the creature occupies right now, for the UI to mark. Empty before
        /// it is marked and after it dies.</summary>
        public IReadOnlyList<GridPos> Region
        {
            get { return region; }
        }

        public bool IsAlive
        {
            get { return marked && !dead; }
        }

        public bool IsDead
        {
            get { return dead; }
        }

        /// <summary>Edge length of the creature: 1 is 1x1, 2 is 2x2, and so on.</summary>
        public int Size
        {
            get { return size; }
        }

        /// <summary>Food banked toward the next growth, and how much that growth needs.</summary>
        public int Food
        {
            get { return food; }
        }

        public int FoodToGrow
        {
            get { return size * FoodPerGrowthStep; }
        }

        /// <summary>Hungry turns it has left before it shrinks (or dies at size 1).</summary>
        public int HungerLeft
        {
            get { return HungerTolerance - hungryTurns; }
        }

        /// <summary>How many hungry turns this size survives. Bigger is hungrier.</summary>
        private int HungerTolerance
        {
            get
            {
                int tolerance = BaseHungerTolerance - (size - 1);
                return tolerance < MinHungerTolerance ? MinHungerTolerance : tolerance;
            }
        }

        public override string StatusText
        {
            get
            {
                if (dead)
                {
                    return Loc.Pick("dead", "öldü");
                }
                if (!marked)
                {
                    return Loc.Pick("waiting", "bekliyor");
                }
                return size + "x" + size + " · " + food + "/" + FoodToGrow
                    + " · " + Loc.Pick("hunger ", "açlık ") + HungerLeft;
            }
        }

        // ---------------------------------------------------------------------- lifecycle

        /// <summary>
        /// The first round after this joker is acquired lays the mark. A round STARTS with an
        /// empty board, so there is no cube to point at yet - the mark goes on a random playable
        /// CELL, and whatever the player later builds there is what gets eaten.
        ///
        /// Every later round leaves the creature exactly as it was: it is coordinates, and the
        /// board being rebuilt does not move them.
        /// </summary>
        public override void OnRoundStarted(RoundContext ctx)
        {
            if (dead || marked || ctx.Round == null)
            {
                return;
            }
            GameBoard board = ctx.Round.Board;
            var candidates = new List<GridPos>();
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                for (int y = board.MinY; y < board.MinY + board.Height; y++)
                {
                    var cell = new GridPos(x, y);
                    if (board.IsInside(cell))
                    {
                        candidates.Add(cell);
                    }
                }
            }
            if (candidates.Count == 0)
            {
                return; // degenerate board - try again next round
            }
            anchor = candidates[ctx.Rng.NextInt(0, candidates.Count)];
            size = 1;
            food = 0;
            hungryTurns = 0;
            timesFed = 0;
            marked = true;
            RebuildRegion(board);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (!IsAlive)
            {
                return;
            }
            GameBoard board = turn.Round.Board;
            RebuildRegion(board);

            int fed = CountFoodThisTurn(turn);
            if (fed > 0)
            {
                Feed(turn, fed, board);
            }
            else
            {
                Starve(turn, board);
            }
        }

        /// <summary>Every cube destroyed inside the region this turn is one food. Counted from
        /// the turn's own destruction log, so it does not matter WHAT killed them - a line, a
        /// joker, a power: food is food.</summary>
        private int CountFoodThisTurn(TurnContext turn)
        {
            int fed = 0;
            IReadOnlyList<DestroyedCube> destroyed = turn.Report.DestroyedCubes;
            for (int i = 0; i < destroyed.Count; i++)
            {
                if (Contains(destroyed[i].Pos))
                {
                    fed++;
                }
            }
            return fed;
        }

        private void Feed(TurnContext turn, int fed, GameBoard board)
        {
            hungryTurns = 0;
            timesFed += fed;
            // The pay-off, scaled by size: this is the entire reason to grow.
            turn.AddFlatScore(fed * BonusPerFedCube * size, DefId);

            food += fed;
            while (food >= FoodToGrow && TryGrow(board))
            {
                // FoodToGrow rises with the new size, so one huge turn cannot balloon it
                // indefinitely - each step costs more than the last.
            }
        }

        /// <summary>Grows one step outward, keeping the creature centred on the cell it started
        /// from. Refuses when the board cannot hold the bigger square, and keeps the food rather
        /// than wasting it - a cramped board postpones growth instead of cancelling it.</summary>
        private bool TryGrow(GameBoard board)
        {
            int next = size + 1;
            if (next > board.Width || next > board.Height)
            {
                return false;
            }
            food -= FoodToGrow;
            size = next;
            RebuildRegion(board);
            return true;
        }

        private void Starve(TurnContext turn, GameBoard board)
        {
            hungryTurns++;
            if (hungryTurns < HungerTolerance)
            {
                return;
            }
            hungryTurns = 0;
            if (size > 1)
            {
                // It sheds a ring, and the bill is for the size it FELL FROM.
                turn.AddFlatScore(-ShrinkPenalty * size, DefId);
                size--;
                food = 0;
                RebuildRegion(board);
                return;
            }
            // Nothing left to shed.
            turn.AddFlatScore(-DeathPenalty, DefId);
            dead = true;
            region.Clear();
        }

        // ------------------------------------------------------------------------ geometry

        /// <summary>Recomputes the occupied cells: a size x size square centred on the anchor,
        /// nudged to fit the board, and holding only cells that are real play area.</summary>
        private void RebuildRegion(GameBoard board)
        {
            region.Clear();
            if (!IsAlive || board == null)
            {
                return;
            }
            int startX = anchor.X - (size - 1) / 2;
            int startY = anchor.Y - (size - 1) / 2;
            startX = Clamp(startX, board.MinX, board.MinX + board.Width - size);
            startY = Clamp(startY, board.MinY, board.MinY + board.Height - size);
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    var cell = new GridPos(startX + x, startY + y);
                    if (board.IsInside(cell))
                    {
                        region.Add(cell);
                    }
                }
            }
        }

        /// <summary>True if that cell is part of the creature.</summary>
        public bool Contains(GridPos cell)
        {
            for (int i = 0; i < region.Count; i++)
            {
                if (region[i].X == cell.X && region[i].Y == cell.Y)
                {
                    return true;
                }
            }
            return false;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min) { return min; }
            if (value < min) { return min; }
            if (value > max) { return max; }
            return value;
        }
    }
}
