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

        /// <summary>How far the background plate overhangs the grid, TOTAL across both sides -
        /// so the visible edge of the arena is half of this outside WorldRect, which reports the
        /// cell area only. Named because effects that sit ON the arena's edge need it:
        /// FlameStreakView plants its flames on the visible corner, not on the grid corner.</summary>
        public const float BorderOverhang = 0.15f;
        private static readonly Color EmptyColor = new Color(0.17f, 0.18f, 0.22f);

        /// <summary>A cell shuffle erosion ATE. Deliberately not hidden like an ordinary hole:
        /// the player has to see what the stalling cost them, and that its row/column is dead.</summary>
        private static readonly Color DeadColor = new Color(0.30f, 0.10f, 0.12f, 0.85f);

        /// <summary>An empty cell a boss has sealed off ("Mapus") - it reads as barred, not
        /// as a cube, because nothing can be placed there but nothing occupies it either.
        /// Deliberately COLD, not another scar red: an eaten cell (DeadColor) is gone for good
        /// and kills its line, while a seal lifts again next turn. The two can sit on the same
        /// board, so they must not look alike.</summary>
        private static readonly Color SealedColor = new Color(0.20f, 0.24f, 0.40f);
        private static readonly Color ValidPreviewColor = new Color(0.35f, 1f, 0.45f, 0.6f);
        private static readonly Color InvalidPreviewColor = new Color(1f, 0.35f, 0.35f, 0.6f);
        private static readonly Color ExplosionPreviewColor = new Color(1f, 0.78f, 0.25f, 0.65f);
        private static readonly Color FallingPieceColor = new Color(0.45f, 0.85f, 1f, 0.9f);
        private static readonly Color FallingGhostColor = new Color(0.45f, 0.85f, 1f, 0.28f);

        /// <summary>"Devre"'s circuit nodes - a circuit-board green that reads on both an empty
        /// cell and a full one, since the route crosses both.</summary>
        private static readonly Color CircuitColor = new Color(0.35f, 1f, 0.75f, 0.85f);

        /// <summary>"Matruşka"'s dolls, drawn as a pip ON a cube rather than as a cube. A warm
        /// lacquer red, because a doll is a thing sitting on the board and not a piece of it.</summary>
        private static readonly Color DollColor = new Color(0.95f, 0.35f, 0.30f, 0.95f);

        /// <summary>"İstilacı"'s marked column: the demolition wash. Deliberately its own colour -
        /// a marked column is neither sealed (a seal lifts next turn) nor eaten (that is
        /// permanent); it is a place with a deadline on it.</summary>
        private static readonly Color DoomedColumnTint = new Color(0.85f, 0.45f, 0.12f);

        /// <summary>"Kütleçekim merkezi"'s pull markers, in water's own blue - what they are
        /// telling you about is where the WATER goes, and nothing else.</summary>
        private static readonly Color GravityArrowColor = new Color(0.35f, 0.6f, 1f, 0.75f);

        /// <summary>"Karantina": a cube exploded in here costs what it would have earned. A
        /// sickly wash over the cell, so the zone reads without hiding what stands in it.</summary>
        private static readonly Color QuarantineTint = new Color(0.75f, 0.72f, 0.20f);

        /// <summary>"Besleme": the patch its creature lives in. A warm living pink, distinct from
        /// the quarantine's sickly yellow - one is a thing you feed, the other a thing you avoid.</summary>
        private static readonly Color CreatureTint = new Color(0.85f, 0.35f, 0.55f);

        /// <summary>"Alacakaranlık": what a cell looks like with the lights out. Barely above
        /// the background, so the grid is still findable but tells you nothing.</summary>
        private static readonly Color DarkCellColor = new Color(0.075f, 0.08f, 0.10f);

        /// <summary>The preview under the cursor while blind. ONE neutral colour: it shows where
        /// the block would land and refuses to say whether it fits, or the player could map the
        /// whole board by waving the mouse over it.</summary>
        private static readonly Color BlindPreviewColor = new Color(0.72f, 0.72f, 0.78f, 0.45f);

        /// <summary>How long a blast keeps its surroundings lit, and how far the light reaches.</summary>
        private const float LightSeconds = 1.1f;
        private const int LightRadius = 2;

        /// <summary>How much of its cell a CUBE covers, and how much an EMPTY one does. A
        /// painted tile brings its own frame and wants to sit nearly edge to edge; an empty
        /// cell stays inset so the grid keeps reading as holes between blocks.</summary>
        private const float CubeFill = 0.98f;
        private const float EmptyFill = 0.92f;

        private GameBoard board;
        private SpriteRenderer[,] cellRenderers;
        private SpriteRenderer[,] previewRenderers;
        private CubeKind?[,] kindCache;
        private Color[,] baseColorCache;
        private readonly List<SpriteRenderer> ghostSprites = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> outsidePreviewSprites = new List<SpriteRenderer>();
        private readonly List<GameObject> infectionMarkers = new List<GameObject>();

        /// <summary>Nodes of "Devre"'s traced circuit, redrawn whenever the route changes.</summary>
        private readonly List<GameObject> circuitMarkers = new List<GameObject>();

        /// <summary>"Matruşka"'s dolls, redrawn whenever one splits or moves.</summary>
        private readonly List<GameObject> dollMarkers = new List<GameObject>();

        /// <summary>"Kütleçekim merkezi"'s arrows, shown only while gravity is NOT pointing
        /// down - normal gravity needs no explaining.</summary>
        private readonly List<GameObject> gravityMarkers = new List<GameObject>();

        /// <summary>"İstilacı"'s marked column, or null when nothing is marked.</summary>
        private int? doomedColumn;

        /// <summary>"Karantina"'s sealed rows and columns, in absolute board coordinates.</summary>
        private readonly List<int> quarantinedRows = new List<int>();
        private readonly List<int> quarantinedColumns = new List<int>();

        /// <summary>"Besleme"'s creature patch, in absolute board coordinates.</summary>
        private readonly List<GridPos> creatureCells = new List<GridPos>();

        /// <summary>"Alacakaranlık": the board is dark and the player is blind.</summary>
        private bool dark;

        /// <summary>Seconds of light left on each cell, indexed like cellRenderers. Only ever
        /// non-zero while dark - an explosion writes into it and Update burns it down.</summary>
        private float[,] litFor;
        private ParticleSystem ambient;
        private float ambientTimer;
        private bool animatingWater;

        /// <summary>Seconds one cell of fall takes. Core's frames are discrete cell steps; this
        /// is what turns them back into a SPEED, so a cube crossing several cells slides through
        /// them in one motion instead of appearing in each in turn.</summary>
        private const float WaterCellSeconds = 0.085f;

        /// <summary>How long a drop spends getting up to speed, in cell-times.</summary>
        private const float WaterAccelCells = 0.55f;

        /// <summary>What the acceleration costs the whole fall: starting from rest, a drop is
        /// always this far behind the frame schedule, so the routine has to outlive it.</summary>
        private const float WaterAccelTailSeconds = WaterAccelCells * 0.5f * WaterCellSeconds;

        /// <summary>How long the squash of a landing lasts.</summary>
        private const float WaterSplashSeconds = 0.16f;

        /// <summary>One falling water cube: the cells it passes through, the frame each of its
        /// steps happens on, and the sprite that carries it between them.</summary>
        private sealed class WaterDrop
        {
            public readonly List<GridPos> Cells = new List<GridPos>();
            public readonly List<int> StepFrames = new List<int>();
            public SpriteRenderer Sprite;
            public int Landed;             // cells covered as of its last settle
            public float SplashAt = -1f;   // when that settle happened, or -1
        }

        /// <summary>Every cell a running fall covers, blanked for its duration so the settled
        /// board underneath cannot show through the cubes still travelling.</summary>
        private readonly HashSet<GridPos> waterHiddenCells = new HashSet<GridPos>();

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
            if (dark)
            {
                // Blind: no idle animation may run, or a flickering fire would give away a
                // cube the player is not allowed to see. Only the blast light moves.
                BurnDownLight();
            }
            else if (!animatingWater)
            {
                AnimateElementCubes(); // would fight the fall animation's cell painting
                AnimateInfections();   // green pulse, applied on top of the base/element color
            }
            ambientTimer += Time.deltaTime;
            while (ambientTimer >= 0.12f)
            {
                ambientTimer -= 0.12f;
                if (!dark) // embers and drips would give away where the elements are
                {
                    EmitAmbientParticle();
                }
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

        /// <summary>Burns each lit cell's remaining time down and repaints it, so a blast's
        /// light dims away instead of snapping off. Repaints only while something is still lit.</summary>
        private void BurnDownLight()
        {
            if (litFor == null)
            {
                return;
            }
            bool anyLit = false;
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (litFor[x, y] <= 0f)
                    {
                        continue;
                    }
                    litFor[x, y] -= Time.deltaTime;
                    if (litFor[x, y] < 0f)
                    {
                        litFor[x, y] = 0f;
                    }
                    anyLit = true;
                }
            }
            if (anyLit)
            {
                Refresh();
            }
        }

        /// <summary>How brightly a cell is lit right now, 0 (dark) to 1. Always 1 with the
        /// lights on, so every caller can just multiply by it.</summary>
        private float LightAt(int x, int y)
        {
            if (!dark)
            {
                return 1f;
            }
            if (litFor == null || x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            {
                return 0f;
            }
            return Mathf.Clamp01(litFor[x, y] / LightSeconds);
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
                    // A kind with its own painted tile is left ALONE here: these colour lerps
                    // were written for flat squares and would only wash the art out. Their idle
                    // animations belong to the tile (fire smoulders, water swirls) and are
                    // coming separately - until then the paint speaks for itself.
                    if (ViewUtil.HasOwnTile(kind.Value))
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
        /// <summary>Wash over a line "Kangren" took whole - it can never explode again.</summary>
        public Color RotDeadLineColor = new Color(0.24f, 0.20f, 0.16f);

        private float cellSize = 1f;

        /// <summary>Edge length of one cell in world units, so an effect outside the board (a
        /// defective smuggled block falling off the screen) can draw cubes at the right size.</summary>
        public float CellWorldSize
        {
            get { return cellSize; }
        }

        private Vector2 bottomLeft;
        private SpriteRenderer deadZoneLine; // red separator for the retro dead zone, or null

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
            waterHiddenCells.Clear(); // the drop sprites go with the children below
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            ghostSprites.Clear();
            outsidePreviewSprites.Clear();
            infectionMarkers.Clear();
            infectionCells.Clear();
            deadZoneLine = null; // destroyed with the other children above; redrawn by SetDeadZone
            board = newBoard;
            cellSize = Mathf.Min(maxWorldSize / board.Width, maxWorldSize / board.Height);
            bottomLeft = center - new Vector2(board.Width, board.Height) * (cellSize * 0.5f);

            var background = new GameObject("Background");
            background.transform.SetParent(transform, false);
            background.transform.localPosition = new Vector3(center.x, center.y, 0f);
            background.transform.localScale = new Vector3(
                board.Width * cellSize + BorderOverhang,
                board.Height * cellSize + BorderOverhang, 1f);
            var bgRenderer = background.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = ViewUtil.WhiteSprite;
            bgRenderer.color = BackgroundColor;
            bgRenderer.sortingOrder = 0;

            cellRenderers = new SpriteRenderer[board.Width, board.Height];
            previewRenderers = new SpriteRenderer[board.Width, board.Height];
            kindCache = new CubeKind?[board.Width, board.Height];
            litFor = new float[board.Width, board.Height];
            baseColorCache = new Color[board.Width, board.Height];
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    Vector2 pos = CellToWorld(new GridPos(board.MinX + x, board.MinY + y));
                    cellRenderers[x, y] = ViewUtil.MakeCell(
                        transform, "Cell_" + x + "_" + y, pos, cellSize * EmptyFill, EmptyColor, 1);
                    previewRenderers[x, y] = ViewUtil.MakeCell(
                        transform, "Preview_" + x + "_" + y, pos, cellSize * EmptyFill,
                        ValidPreviewColor, 2);
                    previewRenderers[x, y].enabled = false;
                    // Holes in an irregular board (Kentsel Dönüşüm / Tılsım) are not play
                    // area: hide their cell so they read as a gap, not an empty cell you can
                    // place in. The background shows through.
                    cellRenderers[x, y].enabled =
                        board.IsInside(new GridPos(board.MinX + x, board.MinY + y));
                }
            }
            Refresh();
        }

        /// <summary>
        /// How a placed cube finds the CARD that put it there, by SourceCardId. Set by the
        /// controller from the session's owned cards; null-safe, and null is the honest answer
        /// for a cube no card placed (rot, a snake, a mine).
        ///
        /// Presentation only. It exists because several block types have no cube kind of their
        /// own - a gear or a fox lands as plain cubes, and a targeted block's unmarked cubes
        /// are plain too - so without it those blocks would lose their face the instant they
        /// touched the board. Core stays unaware.
        /// </summary>
        public System.Func<int, BlockCard> CardLookup;

        /// <summary>Cubes on the last repaint whose card could NOT be found, and the id of one
        /// of them. Diagnostic only, read by the F4 gallery: a block type that loses its face
        /// when placed looks exactly like a block type nobody painted, and these two numbers
        /// are what tell those apart.</summary>
        public int UnresolvedCubes { get; private set; }

        public int UnresolvedExampleId { get; private set; }

        private BlockCard CardOf(Cube cube)
        {
            BlockCard card = CardLookup != null ? CardLookup(cube.SourceCardId) : null;
            if (card == null)
            {
                UnresolvedCubes++;
                UnresolvedExampleId = cube.SourceCardId;
            }
            return card;
        }

        /// <summary>Repaints occupancy colors from the board state.</summary>
        public void Refresh()
        {
            if (board == null)
            {
                return;
            }
            // Which way the water inside a water tile leans. A property of the BOARD (the
            // "Kütleçekim merkezi" power turns it for the round), so it is a shader global
            // rather than something every cube carries: one board, one pull.
            Shader.SetGlobalVector("_BlockFlowDir",
                new Vector4(board.WaterFlow.X, board.WaterFlow.Y, 0f, 0f));
            UnresolvedCubes = 0;
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var gp = new GridPos(board.MinX + x, board.MinY + y);
                    if (!board.IsInside(gp))
                    {
                        kindCache[x, y] = null;
                        if (board.IsDead(gp))
                        {
                            // eaten by erosion: shown as scar tissue, not as a gap. Flat, and
                            // explicitly so - the cell may have been carrying a painted tile
                            // the moment before the erosion ate it.
                            cellRenderers[x, y].enabled = true;
                            ViewUtil.ApplyTile(cellRenderers[x, y], null, cellSize * EmptyFill);
                            cellRenderers[x, y].color = DeadColor;
                            baseColorCache[x, y] = DeadColor;
                            continue;
                        }
                        // hole: stays a hidden gap, never a cube (ghost traces draw separately)
                        cellRenderers[x, y].enabled = false;
                        continue;
                    }
                    cellRenderers[x, y].enabled = true;
                    Cube? cube = board.GetCube(gp);
                    // A CUBE is a painted tile and fills its cell; an EMPTY cell stays the flat
                    // inset square it always was, so the grid still reads as holes waiting to be
                    // filled rather than as pale blocks.
                    Sprite tile = null;
                    if (cube.HasValue)
                    {
                        tile = ViewUtil.CubeTile(cube.Value.Kind, CardOf(cube.Value));
                        ViewUtil.ApplyTile(cellRenderers[x, y], tile, cellSize * CubeFill);
                    }
                    else
                    {
                        ViewUtil.ApplyTile(cellRenderers[x, y], null, cellSize * EmptyFill);
                    }
                    Color color = cube.HasValue
                        ? ViewUtil.CubeTileColor(cube.Value, tile)
                        : (board.IsSealed(gp) ? SealedColor : EmptyColor);
                    // "Karantina" washes its sealed lines without hiding what stands in them.
                    if (IsQuarantined(gp))
                    {
                        color = Color.Lerp(color, QuarantineTint, cube.HasValue ? 0.45f : 0.6f);
                    }
                    // "Kangren": a line the rot took WHOLE can never explode again, which the
                    // player has to be able to see - an unexplodable full line otherwise reads as
                    // a bug. Washed like the erosion scar it behaves like.
                    if (board.RowIsInfectionDead(gp.Y) || board.ColumnIsInfectionDead(gp.X))
                    {
                        color = Color.Lerp(color, RotDeadLineColor, cube.HasValue ? 0.4f : 0.66f);
                    }
                    // "Besleme"'s creature: the patch you have to keep feeding, so it has to be
                    // unmistakable whether there is a cube standing on it or not.
                    if (IsCreature(gp))
                    {
                        color = Color.Lerp(color, CreatureTint, cube.HasValue ? 0.5f : 0.72f);
                    }
                    // "İstilacı": the column with a demolition date on it. Washed rather than
                    // hidden - the player has to be able to see exactly what they are about to
                    // lose and decide whether to keep building there anyway.
                    if (doomedColumn.HasValue && gp.X == doomedColumn.Value)
                    {
                        color = Color.Lerp(color, DoomedColumnTint, cube.HasValue ? 0.45f : 0.6f);
                    }
                    // "Alacakaranlık": the truth is drowned in the dark and only a blast's
                    // light brings any of it back, in proportion to how bright that light is.
                    if (dark)
                    {
                        color = Color.Lerp(DarkCellColor, color, LightAt(x, y));
                    }
                    cellRenderers[x, y].color = color;
                    kindCache[x, y] = cube.HasValue ? cube.Value.Kind : (CubeKind?)null;
                    baseColorCache[x, y] = color;
                }
            }
            RefreshGhostTraces();
            if (animatingWater)
            {
                HideWaterCells();
            }
        }

        /// <summary>Ghost cubes hanging outside the grid render as faint traces.</summary>
        private void RefreshGhostTraces()
        {
            if (dark)
            {
                // A ghost trace is a cube by another name; blind means blind.
                foreach (SpriteRenderer sprite in ghostSprites)
                {
                    if (sprite != null)
                    {
                        Destroy(sprite.gameObject);
                    }
                }
                ghostSprites.Clear();
                return;
            }
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

        private struct InfectionMark
        {
            public int Lx;
            public int Ly;
            public int Turns;
            public int Threshold;
        }

        private readonly List<InfectionMark> infectionCells = new List<InfectionMark>();
        private static readonly Color InfectionGreen = new Color(0.2f, 0.95f, 0.35f);

        /// <summary>Draws the "Enfeksiyon" markers: the infected block pulses GREEN (like a
        /// mine but green, animated in Update), and a row of pips (filled = turns elapsed)
        /// shows the 3-turn countdown to detonation. Rebuilt each refresh.</summary>
        public void ShowInfections(IReadOnlyList<InfectedCell> cells)
        {
            ClearInfections();
            if (board == null || cells == null)
            {
                return;
            }
            for (int i = 0; i < cells.Count; i++)
            {
                InfectedCell inf = cells[i];
                if (!board.IsInside(inf.Cell))
                {
                    continue;
                }
                int lx = inf.Cell.X - board.MinX;
                int ly = inf.Cell.Y - board.MinY;
                infectionCells.Add(new InfectionMark
                {
                    Lx = lx, Ly = ly, Turns = inf.Turns, Threshold = inf.Threshold
                });

                // Static buildup pips just below the cell (filled green = turns elapsed).
                Vector2 center = CellToWorld(inf.Cell);
                var root = new GameObject("InfectionPips");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = new Vector3(center.x, center.y, 0f);
                infectionMarkers.Add(root);

                int pips = Mathf.Max(inf.Threshold, 1);
                float pip = cellSize * 0.17f;
                float startX = -(pips - 1) * pip * 0.9f;
                for (int p = 0; p < pips; p++)
                {
                    Color pipColor = p < inf.Turns
                        ? new Color(0.35f, 1f, 0.4f)
                        : new Color(0.22f, 0.3f, 0.24f);
                    ViewUtil.MakeCell(root.transform, "Pip",
                        new Vector2(startX + p * pip * 1.8f, -cellSize * 0.34f), pip, pipColor, 3);
                }
            }
        }

        /// <summary>Pulses each infected cell green, brighter the closer it is to detonating -
        /// the infection twin of the mine cube's red blink. Runs every frame.</summary>
        private void AnimateInfections()
        {
            if (infectionCells.Count == 0 || cellRenderers == null)
            {
                return;
            }
            float time = Time.time;
            for (int i = 0; i < infectionCells.Count; i++)
            {
                InfectionMark m = infectionCells[i];
                if (m.Lx < 0 || m.Lx >= board.Width || m.Ly < 0 || m.Ly >= board.Height)
                {
                    continue;
                }
                float progress = m.Threshold > 0 ? Mathf.Clamp01(m.Turns / (float)m.Threshold) : 1f;
                float blend = Mathf.Clamp01(
                    0.2f + 0.4f * progress + 0.25f * Mathf.Sin(time * 5f + m.Lx + m.Ly));
                cellRenderers[m.Lx, m.Ly].color = Color.Lerp(baseColorCache[m.Lx, m.Ly],
                    InfectionGreen, blend);
            }
        }

        /// <summary>Turns the lights out, or back on ("Alacakaranlık").</summary>
        public void SetDarkness(bool on)
        {
            if (dark == on)
            {
                return;
            }
            dark = on;
            if (!dark && litFor != null)
            {
                System.Array.Clear(litFor, 0, litFor.Length);
            }
            Refresh();
        }

        /// <summary>True while the board is playing blind.</summary>
        public bool IsDark
        {
            get { return dark; }
        }

        /// <summary>A blast lights its own surroundings for a moment. Only means anything while
        /// dark; on a lit board there is nothing to reveal.</summary>
        public void LightUpAround(IReadOnlyList<GridPos> cells)
        {
            if (!dark || board == null || litFor == null || cells == null)
            {
                return;
            }
            foreach (GridPos cell in cells)
            {
                int cx = cell.X - board.MinX;
                int cy = cell.Y - board.MinY;
                for (int x = cx - LightRadius; x <= cx + LightRadius; x++)
                {
                    for (int y = cy - LightRadius; y <= cy + LightRadius; y++)
                    {
                        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
                        {
                            continue;
                        }
                        // Round light: the corners of the square stay dark, so a blast reads as
                        // a glow rather than as a box.
                        int dx = x - cx;
                        int dy = y - cy;
                        if (dx * dx + dy * dy > LightRadius * LightRadius)
                        {
                            continue;
                        }
                        // Nearer cells hold the light longer, so it fades from the edge inward.
                        float share = 1f - Mathf.Sqrt(dx * dx + dy * dy) / (LightRadius + 1f);
                        float seconds = LightSeconds * share;
                        if (seconds > litFor[x, y])
                        {
                            litFor[x, y] = seconds;
                        }
                    }
                }
            }
            Refresh();
        }

        /// <summary>Marks where "Besleme"'s creature lives. Pass null to clear it.</summary>
        public void ShowCreature(IReadOnlyList<GridPos> cells)
        {
            creatureCells.Clear();
            if (cells != null)
            {
                creatureCells.AddRange(cells);
            }
        }

        private bool IsCreature(GridPos cell)
        {
            for (int i = 0; i < creatureCells.Count; i++)
            {
                if (creatureCells[i].X == cell.X && creatureCells[i].Y == cell.Y)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Marks "Karantina"'s sealed lines. Pass nulls to clear them.</summary>
        public void ShowQuarantine(IReadOnlyList<int> rows, IReadOnlyList<int> columns)
        {
            quarantinedRows.Clear();
            quarantinedColumns.Clear();
            if (rows != null) { quarantinedRows.AddRange(rows); }
            if (columns != null) { quarantinedColumns.AddRange(columns); }
        }

        /// <summary>True if that cell stands in a sealed row or column.</summary>
        private bool IsQuarantined(GridPos cell)
        {
            return quarantinedRows.Contains(cell.Y) || quarantinedColumns.Contains(cell.X);
        }

        /// <summary>
        /// "Kütleçekim merkezi": marks which way water is being pulled, with a row of pips just
        /// outside the edge it falls towards. Nothing is drawn while gravity points DOWN, because
        /// that is what every board does and a permanent marker for it would be noise.
        /// </summary>
        public void ShowGravity(GridPos flow)
        {
            for (int i = gravityMarkers.Count - 1; i >= 0; i--)
            {
                if (gravityMarkers[i] != null)
                {
                    Destroy(gravityMarkers[i]);
                }
            }
            gravityMarkers.Clear();
            if (board == null || (flow.X == 0 && flow.Y == -1))
            {
                return;
            }
            // One pip per lane, just beyond the edge the water is heading for.
            bool horizontal = flow.X != 0;
            int lanes = horizontal ? board.Height : board.Width;
            for (int i = 0; i < lanes; i++)
            {
                GridPos edge = horizontal
                    ? new GridPos(flow.X > 0 ? board.MinX + board.Width - 1 : board.MinX,
                        board.MinY + i)
                    : new GridPos(board.MinX + i,
                        flow.Y > 0 ? board.MinY + board.Height - 1 : board.MinY);
                Vector2 at = CellToWorld(edge)
                    + new Vector2(flow.X, flow.Y) * (cellSize * 0.72f);
                SpriteRenderer pip = ViewUtil.MakeRect(transform, "Gravity_" + i, at,
                    new Vector2(cellSize * (horizontal ? 0.18f : 0.5f),
                        cellSize * (horizontal ? 0.5f : 0.18f)),
                    GravityArrowColor, 6);
                gravityMarkers.Add(pip.gameObject);
            }
        }

        /// <summary>Marks "İstilacı"'s doomed column. Pass null to clear it.</summary>
        public void ShowDoomedColumn(int? column)
        {
            doomedColumn = column;
        }

        /// <summary>
        /// Draws "Matruşka"'s dolls as pips sitting ON their host cubes. Sized by how many splits
        /// each has left, so a nearly-spent doll is visibly a small one - which is the only way
        /// the player can tell how much of the ladder is behind them.
        /// </summary>
        public void ShowDolls(IReadOnlyList<GridPos> cells, IReadOnlyList<int> sizes)
        {
            for (int i = dollMarkers.Count - 1; i >= 0; i--)
            {
                if (dollMarkers[i] != null)
                {
                    Destroy(dollMarkers[i]);
                }
            }
            dollMarkers.Clear();
            if (board == null || cells == null)
            {
                return;
            }
            for (int i = 0; i < cells.Count; i++)
            {
                if (!board.IsInside(cells[i]))
                {
                    continue;
                }
                int left = sizes != null && i < sizes.Count ? sizes[i] : 1;
                if (left < 1) { left = 1; }
                float scale = cellSize * (0.22f + 0.09f * Mathf.Min(left, 4));
                SpriteRenderer pip = ViewUtil.MakeRect(transform, "Doll_" + i,
                    CellToWorld(cells[i]), new Vector2(scale, scale), DollColor, 6);
                dollMarkers.Add(pip.gameObject);
            }
        }

        /// <summary>Draws "Devre"'s circuit as a chain of small nodes across the grid. The route
        /// arrives in order, so consecutive nodes are always neighbours and the chain reads as a
        /// line. Pass null or an empty list to clear it. Drawn ON TOP of the cells, because a
        /// circuit cell may be empty or full and the player has to see the route either way.</summary>
        public void ShowCircuit(IReadOnlyList<GridPos> cells)
        {
            for (int i = circuitMarkers.Count - 1; i >= 0; i--)
            {
                if (circuitMarkers[i] != null)
                {
                    Destroy(circuitMarkers[i]);
                }
            }
            circuitMarkers.Clear();
            if (board == null || cells == null)
            {
                return;
            }
            for (int i = 0; i < cells.Count; i++)
            {
                if (!board.IsInside(cells[i]))
                {
                    continue;
                }
                SpriteRenderer node = ViewUtil.MakeRect(transform, "Circuit_" + i,
                    CellToWorld(cells[i]), new Vector2(cellSize * 0.3f, cellSize * 0.3f),
                    CircuitColor, 6);
                circuitMarkers.Add(node.gameObject);
            }
        }

        public void ClearInfections()
        {
            infectionCells.Clear();
            for (int i = infectionMarkers.Count - 1; i >= 0; i--)
            {
                if (infectionMarkers[i] != null)
                {
                    Destroy(infectionMarkers[i]);
                }
            }
            infectionMarkers.Clear();
        }

        /// <summary>World center of a cell. Coords are taken RELATIVE to the board origin
        /// (MinX/MinY), which inflation can push negative - so the grid stays centered on the
        /// arena no matter where the origin sits.</summary>
        public Vector2 CellToWorld(GridPos cell)
        {
            return bottomLeft + new Vector2(
                (cell.X - board.MinX + 0.5f) * cellSize,
                (cell.Y - board.MinY + 0.5f) * cellSize);
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
            // Blind: ONE neutral colour. Showing valid/invalid would let the player map the
            // whole board just by waving the cursor across it ("Alacakaranlık").
            Color color = dark
                ? BlindPreviewColor
                : (valid ? ValidPreviewColor : InvalidPreviewColor);
            foreach (GridPos offset in shape.Cells)
            {
                GridPos pos = origin + offset;
                if (board.IsInside(pos))
                {
                    // absolute cell -> local array index (origin can be negative after inflation)
                    int lx = pos.X - board.MinX;
                    int ly = pos.Y - board.MinY;
                    previewRenderers[lx, ly].color = color;
                    previewRenderers[lx, ly].enabled = true;
                }
                else
                {
                    // ghost overhang: preview outside the grid with temporary sprites
                    outsidePreviewSprites.Add(ViewUtil.MakeCell(transform, "PreviewGhost",
                        CellToWorld(pos), cellSize * 0.92f, color, 2));
                }
            }
            // The explosion preview is the biggest tell of all: it would announce exactly which
            // lines are one cube from full. Blind means blind.
            if (!valid || dark)
            {
                return;
            }
            LineExplosionResult predicted = board.PredictExplosions(shape, origin);
            foreach (GridPos pos in predicted.ExplodedCells)
            {
                int lx = pos.X - board.MinX;
                int ly = pos.Y - board.MinY;
                previewRenderers[lx, ly].color = ExplosionPreviewColor;
                previewRenderers[lx, ly].enabled = true;
            }
        }

        /// <summary>Tints the given board cells with the explosion color, so a board-targeting
        /// power (Çaprazlama) can preview its blast while being aimed. Cells outside the board
        /// are skipped.</summary>
        public void ShowPowerPreview(IReadOnlyList<GridPos> cells)
        {
            ClearPreview();
            if (board == null || cells == null)
            {
                return;
            }
            for (int i = 0; i < cells.Count; i++)
            {
                GridPos pos = cells[i];
                if (board.IsInside(pos))
                {
                    previewRenderers[pos.X - board.MinX, pos.Y - board.MinY].color = ExplosionPreviewColor;
                    previewRenderers[pos.X - board.MinX, pos.Y - board.MinY].enabled = true;
                }
            }
        }

        /// <summary>Draws (or hides) the red line separating the game area from the retro dead
        /// zone - the top <paramref name="deadZoneRows"/> rows. Recreated on demand; the controller
        /// calls it after each refresh (the board may have been rebuilt/resized).</summary>
        public void SetDeadZone(int deadZoneRows)
        {
            if (deadZoneLine != null)
            {
                Destroy(deadZoneLine.gameObject);
                deadZoneLine = null;
            }
            if (board == null || deadZoneRows <= 0 || deadZoneRows >= board.Height)
            {
                return;
            }
            int floor = board.MinY + board.Height - deadZoneRows; // first dead row
            Vector2 leftCell = CellToWorld(new GridPos(board.MinX, floor));
            float y = leftCell.y - cellSize * 0.5f;               // bottom edge of the first dead row
            float width = board.Width * cellSize;
            float cx = leftCell.x - cellSize * 0.5f + width * 0.5f;
            deadZoneLine = ViewUtil.MakeRect(transform, "DeadZoneLine", new Vector2(cx, y),
                new Vector2(width, 0.14f), new Color(0.95f, 0.22f, 0.22f), 6);
        }

        /// <summary>Retro falling-piece render: the ghost (where the piece will land) faint,
        /// under the live falling piece, both drawn like a placement preview. Cells still up in
        /// the air (above the grid) render as overhang sprites via CellToWorld.</summary>
        public void ShowFallingPiece(BlockShape shape, GridPos currentOrigin, GridPos ghostOrigin)
        {
            ClearPreview();
            if (board == null || shape == null)
            {
                return;
            }
            foreach (GridPos offset in shape.Cells)
            {
                PaintPreviewCell(ghostOrigin + offset, FallingGhostColor);
            }
            foreach (GridPos offset in shape.Cells)
            {
                PaintPreviewCell(currentOrigin + offset, FallingPieceColor);
            }
        }

        /// <summary>Tints one preview cell: in-grid cells use the persistent renderers; a cell
        /// outside the grid (a piece still in the air) gets a temporary overhang sprite.</summary>
        private void PaintPreviewCell(GridPos pos, Color color)
        {
            if (board.IsInside(pos))
            {
                int lx = pos.X - board.MinX;
                int ly = pos.Y - board.MinY;
                previewRenderers[lx, ly].color = color;
                previewRenderers[lx, ly].enabled = true;
            }
            else
            {
                outsidePreviewSprites.Add(ViewUtil.MakeCell(transform, "FallingGhost",
                    CellToWorld(pos), cellSize * 0.92f, color, 2));
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

        /// <summary>Replays the water fall frames, then restores the true board state and
        /// invokes onDone (the controller unlocks input).
        ///
        /// Core hands the fall over as DISCRETE cell steps, but water must not read as a cube
        /// teleporting one box at a time: the frames are rebuilt into per-cube paths and each
        /// cube is a sprite that SLIDES along its own, so a five-cell drop is one continuous
        /// motion rather than five repaints. The grid cells underneath are blanked for the
        /// duration - the board has already settled, so they would otherwise show the cubes
        /// standing at their destinations while the sprites are still travelling.</summary>
        public void PlayWaterAnimation(IReadOnlyList<IReadOnlyList<WaterMove>> frames,
            System.Action onDone)
        {
            StartCoroutine(WaterFallRoutine(frames, onDone));
        }

        private IEnumerator WaterFallRoutine(IReadOnlyList<IReadOnlyList<WaterMove>> frames,
            System.Action onDone)
        {
            if (frames.Count == 0)
            {
                // Routine: the turn's fall is split around the boom, so one half is often
                // empty. Nothing to play, and the caller must not be made to wait for it.
                if (onDone != null)
                {
                    onDone();
                }
                yield break;
            }
            animatingWater = true;
            List<WaterDrop> drops = BuildWaterDrops(frames);
            Color waterColor = ViewUtil.ElementColor(BlockElement.Water);
            waterHiddenCells.Clear();
            foreach (WaterDrop drop in drops)
            {
                foreach (GridPos cell in drop.Cells)
                {
                    waterHiddenCells.Add(cell);
                }
                drop.Sprite = ViewUtil.MakeCell(transform, "WaterDrop",
                    CellToWorld(drop.Cells[0]), cellSize * CubeFill, waterColor, 2);
                // A drop in flight is the cube that left the cell, so it carries the same tile.
                ViewUtil.ApplyTile(drop.Sprite, ViewUtil.CubeTile(CubeKind.Water),
                    cellSize * CubeFill);
            }
            HideWaterCells();
            float total = frames.Count * WaterCellSeconds + WaterAccelTailSeconds
                + WaterSplashSeconds;
            float elapsed = 0f;
            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                foreach (WaterDrop drop in drops)
                {
                    StepWaterDrop(drop, elapsed, waterColor);
                }
                yield return null;
            }
            foreach (WaterDrop drop in drops)
            {
                if (drop.Sprite != null)
                {
                    Destroy(drop.Sprite.gameObject);
                }
            }
            waterHiddenCells.Clear();
            animatingWater = false;
            Refresh();
            if (onDone != null)
            {
                onDone();
            }
        }

        /// <summary>Rebuilds the frame list into one path per water cube: which cells it passes
        /// through, and which frame each of its steps happens on. A cube waiting for the cell
        /// ahead to empty has a GAP between two of its steps, which is why a step carries its
        /// own frame number instead of being counted off from the first one.</summary>
        private static List<WaterDrop> BuildWaterDrops(
            IReadOnlyList<IReadOnlyList<WaterMove>> frames)
        {
            var drops = new List<WaterDrop>();
            var occupant = new Dictionary<GridPos, WaterDrop>();
            var moving = new List<WaterDrop>();
            for (int f = 0; f < frames.Count; f++)
            {
                IReadOnlyList<WaterMove> frame = frames[f];
                moving.Clear();
                for (int i = 0; i < frame.Count; i++)
                {
                    WaterDrop drop;
                    if (!occupant.TryGetValue(frame[i].From, out drop))
                    {
                        drop = new WaterDrop();
                        drop.Cells.Add(frame[i].From);
                        drops.Add(drop);
                    }
                    moving.Add(drop);
                }
                // Vacate first, THEN fill: a cube may move into the cell another one is leaving
                // on this very frame, and the two must not be mistaken for each other.
                for (int i = 0; i < frame.Count; i++)
                {
                    occupant.Remove(frame[i].From);
                }
                for (int i = 0; i < frame.Count; i++)
                {
                    moving[i].Cells.Add(frame[i].To);
                    moving[i].StepFrames.Add(f);
                    occupant[frame[i].To] = moving[i];
                }
            }
            return drops;
        }

        /// <summary>Places one drop for the current time: how far along its path it has got,
        /// the stretch its speed gives it, and the squash of the landing it just made.</summary>
        private void StepWaterDrop(WaterDrop drop, float elapsed, Color waterColor)
        {
            if (drop.Sprite == null)
            {
                return;
            }
            int steps = drop.StepFrames.Count;
            float travelled = steps; // cells covered from Cells[0]
            float speed = 0f;
            bool atRest = true;
            int step = 0;
            while (step < steps)
            {
                // One RUN: the steps this drop takes back to back, without waiting in between.
                int runStart = step;
                while (step + 1 < steps && drop.StepFrames[step + 1] == drop.StepFrames[step] + 1)
                {
                    step++;
                }
                int runLength = step - runStart + 1;
                float cellTimes = (elapsed - drop.StepFrames[runStart] * WaterCellSeconds)
                    / WaterCellSeconds;
                if (cellTimes <= 0f)
                {
                    travelled = runStart; // still waiting for this run to start
                    break;
                }
                float covered = WaterProfile(cellTimes);
                if (covered < runLength)
                {
                    travelled = runStart + covered;
                    speed = Mathf.Clamp01(cellTimes / WaterAccelCells);
                    atRest = false;
                    break;
                }
                travelled = runStart + runLength;
                step++;
            }

            if (atRest && (int)travelled > drop.Landed)
            {
                drop.Landed = (int)travelled;
                drop.SplashAt = elapsed;
                SplashWater(CellToWorld(drop.Cells[drop.Landed]),
                    StepDirection(drop, drop.Landed - 1));
            }

            int index = Mathf.Clamp((int)travelled, 0, drop.Cells.Count - 1);
            float frac = travelled - index;
            Vector2 world = CellToWorld(drop.Cells[index]);
            if (frac > 0f && index + 1 < drop.Cells.Count)
            {
                world = Vector2.Lerp(world, CellToWorld(drop.Cells[index + 1]), frac);
            }
            drop.Sprite.transform.localPosition = new Vector3(world.x, world.y, 0f);

            // Stretched along the flow while it is moving, squashed across it when it lands -
            // the two cues that read as "falling" and "arrived" now that nothing snaps.
            float along = 1f + 0.25f * speed;
            float across = 1f - 0.12f * speed;
            float sinceSplash = elapsed - drop.SplashAt;
            if (drop.SplashAt >= 0f && sinceSplash < WaterSplashSeconds)
            {
                float bump = Mathf.Sin(sinceSplash / WaterSplashSeconds * Mathf.PI) * 0.3f;
                along = 1f - bump;
                across = 1f + bump * 0.75f;
            }
            float size = cellSize * CubeFill;
            GridPos flow = StepDirection(drop, Mathf.Min(index, steps - 1));
            drop.Sprite.transform.localScale = flow.X != 0
                ? new Vector3(size * along, size * across, 1f)
                : new Vector3(size * across, size * along, 1f);

            // The same wave the resting water cubes carry, so a drop in flight still looks
            // like the cube it is about to become again - unless it is on the painted tile,
            // which carries its own water and only wants to be left white.
            drop.Sprite.color = ViewUtil.HasOwnTile(CubeKind.Water)
                ? Color.white
                : Color.Lerp(waterColor, new Color(0.2f, 0.42f, 0.9f),
                    0.3f + 0.3f * Mathf.Sin(Time.time * 2.2f + drop.Cells[index].X * 0.9f));
        }

        /// <summary>Cells covered after so many cell-times of falling: a brief acceleration from
        /// rest, then a constant one cell per cell-time. Deliberately one-sided - a drop only
        /// ever LAGS behind the schedule the frames describe and never runs ahead of it, which
        /// is what keeps it from sliding into the drop in front of it.</summary>
        private static float WaterProfile(float cellTimes)
        {
            if (cellTimes < WaterAccelCells)
            {
                return cellTimes * cellTimes / (2f * WaterAccelCells);
            }
            return cellTimes - WaterAccelCells * 0.5f;
        }

        /// <summary>Which way the drop's step number `step` went (the last one it took, for a
        /// drop standing still). Falls back to the arena's own pull for a pathless drop.</summary>
        private GridPos StepDirection(WaterDrop drop, int step)
        {
            int to = Mathf.Clamp(step + 1, 1, drop.Cells.Count - 1);
            if (drop.Cells.Count < 2)
            {
                return board != null ? board.WaterFlow : new GridPos(0, -1);
            }
            return new GridPos(drop.Cells[to].X - drop.Cells[to - 1].X,
                drop.Cells[to].Y - drop.Cells[to - 1].Y);
        }

        /// <summary>The little spray a drop throws off where it lands, back the way it came.
        /// Silent while blind: a splash would say where the water is.</summary>
        private void SplashWater(Vector2 world, GridPos flow)
        {
            if (dark)
            {
                return;
            }
            var back = new Vector2(-flow.X, -flow.Y);
            var sideways = new Vector2(-back.y, back.x);
            for (int i = 0; i < 3; i++)
            {
                EmitAmbient(world, new Color(0.55f, 0.75f, 1f),
                    back * Random.Range(0.5f, 1.1f) + sideways * Random.Range(-0.5f, 0.5f),
                    0.055f);
            }
        }

        /// <summary>Blanks the cells the falling cubes are standing in or heading for. Called
        /// again from Refresh while a fall is running, because a repaint in the middle of one
        /// (a blast lighting the dark, say) would put the settled cubes back under the drops
        /// that are still on their way to them.</summary>
        private void HideWaterCells()
        {
            foreach (GridPos cell in waterHiddenCells)
            {
                PaintCell(cell, EmptyColor);
            }
        }

        private void PaintCell(GridPos pos, Color color)
        {
            if (board != null && board.IsInside(pos))
            {
                // absolute cell -> local array index (origin can be negative after inflation)
                cellRenderers[pos.X - board.MinX, pos.Y - board.MinY].color = color;
            }
        }
    }
}
