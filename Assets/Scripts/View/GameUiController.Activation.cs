// PURPOSE: GameUiController activation - arming and running player-activated jokers and
// powers, the choice/batak/powerbank/block-designer pickers, targeting, and the
// power-blast FX.

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
        /// <summary>Runs a joker, or arms targeting mode if it must be pointed at something.</summary>
        private void BeginActivation(Joker joker)
        {
            if (!session.Jokers.CanActivate(joker.InstanceId))
            {
                Debug.Log("[project_block] " + joker.DisplayName + " cannot be used right now.");
                return;
            }
            // One joker asks the player for a value first, via a modal picker (Batak is now a
            // power - its picker is opened from BeginPowerActivation instead).
            var powerbank = joker as PowerbankJoker;
            if (powerbank != null)
            {
                OpenPowerbankPicker(powerbank);
                return;
            }
            if (joker.Targeting != ActivationTargeting.None)
            {
                pendingTargetJokerId = joker.InstanceId;
                UpdateHud();
                jokerBar.Refresh(session, pendingTargetJokerId);
                return;
            }
            RunActivation(joker, ActivationTarget.None);
        }

        private void OpenBatakPicker(BatakPower batak)
        {
            batakBetPowerId = batak.InstanceId;
            batakBet.Show(Loc.Pick("Batak: bet how many turns to sweep?",
                "Batak: kaç turda temizlersin?"));
        }

        private void OpenPowerbankPicker(PowerbankJoker powerbank)
        {
            pendingChoice = ChoiceKind.PowerbankTarget;
            pendingChoiceJokerId = powerbank.InstanceId;
            pendingChoiceValues.Clear();
            var labels = new List<string>();
            IReadOnlyList<Power> powers = session.Powers.Powers;
            for (int i = 0; i < powers.Count; i++)
            {
                if (!powers[i].Charged)
                {
                    pendingChoiceValues.Add(powers[i].InstanceId);
                    labels.Add(powers[i].DisplayName);
                }
            }
            if (labels.Count == 0)
            {
                ClearChoice();
                return; // nothing spent to refill
            }
            choicePicker.Show(Loc.Pick("Powerbank: recharge which power?",
                "Powerbank: hangi gücü doldur?"), labels);
        }

        private void ResolveChoice(int index)
        {
            if (index < 0 || index >= pendingChoiceValues.Count)
            {
                return;
            }
            var ctx = new RoundContext(session, session.Rng, session.CurrentRound);
            if (pendingChoice == ChoiceKind.PowerbankTarget)
            {
                var powerbank = session.Jokers.Find(pendingChoiceJokerId) as PowerbankJoker;
                if (powerbank != null && powerbank.RechargeChosen(ctx, pendingChoiceValues[index]))
                {
                    Debug.Log("[project_block] Powerbank recharged power #" + pendingChoiceValues[index]);
                    jokerBar.PulseJoker(pendingChoiceJokerId);
                }
            }
            RefreshAll(null);
        }

        private void ClearChoice()
        {
            pendingChoice = ChoiceKind.None;
            pendingChoiceJokerId = 0;
            pendingChoiceValues.Clear();
        }

        /// <summary>The block designer's Confirm: bake the drawn shape + element into the deck
        /// and spend the "Karakter oluşturma" charge (all rules in GameSession). An empty shape
        /// keeps the designer open.</summary>
        private void ConfirmBlockDesigner()
        {
            IReadOnlyList<GridPos> cells = blockDesigner.ShapeCells();
            if (cells.Count == 0)
            {
                return; // nothing drawn yet - leave the designer open
            }
            // A block must be one connected piece; reject scattered cells and keep the designer
            // open with a warning rather than baking a disjoint "block".
            if (!blockDesigner.IsSingleConnectedPiece())
            {
                blockDesigner.SetWarning(Loc.Pick(
                    "the shape must be one connected piece",
                    "şekil tek parça bağlı olmalı"));
                return;
            }
            // Each drawn cube may carry its own element (or none); Core builds the shape and
            // aligns the per-cube elements to it (see GameSession.CreateDesignedBlock).
            IReadOnlyList<BlockElement?> cellElements = blockDesigner.CellElements();
            bool made = session.CreateDesignedBlock(designerPowerId, cells, cellElements);
            blockDesigner.Hide();
            if (made)
            {
                powerBar.PulsePower(designerPowerId);
                Debug.Log("[project_block] Karakter oluşturma: baked a " + cells.Count
                    + "-cube block into the deck.");
            }
            powerBar.Refresh(session, null);
            RefreshAll(null);
        }

        private void CancelTargeting()
        {
            pendingTargetJokerId = null;
            pendingTargetPowerId = null;
            pendingOltaMark = false;
            boardView.ClearPreview();
            UpdateHud();
            jokerBar.Refresh(session, null);
            powerBar.Refresh(session, null);
        }

        private void RunActivation(Joker joker, ActivationTarget target)
        {
            pendingTargetJokerId = null;
            RoundEngine round = session.CurrentRound;
            // Remember which card a hand-targeted joker (İade) is about to replace, so the
            // swap can be animated after the engine has already done it.
            int replacedCardId = -1;
            if (target.HandIndex.HasValue && round != null
                && target.HandIndex.Value >= 0 && target.HandIndex.Value < round.Hand.Count)
            {
                replacedCardId = round.Hand[target.HandIndex.Value].Id;
            }
            if (!session.Jokers.TryActivate(joker.InstanceId, target))
            {
                Debug.Log("[project_block] " + joker.DisplayName + " could not be used.");
                RefreshAll(null);
                return;
            }
            Debug.Log("[project_block] Joker used: " + joker.DisplayName);
            jokerBar.PulseJoker(joker.InstanceId);
            // The whole-hand redraw has its own animation; a single-card swap flies the
            // returned card out and deals its replacement; everything else just re-syncs.
            if (joker.DefId == "renovasyon" && round.Status != RoundStatus.Lost)
            {
                sfx.Shuffle();
                cardLayer.AnimateRedraw(round);
                boardView.Refresh();
                UpdateHud();
                jokerBar.Refresh(session, null);
                return;
            }
            if (replacedCardId >= 0 && round.Status != RoundStatus.Lost)
            {
                cardLayer.AnimateReplaceCard(round, replacedCardId);
                boardView.Refresh();
                UpdateHud();
                jokerBar.Refresh(session, null);
                return;
            }
            RefreshAll(null);
        }

        /// <summary>Uses a power, or arms targeting mode if it must be pointed at something.
        /// The power twin of BeginActivation.</summary>
        private void BeginPowerActivation(Power power)
        {
            // Olta with no mark: clicking it starts the FREE mark pick (per-round setup,
            // not a use), so the rod is usable at all from the UI.
            var olta = power as OltaPower;
            if (olta != null && !olta.MarkedCardId.HasValue
                && session.CurrentRound != null
                && session.CurrentRound.Status == RoundStatus.InProgress)
            {
                pendingTargetPowerId = power.InstanceId;
                pendingOltaMark = true;
                UpdateHud();
                powerBar.Refresh(session, pendingTargetPowerId);
                return;
            }
            // "Karakter oluşturma" opens the block designer instead of running through TryUse;
            // the designer's Confirm bakes the block and spends the charge (CreateDesignedBlock).
            if (power is KarakterOlusturmaPower)
            {
                if (!session.Powers.CanBeginUse(power.InstanceId))
                {
                    Debug.Log("[project_block] " + power.DisplayName + " cannot be used right now.");
                    return;
                }
                designerPowerId = power.InstanceId;
                blockDesigner.Show();
                return;
            }
            // "Batak" opens the bet picker; GameSession.PlaceBatakBet places it + spends the charge.
            var batak = power as BatakPower;
            if (batak != null && !batak.HasActiveBet)
            {
                if (!session.Powers.CanBeginUse(power.InstanceId))
                {
                    Debug.Log("[project_block] " + power.DisplayName + " cannot be used right now.");
                    return;
                }
                OpenBatakPicker(batak);
                return;
            }
            if (!session.Powers.CanBeginUse(power.InstanceId))
            {
                Debug.Log("[project_block] " + power.DisplayName + " cannot be used right now.");
                return;
            }
            if (power.Targeting != ActivationTargeting.None)
            {
                pendingTargetPowerId = power.InstanceId;
                UpdateHud();
                powerBar.Refresh(session, pendingTargetPowerId);
                return;
            }
            RunPowerActivation(power, ActivationTarget.None);
        }

        private void RunPowerActivation(Power power, ActivationTarget target)
        {
            pendingTargetPowerId = null;
            pendingOltaMark = false;
            // A hand-targeted power may change how the card DISPLAYS (Cımbız rotates it),
            // and card visuals are cached by id - drop the visual so Sync rebuilds it.
            RoundEngine round = session.CurrentRound;
            int targetCardId = -1;
            if (target.HandIndex.HasValue && round != null
                && target.HandIndex.Value >= 0 && target.HandIndex.Value < round.Hand.Count)
            {
                targetCardId = round.Hand[target.HandIndex.Value].Id;
            }
            // Cells a board-targeting power will hit, captured BEFORE the destruction so the
            // blast can play on them afterwards.
            IReadOnlyList<GridPos> blastCells = power.PreviewCells(target);
            // Whole-board powers (Bardağın boş tarafı, Çerçeve...) destroy board-dependent cells
            // that PreviewCells cannot predict; capture what they actually destroy for the blast.
            if (round != null)
            {
                round.BeginExternalCapture();
            }
            if (!session.Powers.TryUse(power.InstanceId, target))
            {
                Debug.Log("[project_block] " + power.DisplayName + " could not be used.");
                boardView.ClearPreview();
                RefreshAll(null);
                // Retro only refuses when turning OFF with a dirty dead zone - tell the player.
                if (power.DefId == "retro")
                {
                    messageText.text = Loc.Pick(
                        "Clear the dead zone before leaving retro mode.",
                        "Retro modundan çıkmadan önce ölü bölgeyi temizle.");
                }
                return;
            }
            // Prefer the cells actually destroyed between turns (no board resize, so their coords
            // stay valid for the FX); targeted powers keep their predicted PreviewCells blast.
            if ((blastCells == null || blastCells.Count == 0) && round != null
                && round.ExternalDestructionLog.Count > 0)
            {
                blastCells = new List<GridPos>(round.ExternalDestructionLog);
            }
            Debug.Log("[project_block] Power used: " + power.DisplayName);
            powerBar.PulsePower(power.InstanceId);
            if (targetCardId >= 0)
            {
                cardLayer.ForgetCard(targetCardId);
            }
            boardView.ClearPreview();
            // Powers can rewrite the board (inflations replace it wholesale, Kum saati
            // rewinds it) and the piles - a full resync covers every one of them.
            RefreshAll(null);
            if (session.Phase == GamePhase.Market)
            {
                // "Totem" ends overtime and advances straight to the market mid-use; mirror the
                // normal advance flow (RefreshAll + Show) so the market actually appears.
                marketView.Show(session);
            }
            PlayPowerBlast(blastCells);
        }

        /// <summary>"Robot süpürge": a short beat after the player places a block, the sweeper
        /// eats a cube with a shake + blast. Input is locked until it goes off, so nothing can
        /// be placed until the sweep resolves.</summary>
        private void TriggerSupurgeBlast()
        {
            supurgeBuffer.Clear();
            IReadOnlyList<Joker> jokers = session.Jokers.Jokers;
            for (int i = 0; i < jokers.Count; i++)
            {
                var rs = jokers[i] as RobotSupurgeJoker;
                if (rs != null)
                {
                    supurgeBuffer.AddRange(rs.LastSweptCells);
                }
            }
            if (supurgeBuffer.Count == 0)
            {
                return;
            }
            StartCoroutine(SupurgeBlastRoutine(new List<GridPos>(supurgeBuffer)));
        }

        private IEnumerator SupurgeBlastRoutine(List<GridPos> cells)
        {
            supurgeAnimating = true;
            yield return new WaitForSeconds(0.28f);
            var color = new Color(0.7f, 0.85f, 1f);
            for (int i = 0; i < cells.Count; i++)
            {
                blastFx.EmitAt(boardView.CellToWorld(cells[i]), color, 6);
            }
            sfx.Explode();
            ShakeCamera(0.14f, 0.22f);
            supurgeAnimating = false;
        }

        /// <summary>Blast particles + shake + sound on the cells a board power just hit.</summary>
        private void PlayPowerBlast(IReadOnlyList<GridPos> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return;
            }
            var blastColor = new Color(1f, 0.72f, 0.35f);
            bool any = false;
            for (int i = 0; i < cells.Count; i++)
            {
                if (session.CurrentRound != null && session.CurrentRound.Board.IsInside(cells[i]))
                {
                    blastFx.EmitAt(boardView.CellToWorld(cells[i]), blastColor, 5);
                    any = true;
                }
            }
            if (any)
            {
                sfx.Explode();
                ShakeCamera(0.12f, 0.2f);
            }
        }
    }
}
