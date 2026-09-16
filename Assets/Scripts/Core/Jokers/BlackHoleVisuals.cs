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

        /// <summary>The count after this turn (0 again after a collapse), and what it needs.</summary>
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
