// PURPOSE: The block types ("blok türleri") from the design plan. A card usually has
// zero or one element; the future "Simya" joker deals market blocks with two.
// IMPLEMENTED so far: Fire, Obsidian, Gold, Dynamite, PiggyBank (see RoundEngine /
// GameBoard). The rest are declared for the model but NOT yet in the market pool -
// implement their behavior before adding them to MarketConfig.ElementPool.

namespace ProjectBlock.Core
{
    /// <summary>Special block types. See the design plan for intended behaviors.</summary>
    public enum BlockElement
    {
        /// <summary>When one cube of the block explodes, the whole block explodes.</summary>
        Fire = 0,

        /// <summary>Settles every turn (falls, spreads diagonally); turns touching fire
        /// to obsidian and persists.</summary>
        Water = 1,

        /// <summary>Ignored by the clean-sweep check, but indestructible.</summary>
        Obsidian = 2,

        /// <summary>Sweep-exempt and indestructible; pays a per-turn bonus proportional
        /// to its cube count while it sits on the board.</summary>
        Gold = 3,

        /// <summary>Other blocks can be placed on top of it; the new cube replaces it.</summary>
        Transparent = 4,

        /// <summary>Placeable partially outside the board (at least one cube inside);
        /// outside cubes persist as ghostly traces for the future Tılsım power.</summary>
        Ghost = 5,

        /// <summary>If the whole block explodes at once on the turn it was placed,
        /// the entire board is cleared.</summary>
        Dynamite = 6,

        /// <summary>Rotatable 90° per right-click while in hand (RoundEngine.RotateCard).</summary>
        Mechanical = 7,

        /// <summary>Reshapeable into any shape that exists in the current deck
        /// (RoundEngine.SetFoxShape; right-click opens the picker).</summary>
        Fox = 9,

        /// <summary>"Kara delik" joker only: a 1x1 trap. Blocks may be placed on top of it,
        /// but the cube that lands there is destroyed on contact and the void goes with it.
        /// Round-scoped - these cards never join the owned deck.</summary>
        Void = 10

        // Mirror ("ayna") and PiggyBank ("kumbara") were cut from the design 2026-07-18.
    }
}
