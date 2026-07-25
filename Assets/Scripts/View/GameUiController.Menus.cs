// PURPOSE: GameUiController's menu layer - the app-level screen state machine that sits
// ABOVE a run, plus the title screen itself.
//
// THE ONE CENTRAL RULE: `screen` decides who owns input. While it is anything other than
// AppScreen.Playing, Update hands the whole frame to HandleMenuInput and the game never
// sees it - so no menu has to know about drags, targeting, or the eight in-game modals.
//
// Presentation and input routing only; rules never live here (see the View folder note).

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>Where the app is, above and around a run. Members are added by the chunk
        /// that needs them, so an unhandled screen is a compile error rather than a silent
        /// dead branch.</summary>
        private enum AppScreen
        {
            /// <summary>The title menu. No session exists yet (or the last one was left).</summary>
            Title,

            /// <summary>Picking the deck a new run will start with - reached from Play.</summary>
            DeckSelect,

            /// <summary>A run owns the screen; every other partial of this class applies.</summary>
            Playing
        }

        // Title entry order. Named so the dispatch below never indexes by a bare number.
        private const int TitlePlay = 0;
        private const int TitleContinue = 1;
        private const int TitleQuit = 2;

        private AppScreen screen = AppScreen.Title;
        private MenuScreenView menu;

        /// <summary>Leaves whatever was on screen and shows the title menu. Safe with a null
        /// session, which is how the game now boots.</summary>
        private void GoToTitle()
        {
            screen = AppScreen.Title;
            deckSelect.Hide();
            SetRunPresentationVisible(false);
            // Handles a null session by turning the CRT, the hum and the bit-crush off, so a
            // run abandoned in retro mode cannot leave the title screen green and buzzing.
            SyncRetroPresentation();
            ShowCurrentMenu();
        }

        /// <summary>(Re)draws whichever menu the current screen calls for. Also the re-text
        /// path after a language switch.</summary>
        private void ShowCurrentMenu()
        {
            if (screen == AppScreen.DeckSelect)
            {
                deckSelect.Show(DeckLibrary.All, currentDeck);
                return;
            }
            ShowTitleMenu();
        }

        private void ShowTitleMenu()
        {
            var entries = new List<MenuEntry>
            {
                MenuEntry.Of(Loc.Pick("PLAY", "OYNA")),
                // EXTENSION POINT - SAVES: enabled once a run can be written to disk; the
                // entry is shown disabled meanwhile so the feature is discoverable.
                MenuEntry.Locked(Loc.Pick("CONTINUE", "DEVAM ET"),
                    Loc.Pick("no saved run", "kayıtlı oyun yok")),
                MenuEntry.Of(Loc.Pick("QUIT", "ÇIKIŞ"))
            };
            // The subtitle names the language you would switch TO, like the debug HUD does.
            menu.Show("PROJECT BLOCK", Loc.Pick("[L] türkçe", "[L] english"), entries);
        }

        /// <summary>Owns the frame whenever a run is not being played.</summary>
        private void HandleMenuInput(Keyboard kb, Mouse mouse)
        {
            if (kb != null && kb.lKey.wasPressedThisFrame)
            {
                ToggleLanguage();
                return;
            }
            if (screen == AppScreen.DeckSelect)
            {
                HandleTitleDeckSelect(kb, mouse);
                return;
            }
            if (mouse != null)
            {
                menu.UpdateHover(mouse.position.ReadValue());
            }
            if (kb != null)
            {
                if (kb.downArrowKey.wasPressedThisFrame)
                {
                    menu.MoveSelection(+1);
                    return;
                }
                if (kb.upArrowKey.wasPressedThisFrame)
                {
                    menu.MoveSelection(-1);
                    return;
                }
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                {
                    ActivateTitleEntry(menu.Activate());
                    return;
                }
            }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                ActivateTitleEntry(menu.EntryAt(mouse.position.ReadValue()));
            }
        }

        private void ActivateTitleEntry(int index)
        {
            switch (index)
            {
                case TitlePlay:
                    // Confirmed flow: choosing a deck is part of starting a run.
                    screen = AppScreen.DeckSelect;
                    menu.Hide();
                    deckSelect.Show(DeckLibrary.All, currentDeck);
                    break;
                case TitleContinue:
                    break; // disabled until saves exist - MenuScreenView never returns it
                case TitleQuit:
                    QuitGame();
                    break;
            }
        }

        /// <summary>The deck pick that starts a run. Clicking a deck starts it; Escape or a
        /// click off the panels goes back to the title rather than stranding the player.</summary>
        private void HandleTitleDeckSelect(Keyboard kb, Mouse mouse)
        {
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                GoToTitle();
                return;
            }
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            int index = deckSelect.DeckAt(world);
            if (index < 0)
            {
                GoToTitle();
                return;
            }
            StartRunWithDeck(DeckLibrary.All[index]);
        }

        private void StartRunWithDeck(DeckDefinition deck)
        {
            currentDeck = deck;
            Debug.Log("[project_block] Deck selected: " + currentDeck.Name);
            deckSelect.Hide();
            menu.Hide();
            screen = AppScreen.Playing;
            SetRunPresentationVisible(true);
            NewGame();
        }

        /// <summary>Shows or hides everything that belongs to a run in progress. The persistent
        /// views are toggled; the modal ones are simply closed, since a menu must never be
        /// reached with a half-finished picker still armed.</summary>
        private void SetRunPresentationVisible(bool visible)
        {
            boardView.gameObject.SetActive(visible);
            cardLayer.gameObject.SetActive(visible);
            flameStreak.gameObject.SetActive(visible);
            blastFx.gameObject.SetActive(visible);
            infoText.gameObject.SetActive(visible);
            messageText.gameObject.SetActive(visible);
            jokerBar.SetVisible(visible);
            powerBar.SetVisible(visible);
            if (visible)
            {
                return;
            }
            marketView.Hide();
            deckOverlay.Hide();
            grantPicker.Hide();
            choicePicker.Hide();
            batakBet.Hide();
            blockDesigner.Hide();
            cubePicker.Hide();
            lineSwapPicker.Hide();
            HideTooltip();
            // Any half-armed flow dies with the run presentation.
            ClearChoice();
            parazitStep = ParazitStep.None;
            pendingTargetJokerId = null;
            pendingTargetPowerId = null;
            pendingOltaMark = false;
            foxPickSlot = -1;
            sellCardsMode = false;
            hileliPickMode = false;
            draggedCard = null;
            retroFallHand = -1;
        }

        private void QuitGame()
        {
            // A no-op inside the editor, so the log is the only feedback there.
            Debug.Log("[project_block] Quit requested.");
            Application.Quit();
        }
    }
}
