// PURPOSE: RoundEngine (partial) - OVERTIME PUTS THE ERODED ARENA BACK (designer's call,
// 2026-10-02). Shuffle erosion is the anti-stalling clock for REACHING the threshold; once the
// player has reached it and chooses to play on, the clock has done its job, so overtime is not
// played on the board it left behind. The rim it ate grows back and the dead zone is lifted, the
// moment the player continues (DecideAdvance(false)).
//
// Nothing can erode the board again after that: past the threshold a dry draw pile is a loss,
// never a recycle, so DeckRecycleCount stops moving and ApplyPendingBoardErosion has nothing
// left to apply. The counters are left as they are - they are the round's history, and resetting
// them would show the player a shot clock that no longer runs.
//
// HOW MUCH grows back is counted per SIDE, not remembered as a rectangle: the board may have
// been reshaped since (an inflation, "Tılsım"), and growing each side back by what erosion took
// from it is what composes with those - an inflation that deflates later still takes back
// exactly its own bands. WHAT each regrown cell was is remembered by position, so a hole in the
// bounding box comes back a hole and bonus ground comes back optional (GameBoard.CreateRegrown).
// The cubes erosion destroyed do not come back: the ground does, empty.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
        /// <summary>Rows/columns the rim erosion has taken off each side this round.</summary>
        private int erodedLeft;
        private int erodedRight;
        private int erodedBottom;
        private int erodedTop;

        /// <summary>Cells of the eaten bands that were NOT plain required ground, by what they
        /// were: bounding-box holes, bonus ground and dead cells. Everything else in the bands
        /// was ordinary play area, which is what grown ground is anyway.</summary>
        private readonly HashSet<GridPos> erodedHoles = new HashSet<GridPos>();
        private readonly HashSet<GridPos> erodedOptional = new HashSet<GridPos>();
        private readonly HashSet<GridPos> erodedDead = new HashSet<GridPos>();

        /// <summary>True while the rim erosion has taken something this round that has not
        /// been given back yet.</summary>
        public bool HasErodedRim
        {
            get { return erodedLeft + erodedRight + erodedBottom + erodedTop > 0; }
        }

        /// <summary>Called by ErodeRim just BEFORE the bands go: notes what each of their cells
        /// was, so growing them back can make them that again.</summary>
        private void RememberErodedBands(IReadOnlyList<GridPos> bandCells)
        {
            for (int i = 0; i < bandCells.Count; i++)
            {
                GridPos cell = bandCells[i];
                if (Board.IsDead(cell))
                {
                    erodedDead.Add(cell);
                }
                else if (!Board.IsInside(cell))
                {
                    erodedHoles.Add(cell);
                }
                else if (Board.IsOptional(cell))
                {
                    erodedOptional.Add(cell);
                }
            }
        }

        /// <summary>Called by ErodeRim once the bands are really gone.</summary>
        private void NoteRimEroded(int left, int right, int bottom, int top)
        {
            erodedLeft += left;
            erodedRight += right;
            erodedBottom += bottom;
            erodedTop += top;
        }

        /// <summary>
        /// Gives the arena back what shuffle erosion took: the rim grows back on every side it
        /// lost, each cell as what it was, empty; and the dead zone is lifted. Called when the
        /// player continues into overtime. Idempotent - a second continue finds nothing to give.
        /// Returns true when the board changed.
        ///
        /// Always the MAIN world: erosion only ever eats the main board, and this runs outside
        /// any activation window, so Board is the main board here.
        /// </summary>
        internal bool RestoreErodedArena()
        {
            bool changed = false;
            if (HasErodedRim)
            {
                GameBoard regrown = GameBoard.CreateRegrown(mainBoard, erodedLeft, erodedRight,
                    erodedBottom, erodedTop, erodedHoles, erodedOptional, erodedDead);
                if (regrown != null)
                {
                    mainBoard = regrown;
                    ResyncSnapshot();
                    CaptureTurnStartCardCounts();
                    // The circuit a shrunk arena squeezed ("Devre") unfolds with the ground.
                    NoteBoardReshaped();
                    changed = true;
                }
                erodedLeft = 0;
                erodedRight = 0;
                erodedBottom = 0;
                erodedTop = 0;
                erodedHoles.Clear();
                erodedOptional.Clear();
                erodedDead.Clear();
            }
            if (mainBoard.ClearBlight() > 0)
            {
                changed = true;
            }
            return changed;
        }

        private void SaveErodedArena(SaveWriter w, string key)
        {
            w.Write(key + ".left", erodedLeft);
            w.Write(key + ".right", erodedRight);
            w.Write(key + ".bottom", erodedBottom);
            w.Write(key + ".top", erodedTop);
            CoreSerializers.WritePosList(w, key + ".holes", Sorted(erodedHoles));
            CoreSerializers.WritePosList(w, key + ".optional", Sorted(erodedOptional));
            CoreSerializers.WritePosList(w, key + ".dead", Sorted(erodedDead));
        }

        private void LoadErodedArena(SaveReader r, string key)
        {
            erodedLeft = r.ReadInt(key + ".left");
            erodedRight = r.ReadInt(key + ".right");
            erodedBottom = r.ReadInt(key + ".bottom");
            erodedTop = r.ReadInt(key + ".top");
            erodedHoles.UnionWith(CoreSerializers.ReadPosList(r, key + ".holes"));
            erodedOptional.UnionWith(CoreSerializers.ReadPosList(r, key + ".optional"));
            erodedDead.UnionWith(CoreSerializers.ReadPosList(r, key + ".dead"));
        }

        /// <summary>A set in a stable order, so the same round always writes the same file.</summary>
        private static List<GridPos> Sorted(HashSet<GridPos> cells)
        {
            var list = new List<GridPos>(cells);
            list.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            return list;
        }
    }
}
