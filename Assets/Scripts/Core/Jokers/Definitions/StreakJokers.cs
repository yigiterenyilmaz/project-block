// PURPOSE: The three "compare this block with the previous one" jokers: Ã§Ä±ÄŸ (bigger),
// dondurma (smaller), Siyam (identical shape). They share StreakJoker, which owns the
// streak bookkeeping so each joker only answers one question: does this placement
// continue the run?
//
// CONFIRMED RULES:
//  - Size means CUBE COUNT (BlockShape.Size), not bounding box.
//  - A placement that does not continue the run RESTARTS it at 1 with the new card as the
//    baseline - it never merely pauses. (Otherwise Siyam turns would be free protection
//    for a Ã§Ä±ÄŸ streak.)
//  - Bonus-hand plays are turns too, so they take part in every streak.
//  - "Same shape" is exact normalized-shape equality (BlockShape.CanonicalKey). Rotations
//    and mirrors are NOT the same shape - the base game never rotates a block.
//  - Streaks are per round: a new round starts from nothing.
//
// BALANCE NOTE: the default generator makes blocks of 1..5 cubes, so a strictly
// increasing (or decreasing) run can never exceed 5 turns. MinStreak must stay well under
// that ceiling or these jokers can never pay out. Siyam has no such ceiling, so it asks
// for a shorter streak by default.

namespace ProjectBlock.Core
{
    /// <summary>Shared streak counting for Ã§Ä±ÄŸ / dondurma / Siyam. All numbers are
    /// BALANCE PLACEHOLDERS.</summary>
    public abstract class StreakJoker : Joker
    {
        protected StreakJoker(string defId, string displayName)
            : base(defId, displayName)
        {
        }

        /// <summary>Turns in a row (including the baseline placement) needed before the
        /// joker pays anything.</summary>
        public int MinStreak = 3;

        /// <summary>Points added per streak step once MinStreak is reached, so a longer run
        /// is worth more than the same run restarted ("birikebilir").</summary>
        public int PointsPerStreakStep = 15;

        /// <summary>Length of the run currently in progress. 0 before the first placement.</summary>
        public int Streak { get; private set; }

        /// <summary>The block this joker compares the next placement against.</summary>
        protected BlockShape PreviousShape { get; private set; }

        public override string StatusText
        {
            get
            {
                string star = Streak >= MinStreak ? " *" : string.Empty;
                return Loc.Pick("streak " + Streak + star, "seri " + Streak + star);
            }
        }

        /// <summary>True if placing <paramref name="current"/> after <paramref name="previous"/>
        /// continues the streak.</summary>
        protected abstract bool Continues(BlockShape previous, BlockShape current);

        public override void OnRoundStarted(RoundContext ctx)
        {
            Streak = 0;
            PreviousShape = null;
        }

        public override void ModifyScore(TurnContext turn)
        {
            BlockShape current = turn.PlayedCard.Shape;
            if (PreviousShape != null && Continues(PreviousShape, current))
            {
                Streak++;
            }
            else
            {
                Streak = 1; // the new card becomes the baseline of a fresh run
            }
            PreviousShape = current;

            if (Streak < MinStreak)
            {
                return;
            }
            int steps = Streak - MinStreak + 1;
            turn.Score.AddFlat(PointsPerStreakStep * steps, DefId);
        }
    }

    /// <summary>"Ã§Ä±ÄŸ" - each block bigger than the last one.</summary>
    public sealed class CigJoker : StreakJoker
    {
        public CigJoker()
            : base("cig", "Ã‡Ä±ÄŸ")
        {
            SetDescription(
                "Score bonus for placing a BIGGER block than last turn; grows as the streak lasts.",
                "Her tur bir Ã¶ncekinden BÃœYÃœK blok koyarsan puan bonusu; seri uzadÄ±kÃ§a bÃ¼yÃ¼r.");
        }

        protected override bool Continues(BlockShape previous, BlockShape current)
        {
            return current.Size > previous.Size;
        }
    }

    /// <summary>"dondurma" - each block smaller than the last one.</summary>
    public sealed class DondurmaJoker : StreakJoker
    {
        public DondurmaJoker()
            : base("dondurma", "Dondurma")
        {
            SetDescription(
                "Score bonus for placing a SMALLER block than last turn; grows as the streak lasts.",
                "Her tur bir Ã¶ncekinden KÃœÃ‡ÃœK blok koyarsan puan bonusu; seri uzadÄ±kÃ§a bÃ¼yÃ¼r.");
        }

        protected override bool Continues(BlockShape previous, BlockShape current)
        {
            return current.Size < previous.Size;
        }
    }

    /// <summary>"Siyam" - the same shape as last turn, over and over.</summary>
    public sealed class SiyamJoker : StreakJoker
    {
        public SiyamJoker()
            : base("siyam", "Siyam")
        {
            SetDescription(
                "Score bonus for placing the SAME shape as last turn; grows as the streak lasts.",
                "Her tur bir Ã¶ncekiyle AYNI ÅŸekli koyarsan puan bonusu; seri uzadÄ±kÃ§a bÃ¼yÃ¼r.");
            MinStreak = 2;          // no natural ceiling, so it pays from the first repeat
            PointsPerStreakStep = 25;
        }

        protected override bool Continues(BlockShape previous, BlockShape current)
        {
            return previous.CanonicalKey == current.CanonicalKey;
        }
    }
}
