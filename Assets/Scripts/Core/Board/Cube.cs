// PURPOSE: One occupied cell on the board and the central rule table about cube kinds.
// A cube's kind comes from its card's element (CubeRules.KindForCard). Query CubeRules
// for behavior - never hardcode kind checks in the flow.
// IMPLEMENTED kinds: Normal, Fire (chain explosion, handled in GameBoard), Obsidian,
// Gold (both sweep-exempt + indestructible; gold also pays a per-turn bonus, handled
// in RoundEngine), PiggyBank (accrues value, pays on destruction - RoundEngine).
// Water and Transparent exist in the enum but their behaviors are NOT implemented yet;
// keep them out of MarketConfig.ElementPool until they are.

namespace ProjectBlock.Core
{
    /// <summary>The element/type of a placed cube.</summary>
    public enum CubeKind
    {
        Normal = 0,
        Fire = 1,
        Water = 2,
        Obsidian = 3,
        Gold = 4,
        Transparent = 5,
        Dynamite = 7,

        /// <summary>"Kara delik" trap: placeable-over like Transparent, but the cube that
        /// lands on it is destroyed instead, and the void is consumed. Indestructible and
        /// sweep-exempt, so it survives line explosions/sweeps and persists until sprung.</summary>
        Void = 9,

        /// <summary>"Mayın" power: an armed empty cell. Behaves like Void on contact - the
        /// arriving cube goes up with the mine - but it is the player's own trap.</summary>
        Mine = 10,

        /// <summary>Frozen water ("Buzluk" joker). Unlike obsidian/gold it CAN be exploded
        /// and pays a bonus when it is, but it does not block a clean sweep - a board that
        /// holds nothing but ice counts as swept.</summary>
        Ice = 8
    }

    /// <summary>A cube occupying one board cell.</summary>
    public readonly struct Cube
    {
        public readonly CubeKind Kind;

        /// <summary>Id of the BlockCard that placed this cube (whole-block effects need it:
        /// fire chains, dynamite, piggy banks, "Kazı çalışması" joker...).</summary>
        public readonly int SourceCardId;

        /// <summary>"Parazit" host cube: sweep-exempt and immune to joker/power destruction.
        /// It still breaks with a player line explosion, which is the only thing that can
        /// take out the parasite's passenger. Preserved by struct copies (snapshots, resize).</summary>
        public readonly bool Protected;

        public Cube(CubeKind kind, int sourceCardId)
            : this(kind, sourceCardId, false)
        {
        }

        public Cube(CubeKind kind, int sourceCardId, bool isProtected)
        {
            Kind = kind;
            SourceCardId = sourceCardId;
            Protected = isProtected;
        }

        /// <summary>A copy of this cube marked as a Parazit host.</summary>
        public Cube AsProtected()
        {
            return new Cube(Kind, SourceCardId, true);
        }
    }

    /// <summary>Central per-kind rule answers. Query this, never hardcode kind behavior.</summary>
    public static class CubeRules
    {
        /// <summary>Does this cube block a clean sweep ("temizlik")? Obsidian, gold and ice
        /// are the confirmed exceptions, and a Parazit host cube is exempt too. Void is exempt
        /// as well: a "Kara delik" trap persists through sweeps, so a board holding nothing but
        /// void counts as swept.</summary>
        public static bool CountsForCleanSweep(Cube cube)
        {
            return !cube.Protected
                && cube.Kind != CubeKind.Obsidian
                && cube.Kind != CubeKind.Gold
                && cube.Kind != CubeKind.Ice
                && cube.Kind != CubeKind.Void;
        }

        /// <summary>Can a LINE EXPLOSION destroy this cube? Obsidian and gold never break, and
        /// void does not either - a "Kara delik" trap is indestructible and only vanishes when a
        /// cube lands on it (consumed on contact in GameBoard.Place). A Parazit host IS
        /// destructible here on purpose - a player-completed line is the only thing that breaks it.</summary>
        public static bool IsDestructible(Cube cube)
        {
            return cube.Kind != CubeKind.Obsidian
                && cube.Kind != CubeKind.Gold
                && cube.Kind != CubeKind.Void;
        }

        /// <summary>Can an EXTERNAL effect (a joker or power) destroy this cube? Same as
        /// IsDestructible, except a Parazit host cube resists all of them.</summary>
        public static bool IsExternallyDestructible(Cube cube)
        {
            return !cube.Protected && IsDestructible(cube);
        }

        /// <summary>The cube kind a card's cubes take when placed (first board-state
        /// element wins; placement-trait elements like Ghost/Mechanical leave Normal cubes).</summary>
        public static CubeKind KindForCard(BlockCard card)
        {
            if (card.Has(BlockElement.Fire)) return CubeKind.Fire;
            if (card.Has(BlockElement.Water)) return CubeKind.Water;
            if (card.Has(BlockElement.Obsidian)) return CubeKind.Obsidian;
            if (card.Has(BlockElement.Gold)) return CubeKind.Gold;
            if (card.Has(BlockElement.Transparent)) return CubeKind.Transparent;
            if (card.Has(BlockElement.Dynamite)) return CubeKind.Dynamite;
            if (card.Has(BlockElement.Void)) return CubeKind.Void;
            return CubeKind.Normal;
        }
    }
}
