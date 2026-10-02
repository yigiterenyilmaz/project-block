// PURPOSE: "Rüzgar" while it is being AIMED: the lane the gust will take, drawn the moment the
// player presses on the board and following the drag - a soft band three cells wide with air
// streaming down it, a ring on the cell it starts from, an arrowhead where it ends, and a ring on
// every cube the wind will actually move (fire orange, water blue with its own arrow, an infection
// green). A gust that would touch nothing goes grey, so the player sees it is empty air before
// letting go.
//
// THE VIEW DECIDES NOTHING: the band is WindGust's (the rules' own cells, snapped start and length
// cap), and which cubes are marked is the controller's question to the power - never a radius
// worked out here.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class WindAimView : MonoBehaviour
    {
        public enum MarkKind
        {
            Fire,
            Water,
            Infection
        }

        public struct Mark
        {
            public GridPos Cell;
            public MarkKind Kind;
        }

        public static class Style
        {
            public static readonly Color Air = new Color(0.80f, 0.95f, 1f);
            public static readonly Color Empty = new Color(0.62f, 0.64f, 0.68f);
            public static readonly Color FireMark = new Color(1f, 0.62f, 0.2f);
            public static readonly Color WaterMark = new Color(0.42f, 0.84f, 1f);
            public static readonly Color InfectionMark = new Color(0.52f, 1f, 0.46f);

            /// <summary>The lane's own light. Low: it is a lane, not a selection.</summary>
            public static float BandAlpha = 0.17f;
            public static float EmptyBandAlpha = 0.09f;

            public static int Streaks = 10;

            /// <summary>How fast the air runs down the lane while aiming, in cells a second.</summary>
            public static float StreakSpeed = 3.4f;
            public static float StreakLength = 1.15f;
            public static float StreakThickness = 0.16f;
            public static float StreakAlpha = 0.55f;

            public static float MarkSize = 0.9f;
        }

        private const int BandOrder = 4;
        private const int StreakOrder = 9;
        private const int MarkOrder = 10;

        // Where each streak runs across the lane, in cells - a fixed weave, never a grid.
        private static readonly float[] Lanes = { -0.95f, 0.35f, -0.4f, 0.9f, 0.05f, -0.7f, 0.6f, -0.15f, 1.05f, -1.1f };

        private BoardView view;
        private WindGust gust;
        private GridPos? startOnly;
        private bool runnable;
        private readonly List<Mark> marks = new List<Mark>();

        private SpriteRenderer band;
        private SpriteRenderer start;
        private SpriteRenderer end;
        private readonly List<SpriteRenderer> streaks = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> rings = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> arrows = new List<SpriteRenderer>();

        /// <summary>The lane for <paramref name="aim"/>, and the cubes it will move.</summary>
        public void Show(BoardView on, WindGust aim, bool canRun, IList<Mark> moving)
        {
            view = on;
            gust = aim;
            startOnly = null;
            runnable = canRun;
            marks.Clear();
            if (moving != null)
            {
                marks.AddRange(moving);
            }
            enabled = true;
        }

        /// <summary>Only the cell the gust will start from (a tap, waiting for where it goes).</summary>
        public void ShowStart(BoardView on, GridPos cell)
        {
            view = on;
            gust = null;
            startOnly = cell;
            marks.Clear();
            enabled = true;
        }

        public void Hide()
        {
            gust = null;
            startOnly = null;
            marks.Clear();
            SetActive(band, false);
            SetActive(start, false);
            SetActive(end, false);
            HideAll(streaks, 0);
            HideAll(rings, 0);
            HideAll(arrows, 0);
            enabled = false;
        }

        private void LateUpdate()
        {
            if (view == null || view.Board == null)
            {
                Hide();
                return;
            }
            float cell = view.CellSizeInWorld;
            float t = Time.unscaledTime;
            if (gust == null || !gust.Valid)
            {
                SetActive(band, false);
                SetActive(end, false);
                HideAll(streaks, 0);
                HideAll(rings, 0);
                HideAll(arrows, 0);
                if (startOnly.HasValue || gust != null)
                {
                    GridPos at = startOnly ?? new GridPos((int)gust.StartX, (int)gust.StartY);
                    start = Ensure(start, WindShapes.Ring, MarkOrder, false);
                    Place(start, view.BoardPointToWorld(at.X, at.Y), 0f, cell * Style.MarkSize,
                        cell * Style.MarkSize, Tint(Style.Air, 0.55f + 0.2f * Mathf.Sin(t * 5f)));
                }
                else
                {
                    SetActive(start, false);
                }
                return;
            }

            Vector2 from = view.BoardPointToWorld(gust.StartX, gust.StartY);
            Vector2 to = view.BoardPointToWorld(gust.EndX, gust.EndY);
            Vector2 dir = (to - from).sqrMagnitude > 1e-6f ? (to - from).normalized : Vector2.right;
            Vector2 side = new Vector2(-dir.y, dir.x);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float lane = gust.Length + 1f;
            Color air = runnable ? Style.Air : Style.Empty;

            // the lane itself, from half a cell behind the start to half a cell past the end
            band = Ensure(band, WindShapes.Band, BandOrder, false);
            Place(band, from + dir * (cell * (gust.Length * 0.5f)), angle, cell * lane,
                cell * WindGust.Width, Tint(air, runnable ? Style.BandAlpha : Style.EmptyBandAlpha));

            // the air running down it
            int n = runnable ? Style.Streaks : Style.Streaks / 2;
            for (int i = 0; i < n; i++)
            {
                SpriteRenderer s = Rent(streaks, i, WindShapes.Streak, StreakOrder, true);
                float phase = Fract(t * Style.StreakSpeed / lane + i * 0.618f);
                float along = -0.5f + phase * lane;
                float across = Lanes[i % Lanes.Length];
                Vector2 at = from + dir * (cell * along) + side * (cell * across);
                float fade = Mathf.Sin(phase * Mathf.PI);
                Place(s, at, angle, cell * Style.StreakLength, cell * Style.StreakThickness,
                    Tint(air, Style.StreakAlpha * fade * (runnable ? 1f : 0.5f)));
            }
            HideAll(streaks, n);

            start = Ensure(start, WindShapes.Ring, MarkOrder, false);
            Place(start, from, 0f, cell * Style.MarkSize, cell * Style.MarkSize, Tint(air, 0.7f));
            end = Ensure(end, WindShapes.Chevron, MarkOrder, false);
            Place(end, to, angle, cell * 0.62f, cell * 0.62f, Tint(air, runnable ? 0.85f : 0.45f));

            // what the wind will move
            int arrowCount = 0;
            for (int i = 0; i < marks.Count; i++)
            {
                Mark m = marks[i];
                Color c = m.Kind == MarkKind.Fire ? Style.FireMark
                    : m.Kind == MarkKind.Water ? Style.WaterMark : Style.InfectionMark;
                Vector2 at = view.BoardPointToWorld(m.Cell.X, m.Cell.Y);
                SpriteRenderer r = Rent(rings, i, WindShapes.Ring, MarkOrder, false);
                Place(r, at, 0f, cell * Style.MarkSize, cell * Style.MarkSize,
                    Tint(c, 0.6f + 0.22f * Mathf.Sin(t * 4.5f + i * 0.9f)));
                if (m.Kind == MarkKind.Water)
                {
                    SpriteRenderer a = Rent(arrows, arrowCount++, WindShapes.Chevron, MarkOrder, false);
                    float nudge = 0.22f + 0.08f * Mathf.Sin(t * 6f + i);
                    Place(a, at + dir * (cell * nudge), angle, cell * 0.34f, cell * 0.34f, Tint(c, 0.85f));
                }
            }
            HideAll(rings, marks.Count);
            HideAll(arrows, arrowCount);
        }

        // ------------------------------------------------------------------ helpers

        private static float Fract(float x)
        {
            return x - Mathf.Floor(x);
        }

        private static Color Tint(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
        }

        private SpriteRenderer Ensure(SpriteRenderer r, Sprite sprite, int order, bool glow)
        {
            if (r == null)
            {
                r = Make(sprite, order, glow);
            }
            r.enabled = true;
            return r;
        }

        private SpriteRenderer Rent(List<SpriteRenderer> pool, int index, Sprite sprite, int order, bool glow)
        {
            while (pool.Count <= index)
            {
                pool.Add(Make(sprite, order, glow));
            }
            pool[index].enabled = true;
            return pool[index];
        }

        private SpriteRenderer Make(Sprite sprite, int order, bool glow)
        {
            var go = new GameObject("WindAim");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            if (glow && WindFx.Glow != null)
            {
                r.sharedMaterial = WindFx.Glow;
            }
            return r;
        }

        private static void Place(SpriteRenderer r, Vector2 at, float angle, float width, float height, Color colour)
        {
            r.transform.position = new Vector3(at.x, at.y, 0f);
            r.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            Vector2 unit = r.sprite.bounds.size;
            r.transform.localScale = new Vector3(width / Mathf.Max(unit.x, 0.0001f),
                height / Mathf.Max(unit.y, 0.0001f), 1f);
            r.color = colour;
        }

        private static void SetActive(SpriteRenderer r, bool on)
        {
            if (r != null)
            {
                r.enabled = on;
            }
        }

        private static void HideAll(List<SpriteRenderer> pool, int from)
        {
            for (int i = from; i < pool.Count; i++)
            {
                pool[i].enabled = false;
            }
        }
    }

    /// <summary>The one material the wind's light is drawn with: the glow Simetri already uses
    /// (premultiplied, mostly additive), so streaks and embers brighten what is under them rather
    /// than painting over it. Null without the shader - the sprites' own material is used then.</summary>
    internal static class WindFx
    {
        private static Material glow;
        private static bool looked;

        public static Material Glow
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    Shader shader = Shader.Find("ProjectBlock/SymmetryGlow");
                    if (shader == null)
                    {
                        shader = Resources.Load<Shader>("Shaders/SymmetryGlow");
                    }
                    if (shader != null && shader.isSupported)
                    {
                        glow = new Material(shader) { name = "WindGlow (shared)" };
                    }
                }
                return glow;
            }
        }
    }
}
