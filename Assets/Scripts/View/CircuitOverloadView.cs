// PURPOSE: The circuit going up. When the player fills the last cell of "Devre" the cable is
// overloaded and blows, and this is what that looks like: the drawn overload sheet TILED along
// the cable, end to end, with each tile a little further behind the one before it.
//
// TILED, NOT ONE ANIMATION. The art is a plain horizontal bar - fifteen frames of a charge that
// swells, whitens, goes blue and bursts - and it is laid along the route like paving. That is the
// whole reason it works: the circuit is a different shape every round, and a single drawing of
// "a circuit exploding" would have to be redrawn for every shape it can take. A bar that repeats
// fits any route, straight or winding, long or short, with no art per case.
//
// IT DOES NOT GO OFF AT ONCE - BUT ONLY JUST. Every tile's clock is offset by how far along the
// cable it sits, so the failure runs DOWN the cable rather than happening to all of it at the same
// instant. The lead is deliberately short, three frames between the two ends: much more than that
// and the cable stops reading as one thing failing and starts reading as two separate events at
// two separate ends.
//
// IT IS SLOWER THAN A LINE CLEAR, on purpose. A cleared row is a beat; this is the payoff for
// three turns of work on a whole route, so it runs at half a line burst's frame rate.
//
// TILES MEET END TO END, ALWAYS. A tile is drawn to the CHORD between its two ends, not to the
// arc of cable between them, so the far end of one lands exactly on the near end of the next
// whatever the route does. And no tile is allowed to swallow a whole bend: the walk cuts on
// turning as well as on distance, so a corner gets two or three short tiles that go round it
// rather than one long one lying across it.
//
// THE DRAWN SHEET IS THE EVENT; everything else is evidence. CircuitAshFx and CircuitScorchView
// hang off this one and are driven from ITS timeline - which frame the lead tile is on decides how
// much ash is coming off, whether the arcs are snapping, and when the burn mark is laid. They are
// support, never competition: no layer here paints along the whole path, and none of them is
// allowed to be the brightest thing on screen.
//
// FIFTEEN FRAMES, NOT SIXTEEN. The sheet arrived as a 4x4 with the last cell empty; read as
// sixteen it plays a blank frame at the end of every tile, which is a visible blink on every one
// of thirty tiles at once.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The overload running down a circuit. Fire and forget: Play, then it runs out.</summary>
    public sealed class CircuitOverloadView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            /// <summary>Frames per second. HALF a line burst's 22: this is the end of a three
            /// turn objective and it is allowed to be watched.</summary>
            public static float Fps = 11f;

            /// <summary>How many FRAMES apart the two ends of the cable are. In frames rather
            /// than seconds on purpose: what is being set is the gap between two PICTURES - at 3,
            /// one end is on frame 11 while the far end is still on frame 8. Set in seconds it
            /// would quietly change every time the frame rate was retuned.
            ///
            /// ONE IS THE FLOOR for this still being a travel at all: at 1 the cable shows exactly
            /// two neighbouring frames at any instant, with the boundary between them sweeping
            /// along it. Below that most of the cable is showing the identical picture and the
            /// direction stops existing - it just all goes off at once.</summary>
            public static float SpreadFrames = 1f;

            /// <summary>The spread as a delay, which is how the tiles actually carry it.</summary>
            public static float TravelSeconds
            {
                get { return SpreadFrames / Mathf.Max(Fps, 0.001f); }
            }

            /// <summary>How long one tile is along the cable, in CELLS. Shorter follows a bend
            /// more closely and costs more renderers; longer starts to cut corners.</summary>
            public static float TileLength = 0.55f;

            /// <summary>How much each tile overruns the next, as a fraction. The faint first and
            /// last frames only cover about three quarters of their cell, so without a little
            /// overlap the chain shows gaps while it is charging. Measured off the sheet: the
            /// frames that carry the animation cover 91-98% of their cell, so a fifth is enough
            /// to close the seam. It is NOT the place to get size from - every bit of overlap is
            /// also overshoot past a corner - so the thickness carries that instead.</summary>
            public static float TileOverlap = 0.22f;

            /// <summary>How much fatter than its natural proportion a tile is drawn. The art is
            /// 1.70:1, so 1 keeps it undistorted.</summary>
            public static float Thickness = 1.62f;

            /// <summary>How big the whole thing is drawn - length and thickness together, so the
            /// art keeps its proportion. This is the size knob. TileOverlap is NOT: that one has
            /// its own job, closing the seam between two tiles, and leaning on it for size stops
            /// being overlap and starts being overshoot past the corners.</summary>
            public static float Scale = 1.18f;

            /// <summary>Brightness of the whole thing.</summary>
            public static float Intensity = 1f;

            /// <summary>How far a tile may TURN before it is cut short, in degrees. This is what
            /// keeps corners honest: a bend is about 0.4 cells of arc, so without it one tile
            /// swallows a whole 90 degree turn and gets drawn as a single diagonal stick lying
            /// across the corner. Cutting on the turn hands the bend two or three short tiles
            /// that follow it round instead.</summary>
            public static float MaxTurnDegrees = 30f;

            /// <summary>The frames the sheet's own charge passes through, 0 based, measured off
            /// the art rather than guessed: 0-5 is an orange fuse, 6-9 the core going blue, 10 the
            /// burst (by far the whitest frame on the sheet), 11-12 the blue dropping away and
            /// 13-14 the last of it. Every procedural layer hangs off these.</summary>
            public const int BlueFrame = 6;

            public const int RuptureFrame = 10;

            public const int DyingFrame = 13;

            /// <summary>How much a tile's THICKNESS may vary, so thirty of them in a row are not
            /// visibly the same drawing repeated. Length is deliberately not jittered - the
            /// tiles have to meet exactly, and a random length is a random gap.</summary>
            public static float TileJitter = 0.10f;
        }

        /// <summary>Above everything on the board - the cubes included, because they are what is
        /// being destroyed.</summary>
        private const int SortingOrder = 20;

        private const int FrameCount = 15;

        private const int Columns = 5;

        private const int Rows = 3;

        private const string SheetPath = "Art/Fx/circuit_overload_sheet";

        /// <summary>The art's own proportion, so a tile is not stretched.</summary>
        private const float FrameAspect = 313f / 184f;

        // =================================================================== the sheet

        private static Sprite[] frames;

        private static bool loaded;

        private static Sprite[] Frames()
        {
            if (loaded)
            {
                return frames;
            }
            loaded = true;
            var sheet = Resources.Load<Texture2D>(SheetPath);
            if (sheet == null)
            {
                Debug.LogWarning("[block_bonk] Circuit overload sheet missing: Resources/"
                    + SheetPath);
                return null;
            }
            int cw = sheet.width / Columns;
            int ch = sheet.height / Rows;
            var built = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                int col = i % Columns;
                int row = i / Columns;
                // Texture y counts from the BOTTOM while the sheet reads top row first.
                int rectY = (Rows - 1 - row) * ch;
                built[i] = Sprite.Create(sheet, new Rect(col * cw, rectY, cw, ch),
                    new Vector2(0.5f, 0.5f), cw);   // PPU = cell width: 1 unit wide at scale 1
            }
            frames = built;
            return built;
        }

        // =================================================================== one tile

        private struct Tile
        {
            public SpriteRenderer Renderer;
            public float Delay;
            public float Length;
            public float Height;
        }

        private Tile[] tiles = new Tile[0];

        /// <summary>Where each tile starts and ends along the cable, in world units.</summary>
        private readonly List<float> cuts = new List<float>();

        // ------------------------------------------------------------- the supporting layers

        private CircuitAshFx ashFx;

        private CircuitScorchView scorch;

        private IReadOnlyList<Vector2> path;

        private IReadOnlyList<float> pathAt;

        private float pathLength;

        private float pathCell = 1f;

        /// <summary>Fractional particles carried between frames, so a rate below one per frame
        /// still emits at the right average instead of rounding away to nothing.</summary>
        private float ashDue;

        private float sparkDue;

        private float hazeDue;

        private bool scorchLaid;

        private bool ruptured;

        private bool embersLeft;

        private int tileCount;

        private float clock;

        private bool running;

        // =================================================================== driving it

        /// <summary>
        /// Sets the overload off along a cable. <paramref name="route"/> is the cable's own centre
        /// line and <paramref name="routeAt"/> how far along each of its points is - the same
        /// arrays the cable is drawn from, so the tiles sit exactly on it.
        /// </summary>
        public void Play(IReadOnlyList<Vector2> route, IReadOnlyList<float> routeAt,
            float routeLength, float cell)
        {
            Stop();
            Sprite[] art = Frames();
            if (art == null || route == null || route.Count < 2 || routeLength <= 0f
                || cell <= 0f)
            {
                return;
            }

            path = route;
            pathAt = routeAt;
            pathLength = routeLength;
            pathCell = cell;
            ashDue = 0f;
            sparkDue = 0f;
            hazeDue = 0f;
            scorchLaid = false;
            ruptured = false;
            embersLeft = false;
            Ash().SetRoute(route, routeAt, routeLength, cell);

            float step = Mathf.Max(Style.TileLength * cell, 0.001f);
            BuildCuts(route, routeAt, routeLength, step);
            int want = cuts.Count - 1;
            Grow(want);
            tileCount = want;

            // One thickness for the whole cable, taken from the NOMINAL tile rather than each
            // tile's own length. A corner's tiles are short by design, and sizing their height
            // off their length would pinch the beam to a third of its width at every bend.
            float baseHeight = step * (1f + Style.TileOverlap) / FrameAspect * Style.Thickness
                * Style.Scale;

            for (int i = 0; i < want; i++)
            {
                // The tile's own stretch of cable, and the direction it actually runs in there -
                // taken from the route, so a tile on a bend is turned with the bend.
                float from = cuts[i];
                float to = cuts[i + 1];
                Vector2 a = PointAt(route, routeAt, from);
                Vector2 b = PointAt(route, routeAt, to);
                Vector2 mid = (a + b) * 0.5f;
                Vector2 delta = b - a;
                float chord = delta.magnitude;
                Vector2 dir = chord > 0.000001f ? delta / chord : Vector2.right;

                // LENGTH COMES FROM THE CHORD, never from the arc between the two ends. They are
                // the same on a straight but not through a bend, where the arc is longer - and a
                // bar drawn to the arc while sitting on the chord hangs out past both of its own
                // ends, into open space, at an angle neither neighbour shares. That is what made
                // corners look like loose diagonal sticks poking out of the tile before them.
                // Drawn to the chord, a tile ends exactly where the next one begins, on any shape.
                float length = chord * (1f + Style.TileOverlap) * Style.Scale;
                float height = baseHeight
                    * (1f + Random.Range(-Style.TileJitter, Style.TileJitter));

                tiles[i].Delay = (from + to) * 0.5f / routeLength * Style.TravelSeconds;
                tiles[i].Length = length;
                tiles[i].Height = height;
                SpriteRenderer r = tiles[i].Renderer;
                r.transform.localPosition = new Vector3(mid.x, mid.y, 0f);
                r.transform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                // The sprite is one unit wide at scale 1 (PPU is the cell width), so x is the
                // tile's length outright and y is its height through the art's own aspect.
                r.transform.localScale = new Vector3(length, height * FrameAspect, 1f);
                r.sprite = art[0];
                r.enabled = false;
            }
            for (int i = want; i < tiles.Length; i++)
            {
                tiles[i].Renderer.enabled = false;
            }

            clock = 0f;
            running = true;
        }

        /// <summary>How long the whole thing runs, so a caller can wait it out.</summary>
        public static float Duration
        {
            get { return FrameCount / Mathf.Max(Style.Fps, 0.001f) + Style.TravelSeconds; }
        }

        /// <summary>Everything including the mark healing back off the board. NOT what a caller
        /// should wait on - the burn mark is meant to fade while play carries on - but the number
        /// to use when something needs the board left alone until there is no trace.</summary>
        public static float TotalDuration
        {
            get
            {
                return Style.RuptureFrame / Mathf.Max(Style.Fps, 0.001f) + Style.TravelSeconds
                    + CircuitScorchView.Duration;
            }
        }

        /// <summary>When the sheet's blue core actually BURSTS, in seconds from Play. Anything
        /// that should happen "as the circuit goes off" happens here - measured off the art, not
        /// picked, so retiming the animation moves it too.</summary>
        public static float RuptureTime
        {
            get { return Style.RuptureFrame / Mathf.Max(Style.Fps, 0.001f); }
        }

        public bool Running
        {
            get { return running; }
        }

        public void Stop()
        {
            running = false;
            if (ashFx != null)
            {
                ashFx.StopAll();
            }
            if (scorch != null)
            {
                scorch.Clear();
            }
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i].Renderer != null)
                {
                    tiles[i].Renderer.enabled = false;
                }
            }
        }

        private CircuitAshFx Ash()
        {
            if (ashFx == null)
            {
                var go = new GameObject("Ash");
                go.transform.SetParent(transform, false);
                ashFx = go.AddComponent<CircuitAshFx>();
            }
            return ashFx;
        }

        private CircuitScorchView Scorch()
        {
            if (scorch == null)
            {
                var go = new GameObject("Scorch");
                go.transform.SetParent(transform, false);
                scorch = go.AddComponent<CircuitScorchView>();
            }
            return scorch;
        }

        /// <summary>
        /// Runs the supporting layers off the LEAD tile's frame. One clock for all of them rather
        /// than a schedule each: they are reactions to the drawn animation, so if it is retimed
        /// they follow it without anything else being touched.
        /// </summary>
        private void DriveSupport(float dt)
        {
            if (pathLength <= 0f || pathCell <= 0f)
            {
                return;
            }
            int lead = Mathf.Clamp(Mathf.FloorToInt(clock * Style.Fps), 0, FrameCount - 1);
            float cells = pathLength / pathCell;
            CircuitAshFx fx = Ash();

            ashDue += AshRate(lead) * cells * dt;
            while (ashDue >= 1f)
            {
                fx.EmitAsh(1);
                ashDue -= 1f;
            }
            sparkDue += SparkRate(lead) * cells * dt;
            while (sparkDue >= 1f)
            {
                fx.EmitSparks(1);
                sparkDue -= 1f;
            }
            hazeDue += HazeRate(lead) * cells * dt;
            while (hazeDue >= 1f)
            {
                fx.EmitHaze(1);
                hazeDue -= 1f;
            }

            // The mark is laid at the burst, sweeping in the same order and at the same speed the
            // failure crossed the cable - it is that failure's own footprint, not a second event.
            if (!scorchLaid && clock >= Style.RuptureFrame / Mathf.Max(Style.Fps, 0.001f))
            {
                scorchLaid = true;
                Scorch().Begin(path, pathAt, pathLength, pathCell, Style.TravelSeconds);
            }
            // The rupture itself, once: crust comes off and the fresh mark keeps a couple of
            // hot points in it. Both are gone long before the mark is - they belong to the break,
            // not to the aftermath.
            if (!ruptured && clock >= Style.RuptureFrame / Mathf.Max(Style.Fps, 0.001f))
            {
                ruptured = true;
                fx.EmitFlecks(Random.Range(CircuitAshFx.Style.FleckCountMin,
                    CircuitAshFx.Style.FleckCountMax + 1));
                fx.EmitEmbers(Random.Range(CircuitAshFx.Style.HotSpotCountMin,
                    CircuitAshFx.Style.HotSpotCountMax + 1));
            }
            // Embers once the energy is spent, not during it: they are what is LEFT.
            if (!embersLeft && clock >= Style.DyingFrame / Mathf.Max(Style.Fps, 0.001f))
            {
                embersLeft = true;
                fx.EmitEmbers(Random.Range(CircuitAshFx.Style.EmberCountMin,
                    CircuitAshFx.Style.EmberCountMax + 1));
            }
        }

        private static float AshRate(int frame)
        {
            if (frame < Style.BlueFrame) { return CircuitAshFx.Style.AshRateHeatUp; }
            if (frame < Style.RuptureFrame) { return CircuitAshFx.Style.AshRateBlueHot; }
            if (frame == Style.RuptureFrame) { return CircuitAshFx.Style.AshRateRupture; }
            if (frame < Style.DyingFrame) { return CircuitAshFx.Style.AshRateFade; }
            return CircuitAshFx.Style.AshRateDying;
        }

        /// <summary>Arcs only while the core is actually blue. An orange fuse does not snap.</summary>
        private static float SparkRate(int frame)
        {
            if (frame == Style.RuptureFrame) { return CircuitAshFx.Style.SparkRateRupture; }
            if (frame >= Style.BlueFrame && frame < Style.RuptureFrame)
            {
                return CircuitAshFx.Style.SparkRateBlueHot;
            }
            return 0f;
        }

        /// <summary>The haze runs from the FIRST frame - the cable is hot long before it is
        /// bright, and that is the only thing saying so during the orange fuse.</summary>
        private static float HazeRate(int frame)
        {
            if (frame == Style.RuptureFrame) { return CircuitAshFx.Style.HazeRateRupture; }
            if (frame >= Style.BlueFrame && frame < Style.RuptureFrame)
            {
                return CircuitAshFx.Style.HazeRateBlueHot;
            }
            if (frame < Style.BlueFrame) { return CircuitAshFx.Style.HazeRatePreheat; }
            return 0f;
        }

        /// <summary>
        /// Chooses where one tile ends and the next begins. Walks the cable and cuts on whichever
        /// comes first: a full tile's worth of distance, or <see cref="Style.MaxTurnDegrees"/> of
        /// turning. The second test is the whole point - cutting on distance alone lets a single
        /// tile straddle an entire corner.
        /// </summary>
        private void BuildCuts(IReadOnlyList<Vector2> route, IReadOnlyList<float> routeAt,
            float routeLength, float step)
        {
            cuts.Clear();
            cuts.Add(0f);
            int start = 0;
            float heading = HeadingAt(route, 0);
            for (int i = 1; i < route.Count; i++)
            {
                float here = HeadingAt(route, i);
                if (routeAt[i] - routeAt[start] >= step
                    || Mathf.Abs(Mathf.DeltaAngle(heading, here)) >= Style.MaxTurnDegrees)
                {
                    cuts.Add(routeAt[i]);
                    start = i;
                    heading = here;
                }
            }
            // A leftover sliver at the far end would draw as a stub; give it to the tile before.
            if (cuts.Count > 1 && routeLength - cuts[cuts.Count - 1] < step * 0.35f)
            {
                cuts[cuts.Count - 1] = routeLength;
            }
            else
            {
                cuts.Add(routeLength);
            }
        }

        private static float HeadingAt(IReadOnlyList<Vector2> route, int i)
        {
            int j = Mathf.Min(i + 1, route.Count - 1);
            Vector2 d = route[j] - route[Mathf.Max(j - 1, 0)];
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        private static Vector2 PointAt(IReadOnlyList<Vector2> route, IReadOnlyList<float> at,
            float distance)
        {
            for (int i = 1; i < route.Count; i++)
            {
                if (distance <= at[i])
                {
                    float span = at[i] - at[i - 1];
                    float k = span > 0.0001f ? (distance - at[i - 1]) / span : 0f;
                    return Vector2.Lerp(route[i - 1], route[i], k);
                }
            }
            return route[route.Count - 1];
        }

        private void Grow(int count)
        {
            if (tiles.Length >= count)
            {
                return;
            }
            var next = new Tile[count];
            System.Array.Copy(tiles, next, tiles.Length);
            for (int i = tiles.Length; i < count; i++)
            {
                var go = new GameObject("Tile" + i);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sortingOrder = SortingOrder;
                r.enabled = false;
                next[i] = new Tile { Renderer = r };
            }
            tiles = next;
        }

        private void Update()
        {
            if (!running)
            {
                return;
            }
            Sprite[] art = Frames();
            if (art == null)
            {
                running = false;
                return;
            }
            float dt = Time.deltaTime;
            clock += dt;
            DriveSupport(dt);
            bool any = false;
            for (int i = 0; i < tileCount; i++)
            {
                float t = clock - tiles[i].Delay;
                if (t < 0f)
                {
                    any = true;
                    tiles[i].Renderer.enabled = false;
                    continue;
                }
                int frame = Mathf.FloorToInt(t * Style.Fps);
                if (frame >= FrameCount)
                {
                    tiles[i].Renderer.enabled = false;
                    continue;
                }
                any = true;
                SpriteRenderer r = tiles[i].Renderer;
                r.sprite = art[frame];
                r.color = new Color(Style.Intensity, Style.Intensity, Style.Intensity, 1f);
                r.enabled = true;
            }
            if (!any && ashFx != null)
            {
                // Energy gone, mark left: a few last places let go of a flake.
                ashFx.FinalAshRelease();
            }
            running = any;
        }
    }
}
