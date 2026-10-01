// PURPOSE: "Simetri" PAYING - the board's symmetry RECOGNISED, its matched regions PAIRED one event
// at a time, the pairs LOCKED with a crystal "dışşınk", the whole thing RESONATING, and only then
// the reward, born in the middle of the arena and carried to the score. The first version lit every
// cube in turn and flashed the board: a highlight, not a recognition, and a "+40" that just
// appeared. This is what replaced it.
//
// SIX PHASES ON ONE CLOCK (~0.75-1.05 s for +40, ~1.0-1.35 s for +120):
//   A DETECT     a faint light runs round the arena's own edge for a tenth of a second: "seen".
//   B TRACE      each matched REGION (SymmetryChunks: medium regions, 2-6 events, never the whole
//                half and never cell by cell) is drawn round by an invisible pen - a bright head
//                running the region's boundary from the point nearest the axis, a soft glow
//                filling in behind it on the inside - and its partner a breath later, the mirror
//                drawn the mirror way round.
//   C CONNECT    energy leaves both regions for the axis (the centre, for a half turn, on two
//                arcs that are each other turned upside down) and meets there in a short crystal
//                LOCK - a four-point flare, a ring, a few sparks, a short pulse ALONG the axis -
//                with the "dışşınk" on that exact frame. The next pair 60-120 ms behind, so the
//                symmetry is seen SPREADING. A cell that is its own partner gets a self-lock.
//   D RESONANCE  every traced region brightens at once, the axis shows itself faintly end to end,
//                the arena settles one pixel - and for BOTH mirrors the second wave runs first and
//                four packets race in along the two axes to collide in the middle: the double
//                verification is a second event, not a brighter first one.
//   E PAYOUT     a warm gold ring opens in the middle of the board and "+40" / "+120" is born in
//                it (0.85 -> 1.08 -> 1, a little gold dust), held long enough to be read, then
//                drifts up while a gold thread carries a seed of it to the score, which answers.
//   F FADE       the traces are gone almost at once; the board is fully legible again.
//
// LIGHT, NOT PAINT. Everything is one premultiplied, mostly additive material (SymmetryGlow): the
// cubes under a trace keep their own colour and only brighten, and the palette is a cold cyan line
// on a lavender edge - a SYSTEM colour, chosen to be no block type's. Gold is kept for the reward.
// It looks at OCCUPANCY only, like the joker: nothing here knows or cares what a cube is made of.
// No shake (one pixel of settle), no smoke, no confetti, eight sparks a lock at the very most.
//
// THE VIEW DECIDES NOTHING: which symmetry held, which cells matched and what was paid are the
// joker's report (SymmetryVisuals, matched by identity); the grouping into events is SymmetryChunks.
// EXTENSION POINT: every length and strength is in Style; a new phase is a time in Schedule.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class SymmetryRewardView : MonoBehaviour
    {
        /// <summary>The brief's tuning (53), at 1x. Lengths are seconds, sizes are in CELLS.</summary>
        public static class Style
        {
            public static float DetectPause = 0.08f;
            public static float TraceDuration = 0.09f;
            public static float TraceStagger = 0.02f;      // the partner's trace, behind its region's
            public static float PairLockDuration = 0.17f;  // the lock flare's life
            public static float PairGap = 0.1f;            // between one pair and the next (it shrinks when there are many)
            public static float PairGapMin = 0.06f;
            public static float ConnectDuration = 0.07f;
            public static float ResonanceDuration = 0.09f;
            public static float ScorePopDuration = 0.12f;
            public static float ScoreHold = 0.18f;
            public static float HudTransferDuration = 0.2f;

            public static float TraceGlow = 0.95f;
            public static float ConnectGlow = 0.9f;
            public static float CenterPulse = 0.8f;
            public static float FinalGlow = 1f;
            public static float HoldGlow = 0.38f;          // a traced region while it waits for the rest
            public static float DetectGlow = 0.28f;

            public static int ChunkMaxCells = 4;
            public static int ChunkSplitLength = 4;        // a straight run longer than this is cut (the same number in practice)
            public static SymmetryChunks.OrderMode PairOrderMode = SymmetryChunks.OrderMode.Auto;
            public static int MaxEventsSingle = 5;
            public static int MaxEventsPerWave = 3;

            public static float LineWidth = 0.035f;        // the trace's own line, half width
            public static float OuterSoft = 0.07f;         // its soft outer rim
            public static float InnerGlow = 0.3f;          // how far the inner glow reaches in
            public static float CornerRadius = 0.2f;
            public static float Outset = 0.02f;            // how far outside the cells' edge the line runs

            // the system colours (no block type's) and the reward's gold
            public static Color TraceCore = new Color(0.72f, 0.96f, 1f);
            public static Color TraceEdge = new Color(0.62f, 0.56f, 1f);
            public static Color LockCore = new Color(0.86f, 0.97f, 1f);
            public static Color Gold = new Color(1f, 0.80f, 0.40f);
            public static Color Cream = new Color(1f, 0.92f, 0.68f);
            public static Color Muted = new Color(0.66f, 0.62f, 0.80f);

            public const int TraceOrder = 12;
            public const int BeamOrder = 13;
            public const int FlareOrder = 14;
            public const int PayoutOrder = 31;
        }

        /// <summary>The lab's debug overlays (brief 52).</summary>
        public static class DebugFlags
        {
            public static bool ShowMatchedPairs;
            public static bool ShowVisualChunks;
            public static bool ShowChunkOrder;
            public static bool ShowAxis;
            public static bool ShowAnchors;
            public static bool ShowType;

            public static bool Any
            {
                get { return ShowMatchedPairs || ShowVisualChunks || ShowChunkOrder || ShowAxis || ShowAnchors || ShowType; }
            }

            public static void AllOff()
            {
                ShowMatchedPairs = ShowVisualChunks = ShowChunkOrder = ShowAxis = ShowAnchors = ShowType = false;
            }
        }

        // ---- what the controller hands in
        /// <summary>A point in board coordinates (a cell's centre is its integer coordinate) to world.</summary>
        public Func<float, float, Vector2> BoardToWorld;
        /// <summary>One cell's edge in world units.</summary>
        public Func<float> CellSize;
        public Func<Vector2> ScoreAnchor;
        /// <summary>One screen pixel in world units.</summary>
        public Func<float> Pixel;
        /// <summary>The arena's own transform term (a pixel of settle - never the camera).</summary>
        public Action<Vector2> BoardImpulse;
        /// <summary>Every audio beat, with the pair's step on the ladder (0 for the others).</summary>
        public Action<SymmetryCue, int> Sounded;

        /// <summary>Playing, waiting to play, or the score still answering.</summary>
        public bool Busy
        {
            get { return playing != null; }
        }

        /// <summary>The report being played (or the last one, for the debug overlays).</summary>
        public SymmetryVisuals Current { get; private set; }

        /// <summary>The events of the last report, in the order they play (debug, lab).</summary>
        public readonly List<SymmetryChunkPair> Events = new List<SymmetryChunkPair>();

        /// <summary>Which phase it is in (debug).</summary>
        public string Phase { get; private set; }

        // ---- the score's answer (GameUiController.TickScoreResponse)
        public float ScoreClaim { get; private set; }
        public float ScoreScale { get; private set; }
        public Color ScoreInk { get; private set; }

        // ================================================================== pools

        private sealed class Strip
        {
            public GameObject Go;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public readonly List<Vector2> Points = new List<Vector2>();
            public readonly List<Vector2> Inward = new List<Vector2>();
            public readonly List<float> S = new List<float>();
            public float Length;
            public bool Outline;     // asymmetric rows (a region's boundary) vs a beam
            public bool Live;
        }

        private sealed class Spr
        {
            public SpriteRenderer R;
            public bool Live;
        }

        private sealed class Particle
        {
            public Spr S;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Age;
            public float Life;
            public float Size;
            public Color Colour;
            public float Spin;
            public bool Stretch;
        }

        private readonly List<Strip> strips = new List<Strip>();
        private readonly List<Spr> sprites = new List<Spr>();
        private readonly List<Particle> particles = new List<Particle>();
        private Material glowMaterial;
        private TextMesh scoreText;
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Color> cols = new List<Color>();
        private readonly List<int> tris = new List<int>();

        // ================================================================== a playback

        private sealed class PairFx
        {
            public SymmetryChunkPair Chunk;
            public readonly List<Strip> LoopsA = new List<Strip>();
            public readonly List<Strip> LoopsB = new List<Strip>();
            public Strip BeamA;
            public Strip BeamB;
            public Strip AxisPulse;
            public Vector2 Meet;
            public Vector2 CentreA;
            public Vector2 CentreB;
            public float T0;
            public float TLock;
            public bool Traced;
            public bool Locked;
            public int Step;
            public int Wave;
            public SymmetryKind Kind;
        }

        private sealed class Playback
        {
            public SymmetryVisuals Seen;
            public float Delay;
            public float T;
            public readonly List<PairFx> Pairs = new List<PairFx>();
            public Strip Frame;
            public readonly List<Strip> Axes = new List<Strip>();
            public readonly List<Strip> Packets = new List<Strip>();
            public float TCross = -1f;
            public float TWave2 = -1f;
            /// <summary>The whole matched shape's outline: the regions hand over to it at the
            /// resonance, so the board lights as ONE symmetric silhouette rather than a lattice of
            /// every region's own line (two waves' regions overlap, and drawn together they were a
            /// grid).</summary>
            public readonly List<Strip> Union = new List<Strip>();
            public float TRes;
            public float TPay;
            public float TArrive;
            public float TEnd;
            public Vector2 Centre;
            public Vector2 Pop;
            public Strip Thread;
            public bool Detected, Resonated, Crossed, Paid, Arrived;
            public bool Double;
            public bool HalfTurn;
            public bool Negative;
        }

        private Playback playing;
        private float cell = 0.9f;

        private void Awake()
        {
            Shader shader = Shader.Find("ProjectBlock/SymmetryGlow");
            if (shader == null)
            {
                shader = Resources.Load<Shader>("Shaders/SymmetryGlow");
            }
            if (shader == null || !shader.isSupported)
            {
                shader = Shader.Find("Sprites/Default");
            }
            glowMaterial = new Material(shader) { name = "SymmetryGlow (shared)" };
        }

        // ================================================================== play / stop

        /// <summary>Plays one payout, <paramref name="delay"/> seconds from now (the turn's own
        /// lines are seen through first). A new report replaces one still playing.</summary>
        public void Play(SymmetryVisuals seen, float delay, bool hideRegions = false)
        {
            if (seen == null || !seen.Any || BoardToWorld == null)
            {
                return;
            }
            Stop();
            Current = seen;
            cell = CellSize != null ? Mathf.Max(0.05f, CellSize()) : 0.9f;
            playing = Schedule(seen, Mathf.Max(0f, delay), hideRegions);
        }

        public void Stop()
        {
            if (playing != null && BoardImpulse != null && playing.T > playing.TRes - 0.06f)
            {
                BoardImpulse(Vector2.zero);
            }
            playing = null;
            foreach (Strip s in strips)
            {
                Release(s);
            }
            foreach (Spr s in sprites)
            {
                s.Live = false;
                s.R.enabled = false;
            }
            particles.Clear();
            if (scoreText != null)
            {
                scoreText.gameObject.SetActive(false);
            }
            ScoreClaim = 0f;
            ScoreScale = 1f;
            Phase = "-";
        }

        // ================================================================== the schedule

        private Playback Schedule(SymmetryVisuals seen, float delay, bool hideRegions)
        {
            var p = new Playback { Seen = seen, Delay = delay };
            p.Double = seen.BothMirrors;
            p.HalfTurn = !seen.LeftRight && !seen.TopBottom && seen.HalfTurn;
            p.Negative = seen.Points < 0;
            p.Centre = BoardToWorld(seen.AxisX, seen.AxisY);
            Events.Clear();

            var waves = new List<SymmetryKind>();
            if (seen.LeftRight)
            {
                waves.Add(SymmetryKind.LeftRight);
            }
            if (seen.TopBottom)
            {
                waves.Add(SymmetryKind.TopBottom);
            }
            if (waves.Count == 0)
            {
                waves.Add(SymmetryKind.HalfTurn);
            }
            // the frame of the arena for the detect beat
            p.Frame = ArenaFrame(seen);

            float t = Style.DetectPause;
            float lastLock = t;
            int step = 0;
            if (hideRegions)
            {
                // a blind round: tracing a region would show where its cubes are
                waves.Clear();
                lastLock = t + 0.18f;
            }
            for (int w = 0; w < waves.Count; w++)
            {
                SymmetryKind kind = waves[w];
                int budget = p.Double ? Style.MaxEventsPerWave : Style.MaxEventsSingle;
                // a half turn is harder to see than a mirror: its regions are cut less finely
                int extra = kind == SymmetryKind.HalfTurn ? 2 : 0;
                int maxCells = Mathf.Max(1, Mathf.Min(Style.ChunkMaxCells, Style.ChunkSplitLength) + extra);
                List<SymmetryChunkPair> events = SymmetryChunks.Build(seen, kind, budget, maxCells, Style.PairOrderMode);
                Events.AddRange(events);
                int n = events.Count;
                float gap = n <= 1 ? Style.PairGap : Mathf.Clamp(0.24f / (n - 1), Style.PairGapMin, Style.PairGap);
                if (p.Double)
                {
                    gap *= 0.8f;
                }
                // the second wave starts as the first is landing
                if (w > 0)
                {
                    t = lastLock - 0.07f;
                    p.TWave2 = t;
                }
                for (int i = 0; i < n; i++)
                {
                    PairFx fx = BuildPair(seen, events[i], kind, t + i * gap, step++);
                    fx.Wave = w;
                    p.Pairs.Add(fx);
                    lastLock = Mathf.Max(lastLock, fx.TLock);
                }
            }
            // the whole matched shape, for the resonance
            var all = new HashSet<GridPos>();
            foreach (SymmetryChunkPair e in Events)
            {
                foreach (GridPos c in e.A)
                {
                    all.Add(c);
                }
                foreach (GridPos c in e.B)
                {
                    all.Add(c);
                }
            }
            foreach (List<BoardPoint> loop in SymmetryChunks.Outline(all))
            {
                p.Union.Add(OutlineStrip(loop, p.Centre, false));
            }

            // D: resonance (and, for both mirrors, the cross collision first)
            if (p.Double)
            {
                p.TCross = lastLock + 0.04f;
                p.TRes = p.TCross + 0.1f;
                BuildCross(p, seen);
            }
            else
            {
                p.TRes = lastLock + 0.05f;
            }
            BuildAxes(p, seen);
            p.TPay = p.TRes + 0.04f;
            p.TArrive = p.TPay + Style.ScorePopDuration + Style.ScoreHold + Style.HudTransferDuration;
            p.TEnd = p.TArrive + 0.34f;
            return p;
        }

        private PairFx BuildPair(SymmetryVisuals seen, SymmetryChunkPair chunk, SymmetryKind kind, float t0, int step)
        {
            var fx = new PairFx { Chunk = chunk, T0 = t0, Step = step, Kind = kind };
            float cx = seen.AxisX;
            float cy = seen.AxisY;
            // the anchors: where each region's energy leaves for the axis, and where they meet
            SymmetryChunks.Anchors(seen, chunk, out BoardPoint ba, out BoardPoint bb, out BoardPoint bm);
            Vector2 a = new Vector2(ba.X, ba.Y);
            Vector2 b = new Vector2(bb.X, bb.Y);
            Vector2 meet = new Vector2(bm.X, bm.Y);
            Vector2 wa = BoardToWorld(a.x, a.y);
            Vector2 wb = BoardToWorld(b.x, b.y);
            fx.Meet = BoardToWorld(meet.x, meet.y);
            fx.CentreA = BoardToWorld(chunk.CentreAX, chunk.CentreAY);
            fx.CentreB = BoardToWorld(chunk.CentreBX, chunk.CentreBY);

            // B: the outlines, each started at its point nearest the anchor; the partner drawn the
            // mirror way round (a mirror turns a counter-clockwise loop clockwise; a half turn does not)
            bool mirrorWay = kind != SymmetryKind.HalfTurn;
            foreach (List<BoardPoint> loop in SymmetryChunks.Outline(chunk.A))
            {
                fx.LoopsA.Add(OutlineStrip(loop, wa, false));
            }
            if (!chunk.Self)
            {
                foreach (List<BoardPoint> loop in SymmetryChunks.Outline(chunk.B))
                {
                    fx.LoopsB.Add(OutlineStrip(loop, wb, mirrorWay));
                }
            }

            // C: the beams, unless the two regions already touch at the axis
            float traceEnd = t0 + Style.TraceStagger + Style.TraceDuration;
            bool beams = !chunk.Self && (wa - fx.Meet).magnitude > cell * 0.12f;
            if (beams)
            {
                fx.BeamA = PathStrip(SymmetryChunks.EnergyPath(ba, bm, kind == SymmetryKind.HalfTurn, 14));
                fx.BeamB = PathStrip(SymmetryChunks.EnergyPath(bb, bm, kind == SymmetryKind.HalfTurn, 14));
            }
            // the same inner timing with beams or without: the locks keep their even spacing (a
            // pair touching the axis used to lock 50 ms early, on top of its neighbour's "dışşınk")
            fx.TLock = traceEnd - Style.TraceDuration * 0.25f + Style.ConnectDuration;
            // the short pulse along the axis where the pair locks
            if (!chunk.Self && kind != SymmetryKind.HalfTurn)
            {
                Vector2 along = kind == SymmetryKind.LeftRight ? Vector2.up : Vector2.right;
                fx.AxisPulse = LineStrip(fx.Meet - along * cell * 0.75f, fx.Meet + along * cell * 0.75f);
            }
            return fx;
        }

        private static void Bounds(List<GridPos> cells, out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;
            foreach (GridPos c in cells)
            {
                minX = Mathf.Min(minX, c.X);
                maxX = Mathf.Max(maxX, c.X);
                minY = Mathf.Min(minY, c.Y);
                maxY = Mathf.Max(maxY, c.Y);
            }
        }

        /// <summary>The faint line round the arena's own edge (the detect beat).</summary>
        private Strip ArenaFrame(SymmetryVisuals seen)
        {
            float x0 = seen.MinX - 0.5f - 0.12f;
            float x1 = seen.MinX + seen.Width - 0.5f + 0.12f;
            float y0 = seen.MinY - 0.5f - 0.12f;
            float y1 = seen.MinY + seen.Height - 0.5f + 0.12f;
            var corners = new List<BoardPoint>
            {
                new BoardPoint(x0, y0), new BoardPoint(x1, y0), new BoardPoint(x1, y1), new BoardPoint(x0, y1)
            };
            return OutlineStrip(corners, BoardToWorld(seen.AxisX, y0), false, 0.45f);
        }

        /// <summary>The full axis lines of the resonance (and the half turn's ring is a sprite).</summary>
        private void BuildAxes(Playback p, SymmetryVisuals seen)
        {
            float y0 = seen.MinY - 0.5f;
            float y1 = seen.MinY + seen.Height - 0.5f;
            float x0 = seen.MinX - 0.5f;
            float x1 = seen.MinX + seen.Width - 0.5f;
            if (seen.LeftRight)
            {
                p.Axes.Add(LineStrip(BoardToWorld(seen.AxisX, y0), BoardToWorld(seen.AxisX, y1)));
            }
            if (seen.TopBottom)
            {
                p.Axes.Add(LineStrip(BoardToWorld(x0, seen.AxisY), BoardToWorld(x1, seen.AxisY)));
            }
        }

        /// <summary>Both mirrors: four packets race in along the two axes and collide in the middle.</summary>
        private void BuildCross(Playback p, SymmetryVisuals seen)
        {
            float y0 = seen.MinY - 0.5f;
            float y1 = seen.MinY + seen.Height - 0.5f;
            float x0 = seen.MinX - 0.5f;
            float x1 = seen.MinX + seen.Width - 0.5f;
            p.Packets.Add(BeamStrip(BoardToWorld(seen.AxisX, y1), p.Centre, false));
            p.Packets.Add(BeamStrip(BoardToWorld(seen.AxisX, y0), p.Centre, false));
            p.Packets.Add(BeamStrip(BoardToWorld(x0, seen.AxisY), p.Centre, false));
            p.Packets.Add(BeamStrip(BoardToWorld(x1, seen.AxisY), p.Centre, false));
        }

        // ================================================================== geometry

        /// <summary>
        /// A region's boundary as a strip: the corners rounded, the straight runs sampled, the
        /// whole thing pushed a hair outward off the cells, rotated so it starts at its point
        /// nearest <paramref name="anchor"/>, and reversed if asked (the normals keep pointing in).
        /// </summary>
        private Strip OutlineStrip(List<BoardPoint> corners, Vector2 anchor, bool reverse, float radiusCells = -1f)
        {
            Strip s = Rent(true);
            List<SymmetryChunks.LoopPoint> loop = SymmetryChunks.RoundedLoop(corners,
                radiusCells < 0f ? Style.CornerRadius : radiusCells, Style.Outset, 0.25f);
            int m = loop.Count;
            if (m < 3)
            {
                return s;
            }
            // to world, with the inward normal carried through the board's own transform
            var world = new List<Vector2>(m);
            var inward = new List<Vector2>(m);
            for (int i = 0; i < m; i++)
            {
                BoardPoint bp = loop[i].P;
                world.Add(BoardToWorld(bp.X, bp.Y));
                Vector2 tip = BoardToWorld(bp.X + loop[i].In.X * 0.1f, bp.Y + loop[i].In.Y * 0.1f);
                inward.Add((tip - world[i]).normalized);
            }
            // start at the point nearest the anchor
            int start = 0;
            float best = float.MaxValue;
            for (int i = 0; i < m; i++)
            {
                float d = (world[i] - anchor).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    start = i;
                }
            }
            for (int k = 0; k <= m; k++)
            {
                int i = reverse ? (start - k + m * 2) % m : (start + k) % m;
                s.Points.Add(world[i]);
                s.Inward.Add(inward[i]);
            }
            Measure(s);
            return s;
        }

        /// <summary>An open path in board units as a beam strip, through the board's transform.</summary>
        private Strip PathStrip(List<BoardPoint> path)
        {
            Strip s = Rent(false);
            foreach (BoardPoint p in path)
            {
                s.Points.Add(BoardToWorld(p.X, p.Y));
            }
            int n = s.Points.Count;
            for (int k = 0; k < n; k++)
            {
                Vector2 t = (s.Points[Mathf.Min(k + 1, n - 1)] - s.Points[Mathf.Max(k - 1, 0)]).normalized;
                s.Inward.Add(new Vector2(-t.y, t.x));
            }
            Measure(s);
            return s;
        }

        private static Vector2 Vec(BoardPoint p)
        {
            return new Vector2(p.X, p.Y);
        }

        /// <summary>An energy path from a region to where it meets its partner: straight to a
        /// mirror's axis, on an arc spiralling in for a half turn (the partner's arc is the same
        /// arc turned upside down, which is what says "turned", not "reflected").</summary>
        private Strip BeamStrip(Vector2 from, Vector2 to, bool arc)
        {
            Strip s = Rent(false);
            Vector2 control = (from + to) * 0.5f;
            if (arc)
            {
                Vector2 d = from - to;
                float ang = 48f * Mathf.Deg2Rad;
                Vector2 turned = new Vector2(d.x * Mathf.Cos(ang) - d.y * Mathf.Sin(ang), d.x * Mathf.Sin(ang) + d.y * Mathf.Cos(ang));
                control = to + turned * 0.62f;
            }
            const int steps = 14;
            for (int k = 0; k <= steps; k++)
            {
                float u = k / (float)steps;
                Vector2 p = (1 - u) * (1 - u) * from + 2 * (1 - u) * u * control + u * u * to;
                s.Points.Add(p);
            }
            for (int k = 0; k <= steps; k++)
            {
                Vector2 t = (s.Points[Mathf.Min(k + 1, steps)] - s.Points[Mathf.Max(k - 1, 0)]).normalized;
                s.Inward.Add(new Vector2(-t.y, t.x));
            }
            Measure(s);
            return s;
        }

        private Strip LineStrip(Vector2 from, Vector2 to)
        {
            return BeamStrip(from, to, false);
        }

        private static void Measure(Strip s)
        {
            s.S.Clear();
            float acc = 0f;
            for (int i = 0; i < s.Points.Count; i++)
            {
                if (i > 0)
                {
                    acc += (s.Points[i] - s.Points[i - 1]).magnitude;
                }
                s.S.Add(acc);
            }
            s.Length = acc;
        }

        // ================================================================== the frame

        private void Update()
        {
            float dt = Time.deltaTime;
            TickParticles(dt);
            if (DebugFlags.Any)
            {
                DrawDebug();
            }
            else
            {
                HideDebug();
            }
            if (playing == null)
            {
                ScoreClaim = Mathf.MoveTowards(ScoreClaim, 0f, dt / 0.2f);
                return;
            }
            Playback p = playing;
            if (p.Delay > 0f)
            {
                p.Delay -= dt;
                return;
            }
            p.T += dt;
            float t = p.T;
            cell = CellSize != null ? Mathf.Max(0.05f, CellSize()) : cell;

            // A: DETECT
            if (!p.Detected)
            {
                p.Detected = true;
                Phase = "detect";
                Sound(SymmetryCue.Detect, 0);
            }
            float detect = Bell(Mathf.Clamp01(t / (Style.DetectPause * 2.2f)));
            DrawOutline(p.Frame, 0f, 1f, Style.DetectGlow * detect, 0f, t, 0.5f);

            // the arena settles a pixel as the last lock lands (never a shake) - written only while
            // it runs, so the impulse's other writers are left alone the rest of the time
            float settleU = (t - p.TRes + 0.04f) / 0.2f;
            if (BoardImpulse != null && settleU > 0f && settleU < 1.2f)
            {
                float px = Pixel != null ? Pixel() : 0.01f;
                float settle = settleU < 1f ? Bell(settleU) : 0f;
                BoardImpulse(new Vector2(0f, -px * (p.Double ? 1.2f : 0.8f) * settle));
            }

            // B / C: the pairs
            float fadeOut = 1f - Smooth((t - p.TPay) / 0.16f);
            float resonance = t >= p.TRes ? Bell(Mathf.Clamp01((t - p.TRes) / (Style.ResonanceDuration * 2f))) : 0f;
            foreach (PairFx fx in p.Pairs)
            {
                TickPair(p, fx, t, fadeOut, resonance);
            }

            // D: the cross collision, the resonance
            if (p.Double && t >= p.TCross)
            {
                float u = Mathf.Clamp01((t - p.TCross) / (p.TRes - p.TCross));
                foreach (Strip packet in p.Packets)
                {
                    float head = EaseIn(u) * packet.Length;
                    DrawBeam(packet, Mathf.Max(0f, head - cell * 1.6f), head, Style.ConnectGlow * (0.6f + 0.6f * u) * (u < 1f ? 1f : 0f),
                        2.2f, t);
                }
                if (!p.Crossed && u >= 1f)
                {
                    p.Crossed = true;
                }
            }
            if (t >= p.TRes && !p.Resonated)
            {
                p.Resonated = true;
                Phase = "resonance";
                Sound(p.Double ? SymmetryCue.ResonanceDouble : SymmetryCue.Resonance, 0);
                Flare(p.Centre, (p.Double ? 2.3f : p.HalfTurn ? 1.6f : 1.2f) * cell, Style.CenterPulse * (p.Double ? 1.15f : 0.8f), 0.3f,
                    p.HalfTurn ? 45f : 0f);
                RingPulse(p.Centre, (p.Double ? 2.4f : 1.5f) * cell, Style.TraceCore, p.Double ? 0.55f : 0.35f, 0.34f);
                if (p.Double)
                {
                    Sparks(p.Centre, 8, Style.LockCore, 1.4f);
                }
            }
            foreach (Strip axis in p.Axes)
            {
                DrawLinePulse(axis, 0.34f * resonance * Style.FinalGlow, t);
            }
            // the whole matched shape glowing at once: "the symmetry is confirmed"
            float whole = (t < p.TRes ? 0f : t < p.TRes + 0.05f ? Smooth((t - p.TRes) / 0.05f) : 1f) * fadeOut;
            foreach (Strip u in p.Union)
            {
                DrawOutline(u, 0f, 1f, Style.FinalGlow * 0.85f * whole, 0f, t, 1f);
            }

            // E: THE PAYOUT
            if (t >= p.TPay && !p.Paid)
            {
                p.Paid = true;
                Phase = "payout";
                p.Pop = p.Centre;
                Sound(p.Negative ? SymmetryCue.PayoutInverted : p.Double ? SymmetryCue.PayoutBig : SymmetryCue.Payout, 0);
                Color warm = p.Negative ? Style.Muted : Style.Gold;
                RingPulse(p.Pop, (p.Double ? 2.0f : 1.5f) * cell, warm, p.Negative ? 0.3f : 0.55f, 0.32f);
                if (!p.Negative)
                {
                    Dust(p.Pop, p.Double ? 7 : 5);
                }
                ShowScore(p);
            }
            if (p.Paid)
            {
                TickScore(p, t);
            }

            // F: done
            if (t >= p.TEnd)
            {
                foreach (Strip s in strips)
                {
                    Release(s);
                }
                if (scoreText != null)
                {
                    scoreText.gameObject.SetActive(false);
                }
                playing = null;
                Phase = "-";
            }
        }

        private void TickPair(Playback p, PairFx fx, float t, float fadeOut, float resonance)
        {
            float tA = t - fx.T0;
            float tB = t - fx.T0 - Style.TraceStagger;
            if (tA >= 0f && !fx.Traced)
            {
                fx.Traced = true;
                Phase = "pairs";
                Sound(SymmetryCue.Trace, fx.Step);
            }
            // the trace: a pen running the boundary, then held while the rest arrive, then the
            // resonance lifts them all at once, then gone
            float hold = Style.HoldGlow * (1f - 0.25f * Smooth((t - fx.TLock) / 0.4f));
            // both mirrors: the first wave steps back while the second is drawn
            if (p.Double && fx.Wave == 0 && p.TWave2 >= 0f)
            {
                hold *= 1f - 0.55f * Smooth((t - p.TWave2) / 0.1f);
            }
            // at the resonance the regions hand over to the whole shape's own outline
            float handOver = 1f - Smooth((t - p.TRes + 0.03f) / 0.08f);
            float lit = hold * handOver * fadeOut;
            foreach (Strip s in fx.LoopsA)
            {
                TraceLoop(s, tA, lit, t);
            }
            foreach (Strip s in fx.LoopsB)
            {
                TraceLoop(s, tB, lit, t);
            }
            // the energy toward the axis
            if (fx.BeamA != null)
            {
                float tc = t - (fx.TLock - Style.ConnectDuration);
                float u = Mathf.Clamp01(tc / Style.ConnectDuration);
                bool on = tc >= 0f && tc < Style.ConnectDuration + 0.08f;
                float glow = on ? Style.ConnectGlow * (tc < Style.ConnectDuration ? 1f : 1f - (tc - Style.ConnectDuration) / 0.08f) : 0f;
                float headA = EaseIn(u) * fx.BeamA.Length;
                float headB = EaseIn(u) * fx.BeamB.Length;
                DrawBeam(fx.BeamA, Mathf.Max(0f, headA - cell * 0.9f), headA, glow, 1.8f, t);
                DrawBeam(fx.BeamB, Mathf.Max(0f, headB - cell * 0.9f), headB, glow, 1.8f, t);
            }
            // THE LOCK - "dışşınk" on exactly this frame
            if (t >= fx.TLock && !fx.Locked)
            {
                fx.Locked = true;
                Sound(SymmetryCue.Lock, fx.Step);
                if (fx.Chunk.Self)
                {
                    Flare(fx.Meet, 0.75f * cell, 0.8f, Style.PairLockDuration * 0.85f, 0f);
                    Sparks(fx.Meet, 3, Style.LockCore, 0.7f);
                }
                else
                {
                    Flare(fx.Meet, (fx.Kind == SymmetryKind.HalfTurn ? 1.15f : 0.95f) * cell, 1f, Style.PairLockDuration,
                        fx.Kind == SymmetryKind.HalfTurn ? fx.Step * 30f : 0f);
                    RingPulse(fx.Meet, 0.9f * cell, Style.TraceCore, 0.45f, 0.2f);
                    Sparks(fx.Meet, 4, Style.LockCore, 0.9f);
                    if (fx.Kind == SymmetryKind.HalfTurn)
                    {
                        Orbit(fx.Meet, 0.55f * cell);
                    }
                }
            }
            if (fx.AxisPulse != null && t >= fx.TLock)
            {
                float u = Mathf.Clamp01((t - fx.TLock) / 0.22f);
                DrawLinePulse(fx.AxisPulse, 0.6f * (1f - Smooth(u)) * Style.CenterPulse, t);
            }
        }

        /// <summary>One region's boundary being drawn round: the pen's head at <paramref name="since"/>
        /// / TraceDuration of the way, a bright comet at the head, the drawn part held at
        /// <paramref name="lit"/>.</summary>
        private void TraceLoop(Strip s, float since, float lit, float t)
        {
            if (since < 0f || s.Length <= 0f)
            {
                Hide(s);
                return;
            }
            float u = Mathf.Clamp01(since / Style.TraceDuration);
            float drawn = EaseOut(u);
            // while it is being drawn the line is at full strength; then it settles to the hold
            float strength = u < 1f ? Style.TraceGlow : Mathf.Lerp(Style.TraceGlow, lit, Smooth((since - Style.TraceDuration) / 0.12f));
            float head = u < 1f ? 2.4f : 2.4f * (1f - Smooth((since - Style.TraceDuration) / 0.1f));
            DrawOutline(s, 0f, drawn, strength, head, t, 1f);
        }

        // ================================================================== drawing strips

        private void DrawOutline(Strip s, float from, float to, float strength, float headBoost, float time, float widthMul)
        {
            if (s == null || strength <= 0.003f || to <= from)
            {
                Hide(s);
                return;
            }
            verts.Clear();
            cols.Clear();
            tris.Clear();
            float a0 = from * s.Length;
            float a1 = to * s.Length;
            float line = Style.LineWidth * cell * widthMul;
            float outer = Style.OuterSoft * cell * widthMul;
            float inner = Style.InnerGlow * cell * widthMul;
            Color core = Style.TraceCore;
            Color edge = Style.TraceEdge;
            int rows = 5;
            int samples = 0;
            for (int i = 0; i < s.Points.Count; i++)
            {
                float si = s.S[i];
                if (si < a0 && (i + 1 >= s.Points.Count || s.S[i + 1] < a0))
                {
                    continue;
                }
                Vector2 pos;
                Vector2 nIn;
                float sAt;
                if (si > a1)
                {
                    // the head, between this point and the last
                    float k = (a1 - s.S[i - 1]) / Mathf.Max(0.0001f, si - s.S[i - 1]);
                    pos = Vector2.Lerp(s.Points[i - 1], s.Points[i], k);
                    nIn = Vector2.Lerp(s.Inward[i - 1], s.Inward[i], k).normalized;
                    sAt = a1;
                }
                else
                {
                    pos = s.Points[i];
                    nIn = s.Inward[i];
                    sAt = si;
                }
                // energy flowing along it, and the comet at the head
                float flow = 1f + 0.22f * Mathf.Sin(sAt / Mathf.Max(0.01f, cell) * 5.5f - time * 18f);
                float comet = 1f + headBoost * Mathf.Exp(-(a1 - sAt) / (0.32f * cell));
                float a = Mathf.Clamp01(strength * flow * comet);
                AddRows(pos, nIn, a, line, outer, inner, core, edge);
                samples++;
                if (si > a1)
                {
                    break;
                }
            }
            Commit(s, samples, rows, Style.TraceOrder);
        }

        private void AddRows(Vector2 pos, Vector2 nIn, float a, float line, float outer, float inner, Color core, Color edge)
        {
            Vector2 nOut = -nIn;
            // outer rim (clear) | line edge | LINE | inner glow | clear
            AddVert(pos + nOut * (line + outer), edge, 0f);
            AddVert(pos + nOut * line, Color.Lerp(edge, core, 0.4f), a * 0.55f);
            AddVert(pos, core, a);
            AddVert(pos + nIn * (line + inner * 0.35f), Color.Lerp(core, edge, 0.55f), a * 0.26f);
            AddVert(pos + nIn * (line + inner), edge, 0f);
        }

        /// <summary>A beam: energy travelling from s0 to s1 along an open path, thin, soft on both
        /// sides, brighter at its head and toward the end of its road.</summary>
        private void DrawBeam(Strip s, float s0, float s1, float strength, float headBoost, float time)
        {
            if (s == null || strength <= 0.003f || s1 <= s0)
            {
                Hide(s);
                return;
            }
            verts.Clear();
            cols.Clear();
            tris.Clear();
            float w = 0.05f * cell;
            float soft = 0.14f * cell;
            int samples = 0;
            for (int i = 0; i < s.Points.Count; i++)
            {
                float si = s.S[i];
                if (si < s0 && (i + 1 >= s.Points.Count || s.S[i + 1] < s0))
                {
                    continue;
                }
                Vector2 pos = s.Points[i];
                Vector2 n = s.Inward[i];
                float sAt = si;
                if (si > s1 && i > 0)
                {
                    float k = (s1 - s.S[i - 1]) / Mathf.Max(0.0001f, si - s.S[i - 1]);
                    pos = Vector2.Lerp(s.Points[i - 1], s.Points[i], k);
                    sAt = s1;
                }
                float tail = Smooth((sAt - s0) / Mathf.Max(0.001f, s1 - s0));
                float toward = 0.7f + 0.3f * (sAt / Mathf.Max(0.001f, s.Length));
                float comet = 1f + headBoost * Mathf.Exp(-(s1 - sAt) / (0.18f * cell));
                float a = Mathf.Clamp01(strength * tail * toward * comet);
                AddVert(pos + n * (w + soft), Style.TraceEdge, 0f);
                AddVert(pos + n * w, Color.Lerp(Style.TraceEdge, Style.TraceCore, 0.5f), a * 0.5f);
                AddVert(pos, Style.TraceCore, a);
                AddVert(pos - n * w, Color.Lerp(Style.TraceEdge, Style.TraceCore, 0.5f), a * 0.5f);
                AddVert(pos - n * (w + soft), Style.TraceEdge, 0f);
                samples++;
                if (si > s1)
                {
                    break;
                }
            }
            Commit(s, samples, 5, Style.BeamOrder);
        }

        /// <summary>A whole line glowing, faded at both ends - an axis showing itself, never a bar.</summary>
        private void DrawLinePulse(Strip s, float strength, float time)
        {
            if (s == null || strength <= 0.003f)
            {
                Hide(s);
                return;
            }
            verts.Clear();
            cols.Clear();
            tris.Clear();
            float w = 0.035f * cell;
            float soft = 0.16f * cell;
            for (int i = 0; i < s.Points.Count; i++)
            {
                float u = s.S[i] / Mathf.Max(0.001f, s.Length);
                float a = Mathf.Clamp01(strength * Mathf.Sin(u * Mathf.PI) * (1f + 0.15f * Mathf.Sin(u * 14f - time * 10f)));
                Vector2 pos = s.Points[i];
                Vector2 n = s.Inward[i];
                AddVert(pos + n * (w + soft), Style.TraceEdge, 0f);
                AddVert(pos + n * w, Style.TraceEdge, a * 0.45f);
                AddVert(pos, Style.TraceCore, a);
                AddVert(pos - n * w, Style.TraceEdge, a * 0.45f);
                AddVert(pos - n * (w + soft), Style.TraceEdge, 0f);
            }
            Commit(s, s.Points.Count, 5, Style.BeamOrder);
        }

        private void AddVert(Vector2 p, Color c, float a)
        {
            verts.Add(new Vector3(p.x, p.y, 0f));
            cols.Add(new Color(c.r, c.g, c.b, Mathf.Clamp01(a)));
        }

        private void Commit(Strip s, int samples, int rows, int order)
        {
            if (samples < 2)
            {
                Hide(s);
                return;
            }
            for (int i = 0; i < samples - 1; i++)
            {
                for (int r = 0; r < rows - 1; r++)
                {
                    int a = i * rows + r;
                    int b = a + 1;
                    int c = a + rows;
                    int d = c + 1;
                    tris.Add(a);
                    tris.Add(c);
                    tris.Add(b);
                    tris.Add(b);
                    tris.Add(c);
                    tris.Add(d);
                }
            }
            s.Mesh.Clear();
            s.Mesh.SetVertices(verts);
            s.Mesh.SetColors(cols);
            s.Mesh.SetTriangles(tris, 0);
            s.Mesh.RecalculateBounds();
            s.Renderer.sortingOrder = order;
            s.Renderer.enabled = true;
        }

        private static void Hide(Strip s)
        {
            if (s != null && s.Renderer != null)
            {
                s.Renderer.enabled = false;
            }
        }

        // ================================================================== sprites and sparks

        private void Flare(Vector2 at, float size, float strength, float life, float turn)
        {
            Particle p = Emit(SymmetryShapes.Flare, at, Vector2.zero, life, size, Style.LockCore, false);
            if (p != null)
            {
                p.Colour.a = strength;
                p.Spin = turn;
            }
            Emit(SymmetryShapes.Glow, at, Vector2.zero, life * 1.2f, size * 0.9f, new Color(Style.TraceEdge.r, Style.TraceEdge.g, Style.TraceEdge.b, 0.55f * strength), false);
        }

        private void RingPulse(Vector2 at, float size, Color colour, float strength, float life)
        {
            Particle p = Emit(SymmetryShapes.Ring, at, Vector2.zero, life, size, colour, false);
            if (p != null)
            {
                p.Colour.a = strength;
                // a ring grows: Size is where it ends, it starts at a third
                p.Spin = -1f;
            }
        }

        private void Sparks(Vector2 at, int count, Color colour, float speedCells)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count) * Mathf.PI * 2f + Hash(i, count) * 0.9f;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Particle p = Emit(SymmetryShapes.Spark, at + dir * 0.12f * cell, dir * speedCells * cell * (0.8f + 0.5f * Hash(i + 7, count)),
                    0.16f + 0.06f * Hash(i + 3, count), 0.32f * cell, colour, true);
                if (p != null)
                {
                    p.Colour.a = 0.9f;
                }
            }
        }

        /// <summary>The half turn's lock: two small lights going half way round the centre.</summary>
        private void Orbit(Vector2 at, float radius)
        {
            for (int i = 0; i < 2; i++)
            {
                Particle p = Emit(SymmetryShapes.Glow, at + new Vector2(i == 0 ? radius : -radius, 0f), Vector2.zero, 0.24f, 0.32f * cell,
                    Style.TraceCore, false);
                if (p != null)
                {
                    p.Spin = 1000f + i;   // marks an orbiting light
                    p.Vel = at;           // its centre
                    p.Size = 0.32f * cell;
                    p.Colour.a = 0.9f;
                    p.Pos = new Vector2(radius, i * Mathf.PI);
                }
            }
        }

        /// <summary>Warm gold dust round the payout (a few specks, never a shower).</summary>
        private void Dust(Vector2 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = Hash(i, 91) * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.7f + 0.35f);
                Particle p = Emit(SymmetryShapes.Glow, at + dir * 0.3f * cell, dir * (0.7f + 0.5f * Hash(i, 13)) * cell, 0.42f + 0.15f * Hash(i, 5),
                    0.14f * cell, Style.Cream, false);
                if (p != null)
                {
                    p.Colour.a = 0.85f;
                }
            }
        }

        private Particle Emit(Sprite sprite, Vector2 at, Vector2 vel, float life, float size, Color colour, bool stretch)
        {
            Spr s = RentSprite();
            if (s == null)
            {
                return null;
            }
            s.R.sprite = sprite;
            s.R.sortingOrder = Style.FlareOrder;
            var p = new Particle { S = s, Pos = at, Vel = vel, Life = Mathf.Max(0.02f, life), Size = size, Colour = colour, Stretch = stretch };
            particles.Add(p);
            PaintParticle(p);
            return p;
        }

        private void TickParticles(float dt)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.S.Live = false;
                    p.S.R.enabled = false;
                    particles.RemoveAt(i);
                    continue;
                }
                if (p.Spin < 999f)
                {
                    p.Pos += p.Vel * dt;
                    p.Vel *= Mathf.Max(0f, 1f - 5f * dt);
                }
                PaintParticle(p);
            }
        }

        private void PaintParticle(Particle p)
        {
            float u = Mathf.Clamp01(p.Age / p.Life);
            Transform tr = p.S.R.transform;
            Color c = p.Colour;
            if (p.Spin >= 999f)
            {
                // an orbiting light: half way round the centre in its life
                float ang = p.Pos.y + u * Mathf.PI;
                tr.position = p.Vel + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * p.Pos.x;
                tr.localScale = Vector3.one * p.Size;
                c.a *= Bell(u);
            }
            else if (p.S.R.sprite == SymmetryShapes.Ring)
            {
                tr.position = p.Pos;
                float grow = Mathf.Lerp(0.3f, 1f, EaseOut(u));
                tr.localScale = Vector3.one * p.Size * grow;
                c.a *= 1f - Smooth(u);
            }
            else if (p.S.R.sprite == SymmetryShapes.Flare)
            {
                tr.position = p.Pos;
                // snaps open, then settles smaller as it fades
                float s = u < 0.18f ? EaseOut(u / 0.18f) * 1.05f : Mathf.Lerp(1.05f, 0.7f, (u - 0.18f) / 0.82f);
                tr.localScale = Vector3.one * p.Size * s;
                tr.rotation = Quaternion.Euler(0f, 0f, p.Spin + u * 12f);
                c.a *= u < 0.18f ? 1f : 1f - Smooth((u - 0.18f) / 0.82f);
            }
            else
            {
                tr.position = p.Pos;
                if (p.Stretch)
                {
                    float speed = p.Vel.magnitude;
                    tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p.Vel.y, p.Vel.x) * Mathf.Rad2Deg);
                    tr.localScale = new Vector3(p.Size * (0.6f + 1.4f * Mathf.Clamp01(speed / Mathf.Max(0.01f, cell))), p.Size * 0.5f, 1f);
                }
                else
                {
                    tr.localScale = Vector3.one * p.Size * (1f - 0.4f * u);
                }
                c.a *= u < 0.15f ? u / 0.15f : 1f - Smooth((u - 0.15f) / 0.85f);
            }
            p.S.R.color = c;
        }

        // ================================================================== the score

        private void ShowScore(Playback p)
        {
            if (scoreText == null)
            {
                // the size lives in the transform's scale, so the outline (built once, an offset of
                // the glyph's own height) scales with it
                scoreText = ViewUtil.MakeText3D(transform, "SymmetryScore", Vector2.zero, "+120", 120, 1f / 12f,
                    Style.Gold, Style.PayoutOrder, TextAnchor.MiddleCenter);
            }
            int points = p.Seen.Points;
            scoreText.text = (points >= 0 ? "+" : "") + points;
            scoreText.gameObject.SetActive(true);
            p.Thread = null;
        }

        private void TickScore(Playback p, float t)
        {
            float since = t - p.TPay;
            float big = p.Double ? 0.72f : 0.58f;
            float height = big * cell;
            Color ink = p.Negative ? Style.Muted : Color.Lerp(Style.Gold, Style.Cream, 0.25f);
            float pop = Style.ScorePopDuration;
            float holdEnd = pop + Style.ScoreHold;
            Vector2 at = p.Pop;
            float scale;
            float alpha = 1f;
            float squashX = 1f;
            if (since < pop)
            {
                // born: 0.85 -> 1.08 -> 1, a little wider than tall at the top of it
                float u = since / pop;
                scale = u < 0.6f ? Mathf.Lerp(0.85f, 1.08f, EaseOut(u / 0.6f)) : Mathf.Lerp(1.08f, 1f, Smooth((u - 0.6f) / 0.4f));
                squashX = 1f + 0.05f * Bell(u);
                alpha = Mathf.Clamp01(u / 0.3f);
            }
            else if (since < holdEnd)
            {
                scale = 1f;
            }
            else
            {
                // drifting up and handing itself over
                float u = Mathf.Clamp01((since - holdEnd) / Style.HudTransferDuration);
                scale = 1f - 0.18f * u;
                at += new Vector2(0f, 0.4f * cell * EaseOut(u));
                alpha = 1f - Smooth(Mathf.Clamp01((u - 0.2f) / 0.8f));
            }
            if (scoreText != null)
            {
                scoreText.transform.position = new Vector3(at.x, at.y, 0f);
                scoreText.transform.localScale = new Vector3(height * scale * squashX, height * scale, 1f);
                ViewUtil.SetTextColor(scoreText, new Color(ink.r, ink.g, ink.b, alpha));
                if (alpha <= 0.002f)
                {
                    scoreText.gameObject.SetActive(false);
                }
            }
            // the gold thread to the score, a seed of light riding its head
            if (since >= holdEnd && ScoreAnchor != null)
            {
                Vector2 target = ScoreAnchor();
                if (p.Thread == null)
                {
                    p.Thread = Rent(false);
                    Vector2 from = p.Pop + new Vector2(0f, 0.25f * cell);
                    Vector2 control = new Vector2(Mathf.Lerp(from.x, target.x, 0.35f), Mathf.Max(from.y, target.y) + 0.4f);
                    const int steps = 18;
                    for (int k = 0; k <= steps; k++)
                    {
                        float u = k / (float)steps;
                        p.Thread.Points.Add((1 - u) * (1 - u) * from + 2 * (1 - u) * u * control + u * u * target);
                    }
                    for (int k = 0; k <= steps; k++)
                    {
                        Vector2 tg = (p.Thread.Points[Mathf.Min(k + 1, steps)] - p.Thread.Points[Mathf.Max(k - 1, 0)]).normalized;
                        p.Thread.Inward.Add(new Vector2(-tg.y, tg.x));
                    }
                    Measure(p.Thread);
                }
                float u2 = Mathf.Clamp01((since - holdEnd) / Style.HudTransferDuration);
                float head = EaseIn(u2) * p.Thread.Length;
                DrawThread(p.Thread, Mathf.Max(0f, head - 1.1f), head, u2 < 1f ? 0.85f : 0f, p.Negative);
                if (u2 >= 1f && !p.Arrived)
                {
                    p.Arrived = true;
                    Phase = "score";
                    Sound(SymmetryCue.Land, 0);
                    Emit(SymmetryShapes.Glow, target, Vector2.zero, 0.28f, 0.7f, p.Negative ? Style.Muted : Style.Cream, false);
                }
            }
            // the score's answer: 1 -> 1.07 -> 1 (1.1 for the triple), warm gold - not for a loss
            if (p.Arrived && !p.Negative)
            {
                float u = Mathf.Clamp01((t - p.TArrive) / 0.3f);
                ScoreClaim = 1f - u;
                ScoreScale = 1f + (p.Double ? 0.1f : 0.07f) * Bell(Mathf.Clamp01(u * 1.6f));
                ScoreInk = Style.Gold;
            }
        }

        private void DrawThread(Strip s, float s0, float s1, float strength, bool muted)
        {
            if (s == null || strength <= 0.003f || s1 <= s0)
            {
                Hide(s);
                return;
            }
            verts.Clear();
            cols.Clear();
            tris.Clear();
            Color core = muted ? Style.Muted : Style.Cream;
            Color edge = muted ? Style.Muted : Style.Gold;
            float w = 0.03f;
            float soft = 0.07f;
            int samples = 0;
            for (int i = 0; i < s.Points.Count; i++)
            {
                float si = s.S[i];
                if (si < s0 && (i + 1 >= s.Points.Count || s.S[i + 1] < s0))
                {
                    continue;
                }
                Vector2 pos = s.Points[i];
                float sAt = si;
                if (si > s1 && i > 0)
                {
                    float k = (s1 - s.S[i - 1]) / Mathf.Max(0.0001f, si - s.S[i - 1]);
                    pos = Vector2.Lerp(s.Points[i - 1], s.Points[i], k);
                    sAt = s1;
                }
                Vector2 n = s.Inward[i];
                float tail = Smooth((sAt - s0) / Mathf.Max(0.001f, s1 - s0));
                float a = Mathf.Clamp01(strength * tail * (1f + 1.6f * Mathf.Exp(-(s1 - sAt) / 0.15f)));
                AddVert(pos + n * (w + soft), edge, 0f);
                AddVert(pos + n * w, edge, a * 0.5f);
                AddVert(pos, core, a);
                AddVert(pos - n * w, edge, a * 0.5f);
                AddVert(pos - n * (w + soft), edge, 0f);
                samples++;
                if (si > s1)
                {
                    break;
                }
            }
            Commit(s, samples, 5, Style.PayoutOrder - 1);
        }

        // ================================================================== pools

        private Strip Rent(bool outline)
        {
            Strip s = null;
            foreach (Strip c in strips)
            {
                if (!c.Live)
                {
                    s = c;
                    break;
                }
            }
            if (s == null)
            {
                s = new Strip();
                s.Go = new GameObject("SymStrip");
                s.Go.transform.SetParent(transform, false);
                s.Go.AddComponent<MeshFilter>().sharedMesh = s.Mesh = new Mesh { name = "SymStrip" };
                s.Mesh.MarkDynamic();
                s.Renderer = s.Go.AddComponent<MeshRenderer>();
                s.Renderer.sharedMaterial = glowMaterial;
                strips.Add(s);
            }
            s.Live = true;
            s.Outline = outline;
            s.Points.Clear();
            s.Inward.Clear();
            s.S.Clear();
            s.Length = 0f;
            s.Renderer.enabled = false;
            // the strips are built in world space: their holder never moves
            s.Go.transform.position = Vector3.zero;
            s.Go.transform.rotation = Quaternion.identity;
            s.Go.transform.localScale = Vector3.one;
            return s;
        }

        private void Release(Strip s)
        {
            s.Live = false;
            if (s.Renderer != null)
            {
                s.Renderer.enabled = false;
            }
        }

        private Spr RentSprite()
        {
            foreach (Spr s in sprites)
            {
                if (!s.Live)
                {
                    s.Live = true;
                    s.R.enabled = true;
                    return s;
                }
            }
            if (sprites.Count >= 64)
            {
                return null;
            }
            var go = new GameObject("SymLight");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sharedMaterial = glowMaterial;
            var n = new Spr { R = r, Live = true };
            sprites.Add(n);
            return n;
        }

        private void Sound(SymmetryCue cue, int step)
        {
            if (Sounded != null)
            {
                Sounded(cue, step);
            }
        }

        // ================================================================== debug (lab)

        private readonly List<SpriteRenderer> debugLines = new List<SpriteRenderer>();
        private readonly List<TextMesh> debugTexts = new List<TextMesh>();
        private int debugLineUsed;
        private int debugTextUsed;

        private void DrawDebug()
        {
            debugLineUsed = 0;
            debugTextUsed = 0;
            SymmetryVisuals seen = Current;
            if (seen != null && BoardToWorld != null)
            {
                Color[] palette =
                {
                    new Color(1f, 0.45f, 0.45f), new Color(0.45f, 1f, 0.5f), new Color(0.5f, 0.6f, 1f),
                    new Color(1f, 0.85f, 0.35f), new Color(0.9f, 0.5f, 1f), new Color(0.4f, 0.95f, 0.95f)
                };
                if (DebugFlags.ShowAxis)
                {
                    float y0 = seen.MinY - 0.5f;
                    float y1 = seen.MinY + seen.Height - 0.5f;
                    float x0 = seen.MinX - 0.5f;
                    float x1 = seen.MinX + seen.Width - 0.5f;
                    if (seen.LeftRight)
                    {
                        DLine(BoardToWorld(seen.AxisX, y0), BoardToWorld(seen.AxisX, y1), Color.cyan);
                    }
                    if (seen.TopBottom)
                    {
                        DLine(BoardToWorld(x0, seen.AxisY), BoardToWorld(x1, seen.AxisY), Color.cyan);
                    }
                    if (seen.HalfTurn)
                    {
                        Vector2 c = BoardToWorld(seen.AxisX, seen.AxisY);
                        DLine(c + new Vector2(-0.2f, 0f), c + new Vector2(0.2f, 0f), Color.yellow);
                        DLine(c + new Vector2(0f, -0.2f), c + new Vector2(0f, 0.2f), Color.yellow);
                    }
                }
                if (DebugFlags.ShowMatchedPairs)
                {
                    foreach (SymmetryKind k in new[] { SymmetryKind.LeftRight, SymmetryKind.TopBottom, SymmetryKind.HalfTurn })
                    {
                        Color c = k == SymmetryKind.LeftRight ? new Color(1f, 1f, 1f, 0.5f) : k == SymmetryKind.TopBottom ? new Color(1f, 0.8f, 0.4f, 0.5f) : new Color(0.8f, 0.6f, 1f, 0.5f);
                        foreach (SymmetryPair pr in seen.PairsOf(k))
                        {
                            Vector2 a = BoardToWorld(pr.A.X, pr.A.Y);
                            Vector2 b = BoardToWorld(pr.B.X, pr.B.Y);
                            if (pr.Self)
                            {
                                DLine(a + new Vector2(-0.12f, -0.12f), a + new Vector2(0.12f, 0.12f), c);
                            }
                            else
                            {
                                DLine(a, b, c);
                            }
                        }
                    }
                }
                for (int i = 0; i < Events.Count; i++)
                {
                    SymmetryChunkPair e = Events[i];
                    Color c = palette[i % palette.Length];
                    if (DebugFlags.ShowVisualChunks)
                    {
                        foreach (List<GridPos> region in new[] { e.A, e.B })
                        {
                            foreach (List<BoardPoint> loop in SymmetryChunks.Outline(region))
                            {
                                for (int k = 0; k < loop.Count; k++)
                                {
                                    BoardPoint p0 = loop[k];
                                    BoardPoint p1 = loop[(k + 1) % loop.Count];
                                    DLine(BoardToWorld(p0.X, p0.Y), BoardToWorld(p1.X, p1.Y), c);
                                }
                            }
                        }
                    }
                    if (DebugFlags.ShowChunkOrder)
                    {
                        string label = (i + 1) + (e.Self ? "s" : "") + " " + (e.Kind == SymmetryKind.LeftRight ? "LR" : e.Kind == SymmetryKind.TopBottom ? "TB" : "180");
                        DText(BoardToWorld(e.CentreAX, e.CentreAY), label, c);
                        if (!e.Self)
                        {
                            DText(BoardToWorld(e.CentreBX, e.CentreBY), (i + 1).ToString(), c);
                        }
                    }
                }
                if (DebugFlags.ShowAnchors && playing != null)
                {
                    foreach (PairFx fx in playing.Pairs)
                    {
                        DCross(fx.Meet, 0.12f, Color.white);
                        if (fx.BeamA != null)
                        {
                            DCross(fx.BeamA.Points[0], 0.08f, Color.cyan);
                            DCross(fx.BeamB.Points[0], 0.08f, Color.cyan);
                        }
                    }
                    DCross(playing.Centre, 0.2f, Style.Gold);
                }
                if (DebugFlags.ShowType)
                {
                    string type = (seen.LeftRight ? "LEFT-RIGHT " : "") + (seen.TopBottom ? "TOP-BOTTOM " : "") + (seen.HalfTurn ? "HALF TURN" : "");
                    string text = type + "\nbonus " + seen.Bonus + "  points " + seen.Points + (seen.BothMirrors ? "  (both mirrors: triple)" : "")
                        + "\npairs LR " + seen.LeftRightPairs.Count + "  TB " + seen.TopBottomPairs.Count + "  180 " + seen.HalfTurnPairs.Count
                        + "\nevents " + Events.Count + "  phase " + Phase;
                    DText(BoardToWorld(seen.AxisX, seen.MinY + seen.Height + 0.6f), text, Color.white);
                }
            }
            for (int i = debugLineUsed; i < debugLines.Count; i++)
            {
                debugLines[i].enabled = false;
            }
            for (int i = debugTextUsed; i < debugTexts.Count; i++)
            {
                debugTexts[i].gameObject.SetActive(false);
            }
        }

        private void HideDebug()
        {
            if (debugLineUsed == 0 && debugTextUsed == 0)
            {
                return;
            }
            debugLineUsed = 0;
            debugTextUsed = 0;
            foreach (SpriteRenderer l in debugLines)
            {
                l.enabled = false;
            }
            foreach (TextMesh t in debugTexts)
            {
                t.gameObject.SetActive(false);
            }
        }

        private void DLine(Vector2 a, Vector2 b, Color c)
        {
            if (debugLineUsed >= debugLines.Count)
            {
                var go = new GameObject("SymDebugLine");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = ViewUtil.WhiteSprite;
                r.sortingOrder = 40;
                debugLines.Add(r);
            }
            SpriteRenderer line = debugLines[debugLineUsed++];
            line.enabled = true;
            line.color = c;
            Vector2 d = b - a;
            line.transform.position = (a + b) * 0.5f;
            line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            line.transform.localScale = new Vector3(Mathf.Max(0.001f, d.magnitude), 0.02f, 1f);
        }

        private void DCross(Vector2 at, float size, Color c)
        {
            DLine(at + new Vector2(-size, 0f), at + new Vector2(size, 0f), c);
            DLine(at + new Vector2(0f, -size), at + new Vector2(0f, size), c);
        }

        private void DText(Vector2 at, string text, Color c)
        {
            if (debugTextUsed >= debugTexts.Count)
            {
                debugTexts.Add(ViewUtil.MakeText3D(transform, "SymDebugText", Vector2.zero, "-", 90, 0.012f, c, 41, TextAnchor.MiddleCenter));
            }
            TextMesh t = debugTexts[debugTextUsed++];
            t.gameObject.SetActive(true);
            t.transform.position = at;
            t.text = text;
            ViewUtil.SetTextColor(t, c);
        }

        // ================================================================== easing

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        private static float EaseIn(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        private static float Bell(float t)
        {
            return Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        }

        /// <summary>A deterministic 0..1 from two ints (no Random in an effect).</summary>
        private static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 374761393u + (uint)b * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / (float)0xffffff;
            }
        }
    }
}
