// PURPOSE: Jokers that reshape the board itself: Buldozer, Robot süpürge, Kayıt defteri,
// Kentsel Dönüşüm, Kazı çalışması. They are the first jokers that DESTROY cubes, so they
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
            Description = "Her 4 turda bir oyun alanını siler. Puan vermez, temizlik sayılmaz.";
            BaseSellValue = 45;
        }

        public override string StatusText
        {
            get { return (TurnsPerWipe - TurnsSinceWipe) + " tur kaldı"; }
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

        public RobotSupurgeJoker()
            : base("robot_supurge", "Robot Süpürge")
        {
            Description = "Her turdan sonra rastgele küp siler. Temizliği kendisi tetiklerse "
                + "kapasitesi artar ve kısa süre dinlenir.";
            BaseSellValue = 55;
        }

        public override string StatusText
        {
            get { return Cooldown > 0 ? "dinleniyor " + Cooldown : Capacity + " küp/tur"; }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            Capacity = 1;
            Cooldown = 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
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
            Description = "Patlatılan küpleri sayar; sayı alan büyüklüğüne ulaşınca temizlik "
                + "tetikler. Bu joker dururken alanı boşaltmak temizlik sayılmaz.";
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
            Target = ctx.Round.Board.Width * ctx.Round.Board.Height;
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

    /// <summary>"Kentsel Dönüşüm" - the board permanently grows. Simplified first version:
    /// every N rounds it adds one row AND one column, which keeps the board rectangular.
    /// EXTENSION POINT: the design asks for "one block of extra space", which needs an
    /// irregular board (a cell mask in GameBoard) - see docs/jokers-plan.md.</summary>
    public sealed class KentselDonusumJoker : Joker
    {
        public int RoundsPerGrowth = 1;
        public int MaxExtra = 3;

        /// <summary>Rounds completed while this joker was held.</summary>
        public int RoundsHeld { get; private set; }

        public KentselDonusumJoker()
            : base("kentsel_donusum", "Kentsel Dönüşüm")
        {
            Description = "Tamamladığın her raunt sonunda oyun alanı kalıcı olarak büyür.";
            BaseSellValue = 70;
        }

        public int ExtraSize
        {
            get
            {
                int grown = RoundsHeld / (RoundsPerGrowth > 0 ? RoundsPerGrowth : 1);
                return grown < MaxExtra ? grown : MaxExtra;
            }
        }

        public override string StatusText
        {
            get { return "+" + ExtraSize; }
        }

        public override void OnRoundEnded(RoundContext ctx, RoundOutcome outcome)
        {
            if (outcome == RoundOutcome.Advanced)
            {
                RoundsHeld++;
            }
        }

        public override RoundConfig FilterRoundConfig(SessionContext ctx, RoundConfig config)
        {
            int extra = ExtraSize;
            if (extra <= 0)
            {
                return config;
            }
            return new RoundConfig(config.RoundNumber, config.BoardWidth + extra,
                config.BoardHeight + extra, config.ScoreThreshold);
        }
    }

    /// <summary>"Kazı çalışması" - a block that explodes whole, in one go, comes back to the
    /// bonus hand. The engine tells us which cards went at once; a block that had already
    /// lost cubes earlier does not qualify.</summary>
    public sealed class KaziCalismasiJoker : Joker
    {
        /// <summary>Blocks recovered this round, for the UI.</summary>
        public int RecoveredThisRound { get; private set; }

        public KaziCalismasiJoker()
            : base("kazi_calismasi", "Kazı Çalışması")
        {
            Description = "Bir blok tek seferde tümüyle patlarsa bonus eline iade edilir.";
            BaseSellValue = 50;
        }

        public override string StatusText
        {
            get { return RecoveredThisRound + " iade"; }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            RecoveredThisRound = 0;
            recovered.Clear();
        }

        private readonly HashSet<int> recovered = new HashSet<int>();

        public override void AfterTurnScored(TurnContext turn)
        {
            IReadOnlyList<int> fullyDestroyed = turn.Report.CardsFullyDestroyed;
            for (int i = 0; i < fullyDestroyed.Count; i++)
            {
                int cardId = fullyDestroyed[i];
                if (!recovered.Add(cardId))
                {
                    continue;
                }
                BlockCard card = turn.Round.TakeCardFromPiles(cardId);
                if (card == null)
                {
                    continue; // already out of the round (removed zone) - nothing to give back
                }
                // ToDiscard, not ExpireFromRound: this is a normal deck card on loan, it
                // must find its way back into the pile economy after being played.
                turn.Round.AddBonusCard(card, BonusPlayOutcome.ToDiscard);
                RecoveredThisRound++;
            }
        }
    }
}
