// PURPOSE: The modal block-designer for the "Karakter oluşturma" power. The player toggles
// cells on a small grid to draw any shape and picks one element (or none), then confirms.
// Like the other pickers under View/, this is a dumb renderer with hit-testing: the
// controller (GameUiController) owns the input and reads the result back through ShapeCells /
// SelectedElement, then calls GameSession.CreateDesignedBlock. No game rules live here.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Modal "draw a shape + pick an element" designer. While open, the controller
    /// blocks other input.</summary>
    public sealed class BlockDesignerView : MonoBehaviour
    {
        private const int GridSize = 5;
        private const float CellPitch = 0.95f;
        private const float CellSize = 0.82f;
        private static readonly Vector2 GridCenter = new Vector2(-3.0f, 0.7f);

        private const float PaletteX = 2.2f;
        private const float PaletteTop = 2.9f;
        private const float PalettePitch = 0.6f;
        private static readonly Vector2 SwatchSize = new Vector2(2.0f, 0.5f);

        private static readonly Vector2 ButtonSize = new Vector2(2.7f, 0.72f);
        private static readonly Vector2 ConfirmCenter = new Vector2(-1.7f, -3.2f);
        private static readonly Vector2 CancelCenter = new Vector2(1.7f, -3.2f);

        private static readonly Color FilledColor = new Color(0.45f, 0.70f, 1f);
        private static readonly Color EmptyColor = new Color(0.20f, 0.22f, 0.27f);
        private static readonly Color Ink = new Color(0.92f, 0.94f, 0.98f);
        private static readonly Color Faint = new Color(0.62f, 0.67f, 0.74f);

        // Element options: index 0 is "no element", the rest mirror the implemented block types.
        private static readonly BlockElement[] Elements =
        {
            BlockElement.Fire, BlockElement.Water, BlockElement.Obsidian, BlockElement.Gold,
            BlockElement.Transparent, BlockElement.Ghost, BlockElement.Dynamite,
            BlockElement.Mechanical, BlockElement.Fox
        };

        private readonly bool[,] filled = new bool[GridSize, GridSize];
        private int selected; // 0 = none, 1..Elements.Length = Elements[selected-1]

        private readonly List<Vector2> cellCenters = new List<Vector2>();
        private readonly List<Vector2> swatchCenters = new List<Vector2>();

        public bool IsOpen { get; private set; }

        public void Show()
        {
            Hide();
            IsOpen = true;
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    filled[x, y] = false;
                }
            }
            selected = 0;
            Rebuild();
        }

        public void Hide()
        {
            IsOpen = false;
            cellCenters.Clear();
            swatchCenters.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        /// <summary>Toggles the grid cell under a world point. Returns true if one was hit.</summary>
        public bool ToggleCellAt(Vector2 world)
        {
            for (int i = 0; i < cellCenters.Count; i++)
            {
                if (Within(world, cellCenters[i], CellSize * 0.5f, CellSize * 0.5f))
                {
                    int x = i % GridSize;
                    int y = i / GridSize;
                    filled[x, y] = !filled[x, y];
                    Rebuild();
                    return true;
                }
            }
            return false;
        }

        /// <summary>Element option index under a world point (0 = none), or -1.</summary>
        public int ElementAt(Vector2 world)
        {
            for (int i = 0; i < swatchCenters.Count; i++)
            {
                if (Within(world, swatchCenters[i], SwatchSize.x * 0.5f, SwatchSize.y * 0.5f))
                {
                    return i;
                }
            }
            return -1;
        }

        public void SelectElement(int optionIndex)
        {
            if (optionIndex < 0 || optionIndex > Elements.Length)
            {
                return;
            }
            selected = optionIndex;
            Rebuild();
        }

        /// <summary>1 = confirm, 0 = cancel, -1 = neither.</summary>
        public int ButtonAt(Vector2 world)
        {
            if (Within(world, ConfirmCenter, ButtonSize.x * 0.5f, ButtonSize.y * 0.5f))
            {
                return 1;
            }
            if (Within(world, CancelCenter, ButtonSize.x * 0.5f, ButtonSize.y * 0.5f))
            {
                return 0;
            }
            return -1;
        }

        /// <summary>The drawn shape's cells (raw grid coords; BlockShape.FromCells normalizes).
        /// Empty when nothing is drawn.</summary>
        public IReadOnlyList<GridPos> ShapeCells()
        {
            var cells = new List<GridPos>();
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    if (filled[x, y])
                    {
                        cells.Add(new GridPos(x, y));
                    }
                }
            }
            return cells;
        }

        /// <summary>The chosen element, or null for a plain block.</summary>
        public BlockElement? SelectedElement
        {
            get { return selected == 0 ? (BlockElement?)null : Elements[selected - 1]; }
        }

        private void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            cellCenters.Clear();
            swatchCenters.Clear();

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.86f), 50);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, 4.7f),
                Loc.Pick("KARAKTER OLUŞTURMA - design a block",
                    "KARAKTER OLUŞTURMA - blok tasarla"), 48, 0.05f, Color.white, 52,
                TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "Help", new Vector2(0f, 4.0f),
                Loc.Pick("click cells to draw a shape, pick an element, then Confirm",
                    "kareleri tıklayıp şekil çiz, element seç, sonra Onayla"), 44, 0.03f,
                Faint, 52, TextAnchor.MiddleCenter);

            // grid: row 0 drawn at the top
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    var center = new Vector2(GridCenter.x + (c - (GridSize - 1) * 0.5f) * CellPitch,
                        GridCenter.y + ((GridSize - 1) * 0.5f - r) * CellPitch);
                    int x = c;
                    int y = GridSize - 1 - r; // grid-space y up, matches ShapeCells indexing
                    // cellCenters is indexed x + y*GridSize to match ToggleCellAt's math
                    RegisterCell(x, y, center);
                    ViewUtil.MakeRect(transform, "Cell_" + c + "_" + r, center,
                        new Vector2(CellSize, CellSize),
                        filled[x, y] ? FilledColor : EmptyColor, 51);
                }
            }

            // element palette (index 0 = none), a vertical list on the right
            DrawSwatch(0, Loc.Pick("no element", "elementsiz"), Faint);
            for (int i = 0; i < Elements.Length; i++)
            {
                DrawSwatch(i + 1, ViewUtil.ElementLabel(Elements[i]),
                    ViewUtil.ElementColor(Elements[i]));
            }

            // buttons
            int filledCount = CountFilled();
            ViewUtil.MakeRect(transform, "Confirm", ConfirmCenter, ButtonSize,
                filledCount > 0 ? new Color(0.20f, 0.55f, 0.30f) : new Color(0.20f, 0.24f, 0.20f),
                51);
            ViewUtil.MakeText3D(transform, "ConfirmT", ConfirmCenter,
                Loc.Pick("Confirm", "Onayla"), 56, 0.04f,
                filledCount > 0 ? Color.white : Faint, 52, TextAnchor.MiddleCenter);
            ViewUtil.MakeRect(transform, "Cancel", CancelCenter, ButtonSize,
                new Color(0.45f, 0.22f, 0.22f), 51);
            ViewUtil.MakeText3D(transform, "CancelT", CancelCenter,
                Loc.Pick("Cancel", "Vazgeç"), 56, 0.04f, Ink, 52, TextAnchor.MiddleCenter);
        }

        private void RegisterCell(int x, int y, Vector2 center)
        {
            // Keep cellCenters ordered so index == x + y*GridSize (ToggleCellAt relies on it).
            while (cellCenters.Count <= x + y * GridSize)
            {
                cellCenters.Add(Vector2.zero);
            }
            cellCenters[x + y * GridSize] = center;
        }

        private void DrawSwatch(int option, string label, Color color)
        {
            var center = new Vector2(PaletteX, PaletteTop - option * PalettePitch);
            while (swatchCenters.Count <= option)
            {
                swatchCenters.Add(Vector2.zero);
            }
            swatchCenters[option] = center;
            bool on = selected == option;
            ViewUtil.MakeRect(transform, "Sw_" + option, center, SwatchSize,
                on ? color : new Color(color.r, color.g, color.b, 0.30f), 51);
            ViewUtil.MakeText3D(transform, "SwT_" + option, center, label, 52, 0.03f,
                on ? Color.black : Ink, 52, TextAnchor.MiddleCenter);
        }

        private int CountFilled()
        {
            int n = 0;
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    if (filled[x, y])
                    {
                        n++;
                    }
                }
            }
            return n;
        }

        private static bool Within(Vector2 p, Vector2 center, float halfW, float halfH)
        {
            return Mathf.Abs(p.x - center.x) <= halfW && Mathf.Abs(p.y - center.y) <= halfH;
        }
    }
}
