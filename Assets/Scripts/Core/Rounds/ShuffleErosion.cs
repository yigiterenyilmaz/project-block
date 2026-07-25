// PURPOSE: How a round's arena erodes once the draw pile has run dry too many times. This is
// the anti-stalling clock: the first RoundRules.FreeDeckRecycles recycles are free, and every
// recycle after that eats a piece of the board, so no round can be farmed forever.
//
// The style is per round range, not per round - see DefaultRoundProgression.BoardSizeBands,
// where each band pairs a board size with the erosion it suffers.
//
// EXTENSION POINT: a new style is a new enum entry plus a branch in RoundEngine.ErodeOnce.
// Nothing else reads this value.

namespace ProjectBlock.Core
{
    /// <summary>Which way the play area is eaten away on each recycle past the free ones.</summary>
    public enum ShuffleErosion
    {
        /// <summary>The board never shrinks. Rounds can run as long as the deck allows.</summary>
        None = 0,

        /// <summary>The rim goes: one row and one column of the outer edge per erosion, taking
        /// alternating sides (top+right, then bottom+left, ...) so the arena stays centred.
        /// 5x5 becomes 4x4, then 3x3, then 2x2.</summary>
        FromOutside = 1,

        /// <summary>The middle goes: a square hole in the centre of the board, 1x1 on the first
        /// erosion, 2x2 on the second, 3x3 on the third. Those cells are DEAD - a row or column
        /// running through one of them can never be filled, so it can never explode again.</summary>
        FromCenter = 2,

        /// <summary>Both at once, every erosion: the rim shrinks AND the centre hole grows.</summary>
        Both = 3
    }
}
