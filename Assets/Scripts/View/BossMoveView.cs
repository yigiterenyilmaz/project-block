// PURPOSE: How a cube a MOVING board carries to another cell - and that survives there - gets
// there: "Yürüyen merdiven" (STEP CARRY) and "Merkezkaç kuvveti" (RADIAL PUSH). One motion system,
// two characters. A cube that does NOT survive the move is not drawn here: MomentumPeelView tears
// it off, starting it on the SAME launch (Launch), so a row's survivors and casualties set off as one.
//
// Nothing here decides where a cube goes. Core reports every move (TurnReport.BoardMoves: from,
// to, the step, which board). The board already stands in its new state; the board view blanks the
// cells the cubes are heading for (BoardView.HoldCells) while this draws them on their way, and
// gets the cells back the moment they have landed - no cube is ever shown twice, or respawned.
//
//   STEP CARRY   the board rides up one step as ONE mechanism: every survivor on the same curve,
//                no stagger. A whisper of load (a hair down and shorter), a short ease-in, a steady
//                carry, a soft ease-out a hair past the new row, and home. A pixel or two of lift,
//                a contact shadow that softens and locks again. Straight up: no drift, no wobble.
//                The fresh bottom row's floor comes up out of the dark.
//   RADIAL PUSH  each cube is pushed along its OWN outward step: a pixel of preload against it, a
//                quick launch, a strong middle, a longer drag to a stop a pixel past its cell and
//                back. A few percent of stretch along the push early on, a lighter leading edge and
//                a darker trailing one, a shadow that lags a touch behind. Straight, even on a
//                diagonal. A few milliseconds between cubes at most - the force acts on all at once.
//
// No particles, trails, glow, flashes, rotation or shake. The quality is in the curves, the shadow,
// the settle and the exact landing.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Cubes a moving board carried to another cell and that survived, riding there.</summary>
    public sealed class BossMoveView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how a board's move READS. Sizes in CELLS, times in
        /// seconds. A profile's own setting of 0 falls back to the common one.</summary>
        public static class Style
        {
            // ---- common ----
            /// <summary>A profile's move when its own duration is 0.</summary>
            public static float MoveDuration = 0.25f;

            /// <summary>How far a cube lifts off the board mid-move (0.022: about 1.4 px on a
            /// 64-pixel cell). The push's lift, and the carry's when its own is 0.</summary>
            public static float LiftAmount = 0.022f;

            /// <summary>How much the contact shadow softens and fades as the cube lifts, 0..1.</summary>
            public static float ShadowLiftStrength = 0.45f;

            /// <summary>How firmly the shadow locks back under the cube as it lands, 0..1.</summary>
            public static float ShadowSettleStrength = 0.6f;

            /// <summary>The carry's stretch along its step at full pace, as a share of the cube.</summary>
            public static float ScaleResponse = 0.01f;

            /// <summary>A profile's settle when its own is 0.</summary>
            public static float SettleDuration = 0.05f;

            /// <summary>Seconds per cube for the carry. 0: every survivor exactly in step.</summary>
            public static float TimingVariation = 0f;

            // ---- Yürüyen merdiven: STEP CARRY ----
            public static float EscalatorMoveDuration = 0.24f;

            public static float EscalatorAnticipationDuration = 0.035f;

            /// <summary>The load: how much shorter along the step, and how far down, in cells.</summary>
            public static float EscalatorAnticipationCompression = 0.012f;

            public static float EscalatorLiftAmount = 0.02f;

            /// <summary>The share of the move spent picking up speed.</summary>
            public static float EscalatorEaseIn = 0.22f;

            /// <summary>The share spent slowing down.</summary>
            public static float EscalatorEaseOut = 0.32f;

            /// <summary>How far past the new row it rides before settling back, in cells (under a pixel).</summary>
            public static float EscalatorSettleAmount = 0.012f;

            public static float EscalatorSettleDuration = 0.05f;

            /// <summary>The fresh bottom row's floor coming up out of the dark.</summary>
            public static float EscalatorBottomRowRevealDuration = 0.15f;

            /// <summary>How much lighter the leading edge reads at full pace.</summary>
            public static float EscalatorEdgeLight = 0.05f;

            // ---- Merkezkaç kuvveti: RADIAL PUSH ----
            public static float CentrifugalMoveDuration = 0.21f;

            /// <summary>How far it loads back against the push first, in cells (0.016: 1 px).</summary>
            public static float CentrifugalPreloadAmount = 0.016f;

            public static float CentrifugalPreloadDuration = 0.035f;

            /// <summary>0..1: how short and hard the launch is.</summary>
            public static float CentrifugalAccelerationStrength = 0.8f;

            /// <summary>0..1: how much of the move is the drag to a stop.</summary>
            public static float CentrifugalDecelerationStrength = 0.7f;

            public static float CentrifugalDirectionalStretch = 0.025f;

            public static float CentrifugalCrossCompression = 0.012f;

            /// <summary>How far the shadow trails behind at full pace, in cells.</summary>
            public static float CentrifugalShadowLag = 0.03f;

            /// <summary>How far past its cell momentum carries it before it settles back, in cells.</summary>
            public static float CentrifugalLandingOvershoot = 0.018f;

            public static float CentrifugalLandingSettleDuration = 0.065f;

            /// <summary>Each pushed cube starts up to twice this late, from its cell: at most 18 ms.</summary>
            public static float CentrifugalTimingVariation = 0.009f;

            public static float CentrifugalEdgeLight = 0.09f;

            /// <summary>How much darker the trailing edge reads at full pace.</summary>
            public static float CentrifugalTrailingShade = 0.07f;
        }

        /// <summary>The lab's debug switches. The last three draw what the move is doing; they
        /// start off, and the lab puts every switch back to its default on close.</summary>
        public static class Layers
        {
            public static bool ShowContactShadowResponse = true;

            public static bool ShowScaleResponse = true;

            /// <summary>A line from the cell a cube left to the cell it is going to.</summary>
            public static bool ShowMovementVector;

            /// <summary>An outline on the cell each cube is going to.</summary>
            public static bool ShowDestinationCell;

            /// <summary>The travelling copies tinted, so they can be told from the board's cubes.</summary>
            public static bool ShowMovementProxy;

            public static void Defaults()
            {
                ShowContactShadowResponse = true;
                ShowScaleResponse = true;
                ShowMovementVector = false;
                ShowDestinationCell = false;
                ShowMovementProxy = false;
            }
        }

        // =================================================================== palette & layers

        private static readonly Color ShadowColour = new Color(0.02f, 0.025f, 0.04f);

        private static readonly Color RevealColour = new Color(0.03f, 0.035f, 0.05f);

        private static readonly Color DebugColour = new Color(0.45f, 0.85f, 1f);

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        private const int RevealOrder = 6;

        private const int ShadowOrder = 7;

        private const int BodyOrder = 8;

        private const int SurfaceOrder = 9;

        private const int DebugOrder = 10;

        /// <summary>The contact shadow under a moving cube, at rest.</summary>
        private const float ShadowAlpha = 0.24f;

        /// <summary>How long the shadow takes to go once the cube has settled - static cubes have none.</summary>
        private const float ShadowFade = 0.06f;

        /// <summary>How dark the fresh bottom row starts.</summary>
        private const float RevealAlpha = 0.38f;

        // =================================================================== the motion

        /// <summary>One board's motion character, resolved from Style.</summary>
        private struct Profile
        {
            /// <summary>The load (carry) or preload (push) before the move, seconds.</summary>
            public float Lead;
            /// <summary>How far back against the step it loads, cells.</summary>
            public float LeadAmount;
            /// <summary>How much shorter along the step it gets while it loads.</summary>
            public float Squash;
            public float Move;
            public float EaseIn;
            public float EaseOut;
            public float Overshoot;
            public float Settle;
            public float Lift;
            public float Stretch;
            public float Cross;
            /// <summary>Stretch only through the first half of the move: the push, not the carry.</summary>
            public bool StretchEarly;
            public float ShadowLag;
            public float EdgeLight;
            public float TrailShade;
            public float Variation;
        }

        private static Profile ProfileFor(BoardMotionSource source)
        {
            var p = new Profile();
            if (source == BoardMotionSource.Centrifuge)
            {
                p.Lead = Style.CentrifugalPreloadDuration;
                p.LeadAmount = Style.CentrifugalPreloadAmount;
                p.Move = Style.CentrifugalMoveDuration > 0f ? Style.CentrifugalMoveDuration : Style.MoveDuration;
                p.EaseIn = Mathf.Lerp(0.35f, 0.08f, Mathf.Clamp01(Style.CentrifugalAccelerationStrength));
                p.EaseOut = Mathf.Lerp(0.25f, 0.55f, Mathf.Clamp01(Style.CentrifugalDecelerationStrength));
                p.Overshoot = Style.CentrifugalLandingOvershoot;
                p.Settle = Style.CentrifugalLandingSettleDuration > 0f
                    ? Style.CentrifugalLandingSettleDuration : Style.SettleDuration;
                p.Lift = Style.LiftAmount;
                p.Stretch = Style.CentrifugalDirectionalStretch;
                p.Cross = Style.CentrifugalCrossCompression;
                p.StretchEarly = true;
                p.ShadowLag = Style.CentrifugalShadowLag;
                p.EdgeLight = Style.CentrifugalEdgeLight;
                p.TrailShade = Style.CentrifugalTrailingShade;
                p.Variation = Style.CentrifugalTimingVariation;
            }
            else
            {
                p.Lead = Style.EscalatorAnticipationDuration;
                p.LeadAmount = Style.EscalatorAnticipationCompression;
                p.Squash = Style.EscalatorAnticipationCompression;
                p.Move = Style.EscalatorMoveDuration > 0f ? Style.EscalatorMoveDuration : Style.MoveDuration;
                p.EaseIn = Style.EscalatorEaseIn;
                p.EaseOut = Style.EscalatorEaseOut;
                p.Overshoot = Style.EscalatorSettleAmount;
                p.Settle = Style.EscalatorSettleDuration > 0f ? Style.EscalatorSettleDuration : Style.SettleDuration;
                p.Lift = Style.EscalatorLiftAmount > 0f ? Style.EscalatorLiftAmount : Style.LiftAmount;
                p.Stretch = Style.ScaleResponse;
                // Straight up and nothing else: no lag, no squeeze across, no drift.
                p.Cross = 0f;
                p.ShadowLag = 0f;
                p.EdgeLight = Style.EscalatorEdgeLight;
                p.TrailShade = 0f;
                p.Variation = Style.TimingVariation;
            }
            p.EaseIn = Mathf.Clamp(p.EaseIn, 0.01f, 0.9f);
            p.EaseOut = Mathf.Clamp(p.EaseOut, 0.01f, 0.98f - p.EaseIn);
            p.Move = Mathf.Max(p.Move, 0.02f);
            return p;
        }

        /// <summary>The top speed of a move of unit length, per unit of its duration.</summary>
        private static float Vmax(Profile p)
        {
            return 1f / (1f - 0.5f * p.EaseIn - 0.5f * p.EaseOut);
        }

        /// <summary>The share of the way a move has come at u (0..1 of it): a smooth ramp UP in
        /// speed over easeIn, a steady middle, a smooth ramp down over easeOut. The ramps are in
        /// VELOCITY, so the travel has no kink anywhere. <paramref name="rate"/> is its speed per
        /// unit of u.</summary>
        private static float Travel(float u, float easeIn, float easeOut, out float rate)
        {
            float vmax = 1f / (1f - 0.5f * easeIn - 0.5f * easeOut);
            u = Mathf.Clamp01(u);
            if (u < easeIn)
            {
                float x = u / easeIn;
                rate = vmax * x * x * (3f - 2f * x);
                return vmax * easeIn * (x * x * x - 0.5f * x * x * x * x);
            }
            float cruiseEnd = 1f - easeOut;
            if (u <= cruiseEnd)
            {
                rate = vmax;
                return vmax * (0.5f * easeIn + (u - easeIn));
            }
            float y = (u - cruiseEnd) / easeOut;
            rate = vmax * (1f - y * y * (3f - 2f * y));
            return vmax * (0.5f * easeIn + (cruiseEnd - easeIn) + easeOut * (y - (y * y * y - 0.5f * y * y * y * y)));
        }

        /// <summary>The load or preload: a small dip back against the step that has let go again
        /// by twice its duration - by then the move itself has begun.</summary>
        private static float LeadOffset(Profile p, float t)
        {
            if (p.Lead <= 0f || t <= 0f || t >= 2f * p.Lead)
            {
                return 0f;
            }
            return -p.LeadAmount * Mathf.Sin(Mathf.PI * t / (2f * p.Lead));
        }

        /// <summary>How far along its step a SURVIVING cube is at t, in cells (1 = its new cell): the
        /// load, the move carried a hair past, the settle home. <paramref name="speed"/> in cells a
        /// second.</summary>
        private static float Along(Profile p, float t, out float speed)
        {
            float lead = LeadOffset(p, t);
            float u = (t - p.Lead) / p.Move;
            if (u <= 0f)
            {
                speed = 0f;
                return lead;
            }
            if (u >= 1f)
            {
                speed = 0f;
                float k = Smooth((t - p.Lead - p.Move) / Mathf.Max(p.Settle, 0.0001f));
                return 1f + p.Overshoot * (1f - k) + lead;
            }
            float rate;
            float travel = Travel(u, p.EaseIn, p.EaseOut, out rate);
            speed = rate * (1f + p.Overshoot) / p.Move;
            return travel * (1f + p.Overshoot) + lead;
        }

        /// <summary>
        /// The SAME move for a cube that will not land - MomentumPeelView's lead-in: the same load or
        /// preload and the same launch, then the cruise held instead of the ease-out. Cells along the
        /// step at t, and the speed in cells a second.
        /// </summary>
        public static float Launch(BoardMotionSource source, float t, out float speed)
        {
            Profile p = ProfileFor(source);
            float lead = LeadOffset(p, t);
            float u = (t - p.Lead) / p.Move;
            float vmax = Vmax(p);
            if (u <= 0f)
            {
                speed = 0f;
                return lead;
            }
            if (u < p.EaseIn)
            {
                float x = u / p.EaseIn;
                speed = vmax * x * x * (3f - 2f * x) / p.Move;
                return vmax * p.EaseIn * (x * x * x - 0.5f * x * x * x * x) + lead;
            }
            speed = vmax / p.Move;
            return vmax * (0.5f * p.EaseIn + (u - p.EaseIn)) + lead;
        }

        /// <summary>When a launched cube that will not land has come far enough to start coming
        /// apart: a third of a cell into its step.</summary>
        public static float LaunchRelease(BoardMotionSource source)
        {
            Profile p = ProfileFor(source);
            float speed;
            for (float t = 0f; t < p.Lead + p.Move; t += 0.002f)
            {
                if (Launch(source, t, out speed) >= 0.3f)
                {
                    return t;
                }
            }
            return p.Lead + p.Move;
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // =================================================================== state

        private struct Dice
        {
            private uint state;

            public Dice(uint seed)
            {
                state = seed == 0u ? 0x9E3779B9u : seed;
            }

            public float Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state >> 8) * (1f / 16777216f);
            }

            public float Range(float a, float b)
            {
                return a + (b - a) * Next();
            }
        }

        private static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        private sealed class Mover
        {
            public Vector2 From;
            public Vector2 To;
            public Vector2 Dir;
            public float Start;
            public Profile P;
            public ClusterBurstView.Look Look;
            public SpriteRenderer Shadow;
            public SpriteRenderer Body;
            public SpriteRenderer Light;
            public SpriteRenderer Shade;
            public SpriteRenderer Line;
            public SpriteRenderer Marker;
        }

        private sealed class Batch
        {
            public BoardView Board;
            public readonly List<Mover> Movers = new List<Mover>();
            /// <summary>The cells the board blanks until the cubes have landed in them.</summary>
            public readonly List<GridPos> Held = new List<GridPos>();
            public float Clock;
            public float End;
            public float Cell;
            public float CubeSize;
            public SpriteRenderer Reveal;
            public Rect RevealRect;
        }

        private readonly List<Batch> batches = new List<Batch>();

        private readonly Stack<SpriteRenderer> spareRenderers = new Stack<SpriteRenderer>();

        private Material plainMaterial;

        // =================================================================== driving it

        /// <summary>
        /// Rides each cube in <paramref name="looks"/> from <paramref name="from"/>[i] to
        /// <paramref name="to"/>[i] (world centres, both as Core reported them) on its board's
        /// profile. <paramref name="targets"/> are the cells it is going to - <paramref name="board"/>
        /// keeps them blank until the cubes are there. <paramref name="boardRect"/> is the board's rect,
        /// for the escalator's fresh bottom row.
        /// </summary>
        public void Play(BoardView board, IReadOnlyList<Vector2> from, IReadOnlyList<Vector2> to,
            IReadOnlyList<GridPos> targets, IReadOnlyList<BoardMotionSource> sources,
            IReadOnlyList<ClusterBurstView.Look> looks, float cellSize, float cubeSize, Rect boardRect)
        {
            if (board == null || from == null || to == null || from.Count == 0 || cellSize <= 0f)
            {
                return;
            }
            // A move still under way on this board lands at once: its cells have moved on.
            for (int b = batches.Count - 1; b >= 0; b--)
            {
                if (batches[b].Board == board)
                {
                    Release(batches[b]);
                    batches.RemoveAt(b);
                }
            }
            var batch = new Batch
            {
                Board = board,
                Cell = cellSize,
                CubeSize = cubeSize > 0f ? cubeSize : cellSize
            };
            Sprite fallbackTile = ViewUtil.DefaultTile != null ? ViewUtil.DefaultTile : ViewUtil.WhiteSprite;
            bool escalator = false;
            int n = Mathf.Min(from.Count, to.Count);
            for (int i = 0; i < n; i++)
            {
                BoardMotionSource source = sources != null && i < sources.Count
                    ? sources[i] : BoardMotionSource.Escalator;
                escalator |= source != BoardMotionSource.Centrifuge;
                var m = new Mover { From = from[i], To = to[i], P = ProfileFor(source) };
                Vector2 delta = to[i] - from[i];
                m.Dir = delta.sqrMagnitude > 1e-8f ? delta.normalized : Vector2.up;
                uint seed = Hash(Mathf.RoundToInt(to[i].x / cellSize * 4f), Mathf.RoundToInt(to[i].y / cellSize * 4f));
                var dice = new Dice(seed);
                m.Start = m.P.Variation > 0f ? dice.Range(0f, 2f) * m.P.Variation : 0f;
                bool known = looks != null && i < looks.Count && looks[i].Tile != null;
                m.Look = known ? looks[i] : new ClusterBurstView.Look
                {
                    Tile = fallbackTile,
                    Colour = new Color(0.62f, 0.68f, 0.82f)
                };
                m.Shadow = Rent(ShadowSprite(), ShadowOrder, null);
                // Its own material, so water swirls and fire smoulders all the way there.
                m.Body = Rent(m.Look.Tile, BodyOrder, ViewUtil.TileMaterial(m.Look.Tile));
                m.Light = Rent(EdgeSprite(m.Dir), SurfaceOrder, null);
                m.Shade = Rent(EdgeSprite(-m.Dir), SurfaceOrder, null);
                m.Line = Rent(LineSprite(), DebugOrder, null);
                m.Marker = Rent(MarkerSprite(), DebugOrder, null);
                if (targets != null && i < targets.Count)
                {
                    batch.Held.Add(targets[i]);
                }
                PaintMover(batch, m, -m.Start);
                batch.Movers.Add(m);
                batch.End = Mathf.Max(batch.End, m.Start + m.P.Lead + m.P.Move + m.P.Settle + ShadowFade);
            }
            if (escalator && Style.EscalatorBottomRowRevealDuration > 0f)
            {
                batch.Reveal = Rent(ViewUtil.WhiteSprite, RevealOrder, null);
                batch.RevealRect = new Rect(boardRect.xMin, boardRect.yMin, boardRect.width, cellSize);
                batch.End = Mathf.Max(batch.End, Style.EscalatorBottomRowRevealDuration);
                PaintReveal(batch);
            }
            board.HoldCells(batch.Held);
            batches.Add(batch);
        }

        /// <summary>Lands everything at once - the run ending, the board being torn down.</summary>
        public void Stop()
        {
            for (int i = 0; i < batches.Count; i++)
            {
                Release(batches[i]);
            }
            batches.Clear();
        }

        // =================================================================== the clock

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            for (int b = batches.Count - 1; b >= 0; b--)
            {
                Batch batch = batches[b];
                batch.Clock += dt;
                for (int i = 0; i < batch.Movers.Count; i++)
                {
                    Mover m = batch.Movers[i];
                    PaintMover(batch, m, batch.Clock - m.Start);
                }
                PaintReveal(batch);
                if (batch.Clock >= batch.End)
                {
                    Release(batch);
                    batches.RemoveAt(b);
                }
            }
        }

        /// <summary>One cube at its own age: loaded, moving, landing, home.</summary>
        private void PaintMover(Batch batch, Mover m, float t)
        {
            Profile p = m.P;
            float cell = batch.Cell;
            float size = batch.CubeSize;
            float speed;
            float along = Along(p, t, out speed);
            float cruise = Vmax(p) * (1f + p.Overshoot) / p.Move;
            float pace = Mathf.Clamp01(speed / Mathf.Max(cruise, 0.0001f));
            float u = (t - p.Lead) / p.Move;
            // Off the surface through the move, down again by the time it arrives.
            float lifted = Smooth(u / 0.2f) * (1f - Smooth((u - 0.8f) / 0.2f));
            // The straight line from the cell it left to the cell it is going to - nothing else.
            Vector2 at = m.From + (m.To - m.From) * along;
            Vector2 body = at + new Vector2(0f, p.Lift * cell * lifted);

            float stretch = 0f;
            float cross = 0f;
            if (Layers.ShowScaleResponse)
            {
                float push = p.StretchEarly ? pace * (1f - Smooth((u - 0.35f) / 0.3f)) : pace;
                stretch = p.Stretch * push;
                cross = p.Cross * push;
                if (p.Squash > 0f && t > 0f && t < 2f * p.Lead)
                {
                    stretch -= p.Squash * Mathf.Sin(Mathf.PI * t / (2f * p.Lead));
                }
            }
            float ax = Mathf.Abs(m.Dir.x);
            float ay = Mathf.Abs(m.Dir.y);
            float sx = 1f + stretch * ax - cross * ay;
            float sy = 1f + stretch * ay - cross * ax;
            Color tint = Layers.ShowMovementProxy ? Color.Lerp(m.Look.Colour, DebugColour, 0.35f) : m.Look.Colour;
            PlaceAxes(m.Body, body, size * sx, size * sy, tint, 1f);

            // ---- the contact shadow: in as it sets off, softer while it is up, locked as it lands,
            // gone once it has settled (a cube at rest on the board has none) ----
            float end = p.Lead + p.Move + p.Settle;
            float fadeIn = Smooth(t / 0.04f);
            float fadeOut = 1f - Smooth((t - end) / ShadowFade);
            float shadowAlpha = ShadowAlpha * fadeIn * fadeOut;
            Vector2 shadowAt = at + new Vector2(0f, -0.02f * cell);
            float shadowSize = size * 1.02f;
            if (Layers.ShowContactShadowResponse)
            {
                float soften = Style.ShadowLiftStrength * lifted;
                float lockK = Mathf.Clamp01((t - (p.Lead + p.Move * 0.85f)) / (p.Move * 0.15f + p.Settle));
                float tighten = Style.ShadowSettleStrength * Mathf.Sin(Mathf.PI * lockK);
                shadowAlpha *= (1f - 0.6f * soften) * (1f + 0.35f * tighten);
                shadowSize = size * (1.02f + 0.08f * soften - 0.03f * tighten);
                shadowAt -= m.Dir * (p.ShadowLag * cell * pace);
            }
            PlaceAxes(m.Shadow, shadowAt, shadowSize, shadowSize, ShadowColour, shadowAlpha);

            // ---- the surface: the leading edge a touch lighter, the trailing one a touch darker ----
            PlaceAxes(m.Light, body, size * sx, size * sy, Color.white, p.EdgeLight * pace);
            PlaceAxes(m.Shade, body, size * sx, size * sy, Color.black, p.TrailShade * pace);

            // ---- the lab's debug drawing ----
            Vector2 span = m.To - m.From;
            if (Layers.ShowMovementVector && span.sqrMagnitude > 1e-8f)
            {
                PlaceRect(m.Line, (m.From + m.To) * 0.5f, span.magnitude, 0.03f * cell, DebugColour, 0.85f,
                    Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
            }
            else
            {
                m.Line.color = Clear;
            }
            if (Layers.ShowDestinationCell)
            {
                PlaceAxes(m.Marker, m.To, cell * 0.96f, cell * 0.96f, DebugColour, 0.7f);
            }
            else
            {
                m.Marker.color = Clear;
            }
        }

        /// <summary>The escalator's fresh bottom row: its floor comes up out of the dark, no more.</summary>
        private void PaintReveal(Batch batch)
        {
            if (batch.Reveal == null)
            {
                return;
            }
            float k = batch.Clock / Mathf.Max(Style.EscalatorBottomRowRevealDuration, 0.0001f);
            PlaceRect(batch.Reveal, batch.RevealRect.center, batch.RevealRect.width, batch.RevealRect.height,
                RevealColour, RevealAlpha * (1f - Smooth(k)), 0f);
        }

        // =================================================================== renderers

        /// <summary>A sprite whose one unit is its full size, sized width by height, never turned.</summary>
        private static void PlaceAxes(SpriteRenderer r, Vector2 at, float width, float height, Color colour,
            float alpha)
        {
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = new Vector3(width, height, 1f);
        }

        private static void PlaceRect(SpriteRenderer r, Vector2 at, float width, float height,
            Color colour, float alpha, float angle)
        {
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            Vector2 unit = r.sprite != null
                ? new Vector2(r.sprite.rect.width, r.sprite.rect.height) / r.sprite.pixelsPerUnit
                : Vector2.one;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            r.transform.localScale = new Vector3(width / Mathf.Max(unit.x, 0.0001f),
                height / Mathf.Max(unit.y, 0.0001f), 1f);
        }

        private SpriteRenderer Rent(Sprite sprite, int order, Material material)
        {
            SpriteRenderer r;
            if (spareRenderers.Count > 0)
            {
                r = spareRenderers.Pop();
            }
            else
            {
                var go = new GameObject("BossMove");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plainMaterial == null)
                {
                    plainMaterial = r.sharedMaterial;
                }
            }
            // A pooled renderer may have last worn a water tile's material.
            Material wanted = material != null ? material : plainMaterial;
            if (wanted != null && r.sharedMaterial != wanted)
            {
                r.sharedMaterial = wanted;
            }
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
            spareRenderers.Push(r);
        }

        /// <summary>The cubes are home: the board takes its cells back (and repaints them with the
        /// cubes that stand there), and the copies go.</summary>
        private void Release(Batch batch)
        {
            if (batch.Board != null)
            {
                batch.Board.ReleaseCells(batch.Held);
            }
            for (int i = 0; i < batch.Movers.Count; i++)
            {
                Mover m = batch.Movers[i];
                Return(m.Shadow);
                Return(m.Body);
                Return(m.Light);
                Return(m.Shade);
                Return(m.Line);
                Return(m.Marker);
            }
            Return(batch.Reveal);
        }

        // =================================================================== shared art

        private static Sprite shadowSprite;

        private static Sprite lineSprite;

        private static Sprite markerSprite;

        private static readonly Dictionary<int, Sprite> edgeSprites = new Dictionary<int, Sprite>();

        /// <summary>The contact shadow: a rounded square whose edge softens inward.</summary>
        private static Sprite ShadowSprite()
        {
            if (shadowSprite != null)
            {
                return shadowSprite;
            }
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float sd = RoundedBox((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f, 0.2f);
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(-sd / 0.12f) * 255f));
                }
            }
            shadowSprite = MakeSprite(n, n, px, n);
            return shadowSprite;
        }

        /// <summary>A soft wash toward one edge (or corner) of a rounded cube face: with the step, the
        /// leading edge's light; with it reversed, the trailing edge's shade. One per direction.</summary>
        private static Sprite EdgeSprite(Vector2 dir)
        {
            int kx = Mathf.RoundToInt(Mathf.Clamp(dir.x * 1.5f, -1f, 1f));
            int ky = Mathf.RoundToInt(Mathf.Clamp(dir.y * 1.5f, -1f, 1f));
            int key = (kx + 1) * 3 + ky + 1;
            Sprite sprite;
            if (edgeSprites.TryGetValue(key, out sprite))
            {
                return sprite;
            }
            var d = new Vector2(kx, ky);
            if (d.sqrMagnitude < 0.5f)
            {
                d = Vector2.up;
            }
            d.Normalize();
            float extent = Mathf.Abs(d.x) + Mathf.Abs(d.y);
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f;
                    float v = (y + 0.5f) / n - 0.5f;
                    float face = Mathf.Clamp01(-RoundedBox(u, v, 0.16f) / (1.5f / n));
                    float s = (u * 2f * d.x + v * 2f * d.y) / extent;
                    float wash = Mathf.Clamp01((s - 0.15f) / 0.85f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(face * wash * wash * 255f));
                }
            }
            sprite = MakeSprite(n, n, px, n);
            edgeSprites[key] = sprite;
            return sprite;
        }

        /// <summary>The debug line: a thin bar with soft ends.</summary>
        private static Sprite LineSprite()
        {
            if (lineSprite != null)
            {
                return lineSprite;
            }
            const int w = 64;
            const int h = 8;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float along = (x + 0.5f) / w;
                    float off = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                    float a = Mathf.Clamp01(1f - off) * Mathf.Clamp01(along / 0.08f) * Mathf.Clamp01((1f - along) / 0.08f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            lineSprite = MakeSprite(w, h, px, w);
            return lineSprite;
        }

        /// <summary>The debug outline on a destination cell: a thin rounded ring.</summary>
        private static Sprite MarkerSprite()
        {
            if (markerSprite != null)
            {
                return markerSprite;
            }
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float sd = RoundedBox((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f, 0.14f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(sd + 0.03f) / 0.02f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            markerSprite = MakeSprite(n, n, px, n);
            return markerSprite;
        }

        private static Sprite MakeSprite(int w, int h, Color32[] px, float ppu)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        }

        /// <summary>Signed distance to a rounded square of half-size 0.5: negative inside.</summary>
        private static float RoundedBox(float u, float v, float radius)
        {
            float dx = Mathf.Abs(u) - (0.5f - radius);
            float dy = Mathf.Abs(v) - (0.5f - radius);
            float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)
                + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
            return outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
        }
    }
}
