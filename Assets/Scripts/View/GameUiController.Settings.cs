// PURPOSE: GameUiController's settings screen and the player preferences behind it.
//
// Every setting is a plain field on the controller that something else already read
// (Loc.Language, SoundFx.MasterVolume, the seed field, verboseTurnLogs) - this file only
// edits and PERSISTS them, so nothing new has to be threaded through the game.
//
// Settings are stored in PlayerPrefs and loaded once at startup. They are player prefs, not
// run state: they survive restarts and are deliberately NOT part of a save file.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        // Settings entry order.
        private const int SettingLanguage = 0;
        private const int SettingVolume = 1;
        private const int SettingSeed = 2;
        private const int SettingLogs = 3;
        private const int SettingBack = 4;

        private const string PrefLanguage = "language";
        private const string PrefVolume = "volume";
        private const string PrefSeed = "seed";
        private const string PrefLogs = "verboseLogs";

        private const float VolumeStep = 0.1f;

        /// <summary>Where BACK goes - settings is reachable from the title AND from a paused
        /// run, and must return to whichever opened it.</summary>
        private AppScreen settingsReturnTo = AppScreen.Title;

        private float masterVolume = 1f;

        /// <summary>Reads every persisted preference. Called before the views are built, so
        /// the language is right the first time any label is created; the volume is pushed
        /// into SoundFx afterwards, once it exists.</summary>
        private void LoadSettings()
        {
            Loc.Language = PlayerPrefs.GetString(PrefLanguage, "en") == "tr"
                ? GameLanguage.Turkish
                : GameLanguage.English;
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefVolume, 1f));
            // 0 means "roll a fresh seed every run" - the same meaning the field has always had.
            seed = PlayerPrefs.GetInt(PrefSeed, 0);
            verboseTurnLogs = PlayerPrefs.GetInt(PrefLogs, 1) != 0;
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetString(PrefLanguage, Loc.Language == GameLanguage.Turkish ? "tr" : "en");
            PlayerPrefs.SetFloat(PrefVolume, masterVolume);
            PlayerPrefs.SetInt(PrefSeed, seed);
            PlayerPrefs.SetInt(PrefLogs, verboseTurnLogs ? 1 : 0);
        }

        private void OpenSettings(AppScreen returnTo)
        {
            settingsReturnTo = returnTo;
            screen = AppScreen.Settings;
            ShowSettingsMenu();
        }

        private void CloseSettings()
        {
            SaveSettings();
            screen = settingsReturnTo;
            ShowCurrentMenu();
        }

        private void ShowSettingsMenu()
        {
            string on = Loc.Pick("on", "açık");
            string off = Loc.Pick("off", "kapalı");
            var entries = new List<MenuEntry>
            {
                MenuEntry.Of(Loc.Pick("Language: ", "Dil: ")
                    + (Loc.Language == GameLanguage.Turkish ? "Türkçe" : "English")),
                MenuEntry.Of(Loc.Pick("Volume: ", "Ses: ")
                    + Mathf.RoundToInt(masterVolume * 100f) + "%"),
                MenuEntry.Of(Loc.Pick("Seed: ", "Tohum: ") + DescribeSeedSetting()),
                MenuEntry.Of(Loc.Pick("Turn logs: ", "Tur kaydı: ")
                    + (verboseTurnLogs ? on : off)),
                MenuEntry.Of(Loc.Pick("BACK", "GERİ"))
            };
            // Opened over a paused run, the board stays visible behind it.
            Color backdrop = settingsReturnTo == AppScreen.Paused
                ? MenuSkin.OverlayBackdrop
                : MenuSkin.Backdrop;
            menu.Show(Loc.Pick("SETTINGS", "AYARLAR"),
                Loc.Pick("click or  ← →  to change     [Esc] back",
                    "tıkla ya da  ← →  ile değiştir     [Esc] geri"),
                entries, backdrop);
        }

        /// <summary>"random", or the pinned seed number.</summary>
        private string DescribeSeedSetting()
        {
            return seed != 0 ? seed.ToString() : Loc.Pick("random", "rastgele");
        }

        /// <summary>Changes one setting. <paramref name="delta"/> is +1 for a click or the right
        /// arrow and -1 for the left arrow; the toggles ignore its sign.</summary>
        private void AdjustSetting(int index, int delta)
        {
            switch (index)
            {
                case SettingLanguage:
                    // Routes through the normal switch so every open view is re-texted; it ends
                    // by calling ShowCurrentMenu, which redraws this screen with the new labels.
                    ToggleLanguage();
                    return;
                case SettingVolume:
                    SetMasterVolume(masterVolume + delta * VolumeStep);
                    break;
                case SettingSeed:
                    ToggleSeedPinned();
                    break;
                case SettingLogs:
                    verboseTurnLogs = !verboseTurnLogs;
                    break;
                default:
                    return;
            }
            SaveSettings();
            ShowSettingsMenu();
        }

        /// <summary>Applies a new master volume, wrapping rather than clamping so a click-only
        /// player can still come back down from 100%.</summary>
        private void SetMasterVolume(float value)
        {
            if (value > 1f + 0.001f)
            {
                value = 0f;
            }
            else if (value < 0f)
            {
                value = 1f;
            }
            masterVolume = Mathf.Clamp01(value);
            if (sfx != null)
            {
                sfx.MasterVolume = masterVolume;
            }
        }

        /// <summary>Flips between "roll a fresh seed each run" and pinning the seed. Pinning
        /// takes the seed of the run last started, so the natural use - "let me replay that
        /// exact run" - is one click. Before any run has started there is nothing to pin, so
        /// one is minted on the spot.</summary>
        private void ToggleSeedPinned()
        {
            if (seed != 0)
            {
                seed = 0;
                return;
            }
            seed = lastSeedUsed != 0 ? lastSeedUsed : System.Environment.TickCount;
        }

        /// <summary>Input for the settings screen. Left/right adjust the highlighted row;
        /// a click adjusts the row it hits; BACK and Escape leave.</summary>
        private void HandleSettingsInput(Keyboard kb, Mouse mouse)
        {
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                CloseSettings();
                return;
            }
            if (kb != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame)
                {
                    AdjustSetting(menu.Selected, +1);
                    return;
                }
                if (kb.leftArrowKey.wasPressedThisFrame)
                {
                    AdjustSetting(menu.Selected, -1);
                    return;
                }
            }
            int picked = ReadMenuChoice(kb, mouse);
            if (picked < 0)
            {
                return;
            }
            if (picked == SettingBack)
            {
                CloseSettings();
                return;
            }
            AdjustSetting(picked, +1);
        }
    }
}
