// PURPOSE: "Barut tedarikçisi" telling the View what its powder did this turn - which cells took
// another charge and how close to the cap each one now is. REPORTING ONLY: the rules have already
// banked the charges, and the View never counts a charge, never reads the board and never works
// out which cube belongs to which block.
//
// WHY THE CELL AND NOT THE CARD. Charges are banked per CARD (a block that loses half its cubes
// keeps what it earned), but the player sees CUBES - so the joker flattens its own per-card
// bookkeeping into the cells standing right now. A block of four cubes reports four cells at the
// same charge, which is exactly what the sizzle and the fuse marks want: the whole block lights,
// because the whole block is what is charging.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One turn of powder. A NEW object per turn, matched by identity (never by a
    /// serial - see the per-turn report rule in CLAUDE.md).</summary>
    public sealed class PowderVisuals
    {
        /// <summary>Every dynamite cell that took a charge this turn, in board order.</summary>
        public readonly List<GridPos> Cells = new List<GridPos>();

        /// <summary>Charges its BLOCK holds now, same order and same value for every cube of one
        /// block.</summary>
        public readonly List<int> Charges = new List<int>();

        /// <summary>How full each one is, 0 to 1 against the cap - what the fuse's pitch and the
        /// mark's intensity ride on. Carried rather than derived so the View never needs to know
        /// what the cap is.</summary>
        public readonly List<float> Fullness = new List<float>();

        /// <summary>True for a cell whose block has hit the cap and will take no more.</summary>
        public readonly List<bool> Full = new List<bool>();

        /// <summary>
        /// True for a cell whose block took a charge THIS turn.
        ///
        /// It is the difference between the EVENT and the STATE, and both have to be reported or
        /// the View cannot tell them apart: a capped block gains nothing and must not sizzle or
        /// spark again, but its ember has to STAY LIT until the cubes are actually gone - it is
        /// still holding all that powder, which is the whole thing the player is deciding about.
        /// Reporting only the gainers made a block go dark at the exact moment it became most
        /// valuable.
        /// </summary>
        public readonly List<bool> Gained = new List<bool>();

        public int Count
        {
            get { return Cells.Count; }
        }

        internal void Add(GridPos cell, int charges, int cap, bool gained)
        {
            Cells.Add(cell);
            Charges.Add(charges);
            Fullness.Add(cap > 0 ? charges / (float)cap : 1f);
            Full.Add(charges >= cap);
            Gained.Add(gained);
        }

        /// <summary>True if anything actually took a charge this turn - what the sizzle asks,
        /// so a board of nothing but capped blocks stays quiet.</summary>
        public bool AnyGained
        {
            get
            {
                for (int i = 0; i < Gained.Count; i++)
                {
                    if (Gained[i])
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
