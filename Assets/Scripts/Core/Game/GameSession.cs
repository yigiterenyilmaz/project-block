// PURPOSE: The whole run: owned card collection, joker/power inventories, round
// sequence, market phase, and the two score meanings (per-round RoundScore vs the
// run-wide TotalScore that doubles as market currency). Survives every round; each
// RoundEngine does not. See StartRound / OnRoundStatusChanged for the joker wiring.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One full run of the game.</summary>
    public sealed partial class GameSession
    {
        public GameConfig Config { get; }
        public GamePhase Phase { get; private set; }

        /// <summary>1-based number of the current (or just lost) round.</summary>
        public int RoundNumber { get; private set; }

        /// <summary>True while the LAST round of the run is being played. Advancing out of it
        /// wins the run (GamePhase.RunWon) instead of opening a market.</summary>
        public bool IsFinalRound
        {
            get { return RoundNumber >= Config.TotalRounds; }
        }

        /// <summary>Run-wide score, doubling as market currency (confirmed design).</summary>
        public long TotalScore { get; private set; }

        // ------------------------------------------------------------------- market credit
        //
        // "Kredi kartı" lets the player buy past what they own. The shortfall becomes DEBT, the
        // debt compounds at the end of every round, and a boss round that ends with it still
        // open ends the RUN - the one loss in the game that has nothing to do with the board.
        //
        // Repayment is deliberately MANUAL (RepayDebt, a market action): choosing to carry the
        // debt one more round is the decision the joker is built around. Practical consequence,
        // worth knowing: since paying only happens in the market, the real deadline is the
        // market BEFORE the boss round - what you earn during the boss round cannot save you.

        /// <summary>What the player owes, in the scaled run economy. 0 when debt-free.</summary>
        public long Debt { get; private set; }

        /// <summary>True while a held joker lets the player buy with points they do not have.</summary>
        public bool CreditAvailable
        {
            get { return Jokers.GrantsMarketCredit; }
        }

        /// <summary>True if this price is payable at all - out of the run score, or on credit.</summary>
        public bool CanAfford(long price)
        {
            return TotalScore >= price || CreditAvailable;
        }

        /// <summary>Pays a market price: the player's own points first, the rest borrowed. Only
        /// ever called after CanAfford said yes.</summary>
        private void Spend(long price)
        {
            if (TotalScore >= price)
            {
                TotalScore -= price;
                return;
            }
            // Spend what there is and borrow the difference - a credit card, not a blank cheque
            // that ignores the balance.
            Debt += price - TotalScore;
            TotalScore = 0;
        }

        /// <summary>
        /// Pays the debt down from the run score, in the market. Pays as much of
        /// <paramref name="amount"/> as the player both owes and can afford, and returns what
        /// actually moved. Nothing happens outside the market, and paying is never automatic:
        /// earnings pile up in TotalScore until the player chooses to settle.
        /// </summary>
        public long RepayDebt(long amount)
        {
            if (Phase != GamePhase.Market || amount <= 0 || Debt <= 0)
            {
                return 0;
            }
            long paid = amount;
            if (paid > Debt)
            {
                paid = Debt;
            }
            if (paid > TotalScore)
            {
                paid = TotalScore;
            }
            if (paid <= 0)
            {
                return 0;
            }
            TotalScore -= paid;
            Debt -= paid;
            return paid;
        }

        /// <summary>Settles as much of the debt as the run score covers.</summary>
        public long RepayDebtInFull()
        {
            return RepayDebt(Debt);
        }

        /// <summary>Compounds the debt at the end of a round. Rounded UP, so a small debt still
        /// grows instead of sitting still forever.</summary>
        private void AccrueDebtInterest()
        {
            if (Debt <= 0)
            {
                return;
            }
            int percent = Jokers.MarketCreditInterestPercent;
            if (percent <= 0)
            {
                return;
            }
            Debt += (Debt * percent + 99) / 100;
        }

        /// <summary>Engine of the current round. Replaced wholesale every round.</summary>
        public RoundEngine CurrentRound { get; private set; }

        /// <summary>Restocked with block-card offers every time a round is won.</summary>
        public Market Market { get; }

        /// <summary>The player's jokers. Session-scoped: they outlive every round.</summary>
        public JokerInventory Jokers { get; }

        /// <summary>The player's powers. Session-scoped like the jokers, but a separate
        /// pool: powers and jokers do not compete for the same slots.</summary>
        public PowerInventory Powers { get; }

        private readonly List<BlockCard> ownedCards = new List<BlockCard>();

        /// <summary>The player's whole collection ("oyun destesi").</summary>
        public IReadOnlyList<BlockCard> OwnedCards
        {
            get { return ownedCards; }
        }

        private readonly IRandomSource rng;
        private readonly int resolvedSeed;
        private readonly IScoreCalculator scorer;
        private int nextCardId = 1;

        /// <summary>True if the player bought anything during the current market visit.
        /// "Damlaya damlaya" reads it when the market is left.</summary>
        private bool purchasedThisMarket;

        /// <summary>Fraction knocked off every price in the NEXT market visit, 0 = none.
        /// Granted by an effect during a round ("Hazine" treasure) and consumed when that
        /// market is left, so a discount is never carried into a second visit.</summary>
        public double PendingMarketDiscount { get; private set; }

        /// <summary>Adds a discount for the next market visit. Several sources stack up to a
        /// sane ceiling rather than making things free.</summary>
        public void AddMarketDiscount(double fraction)
        {
            if (fraction <= 0.0)
            {
                return;
            }
            PendingMarketDiscount += fraction;
            if (PendingMarketDiscount > 0.75)
            {
                PendingMarketDiscount = 0.75;
            }
        }

        /// <summary>Applies the pending discount to a price, never below 1.</summary>
        private int Discounted(int price)
        {
            if (PendingMarketDiscount <= 0.0)
            {
                return price;
            }
            int cut = (int)System.Math.Round(price * (1.0 - PendingMarketDiscount));
            return cut < 1 ? 1 : cut;
        }

        /// <summary>Rerolls done in the current market visit. Raises the next reroll's cost and
        /// varies the reroll rng. Reset to 0 on every market entry (and on leaving).</summary>
        private int rerollCount;

        /// <summary>Owned card ids "Hileli zar" guaranteed onto the top of the next round's
        /// draw pile, or null. Consumed once when that round's engine is built.</summary>
        private List<int> pendingOpeningHand;

        /// <summary>Bosses already met this run. A run never repeats a boss, so this is what
        /// the draw excludes.</summary>
        private readonly List<string> bossesFought = new List<string>();

        /// <summary>Boss kinds already met this run, in the order they were fought.</summary>
        public IReadOnlyList<string> BossesFought
        {
            get { return bossesFought; }
        }

        /// <summary>The boss of the current round, or null on an ordinary round. Shortcut for
        /// the UI - the boss itself lives on the RoundEngine.</summary>
        public BossRound ActiveBoss
        {
            get { return CurrentRound != null ? CurrentRound.Boss : null; }
        }

        /// <summary>"Hileli zar": guarantee these owned cards into the next round's opening
        /// hand (they are moved to the top of the fresh draw pile). Market-phase only.</summary>
        public void SetPendingOpeningHand(IEnumerable<int> cardIds)
        {
            pendingOpeningHand = cardIds != null ? new List<int>(cardIds) : null;
        }

        /// <summary>Takes and clears the preset opening hand (the round engine calls this once).</summary>
        internal IReadOnlyList<int> TakePendingOpeningHand()
        {
            List<int> preset = pendingOpeningHand;
            pendingOpeningHand = null;
            return preset;
        }

        public event Action<GamePhase> PhaseChanged;

        public GameSession(GameConfig config)
        {
            Config = config;
            resolvedSeed = config.RngSeed ?? Environment.TickCount;
            rng = new SeededRandom(resolvedSeed);
            scorer = new DefaultScoreCalculator(config.Scoring);
            Market = new Market();
            Jokers = new JokerInventory(this, rng);
            Powers = new PowerInventory(this, rng);
            if (config.Deck.FixedShapes != null)
            {
                // static deck: identical composition every run
                foreach (BlockShape shape in config.Deck.FixedShapes)
                {
                    ownedCards.Add(new BlockCard(nextCardId++, shape));
                }
            }
            else
            {
                for (int i = 0; i < config.Deck.Size; i++)
                {
                    ownedCards.Add(CreateRandomCard());
                }
            }
            RoundNumber = 1;
            StartRound();
        }

        /// <summary>
        /// DEBUG helper: puts a freshly generated random card into the current round's
        /// bonus hand. The card is round-scoped and does NOT join the owned deck (bonus
        /// cards expire when played or when the round ends). Real bonus-card sources
        /// arrive with the powers (Klon, Dolly, Olta, Kara delik).
        /// </summary>
        public BlockCard DebugAddRandomBonusCard()
        {
            if (Phase != GamePhase.Round)
            {
                throw new InvalidOperationException("Bonus cards can only be added during a round.");
            }
            BlockCard card = CreateRandomCard();
            CurrentRound.AddBonusCard(card, BonusPlayOutcome.ExpireFromRound);
            return card;
        }

        /// <summary>
        /// Buys a market offer with TotalScore. A block joins the owned deck (it shuffles in
        /// from the next round on); a joker or a power joins its inventory. Returns false
        /// when the offer is already sold, unaffordable, or there is no free slot.
        /// </summary>
        public bool TryBuyOffer(int offerIndex)
        {
            if (Phase != GamePhase.Market)
            {
                throw new InvalidOperationException("Not in the market phase.");
            }
            if (offerIndex < 0 || offerIndex >= Market.Offers.Count)
            {
                throw new ArgumentOutOfRangeException("offerIndex");
            }
            MarketOffer offer = Market.Offers[offerIndex];
            if (offer.Sold || !CanAfford(offer.Price))
            {
                return false;
            }
            if (offer.Kind == MarketOfferKind.Joker)
            {
                if (!CanAcquireJoker(offer.Joker))
                {
                    return false;
                }
                Joker bought = Jokers.Add(offer.Joker.Create());
                // Remembered so a joker may refund it later ("Yer altı kaynakları").
                bought.PurchasePrice = offer.Price;
                // "Yer altı kaynakları" refunds that price when its seam is spent, and it works
                // in market units - so it needs to know the economy the price was recorded in.
                var seam = bought as YerAltiKaynaklariJoker;
                if (seam != null)
                {
                    seam.ScoreScaleForRefund = Config.Scoring.ScoreScale;
                }
            }
            else if (offer.Kind == MarketOfferKind.Power)
            {
                if (!CanAcquirePower(offer.Power))
                {
                    return false;
                }
                Powers.Add(offer.Power.Create()); // arrives charged (Power constructor)
            }
            else
            {
                ownedCards.Add(offer.Card);
            }
            Spend(offer.Price);
            offer.Sold = true;
            purchasedThisMarket = true;
            return true;
        }

        // ------------------------------------------------------------------- smuggling

        /// <summary>
        /// "Kaçakçı" is a SESSION rule, not joker state - the same division as the market credit.
        /// The joker is only the switch; the taking, the roll and the spoiling all live here, so
        /// nothing else in the game has to know what smuggling is.
        /// </summary>
        private bool smuggledThisMarket;

        /// <summary>True while a free item is still there for the taking this market visit.</summary>
        public bool CanSmuggle
        {
            get
            {
                return Phase == GamePhase.Market && !smuggledThisMarket && Jokers.EnablesSmuggling;
            }
        }

        /// <summary>
        /// Takes one market offer for FREE ("Kaçakçı"), once per market visit. The goods may be
        /// defective: a block becomes junk, a joker comes broken, a power comes empty and slow.
        /// Returns false and changes nothing when there is nothing to smuggle with, the free item
        /// is already spent this visit, the offer is sold, or there is no slot for it.
        ///
        /// Deliberately NOT free of consequence beyond the goods: it counts as having bought
        /// something, so a joker that pays for leaving the market empty-handed is not fooled by
        /// walking out with stolen stock.
        /// </summary>
        public bool TrySmuggleOffer(int offerIndex)
        {
            if (Phase != GamePhase.Market)
            {
                throw new InvalidOperationException("Not in the market phase.");
            }
            if (offerIndex < 0 || offerIndex >= Market.Offers.Count)
            {
                throw new ArgumentOutOfRangeException("offerIndex");
            }
            if (!CanSmuggle)
            {
                return false;
            }
            MarketOffer offer = Market.Offers[offerIndex];
            if (offer.Sold)
            {
                return false;
            }
            // One roll for the whole transaction, before anything is handed over, so every kind of
            // goods reads the same coin flip.
            bool defective = rng.NextInt(0, 100) < Jokers.SmuggleDefectChancePercent;
            if (offer.Kind == MarketOfferKind.Joker)
            {
                if (!CanAcquireJoker(offer.Joker))
                {
                    return false;
                }
                Joker taken = Jokers.Add(offer.Joker.Create());
                taken.PurchasePrice = 0; // nothing was paid, so nothing can be refunded
                if (defective)
                {
                    // Two ways for a joker to be broken; the coin decides which.
                    taken.Defect = rng.NextInt(0, 2) == 0
                        ? SmuggledDefect.DeadInBossRounds
                        : SmuggledDefect.NeverWorks;
                }
            }
            else if (offer.Kind == MarketOfferKind.Power)
            {
                if (!CanAcquirePower(offer.Power))
                {
                    return false;
                }
                Power taken = Powers.Add(offer.Power.Create());
                if (defective)
                {
                    // After Add, which charges it: broken goods arrive empty.
                    taken.MakeSmuggled(Jokers.SmuggledPowerRechargeCost);
                }
            }
            else
            {
                // The block itself is exactly what was on the shelf - an ordinary card. A
                // DEFECTIVE one simply will not stay on the board: see BlockCard.FallsThrough.
                offer.Card.IsSmuggled = true;
                offer.Card.FallsThrough = defective;
                ownedCards.Add(offer.Card);
            }
            offer.Sold = true;
            smuggledThisMarket = true;
            purchasedThisMarket = true;
            return true;
        }

        /// <summary>Cost of the NEXT market reroll, in the scaled run economy. Escalates with
        /// each reroll this visit and resets when the market is re-entered.</summary>
        public long NextRerollCost
        {
            get
            {
                MarketConfig market = Config.Market;
                return (long)(market.RerollBaseCost + market.RerollCostStep * rerollCount)
                    * Config.Scoring.ScoreScale;
            }
        }

        /// <summary>
        /// Refreshes ONE section of the market - the blocks, the jokers or the powers - for an
        /// escalating cost, spending TotalScore like a purchase. Offers of the other kinds are
        /// left exactly as they are, sold flags included.
        ///
        /// The price escalates on a SINGLE counter shared by all three sections, so rerolling
        /// blocks also raises what the next joker reroll costs. That is deliberate: a per-section
        /// counter would let a player refresh three shelves for the price of one.
        ///
        /// Returns false when the current reroll cost is unaffordable - the market is untouched.
        /// </summary>
        public bool RerollMarket(MarketOfferKind kind)
        {
            if (Phase != GamePhase.Market)
            {
                throw new InvalidOperationException("Not in the market phase.");
            }
            long cost = NextRerollCost;
            if (!CanAfford(cost))
            {
                return false;
            }
            Spend(cost);
            rerollCount++;
            RestockSection(kind, rerollCount);
            return true;
        }

        /// <summary>Replaces just this kind's offers, keeping every other offer untouched. The
        /// result is re-grouped into kind order so the market view's rows stay stable.</summary>
        private void RestockSection(MarketOfferKind kind, int reroll)
        {
            MarketConfig market = Config.Market;
            var fresh = new List<MarketOffer>();
            if (kind == MarketOfferKind.Joker)
            {
                AddJokerOffers(market, fresh, reroll);
            }
            else if (kind == MarketOfferKind.Power)
            {
                AddPowerOffers(market, fresh, reroll);
            }
            else
            {
                AddBlockOffers(market, fresh, reroll);
            }

            var survivors = new List<MarketOffer>();
            foreach (MarketOffer offer in Market.Offers)
            {
                if (offer.Kind != kind)
                {
                    survivors.Add(offer);
                }
            }
            survivors.AddRange(fresh);
            // Stable regroup by kind: blocks, then jokers, then powers.
            var ordered = new List<MarketOffer>(survivors.Count);
            AppendOfKind(ordered, survivors, MarketOfferKind.Block);
            AppendOfKind(ordered, survivors, MarketOfferKind.Joker);
            AppendOfKind(ordered, survivors, MarketOfferKind.Power);
            Market.SetOffers(ordered);
        }

        private static void AppendOfKind(List<MarketOffer> into, List<MarketOffer> from,
            MarketOfferKind kind)
        {
            for (int i = 0; i < from.Count; i++)
            {
                if (from[i].Kind == kind)
                {
                    into.Add(from[i]);
                }
            }
        }

        /// <summary>True if the player already owns a joker of this kind.</summary>
        public bool OwnsJoker(string defId)
        {
            foreach (Joker joker in Jokers.Jokers)
            {
                if (joker.DefId == defId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if the player already owns a power of this kind.</summary>
        public bool OwnsPower(string defId)
        {
            foreach (Power power in Powers.Powers)
            {
                if (power.DefId == defId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if any held joker is legendary (only one may be held at a time).</summary>
        public bool HoldsLegendaryJoker()
        {
            foreach (Joker joker in Jokers.Jokers)
            {
                if (joker.IsLegendary)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether this joker kind can be acquired right now: a free slot, no
        /// duplicate copy, and - for a legendary - no legendary already held.</summary>
        public bool CanAcquireJoker(JokerDefinition definition)
        {
            if (definition == null || Jokers.IsFull || OwnsJoker(definition.DefId))
            {
                return false;
            }
            return !(definition.IsLegendary && HoldsLegendaryJoker());
        }

        /// <summary>Whether this power kind can be acquired right now: a free slot and no
        /// duplicate copy.</summary>
        public bool CanAcquirePower(PowerDefinition definition)
        {
            return definition != null && !Powers.IsFull && !OwnsPower(definition.DefId);
        }

        /// <summary>Sells an owned card back for its sell value (added to TotalScore) and
        /// removes it from the deck. Plain blocks pay nothing; elemental ones pay a fraction
        /// of their buy price. Returns what was paid, or 0 if the card was not owned.</summary>
        public long SellCard(BlockCard card)
        {
            if (card == null || !ownedCards.Remove(card))
            {
                return 0;
            }
            // Sell values live in the same currency as the scaled run economy.
            int value = Config.Market.SellValue(card) * Config.Scoring.ScoreScale;
            TotalScore += value;
            return value;
        }

        /// <summary>
        /// "Parazit": binds a joker to one cube of an owned block. Market-phase only, because
        /// the deck is only stable between rounds. Returns false if there is no Parazit, it
        /// already carries a binding, or the joker/card/cube is not a legal target.
        /// EXTENSION POINT: the market UI calls this once it can ask for the three picks.
        /// </summary>
        public bool TryAttachJokerToCard(int jokerInstanceId, int cardId, int cellIndex)
        {
            if (Phase != GamePhase.Market)
            {
                throw new InvalidOperationException("Jokers can only be attached in the market.");
            }
            ParazitJoker parazit = null;
            foreach (Joker joker in Jokers.Jokers)
            {
                parazit = joker as ParazitJoker;
                if (parazit != null && !parazit.HasBinding)
                {
                    break;
                }
                parazit = null;
            }
            if (parazit == null)
            {
                return false;
            }
            Joker target = Jokers.Find(jokerInstanceId);
            BlockCard card = null;
            for (int i = 0; i < ownedCards.Count; i++)
            {
                if (ownedCards[i].Id == cardId)
                {
                    card = ownedCards[i];
                    break;
                }
            }
            return parazit.TryBind(new SessionContext(this, rng), target, card, cellIndex);
        }

        /// <summary>Turns down the dead-end rescue offer and takes the loss. The UI's
        /// "give up" path while the round is paused in AwaitingRescue.</summary>
        public void DeclineDeadEndRescue()
        {
            if (CurrentRound != null)
            {
                CurrentRound.DebugDeclineRescue();
            }
        }

        /// <summary>Leaves the market and starts the next round.</summary>
        public void LeaveMarket()
        {
            if (Phase != GamePhase.Market)
            {
                throw new InvalidOperationException("Not in the market phase.");
            }
            if (IsFinalRound)
            {
                // Unreachable today (the final round wins instead of opening a market), but the
                // run length is an invariant: RoundNumber must never walk past TotalRounds.
                throw new InvalidOperationException(
                    "The run is over - round " + RoundNumber + " of " + Config.TotalRounds + ".");
            }
            Jokers.DispatchMarketLeft(purchasedThisMarket);
            PendingMarketDiscount = 0.0; // spent on this visit, never carried to the next
            rerollCount = 0;
            RoundNumber++;
            StartRound();
        }

        /// <summary>
        /// PERMANENTLY takes cards out of the run deck - the two tax bosses ("Harcama vergisi",
        /// "Özel tüketim vergisi"). Unlike the overtime continue cost this is not round-scoped:
        /// the cards leave OwnedCards for good, so later rounds are poorer too. They are pulled
        /// out of the round in progress as well, so the tax bites immediately.
        ///
        /// Refuses to shrink the deck below a playable size (the hand size): a deck smaller than
        /// that loses the NEXT round during construction, which is a bug, not a difficulty.
        /// Returns how many cards actually left.
        /// </summary>
        public int TaxOwnedCards(int count, IRandomSource taxRng)
        {
            if (count <= 0 || taxRng == null)
            {
                return 0;
            }
            int floor = Config.Rules.HandSize;
            int taken = 0;
            for (int i = 0; i < count && ownedCards.Count > floor; i++)
            {
                BlockCard card = ownedCards[taxRng.NextInt(0, ownedCards.Count)];
                ownedCards.Remove(card);
                if (CurrentRound != null)
                {
                    // Out of the piles too, so it cannot still be drawn this round.
                    CurrentRound.TaxCardOutOfRound(card);
                }
                taken++;
            }
            return taken;
        }

        /// <summary>
        /// Takes a percentage of the run score away ("Cana geleceğine mala" charging the purse
        /// every time the draw pile dries up). Rounded UP so a small purse is not immune, floored
        /// at what there is - the run score never goes negative, and this never touches the debt:
        /// losing money makes an open debt harder to clear, it does not grow it.
        /// Returns what was actually taken.
        /// </summary>
        public long TakeCurrencyPercent(int percent)
        {
            if (percent <= 0 || TotalScore <= 0)
            {
                return 0;
            }
            long taken = (TotalScore * percent + 99) / 100;
            if (taken > TotalScore)
            {
                taken = TotalScore;
            }
            TotalScore -= taken;
            CurrencyTakenByEffects += taken;
            return taken;
        }

        /// <summary>
        /// Run currency taken away by an EFFECT rather than earned, spent or sold - "Cana
        /// geleceğine mala" charging the purse, an overtime cap clawing back farmed score.
        /// Book-keeping only: it exists so the books can be balanced (the fuzz suite proves
        /// TotalScore against it), and nothing in the rules reads it.
        /// </summary>
        public long CurrencyTakenByEffects { get; private set; }

        /// <summary>Adds run currency (a joker sale today; market refunds later).</summary>
        public void AddCurrency(long amount)
        {
            TotalScore += amount;
            if (amount < 0)
            {
                // A negative grant is an effect taking money back (the overtime score cap).
                CurrencyTakenByEffects += -amount;
            }
        }

        /// <summary>The session RNG. Everything random in the run must come from here.</summary>
        public IRandomSource Rng
        {
            get { return rng; }
        }

        /// <summary>Mints a new card owned by the player. Public so jokers that hand out
        /// cards (Kara delik's void block...) keep the id counter unique across the run.</summary>
        public BlockCard CreateRandomCard()
        {
            BlockShape shape = Config.Deck.ShapeGenerator.NextShape(rng);
            return new BlockCard(nextCardId++, shape);
        }

        /// <summary>Mints a specific card. Round-scoped joker cards ("Kara delik" void
        /// blocks) use this so their ids stay unique across the run without joining the
        /// owned deck - they simply are never added to ownedCards.</summary>
        public BlockCard CreateCard(BlockShape shape, IEnumerable<BlockElement> elements)
        {
            return new BlockCard(nextCardId++, shape, elements);
        }

        /// <summary>"Karakter oluşturma": bakes a player-designed block into the owned deck and
        /// spends the driving power. Each drawn cube may carry its OWN element (or none), so the
        /// block can mix types. <paramref name="drawnCells"/> are the raw cells the player drew and
        /// <paramref name="cellElements"/> is the element chosen for each (index-parallel, a null
        /// entry = a plain cube). The rules are enforced HERE so the View stays rules-free: the
        /// power must be charged, a round must be running, and this turn's single power slot still
        /// free. The new card joins the shuffle from the next round, exactly like a bought block.
        /// Returns false and changes nothing if any of that fails.</summary>
        public bool CreateDesignedBlock(int powerInstanceId, IReadOnlyList<GridPos> drawnCells,
            IReadOnlyList<BlockElement?> cellElements)
        {
            Power power = Powers.Find(powerInstanceId);
            RoundEngine round = CurrentRound;
            if (power == null || !power.Charged || drawnCells == null || drawnCells.Count == 0
                || round == null || round.Status != RoundStatus.InProgress
                || round.PowersUsedThisTurn > 0)
            {
                return false;
            }
            BlockShape shape = BlockShape.FromCells(drawnCells);
            // FromCells normalizes (subtract min) AND sorts, so the shape's cell order is not the
            // draw order - map each drawn cell's element to the shape's cells by COORDINATE.
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            for (int i = 0; i < drawnCells.Count; i++)
            {
                if (drawnCells[i].X < minX) minX = drawnCells[i].X;
                if (drawnCells[i].Y < minY) minY = drawnCells[i].Y;
            }
            var byNormalized = new Dictionary<GridPos, BlockElement?>();
            for (int i = 0; i < drawnCells.Count; i++)
            {
                var normalized = new GridPos(drawnCells[i].X - minX, drawnCells[i].Y - minY);
                byNormalized[normalized] = cellElements != null && i < cellElements.Count
                    ? cellElements[i]
                    : null;
            }
            var perCube = new BlockElement?[shape.Cells.Count];
            for (int i = 0; i < shape.Cells.Count; i++)
            {
                BlockElement? e;
                perCube[i] = byNormalized.TryGetValue(shape.Cells[i], out e) ? e : null;
            }
            ownedCards.Add(BlockCard.Designed(nextCardId++, shape, perCube)); // tagged "custom"
            power.Spend();
            round.NotePowerUsed();
            return true;
        }

        /// <summary>"Batak": places the player's bet through the power, spending its charge and
        /// this turn's power slot. Rules enforced here so the View stays rules-free - the power
        /// must be a charged BatakPower, a round must be running with the power slot free, and
        /// the bet number must be legal. Returns false and changes nothing otherwise.</summary>
        public bool PlaceBatakBet(int powerInstanceId, int turns)
        {
            var batak = Powers.Find(powerInstanceId) as BatakPower;
            RoundEngine round = CurrentRound;
            if (batak == null || !batak.Charged || round == null
                || round.Status != RoundStatus.InProgress || round.PowersUsedThisTurn > 0)
            {
                return false;
            }
            if (!batak.PlaceBet(new RoundContext(this, rng, round), turns))
            {
                return false;
            }
            batak.Spend();
            round.NotePowerUsed();
            return true;
        }

        /// <summary><paramref name="replayBossDefId"/> is set only when a round is being played
        /// AGAIN ("Uzun vadeli yatırımcı" spending its second chance): it pins the same boss KIND
        /// as the attempt that failed, as a fresh instance. A do-over must be the same fight - and
        /// drawing again would hand out a different boss, because the first one is already in
        /// bossesFought.</summary>
        private void StartRound(string replayBossDefId = null)
        {
            RoundConfig roundConfig = Config.Progression.GetRound(RoundNumber);
            roundConfig = Jokers.FilterRoundConfig(roundConfig);
            roundConfig = Powers.FilterRoundConfig(roundConfig);
            // The boss is DRAWN before the engine exists, because it may reshape the round itself
            // ("Dört kutup" rounding the arena up to an even edge). Drawing early costs nothing:
            // DrawBoss has its own rng, so the main stream is untouched either way.
            BossRound boss = null;
            if (roundConfig.IsBossRound)
            {
                boss = replayBossDefId != null
                    ? BossRegistry.Create(replayBossDefId)
                    : DrawBoss();
                if (boss != null)
                {
                    roundConfig = boss.FilterRoundConfig(roundConfig);
                }
            }
            CurrentRound = new RoundEngine(roundConfig, Config.Rules, ownedCards, rng, scorer, this, Jokers);
            // Attached before anything else runs, so it governs the round's very first turn.
            // Ordinary rounds get null and behave exactly as they always have.
            if (boss != null)
            {
                CurrentRound.SetBoss(boss);
            }
            // The final round is where "Uzun vadeli yatırımcı" finally pays: its two exclusive
            // powers are handed over now, before the round's first turn. Deliberately ahead of the
            // Lost check below so a degenerate round start cannot swallow them.
            GrantInvestorPowers();
            CurrentRound.TurnResolved += OnTurnResolved;
            CurrentRound.StatusChanged += OnRoundStatusChanged;
            SetPhase(GamePhase.Round);
            if (CurrentRound.Status == RoundStatus.Lost)
            {
                // Degenerate case (e.g. deck smaller than hand size): lost on round start.
                Jokers.DispatchRoundEnded(CurrentRound, RoundOutcome.Lost);
                SetPhase(GamePhase.GameOver);
                return;
            }
            Jokers.DispatchRoundStarted(CurrentRound);
            Powers.DispatchRoundStarted(CurrentRound);
            // The antagonist moves last: the player's jokers and powers are set up (and the
            // powers recharged) before the boss picks a victim or takes its first bite.
            if (CurrentRound.Boss != null)
            {
                CurrentRound.Boss.OnRoundStarted(new RoundContext(this, rng, CurrentRound));
            }
        }

        /// <summary>
        /// Hands over the InvestorOnly powers for the FINAL round, when a held joker unlocks them
        /// ("Uzun vadeli yatırımcı"). They arrive charged, outside the normal slot limit - they are
        /// a loan for one round, not stock the player had to make room for - and never twice: a
        /// replayed final round finds them already held and adds nothing.
        ///
        /// There are no InvestorOnly powers in the registry yet, so today this is inert. When the
        /// designer names the two, registering them is the whole of the work.
        /// </summary>
        private void GrantInvestorPowers()
        {
            if (!IsFinalRound || !Jokers.UnlocksInvestorPowers)
            {
                return;
            }
            IReadOnlyList<PowerDefinition> catalogue = PowerRegistry.All;
            for (int i = 0; i < catalogue.Count; i++)
            {
                if (!catalogue[i].InvestorOnly || HoldsPower(catalogue[i].DefId))
                {
                    continue;
                }
                Powers.Add(catalogue[i].Create());
            }
        }

        private bool HoldsPower(string defId)
        {
            foreach (Power held in Powers.Powers)
            {
                if (held.DefId == defId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Spends "Uzun vadeli yatırımcı"'s one second chance on a lost FINAL round and plays that
        /// round again from the start. Returns false - and changes nothing - when there is no
        /// second chance to spend or this was not the last round, in which case the loss stands.
        ///
        /// The failed attempt is VOIDED, not ended: RoundOutcome.Lost is never dispatched for it
        /// (a round-end effect must not settle for a round that is about to be replayed) and the
        /// score it banked is clawed back out of the run currency, so a replayed round can still
        /// only ever pay once. The same boss kind comes back as a fresh instance.
        /// </summary>
        private bool TryReplayFinalRound()
        {
            if (!IsFinalRound || CurrentRound == null)
            {
                return false;
            }
            Joker investor = Jokers.TryConsumeFinalRoundRetry();
            if (investor == null)
            {
                return false;
            }
            // Un-bank the voided attempt. Clamped: an effect may already have emptied the purse
            // ("Cana geleceğine mala"), and the run currency never goes negative.
            long banked = CurrentRound.RoundScore;
            if (banked > TotalScore)
            {
                banked = TotalScore;
            }
            if (banked > 0)
            {
                AddCurrency(-banked); // books it as taken by an effect, so the ledger balances
            }
            string bossDefId = CurrentRound.Boss != null ? CurrentRound.Boss.DefId : null;
            CurrentRound.TurnResolved -= OnTurnResolved;
            CurrentRound.StatusChanged -= OnRoundStatusChanged;
            FinalRoundReplays++;
            StartRound(bossDefId);
            return true;
        }

        /// <summary>How many times the final round has been replayed this run (0 or 1 today).
        /// For the HUD, so the player can see the second chance was spent.</summary>
        public int FinalRoundReplays { get; private set; }

        /// <summary>
        /// Draws the boss for a flagged round: a random kind this run has not met yet, so five
        /// boss rounds never repeat themselves. Deterministic and drawn from its OWN rng (seed +
        /// round number), so boss selection never disturbs the main stream that shuffles decks
        /// and drives play - an ordinary round is unaffected by bosses existing at all.
        /// Falls back to allowing repeats only if a run somehow has more boss rounds than there
        /// are bosses.
        /// </summary>
        private BossRound DrawBoss()
        {
            // DEBUG/TEST override: a pinned boss skips the draw entirely (and the no-repeats
            // bookkeeping, so the same one can be met every round on purpose).
            if (Config.ForcedBossDefId != null)
            {
                return BossRegistry.Create(Config.ForcedBossDefId);
            }
            IReadOnlyList<BossDefinition> catalogue = BossRegistry.All;
            if (catalogue.Count == 0)
            {
                return null; // no content yet: the round is simply flagged and plays normally
            }
            var pool = new List<BossDefinition>();
            for (int i = 0; i < catalogue.Count; i++)
            {
                if (!bossesFought.Contains(catalogue[i].DefId))
                {
                    pool.Add(catalogue[i]);
                }
            }
            if (pool.Count == 0)
            {
                pool.AddRange(catalogue);
            }
            var bossRng = new SeededRandom(
                unchecked(resolvedSeed * 1566083941 + RoundNumber * 31337));
            BossDefinition picked = pool[bossRng.NextInt(0, pool.Count)];
            bossesFought.Add(picked.DefId);
            return picked.Create();
        }

        private void OnTurnResolved(TurnReport report)
        {
            TotalScore += report.ScoreGained;
        }

        private void OnRoundStatusChanged(RoundStatus status)
        {
            if (status == RoundStatus.Advanced)
            {
                // Round-end effects pay out either way - a run-winning round must still settle
                // the kumbara jokers - so this runs BEFORE the win check.
                Jokers.DispatchRoundEnded(CurrentRound, RoundOutcome.Advanced);

                // "Kredi kartı": the debt compounds every round, and a BOSS round that ends with
                // it still open ends the run. Deliberately ahead of the win check - surviving the
                // final round does not settle your books, so round 15 can be survived and still
                // lost. Without that, the last market would be a free shopping spree.
                AccrueDebtInterest();
                if (Debt > 0 && CurrentRound.Config.IsBossRound)
                {
                    CurrentRound.NoteRunLoss(LossReason.DebtNotRepaid);
                    SetPhase(GamePhase.GameOver);
                    return;
                }
                if (IsFinalRound)
                {
                    // Survived the last round: the run is won and there is no market to
                    // stock, because there is no round after this one to prepare for.
                    SetPhase(GamePhase.RunWon);
                    return;
                }
                rerollCount = 0; // a fresh reroll price each market visit
                RestockMarket();
                purchasedThisMarket = false;
                smuggledThisMarket = false; // one free item per VISIT, not per run
                SetPhase(GamePhase.Market);
                Jokers.DispatchMarketEntered();
            }
            else if (status == RoundStatus.Lost)
            {
                // "Uzun vadeli yatırımcı": the last round, and only the last round, may be played
                // again once. Ahead of everything else - the round is being voided, so nothing
                // that settles a finished round may run for it.
                if (TryReplayFinalRound())
                {
                    return;
                }
                Jokers.DispatchRoundEnded(CurrentRound, RoundOutcome.Lost);
                SetPhase(GamePhase.GameOver);
            }
        }

        /// <summary>(Re)builds the market stock. <paramref name="reroll"/> is 0 for the initial
        /// stock on market entry and the reroll counter (1, 2, ...) for a paid reroll. At 0 the
        /// block/joker/power draws are byte-identical to the base game (the block loop consumes
        /// the main rng, the joker/power seeds carry no reroll term); a reroll instead draws from
        /// dedicated deterministic rngs so it varies per reroll AND never disturbs the main rng
        /// stream that shuffles decks and drives play.</summary>
        private void RestockMarket(int reroll = 0)
        {
            MarketConfig market = Config.Market;
            var newOffers = new List<MarketOffer>();
            AddBlockOffers(market, newOffers, reroll);
            AddJokerOffers(market, newOffers, reroll);
            AddPowerOffers(market, newOffers, reroll);
            Market.SetOffers(newOffers);
        }

        /// <summary>Appends this visit's block offers. At reroll 0 the draws come from the MAIN
        /// rng, which is what keeps a fresh market byte-identical to the base game; a paid
        /// reroll uses a derived deterministic rng instead, so it never disturbs the stream that
        /// shuffles decks and drives play.</summary>
        private void AddBlockOffers(MarketConfig market, List<MarketOffer> newOffers, int reroll)
        {
            IRandomSource blockRng = reroll == 0
                ? rng
                : new SeededRandom(unchecked(resolvedSeed * 374761393 + RoundNumber * 66037 + reroll * 21179));
            for (int i = 0; i < market.BlockOfferCount; i++)
            {
                bool giveElement = market.ElementPool.Count > 0
                    && blockRng.NextDouble() < market.ElementChance;
                // An elemental block never comes as a single cube - most element behaviours
                // (fire chains, "whole block explodes", per-cube bonuses) need more than one.
                BlockShape shape = NextBlockShape(blockRng, giveElement ? market.MinElementalBlockSize : 1);
                List<BlockElement> elements = giveElement
                    ? new List<BlockElement>
                    {
                        market.ElementPool[blockRng.NextInt(0, market.ElementPool.Count)]
                    }
                    : null;
                var card = new BlockCard(nextCardId++, shape, elements);
                card = Jokers.FilterMarketOffer(card); // "Simya" adds a second element here
                // priced AFTER the filter so a joker-added element is surcharged too, and
                // lifted into the scaled economy so prices track the bigger score numbers
                newOffers.Add(new MarketOffer(card,
                    Discounted(market.BuyPrice(card) * Config.Scoring.ScoreScale)));
            }
        }

        /// <summary>Rolls a block shape of at least <paramref name="minSize"/> cubes, re-rolling
        /// a few times if the generator hands back something smaller. Capped so a generator that
        /// only makes tiny shapes cannot loop forever - it just returns its best effort.</summary>
        private BlockShape NextBlockShape(IRandomSource r, int minSize)
        {
            BlockShape shape = Config.Deck.ShapeGenerator.NextShape(r);
            for (int attempt = 0; attempt < 24 && shape.Size < minSize; attempt++)
            {
                shape = Config.Deck.ShapeGenerator.NextShape(r);
            }
            return shape;
        }

        /// <summary>Appends this visit's joker offers. Uses a SEPARATE rng derived from the
        /// run seed and round number, so joker stocking is deterministic yet never disturbs
        /// the main rng stream that drives deck shuffles and block play.</summary>
        private void AddJokerOffers(MarketConfig market, List<MarketOffer> newOffers, int reroll)
        {
            IReadOnlyList<JokerDefinition> catalogue = JokerRegistry.All;
            if (market.JokerOfferCount <= 0 || catalogue.Count == 0)
            {
                return;
            }
            // reroll * K is an additive salt that is 0 on the initial stock, so reroll 0 keeps
            // the exact base-game seed while each paid reroll shifts to a fresh shop.
            var jokerRng = new SeededRandom(
                unchecked(resolvedSeed * 486187739 + RoundNumber + reroll * 40503));
            // Never offer a joker the player already owns. Distinct picks within this visit:
            // shuffle the remaining pool and take the first N.
            var owned = new HashSet<string>();
            foreach (Joker held in Jokers.Jokers)
            {
                owned.Add(held.DefId);
            }
            // A legendary the player already holds one of is filtered out entirely (only one
            // legendary may be held), alongside plain duplicates.
            bool holdsLegendary = HoldsLegendaryJoker();
            var pool = new List<JokerDefinition>();
            foreach (JokerDefinition definition in catalogue)
            {
                if (owned.Contains(definition.DefId))
                {
                    continue;
                }
                if (definition.IsLegendary && holdsLegendary)
                {
                    continue;
                }
                // An early-game-only bet ("Uzun vadeli yatırımcı") leaves the shop for good once
                // its window closes - buying it late would be no investment at all.
                if (RoundNumber > definition.LastOfferableRound)
                {
                    continue;
                }
                pool.Add(definition);
            }
            if (pool.Count == 0)
            {
                return;
            }
            // Weighted by rarity (A-Res weighted shuffle): each item's key is u^(1/weight), so
            // commoner items rise to the top and legendaries seldom appear. Deterministic on
            // jokerRng, so the same seed + round always stocks the same shop.
            var keyed = new List<KeyValuePair<double, JokerDefinition>>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                int w = market.Weight(pool[i].Rarity);
                double u = jokerRng.NextDouble();
                keyed.Add(new KeyValuePair<double, JokerDefinition>(
                    w <= 0 ? 0.0 : Math.Pow(u, 1.0 / w), pool[i]));
            }
            keyed.Sort((a, b) => b.Key.CompareTo(a.Key)); // highest key first
            // At most one legendary per visit, so the player is never teased with a second
            // legendary they could not also take.
            int count = Math.Min(market.JokerOfferCount, keyed.Count);
            int taken = 0;
            bool legendaryTaken = false;
            for (int i = 0; i < keyed.Count && taken < count; i++)
            {
                JokerDefinition def = keyed[i].Value;
                if (def.IsLegendary)
                {
                    if (legendaryTaken)
                    {
                        continue;
                    }
                    legendaryTaken = true;
                }
                // Rounded BEFORE the scale so a fractional rarity multiplier still yields a
                // round price in the scaled economy (40 * 1.5 = 60 -> 600, not 599.99...).
                int price = market.JokerBuyPrice(def.Rarity) * Config.Scoring.ScoreScale;
                newOffers.Add(new MarketOffer(def, Discounted(price)));
                taken++;
            }
        }

        /// <summary>Appends this visit's power offers, mirroring AddJokerOffers: a separate
        /// deterministic rng (its own mixing constant, so joker stocking is untouched) and
        /// never a power the player already holds.</summary>
        private void AddPowerOffers(MarketConfig market, List<MarketOffer> newOffers, int reroll)
        {
            IReadOnlyList<PowerDefinition> catalogue = PowerRegistry.All;
            if (market.PowerOfferCount <= 0 || catalogue.Count == 0)
            {
                return;
            }
            // Additive reroll salt, 0 on the initial stock (see AddJokerOffers).
            var powerRng = new SeededRandom(
                unchecked(resolvedSeed * 1000000007 + RoundNumber + reroll * 92821));
            var owned = new HashSet<string>();
            foreach (Power held in Powers.Powers)
            {
                owned.Add(held.DefId);
            }
            var pool = new List<PowerDefinition>();
            foreach (PowerDefinition definition in catalogue)
            {
                // The investor's exclusive powers are not for sale at any price, in any market.
                if (!owned.Contains(definition.DefId) && !definition.InvestorOnly)
                {
                    pool.Add(definition);
                }
            }
            if (pool.Count == 0)
            {
                return;
            }
            // Rarity-weighted, same A-Res method as the joker offers (see AddJokerOffers).
            var keyed = new List<KeyValuePair<double, PowerDefinition>>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                int w = market.Weight(pool[i].Rarity);
                double u = powerRng.NextDouble();
                keyed.Add(new KeyValuePair<double, PowerDefinition>(
                    w <= 0 ? 0.0 : Math.Pow(u, 1.0 / w), pool[i]));
            }
            keyed.Sort((a, b) => b.Key.CompareTo(a.Key));
            int count = Math.Min(market.PowerOfferCount, keyed.Count);
            for (int i = 0; i < count; i++)
            {
                PowerDefinition def = keyed[i].Value;
                // Rounded before the scale, as in AddJokerOffers.
                int price = market.PowerBuyPrice(def.Rarity) * Config.Scoring.ScoreScale;
                newOffers.Add(new MarketOffer(def, Discounted(price)));
            }
        }

        private void SetPhase(GamePhase newPhase)
        {
            if (Phase == newPhase && newPhase != GamePhase.Round)
            {
                return;
            }
            Phase = newPhase;
            if (PhaseChanged != null)
            {
                PhaseChanged(newPhase);
            }
        }
    }
}
