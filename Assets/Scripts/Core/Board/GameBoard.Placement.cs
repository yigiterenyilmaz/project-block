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
                if (IsSealed(pos) || Blocks(cells[pos.X - MinX, pos.Y - MinY], negative))
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
                    if (IsSealed(pos) || Blocks(cells[pos.X - MinX, pos.Y - MinY], negative))
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

        private readonly List<GridPos> lastMinesTriggered = new List<GridPos>();

        /// <summary>The "Mayın" mines the LAST placement landed on and set off, for the View's
        /// explosion. Reporting only, rewritten by every placement.</summary>
        public IReadOnlyList<GridPos> LastMinesTriggered
        {
            get { return lastMinesTriggered; }
        }

        /// <summary>"Kara Delik": the cubes the last Place dropped into a black hole - they never
        /// landed, so they are in no destruction log. Cleared by every Place. Reporting only.</summary>
        public readonly List<DestroyedCube> LastPlacementSwallows = new List<DestroyedCube>();

        /// <summary>"Kara Delik": may a void card be laid on <paramref name="origin"/>? Like any
        /// block, except that an occupied cell does not refuse it as long as a black hole can
        /// take what stands there (the engine swallows it first).</summary>
        public bool CanPlaceVoid(BlockShape shape, GridPos origin)
        {
            foreach (GridPos offset in shape.Cells)
            {
                GridPos pos = origin + offset;
                if (!IsInside(pos) || IsSealed(pos))
                {
                    return false;
                }
                Cube? occupant = cells[pos.X - MinX, pos.Y - MinY];
                if (occupant.HasValue && !CubeRules.CanBeSwallowed(occupant.Value))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>"Kara Delik": moves one cube a step (the pull). Refused for a hole, onto
        /// anything but an empty play cell, and onto a sealed cell. The cube keeps its kind and
        /// its card. The caller re-baselines the destruction diff: this is a move, not a death.</summary>
        public bool MoveCube(GridPos from, GridPos to)
        {
            if (!IsInside(from) || !IsInside(to) || IsSealed(to))
            {
                return false;
            }
            Cube? cube = cells[from.X - MinX, from.Y - MinY];
            if (!cube.HasValue || CubeRules.IsAnchored(cube.Value)
                || cells[to.X - MinX, to.Y - MinY].HasValue)
            {
                return false;
            }
            cells[from.X - MinX, from.Y - MinY] = null;
            cells[to.X - MinX, to.Y - MinY] = cube;
            return true;
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
            return Place(card, shape, origin, allowOutside, null);
        }

        /// <summary>
        /// The kind of cube each cell of <paramref name="shape"/> (in shape.Cells order) is
        /// stamped with when this card lands - THE decision Place makes, pulled out so the
        /// placement preview can ask it too rather than guess. A gold cube locks every line it
        /// lands in, so "would this placement explode that row?" cannot be answered without
        /// knowing which of the arriving cubes are gold.
        /// </summary>
        public CubeKind[] StampedKinds(BlockCard card, BlockShape shape, CubeKind? cardKindOverride)
        {
            // "Vanilya" (boss round): the card's element is ignored, so every cube it stamps is
            // an ordinary one - including the per-cube elements of a designed block.
            CubeKind cardKind = IgnoreElements ? CubeKind.Normal
                : cardKindOverride ?? CubeRules.KindForCard(card);
            // A per-cube designed block stamps each cube from its own element. The per-cube array
            // is aligned to card.Shape.Cells, and a designed block is never rotated/reshaped, so
            // the shape being placed matches it cell-for-cell; fall back to the one card-wide kind
            // if the counts ever diverge (a transformed shape).
            IReadOnlyList<GridPos> shapeCells = shape.Cells;
            bool perCube = !IgnoreElements && card.HasPerCubeElements
                && shapeCells.Count == card.Shape.Cells.Count;
            // "Hedefli": exactly one cube of the block is stamped as its TARGET, whatever else
            // the card is made of. Read off the shape actually being placed, so a rotation or a
            // reshape marks the cube the player was shown rather than a stale coordinate. Under
            // "Vanilya" the element is not there at all, so neither is the mark.
            int targetIndex = IgnoreElements || !card.Has(BlockElement.Targeted)
                ? -1
                : card.TargetIndexIn(shape);
            var kinds = new CubeKind[shapeCells.Count];
            for (int ci = 0; ci < shapeCells.Count; ci++)
            {
                kinds[ci] = ci == targetIndex
                    ? CubeKind.Target
                    : (perCube ? CubeRules.KindForElement(card.CellElement(ci)) : cardKind);
            }
            return kinds;
        }

        /// <summary>The same, with the cube kind the card lays decided by the ROUND rather than
        /// read off the card: a card carrying a borrowed gene ("Gen nakli") has no element of its
        /// own printed on it, and would otherwise put down plain cubes. Null reads the card, as
        /// every other placement always has.</summary>
        public IReadOnlyList<GridPos> Place(BlockCard card, BlockShape shape, GridPos origin,
            bool allowOutside, CubeKind? cardKindOverride)
        {
            if (!CanPlace(shape, origin, allowOutside))
            {
                throw new InvalidOperationException("Illegal placement of " + card + " at " + origin + ".");
            }
            var placed = new List<GridPos>(shape.Size);
            lastMinesTriggered.Clear();
            LastPlacementSwallows.Clear();
            IReadOnlyList<GridPos> shapeCells = shape.Cells;
            CubeKind[] kinds = StampedKinds(card, shape, cardKindOverride);
            for (int ci = 0; ci < shapeCells.Count; ci++)
            {
                GridPos offset = shapeCells[ci];
                CubeKind kind = kinds[ci];
                GridPos pos = origin + offset;
                if (IsInside(pos))
                {
                    Cube? occupant = cells[pos.X - MinX, pos.Y - MinY];
                    if (occupant.HasValue && occupant.Value.Kind == CubeKind.Void)
                    {
                        // "Kara Delik": the arriving cube falls in and the hole stays.
                        LastPlacementSwallows.Add(new DestroyedCube(pos, Stamp(kind, card.Id)));
                        continue;
                    }
                    if (occupant.HasValue && occupant.Value.Kind == CubeKind.Mine)
                    {
                        // "Mayın" blows the arriving cube up and is spent with it.
                        lastMinesTriggered.Add(pos);
                        cells[pos.X - MinX, pos.Y - MinY] = null;
                        OccupiedCount--;
                        continue;
                    }
                    if (!occupant.HasValue)
                    {
                        OccupiedCount++; // replaced transparents were already counted
                    }
                    cells[pos.X - MinX, pos.Y - MinY] = Stamp(kind, card.Id);
                }
                else
                {
                    outsideCubes[pos] = Stamp(kind, card.Id);
                }
                placed.Add(pos);
            }
            return placed;
        }

        /// <summary>The cube a card lays for a kind. Snow is born with its numbers (one layer, the
        /// full melt time); every other kind is the bare cube it always was.</summary>
        private static Cube Stamp(CubeKind kind, int cardId)
        {
            return kind == CubeKind.Snow ? Cube.FreshSnow(cardId) : new Cube(kind, cardId);
        }
    }
}
