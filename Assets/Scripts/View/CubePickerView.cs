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

        /// <summary>True while the picker is collecting a SET of cubes rather than one
        /// ("Neşter" choosing where to cut). Picked cells light up and a CUT button appears.</summary>
        public bool IsMultiPick { get; private set; }

        private static readonly Color PickedColor = new Color(0.95f, 0.72f, 0.35f);

        private readonly List<int> picked = new List<int>();
        private readonly List<SpriteRenderer> cellSprites = new List<SpriteRenderer>();
        private BlockShape shownShape;
        private Vector2 confirmCenter;
        private Vector2 confirmSize;

        /// <summary>The cubes picked so far, as SHAPE OFFSETS - what ActivationTarget.CardCubes
        /// wants.</summary>
        public List<GridPos> PickedCells
        {
            get
            {
                var cells = new List<GridPos>();
                if (shownShape == null)
                {
                    return cells;
                }
                foreach (int index in picked)
                {
                    cells.Add(shownShape.Cells[index]);
                }
                return cells;
            }
        }

        /// <summary>Opens in multi-pick mode: click cubes to toggle them, then the CUT button.
        /// </summary>
        public void ShowMulti(BlockShape shape, string title, string confirmLabel)
        {
            Show(shape, title);
            IsMultiPick = true;
            confirmCenter = new Vector2(0f, -(shape.Height * Cell * 0.5f) - 1.1f);
            confirmSize = new Vector2(3.2f, 0.8f);
            ViewUtil.MakeRect(transform, "Confirm", confirmCenter, confirmSize,
                new Color(0.24f, 0.42f, 0.30f), 41);
            ViewUtil.MakeText3D(transform, "ConfirmText", confirmCenter, confirmLabel,
                44, 0.05f, Color.white, 42, TextAnchor.MiddleCenter);
        }

        /// <summary>Toggles a cube in the picked set. No-op outside multi-pick.</summary>
        public void Toggle(int index)
        {
            if (!IsMultiPick || index < 0 || index >= cellSprites.Count)
            {
                return;
            }
            if (picked.Contains(index))
            {
                picked.Remove(index);
                cellSprites[index].color = CellColor;
                return;
            }
            picked.Add(index);
            cellSprites[index].color = PickedColor;
        }

        /// <summary>True when the point is on the confirm button.</summary>
        public bool ConfirmAt(Vector2 world)
        {
            return IsMultiPick
                && Mathf.Abs(world.x - confirmCenter.x) <= confirmSize.x * 0.5f
                && Mathf.Abs(world.y - confirmCenter.y) <= confirmSize.y * 0.5f;
        }

        public void Show(BlockShape shape, string title)
        {
            Hide();
            IsOpen = true;
            shownShape = shape;

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
                cellSprites.Add(ViewUtil.MakeCell(transform, "Cell_" + index, center,
                    Cell * 0.9f, CellColor, 41));
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
            IsMultiPick = false;
            shownShape = null;
            picked.Clear();
            cellSprites.Clear();
            cellCenters.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
