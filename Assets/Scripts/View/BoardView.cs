// PURPOSE: Draws the round's board as a grid of runtime sprites and shows the
// placement preview under the mouse. Pure presentation - reads GameBoard, never
// mutates it. Rebuilt whenever a round starts (board sizes differ per round).

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Debug renderer for the play grid.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        private static readonly Color BackgroundColor = new Color(0.10f, 0.11f, 0.13f);
        private static readonly Color EmptyColor = new Color(0.17f, 0.18f, 0.22f);
        private static readonly Color ValidPreviewColor = new Color(0.35f, 1f, 0.45f, 0.6f);
        private static readonly Color InvalidPreviewColor = new Color(1f, 0.35f, 0.35f, 0.6f);
        private static readonly Color ExplosionPreviewColor = new Color(1f, 0.78f, 0.25f, 0.65f);

        private GameBoard board;
        private SpriteRenderer[,] cellRenderers;
        private SpriteRenderer[,] previewRenderers;
        private readonly List<SpriteRenderer> ghostSprites = new List<SpriteRenderer>();
        private float cellSize = 1f;
        private Vector2 bottomLeft;

        /// <summary>The board currently displayed (used to detect round changes).</summary>
        public GameBoard Board
        {
            get { return board; }
        }

        /// <summary>World-space rectangle the grid covers (for effects around the arena).</summary>
        public Rect WorldRect
        {
            get
            {
                if (board == null)
                {
                    return new Rect(0f, 0f, 0f, 0f);
                }
                return new Rect(bottomLeft.x, bottomLeft.y,
                    board.Width * cellSize, board.Height * cellSize);
            }
        }

        /// <summary>Destroys and recreates the whole grid for a (new) board.</summary>
        public void Rebuild(GameBoard newBoard, float maxWorldSize, Vector2 center)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            ghostSprites.Clear();
            board = newBoard;
            cellSize = Mathf.Min(maxWorldSize / board.Width, maxWorldSize / board.Height);
            bottomLeft = center - new Vector2(board.Width, board.Height) * (cellSize * 0.5f);

            var background = new GameObject("Background");
            background.transform.SetParent(transform, false);
            background.transform.localPosition = new Vector3(center.x, center.y, 0f);
            background.transform.localScale = new Vector3(
                board.Width * cellSize + 0.15f, board.Height * cellSize + 0.15f, 1f);
            var bgRenderer = background.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = ViewUtil.WhiteSprite;
            bgRenderer.color = BackgroundColor;
            bgRenderer.sortingOrder = 0;

            cellRenderers = new SpriteRenderer[board.Width, board.Height];
            previewRenderers = new SpriteRenderer[board.Width, board.Height];
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    Vector2 pos = CellToWorld(new GridPos(x, y));
                    cellRenderers[x, y] = ViewUtil.MakeCell(
                        transform, "Cell_" + x + "_" + y, pos, cellSize * 0.92f, EmptyColor, 1);
                    previewRenderers[x, y] = ViewUtil.MakeCell(
                        transform, "Preview_" + x + "_" + y, pos, cellSize * 0.92f, ValidPreviewColor, 2);
                    previewRenderers[x, y].enabled = false;
                }
            }
            Refresh();
        }

        /// <summary>Repaints occupancy colors from the board state.</summary>
        public void Refresh()
        {
            if (board == null)
            {
                return;
            }
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    Cube? cube = board.GetCube(new GridPos(x, y));
                    cellRenderers[x, y].color = cube.HasValue
                        ? ViewUtil.CubeDisplayColor(cube.Value)
                        : EmptyColor;
                }
            }
            RefreshGhostTraces();
        }

        /// <summary>Ghost cubes hanging outside the grid render as faint traces.</summary>
        private void RefreshGhostTraces()
        {
            foreach (SpriteRenderer sprite in ghostSprites)
            {
                if (sprite != null)
                {
                    Destroy(sprite.gameObject);
                }
            }
            ghostSprites.Clear();
            foreach (KeyValuePair<GridPos, Cube> entry in board.OutsideCubes)
            {
                ghostSprites.Add(ViewUtil.MakeCell(transform, "GhostCube",
                    CellToWorld(entry.Key), cellSize * 0.86f,
                    new Color(0.8f, 0.8f, 0.95f, 0.35f), 1));
            }
        }

        /// <summary>World center of a cell.</summary>
        public Vector2 CellToWorld(GridPos cell)
        {
            return bottomLeft + new Vector2((cell.X + 0.5f) * cellSize, (cell.Y + 0.5f) * cellSize);
        }

        /// <summary>Maps a world point to a board cell; false when outside the grid.</summary>
        public bool TryWorldToCell(Vector2 world, out GridPos cell)
        {
            int x = Mathf.FloorToInt((world.x - bottomLeft.x) / cellSize);
            int y = Mathf.FloorToInt((world.y - bottomLeft.y) / cellSize);
            cell = new GridPos(x, y);
            return board != null && x >= 0 && x < board.Width && y >= 0 && y < board.Height;
        }

        /// <summary>Highlights the shape's target cells (green legal / red illegal) and, for
        /// legal placements, tints every cell of the rows/columns that would explode.</summary>
        public void ShowPreview(BlockShape shape, GridPos origin, bool valid)
        {
            ClearPreview();
            if (board == null)
            {
                return;
            }
            Color color = valid ? ValidPreviewColor : InvalidPreviewColor;
            foreach (GridPos offset in shape.Cells)
            {
                GridPos pos = origin + offset;
                if (board.IsInside(pos))
                {
                    previewRenderers[pos.X, pos.Y].color = color;
                    previewRenderers[pos.X, pos.Y].enabled = true;
                }
            }
            if (!valid)
            {
                return;
            }
            LineExplosionResult predicted = board.PredictExplosions(shape, origin);
            foreach (GridPos pos in predicted.ExplodedCells)
            {
                previewRenderers[pos.X, pos.Y].color = ExplosionPreviewColor;
                previewRenderers[pos.X, pos.Y].enabled = true;
            }
        }

        public void ClearPreview()
        {
            if (previewRenderers == null)
            {
                return;
            }
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    previewRenderers[x, y].enabled = false;
                }
            }
        }
    }
}
