// PURPOSE: GameUiController post-placement feedback - finalizing a placement, explosion
// and blast FX, camera shake, full refresh, infection marks, the HUD, and turn logs.

using System.Collections;
using System.Collections.Generic;
using System.Text;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>Everything the view does AFTER a card is played, shared by the drag path and
        /// the retro falling-piece path: logging, sounds, a full refresh, then the water/explosion
        /// feedback (water falls first, boom next, post-explosion falls last) and the sweeper.</summary>
        private void FinalizePlacement(RoundEngine round, TurnReport report)
        {
            if (verboseTurnLogs)
            {
                LogTurn(report);
            }
            sfx.Place();
            if (report.PlayedCardExpired)
            {
                sfx.Vanish();
            }
            if (report.DiscardWasReshuffled)
            {
                sfx.Shuffle();
            }
            RefreshAll(report);
            IReadOnlyList<IReadOnlyList<WaterMove>> frames = report.WaterFallFrames;
            if (frames.Count == 0)
            {
                PlayExplosionFeedback(round, report);
            }
            else
            {
                // Water falls first, THEN the boom it caused - and any post-explosion
                // falls play after the boom (WaterFramesBeforeExplosion splits them).
                int boomAt = Mathf.Clamp(report.WaterFramesBeforeExplosion, 0, frames.Count);
                var preFall = new List<IReadOnlyList<WaterMove>>();
                var postFall = new List<IReadOnlyList<WaterMove>>();
                for (int f = 0; f < frames.Count; f++)
                {
                    (f < boomAt ? preFall : postFall).Add(frames[f]);
                }
                waterAnimating = true;
                boardView.PlayWaterAnimation(preFall, delegate
                {
                    PlayExplosionFeedback(round, report);
                    boardView.PlayWaterAnimation(postFall,
                        delegate { waterAnimating = false; });
                });
            }
            TriggerSupurgeBlast();
        }

        /// <summary>Explosion sound + blast feedback for one turn. Deferred until after the
        /// pre-explosion water falls when the flow is what completed the line.</summary>
        private void PlayExplosionFeedback(RoundEngine round, TurnReport report)
        {
            if (report.CleanSweep)
            {
                // the sweep bling rises in pitch with every sweep this round
                sfx.CleanSweep(1f + 0.12f * Mathf.Min(round.CleanSweepCount - 1, 8));
                sfx.Flame();
            }
            else if (report.CubesExploded > 0 || report.ExtraExplodedCells.Count > 0)
            {
                // ExtraExplodedCells covers a late board-reshape clear (inflation deflate)
                // that the placement's own CubesExploded count never saw.
                sfx.Explode();
            }
            HandleBlastFeedback(round, report);
        }

        /// <summary>Particles, shake, combo popups and the sweep celebration for one turn.</summary>
        private void HandleBlastFeedback(RoundEngine round, TurnReport report)
        {
            if (report.CubesExploded == 0 && report.ExtraExplodedCells.Count == 0)
            {
                comboStreak = 0;
                return;
            }
            comboStreak++;
            EmitBlastParticles(round, report);
            // shake grows with the combo streak
            float shakeAmplitude = report.DynamiteTriggered ? 0.22f : report.CleanSweep ? 0.16f : 0.09f;
            shakeAmplitude *= 1f + 0.25f * Mathf.Min(comboStreak - 1, 5);
            ShakeCamera(shakeAmplitude, 0.2f);
            if (report.DynamiteTriggered)
            {
                FloatingTextFx.Spawn(transform, new Vector2(0f, 3.4f),
                    Loc.Pick("DYNAMITE!", "DİNAMİT!"), new Color(0.95f, 0.3f, 0.2f), 72, 0.09f);
            }
            // The popup shows the SCORING combo (consecutive line-clearing turns), which is
            // what actually pays out - not the destruction-only comboStreak that drives shake.
            if (report.ComboCount >= 2)
            {
                FloatingTextFx.Spawn(transform, new Vector2(0f, 2.6f),
                    Loc.Pick("COMBO x", "KOMBO x") + report.ComboCount + "!",
                    new Color(1f, 0.6f, 0.2f), 64, 0.08f);
            }
            if (report.CleanSweep)
            {
                FloatingTextFx.Spawn(transform, new Vector2(0f, 1.4f),
                    Loc.Pick("CLEAN SWEEP!", "TEMİZLİK!"), new Color(1f, 0.85f, 0.3f), 80, 0.1f);
            }
        }

        private void EmitBlastParticles(RoundEngine round, TurnReport report)
        {
            var blastColor = new Color(1f, 0.72f, 0.35f);
            foreach (int y in report.ExplodedRows)
            {
                for (int x = 0; x < round.Board.Width; x++)
                {
                    blastFx.EmitAt(boardView.CellToWorld(new GridPos(x, y)), blastColor, 4);
                }
            }
            foreach (int x in report.ExplodedColumns)
            {
                for (int y = 0; y < round.Board.Height; y++)
                {
                    blastFx.EmitAt(boardView.CellToWorld(new GridPos(x, y)), blastColor, 4);
                }
            }
            // Late board-reshape clears (inflation deflate, board powers) blast their exact
            // absolute cells - ExplodedRows/Columns never covered them. The board has already
            // been rebuilt to its new size by RefreshAll, so CellToWorld maps these correctly.
            foreach (GridPos cell in report.ExtraExplodedCells)
            {
                blastFx.EmitAt(boardView.CellToWorld(cell), blastColor, 4);
            }
            if (report.CleanSweep)
            {
                var gold = new Color(1f, 0.85f, 0.3f);
                for (int i = 0; i < 70; i++)
                {
                    var pos = new Vector2(Random.Range(-3.2f, 3.2f), Random.Range(-2.2f, 4f));
                    blastFx.EmitAt(pos, gold, 2);
                }
            }
        }

        /// <summary>Very small camera shake for explosions (slightly bigger on clean sweeps).</summary>
        private void ShakeCamera(float amplitude, float duration)
        {
            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                cam.transform.position = camBasePosition;
            }
            shakeRoutine = StartCoroutine(ShakeRoutine(amplitude, duration));
        }

        private IEnumerator ShakeRoutine(float amplitude, float duration)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float falloff = 1f - Mathf.Clamp01(time / duration);
                Vector2 offset = Random.insideUnitCircle * (amplitude * falloff);
                cam.transform.position = camBasePosition + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }
            cam.transform.position = camBasePosition;
            shakeRoutine = null;
        }

        /// <summary>The top revealed cards of the discard pile ("Fraksiyon" inspect),
        /// newest first.</summary>
        private static List<BlockCard> RevealedDiscardCards(RoundEngine round)
        {
            var list = new List<BlockCard>();
            IReadOnlyList<BlockCard> pile = round.Deck.DiscardPile;
            int n = Mathf.Min(round.Rules.RevealedDiscardCount, pile.Count);
            for (int i = 0; i < n; i++)
            {
                list.Add(pile[pile.Count - 1 - i]);
            }
            return list;
        }

        private static BlockCard CardOfSlot(RoundEngine round, int slot)
        {
            if (slot < 0)
            {
                return null;
            }
            if (slot < round.Hand.Count)
            {
                return round.Hand[slot];
            }
            int bonusIndex = slot - round.Hand.Count;
            return bonusIndex < round.BonusHand.Count ? round.BonusHand[bonusIndex].Card : null;
        }

        private void RefreshAll(TurnReport report)
        {
            RoundEngine round = session.CurrentRound;
            if (boardView.Board != round.Board)
            {
                boardView.Rebuild(round.Board, maxBoardWorldSize, BoardCenter);
            }
            boardView.Refresh();
            boardView.SetDeadZone(session.Config.Rules.DeadZoneRows);
            boardView.ClearPreview();
            RefreshInfections();
            cardLayer.Sync(round, report);
            flameStreak.SetState(round.ContinueCount, boardView.WorldRect);
            UpdateHud();
            jokerBar.Refresh(session, pendingTargetJokerId);
            powerBar.Refresh(session, pendingTargetPowerId);
            SyncRetroPresentation();
        }

        /// <summary>Keeps every retro-mode presentation layer in sync with RoundRules.RetroMode:
        /// the CRT overlay, the CRT hum + bit-crush audio, and the fullscreen edge-bend shader
        /// global. Called at round start and on every full refresh.</summary>
        private void SyncRetroPresentation()
        {
            bool on = session != null && session.Config.Rules.RetroMode;
            if (!on)
            {
                retroFallHand = -1; // no piece falls once retro is off (the board shrank back)
            }
            if (crt != null)
            {
                crt.SetVisible(on);
            }
            if (sfx != null)
            {
                sfx.SetRetro(on); // the CRT hum loop
            }
            if (bitCrush != null)
            {
                bitCrush.Active = on; // grit the whole mix
            }
            Shader.SetGlobalFloat(CrtBendId, on ? 1f : 0f);
        }

        /// <summary>Gathers "Enfeksiyon" infection markers from the inventory and hands them
        /// to the board view to draw (buildup pips + tint).</summary>
        private void RefreshInfections()
        {
            infectionBuffer.Clear();
            IReadOnlyList<Joker> jokers = session.Jokers.Jokers;
            for (int i = 0; i < jokers.Count; i++)
            {
                var enf = jokers[i] as EnfeksiyonJoker;
                if (enf != null)
                {
                    infectionBuffer.AddRange(enf.InfectedCells);
                }
            }
            boardView.ShowInfections(infectionBuffer);
        }

        private void UpdateHud()
        {
            RoundEngine round = session.CurrentRound;
            var sb = new StringBuilder();
            sb.Append("Seed ").Append(lastSeedUsed)
                .Append(Loc.Pick("   Deck: ", "   Deste: ")).Append(currentDeck.Name).Append('\n');
            sb.Append(Loc.Pick("Round ", "Raunt ")).Append(session.RoundNumber)
                .Append(Loc.Pick("   Turn ", "   Tur ")).Append(round.TurnNumber).Append('\n');
            // RoundScore lives in the scaled economy; lift the threshold to match for display.
            sb.Append(Loc.Pick("Score ", "Puan ")).Append(round.RoundScore)
                .Append(" / ").Append(round.Config.ScoreThreshold * session.Config.Scoring.ScoreScale);
            if (round.ThresholdPassed)
            {
                sb.Append(Loc.Pick("  [threshold passed]", "  [eşik geçildi]"));
            }
            sb.Append('\n');
            sb.Append(Loc.Pick("Total score ", "Toplam puan ")).Append(session.TotalScore).Append('\n');
            sb.Append(Loc.Pick("Draw ", "Çekme ")).Append(round.Deck.DrawCount)
                .Append(Loc.Pick("   Discard ", "   Iskarta ")).Append(round.Deck.DiscardCount)
                .Append(Loc.Pick("   Removed ", "   Çıkan ")).Append(round.Deck.RemovedCount).Append('\n');
            sb.Append(Loc.Pick("Jokers ", "Joker ")).Append(session.Jokers.Count);
            if (session.Jokers.Count > 0)
            {
                sb.Append(Loc.Pick("   (1-9 activate)", "   (1-9 kullan)"));
            }
            sb.Append(Loc.Pick("   Powers ", "   Güç ")).Append(session.Powers.Count);
            if (session.Powers.Count > 0)
            {
                sb.Append(Loc.Pick("   (click to use, one per turn)", "   (tıkla, tur başına bir)"));
            }
            sb.Append('\n');
            sb.Append(Loc.Pick(
                "Drag to place.  Click draw pile: deck.  Right-click: rotate GEARS / reshape FOX\n",
                "Sürükleyip yerleştir.  Çekme destesi: kartların.  Sağ tık: ÇARK döndür / TİLKİ şekillendir\n"));
            sb.Append(Loc.Pick(
                "Debug - S: redraw hand   B: bonus card   D: choose deck   R: new run   L: türkçe\n",
                "Debug - S: eli yenile   B: bonus kart   D: deste seç   R: yeni oyun   L: english\n"));
            sb.Append(Loc.Pick(
                "Debug - J: pick joker   P: pick power   K: sell last joker",
                "Debug - J: joker seç   P: güç seç   K: son jokeri sat"));
            infoText.text = sb.ToString();

            if (pendingTargetJokerId.HasValue)
            {
                Joker targeting = session.Jokers.Find(pendingTargetJokerId.Value);
                string what = targeting != null && targeting.Targeting == ActivationTargeting.BoardCell
                    ? Loc.Pick("pick a cube on the board", "oyun alanından bir küp seç")
                    : Loc.Pick("pick a block from your hand", "elinden bir blok seç");
                messageText.text = (targeting != null ? targeting.DisplayName : "Joker")
                    + ": " + what + Loc.Pick("\n[Esc] cancel", "\n[Esc] vazgeç");
                return;
            }
            if (pendingTargetPowerId.HasValue)
            {
                Power targeting = session.Powers.Find(pendingTargetPowerId.Value);
                string what = pendingOltaMark
                    ? Loc.Pick("pick the hand card to mark (free, once per round)",
                        "işaretlenecek kartı elinden seç (bedava, raunt başına bir)")
                    : targeting != null && targeting.Targeting == ActivationTargeting.BoardCell
                        ? Loc.Pick("pick a cell on the board", "oyun alanından bir hücre seç")
                        : Loc.Pick("pick a block from your hand", "elinden bir blok seç");
                messageText.text = (targeting != null ? targeting.DisplayName : Loc.Pick("Power", "Güç"))
                    + ": " + what + Loc.Pick("\n[Esc] cancel", "\n[Esc] vazgeç");
                return;
            }

            switch (session.Phase)
            {
                case GamePhase.GameOver:
                    messageText.text = Loc.Pick("GAME OVER - ", "OYUN BİTTİ - ") + DescribeLoss(round.Loss)
                        + Loc.Pick("\n[R] new run", "\n[R] yeni oyun");
                    break;
                case GamePhase.Market:
                    messageText.text = Loc.Pick(
                            "Click a card to add it to your deck (price below it)\n[N] start round ",
                            "Desteye katmak için karta tıkla (fiyatı altında)\n[N] raunt başlat: ")
                        + (session.RoundNumber + 1);
                    break;
                default:
                    if (round.Status == RoundStatus.AwaitingAdvanceDecision)
                    {
                        int continueCost = round.NextContinueCost;
                        int drawAfter = round.PredictDrawCountAfterContinue();
                        string warning = drawAfter < 0
                            ? Loc.Pick("  DECK OUT!", "  DESTE BİTER!")
                            : string.Empty;
                        messageText.text = Loc.Pick(
                                "Threshold reached!\n[A] advance to market    [C] continue: removes ",
                                "Eşik geçildi!\n[A] markete ilerle    [C] devam et: ")
                            + continueCost
                            + Loc.Pick(" cards, draw pile ", " kart gider, çekme destesi ")
                            + round.Deck.DrawCount
                            + " -> " + Mathf.Max(drawAfter, 0) + warning;
                    }
                    else
                    {
                        messageText.text = string.Empty;
                    }
                    break;
            }
        }

        private static string DescribeLoss(LossReason? loss)
        {
            switch (loss)
            {
                case LossReason.NoPlayableMove:
                    return Loc.Pick("no held block fits the board",
                        "eldeki hiçbir blok alana sığmıyor");
                case LossReason.HandCannotBeRefilled:
                    return Loc.Pick("the deck ran out (hand could not be refilled)",
                        "deste tükendi (el doldurulamadı)");
                case LossReason.DrawPileEmptyAfterThreshold:
                    return Loc.Pick("draw pile emptied before a clean sweep",
                        "temizlik gelmeden çekme destesi bitti");
                case LossReason.BetFailed:
                    return Loc.Pick("the bet was lost (Batak)", "bahis tutmadı (Batak)");
                case LossReason.RetroTopOut:
                    return Loc.Pick("topped out - no room to drop from above",
                        "tepeye ulaştın - yukarıdan blok düşecek yer yok");
                default:
                    return Loc.Pick("unknown", "bilinmiyor");
            }
        }

        private static void LogTurn(TurnReport report)
        {
            string sweep = report.CleanSweep ? ", CLEAN SWEEP" : string.Empty;
            Debug.Log("[project_block] Turn " + report.TurnNumber + ": " + report.Card
                + " at " + report.Origin + " -> +" + report.ScoreGained + " ("
                + (report.ExplodedRows.Count + report.ExplodedColumns.Count) + " lines, "
                + report.CubesExploded + " cubes" + sweep + "), status " + report.StatusAfter);
        }
    }
}
