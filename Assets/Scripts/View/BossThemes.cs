// PURPOSE: BOSS THEMES - which of the boss LOOKS each boss is dressed in. A theme IS a visual style
// (an intro + a persistent look from BossIdentityView), and every boss is put under the style that
// best expresses its character - the mystic ones under the runes, the ones that lock things away
// under the cage, and so on. Several bosses share one style; no boss needs art of its own.
//
//   RUNIC     Seal + Rune circle         - mystic, cursed, memory and trickery
//   ECLIPSE   Eclipse + Blood eclipse    - darkness, nothingness, hunger and dread
//   COSMIC    Gravity well + Orbital     - forces and directions acting on the arena
//   INFERNO   Eruption + Lava lake       - demolition, mines, things burning up
//   CAGE      Lockdown + Iron cage       - cells, cards and your kit seized or locked
//   CREATURE  Alarm + Hazard tape        - something alive loose on the board, spreading
//   DECREE    Cinematic + Letterbox      - cold rules: taxes, bureaucracy, deadlines
//
// Presentation only: keyed by the boss's DefId, never read by Core. A boss missing from the table
// gets DECREE, so a new boss is never left without a look.
// EXTENSION POINT: moving a boss is one line; a new style is one enum value + one case in each switch.

using System.Collections.Generic;
using ProjectBlock.Core;

namespace ProjectBlock.View
{
    public enum BossTheme
    {
        Decree,
        Runic,
        Eclipse,
        Cosmic,
        Inferno,
        Cage,
        Creature
    }

    public static class BossThemes
    {
        private static readonly Dictionary<string, BossTheme> byBoss = new Dictionary<string, BossTheme>
        {
            // RUNIC - curses, rituals, tricks of the mind
            { "alzheimer", BossTheme.Runic },            // the board forgets
            { "sasirtmaca", BossTheme.Runic },           // shell game, cards face down
            { "bilinmezlik", BossTheme.Runic },          // the unknown: full lines do not explode
            { "terslik", BossTheme.Runic },              // your jokers are hexed against you
            { "matruska", BossTheme.Runic },             // the doll that keeps splitting
            { "vanilya", BossTheme.Runic },              // every block's element is stripped away
            { "feda", BossTheme.Runic },                 // sacrifice
            { "cana_gelecegine_mala", BossTheme.Runic }, // a ransom paid from your score

            // ECLIPSE - the light goes out; emptiness and hunger
            { "alacakaranlik", BossTheme.Eclipse },      // twilight: you play blind
            { "hiclik", BossTheme.Eclipse },             // nothingness
            { "kitlik", BossTheme.Eclipse },             // famine
            { "oburluk", BossTheme.Eclipse },            // gluttony
            { "cikmaz", BossTheme.Eclipse },             // the dead end

            // COSMIC - a force moves the arena, or scoring follows a direction
            { "merkezkac", BossTheme.Cosmic },           // centrifugal force
            { "dort_kutup", BossTheme.Cosmic },          // four poles
            { "yuruyen_merdiven", BossTheme.Cosmic },    // the board rides upward
            { "ufuk", BossTheme.Cosmic },                // horizon: only rows score
            { "kule", BossTheme.Cosmic },                // tower: only columns score

            // INFERNO - demolition, explosives, burning out
            { "istilaci", BossTheme.Inferno },           // a column is demolished
            { "mayin_esegi", BossTheme.Inferno },        // a hidden mine
            { "tukenmislik", BossTheme.Inferno },        // burnout: powers never refill
            { "enflasyon", BossTheme.Inferno },          // the bar keeps heating up

            // CAGE - something of yours is seized, sealed or held
            { "mapus", BossTheme.Cage },                 // cells sealed like a prison
            { "alikoyma", BossTheme.Cage },              // a card in your hand is seized
            { "tas_ve_sopa", BossTheme.Cage },           // all jokers and powers locked away
            { "anarsi", BossTheme.Cage },                // rare kit switched off
            { "bul_parayi", BossTheme.Cage },            // one piece of kit secretly confiscated
            { "rehin_puan", BossTheme.Cage },            // your score is held hostage

            // CREATURE - something alive on the board
            { "snake", BossTheme.Creature },
            { "kangren", BossTheme.Creature },           // the rot spreads
            { "karantina", BossTheme.Creature },         // an infected patch that moves
            { "tamagotchi", BossTheme.Creature },        // a pet that must be fed

            // DECREE - cold, bureaucratic rules
            { "harcama_vergisi", BossTheme.Decree },
            { "ozel_tuketim_vergisi", BossTheme.Decree },
            { "burokrasi_batagi", BossTheme.Decree },
            { "titizlik", BossTheme.Decree },
            { "saatci", BossTheme.Decree }
        };

        public static BossTheme For(string defId)
        {
            BossTheme theme;
            return defId != null && byBoss.TryGetValue(defId, out theme) ? theme : BossTheme.Decree;
        }

        public static BossIntroStyle Intro(BossTheme theme)
        {
            switch (theme)
            {
                case BossTheme.Runic: return BossIntroStyle.Seal;
                case BossTheme.Eclipse: return BossIntroStyle.Eclipse;
                case BossTheme.Cosmic: return BossIntroStyle.Orbit;
                case BossTheme.Inferno: return BossIntroStyle.Eruption;
                case BossTheme.Cage: return BossIntroStyle.Lockdown;
                case BossTheme.Creature: return BossIntroStyle.Alarm;
            }
            return BossIntroStyle.Cinematic;
        }

        public static BossAmbience Look(BossTheme theme)
        {
            switch (theme)
            {
                case BossTheme.Runic: return BossAmbience.RuneCircle;
                case BossTheme.Eclipse: return BossAmbience.BloodEclipse;
                case BossTheme.Cosmic: return BossAmbience.Orbital;
                case BossTheme.Inferno: return BossAmbience.LavaLake;
                case BossTheme.Cage: return BossAmbience.IronCage;
                case BossTheme.Creature: return BossAmbience.HazardTape;
            }
            return BossAmbience.Letterbox;
        }

        public static string Name(BossTheme theme)
        {
            switch (theme)
            {
                case BossTheme.Runic: return Loc.Pick("Runic", "Rünik");
                case BossTheme.Eclipse: return Loc.Pick("Eclipse", "Tutulma");
                case BossTheme.Cosmic: return Loc.Pick("Cosmic", "Kozmik");
                case BossTheme.Inferno: return Loc.Pick("Inferno", "Cehennem");
                case BossTheme.Cage: return Loc.Pick("Cage", "Kafes");
                case BossTheme.Creature: return Loc.Pick("Creature", "Yaratık");
            }
            return Loc.Pick("Decree", "Ferman");
        }
    }
}
