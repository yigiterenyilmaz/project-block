// PURPOSE: The row/column picker the "Kentsel Dönüşüm" rescue puts on screen. Blinking
// arrows sit beside every row and above every column; the player clicks two on the SAME
// axis and the two lines trade places.
//
// Presentation only: it reports the pick and never touches the board. GameUiController
// turns the pick into ActivationTarget.LineSwap and hands it to the power.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Blinking row/column arrows for a line-swap pick.</summary>
    public sealed class LineSwapPickerView : MonoBehaviour
    {
        private const float ArrowLength = 0.52f;
        private const float ArrowThickness = 0.2f;
        private const float ArrowGap = 0.32f;
        private const int SortingOrder = 60;

        private static readonly Color IdleColor = new Color(1f, 0.85f, 0.35f, 0.95f);
        private static readonly Color PickedColor = new Color(0.45f, 1f, 0.55f, 1f);
        private static readonly Color DimColor = new Color(0.55f, 0.55f, 0.6f, 0.35f);

        private sealed class Arrow
        {
            public SpriteRenderer Renderer;
            public LineAxis Axis;
            public int Line;      // board coordinate: a row's Y, a column's X
            public Vector2 Center;
        }

        private readonly List<Arrow> arrows = new List<Arrow>();
        private BoardView boardView;
        private Action<LineAxis, int, int> onPicked;

        /// <summary>Axis of the first pick, once one has been made.</summary>
        private LineAxis? pickedAxis;
        private int pickedLine;

        public bool IsOpen { get; private set; }

        /// <summary>Opens the picker over the current board. The callback fires once two
        /// lines on the same axis have been chosen.</summary>
        public void Show(BoardView board, Action<LineAxis, int, int> picked)
        {
            Hide();
            boardView = board;
            onPicked = picked;
            IsOpen = true;
            pickedAxis = null;
            BuildArrows();
        }

        public void Hide()
        {
            for (int i = 0; i < arrows.Count; i++)
            {
                if (arrows[i].Renderer != null)
                {
                    Destroy(arrows[i].Renderer.gameObject);
                }
            }
            arrows.Clear();
            IsOpen = false;
            pickedAxis = null;
        }

        /// <summary>Feeds a world-space click in. Returns true if it landed on an arrow.</summary>
        public bool HandleClick(Vector2 world)
        {
            if (!IsOpen)
            {
                return false;
            }
            Arrow hit = ArrowAt(world);
            if (hit == null)
            {
                return false;
            }
            if (!pickedAxis.HasValue)
            {
                pickedAxis = hit.Axis;
                pickedLine = hit.Line;
                RefreshColors();
                return true;
            }
            // Second pick must be a DIFFERENT line on the SAME axis; anything else re-picks.
            if (hit.Axis != pickedAxis.Value || hit.Line == pickedLine)
            {
                pickedAxis = hit.Axis;
                pickedLine = hit.Line;
                RefreshColors();
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

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            // Blink: unpicked arrows pulse, the chosen one holds steady so it reads as locked.
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 4f));
            for (int i = 0; i < arrows.Count; i++)
            {
                Arrow arrow = arrows[i];
                if (arrow.Renderer == null)
                {
                    continue;
                }
                bool isPicked = pickedAxis.HasValue && arrow.Axis == pickedAxis.Value
                    && arrow.Line == pickedLine;
                if (isPicked)
                {
                    continue;
                }
                Color color = arrow.Renderer.color;
                color.a = (pickedAxis.HasValue && arrow.Axis != pickedAxis.Value ? 0.35f : 0.95f)
                    * pulse;
                arrow.Renderer.color = color;
            }
        }

        private void BuildArrows()
        {
            GameBoard board = boardView != null ? boardView.Board : null;
            if (board == null)
            {
                return;
            }
            // One arrow per row, to the LEFT of the grid.
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                if (!RowHasPlayableCell(board, y))
                {
                    continue;
                }
                Vector2 rowStart = boardView.CellToWorld(new GridPos(board.MinX, y));
                Vector2 at = rowStart + new Vector2(-(ArrowGap + ArrowLength * 0.5f), 0f);
                arrows.Add(MakeArrow("RowArrow_" + y, at, LineAxis.Row, y, true));
            }
            // One arrow per column, ABOVE the grid.
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                if (!ColumnHasPlayableCell(board, x))
                {
                    continue;
                }
                Vector2 colTop = boardView.CellToWorld(new GridPos(x, board.MinY + board.Height - 1));
                Vector2 at = colTop + new Vector2(0f, ArrowGap + ArrowLength * 0.5f);
                arrows.Add(MakeArrow("ColArrow_" + x, at, LineAxis.Column, x, false));
            }
            RefreshColors();
        }

        private Arrow MakeArrow(string name, Vector2 at, LineAxis axis, int line, bool horizontal)
        {
            Vector2 size = horizontal
                ? new Vector2(ArrowLength, ArrowThickness)
                : new Vector2(ArrowThickness, ArrowLength);
            SpriteRenderer renderer = ViewUtil.MakeRect(transform, name, at, size,
                IdleColor, SortingOrder);
            return new Arrow
            {
                Renderer = renderer,
                Axis = axis,
                Line = line,
                Center = at
            };
        }

        private void RefreshColors()
        {
            for (int i = 0; i < arrows.Count; i++)
            {
                Arrow arrow = arrows[i];
                if (arrow.Renderer == null)
                {
                    continue;
                }
                bool isPicked = pickedAxis.HasValue && arrow.Axis == pickedAxis.Value
                    && arrow.Line == pickedLine;
                bool wrongAxis = pickedAxis.HasValue && arrow.Axis != pickedAxis.Value;
                arrow.Renderer.color = isPicked ? PickedColor : (wrongAxis ? DimColor : IdleColor);
            }
        }

        private Arrow ArrowAt(Vector2 world)
        {
            for (int i = 0; i < arrows.Count; i++)
            {
                Arrow arrow = arrows[i];
                if (arrow.Renderer == null)
                {
                    continue;
                }
                Vector3 scale = arrow.Renderer.transform.localScale;
                // A generous hit box: these are small targets and the click is a rescue.
                if (Mathf.Abs(world.x - arrow.Center.x) <= scale.x * 0.5f + 0.12f
                    && Mathf.Abs(world.y - arrow.Center.y) <= scale.y * 0.5f + 0.12f)
                {
                    return arrow;
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
