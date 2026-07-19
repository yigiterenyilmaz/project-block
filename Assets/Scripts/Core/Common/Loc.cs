// PURPOSE: The one language switch of the game. Core text (joker/power descriptions,
// status words) and View text (HUD, market, tooltips) all route through Loc.Pick, so
// flipping Language re-texts the whole game live - callers re-read strings on their next
// refresh rather than caching them (JokerDefinition/PowerDefinition read live too).
// RULE FOR AGENTS: never store a picked string long-term; store the (en, tr) pair or
// re-call Pick at display time.

namespace ProjectBlock.Core
{
    /// <summary>Supported display languages.</summary>
    public enum GameLanguage
    {
        English = 0,
        Turkish = 1
    }

    /// <summary>Central language state + string picker. Pure state: persistence (e.g.
    /// PlayerPrefs) belongs to the platform layer that sets Language at startup.</summary>
    public static class Loc
    {
        /// <summary>The active display language. Default English.</summary>
        public static GameLanguage Language = GameLanguage.English;

        /// <summary>Returns the string matching the active language.</summary>
        public static string Pick(string english, string turkish)
        {
            return Language == GameLanguage.Turkish ? turkish : english;
        }
    }
}
