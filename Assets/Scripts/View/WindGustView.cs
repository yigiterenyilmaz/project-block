// PURPOSE: "Rüzgar" when it BLOWS - one use of the power, played once, about a second.
//
//   GUST     air streaks rush down the lane from the start to the end, and a front crosses it at
//            a steady speed; everything below begins when that front REACHES it, so the wind is
//            seen doing the carrying rather than everything happening at once.
//   EMBERS   each fire the front reaches throws its embers downwind on fluttering arcs. One that
//            found a block lands on it; one that did not is carried off past the lane and dies.
//   CATCH    a block an ember lands on burns into fire FROM THE SIDE THE EMBER CAME IN - Yangın's
//            own burn material (FireBloom), so a cube lit by the wind and one lit by the joker are
//            the same event. Until then the board is asked to stand back (HoldCells): Core has
//            already made it fire, and the cell underneath would give the answer away.
//   SPORES   an infection the wind carries leaves its cell as a mote and arrives on the block it
//            reaches; the new core is held back until it lands (HoldInfectionBirth).
//   WATER    is the board's own water animation (the push and then the fall, one sequence of
//            frames from Core) - started by the controller as the gust leaves.
//
// THE VIEW DECIDES NOTHING: which embers caught, where, what each cube was, which water moved and
// which infection went where are all WindVisuals (Core, reporting only, matched by identity). The
// only thing chosen here is where an ember that caught nothing flies off to, which is decoration.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class WindGustView : MonoBehaviour
    {
        public enum Cue
        {
            Gust,
            Ignite,
            Spore
        }

        public static class Style
        {
            public static readonly Color Air = new Color(0.82f, 0.95f, 1f);
            public static readonly Color Spore = new Color(0.6f, 0.95f, 0.45f);

            /// <summary>How long the front takes to cross the whole lane.</summary>
            public static float FrontTravel = 0.42f;

            public static int MinStreaks = 10;
            public static int MaxStreaks = 22;
            public static float StreakLife = 0.46f;
            public static float StreakAlpha = 0.8f;

            /// <summary>A beat between one ember of a fire and the next.</summary>
            public static float EmberStagger = 0.04f;
            public static float EmberFlight = 0.32f;
            public static float EmberEscape = 0.5f;
            public static float EmberSize = 0.16f;
            public static int EmberTrail = 4;

            /// <summary>The burn, in Yangın's beats.</summary>
            public static float Catch = 0.08f;
            public static float Heat = 0.26f;
            public static float Settle = 0.14f;

            public static float SporeFlight = 0.5f;
            public static float SporeSize = 0.2f;
        }

        public Action<Cue> Sounded;

        private const int WasOrder = 6;
        private const int FireOrder = 7;
        private const int StreakOrder = 9;
        private const int FlashOrder = 11;
        private const int MoteOrder = 12;

        private sealed class Streak
        {
            public float Start;
            public float Across;
            public float Length;
            public float Thick;
            public SpriteRenderer R;
        }

        private sealed class Ember
        {
            public Vector2 P0;
            public Vector2 P1;
            public Vector2 P2;
            public float Start;
            public float Life;
            public bool Caught;
            public float Seed;
            public SpriteRenderer Head;
            public readonly List<SpriteRenderer> Trail = new List<SpriteRenderer>();
        }

        private sealed class Burn
        {
            public GridPos Cell;
            public Vector2 Dir;
            public float Land;
            public float Seed;
            public Sprite WasFace;
            public Color WasTint;
            public Sprite FireFace;
            public Color FireTint;
            public SpriteRenderer Was;
            public SpriteRenderer Fire;
            public SpriteRenderer Flash;
            public bool Done;
        }

        private sealed class Spore
        {
            public Vector2 P0;
            public Vector2 P1;
            public Vector2 P2;
            public float Start;
            public SpriteRenderer Head;
            public SpriteRenderer Pulse;
            public readonly List<SpriteRenderer> Trail = new List<SpriteRenderer>();
        }

        /// <summary>A cube's face as the board drew it.</summary>
        public struct Face
        {
            public Sprite Tile;
            public Color Colour;
        }

        private BoardView view;
        private WindGust gust;
        private WindVisuals lastPlayed;
        private float clock = -1f;
        private float span;
        private bool ignitedSound;
        private readonly List<Streak> streaks = new List<Streak>();
        private readonly List<Ember> embers = new List<Ember>();
        private readonly List<Burn> burns = new List<Burn>();
        private readonly List<Spore> spores = new List<Spore>();
        private readonly List<GridPos> held = new List<GridPos>();
        private SpriteRenderer lane;
        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();
        private Material plain;
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
        /// Plays one gust. Keyed on the report ITSELF, so a repaint can never restart it.
        /// <paramref name="before"/> is every cube in the lane as the board drew it BEFORE the
        /// rules ran - the face a block burns FROM is gone from the board by now.
        /// </summary>
        public void Play(BoardView on, WindVisuals report, IDictionary<GridPos, Face> before)
        {
            if (on == null || report == null || report.Gust == null || !report.Gust.Valid
                || ReferenceEquals(report, lastPlayed))
            {
                return;
            }
            Stop();
            lastPlayed = report;
            view = on;
            gust = report.Gust;
            clock = 0f;
            span = Style.FrontTravel + Style.StreakLife;
            ignitedSound = false;

            float laneLength = gust.Length + 1f;
            int count = Mathf.Clamp(Mathf.RoundToInt(6f + laneLength * 1.8f), Style.MinStreaks,
                Style.MaxStreaks);
            for (int i = 0; i < count; i++)
            {
                float h = Hash(i, 3);
                streaks.Add(new Streak
                {
                    Start = h * 0.22f,
                    Across = (Hash(i, 5) * 2f - 1f) * 1.35f,
                    Length = Mathf.Lerp(1.1f, 2.3f, Hash(i, 7)),
                    Thick = Mathf.Lerp(0.07f, 0.15f, Hash(i, 11))
                });
            }

            // EMBERS, each leaving when the front reaches its fire.
            var perSource = new Dictionary<GridPos, int>();
            var landing = new Dictionary<GridPos, Ember>();
            for (int i = 0; i < report.Embers.Count; i++)
            {
                WindEmber e = report.Embers[i];
                int k;
                perSource.TryGetValue(e.Source, out k);
                perSource[e.Source] = k + 1;
                var from = new Vector2(e.Source.X, e.Source.Y);
                float seed = Hash(e.Source.X * 31 + e.Source.Y * 7 + k * 3, 13);
                var ember = new Ember
                {
                    P0 = from,
                    Start = FrontAt(e.Source) + 0.04f + k * Style.EmberStagger,
                    Caught = e.Target.HasValue,
                    Seed = seed
                };
                Vector2 dir = new Vector2(gust.DirX, gust.DirY);
                Vector2 side = new Vector2(-dir.y, dir.x);
                if (e.Target.HasValue)
                {
                    ember.P2 = new Vector2(e.Target.Value.X, e.Target.Value.Y);
                    float dist = (ember.P2 - from).magnitude;
                    ember.Life = Style.EmberFlight + 0.05f * Mathf.Sqrt(dist);
                    ember.P1 = (from + ember.P2) * 0.5f + side * ((seed - 0.5f) * 1.3f) + dir * 0.4f;
                    Ember first;
                    if (!landing.TryGetValue(e.Target.Value, out first)
                        || ember.Start + ember.Life < first.Start + first.Life)
                    {
                        landing[e.Target.Value] = ember;
                    }
                }
                else
                {
                    // Carried off past the lane - where to is decoration only.
                    ember.P2 = new Vector2(gust.EndX, gust.EndY) + dir * (0.8f + seed * 0.9f)
                        + side * ((Hash(i, 17) - 0.5f) * 2.4f);
                    ember.P1 = (from + ember.P2) * 0.5f + side * ((seed - 0.5f) * 1.6f);
                    ember.Life = Style.EmberEscape;
                }
                embers.Add(ember);
                span = Mathf.Max(span, ember.Start + ember.Life);
            }

            // THE CATCHES. The fire face is read before the cells are held back.
            foreach (SpreadIgnition lit in report.Ignitions)
            {
                Ember first;
                if (!landing.TryGetValue(lit.Cell, out first))
                {
                    continue;
                }
                Face was;
                if (before == null || !before.TryGetValue(lit.Cell, out was))
                {
                    was = new Face { Tile = view.FaceOf(lit.Was), Colour = Color.white };
                }
                Sprite fireTile;
                Color fireColour;
                if (!view.TryCubeLook(lit.Cell, 0f, out fireTile, out fireColour))
                {
                    fireTile = view.FaceOf(new Cube(CubeKind.Fire, lit.Was.SourceCardId));
                    fireColour = Color.white;
                }
                Vector2 approach = first.P2 - first.P1;
                var burn = new Burn
                {
                    Cell = lit.Cell,
                    Dir = approach.sqrMagnitude > 1e-6f ? approach.normalized : new Vector2(gust.DirX, gust.DirY),
                    Land = first.Start + first.Life,
                    Seed = Hash(lit.Cell.X * 17 + lit.Cell.Y, 19),
                    WasFace = was.Tile,
                    WasTint = was.Colour,
                    FireFace = fireTile,
                    FireTint = fireColour
                };
                burns.Add(burn);
                held.Add(lit.Cell);
                span = Mathf.Max(span, burn.Land + Style.Catch + Style.Heat + Style.Settle);
            }

            // THE SPORES.
            var newCores = new List<GridPos>();
            foreach (WindCarry carry in gust.Carries)
            {
                var from = new Vector2(carry.From.X, carry.From.Y);
                var to = new Vector2(carry.To.X, carry.To.Y);
                Vector2 side = new Vector2(-gust.DirY, gust.DirX);
                var spore = new Spore
                {
                    P0 = from,
                    P2 = to,
                    P1 = (from + to) * 0.5f + side * 0.55f,
                    Start = FrontAt(carry.From) + 0.06f
                };
                spores.Add(spore);
                newCores.Add(carry.To);
                span = Mathf.Max(span, spore.Start + Style.SporeFlight + 0.3f);
            }

            if (held.Count > 0)
            {
                view.HoldCells(held);
            }
            if (newCores.Count > 0)
            {
                float arrive = 0f;
                foreach (Spore s in spores)
                {
                    arrive = Mathf.Max(arrive, s.Start + Style.SporeFlight);
                }
                view.HoldInfectionBirth(newCores, arrive);
            }
            Sound(Cue.Gust);
        }

        public void Stop()
        {
            if (view != null && held.Count > 0)
            {
                view.ReleaseCells(held);
            }
            held.Clear();
            foreach (Streak s in streaks)
            {
                Return(s.R);
            }
            streaks.Clear();
            foreach (Ember e in embers)
            {
                Return(e.Head);
                foreach (SpriteRenderer r in e.Trail)
                {
                    Return(r);
                }
            }
            embers.Clear();
            foreach (Burn b in burns)
            {
                Return(b.Was);
                Return(b.Fire);
                Return(b.Flash);
            }
            burns.Clear();
            foreach (Spore s in spores)
            {
                Return(s.Head);
                Return(s.Pulse);
                foreach (SpriteRenderer r in s.Trail)
                {
                    Return(r);
                }
            }
            spores.Clear();
            if (lane != null)
            {
                lane.enabled = false;
            }
            clock = -1f;
        }

        private void Update()
        {
            if (clock < 0f)
            {
                return;
            }
            if (view == null || view.Board == null)
            {
                Stop();
                return;
            }
            clock += Time.deltaTime;
            float cell = view.CellSizeInWorld;
            PaintLane(cell);
            PaintStreaks(cell);
            PaintEmbers(cell);
            PaintBurns(cell);
            PaintSpores(cell);
            if (clock > span + 0.05f)
            {
                Stop();
            }
        }

        // ------------------------------------------------------------------ the beats

        /// <summary>When the front reaches a cell.</summary>
        private float FrontAt(GridPos cell)
        {
            float along = Mathf.Clamp(gust.Along(cell) + 0.5f, 0f, gust.Length + 1f);
            return Style.FrontTravel * along / (gust.Length + 1f);
        }

        private Vector2 World(Vector2 board)
        {
            return view.BoardPointToWorld(board.x, board.y);
        }

        private float Angle()
        {
            Vector2 a = World(new Vector2(gust.StartX, gust.StartY));
            Vector2 b = World(new Vector2(gust.EndX, gust.EndY));
            return Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        }

        private void PaintLane(float cell)
        {
            // A breath of the lane as the gust passes - the aim's band, lit once and let go.
            float k = clock / (Style.FrontTravel + 0.3f);
            if (k >= 1f)
            {
                if (lane != null)
                {
                    lane.enabled = false;
                }
                return;
            }
            if (lane == null || !lane.enabled)
            {
                lane = Rent(WindShapes.Band, StreakOrder - 1, false);
            }
            float alpha = 0.16f * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
            var mid = new Vector2(gust.StartX + gust.DirX * gust.Length * 0.5f,
                gust.StartY + gust.DirY * gust.Length * 0.5f);
            Place(lane, World(mid), Angle(), cell * (gust.Length + 1f), cell * WindGust.Width,
                Tint(Style.Air, alpha));
        }

        private void PaintStreaks(float cell)
        {
            float laneLength = gust.Length + 1f;
            float angle = Angle();
            var dir = new Vector2(gust.DirX, gust.DirY);
            var side = new Vector2(-dir.y, dir.x);
            var start = new Vector2(gust.StartX, gust.StartY);
            foreach (Streak s in streaks)
            {
                float k = (clock - s.Start) / Style.StreakLife;
                if (k <= 0f || k >= 1f)
                {
                    if (s.R != null)
                    {
                        Return(s.R);
                        s.R = null;
                    }
                    continue;
                }
                if (s.R == null)
                {
                    s.R = Rent(WindShapes.Streak, StreakOrder, true);
                }
                // fast in, easing out - a gust, not a conveyor
                float travel = 1f - (1f - k) * (1f - k);
                float along = -0.5f - s.Length * 0.5f + travel * (laneLength + s.Length);
                Vector2 at = start + dir * along + side * s.Across;
                float fade = Mathf.Sin(k * Mathf.PI);
                Place(s.R, World(at), angle, cell * s.Length, cell * s.Thick * 3f,
                    Tint(Style.Air, Style.StreakAlpha * fade));
            }
        }

        private void PaintEmbers(float cell)
        {
            var dir = new Vector2(gust.DirX, gust.DirY);
            var side = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < embers.Count; i++)
            {
                Ember e = embers[i];
                float k = (clock - e.Start) / e.Life;
                if (k <= 0f || k >= 1f)
                {
                    if (e.Head != null)
                    {
                        Return(e.Head);
                        e.Head = null;
                        foreach (SpriteRenderer r in e.Trail)
                        {
                            Return(r);
                        }
                        e.Trail.Clear();
                    }
                    continue;
                }
                if (e.Head == null)
                {
                    e.Head = Rent(i % 3 == 0 ? FireShapes.Spark : FireShapes.Ember, MoteOrder, true);
                    for (int j = 0; j < Style.EmberTrail; j++)
                    {
                        e.Trail.Add(Rent(FireShapes.Ember, MoteOrder - 1, true));
                    }
                }
                float alpha = e.Caught ? Mathf.Clamp01(k * 6f) : Mathf.Clamp01(k * 6f) * (1f - Mathf.Clamp01((k - 0.55f) / 0.45f));
                for (int j = -1; j < e.Trail.Count; j++)
                {
                    float kk = Mathf.Clamp01(k - (j + 1) * 0.045f);
                    Vector2 p = Bezier(e.P0, e.P1, e.P2, Ease(kk));
                    // the flutter: a ember rides the air, it does not fly a rail
                    p += side * (Mathf.Sin((kk * 3.2f + e.Seed) * Mathf.PI * 2f) * 0.09f * (1f - kk));
                    SpriteRenderer r = j < 0 ? e.Head : e.Trail[j];
                    float size = j < 0 ? Style.EmberSize : Style.EmberSize * (0.8f - j * 0.15f);
                    Color c = j < 0 ? FireSpreadView.Style.Core
                        : Color.Lerp(FireSpreadView.Style.Flame, FireSpreadView.Style.Deep, j / (float)e.Trail.Count);
                    float a = j < 0 ? alpha : alpha * (0.55f - j * 0.12f);
                    Place(r, World(p), (clock * 400f + e.Seed * 360f) % 360f, cell * size, cell * size, Tint(c, a));
                }
            }
        }

        private void PaintBurns(float cell)
        {
            Material bloom = FireSpreadView.BloomMaterial();
            foreach (Burn b in burns)
            {
                float life = Style.Catch + Style.Heat + Style.Settle;
                // The old face stands from the very start: the cell is held, and it has to show the
                // block that is there until the ember gets to it.
                if (b.Was == null && !b.Done)
                {
                    b.Was = Rent(b.WasFace, WasOrder, false, bloom != null ? bloom : ViewUtil.TileMaterial(b.WasFace));
                    b.Fire = Rent(b.FireFace, FireOrder, false, bloom != null ? bloom : ViewUtil.TileMaterial(b.FireFace));
                    Vector2 at = World(new Vector2(b.Cell.X, b.Cell.Y));
                    // at the size the board draws a cube, so the hand-back shows nothing
                    float cube = view.CubeWorldSize * Mathf.Abs(view.transform.lossyScale.x);
                    Place(b.Was, at, 0f, cube, cube, b.WasTint);
                    Place(b.Fire, at, 0f, cube, cube, b.FireTint);
                }
                if (b.Done)
                {
                    continue;
                }
                float k = (clock - b.Land) / life;
                float front = Mathf.Lerp(-FireSpreadView.Style.Band, 1f + FireSpreadView.Style.Band,
                    Ease(Mathf.Clamp01((clock - b.Land - Style.Catch * 0.55f) / (Style.Catch + Style.Heat))));
                float settle = Mathf.Clamp01((clock - b.Land - Style.Catch - Style.Heat) / Style.Settle);
                float heat = FireSpreadView.Style.SettleHeat * (1f - settle) * (k > 0f ? 1f : 0f);
                PaintFace(b.Was, b, front, true, 0f, bloom);
                PaintFace(b.Fire, b, front, false, heat, bloom);

                // the landing flash: hot, inside its own cell, gone fast
                float f = (clock - b.Land) / 0.2f;
                if (f > 0f && f < 1f)
                {
                    if (b.Flash == null)
                    {
                        b.Flash = Rent(FireShapes.Ember, FlashOrder, true);
                        if (!ignitedSound)
                        {
                            ignitedSound = true;
                            Sound(Cue.Ignite);
                        }
                    }
                    float size = cell * Mathf.Lerp(0.55f, 0.95f, f);
                    Place(b.Flash, World(new Vector2(b.Cell.X, b.Cell.Y)), 0f, size, size,
                        Tint(FireSpreadView.Style.Flame, 0.85f * (1f - f)));
                }
                else if (b.Flash != null && f >= 1f)
                {
                    Return(b.Flash);
                    b.Flash = null;
                }
                if (k >= 1f)
                {
                    b.Done = true;
                    Return(b.Was);
                    Return(b.Fire);
                    b.Was = null;
                    b.Fire = null;
                    if (held.Remove(b.Cell))
                    {
                        view.ReleaseCells(new[] { b.Cell });
                    }
                }
            }
        }

        private void PaintFace(SpriteRenderer r, Burn b, float front, bool was, float heat, Material bloom)
        {
            if (r == null)
            {
                return;
            }
            if (bloom == null)
            {
                // No shader: the old face simply gives way to the fire one.
                Color tint = was ? b.WasTint : b.FireTint;
                float a = was ? Mathf.Clamp01(1f - front) : Mathf.Clamp01(front);
                r.color = new Color(tint.r, tint.g, tint.b, tint.a * a);
                return;
            }
            block.Clear();
            block.SetFloat(FrontId, front);
            block.SetVector(DirId, new Vector4(b.Dir.x, b.Dir.y, 0f, 0f));
            block.SetFloat(BandId, FireSpreadView.Style.Band);
            block.SetFloat(RaggedId, FireSpreadView.Style.Ragged);
            block.SetFloat(InvertId, was ? 1f : 0f);
            block.SetFloat(SootId, FireSpreadView.Style.Soot);
            block.SetFloat(DrainId, FireSpreadView.Style.Drain);
            block.SetFloat(VeinsId, FireSpreadView.Style.Veins);
            block.SetFloat(HeatId, heat);
            block.SetFloat(SeedId, b.Seed);
            block.SetFloat(RimId, 1f);
            block.SetColor(RimColourId, FireSpreadView.Style.Flame);
            block.SetColor(CoreColourId, FireSpreadView.Style.Core);
            r.SetPropertyBlock(block);
        }

        private void PaintSpores(float cell)
        {
            foreach (Spore s in spores)
            {
                float k = (clock - s.Start) / Style.SporeFlight;
                if (k > 0f && k < 1f)
                {
                    if (s.Head == null)
                    {
                        s.Head = Rent(FireShapes.Ember, MoteOrder, true);
                        for (int j = 0; j < 3; j++)
                        {
                            s.Trail.Add(Rent(FireShapes.Ember, MoteOrder - 1, true));
                        }
                        Sound(Cue.Spore);
                    }
                    for (int j = -1; j < s.Trail.Count; j++)
                    {
                        float kk = Mathf.Clamp01(k - (j + 1) * 0.06f);
                        Vector2 p = Bezier(s.P0, s.P1, s.P2, Ease(kk));
                        SpriteRenderer r = j < 0 ? s.Head : s.Trail[j];
                        float size = cell * Style.SporeSize * (j < 0 ? 1f : 0.75f - j * 0.15f);
                        float a = Mathf.Clamp01(k * 5f) * (j < 0 ? 0.95f : 0.45f - j * 0.1f);
                        Place(r, World(p), 0f, size, size, Tint(Style.Spore, a));
                    }
                }
                else if (s.Head != null && k >= 1f)
                {
                    Return(s.Head);
                    s.Head = null;
                    foreach (SpriteRenderer r in s.Trail)
                    {
                        Return(r);
                    }
                    s.Trail.Clear();
                }
                // the arrival: a ring that opens on the block and is gone
                float p2 = (clock - s.Start - Style.SporeFlight) / 0.3f;
                if (p2 > 0f && p2 < 1f)
                {
                    if (s.Pulse == null)
                    {
                        s.Pulse = Rent(WindShapes.Ring, FlashOrder, true);
                    }
                    float size = cell * Mathf.Lerp(0.4f, 0.95f, Ease(p2));
                    Place(s.Pulse, World(s.P2), 0f, size, size, Tint(Style.Spore, 0.8f * (1f - p2)));
                }
                else if (s.Pulse != null && p2 >= 1f)
                {
                    Return(s.Pulse);
                    s.Pulse = null;
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        private void Sound(Cue cue)
        {
            if (Sounded != null)
            {
                Sounded(cue);
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return a * (u * u) + b * (2f * u * t) + c * (t * t);
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static Color Tint(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
        }

        private static float Hash(int a, int b)
        {
            uint v = (uint)(a * 73856093) ^ (uint)(b * 19349663);
            v ^= v >> 13;
            v *= 1274126177u;
            v ^= v >> 16;
            return (v & 0xFFFFFF) / (float)0xFFFFFF;
        }

        private SpriteRenderer Rent(Sprite sprite, int order, bool glow)
        {
            return Rent(sprite, order, glow, null);
        }

        private SpriteRenderer Rent(Sprite sprite, int order, bool glow, Material material)
        {
            SpriteRenderer r;
            if (spare.Count > 0)
            {
                r = spare.Pop();
            }
            else
            {
                var go = new GameObject("WindFx");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plain == null)
                {
                    plain = r.sharedMaterial;
                }
            }
            Material wanted = material != null ? material : glow && WindFx.Glow != null ? WindFx.Glow : plain;
            if (wanted != null && r.sharedMaterial != wanted)
            {
                r.sharedMaterial = wanted;
            }
            r.SetPropertyBlock(null);
            r.sprite = sprite;
            r.sortingOrder = order;
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

        private static void Place(SpriteRenderer r, Vector2 at, float angle, float width, float height, Color colour)
        {
            if (r == null || r.sprite == null)
            {
                return;
            }
            r.transform.position = new Vector3(at.x, at.y, 0f);
            r.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            Vector2 unit = r.sprite.bounds.size;
            r.transform.localScale = new Vector3(width / Mathf.Max(unit.x, 0.0001f),
                height / Mathf.Max(unit.y, 0.0001f), 1f);
            r.color = colour;
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
    }
}
