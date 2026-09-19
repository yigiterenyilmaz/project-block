// PURPOSE: "Meydan Okuma" - the dare joker. A few blocks into a round it marks one row or
// column and challenges the player to clear it within a deadline for a bonus. Miss it and
// it re-marks somewhere else for half the bonus; miss that and it halves again; miss the
// third and it gives up for the round. Land any one of them and it is done for the round.
//
// (designer's call, 2026-09-19) THE BONUS IS A SHARE OF THE ROUND'S BAR - 15% of
// RoundEngine.ScoreThreshold, fixed when the round starts - so it stays worth chasing in round
// fifteen. And A MISS IS ALSO clearing ANY OTHER line while the dare is live: the dare moves and
// halves at once, exactly as if its deadline had run out.
//
// The whole event happens once per round: once it pays out OR runs out of attempts, it
// stays quiet until the next round.
//
// The marked line is tracked as a 0-BASED row/column index, the same space TurnReport uses
// for ExplodedRows / ExplodedColumns, so "did the marked line explode" is a direct lookup.
//
// WHICH LINE IS NOT RANDOM ANY MORE. It used to be any row or column at all, which made the
// bonus a lottery: sometimes the full reward for a line one cube from going off with the right
// block already in hand, sometimes a line nothing could ever fill. Now every time a mark is laid
// the joker measures the SEA OF CHANCES (LineChanceSea) - for each line, how likely it is to be
// cleared within the deadline, from the hand, the draw pile and the board - and walks a LADDER:
//
//   THE FIRST, FULL-BONUS DARE goes on the HARDEST line that can still go off at all.
//   EACH HALVING moves toward a more reasonable line (AttemptTargets).
//   A GIMME IS NEVER DARED, at any attempt (GimmeChance). A line full but for one cell with a
//   block that fits it already in hand is not a dare - it is a line that is about to go.
//   A LINE THAT CAN NEVER GO OFF IS NEVER DARED either (dead, sealed, explosions suppressed):
//   that is a rigged bet, not a hard one.
//   A MISSED LINE IS NOT DARED AGAIN straight away.
//
// When the board offers no honest dare - every line a gimme, or none that can go off - no mark
// is laid and it looks again next turn. The bonus is worked out from how many dares have been
// laid, so waiting a turn can never reset it back to full.
//
// SAVE FORMAT: the tuning below is const and the sea is [NotSaved]. (The share-of-the-bar rework
// added saved fields, and the format version moved with it.) The sea is re-measured from the
// saved board whenever it is next needed, and it is seeded from that state, so a loaded run
// reaches the same dare.
//
// WHAT THE PLAYER SEES comes from LastEvent (ChallengeVisuals): one report per turn that says
// whether the dare was laid, ticked, paid, missed or ran out, with the contract before and after.
// The deadline a dare STARTED with is kept beside it for the countdown's look only, and never
// saved (a loaded run treats the turns it has left as the whole deadline).
//
// All numbers are BALANCE PLACEHOLDERS.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Meydan Okuma" - marks a line and dares the player to clear it for a bonus.</summary>
    public sealed class MeydanOkumaJoker : Joker
    {
        /// <summary>The mark is not laid until the board has this many turns of blocks on it,
        /// so the player is not asked to fill a line on an empty board.</summary>
        public int ArmAfterTurns = 3;

        /// <summary>A FIXED bonus for clearing the first mark, overriding the share below. 0 (the
        /// default) means "use BaseSharePercent of the round's threshold". Halves on every miss.</summary>
        public int BaseBonus = 0;

        /// <summary>The first dare's bonus as a percentage of the round's score threshold.</summary>
        public double BaseSharePercent = 15.0;

        /// <summary>This round's first-dare bonus, fixed at round start (logical points).</summary>
        private int roundBase;

        /// <summary>Points this joker has paid over the run, in SCREEN points - what the card
        /// shows under it.</summary>
        private long pointsEarned;

        /// <summary>Floor for the deadline, in turns: max(3, empty cells in the marked line).</summary>
        public int MinDeadline = 3;

        private const int MaxAttempts = 3;

        /// <summary>A line at least this likely to be cleared in time is never dared - it is
        /// going to go off anyway.</summary>
        public const double GimmeChance = 0.75;

        /// <summary>The chance each attempt aims for, in order: the full-bonus dare on the
        /// hardest line that can still go off, then more reasonable lines as the bonus halves.
        /// </summary>
        public static readonly double[] AttemptTargets = { 0.0, 0.35, 0.6 };

        /// <summary>Lines whose chance is this close to the best match are treated as equally
        /// good, and one of them is chosen - so the same board does not always get the same
        /// line, and a few per cent of sampling noise does not decide it.</summary>
        public const double TieWidth = 0.05;

        /// <summary>
        /// The sea the last mark was chosen from, one entry per row and column. For the View and
        /// the lab - "why this line" should be something you can look at. Per-turn and
        /// meaningless across a load, so never saved.
        /// </summary>
        [field: NotSaved]
        public List<LineChance> LastSea { get; private set; }

        private bool resolved;         // paid out or ran out of attempts this round
        private int attemptsMade;      // marks laid so far (1..3)
        private int currentBonus;
        private int turnsLeft;
        private bool markIsRow;
        private int markedLine;        // 0-based row (Y) or column (X) index
        private bool hasMark;

        /// <summary>The deadline the live dare was laid with - presentation only.</summary>
        [NotSaved]
        private int markDeadline;

        /// <summary>This turn's event for the View: a new object per event, never saved.</summary>
        [field: NotSaved]
        public ChallengeVisuals LastEvent { get; private set; }

        public MeydanOkumaJoker()
            : base("meydan_okuma", "Meydan Okuma")
        {
            SetDescription(
                "A few turns in, it dares you to clear a marked row or column within a "
                    + "deadline, for 15% of the round's target score. If the deadline runs out, "
                    + "or you clear ANY other line first, the dare moves and the bonus halves - "
                    + "up to three tries; clear the marked one and it is done for the round.",
                "Birkaç tur sonra işaretlediği bir satırı ya da sütunu süre dolmadan "
                    + "patlatman için rauntun hedef puanının %15'ini vaat eder. Süre dolarsa ya "
                    + "da önce BAŞKA bir satır/sütun patlatırsan hedef yer değiştirir ve bonus "
                    + "yarıya iner - en fazla üç deneme; işaretliyi patlatınca o raunt biter.");
        }

        /// <summary>True once the event is over for the round (paid out or three misses).</summary>
        public bool IsResolved
        {
            get { return resolved; }
        }

        /// <summary>Marked line, for the UI to highlight. Null when nothing is challenged.</summary>
        public bool HasActiveMark
        {
            get { return hasMark && !resolved; }
        }

        public bool MarkIsRow
        {
            get { return markIsRow; }
        }

        public int MarkedLine
        {
            get { return markedLine; }
        }

        public int TurnsLeft
        {
            get { return turnsLeft; }
        }

        public int CurrentBonus
        {
            get { return currentBonus; }
        }

        /// <summary>Dares laid this round (0..3); the live one is this number.</summary>
        public int AttemptsMade
        {
            get { return attemptsMade; }
        }

        /// <summary>The deadline the live dare started with (its turns left after a load).</summary>
        public int InitialTurns
        {
            get { return markDeadline > 0 ? markDeadline : turnsLeft; }
        }

        /// <summary>After a miss with attempts left and no honest line yet: what the next dare
        /// will be worth. 0 otherwise.</summary>
        public int PendingBonus
        {
            get { return !resolved && !hasMark && attemptsMade > 0 ? RoundBase >> attemptsMade : 0; }
        }

        /// <summary>The first dare's bonus this round: the fixed override when one is set,
        /// otherwise the share fixed at round start.</summary>
        public int RoundBase
        {
            get { return BaseBonus > 0 ? BaseBonus : roundBase; }
        }

        /// <summary>What the first dare of a round with this bar would be worth - for the lab,
        /// which has no round of the joker's own to read it from.</summary>
        public int BaseBonusFor(RoundEngine round)
        {
            if (BaseBonus > 0)
            {
                return BaseBonus;
            }
            int threshold = round != null ? round.ScoreThreshold : 0;
            return Math.Max(1, (int)Math.Round(threshold * BaseSharePercent / 100.0));
        }

        /// <summary>Statistics: one proc per dare landed, worth what it paid.</summary>
        public override bool TracksProcs
        {
            get { return true; }
        }

        public override string StatusText
        {
            get
            {
                if (resolved)
                {
                    return Loc.Pick("done", "bitti");
                }
                // The POINTS it has earned, never the line - the line is drawn on the board.
                return pointsEarned > 0
                    ? Loc.Pick("+" + pointsEarned + " earned", "+" + pointsEarned + " kazandı")
                    : Loc.Pick("nothing earned yet", "henüz kazanmadı");
            }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            roundBase = BaseBonusFor(ctx.Round);
            resolved = false;
            attemptsMade = 0;
            hasMark = false;
            currentBonus = 0;
            turnsLeft = 0;
            markDeadline = 0;
            LastEvent = null;
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (resolved)
            {
                return;
            }
            ChallengeVisuals missed = null;
            if (hasMark)
            {
                var before = new ChallengeVisuals
                {
                    OldIsRow = markIsRow,
                    OldLine = markedLine,
                    OldBonus = currentBonus,
                    OldAttempt = attemptsMade
                };
                // The mark is live: did the player clear it this turn?
                if (MarkedLineExploded(turn.Report))
                {
                    int scoreBefore = turn.Round.RoundScore;
                    turn.AddFlatScore(currentBonus, DefId);
                    NoteProc(currentBonus, turn);
                    resolved = true;
                    hasMark = false;
                    before.Event = ChallengeEvent.Succeeded;
                    before.ScoreDelta = turn.Round.RoundScore - scoreBefore;
                    pointsEarned += Math.Max(0, before.ScoreDelta);
                    LastEvent = before;
                    return;
                }
                // Not this turn - the deadline ticks. Clearing ANY OTHER line first is a miss on
                // the spot: the dare was for THAT line.
                turnsLeft--;
                if (OtherLineExploded(turn.Report))
                {
                    turnsLeft = 0;
                }
                if (turnsLeft > 0)
                {
                    before.Event = ChallengeEvent.Ticked;
                    LastEvent = Live(before);
                    return;
                }
                // Missed. markedLine / markIsRow still name it, which is what keeps the next dare
                // off the same line.
                hasMark = false;
                if (attemptsMade >= MaxAttempts)
                {
                    resolved = true;
                    before.Event = ChallengeEvent.Expired;
                    LastEvent = before;
                    return;
                }
                before.Event = ChallengeEvent.Failed;
                before.NextBonus = RoundBase >> attemptsMade;
                LastEvent = before;
                missed = before;
            }
            else if (attemptsMade == 0 && turn.Round.TurnNumber < ArmAfterTurns)
            {
                return; // not until enough blocks are down
            }
            LayMark(turn);
            if (!hasMark)
            {
                return; // no honest dare this turn - a miss stays a miss with no new line yet
            }
            if (missed != null)
            {
                Live(missed); // the miss and the new line are one event
            }
            else
            {
                LastEvent = Live(new ChallengeVisuals { Event = ChallengeEvent.Started });
            }
        }

        /// <summary>Writes the live contract into a report.</summary>
        private ChallengeVisuals Live(ChallengeVisuals report)
        {
            report.HasTarget = true;
            report.IsRow = markIsRow;
            report.Line = markedLine;
            report.Bonus = currentBonus;
            report.Attempt = attemptsMade;
            report.TurnsLeft = turnsLeft;
            report.InitialTurns = InitialTurns;
            return report;
        }

        /// <summary>The deadline the dare gives a line with this many gaps. Public because the
        /// sea has to measure odds against the SAME deadline the player will get.</summary>
        public int DeadlineFor(int gaps)
        {
            return gaps > MinDeadline ? gaps : MinDeadline;
        }

        /// <summary>
        /// Lays the next dare on the line the ladder picks from the sea of chances, or lays
        /// nothing when the board offers no honest dare and tries again next turn.
        /// </summary>
        private void LayMark(TurnContext turn)
        {
            RoundEngine round = turn.Round;
            uint seed = SeedFrom(round, attemptsMade);
            LastSea = LineChanceSea.Measure(round, DeadlineFor, LineChanceSea.DefaultSamples, seed);
            LineChance previous = attemptsMade > 0
                ? new LineChance { IsRow = markIsRow, Index = markedLine }
                : null;
            LineChance pick = Choose(LastSea, attemptsMade, previous, seed);
            if (pick == null)
            {
                return;
            }
            // The bonus comes from how many dares have been laid, never from a running halving:
            // a turn spent waiting for an honest dare must not be able to reset it.
            currentBonus = RoundBase >> attemptsMade;
            markIsRow = pick.IsRow;
            markedLine = pick.Index;
            turnsLeft = pick.Deadline;
            markDeadline = pick.Deadline;
            hasMark = true;
            attemptsMade++;
        }

        /// <summary>
        /// THE LADDER. Of the lines that can go off, are not gimmes and were not just missed, the
        /// one whose chance is nearest this attempt's target - the first attempt's target being
        /// zero, which is simply the hardest. Lines within TieWidth of the best are equals, and
        /// one of them is taken by the seed. Null when nothing qualifies.
        ///
        /// Public and static so the rule can be tested on a sea written by hand, apart from the
        /// sampling that produces a real one.
        /// </summary>
        public static LineChance Choose(IReadOnlyList<LineChance> sea, int attempt,
            LineChance previous, uint seed)
        {
            if (sea == null)
            {
                return null;
            }
            double target = AttemptTargets[attempt < AttemptTargets.Length
                ? attempt
                : AttemptTargets.Length - 1];
            double bestDistance = double.MaxValue;
            for (int i = 0; i < sea.Count; i++)
            {
                if (Eligible(sea[i], previous))
                {
                    bestDistance = Math.Min(bestDistance, Math.Abs(sea[i].Chance - target));
                }
            }
            if (bestDistance == double.MaxValue)
            {
                return null;
            }
            var ties = new List<LineChance>();
            for (int i = 0; i < sea.Count; i++)
            {
                if (Eligible(sea[i], previous)
                    && Math.Abs(sea[i].Chance - target) <= bestDistance + TieWidth)
                {
                    ties.Add(sea[i]);
                }
            }
            return ties[(int)(seed % (uint)ties.Count)];
        }

        private static bool Eligible(LineChance line, LineChance previous)
        {
            if (line == null || !line.Possible || line.Chance >= GimmeChance)
            {
                return false;
            }
            return previous == null || line.IsRow != previous.IsRow || line.Index != previous.Index;
        }

        /// <summary>From the round's own state only - never its IRandomSource, which the sea must
        /// not draw from - so a replayed save reaches the same dare from the same board.</summary>
        private static uint SeedFrom(RoundEngine round, int attempt)
        {
            uint h = 2166136261u;
            h = (h ^ (uint)round.TurnNumber) * 16777619u;
            h = (h ^ (uint)attempt) * 16777619u;
            h = (h ^ (uint)round.Board.OccupiedCount) * 16777619u;
            h = (h ^ (uint)round.Deck.DrawCount) * 16777619u;
            for (int i = 0; i < round.Hand.Count; i++)
            {
                h = (h ^ (uint)round.Hand[i].Id) * 16777619u;
            }
            return h;
        }

        /// <summary>True if a row or column OTHER than the dared one went off this turn.</summary>
        private bool OtherLineExploded(TurnReport report)
        {
            IReadOnlyList<int> rows = report.ExplodedRows;
            IReadOnlyList<int> columns = report.ExplodedColumns;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!markIsRow || rows[i] != markedLine)
                {
                    return true;
                }
            }
            for (int i = 0; i < columns.Count; i++)
            {
                if (markIsRow || columns[i] != markedLine)
                {
                    return true;
                }
            }
            return false;
        }

        private bool MarkedLineExploded(TurnReport report)
        {
            IReadOnlyList<int> exploded = markIsRow ? report.ExplodedRows : report.ExplodedColumns;
            for (int i = 0; i < exploded.Count; i++)
            {
                if (exploded[i] == markedLine)
                {
                    return true;
                }
            }
            return false;
        }

    }
}
