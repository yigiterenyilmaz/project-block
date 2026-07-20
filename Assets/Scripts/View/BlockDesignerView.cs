// PURPOSE: The modal block-designer for the "Karakter oluşturma" power. The player picks an
// element as a BRUSH and paints cubes on a small grid - each cube can carry its own element
// (or none), so a block may mix types. Like the other pickers under View/, this is a dumb
// renderer with hit-testing: the controller (GameUiController) owns the input and reads the
// result back through ShapeCells / CellElements, then calls GameSession.CreateDesignedBlock.
// No game rules live here.

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
        // Ghost, Mechanical, Dynamite (TNT) and Fox are intentionally excluded - a hang-off, a
        // rotating gear, a whole-block-detonation, or a shape-shifter make no sense for a piece
        // the player draws by hand.
        private static readonly BlockElement[] Elements =
        {
            BlockElement.Fire, BlockElement.Water, BlockElement.Obsidian, BlockElement.Gold,
            BlockElement.Transparent
        };

        /// <summary>Most cubes a designed block may have. Balance placeholder - keeps custom
        /// blocks in the same size range as normal ones.</summary>
        private const int MaxCubes = 5;

        private readonly bool[,] filled = new bool[GridSize, GridSize];
        // The element brush stamped on each filled cell: 0 = plain (no element),
        // 1..Elements.Length = Elements[brush-1]. Only meaningful where filled is true.
        private readonly int[,] cellBrush = new int[GridSize, GridSize];
        private int selected; // the ACTIVE brush: 0 = plain, 1..Elements.Length = Elements[selected-1]
        private string warning = ""; // shown when Confirm is rejected (e.g. a disconnected shape)

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
                    cellBrush[x, y] = 0;
                }
            }
            selected = 0;
            warning = "";
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

        /// <summary>The grid cell index (x + y*GridSize) under a world point, or -1.</summary>
        public int CellIndexAt(Vector2 world)
        {
            for (int i = 0; i < cellCenters.Count; i++)
            {
                if (Within(world, cellCenters[i], CellSize * 0.5f, CellSize * 0.5f))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Whether the given cell (from CellIndexAt) is currently filled.</summary>
        public bool IsCellFilled(int cellIndex)
        {
            if (cellIndex < 0)
            {
                return false;
            }
            return filled[cellIndex % GridSize, cellIndex / GridSize];
        }

        /// <summary>fill=true paints the ACTIVE brush onto a cell: fills it if empty, or recolours
        /// it in place if already filled. fill=false erases it from the shape. Rebuilds only on an
        /// actual change, so a drag lingering on one cell does not thrash the modal, and a change
        /// clears any standing Confirm warning.</summary>
        public void SetCell(int cellIndex, bool fill)
        {
            if (cellIndex < 0)
            {
                return;
            }
            int x = cellIndex % GridSize;
            int y = cellIndex / GridSize;
            if (!fill)
            {
                if (!filled[x, y])
                {
                    return;
                }
                filled[x, y] = false;
                warning = "";
                Rebuild();
                return;
            }
            if (filled[x, y])
            {
                if (cellBrush[x, y] == selected)
                {
                    return; // already this brush - nothing to do
                }
                cellBrush[x, y] = selected; // recolour in place, no cube-count change
                warning = "";
                Rebuild();
                return;
            }
            if (CountFilled() >= MaxCubes)
            {
                warning = Loc.Pick("max " + MaxCubes + " cubes", "en fazla " + MaxCubes + " küp");
                Rebuild();
                return;
            }
            filled[x, y] = true;
            cellBrush[x, y] = selected;
            warning = "";
            Rebuild();
        }

        /// <summary>True if the filled cells form exactly ONE 4-connected group (a real block).
        /// Empty draws are not connected. Used to reject scattered shapes on Confirm.</summary>
        public bool IsSingleConnectedPiece()
        {
            int total = CountFilled();
            if (total == 0)
            {
                return false;
            }
            // Flood fill from the first filled cell; every filled cell must be reachable.
            var visited = new bool[GridSize, GridSize];
            var stack = new Stack<GridPos>();
            for (int x = 0; x < GridSize && stack.Count == 0; x++)
            {
                for (int y = 0; y < GridSize && stack.Count == 0; y++)
                {
                    if (filled[x, y])
                    {
                        stack.Push(new GridPos(x, y));
                        visited[x, y] = true;
                    }
                }
            }
            int reached = 0;
            while (stack.Count > 0)
            {
                GridPos p = stack.Pop();
                reached++;
                foreach (GridPos d in Neighbors)
                {
                    int nx = p.X + d.X;
                    int ny = p.Y + d.Y;
                    if (nx >= 0 && nx < GridSize && ny >= 0 && ny < GridSize
                        && filled[nx, ny] && !visited[nx, ny])
                    {
                        visited[nx, ny] = true;
                        stack.Push(new GridPos(nx, ny));
                    }
                }
            }
            return reached == total;
        }

        /// <summary>Shows a warning line under the help text (Confirm rejected). Cleared on the
        /// next cell edit.</summary>
        public void SetWarning(string message)
        {
            warning = message ?? "";
            Rebuild();
        }

        private static readonly GridPos[] Neighbors =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1)
        };

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

        /// <summary>The element chosen for each drawn cell, index-parallel to ShapeCells()
        /// (a null entry is a plain cube). Same iteration order as ShapeCells so the two align.</summary>
        public IReadOnlyList<BlockElement?> CellElements()
        {
            var list = new List<BlockElement?>();
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    if (filled[x, y])
                    {
                        int brush = cellBrush[x, y];
                        list.Add(brush == 0 ? (BlockElement?)null : Elements[brush - 1]);
                    }
                }
            }
            return list;
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
                Loc.Pick("pick an element as your brush, left-drag to paint cubes (right-drag erases), then Confirm",
                    "fırça olarak bir element seç, sol-sürükle küp boya (sağ-sürükle siler), sonra Onayla"), 40,
                0.028f, Faint, 52, TextAnchor.MiddleCenter);
            if (warning.Length > 0)
            {
                ViewUtil.MakeText3D(transform, "Warn", new Vector2(0f, 3.5f), warning, 48, 0.032f,
                    new Color(1f, 0.5f, 0.42f), 52, TextAnchor.MiddleCenter);
            }

            // grid: row 0 drawn at the top. Each filled cell shows ITS OWN brush colour so a
            // mixed-element block previews cube-by-cube; a plain cube keeps the neutral fill.
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    var center = new Vector2(GridCenter.x + (c - (GridSize - 1) * 0.5f) * CellPitch,
                        GridCenter.y + ((GridSize - 1) * 0.5f - r) * CellPitch);
                    int x = c;
                    int y = GridSize - 1 - r; // grid-space y up, matches ShapeCells indexing
                    // cellCenters is indexed x + y*GridSize to match CellIndexAt's math
                    RegisterCell(x, y, center);
                    Color cellColor = EmptyColor;
                    if (filled[x, y])
                    {
                        int brush = cellBrush[x, y];
                        cellColor = brush == 0 ? FilledColor : ViewUtil.ElementColor(Elements[brush - 1]);
                    }
                    ViewUtil.MakeRect(transform, "Cell_" + c + "_" + r, center,
                        new Vector2(CellSize, CellSize), cellColor, 51);
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
