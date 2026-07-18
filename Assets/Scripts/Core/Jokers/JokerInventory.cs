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

        /// <summary>Sells a joker for its SellValue, which is added to the run score/currency.
        /// EXTENSION POINT: the real market will call this; it works today for debugging.</summary>
        public int Sell(Joker joker)
        {
            if (joker == null)
            {
                return 0;
            }
            int value = joker.SellValue;
            if (!Remove(joker))
            {
                return 0;
            }
            session.AddCurrency(value);
            return value;
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
            if (!joker.CanActivate(ctx))
            {
                return false;
            }
            bool ran = joker.Activate(ctx, target);
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

        public void DispatchRoundEnded(RoundEngine round, RoundOutcome outcome)
        {
            RoundContext ctx = RoundCtx(round);
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                // NOT gated on overtime: a round that ends in overtime must still pay out
                // round-end effects, otherwise every overtime round silently loses them.
                dispatchBuffer[i].OnRoundEnded(ctx, outcome);
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
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                if (!IsGated(dispatchBuffer[i], turn.Round))
                {
                    dispatchBuffer[i].AfterLineExplosion(turn);
                }
            }
        }

        public void AfterCleanSweep(TurnContext turn)
        {
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                if (!IsGated(dispatchBuffer[i], turn.Round))
                {
                    dispatchBuffer[i].AfterCleanSweep(turn);
                }
            }
        }

        public void ModifyScore(TurnContext turn)
        {
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                if (!IsGated(dispatchBuffer[i], turn.Round))
                {
                    dispatchBuffer[i].ModifyScore(turn);
                }
            }
        }

        public void AfterTurnScored(TurnContext turn)
        {
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                if (!IsGated(dispatchBuffer[i], turn.Round))
                {
                    dispatchBuffer[i].AfterTurnScored(turn);
                }
            }
            RaiseChanged();
        }

        // ---------------------------------------------------------------------- internals

        /// <summary>The central overtime gate - see the file header.</summary>
        private static bool IsGated(Joker joker, RoundEngine round)
        {
            return joker.DisabledInOvertime && round != null && round.ThresholdPassed;
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
