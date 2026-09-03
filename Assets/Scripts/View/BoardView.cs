// PURPOSE: Draws the round's board as a grid of runtime sprites and shows the
// placement preview under the mouse. Pure presentation - reads GameBoard, never
// mutates it. Rebuilt whenever a round starts (board sizes differ per round).
//
// EVERY PREVIEW HIGHLIGHT BREATHES, and it does so in ONE place: PaintPreviewCell is the only
// thing that colours the preview layer, and BreathePreview re-lays the swing over it each frame
// (see PreviewBreathPeriod). So the green "it fits", the red "it does not" and the yellow "this
// line goes" are on the same beat by construction, and a new preview gets it for free.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Debug renderer for the play grid.</summary>
    public sealed class BoardView : MonoBehaviour
    {

        /// <summary>Seconds per pulse at the first overtime level and at the deepest. It speeds
        /// up as the round runs on, which is the part that reads as pressure - but not far.
        /// These were 1.30 and 0.42 first: 0.42s is 2.4Hz, and a large area of the screen
        /// swinging that fast is genuinely unpleasant to sit in front of. At 2.4 the deepest
        /// pulse is a slow breath, which is what a clock running out should feel like anyway.</summary>
        private const float OvertimePulseSlow = 4.60f;

        private const float OvertimePulseFast = 2.40f;

        // THE OVERTIME EFFECT ONLY ADDS LIGHT. It used to tint this plate as well, which is what
        // the lines are cut out of - and that was wrong for a reason worth keeping written down:
        // the plate shows through a gap of cellSize * (1 - EmptyFill) between two empty cells and
        // cellSize * (1 - CubeFill) between two filled ones, and at 0.92 against 0.98 the first
        // is EIGHT TIMES the second. Tinting it therefore lit a band whose width depended on what
        // was on the board, so the arena visibly changed the moment the first block landed.
        //
        // BoardLineGlowView draws the whole effect now, on its own fixed-width filament over the
        // top. Nothing here re-colours geometry that the board's own contents can resize.

        /// <summary>Overtime levels over which the pulse reaches its deepest. Matches
        /// FlameStreakView's own full-heat level so the fire and the lines escalate together.</summary>
        private const int OvertimeFullLevel = 6;

        /// <summary>How far the background plate overhangs the grid, TOTAL across both sides -
        /// so the visible edge of the arena is half of this outside WorldRect, which reports the
        /// cell area only. Named because effects that sit ON the arena's edge need it:
        /// FlameStreakView plants its flames on the visible corner, not on the grid corner.</summary>
        public const float BorderOverhang = 0.15f;
        private static readonly Color EmptyColor = new Color(0.112f, 0.121f, 0.147f);

        /// <summary>A cell shuffle erosion ATE. Deliberately not hidden like an ordinary hole:
        /// the player has to see what the stalling cost them, and that its row/column is dead.</summary>
        private static readonly Color DeadColor = new Color(0.30f, 0.10f, 0.12f, 0.85f);

        /// <summary>An empty cell a boss has sealed off ("Mapus") - it reads as barred, not
        /// as a cube, because nothing can be placed there but nothing occupies it either.
        /// Deliberately COLD, not another scar red: an eaten cell (DeadColor) is gone for good
        /// and kills its line, while a seal lifts again next turn. The two can sit on the same
        /// board, so they must not look alike.</summary>
        private static readonly Color SealedColor = new Color(0.20f, 0.24f, 0.40f);

        /// <summary>Empty BONUS ground ("Tılsım"): free to build on, and no line waits for it.
        /// Warm and faint - a gift, not a wound - so it cannot be mistaken for the eroded cell
        /// (DeadColor) or the barred one (SealedColor) it may sit beside.</summary>
        private static readonly Color BonusGroundColor = new Color(0.20f, 0.19f, 0.13f);
        private static readonly Color ValidPreviewColor = new Color(0.35f, 1f, 0.45f, 0.6f);
        private static readonly Color InvalidPreviewColor = new Color(1f, 0.35f, 0.35f, 0.6f);
        private static readonly Color ExplosionPreviewColor = new Color(1f, 0.78f, 0.25f, 0.65f);
        private static readonly Color FallingPieceColor = new Color(0.45f, 0.85f, 1f, 0.9f);
        private static readonly Color FallingGhostColor = new Color(0.45f, 0.85f, 1f, 0.28f);

        /// <summary>EVERY preview highlight BREATHES - the green "it fits", the red "it does not"
        /// and the yellow "this line goes" all swell and settle on the same clock, so the layer
        /// that answers a question reads as alive while the board underneath stays still.
        ///
        /// Seconds per breath. A preview lives only as long as the cursor rests on a cell, so a
        /// real 4s breath (the overtime pulse's slowest) would show the player a random slice of
        /// one wave and never a whole one. At 0.85s a breath completes under a resting cursor
        /// two or three times over, and it is still well under the 2.4Hz the overtime note calls
        /// unpleasant to sit in front of.</summary>
        private const float PreviewBreathPeriod = 0.85f;

        /// <summary>The swing is NOT symmetric: it goes down hard and up barely. What the eye
        /// catches is the DARKENING, so that end is where the range is spent - the painted colour
        /// is very nearly the top of the breath and the cell falls to under half of it, rather
        /// than the highlight lightening and darkening the same amount around a middle.
        ///
        /// These multiply the painted rgb: Dim at the bottom of the breath, Lift at the top.</summary>
        private const float PreviewBreathDim = 0.42f;

        private const float PreviewBreathLift = 1.08f;

        /// <summary>What the ALPHA does at the bottom of the breath, as a multiple of the painted
        /// alpha (it is the painted alpha at the top). It goes UP as the colour goes down, and
        /// that is the whole reason the dark end reads as dark on every cell: a preview square
        /// lies over an empty cell one moment and over a bright cube the next, and dimming the
        /// colour while ALSO thinning the square would only let the cube underneath show through -
        /// which is lighter, not darker. Dimming and covering more at the same time is what makes
        /// it darker over anything.</summary>
        private const float PreviewBreathDimAlpha = 1.20f;

        /// <summary>"Devre"'s circuit nodes - a circuit-board green that reads on both an empty
        /// cell and a full one, since the route crosses both.</summary>
        private static readonly Color CircuitColor = new Color(0.35f, 1f, 0.75f, 0.85f);

        /// <summary>"Matruşka"'s dolls, drawn as a pip ON a cube rather than as a cube. A warm
        /// lacquer red, because a doll is a thing sitting on the board and not a piece of it.</summary>
        private static readonly Color DollColor = new Color(0.95f, 0.35f, 0.30f, 0.95f);

        /// <summary>"İstilacı"'s marked column: the demolition wash. Deliberately its own colour -
        /// a marked column is neither sealed (a seal lifts next turn) nor eaten (that is
        /// permanent); it is a place with a deadline on it.</summary>
        /// <summary>"Kütleçekim merkezi"'s pull markers, in water's own blue - what they are
        /// telling you about is where the WATER goes, and nothing else.</summary>
        private static readonly Color GravityArrowColor = new Color(0.35f, 0.6f, 1f, 0.75f);

        /// <summary>"Karantina": how much colour is pulled out of a block standing in a sealed
        /// zone, and how far its value is taken down. This is done to the block's OWN colour
        /// rather than by an overlay, because alpha blending can only mix toward a colour - it
        /// cannot drain one - and a zone that merely tinted its blocks would still look like a
        /// place things live. The hue survives, so the player can still read WHICH block it is.</summary>
        private const float QuarantineDrain = 0.66f;

        private const float QuarantineDarken = 0.50f;

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
        private const float EmptyFill = 0.82f;

        private GameBoard board;
        private SpriteRenderer[,] cellRenderers;
        private SpriteRenderer[,] previewRenderers;

        /// <summary>The colour each preview cell was PAINTED, before the breath. Kept because the
        /// breath is re-applied every frame and must always start from the painted colour - reading
        /// the renderer back and modulating that would compound the swing into a runaway.</summary>
        private Color[,] previewBaseColors;

        /// <summary>The painted colours of the overhang sprites, parallel to
        /// outsidePreviewSprites - same reason, and the two lists are always grown and cleared
        /// together.</summary>
        private readonly List<Color> outsidePreviewBaseColors = new List<Color>();

        /// <summary>Whether the overhang sprite at the same index breathes. The retro falling
        /// PIECE is the one thing drawn in this layer that does not: see PaintPreviewCell.</summary>
        private readonly List<bool> outsidePreviewBreathes = new List<bool>();

        /// <summary>Which in-grid preview cells breathe, by the same rule.</summary>
        private bool[,] previewBreathes;
        private CubeKind?[,] kindCache;
        private Color[,] baseColorCache;
        private readonly List<SpriteRenderer> ghostSprites = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> outsidePreviewSprites = new List<SpriteRenderer>();
        private readonly List<GameObject> infectionMarkers = new List<GameObject>();

        private InfectionCoreView infectionCores;

        private QuarantineFieldView quarantineField;

        private CircuitHeatFx circuitHeat;

        private CreatureNestView creatureNest;

        private InvaderColumnView invaderColumn;

        private CircuitTraceView circuitTrace;

        private CircuitOverloadView circuitOverload;

        /// <summary>Nodes of "Devre"'s traced circuit, redrawn whenever the route changes.</summary>
        private readonly List<GameObject> circuitMarkers = new List<GameObject>();

        /// <summary>"Matruşka"'s dolls, redrawn whenever one splits or moves.</summary>
        private readonly List<GameObject> dollMarkers = new List<GameObject>();

        /// <summary>"Kütleçekim merkezi"'s arrows, shown only while gravity is NOT pointing
        /// down - normal gravity needs no explaining.</summary>
        private readonly List<GameObject> gravityMarkers = new List<GameObject>();

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

        /// <summary>Cells whose own cube the circuit effect has taken over the drawing of. In a
        /// real turn Core has already destroyed them and the board is empty there anyway; in the
        /// ANIMATION LAB nothing was destroyed, so without this the lab draws a copy on top of the
        /// player's actual block, breaks the copy, and leaves the original sitting there - which
        /// looks exactly like the effect skipping their block.</summary>
        private readonly List<GridPos> circuitHiddenCells = new List<GridPos>();

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

        /// <summary>Sets the arena's lines burning. 0 puts them back to the plain grid; higher
        /// levels pulse faster and further. Pushed from the same place the fire is, so the two
        /// can never disagree about whether the round is in overtime.</summary>
        public void SetOvertimeGlow(int level)
        {
            if (level == overtimeLevel)
            {
                return;
            }
            overtimeLevel = Mathf.Max(0, level);
            overtimeClock = 0f;
            if (overtimeLevel == 0)
            {
                LineGlow.SetGlow(0, 0f);
            }
        }

        /// <summary>The pulse itself. Kept out of the blind-mode branch below on purpose: a round
        /// played in the dark still has a threshold to be past, and the grid is not a cube.</summary>
        private void PulseOvertimeLines()
        {
            if (overtimeLevel <= 0)
            {
                return;
            }
            float depth = Mathf.Clamp01(overtimeLevel / (float)OvertimeFullLevel);
            float period = Mathf.Lerp(OvertimePulseSlow, OvertimePulseFast, depth);
            overtimeClock += Time.deltaTime;
            // Cosine rather than a sawtooth: the lines have to swell and settle, and a linear
            // ramp back to dark reads as a strobe.
            float wave = 0.5f - 0.5f * Mathf.Cos(overtimeClock / period * 2f * Mathf.PI);
            LineGlow.SetGlow(overtimeLevel, wave);
        }

        /// <summary>The board's PHYSICAL answer to the overtime pressure: a squeeze and a knock.
        /// Both are tiny by design - the player must never be able to say the arena got smaller,
        /// only feel that something pressed on it.
        ///
        /// Scaled about the BOARD'S OWN CENTRE, not this transform's origin. Everything here is
        /// laid out in local space around `center`, so a plain scale would swing the whole arena
        /// towards the origin instead of squeezing it where it stands; the offset undoes that.</summary>
        public void SetPressure(float squeeze, Vector2 knock)
        {
            float s = 1f - Mathf.Clamp(squeeze, 0f, 0.25f);
            transform.localScale = new Vector3(s, s, 1f);
            transform.localPosition = new Vector3(
                pressureCentre.x * (1f - s) + knock.x,
                pressureCentre.y * (1f - s) + knock.y, 0f);
        }

        /// <summary>Where the arena stands, kept for SetPressure to squeeze about.</summary>
        private Vector2 pressureCentre;

        /// <summary>The glow layer, made on first use AND whenever it has gone. It is a child of
        /// this transform like everything else here, so Rebuild takes it - and a replacement is
        /// no use until it has been given the board's geometry again, which is why that is
        /// cached above and handed straight back here. Without this the pulse dies the first
        /// time the board is rebuilt and never returns.</summary>
        /// <summary>The surface layer, made on first use. Kept out of the rebuild sweep for the
        /// same reason the glow is - see the loop in Rebuild.</summary>
        private BoardSurfaceView Surface
        {
            get
            {
                if (surface == null)
                {
                    var go = new GameObject("Surface");
                    go.transform.SetParent(transform, false);
                    surface = go.AddComponent<BoardSurfaceView>();
                }
                return surface;
            }
        }

        private BoardLineGlowView LineGlow
        {
            get
            {
                if (lineGlow == null)
                {
                    var go = new GameObject("LineGlow");
                    go.transform.SetParent(transform, false);
                    lineGlow = go.AddComponent<BoardLineGlowView>();
                    if (glowCellsWide > 0 && glowCellsHigh > 0)
                    {
                        lineGlow.Build(glowCenter, glowCellsWide, glowCellsHigh, glowCellSize,
                            BorderOverhang);
                    }
                }
                return lineGlow;
            }
        }

        private void Update()
        {
            // The circuit effect draws its own copies of the cubes it owns; the moment it lets go,
            // the board takes its own back. Polled rather than pushed because the effect ends on
            // its own clock, not on anything the board is told about.
            if (circuitHiddenCells.Count > 0 && (circuitHeat == null || !circuitHeat.Active))
            {
                RestoreCircuitCells();
            }
            PulseOvertimeLines();
            if (board == null || kindCache == null)
            {
                return;
            }
            // The preview breathes in the DARK too. It says nothing about the board - a blind
            // preview is one neutral colour on the cells the block would cover - so there is
            // nothing here for the blindness to protect, and freezing it would be the one dead
            // thing on a screen the player is being asked to feel their way across.
            BreathePreview();
            if (dark)
            {
                // Blind: no idle animation may run, or a flickering fire would give away a
                // cube the player is not allowed to see. Only the blast light moves.
                BurnDownLight();
            }
            else if (!animatingWater)
            {
                AnimateElementCubes(); // would fight the fall animation's cell painting
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

        /// <summary>0 when the round is inside its threshold. Otherwise the overtime level,
        /// which sets both how fast the lines pulse and how far they get.</summary>
        private int overtimeLevel;

        private float overtimeClock;

        /// <summary>The soft light over the lines - the whole overtime effect. Made on first
        /// use, and remade whenever Rebuild has taken it: see the LineGlow property.</summary>
        private BoardLineGlowView lineGlow;

        /// <summary>The generated plate the cells sit in. Like the glow, it outlives a rebuild:
        /// its texture costs real time to make and depends only on the cell COUNT.</summary>
        private BoardSurfaceView surface;

        /// <summary>The geometry the glow was last built for, kept so a REMADE one can be given
        /// it back. Rebuild destroys the glow along with every other child of this transform,
        /// and Destroy only takes effect at the end of the frame - so the Build call inside
        /// Rebuild lands on the object that is already on its way out, and the replacement the
        /// getter makes next frame has never been built at all. That is what silently killed the
        /// pulse the first time a block was placed: lastMainBoardSize starts at -1, so the first
        /// turn always rebuilds, and from then on SetGlow was talking to a component with no
        /// sprite. Cells is 0 until the first real build, which is what makes this safe to read
        /// before there is a board.</summary>
        private Vector2 glowCenter;

        private int glowCellsWide;

        private int glowCellsHigh;

        private float glowCellSize;

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
            circuitHiddenCells.Clear(); // the renderers below are rebuilt enabled anyway
            // EXCEPT the overtime glow, which outlives a rebuild. It is not part of the board's
            // contents: it is a light over them, its texture costs real time to generate, and
            // sweeping it up with everything else meant the overtime effect was torn down and
            // rebuilt every time a block landed - which is precisely what the visible hitch was.
            Transform keepGlow = lineGlow != null ? lineGlow.transform : null;
            Transform keepSurface = surface != null ? surface.transform : null;
            Transform keepInfection = infectionCores != null
                ? infectionCores.transform : null;
            Transform keepCircuit = circuitTrace != null ? circuitTrace.transform : null;
            Transform keepOverload = circuitOverload != null
                ? circuitOverload.transform : null;
            // The containment field is a layer over the board rather than part of its contents,
            // and its whole texture would have to be recomputed on every placement otherwise.
            Transform keepQuarantine = quarantineField != null
                ? quarantineField.transform : null;
            Transform keepHeat = circuitHeat != null ? circuitHeat.transform : null;
            // The nest is a habitat over the board, not part of its contents, and rebuilding its
            // distance field on every placement would be pure waste.
            Transform keepNest = creatureNest != null ? creatureNest.transform : null;
            Transform keepLane = invaderColumn != null ? invaderColumn.transform : null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == keepGlow || child == keepSurface
                    || child == keepInfection || child == keepCircuit
                    || child == keepOverload || child == keepQuarantine
                    || child == keepHeat || child == keepNest || child == keepLane)
                {
                    continue;
                }
                Destroy(child.gameObject);
            }
            ghostSprites.Clear();
            outsidePreviewSprites.Clear();
            outsidePreviewBaseColors.Clear();
            outsidePreviewBreathes.Clear();
            infectionMarkers.Clear();
            deadZoneLine = null; // destroyed with the other children above; redrawn by SetDeadZone
            board = newBoard;
            cellSize = Mathf.Min(maxWorldSize / board.Width, maxWorldSize / board.Height);
            bottomLeft = center - new Vector2(board.Width, board.Height) * (cellSize * 0.5f);

            // The board's surface is GENERATED - a plate with a bevelled frame and a recess per
            // cell - rather than the flat rectangle this used to be. See BoardSurfaceView.
            Surface.Build(center, board.Width, board.Height, cellSize, BorderOverhang);
            pressureCentre = center;
            glowCenter = center;
            glowCellsWide = board.Width;
            glowCellsHigh = board.Height;
            glowCellSize = cellSize;
            LineGlow.Build(center, board.Width, board.Height, cellSize, BorderOverhang);

            cellRenderers = new SpriteRenderer[board.Width, board.Height];
            previewRenderers = new SpriteRenderer[board.Width, board.Height];
            previewBaseColors = new Color[board.Width, board.Height];
            previewBreathes = new bool[board.Width, board.Height];
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
                    // "Tılsım" bonus ground: playable, but no line waits for it. An optional
                    // cell that looked like an ordinary one would make the fullness rule read
                    // as a bug, so empty bonus ground is tinted - the same argument the erosion
                    // scar and the rot-dead line already make.
                    if (!cube.HasValue && board.IsOptional(gp))
                    {
                        color = BonusGroundColor;
                    }
                    if (IsQuarantined(gp))
                    {
                        color = Drained(color);
                    }
                    // "Kangren": a line the rot took WHOLE can never explode again, which the
                    // player has to be able to see - an unexplodable full line otherwise reads as
                    // a bug. Washed like the erosion scar it behaves like.
                    if (board.RowIsInfectionDead(gp.Y) || board.ColumnIsInfectionDead(gp.X))
                    {
                        color = Color.Lerp(color, RotDeadLineColor, cube.HasValue ? 0.4f : 0.66f);
                    }
                    // NOT painted here, on purpose: "Besleme"'s creature patch and "İstilacı"'s
                    // doomed column both draw themselves in their own views (CreatureNestView,
                    // InvaderColumnView), over the top of this one. Washing the cells here as
                    // well would double the tint and fight the corridor's own escalation.
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

        private static readonly Color InfectionGreen = new Color(0.2f, 0.95f, 0.35f);

        /// <summary>The infected cells, as the rules currently see them. The drawing is
        /// InfectionCoreView's business: a living core inside each cell rather than the flat
        /// green tint and the three pips this used to hang under it.</summary>
        public void ShowInfections(IReadOnlyList<InfectedCell> cells)
        {
            EnsureInfectionCores();
            if (infectionCores != null)
            {
                infectionCores.SetCells(cells, board, CellToWorld, cellSize);
            }
        }

        /// <summary>The cores live on their own object so a Rebuild does not take them with the
        /// cells - the same arrangement the surface and the line glow use.</summary>
        private void EnsureInfectionCores()
        {
            if (infectionCores != null)
            {
                return;
            }
            var go = new GameObject("InfectionCores");
            go.transform.SetParent(transform, false);
            infectionCores = go.AddComponent<InfectionCoreView>();
            infectionCores.Build(cellSize);
        }

        /// <summary>Blows the circuit: the overload runs down the cable, tiled end to end, and
        /// the cable burns away behind it. Returns how long the whole thing takes.</summary>
        public float DetonateCircuit()
        {
            if (circuitTrace == null || circuitTrace.RouteLength <= 0f)
            {
                return 0f;
            }
            if (circuitOverload == null)
            {
                var go = new GameObject("CircuitOverload");
                go.transform.SetParent(transform, false);
                circuitOverload = go.AddComponent<CircuitOverloadView>();
            }
            circuitOverload.Play(circuitTrace.Route, circuitTrace.RouteAt,
                circuitTrace.RouteLength, circuitTrace.CellSize);
            // The cable goes out behind the failure rather than after it, so the two are one
            // event: a tile bursts and the cable it was on stops being there.
            circuitTrace.BurnAway(CircuitOverloadView.Style.TravelSeconds + 0.35f);
            return CircuitOverloadView.Duration;
        }

        /// <summary>The charge before an infected block is taken. Returns how long it runs, so
        /// the caller can hand off to the blast when it is done.</summary>
        public float PlayInfectionCharge(GridPos cell)
        {
            return infectionCores != null ? infectionCores.PlayCharge(cell) : 0f;
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
            // Only rebuild when the REGION actually changed. This is called on every refresh, and
            // the nest's distance field and speck pool are not free.
            bool same = cells != null && cells.Count == creatureCells.Count;
            if (same)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    if (!cells[i].Equals(creatureCells[i]))
                    {
                        same = false;
                        break;
                    }
                }
            }
            if (same)
            {
                // The REGION has not moved, but what stands on it may have. The habitat has to
                // duck under a block the turn it lands, not the next time the joker moves.
                if (creatureNest != null)
                {
                    creatureNest.RefreshOccupancy(IsOccupied);
                }
                return;
            }
            creatureCells.Clear();
            if (cells != null)
            {
                creatureCells.AddRange(cells);
            }
            if (board == null)
            {
                return;
            }
            if (creatureCells.Count == 0)
            {
                if (creatureNest != null)
                {
                    creatureNest.Clear();
                }
                return;
            }
            EnsureCreatureNest();
            creatureNest.SetRegion(creatureCells, board, CellToWorld, cellSize, IsOccupied);
            creatureNest.RefreshOccupancy(IsOccupied);
        }

        /// <summary>A cube was eaten inside the nest. The pool answers - the view decides for
        /// itself whether the cell was in the region, so callers need not know the joker exists.</summary>
        public void PlayCreatureFeed(GridPos cell)
        {
            if (creatureNest != null && IsCreature(cell))
            {
                creatureNest.PlayFeed(CellToWorld(cell));
            }
        }

        private void EnsureCreatureNest()
        {
            if (creatureNest == null)
            {
                var go = new GameObject("CreatureNest");
                go.transform.SetParent(transform, false);
                creatureNest = go.AddComponent<CreatureNestView>();
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

        /// <summary>Hands "Karantina"'s sealed lines to the containment field. Pass nulls to
        /// clear them. The field works out for itself which lines are new and which board edge
        /// each one came in from, so this is only ever told the current state.</summary>
        public void ShowQuarantine(IReadOnlyList<int> rows, IReadOnlyList<int> columns)
        {
            quarantinedRows.Clear();
            quarantinedColumns.Clear();
            if (rows != null) { quarantinedRows.AddRange(rows); }
            if (columns != null) { quarantinedColumns.AddRange(columns); }
            if (board == null)
            {
                return;
            }
            if (quarantineField == null)
            {
                var go = new GameObject("QuarantineField");
                go.transform.SetParent(transform, false);
                quarantineField = go.AddComponent<QuarantineFieldView>();
            }
            quarantineField.SetLines(quarantinedRows, quarantinedColumns, board, CellToWorld,
                cellSize, IsOccupied);
        }

        private bool IsOccupied(GridPos cell)
        {
            return board != null && board.IsInside(cell) && board.GetCube(cell).HasValue;
        }

        /// <summary>
        /// Holds copies of the cubes a breaking circuit is about to take, and cooks them for
        /// <paramref name="seconds"/>. They are copies because Core already destroyed the real
        /// ones and the board has been redrawn without them - the tile and colour come out of the
        /// destruction log, which carries the whole Cube, so what the player sees heat up is
        /// exactly what was standing there.
        /// </summary>
        public void PlayCircuitHeat(IReadOnlyList<DestroyedCube> cubes, float seconds)
        {
            if (cubes == null || cubes.Count == 0 || board == null)
            {
                return;
            }
            EnsureCircuitHeat();
            HideForCircuit(cubes);
            circuitHeat.Play(CircuitGhosts(cubes), cellSize, seconds);
        }

        /// <summary>Parks copies of those cubes on the board without cooking them - what the
        /// circuit looks like with blocks under it, before anything happens to either.</summary>
        public void HoldCircuitBlocks(IReadOnlyList<DestroyedCube> cubes)
        {
            if (cubes == null || cubes.Count == 0 || board == null)
            {
                return;
            }
            EnsureCircuitHeat();
            HideForCircuit(cubes);
            circuitHeat.Hold(CircuitGhosts(cubes), cellSize);
        }

        /// <summary>Takes the parked blocks back off the board.</summary>
        public void ClearCircuitBlocks()
        {
            if (circuitHeat != null)
            {
                circuitHeat.Stop();
            }
            RestoreCircuitCells();
        }

        private void HideForCircuit(IReadOnlyList<DestroyedCube> cubes)
        {
            RestoreCircuitCells();
            for (int i = 0; i < cubes.Count; i++)
            {
                GridPos p = cubes[i].Pos;
                int cx = p.X - board.MinX;
                int cy = p.Y - board.MinY;
                if (cx < 0 || cy < 0 || cx >= cellRenderers.GetLength(0)
                    || cy >= cellRenderers.GetLength(1))
                {
                    continue;
                }
                SpriteRenderer r = cellRenderers[cx, cy];
                if (r != null && r.enabled)
                {
                    r.enabled = false;
                    circuitHiddenCells.Add(p);
                }
            }
        }

        private void RestoreCircuitCells()
        {
            for (int i = 0; i < circuitHiddenCells.Count; i++)
            {
                GridPos p = circuitHiddenCells[i];
                int cx = p.X - board.MinX;
                int cy = p.Y - board.MinY;
                if (cx >= 0 && cy >= 0 && cx < cellRenderers.GetLength(0)
                    && cy < cellRenderers.GetLength(1) && cellRenderers[cx, cy] != null)
                {
                    cellRenderers[cx, cy].enabled = true;
                }
            }
            circuitHiddenCells.Clear();
        }

        private void EnsureCircuitHeat()
        {
            if (circuitHeat == null)
            {
                var go = new GameObject("CircuitHeat");
                go.transform.SetParent(transform, false);
                circuitHeat = go.AddComponent<CircuitHeatFx>();
            }
        }

        private List<CircuitHeatFx.Ghost> CircuitGhosts(IReadOnlyList<DestroyedCube> cubes)
        {
            var ghosts = new List<CircuitHeatFx.Ghost>(cubes.Count);
            for (int i = 0; i < cubes.Count; i++)
            {
                Cube cube = cubes[i].Cube;
                Sprite tile = ViewUtil.CubeTile(cube.Kind, CardOf(cube));
                // Which way the cable runs through THIS cell, from its neighbours on the path.
                // The heat is a band on that axis, so the block burns where the circuit touches
                // it rather than all over at once.
                GridPos prev = cubes[System.Math.Max(i - 1, 0)].Pos;
                GridPos nextCell = cubes[System.Math.Min(i + 1, cubes.Count - 1)].Pos;
                var dir = new Vector2(nextCell.X - prev.X, nextCell.Y - prev.Y);
                ghosts.Add(new CircuitHeatFx.Ghost
                {
                    World = CellToWorld(cubes[i].Pos),
                    Tile = tile,
                    Colour = ViewUtil.CubeTileColor(cube, tile),
                    Size = cellSize * CubeFill,
                    CableDir = dir.sqrMagnitude > 0.0001f ? dir : Vector2.right
                });
            }
            return ghosts;
        }

        /// <summary>A cube exploded inside a sealed zone. The mechanic charges the player for it;
        /// this is the membrane over it noticing.</summary>
        public void PlayQuarantineReaction(GridPos cell)
        {
            if (quarantineField != null && IsQuarantined(cell))
            {
                quarantineField.PlayReaction(CellToWorld(cell));
            }
        }

        /// <summary>Pulls the life out of a colour: most of the way to its own grey, then down.
        /// Hue is left alone on purpose - a drained green is still recognisably the green block,
        /// which is the difference between a zone being dangerous and a zone being unreadable.</summary>
        private static Color Drained(Color c)
        {
            float grey = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            return new Color(
                Mathf.Lerp(c.r, grey, QuarantineDrain) * QuarantineDarken,
                Mathf.Lerp(c.g, grey, QuarantineDrain) * QuarantineDarken,
                Mathf.Lerp(c.b, grey, QuarantineDrain) * QuarantineDarken,
                c.a);
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

        /// <summary>Hands "İstilacı"'s marked column to the corridor, with how many turns it has
        /// left - which is what the whole escalation is driven from. Pass null to clear it.</summary>
        public void ShowDoomedColumn(int? column, int turnsLeft)
        {
            if (board == null)
            {
                return;
            }
            if (!column.HasValue)
            {
                if (invaderColumn != null)
                {
                    invaderColumn.Clear();
                }
                return;
            }
            EnsureInvaderColumn();
            invaderColumn.Show(column, turnsLeft, board, CellToWorld, cellSize, IsOccupied);
        }

        /// <summary>The marked column came due. One band travels it and takes what it passes.</summary>
        public void PlayColumnExtraction(IReadOnlyList<DestroyedCube> taken)
        {
            if (invaderColumn == null || taken == null || taken.Count == 0)
            {
                return;
            }
            invaderColumn.PlayExtraction(taken, CellToWorld,
                cube => ViewUtil.CubeTile(cube.Kind, CardOf(cube)),
                (cube, tile) => ViewUtil.CubeTileColor(cube, tile));
        }

        private void EnsureInvaderColumn()
        {
            if (invaderColumn == null)
            {
                var go = new GameObject("InvaderColumn");
                go.transform.SetParent(transform, false);
                invaderColumn = go.AddComponent<InvaderColumnView>();
            }
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

        /// <summary>The circuit path, IN ORDER. The drawing is CircuitTraceView's business: one
        /// continuous trace routed between two terminals, rather than the pale square this used
        /// to drop in the middle of every path cell.</summary>
        public void ShowCircuit(IReadOnlyList<GridPos> cells)
        {
            EnsureCircuitTrace();
            if (circuitTrace != null)
            {
                circuitTrace.Show(cells, board, CellToWorld, cellSize);
            }
        }

        private void EnsureCircuitTrace()
        {
            if (circuitTrace != null)
            {
                return;
            }
            var go = new GameObject("CircuitTrace");
            go.transform.SetParent(transform, false);
            circuitTrace = go.AddComponent<CircuitTraceView>();
        }

        public void ClearInfections()
        {
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

        /// <summary>Where a shape must START so that the CURSOR sits at the middle of it. The
        /// block follows the pointer instead of hanging off one side of it, which is the whole
        /// point: anchoring off a cell index alone put every EVEN-sized block (a 2x2, a 4-long
        /// bar) to the right of the cursor, because the half-cell it owed could not be paid in
        /// whole cells.
        ///
        /// So the world point is used AS IT IS, sub-cell part included, and only the finished
        /// answer is rounded. Odd widths land on the hovered cell exactly as before; an even one
        /// straddles the cursor and flips to whichever side is nearer as it crosses the middle of
        /// a cell. It is the shape's BOUNDING BOX that is centred - the same box CardVisual
        /// centres its mini-block in - so the preview sits under the dragged card, and a rotation
        /// cannot make the anchor wander the way a filled-cell centroid would.
        ///
        /// Unclamped on purpose: an origin off the board is a placement the engine refuses, which
        /// is exactly the red preview the player should see. Callers gate on TryWorldToCell(Loose)
        /// for "is the cursor over the arena at all".</summary>
        public GridPos WorldToCenteredOrigin(Vector2 world, int shapeWidth, int shapeHeight)
        {
            if (board == null)
            {
                return default(GridPos);
            }
            float x = (world.x - bottomLeft.x) / cellSize - shapeWidth * 0.5f;
            float y = (world.y - bottomLeft.y) / cellSize - shapeHeight * 0.5f;
            // FloorToInt(v + 0.5) rather than RoundToInt: the latter rounds a .5 to the nearest
            // EVEN number, which would make the flip point of an even-sized block jump about
            // depending on where it is on the board.
            return new GridPos(
                board.MinX + Mathf.FloorToInt(x + 0.5f),
                board.MinY + Mathf.FloorToInt(y + 0.5f));
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
                // Outside the grid this paints a temporary overhang sprite instead - a ghost
                // block hanging off the edge breathes with the rest of its own preview.
                PaintPreviewCell(origin + offset, color, true);
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
                PaintPreviewCell(pos, ExplosionPreviewColor, true);
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
                if (board.IsInside(cells[i]))
                {
                    PaintPreviewCell(cells[i], ExplosionPreviewColor, true);
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
                // The LANDING GHOST is a preview - where the piece would come to rest - so it
                // breathes with every other preview in the game.
                PaintPreviewCell(ghostOrigin + offset, FallingGhostColor, true);
            }
            foreach (GridPos offset in shape.Cells)
            {
                // The PIECE ITSELF does not. It is drawn in the preview layer for convenience,
                // but it is the block the player is steering, not an answer about it, and a
                // block that pulsed while it fell would read as a warning.
                PaintPreviewCell(currentOrigin + offset, FallingPieceColor, false);
            }
        }

        /// <summary>Tints one preview cell: in-grid cells use the persistent renderers; a cell
        /// outside the grid (a piece still in the air, or a ghost block hanging off the edge)
        /// gets a temporary overhang sprite. The colour is remembered as PAINTED and the breath
        /// is laid on top of it, here and again every frame in BreathePreview.</summary>
        private void PaintPreviewCell(GridPos pos, Color color, bool breathes)
        {
            float wave = PreviewBreath();
            if (board.IsInside(pos))
            {
                int lx = pos.X - board.MinX;
                int ly = pos.Y - board.MinY;
                previewBaseColors[lx, ly] = color;
                previewBreathes[lx, ly] = breathes;
                previewRenderers[lx, ly].color = breathes ? Breathed(color, wave) : color;
                previewRenderers[lx, ly].enabled = true;
            }
            else
            {
                outsidePreviewSprites.Add(ViewUtil.MakeCell(transform, "PreviewGhost",
                    CellToWorld(pos), cellSize * 0.92f,
                    breathes ? Breathed(color, wave) : color, 2));
                outsidePreviewBaseColors.Add(color);
                outsidePreviewBreathes.Add(breathes);
            }
        }

        /// <summary>Where the breath is in its cycle right now, 0 (smallest) to 1 (fullest).
        ///
        /// Driven by the GLOBAL clock rather than a timer started when the preview appeared, and
        /// that is the whole reason it works: a preview is torn down and repainted from scratch
        /// every single frame the cursor moves, so a phase that belonged to the preview would
        /// restart forever and never leave the beginning of its own breath. Off the shared clock
        /// it is STEADY - the highlight keeps breathing at the same rate no matter how the block
        /// is dragged, and every cell of it is on the same beat.</summary>
        private static float PreviewBreath()
        {
            return 0.5f - 0.5f * Mathf.Cos(Time.time / PreviewBreathPeriod * 2f * Mathf.PI);
        }

        /// <summary>One flat preview square at the given point of its breath: the colour dimmed
        /// and the square thickened together, so the bottom of the breath is darker than the
        /// painted colour over an empty cell AND over a cube (see PreviewBreathDimAlpha).
        /// Nothing is added to the square but its own strength - this board has no glows, and a
        /// preview that grew a halo would look imported.</summary>
        private static Color Breathed(Color color, float wave)
        {
            float k = Mathf.Lerp(PreviewBreathDim, PreviewBreathLift, wave);
            return new Color(
                Mathf.Min(1f, color.r * k),
                Mathf.Min(1f, color.g * k),
                Mathf.Min(1f, color.b * k),
                Mathf.Clamp01(color.a * Mathf.Lerp(PreviewBreathDimAlpha, 1f, wave)));
        }

        /// <summary>Re-lays the breath over whatever is previewed right now, from the PAINTED
        /// colours. Runs every frame because a preview that is not being moved is not repainted -
        /// the pad's selection sits still, the animation lab holds one preview open indefinitely -
        /// and those are exactly the moments the breathing is there to fill.</summary>
        private void BreathePreview()
        {
            if (previewRenderers == null)
            {
                return;
            }
            float wave = PreviewBreath();
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (previewRenderers[x, y].enabled && previewBreathes[x, y])
                    {
                        previewRenderers[x, y].color = Breathed(previewBaseColors[x, y], wave);
                    }
                }
            }
            for (int i = 0; i < outsidePreviewSprites.Count; i++)
            {
                if (outsidePreviewSprites[i] != null && outsidePreviewBreathes[i])
                {
                    outsidePreviewSprites[i].color = Breathed(outsidePreviewBaseColors[i], wave);
                }
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
            outsidePreviewBaseColors.Clear();
            outsidePreviewBreathes.Clear();
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
                BlankCell(cell);
            }
        }

        /// <summary>Draws a cell as EMPTY, tile and all. Recolouring alone is not enough any
        /// more: Refresh gives an occupied cell a painted tile sprite, and a tile tinted
        /// EmptyColor is still a fully visible cube - which is how a falling water cube used to
        /// be shown twice at once, in flight AND already settled at the cell it was heading for.
        /// </summary>
        private void BlankCell(GridPos pos)
        {
            if (board == null || !board.IsInside(pos))
            {
                return;
            }
            SpriteRenderer renderer = cellRenderers[pos.X - board.MinX, pos.Y - board.MinY];
            ViewUtil.ApplyTile(renderer, null, cellSize * EmptyFill);
            renderer.color = board.IsSealed(pos) ? SealedColor : EmptyColor;
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
