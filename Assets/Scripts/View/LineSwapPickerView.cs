// PURPOSE: The row/column picker "Kentsel Dönüşüm" puts on screen - in normal play and as the
// dead-end rescue. A TAB sits beside every row and above every column, each with an arrow pointing
// into the board; the player picks two on the SAME axis and the two lines trade places.
//
// What it shows is the whole question, answered before the click:
//   - HOVER a tab and its whole LINE lights across the board (a soft band with an outline), so the
//     player sees the row, not a little arrow standing next to it.
//   - The FIRST pick stays lit in a steady colour; the other axis dims, since a row cannot be
//     swapped with a column (clicking one of those simply re-picks on that axis).
//   - With one line picked, hovering a second lights it too and draws a CONNECTOR between the two
//     tabs with an arrowhead at each end - the swap, previewed.
//   - Clicking the picked tab again puts the first pick down.
// Tabs slide in on a short stagger when the picker opens, and lift a little under the pointer.
//
// Presentation only: it reports the pick and never touches the board. GameUiController turns the
// pick into ActivationTarget.LineSwap and hands it to the power. It reads the pointer itself for
// the hover (the pad's snapped pointer drives it too); clicks still come in through HandleClick.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    /// <summary>Row/column tabs, line bands and a swap preview for a line-swap pick.</summary>
    public sealed class LineSwapPickerView : MonoBehaviour
    {
        private const int Order = 60;
        private const float TabShare = 0.78f;     // a tab's side, as a share of a cell
        private const float TabGap = 0.95f;       // tab centre to the board's edge cell, in cells
        private const float EnterSeconds = 0.22f;
        private const float EnterStagger = 0.018f;

        private static readonly Color TabIdle = new Color(0.17f, 0.21f, 0.28f, 0.96f);
        private static readonly Color TabHover = new Color(0.30f, 0.26f, 0.14f, 0.98f);
        private static readonly Color TabPicked = new Color(0.14f, 0.36f, 0.46f, 0.98f);
        private static readonly Color TabDim = new Color(0.13f, 0.14f, 0.17f, 0.7f);
        private static readonly Color ArrowIdle = new Color(1f, 0.85f, 0.45f, 1f);
        private static readonly Color ArrowPicked = new Color(0.6f, 0.95f, 1f, 1f);
        private static readonly Color ArrowDim = new Color(0.5f, 0.52f, 0.58f, 0.6f);
        private static readonly Color HoverBand = new Color(1f, 0.82f, 0.42f, 1f);
        private static readonly Color PickedBand = new Color(0.45f, 0.85f, 1f, 1f);

        private sealed class Tab
        {
            public Transform Root;
            public SpriteRenderer Plate;
            public SpriteRenderer Arrow;
            public LineAxis Axis;
            public int Line;          // board coordinate: a row's Y, a column's X
            public Vector2 Center;    // world
            public Vector2 Inward;    // the direction the tab slides in from, and leans on hover
            public float Hover;
        }

        /// <summary>A line lit across the board: a faint fill and four edge bars.</summary>
        private sealed class Band
        {
            public SpriteRenderer Fill;
            public SpriteRenderer[] Edges = new SpriteRenderer[4];
        }

        private readonly List<Tab> tabs = new List<Tab>();
        private BoardView boardView;
        private Action<LineAxis, int, int> onPicked;
        private float cell = 1f;
        private float openTime;
        private Band hoverBand;
        private Band pickedBand;
        private SpriteRenderer connector;
        private SpriteRenderer connectorHeadA;
        private SpriteRenderer connectorHeadB;

        /// <summary>Axis of the first pick, once one has been made.</summary>
        private LineAxis? pickedAxis;
        private int pickedLine;

        public bool IsOpen { get; private set; }

        /// <summary>True once the first of the two lines is chosen - the HUD says so.</summary>
        public bool HasFirstPick
        {
            get { return pickedAxis.HasValue; }
        }

        /// <summary>The axis of the first pick; meaningful only while HasFirstPick.</summary>
        public LineAxis FirstPickAxis
        {
            get { return pickedAxis.HasValue ? pickedAxis.Value : LineAxis.Row; }
        }

        /// <summary>Opens the picker over the current board. The callback fires once two
        /// lines on the same axis have been chosen.</summary>
        public void Show(BoardView board, Action<LineAxis, int, int> picked)
        {
            Hide();
            boardView = board;
            onPicked = picked;
            IsOpen = true;
            pickedAxis = null;
            openTime = 0f;
            Build();
        }

        public void Hide()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            tabs.Clear();
            hoverBand = null;
            pickedBand = null;
            connector = null;
            connectorHeadA = null;
            connectorHeadB = null;
            IsOpen = false;
            pickedAxis = null;
        }

        /// <summary>The tabs on offer, and where each one is - what a gamepad steps through
        /// instead of pointing at them (see GameUiController.PadPanels.cs). Picking one is
        /// still HandleClick, handed the tab's own centre.</summary>
        public int ArrowCount
        {
            get { return tabs.Count; }
        }

        public Vector2? ArrowWorldCenter(int index)
        {
            return index >= 0 && index < tabs.Count ? tabs[index].Center : (Vector2?)null;
        }

        /// <summary>Feeds a world-space click in. Returns true if it landed on a tab.</summary>
        public bool HandleClick(Vector2 world)
        {
            if (!IsOpen)
            {
                return false;
            }
            Tab hit = TabAt(world);
            if (hit == null)
            {
                return false;
            }
            if (!pickedAxis.HasValue || hit.Axis != pickedAxis.Value)
            {
                // First pick - or a pick on the OTHER axis, which starts over on that axis.
                pickedAxis = hit.Axis;
                pickedLine = hit.Line;
                return true;
            }
            if (hit.Line == pickedLine)
            {
                pickedAxis = null; // the same tab again: put the first pick down
                return true;
            }
            LineAxis axis = pickedAxis.Value;
            int first = pickedLine;
            int second = hit.Line;
            Action<LineAxis, int, int> callback = onPicked;
            Hide();
            if (callback != null)
            {
                callback(axis, first, second);
            }
            return true;
        }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            GameBoard board = boardView != null ? boardView.Board : null;
            if (board == null)
            {
                return;
            }
            Vector2 origin = boardView.CellToWorld(new GridPos(board.MinX, board.MinY));
            cell = board.Width > 1
                ? Vector2.Distance(origin, boardView.CellToWorld(new GridPos(board.MinX + 1, board.MinY)))
                : Vector2.Distance(origin, boardView.CellToWorld(new GridPos(board.MinX, board.MinY + 1)));
            if (cell <= 0f)
            {
                cell = 1f;
            }
            // One tab per row, to the LEFT of the grid, its arrow pointing right into the row.
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                if (RowHasPlayableCell(board, y))
                {
                    Vector2 at = boardView.CellToWorld(new GridPos(board.MinX, y))
                        + new Vector2(-cell * TabGap, 0f);
                    tabs.Add(MakeTab(at, LineAxis.Row, y, new Vector2(1f, 0f)));
                }
            }
            // One tab per column, ABOVE the grid, its arrow pointing down into the column.
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                if (ColumnHasPlayableCell(board, x))
                {
                    Vector2 at = boardView.CellToWorld(new GridPos(x, board.MinY + board.Height - 1))
                        + new Vector2(0f, cell * TabGap);
                    tabs.Add(MakeTab(at, LineAxis.Column, x, new Vector2(0f, -1f)));
                }
            }
            hoverBand = MakeBand("HoverBand");
            pickedBand = MakeBand("PickedBand");
            connector = ViewUtil.MakeRounded(transform, "Connector", Vector2.zero, Vector2.one,
                HoverBand, Order + 1);
            connectorHeadA = ViewUtil.MakeIcon(transform, "ConnectorHeadA", Vector2.zero,
                cell * 0.34f, HoverBand, Order + 2, ChoicePickerView.ArrowSprite);
            connectorHeadB = ViewUtil.MakeIcon(transform, "ConnectorHeadB", Vector2.zero,
                cell * 0.34f, HoverBand, Order + 2, ChoicePickerView.ArrowSprite);
            connector.enabled = false;
            connectorHeadA.enabled = false;
            connectorHeadB.enabled = false;
        }

        private Tab MakeTab(Vector2 at, LineAxis axis, int line, Vector2 inward)
        {
            var go = new GameObject((axis == LineAxis.Row ? "RowTab_" : "ColTab_") + line);
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            float side = cell * TabShare;
            var tab = new Tab
            {
                Root = go.transform,
                Axis = axis,
                Line = line,
                Center = at,
                Inward = inward
            };
            tab.Plate = ViewUtil.MakeRounded(go.transform, "Plate", Vector2.zero,
                new Vector2(side, side), TabIdle, Order + 3);
            tab.Arrow = ViewUtil.MakeIcon(go.transform, "Arrow", Vector2.zero, side * 0.62f,
                ArrowIdle, Order + 4, ChoicePickerView.ArrowSprite);
            tab.Arrow.transform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg);
            return tab;
        }

        private Band MakeBand(string name)
        {
            var band = new Band();
            band.Fill = ViewUtil.MakeRounded(transform, name, Vector2.zero, Vector2.one,
                Color.clear, Order);
            for (int i = 0; i < 4; i++)
            {
                band.Edges[i] = ViewUtil.MakeRounded(transform, name + "Edge", Vector2.zero,
                    Vector2.one, Color.clear, Order + 1);
            }
            ShowBand(band, null, 0, Color.clear, 0f);
            return band;
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            if (!IsOpen || boardView == null || boardView.Board == null)
            {
                return;
            }
            openTime += Time.unscaledDeltaTime;
            Tab hovered = null;
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse != null && cam != null)
            {
                hovered = TabAt(cam.ScreenToWorldPoint(mouse.position.ReadValue()));
            }
            float ease = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
            float breath = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);

            for (int i = 0; i < tabs.Count; i++)
            {
                Tab tab = tabs[i];
                bool picked = pickedAxis.HasValue && tab.Axis == pickedAxis.Value
                    && tab.Line == pickedLine;
                bool dim = pickedAxis.HasValue && tab.Axis != pickedAxis.Value;
                tab.Hover = Mathf.Lerp(tab.Hover, tab == hovered ? 1f : 0f, ease);

                // THE ENTRANCE: each tab slides in from outside the board on a short stagger.
                float t = Mathf.Clamp01((openTime - i * EnterStagger) / EnterSeconds);
                float enter = 1f - (1f - t) * (1f - t);
                Vector2 offset = -tab.Inward * cell * 0.5f * (1f - enter)
                    + tab.Inward * cell * 0.08f * tab.Hover; // leans toward its line on hover
                tab.Root.position = tab.Center + offset;
                float scale = (0.85f + 0.15f * enter) * (1f + 0.1f * tab.Hover);
                tab.Root.localScale = new Vector3(scale, scale, 1f);

                Color plate = picked ? TabPicked : dim ? TabDim : Color.Lerp(TabIdle, TabHover, tab.Hover);
                Color arrow = picked ? ArrowPicked : dim ? ArrowDim : ArrowIdle;
                plate.a *= enter;
                arrow.a *= enter;
                tab.Plate.color = plate;
                tab.Arrow.color = arrow;
            }

            // THE LINES: the first pick lit steady, the hovered one breathing.
            if (pickedAxis.HasValue)
            {
                ShowBand(pickedBand, pickedAxis.Value, pickedLine, PickedBand, 1f);
            }
            else
            {
                ShowBand(pickedBand, null, 0, Color.clear, 0f);
            }
            bool hoverIsPick = hovered != null && pickedAxis.HasValue
                && hovered.Axis == pickedAxis.Value && hovered.Line == pickedLine;
            if (hovered != null && !hoverIsPick)
            {
                bool offAxis = pickedAxis.HasValue && hovered.Axis != pickedAxis.Value;
                ShowBand(hoverBand, hovered.Axis, hovered.Line, HoverBand,
                    (offAxis ? 0.5f : 1f) * breath);
            }
            else
            {
                ShowBand(hoverBand, null, 0, Color.clear, 0f);
            }

            // THE SWAP PREVIEW: a connector between the two tabs, an arrowhead at each end.
            Tab first = pickedAxis.HasValue ? FindTab(pickedAxis.Value, pickedLine) : null;
            bool swap = first != null && hovered != null && !hoverIsPick
                && hovered.Axis == first.Axis;
            connector.enabled = swap;
            connectorHeadA.enabled = swap;
            connectorHeadB.enabled = swap;
            if (swap)
            {
                ShowConnector(first, hovered, breath);
            }
        }

        /// <summary>Lights one whole row or column across the board, or hides the band.</summary>
        private void ShowBand(Band band, LineAxis? axis, int line, Color color, float strength)
        {
            if (band == null)
            {
                return;
            }
            bool on = axis.HasValue && strength > 0f;
            band.Fill.enabled = on;
            for (int i = 0; i < 4; i++)
            {
                band.Edges[i].enabled = on;
            }
            if (!on)
            {
                return;
            }
            GameBoard board = boardView.Board;
            Vector2 a;
            Vector2 b;
            if (axis.Value == LineAxis.Row)
            {
                a = boardView.CellToWorld(new GridPos(board.MinX, line));
                b = boardView.CellToWorld(new GridPos(board.MinX + board.Width - 1, line));
            }
            else
            {
                a = boardView.CellToWorld(new GridPos(line, board.MinY));
                b = boardView.CellToWorld(new GridPos(line, board.MinY + board.Height - 1));
            }
            Vector2 centre = (a + b) * 0.5f;
            Vector2 size = new Vector2(Mathf.Abs(b.x - a.x) + cell, Mathf.Abs(b.y - a.y) + cell);
            band.Fill.transform.position = centre;
            band.Fill.size = size;
            band.Fill.color = new Color(color.r, color.g, color.b, 0.16f * strength);
            float line2 = cell * 0.06f;
            Vector2 half = size * 0.5f;
            SetBar(band.Edges[0], centre + new Vector2(0f, half.y), new Vector2(size.x + line2, line2));
            SetBar(band.Edges[1], centre - new Vector2(0f, half.y), new Vector2(size.x + line2, line2));
            SetBar(band.Edges[2], centre + new Vector2(half.x, 0f), new Vector2(line2, size.y + line2));
            SetBar(band.Edges[3], centre - new Vector2(half.x, 0f), new Vector2(line2, size.y + line2));
            Color edge = new Color(color.r, color.g, color.b, 0.95f * strength);
            for (int i = 0; i < 4; i++)
            {
                band.Edges[i].color = edge;
            }
        }

        private static void SetBar(SpriteRenderer bar, Vector2 at, Vector2 size)
        {
            bar.transform.position = at;
            bar.size = size;
        }

        /// <summary>The two tabs joined along their strip, an arrowhead pointing at each.</summary>
        private void ShowConnector(Tab a, Tab b, float breath)
        {
            // Rows' tabs stand in a column, so the connector runs vertically a little outside
            // them; columns' tabs stand in a row, so it runs horizontally above them.
            bool rows = a.Axis == LineAxis.Row;
            Vector2 away = rows ? new Vector2(-cell * 0.62f, 0f) : new Vector2(0f, cell * 0.62f);
            Vector2 from = a.Center + away;
            Vector2 to = b.Center + away;
            float thick = cell * 0.08f;
            Color c = HoverBand;
            c.a = 0.9f * breath;
            connector.transform.position = (from + to) * 0.5f;
            connector.size = rows
                ? new Vector2(thick, Mathf.Abs(to.y - from.y) + thick)
                : new Vector2(Mathf.Abs(to.x - from.x) + thick, thick);
            connector.color = c;
            // Arrowheads at both ends, pointing back toward their own tabs.
            PlaceHead(connectorHeadA, from, -away, c);
            PlaceHead(connectorHeadB, to, -away, c);
        }

        private static void PlaceHead(SpriteRenderer head, Vector2 at, Vector2 pointing, Color c)
        {
            head.transform.position = at;
            head.transform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(pointing.y, pointing.x) * Mathf.Rad2Deg);
            head.color = c;
        }

        // ------------------------------------------------------------------ queries

        private Tab FindTab(LineAxis axis, int line)
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].Axis == axis && tabs[i].Line == line)
                {
                    return tabs[i];
                }
            }
            return null;
        }

        private Tab TabAt(Vector2 world)
        {
            // A generous hit box - the tabs are the whole target, and the click can be a rescue.
            float half = cell * TabShare * 0.5f + cell * 0.08f;
            for (int i = 0; i < tabs.Count; i++)
            {
                if (Mathf.Abs(world.x - tabs[i].Center.x) <= half
                    && Mathf.Abs(world.y - tabs[i].Center.y) <= half)
                {
                    return tabs[i];
                }
            }
            return null;
        }

        private static bool RowHasPlayableCell(GameBoard board, int y)
        {
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                if (board.IsInside(new GridPos(x, y)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ColumnHasPlayableCell(GameBoard board, int x)
        {
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                if (board.IsInside(new GridPos(x, y)))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
