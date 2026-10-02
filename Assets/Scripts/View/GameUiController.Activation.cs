// PURPOSE: GameUiController activation - arming and running player-activated jokers and
// powers, the choice/batak/block-designer pickers, targeting (Powerbank aims at the power
// bar), and the power-blast FX.

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
            RoundEngine round = session.CurrentRound;
            batakBet.Show(Loc.Pick("Batak: how many turns to sweep the board?",
                    "Batak: tahtayı kaç turda temizlersin?"),
                round != null ? round.Board.OccupiedCount : 0);
        }

        /// <summary>True for a power a pointing power ("Powerbank") may be aimed at right now: one
        /// of your OTHER powers, and a spent one. The bar lights exactly these while aiming, and
        /// a click on anything else is not a pick.</summary>
        private bool IsOwnedPowerPick(Power aiming, Power candidate)
        {
            return aiming != null && candidate != null && candidate != aiming && !candidate.Charged;
        }

        /// <summary>
        /// "Powerbank" is aimed at a CARD IN THE POWER BAR - the thing being refilled is right
        /// there on screen, so it is clicked rather than named in a list. Runs ahead of the
        /// drag and retro handlers, so it answers in every play mode. Returns true when it owned
        /// the click.
        ///
        /// A click on a card that cannot take the charge (a charged one) is a MISS, not a cancel:
        /// the player is pointing at the right strip and simply picked the wrong card. A click on
        /// the Powerbank itself puts it down again, and a click anywhere else cancels.
        /// </summary>
        private bool HandleOwnedPowerPick(Mouse mouse)
        {
            if (!pendingTargetPowerId.HasValue || mouse == null
                || !mouse.leftButton.wasPressedThisFrame)
            {
                return false;
            }
            Power aiming = session.Powers.Find(pendingTargetPowerId.Value);
            if (aiming == null || aiming.Targeting != ActivationTargeting.OwnedPower)
            {
                return false;
            }
            int index = powerBar.PowerIndexAt(mouse.position.ReadValue());
            Power picked = index >= 0 && index < session.Powers.Count
                ? session.Powers.Powers[index] : null;
            if (IsOwnedPowerPick(aiming, picked))
            {
                RunPowerActivation(aiming, ActivationTarget.PowerChoice(picked.InstanceId));
                return true;
            }
            if (picked != null && picked != aiming)
            {
                return true; // a charged card: a miss inside the aim, keep aiming
            }
            CancelTargeting();
            return true;
        }

        /// <summary>The four sides, in the order the direction picker lists them.</summary>
        private static readonly Vector2Int[] CompassSides =
        {
            new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0)
        };

        private static string SideName(Vector2Int side)
        {
            if (side.y > 0) return Loc.Pick("UP", "YUKARI");
            if (side.y < 0) return Loc.Pick("DOWN", "AŞAĞI");
            if (side.x < 0) return Loc.Pick("LEFT", "SOLA");
            return Loc.Pick("RIGHT", "SAĞA");
        }

        /// <summary>"Kütleçekim merkezi": asks which way water should fall. A direction is not a
        /// place on the board, so it is asked in a menu - but a COMPASS of four arrows rather than
        /// a list of words, each arrow on the side it names. The way water already falls is shown
        /// in the middle and its own arrow is switched off: picking it would spend the charge on
        /// nothing.</summary>
        private void OpenDirectionPicker(Power power)
        {
            pendingChoice = ChoiceKind.GravityDirection;
            pendingChoiceJokerId = power.InstanceId;
            pendingChoiceValues.Clear();
            RoundEngine round = session.CurrentRound;
            GridPos flow = round != null ? round.MainBoard.WaterFlow : new GridPos(0, -1);
            var current = new Vector2Int(flow.X, flow.Y);
            var labels = new List<string>();
            for (int i = 0; i < CompassSides.Length; i++)
            {
                // The values are packed steps (see UnpackStep), in the same order as the arrows.
                pendingChoiceValues.Add(PackStep(CompassSides[i].x, CompassSides[i].y));
                labels.Add(SideName(CompassSides[i]));
            }
            choicePicker.ShowCompass(power.DisplayName,
                Loc.Pick("Which way should water fall?", "Su hangi yöne aksın?"),
                CompassSides, labels, current,
                Loc.Pick("Water falls ", "Su şu an ") + SideName(current)
                    + Loc.Pick(" now", " akıyor"),
                PadOr(Loc.Pick("click an arrow or press the arrow keys   [Esc] cancel",
                        "bir oka tıkla ya da yön tuşlarına bas   [Esc] vazgeç"),
                    Loc.Pick("choose a side with the stick", "yönü çubukla seç")));
        }

        /// <summary>Puts a card "Cımbız" was turning back as it was and forgets the pick.</summary>
        private void ResetTweezerPreview()
        {
            TweezerCard(tweezerSlot, 0f, 0f);
            tweezerSlot = -1;
            tweezerSteps = 0;
            tweezerCardSpin = 0f;
            tweezerBlockShown = 0f;
            tweezerTurnWanted = 0f;
            tweezerMinis.Clear();
            tweezerMiniHome.Clear();
        }

        /// <summary>The preview's state: the card's own spin (a kick that eases back upright)
        /// and the angle the block is shown at on it.</summary>
        private float tweezerCardSpin;

        private float tweezerBlockShown;

        /// <summary>The angle the preview is heading for, in the order the turns were clicked.
        /// </summary>
        private float tweezerTurnWanted;

        /// <summary>True while the tweezers are setting the card after a confirm.</summary>
        private bool tweezerSetting;

        private readonly List<Transform> tweezerMinis = new List<Transform>();

        private readonly List<Vector3> tweezerMiniHome = new List<Vector3>();

        private Vector3 tweezerMiniCentre;

        /// <summary>Poses the card being turned: the card body at <paramref name="card"/> degrees,
        /// and the block's cubes turned about their own middle so they stand at
        /// <paramref name="block"/> degrees ON SCREEN whatever the card is doing.</summary>
        private void TweezerCard(int slot, float card, float block)
        {
            if (slot < 0 || cardLayer == null)
            {
                return;
            }
            CardVisual v = cardLayer.VisualOfSlot(slot);
            if (v == null)
            {
                return;
            }
            if (tweezerMinis.Count == 0)
            {
                // Taken once, upright: the block's own cubes and where they sit on the card.
                v.transform.localRotation = Quaternion.identity;
                tweezerMiniCentre = Vector3.zero;
                foreach (Transform child in v.transform)
                {
                    if (child.name == "Mini")
                    {
                        tweezerMinis.Add(child);
                        tweezerMiniHome.Add(child.localPosition);
                        tweezerMiniCentre += child.localPosition;
                    }
                }
                if (tweezerMinis.Count > 0)
                {
                    tweezerMiniCentre /= tweezerMinis.Count;
                }
            }
            v.transform.localRotation = Quaternion.Euler(0f, 0f, card);
            // The block turns RELATIVE to the card by whatever keeps it at its on-screen angle.
            Quaternion relative = Quaternion.Euler(0f, 0f, block - card);
            for (int i = 0; i < tweezerMinis.Count; i++)
            {
                if (tweezerMinis[i] == null)
                {
                    continue;
                }
                Vector3 home = tweezerMiniHome[i];
                tweezerMinis[i].localPosition = tweezerMiniCentre + relative * (home - tweezerMiniCentre);
                tweezerMinis[i].localRotation = relative;
            }
        }

        /// <summary>
        /// The two powers that are aimed IN PLACE rather than through a menu. Called every frame
        /// while one is pending; true when it consumed the frame's input.
        ///
        /// "Buldozer": the hover preview is the band the blade will take, and right-click, the
        /// wheel or R switch it between rows and columns - so what you see is what you click.
        /// "Cımbız": once a card is picked, LEFT-click on it (or the wheel, or R) turns its block a
        /// quarter: the card spins with it and eases back upright while the block keeps the new
        /// angle. RIGHT-click (or Enter) confirms, Esc cancels.
        /// </summary>
        private bool HandleLiveAim(Power aiming, Vector2 world, Mouse mouse)
        {
            Keyboard kb = Keyboard.current;
            float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if (aiming is BuldozerPower)
            {
                bool flip = (mouse != null && mouse.rightButton.wasPressedThisFrame)
                    || (kb != null && kb.rKey.wasPressedThisFrame) || Mathf.Abs(wheel) > 0.01f;
                if (flip)
                {
                    bulldozerAxis = bulldozerAxis == LineAxis.Row ? LineAxis.Column : LineAxis.Row;
                    sfx.Pluck(bulldozerAxis == LineAxis.Row ? 1.3f : 1.5f);
                    UpdateHud();
                    return true;
                }
                return false;
            }
            if (aiming is CimbizPower && tweezerSetting)
            {
                return true;
            }
            if (aiming is CimbizPower && tweezerSlot >= 0)
            {
                if ((mouse != null && mouse.rightButton.wasPressedThisFrame)
                    || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)))
                {
                    ConfirmTweezers(aiming);
                    return true;
                }
                CardVisual picked = cardLayer.VisualOfSlot(tweezerSlot);
                bool onPicked = mouse != null && mouse.leftButton.wasPressedThisFrame
                    && cardLayer.CardAt(world) == picked && picked != null;
                int step = onPicked || (kb != null && kb.rKey.wasPressedThisFrame) ? 1
                    : wheel > 0.01f ? 1 : wheel < -0.01f ? -1 : 0;
                if (step != 0)
                {
                    tweezerSteps = ((tweezerSteps + step) % 4 + 4) % 4;
                    sfx.Pluck(1.6f + 0.1f * tweezerSteps);
                    UpdateHud();
                }
                // The PREVIEW: the whole card, block and all, turns to where the block will point.
                // (Counted as it was clicked, so a full lap keeps turning the same way.)
                tweezerTurnWanted += step * -90f;
                tweezerBlockShown = Mathf.Lerp(tweezerBlockShown, tweezerTurnWanted, Mathf.Clamp01(Time.deltaTime * 16f));
                TweezerCard(tweezerSlot, tweezerBlockShown, tweezerBlockShown);
                return step != 0;
            }
            return false;
        }

        /// <summary>Runs "Cımbız" with the turn chosen. The previewed card is put straight first:
        /// the rebuilt card already carries the new shape, and the tweezers play over it.</summary>
        private void ConfirmTweezers(Power power)
        {
            int slot = tweezerSlot;
            int steps = tweezerSteps;
            if (steps == 0 || tweezerSetting)
            {
                if (!tweezerSetting)
                {
                    CancelTargeting();
                    RefreshAll(null);
                }
                return;
            }
            StartCoroutine(SetTweezers(power, slot, steps));
        }

        /// <summary>
        /// THE LOCK-IN. The card and its block stand turned from the preview; now the tweezers
        /// grip the block and turn the CARD back upright underneath it, the block holding exactly
        /// where it is. What is left is an upright card with the block turned - which is the card
        /// the rules are about to hand back, so when the power runs and the card is rebuilt with
        /// its new shape, nothing moves.
        /// </summary>
        private IEnumerator SetTweezers(Power power, int slot, int steps)
        {
            tweezerSetting = true;
            float held = tweezerTurnWanted;
            CardVisual v = cardLayer.VisualOfSlot(slot);
            if (v != null)
            {
                powerFx.PlayTweezerGrip(v, CardLayerView.HandFrontOrder + 5, 0.42f);
            }
            sfx.Pluck(2.2f);
            // Settle the preview first, if it was still easing in.
            float t = 0f;
            float from = tweezerBlockShown;
            while (t < 0.08f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.08f);
                float a = Mathf.Lerp(from, held, k);
                TweezerCard(slot, a, a);
                yield return null;
            }
            // The card turns back upright under the block.
            t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.3f);
                float e = 1f - (1f - k) * (1f - k) * (1f - k);
                TweezerCard(slot, Mathf.Lerp(held, 0f, e), held);
                yield return null;
            }
            sfx.Drum(1.8f);
            tweezerSetting = false;
            ResetTweezerPreview();
            RunPowerActivation(power, new ActivationTarget(slot, null, null, null, null, false, null,
                new GridPos(steps, 0), null));
        }

        /// <summary>The compass option an arrow key (or WASD) pressed this frame points at, or -1.
        /// </summary>
        private int CompassKeyPressed(Keyboard kb)
        {
            if (kb == null)
            {
                return -1;
            }
            Vector2Int side = Vector2Int.zero;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
            {
                side = new Vector2Int(0, 1);
            }
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
            {
                side = new Vector2Int(0, -1);
            }
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
            {
                side = new Vector2Int(-1, 0);
            }
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
            {
                side = new Vector2Int(1, 0);
            }
            return side == Vector2Int.zero ? -1 : choicePicker.OptionInDirection(side);
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
            // THE ONE YOU ARE WORKING ON, first: testing a boss means starting the same stage over
            // and over, and it should not cost three pages of "more..." each time.
            int again = LastDebugBossIndex();
            if (again >= 0)
            {
                pendingChoiceValues.Add(BossPickerAgain);
                labels.Add(Loc.Pick("AGAIN: ", "TEKRAR: ") + all[again].DisplayName
                    + Loc.Pick("   (shift+G)", "   (shift+G)"));
            }
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
                // the pages wrap both ways, so the last page is one step BACK from the first
                pendingChoiceValues.Add(BossPickerBack);
                labels.Add(Loc.Pick("back...", "geri..."));
            }
            pendingChoiceValues.Add(BossPickerRandom);
            labels.Add(Loc.Pick("RANDOM (draw one normally)", "RASTGELE (normal çekim)"));
            choicePicker.Show(Loc.Pick("DEBUG: start a boss stage",
                "DEBUG: patron sahnesi başlat"), labels);
        }

        private const int BossPickerPageSize = 10;
        private const int BossPickerMore = -2;
        private const int BossPickerRandom = -1;
        private const int BossPickerBack = -3;
        private const int BossPickerAgain = -4;

        /// <summary>The boss the debug picker last started, kept across sessions.</summary>
        private const string LastDebugBossKey = "debug_last_boss";

        private static int LastDebugBossIndex()
        {
            string defId = PlayerPrefs.GetString(LastDebugBossKey, string.Empty);
            if (string.IsNullOrEmpty(defId))
            {
                return -1;
            }
            IReadOnlyList<BossDefinition> all = BossRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].DefId == defId)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>[shift+G]: the boss stage the picker last started, again, with no picker.
        /// False when nothing has been picked yet (the caller opens the picker instead).</summary>
        private bool DebugRestartLastBoss()
        {
            int again = LastDebugBossIndex();
            return again >= 0 && ResolveBossPick(again);
        }

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
            if (pendingChoice == ChoiceKind.CardElement)
            {
                ResolveAlchemyChoice(pendingChoiceValues[index]);
                RefreshAll(null);
                return true;
            }
            var ctx = new RoundContext(session, session.Rng, session.CurrentRound);
            if (pendingChoice == ChoiceKind.GravityDirection)
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
            if (value == BossPickerMore || value == BossPickerBack)
            {
                bossPickerPage += value == BossPickerMore ? 1 : -1;
                OpenBossPicker(); // wraps at either end
                return false;     // still pending - the picker is open again
            }
            if (value == BossPickerAgain)
            {
                value = LastDebugBossIndex();
            }
            IReadOnlyList<BossDefinition> all = BossRegistry.All;
            string defId = value >= 0 && value < all.Count ? all[value].DefId : null;
            if (defId != null)
            {
                PlayerPrefs.SetString(LastDebugBossKey, defId);
            }
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
            ResetTweezerPreview();
            // The workshop powers' own pick, and whichever modal it had open.
            workshopPowerId = null;
            workshopFirstCard = -1;
            SecondWorkshopCard = -1;
            workshopDonorCell = null;
            workshopPressAnchor = null;
            geneRefusal = null;
            nesterRefusal = null;
            if (playSwapPowerId.HasValue)
            {
                playSwapPowerId = null;
                lineSwapPicker.Hide();
            }
            HideCardMarks();
            cubePicker.Hide();
            nesterEditor.Hide();
            weldPicker.Hide();
            ClearWindAim();
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
                SyncHazine(round, null);
                return;
            }
            if (replacedCardId >= 0 && round.Status != RoundStatus.Lost)
            {
                cardLayer.AnimateReplaceCard(round, replacedCardId);
                boardView.Refresh();
                UpdateHud();
                jokerBar.Refresh(session, null);
                SyncHazine(round, null);
                return;
            }
            RefreshAll(null);
            // "Hazine": a mark the activation blew open is revealed now, not at the next turn.
            SyncHazine(round, null);
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
            // "Powerbank" falls through to the ordinary aim below: it waits for a click on a
            // spent power's card in the bar (HandleOwnedPowerPick). CanBeginUse has already
            // refused it when nothing is spent, so arming it always has something to point at.
            // TWO WHOLE LINES ("Kentsel Dönüşüm" used in normal play): the same row/column arrows
            // the dead-end rescue puts up, answered through the normal power path.
            if (power.Targeting == ActivationTargeting.LineSwap)
            {
                pendingTargetPowerId = power.InstanceId; // lights the bar and the HUD line
                playSwapPowerId = power.InstanceId;
                lineSwapPicker.Show(boardView, OnPlaySwapPicked);
                UpdateHud();
                powerBar.Refresh(session, pendingTargetPowerId);
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

            // "Gen nakli" is block to block - a board block, then a card OR another board block -
            // so it has a flow of its own rather than the hand-card tail below.
            if (power.Targeting == ActivationTargeting.CellAndHandCard)
            {
                return HandleGeneClick(power, round, world);
            }

            // The cut editor reads the pointer itself (painting needs the frames between press and
            // release) - its answer is polled in PollNesterEditor, so a click here is its own.
            if (nesterEditor.IsOpen)
            {
                return true;
            }
            if (weldPicker.IsOpen)
            {
                WeldPickerView.ClickResult result = weldPicker.ClickAt(world);
                if (result == WeldPickerView.ClickResult.Confirm)
                {
                    ConfirmWeld(power);
                }
                else if (result == WeldPickerView.ClickResult.Cancel)
                {
                    CancelTargeting();
                }
                return true; // a miss inside the editor is just a miss
            }

            // Everything else is waiting for a hand card.
            CardVisual hit = cardLayer.CardAt(world);
            if (hit == null || hit.SlotIndex < 0 || hit.SlotIndex >= round.Hand.Count)
            {
                CancelTargeting();
                return true;
            }
            if (power.Targeting == ActivationTargeting.CardCubes)
            {
                BlockCard toCut = round.Hand[hit.SlotIndex];
                BlockShape cutShape = round.EffectiveShape(toCut);
                if (!NesterPower.CanBeCut(toCut))
                {
                    nesterRefusal = toCut.IsWelded
                        ? Loc.Pick("a welded block cannot be cut", "lehimli blok kesilemez")
                        : Loc.Pick("this special block cannot be cut", "bu özel blok kesilemez");
                    UpdateHud();
                    return true; // keep picking
                }
                if (cutShape.Size < 2)
                {
                    nesterRefusal = Loc.Pick("a single cube cannot be cut", "tek küp kesilemez");
                    UpdateHud();
                    return true;
                }
                nesterRefusal = null;
                workshopFirstCard = hit.SlotIndex;
                HideCardMarks();
                nesterEditor.Show(round.Hand[hit.SlotIndex], cutShape, power.DisplayName);
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
            HideCardMarks();
            BlockCard firstCard = round.Hand[workshopFirstCard];
            BlockCard secondCard = round.Hand[hit.SlotIndex];
            weldPicker.Show(firstCard, round.EffectiveShape(firstCard), secondCard,
                round.EffectiveShape(secondCard), power.DisplayName);
            return true;
        }

        /// <summary>Every frame the cut editor is up: CUT runs the power with the painted piece,
        /// CANCEL puts the whole pick down.</summary>
        private void PollNesterEditor(Power power)
        {
            NesterEditorView.Result result = nesterEditor.TakeResult();
            if (result == NesterEditorView.Result.Cancel)
            {
                CancelTargeting();
            }
            else if (result == NesterEditorView.Result.Confirm)
            {
                ActivationTarget target = ActivationTarget.CardCubes(workshopFirstCard,
                    nesterEditor.PickedCells);
                nesterEditor.Hide();
                workshopPowerId = null;
                bool wasCharged = power.Charged;
                RunPowerActivation(power, target);
                if (wasCharged && !power.Charged)
                {
                    sfx.Cut(); // the blade went through
                }
            }
        }

        /// <summary>The weld editor's WELD: the placement the player laid, through the normal
        /// power path.</summary>
        private void ConfirmWeld(Power power)
        {
            if (!weldPicker.PlacedOffset.HasValue)
            {
                return;
            }
            ActivationTarget target = ActivationTarget.TwoCards(workshopFirstCard,
                SecondWorkshopCard, weldPicker.PlacedOffset.Value);
            weldPicker.Hide();
            workshopPowerId = null;
            RunPowerActivation(power, target);
        }

        /// <summary>Keys inside the weld editor: arrows nudge the laid block, Enter welds.</summary>
        private void HandleWeldKeys(Power power)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !weldPicker.IsOpen)
            {
                return;
            }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                ConfirmWeld(power);
                return;
            }
            int dx = kb.leftArrowKey.wasPressedThisFrame ? -1 : kb.rightArrowKey.wasPressedThisFrame ? 1 : 0;
            int dy = kb.downArrowKey.wasPressedThisFrame ? -1 : kb.upArrowKey.wasPressedThisFrame ? 1 : 0;
            if (dx != 0 || dy != 0)
            {
                weldPicker.Nudge(dx, dy);
            }
        }

        // ------------------------------------------------------- hand-card pick marks
        //
        // While a workshop power is picking cards out of the hand ("Lehimleme", "Neşter"), the card
        // under the pointer is outlined and breathes, and a card already picked keeps a steady
        // outline - the same language the board uses for a block being aimed at. Drawn as four
        // bars over the card rather than on it, following the card's own transform every frame,
        // so the hand's hover lift carries the outline with it.

        private static readonly Color CardMarkHover = new Color(1f, 0.82f, 0.42f, 0.95f);
        private static readonly Color CardMarkRefused = new Color(1f, 0.35f, 0.35f, 0.95f);

        /// <summary>Why the last Neşter card pick was refused, shown under the step; null when
        /// there is nothing to say.</summary>
        private string nesterRefusal;
        private static readonly Color CardMarkPicked = new Color(0.45f, 0.85f, 1f, 0.9f);
        private readonly List<SpriteRenderer> cardMarks = new List<SpriteRenderer>();

        /// <summary>Every frame of a workshop pick: which hand card to outline, if any.</summary>
        private void SyncCardMarks(Vector2 world)
        {
            Power power = workshopPowerId.HasValue ? session.Powers.Find(workshopPowerId.Value) : null;
            RoundEngine round = session.CurrentRound;
            bool picking = power != null && round != null && !weldPicker.IsOpen
                && !nesterEditor.IsOpen
                && (power.Targeting == ActivationTargeting.TwoHandCards
                    || power.Targeting == ActivationTargeting.CardCubes);
            if (!picking)
            {
                HideCardMarks();
                return;
            }
            CardVisual hovered = cardLayer.CardAt(world);
            if (hovered != null && (hovered.SlotIndex < 0 || hovered.SlotIndex >= round.Hand.Count
                || hovered.SlotIndex == workshopFirstCard))
            {
                hovered = null;
            }
            CardVisual picked = workshopFirstCard >= 0 ? cardLayer.VisualOfSlot(workshopFirstCard)
                : null;
            float breath = 0.65f + 0.35f * Mathf.Sin(Time.time * 5f);
            int used = 0;
            used = OutlineCard(picked, CardMarkPicked, used);
            // Neşter: a card it cannot cut is outlined in the refusal red rather than invited.
            bool refused = hovered != null && power.Targeting == ActivationTargeting.CardCubes
                && (!NesterPower.CanBeCut(round.Hand[hovered.SlotIndex])
                    || round.EffectiveShape(round.Hand[hovered.SlotIndex]).Size < 2);
            Color hover = refused ? CardMarkRefused : CardMarkHover;
            hover.a *= breath;
            used = OutlineCard(hovered, hover, used);
            for (int i = used; i < cardMarks.Count; i++)
            {
                cardMarks[i].enabled = false;
            }
        }

        private int OutlineCard(CardVisual card, Color color, int used)
        {
            if (card == null)
            {
                return used;
            }
            Vector3 at = card.transform.position;
            float scale = card.transform.lossyScale.x;
            float w = CardVisual.BodyWidth * scale;
            float h = CardVisual.BodyHeight * scale;
            float line = 0.06f * scale;
            float pad = 0.05f * scale;
            var bars = new[]
            {
                new Vector4(0f, h * 0.5f + pad, w + pad * 2f + line, line),
                new Vector4(0f, -h * 0.5f - pad, w + pad * 2f + line, line),
                new Vector4(-w * 0.5f - pad, 0f, line, h + pad * 2f + line),
                new Vector4(w * 0.5f + pad, 0f, line, h + pad * 2f + line)
            };
            for (int i = 0; i < bars.Length; i++)
            {
                if (used == cardMarks.Count)
                {
                    SpriteRenderer made = ViewUtil.MakeRounded(transform, "CardPickMark",
                        Vector2.zero, Vector2.one, color, CardLayerView.HandFrontOrder + 3);
                    cardMarks.Add(made);
                }
                SpriteRenderer bar = cardMarks[used++];
                bar.transform.position = new Vector3(at.x + bars[i].x, at.y + bars[i].y, 0f);
                bar.size = new Vector2(bars[i].z, bars[i].w);
                bar.color = color;
                bar.enabled = true;
            }
            return used;
        }

        private void HideCardMarks()
        {
            for (int i = 0; i < cardMarks.Count; i++)
            {
                if (cardMarks[i] != null)
                {
                    cardMarks[i].enabled = false;
                }
            }
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

        // ------------------------------------------------------------------ "Gen nakli"
        //
        // Two picks, both of WHOLE BLOCKS: the block the element leaves, then the block that
        // takes it - a card in the hand, or a plain block on the board. Every "which block is
        // this" and "may it take this" answer is GenNakliPower's own (BlockAt, GeneOfBlock,
        // CanTakeIntoCard, CanTakeOnBoard), so the block lit under the cursor is exactly the block
        // the rules will move. A pick the rules would refuse is a MISS with a reason on the HUD,
        // never a silent cancel: the player is mid-aim and just picked the wrong thing.
        // The main world only - a gene never crosses into "Öteki dünya"'s mirror.

        /// <summary>Why the last Gen nakli pick was refused, shown under the step; null when
        /// there is nothing to say. Cleared whenever the aim moves on or ends.</summary>
        private string geneRefusal;

        private bool HandleGeneClick(Power power, RoundEngine round, Vector2 world)
        {
            GridPos cell;
            if (boardView.TryWorldToCell(world, out cell))
            {
                if (!workshopDonorCell.HasValue)
                {
                    TryGeneDonor(round, cell);
                }
                else
                {
                    TryGeneOntoBoard(power, round, cell);
                }
                return true;
            }
            CardVisual hit = cardLayer.CardAt(world);
            if (hit != null && workshopDonorCell.HasValue)
            {
                TryGeneIntoCard(power, round, hit.SlotIndex);
                return true;
            }
            CancelTargeting();
            return true;
        }

        /// <summary>First pick: the block the element leaves.</summary>
        private void TryGeneDonor(RoundEngine round, GridPos cell)
        {
            GameBoard board = round.MainBoard;
            if (!board.GetCube(cell).HasValue)
            {
                geneRefusal = Loc.Pick("that cell is empty - pick a block", "o kare boş - bir blok seç");
            }
            else if (!GenNakliPower.GeneOfBlock(board, cell).HasValue)
            {
                geneRefusal = Loc.Pick("that block has no element to give",
                    "o bloğun verecek elementi yok");
            }
            else
            {
                workshopDonorCell = cell;
                geneRefusal = null;
            }
            UpdateHud();
        }

        /// <summary>Second pick on the board: another block takes the element - or, clicking the
        /// chosen block again, the first pick is put back.</summary>
        private void TryGeneOntoBoard(Power power, RoundEngine round, GridPos cell)
        {
            GameBoard board = round.MainBoard;
            GridPos donor = workshopDonorCell.Value;
            BlockElement? gene = GenNakliPower.GeneOfBlock(board, donor);
            if (!gene.HasValue)
            {
                CancelTargeting(); // the chosen block is gone from under the aim
                return;
            }
            if (Contains(GenNakliPower.BlockAt(board, donor), cell))
            {
                workshopDonorCell = null; // the same block again: un-pick it
                geneRefusal = null;
                UpdateHud();
                return;
            }
            if (GenNakliPower.CanTakeOnBoard(round, donor, cell, gene.Value))
            {
                FireGene(power, ActivationTarget.CellToCell(donor, cell));
                return;
            }
            Cube? cube = board.GetCube(cell);
            geneRefusal = !cube.HasValue
                ? Loc.Pick("that cell is empty - pick a block or a card",
                    "o kare boş - bir blok ya da kart seç")
                : gene.Value == BlockElement.Dynamite
                    ? Loc.Pick("dynamite only goes into a card", "dinamit sadece karta aktarılır")
                : cube.Value.Kind != CubeKind.Normal
                    ? Loc.Pick("that block already has an element", "o bloğun zaten elementi var")
                    : Loc.Pick("that block is broken - it has lost a cube",
                        "o blok kırık - bir küpü eksik");
            UpdateHud();
        }

        /// <summary>Second pick in the hand: the card takes the element, on loan.</summary>
        private void TryGeneIntoCard(Power power, RoundEngine round, int slot)
        {
            if (slot < 0 || slot >= round.Hand.Count)
            {
                geneRefusal = Loc.Pick("only a card in your hand can take it",
                    "sadece elindeki bir kart alabilir");
            }
            else if (!GenNakliPower.CanTakeIntoCard(round, slot))
            {
                geneRefusal = Loc.Pick("that card already has an element",
                    "o kartın zaten elementi var");
            }
            else
            {
                FireGene(power, ActivationTarget.CellAndCard(workshopDonorCell.Value, slot));
                return;
            }
            UpdateHud();
        }

        private void FireGene(Power power, ActivationTarget target)
        {
            workshopPowerId = null;
            workshopDonorCell = null;
            geneRefusal = null;
            boardView.ClearPreview();
            RunPowerActivation(power, target);
        }

        /// <summary>
        /// The Gen nakli aim, drawn every frame: the chosen block held steady in its element's
        /// colour, and the WHOLE block under the cursor breathing - in the element's colour when
        /// the rules would take it (for a block that is about to receive, that is the colour it
        /// is about to become), in the refusal red when they would not.
        /// </summary>
        private void ShowGeneHover(Vector2 world)
        {
            Power aiming = workshopPowerId.HasValue ? session.Powers.Find(workshopPowerId.Value) : null;
            RoundEngine round = session.CurrentRound;
            if (aiming == null || round == null
                || aiming.Targeting != ActivationTargeting.CellAndHandCard)
            {
                return;
            }
            GameBoard board = round.MainBoard;
            IReadOnlyList<GridPos> held = null;
            Color heldColor = Color.clear;
            BlockElement? gene = null;
            if (workshopDonorCell.HasValue)
            {
                gene = GenNakliPower.GeneOfBlock(board, workshopDonorCell.Value);
                if (gene.HasValue)
                {
                    held = GenNakliPower.BlockAt(board, workshopDonorCell.Value);
                    heldColor = GeneTint(gene.Value, 0.5f);
                }
            }
            IReadOnlyList<GridPos> hovered = null;
            Color hoveredColor = Color.clear;
            GridPos cell;
            if (boardView.TryWorldToCell(world, out cell) && board.GetCube(cell).HasValue)
            {
                hovered = GenNakliPower.BlockAt(board, cell);
                if (!gene.HasValue)
                {
                    BlockElement? offered = GenNakliPower.GeneOfBlock(board, cell);
                    hoveredColor = offered.HasValue ? GeneTint(offered.Value, 0.7f)
                        : BoardView.RefusedPreviewColor;
                }
                else if (Contains(held, cell))
                {
                    hovered = null; // the chosen block itself: it is already lit
                }
                else
                {
                    // Red for every block the rules would refuse - one with an element already,
                    // and one that has lost even a single cube (GenNakliPower.IsIntact).
                    hoveredColor = GenNakliPower.CanTakeOnBoard(round, workshopDonorCell.Value,
                            cell, gene.Value)
                        ? GeneTint(gene.Value, 0.75f)
                        : BoardView.RefusedPreviewColor;
                }
            }
            if (held == null && hovered == null)
            {
                boardView.ClearPreview();
                return;
            }
            boardView.ShowBlockHighlight(hovered, hoveredColor, held, heldColor);
        }

        /// <summary>An element's colour as a preview tint: lifted a little toward white so the
        /// dark ones (obsidian) still read over a dark board, at the given strength.</summary>
        private static Color GeneTint(BlockElement gene, float alpha)
        {
            Color c = Color.Lerp(ViewUtil.ElementColor(gene), Color.white, 0.25f);
            c.a = alpha;
            return c;
        }

        private static bool Contains(IReadOnlyList<GridPos> cells, GridPos cell)
        {
            if (cells == null)
            {
                return false;
            }
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].Equals(cell))
                {
                    return true;
                }
            }
            return false;
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
            // A power aimed at another POWER carries that power's instance id in HandIndex
            // (ActivationTarget.PowerChoice) - it names no card.
            bool aimedAtPower = power.Targeting == ActivationTargeting.OwnedPower;
            if (!aimedAtPower && target.HandIndex.HasValue && round != null
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
            // What the activation effects need from BEFORE the power runs: İkinci Şans wipes the
            // board, so its cubes' faces are taken now; Soğuk Füzyon adds bonus cards, so the ones
            // already there are remembered to tell the new ones apart.
            List<PowerFxView.CubeFace> slateFaces = power.DefId == "ikinci_sans"
                ? CaptureCubeFaces(round) : null;
            bool copiesOut = power.DefId == "asirma" || power.DefId == "yedekleme";
            var rod = power as OltaPower;
            HashSet<int> bonusBefore = power.DefId == "soguk_fuzyon" || copiesOut || rod != null
                ? BonusCardIds(round) : null;
            // "Olta": which pile the marked card is reeled out of, known only BEFORE the pull.
            bool reelFromDiscard = rod != null && rod.MarkedCardId.HasValue && round != null
                && PileHolds(round.Deck.DiscardPile, rod.MarkedCardId.Value);
            bool discardHadCards = round != null && round.Deck.DiscardCount > 0;
            // The board strikes need the faces of what they are about to destroy.
            bool strikes = power.DefId == "caprazlama" || power.DefId == "cerceve"
                || power.DefId == "buldozer" || power.DefId == "eko" || power.DefId == "mayin";
            List<PowerFxView.CubeFace> strikeFaces = strikes ? CaptureCellFaces(false) : null;
            // "Eko": a use WITH a memory is the replay (even when the remembered cells stand empty
            // now and nothing breaks); without one it is the arm. Its cells, before Run forgets them.
            var echo = power as EkoPower;
            List<GridPos> echoCells = echo != null && echo.HasMemory ? new List<GridPos>(echo.Memory) : null;
            // "Klon": where the card being cloned sits.
            Vector2? cloneFrom = null;
            if (power.DefId == "klon" && target.HandIndex.HasValue)
            {
                CardVisual source = cardLayer.VisualOfSlot(target.HandIndex.Value);
                if (source != null)
                {
                    cloneFrom = source.HomePosition;
                }
            }
            HashSet<int> bonusBeforeClone = power.DefId == "klon" ? BonusCardIds(round) : null;
            // "Kum Saati" and "Bardağın Boş Tarafı" animate FROM the board as it is shown now.
            List<PowerFxView.CubeFace> boardBefore = power.DefId == "kum_saati"
                || power.DefId == "bardagin_bos_tarafi" ? CaptureCellFaces(true) : null;
            // "Hızlı Çekim Şarjörü": how many cards it fires, and how many come back.
            int drawBefore = round != null ? round.Deck.DrawCount : 0;
            int discardBefore = round != null ? round.Deck.DiscardCount : 0;
            // "Hologram": where the card is, before it leaves the hand.
            Vector2? holoAt = null;
            if (power.DefId == "hologram" && target.HandIndex.HasValue)
            {
                CardVisual holo = cardLayer.VisualOfSlot(target.HandIndex.Value);
                if (holo != null)
                {
                    holoAt = holo.transform.position;
                }
            }
            // The inflations replace the board: the one on screen and its cell size are what the
            // resize animates FROM.
            GameBoard shownBefore = boardView != null ? boardView.Board : null;
            float cellBefore = boardView != null ? boardView.CellWorldSize : 1f;
            // "Rüzgar": the faces of everything in the lane, before the embers turn some to fire.
            Dictionary<GridPos, WindGustView.Face> windFaces = power is RuzgarPower
                ? CaptureWindFaces(target) : null;
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
            // "İKİNCİ ŞANS": a clean slate, not a demolition - the cubes dissolve into gold and the
            // hand is dealt afresh with the redraw animation. The Renovasyon path's shape: the
            // hand is NOT resynced first, or the redraw would fly the NEW hand to the discard.
            if (power.DefId == "ikinci_sans" && round != null && slateFaces != null)
            {
                sfx.Shuffle();
                cardLayer.AnimateRedraw(round);
                boardView.Refresh();
                UpdateHud();
                jokerBar.Refresh(session, null);
                powerBar.Refresh(session, null);
                powerFx.PlayCleanSlate(slateFaces, MainBoardCenter, boardView.CubeWorldSize,
                    MainBoardWorldSize);
                return;
            }
            // Powers can rewrite the board (inflations replace it wholesale, Kum saati
            // rewinds it) and the piles - a full resync covers every one of them.
            RefreshAll(null);
            // "RÜZGAR": the gust, its embers and spores, the board's own water animation for the
            // push and the fall, and only then any line it completed.
            var wind = power as RuzgarPower;
            if (wind != null && PlayWind(wind, target, windFaces))
            {
                return;
            }
            // THE BOARD STRIKES: each plays its own lead-in, then its cubes go.
            if (strikes && strikeFaces != null && PlayStrike(power, target, strikeFaces, blastCells, echoCells))
            {
                return;
            }
            // "CIMBIZ": tweezers twist the card.
            if (power.DefId == "cimbiz" && target.HandIndex.HasValue)
            {
                return; // the tweezers already turned it (SetTweezers), before the rules ran
            }
            // "KLON": two copies peel off the chosen card.
            if (power.DefId == "klon" && cloneFrom.HasValue && bonusBeforeClone != null)
            {
                var clones = new List<CardVisual>();
                for (int i = 0; i < round.BonusHand.Count; i++)
                {
                    if (!bonusBeforeClone.Contains(round.BonusHand[i].Card.Id))
                    {
                        CardVisual v = cardLayer.VisualOfSlot(round.Hand.Count + i);
                        if (v != null)
                        {
                            clones.Add(v);
                        }
                    }
                }
                if (clones.Count > 0)
                {
                    powerFx.PlayCopyOut(cardLayer.transform, clones, cloneFrom.Value,
                        new Color(0.5f, 1f, 0.9f), CardLayerView.HandFrontOrder + 5, delegate { sfx.Fusion(); });
                    return;
                }
            }
            // "BÜYÜTEÇ": a magnifying glass over the draw pile.
            if (power.DefId == "buyutec")
            {
                powerFx.PlayMagnifier(cardLayer.transform.TransformPoint(CardLayerView.DrawPilePos),
                    CardVisual.BodyWidth * cardLayer.transform.lossyScale.x * UiLayout.Active.PileScale * 1.1f,
                    CardLayerView.HandFrontOrder + 5);
                return;
            }
            // "TRANSFER": the two top cards trade places.
            if (power.DefId == "transfer")
            {
                float s = cardLayer.transform.lossyScale.x * UiLayout.Active.PileScale;
                powerFx.PlaySwapPiles(cardLayer.transform.TransformPoint(CardLayerView.DrawPilePos),
                    cardLayer.transform.TransformPoint(CardLayerView.DiscardPilePos),
                    new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight) * s, CardLayerView.HandFrontOrder + 5);
                return;
            }
            // "ÖTEKİ DÜNYA": the world beneath opens.
            if (power.DefId == "oteki_dunya")
            {
                powerFx.PlayMirror(MainBoardCenter, MainBoardWorldSize);
            }
            // "KUM SAATİ": time runs back over the board.
            if (power.DefId == "kum_saati" && boardBefore != null)
            {
                PlayRewind(boardBefore);
                return;
            }
            // "BARDAĞIN BOŞ TARAFI": every cell turns over; the lines it made go off after.
            if (power.DefId == "bardagin_bos_tarafi" && boardBefore != null)
            {
                PlayInvert(boardBefore, round);
                return;
            }
            // "HIZLI ÇEKİM ŞARJÖRÜ": the draw pile is fired into the discard and reloaded.
            if (power.DefId == "hizli_cekim_sarjoru")
            {
                powerFx.PlayMagazine(cardLayer.transform, CardLayerView.DrawPilePos,
                    CardLayerView.DiscardPilePos, drawBefore, drawBefore + discardBefore,
                    UiLayout.Active.PileScale, CardLayerView.HandFrontOrder + 5);
                return;
            }
            // "HOLOGRAM": the card turns to light and streams into the discard.
            if (power.DefId == "hologram" && holoAt.HasValue)
            {
                powerFx.PlayHologram(holoAt.Value,
                    cardLayer.transform.TransformPoint(CardLayerView.DiscardPilePos),
                    cardLayer.transform.lossyScale.x, CardLayerView.HandFrontOrder + 5);
                return;
            }
            // THE INFLATIONS: the arena unfolds its new bands rather than snapping to a new size.
            if (power is InflationPower && boardView.Board != shownBefore)
            {
                powerFx.PlayBoardResize(boardView, shownBefore, cellBefore, null);
            }
            // "TILSIM": the charm is summoned while TalismanView harvests the ghosts.
            var talisman = power as TilsimPower;
            if (talisman != null && talisman.LastActivation != null)
            {
                PlayTalismanCharm(talisman.LastActivation);
            }
            // "Powerbank": the power it refilled FILLS, rather than simply being drawn charged.
            // AFTER RefreshAll, always - the fill uncovers a card the bar has already painted
            // charged (see PowerBarView.RefuelPower).
            if (aimedAtPower && target.HandIndex.HasValue)
            {
                powerBar.RefuelPower(target.HandIndex.Value, 0f, false);
            }
            if (session.Phase == GamePhase.Market)
            {
                // "Totem" ends overtime and advances straight to the market mid-use; mirror the
                // normal advance flow (RefreshAll + Show) so the market actually appears - AFTER
                // the totem has risen, blessed the arena and sunk away again.
                if (power.DefId == "totem")
                {
                    powerFx.PlayTotem(MainBoardCenter, MainBoardWorldSize,
                        delegate { marketView.Show(session); });
                    return;
                }
                marketView.Show(session);
            }
            // "SOĞUK FÜZYON": the two new bonus cards fly out of their piles and meet.
            // "OLTA": the card is reeled up out of its pile on a line.
            if (bonusBefore != null && rod != null)
            {
                if (PlayReel(round, bonusBefore, reelFromDiscard))
                {
                    return;
                }
                bonusBefore = null;
            }
            if (bonusBefore != null && !copiesOut && PlayColdFusion(round, bonusBefore, discardHadCards))
            {
                return;
            }
            // "AŞIRMA" / "YEDEKLEME": one card lifted off its pile splits into the two copies.
            if (power.DefId == "bukulme")
            {
                PlayBukulmeCopy(round);
                return;
            }
            if (bonusBefore != null && copiesOut && PlayCopyOut(round, bonusBefore, power.DefId == "asirma"))
            {
                return;
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
            // "Hazine": a mark the power blew open is revealed as the blast breaks it.
            if (session.Phase == GamePhase.Round)
            {
                SyncHazine(round, null);
            }
        }

        /// <summary>Every cube on the main board with the face it is wearing right now - what the
        /// clean slate dissolves once the rules have already emptied the board.</summary>
        private List<PowerFxView.CubeFace> CaptureCubeFaces(RoundEngine round)
        {
            var faces = new List<PowerFxView.CubeFace>();
            GameBoard board = boardView != null ? boardView.Board : null;
            if (board == null)
            {
                return faces;
            }
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                for (int y = board.MinY; y < board.MinY + board.Height; y++)
                {
                    var pos = new GridPos(x, y);
                    Sprite tile;
                    Color colour;
                    if (board.GetCube(pos).HasValue && boardView.TryCubeLook(pos, 0f, out tile, out colour))
                    {
                        faces.Add(new PowerFxView.CubeFace
                        {
                            World = boardView.CellToWorld(pos),
                            Tile = tile,
                            Colour = colour
                        });
                    }
                }
            }
            return faces;
        }

        private static HashSet<int> BonusCardIds(RoundEngine round)
        {
            var ids = new HashSet<int>();
            if (round != null)
            {
                for (int i = 0; i < round.BonusHand.Count; i++)
                {
                    ids.Add(round.BonusHand[i].Card.Id);
                }
            }
            return ids;
        }

        /// <summary>The bonus cards Soğuk Füzyon just made, flown in from the piles they were
        /// copied from: the discard's copy first (the power adds it first), then the draw pile's.
        /// False when nothing new is on screen to animate.</summary>
        private bool PlayColdFusion(RoundEngine round, HashSet<int> before, bool discardHadCards)
        {
            var cards = new List<CardVisual>();
            var piles = new List<Vector2>();
            for (int i = 0; i < round.BonusHand.Count; i++)
            {
                if (before.Contains(round.BonusHand[i].Card.Id))
                {
                    continue;
                }
                CardVisual visual = cardLayer.VisualOfSlot(round.Hand.Count + i);
                if (visual == null)
                {
                    continue;
                }
                bool fromDiscard = cards.Count == 0 && discardHadCards;
                cards.Add(visual);
                piles.Add(fromDiscard ? CardLayerView.DiscardPilePos : CardLayerView.DrawPilePos);
            }
            if (cards.Count == 0)
            {
                return false;
            }
            powerFx.PlayColdFusion(cardLayer.transform, cards, piles, CardLayerView.HandFrontOrder + 5,
                delegate { sfx.Fusion(); });
            return true;
        }

        /// <summary>The bonus cards Aşırma / Yedekleme just made, lifted off the pile they were
        /// copied from (the draw pile for Aşırma, the discard for Yedekleme) and split in two.
        /// False when nothing new is on screen to animate.</summary>
        private bool PlayCopyOut(RoundEngine round, HashSet<int> before, bool fromDraw)
        {
            var cards = new List<CardVisual>();
            for (int i = 0; i < round.BonusHand.Count; i++)
            {
                if (before.Contains(round.BonusHand[i].Card.Id))
                {
                    continue;
                }
                CardVisual visual = cardLayer.VisualOfSlot(round.Hand.Count + i);
                if (visual != null)
                {
                    cards.Add(visual);
                }
            }
            if (cards.Count == 0)
            {
                return false;
            }
            powerFx.PlayCopyOut(cardLayer.transform, cards,
                fromDraw ? CardLayerView.DrawPilePos : CardLayerView.DiscardPilePos,
                fromDraw ? PowerFxView.StealColour : PowerFxView.BackupColour,
                CardLayerView.HandFrontOrder + 5, delegate { sfx.Fusion(); });
            return true;
        }

        private BukulmeCopyVisuals lastBukulmeCopy;

        /// <summary>
        /// "Bükülme": a copy of the marked card split off it - on activation and every time the
        /// marked card comes back into the hand. The copy flies out of the MARKED CARD itself in
        /// warp violet (the same copy-out Aşırma and Yedekleme use off their piles), so the player
        /// sees which card is doing the multiplying. Called after every repaint; matched by
        /// identity, so a repaint never replays it.
        /// </summary>
        private void PlayBukulmeCopy(RoundEngine round)
        {
            if (session == null || round == null || cardLayer == null)
            {
                return;
            }
            IReadOnlyList<Power> powers = session.Powers.Powers;
            for (int p = 0; p < powers.Count; p++)
            {
                var bukulme = powers[p] as BukulmePower;
                BukulmeCopyVisuals copy = bukulme != null ? bukulme.LastCopy : null;
                if (copy == null || ReferenceEquals(copy, lastBukulmeCopy))
                {
                    continue;
                }
                lastBukulmeCopy = copy;
                Vector2 from = CardLayerView.DrawPilePos;
                for (int i = 0; i < round.Hand.Count; i++)
                {
                    if (round.Hand[i].Id == copy.SourceCardId)
                    {
                        CardVisual source = cardLayer.VisualOfSlot(i);
                        if (source != null)
                        {
                            from = cardLayer.transform.InverseTransformPoint(source.transform.position);
                        }
                        break;
                    }
                }
                var cards = new List<CardVisual>();
                for (int i = 0; i < round.BonusHand.Count; i++)
                {
                    if (round.BonusHand[i].Card.Id == copy.CopyCardId)
                    {
                        CardVisual visual = cardLayer.VisualOfSlot(round.Hand.Count + i);
                        if (visual != null)
                        {
                            cards.Add(visual);
                        }
                    }
                }
                if (cards.Count > 0)
                {
                    powerFx.PlayCopyOut(cardLayer.transform, cards, from, BukulmeWarpColour,
                        CardLayerView.HandFrontOrder + 5, delegate { sfx.Fusion(); });
                }
            }
        }

        private static readonly Color BukulmeWarpColour = new Color(0.74f, 0.52f, 1f);

        private static bool PileHolds(IReadOnlyList<BlockCard> pile, int cardId)
        {
            for (int i = 0; i < pile.Count; i++)
            {
                if (pile[i].Id == cardId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The card Olta just fished, reeled out of its pile. False when nothing new
        /// reached the bonus hand (the card was already in hand, or on the board).</summary>
        private bool PlayReel(RoundEngine round, HashSet<int> before, bool fromDiscard)
        {
            for (int i = 0; i < round.BonusHand.Count; i++)
            {
                if (before.Contains(round.BonusHand[i].Card.Id))
                {
                    continue;
                }
                CardVisual visual = cardLayer.VisualOfSlot(round.Hand.Count + i);
                if (visual == null)
                {
                    continue;
                }
                powerFx.PlayReel(cardLayer.transform, visual,
                    fromDiscard ? CardLayerView.DiscardPilePos : CardLayerView.DrawPilePos,
                    fromDiscard, CardLayerView.HandFrontOrder + 5);
                return true;
            }
            return false;
        }

        /// <summary>Every play cell of the board as it is SHOWN, with its face - or, when
        /// <paramref name="withEmpty"/>, empty cells too (Tile null).</summary>
        private List<PowerFxView.CubeFace> CaptureCellFaces(bool withEmpty)
        {
            var faces = new List<PowerFxView.CubeFace>();
            GameBoard board = boardView != null ? boardView.Board : null;
            if (board == null)
            {
                return faces;
            }
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                for (int y = board.MinY; y < board.MinY + board.Height; y++)
                {
                    var pos = new GridPos(x, y);
                    if (!board.IsInside(pos))
                    {
                        continue;
                    }
                    Sprite tile = null;
                    Color colour = Color.white;
                    bool cube = board.GetCube(pos).HasValue && boardView.TryCubeLook(pos, 0f, out tile, out colour);
                    if (!cube)
                    {
                        tile = null;
                        colour = Color.white;
                    }
                    if (cube || withEmpty)
                    {
                        faces.Add(new PowerFxView.CubeFace { Cell = pos, Tile = tile, Colour = colour });
                    }
                }
            }
            return faces;
        }

        /// <summary>The board powers' own lead-ins. The cubes each one destroyed stand as copies
        /// until its strike lands, and then burst there - with the faces they wore, taken before
        /// the power ran. False when the power did nothing this lead-in covers.</summary>
        private bool PlayStrike(Power power, ActivationTarget target, List<PowerFxView.CubeFace> before,
            IReadOnlyList<GridPos> destroyed, List<GridPos> echoCells)
        {
            var faceAt = new Dictionary<GridPos, PowerFxView.CubeFace>();
            foreach (PowerFxView.CubeFace f in before)
            {
                faceAt[f.Cell] = f;
            }
            var doomed = new List<PowerFxView.CubeFace>();
            var cells = new List<GridPos>();
            var looks = new List<ClusterBurstView.Look>();
            if (destroyed != null)
            {
                foreach (GridPos c in destroyed)
                {
                    PowerFxView.CubeFace f;
                    if (faceAt.TryGetValue(c, out f) && !cells.Contains(c))
                    {
                        doomed.Add(f);
                        cells.Add(c);
                        looks.Add(new ClusterBurstView.Look { Tile = f.Tile, Colour = f.Colour });
                    }
                }
            }
            PowerFxView.Strike kind;
            bool rows = false;
            switch (power.DefId)
            {
                case "caprazlama":
                    kind = PowerFxView.Strike.Cross;
                    break;
                case "cerceve":
                    kind = PowerFxView.Strike.Frame;
                    break;
                case "buldozer":
                    kind = PowerFxView.Strike.Bulldozer;
                    var ys = new HashSet<int>();
                    var xs = new HashSet<int>();
                    foreach (GridPos c in cells)
                    {
                        ys.Add(c.Y);
                        xs.Add(c.X);
                    }
                    rows = ys.Count < xs.Count || (ys.Count == xs.Count && ys.Count <= 2);
                    break;
                case "eko":
                    kind = echoCells != null ? PowerFxView.Strike.EchoReplay : PowerFxView.Strike.EchoArm;
                    break;
                default:
                    kind = doomed.Count > 0 ? PowerFxView.Strike.MineBlast : PowerFxView.Strike.MineArm;
                    break;
            }
            if (doomed.Count == 0 && kind != PowerFxView.Strike.EchoArm && kind != PowerFxView.Strike.EchoReplay
                && kind != PowerFxView.Strike.MineArm)
            {
                return false;
            }
            powerFx.PlayStrike(boardView, kind, doomed, target.Cell, rows, echoCells, MainBoardCenter, MainBoardWorldSize,
                delegate
                {
                    if (cells.Count > 0)
                    {
                        FlashCells(cells, BlastColor, delegate { sfx.Explode(); }, looks);
                    }
                    // "Mayın" is a MINE: it goes off with a real blast, not only a burst.
                    if (kind == PowerFxView.Strike.MineBlast && target.Cell.HasValue)
                    {
                        FlashDynamite(boardView.transform.TransformPoint(boardView.CellToWorld(target.Cell.Value)));
                    }
                });
            return true;
        }

        /// <summary>"Kum Saati": what left the board goes back up, what came back reassembles.
        /// Both are worked out by comparing the board as it was SHOWN with the rewound one.</summary>
        private void PlayRewind(List<PowerFxView.CubeFace> before)
        {
            List<PowerFxView.CubeFace> after = CaptureCellFaces(true);
            var afterAt = new Dictionary<GridPos, PowerFxView.CubeFace>();
            foreach (PowerFxView.CubeFace f in after)
            {
                afterAt[f.Cell] = f;
            }
            var beforeAt = new Dictionary<GridPos, PowerFxView.CubeFace>();
            var leaving = new List<PowerFxView.CubeFace>();
            foreach (PowerFxView.CubeFace f in before)
            {
                beforeAt[f.Cell] = f;
                PowerFxView.CubeFace now;
                if (f.Tile != null && (!afterAt.TryGetValue(f.Cell, out now) || now.Tile != f.Tile))
                {
                    leaving.Add(f);
                }
            }
            var returning = new List<PowerFxView.CubeFace>();
            foreach (PowerFxView.CubeFace f in after)
            {
                PowerFxView.CubeFace was;
                if (f.Tile != null && (!beforeAt.TryGetValue(f.Cell, out was) || was.Tile != f.Tile))
                {
                    returning.Add(f);
                }
            }
            powerFx.PlayRewind(boardView, leaving, returning, MainBoardCenter, MainBoardWorldSize);
        }

        /// <summary>"Bardağın Boş Tarafı": every cell turns over; the cubes conjured into a line
        /// that then went off are shown landing as plain cubes, and burst once all have turned.
        /// </summary>
        private void PlayInvert(List<PowerFxView.CubeFace> before, RoundEngine round)
        {
            var afterAt = new Dictionary<GridPos, PowerFxView.CubeFace>();
            foreach (PowerFxView.CubeFace f in CaptureCellFaces(true))
            {
                afterAt[f.Cell] = f;
            }
            var wasFull = new HashSet<GridPos>();
            foreach (PowerFxView.CubeFace f in before)
            {
                if (f.Tile != null)
                {
                    wasFull.Add(f.Cell);
                }
            }
            // A cell that was empty and is empty again was filled and then went off in a line.
            var lineCells = new List<GridPos>();
            var after = new List<PowerFxView.CubeFace>();
            Sprite plain = ViewUtil.CubeTile(CubeKind.Normal);
            foreach (PowerFxView.CubeFace f in before)
            {
                PowerFxView.CubeFace now;
                afterAt.TryGetValue(f.Cell, out now);
                now.Cell = f.Cell;
                if (!wasFull.Contains(f.Cell) && now.Tile == null)
                {
                    lineCells.Add(f.Cell);
                    now.Tile = plain;
                    now.Colour = Color.white;
                }
                after.Add(now);
            }
            powerFx.PlayInvert(boardView, before, after, MainBoardCenter, MainBoardWorldSize,
                delegate
                {
                    if (lineCells.Count > 0)
                    {
                        FlashCells(lineCells, BlastColor, delegate { sfx.Explode(); });
                        // The lines the inversion made emptied the arena: that IS a clean sweep
                        // and it looks like one - the usual wave, popup and confetti. What it
                        // PAYS is still the rules' business (nothing without "Genel temizlik").
                        if (round != null && round.Board != null && round.Board.OccupiedCount == 0)
                        {
                            sfx.CleanSweep(1f);
                            SpawnSweepPopup();
                            EmitSweepConfetti();
                        }
                    }
                    else
                    {
                        sfx.Chime(1.1f);
                    }
                });
        }

        /// <summary>"Tılsım"'s charm, handed the ghosts' world positions and own colours from the
        /// power's report (the ghosts themselves are already gone).</summary>
        private void PlayTalismanCharm(TalismanActivationVisuals report)
        {
            var at = new List<Vector2>();
            var colours = new List<Color>();
            for (int i = 0; i < report.Ghosts.Count; i++)
            {
                at.Add(boardView.transform.TransformPoint(boardView.CellToWorld(report.Ghosts[i].Cell)));
                colours.Add(ViewUtil.CubeMaterialColor(report.Ghosts[i].Cube));
            }
            powerFx.PlayTalismanCharm(MainBoardCenter, MainBoardWorldSize, at, colours, report.TotalScore);
            sfx.Fusion();
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
            else if (!paused && lineSwapPicker.IsOpen && !playSwapPowerId.HasValue)
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
            if (playSwapPowerId.HasValue && kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                CancelTargeting(); // a swap chosen in normal play is simply put down
                return;
            }
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
                UpdateHud(); // the step text follows the first pick
            }
        }

        /// <summary>"Kentsel Dönüşüm" in normal play: the power waiting for its two lines, or
        /// null. Kept apart from the rescue so the picker it opened is not taken down by
        /// SyncRescueState, which closes the arrows whenever the round is not paused.</summary>
        private int? playSwapPowerId;

        /// <summary>The two lines of a normal-play swap: through the normal power path.</summary>
        private void OnPlaySwapPicked(LineAxis axis, int first, int second)
        {
            Power power = playSwapPowerId.HasValue ? session.Powers.Find(playSwapPowerId.Value) : null;
            playSwapPowerId = null;
            if (power == null)
            {
                RefreshAll(null);
                return;
            }
            ActivationTarget target = AimedAtChosenWorld(ActivationTarget.LineSwap(axis, first, second));
            LineSwapAnimView.Capture capture = BeginSwapAnimation(target);
            bool wasCharged = power.Charged;
            RunPowerActivation(power, target);
            FinishSwapAnimation(capture, wasCharged && !power.Charged); // spent = it swapped
            sfx.Shuffle();
        }

        /// <summary>
        /// Before a line swap runs: takes the two lines' faces off the board and HOLDS their cells
        /// blank, so the repaint that follows the swap shows nothing there until the travelling
        /// copies land. Null when the swap is aimed at the mirror world, which this board view does
        /// not draw - that swap simply repaints, as it always did.
        /// </summary>
        private LineSwapAnimView.Capture BeginSwapAnimation(ActivationTarget target)
        {
            if (target.OnMirrorWorld || !target.Axis.HasValue || !target.LineA.HasValue
                || !target.LineB.HasValue)
            {
                return null;
            }
            LineSwapAnimView.Capture capture = LineSwapAnimView.Take(boardView, target.Axis.Value,
                target.LineA.Value, target.LineB.Value);
            boardView.HoldCells(capture.Cells);
            return capture;
        }

        /// <summary>After a line swap: plays it if it went through, or just gives the cells
        /// back if it did not.</summary>
        private void FinishSwapAnimation(LineSwapAnimView.Capture capture, bool swapped)
        {
            if (capture == null)
            {
                return;
            }
            if (!swapped)
            {
                boardView.ReleaseCells(capture.Cells);
                return;
            }
            lineSwapAnim.Play(boardView, capture, delegate { boardView.ReleaseCells(capture.Cells); });
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
                LineSwapAnimView.Capture capture = BeginSwapAnimation(target);
                bool swapped = session.Powers.TryUse(powers[i].InstanceId, target);
                FinishSwapAnimation(capture, swapped);
                if (swapped)
                {
                    // No camera shake: the lines travelling IS the feedback now, and a shake
                    // under moving cubes only blurs them.
                    sfx.Shuffle();
                    break;
                }
            }
            RefreshAll(null);
        }
    }
}
