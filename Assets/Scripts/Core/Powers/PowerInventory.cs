// PURPOSE: The player's powers and the ONE place they are used from. Session-scoped, like
// JokerInventory, and the enforcer of the four central power rules (see Power.cs).
//
// The "at most one power per turn" budget lives on the ROUND, not here, because a turn is a
// round-level concept: RoundEngine.PowersUsedThisTurn is reset when a placement resolves.
//
// Using a power also notifies every joker through Joker.OnPowerUsed - that hook has been
// sitting unused since the joker framework landed, and "Powerbank" is what it was for.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Owns the player's powers and runs them.</summary>
    public sealed class PowerInventory
    {
        private readonly List<Power> powers = new List<Power>();
        private readonly List<Power> dispatchBuffer = new List<Power>();
        private readonly GameSession session;
        private readonly IRandomSource rng;
        private int nextInstanceId = 1;

        /// <summary>Fires whenever the list or a charge changes, for the UI.</summary>
        public event Action Changed;

        public PowerInventory(GameSession session, IRandomSource rng)
        {
            this.session = session;
            this.rng = rng;
        }

        public IReadOnlyList<Power> Powers
        {
            get { return powers; }
        }

        public int Count
        {
            get { return powers.Count; }
        }

        /// <summary>How many powers the player may hold. Separate from the joker slots -
        /// the two inventories do not compete. Balance placeholder.</summary>
        public int MaxSlots = 3;

        public bool IsFull
        {
            get { return powers.Count >= MaxSlots; }
        }

        public Power Find(int instanceId)
        {
            for (int i = 0; i < powers.Count; i++)
            {
                if (powers[i].InstanceId == instanceId)
                {
                    return powers[i];
                }
            }
            return null;
        }

        public Power Add(Power power)
        {
            if (power == null)
            {
                throw new ArgumentNullException("power");
            }
            power.InstanceId = nextInstanceId++;
            powers.Add(power);
            power.Recharge();
            power.OnAcquired(SessionCtx());
            RaiseChanged();
            return power;
        }

        public bool Remove(Power power)
        {
            if (power == null || !powers.Remove(power))
            {
                return false;
            }
            power.OnRemoved(SessionCtx());
            RaiseChanged();
            return true;
        }

        /// <summary>Sells a power for its base value, paid into the run currency.</summary>
        public int Sell(Power power)
        {
            if (power == null)
            {
                return 0;
            }
            int value = power.BaseSellValue;
            if (!Remove(power))
            {
                return 0;
            }
            session.AddCurrency(value);
            return value;
        }

        // ------------------------------------------------------------------- using

        /// <summary>Target-less usability check, for the UI to decide whether clicking a
        /// power should arm targeting mode (and how to tint its panel). Mirrors CanUse
        /// minus the target validation - a targeting power's CanRun only ever validates
        /// its target, which does not exist yet at this point.</summary>
        public bool CanBeginUse(int instanceId)
        {
            Power power = Find(instanceId);
            RoundEngine round = session.CurrentRound;
            if (power == null || round == null || !power.Charged)
            {
                return false;
            }
            if (round.Status != RoundStatus.InProgress || round.PowersUsedThisTurn > 0)
            {
                return false;
            }
            return power.Targeting != ActivationTargeting.None
                || power.CanRun(RoundCtx(round), ActivationTarget.None);
        }

        /// <summary>Every condition a power must meet to be usable right now: it exists, it
        /// is charged, a round is running, and this turn's single power slot is still free.</summary>
        public bool CanUse(int instanceId, ActivationTarget target)
        {
            Power power = Find(instanceId);
            RoundEngine round = session.CurrentRound;
            if (power == null || round == null || !power.Charged)
            {
                return false;
            }
            if (round.Status != RoundStatus.InProgress || round.PowersUsedThisTurn > 0)
            {
                return false;
            }
            return power.CanRun(RoundCtx(round), target);
        }

        /// <summary>Runs a power. The charge is only spent if the power reports success, and
        /// using one never costs a turn - it just uses up this turn's single power slot.</summary>
        public bool TryUse(int instanceId, ActivationTarget target)
        {
            if (!CanUse(instanceId, target))
            {
                return false;
            }
            Power power = Find(instanceId);
            RoundEngine round = session.CurrentRound;
            if (!power.Run(RoundCtx(round), target))
            {
                return false;
            }
            power.Spend();
            round.NotePowerUsed();
            session.Jokers.DispatchPowerUsed(round, power.DefId);
            RaiseChanged();
            return true;
        }

        /// <summary>"Olta"'s free marking action. Routed through the inventory because the
        /// UI cannot build a RoundContext itself. Deliberately NOT a "use": marking spends
        /// no charge and does not take the turn's power slot (see OltaPower.TryMark).</summary>
        public bool TryMarkOlta(int instanceId, int handIndex)
        {
            var olta = Find(instanceId) as OltaPower;
            RoundEngine round = session.CurrentRound;
            if (olta == null || round == null)
            {
                return false;
            }
            bool marked = olta.TryMark(RoundCtx(round), handIndex);
            if (marked)
            {
                RaiseChanged();
            }
            return marked;
        }

        // -------------------------------------------------------------- lifecycle

        /// <summary>Runs before a round's board exists, so a power can reshape it.</summary>
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

        /// <summary>New round: every power comes back charged, then OnRoundStarted runs.</summary>
        public void DispatchRoundStarted(RoundEngine round)
        {
            RoundContext ctx = RoundCtx(round);
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                dispatchBuffer[i].Recharge();
            }
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                dispatchBuffer[i].OnRoundStarted(ctx);
            }
            RaiseChanged();
        }

        /// <summary>The clean sweep is the powers' economy: it puts every charge back.</summary>
        public void DispatchCleanSweep(TurnContext turn)
        {
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                dispatchBuffer[i].Recharge();
                dispatchBuffer[i].AfterCleanSweep(turn);
            }
            RaiseChanged();
        }

        public void DispatchAfterTurnScored(TurnContext turn)
        {
            Snapshot();
            for (int i = 0; i < dispatchBuffer.Count; i++)
            {
                dispatchBuffer[i].AfterTurnScored(turn);
            }
            RaiseChanged();
        }

        /// <summary>"Powerbank" refills one power without a sweep. Returns false if every
        /// power is already charged, so the joker does not waste its own charge.</summary>
        public bool RechargeOne()
        {
            for (int i = 0; i < powers.Count; i++)
            {
                if (!powers[i].Charged)
                {
                    powers[i].Recharge();
                    RaiseChanged();
                    return true;
                }
            }
            return false;
        }

        private void Snapshot()
        {
            dispatchBuffer.Clear();
            dispatchBuffer.AddRange(powers);
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
