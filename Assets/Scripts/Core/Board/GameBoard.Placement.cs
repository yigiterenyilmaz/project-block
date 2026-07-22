// PURPOSE: GameBoard placement - CanPlace legality checks (incl. ghost overhang) and
// the Place methods that stamp a card's cubes onto the grid.

using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectBlock.Core
{
    partial class GameBoard
    {
        /// <summary>True if every cell of the shape (anchored at origin) is inside and
        /// empty - or holds a transparent cube, which placements may cover (it gets
        /// replaced, confirmed 2026-07-18).</summary>
        public bool CanPlace(BlockShape shape, GridPos origin)
        {
            return CanPlace(shape, origin, false, false);
        }

        /// <summary>Placement check with the negative-block rule (see Blocks).</summary>
        public bool CanPlace(BlockShape shape, GridPos origin, bool allowOutside, bool negative)
        {
            if (allowOutside)
            {
                return CanPlaceAllowingOutside(shape, origin, negative);
            }
            foreach (GridPos offset in shape.Cells)
            {
                GridPos pos = origin + offset;
                if (!IsInside(pos))
                {
                    return false;
                }
                if (Blocks(cells[pos.X - MinX, pos.Y - MinY], negative))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Does the cube already in a cell refuse an arriving block?
        /// Transparent is replaced, and the two traps (Void, Mine) consume what lands on
        /// them, so all three accept a placement. A NEGATIVE block is the anti-block: it may
        /// land on anything it can erase, and is refused only by cubes nothing can break.</summary>
        private static bool Blocks(Cube? occupant, bool negative)
        {
            if (!occupant.HasValue)
            {
                return false;
            }
            if (negative)
            {
                return !CubeRules.IsDestructible(occupant.Value);
            }
            return occupant.Value.Kind != CubeKind.Transparent
                && occupant.Value.Kind != CubeKind.Void
                && occupant.Value.Kind != CubeKind.Mine;
        }

        /// <summary>Ghost placement check: cubes may hang outside the grid (onto free
        /// outside space), but at least one cube must land inside.</summary>
        public bool CanPlace(BlockShape shape, GridPos origin, bool allowOutside)
        {
            return CanPlace(shape, origin, allowOutside, false);
        }

        private bool CanPlaceAllowingOutside(BlockShape shape, GridPos origin, bool negative)
        {
            int insideCount = 0;
            foreach (GridPos offset in shape.Cells)
            {
                GridPos pos = origin + offset;
                if (IsInside(pos))
                {
                    if (Blocks(cells[pos.X - MinX, pos.Y - MinY], negative))
                    {
                        return false;
                    }
                    insideCount++;
                }
                else if (outsideCubes.ContainsKey(pos))
                {
                    return false;
                }
            }
            return insideCount >= 1;
        }

        /// <summary>Places the card's cubes. Caller must have validated with CanPlace.
        /// Transparent cubes underneath are replaced.</summary>
        public IReadOnlyList<GridPos> Place(BlockCard card, GridPos origin)
        {
            return Place(card, origin, false);
        }

        /// <summary>Placement with optional ghost overhang (cubes outside the grid
        /// persist in OutsideCubes).</summary>
        public IReadOnlyList<GridPos> Place(BlockCard card, GridPos origin, bool allowOutside)
        {
            return Place(card, card.Shape, origin, allowOutside);
        }

        /// <summary>Placement of an explicit shape (mechanical rotation / fox reshape
        /// place a transformed shape on behalf of the card).</summary>
        public IReadOnlyList<GridPos> Place(BlockCard card, BlockShape shape, GridPos origin,
            bool allowOutside)
        {
            if (!CanPlace(shape, origin, allowOutside))
            {
                throw new InvalidOperationException("Illegal placement of " + card + " at " + origin + ".");
            }
            var placed = new List<GridPos>(shape.Size);
            CubeKind cardKind = CubeRules.KindForCard(card);
            // A per-cube designed block stamps each cube from its own element. The per-cube array
            // is aligned to card.Shape.Cells, and a designed block is never rotated/reshaped, so
            // the shape being placed matches it cell-for-cell; fall back to the one card-wide kind
            // if the counts ever diverge (a transformed shape).
            IReadOnlyList<GridPos> shapeCells = shape.Cells;
            bool perCube = card.HasPerCubeElements && shapeCells.Count == card.Shape.Cells.Count;
            for (int ci = 0; ci < shapeCells.Count; ci++)
            {
                GridPos offset = shapeCells[ci];
                CubeKind kind = perCube ? CubeRules.KindForElement(card.CellElement(ci)) : cardKind;
                GridPos pos = origin + offset;
                if (IsInside(pos))
                {
                    Cube? occupant = cells[pos.X - MinX, pos.Y - MinY];
                    if (occupant.HasValue && (occupant.Value.Kind == CubeKind.Void
                        || occupant.Value.Kind == CubeKind.Mine))
                    {
                        // Traps: "Kara delik" swallows the arriving cube, "Mayın" blows it up.
                        // Either way both are gone, so nothing is placed.
                        cells[pos.X - MinX, pos.Y - MinY] = null;
                        OccupiedCount--;
                        continue;
                    }
                    if (!occupant.HasValue)
                    {
                        OccupiedCount++; // replaced transparents were already counted
                    }
                    cells[pos.X - MinX, pos.Y - MinY] = new Cube(kind, card.Id);
                }
                else
                {
                    outsideCubes[pos] = new Cube(kind, card.Id);
                }
                placed.Add(pos);
            }
            return placed;
        }
    }
}
