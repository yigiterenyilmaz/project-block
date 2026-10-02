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
        // 2 (2026-07-25): BaseSellValue left Joker/Power (sell value is now derived from the
        // buy price), so every joker and power writes one field fewer.
        // 3 (2026-07-25): Enfeksiyon gained LastDetonatedCells for its break animation, so it
        // writes one field more.
        // 4 (2026-07-26): the boss/joker wave landed - the session writes its debt, the currency
        // effects took, the market's free-item flag and the final-round replay count; the board
        // writes the lines "Kangren" killed; every card writes its two "Kaçakçı" flags; and the
        // seven new bosses and three new jokers change the reflected field counts.
        // 5 (2026-07-26): the pattern jokers landed - the session writes what effects GRANTED
        // the purse, and every card writes the element it is the antimatter of.
        // 6 (2026-07-26): a run is now 15 numbered rounds with a BOSS STAGE between every third
        // one, so a save has to say which stage of its number it is on.
        // 7 (2026-07-26): the combo can now be bridged ("Mikrodalga"), so the rules write the
        // bridge allowance and its discount and the round writes how long the streak has been
        // asleep; and the "Hedefli" block landed, so every card writes which cube is its target
        // and the round writes which targeted blocks still have their shot.
        // 8 (2026-07-26): "Kütleçekim merkezi" landed, so the board writes which way water falls.
        // 9 (2026-07-26): "Kaçakçı" wears out, so it writes how many sound hauls it has left in it.
        // 10 (2026-08-10): "Savunmacı" was cut and "Kiracı" became "Metamorfoz", so a DefId an
        // older save owns may no longer exist in the registry.
        // 11 (2026-08-12): three jokers were renamed - "Dezenformasyon" -> "Konfüzyon",
        // "Oryantasyon" -> "Baba Ocağı", "Damlaya Damlaya Göl Olur" -> "Kapalı Ekonomi" - so
        // their DefIds (dezenformasyon -> konfuzyon, oryantasyon -> baba_ocagi, damlaya ->
        // kapali_ekonomi) are no longer in the registry for an older save to find.
        // 12 (2026-08-17): "Hileli zar" moved from the powers to the jokers, keeping its DefId,
        // so an older save's owned POWER "hileli_zar" no longer resolves - and a new one owns a
        // joker of that id with a field the power never had.
        // 13 (2026-09-03): "Rehin puan" grew a grace period, and the turn counter behind it is a
        // new field on the boss - so the reflection walk writes one more entry than a 12 does,
        // and the positional reader would go out of step on every boss saved after it.
        // 15 (2026-09-06): the market's block purchase limit landed, so every card writes
        // whether it came off the shelf - a 14 cannot say, and reading one would give the
        // player back every slot they had spent.
        // 16 (2026-09-06): "Altın Kumbara" counts turns between payments, "Karantina" was
        // rebuilt from sealed lines into a moving patch of cells, and "Tutumluluk" remembers the
        // deck it started with - each changed which fields the reflection walk writes, so a 15's
        // content block no longer lines up.
        // 17 (2026-09-10): "Enfeksiyon" reports which cells its one-time spread actually TOOK
        // and where it spread from, so the detonation can be drawn against the infections that
        // exist rather than the four a plus assumes. Two more fields on the joker, so a 16's
        // content block no longer lines up.
        // 18 (2026-09-14): merged with a separate 17 - "Hidrolik pres" now remembers WHICH cell
        // of its patch the player chose to keep the cube (pressCell), on top of the Enfeksiyon
        // fields above. Neither 17 writes both, so both are refused.
        // 19 (2026-09-16): every joker now keeps its own RUN-LONG proc statistics (how often it
        // has fired and what it has paid), so the reflection walk writes two more entries for
        // EVERY joker in the file rather than for one of them - an 18's content block goes out
        // of step on the first joker it reads.
        // 20 (2026-09-16): the combo became a MULTIPLIER ladder (ScoringConfig.ComboMultipliers)
        // instead of a flat per-step bonus, so the scoring block - which is positional - now
        // writes the ladder's length and its rungs where the step and the cap used to be. A 19
        // goes out of step on every field after it.
        // 23 (2026-09-29): merged with the "Simya" / "Kara Delik" branch - every card saves its
        // chosen element (".active") and the black hole joker saves what it swallowed. Neither side's
        // 19-22 writes both, so all of them are refused.
        // 24 (2026-09-30): "Kredi kartı" became a loan - the session saves the term it is into and
        // the stage's books (credit.*), every market offer its bank campaign, and the round its
        // minimum payment (creditInstallment). A 23 is short all three.
        // 25 (2026-09-30): "Enflasyon" raises the bar by a share of what is still missing rather
        // than by a compounding rate, so the boss saves what it has ADDED (and its new rate) where
        // it used to save the turns it had counted. A 24's boss block goes out of step.
        // 26 (2026-09-30): "Tamagotchi" asks for CARDS rather than shapes and goes berserk when it
        // is left hungry, so the boss saves the card ids it is owed, whether a rampage is pending
        // and its new knobs. A 25's boss block goes out of step.
        // 27 (2026-10-01): "Tamagotchi" keeps its two requests as fixed SLOTS with a fed flag and
        // a tier each, goes furious for the rest of the round, and has a punish planner with its own
        // knobs - the boss block changed shape again.
        // 28 (2026-10-02): overtime grows the eroded rim back, so the round saves what the rim
        // erosion took off each side and what its cells were (round.eroded.*). A 27 is short it.
        // 29 (2026-10-02): "Mapus" keeps several locks at once - a list of cells with the turns
        // each has left and a count toward the next - where it kept one seal and how long it had
        // held it, and its knobs changed. A 28's boss block goes out of step.
        // 30 (2026-10-02): SNOW. Every cube writes three more values (its snow power, melt time
        // and packed flag - zero on anything that is not snow), and the "Çığ" JOKER is gone: it
        // is a power now, so a 29 could name a joker the registry no longer has.
        public const int FormatVersion = 30;

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
