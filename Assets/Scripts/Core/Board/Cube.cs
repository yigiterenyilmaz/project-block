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
        Dynamite = 7
    }

    /// <summary>A cube occupying one board cell.</summary>
    public readonly struct Cube
    {
        public readonly CubeKind Kind;

        /// <summary>Id of the BlockCard that placed this cube (whole-block effects need it:
        /// fire chains, dynamite, piggy banks, "Kazı çalışması" joker...).</summary>
        public readonly int SourceCardId;

        public Cube(CubeKind kind, int sourceCardId)
        {
            Kind = kind;
            SourceCardId = sourceCardId;
        }
    }

    /// <summary>Central per-kind rule answers. Query this, never hardcode kind behavior.</summary>
    public static class CubeRules
    {
        /// <summary>Does this cube block a clean sweep ("temizlik")? Obsidian and gold
        /// are the confirmed exceptions.</summary>
        public static bool CountsForCleanSweep(Cube cube)
        {
            return cube.Kind != CubeKind.Obsidian && cube.Kind != CubeKind.Gold;
        }

        /// <summary>Can an explosion destroy this cube?</summary>
        public static bool IsDestructible(Cube cube)
        {
            return cube.Kind != CubeKind.Obsidian && cube.Kind != CubeKind.Gold;
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
            return CubeKind.Normal;
        }
    }
}
