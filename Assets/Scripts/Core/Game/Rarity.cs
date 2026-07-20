// PURPOSE: How rare a joker/power is (Common/Rare/Legendary). Higher = rarer.
// Drives shop appearance odds and price multipliers.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>How rare a joker/power is. Higher = rarer.</summary>
    public enum Rarity
    {
        Common = 0,
        Rare = 1,
        Legendary = 2
    }
}
