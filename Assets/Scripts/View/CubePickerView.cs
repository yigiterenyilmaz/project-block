// PURPOSE: A modal that draws one block's shape as numbered, clickable cells so the player
// can point at a specific cube of it ("Parazit" choosing the host cube). The clicked index
// matches BlockShape.Cells order, which is exactly what GameSession.TryAttachJokerToCard
// expects as the cell index. Placeholder presentation like everything else under View/.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Modal cube picker for a single block shape.</summary>
    public sealed class CubePickerView : MonoBehaviour
    {
        private const float Cell = 0.95f;

        private static readonly Color CellColor = new Color(0.55f, 0.68f, 0.85f);

        private readonly List<Vector2> cellCenters = new List<Vector2>();

        public bool IsOpen { get; private set; }

        public void Show(BlockShape shape, string title)
        {
            Hide();
            IsOpen = true;

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.82f), 40);
            ViewUtil.MakeText3D(transform, "Title",
                new Vector2(0f, shape.Height * Cell * 0.5f + 1.0f), title,
                48, 0.06f, Color.white, 41, TextAnchor.MiddleCenter);

            Vector2 bottomLeft = new Vector2(
                -(shape.Width - 1) * Cell * 0.5f,
                -(shape.Height - 1) * Cell * 0.5f);
            int index = 0;
            foreach (GridPos c in shape.Cells)
            {
                var center = bottomLeft + new Vector2(c.X * Cell, c.Y * Cell);
                cellCenters.Add(center);
                ViewUtil.MakeCell(transform, "Cell_" + index, center, Cell * 0.9f, CellColor, 41);
                ViewUtil.MakeText3D(transform, "Num_" + index, center, (index + 1).ToString(),
                    60, 0.05f, Color.black, 42, TextAnchor.MiddleCenter);
                index++;
            }
        }

        /// <summary>Cell index (into the shape's cell order) under a world point, or -1.</summary>
        public int CellAt(Vector2 world)
        {
            for (int i = 0; i < cellCenters.Count; i++)
            {
                if (Mathf.Abs(world.x - cellCenters[i].x) <= Cell * 0.5f
                    && Mathf.Abs(world.y - cellCenters[i].y) <= Cell * 0.5f)
                {
                    return i;
                }
            }
            return -1;
        }

        public void Hide()
        {
            IsOpen = false;
            cellCenters.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
