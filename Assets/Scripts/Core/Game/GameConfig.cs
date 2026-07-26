// PURPOSE: Everything needed to start a run, with the pluggable systems exposed as
// interfaces so variants can be swapped without touching game code.
// EXTENSION POINT: starting jokers/powers, unlockable deck archetypes ("small blocks
// weighted" etc.), and market configuration belong here later.

namespace ProjectBlock.Core
{
    /// <summary>Run setup. Plain mutable object: build one, tweak, hand to GameSession.</summary>
    public sealed class GameConfig
    {
        /// <summary>Seed for the whole run; null = time-based random seed.</summary>
        public int? RngSeed = null;

        /// <summary>Starting-deck recipe (see DeckLibrary for the built-in archetypes).
        /// Its Size must be at least Rules.HandSize or the first refill loses instantly.
        /// Market offers also draw from this deck's shape source.</summary>
        public DeckDefinition Deck = DeckLibrary.Classic;

        /// <summary>Rounds in a full run. Surviving the LAST one wins the run (GamePhase.RunWon)
        /// instead of opening another market - see GameSession.OnRoundStatusChanged. Kept here
        /// rather than on IRoundProgression so run LENGTH and the difficulty CURVE stay
        /// independent: a variant run can be shorter or longer without a new progression.</summary>
        public int TotalRounds = 15;

        /// <summary>DEBUG/TEST: DefId of the boss every flagged round must draw, or null for the
        /// normal random draw. Set it and the whole real path still runs - the boss is drawn before
        /// the engine exists, so one that reshapes the round ("Dort kutup") still does.</summary>
        public string ForcedBossDefId;

        public RoundRules Rules = new RoundRules();
        public ScoringConfig Scoring = new ScoringConfig();
        public MarketConfig Market = new MarketConfig();
        public IRoundProgression Progression = new DefaultRoundProgression();
    }
}
