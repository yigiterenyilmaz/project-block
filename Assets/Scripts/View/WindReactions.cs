// PURPOSE: HOW EACH MATERIAL ANSWERS THE WIND. The storm (WindStormView) is one airflow with one
// clock; this file is what that airflow does to the things it reaches - and it is a LAYER, not three
// hard-coded effects: Core says how a thing reacts (WindReactionKind) and what became of it, and a
// reaction here turns that into a presentation through five moments every one of them shares:
//
//   OnWindPreview              the aim: "this will be affected" - never where it will end up
//   OnWindContact              the storm front reaches it (never at cast time)
//   CreateWindTransportVisual  what it gives to the wind leaves it
//   OnWindArrival              that lands
//   OnWindFinished             everything it borrowed from the board is handed back
//
//   FIRE (ParticleTransfer)   the cube stays. Its flame is laid over along the wind, torn scraps
//                             and embers leave it, ride the flow to the block Core chose, and that
//                             block HEATS before it burns: a hot spot where the ember struck, the
//                             heat crossing the face from that side (Yangın's own FireBloom, so a
//                             cube lit by the wind and one lit by the joker are one event), flame
//                             tongues, a flare, then an ordinary fire cube.
//   WATER (PhysicalMove)      the cube is pressed first - thin on its leading edge, bulged behind -
//                             then leaves its cell as a proxy and GLIDES the route Core reported
//                             (the push, then the fall) in one motion, never cell by cell, and
//                             lands with a compression and a few drops.
//   INFECTION (DuplicateSpread) the core is squeezed, a piece of it tears off with a stream of
//                             spores, rides to the block Core named, stains inward from where it
//                             struck, and the new core grows there while the source recovers.
//   A BYSTANDER (None/LeanOnly) only catches the front on its windward edge. It never moves:
//                             nothing in the rules moved it.
//
// THE VIEW DECIDES NOTHING here either: every destination, route and face is the report's. The one
// thing a reaction invents is where an ember that caught nothing drifts off to.
// EXTENSION POINT: a new carried thing is a WindReaction subclass and a case in the storm's Build.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    internal interface IWindReactiveVisual
    {
        WindReactionKind Reaction { get; }
        GridPos Cell { get; }

        /// <summary>When its readable part is over, and when all of it is.</summary>
        float HeroEnd { get; }
        float End { get; }

        void OnWindPreview(WindStage stage);
        void OnWindContact(WindStage stage);
        void CreateWindTransportVisual(WindStage stage);
        void OnWindArrival(WindStage stage);
        void OnWindFinished(WindStage stage);

        /// <summary>One frame of the cast.</summary>
        void Tick(WindStage stage);

        /// <summary>Hands every sprite back at once (a stop, or the aim going away).</summary>
        void Release(WindStage stage);

        bool TryBeat(WindBeat beat, out float from, out float to);
    }

    /// <summary>What a reaction needs to know about the storm it is in. One per aim or cast.</summary>
    internal sealed class WindStage
    {
        public BoardView View;
        public WindGust Gust;
        public WindFx.Clock Clock;
        public WindSprites Sprites;
        public WindIgnitions Ignitions;

        /// <summary>One cell's edge, one cube's edge and one screen pixel, in world units.</summary>
        public float Cell;
        public float Cube;
        public float Pixel;

        /// <summary>The wind's direction on screen, the side across it, and its angle.</summary>
        public Vector2 Dir;
        public Vector2 Side;
        public float Angle;

        /// <summary>Seconds since the cast began (or since the aim came up).</summary>
        public float Now;
        public int Seed;

        public Action<WindCue> Sound;
        public Action<WindHaptic> Haptic;

        private readonly HashSet<GridPos> held = new HashSet<GridPos>();

        public Vector2 World(Vector2 board)
        {
            return View.BoardPointToWorld(board.x, board.y);
        }

        public Vector2 World(GridPos cell)
        {
            return View.BoardPointToWorld(cell.X, cell.Y);
        }

        /// <summary>The shared airflow's sideways push at a board point, in world units.</summary>
        public Vector2 Drift(Vector2 board, int seed)
        {
            float along = (board.x - Gust.StartX) * Gust.DirX + (board.y - Gust.StartY) * Gust.DirY;
            return Side * (WindFx.Turbulence(along, Now, seed) * Cell);
        }

        public float Along(GridPos cell)
        {
            return Gust.Along(cell);
        }

        public void Cue(WindCue cue)
        {
            if (Sound != null)
            {
                Sound(cue);
            }
        }

        public void Tap(WindHaptic beat)
        {
            if (Haptic != null)
            {
                Haptic(beat);
            }
        }

        /// <summary>Asks the board to stand back from a cell (Core has already changed it).</summary>
        public void Hold(GridPos cell)
        {
            if (held.Add(cell))
            {
                View.HoldCells(new[] { cell });
            }
        }

        public void Unhold(GridPos cell)
        {
            if (held.Remove(cell))
            {
                View.ReleaseCells(new[] { cell });
            }
        }

        public void UnholdAll()
        {
            if (held.Count > 0 && View != null)
            {
                View.ReleaseCells(new List<GridPos>(held));
            }
            held.Clear();
        }
    }

    /// <summary>The shared sequencing: the four once-only moments fire off the storm's clock.</summary>
    internal abstract class WindReaction : IWindReactiveVisual
    {
        public WindReactionKind Reaction { get; protected set; }
        public GridPos Cell { get; protected set; }
        public float HeroEnd { get; protected set; }
        public float End { get; protected set; }

        protected float ContactTime;
        protected float SendTime = float.MaxValue;
        protected float ArriveTime = float.MaxValue;

        private bool contacted;
        private bool sent;
        private bool arrived;
        private bool finished;

        public void Tick(WindStage stage)
        {
            if (!contacted && stage.Now >= ContactTime)
            {
                contacted = true;
                OnWindContact(stage);
            }
            if (!sent && stage.Now >= SendTime)
            {
                sent = true;
                CreateWindTransportVisual(stage);
            }
            if (!arrived && stage.Now >= ArriveTime)
            {
                arrived = true;
                OnWindArrival(stage);
            }
            if (!finished)
            {
                Paint(stage);
                if (stage.Now >= End)
                {
                    finished = true;
                    OnWindFinished(stage);
                }
            }
        }

        public virtual void OnWindPreview(WindStage stage)
        {
        }

        public virtual void OnWindContact(WindStage stage)
        {
        }

        public virtual void CreateWindTransportVisual(WindStage stage)
        {
        }

        public virtual void OnWindArrival(WindStage stage)
        {
        }

        public virtual void OnWindFinished(WindStage stage)
        {
            Release(stage);
        }

        public abstract void Release(WindStage stage);

        protected virtual void Paint(WindStage stage)
        {
        }

        public virtual bool TryBeat(WindBeat beat, out float from, out float to)
        {
            from = 0f;
            to = 0f;
            return false;
        }

        protected static bool Beat(float a, float b, out float from, out float to)
        {
            from = a;
            to = b;
            return true;
        }

        protected static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return a * (u * u) + b * (2f * u * t) + c * (t * t);
        }

        protected static float Span(float now, float from, float length)
        {
            return length <= 0.0001f ? (now >= from ? 1f : 0f) : Mathf.Clamp01((now - from) / length);
        }

        protected static float Bell(float k)
        {
            return Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
        }

        // ---- the aim's shared mark: a faint light on the edge the wind will leave by ----
        protected SpriteRenderer previewEdge;
        protected SpriteRenderer previewEdge2;

        protected void PaintPreviewEdge(WindStage stage, float strength)
        {
            if (previewEdge == null)
            {
                previewEdge = stage.Sprites.RentGlow(WindShapes.Edge, WindOrders.Bystander);
                previewEdge2 = stage.Sprites.RentGlow(WindShapes.Edge, WindOrders.Bystander);
            }
            float pulse = 0.55f + 0.45f * Mathf.Sin(stage.Now * 4.2f + Cell.X * 1.3f + Cell.Y * 0.7f);
            PlaceEdges(previewEdge, previewEdge2, stage.World(Cell), stage.Dir, stage.Cube,
                WindFx.Tint(WindFx.Style.Air, 0.16f * strength * pulse));
        }

        /// <summary>
        /// Light (or shade) on the side of a cube facing <paramref name="toward"/>. A cube is a
        /// SQUARE and the wind blows at any angle, so the edge sprite is never turned to the wind:
        /// turned 27 degrees it is a tilted square with its corners on the neighbours. It is laid
        /// on the cube's own left/right side and its own top/bottom side, each weighted by how
        /// much of the wind runs that way - which stays inside the cube at every angle.
        /// </summary>
        protected static void PlaceEdges(SpriteRenderer alongX, SpriteRenderer alongY, Vector2 at, Vector2 toward,
            float cube, Color colour)
        {
            float ax = Mathf.Abs(toward.x);
            float ay = Mathf.Abs(toward.y);
            WindSprites.Place(alongX, at, toward.x >= 0f ? 0f : 180f, cube, cube,
                new Color(colour.r, colour.g, colour.b, colour.a * ax));
            WindSprites.Place(alongY, at, toward.y >= 0f ? 90f : 270f, cube, cube,
                new Color(colour.r, colour.g, colour.b, colour.a * ay));
        }
    }

    /// <summary>The wind's sorting orders: the corridor under everything it carries, water with
    /// the board's own objects, fire and spores above the cubes.</summary>
    internal static class WindOrders
    {
        public const int Corridor = 5;
        public const int Bystander = 6;
        public const int IgniteWas = 6;
        public const int IgniteFire = 7;
        public const int WaterTrail = 7;
        public const int Water = 8;
        public const int Ribbon = 10;
        public const int Mote = 11;
        public const int Spore = 12;
        public const int Ember = 13;
        public const int Aim = 14;
        public const int Debug = 30;
    }

    // ================================================================== FIRE

    /// <summary>One fire in the wind: the lean, and every ember it throws.</summary>
    internal sealed class WindFireReaction : WindReaction
    {
        private sealed class Scrap
        {
            public SpriteRenderer Outer;
            public SpriteRenderer Core;
            public Vector2 From;
            public Vector2 Control;
            public Vector2 To;
            public float Start;
            public float Life;
            public float Size;
            public bool Fades;
            public bool Tiny;
            public int Seed;
        }

        private sealed class Smear
        {
            public SpriteRenderer Body;
            public Scrap Follows;
        }

        private readonly List<WindEmber> embers;
        private readonly List<Scrap> scraps = new List<Scrap>();
        private readonly List<Smear> smears = new List<Smear>();
        private readonly List<SpriteRenderer> tongues = new List<SpriteRenderer>();
        private readonly int heroes;
        private readonly int tinies;
        private float leanEnd;
        private float firstTear;
        private float firstLanding = float.MaxValue;
        private bool hissed;

        /// <summary>The aim's fire: it only leans.</summary>
        public WindFireReaction(GridPos cell, WindReactionKind reaction)
        {
            Cell = cell;
            Reaction = reaction;
            embers = new List<WindEmber>();
        }

        public WindFireReaction(WindStage stage, GridPos cell, List<WindEmber> thrown, int heroPerEmber,
            int tinyPerEmber)
        {
            Cell = cell;
            Reaction = WindReactionKind.ParticleTransfer;
            embers = thrown;
            heroes = heroPerEmber;
            tinies = tinyPerEmber;
            ContactTime = stage.Clock.ReachTime(stage.Along(cell));
            firstTear = ContactTime + 0.05f;
            SendTime = firstTear;
            float last = firstTear;
            var from = new Vector2(cell.X, cell.Y);
            var dir = new Vector2(stage.Gust.DirX, stage.Gust.DirY);
            var side = new Vector2(-dir.y, dir.x);
            for (int e = 0; e < embers.Count; e++)
            {
                WindEmber ember = embers[e];
                float tear = firstTear + e * 0.045f;
                bool caught = ember.Target.HasValue;
                Vector2 to;
                float flight;
                float sway = WindFx.Hash(ember.Seed, 3) - 0.5f;
                if (caught)
                {
                    to = new Vector2(ember.Target.Value.X, ember.Target.Value.Y);
                    float dist = (to - from).magnitude;
                    flight = Mathf.Clamp(WindFx.Style.EmberFlightMin + 0.04f * dist,
                        WindFx.Style.EmberFlightMin, WindFx.Style.EmberFlightMax);
                }
                else
                {
                    // carried off past the lane: where to is decoration, never a rule
                    to = new Vector2(stage.Gust.EndX, stage.Gust.EndY) + dir * (0.9f + WindFx.Hash(ember.Seed, 5) * 0.9f)
                        + side * (sway * 2.6f);
                    flight = WindFx.Style.EmberFlightMax;
                }
                Vector2 mid = (from + to) * 0.5f;
                int count = heroes + tinies;
                for (int j = 0; j < count; j++)
                {
                    bool tiny = j >= heroes;
                    float h = WindFx.Hash(ember.Seed + j * 17, 7);
                    float h2 = WindFx.Hash(ember.Seed + j * 29, 11);
                    // each scrap strikes its own part of the block: a hot spot per hero
                    Vector2 hit = caught
                        ? to + new Vector2(h - 0.5f, h2 - 0.5f) * (tiny ? 0.5f : 0.42f)
                        : to + new Vector2(h - 0.5f, h2 - 0.5f) * 1.1f;
                    var scrap = new Scrap
                    {
                        From = from + new Vector2(h2 - 0.5f, 0.12f + h * 0.2f) * 0.5f,
                        To = hit,
                        Control = mid + side * (sway * 0.9f + (h - 0.5f) * 0.5f) + dir * 0.25f,
                        Start = tear + j * 0.018f,
                        Life = flight * (tiny ? 0.85f + h * 0.4f : 1f),
                        Size = tiny ? 0.08f + h * 0.05f : 0.26f + h * 0.1f,
                        Fades = !caught,
                        Tiny = tiny,
                        Seed = ember.Seed + j
                    };
                    scraps.Add(scrap);
                    last = Mathf.Max(last, scrap.Start + scrap.Life);
                    if (caught && !tiny)
                    {
                        float land = scrap.Start + scrap.Life;
                        firstLanding = Mathf.Min(firstLanding, land);
                        stage.Ignitions.Hit(ember.Target.Value, land, hit, (hit - scrap.Control).normalized);
                    }
                    if (j == 0 && WindFx.Trails)
                    {
                        smears.Add(new Smear { Follows = scrap });
                    }
                }
            }
            leanEnd = firstTear + embers.Count * 0.045f + 0.08f;
            HeroEnd = last;
            End = last + 0.02f;
        }

        public override void OnWindPreview(WindStage stage)
        {
            float strength = Reaction == WindReactionKind.ParticleTransfer ? 1f : 0.55f;
            PaintPreviewEdge(stage, strength);
            // the flame leans a little the way the wind will blow, before anything is cast
            PaintTongues(stage, 2, 0.32f * strength, 0.9f, 0.38f * strength);
        }

        public override void OnWindContact(WindStage stage)
        {
            stage.Cue(WindCue.FireContact);
        }

        public override void CreateWindTransportVisual(WindStage stage)
        {
            stage.Cue(WindCue.EmberTear);
        }

        protected override void Paint(WindStage stage)
        {
            // THE LEAN: hard over as the front hits, held while it is being torn, let go after.
            float into = Span(stage.Now, ContactTime, 0.06f);
            float outOf = Span(stage.Now, leanEnd, 0.16f);
            float lean = WindFx.Smooth(into) * (1f - WindFx.Smooth(outOf));
            if (lean > 0.01f)
            {
                PaintTongues(stage, 3, lean, Mathf.Lerp(1.15f, 1.35f, lean), 0.85f * lean);
            }
            else if (tongues.Count > 0)
            {
                stage.Sprites.ReturnAll(tongues);
            }

            for (int i = 0; i < scraps.Count; i++)
            {
                Scrap s = scraps[i];
                float k = (stage.Now - s.Start) / s.Life;
                if (k <= 0f || k >= 1f)
                {
                    if (s.Outer != null)
                    {
                        stage.Sprites.Return(ref s.Outer);
                        stage.Sprites.Return(ref s.Core);
                    }
                    continue;
                }
                if (s.Outer == null)
                {
                    Sprite shape = s.Tiny ? WindShapes.Shard : WindShapes.Scrap;
                    s.Outer = stage.Sprites.RentGlow(shape, WindOrders.Ember);
                    s.Core = s.Tiny ? null : stage.Sprites.RentGlow(shape, WindOrders.Ember + 1);
                    if (!hissed && !s.Tiny)
                    {
                        hissed = true;
                        stage.Cue(WindCue.EmberHiss);
                    }
                }
                // torn off fast, then carried: most of the speed is at the start
                float travel = 1f - (1f - k) * (1f - k) * (1f - 0.35f * k);
                Vector2 board = Bezier(s.From, s.Control, s.To, travel);
                Vector2 ahead = Bezier(s.From, s.Control, s.To, Mathf.Min(1f, travel + 0.04f));
                Vector2 at = stage.World(board) + stage.Drift(board, s.Seed);
                Vector2 heading = stage.World(ahead) + stage.Drift(ahead, s.Seed) - at;
                float angle = heading.sqrMagnitude > 1e-8f
                    ? Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg + 180f // the bulb leads, the point trails
                    : stage.Angle + 180f;
                float alpha = Mathf.Clamp01(k * 9f) * (s.Fades ? 1f - WindFx.Smooth((k - 0.5f) / 0.5f) : 1f);
                float size = stage.Cell * s.Size * (1f - 0.25f * k);
                Color outer = Color.Lerp(WindFx.Style.EmberOuter, WindFx.Style.EmberTail, s.Tiny ? k * 0.7f : k * 0.35f);
                WindSprites.Place(s.Outer, at, angle, size * (s.Tiny ? 1.6f : 1.5f), size, WindFx.Tint(outer, 0.85f * alpha));
                if (s.Core != null)
                {
                    WindSprites.Place(s.Core, at, angle, size * 0.9f, size * 0.55f,
                        WindFx.Tint(WindFx.Style.EmberCore, 0.95f * alpha));
                }
            }

            for (int i = 0; i < smears.Count; i++)
            {
                Smear m = smears[i];
                Scrap s = m.Follows;
                float k = (stage.Now - s.Start) / s.Life;
                if (k <= 0f || k >= 1f || s.Outer == null)
                {
                    if (m.Body != null)
                    {
                        stage.Sprites.Return(ref m.Body);
                    }
                    continue;
                }
                if (m.Body == null)
                {
                    m.Body = stage.Sprites.RentGlow(WindShapes.Smear, WindOrders.Ember - 1);
                }
                Vector3 p = s.Outer.transform.position;
                float a = s.Outer.transform.eulerAngles.z;
                Vector2 back = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                WindSprites.Place(m.Body, (Vector2)p + back * (stage.Cell * 0.22f), a, stage.Cell * 0.6f,
                    stage.Cell * 0.14f, WindFx.Tint(WindFx.Style.EmberOuter, 0.14f * Bell(k)));
            }
        }

        /// <summary>Flame laid over along the wind: rooted on the cube, never moving it.</summary>
        private void PaintTongues(WindStage stage, int count, float lean, float stretch, float alpha)
        {
            while (tongues.Count < count * 2)
            {
                tongues.Add(stage.Sprites.RentGlow(WindShapes.ScrapRooted, WindOrders.Ember));
            }
            Vector2 centre = stage.World(Cell);
            for (int i = 0; i < count; i++)
            {
                float h = WindFx.Hash(Cell.X * 31 + Cell.Y * 17 + i, 13);
                float flick = 0.86f + 0.14f * Mathf.Sin(stage.Now * (17f + i * 5f) + h * 6.28f);
                // upright at rest, laid along the wind as it leans
                float angle = Mathf.LerpAngle(90f, stage.Angle, Mathf.Clamp01(lean) * 0.92f)
                    + (h - 0.5f) * 16f;
                Vector2 root = centre + stage.Side * ((i - (count - 1) * 0.5f) * stage.Cube * 0.26f)
                    + new Vector2(0f, stage.Cube * 0.08f);
                float length = stage.Cube * 0.5f * stretch * flick;
                WindSprites.Place(tongues[i * 2], root, angle, length, length * 0.5f,
                    WindFx.Tint(WindFx.Style.EmberOuter, alpha));
                WindSprites.Place(tongues[i * 2 + 1], root, angle, length * 0.62f, length * 0.28f,
                    WindFx.Tint(WindFx.Style.EmberCore, alpha));
            }
        }

        public override void Release(WindStage stage)
        {
            stage.Sprites.ReturnAll(tongues);
            stage.Sprites.Return(ref previewEdge);
            stage.Sprites.Return(ref previewEdge2);
            foreach (Scrap s in scraps)
            {
                stage.Sprites.Return(ref s.Outer);
                stage.Sprites.Return(ref s.Core);
            }
            foreach (Smear m in smears)
            {
                stage.Sprites.Return(ref m.Body);
            }
        }

        public override bool TryBeat(WindBeat beat, out float from, out float to)
        {
            switch (beat)
            {
                case WindBeat.FireLean: return Beat(ContactTime, ContactTime + WindFx.Style.FireLean, out from, out to);
                case WindBeat.FireTear: return Beat(firstTear, firstTear + 0.12f, out from, out to);
                case WindBeat.FireTravel:
                    return Beat(firstTear, firstLanding < float.MaxValue ? firstLanding : HeroEnd, out from, out to);
            }
            return base.TryBeat(beat, out from, out to);
        }
    }

    /// <summary>
    /// The blocks the embers set alight. A block may be struck by several scraps from several
    /// fires; it is ONE ignition, started by the first to land, and each strike is its own hot spot.
    /// </summary>
    internal sealed class WindIgnitions
    {
        private sealed class Strike
        {
            public float Time;
            public Vector2 Point;
            public SpriteRenderer Spot;
        }

        private sealed class Burn
        {
            public GridPos Cell;
            public float Start = float.MaxValue;
            public Vector2 Dir;
            public readonly List<Strike> Strikes = new List<Strike>();
            public Sprite WasFace;
            public Color WasTint = Color.white;
            public Sprite FireFace;
            public Color FireTint = Color.white;
            public SpriteRenderer Was;
            public SpriteRenderer Fire;
            public readonly List<SpriteRenderer> Tongues = new List<SpriteRenderer>();
            public bool Sounded;
            public bool Done;
            public bool KeepHeld;
            public float Seed;
        }

        private readonly Dictionary<GridPos, Burn> burns = new Dictionary<GridPos, Burn>();
        private MaterialPropertyBlock block;

        public float Total
        {
            get
            {
                return WindFx.Style.IgniteSpot + WindFx.Style.IgniteSpread + WindFx.Style.IgniteTongues
                    + WindFx.Style.IgniteSettle;
            }
        }

        public float End { get; private set; }

        public void Clear()
        {
            burns.Clear();
            End = 0f;
        }

        /// <summary>The block as it stood, and as Core has made it. Called for every ignition the
        /// report lists, before any strike is registered.</summary>
        public void Register(SpreadIgnition lit, Sprite wasFace, Color wasTint, Sprite fireFace, Color fireTint,
            bool keepHeld)
        {
            burns[lit.Cell] = new Burn
            {
                Cell = lit.Cell,
                WasFace = wasFace,
                WasTint = wasTint,
                FireFace = fireFace,
                FireTint = fireTint,
                KeepHeld = keepHeld,
                Seed = WindFx.Hash(lit.Cell.X * 17 + lit.Cell.Y, 19)
            };
        }

        /// <summary>A scrap will land here then. The earliest starts the burn.</summary>
        public void Hit(GridPos cell, float time, Vector2 boardPoint, Vector2 approach)
        {
            Burn b;
            if (!burns.TryGetValue(cell, out b))
            {
                return;
            }
            b.Strikes.Add(new Strike { Time = time, Point = boardPoint });
            if (time < b.Start)
            {
                b.Start = time;
                b.Dir = approach.sqrMagnitude > 1e-6f ? approach : Vector2.right;
            }
            End = Mathf.Max(End, b.Start + Total);
        }

        /// <summary>When the burn on this cell is over (0 when nothing lit it).</summary>
        public float EndOf(GridPos cell)
        {
            Burn b;
            return burns.TryGetValue(cell, out b) && b.Start < float.MaxValue ? b.Start + Total : 0f;
        }

        public bool TryBeat(WindBeat beat, out float from, out float to)
        {
            from = 0f;
            to = 0f;
            float first = float.MaxValue;
            foreach (Burn b in burns.Values)
            {
                first = Mathf.Min(first, b.Start);
            }
            if (first == float.MaxValue)
            {
                return false;
            }
            switch (beat)
            {
                case WindBeat.FireHotSpot:
                    from = first;
                    to = first + WindFx.Style.IgniteSpot;
                    return true;
                case WindBeat.FireIgnite:
                    from = first + WindFx.Style.IgniteSpot;
                    to = first + WindFx.Style.IgniteSpot + WindFx.Style.IgniteSpread + WindFx.Style.IgniteTongues;
                    return true;
                case WindBeat.FireConversion:
                    from = first;
                    to = first + Total;
                    return true;
            }
            return false;
        }

        public void Tick(WindStage stage)
        {
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            Material bloom = FireSpreadView.BloomMaterial();
            foreach (Burn b in burns.Values)
            {
                if (b.Done || b.Start == float.MaxValue)
                {
                    continue;
                }
                Vector2 at = stage.World(b.Cell);
                // The block stands as it was from the first frame: its cell is held back, and it
                // has to be there, whole and its own colour, until a scrap reaches it.
                if (b.Was == null)
                {
                    stage.Hold(b.Cell);
                    b.Was = stage.Sprites.Rent(b.WasFace, WindOrders.IgniteWas,
                        bloom != null ? bloom : ViewUtil.TileMaterial(b.WasFace));
                    b.Fire = stage.Sprites.Rent(b.FireFace, WindOrders.IgniteFire,
                        bloom != null ? bloom : ViewUtil.TileMaterial(b.FireFace));
                }
                WindSprites.Place(b.Was, at, 0f, stage.Cube, stage.Cube, b.WasTint);
                WindSprites.Place(b.Fire, at, 0f, stage.Cube, stage.Cube, b.FireTint);

                float t = stage.Now - b.Start;
                float spot = WindFx.Style.IgniteSpot;
                float spread = WindFx.Style.IgniteSpread;
                float tongueTime = WindFx.Style.IgniteTongues;
                // A: the hot spots, one per strike, where each scrap landed
                foreach (Strike s in b.Strikes)
                {
                    float k = (stage.Now - s.Time) / (spot + spread);
                    if (k <= 0f || k >= 1f)
                    {
                        if (s.Spot != null)
                        {
                            stage.Sprites.Return(ref s.Spot);
                        }
                        continue;
                    }
                    if (s.Spot == null)
                    {
                        s.Spot = stage.Sprites.RentGlow(WindShapes.Dot, WindOrders.Ember - 2);
                    }
                    // clamped inside the cube: heat on the block, never a glow on its neighbours
                    Vector2 p = stage.World(s.Point);
                    float reach = stage.Cube * 0.5f;
                    p = at + Vector2.ClampMagnitude(p - at, reach * 0.62f);
                    float size = stage.Cube * Mathf.Lerp(0.2f, 0.52f, WindFx.Smooth(k));
                    WindSprites.Place(s.Spot, p, 0f, size, size,
                        WindFx.Tint(Color.Lerp(WindFx.Style.EmberCore, WindFx.Style.EmberOuter, k),
                            0.9f * (1f - k * k)));
                }
                if (t < 0f)
                {
                    PaintFaces(b, -FireSpreadView.Style.Band, 0f, bloom);
                    continue;
                }
                if (!b.Sounded)
                {
                    b.Sounded = true;
                    stage.Cue(WindCue.Ignite);
                    stage.Tap(WindHaptic.Ignition);
                }
                // B: the heat crosses the face from the side it was struck on
                float front = Mathf.Lerp(-FireSpreadView.Style.Band, 1f + FireSpreadView.Style.Band,
                    WindFx.Smooth((t - spot * 0.6f) / (spread + tongueTime)));
                float settle = Mathf.Clamp01((t - spot - spread - tongueTime) / WindFx.Style.IgniteSettle);
                PaintFaces(b, front, FireSpreadView.Style.SettleHeat * (1f - settle), bloom);

                // C/D: the flame catches - tongues rise out of the surface, flare, and settle
                float rise = Mathf.Clamp01((t - spot - spread * 0.5f) / tongueTime);
                if (rise > 0f && settle < 1f)
                {
                    int count = WindFx.Level == WindFx.Quality.Low ? 2 : 3;
                    while (b.Tongues.Count < count * 2)
                    {
                        b.Tongues.Add(stage.Sprites.RentGlow(WindShapes.ScrapRooted, WindOrders.Ember));
                    }
                    // the flare: a fifth taller than it will stand, on the frame it turns to fire
                    float flare = 1f + 0.2f * Mathf.Sin(
                        Mathf.Clamp01((t - spot - spread) / (tongueTime + WindFx.Style.IgniteSettle)) * Mathf.PI);
                    for (int i = 0; i < count; i++)
                    {
                        float h = WindFx.Hash(b.Cell.X * 13 + b.Cell.Y * 7 + i, 23);
                        float flick = 0.85f + 0.15f * Mathf.Sin(stage.Now * (19f + i * 4f) + h * 6.28f);
                        float angle = Mathf.LerpAngle(90f, stage.Angle, 0.35f) + (h - 0.5f) * 24f;
                        Vector2 root = at + new Vector2((i - (count - 1) * 0.5f) * stage.Cube * 0.27f,
                            stage.Cube * (0.02f + 0.12f * h));
                        float length = stage.Cube * 0.46f * WindFx.Smooth(rise) * flare * flick;
                        float alpha = 0.9f * WindFx.Smooth(rise) * (1f - settle);
                        WindSprites.Place(b.Tongues[i * 2], root, angle, length, length * 0.5f,
                            WindFx.Tint(WindFx.Style.EmberOuter, alpha));
                        WindSprites.Place(b.Tongues[i * 2 + 1], root, angle, length * 0.6f, length * 0.27f,
                            WindFx.Tint(WindFx.Style.EmberCore, alpha));
                    }
                }
                if (t >= Total)
                {
                    b.Done = true;
                    ReleaseBurn(stage, b);
                    if (!b.KeepHeld)
                    {
                        stage.Unhold(b.Cell);
                    }
                }
            }
        }

        private void PaintFaces(Burn b, float front, float heat, Material bloom)
        {
            if (bloom == null)
            {
                // No shader: the old face simply gives way to the fire one.
                float a = Mathf.Clamp01(front);
                b.Was.color = new Color(b.WasTint.r, b.WasTint.g, b.WasTint.b, b.WasTint.a * (1f - a));
                b.Fire.color = new Color(b.FireTint.r, b.FireTint.g, b.FireTint.b, b.FireTint.a * a);
                return;
            }
            Face(b.Was, b, front, true, 0f);
            Face(b.Fire, b, front, false, heat);
        }

        private void Face(SpriteRenderer r, Burn b, float front, bool was, float heat)
        {
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

        private static void ReleaseBurn(WindStage stage, Burn b)
        {
            stage.Sprites.Return(ref b.Was);
            stage.Sprites.Return(ref b.Fire);
            stage.Sprites.ReturnAll(b.Tongues);
            foreach (Strike s in b.Strikes)
            {
                stage.Sprites.Return(ref s.Spot);
            }
        }

        public void Release(WindStage stage)
        {
            foreach (Burn b in burns.Values)
            {
                ReleaseBurn(stage, b);
            }
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

    // ================================================================== WATER

    /// <summary>
    /// One cube of water on the move: pressed by the front, lifted out of its cell as a proxy,
    /// carried along the whole route Core reported in ONE motion, and set down. Also plays the
    /// water that only FELL (it lost what it stood on), and the water the wind pressed but could
    /// not move - the same body with a shorter story.
    /// </summary>
    internal sealed class WindWaterReaction : WindReaction
    {
        private readonly List<Vector2> route = new List<Vector2>();
        private readonly List<float> marks = new List<float>(); // distance at each route point
        private float pushLength;
        private float fallLength;
        private readonly Sprite face;
        private readonly Color tint;
        private readonly GridPos rest;
        private readonly bool pressedOnly;
        private readonly bool windBorne;

        private float detach;
        private float glideEnd;
        private float arrive;

        private SpriteRenderer body;
        private SpriteRenderer smear;
        private readonly List<SpriteRenderer> tail = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> splash = new List<SpriteRenderer>();
        private MaterialPropertyBlock block;
        private Vector2 lastHeading;

        /// <summary>The aim's water: its surface is drawn out the way the wind will blow.</summary>
        public WindWaterReaction(GridPos cell, WindReactionKind reaction)
        {
            Cell = cell;
            Reaction = reaction;
        }

        /// <summary>Pushed water. <paramref name="look"/> is its face before the rules ran.</summary>
        public WindWaterReaction(WindStage stage, WindPush push, Sprite look, Color colour)
        {
            Cell = push.From;
            Reaction = WindReactionKind.PhysicalMove;
            face = look;
            tint = colour;
            rest = push.Rest;
            windBorne = true;
            route.Add(new Vector2(push.From.X, push.From.Y));
            foreach (GridPos p in push.Path)
            {
                route.Add(new Vector2(p.X, p.Y));
            }
            pushLength = Measure(0);
            int turn = route.Count - 1;
            foreach (GridPos p in push.Fall)
            {
                route.Add(new Vector2(p.X, p.Y));
            }
            fallLength = Measure(turn);
            ContactTime = stage.Clock.ReachTime(stage.Along(push.From));
            Schedule(stage);
        }

        /// <summary>Water that only fell, from <paramref name="start"/> seconds in.</summary>
        public WindWaterReaction(WindStage stage, List<GridPos> fall, Sprite look, Color colour, float start)
        {
            Cell = fall[0];
            Reaction = WindReactionKind.PhysicalMove;
            face = look;
            tint = colour;
            rest = fall[fall.Count - 1];
            foreach (GridPos p in fall)
            {
                route.Add(new Vector2(p.X, p.Y));
            }
            pushLength = 0f;
            fallLength = Measure(0);
            ContactTime = start;
            Schedule(stage);
        }

        /// <summary>Water the wind pressed and could not move.</summary>
        public WindWaterReaction(WindStage stage, GridPos cell, Sprite look, Color colour)
        {
            Cell = cell;
            Reaction = WindReactionKind.LeanOnly;
            face = look;
            tint = colour;
            rest = cell;
            pressedOnly = true;
            windBorne = true;
            route.Add(new Vector2(cell.X, cell.Y));
            marks.Add(0f);
            ContactTime = stage.Clock.ReachTime(stage.Along(cell));
            detach = ContactTime + WindFx.Style.WaterPress;
            glideEnd = detach;
            arrive = detach;
            HeroEnd = arrive + WindFx.Style.WaterArrive;
            End = HeroEnd;
        }

        private float Measure(int from)
        {
            float total = 0f;
            for (int i = marks.Count; i < route.Count; i++)
            {
                float step = i == 0 ? 0f : (route[i] - route[i - 1]).magnitude;
                float before = i == 0 ? 0f : marks[i - 1];
                marks.Add(before + step);
            }
            if (route.Count > 0)
            {
                total = marks[route.Count - 1] - marks[Mathf.Clamp(from, 0, route.Count - 1)];
            }
            return total;
        }

        private void Schedule(WindStage stage)
        {
            detach = ContactTime + (windBorne ? WindFx.Style.WaterPress : 0.03f);
            // a little behind the front, and never left crossing the board after the storm has gone
            float glide = pushLength > 0.001f
                ? Mathf.Clamp(pushLength / (stage.Clock.Speed * WindFx.Style.WaterGlideShare), 0.12f,
                    WindFx.Style.WaterGlideMax)
                : 0f;
            glideEnd = detach + glide;
            float fall = fallLength > 0.001f ? 0.06f + Mathf.Sqrt(fallLength) * 0.07f : 0f;
            arrive = glideEnd + fall;
            SendTime = detach;
            ArriveTime = arrive;
            HeroEnd = arrive + WindFx.Style.WaterArrive;
            End = HeroEnd + 0.12f;
            stage.Hold(rest);
        }

        public override void OnWindPreview(WindStage stage)
        {
            float strength = Reaction == WindReactionKind.PhysicalMove ? 1f : 0.5f;
            PaintPreviewEdge(stage, strength);
            // the surface highlight, drawn out along the wind
            if (smear == null)
            {
                smear = stage.Sprites.RentGlow(WindShapes.Smear, WindOrders.Bystander + 1);
            }
            float breathe = 0.5f + 0.5f * Mathf.Sin(stage.Now * 3.1f + Cell.X + Cell.Y * 0.6f);
            Vector2 at = stage.World(Cell) + stage.Dir * (stage.Cube * (0.06f + 0.08f * breathe))
                + new Vector2(0f, stage.Cube * 0.16f);
            WindSprites.Place(smear, at, stage.Angle, stage.Cube * (0.5f + 0.24f * breathe), stage.Cube * 0.13f,
                WindFx.Tint(WindFx.Style.Aqua, 0.2f * strength));
        }

        public override void OnWindContact(WindStage stage)
        {
            if (windBorne)
            {
                stage.Cue(WindCue.WaterContact);
            }
        }

        public override void CreateWindTransportVisual(WindStage stage)
        {
            if (windBorne && !pressedOnly)
            {
                stage.Cue(WindCue.WaterHiss);
            }
        }

        public override void OnWindArrival(WindStage stage)
        {
            if (pressedOnly)
            {
                return;
            }
            stage.Cue(WindCue.WaterArrive);
            stage.Tap(WindHaptic.WaterArrival);
            int drops = WindFx.Level == WindFx.Quality.High ? 4 : WindFx.Level == WindFx.Quality.Medium ? 3 : 2;
            for (int i = 0; i < drops; i++)
            {
                splash.Add(stage.Sprites.RentGlow(WindShapes.Droplet, WindOrders.Water + 1));
            }
        }

        /// <summary>Where the cube is, in board units, this far along its route.</summary>
        private Vector2 PointAt(float distance, out Vector2 heading)
        {
            int last = route.Count - 1;
            if (last <= 0)
            {
                heading = Vector2.zero;
                return route[0];
            }
            distance = Mathf.Clamp(distance, 0f, marks[last]);
            int i = 1;
            while (i < last && marks[i] < distance)
            {
                i++;
            }
            float span = Mathf.Max(0.0001f, marks[i] - marks[i - 1]);
            float k = (distance - marks[i - 1]) / span;
            heading = (route[i] - route[i - 1]).normalized;
            return Vector2.Lerp(route[i - 1], route[i], k);
        }

        protected override void Paint(WindStage stage)
        {
            if (face == null)
            {
                return;
            }
            if (body == null)
            {
                Material carry = WindFx.Carry;
                body = stage.Sprites.Rent(face, WindOrders.Water, carry != null ? carry : ViewUtil.TileMaterial(face));
                block = new MaterialPropertyBlock();
                if (pressedOnly)
                {
                    stage.Hold(Cell);
                }
            }
            float now = stage.Now;
            Vector2 heading;
            float along = 1f;
            float across = 1f;
            float taper = 0f;
            float sheen = 0f;
            Vector2 board;
            float speed = 0f; // 0..1, how much of the wind is in it

            if (now < detach)
            {
                // PRESSURE: still in its cell, leaning into the wind
                float k = windBorne ? WindFx.Smooth(Span(now, ContactTime, WindFx.Style.WaterPress)) : 0f;
                board = route[0];
                heading = new Vector2(stage.Gust.DirX, stage.Gust.DirY);
                along = 1f + 0.12f * k;
                across = 1f - 0.07f * k;
                taper = 0.12f * k;
                sheen = 0.7f * k;
            }
            else if (now < glideEnd)
            {
                // THE DRAG: one low glide along the wind, quick to start and never braking -
                // it is the obstacle that stops it
                float k = Span(now, detach, glideEnd - detach);
                float eased = 0.3f * k * k + 0.7f * k;
                board = PointAt(pushLength * eased, out heading);
                speed = 0.55f + 0.45f * Mathf.Sin(k * Mathf.PI * 0.5f);
                along = 1.12f + 0.1f * speed;
                across = 0.93f - 0.07f * speed;
                taper = 0.1f + 0.06f * speed;
                sheen = 0.8f;
            }
            else if (now < arrive)
            {
                // THE FALL: the arena's own gravity, quickening
                float k = Span(now, glideEnd, arrive - glideEnd);
                board = PointAt(pushLength + fallLength * k * k, out heading);
                speed = 0.3f + 0.5f * k;
                along = 1.04f + 0.12f * k;
                across = 0.97f - 0.06f * k;
                taper = 0.06f * k;
                sheen = 0.35f;
            }
            else
            {
                // ARRIVAL: the stretched body is driven short, and comes back to rest
                float k = Span(now, arrive, WindFx.Style.WaterArrive);
                board = PointAt(marks[route.Count - 1], out heading);
                if (pressedOnly)
                {
                    heading = new Vector2(stage.Gust.DirX, stage.Gust.DirY);
                    along = 1f + 0.12f * (1f - WindFx.Smooth(k));
                    across = 1f - 0.07f * (1f - WindFx.Smooth(k));
                    taper = 0.12f * (1f - WindFx.Smooth(k));
                    sheen = 0.7f * (1f - k);
                }
                else
                {
                    // 1.15 -> 0.90 -> 1.00 along the way it was travelling
                    along = k < 0.4f ? Mathf.Lerp(1.15f, 0.9f, WindFx.Smooth(k / 0.4f))
                        : Mathf.Lerp(0.9f, 1f, WindFx.Smooth((k - 0.4f) / 0.6f));
                    across = 1f + (1f - along) * 0.7f;
                    sheen = 0.25f * (1f - k);
                }
            }
            if (heading.sqrMagnitude < 1e-6f)
            {
                heading = lastHeading.sqrMagnitude > 1e-6f ? lastHeading : new Vector2(stage.Gust.DirX, stage.Gust.DirY);
            }
            lastHeading = heading;
            Vector2 at = stage.World(board);
            if (speed > 0f && windBorne && now < glideEnd)
            {
                at += stage.Drift(board, Cell.X * 31 + Cell.Y) * 0.5f;
            }
            // the heading on screen (the board's own transform may squeeze or turn it)
            Vector2 ahead = stage.World(board + heading * 0.5f) - stage.World(board);
            Vector2 dir = ahead.sqrMagnitude > 1e-8f ? ahead.normalized : stage.Dir;

            WindSprites.Place(body, at, 0f, stage.Cube, stage.Cube, tint);
            if (WindFx.Carry != null)
            {
                block.Clear();
                block.SetVector(CentreId, new Vector4(at.x, at.y, 0f, 0f));
                block.SetVector(DirId, new Vector4(dir.x, dir.y, 0f, 0f));
                block.SetFloat(HalfId, stage.Cube * 0.5f);
                block.SetFloat(AlongId, along);
                block.SetFloat(AcrossId, across);
                block.SetFloat(TaperId, taper);
                block.SetFloat(SheenId, sheen);
                block.SetColor(SheenColorId, WindFx.Style.Aqua);
                body.SetPropertyBlock(block);
            }

            PaintTrail(stage, at, dir, speed, now < arrive && now >= detach && !pressedOnly);
            PaintSplash(stage, at, dir, now);
        }

        private void PaintTrail(WindStage stage, Vector2 at, Vector2 dir, float speed, bool moving)
        {
            if (!moving || !WindFx.Trails)
            {
                stage.Sprites.ReturnAll(tail);
                stage.Sprites.Return(ref smear);
                return;
            }
            int count = WindFx.Level == WindFx.Quality.High ? 3 : 2;
            while (tail.Count < count)
            {
                tail.Add(stage.Sprites.RentGlow(WindShapes.Droplet, WindOrders.WaterTrail));
            }
            if (smear == null)
            {
                smear = stage.Sprites.RentGlow(WindShapes.Smear, WindOrders.WaterTrail);
            }
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 side = new Vector2(-dir.y, dir.x);
            // a thin wet smear behind it, a third of a cell long
            WindSprites.Place(smear, at - dir * (stage.Cell * 0.42f), angle, stage.Cell * (0.25f + 0.2f * speed),
                stage.Cube * 0.42f, WindFx.Tint(WindFx.Style.Aqua, 0.09f * speed));
            for (int i = 0; i < tail.Count; i++)
            {
                float h = WindFx.Hash(Cell.X * 7 + Cell.Y * 13 + i, 29);
                float lag = 0.5f + i * 0.22f + 0.06f * Mathf.Sin(stage.Now * 21f + i * 2.1f);
                Vector2 p = at - dir * (stage.Cell * lag) + side * (stage.Cell * (h - 0.5f) * 0.34f);
                float size = stage.Cell * (0.2f - i * 0.04f);
                WindSprites.Place(tail[i], p, angle, size * 1.5f, size,
                    WindFx.Tint(WindFx.Style.Aqua, (0.7f - i * 0.15f) * speed));
            }
        }

        private void PaintSplash(WindStage stage, Vector2 at, Vector2 dir, float now)
        {
            if (splash.Count == 0)
            {
                return;
            }
            float k = Span(now, arrive, 0.2f);
            if (k >= 1f)
            {
                stage.Sprites.ReturnAll(splash);
                return;
            }
            Vector2 side = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < splash.Count; i++)
            {
                float h = WindFx.Hash(Cell.X * 19 + Cell.Y * 5 + i, 31);
                float spread = (i - (splash.Count - 1) * 0.5f) / Mathf.Max(1f, splash.Count - 1f);
                // thrown ASIDE and a little back off the edge it landed on - what it struck is in
                // front of it, so nothing goes forward - on a small arc, local to the cell
                Vector2 v = side * (spread * 0.7f + (h - 0.5f) * 0.2f) - dir * (0.16f + 0.14f * h);
                Vector2 p = at + dir * (stage.Cube * 0.36f) + v * (stage.Cell * k)
                    + dir * (stage.Cell * 0.14f * k * k);
                float size = stage.Cell * (0.1f + 0.04f * h) * (1f - 0.4f * k);
                WindSprites.Place(splash[i], p, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg, size * 1.4f, size,
                    WindFx.Tint(WindFx.Style.Aqua, 0.75f * (1f - k)));
            }
        }

        public override void OnWindFinished(WindStage stage)
        {
            Release(stage);
            stage.Unhold(rest);
            if (pressedOnly)
            {
                stage.Unhold(Cell);
            }
        }

        public override void Release(WindStage stage)
        {
            stage.Sprites.Return(ref body);
            stage.Sprites.Return(ref smear);
            stage.Sprites.Return(ref previewEdge);
            stage.Sprites.Return(ref previewEdge2);
            stage.Sprites.ReturnAll(tail);
            stage.Sprites.ReturnAll(splash);
        }

        /// <summary>When the last of the gust's water comes to rest (for the fires it puts out).</summary>
        public float Arrival
        {
            get { return arrive; }
        }

        public override bool TryBeat(WindBeat beat, out float from, out float to)
        {
            switch (beat)
            {
                case WindBeat.WaterDeform: return Beat(ContactTime, detach, out from, out to);
                case WindBeat.WaterDetach: return Beat(detach, detach + 0.07f, out from, out to);
                case WindBeat.WaterDrag: return Beat(detach, arrive, out from, out to);
                case WindBeat.WaterArrival: return Beat(arrive, arrive + WindFx.Style.WaterArrive, out from, out to);
            }
            return base.TryBeat(beat, out from, out to);
        }

        private static readonly int CentreId = Shader.PropertyToID("_Centre");
        private static readonly int DirId = Shader.PropertyToID("_Dir");
        private static readonly int HalfId = Shader.PropertyToID("_HalfSize");
        private static readonly int AlongId = Shader.PropertyToID("_Along");
        private static readonly int AcrossId = Shader.PropertyToID("_Across");
        private static readonly int TaperId = Shader.PropertyToID("_Taper");
        private static readonly int SheenId = Shader.PropertyToID("_Sheen");
        private static readonly int SheenColorId = Shader.PropertyToID("_SheenColor");
    }

    // ================================================================== INFECTION

    /// <summary>One infection the wind carries: the source squeezed, a piece torn off, its ride,
    /// the block it stains, and the new core.</summary>
    internal sealed class WindInfectionReaction : WindReaction
    {
        private readonly GridPos to;
        private readonly int seed;
        private Vector2 from;
        private Vector2 control;
        private Vector2 target;
        private Vector2 contact;
        private float tear;
        private float land;
        private float seedTime;

        private SpriteRenderer essence;
        private SpriteRenderer halo;
        private SpriteRenderer impact;
        private bool grown;
        private readonly List<SpriteRenderer> spores = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> branches = new List<SpriteRenderer>();

        /// <summary>The aim's infection: its spores are drawn a few pixels downwind.</summary>
        public WindInfectionReaction(GridPos cell)
        {
            Cell = cell;
            Reaction = WindReactionKind.DuplicateSpread;
        }

        public WindInfectionReaction(WindStage stage, WindCarry carry)
        {
            Cell = carry.From;
            to = carry.To;
            seed = carry.Seed;
            Reaction = WindReactionKind.DuplicateSpread;
            from = new Vector2(carry.From.X, carry.From.Y);
            target = new Vector2(carry.To.X, carry.To.Y);
            Vector2 run = target - from;
            float dist = run.magnitude;
            Vector2 side = new Vector2(-run.y, run.x).normalized;
            float bow = (WindFx.Hash(seed, 3) > 0.5f ? 1f : -1f) * Mathf.Min(0.6f, 0.18f + 0.08f * dist);
            control = (from + target) * 0.5f + side * bow;
            // it strikes the face it came toward, not the middle
            contact = target - (target - control).normalized * 0.34f;
            ContactTime = stage.Clock.ReachTime(stage.Along(carry.From));
            tear = ContactTime + WindFx.Style.SporePress;
            land = tear + Mathf.Clamp(WindFx.Style.SporeFlightMin + 0.035f * dist,
                WindFx.Style.SporeFlightMin, WindFx.Style.SporeFlightMax);
            seedTime = land + WindFx.Style.SporeBranch + WindFx.Style.SporePull * 0.5f;
            SendTime = tear;
            ArriveTime = land;
            HeroEnd = seedTime + WindFx.Style.SporeSeed;
            End = HeroEnd + 0.05f;
            // The new core is already in the rules; it may not be seen before its piece arrives.
            stage.View.HoldInfectionBirth(new[] { to }, seedTime, true);
        }

        public override void OnWindPreview(WindStage stage)
        {
            PaintPreviewEdge(stage, 1f);
            while (spores.Count < 3)
            {
                spores.Add(stage.Sprites.RentGlow(WindShapes.Dot, WindOrders.Spore));
            }
            Vector2 centre = stage.World(Cell);
            for (int i = 0; i < spores.Count; i++)
            {
                // drawn 2-4 px the way the wind will go, and let back
                float k = Mathf.Repeat(stage.Now * 0.9f + i * 0.37f, 1f);
                float pull = stage.Pixel * Mathf.Lerp(2f, 4.5f, WindFx.Smooth(Bell(k)));
                Vector2 rest = stage.Side * ((i - 1) * stage.Pixel * 3.5f);
                float size = stage.Pixel * (2.2f + i * 0.5f);
                WindSprites.Place(spores[i], centre + rest + stage.Dir * pull, 0f, size, size,
                    WindFx.Tint(WindFx.Style.SporeLime, 0.75f * Bell(k)));
            }
        }

        public override void OnWindContact(WindStage stage)
        {
            // nothing is heard yet: the core is only being pressed
        }

        public override void CreateWindTransportVisual(WindStage stage)
        {
            stage.Cue(WindCue.SporeTear);
            stage.Cue(WindCue.SporeWhisper);
            essence = stage.Sprites.Rent(WindShapes.Essence, WindOrders.Spore + 1, null);
            halo = stage.Sprites.RentGlow(WindShapes.Dot, WindOrders.Spore);
            int count = WindFx.Level == WindFx.Quality.High ? 9 : WindFx.Level == WindFx.Quality.Medium ? 6 : 3;
            for (int i = 0; i < count; i++)
            {
                spores.Add(stage.Sprites.RentGlow(WindShapes.Dot, WindOrders.Spore));
            }
        }

        public override void OnWindArrival(WindStage stage)
        {
            stage.Cue(WindCue.SporeImpact);
            impact = stage.Sprites.RentGlow(WindShapes.Dot, WindOrders.Spore + 1);
            int count = WindFx.Level == WindFx.Quality.Low ? 2 : 4;
            for (int i = 0; i < count; i++)
            {
                branches.Add(stage.Sprites.RentGlow(WindShapes.Branch, WindOrders.Spore));
            }
        }

        protected override void Paint(WindStage stage)
        {
            float now = stage.Now;
            // THE SOURCE: squeezed on its windward side and drawn out to leeward while it is
            // pressed; shrunk and dimmed where the piece left it, then whole again.
            float press = WindFx.Smooth(Span(now, ContactTime, WindFx.Style.SporePress))
                * (1f - WindFx.Smooth(Span(now, tear, 0.1f)));
            float deplete = now >= tear ? 1f - WindFx.Smooth(Span(now, tear + 0.06f, 0.3f)) : 0f;
            Vector2 local = new Vector2(stage.Gust.DirX, stage.Gust.DirY);
            stage.View.SetInfectionWind(Cell, local, press, deplete);

            if (essence != null)
            {
                float k = Span(now, tear, land - tear);
                if (k >= 1f)
                {
                    stage.Sprites.Return(ref essence);
                    stage.Sprites.Return(ref halo);
                }
                else
                {
                    Vector2 board = Bezier(from, control, contact, WindFx.Smooth(k) * 0.35f + k * 0.65f);
                    Vector2 at = stage.World(board) + stage.Drift(board, seed);
                    float size = stage.Cell * 0.18f * (0.85f + 0.15f * Mathf.Sin(now * 23f));
                    // its own faint light: a dark-green piece on a dark board is not there at all
                    WindSprites.Place(halo, at, 0f, size * 2.4f, size * 2.4f,
                        WindFx.Tint(WindFx.Style.SporeLime, 0.3f * Mathf.Clamp01(k * 10f)));
                    // it turns as it rides - an organism tumbling, not a projectile
                    WindSprites.Place(essence, at, now * 220f + seed % 360, size, size * 0.9f,
                        new Color(1f, 1f, 1f, Mathf.Clamp01(k * 10f)));
                    // the stream behind it
                    for (int i = 0; i < spores.Count; i++)
                    {
                        float h = WindFx.Hash(seed + i * 7, 37);
                        float kk = Mathf.Clamp01(k - (0.05f + i * 0.035f));
                        Vector2 b = Bezier(from, control, contact, kk);
                        Vector2 p = stage.World(b) + stage.Drift(b, seed + i * 3)
                            + stage.Side * ((h - 0.5f) * stage.Cell * 0.16f);
                        float s = stage.Pixel * Mathf.Lerp(2f, 3.6f, h);
                        WindSprites.Place(spores[i], p, 0f, s, s,
                            WindFx.Tint(WindFx.Style.SporeLime, kk <= 0f ? 0f : 0.8f * (1f - 0.5f * k)));
                    }
                }
            }
            else if (spores.Count > 0 && now >= land)
            {
                stage.Sprites.ReturnAll(spores);
            }

            if (!grown && now >= seedTime)
            {
                // the new core is let go here (HoldInfectionBirth) and grows where the stain gathered
                grown = true;
                stage.Cue(WindCue.SporeGrow);
                stage.Tap(WindHaptic.Seed);
            }
            if (now >= land)
            {
                Vector2 at = stage.World(contact);
                Vector2 centre = stage.World(target);
                // IMPACT: a small wet compression where it struck
                float hit = Span(now, land, 0.08f);
                if (impact != null)
                {
                    if (hit >= 1f)
                    {
                        stage.Sprites.Return(ref impact);
                    }
                    else
                    {
                        float size = stage.Cell * Mathf.Lerp(0.16f, 0.07f, hit);
                        WindSprites.Place(impact, at, 0f, size * 1.25f, size * 0.8f,
                            WindFx.Tint(WindFx.Style.SporeLime, 0.9f * (1f - hit)));
                    }
                }
                // THE STAIN: short branches out of the contact point, then drawn to the middle
                float grow = WindFx.Smooth(Span(now, land, WindFx.Style.SporeBranch));
                float pull = WindFx.Smooth(Span(now, land + WindFx.Style.SporeBranch, WindFx.Style.SporePull));
                Vector2 toward = (centre - at);
                float baseAngle = Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg;
                Vector2 root = Vector2.Lerp(at, centre, pull);
                for (int i = 0; i < branches.Count; i++)
                {
                    float spread = branches.Count == 1 ? 0f : (i / (float)(branches.Count - 1) - 0.5f) * 110f;
                    float h = WindFx.Hash(seed + i * 11, 41);
                    float length = stage.Cell * (0.32f + 0.12f * h) * grow * (1f - pull);
                    WindSprites.Place(branches[i], root, baseAngle + spread, length, stage.Cell * 0.09f,
                        WindFx.Tint(WindFx.Style.SporeLime, 0.8f * grow * (1f - pull * pull)));
                }
                if (pull >= 1f && branches.Count > 0)
                {
                    stage.Sprites.ReturnAll(branches);
                }
            }
        }

        public override void OnWindFinished(WindStage stage)
        {
            Release(stage);
            stage.View.SetInfectionWind(Cell, Vector2.zero, 0f, 0f);
        }

        public override void Release(WindStage stage)
        {
            stage.Sprites.Return(ref essence);
            stage.Sprites.Return(ref halo);
            stage.Sprites.Return(ref impact);
            stage.Sprites.Return(ref previewEdge);
            stage.Sprites.Return(ref previewEdge2);
            stage.Sprites.ReturnAll(spores);
            stage.Sprites.ReturnAll(branches);
            if (stage.View != null)
            {
                stage.View.SetInfectionWind(Cell, Vector2.zero, 0f, 0f);
            }
        }

        public override bool TryBeat(WindBeat beat, out float from1, out float to1)
        {
            switch (beat)
            {
                case WindBeat.SporeContact: return Beat(ContactTime, tear, out from1, out to1);
                case WindBeat.SporeTear: return Beat(tear, tear + 0.1f, out from1, out to1);
                case WindBeat.SporeTravel: return Beat(tear, land, out from1, out to1);
                case WindBeat.SporeContaminate:
                    return Beat(land, land + WindFx.Style.SporeBranch + WindFx.Style.SporePull, out from1, out to1);
                case WindBeat.SporeSeed: return Beat(seedTime, seedTime + WindFx.Style.SporeSeed, out from1, out to1);
                case WindBeat.SporeSource: return Beat(tear, tear + 0.36f, out from1, out to1);
            }
            return base.TryBeat(beat, out from1, out to1);
        }
    }

    // ================================================================== BYSTANDERS

    /// <summary>
    /// A cube the wind only passed over. It catches the front on its windward edge and loses a
    /// little light to leeward for a tenth of a second, a pixel of give - and it does NOT move:
    /// nothing in the rules moved it, and a block that looks carried and is not is the worst thing
    /// this power could say.
    /// </summary>
    internal sealed class WindBystanderReaction : WindReaction
    {
        private SpriteRenderer lit;
        private SpriteRenderer lit2;
        private SpriteRenderer shade;
        private SpriteRenderer shade2;

        public WindBystanderReaction(WindStage stage, GridPos cell)
        {
            Cell = cell;
            Reaction = WindReactionKind.None;
            ContactTime = stage.Clock.ReachTime(stage.Along(cell));
            HeroEnd = ContactTime + 0.11f;
            End = HeroEnd;
        }

        protected override void Paint(WindStage stage)
        {
            float k = Span(stage.Now, ContactTime, 0.11f);
            if (k <= 0f)
            {
                return;
            }
            if (lit == null)
            {
                lit = stage.Sprites.RentGlow(WindShapes.Edge, WindOrders.Bystander);
                lit2 = stage.Sprites.RentGlow(WindShapes.Edge, WindOrders.Bystander);
                shade = stage.Sprites.Rent(WindShapes.Edge, WindOrders.Bystander, null);
                shade2 = stage.Sprites.Rent(WindShapes.Edge, WindOrders.Bystander, null);
            }
            float bell = Bell(k);
            Vector2 at = stage.World(Cell) + stage.Dir * (stage.Pixel * bell);
            // light on the side the wind comes from, shade on the side it leaves by
            PlaceEdges(lit, lit2, at, -stage.Dir, stage.Cube, WindFx.Tint(WindFx.Style.Air, 0.2f * bell));
            PlaceEdges(shade, shade2, at, stage.Dir, stage.Cube, new Color(0.02f, 0.05f, 0.08f, 0.22f * bell));
        }

        public override void Release(WindStage stage)
        {
            stage.Sprites.Return(ref lit);
            stage.Sprites.Return(ref lit2);
            stage.Sprites.Return(ref shade);
            stage.Sprites.Return(ref shade2);
        }
    }
}
