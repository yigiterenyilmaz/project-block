// PURPOSE: What the VIEW is told about "Hidrolik pres" - the squeeze, and the release four turns
// later. REPORTING ONLY - every field in here is a copy of something GameBoard.Press already did,
// written as it did it, and nothing in the rules ever reads it back.
//
// The press is the power whose PRESENTATION cannot be derived from before/after pictures. Four
// cells become one, and three turns later one cell becomes four again, pushing whatever is in the
// way. A View handed only the two board states would have to invent the whole middle - which cube
// went where, which quadrant was empty, which way the corner opened, which cube was shoved and how
// far, which side refused - and each of those guesses is a way for the animation to say something
// the rules did not do.
//
// So the board writes down, in order:
//
//   COMPRESSION   the four cells in patch order, what stood in each (null for an empty quadrant),
//                 and the cell the compressed cube now fills.
//   RELEASE       the cell it opened from, the four cells it wants back, which of them were
//                 already free, every PRESSURE TEST it made (the cell it pushed from, the axis it
//                 tried, and the immovable cube that refused, if one did), every cube it actually
//                 moved with its from/to (and whether the "to" is off the board), and - when no
//                 direction was open at all - that it detonated and which cells the failure took.
//
// THE SHAPE OF THE REAL RULES, which the View must not smooth over (see GameBoard.Press):
//
//   * The compressed cube stands on the patch's ANCHOR - its bottom-left cell - never its centre.
//   * There is no single expansion direction. The anchor is restored in place; the cell to its
//     RIGHT is pushed +x, the cell ABOVE it +y, and only the DIAGONAL (up-right) has a choice:
//     horizontal first, vertical if that is shut. That diagonal is the one place in the power
//     where "this way is blocked, so it opens the other way" happens at all.
//   * A straight cell that is blocked is therefore not rerouted - it detonates at once. Two
//     refusals in a row only ever happen on the diagonal.
//
// Reported, never recomputed: a View that counts cubes along a row or looks at a neighbour to see
// whether it is gold has started deciding the rules for itself.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Which single axis a push ran along. A diagonal want is always resolved to one of
    /// these - the shove is a straight line or it is nothing.</summary>
    public enum PressAxis
    {
        Horizontal = 0,
        Vertical = 1
    }

    /// <summary>One cube the press actually shoved, exactly one cell along one axis.</summary>
    public struct PressPush
    {
        /// <summary>Where it stood.</summary>
        public GridPos From;

        /// <summary>Where it went - OUTSIDE the board when it was shoved off the edge, so a View
        /// can carry it out rather than stopping it at the rim.</summary>
        public GridPos To;

        /// <summary>The one-cell step, as the rules took it.</summary>
        public GridPos Step;

        /// <summary>True when To is off the board: this cube is gone.</summary>
        public bool LeftBoard;

        /// <summary>The cube itself, kept because the cell it stood in may already hold the one
        /// that slid in behind it.</summary>
        public Cube Cube;

        /// <summary>Which of the three wanted cells this shove was clearing the way for.</summary>
        public GridPos ForWantedCell;

        /// <summary>How far down the line of shoved cubes this one was, 0 at the press end. The
        /// pressure reaches the near cube first, and this is that order - not a guess at it.</summary>
        public int Order;
    }

    /// <summary>
    /// One push the press TRIED: the wanted cell it was clearing, the axis it tried it on, and
    /// whether anything refused. A refusal names the immovable cube that did it, which is the
    /// whole reason the side is shut - so the View can press on a gold cube that does not budge
    /// instead of inventing a shield for it.
    /// </summary>
    public struct PressureTest
    {
        /// <summary>The cell of the 2x2 this test was trying to empty.</summary>
        public GridPos WantedCell;

        public PressAxis Axis;

        /// <summary>The one-cell step the shove would have taken.</summary>
        public GridPos Step;

        /// <summary>True when the line moved. False when something in it could not be moved.</summary>
        public bool Succeeded;

        /// <summary>The obsidian or gold cube that refused, when one did.</summary>
        public GridPos BlockedAt;

        /// <summary>What that cube was - gold and obsidian do not look alike and must not be drawn
        /// alike.</summary>
        public CubeKind BlockedKind;

        /// <summary>True when this test is the SECOND one on the same wanted cell - the diagonal
        /// having its horizontal refused and its vertical tried instead. The only reroute in the
        /// power, and the View must never stage one anywhere else.</summary>
        public bool IsReroute;
    }

    /// <summary>The squeeze, as it happened. Reporting only.</summary>
    public sealed class PressCompressionVisuals
    {
        internal PressCompressionVisuals(GridPos anchor)
        {
            Anchor = anchor;
        }

        /// <summary>The patch's bottom-left cell - and the cell the compressed cube now fills.
        /// They are the same cell: the press does not move to a centre.</summary>
        public GridPos Anchor { get; private set; }

        private readonly List<GridPos> cells = new List<GridPos>();

        /// <summary>The four cells, in the order the rules swallow and restore them: anchor,
        /// right, up, up-right.</summary>
        public IReadOnlyList<GridPos> Cells
        {
            get { return cells; }
        }

        private readonly List<Cube?> swallowed = new List<Cube?>();

        /// <summary>What stood in each of those cells - NULL for a quadrant that was empty. The
        /// nulls are the point: the press stores the picture it swallowed, holes and all, and the
        /// View has to be able to say so.</summary>
        public IReadOnlyList<Cube?> Swallowed
        {
            get { return swallowed; }
        }

        /// <summary>Where the compressed cube ended up. Always Anchor; named separately because
        /// that is what the animation converges on, and because the rules choosing a different
        /// cell one day must not silently move the animation's target.</summary>
        public GridPos CompressedCell
        {
            get { return Anchor; }
        }

        /// <summary>How many of the four quadrants actually held a cube.</summary>
        public int OccupiedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < swallowed.Count; i++)
                {
                    if (swallowed[i].HasValue)
                    {
                        n++;
                    }
                }
                return n;
            }
        }

        internal void Add(GridPos cell, Cube? cube)
        {
            cells.Add(cell);
            swallowed.Add(cube);
        }
    }

    /// <summary>The release, as it happened. Reporting only.</summary>
    public sealed class PressReleaseVisuals
    {
        internal PressReleaseVisuals(GridPos anchor)
        {
            Anchor = anchor;
        }

        /// <summary>The cell the compressed cube stood in and opened from.</summary>
        public GridPos Anchor { get; private set; }

        private readonly List<GridPos> cells = new List<GridPos>();

        /// <summary>The four cells it wants back, in patch order. Index 0 is the anchor itself,
        /// which is never pushed for - it is already the press's own cell.</summary>
        public IReadOnlyList<GridPos> Cells
        {
            get { return cells; }
        }

        private readonly List<Cube?> stored = new List<Cube?>();

        /// <summary>What goes back into each of those cells - null where the quadrant was empty
        /// when it was swallowed, and still has to end up empty.</summary>
        public IReadOnlyList<Cube?> Stored
        {
            get { return stored; }
        }

        private readonly List<GridPos> alreadyFree = new List<GridPos>();

        /// <summary>The wanted cells that needed no shove at all. A clean release is this list
        /// holding all three.</summary>
        public IReadOnlyList<GridPos> AlreadyFree
        {
            get { return alreadyFree; }
        }

        private readonly List<PressureTest> tests = new List<PressureTest>();

        /// <summary>Every push the press TRIED, in the order it tried them - refusals included.
        /// This is the sequence the View plays: a side pressed and denied is in here, and so is
        /// the diagonal's reroute onto its other axis.</summary>
        public IReadOnlyList<PressureTest> Tests
        {
            get { return tests; }
        }

        private readonly List<PressPush> pushes = new List<PressPush>();

        /// <summary>Every cube that actually moved, with where it came from and where it went.
        /// The View never works a push chain out for itself.</summary>
        public IReadOnlyList<PressPush> Pushes
        {
            get { return pushes; }
        }

        /// <summary>True when nothing could be moved and the press blew instead.</summary>
        public bool Detonated { get; internal set; }

        private readonly List<GridPos> detonatedCells = new List<GridPos>();

        /// <summary>Exactly the cells the failure emptied - the press itself and the surrounding
        /// gold and obsidian, which is the one event in the game that removes those. The burst's
        /// visual footprint is THIS, never a radius of its own choosing.</summary>
        public IReadOnlyList<GridPos> DetonatedCells
        {
            get { return detonatedCells; }
        }

        private readonly List<CubeKind> detonatedKinds = new List<CubeKind>();

        /// <summary>What stood in each detonated cell, index for index with DetonatedCells. Gold
        /// and obsidian are sheared, the press's own shell is not, and nothing else is in here at
        /// all.</summary>
        public IReadOnlyList<CubeKind> DetonatedKinds
        {
            get { return detonatedKinds; }
        }

        /// <summary>How many cubes went over the edge. The power scores for these.</summary>
        public int CubesPushedOff { get; internal set; }

        /// <summary>The axis the DIAGONAL cell finally opened on, or null when it needed no shove
        /// or the press never got that far. The one direction choice the rules actually make.</summary>
        public PressAxis? DiagonalAxis { get; internal set; }

        /// <summary>True when a side was pressed, refused, and the press opened the other way
        /// instead - the diagonal's reroute, and nothing else.</summary>
        public bool Rerouted
        {
            get
            {
                for (int i = 0; i < tests.Count; i++)
                {
                    if (tests[i].IsReroute && tests[i].Succeeded)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        internal void AddCell(GridPos cell, Cube? cube)
        {
            cells.Add(cell);
            stored.Add(cube);
        }

        internal void AddAlreadyFree(GridPos cell)
        {
            alreadyFree.Add(cell);
        }

        internal void AddTest(PressureTest test)
        {
            tests.Add(test);
        }

        internal void AddPush(PressPush push)
        {
            pushes.Add(push);
        }

        internal void AddDetonated(GridPos cell, CubeKind kind)
        {
            detonatedCells.Add(cell);
            detonatedKinds.Add(kind);
        }
    }
}
