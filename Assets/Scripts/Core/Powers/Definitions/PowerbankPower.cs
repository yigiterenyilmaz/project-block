// PURPOSE: "Powerbank" - the power that refills another power. It used to be a joker with one
// charge a round; as a power it follows the power rules instead: one charge, refilled by a clean
// sweep or a new round, and it counts as the turn's power use (so the power it refills is ready
// from the NEXT turn - a battery charges, it does not fire the thing it charged).
//
// All numbers are BALANCE PLACEHOLDERS.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Powerbank" - puts one of your SPENT powers back on charge. The player picks
    /// which; with no pick it takes the first spent one in inventory order.</summary>
    public sealed class PowerbankPower : Power
    {
        public PowerbankPower()
            : base("powerbank", "Powerbank")
        {
            SetDescription(
                "Recharges one of your spent powers without waiting for a clean sweep. The "
                    + "recharged power can be used from the next turn.",
                "Harcanmış güçlerinden birini temizlik beklemeden doldurur. Doldurulan güç "
                    + "sonraki turdan itibaren kullanılabilir.");
        }

        /// <summary>The power a target names, for the picker: its instance id rides in
        /// HandIndex (<see cref="ActivationTarget.PowerChoice"/>).</summary>
        private static Power Chosen(RoundContext ctx, ActivationTarget target, Power self)
        {
            IReadOnlyList<Power> powers = ctx.Session.Powers.Powers;
            if (target.HandIndex.HasValue)
            {
                Power named = ctx.Session.Powers.Find(target.HandIndex.Value);
                return named != null && named != self && !named.Charged ? named : null;
            }
            for (int i = 0; i < powers.Count; i++)
            {
                if (powers[i] != self && !powers[i].Charged)
                {
                    return powers[i];
                }
            }
            return null;
        }

        /// <summary>Refuses when there is nothing spent to refill, or a boss has cut the
        /// recharge off - either way the charge is kept rather than wasted.</summary>
        public override bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return ctx.Round != null && !ctx.Round.PowerRechargeBlocked
                && Chosen(ctx, target, this) != null;
        }

        public override bool Run(RoundContext ctx, ActivationTarget target)
        {
            Power chosen = Chosen(ctx, target, this);
            return chosen != null && ctx.Session.Powers.Recharge(chosen.InstanceId);
        }
    }
}
