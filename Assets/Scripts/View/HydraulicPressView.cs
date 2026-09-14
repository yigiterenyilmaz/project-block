// PURPOSE: "Hidrolik pres" - QUAD-STACK HYDRAULIC PRESS. The squeeze, the four turns of held
// pressure, and the release. Not a shrink, not a teleport, not a dissolve and not a sprite swap:
// a COMPRESSION, then STORAGE UNDER PRESSURE, then a DIRECTED RELEASE of that pressure.
//
// THE PHYSICAL IDEA. A 2x2 patch is taken under pressure. Each occupied cube loses its VOLUME -
// its bevel, its contact depth, its soft-3D shading - and becomes a thin PRESSED LAMINA that still
// wears its own material, so gold goes in as gold. An empty quadrant is not skipped: it is
// remembered for a moment as a dark NEGATIVE IMPRINT, because the rules store the hole too. The
// four laminae are drawn along guide rails to the cell Core put the compressed cube in, stack with
// a readable offset, and one heavy hydraulic crush presses the stack down to a single cell. Then the
// industrial slate shell closes OVER them - edge shutters, centre fill, seam lock - and a tiny
// inward punch settles it. Four turns later the locks let go, the stored colours come up under the
// seams for a moment, the shell retracts, and the laminae separate and expand to the cells CORE
// says they go to, regaining their volume on the way; a stored hole comes back as a hole.
//
// WHEN SOMETHING IS IN THE WAY the press does not slide it: a directional pressure front runs out
// along the axis Core used, the near cube answers first, and the chain moves exactly as the rules
// moved it - a cube whose destination is off the board keeps going and never stops at the rim.
//
// WHEN A SIDE IS SHUT by gold or obsidian, that side is PRESSED and refused: one pixel of shell
// pressure, a compression mark on the cube that will not budge, zero movement, a pixel of recoil -
// and then the pressure is rerouted through the shell to the axis Core actually opened on. No
// shield, no spark, no glow. THE VIEW NEVER DECIDES ANY OF THIS: the sides it presses, the order it
// presses them in, the axis it ends on and every cube that moves all come from
// PressReleaseVisuals. It is handed world positions and plays them.
//
// What it is NOT: a piston sprite, a hydraulic cylinder, a factory machine, a magic portal, a
// cartoon squash, a white flash, a screen shake. The mechanical language is ABSTRACT INDUSTRIAL and
// the board itself is the chamber. The catastrophic failure is not here either - a pressure vessel
// bursting is its own event with its own identity (PressureVesselView).

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The press squeezing a 2x2 patch shut, and letting it back out.</summary>
    public sealed class HydraulicPressView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the press READS. Times in seconds, sizes in CELLS
        /// unless a name says world.</summary>
        public static class Style
        {
            // ---- B) the chamber forms: four pressure jaws ----
            /// <summary>The jaws come out of the board rather than fading in: "the mechanism
            /// engaged".</summary>
            public static float JawAppearDuration = 0.08f;

            /// <summary>How thick a jaw is, as a share of a cell (0.14 = about 9px on a 64px cell).
            /// Heavy: these are pressure parts, not a frame's edges.</summary>
            public static float JawDepth = 0.14f;

            /// <summary>
            /// How much of its side a jaw covers, 0..1. WELL UNDER 1 on purpose: four jaws that meet
            /// at the corners are a rectangle, and a thin rectangle around the patch reads as a
            /// debug bounding box - which is exactly what the first pass drew. At 0.55 they are four
            /// separate mechanical parts pressing on four sides.
            /// </summary>
            public static float JawLength = 0.55f;

            /// <summary>How far a jaw sits outside the patch it grips, in cells.</summary>
            public static float JawInset = 0.06f;

            /// <summary>The small contact shadow under each jaw, so it sits ON the board.</summary>
            public static float JawShadowStrength = 0.35f;

            /// <summary>How long the jaws take to pull back to the board's edges at the end. They
            /// retract; they never snap out of existence.</summary>
            public static float JawRetractDuration = 0.1f;

            /// <summary>How far a jaw travels out of the board edge as it forms.</summary>
            public static float JawEmerge = 0.055f;

            public static Color JawColour = new Color(0.19f, 0.21f, 0.26f);

            /// <summary>How much the cells under the patch darken as the jaws lock.</summary>
            public static float ChamberDarkening = 0.22f;

            /// <summary>How far the jaws close in as they drive the material onto the anchor. The
            /// chamber shrinks with the material rather than standing around it.</summary>
            public static float JawClose = 0.34f;

            // ---- C) pressure build ----
            public static float PressureBuildStart = 0.05f;

            public static float PressureBuildEnd = 0.14f;

            /// <summary>How far a cube shifts toward the middle BEFORE it leaves its cell, in cells
            /// (0.03 = about 2px). It must not look like it has set off yet.</summary>
            public static float PressureOffset = 0.03f;

            /// <summary>How much the contact shadows tighten under the build.</summary>
            public static float PressureShadowTighten = 0.12f;

            // ---- D) flattening into laminae ----
            public static float FlattenStart = 0.1f;

            public static float FlattenEnd = 0.25f;

            /// <summary>
            /// How much of the cube's HEIGHT a finished lamina keeps.
            ///
            /// HIGH ON PURPOSE. This is a top-down board: a cube that loses its thickness does not
            /// lose its FOOTPRINT, it loses its bevel, its side shading and its contact depth. The
            /// first pass squashed the sprite to 0.22 and the result was a coloured bar with no
            /// relation to the cube it came from - a UI strip, not a pressed block. The volume now
            /// comes out of the SHADING (PressLamina's _Volume) and the silhouette stays a rounded
            /// square that has been pressed down a little.
            /// </summary>
            public static float LaminaThickness = 0.86f;

            /// <summary>The plate's own visible thickness - a dark lip along its bottom edge and a
            /// catch of light on its top. One to three pixels; without it a flat cube is a sticker.
            /// </summary>
            public static float LaminaEdgeShadow = 1f;

            /// <summary>How much of its surface highlight a pressed plate keeps. Not zero: a plate
            /// with no light on it at all stops being a material.</summary>
            public static float LaminaSurfaceHighlight = 0.22f;

            /// <summary>How far the material flatten goes - bevel and shading suppression, NOT a
            /// scale. A lamina still wears its own material at 1.</summary>
            public static float FlattenStrength = 1f;

            /// <summary>How much a lamina widens as it is pressed thin. Small - the footprint is
            /// meant to be RECOGNISABLE, not squeezed sideways into a different shape.</summary>
            public static float LaminaSpread = 0.03f;

            /// <summary>The dark negative plate an EMPTY quadrant leaves, and how long it lives
            /// past the flatten. It must never look like an object.</summary>
            public static float NullImprintStrength = 0.55f;

            public static float NullImprintThickness = 0.12f;

            // ---- E) convergence ----
            public static float ConvergeStart = 0.2f;

            public static float ConvergeEnd = 0.38f;

            /// <summary>How far the path bows inward on its way - a guide rail, never an orbit.
            /// </summary>
            public static float ConvergeArc = 0.13f;

            /// <summary>Seconds between the four laminae arriving. A small overlap, not a queue.
            /// </summary>
            public static float LaminaStagger = 0.022f;

            /// <summary>How far the stacked laminae stand apart before the crush, in cells. This
            /// is what makes FOUR layers readable rather than one blob.</summary>
            public static float StackSpacing = 0.055f;

            /// <summary>The soft shadow each lamina drops on the one under it. Without this the
            /// stack is four overlapping decals rather than four physical plates.</summary>
            public static float StackLayerShadow = 0.4f;

            /// <summary>How much colour a lamina loses while it travels under pressure, and how
            /// much cold comes in. Both small.</summary>
            public static float TravelDesaturation = 0.18f;

            public static float TravelCold = 0.1f;

            // ---- F) the final hydraulic crush ----
            //
            // The four layers must be READABLE as four for a moment before they are driven
            // together - that beat is the whole difference between "a stack was pressed" and "four
            // things vanished into one". Converge ends at 0.38 and the crush starts at 0.43, so
            // there are 50ms of stack to see.
            public static float StackHoldEnd = 0.52f;

            public static float CrushStart = 0.43f;

            /// <summary>How hard the stack is driven together. The peak is a dull warm pressure
            /// line along the seams - never a flash.</summary>
            public static float CrushStrength = 1f;

            public static float CrushSeamLight = 0.8f;

            // ---- G) the shell closing ----
            public static float ShellCloseStart = 0.49f;

            public static float ShellCloseEnd = 0.62f;

            /// <summary>The inward punch as the seam locks, in cells, and how long it settles.
            /// </summary>
            public static float ShellSettleAmount = 0.03f;

            public static float ShellSettleEnd = 0.74f;

            // ---- I) release preparation ----
            public static float UnlockDuration = 0.1f;

            /// <summary>How far each edge of the shell strains outward before it opens, in cells.
            /// Per edge - never a uniform scale.</summary>
            public static float ShellTension = 0.028f;

            /// <summary>How far the stored colours come up under the seams. Brief, and the whole
            /// reason the player believes the four cells are still in there.</summary>
            public static float InternalColorMemory = 0.75f;

            public static float TensionStart = 0.07f;

            public static float TensionEnd = 0.18f;

            // ---- J/K) the shell opening and the laminae expanding ----
            public static float ShellOpenStart = 0.14f;

            public static float ShellOpenEnd = 0.28f;

            public static float RestoreTravelStart = 0.22f;

            public static float RestoreTravelEnd = 0.42f;

            /// <summary>When the volume comes back - the flatten run backwards.</summary>
            public static float RestoreReliefStart = 0.34f;

            public static float RestoreReliefEnd = 0.52f;

            public static float RestoreSettleEnd = 0.6f;

            /// <summary>How long a restored HOLE shows its imprint on the floor before it goes.
            /// </summary>
            public static float NullRestoreImprint = 0.12f;

            /// <summary>How far the edge on the release's own side leads the others, in cells.
            /// </summary>
            public static float DirectionalLead = 0.035f;

            // ---- L/M/N/O/P/Q/R/S) the push ----
            /// <summary>The mechanical pressure front: a low-opacity slate compression band along
            /// the cells, never a beam or a shockwave ring.</summary>
            public static float PushFrontStrength = 0.3f;

            public static float PushFrontStart = 0.16f;

            public static float PushFrontEnd = 0.32f;

            /// <summary>How far ahead of the cubes the front runs, in cells.</summary>
            public static float PushFrontReach = 1.6f;

            /// <summary>The first obstacle's pressure preparation: a percent or two of compression
            /// along the movement axis, before anything moves.</summary>
            public static float PushPreCompression = 0.025f;

            public static float PushPreStart = 0.16f;

            public static float PushPreEnd = 0.22f;

            public static float PushStart = 0.22f;

            public static float PushEnd = 0.48f;

            /// <summary>Seconds between cubes in one chain. The near one answers first; it is never
            /// a domino queue.</summary>
            public static float PushStagger = 0.016f;

            /// <summary>How much a pushed cube compresses along its axis while it moves.</summary>
            public static float PushDeformation = 0.025f;

            /// <summary>How far the contact shadow lags behind a moving cube, in cells.</summary>
            public static float PushShadowLag = 0.05f;

            /// <summary>The settle: a hard-ish but premium stop, never a float.</summary>
            public static float PushSettleAmount = 0.022f;

            public static float PushSettleEnd = 0.62f;

            /// <summary>How fast a cube that was shoved OFF the board keeps going, in cells per
            /// second. It never stops at the rim and then starts a removal of its own.</summary>
            public static float EjectSpeed = 5.5f;

            // ---- V) a side that is shut ----
            /// <summary>How long one pressure test takes - pressed, and denied.</summary>
            public static float BlockedTestDuration = 0.065f;

            /// <summary>How far the shell presses toward a shut side, in cells. One pixel.</summary>
            public static float BlockedShellPressure = 0.016f;

            /// <summary>The compression mark on the cube that will not move. No shield, no spark.
            /// </summary>
            public static float BlockedTargetResponse = 0.4f;

            /// <summary>The shell recoil away from a shut side, in cells.</summary>
            public static float BlockedRecoil = 0.016f;

            /// <summary>The dull amber pressure line travelling blocked edge -> centre -> chosen
            /// edge.</summary>
            public static float PressureRerouteDuration = 0.095f;

            public static float PressureRerouteStrength = 0.7f;

            // ---- the few flecks a hero moment throws ----
            public static int CrushFleckCount = 6;

            public static float FleckLifetime = 0.18f;
        }

        /// <summary>What the lab can switch off one at a time.</summary>
        public static class Layers
        {
            public static bool ShowJaws = true;

            public static bool ShowLaminae = true;

            public static bool ShowNullImprints = true;

            public static bool ShowShell = true;

            public static bool ShowPressureFront = true;

            public static bool ShowPushResponse = true;

            public static bool ShowFlecks = true;

            public static void AllOn()
            {
                ShowJaws = true;
                ShowLaminae = true;
                ShowNullImprints = true;
                ShowShell = true;
                ShowPressureFront = true;
                ShowPushResponse = true;
                ShowFlecks = true;
            }
        }

        // =================================================================== what it is told

        /// <summary>One quadrant of the patch, as Core reported it: where it is, and what stood
        /// there (a Tile of null means the quadrant was EMPTY and travels as a hole).</summary>
        public struct Quad
        {
            public GridPos Cell;
            public Vector2 Centre;
            public bool Occupied;
            public ClusterBurstView.Look Look;
        }

        /// <summary>One cube the press shoved, straight from PressReleaseVisuals.</summary>
        public struct Shove
        {
            public Vector2 From;
            public Vector2 To;
            public Vector2 Step;
            public bool LeftBoard;
            public int Order;
            public GridPos Target;
            public ClusterBurstView.Look Look;
        }

        /// <summary>One side the press PRESSED, and whether it gave - refusals included, because a
        /// refusal is something the player has to see happen.</summary>
        public struct Denial
        {
            public Vector2 Step;
            public bool Succeeded;
            public bool IsReroute;
            public Vector2 BlockedAt;
            public CubeKind BlockedKind;
        }

        /// <summary>The squeeze to play.</summary>
        public sealed class CompressionScene
        {
            public Quad[] Quads = new Quad[4];
            public Vector2 CompressedCentre;

            /// <summary>The cell the compressed cube stands in - the one the player chose, which
            /// is not necessarily Quads[0]. It is the cell held blank until the shell closes.</summary>
            public GridPos CompressedCell;
            public Color Slate;
            public float CellSize;
            public float CubeSize;
        }

        /// <summary>The release to play. Detonated means the shell's own part stops at the
        /// prelude and PressureVesselView takes the event over.</summary>
        public sealed class ReleaseScene
        {
            public Quad[] Quads = new Quad[4];

            /// <summary>Where the compressed cube stood and the shell opens from - the press cell
            /// the player chose, not necessarily the patch's bottom-left.</summary>
            public Vector2 AnchorCentre;
            public Color Slate;
            public float CellSize;
            public float CubeSize;
            public readonly List<Denial> Denials = new List<Denial>();
            public readonly List<Shove> Shoves = new List<Shove>();
            public PressAxis? DiagonalAxis;
            public bool Detonated;
            public Rect BoardRect;
        }

        // =================================================================== materials

        private static Material laminaMaterial;

        private static bool laminaLooked;

        private static readonly Dictionary<Sprite, Color> averages = new Dictionary<Sprite, Color>();

        /// <summary>The PressLamina material, or null when the shader cannot be had - then the
        /// laminae are plain sprites, which still reads as four going in and four coming out.
        /// </summary>
        public static Material LaminaMaterial()
        {
            if (!laminaLooked)
            {
                laminaLooked = true;
                Shader shader = Shader.Find("ProjectBlock/PressLamina");
                if (shader == null)
                {
                    shader = Resources.Load<Shader>("Shaders/PressLamina");
                }
                if (shader != null && shader.isSupported)
                {
                    laminaMaterial = new Material(shader);
                    laminaMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return laminaMaterial;
        }

        /// <summary>
        /// A face's own average colour - what the flatten pulls it toward. Taken from the PAINT the
        /// block is made of, never from the renderer tint: a painted tile's tint is white, so an
        /// average derived from it would flatten every block to white (the same trap the snake's
        /// palette fell into). Read once per sprite and kept.
        /// </summary>
        private static Color Average(ClusterBurstView.Look look)
        {
            if (look.Paint.a > 0f)
            {
                return look.Paint;
            }
            if (look.Tile == null)
            {
                return look.Colour;
            }
            Color got;
            if (averages.TryGetValue(look.Tile, out got))
            {
                return got;
            }
            got = look.Colour;
            try
            {
                Texture2D tex = look.Tile.texture;
                if (tex != null && tex.isReadable)
                {
                    Color32[] pixels = tex.GetPixels32();
                    float r = 0f;
                    float g = 0f;
                    float b = 0f;
                    int n = 0;
                    // Every sixteenth pixel: an average, not a render.
                    for (int i = 0; i < pixels.Length; i += 16)
                    {
                        if (pixels[i].a < 8)
                        {
                            continue;
                        }
                        r += pixels[i].r;
                        g += pixels[i].g;
                        b += pixels[i].b;
                        n++;
                    }
                    if (n > 0)
                    {
                        got = new Color(r / n / 255f, g / n / 255f, b / n / 255f, 1f);
                    }
                }
            }
            catch (UnityException)
            {
                // Unreadable texture: the tint is a good enough average and nothing breaks.
            }
            averages[look.Tile] = got;
            return got;
        }

        private static readonly int AverageId = Shader.PropertyToID("_Average");
        private static readonly int FlattenId = Shader.PropertyToID("_Flatten");
        private static readonly int ReliefId = Shader.PropertyToID("_Relief");
        private static readonly int SeamId = Shader.PropertyToID("_Seam");
        private static readonly int SeamColourId = Shader.PropertyToID("_SeamColour");
        private static readonly int ToneId = Shader.PropertyToID("_Tone");
        private static readonly int NegativeId = Shader.PropertyToID("_Negative");
        private static readonly int NegativeColourId = Shader.PropertyToID("_NegativeColour");
        private static readonly int PressId = Shader.PropertyToID("_Press");
        private static readonly int VolumeId = Shader.PropertyToID("_Volume");
        private static readonly int PlateEdgeId = Shader.PropertyToID("_PlateEdge");
        private static readonly int FaceHalfId = Shader.PropertyToID("_FaceHalf");

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        /// <summary>A lamina's pressed-edge line and the crush peak: dull warm steel. The one light
        /// in the effect.</summary>
        private static readonly Color SeamWarm = new Color(0.78f, 0.56f, 0.28f);

        private static readonly Color NegativePlate = new Color(0.13f, 0.15f, 0.19f);

        // Sorting: the chamber's floor marks under everything, then shadows, the laminae, the
        // shell over them (it closes OVER the stack), and the few flecks last.
        private const int FloorOrder = 3;

        private const int ShadowOrder = 4;

        private const int LaminaOrder = 6;

        private const int ShellOrder = 8;

        private const int JawOrder = 9;

        private const int MarkOrder = 10;

        private const int FleckOrder = 11;

        // =================================================================== a running event

        private sealed class Piece
        {
            public SpriteRenderer Renderer;
            public Vector2 From;
            public Vector2 To;
            public float Delay;
            public int Index;
            public bool Negative;
            public ClusterBurstView.Look Look;
            public Color Avg;
            public SpriteRenderer Shadow;
            /// <summary>A pushed cube: how far along its axis it has to go, and whether it keeps
            /// going past the rim.</summary>
            public Vector2 Step;
            public bool Eject;
            public GridPos Target;
        }

        private sealed class Run
        {
            public bool Release;
            public float Clock;
            public float Prelude;
            public float End;
            public float Cell;
            public float Cube;
            public Color Slate;
            public Vector2 Centre;
            public Rect BoardRect;
            public BoardView Board;
            public readonly List<Piece> Laminae = new List<Piece>();
            public readonly List<Piece> Pushes = new List<Piece>();
            public readonly List<SpriteRenderer> Jaws = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> JawShadows = new List<SpriteRenderer>();
            /// <summary>The four slate plates that CLOSE over the stack. A shell that faded in
            /// would be a sprite swap, which is the thing this replaces.</summary>
            public readonly List<SpriteRenderer> Shutters = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> Floor = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> Flecks = new List<SpriteRenderer>();
            public readonly List<Vector2> FleckDirs = new List<Vector2>();
            public SpriteRenderer Shell;
            public SpriteRenderer RerouteMark;
            public SpriteRenderer Front;
            public readonly List<Denial> Denials = new List<Denial>();
            public readonly List<SpriteRenderer> BlockMarks = new List<SpriteRenderer>();
            public readonly List<GridPos> Held = new List<GridPos>();
            public bool Released;
            public PressAxis? DiagonalAxis;
            public bool Detonated;
            public Color[] Memory = new Color[4];
            public GridPos Anchor;
        }

        private readonly List<Run> runs = new List<Run>();

        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

        private MaterialPropertyBlock block;

        private Material plainMaterial;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        /// <summary>True while anything is playing - what the lab waits on.</summary>
        public bool Busy
        {
            get { return runs.Count > 0; }
        }

        // =================================================================== driving it

        /// <summary>
        /// THE SQUEEZE. Four quadrants (patch order, a null Tile meaning the quadrant was empty)
        /// are flattened into laminae, drawn to <paramref name="scene"/>.CompressedCentre, stacked,
        /// crushed, and shut inside the slate shell. Everything here is geometry the caller took
        /// from PressCompressionVisuals.
        /// </summary>
        public void PlayCompression(BoardView board, CompressionScene scene)
        {
            if (scene == null || scene.CellSize <= 0f)
            {
                return;
            }
            var run = new Run
            {
                Release = false,
                Cell = scene.CellSize,
                Cube = scene.CubeSize > 0f ? scene.CubeSize : scene.CellSize,
                Slate = scene.Slate,
                Centre = scene.CompressedCentre,
                Board = board,
                End = Style.ShellSettleEnd
            };
            BuildJaws(run, scene.Quads);
            for (int i = 0; i < scene.Quads.Length && i < 4; i++)
            {
                Quad q = scene.Quads[i];
                var piece = new Piece
                {
                    From = q.Centre,
                    To = scene.CompressedCentre,
                    Index = i,
                    Negative = !q.Occupied,
                    Look = q.Look,
                    // The stack order is the rules' own patch order, so the picture inside is
                    // always assembled the same way round.
                    Delay = i * Style.LaminaStagger
                };
                piece.Avg = q.Occupied ? Average(q.Look) : NegativePlate;
                run.Memory[i] = q.Occupied
                    ? new Color(piece.Avg.r, piece.Avg.g, piece.Avg.b, 1f)
                    : new Color(0f, 0f, 0f, 0f);
                if (q.Occupied && Layers.ShowLaminae)
                {
                    piece.Renderer = Rent(q.Look.Tile, LaminaOrder, LaminaFor(q.Look));
                    piece.Shadow = Rent(ViewUtil.RoundedSprite, ShadowOrder, null);
                }
                else if (!q.Occupied && Layers.ShowNullImprints)
                {
                    // A HOLE travels too, as a negative plate. Never a cube the press invented.
                    piece.Renderer = Rent(ViewUtil.RoundedSprite, LaminaOrder, LaminaMaterial());
                }
                if (piece.Renderer != null)
                {
                    run.Laminae.Add(piece);
                }
            }
            if (Layers.ShowShell)
            {
                Material shell = CompressedCubeView.ShellMaterial();
                run.Shell = Rent(ViewUtil.RoundedSprite, ShellOrder, shell);
                if (shell == null)
                {
                    Return(run.Shell);
                    run.Shell = null;
                }
                // The four plates that travel in over the stack. Plain slate rectangles: they are
                // the shell ARRIVING, and the shell's own surface takes over once they have met.
                for (int i = 0; i < 4; i++)
                {
                    run.Shutters.Add(Rent(ViewUtil.RoundedSprite, ShellOrder + 1, null));
                }
            }
            BuildFlecks(run, scene.CompressedCentre);
            // THE BOARD HAS ALREADY PUT THE PRESSED CUBE THERE. Without this it is visible from the
            // first frame, while the laminae are still on their way to it - a cube that exists
            // before the thing that makes it. It comes back as the shell finishes closing.
            if (board != null && scene.Quads.Length > 0)
            {
                run.Held.Add(scene.CompressedCell);
                board.HoldCells(run.Held);
            }
            runs.Add(run);
            Paint(run);
        }

        /// <summary>
        /// THE RELEASE. Plays, in this order: the locks letting go, the stored colours coming up
        /// under the seams, every side Core PRESSED (refusals and the one reroute included), the
        /// shell retracting, the laminae expanding to the cells Core restored them to, and every
        /// cube Core shoved travelling its own reported step. A cube whose destination is off the
        /// board carries straight out.
        /// </summary>
        public void PlayRelease(BoardView board, ReleaseScene scene)
        {
            if (scene == null || scene.CellSize <= 0f)
            {
                return;
            }
            var run = new Run
            {
                Release = true,
                Cell = scene.CellSize,
                Cube = scene.CubeSize > 0f ? scene.CubeSize : scene.CellSize,
                Slate = scene.Slate,
                Centre = scene.AnchorCentre,
                Board = board,
                BoardRect = scene.BoardRect,
                DiagonalAxis = scene.DiagonalAxis,
                Detonated = scene.Detonated
            };
            // THE PRELUDE IS THE DENIALS. Every refused side gets its own beat, in the order the
            // rules tried them, and the reroute that follows a refusal gets one more.
            int refusals = 0;
            for (int i = 0; i < scene.Denials.Count; i++)
            {
                run.Denials.Add(scene.Denials[i]);
                if (!scene.Denials[i].Succeeded)
                {
                    refusals++;
                }
            }
            run.Prelude = refusals * Style.BlockedTestDuration
                + (refusals > 0 ? Style.PressureRerouteDuration : 0f);
            for (int i = 0; i < refusals; i++)
            {
                run.BlockMarks.Add(Rent(ViewUtil.RoundedSprite, MarkOrder, null));
            }
            if (refusals > 0)
            {
                run.RerouteMark = Rent(ViewUtil.RoundedSprite, MarkOrder, null);
            }
            if (scene.Detonated)
            {
                // The shell never opens: the prelude plays, and the failure takes the event over.
                run.End = run.Prelude;
                if (Layers.ShowShell)
                {
                    Material shutShell = CompressedCubeView.ShellMaterial();
                    run.Shell = Rent(ViewUtil.RoundedSprite, ShellOrder, shutShell);
                    if (shutShell == null)
                    {
                        Return(run.Shell);
                        run.Shell = null;
                    }
                }
                runs.Add(run);
                Paint(run);
                return;
            }
            run.End = run.Prelude + Mathf.Max(Style.RestoreSettleEnd,
                scene.Shoves.Count > 0 ? Style.PushSettleEnd : 0f);
            for (int i = 0; i < scene.Quads.Length && i < 4; i++)
            {
                Quad q = scene.Quads[i];
                var piece = new Piece
                {
                    From = scene.AnchorCentre,
                    To = q.Centre,
                    Index = i,
                    Negative = !q.Occupied,
                    Look = q.Look,
                    Delay = i * Style.LaminaStagger
                };
                piece.Avg = q.Occupied ? Average(q.Look) : NegativePlate;
                run.Memory[i] = q.Occupied
                    ? new Color(piece.Avg.r, piece.Avg.g, piece.Avg.b, 1f)
                    : new Color(0f, 0f, 0f, 0f);
                if (q.Occupied && Layers.ShowLaminae)
                {
                    piece.Renderer = Rent(q.Look.Tile, LaminaOrder, LaminaFor(q.Look));
                    piece.Shadow = Rent(ViewUtil.RoundedSprite, ShadowOrder, null);
                }
                else if (!q.Occupied && Layers.ShowNullImprints)
                {
                    piece.Renderer = Rent(ViewUtil.RoundedSprite, LaminaOrder, LaminaMaterial());
                }
                if (piece.Renderer != null)
                {
                    run.Laminae.Add(piece);
                }
            }
            if (Layers.ShowShell)
            {
                Material shell = CompressedCubeView.ShellMaterial();
                run.Shell = Rent(ViewUtil.RoundedSprite, ShellOrder, shell);
                if (shell == null)
                {
                    Return(run.Shell);
                    run.Shell = null;
                }
                // The same four plates, run the other way: they RETRACT off the stack.
                for (int i = 0; i < 4; i++)
                {
                    run.Shutters.Add(Rent(ViewUtil.RoundedSprite, ShellOrder + 1, null));
                }
            }
            // THE PUSH CHAIN, exactly as the rules moved it. Order is the distance from the press,
            // so the near cube answers first without anything here counting cubes.
            for (int i = 0; i < scene.Shoves.Count; i++)
            {
                Shove s = scene.Shoves[i];
                var piece = new Piece
                {
                    From = s.From,
                    To = s.To,
                    Step = s.Step,
                    Eject = s.LeftBoard,
                    Target = s.Target,
                    Look = s.Look,
                    Delay = s.Order * Style.PushStagger
                };
                piece.Index = s.Order;
                piece.Avg = Average(s.Look);
                piece.Renderer = Rent(s.Look.Tile, LaminaOrder, LaminaFor(s.Look));
                piece.Shadow = Rent(ViewUtil.RoundedSprite, ShadowOrder, null);
                run.Pushes.Add(piece);
                if (!s.LeftBoard)
                {
                    run.Held.Add(s.Target);
                }
            }
            if (run.Pushes.Count > 0 && Layers.ShowPressureFront)
            {
                run.Front = Rent(ViewUtil.RoundedSprite, FloorOrder, null);
            }
            // THE RESTORED CELLS ARE HELD TOO. The rules have already put the four cubes back, so
            // without this the board shows them standing in their cells while the laminae are still
            // inside the shell on their way out - and a cube a lamina is about to become must not
            // be on the board before it gets there. The push destinations are held for the older
            // reason: two cubes are never in one cell (the same bargain BossMoveView makes).
            for (int i = 0; i < scene.Quads.Length && i < 4; i++)
            {
                if (scene.Quads[i].Occupied)
                {
                    run.Held.Add(scene.Quads[i].Cell);
                }
            }
            if (board != null && run.Held.Count > 0)
            {
                board.HoldCells(run.Held);
            }
            runs.Add(run);
            Paint(run);
        }

        /// <summary>Ends everything at once - the lab resyncing, or the round going away.</summary>
        public void Stop()
        {
            for (int i = 0; i < runs.Count; i++)
            {
                Release(runs[i]);
            }
            runs.Clear();
        }

        // =================================================================== the clock

        private void Update()
        {
            if (runs.Count == 0)
            {
                return;
            }
            float dt = Time.deltaTime;
            for (int i = runs.Count - 1; i >= 0; i--)
            {
                Run run = runs[i];
                run.Clock += dt;
                Paint(run);
                if (run.Clock >= run.End)
                {
                    Release(run);
                    runs.RemoveAt(i);
                }
            }
        }

        /// <summary>0..1 across a window, flat outside it.</summary>
        private static float Span(float t, float a, float b)
        {
            if (b <= a)
            {
                return t >= b ? 1f : 0f;
            }
            return Mathf.Clamp01((t - a) / (b - a));
        }

        private static float Ease(float k)
        {
            return k * k * (3f - 2f * k);
        }

        /// <summary>Slow pressure build, strong controlled movement, a hard-ish premium settle -
        /// the hydraulic feel. Never a conveyor's smooth float.</summary>
        private static float Hydraulic(float k)
        {
            k = Mathf.Clamp01(k);
            if (k < 0.35f)
            {
                float a = k / 0.35f;
                return 0.1f * a * a;
            }
            float b = (k - 0.35f) / 0.65f;
            return 0.1f + 0.9f * (1f - Mathf.Pow(1f - b, 2.4f));
        }

        private void Paint(Run run)
        {
            if (run.Release)
            {
                PaintRelease(run);
            }
            else
            {
                PaintCompression(run);
            }
        }

        // =================================================================== the squeeze

        private void PaintCompression(Run run)
        {
            float t = run.Clock;
            float jaws = Span(t, 0f, Style.JawAppearDuration);
            float build = Span(t, Style.PressureBuildStart, Style.PressureBuildEnd);
            float flat = Span(t, Style.FlattenStart, Style.FlattenEnd);
            float crushed = Span(t, Style.CrushStart, Style.StackHoldEnd);
            float shell = Span(t, Style.ShellCloseStart, Style.ShellCloseEnd);
            float settle = Span(t, Style.ShellCloseEnd, Style.ShellSettleEnd);

            PaintJaws(run, jaws, build, shell);

            for (int i = 0; i < run.Laminae.Count; i++)
            {
                Piece p = run.Laminae[i];
                float local = Mathf.Max(0f, t - p.Delay);
                float myFlat = Span(local, Style.FlattenStart, Style.FlattenEnd);
                float travel = Ease(Span(local, Style.ConvergeStart, Style.ConvergeEnd));
                // The stack: each lamina parks at its own height above the destination, and the
                // crush drives the spacing to nothing.
                float slot = (p.Index - 1.5f) * Style.StackSpacing * run.Cell;
                slot *= 1f - crushed * Style.CrushStrength;
                // A pressure offset toward the middle BEFORE it leaves: it has not set off yet.
                Vector2 toward = (run.Centre - p.From).normalized;
                Vector2 held = p.From + toward * (build * Style.PressureOffset * run.Cell);
                Vector2 at = Arc(held, run.Centre, travel, run.Cell);
                at.y += slot;
                float thin = Mathf.Lerp(1f, Style.LaminaThickness,
                    myFlat * Style.FlattenStrength);
                float wide = 1f + myFlat * Style.LaminaSpread;
                if (p.Negative)
                {
                    thin = Mathf.Lerp(1f, Style.NullImprintThickness, myFlat);
                }
                float alpha = p.Negative
                    ? Style.NullImprintStrength * Mathf.Min(myFlat * 2.2f, 1f) * (1f - crushed * 0.5f)
                    : 1f;
                // Once the shell has shut over them the laminae are inside: they stop being drawn,
                // and the shell's own stored-colour memory is all that is left of them.
                alpha *= 1f - Span(t, Style.ShellCloseStart + 0.03f, Style.ShellCloseEnd);
                PaintLamina(run, p, at, thin, wide, myFlat, 0f, alpha,
                    crushed * Style.CrushSeamLight, build, travel);
            }

            PaintShell(run, shell, settle, crushed, 0f, Vector4.zero, 0f, 0f);
            PaintFlecks(run, Span(t, Style.CrushStart, Style.CrushStart + Style.FleckLifetime),
                run.Cell);
            // The shell has shut: the board's own pressed cube is the truth from here, and the
            // standing press's layer takes it over.
            if (!run.Released && run.Board != null && run.Held.Count > 0
                && t >= Style.ShellCloseEnd)
            {
                run.Board.ReleaseCells(run.Held);
                run.Released = true;
            }
        }

        // =================================================================== the release

        private void PaintRelease(Run run)
        {
            float t = run.Clock;
            // ---- the prelude: every side the rules pressed, in their order ----
            float preludeDone = run.Prelude > 0f ? Mathf.Clamp01(t / run.Prelude) : 1f;
            Vector4 edgeLead = Vector4.zero;
            float stress = 0f;
            int refusal = 0;
            for (int i = 0; i < run.Denials.Count; i++)
            {
                Denial d = run.Denials[i];
                if (d.Succeeded)
                {
                    continue;
                }
                float a = refusal * Style.BlockedTestDuration;
                float k = Span(t, a, a + Style.BlockedTestDuration);
                // Pressed, denied, and a pixel of recoil - with no movement at all in between.
                float push = k < 0.6f ? Ease(k / 0.6f) : 1f - Ease((k - 0.6f) / 0.4f);
                float recoil = k < 0.6f ? 0f : Ease((k - 0.6f) / 0.4f);
                edgeLead += SideVector(d.Step,
                    push * Style.BlockedShellPressure * run.Cell
                        - recoil * Style.BlockedRecoil * run.Cell);
                stress = Mathf.Max(stress, k * 0.3f);
                if (refusal < run.BlockMarks.Count)
                {
                    PaintBlockMark(run, run.BlockMarks[refusal], d, push);
                }
                refusal++;
            }
            if (run.RerouteMark != null)
            {
                float a = refusal * Style.BlockedTestDuration;
                PaintReroute(run, Span(t, a, a + Style.PressureRerouteDuration));
            }
            if (run.Detonated)
            {
                // Shut, strained, and handed over. PressureVesselView does the rest.
                PaintShell(run, 0f, 0f, 0f, Style.InternalColorMemory * preludeDone * 0.3f,
                    edgeLead, stress, 0f);
                return;
            }

            float local = Mathf.Max(0f, t - run.Prelude);
            float unlock = Span(local, 0f, Style.UnlockDuration);
            float tension = Span(local, Style.TensionStart, Style.TensionEnd);
            float open = Span(local, Style.ShellOpenStart, Style.ShellOpenEnd);
            float front = Span(local, Style.PushFrontStart, Style.PushFrontEnd);

            // THE RELEASE'S OWN SIDE LEADS. The axis is Core's; this only draws it.
            if (run.DiagonalAxis.HasValue)
            {
                Vector2 step = run.DiagonalAxis.Value == PressAxis.Horizontal
                    ? new Vector2(1f, 0f)
                    : new Vector2(0f, 1f);
                edgeLead += SideVector(step, tension * Style.DirectionalLead * run.Cell);
            }
            // Every edge strains a little before it opens - per edge, never a uniform scale.
            float strain = tension * Style.ShellTension * run.Cell;
            edgeLead += new Vector4(strain, strain, strain, strain);
            PaintShell(run, 1f, 0f, 0f,
                Style.InternalColorMemory * Mathf.Min(tension * 1.4f, 1f) * (1f - open),
                edgeLead, stress, open);

            for (int i = 0; i < run.Laminae.Count; i++)
            {
                Piece p = run.Laminae[i];
                float mine = Mathf.Max(0f, local - p.Delay);
                float travel = Ease(Span(mine, Style.RestoreTravelStart, Style.RestoreTravelEnd));
                float relief = Span(mine, Style.RestoreReliefStart, Style.RestoreReliefEnd);
                float slot = (p.Index - 1.5f) * Style.StackSpacing * run.Cell * (1f - travel);
                Vector2 at = Arc(p.From, p.To, travel, run.Cell);
                at.y += slot;
                float thin = Mathf.Lerp(Style.LaminaThickness, 1f, Ease(relief));
                float wide = 1f + (1f - relief) * Style.LaminaSpread;
                float alpha = 1f;
                if (p.Negative)
                {
                    thin = Style.NullImprintThickness;
                    // A HOLE comes back as a hole: its imprint reaches the cell and goes out.
                    float showing = Span(mine, Style.RestoreTravelStart, Style.RestoreTravelEnd);
                    float gone = Span(mine, Style.RestoreReliefEnd,
                        Style.RestoreReliefEnd + Style.NullRestoreImprint);
                    alpha = Style.NullImprintStrength * showing * (1f - gone);
                }
                // Before the shell cracks, the laminae are still inside it.
                alpha *= Span(mine, Style.ShellOpenStart, Style.ShellOpenStart + 0.04f);
                PaintLamina(run, p, at, thin, wide, 1f - relief, relief, alpha,
                    (1f - relief) * 0.35f, 0f, travel);
            }

            PaintFront(run, front);
            PaintPushes(run, local);
            // The cells come back the moment their contents have actually arrived: the laminae
            // when their volume is back, the shoved cubes when the chain has finished moving.
            if (!run.Released && run.Board != null && run.Held.Count > 0
                && local >= Mathf.Max(Style.RestoreReliefEnd, Style.PushEnd))
            {
                run.Board.ReleaseCells(run.Held);
                run.Released = true;
            }
        }

        private void PaintPushes(Run run, float local)
        {
            for (int i = 0; i < run.Pushes.Count; i++)
            {
                Piece p = run.Pushes[i];
                float mine = Mathf.Max(0f, local - p.Delay);
                float pre = Span(mine, Style.PushPreStart, Style.PushPreEnd);
                float move = Hydraulic(Span(mine, Style.PushStart, Style.PushEnd));
                float settle = Span(mine, Style.PushEnd, Style.PushSettleEnd);
                Vector2 step = p.Step.sqrMagnitude > 0f ? p.Step.normalized : Vector2.right;
                Vector2 at;
                if (p.Eject)
                {
                    // OFF THE BOARD IS ONE MOVEMENT. It does not stop at the rim and then start a
                    // removal: it keeps the velocity it was shoved with and goes.
                    float travelled = move * (p.To - p.From).magnitude
                        + Mathf.Max(0f, mine - Style.PushEnd) * Style.EjectSpeed * run.Cell;
                    at = p.From + step * travelled;
                }
                else
                {
                    at = Vector2.Lerp(p.From, p.To, move);
                    // A hard-ish settle: a touch past the cell and back.
                    at += step * (Mathf.Sin(settle * Mathf.PI) * Style.PushSettleAmount * run.Cell
                        * (1f - settle) * -1f);
                }
                // A percent or two of pressure preparation, then a little compression along the
                // axis while it travels. Never a cartoon squash.
                float squeeze = pre * Style.PushPreCompression
                    + Mathf.Sin(Mathf.Clamp01(move) * Mathf.PI) * Style.PushDeformation;
                float alongScale = 1f - squeeze;
                float acrossScale = 1f + squeeze * 0.6f;
                bool horizontal = Mathf.Abs(step.x) > Mathf.Abs(step.y);
                float w = run.Cube * (horizontal ? alongScale : acrossScale);
                float h = run.Cube * (horizontal ? acrossScale : alongScale);
                float alpha = 1f;
                if (p.Eject && Layers.ShowPushResponse)
                {
                    // Clipped by the board it went over: a little past the edge, fading.
                    float past = Outside(run.BoardRect, at);
                    alpha = 1f - Mathf.Clamp01(past / (run.Cell * 0.7f));
                }
                if (p.Renderer != null)
                {
                    p.Renderer.transform.localPosition = new Vector3(at.x, at.y, 0f);
                    Fit(p.Renderer, w, h);
                    Color tint = p.Look.Colour;
                    tint.a = alpha;
                    p.Renderer.color = tint;
                    if (LaminaMaterial() != null)
                    {
                        block.Clear();
                        block.SetColor(AverageId, p.Avg);
                        block.SetFloat(FlattenId, 0f);
                        block.SetFloat(ReliefId, 1f);
                        block.SetFloat(SeamId, 0f);
                        block.SetColor(SeamColourId, SeamWarm);
                        block.SetVector(ToneId, new Vector2(0f, 0f));
                        block.SetFloat(NegativeId, 0f);
                        block.SetColor(NegativeColourId, NegativePlate);
                        block.SetFloat(PressId, 0f);
                        block.SetFloat(FaceHalfId,
                            CompressedCubeView.FaceHalfOf(p.Renderer));
                        p.Renderer.SetPropertyBlock(block);
                    }
                }
                if (p.Shadow != null)
                {
                    // The shadow lags the cube and catches up as it settles.
                    bool moving = move > 0.001f && move < 0.999f;
                    Vector2 lag = at - step * (moving ? Style.PushShadowLag * run.Cell : 0f);
                    p.Shadow.transform.localPosition = new Vector3(lag.x,
                        lag.y - run.Cube * 0.05f, 0f);
                    Fit(p.Shadow, run.Cube * 0.86f, run.Cube * 0.86f);
                    p.Shadow.color = new Color(0f, 0f, 0f,
                        0.26f * alpha * (Layers.ShowPushResponse ? 1f : 0f));
                }
            }
        }

        // =================================================================== the parts

        private void BuildJaws(Run run, Quad[] quads)
        {
            if (!Layers.ShowJaws)
            {
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                run.Jaws.Add(Rent(ViewUtil.RoundedSprite, JawOrder, null));
                // Each jaw sits ON the board, so it drops its own small contact shadow.
                run.JawShadows.Add(Rent(ViewUtil.RoundedSprite, ShadowOrder, null));
            }
            // The chamber floor: the four cells darken a little as the jaws lock.
            for (int i = 0; i < quads.Length && i < 4; i++)
            {
                SpriteRenderer floor = Rent(ViewUtil.RoundedSprite, FloorOrder, null);
                floor.transform.localPosition = new Vector3(quads[i].Centre.x,
                    quads[i].Centre.y, 0f);
                run.Floor.Add(floor);
            }
        }

        /// <summary>
        /// The four pressure jaws on the patch's OUTER edges. They come out of the board by a few
        /// pixels rather than fading in, and they are short-lived graphic lips - no piston, no
        /// cylinder, no machine. The patch is two cells across, so the jaws bracket a 2x2.
        ///
        /// AND THEY CLOSE TOWARD THE ANCHOR, not toward the patch's middle. The rules put the
        /// compressed cube on the patch's BOTTOM-LEFT cell, so a chamber that squeezed symmetrically
        /// would be saying one thing while the material did another - jaws driving to the centre and
        /// laminae sliding off to a corner, which is what the first pass looked like. The chamber's
        /// own centre travels from the patch's middle to the anchor as the pressure builds, so the
        /// mechanism is visibly what drives the material where Core actually puts it.
        /// </summary>
        private void PaintJaws(Run run, float appear, float build, float shell)
        {
            if (run.Jaws.Count < 4)
            {
                return;
            }
            float k = Ease(appear);
            // They RETRACT to the board rather than snapping out of existence.
            float retract = Ease(Span(run.Clock, Style.ShellSettleEnd - Style.JawRetractDuration,
                Style.ShellSettleEnd));
            float alive = 1f - retract;
            float thick = Style.JawDepth * run.Cell;
            // A jaw covers only part of its side, so four of them are four PARTS rather than a
            // rectangle around the patch.
            float length = run.Cell * 2f * Mathf.Clamp01(Style.JawLength);
            // Out of the board as they form, in as the pressure builds, and away at the end.
            float emerge = (1f - k) * Style.JawEmerge * run.Cell;
            float press = Ease(Span(run.Clock, Style.PressureBuildStart, Style.CrushStart))
                * Style.JawInset * run.Cell;
            float drive = Ease(Span(run.Clock, Style.PressureBuildStart, Style.ConvergeEnd));
            // The chamber closes onto the ANCHOR, which is the cell the rules compress into.
            Vector2 patchMiddle = run.Centre + new Vector2(run.Cell * 0.5f, run.Cell * 0.5f);
            Vector2 c = Vector2.Lerp(patchMiddle, run.Centre, drive);
            float reach = run.Cell * (1f - 0.3f * drive) + Style.JawInset * run.Cell
                - press + emerge + retract * run.Cell * 0.5f;
            float a = k * alive;
            PaintJawPair(run, 0, c + new Vector2(0f, reach), length, thick, a);
            PaintJawPair(run, 1, c - new Vector2(0f, reach), length, thick, a);
            PaintJawPair(run, 2, c + new Vector2(reach, 0f), thick, length, a);
            PaintJawPair(run, 3, c - new Vector2(reach, 0f), thick, length, a);
            for (int i = 0; i < run.Floor.Count; i++)
            {
                Fit(run.Floor[i], run.Cell * 0.94f, run.Cell * 0.94f);
                float dark = Style.ChamberDarkening * Mathf.Max(k, build) * alive;
                run.Floor[i].color = new Color(0f, 0f, 0f, dark);
            }
        }

        private void PaintJawPair(Run run, int index, Vector2 at, float w, float h, float alpha)
        {
            PaintJaw(run.Jaws[index], at, w, h, alpha);
            if (index < run.JawShadows.Count)
            {
                SpriteRenderer sh = run.JawShadows[index];
                if (sh != null)
                {
                    sh.transform.localPosition =
                        new Vector3(at.x, at.y - run.Cell * 0.035f, 0f);
                    Fit(sh, w * 1.06f, h * 1.06f);
                    sh.color = new Color(0f, 0f, 0f, Style.JawShadowStrength * alpha);
                }
            }
        }

        private void PaintJaw(SpriteRenderer jaw, Vector2 at, float w, float h, float alpha)
        {
            if (jaw == null)
            {
                return;
            }
            jaw.transform.localPosition = new Vector3(at.x, at.y, 0f);
            Fit(jaw, w, h);
            Color c = Style.JawColour;
            c.a = alpha;
            jaw.color = c;
        }

        private void PaintLamina(Run run, Piece p, Vector2 at, float thin, float wide, float flatten,
            float relief, float alpha, float seam, float build, float travel)
        {
            if (p.Renderer == null)
            {
                return;
            }
            p.Renderer.transform.localPosition = new Vector3(at.x, at.y, 0f);
            Fit(p.Renderer, run.Cube * wide, run.Cube * thin);
            Color tint = p.Negative ? Color.white : p.Look.Colour;
            tint.a = Mathf.Clamp01(alpha);
            p.Renderer.color = tint;
            if (LaminaMaterial() != null)
            {
                block.Clear();
                block.SetColor(AverageId, p.Avg);
                block.SetFloat(FlattenId, flatten * Style.FlattenStrength);
                block.SetFloat(ReliefId, relief);
                block.SetFloat(SeamId, seam);
                block.SetColor(SeamColourId, SeamWarm);
                block.SetVector(ToneId, new Vector2(travel * Style.TravelDesaturation,
                    travel * Style.TravelCold));
                block.SetFloat(NegativeId, p.Negative ? 1f : 0f);
                block.SetColor(NegativeColourId, NegativePlate);
                block.SetFloat(PressId, flatten);
                // THE BEVEL IS WHAT GOES. A cube has volume in its shading; a pressed plate does
                // not - so the flatten dial takes _Volume down rather than squashing the sprite,
                // and the plate keeps a little surface light so it stays a material.
                float vol = Mathf.Lerp(1f, Style.LaminaSurfaceHighlight,
                    Mathf.Clamp01(flatten) * (1f - Mathf.Clamp01(relief)));
                block.SetFloat(VolumeId, p.Negative ? 0f : vol);
                // And the one piece of depth it DOES have: a lip along its bottom edge.
                block.SetFloat(PlateEdgeId, p.Negative
                    ? 0f
                    : Style.LaminaEdgeShadow * Mathf.Clamp01(flatten) * (1f - relief));
                block.SetFloat(FaceHalfId, CompressedCubeView.FaceHalfOf(p.Renderer));
                p.Renderer.SetPropertyBlock(block);
            }
            if (p.Shadow != null)
            {
                // ON THE FLOOR while it is still in its cell, and on the LAYER BELOW once it is in
                // the stack - a plate resting on another plate is what makes four of them read as
                // four rather than as one blob.
                float grounded = Mathf.Clamp01(1f - travel * 3f);
                float stacked = Mathf.Clamp01((travel - 0.6f) / 0.4f);
                float drop = run.Cube * (grounded > stacked ? 0.05f : 0.022f);
                p.Shadow.transform.localPosition = new Vector3(at.x, at.y - drop, 0f);
                float s2 = run.Cube * (0.88f - Style.PressureShadowTighten * build);
                Fit(p.Shadow, s2 * (stacked > 0f ? 0.98f : 1f), s2 * thin * 1.1f + run.Cube * 0.06f);
                float strength = Mathf.Max(0.3f * grounded, Style.StackLayerShadow * stacked);
                p.Shadow.color = new Color(0f, 0f, 0f, strength * Mathf.Clamp01(alpha));
            }
        }

        /// <summary>
        /// The slate shell. On the squeeze it CLOSES over the stack - the four edge shutters drawn
        /// in and the centre filled, so it is never a cube fading in. On the release it is already
        /// shut and retracts: the seams widen, the coverage comes off, and the laminae are under it.
        /// </summary>
        private void PaintShell(Run run, float close, float settle, float crushed, float memory,
            Vector4 edgeLead, float stress, float open)
        {
            if (run.Shell == null)
            {
                return;
            }
            float k = Ease(Mathf.Clamp01(close));
            // THE SHELL DOES NOT FADE IN. Four plates travel in over the stack (PaintShutters) and
            // the surface behind them is only drawn as far as they have actually covered - so the
            // colour inside is shut away rather than replaced by a grey sprite appearing.
            float size = run.Cube;
            // The seam lock's inward punch, then the settle back.
            float punch = Mathf.Sin(Mathf.Clamp01(settle) * Mathf.PI) * Style.ShellSettleAmount;
            size *= 1f - punch;
            if (run.Release)
            {
                // Retracting: the coverage comes off the plate from its edges inward.
                size = run.Cube * (1f + open * 0.12f);
            }
            run.Shell.transform.localPosition = new Vector3(run.Centre.x, run.Centre.y, 0f);
            Fit(run.Shell, size, size);
            PaintShutters(run, close, open);
            CompressedCubeView.ShellLook look = CompressedCubeView.ShellLook.Rest(run.Slate);
            look.Q0 = run.Memory[0];
            look.Q1 = run.Memory[1];
            look.Q2 = run.Memory[2];
            look.Q3 = run.Memory[3];
            look.Memory = memory;
            look.Stress = stress;
            look.EdgeLead = edgeLead;
            // A shell that is OPENING shows its quadrant seams wide; one that is closing presses
            // them in as the crush finishes.
            look.SeamWidth *= 1f + open * 5f;
            look.SeamDepth *= 1f + crushed * 0.4f;
            // Coverage follows the shutters: nothing until they are most of the way in, then the
            // surface completes behind them.
            look.Alpha = run.Release
                ? 1f - Ease(Mathf.Clamp01(open))
                : Ease(Mathf.Clamp01((k - 0.45f) / 0.55f));
            CompressedCubeView.ApplyShell(run.Shell, block, look);
        }

        /// <summary>
        /// The four slate plates CLOSING over the stack - left, right, top and bottom travelling in
        /// from outside the cell until they meet. This is the difference between a shell that was
        /// BUILT on top of the laminae and a grey cube that appeared where they used to be, and the
        /// second is what the first pass looked like. On the release they run the other way.
        /// </summary>
        private void PaintShutters(Run run, float close, float open)
        {
            if (run.Shutters.Count < 4)
            {
                return;
            }
            float k = run.Release ? 1f - Ease(Mathf.Clamp01(open)) : Ease(Mathf.Clamp01(close));
            float half = run.Cube * 0.5f;
            // Each plate is half the cube across and travels from a cube's width out to its own
            // half of the face.
            float travel = Mathf.Lerp(run.Cube * 1.15f, half * 0.5f, k);
            float lip = run.Cube * 0.52f;
            Color slate = run.Slate;
            // A touch darker at the leading edge, so a plate reads as a plate arriving.
            Color face = Color.Lerp(slate, Color.black, 0.12f);
            PaintShutter(run.Shutters[0], run.Centre + new Vector2(travel, 0f), half, lip, face, k);
            PaintShutter(run.Shutters[1], run.Centre - new Vector2(travel, 0f), half, lip, face, k);
            PaintShutter(run.Shutters[2], run.Centre + new Vector2(0f, travel), lip, half, slate, k);
            PaintShutter(run.Shutters[3], run.Centre - new Vector2(0f, travel), lip, half, slate, k);
        }

        private void PaintShutter(SpriteRenderer r, Vector2 at, float w, float h, Color colour,
            float k)
        {
            if (r == null)
            {
                return;
            }
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            Fit(r, w, h);
            Color c = colour;
            // They are solid the moment they exist - a translucent shutter is a fade by another
            // name. They only stop being drawn once the shell's own surface has taken over.
            c.a = k > 0.02f ? 1f - Ease(Mathf.Clamp01((k - 0.8f) / 0.2f)) : 0f;
            r.color = c;
        }

        /// <summary>The mechanical pressure front: a low-opacity slate compression band running out
        /// along the axis over the cells' floor. No beam, no ring.</summary>
        private void PaintFront(Run run, float k)
        {
            if (run.Front == null || run.Pushes.Count == 0)
            {
                return;
            }
            Piece first = run.Pushes[0];
            Vector2 step = first.Step.sqrMagnitude > 0f ? first.Step.normalized : Vector2.right;
            float reach = Style.PushFrontReach * run.Cell;
            Vector2 at = run.Centre + step * (Ease(k) * reach);
            bool horizontal = Mathf.Abs(step.x) > Mathf.Abs(step.y);
            run.Front.transform.localPosition = new Vector3(at.x, at.y, 0f);
            Fit(run.Front, horizontal ? run.Cell * 0.5f : run.Cell * 0.92f,
                horizontal ? run.Cell * 0.92f : run.Cell * 0.5f);
            float alpha = Style.PushFrontStrength * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
            run.Front.color = new Color(0.46f, 0.5f, 0.58f, alpha);
        }

        /// <summary>A side pressed and refused: a compression mark on the face of the cube that
        /// will not budge, on the edge the press is on. No shield, no spark, and no movement.
        /// </summary>
        private void PaintBlockMark(Run run, SpriteRenderer mark, Denial d, float k)
        {
            if (mark == null)
            {
                return;
            }
            Vector2 step = d.Step.sqrMagnitude > 0f ? d.Step.normalized : Vector2.right;
            // On the contact face - the side of the blocker the pressure arrives from.
            Vector2 at = d.BlockedAt - step * (run.Cube * 0.44f);
            bool horizontal = Mathf.Abs(step.x) > Mathf.Abs(step.y);
            mark.transform.localPosition = new Vector3(at.x, at.y, 0f);
            Fit(mark, horizontal ? run.Cube * 0.12f : run.Cube * 0.78f,
                horizontal ? run.Cube * 0.78f : run.Cube * 0.12f);
            // Obsidian and gold do not look alike, so the mark takes the cube's own weight: a
            // deeper mark on the darker stone.
            float weight = d.BlockedKind == CubeKind.Obsidian ? 0.5f : 0.36f;
            mark.color = new Color(0f, 0f, 0f,
                weight * Style.BlockedTargetResponse * Mathf.Clamp01(k));
        }

        /// <summary>The pressure being rerouted: a dull amber line travelling blocked edge -> centre
        /// -> the side Core actually opened on, over the shell's surface.</summary>
        private void PaintReroute(Run run, float k)
        {
            if (run.RerouteMark == null)
            {
                return;
            }
            Vector2 from = Vector2.zero;
            for (int i = 0; i < run.Denials.Count; i++)
            {
                if (!run.Denials[i].Succeeded)
                {
                    from = run.Denials[i].Step.sqrMagnitude > 0f
                        ? run.Denials[i].Step.normalized
                        : Vector2.right;
                    break;
                }
            }
            Vector2 to = Vector2.zero;
            if (run.DiagonalAxis.HasValue)
            {
                to = run.DiagonalAxis.Value == PressAxis.Horizontal
                    ? new Vector2(1f, 0f)
                    : new Vector2(0f, 1f);
            }
            // Out to the shut edge, back through the middle, out to the one that opened.
            float half = run.Cube * 0.4f;
            Vector2 at = k < 0.5f
                ? from * half * (1f - k / 0.5f)
                : to * half * ((k - 0.5f) / 0.5f);
            run.RerouteMark.transform.localPosition =
                new Vector3(run.Centre.x + at.x, run.Centre.y + at.y, 0f);
            Fit(run.RerouteMark, run.Cube * 0.26f, run.Cube * 0.1f);
            Color amber = CompressedCubeView.PressureAmber;
            amber.a = Style.PressureRerouteStrength * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
            run.RerouteMark.color = amber;
        }

        private void BuildFlecks(Run run, Vector2 at)
        {
            if (!Layers.ShowFlecks)
            {
                return;
            }
            int n = Mathf.Max(0, Style.CrushFleckCount);
            for (int i = 0; i < n; i++)
            {
                run.Flecks.Add(Rent(ViewUtil.RoundedSprite, FleckOrder, null));
                // Out of the seams, flat: the crush squeezes them sideways, not upward in a spray.
                float a = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f;
                run.FleckDirs.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.35f));
            }
        }

        private void PaintFlecks(Run run, float k, float cell)
        {
            for (int i = 0; i < run.Flecks.Count; i++)
            {
                SpriteRenderer f = run.Flecks[i];
                if (f == null)
                {
                    continue;
                }
                Vector2 dir = run.FleckDirs[i];
                Vector2 at = run.Centre + dir * (Ease(k) * cell * 0.5f);
                f.transform.localPosition = new Vector3(at.x, at.y, 0f);
                float s = cell * 0.05f * (1f - k);
                Fit(f, s * 2.2f, s);
                Color c = SeamWarm;
                c.a = 0.5f * (1f - k) * (k > 0f ? 1f : 0f);
                f.color = c;
            }
        }

        // =================================================================== helpers

        /// <summary>A small inward bow on the way to the stack: a guide rail, never an orbit.
        /// </summary>
        private static Vector2 Arc(Vector2 from, Vector2 to, float k, float cell)
        {
            Vector2 straight = Vector2.Lerp(from, to, k);
            Vector2 d = to - from;
            if (d.sqrMagnitude < 1e-6f)
            {
                return straight;
            }
            Vector2 normal = new Vector2(-d.y, d.x).normalized;
            float bow = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * Style.ConvergeArc * cell;
            return straight + normal * bow;
        }

        /// <summary>A step turned into the shell's per-edge push vector (x = +x, y = +y, z = -x,
        /// w = -y).</summary>
        private static Vector4 SideVector(Vector2 step, float amount)
        {
            var v = Vector4.zero;
            if (Mathf.Abs(step.x) > Mathf.Abs(step.y))
            {
                if (step.x > 0f)
                {
                    v.x = amount;
                }
                else
                {
                    v.z = amount;
                }
            }
            else
            {
                if (step.y > 0f)
                {
                    v.y = amount;
                }
                else
                {
                    v.w = amount;
                }
            }
            return v;
        }

        private static float Outside(Rect rect, Vector2 at)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return 0f;
            }
            float dx = Mathf.Max(rect.xMin - at.x, at.x - rect.xMax);
            float dy = Mathf.Max(rect.yMin - at.y, at.y - rect.yMax);
            return Mathf.Max(0f, Mathf.Max(dx, dy));
        }

        private Material LaminaFor(ClusterBurstView.Look look)
        {
            Material lamina = LaminaMaterial();
            return lamina != null ? lamina : ViewUtil.TileMaterial(look.Tile);
        }

        private static void Fit(SpriteRenderer r, float width, float height)
        {
            if (r == null || r.sprite == null)
            {
                return;
            }
            Vector2 unit = r.sprite.bounds.size;
            r.transform.localScale = new Vector3(width / Mathf.Max(unit.x, 0.0001f),
                height / Mathf.Max(unit.y, 0.0001f), 1f);
        }

        private SpriteRenderer Rent(Sprite sprite, int order, Material material)
        {
            SpriteRenderer r;
            if (spare.Count > 0)
            {
                r = spare.Pop();
            }
            else
            {
                var go = new GameObject("Press");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plainMaterial == null)
                {
                    plainMaterial = r.sharedMaterial;
                }
            }
            Material wanted = material != null ? material : plainMaterial;
            if (wanted != null && r.sharedMaterial != wanted)
            {
                r.sharedMaterial = wanted;
            }
            r.SetPropertyBlock(null);
            r.maskInteraction = SpriteMaskInteraction.None;
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = Clear;
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = Vector3.one;
            r.enabled = true;
            return r;
        }

        private void Return(SpriteRenderer r)
        {
            if (r == null)
            {
                return;
            }
            r.enabled = false;
            r.SetPropertyBlock(null);
            spare.Push(r);
        }

        private void Release(Run run)
        {
            for (int i = 0; i < run.Laminae.Count; i++)
            {
                Return(run.Laminae[i].Renderer);
                Return(run.Laminae[i].Shadow);
            }
            for (int i = 0; i < run.Pushes.Count; i++)
            {
                Return(run.Pushes[i].Renderer);
                Return(run.Pushes[i].Shadow);
            }
            for (int i = 0; i < run.Jaws.Count; i++)
            {
                Return(run.Jaws[i]);
            }
            for (int i = 0; i < run.JawShadows.Count; i++)
            {
                Return(run.JawShadows[i]);
            }
            for (int i = 0; i < run.Shutters.Count; i++)
            {
                Return(run.Shutters[i]);
            }
            for (int i = 0; i < run.Floor.Count; i++)
            {
                Return(run.Floor[i]);
            }
            for (int i = 0; i < run.Flecks.Count; i++)
            {
                Return(run.Flecks[i]);
            }
            for (int i = 0; i < run.BlockMarks.Count; i++)
            {
                Return(run.BlockMarks[i]);
            }
            Return(run.Shell);
            Return(run.Front);
            Return(run.RerouteMark);
            if (!run.Released && run.Board != null && run.Held.Count > 0)
            {
                run.Board.ReleaseCells(run.Held);
                run.Released = true;
            }
            run.Laminae.Clear();
            run.Pushes.Clear();
            run.Jaws.Clear();
            run.JawShadows.Clear();
            run.Shutters.Clear();
            run.Floor.Clear();
            run.Flecks.Clear();
            run.FleckDirs.Clear();
            run.BlockMarks.Clear();
            run.Held.Clear();
        }
    }
}
