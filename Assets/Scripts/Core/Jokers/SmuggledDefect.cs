// PURPOSE: What is wrong with a joker that came off the back of a lorry ("Kaçakçı"). The goods
// are free, and this is what free can cost you. Set once when the item is smuggled and never
// cleared - a defect is not a debuff, it is what the thing IS now.
// The defect on a BLOCK CARD needs no entry here: a junk card's defect is its shape. The defect
// on a POWER is a number, not a kind: Power.RechargeCost.

namespace ProjectBlock.Core
{
    /// <summary>How a smuggled joker is broken. None is every joker that was paid for.</summary>
    public enum SmuggledDefect
    {
        /// <summary>Sound goods.</summary>
        None = 0,

        /// <summary>Works normally, then goes quiet in exactly the rounds you needed it for:
        /// every boss round.</summary>
        DeadInBossRounds = 1,

        /// <summary>It never worked. You own a paperweight.</summary>
        NeverWorks = 2
    }
}
