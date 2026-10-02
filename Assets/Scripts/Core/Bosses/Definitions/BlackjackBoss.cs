// PURPOSE: "Blackjack" (designer's call, 2026-10-02) - the boss stage played at a card table,
// against the house, for the player's own money.
//
// THE TABLE. Every hand the whole deck is shuffled and cut in two: one half is the player's, the
// other the COMPUTER's, and each plays its half out once on its own arena. The player sees the
// computer's arena - every card it lays, every line it clears - but never its hand, its pile or
// its discard. The player has jokers; the house has none. The house is dealt the elemental cards
// more often than not (ElementalLean: seven in ten of them).
//
// THE MONEY. Before each hand the player stakes part of the purse (GameSession.PlaceDuelBet). When
// both halves are played out, the side with more points wins the hand: a win pays the stake back
// twice, a draw returns it, a loss burns it. The points themselves are NOT money here
// (RoundScoreIsNotMoney) - a cleared line is only worth what it does to the comparison.
//   - the stage is beaten when the purse reaches TWICE what it held when the stage began
//     (Target). A player who sits down with nothing is staked StarterStake by the house first.
//   - the run is lost when the purse runs dry.
// So a stage is a run of hands - bet, deal, play out, settle - until one of the two happens.
//
// THE TURN. The player plays a card on their arena; then, last of all (the boss moves last), the
// house plays one on its own. When the player's half is done - every dealt card played, or
// nothing left fits - the house plays out the rest of its half and the hand is settled on the
// spot. A side with nothing that fits is simply finished; that is never a loss by itself
// (BossRound.PlaysOutCards). The player plays at most as many cards as were DEALT (bonus-hand
// cards excepted), so a joker that sends played cards back to the pile cannot make a hand endless.
//
// THE HOUSE'S PLAY is DuelPlanner's: it plays every legal move on a copy of its round and keeps
// the one whose points and position are worth most, two moves deep. See Core/Duel.
//
// A few powers would break the table - the second world (it needs the arena the house sits at),
// the inflations and retro (the arena is rebuilt every hand under them), Totem and İkinci şans
// (overtime powers - there is no overtime here) and Batak (a bet on a sweep that ends the round) -
// so they are switched off for the stage (DisablesPower), exactly as a boss silences anything.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    public sealed class BlackjackBoss : BossRound
    {
        /// <summary>What the house stakes a player who sits down with an empty purse, in purse
        /// points. The target is then twice it.</summary>
        public const long StarterStake = 500;

        /// <summary>The chance an elemental card goes to the house rather than the player.</summary>
        public const double ElementalLean = 0.7;

        /// <summary>Where the table stands.</summary>
        public enum DuelPhase
        {
            AwaitingBet = 0,
            Playing = 1,
            Won = 2,
            Lost = 3
        }

        private static readonly HashSet<string> TableBreakingPowers = new HashSet<string>
        {
            "oteki_dunya", "yatay_enflasyon", "dikey_enflasyon", "hiper_enflasyon",
            "retro", "totem", "ikinci_sans", "batak"
        };

        // ---- table state (saved by the reflection walk) ----
        public DuelPhase Phase;

        /// <summary>The purse that beats the stage.</summary>
        public long Target;

        /// <summary>The purse when the stage began (after the house's stake, if it gave one).</summary>
        public long StartPurse;

        public bool StarterGranted;

        public int HandNumber;

        /// <summary>The stake on the hand being played; 0 between hands.</summary>
        public long Bet;

        /// <summary>The player's round score when this hand was dealt.</summary>
        public int HandStartScore;

        public int PlayerDealt;
        public int PlayerPlaced;
        public bool PlayerDone;
        public bool AiDone;

        /// <summary>Every card on the table - the run's deck as the stage began.</summary>
        public List<int> TableCardIds = new List<int>();

        /// <summary>The house's half this hand is about, as it was dealt (ids) - for the tests.</summary>
        public List<int> AiDealtIds = new List<int>();

        // ---- the house's own round, and what the View reads ----

        /// <summary>The house's arena, hand and pile for the hand being played. Saved beside the
        /// boss (SaveExtra): a whole round is beyond the reflection walk.</summary>
        [NotSaved]
        internal RoundEngine AiRound;

        /// <summary>What the house played in answer to the player's last turn. New object per turn.</summary>
        [NotSaved]
        public DuelAiTurns LastAiTurns;

        /// <summary>The last hand settled. New object per hand.</summary>
        [NotSaved]
        public DuelHandResult LastHand;

        [NotSaved]
        private DuelPlanner planner;

        public BlackjackBoss()
            : base("blackjack", "Blackjack")
        {
            SetDescription(
                "The deck is cut in two: half for you, half for the house, each played out on its "
                    + "own arena. Bet from your purse before every hand - outscore the house and it "
                    + "pays double. Double your purse to win; go broke and the run is over. Lines "
                    + "here earn no money. The house gets most of the elemental cards.",
                "Deste ikiye bölünür: yarısı sana, yarısı kasaya, herkes kendi arenasında oynar. "
                    + "Her elden önce kasandan bahis koy - kasayı geçersen iki katını alırsın. "
                    + "Paranı ikiye katla ve geç; paran biterse oyun biter. Buradaki çizgiler para "
                    + "kazandırmaz. Elementli kartların çoğu kasaya gider.");
        }

        /// <summary>The house's round for the hand being played, or null between stages.</summary>
        public RoundEngine HouseRound
        {
            get { return AiRound; }
        }

        /// <summary>The player's points this hand (scaled).</summary>
        public int PlayerHandScore(RoundEngine round)
        {
            return round != null && Phase == DuelPhase.Playing ? round.RoundScore - HandStartScore : 0;
        }

        /// <summary>The house's points this hand (scaled).</summary>
        public int HouseHandScore
        {
            get { return AiRound != null ? AiRound.RoundScore : 0; }
        }

        /// <summary>True while the table waits for the player's stake.</summary>
        public bool AwaitingBet
        {
            get { return Phase == DuelPhase.AwaitingBet; }
        }

        // ------------------------------------------------------------------ the rules it bends

        public override bool PlaysOutCards
        {
            get { return true; }
        }

        public override bool HoldsPlay
        {
            get { return Phase != DuelPhase.Playing || PlayerDone; }
        }

        public override bool RoundScoreIsNotMoney
        {
            get { return true; }
        }

        public override bool ThresholdDoesNotWin
        {
            get { return true; }
        }

        public override bool SuspendsBoardErosion
        {
            get { return true; }
        }

        public override bool DisablesPower(Power power)
        {
            return power != null && TableBreakingPowers.Contains(power.DefId);
        }

        public override string StatusText
        {
            get
            {
                if (Phase == DuelPhase.AwaitingBet)
                {
                    return Loc.Pick("place your bet - target ", "bahsini koy - hedef ") + Target;
                }
                if (Phase == DuelPhase.Playing)
                {
                    return Loc.Pick("bet ", "bahis ") + Bet + Loc.Pick(" - target ", " - hedef ") + Target;
                }
                return null;
            }
        }

        /// <summary>
        /// The stakes the table offers, smallest first: a tenth, a quarter, half and three
        /// quarters of the purse, and all of it - rounded to tens where the purse is big enough
        /// for that to matter, never below 1, never above the purse, no stake twice.
        /// </summary>
        public static List<long> BetOptions(long purse)
        {
            var options = new List<long>();
            if (purse <= 0)
            {
                return options;
            }
            double[] shares = { 0.10, 0.25, 0.50, 0.75 };
            for (int i = 0; i < shares.Length; i++)
            {
                long stake = (long)Math.Round(purse * shares[i]);
                if (purse >= 100)
                {
                    stake = (stake / 10) * 10;
                }
                stake = Math.Max(1, Math.Min(purse, stake));
                if (!options.Contains(stake))
                {
                    options.Add(stake);
                }
            }
            if (!options.Contains(purse))
            {
                options.Add(purse);
            }
            options.Sort();
            return options;
        }

        // ------------------------------------------------------------------ the stage

        public override void OnRoundStarted(RoundContext ctx)
        {
            RoundEngine round = ctx.Round;
            TableCardIds.Clear();
            foreach (BlockCard card in ctx.Session.OwnedCards)
            {
                TableCardIds.Add(card.Id);
            }
            // The round dealt itself a hand from the whole deck; the table deals instead.
            round.SetAsideForDuel();
            if (ctx.Session.TotalScore <= 0)
            {
                ctx.Session.PayDuel(StarterStake);
                StarterGranted = true;
            }
            StartPurse = ctx.Session.TotalScore;
            Target = StartPurse * 2;
            HandNumber = 0;
            Bet = 0;
            AiRound = null;
            Phase = DuelPhase.AwaitingBet;
        }

        /// <summary>
        /// Stakes <paramref name="amount"/> and deals the hand. The player's arena and pile are
        /// rebuilt from their half (RoundEngine.BeginDuelHand); the house gets a round of its own.
        /// </summary>
        internal bool PlaceBet(RoundContext ctx, long amount)
        {
            if (Phase != DuelPhase.AwaitingBet || amount < 1 || amount > ctx.Session.TotalScore)
            {
                return false;
            }
            if (!ctx.Session.TakeDuelStake(amount))
            {
                return false;
            }
            Bet = amount;
            List<BlockCard> player;
            List<BlockCard> house;
            Deal(ctx, out player, out house);
            RoundEngine round = ctx.Round;
            round.BeginDuelHand(player);
            AiRound = RoundEngine.CreateDuelSide(round.Config, house, round.Scorer);
            AiDealtIds.Clear();
            foreach (BlockCard card in house)
            {
                AiDealtIds.Add(card.Id);
            }
            HandStartScore = round.RoundScore;
            PlayerDealt = player.Count;
            PlayerPlaced = 0;
            PlayerDone = false;
            AiDone = AiRound.Hand.Count == 0;
            HandNumber++;
            LastAiTurns = null;
            Phase = DuelPhase.Playing;
            return true;
        }

        /// <summary>
        /// Shuffles the table's cards and cuts them in two equal halves (an odd card sits the hand
        /// out). Each elemental card goes to the house with ElementalLean's chance - as many as the
        /// halves allow - and each half is shuffled again so the house's elements are not stacked
        /// on top of its pile. Every draw comes from the run's own stream.
        /// </summary>
        private void Deal(RoundContext ctx, out List<BlockCard> player, out List<BlockCard> house)
        {
            var byId = new Dictionary<int, BlockCard>();
            foreach (BlockCard card in ctx.Session.OwnedCards)
            {
                byId[card.Id] = card;
            }
            var cards = new List<BlockCard>();
            for (int i = 0; i < TableCardIds.Count; i++)
            {
                BlockCard card;
                if (byId.TryGetValue(TableCardIds[i], out card))
                {
                    cards.Add(card);
                }
            }
            Shuffle(cards, ctx.Rng);
            int half = cards.Count / 2;
            var elemental = new List<BlockCard>();
            var plain = new List<BlockCard>();
            for (int i = 0; i < half * 2; i++)
            {
                (cards[i].Elements.Count > 0 ? elemental : plain).Add(cards[i]);
            }
            int toHouse = 0;
            for (int i = 0; i < elemental.Count; i++)
            {
                if (ctx.Rng.NextDouble() < ElementalLean)
                {
                    toHouse++;
                }
            }
            toHouse = Math.Max(toHouse, elemental.Count - half);
            toHouse = Math.Min(toHouse, Math.Min(elemental.Count, half));
            house = new List<BlockCard>(half);
            player = new List<BlockCard>(half);
            for (int i = 0; i < elemental.Count; i++)
            {
                (i < toHouse ? house : player).Add(elemental[i]);
            }
            int plainToHouse = half - toHouse;
            for (int i = 0; i < plain.Count; i++)
            {
                (i < plainToHouse ? house : player).Add(plain[i]);
            }
            Shuffle(house, ctx.Rng);
            Shuffle(player, ctx.Rng);
        }

        private static void Shuffle(List<BlockCard> cards, IRandomSource rng)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                BlockCard swap = cards[i];
                cards[i] = cards[j];
                cards[j] = swap;
            }
        }

        // ------------------------------------------------------------------ the hand

        public override void AfterTurnScored(TurnContext turn)
        {
            if (Phase != DuelPhase.Playing || PlayerDone)
            {
                return;
            }
            if (!turn.Report.PlayedFromBonusHand)
            {
                PlayerPlaced++;
            }
            var answer = new DuelAiTurns();
            if (!AiDone)
            {
                HousePlaysOne(answer);
            }
            if (PlayerPlaced >= PlayerDealt || !turn.Round.HasMoveLeft)
            {
                FinishHand(turn.Session, turn.Round, answer);
            }
            if (answer.Plays.Count > 0)
            {
                LastAiTurns = answer;
            }
        }

        /// <summary>Between turns the player ran out of moves (a power froze the last card): the
        /// hand is over for them here too.</summary>
        public override void OnOutOfMoves(RoundContext ctx)
        {
            if (Phase != DuelPhase.Playing || PlayerDone)
            {
                return;
            }
            var answer = new DuelAiTurns();
            FinishHand(ctx.Session, ctx.Round, answer);
            if (answer.Plays.Count > 0)
            {
                LastAiTurns = answer;
            }
        }

        /// <summary>The player is finished: the house plays out the rest of its half and the hand
        /// is settled.</summary>
        private void FinishHand(GameSession session, RoundEngine round, DuelAiTurns answer)
        {
            PlayerDone = true;
            int guard = 0;
            while (!AiDone && guard++ < 64)
            {
                HousePlaysOne(answer);
            }
            Settle(session, round);
        }

        /// <summary>The house's turn: the planner's move, laid on the house's arena.</summary>
        private void HousePlaysOne(DuelAiTurns answer)
        {
            if (AiRound == null || AiRound.Hand.Count == 0)
            {
                AiDone = true;
                return;
            }
            if (planner == null)
            {
                planner = new DuelPlanner();
            }
            DuelMove move = planner.Choose(AiRound);
            BlockCard card = null;
            if (move != null)
            {
                int slot = DuelMove.SlotOf(AiRound, move.CardId);
                card = slot >= 0 ? AiRound.Hand[slot] : null;
            }
            GameBoard before = GameBoard.CreateClone(AiRound.Board);
            TurnReport report = move != null ? move.ApplyTo(AiRound) : null;
            if (report == null)
            {
                AiDone = true;
                return;
            }
            var play = new DuelAiPlay
            {
                Card = card,
                Move = move,
                BoardBefore = before,
                BoardAfter = GameBoard.CreateClone(AiRound.Board),
                Gained = report.ScoreGained,
                HandScoreAfter = AiRound.RoundScore,
                ComboCount = report.ComboCount,
                CleanSweep = report.CleanSweep
            };
            if (report.PlacedCells != null)
            {
                play.PlacedCells.AddRange(report.PlacedCells);
            }
            if (report.ExplodedRows != null)
            {
                play.ExplodedRows.AddRange(report.ExplodedRows);
            }
            if (report.ExplodedColumns != null)
            {
                play.ExplodedColumns.AddRange(report.ExplodedColumns);
            }
            answer.Plays.Add(play);
            if (AiRound.Hand.Count == 0)
            {
                AiDone = true;
            }
        }

        /// <summary>Compares the two halves, pays the bet, and decides the stage.</summary>
        private void Settle(GameSession session, RoundEngine round)
        {
            int playerScore = round.RoundScore - HandStartScore;
            int houseScore = HouseHandScore;
            DuelHandOutcome outcome = playerScore > houseScore ? DuelHandOutcome.Win
                : playerScore == houseScore ? DuelHandOutcome.Push : DuelHandOutcome.Lose;
            long payout = outcome == DuelHandOutcome.Win ? Bet * 2
                : outcome == DuelHandOutcome.Push ? Bet : 0;
            session.PayDuel(payout);
            var result = new DuelHandResult
            {
                HandNumber = HandNumber,
                Bet = Bet,
                PlayerScore = playerScore,
                AiScore = houseScore,
                Outcome = outcome,
                Payout = payout,
                PurseAfter = session.TotalScore,
                Target = Target
            };
            Bet = 0;
            // What the player could not place goes back to the table with the rest.
            round.SetAsideForDuel();
            if (session.TotalScore >= Target)
            {
                result.StageWon = true;
                Phase = DuelPhase.Won;
                LastHand = result;
                round.DeclareRoundWon();
                return;
            }
            if (session.TotalScore <= 0)
            {
                result.Bankrupt = true;
                Phase = DuelPhase.Lost;
                LastHand = result;
                round.DeclareLoss(LossReason.DuelBankrupt);
                return;
            }
            Phase = DuelPhase.AwaitingBet;
            LastHand = result;
        }

        // ------------------------------------------------------------------ saving

        internal override void SaveExtra(SaveWriter w, string key, CardTable cards)
        {
            w.Write(key + ".house", AiRound != null);
            if (AiRound != null)
            {
                AiRound.Save(w, key + ".houseRound", cards);
            }
        }

        internal override void LoadExtra(SaveReader r, string key, CardTable cards, RoundEngine round)
        {
            AiRound = null;
            if (!r.ReadBool(key + ".house"))
            {
                return;
            }
            AiRound = RoundEngine.Load(r, key + ".houseRound", cards, new RoundRules(),
                RoundEngine.PlanningRandom, round.Scorer, null, null);
            AiRound.SeatAtDuelTable();
        }
    }
}
