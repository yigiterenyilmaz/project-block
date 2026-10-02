// PURPOSE: "Rüzgar" when it BLOWS - the directed storm, one use of the power, played once.
//
// ONE PHYSICAL EVENT, ONE CLOCK (WindFx.Clock):
//   PRESSURE LOCK  ~0.13 s. The aim's lane does not vanish: it draws IN, its flow slows, the
//                  origin brightens. "The air is being pressed."
//   RELEASE        the origin lets go - a small compression and a directional burst of air, not
//                  an explosion - and a FRONT leaves it: a bowed pressure wall as wide as the lane.
//   THE CROSSING   the front runs origin to endpoint in 0.34-0.85 s by length. Behind it the lane
//                  FILLS: the pressure body, long broken flow ribbons racing down it, a few motes,
//                  and the board seen through moving air (the corridor shader's doublets).
//   REACTIONS      nothing the storm touches reacts at cast time. Each thing begins when the front
//                  REACHES it - water first if water is first, the fire a beat later, the infection
//                  after - and they all ride the one shared turbulence. That is what makes fire,
//                  water and infection read as one wind acting on three materials rather than three
//                  effects fired together (WindReactions).
//   LETTING GO     at the endpoint the front runs on a little and dissolves, the ribbons outlive it
//                  by a tenth of a second, the last motes drift out, the flow dies, the origin
//                  folds. No flash, no shake, no haze: what the player is left looking at is what
//                  CHANGED, where it went, and which way the wind blew - in that order.
//
// THE BOARD IS ASKED TO STAND BACK only where Core has already changed it: the cell a pushed cube
// came to rest on, a block an ember lit, a fire the water put out (BoardView.HoldCells). Each is
// given back the moment its own reaction lands.
//
// THE VIEW DECIDES NOTHING. Every target, route, rest cell and face is WindVisuals (Core, reporting
// only, matched by identity); the front's timing is a projection onto the gust, which changes when
// a thing is SEEN to happen and never what happens. Input is not held for it: the hero reactions
// are readable inside ~1.3 s and the rest finishes on its own.
// EXTENSION POINT: a new carried thing is a WindReaction and a case in Build; a new beat is a
// WindBeat the lab can isolate.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class WindStormView : MonoBehaviour
    {
        /// <summary>A cube's face as the board drew it.</summary>
        public struct Face
        {
            public Sprite Tile;
            public Color Colour;
        }

        public Action<WindCue, float> Sounded;
        public Action<WindHaptic> Haptic;

        /// <summary>World units per screen pixel.</summary>
        public Func<float> Pixel;

        private sealed class Ribbon
        {
            public WindRibbon Body;
            public float Born = -10f;
            public float Life;
            public float Start;
            public float Length;
            public float Across;
            public float Speed;
            public float Breaks;
            public int Seed;
        }

        private sealed class Mote
        {
            public SpriteRenderer Body;
            public float Born = -10f;
            public float Life;
            public float Start;
            public float Across;
            public float Speed;
            public float Pixels;
            public int Seed;
        }

        private sealed class Doused
        {
            public GridPos Cell;
            public Sprite Tile;
            public float From;
            public float Until;
            public SpriteRenderer Body;
            public readonly List<SpriteRenderer> Steam = new List<SpriteRenderer>();
            public bool Done;
        }

        private readonly WindStage stage = new WindStage();
        private WindSprites sprites;
        private WindCorridor corridor;
        private WindDebugDraw debug;
        private readonly WindIgnitions ignitions = new WindIgnitions();
        private readonly List<WindReaction> reactions = new List<WindReaction>();
        private readonly List<Ribbon> ribbons = new List<Ribbon>();
        private readonly List<Mote> motes = new List<Mote>();
        private readonly List<Doused> doused = new List<Doused>();
        private readonly List<SpriteRenderer> knot = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> burst = new List<SpriteRenderer>();
        private SpriteRenderer ring;

        private WindVisuals report;
        private WindVisuals lastPlayed;
        private float clock = -1f;
        private float span;
        private float heroEnd;
        private float flow;
        private int spawned;
        private bool released;
        private bool tailed;

        // the lab: run fast up to a beat, play it, hold, stop
        private bool isolating;
        private float isolateFrom;
        private float isolateTo;
        private float holdClock;

        public bool Busy
        {
            get { return clock >= 0f; }
        }

        /// <summary>Seconds from the start of the cast until its readable part is over.</summary>
        public float HeroSeconds
        {
            get { return heroEnd; }
        }

        /// <summary>The cast now playing (null when idle) - the lab's label and debug read it.</summary>
        public WindVisuals Playing
        {
            get { return clock >= 0f ? report : null; }
        }

        private void Ensure()
        {
            if (sprites == null)
            {
                sprites = new WindSprites(transform);
                corridor = WindCorridor.Make(transform, WindOrders.Corridor);
                debug = new WindDebugDraw(transform);
                stage.Sprites = sprites;
                stage.Ignitions = ignitions;
                stage.Sound = delegate(WindCue cue)
                {
                    if (Sounded != null && !FastForwarding)
                    {
                        Sounded(cue, stage.Clock.Travel);
                    }
                };
                stage.Haptic = delegate(WindHaptic beat)
                {
                    if (Haptic != null && !FastForwarding)
                    {
                        Haptic(beat);
                    }
                };
            }
        }

        private bool FastForwarding
        {
            get { return isolating && clock < isolateFrom; }
        }

        // ================================================================== play / stop

        /// <summary>
        /// Plays one cast. Keyed on the report ITSELF, so a repaint can never restart it.
        /// <paramref name="before"/> is every cube in the lane as the board drew it BEFORE the
        /// rules ran - the face a block burns from, and the face a pushed cube wears, are gone from
        /// the board by now.
        /// </summary>
        public void Play(BoardView on, WindVisuals cast, IDictionary<GridPos, Face> before)
        {
            if (on == null || cast == null || cast.Gust == null || !cast.Gust.Valid
                || ReferenceEquals(cast, lastPlayed))
            {
                return;
            }
            Stop();
            Ensure();
            lastPlayed = cast;
            report = cast;
            clock = 0f;
            flow = 0f;
            spawned = 0;
            released = false;
            tailed = false;
            isolating = false;
            stage.View = on;
            stage.Gust = cast.Gust;
            stage.Clock = WindFx.Clock.For(cast.Gust);
            stage.Seed = cast.Seed;
            stage.Now = 0f;
            Measure();
            Build(before);
            stage.Cue(WindCue.CastLock);
        }

        /// <summary>The lab: run the cast fast up to this beat, play it at speed, hold, stop.
        /// False when nothing in this cast has that beat.</summary>
        public bool Isolate(WindBeat beat)
        {
            if (clock < 0f)
            {
                return false;
            }
            float from;
            float to;
            if (!ignitions.TryBeat(beat, out from, out to))
            {
                bool found = false;
                foreach (WindReaction r in reactions)
                {
                    if (r.TryBeat(beat, out from, out to))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return false;
                }
            }
            isolating = true;
            isolateFrom = Mathf.Max(0f, from - 0.02f);
            isolateTo = to + 0.02f;
            holdClock = 0f;
            return true;
        }

        public void Stop()
        {
            if (sprites == null)
            {
                clock = -1f;
                return;
            }
            foreach (WindReaction r in reactions)
            {
                r.Release(stage);
            }
            reactions.Clear();
            ignitions.Release(stage);
            ignitions.Clear();
            foreach (Doused d in doused)
            {
                sprites.Return(ref d.Body);
                sprites.ReturnAll(d.Steam);
            }
            doused.Clear();
            if (stage.View != null)
            {
                stage.UnholdAll();
            }
            foreach (Ribbon r in ribbons)
            {
                r.Body.Hide();
                r.Born = -10f;
            }
            foreach (Mote m in motes)
            {
                sprites.Return(ref m.Body);
                m.Born = -10f;
            }
            sprites.ReturnAll(knot);
            sprites.ReturnAll(burst);
            sprites.Return(ref ring);
            corridor.Hide();
            debug.Clear();
            clock = -1f;
            isolating = false;
            report = null;
        }

        // ================================================================== building the cast

        private void Build(IDictionary<GridPos, Face> before)
        {
            BoardView view = stage.View;
            var dousedCells = new HashSet<GridPos>();
            foreach (WindDoused d in report.Doused)
            {
                dousedCells.Add(d.Cell);
            }

            // the blocks the embers lit, as they stood and as Core has made them - first, because
            // every fire registers its strikes against them
            foreach (SpreadIgnition lit in report.Ignitions)
            {
                Face was = FaceOf(before, lit.Cell, lit.Was);
                var burning = new Cube(CubeKind.Fire, lit.Was.SourceCardId);
                ignitions.Register(lit, was.Tile, was.Colour, view.FaceOf(burning), Color.white,
                    dousedCells.Contains(lit.Cell));
            }

            // FIRE: one reaction per fire, its embers shared out of one budget for the whole gust
            var bySource = new Dictionary<GridPos, List<WindEmber>>();
            var order = new List<GridPos>();
            foreach (WindEmber ember in report.Embers)
            {
                List<WindEmber> list;
                if (!bySource.TryGetValue(ember.Source, out list))
                {
                    list = new List<WindEmber>();
                    bySource[ember.Source] = list;
                    order.Add(ember.Source);
                }
                list.Add(ember);
            }
            int total = Mathf.Max(1, report.Embers.Count);
            int heroes = Mathf.Clamp(WindFx.HeroScrapBudget / total, 1, 2);
            int tinies = Mathf.Clamp(WindFx.TinyEmberBudget / total, 1, 5);
            if (WindFx.Layers.Reactions)
            {
                foreach (GridPos source in order)
                {
                    reactions.Add(new WindFireReaction(stage, source, bySource[source], heroes, tinies));
                }
            }

            // WATER: the pushed cubes, then whatever only fell, then what was pressed and stayed
            float firstPush = float.MaxValue;
            float lastWater = 0f;
            foreach (WindPush push in report.Pushes)
            {
                Face look = FaceOf(before, push.From, push.Cube);
                var water = new WindWaterReaction(stage, push, look.Tile, look.Colour);
                reactions.Add(water);
                firstPush = Mathf.Min(firstPush, stage.Clock.ReachTime(stage.Along(push.From)));
                lastWater = Mathf.Max(lastWater, water.Arrival);
            }
            if (firstPush == float.MaxValue)
            {
                firstPush = stage.Clock.Lock;
            }
            foreach (List<GridPos> chain in Chains(report.OtherFallFrames))
            {
                GridPos restCell = chain[chain.Count - 1];
                Sprite tile;
                Color colour;
                if (!view.TryCubeLook(restCell, 0f, out tile, out colour))
                {
                    tile = view.FaceOf(new Cube(CubeKind.Water, 0));
                    colour = Color.white;
                }
                var falling = new WindWaterReaction(stage, chain, tile, colour, firstPush + 0.14f);
                reactions.Add(falling);
                lastWater = Mathf.Max(lastWater, falling.Arrival);
            }

            // BYSTANDERS: the front passing over what it does not move
            if (WindFx.Layers.Reactions)
            {
                foreach (WindBystander by in report.Bystanders)
                {
                    if (by.Reaction == WindReactionKind.LeanOnly)
                    {
                        Face look = FaceOf(before, by.Cell, by.Cube);
                        reactions.Add(new WindWaterReaction(stage, by.Cell, look.Tile, look.Colour));
                    }
                    else
                    {
                        reactions.Add(new WindBystanderReaction(stage, by.Cell));
                    }
                }
            }

            // INFECTION
            foreach (WindCarry carry in report.Gust.Carries)
            {
                reactions.Add(new WindInfectionReaction(stage, carry));
            }

            // THE FIRES THE WATER PUT OUT: they stay fire until the water is there
            foreach (WindDoused d in report.Doused)
            {
                float from = ignitions.EndOf(d.Cell);
                doused.Add(new Doused
                {
                    Cell = d.Cell,
                    Tile = view.FaceOf(d.Was),
                    From = from,
                    Until = Mathf.Max(from, lastWater)
                });
                stage.Hold(d.Cell);
            }

            float end = stage.Clock.End + WindFx.Style.ReleaseSeconds + WindFx.Style.RibbonTail + 0.25f;
            heroEnd = stage.Clock.End;
            foreach (WindReaction r in reactions)
            {
                end = Mathf.Max(end, r.End);
                if (r.Reaction != WindReactionKind.None)
                {
                    heroEnd = Mathf.Max(heroEnd, r.HeroEnd);
                }
            }
            end = Mathf.Max(end, ignitions.End);
            heroEnd = Mathf.Max(heroEnd, ignitions.End);
            foreach (Doused d in doused)
            {
                end = Mathf.Max(end, d.Until + 0.3f);
            }
            span = end + 0.05f;
        }

        private Face FaceOf(IDictionary<GridPos, Face> before, GridPos cell, Cube cube)
        {
            Face face;
            if (before != null && before.TryGetValue(cell, out face) && face.Tile != null)
            {
                return face;
            }
            return new Face { Tile = stage.View.FaceOf(cube), Colour = Color.white };
        }

        /// <summary>The reported fall moves, strung into one route per cube.</summary>
        private static List<List<GridPos>> Chains(IReadOnlyList<IReadOnlyList<WaterMove>> frames)
        {
            var chains = new List<List<GridPos>>();
            var open = new Dictionary<GridPos, List<GridPos>>();
            foreach (IReadOnlyList<WaterMove> frame in frames)
            {
                var next = new Dictionary<GridPos, List<GridPos>>();
                foreach (WaterMove move in frame)
                {
                    List<GridPos> chain;
                    if (open.TryGetValue(move.From, out chain))
                    {
                        open.Remove(move.From);
                    }
                    else
                    {
                        chain = new List<GridPos> { move.From };
                        chains.Add(chain);
                    }
                    chain.Add(move.To);
                    next[move.To] = chain;
                }
                foreach (KeyValuePair<GridPos, List<GridPos>> still in open)
                {
                    next[still.Key] = still.Value;
                }
                open = next;
            }
            return chains;
        }

        // ================================================================== the frame

        private void Measure()
        {
            BoardView view = stage.View;
            WindGust gust = stage.Gust;
            stage.Cell = view.CellSizeInWorld;
            stage.Cube = view.CubeWorldSize * Mathf.Abs(view.transform.lossyScale.x);
            stage.Pixel = Pixel != null ? Pixel() : stage.Cell / 96f;
            Vector2 a = view.BoardPointToWorld(gust.StartX, gust.StartY);
            Vector2 b = view.BoardPointToWorld(gust.EndX, gust.EndY);
            stage.Dir = (b - a).sqrMagnitude > 1e-8f ? (b - a).normalized : Vector2.right;
            stage.Side = new Vector2(-stage.Dir.y, stage.Dir.x);
            stage.Angle = Mathf.Atan2(stage.Dir.y, stage.Dir.x) * Mathf.Rad2Deg;
        }

        private void Update()
        {
            if (clock < 0f)
            {
                return;
            }
            if (stage.View == null || stage.View.Board == null)
            {
                Stop();
                return;
            }
            float dt = Time.deltaTime;
            if (isolating)
            {
                if (clock < isolateFrom)
                {
                    dt = Mathf.Min(isolateFrom - clock + 0.0001f, dt * 40f);
                }
                else if (clock >= isolateTo)
                {
                    // the beat is over: hold the picture a moment, then put everything back
                    dt = 0f;
                    holdClock += Time.unscaledDeltaTime;
                    if (holdClock > 1.1f)
                    {
                        Stop();
                        return;
                    }
                }
            }
            clock += dt;
            stage.Now = clock;
            Measure();
            PaintCorridor(dt);
            PaintOrigin();
            PaintRibbons();
            PaintMotes();
            ignitions.Tick(stage);
            for (int i = 0; i < reactions.Count; i++)
            {
                reactions[i].Tick(stage);
            }
            PaintDoused();
            debug.Draw(stage, report, reactions, sprites.Live, ribbons.Count);
            if (!isolating && clock > span)
            {
                Stop();
            }
        }

        private void PaintCorridor(float dt)
        {
            WindFx.Clock c = stage.Clock;
            WindGust gust = stage.Gust;
            float lane = gust.Length + 1f;
            float t = clock;
            var look = new WindCorridor.Look
            {
                Tint = WindFx.Style.Air,
                Edge = WindFx.Style.Air,
                Fade = 1f
            };
            if (t < c.Lock)
            {
                // PRESSURE LOCK: the aim's own lane, drawing in
                float k = WindFx.Smooth(t / c.Lock);
                flow += dt * Mathf.Lerp(WindFx.Style.PreviewFlowSpeed, 0.2f, k);
                look.BodyAlpha = WindFx.Style.PreviewBodyAlpha;
                look.EdgeAlpha = WindFx.Style.PreviewEdgeAlpha;
                look.Doublet = WindFx.Layers.Distortion ? WindFx.Style.PreviewDoublet * WindFx.DoubletScale * (1f - k) : 0f;
                look.Front = 999f;
                look.Reveal = lane;
                look.Squeeze = k;
            }
            else
            {
                float since = t - c.Lock;
                float front = c.FrontAt(t) + 0.5f; // in the quad's own cells, from its back edge
                float after = t - c.End;
                flow += dt * WindFx.Style.FlowSpeed;
                look.Squeeze = 1f - WindFx.Smooth(since / 0.07f);
                look.BodyAlpha = WindFx.Layers.Body ? WindFx.Style.BodyAlpha : 0f;
                look.EdgeAlpha = WindFx.Layers.Body ? WindFx.Style.EdgeAlpha : 0f;
                look.Doublet = WindFx.Layers.Distortion ? WindFx.Style.Doublet * WindFx.DoubletScale : 0f;
                look.Reveal = Mathf.Min(front, lane + 0.15f);
                if (after <= 0f)
                {
                    look.Front = front;
                    look.FrontAlpha = WindFx.Layers.Front ? WindFx.Style.FrontAlpha * Mathf.Clamp01(since / 0.05f) : 0f;
                }
                else
                {
                    // LETTING GO: the front runs on a little, thins, and is gone; the flow dies
                    // with it and the lane a breath later
                    float k = Mathf.Clamp01(after / WindFx.Style.ReleaseSeconds);
                    look.Front = lane + after * c.Speed * 0.45f;
                    look.FrontAlpha = WindFx.Layers.Front ? WindFx.Style.FrontAlpha * (1f - k) * (1f - k) : 0f;
                    look.Doublet *= 1f - Mathf.Clamp01(after / WindFx.Style.DistortionTail);
                    look.Fade = 1f - WindFx.Smooth((after - WindFx.Style.RibbonTail) / 0.22f);
                }
            }
            look.Flow = flow;
            Vector2 back = stage.World(new Vector2(gust.StartX, gust.StartY)) - stage.Dir * (0.5f * stage.Cell);
            corridor.Set(back, stage.Angle, lane + 1f, stage.Cell, look);

            if (!released && t >= c.Lock)
            {
                released = true;
                stage.Cue(WindCue.Release);
                stage.Tap(WindHaptic.Cast);
            }
            if (!tailed && t >= c.End)
            {
                tailed = true;
                stage.Cue(WindCue.Tail);
            }
        }

        /// <summary>The origin: gathered and brightening through the lock, a compression and a
        /// burst of air at the release, then folding away.</summary>
        private void PaintOrigin()
        {
            WindFx.Clock c = stage.Clock;
            Vector2 at = stage.World(new Vector2(stage.Gust.StartX, stage.Gust.StartY));
            float t = clock;
            float lockK = Mathf.Clamp01(t / c.Lock);
            float gone = Mathf.Clamp01((t - c.Lock - 0.1f) / 0.18f);
            if (gone >= 1f)
            {
                sprites.ReturnAll(knot);
            }
            else
            {
                while (knot.Count < 3)
                {
                    knot.Add(sprites.RentGlow(WindShapes.Arc, WindOrders.Aim));
                }
                // drawn in and brighter as the air is pressed, collapsing once it has gone
                float size = stage.Cell * Mathf.Lerp(0.5f, 0.3f, WindFx.Smooth(lockK)) * (1f - 0.6f * gone);
                float alpha = Mathf.Lerp(0.32f, 0.6f, lockK) * (1f - gone);
                for (int i = 0; i < knot.Count; i++)
                {
                    float angle = t * (60f + 240f * lockK) + i * 120f;
                    WindSprites.Place(knot[i], at, angle, size, size, WindFx.Tint(WindFx.Style.AirLavender, alpha));
                }
            }
            // the release: a ring that opens, and wisps thrown forward - air, not an explosion
            float k = (t - c.Lock) / 0.2f;
            if (k > 0f && k < 1f)
            {
                if (ring == null)
                {
                    ring = sprites.RentGlow(WindShapes.Ring, WindOrders.Aim);
                    int count = WindFx.Level == WindFx.Quality.Low ? 3 : 5;
                    for (int i = 0; i < count; i++)
                    {
                        burst.Add(sprites.RentGlow(WindShapes.WispRooted, WindOrders.Aim));
                    }
                }
                float size = stage.Cell * Mathf.Lerp(0.45f, 1.5f, WindFx.Smooth(k));
                WindSprites.Place(ring, at, 0f, size, size, WindFx.Tint(WindFx.Style.Air, 0.26f * (1f - k) * (1f - k)));
                for (int i = 0; i < burst.Count; i++)
                {
                    float spread = burst.Count == 1 ? 0f : (i / (float)(burst.Count - 1) - 0.5f) * 64f;
                    float length = stage.Cell * (0.5f + 0.9f * WindFx.Smooth(k));
                    WindSprites.Place(burst[i], at, stage.Angle + spread, length, stage.Cell * 0.2f,
                        WindFx.Tint(WindFx.Style.Air, 0.3f * Mathf.Sin(k * Mathf.PI)));
                }
            }
            else if (k >= 1f && ring != null)
            {
                sprites.Return(ref ring);
                sprites.ReturnAll(burst);
            }
        }

        /// <summary>The flow ribbons: born behind the front, racing down the lane, dissolving.</summary>
        private void PaintRibbons()
        {
            WindFx.Clock c = stage.Clock;
            int budget = WindFx.Layers.Ribbons ? WindFx.RibbonBudget : 0;
            while (ribbons.Count < budget)
            {
                ribbons.Add(new Ribbon { Body = WindRibbon.Make(transform, WindOrders.Ribbon) });
            }
            float lane = stage.Gust.Length + 1f;
            float front = c.FrontAt(clock);
            bool blowing = clock >= c.Lock && clock <= c.End + WindFx.Style.RibbonTail;
            var start = new Vector2(stage.Gust.StartX, stage.Gust.StartY);
            var dir = new Vector2(stage.Gust.DirX, stage.Gust.DirY);
            var side = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < ribbons.Count; i++)
            {
                Ribbon r = ribbons[i];
                float age = clock - r.Born;
                if (age < 0f || age >= r.Life)
                {
                    r.Body.Hide();
                    if (!blowing || i >= budget)
                    {
                        continue;
                    }
                    // a new one, somewhere in the lane the front has already crossed
                    int n = spawned++;
                    r.Seed = stage.Seed + n * 13;
                    float h = WindFx.Hash(r.Seed, 61);
                    r.Length = lane * Mathf.Lerp(WindFx.Style.RibbonMinShare, WindFx.Style.RibbonMaxShare,
                        WindFx.Hash(r.Seed, 63));
                    r.Start = Mathf.Lerp(-0.5f, Mathf.Max(-0.4f, Mathf.Min(front, lane - 0.5f) - r.Length * 0.6f), h);
                    r.Across = (WindFx.Hash(r.Seed, 67) * 2f - 1f) * 1.2f;
                    r.Speed = c.Speed * Mathf.Lerp(0.95f, 1.35f, WindFx.Hash(r.Seed, 71));
                    r.Life = Mathf.Lerp(0.2f, 0.36f, WindFx.Hash(r.Seed, 73));
                    r.Breaks = WindFx.Hash(r.Seed, 79);
                    // staggered, so they never all leave on one frame
                    r.Born = clock + (n < budget ? n * 0.028f : 0.02f * WindFx.Hash(r.Seed, 83));
                    continue;
                }
                float k = age / r.Life;
                float head = Mathf.Min(r.Start + r.Length + r.Speed * age, lane - 0.4f + 0.6f * k);
                float tail = Mathf.Max(-0.5f, head - r.Length);
                if (head - tail < 0.25f)
                {
                    r.Body.Hide();
                    continue;
                }
                Vector2 p0 = stage.World(start + dir * tail + side * r.Across);
                Vector2 p1 = stage.World(start + dir * head + side * r.Across);
                float now = clock;
                int seed = r.Seed;
                float span0 = head - tail;
                float cell = stage.Cell;
                r.Body.Draw(p0, p1,
                    q => WindFx.RibbonWander(tail + span0 * q, now, seed) * cell,
                    stage.Cell * WindFx.Style.RibbonWidth,
                    WindFx.Tint(Color.Lerp(WindFx.Style.Air, WindFx.Style.AirLavender, r.Breaks * 0.5f),
                        WindFx.Style.RibbonAlpha * Mathf.Sin(k * Mathf.PI)),
                    r.Breaks);
            }
        }

        /// <summary>The motes: a handful of soft slivers carried down the lane.</summary>
        private void PaintMotes()
        {
            WindFx.Clock c = stage.Clock;
            float lane = stage.Gust.Length + 1f;
            int budget = WindFx.Layers.Motes ? Mathf.RoundToInt(WindFx.MoteBudget * Mathf.Clamp01(lane / 9.5f)) : 0;
            while (motes.Count < budget)
            {
                motes.Add(new Mote());
            }
            float front = c.FrontAt(clock);
            bool blowing = clock >= c.Lock && clock <= c.End + 0.05f;
            var start = new Vector2(stage.Gust.StartX, stage.Gust.StartY);
            var dir = new Vector2(stage.Gust.DirX, stage.Gust.DirY);
            var side = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < motes.Count; i++)
            {
                Mote m = motes[i];
                float age = clock - m.Born;
                if (age < 0f || age >= m.Life)
                {
                    if (m.Body != null)
                    {
                        sprites.Return(ref m.Body);
                    }
                    if (!blowing || i >= budget)
                    {
                        continue;
                    }
                    m.Seed = stage.Seed + (spawned++) * 7 + i;
                    m.Start = Mathf.Lerp(-0.5f, Mathf.Max(-0.3f, Mathf.Min(front, lane - 0.5f) - 0.3f),
                        WindFx.Hash(m.Seed, 89));
                    m.Across = (WindFx.Hash(m.Seed, 97) * 2f - 1f) * 1.35f;
                    m.Speed = c.Speed * Mathf.Lerp(0.7f, 1.25f, WindFx.Hash(m.Seed, 101));
                    m.Life = Mathf.Lerp(0.22f, 0.5f, WindFx.Hash(m.Seed, 103));
                    m.Pixels = Mathf.Lerp(WindFx.Style.MotePixelsMin, WindFx.Style.MotePixelsMax,
                        WindFx.Hash(m.Seed, 107));
                    m.Born = clock + 0.3f * WindFx.Hash(m.Seed, 109) * (clock < c.Lock + 0.05f ? 1f : 0.15f);
                    continue;
                }
                if (m.Body == null)
                {
                    m.Body = sprites.RentGlow(WindShapes.Shard, WindOrders.Mote);
                }
                float k = age / m.Life;
                // the last of them drift out past the end and fade, never stopping on a wall
                var board = start + dir * (m.Start + m.Speed * age) + side * m.Across;
                Vector2 at = stage.World(board) + stage.Drift(board, m.Seed) * 1.4f;
                float size = stage.Pixel * m.Pixels;
                WindSprites.Place(m.Body, at, stage.Angle, size * 2.4f, size * 1.2f,
                    WindFx.Tint(WindFx.Style.Air, WindFx.Style.MoteAlpha * Mathf.Sin(k * Mathf.PI)));
            }
        }

        /// <summary>The fires the gust's water put out: fire until the water is beside them, then a
        /// breath of steam and the stone Core already made of them.</summary>
        private void PaintDoused()
        {
            for (int i = 0; i < doused.Count; i++)
            {
                Doused d = doused[i];
                if (d.Done || clock < d.From)
                {
                    continue;
                }
                Vector2 at = stage.World(d.Cell);
                if (clock < d.Until)
                {
                    if (d.Body == null)
                    {
                        d.Body = sprites.Rent(d.Tile, WindOrders.IgniteFire, ViewUtil.TileMaterial(d.Tile));
                    }
                    WindSprites.Place(d.Body, at, 0f, stage.Cube, stage.Cube, Color.white);
                    continue;
                }
                if (d.Body != null)
                {
                    sprites.Return(ref d.Body);
                    stage.Unhold(d.Cell);
                    for (int s = 0; s < 3; s++)
                    {
                        d.Steam.Add(sprites.RentGlow(WindShapes.Dot, WindOrders.Ember));
                    }
                }
                float k = (clock - d.Until) / 0.3f;
                if (k >= 1f)
                {
                    sprites.ReturnAll(d.Steam);
                    d.Done = true;
                    continue;
                }
                for (int s = 0; s < d.Steam.Count; s++)
                {
                    float h = WindFx.Hash(d.Cell.X * 11 + d.Cell.Y * 3 + s, 113);
                    Vector2 p = at + new Vector2((h - 0.5f) * stage.Cube * 0.6f, stage.Cube * (0.15f + 0.55f * k));
                    float size = stage.Cube * (0.3f + 0.3f * k);
                    WindSprites.Place(d.Steam[s], p, 0f, size, size, WindFx.Tint(WindFx.Style.Air, 0.12f * (1f - k)));
                }
            }
        }
    }

    /// <summary>
    /// The development overlays: the storm's own geometry and what the report says, drawn over the
    /// board so "is the lane where the rules put it" and "did that go where Core said" are a glance.
    /// </summary>
    internal sealed class WindDebugDraw
    {
        private readonly Transform parent;
        private readonly List<SpriteRenderer> lines = new List<SpriteRenderer>();
        private int used;
        private TextMesh label;

        public WindDebugDraw(Transform parent)
        {
            this.parent = parent;
        }

        public void Clear()
        {
            for (int i = 0; i < lines.Count; i++)
            {
                lines[i].enabled = false;
            }
            used = 0;
            if (label != null)
            {
                label.gameObject.SetActive(false);
            }
        }

        private void Line(Vector2 a, Vector2 b, Color colour, float width)
        {
            if (used >= lines.Count)
            {
                var go = new GameObject("WindDebug");
                go.transform.SetParent(parent, false);
                var made = go.AddComponent<SpriteRenderer>();
                made.sprite = WindShapes.Pixel;
                made.sortingOrder = WindOrders.Debug;
                lines.Add(made);
            }
            SpriteRenderer r = lines[used++];
            r.enabled = true;
            Vector2 d = b - a;
            WindSprites.Place(r, (a + b) * 0.5f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg,
                Mathf.Max(d.magnitude, width), width, colour);
        }

        private void Box(Vector2 at, float half, Color colour, float width)
        {
            Line(at + new Vector2(-half, -half), at + new Vector2(half, -half), colour, width);
            Line(at + new Vector2(half, -half), at + new Vector2(half, half), colour, width);
            Line(at + new Vector2(half, half), at + new Vector2(-half, half), colour, width);
            Line(at + new Vector2(-half, half), at + new Vector2(-half, -half), colour, width);
        }

        private void Cross(Vector2 at, float half, Color colour, float width)
        {
            Line(at + new Vector2(-half, -half), at + new Vector2(half, half), colour, width);
            Line(at + new Vector2(-half, half), at + new Vector2(half, -half), colour, width);
        }

        /// <summary>The corridor's own geometry (shared by the aim and the cast).</summary>
        public void Geometry(WindStage stage, IReadOnlyList<WindAffected> affected)
        {
            WindGust gust = stage.Gust;
            float w = Mathf.Max(stage.Pixel * 1.5f, stage.Cell * 0.02f);
            Vector2 start = stage.World(new Vector2(gust.StartX, gust.StartY));
            Vector2 end = stage.World(new Vector2(gust.EndX, gust.EndY));
            float half = WindGust.Width * 0.5f * stage.Cell;
            if (WindFx.DebugFlags.ShowWindOrigin)
            {
                Cross(start, stage.Cell * 0.16f, Color.green, w);
            }
            if (WindFx.DebugFlags.ShowWindEndpoint)
            {
                Cross(end, stage.Cell * 0.16f, Color.red, w);
            }
            if (WindFx.DebugFlags.ShowWindVector)
            {
                Line(start, end, Color.yellow, w);
            }
            if (WindFx.DebugFlags.ShowWindWidth)
            {
                Line(start - stage.Side * half, start + stage.Side * half, Color.cyan, w);
                Line(end - stage.Side * half, end + stage.Side * half, Color.cyan, w);
            }
            if (WindFx.DebugFlags.ShowWindMaxLength)
            {
                Vector2 max = start + stage.Dir * (gust.MaxLength * stage.Cell);
                Line(end, max, new Color(1f, 0.5f, 0f, 0.8f), w);
                Cross(max, stage.Cell * 0.1f, new Color(1f, 0.5f, 0f), w);
            }
            if (WindFx.DebugFlags.ShowWindCorridorBounds)
            {
                Vector2 back = start - stage.Dir * (0.5f * stage.Cell);
                Vector2 fore = end + stage.Dir * (0.5f * stage.Cell);
                Line(back - stage.Side * half, fore - stage.Side * half, Color.magenta, w);
                Line(back + stage.Side * half, fore + stage.Side * half, Color.magenta, w);
                Line(back - stage.Side * half, back + stage.Side * half, Color.magenta, w);
                Line(fore - stage.Side * half, fore + stage.Side * half, Color.magenta, w);
            }
            if (WindFx.DebugFlags.ShowWindAffectedCells)
            {
                foreach (GridPos cell in gust.Cells)
                {
                    Box(stage.World(cell), stage.Cell * 0.46f, new Color(1f, 1f, 0.3f, 0.7f), w);
                }
            }
            if (WindFx.DebugFlags.ShowWindAffectedEntities && affected != null)
            {
                foreach (WindAffected a in affected)
                {
                    Color c = a.Reaction == WindReactionKind.ParticleTransfer ? new Color(1f, 0.5f, 0.1f)
                        : a.Reaction == WindReactionKind.PhysicalMove ? new Color(0.3f, 0.7f, 1f)
                        : a.Reaction == WindReactionKind.DuplicateSpread ? new Color(0.4f, 1f, 0.4f)
                        : new Color(0.7f, 0.7f, 0.7f);
                    Box(stage.World(a.Cell), stage.Cell * 0.36f, c, w * 1.5f);
                }
            }
        }

        public void Draw(WindStage stage, WindVisuals report, List<WindReaction> reactions, int sprites, int ribbons)
        {
            Clear();
            if (!WindFx.DebugFlags.Any || report == null)
            {
                return;
            }
            float w = Mathf.Max(stage.Pixel * 1.5f, stage.Cell * 0.02f);
            Geometry(stage, null);
            WindGust gust = stage.Gust;
            float half = WindGust.Width * 0.5f * stage.Cell;
            if (WindFx.DebugFlags.ShowStormFront || WindFx.DebugFlags.ShowInteractionTiming)
            {
                Vector2 at = stage.World(new Vector2(gust.StartX, gust.StartY))
                    + stage.Dir * (Mathf.Clamp(stage.Clock.FrontAt(stage.Now), -0.5f, gust.Length + 0.5f) * stage.Cell);
                Line(at - stage.Side * half, at + stage.Side * half, Color.white, w * 1.5f);
            }
            if (WindFx.DebugFlags.ShowWindAffectedEntities)
            {
                foreach (WindBystander by in report.Bystanders)
                {
                    Box(stage.World(by.Cell), stage.Cell * 0.3f, new Color(0.7f, 0.7f, 0.7f, 0.7f), w);
                }
            }
            foreach (WindEmber ember in report.Embers)
            {
                if (WindFx.DebugFlags.ShowFireTransferSources)
                {
                    Box(stage.World(ember.Source), stage.Cell * 0.4f, new Color(1f, 0.55f, 0.1f), w * 1.5f);
                }
                if (ember.Target.HasValue)
                {
                    if (WindFx.DebugFlags.ShowFireTransferTargets)
                    {
                        Cross(stage.World(ember.Target.Value), stage.Cell * 0.3f, new Color(1f, 0.2f, 0.1f), w * 1.5f);
                    }
                    if (WindFx.DebugFlags.ShowFirePaths)
                    {
                        Line(stage.World(ember.Source), stage.World(ember.Target.Value), new Color(1f, 0.6f, 0.2f, 0.8f), w);
                    }
                }
            }
            foreach (WindPush push in report.Pushes)
            {
                if (WindFx.DebugFlags.ShowWaterSource)
                {
                    Box(stage.World(push.From), stage.Cell * 0.4f, new Color(0.3f, 0.7f, 1f), w * 1.5f);
                }
                if (WindFx.DebugFlags.ShowWaterDestination)
                {
                    Cross(stage.World(push.Rest), stage.Cell * 0.3f, new Color(0.2f, 0.5f, 1f), w * 1.5f);
                }
                if (WindFx.DebugFlags.ShowWaterPath)
                {
                    Vector2 at = stage.World(push.From);
                    foreach (GridPos p in push.Path)
                    {
                        Line(at, stage.World(p), new Color(0.4f, 0.8f, 1f, 0.9f), w);
                        at = stage.World(p);
                    }
                    foreach (GridPos p in push.Fall)
                    {
                        Line(at, stage.World(p), new Color(0.2f, 0.4f, 1f, 0.9f), w);
                        at = stage.World(p);
                    }
                }
            }
            foreach (WindCarry carry in gust.Carries)
            {
                if (WindFx.DebugFlags.ShowInfectionSource)
                {
                    Box(stage.World(carry.From), stage.Cell * 0.4f, new Color(0.4f, 1f, 0.4f), w * 1.5f);
                }
                if (WindFx.DebugFlags.ShowInfectionDestination)
                {
                    Cross(stage.World(carry.To), stage.Cell * 0.3f, new Color(0.2f, 0.9f, 0.3f), w * 1.5f);
                }
                if (WindFx.DebugFlags.ShowInfectionPath)
                {
                    Line(stage.World(carry.From), stage.World(carry.To), new Color(0.5f, 1f, 0.5f, 0.8f), w);
                }
            }
            if (WindFx.DebugFlags.ShowLabel || WindFx.DebugFlags.ShowVisualBudget || WindFx.DebugFlags.ShowInteractionTiming)
            {
                string text = string.Empty;
                if (WindFx.DebugFlags.ShowLabel)
                {
                    text += "Wind\nStart (" + gust.StartX.ToString("0.0") + "," + gust.StartY.ToString("0.0") + ")  End ("
                        + gust.EndX.ToString("0.0") + "," + gust.EndY.ToString("0.0") + ")\nLength " + gust.Length.ToString("0.00")
                        + "  Width " + WindGust.Width.ToString("0") + "\nAffected " + (report.Bystanders.Count + report.Pushes.Count)
                        + "\nFire transfers " + report.Embers.Count + " (lit " + report.Ignitions.Count + ")\nWater moves "
                        + report.Pushes.Count + "\nInfection spreads " + gust.Carries.Count + "\n";
                }
                if (WindFx.DebugFlags.ShowInteractionTiming)
                {
                    text += "t " + stage.Now.ToString("0.00") + "  lock " + stage.Clock.Lock.ToString("0.00") + "  travel "
                        + stage.Clock.Travel.ToString("0.00") + "\n";
                }
                if (WindFx.DebugFlags.ShowVisualBudget)
                {
                    text += "sprites " + sprites + "  ribbons " + ribbons + "  reactions " + reactions.Count + "  quality "
                        + WindFx.Level + "\n";
                }
                if (label == null)
                {
                    label = ViewUtil.MakeText3D(parent, "WindDebugLabel", Vector2.zero, string.Empty, 90, 0.1f,
                        Color.white, WindOrders.Debug + 1, TextAnchor.UpperLeft);
                    label.anchor = TextAnchor.UpperLeft;
                }
                label.gameObject.SetActive(true);
                label.text = text;
                Rect board = stage.View.WorldRect;
                Vector2 corner = stage.View.transform.TransformPoint(new Vector2(board.xMin, board.yMax));
                label.transform.position = new Vector3(corner.x, corner.y + stage.Cell * 0.1f, 0f);
                float size = stage.Cell * 0.16f;
                label.transform.localScale = new Vector3(size, size, 1f);
            }
        }
    }
}
