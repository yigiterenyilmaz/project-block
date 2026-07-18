// PURPOSE: The two jokers that trade safety for power: Seri tetik (a bigger hand that
// churns itself every turn) and Batak (an optional bet that can lose the run outright).
//
// Batak is the first joker that can END a round, through RoundEngine.DeclareLoss. It obeys
// the standing rule that a pending advance offer outranks a same-turn loss, so a player who
// earns an offer on the turn their bet expires can still escape by advancing.
//
// All numbers are BALANCE PLACEHOLDERS.

using System;

namespace ProjectBlock.Core
{
    /// <summary>"Seri tetik" - hand size +2, but every turn ends by throwing the unused hand
    /// away and drawing a fresh one. Off in overtime, where the discard is not recycled and
    /// churning the hand would just mill the player to death.</summary>
    public sealed class SeriTetikJoker : Joker
    {
        public int ExtraHandSize = 2;

        private bool applied;

        public SeriTetikJoker()
            : base("seri_tetik", "Seri Tetik")
        {
            Description = "El boyutu +2, ama her tur sonunda kullanılmayan kartlar ıskartaya "
                + "gidip yenileri gelir. Uzatmada çalışmaz.";
            BaseSellValue = 55;
        }

        public override bool DisabledInOvertime
        {
            get { return true; }
        }

        public override string StatusText
        {
            get { return applied ? "el +" + ExtraHandSize : "kapalı"; }
        }

        public override void OnAcquired(SessionContext ctx)
        {
            Apply(ctx.Rules);
        }

        public override void OnRemoved(SessionContext ctx)
        {
            Revert(ctx.Rules);
        }

        /// <summary>Re-asserted every round: the threshold pass reverts the bonus mid-round,
        /// and the next round has to start from the boosted size again.</summary>
        public override void OnRoundStarted(RoundContext ctx)
        {
            Apply(ctx.Rules);
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            // CONFIRMED: the joker switches off the moment the threshold falls, and the turn
            // that passed it does NOT churn - otherwise the player loses a hand they earned.
            if (turn.Report.ThresholdJustPassed)
            {
                Revert(turn.Rules);
                return;
            }
            // A bonus-hand play never touched the hand, so there is nothing to churn.
            if (turn.Report.PlayedFromBonusHand)
            {
                return;
            }
            turn.Round.CycleHandWithoutReshuffle();
        }

        private void Apply(RoundRules rules)
        {
            if (applied)
            {
                return;
            }
            rules.HandSize += ExtraHandSize;
            applied = true;
        }

        private void Revert(RoundRules rules)
        {
            if (!applied)
            {
                return;
            }
            rules.HandSize -= ExtraHandSize;
            applied = false;
            // The engine never trims an oversized hand on purpose: the extra cards stay
            // playable and the hand shrinks naturally as they are used.
        }
    }

    /// <summary>
    /// "Batak" - before playing, the player may bet that the next clean sweep comes within
    /// N turns. Miss it and the round is lost; make it and the score earned since the bet is
    /// paid again, multiplied by how bold the call was.
    ///
    /// PAYOUT CURVE: a bet of N turns is worth MaxMultiplier * (1 - (N-1)/(ZeroAtTurns-1)),
    /// i.e. "1 turn" pays the most and a bet at or beyond ZeroAtTurns pays nothing. Clearing
    /// EARLY pays pro rata (bet 7, cleared in 3 -> 3/7 of the 7-turn reward), which is the
    /// designer's confirmed rule.
    /// </summary>
    public sealed class BatakJoker : Joker
    {
        /// <summary>Multiplier for the boldest possible call (1 turn).</summary>
        public double MaxMultiplier = 3.0;

        /// <summary>Bets this long or longer are worth nothing.</summary>
        public int ZeroAtTurns = 100;

        /// <summary>Turns bet on, or 0 when no bet is running.</summary>
        public int BetTurns { get; private set; }

        /// <summary>Turns already spent against the active bet.</summary>
        public int TurnsElapsed { get; private set; }

        /// <summary>Round score when the bet was placed; the payout is based on the gain
        /// since then.</summary>
        private int scoreAtBet;

        public BatakJoker()
            : base("batak", "Batak")
        {
            Description = "İstersen 'şu kadar turda temizlerim' diye bahse girersin. "
                + "Tutturamazsan raunt biter, tutturursan aradaki puanı katlayarak alırsın.";
            BaseSellValue = 65;
        }

        /// <summary>Turns bet when the joker is triggered through the generic activation
        /// path (a hotkey). A real bet UI should call PlaceBet with the player's number.</summary>
        public int DefaultBetTurns = 5;

        public bool HasActiveBet
        {
            get { return BetTurns > 0; }
        }

        public override bool CanActivate(RoundContext ctx)
        {
            return !HasActiveBet && ctx.Round.Status == RoundStatus.InProgress;
        }

        public override bool Activate(RoundContext ctx, ActivationTarget target)
        {
            return PlaceBet(ctx, DefaultBetTurns);
        }

        public override string StatusText
        {
            get { return HasActiveBet ? "bahis " + (BetTurns - TurnsElapsed) + " tur" : "bahis yok"; }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            ClearBet();
        }

        /// <summary>Places a bet. Only legal while a round is running and no bet is open;
        /// the UI calls this rather than the generic Activate, because it needs a number.</summary>
        public bool PlaceBet(RoundContext ctx, int turns)
        {
            if (HasActiveBet || turns < 1 || ctx.Round.Status != RoundStatus.InProgress)
            {
                return false;
            }
            BetTurns = turns;
            TurnsElapsed = 0;
            scoreAtBet = ctx.Round.RoundScore;
            return true;
        }

        /// <summary>Reward for clearing in <paramref name="usedTurns"/> against a bet of
        /// <paramref name="betTurns"/>, applied to the score gained in between.</summary>
        public int PayoutFor(int betTurns, int usedTurns, int scoreGained)
        {
            if (betTurns <= 0 || scoreGained <= 0)
            {
                return 0;
            }
            double boldness = ZeroAtTurns > 1
                ? 1.0 - (betTurns - 1) / (double)(ZeroAtTurns - 1)
                : 1.0;
            if (boldness <= 0.0)
            {
                return 0;
            }
            // Finishing early only earns the fraction of the window actually used.
            double earliness = usedTurns / (double)betTurns;
            return (int)Math.Floor(scoreGained * MaxMultiplier * boldness * earliness);
        }

        public override void AfterCleanSweep(TurnContext turn)
        {
            if (!HasActiveBet)
            {
                return;
            }
            int usedTurns = TurnsElapsed + 1; // this turn closes the window
            int gained = turn.Round.RoundScore + turn.Score.Total - scoreAtBet;
            int payout = PayoutFor(BetTurns, usedTurns, gained);
            ClearBet();
            if (payout > 0)
            {
                turn.AddFlatScore(payout, DefId);
            }
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (!HasActiveBet)
            {
                return;
            }
            TurnsElapsed++;
            if (TurnsElapsed < BetTurns)
            {
                return;
            }
            ClearBet();
            // The engine decides what a loss means here: a pending advance offer still wins,
            // exactly like every other same-turn loss.
            turn.Round.DeclareLoss(LossReason.BetFailed);
        }

        /// <summary>Taking the advance offer cancels an open bet - no reward, no penalty.</summary>
        public override void OnRoundEnded(RoundContext ctx, RoundOutcome outcome)
        {
            ClearBet();
        }

        private void ClearBet()
        {
            BetTurns = 0;
            TurnsElapsed = 0;
            scoreAtBet = 0;
        }
    }
}
