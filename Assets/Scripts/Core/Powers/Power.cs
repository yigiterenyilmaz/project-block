// PURPOSE: Base type of every power ("güç"). Powers are the ACTIVE counterpart of jokers:
// a joker sits there and bends the rules, a power is spent.
//
// THE FOUR CENTRAL RULES, all enforced by PowerInventory / RoundEngine so no power has to:
//  1. ONE USE. A power holds a single charge. Spending it empties the power until it is
//     recharged - it is not a per-round allowance.
//  2. RECHARGED BY A CLEAN SWEEP, or by the start of a new round. That is the whole
//     economy of powers: they push the player toward clearing the board.
//  3. AT MOST ONE POWER PER TURN. Tracked on the round, reset when a placement resolves.
//  4. USING A POWER NEVER COSTS A TURN. Powers act between placements; nothing about the
//     turn counter, the hand refill or the loss checks treats them as a move.
//
// Powers reuse the joker activation vocabulary (ActivationTargeting / ActivationTarget), so
// the UI asks for a target the same way for both.
//
// EXTENSION POINT: the market will sell powers - add a MarketOfferKind member and branch in
// GameSession.TryBuyOffer, exactly like joker offers.

namespace ProjectBlock.Core
{
    /// <summary>A power the player owns. Subclass and override Run.</summary>
    public abstract class Power
    {
        protected Power(string defId, string displayName)
        {
            DefId = defId;
            DisplayName = displayName;
            Charged = true;
        }

        /// <summary>Stable content id ("kum_saati"). The save/replay key - never rename.</summary>
        public string DefId { get; }

        /// <summary>Human-readable name for the UI (Turkish).</summary>
        public string DisplayName { get; }

        /// <summary>One-line rules text for the UI (Turkish). Set by the subclass.</summary>
        public string Description { get; protected set; } = string.Empty;

        /// <summary>Short live state for the UI, or null when there is nothing to show.</summary>
        public virtual string StatusText
        {
            get { return null; }
        }

        /// <summary>Unique within the session; PowerInventory hands these out.</summary>
        public int InstanceId { get; internal set; }

        /// <summary>What the market pays to buy this power back.</summary>
        public int BaseSellValue { get; protected set; } = 30;

        /// <summary>False once spent. A clean sweep or a new round puts it back.</summary>
        public bool Charged { get; private set; }

        internal void Recharge()
        {
            Charged = true;
        }

        internal void Spend()
        {
            Charged = false;
        }

        /// <summary>What Run needs in its ActivationTarget.</summary>
        public virtual ActivationTargeting Targeting
        {
            get { return ActivationTargeting.None; }
        }

        /// <summary>Extra conditions beyond "charged and a round is running". Override to
        /// refuse when the power would do nothing (no ghost cubes, empty bonus hand...).</summary>
        public virtual bool CanRun(RoundContext ctx, ActivationTarget target)
        {
            return true;
        }

        /// <summary>Does the thing. Return false to refuse without spending the charge -
        /// the inventory only marks the power spent when this returns true.</summary>
        public abstract bool Run(RoundContext ctx, ActivationTarget target);

        /// <summary>Bought or granted.</summary>
        public virtual void OnAcquired(SessionContext ctx)
        {
        }

        /// <summary>Sold or destroyed. MUST undo anything OnAcquired changed.</summary>
        public virtual void OnRemoved(SessionContext ctx)
        {
        }

        /// <summary>A new round started; the power is already recharged when this runs.</summary>
        public virtual void OnRoundStarted(RoundContext ctx)
        {
        }

        /// <summary>End of a turn, for powers with a lifetime (the inflation powers count
        /// down here). Runs for every power, charged or not.</summary>
        public virtual void AfterTurnScored(TurnContext turn)
        {
        }

        /// <summary>The clean sweep fired. The recharge itself is central; override only if
        /// the power needs to do something else on a sweep.</summary>
        public virtual void AfterCleanSweep(TurnContext turn)
        {
        }

        public override string ToString()
        {
            return DisplayName + "#" + InstanceId + (Charged ? "" : " (spent)");
        }
    }
}
