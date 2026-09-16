// PURPOSE: "Yangın" - THE FIRE FRONT. The one ring of cubes the joker lights, drawn as fire
// jumping to them rather than as cubes changing colour.
//
// IT IS FIVE BEATS AND THE ORDER IS THE WHOLE POINT:
//
//   WARM     the cubes that are ALREADY fire gather - a core brightening, a few sparks off their
//            edges, a fast halo. This says where the spread is about to come from, and it happens
//            ONLY on the sources: a cube lit by this use never warms up, because it never spreads.
//   LICK     a short curved tongue leaves each source for each neighbour it is lighting. Not a
//            beam: it bows, it is hottest at its core, and it carries an ember or two at its tip.
//            Sources go a beat apart, so a board full of fire reads as a front crossing it.
//   CATCH    the tongue lands and the target lights ON THE EDGE IT ARRIVED AT. Nothing else about
//            the cube has changed yet - it is still itself, with a hot line down one side.
//   HEAT     the burn crosses the cube (FireBloom): the old face is drained, sooted and eaten
//            from that side while the fire face comes up behind the same front, glowing through
//            ember veins. This is the beat that makes it a transformation rather than a swap.
//   SETTLE   the front finishes, a couple of embers lift, the heat sinks out of it, and the board
//            takes the cell back as an ordinary fire cube.
//
// THE BOARD IS ASKED TO STAND BACK (BoardView.HoldCells) for exactly as long as that runs. Core
// converted the cube the moment the joker fired, so without this the cell is already painted fire
// underneath and the whole transformation is drawn over an answer the player has been given.
// It is the same bargain the press and BossMoveView make, and the rot's SetRotWash is the same
// idea in a different shape.
//
// THE VIEW DECIDES NOTHING. SpreadVisuals (Core, reporting only, [NotSaved]) says which cubes were
// sources, which were lit, WHAT each one used to be and WHICH SIDES it caught from. In particular
// the one-ring rule is the report's, not a guess here - and it is why a cube that catches is
// never drawn spreading.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>One use of "Yangın", playing. Pooled, self-driving, under a second.</summary>
    public sealed class FireSpreadView : MonoBehaviour
    {
        public static class Style
        {
            // ---- the palette: amber, orange, a yellow core, a burnt-red edge ----
            public static readonly Color Core = new Color(1f, 0.93f, 0.62f);

            public static readonly Color Flame = new Color(1f, 0.62f, 0.18f);

            public static readonly Color Deep = new Color(0.72f, 0.24f, 0.08f);

            public static readonly Color Ember = new Color(1f, 0.72f, 0.3f);

            // ---- 1. the sources gathering ----
            public static float Warm = 0.13f;

            /// <summary>How bright a source's middle gets, as a fraction of a cell.</summary>
            public static float WarmGlow = 0.62f;

            /// <summary>
            /// The warm-up's outer glow, and IT MUST NEVER LEAVE ITS OWN CELL. This was 1.25,
            /// which with the breath on top of it peaked at 1.44 cells - so a quarter of it lay
            /// on each of the four neighbours, and in the common case those neighbours are EMPTY.
            /// An orange glow standing on an empty cell says the empty cell is catching fire, and
            /// the rule is the exact opposite: the spread converts CUBES and never creates one.
            /// A picture that contradicts the rule is worse than no picture. 0.86 * 1.15 = 0.99.
            /// </summary>
            public static float WarmHalo = 0.86f;

            /// <summary>Hard ceiling, in cells, on anything drawn centred on a single cell. The
            /// checker holds every per-cell size under it.</summary>
            public static float CellBound = 1f;

            public static int WarmSparks = 3;

            // ---- 2. the tongue ----
            /// <summary>How far apart two sources start. A whole board of fire is a front moving
            /// across it, not a flashbulb.</summary>
            public static float Stagger = 0.032f;

            public static float LickTravel = 0.145f;

            /// <summary>Width of the tongue's body and of the hot core inside it, in cells.
            /// </summary>
            public static float LickWidth = 0.34f;

            public static float LickCore = 0.15f;

            /// <summary>How far it bows off the straight line between the two cells. A straight
            /// one is a beam, and a beam is the one thing this must not be.</summary>
            public static float LickBow = 0.3f;

            // ---- 3-4. the cube catching, and the burn crossing it ----
            public static float Catch = 0.1f;

            public static float Heat = 0.24f;

            /// <summary>How soft and how ragged the burn line is. Both in cube-space, where the
            /// cube is one unit across.</summary>
            public static float Band = 0.24f;

            public static float Ragged = 0.11f;

            public static float Soot = 0.34f;

            public static float Drain = 0.72f;

            public static float Veins = 0.55f;

            // ---- 5. settling ----
            public static float Settle = 0.13f;

            public static int SettleEmbers = 3;

            /// <summary>The short amber pulse the new fire cube ends on, and how far the heat
            /// runs past the front before it sinks out.</summary>
            public static float SettleHeat = 0.55f;

            // ---- the embers ----
            public static float EmberSize = 0.1f;

            public static float EmberRise = 0.42f;

            public static float EmberLife = 0.4f;

            /// <summary>Per cell, and for the whole effect. A board of fire must not become a
            /// particle storm - the richness is per cube, not per screen.</summary>
            public static int EmbersPerCell = 4;

            public static int EmberCap = 40;

            /// <summary>Above this many targets the per-cube trimmings thin out. The BURN never
            /// does: that is the mechanic, and it is the last thing that may be dropped.</summary>
            public static int CrowdedAbove = 10;
        }

        /// <summary>What the lab can take away, one layer at a time.</summary>
        public static class Layers
        {
            public static bool ShowSourceWarmup = true;

            public static bool ShowFlameLicks = true;

            public static bool ShowBurn = true;

            public static bool ShowEmbers = true;

            public static bool ShowSettle = true;

            /// <summary>DEV: ring every SOURCE in one colour and every TARGET in another - the
            /// fastest way to see that the ring is one deep and that nothing lit is spreading.
            /// </summary>
            public static bool ShowSourceTargetMarks = false;

            public static void AllOn()
            {
                ShowSourceWarmup = true;
                ShowFlameLicks = true;
                ShowBurn = true;
                ShowEmbers = true;
                ShowSettle = true;
                ShowSourceTargetMarks = false;
            }
        }

        // Sorting: the board's cubes are 0-2, so everything here sits above them. The burn's two
        // faces are one order apart (old under new) and the tongues are over both, because a
        // tongue arriving is what makes the cube catch.
        private const int WasOrder = 6;

        private const int FireOrder = 7;

        private const int LickOrder = 9;

        private const int EmberOrder = 11;

        private const int MarkOrder = 13;

        private sealed class Burn
        {
            public GridPos Cell;
            public Vector2 At;
            public Vector2 Dir;
            public float Start;
            public float Seed;
            public SpriteRenderer Was;
            public SpriteRenderer Fire;
            public Sprite WasFace;
            public Sprite FireFace;
            public Color WasTint;
            public Color FireTint;

            /// <summary>Extra sides this cube was reached from. One transformation, one extra
            /// mark each - two fires meeting on a cube says so without two animations.</summary>
            public readonly List<Vector2> AlsoFrom = new List<Vector2>();
        }

        private sealed class Lick
        {
            public TalismanTendril Body;
            public TalismanTendril Hot;
            public Vector2 From;
            public Vector2 Control;
            public Vector2 To;
            public float Start;
        }

        private sealed class Glow
        {
            public SpriteRenderer Body;
            public SpriteRenderer Halo;
            public Vector2 At;
            public float Start;
        }

        private sealed class Speck
        {
            public SpriteRenderer Body;
            public Vector2 At;
            public Vector2 Drift;
            public float Start;
            public float Life;
            public float Size;
        }

        private readonly List<Burn> burns = new List<Burn>();

        private readonly List<Lick> licks = new List<Lick>();

        private readonly List<Glow> glows = new List<Glow>();

        private readonly List<Speck> specks = new List<Speck>();

        private readonly List<SpriteRenderer> marks = new List<SpriteRenderer>();

        private readonly List<GridPos> held = new List<GridPos>();

        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

        private readonly Stack<TalismanTendril> spareLicks = new Stack<TalismanTendril>();

        private BoardView owner;

        private float cellSize;

        private System.Func<GridPos, Vector2> toWorld;

        private float clock = -1f;

        private float span;

        /// <summary>
        /// The report last played, compared by IDENTITY rather than by serial.
        ///
        /// Serials restart at 1 with every new joker, and this view outlives a run - so keyed on the
        /// serial, a new run's first event was silently skipped whenever the run before had fired
        /// exactly as many times. Every use writes a NEW report object, a repaint hands back the
        /// same one, and a loaded save has none at all, so identity is right in all three cases.
        /// </summary>
        private SpreadVisuals lastPlayed;

        private MaterialPropertyBlock block;

        public bool Busy
        {
            get { return clock >= 0f; }
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Plays one use of the joker. Keyed on the report ITSELF: the game asks every repaint
        /// and the spread must not restart because something else redrew the board.
        /// </summary>
        public void Play(BoardView view, SpreadVisuals report)
        {
            if (view == null || report == null || !report.Any || ReferenceEquals(report, lastPlayed))
            {
                return;
            }
            // FIRE ONLY, for now. The report is kind-agnostic (Taşkın spreads water through the
            // same joker) and this is the fire language: amber, embers, a burn front. Water will
            // want its own, and drawing it in this one would be a lie in two directions.
            if (report.Kind != CubeKind.Fire)
            {
                return;
            }
            lastPlayed = report;
            Stop();
            owner = view;
            cellSize = view.CellWorldSize;
            toWorld = view.CellToWorld;

            bool crowded = report.Targets.Count > Style.CrowdedAbove;
            int embers = 0;

            // 1. THE SOURCES GATHER - and only they. A cube lit by this use gets no warm-up,
            //    because it is not going to spread, and showing it gather would say it was.
            var order = new Dictionary<GridPos, int>();
            for (int i = 0; i < report.Sources.Count; i++)
            {
                order[report.Sources[i]] = i;
                if (!Layers.ShowSourceWarmup)
                {
                    continue;
                }
                glows.Add(new Glow
                {
                    At = toWorld(report.Sources[i]),
                    Start = i * Style.Stagger * 0.5f
                });
                if (Layers.ShowEmbers && !crowded && embers < Style.EmberCap)
                {
                    embers += Scatter(toWorld(report.Sources[i]), i * Style.Stagger * 0.5f,
                        Style.WarmSparks, 0.45f);
                }
            }

            // 2-5. ONE TRANSFORMATION PER TARGET, started by the tongue that reaches it.
            for (int i = 0; i < report.Targets.Count; i++)
            {
                SpreadIgnition lit = report.Targets[i];
                if (lit.From.Count == 0)
                {
                    continue;
                }
                GridPos first = lit.From[0];
                int wave = order.ContainsKey(first) ? order[first] : 0;
                float start = Style.Warm + wave * Style.Stagger;
                Vector2 at = toWorld(lit.Cell);
                Vector2 from = toWorld(first);
                var burn = new Burn
                {
                    Cell = lit.Cell,
                    At = at,
                    // The way the fire is TRAVELLING, which is the side the cube catches on.
                    Dir = Direction(from, at),
                    Start = start + Style.LickTravel,
                    Seed = Random01(lit.Cell, 7),
                    WasFace = view.FaceOf(lit.Was),
                    WasTint = ViewUtil.CubeMaterialColor(lit.Was),
                    FireFace = view.FaceOf(new Cube(CubeKind.Fire, lit.Was.SourceCardId)),
                    FireTint = ViewUtil.CubeMaterialColor(new Cube(CubeKind.Fire,
                        lit.Was.SourceCardId))
                };
                for (int k = 1; k < lit.From.Count; k++)
                {
                    burn.AlsoFrom.Add(Direction(toWorld(lit.From[k]), at));
                }
                burns.Add(burn);
                held.Add(lit.Cell);

                if (Layers.ShowFlameLicks)
                {
                    for (int k = 0; k < lit.From.Count; k++)
                    {
                        Vector2 source = toWorld(lit.From[k]);
                        int w = order.ContainsKey(lit.From[k]) ? order[lit.From[k]] : wave;
                        licks.Add(MakeLick(source, at, Style.Warm + w * Style.Stagger,
                            lit.Cell, k));
                    }
                }
                if (Layers.ShowEmbers && embers < Style.EmberCap)
                {
                    int n = crowded ? 1 : Style.EmbersPerCell;
                    embers += Scatter(at, burn.Start + Style.Catch, n, 1f);
                }
                span = Mathf.Max(span,
                    burn.Start + Style.Catch + Style.Heat + Style.Settle + Style.EmberLife);
            }

            if (burns.Count == 0)
            {
                Stop();
                return;
            }
            // THE BOARD STANDS BACK for exactly as long as this runs - Core has already made
            // every one of these cells fire, and the cell underneath would answer the question
            // the animation is asking.
            if (Layers.ShowBurn)
            {
                view.HoldCells(held);
            }
            if (Layers.ShowSourceTargetMarks)
            {
                Marks(report);
            }
            clock = 0f;
        }

        public void Stop()
        {
            if (owner != null && held.Count > 0)
            {
                owner.ReleaseCells(held);
            }
            held.Clear();
            for (int i = 0; i < burns.Count; i++)
            {
                Return(burns[i].Was);
                Return(burns[i].Fire);
            }
            burns.Clear();
            for (int i = 0; i < licks.Count; i++)
            {
                ReturnLick(licks[i].Body);
                ReturnLick(licks[i].Hot);
            }
            licks.Clear();
            for (int i = 0; i < glows.Count; i++)
            {
                Return(glows[i].Body);
                Return(glows[i].Halo);
            }
            glows.Clear();
            for (int i = 0; i < specks.Count; i++)
            {
                Return(specks[i].Body);
            }
            specks.Clear();
            for (int i = 0; i < marks.Count; i++)
            {
                Return(marks[i]);
            }
            marks.Clear();
            clock = -1f;
            span = 0f;
        }

        // =================================================================== building

        private static Vector2 Direction(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            // SNAPPED TO THE AXIS the neighbour actually lies on. The spread is 4-neighbour only,
            // so a diagonal here would be a direction the rule cannot produce.
            return Mathf.Abs(d.x) >= Mathf.Abs(d.y)
                ? new Vector2(Mathf.Sign(d.x), 0f)
                : new Vector2(0f, Mathf.Sign(d.y));
        }

        private Lick MakeLick(Vector2 from, Vector2 to, float start, GridPos salt, int index)
        {
            Vector2 d = to - from;
            var side = new Vector2(-d.y, d.x);
            float bow = (Random01(salt, 31 + index) - 0.5f) * 2f * Style.LickBow;
            return new Lick
            {
                From = from,
                Control = (from + to) * 0.5f + side.normalized * (bow * cellSize),
                To = to,
                Start = start,
                Body = RentLick(LickOrder),
                Hot = RentLick(LickOrder + 1)
            };
        }

        private int Scatter(Vector2 at, float when, int count, float strength)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (i * 2.39996f + at.x * 2.7f + at.y * 1.3f) % 6.2832f;
                specks.Add(new Speck
                {
                    At = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (cellSize * 0.24f),
                    Drift = new Vector2(Mathf.Cos(a) * 0.4f, 1f) * strength,
                    Start = when,
                    Life = Style.EmberLife * (0.7f + 0.5f * ((i * 13) % 7) / 10f),
                    Size = cellSize * Style.EmberSize * (0.7f + 0.6f * ((i * 17) % 5) / 10f)
                });
            }
            return count;
        }

        private void Marks(SpreadVisuals report)
        {
            for (int i = 0; i < report.Sources.Count; i++)
            {
                SpriteRenderer m = Rent(FireShapes.Rim, MarkOrder);
                m.transform.localPosition = toWorld(report.Sources[i]);
                Fit(m, cellSize * 0.95f, cellSize * 0.95f);
                m.color = new Color(1f, 0.35f, 0.1f, 0.95f);
                marks.Add(m);
            }
            for (int i = 0; i < report.Targets.Count; i++)
            {
                SpriteRenderer m = Rent(FireShapes.Rim, MarkOrder);
                m.transform.localPosition = toWorld(report.Targets[i].Cell);
                Fit(m, cellSize * 0.8f, cellSize * 0.8f);
                m.color = new Color(0.3f, 0.85f, 1f, 0.95f);
                marks.Add(m);
            }
        }

        // =================================================================== the clock

        private void Update()
        {
            if (clock < 0f)
            {
                return;
            }
            clock += Time.deltaTime;
            PaintGlows();
            PaintLicks();
            PaintBurns();
            PaintSpecks();
            if (clock > span)
            {
                Stop();
            }
        }

        private void PaintGlows()
        {
            for (int i = 0; i < glows.Count; i++)
            {
                Glow g = glows[i];
                float k = Span(clock, g.Start, g.Start + Style.Warm);
                if (k <= 0f || k >= 1f)
                {
                    if (g.Body != null)
                    {
                        Return(g.Body);
                        Return(g.Halo);
                        g.Body = null;
                        g.Halo = null;
                    }
                    continue;
                }
                if (g.Body == null)
                {
                    g.Halo = Rent(FireShapes.Ember, WasOrder);
                    g.Body = Rent(FireShapes.Ember, LickOrder);
                }
                // In and out over its own short window: the source is gathering, not changing.
                float on = Mathf.Sin(k * Mathf.PI);
                float core = cellSize * Style.WarmGlow * (0.7f + 0.4f * on);
                g.Body.transform.localPosition = g.At;
                Fit(g.Body, core, core);
                g.Body.color = new Color(Style.Core.r, Style.Core.g, Style.Core.b, 0.7f * on);
                // Clamped to the cell it belongs to: see Style.WarmHalo. The source is heating
                // up, and what is beside it has not been told anything yet.
                float halo = cellSize * Mathf.Min(Style.WarmHalo * (0.8f + 0.35f * on),
                    Style.CellBound);
                g.Halo.transform.localPosition = g.At;
                Fit(g.Halo, halo, halo);
                g.Halo.color = new Color(Style.Flame.r, Style.Flame.g, Style.Flame.b, 0.22f * on);
            }
        }

        private void PaintLicks()
        {
            for (int i = 0; i < licks.Count; i++)
            {
                Lick l = licks[i];
                float k = Span(clock, l.Start, l.Start + Style.LickTravel);
                float gone = Span(clock, l.Start + Style.LickTravel * 0.75f,
                    l.Start + Style.LickTravel * 1.5f);
                if (k <= 0f || gone >= 1f)
                {
                    l.Body.Hide();
                    l.Hot.Hide();
                    continue;
                }
                // OUT FAST, then arriving - a tongue is a strike, not a pour.
                float reach = 1f - (1f - k) * (1f - k);
                float tail = Mathf.Max(0f, reach - 0.55f);
                float fade = 1f - gone;
                l.Body.Ribbon(l.From, l.Control, l.To, cellSize * Style.LickWidth, 0.75f,
                    tail, reach,
                    new Color(Style.Flame.r, Style.Flame.g, Style.Flame.b, 0.9f * fade));
                l.Hot.Ribbon(l.From, l.Control, l.To, cellSize * Style.LickCore, 0.85f,
                    tail + 0.05f, reach,
                    new Color(Style.Core.r, Style.Core.g, Style.Core.b, fade));
            }
        }

        private void PaintBurns()
        {
            if (!Layers.ShowBurn)
            {
                return;
            }
            for (int i = 0; i < burns.Count; i++)
            {
                Burn b = burns[i];
                float life = Style.Catch + Style.Heat + Style.Settle;
                float k = Span(clock, b.Start, b.Start + life);
                if (k <= 0f)
                {
                    continue;
                }
                if (b.Was == null)
                {
                    b.Was = Rent(b.WasFace, WasOrder, BloomMaterial());
                    b.Fire = Rent(b.FireFace, FireOrder, BloomMaterial());
                    b.Was.transform.localPosition = b.At;
                    b.Fire.transform.localPosition = b.At;
                    Fit(b.Was, cellSize, cellSize);
                    Fit(b.Fire, cellSize, cellSize);
                    b.Was.color = b.WasTint;
                    b.Fire.color = b.FireTint;
                }
                // THE FRONT. It waits on the edge for the CATCH, crosses over the HEAT, and is
                // past the far side by the time it SETTLES - so the cube is whole, then eaten,
                // then fire.
                float front = Mathf.Lerp(-Style.Band,
                    1f + Style.Band, Ease(Span(clock, b.Start + Style.Catch * 0.55f,
                        b.Start + Style.Catch + Style.Heat)));
                float settle = Span(clock, b.Start + Style.Catch + Style.Heat,
                    b.Start + life);
                float heat = Layers.ShowSettle ? Style.SettleHeat * (1f - settle) : 0f;
                Paint(b.Was, b, front, true, 0f);
                Paint(b.Fire, b, front, false, heat);
                if (settle >= 1f && b.Was != null)
                {
                    // Done: the board takes the cell back, and what it draws is the cube Core
                    // made when the joker fired.
                    Return(b.Was);
                    b.Was = null;
                    if (owner != null)
                    {
                        owner.ReleaseCells(new[] { b.Cell });
                        held.Remove(b.Cell);
                    }
                    Return(b.Fire);
                    b.Fire = null;
                    if (Layers.ShowEmbers && Layers.ShowSettle)
                    {
                        Scatter(b.At, clock, Style.SettleEmbers, 0.8f);
                    }
                }
            }
        }

        private void Paint(SpriteRenderer r, Burn b, float front, bool was, float heat)
        {
            if (r == null || BloomMaterial() == null)
            {
                // No shader: the old face simply gives way to the fire one. It still says the
                // cube turned, which is the sentence that matters.
                if (r != null)
                {
                    Color tint = was ? b.WasTint : b.FireTint;
                    float a = was ? Mathf.Clamp01(1f - front) : Mathf.Clamp01(front);
                    r.color = new Color(tint.r, tint.g, tint.b, a);
                }
                return;
            }
            block.Clear();
            block.SetFloat(FrontId, front);
            block.SetVector(DirId, new Vector4(b.Dir.x, b.Dir.y, 0f, 0f));
            block.SetFloat(BandId, Style.Band);
            block.SetFloat(RaggedId, Style.Ragged);
            block.SetFloat(InvertId, was ? 1f : 0f);
            block.SetFloat(SootId, Style.Soot);
            block.SetFloat(DrainId, Style.Drain);
            block.SetFloat(VeinsId, Style.Veins);
            block.SetFloat(HeatId, heat);
            block.SetFloat(SeedId, b.Seed);
            block.SetFloat(RimId, 1f);
            block.SetColor(RimColourId, Style.Flame);
            block.SetColor(CoreColourId, Style.Core);
            r.SetPropertyBlock(block);
        }

        private void PaintSpecks()
        {
            for (int i = 0; i < specks.Count; i++)
            {
                Speck s = specks[i];
                float k = Span(clock, s.Start, s.Start + s.Life);
                if (k <= 0f || k >= 1f)
                {
                    if (s.Body != null)
                    {
                        Return(s.Body);
                        s.Body = null;
                    }
                    continue;
                }
                if (s.Body == null)
                {
                    s.Body = Rent(i % 3 == 0 ? FireShapes.Spark : FireShapes.Ember, EmberOrder);
                }
                // Up and out quickly, then almost stopping - an ember has no weight to speak of
                // but it does not float away either.
                float ease = 1f - (1f - k) * (1f - k);
                Vector2 at = s.At + s.Drift * (Style.EmberRise * cellSize * ease);
                s.Body.transform.localPosition = at;
                float size = s.Size * (1f - k * 0.45f);
                Fit(s.Body, size, size);
                Color tint = i % 4 == 0 ? Style.Core : (i % 3 == 0 ? Style.Ember : Style.Flame);
                s.Body.color = new Color(tint.r, tint.g, tint.b, 1f - k * k);
            }
        }

        // =================================================================== helpers

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

        private static float Random01(GridPos cell, int salt)
        {
            uint v = (uint)(cell.X * 73856093 ^ cell.Y * 19349663 ^ salt * 83492791);
            v ^= v >> 13;
            v *= 1274126177u;
            v ^= v >> 16;
            return (v & 0xFFFFFF) / (float)0xFFFFFF;
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

        private static readonly int FrontId = Shader.PropertyToID("_Front");
        private static readonly int DirId = Shader.PropertyToID("_Dir");
        private static readonly int BandId = Shader.PropertyToID("_Band");
        private static readonly int RaggedId = Shader.PropertyToID("_Ragged");
        private static readonly int InvertId = Shader.PropertyToID("_Invert");
        private static readonly int SootId = Shader.PropertyToID("_Soot");
        private static readonly int DrainId = Shader.PropertyToID("_Drain");
        private static readonly int VeinsId = Shader.PropertyToID("_Veins");
        private static readonly int HeatId = Shader.PropertyToID("_Heat");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int RimId = Shader.PropertyToID("_Rim");
        private static readonly int RimColourId = Shader.PropertyToID("_RimColour");
        private static readonly int CoreColourId = Shader.PropertyToID("_CoreColour");

        private static Material bloom;

        private static bool looked;

        /// <summary>The FireBloom material, or null - the cube then simply gives way.</summary>
        public static Material BloomMaterial()
        {
            if (!looked)
            {
                looked = true;
                Shader shader = Shader.Find("ProjectBlock/FireBloom");
                if (shader == null)
                {
                    shader = Resources.Load<Shader>("Shaders/FireBloom");
                }
                if (shader != null && shader.isSupported)
                {
                    bloom = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
            }
            return bloom;
        }

        private Material plain;

        private SpriteRenderer Rent(Sprite sprite, int order)
        {
            return Rent(sprite, order, null);
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
                var go = new GameObject("FireFx");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plain == null)
                {
                    plain = r.sharedMaterial;
                }
            }
            Material wanted = material != null ? material : plain;
            if (wanted != null && r.sharedMaterial != wanted)
            {
                r.sharedMaterial = wanted;
            }
            r.SetPropertyBlock(null);
            r.sprite = sprite;
            r.sortingOrder = order;
            r.transform.localRotation = Quaternion.identity;
            r.color = Color.white;
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

        private TalismanTendril RentLick(int order)
        {
            TalismanTendril t = spareLicks.Count > 0
                ? spareLicks.Pop()
                : TalismanTendril.Make(transform, order);
            t.Order(order);
            t.Hide();
            return t;
        }

        private void ReturnLick(TalismanTendril t)
        {
            if (t == null)
            {
                return;
            }
            t.Hide();
            spareLicks.Push(t);
        }
    }

    /// <summary>The two silhouettes the fire's trimmings are made of: a soft EMBER and a small
    /// hard SPARK, plus a RIM for the lab's source/target marks. Baked once, shared.</summary>
    internal static class FireShapes
    {
        private const int Pixels = 48;

        private static Sprite ember;

        private static Sprite spark;

        private static Sprite rim;

        public static Sprite Ember
        {
            get
            {
                if (ember == null)
                {
                    ember = Bake("FireEmber", delegate (float x, float y)
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
                    });
                }
                return ember;
            }
        }

        public static Sprite Spark
        {
            get
            {
                if (spark == null)
                {
                    spark = Bake("FireSpark", delegate (float x, float y)
                    {
                        // A splinter rather than a dot: taller than it is wide, cut at the ends.
                        float d = Mathf.Abs(x) * 2.6f + Mathf.Abs(y) * 0.9f;
                        return d <= 0.72f ? 1f : Mathf.Clamp01((0.92f - d) / 0.2f);
                    });
                }
                return spark;
            }
        }

        public static Sprite Rim
        {
            get
            {
                if (rim == null)
                {
                    rim = Bake("FireRim", delegate (float x, float y)
                    {
                        float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                        return Mathf.Min(Mathf.Clamp01((d - 0.74f) / 0.1f),
                            Mathf.Clamp01((0.95f - d) / 0.08f));
                    });
                }
                return rim;
            }
        }

        private delegate float Field(float x, float y);

        private static Sprite Bake(string name, Field field)
        {
            var tex = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[Pixels * Pixels];
            float step = 2f / Pixels;
            for (int j = 0; j < Pixels; j++)
            {
                float y = -1f + (j + 0.5f) * step;
                for (int i = 0; i < Pixels; i++)
                {
                    float x = -1f + (i + 0.5f) * step;
                    pixels[j * Pixels + i] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(field(x, y)) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            Sprite made = Sprite.Create(tex, new Rect(0f, 0f, Pixels, Pixels),
                new Vector2(0.5f, 0.5f), Pixels);
            made.hideFlags = HideFlags.HideAndDontSave;
            return made;
        }
    }
}
