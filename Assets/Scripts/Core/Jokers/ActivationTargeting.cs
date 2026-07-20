// PURPOSE: What a player-activated joker needs to be pointed at (none, a board
// cell, a hand card...). The UI reads it to know what click to wait for.

namespace ProjectBlock.Core
{
    /// <summary>What an activated joker needs to be pointed at before it can run. The UI
    /// reads this to decide whether to ask for a target first.</summary>
    public enum ActivationTargeting
    {
        /// <summary>Fires immediately (Renovasyon).</summary>
        None = 0,

        /// <summary>Needs a card in hand (İade).</summary>
        HandCard = 1,

        /// <summary>Needs a board cell (future: Enfeksiyon).</summary>
        BoardCell = 2
    }
}
