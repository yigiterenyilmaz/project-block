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

        /// <summary>
        /// True while this joker has something WAITING TO BE DONE WITH IT in the market - a pick
        /// unspent ("Hileli zar"), a binding unmade ("Parazit"). The market UI breathes a light
        /// around such a card, and that is the whole reason this is a generic question rather
        /// than the bar testing for two specific jokers: the affordance is the same one every
        /// time, so a market joker written next year gets it by overriding this.
        ///
        /// It says only that the joker is WAITING, never what clicking it does - that is still
        /// the controller's business, because only it knows which panel to open.
        ///
        /// EXTENSION POINT: a new market-phase joker overrides this alongside its own click
        /// handler in GameUiController.Bars.
        /// </summary>
        public virtual bool HasPendingMarketAction
        {
            get { return false; }
        }

        /// <summary>Unique within the session. Assigned by JokerInventory on acquisition,
        /// so two copies of the same joker are still distinguishable.</summary>
        public int InstanceId { get; internal set; }

        /// <summary>Legendary jokers are the powerful pile-rewriters (Baba Ocağı, İmitasyon,
        /// Konfüzyon, Fraksiyon). At most ONE legendary may be held at a time; the market
        /// and the debug picker enforce that through GameSession. Set in the constructor.</summary>
        public bool IsLegendary { get; protected set; }

        /// <summary>True while this joker lets the player buy in the market with points they do
        /// not have ("Kredi kartı"). GameSession asks the inventory, never a specific joker.</summary>
        public virtual bool GrantsMarketCredit
        {
            get { return false; }
        }

        /// <summary>Interest charged on the debt at the end of every round, in percent. Only
        /// meaningful on a joker that grants credit. Balance placeholder.</summary>
        public virtual int MarketCreditInterestPercent
        {
            get { return 0; }
        }

        /// <summary>True while this joker lets the player walk out of the market with goods they
        /// did not pay for ("Kaçakçı"). Like the credit joker, smuggling is a SESSION rule and this
        /// is only the switch: GameSession does the taking and the rolling, and asks the inventory
        /// rather than any particular joker.</summary>
        public virtual bool EnablesSmuggling
        {
            get { return false; }
        }

        /// <summary>Chance in percent that smuggled goods turn out to be defective. Only
        /// meaningful on a joker that enables smuggling. Balance placeholder.</summary>
        public virtual int SmuggleDefectChancePercent
        {
            get { return 0; }
        }

        /// <summary>How many recharge events a smuggled DEFECTIVE power needs instead of one.
        /// Only meaningful on a joker that enables smuggling. Balance placeholder.</summary>
        public virtual int SmuggledPowerRechargeCost
        {
            get { return 1; }
        }

        /// <summary>
        /// What is wrong with this joker, when it was smuggled ("Kaçakçı") and came out defective.
        /// None for everything that was paid for. Written once by GameSession when the goods are
        /// taken, and never cleared.
        ///
        /// It is CENTRAL, like the boss silencing it borrows: JokerInventory.IsGated is the one
        /// place that reads it, so a broken joker is simply never dispatched. Nothing is added or
        /// removed, and no joker anywhere has to ask whether it is the broken one.
        /// </summary>
        public SmuggledDefect Defect { get; internal set; }

        /// <summary>Last round whose market may stock this joker kind ("Uzun vadeli yatırımcı" is
        /// an EARLY-game bet, so it is only ever offered in the first markets). Unlimited by
        /// default. Read by GameSession.AddJokerOffers off the catalogue's sample instance; the
        /// debug picker ignores it, so every joker stays reachable for testing.</summary>
        public virtual int LastOfferableRound
        {
            get { return int.MaxValue; }
        }

        /// <summary>True while this joker unlocks the InvestorOnly powers - the ones no market ever
        /// stocks. GameSession asks the inventory when the final round starts, never a specific
        /// joker.</summary>
        public virtual bool UnlocksInvestorPowers
        {
            get { return false; }
        }

        /// <summary>
        /// Offers up this joker's one-off second chance at a LOST FINAL ROUND, and spends it.
        /// Returns false when it has none (which is every joker but "Uzun vadeli yatırımcı", and
        /// that one only once). GameSession decides WHEN to ask, so a joker never has to know
        /// which round it is - the same division of labour as the boss queries.
        /// </summary>
        internal virtual bool ConsumeFinalRoundRetry()
        {
            return false;
        }

        // ---------------------------------------------------------------- sell value

        /// <summary>True while this joker may never be sold at any price ("Uzun vadeli yatırımcı":
        /// the investment is locked in for the run). Read by JokerInventory.CanSell, alongside the
        /// credit joker's conditional lock.</summary>
        public virtual bool NeverSellable
        {
            get { return false; }
        }

        /// <summary>
        /// Last word on what this joker sells for, given what the market's formula worked out.
        /// Return <paramref name="marketValue"/> to accept it, which is what all but one joker does.
        ///
        /// It exists for "Yer altı kaynakları", which refunds EXACTLY what you paid once its seam is
        /// spent - a promise no rarity-derived formula can express. Asked by
        /// JokerInventory.SellValueOf, the one place a sale is priced.
        /// </summary>
        public virtual int OverrideSellValue(int marketValue)
        {
            return marketValue;
        }

        // ---------------------------------------------------------------- proc statistics

        /// <summary>
        /// HOW OFTEN THIS JOKER HAS ACTUALLY FIRED, and what it has paid while doing it - kept
        /// for the WHOLE RUN, because "has this earned its slot" is a question about the run and
        /// not about the round you happen to be in.
        ///
        /// It is opt-in rather than derived from the score log, and deliberately so: half the
        /// interesting jokers pay nothing at the moment they fire ("Mikrodalga" bends a rule and
        /// the score it saves lands in a BASE field), so a counter read off the contributions
        /// would miss exactly the ones worth counting. A joker says when it went off.
        ///
        /// What the bar and the tooltip do with these is generic, so any joker that calls
        /// NoteProc gets the flash and the two statistics lines for free.
        ///
        /// EXTENSION POINT: call NoteProc from wherever a joker's effect actually lands. Call it
        /// ONCE per firing - not once per cube, not once per hook.
        /// </summary>
        private int procCount;

        private long procPoints;

        /// <summary>
        /// True for a joker that KEEPS proc statistics - i.e. one that calls NoteProc somewhere.
        /// The tooltip prints its count even at ZERO, because "this has not fired yet" is real
        /// information about a joker you are deciding whether to keep, while a silent card tells
        /// you nothing about whether it is working.
        ///
        /// It has to be declared rather than inferred: a count that only appeared after the first
        /// firing could never show the zero, and one shown for every joker would put "Fired 0
        /// times" on the forty-odd that have no firing to count.
        ///
        /// EXTENSION POINT: override this to true in the same joker that calls NoteProc. The two
        /// go together and neither is any use alone.
        /// </summary>
        public virtual bool TracksProcs
        {
            get { return false; }
        }

        /// <summary>Times this joker has fired this run.</summary>
        public int ProcCount
        {
            get { return procCount; }
        }

        /// <summary>Points this joker has been credited with over the run, in the LOGICAL
        /// economy - the same units a joker's own bonus fields are written in, so the UI scales
        /// it exactly as it scales every other number.</summary>
        public long ProcPoints
        {
            get { return procPoints; }
        }

        /// <summary>
        /// Records one firing, worth <paramref name="points"/> (0 for a joker whose effect is not
        /// score). Pass the turn so the flash can be reported to the View; a null turn still
        /// counts the proc, which is what a market-phase or round-end effect does.
        ///
        /// Points are NOT clamped at zero here: a joker that costs the player points has fired
        /// just as much as one that paid, and a statistic that hides the losses is a lie about
        /// the joker. ProcCount, on the other hand, only ever goes up.
        /// </summary>
        protected void NoteProc(int points, TurnContext turn)
        {
            procCount++;
            procPoints += points;
            if (turn != null && turn.Report != null)
            {
                turn.Report.ProcedJokers.Add(InstanceId);
            }
        }

        /// <summary>A firing with no turn to report it on (market, round end).</summary>
        protected void NoteProc(int points)
        {
            NoteProc(points, null);
        }

        /// <summary>Value the joker earned by itself (the three kumbara jokers).</summary>
        public int AccruedValue { get; private set; }

        /// <summary>Extra price put on this joker by "ihale". Written from outside.</summary>
        public int AuctionPremium { get; internal set; }

        // The BASE sell value is NOT a field here: it is MarketConfig.JokerSellValue(rarity),
        // a fixed fraction of the buy price, so sell can never drift above buy. Ask
        // JokerInventory.SellValueOf(joker) for the whole number (base + the two fields above);
        // a joker has no session, so it cannot answer that by itself.

        /// <summary>What the market charged for this joker, in the SCALED economy. 0 when it was
        /// never bought - a starting joker, or one granted by the debug picker.</summary>
        public long PurchasePrice { get; internal set; }

        /// <summary>"Terslik" window: while it is open this joker's Accrue runs BACKWARDS, so a
        /// piggy bank leaks value instead of filling. Set by JokerInventory around joker dispatch,
        /// exactly like the score inversion, and false on every ordinary round.</summary>
        internal bool ValueGainInverted { get; set; }

        /// <summary>Grows SellValue. The kumbara jokers call this from their hooks. Inverted by
        /// "Terslik" into a loss of the same size - but never below nothing: a piggy bank can be
        /// emptied, it cannot go into debt.</summary>
        protected void Accrue(int amount)
        {
            if (ValueGainInverted)
            {
                amount = -amount;
            }
            AccruedValue += amount;
            if (AccruedValue < 0)
            {
                AccruedValue = 0;
            }
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

        /// <summary>Gives one use back, never past the round's allowance ("Şifacı" healing a
        /// spent joker). Returns false when there was nothing to give back.</summary>
        internal bool GrantCharge()
        {
            if (ChargesPerRound <= 0 || ChargesLeft >= ChargesPerRound)
            {
                return false;
            }
            ChargesLeft++;
            return true;
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
        /// <summary>
        /// The owned deck ("oyun destesi") gained or lost a card - a market purchase, a sale, a
        /// boss's deck tax. Session-scoped and purely informational: it fires in the MARKET as
        /// well as in a round, so a joker that reads the deck size for its badge can stay honest
        /// between turns instead of quoting whatever it saw last time it scored.
        ///
        /// Never use it to score - no turn is resolving. GameSession fires it from every place
        /// that changes the collection (see NoteDeckChanged).
        /// </summary>
        public virtual void OnDeckChanged(SessionContext ctx)
        {
        }

        public virtual void OnMarketEntered(SessionContext ctx)
        {
        }

        /// <summary>Market left. "Kapalı Ekonomi" watches anythingPurchased.</summary>
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
