// PURPOSE: The static definition of one round: its board size, the score threshold ("eşik")
// the player must reach to earn the right to advance, how the arena erodes when the deck keeps
// recycling, and whether it is a boss round.
//
// ANYTHING THAT REBUILDS A CONFIG FROM ANOTHER ONE (a joker/power FilterRoundConfig) MUST GO
// THROUGH WithBoard. Rebuilding by hand means listing every field, and a field forgotten there
// is silently lost - a boss round quietly stops being one, or an eroding round stops eroding.
// Both of those bugs have already happened once; WithBoard is what makes them impossible.
//
// EXTENSION POINT: ExtraPlayableCells is how a joker or power hands the round a board that
// is bigger than a plain rectangle ("Kentsel Dönüşüm", "Tılsım"). Jokers rewrite this
// through Joker.FilterRoundConfig, which runs before the board is built.
// OptionalPlayableCells is the same seam for ground that is BONUS: playable, but never
// required to complete the line it sits in ("Tılsım" only - "Kentsel Dönüşüm" hands over
// permanent, ordinary board). Kept a separate list precisely so the two cannot be confused.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Immutable setup values for one round.</summary>
    public sealed class RoundConfig
    {
        private static readonly GridPos[] NoExtraCells = new GridPos[0];

        /// <summary>1-based round index.</summary>
        public int RoundNumber { get; }

        public int BoardWidth { get; }
        public int BoardHeight { get; }

        /// <summary>Round score needed to be offered advancement to the next round.</summary>
        public int ScoreThreshold { get; }

        /// <summary>Cells bolted onto the base rectangle, making the board irregular.
        /// Empty for a normal round. Coordinates must be non-negative: the board grows right
        /// and up, never left or down (see GameBoard).</summary>
        public IReadOnlyList<GridPos> ExtraPlayableCells { get; }

        /// <summary>Cells bolted on as BONUS ground: playable, but skipped by the fullness
        /// check while empty, so they never raise the price of the lines they stretched
        /// ("Tılsım"). Normally a subset of ExtraPlayableCells; a cell named only here is
        /// bolted on just the same. Empty for a normal round.</summary>
        public IReadOnlyList<GridPos> OptionalPlayableCells { get; }

        /// <summary>How this round's arena erodes once the draw pile has run dry more than
        /// RoundRules.FreeDeckRecycles times - the anti-stalling clock. Comes from the round
        /// band (see DefaultRoundProgression), so it is fixed for the whole round.</summary>
        public ShuffleErosion Erosion { get; }

        /// <summary>True for a boss round ("patron raundu"). The progression decides WHICH rounds
        /// are boss rounds (DefaultRoundProgression.BossRoundInterval); this is the single flag
        /// everything else reads, so nothing has to recompute "round number % 3". GameSession
        /// draws the boss itself from BossRegistry when it sees this.</summary>
        public bool IsBossRound { get; }

        public RoundConfig(int roundNumber, int boardWidth, int boardHeight, int scoreThreshold)
            : this(roundNumber, boardWidth, boardHeight, scoreThreshold, null,
                ShuffleErosion.None, false)
        {
        }

        public RoundConfig(int roundNumber, int boardWidth, int boardHeight, int scoreThreshold,
            IReadOnlyList<GridPos> extraPlayableCells)
            : this(roundNumber, boardWidth, boardHeight, scoreThreshold, extraPlayableCells,
                ShuffleErosion.None, false)
        {
        }

        public RoundConfig(int roundNumber, int boardWidth, int boardHeight, int scoreThreshold,
            IReadOnlyList<GridPos> extraPlayableCells, ShuffleErosion erosion)
            : this(roundNumber, boardWidth, boardHeight, scoreThreshold, extraPlayableCells,
                erosion, false)
        {
        }

        /// <summary>The full setup. Only the progression should need this one - a filter that
        /// merely reshapes the board wants WithBoard instead.</summary>
        public RoundConfig(int roundNumber, int boardWidth, int boardHeight, int scoreThreshold,
            IReadOnlyList<GridPos> extraPlayableCells, ShuffleErosion erosion, bool isBossRound)
            : this(roundNumber, boardWidth, boardHeight, scoreThreshold, extraPlayableCells,
                erosion, isBossRound, null)
        {
        }

        /// <summary>The full setup, bonus ground included.</summary>
        public RoundConfig(int roundNumber, int boardWidth, int boardHeight, int scoreThreshold,
            IReadOnlyList<GridPos> extraPlayableCells, ShuffleErosion erosion, bool isBossRound,
            IReadOnlyList<GridPos> optionalPlayableCells)
        {
            RoundNumber = roundNumber;
            BoardWidth = boardWidth;
            BoardHeight = boardHeight;
            ScoreThreshold = scoreThreshold;
            ExtraPlayableCells = extraPlayableCells ?? NoExtraCells;
            OptionalPlayableCells = optionalPlayableCells ?? NoExtraCells;
            Erosion = erosion;
            IsBossRound = isBossRound;
        }

        /// <summary>
        /// A copy of this config with a different board, everything else carried across. THE way
        /// a joker/power FilterRoundConfig should rebuild a config: a field added to this class
        /// later travels automatically instead of being silently dropped (see the file header).
        /// </summary>
        public RoundConfig WithBoard(int boardWidth, int boardHeight,
            IReadOnlyList<GridPos> extraPlayableCells)
        {
            return new RoundConfig(RoundNumber, boardWidth, boardHeight, ScoreThreshold,
                extraPlayableCells, Erosion, IsBossRound, OptionalPlayableCells);
        }

        /// <summary>As above, also replacing the bonus ground. Only a filter that actually
        /// GRANTS optional cells ("Tılsım") wants this one; every other caller uses the
        /// three-argument overload, which carries the existing bonus ground across.</summary>
        public RoundConfig WithBoard(int boardWidth, int boardHeight,
            IReadOnlyList<GridPos> extraPlayableCells,
            IReadOnlyList<GridPos> optionalPlayableCells)
        {
            return new RoundConfig(RoundNumber, boardWidth, boardHeight, ScoreThreshold,
                extraPlayableCells, Erosion, IsBossRound, optionalPlayableCells);
        }
    }
}
