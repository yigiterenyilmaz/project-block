// PURPOSE: The handles a joker is given when one of its hooks fires. Jokers never reach
// for global state - everything they may read or change arrives through a context, which
// keeps them testable and keeps all randomness on the single seeded session RNG.
//
// Three scopes, matching the three lifetimes in the game:
//   SessionContext - the run (market, inventory, owned cards)
//   RoundContext   - one round (adds the live RoundEngine)
//   TurnContext    - one resolving turn (adds the in-progress report and this turn's score)
//
// EXTENSION POINT: powers ("gucler") will use the same contexts. When the market lands,
// give SessionContext the market handle rather than passing it separately.

namespace ProjectBlock.Core
{
    /// <summary>What a joker is allowed to touch outside a round.</summary>
    public class SessionContext
    {
        public GameSession Session { get; }

        /// <summary>The ONE session RNG. Never use anything else, replays depend on it.</summary>
        public IRandomSource Rng { get; }

        public SessionContext(GameSession session, IRandomSource rng)
        {
            Session = session;
            Rng = rng;
        }

        /// <summary>Live rules object - mutate to bend the rules (hand size, charges...).</summary>
        public RoundRules Rules
        {
            get { return Session.Config.Rules; }
        }

        /// <summary>Live scoring tunables - mutate for permanent score buffs.</summary>
        public ScoringConfig Scoring
        {
            get { return Session.Config.Scoring; }
        }
    }

    /// <summary>SessionContext plus the round that is currently being played.</summary>
    public sealed class RoundContext : SessionContext
    {
        public RoundEngine Round { get; }

        public RoundContext(GameSession session, IRandomSource rng, RoundEngine round)
            : base(session, rng)
        {
            Round = round;
        }

        /// <summary>True once the threshold was passed - "uzatma" (overtime) is running.</summary>
        public bool Overtime
        {
            get { return Round.ThresholdPassed; }
        }
    }

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

    /// <summary>What a player-activated joker was pointed at. All fields optional.</summary>
    public readonly struct ActivationTarget
    {
        /// <summary>Index into the hand (Iade picks the card to swap).</summary>
        public readonly int? HandIndex;

        /// <summary>A board cell (future: Enfeksiyon picks the cube to infect).</summary>
        public readonly GridPos? Cell;

        public ActivationTarget(int? handIndex, GridPos? cell)
        {
            HandIndex = handIndex;
            Cell = cell;
        }

        public static readonly ActivationTarget None = new ActivationTarget(null, null);

        public static ActivationTarget Hand(int handIndex)
        {
            return new ActivationTarget(handIndex, null);
        }

        public static ActivationTarget Board(GridPos cell)
        {
            return new ActivationTarget(null, cell);
        }
    }
}
