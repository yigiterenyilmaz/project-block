// PURPOSE: The mid-turn context handed to in-turn joker hooks - the round, the
// TurnReport-in-progress, and the ScoreBreakdown a joker adds flats/mults to.

namespace ProjectBlock.Core
{
    /// <summary>
    /// The context of a turn that is still resolving. Jokers may read the half-filled
    /// TurnReport and change this turn's score; anything else must go through Round.
    /// </summary>
    public sealed class TurnContext
    {
        public GameSession Session { get; }
        public IRandomSource Rng { get; }
        public RoundEngine Round { get; }

        /// <summary>The report being filled right now. Complete only up to the current hook.</summary>
        public TurnReport Report { get; }

        /// <summary>This turn's score, still open for flat bonuses and multipliers.</summary>
        public ScoreBreakdown Score { get; }

        public TurnContext(GameSession session, IRandomSource rng, RoundEngine round,
            TurnReport report, ScoreBreakdown score)
        {
            Session = session;
            Rng = rng;
            Round = round;
            Report = report;
            Score = score;
        }

        /// <summary>Read from the engine, not the session: this is the same instance the
        /// engine consults live on every refill.</summary>
        public RoundRules Rules
        {
            get { return Round.Rules; }
        }

        public ScoringConfig Scoring
        {
            get { return Session.Config.Scoring; }
        }

        public bool Overtime
        {
            get { return Round.ThresholdPassed; }
        }

        /// <summary>The card placed this turn (hand or bonus hand).</summary>
        public BlockCard PlayedCard
        {
            get { return Report.Card; }
        }

        /// <summary>Adds flat points to this turn. Works from every in-turn hook: after the
        /// score is finalized the engine still routes late additions into the round score,
        /// so one turn always has exactly one total and one rounding.</summary>
        public void AddFlatScore(int amount, string source)
        {
            Round.AddLateTurnScore(amount, source);
        }

        /// <summary>Multiplies this turn's score. Only valid before the score is finalized
        /// (i.e. from ModifyScore); later calls throw so a joker cannot silently no-op.</summary>
        public void AddMultiplier(double factor, string source)
        {
            Round.AddTurnMultiplier(factor, source);
        }
    }
}
