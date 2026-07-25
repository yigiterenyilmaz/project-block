// PURPOSE: The scoring facts of one line explosion, split by axis so a boss round can pay
// for one axis only ("Ufuk", "Kule"). Built by RoundEngine with the retro dead zone already
// removed, so a boss never has to know the dead zone exists.

namespace ProjectBlock.Core
{
    /// <summary>What a line explosion is worth, split by axis. All counts are SCORING counts:
    /// rows/cubes inside the retro dead zone are already gone.</summary>
    public readonly struct LineExplosionScore
    {
        /// <summary>Full rows that exploded and score.</summary>
        public readonly int Rows;

        /// <summary>Full columns that exploded and score.</summary>
        public readonly int Columns;

        /// <summary>Every cube this explosion destroyed and scores for. This can exceed
        /// RowCubes + ColumnCubes: a dynamite block adds a whole-board wipe to the same
        /// explosion, and those cubes belong to no line at all.</summary>
        public readonly int Cubes;

        /// <summary>Destroyed cubes standing on an exploded row. A cube at a row/column
        /// crossing is counted in BOTH RowCubes and ColumnCubes - that is deliberate: each
        /// axis is priced as if it had exploded alone.</summary>
        public readonly int RowCubes;

        /// <summary>Destroyed cubes standing in an exploded column (see RowCubes).</summary>
        public readonly int ColumnCubes;

        public LineExplosionScore(int rows, int columns, int cubes, int rowCubes, int columnCubes)
        {
            Rows = rows;
            Columns = columns;
            Cubes = cubes;
            RowCubes = rowCubes;
            ColumnCubes = columnCubes;
        }
    }
}
