// PURPOSE: The four bosses that rewrite HOW A TURN PAYS rather than what is on the board -
// lines that refuse to go off, score held hostage, income that only comes from paperwork, and a
// coin flip over one of the things you own.
//
// Three of them lean on queries that already existed for other bosses (ScoreLineExplosion,
// DisablesJoker/Power); the two genuinely new ones - SuppressesLineExplosions and
// SuppressesAllBaseScore - are asked live by the turn resolver in exactly one place each.
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>
    /// "Bilinmezlik" - a full line does not go off. It sits there, complete, taking up room, while
    /// you keep filling what is left. Then, on a turn you cannot predict, the rule comes back for
    /// ONE turn and everything that is full goes at once.
    ///
    /// So the arena becomes a magazine you load and cannot fire. The skill is loading it evenly -
    /// eight full lines going off together is the best turn in the game, and running out of room
    /// before the moment comes is an ordinary dead end, which the boss will NOT save you from
    /// (confirmed design). It is called uncertainty for a reason.
    ///
    /// The roll happens at the END of a turn, for the NEXT one, so the engine can ask
    /// SuppressesLineExplosions live all the way through a turn and never change its mind halfway.
    /// </summary>
    public sealed class BilinmezlikBoss : BossRound
    {
        /// <summary>Chance in percent that any given turn is a firing turn.</summary>
        public int FirePercent = 22;

        /// <summary>Turns the magazine can stay shut before the next turn fires for certain. A
        /// pure coin flip can go cold for a dozen turns, which is not tension, it is a loss.</summary>
        public int MaxTurnsWithoutFiring = 6;

        private bool firesThisTurn;
        private int turnsSinceFiring;
        private int biggestVolley;

        public BilinmezlikBoss()
            : base("bilinmezlik", "Bilinmezlik")
        {
            SetDescription(
                "Full lines do NOT explode - they sit there, complete, taking up room. Every so "
                    + "often the rule comes back for one turn and everything that is full goes at "
                    + "once. Load the arena evenly, and pray it fires before you run out of room.",
                "Dolu hatlar patlamaz - tamamlanmış hâlde durur ve yer kaplar. Arada bir kural "
                    + "tek turluğuna geri gelir ve o an dolu olan ne varsa hep birden patlar. "
                    + "Alanı dengeli doldur ve yer bitmeden ateşlenmesi için dua et.");
        }

        /// <summary>True while this turn's lines will actually go off.</summary>
        public bool FiresThisTurn
        {
            get { return firesThisTurn; }
        }

        /// <summary>The most lines that ever went off in one turn this round, for the UI.</summary>
        public int BiggestVolley
        {
            get { return biggestVolley; }
        }

        public override bool SuppressesLineExplosions
        {
            get { return !firesThisTurn; }
        }

        public override string StatusText
        {
            get
            {
                if (firesThisTurn)
                {
                    return Loc.Pick("FIRING", "ATEŞ");
                }
                return biggestVolley > 0
                    ? Loc.Pick("held (best " + biggestVolley + ")", "tutuk (en iyi " + biggestVolley + ")")
                    : Loc.Pick("held", "tutuk");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            firesThisTurn = false;
            turnsSinceFiring = 0;
            biggestVolley = 0;
            Roll(ctx.Rng);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (firesThisTurn)
            {
                int volley = turn.Report.ExplodedRows.Count + turn.Report.ExplodedColumns.Count;
                if (volley > biggestVolley)
                {
                    biggestVolley = volley;
                }
                turnsSinceFiring = 0;
            }
            else
            {
                turnsSinceFiring++;
            }
            Roll(turn.Rng);
        }

        /// <summary>Decides whether the NEXT turn fires. The dry-spell cap is what keeps a cold
        /// streak from being a slow loss the player could do nothing about.</summary>
        private void Roll(IRandomSource rng)
        {
            if (turnsSinceFiring >= MaxTurnsWithoutFiring)
            {
                firesThisTurn = true;
                return;
            }
            firesThisTurn = rng != null && rng.NextInt(0, 100) < FirePercent;
        }
    }

    /// <summary>
    /// "Rehin puan" - what a line clear earns is not paid, it is HELD. Clear a line again on the
    /// very next turn and the held score is released; fail to, and it burns.
    ///
    /// So every clear is a debt the next turn has to honour, and a chain of clears pays out one
    /// turn behind itself - which means the LAST clear of any chain is always lost. Stopping is
    /// what costs you; there is no safe moment to stop.
    ///
    /// Only the LINE score is held (confirmed design). Placement points, combo, gold and every
    /// joker bonus are paid normally, so this beats your board without touching your build.
    /// </summary>
    public sealed class RehinPuanBoss : BossRound
    {
        private int held;
        private int earnedThisTurn;
        private int released;
        private int burned;

        public RehinPuanBoss()
            : base("rehin_puan", "Rehin Puan")
        {
            SetDescription(
                "What a line clear earns is HELD, not paid. Clear a line again on the very next "
                    + "turn and you get it; fail to and it burns. Only the line score is held - "
                    + "everything else pays as usual.",
                "Bir hat patlatınca kazandığın puan ödenmez, REHİN kalır. Hemen sonraki tur bir "
                    + "hat daha patlatırsan alırsın; patlatamazsan yanar. Sadece hat puanı rehin "
                    + "kalır - gerisi normal ödenir.");
        }

        /// <summary>Score waiting on the next turn to honour it.</summary>
        public int Held
        {
            get { return held; }
        }

        public int Released
        {
            get { return released; }
        }

        public int Burned
        {
            get { return burned; }
        }

        public override string StatusText
        {
            get
            {
                if (held > 0)
                {
                    return Loc.Pick("holding " + held, "rehin " + held);
                }
                return burned > 0
                    ? Loc.Pick("burned " + burned, "yanan " + burned)
                    : Loc.Pick("nothing held", "rehin yok");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            held = 0;
            earnedThisTurn = 0;
            released = 0;
            burned = 0;
        }

        /// <summary>The line pays NOTHING now. What it would have paid is remembered, and becomes
        /// the hostage the next turn has to ransom.</summary>
        public override int ScoreLineExplosion(IScoreCalculator scorer, LineExplosionScore lines)
        {
            earnedThisTurn += base.ScoreLineExplosion(scorer, lines);
            return 0;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            bool clearedALine = earnedThisTurn > 0;
            if (clearedALine)
            {
                if (held > 0)
                {
                    released += held;
                    turn.AddFlatScore(held, DefId);
                }
                held = earnedThisTurn;
            }
            else if (held > 0)
            {
                burned += held;
                held = 0;
            }
            earnedThisTurn = 0;
        }
    }

    /// <summary>
    /// "Bürokrasi bataklığı" - the arena stops paying you and starts handing you paperwork. Nothing
    /// scores on its own any more: not a placement, not a line, not a sweep. The ONLY income is
    /// finishing the task you were given, and every task carries a deadline (confirmed design).
    /// Let one run out and you take a small fine and are handed the next one.
    ///
    /// The tasks are deliberately of two shapes: DO something (clear a row, clear a column), which
    /// the deadline is a real pressure on, and DO NOT do something for N turns (no lines, no
    /// powers, only the left of your hand), which fails the moment you slip and completes by simply
    /// surviving. Between them they can ask the player to invert their whole plan twice in a round.
    /// </summary>
    public sealed class BurokrasiBatagiBoss : BossRound
    {
        /// <summary>Paid for finishing a task, scaled by how long it took to hand out.</summary>
        public int RewardPerTask = 220;

        /// <summary>Docked when a deadline runs out.</summary>
        public int FinePerFailure = 25;

        /// <summary>What each task is.</summary>
        public enum TaskKind
        {
            ClearARow = 0,
            ClearAColumn = 1,
            NoLinesFor = 2,
            NoPowersFor = 3,
            OnlyLeftOfHand = 4,
            OnlyRightOfHand = 5
        }

        private TaskKind task;
        private int turnsLeft;
        private int completed;
        private int failed;
        private bool usedAPowerThisTurn;

        public BurokrasiBatagiBoss()
            : base("burokrasi_batagi", "Bürokrasi Bataklığı")
        {
            SetDescription(
                "Nothing scores by itself any more - the only income is the task you are handed, "
                    + "and every task has a deadline. Miss one and you are fined and handed the "
                    + "next.",
                "Artık hiçbir şey kendi başına puan vermez - tek gelirin sana verilen görev ve her "
                    + "görevin süresi var. Kaçırırsan ceza yer ve bir sonrakini alırsın.");
        }

        public override bool SuppressesAllBaseScore
        {
            get { return true; }
        }

        /// <summary>The task in play, for the UI.</summary>
        public TaskKind CurrentTask
        {
            get { return task; }
        }

        public int TurnsLeft
        {
            get { return turnsLeft; }
        }

        public int Completed
        {
            get { return completed; }
        }

        public int Failed
        {
            get { return failed; }
        }

        public override string StatusText
        {
            get { return TaskText(task) + " · " + turnsLeft + Loc.Pick(" turns", " tur"); }
        }

        /// <summary>The task in words. Public so the UI can put it where the player will read it -
        /// a task you cannot see is not a task, it is a trap.</summary>
        public string TaskText(TaskKind kind)
        {
            switch (kind)
            {
                case TaskKind.ClearARow:
                    return Loc.Pick("clear a ROW", "bir SATIR patlat");
                case TaskKind.ClearAColumn:
                    return Loc.Pick("clear a COLUMN", "bir SÜTUN patlat");
                case TaskKind.NoLinesFor:
                    return Loc.Pick("clear NOTHING", "hiç patlatma");
                case TaskKind.NoPowersFor:
                    return Loc.Pick("use NO power", "güç kullanma");
                case TaskKind.OnlyLeftOfHand:
                    return Loc.Pick("play the LEFT of your hand", "elinin SOLUNDAN oyna");
                default:
                    return Loc.Pick("play the RIGHT of your hand", "elinin SAĞINDAN oyna");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            completed = 0;
            failed = 0;
            usedAPowerThisTurn = false;
            HandOutATask(ctx.Rng);
        }

        public override void OnPowerUsed(RoundContext ctx, string powerId)
        {
            usedAPowerThisTurn = true;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            bool broke = TurnBreaksTheTask(turn);
            turnsLeft--;
            usedAPowerThisTurn = false;

            if (broke)
            {
                Fine(turn);
                HandOutATask(turn.Rng);
                return;
            }
            if (TurnCompletesTheTask(turn))
            {
                completed++;
                turn.AddFlatScore(RewardPerTask, DefId);
                HandOutATask(turn.Rng);
                return;
            }
            if (turnsLeft > 0)
            {
                return;
            }
            // The deadline ran out. A "do not" task that survived its whole window is a PASS;
            // a "do" task that never happened is a miss.
            if (IsAbstinenceTask(task))
            {
                completed++;
                turn.AddFlatScore(RewardPerTask, DefId);
            }
            else
            {
                Fine(turn);
            }
            HandOutATask(turn.Rng);
        }

        private void Fine(TurnContext turn)
        {
            failed++;
            turn.Round.ChargeScore(FinePerFailure, DefId);
        }

        private static bool IsAbstinenceTask(TaskKind kind)
        {
            return kind != TaskKind.ClearARow && kind != TaskKind.ClearAColumn;
        }

        /// <summary>Did this turn break a "do not" task outright? A "do" task cannot be broken,
        /// only missed.</summary>
        private bool TurnBreaksTheTask(TurnContext turn)
        {
            TurnReport report = turn.Report;
            switch (task)
            {
                case TaskKind.NoLinesFor:
                    return report.ExplodedRows.Count + report.ExplodedColumns.Count > 0;
                case TaskKind.NoPowersFor:
                    return usedAPowerThisTurn;
                case TaskKind.OnlyLeftOfHand:
                    return report.HandIndex > HalfOf(turn.Round);
                case TaskKind.OnlyRightOfHand:
                    return report.HandIndex >= 0 && report.HandIndex < HalfOf(turn.Round);
                default:
                    return false;
            }
        }

        private bool TurnCompletesTheTask(TurnContext turn)
        {
            switch (task)
            {
                case TaskKind.ClearARow:
                    return turn.Report.ExplodedRows.Count > 0;
                case TaskKind.ClearAColumn:
                    return turn.Report.ExplodedColumns.Count > 0;
                default:
                    return false; // an abstinence task completes by running its window out
            }
        }

        /// <summary>The middle of the hand. A hand of 3 splits 0 | 1,2 - the "left" is the first
        /// slot and a bonus card (index -1) never counts as either side.</summary>
        private static int HalfOf(RoundEngine round)
        {
            return round == null ? 1 : round.Hand.Count / 2;
        }

        private void HandOutATask(IRandomSource rng)
        {
            if (rng == null)
            {
                task = TaskKind.ClearARow;
                turnsLeft = 4;
                return;
            }
            task = (TaskKind)rng.NextInt(0, 6);
            // "Do" tasks get a tight window; "do not" tasks ARE their window.
            turnsLeft = IsAbstinenceTask(task) ? rng.NextInt(4, 7) : rng.NextInt(3, 6);
        }
    }

    /// <summary>
    /// "Bul parayı al karayı" - a shell game with your own inventory. At the start of the round the
    /// boss quietly picks one of your jokers or powers to switch off, and you pick one to protect,
    /// blind. Guess right and you save it; guess wrong and it is gone for the round.
    ///
    /// You have to choose BEFORE your first turn (RoundEngine.ChooseBossProtection enforces it),
    /// and that is not fussiness: a silenced joker is visibly silent, so a player allowed to wait
    /// one turn would simply read the answer off the screen.
    ///
    /// It is a coin flip, and a coin flip is an introduction rather than a wall - so it may only
    /// ever be drawn as a run's FIRST boss (OnlyOnFirstBossRound).
    /// </summary>
    public sealed class BulParayiBoss : BossRound
    {
        private int victimId = -1;
        private bool victimIsJoker;
        private int protectedId = -1;
        private bool chosen;

        public BulParayiBoss()
            : base("bul_parayi", "Bul Parayı Al Karayı")
        {
            SetDescription(
                "It has quietly picked one of your jokers or powers to switch off for the round. "
                    + "Pick one to protect before your first turn - guess right and you save it, "
                    + "guess wrong and it is gone.",
                "Jokerlerinden ya da güçlerinden birini bu raunt boyunca kapatmayı çoktan seçti. "
                    + "İlk turundan önce koruyacağın birini seç - tutturursan kurtarırsın, "
                    + "tutturamazsan gider.");
        }

        public override bool OnlyOnFirstBossRound
        {
            get { return true; }
        }

        /// <summary>True while the player may still make their pick.</summary>
        public bool AwaitingChoice
        {
            get { return !chosen && victimId >= 0; }
        }

        /// <summary>True once the guess was right and nothing is switched off.</summary>
        public bool Saved
        {
            get { return chosen && protectedId == victimId; }
        }

        public override string StatusText
        {
            get
            {
                if (AwaitingChoice)
                {
                    return Loc.Pick("pick one to protect", "birini koru");
                }
                if (victimId < 0)
                {
                    return Loc.Pick("nothing to take", "alacak bir şey yok");
                }
                return Saved
                    ? Loc.Pick("saved", "kurtardın")
                    : Loc.Pick("one is switched off", "biri kapalı");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            victimId = -1;
            protectedId = -1;
            chosen = false;
            if (ctx.Session == null)
            {
                return;
            }
            // One pool: a joker and a power are the same kind of thing to a shell game.
            var jokerIds = new List<int>();
            foreach (Joker joker in ctx.Session.Jokers.Jokers)
            {
                jokerIds.Add(joker.InstanceId);
            }
            var powerIds = new List<int>();
            foreach (Power power in ctx.Session.Powers.Powers)
            {
                powerIds.Add(power.InstanceId);
            }
            int total = jokerIds.Count + powerIds.Count;
            if (total == 0)
            {
                return; // nothing owned: the boss has nothing to take and does nothing
            }
            int pick = ctx.Rng.NextInt(0, total);
            victimIsJoker = pick < jokerIds.Count;
            victimId = victimIsJoker ? jokerIds[pick] : powerIds[pick - jokerIds.Count];
        }

        /// <summary>The player's blind guess. Goes through RoundEngine.ChooseBossProtection, which
        /// is what enforces that it happens before any turn resolves.</summary>
        internal void Protect(int instanceId)
        {
            if (chosen)
            {
                return;
            }
            protectedId = instanceId;
            chosen = true;
        }

        public override bool DisablesJoker(Joker joker)
        {
            return victimIsJoker && joker != null && joker.InstanceId == victimId && !Saved;
        }

        public override bool DisablesPower(Power power)
        {
            return !victimIsJoker && power != null && power.InstanceId == victimId && !Saved;
        }
    }
}
