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

        /// <summary>What an empty cell looks like, for an effect that hands a cell back to the
        /// board and must do it without a seam (ColdSinkView closing its pit).</summary>
        public static Color EmptySlotColor
        {
            get { return EmptyColor; }
        }

        /// <summary>A cell shuffle erosion ATE. Deliberately not hidden like an ordinary hole:
        /// the player has to see what the stalling cost them, and that its row/column is dead.</summary>
        private static readonly Color DeadColor = new Color(0.30f, 0.10f, 0.12f, 0.85f);

        /// <summary>A DEAD-ZONE cell (shuffle erosion): still play area, but any row or column
        /// touching it scores nothing. The eroded scar's hue, darker and fully opaque, so it reads
        /// as ground you can still stand on rather than as a hole.</summary>
        private static readonly Color BlightColor = new Color(0.22f, 0.075f, 0.09f);

        /// <summary>An empty cell a boss has sealed off ("Mapus") - it reads as barred, not
        /// as a cube, because nothing can be placed there but nothing occupies it either.
        /// Deliberately COLD, not another scar red: an eaten cell (DeadColor) is gone for good
        /// and kills its line, while a seal lifts again next turn. The two can sit on the same
        /// board, so they must not look alike.</summary>
        /// <summary>What a sealed cell's own plate is under everything MapusSealView draws on it.
        /// It used to be the entire visual language of the boss - one blue-grey square - and it is
        /// now only what shows through while the pit is still sinking, so it is a DARKENING rather
        /// than a colour: a cell somebody painted blue is exactly what the seal replaced.</summary>
        private static readonly Color SealedColor = new Color(0.075f, 0.078f, 0.105f);

        /// <summary>Empty BONUS ground ("Tılsım"): free to build on, and no line waits for it.
        /// Warm and faint - a gift, not a wound - so it cannot be mistaken for the eroded cell
        /// (DeadColor) or the barred one (SealedColor) it may sit beside.</summary>
        /// <summary>BONUS GROUND ("Tılsım"): smoked jade-slate, a shade warmer and lighter than
        /// the board rather than a different colour on it. What actually says "this is bonus
        /// ground" is TalismanView's four OPEN corner runes - a cell whose structural frame is not
        /// closed, because no line waits for it. This plate is only what they sit on, and it used
        /// to be the entire language: one flat olive square.</summary>
        private static readonly Color BonusGroundColor = new Color(0.135f, 0.165f, 0.163f);
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

        /// <summary>"İstilacı"'s marked column: the demolition wash. Deliberately its own colour -
        /// a marked column is neither sealed (a seal lifts next turn) nor eaten (that is
        /// permanent); it is a place with a deadline on it.</summary>
        /// <summary>"Kütleçekim merkezi"'s pull markers, in water's own blue - what they are
        /// telling you about is where the WATER goes, and nothing else.</summary>

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

        /// <summary>How long a blast keeps its surroundings lit, and how far the light reaches.
        /// The radius is generous on purpose: a blind round is meant to be READ from what you
        /// blow up, and a light that only reached its own neighbours told the player almost
        /// nothing for the trouble of clearing a line.</summary>
        private const float LightSeconds = 1.1f;
        private const int LightRadius = 4;

        /// <summary>What a PLACEMENT lights, as against a blast: one cell out, and a fraction as
        /// long. Setting a block down disturbs the dark around your own hand - just enough to
        /// confirm what you touched and to hint at what it landed against - without turning
        /// walking a block across the arena into a way to survey it. Blowing something up is
        /// still the only way to actually see.</summary>
        private const int PlacementLightRadius = 1;
        private const float PlacementLightStrength = 0.45f;

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

        /// <summary>What a cube looked like on the repaint that emptied its cell, and when. An
        /// explosion plays a moment AFTER that repaint, and this is what lets it break the cube
        /// that was there instead of an empty slot. Keyed by absolute cell, so a rebuild of the
        /// arrays cannot lose it; overwritten the moment a cube stands there again.</summary>
        private readonly Dictionary<GridPos, VacatedCube> vacated =
            new Dictionary<GridPos, VacatedCube>();

        private struct VacatedCube
        {
            public Sprite Tile;
            public Color Colour;
            public float At;
        }
        private readonly List<SpriteRenderer> ghostSprites = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> outsidePreviewSprites = new List<SpriteRenderer>();
        private readonly List<GameObject> infectionMarkers = new List<GameObject>();

        private InfectionCoreView infectionCores;

        private InfectionBurstView infectionBurst;

        private QuarantineFieldView quarantineField;

        private CircuitHeatFx circuitHeat;

        private CreatureNestView creatureNest;

        private InvaderColumnView invaderColumn;

        /// <summary>"Kangren"'s dead tissue, its dead-line bands and its turn animations. Like the
        /// nest, it is a layer over the board rather than part of its contents - a dead line has to
        /// outlive every repaint and every rebuild.</summary>
        private GangreneView gangrene;

        /// <summary>"Yılan": the snake DRAWS ITSELF from the six pieces painted for it, so the
        /// board leaves its cells blank (see Refresh) and this layer puts the snake in them. Like
        /// the rot, it outlives a repaint and a rebuild - the snake is not board contents, it is a
        /// thing standing on the board.</summary>
        private SnakeView snake;

        private CompressedCubeView press;

        private IceFreezeView ice;

        private QuakeCollapseView quake;

        private ParasiteHostView parasite;

        private MapusSealView mapus;

        private TalismanView talisman;

        private FireSpreadView fireSpread;

        private CircuitTraceView circuitTrace;

        private CircuitOverloadView circuitOverload;

        /// <summary>Nodes of "Devre"'s traced circuit, redrawn whenever the route changes.</summary>
        private readonly List<GameObject> circuitMarkers = new List<GameObject>();

        /// <summary>"Matruşka"'s dolls - resting on their cubes and staged when they split. A layer over
        /// the board rather than part of its contents, so it survives a rebuild.</summary>
        private MatryoshkaView matryoshka;

        /// <summary>"Kütleçekim merkezi"'s field, shown only while gravity is NOT pointing
        /// down - normal gravity needs no explaining. See GravityFieldView.</summary>
        private GravityFieldView gravityField;

        /// <summary>"Karantina"'s sealed rows and columns, in absolute board coordinates.</summary>
        private readonly List<GridPos> quarantinedCells = new List<GridPos>();

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
        /// <summary>
        /// Cells whose cube the BOARD has stopped drawing because an effect is drawing its own
        /// copy instead. Shared by every effect that borrows a cube rather than owned by one:
        /// the circuit cooks blocks, the invader's column pulls them out, and both need the
        /// board to stand back for exactly as long as they are holding them.
        ///
        /// In a real round the cube is already destroyed and this changes nothing. It is the
        /// ANIMATION LAB it exists for - there nothing has been destroyed, so without it the
        /// effect's copy is drawn on top of a cube that never leaves.
        /// </summary>
        private readonly List<GridPos> borrowedCells = new List<GridPos>();

        /// <summary>Cells a moving-cube effect (BossMoveView) is drawing its travelling copies into.
        /// Blanked while it holds them - and again after every repaint in the meantime, as the
        /// water's are - so the cube already standing at its new cell never shows under the copy
        /// still on its way there.</summary>
        private readonly HashSet<GridPos> heldCells = new HashSet<GridPos>();

        /// <summary>Blanks these cells until ReleaseCells gives them back.</summary>
        public void HoldCells(IEnumerable<GridPos> cells)
        {
            if (board == null || cells == null)
            {
                return;
            }
            foreach (GridPos cell in cells)
            {
                if (board.IsInside(cell) && heldCells.Add(cell))
                {
                    BlankHeld(cell);
                }
            }
        }

        /// <summary>Gives held cells back, repainted as they really are.</summary>
        public void ReleaseCells(IEnumerable<GridPos> cells)
        {
            if (cells == null)
            {
                return;
            }
            bool any = false;
            foreach (GridPos cell in cells)
            {
                any |= heldCells.Remove(cell);
            }
            if (any)
            {
                Refresh();
            }
        }

        private void BlankHeld(GridPos cell)
        {
            BlankCell(cell);
            // Nor may the element pulse paint a cube back in (AnimateElementCubes reads this).
            kindCache[cell.X - board.MinX, cell.Y - board.MinY] = null;
        }

        // ---- "Kangren" ---------------------------------------------------------------------

        /// <summary>
        /// The rot's own layer: dead tissue on every rotten cube, a band under every line it took
        /// whole, and a turn's spread, deaths and jumps. Made on first use and kept through a
        /// rebuild (see Rebuild), because a dead line lasts the round.
        /// </summary>
        public GangreneView Gangrene
        {
            get
            {
                if (gangrene == null)
                {
                    var go = new GameObject("Gangrene");
                    go.transform.SetParent(transform, false);
                    gangrene = go.AddComponent<GangreneView>();
                }
                return gangrene;
            }
        }

        /// <summary>Ends anything the rot is playing, without making the view if there is none.</summary>
        public void StopGangrene()
        {
            if (gangrene != null)
            {
                gangrene.Stop();
            }
        }

        /// <summary>"Yılan"'s own layer: the six pieces, its slides, its bites and its cuts. Made on
        /// first use and kept through a rebuild, like the rot.</summary>
        public SnakeView Snake
        {
            get
            {
                if (snake == null)
                {
                    var go = new GameObject("Snake");
                    go.transform.SetParent(transform, false);
                    snake = go.AddComponent<SnakeView>();
                }
                return snake;
            }
        }

        /// <summary>Ends anything the snake is playing, without making the view if there is none.</summary>
        public void StopSnake()
        {
            if (snake != null)
            {
                snake.Stop();
            }
        }

        /// <summary>"Hidrolik pres"'s own layer: the compressed cube's shell, its contained-pressure
        /// idle and its turn countdown. Made on first use and kept through a rebuild, like the rot
        /// and the snake - a press stands for four turns and the board is repainted many times in
        /// them. It draws OVER the board's own slate cube rather than replacing it, so without its
        /// shader the press is still a readable block.</summary>
        public CompressedCubeView Press
        {
            get
            {
                if (press == null)
                {
                    var go = new GameObject("Press");
                    go.transform.SetParent(transform, false);
                    press = go.AddComponent<CompressedCubeView>();
                }
                return press;
            }
        }

        /// <summary>Takes every press plate down, without making the view if there is none.</summary>
        public void StopPress()
        {
            if (press != null)
            {
                press.Stop();
            }
        }

        /// <summary>"Parazit"'s own layer: the clasp on every host cube, its bonds and the joker
        /// riding in its middle. Made on first use and kept through a rebuild, like the rest - a
        /// host stands for as long as its block does.</summary>
        public ParasiteHostView Parasite
        {
            get
            {
                if (parasite == null)
                {
                    var go = new GameObject("Parasite");
                    go.transform.SetParent(transform, false);
                    parasite = go.AddComponent<ParasiteHostView>();
                }
                return parasite;
            }
        }

        /// <summary>"Mapus"'s own layer: the prison it builds in one empty cell, and the pressure
        /// that puts on the row and the column through it. Made on first use and kept through a
        /// rebuild like the rest - a seal stands for turns at a time.</summary>
        public MapusSealView Mapus
        {
            get
            {
                if (mapus == null)
                {
                    var go = new GameObject("Mapus");
                    go.transform.SetParent(transform, false);
                    mapus = go.AddComponent<MapusSealView>();
                }
                return mapus;
            }
        }

        /// <summary>"Tılsım"'s own layer: the harvest of the ghosts, the vine claim it leaves in
        /// the outside space, and the bonus ground it hands the next round. Made on first use and
        /// kept through a rebuild - and keeping it through the rebuild is the whole point here,
        /// because the one thing this power has to survive is a ROUND BOUNDARY.</summary>
        public TalismanView Talisman
        {
            get
            {
                if (talisman == null)
                {
                    var go = new GameObject("Talisman");
                    go.transform.SetParent(transform, false);
                    talisman = go.AddComponent<TalismanView>();
                }
                return talisman;
            }
        }

        /// <summary>
        /// "Yangın"'s fire front. Owned here because it borrows CELLS: it holds the ones it is
        /// lighting blank (the rules have already made them fire) and draws the transformation
        /// itself, which nothing outside the board can do.
        /// </summary>
        public FireSpreadView FireSpread
        {
            get
            {
                if (fireSpread == null)
                {
                    var go = new GameObject("FireSpread");
                    go.transform.SetParent(transform, false);
                    fireSpread = go.AddComponent<FireSpreadView>();
                }
                return fireSpread;
            }
        }

        /// <summary>Puts the fire out and gives any held cells back, without making the view if
        /// there is none.</summary>
        public void StopFireSpread()
        {
            if (fireSpread != null)
            {
                fireSpread.Stop();
            }
        }

        /// <summary>
        /// "Buzluk"'s freeze, and the ice it leaves standing. Owned here for the same reason the
        /// rot and the press are: half of it is PRESENCE. The animation plays once, but every ice
        /// cube on the board has to go on being ice through every repaint and every rebuild, and
        /// it is this view that writes that onto the board's own cell renderers.
        /// </summary>
        public IceFreezeView Ice
        {
            get
            {
                if (ice == null)
                {
                    var go = new GameObject("IceFreeze");
                    go.transform.SetParent(transform, false);
                    ice = go.AddComponent<IceFreezeView>();
                }
                return ice;
            }
        }

        /// <summary>
        /// "Deprem"'s collapse. Owned here because the tremor moves THIS transform and because
        /// the fallen cubes are drawn as proxies in the board's own space, riding the tremor with
        /// the cells they fell out of.
        /// </summary>
        public QuakeCollapseView Quake
        {
            get
            {
                if (quake == null)
                {
                    var go = new GameObject("QuakeCollapse");
                    go.transform.SetParent(transform, false);
                    quake = go.AddComponent<QuakeCollapseView>();
                }
                return quake;
            }
        }

        /// <summary>Ends the collapse and puts the arena back still, without making the view if
        /// there is none.</summary>
        public void StopQuake()
        {
            if (quake != null)
            {
                quake.Stop();
            }
        }

        /// <summary>The face a cube would be drawn with on this board: its tile and its tint.
        /// For an effect that has the CUBE but no longer has a cell showing it.</summary>
        public void CubeFace(Cube cube, out Sprite tile, out Color colour)
        {
            tile = ViewUtil.CubeTile(cube.Kind, CardOf(cube));
            colour = ViewUtil.CubeTileColor(cube, tile);
        }

        /// <summary>Puts every ice cube back to a plain renderer, without making the view if
        /// there is none.</summary>
        public void StopIceFreeze()
        {
            if (ice != null)
            {
                ice.Stop();
            }
        }

        /// <summary>
        /// The board's own renderer for a cell, for the ONE effect that paints THROUGH it rather
        /// than over it.
        ///
        /// "Buzluk" is that effect: an ice cube is the board's cube wearing the cryo material, not
        /// a second sprite laid on top - which is what lets a missing shader cost detail and never
        /// the block, and what stops the ice and the board disagreeing during a repaint. Null for
        /// a cell outside the board.
        /// </summary>
        public SpriteRenderer CellRendererAt(GridPos pos)
        {
            if (board == null || cellRenderers == null)
            {
                return null;
            }
            int x = pos.X - board.MinX;
            int y = pos.Y - board.MinY;
            if (x < 0 || y < 0 || x >= cellRenderers.GetLength(0)
                || y >= cellRenderers.GetLength(1))
            {
                return null;
            }
            return cellRenderers[x, y];
        }

        /// <summary>Takes the vines and the bonus ground down, without making the view if there is
        /// none.</summary>
        public void StopTalisman()
        {
            if (talisman != null)
            {
                talisman.Stop();
            }
        }

        /// <summary>Takes the seal down, without making the view if there is none.</summary>
        public void StopMapus()
        {
            if (mapus != null)
            {
                mapus.Stop();
            }
        }

        /// <summary>Takes every harness down, without making the view if there is none.</summary>
        public void StopParasite()
        {
            if (parasite != null)
            {
                parasite.Stop();
            }
        }

        /// <summary>True on the last repaint that drew at least one ice cube.</summary>
        private bool sawIce;

        /// <summary>True on the last repaint that drew at least one compressed cube - what decides
        /// whether the press's layer is worth asking for at all.</summary>
        private bool sawPress;

        /// <summary>True on the last repaint that left a cell blank for the snake.</summary>
        private bool sawSnake;

        /// <summary>True on the last repaint that drew at least one rotten cube - what decides
        /// whether the rot's layer is worth asking for at all.</summary>
        private bool sawRot;

        /// <summary>Each cell's colour as it was BEFORE the dead-line wash, so one cell can be
        /// repainted at a new wash without repainting the board.</summary>
        private Color[,] preWashCache;

        /// <summary>Cells whose dead-line wash is being walked in by the rot's sweep, 0..1. A cell
        /// that is not in here is washed fully, exactly as it always was.</summary>
        private readonly Dictionary<GridPos, float> rotWash = new Dictionary<GridPos, float>();

        /// <summary>
        /// How much of the dead-line wash a cell shows. The rules kill a line in one go; the sweep
        /// that puts its life out brings the wash in behind its own front, so it winds it back to
        /// nothing first and then walks it up. Cheap enough to call every frame on a whole line.
        /// </summary>
        public void SetRotWash(GridPos cell, float amount)
        {
            if (board == null || !board.IsInside(cell))
            {
                return;
            }
            amount = Mathf.Clamp01(amount);
            float now;
            if (rotWash.TryGetValue(cell, out now) && Mathf.Abs(now - amount) < 0.002f)
            {
                return;
            }
            rotWash[cell] = amount;
            RepaintWash(cell);
        }

        /// <summary>Every cell goes back to the full wash: the sweep is done, or the round is.</summary>
        public void ClearRotWash()
        {
            if (rotWash.Count == 0)
            {
                return;
            }
            rotWash.Clear();
            Refresh();
        }

        private float RotWashAt(GridPos cell)
        {
            float amount;
            return rotWash.TryGetValue(cell, out amount) ? amount : 1f;
        }

        /// <summary>One cell repainted at its current wash. Cells something else is drawing (a held
        /// cell, a water cube in flight) are left alone - they are not the board's to paint.</summary>
        private void RepaintWash(GridPos cell)
        {
            int x = cell.X - board.MinX;
            int y = cell.Y - board.MinY;
            if (cellRenderers == null || preWashCache == null || cellRenderers[x, y] == null
                || heldCells.Contains(cell) || waterHiddenCells.Contains(cell))
            {
                return;
            }
            float light = LightAt(x, y);
            if (light <= 0f)
            {
                return;
            }
            Color color = preWashCache[x, y];
            if (board.RowIsInfectionDead(cell.Y) || board.ColumnIsInfectionDead(cell.X))
            {
                color = Color.Lerp(color, RotDeadLineColor,
                    RotWashAt(cell) * (kindCache[x, y].HasValue ? 0.4f : 0.66f));
            }
            if (dark)
            {
                color = Color.Lerp(DarkCellColor, color, light);
            }
            cellRenderers[x, y].color = color;
            baseColorCache[x, y] = color;
        }

        /// <summary>How brightly a ROTTEN cube is being drawn at this cell: 0 where there is none,
        /// where an animation is holding the cell, or where the dark has it. The rot's tissue hangs
        /// on exactly this, so the overlay and the cube under it can never disagree.</summary>
        public float RotLight(GridPos cell)
        {
            if (board == null || kindCache == null || !board.IsInside(cell))
            {
                return 0f;
            }
            int x = cell.X - board.MinX;
            int y = cell.Y - board.MinY;
            if (!kindCache[x, y].HasValue || kindCache[x, y].Value != CubeKind.Gangrene)
            {
                return 0f;
            }
            return LightAt(x, y);
        }

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
            pressureScale = 1f - Mathf.Clamp(squeeze, 0f, 0.25f);
            pressureKnock = knock;
            ApplyArenaTransform();
        }

        /// <summary>
        /// "Deprem"'s tremor: the ARENA moves, a pixel or two and a fraction of a degree, while
        /// the camera, the hand and the HUD stay exactly where they are - an earthquake is
        /// something the board suffers, not something the screen does.
        ///
        /// It is a SECOND TERM on this transform rather than a second writer of it. The overtime
        /// pressure already writes the scale and the position every frame, and two systems each
        /// writing the transform end with whichever ran last winning; so both setters only record
        /// their own part and ApplyArenaTransform composes them.
        /// </summary>
        public void SetTremor(Vector2 offset, float degrees)
        {
            tremorOffset = offset;
            tremorDegrees = degrees;
            ApplyArenaTransform();
        }

        /// <summary>
        /// "Hazine"'s dynamite: one knock of the ARENA at the blast's peak frame. A third term for
        /// the same reason the tremor is one - a quake can bring down the cube a stick of dynamite
        /// was buried under, and two writers of this transform on the same frame would fight.
        /// </summary>
        public void SetImpulse(Vector2 offset)
        {
            impulseOffset = offset;
            ApplyArenaTransform();
        }

        private float pressureScale = 1f;
        private Vector2 pressureKnock;
        private Vector2 tremorOffset;
        private float tremorDegrees;
        private Vector2 impulseOffset;

        /// <summary>Scale AND turn about the board's own centre, then knock, tremor and impulse on top.
        /// With no turn this is exactly the squeeze SetPressure always wrote.</summary>
        private void ApplyArenaTransform()
        {
            float s = pressureScale;
            Quaternion turn = Quaternion.Euler(0f, 0f, tremorDegrees);
            Vector3 centre = new Vector3(pressureCentre.x, pressureCentre.y, 0f);
            Vector3 turnedCentre = turn * (centre * s);
            transform.localScale = new Vector3(s, s, 1f);
            transform.localRotation = turn;
            transform.localPosition = centre - turnedCentre
                + new Vector3(pressureKnock.x + tremorOffset.x + impulseOffset.x,
                    pressureKnock.y + tremorOffset.y + impulseOffset.y, 0f);
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
            if (borrowedCells.Count > 0
                && (circuitHeat == null || !circuitHeat.Active)
                && (invaderColumn == null || !invaderColumn.Extracting))
            {
                RestoreBorrowedCells();
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

        /// <summary>Edge length a CUBE is drawn at - a cell less its gap - so cubes drawn away from
        /// the board (a defective block falling off the screen) are the size the board draws them.</summary>
        public float CubeWorldSize
        {
            get { return cellSize * CubeFill; }
        }

        /// <summary>The size an empty cell's slot is drawn at - the mouth a pit opens in.</summary>
        public float EmptySlotSize
        {
            get { return cellSize * EmptyFill; }
        }

        /// <summary>The face the cube at <paramref name="pos"/> wears - still standing there, or
        /// taken by a repaint in the last <paramref name="maxAge"/> seconds (unscaled). False
        /// for a cell that has held no cube lately, or one lost to the dark.</summary>
        public bool TryCubeLook(GridPos pos, float maxAge, out Sprite tile, out Color colour)
        {
            tile = null;
            colour = Color.white;
            if (board != null && kindCache != null && cellRenderers != null)
            {
                int x = pos.X - board.MinX;
                int y = pos.Y - board.MinY;
                if (x >= 0 && y >= 0 && x < kindCache.GetLength(0) && y < kindCache.GetLength(1)
                    && kindCache[x, y].HasValue && cellRenderers[x, y] != null)
                {
                    tile = cellRenderers[x, y].sprite;
                    colour = baseColorCache[x, y];
                    return tile != null;
                }
            }
            VacatedCube gone;
            if (vacated.TryGetValue(pos, out gone) && Time.unscaledTime - gone.At <= maxAge)
            {
                tile = gone.Tile;
                colour = gone.Colour;
                return tile != null;
            }
            return false;
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
            borrowedCells.Clear(); // the renderers below are rebuilt enabled anyway
            heldCells.Clear(); // a new board: nothing is on its way to any of its cells
            // EXCEPT the overtime glow, which outlives a rebuild. It is not part of the board's
            // contents: it is a light over them, its texture costs real time to generate, and
            // sweeping it up with everything else meant the overtime effect was torn down and
            // rebuilt every time a block landed - which is precisely what the visible hitch was.
            Transform keepGlow = lineGlow != null ? lineGlow.transform : null;
            Transform keepSurface = surface != null ? surface.transform : null;
            Transform keepInfection = infectionCores != null
                ? infectionCores.transform : null;
            // The burst outlives a rebuild for the same reason the cores do - and for one more:
            // the turn that detonates an infection can also erode the arena, and a rebuild
            // halfway through the animation would otherwise delete it mid-frame.
            Transform keepBurst = infectionBurst != null ? infectionBurst.transform : null;
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
            // The rot is a property of the ROUND: its bands and tissue are put back in step with
            // Core by the Refresh below, and a new arena clears them (GangreneView.Sync).
            Transform keepRot = gangrene != null ? gangrene.transform : null;
            Transform keepSnake = snake != null ? snake.transform : null;
            // The press is a property of the ROUND too: its plates are put back in step with Core
            // by the Refresh below, and a new arena has no press on it.
            Transform keepPress = press != null ? press.transform : null;
            Transform keepParasite = parasite != null ? parasite.transform : null;
            Transform keepMapus = mapus != null ? mapus.transform : null;
            Transform keepTalisman = talisman != null ? talisman.transform : null;
            // The fire front holds cells blank while it burns across them; destroyed mid-burn it
            // would leave the board with holes it never gives back.
            Transform keepFire = fireSpread != null ? fireSpread.transform : null;
            // The ice keeps per-cube state for every frozen cube on the board, which outlives a
            // rebuild the way the rot's tissue and the press's shell do.
            Transform keepIce = ice != null ? ice.transform : null;
            // A collapse in flight holds the arena's tremor; destroyed mid-quake the board would be
            // left turned a fraction of a degree.
            Transform keepQuake = quake != null ? quake.transform : null;
            // The gravity field is a property of the ROUND, not of the board's contents, so it
            // survives the rebuild and is CLEARED below - a new arena starts under ordinary
            // gravity, which is the rules' own behaviour and not something this decides.
            Transform keepGravity = gravityField != null ? gravityField.transform : null;
            // The dolls are staged across several frames; a rebuild mid-split must not delete the
            // dolls in the air.
            Transform keepDolls = matryoshka != null ? matryoshka.transform : null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == keepGlow || child == keepSurface
                    || child == keepInfection || child == keepBurst || child == keepCircuit
                    || child == keepOverload || child == keepQuarantine
                    || child == keepHeat || child == keepNest || child == keepLane
                    || child == keepGravity || child == keepDolls || child == keepRot
                    || child == keepSnake || child == keepPress
                    || child == keepParasite || child == keepMapus
                    || child == keepTalisman || child == keepFire || child == keepIce
                    || child == keepQuake)
                {
                    continue;
                }
                Destroy(child.gameObject);
            }
            if (gravityField != null)
            {
                gravityField.Clear();
            }
            ghostSprites.Clear();
            pressPreview.Clear();
            outsidePreviewSprites.Clear();
            outsidePreviewBaseColors.Clear();
            outsidePreviewBreathes.Clear();
            infectionMarkers.Clear();
            deadZoneLine = null; // destroyed with the other children above; redrawn by SetDeadZone
            board = newBoard;

            // THE ARENA IS NOT THE BOARD'S BOUNDING BOX. The backing store is a rectangle, and
            // "Tılsım" grows it rightward to hold ground it reclaimed OUTSIDE the arena - a 2x2
            // claim past the right edge makes the store two columns wider. The cells it grows
            // over are holes and the cell renderers already skip them, but the SURFACE was still
            // being built from that rectangle, so the plate, its bevelled frame and its per-cell
            // recesses all extended two full columns: the player was handed a whole new column of
            // floor for four reclaimed cells. It also shrank the board, because the cell size is
            // derived from the same numbers.
            //
            // So the arena is measured instead: every cell that is REQUIRED play area, plus the
            // dead ones, which are eroded arena and still stand on the plate. Bonus ground is
            // optional by definition, so it falls outside it - which is the point, because it is
            // a gift lying beside the board rather than part of it.
            int arenaWide = 1;
            int arenaHigh = 1;
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var gp = new GridPos(board.MinX + x, board.MinY + y);
                    bool arena = board.IsDead(gp) || (board.IsInside(gp) && !board.IsOptional(gp));
                    if (!arena)
                    {
                        continue;
                    }
                    if (x + 1 > arenaWide)
                    {
                        arenaWide = x + 1;
                    }
                    if (y + 1 > arenaHigh)
                    {
                        arenaHigh = y + 1;
                    }
                }
            }
            cellSize = Mathf.Min(maxWorldSize / arenaWide, maxWorldSize / arenaHigh);
            bottomLeft = center - new Vector2(arenaWide, arenaHigh) * (cellSize * 0.5f);

            // The board's surface is GENERATED - a plate with a bevelled frame and a recess per
            // cell - rather than the flat rectangle this used to be. See BoardSurfaceView.
            Surface.Build(center, arenaWide, arenaHigh, cellSize, BorderOverhang);
            pressureCentre = center;
            glowCenter = center;
            glowCellsWide = arenaWide;
            glowCellsHigh = arenaHigh;
            glowCellSize = cellSize;
            LineGlow.Build(center, arenaWide, arenaHigh, cellSize, BorderOverhang);

            cellRenderers = new SpriteRenderer[board.Width, board.Height];
            previewRenderers = new SpriteRenderer[board.Width, board.Height];
            previewBaseColors = new Color[board.Width, board.Height];
            previewBreathes = new bool[board.Width, board.Height];
            kindCache = new CubeKind?[board.Width, board.Height];
            litFor = new float[board.Width, board.Height];
            baseColorCache = new Color[board.Width, board.Height];
            preWashCache = new Color[board.Width, board.Height];
            rotWash.Clear();
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

        /// <summary>The face a cube wears, card and all. A ghost cube's art lives on its CARD's
        /// element rather than on its kind, so a presence that draws one (TalismanView's harvest)
        /// has to ask through here - CubeTile(kind, null) would hand it the default block.
        /// </summary>
        public Sprite FaceOf(Cube cube)
        {
            return ViewUtil.CubeTile(cube.Kind, CardOf(cube));
        }

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
            sawRot = false;
            sawSnake = false;
            sawPress = false;
            sawIce = false;
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
                    // "Alacakaranlık": an UNLIT cell tells the player NOTHING. Not by being
                    // tinted dark - the tint was never the tell. A cube brings its own painted
                    // tile and stands nearly edge to edge (CubeFill) while an empty cell is a
                    // smaller flat square (EmptyFill), so a board darkened only by colour still
                    // drew its blocks in silhouette: bigger squares with tighter gaps, plainly
                    // readable. So the sprite and the SIZE are anonymised too, and every cell in
                    // the arena becomes the same dead square until something lights it.
                    //
                    // kindCache is cleared with it, which is what stops AnimateElementCubes from
                    // flickering a hidden fire cube back into view every frame.
                    float light = dark ? LightAt(x, y) : 1f;
                    if (light <= 0f)
                    {
                        ViewUtil.ApplyTile(cellRenderers[x, y], null, cellSize * EmptyFill);
                        cellRenderers[x, y].color = DarkCellColor;
                        kindCache[x, y] = null;
                        baseColorCache[x, y] = DarkCellColor;
                        continue;
                    }
                    // "YILAN" DRAWS ITSELF. Its segments are the six pieces painted for it and
                    // they are placed on these cells by SnakeView, so the board must not put a cube
                    // here: a green square under the snake is exactly what that art replaces.
                    if (cube.HasValue && cube.Value.Kind == CubeKind.Snake)
                    {
                        ViewUtil.ApplyTile(cellRenderers[x, y], null, cellSize * EmptyFill);
                        cellRenderers[x, y].color = EmptyColor;
                        kindCache[x, y] = null;
                        baseColorCache[x, y] = EmptyColor;
                        preWashCache[x, y] = EmptyColor;
                        sawSnake = true;
                        continue;
                    }
                    // A CUBE is a painted tile and fills its cell; an EMPTY cell stays the flat
                    // inset square it always was, so the grid still reads as holes waiting to be
                    // filled rather than as pale blocks.
                    Sprite tile = null;
                    if (cube.HasValue)
                    {
                        vacated.Remove(gp);
                        tile = ViewUtil.CubeTile(cube.Value.Kind, CardOf(cube.Value));
                        // Blind: the cube GROWS into its cell as the light reaches it and
                        // shrinks back to an anonymous square as it fades, so a revealed block
                        // never pops in and out of the dark.
                        ViewUtil.ApplyTile(cellRenderers[x, y], tile,
                            cellSize * Mathf.Lerp(EmptyFill, CubeFill, light));
                    }
                    else
                    {
                        // A cube stood here on the last repaint and is gone on this one: keep
                        // its face for the explosion that is about to play over the cell.
                        if (kindCache[x, y].HasValue && cellRenderers[x, y].sprite != null)
                        {
                            vacated[gp] = new VacatedCube
                            {
                                Tile = cellRenderers[x, y].sprite,
                                Colour = baseColorCache[x, y],
                                At = Time.unscaledTime
                            };
                        }
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
                    // SHUFFLE EROSION'S DEAD ZONE: still play area, but a line through it pays
                    // nothing, so it has to read as spoiled ground rather than as a hole. Empty it
                    // takes the scar colour outright; a cube standing on it is only dulled toward
                    // it, because the cube is still a real cube that will explode with its line.
                    if (board.IsBlighted(gp))
                    {
                        color = cube.HasValue ? Color.Lerp(color, BlightColor, 0.38f) : BlightColor;
                    }
                    // "Kangren": a line the rot took WHOLE can never explode again, which the
                    // player has to be able to see - an unexplodable full line otherwise reads as
                    // a bug. Washed like the erosion scar it behaves like. The colour BEFORE the
                    // wash is kept so the sweep that kills a line can walk the wash in a cell at a
                    // time (SetRotWash) without a full repaint per frame.
                    preWashCache[x, y] = color;
                    if (board.RowIsInfectionDead(gp.Y) || board.ColumnIsInfectionDead(gp.X))
                    {
                        color = Color.Lerp(color, RotDeadLineColor,
                            RotWashAt(gp) * (cube.HasValue ? 0.4f : 0.66f));
                    }
                    // NOT painted here, on purpose: "Besleme"'s creature patch and "İstilacı"'s
                    // doomed column both draw themselves in their own views (CreatureNestView,
                    // InvaderColumnView), over the top of this one. Washing the cells here as
                    // well would double the tint and fight the corridor's own escalation.
                    // "Alacakaranlık": the truth is drowned in the dark, and comes back only in
                    // proportion to the light standing on the cell - a placement's faint one or
                    // a blast's far brighter one.
                    if (dark)
                    {
                        color = Color.Lerp(DarkCellColor, color, light);
                    }
                    cellRenderers[x, y].color = color;
                    kindCache[x, y] = cube.HasValue ? cube.Value.Kind : (CubeKind?)null;
                    baseColorCache[x, y] = color;
                    sawRot |= cube.HasValue && cube.Value.Kind == CubeKind.Gangrene;
                    sawPress |= cube.HasValue && cube.Value.Kind == CubeKind.Compressed;
                    sawIce |= cube.HasValue && cube.Value.Kind == CubeKind.Ice;
                }
            }
            RefreshGhostTraces();
            if (animatingWater)
            {
                HideWaterCells();
            }
            foreach (GridPos cell in heldCells)
            {
                BlankHeld(cell);
            }
            // "Kangren": the standing tissue and the dead lines' bands follow every repaint, so
            // what they draw is what the rules say rather than a memory of it. Asked for only once
            // there is rot to draw - or once there is a view holding some.
            if (sawRot || gangrene != null || board.InfectionDeadRows.Count > 0
                || board.InfectionDeadColumns.Count > 0)
            {
                Gangrene.Sync(this);
            }
            // And the snake, for the same reason: the cells above were left blank for it.
            if (sawSnake || snake != null)
            {
                Snake.Sync(this);
            }
            // And the press, for the same reason: its shell, its idle and its countdown have to
            // follow the rules' own board rather than a memory of it.
            if (sawPress || press != null)
            {
                Press.Sync(this);
            }
            // And the ice, for the same reason: every frozen cube's front, rim and stillness are
            // written onto the cell renderer this repaint has just re-tiled, so without this the
            // board would paint plain water over an ice cube every time anything changed.
            if (sawIce || ice != null)
            {
                Ice.Sync(this);
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
                // A GHOST TRACE IS A GHOST BLOCK, and it is drawn with the ghost block's own art
                // and its own billowing material (ViewUtil.ApplyTile picks both up from the tile).
                // It used to be a flat pale square instead - the painted tile and the warp were
                // already there and simply were not asked for, so the one cube in the game whose
                // whole identity is "not quite solid" was the one drawn as a rectangle.
                SpriteRenderer trace = ViewUtil.MakeCell(transform, "GhostCube",
                    CellToWorld(entry.Key), cellSize * CubeFill, Color.white, 1);
                ViewUtil.ApplyTile(trace, ViewUtil.CubeTile(entry.Value.Kind, CardOf(entry.Value)),
                    cellSize * CubeFill);
                // Off the board and not real yet, so it is thinner than a cube that landed - the
                // alpha pulse above is what carries that, and the tint stays neutral so the
                // block's own material shows through it.
                trace.color = new Color(1f, 1f, 1f, 0.35f);
                ghostSprites.Add(trace);
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

        /// <summary>
        /// The detonation itself: the infected block is eaten from inside and comes apart, and on
        /// the FIRST one its spores carry the contagion to the cells the spread took.
        ///
        /// It takes the destroyed cubes rather than plain cells because the burst draws the
        /// block wearing its own tiles right up to the moment it comes apart, and by the time
        /// this runs the rules have destroyed them and Refresh has already painted them away -
        /// so the art has to come from the turn's destruction log.
        ///
        /// <paramref name="hold"/> is how long the block stands intact first: the core's charge.
        /// <paramref name="spreadTo"/> is the rules' own list of cells that were actually
        /// infected, empty on every detonation after the first; the view never works the plus
        /// out for itself. Those cells' cores already exist - the refresh made them - and would
        /// arrive before the block had even gone, so their birth is held until the spores
        /// carrying the spread have landed on them.
        ///
        /// Returns the seconds from now until the source cell ruptures.
        /// </summary>
        public float PlayInfectionBurst(IReadOnlyList<DestroyedCube> block, GridPos source,
            IReadOnlyList<GridPos> spreadTo, float hold)
        {
            if (block == null || block.Count == 0)
            {
                return 0f;
            }
            EnsureInfectionBurst();
            if (infectionBurst == null)
            {
                return 0f;
            }
            // How many steps THROUGH the block each cube is from the cell that ripened - through
            // it, not across the gap inside an L or a U - because that is the path the sickness
            // takes, and the rupture follows it.
            var inBlock = new HashSet<GridPos>();
            for (int i = 0; i < block.Count; i++)
            {
                inBlock.Add(block[i].Pos);
            }
            var steps = new Dictionary<GridPos, int>();
            var frontier = new Queue<GridPos>();
            steps[source] = 0;
            frontier.Enqueue(source);
            while (frontier.Count > 0)
            {
                GridPos at = frontier.Dequeue();
                int next = steps[at] + 1;
                GridPos[] around =
                {
                    new GridPos(at.X + 1, at.Y), new GridPos(at.X - 1, at.Y),
                    new GridPos(at.X, at.Y + 1), new GridPos(at.X, at.Y - 1)
                };
                for (int i = 0; i < around.Length; i++)
                {
                    if (inBlock.Contains(around[i]) && !steps.ContainsKey(around[i]))
                    {
                        steps[around[i]] = next;
                        frontier.Enqueue(around[i]);
                    }
                }
            }

            var ghosts = new List<InfectionBurstView.Ghost>();
            for (int i = 0; i < block.Count; i++)
            {
                Cube cube = block[i].Cube;
                // CardLookup rather than CardOf: this is not a repaint, and it must not be
                // counted against the gallery's unresolved-cube diagnostic.
                BlockCard card = CardLookup != null ? CardLookup(cube.SourceCardId) : null;
                Sprite tile = ViewUtil.CubeTile(cube.Kind, card);
                int step;
                if (!steps.TryGetValue(block[i].Pos, out step))
                {
                    // Not joined to the source (a block always is) - plain distance, so it
                    // still goes in a sensible order.
                    step = Mathf.Abs(block[i].Pos.X - source.X)
                        + Mathf.Abs(block[i].Pos.Y - source.Y);
                }
                ghosts.Add(new InfectionBurstView.Ghost
                {
                    Where = CellToWorld(block[i].Pos),
                    Tile = tile,
                    Colour = ViewUtil.CubeTileColor(cube, tile),
                    // The size a cube is drawn at, so the ghost takes over without a jump.
                    Size = cellSize * CubeFill,
                    Steps = step
                });
            }
            var carry = new List<Vector2>();
            if (spreadTo != null)
            {
                for (int i = 0; i < spreadTo.Count; i++)
                {
                    carry.Add(CellToWorld(spreadTo[i]));
                }
            }
            float rupture = infectionBurst.Play(ghosts, cellSize, hold, CellToWorld(source), carry);
            if (infectionCores != null && spreadTo != null)
            {
                // Until the carriers LAND, not merely until the rupture: they do the travelling,
                // and the core only has to bloom where they arrive.
                infectionCores.HoldBirth(spreadTo, rupture + InfectionBurstView.Style.CarrierSeconds);
            }
            return rupture;
        }

        /// <summary>True while a detonation is still on screen.</summary>
        public bool InfectionBursting
        {
            get { return infectionBurst != null && infectionBurst.Active; }
        }

        /// <summary>Clears a detonation mid-flight - the animation lab's RESET.</summary>
        public void StopInfectionBurst()
        {
            if (infectionBurst != null)
            {
                infectionBurst.Stop();
            }
        }

        private void EnsureInfectionBurst()
        {
            if (infectionBurst != null)
            {
                return;
            }
            var go = new GameObject("InfectionBurst");
            go.transform.SetParent(transform, false);
            infectionBurst = go.AddComponent<InfectionBurstView>();
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
            LightUpAround(cells, LightRadius, 1f);
        }

        /// <summary>A block was set down. It lights what it touches, faintly and briefly - see
        /// PlacementLightRadius.</summary>
        public void LightUpPlacement(IReadOnlyList<GridPos> cells)
        {
            LightUpAround(cells, PlacementLightRadius, PlacementLightStrength);
        }

        /// <summary>
        /// As above, but at a chosen reach and strength - the second caller is a PLACEMENT,
        /// which lights only what it touches and only faintly ("Alacakaranlık").
        ///
        /// One method rather than two because the falloff, the round mask and the
        /// keep-the-brightest rule are the whole behaviour and must not be written twice: a
        /// placement that faded differently from a blast would read as a second kind of light.
        /// A blast landing on a cell a placement just lit keeps the brighter of the two, which
        /// is what stops the small light from ever dimming the big one.
        /// </summary>
        public void LightUpAround(IReadOnlyList<GridPos> cells, int radius, float strength)
        {
            if (!dark || board == null || litFor == null || cells == null || radius < 0
                || strength <= 0f)
            {
                return;
            }
            foreach (GridPos cell in cells)
            {
                int cx = cell.X - board.MinX;
                int cy = cell.Y - board.MinY;
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    for (int y = cy - radius; y <= cy + radius; y++)
                    {
                        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
                        {
                            continue;
                        }
                        // Round light: the corners of the square stay dark, so a blast reads as
                        // a glow rather than as a box.
                        int dx = x - cx;
                        int dy = y - cy;
                        if (dx * dx + dy * dy > radius * radius)
                        {
                            continue;
                        }
                        // Nearer cells hold the light longer, so it fades from the edge inward.
                        float share = 1f - Mathf.Sqrt(dx * dx + dy * dy) / (radius + 1f);
                        float seconds = LightSeconds * share * strength;
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

        /// <summary>Hands "Karantina"'s sealed cells to the containment field. Pass null to
        /// clear them. The field works out for itself which cells are new, so this is only ever
        /// told the current state - which now CHANGES rather than only growing, since the zone
        /// is relaid somewhere else every few turns.</summary>
        public void ShowQuarantine(IReadOnlyList<GridPos> cells)
        {
            quarantinedCells.Clear();
            if (cells != null) { quarantinedCells.AddRange(cells); }
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
            quarantineField.SetCells(quarantinedCells, board, CellToWorld, cellSize, IsOccupied);
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
            HideBorrowedCells(cubes);
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
            HideBorrowedCells(cubes);
            circuitHeat.Hold(CircuitGhosts(cubes), cellSize);
        }

        /// <summary>Takes the parked blocks back off the board.</summary>
        public void ClearCircuitBlocks()
        {
            if (circuitHeat != null)
            {
                circuitHeat.Stop();
            }
            RestoreBorrowedCells();
        }

        private void HideBorrowedCells(IReadOnlyList<DestroyedCube> cubes)
        {
            RestoreBorrowedCells();
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
                    borrowedCells.Add(p);
                }
            }
        }

        private void RestoreBorrowedCells()
        {
            for (int i = 0; i < borrowedCells.Count; i++)
            {
                GridPos p = borrowedCells[i];
                int cx = p.X - board.MinX;
                int cy = p.Y - board.MinY;
                if (cx >= 0 && cy >= 0 && cx < cellRenderers.GetLength(0)
                    && cy < cellRenderers.GetLength(1) && cellRenderers[cx, cy] != null)
                {
                    cellRenderers[cx, cy].enabled = true;
                }
            }
            borrowedCells.Clear();
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

        /// <summary>True if that cell stands in the quarantine.</summary>
        private bool IsQuarantined(GridPos cell)
        {
            for (int i = 0; i < quarantinedCells.Count; i++)
            {
                if (quarantinedCells[i].X == cell.X && quarantinedCells[i].Y == cell.Y)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// "Kütleçekim merkezi": points the arena's gravity FIELD along the pull.
        ///
        /// <paramref name="flow"/> is GameBoard.WaterFlow, handed straight through from the rules
        /// - this view keeps no direction of its own. (0,-1) is an ordinary board and turns the
        /// whole thing off: every board falls downward, and a permanent marker for that is noise.
        ///
        /// It used to be a row of pips outside one edge. They read as a debug marker, which is
        /// the wrong weight for a power that turns the physics of the arena for a whole round -
        /// see GravityFieldView for what replaced them.
        /// </summary>
        public void ShowGravity(GridPos flow)
        {
            bool down = flow.X == 0 && flow.Y == -1;
            if (board == null || (down && gravityField == null))
            {
                return;
            }
            EnsureGravityField();
            gravityField.Show(flow, WorldRect, cellSize, board.Width, board.Height);
        }

        private void EnsureGravityField()
        {
            if (gravityField == null)
            {
                var go = new GameObject("GravityField");
                go.transform.SetParent(transform, false);
                gravityField = go.AddComponent<GravityFieldView>();
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
            // The column draws its OWN copies of what it takes, so the board stands back for
            // as long as it is holding them - see borrowedCells.
            HideBorrowedCells(taken);
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

        /// <summary>"Matruşka"'s dolls as they stand, drawn without ceremony. Null clears them. The
        /// drawing - and everything that happens to them - is MatryoshkaView's.</summary>
        public void ShowDolls(IReadOnlyList<GridPos> cells, IReadOnlyList<int> generations,
            int lastGeneration)
        {
            if (cells == null || board == null)
            {
                if (matryoshka != null)
                {
                    matryoshka.Clear();
                }
                return;
            }
            EnsureMatryoshka();
            matryoshka.Show(DollMarks(cells, generations), lastGeneration, board, CellToWorld, cellSize);
        }

        /// <summary>
        /// A turn that did something to the dolls: its events and where the dolls end up, held until
        /// ReleaseDolls - the moment the cubes under them go - so a doll opens as its cube breaks.
        /// </summary>
        public void HoldDolls(IReadOnlyList<DollEvent> events, IReadOnlyList<GridPos> cells,
            IReadOnlyList<int> generations, int lastGeneration)
        {
            if (board == null || events == null)
            {
                return;
            }
            EnsureMatryoshka();
            matryoshka.Hold(events, DollMarks(cells, generations), lastGeneration, board, CellToWorld,
                cellSize);
        }

        /// <summary>Plays the held doll events, if any are held.</summary>
        public void ReleaseDolls()
        {
            if (matryoshka != null)
            {
                matryoshka.Release();
            }
        }

        private List<MatryoshkaView.Mark> DollMarks(IReadOnlyList<GridPos> cells,
            IReadOnlyList<int> generations)
        {
            var marks = new List<MatryoshkaView.Mark>();
            for (int i = 0; cells != null && i < cells.Count; i++)
            {
                if (board.IsInside(cells[i]))
                {
                    int generation = generations != null && i < generations.Count ? generations[i] : 1;
                    marks.Add(new MatryoshkaView.Mark(cells[i], Mathf.Max(1, generation)));
                }
            }
            return marks;
        }

        private void EnsureMatryoshka()
        {
            if (matryoshka != null)
            {
                return;
            }
            var go = new GameObject("Matryoshka");
            go.transform.SetParent(transform, false);
            matryoshka = go.AddComponent<MatryoshkaView>();
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
                GridPos cell = origin + offset;
                PaintPreviewCell(cell, color, true);
                // DENIED ENTRY. If a sealed cell is under this preview it refuses it ITSELF - the
                // nearest ribs clamp and the seal tightens. That is the whole message: no red
                // cross, no shake, no text, because the thing stopping you is right there and can
                // answer for itself. Asking the board whether the cell is sealed is reading a
                // state, not deciding a rule.
                if (mapus != null && board.IsSealed(cell))
                {
                    mapus.PlayDenied(cell);
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

        /// <summary>
        /// "HIDROLIK PRES" AIMED. The four cells are ONE MECHANICAL AREA, so they are not tinted
        /// one at a time and never with the explosion colour - nothing here explodes. What is drawn
        /// is a single very thin slate pressure frame around the whole 2x2, four small INWARD-facing
        /// corner brackets, and a faint inner shadow. No arrows: four big arrows over the board is
        /// the thing this replaces, and brackets plus a shadow already say "this is about to be
        /// squeezed".
        ///
        /// Invalid targets keep the game's existing language (the red preview tint), so a refusal
        /// reads the way every other refusal in the game does.
        ///
        /// <paramref name="pressCell"/> is the press's SECOND aim: once the patch is committed the
        /// player names which of its four cells keeps the cube, and that one cell is tinted inside
        /// the frame - the frame stays, because the patch is still what gets squeezed.
        /// </summary>
        public void ShowPressPreview(IReadOnlyList<GridPos> cells, bool valid,
            GridPos? pressCell = null)
        {
            ClearPreview();
            if (board == null || cells == null || cells.Count == 0)
            {
                return;
            }
            if (!valid)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    if (board.IsInside(cells[i]))
                    {
                        PaintPreviewCell(cells[i], InvalidPreviewColor, true);
                    }
                }
                return;
            }
            // The patch's own middle and extent, taken from the cells rather than assumed, so a
            // differently shaped patch one day draws its own frame.
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            int inside = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (!board.IsInside(cells[i]))
                {
                    continue;
                }
                Vector2 at = CellToWorld(cells[i]);
                min = Vector2.Min(min, at);
                max = Vector2.Max(max, at);
                inside++;
            }
            if (inside == 0)
            {
                return;
            }
            Vector2 centre = (min + max) * 0.5f;
            Vector2 span = (max - min) + new Vector2(cellSize, cellSize);
            float line = Mathf.Max(cellSize * 0.035f, 0.01f);
            float bracket = cellSize * 0.26f;
            // ONE frame: four thin edges of a single rectangle, muted steel.
            pressPreview.Add(ViewUtil.MakeRounded(transform, "PressFrame",
                centre + new Vector2(0f, span.y * 0.5f - line * 0.5f),
                new Vector2(span.x, line), PressFrameColor, PressPreviewOrder));
            pressPreview.Add(ViewUtil.MakeRounded(transform, "PressFrame",
                centre - new Vector2(0f, span.y * 0.5f - line * 0.5f),
                new Vector2(span.x, line), PressFrameColor, PressPreviewOrder));
            pressPreview.Add(ViewUtil.MakeRounded(transform, "PressFrame",
                centre + new Vector2(span.x * 0.5f - line * 0.5f, 0f),
                new Vector2(line, span.y), PressFrameColor, PressPreviewOrder));
            pressPreview.Add(ViewUtil.MakeRounded(transform, "PressFrame",
                centre - new Vector2(span.x * 0.5f - line * 0.5f, 0f),
                new Vector2(line, span.y), PressFrameColor, PressPreviewOrder));
            // Four corner brackets, each pointing IN - the game's own rounded language rather than
            // hard typographic corners.
            float bx = span.x * 0.5f - line * 1.6f;
            float by = span.y * 0.5f - line * 1.6f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Vector2 corner = centre + new Vector2(bx * sx, by * sy);
                    pressPreview.Add(ViewUtil.MakeRounded(transform, "PressBracket",
                        corner - new Vector2(bracket * 0.5f * sx, 0f),
                        new Vector2(bracket, line * 1.6f), PressBracketColor,
                        PressPreviewOrder));
                    pressPreview.Add(ViewUtil.MakeRounded(transform, "PressBracket",
                        corner - new Vector2(0f, bracket * 0.5f * sy),
                        new Vector2(line * 1.6f, bracket), PressBracketColor,
                        PressPreviewOrder));
                }
            }
            // And the faintest inner shadow, so the area reads as recessed under the jaws.
            pressPreview.Add(ViewUtil.MakeRounded(transform, "PressInner", centre,
                span - new Vector2(line * 3f, line * 3f), PressInnerColor,
                PressPreviewOrder - 1));
            if (pressCell.HasValue && board.IsInside(pressCell.Value))
            {
                PaintPreviewCell(pressCell.Value, ValidPreviewColor, true);
            }
        }

        /// <summary>The frame's muted steel, the brackets a shade brighter, and a very faint inner
        /// shadow. Slate-grey throughout: the press is industrial, not an alert.</summary>
        private static readonly Color PressFrameColor = new Color(0.56f, 0.60f, 0.67f, 0.55f);

        private static readonly Color PressBracketColor = new Color(0.70f, 0.74f, 0.80f, 0.7f);

        private static readonly Color PressInnerColor = new Color(0f, 0f, 0f, 0.1f);

        private const int PressPreviewOrder = 4;

        private readonly List<SpriteRenderer> pressPreview = new List<SpriteRenderer>();

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
            for (int i = 0; i < pressPreview.Count; i++)
            {
                if (pressPreview[i] != null)
                {
                    Destroy(pressPreview[i].gameObject);
                }
            }
            pressPreview.Clear();
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

        /// <summary>THE ANIMATION LAB ONLY: paints one cell in a state colour the board itself
        /// uses - "Mapus" sealed, or "Tılsım" bonus ground - so the lab can show what they look
        /// like without the rules having to make one on a board of its own. Any repaint takes it
        /// straight back; nothing here is state.</summary>
        public void PaintCellState(GridPos cell, bool sealedCell)
        {
            PaintCell(cell, sealedCell ? SealedColor : BonusGroundColor);
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
