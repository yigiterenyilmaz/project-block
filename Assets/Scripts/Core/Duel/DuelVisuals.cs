// PURPOSE: What the duel ("Blackjack") reports to the View - reporting only, never read back by a
// rule and never saved. A NEW object per event, so the View matches them by identity (see
// CLAUDE.md: "a per-turn report is matched by identity, never by its serial").
//
// The computer's plays are copied out of its turn reports rather than handed over as the reports
// themselves: a round reuses some of a report's lists from turn to turn, and when the computer
// plays out several cards at once only the last report's lists would still be true.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One card the computer laid on its own board.</summary>
    public sealed class DuelAiPlay
    {
        public BlockCard Card;
        public DuelMove Move;

        /// <summary>The house's arena just before and just after this play - copies, so the View
        /// can show the card going down and the lines going off one play at a time even when the
        /// house plays out several cards at once.</summary>
        public GameBoard BoardBefore;
        public GameBoard BoardAfter;

        /// <summary>Where in its CLOSED hand the card was held (0 = first), how many cards that
        /// hand held before the play and how many after its refill - so the View can lift that
        /// very card out of the fan and slide in a face-down replacement, never showing which
        /// card that replacement is.</summary>
        public int HandSlot = -1;
        public int HandBefore;
        public int HandAfter;

        /// <summary>The cells its cubes landed in, and the lines that went off.</summary>
        public List<GridPos> PlacedCells = new List<GridPos>();
        public List<int> ExplodedRows = new List<int>();
        public List<int> ExplodedColumns = new List<int>();

        /// <summary>What the play banked, and the computer's hand total after it (scaled).</summary>
        public int Gained;
        public int HandScoreAfter;
        public int ComboCount;
        public bool CleanSweep;
    }

    /// <summary>Everything the computer played in answer to one turn of the player's - one card,
    /// or the rest of its half when the player had finished.</summary>
    public sealed class DuelAiTurns
    {
        public readonly List<DuelAiPlay> Plays = new List<DuelAiPlay>();
    }

    public enum DuelHandOutcome
    {
        Lose = 0,
        Push = 1,
        Win = 2
    }

    /// <summary>A hand settled: who scored what, and what the bet paid.</summary>
    public sealed class DuelHandResult
    {
        public int HandNumber;
        public long Bet;
        public int PlayerScore;
        public int AiScore;
        public DuelHandOutcome Outcome;

        /// <summary>What came back to the purse: twice the bet for a win, the bet for a push.</summary>
        public long Payout;
        public long PurseAfter;
        public long Target;

        /// <summary>The purse reached the target: the stage is beaten.</summary>
        public bool StageWon;

        /// <summary>The purse ran dry: the run is over.</summary>
        public bool Bankrupt;
    }
}
