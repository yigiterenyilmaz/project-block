// PURPOSE: "Mayın eşeği" - the shell game. A mine is put on one cell and SHOWN to you, the board is
// covered over, the covers dance, and the mine travels with its own cover. Follow it with your eyes
// and you know where it ended up; lose it and you are guessing for ten turns.
//
// WHAT MOVES AND WHAT DOES NOT (confirmed design, and the whole trick):
//   - the CUBES never move. Not one of them, ever. The board after a shuffle is the board before it.
//   - the MINE travels with the cover hiding it, so where its cover lands is where the mine is.
// So the shuffle is not misdirection about a fixed answer - it genuinely relocates the mine, and
// watching it is the only honest way to know. A player who blinks has to play blind.
//
// SETTING IT OFF HALVES the round's banked score (RoundEngine.HalveRoundScore). Deliberately a
// fraction rather than a flat number: a fixed penalty is barely felt on a late round and ruinous
// on an early one, while half of what you have is the same size of disaster whenever it happens -
// and it grows exactly as fast as your reason to go on playing carefully does.
//
// CORE DECIDES, THE VIEW ANIMATES. The path the mine's cover takes is computed here, off the round's
// own rng, so a headless run and a played one agree about where the mine is. The View replays that
// exact path as the dance; it never invents one.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Mayın eşeği" - a mine you have to keep your eyes on for ten turns at a time.</summary>
    public sealed class MayinEsegiBoss : BossRound
    {
        /// <summary>Turns between one reveal-and-shuffle and the next.</summary>
        public int TurnsPerShuffle = 10;

        /// <summary>How many covers the mine swaps with while the board is dark. Enough to be
        /// genuinely hard to follow, few enough to be followable at all.</summary>
        public int ShuffleSteps = 12;

        private GridPos mineCell;
        private bool armed;
        private int turnsLeft;
        private int detonations;
        private int shuffles;
        private int lastLoss;
        private GridPos lastLossCell;
        private readonly List<GridPos> path = new List<GridPos>();

        public MayinEsegiBoss()
            : base("mayin_esegi", "Mayın Eşeği")
        {
            SetDescription(
                "A mine is put on one cell and shown to you, then the board is covered and the "
                    + "covers are shuffled - and the mine travels with its own cover. Explode the "
                    + "cell it landed on and you lose HALF of everything the round has banked. "
                    + "Every ten turns it is revealed and shuffled again. The cubes never move; "
                    + "only your certainty does.",
                "Bir kareye mayın konur ve sana gösterilir, sonra harita kapatılıp kapaklar "
                    + "karıştırılır - ve mayın kendi kapağıyla birlikte gider. Kapağın indiği "
                    + "kareyi patlatırsan rauntta topladığın puanın YARISINI kaybedersin. Her 10 "
                    + "turda bir yeri tekrar gösterilip yeniden karıştırılır. Küpler hiç "
                    + "kıpırdamaz; kıpırdayan tek şey senin emin olduğun yer.");
        }

        /// <summary>Where the mine is RIGHT NOW - after the last shuffle. The View shows this only
        /// during a reveal; the rest of the time the player is on their own.</summary>
        public GridPos MineCell
        {
            get { return mineCell; }
        }

        public bool Armed
        {
            get { return armed; }
        }

        /// <summary>Turns before it is revealed and shuffled again.</summary>
        public int TurnsLeft
        {
            get { return turnsLeft; }
        }

        /// <summary>Times the player has set it off this round.</summary>
        public int Detonations
        {
            get { return detonations; }
        }

        /// <summary>The cell the LAST detonation went off on. Kept separately from MineCell
        /// because setting a mine off arms a fresh one immediately, so by the time anything can
        /// react MineCell is already the NEW mine's - and the blast belongs over the old one.
        /// </summary>
        public GridPos LastDetonationCell
        {
            get { return lastLossCell; }
        }

        /// <summary>Points the LAST detonation took, in the scaled economy the player is shown.
        /// The View says this number out loud over the cell that blew - a halving the player
        /// cannot see the size of is indistinguishable from nothing happening.</summary>
        public int LastDetonationLoss
        {
            get { return lastLoss; }
        }

        /// <summary>Bumped on every reveal-and-shuffle. The View watches it to know when to run
        /// the dance, so a shuffle is never animated twice or missed.</summary>
        public int ShuffleCount
        {
            get { return shuffles; }
        }

        /// <summary>The cells the mine's cover passes through, in order, ending where it stopped.
        /// The View animates exactly this - it does not make up a path of its own, or what the
        /// player follows would not be what the rules did.</summary>
        public IReadOnlyList<GridPos> ShufflePath
        {
            get { return path; }
        }

        public override string StatusText
        {
            get
            {
                if (!armed)
                {
                    return Loc.Pick("no room", "yer yok");
                }
                string clock = turnsLeft + Loc.Pick(" turns", " tur");
                return detonations > 0
                    ? clock + " · " + detonations + Loc.Pick(" set off", " patladı")
                    : clock;
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            detonations = 0;
            shuffles = 0;
            lastLoss = 0;
            armed = false;
            Arm(ctx.Round, ctx.Rng);
        }

        /// <summary>
        /// The boss moves last, so this is after the player's turn has fully resolved: it asks
        /// whether the mine's cell was among the cubes destroyed, and otherwise runs the clock
        /// down to the next reveal.
        /// </summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            if (!armed)
            {
                Arm(turn.Round, turn.Rng); // a board that had no room before may have some now
                return;
            }
            if (BlewUpThisTurn(turn))
            {
                detonations++;
                lastLossCell = mineCell;
                // NOT ChargeScore: that is a turn penalty, floored at what the turn earned, so a
                // mine set off by an ordinary line clear would only ever cancel that line. See
                // RoundEngine.HalveRoundScore for why this one is allowed to stand.
                lastLoss = turn.Round.HalveRoundScore(DefId);
                // A fresh mine, shown and shuffled again - the player gets a clean look at it,
                // which is the only mercy in this.
                Arm(turn.Round, turn.Rng);
                return;
            }
            turnsLeft--;
            if (turnsLeft <= 0)
            {
                Reveal(turn.Round, turn.Rng);
            }
        }

        /// <summary>Was a cube standing on the mine's cell destroyed this turn? Read from the
        /// turn's own destruction log, so it does not matter WHAT set it off - a line, a joker, a
        /// power, another boss's rule. A mine is a mine.</summary>
        private bool BlewUpThisTurn(TurnContext turn)
        {
            IReadOnlyList<DestroyedCube> destroyed = turn.Report.DestroyedCubes;
            for (int i = 0; i < destroyed.Count; i++)
            {
                if (destroyed[i].Pos.X == mineCell.X && destroyed[i].Pos.Y == mineCell.Y)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Puts a mine down somewhere random and immediately reveals and shuffles it.</summary>
        private void Arm(RoundEngine round, IRandomSource rng)
        {
            List<GridPos> cells = PlayableCells(round);
            if (cells.Count == 0 || rng == null)
            {
                armed = false;
                return;
            }
            mineCell = cells[rng.NextInt(0, cells.Count)];
            armed = true;
            Reveal(round, rng);
        }

        /// <summary>
        /// The reveal and the dance. The mine's cover swaps with a random other cover, over and
        /// over, and the mine goes where its cover goes - so the path recorded here IS where the
        /// mine has been, not a decoration.
        /// </summary>
        private void Reveal(RoundEngine round, IRandomSource rng)
        {
            turnsLeft = TurnsPerShuffle;
            shuffles++;
            path.Clear();
            path.Add(mineCell);
            List<GridPos> cells = PlayableCells(round);
            if (cells.Count < 2 || rng == null)
            {
                return; // nowhere to swap to: the mine simply stays and the View shows no dance
            }
            for (int step = 0; step < ShuffleSteps; step++)
            {
                GridPos next = cells[rng.NextInt(0, cells.Count)];
                if (next.X == mineCell.X && next.Y == mineCell.Y)
                {
                    continue; // swapping a cover with itself is not a swap
                }
                mineCell = next;
                path.Add(mineCell);
            }
        }

        /// <summary>Every cell a mine may sit on: real play area of the MAIN board. "Öteki dünya"
        /// opening a second arena must not double a boss balanced against one.</summary>
        private static List<GridPos> PlayableCells(RoundEngine round)
        {
            var cells = new List<GridPos>();
            if (round == null)
            {
                return cells;
            }
            GameBoard board = round.MainBoard;
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                for (int y = board.MinY; y < board.MinY + board.Height; y++)
                {
                    var cell = new GridPos(x, y);
                    if (board.IsInside(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }
            return cells;
        }
    }
}
