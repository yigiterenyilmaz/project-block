// PURPOSE: "Kara Delik"'s reports for the View - what the holes did this turn, and what the
// gravity swallowed when it lost control. Reporting only: nothing in the rules reads them, they are
// never saved, and each is a NEW object per event so a view matches it by identity.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One cube a hole ate this turn, and which hole ate it.</summary>
    public readonly struct BlackHoleBite
    {
        public readonly GridPos Hole;
        public readonly DestroyedCube Cube;

        public BlackHoleBite(GridPos hole, DestroyedCube cube)
        {
            Hole = hole;
            Cube = cube;
        }
    }

    /// <summary>One cube a hole pulled a step closer this turn.</summary>
    public readonly struct BlackHolePull
    {
        public readonly GridPos Hole;
        public readonly GridPos From;
        public readonly GridPos To;
        public readonly Cube Cube;

        public BlackHolePull(GridPos hole, GridPos from, GridPos to, Cube cube)
        {
            Hole = hole;
            From = from;
            To = to;
            Cube = cube;
        }
    }

    /// <summary>What the black holes did on one turn, in the order it happened: the placement
    /// swallows (a void card laid over cubes, cubes that landed on a hole), the bites (ring 1),
    /// the pulls (ring 2 -> ring 1), and - when the count reached the arena's size - the collapse
    /// that took every other cube and set the sweep off.</summary>
    public sealed class BlackHoleVisuals
    {
        /// <summary>Every hole on the board when the turn's gravity ran.</summary>
        public readonly List<GridPos> Holes = new List<GridPos>();

        public readonly List<DestroyedCube> PlacementSwallows = new List<DestroyedCube>();
        public readonly List<BlackHoleBite> Bites = new List<BlackHoleBite>();
        public readonly List<BlackHolePull> Pulls = new List<BlackHolePull>();

        /// <summary>True when the swallowed count reached the arena's size this turn.</summary>
        public bool Collapsed;

        /// <summary>What the collapse destroyed (each cube as it stood).</summary>
        public readonly List<DestroyedCube> CollapseCubes = new List<DestroyedCube>();

        /// <summary>Whether the collapse's sweep actually went off.</summary>
        public bool SweepFired;

        /// <summary>Score the joker paid this turn (swallows + collapse), MEASURED around the
        /// payment, at the score's scale.</summary>
        public int Points;

        /// <summary>Score per swallowed (or collapsed) cube, at the score's scale.</summary>
        public int PointsEach;

        /// <summary>The count before this turn's swallows, after them (0 again after a collapse),
        /// and what it needs.</summary>
        public int SwallowedBefore;
        public int SwallowedAfter;
        public int Goal;

        /// <summary>Everything swallowed this turn: placement swallows plus bites.</summary>
        public int SwallowedThisTurn
        {
            get { return PlacementSwallows.Count + Bites.Count; }
        }
    }

    /// <summary>The gravity losing control: one pile of cards swallowed for the rest of the round.</summary>
    public sealed class DeckSwallowVisuals
    {
        /// <summary>True for the draw pile, false for the discard.</summary>
        public bool FromDrawPile;

        /// <summary>The cards that went, top of the pile first.</summary>
        public readonly List<int> CardIds = new List<int>();

        /// <summary>Which malfunction of the round this was (1 or 2).</summary>
        public int Number;

        /// <summary>How many cards the other pile still had.</summary>
        public int OtherPileCount;
    }
}

namespace ProjectBlock.Core
{
    /// <summary>One attempt to remove, move, retype or cover a "Kara Delik" hole that the board
    /// refused. Reporting only - the rules never read it back.</summary>
    public readonly struct AnchorRefusal
    {
        public readonly GridPos Cell;

        /// <summary>The way the force pushed, when it had a way (a moving board, a shove).</summary>
        public readonly GridPos Step;

        public AnchorRefusal(GridPos cell, GridPos step)
        {
            Cell = cell;
            Step = step;
        }

        public bool HasDirection
        {
            get { return Step.X != 0 || Step.Y != 0; }
        }
    }

    /// <summary>Every refused attempt on a hole since the engine last cleared the log (the top of
    /// each turn). Between turns it keeps growing, so a view remembers how many it has answered.</summary>
    public sealed class AnchorRefusalLog
    {
        private readonly List<AnchorRefusal> refusals = new List<AnchorRefusal>();

        /// <summary>Bumped on every Clear, so a view can tell a fresh log from a shorter one.</summary>
        public int Generation { get; private set; }

        public IReadOnlyList<AnchorRefusal> Refusals
        {
            get { return refusals; }
        }

        public int Count
        {
            get { return refusals.Count; }
        }

        internal void Add(GridPos cell, GridPos step)
        {
            refusals.Add(new AnchorRefusal(cell, step));
        }

        /// <summary>Public for the Animation Lab, which runs the real board on a board of its own.</summary>
        public void Clear()
        {
            refusals.Clear();
            Generation++;
        }
    }
}
