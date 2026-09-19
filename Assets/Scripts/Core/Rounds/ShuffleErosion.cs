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
        /// 7x7 becomes 6x6, then 5x5, then 4x4.</summary>
        FromOutside = 1,

        /// <summary>A DEAD ZONE spreads from the centre: 1x1 on the first erosion, 3x3 on the
        /// second, 5x5 on the third, and the fourth ends the round. Blocks can still be placed in
        /// it and its lines still explode, but a row or column touching it scores nothing.</summary>
        FromCenter = 2,

        /// <summary>Both at once, every erosion: the rim shrinks AND the dead zone grows.</summary>
        Both = 3
    }
}
