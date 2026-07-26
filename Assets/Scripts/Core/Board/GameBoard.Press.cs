// PURPOSE: The board side of "Hidrolik pres" - squeezing a 2x2 patch into one cell, and letting it
// back out again four turns later.
//
// THE COMPRESSED CUBE is an ordinary destructible cube of its own kind (CubeKind.Compressed). It
// fills one cell, blocks a clean sweep and breaks with a completed line like anything else; what is
// special is only that the power pays four cubes' worth when it goes, and that it wants its three
// cells back.
//
// EXPANDING is where the rules live, and they are the designer's, in this order:
//   1. It expands into whatever is EMPTY. Nothing to push, nothing to decide.
//   2. Cubes in the way are PUSHED outward, away from the press, and whatever leaves the board is
//      gone (the power scores for those).
//   3. Obsidian and gold cannot be pushed at all - they are the two cubes nothing may destroy. A
//      side holding one is simply not a direction, so the press expands the OTHER way entirely.
//   4. If NO direction is open, the press detonates: it takes the surrounding gold and obsidian
//      with it and pays nothing. That is the one thing in the game that removes them, and it costs
//      you the press and the score.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>What happened when a press let go.</summary>
    public sealed class PressExpansion
    {
        /// <summary>Cubes shoved off the edge of the board. The power scores for these.</summary>
        public int CubesPushedOff;

        /// <summary>True when nothing could move and the press blew instead.</summary>
        public bool Detonated;

        /// <summary>Cells the detonation cleared, for the UI. Empty on an ordinary expansion.</summary>
        public readonly List<GridPos> DetonatedCells = new List<GridPos>();

        /// <summary>The four cells the press now occupies again.</summary>
        public readonly List<GridPos> Restored = new List<GridPos>();
    }

    partial class GameBoard
    {
        /// <summary>
        /// Squeezes the 2x2 patch anchored at its BOTTOM-LEFT cell into that one cell, and hands
        /// back the four cubes that were there (index 0 is the anchor, then right, then up, then
        /// up-right - the order Expand puts them back in). Returns null when the patch is not four
        /// real cells of play area.
        ///
        /// Empty cells inside the patch travel too, as nulls: the press restores exactly the
        /// picture it swallowed.
        /// </summary>
        internal Cube?[] Compress(GridPos anchor)
        {
            List<GridPos> patch = PatchAt(anchor);
            if (patch == null)
            {
                return null;
            }
            var swallowed = new Cube?[4];
            for (int i = 0; i < 4; i++)
            {
                GridPos cell = patch[i];
                swallowed[i] = cells[cell.X - MinX, cell.Y - MinY];
                if (swallowed[i].HasValue)
                {
                    cells[cell.X - MinX, cell.Y - MinY] = null;
                    OccupiedCount--;
                }
            }
            cells[anchor.X - MinX, anchor.Y - MinY] = new Cube(CubeKind.Compressed, PressCardId);
            OccupiedCount++;
            return swallowed;
        }

        /// <summary>Source card id stamped on a compressed cube. Negative, so it can never collide
        /// with a real card.</summary>
        public const int PressCardId = -11;

        /// <summary>
        /// Lets a press back out at <paramref name="anchor"/>, restoring <paramref name="swallowed"/>.
        /// See the file header for the order of the rules. Returns what happened, or null when
        /// there is no press there any more (it was exploded, lifted, or the board was resized).
        /// </summary>
        internal PressExpansion Expand(GridPos anchor, Cube?[] swallowed)
        {
            List<GridPos> patch = PatchAt(anchor);
            if (patch == null || swallowed == null || swallowed.Length != 4)
            {
                return null;
            }
            Cube? here = cells[anchor.X - MinX, anchor.Y - MinY];
            if (!here.HasValue || here.Value.Kind != CubeKind.Compressed)
            {
                return null;
            }
            var result = new PressExpansion();

            // The three cells it wants back, and what is in the way.
            var wanted = new List<GridPos>();
            for (int i = 1; i < 4; i++)
            {
                wanted.Add(patch[i]);
            }
            if (!ClearTheWay(anchor, wanted, result))
            {
                Detonate(anchor, result);
                return result;
            }

            // Put the picture back exactly as it was swallowed.
            cells[anchor.X - MinX, anchor.Y - MinY] = null;
            OccupiedCount--;
            for (int i = 0; i < 4; i++)
            {
                GridPos cell = patch[i];
                if (swallowed[i].HasValue)
                {
                    cells[cell.X - MinX, cell.Y - MinY] = swallowed[i];
                    OccupiedCount++;
                }
                result.Restored.Add(cell);
            }
            return result;
        }

        /// <summary>Empties the three cells the press wants, pushing what is in them away from the
        /// anchor. False when a cell cannot be emptied at all - which is what makes it detonate.
        /// </summary>
        private bool ClearTheWay(GridPos anchor, List<GridPos> wanted, PressExpansion result)
        {
            foreach (GridPos cell in wanted)
            {
                if (!cells[cell.X - MinX, cell.Y - MinY].HasValue)
                {
                    continue; // already free
                }
                int dx = cell.X > anchor.X ? 1 : (cell.X < anchor.X ? -1 : 0);
                int dy = cell.Y > anchor.Y ? 1 : (cell.Y < anchor.Y ? -1 : 0);
                if (!PushAway(cell, dx, dy, result))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Shoves everything from <paramref name="from"/> outward along (dx, dy) by one cell.
        /// Cubes falling off the far edge are counted and gone. False when the line of cubes holds
        /// one that cannot be moved - obsidian or gold - because then this direction is not a
        /// direction at all.
        ///
        /// A diagonal want (the far corner of the patch) is pushed on whichever single axis is
        /// open, preferring the horizontal, so the shove is always a straight line.
        /// </summary>
        private bool PushAway(GridPos from, int dx, int dy, PressExpansion result)
        {
            if (dx != 0 && dy != 0)
            {
                return PushAway(from, dx, 0, result) || PushAway(from, 0, dy, result);
            }
            // Walk out to the edge collecting what has to move, and refuse on an immovable cube.
            var line = new List<GridPos>();
            GridPos at = from;
            while (IsInside(at) && cells[at.X - MinX, at.Y - MinY].HasValue)
            {
                Cube cube = cells[at.X - MinX, at.Y - MinY].Value;
                if (cube.Kind == CubeKind.Obsidian || cube.Kind == CubeKind.Gold)
                {
                    return false; // nothing may shift these, so this way is shut
                }
                line.Add(at);
                at = new GridPos(at.X + dx, at.Y + dy);
            }
            // Move from the far end back, so nothing overwrites a cube that has not moved yet.
            for (int i = line.Count - 1; i >= 0; i--)
            {
                GridPos cell = line[i];
                var target = new GridPos(cell.X + dx, cell.Y + dy);
                Cube cube = cells[cell.X - MinX, cell.Y - MinY].Value;
                cells[cell.X - MinX, cell.Y - MinY] = null;
                OccupiedCount--;
                if (!IsInside(target))
                {
                    result.CubesPushedOff++; // over the edge and gone
                    continue;
                }
                cells[target.X - MinX, target.Y - MinY] = cube;
                OccupiedCount++;
            }
            return true;
        }

        /// <summary>Nothing could move: the press goes up and takes the gold and obsidian around it
        /// with it. The one thing in the game that removes those - and it pays nothing.</summary>
        private void Detonate(GridPos anchor, PressExpansion result)
        {
            result.Detonated = true;
            for (int x = anchor.X - 1; x <= anchor.X + 2; x++)
            {
                for (int y = anchor.Y - 1; y <= anchor.Y + 2; y++)
                {
                    var cell = new GridPos(x, y);
                    if (!IsInside(cell))
                    {
                        continue;
                    }
                    Cube? cube = cells[x - MinX, y - MinY];
                    if (!cube.HasValue)
                    {
                        continue;
                    }
                    bool isPress = cube.Value.Kind == CubeKind.Compressed;
                    bool isStone = cube.Value.Kind == CubeKind.Obsidian
                        || cube.Value.Kind == CubeKind.Gold;
                    if (!isPress && !isStone)
                    {
                        continue; // ordinary cubes are not what the blast is for
                    }
                    cells[x - MinX, y - MinY] = null;
                    OccupiedCount--;
                    result.DetonatedCells.Add(cell);
                }
            }
        }

        /// <summary>The four cells of a 2x2 anchored bottom-left, or null when any of them is not
        /// real play area. Order: anchor, right, up, up-right.</summary>
        private List<GridPos> PatchAt(GridPos anchor)
        {
            var patch = new List<GridPos>
            {
                anchor,
                new GridPos(anchor.X + 1, anchor.Y),
                new GridPos(anchor.X, anchor.Y + 1),
                new GridPos(anchor.X + 1, anchor.Y + 1)
            };
            foreach (GridPos cell in patch)
            {
                if (!IsInside(cell))
                {
                    return null;
                }
            }
            return patch;
        }

        /// <summary>True when a 2x2 press could be applied here at all.</summary>
        public bool CanCompressAt(GridPos anchor)
        {
            return PatchAt(anchor) != null;
        }
    }
}
