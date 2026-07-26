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
            TriggerInfectionBlast();
            // A resolved turn is the natural save point: the per-turn scratch state is at rest,
            // which is exactly what the save format assumes (see RoundEngine.Save).
            AutoSave();
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
            // "Alacakaranlık": a blast is the one thing that shows the player anything, so it
            // lights its own surroundings before the dark closes back over them.
            if (boardView.IsDark)
            {
                var lit = new List<GridPos>();
                foreach (DestroyedCube dead in report.DestroyedCubes)
                {
                    lit.Add(dead.Pos);
                }
                lit.AddRange(report.ExtraExplodedCells);
                boardView.LightUpAround(lit);
            }
            HandleBlastFeedback(round, report);
        }

        /// <summary>Particles, shake, combo popups and the sweep celebration for one turn.</summary>
        private void HandleBlastFeedback(RoundEngine round, TurnReport report)
        {
            if (report.CubesExploded == 0 && report.ExtraExplodedCells.Count == 0)
            {
                comboStreak = 0;
                // A turn where a boss only LIFTED cells still needs its puff drawn - it just
                // breaks no combo and shakes no camera, because nothing exploded.
                EmitBlastParticles(round, report);
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
            // Cells a BOSS lifted off rather than destroyed ("Alzheimer" forgetting a card,
            // "Yürüyen merdiven" carrying a row away). A pale, cold puff instead of the warm
            // explosion, because nothing blew up and nothing was earned.
            if (report.LiftedCells.Count > 0)
            {
                var faded = new Color(0.62f, 0.68f, 0.82f, 0.9f);
                foreach (GridPos cell in report.LiftedCells)
                {
                    blastFx.EmitAt(boardView.CellToWorld(cell), faded, 3);
                }
            }
            // "Kaçakçı" defective goods: the cubes showed up and then let go. They fall through
            // the arena and off the bottom of the screen - nothing landed, so there is nothing to
            // blast, only something to drop.
            DropFellThroughCubes(report);
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

        /// <summary>Drops the cubes of a card that would not stay on the board, in both worlds.
        /// Staggered a little per cube so the block crumbles instead of sliding off as one slab.
        /// </summary>
        private void DropFellThroughCubes(TurnReport report)
        {
            SpawnFallingCubes(boardView, report.FellThroughCells, report.Card);
            SpawnFallingCubes(mirrorBoardView, report.MirrorFellThroughCells, report.MirrorCard);
        }

        private void SpawnFallingCubes(BoardView view, IReadOnlyList<GridPos> cells, BlockCard card)
        {
            if (view == null || cells == null || cells.Count == 0)
            {
                return;
            }
            Color color = card != null && card.Elements.Count > 0
                ? ViewUtil.ElementColor(card.Elements[0])
                : ViewUtil.ColorForCard(card != null ? card.Id : 0);
            for (int i = 0; i < cells.Count; i++)
            {
                FallingCubeFx.Spawn(transform, view.CellToWorld(cells[i]),
                    view.CellWorldSize * 0.86f, color, i * 0.045f);
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
            // "Öteki dünya" shrinks the main board and lifts it, to make room for the mirror
            // below. With one world these are the values the board always had.
            float mainSize = MainBoardWorldSize;
            Vector2 mainCenter = MainBoardCenter;
            if (boardView.Board != round.Board || !Mathf.Approximately(lastMainBoardSize, mainSize))
            {
                boardView.Rebuild(round.Board, mainSize, mainCenter);
                lastMainBoardSize = mainSize;
            }
            // "Alacakaranlık" - set BEFORE the refresh, so the very first paint is already dark.
            boardView.SetDarkness(round.BoardIsDark);
            boardView.Refresh();
            boardView.SetDeadZone(session.Config.Rules.DeadZoneRows);
            boardView.ClearPreview();
            RefreshMirrorWorld();
            RefreshInfections();
            cardLayer.Sync(round, report);
            flameStreak.SetState(round.ContinueCount, boardView.WorldRect);
            UpdateHud();
            jokerBar.Refresh(session, pendingTargetJokerId);
            powerBar.Refresh(session, pendingTargetPowerId);
            SyncRetroPresentation();
            SyncRescueState();
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
            // Every board marker is a map of the board, so none of them may show while blind.
            if (session.CurrentRound != null && session.CurrentRound.BoardIsDark)
            {
                boardView.ShowInfections(null);
                boardView.ShowCircuit(null);
                boardView.ShowQuarantine(null, null);
                boardView.ShowCreature(null);
                return;
            }
            boardView.ShowInfections(infectionBuffer);
            RefreshCircuit();
            RefreshQuarantine();
            RefreshCreature();
        }

        /// <summary>Hands "Besleme"'s creature patch to the board view. A pet you cannot see is
        /// a pet you cannot feed.</summary>
        private void RefreshCreature()
        {
            IReadOnlyList<Joker> jokers = session.Jokers.Jokers;
            for (int i = 0; i < jokers.Count; i++)
            {
                var pet = jokers[i] as BeslemeJoker;
                if (pet != null && pet.IsAlive)
                {
                    boardView.ShowCreature(pet.Region);
                    return;
                }
            }
            boardView.ShowCreature(null);
        }

        /// <summary>Hands "Karantina"'s sealed lines to the board view, which washes them.</summary>
        private void RefreshQuarantine()
        {
            RoundEngine round = session.CurrentRound;
            var boss = round != null ? round.Boss as KarantinaBoss : null;
            if (boss != null)
            {
                boardView.ShowQuarantine(boss.QuarantinedRows, boss.QuarantinedColumns);
            }
            else
            {
                boardView.ShowQuarantine(null, null);
            }
        }

        /// <summary>Hands "Devre"'s traced circuit to the board view. Same shape as the infection
        /// pass: the joker holds the route, the board just draws it.</summary>
        private void RefreshCircuit()
        {
            IReadOnlyList<Joker> jokers = session.Jokers.Jokers;
            for (int i = 0; i < jokers.Count; i++)
            {
                var devre = jokers[i] as DevreJoker;
                if (devre != null && devre.HasCircuit)
                {
                    boardView.ShowCircuit(devre.Path);
                    return;
                }
            }
            boardView.ShowCircuit(null);
        }

        /// <summary>The prominent score line: the run total (which is also the market's money)
        /// and, while a round is being played, how that round stands against its threshold.
        /// Both numbers are in the scaled economy, so the threshold is lifted to match.</summary>
        private void UpdateScoreHud()
        {
            if (totalText == null)
            {
                return;
            }
            if (session == null)
            {
                totalText.text = string.Empty;
                return;
            }
            if (session.Phase != GamePhase.Round)
            {
                // The market panel is opaque and reaches the top of the screen, and it prints
                // the balance itself - a HUD line here would just overprint its title.
                totalText.text = string.Empty;
                return;
            }
            var sb = new StringBuilder();
            sb.Append(Loc.Pick("TOTAL ", "TOPLAM ")).Append(session.TotalScore);
            RoundEngine round = session.CurrentRound;
            if (round != null)
            {
                sb.Append(Loc.Pick("        round ", "        raunt "))
                    .Append(round.RoundScore).Append(" / ")
                    .Append(round.Config.ScoreThreshold * session.Config.Scoring.ScoreScale);
            }
            totalText.text = sb.ToString();
        }

        /// <summary>The HUD while the market is open. The round dump does NOT belong here: it
        /// describes a round that has already finished (turn counter, board size, the erosion
        /// clock, "right-click to rotate") and, because the canvas draws over world space, it
        /// printed straight across the market panel. Only the run-level facts and the debug
        /// keys survive, and the panel itself carries the prompts.</summary>
        private void BuildMarketHud()
        {
            var sb = new StringBuilder();
            sb.Append("Seed ").Append(lastSeedUsed)
                .Append(Loc.Pick("   Deck: ", "   Deste: ")).Append(currentDeck.Name).Append('\n');
            sb.Append(Loc.Pick("Round ", "Raunt ")).Append(session.RoundNumber)
                .Append(" / ").Append(session.Config.TotalRounds)
                .Append(Loc.Pick("  done", "  bitti")).Append('\n');
            sb.Append(Loc.Pick("Cards ", "Kart ")).Append(session.OwnedCards.Count)
                .Append(Loc.Pick("   Jokers ", "   Joker ")).Append(session.Jokers.Count)
                .Append(Loc.Pick("   Powers ", "   Güç ")).Append(session.Powers.Count).Append('\n');
            // "Kredi kartı": paying the debt down is a MARKET action, so its prompt belongs
            // here and nowhere else - the round HUD only names the debt and its deadline.
            if (session.Debt > 0)
            {
                sb.Append(Loc.Pick("DEBT ", "BORÇ ")).Append(session.Debt)
                    .Append(Loc.Pick("   [O] pay", "   [O] öde")).Append('\n');
            }
            sb.Append(Loc.Pick(
                "Debug - J: pick joker   P: pick power   D: choose deck   R: new run   L: türkçe",
                "Debug - J: joker seç   P: güç seç   D: deste seç   R: yeni oyun   L: english"));
            infoText.text = sb.ToString();
            // "Kaçakçı": the free item is invisible unless the market says so.
            messageText.text = session.CanSmuggle
                ? Loc.Pick("SHIFT+click an offer: take it FREE (may be defective)",
                    "Bir ürüne SHIFT+tık: BEDAVA al (defolu çıkabilir)")
                : string.Empty;
        }

        private void UpdateHud()
        {
            UpdateScoreHud();
            // The draw pile doubles as the SELL screen while shopping, which nothing on screen
            // said. Set from the phase on every refresh, so it can never be left on in a round.
            cardLayer.SetSellHint(session.Phase == GamePhase.Market);
            if (session.Phase == GamePhase.Market)
            {
                BuildMarketHud();
                return;
            }
            RoundEngine round = session.CurrentRound;
            var sb = new StringBuilder();
            sb.Append("Seed ").Append(lastSeedUsed)
                .Append(Loc.Pick("   Deck: ", "   Deste: ")).Append(currentDeck.Name).Append('\n');
            sb.Append(Loc.Pick("Round ", "Raunt ")).Append(session.RoundNumber)
                .Append(" / ").Append(session.Config.TotalRounds);
            if (round.Config.IsBossRound)
            {
                sb.Append(Loc.Pick("  [BOSS", "  [PATRON"));
                if (round.Boss != null)
                {
                    sb.Append(": ").Append(round.Boss.DisplayName);
                    if (round.Boss.StatusText != null)
                    {
                        sb.Append(" - ").Append(round.Boss.StatusText);
                    }
                }
                sb.Append(']');
            }
            // "Uzun vadeli yatırımcı" restarts the final round silently, so say why the board
            // just went blank - otherwise the do-over looks like a bug.
            if (session.FinalRoundReplays > 0)
            {
                sb.Append(Loc.Pick("  [REPLAY]", "  [TEKRAR]"));
            }
            sb.Append(Loc.Pick("   Turn ", "   Tur ")).Append(round.TurnNumber).Append('\n');
            if (round.Boss != null)
            {
                sb.Append(round.Boss.Description).Append('\n');
            }
            // RoundScore lives in the scaled economy; lift the threshold to match for display.
            sb.Append(Loc.Pick("Score ", "Puan ")).Append(round.RoundScore)
                // RoundEngine.ScoreThreshold, not the config's: a boss may ask for less
                // ("Taş ve sopa") and the bar on screen has to be the bar the rules use.
                .Append(" / ").Append(round.ScoreThreshold * session.Config.Scoring.ScoreScale);
            if (round.ThresholdPassed)
            {
                sb.Append(Loc.Pick("  [threshold passed]", "  [eşik geçildi]"));
            }
            sb.Append('\n');
            // The run total is NOT repeated here - it has its own line at the top of the screen
            // (UpdateScoreHud), because it is real UI rather than debug furniture. The DEBT is,
            // though: "Kredi kartı" is the one number the player must not lose track of, and its
            // deadline has to be spelled out. Paying is a MARKET action, so the [O] prompt lives
            // in BuildMarketHud instead - this method returns early in the market.
            if (session.Debt > 0)
            {
                sb.Append(Loc.Pick("DEBT ", "BORÇ ")).Append(session.Debt);
                int next = NextBossRound(session);
                if (next > 0)
                {
                    sb.Append(Loc.Pick("  (pay before round ", "  (raunt "))
                        .Append(next)
                        .Append(Loc.Pick(" or lose)", " bitmeden öde yoksa kaybedersin)"));
                }
                sb.Append('\n');
            }
            else if (session.CreditAvailable)
            {
                sb.Append(Loc.Pick("(credit open)", "(kredi açık)")).Append('\n');
            }
            sb.Append(Loc.Pick("Draw ", "Çekme ")).Append(round.Deck.DrawCount)
                .Append(Loc.Pick("   Discard ", "   Iskarta ")).Append(round.Deck.DiscardCount)
                .Append(Loc.Pick("   Removed ", "   Çıkan ")).Append(round.Deck.RemovedCount).Append('\n');
            // The anti-stalling clock: how many more times the deck may run dry for free, and
            // how much of the arena the stalling has already cost.
            if (round.Config.Erosion != ShuffleErosion.None)
            {
                sb.Append(Loc.Pick("Board ", "Alan ")).Append(round.Board.Width)
                    .Append('x').Append(round.Board.Height);
                if (round.Board.DeadCellCount > 0)
                {
                    sb.Append(Loc.Pick("  eaten ", "  yenen ")).Append(round.Board.DeadCellCount);
                }
                if (round.FreeDeckRecyclesLeft > 0)
                {
                    sb.Append(Loc.Pick("   free reshuffles ", "   bedava karma "))
                        .Append(round.FreeDeckRecyclesLeft);
                }
                else
                {
                    sb.Append(Loc.Pick("   ERODING - next reshuffle eats the board",
                        "   ERİYOR - sıradaki karma alanı yiyor"));
                }
                sb.Append('\n');
            }
            AppendMirrorHud(sb, round);
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
                "Debug - J: pick joker   P: pick power   K: sell last joker   O: pay debt   W/M: two worlds",
                "Debug - J: joker seç   P: güç seç   K: son jokeri sat   O: borç öde   W/M: iki dünya"));
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
            // A workshop power's pick has steps, and a player who does not know which step they
            // are on is just clicking. Say it.
            if (workshopPowerId.HasValue)
            {
                Power shop = session.Powers.Find(workshopPowerId.Value);
                string step;
                if (shop != null && shop.Targeting == ActivationTargeting.CellAndHandCard)
                {
                    step = workshopDonorCell.HasValue
                        ? Loc.Pick("now pick the card that takes the element",
                            "şimdi elementi alacak kartı seç")
                        : Loc.Pick("pick the cube whose element you want",
                            "elementini alacağın küpü seç");
                }
                else if (shop != null && shop.Targeting == ActivationTargeting.TwoHandCards)
                {
                    step = workshopFirstCard < 0
                        ? Loc.Pick("pick the FIRST block to weld",
                            "lehimlenecek BİRİNCİ bloğu seç")
                        : Loc.Pick("now pick the SECOND block",
                            "şimdi İKİNCİ bloğu seç");
                }
                else
                {
                    step = Loc.Pick("pick the block to cut",
                        "kesilecek bloğu seç");
                }
                messageText.text = (shop != null ? shop.DisplayName : "Power") + ": " + step
                    + Loc.Pick("\n[Esc] cancel", "\n[Esc] vazgeç");
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

            // The dead-end pause: the board is full and only the rescue power can save it.
            if (round.Status == RoundStatus.AwaitingRescue)
            {
                messageText.text = Loc.Pick(
                    "No room left! Pick two rows or two columns to swap.\n[Esc] give up",
                    "Yer kalmadı! Yerini değiştirmek için iki satır ya da iki sütun seç."
                        + "\n[Esc] pes et");
                return;
            }

            switch (session.Phase)
            {
                case GamePhase.GameOver:
                    messageText.text = Loc.Pick("GAME OVER - ", "OYUN BİTTİ - ") + DescribeLoss(round.Loss)
                        + Loc.Pick("\n[R] new run", "\n[R] yeni oyun");
                    break;
                case GamePhase.RunWon:
                    messageText.text = Loc.Pick(
                            "RUN COMPLETE! All ", "OYUN TAMAMLANDI! ")
                        + session.Config.TotalRounds
                        + Loc.Pick(" rounds survived.\nFinal score ", " rauntun hepsi geçildi.\nSon puan ")
                        + session.TotalScore
                        + Loc.Pick("\n[R] new run", "\n[R] yeni oyun");
                    break;
                // GamePhase.Market never reaches here - BuildMarketHud handles it and returns,
                // and the buy / next-round prompt is drawn inside the market panel instead.
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

        /// <summary>The round number of the next boss round at or after the current one - the
        /// deadline the market debt has to be settled by. 0 when the progression has no boss
        /// rounds at all.</summary>
        private static int NextBossRound(GameSession session)
        {
            for (int round = session.RoundNumber; round <= session.Config.TotalRounds; round++)
            {
                if (session.Config.Progression.GetRound(round).IsBossRound)
                {
                    return round;
                }
            }
            return 0;
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
                case LossReason.ForbiddenCleanSweep:
                    return Loc.Pick("you cleared the board - Çıkmaz forbids it",
                        "tahtayı temizledin - Çıkmaz buna izin vermiyor");
                case LossReason.ForbiddenThreshold:
                    return Loc.Pick("you reached the threshold - Çıkmaz forbids it",
                        "puan eşiğine ulaştın - Çıkmaz buna izin vermiyor");
                case LossReason.DebtNotRepaid:
                    return Loc.Pick("a boss round ended with your market debt still open",
                        "patron raundu bitti, market borcun hâlâ açıktı");
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
