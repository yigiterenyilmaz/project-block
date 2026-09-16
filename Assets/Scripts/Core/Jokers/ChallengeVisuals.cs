// PURPOSE: What "Meydan Okuma"'s dare did THIS TURN, written down for the VIEW. Reporting only:
// which line is dared, for how long, for how much, and whether the player made it are all
// decided by MeydanOkumaJoker exactly as before.
//
// IT EXISTS BECAUSE EVERY NUMBER IN THE PICTURE IS A RULE. The next line comes off a measured sea
// of chances, the deadline off the line's gaps, the bonus off how many dares have been LAID (not
// a running halving - a turn spent waiting for an honest dare must not reset it), and "did the
// marked line go off" off the turn's own exploded lines. A View that halved the number itself,
// or checked the explosion itself, would be a second copy of each of those rules. So one report
// per turn says which of five things happened, with the contract as it stood BEFORE (the one that
// was paid, missed or ran out) and as it stands AFTER (the one now live).
//
// A MISS DOES NOT ALWAYS NAME A NEW LINE. When the board offers no honest dare the joker lays
// nothing and looks again next turn; the report then carries only NextBonus - what the next dare
// WILL be worth, which is already fixed - and the new line arrives later as a Started.
//
// A NEW OBJECT PER EVENT, matched by IDENTITY in the View (CLAUDE.md). A turn on which nothing
// happened (still waiting to arm, still no honest dare) writes nothing.

namespace ProjectBlock.Core
{
    public enum ChallengeEvent
    {
        /// <summary>A dare was laid (the first, or a later one after a wait).</summary>
        Started,
        /// <summary>A turn passed without the line going off; the deadline moved.</summary>
        Ticked,
        /// <summary>The marked line went off: the bonus was paid and the dare is over.</summary>
        Succeeded,
        /// <summary>The deadline ran out with attempts left: the bonus fell, and the dare moved
        /// (HasTarget) or will move once the board offers an honest line.</summary>
        Failed,
        /// <summary>The last attempt ran out: nothing more this round.</summary>
        Expired
    }

    /// <summary>How pressed the dare is, from its own turns - three looks, one definition.</summary>
    public enum ChallengeUrgency
    {
        Calm,
        Tension,
        Final
    }

    public sealed class ChallengeVisuals
    {
        public ChallengeEvent Event;

        // ---- the contract as it stood before this turn (Succeeded, Failed, Expired, Ticked)
        public bool OldIsRow;
        /// <summary>0-based, the space TurnReport.ExplodedRows / ExplodedColumns use.</summary>
        public int OldLine;
        public int OldBonus;
        public int OldAttempt;

        // ---- the contract live after this turn (Started, Ticked, Failed with a new line)
        public bool HasTarget;
        public bool IsRow;
        public int Line;
        public int Bonus;
        /// <summary>1..3.</summary>
        public int Attempt;
        /// <summary>Turns still allowed, counting the next one. 1 = the last chance.</summary>
        public int TurnsLeft;
        public int InitialTurns;

        /// <summary>Failed: what the next dare is worth, whether or not it has been laid yet.
        /// </summary>
        public int NextBonus;

        /// <summary>Succeeded: what the round score actually moved by (inversion, scale and floor
        /// included).</summary>
        public int ScoreDelta;

        public ChallengeUrgency Urgency
        {
            get { return UrgencyOf(TurnsLeft, InitialTurns); }
        }

        /// <summary>
        /// The last turn is FINAL; the second half of the deadline is TENSION; the rest is calm.
        /// With the floor of three turns that is one turn of each.
        /// </summary>
        public static ChallengeUrgency UrgencyOf(int turnsLeft, int initialTurns)
        {
            if (turnsLeft <= 1)
            {
                return ChallengeUrgency.Final;
            }
            int initial = initialTurns > 0 ? initialTurns : turnsLeft;
            return turnsLeft * 2 <= initial + 1 ? ChallengeUrgency.Tension : ChallengeUrgency.Calm;
        }
    }
}
