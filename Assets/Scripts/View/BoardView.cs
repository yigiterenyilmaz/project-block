// PURPOSE: Draws the round's board as a grid of runtime sprites and shows the
// placement preview under the mouse. Pure presentation - reads GameBoard, never
// mutates it. Rebuilt whenever a round starts (board sizes differ per round).

using System.Collections;
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
        private CubeKind?[,] kindCache;
        private Color[,] baseColorCache;
        private readonly List<SpriteRenderer> ghostSprites = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> outsidePreviewSprites = new List<SpriteRenderer>();
        private ParticleSystem ambient;
        private float ambientTimer;
        private bool animatingWater;

        private void Awake()
        {
            // ambient element particles (embers, drips, sparkles) - lives on this GO,
            // so board rebuilds (which destroy children) leave it alone
            ambient = gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ambient.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.maxParticles = 500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ambient.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ambient.shape;
            shape.enabled = false;
            ParticleSystem.SizeOverLifetimeModule sizeModule = ambient.sizeOverLifetime;
            sizeModule.enabled = true;
            sizeModule.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
            var ambientRenderer = GetComponent<ParticleSystemRenderer>();
            ambientRenderer.material = new Material(Shader.Find("Sprites/Default"));
            ambientRenderer.sortingOrder = 3;
        }

        private void Update()
        {
            if (board == null || kindCache == null)
            {
                return;
            }
            if (!animatingWater)
            {
                AnimateElementCubes(); // would fight the fall animation's cell painting
            }
            ambientTimer += Time.deltaTime;
            while (ambientTimer >= 0.12f)
            {
                ambientTimer -= 0.12f;
                EmitAmbientParticle();
            }
            float ghostAlpha = 0.28f + 0.1f * Mathf.Sin(Time.time * 2.5f);
            foreach (SpriteRenderer sprite in ghostSprites)
            {
                if (sprite != null)
                {
                    Color color = sprite.color;
                    color.a = ghostAlpha;
                    sprite.color = color;
                }
            }
        }

        /// <summary>Element cubes get simple idle animations: fire flickers, water waves,
        /// gold shimmers, dynamite blinks, transparent breathes.</summary>
        private void AnimateElementCubes()
        {
            float time = Time.time;
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    CubeKind? kind = kindCache[x, y];
                    if (!kind.HasValue || kind.Value == CubeKind.Normal)
                    {
                        continue;
                    }
                    Color baseColor = baseColorCache[x, y];
                    SpriteRenderer cell = cellRenderers[x, y];
                    switch (kind.Value)
                    {
                        case CubeKind.Fire:
                            cell.color = Color.Lerp(baseColor, new Color(1f, 0.85f, 0.3f),
                                0.25f + 0.25f * Mathf.Sin(time * 6f + x * 1.3f + y * 2.1f));
                            break;
                        case CubeKind.Water:
                            cell.color = Color.Lerp(baseColor, new Color(0.2f, 0.42f, 0.9f),
                                0.3f + 0.3f * Mathf.Sin(time * 2.2f + x * 0.9f));
                            break;
                        case CubeKind.Gold:
                            cell.color = Color.Lerp(baseColor, Color.white,
                                0.15f + 0.15f * Mathf.Sin(time * 3f + x + y));
                            break;
                        case CubeKind.Dynamite:
                            cell.color = Color.Lerp(baseColor, new Color(1f, 0.9f, 0.85f),
                                0.25f + 0.25f * Mathf.Sin(time * 2.4f));
                            break;
                        case CubeKind.Transparent:
                            Color transparent = baseColor;
                            transparent.a = 0.65f + 0.2f * Mathf.Sin(time * 2f + x);
                            cell.color = transparent;
                            break;
                        case CubeKind.Mine:
                            // armed trap: urgent red blink
                            cell.color = Color.Lerp(baseColor, new Color(1f, 0.25f, 0.2f),
                                0.3f + 0.3f * Mathf.Sin(time * 5f));
                            break;
                        case CubeKind.Ice:
                            cell.color = Color.Lerp(baseColor, Color.white,
                                0.15f + 0.15f * Mathf.Sin(time * 1.6f + x * 0.7f + y));
                            break;
                        case CubeKind.Void:
                            cell.color = Color.Lerp(baseColor, Color.black,
                                0.3f + 0.3f * Mathf.Sin(time * 1.2f));
                            break;
                    }
                }
            }
        }

        private void EmitAmbientParticle()
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int x = Random.Range(0, board.Width);
                int y = Random.Range(0, board.Height);
                CubeKind? kind = kindCache[x, y];
                if (!kind.HasValue)
                {
                    continue;
                }
                Vector2 world = CellToWorld(new GridPos(board.MinX + x, board.MinY + y));
                switch (kind.Value)
                {
                    case CubeKind.Fire:
                        EmitAmbient(world, new Color(1f, 0.6f, 0.2f),
                            new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.8f, 1.4f)), 0.09f);
                        return;
                    case CubeKind.Water:
                        EmitAmbient(world, new Color(0.4f, 0.65f, 1f),
                            new Vector2(0f, Random.Range(-0.5f, -0.2f)), 0.07f);
                        return;
                    case CubeKind.Gold:
                        EmitAmbient(world + Random.insideUnitCircle * 0.2f,
                            new Color(1f, 0.9f, 0.5f), Vector2.zero, 0.06f);
                        return;
                    case CubeKind.Dynamite:
                        EmitAmbient(world, new Color(1f, 0.4f, 0.25f),
                            Random.insideUnitCircle * 0.5f, 0.05f);
                        return;
                }
            }
        }

        private void EmitAmbient(Vector2 world, Color color, Vector2 velocity, float size)
        {
            var emitParams = new ParticleSystem.EmitParams();
            emitParams.position = new Vector3(world.x, world.y, 0f);
            emitParams.velocity = new Vector3(velocity.x, velocity.y, 0f);
            emitParams.startColor = color;
            emitParams.startSize = size;
            ambient.Emit(emitParams, 1);
        }
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
            StopAllCoroutines();
            animatingWater = false;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            ghostSprites.Clear();
            outsidePreviewSprites.Clear();
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
            kindCache = new CubeKind?[board.Width, board.Height];
            baseColorCache = new Color[board.Width, board.Height];
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    Vector2 pos = CellToWorld(new GridPos(board.MinX + x, board.MinY + y));
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
                    Cube? cube = board.GetCube(new GridPos(board.MinX + x, board.MinY + y));
                    Color color = cube.HasValue
                        ? ViewUtil.CubeDisplayColor(cube.Value)
                        : EmptyColor;
                    cellRenderers[x, y].color = color;
                    kindCache[x, y] = cube.HasValue ? cube.Value.Kind : (CubeKind?)null;
                    baseColorCache[x, y] = color;
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
            cell = new GridPos(board.MinX + x, board.MinY + y);
            return board != null && x >= 0 && x < board.Width && y >= 0 && y < board.Height;
        }

        /// <summary>Unclamped world-to-cell with a margin around the grid, so ghost
        /// blocks can be anchored to overhang ANY edge (including left/bottom).</summary>
        public bool TryWorldToCellLoose(Vector2 world, int margin, out GridPos cell)
        {
            int x = Mathf.FloorToInt((world.x - bottomLeft.x) / cellSize);
            int y = Mathf.FloorToInt((world.y - bottomLeft.y) / cellSize);
            cell = new GridPos(board.MinX + x, board.MinY + y);
            return board != null
                && x >= -margin && x < board.Width + margin
                && y >= -margin && y < board.Height + margin;
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
                else
                {
                    // ghost overhang: preview outside the grid with temporary sprites
                    outsidePreviewSprites.Add(ViewUtil.MakeCell(transform, "PreviewGhost",
                        CellToWorld(pos), cellSize * 0.92f, color, 2));
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
            foreach (SpriteRenderer sprite in outsidePreviewSprites)
            {
                if (sprite != null)
                {
                    Destroy(sprite.gameObject);
                }
            }
            outsidePreviewSprites.Clear();
        }

        /// <summary>Replays the water fall frames as discrete cell steps, then restores
        /// the true board state and invokes onDone (the controller unlocks input).</summary>
        public void PlayWaterAnimation(IReadOnlyList<IReadOnlyList<WaterMove>> frames,
            System.Action onDone)
        {
            StartCoroutine(WaterFallRoutine(frames, onDone));
        }

        private IEnumerator WaterFallRoutine(IReadOnlyList<IReadOnlyList<WaterMove>> frames,
            System.Action onDone)
        {
            animatingWater = true;
            const float tickSeconds = 0.09f;
            Color waterColor = ViewUtil.ElementColor(BlockElement.Water);
            // hide the fallen cubes at their final cells so they can visibly arrive
            var finalCells = new HashSet<GridPos>();
            foreach (IReadOnlyList<WaterMove> frame in frames)
            {
                foreach (WaterMove move in frame)
                {
                    finalCells.Remove(move.From);
                    finalCells.Add(move.To);
                }
            }
            foreach (GridPos pos in finalCells)
            {
                PaintCell(pos, EmptyColor);
            }
            var current = new HashSet<GridPos>();
            var previous = new HashSet<GridPos>();
            foreach (IReadOnlyList<WaterMove> frame in frames)
            {
                bool introduced = false;
                foreach (WaterMove move in frame)
                {
                    if (current.Add(move.From))
                    {
                        introduced = true;
                    }
                }
                if (introduced)
                {
                    PaintWaterState(current, previous, waterColor);
                    yield return new WaitForSeconds(tickSeconds);
                }
                foreach (WaterMove move in frame)
                {
                    current.Remove(move.From);
                }
                foreach (WaterMove move in frame)
                {
                    current.Add(move.To);
                }
                PaintWaterState(current, previous, waterColor);
                yield return new WaitForSeconds(tickSeconds);
            }
            animatingWater = false;
            Refresh();
            if (onDone != null)
            {
                onDone();
            }
        }

        private void PaintWaterState(HashSet<GridPos> current, HashSet<GridPos> previous,
            Color waterColor)
        {
            foreach (GridPos pos in previous)
            {
                if (!current.Contains(pos))
                {
                    PaintCell(pos, EmptyColor);
                }
            }
            foreach (GridPos pos in current)
            {
                PaintCell(pos, waterColor);
            }
            previous.Clear();
            foreach (GridPos pos in current)
            {
                previous.Add(pos);
            }
        }

        private void PaintCell(GridPos pos, Color color)
        {
            if (board != null && board.IsInside(pos))
            {
                cellRenderers[pos.X, pos.Y].color = color;
            }
        }
    }
}
