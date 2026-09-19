// PURPOSE: "Besleme" - the joker that gives you a PET on the board. It marks a patch of the
// arena, and from then on that patch is a thing you keep alive: explode cubes inside it and it
// feeds and grows, neglect it and it starves, shrinks and finally dies.
//
// WHY IT IS A PATCH AND NOT A CUBE. The design says "marks a cube", but a cube that explodes is
// gone from the board - it cannot be the thing that persists. So what is marked is a REGION,
// and an explosion inside that region is what feeds the creature living there. The player farms
// the same square over and over: fill it, blow it up, fill it again.
//
// IT LIVES ACROSS ROUNDS. Marked once, in the first round after the joker is acquired, and it
// survives every round change (the board is rebuilt but the region is coordinates).
//
// DEATH IS COMPLETE, AND IT COSTS TWO ROUNDS. A creature that starves dies outright: its size,
// its banked food and its place on the board are all gone, and nothing of it carries on. The
// joker then sits empty for the rest of the round it died in AND the whole round after that,
// and only the round after THAT lays a brand-new mark - a size-1 creature somewhere new. That is
// the bet: not the end of the joker, but two rounds of a dead slot and starting over from nothing.
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

        /// <summary>Whole rounds it stays empty AFTER the round it died in. 1 = out for the
        /// death round and the next one, back the round after.</summary>
        public int RestRoundsAfterDeath = 1;

        // ---- the creature. All of it survives round changes; only death ends it. ----
        private bool marked;
        private bool dead;
        /// <summary>While dead: whole rounds still to sit out before a new creature is laid.</summary>
        private int restRoundsLeft;
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
                    + "and costs far more when it shrinks. Starve it to death and it is gone for "
                    + "good - this joker sits empty for that round and the next, then a new one "
                    + "hatches somewhere else, starting from nothing.",
                "Oyun alanında bir bölgeyi işaretler ve içine canlı bir şey koyar. O bölgede "
                    + "patlattığın her küp onu besler; hiç patlatmadığın tur onu aç bırakır. "
                    + "Yeterince beslenirse BÜYÜR ve küp başına çok daha fazla öder - ama büyük "
                    + "yaratık daha çabuk açlıktan ölür ve küçüldüğünde çok daha pahalıya gelir. "
                    + "Açlıktan ölürse tamamen ölür - joker o raunt ve sonraki raunt boş kalır, "
                    + "ardından başka bir yerde sıfırdan yeni biri doğar.");
        }

        // ------------------------------------------------------------------ state, for the UI

        /// <summary>The cells the creature occupies right now, for the UI to mark. Empty before
        /// it is marked and while it is dead.</summary>
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

        /// <summary>It keeps proc statistics, so the tooltip prints its count even at zero.
        /// What it counts is FEEDINGS AND BILLS - every turn the creature ate, shrank or died -
        /// because those are the turns it did something, and a hungry turn it survived did
        /// nothing at all. The points run BOTH WAYS on purpose: a pet that has cost you more
        /// than it has paid is exactly what the player needs to see before deciding to keep
        /// feeding it.</summary>
        public override bool TracksProcs
        {
            get { return true; }
        }

        public override string StatusText
        {
            get
            {
                if (dead)
                {
                    int back = restRoundsLeft + 1;
                    return back <= 1
                        ? Loc.Pick("dead · new one next round", "öldü · sonraki raunt yenisi gelir")
                        : Loc.Pick("dead · new one in " + back + " rounds",
                            "öldü · " + back + " raunt sonra yenisi gelir");
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
        /// board being rebuilt does not move them. A DEAD creature sits its rest rounds out here,
        /// and the first round after them lays a new mark exactly as the first one was laid.
        /// </summary>
        public override void OnRoundStarted(RoundContext ctx)
        {
            if (dead)
            {
                if (restRoundsLeft > 0)
                {
                    restRoundsLeft--;
                    return;
                }
                dead = false; // rested: a new creature is laid below, from nothing
            }
            if (marked || ctx.Round == null)
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
            int bonus = fed * BonusPerFedCube * size;
            turn.AddFlatScore(bonus, DefId);
            // ONE proc per TURN the creature ate, not one per cube: what the player is being told
            // is "it fed", and it fed once. The nest's own pulse already answers cube by cube.
            NoteProc(bonus, turn);

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
                int bill = -ShrinkPenalty * size;
                turn.AddFlatScore(bill, DefId);
                // A bill is a firing too. NoteProc does not clamp, deliberately - a statistic
                // that hid the losses would be a lie about what this joker has cost you.
                NoteProc(bill, turn);
                size--;
                food = 0;
                RebuildRegion(board);
                return;
            }
            // Nothing left to shed: it dies COMPLETELY. Nothing of it survives - not its size, its
            // food or its place - and the joker sits out its rest rounds before a new one.
            turn.AddFlatScore(-DeathPenalty, DefId);
            NoteProc(-DeathPenalty, turn);
            dead = true;
            marked = false;
            restRoundsLeft = RestRoundsAfterDeath;
            size = 1;
            food = 0;
            hungryTurns = 0;
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
