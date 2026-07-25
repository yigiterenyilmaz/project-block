// PURPOSE: Where a saved run lives on disk. Core produces and consumes a plain string (see
// SaveGame); this is the platform half that puts it somewhere, which is why Core stays free
// of UnityEngine.
//
// The DECK ARCHETYPE is stored beside the file rather than inside it. A DeckDefinition is code
// (it carries a shape generator), not data, so it cannot be serialized - but it must be
// restored, because the generator decides what shapes future market offers and minted cards
// take. The name is enough to find it again in DeckLibrary.

using System;
using System.IO;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Reads and writes the single autosave slot.</summary>
    public static class SaveFileStore
    {
        private const string FileName = "run.save";
        private const string PrefDeck = "saveDeck";
        private const string PrefSeed = "saveSeed";

        private static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        /// <summary>The deck archetype the saved run is being played with, or the library's
        /// default when there is no save or its deck no longer exists.</summary>
        public static DeckDefinition SavedDeck
        {
            get
            {
                string name = PlayerPrefs.GetString(PrefDeck, string.Empty);
                for (int i = 0; i < DeckLibrary.All.Count; i++)
                {
                    if (DeckLibrary.All[i].Name == name)
                    {
                        return DeckLibrary.All[i];
                    }
                }
                return DeckLibrary.Classic;
            }
        }

        public static int SavedSeed
        {
            get { return PlayerPrefs.GetInt(PrefSeed, 0); }
        }

        /// <summary>The saved run's text, or null when there is none or it cannot be read.</summary>
        public static string Read()
        {
            try
            {
                return File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[project_block] Could not read the save: " + e.Message);
                return null;
            }
        }

        /// <summary>True when there is a save THIS BUILD can actually load. A file from an
        /// older format counts as no save, so CONTINUE is never offered for one.</summary>
        public static bool HasLoadableSave()
        {
            return SaveGame.CanLoad(Read());
        }

        public static void Write(string text, DeckDefinition deck, int seed)
        {
            try
            {
                File.WriteAllText(FilePath, text);
                PlayerPrefs.SetString(PrefDeck, deck != null ? deck.Name : string.Empty);
                PlayerPrefs.SetInt(PrefSeed, seed);
            }
            catch (Exception e)
            {
                // A failed autosave must never take the run down with it.
                Debug.LogWarning("[project_block] Could not write the save: " + e.Message);
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[project_block] Could not delete the save: " + e.Message);
            }
        }
    }
}
