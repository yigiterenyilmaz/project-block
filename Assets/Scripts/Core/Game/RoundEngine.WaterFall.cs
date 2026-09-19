// PURPOSE: RoundEngine's WATER-FALL BONUS (partial) - a line that only filled because water
// SETTLED into it pays ScoringConfig.WaterFallBonusPercent more, and so does the clean sweep that
// clear leads to (designer's call, 2026-09-19).
//
// WHAT COUNTS IS THE FALL, NOT THE WATER. A water cube PLACED straight into a line explodes in
// place (the confirmed water rule), and that is an ordinary clear. The bonus is only for the
// path where the placement completed nothing, the water settled, and a line was full AFTER it -
// and even then only for a line that holds a cell a fallen water cube came to REST in. A line
// that happens to go off in the same resolve without any water in it earns nothing extra.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
        /// <summary>The turn whose explosion fell into place - the sweep reads it, so a sweep
        /// raised between turns (or on another turn) never inherits the bonus.</summary>
        private int waterFallTurn = -1;

        /// <summary>True while the turn being resolved is one whose lines water filled.</summary>
        internal bool WaterFellIntoPlaceThisTurn
        {
            get { return waterFallTurn == TurnNumber && currentReport != null; }
        }

        /// <summary>
        /// How many of the exploded lines hold a cell where a FALLEN water cube came to rest,
        /// walking the settle's frames (the first <paramref name="framesBefore"/> of them - the
        /// ones played before the explosion) to find where each moved cube finally stopped.
        /// </summary>
        internal static int WaterFallLines(LineExplosionResult explosion,
            IReadOnlyList<IReadOnlyList<WaterMove>> frames, int framesBefore)
        {
            var resting = new HashSet<GridPos>();
            int count = framesBefore < frames.Count ? framesBefore : frames.Count;
            for (int f = 0; f < count; f++)
            {
                IReadOnlyList<WaterMove> frame = frames[f];
                for (int i = 0; i < frame.Count; i++)
                {
                    resting.Remove(frame[i].From);
                }
                for (int i = 0; i < frame.Count; i++)
                {
                    resting.Add(frame[i].To);
                }
            }
            if (resting.Count == 0)
            {
                return 0;
            }
            int lines = 0;
            for (int i = 0; i < explosion.Rows.Count; i++)
            {
                if (AnyOn(resting, explosion.Rows[i], true))
                {
                    lines++;
                }
            }
            for (int i = 0; i < explosion.Columns.Count; i++)
            {
                if (AnyOn(resting, explosion.Columns[i], false))
                {
                    lines++;
                }
            }
            return lines;
        }

        private static bool AnyOn(HashSet<GridPos> cells, int line, bool row)
        {
            foreach (GridPos cell in cells)
            {
                if (row ? cell.Y == line : cell.X == line)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
