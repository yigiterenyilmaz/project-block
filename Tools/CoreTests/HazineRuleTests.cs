// PURPOSE: "Hazine" - a mark is blown open by ANY destruction of its cube, from any source and at
// any time, and what a find pays is what the rules say it pays. Each test here is a hole the joker
// really had: a power used between turns never counted, a joker further right or the boss
// destroying at the end of a turn never counted, a find on the threshold-crossing turn was paid and
// never shown, and the explosion bonus in overtime paid five times what the line did.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;

public static partial class JokerTests
{
    /// <summary>Destroys the cells it is given at the end of the turn - a stand-in for any
    /// end-of-turn destroyer that sits to the RIGHT of "Hazine" in the inventory.</summary>
    private sealed class LateWreckerJoker : Joker
    {
        public readonly List<GridPos> Cells = new List<GridPos>();

        public LateWreckerJoker()
            : base("test_late_wrecker", "Late wrecker")
        {
        }

        public override void AfterTurnScored(TurnContext turn)
        {
            if (Cells.Count > 0)
            {
                turn.Round.DestroyCubes(new List<GridPos>(Cells), true);
                Cells.Clear();
            }
        }
    }

    /// <summary>Lays one plain cube through a real turn, so the engine knows it is there.</summary>
    private static TurnReport LayCube(GameSession session, GridPos cell)
    {
        RoundEngine round = session.CurrentRound;
        BlockCard card = session.CreateCard(Bar(1), new BlockElement[0]);
        round.AddBonusCard(card, BonusPlayOutcome.ExpireFromRound);
        return round.PlayFromBonus(round.BonusHand.Count - 1, cell);
    }

    /// <summary>A cell of the board that is neither mark nor any of <paramref name="avoid"/>.</summary>
    private static GridPos FreeCell(RoundEngine round, HazineJoker joker, params GridPos[] avoid)
    {
        GameBoard board = round.Board;
        for (int y = board.MinY; y < board.MinY + board.Height; y++)
        {
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                var pos = new GridPos(x, y);
                bool taken = !board.IsInside(pos) || board.GetCube(pos).HasValue
                    || pos.Equals(joker.TreasureCell) || pos.Equals(joker.DynamiteCell);
                for (int i = 0; i < avoid.Length && !taken; i++)
                {
                    taken = pos.Equals(avoid[i]);
                }
                if (!taken)
                {
                    return pos;
                }
            }
        }
        throw new InvalidOperationException("no free cell");
    }

    private static void Hazine_APowerBetweenTurnsBlowsAMarkOpen()
    {
        Section("hazine / a power used between turns counts");
        var session = NewSession(509, 6, 1000000, 40, 1);
        var joker = (HazineJoker)session.Jokers.Add(new HazineJoker());
        Power cross = session.Powers.Add(new CaprazlamaPower());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        GridPos treasure = joker.TreasureCell.Value;
        LayCube(session, treasure);
        Check(joker.TreasureCell.HasValue, "laying a cube on the treasure finds nothing");

        Check(session.Powers.TryUse(cross.InstanceId, ActivationTarget.Board(treasure)),
            "the plus is set off on the treasure");
        Check(!session.CurrentRound.Board.GetCube(treasure).HasValue, "its cube is gone");
        Check(!joker.TreasureCell.HasValue && !joker.DynamiteCell.HasValue,
            "and the treasure was found there and then - it used to stay buried");
        Check(joker.LastFind != null && joker.LastFind.Result == HazineResult.TreasureOnly
                && joker.LastFind.Discoveries.Count == 1
                && joker.LastFind.Discoveries[0].Cell.Equals(treasure),
            "reported for the View at once, naming only the treasure");
        Check(joker.LastFind != null && joker.LastFind.Effect != HazineEffect.ExplosionBonus,
            "with no explosion bonus, since no line went off",
            joker.LastFind == null ? "null" : "" + joker.LastFind.Effect);
    }

    private static void Hazine_LateDestructionInTheTurnCountsAndBothCancel()
    {
        Section("hazine / end-of-turn destruction to its right counts");
        var session = NewSession(523, 6, 1000000, 40, 1);
        var joker = (HazineJoker)session.Jokers.Add(new HazineJoker());
        var wrecker = (LateWreckerJoker)session.Jokers.Add(new LateWreckerJoker()); // to the RIGHT
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;
        GridPos treasure = joker.TreasureCell.Value;
        GridPos dynamite = joker.DynamiteCell.Value;
        LayCube(session, treasure);
        LayCube(session, dynamite);
        int handBefore = round.Hand.Count;
        double discountBefore = session.PendingMarketDiscount;

        wrecker.Cells.Add(treasure);
        wrecker.Cells.Add(dynamite);
        TurnReport report = LayCube(session, FreeCell(round, joker));
        Check(!round.Board.GetCube(treasure).HasValue && !round.Board.GetCube(dynamite).HasValue,
            "the wrecker took both cubes at the end of the turn");
        Check(!joker.TreasureCell.HasValue && !joker.DynamiteCell.HasValue,
            "and both marks were blown open - it used to depend on inventory order");
        HazineVisuals find = joker.LastFind;
        Check(find != null && find.Result == HazineResult.BothCancelled
                && find.Effect == HazineEffect.None && find.ScoreDelta == 0,
            "one settle took both, so they cancel",
            find == null ? "null" : find.Result + " " + find.Effect);
        Check(find != null && find.Find(true) != null && find.Find(true).Cell.Equals(treasure)
                && find.Find(false) != null && find.Find(false).Cell.Equals(dynamite),
            "with both marks where they really were");
        Check(find != null && find.Find(true).Cube.SourceCardId > 0
                && find.Find(false).Cube.SourceCardId > 0,
            "and the cube each one was under");
        Check(round.Hand.Count == handBefore && session.PendingMarketDiscount == discountBefore,
            "nothing was paid and nothing was taken");
        Check(!string.IsNullOrEmpty(joker.LastOutcome), "the cancellation was reported");
    }

    private static void Hazine_AFindOnTheCrossingTurnIsStillShown()
    {
        Section("hazine / a find on the threshold-crossing turn");
        var session = NewSession(509, 6, 1, 40, 1);
        var joker = (HazineJoker)session.Jokers.Add(new HazineJoker());
        session.Jokers.DispatchRoundStarted(session.CurrentRound);
        RoundEngine round = session.CurrentRound;
        GridPos treasure = joker.TreasureCell.Value;
        BlowUpCell(session, treasure);
        Check(round.ThresholdPassed, "the turn crossed the threshold");
        Check(joker.TreasureCell.HasValue && joker.DynamiteCell.HasValue,
            "overtime buried a fresh pair");
        Check(joker.LastFind != null && joker.LastFind.Result == HazineResult.TreasureOnly
                && joker.LastFind.Discoveries[0].Cell.Equals(treasure),
            "and the find it paid is still there for the View - it used to be wiped");
        Check(!string.IsNullOrEmpty(joker.LastOutcome), "so is its outcome text");

        HazineVisuals shown = joker.LastFind;
        session.Jokers.DispatchRoundStarted(round);
        Check(joker.LastFind == null, "a NEW round does forget it");
        Check(shown != null, "(the object itself is untouched)");
    }

    private static void Hazine_TheExplosionBonusIsHalfWhatTheLineBanked()
    {
        Section("hazine / the explosion bonus is half of what the line banked");
        int checkedOvertime = 0;
        int checkedNormal = 0;
        for (int seed = 0; seed < 120 && (checkedOvertime == 0 || checkedNormal == 0); seed++)
        {
            bool overtime = checkedOvertime == 0;
            var session = NewSession(7000 + seed, 6, overtime ? 1 : 1000000, 40, 1);
            var joker = (HazineJoker)session.Jokers.Add(new HazineJoker());
            session.Jokers.DispatchRoundStarted(session.CurrentRound);
            RoundEngine round = session.CurrentRound;
            if (joker.TreasureCell.Value.Y == 5 || joker.DynamiteCell.Value.Y == 5)
            {
                continue;
            }
            if (overtime)
            {
                // cross the bar with a line on row 5, then play on
                for (int x = 0; x < 5; x++)
                {
                    PaintBoard(round, session, CubeKind.Normal, new GridPos(x, 5));
                }
                PaintBoard(round, session, CubeKind.Normal, new GridPos(0, 4));
                LayCube(session, new GridPos(5, 5));
                if (round.Status != RoundStatus.AwaitingAdvanceDecision)
                {
                    continue;
                }
                round.DecideAdvance(false);
                if (!joker.TreasureCell.HasValue)
                {
                    continue;
                }
            }
            GridPos t = joker.TreasureCell.Value;
            GridPos d = joker.DynamiteCell.Value;
            var row = new List<GridPos>();
            for (int x = 0; x < 6; x++)
            {
                var p = new GridPos(x, t.Y);
                if (!p.Equals(t) && !p.Equals(d))
                {
                    row.Add(p);
                }
            }
            if (row.Count != 5)
            {
                continue;
            }
            foreach (GridPos p in row)
            {
                if (!round.Board.GetCube(p).HasValue)
                {
                    PaintBoard(round, session, CubeKind.Normal, p);
                }
            }
            var spare = new GridPos(t.X == 0 ? 1 : 0, t.Y == 0 ? 1 : 0);
            if (!spare.Equals(d) && !round.Board.GetCube(spare).HasValue)
            {
                PaintBoard(round, session, CubeKind.Normal, spare);
            }
            TurnReport report = LayCube(session, t);
            HazineVisuals find = joker.LastFind;
            if (find == null || find.Effect != HazineEffect.ExplosionBonus)
            {
                continue;
            }
            ScoreBreakdown score = report.Score;
            int scale = session.Config.Scoring.ScoreScale;
            int expected = Math.Max(1, (int)Math.Round(score.BaseLines * score.RegularScoreFactor
                * joker.TreasureScoreBonus)) * scale;
            Check(find.ScoreDelta == expected,
                (overtime ? "overtime" : "normal play") + ": the bonus is half the line's banked value",
                "seed " + seed + " lines " + score.BaseLines + " factor " + score.RegularScoreFactor
                    + " paid " + find.ScoreDelta + " expected " + expected);
            if (overtime)
            {
                Check(score.RegularScoreFactor < 1.0, "(this one really was taxed)");
                checkedOvertime++;
            }
            else
            {
                checkedNormal++;
            }
        }
        Check(checkedOvertime > 0 && checkedNormal > 0, "both cases were reached",
            "overtime " + checkedOvertime + " normal " + checkedNormal);
    }
}
