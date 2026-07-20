// PURPOSE: The result of resolving full rows/columns after a placement - which rows
// and columns cleared and the exact cells that exploded. Read by RoundEngine for
// scoring and the destruction log.

using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectBlock.Core
{
    /// <summary>Result of resolving full rows/columns after a placement.</summary>
    public sealed class LineExplosionResult
    {
        public static readonly LineExplosionResult None = new LineExplosionResult(
            Array.Empty<int>(), Array.Empty<int>(), Array.Empty<GridPos>());

        public IReadOnlyList<int> Rows { get; }
        public IReadOnlyList<int> Columns { get; }
        public IReadOnlyList<GridPos> ExplodedCells { get; }

        public int LineCount
        {
            get { return Rows.Count + Columns.Count; }
        }

        public LineExplosionResult(IReadOnlyList<int> rows, IReadOnlyList<int> columns,
            IReadOnlyList<GridPos> explodedCells)
        {
            Rows = rows;
            Columns = columns;
            ExplodedCells = explodedCells;
        }
    }
}
