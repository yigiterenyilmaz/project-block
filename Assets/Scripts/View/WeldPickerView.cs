// PURPOSE: The modal that lets the player decide where two blocks get welded together
// ("Lehimleme"). The first block is drawn fixed; every LEGAL position of the second - touching it,
// not overlapping it - is drawn around it as a clickable ghost, and the click is the answer.
//
// WHY EVERY POSITION RATHER THAN A FREE DRAG: it is the same choice with none of the fiddling.
// The set of legal offsets is small and finite, so offering all of them gives the player exactly
// the control a drag would, and a click can never land somewhere illegal. Placeholder presentation
// like everything else under View/.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Modal picker for where the second block sits against the first.</summary>
    public sealed class WeldPickerView : MonoBehaviour
    {
        private const float Cell = 0.62f;

        private static readonly Color FirstColor = new Color(0.55f, 0.68f, 0.85f);
        private static readonly Color GhostColor = new Color(0.95f, 0.72f, 0.35f, 0.55f);

        private readonly List<GridPos> offers = new List<GridPos>();
        private readonly List<Vector2> offerCenters = new List<Vector2>();

        public bool IsOpen { get; private set; }

        /// <summary>Draws the first shape and one ghost per legal offset of the second.</summary>
        public void Show(BlockShape first, BlockShape second, string title)
        {
            Hide();
            IsOpen = true;

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.85f), 40);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, 4.4f), title,
                44, 0.055f, Color.white, 41, TextAnchor.MiddleCenter);

            var firstCells = new List<GridPos>(first.Cells);
            foreach (GridPos offset in LegalOffsets(firstCells, second))
            {
                offers.Add(offset);
            }
            if (offers.Count == 0)
            {
                return;
            }

            // One little diagram per offer, laid out in a row that wraps.
            int perRow = Mathf.Max(1, Mathf.Min(offers.Count, 6));
            float span = 5.2f;
            for (int i = 0; i < offers.Count; i++)
            {
                int row = i / perRow;
                int column = i % perRow;
                var origin = new Vector2(
                    (column - (perRow - 1) * 0.5f) * span,
                    2.4f - row * span * 0.72f);
                offerCenters.Add(origin);
                DrawOffer(origin, firstCells, second, offers[i], i);
            }
        }

        /// <summary>The offset under a world point, or null.</summary>
        public GridPos? OfferAt(Vector2 world)
        {
            for (int i = 0; i < offerCenters.Count; i++)
            {
                if (Mathf.Abs(world.x - offerCenters[i].x) <= span * 0.5f
                    && Mathf.Abs(world.y - offerCenters[i].y) <= span * 0.5f)
                {
                    return offers[i];
                }
            }
            return null;
        }

        private const float span = 2.4f;

        public void Hide()
        {
            IsOpen = false;
            offers.Clear();
            offerCenters.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private void DrawOffer(Vector2 origin, List<GridPos> firstCells, BlockShape second,
            GridPos offset, int index)
        {
            ViewUtil.MakeRect(transform, "Offer_" + index, origin,
                new Vector2(span, span), new Color(0.14f, 0.16f, 0.21f, 0.95f), 41);
            // Centre the welded picture inside its tile.
            var all = new List<GridPos>(firstCells);
            foreach (GridPos c in second.Cells)
            {
                all.Add(new GridPos(c.X + offset.X, c.Y + offset.Y));
            }
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (GridPos c in all)
            {
                if (c.X < minX) minX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.X > maxX) maxX = c.X;
                if (c.Y > maxY) maxY = c.Y;
            }
            var bottomLeft = origin - new Vector2((maxX - minX) * Cell, (maxY - minY) * Cell) * 0.5f;
            foreach (GridPos c in firstCells)
            {
                ViewUtil.MakeCell(transform, "A", bottomLeft
                    + new Vector2((c.X - minX) * Cell, (c.Y - minY) * Cell),
                    Cell * 0.88f, FirstColor, 42);
            }
            foreach (GridPos c in second.Cells)
            {
                var moved = new GridPos(c.X + offset.X, c.Y + offset.Y);
                ViewUtil.MakeCell(transform, "B", bottomLeft
                    + new Vector2((moved.X - minX) * Cell, (moved.Y - minY) * Cell),
                    Cell * 0.88f, GhostColor, 43);
            }
        }

        /// <summary>Every offset at which the second shape touches the first without overlapping
        /// it. Mirrors LehimlemePower.WeldOf exactly - the UI must never offer a join the rules
        /// would refuse.</summary>
        private static IEnumerable<GridPos> LegalOffsets(List<GridPos> firstCells,
            BlockShape second)
        {
            var seen = new HashSet<string>();
            for (int dx = -second.Width - 1; dx <= 4 + second.Width; dx++)
            {
                for (int dy = -second.Height - 1; dy <= 4 + second.Height; dy++)
                {
                    var offset = new GridPos(dx, dy);
                    if (!Joins(firstCells, second, offset))
                    {
                        continue;
                    }
                    string key = dx + "," + dy;
                    if (seen.Add(key))
                    {
                        yield return offset;
                    }
                }
            }
        }

        private static bool Joins(List<GridPos> firstCells, BlockShape second, GridPos offset)
        {
            bool touches = false;
            foreach (GridPos c in second.Cells)
            {
                var moved = new GridPos(c.X + offset.X, c.Y + offset.Y);
                foreach (GridPos f in firstCells)
                {
                    if (f.X == moved.X && f.Y == moved.Y)
                    {
                        return false; // overlap
                    }
                    if (Mathf.Abs(f.X - moved.X) + Mathf.Abs(f.Y - moved.Y) == 1)
                    {
                        touches = true;
                    }
                }
            }
            return touches;
        }
    }
}
