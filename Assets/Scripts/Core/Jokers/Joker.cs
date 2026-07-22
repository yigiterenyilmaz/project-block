// PURPOSE: The joker base type. All hooks are virtual no-ops; a concrete joker
// overrides only what it needs. JokerInventory is the only thing that calls the
// hooks. Subclass, override, and register in JokerRegistry to add one.

namespace ProjectBlock.Core
{
    /// <summary>A joker the player owns. Subclass and override only the hooks you need.</summary>
    public abstract class Joker
    {
        protected Joker(string defId, string displayName)
        {
            DefId = defId;
            DisplayName = displayName;
        }

        /// <summary>Stable content id ("domuz_kumbarasi"). This is the save/replay key -
        /// never rename it, even if DisplayName changes.</summary>
        public string DefId { get; }

        /// <summary>Human-readable name for the UI (Turkish).</summary>
        public string DisplayName { get; }

        /// <summary>One-line rules text for the UI in the ACTIVE language (see Loc).
        /// Subclasses set both languages via SetDescription; reading is always live, so a
        /// language switch takes effect on the next UI refresh.</summary>
        public string Description
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

        /// <summary>Short live state for the UI ("seri 3", "x4"), or null if the joker has
        /// nothing to show. Charges and sell value are rendered generically, not here.</summary>
        public virtual string StatusText
        {
            get { return null; }
        }

        /// <summary>Unique within the session. Assigned by JokerInventory on acquisition,
        /// so two copies of the same joker are still distinguishable.</summary>
        public int InstanceId { get; internal set; }

        /// <summary>Legendary jokers are the powerful pile-rewriters (Oryantasyon, İmitasyon,
        /// Dezenformasyon, Fraksiyon). At most ONE legendary may be held at a time; the market
        /// and the debug picker enforce that through GameSession. Set in the constructor.</summary>
        public bool IsLegendary { get; protected set; }

        // ---------------------------------------------------------------- sell value

        /// <summary>Base price the market will buy this joker back for.</summary>
        public int BaseSellValue { get; protected set; } = 25;

        /// <summary>Value the joker earned by itself (the three kumbara jokers).</summary>
        public int AccruedValue { get; private set; }

        /// <summary>Extra price put on this joker by "ihale". Written from outside.</summary>
        public int AuctionPremium { get; internal set; }

        /// <summary>What the market pays for this joker right now.</summary>
        public int SellValue
        {
            get { return BaseSellValue + AccruedValue + AuctionPremium; }
        }

        /// <summary>Grows SellValue. The kumbara jokers call this from their hooks.</summary>
        protected void Accrue(int amount)
        {
            AccruedValue += amount;
        }

        // ------------------------------------------------------------- round charges

        /// <summary>Uses per round for an activated joker. 0 = passive joker.</summary>
        public int ChargesPerRound { get; protected set; }

        /// <summary>Uses left this round. Reset centrally at round start.</summary>
        public int ChargesLeft { get; private set; }

        internal void ResetCharges()
        {
            ChargesLeft = ChargesPerRound;
        }

        /// <summary>Spends one charge if any is left. Returns false when empty.</summary>
        protected bool TrySpendCharge()
        {
            if (ChargesLeft <= 0)
            {
                return false;
            }
            ChargesLeft--;
            return true;
        }

        // -------------------------------------------------------------------- gating

        /// <summary>True = every hook is skipped while the round is in overtime.
        /// Used by "Kayit defteri" and "Seri tetik"; checked by the inventory, not here.</summary>
        public virtual bool DisabledInOvertime
        {
            get { return false; }
        }

        /// <summary>Non-null once Parazit has bound this joker to a cube. Reserved:
        /// dispatch treats null as "always active", which is every joker today.</summary>
        public CubeAttachment? Attachment { get; internal set; }

        /// <summary>What Activate needs in its ActivationTarget. None for passive jokers.</summary>
        public virtual ActivationTargeting Targeting
        {
            get { return ActivationTargeting.None; }
        }

        /// <summary>True if the player can activate this joker right now. Activated jokers
        /// override this; passive ones stay false and the UI hides the button.</summary>
        public virtual bool CanActivate(RoundContext ctx)
        {
            return false;
        }

        /// <summary>Runs the player-triggered ability. Returns false if it could not run.
        /// Implementations must call TrySpendCharge themselves.</summary>
        public virtual bool Activate(RoundContext ctx, ActivationTarget target)
        {
            return false;
        }

        // -------------------------------------------------------- session / round life

        /// <summary>Bought or granted. Apply permanent rule changes here (Seri tetik: +2 hand).</summary>
        public virtual void OnAcquired(SessionContext ctx)
        {
        }

        /// <summary>Sold or destroyed. MUST undo whatever OnAcquired changed.</summary>
        public virtual void OnRemoved(SessionContext ctx)
        {
        }

        /// <summary>Another joker left the inventory (ihale re-auctions on this).</summary>
        public virtual void OnJokerRemoved(SessionContext ctx, Joker other)
        {
        }

        /// <summary>A new round started. Charges are already reset when this runs.</summary>
        public virtual void OnRoundStarted(RoundContext ctx)
        {
        }

        /// <summary>Overtime just began (the round score crossed the threshold). Fires ONCE,
        /// for every joker INCLUDING overtime-disabled ones, because it is the transition
        /// itself - the moment a joker like Seri Tetik must undo a permanent rule change
        /// before its hooks go silent. Never fires again until the next round.</summary>
        public virtual void OnOvertimeStarted(RoundContext ctx)
        {
        }

        /// <summary>The round ended, either way. Check the outcome before paying out.</summary>
        public virtual void OnRoundEnded(RoundContext ctx, RoundOutcome outcome)
        {
        }

        /// <summary>Last chance to change a round's setup before its board is built -
        /// a joker may hand the round extra playable cells here.</summary>
        public virtual RoundConfig FilterRoundConfig(SessionContext ctx, RoundConfig config)
        {
            return config;
        }

        // ------------------------------------------------------------ market (reserved)

        /// <summary>Reserved: market phase opened.</summary>
        public virtual void OnMarketEntered(SessionContext ctx)
        {
        }

        /// <summary>Market left. "Damlaya damlaya" watches anythingPurchased.</summary>
        public virtual void OnMarketLeft(SessionContext ctx, bool anythingPurchased)
        {
        }

        /// <summary>Rewrites a card the market is about to offer, before it is priced
        /// ("Simya" giving elemental blocks a second element). Return the card unchanged to
        /// leave the offer alone; the replacement MUST keep the same Id.</summary>
        public virtual BlockCard FilterMarketOffer(SessionContext ctx, BlockCard card)
        {
            return card;
        }

        /// <summary>Reserved: a power was used. Powerbank refills one use here.</summary>
        public virtual void OnPowerUsed(RoundContext ctx, string powerId)
        {
        }

        // ------------------------------------------------------------------ in-turn

        /// <summary>After full lines exploded, before the clean-sweep check. Extra cubes may
        /// still be destroyed here (Tutustur, Enfeksiyon, Kayit defteri).</summary>
        public virtual void AfterLineExplosion(TurnContext turn)
        {
        }

        /// <summary>The clean sweep ("temizlik") fired this turn. Single central event -
        /// joker-triggered sweeps raise it too, so listeners never re-implement the check.</summary>
        public virtual void AfterCleanSweep(TurnContext turn)
        {
        }

        /// <summary>The scoring hook: add flat bonuses and multipliers for this turn.
        /// Runs once, after every base value is known and before the score is banked.</summary>
        public virtual void ModifyScore(TurnContext turn)
        {
        }

        /// <summary>End of turn: the card is gone and the hand is refilled, but the
        /// threshold has NOT been checked yet - score added here still counts toward it.</summary>
        public virtual void AfterTurnScored(TurnContext turn)
        {
        }

        /// <summary>The board filled up and nothing in hand fits - the round is about to be
        /// lost. A joker with a way to open a gap ("Deprem") acts here and returns true; the
        /// engine then re-checks for a legal move. Return false to let the loss stand.</summary>
        public virtual bool TryRescueFromDeadEnd(RoundContext ctx)
        {
            return false;
        }

        public override string ToString()
        {
            return DisplayName + "#" + InstanceId;
        }
    }
}
