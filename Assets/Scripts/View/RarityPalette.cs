// PURPOSE: The one place that turns a Core Rarity into presentation - accent colour,
// body tint and the localized tier label. Every surface that shows a joker or a power
// (joker/power bars, market tiles, the debug grant picker, tooltips) reads it from here
// so a rare item looks the same everywhere.
// EXTENSION POINT: a new rarity tier only needs an entry in Accent/Label; the tint and
// frame helpers derive from the accent.

using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Rarity -> colours and labels. Common stays neutral (unchanged look), rare
    /// is blue, legendary is gold.</summary>
    public static class RarityPalette
    {
        private static readonly Color CommonAccent = new Color(0.78f, 0.82f, 0.90f);
        private static readonly Color RareAccent = new Color(0.38f, 0.72f, 1f);
        private static readonly Color LegendaryAccent = new Color(1f, 0.78f, 0.30f);

        /// <summary>The tier's signature colour - used for titles, tags and edge strips.</summary>
        public static Color Accent(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return RareAccent;
                case Rarity.Legendary: return LegendaryAccent;
                default: return CommonAccent;
            }
        }

        /// <summary>Tints a panel/tile body toward the tier colour, keeping enough of the
        /// original so joker (purple) and power (teal) tiles stay tellable apart.</summary>
        public static Color Tint(Color body, Rarity rarity)
        {
            return rarity == Rarity.Common ? body : Color.Lerp(body, Accent(rarity), 0.26f);
        }

        /// <summary>Border colour for a tile of this tier (common keeps the neutral frame).</summary>
        public static Color Frame(Color frame, Rarity rarity)
        {
            return rarity == Rarity.Common ? frame : Color.Lerp(Accent(rarity), frame, 0.25f);
        }

        /// <summary>Localized tier word, or null for common (which shows no tier tag).</summary>
        public static string Label(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return Loc.Pick("RARE", "NADİR");
                case Rarity.Legendary: return Loc.Pick("LEGENDARY", "EFSANEVİ");
                default: return null;
            }
        }

        /// <summary>Rarity of a held joker (the bars only have instances, not definitions).</summary>
        public static Rarity Of(Joker joker)
        {
            return joker == null ? Rarity.Common : RarityTable.For(joker.DefId);
        }

        /// <summary>Rarity of a held power.</summary>
        public static Rarity Of(Power power)
        {
            return power == null ? Rarity.Common : RarityTable.For(power.DefId);
        }
    }
}
