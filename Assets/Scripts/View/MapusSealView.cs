// PURPOSE: "Mapus"'s SEAL - the MAHKÛM HÜCRESİ. The boss picks one empty cell and builds a small
// prison in it; this draws that prison, and the pressure it puts on the row and the column running
// through it. Owned by BoardView like the rot, the snake, the press and the parasite, because a
// seal stands for turns at a time and the board is repainted many times in that.
//
// WHAT IT REPLACES. A sealed cell used to be the empty cell in a different colour - one flat
// blue-grey instead of the usual one. That said "somebody painted this square" and nothing about
// what the boss actually does, which is worse here than almost anywhere else in the game, because
// Mapus does not take a square: it takes a square AND the row AND the column through it. A cube
// short of a line, the player has no way to see which cell is holding it.
//
// THE CELL IS NOT MARKED. IT IS TURNED INTO A PRISON, and that is five layers working together:
//
//   THE PIT       the cell's floor drops a step into the board (Resources/Shaders/MapusPit). This
//                 is the first read and the one that does the most work: a cell that has SUNK is
//                 somewhere a block cannot go for a reason the eye supplies by itself. Lit like a
//                 hole and not like a bump - the light falls on the FAR inner wall.
//   THE SOCKETS   four heavy brackets recessed into the cell's edges. Without them the ribs are
//                 four shapes floating over a cell; with them the ironwork is hinged into the
//                 board's own frame.
//   THE RIBS      four warden ribs swing out of those sockets and reach in. Broad at the root,
//                 tapering, and each ending in a HOOK that turns the same way round the middle -
//                 so the four of them read as an iris closing rather than as a compass rose. They
//                 never meet: a small dark negative space is left between their tips.
//   THE SEAL      in that gap, a struck lump of dull garnet wax with a die's guilloche pressed
//                 into it. It is the only warm thing in the whole effect, it is small, and it
//                 never glows - iron that was hot a long time ago and has not quite gone cold.
//   THE PRESSURE  and out along the row and the column, on the grid edge of every cell they pass
//                 through, a faint bracket of shadow. Never a beam, never a coloured stripe, never
//                 a tint on anybody's block: the line is not lit up, it is being LEANED ON.
//
// THE VIEW DECIDES NONE OF IT. MapusSealVisuals (Core, reporting only, [NotSaved]) says which cell
// is sealed, whether this turn MOVED the seal or HELD it, whether the cap just let a cell go, and
// how many cubes the row and column still want. All four change what is drawn and not one of them
// is worked out here - "is this line being held by exactly this cell" in particular is the
// explosion rule's own question, and there is one answer to it in the codebase.
//
// THE SEAL HOLDS FOR UP TO THREE TURNS, so the big animation is NOT every turn. Held, it plays a
// warden check and nothing else. Moved, the old prison is dismantled while the new one is built.
// Released by the cap, the ironwork lets go and the cell is honestly open for one turn - which is
// the player's window and has to look like one.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class MapusSealView : MonoBehaviour
    {
        /// <summary>What the board hands over: one seal, exactly as Core reported it.</summary>
        public struct Seal
        {
            public GridPos Cell;

            /// <summary>How many turns running this cell has been held, and the most it may be.
            /// The count is the rules' own; the View never runs a turn clock.</summary>
            public int TurnsHeld;

            public int MaxTurns;

            /// <summary>How many cubes this cell's row and column still want, or -1 when the line
            /// can never explode anyway. From GameBoard.RowGapCount / ColumnGapCount.</summary>
            public int RowGaps;

            public int ColumnGaps;

            /// <summary>True when the seal is the LAST thing between the player and that line.
            /// The pressure leans harder there - "this is the cell holding it".</summary>
            public bool RowHeldAlone;

            public bool ColumnHeldAlone;
        }

        /// <summary>Every number the look is made of.</summary>
        public static class Style
        {
            // ---- the pit ----
            /// <summary>How much of the cell the sunken floor covers. Slightly more than the
            /// board's own empty slot, so the sinking reaches the cell's edge.</summary>
            public static float PitSize = 0.9f;

            public static float PitInnerDarkness = 1f;

            public static float PitRimTightness = 0.55f;

            /// <summary>The floor, and the dark in the middle of it. The void is a COLD INDIGO and
            /// never actual black: a pure black disc in a cell is a hole punched in the render, and
            /// it has to keep a hue of its own to separate from the board's near-black.</summary>
            public static readonly Color PitFloor = new Color(0.085f, 0.088f, 0.115f);

            public static readonly Color PitVoid = new Color(0.036f, 0.038f, 0.068f);

            /// <summary>Light on the FAR inner wall - what makes it a hole rather than a dome.
            /// </summary>
            public static readonly Color PitLip = new Color(0.28f, 0.31f, 0.38f);

            // ---- the sockets ----
            /// <summary>
            /// THE HOUSING IS THE HEAVY PIECE, AND THAT IS THE WHOLE DESIGN.
            ///
            /// Two passes failed the same way: the mass sat near the middle, so four pieces
            /// converging on a small centre read as a ROTOR - first as thin blades, then as chunky
            /// ones, which is still a rotor. What a prison bolt actually looks like from above is
            /// a big machined block sunk into the WALL with a comparatively slim bar sliding out
            /// of it. Put the weight at the edge and the same four pieces stop being spokes and
            /// start being latches, because the eye reads where a thing comes FROM by where its
            /// mass is.
            ///
            /// So: the housing is nearly a third of the cell wide, the bolt about half that, and
            /// the housing is never allowed to be the smaller of the two.
            /// </summary>
            public static float SocketWidth = 0.32f;

            public static float SocketHeight = 0.21f;

            /// <summary>How far out from the middle the housing sits, as a share of the half cell.
            /// Past 1 it would leave the cell; here it straddles the cell's own edge, half sunk
            /// into the wall, which is what stops the ironwork floating.</summary>
            public static float SocketInset = 0.94f;

            public static float SocketTone = 0.3f;

            // ---- the warden ribs ----
            /// <summary>How far a rib reaches in from its socket, as a share of the cell, and how
            /// wide it is at the root. They must NOT meet: the gap between the tips is where the
            /// seal lives, and four pieces that touch in the middle are a plus sign.</summary>
            /// <summary>How far the bolt reaches in from its housing, as a share of the cell. It
            /// stops well short of the middle: what is left there is the PRISON GAP, and four
            /// pieces that close it are a hub.</summary>
            public static float RibReach = 0.3f;

            /// <summary>The bolt's own width - about half the housing's, because the housing is
            /// what carries the weight. Making the BOLT chunky instead was the second failed
            /// answer to the rotor problem: it is where the mass sits, not how much of it there
            /// is.</summary>
            public static float RibWidth = 0.17f;

            /// <summary>How far up the ironwork's range a rib sits. HIGH: it has to be clearly
            /// lighter than the board or the whole mechanism is four flat black shapes on a black
            /// cell, which is exactly what the first pass was.</summary>
            public static float RibTone = 0.88f;

            /// <summary>How deep the recess down the middle of a rib is - the one detail that
            /// stops a broad piece reading as a flat polygon.</summary>
            public static float RibGroove = 0.85f;

            /// <summary>The shadow a rib drops into the pit under it, and how far. Without it the
            /// iron is printed on the cell rather than closed over it.</summary>
            public static float RibShadowStrength = 0.55f;

            public static float RibShadowOffset = 0.022f;

            public static float RibShadowSpread = 1.1f;

            public static float RibBevel = 1f;

            public static float RibWear = 0.16f;

            /// <summary>Light on the ironwork's own contour. The board is near-black and so is the
            /// iron; this is what stops the whole mechanism disappearing into it. Separation, never
            /// an outline.</summary>
            public static float IronRim = 0.4f;

            // ---- the warden seal ----
            /// <summary>THE SEAL IS A MEDALLION, NOT A DOT: an outer rim of dried garnet, a body
            /// of burnt wine inside it, and the die's mark inside that. One flat disc of red in
            /// the middle of a dark cell is a status LED, and that is what it replaced.</summary>
            public static float SealSize = 0.19f;

            /// <summary>How much of the medallion the inner body takes - the rest is its rim.
            /// </summary>
            public static float SealBodyScale = 0.74f;

            public static float SealShadowStrength = 0.5f;

            /// <summary>How far off the exact middle it sits. Dead centre is a status light.
            /// </summary>
            public static float SealOffset = 0.022f;

            public static float BrandSize = 0.62f;

            /// <summary>Its warmth at rest. Low, and it never rises to a glow.</summary>
            public static float SealHeat = 0.72f;

            /// <summary>How much of that heat the pit's floor catches back.</summary>
            public static float SealFloorWarmth = 0.34f;

            // ---- the pressure down the row and the column ----
            /// <summary>
            /// How present a suppression bracket is far from the seal, and next to it.
            ///
            /// THE BRACKET IS IRON, NOT A SHADOW, and that is not decoration - it is the only thing
            /// that works. A dark mark has to be read against what it lies on, and this layer lies
            /// on both a near-black board AND a bright gold cube in the same frame: at any opacity
            /// that stays subtle on the gold it is invisible on the board, and at any that shows on
            /// the board it is a smear on the gold. Drawn in the ironwork's own material instead,
            /// it carries its own contour light (MapusIron._Rim) and reads on both - and it says
            /// the right thing as well, because what is reaching down the row is the prison.
            /// </summary>
            public static float PressureFar = 0.26f;

            public static float PressureNear = 0.46f;

            /// <summary>How fast it falls off with distance. It NEVER reaches zero: the whole line
            /// is held, not just the part near the seal.</summary>
            public static float PressureFalloff = 0.22f;

            /// <summary>Multiplier when Core says this line is held by the seal ALONE - every other
            /// required cell of it is already filled. "This is the cell doing it."</summary>
            public static float PressureHeldAlone = 1.5f;

            /// <summary>A cell holding a cube still wants a little more than an empty one - the
            /// iron has a lit face to compete with there. Small, because the material is now doing
            /// most of the work that opacity used to be asked to do.</summary>
            public static float PressureOnCube = 1.3f;

            public static float NotchLength = 0.78f;

            public static float NotchWidth = 0.11f;

            /// <summary>Where across the cell the bracket sits, as a share of the half cell. Right
            /// out at the cell's own edge - far enough out to be a pressure on the boundary rather
            /// than a mark on somebody's block, and not so far that it falls into the seam, which
            /// is already black and would swallow it whole.</summary>
            public static float NotchInset = 0.87f;

            /// <summary>How dark the iron of a bracket is - near the bottom of the ironwork's own
            /// range, so it is unmistakably the same material as the ribs and unmistakably not a
            /// coloured line.</summary>
            public static float PressureTone = 0.1f;

            /// <summary>How long each cell waits before its own brackets come in, per cell of
            /// distance - so the pressure travels out rather than appearing along the whole line.
            /// </summary>
            public static float PressureDeployPerCell = 0.02f;

            public static float PressureRetractPerCell = 0.016f;

            // ---- CELL SENTENCE: the prison going up ----
            public static float SpawnDarken = 0.08f;

            public static float SpawnSink = 0.13f;

            public static float SpawnVoid = 0.15f;

            public static float SpawnSockets = 0.14f;

            public static float SpawnRibs = 0.2f;

            /// <summary>Between the two bolts of one PAIR - almost nothing, they close together.
            /// </summary>
            public static float SpawnRibStagger = 0.01f;

            /// <summary>And between the pairs. Top and bottom shut first, then left and right:
            /// two axes closing one after the other, which is a mechanism. Four pieces arriving
            /// evenly round a circle is a fan opening, whatever shape they are.</summary>
            public static float SpawnPairOffset = 0.035f;

            /// <summary>How far out a rib is still swung when it starts, in degrees. It ROTATES in
            /// from its socket - a piece that slides in is a sprite being tweened.</summary>
            /// <summary>How far out a bolt is still swung when it starts, in degrees. SMALL: it
            /// SLIDES out of its housing and only settles with a hint of rotation. Anything more
            /// is a petal swinging open.</summary>
            public static float SpawnRibTilt = 4f;

            public static float SpawnSeal = 0.14f;

            public static float SpawnSealStartScale = 0.55f;

            public static float SpawnLock = 0.12f;

            /// <summary>How far the ribs drive in on the final lock, as a share of the cell.
            /// </summary>
            public static float SpawnLockBite = 0.014f;

            public static float SpawnPressure = 0.22f;

            public static float SpawnTotal = 0.86f;

            // ---- CELL RELEASE: the prison coming down ----
            public static float DespawnPressure = 0.18f;

            public static float DespawnSealDim = 0.12f;

            public static float DespawnRibRelease = 0.09f;

            public static float DespawnRibRetract = 0.18f;

            public static float DespawnSockets = 0.12f;

            public static float DespawnVoid = 0.15f;

            public static float DespawnTotal = 0.56f;

            /// <summary>How far into the old prison's release the new one starts going up. They
            /// overlap, because two full sequences end to end stop the game dead - but never far
            /// enough for two live seals to be on the board at once.</summary>
            public static float MoveOverlap = 0.2f;

            // ---- WARDEN CHECK: the idle ----
            public static float IdleMinInterval = 3f;

            public static float IdleMaxInterval = 5f;

            public static float IdleDuration = 0.52f;

            /// <summary>How far the ribs tighten, as a share of the cell - a pixel, no more.
            /// </summary>
            public static float IdleRibTighten = 0.008f;

            /// <summary>Top and bottom press first, then left and right. Never all four at
            /// once: two opposing bolts closing on a cell is a vice, four moving together round a
            /// middle is a fan.</summary>
            public static float IdlePairDelay = 0.032f;

            public static float IdleVoidDeepen = 0.16f;

            public static float IdleSealDim = 0.3f;

            public static float IdleSealCompress = 0.04f;

            public static float IdlePressureEcho = 0.35f;

            /// <summary>The rarer second idle: for a moment the pit looks bottomless and the seal
            /// almost disappears into it. "I saw the bottom of the well." Not horror - it is quiet
            /// and it is over.</summary>
            public static float DepthIdleMinInterval = 6f;

            public static float DepthIdleMaxInterval = 10f;

            public static float DepthIdleDuration = 1.1f;

            public static float DepthIdleAmount = 0.3f;

            // ---- DENIED ENTRY: a block hovered over it ----
            public static float DeniedDuration = 0.2f;

            public static float DeniedRibClamp = 0.011f;

            public static float DeniedSealCompress = 0.06f;

            public static float DeniedVoidDarken = 0.22f;

            public static float DeniedPressureBoost = 0.3f;

            // ---- the cap letting go ----
            /// <summary>The one release that is NOT the seal moving on: the cap made it let go, and
            /// the cell is honestly open for a turn. The ironwork opens WIDER than it needs to
            /// before it goes, which is the only way the player reads a window rather than a
            /// wander.</summary>
            public static float ReleaseOpenExtra = 0.05f;
        }

        /// <summary>What the lab can switch off one at a time.</summary>
        public static class Layers
        {
            public static bool ShowPit = true;

            public static bool ShowSockets = true;

            public static bool ShowRibs = true;

            public static bool ShowSeal = true;

            public static bool ShowBrand = true;

            public static bool ShowRowPressure = true;

            public static bool ShowColumnPressure = true;

            public static bool ShowWarmth = true;

            /// <summary>The contact shadows the iron drops into the pit.</summary>
            public static bool ShowShadows = true;

            /// <summary>THE PROPELLER TEST: everything flat grey, no brand, no shadows, no light.
            /// Only the silhouette is left, and the question is whether THAT reads as four heavy
            /// wall latches or as a fan. Two passes of this design were lost to tuning materials
            /// while the shape underneath was wrong, so the test is a switch rather than an
            /// intention.</summary>
            public static bool SilhouetteTest;

            /// <summary>THE EDGE-MASS TEST: housing green, bolt shaft blue, latch head red. The
            /// green must be visibly the heaviest region. Mass near the middle IS the rotor
            /// problem, and this makes it a thing you can see rather than argue about.</summary>
            public static bool MassTest;

            public static void AllOn()
            {
                ShowPit = true;
                ShowSockets = true;
                ShowRibs = true;
                ShowSeal = true;
                ShowBrand = true;
                ShowRowPressure = true;
                ShowColumnPressure = true;
                ShowWarmth = true;
                ShowShadows = true;
                SilhouetteTest = false;
                MassTest = false;
            }
        }

        private const int PressureOrder = 2;

        private const int PitOrder = 4;

        private const int SocketOrder = 5;

        /// <summary>Each rib's own shadow, cast into the pit UNDER it - the difference between
        /// iron closed over a cell and iron printed on one.</summary>
        private const int RibShadowOrder = 6;

        private const int RibOrder = 7;

        private const int SealShadowOrder = 8;

        private const int SealOrder = 9;

        private const int SealBodyOrder = 10;

        private const int BrandOrder = 11;

        /// <summary>The four sides a rib comes in from, in the order they deploy. Up and down
        /// first, then the sides - a mechanism closes in sequence.</summary>
        private static readonly Vector2[] Sides =
        {
            new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(-1f, 0f), new Vector2(1f, 0f)
        };

        /// <summary>One suppression bracket: where it is, and how far from the seal.</summary>
        private struct Mark
        {
            public SpriteRenderer Renderer;
            public Vector2 At;
            public float Turn;
            public int Distance;
            public bool Column;
            /// <summary>Whether the cell under it held a cube when the line was measured. Reading
            /// the board, not deciding anything - and it is what the bracket's weight is scaled
            /// by.</summary>
            public bool OnCube;
        }

        /// <summary>One cell's prison. There is normally one; there are briefly two while the seal
        /// is moving, and the retiring one is coming down while the arriving one goes up.</summary>
        private sealed class Cellwork
        {
            public GridPos Cell;
            public SpriteRenderer Pit;
            public readonly SpriteRenderer[] Sockets = new SpriteRenderer[4];
            public readonly SpriteRenderer[] Ribs = new SpriteRenderer[4];
            public readonly SpriteRenderer[] RibShadows = new SpriteRenderer[4];
            public readonly int[] Variants = new int[4];
            public SpriteRenderer SealShadow;
            public SpriteRenderer Seal;
            public SpriteRenderer SealBody;
            public SpriteRenderer Brand;
            public readonly List<Mark> Marks = new List<Mark>();
            /// <summary>Seconds into the deploy, or past SpawnTotal once it is standing.</summary>
            public float SpawnClock;
            /// <summary>Seconds into the release, or below zero while it holds.</summary>
            public float DespawnClock = -1f;
            /// <summary>True when the release is the CAP letting go rather than the seal moving.
            /// </summary>
            public bool Released;
            public float IdleWait;
            public float IdleClock = -1f;
            public float DepthWait;
            public float DepthClock = -1f;
            public float DeniedClock = -1f;
            /// <summary>What Core last said about this cell.</summary>
            public int TurnsHeld;
            public int MaxTurns;
            public bool RowAlone;
            public bool ColumnAlone;
            public bool RowLive;
            public bool ColumnLive;
        }

        private readonly List<Cellwork> works = new List<Cellwork>();

        private MaterialPropertyBlock block;

        private float cellSize;

        private System.Func<GridPos, Vector2> toWorld;

        private GameBoard board;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        /// <summary>True while anything is deploying or coming down - what the lab waits on.
        /// </summary>
        public bool Busy
        {
            get
            {
                for (int i = 0; i < works.Count; i++)
                {
                    if (works[i].DespawnClock >= 0f || works[i].SpawnClock < Style.SpawnTotal)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// The seal as Core reports it. Pass null when there is none - the board had too few free
        /// cells, or the cap released the only cell worth taking.
        ///
        /// A seal on the cell that already has one is HELD: its state is updated and nothing is
        /// replayed. A seal somewhere else takes the old prison down and puts a new one up, the two
        /// overlapping. This is the only entry point, so a repaint can call it as often as it likes.
        /// </summary>
        public void Sync(BoardView view, Seal? live, bool released)
        {
            if (view == null || view.Board == null)
            {
                return;
            }
            cellSize = view.CellWorldSize;
            toWorld = view.CellToWorld;
            board = view.Board;

            GridPos want = default(GridPos);
            bool wanted = live.HasValue;
            if (wanted)
            {
                want = live.Value.Cell;
            }
            Cellwork standing = null;
            for (int i = 0; i < works.Count; i++)
            {
                Cellwork w = works[i];
                if (w.DespawnClock >= 0f)
                {
                    continue;
                }
                if (wanted && w.Cell.Equals(want))
                {
                    standing = w;
                }
                else
                {
                    // Anything else that is still up is coming down.
                    w.DespawnClock = 0f;
                    w.Released = released;
                    w.IdleClock = -1f;
                    w.DepthClock = -1f;
                }
            }
            if (!wanted)
            {
                return;
            }
            if (standing == null)
            {
                standing = Build(want);
                works.Add(standing);
            }
            standing.TurnsHeld = live.Value.TurnsHeld;
            standing.MaxTurns = live.Value.MaxTurns;
            standing.RowAlone = live.Value.RowHeldAlone;
            standing.ColumnAlone = live.Value.ColumnHeldAlone;
            // A line that can never explode is not being held by anything, so it takes no
            // pressure - Core says so with -1 and the View does not second-guess it.
            standing.RowLive = live.Value.RowGaps >= 0;
            standing.ColumnLive = live.Value.ColumnGaps >= 0;
            Paint();
        }

        /// <summary>
        /// A block's preview was dragged over the sealed cell. The cell REFUSES it - the nearest
        /// ribs clamp, the seal tightens, the pit darkens - and that is the whole message. No red
        /// cross, no shake, no text: the thing that is stopping you is right there and it can
        /// answer for itself.
        /// </summary>
        public void PlayDenied(GridPos cell)
        {
            for (int i = 0; i < works.Count; i++)
            {
                if (works[i].Cell.Equals(cell) && works[i].DespawnClock < 0f
                    && works[i].DeniedClock < 0f)
                {
                    works[i].DeniedClock = 0f;
                }
            }
        }

        /// <summary>True while that cell carries a seal that is still up.</summary>
        public bool Holds(GridPos cell)
        {
            for (int i = 0; i < works.Count; i++)
            {
                if (works[i].Cell.Equals(cell) && works[i].DespawnClock < 0f)
                {
                    return true;
                }
            }
            return false;
        }

        public void Stop()
        {
            for (int i = works.Count - 1; i >= 0; i--)
            {
                Drop(works[i]);
            }
            works.Clear();
        }

        // =================================================================== building

        private Cellwork Build(GridPos cell)
        {
            var w = new Cellwork { Cell = cell };
            Material iron = IronMaterial();
            if (Layers.ShowPit)
            {
                w.Pit = Rent(ViewUtil.RoundedSprite, PitOrder, PitMaterial());
            }
            for (int s = 0; s < 4; s++)
            {
                w.Variants[s] = (int)(Random01(cell, 10 + s) * 3f) % 3;
                if (Layers.ShowSockets)
                {
                    w.Sockets[s] = Rent(MapusShapes.Socket, SocketOrder, iron);
                }
                if (Layers.ShowRibs)
                {
                    if (Layers.ShowShadows)
                    {
                        w.RibShadows[s] = Rent(MapusShapes.Rib(w.Variants[s]), RibShadowOrder,
                            null);
                    }
                    w.Ribs[s] = Rent(MapusShapes.Rib(w.Variants[s]), RibOrder, iron);
                }
            }
            if (Layers.ShowSeal)
            {
                if (Layers.ShowShadows)
                {
                    w.SealShadow = Rent(MapusShapes.Seal, SealShadowOrder, null);
                }
                w.Seal = Rent(MapusShapes.Seal, SealOrder, iron);
                w.SealBody = Rent(MapusShapes.Seal, SealBodyOrder, iron);
                if (Layers.ShowBrand)
                {
                    w.Brand = Rent(MapusShapes.Brand, BrandOrder, iron);
                }
            }
            BuildPressure(w);
            w.IdleWait = Style.IdleMinInterval
                + Random01(cell, 3) * (Style.IdleMaxInterval - Style.IdleMinInterval);
            w.DepthWait = Style.DepthIdleMinInterval
                + Random01(cell, 4)
                    * (Style.DepthIdleMaxInterval - Style.DepthIdleMinInterval);
            return w;
        }

        /// <summary>
        /// The brackets down the row and the column. One PAIR per cell - on the two grid edges the
        /// line runs between - so the pressure is on the SEAM rather than across anybody's block,
        /// and the whole line can be marked without a single stripe being drawn over the board.
        /// </summary>
        private void BuildPressure(Cellwork w)
        {
            w.Marks.Clear();
            if (board == null || toWorld == null)
            {
                return;
            }
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                if (x == w.Cell.X)
                {
                    continue; // the sealed cell speaks for itself
                }
                var at = new GridPos(x, w.Cell.Y);
                if (board.IsInside(at))
                {
                    AddMark(w, at, false, Mathf.Abs(x - w.Cell.X));
                }
            }
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                if (y == w.Cell.Y)
                {
                    continue;
                }
                var at = new GridPos(w.Cell.X, y);
                if (board.IsInside(at))
                {
                    AddMark(w, at, true, Mathf.Abs(y - w.Cell.Y));
                }
            }
        }

        private void AddMark(Cellwork w, GridPos at, bool column, int distance)
        {
            Vector2 centre = toWorld(at);
            float half = cellSize * 0.5f * Style.NotchInset;
            // A ROW is held along its length, so its brackets sit on the top and bottom edges of
            // each cell; a COLUMN's sit on the left and right. That is what makes the two axes
            // distinguishable at a glance without either of them being a coloured line.
            for (int k = 0; k < 2; k++)
            {
                float sign = k == 0 ? 1f : -1f;
                var mark = new Mark
                {
                    Renderer = Rent(MapusShapes.Notch, PressureOrder, IronMaterial()),
                    At = column
                        ? new Vector2(centre.x + half * sign, centre.y)
                        : new Vector2(centre.x, centre.y + half * sign),
                    // The bracket's opening always faces the cell it is pressing on.
                    Turn = column ? (sign > 0f ? 90f : -90f) : (sign > 0f ? 180f : 0f),
                    Distance = distance,
                    Column = column,
                    OnCube = board.GetCube(at).HasValue
                };
                w.Marks.Add(mark);
            }
        }

        private void Drop(Cellwork w)
        {
            Return(w.Pit);
            for (int s = 0; s < 4; s++)
            {
                Return(w.Sockets[s]);
                Return(w.Ribs[s]);
                Return(w.RibShadows[s]);
            }
            Return(w.SealShadow);
            Return(w.Seal);
            Return(w.SealBody);
            Return(w.Brand);
            for (int i = 0; i < w.Marks.Count; i++)
            {
                Return(w.Marks[i].Renderer);
            }
            w.Marks.Clear();
            works.Remove(w);
        }

        // =================================================================== the clock

        private void Update()
        {
            if (works.Count == 0)
            {
                return;
            }
            float dt = Time.deltaTime;
            for (int i = works.Count - 1; i >= 0; i--)
            {
                Cellwork w = works[i];
                w.SpawnClock += dt;
                if (w.DespawnClock >= 0f)
                {
                    w.DespawnClock += dt;
                    if (w.DespawnClock > Style.DespawnTotal)
                    {
                        Drop(w);
                    }
                    continue;
                }
                if (w.DeniedClock >= 0f)
                {
                    w.DeniedClock += dt;
                    if (w.DeniedClock > Style.DeniedDuration)
                    {
                        w.DeniedClock = -1f;
                    }
                }
                if (w.SpawnClock < Style.SpawnTotal)
                {
                    continue; // still going up
                }
                // THE WARDEN CHECK, and the rarer look down the well. Neither runs while anything
                // else is happening: this is a mechanism, and it only fidgets when nothing is.
                if (w.IdleClock >= 0f)
                {
                    w.IdleClock += dt;
                    if (w.IdleClock > Style.IdleDuration)
                    {
                        w.IdleClock = -1f;
                        w.IdleWait = Style.IdleMinInterval
                            + Random01(w.Cell, Mathf.RoundToInt(Time.time * 11f))
                                * (Style.IdleMaxInterval - Style.IdleMinInterval);
                    }
                }
                else if (w.DeniedClock < 0f && w.DepthClock < 0f)
                {
                    w.IdleWait -= dt;
                    if (w.IdleWait <= 0f)
                    {
                        w.IdleClock = 0f;
                    }
                }
                if (w.DepthClock >= 0f)
                {
                    w.DepthClock += dt;
                    if (w.DepthClock > Style.DepthIdleDuration)
                    {
                        w.DepthClock = -1f;
                        w.DepthWait = Style.DepthIdleMinInterval
                            + Random01(w.Cell, Mathf.RoundToInt(Time.time * 5f))
                                * (Style.DepthIdleMaxInterval - Style.DepthIdleMinInterval);
                    }
                }
                else if (w.IdleClock < 0f && w.DeniedClock < 0f)
                {
                    w.DepthWait -= dt;
                    if (w.DepthWait <= 0f)
                    {
                        w.DepthClock = 0f;
                    }
                }
            }
            Paint();
        }

        /// <summary>A deterministic 0..1 from a cell and a salt - no Random, so a seal looks the
        /// same every time it is drawn and the lab is reproducible frame for frame.</summary>
        private static float Random01(GridPos cell, int salt)
        {
            uint v = (uint)(cell.X * 73856093 ^ cell.Y * 19349663 ^ salt * 83492791);
            v ^= v >> 13;
            v *= 1274126177u;
            v ^= v >> 16;
            return (v & 0xFFFFFF) / (float)0xFFFFFF;
        }

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
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        /// <summary>Heavy easing: it arrives fast and STOPS. No bounce, no overshoot - the whole
        /// point of the piece is that it is not springy.</summary>
        private static float Heavy(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - (1f - k) * (1f - k) * (1f - k);
        }

        // =================================================================== painting

        private void Paint()
        {
            if (toWorld == null)
            {
                return;
            }
            for (int i = 0; i < works.Count; i++)
            {
                Paint(works[i]);
            }
        }

        private void Paint(Cellwork w)
        {
            Vector2 at = toWorld(w.Cell);
            float cell = cellSize;
            bool down = w.DespawnClock >= 0f;
            float t = down ? w.DespawnClock : w.SpawnClock;

            // ---- how far the prison is up, layer by layer ----
            float sink = down
                ? 1f - Ease(Span(t, Style.DespawnPressure * 0.7f + Style.DespawnRibRetract,
                    Style.DespawnTotal))
                : Ease(Span(t, Style.SpawnDarken, Style.SpawnDarken + Style.SpawnSink));
            float voidness = down
                ? 1f - Ease(Span(t, Style.DespawnTotal - Style.DespawnVoid, Style.DespawnTotal))
                : Ease(Span(t, Style.SpawnDarken + Style.SpawnSink * 0.5f,
                    Style.SpawnDarken + Style.SpawnSink * 0.5f + Style.SpawnVoid));
            float socketOut = down
                ? 1f - Ease(Span(t, Style.DespawnTotal - Style.DespawnVoid - Style.DespawnSockets,
                    Style.DespawnTotal - Style.DespawnVoid))
                : Heavy(Span(t, Style.SpawnDarken + Style.SpawnSink,
                    Style.SpawnDarken + Style.SpawnSink + Style.SpawnSockets));

            // ---- the beats that press on a standing prison ----
            float idle = 0f;
            float idleEcho = 0f;
            if (w.IdleClock >= 0f)
            {
                float k = Mathf.Clamp01(w.IdleClock / Style.IdleDuration);
                idle = k < 0.45f ? Ease(k / 0.45f) : 1f - Ease((k - 0.45f) / 0.55f);
                idleEcho = k > 0.4f && k < 0.85f
                    ? Mathf.Sin((k - 0.4f) / 0.45f * Mathf.PI) * Style.IdlePressureEcho
                    : 0f;
            }
            float denied = 0f;
            if (w.DeniedClock >= 0f)
            {
                float k = Mathf.Clamp01(w.DeniedClock / Style.DeniedDuration);
                denied = k < 0.4f ? Ease(k / 0.4f) : 1f - Ease((k - 0.4f) / 0.6f);
            }
            float well = 0f;
            if (w.DepthClock >= 0f)
            {
                well = Mathf.Sin(Mathf.Clamp01(w.DepthClock / Style.DepthIdleDuration) * Mathf.PI)
                    * Style.DepthIdleAmount;
            }
            // The final lock: the one moment the whole mechanism drives in together.
            float lockIn = down
                ? 0f
                : Ease(Span(t, Style.SpawnTotal - Style.SpawnPressure - Style.SpawnLock,
                    Style.SpawnTotal - Style.SpawnPressure))
                    * (1f - Ease(Span(t, Style.SpawnTotal - Style.SpawnPressure,
                        Style.SpawnTotal - Style.SpawnPressure + 0.1f)));
            float press = Mathf.Max(Mathf.Max(idle, denied), lockIn);

            PaintPit(w, at, cell, sink, voidness, well, denied, press);
            for (int s = 0; s < 4; s++)
            {
                PaintSide(w, s, at, cell, t, down, socketOut, idle, denied, lockIn, press);
            }
            PaintSeal(w, at, cell, t, down, idle, denied, press);
            PaintPressure(w, t, down, idleEcho, denied);
        }

        /// <summary>The sunken floor and the dark in it.</summary>
        private void PaintPit(Cellwork w, Vector2 at, float cell, float sink, float voidness,
            float well, float denied, float press)
        {
            if (w.Pit == null)
            {
                return;
            }
            w.Pit.transform.localPosition = new Vector3(at.x, at.y, 0f);
            float size = cell * Style.PitSize;
            Fit(w.Pit, size, size);
            w.Pit.color = new Color(1f, 1f, 1f, Mathf.Clamp01(sink * 1.4f));
            if (PitMaterial() == null)
            {
                // No shader: a flat dark slot, which still says the cell is shut.
                w.Pit.color = new Color(Style.PitVoid.r, Style.PitVoid.g, Style.PitVoid.b,
                    Mathf.Clamp01(sink));
                return;
            }
            block.Clear();
            block.SetColor(FloorId, Style.PitFloor);
            block.SetColor(VoidId, Style.PitVoid);
            block.SetColor(LipId, Style.PitLip);
            // The well-idle is the pit alone going deeper for a moment; a refusal drives it too.
            block.SetFloat(DepthId, Mathf.Clamp01(sink * (1f + well * 0.5f)));
            block.SetFloat(InnerId, Style.PitInnerDarkness * voidness
                * (1f + well + denied * Style.DeniedVoidDarken + press * Style.IdleVoidDeepen));
            block.SetFloat(RimTightId, Style.PitRimTightness * (1f + press * 0.4f));
            block.SetFloat(WarmId, Layers.ShowWarmth && w.Seal != null
                ? Style.SealFloorWarmth * Style.SealHeat * (1f - well * 1.6f)
                : 0f);
            block.SetColor(WarmColourId, SealWarm);
            block.SetFloat(FaceHalfId, CompressedCubeView.FaceHalfOf(w.Pit));
            w.Pit.SetPropertyBlock(block);
        }

        /// <summary>One side's socket and the rib hinged into it.</summary>
        private void PaintSide(Cellwork w, int side, Vector2 at, float cell, float t, bool down,
            float socketOut, float idle, float denied, float lockIn, float press)
        {
            Vector2 outward = Sides[side];
            Vector2 inward = -outward;
            float half = cell * 0.5f;
            // The socket sits just inside the cell's edge and RISES out of the board as it takes
            // its place - it is part of the frame, so it comes from the frame.
            Vector2 seat = at + outward * (half * Style.SocketInset
                + half * (1f - socketOut) * 0.06f);
            if (w.Sockets[side] != null)
            {
                SpriteRenderer r = w.Sockets[side];
                r.transform.localPosition = new Vector3(seat.x, seat.y, 0f);
                r.transform.localRotation = Turn(inward);
                Fit(r, cell * Style.SocketWidth, cell * Style.SocketHeight * socketOut);
                PaintIron(r, Style.SocketTone, 0.75f, press * 0.5f, 0f,
                    Mathf.Clamp01(socketOut * 1.4f), 0f, 1f);
            }
            if (w.Ribs[side] == null)
            {
                return;
            }
            // ---- how far this rib has swung in ----
            // Sides 0 and 1 are top and bottom, 2 and 3 left and right - so the pair offset
            // falls between the axes and the small stagger falls within one.
            float pair = side < 2 ? 0f : Style.SpawnPairOffset;
            float within = (side % 2) * Style.SpawnRibStagger;
            float from = down
                ? Style.DespawnPressure * 0.5f + (side < 2 ? Style.SpawnPairOffset : 0f) + within
                : Style.SpawnDarken + Style.SpawnSink + Style.SpawnSockets * 0.6f + pair + within;
            float outAmount = down
                ? 1f - Heavy(Span(t, from + Style.DespawnRibRelease,
                    from + Style.DespawnRibRelease + Style.DespawnRibRetract))
                : Heavy(Span(t, from, from + Style.SpawnRibs));
            // THE RELEASE OPENS WIDER THAN IT HAS TO. The cap let this cell go, and a window has
            // to look like one - the iron swings out past its rest before it leaves.
            if (down && w.Released)
            {
                float open = Ease(Span(t, from, from + Style.DespawnRibRelease))
                    * (1f - Ease(Span(t, from + Style.DespawnRibRelease,
                        from + Style.DespawnRibRelease + Style.DespawnRibRetract * 0.5f)));
                outAmount += open * Style.ReleaseOpenExtra / Mathf.Max(Style.RibReach, 0.01f);
            }
            float reach = cell * Style.RibReach * Mathf.Max(outAmount, 0.0001f);
            // Every beat that presses on the mechanism drives the ribs a little further in. Never
            // more than a pixel: this is a lock being checked, not a machine chewing.
            float bite = idle * Style.IdleRibTighten
                + denied * Style.DeniedRibClamp
                + lockIn * Style.SpawnLockBite;
            // THE PAIRS PRESS ONE AFTER THE OTHER: top and bottom squeeze the cell, then left
            // and right do. Two opposing bolts closing on a thing is a vice; four moving at once
            // round a middle is the fan again, even at a pixel.
            if (idle > 0f && side >= 2)
            {
                bite -= idle * Style.IdleRibTighten
                    * (1f - Ease(Span(w.IdleClock, Style.IdlePairDelay,
                        Style.IdlePairDelay * 2f)));
            }
            reach += cell * bite;
            // It ROTATES in from its socket rather than sliding: the root stays put and the tip
            // swings. A piece that slides is a sprite being tweened.
            float tilt = (1f - outAmount) * Style.SpawnRibTilt * (side % 2 == 0 ? 1f : -1f);
            Vector2 aim = Rotate(inward, tilt);
            Vector2 centre = seat + aim * (reach * 0.5f);
            SpriteRenderer rib = w.Ribs[side];
            rib.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
            rib.transform.localRotation = Turn(aim);
            Fit(rib, cell * Style.RibWidth, reach);
            PaintIron(rib, Style.RibTone, Style.RibBevel, press, 0f,
                Mathf.Clamp01(outAmount * 2.2f), Style.RibGroove);
            // ITS SHADOW, in the pit under it. The same silhouette, a little bigger, offset away
            // from the board's own light - so the iron is closed OVER the cell rather than printed
            // on it. It is the cheapest volume in the whole effect and the most missed when it is
            // not there.
            if (w.RibShadows[side] != null)
            {
                SpriteRenderer sh = w.RibShadows[side];
                Vector2 drop = new Vector2(0.6f, -0.8f) * (cell * Style.RibShadowOffset);
                sh.transform.localPosition = new Vector3(centre.x + drop.x, centre.y + drop.y, 0f);
                sh.transform.localRotation = rib.transform.localRotation;
                Fit(sh, cell * Style.RibWidth * Style.RibShadowSpread,
                    reach * Style.RibShadowSpread);
                sh.color = new Color(0f, 0f, 0f,
                    Style.RibShadowStrength * Mathf.Clamp01(outAmount * 2.2f));
            }
        }

        /// <summary>The struck lump in the negative space the four hooks leave.</summary>
        private void PaintSeal(Cellwork w, Vector2 at, float cell, float t, bool down, float idle,
            float denied, float press)
        {
            if (w.Seal == null)
            {
                return;
            }
            float from = Style.SpawnTotal - Style.SpawnPressure - Style.SpawnLock - Style.SpawnSeal;
            // IT RISES OUT OF THE PIT. Not a pop and not a fade: it comes up from below, which is
            // the one motion that says the well has something in it.
            float rise = down
                ? 1f - Ease(Span(t, 0f, Style.DespawnSealDim))
                : Heavy(Span(t, from, from + Style.SpawnSeal));
            float scale = Mathf.Lerp(Style.SpawnSealStartScale, 1f, rise);
            float squeeze = 1f - idle * Style.IdleSealCompress - denied * Style.DeniedSealCompress;
            // Slightly off the exact middle: dead centre is a status light.
            Vector2 off = new Vector2(Random01(w.Cell, 7) - 0.5f, Random01(w.Cell, 8) - 0.5f)
                * (cell * Style.SealOffset * 2f);
            Vector2 seat = at + off - new Vector2(0f, cell * (1f - rise) * 0.03f);
            float size = cell * Style.SealSize * scale;
            w.Seal.transform.localPosition = new Vector3(seat.x, seat.y, 0f);
            Fit(w.Seal, size * squeeze, size / Mathf.Max(squeeze, 0.01f));
            // The heat DIPS as the warden checks - the seal is being leaned on, not lit up.
            float heat = Style.SealHeat * rise * (1f - idle * Style.IdleSealDim)
                * (down ? Mathf.Max(0f, 1f - Span(t, 0f, Style.DespawnSealDim) * 1.3f) : 1f);
            float shown = Mathf.Clamp01(rise * 1.6f);
            if (Layers.SilhouetteTest)
            {
                // Only the ironwork's outline is under test; the brand is the strongest focal
                // point in the cell and would answer the question for the eye.
                shown = 0f;
            }
            if (w.SealShadow != null)
            {
                w.SealShadow.transform.localPosition =
                    new Vector3(seat.x + cell * 0.012f, seat.y - cell * 0.016f, 0f);
                Fit(w.SealShadow, size * 1.16f, size * 1.16f);
                w.SealShadow.color = new Color(0f, 0f, 0f, Style.SealShadowStrength * shown);
            }
            // THE RIM: dried, almost cold garnet - the outside of a lump of wax that was struck a
            // long time ago.
            PaintIron(w.Seal, 0.24f, 0.55f, press, Layers.ShowWarmth ? heat * 0.45f : 0f, shown,
                0f);
            if (w.SealBody != null)
            {
                // AND THE BODY INSIDE IT: burnt wine, and what little heat is left is in here.
                float inner = size * Style.SealBodyScale;
                w.SealBody.transform.localPosition =
                    new Vector3(seat.x, seat.y + cell * 0.004f, 0f);
                Fit(w.SealBody, inner * squeeze, inner / Mathf.Max(squeeze, 0.01f));
                PaintIron(w.SealBody, 0.62f, 0.8f, press, Layers.ShowWarmth ? heat : 0f, shown,
                    0f);
            }
            if (w.Brand == null)
            {
                return;
            }
            w.Brand.transform.localPosition = new Vector3(seat.x, seat.y + cell * 0.004f, 0f);
            float mark = size * Style.SealBodyScale * Style.BrandSize;
            Fit(w.Brand, mark * squeeze, mark / Mathf.Max(squeeze, 0.01f));
            // The die's mark is pressed INTO the wax, so it is the seal's own colour gone deeper -
            // never a lighter mark laid on top, which would read as printing.
            PaintIron(w.Brand, 0.06f, 0.35f, press, Layers.ShowWarmth ? heat * 0.28f : 0f,
                shown * 0.85f, 0f);
        }

        /// <summary>
        /// The pressure down the row and the column. It travels OUT from the seal cell by cell as
        /// the prison locks, and back in as it comes down - never a beam, and never all at once.
        /// </summary>
        private void PaintPressure(Cellwork w, float t, bool down, float echo, float denied)
        {
            for (int i = 0; i < w.Marks.Count; i++)
            {
                Mark m = w.Marks[i];
                if (m.Renderer == null)
                {
                    continue;
                }
                bool live = m.Column ? w.ColumnLive : w.RowLive;
                bool shown = m.Column ? Layers.ShowColumnPressure : Layers.ShowRowPressure;
                if (!live || !shown)
                {
                    m.Renderer.color = Clear;
                    continue;
                }
                float delay = m.Distance
                    * (down ? Style.PressureRetractPerCell : Style.PressureDeployPerCell);
                float open;
                if (down)
                {
                    // It retracts from the FAR end first, closing back toward the seal.
                    float far = (m.Distance == 0 ? 0 : m.Distance) * Style.PressureRetractPerCell;
                    open = 1f - Ease(Span(t, Style.DespawnPressure - far
                        - Style.PressureRetractPerCell * 3f, Style.DespawnPressure - far * 0.2f));
                }
                else
                {
                    float start = Style.SpawnTotal - Style.SpawnPressure + delay;
                    open = Ease(Span(t, start, start + Style.SpawnPressure * 0.5f));
                }
                float strength = Mathf.Lerp(Style.PressureFar, Style.PressureNear,
                    1f / (1f + m.Distance * Style.PressureFalloff * 4f));
                bool alone = m.Column ? w.ColumnAlone : w.RowAlone;
                if (alone)
                {
                    strength *= Style.PressureHeldAlone;
                }
                if (m.OnCube)
                {
                    strength *= Style.PressureOnCube;
                }
                strength *= 1f + echo + denied * Style.DeniedPressureBoost;
                m.Renderer.transform.localPosition = new Vector3(m.At.x, m.At.y, 0f);
                m.Renderer.transform.localRotation = Quaternion.Euler(0f, 0f, m.Turn);
                Fit(m.Renderer, cellSize * Style.NotchLength, cellSize * Style.NotchWidth);
                PaintIron(m.Renderer, Style.PressureTone, 0.45f, 0f, 0f,
                    Mathf.Clamp01(strength * open));
            }
        }

        private static Quaternion Turn(Vector2 aim)
        {
            return Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg - 90f);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Paints one iron piece: gunmetal, its own bevel lit from the board's light in
        /// WORLD space, and only as much contour as a near-black board needs.</summary>
        private void PaintIron(SpriteRenderer r, float tone, float bevel, float press, float heat,
            float alpha)
        {
            PaintIron(r, tone, bevel, press, heat, alpha, 0f);
        }

        /// <summary>The same, with the recess only a rib wants down its middle.</summary>
        private void PaintIron(SpriteRenderer r, float tone, float bevel, float press, float heat,
            float alpha, float groove)
        {
            PaintIron(r, tone, bevel, press, heat, alpha, groove, 2f);
        }

        /// <summary>The same, told which part of the mechanism this is - housing or bolt - so the
        /// edge-mass test can colour them apart.</summary>
        private void PaintIron(SpriteRenderer r, float tone, float bevel, float press, float heat,
            float alpha, float groove, float mass)
        {
            if (r == null)
            {
                return;
            }
            r.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            if (IronMaterial() == null)
            {
                // No shader: flat gunmetal, or dull garnet for the seal. The cell still reads as
                // shut; only the material's story is lost.
                Color flat = heat > 0f ? SealWarm : IronBody;
                r.color = new Color(flat.r, flat.g, flat.b, Mathf.Clamp01(alpha));
                return;
            }
            block.Clear();
            block.SetColor(DeepId, IronDeep);
            block.SetColor(BodyId, IronBody);
            block.SetColor(HiId, IronHigh);
            block.SetColor(WearId, IronWear);
            block.SetFloat(ToneId, tone);
            block.SetFloat(BevelId, bevel);
            block.SetFloat(WearAmountId, Style.RibWear);
            block.SetFloat(IronRimId, Style.IronRim);
            block.SetColor(IronRimColourId, IronRimColour);
            block.SetFloat(HeatId, heat);
            block.SetColor(HeatColourId, SealWarm);
            block.SetFloat(DebugId, Layers.SilhouetteTest ? 1f : (Layers.MassTest ? 2f : 0f));
            block.SetFloat(MassKindId, mass);
            block.SetFloat(GrooveId, groove);
            block.SetFloat(PressId, press);
            block.SetFloat(FaceHalfId, CompressedCubeView.FaceHalfOf(r));
            r.SetPropertyBlock(block);
        }

        /// <summary>The palette: cold graphite through gunmetal, one dull bronze, one dull garnet.
        /// No neon, no alarm orange, no sci-fi cyan - the boss is authority, not danger.</summary>
        public static readonly Color IronDeep = new Color(0.05f, 0.06f, 0.09f);

        /// <summary>Gunmetal, and it is deliberately WELL clear of the board's own value (~0.11).
        /// The rule this pass was given is the right one: the iron has to sit 10-20% above the
        /// board or the silhouette does not exist.</summary>
        public static readonly Color IronBody = new Color(0.23f, 0.25f, 0.31f);

        public static readonly Color IronHigh = new Color(0.4f, 0.44f, 0.53f);

        public static readonly Color IronWear = new Color(0.32f, 0.25f, 0.15f);

        public static readonly Color IronRimColour = new Color(0.46f, 0.51f, 0.6f);

        /// <summary>The seal's own colour: deep garnet, a long way from a warning red.</summary>
        public static readonly Color SealWarm = new Color(0.5f, 0.15f, 0.13f);

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        private static readonly int DeepId = Shader.PropertyToID("_Deep");
        private static readonly int BodyId = Shader.PropertyToID("_Body");
        private static readonly int HiId = Shader.PropertyToID("_Hi");
        private static readonly int WearId = Shader.PropertyToID("_Wear");
        private static readonly int ToneId = Shader.PropertyToID("_Tone");
        private static readonly int BevelId = Shader.PropertyToID("_Bevel");
        private static readonly int WearAmountId = Shader.PropertyToID("_WearAmount");
        private static readonly int IronRimId = Shader.PropertyToID("_Rim");
        private static readonly int IronRimColourId = Shader.PropertyToID("_RimColour");
        private static readonly int HeatId = Shader.PropertyToID("_Heat");
        private static readonly int HeatColourId = Shader.PropertyToID("_HeatColour");
        private static readonly int DebugId = Shader.PropertyToID("_Debug");
        private static readonly int MassKindId = Shader.PropertyToID("_MassKind");
        private static readonly int GrooveId = Shader.PropertyToID("_Groove");
        private static readonly int PressId = Shader.PropertyToID("_Press");
        private static readonly int FloorId = Shader.PropertyToID("_Floor");
        private static readonly int VoidId = Shader.PropertyToID("_Void");
        private static readonly int LipId = Shader.PropertyToID("_Lip");
        private static readonly int DepthId = Shader.PropertyToID("_Depth");
        private static readonly int InnerId = Shader.PropertyToID("_Inner");
        private static readonly int RimTightId = Shader.PropertyToID("_Rim");
        private static readonly int WarmId = Shader.PropertyToID("_Warm");
        private static readonly int WarmColourId = Shader.PropertyToID("_WarmColour");
        private static readonly int FaceHalfId = Shader.PropertyToID("_FaceHalf");

        private static Material ironMaterial;

        private static bool ironLooked;

        /// <summary>The MapusIron material, or null - the pieces are then flat gunmetal, which
        /// still says the cell is shut.</summary>
        public static Material IronMaterial()
        {
            if (!ironLooked)
            {
                ironLooked = true;
                Shader shader = Shader.Find("ProjectBlock/MapusIron");
                if (shader == null)
                {
                    shader = Resources.Load<Shader>("Shaders/MapusIron");
                }
                if (shader != null && shader.isSupported)
                {
                    ironMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
            }
            return ironMaterial;
        }

        private static Material pitMaterial;

        private static bool pitLooked;

        /// <summary>The MapusPit material, or null - the cell is then a flat dark slot.</summary>
        public static Material PitMaterial()
        {
            if (!pitLooked)
            {
                pitLooked = true;
                Shader shader = Shader.Find("ProjectBlock/MapusPit");
                if (shader == null)
                {
                    shader = Resources.Load<Shader>("Shaders/MapusPit");
                }
                if (shader != null && shader.isSupported)
                {
                    pitMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
            }
            return pitMaterial;
        }

        // =================================================================== pooling

        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

        private Material plainMaterial;

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
                var go = new GameObject("Mapus");
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
    }
}
