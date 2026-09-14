// PURPOSE: What "Tılsım" did, written down for the VIEW. Reporting only: nothing here decides
// anything, nothing here is saved, and the rules read exactly the same with it as without it.
//
// This power is unusual in that its story crosses a ROUND BOUNDARY - and a market screen with it -
// so one report is not enough. It is paid for now and delivered later:
//
//   ACTIVATION  the ghost cubes hanging off the board are harvested. Every one of them scores, but
//               only the ones the rules can actually reclaim ground from (the board never grows
//               left or down) leave anything behind. The View must not work that split out for
//               itself - "which of these is reclaimable" is a rule, and it lives in the power.
//               The ghosts are also GONE the moment the power runs, so their faces are snapshotted
//               here before they go; an animation that starts from the block's own colour cannot
//               start from a block that has already been deleted.
//   REVEAL      the ground arrives when the NEXT round's board is built, which is a different
//               round, after a market. What the View has to unwrap is the set of cells that board
//               actually got - not what the activation hoped for, and not something derived from
//               the previous round's drawing.
//   RECALL      and at the end of that round the gift is taken back. The same set, going the other
//               way.
//
// The power keeps its copies [NotSaved]: they are per-event presentation data, rebuilt by the next
// activation, and meaningless across a load. See SnakeTurnVisuals, PressCompressionVisuals,
// MapusSealVisuals - the same bargain.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One ghost cube as it stood when the power took it - its cell and the cube itself,
    /// so the harvest can begin from the block's OWN face and colour.</summary>
    public struct HarvestedGhost
    {
        public GridPos Cell;

        public Cube Cube;

        /// <summary>True when the rules can reclaim ground here. Every ghost scores; only these
        /// leave a talisman seed and get wrapped. Worked out by the power from the same condition
        /// the conversion uses, never guessed from the coordinate in the View.</summary>
        public bool Reclaimable;
    }

    /// <summary>The activation: what was harvested, what it paid, and what it claimed.</summary>
    public sealed class TalismanActivationVisuals
    {
        public readonly List<HarvestedGhost> Ghosts = new List<HarvestedGhost>();

        /// <summary>Just the cells that will become ground next round - the subset the claim
        /// animation wraps.</summary>
        public readonly List<GridPos> Reclaimed = new List<GridPos>();

        public int ScorePerGhost;

        public int TotalScore;

        /// <summary>Bumped on every activation, so a repaint can tell a new harvest from the one
        /// it is already playing without comparing lists.</summary>
        public int Serial;

        public void Clear()
        {
            Ghosts.Clear();
            Reclaimed.Clear();
            ScorePerGhost = 0;
            TotalScore = 0;
        }
    }

    /// <summary>
    /// The ground as the NEXT round's board actually received it. Written when that board's config
    /// is built, and deliberately NOT cleared when the round starts: the power's own working list
    /// is reset there, and the View still has an unwrapping to play.
    /// </summary>
    public sealed class TalismanGroundVisuals
    {
        /// <summary>The cells this power put into the board being built. Both lists it feeds are
        /// the same cells - bonus ground is extra play area AND optional at once - so one list is
        /// the honest report.</summary>
        public readonly List<GridPos> Cells = new List<GridPos>();

        /// <summary>Bumped each time ground is handed over, so the View can tell a fresh gift from
        /// the one it has already unwrapped.</summary>
        public int Serial;

        public bool Any
        {
            get { return Cells.Count > 0; }
        }

        public void Clear()
        {
            Cells.Clear();
        }
    }
}
