// PURPOSE: GameUiController's save/continue wiring - when the run is written to disk, and how
// CONTINUE picks it back up.
//
// AUTOSAVE, NOT A SAVE BUTTON: the run is written after every resolved turn, when the pause
// menu opens, when the player leaves a run from the pause menu, and when the application quits.
// "Continue any time" should not depend on the player remembering to do anything.
//
// A save is DELETED only when it describes a run that can no longer be continued: one that has
// been won or lost, or one that could not be read back. LEAVING a run does not delete it -
// SAVE & QUIT writes it and goes to the title, and RESTART simply overwrites it with the new
// run's first autosave.

using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>Writes the run to disk. Never throws: a failed autosave logs and is dropped,
        /// because losing a save is bad but taking the run down with it is worse.</summary>
        private void AutoSave()
        {
            if (session == null || session.Phase == GamePhase.GameOver
                || session.Phase == GamePhase.RunWon)
            {
                return;
            }
            try
            {
                SaveFileStore.Write(SaveGame.Save(session), currentDeck, lastSeedUsed);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[project_block] Autosave failed: " + e.Message);
            }
        }

        /// <summary>Drops the saved run: it is over, or the file could not be read back.</summary>
        private void DiscardSave()
        {
            SaveFileStore.Delete();
        }

        /// <summary>Picks up the saved run from the title menu. Anything wrong with the file
        /// (an older format, a joker that no longer exists, a truncated write) lands here as a
        /// SaveFormatException: the save is dropped and the player is left on the title, rather
        /// than dumped into a half-restored run.</summary>
        private void ContinueSavedRun()
        {
            string text = SaveFileStore.Read();
            if (!SaveGame.CanLoad(text))
            {
                Debug.Log("[project_block] No loadable save.");
                DiscardSave();
                GoToTitle();
                return;
            }
            currentDeck = SaveFileStore.SavedDeck;
            var config = new GameConfig();
            config.Deck = currentDeck;
            GameSession restored;
            try
            {
                restored = SaveGame.Load(text, config);
            }
            catch (SaveFormatException e)
            {
                Debug.LogWarning("[project_block] The save could not be loaded: " + e.Message);
                DiscardSave();
                GoToTitle();
                return;
            }

            session = restored;
            lastSeedUsed = SaveFileStore.SavedSeed;
            runOverPending = false;
            session.PhaseChanged += OnSessionPhaseChanged;

            menu.Hide();
            screen = AppScreen.Playing;
            SetRunPresentationVisible(true);
            // The board may be a different object than the one the views last drew, and the run
            // may have been saved in the market - StartRoundPresentation rebuilds either way.
            StartRoundPresentation();
            if (session.Phase == GamePhase.Market)
            {
                marketView.Show(session);
            }
            Debug.Log("[project_block] Continued a saved run at round " + session.RoundNumber);
        }

        /// <summary>Last chance to write the run down. Covers quitting from the market or
        /// mid-round without opening the pause menu first.</summary>
        private void OnApplicationQuit()
        {
            if (screen == AppScreen.Playing || screen == AppScreen.Paused)
            {
                AutoSave();
            }
        }
    }
}
