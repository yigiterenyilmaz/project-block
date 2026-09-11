// PURPOSE: What happened to "Matruşka"'s dolls during one turn, in the order it happened - so the
// View can STAGE it rather than work it out.
//
// MatruskaBoss writes one of these at the moment it decides each thing (TurnReport.DollEvents).
// Nothing reads them back and nothing is decided by them. The View is not allowed to infer any of
// this from what it can see - which doll broke, how many came out of it and where they went,
// whether it was the last generation, whether water only moved it - so this is where the boss says
// it. Reporting only: adding it changed no rule, no random draw and no saved field.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>What happened to a doll.</summary>
    public enum DollEventKind
    {
        /// <summary>The round's first doll was set down on Cell.</summary>
        Arrived = 0,

        /// <summary>The cube under the doll on Cell was destroyed and the doll split: Children
        /// are the cells the boss put its halves on, one generation smaller.</summary>
        Split = 1,

        /// <summary>The cube under a LAST-generation doll was destroyed. It holds nothing, so
        /// nothing came out.</summary>
        Emptied = 2,

        /// <summary>The doll's cube left from under it WITHOUT being destroyed (settling water)
        /// and the boss moved the doll from Cell to To. Not a break: its generation is unchanged.</summary>
        Moved = 3,

        /// <summary>The last doll is gone and the boss is beaten.</summary>
        AllCracked = 4
    }

    /// <summary>One thing that happened to the dolls. Which fields mean something depends on the
    /// kind - see DollEventKind; a field a kind does not use holds its default.</summary>
    public readonly struct DollEvent
    {
        private static readonly GridPos[] NoCells = new GridPos[0];

        public readonly DollEventKind Kind;

        /// <summary>Where the doll was - or, for Arrived, where it was set down.</summary>
        public readonly GridPos Cell;

        /// <summary>Moved only: where the boss put it.</summary>
        public readonly GridPos To;

        /// <summary>The doll's generation as the boss counts it: 1 is the largest.</summary>
        public readonly int Generation;

        /// <summary>Split only: the cells the smaller dolls were put on.</summary>
        public readonly IReadOnlyList<GridPos> Children;

        /// <summary>The doll was of the last generation - the one that holds nothing.</summary>
        public readonly bool IsLastGeneration;

        private DollEvent(DollEventKind kind, GridPos cell, GridPos to, int generation,
            IReadOnlyList<GridPos> children, bool lastGeneration)
        {
            Kind = kind;
            Cell = cell;
            To = to;
            Generation = generation;
            Children = children != null ? children : NoCells;
            IsLastGeneration = lastGeneration;
        }

        /// <summary>The generation a split's children are.</summary>
        public int ChildGeneration
        {
            get { return Generation + 1; }
        }

        public static DollEvent Arrived(GridPos cell, int generation, bool lastGeneration)
        {
            return new DollEvent(DollEventKind.Arrived, cell, cell, generation, null, lastGeneration);
        }

        /// <summary>The children are COPIED, so the list the boss built them in can go on changing
        /// without rewriting what the report says happened.</summary>
        public static DollEvent Split(GridPos cell, int generation, IReadOnlyList<GridPos> children)
        {
            var copy = new GridPos[children != null ? children.Count : 0];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = children[i];
            }
            return new DollEvent(DollEventKind.Split, cell, cell, generation, copy, false);
        }

        public static DollEvent Emptied(GridPos cell, int generation)
        {
            return new DollEvent(DollEventKind.Emptied, cell, cell, generation, null, true);
        }

        public static DollEvent Moved(GridPos from, GridPos to, int generation, bool lastGeneration)
        {
            return new DollEvent(DollEventKind.Moved, from, to, generation, null, lastGeneration);
        }

        public static DollEvent AllCracked()
        {
            return new DollEvent(DollEventKind.AllCracked, default(GridPos), default(GridPos), 0,
                null, false);
        }
    }
}
