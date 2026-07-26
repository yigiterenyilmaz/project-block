// PURPOSE: The player's jokers and the ONE place they are called from. Session-scoped
// (it outlives every RoundEngine) and the single implementation of ITurnHooks.
//
// TWO RULES THIS FILE ENFORCES SO NO JOKER HAS TO:
//  1. ORDER: every dispatch walks the inventory left to right (acquisition order). That is
//     the game's canonical ordering rule - score, sweep listeners, end-of-turn effects.
//  2. OVERTIME GATING: a joker with DisabledInOvertime is skipped entirely once the round
//     threshold is passed. Jokers never test for overtime themselves.
//
// Dispatch iterates a snapshot, so a joker may remove another joker (future: Parazit's
// host cube dying) without corrupting the walk.
//
// EXTENSION POINT: the market will call Add/Sell here; "ihale" writes AuctionPremium
// through SetAuctionPremium and watches OnJokerRemoved to re-auction.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Owns the player's jokers and dispatches every joker hook.</summary>
    public sealed class JokerInventory : ITurnHooks
    {
        private readonly List<Joker> jokers = new List<Joker>();
        private readonly List<Joker> dispatchBuffer = new List<Joker>();
        private readonly GameSession session;
        private readonly IRandomSource rng;
        private int nextInstanceId = 1;

        /// <summary>Fires whenever the joker list changes, for the UI.</summary>
        public event Action Changed;

        public JokerInventory(GameSession session, IRandomSource rng)
        {
            this.session = session;
            this.rng = rng;
        }

        /// <summary>The jokers, in canonical order (left to right).</summary>
        public IReadOnlyList<Joker> Jokers
        {
            get { return jokers; }
        }

        public int Count
        {
            get { return jokers.Count; }
        }

        /// <summary>How many jokers the player may hold at once. The market refuses to sell
        /// past this; Add itself stays uncapped so debug grants and tests are unaffected.
        /// Balance placeholder.</summary>
        public int MaxSlots = 5;

        /// <summary>Jokers that actually take up a slot. A joker bound to a block by
        /// "Parazit" rides along on the card instead, which is exactly what that joker buys
        /// you - Parazit itself keeps occupying its own slot.</summary>
        public int OccupiedSlots
        {
            get
            {
                int used = 0;
                for (int i = 0; i < jokers.Count; i++)
                {
                    if (!jokers[i].Attachment.HasValue)
                    {
                        used++;
                    }
                }
                return used;
            }
        }

        /// <summary>True when every joker slot is taken (the market can sell no more).</summary>
        public bool IsFull
        {
            get { return OccupiedSlots >= MaxSlots; }
        }

        /// <summary>Instance id of the joker "ihale" is currently auctioning, if any.</summary>
        public int? ActiveAuctionInstanceId { get; internal set; }

        public Joker Find(int instanceId)
        {
            for (int i = 0; i < jokers.Count; i++)
            {
                if (jokers[i].InstanceId == instanceId)
                {
                    return jokers[i];
                }
            }
            return null;
        }

        // ------------------------------------------------------------- acquire / remove

        /// <summary>Adds a joker and runs its OnAcquired. Charges start full.</summary>
        public Joker Add(Joker joker)
        {
            if (joker == null)
            {
                throw new ArgumentNullException("joker");
            }
            joker.InstanceId = nextInstanceId++;
            jokers.Add(joker);
            joker.ResetCharges();
            joker.OnAcquired(SessionCtx());
            RaiseChanged();
            return joker;
        }

        /// <summary>Removes a joker (sale, destruction). Runs OnRemoved so it can undo any
        /// permanent rule change, then tells the other jokers.</summary>
        public bool Remove(Joker joker)
        {
            if (joker == null || !jokers.Remove(joker))
            {
                return false;
            }
            joker.OnRemoved(SessionCtx());
            if (ActiveAuctionInstanceId == joker.InstanceId)
            {
                ActiveAuctionInstanceId = null;
            }
            SessionContext ctx = SessionCtx();
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                dispatchBuffer[i].OnJokerRemoved(ctx, joker);
            }
            RaiseChanged();
            return true;
        }

        /// <summary>True while any held joker lets the player buy on credit ("Kredi kartı").</summary>
        public bool GrantsMarketCredit
        {
            get
            {
                for (int i = 0; i < jokers.Count; i++)
                {
                    if (jokers[i].GrantsMarketCredit)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>Interest the debt accrues per round, in percent - the highest any held credit
        /// joker charges. 0 when none is held.</summary>
        public int MarketCreditInterestPercent
        {
            get
            {
                int highest = 0;
                for (int i = 0; i < jokers.Count; i++)
                {
                    if (jokers[i].GrantsMarketCredit
                        && jokers[i].MarketCreditInterestPercent > highest)
                    {
                        highest = jokers[i].MarketCreditInterestPercent;
                    }
                }
                return highest;
            }
        }

        /// <summary>True while any held joker lets the player smuggle goods out of the market
        /// ("Kaçakçı"). A BROKEN smuggler smuggles nothing - IsGated covers dispatch, and this
        /// covers the market, where there is no round to gate against.</summary>
        public bool EnablesSmuggling
        {
            get { return FindSmuggler() != null; }
        }

        /// <summary>Chance in percent that smuggled goods are defective - the WORST (highest) any
        /// working smuggler names, because the goods come off one lorry. 0 when none is held.</summary>
        public int SmuggleDefectChancePercent
        {
            get
            {
                Joker smuggler = FindSmuggler();
                return smuggler != null ? smuggler.SmuggleDefectChancePercent : 0;
            }
        }

        /// <summary>Recharge events a defective smuggled power needs, from the same joker.</summary>
        public int SmuggledPowerRechargeCost
        {
            get
            {
                Joker smuggler = FindSmuggler();
                return smuggler != null ? smuggler.SmuggledPowerRechargeCost : 1;
            }
        }

        /// <summary>The working smuggler with the worst goods, or null. Inventory order breaks a
        /// tie, like every other walk.</summary>
        private Joker FindSmuggler()
        {
            Joker worst = null;
            for (int i = 0; i < jokers.Count; i++)
            {
                if (!jokers[i].EnablesSmuggling
                    || jokers[i].Defect == SmuggledDefect.NeverWorks)
                {
                    continue;
                }
                if (worst == null
                    || jokers[i].SmuggleDefectChancePercent > worst.SmuggleDefectChancePercent)
                {
                    worst = jokers[i];
                }
            }
            return worst;
        }

        /// <summary>True while any held joker unlocks the InvestorOnly powers ("Uzun vadeli
        /// yatırımcı"). Asked centrally when the final round starts.</summary>
        public bool UnlocksInvestorPowers
        {
            get
            {
                for (int i = 0; i < jokers.Count; i++)
                {
                    if (jokers[i].UnlocksInvestorPowers)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Spends the first unused second chance in the inventory and says whose it was, or null
        /// when nobody has one left. Inventory order, like every other dispatch, so with two of
        /// them the leftmost pays first.
        ///
        /// This is the ONLY way a retry is consumed, and the caller (GameSession, on a lost final
        /// round) is the only thing that knows when asking is legal.
        /// </summary>
        internal Joker TryConsumeFinalRoundRetry()
        {
            for (int i = 0; i < jokers.Count; i++)
            {
                if (jokers[i].ConsumeFinalRoundRetry())
                {
                    return jokers[i];
                }
            }
            return null;
        }

        /// <summary>False while selling this joker would let the player walk away from what they
        /// borrowed through it: a credit joker is locked until the debt is back to zero. Some
        /// jokers are locked outright ("Uzun vadeli yatırımcı" is an investment, not a trade). The
        /// UI asks this so it can grey the sale out rather than silently refusing it.</summary>
        public bool CanSell(Joker joker)
        {
            return joker != null
                && !joker.NeverSellable
                && !(joker.GrantsMarketCredit && session.Debt > 0);
        }

        /// <summary>Sells a joker for its SellValue, which is added to the run score/currency.
        /// Returns 0 and changes nothing when the sale is refused (see CanSell).
        /// EXTENSION POINT: the real market will call this; it works today for debugging.</summary>
        public int Sell(Joker joker)
        {
            if (joker == null || !CanSell(joker))
            {
                return 0;
            }
            // The joker prices its own sale: normally SellValue scaled, but "Yer altı
            // kaynakları" refunds what you paid once it is spent.
            long value = joker.SellPriceScaled(session.Config.Scoring.ScoreScale);
            if (!Remove(joker))
            {
                return 0;
            }
            session.AddCurrency(value);
            return (int)value;
        }

        /// <summary>"ihale" uses this to put an extra price on a joker.</summary>
        public void SetAuctionPremium(Joker joker, int premium)
        {
            joker.AuctionPremium = premium;
            ActiveAuctionInstanceId = premium > 0 ? joker.InstanceId : (int?)null;
            RaiseChanged();
        }

        // ---------------------------------------------------------------- activation

        /// <summary>True if that joker can be activated right now.</summary>
        public bool CanActivate(int instanceId)
        {
            Joker joker = Find(instanceId);
            RoundEngine round = session.CurrentRound;
            if (joker == null || round == null)
            {
                return false;
            }
            if (IsGated(joker, round))
            {
                return false;
            }
            return joker.CanActivate(RoundCtx(round));
        }

        /// <summary>Runs a player-activated joker. Returns false if it was not usable.</summary>
        public bool TryActivate(int instanceId, ActivationTarget target)
        {
            Joker joker = Find(instanceId);
            RoundEngine round = session.CurrentRound;
            if (joker == null || round == null || IsGated(joker, round))
            {
                return false;
            }
            RoundContext ctx = RoundCtx(round);
            // "Öteki dünya": aim the round at the world the player picked for the whole
            // activation, so a joker that asks for "the board" gets the one it was pointed at.
            // In a finally, because a throwing joker must not leave the engine looking the wrong
            // way for the rest of the round.
            bool previousWorld = round.BeginMirrorTargeting(target.OnMirrorWorld);
            bool ran;
            try
            {
                if (!joker.CanActivate(ctx))
                {
                    return false;
                }
                ran = joker.Activate(ctx, target);
            }
            finally
            {
                round.EndMirrorTargeting(previousWorld);
            }
            if (ran)
            {
                RaiseChanged();
            }
            return ran;
        }

        // ------------------------------------------------------------ round lifecycle

        /// <summary>Runs before a round's board exists, so a joker can resize it.</summary>
        public RoundConfig FilterRoundConfig(RoundConfig config)
        {
            SessionContext ctx = SessionCtx();
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                config = dispatchBuffer[i].FilterRoundConfig(ctx, config);
            }
            return config;
        }

        /// <summary>Resets every joker's charges, then dispatches OnRoundStarted.</summary>
        public void DispatchRoundStarted(RoundEngine round)
        {
            RoundContext ctx = RoundCtx(round);
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                dispatchBuffer[i].ResetCharges();
            }
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                if (!IsGated(dispatchBuffer[i], round))
                {
                    dispatchBuffer[i].OnRoundStarted(ctx);
                }
            }
            RaiseChanged();
        }

        /// <summary>Overtime began. Deliberately NOT gated on DisabledInOvertime: this is the
        /// transition, the last moment an overtime-disabled joker gets to react (Seri Tetik
        /// takes its hand-size bonus back here).</summary>
        public void DispatchOvertimeStarted(RoundEngine round)
        {
            RoundContext ctx = RoundCtx(round);
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                dispatchBuffer[i].OnOvertimeStarted(ctx);
            }
            RaiseChanged();
        }

        public void DispatchRoundEnded(RoundEngine round, RoundOutcome outcome)
        {
            RoundContext ctx = RoundCtx(round);
            Snapshot();
            // The piggy banks take their per-round cut here, so the "Terslik" window has to
            // reach this far too - otherwise the boss would leak the one payout that matters
            // most. No breakdown: the round is over, there is no turn score left to invert.
            bool inverted = BeginInversion(round, null);
            try
            {
                for (int i = 0; i < dispatchBuffer.Count; i++)
                {
                    // NOT gated on overtime: a round that ends in overtime must still pay out
                    // round-end effects, otherwise every overtime round silently loses them.
                    dispatchBuffer[i].OnRoundEnded(ctx, outcome);
                }
            }
            finally
            {
                EndInversion(inverted, null);
            }
            RaiseChanged();
        }

        public void DispatchMarketEntered()
        {
            SessionContext ctx = SessionCtx();
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                dispatchBuffer[i].OnMarketEntered(ctx);
            }
        }

        /// <summary>Runs the market-offer filter through every joker, in inventory order.</summary>
        public BlockCard FilterMarketOffer(BlockCard card)
        {
            SessionContext ctx = SessionCtx();
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                card = dispatchBuffer[i].FilterMarketOffer(ctx, card) ?? card;
            }
            return card;
        }

        /// <summary>A power was used. "Powerbank" is what this hook was reserved for.</summary>
        public void DispatchPowerUsed(RoundEngine round, string powerId)
        {
            RoundContext ctx = RoundCtx(round);
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                if (!IsGated(dispatchBuffer[i], round))
                {
                    dispatchBuffer[i].OnPowerUsed(ctx, powerId);
                }
            }
            RaiseChanged();
        }

        public void DispatchMarketLeft(bool anythingPurchased)
        {
            SessionContext ctx = SessionCtx();
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                dispatchBuffer[i].OnMarketLeft(ctx, anythingPurchased);
            }
        }

        // ------------------------------------------------------------------ ITurnHooks

        public void AfterLineExplosion(TurnContext turn)
        {
            Snapshot();
            bool inverted = BeginInversion(turn.Round, turn.Score);
            try
            {
                for (int i = 0; i < dispatchBuffer.Count; i++)
                {
                    if (!IsGated(dispatchBuffer[i], turn.Round))
                    {
                        dispatchBuffer[i].AfterLineExplosion(turn);
                    }
                }
            }
            finally
            {
                EndInversion(inverted, turn.Score);
            }
        }

        public void AfterCleanSweep(TurnContext turn)
        {
            Snapshot();
            bool inverted = BeginInversion(turn.Round, turn.Score);
            try
            {
                for (int i = 0; i < dispatchBuffer.Count; i++)
                {
                    if (!IsGated(dispatchBuffer[i], turn.Round))
                    {
                        dispatchBuffer[i].AfterCleanSweep(turn);
                    }
                }
            }
            finally
            {
                EndInversion(inverted, turn.Score);
            }
        }

        public void ModifyScore(TurnContext turn)
        {
            Snapshot();
            bool inverted = BeginInversion(turn.Round, turn.Score);
            try
            {
                for (int i = 0; i < dispatchBuffer.Count; i++)
                {
                    if (!IsGated(dispatchBuffer[i], turn.Round))
                    {
                        dispatchBuffer[i].ModifyScore(turn);
                    }
                }
            }
            finally
            {
                EndInversion(inverted, turn.Score);
            }
        }

        public void AfterTurnScored(TurnContext turn)
        {
            Snapshot();
            bool inverted = BeginInversion(turn.Round, turn.Score);
            try
            {
                for (int i = 0; i < dispatchBuffer.Count; i++)
                {
                    if (!IsGated(dispatchBuffer[i], turn.Round))
                    {
                        dispatchBuffer[i].AfterTurnScored(turn);
                    }
                }
            }
            finally
            {
                EndInversion(inverted, turn.Score);
            }
            RaiseChanged();
        }

        /// <summary>Gives every joker one chance to open a gap when the board has filled up.
        /// Stops at the first one that acts - the engine re-checks for a legal move after
        /// each rescue, so a second joker only fires if the first did not save the round.</summary>
        public bool TryRescueFromDeadEnd(RoundContext ctx)
        {
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                if (IsGated(dispatchBuffer[i], ctx.Round))
                {
                    continue;
                }
                if (dispatchBuffer[i].TryRescueFromDeadEnd(ctx))
                {
                    RaiseChanged();
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------------- internals

        /// <summary>
        /// Opens the "Terslik" window: while it is open, every point and every lira a joker hands
        /// out turns into a loss of the same size. Returns whether it opened, to be passed to
        /// EndInversion - always in a try/finally, so a throwing joker can never leave the game
        /// stuck paying backwards.
        ///
        /// It is opened around JOKER dispatch ONLY. Powers, the base score and the engine's own
        /// bookkeeping share the same ScoreBreakdown, and they must not be inverted with it.
        /// The score arrives through the breakdown; the sell value through the joker itself,
        /// because Accrue never touches the breakdown.
        /// </summary>
        private bool BeginInversion(RoundEngine round, ScoreBreakdown score)
        {
            if (round == null || !round.InvertsJokerScore)
            {
                return false;
            }
            if (score != null)
            {
                score.InvertContributions = true;
            }
            for (int i = 0; i < jokers.Count; i++)
            {
                jokers[i].ValueGainInverted = true;
            }
            return true;
        }

        private void EndInversion(bool opened, ScoreBreakdown score)
        {
            if (!opened)
            {
                return;
            }
            if (score != null)
            {
                score.InvertContributions = false;
            }
            for (int i = 0; i < jokers.Count; i++)
            {
                jokers[i].ValueGainInverted = false;
            }
        }

        /// <summary>The central gate - see the file header. Two reasons a joker goes quiet:
        /// overtime (its own DisabledInOvertime) and a boss round silencing it ("Anarşi",
        /// "Oburluk"). Both are checked HERE so no joker ever tests for either.</summary>
        private static bool IsGated(Joker joker, RoundEngine round)
        {
            // A joker that never worked is gated everywhere, round or no round: smuggled goods do
            // not wait for a round to be broken.
            if (joker.Defect == SmuggledDefect.NeverWorks)
            {
                return true;
            }
            if (round == null)
            {
                return false;
            }
            return (joker.DisabledInOvertime && round.ThresholdPassed)
                || round.IsSilencedByBoss(joker)
                // "Kaçakçı"'s other defect: quiet in exactly the rounds you bought it for.
                || (joker.Defect == SmuggledDefect.DeadInBossRounds && round.Config.IsBossRound);
        }

        private void Snapshot()
        {
            dispatchBuffer.Clear();
            dispatchBuffer.AddRange(jokers);
        }

        private SessionContext SessionCtx()
        {
            return new SessionContext(session, rng);
        }

        private RoundContext RoundCtx(RoundEngine round)
        {
            return new RoundContext(session, rng, round);
        }

        private void RaiseChanged()
        {
            if (Changed != null)
            {
                Changed();
            }
        }
    }
}
