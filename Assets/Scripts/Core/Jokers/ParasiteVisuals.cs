// PURPOSE: What the VIEW is told about "Parazit"'s HOST CUBE. REPORTING ONLY - every field here is
// a copy of something the rules already did, taken as they did it, and nothing in the rules reads
// it back.
//
// The host cube's whole identity is a NEGATIVE: it is the cube that does NOT break when a power
// hits it, does NOT move when a board carries everything else away, and does NOT count for a clean
// sweep - and then dies, with its passenger, to a line the player completed. A View given only the
// board state can see none of that. It can see a cube that is still there, which is exactly why the
// first pass could say nothing better than "this one is pink".
//
// So the board writes down every REFUSAL: which cell held, what tried it, and - when the rules knew
// one - the direction the force came from. That is what lets the parasite answer an attack by
// clamping harder on the side it arrived from, instead of playing one generic pulse.
//
// The refusals are cleared at the start of every turn and rebuilt by the rules as they run, exactly
// like the rest of TurnReport. They are not state and are never saved.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>What tried to take a host cube and was refused.</summary>
    public enum HostRefusalKind
    {
        /// <summary>A joker or power tried to destroy it (GameBoard.DestroyCube).</summary>
        Destroy = 0,

        /// <summary>A moving board tried to carry it off (GameBoard.DestroyCubeForced - the
        /// forced pickup a relocation starts with).</summary>
        ForcedMove = 1
    }

    /// <summary>One attempt on a host cube that the rules refused. Reporting only.</summary>
    public struct HostRefusal
    {
        /// <summary>The cell that held.</summary>
        public GridPos Cell;

        public HostRefusalKind Kind;

        /// <summary>The one-cell step the force was taking, when the rules knew one - so the
        /// parasite can clamp on the side it came from. Zero when the attempt had no direction
        /// (a power that simply names a cell), and the View must then play an undirected clamp
        /// rather than inventing a side.</summary>
        public GridPos Step;

        /// <summary>True when <see cref="Step"/> is a real direction rather than a default.
        /// </summary>
        public bool HasDirection;

        /// <summary>The cube that held, kept because a repaint may already have moved on.</summary>
        public Cube Cube;
    }

    /// <summary>
    /// Which joker is riding a host cube, for the View to show inside the parasite's node. The
    /// passenger's IDENTITY is the point: losing the cube means losing that joker, and a player who
    /// cannot see which one has not been told what the line clear is about to cost them.
    /// Reporting only.
    /// </summary>
    public struct BoundJokerIdentity
    {
        /// <summary>The bound joker's definition id ("robot_supurge", ...), or null when the
        /// parasite is idle. The View maps this to a colour; it must never guess from the host
        /// cube's own material, which says nothing about the passenger.</summary>
        public string DefId;

        /// <summary>Its display name, for a label or a tooltip.</summary>
        public string DisplayName;

        /// <summary>Its instance id, so the View can tell one passenger from another when the
        /// binding changes.</summary>
        public int InstanceId;

        /// <summary>False when nothing is bound.</summary>
        public bool Bound;
    }

    /// <summary>Every refusal a turn produced, in the order the rules made them.</summary>
    public sealed class HostRefusalLog
    {
        private readonly List<HostRefusal> refusals = new List<HostRefusal>();

        public IReadOnlyList<HostRefusal> Refusals
        {
            get { return refusals; }
        }

        public int Count
        {
            get { return refusals.Count; }
        }

        internal void Add(HostRefusal refusal)
        {
            refusals.Add(refusal);
        }

        /// <summary>Drops last turn's refusals. PUBLIC for the Animation Lab, which runs the
        /// real GameBoard.DestroyCube / DestroyCubeForced on a board of its own and needs each
        /// scene to start from an empty log - the same reason the rot's and the press's reporting
        /// entry points are public. Reporting only: clearing it changes nothing about the rules.
        /// </summary>
        public void Clear()
        {
            refusals.Clear();
        }

        /// <summary>The refusals on one cell, for a View driving that cell's parasite.</summary>
        public void CollectAt(GridPos cell, List<HostRefusal> into)
        {
            if (into == null)
            {
                return;
            }
            for (int i = 0; i < refusals.Count; i++)
            {
                if (refusals[i].Cell.Equals(cell))
                {
                    into.Add(refusals[i]);
                }
            }
        }
    }
}
