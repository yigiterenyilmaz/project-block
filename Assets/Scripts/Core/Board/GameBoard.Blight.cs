// PURPOSE: GameBoard's DEAD ZONE (partial) - the anti-stalling clock's mark on the arena.
//
// A blighted cell is still ordinary play area: blocks go on it, it is required for its line to
// be full, and a line through it still explodes. What it takes away is the PAY: a row or column
// that touches the dead zone scores nothing when it goes off (RoundEngine.BuildLineScore). So the
// arena keeps working as a place to survive in while the part of it worth playing for shrinks.
//
// It is NOT the `dead` mask. A dead cell (MarkDead) cannot be built on and kills its lines
// outright; the blight kills only their score. The two are kept apart on purpose - the Gangrene,
// mirror and dead-end code all read `dead` as "no cube can ever stand here", and a blighted cell
// is the opposite of that.
//
// Stored as absolute coordinates like the seals, so it survives a resize and a clone unchanged.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class GameBoard
    {
        private readonly HashSet<GridPos> blight = new HashSet<GridPos>();

        /// <summary>Cells in the dead zone. 0 on every untouched board.</summary>
        public int BlightedCellCount
        {
            get { return blight.Count; }
        }

        /// <summary>True for a dead-zone cell: playable, but a line through it pays nothing.</summary>
        public bool IsBlighted(GridPos pos)
        {
            return blight.Contains(pos);
        }

        /// <summary>The dead zone's cells, for the View to draw.</summary>
        public IEnumerable<GridPos> BlightedCells
        {
            get { return blight; }
        }

        /// <summary>Adds the playable cells among <paramref name="targets"/> to the dead zone.
        /// Returns the cells newly blighted. Cubes standing there stay where they are.</summary>
        internal List<GridPos> Blight(IEnumerable<GridPos> targets)
        {
            var added = new List<GridPos>();
            foreach (GridPos pos in targets)
            {
                if (IsInside(pos) && blight.Add(pos))
                {
                    added.Add(pos);
                }
            }
            return added;
        }

        /// <summary>True if any cell of row <paramref name="y"/> is in the dead zone.</summary>
        public bool RowTouchesBlight(int y)
        {
            if (blight.Count == 0)
            {
                return false;
            }
            foreach (GridPos pos in blight)
            {
                if (pos.Y == y)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if any cell of column <paramref name="x"/> is in the dead zone.</summary>
        public bool ColumnTouchesBlight(int x)
        {
            if (blight.Count == 0)
            {
                return false;
            }
            foreach (GridPos pos in blight)
            {
                if (pos.X == x)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Carries the dead zone over from the board this one was made from, keeping
        /// only the cells that are still play area here.</summary>
        private void CopyBlightFrom(GameBoard source)
        {
            foreach (GridPos pos in source.blight)
            {
                if (IsInside(pos))
                {
                    blight.Add(pos);
                }
            }
        }

        private void SaveBlight(SaveWriter w, string key)
        {
            var cells = new List<GridPos>(blight);
            cells.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            CoreSerializers.WritePosList(w, key + ".blight", cells);
        }

        private void LoadBlight(SaveReader r, string key)
        {
            List<GridPos> cells = CoreSerializers.ReadPosList(r, key + ".blight");
            for (int i = 0; i < cells.Count; i++)
            {
                blight.Add(cells[i]);
            }
        }
    }
}
