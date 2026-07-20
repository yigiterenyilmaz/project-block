// PURPOSE: The run-scoped context handed to session-level joker hooks (session +
// rng). RoundContext/TurnContext extend the reach for round- and turn-level hooks.

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
}
