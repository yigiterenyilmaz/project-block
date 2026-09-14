// PURPOSE: "Yılan" (Snake) - the one boss that is a LIVING THING loose in the arena, drawn from the
// six pieces that were painted for it: a head, an open-mouthed head, a straight body, a 90-degree
// bend, a tail tip and a swollen segment. NOTHING here draws a snake: it places those pieces.
//
// Each piece's PIVOT is on the cell centre its tube belongs to (baked into the sprites' import
// settings), so a segment is placed at a cell centre and turned in quarter steps, and the mouths
// land on the cell boundaries by themselves. The topology is derived, not guessed:
//
//   bend, drawn as LEFT + DOWN, turned anticlockwise: 0 left+down, 1 down+right, 2 right+up,
//   3 up+left. Head and tail are drawn facing RIGHT: 0 right, 1 up, 2 left, 3 down.
//
// THE SNAKE'S MOTION IS ONE SPINE. Core reports the body after EVERY CELL of a slide
// (SnakeTurnVisuals.StepSnapshots), and those cells are strung into a single polyline - the head's
// path in front, the body it is dragging behind. Every segment then rides that polyline at a fixed
// spacing of one cell, so the head leads, the body follows through its own shape and the tail comes
// last, all from one number: how far along the head has come. At the end of the move every segment
// lands exactly on the cell Core says it is in, because the polyline IS those cells.
//
//   SPAWN    a wake-up runs tail to head: each segment comes up from 88% and a little drained to
//            full size and colour. The head is last, and settles a pixel forward - it is awake.
//   IDLE     one low peristaltic wave every few seconds, head to tail: a percent and a half of
//            cross-axis swell, a hair of shortening. Muscle, not jelly, and never per-segment noise.
//   MOVE     a small muscular release, a smooth carry, a deceleration into the last cell. Long
//            slides take longer, but not linearly - six cells must not take six times as long.
//   STUCK    it tries: a pixel of head pressure and a short contraction three segments deep, and
//            nothing moves. A turn where the snake does nothing at all reads as a bug.
//   BITE     the slide decelerates into the block, the head pulls back a pixel, the mouth opens,
//            it lunges the last quarter cell, and THE BLOCK ITSELF - with its own face, kept from
//            the report - is drawn into the mouth and occluded by the head. Then the mouth closes,
//            a gulp passes into the neck, and the segment behind the head swells: the snake grew.
//   CUT      the player's line does not break a single segment. It sends a constriction down the
//            body from where it crossed, the tail root tightens, and the last segment lets go:
//            recoil, shrink, fade, a few flecks. The new last segment becomes a tail.
//   DEFEAT   the last segment collapses inward and goes to a handful of soft motes. No explosion.
//
// NOTHING HERE DECIDES ANYTHING. Which way it went, how far, what it ate, how many segments the
// lines cut and which cells left the tail all come from Core (SnakeBoss.LastTurn); this plays them.
// The board does not draw snake cells at all (BoardView blanks them) - these sprites are the snake.
//
// NEVER: a green square, a procedural snake, disconnected tiles, a teleport, a sine slither, jelly,
// a particle trail, screen shake, an explosion for the bite, a generic removal effect for the block
// it eats, gore for the cut, or a hard delete of anything.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>"Yılan": the snake on the board, its slides, its bites and its cuts.</summary>
    public sealed class SnakeView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the snake READS. Sizes in CELLS, times in seconds.</summary>
        public static class Style
        {
            // ---- the body itself ----
            /// <summary>How much bigger than a cell a segment is drawn, so neighbours overlap and no
            /// joint can ever show a gap. The art's mouths are round, so this is invisible.</summary>
            public static float SegmentOverscale = 1.04f;

            // ---- spawn ----
            public static float SpawnDuration = 0.60f;

            /// <summary>Seconds between one segment waking and the next, tail to head.</summary>
            public static float SpawnSegmentDelay = 0.022f;

            public static float SpawnStartScale = 0.88f;

            /// <summary>How drained a segment starts: 0 is its own colour, 1 is grey.</summary>
            public static float SpawnBrightnessStart = 0.45f;

            /// <summary>How far the head settles forward as it comes awake, in cells.</summary>
            public static float SpawnHeadSettleAmount = 0.03f;

            // ---- idle ----
            public static float IdleWavePeriodMin = 2.0f;

            public static float IdleWavePeriodMax = 3.2f;

            /// <summary>Seconds the wave takes to reach one segment further down the body.</summary>
            public static float IdleWaveSegmentDelay = 0.085f;

            /// <summary>The swell across the body at the top of the wave, as a share of a cell.</summary>
            public static float IdleCrossScaleAmount = 0.095f;

            /// <summary>And the shortening along it - always smaller than the swell.</summary>
            public static float IdleLongScaleAmount = 0.042f;

            /// <summary>The head's own small forward pressure, in cells.</summary>
            public static float IdleHeadPressureAmount = 0.040f;

            /// <summary>The tail tip's rare micro settle, in cells. Never a wagging tail.</summary>
            public static float IdleTailSwayAmount = 0.060f;

            // ---- move ----
            public static float MoveDurationOneCell = 0.23f;

            /// <summary>Each further cell costs this much more - far less than the first, so a six
            /// cell slide is quick without ever looking like a teleport.</summary>
            public static float MoveDurationPerExtraCell = 0.075f;

            public static float MoveMaxDuration = 0.72f;

            /// <summary>The share of the slide spent picking up speed, and slowing down.</summary>
            public static float MoveEaseIn = 0.24f;

            public static float MoveEaseOut = 0.34f;

            /// <summary>The contraction that runs down the body while it slides, as a share of a
            /// cell. A percent or two: it is muscle under the skin, not a wobble.</summary>
            public static float MoveBodyWaveStrength = 0.05f;

            /// <summary>How fast that contraction runs down the body, in segments a second.</summary>
            public static float MoveBodyWaveSpeed = 18f;

            /// <summary>The head's pressure into the direction as the slide starts, in cells.</summary>
            public static float MoveHeadLeadAmount = 0.022f;

            public static float MoveSettleDuration = 0.07f;

            /// <summary>How far past its cell the head carries before settling back, in cells.</summary>
            public static float MoveSettleAmount = 0.014f;

            /// <summary>How long the head takes to come round to a new direction.</summary>
            public static float HeadTurnDuration = 0.075f;

            /// <summary>How long a BODY piece takes to come round to a new quarter turn. Snapping
            /// it (which is what a rotation straight off the turn table does) is what made a
            /// corner look like the whole snake flipping at once instead of a bend travelling
            /// down it.</summary>
            public static float BodyTurnDuration = 0.13f;

            /// <summary>How long a segment takes to become a bend, or stop being one.</summary>
            public static float TopologyCrossfadeDuration = 0.1f;

            /// <summary>How close to a corner, in cells, a segment has to be to wear the elbow.
            /// The elbow is drawn at the segment's own place on the spine, so this is also how
            /// far its mouths may be out of line with its neighbours': at half a cell that is
            /// half a tube's width and the body reads as stepping sideways, not bending.</summary>
            public static float BendWindow = 0.3f;

            // ---- stuck ----
            public static float StuckDuration = 0.22f;

            /// <summary>The head's shove into a wall it cannot pass, in cells. It has to be
            /// felt: at 1.6% this was a single pixel, and being boxed in looked like a turn where
            /// nothing happened at all.</summary>
            public static float StuckHeadPressure = 0.09f;

            /// <summary>How many segments behind the head feel it.</summary>
            public static int StuckBodyWaveLength = 4;

            public static float StuckBodyWaveStrength = 0.13f;

            // ---- the bite ----
            //
            // ITS TIMELINE IS NOT HERE. The coil, the mouth, the lunge, the extraction, the core,
            // the jaw and the gulp are one overlapping sequence owned by SnakeEatView.Style, and
            // read off it here - the head and the block's matter have to be on the same clock or
            // the bite becomes two animations happening near each other.

            /// <summary>How far SHORT of the block the head stops, in cells: its snout comes
            /// to the block's own face and no further. A head parked on top of the block hides
            /// the thing it is about to eat, and leaves the bite nowhere to happen.</summary>
            public static float BiteLungeDistance = 0.9f;

            /// <summary>The swallow pressure in the head and neck, as a share of a cell.</summary>
            public static float GulpStrength = 0.030f;

            /// <summary>How big the segment a bite adds starts out, as a share of full size:
            /// it comes up to size where the body already ends, as the block goes down.</summary>
            public static float GrowthSegmentStartScale = 0.45f;

            public static float SwollenAppearDuration = 0.09f;

            public static float SwollenOvershoot = 0.07f;

            /// <summary>How long the swelling stays before the body goes back to normal.</summary>
            public static float SwollenHoldDuration = 0.55f;

            public static float SwollenFadeDuration = 0.12f;

            // ---- the cut ----
            /// <summary>The squeeze where the player's line crossed the snake, as a share of a cell.</summary>
            public static float CutIntersectionResponse = 0.085f;

            public static float CutSignalTravelDuration = 0.22f;

            /// <summary>How hard the constriction squeezes as it passes a segment.</summary>
            public static float CutSignalCompression = 0.16f;

            public static float TailConstrictDuration = 0.08f;

            /// <summary>How far the cut segment recoils as it lets go, in cells.</summary>
            public static float TailDetachDistance = 0.16f;

            public static float TailDetachDuration = 0.20f;

            /// <summary>Degrees the loose segment turns as it goes. A couple, not a tumble.</summary>
            public static float TailDetachRotation = 16f;

            /// <summary>How long the new last segment takes to become a tail.</summary>
            public static float NewTailMorphDuration = 0.10f;

            public static float NewTailSettleAmount = 0.042f;

            /// <summary>Between one cut of a multi-cut turn and the next.</summary>
            public static float MultiCutDelay = 0.10f;

            // ---- defeat ----
            public static float DefeatCollapseDuration = 0.26f;

            public static float DefeatCollapseScale = 0.55f;

            // ---- level of detail ----
            /// <summary>Past this many segments the body's own movement and idle amplitudes are
            /// turned down: twenty segments all flexing at once is noise, not life.</summary>
            public static int LodLongLength = 15;

            public static float LodLongAmplitude = 0.55f;

        }

        /// <summary>The lab's debug switches. All off in a real round; the lab puts them back.</summary>
        public static class Layers
        {
            /// <summary>Off: the snake stands in the cells Core says it is in, with no animation at
            /// all - what the VFX layers are worth is easiest to judge against that.</summary>
            public static bool ShowSnakeMotion = true;

            public static bool ShowSnakeBodyIndices;

            public static bool ShowSnakeHeadPath;

            public static bool ShowSnakeMovementDirection;

            /// <summary>A mark per segment, coloured by the piece it is drawn with.</summary>
            public static bool ShowSnakeTopology;

            public static bool ShowSnakeEatenCell;

            public static bool ShowSnakeCutTrigger;

            /// <summary>Where each segment's spine position is, before a bend is snapped to its cell.</summary>
            public static bool ShowSnakeVisualProxyPositions;

            public static void Defaults()
            {
                ShowSnakeMotion = true;
                ShowSnakeBodyIndices = false;
                ShowSnakeHeadPath = false;
                ShowSnakeMovementDirection = false;
                ShowSnakeTopology = false;
                ShowSnakeEatenCell = false;
                ShowSnakeCutTrigger = false;
                ShowSnakeVisualProxyPositions = false;
            }
        }

        // =================================================================== palette
        //
        // The snake's own colours are IN ITS ART and must not be tinted away. What is here is only
        // what the art cannot carry: the shadow it sits in, the flecks a cut throws, and the lab's
        // markers.

        private static readonly Color DebugColour = new Color(1f, 0.45f, 0.35f);

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        // =================================================================== layers
        //
        // The snake sits over the board's cells (1) and its own head over its body, so a bite reads
        // with the block going in BEHIND the head. Everything is below the blast effects (5+).

        // The VFX layer draws below (its light, board marks and shadows) and above (its flecks);
        // these are the snake's own three.
        private const int ProxyOrder = 4;

        private const int BodyOrder = 5;

        private const int HeadOrder = 6;

        private const int DebugOrder = 11;

        // =================================================================== the pieces

        private enum Piece
        {
            Head,
            HeadOpen,
            Body,
            Bend,
            Tail,
            Swollen
        }

        private static readonly Vector2[] Cardinals =
        {
            new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(0f, -1f)
        };

        /// <summary>The quarter turn that points a piece drawn facing RIGHT at <paramref name="dir"/>.
        /// Anticlockwise, which is what Quaternion.Euler(0, 0, +angle) does.</summary>
        private static int TurnForFacing(Vector2 dir)
        {
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            {
                return dir.x >= 0f ? 0 : 2;
            }
            return dir.y >= 0f ? 1 : 3;
        }

        /// <summary>The quarter turn for a BEND, whose art connects LEFT and DOWN, so that it
        /// connects <paramref name="a"/> and <paramref name="b"/>. Rotating the art's own two connections
        /// covers all four elbows - no mirroring anywhere.</summary>
        private static bool TryTurnForBend(Vector2 a, Vector2 b, out int turn)
        {
            for (turn = 0; turn < 4; turn++)
            {
                Vector2 left = Rotate(new Vector2(-1f, 0f), turn);
                Vector2 down = Rotate(new Vector2(0f, -1f), turn);
                if ((Same(left, a) && Same(down, b)) || (Same(left, b) && Same(down, a)))
                {
                    return true;
                }
            }
            // NOT a quarter turn of the art's own two connections, so there is no elbow that can
            // honestly join these two directions. It used to answer 0 here - the art as drawn,
            // left and down - which is an elbow pointing somewhere the body is not going.
            turn = 0;
            return false;
        }

        private static Vector2 Rotate(Vector2 v, int quarters)
        {
            for (int i = 0; i < (quarters & 3); i++)
            {
                v = new Vector2(-v.y, v.x);
            }
            return v;
        }

        private static bool Same(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.y - b.y) < 0.01f;
        }

        /// <summary>A direction snapped to the four cardinals.</summary>
        private static Vector2 Snap(Vector2 v)
        {
            if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            {
                return new Vector2(Mathf.Sign(v.x), 0f);
            }
            return new Vector2(0f, Mathf.Sign(v.y));
        }

        // =================================================================== the spine
        //
        // One polyline: the head's path in front (the cells Core says it slid through), then the
        // body it is dragging. Every segment rides it at one cell's spacing, which is what makes
        // the snake one moving thing instead of a row of tiles.

        private sealed class Spine
        {
            private readonly List<Vector2> points = new List<Vector2>();

            private readonly List<float> arcs = new List<float>();

            /// <summary>The distance from the FRONT of the polyline to each of its corners.</summary>
            public IReadOnlyList<float> CornerArcs
            {
                get { return arcs; }
            }

            public float Length
            {
                get { return arcs.Count > 0 ? arcs[arcs.Count - 1] : 0f; }
            }

            public void Build(IReadOnlyList<Vector2> front, IReadOnlyList<Vector2> behind)
            {
                points.Clear();
                arcs.Clear();
                for (int i = 0; i < front.Count; i++)
                {
                    Add(front[i]);
                }
                for (int i = 0; i < behind.Count; i++)
                {
                    Add(behind[i]);
                }
            }

            private void Add(Vector2 p)
            {
                if (points.Count > 0 && (points[points.Count - 1] - p).sqrMagnitude < 1e-8f)
                {
                    return;
                }
                float arc = points.Count == 0 ? 0f
                    : arcs[arcs.Count - 1] + (p - points[points.Count - 1]).magnitude;
                points.Add(p);
                arcs.Add(arc);
            }

            /// <summary>The point at a distance along the polyline, measured from its front. Past
            /// either end it carries straight on, so a segment never bunches up at the tail.</summary>
            public Vector2 At(float arc)
            {
                if (points.Count == 0)
                {
                    return Vector2.zero;
                }
                if (points.Count == 1)
                {
                    return points[0];
                }
                if (arc <= 0f)
                {
                    return points[0] + (points[0] - points[1]).normalized * -arc;
                }
                float end = arcs[arcs.Count - 1];
                if (arc >= end)
                {
                    int n = points.Count - 1;
                    return points[n] + (points[n] - points[n - 1]).normalized * (arc - end);
                }
                for (int i = 1; i < points.Count; i++)
                {
                    if (arc <= arcs[i])
                    {
                        float span = Mathf.Max(arcs[i] - arcs[i - 1], 1e-5f);
                        return Vector2.Lerp(points[i - 1], points[i], (arc - arcs[i - 1]) / span);
                    }
                }
                return points[points.Count - 1];
            }

            /// <summary>The way the body runs at a point, pointing toward the HEAD.</summary>
            public Vector2 Toward(float arc)
            {
                Vector2 a = At(arc - 0.001f);
                Vector2 b = At(arc + 0.001f);
                Vector2 d = a - b;
                return d.sqrMagnitude > 1e-10f ? d.normalized : new Vector2(1f, 0f);
            }

            /// <summary>The corner nearest an arc position, and how far away it is - what decides
            /// whether a segment is drawn as a bend, and where that bend sits.</summary>
            public bool NearestCorner(float arc, float within, out Vector2 at, out float distance,
                out Vector2 into, out Vector2 outOf)
            {
                at = Vector2.zero;
                distance = float.MaxValue;
                into = Vector2.zero;
                outOf = Vector2.zero;
                for (int i = 1; i < points.Count - 1; i++)
                {
                    Vector2 before = (points[i] - points[i - 1]).normalized;
                    Vector2 after = (points[i + 1] - points[i]).normalized;
                    if (Mathf.Abs(Vector2.Dot(before, after)) > 0.2f)
                    {
                        continue; // straight through
                    }
                    float d = Mathf.Abs(arcs[i] - arc);
                    if (d < distance)
                    {
                        distance = d;
                        at = points[i];
                        // THE CORNER'S OWN TWO LIMBS. Handed out because an elbow pointed by
                        // samples taken NEAR it gets them wrong exactly when it is furthest from
                        // it: both samples land on one limb and there is nothing to match.
                        into = before;
                        outOf = after;
                    }
                }
                return distance <= within;
            }
        }

        // =================================================================== the turn it is handed
        //
        // Built by GameUiController from SnakeBoss.LastTurn - a copy, never a guess - and by the
        // animation lab from a staged snake. Nothing in here is worked out by the View.

        /// <summary>One segment the player's line took off the tail.</summary>
        public struct CutStep
        {
            /// <summary>A segment the exploding line actually crossed: where the signal starts.</summary>
            public GridPos Trigger;

            /// <summary>The tail cell that let go.</summary>
            public GridPos RemovedTail;

            /// <summary>The snake as it stood after this cut, head first.</summary>
            public List<GridPos> BodyAfter;
        }

        /// <summary>Everything the snake did on one turn, in the order the rules did it.</summary>
        public sealed class TurnScene
        {
            /// <summary>The body as the turn ended, head first, before any cut.</summary>
            public readonly List<GridPos> BodyBefore = new List<GridPos>();

            /// <summary>The cuts the player's lines made, in order.</summary>
            public readonly List<CutStep> Cuts = new List<CutStep>();

            /// <summary>The last cut finished it.</summary>
            public bool Defeated;

            /// <summary>It had nowhere to go.</summary>
            public bool Stuck;

            /// <summary>The body after EVERY CELL of the slide, as Core reported it.</summary>
            public readonly List<List<GridPos>> Steps = new List<List<GridPos>>();

            /// <summary>The cell it ate, if it ate.</summary>
            public GridPos? EatenCell;

            /// <summary>And the block that was standing there, with its own face.</summary>
            public ClusterBurstView.Look EatenLook;

            public bool Grew;

            /// <summary>Where the snake stands now.</summary>
            public readonly List<GridPos> BodyAfter = new List<GridPos>();
        }

        // =================================================================== state

        private BoardView view;

        private GameBoard board;

        private float cellSize;

        /// <summary>The snake as it is DRAWN right now, head first. An animation moves this to
        /// where Core says it is; it is never a second opinion about the rules.</summary>
        private readonly List<GridPos> body = new List<GridPos>();

        /// <summary>Where Core says it is, kept so the drawing can be put back in step whenever
        /// nothing is playing (a loaded save, the lab, a round change).</summary>
        private readonly List<GridPos> settled = new List<GridPos>();

        private sealed class Segment
        {
            public SpriteRenderer Main;
            public SpriteRenderer Fade;      // the piece it is coming FROM, crossfading out
            public Piece Piece;
            public int Turn;
            public Piece FadePiece;
            public int FadeTurn;
            public Vector2 FadeAt;
            public float FadeClock;          // seconds left of the crossfade
            public float FadeLength;
            public float Angle;              // where it is pointing NOW, in degrees
            public bool HasAngle;            // false until it has been placed once
        }

        private readonly List<Segment> segments = new List<Segment>();

        private enum Beat
        {
            Spawn,
            Move,
            Stuck,
            Cut,
            Defeat
        }

        private sealed class Act
        {
            public Beat Kind;
            public float Clock;
            public float Length;
            public readonly List<GridPos> From = new List<GridPos>();
            public readonly List<GridPos> To = new List<GridPos>();
            /// <summary>The head's path, world centres, FRONT first: where it ends, back to where
            /// it started. Built from Core's own per-cell snapshots.</summary>
            public readonly List<Vector2> Path = new List<Vector2>();
            public GridPos? Eaten;
            public ClusterBurstView.Look EatenLook;
            public bool Grew;
            public GridPos Trigger;
            public GridPos RemovedTail;
            public float Delay;              // a held breath before it starts (multi-cut)
            // Which of its beats the VFX layer has already been told about: every hook fires once.
            public bool SaidMouth;
            public bool SaidContact;
            public bool SaidIngest;
            public bool SaidIngestDone;
            public bool SaidGulp;
            public bool SaidPinch;
            public int WokenTo;
            public Vector2 LastFacing;
        }

        private readonly List<Act> queue = new List<Act>();

        private Act act;

        private readonly Spine spine = new Spine();

        private readonly List<Vector2> pathBuffer = new List<Vector2>();

        private readonly List<Vector2> behindBuffer = new List<Vector2>();

        /// <summary>The swelling behind the head after a meal: which segment, and how old it is.
        /// It runs on its own clock so it can outlive the bite without holding the queue.</summary>
        private int swollenIndex = -1;

        private float swollenClock;

        /// <summary>A segment that has let go and is drifting off, and the motes of a defeat.</summary>
        private sealed class Loose
        {
            public Vector2 At;
            public Vector2 Drift;
            public Piece Piece;
            public int Turn;
            public float Clock;
            public float Life;
            public float Spin;
            public SpriteRenderer Renderer;
            public bool Collapse;            // the defeat's inward collapse rather than a recoil
            public bool SaidCollapse;
            public bool SaidMote;
        }

        private readonly List<Loose> loose = new List<Loose>();

        private SnakeEatView eat;             // the block being eaten, taken apart
        private SnakeDefeatView beaten;       // and the colours it swallowed, let go of

        private readonly List<SpriteRenderer> debugMarks = new List<SpriteRenderer>();

        private readonly List<TextMesh> debugLabels = new List<TextMesh>();

        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

        private Material plainMaterial;

        private float idleClock;

        /// <summary>The head's drawn angle, which comes ROUND to a new direction over
        /// Style.HeadTurnDuration instead of snapping - a head that flipped would break the neck.</summary>
        private float headAngle;

        /// <summary>The recoil left in the segment that has just become the tail.</summary>
        private float newTailClock;

        private Vector2 newTailAway;

        /// <summary>The VFX layer: the surface response, the shadows, what the board feels and
        /// the few flecks a hero action throws. A child of this, so it lives and dies with it.</summary>
        private SnakeVfxController vfx;

        private SnakeVfxController Vfx
        {
            get
            {
                if (vfx == null)
                {
                    var go = new GameObject("SnakeVfx");
                    go.transform.SetParent(transform, false);
                    vfx = go.AddComponent<SnakeVfxController>();
                }
                return vfx;
            }
        }
        // =================================================================== driving it

        /// <summary>Geometry, and nothing else: a new arena drops whatever was playing.</summary>
        public void Sync(BoardView owner)
        {
            view = owner;
            GameBoard now = owner != null ? owner.Board : null;
            if (now != board)
            {
                Stop();
                board = now;
            }
            if (view != null)
            {
                cellSize = view.CellWorldSize;
            }
        }

        /// <summary>
        /// Where Core says the snake is. With nothing playing the drawing is put there at once (a
        /// loaded round, a lab board); with a turn in flight it is only remembered, because the
        /// animation is on its way to exactly this state. <paramref name="spawn"/> asks for the
        /// wake-up rather than a snake that was simply always there.
        /// </summary>
        public void SetBody(IReadOnlyList<GridPos> cells, bool spawn)
        {
            settled.Clear();
            if (cells != null)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    settled.Add(cells[i]);
                }
            }
            if (spawn && settled.Count > 0)
            {
                Stop();
                CopyInto(body, settled);
                var wake = new Act { Kind = Beat.Spawn, Length = Style.SpawnDuration };
                CopyInto(wake.From, settled);
                CopyInto(wake.To, settled);
                queue.Add(wake);
                return;
            }
            if (act == null && queue.Count == 0)
            {
                CopyInto(body, settled);
            }
        }

        /// <summary>One turn, played as the sequence the rules made it: the cuts in order, then
        /// either the death or the slide (with the bite folded into its end).</summary>
        public void PlayTurn(TurnScene turn)
        {
            if (turn == null || view == null)
            {
                return;
            }
            // The drawing starts from where the snake stood BEFORE any of it happened.
            CopyInto(body, turn.BodyBefore);
            List<GridPos> standing = turn.BodyBefore;
            // THE LAST PIECE IS NOT CUT OFF - the boss's own ending takes it. Core says every
            // segment went, and it still ends on Core's state (nothing), but presenting the final
            // removal as an ordinary cut left the defeat with an EMPTY body to work from: no
            // piece to tighten, nothing to show the swallowed colours through, and no centre to
            // uncoil from. That is why the defeat drew nothing at all whatever palette it was
            // given - the whole block was behind an `a.From.Count > 0` guard that could never be
            // true.
            int cuts = turn.Cuts.Count;
            if (turn.Defeated && cuts > 0)
            {
                cuts--;
            }
            for (int i = 0; i < cuts; i++)
            {
                CutStep cut = turn.Cuts[i];
                var a = new Act
                {
                    Kind = Beat.Cut,
                    Trigger = cut.Trigger,
                    RemovedTail = cut.RemovedTail,
                    Delay = i == 0 ? 0f : Style.MultiCutDelay,
                    Length = 0.06f + Style.CutSignalTravelDuration + Style.TailConstrictDuration
                        + Style.TailDetachDuration
                };
                CopyInto(a.From, standing);
                CopyInto(a.To, cut.BodyAfter);
                queue.Add(a);
                standing = cut.BodyAfter;
            }
            if (turn.Defeated)
            {
                var end = new Act
                {
                    Kind = Beat.Defeat,
                    Length = SnakeDefeatView.Style.Total
                };
                // Whatever is left standing when the cuts stop: the last piece, which is the
                // subject of everything that follows. Never empty now.
                CopyInto(end.From, standing.Count > 0 ? standing : turn.BodyBefore);
                queue.Add(end);
                return;
            }
            if (!turn.Stuck && turn.Steps.Count == 0)
            {
                return; // cuts and nothing else: the turn is over
            }
            if (turn.Stuck)
            {
                var still = new Act { Kind = Beat.Stuck, Length = Style.StuckDuration };
                CopyInto(still.From, standing);
                CopyInto(still.To, standing);
                queue.Add(still);
                return;
            }
            var move = new Act { Kind = Beat.Move, Eaten = turn.EatenCell, EatenLook = turn.EatenLook,
                Grew = turn.Grew };
            CopyInto(move.From, standing);
            CopyInto(move.To, turn.BodyAfter);
            // THE PATH THE RULES TOOK: the head cell of every snapshot, front first.
            for (int i = turn.Steps.Count - 1; i >= 0; i--)
            {
                move.Path.Add(view.CellToWorld(turn.Steps[i][0]));
            }
            move.Path.Add(view.CellToWorld(standing[0]));
            // THE SLIDE IS TIMED BY WHAT IT ACTUALLY COVERS. A bite parks the head at the
            // block's face, so its slide is the path MINUS that parking distance - on an adjacent
            // block almost nothing. Timed as a whole cell instead, the snake stood still for a
            // quarter of a second before it struck, which is what "it doesn't move" was.
            float cells = turn.Steps.Count;
            if (move.Eaten.HasValue)
            {
                cells = Mathf.Max(cells - Style.BiteLungeDistance, 0.15f);
            }
            move.Length = Mathf.Min(Style.MoveDurationOneCell * Mathf.Min(cells, 1f)
                + Style.MoveDurationPerExtraCell * Mathf.Max(cells - 1f, 0f),
                Style.MoveMaxDuration) + Style.MoveSettleDuration;
            if (move.Eaten.HasValue)
            {
                move.Length += SnakeEatView.Style.Total;
            }
            queue.Add(move);
        }

        /// <summary>Everything playing ends where the rules already are.</summary>
        public void Stop()
        {
            act = null;
            queue.Clear();
            swollenIndex = -1;
            for (int i = 0; i < loose.Count; i++)
            {
                Return(loose[i].Renderer);
            }
            loose.Clear();
            if (eat != null)
            {
                eat.Stop();
            }
            newTailClock = 0f;
            if (vfx != null)
            {
                vfx.Stop();
            }
            for (int i = 0; i < segments.Count; i++)
            {
                Return(segments[i].Main);
                Return(segments[i].Fade);
            }
            segments.Clear();
            for (int i = 0; i < debugMarks.Count; i++)
            {
                Return(debugMarks[i]);
            }
            debugMarks.Clear();
            for (int i = 0; i < debugLabels.Count; i++)
            {
                if (debugLabels[i] != null)
                {
                    Destroy(debugLabels[i].gameObject);
                }
            }
            debugLabels.Clear();
            body.Clear();
        }

        /// <summary>True while any of it is still playing - the lab waits on this.</summary>
        /// <summary>Where the pieces actually ARE, for the lab to hold against the report.
        /// Nothing in the game reads this: it exists so a disagreement between what the rules
        /// said and what got drawn is something you can see rather than guess at.</summary>
        public IReadOnlyList<GridPos> DrawnBody
        {
            get { return body; }
        }

        public bool Busy
        {
            get { return act != null || queue.Count > 0; }
        }

        // =================================================================== the clock

        private void Update()
        {
            if (view == null || cellSize <= 0f)
            {
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            idleClock += dt;
            if (swollenIndex >= 0)
            {
                swollenClock += dt;
                if (swollenClock > Style.SwollenAppearDuration + Style.SwollenHoldDuration
                    + Style.SwollenFadeDuration)
                {
                    swollenIndex = -1;
                }
            }
            if (newTailClock > 0f)
            {
                newTailClock = Mathf.Max(newTailClock - dt, 0f);
            }
            if (turnSheen > 0f)
            {
                turnSheen = Mathf.Max(turnSheen - dt / 0.12f, 0f);
            }
            if (jawImpact > 0f)
            {
                jawImpact = Mathf.Max(jawImpact - dt / JawImpactFall, 0f);
            }
            if (beaten != null && beaten.Playing)
            {
                beaten.Paint(dt);
            }
            Advance(dt);
            PaintFrame();
            PaintLoose(dt);
        }

        private void Advance(float dt)
        {
            if (act == null)
            {
                if (queue.Count == 0)
                {
                    return;
                }
                act = queue[0];
                queue.RemoveAt(0);
                act.Clock = -act.Delay;
                OnActStarted(act);
            }
            act.Clock += dt;
            if (act.Clock < act.Length)
            {
                return;
            }
            OnActFinished(act);
            act = null;
            // THE BEAT THAT JUST FINISHED IS THE END STATE. `settled` is only the last body the
            // board HANDED us - before the turn played - so copying it back over the body is not
            // guarding against drift, it is undoing the turn: the snake ate, grew, moved into the
            // cell, and was then put back exactly where it started with the swelling left on it.
            // They are made to agree the other way round.
            if (queue.Count == 0)
            {
                CopyInto(settled, body);
            }
        }

        private void OnActStarted(Act a)
        {
            if (a.Kind == Beat.Move)
            {
                CopyInto(body, a.From);
                if (a.From.Count > 0)
                {
                    Vfx.Signal(SnakeVfxController.Hook.MoveStart, view.CellToWorld(a.From[0]),
                        Snap(new Vector2(a.To[0].X - a.From[0].X, a.To[0].Y - a.From[0].Y)),
                        default(ClusterBurstView.Look));
                }
            }
            if (a.Kind == Beat.Stuck && a.From.Count > 1)
            {
                // It shoved at something it cannot pass: the cell in front answers, not the snake.
                Vector2 facing = Snap(new Vector2(a.From[0].X - a.From[1].X,
                    a.From[0].Y - a.From[1].Y));
                Vfx.Signal(SnakeVfxController.Hook.StuckPressure, view.CellToWorld(a.From[0]),
                    facing, default(ClusterBurstView.Look));
            }
            if (a.Kind == Beat.Cut)
            {
                Vfx.Signal(SnakeVfxController.Hook.LineSnakeImpact, view.CellToWorld(a.Trigger),
                    new Vector2(0f, 1f), default(ClusterBurstView.Look));
            }
            if (a.Kind == Beat.Defeat && a.From.Count > 0)
            {
                // The last piece is drawn again for its own ending: the cuts left `body` at
                // whatever they ended on, and this beat's subject is the piece that survived them.
                CopyInto(body, a.From);
                Vfx.Signal(SnakeVfxController.Hook.DefeatStart, view.CellToWorld(a.From[0]),
                    new Vector2(0f, 1f), default(ClusterBurstView.Look));
                // THE BODY STAYS, and it is what tells the first half of this: it tightens, its
                // own teal goes quiet, the colours it SWALLOWED come up from underneath and run to
                // the middle, and it folds in unevenly with its shadow. The segments used to be
                // thrown off as loose pieces here, which is a sprite drifting away on its own -
                // the "deleted, with particles" reading the redesign exists instead of.
                SnakeDefeatView beaten = Beaten();
                if (beaten != null)
                {
                    Vector2 at = Vector2.zero;
                    for (int i = 0; i < a.From.Count; i++)
                    {
                        at += view.CellToWorld(a.From[i]);
                    }
                    beaten.Begin(view, at / Mathf.Max(a.From.Count, 1),
                        Hash(a.From[0].X, a.From[0].Y) ^ (uint)a.From.Count);
                }
            }
        }

        private void OnActFinished(Act a)
        {
            switch (a.Kind)
            {
                case Beat.Move:
                    CopyInto(body, a.To);
                    if (a.Grew)
                    {
                        // The swelling that says it grew, on the segment behind the head.
                        swollenIndex = 1;
                        swollenClock = 0f;
                        if (a.To.Count > 1)
                        {
                            Vfx.Signal(SnakeVfxController.Hook.GrowthSettle,
                                view.CellToWorld(a.To[1]), new Vector2(0f, 1f),
                                default(ClusterBurstView.Look));
                        }
                    }
                    else if (a.To.Count > 0)
                    {
                        Vfx.Signal(SnakeVfxController.Hook.MoveSettle, view.CellToWorld(a.To[0]),
                            Snap(new Vector2(a.To[0].X - a.To[1].X, a.To[0].Y - a.To[1].Y)),
                            default(ClusterBurstView.Look));
                    }
                    break;
                case Beat.Cut:
                {
                    // The segment that was cut does not vanish: it recoils off the new tail, turns
                    // a couple of degrees, shrinks and fades, with a few flecks.
                    int last = a.From.Count - 1;
                    Vector2 away = last > 0
                        ? Snap(new Vector2(a.From[last].X - a.From[last - 1].X,
                            a.From[last].Y - a.From[last - 1].Y))
                        : new Vector2(0f, -1f);
                    Release(a.RemovedTail, Piece.Tail, TurnForFacing(away), away, false);
                    // The tail's own breath left where it was, then the flecks of the release.
                    Vfx.Afterimage(SpriteOf(Piece.Tail), view.CellToWorld(a.RemovedTail),
                        cellSize * Style.SegmentOverscale, TurnForFacing(away));
                    Vfx.Signal(SnakeVfxController.Hook.TailDetach,
                        view.CellToWorld(a.RemovedTail), away, default(ClusterBurstView.Look));
                    CopyInto(body, a.To);
                    newTailClock = 0.18f;
                    newTailAway = away;
                    if (a.To.Count > 0)
                    {
                        Vfx.Signal(SnakeVfxController.Hook.NewTailSettled,
                            view.CellToWorld(a.To[a.To.Count - 1]), away,
                            default(ClusterBurstView.Look));
                    }
                    break;
                }
                case Beat.Defeat:
                    // Only now: it has been the subject of the animation until this moment.
                    body.Clear();
                    if (a.From.Count > 0)
                    {
                        Vfx.Signal(SnakeVfxController.Hook.DefeatComplete,
                            view.CellToWorld(a.From[0]), new Vector2(0f, 1f),
                            default(ClusterBurstView.Look));
                    }
                    break;
            }
        }

        // =================================================================== painting the snake

        /// <summary>How far the whole body's own flexing is turned down on a long snake.</summary>
        private float Lod
        {
            get
            {
                return body.Count >= Style.LodLongLength ? Style.LodLongAmplitude : 1f;
            }
        }

        private void PaintFrame()
        {
            Vfx.Begin(view, body.Count);
            if (body.Count == 0)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    Hide(segments[i]);
                }
                Vfx.End(false);
                PaintDebug(0f, null);
                return;
            }
            // ---- the spine: the path in front (only while sliding), the body behind ----
            float headArc = 0f;
            float slide = 1f;
            pathBuffer.Clear();
            behindBuffer.Clear();
            if (act != null && act.Kind == Beat.Move && act.Clock > 0f && Layers.ShowSnakeMotion)
            {
                for (int i = 0; i < act.Path.Count; i++)
                {
                    pathBuffer.Add(act.Path[i]);
                }
                for (int i = 1; i < act.From.Count; i++)
                {
                    behindBuffer.Add(view.CellToWorld(act.From[i]));
                }
                // ONE CELL OF SPARE TRACK behind the tail. A bite parks the head at the block's
                // face, so until it swallows its way in the whole body rides nearly a cell behind
                // the cells it ends on - and a spine only as long as those cells leaves the last
                // segment's arc past its end, where `arc > limit` drops it. That segment is the
                // one a growing bite just added, so the snake looked like it bit and went back to
                // its old length, with the tail popping in on the final frame.
                ExtendBehind(pathBuffer, behindBuffer);
                spine.Build(pathBuffer, behindBuffer);
                float pathLength = Mathf.Max((act.Path.Count - 1) * cellSize, 0.0001f);
                slide = SlideProgress(act);
                headArc = pathLength * (1f - slide);
                // A bite parks the head a quarter cell short and finishes the last of it by hand.
                headArc += BiteHeadOffset(act);
                if (!act.Eaten.HasValue)
                {
                    // It carries a hair past the cell it stops in and settles back - a body with
                    // weight, not a tile that snaps.
                    float settle = act.Clock - (act.Length - Style.MoveSettleDuration);
                    if (settle > 0f)
                    {
                        headArc = -Style.MoveSettleAmount * cellSize
                            * (1f - Smooth(settle / Mathf.Max(Style.MoveSettleDuration, 0.001f)));
                    }
                }
            }
            else
            {
                for (int i = 0; i < body.Count; i++)
                {
                    behindBuffer.Add(view.CellToWorld(body[i]));
                }
                spine.Build(behindBuffer, pathBuffer);
                headArc = StaticHeadOffset();
            }
            int count = act != null && act.Kind == Beat.Move
                ? Mathf.Max(act.From.Count, act.To.Count) : body.Count;
            // A GROWING BITE gains its segment as the snake SWALLOWS, not when the act begins:
            // until then the body is the length it was, and the piece at the end of it is still
            // the tail. Drawn from the first frame instead, the extra segment had nowhere to be
            // but a cell behind the old tail that the snake had never been in.
            bool growingIn = act != null && act.Kind == Beat.Move && act.Grew
                && act.To.Count > act.From.Count;
            if (growingIn && GrowthShare(act) <= 0f)
            {
                count = act.From.Count;
            }
            float limit = spine.Length + cellSize * 0.02f;
            EnsureSegments(count);
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                float arc = headArc + i * cellSize;
                if (growingIn && i == count - 1)
                {
                    // Pinned: the cell the growth keeps is the cell the body already ends in, and
                    // it does not move. Everything else flows forward past it.
                    arc = (count - 1) * cellSize;
                }
                if (arc > limit)
                {
                    break;
                }
                drawn++;
                PaintSegment(i, count, arc, slide, dt);
            }
            for (int i = drawn; i < segments.Count; i++)
            {
                Hide(segments[i]);
            }
            bool moving = act != null && act.Kind == Beat.Move && act.Clock > 0f && slide < 1f;
            Vfx.End(moving);
            PaintProxy(headArc);
            PaintDebug(headArc, spine);
        }

        /// <summary>Carries the spine one cell past the tail, straight on in whatever direction
        /// the body's last two points give. It is track, not a segment: nothing is ever drawn
        /// there, it is only there so a body that is still moving in cannot run off the end.</summary>
        private static void ExtendBehind(List<Vector2> path, List<Vector2> behind)
        {
            Vector2 last;
            Vector2 prev;
            if (behind.Count >= 2)
            {
                last = behind[behind.Count - 1];
                prev = behind[behind.Count - 2];
            }
            else if (behind.Count == 1 && path.Count >= 1)
            {
                last = behind[0];
                prev = path[path.Count - 1];
            }
            else if (behind.Count == 0 && path.Count >= 2)
            {
                last = path[path.Count - 1];
                prev = path[path.Count - 2];
            }
            else
            {
                return;
            }
            Vector2 step = last - prev;
            if (step.sqrMagnitude < 1e-8f)
            {
                return;
            }
            behind.Add(last + step);
        }

        /// <summary>The slide's own curve: a small release, a carry, a deceleration, and a settle a
        /// hair past the cell it stops in.</summary>
        private float SlideProgress(Act a)
        {
            float move = Mathf.Max(a.Length - Style.MoveSettleDuration
                - (a.Eaten.HasValue ? SnakeEatView.Style.Total : 0f), 0.02f);
            float u = Mathf.Clamp01(a.Clock / move);
            return Eased(u, Style.MoveEaseIn, Style.MoveEaseOut);
        }

        /// <summary>The head's own pressure while nothing is playing (idle) or while it is shoving
        /// at something it cannot pass (stuck), as an arc offset: negative is forward.</summary>
        private float StaticHeadOffset()
        {
            if (act != null && act.Kind == Beat.Stuck)
            {
                float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(act.Clock / act.Length));
                return -Style.StuckHeadPressure * cellSize * k;
            }
            if (act != null)
            {
                return 0f;
            }
            float breath = Mathf.Sin(idleClock * 1.7f) * 0.5f + 0.5f;
            return -Style.IdleHeadPressureAmount * cellSize * breath * Lod;
        }

        /// <summary>THE BITE'S OWN CLOCK: zero the moment the slide ends and the bite begins,
        /// which is what SnakeEatView's whole timeline is measured from. Negative while the snake
        /// is still sliding up to the block.</summary>
        private float BiteClock(Act a)
        {
            if (a == null || !a.Eaten.HasValue)
            {
                return -1f;
            }
            return a.Clock - (a.Length - SnakeEatView.Style.Total - Style.MoveSettleDuration);
        }

        /// <summary>Where the head is through a bite, as an arc offset from the cell it eats. The
        /// shape of it belongs to SnakeEatView, so the head, the jaw and the block's matter cannot
        /// come apart: it parks at the block's own face, coils, strikes over its near half, and
        /// pulls itself the rest of the way in as it swallows.</summary>
        private float BiteHeadOffset(Act a)
        {
            if (!a.Eaten.HasValue)
            {
                return 0f;
            }
            float t = BiteClock(a);
            float parked = Style.BiteLungeDistance;
            if (t <= 0f)
            {
                return parked * cellSize * SlideProgress(a);
            }
            return SnakeEatView.HeadOffset(t, parked) * cellSize;
        }

        /// <summary>Which head the bite is wearing right now - the bite's clock decides, not this
        /// file, so the sprite and the matter leaving the block agree.</summary>
        private bool MouthIsOpen(Act a)
        {
            if (a == null || !a.Eaten.HasValue)
            {
                return false;
            }
            float t = BiteClock(a);
            // Halfway through the opening, so the topology crossfade straddles it rather than the
            // open mouth appearing on one frame.
            return t >= SnakeEatView.Style.MouthOpenStart
                    + SnakeEatView.Style.MouthOpenDuration * 0.4f
                && t < SnakeEatView.Style.MouthCloseStart
                    + SnakeEatView.Style.MouthCloseDuration * 0.6f;
        }

        private void PaintSegment(int index, int count, float arc, float slide, float dt)
        {
            Segment seg = segments[index];
            Vector2 at = spine.At(arc);
            Vector2 raw = at;
            // ---- which piece, and which way round ----
            Piece piece;
            int turn;
            // Set only where a piece may point somewhere the quarter grid cannot: the tail.
            float freeAngle = float.NaN;
            Vector2 towardHead = spine.Toward(arc);
            if (index == 0)
            {
                piece = MouthIsOpen(act) ? Piece.HeadOpen : Piece.Head;
                turn = TurnForFacing(Snap(towardHead));
            }
            else if (index == count - 1)
            {
                // A tail is a cone with ONE open end, so it must NOT take its direction from the
                // tangent at its OWN arc: that is the end of the polyline, where a tail just past
                // a corner gets a tangent across the turn, points off into nothing and leaves a
                // gap. The tangent HALFWAY to its neighbour is the direction the body is running
                // as it enters the tail, which is the one that meets the neighbour's mouth whether
                // that neighbour is a straight piece or an elbow.
                piece = Piece.Tail;
                Vector2 chord = -spine.Toward(arc - 0.5f * cellSize);
                turn = TurnForFacing(Snap(chord));
                // AND ITS ANGLE IS NOT SNAPPED. A cone has one open end and one neighbour; it owes
                // nothing to the quarter grid the tiling pieces need. The tangent at its OWN arc
                // is the limb direction along a straight run and the BISECTOR on a corner - and
                // the corner is the case that matters, because no elbow is ever drawn there: a
                // bend is only offered to the middle segments, so a tail sitting on the corner met
                // the body at a right angle. That butt joint is the "foot". Unsnapped it lies
                // along the elbow instead, mouth still meeting the piece in front of it.
                Vector2 back = -spine.Toward(arc);
                if (back.sqrMagnitude < 1e-8f)
                {
                    back = chord;
                }
                if (back.sqrMagnitude > 1e-8f)
                {
                    freeAngle = Mathf.Atan2(back.y, back.x) * Mathf.Rad2Deg;
                }
            }
            else
            {
                Vector2 corner;
                float distance;
                Vector2 into;
                Vector2 outOf;
                int elbow = 0;   // the && below may short-circuit before it is written
                // An elbow is pointed by the CORNER'S OWN limbs, and it is drawn only while the
                // segment is genuinely at the corner: it stays where the spine puts it (snapping
                // it would open a gap to its neighbours), so the further it is from the corner the
                // further its two mouths are out of line with them - at half a cell that is half
                // a tube, which reads as a step in the body rather than as a bend.
                bool bend = spine.NearestCorner(arc, cellSize * Style.BendWindow, out corner,
                        out distance, out into, out outOf)
                    && TryTurnForBend(Snap(-into), Snap(outOf), out elbow);
                if (bend)
                {
                    piece = Piece.Bend;
                    turn = elbow;
                }
                else
                {
                    piece = index == swollenIndex && SwollenShare() > 0f ? Piece.Swollen : Piece.Body;
                    turn = Mathf.Abs(Snap(towardHead).x) > 0.5f ? 0 : 1;
                }
            }
            // ---- the crossfade, so a piece never pops ----
            if (piece != seg.Piece || turn != seg.Turn)
            {
                if (seg.Main.sprite != null && seg.Main.color.a > 0.01f)
                {
                    seg.FadePiece = seg.Piece;
                    seg.FadeTurn = seg.Turn;
                    seg.FadeAt = seg.Main.transform.localPosition;
                    // Becoming a TAIL is the one change the player is meant to notice, so it has a
                    // length of its own: this is the snake visibly getting shorter.
                    seg.FadeLength = piece == Piece.Tail
                        ? Style.NewTailMorphDuration : Style.TopologyCrossfadeDuration;
                    seg.FadeClock = seg.FadeLength;
                }
                seg.Piece = piece;
                seg.Turn = turn;
            }
            // ---- how big, and how bright ----
            float grow = 1f;
            float cross = 1f;
            float along = 1f;
            float alpha = 1f;
            float drain = 0f;
            if (act != null && act.Kind == Beat.Spawn)
            {
                float delay = (count - 1 - index) * Style.SpawnSegmentDelay;
                float k = Smooth((act.Clock - delay)
                    / Mathf.Max(act.Length - delay - 0.05f, 0.05f));
                grow = Mathf.Lerp(Style.SpawnStartScale, 1f, k);
                drain = Style.SpawnBrightnessStart * (1f - k);
                alpha = Mathf.Clamp01(k * 3f);
                if (index == 0)
                {
                    at += Snap(towardHead) * (Style.SpawnHeadSettleAmount * cellSize
                        * Mathf.Sin(Mathf.PI * k));
                }
            }
            else if (act != null && act.Kind == Beat.Move)
            {
                if (act.Grew && act.To.Count > act.From.Count && index == count - 1)
                {
                    // The new segment arriving: it comes up to size as the block goes down.
                    float born = GrowthShare(act);
                    grow = Mathf.Lerp(Style.GrowthSegmentStartScale, 1f, born);
                    alpha = Mathf.Clamp01(born * 2.5f);
                }
                // A contraction running down the body while it slides: a percent or two.
                float wave = Mathf.Sin((act.Clock * Style.MoveBodyWaveSpeed - index) * 0.9f);
                cross += Style.MoveBodyWaveStrength * Lod * Mathf.Max(wave, 0f);
                along -= Style.MoveBodyWaveStrength * 0.5f * Lod * Mathf.Max(wave, 0f);
                if (index == 0)
                {
                    float lead = Mathf.Sin(Mathf.PI * Mathf.Clamp01(act.Clock / 0.12f));
                    at += Snap(towardHead) * (Style.MoveHeadLeadAmount * cellSize * lead);
                }
            }
            else if (act != null && act.Kind == Beat.Defeat)
            {
                // THE BOSS IS BEATEN, and this is the half of it the BODY tells.
                float dt2 = act.Clock;
                // 1. the last tightening: something under pressure, two or three percent.
                cross += SnakeDefeatView.Tension(dt2);
                along -= SnakeDefeatView.Tension(dt2) * 0.4f;
                // 2. its own teal and cream going quiet - not to grey, just enough to make room
                //    for what is coming up underneath.
                drain = Mathf.Max(drain, SnakeDefeatView.Drain(dt2));
                // 3. and folding in, UNEVENLY: the ends before the middle.
                if (SnakeDefeatView.Layers.ShowCollapse)
                {
                    grow = Mathf.Clamp01(1f - SnakeDefeatView.Collapse(dt2, index, count));
                    alpha = Mathf.Clamp01(grow * 2.6f);
                }
            }
            else if (act != null && act.Kind == Beat.Stuck)
            {
                if (index < Style.StuckBodyWaveLength)
                {
                    float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(act.Clock / act.Length));
                    float share = 1f - index / (float)Mathf.Max(Style.StuckBodyWaveLength, 1);
                    // The surface answers the shove as well as the shape does.
                    float surface = 1f + SnakeVfxController.Style.StuckSurfaceCompressionResponse;
                    cross += Style.StuckBodyWaveStrength * share * k * surface;
                    along -= Style.StuckBodyWaveStrength * 0.5f * share * k;
                }
            }
            else if (act != null && act.Kind == Beat.Cut)
            {
                cross -= CutSqueezeAt(index, count);
                along += CutSqueezeAt(index, count) * 0.4f;
                // The last two segments: the root tightens and the one that is going narrows with
                // it, so the release is something the body DOES rather than a deletion.
                float release = act.Clock - (act.Length - Style.TailConstrictDuration
                    - Style.TailDetachDuration);
                if (release > 0f && index >= count - 2)
                {
                    float k = Mathf.Clamp01(release / Mathf.Max(Style.TailConstrictDuration, 0.001f));
                    cross -= Style.CutSignalCompression * 1.4f * k * (index == count - 1 ? 1f : 0.55f);
                }
            }
            else if (act == null && Layers.ShowSnakeMotion)
            {
                // IDLE: one low wave down the body, and nothing per-segment.
                float period = Mathf.Lerp(Style.IdleWavePeriodMin, Style.IdleWavePeriodMax,
                    (Hash(body.Count, 17) & 255u) / 255f);
                float phase = idleClock / Mathf.Max(period, 0.1f) - index * Style.IdleWaveSegmentDelay;
                // ALWAYS MOVING. Half-rectifying this (max(sin, 0), then squared) parked every
                // segment at exactly zero for most of the cycle: the wave was there, and the
                // snake still stood dead still for seconds at a time. Smoothstepped instead, so
                // the peak still reads as a bulge travelling down the body but nothing ever stops.
                float pulse = 0.5f + 0.5f * Mathf.Sin(phase * 6.2831853f);
                pulse = pulse * pulse * (3f - 2f * pulse);
                cross += Style.IdleCrossScaleAmount * pulse * Lod;
                along -= Style.IdleLongScaleAmount * pulse * Lod;
                if (index == count - 1)
                {
                    at += Snap(-towardHead) * (Style.IdleTailSwayAmount * cellSize * pulse);
                }
            }
            // The gulp's pressure, and the swelling that follows it.
            if (index == 0 && act != null && act.Kind == Beat.Move && act.Eaten.HasValue)
            {
                float gulp = GulpShare(act);
                cross += Style.GulpStrength * gulp;
                along += Style.GulpStrength * 0.5f * gulp;
            }
            if (index == swollenIndex)
            {
                float share = SwollenShare();
                cross += Style.SwollenOvershoot * share;
                along += Style.SwollenOvershoot * 0.4f * share;
            }
            float size = cellSize * Style.SegmentOverscale * grow;
            Vector2 axis = Snap(towardHead);
            bool horizontal = Mathf.Abs(axis.x) > 0.5f;
            float w = size * (horizontal ? along : cross);
            float h = size * (horizontal ? cross : along);
            Color tint = drain > 0f
                ? Color.Lerp(Color.white, new Color(0.55f, 0.6f, 0.58f), drain) : Color.white;
            tint.a = alpha;
            Place(seg.Main, SpriteOf(seg.Piece), at, w, h, seg.Turn, tint,
                index == 0 ? HeadOrder : BodyOrder);
            // THE TWO ENDS TURN; THE TILES IN BETWEEN DO NOT. A body or bend tile lands its mouths
            // on cell boundaries because its pivot is on a cell centre AND it sits on the quarter
            // grid - rotate it off that grid and the body opens a notch at every joint. A straight
            // never needs to sweep a quarter anyway: it becomes an elbow and then a straight the
            // other way, so what it needs is a clean SPRITE change, which is the crossfade's job.
            // The head is one shape and the tail is a cone with a single mouth, so neither owes
            // the grid anything, and those were the two that were visibly snapping.
            bool turns = index == 0 || index == count - 1;
            if (turns)
            {
                float target = float.IsNaN(freeAngle) ? 90f * (seg.Turn & 3) : freeAngle;
                float speed = 90f * dt / Mathf.Max(index == 0
                    ? Style.HeadTurnDuration : Style.BodyTurnDuration, 0.001f);
                if (!seg.HasAngle)
                {
                    seg.Angle = target;
                    seg.HasAngle = true;
                }
                else
                {
                    seg.Angle = Mathf.MoveTowardsAngle(seg.Angle, target, speed);
                }
                if (index == 0)
                {
                    headAngle = seg.Angle;
                }
                seg.Main.transform.localRotation = Quaternion.Euler(0f, 0f, seg.Angle);
            }
            else
            {
                seg.HasAngle = false;
            }
            if (index == count - 1 && newTailClock > 0f)
            {
                // It has just become the tail: a pixel of recoil, settling.
                float k = Mathf.Clamp01(newTailClock / 0.18f);
                seg.Main.transform.localPosition += (Vector3)(newTailAway
                    * (Style.NewTailSettleAmount * cellSize * k));
            }
            // ---- the piece it is coming from, fading out on top of the new one ----
            if (seg.FadeClock > 0f)
            {
                seg.FadeClock -= dt;
                float k = Mathf.Clamp01(seg.FadeClock / Mathf.Max(seg.FadeLength, 0.001f));
                Color old = Color.white;
                old.a = k * alpha;
                Place(seg.Fade, SpriteOf(seg.FadePiece), seg.FadeAt, w, h, seg.FadeTurn, old,
                    index == 0 ? HeadOrder : BodyOrder);
            }
            else
            {
                seg.Fade.color = Clear;
            }
            // ---- and the other half of the same event: surface, shadow, what the board feels ----
            PaintVfx(seg, index, count, at, w, h, axis, alpha, drain, cross, along, slide);
            if (Layers.ShowSnakeVisualProxyPositions && raw != at)
            {
                Mark(raw, cellSize * 0.18f, new Color(0.4f, 0.9f, 1f, 0.9f));
            }
        }

        /// <summary>
        /// One segment's pose handed to the VFX layer, with everything it needs to answer it: how
        /// hard it is squeezed, how much of a leading edge it carries, where a travelling band is,
        /// how drained it is - and the beats that belong to this segment (a wake passing it, the
        /// head coming round, the gulp reaching it, the cut's signal crossing it).
        /// </summary>
        private void PaintVfx(Segment seg, int index, int count, Vector2 at, float w, float h,
            Vector2 axis, float alpha, float drain, float cross, float along, float slide)
        {
            bool moving = act != null && (act.Kind == Beat.Move || act.Kind == Beat.Stuck);
            var pose = new SnakeVfxController.SegmentPose
            {
                Renderer = seg.Main,
                At = at,
                Width = w,
                Height = h,
                Piece = PartOf(seg.Piece),
                Index = index,
                Count = count,
                Facing = axis,
                Alpha = alpha,
                Moving = moving,
                Drain = drain,
                Band = -1f,
                // A squeeze only reads if the surface answers it: 8% is as hard as this snake
                // is ever squeezed, so that is the top of the scale.
                Compression = Mathf.Clamp01((Mathf.Max(1f - cross, 0f)
                    + Mathf.Max(1f - along, 0f)) / 0.08f)
            };
            if (act != null && act.Kind == Beat.Move && slide < 1f)
            {
                // The side it leads with lifts; the front of the body carries most of it.
                float pace = 1f - Smooth((slide - 0.72f) / 0.28f);
                pose.Lead = pace * (index == 0 ? 1f : Mathf.Max(0f, 0.6f - index * 0.08f));
                pose.Sheen = SnakeVfxController.Style.MoveSurfaceFlowStrength * 0.35f * pace
                    * Mathf.Max(0f, 1f - Mathf.Abs(index - slide * count * 0.6f) / 3f);
            }
            if (act != null && act.Kind == Beat.Spawn)
            {
                // The wake's own sheen, travelling with it.
                float delay = (count - 1 - index) * Style.SpawnSegmentDelay;
                float k = Mathf.Clamp01((act.Clock - delay)
                    / Mathf.Max(SnakeVfxController.Style.WakeSheenDuration, 0.01f));
                pose.Sheen += SnakeVfxController.Style.WakeSheenStrength
                    * Mathf.Sin(Mathf.PI * k) * (k > 0f ? 1f : 0f);
                pose.Drain = Mathf.Max(pose.Drain, SnakeVfxController.Style.WakeColorRecovery
                    * (1f - Smooth((act.Clock - delay) / Mathf.Max(act.Length - delay, 0.05f))));
                // Each segment coming awake is a small thing the board feels, tail first.
                int woken = count - 1 - Mathf.FloorToInt(act.Clock / Mathf.Max(Style.SpawnSegmentDelay, 0.001f));
                if (index >= woken && index > act.WokenTo - 1 && index == count - 1 - act.WokenTo)
                {
                    act.WokenTo++;
                    Vfx.Signal(index == 0 ? SnakeVfxController.Hook.WakeHead
                        : SnakeVfxController.Hook.WakeSegment, at, axis, default(ClusterBurstView.Look));
                }
            }
            if (index == 0 && act != null && act.Kind == Beat.Move)
            {
                // The head coming round to a new direction: the outer side of the turn lifts.
                if (act.LastFacing != Vector2.zero && !Same(Snap(act.LastFacing), Snap(axis)))
                {
                    Vfx.Signal(SnakeVfxController.Hook.MoveTurn, at, axis,
                        default(ClusterBurstView.Look));
                    turnSheen = SnakeVfxController.Style.MoveTurnHighlightStrength;
                }
                act.LastFacing = axis;
            }
            if (index == 0 && turnSheen > 0f)
            {
                pose.Sheen += turnSheen;
            }
            // THE BITE, in the surface: the head tightens and warms as the jaw lands, and the
            // neck behind it takes not quite half of it. Without this the bite is arcs and
            // flecks happening NEAR a head that does not react to them.
            if (jawImpact > 0f && index <= 1)
            {
                float hit = jawImpact * (index == 0 ? 1f : 0.45f);
                pose.Sheen += SnakeVfxController.Style.BiteContactSurfaceResponse * hit;
                pose.Accent = Mathf.Max(pose.Accent,
                    SnakeVfxController.Style.BiteContactAccentBoost * hit);
            }
            // THE GULP, and the CUT's signal: one soft band travelling along the body, and the
            // snake's own surface is the signal - there is no light going down it.
            float wave = BandIndex();
            if (wave >= 0f && Mathf.Abs(wave - index) < 0.8f)
            {
                pose.Band = Mathf.Clamp01(0.5f + (wave - index));
                pose.BandStrength = bandStrength * (1f - Mathf.Abs(wave - index) / 0.8f);
                pose.Accent = bandAccent * (1f - Mathf.Abs(wave - index) / 0.8f);
            }
            // THE SWALLOW, CARRYING THE COLOUR OF WHAT WAS EATEN. The packet passes the head,
            // then the neck, then the first body segment - each one a beat behind the last - and
            // at each it is a local bulge, a local highlight and a tint UNDER the skin, never a
            // segment painted the block's colour. This is what makes the bite end with "that went
            // into it" rather than with the block simply having stopped existing.
            if (act != null && act.Kind == Beat.Move && act.Eaten.HasValue && index == 1)
            {
                // The neck gathers behind the head's coil: the bite is something the body does,
                // not something the head does alone.
                cross += SnakeEatView.NeckGather(BiteClock(act));
            }
            if (act != null && act.Kind == Beat.Move && act.Eaten.HasValue && eat != null
                && index <= 2)
            {
                float gulpAt = BiteClock(act) - SnakeEatView.Style.GulpStart
                    - index * SnakeEatView.Style.GulpSegmentDelay;
                float through = Mathf.Clamp01(gulpAt
                    / Mathf.Max(SnakeEatView.Style.GulpDuration * 0.6f, 1e-4f));
                float here = gulpAt <= 0f ? 0f : Mathf.Sin(Mathf.PI * through);
                float ceiling = index == 0 ? SnakeEatView.Style.GulpHeadStrength
                    : index == 1 ? SnakeEatView.Style.GulpNeckStrength
                    : SnakeEatView.Style.GulpBodyStrength;
                float felt = here * ceiling;
                if (felt > 0.001f)
                {
                    cross += Style.GulpStrength * felt * SnakeEatView.Style.GulpCoreWidth * 3f;
                    pose.Sheen += 0.3f * felt;
                    pose.Accent = Mathf.Max(pose.Accent,
                        felt * SnakeEatView.Style.GulpColorStrength);
                    pose.AccentColour = eat.FoodColour;
                }
            }
            // THE COLOURS IT SWALLOWED, coming up from UNDER its own surface as it dies and
            // running to the middle of the piece. Through the skin material, so the artwork's
            // shading stays on top of them: a coloured sprite laid over the segment is a sticker,
            // and a sticker says nothing about the colour having been INSIDE.
            if (act != null && act.Kind == Beat.Defeat && beaten != null
                && SnakeDefeatView.Layers.ShowColorPockets)
            {
                float strength;
                float gathered;
                SnakeDefeatView.Pockets(act.Clock, out strength, out gathered);
                if (!SnakeDefeatView.Layers.ShowColorFlow)
                {
                    gathered = 0f;
                }
                IReadOnlyList<Color> swallowed = beaten.Colours;
                int pockets = Mathf.Min(SnakeDefeatView.Style.ColorPocketCount, 3);
                for (int p = 0; p < pockets && swallowed.Count > 0; p++)
                {
                    Color c = swallowed[(index + p) % swallowed.Count];
                    // Each one starts somewhere of its own on the piece and is drawn to the
                    // middle, so they arrive together instead of fading in place.
                    float a = (p * 2.3f + index * 0.7f);
                    Vector2 from = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.45f;
                    Vector2 pocketAt = Vector2.Lerp(from, Vector2.zero, gathered);
                    var packed = new Vector4(pocketAt.x, pocketAt.y,
                        SnakeDefeatView.Style.ColorPocketSoftness, strength);
                    if (p == 0)
                    {
                        pose.Pocket0 = c;
                        pose.PocketAt0 = packed;
                    }
                    else if (p == 1)
                    {
                        pose.Pocket1 = c;
                        pose.PocketAt1 = packed;
                    }
                    else
                    {
                        pose.Pocket2 = c;
                        pose.PocketAt2 = packed;
                    }
                }
                pose.ShadowFade = SnakeDefeatView.ShadowLeft(act.Clock);
            }
            if (index == swollenIndex)
            {
                // A swollen segment's surface reads taut while it holds - and keeps a trace of
                // the colour that made it swell, which then goes out.
                pose.Sheen += SnakeVfxController.Style.SwollenSurfaceTension * SwollenShare();
                pose.Sheen += SnakeEatView.Style.SwollenSurfaceTension * 0.4f * SwollenShare();
                // Up past its size and back: 100 -> 107 -> 103 -> 100, as a bulge and not as a
                // sprite scale.
                float over = 1f - Mathf.Clamp01(swollenClock
                    / Mathf.Max(SnakeEatView.Style.SwollenRelaxDuration, 1e-4f));
                cross += SnakeEatView.Style.SwollenOvershoot * SwollenShare() * over;
                if (eat != null)
                {
                    float residue = SnakeEatView.Style.SwollenColorResidue * SwollenShare();
                    if (residue > pose.Accent)
                    {
                        pose.Accent = residue;
                        pose.AccentColour = eat.FoodColour;
                    }
                }
            }
            if (index == count - 1 && newTailClock > 0f)
            {
                pose.Sheen += SnakeVfxController.Style.NewTailSurfaceResponse
                    * Mathf.Clamp01(newTailClock / 0.18f);
            }
            Vfx.Paint(pose);
        }

        /// <summary>Which part the VFX layer is answering - it treats a head, a corner and a tail
        /// differently, and a swollen segment differently again.</summary>
        private static SnakeVfxController.Part PartOf(Piece piece)
        {
            switch (piece)
            {
                case Piece.Head:
                case Piece.HeadOpen:
                    return SnakeVfxController.Part.Head;
                case Piece.Bend:
                    return SnakeVfxController.Part.Bend;
                case Piece.Tail:
                    return SnakeVfxController.Part.Tail;
                case Piece.Swollen:
                    return SnakeVfxController.Part.Swollen;
                default:
                    return SnakeVfxController.Part.Body;
            }
        }

        /// <summary>The turn's own highlight, which dies away over a tenth of a second.</summary>
        private float turnSheen;

        /// <summary>How long the jaw's landing stays in the head's surface.</summary>
        private const float JawImpactFall = 0.17f;

        private float jawImpact;

        /// <summary>Where a travelling band is along the body right now, in segments from the head,
        /// or -1 when nothing is passing. The gulp runs head to neck; the cut's constriction runs
        /// from where the line crossed to the tail.</summary>
        private float bandStrength;

        private float bandAccent;

        private float BandIndex()
        {
            bandStrength = 0f;
            bandAccent = 0f;
            if (act == null)
            {
                return -1f;
            }
            if (act.Kind == Beat.Move && act.Eaten.HasValue)
            {
                float gulp = GulpShare(act);
                if (gulp > 0f)
                {
                    bandStrength = SnakeVfxController.Style.GulpSurfaceResponse * gulp;
                    bandAccent = SnakeVfxController.Style.GulpAccentBoost * gulp;
                    // From the head into the neck, no further: a swallow is a short trip.
                    return Mathf.Lerp(0f, 2.2f, 1f - gulp);
                }
                return -1f;
            }
            if (act.Kind == Beat.Cut)
            {
                float impact = 0.06f;
                float travel = Mathf.Clamp01((act.Clock - impact)
                    / Mathf.Max(Style.CutSignalTravelDuration, 0.01f));
                if (travel <= 0f || travel >= 1f)
                {
                    return -1f;
                }
                int trigger = Mathf.Max(IndexOf(act.From, act.Trigger), 0);
                bandStrength = SnakeVfxController.Style.CutSignalMaterialStrength;
                bandAccent = SnakeVfxController.Style.CutSignalAccentBoost;
                return Mathf.Lerp(trigger, act.From.Count - 1, travel);
            }
            return -1f;
        }

        /// <summary>The constriction the player's line sent down the body: where it is now, and how
        /// hard it squeezes this segment.</summary>
        private float CutSqueezeAt(int index, int count)
        {
            if (act == null || act.Kind != Beat.Cut)
            {
                return 0f;
            }
            int trigger = IndexOf(act.From, act.Trigger);
            if (trigger < 0)
            {
                trigger = 0;
            }
            float impact = 0.06f;
            if (act.Clock < impact)
            {
                // The moment it is crossed: every segment the line touched answers, and nothing else.
                bool onLine = OnSameLine(act.From, index, act.Trigger);
                float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(act.Clock / impact));
                // The skin of the segment the line crossed answers hardest.
                return onLine ? Style.CutIntersectionResponse * k
                    * (1f + SnakeVfxController.Style.CutImpactSurfaceResponse) : 0f;
            }
            float travel = Mathf.Clamp01((act.Clock - impact) / Mathf.Max(Style.CutSignalTravelDuration, 0.01f));
            if (!act.SaidPinch && travel >= 1f && act.From.Count > 1)
            {
                act.SaidPinch = true;
                Vfx.Signal(SnakeVfxController.Hook.TailPinch,
                    view.CellToWorld(act.From[act.From.Count - 2]), new Vector2(0f, 1f),
                    default(ClusterBurstView.Look));
            }
            float front = Mathf.Lerp(trigger, count - 1, travel);
            float distance = Mathf.Abs(index - front);
            if (distance > 1.6f)
            {
                return 0f;
            }
            return Style.CutSignalCompression * (1f - distance / 1.6f);
        }

        private static bool OnSameLine(List<GridPos> cells, int index, GridPos trigger)
        {
            if (index < 0 || index >= cells.Count)
            {
                return false;
            }
            return cells[index].X == trigger.X || cells[index].Y == trigger.Y;
        }

        /// <summary>How far through the swallow the head is.</summary>
        private float GulpShare(Act a)
        {
            float k = GulpTravel(a);
            return k <= 0f || k >= 1f ? 0f : Mathf.Sin(Mathf.PI * k);
        }

        /// <summary>How far the swallow has travelled, 0 to 1 - the packet's position along the
        /// head, the neck and the first segment. Monotonic, unlike the pressure above, because a
        /// colour that travels has to keep going.</summary>
        private float GulpTravel(Act a)
        {
            if (a == null || !a.Eaten.HasValue)
            {
                return 0f;
            }
            return Mathf.Clamp01((BiteClock(a) - SnakeEatView.Style.GulpStart)
                / Mathf.Max(SnakeEatView.Style.GulpDuration, 1e-4f));
        }

        /// <summary>How much of the new segment is there. A growing bite gains it on the same
        /// curve that carries the head into the cell - the body gets longer AS the block goes
        /// down, not a frame after the act has ended.</summary>
        private float GrowthShare(Act a)
        {
            if (a == null || a.Kind != Beat.Move || !a.Grew || !a.Eaten.HasValue)
            {
                return 0f;
            }
            return Smooth(GulpTravel(a));
        }

        /// <summary>How much of the swelling is showing: in, held, out.</summary>
        private float SwollenShare()
        {
            if (swollenIndex < 0)
            {
                return 0f;
            }
            if (swollenClock < Style.SwollenAppearDuration)
            {
                return Smooth(swollenClock / Style.SwollenAppearDuration);
            }
            float after = swollenClock - Style.SwollenAppearDuration - Style.SwollenHoldDuration;
            if (after <= 0f)
            {
                return 1f;
            }
            return 1f - Smooth(after / Mathf.Max(Style.SwollenFadeDuration, 0.01f));
        }

        // =================================================================== the block it eats

        /// <summary>THE BLOCK IS NOT DRAWN HERE. It is handed to SnakeEatView, which takes its
        /// matter off it and draws that into the mouth - this method's whole job is to tell that
        /// view where the mouth IS this frame and which way it faces, and to fire the signals the
        /// VFX layer already answers. What used to be here was the old reading of the moment: the
        /// block shrunk, squeezed and slid into the mouth, which is "a tile being deleted with a
        /// tween on it" and is exactly what the bite is no longer allowed to look like.</summary>
        private void PaintProxy(float headArc)
        {
            if (act == null || act.Kind != Beat.Move || !act.Eaten.HasValue
                || act.EatenLook.Tile == null)
            {
                if (eat != null && eat.Playing)
                {
                    eat.Stop();
                }
                return;
            }
            float t = BiteClock(act);
            Vector2 axis = Snap(spine.Toward(headArc + 0.2f * cellSize));
            Vector2 cell = view.CellToWorld(act.Eaten.Value);
            Vector2 head = spine.At(headArc);
            Vector2 face = cell - axis * (cellSize * 0.5f);
            SnakeEatView bite = Eat();
            if (bite != null && !bite.Playing && t >= 0f)
            {
                bite.Begin(view, act.EatenLook, cell,
                    Hash(act.Eaten.Value.X, act.Eaten.Value.Y));
            }
            if (bite != null && bite.Playing)
            {
                bite.Paint(t, head, axis, Mathf.Min(Time.deltaTime, 0.05f));
            }
            // The VFX layer's own contact work, on the same clock as everything else.
            if (!act.SaidMouth && t >= SnakeEatView.Style.MouthOpenStart)
            {
                act.SaidMouth = true;
                Vfx.Signal(SnakeVfxController.Hook.MouthOpen, head, axis, act.EatenLook);
            }
            if (!act.SaidContact && t >= SnakeEatView.Style.LungeStart
                + SnakeEatView.Style.LungeDuration * 0.35f)
            {
                act.SaidContact = true;
                jawImpact = 1f;
                Vfx.Signal(SnakeVfxController.Hook.BiteContact, face, axis, act.EatenLook);
            }
            // The block going in is the BITE's business, not the contact layer's: its matter
            // leaves along filaments of its own colour, so there is nothing to announce here.

            if (act.Grew && swollenIndex < 0 && t >= SnakeEatView.Style.SwollenStart)
            {
                // The swelling is the gulp ARRIVING, so it starts on the bite's own clock rather
                // than a frame after the beat has finished.
                swollenIndex = 1;
                swollenClock = 0f;
            }
            if (!act.SaidGulp && GulpShare(act) > 0.5f)
            {
                act.SaidGulp = true;
                Vfx.Signal(SnakeVfxController.Hook.GulpPass,
                    spine.At(headArc + cellSize * 0.6f), axis, act.EatenLook);
            }
        }

        /// <summary>The defeat's own view, made the first time the boss is beaten.</summary>
        private SnakeDefeatView Beaten()
        {
            if (beaten == null && view != null)
            {
                var go = new GameObject("SnakeDefeat");
                go.transform.SetParent(view.transform, false);
                beaten = go.AddComponent<SnakeDefeatView>();
            }
            return beaten;
        }

        /// <summary>The bite's own view, made the first time one happens and kept after.</summary>
        private SnakeEatView Eat()
        {
            if (eat == null && view != null)
            {
                var go = new GameObject("SnakeEat");
                go.transform.SetParent(view.transform, false);
                eat = go.AddComponent<SnakeEatView>();
            }
            return eat;
        }

        // =================================================================== loose parts

        /// <summary>A segment that has let go: it recoils, turns a couple of degrees, shrinks and
        /// fades. Never a hard delete, and never gore.</summary>
        private void Release(GridPos cell, Piece piece, int turn, Vector2 away, bool collapse)
        {
            var l = new Loose
            {
                At = view.CellToWorld(cell),
                Drift = away * (Style.TailDetachDistance * cellSize),
                Piece = piece,
                Turn = turn,
                Life = collapse ? Style.DefeatCollapseDuration : Style.TailDetachDuration,
                Spin = collapse ? 0f : Style.TailDetachRotation,
                Collapse = collapse
            };
            l.Renderer = Rent(SpriteOf(piece), BodyOrder, SnakeVfxController.SkinMaterial);
            loose.Add(l);
        }

        private void PaintLoose(float dt)
        {
            for (int i = loose.Count - 1; i >= 0; i--)
            {
                Loose l = loose[i];
                l.Clock += dt;
                float k = Mathf.Clamp01(l.Clock / Mathf.Max(l.Life, 0.01f));
                float size = cellSize * Style.SegmentOverscale
                    * (l.Collapse ? Mathf.Lerp(1.04f, Style.DefeatCollapseScale, Smooth(k))
                        : Mathf.Lerp(1f, 0.75f, Smooth(k)));
                Vector2 at = l.At + l.Drift * Smooth(k);
                var tint = Color.white;
                tint.a = 1f - Smooth((k - 0.35f) / 0.65f);
                Place(l.Renderer, SpriteOf(l.Piece), at, size, size, l.Turn, tint, BodyOrder);
                // A part that has let go is drained of its colour as it goes, and its shadow
                // outlives it by a breath (the VFX layer's own contact pass).
                Vfx.Paint(new SnakeVfxController.SegmentPose
                {
                    Renderer = l.Renderer,
                    At = at,
                    Width = size,
                    Height = size,
                    Piece = PartOf(l.Piece),
                    Index = 0,
                    Count = 1,
                    Facing = Rotate(new Vector2(1f, 0f), l.Turn),
                    Alpha = tint.a,
                    Band = -1f,
                    Drain = (l.Collapse ? SnakeVfxController.Style.DefeatColorDrain : 0.35f) * Smooth(k),
                    Dim = (l.Collapse ? SnakeVfxController.Style.DefeatSurfaceDim : 0.2f) * Smooth(k),
                    Sheen = l.Collapse && k < 0.25f ? 0.5f * (1f - k / 0.25f) : 0f
                });
                if (l.Collapse && !l.SaidCollapse && k >= 0.5f)
                {
                    l.SaidCollapse = true;
                    Vfx.Signal(SnakeVfxController.Hook.DefeatCollapse, at, new Vector2(0f, 1f),
                        default(ClusterBurstView.Look));
                }
                if (l.Collapse && !l.SaidMote && k >= 0.85f)
                {
                    l.SaidMote = true;
                    Vfx.Signal(SnakeVfxController.Hook.DefeatMote, at, new Vector2(0f, 1f),
                        default(ClusterBurstView.Look));
                }
                if (l.Spin != 0f)
                {
                    l.Renderer.transform.localRotation = Quaternion.Euler(0f, 0f,
                        90f * l.Turn + l.Spin * Smooth(k));
                }
                if (k >= 1f + SnakeVfxController.Style.DefeatShadowDeathDuration
                    / Mathf.Max(l.Life, 0.01f))
                {
                    Return(l.Renderer);
                    loose.RemoveAt(i);
                }
            }
        }

        // =================================================================== the lab's markers

        private int markCount;

        private void PaintDebug(float headArc, Spine s)
        {
            markCount = 0;
            if (Layers.ShowSnakeTopology)
            {
                for (int i = 0; i < segments.Count && i < body.Count; i++)
                {
                    Mark(segments[i].Main.transform.localPosition, cellSize * 0.22f,
                        TopologyColour(segments[i].Piece));
                }
            }
            if (Layers.ShowSnakeHeadPath && act != null && act.Kind == Beat.Move)
            {
                for (int i = 1; i < act.Path.Count; i++)
                {
                    Line(act.Path[i - 1], act.Path[i], cellSize * 0.05f, DebugColour);
                }
            }
            if (Layers.ShowSnakeMovementDirection && s != null && body.Count > 0)
            {
                Vector2 head = s.At(headArc);
                Line(head, head + Snap(s.Toward(headArc + 0.2f * cellSize)) * cellSize * 0.7f,
                    cellSize * 0.07f, DebugColour);
            }
            if (Layers.ShowSnakeEatenCell && act != null && act.Eaten.HasValue)
            {
                Ring(view.CellToWorld(act.Eaten.Value), cellSize * 0.95f, DebugColour);
            }
            if (Layers.ShowSnakeCutTrigger && act != null && act.Kind == Beat.Cut)
            {
                Ring(view.CellToWorld(act.Trigger), cellSize * 0.95f, new Color(1f, 0.8f, 0.3f));
                Ring(view.CellToWorld(act.RemovedTail), cellSize * 0.8f, new Color(1f, 0.5f, 0.5f));
            }
            for (int i = markCount; i < debugMarks.Count; i++)
            {
                debugMarks[i].color = Clear;
            }
            PaintIndices();
        }

        private void PaintIndices()
        {
            if (!Layers.ShowSnakeBodyIndices)
            {
                for (int i = 0; i < debugLabels.Count; i++)
                {
                    if (debugLabels[i] != null)
                    {
                        debugLabels[i].gameObject.SetActive(false);
                    }
                }
                return;
            }
            for (int i = 0; i < body.Count && i < segments.Count; i++)
            {
                while (debugLabels.Count <= i)
                {
                    debugLabels.Add(ViewUtil.MakeText3D(transform, "SnakeIndex", Vector2.zero,
                        "", 90, cellSize * 0.012f, DebugColour, DebugOrder, TextAnchor.MiddleCenter));
                }
                TextMesh label = debugLabels[i];
                label.gameObject.SetActive(true);
                label.text = i.ToString();
                label.transform.localPosition = segments[i].Main.transform.localPosition;
            }
            for (int i = body.Count; i < debugLabels.Count; i++)
            {
                debugLabels[i].gameObject.SetActive(false);
            }
        }

        private static Color TopologyColour(Piece piece)
        {
            switch (piece)
            {
                case Piece.Head: return new Color(1f, 0.45f, 0.35f);
                case Piece.HeadOpen: return new Color(1f, 0.25f, 0.25f);
                case Piece.Tail: return new Color(0.45f, 0.8f, 1f);
                case Piece.Bend: return new Color(1f, 0.85f, 0.35f);
                case Piece.Swollen: return new Color(0.7f, 1f, 0.5f);
                default: return new Color(0.6f, 0.65f, 0.7f);
            }
        }

        private void Mark(Vector2 at, float size, Color colour)
        {
            SpriteRenderer r = NextMark();
            PlaceAxes(r, at, size, size, colour, 0.85f);
        }

        private void Ring(Vector2 at, float size, Color colour)
        {
            SpriteRenderer r = NextMark();
            r.sprite = RingSprite();
            PlaceAxes(r, at, size, size, colour, 0.8f);
        }

        private void Line(Vector2 a, Vector2 b, float width, Color colour)
        {
            SpriteRenderer r = NextMark();
            r.sprite = ViewUtil.WhiteSprite;
            Vector2 span = b - a;
            colour.a = 0.75f;
            r.color = colour;
            r.transform.localPosition = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, 0f);
            r.transform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(Mathf.Max(span.magnitude, 0.001f), width, 1f);
        }

        private SpriteRenderer NextMark()
        {
            while (debugMarks.Count <= markCount)
            {
                debugMarks.Add(Rent(DotSprite(), DebugOrder, null));
            }
            SpriteRenderer r = debugMarks[markCount++];
            r.sprite = DotSprite();
            r.enabled = true;
            return r;
        }

        // =================================================================== renderers

        private void EnsureSegments(int count)
        {
            while (segments.Count < count)
            {
                var seg = new Segment
                {
                    // The SKIN material: one for the whole snake, per-segment values through a
                    // property block (SnakeVfxController).
                    Main = Rent(null, BodyOrder, SnakeVfxController.SkinMaterial),
                    Fade = Rent(null, BodyOrder, SnakeVfxController.SkinMaterial),
                    Piece = Piece.Body,
                    Turn = 0
                };
                segments.Add(seg);
            }
        }

        private static void Hide(Segment seg)
        {
            seg.Main.color = Clear;
            seg.Fade.color = Clear;
            seg.FadeClock = 0f;
            // Whatever angle it was left at means nothing now: the next time it is used it starts
            // pointing the right way rather than spinning in from a pose nobody saw.
            seg.HasAngle = false;
        }

        /// <summary>One piece, on its cell centre (its pivot), turned in quarter steps.</summary>
        private static void Place(SpriteRenderer r, Sprite sprite, Vector2 at, float width,
            float height, int turn, Color tint, int order)
        {
            if (r == null)
            {
                return;
            }
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = tint;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * (turn & 3));
            // The art is one unit per CELL (its import sets pixels-per-unit to the cell), so the
            // scale here is the cell size, and the turn is what points it.
            bool swapped = (turn & 1) != 0;
            r.transform.localScale = new Vector3(swapped ? height : width,
                swapped ? width : height, 1f);
        }

        private static void PlaceAxes(SpriteRenderer r, Vector2 at, float width, float height,
            Color colour, float alpha)
        {
            if (r == null)
            {
                return;
            }
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.identity;
            Vector2 unit = r.sprite != null
                ? new Vector2(r.sprite.rect.width, r.sprite.rect.height) / r.sprite.pixelsPerUnit
                : Vector2.one;
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
                var go = new GameObject("Snake");
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
            r.color = Clear;
            spare.Push(r);
        }

        // =================================================================== art

        private static Sprite SpriteOf(Piece piece)
        {
            switch (piece)
            {
                case Piece.Head: return ViewUtil.SnakeTile(ViewUtil.SnakePiece.HeadClosed);
                case Piece.HeadOpen: return ViewUtil.SnakeTile(ViewUtil.SnakePiece.HeadOpen);
                case Piece.Bend: return ViewUtil.SnakeTile(ViewUtil.SnakePiece.Bend);
                case Piece.Tail: return ViewUtil.SnakeTile(ViewUtil.SnakePiece.Tail);
                case Piece.Swollen: return ViewUtil.SnakeTile(ViewUtil.SnakePiece.Swollen);
                default: return ViewUtil.SnakeTile(ViewUtil.SnakePiece.Body);
            }
        }

        private static Sprite dotSprite;

        private static Sprite ringSprite;

        private static Sprite DotSprite()
        {
            if (dotSprite == null)
            {
                dotSprite = Blob(32, 0.5f, 0.1f);
            }
            return dotSprite;
        }

        private static Sprite RingSprite()
        {
            if (ringSprite != null)
            {
                return ringSprite;
            }
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f;
                    float v = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.47f) / 0.02f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            ringSprite = MakeSprite(n, px);
            return ringSprite;
        }

        /// <summary>A soft round blot: radius as a share of the sprite, and how soft its edge is.</summary>
        private static Sprite Blob(int n, float radius, float softness)
        {
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f;
                    float v = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((radius - d) / Mathf.Max(softness, 0.001f));
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(a * a * 255f));
                }
            }
            return MakeSprite(n, px);
        }

        private static Sprite MakeSprite(int n, Color32[] px)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // =================================================================== maths

        /// <summary>A ramp in SPEED: it sets off, carries, and slows into its stop.</summary>
        private static float Eased(float u, float easeIn, float easeOut)
        {
            easeIn = Mathf.Clamp(easeIn, 0.01f, 0.9f);
            easeOut = Mathf.Clamp(easeOut, 0.01f, 0.98f - easeIn);
            float vmax = 1f / (1f - 0.5f * easeIn - 0.5f * easeOut);
            u = Mathf.Clamp01(u);
            if (u < easeIn)
            {
                float x = u / easeIn;
                return vmax * easeIn * (x * x * x - 0.5f * x * x * x * x);
            }
            float cruiseEnd = 1f - easeOut;
            if (u <= cruiseEnd)
            {
                return vmax * (0.5f * easeIn + (u - easeIn));
            }
            float y = (u - cruiseEnd) / easeOut;
            return vmax * (0.5f * easeIn + (cruiseEnd - easeIn)
                + easeOut * (y - (y * y * y - 0.5f * y * y * y * y)));
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static void CopyInto(List<GridPos> into, IReadOnlyList<GridPos> from)
        {
            if (into == from)
            {
                return;
            }
            into.Clear();
            if (from == null)
            {
                return;
            }
            for (int i = 0; i < from.Count; i++)
            {
                into.Add(from[i]);
            }
        }

        private static int IndexOf(List<GridPos> cells, GridPos cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].X == cell.X && cells[i].Y == cell.Y)
                {
                    return i;
                }
            }
            return -1;
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
    }
}
