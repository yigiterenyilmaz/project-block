// PURPOSE: The whole run: owned card collection, round sequence, market phase, and
// the two score meanings (confirmed design):
//   - RoundEngine.RoundScore : resets every round, compared against the threshold
//   - TotalScore             : accumulates across the run, spent in the market later
// Every round starts with the FULL owned deck reshuffled into a fresh draw pile
// (confirmed). The session survives rounds; RoundEngine instances do not.
// EXTENSION POINT: the joker/power inventories will live here (session-scoped state),
// subscribing to each new RoundEngine's events in StartRound.

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

        private readonly List<BlockCard> ownedCards = new List<BlockCard>();

        /// <summary>The player's whole collection ("oyun destesi").</summary>
        public IReadOnlyList<BlockCard> OwnedCards
        {
            get { return ownedCards; }
        }

        private readonly IRandomSource rng;
        private readonly IScoreCalculator scorer;
        private int nextCardId = 1;

        public event Action<GamePhase> PhaseChanged;

        public GameSession(GameConfig config)
        {
            Config = config;
            rng = new SeededRandom(config.RngSeed ?? Environment.TickCount);
            scorer = new DefaultScoreCalculator(config.Scoring);
            Market = new MarketStub();
            for (int i = 0; i < config.StartingDeckSize; i++)
            {
                ownedCards.Add(CreateRandomCard());
            }
            RoundNumber = 1;
            StartRound();
        }

        /// <summary>Leaves the (empty) market and starts the next round.</summary>
        public void LeaveMarket()
        {
            if (Phase != GamePhase.Market)
            {
                throw new InvalidOperationException("Not in the market phase.");
            }
            RoundNumber++;
            StartRound();
        }

        private BlockCard CreateRandomCard()
        {
            BlockShape shape = Config.ShapeGenerator.NextShape(rng);
            return new BlockCard(nextCardId++, shape);
        }

        private void StartRound()
        {
            RoundConfig roundConfig = Config.Progression.GetRound(RoundNumber);
            CurrentRound = new RoundEngine(roundConfig, Config.Rules, ownedCards, rng, scorer);
            CurrentRound.TurnResolved += OnTurnResolved;
            CurrentRound.StatusChanged += OnRoundStatusChanged;
            SetPhase(GamePhase.Round);
            if (CurrentRound.Status == RoundStatus.Lost)
            {
                // Degenerate case (e.g. deck smaller than hand size): lost on round start.
                SetPhase(GamePhase.GameOver);
            }
        }

        private void OnTurnResolved(TurnReport report)
        {
            TotalScore += report.ScoreGained;
        }

        private void OnRoundStatusChanged(RoundStatus status)
        {
            if (status == RoundStatus.Advanced)
            {
                SetPhase(GamePhase.Market);
            }
            else if (status == RoundStatus.Lost)
            {
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
