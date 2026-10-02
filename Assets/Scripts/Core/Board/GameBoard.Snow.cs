// PURPOSE: SNOW (the "Kar" block) on the board - melting, falling and merging into HEAPS, and the
// avalanche the "Çığ" power makes of a heap (designer's calls, 2026-10-02).
//
// A SNOW CUBE CARRIES THREE NUMBERS ON ITSELF (Cube.SnowPower / SnowMelt / SnowPacked), so every
// move, copy, resize, rewind and save takes them along without knowing snow exists.
//
// THE RULES:
//  - It FALLS like water: one cell a pass along GameBoard.WaterFlow, cube by cube, inside
//    SettleWaterAndReact - so every caller of that settle (the turn, a gravity turn, the wind,
//    "Meydan Okuma"'s futures) moves snow without being told to.
//  - A HEAP ("kar öbeği") is a run of snow cubes side by side ACROSS the flow (a row, while
//    gravity points down). A heap shares ONE power and ONE melt time: joined runs take the larger
//    of each (NormalizeSnow), so the numbers on a heap's cubes never disagree.
//  - Snow that comes to rest ON snow is ABSORBED: the cubes that landed vanish into the heap under
//    them and that heap gains the arriving heap's power - once per arriving heap, however many of
//    its cubes landed. The melt time follows SnowRules.MergedMelt.
//  - It MELTS: the timer drops by one at the top of every turn (TickSnowMelt) and the cube goes
//    at zero. Melting is not a destruction - nothing is scored, logged or swept.
//  - AN AVALANCHE takes every heap in one line across the flow and sends each one down as many
//    cells as it has power: the heap leaves its line, everything in its way is crushed (gold and
//    obsidian too), and the cells it covers hold PACKED snow of power 1 that melts sooner. Two
//    packed cubes never merge with each other, so the layers stay stacked; fresh snow landing on
//    a packed layer is absorbed as usual, and that is what makes the layer able to slide again.
//    A black hole, a "Parazit" host, the boss's snake, a "Mayın" trap, a press capsule, a sealed
//    cell and the edge of the play area stop a column; what cannot come down is lost.
//
// The board only PLANS an avalanche and applies the snow's own half of it. The crushing goes
// through RoundEngine.DestroyCubes like every other destruction (RoundEngine.Snow).

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>The snow numbers. BALANCE PLACEHOLDERS, except MergedMelt, which is the
    /// designer's table.</summary>
    public static class SnowRules
    {
        /// <summary>Turns a freshly placed snow cube stands before it melts.</summary>
        public const int MeltTurns = 5;

        /// <summary>Turns the snow an avalanche lays stands.</summary>
        public const int AvalancheMeltTurns = 3;

        /// <summary>The longest bar the market sells snow as (1x1 .. 1xN, never two rows).</summary>
        public const int MaxBarLength = 4;

        /// <summary>
        /// The melt time of a heap that absorbed another. The designer's table, for a heap with
        /// three turns left: five arrives - five; four - four; three - FOUR (two equal heaps
        /// pack each other, one turn's worth); two or less - still three. Never past MeltTurns.
        /// </summary>
        public static int MergedMelt(int below, int arriving)
        {
            if (arriving > below)
            {
                return arriving;
            }
            if (arriving == below && below < MeltTurns)
            {
                return below + 1;
            }
            return below;
        }

        /// <summary>Can the "Çığ" power send this cube's heap down? Everything but a packed
        /// layer that has taken no fresh snow since the avalanche that laid it.</summary>
        public static bool CanSlide(Cube cube)
        {
            return cube.Kind == CubeKind.Snow && !cube.Protected
                && !(cube.SnowPacked && cube.SnowPower <= 1);
        }

        /// <summary>A snow cube with numbers that mean something: one written without them (a
        /// retype, a conjured cube) is a fresh one.</summary>
        public static Cube Sane(Cube cube)
        {
            if (cube.Kind != CubeKind.Snow || (cube.SnowPower > 0 && cube.SnowMelt > 0))
            {
                return cube;
            }
            return new Cube(CubeKind.Snow, cube.SourceCardId, cube.Protected,
                cube.SnowPower > 0 ? cube.SnowPower : 1,
                cube.SnowMelt > 0 ? cube.SnowMelt : MeltTurns, cube.SnowPacked);
        }
    }

    /// <summary>One column of an avalanche: the heap cube that slides, the cells it comes down
    /// over (nearest first), and what stood in them.</summary>
    public sealed class AvalancheColumn
    {
        public GridPos Source;
        public Cube SourceCube;
        public readonly List<GridPos> Covered = new List<GridPos>();

        /// <summary>The cubes in the covered cells, by cell - crushed when the snow arrives.</summary>
        public readonly List<DestroyedCube> Crushed = new List<DestroyedCube>();
    }

    /// <summary>What an avalanche on one line WOULD do. Planned by the board, applied by the
    /// engine; also what the View previews and, afterwards, animates.</summary>
    public sealed class AvalanchePlan
    {
        /// <summary>The one-cell step the snow comes down along (the board's WaterFlow).</summary>
        public GridPos Flow;

        public readonly List<AvalancheColumn> Columns = new List<AvalancheColumn>();

        public bool Any
        {
            get { return Columns.Count > 0; }
        }

        /// <summary>Every cell the snow will cover, all columns.</summary>
        public List<GridPos> CoveredCells()
        {
            var cells = new List<GridPos>();
            for (int i = 0; i < Columns.Count; i++)
            {
                cells.AddRange(Columns[i].Covered);
            }
            return cells;
        }

        /// <summary>Every cell holding a cube the snow will crush.</summary>
        public List<GridPos> CrushedCells()
        {
            var cells = new List<GridPos>();
            for (int i = 0; i < Columns.Count; i++)
            {
                for (int c = 0; c < Columns[i].Crushed.Count; c++)
                {
                    cells.Add(Columns[i].Crushed[c].Pos);
                }
            }
            return cells;
        }
    }

    partial class GameBoard
    {
        /// <summary>The snow that melted on the last TickSnowMelt, and where. Reporting only.</summary>
        public readonly List<DestroyedCube> LastSnowMelted = new List<DestroyedCube>();

        /// <summary>The snow cubes the last settle ABSORBED into the heap under them: where each
        /// stood and the cell it went into. Reporting only, rewritten by every settle.</summary>
        public readonly List<WaterMove> LastSnowMerges = new List<WaterMove>();

        /// <summary>True if any snow stands on the board.</summary>
        public bool HasSnow
        {
            get
            {
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        if (cells[x, y].HasValue && cells[x, y].Value.Kind == CubeKind.Snow)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// One turn passes for the snow: every cube's melt time drops by one and what reaches
        /// zero is gone. Returns (and keeps in LastSnowMelted) what melted. A "Parazit" host does
        /// not melt - only the player's line takes a host.
        ///
        /// Melting is NOT a destruction: the caller re-baselines the destruction diff afterwards.
        /// </summary>
        public IReadOnlyList<DestroyedCube> TickSnowMelt()
        {
            LastSnowMelted.Clear();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Cube? cube = cells[x, y];
                    if (!cube.HasValue || cube.Value.Kind != CubeKind.Snow || cube.Value.Protected)
                    {
                        continue;
                    }
                    Cube snow = SnowRules.Sane(cube.Value);
                    if (snow.SnowMelt <= 1)
                    {
                        LastSnowMelted.Add(new DestroyedCube(new GridPos(x + MinX, y + MinY), snow));
                        cells[x, y] = null;
                        OccupiedCount--;
                    }
                    else
                    {
                        cells[x, y] = snow.WithSnow(snow.SnowPower, snow.SnowMelt - 1, snow.SnowPacked);
                    }
                }
            }
            return LastSnowMelted;
        }

        // ---- lines across the flow ----------------------------------------------------------
        //
        // A heap is a run ACROSS the flow, and "below" is one line further ALONG it. With gravity
        // pointing down that is a row and the row under it; turned sideways it is a column and the
        // column beside it. Everything below works in (line, along) and converts at the edges.

        private bool SnowFlowVertical
        {
            get { return WaterFlow.X == 0; }
        }

        private int SnowLineCount
        {
            get { return SnowFlowVertical ? Height : Width; }
        }

        private int SnowLineLength
        {
            get { return SnowFlowVertical ? Width : Height; }
        }

        /// <summary>+1 or -1: which way along the line index the snow falls.</summary>
        private int SnowLineStep
        {
            get { return SnowFlowVertical ? WaterFlow.Y : WaterFlow.X; }
        }

        private Cube? SnowAt(int line, int along)
        {
            return SnowFlowVertical ? cells[along, line] : cells[line, along];
        }

        private void SnowSet(int line, int along, Cube? cube)
        {
            if (SnowFlowVertical)
            {
                cells[along, line] = cube;
            }
            else
            {
                cells[line, along] = cube;
            }
        }

        private GridPos SnowPos(int line, int along)
        {
            return SnowFlowVertical
                ? new GridPos(along + MinX, line + MinY)
                : new GridPos(line + MinX, along + MinY);
        }

        private bool IsSnow(int line, int along)
        {
            if (along < 0 || along >= SnowLineLength || line < 0 || line >= SnowLineCount)
            {
                return false;
            }
            Cube? cube = SnowAt(line, along);
            return cube.HasValue && cube.Value.Kind == CubeKind.Snow;
        }

        /// <summary>The run of snow around <paramref name="along"/> in a line: first and last
        /// index, inclusive.</summary>
        private void SnowRun(int line, int along, out int first, out int last)
        {
            first = along;
            last = along;
            while (IsSnow(line, first - 1))
            {
                first--;
            }
            while (IsSnow(line, last + 1))
            {
                last++;
            }
        }

        /// <summary>Makes every heap agree with itself: the larger power, the longer melt time,
        /// packed if any of it is. Runs join when a cube comes to rest beside a heap, and a heap
        /// has one power and one clock.</summary>
        private bool NormalizeSnow()
        {
            bool changed = false;
            for (int line = 0; line < SnowLineCount; line++)
            {
                int along = 0;
                while (along < SnowLineLength)
                {
                    if (!IsSnow(line, along))
                    {
                        along++;
                        continue;
                    }
                    int first;
                    int last;
                    SnowRun(line, along, out first, out last);
                    int power = 1;
                    int melt = 1;
                    bool packed = false;
                    for (int i = first; i <= last; i++)
                    {
                        Cube snow = SnowRules.Sane(SnowAt(line, i).Value);
                        power = snow.SnowPower > power ? snow.SnowPower : power;
                        melt = snow.SnowMelt > melt ? snow.SnowMelt : melt;
                        packed |= snow.SnowPacked;
                    }
                    for (int i = first; i <= last; i++)
                    {
                        Cube snow = SnowAt(line, i).Value;
                        if (snow.SnowPower != power || snow.SnowMelt != melt || snow.SnowPacked != packed)
                        {
                            SnowSet(line, i, snow.WithSnow(power, melt, packed));
                            changed = true;
                        }
                    }
                    along = last + 1;
                }
            }
            return changed;
        }

        /// <summary>
        /// One pass of the snow's settle: heaps resting on snow are absorbed by it, then every
        /// cube with nothing under it drops a cell. Lines are walked from the side the snow drains
        /// toward, so an absorb is settled before the line above it is looked at and a column
        /// drops together. Moves and absorbs both go into <paramref name="frame"/> (an absorb is
        /// a move into the heap's cell). Returns true if anything changed.
        /// </summary>
        private bool SnowPass(ref List<WaterMove> frame, bool report)
        {
            bool changed = NormalizeSnow();
            int step = SnowLineStep;
            int from = step < 0 ? 0 : SnowLineCount - 1;
            int end = step < 0 ? SnowLineCount : -1;
            int walk = step < 0 ? 1 : -1;
            for (int line = from; line != end; line += walk)
            {
                int below = line + step;
                if (below < 0 || below >= SnowLineCount)
                {
                    continue; // the edge of the arena holds this line
                }
                // ---- absorb: each heap in this line, into the heaps it rests on
                int along = 0;
                while (along < SnowLineLength)
                {
                    if (!IsSnow(line, along))
                    {
                        along++;
                        continue;
                    }
                    int first;
                    int last;
                    SnowRun(line, along, out first, out last);
                    Cube arriving = SnowAt(line, first).Value;
                    int lastTarget = int.MinValue; // the heap below already fed by this one
                    for (int i = first; i <= last; i++)
                    {
                        Cube snow = SnowAt(line, i).Value;
                        if (snow.Protected || !IsSnow(below, i))
                        {
                            continue;
                        }
                        Cube under = SnowAt(below, i).Value;
                        if (snow.SnowPacked && under.SnowPacked)
                        {
                            continue; // two layers of one avalanche stay two layers
                        }
                        int targetFirst;
                        int targetLast;
                        SnowRun(below, i, out targetFirst, out targetLast);
                        if (targetFirst != lastTarget)
                        {
                            lastTarget = targetFirst;
                            int power = under.SnowPower + arriving.SnowPower;
                            int melt = SnowRules.MergedMelt(under.SnowMelt, arriving.SnowMelt);
                            for (int t = targetFirst; t <= targetLast; t++)
                            {
                                Cube heap = SnowAt(below, t).Value;
                                SnowSet(below, t, heap.WithSnow(power, melt, heap.SnowPacked));
                            }
                        }
                        SnowSet(line, i, null);
                        OccupiedCount--;
                        changed = true;
                        var move = new WaterMove(SnowPos(line, i), SnowPos(below, i));
                        LastSnowMerges.Add(move);
                        if (report)
                        {
                            if (frame == null)
                            {
                                frame = new List<WaterMove>();
                            }
                            frame.Add(move);
                        }
                    }
                    along = last + 1;
                }
                // ---- fall: what has nothing under it drops one cell
                for (int i = 0; i < SnowLineLength; i++)
                {
                    if (!IsSnow(line, i))
                    {
                        continue;
                    }
                    Cube snow = SnowAt(line, i).Value;
                    GridPos to = SnowPos(below, i);
                    if (snow.Protected || !IsInside(to) || SnowAt(below, i).HasValue)
                    {
                        continue;
                    }
                    SnowSet(below, i, snow);
                    SnowSet(line, i, null);
                    changed = true;
                    if (report)
                    {
                        if (frame == null)
                        {
                            frame = new List<WaterMove>();
                        }
                        frame.Add(new WaterMove(SnowPos(line, i), to));
                    }
                }
            }
            return changed;
        }

        // ---- the avalanche ------------------------------------------------------------------

        /// <summary>Can the snow come down onto this cube? It crushes anything a line could not -
        /// gold and obsidian included - but not the things nothing may take or cover.</summary>
        private static bool SnowCrushes(Cube cube)
        {
            return CubeRules.CanBeSwallowed(cube);
        }

        /// <summary>
        /// What an avalanche on the line through <paramref name="cell"/> (the line ACROSS the
        /// flow - the cell's row, while gravity points down) would do: every heap there that can
        /// slide comes down as many cells as it has power, column by column, each column stopping
        /// at the edge of the play area, a sealed cell or a cube the snow cannot cover. A column
        /// that cannot move at all is left out. Changes nothing.
        /// </summary>
        public AvalanchePlan PlanAvalanche(GridPos cell)
        {
            var plan = new AvalanchePlan { Flow = WaterFlow };
            int line = SnowFlowVertical ? cell.Y - MinY : cell.X - MinX;
            if (line < 0 || line >= SnowLineCount)
            {
                return plan;
            }
            // Heap by heap rather than cube by cube: the heap's numbers are read off the whole
            // run, so a run that has only just joined still slides as one.
            int along = 0;
            while (along < SnowLineLength)
            {
                if (!IsSnow(line, along))
                {
                    along++;
                    continue;
                }
                int first;
                int last;
                SnowRun(line, along, out first, out last);
                int power = 1;
                bool packed = false;
                for (int i = first; i <= last; i++)
                {
                    Cube snow = SnowRules.Sane(SnowAt(line, i).Value);
                    power = snow.SnowPower > power ? snow.SnowPower : power;
                    packed |= snow.SnowPacked;
                }
                if (!(packed && power <= 1))
                {
                    for (int i = first; i <= last; i++)
                    {
                        Cube snow = SnowAt(line, i).Value;
                        if (snow.Protected)
                        {
                            continue;
                        }
                        var column = new AvalancheColumn { Source = SnowPos(line, i), SourceCube = snow };
                        for (int k = 1; k <= power; k++)
                        {
                            int target = line + SnowLineStep * k;
                            if (target < 0 || target >= SnowLineCount)
                            {
                                break;
                            }
                            GridPos at = SnowPos(target, i);
                            if (!IsInside(at) || IsSealed(at))
                            {
                                break;
                            }
                            Cube? under = SnowAt(target, i);
                            if (under.HasValue && !SnowCrushes(under.Value))
                            {
                                break;
                            }
                            column.Covered.Add(at);
                            if (under.HasValue)
                            {
                                column.Crushed.Add(new DestroyedCube(at, under.Value));
                            }
                        }
                        if (column.Covered.Count > 0)
                        {
                            plan.Columns.Add(column);
                        }
                    }
                }
                along = last + 1;
            }
            return plan;
        }

        /// <summary>
        /// The snow's own half of an avalanche: the heap cubes leave their line and every covered
        /// cell holds a packed layer. The cubes in the way must already have been destroyed
        /// (RoundEngine.DestroyCubes) - a cell that still holds one is skipped, which is what a
        /// refused destroy means. Returns the cells that took snow.
        /// </summary>
        internal List<GridPos> ApplyAvalanche(AvalanchePlan plan)
        {
            var laid = new List<GridPos>();
            for (int c = 0; c < plan.Columns.Count; c++)
            {
                AvalancheColumn column = plan.Columns[c];
                Cube? source = GetCube(column.Source);
                if (!source.HasValue || source.Value.Kind != CubeKind.Snow)
                {
                    continue;
                }
                cells[column.Source.X - MinX, column.Source.Y - MinY] = null;
                OccupiedCount--;
                for (int i = 0; i < column.Covered.Count; i++)
                {
                    GridPos at = column.Covered[i];
                    if (cells[at.X - MinX, at.Y - MinY].HasValue)
                    {
                        break; // something refused to be crushed: the column ends on it
                    }
                    cells[at.X - MinX, at.Y - MinY] = new Cube(CubeKind.Snow, source.Value.SourceCardId,
                        false, 1, SnowRules.AvalancheMeltTurns, true);
                    OccupiedCount++;
                    laid.Add(at);
                }
            }
            return laid;
        }

        /// <summary>ApplyAvalanche for the animation lab, which runs an avalanche on a board of
        /// its own and does the crushing itself (the engine is what does it in a round).</summary>
        public List<GridPos> ApplyAvalancheOnLabBoard(AvalanchePlan plan)
        {
            return ApplyAvalanche(plan);
        }

        /// <summary>The heap a snow cube belongs to: every cell of its run across the flow. Empty
        /// when the cell holds no snow. For the View (a heap is drawn as one thing) and tests.</summary>
        public List<GridPos> SnowHeapAt(GridPos cell)
        {
            var heap = new List<GridPos>();
            int line = SnowFlowVertical ? cell.Y - MinY : cell.X - MinX;
            int along = SnowFlowVertical ? cell.X - MinX : cell.Y - MinY;
            if (!IsSnow(line, along))
            {
                return heap;
            }
            int first;
            int last;
            SnowRun(line, along, out first, out last);
            for (int i = first; i <= last; i++)
            {
                heap.Add(SnowPos(line, i));
            }
            return heap;
        }
    }
}
