// PURPOSE: "Simetri"'s payout, turned from cells into EVENTS the eye can follow. The rules hand over
// every matched pair of cells (SymmetryVisuals); shown as they are, that is either one big outline
// round half the board (too coarse - a tint, not a recognition) or every cell blinking on its own
// (too fine - cheap, a pinball table). The brief asks for neither: MEDIUM REGIONS, matched in pairs,
// 2-6 events a payout. This file is where that grouping happens, and it is pure arithmetic on cells
// (no UnityEngine) so it can be run and printed outside the editor.
//
// HOW A PAYOUT IS CUT UP (Build):
//   1. The pairs are split into PROPER pairs (a cell and its mirror on the other side) and SELF
//      cells (on the axis, or the centre of a half turn - each its own partner).
//   2. The primaries (the left / bottom / earlier cell of every proper pair) are grouped into
//      4-connected regions, and a region bigger than ChunkMaxCells is cut into BALANCED, compact
//      parts - five cells is three and two, never four and a lonely one.
//   3. A lone cell is folded into a region it touches (even diagonally) - a 1x1 must never be the
//      thing the eye lands on - and self cells are grouped the same way along their axis.
//   4. Too many events (a dense board) and the two nearest regions merge until the count is inside
//      the budget; a merged region may be two islands, which is fine - it is one EVENT.
//   5. Every region's partner is its mirror (or its half-turn image), and the events are ordered so
//      the eye travels: a left-right mirror top to bottom, a top-bottom one left to right, a half
//      turn round the clock - the centre's own cell last.
// Outline() then gives each region the loops its trace runs round: cell boundaries, interior on the
// LEFT (counter-clockwise), diagonal-touching cells kept as separate loops.
// EXTENSION POINT: another order is a value of OrderMode and a key in SortKey.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;

namespace ProjectBlock.View
{
    /// <summary>One matched EVENT: a region and its partner region (none when it is its own).</summary>
    public sealed class SymmetryChunkPair
    {
        public SymmetryKind Kind;
        public readonly List<GridPos> A = new List<GridPos>();
        public readonly List<GridPos> B = new List<GridPos>();
        public bool Self;
        public int Order;

        public float CentreAX
        {
            get { return Mean(A, true); }
        }

        public float CentreAY
        {
            get { return Mean(A, false); }
        }

        public float CentreBX
        {
            get { return Mean(Self ? A : B, true); }
        }

        public float CentreBY
        {
            get { return Mean(Self ? A : B, false); }
        }

        public int Cells
        {
            get { return A.Count + B.Count; }
        }

        private static float Mean(List<GridPos> cells, bool x)
        {
            if (cells.Count == 0)
            {
                return 0f;
            }
            float sum = 0f;
            foreach (GridPos c in cells)
            {
                sum += x ? c.X : c.Y;
            }
            return sum / cells.Count;
        }
    }

    /// <summary>A point in BOARD coordinates (a cell's centre is its integer coordinate).</summary>
    public struct BoardPoint
    {
        public float X;
        public float Y;

        public BoardPoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    public static class SymmetryChunks
    {
        public enum OrderMode
        {
            Auto,       // the brief's: mirrors in reading order, a half turn round the clock
            CentreOut,  // nearest the axis / centre first
            OutsideIn   // farthest first
        }

        /// <summary>The partner of a cell under one symmetry.</summary>
        public static GridPos Partner(SymmetryVisuals seen, SymmetryKind kind, GridPos cell)
        {
            int x = kind == SymmetryKind.TopBottom ? cell.X : 2 * seen.MinX + seen.Width - 1 - cell.X;
            int y = kind == SymmetryKind.LeftRight ? cell.Y : 2 * seen.MinY + seen.Height - 1 - cell.Y;
            return new GridPos(x, y);
        }

        /// <summary>
        /// The events one symmetry's payout plays, in order. <paramref name="maxPairs"/> is the
        /// budget (self events count against it), <paramref name="maxCells"/> the largest region
        /// before it is cut.
        /// </summary>
        public static List<SymmetryChunkPair> Build(SymmetryVisuals seen, SymmetryKind kind, int maxPairs, int maxCells,
            OrderMode mode)
        {
            var events = new List<SymmetryChunkPair>();
            if (seen == null)
            {
                return events;
            }
            maxPairs = Math.Max(1, maxPairs);
            maxCells = Math.Max(1, maxCells);
            var primaries = new List<GridPos>();
            var selves = new List<GridPos>();
            foreach (SymmetryPair p in seen.PairsOf(kind))
            {
                if (p.Self)
                {
                    selves.Add(p.A);
                }
                else
                {
                    primaries.Add(p.A);
                }
            }

            List<List<GridPos>> regions = Regions(primaries, maxCells);
            List<List<GridPos>> selfRegions = Regions(selves, maxCells);
            // the budget: merge the nearest regions until it fits (self events first, they are
            // the smaller part of what the eye reads)
            while (regions.Count + selfRegions.Count > maxPairs)
            {
                if (selfRegions.Count > 1 && (selfRegions.Count >= regions.Count || regions.Count <= 1))
                {
                    MergeNearest(selfRegions);
                }
                else if (regions.Count > 1)
                {
                    MergeNearest(regions);
                }
                else if (selfRegions.Count > 1)
                {
                    MergeNearest(selfRegions);
                }
                else
                {
                    break;
                }
            }

            foreach (List<GridPos> region in regions)
            {
                var e = new SymmetryChunkPair { Kind = kind };
                e.A.AddRange(region);
                foreach (GridPos c in region)
                {
                    e.B.Add(Partner(seen, kind, c));
                }
                events.Add(e);
            }
            foreach (List<GridPos> region in selfRegions)
            {
                var e = new SymmetryChunkPair { Kind = kind, Self = true };
                e.A.AddRange(region);
                events.Add(e);
            }

            float cx = seen.AxisX;
            float cy = seen.AxisY;
            events.Sort((a, b) => SortKey(a, kind, mode, cx, cy).CompareTo(SortKey(b, kind, mode, cx, cy)));
            for (int i = 0; i < events.Count; i++)
            {
                events[i].Order = i;
            }
            return events;
        }

        private static float SortKey(SymmetryChunkPair e, SymmetryKind kind, OrderMode mode, float cx, float cy)
        {
            float x = e.CentreAX;
            float y = e.CentreAY;
            // the centre's own cell is the finale of a half turn
            if (e.Self && kind == SymmetryKind.HalfTurn)
            {
                return 1e6f;
            }
            float off;
            switch (kind)
            {
                case SymmetryKind.LeftRight: off = Math.Abs(x - cx); break;
                case SymmetryKind.TopBottom: off = Math.Abs(y - cy); break;
                default: off = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)); break;
            }
            switch (mode)
            {
                case OrderMode.CentreOut:
                    return off * 100f - y;
                case OrderMode.OutsideIn:
                    return -off * 100f - y;
                default:
                    if (kind == SymmetryKind.LeftRight)
                    {
                        // top to bottom, and down a row the one nearer the axis first
                        return -y * 100f + off;
                    }
                    if (kind == SymmetryKind.TopBottom)
                    {
                        return x * 100f + off;
                    }
                    // round the clock from twelve
                    double angle = Math.Atan2(x - cx, y - cy);
                    if (angle < 0)
                    {
                        angle += Math.PI * 2;
                    }
                    return (float)angle * 100f + off;
            }
        }

        // ================================================================== regions

        /// <summary>4-connected regions, each cut into balanced compact parts of at most
        /// <paramref name="maxCells"/>, with lone cells folded into a neighbour.</summary>
        private static List<List<GridPos>> Regions(List<GridPos> cells, int maxCells)
        {
            var set = new HashSet<GridPos>(cells);
            var regions = new List<List<GridPos>>();
            var seen = new HashSet<GridPos>();
            // deterministic: walk in board order
            var ordered = new List<GridPos>(cells);
            ordered.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            foreach (GridPos start in ordered)
            {
                if (seen.Contains(start))
                {
                    continue;
                }
                var component = new List<GridPos>();
                var queue = new Queue<GridPos>();
                queue.Enqueue(start);
                seen.Add(start);
                while (queue.Count > 0)
                {
                    GridPos c = queue.Dequeue();
                    component.Add(c);
                    foreach (GridPos n in Neighbours4(c))
                    {
                        if (set.Contains(n) && seen.Add(n))
                        {
                            queue.Enqueue(n);
                        }
                    }
                }
                regions.AddRange(Split(component, maxCells));
            }
            FoldLoneCells(regions, maxCells);
            return regions;
        }

        /// <summary>Cuts a connected region into ceil(n / max) parts of near-equal size, each grown
        /// compactly from an END of what is left (a cell with the fewest neighbours remaining), so a
        /// long strip comes apart into segments and a blob into pieces.</summary>
        private static List<List<GridPos>> Split(List<GridPos> region, int maxCells)
        {
            var parts = new List<List<GridPos>>();
            if (region.Count <= maxCells)
            {
                parts.Add(region);
                return parts;
            }
            int count = (region.Count + maxCells - 1) / maxCells;
            var left = new HashSet<GridPos>(region);
            for (int p = 0; p < count && left.Count > 0; p++)
            {
                int target = (left.Count + (count - p) - 1) / (count - p);
                // the seed: an end of what is left
                GridPos seed = default(GridPos);
                int fewest = int.MaxValue;
                foreach (GridPos c in Ordered(left))
                {
                    int n = 0;
                    foreach (GridPos m in Neighbours4(c))
                    {
                        if (left.Contains(m))
                        {
                            n++;
                        }
                    }
                    if (n < fewest)
                    {
                        fewest = n;
                        seed = c;
                    }
                }
                var part = new List<GridPos> { seed };
                left.Remove(seed);
                while (part.Count < target)
                {
                    // the frontier cell nearest the seed: compact
                    GridPos best = default(GridPos);
                    int bestD = int.MaxValue;
                    bool found = false;
                    foreach (GridPos c in part)
                    {
                        foreach (GridPos m in Neighbours4(c))
                        {
                            if (!left.Contains(m))
                            {
                                continue;
                            }
                            int d = Math.Abs(m.X - seed.X) + Math.Abs(m.Y - seed.Y);
                            if (d < bestD || (d == bestD && Before(m, best)))
                            {
                                bestD = d;
                                best = m;
                                found = true;
                            }
                        }
                    }
                    if (!found)
                    {
                        break;
                    }
                    part.Add(best);
                    left.Remove(best);
                }
                parts.Add(part);
            }
            // anything cut off from every part on the way (never in a strip; rare in a blob)
            foreach (GridPos c in Ordered(left))
            {
                parts.Add(new List<GridPos> { c });
            }
            return parts;
        }

        /// <summary>A 1x1 region is folded into the smallest region it touches, diagonals included.</summary>
        private static void FoldLoneCells(List<List<GridPos>> regions, int maxCells)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < regions.Count; i++)
                {
                    if (regions[i].Count != 1)
                    {
                        continue;
                    }
                    GridPos lone = regions[i][0];
                    int into = -1;
                    for (int j = 0; j < regions.Count; j++)
                    {
                        if (j == i || regions[j].Count >= maxCells + 1)
                        {
                            continue;
                        }
                        if (Touches8(regions[j], lone) && (into < 0 || regions[j].Count < regions[into].Count))
                        {
                            into = j;
                        }
                    }
                    if (into >= 0)
                    {
                        regions[into].Add(lone);
                        regions.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }
            }
        }

        private static void MergeNearest(List<List<GridPos>> regions)
        {
            int bi = 0;
            int bj = 1;
            float best = float.MaxValue;
            for (int i = 0; i < regions.Count; i++)
            {
                for (int j = i + 1; j < regions.Count; j++)
                {
                    float d = Gap(regions[i], regions[j]) * 10f + regions[i].Count + regions[j].Count;
                    if (d < best)
                    {
                        best = d;
                        bi = i;
                        bj = j;
                    }
                }
            }
            regions[bi].AddRange(regions[bj]);
            regions.RemoveAt(bj);
        }

        /// <summary>The nearest two cells of two regions, in cells (Chebyshev).</summary>
        private static float Gap(List<GridPos> a, List<GridPos> b)
        {
            int best = int.MaxValue;
            foreach (GridPos p in a)
            {
                foreach (GridPos q in b)
                {
                    best = Math.Min(best, Math.Max(Math.Abs(p.X - q.X), Math.Abs(p.Y - q.Y)));
                }
            }
            return best;
        }

        private static bool Touches8(List<GridPos> region, GridPos cell)
        {
            foreach (GridPos c in region)
            {
                if (Math.Abs(c.X - cell.X) <= 1 && Math.Abs(c.Y - cell.Y) <= 1)
                {
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<GridPos> Neighbours4(GridPos c)
        {
            yield return new GridPos(c.X + 1, c.Y);
            yield return new GridPos(c.X - 1, c.Y);
            yield return new GridPos(c.X, c.Y + 1);
            yield return new GridPos(c.X, c.Y - 1);
        }

        private static List<GridPos> Ordered(HashSet<GridPos> cells)
        {
            var list = new List<GridPos>(cells);
            list.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            return list;
        }

        private static bool Before(GridPos a, GridPos b)
        {
            return a.Y != b.Y ? a.Y < b.Y : a.X < b.X;
        }

        // ================================================================== geometry (shared by the view and its renders)

        /// <summary>A point of a traced loop and the direction INTO its region, in board units.</summary>
        public struct LoopPoint
        {
            public BoardPoint P;
            public BoardPoint In;
        }

        /// <summary>
        /// A region's loop as the pen runs it: every corner rounded to <paramref name="radius"/> (a
        /// quadratic through the corner, so a cell's rounded square and an L's inner corner both
        /// come out soft), the straight runs sampled every <paramref name="step"/>, and the whole
        /// thing pushed <paramref name="outset"/> outward off the cells, with the inward normal at
        /// every point (the loop is counter-clockwise, so in is to the LEFT).
        /// </summary>
        public static List<LoopPoint> RoundedLoop(List<BoardPoint> corners, float radius, float outset, float step)
        {
            var result = new List<LoopPoint>();
            int n = corners.Count;
            if (n < 3)
            {
                return result;
            }
            var pts = new List<BoardPoint>();
            for (int i = 0; i < n; i++)
            {
                BoardPoint prev = corners[(i + n - 1) % n];
                BoardPoint c = corners[i];
                BoardPoint next = corners[(i + 1) % n];
                float lin = Dist(prev, c);
                float lout = Dist(next, c);
                float rIn = Math.Min(radius, lin * 0.5f);
                float rOut = Math.Min(radius, lout * 0.5f);
                var a = new BoardPoint(c.X + (prev.X - c.X) / lin * rIn, c.Y + (prev.Y - c.Y) / lin * rIn);
                var b = new BoardPoint(c.X + (next.X - c.X) / lout * rOut, c.Y + (next.Y - c.Y) / lout * rOut);
                if (pts.Count > 0)
                {
                    SampleRun(pts, pts[pts.Count - 1], a, step);
                }
                for (int k = 0; k <= 4; k++)
                {
                    float u = k / 4f;
                    pts.Add(new BoardPoint((1 - u) * (1 - u) * a.X + 2 * (1 - u) * u * c.X + u * u * b.X,
                        (1 - u) * (1 - u) * a.Y + 2 * (1 - u) * u * c.Y + u * u * b.Y));
                }
            }
            SampleRun(pts, pts[pts.Count - 1], pts[0], step);
            int m = pts.Count;
            for (int i = 0; i < m; i++)
            {
                BoardPoint p0 = pts[(i + m - 1) % m];
                BoardPoint p1 = pts[(i + 1) % m];
                float tx = p1.X - p0.X;
                float ty = p1.Y - p0.Y;
                float len = (float)Math.Sqrt(tx * tx + ty * ty);
                if (len < 1e-6f)
                {
                    len = 1f;
                }
                var left = new BoardPoint(-ty / len, tx / len);
                result.Add(new LoopPoint
                {
                    P = new BoardPoint(pts[i].X - left.X * outset, pts[i].Y - left.Y * outset),
                    In = left
                });
            }
            return result;
        }

        private static void SampleRun(List<BoardPoint> pts, BoardPoint from, BoardPoint to, float step)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(Dist(from, to) / Math.Max(0.01f, step)));
            for (int k = 1; k < steps; k++)
            {
                float u = k / (float)steps;
                pts.Add(new BoardPoint(from.X + (to.X - from.X) * u, from.Y + (to.Y - from.Y) * u));
            }
        }

        private static float Dist(BoardPoint a, BoardPoint b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Where a pair's energy leaves each region and where the two meet - always ON THE
        /// REGION'S OWN EDGE, never inside it (from a region's middle the energy would cut across
        /// its own cubes, a scratch rather than a flow): for a mirror, the edge FACING the axis of
        /// the facing cell nearest the region's middle row (or column), meeting ON the axis along
        /// that row; for a half turn, the region's boundary point nearest the board's centre,
        /// meeting AT the centre (the partner's is the same point turned upside down); a self
        /// region is its own meeting point.
        /// </summary>
        public static void Anchors(SymmetryVisuals seen, SymmetryChunkPair chunk, out BoardPoint a, out BoardPoint b,
            out BoardPoint meet)
        {
            float cx = seen.AxisX;
            float cy = seen.AxisY;
            if (chunk.Self)
            {
                a = new BoardPoint(chunk.CentreAX, chunk.CentreAY);
                b = a;
                meet = a;
                return;
            }
            if (chunk.Kind == SymmetryKind.LeftRight || chunk.Kind == SymmetryKind.TopBottom)
            {
                bool lr = chunk.Kind == SymmetryKind.LeftRight;
                // the side facing the axis, and of the cells on it the one nearest the middle row
                bool lowSide = lr ? chunk.CentreAX < cx : chunk.CentreAY < cy;
                float edge = lowSide ? float.MinValue : float.MaxValue;
                foreach (GridPos c in chunk.A)
                {
                    float v = lr ? c.X : c.Y;
                    edge = lowSide ? Math.Max(edge, v) : Math.Min(edge, v);
                }
                float mid = lr ? chunk.CentreAY : chunk.CentreAX;
                float along = mid;
                float best = float.MaxValue;
                foreach (GridPos c in chunk.A)
                {
                    if ((lr ? c.X : c.Y) != edge)
                    {
                        continue;
                    }
                    float d = Math.Abs((lr ? c.Y : c.X) - mid);
                    if (d < best)
                    {
                        best = d;
                        along = lr ? c.Y : c.X;
                    }
                }
                float face = edge + (lowSide ? 0.5f : -0.5f);
                if (lr)
                {
                    a = new BoardPoint(face, along);
                    b = new BoardPoint(2f * cx - face, along);
                    meet = new BoardPoint(cx, along);
                }
                else
                {
                    a = new BoardPoint(along, face);
                    b = new BoardPoint(along, 2f * cy - face);
                    meet = new BoardPoint(along, cy);
                }
                return;
            }
            // a half turn: the region's boundary point nearest the centre
            float bx = chunk.CentreAX;
            float by = chunk.CentreAY;
            float bestD = float.MaxValue;
            foreach (GridPos c in chunk.A)
            {
                float px = Math.Max(c.X - 0.5f, Math.Min(cx, c.X + 0.5f));
                float py = Math.Max(c.Y - 0.5f, Math.Min(cy, c.Y + 0.5f));
                float d = (px - cx) * (px - cx) + (py - cy) * (py - cy);
                if (d < bestD)
                {
                    bestD = d;
                    bx = px;
                    by = py;
                }
            }
            a = new BoardPoint(bx, by);
            b = new BoardPoint(2f * cx - bx, 2f * cy - by);
            meet = new BoardPoint(cx, cy);
        }

        /// <summary>The energy's road from a region to the meeting point: straight for a mirror,
        /// spiralling in on an arc for a half turn - and the partner's arc, built the same way from
        /// the opposite side, is this one turned upside down.</summary>
        public static List<BoardPoint> EnergyPath(BoardPoint from, BoardPoint to, bool arc, int steps)
        {
            var path = new List<BoardPoint>();
            float cxp = (from.X + to.X) * 0.5f;
            float cyp = (from.Y + to.Y) * 0.5f;
            if (arc)
            {
                float dx = from.X - to.X;
                float dy = from.Y - to.Y;
                float ang = 48f * (float)Math.PI / 180f;
                float rx = dx * (float)Math.Cos(ang) - dy * (float)Math.Sin(ang);
                float ry = dx * (float)Math.Sin(ang) + dy * (float)Math.Cos(ang);
                cxp = to.X + rx * 0.62f;
                cyp = to.Y + ry * 0.62f;
            }
            for (int k = 0; k <= steps; k++)
            {
                float u = k / (float)steps;
                path.Add(new BoardPoint((1 - u) * (1 - u) * from.X + 2 * (1 - u) * u * cxp + u * u * to.X,
                    (1 - u) * (1 - u) * from.Y + 2 * (1 - u) * u * cyp + u * u * to.Y));
            }
            return path;
        }

        // ================================================================== outlines

        /// <summary>
        /// The loops round a set of cells, on the cell boundaries (a cell (x, y) spans x-0.5 to
        /// x+0.5), counter-clockwise - the interior on the LEFT - with the straight runs reduced to
        /// their corners. Two cells touching only at a corner come out as two loops.
        /// </summary>
        public static List<List<BoardPoint>> Outline(IEnumerable<GridPos> cells)
        {
            var set = new HashSet<GridPos>(cells);
            // directed boundary edges, keyed by their start corner (corners on a doubled lattice)
            var edges = new Dictionary<long, List<long>>();
            foreach (GridPos c in set)
            {
                int x0 = c.X * 2 - 1;
                int x1 = c.X * 2 + 1;
                int y0 = c.Y * 2 - 1;
                int y1 = c.Y * 2 + 1;
                if (!set.Contains(new GridPos(c.X, c.Y - 1)))
                {
                    AddEdge(edges, x0, y0, x1, y0);
                }
                if (!set.Contains(new GridPos(c.X + 1, c.Y)))
                {
                    AddEdge(edges, x1, y0, x1, y1);
                }
                if (!set.Contains(new GridPos(c.X, c.Y + 1)))
                {
                    AddEdge(edges, x1, y1, x0, y1);
                }
                if (!set.Contains(new GridPos(c.X - 1, c.Y)))
                {
                    AddEdge(edges, x0, y1, x0, y0);
                }
            }
            var loops = new List<List<BoardPoint>>();
            var starts = new List<long>(edges.Keys);
            starts.Sort();
            foreach (long first in starts)
            {
                while (edges.ContainsKey(first) && edges[first].Count > 0)
                {
                    var corners = new List<long>();
                    long at = first;
                    long prev = long.MinValue;
                    int guard = 0;
                    do
                    {
                        List<long> outs = edges[at];
                        int pick = 0;
                        if (outs.Count > 1 && prev != long.MinValue)
                        {
                            // a pinch (two cells touching at a corner): turn LEFT, staying on the
                            // cell we came round, so the two come out as two loops
                            pick = LeftmostTurn(prev, at, outs);
                        }
                        long next = outs[pick];
                        outs.RemoveAt(pick);
                        corners.Add(at);
                        prev = at;
                        at = next;
                    }
                    while (at != first && ++guard < 10000);
                    loops.Add(Simplify(corners));
                }
            }
            return loops;
        }

        private static long Key(int x, int y)
        {
            return ((long)(x + 100000) << 32) | (uint)(y + 100000);
        }

        private static int KX(long k)
        {
            return (int)(k >> 32) - 100000;
        }

        private static int KY(long k)
        {
            return (int)(uint)(k & 0xffffffff) - 100000;
        }

        private static void AddEdge(Dictionary<long, List<long>> edges, int x0, int y0, int x1, int y1)
        {
            long a = Key(x0, y0);
            List<long> list;
            if (!edges.TryGetValue(a, out list))
            {
                list = new List<long>();
                edges[a] = list;
            }
            list.Add(Key(x1, y1));
        }

        private static int LeftmostTurn(long prev, long at, List<long> outs)
        {
            int dx = KX(at) - KX(prev);
            int dy = KY(at) - KY(prev);
            int best = 0;
            int bestScore = int.MinValue;
            for (int i = 0; i < outs.Count; i++)
            {
                int ox = KX(outs[i]) - KX(at);
                int oy = KY(outs[i]) - KY(at);
                // cross > 0 is a left turn, then straight, then right
                int cross = dx * oy - dy * ox;
                int dot = dx * ox + dy * oy;
                int score = cross > 0 ? 2 : (cross == 0 && dot > 0 ? 1 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Corners only (the straight runs' middle points dropped), in board units.</summary>
        private static List<BoardPoint> Simplify(List<long> corners)
        {
            var points = new List<BoardPoint>();
            int n = corners.Count;
            for (int i = 0; i < n; i++)
            {
                long p = corners[(i + n - 1) % n];
                long c = corners[i];
                long q = corners[(i + 1) % n];
                int ax = KX(c) - KX(p);
                int ay = KY(c) - KY(p);
                int bx = KX(q) - KX(c);
                int by = KY(q) - KY(c);
                if (ax * by - ay * bx == 0)
                {
                    continue;
                }
                points.Add(new BoardPoint(KX(c) * 0.5f, KY(c) * 0.5f));
            }
            return points;
        }
    }
}
