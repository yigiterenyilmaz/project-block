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

        /// <summary>One-line rules text for the UI in the ACTIVE language (see Loc).
        /// Subclasses set both languages via SetDescription; reading is always live, so a
        /// language switch takes effect on the next UI refresh.</summary>
        public virtual string Description
        {
            get { return Loc.Pick(descriptionEn, descriptionTr); }
        }

        private string descriptionEn = string.Empty;
        private string descriptionTr = string.Empty;

        /// <summary>Sets the rules text in both languages.</summary>
        protected void SetDescription(string english, string turkish)
        {
            descriptionEn = english;
            descriptionTr = turkish;
        }

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

        /// <summary>A power may set this true INSIDE Run to signal the use should not spend its
        /// charge - the power did something, but this use was "free" and it stays charged.
        /// Reset before every Run by PowerInventory. Used by Eko's arming (memorising is free,
        /// only the replay costs the charge) and Olta pulling a card out of the draw pile.</summary>
        internal bool KeepChargeAfterUse { get; set; }

        /// <summary>What Run needs in its ActivationTarget.</summary>
        public virtual ActivationTargeting Targeting
        {
            get { return ActivationTargeting.None; }
        }

        /// <summary>True for a power that is only usable at the moment the board has filled
        /// up and nothing fits ("Kentsel Dönüşüm"). The engine pauses the round in
        /// AwaitingRescue when the player holds one of these; normal powers cannot be used
        /// there, and these cannot be used anywhere else.</summary>
        public virtual bool IsDeadEndRescue
        {
            get { return false; }
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

        /// <summary>Board cells this power WOULD affect for a given aim, so the UI can preview
        /// the blast while the player is targeting. Empty by default; a board-targeting power
        /// (Çaprazlama) overrides it. Cells outside the board are ignored by the view.</summary>
        public virtual System.Collections.Generic.IReadOnlyList<GridPos> PreviewCells(
            ActivationTarget target)
        {
            return System.Array.Empty<GridPos>();
        }

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

        /// <summary>Last chance to change a round's setup before its board is built. The
        /// twin of Joker.FilterRoundConfig - "Tılsım" hands over the ground it converted.</summary>
        public virtual RoundConfig FilterRoundConfig(SessionContext ctx, RoundConfig config)
        {
            return config;
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
