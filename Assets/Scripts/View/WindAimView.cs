// PURPOSE: "Rüzgar" while it is being AIMED. The player has to read five things without a word of
// text - where the storm starts, which way it goes, how WIDE it is, how LONG it is, and what it
// will touch - so the aim is the storm itself, held still:
//
//   CURSOR   before anything is pinned, a small PRESSURE KNOT rides the pointer over the board:
//            two or three thin arcs turning slowly round a breathing dot. Not a crosshair.
//   LOCK     the press pins it: a thin ring closes IN on the cell, and the knot stays there with
//            four short wisps of air round it.
//   CORRIDOR the drag draws the lane from the origin to the pointer - the storm's own body
//            (WindCorridor: a faint band, clearer down the middle, with two soft BROKEN edges that
//            say how wide it is) and a few slow flow ribbons whose motion and taper say which way
//            it blows. No arrow, no chevrons, no two hard parallel lines, no filled cells.
//   NOSE     the end of the lane is two curved arcs closing on each other - pressure gathering, not
//            an arrowhead. It follows the pointer until the rules' own length cap, where it stays
//            and takes a small compression (never a red line: a long drag is not an error).
//   TOUCHED  everything the rules say the gust will affect leans a little the way it will blow -
//            a flame laid over, a water surface drawn out, a few spores pulled a couple of pixels.
//            It says "these", never where they will end up.
// A gust the rules refuse keeps its shape and loses its life: the flow stops, the edges go a muted
// rose and the nose folds - not a red corridor. Cancelling folds the lane back into its origin.
//
// THE VIEW DECIDES NOTHING: the lane is WindGust (the snapped start, the length cap, the cells),
// and what is touched and whether it may blow are WindPreview's. Nothing here measures a cell
// against the band.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class WindAimView : MonoBehaviour
    {
        private enum Mode
        {
            Off,
            Cursor,
            Pinned,
            Aiming,
            Folding
        }

        /// <summary>World units per screen pixel (the motes and spores are sized in pixels).</summary>
        public Func<float> Pixel;

        private Mode mode;
        private BoardView view;
        private WindPreview preview;
        private bool usable;
        private GridPos origin;
        private Vector2 cursorWorld;
        private float modeClock;
        private float flow;
        private float flowSpeed;
        private Vector2 ribbonDir;
        private float clampPulse = 1f;
        private bool wasClamped;
        private float refusedClock = 1f;
        private string refusedText;
        private float foldFrom;

        private WindSprites sprites;
        private WindCorridor corridor;
        private WindDebugDraw debug;
        private readonly WindStage stage = new WindStage();
        private readonly List<WindRibbon> ribbons = new List<WindRibbon>();
        private readonly List<SpriteRenderer> knot = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> wisps = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> nose = new List<SpriteRenderer>();
        private SpriteRenderer dot;
        private SpriteRenderer ring;
        private TextMesh tip;
        private readonly Dictionary<long, WindReaction> touched = new Dictionary<long, WindReaction>();
        private readonly List<long> stale = new List<long>();
        private readonly HashSet<long> seen = new HashSet<long>();

        /// <summary>How much of the longest gust has been drawn, 0..1 - the bed's level and pitch.</summary>
        public float Drawn
        {
            get
            {
                WindGust gust = preview != null ? preview.Gust : null;
                return mode == Mode.Aiming && gust != null && gust.Valid
                    ? Mathf.Clamp01(gust.Length / Mathf.Max(0.5f, gust.MaxLength)) : 0f;
            }
        }

        public bool Showing
        {
            get { return mode != Mode.Off; }
        }

        private void Ensure()
        {
            if (sprites == null)
            {
                sprites = new WindSprites(transform);
                corridor = WindCorridor.Make(transform, WindOrders.Corridor);
                debug = new WindDebugDraw(transform);
                stage.Sprites = sprites;
            }
        }

        // ------------------------------------------------------------------ the states

        /// <summary>The knot on the pointer, before the origin is chosen.</summary>
        public void Cursor(BoardView on, Vector2 world)
        {
            Ensure();
            if (mode != Mode.Cursor)
            {
                Enter(Mode.Cursor);
            }
            view = on;
            cursorWorld = world;
        }

        /// <summary>The origin is pinned to a cell.</summary>
        public void Pin(BoardView on, GridPos cell)
        {
            Ensure();
            view = on;
            origin = cell;
            preview = null;
            Enter(Mode.Pinned);
        }

        /// <summary>The lane as the rules would blow it. <paramref name="canBlow"/> is the whole
        /// answer (the gust itself, the charge, the turn's budget).</summary>
        public void Aim(BoardView on, WindPreview aim, bool canBlow)
        {
            Ensure();
            view = on;
            preview = aim;
            usable = canBlow;
            WindGust gust = aim != null ? aim.Gust : null;
            if (gust != null)
            {
                origin = new GridPos(Mathf.RoundToInt(gust.StartX), Mathf.RoundToInt(gust.StartY));
            }
            if (gust == null || !gust.Valid)
            {
                // a stroke too short to be a wind: the origin alone
                if (mode != Mode.Pinned)
                {
                    mode = Mode.Pinned;
                    modeClock = 1f; // no second lock animation
                }
                return;
            }
            if (mode != Mode.Aiming)
            {
                mode = Mode.Aiming;
                modeClock = 0f;
                ribbonDir = new Vector2(gust.DirX, gust.DirY);
            }
            if (gust.Clamped && !wasClamped)
            {
                clampPulse = 0f;
            }
            wasClamped = gust.Clamped;
        }

        /// <summary>The stroke was let go and the rules refused it: the nose folds and a small
        /// word says why.</summary>
        public void Refuse(string why)
        {
            refusedClock = 0f;
            refusedText = why;
        }

        /// <summary>The aim is given up: the lane folds back into its origin and is gone.</summary>
        public void Cancel()
        {
            if (mode == Mode.Off)
            {
                return;
            }
            WindGust gust = preview != null ? preview.Gust : null;
            if (mode == Mode.Aiming && gust != null && gust.Valid)
            {
                foldFrom = gust.Length + 1f;
                Enter(Mode.Folding);
            }
            else
            {
                Hide();
            }
        }

        /// <summary>Gone at once - the storm takes the lane over on this very frame.</summary>
        public void Hide()
        {
            mode = Mode.Off;
            preview = null;
            refusedClock = 1f;
            wasClamped = false;
            if (sprites == null)
            {
                return;
            }
            corridor.Hide();
            foreach (WindRibbon r in ribbons)
            {
                r.Hide();
            }
            sprites.ReturnAll(knot);
            sprites.ReturnAll(wisps);
            sprites.ReturnAll(nose);
            sprites.Return(ref dot);
            sprites.Return(ref ring);
            ReleaseTouched(null);
            debug.Clear();
            if (tip != null)
            {
                tip.gameObject.SetActive(false);
            }
        }

        private void Enter(Mode next)
        {
            mode = next;
            modeClock = 0f;
        }

        // ------------------------------------------------------------------ painting

        private void LateUpdate()
        {
            if (mode == Mode.Off || view == null || view.Board == null)
            {
                if (mode != Mode.Off)
                {
                    Hide();
                }
                return;
            }
            float dt = Time.unscaledDeltaTime;
            modeClock += dt;
            clampPulse = Mathf.Min(1f, clampPulse + dt / 0.22f);
            refusedClock = Mathf.Min(1f, refusedClock + dt / 1.3f);
            float cell = view.CellSizeInWorld;
            float pixel = Pixel != null ? Pixel() : cell / 96f;
            float now = Time.unscaledTime;

            if (mode == Mode.Cursor)
            {
                corridor.Hide();
                HideRibbons(0);
                sprites.ReturnAll(nose);
                sprites.ReturnAll(wisps);
                sprites.Return(ref ring);
                ReleaseTouched(null);
                PaintKnot(cursorWorld, cell, now, 0.8f, 1f);
                return;
            }

            Vector2 start = view.BoardPointToWorld(origin.X, origin.Y);
            if (mode == Mode.Pinned)
            {
                corridor.Hide();
                HideRibbons(0);
                sprites.ReturnAll(nose);
                ReleaseTouched(null);
                PaintLock(start, cell, now);
                return;
            }

            WindGust gust = preview.Gust;
            start = view.BoardPointToWorld(gust.StartX, gust.StartY);
            Vector2 end = view.BoardPointToWorld(gust.EndX, gust.EndY);
            Vector2 dir = (end - start).sqrMagnitude > 1e-6f ? (end - start).normalized : Vector2.right;
            var side = new Vector2(-dir.y, dir.x);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            bool alive = usable && preview.Valid;
            float lane = gust.Length + 1f;

            // FOLDING: the lane draws back into the origin, quickly, and is gone
            float shown = lane;
            float fade = 1f;
            if (mode == Mode.Folding)
            {
                float k = Mathf.Clamp01(modeClock / 0.14f);
                shown = Mathf.Lerp(foldFrom, 0f, WindFx.Smooth(k));
                fade = 1f - Mathf.Clamp01((modeClock - 0.1f) / 0.12f);
                if (modeClock > 0.24f)
                {
                    Hide();
                    return;
                }
            }

            // the air runs while the gust is alive, and simply stops when it is not
            flowSpeed = Mathf.MoveTowards(flowSpeed, alive && mode == Mode.Aiming ? WindFx.Style.PreviewFlowSpeed : 0f,
                dt * 14f);
            flow += flowSpeed * dt;

            // THE CORRIDOR: the storm's own body, calmer
            Color edge = Color.Lerp(WindFx.Style.Refused, WindFx.Style.Air, alive ? 1f : 0f);
            var look = new WindCorridor.Look
            {
                Tint = alive ? WindFx.Style.Air : WindFx.Style.AirDeep,
                Edge = edge,
                BodyAlpha = WindFx.Style.PreviewBodyAlpha * (alive ? 1f : 0.7f),
                EdgeAlpha = WindFx.Style.PreviewEdgeAlpha,
                Doublet = WindFx.Layers.Distortion ? WindFx.Style.PreviewDoublet * WindFx.DoubletScale * (alive ? 1f : 0f) : 0f,
                Flow = flow,
                Front = 999f,
                FrontAlpha = 0f,
                Reveal = mode == Mode.Folding ? shown : 999f,
                Squeeze = gust.Clamped ? 0.25f * (1f - clampPulse) : 0f,
                Fade = fade
            };
            corridor.Set(start - dir * (0.5f * cell), angle, lane, cell, look);

            // THE FLOW: a few slow ribbons. Their line follows the drag a beat behind it, which is
            // the one place a little lag reads as air rather than as sluggishness.
            var want = new Vector2(gust.DirX, gust.DirY);
            ribbonDir = Vector2.Lerp(ribbonDir, want, 1f - Mathf.Exp(-dt / 0.07f));
            int count = alive && mode == Mode.Aiming && WindFx.Layers.Ribbons
                ? Mathf.Clamp(WindFx.RibbonBudget / 2, 2, 4) : 0;
            PaintRibbons(count, gust, cell, start, angle);

            // THE ORIGIN and THE NOSE
            PaintKnot(start, cell, now, 0.6f * fade, 0.72f);
            if (mode == Mode.Aiming)
            {
                PaintNose(end, dir, side, angle, cell, now, alive, edge);
            }
            else
            {
                sprites.ReturnAll(nose);
            }
            sprites.ReturnAll(wisps);
            sprites.Return(ref ring);

            // WHAT IT WILL TOUCH
            if (mode == Mode.Aiming)
            {
                stage.View = view;
                stage.Gust = gust;
                stage.Cell = cell;
                stage.Cube = view.CubeWorldSize * Mathf.Abs(view.transform.lossyScale.x);
                stage.Pixel = pixel;
                stage.Dir = dir;
                stage.Side = side;
                stage.Angle = angle;
                stage.Now = now;
                PaintTouched();
                debug.Clear();
                if (WindFx.DebugFlags.Any)
                {
                    debug.Geometry(stage, preview.Affected);
                }
            }
            else
            {
                ReleaseTouched(null);
            }
            PaintTip(end, cell);
        }

        /// <summary>The pressure knot: thin arcs turning slowly round a breathing dot.</summary>
        private void PaintKnot(Vector2 at, float cell, float now, float alpha, float scale)
        {
            int arcs = WindFx.Level == WindFx.Quality.Low ? 2 : 3;
            while (knot.Count < arcs)
            {
                knot.Add(sprites.RentGlow(WindShapes.Arc, WindOrders.Aim));
            }
            float size = cell * 0.62f * scale;
            for (int i = 0; i < knot.Count; i++)
            {
                float a = now * 22f + i * (360f / knot.Count);
                WindSprites.Place(knot[i], at, a, size, size, WindFx.Tint(WindFx.Style.Air, 0.32f * alpha));
            }
            if (dot == null)
            {
                dot = sprites.RentGlow(WindShapes.Dot, WindOrders.Aim);
            }
            float breath = 1f + 0.12f * Mathf.Sin(now * 2.6f);
            float d = cell * 0.13f * scale * breath;
            WindSprites.Place(dot, at, 0f, d, d, WindFx.Tint(WindFx.Style.AirLavender, 0.5f * alpha));
        }

        /// <summary>The origin being pinned: a ring closes in, then the knot sits with four short
        /// wisps of air about it.</summary>
        private void PaintLock(Vector2 at, float cell, float now)
        {
            float k = Mathf.Clamp01(modeClock / 0.16f);
            if (k < 1f)
            {
                if (ring == null)
                {
                    ring = sprites.RentGlow(WindShapes.Ring, WindOrders.Aim);
                }
                float size = cell * Mathf.Lerp(1.5f, 0.5f, WindFx.Smooth(k));
                WindSprites.Place(ring, at, 0f, size, size, WindFx.Tint(WindFx.Style.Air, 0.4f * Mathf.Sin(k * Mathf.PI)));
            }
            else
            {
                sprites.Return(ref ring);
            }
            PaintKnot(at, cell, now, 0.45f + 0.4f * k, Mathf.Lerp(1f, 0.72f, k));
            float settle = Mathf.Clamp01((modeClock - 0.1f) / 0.14f);
            while (wisps.Count < 4)
            {
                wisps.Add(sprites.RentGlow(WindShapes.WispRooted, WindOrders.Aim));
            }
            for (int i = 0; i < wisps.Count; i++)
            {
                // TANGENT to the knot, all turning the same way: air circling a point. Pointed
                // outward along the diagonals they were four spokes - a crosshair.
                float a = 45f + i * 90f + now * 14f;
                var d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                float len = cell * (0.24f + 0.05f * Mathf.Sin(now * 3.4f + i * 1.7f)) * settle;
                WindSprites.Place(wisps[i], at + d * (cell * 0.27f), a + 100f, len, cell * 0.1f,
                    WindFx.Tint(WindFx.Style.Air, 0.3f * settle));
            }
        }

        /// <summary>Two curved arcs closing on each other at the lane's end.</summary>
        private void PaintNose(Vector2 end, Vector2 dir, Vector2 side, float angle, float cell, float now,
            bool alive, Color colour)
        {
            while (nose.Count < 2)
            {
                nose.Add(sprites.RentGlow(WindShapes.Arc, WindOrders.Aim));
            }
            // at the length cap it is pressed for a moment; refused, it folds and stays folded
            float press = (1f - clampPulse) * 0.5f;
            float fold = alive ? 0f : 0.45f + 0.35f * (1f - refusedClock);
            float gap = cell * (0.1f + 0.035f * Mathf.Sin(now * 3.1f)) * (1f - press) * (1f - fold);
            float size = cell * 1.5f * (1f - 0.3f * fold) * (1f - 0.18f * press);
            float alpha = (alive ? 0.46f : 0.28f);
            // the arc sprite's stroke lies on a circle of this radius about its own centre, and
            // runs 54 degrees either side of its own +x
            float radius = size * 0.4f;
            const float Lead = 27f;  // how far round its circle the tip sits from the wind's line
            const float Reach = 44f; // from the stroke's middle to the end that makes the tip
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? 1f : -1f;
                // Each arc STARTS at the endpoint and sweeps back along its own side of the lane,
                // bulging outward: two curves closing on each other at one point - a nose. Set by
                // their crowns they crossed there, which is an X.
                float tip = (angle + Lead * s) * Mathf.Deg2Rad;
                var toTip = new Vector2(Mathf.Cos(tip), Mathf.Sin(tip));
                Vector2 centre = end + side * (gap * s) - toTip * radius;
                WindSprites.Place(nose[i], centre, angle + (Lead + Reach) * s, size, size, WindFx.Tint(colour, alpha));
            }
        }

        private void PaintRibbons(int count, WindGust gust, float cell, Vector2 start, float angle)
        {
            while (ribbons.Count < count)
            {
                ribbons.Add(WindRibbon.Make(transform, WindOrders.Ribbon));
            }
            float lane = gust.Length + 1f;
            // the ribbons run on the smoothed line; the lane itself is always the true one
            Vector2 a = view.BoardPointToWorld(gust.StartX, gust.StartY);
            Vector2 b = view.BoardPointToWorld(gust.StartX + ribbonDir.x, gust.StartY + ribbonDir.y);
            Vector2 dir = (b - a).sqrMagnitude > 1e-8f ? (b - a).normalized : Vector2.right;
            var side = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < count; i++)
            {
                float share = Mathf.Lerp(WindFx.Style.RibbonMinShare, WindFx.Style.RibbonMaxShare,
                    WindFx.Hash(i, 51));
                float length = Mathf.Max(0.7f, lane * share);
                float phase = Mathf.Repeat(flow / lane * 0.9f + i * 0.381f, 1f);
                float head = -0.5f + phase * (lane + length);
                float tail = head - length;
                head = Mathf.Min(head, lane - 0.5f);
                tail = Mathf.Max(tail, -0.5f);
                if (head - tail < 0.2f)
                {
                    ribbons[i].Hide();
                    continue;
                }
                float across = (WindFx.Hash(i, 53) * 2f - 1f) * 1.05f;
                float fade = Mathf.Sin(phase * Mathf.PI);
                int seed = i * 31;
                float t = Time.unscaledTime * 0.35f;
                Vector2 p0 = start + dir * (tail * cell) + side * (across * cell);
                Vector2 p1 = start + dir * (head * cell) + side * (across * cell);
                float tailAlong = tail;
                float span = head - tail;
                ribbons[i].Draw(p0, p1,
                    k => WindFx.RibbonWander(tailAlong + span * k, t, seed) * cell,
                    cell * WindFx.Style.RibbonWidth, WindFx.Tint(WindFx.Style.Air, WindFx.Style.PreviewRibbonAlpha * fade),
                    WindFx.Hash(i, 57));
            }
            HideRibbons(count);
        }

        private void HideRibbons(int from)
        {
            for (int i = from; i < ribbons.Count; i++)
            {
                ribbons[i].Hide();
            }
        }

        /// <summary>Everything the rules say the gust will affect, each in its own material's way.</summary>
        private void PaintTouched()
        {
            seen.Clear();
            foreach (WindAffected affected in preview.Affected)
            {
                bool fire = affected.Cube.HasValue && affected.Cube.Value.Kind == CubeKind.Fire;
                bool water = affected.Cube.HasValue && affected.Cube.Value.Kind == CubeKind.Water;
                bool spread = affected.Reaction == WindReactionKind.DuplicateSpread;
                if (!fire && !water && !spread)
                {
                    continue;
                }
                long key = ((long)(affected.Cell.X + 512) << 24) | ((long)(affected.Cell.Y + 512) << 8)
                    | (long)((int)affected.Reaction << 2) | (spread ? 3L : fire ? 1L : 2L);
                seen.Add(key);
                WindReaction visual;
                if (!touched.TryGetValue(key, out visual))
                {
                    visual = spread ? (WindReaction)new WindInfectionReaction(affected.Cell)
                        : fire ? (WindReaction)new WindFireReaction(affected.Cell, affected.Reaction)
                        : new WindWaterReaction(affected.Cell, affected.Reaction);
                    touched[key] = visual;
                }
                visual.OnWindPreview(stage);
            }
            ReleaseTouched(seen);
        }

        /// <summary>Hands back every preview not in <paramref name="keep"/> (all of them for null).</summary>
        private void ReleaseTouched(HashSet<long> keep)
        {
            if (touched.Count == 0)
            {
                return;
            }
            stale.Clear();
            foreach (KeyValuePair<long, WindReaction> entry in touched)
            {
                if (keep == null || !keep.Contains(entry.Key))
                {
                    entry.Value.Release(stage);
                    stale.Add(entry.Key);
                }
            }
            for (int i = 0; i < stale.Count; i++)
            {
                touched.Remove(stale[i]);
            }
        }

        /// <summary>The small word by the endpoint when a stroke was let go and refused.</summary>
        private void PaintTip(Vector2 end, float cell)
        {
            bool show = refusedClock < 1f && !string.IsNullOrEmpty(refusedText) && mode == Mode.Aiming;
            if (!show)
            {
                if (tip != null && tip.gameObject.activeSelf)
                {
                    tip.gameObject.SetActive(false);
                }
                return;
            }
            if (tip == null)
            {
                tip = ViewUtil.MakeText3D(transform, "WindTip", Vector2.zero, string.Empty, 90, 0.1f,
                    Color.white, WindOrders.Debug, TextAnchor.MiddleCenter);
                tip.anchor = TextAnchor.MiddleCenter;
            }
            tip.gameObject.SetActive(true);
            tip.text = refusedText;
            tip.transform.position = new Vector3(end.x, end.y + cell * 0.62f, 0f);
            float size = cell * 0.2f;
            tip.transform.localScale = new Vector3(size, size, 1f);
            float a = Mathf.Clamp01(refusedClock * 8f) * Mathf.Clamp01((1f - refusedClock) * 4f);
            ViewUtil.SetTextColor(tip, new Color(0.95f, 0.84f, 0.87f, a));
        }
    }
}
