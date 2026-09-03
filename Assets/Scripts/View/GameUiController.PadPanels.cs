// PURPOSE: the panels, driven by the pad instead of by a cursor.
//
// DIRECT MODE DOES NOT USE A CURSOR ANYWHERE. The board and the shop are stepped in
// .PadPlay; this file is the rest - the collection overlay, the deck pick that starts a run,
// the option picker and the dead-end rescue - so that choosing the direct scheme never puts
// an arrow back on the screen.
//
// THEY ALL WORK THE SAME WAY, and it is the shelf's way (see PadNearestInDirection): a panel
// says how many things it is showing and WHERE each one is, a direction moves to the nearest
// one that actually lies that way, and the pointer is snapped onto it so the panel's own
// hover, highlight and tooltip follow with nothing added. Pressing A then does what a CLICK
// on that spot does - through the same method, never a synthesized click.
//
// EXTENSION POINT: a panel joins this by exposing a count and a world centre per item (the
// four here needed three lines each) and getting a case in PadPanelActive / PadPanelSnap and
// a handler. Rules NEVER live here.

using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>Which item in the open panel is chosen.</summary>
        private int padPanelIndex;

        /// <summary>The collection overlay relays every slot when it scrolls, so the pad holds
        /// on to the CARD rather than to a slot number.</summary>
        private int padPanelCardId;

        /// <summary>True when a panel is open that the pad drives itself. The scheme and the
        /// pad being live are part of the question: in cursor mode, and for a player who has
        /// put the pad down, these panels stay exactly as they were.</summary>
        private bool PadPanelDriven()
        {
            if (padScheme != PadScheme.Direct || gamepad == null || !gamepad.Active)
            {
                return false;
            }
            return deckOverlay.IsOpen || choicePicker.IsOpen || lineSwapPicker.IsOpen
                || (screen == AppScreen.DeckSelect && deckSelect.IsOpen);
        }

        /// <summary>Where the pointer sits while a panel is being stepped - on the chosen item,
        /// so the panel highlights it and the tooltip explains it without knowing about pads.
        /// Returns false when no driven panel is open.</summary>
        private bool PadPanelSnap(out Vector2 screenPoint)
        {
            Vector2? world = null;
            if (deckOverlay.IsOpen)
            {
                world = deckOverlay.EntryWorldCenter(padPanelIndex);
            }
            else if (choicePicker.IsOpen)
            {
                world = choicePicker.OptionWorldCenter(padPanelIndex);
            }
            else if (lineSwapPicker.IsOpen)
            {
                world = lineSwapPicker.ArrowWorldCenter(padPanelIndex);
            }
            else if (screen == AppScreen.DeckSelect && deckSelect.IsOpen)
            {
                world = deckSelect.DeckWorldCenter(padPanelIndex);
            }
            if (!world.HasValue)
            {
                screenPoint = Vector2.zero;
                return false;
            }
            screenPoint = cam.WorldToScreenPoint(world.Value);
            return true;
        }

        /// <summary>Steps a panel's selection. Returns true when the direction moved it, which
        /// is also the frame the caller should consume.</summary>
        private bool PadStepPanel(Gamepad pad, int count, System.Func<int, Vector2?> centreOf)
        {
            if (count <= 0)
            {
                padPanelIndex = 0;
                return false;
            }
            padPanelIndex = Mathf.Clamp(padPanelIndex, 0, count - 1);
            Vector2Int step = PadStep(pad);
            if (step.x == 0 && step.y == 0)
            {
                return false;
            }
            int wanted = PadNearestInDirection(padPanelIndex, count, centreOf, step);
            if (wanted == padPanelIndex)
            {
                return false; // the edges of a panel are hard, like the shelf's
            }
            padPanelIndex = wanted;
            return true;
        }

        /// <summary>Resets the panel selection. Called wherever one is opened from the pad, so
        /// a panel never opens with last time's row already chosen.</summary>
        private void PadPanelReset()
        {
            padPanelIndex = 0;
            padPanelCardId = 0;
        }

        // ---- the collection overlay ---------------------------------------------------

        /// <summary>The overlay, stepped. It is four screens in one - just looking, selling,
        /// picking a shape for a fox, and choosing next round's opening hand - so A means
        /// whatever the screen was opened FOR, exactly as a click does.</summary>
        private bool HandlePadDeckOverlay()
        {
            Gamepad pad = Gamepad.current;
            if (pad == null || !PadPanelDriven() || !deckOverlay.IsOpen)
            {
                return false;
            }
            // The scroll relays every slot, so the chosen CARD is found again by id first.
            int count = deckOverlay.EntryCount;
            if (padPanelCardId != 0)
            {
                for (int i = 0; i < count; i++)
                {
                    BlockCard at = deckOverlay.EntryCard(i);
                    if (at != null && at.Id == padPanelCardId)
                    {
                        padPanelIndex = i;
                        break;
                    }
                }
            }
            if (pad.buttonEast.wasPressedThisFrame)
            {
                foxPickSlot = -1;
                sellCardsMode = false;
                hileliPickMode = false;
                deckOverlay.Hide();
                PadPanelReset();
                return true;
            }
            // A collection is taller than the panel: the shoulders page it, and so does a
            // direction that has run out of cards to move onto.
            if (pad.rightShoulder.wasPressedThisFrame)
            {
                deckOverlay.Scroll(DeckScrollRowsPerNotch);
                return true;
            }
            if (pad.leftShoulder.wasPressedThisFrame)
            {
                deckOverlay.Scroll(-DeckScrollRowsPerNotch);
                return true;
            }
            Vector2Int look = PadStep(pad);
            if (look.x != 0 || look.y != 0)
            {
                int wanted = PadNearestInDirection(padPanelIndex, count,
                    deckOverlay.EntryWorldCenter, look);
                if (wanted != padPanelIndex)
                {
                    padPanelIndex = wanted;
                }
                else if (look.y != 0)
                {
                    // Off the top or the bottom of what is laid out: that is the page turning.
                    deckOverlay.Scroll(look.y > 0 ? -DeckScrollRowsPerNotch : DeckScrollRowsPerNotch);
                    padPanelIndex = Mathf.Clamp(padPanelIndex, 0,
                        Mathf.Max(0, deckOverlay.EntryCount - 1));
                }
                BlockCard now = deckOverlay.EntryCard(padPanelIndex);
                padPanelCardId = now != null ? now.Id : 0;
                return true;
            }
            if (hileliPickMode && pad.buttonNorth.wasPressedThisFrame)
            {
                ConfirmHileliZar();
                PadPanelReset();
                return true;
            }
            if (!pad.buttonSouth.wasPressedThisFrame)
            {
                return false;
            }
            BlockCard card = deckOverlay.EntryCard(padPanelIndex);
            Vector2? centre = deckOverlay.EntryWorldCenter(padPanelIndex);
            if (card == null || !centre.HasValue)
            {
                return true;
            }
            if (hileliPickMode)
            {
                ToggleHileliPick(card);
                return true;
            }
            if (foxPickSlot >= 0)
            {
                ApplyFoxShape(deckOverlay.EntryShape(padPanelIndex));
                PadPanelReset();
                return true;
            }
            if (sellCardsMode)
            {
                SellCardFromDeck(card, centre.Value);
                padPanelCardId = 0;
                padPanelIndex = Mathf.Clamp(padPanelIndex, 0,
                    Mathf.Max(0, deckOverlay.EntryCount - 1));
                return true;
            }
            return true; // just looking: A does nothing, B closes
        }

        // ---- the deck pick that starts a run --------------------------------------------

        private bool HandlePadDeckSelect()
        {
            Gamepad pad = Gamepad.current;
            if (pad == null || !PadPanelDriven() || !deckSelect.IsOpen)
            {
                return false;
            }
            int count = deckSelect.DeckCount;
            if (PadStepPanel(pad, count, deckSelect.DeckWorldCenter))
            {
                Vector2? centre = deckSelect.DeckWorldCenter(padPanelIndex);
                if (centre.HasValue)
                {
                    deckSelect.SetHovered(centre.Value); // the panel lights its own choice
                }
                return true;
            }
            if (pad.buttonEast.wasPressedThisFrame)
            {
                PadPanelReset();
                GoToTitle();
                return true;
            }
            if (pad.buttonSouth.wasPressedThisFrame && padPanelIndex < DeckLibrary.All.Count)
            {
                int picked = padPanelIndex;
                PadPanelReset();
                StartRunWithDeck(DeckLibrary.All[picked]);
                return true;
            }
            return false;
        }

        // ---- the option picker ------------------------------------------------------------

        private bool HandlePadChoice()
        {
            Gamepad pad = Gamepad.current;
            if (pad == null || !PadPanelDriven() || !choicePicker.IsOpen)
            {
                return false;
            }
            if (PadStepPanel(pad, choicePicker.OptionCount, choicePicker.OptionWorldCenter))
            {
                return true;
            }
            if (pad.buttonEast.wasPressedThisFrame)
            {
                choicePicker.Hide();
                ClearChoice();
                PadPanelReset();
                return true;
            }
            if (!pad.buttonSouth.wasPressedThisFrame)
            {
                return false;
            }
            int chosen = padPanelIndex;
            choicePicker.Hide();
            PadPanelReset();
            // ResolveChoice answers false when it re-opened the picker itself (the debug boss
            // list pages), in which case the pending choice has to survive.
            if (ResolveChoice(chosen))
            {
                ClearChoice();
            }
            return true;
        }

        // ---- the dead-end rescue ------------------------------------------------------------

        /// <summary>The round is paused on this, so it is the one panel a pad MUST be able to
        /// answer - a scheme that cannot take the rescue would leave the run stuck.</summary>
        private bool HandlePadRescue()
        {
            Gamepad pad = Gamepad.current;
            if (pad == null || !PadPanelDriven() || !lineSwapPicker.IsOpen)
            {
                return false;
            }
            if (PadStepPanel(pad, lineSwapPicker.ArrowCount, lineSwapPicker.ArrowWorldCenter))
            {
                return true;
            }
            if (!pad.buttonSouth.wasPressedThisFrame)
            {
                return false;
            }
            Vector2? at = lineSwapPicker.ArrowWorldCenter(padPanelIndex);
            if (at.HasValue)
            {
                // The picker's own two-step (pick an axis, then where it lands) is untouched:
                // this hands it the arrow's centre exactly as a click would.
                lineSwapPicker.HandleClick(at.Value);
                padPanelIndex = 0;
            }
            return true;
        }
    }
}
