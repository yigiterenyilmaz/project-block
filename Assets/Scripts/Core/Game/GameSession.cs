// PURPOSE: The whole run: owned card collection, joker/power inventories, round
// sequence, market phase, and the two score meanings (per-round RoundScore vs the
// run-wide TotalScore that doubles as market currency). Survives every round; each
// RoundEngine does not. See StartRound / OnRoundStatusChanged for the joker wiring.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One full run of the game.</summary>
    public sealed class GameSession
    {
        public GameConfig Config { get; }
        public GamePhase Phase { get; private set; }

        /// <summary>1-based number of the current (or just lost) round.</summary>
        public int RoundNumber { get; private set; }

        /// <summary>Run-wide score, doubling as market currency (confirmed design).</summary>
        public long TotalScore { get; private set; }

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

        /// <summary>Rerolls done in the current market visit. Raises the next reroll's cost and
        /// varies the reroll rng. Reset to 0 on every market entry (and on leaving).</summary>
        private int rerollCount;

        /// <summary>Owned card ids "Hileli zar" guaranteed onto the top of the next round's
        /// draw pile, or null. Consumed once when that round's engine is built.</summary>
        private List<int> pendingOpeningHand;

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
            if (offer.Sold || TotalScore < offer.Price)
            {
                return false;
            }
            if (offer.Kind == MarketOfferKind.Joker)
            {
                if (!CanAcquireJoker(offer.Joker))
                {
                    return false;
                }
                Jokers.Add(offer.Joker.Create());
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
            TotalScore -= offer.Price;
            offer.Sold = true;
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

        /// <summary>Refreshes EVERY market offer (blocks, jokers, powers) at once for an
        /// escalating cost, spending TotalScore like a purchase. Returns false when not in the
        /// market or the current reroll cost is unaffordable - the market is untouched then.</summary>
        public bool RerollMarket()
        {
            if (Phase != GamePhase.Market)
            {
                throw new InvalidOperationException("Not in the market phase.");
            }
            long cost = NextRerollCost;
            if (TotalScore < cost)
            {
                return false;
            }
            TotalScore -= cost;
            rerollCount++;
            RestockMarket(rerollCount);
            return true;
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

        /// <summary>Leaves the market and starts the next round.</summary>
        public void LeaveMarket()
        {
            if (Phase != GamePhase.Market)
            {
                throw new InvalidOperationException("Not in the market phase.");
            }
            Jokers.DispatchMarketLeft(purchasedThisMarket);
            rerollCount = 0;
            RoundNumber++;
            StartRound();
        }

        /// <summary>Adds run currency (a joker sale today; market refunds later).</summary>
        public void AddCurrency(long amount)
        {
            TotalScore += amount;
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

        /// <summary>"Karakter oluşturma": bakes a player-designed block (any shape, any
        /// element) into the owned deck and spends the driving power. The power rules are
        /// enforced HERE so the View stays rules-free: the power must be charged, a round must
        /// be running, and this turn's single power slot still free. The new card joins the
        /// shuffle from the next round, exactly like a bought block. Returns false and changes
        /// nothing if any of that fails.</summary>
        public bool CreateDesignedBlock(int powerInstanceId, BlockShape shape,
            IEnumerable<BlockElement> elements)
        {
            Power power = Powers.Find(powerInstanceId);
            RoundEngine round = CurrentRound;
            if (power == null || !power.Charged || shape == null || round == null
                || round.Status != RoundStatus.InProgress || round.PowersUsedThisTurn > 0)
            {
                return false;
            }
            ownedCards.Add(new BlockCard(nextCardId++, shape, elements, true)); // tagged "custom"
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

        private void StartRound()
        {
            RoundConfig roundConfig = Config.Progression.GetRound(RoundNumber);
            roundConfig = Jokers.FilterRoundConfig(roundConfig);
            roundConfig = Powers.FilterRoundConfig(roundConfig);
            CurrentRound = new RoundEngine(roundConfig, Config.Rules, ownedCards, rng, scorer, this, Jokers);
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
        }

        private void OnTurnResolved(TurnReport report)
        {
            TotalScore += report.ScoreGained;
        }

        private void OnRoundStatusChanged(RoundStatus status)
        {
            if (status == RoundStatus.Advanced)
            {
                Jokers.DispatchRoundEnded(CurrentRound, RoundOutcome.Advanced);
                rerollCount = 0; // a fresh reroll price each market visit
                RestockMarket();
                purchasedThisMarket = false;
                SetPhase(GamePhase.Market);
                Jokers.DispatchMarketEntered();
            }
            else if (status == RoundStatus.Lost)
            {
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
                newOffers.Add(new MarketOffer(card, market.BuyPrice(card) * Config.Scoring.ScoreScale));
            }
            AddJokerOffers(market, newOffers, reroll);
            AddPowerOffers(market, newOffers, reroll);
            Market.SetOffers(newOffers);
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
                int price = market.JokerPrice * market.PriceMultiplier(def.Rarity)
                    * Config.Scoring.ScoreScale;
                newOffers.Add(new MarketOffer(def, price));
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
                if (!owned.Contains(definition.DefId))
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
                int price = market.PowerPrice * market.PriceMultiplier(def.Rarity)
                    * Config.Scoring.ScoreScale;
                newOffers.Add(new MarketOffer(def, price));
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
