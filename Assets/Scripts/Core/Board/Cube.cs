// PURPOSE: One occupied cell on the board and the central rule table about cube kinds.
// EXTENSION POINT (IMPORTANT FOR FUTURE AGENTS): all elemental block types from the
// design plan (fire, water, obsidian, gold, ghost, dynamite, transparent, mechanical,
// mirror, fox, piggy bank, ice, void...) start here:
//   1. add a CubeKind member,
//   2. teach CubeRules how the kind behaves (counts for clean sweep? destructible?),
//   3. add the kind-specific behavior where the board resolves explosions/placement.
// The base game only has Normal cubes, but ALL core logic already asks CubeRules
// instead of assuming behavior, so new kinds plug in without touching the flow.

namespace ProjectBlock.Core
{
    /// <summary>The element/type of a placed cube. Base game: Normal only.</summary>
    public enum CubeKind
    {
        Normal = 0
        // Future: Fire, Water, Obsidian, Gold, Ghost, Dynamite, Transparent,
        // Mechanical, Mirror, Fox, PiggyBank, Ice, Void ...
    }

    /// <summary>A cube occupying one board cell.</summary>
    public readonly struct Cube
    {
        public readonly CubeKind Kind;

        /// <summary>Id of the BlockCard that placed this cube (whole-block effects need it,
        /// e.g. fire spreading to the rest of the block, "Kazı çalışması" joker).</summary>
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
        /// <summary>Does this cube block a clean sweep ("temizlik")? Obsidian/gold later: no.</summary>
        public static bool CountsForCleanSweep(Cube cube)
        {
            return true;
        }

        /// <summary>Can a line explosion destroy this cube? Obsidian/map-placed gold later: no.</summary>
        public static bool IsDestructible(Cube cube)
        {
            return true;
        }
    }
}
