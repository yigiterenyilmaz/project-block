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
                Debug.Log("[block_bonk] " + joker.DisplayName + " cannot be used right now.");
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
            RunActivation(joker, AimedAtChosenWorld(ActivationTarget.None));
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

        /// <summary>"Kütleçekim merkezi": asks which way water should fall. A direction is not a
        /// place, so it is picked from a small list rather than clicked on the board - the same
        /// modal Powerbank and Batak use.</summary>
        private void OpenDirectionPicker(Power power)
        {
            pendingChoice = ChoiceKind.GravityDirection;
            pendingChoiceJokerId = power.InstanceId;
            pendingChoiceValues.Clear();
            // The values are packed steps (see UnpackStep), in the same order as the labels.
            pendingChoiceValues.Add(PackStep(0, 1));
            pendingChoiceValues.Add(PackStep(0, -1));
            pendingChoiceValues.Add(PackStep(-1, 0));
            pendingChoiceValues.Add(PackStep(1, 0));
            choicePicker.Show(
                power.DisplayName + Loc.Pick(": water falls which way?", ": su hangi yöne aksın?"),
                new List<string>
                {
                    Loc.Pick("UP", "YUKARI"),
                    Loc.Pick("DOWN (normal)", "AŞAĞI (normal)"),
                    Loc.Pick("LEFT", "SOLA"),
                    Loc.Pick("RIGHT", "SAĞA")
                });
        }

        // A GridPos will not fit in the picker's int list, so the step travels packed.
        private static int PackStep(int x, int y)
        {
            return (x + 1) * 10 + (y + 1);
        }

        private static GridPos UnpackStep(int packed)
        {
            return new GridPos(packed / 10 - 1, packed % 10 - 1);
        }

        /// <summary>
        /// DEBUG: lists every boss so one can be started on the spot. There are far more of them
        /// than fit a single modal, so the list PAGES - the last two rows are "more" (which wraps)
        /// and a normal random draw.
        /// </summary>
        private void OpenBossPicker()
        {
            pendingChoice = ChoiceKind.BossStage;
            pendingChoiceValues.Clear();
            var labels = new List<string>();
            IReadOnlyList<BossDefinition> all = BossRegistry.All;
            int pages = (all.Count + BossPickerPageSize - 1) / BossPickerPageSize;
            if (pages < 1)
            {
                pages = 1;
            }
            bossPickerPage = ((bossPickerPage % pages) + pages) % pages;
            int first = bossPickerPage * BossPickerPageSize;
            for (int i = first; i < all.Count && i < first + BossPickerPageSize; i++)
            {
                pendingChoiceValues.Add(i); // the registry index; -1 and -2 are the two commands
                labels.Add(all[i].DisplayName);
            }
            if (all.Count > BossPickerPageSize)
            {
                pendingChoiceValues.Add(BossPickerMore);
                labels.Add(Loc.Pick("more...  (" + (bossPickerPage + 1) + "/" + pages + ")",
                    "devamı...  (" + (bossPickerPage + 1) + "/" + pages + ")"));
            }
            pendingChoiceValues.Add(BossPickerRandom);
            labels.Add(Loc.Pick("RANDOM (draw one normally)", "RASTGELE (normal çekim)"));
            choicePicker.Show(Loc.Pick("DEBUG: start a boss stage",
                "DEBUG: patron sahnesi başlat"), labels);
        }

        private const int BossPickerPageSize = 10;
        private const int BossPickerMore = -2;
        private const int BossPickerRandom = -1;

        /// <summary>Settles a picker row. Returns false when the picker was RE-OPENED instead of
        /// answered (the boss list turning a page), so the caller keeps the choice pending.</summary>
        private bool ResolveChoice(int index)
        {
            if (index < 0 || index >= pendingChoiceValues.Count)
            {
                return true;
            }
            if (pendingChoice == ChoiceKind.BossStage)
            {
                return ResolveBossPick(pendingChoiceValues[index]);
            }
            var ctx = new RoundContext(session, session.Rng, session.CurrentRound);
            if (pendingChoice == ChoiceKind.PowerbankTarget)
            {
                var powerbank = session.Jokers.Find(pendingChoiceJokerId) as PowerbankJoker;
                if (powerbank != null && powerbank.RechargeChosen(ctx, pendingChoiceValues[index]))
                {
                    Debug.Log("[block_bonk] Powerbank recharged power #" + pendingChoiceValues[index]);
                    jokerBar.PulseJoker(pendingChoiceJokerId);
                }
            }
            else if (pendingChoice == ChoiceKind.GravityDirection)
            {
                Power power = session.Powers.Find(pendingChoiceJokerId);
                if (power != null)
                {
                    GridPos step = UnpackStep(pendingChoiceValues[index]);
                    // Straight through the normal power path, so the charge, the one-power-per-turn
                    // rule and the between-turn FX capture all behave as they do for every power.
                    RunPowerActivation(power, ActivationTarget.Direction(step));
                }
            }
            RefreshAll(null);
            return true;
        }

        /// <summary>One row of the DEBUG boss picker: turn the page, or start a boss stage.</summary>
        private bool ResolveBossPick(int value)
        {
            if (value == BossPickerMore)
            {
                bossPickerPage++;
                OpenBossPicker(); // wraps at the last page
                return false;     // still pending - the picker is open again
            }
            IReadOnlyList<BossDefinition> all = BossRegistry.All;
            string defId = value >= 0 && value < all.Count ? all[value].DefId : null;
            if (!session.DebugStartBossStage(defId))
            {
                return true;
            }
            BossRound started = session.ActiveBoss;
            Debug.Log("[block_bonk] Debug boss stage: "
                + (started != null ? started.ToString() : "none"));
            marketView.Hide(); // the jump can be made from the market as well as mid-round
            StartRoundPresentation();
            messageText.text = Loc.Pick("DEBUG boss stage: ", "DEBUG patron sahnesi: ")
                + (started != null ? started.DisplayName : Loc.Pick("none", "yok"));
            return true;
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
                Debug.Log("[block_bonk] Karakter oluşturma: baked a " + cells.Count
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
            // The workshop powers' own pick, and whichever modal it had open.
            workshopPowerId = null;
            workshopFirstCard = -1;
            SecondWorkshopCard = -1;
            workshopDonorCell = null;
            workshopPressAnchor = null;
            cubePicker.Hide();
            weldPicker.Hide();
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
                Debug.Log("[block_bonk] " + joker.DisplayName + " could not be used.");
                RefreshAll(null);
                return;
            }
            Debug.Log("[block_bonk] Joker used: " + joker.DisplayName);
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
                    Debug.Log("[block_bonk] " + power.DisplayName + " cannot be used right now.");
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
                    Debug.Log("[block_bonk] " + power.DisplayName + " cannot be used right now.");
                    return;
                }
                OpenBatakPicker(batak);
                return;
            }
            if (!session.Powers.CanBeginUse(power.InstanceId))
            {
                Debug.Log("[block_bonk] " + power.DisplayName + " cannot be used right now.");
                return;
            }
            // The workshop powers ask for more than one thing, so they get their own little
            // state machine rather than the single pending click.
            if (power.Targeting == ActivationTargeting.CardCubes
                || power.Targeting == ActivationTargeting.TwoHandCards
                || power.Targeting == ActivationTargeting.CellAndHandCard
                || power.Targeting == ActivationTargeting.BoardArea)
            {
                BeginWorkshopTargeting(power);
                return;
            }
            // A DIRECTION is not a place on the board, so it is asked for with a picker rather
            // than by waiting for a click ("Kütleçekim merkezi").
            if (power.Targeting == ActivationTargeting.Direction)
            {
                OpenDirectionPicker(power);
                return;
            }
            if (power.Targeting != ActivationTargeting.None)
            {
                pendingTargetPowerId = power.InstanceId;
                UpdateHud();
                powerBar.Refresh(session, pendingTargetPowerId);
                return;
            }
            RunPowerActivation(power, AimedAtChosenWorld(ActivationTarget.None));
        }

        /// <summary>Starts a workshop power's pick. All three begin the same way - waiting for
        /// a click on the board or the hand - and diverge in HandleWorkshopClick.</summary>
        private void BeginWorkshopTargeting(Power power)
        {
            workshopPowerId = power.InstanceId;
            workshopFirstCard = -1;
            workshopDonorCell = null;
            workshopPressAnchor = null;
            pendingTargetPowerId = power.InstanceId; // so the bar and the hint light up
            UpdateHud();
            powerBar.Refresh(session, pendingTargetPowerId);
        }

        /// <summary>
        /// One click of a workshop power's pick. Returns true when it consumed the click.
        ///
        /// Neşter:    hand card  -> cube picker (multi) -> CUT
        /// Lehimleme: hand card  -> hand card          -> weld picker
        /// Gen nakli: board cube -> hand card
        /// </summary>
        private bool HandleWorkshopClick(Vector2 world)
        {
            if (!workshopPowerId.HasValue || session == null)
            {
                return false;
            }
            Power power = session.Powers.Find(workshopPowerId.Value);
            RoundEngine round = session.CurrentRound;
            if (power == null || round == null)
            {
                CancelTargeting();
                return true;
            }

            // "Hidrolik pres" is two clicks on the BOARD - the patch, then which of its four
            // cells keeps the cube - so it never reaches the hand-card tail below.
            if (power.Targeting == ActivationTargeting.BoardArea)
            {
                return HandlePressClick(power, round, world);
            }

            // A picker is open: the click belongs to it.
            if (cubePicker.IsMultiPick)
            {
                if (cubePicker.ConfirmAt(world))
                {
                    ActivationTarget target = ActivationTarget.CardCubes(workshopFirstCard,
                        cubePicker.PickedCells);
                    cubePicker.Hide();
                    RunPowerActivation(power, target);
                    workshopPowerId = null;
                    return true;
                }
                cubePicker.Toggle(cubePicker.CellAt(world));
                return true;
            }
            if (weldPicker.IsOpen)
            {
                GridPos? offset = weldPicker.OfferAt(world);
                if (!offset.HasValue)
                {
                    return true; // a miss inside the modal is just a miss
                }
                int second = SecondWorkshopCard;
                ActivationTarget target = ActivationTarget.TwoCards(workshopFirstCard, second,
                    offset.Value);
                weldPicker.Hide();
                RunPowerActivation(power, target);
                workshopPowerId = null;
                return true;
            }

            // "Gen nakli" wants the board cube first.
            if (power.Targeting == ActivationTargeting.CellAndHandCard
                && !workshopDonorCell.HasValue)
            {
                ActivationTarget cellTarget;
                if (!TryBoardTargetAt(world, out cellTarget) || !cellTarget.Cell.HasValue)
                {
                    CancelTargeting();
                    return true;
                }
                workshopDonorCell = cellTarget.Cell;
                UpdateHud();
                return true;
            }

            // Everything else is waiting for a hand card.
            CardVisual hit = cardLayer.CardAt(world);
            if (hit == null || hit.SlotIndex < 0 || hit.SlotIndex >= round.Hand.Count)
            {
                CancelTargeting();
                return true;
            }
            if (power.Targeting == ActivationTargeting.CellAndHandCard)
            {
                ActivationTarget target = ActivationTarget.CellAndCard(workshopDonorCell.Value,
                    hit.SlotIndex);
                RunPowerActivation(power, target);
                workshopPowerId = null;
                return true;
            }
            if (power.Targeting == ActivationTargeting.CardCubes)
            {
                workshopFirstCard = hit.SlotIndex;
                cubePicker.ShowMulti(round.EffectiveShape(round.Hand[hit.SlotIndex]),
                    Loc.Pick("Neşter: pick the cubes for the FIRST piece   [Esc] cancel",
                        "Neşter: BİRİNCİ parçaya girecek küpleri seç   [Esc] iptal"),
                    Loc.Pick("CUT", "KES"));
                return true;
            }
            // "Lehimleme": first card, then second, then where it goes.
            if (workshopFirstCard < 0)
            {
                workshopFirstCard = hit.SlotIndex;
                UpdateHud();
                return true;
            }
            if (hit.SlotIndex == workshopFirstCard)
            {
                return true; // the same card twice is not two cards
            }
            SecondWorkshopCard = hit.SlotIndex;
            weldPicker.Show(round.EffectiveShape(round.Hand[workshopFirstCard]),
                round.EffectiveShape(round.Hand[hit.SlotIndex]),
                Loc.Pick("Lehimleme: pick where the second block goes   [Esc] cancel",
                    "Lehimleme: ikinci bloğun nereye geleceğini seç   [Esc] iptal"));
            return true;
        }

        /// <summary>
        /// One click of "Hidrolik pres". The first names the 2x2 PATCH, the second names which of
        /// its four cells the pressed cube ends up in - a press in the far corner is a different
        /// piece of board from one in the near corner, and it opens outward from where it sits.
        /// A second click outside the patch is a miss, not a cancel: having already committed to
        /// a patch, the player should not lose it to a slipped cursor.
        /// </summary>
        private bool HandlePressClick(Power power, RoundEngine round, Vector2 world)
        {
            GridPos cell;
            if (!boardView.TryWorldToCell(world, out cell))
            {
                CancelTargeting();
                return true;
            }
            if (!workshopPressAnchor.HasValue)
            {
                if (!round.MainBoard.CanCompressAt(cell))
                {
                    CancelTargeting();
                    return true;
                }
                workshopPressAnchor = cell;
                boardView.ShowPressPreview(power.PreviewCells(ActivationTarget.Board(cell)), true);
                UpdateHud();
                return true;
            }
            GridPos anchor = workshopPressAnchor.Value;
            int dx = cell.X - anchor.X;
            int dy = cell.Y - anchor.Y;
            if (dx < 0 || dx > 1 || dy < 0 || dy > 1)
            {
                return true; // outside the patch: a miss inside a committed pick
            }
            workshopPowerId = null;
            workshopPressAnchor = null;
            boardView.ClearPreview();
            RunPowerActivation(power, ActivationTarget.BoardArea(anchor, new GridPos(dx, dy)));
            return true;
        }

        /// <summary>The second hand slot a weld is using. Its own field so the flow above reads
        /// as the three straight lines it is.</summary>
        private int SecondWorkshopCard { get; set; } = -1;

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
                Debug.Log("[block_bonk] " + power.DisplayName + " could not be used.");
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
            Debug.Log("[block_bonk] Power used: " + power.DisplayName);
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
            // "Kütleçekim merkezi": the flow it caused is the whole point of the power, so it is
            // animated rather than teleported. RefreshAll has already drawn the water where it
            // ended up, so the animation replays it from the start.
            if (round != null && round.ExternalWaterFrames.Count > 0)
            {
                var flow = new List<IReadOnlyList<WaterMove>>(round.ExternalWaterFrames);
                waterAnimating = true;
                boardView.PlayWaterAnimation(flow, delegate { waterAnimating = false; });
            }
            // "HIDROLIK PRES" DESTROYS NOTHING. It takes four cubes into storage under pressure, so
            // the cluster burst a board-targeting power would otherwise get here is the wrong
            // sentence entirely - four cubes breaking where they stood. The compression plays
            // instead, on THIS frame, the one the board was repainted in.
            if (PlayPressCompression(round))
            {
                return;
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
            // The bang waits for the first cell to burst, and the shake is the burst's own -
            // sized by how many cells the sweeper took.
            FlashCells(cells, new Color(0.7f, 0.85f, 1f), delegate { sfx.Explode(); });
            supurgeAnimating = false;
        }

        /// <summary>
        /// "Enfeksiyon": the infected block detonates at the END of the turn, through
        /// DestroyCubes rather than a line explosion - so its cubes appear in no exploded row
        /// or column and the normal blast pass never sees them. They used to simply blink out
        /// of existence. This blasts them in the same green the infection pips were pulsing,
        /// so the detonation reads as the infection going off.
        ///
        /// Immediate rather than delayed (unlike the sweeper): the destruction already happened
        /// during the turn, so the particles have to land as the cubes disappear, not after.
        /// </summary>
        private void TriggerInfectionBlast(TurnReport report)
        {
            IReadOnlyList<Joker> jokers = session.Jokers.Jokers;
            for (int i = 0; i < jokers.Count; i++)
            {
                var enf = jokers[i] as EnfeksiyonJoker;
                if (enf != null && enf.LastDetonatedCells.Count > 0)
                {
                    // Per JOKER, not merged: two infections are two organisms, each with its
                    // own block and its own answer to whether it has already spread.
                    PlayInfectionDetonation(report, enf);
                }
            }
        }

        /// <summary>
        /// One infection going off.
        ///
        /// It is split BY BLOCK before anything is drawn, because a turn can ripen two of them
        /// at once and a detonation is a block coming apart - eight cells from two different
        /// blocks bursting as one shape would be a lie about what happened. The split is by
        /// SourceCardId, which is what a block IS: the cells one card put down.
        ///
        /// The block's own tiles come from the turn's destruction log rather than from the
        /// board, because RefreshAll has already run and painted those cells empty.
        /// </summary>
        private void PlayInfectionDetonation(TurnReport report, EnfeksiyonJoker enf)
        {
            IReadOnlyList<GridPos> cells = enf.LastDetonatedCells;

            // THE CORE CHARGES FIRST. The rules already destroyed the cubes, so this is the one
            // place the presentation runs LATE on purpose: the infection goes quiet, beats twice
            // and then lets go. Without it the block simply vanishes and the joker's whole three
            // turns of buildup pay off in nothing. If the gap ever reads as detached from the
            // cubes going, InfectionChargeDelay is the one number to take to zero.
            float charge = 0f;
            for (int i = 0; i < cells.Count; i++)
            {
                charge = Mathf.Max(charge, boardView.PlayInfectionCharge(cells[i]));
            }

            infectionCubes.Clear();
            IReadOnlyList<DestroyedCube> log = report != null
                ? report.DestroyedCubes : null;
            if (log != null)
            {
                for (int i = 0; i < log.Count; i++)
                {
                    infectionCubes[log[i].Pos] = log[i].Cube;
                }
            }

            // Cells the arena no longer has (a turn that also eroded it) are dropped, exactly
            // as FlashCells drops them.
            GameBoard board = boardView != null ? boardView.Board : null;
            var blocks = new List<List<DestroyedCube>>();
            var blockIds = new List<int>();
            for (int i = 0; i < cells.Count; i++)
            {
                if (board == null || !board.IsInside(cells[i]))
                {
                    continue;
                }
                Cube cube;
                if (!infectionCubes.TryGetValue(cells[i], out cube))
                {
                    // Not in the log: draw it as a plain cube rather than skipping it. A cell
                    // the player watched go has to be seen going.
                    cube = new Cube(CubeKind.Normal, -1);
                }
                int at = blockIds.IndexOf(cube.SourceCardId);
                if (at < 0)
                {
                    blockIds.Add(cube.SourceCardId);
                    blocks.Add(new List<DestroyedCube>());
                    at = blocks.Count - 1;
                }
                blocks[at].Add(new DestroyedCube(cells[i], cube));
            }
            if (blocks.Count == 0)
            {
                return;
            }

            // The spread belongs to ONE of those blocks - the one whose ripe cell set it off -
            // and to no other. Both halves come from the rules: which cell, and which
            // neighbours actually took the infection.
            GridPos? spreadCentre = enf.LastSpreadCentre;
            var spread = new List<GridPos>(enf.LastSpreadCells);
            for (int i = 0; i < blocks.Count; i++)
            {
                bool owns = spreadCentre.HasValue && Holds(blocks[i], spreadCentre.Value);
                GridPos from = owns ? spreadCentre.Value : RipeCellOf(blocks[i], enf);
                // One sound for the detonation, not one per block: two blocks going on the same
                // beat are one event to the ear.
                PlayInfectionBlock(blocks[i], from, owns ? spread : null,
                    charge * InfectionChargeDelay, i == 0);
            }
        }

        /// <summary>
        /// ONE block's detonation - the call the turn and the animation lab share, so the lab
        /// shows exactly what a turn does.
        ///
        /// It starts NOW, not after the charge, on purpose. The rules destroyed these cubes and
        /// RefreshAll has already painted their cells empty, so anything that waited out the
        /// charge before drawing the block showed it vanish and then come back to explode. The
        /// ghost goes up on this same frame and simply stands there until the charge is done.
        ///
        /// No FlashCells and no ShakeCamera, and both on purpose. The flash was the SHARED
        /// destruction language with a green tint on it - the same squares the dynamite and the
        /// sweeper strike - so three turns of ripening paid off in an effect the player had
        /// already seen a dozen times that round. And a shake says IMPACT, which is the one thing
        /// this is not: nothing hits the board, something inside it gives way.
        /// </summary>
        private void PlayInfectionBlock(List<DestroyedCube> block, GridPos from,
            List<GridPos> spreadTo, float hold, bool sound)
        {
            float rupture = boardView.PlayInfectionBurst(block, from, spreadTo, hold);
            if (sound)
            {
                StartCoroutine(InfectionSoundAt(rupture));
            }
        }

        /// <summary>The sound lands on the rupture, not on the first beat of the charge.</summary>
        private IEnumerator InfectionSoundAt(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            sfx.Explode();
        }

        /// <summary>Reused per detonation - the turn's destruction log by cell.</summary>
        private readonly Dictionary<GridPos, Cube> infectionCubes =
            new Dictionary<GridPos, Cube>();

        private static bool Holds(List<DestroyedCube> block, GridPos cell)
        {
            for (int i = 0; i < block.Count; i++)
            {
                if (block[i].Pos.Equals(cell))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Where a block with no spread ruptures FROM: the infected cell inside it - the one
        /// whose core just charged - so the sickness leaves from the cell the player watched
        /// beat. The rules keep a detonated cell on their infected list, which is what makes
        /// this answerable. The block's middle only if none of its cells is listed.
        /// </summary>
        private static GridPos RipeCellOf(List<DestroyedCube> block, EnfeksiyonJoker enf)
        {
            IReadOnlyList<InfectedCell> infected = enf.InfectedCells;
            for (int i = 0; i < block.Count; i++)
            {
                for (int j = 0; j < infected.Count; j++)
                {
                    if (infected[j].Cell.Equals(block[i].Pos))
                    {
                        return block[i].Pos;
                    }
                }
            }
            return Middle(block);
        }

        /// <summary>The cell nearest a block's middle. Only a fallback for RipeCellOf.</summary>
        private static GridPos Middle(List<DestroyedCube> block)
        {
            float cx = 0f;
            float cy = 0f;
            for (int i = 0; i < block.Count; i++)
            {
                cx += block[i].Pos.X;
                cy += block[i].Pos.Y;
            }
            cx /= block.Count;
            cy /= block.Count;
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < block.Count; i++)
            {
                float dx = block[i].Pos.X - cx;
                float dy = block[i].Pos.Y - cy;
                float d = dx * dx + dy * dy;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return block[best].Pos;
        }

        /// <summary>How much of the core's charge the blast waits out. One means the full
        /// silence-and-two-beats; zero fires it the instant the cubes go, as it used to.</summary>
        private const float InfectionChargeDelay = 1f;

        /// <summary>The group explosion + sound a board power just hit. The bang lands on the
        /// first cell's burst, and the shake comes from the burst itself, sized by how many cells
        /// went.</summary>
        private void PlayPowerBlast(IReadOnlyList<GridPos> cells)
        {
            FlashCells(cells, BlastColor, delegate { sfx.Explode(); });
        }

        // ------------------------------------------------------- dead-end rescue flow

        /// <summary>Called after every action: if the round has paused on a dead end, put the
        /// row/column arrows up; if a quake just brought the board down, play the collapse.</summary>
        private void SyncRescueState()
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null)
            {
                return;
            }
            SyncQuake();

            bool paused = round.Status == RoundStatus.AwaitingRescue;
            if (paused && !lineSwapPicker.IsOpen)
            {
                lineSwapPicker.Show(boardView, OnRescueLinesPicked);
            }
            else if (!paused && lineSwapPicker.IsOpen)
            {
                lineSwapPicker.Hide();
            }
        }

        /// <summary>
        /// "Deprem"'s collapse, from the joker's own report, matched by IDENTITY.
        ///
        /// It used to watch the joker's CollapseCount against a dictionary of counts it had seen,
        /// keyed by instance id, and never cleared - so a new run (whose ids start again at 1)
        /// silently skipped its first quake, and a loaded save could replay an old one. It also
        /// played the wrong thing: dust bursts, the explosion sound and a CAMERA shake, which is
        /// the language of a line clear. A quake is the arena losing its footing; see
        /// QuakeCollapseView. Asked from here and from the repaint, and harmless twice.
        /// </summary>
        private void SyncQuake()
        {
            if (session == null || session.Jokers == null || boardView == null)
            {
                return;
            }
            IReadOnlyList<Joker> jokers = session.Jokers.Jokers;
            for (int i = 0; i < jokers.Count; i++)
            {
                var deprem = jokers[i] as DepremJoker;
                if (deprem != null && deprem.LastQuake != null)
                {
                    boardView.Quake.Play(boardView, deprem.LastQuake);
                }
            }
        }

        /// <summary>Routes clicks to the arrows while the rescue picker is up. Escape gives up
        /// and takes the loss, which is the only other way out of the pause.</summary>
        private void HandleRescuePick(Mouse mouse, Keyboard kb)
        {
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                lineSwapPicker.Hide();
                session.DeclineDeadEndRescue();
                RefreshAll(null);
                return;
            }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                lineSwapPicker.HandleClick(world);
            }
        }

        /// <summary>The player picked two lines: hand them to the rescue power.</summary>
        private void OnRescueLinesPicked(LineAxis axis, int first, int second)
        {
            IReadOnlyList<Power> powers = session.Powers.Powers;
            for (int i = 0; i < powers.Count; i++)
            {
                if (!powers[i].IsDeadEndRescue)
                {
                    continue;
                }
                ActivationTarget target = AimedAtChosenWorld(
                    ActivationTarget.LineSwap(axis, first, second));
                if (session.Powers.TryUse(powers[i].InstanceId, target))
                {
                    sfx.Shuffle();
                    ShakeCamera(0.16f, 0.3f);
                    break;
                }
            }
            RefreshAll(null);
        }
    }
}
