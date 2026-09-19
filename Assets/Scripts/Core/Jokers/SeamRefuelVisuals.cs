// PURPOSE: What "Yer altı kaynakları" pumped into which powers this turn, written down for the
// VIEW. Reporting only: the refuel itself is unchanged and the rules never read this back.
//
// IT EXISTS BECAUSE THE VIEW CANNOT WORK THIS OUT, and the reason is worth stating.
//
// WHICH POWERS. A power going from spent to charged is not enough to identify a refuel: a clean
// sweep refills every spent power, a new round refills them all, and "Hazine" refills one - all
// through the same PowerInventory.Recharge this joker uses. A View that watched the charged flag
// would draw the seam's animation on all four events and be wrong on three of them. So the report
// is written exactly where the seam paid for the fuel, and names the instances it paid for.
//
// AND HOW MANY. The seam stops mid-tier the moment it cannot afford the next power, so a tick
// that was due for three powers may deliver two - and the one that went short is the interesting
// one. CapacitySpent/CapacityLeft carry what it actually cost, so the picture can say the seam is
// running down without the View owning a copy of the capacity rules.
//
// A tick that delivered NOTHING (every power already charged, a boss forbidding refills, an
// exhausted seam) writes no report at all, so there is nothing to draw and nothing to suppress.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>
    /// One delivery of fuel. A NEW object every time, matched by IDENTITY in the View - never by
    /// a serial, which restarts at 1 with every new joker while the views outlive a run.
    /// </summary>
    public sealed class SeamRefuelVisuals
    {
        /// <summary>The powers actually refilled, by instance id, in the order the seam paid for
        /// them. Never empty - a delivery of nothing is not reported.</summary>
        public List<int> PowerInstanceIds = new List<int>();

        /// <summary>Which clock delivered this: Common or Rare. The two are separate ticks and a
        /// turn due for both writes two reports.</summary>
        public Rarity Tier;

        /// <summary>Capacity this delivery cost, in seam points.</summary>
        public int CapacitySpent;

        /// <summary>Capacity left AFTER it, and the seam's full size - so the picture can show how
        /// far down the seam is without re-deriving the cost rules.</summary>
        public int CapacityLeft;

        public int Capacity;

        /// <summary>True when this delivery worked the seam out. The last pump is the one worth
        /// drawing differently.</summary>
        public bool Exhausted
        {
            get { return CapacityLeft <= 0; }
        }
    }
}
