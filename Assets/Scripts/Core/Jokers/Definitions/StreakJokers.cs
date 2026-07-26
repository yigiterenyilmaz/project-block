// PURPOSE: The streak jokers. Three of them compare this block with the previous one - çığ
// (bigger), dondurma (smaller), Siyam (identical shape) - and share StreakJoker, which owns
// the streak bookkeeping so each joker only answers one question: does this placement
// continue the run? The fourth, Mikrodalga, is a streak joker of a different kind: it does
// not count placements at all, it bends the engine's own COMBO streak so it survives a quiet
// turn.
//
// CONFIRMED RULES:
//  - Size means CUBE COUNT (BlockShape.Size), not bounding box.
//  - A placement that does not continue the run RESTARTS it at 1 with the new card as the
//    baseline - it never merely pauses. (Otherwise Siyam turns would be free protection
//    for a çığ streak.)
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
    /// <summary>Shared streak counting for çığ / dondurma / Siyam. All numbers are
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

    /// <summary>"çığ" - each block bigger than the last one.</summary>
    public sealed class CigJoker : StreakJoker
    {
        public CigJoker()
            : base("cig", "Çığ")
        {
            SetDescription(
                "Score bonus for placing a BIGGER block than last turn; grows as the streak lasts.",
                "Her tur bir öncekinden BÜYÜK blok koyarsan puan bonusu; seri uzadıkça büyür.");
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
                "Her tur bir öncekinden KÜÇÜK blok koyarsan puan bonusu; seri uzadıkça büyür.");
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
                "Her tur bir öncekiyle AYNI şekli koyarsan puan bonusu; seri uzadıkça büyür.");
            MinStreak = 2;          // no natural ceiling, so it pays from the first repeat
            PointsPerStreakStep = 25;
        }

        protected override bool Continues(BlockShape previous, BlockShape current)
        {
            return previous.CanonicalKey == current.CanonicalKey;
        }
    }

    /// <summary>
    /// "Mikrodalga" - a combo kept warm. Two explosions with ONE quiet turn between them still
    /// count as a combo: the streak does not reset on that turn, it merely sleeps, and the next
    /// clear picks it up where it left off. Two quiet turns in a row still end it, so this buys
    /// a breath, not immunity.
    ///
    /// Reheated food is not fresh food: the turn that ends a gap pays a REDUCED combo bonus
    /// (BridgedScorePercent), while a streak carried turn after turn keeps paying in full. So
    /// the joker is worth most to a player who nearly always clears - it patches the occasional
    /// hole in a real streak rather than manufacturing one out of every other turn.
    ///
    /// It bends a rule instead of paying a bonus, so nothing here touches the score directly:
    /// the two knobs live on RoundRules (ComboBridgeTurns, ComboBridgedScorePercent) and the
    /// engine reads them live while it counts the streak. Both are inert at their defaults,
    /// which is what keeps an ordinary run byte-identical to the base game.
    ///
    /// The numbers are BALANCE PLACEHOLDERS.
    /// </summary>
    public sealed class MikrodalgaJoker : Joker
    {
        /// <summary>Quiet turns a streak survives. One - "iki patlama arasında bir tur".</summary>
        public int BridgeTurns = 1;

        /// <summary>What the combo pays on the turn that ends a gap, in percent.</summary>
        public int BridgedScorePercent = 50;

        public MikrodalgaJoker()
            : base("mikrodalga", "Mikrodalga")
        {
            SetDescription(
                "Your combo survives ONE turn without a clear - two explosions with a quiet turn "
                    + "between them still count as a combo. The turn that picks the streak back "
                    + "up pays half the combo bonus; an unbroken streak still pays in full.",
                "Kombon araya giren BİR turu affeder - arasında bir tur bulunan iki patlama da "
                    + "kombodan sayılır. Seriyi geri alan tur kombo bonusunun yarısını öder; "
                    + "hiç bozulmayan seri tam ödemeye devam eder.");
        }

        public override string StatusText
        {
            get { return Loc.Pick("combo kept warm", "kombo sıcak tutuluyor"); }
        }

        public override void OnAcquired(SessionContext ctx)
        {
            Apply(ctx.Rules);
        }

        public override void OnRemoved(SessionContext ctx)
        {
            ctx.Rules.ComboBridgeTurns = 0;
            ctx.Rules.ComboBridgedScorePercent = 100;
        }

        // RoundRules is shared and another effect may have reset it; re-assert each round.
        public override void OnRoundStarted(RoundContext ctx)
        {
            Apply(ctx.Rules);
        }

        private void Apply(RoundRules rules)
        {
            rules.ComboBridgeTurns = BridgeTurns;
            rules.ComboBridgedScorePercent = BridgedScorePercent;
        }
    }
}
