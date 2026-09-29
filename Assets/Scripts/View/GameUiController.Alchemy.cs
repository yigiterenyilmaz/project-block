// PURPOSE: "Simya" - choosing what a two-element card IS. The rule is Core's (BlockCard: an
// alchemical card is ONE of its elements, the player's pick, and every rule asks Has()); this file is
// only the way the player picks.
//
// Three ways in, one picker. RIGHT-CLICK a card in the hand or the bonus hand (the desktop). PRESS
// AND HOLD it without moving (a phone - a finger is the left button, so the hold begins as a
// pick-up and is turned into the picker once it is plainly not a drag; it works with a mouse too).
// The pad's west button in the direct scheme. Whatever a right-click used to do on such a card - a
// gear's turn, the fox's shape list, retro's rotation - is still there as a row of the same picker,
// so the choice never takes an ability away. No animation: the card is simply rebuilt wearing the
// element it now is.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>How long a still press on an alchemical card waits before it opens the picker.
        /// Long enough that a quick pick-up never trips it, short enough to feel like a hold.</summary>
        private const float AlchemyHoldSeconds = 0.45f;

        /// <summary>How far (world units) the pointer may wander during that hold and still be a
        /// hold rather than the start of a drag.</summary>
        private const float AlchemyHoldSlack = 0.22f;

        // Picker rows that are not elements (element rows carry the element's own int, >= 0).
        private const int AlchemyRowRotate = -10;
        private const int AlchemyRowFox = -11;

        private float alchemyPressTime = -1f;
        private Vector2 alchemyPressWorld;
        private int alchemyChoiceSlot = -1;
        private int alchemyChoiceCardId;

        /// <summary>A card may be chosen for only when the player can SEE it: a face-down hand
        /// ("Şaşırtmaca") hides what a card is until it is committed to.</summary>
        private static bool CanChooseAlchemy(RoundEngine round, BlockCard card)
        {
            return card != null && card.IsAlchemical
                && (!round.HandIsFaceDown || round.RevealedHandCardId == card.Id);
        }

        /// <summary>Opens the small element picker over the card in <paramref name="slot"/>.
        /// False when that card has nothing to choose.</summary>
        private bool OpenAlchemyPicker(RoundEngine round, int slot, Vector2 nearWorld)
        {
            BlockCard card = CardOfSlot(round, slot);
            if (!CanChooseAlchemy(round, card))
            {
                return false;
            }
            HideTooltip();
            pendingChoice = ChoiceKind.CardElement;
            pendingChoiceJokerId = 0;
            pendingChoiceValues.Clear();
            alchemyChoiceSlot = slot;
            alchemyChoiceCardId = card.Id;

            var labels = new List<string>();
            var icons = new List<Sprite>();
            var tints = new List<Color>();
            IReadOnlyList<BlockElement> choices = card.ElementChoices;
            BlockElement? active = card.ActiveElement;
            int picked = -1;
            for (int i = 0; i < choices.Count; i++)
            {
                BlockElement element = choices[i];
                if (active.HasValue && element == active.Value)
                {
                    picked = labels.Count;
                }
                pendingChoiceValues.Add((int)element);
                labels.Add(ViewUtil.ElementLabel(element));
                Sprite tile = ViewUtil.CubeTile(element);
                icons.Add(tile);
                tints.Add(ViewUtil.CarriesOwnPaint(tile) ? Color.white : ViewUtil.ElementColor(element));
            }
            // The right-click abilities the card has AS IT IS NOW stay reachable from here.
            if (round.CardHasElement(card, BlockElement.Mechanical) || session.Config.Rules.RetroMode)
            {
                pendingChoiceValues.Add(AlchemyRowRotate);
                labels.Add(Loc.Pick("ROTATE", "DÖNDÜR"));
                icons.Add(null);
                tints.Add(Color.white);
            }
            else if (round.CardHasElement(card, BlockElement.Fox))
            {
                pendingChoiceValues.Add(AlchemyRowFox);
                labels.Add(Loc.Pick("PICK SHAPE", "ŞEKİL SEÇ"));
                icons.Add(null);
                tints.Add(Color.white);
            }
            choicePicker.ShowCompact(Loc.Pick("BE WHICH?", "HANGİSİ OLSUN?"), labels, icons, tints,
                picked, nearWorld);
            return true;
        }

        /// <summary>One row of the element picker.</summary>
        private void ResolveAlchemyChoice(int value)
        {
            RoundEngine round = session.CurrentRound;
            int slot = alchemyChoiceSlot;
            alchemyChoiceSlot = -1;
            BlockCard card = round != null ? CardOfSlot(round, slot) : null;
            // The hand may have moved under an open picker (it is modal, but be sure).
            if (card == null || card.Id != alchemyChoiceCardId)
            {
                return;
            }
            if (value == AlchemyRowRotate)
            {
                round.RotateCard(slot, ignoreMechanicalRequirement: session.Config.Rules.RetroMode
                    && !round.CardHasElement(card, BlockElement.Mechanical));
                cardLayer.ForgetCard(card.Id);
                return;
            }
            if (value == AlchemyRowFox)
            {
                foxPickSlot = slot;
                deckOverlay.ResetScroll();
                deckOverlay.ShowShapes(FoxShapeChoices());
                return;
            }
            if (session.ChooseCardElement(card.Id, (BlockElement)value))
            {
                // Rebuilt, not animated: the card now simply wears what it is.
                cardLayer.ForgetCard(card.Id);
                sfx.Place();
            }
        }

        /// <summary>The press-and-hold route, ticked every frame a card is being carried. Once
        /// the press has stayed put long enough, the pick-up is undone and the picker opens.
        /// True when it did.</summary>
        private bool TickAlchemyHold(RoundEngine round, Mouse mouse, Vector2 world)
        {
            if (draggedCard == null || alchemyPressTime < 0f || !mouse.leftButton.isPressed)
            {
                return false;
            }
            if ((world - alchemyPressWorld).sqrMagnitude > AlchemyHoldSlack * AlchemyHoldSlack)
            {
                alchemyPressTime = -1f; // it moved: this is a drag, and stays one
                return false;
            }
            if (Time.unscaledTime - alchemyPressTime < AlchemyHoldSeconds)
            {
                return false;
            }
            alchemyPressTime = -1f;
            CardVisual held = draggedCard;
            int slot = held.SlotIndex;
            if (!CanChooseAlchemy(round, CardOfSlot(round, slot)))
            {
                return false;
            }
            CancelDrag();
            return OpenAlchemyPicker(round, slot, held.HomePosition);
        }

        /// <summary>Arms the hold for a card that has just been picked up.</summary>
        private void ArmAlchemyHold(RoundEngine round, CardVisual picked, Vector2 world)
        {
            alchemyPressTime = picked != null && CanChooseAlchemy(round, CardOfSlot(round, picked.SlotIndex))
                ? Time.unscaledTime
                : -1f;
            alchemyPressWorld = world;
        }
    }
}
