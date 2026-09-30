// PURPOSE: The modal block-designer for the "Karakter oluşturma" power. The player picks an
// element as a BRUSH and paints cubes on a small grid - each cube can carry its own element
// (or none), so a block may mix types. Like the other pickers under View/, this is a dumb
// renderer with hit-testing: the controller (GameUiController) owns the input and reads the
// result back through ShapeCells / CellElements, then calls GameSession.CreateDesignedBlock.
// No game rules live here.
//
// THE LOOK (rebuilt): one menu plate like the weld and cut editors. The grid shows real CUBE
// TILES, not flat colours; a ghost of the active brush follows the pointer; the drawn shape wears
// an outline - cyan while it is one piece, red while it is not - so the "must be connected" rule is
// visible before Confirm refuses it. The palette is a column of brush cards, each with its tile, its
// name and one line on what the element DOES. A preview shows the finished block as a card would,
// a status line counts cubes, and CONFIRM only lights when the block is legal. CLEAR empties the grid.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    /// <summary>Modal "draw a shape + pick an element" designer. While open, the controller
    /// blocks other input.</summary>
    public sealed class BlockDesignerView : MonoBehaviour
    {
        private const int GridSize = 5;
        private const float CellPitch = 0.86f;
        private const float CellSize = 0.8f;
        private static readonly Vector2 GridCenter = new Vector2(-2.5f, 0.15f);

        private const float PaletteX = 2.55f;
        private const float PaletteTop = 2.35f;
        private const float PalettePitch = 0.68f;
        private static readonly Vector2 SwatchSize = new Vector2(3.9f, 0.6f);

        private static readonly Vector2 PreviewCenter = new Vector2(2.55f, -2.25f);
        private const float PreviewCube = 0.26f;

        private static readonly Vector2 ButtonSize = new Vector2(2.4f, 0.62f);
        private static readonly Vector2 ConfirmCenter = new Vector2(-3.0f, -3.55f);
        private static readonly Vector2 ClearCenter = new Vector2(0f, -3.55f);
        private static readonly Vector2 CancelCenter = new Vector2(3.0f, -3.55f);

        private static readonly Color SlotColor = new Color(0.17f, 0.20f, 0.26f, 1f);
        private static readonly Color PlainTint = new Color(0.62f, 0.72f, 0.86f, 1f);
        private static readonly Color Ink = new Color(0.92f, 0.94f, 0.98f);
        private static readonly Color Faint = new Color(0.62f, 0.67f, 0.74f);
        private static readonly Color Good = new Color(0.45f, 0.9f, 1f, 1f);
        private static readonly Color Bad = new Color(1f, 0.45f, 0.4f, 1f);
        private static readonly Color SwatchColor = new Color(0.16f, 0.19f, 0.25f, 1f);
        private static readonly Color SwatchOn = new Color(0.14f, 0.34f, 0.44f, 1f);

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
        private readonly List<Transform> swatchRoots = new List<Transform>();
        private readonly List<float> swatchHover = new List<float>();
        private SpriteRenderer ghost;
        private SpriteRenderer ghostSlot;
        private bool fitted;

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
            fitted = false;
            Rebuild();
        }

        public void Hide()
        {
            IsOpen = false;
            cellCenters.Clear();
            swatchCenters.Clear();
            swatchRoots.Clear();
            swatchHover.Clear();
            ghost = null;
            ghostSlot = null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        /// <summary>The grid cell index (x + y*GridSize) under a world point, or -1.</summary>
        public int CellIndexAt(Vector2 world)
        {
            Vector2 local = ToLocal(world);
            for (int i = 0; i < cellCenters.Count; i++)
            {
                if (Within(local, cellCenters[i], CellPitch * 0.5f, CellPitch * 0.5f))
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

        /// <summary>Empties the whole grid (the CLEAR button). The brush stays chosen.</summary>
        public void ClearAll()
        {
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    filled[x, y] = false;
                    cellBrush[x, y] = 0;
                }
            }
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

        /// <summary>Shows a warning line under the status (Confirm rejected). Cleared on the next
        /// cell edit.</summary>
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
            Vector2 local = ToLocal(world);
            for (int i = 0; i < swatchCenters.Count; i++)
            {
                if (Within(local, swatchCenters[i], SwatchSize.x * 0.5f, SwatchSize.y * 0.5f))
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

        /// <summary>1 = confirm, 0 = cancel, 2 = clear, -1 = none.</summary>
        public int ButtonAt(Vector2 world)
        {
            Vector2 local = ToLocal(world);
            if (Within(local, ConfirmCenter, ButtonSize.x * 0.5f, ButtonSize.y * 0.5f))
            {
                return 1;
            }
            if (Within(local, CancelCenter, ButtonSize.x * 0.5f, ButtonSize.y * 0.5f))
            {
                return 0;
            }
            if (Within(local, ClearCenter, ButtonSize.x * 0.5f, ButtonSize.y * 0.5f))
            {
                return 2;
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

        // ------------------------------------------------------------------ drawing

        private void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            cellCenters.Clear();
            swatchCenters.Clear();
            swatchRoots.Clear();
            swatchHover.Clear();

            int count = CountFilled();
            bool connected = IsSingleConnectedPiece();

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.86f), ViewUtil.MenuDimOrder);
            ViewUtil.MakeMenuPlate(transform, new Vector2(0f, 0.15f), new Vector2(11.2f, 8.9f));
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, 4.0f),
                Loc.Pick("KARAKTER OLUŞTURMA", "KARAKTER OLUŞTURMA"), 60, 0.05f, Color.white, 44,
                TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "Help", new Vector2(0f, 3.5f),
                Loc.Pick("pick a brush, drag on the grid to paint cubes - right-drag erases",
                    "bir fırça seç, ızgarada sürükleyerek küp boya - sağ sürükleme siler"),
                60, 0.03f, Faint, 44, TextAnchor.MiddleCenter);

            // THE GRID: a darker well of rounded slots; a drawn cube is its element's REAL tile.
            float wellSide = GridSize * CellPitch + 0.3f;
            ViewUtil.MakeRounded(transform, "Well", GridCenter, new Vector2(wellSide, wellSide),
                new Color(0.09f, 0.10f, 0.13f, 1f), 41);
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    int x = c;
                    int y = GridSize - 1 - r; // grid-space y up, matches ShapeCells indexing
                    Vector2 center = CellCentre(x, y);
                    RegisterCell(x, y, center);
                    ViewUtil.MakeRounded(transform, "Slot", center, new Vector2(CellSize, CellSize),
                        SlotColor, 42);
                    if (filled[x, y])
                    {
                        DrawCube(transform, center, CellSize * 0.96f, cellBrush[x, y], 1f, 44);
                    }
                }
            }
            if (count > 0)
            {
                OutlineShape(connected ? Good : Bad);
            }

            // THE STATUS under the grid: how many cubes, and whether it is one piece.
            string status = count == 0
                ? Loc.Pick("empty - paint up to " + MaxCubes + " cubes",
                    "boş - en fazla " + MaxCubes + " küp boya")
                : count + "/" + MaxCubes + Loc.Pick(" cubes  -  ", " küp  -  ")
                    + (connected ? Loc.Pick("one piece", "tek parça")
                        : Loc.Pick("cubes must touch side by side", "küpler kenardan değmeli"));
            float statusY = GridCenter.y - wellSide * 0.5f - 0.3f;
            ViewUtil.MakeText3D(transform, "Status", new Vector2(GridCenter.x, statusY), status,
                60, 0.03f, count == 0 || connected ? Ink : Bad, 44, TextAnchor.MiddleCenter);
            if (warning.Length > 0)
            {
                ViewUtil.MakeText3D(transform, "Warn", new Vector2(GridCenter.x, statusY - 0.38f),
                    warning, 60, 0.03f, Bad, 44, TextAnchor.MiddleCenter);
            }

            // THE PALETTE: brush cards with the tile, the name and what the element does.
            ViewUtil.MakeText3D(transform, "BrushLabel", new Vector2(PaletteX, PaletteTop + 0.52f),
                Loc.Pick("BRUSH", "FIRÇA"), 60, 0.03f, Faint, 44, TextAnchor.MiddleCenter);
            DrawSwatch(0, Loc.Pick("Plain", "Sade"), Loc.Pick("an ordinary cube", "sıradan küp"));
            for (int i = 0; i < Elements.Length; i++)
            {
                DrawSwatch(i + 1, ViewUtil.ElementLabel(Elements[i]), ElementLine(Elements[i]));
            }

            // THE PREVIEW: the finished block, normalised, the way a card would show it.
            ViewUtil.MakeRounded(transform, "PreviewWell", PreviewCenter, new Vector2(3.9f, 1.5f),
                new Color(0.09f, 0.10f, 0.13f, 1f), 41);
            ViewUtil.MakeText3D(transform, "PreviewLabel", PreviewCenter + new Vector2(-1.4f, 0.5f),
                Loc.Pick("PREVIEW", "ÖNİZLEME"), 60, 0.024f, Faint, 44, TextAnchor.MiddleCenter);
            DrawPreview();

            // THE BUTTONS: CONFIRM lights only when the block is legal.
            bool ok = count > 0 && connected;
            ViewUtil.MakeRounded(transform, "Confirm", ConfirmCenter, ButtonSize,
                ok ? new Color(0.18f, 0.46f, 0.28f) : new Color(0.14f, 0.16f, 0.15f), 43);
            ViewUtil.MakeText3D(transform, "ConfirmT", ConfirmCenter, Loc.Pick("CONFIRM", "ONAYLA"),
                60, 0.04f, ok ? Color.white : Faint, 44, TextAnchor.MiddleCenter);
            ViewUtil.MakeRounded(transform, "Clear", ClearCenter, ButtonSize,
                new Color(0.20f, 0.22f, 0.28f), 43);
            ViewUtil.MakeText3D(transform, "ClearT", ClearCenter, Loc.Pick("CLEAR", "TEMİZLE"),
                60, 0.04f, count > 0 ? Ink : Faint, 44, TextAnchor.MiddleCenter);
            ViewUtil.MakeRounded(transform, "Cancel", CancelCenter, ButtonSize,
                new Color(0.26f, 0.16f, 0.18f), 43);
            ViewUtil.MakeText3D(transform, "CancelT", CancelCenter, Loc.Pick("CANCEL", "VAZGEÇ"),
                60, 0.04f, Ink, 44, TextAnchor.MiddleCenter);

            // THE GHOST that follows the pointer: the active brush, faint, over a lit slot.
            ghostSlot = ViewUtil.MakeRounded(transform, "GhostSlot", Vector2.zero,
                new Vector2(CellSize, CellSize), new Color(1f, 1f, 1f, 0.12f), 43);
            ghostSlot.enabled = false;
            ghost = DrawCube(transform, Vector2.zero, CellSize * 0.96f, selected, 0.45f, 45);
            ghost.enabled = false;

            if (!fitted)
            {
                // Fitted once per opening: a rebuild must not move the modal under the pointer.
                ViewUtil.FitOverlay(transform, new Vector2(11.4f, 9.1f), new Vector2(0f, 0.15f));
                fitted = true;
            }
        }

        private void Update()
        {
            if (!IsOpen || ghost == null)
            {
                return;
            }
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null)
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            int cell = CellIndexAt(world);
            bool show = cell >= 0 && !IsCellFilled(cell) && CountFilled() < MaxCubes;
            ghost.enabled = show;
            ghostSlot.enabled = cell >= 0;
            if (cell >= 0)
            {
                Vector2 at = cellCenters[cell];
                ghost.transform.localPosition = at;
                ghostSlot.transform.localPosition = at;
            }
            int hovered = ElementAt(world);
            float ease = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
            for (int i = 0; i < swatchRoots.Count; i++)
            {
                swatchHover[i] = Mathf.Lerp(swatchHover[i], i == hovered ? 1f : 0f, ease);
                float s = 1f + 0.04f * swatchHover[i];
                swatchRoots[i].localScale = new Vector3(s, s, 1f);
            }
        }

        private SpriteRenderer DrawCube(Transform parent, Vector2 at, float size, int brush,
            float alpha, int order)
        {
            Sprite tile = brush == 0 ? ViewUtil.DefaultTile : ViewUtil.CubeTile(Elements[brush - 1]);
            Color flat = brush == 0 ? PlainTint : ViewUtil.ElementColor(Elements[brush - 1]);
            Color tint = ViewUtil.CubeTileColor(tile, flat);
            tint.a *= alpha;
            SpriteRenderer cube = ViewUtil.MakeCell(parent, "Cube", at, size, tint, order);
            ViewUtil.ApplyTile(cube, tile, size);
            return cube;
        }

        /// <summary>The drawn shape's silhouette: a bar on every outer edge of it.</summary>
        private void OutlineShape(Color color)
        {
            float line = CellPitch * 0.06f;
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    if (!filled[x, y])
                    {
                        continue;
                    }
                    Vector2 c = CellCentre(x, y);
                    foreach (GridPos d in Neighbors)
                    {
                        int nx = x + d.X;
                        int ny = y + d.Y;
                        if (nx >= 0 && nx < GridSize && ny >= 0 && ny < GridSize && filled[nx, ny])
                        {
                            continue;
                        }
                        Vector2 at = c + new Vector2(d.X, d.Y) * (CellPitch * 0.5f);
                        ViewUtil.MakeRounded(transform, "Edge", at, d.Y != 0
                            ? new Vector2(CellPitch + line, line) : new Vector2(line, CellPitch + line),
                            color, 46);
                    }
                }
            }
        }

        private void DrawSwatch(int option, string label, string line)
        {
            var center = new Vector2(PaletteX, PaletteTop - option * PalettePitch);
            while (swatchCenters.Count <= option)
            {
                swatchCenters.Add(Vector2.zero);
            }
            swatchCenters[option] = center;
            var go = new GameObject("Brush_" + option);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            swatchRoots.Add(go.transform);
            swatchHover.Add(0f);
            bool on = selected == option;
            if (on)
            {
                ViewUtil.MakeRounded(go.transform, "Rim", Vector2.zero,
                    SwatchSize + new Vector2(0.08f, 0.08f), Good, 41);
            }
            ViewUtil.MakeRounded(go.transform, "Plate", Vector2.zero, SwatchSize,
                on ? SwatchOn : SwatchColor, 42);
            DrawCube(go.transform, new Vector2(-SwatchSize.x * 0.5f + 0.34f, 0f), 0.44f, option, 1f, 44);
            ViewUtil.MakeText3D(go.transform, "Name", new Vector2(-SwatchSize.x * 0.5f + 0.7f, 0.1f),
                label, 60, 0.032f, on ? Color.white : Ink, 44, TextAnchor.MiddleLeft);
            ViewUtil.MakeText3D(go.transform, "Line", new Vector2(-SwatchSize.x * 0.5f + 0.7f, -0.14f),
                line, 60, 0.022f, Faint, 44, TextAnchor.MiddleLeft);
        }

        /// <summary>One line on what an element does, for its brush card.</summary>
        private static string ElementLine(BlockElement element)
        {
            switch (element)
            {
                case BlockElement.Fire:
                    return Loc.Pick("one cube breaks - the whole block goes", "bir küp kırılınca tüm blok patlar");
                case BlockElement.Water:
                    return Loc.Pick("falls and settles every turn", "her tur düşer ve yerleşir");
                case BlockElement.Obsidian:
                    return Loc.Pick("unbreakable, pays in every line", "kırılmaz, geçtiği her satırda puan");
                case BlockElement.Gold:
                    return Loc.Pick("unbreakable, pays every turn, locks its lines",
                        "kırılmaz, her tur puan verir, satırını ve sütununu kilitler");
                case BlockElement.Transparent:
                    return Loc.Pick("other blocks can go on top", "üstüne başka blok konabilir");
                default:
                    return "";
            }
        }

        private void DrawPreview()
        {
            IReadOnlyList<GridPos> cells = ShapeCells();
            if (cells.Count == 0)
            {
                ViewUtil.MakeText3D(transform, "PreviewNone", PreviewCenter, "-", 60, 0.05f, Faint,
                    44, TextAnchor.MiddleCenter);
                return;
            }
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            foreach (GridPos c in cells)
            {
                minX = Mathf.Min(minX, c.X);
                minY = Mathf.Min(minY, c.Y);
                maxX = Mathf.Max(maxX, c.X);
                maxY = Mathf.Max(maxY, c.Y);
            }
            float w = maxX - minX + 1;
            float h = maxY - minY + 1;
            Vector2 middle = PreviewCenter + new Vector2(0.35f, -0.05f);
            foreach (GridPos c in cells)
            {
                var at = middle + new Vector2((c.X - minX - (w - 1) * 0.5f) * PreviewCube,
                    (c.Y - minY - (h - 1) * 0.5f) * PreviewCube);
                DrawCube(transform, at, PreviewCube * 0.94f, cellBrush[c.X, c.Y], 1f, 44);
            }
        }

        // ------------------------------------------------------------------ helpers

        private Vector2 CellCentre(int x, int y)
        {
            int r = GridSize - 1 - y;
            return new Vector2(GridCenter.x + (x - (GridSize - 1) * 0.5f) * CellPitch,
                GridCenter.y + ((GridSize - 1) * 0.5f - r) * CellPitch);
        }

        private void RegisterCell(int x, int y, Vector2 center)
        {
            // Keep cellCenters ordered so index == x + y*GridSize (CellIndexAt relies on it).
            while (cellCenters.Count <= x + y * GridSize)
            {
                cellCenters.Add(Vector2.zero);
            }
            cellCenters[x + y * GridSize] = center;
        }

        private Vector2 ToLocal(Vector2 world)
        {
            return transform.InverseTransformPoint(world);
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
