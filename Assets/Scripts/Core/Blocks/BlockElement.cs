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

        /// <summary>Indestructible and ignored by the clean-sweep check - and PAID for it: every
        /// row or column that goes off through an obsidian cube pays a bonus for it
        /// (ScoringConfig.PointsPerObsidianInLine). The stone survives the clear, so the same
        /// cube pays again every time you complete a line through it.</summary>
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

        /// <summary>"Kara Delik" joker only: a 1x1 BLACK HOLE. It may be laid on any cell, a
        /// filled one included (what stands there is swallowed), and once down nothing removes or
        /// moves it; a cube that lands on it falls in. The card is spent when it is laid, and
        /// these cards never join the owned deck. See KaraDelikJoker.</summary>
        Void = 10,

        /// <summary>The anti-block: it may be placed ON TOP of existing cubes and ERASES
        /// them. Nothing is left behind - the negative block goes with what it deleted, so
        /// the cells end up empty. Indestructible cubes (obsidian, gold) refuse it, which is
        /// what stops it from being a universal solvent.</summary>
        Negative = 11,

        /// <summary>"Hedefli": one marked cube of the block is its TARGET (BlockCard
        /// .TargetCellIndex, stamped onto the board as CubeKind.Target). Break the target in the
        /// first explosion that touches the block and it pays a bonus and takes the whole block
        /// with it; break anything else first and the block is spent - it stands there as
        /// ordinary cubes for the rest of its stay. See RoundEngine.Targeted.</summary>
        Targeted = 12

        // Mirror ("ayna") and PiggyBank ("kumbara") were cut from the design 2026-07-18.
    }
}
