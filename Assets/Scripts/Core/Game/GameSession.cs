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

        public MarketStub Market { get; }

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
            Market = new MarketStub();
            Jokers = new JokerInventory(this, rng);
            for (int i = 0; i < config.StartingDeckSize; i++)
            {
                ownedCards.Add(CreateRandomCard());
            }
            RoundNumber = 1;
            StartRound();
        }

        /// <summary>The session RNG. Everything random in the run must come from here.</summary>
        public IRandomSource Rng
        {
            get { return rng; }
        }

        /// <summary>Leaves the (empty) market and starts the next round.</summary>
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

        /// <summary>Records that something was bought this market visit.
        /// EXTENSION POINT: the real market calls this from every purchase.</summary>
        public void NotifyPurchase()
        {
            purchasedThisMarket = true;
        }

        /// <summary>Mints a new random card owned by the player (market purchases, and the
        /// temporary cards future jokers hand out). Ids stay unique across the run.</summary>
        public BlockCard CreateRandomCard()
        {
            BlockShape shape = Config.ShapeGenerator.NextShape(rng);
            return new BlockCard(nextCardId++, shape);
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
