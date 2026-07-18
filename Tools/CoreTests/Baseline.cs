// Deterministic scripted playthrough used to prove the joker refactor did not change
// base-game behaviour. Compiles against BOTH the pre-joker Core and the new one, so it may
// only touch API that exists in both.

using System;
using System.Text;
using ProjectBlock.Core;

public static class Baseline
{
    public static string RunAll()
    {
        var sb = new StringBuilder();
        for (int seed = 1; seed <= 12; seed++)
        {
            sb.Append(RunGame(seed, alwaysAdvance: true));
            sb.Append(RunGame(seed, alwaysAdvance: false));
        }
        return sb.ToString();
    }

    private static string RunGame(int seed, bool alwaysAdvance)
    {
        var sb = new StringBuilder();
        sb.Append("=== seed=").Append(seed).Append(" advance=").Append(alwaysAdvance).Append('\n');

        var config = new GameConfig();
        config.RngSeed = seed;
        var session = new GameSession(config);

        int safety = 0;
        while (session.Phase != GamePhase.GameOver && safety++ < 2000)
        {
            if (session.Phase == GamePhase.Market)
            {
                sb.Append("market round=").Append(session.RoundNumber)
                  .Append(" total=").Append(session.TotalScore).Append('\n');
                if (session.RoundNumber >= 6)
                {
                    break;
                }
                session.LeaveMarket();
                continue;
            }

            RoundEngine round = session.CurrentRound;
            if (round.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                sb.Append("offer turn=").Append(round.TurnNumber)
                  .Append(" score=").Append(round.RoundScore).Append('\n');
                round.DecideAdvance(alwaysAdvance);
                continue;
            }
            if (round.Status != RoundStatus.InProgress)
            {
                break;
            }

            // First hand card with a legal origin, first legal origin. Fully deterministic.
            int handIndex = -1;
            GridPos origin = new GridPos(0, 0);
            for (int i = 0; i < round.Hand.Count && handIndex < 0; i++)
            {
                var origins = round.GetValidOrigins(round.Hand[i].Shape);
                if (origins.Count > 0)
                {
                    handIndex = i;
                    origin = origins[0];
                }
            }
            if (handIndex < 0)
            {
                sb.Append("stuck\n");
                break;
            }

            TurnReport report = round.PlayFromHand(handIndex, origin);
            sb.Append("t").Append(report.TurnNumber)
              .Append(" card=").Append(report.Card.Id).Append('/').Append(report.Card.Shape.Size)
              .Append(" at=").Append(report.Origin.X).Append(',').Append(report.Origin.Y)
              .Append(" cells=").Append(report.PlacedCells.Count)
              .Append(" rows=").Append(report.ExplodedRows.Count)
              .Append(" cols=").Append(report.ExplodedColumns.Count)
              .Append(" boom=").Append(report.CubesExploded)
              .Append(" sweep=").Append(report.CleanSweep ? 1 : 0)
              .Append(" gain=").Append(report.ScoreGained)
              .Append(" round=").Append(report.RoundScoreAfter)
              .Append(" thr=").Append(report.ThresholdJustPassed ? 1 : 0)
              .Append(" shuf=").Append(report.DiscardWasReshuffled ? 1 : 0)
              .Append(" burn=").Append(report.BurnedCard == null ? 0 : 1)
              .Append(" draw=").Append(round.Deck.DrawCount)
              .Append(" disc=").Append(round.Deck.DiscardCount)
              .Append(" hand=").Append(round.Hand.Count)
              .Append(" occ=").Append(round.Board.OccupiedCount)
              .Append(" st=").Append(report.StatusAfter)
              .Append('\n');
        }

        sb.Append("end phase=").Append(session.Phase)
          .Append(" round=").Append(session.RoundNumber)
          .Append(" total=").Append(session.TotalScore);
        if (session.CurrentRound != null && session.CurrentRound.Loss.HasValue)
        {
            sb.Append(" loss=").Append(session.CurrentRound.Loss.Value);
        }
        sb.Append('\n');
        return sb.ToString();
    }
}
