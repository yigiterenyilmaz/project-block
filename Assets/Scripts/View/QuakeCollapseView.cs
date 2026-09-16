// PURPOSE: "Deprem" - SEISMIC COLLAPSE. When the player has no legal move left, the arena shakes
// and the cubes the rules picked sink into cracks that open under them. No score, no clean sweep,
// and above all NOT an explosion.
//
// THE PICTURE HAS TO SAY THREE THINGS AND NOT A FOURTH. The whole arena lost its footing; only
// some cubes were taken; they were taken by the GROUND. What it must never say is "a joker blew
// these up", which is what the old version said: a burst of dust particles on each cell, the
// explosion sound and a camera shake - the language of a line clear with no line in it.
//
// NINE BEATS, ONE CLOCK, about three quarters of a second:
//   1. MICRO TREMOR    the ARENA moves (BoardView.SetTremor) - a pixel or two and a fifth of a
//                      degree, in four controlled impulses. The camera does not move, the hand and
//                      the HUD do not move: an earthquake is something the board suffers.
//   2. REVEAL          only then do the targets show, so the quake reads as global first.
//   3. STRESS          each target settles a pixel, its contact shadow spreads, a pressure mark or
//                      two comes up on its face in its OWN colour, darkened - never a white crack.
//   4. FAULT OPENS     a crack opens under it: a ragged split, not a hole (see QuakeShapes).
//   5. DETACH          the shadow thins, another pixel down, a hair of squeeze.
//   6. SINK            and this is the effect: THE GROUND LINE CLIMBS THE CUBE'S FACE. A cube that
//                      shrinks and fades reads as deleted; a cube hidden from the bottom up behind a
//                      rising ragged edge reads as going into the ground. It also settles, turns a
//                      few degrees and darkens as it goes, but the climbing line carries it.
//   7. DUST, CRUMBS    a few puffs in stone grey-brown, a few chips in the cube's own colour,
//                      thrown sideways and falling - nothing flies up, nothing sparkles.
//   8. FAULT CLOSES    the plates come back together. No glow, no heal: the ground settles.
//   9. SETTLE          one last small counter-move of the arena, and it is still.
//
// NOT ALL AT ONCE. The rules take the cubes in one event; the picture spreads them over WAVES, and
// the wave a cube falls in comes from a hash of its cell and the quake's seed - never a
// left-to-right sweep, which would read as a scan, and never one frame, which flattens it.
//
// THE VIEW DECIDES NOTHING. Which cells, and what stood in them, are QuakeVisuals - the cells the
// engine actually emptied, snapshotted before they went. No quarter is counted here, no
// destructibility is re-checked, no "which ones" is re-drawn. The report is matched by IDENTITY.
//
// THE CUBES ARE PROXIES. The rules have already emptied the cells, so each fallen cube is drawn
// again here from the face the board last showed (BoardView.TryCubeLook - which is also what keeps
// a blind round blind) and sunk from there. Clipping is per renderer (Resources/Shaders/QuakeDrop),
// because SpriteMasks leak into neighbouring collapses. A water cube's swirl stops for the fifth of a
// second it takes to sink - the proxy wears the clip material, not the warp.
//
// NOT DONE, on purpose: the non-target cubes' half-pixel of inertia. The board's own cells move with
// the arena and moving them one by one would fight every repaint; half a pixel does not survive that
// trade.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class QuakeCollapseView : MonoBehaviour
    {
        /// <summary>Every number. Times in seconds, distances in CELLS (a 64-pixel cell makes one
        /// pixel 0.016).</summary>
        public static class Style
        {
            // ---- the arena ----
            public static float PreTremor = 0.11f;

            public static float TremorX = 1.5f / 64f;

            public static float TremorY = 1.0f / 64f;

            public static float TremorDegrees = 0.20f;

            public static float Settle = 0.12f;

            /// <summary>The targets show only after the quake has been felt everywhere.</summary>
            public static float Reveal = 0.07f;

            // ---- one target, relative to its own start ----
            public static float StressFor = 0.12f;

            public static float CrackAt = 0.05f;

            public static float CrackFor = 0.10f;

            public static float DetachAt = 0.13f;

            public static float DetachFor = 0.08f;

            public static float DropAt = 0.21f;

            public static float DropFor = 0.22f;

            public static float CloseFor = 0.13f;

            public static float StressSink = 1f / 64f;

            public static float DetachSink = 1f / 64f;

            public static float StressTilt = 0.6f;

            public static float DropTiltMin = 2f;

            public static float DropTiltMax = 4f;

            public static float DropShear = 3f / 64f;

            /// <summary>How far the cube itself goes down while the ground climbs it.</summary>
            public static float DropSettle = 0.10f;

            public static float ScaleMid = 0.86f;

            public static float ScaleEnd = 0.62f;

            public static float DropDarken = 0.65f;

            /// <summary>The crack sits a little below the cell's middle, off centre by a hash.</summary>
            public static float CrackOffsetY = -0.10f;

            public static float CrackJitterX = 0.04f;

            public static float CrackStartOpening = 0.25f;

            public static float CrackStartLength = 0.35f;

            /// <summary>How far past the crack's near edge the ground line climbs, so the last of
            /// the cube goes INTO the crack rather than stopping at its lip.</summary>
            public static float ClipOvershoot = 0.20f;

            public static float ClipRagged = 0.006f;

            // ---- the waves ----
            public static float SingleStagger = 0.035f;

            public static float WaveGapMedium = 0.10f;

            public static float WaveGapLarge = 0.075f;

            public static float InWaveMedium = 0.02f;

            public static float InWaveLarge = 0.012f;

            public static int Waves = 3;

            /// <summary>A whole quake never runs past this, however much came down.</summary>
            public static float TotalCap = 1.05f;

            // ---- particles ----
            public static int DustCap = 28;

            public static int CrumbCap = 34;

            public static Color Dust = new Color(0.42f, 0.38f, 0.33f, 0.32f);

            public static Color Stone = new Color(0.36f, 0.34f, 0.32f);

            public static float ShadowAlpha = 0.28f;

            public static float StressAlpha = 0.6f;

            /// <summary>One target's whole chain.</summary>
            public static float Span
            {
                get { return DropAt + DropFor + CloseFor; }
            }
        }

        public static class Layers
        {
            public static bool ShowBoardTremor = true;

            public static bool ShowTargetStress = true;

            public static bool ShowFissure = true;

            public static bool ShowDetach = true;

            public static bool ShowDrop = true;

            public static bool ShowDropMask = true;

            public static bool ShowDust = true;

            public static bool ShowCrumbs = true;

            public static bool ShowFaultClose = true;

            public static bool ShowCubeProxy = true;

            /// <summary>DEV: fallen cells red, other cubes that could have fallen yellow, cubes a
            /// quake cannot touch grey.</summary>
            public static bool ShowTargets = false;

            /// <summary>DEV: a small mark per target in its wave's colour.</summary>
            public static bool ShowFaultPhase = false;

            /// <summary>DEV: the box each crack is drawn in.</summary>
            public static bool ShowFissureBounds = false;

            public static void AllOn()
            {
                ShowBoardTremor = true;
                ShowTargetStress = true;
                ShowFissure = true;
                ShowDetach = true;
                ShowDrop = true;
                ShowDropMask = true;
                ShowDust = true;
                ShowCrumbs = true;
                ShowFaultClose = true;
                ShowCubeProxy = true;
                ShowTargets = false;
                ShowFaultPhase = false;
                ShowFissureBounds = false;
            }
        }

        /// <summary>Audio: a low short rumble, a muted stone crack, a soft thud per cube, the dust
        /// settling. Never an explosion boom and never a rockslide that goes on.</summary>
        public static System.Action<string> Sounded;

        /// <summary>Haptics: a very light impact on the tremor and one small tap at the collapse's
        /// peak. Two pulses at most, ever.</summary>
        public static System.Action<string> Haptic;

        public const string SoundBegin = "deprem.begin";
        public const string SoundFaultOpen = "deprem.fault";
        public const string SoundCubeDrop = "deprem.drop";
        public const string SoundSettle = "deprem.settle";
        public const string HapticTremor = "deprem.haptic.tremor";
        public const string HapticPeak = "deprem.haptic.peak";

        private const int CrackOrder = 30;
        private const int ShadowOrder = 31;
        private const int ProxyOrder = 32;
        private const int StressOrder = 33;
        private const int LedgeOrder = 34;
        private const int CrumbOrder = 35;
        private const int DustOrder = 36;
        private const int DebugOrder = 37;

        private sealed class Target
        {
            public GridPos Cell;
            public Vector2 At;
            public float Start;
            public int Wave;
            public float Tilt;
            public float Shear;
            public int Variant;
            public bool Mirror;
            public float CrackTurn;
            public float CrackX;
            public Color Paint;
            public SpriteRenderer Proxy;
            public SpriteRenderer Shadow;
            public SpriteRenderer Crack;
            public SpriteRenderer Ledge;
            public readonly List<SpriteRenderer> Stress = new List<SpriteRenderer>();
            public bool SaidDrop;
            public bool Threw;
            public bool Closed;
        }

        private sealed class Mote
        {
            public SpriteRenderer Sprite;
            public Vector2 At;
            public Vector2 Velocity;
            public float Gravity;
            public float Born;
            public float Life;
            public float Size;
            public float Grow;
            public float Spin;
            public Color Colour;
        }

        private BoardView owner;
        private QuakeVisuals lastPlayed;
        private readonly List<Target> targets = new List<Target>();
        private readonly List<Mote> dust = new List<Mote>();
        private readonly List<Mote> crumbs = new List<Mote>();
        private readonly List<SpriteRenderer> marks = new List<SpriteRenderer>();
        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();
        private MaterialPropertyBlock block;
        private float clock = -1f;
        private float total;
        private float settleAt;
        private float cell = 1f;
        private bool saidFault;
        private bool saidPeak;
        private bool saidSettle;

        private static Material dropMaterial;
        private static bool dropShaderMissing;

        private static readonly int ClipId = Shader.PropertyToID("_Clip");
        private static readonly int SoftId = Shader.PropertyToID("_Soft");
        private static readonly int DarkenId = Shader.PropertyToID("_Darken");

        public bool Busy
        {
            get { return clock >= 0f; }
        }

        /// <summary>
        /// One quake, from the report. Asked every repaint; the report is matched by IDENTITY, so
        /// a repaint mid-collapse cannot restart it and a new run's first quake is never mistaken
        /// for the last one of the run before.
        /// </summary>
        public void Play(BoardView board, QuakeVisuals report)
        {
            if (board == null || report == null || !report.Any || ReferenceEquals(report, lastPlayed))
            {
                return;
            }
            lastPlayed = report;
            Stop();
            owner = board;
            cell = board.CellWorldSize;
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }

            // THE WAVES. Each cube's wave is a hash of its cell and the quake's seed; the order
            // within a wave comes from the same hash. Neither a sweep nor a single frame.
            int n = report.Cells.Count;
            var order = new List<KeyValuePair<float, int>>(n);
            for (int i = 0; i < n; i++)
            {
                order.Add(new KeyValuePair<float, int>(Phase(report.Cells[i], report.Seed), i));
            }
            order.Sort((a, b) => a.Key.CompareTo(b.Key));
            var starts = new float[n];
            var waves = new int[n];
            float lastStart = 0f;
            for (int r = 0; r < n; r++)
            {
                float phase = order[r].Key;
                int wave = Mathf.Min(Style.Waves - 1, (int)(phase * Style.Waves));
                float within = phase * Style.Waves - wave;
                float start;
                if (n <= 4)
                {
                    start = r * Style.SingleStagger;
                    wave = r % Style.Waves;
                }
                else if (n <= 12)
                {
                    start = wave * Style.WaveGapMedium + within * Style.InWaveMedium;
                }
                else
                {
                    start = wave * Style.WaveGapLarge + within * Style.InWaveLarge;
                }
                starts[order[r].Value] = start;
                waves[order[r].Value] = wave;
                lastStart = Mathf.Max(lastStart, start);
            }
            // Squeezed, never lengthened: past the cap the waves draw closer together.
            float room = Style.TotalCap - Style.Reveal - Style.Span;
            float squeeze = lastStart > room && lastStart > 0f ? room / lastStart : 1f;
            lastStart *= squeeze;

            for (int i = 0; i < n; i++)
            {
                uint h = Hash(report.Cells[i], report.Seed);
                var t = new Target
                {
                    Cell = report.Cells[i],
                    At = board.CellToWorld(report.Cells[i]),
                    Start = Style.Reveal + starts[i] * squeeze,
                    Wave = waves[i],
                    Tilt = Mathf.Lerp(Style.DropTiltMin, Style.DropTiltMax, Unit(h, 3))
                        * ((h & 1u) == 0 ? 1f : -1f),
                    Shear = Style.DropShear * ((h & 2u) == 0 ? 1f : -1f),
                    Variant = (int)((h >> 5) % QuakeShapes.CrackVariants),
                    Mirror = (h & 4u) != 0,
                    CrackTurn = (Unit(h, 7) - 0.5f) * 12f,
                    CrackX = (Unit(h, 11) - 0.5f) * 2f * Style.CrackJitterX
                };
                BuildTarget(t, report.Cubes[i], (int)(h >> 9));
                targets.Add(t);
            }
            total = Style.Reveal + lastStart + Style.Span;
            settleAt = Mathf.Max(Style.PreTremor, Style.Reveal + lastStart + Style.DropAt + Style.DropFor
                - 0.05f);
            clock = 0f;
            saidFault = saidPeak = saidSettle = false;
            BuildDebugMarks();
            Announce(Sounded, SoundBegin);
            Announce(Haptic, HapticTremor);
        }

        /// <summary>Ends it wherever it is and puts the arena back still.</summary>
        public void Stop()
        {
            clock = -1f;
            if (owner != null)
            {
                owner.SetTremor(Vector2.zero, 0f);
            }
            foreach (Target t in targets)
            {
                foreach (SpriteRenderer s in t.Stress)
                {
                    s.transform.SetParent(transform, false);
                    Return(s);
                }
                Return(t.Proxy);
                Return(t.Shadow);
                Return(t.Crack);
                Return(t.Ledge);
            }
            targets.Clear();
            foreach (Mote m in dust) { Return(m.Sprite); }
            foreach (Mote m in crumbs) { Return(m.Sprite); }
            dust.Clear();
            crumbs.Clear();
            foreach (SpriteRenderer m in marks) { Return(m); }
            marks.Clear();
        }

        private void BuildTarget(Target t, Cube cube, int stressSeed)
        {
            Sprite tile;
            Color colour;
            if (!owner.TryCubeLook(t.Cell, 1f, out tile, out colour))
            {
                owner.CubeFace(cube, out tile, out colour);
            }
            // The cube's MATERIAL colour, for the marks on its face and the chips off it - never the
            // tile tint, which is white on a painted tile.
            t.Paint = owner.IsDark ? Style.Stone : ViewUtil.CubeMaterialColor(cube);

            t.Crack = Rent(QuakeShapes.Crack(t.Variant), CrackOrder, false);
            t.Crack.enabled = false;
            t.Shadow = Rent(QuakeShapes.Shadow, ShadowOrder, false);
            t.Proxy = Rent(tile, ProxyOrder, true);
            t.Proxy.color = colour;
            t.Ledge = Rent(QuakeShapes.Ledge, LedgeOrder, false);
            t.Ledge.enabled = false;

            // One to three pressure marks, in the cube's own colour darkened - not white, because
            // nothing here is breaking.
            int marksOnFace = 1 + (stressSeed & 3) % 3;
            Color ink = new Color(t.Paint.r * 0.42f, t.Paint.g * 0.42f, t.Paint.b * 0.42f, 0f);
            for (int i = 0; i < marksOnFace; i++)
            {
                SpriteRenderer s = Rent(QuakeShapes.Stress(stressSeed + i), StressOrder, true);
                s.transform.SetParent(t.Proxy.transform, false);
                float a = ((stressSeed >> (3 + i * 4)) & 15) / 15f;
                float b = ((stressSeed >> (5 + i * 3)) & 15) / 15f;
                s.transform.localPosition = new Vector3((a - 0.5f) * 0.46f, (b - 0.5f) * 0.46f, 0f);
                s.transform.localRotation = Quaternion.Euler(0f, 0f, (a * 140f) - 70f);
                s.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                s.color = ink;
                t.Stress.Add(s);
            }
        }

        private void LateUpdate()
        {
            if (clock < 0f)
            {
                return;
            }
            // SCALED: the lab slows effects with Time.timeScale.
            clock += Time.deltaTime;
            PaintTremor();
            foreach (Target t in targets)
            {
                PaintTarget(t);
            }
            PaintMotes(dust, true);
            PaintMotes(crumbs, false);
            if (clock > total + 0.02f && dust.Count == 0 && crumbs.Count == 0)
            {
                Stop();
            }
        }

        // ---- the arena ---------------------------------------------------------------------------

        private void PaintTremor()
        {
            float px = cell;
            Vector2 offset = Vector2.zero;
            float degrees = 0f;
            if (Layers.ShowBoardTremor)
            {
                if (clock < Style.PreTremor)
                {
                    // Four controlled impulses - left, right, a small counter, still. Never noise.
                    float k = clock / Style.PreTremor;
                    Vector3 v = Keys(k, new[] { 0f, 0.25f, 0.55f, 0.80f, 1f },
                        new[]
                        {
                            Vector3.zero, new Vector3(-1f, 0.6f, -1f), new Vector3(1f, -1f, 1f),
                            new Vector3(-0.4f, 0.4f, -0.4f), Vector3.zero
                        });
                    offset = new Vector2(v.x * Style.TremorX, v.y * Style.TremorY) * px;
                    degrees = v.z * Style.TremorDegrees;
                }
                else if (clock >= settleAt && clock < settleAt + Style.Settle)
                {
                    if (!saidSettle)
                    {
                        saidSettle = true;
                        Announce(Sounded, SoundSettle);
                    }
                    float k = (clock - settleAt) / Style.Settle;
                    Vector3 v = Keys(k, new[] { 0f, 0.4f, 1f },
                        new[] { Vector3.zero, new Vector3(0.8f, -0.8f, 0.7f), Vector3.zero });
                    offset = new Vector2(v.x * Style.TremorX, v.y * Style.TremorY) * px;
                    degrees = v.z * Style.TremorDegrees;
                }
            }
            owner.SetTremor(offset, degrees);
        }

        // ---- one cube ----------------------------------------------------------------------------

        private void PaintTarget(Target t)
        {
            float local = clock - t.Start;
            float stress = Layers.ShowTargetStress ? Smooth(Span(local, 0f, Style.StressFor)) : 0f;
            float open = Layers.ShowFissure ? Smooth(Span(local, Style.CrackAt, Style.CrackAt + Style.CrackFor)) : 0f;
            float detach = Layers.ShowDetach
                ? Smooth(Span(local, Style.DetachAt, Style.DetachAt + Style.DetachFor)) : 0f;
            float drop = Layers.ShowDrop ? Span(local, Style.DropAt, Style.DropAt + Style.DropFor) : 0f;
            float fall = Mathf.Pow(drop, 1.7f);
            float dropEnd = Style.DropAt + Style.DropFor;
            float close = Span(local, dropEnd, dropEnd + Style.CloseFor);

            // ---- the crack, opening from a hairline and closing back into the ground
            float crackCentreY = t.At.y + Style.CrackOffsetY * cell;
            if (open > 0f && (close < 1f || !Layers.ShowFaultClose))
            {
                float opening = Mathf.Lerp(Style.CrackStartOpening, 1f, open);
                float length = Mathf.Lerp(Style.CrackStartLength, 1f, open);
                if (Layers.ShowFaultClose && close > 0f)
                {
                    opening *= close < 0.5f ? Mathf.Lerp(1f, 0.45f, close / 0.5f)
                        : Mathf.Lerp(0.45f, 0f, (close - 0.5f) / 0.5f);
                    length *= Mathf.Lerp(1f, 0.8f, close);
                    if (!t.Closed && close > 0.4f)
                    {
                        t.Closed = true;
                        ThrowDust(t, new Vector2(t.At.x + t.CrackX * cell, crackCentreY), 1, 0.5f);
                    }
                }
                t.Crack.enabled = true;
                t.Crack.transform.localPosition = new Vector3(t.At.x + t.CrackX * cell, crackCentreY, 0f);
                t.Crack.transform.localRotation = Quaternion.Euler(0f, 0f, t.CrackTurn);
                t.Crack.transform.localScale = new Vector3(cell * length * (t.Mirror ? -1f : 1f),
                    cell * opening, 1f);
                if (!saidFault)
                {
                    saidFault = true;
                    Announce(Sounded, SoundFaultOpen);
                }
            }
            else
            {
                t.Crack.enabled = false;
            }

            // ---- the proxy cube
            bool gone = drop >= 1f;
            float size = cell * 0.98f;
            float s = Mathf.Lerp(1f, Style.ScaleMid, Mathf.Clamp01(drop / 0.5f));
            if (drop > 0.5f)
            {
                s = Mathf.Lerp(Style.ScaleMid, Style.ScaleEnd, (drop - 0.5f) / 0.5f);
            }
            float squeeze = 1f - 0.025f * detach * (1f - fall);
            float bottom = t.At.y - size * 0.5f - Style.StressSink * cell * stress
                - Style.DetachSink * cell * detach - Style.DropSettle * cell * fall;
            float w = size * s;
            float h = size * s * squeeze;
            float tilt = Style.StressTilt * stress * (t.Tilt > 0f ? 1f : -1f) * (1f - detach)
                + t.Tilt * fall;
            // Rotate about the cube's BASE, so it tips into the crack instead of spinning in place.
            Quaternion turn = Quaternion.Euler(0f, 0f, tilt);
            Vector3 up = turn * new Vector3(0f, h * 0.5f, 0f);
            float shear = t.Shear * cell * (0.3f * detach + 0.7f * fall);
            t.Proxy.enabled = Layers.ShowCubeProxy && !gone;
            t.Proxy.transform.localPosition = new Vector3(t.At.x + shear, bottom, 0f) + up;
            t.Proxy.transform.localRotation = turn;
            t.Proxy.transform.localScale = new Vector3(w, h, 1f);

            // THE GROUND LINE, climbing from the base to past the crack's near edge.
            float nearEdge = crackCentreY - QuakeShapes.CrackHalfOpening * 0.7f * cell;
            float clipLocalY = Mathf.Lerp(bottom, nearEdge + Style.ClipOvershoot * cell, fall);
            float clipWorldY = transform.TransformPoint(new Vector3(t.At.x, clipLocalY, 0f)).y;
            bool clipping = drop > 0f && Layers.ShowDropMask && DropMaterial != null;
            float darken = Mathf.Lerp(1f, Style.DropDarken, fall);
            float fade = DropMaterial == null ? 1f - fall : (drop < 0.92f ? 1f : 1f - (drop - 0.92f) / 0.08f);

            block.Clear();
            block.SetVector(ClipId, new Vector4(clipWorldY, Style.ClipRagged * cell,
                31f / Mathf.Max(cell, 0.0001f), clipping ? 1f : 0f));
            block.SetFloat(SoftId, cell * 0.012f);
            block.SetFloat(DarkenId, darken);
            t.Proxy.SetPropertyBlock(block);
            Color pc = t.Proxy.color;
            pc.a = fade;
            t.Proxy.color = pc;
            foreach (SpriteRenderer s2 in t.Stress)
            {
                s2.enabled = t.Proxy.enabled;
                s2.SetPropertyBlock(block);
                Color c = s2.color;
                c.a = Style.StressAlpha * stress * fade;
                s2.color = c;
            }

            // ---- the contact shadow: spreads under stress, thins as it lets go, gone as it sinks
            t.Shadow.enabled = Layers.ShowCubeProxy && fall < 1f;
            float shadowSpread = 1f + 0.12f * stress;
            t.Shadow.transform.localPosition = new Vector3(t.At.x + cell * 0.03f,
                t.At.y - cell * 0.04f - Style.StressSink * cell * stress, 0f);
            t.Shadow.transform.localScale = new Vector3(size * 1.05f * shadowSpread,
                size * 1.05f * shadowSpread, 1f);
            float shadowA = Style.ShadowAlpha * (1f + 0.2f * stress) * (1f - 0.6f * detach) * (1f - fall);
            t.Shadow.color = new Color(0f, 0f, 0f, shadowA);

            // ---- the ledge riding the ground line
            if (drop > 0f && !gone && Layers.ShowDropMask && Layers.ShowCubeProxy)
            {
                t.Ledge.enabled = true;
                t.Ledge.transform.localPosition = new Vector3(t.At.x + shear, clipLocalY, 0f);
                t.Ledge.transform.localRotation = Quaternion.identity;
                float lw = Mathf.Max(w, cell * 0.62f) * 1.04f;
                t.Ledge.transform.localScale = new Vector3(lw, lw, 1f);
                t.Ledge.color = new Color(1f, 1f, 1f, Mathf.Clamp01(drop / 0.12f) * fade);
            }
            else
            {
                t.Ledge.enabled = false;
            }

            // ---- the chips and the dust, once, as it goes
            if (drop > 0f && !t.Threw)
            {
                t.Threw = true;
                int n = targets.Count;
                ThrowCrumbs(t, bottom, n <= 4 ? 4 : n <= 12 ? 3 : 2);
                ThrowDust(t, new Vector2(t.At.x, bottom), n <= 4 ? 3 : n <= 12 ? 2 : 1, 1f);
                if (!t.SaidDrop)
                {
                    t.SaidDrop = true;
                    Announce(Sounded, SoundCubeDrop);
                }
                if (!saidPeak)
                {
                    saidPeak = true;
                    Announce(Haptic, HapticPeak);
                }
            }
        }

        // ---- particles ---------------------------------------------------------------------------

        private void ThrowCrumbs(Target t, float bottom, int count)
        {
            if (!Layers.ShowCrumbs)
            {
                return;
            }
            uint h = Hash(t.Cell, 0x51ED27u);
            for (int i = 0; i < count && crumbs.Count < Style.CrumbCap; i++)
            {
                float side = (i & 1) == 0 ? -1f : 1f;
                float u = Unit(h, i * 3 + 1);
                crumbs.Add(new Mote
                {
                    Sprite = Rent(QuakeShapes.Crumb(i + (int)(h >> 3)), CrumbOrder, false),
                    At = new Vector2(t.At.x + side * cell * (0.25f + 0.2f * u), bottom + cell * 0.04f),
                    // SIDEWAYS and a little DOWN. Nothing a quake drops flies up.
                    Velocity = new Vector2(side * cell * (0.5f + 0.5f * u), -cell * (0.1f + 0.25f * Unit(h, i * 5 + 2))),
                    Gravity = -3f * cell,
                    Born = clock,
                    Life = 0.20f + 0.10f * Unit(h, i * 7 + 3),
                    Size = cell * (0.045f + 0.035f * Unit(h, i * 11 + 4)),
                    Spin = (Unit(h, i * 13 + 5) - 0.5f) * 540f,
                    Colour = new Color(t.Paint.r * 0.9f, t.Paint.g * 0.9f, t.Paint.b * 0.9f, 1f)
                });
            }
        }

        private void ThrowDust(Target t, Vector2 at, int count, float strength)
        {
            if (!Layers.ShowDust)
            {
                return;
            }
            uint h = Hash(t.Cell, 0xD057u + (uint)count);
            for (int i = 0; i < count && dust.Count < Style.DustCap; i++)
            {
                float side = (i & 1) == 0 ? -1f : 1f;
                float u = Unit(h, i * 3 + 1);
                Color c = Style.Dust;
                c.a *= strength;
                dust.Add(new Mote
                {
                    Sprite = Rent(QuakeShapes.Dust, DustOrder, false),
                    At = at + new Vector2(side * cell * 0.2f * u, 0f),
                    Velocity = new Vector2(side * cell * (0.15f + 0.15f * u), cell * 0.12f),
                    Born = clock,
                    Life = 0.16f + 0.10f * Unit(h, i * 5 + 2),
                    Size = cell * (0.18f + 0.08f * Unit(h, i * 7 + 3)) * (0.6f + 0.4f * strength),
                    Grow = 0.5f,
                    Colour = c
                });
            }
        }

        private void PaintMotes(List<Mote> motes, bool isDust)
        {
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                Mote m = motes[i];
                float age = clock - m.Born;
                float k = age / m.Life;
                if (k >= 1f)
                {
                    Return(m.Sprite);
                    motes.RemoveAt(i);
                    continue;
                }
                Vector2 p = m.At + m.Velocity * age + new Vector2(0f, 0.5f * m.Gravity * age * age);
                m.Sprite.enabled = true;
                m.Sprite.transform.localPosition = new Vector3(p.x, p.y, 0f);
                float size = m.Size * (1f + m.Grow * k);
                m.Sprite.transform.localScale = new Vector3(size, size, 1f);
                m.Sprite.transform.localRotation = Quaternion.Euler(0f, 0f, m.Spin * age);
                Color c = m.Colour;
                c.a *= isDust ? (1f - k) : (k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
                m.Sprite.color = c;
            }
        }

        // ---- dev ---------------------------------------------------------------------------------

        private void BuildDebugMarks()
        {
            GameBoard board = owner.Board;
            if (Layers.ShowTargets && board != null)
            {
                var fallen = new HashSet<GridPos>();
                foreach (Target t in targets)
                {
                    fallen.Add(t.Cell);
                    Mark(t.At, cell * 0.96f, new Color(1f, 0.25f, 0.25f, 0.95f));
                }
                foreach (GridPos pos in board.GetOccupiedCells())
                {
                    Cube? cube = board.GetCube(pos);
                    if (!cube.HasValue || fallen.Contains(pos))
                    {
                        continue;
                    }
                    Mark(owner.CellToWorld(pos), cell * 0.9f,
                        CubeRules.IsExternallyDestructible(cube.Value)
                            ? new Color(1f, 0.9f, 0.2f, 0.7f)
                            : new Color(0.6f, 0.6f, 0.6f, 0.7f));
                }
            }
            if (Layers.ShowFaultPhase)
            {
                var wave = new[] { new Color(0.3f, 0.8f, 1f), new Color(0.6f, 1f, 0.4f), new Color(1f, 0.6f, 0.9f) };
                foreach (Target t in targets)
                {
                    Mark(t.At, cell * 0.3f, wave[t.Wave % wave.Length]);
                }
            }
            if (Layers.ShowFissureBounds)
            {
                foreach (Target t in targets)
                {
                    Mark(new Vector2(t.At.x + t.CrackX * cell, t.At.y + Style.CrackOffsetY * cell),
                        cell, new Color(0.3f, 0.5f, 1f, 0.8f));
                }
            }
        }

        private void Mark(Vector2 at, float size, Color colour)
        {
            SpriteRenderer r = Rent(QuakeShapes.Ring, DebugOrder, false);
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localScale = new Vector3(size, size, 1f);
            r.color = colour;
            marks.Add(r);
        }

        // ---- pool, material, maths ---------------------------------------------------------------

        private static Material DropMaterial
        {
            get
            {
                if (dropMaterial == null && !dropShaderMissing)
                {
                    Shader shader = Shader.Find("ProjectBlock/QuakeDrop");
                    if (shader == null)
                    {
                        shader = Resources.Load<Shader>("Shaders/QuakeDrop");
                    }
                    dropShaderMissing = shader == null;
                    if (shader != null)
                    {
                        dropMaterial = new Material(shader);
                    }
                }
                return dropMaterial;
            }
        }

        private SpriteRenderer Rent(Sprite sprite, int order, bool clipped)
        {
            SpriteRenderer r;
            if (spare.Count > 0)
            {
                r = spare.Pop();
            }
            else
            {
                var go = new GameObject("Quake");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
            }
            r.sprite = sprite;
            r.sortingOrder = order;
            r.enabled = true;
            r.color = Color.white;
            r.transform.localRotation = Quaternion.identity;
            r.SetPropertyBlock(null);
            if (clipped && DropMaterial != null)
            {
                r.sharedMaterial = DropMaterial;
            }
            else if (ViewUtil.PlainSpriteMaterial != null)
            {
                r.sharedMaterial = ViewUtil.PlainSpriteMaterial;
            }
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

        private static float Phase(GridPos cell, uint seed)
        {
            return (Hash(cell, seed) & 0xFFFFFF) / (float)0x1000000;
        }

        private static uint Hash(GridPos cell, uint seed)
        {
            uint h = seed ^ (uint)(cell.X * 73856093) ^ (uint)(cell.Y * 19349663);
            h ^= h >> 16;
            h *= 0x7feb352dU;
            h ^= h >> 15;
            h *= 0x846ca68bU;
            h ^= h >> 16;
            return h;
        }

        private static float Unit(uint h, int salt)
        {
            uint x = h ^ ((uint)salt * 0x9E3779B9u);
            x ^= x >> 13;
            x *= 0x5bd1e995u;
            x ^= x >> 15;
            return (x & 0xFFFF) / 65535f;
        }

        private static Vector3 Keys(float k, float[] at, Vector3[] values)
        {
            for (int i = 1; i < at.Length; i++)
            {
                if (k <= at[i])
                {
                    float f = Smooth(Mathf.InverseLerp(at[i - 1], at[i], k));
                    return Vector3.Lerp(values[i - 1], values[i], f);
                }
            }
            return values[values.Length - 1];
        }

        private static float Span(float t, float from, float to)
        {
            return Mathf.Clamp01((t - from) / Mathf.Max(to - from, 0.0001f));
        }

        private static float Smooth(float k)
        {
            return k * k * (3f - 2f * k);
        }

        private static void Announce(System.Action<string> channel, string what)
        {
            if (channel != null)
            {
                channel(what);
            }
        }
    }
}
