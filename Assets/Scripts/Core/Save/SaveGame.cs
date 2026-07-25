// PURPOSE: The one entry point for saving and loading a run. Core produces and consumes a
// STRING; where that string is stored is the platform layer's business (the View writes it
// under Application.persistentDataPath), which is what keeps Core free of UnityEngine.
//
// VERSIONING is deliberately blunt. A save carries the format version, and a mismatch is
// refused outright rather than migrated. The game's rules are still changing every week; a
// save written before a joker changed shape cannot be made correct by guesswork, and a run
// that loads WRONG is far worse than one that refuses to load. Bump FormatVersion whenever
// the written fields change.

namespace ProjectBlock.Core
{
    /// <summary>Saves and loads a whole run.</summary>
    public static class SaveGame
    {
        /// <summary>Bump whenever the save layout changes. Older files are then refused.</summary>
        public const int FormatVersion = 1;

        private const string VersionKey = "version";

        /// <summary>The whole run as text.</summary>
        public static string Save(GameSession session)
        {
            var w = new SaveWriter();
            w.Write(VersionKey, FormatVersion);
            session.Save(w);
            return w.ToText();
        }

        /// <summary>
        /// Rebuilds a run from text. <paramref name="template"/> supplies the parts of a
        /// GameConfig that are CODE rather than data - the deck's shape generator, the round
        /// progression, the market config - while the live rules and scoring come from the file.
        /// Throws SaveFormatException on a version mismatch or any drifted field.
        /// </summary>
        public static GameSession Load(string text, GameConfig template)
        {
            var r = new SaveReader(text);
            int version = r.ReadInt(VersionKey);
            if (version != FormatVersion)
            {
                throw new SaveFormatException("This save is version " + version
                    + ", but this build reads version " + FormatVersion + ".");
            }
            return GameSession.Load(r, template);
        }

        /// <summary>True if the text looks like a save this build can read. Used by the menu to
        /// decide whether CONTINUE is offered, without throwing on a stale file.</summary>
        public static bool CanLoad(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            try
            {
                var r = new SaveReader(text);
                return r.ReadInt(VersionKey) == FormatVersion;
            }
            catch (SaveFormatException)
            {
                return false;
            }
        }
    }
}
