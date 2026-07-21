// PURPOSE: Jokers that reshape the board itself: Buldozer, Robot süpürge, Kayıt defteri,
// Kazı çalışması. They are the first jokers that DESTROY cubes, so they
// all obey the same two engine rules:
//   - destruction goes through RoundEngine.DestroyCubes, which logs what died (kind +
//     source card) and feeds the sweep pre-condition;
//   - a sweep is never detected locally - TryResolveCleanSweep / ForceCleanSweep decide.
//
// EXPLOSION ACCOUNTING (confirmed table, see docs/jokers-plan.md):
//   line explosion       -> scores, counts for Kayıt defteri, can trigger a sweep
//   Robot süpürge        -> no score,  counts,                 can trigger a sweep
//   Buldozer             -> no score,  does NOT count,          can NEVER trigger a sweep
// Buldozer is deliberately inert: a free board wipe that also fed the counter would hand
// out sweeps for nothing.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Buldozer" - every N turns it wipes the board. No points, no sweep.</summary>
    public sealed class BuldozerJoker : Joker
    {
        public int TurnsPerWipe = 4;

        /// <summary>Turns since the last wipe, for the UI.</summary>
        public int TurnsSinceWipe { get; private set; }

        public BuldozerJoker()
            : base("buldozer", "Buldozer")
        {
            SetDescription(
                "Wipes the board every 4 turns. Pays no points and never counts as a clean sweep.",
                "Her 4 turda bir oyun alanını siler. Puan vermez, temizlik sayılmaz.");
            BaseSellValue = 45;
        }

        public override string StatusText
        {
            get
            {
                int left = TurnsPerWipe - TurnsSinceWipe;
                return Loc.Pick(left + " turns left", left + " tur kaldı");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            TurnsSinceWipe = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            TurnsSinceWipe++;
            if (TurnsSinceWipe < TurnsPerWipe)
            {
                return;
            }
            TurnsSinceWipe = 0;
            // countsForSweep: false - a Buldozer wipe is explicitly not a temizlik.
            turn.Round.DestroyCubes(turn.Round.Board.GetOccupiedCells(), false);
        }
    }

    /// <summary>"Robot süpürge" - eats random cubes after every turn. If it takes the last
    /// one and that triggers a sweep, it gets hungrier for the rest of the round and then
    /// needs a couple of turns to recharge.</summary>
    public sealed class RobotSupurgeJoker : Joker
    {
        public int CooldownTurns = 2;

        /// <summary>Cubes eaten per turn. Grows by 1 each time the sweeper finishes a board.</summary>
        public int Capacity { get; private set; } = 1;

        /// <summary>Turns left before it can eat again.</summary>
        public int Cooldown { get; private set; }

        /// <summary>Cells the sweeper ate on the most recent turn, for the UI's delayed blast.
        /// Reused list; the view copies what it needs.</summary>
        private readonly List<GridPos> lastSwept = new List<GridPos>();

        public IReadOnlyList<GridPos> LastSweptCells
        {
            get { return lastSwept; }
        }

        public RobotSupurgeJoker()
            : base("robot_supurge", "Robot Süpürge")
        {
            SetDescription(
                "Deletes random cubes after every turn. Triggering the sweep itself grows "
                    + "its capacity and sends it on a short rest.",
                "Her turdan sonra rastgele küp siler. Temizliği kendisi tetiklerse "
                    + "kapasitesi artar ve kısa süre dinlenir.");
            BaseSellValue = 55;
        }

        public override string StatusText
        {
            get
            {
                return Cooldown > 0
                    ? Loc.Pick("resting " + Cooldown, "dinleniyor " + Cooldown)
                    : Loc.Pick(Capacity + " cubes/turn", Capacity + " küp/tur");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            Capacity = 1;
            Cooldown = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            lastSwept.Clear();
            if (Cooldown > 0)
            {
                Cooldown--;
                return;
            }
            List<GridPos> occupied = turn.Round.Board.GetOccupiedCells();
            if (occupied.Count == 0)
            {
                return;
            }
            // Deterministic pick: a stable list plus the session RNG (never Random.value).
            var targets = new List<GridPos>();
            for (int i = 0; i < Capacity && occupied.Count > 0; i++)
            {
                int index = turn.Rng.NextInt(0, occupied.Count);
                targets.Add(occupied[index]);
                occupied.RemoveAt(index);
            }
            lastSwept.AddRange(targets);
            turn.Round.DestroyCubes(targets, true);

            // The sweeper may have taken the last cube - ask the engine, never guess.
            if (turn.Round.TryResolveCleanSweep())
            {
                Capacity++;
                Cooldown = CooldownTurns;
            }
        }
    }

    /// <summary>"Kayıt defteri" - counts destroyed cubes and calls a sweep when the count
    /// reaches the board's cell count. While it is held, emptying the board is NOT a sweep
    /// any more; the ledger is the only source. Off in overtime (the counter would freeze
    /// the discard recycle and make the round unwinnable).</summary>
    public sealed class KayitDefteriJoker : Joker
    {
        /// <summary>Cubes counted toward the next forced sweep.</summary>
        public int Counter { get; private set; }

        /// <summary>Cubes needed, refreshed at round start from the board size.</summary>
        public int Target { get; private set; }

        public KayitDefteriJoker()
            : base("kayit_defteri", "Kayıt Defteri")
        {
            SetDescription(
                "Counts exploded cubes; reaching the board's size triggers a clean sweep. "
                    + "While it is held, emptying the board does not count as one.",
                "Patlatılan küpleri sayar; sayı alan büyüklüğüne ulaşınca temizlik "
                    + "tetikler. Bu joker dururken alanı boşaltmak temizlik sayılmaz.");
            BaseSellValue = 60;
        }

        /// <summary>Overtime would otherwise leave no way to recycle the discard.</summary>
        public override bool DisabledInOvertime
        {
            get { return true; }
        }

        public override string StatusText
        {
            get { return Counter + "/" + Target; }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            Counter = 0;
            // PlayableCellCount, not Width*Height: an irregular board ("Tılsım") has
            // holes in its bounding box that are not play area.
            Target = ctx.Round.Board.PlayableCellCount;
            ctx.Round.SuppressNaturalSweep = true;
        }

        public override void OnRemoved(SessionContext ctx)
        {
            RoundEngine round = ctx.Session.CurrentRound;
            if (round != null)
            {
                round.SuppressNaturalSweep = false;
            }
        }

        public override void AfterLineExplosion(TurnContext turn)
        {
            Count(turn);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            Count(turn);
        }

        private int seenThisTurn;
        private int seenTurnNumber = -1;

        /// <summary>Counts the cubes the engine has destroyed but the ledger has not seen
        /// yet, so a turn with several destruction sources is counted exactly once. Reads
        /// the engine's COUNTABLE tally, which excludes Buldozer's scoreless wipe.</summary>
        private void Count(TurnContext turn)
        {
            if (seenTurnNumber != turn.Report.TurnNumber)
            {
                seenTurnNumber = turn.Report.TurnNumber;
                seenThisTurn = 0;
            }
            int destroyed = turn.Round.CubesDestroyedThisTurn;
            if (destroyed <= seenThisTurn)
            {
                return;
            }
            Counter += destroyed - seenThisTurn;
            seenThisTurn = destroyed;
            if (Target > 0 && Counter >= Target)
            {
                Counter -= Target; // the overflow carries into the next cycle
                turn.Round.ForceCleanSweep();
            }
        }
    }

    /// <summary>"Kazı çalışması" - the block you place, if it explodes whole in one go on the
    /// very turn you place it, comes back to the bonus hand. A block that survives to a later
    /// turn, or that had already lost cubes, does not qualify.</summary>
    public sealed class KaziCalismasiJoker : Joker
    {
        /// <summary>Blocks recovered this round, for the UI.</summary>
        public int RecoveredThisRound { get; private set; }

        public KaziCalismasiJoker()
            : base("kazi_calismasi", "Kazı Çalışması")
        {
            SetDescription(
                "A block that explodes whole in one go on the turn you place it is returned "
                    + "to your bonus hand.",
                "Koyduğun turda tek seferde tümüyle patlayan blok bonus eline iade edilir.");
            BaseSellValue = 50;
        }

        public override string StatusText
        {
            get { return Loc.Pick(RecoveredThisRound + " returned", RecoveredThisRound + " iade"); }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            RecoveredThisRound = 0;
            recovered.Clear();
        }

        private readonly HashSet<int> recovered = new HashSet<int>();

        public override void AfterTurnScored(TurnContext turn)
        {
            // Only the block placed THIS turn qualifies - and only if it went whole in one go.
            BlockCard placed = turn.Report.Card;
            if (placed == null || !WentWholeThisTurn(turn.Report, placed.Id))
            {
                return;
            }
            if (!recovered.Add(placed.Id))
            {
                return;
            }
            BlockCard card = turn.Round.TakeCardFromPiles(placed.Id);
            if (card == null)
            {
                return; // already out of the round (removed zone) - nothing to give back
            }
            // ToDiscard, not ExpireFromRound: this is a normal deck card on loan, it
            // must find its way back into the pile economy after being played.
            turn.Round.AddBonusCard(card, BonusPlayOutcome.ToDiscard);
            RecoveredThisRound++;
        }

        private static bool WentWholeThisTurn(TurnReport report, int cardId)
        {
            IReadOnlyList<int> fullyDestroyed = report.CardsFullyDestroyed;
            for (int i = 0; i < fullyDestroyed.Count; i++)
            {
                if (fullyDestroyed[i] == cardId)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// "Deprem" - when the board fills up and nothing in hand fits, the ground shakes and a
    /// quarter of the cubes come down, opening room to carry on. Once per round.
    ///
    /// It is a RESCUE, not a reward: the collapse pays no points and never counts as a clean
    /// sweep, the same rule Buldozer follows. If the quarter that falls still leaves no legal
    /// move, the round is lost - there is no second tremor.
    ///
    /// Only destructible cubes are candidates: obsidian and gold do not care about
    /// earthquakes, and shaking them would waste the rescue on cubes that cannot move.
    /// </summary>
    public sealed class DepremJoker : Joker
    {
        /// <summary>Share of the destructible cubes that comes down. Balance placeholder.</summary>
        public double CollapseFraction = 0.25;

        private bool usedThisRound;

        /// <summary>Cells the quake brought down, for the UI's shake-and-blast. Reused list.</summary>
        private readonly List<GridPos> lastCollapsed = new List<GridPos>();

        public IReadOnlyList<GridPos> LastCollapsedCells
        {
            get { return lastCollapsed; }
        }

        /// <summary>Quakes this run. The UI watches it change to fire the shake exactly once
        /// per collapse, without Core having to know a view exists.</summary>
        public int CollapseCount { get; private set; }

        public DepremJoker()
            : base("deprem", "Deprem")
        {
            SetDescription(
                "Once per round, if the board fills up and nothing fits, an earthquake brings "
                    + "down a quarter of the cubes so you can play on. Pays no points.",
                "Raunt başına bir kez: oyun alanı dolup koyacak yer kalmazsa deprem olur ve "
                    + "küplerin dörtte biri yıkılır, oyuna devam edersin. Puan vermez.");
            BaseSellValue = 65;
        }

        public override string StatusText
        {
            get
            {
                return usedThisRound
                    ? Loc.Pick("used", "kullanıldı")
                    : Loc.Pick("ready", "hazır");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            usedThisRound = false;
            lastCollapsed.Clear();
        }

        public override bool TryRescueFromDeadEnd(RoundContext ctx)
        {
            if (usedThisRound)
            {
                return false;
            }
            List<GridPos> candidates = DestructibleCells(ctx.Round.Board);
            if (candidates.Count == 0)
            {
                return false; // nothing an earthquake could bring down
            }

            int count = (int)System.Math.Ceiling(candidates.Count * CollapseFraction);
            if (count < 1)
            {
                count = 1;
            }
            lastCollapsed.Clear();
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int index = ctx.Rng.NextInt(0, candidates.Count);
                lastCollapsed.Add(candidates[index]);
                candidates.RemoveAt(index);
            }

            usedThisRound = true;
            CollapseCount++;
            // countsForSweep: false - a quake is explicitly not a temizlik, exactly like a
            // Buldozer wipe, so it can neither score nor hand out a sweep.
            ctx.Round.DestroyCubes(lastCollapsed, false);
            return true;
        }

        private static List<GridPos> DestructibleCells(GameBoard board)
        {
            var found = new List<GridPos>();
            foreach (GridPos cell in board.GetOccupiedCells())
            {
                Cube? cube = board.GetCube(cell);
                if (cube.HasValue && CubeRules.IsExternallyDestructible(cube.Value))
                {
                    found.Add(cell);
                }
            }
            return found;
        }
    }
}
