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
            // Where the round score stood before this turn. A turn may end up worth NOTHING
            // (an inverted joker, a starving creature's bill) but it may never push the round
            // score backwards - see ClampTurnScoreFloor, applied at step 8.6 and again after any
            // late write, because some of them land after that (the dead-end check).
            turnStartRoundScore = RoundScore;

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
            cleanSampleLocked = false;
            destroyedThisTurn.Clear();
            cardsFullyDestroyedThisTurn.Clear();
            report.DestroyedCubes = destroyedThisTurn;
            report.CardsFullyDestroyed = cardsFullyDestroyedThisTurn;

            // 1. place + score. A NEGATIVE block places nothing - it erases what it covers
            // and goes with it - so it is resolved after the placement score below, which
            // would otherwise overwrite what the erasure earns.
            //
            // card is NULL only in one case: "Öteki dünya" is open, the main world has nowhere
            // left to put anything, and the mirror world is playing this turn alone. Everything
            // that belongs to the main card is skipped; the rest of the turn runs unchanged.
            bool mainWorldSitsOut = card == null;
            // A DEFECTIVE SMUGGLED card ("Kaçakçı") will not stay on the board: it drops through
            // the arena and out of the frame. Checked before anything else touches the board, so
            // NOTHING lands - no cubes, no element, no dynamite to track, no line to complete.
            // The cells it passed through are recorded for the UI to animate the fall, and for
            // nothing else.
            bool fallsThrough = !mainWorldSitsOut && card.FallsThrough;
            if (fallsThrough)
            {
                report.FellThroughCells = CellsCovered(EffectiveShape(card), origin);
            }
            // "Antimadde": the card is a key, not a block. CanPlaceCard has already guaranteed it
            // covers nothing but cubes of its own kind, so reaching here IS the perfect fit - every
            // cube of that kind on the board is annihilated and nothing is placed. Through
            // DestroyCubes, so the destruction log, the sweep pre-condition and the tally all stay
            // correct; the joker that minted the card pays the bonus from AfterTurnScored.
            bool antimatter = !mainWorldSitsOut && !fallsThrough && card.AntimatterOf.HasValue;
            if (antimatter)
            {
                report.AnnihilatedKind = card.AntimatterOf;
                var doomed = Board.CellsOfKind(card.AntimatterOf.Value);
                if (doomed.Count > 0)
                {
                    report.AddExtraExplodedCells(DestroyCubes(doomed, true));
                }
            }
            bool negative = !mainWorldSitsOut && !fallsThrough && !antimatter
                && Has(card, BlockElement.Negative);
            if (!mainWorldSitsOut && !fallsThrough && !antimatter && !negative)
            {
                report.PlacedCells = Board.Place(card, EffectiveShape(card), origin,
                    Has(card, BlockElement.Ghost));
                // "Hedefli": the block's one shot is live from the moment it lands until the
                // first of its cubes breaks (see RoundEngine.Targeted).
                ArmTargetedBlock(card);
            }
            if (!mainWorldSitsOut && !fallsThrough && !antimatter
                && Has(card, BlockElement.Dynamite))
            {
                var state = new DynamiteState();
                state.FullSize = report.PlacedCells.Count;
                state.RemainingAtTurnStart = report.PlacedCells.Count;
                state.PlacementTurn = TurnNumber;
                dynamiteBlocks[card.Id] = state;
            }
            // 1b. the mirror world's half of the turn lands with the main one, so both worlds'
            //     placements are in before anything explodes.
            ApplyStagedMirrorPlacement(report);
            breakdown.BasePlacement = scorer.ScorePlacement(
                report.PlacedCells.Count + report.MirrorPlacedCells.Count);
            if (Rules.RetroMode)
            {
                // retro pays a flat bonus for every placement (ScoringConfig.RetroPlacementBonus)
                breakdown.BasePlacement += scorer.RetroPlacementBonus;
            }
            if (negative && !mainWorldSitsOut)
            {
                ResolveNegativePlacement(card, origin, report, breakdown);
            }
            var waterFrames = new List<IReadOnlyList<WaterMove>>();

            if (!mainWorldSitsOut)
            {
                cardPlacedSize[card.Id] = report.PlacedCells.Count;
            }

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
            if (!cleanSampleLocked)
            {
                boardCleanBeforeExplosion = Board.IsCleanForSweep();
                ResyncSnapshot();
                CaptureTurnStartCardCounts();
            }
            // "Bilinmezlik": a full line simply does not go off. Nothing is cleared, nothing is
            // scored, and the line sits there full - which is what makes the board fill up.
            LineExplosionResult explosion = LineExplosionsSuppressed
                ? LineExplosionResult.None
                : Board.ResolveFullLines(Rules.RetroMode);
            if (explosion.LineCount == 0 && !LineExplosionsSuppressed)
            {
                Board.SettleWaterAndReact(waterFrames); // nothing exploded in place -> water falls
                ResyncSnapshot(); // water moved, nothing died - re-baseline the destruction diff
                if (!cleanSampleLocked)
                {
                    boardCleanBeforeExplosion = Board.IsCleanForSweep();
                }
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
                breakdown.BaseLines = ScoreLineExplosionScored(explosion, cubesExploded)
                    // "Karantina" charges for the cubes that stood in its zones.
                    + AdjustExplosionScore(explosion.ExplodedCells);
                Board.SettleWaterAndReact(waterFrames); // explosions pull the floor out from water
                ResyncSnapshot();
            }
            report.WaterFallFrames = waterFrames;

            // 2.5 the SECOND world ("Öteki dünya") clears its own lines, sweeps for itself and
            //     pays the cross-world column bonus. Here, right after the main world's own
            //     explosion, so both clears belong to this turn and a matching column can be
            //     seen at all. A no-op on every ordinary round.
            ResolveMirrorExplosions(report);

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
            //
            // "Mikrodalga" bends the RESET, not the streak: while Rules.ComboBridgeTurns allows
            // it, a quiet turn is merely counted instead of ending the run, and the clear that
            // comes after it picks the streak up where it left off. What crossing a gap costs is
            // that one turn's bonus (Rules.ComboBridgedScorePercent) - the reheated combo is
            // worth less than the one that never went cold. Both rules read live and both are
            // inert in the base game, so an ordinary run behaves exactly as it always has.
            if (report.ExplodedRows.Count + report.ExplodedColumns.Count > 0)
            {
                comboCount++;
                int comboBonus = scorer.ScoreCombo(comboCount);
                if (comboBlankTurns > 0)
                {
                    comboBonus = comboBonus * Rules.ComboBridgedScorePercent / 100;
                }
                comboBlankTurns = 0;
                breakdown.BaseCombo = comboBonus;
            }
            else if (comboCount > 0 && comboBlankTurns < Rules.ComboBridgeTurns)
            {
                comboBlankTurns++; // the streak is only sleeping - see above
            }
            else
            {
                comboCount = 0;
                comboBlankTurns = 0;
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

            // 4.5 "Titizlik": only a clean sweep pays, so everything else the turn earned is
            //     wiped. Here rather than at each assignment, so it is one rule in one place -
            //     and after the sweep has had its chance to fire in step 3.
            if (Boss != null && Boss.OnlyCleanSweepsScore)
            {
                breakdown.KeepOnlyCleanSweep();
            }
            // 4.6 "Bürokrasi bataklığı" goes further: nothing at all scores by itself, and the
            //     boss pays the player from its own hooks instead. Same place, same rule shape.
            if (BaseScoreSuppressed)
            {
                breakdown.KeepNoBaseScore();
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
                // Bonus plays do not refill the hand - the hand was not touched. Unless the
                // boss says otherwise: "Feda" makes the whole hand the price of a bonus card.
                if (Boss != null)
                {
                    Boss.OnBonusCardPlayed(currentTurn);
                }
            }
            else if (!mainWorldSitsOut)
            {
                DisposeCard(card);
                // 7. refill - unless a joker manages the hand itself ("İmitasyon" refills in
                // AfterTurnScored, so topping up here would just draw a card it discards).
                if (!Rules.SkipStandardRefill)
                {
                    RefillHand();
                }
            }
            // The mirror tops up from the SAME piles. A short mirror hand is never a loss - a
            // world with nothing playable simply sits out the next turn.
            RefillMirrorHand();

            TickFreezes(); // a frozen card thaws after the agreed number of resolved turns

            // 8. end-of-turn effects (may still add score - see step 9)
            hooks.AfterTurnScored(currentTurn);
            if (session != null)
            {
                session.Powers.DispatchAfterTurnScored(currentTurn);
            }
            // The boss harasses last, after the player's own end-of-turn effects have run, but
            // still BEFORE the threshold and dead-end checks - so what it does this turn can
            // decide the round (a sealed cell or a frozen card may be the last straw).
            if (Boss != null)
            {
                Boss.AfterTurnScored(currentTurn);
            }

            // 8.5 the arena erodes if the draw pile has run dry too often. After the hooks, so a
            // joker that refilled the hand in step 8 is counted too; before the threshold and
            // dead-end checks below, so a line the squeeze completes still scores this turn and
            // an erosion that leaves nowhere to play can genuinely end the round.
            ApplyPendingBoardErosion();

            // 8.6 A TURN IS NEVER WORTH LESS THAN NOTHING. Negative score is real - "Terslik"
            //     inverts every joker, "Besleme" bills you for a starving creature - but it can
            //     only ever eat what this turn earned, never bite into the round. The floor on
            //     ScoreBreakdown.Total covers the score that is settled before finalization;
            //     this covers everything added AFTER it, which lands on RoundScore directly.
            ClampTurnScoreFloor();

            // 9. threshold check (first pass only)
            if (!ThresholdPassed && RoundScore >= ScaledThreshold)
            {
                // "Çıkmaz": the bar is a trap, not a goal. Reaching it ends the round as a
                // loss, so there is no overtime and no advance offer to make.
                if (RoundOutcomeInverted)
                {
                    Loss = LossReason.ForbiddenThreshold;
                }
                else
                {
                    // The bar is a CEILING for normal play: this turn takes you to it and no
                    // further. Everything past it has to be earned in overtime.
                    CapScoreAtThresholdOnCrossing();
                    ThresholdPassed = true;
                    report.ThresholdJustPassed = true;
                    Deck.ShuffleDiscardIntoDraw();
                    pendingAdvanceOffer = true;
                    EnterOvertime();
                }
            }

            // Retro top-out: a block reached the top row, so nothing can drop from above (Tetris).
            // Sampled here so it obeys the same "advance offer outranks the loss" ordering below.
            if (Loss == null && Rules.DeadZoneRows > 0 && IsToppedOut())
            {
                Loss = LossReason.RetroTopOut;
            }

            // 10./11. status update - see file header for why the offer outranks the loss.
            // A boss beaten on its own terms comes FIRST: it already banked the threshold and
            // set ThresholdPassed, so there is no offer to make and no overtime to enter, and
            // DeclareRoundWon refused to fire at all if the same turn had lost the round.
            if (bossWonTheRound)
            {
                SetStatus(RoundStatus.Advanced);
            }
            else if (pendingAdvanceOffer)
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

        /// <summary>
        /// Resolves a NEGATIVE block: it erases the cubes under its shape and leaves nothing
        /// behind - both the block and what it deleted are gone, so the cells end up empty.
        ///
        /// The erased cubes go through DestroyCubes like any other destruction, which is what
        /// keeps the destruction log, the "Kayıt defteri" tally and the clean-sweep
        /// pre-condition correct. They pay the normal per-cube explosion score.
        ///
        /// Indestructible cubes cannot be here at all: CanPlace refuses to let a negative
        /// block land on obsidian or gold in the first place.
        /// </summary>
        private void ResolveNegativePlacement(BlockCard card, GridPos origin, TurnReport report,
            ScoreBreakdown score)
        {
            // Sample the sweep pre-condition BEFORE erasing: for a normal card the placement
            // is what makes the board "not clean", but here the erasure IS the placement, so
            // the sample has to happen first or a board this block empties could never sweep.
            // The lock stops the normal explosion path from re-sampling it afterwards.
            boardCleanBeforeExplosion = Board.IsCleanForSweep();
            ResyncSnapshot();
            CaptureTurnStartCardCounts();
            cleanSampleLocked = true;

            BlockShape shape = EffectiveShape(card);
            var targets = new List<GridPos>();
            foreach (GridPos offset in shape.Cells)
            {
                GridPos pos = origin + offset;
                if (Board.IsInside(pos) && Board.GetCube(pos).HasValue)
                {
                    targets.Add(pos);
                }
            }
            if (targets.Count == 0)
            {
                return; // dropped on empty space: legal, but there was nothing to erase
            }
            IReadOnlyList<GridPos> erased = DestroyCubes(targets, true);
            if (erased.Count == 0)
            {
                return;
            }
            // Scored as an explosion of its own: no lines were completed, so the line count
            // is zero and only the per-cube value applies.
            score.BasePlacement += scorer.ScoreLineExplosion(0, erased.Count);
            report.AddExtraExplodedCells(erased);
        }
    }
}
