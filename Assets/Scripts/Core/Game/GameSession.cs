// PURPOSE: The whole run: owned card collection, joker inventory, round sequence, market
// phase, and the two score meanings (confirmed design):
//   - RoundEngine.RoundScore : resets every round, compared against the threshold
//   - TotalScore             : accumulates across the run, spent in the market later
// Every round starts with the FULL owned deck reshuffled into a fresh draw pile
// (confirmed). The session survives rounds; RoundEngine instances do not.
//
// JOKER WIRING (all of it lives in StartRound / OnRoundStatusChanged):
//   1. jokers may rewrite the round setup BEFORE the board exists (FilterRoundConfig)
//   2. the inventory is handed to the engine as ITurnHooks - not as event subscribers,
//      because in-turn hooks must run mid-resolution and may change the turn
//   3. charges reset + OnRoundStarted after the engine exists
//   4. OnRoundEnded when the round resolves either way
// EXTENSION POINT: the power inventory belongs here too, wired the same way.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Top-level phase of the run.</summary>
    public enum GamePhase
    {
        /// <summary>A round is being played (see CurrentRound.Status for detail).</summary>
        Round = 0,

        /// <summary>Between rounds; leave via LeaveMarket().</summary>
        Market = 1,

        /// <summary>The run is over (see CurrentRound.Loss).</summary>
        GameOver = 2
    }

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

        private readonly List<BlockCard> ownedCards = new List<BlockCard>();

        /// <summary>The player's whole collection ("oyun destesi").</summary>
        public IReadOnlyList<BlockCard> OwnedCards
        {
            get { return ownedCards; }
        }

        private readonly IRandomSource rng;
        private readonly IScoreCalculator scorer;
        private int nextCardId = 1;

        /// <summary>True if the player bought anything during the current market visit.
        /// "Damlaya damlaya" reads it when the market is left.</summary>
        private bool purchasedThisMarket;

        public event Action<GamePhase> PhaseChanged;

        public GameSession(GameConfig config)
        {
            Config = config;
            rng = new SeededRandom(config.RngSeed ?? Environment.TickCount);
            scorer = new DefaultScoreCalculator(config.Scoring);
            Market = new Market();
            Jokers = new JokerInventory(this, rng);
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
        /// Buys a market offer: deducts the price from TotalScore and permanently adds
        /// the card to the owned deck (it joins the shuffle from the next round on).
        /// Returns false when the offer is already sold or unaffordable.
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
            TotalScore -= offer.Price;
            offer.Sold = true;
            ownedCards.Add(offer.Card);
            purchasedThisMarket = true;
            return true;
        }

        /// <summary>Leaves the market and starts the next round.</summary>
        public void LeaveMarket()
        {
            if (Phase != GamePhase.Market)
            {
                throw new InvalidOperationException("Not in the market phase.");
            }
            Jokers.DispatchMarketLeft(purchasedThisMarket);
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

        private void StartRound()
        {
            RoundConfig roundConfig = Jokers.FilterRoundConfig(Config.Progression.GetRound(RoundNumber));
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

        private void RestockMarket()
        {
            MarketConfig market = Config.Market;
            var newOffers = new List<MarketOffer>();
            for (int i = 0; i < market.BlockOfferCount; i++)
            {
                BlockShape shape = Config.Deck.ShapeGenerator.NextShape(rng);
                List<BlockElement> elements = null;
                if (market.ElementPool.Count > 0 && rng.NextDouble() < market.ElementChance)
                {
                    elements = new List<BlockElement>
                    {
                        market.ElementPool[rng.NextInt(0, market.ElementPool.Count)]
                    };
                }
                var card = new BlockCard(nextCardId++, shape, elements);
                card = Jokers.FilterMarketOffer(card); // "Simya" adds a second element here
                int price = market.BlockBasePrice
                    + market.BlockPricePerCube * card.Shape.Size
                    + market.ElementPriceSurcharge * card.Elements.Count;
                newOffers.Add(new MarketOffer(card, price));
            }
            Market.SetOffers(newOffers);
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
