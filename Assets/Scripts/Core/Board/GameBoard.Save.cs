// PURPOSE: GameBoard save/load (partial). Lives on the class itself because the board's
// state is private - the masks, the cube grid and the ghost/seal side tables.
//
// THE TWO MASKS BOTH TRAVEL, and they are not the same thing (see the GameBoard header):
// a hole in the bounding box is merely skipped by a line, while a cell EATEN by erosion
// kills its row and column for good. Restoring one without the other would silently change
// which lines can still be completed.
//
// The derived counts (PlayableCellCount, DeadCellCount, OccupiedCount) are NOT read from the
// file - they are recomputed from the masks and the cubes, so a save can never load a board
// whose counts disagree with what is actually on it.

using System.Collections.Generic;
using System.Text;

namespace ProjectBlock.Core
{
    partial class GameBoard
    {
        internal void Save(SaveWriter w, string key)
        {
            w.Write(key + ".minX", MinX);
            w.Write(key + ".minY", MinY);
            w.Write(key + ".width", Width);
            w.Write(key + ".height", Height);
            w.Write(key + ".ignoreElements", IgnoreElements);

            // One row per line, as a run of 0/1 - compact and readable in a text editor.
            for (int iy = 0; iy < Height; iy++)
            {
                w.Write(key + ".playable." + iy, MaskRow(playable, iy));
                w.Write(key + ".dead." + iy, MaskRow(dead, iy));
            }

            // Cubes are sparse, so they are written as a list rather than a full grid.
            var occupied = new List<GridPos>();
            for (int ix = 0; ix < Width; ix++)
            {
                for (int iy = 0; iy < Height; iy++)
                {
                    if (cells[ix, iy].HasValue)
                    {
                        occupied.Add(new GridPos(MinX + ix, MinY + iy));
                    }
                }
            }
            w.Write(key + ".cubes.count", occupied.Count);
            for (int i = 0; i < occupied.Count; i++)
            {
                GridPos at = occupied[i];
                CoreSerializers.WritePos(w, key + ".cubes." + i, at);
                CoreSerializers.WriteCube(w, key + ".cubes." + i,
                    cells[at.X - MinX, at.Y - MinY].Value);
            }

            // Ghost cubes sitting outside the grid, and the boss's placement seals.
            w.Write(key + ".outside.count", outsideCubes.Count);
            int index = 0;
            foreach (KeyValuePair<GridPos, Cube> ghost in outsideCubes)
            {
                CoreSerializers.WritePos(w, key + ".outside." + index, ghost.Key);
                CoreSerializers.WriteCube(w, key + ".outside." + index, ghost.Value);
                index++;
            }
            CoreSerializers.WritePosList(w, key + ".sealed", sealedCells);
        }

        internal static GameBoard Load(SaveReader r, string key)
        {
            int minX = r.ReadInt(key + ".minX");
            int minY = r.ReadInt(key + ".minY");
            int width = r.ReadInt(key + ".width");
            int height = r.ReadInt(key + ".height");
            bool ignoreElements = r.ReadBool(key + ".ignoreElements");

            var mask = new bool[width, height];
            var deadMask = new bool[width, height];
            int playableCount = 0;
            int deadCount = 0;
            for (int iy = 0; iy < height; iy++)
            {
                string playableRow = r.ReadString(key + ".playable." + iy);
                string deadRow = r.ReadString(key + ".dead." + iy);
                for (int ix = 0; ix < width; ix++)
                {
                    if (ix < playableRow.Length && playableRow[ix] == '1')
                    {
                        mask[ix, iy] = true;
                        playableCount++;
                    }
                    if (ix < deadRow.Length && deadRow[ix] == '1')
                    {
                        deadMask[ix, iy] = true;
                        deadCount++;
                    }
                }
            }

            var board = new GameBoard(minX, minY, width, height, mask, deadMask,
                playableCount, deadCount);
            board.IgnoreElements = ignoreElements;

            int cubeCount = r.ReadInt(key + ".cubes.count");
            for (int i = 0; i < cubeCount; i++)
            {
                GridPos at = CoreSerializers.ReadPos(r, key + ".cubes." + i);
                Cube cube = CoreSerializers.ReadCube(r, key + ".cubes." + i);
                board.cells[at.X - minX, at.Y - minY] = cube;
                board.OccupiedCount++;
            }

            int outsideCount = r.ReadInt(key + ".outside.count");
            for (int i = 0; i < outsideCount; i++)
            {
                GridPos at = CoreSerializers.ReadPos(r, key + ".outside." + i);
                board.outsideCubes[at] = CoreSerializers.ReadCube(r, key + ".outside." + i);
            }
            board.sealedCells.AddRange(CoreSerializers.ReadPosList(r, key + ".sealed"));
            return board;
        }

        private string MaskRow(bool[,] source, int iy)
        {
            var sb = new StringBuilder(Width);
            for (int ix = 0; ix < Width; ix++)
            {
                sb.Append(source[ix, iy] ? '1' : '0');
            }
            return sb.ToString();
        }
    }
}
