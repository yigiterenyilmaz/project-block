// PURPOSE: RoundEngine.ResolvePlacement - the ordered turn resolver. KEEP THE ORDER
// STABLE: place+score, explode lines, clean sweep, gold upkeep, finalize score,
// card disposition, hand refill, end-of-turn hooks, threshold + status.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
        private TurnReport ResolvePlacement(BlockCard card, GridPos origin, bool fromBonus,
            BonusPlayOutcome bonusOutcome)
        {
            TurnNumber++;
            int shufflesBeforeTurn = Deck.ShuffleCount;

            // Remember the board as it stands BEFORE this placement, so "Kum saati" can
            // rewind into it later. Oldest entries fall off the front.
            var turnStartBoard = new Dictionary<GridPos, Cube>();
            Board.SnapshotInto(turnStartBoard);
            boardHistory.Add(turnStartBoard);
            if (boardHistory.Count > BoardHistoryDepth)
            {
                boardHistory.RemoveAt(0);
            }

            breakdown.Reset();
            breakdown.ScoreScale = scorer.ScoreScale; // whole-economy x scale, applied to Total
            var report = new TurnReport();
            report.TurnNumber = TurnNumber;
            report.Card = card;
            report.PlayedFromBonusHand = fromBonus;
            report.Origin = origin;
            report.Score = breakdown;

            currentReport = report;
            currentTurn = new TurnContext(session, rng, this, report, breakdown);
            scoreFinalized = false;
            sweepResolvedThisTurn = false;
            cubesDestroyedThisTurn = 0;
            pendingAdvanceOffer = false;
            externalClearReady = false;
            destroyedThisTurn.Clear();
            cardsFullyDestroyedThisTurn.Clear();
            report.DestroyedCubes = destroyedThisTurn;
            report.CardsFullyDestroyed = cardsFullyDestroyedThisTurn;

            // 1. place + score
            report.PlacedCells = Board.Place(card, EffectiveShape(card), origin,
                card.Has(BlockElement.Ghost));
            if (card.Has(BlockElement.Dynamite))
            {
                var state = new DynamiteState();
                state.FullSize = report.PlacedCells.Count;
                state.RemainingAtTurnStart = report.PlacedCells.Count;
                state.PlacementTurn = TurnNumber;
                dynamiteBlocks[card.Id] = state;
            }
            breakdown.BasePlacement = scorer.ScorePlacement(report.PlacedCells.Count);
            if (Rules.RetroMode)
            {
                // retro pays a flat bonus for every placement (ScoringConfig.RetroPlacementBonus)
                breakdown.BasePlacement += scorer.RetroPlacementBonus;
            }
            var waterFrames = new List<IReadOnlyList<WaterMove>>();

            cardPlacedSize[card.Id] = report.PlacedCells.Count;

            // 2. explode full lines + score (fire chains resolve inside the board).
            // WATER RULE (confirmed 2026-07-19): a freshly placed water block that completes a
            // line explodes IN PLACE, before it would drop into any empty space beneath it.
            // Only when the placement triggers no explosion does the water settle and we
            // re-check the lines it may complete after falling.
            // boardCleanBeforeExplosion is sampled right before the destruction we score, so a
            // sweep still sees the pre-explosion board. Water only moves cubes (a fall never
            // changes the clean check), but a fire->obsidian douse can, hence the resample.
            // The destruction snapshot baselines here, before the first explosion attempt,
            // and is resynced after every settle - moved water must not read as destroyed.
            boardCleanBeforeExplosion = Board.IsCleanForSweep();
            ResyncSnapshot();
            CaptureTurnStartCardCounts();
            LineExplosionResult explosion = Board.ResolveFullLines(Rules.RetroMode);
            if (explosion.LineCount == 0)
            {
                Board.SettleWaterAndReact(waterFrames); // nothing exploded in place -> water falls
                ResyncSnapshot(); // water moved, nothing died - re-baseline the destruction diff
                boardCleanBeforeExplosion = Board.IsCleanForSweep();
                explosion = Board.ResolveFullLines(Rules.RetroMode);
            }
            // Frames appended after this point are post-explosion falls; the UI plays the
            // boom between the two batches.
            report.WaterFramesBeforeExplosion = waterFrames.Count;
            report.ExplodedRows = explosion.Rows;
            report.ExplodedColumns = explosion.Columns;
            int cubesExploded = explosion.ExplodedCells.Count;

            // DYNAMITE RULE (confirmed 2026-07-18): any dynamite block that was intact at
            // turn start and got fully exploded in one shot clears the entire board.
            if (explosion.LineCount > 0 && dynamiteBlocks.Count > 0)
            {
                bool boom = false;
                var trackedIds = new List<int>(dynamiteBlocks.Keys);
                foreach (int id in trackedIds)
                {
                    DynamiteState state = dynamiteBlocks[id];
                    int remaining = Board.CountCubesOf(id);
                    if (remaining == 0)
                    {
                        dynamiteBlocks.Remove(id);
                        // Only the block placed THIS turn detonates the board (confirmed):
                        // a still-whole block that lingers to a later turn just explodes.
                        if (state.RemainingAtTurnStart == state.FullSize
                            && state.PlacementTurn == TurnNumber)
                        {
                            boom = true;
                        }
                    }
                    else
                    {
                        state.RemainingAtTurnStart = remaining;
                    }
                }
                if (boom)
                {
                    cubesExploded += Board.DestroyAllDestructible().Count;
                    report.DynamiteTriggered = true;
                    // blocks wiped by the clear must not delayed-trigger next turn
                    foreach (int id in new List<int>(dynamiteBlocks.Keys))
                    {
                        if (Board.CountCubesOf(id) == 0)
                        {
                            dynamiteBlocks.Remove(id);
                        }
                    }
                }
            }
            report.CubesExploded = cubesExploded;
            cubesDestroyedThisTurn += cubesExploded;
            // Logged BEFORE the post-explosion settle: settling MOVES water, and a moved
            // cube would otherwise look like a destroyed one to the snapshot diff.
            LogDestruction();
            if (explosion.LineCount > 0)
            {
                breakdown.BaseLines = ScoreLineExplosionScored(explosion, cubesExploded);
                Board.SettleWaterAndReact(waterFrames); // explosions pull the floor out from water
                ResyncSnapshot();
            }
            report.WaterFallFrames = waterFrames;
            hooks.AfterLineExplosion(currentTurn);

            // Retro gravity: classic Tetris. A locked block stays exactly where it landed; when
            // full rows clear, only the rows ABOVE them drop straight down (a block never falls
            // into a gap beneath it). A row-collapse cannot complete a new line, so there is no
            // cascade and no extra scoring - the turn's line score is the single clear above.
            if (Rules.RetroMode && explosion.LineCount > 0)
            {
                CollapseRetroLines(explosion.Rows);
            }

            // COMBO ("kombo"): consecutive line-clearing turns stack a growing bonus. A turn
            // that explodes >=1 row/column continues the streak (1,2,3...) and pays
            // comboCount*step; a turn that clears no line resets it. RedrawHand never reaches
            // here, so a redraw does not break the streak. BaseCombo is a regular base field,
            // so overtime trickles it like the rest of the regular score.
            if (report.ExplodedRows.Count + report.ExplodedColumns.Count > 0)
            {
                comboCount++;
                breakdown.BaseCombo = scorer.ScoreCombo(comboCount);
            }
            else
            {
                comboCount = 0;
            }
            report.ComboCount = comboCount;

            // 3. clean sweep (single central event - see the file header). This is the player's
            // OWN placement clear, so it always counts (pays bonus + recharges) - unlike the
            // joker/power-triggered sweeps, which route through TryResolveCleanSweep.
            ResolvePlacementSweep();

            // 4. element upkeep: gold pays while it sits on the board
            int goldCubes = Board.CountCubesOfKind(CubeKind.Gold);
            if (goldCubes > 0)
            {
                report.GoldBonus = scorer.ScoreGoldBonus(goldCubes);
                breakdown.BaseGold = report.GoldBonus;
            }

            // 5. finalize the score. In overtime the regular base (placement/lines/sweep/gold)
            //    is taxed down to a trickle; the overtime win bonus and joker contributions are
            //    exempt. ThresholdPassed is sampled live here, so the turn that crosses the
            //    threshold still scores in full - only later overtime turns are trickled.
            breakdown.RegularScoreFactor = ThresholdPassed ? scorer.OvertimeRegularScoreFactor : 1.0;
            hooks.ModifyScore(currentTurn);
            scoreFinalized = true;
            RoundScore += breakdown.Total;
            report.ScoreGained = breakdown.Total;
            report.RoundScoreAfter = RoundScore;

            // 6. card disposition
            if (fromBonus)
            {
                if (bonusOutcome == BonusPlayOutcome.ToDiscard)
                {
                    DisposeCard(card);
                }
                else
                {
                    // Expires from the round: it joins no pile, so the UI vanishes it.
                    Deck.RemoveFromRound(card);
                    report.PlayedCardExpired = true;
                }
                // Burn: the next available card is flipped face-up into the discard.
                // "Next available" follows the normal draw rules (confirmed design):
                // before the threshold an empty draw pile recycles the discard first;
                // in overtime an empty pile on any draw attempt is a loss.
                report.BurnedCard = DrawWithRules();
                if (report.BurnedCard != null)
                {
                    DisposeCard(report.BurnedCard);
                }
                // Bonus plays do not refill the hand - the hand was not touched.
            }
            else
            {
                DisposeCard(card);
                // 7. refill - unless a joker manages the hand itself ("İmitasyon" refills in
                // AfterTurnScored, so topping up here would just draw a card it discards).
                if (!Rules.SkipStandardRefill)
                {
                    RefillHand();
                }
            }

            // 8. end-of-turn effects (may still add score - see step 9)
            hooks.AfterTurnScored(currentTurn);
            if (session != null)
            {
                session.Powers.DispatchAfterTurnScored(currentTurn);
            }

            // 9. threshold check (first pass only)
            if (!ThresholdPassed && RoundScore >= ScaledThreshold)
            {
                ThresholdPassed = true;
                report.ThresholdJustPassed = true;
                Deck.ShuffleDiscardIntoDraw();
                pendingAdvanceOffer = true;
                EnterOvertime();
            }

            // Retro top-out: a block reached the top row, so nothing can drop from above (Tetris).
            // Sampled here so it obeys the same "advance offer outranks the loss" ordering below.
            if (Loss == null && Rules.DeadZoneRows > 0 && IsToppedOut())
            {
                Loss = LossReason.RetroTopOut;
            }

            // 10./11. status update - see file header for why the offer outranks the loss.
            if (pendingAdvanceOffer)
            {
                SetStatus(RoundStatus.AwaitingAdvanceDecision);
            }
            else if (Loss != null)
            {
                SetStatus(RoundStatus.Lost);
            }
            else
            {
                CheckForNoPlayableMove();
            }
            report.StatusAfter = Status;
            report.DiscardWasReshuffled = Deck.ShuffleCount != shufflesBeforeTurn;

            // A new turn begins: its single power slot is free again.
            PowersUsedThisTurn = 0;
            currentReport = null;
            currentTurn = null;

            if (TurnResolved != null)
            {
                TurnResolved(report);
            }
            return report;
        }
    }
}
