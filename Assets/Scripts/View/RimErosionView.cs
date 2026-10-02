// PURPOSE: The anti-stalling clock's RIM EROSION, drawn. The arena used to simply SNAP: the turn
// that ran the draw pile dry once too often replaced the board with a smaller one, and the view
// rebuilt the grid at the new cell count on the same frame - a row and a column gone, every
// surviving cell a sixth bigger and half a cell over, all in one frame and with no picture.
//
// IT IS THE ARENA LOSING ITS EDGE, NOT AN EXPLOSION. The erosion pays nothing and is never a
// sweep, so nothing here bursts, flashes or shakes. Four overlapping beats on one clock, a little
// over a second:
//
//   HOLD     the old arena exactly as it stood, for a breath. The new board has already been
//            built, but it is held at the OLD cell size and the OLD place (BoardView.SetInflate +
//            SetInflateOffset) under the old plate, so the first frame shows no change at all.
//   CRACK    a fracture runs the seam between the ground that stays and the ground that goes,
//            from the corner the two lost bands share. Dark, with a dull wine ember under it -
//            the erosion's own colour (the dead zone's), and a gradient that dies at its own edge.
//   BREAK    the bands let go a cell at a time, nearest the crack's origin first: each piece is
//            the old plate's OWN pixels for that cell (cut from the texture the surface handed
//            over) with the floor or the cube that stood on it, and it slides off outward as it
//            sinks - smaller, darker, a few degrees turned - with a few crumbs and a puff of dust.
//            Never a fade in place: ground that only fades was never there.
//   REFIT    what is left grows into the room, eased at both ends: cell size and place travel
//            together, so the survivors never jump.
//
// THE VIEW DECIDES NOTHING. Which cells went and which cubes stood in them is RimErosionVisuals
// (RoundEngine.LastRimErosion); this only knows where a cell is on the screen and what it looked
// like. The cubes are drawn from the report rather than from the board as shown, because the
// shown board is a turn old: a block placed INTO the band on the very turn it went is not on it,
// and a cube a line took this turn still is.
//
// IT LIVES OUTSIDE THE BOARD'S TRANSFORM. The proxies belong to the OLD arena, which no longer
// exists: they sit under a root that copies the transform the board had when it was captured, so
// the refit moves the survivors and leaves the falling rim where it broke off.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Plays one rim erosion. Owned by the controller; one at a time.</summary>
    public sealed class RimErosionView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Every length of the event, in seconds; distances in cells.</summary>
        public static class Style
        {
            /// <summary>When the fracture starts, how long it takes to run the whole seam, and how
            /// long one piece of it takes to open.</summary>
            public static float CrackStart = 0.06f;

            public static float CrackRun = 0.22f;

            public static float CrackGrow = 0.08f;

            public static float CrackWidth = 0.05f;

            public static float CrackJag = 0.07f;

            public static float EmberWidth = 0.34f;

            public static float EmberAlpha = 0.42f;

            /// <summary>The first piece lets go here; each further one a little later per cell of
            /// distance from the crack's origin, the whole stagger never longer than the cap.</summary>
            public static float BreakStart = 0.27f;

            public static float BreakStagger = 0.032f;

            public static float BreakStaggerCap = 0.3f;

            /// <summary>One piece's fall: how long, how far out, how small, how turned, how dark,
            /// and the share of the fall it is still fully opaque for.</summary>
            public static float BreakSeconds = 0.5f;

            public static float FallDistance = 0.62f;

            public static float FallScale = 0.74f;

            public static float FallTurn = 10f;

            public static float FallDarken = 0.38f;

            public static float FadeFrom = 0.42f;

            /// <summary>The tremor a piece takes while the crack passes it, in cells (about a
            /// pixel), and how dark the doomed ground goes before it lets go.</summary>
            public static float Tremble = 0.014f;

            public static float DoomedDarken = 0.8f;

            /// <summary>The old plate gives way to the pieces cut from it over this long.</summary>
            public static float PlateFade = 0.12f;

            /// <summary>The refit starts this long after the LAST piece has let go.</summary>
            public static float RefitAfterLastBreak = 0.16f;

            public static float RefitSeconds = 0.52f;

            public static int CrumbsPerPiece = 3;

            public static Color Crack = new Color(0.020f, 0.022f, 0.032f, 0.95f);

            public static Color Ember = new Color(0.62f, 0.17f, 0.19f, 1f);

            public static Color Dust = new Color(0.40f, 0.43f, 0.50f, 0.20f);
        }

        /// <summary>Sorting, against the board's own: plate -3, cells 1, previews 2.</summary>
        private const int OldPlateOrder = -2;

        private const int ChunkOrder = -1;

        private const int EmberOrder = 0;

        private const int CellOrder = 1;

        private const int CrackOrder = 2;

        private const int CrumbOrder = 3;

        /// <summary>The cues, set by the controller.</summary>
        public SoundFx Sfx;

        /// <summary>True while an erosion is on screen - the controller locks placement, because a
        /// cell is not where the pointer says it is until the refit lands.</summary>
        public bool Playing
        {
            get { return routine != null; }
        }

        // =================================================================== capture

        /// <summary>What the arena was, taken BEFORE the rebuild that replaces it.</summary>
        public sealed class Capture
        {
            public RimErosionVisuals Report;
            public float CellBefore;
            public Vector2 RefOld;
            public GridPos RefCell;
            public Transform Parent;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public BoardSurfaceView.PlateSnapshot Plate;
            public readonly List<CellLook> Cells = new List<CellLook>();
        }

        /// <summary>One lost cell as it stood: where, what its floor looked like and the cube on
        /// it (null tile: none).</summary>
        public struct CellLook
        {
            public GridPos Cell;
            public Vector2 Local;
            public bool Ground;
            public Color Floor;
            public Sprite Tile;
            public Color TileColour;
        }

        /// <summary>
        /// Takes the old arena off <paramref name="view"/>: its transform, its plate, and every
        /// cell the report says was lost. Returns null - and takes nothing - when the view is not
        /// showing the arena the report describes.
        /// </summary>
        public Capture Take(BoardView view, RimErosionVisuals report)
        {
            GameBoard before = view != null ? view.Board : null;
            if (before == null || report == null
                || before.MinX != report.BeforeMinX || before.MinY != report.BeforeMinY
                || before.Width != report.BeforeWidth || before.Height != report.BeforeHeight)
            {
                return null;
            }
            var shot = new Capture
            {
                Report = report,
                CellBefore = view.CellWorldSize,
                RefCell = new GridPos(report.AfterMinX, report.AfterMinY),
                Parent = view.transform.parent,
                Position = view.transform.localPosition,
                Rotation = view.transform.localRotation,
                Scale = view.transform.localScale
            };
            shot.RefOld = view.CellToWorld(shot.RefCell);
            for (int x = report.BeforeMinX; x < report.BeforeMinX + report.BeforeWidth; x++)
            {
                for (int y = report.BeforeMinY; y < report.BeforeMinY + report.BeforeHeight; y++)
                {
                    var cell = new GridPos(x, y);
                    if (!report.WasRemoved(cell))
                    {
                        continue;
                    }
                    var look = new CellLook
                    {
                        Cell = cell,
                        Local = view.CellToWorld(cell),
                        Ground = before.IsInside(cell) || before.IsDead(cell),
                        Floor = view.EmptyFloorColour(cell),
                        TileColour = Color.white
                    };
                    int at = report.DestroyedCells.IndexOf(cell);
                    if (at >= 0)
                    {
                        // What the board last SHOWED there when it showed a cube (a blind round
                        // stays blind); otherwise the cube the rules say went - it was placed this
                        // very turn and never painted.
                        Sprite tile;
                        Color colour;
                        if (view.TryCubeLook(cell, 0f, out tile, out colour))
                        {
                            look.Tile = tile;
                            look.TileColour = colour;
                        }
                        else if (!view.IsDark && at < report.DestroyedCubes.Count)
                        {
                            Cube cube = report.DestroyedCubes[at];
                            look.Tile = view.FaceOf(cube);
                            look.TileColour = ViewUtil.CubeTileColor(cube, look.Tile);
                        }
                    }
                    shot.Cells.Add(look);
                }
            }
            shot.Plate = view.DetachSurfacePlate();
            return shot;
        }

        // =================================================================== play

        private Coroutine routine;
        private Transform root;
        private BoardView playingOn;
        private readonly List<Object> owned = new List<Object>();

        /// <summary>
        /// Plays the erosion over <paramref name="view"/>, which has ALREADY been rebuilt for the
        /// smaller arena. <paramref name="centre"/> is the point the arena is laid out about (the
        /// one handed to BoardView.Rebuild) - the same before and after.
        /// </summary>
        public void Play(BoardView view, Capture shot, Vector2 centre)
        {
            Stop();
            if (view == null || shot == null || view.Board == null)
            {
                Discard(shot);
                return;
            }
            playingOn = view;
            routine = StartCoroutine(Run(view, shot, centre));
        }

        /// <summary>Ends whatever is playing and puts the arena back at its true size and place.
        /// </summary>
        public void Stop()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            Cleanup();
        }

        private void OnDisable()
        {
            routine = null;
            Cleanup();
        }

        private void Cleanup()
        {
            if (playingOn != null)
            {
                playingOn.SetInflate(1f);
                playingOn.SetInflateOffset(Vector2.zero);
                playingOn = null;
            }
            if (root != null)
            {
                Destroy(root.gameObject);
                root = null;
            }
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != null)
                {
                    Destroy(owned[i]);
                }
            }
            owned.Clear();
        }

        /// <summary>A capture that will not be played still owns a plate and its texture.</summary>
        private static void Discard(Capture shot)
        {
            if (shot == null || shot.Plate == null)
            {
                return;
            }
            if (shot.Plate.Renderer != null)
            {
                Destroy(shot.Plate.Renderer.gameObject);
            }
            if (shot.Plate.Texture != null)
            {
                Destroy(shot.Plate.Texture);
            }
            shot.Plate = null;
        }

        private sealed class Piece
        {
            public Transform Root;
            public SpriteRenderer Chunk;
            public SpriteRenderer Cell;
            public Color CellBase;
            public Vector2 From;
            public Vector2 Out;
            public float Delay;
            public float Turn;
            public float Phase;
            public float Distance;
            public bool Broke;
        }

        private sealed class Fissure
        {
            public Transform Line;
            public SpriteRenderer LineRenderer;
            public SpriteRenderer Ember;
            public Vector2 A;
            public Vector2 B;
            public float Delay;
        }

        private sealed class Crumb
        {
            public SpriteRenderer Renderer;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Size;
            public float Spin;
            public Color Base;
            public bool Soft;
        }

        private IEnumerator Run(BoardView view, Capture shot, Vector2 centre)
        {
            RimErosionVisuals report = shot.Report;
            GameBoard after = view.Board;
            float cell = shot.CellBefore;
            float s0 = cell / Mathf.Max(0.0001f, view.CellWorldSize);
            // Where the survivors stood, against where the rebuilt arena puts them at the old cell
            // size: the difference is one constant for every cell, so one cell measures it.
            Vector2 refNew = view.CellToWorld(shot.RefCell);
            Vector2 shift = (shot.RefOld - centre) - s0 * (refNew - centre);
            view.SetInflate(s0);
            view.SetInflateOffset(shift);

            var rootGo = new GameObject("RimErosion");
            root = rootGo.transform;
            root.SetParent(shot.Parent, false);
            root.localPosition = shot.Position;
            root.localRotation = shot.Rotation;
            root.localScale = shot.Scale;

            // ---- the old plate, standing over the new one until the pieces take over
            SpriteRenderer oldPlate = shot.Plate != null ? shot.Plate.Renderer : null;
            Texture2D oldTexture = shot.Plate != null ? shot.Plate.Texture : null;
            Vector2 plateCentre = centre;
            Vector2 plateSpan = Vector2.zero;
            Vector3 plateScale = Vector3.one;
            float platePpu = 100f;
            if (oldTexture != null)
            {
                owned.Add(oldTexture);
            }
            if (oldPlate != null && oldPlate.sprite != null)
            {
                oldPlate.transform.SetParent(root, false);
                oldPlate.sortingOrder = OldPlateOrder;
                plateCentre = oldPlate.transform.localPosition;
                plateScale = oldPlate.transform.localScale;
                Vector2 bounds = oldPlate.sprite.bounds.size;
                plateSpan = new Vector2(bounds.x * plateScale.x, bounds.y * plateScale.y);
                platePpu = oldPlate.sprite.pixelsPerUnit;
                owned.Add(oldPlate.sprite);
            }
            // The plate's CELL area, in the old arena's units - a piece on its edge takes the
            // frame and the shadow beyond it along.
            Vector2 areaHalf = shot.Plate != null
                ? new Vector2(shot.Plate.CellsWide, shot.Plate.CellsHigh) * (cell * 0.5f)
                : Vector2.zero;

            // ---- where the crack starts: the corner the lost bands share, or the middle of the
            // one side that went
            int afterMaxX = report.AfterMinX + report.AfterWidth - 1;
            int afterMaxY = report.AfterMinY + report.AfterHeight - 1;
            bool lostLeft = report.BeforeMinX < report.AfterMinX;
            bool lostRight = report.BeforeMinX + report.BeforeWidth - 1 > afterMaxX;
            bool lostBottom = report.BeforeMinY < report.AfterMinY;
            bool lostTop = report.BeforeMinY + report.BeforeHeight - 1 > afterMaxY;
            float originX = lostRight && !lostLeft ? afterMaxX + 0.5f
                : lostLeft && !lostRight ? report.AfterMinX - 0.5f
                : (report.AfterMinX + afterMaxX) * 0.5f;
            float originY = lostTop && !lostBottom ? afterMaxY + 0.5f
                : lostBottom && !lostTop ? report.AfterMinY - 0.5f
                : (report.AfterMinY + afterMaxY) * 0.5f;
            var origin = new Vector2(originX, originY);

            // ---- the pieces
            var pieces = new List<Piece>();
            float farthest = 0.0001f;
            for (int i = 0; i < shot.Cells.Count; i++)
            {
                CellLook look = shot.Cells[i];
                var piece = new Piece();
                var go = new GameObject("RimPiece");
                piece.Root = go.transform;
                piece.Root.SetParent(root, false);
                piece.Root.localPosition = look.Local;
                piece.From = look.Local;
                float ox = look.Cell.X > afterMaxX ? 1f : look.Cell.X < report.AfterMinX ? -1f : 0f;
                float oy = look.Cell.Y > afterMaxY ? 1f : look.Cell.Y < report.AfterMinY ? -1f : 0f;
                piece.Out = new Vector2(ox, oy).normalized;
                piece.Distance = Vector2.Distance(new Vector2(look.Cell.X, look.Cell.Y), origin);
                farthest = Mathf.Max(farthest, piece.Distance);
                uint h = Hash(look.Cell.X, look.Cell.Y);
                piece.Turn = ((h & 1u) == 0u ? 1f : -1f) * Mathf.Lerp(0.55f, 1f, ((h >> 3) & 255u) / 255f);
                piece.Phase = ((h >> 11) & 1023u) / 1023f * 6.2831853f;
                if (look.Ground && oldTexture != null && plateSpan.x > 0f && plateSpan.y > 0f)
                {
                    piece.Chunk = CutChunk(piece.Root, look.Local, cell, oldTexture, plateCentre, plateSpan,
                        plateScale, platePpu, areaHalf);
                }
                if (look.Ground)
                {
                    if (look.Tile != null)
                    {
                        piece.Cell = ViewUtil.MakeCell(piece.Root, "Cube", Vector2.zero,
                            cell * BoardView.CubeCellShare, look.TileColour, CellOrder);
                        ViewUtil.ApplyTile(piece.Cell, look.Tile, cell * BoardView.CubeCellShare);
                        piece.Cell.color = look.TileColour;
                        piece.CellBase = look.TileColour;
                    }
                    else
                    {
                        piece.Cell = ViewUtil.MakeCell(piece.Root, "Floor", Vector2.zero,
                            cell * BoardView.EmptyCellShare, look.Floor, CellOrder);
                        piece.CellBase = look.Floor;
                    }
                }
                pieces.Add(piece);
            }
            float stagger = Mathf.Min(Style.BreakStagger, Style.BreakStaggerCap / farthest);
            float lastBreak = Style.BreakStart;
            for (int i = 0; i < pieces.Count; i++)
            {
                pieces[i].Delay = Style.BreakStart + stagger * pieces[i].Distance;
                lastBreak = Mathf.Max(lastBreak, pieces[i].Delay);
            }

            // ---- the fracture: every edge a lost cell shares with one that stays
            var fissures = new List<Fissure>();
            float seamFar = 0.0001f;
            for (int i = 0; i < shot.Cells.Count; i++)
            {
                GridPos c = shot.Cells[i].Cell;
                for (int d = 0; d < 4; d++)
                {
                    int dx = d == 0 ? 1 : d == 1 ? -1 : 0;
                    int dy = d == 2 ? 1 : d == 3 ? -1 : 0;
                    var inner = new GridPos(c.X + dx, c.Y + dy);
                    if (!report.InAfter(inner) || !(after.IsInside(inner) || after.IsDead(inner)))
                    {
                        continue;
                    }
                    // The shared edge, in cell units, and its two ends.
                    var mid = new Vector2(c.X + dx * 0.5f, c.Y + dy * 0.5f);
                    var along = new Vector2(dy != 0 ? 1f : 0f, dx != 0 ? 1f : 0f);
                    var across = new Vector2(dx, dy);
                    Vector2 a = mid - along * 0.5f;
                    Vector2 b = mid + along * 0.5f;
                    uint h = Hash(c.X * 2 + dx + 97, c.Y * 2 + dy + 31);
                    float jag = (((h >> 4) & 255u) / 255f - 0.5f) * 2f * Style.CrackJag;
                    Vector2 kink = mid + across * jag + along * ((((h >> 13) & 255u) / 255f - 0.5f) * 0.3f);
                    AddFissure(fissures, shot.Cells[i].Local, c, a, kink, cell, origin, ref seamFar);
                    AddFissure(fissures, shot.Cells[i].Local, c, kink, b, cell, origin, ref seamFar);
                }
            }
            for (int i = 0; i < fissures.Count; i++)
            {
                fissures[i].Delay = Style.CrackStart + Style.CrackRun * (fissures[i].Delay / seamFar);
            }

            float refitStart = lastBreak + Style.RefitAfterLastBreak;
            float end = Mathf.Max(refitStart + Style.RefitSeconds, lastBreak + Style.BreakSeconds);
            var crumbs = new List<Crumb>();
            bool rumbled = false;
            bool broke = false;
            float t = 0f;
            while (t < end)
            {
                if (view == null || view.Board != after)
                {
                    break; // the arena was rebuilt under us: nothing here describes it any more
                }
                float dt = Time.deltaTime;
                t += dt;

                // ---- CRACK
                if (!rumbled && t >= Style.CrackStart)
                {
                    rumbled = true;
                    if (Sfx != null)
                    {
                        Sfx.Rumble();
                    }
                }
                float crackOut = Mathf.Clamp01((t - lastBreak - 0.06f) / 0.22f);
                for (int i = 0; i < fissures.Count; i++)
                {
                    Fissure f = fissures[i];
                    float k = Mathf.Clamp01((t - f.Delay) / Style.CrackGrow);
                    float open = 1f - (1f - k) * (1f - k);
                    Vector2 tip = Vector2.Lerp(f.A, f.B, open);
                    Vector2 run = tip - f.A;
                    float length = run.magnitude;
                    float widen = 1f + 0.7f * Mathf.Clamp01((t - Style.BreakStart) / 0.12f);
                    f.Line.localPosition = (f.A + tip) * 0.5f;
                    f.Line.localScale = new Vector3(length + Style.CrackWidth * cell * 0.6f,
                        Style.CrackWidth * cell * widen, 1f);
                    Color lc = Style.Crack;
                    lc.a *= (k > 0f ? 1f : 0f) * (1f - crackOut);
                    f.LineRenderer.color = lc;
                    Color ec = Style.Ember;
                    // The ember comes up with the crack and is spent by the time the rim has gone.
                    ec.a = Style.EmberAlpha * open * (1f - crackOut)
                        * (0.82f + 0.18f * Mathf.Sin(t * 9f + f.Delay * 40f));
                    f.Ember.color = ec;
                }

                // ---- the old plate gives way to the pieces cut out of it
                if (oldPlate != null)
                {
                    float pk = Mathf.Clamp01((t - Style.BreakStart) / Style.PlateFade);
                    Color pc = oldPlate.color;
                    pc.a = 1f - pk;
                    oldPlate.color = pc;
                    oldPlate.enabled = pk < 1f;
                }

                // ---- BREAK
                for (int i = 0; i < pieces.Count; i++)
                {
                    Piece p = pieces[i];
                    float k = Mathf.Clamp01((t - p.Delay) / Style.BreakSeconds);
                    // The crack reaching this piece: it darkens and trembles until it lets go.
                    float doom = Mathf.Clamp01((t - Style.CrackStart)
                        / Mathf.Max(0.01f, p.Delay - Style.CrackStart));
                    float shake = Style.Tremble * cell * doom * doom * (k > 0f ? 0f : 1f);
                    var tremble = new Vector2(
                        Mathf.Sin(t * 71f + p.Phase) * 0.6f + Mathf.Sin(t * 43f + p.Phase * 1.7f) * 0.4f,
                        Mathf.Sin(t * 59f + p.Phase * 2.3f) * 0.6f + Mathf.Sin(t * 37f + p.Phase) * 0.4f) * shake;
                    // Falling off: it starts slowly and goes, never the other way round.
                    float fall = k * k;
                    p.Root.localPosition = p.From + tremble + p.Out * (Style.FallDistance * cell * fall);
                    float size = Mathf.Lerp(1f, Style.FallScale, fall);
                    p.Root.localScale = new Vector3(size, size, 1f);
                    p.Root.localRotation = Quaternion.Euler(0f, 0f, Style.FallTurn * p.Turn * fall);
                    float shade = Mathf.Lerp(1f, Style.DoomedDarken, doom) * Mathf.Lerp(1f, Style.FallDarken, fall);
                    float alpha = 1f - Mathf.Clamp01((k - Style.FadeFrom) / (1f - Style.FadeFrom));
                    if (p.Chunk != null)
                    {
                        p.Chunk.color = new Color(shade, shade, shade, alpha);
                    }
                    if (p.Cell != null)
                    {
                        p.Cell.color = new Color(p.CellBase.r * shade, p.CellBase.g * shade,
                            p.CellBase.b * shade, p.CellBase.a * alpha);
                    }
                    if (!p.Broke && k > 0f)
                    {
                        p.Broke = true;
                        if (!broke)
                        {
                            broke = true;
                            if (Sfx != null)
                            {
                                Sfx.Squish();
                            }
                        }
                        if (p.Chunk != null || p.Cell != null)
                        {
                            SpawnDebris(crumbs, p, cell);
                        }
                    }
                }
                StepCrumbs(crumbs, dt, cell);

                // ---- REFIT: cell size and place travel together, eased at both ends
                float rk = Mathf.Clamp01((t - refitStart) / Style.RefitSeconds);
                float re = rk * rk * rk * (rk * (rk * 6f - 15f) + 10f);
                view.SetInflate(Mathf.Lerp(s0, 1f, re));
                view.SetInflateOffset(shift * (1f - re));
                yield return null;
            }
            routine = null;
            Cleanup();
        }

        /// <summary>One straight run of the fracture, from <paramref name="a"/> to
        /// <paramref name="b"/> in cell units. Its Delay is left as its distance from the origin
        /// for the caller to turn into time.</summary>
        private void AddFissure(List<Fissure> into, Vector2 cellLocal, GridPos cellAt, Vector2 a, Vector2 b,
            float cell, Vector2 origin, ref float farthest)
        {
            // Cell units -> the old arena's local units, measured off the cell's own centre.
            Vector2 la = cellLocal + (a - new Vector2(cellAt.X, cellAt.Y)) * cell;
            Vector2 lb = cellLocal + (b - new Vector2(cellAt.X, cellAt.Y)) * cell;
            // Always opened AWAY from the origin, so the crack runs rather than appears.
            float da = Vector2.Distance(a, origin);
            float db = Vector2.Distance(b, origin);
            if (db < da)
            {
                Vector2 swap = la;
                la = lb;
                lb = swap;
                float d = da;
                da = db;
                db = d;
            }
            var f = new Fissure { A = la, B = lb, Delay = da };
            farthest = Mathf.Max(farthest, da);
            float angle = Mathf.Atan2(lb.y - la.y, lb.x - la.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(la, lb);
            SpriteRenderer ember = ViewUtil.MakeRect(root, "RimEmber", (la + lb) * 0.5f,
                new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0f), EmberOrder);
            ember.sprite = ViewUtil.GlowSprite;
            Vector2 glow = ember.sprite.bounds.size;
            ember.transform.localScale = new Vector3(
                (length + Style.EmberWidth * cell) / Mathf.Max(0.0001f, glow.x),
                Style.EmberWidth * cell / Mathf.Max(0.0001f, glow.y), 1f);
            ember.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            f.Ember = ember;
            SpriteRenderer line = ViewUtil.MakeRect(root, "RimCrack", la, new Vector2(0f, 0f),
                new Color(0f, 0f, 0f, 0f), CrackOrder);
            line.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            f.Line = line.transform;
            f.LineRenderer = line;
            into.Add(f);
        }

        /// <summary>
        /// The old plate's own pixels for one cell, as a sprite under the piece. A cell on the
        /// plate's edge takes everything beyond it along - the bevelled frame and the shadow -
        /// so the rim that falls away is the rim that was there.
        /// </summary>
        private SpriteRenderer CutChunk(Transform piece, Vector2 cellLocal, float cell, Texture2D texture,
            Vector2 plateCentre, Vector2 plateSpan, Vector3 plateScale, float ppu, Vector2 areaHalf)
        {
            float half = cell * 0.5f;
            float xMin = cellLocal.x - half;
            float xMax = cellLocal.x + half;
            float yMin = cellLocal.y - half;
            float yMax = cellLocal.y + half;
            float eps = cell * 0.02f;
            if (xMin <= plateCentre.x - areaHalf.x + eps)
            {
                xMin = plateCentre.x - plateSpan.x * 0.5f;
            }
            if (xMax >= plateCentre.x + areaHalf.x - eps)
            {
                xMax = plateCentre.x + plateSpan.x * 0.5f;
            }
            if (yMin <= plateCentre.y - areaHalf.y + eps)
            {
                yMin = plateCentre.y - plateSpan.y * 0.5f;
            }
            if (yMax >= plateCentre.y + areaHalf.y - eps)
            {
                yMax = plateCentre.y + plateSpan.y * 0.5f;
            }
            int w = texture.width;
            int h = texture.height;
            // Rounded, not floored: two neighbours round their shared edge to the same texel.
            int px0 = Mathf.Clamp(Mathf.RoundToInt(((xMin - plateCentre.x) / plateSpan.x + 0.5f) * w), 0, w);
            int px1 = Mathf.Clamp(Mathf.RoundToInt(((xMax - plateCentre.x) / plateSpan.x + 0.5f) * w), 0, w);
            int py0 = Mathf.Clamp(Mathf.RoundToInt(((yMin - plateCentre.y) / plateSpan.y + 0.5f) * h), 0, h);
            int py1 = Mathf.Clamp(Mathf.RoundToInt(((yMax - plateCentre.y) / plateSpan.y + 0.5f) * h), 0, h);
            if (px1 - px0 < 1 || py1 - py0 < 1)
            {
                return null; // the cell lay outside the plate (bonus ground beside the arena)
            }
            Sprite sprite = Sprite.Create(texture, new Rect(px0, py0, px1 - px0, py1 - py0),
                new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            owned.Add(sprite);
            // Placed from the texels actually cut, so the copy lies on the plate it came from.
            var centre = new Vector2(
                plateCentre.x + ((px0 + px1) * 0.5f / w - 0.5f) * plateSpan.x,
                plateCentre.y + ((py0 + py1) * 0.5f / h - 0.5f) * plateSpan.y);
            SpriteRenderer chunk = ViewUtil.MakeRect(piece, "Ground", centre - cellLocal,
                new Vector2(1f, 1f), Color.white, ChunkOrder);
            chunk.sprite = sprite;
            chunk.transform.localScale = plateScale;
            return chunk;
        }

        /// <summary>What a piece throws as it lets go: a few crumbs of its own ground off the
        /// broken edge and one soft puff of dust. Deterministic per cell.</summary>
        private void SpawnDebris(List<Crumb> crumbs, Piece p, float cell)
        {
            Vector2 side = new Vector2(-p.Out.y, p.Out.x);
            Vector2 edge = p.From - p.Out * (cell * 0.42f);
            uint h = Hash(Mathf.RoundToInt(p.From.x * 131f), Mathf.RoundToInt(p.From.y * 173f));
            Color ground = BoardSurfaceView.Style.Surface;
            for (int i = 0; i < Style.CrumbsPerPiece; i++)
            {
                h = h * 1664525u + 1013904223u;
                float a = ((h >> 8) & 1023u) / 1023f;
                float b = ((h >> 18) & 1023u) / 1023f;
                var crumb = new Crumb();
                crumb.Size = cell * Mathf.Lerp(0.05f, 0.11f, a);
                crumb.Position = edge + side * ((b - 0.5f) * cell * 0.8f);
                crumb.Velocity = p.Out * (cell * Mathf.Lerp(1.1f, 2.3f, b))
                    + side * ((a - 0.5f) * cell * 1.6f);
                crumb.Life = Mathf.Lerp(0.3f, 0.5f, a);
                crumb.Spin = (b - 0.5f) * 520f;
                // The board's own material, lifted enough to be seen against it. Never the cube's
                // tint: a painted tile's tint is white, and a white speck is a spark.
                float lift = Mathf.Lerp(1.25f, 1.9f, a);
                crumb.Base = new Color(ground.r * lift, ground.g * lift, ground.b * lift, 1f);
                crumb.Renderer = ViewUtil.MakeRect(root, "RimCrumb", crumb.Position,
                    new Vector2(crumb.Size, crumb.Size), crumb.Base, CrumbOrder);
                crumbs.Add(crumb);
            }
            var puff = new Crumb();
            puff.Soft = true;
            puff.Size = cell * 0.7f;
            puff.Position = edge;
            puff.Velocity = p.Out * (cell * 0.5f);
            puff.Life = 0.42f;
            puff.Base = Style.Dust;
            puff.Renderer = ViewUtil.MakeRect(root, "RimDust", puff.Position,
                new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0f), EmberOrder);
            puff.Renderer.sprite = ViewUtil.GlowSprite;
            crumbs.Add(puff);
        }

        private static void StepCrumbs(List<Crumb> crumbs, float dt, float cell)
        {
            for (int i = crumbs.Count - 1; i >= 0; i--)
            {
                Crumb c = crumbs[i];
                c.Age += dt;
                float k = Mathf.Clamp01(c.Age / c.Life);
                if (c.Renderer == null || k >= 1f)
                {
                    if (c.Renderer != null)
                    {
                        Destroy(c.Renderer.gameObject);
                    }
                    crumbs.RemoveAt(i);
                    continue;
                }
                // Thrown hard and stopped by the air, not carried at a steady speed.
                c.Velocity *= Mathf.Exp(-dt * (c.Soft ? 3f : 4.5f));
                c.Position += c.Velocity * dt;
                Transform tr = c.Renderer.transform;
                tr.localPosition = c.Position;
                Color col = c.Base;
                if (c.Soft)
                {
                    Vector2 glow = c.Renderer.sprite.bounds.size;
                    float size = c.Size * Mathf.Lerp(0.6f, 1.35f, 1f - (1f - k) * (1f - k));
                    tr.localScale = new Vector3(size / Mathf.Max(0.0001f, glow.x),
                        size / Mathf.Max(0.0001f, glow.y), 1f);
                    col.a = c.Base.a * Mathf.Sin(Mathf.Clamp01(k * 1.15f) * Mathf.PI);
                }
                else
                {
                    float size = c.Size * Mathf.Lerp(1f, 0.45f, k);
                    tr.localScale = new Vector3(size, size, 1f);
                    tr.localRotation = Quaternion.Euler(0f, 0f, c.Spin * c.Age);
                    col.a = 1f - k * k;
                }
                c.Renderer.color = col;
            }
        }

        private static uint Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }
}
