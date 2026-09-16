// PURPOSE: "Buzluk" - water that reached a wall FREEZING there, and the ice it leaves standing.
//
// HALF OF THIS IS AN ANIMATION AND HALF OF IT IS A PRESENCE, which is why it is owned by
// BoardView like the rot, the snake and the press. The freeze plays once; the ice it produces has
// to go on being ice through every repaint, every rebuild and the rest of the round. Both are the
// same numbers on the same shared material (Resources/Shaders/CryoFreeze), so there is no
// hand-off at the end and nothing to get wrong there.
//
// THE FIRST PASS WAS ONE PROGRESS VALUE AND IT READ AS A RECOLOUR. A single front crossing the
// cube with the face lerped toward an ice colour behind it is a WIPE, however ragged the front
// is - water and ice came out as two brightnesses of one sprite. A freeze is not one event, so
// it is not one number. Five, overlapping (spec 57 / 112):
//
//   _Slow    1 -> 0     the water's flow dying, and ONLY where it is still liquid
//   _Seeds   0 -> 1     frost buds on the wall
//   _Fingers 0 -> 1     crystals reaching into the water
//   _Film    0 -> 1.16  the sheet closing the gaps between them
//   _Thick   0 -> 1     the shell thickening once the surface is closed
//
// and then a LOCK: a 2.5% seating, a couple of glints, done. The liquid pocket is not one of
// them because it is not drawn - it is whatever the film has not reached yet, which is why it
// comes out irregular and off-centre instead of a shrinking disc.
//
// Standing ice is those five at their ends. The animation is them on the way.
//
// THE VIEW DECIDES NOTHING. Which cubes froze, what they used to be and - the one that matters -
// WHICH SIDES ARE WALL all come from Core's own report (BuzlukJoker.FreezeOn -> FreezeVisuals,
// keyed on a SERIAL so a repaint mid-freeze cannot restart it). A View that worked out the walls
// itself would be a second copy of the rule, and the rule is the whole subject of the picture:
// this water froze BECAUSE it reached a wall. A freeze that grows out of the middle of the cube
// says nothing, and a freeze on the wrong side says something false.
//
// A CORNER TOUCHES TWO WALLS and gets two fronts that meet in the middle. Nothing here codes for
// that: the report carries two flags and the shader unions the two distances with MIN.
//
// WHAT IT IS NOT: no snow, no burst, no shake, no full-screen anything, no flash. A cube quietly
// stopped moving and became a different material. The loudest moment in it is a 2.5% scale
// response as the crystal locks.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class IceFreezeView : MonoBehaviour
    {
        /// <summary>Every number, in one place.</summary>
        public static class Style
        {
            // ---- the timeline, per cube. EACH STAGE IS A START AND A LENGTH, in seconds,
            // and they OVERLAP heavily - which is what lets the whole thing take 0.8s without
            // feeling slow. Laid out as (start, length) rather than as a chain precisely so the
            // overlap is visible in the numbers.

            /// <summary>Stage A: the water's flow dying. It starts before anything else and
            /// runs under all of it; without that beat the freeze reads as a sprite swap.
            /// </summary>
            public static float SlowAt = 0f;

            public static float SlowFor = 0.30f;

            /// <summary>Stage B: frost buds on the wall.</summary>
            public static float SeedsAt = 0.07f;

            public static float SeedsFor = 0.10f;

            /// <summary>Stage C: crystal fingers reaching into the water.</summary>
            public static float FingersAt = 0.12f;

            public static float FingersFor = 0.21f;

            /// <summary>Stage D: the thin film closing the gaps between them - and, behind it,
            /// the liquid pocket shrinking, which is the same number seen from the other side.
            /// The longest stage, because it is the one the eye actually follows.</summary>
            public static float FilmAt = 0.20f;

            public static float FilmFor = 0.38f;

            /// <summary>Stage E: the shell thickening after the surface has closed. Without it
            /// the finished material arrives in one frame and looks cheap.</summary>
            public static float ThickAt = 0.45f;

            public static float ThickFor = 0.20f;

            /// <summary>Stage F: the crystal seating.</summary>
            public static float LockAt = 0.66f;

            public static float LockFor = 0.12f;

            /// <summary>The film runs PAST 1, or the last texel to seal never seals and the
            /// trapped pocket stays a bright blue patch that reads as a hole in the ice.
            /// </summary>
            public static float FilmOver = 1.16f;

            // ---- more than one cube freezing at once ----
            /// <summary>Between one cube and the next along the wall: a cold wave, not a
            /// flashbulb and not five separate cinematics.</summary>
            public static float Stagger = 0.055f;

            /// <summary>A whole turn's freezing never runs past this, however many froze - the
            /// stagger is squeezed instead. A board edge full of water must not become a
            /// nine-second ceremony.</summary>
            public static float TotalCap = 1.15f;

            // ---- the crystal lock ----
            /// <summary>
            /// THE WHOLE SEATING IS UNDER 1.5%, and it is deliberately almost nothing.
            ///
            /// Water really does expand as it freezes, but saying so with SIZE makes the cube
            /// look like a bigger block in the same cell - the one reading this effect must not
            /// have. The expansion is said with the shell instead: the clouding rises, the rim
            /// thickens, the crystal density goes up and the highlight comes forward. What is
            /// left here is just enough to feel the crystal settle.
            /// </summary>
            public static float LockPeak = 1.012f;

            public static float LockDip = 0.997f;

            /// <summary>The shell swelling a hair as it locks - see LockPeak.</summary>
            public static float LockSwell = 1.010f;

            /// <summary>
            /// HARD CEILING on anything this effect scales a cube to, as a multiple of the size
            /// the board gave it. Nothing here may make an ice cube read as a bigger block than
            /// the cube standing next to it; the checker holds the seating under it.
            /// </summary>
            public static float ScaleCeiling = 1.02f;

            // ---- glints ----
            public static int LockGlints = 3;

            /// <summary>Two at the rim's birth - the "cold snap". Never a snow burst.</summary>
            public static int RimGlints = 2;

            public static float GlintSize = 0.115f;

            public static float GlintLife = 0.10f;

            /// <summary>How far out a glint may sit, as a share of the CUBE. Under 1, so even
            /// the furthest one is inside the block's own face and nothing lands in the gap
            /// between two cells.</summary>
            public static float GlintInside = 0.82f;

            /// <summary>Hard ceiling on glints alive at once, for a board edge of ice.</summary>
            public static int GlintCap = 20;

            // ---- the frozen idle ----
            public static float IdleMin = 2.8f;

            public static float IdleMax = 5.0f;

            /// <summary>One pale band drifting through the interior. Very quiet, and never the
            /// whole cube pulsing: a cell that breathes as one is a UI element.</summary>
            public static float IdleSweep = 0.34f;

            // ---- the ice itself (shared material) ----
            public static Color Cold = new Color(0.55f, 0.74f, 0.90f);

            public static Color Frost = new Color(0.90f, 0.95f, 0.99f);

            public static Color Cloud = new Color(0.76f, 0.84f, 0.90f);

            /// <summary>A crystal finger's CORE. Its edge goes to Frost, which is what makes it
            /// read as a filament with a cross-section rather than as a drawn line.</summary>
            public static Color Vein = new Color(0.72f, 0.86f, 0.97f);

            /// <summary>How much of the real water face survives in the finished ice (spec 68).
            /// Enough that "there was water in this" is legible on a second look, never enough
            /// to be the first reading.</summary>
            public static float InnerWater = 0.30f;

            /// <summary>...and how much further it is buried as the shell thickens.</summary>
            public static float InnerFade = 0.10f;

            public static float IceBody = 0.30f;

            public static float IceBodyVariation = 0.45f;

            public static float FingerAmount = 0.68f;

            /// <summary>
            /// How hard the frost whitens the cube's edge.
            ///
            /// This is a SILHOUETTE number even though it scales nothing: a near-white band all
            /// the way round a cube, on a board this dark, makes it read as a larger object than
            /// its neighbour. It came down with the rim's thickness for that reason and no other.
            /// </summary>
            public static float RimAmount = 0.60f;

            public static float FilmBand = 0.13f;

            /// <summary>One cube's whole chain, for the lab's labels and for the checker.
            /// </summary>
            public static float Single
            {
                get { return LockAt + LockFor; }
            }
        }

        /// <summary>What the lab can take away, one layer at a time.</summary>
        public static class Layers
        {
            public static bool ShowFrostSeeds = true;

            public static bool ShowCrystalFingers = true;

            public static bool ShowIceFilm = true;

            public static bool ShowShellThickness = true;

            public static bool ShowFinalRim = true;

            public static bool ShowClouding = true;

            public static bool ShowTrappedWater = true;

            public static bool ShowWaterMotion = true;

            public static bool ShowLockGlints = true;

            public static bool ShowLockResponse = true;

            public static bool ShowIdleGlint = true;

            /// <summary>DEV: 1 paints the WALL SIDES the report named (left red, right blue,
            /// down green, up yellow), 2 the finger field, 3 the film field, 4 the liquid
            /// pocket. "Is it coming off the side Core said" and "is the pocket really the last
            /// unsealed region" stop being arguments and become a glance.</summary>
            public static int ShowField;

            public static void AllOn()
            {
                ShowFrostSeeds = true;
                ShowCrystalFingers = true;
                ShowIceFilm = true;
                ShowShellThickness = true;
                ShowFinalRim = true;
                ShowClouding = true;
                ShowTrappedWater = true;
                ShowWaterMotion = true;
                ShowLockGlints = true;
                ShowLockResponse = true;
                ShowIdleGlint = true;
                ShowField = 0;
            }
        }

        /// <summary>Audio hooks. Four moments, named rather than numbered so a sound designer can
        /// read them: the flow beginning to die, the frost touching the wall, the crystal
        /// locking, and the whole turn's freezing finished.</summary>
        public static System.Action<string> Sounded;

        public const string SoundFreezeBegin = "buzluk.freeze.begin";
        public const string SoundFrostContact = "buzluk.frost.contact";
        public const string SoundCrystalLock = "buzluk.crystal.lock";
        public const string SoundFreezeComplete = "buzluk.freeze.complete";

        private sealed class Cell
        {
            public GridPos At;
            public BoardSides Sides;

            /// <summary>Which of IceGrowth's crystal fields this cube grew, and - for a corner -
            /// which it grew from its second wall. Stable per cell, so a wall of ice is never
            /// the same drawing repeated and the same cube looks the same on every repaint.
            /// </summary>
            public int VariantA;

            public int VariantB;

            /// <summary>When this cube's own chain started, on the view's clock. Negative
            /// infinity for ice that was simply already there - standing ice, drawn at the end
            /// of the timeline without ever having played it.</summary>
            public float Start;

            public bool Animating;

            /// <summary>Set once its glints have been thrown, so a repaint cannot throw them
            /// again.</summary>
            public bool SnappedRim;

            public bool SnappedLock;

            public float NextIdle;

            public float IdleAt;

            /// <summary>The size the BOARD last gave this cube, captured whenever the seating is
            /// not running. The seating writes from this rather than from the renderer - see
            /// Paint, and the bug that cost.</summary>
            public float BaseScale;
        }

        private sealed class Glint
        {
            public SpriteRenderer Sprite;
            public Vector2 At;
            public Vector2 Drift;
            public float Born;
            public float Size;
        }

        private BoardView owner;
        private readonly Dictionary<GridPos, Cell> cells = new Dictionary<GridPos, Cell>();
        private readonly List<Glint> glints = new List<Glint>();
        private readonly List<SpriteRenderer> spare = new List<SpriteRenderer>();
        private readonly List<GridPos> gone = new List<GridPos>();
        private MaterialPropertyBlock block;
        private int playedSerial = -1;
        private float clock;
        private float completeAt = -1f;

        private static Sprite glintSprite;

        private static readonly int SlowId = Shader.PropertyToID("_Slow");
        private static readonly int SeedsId = Shader.PropertyToID("_Seeds");
        private static readonly int FingersId = Shader.PropertyToID("_Fingers");
        private static readonly int FilmId = Shader.PropertyToID("_Film");
        private static readonly int ThickId = Shader.PropertyToID("_Thick");
        private static readonly int GlintId = Shader.PropertyToID("_Glint");
        private static readonly int TileAId = Shader.PropertyToID("_TileA");
        private static readonly int TileBId = Shader.PropertyToID("_TileB");
        private static readonly int DebugId = Shader.PropertyToID("_Debug");

        /// <summary>True while at least one cube is still freezing.</summary>
        public bool Busy
        {
            get
            {
                foreach (KeyValuePair<GridPos, Cell> e in cells)
                {
                    if (e.Value.Animating) { return true; }
                }
                return false;
            }
        }

        /// <summary>
        /// A turn's freezing, from Core's own report.
        ///
        /// Called on every repaint with whatever the joker last did, so the SERIAL is what makes
        /// it happen once. An empty report is still a report - a turn where nothing touched a
        /// wall - and consumes its serial without drawing anything.
        /// </summary>
        public void Play(BoardView board, FreezeVisuals report)
        {
            owner = board;
            if (report == null || report.Serial == playedSerial)
            {
                return;
            }
            playedSerial = report.Serial;
            if (!report.Any)
            {
                return;
            }

            // THE STAGGER IS A WAVE ALONG THE WALL, so the order is spatial and deterministic:
            // left to right, then bottom to top. Down one edge of the board that reads as cold
            // travelling along it, which is the picture spec 141 asks for - and it is the same
            // order every time, so nothing here needs a random source.
            var order = new List<FrozenCell>(report.Cells);
            order.Sort(delegate(FrozenCell a, FrozenCell b)
            {
                int byX = a.Cell.X.CompareTo(b.Cell.X);
                return byX != 0 ? byX : a.Cell.Y.CompareTo(b.Cell.Y);
            });

            // Squeezed rather than lengthened: however many froze, the turn is over inside
            // TotalCap. Five cubes at half a second each, one after another, is five cinematics.
            float stagger = Style.Stagger;
            if (order.Count > 1)
            {
                float room = (Style.TotalCap - Style.Single) / (order.Count - 1);
                stagger = Mathf.Min(stagger, Mathf.Max(0f, room));
            }

            for (int i = 0; i < order.Count; i++)
            {
                Cell cell = Ensure(order[i].Cell, order[i].Sides);
                cell.Sides = order[i].Sides;
                cell.Start = clock + i * stagger;
                cell.Animating = true;
                cell.SnappedRim = false;
                cell.SnappedLock = false;
            }
            completeAt = clock + (order.Count - 1) * stagger + Style.Single;
            Announce(SoundFreezeBegin);
        }

        /// <summary>
        /// Every repaint: the ice standing on the board right now.
        ///
        /// Cubes the freeze animated keep their clocks; ice that was simply already there - a
        /// load, a rebuild, a cube that froze while the board was elsewhere - is given the end of
        /// the timeline and drawn as finished. That is the whole of the presence half: there is
        /// no separate "static ice" path to keep in step with the animated one.
        /// </summary>
        public void Sync(BoardView board)
        {
            owner = board;
            GameBoard model = board != null ? board.Board : null;
            if (model == null)
            {
                return;
            }
            gone.Clear();
            foreach (KeyValuePair<GridPos, Cell> e in cells)
            {
                Cube? cube = model.GetCube(e.Key);
                if (!cube.HasValue || cube.Value.Kind != CubeKind.Ice)
                {
                    gone.Add(e.Key);
                }
            }
            for (int i = 0; i < gone.Count; i++)
            {
                Clear(gone[i]);
                cells.Remove(gone[i]);
            }

            List<GridPos> ice = model.CellsOfKind(CubeKind.Ice);
            for (int i = 0; i < ice.Count; i++)
            {
                if (!cells.ContainsKey(ice[i]))
                {
                    // Already frozen when we got here: drawn finished, and never animated.
                    Cell cell = Ensure(ice[i], model.EdgeSidesOf(ice[i]));
                    cell.Start = float.NegativeInfinity;
                    cell.Animating = false;
                }
            }
            Paint();
        }

        /// <summary>Puts every ice cube back to a plain renderer and drops the effect's own
        /// pieces. The board calls this when it is torn down.</summary>
        public void Stop()
        {
            foreach (KeyValuePair<GridPos, Cell> e in cells)
            {
                Clear(e.Key);
            }
            cells.Clear();
            for (int i = 0; i < glints.Count; i++)
            {
                Return(glints[i].Sprite);
            }
            glints.Clear();
            playedSerial = -1;
            completeAt = -1f;
        }

        private Cell Ensure(GridPos at, BoardSides sides)
        {
            Cell cell;
            if (!cells.TryGetValue(at, out cell))
            {
                cell = new Cell
                {
                    At = at,
                    Sides = sides,
                    // Stable per cell, so a field of ice never crystallises in unison and the
                    // same cube looks the same on the next repaint.
                    VariantA = (int)((uint)((at.X * 73856093) ^ (at.Y * 19349663))
                        % (uint)IceGrowth.Variants),
                    VariantB = (int)((uint)((at.X * 19349663) ^ (at.Y * 83492791) ^ 0x5bd1u)
                        % (uint)IceGrowth.Variants),
                    Start = float.NegativeInfinity,
                    NextIdle = clock + Random.Range(Style.IdleMin, Style.IdleMax)
                };
                cells[at] = cell;
            }
            cell.Sides = sides;
            return cell;
        }

        private void LateUpdate()
        {
            // SCALED, deliberately: the animation lab slows an effect down with Time.timeScale,
            // and spec 113's acceptance test is watching the five stages come apart at 0.25x.
            // An unscaled clock here would make that test impossible to run.
            clock += Time.deltaTime;
            Paint();
            PaintGlints();
            if (completeAt > 0f && clock >= completeAt)
            {
                completeAt = -1f;
                Announce(SoundFreezeComplete);
            }
        }

        // ---- the three numbers, per cube, per frame ------------------------------------------

        private void Paint()
        {
            if (owner == null)
            {
                return;
            }
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            foreach (KeyValuePair<GridPos, Cell> entry in cells)
            {
                Cell cell = entry.Value;
                SpriteRenderer r = owner.CellRendererAt(cell.At);
                if (r == null || r.sprite == null)
                {
                    continue;
                }

                float t = clock - cell.Start;
                bool done = cell.Start == float.NegativeInfinity || t >= Style.Single;

                // THE FIVE STAGES. Each is its own window on the cube's own clock and they
                // OVERLAP - the fingers are still growing while the film has started closing
                // behind them, and the pocket is still shrinking while the shell thickens. That
                // overlap is what lets 0.8 seconds not feel like 0.8 seconds.
                float slow, seeds, fingers, film, thick;
                if (done)
                {
                    slow = 0f;
                    seeds = 1f;
                    fingers = 1f;
                    film = Style.FilmOver;
                    thick = 1f;
                    cell.Animating = false;
                }
                else if (t < 0f)
                {
                    // Waiting its turn in the wave: still ordinary water, still flowing. Core has
                    // already turned it to ice, so this is the one place the picture is
                    // deliberately a beat behind the board - which is the point of holding it.
                    slow = 1f;
                    seeds = 0f;
                    fingers = 0f;
                    film = 0f;
                    thick = 0f;
                }
                else
                {
                    slow = 1f - Smooth(Span(t, Style.SlowAt, Style.SlowAt + Style.SlowFor));
                    seeds = Smooth(Span(t, Style.SeedsAt, Style.SeedsAt + Style.SeedsFor));
                    fingers = Smooth(Span(t, Style.FingersAt,
                        Style.FingersAt + Style.FingersFor));
                    film = Smooth(Span(t, Style.FilmAt, Style.FilmAt + Style.FilmFor))
                        * Style.FilmOver;
                    thick = Smooth(Span(t, Style.ThickAt, Style.ThickAt + Style.ThickFor));

                    if (!cell.SnappedRim && seeds > 0.5f)
                    {
                        cell.SnappedRim = true;
                        Announce(SoundFrostContact);
                        Throw(cell, Style.RimGlints, true);
                    }
                    if (!cell.SnappedLock && t >= Style.LockAt)
                    {
                        cell.SnappedLock = true;
                        Announce(SoundCrystalLock);
                        Throw(cell, Style.LockGlints, false);
                    }
                }

                // The switches take a stage AWAY rather than dimming it, so "the film alone" is
                // genuinely the film and not the film over everything before it.
                if (!Layers.ShowWaterMotion) { slow = 0f; }
                if (!Layers.ShowFrostSeeds) { seeds = 0f; }
                if (!Layers.ShowCrystalFingers) { fingers = 0f; }
                if (!Layers.ShowIceFilm) { film = 0f; }
                if (!Layers.ShowShellThickness) { thick = 0f; }

                float lockT = Span(t, Style.LockAt, Style.LockAt + Style.LockFor);

                // THE FROZEN IDLE: one pale band drifting through the interior, every few
                // seconds, on its own schedule per cube. No breathing, no wobble.
                float glint = -0.3f;
                if (done && Layers.ShowIdleGlint)
                {
                    if (clock >= cell.NextIdle)
                    {
                        cell.IdleAt = clock;
                        cell.NextIdle = clock + Random.Range(Style.IdleMin, Style.IdleMax);
                    }
                    float since = clock - cell.IdleAt;
                    if (cell.IdleAt > 0f && since < Style.IdleSweep)
                    {
                        glint = Mathf.Lerp(-0.15f, 1.15f, since / Style.IdleSweep);
                    }
                }

                ApplyMaterial(r);
                r.GetPropertyBlock(block);
                block.SetFloat(SlowId, slow);
                block.SetFloat(SeedsId, seeds);
                block.SetFloat(FingersId, fingers);
                block.SetFloat(FilmId, film);
                block.SetFloat(ThickId, thick);
                block.SetFloat(GlintId, glint);
                block.SetFloat(DebugId, Layers.ShowField);
                // A CORNER GROWS TWO FIELDS. Both go in, the shader takes MIN of their arrivals,
                // and they meet in the middle - which nothing here had to arrange.
                int wallA, wallB;
                Walls(cell.Sides, out wallA, out wallB);
                block.SetVector(TileAId, Tile(wallA, cell.VariantA, true));
                block.SetVector(TileBId, Tile(wallB, cell.VariantB, wallB >= 0));
                r.SetPropertyBlock(block);

                // THE CRYSTAL SEATING, written ABSOLUTELY off the size the BOARD gave this
                // cube - never read back off the renderer.
                //
                // Reading it back is what the first version did, and it multiplied the already
                // scaled value by the curve again on every frame of the window: seven frames at
                // 60fps left the cube 6.6% oversized, and the lab at 0.25x gave it twenty-eight
                // frames and 29%. It then STAYED there until something repainted the board, which
                // is why the ice read as a block overflowing its cell while every other cube sat
                // inside one. A per-frame scale must always be written from a base, never from
                // itself.
                bool seating = Layers.ShowLockResponse && lockT > 0f && lockT < 1f;
                if (!seating)
                {
                    // Outside the window the board is the authority again - including the blind
                    // round's own growth, which lerps a cube's size with the light on it.
                    cell.BaseScale = Mathf.Abs(r.transform.localScale.x);
                }
                else if (cell.BaseScale > 0f)
                {
                    float k = LockCurve(lockT);
                    float side = cell.BaseScale * k;
                    r.transform.localScale = new Vector3(side, side, r.transform.localScale.z);
                }
            }
        }

        /// <summary>
        /// The walls as INDICES the field understands: 0 left, 1 right, 2 down, 3 up, -1 none.
        ///
        /// At most two, because at most two matter: three walls is a one-cell corridor and a
        /// third crystal field would only cancel the other two out into a centre-out freeze,
        /// which is the exact reading spec 105 forbids.
        /// </summary>
        private static void Walls(BoardSides sides, out int a, out int b)
        {
            a = -1;
            b = -1;
            if ((sides & BoardSides.Left) != 0) { a = 0; }
            if ((sides & BoardSides.Right) != 0) { if (a < 0) { a = 1; } else if (b < 0) { b = 1; } }
            if ((sides & BoardSides.Down) != 0) { if (a < 0) { a = 2; } else if (b < 0) { b = 2; } }
            if ((sides & BoardSides.Up) != 0) { if (a < 0) { a = 3; } else if (b < 0) { b = 3; } }
            if (a < 0)
            {
                // Not against anything. Cannot happen for a cube Buzluk froze, but ice can reach
                // the board another way one day, and a field growing off nothing is better than
                // a divide by a missing wall.
                a = 2;
            }
        }

        /// <summary>One tile of the baked atlas: u offset, u scale, wall index, in-use flag.
        /// </summary>
        private static Vector4 Tile(int wall, int variant, bool used)
        {
            float scale = 1f / IceGrowth.Variants;
            int v = Mathf.Clamp(variant, 0, IceGrowth.Variants - 1);
            return new Vector4(v * scale, scale, Mathf.Max(wall, 0), used ? 1f : 0f);
        }

        /// <summary>The shared cryo material carries the look; the layer switches and the two
        /// tone colours are pushed onto it rather than onto every renderer.</summary>
        private void ApplyMaterial(SpriteRenderer r)
        {
            Material ice = ViewUtil.IceMaterial;
            if (ice == null)
            {
                return;
            }
            if (r.sharedMaterial != ice)
            {
                r.sharedMaterial = ice;
            }
            if (ice.GetTexture("_Growth") == null)
            {
                ice.SetTexture("_Growth", IceGrowth.Field);
            }
            ice.SetColor("_ColdTint", Style.Cold);
            ice.SetColor("_FrostColour", Style.Frost);
            ice.SetColor("_CloudColour", Style.Cloud);
            ice.SetColor("_VeinColour", Style.Vein);
            ice.SetFloat("_InnerWater", Layers.ShowTrappedWater ? Style.InnerWater : 0f);
            ice.SetFloat("_InnerFade", Style.InnerFade);
            ice.SetFloat("_IceBody", Style.IceBody);
            ice.SetFloat("_IceBodyVar", Layers.ShowClouding ? Style.IceBodyVariation : 0f);
            ice.SetFloat("_FingerAmount", Layers.ShowCrystalFingers ? Style.FingerAmount : 0f);
            ice.SetFloat("_RimAmount", Layers.ShowFinalRim ? Style.RimAmount : 0f);
            ice.SetFloat("_EdgeBand", IceGrowth.EdgeBand);
            ice.SetFloat("_SeedShare", IceGrowth.SeedShare);
            ice.SetFloat("_FilmBand", Style.FilmBand);
        }

        /// <summary>Puts a cell back to an ordinary renderer: the property block cleared and the
        /// scale honest again. A pooled cell renderer keeps whatever was last pushed into it, so
        /// without this a cube that landed where ice used to stand would wear a stale freeze.
        /// </summary>
        private void Clear(GridPos at)
        {
            if (owner == null)
            {
                return;
            }
            SpriteRenderer r = owner.CellRendererAt(at);
            if (r != null)
            {
                r.SetPropertyBlock(null);
            }
        }

        // ---- the glints ----------------------------------------------------------------------

        /// <summary>A few tiny ice sparks, and a HARD CAP: the richness here is per cube, not per
        /// screen, so a wall of ice must never become a particle storm.</summary>
        private void Throw(Cell cell, int count, bool atRim)
        {
            if (!Layers.ShowLockGlints || owner == null)
            {
                return;
            }
            Vector2 centre = owner.CellToWorld(cell.At);
            float size = owner.CellWorldSize;
            for (int i = 0; i < count && glints.Count < Style.GlintCap; i++)
            {
                Vector2 off;
                if (atRim)
                {
                    // On the wall side, because that is where the frost is.
                    off = WallOffset(cell.Sides) * 0.30f
                        + new Vector2(Random.Range(-0.22f, 0.22f), Random.Range(-0.22f, 0.22f));
                }
                else
                {
                    off = new Vector2(Random.Range(-0.30f, 0.30f), Random.Range(-0.30f, 0.30f));
                }
                // CLAMPED INSIDE THE CUBE. A glint is frost, and frost that lands in the gap
                // between two cells is the ice spilling out of its own square - the same mistake
                // the fire's warm-up halo made, and the reason that has a rule of its own now.
                float inside = 0.5f * Style.GlintInside;
                off.x = Mathf.Clamp(off.x, -inside, inside);
                off.y = Mathf.Clamp(off.y, -inside, inside);
                Vector2 at = centre + off * size;
                SpriteRenderer sprite = Rent();
                var g = new Glint
                {
                    Sprite = sprite,
                    At = at,
                    // A short drift, and small enough that the clamp above still holds by the
                    // time the glint dies.
                    Drift = new Vector2(Random.Range(-0.05f, 0.05f), Random.Range(0.01f, 0.07f))
                        * size,
                    Born = clock,
                    Size = size * Style.GlintSize * Random.Range(0.75f, 1.25f)
                };
                glints.Add(g);
            }
        }

        private void PaintGlints()
        {
            for (int i = glints.Count - 1; i >= 0; i--)
            {
                Glint g = glints[i];
                float k = (clock - g.Born) / Style.GlintLife;
                if (k >= 1f)
                {
                    Return(g.Sprite);
                    glints.RemoveAt(i);
                    continue;
                }
                // In fast and out slower, and it never grows: a glint that swells is a bubble.
                float on = k < 0.3f ? k / 0.3f : 1f - (k - 0.3f) / 0.7f;
                g.Sprite.transform.position = g.At + g.Drift * k;
                float s = g.Size * (0.7f + 0.3f * on);
                g.Sprite.transform.localScale = new Vector3(s, s, 1f);
                Color c = Style.Frost;
                c.a = 0.85f * on;
                g.Sprite.color = c;
            }
        }

        private static Vector2 WallOffset(BoardSides sides)
        {
            var v = new Vector2(
                ((sides & BoardSides.Right) != 0 ? 1f : 0f)
                    - ((sides & BoardSides.Left) != 0 ? 1f : 0f),
                ((sides & BoardSides.Up) != 0 ? 1f : 0f)
                    - ((sides & BoardSides.Down) != 0 ? 1f : 0f));
            return v;
        }

        private SpriteRenderer Rent()
        {
            SpriteRenderer r;
            if (spare.Count > 0)
            {
                r = spare[spare.Count - 1];
                spare.RemoveAt(spare.Count - 1);
                r.enabled = true;
                return r;
            }
            var go = new GameObject("IceGlint");
            go.transform.SetParent(transform, false);
            r = go.AddComponent<SpriteRenderer>();
            r.sprite = GlintSprite;
            r.sortingOrder = 62;
            return r;
        }

        private void Return(SpriteRenderer r)
        {
            if (r != null)
            {
                r.enabled = false;
                spare.Add(r);
            }
        }

        /// <summary>A four-point star, baked once. A round dot is a bubble and a square is a
        /// pixel; a glint on ice is a short cross with a bright middle.</summary>
        private static Sprite GlintSprite
        {
            get
            {
                if (glintSprite == null)
                {
                    const int n = 32;
                    var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    var px = new Color[n * n];
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            float u = (x + 0.5f) / n - 0.5f;
                            float v = (y + 0.5f) / n - 0.5f;
                            float au = Mathf.Abs(u);
                            float av = Mathf.Abs(v);
                            // Two tapered bars crossing, plus a soft core. The taper is what
                            // makes it a star rather than a plus sign.
                            float bar = Mathf.Max(
                                Mathf.Clamp01(1f - au / 0.5f) * Mathf.Clamp01(1f - av / 0.055f),
                                Mathf.Clamp01(1f - av / 0.5f) * Mathf.Clamp01(1f - au / 0.055f));
                            float core = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) / 0.13f);
                            float a = Mathf.Clamp01(bar * bar + core * core);
                            px[y * n + x] = new Color(1f, 1f, 1f, a);
                        }
                    }
                    tex.SetPixels(px);
                    tex.Apply();
                    glintSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f),
                        n);
                }
                return glintSprite;
            }
        }

        // ---- curves --------------------------------------------------------------------------

        private static float Span(float t, float from, float to)
        {
            return Mathf.Clamp01((t - from) / Mathf.Max(to - from, 0.0001f));
        }

        private static float Smooth(float k)
        {
            return k * k * (3f - 2f * k);
        }

        /// <summary>1 -> swell -> peak -> dip -> 1. Soft on every leg: a crystal seating, and
        /// the swell on the way is water expanding as it freezes - not a cartoon bounce.
        /// </summary>
        private static float LockCurve(float k)
        {
            if (k < 0.30f)
            {
                return Mathf.Lerp(1f, Style.LockSwell, Smooth(k / 0.30f));
            }
            if (k < 0.52f)
            {
                return Mathf.Lerp(Style.LockSwell, Style.LockPeak, Smooth((k - 0.30f) / 0.22f));
            }
            if (k < 0.74f)
            {
                return Mathf.Lerp(Style.LockPeak, Style.LockDip, Smooth((k - 0.52f) / 0.22f));
            }
            return Mathf.Lerp(Style.LockDip, 1f, Smooth((k - 0.74f) / 0.26f));
        }

        private static void Announce(string what)
        {
            if (Sounded != null)
            {
                Sounded(what);
            }
        }
    }
}
