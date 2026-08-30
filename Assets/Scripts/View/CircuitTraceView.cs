// PURPOSE: "Devre" - the circuit, drawn as ONE CABLE laid across the board: a dark soft-3D body
// with two energy veins running side by side inside it and small windows in the body where that
// energy shows through.
//
// THE ROUTE IS SETTLED. Path cells become a polyline, corners become real arcs, and the whole
// thing is extruded into strips. There is no seam at a cell boundary because there is no
// boundary. Do not take that apart - everything below is about the MATERIAL.
//
// THE ROUTE IS RESAMPLED, and that is what makes the material possible at all. Straight from the
// arcs, a long straight run is TWO points - so nothing can vary along it: no window in the middle
// of a run, no width noise, no pulse resolution. Resampling to a fixed spacing gives every strip
// enough vertices to carry a per-vertex effect anywhere along the cable.
//
// IT IS FIVE STRIPS OVER ONE CENTRE LINE:
//   - CONTACT SHADOW, wider and offset a hair down-right, which puts the cable ON the board;
//   - BODY, dark and nearly neutral, carrying the volume - a tube shade baked across v times a
//     per-vertex directional term, so it is lit from above and stays lit through a corner;
//   - VEIN A and VEIN B, thin, offset either side of the middle, the only coloured things here;
//   - WINDOWS, energy showing through gaps in the body at intervals.
// Splitting them is the whole design: a pulse can run down the veins and light the windows it
// passes without touching the body, so the cable stays calm and only the energy moves.
//
// THE TWO VEINS ARE NOT COPIES. B trails A through a pulse by a few tens of milliseconds, and at
// rest they breathe out of phase. One light sliding along a line reads as a sprite being moved;
// two currents slightly out of step read as something flowing through a cable.
//
// NOT IN HERE, DELIBERATELY: completing the circuit. No overload, no rupture, no payoff.

using System.Collections.Generic;
using UnityEngine;
using ProjectBlock.Core;

namespace ProjectBlock.View
{
    /// <summary>The circuit, as one continuous power cable. Fed by BoardView.ShowCircuit.</summary>
    public sealed class CircuitTraceView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            // ---- the body ----
            /// <summary>Body width in CELLS before the soft edge. Wide enough to hold two veins
            /// and a window and still be a thin cable on the board.</summary>
            public static float CableBodyWidth = 0.115f;

            public static float CableEdgeSpread = 1.22f;

            public static float CableBodyHighlight = 0.30f;

            public static float CableBodyShadow = 0.22f;

            /// <summary>How much round-surface shade is baked across the body, independently of
            /// where the light is. This is the volume; the two above are only the direction.</summary>
            public static float TubeShading = 0.36f;

            /// <summary>Very faint banding along the body so it is not a sterile tube. It must
            /// never look like the cable is made of segments joined together.</summary>
            public static float BodyBandingStrength = 0.05f;

            public static float BodyBandingSpacing = 0.85f;

            public static float GlowStrength = 0.10f;

            // ---- contact shadow ----
            public static float ContactShadowStrength = 0.34f;

            public static float ContactShadowSpread = 1.5f;

            public static Vector2 ContactShadowOffset = new Vector2(0.012f, -0.020f);

            // ---- the two veins ----
            public static float EnergyVeinWidth = 0.022f;

            /// <summary>How far apart the two veins run, centre to centre, in CELLS.</summary>
            public static float EnergyVeinSpacing = 0.040f;

            public static float EnergyVeinIdleIntensity = 0.62f;

            public static float EnergyVeinPulseIntensity = 1.5f;

            /// <summary>How much the two drift apart in brightness at rest, and how fast. Small:
            /// the cable is live, it is not blinking.</summary>
            public static float VeinIdleVariation = 0.14f;

            public static float VeinIdleSpeed = 0.42f;

            // ---- energy windows ----
            /// <summary>Length of one window and the gap between them, in CELLS. Spaced along the
            /// cable rather than per cell - a window in every cell is a marker again.</summary>
            public static float EnergyWindowSize = 0.28f;

            public static float EnergyWindowSpacing = 1.15f;

            public static float EnergyWindowIdleIntensity = 0.30f;

            public static float EnergyWindowPulseIntensity = 1.25f;

            /// <summary>Windows are suppressed near a bend, where the body is already doing
            /// something and an opening would read as a fitting.</summary>
            public static float WindowCornerSuppress = 0.55f;

            // ---- the pulse ----
            public static float PulseSeconds = 0.95f;

            public static float PulseLength = 0.5f;

            public static float PulseTrailLength = 1.8f;

            /// <summary>How far behind vein A the second vein's pulse runs, in seconds. The whole
            /// reason the cable reads as carrying a current rather than a moving dot.</summary>
            public static float PulseSecondVeinDelay = 0.065f;

            /// <summary>How much the body picks up as the pulse goes by - a rim, not a flash.</summary>
            public static float PulseBodyRim = 0.18f;

            public static float PulseGapMin = 2.5f;

            public static float PulseGapMax = 4.5f;

            // ---- shape ----
            public static float CornerRadius = 0.26f;

            /// <summary>How much wider the outside of a bend runs, and how much the veins spread
            /// apart through it. Both tiny - felt, not seen.</summary>
            public static float CornerOuterExpansion = 0.10f;

            public static float CornerVeinSpread = 0.22f;

            public static int CornerSteps = 8;

            /// <summary>How far apart the resampled points sit, in CELLS. This is what every
            /// per-vertex effect below is limited by.</summary>
            public static float SampleSpacing = 0.12f;

            /// <summary>A whisper of curvature on long straights, in CELLS, so they are not ruled
            /// lines. Must stay far below half a cell or the cable leaves its own path.</summary>
            public static float StraightWaver = 0.012f;

            public static float StraightWaverScale = 0.55f;

            public static float WidthNoise = 0.03f;

            public static float EdgeReach = 0.5f;

            // ---- laying it ----
            public static float DrawSecondsPerCell = 0.05f;

            public static float DrawMinSeconds = 0.4f;

            /// <summary>How far behind the tip the body has formed but the energy has not lit
            /// yet, in cells. The body is laid first and the current follows it.</summary>
            public static float CreationEnergyDelay = 0.9f;

            public static float CreationSettle = 1.3f;

            public static float LeadingTipSize = 0.15f;

            public static float LeadingTipIntensity = 0.85f;

            public static float ConfirmSeconds = 0.30f;

            public static float ConfirmStrength = 0.45f;

            // ---- palette ----
            /// <summary>The body is DARK and nearly neutral; only the veins and windows are
            /// coloured. When the whole cable was cyan it read as a debug spline.</summary>
            public static Color BodyColor = new Color(0.150f, 0.195f, 0.225f);

            public static Color BodyLight = new Color(0.31f, 0.38f, 0.43f);

            public static Color EnergyColor = new Color(0.55f, 0.88f, 0.95f);

            public static Color WindowColor = new Color(0.38f, 0.76f, 0.86f);

            public static Color GlowColor = new Color(0.18f, 0.40f, 0.48f);

            public static Color ShadowColor = new Color(0.02f, 0.03f, 0.05f);
        }

        /// <summary>The cable lies ON the board: over the surface and its grid, so a grid line
        /// never cuts it, and under the cubes, so a block on a path cell covers it.</summary>
        private const int ShadowOrder = 4;

        private const int BodyOrder = 5;

        private const int WindowOrder = 6;

        private const int VeinOrder = 7;

        private const int TipOrder = 8;

        // =================================================================== shared art

        private static Texture2D bodyTex;
        private static Texture2D veinTex;
        private static Texture2D windowTex;
        private static Texture2D shadowTex;
        private static Material bodyMat;
        private static Material veinMat;
        private static Material windowMat;
        private static Material shadowMat;
        private static Sprite tipSprite;

        private delegate float CrossSection(float v, out Color colour);

        /// <summary>The body end-on: a tube shade with a soft edge. The brightening sits a third
        /// of the way out rather than in the middle, which is what a round surface does.</summary>
        private static Texture2D BodyTex()
        {
            if (bodyTex != null)
            {
                return bodyTex;
            }
            bodyTex = Bake(64, delegate (float v, out Color c)
            {
                float core = 1f / Style.CableEdgeSpread;
                if (v <= core)
                {
                    float k = v / core;
                    float lit = Mathf.Exp(-4.5f * (k - 0.34f) * (k - 0.34f));
                    c = Color.Lerp(Style.BodyColor, Style.BodyLight, lit * Style.TubeShading);
                    return 1f;
                }
                float g = Mathf.InverseLerp(core, 1f, v);
                c = Color.Lerp(Style.BodyColor, Style.GlowColor, g);
                return (1f - g) * (1f - g) + Style.GlowStrength * g * (1f - g) * 3f;
            });
            return bodyTex;
        }

        private static Texture2D VeinTex()
        {
            if (veinTex != null)
            {
                return veinTex;
            }
            veinTex = Bake(32, delegate (float v, out Color c)
            {
                c = Style.EnergyColor;
                return Mathf.Exp(-3.2f * v * v) - 0.04f;
            });
            return veinTex;
        }

        /// <summary>A window: an opening in the body, so it is softer at its edges than a vein and
        /// a little wider - the energy is being seen THROUGH something.</summary>
        private static Texture2D WindowTex()
        {
            if (windowTex != null)
            {
                return windowTex;
            }
            windowTex = Bake(32, delegate (float v, out Color c)
            {
                c = Style.WindowColor;
                return (Mathf.Exp(-2.2f * v * v) - 0.11f) * 1.12f;
            });
            return windowTex;
        }

        private static Texture2D ShadowTex()
        {
            if (shadowTex != null)
            {
                return shadowTex;
            }
            shadowTex = Bake(32, delegate (float v, out Color c)
            {
                c = Style.ShadowColor;
                return Mathf.Exp(-2.6f * v * v) - 0.07f;
            });
            return shadowTex;
        }

        private static Texture2D Bake(int h, CrossSection fn)
        {
            const int w = 4;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                Color c;
                float a = Mathf.Clamp01(fn(v, out c));
                var col = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f),
                    (byte)Mathf.RoundToInt(a * 255f));
                for (int x = 0; x < w; x++)
                {
                    px[y * w + x] = col;
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        private static Material Mat(ref Material slot, Texture2D tex)
        {
            if (slot == null)
            {
                slot = new Material(Shader.Find("Sprites/Default"));
                slot.hideFlags = HideFlags.HideAndDontSave;
                slot.mainTexture = tex;
            }
            return slot;
        }

        private static Sprite TipSprite()
        {
            if (tipSprite != null)
            {
                return tipSprite;
            }
            const int n = 32;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float a = Mathf.Exp(-4.5f * (u * u + v * v));
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            tipSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return tipSprite;
        }

        // =================================================================== strips

        private enum Layer { Shadow, Body, Window, VeinA, VeinB }

        private sealed class Strip
        {
            public Layer Kind;
            public Mesh Mesh;
            public Vector3[] Verts;
            public Vector2[] Uvs;
            public Color[] Cols;
            public int[] Tris;
            public float Half;
            public float Lateral;     // offset across the cable, world units
            public Vector2 Offset;
        }

        private readonly List<Strip> strips = new List<Strip>();

        // =================================================================== state

        private readonly List<Vector2> route = new List<Vector2>();
        private readonly List<float> routeAt = new List<float>();
        private readonly List<float> routeBend = new List<float>();
        private float routeLength;

        private SpriteRenderer tip;
        private float cellSize = 1f;

        /// <summary>How far down the cable the overload has burned, or -1 while it is intact.
        /// The cable goes out BEHIND the failure rather than all at once, so the two read as one
        /// event: the tiles burst and the cable they were on stops existing.</summary>
        private float burnAt = -1f;

        private float burnSeconds;
        private float clock;
        private float drawSeconds;
        private bool live;
        private bool bodySettled;
        private float nextPulseAt;
        private float pulseStartedAt = -1f;

        // =================================================================== driving it

        /// <summary>
        /// Lays the cable. <paramref name="cells"/> is the path IN ORDER - the cells are the
        /// cable's ROUTE and nothing whatever is drawn for them individually.
        /// </summary>
        public void Show(IReadOnlyList<GridPos> cells, GameBoard board,
            System.Func<GridPos, Vector2> toWorld, float cell)
        {
            Clear();
            cellSize = cell;
            if (cells == null || board == null || toWorld == null || cells.Count == 0)
            {
                return;
            }

            var way = new List<Vector2>(cells.Count + 2);
            Vector2 first = toWorld(cells[0]);
            Vector2 second = cells.Count > 1 ? toWorld(cells[1]) : first + Vector2.up * cell;
            way.Add(first - (second - first).normalized * cell * Style.EdgeReach);
            for (int i = 0; i < cells.Count; i++)
            {
                way.Add(toWorld(cells[i]));
            }
            Vector2 last = toWorld(cells[cells.Count - 1]);
            Vector2 before = cells.Count > 1
                ? toWorld(cells[cells.Count - 2]) : last - Vector2.up * cell;
            way.Add(last + (last - before).normalized * cell * Style.EdgeReach);

            BuildRoute(way);
            Resample();
            if (route.Count < 3)
            {
                return;
            }

            float body = cellSize * Style.CableBodyWidth;
            float vein = cellSize * Style.EnergyVeinWidth;
            float spacing = cellSize * Style.EnergyVeinSpacing * 0.5f;
            AddStrip(Layer.Shadow, Mat(ref shadowMat, ShadowTex()), ShadowOrder,
                body * Style.ContactShadowSpread * 0.5f, 0f,
                Style.ContactShadowOffset * cellSize);
            AddStrip(Layer.Body, Mat(ref bodyMat, BodyTex()), BodyOrder,
                body * Style.CableEdgeSpread * 0.5f, 0f, Vector2.zero);
            AddStrip(Layer.Window, Mat(ref windowMat, WindowTex()), WindowOrder,
                body * 0.34f, 0f, Vector2.zero);
            AddStrip(Layer.VeinA, Mat(ref veinMat, VeinTex()), VeinOrder,
                vein * 0.5f, spacing, Vector2.zero);
            AddStrip(Layer.VeinB, Mat(ref veinMat, VeinTex()), VeinOrder,
                vein * 0.5f, -spacing, Vector2.zero);

            var tipGo = new GameObject("Tip");
            tipGo.transform.SetParent(transform, false);
            tip = tipGo.AddComponent<SpriteRenderer>();
            tip.sprite = TipSprite();
            tip.sortingOrder = TipOrder;
            tip.color = new Color(1f, 1f, 1f, 0f);

            drawSeconds = Mathf.Max(Style.DrawMinSeconds, cells.Count * Style.DrawSecondsPerCell);
            clock = 0f;
            live = true;
            bodySettled = false;
            nextPulseAt = drawSeconds + Style.ConfirmSeconds + Style.PulseGapMin;
            Redraw(0f, true);
        }

        private void AddStrip(Layer kind, Material material, int order, float half,
            float lateral, Vector2 offset)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(transform, false);
            var f = go.AddComponent<MeshFilter>();
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.sortingOrder = order;
            int n = route.Count;
            var s = new Strip
            {
                Kind = kind,
                Mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = kind.ToString() },
                Verts = new Vector3[n * 2],
                Uvs = new Vector2[n * 2],
                Cols = new Color[n * 2],
                Tris = new int[(n - 1) * 6],
                Half = half,
                Lateral = lateral,
                Offset = offset
            };
            for (int i = 0; i < n - 1; i++)
            {
                int t = i * 6;
                int a = i * 2;
                s.Tris[t] = a; s.Tris[t + 1] = a + 2; s.Tris[t + 2] = a + 1;
                s.Tris[t + 3] = a + 1; s.Tris[t + 4] = a + 2; s.Tris[t + 5] = a + 3;
            }
            f.sharedMesh = s.Mesh;
            strips.Add(s);
        }

        /// <summary>Straight runs joined by real ARCS.</summary>
        private void BuildRoute(List<Vector2> way)
        {
            route.Clear();
            routeBend.Clear();
            float radius = Style.CornerRadius * cellSize;
            route.Add(way[0]);
            routeBend.Add(0f);
            for (int i = 1; i < way.Count - 1; i++)
            {
                Vector2 prev = way[i - 1];
                Vector2 here = way[i];
                Vector2 next = way[i + 1];
                Vector2 inDir = (here - prev).normalized;
                Vector2 outDir = (next - here).normalized;
                if (Vector2.Dot(inDir, outDir) > 0.999f)
                {
                    continue;
                }
                float bend = inDir.x * outDir.y - inDir.y * outDir.x;
                float r = Mathf.Min(radius,
                    (here - prev).magnitude * 0.5f, (next - here).magnitude * 0.5f);
                Vector2 a = here - inDir * r;
                Vector2 b = here + outDir * r;
                route.Add(a);
                routeBend.Add(0f);
                for (int st = 1; st < Style.CornerSteps; st++)
                {
                    float t = (float)st / Style.CornerSteps;
                    Vector2 p = Vector2.Lerp(Vector2.Lerp(a, here, t), Vector2.Lerp(here, b, t), t);
                    route.Add(p);
                    routeBend.Add(bend * Mathf.Sin(t * Mathf.PI));
                }
                route.Add(b);
                routeBend.Add(0f);
            }
            route.Add(way[way.Count - 1]);
            routeBend.Add(0f);
        }

        /// <summary>Walks the polyline at a fixed spacing. Without this a straight run is two
        /// points, and then nothing can vary along it - no window, no noise, no pulse shape.</summary>
        private void Resample()
        {
            if (route.Count < 2)
            {
                return;
            }
            var src = new List<Vector2>(route);
            var srcBend = new List<float>(routeBend);
            var at = new List<float>(src.Count) { 0f };
            float total = 0f;
            for (int i = 1; i < src.Count; i++)
            {
                total += (src[i] - src[i - 1]).magnitude;
                at.Add(total);
            }

            route.Clear();
            routeBend.Clear();
            routeAt.Clear();
            float step = Mathf.Max(Style.SampleSpacing * cellSize, 0.001f);
            int count = Mathf.Max(2, Mathf.CeilToInt(total / step));
            int seg = 1;
            for (int k = 0; k <= count; k++)
            {
                float d = total * k / count;
                while (seg < at.Count - 1 && at[seg] < d)
                {
                    seg++;
                }
                float span = at[seg] - at[seg - 1];
                float t = span > 0.0001f ? (d - at[seg - 1]) / span : 0f;
                Vector2 p = Vector2.Lerp(src[seg - 1], src[seg], t);
                float bend = Mathf.Lerp(srcBend[seg - 1], srcBend[seg], t);

                // A whisper of curvature so a long straight is not a ruled line. Suppressed
                // through a bend, where the cable is already doing something.
                if (Mathf.Abs(bend) < 0.05f)
                {
                    Vector2 dir = (src[seg] - src[seg - 1]).normalized;
                    var nrm = new Vector2(-dir.y, dir.x);
                    p += nrm * Mathf.Sin(d / cellSize * Style.StraightWaverScale)
                        * Style.StraightWaver * cellSize;
                }
                route.Add(p);
                routeBend.Add(bend);
            }

            routeLength = 0f;
            routeAt.Add(0f);
            for (int i = 1; i < route.Count; i++)
            {
                routeLength += (route[i] - route[i - 1]).magnitude;
                routeAt.Add(routeLength);
            }
        }

        /// <summary>The cable's own centre line, for whatever draws along it. Read-only by
        /// contract: the overload places its tiles on exactly these points.</summary>
        public IReadOnlyList<Vector2> Route
        {
            get { return route; }
        }

        public IReadOnlyList<float> RouteAt
        {
            get { return routeAt; }
        }

        public float RouteLength
        {
            get { return routeLength; }
        }

        public float CellSize
        {
            get { return cellSize; }
        }

        /// <summary>Starts the cable burning away, front first, over <paramref name="seconds"/>.
        /// The overload itself is CircuitOverloadView's business - this only takes the cable out
        /// from under it.</summary>
        public void BurnAway(float seconds)
        {
            burnSeconds = Mathf.Max(seconds, 0.05f);
            burnAt = 0f;
            bodySettled = false;
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            for (int i = 0; i < strips.Count; i++)
            {
                if (strips[i].Mesh != null)
                {
                    Destroy(strips[i].Mesh);
                }
            }
            strips.Clear();
            route.Clear();
            routeAt.Clear();
            routeBend.Clear();
            tip = null;
            live = false;
            bodySettled = false;
            burnAt = -1f;
            routeLength = 0f;
            pulseStartedAt = -1f;
        }

        // =================================================================== the clock

        private void Update()
        {
            if (!live || strips.Count == 0)
            {
                return;
            }
            clock += Time.deltaTime;
            if (burnAt >= 0f)
            {
                burnAt += routeLength * Time.deltaTime / burnSeconds;
                bodySettled = false;   // the body is changing again, so it must be redrawn
            }
            Redraw(clock, false);
        }

        private void Redraw(float now, bool force)
        {
            float drawn = Mathf.Clamp01(now / Mathf.Max(drawSeconds, 0.0001f));
            float cut = drawn * routeLength;
            bool laid = drawn >= 1f;

            float confirm = 0f;
            if (laid)
            {
                float since = now - drawSeconds;
                if (since < Style.ConfirmSeconds)
                {
                    confirm = Mathf.Sin(since / Style.ConfirmSeconds * Mathf.PI)
                        * Style.ConfirmStrength;
                }
            }
            float pulseA = laid ? Pulse(now, 0f) : -1f;
            float pulseB = laid ? Pulse(now, Style.PulseSecondVeinDelay) : -1f;

            // The body and its shadow stop changing once the cable is down and the confirm pass
            // is over; only the energy keeps moving. That is most of the per-frame cost gone.
            bool staticDone = laid && confirm <= 0f;
            for (int i = 0; i < strips.Count; i++)
            {
                Strip s = strips[i];
                bool isStatic = s.Kind == Layer.Shadow || s.Kind == Layer.Body;
                if (isStatic && bodySettled && !force)
                {
                    continue;
                }
                Extrude(s, cut, confirm, s.Kind == Layer.VeinB ? pulseB : pulseA, now);
            }
            if (staticDone)
            {
                bodySettled = true;
            }
            DrawTip(cut, laid);
        }

        /// <summary>Where a vein's current is, in distance along the cable, or -1 for none.
        /// <paramref name="delay"/> is what makes vein B trail vein A.</summary>
        private float Pulse(float now, float delay)
        {
            if (pulseStartedAt < 0f)
            {
                if (now < nextPulseAt)
                {
                    return -1f;
                }
                pulseStartedAt = now;
            }
            float t = (now - pulseStartedAt - delay) / Mathf.Max(Style.PulseSeconds, 0.0001f);
            if (t >= 1f && delay > 0f)
            {
                return -1f;
            }
            if (t >= 1f)
            {
                // Only the leading vein retires the pulse, so B is never cut off mid-run.
                if (now - pulseStartedAt > Style.PulseSeconds + Style.PulseSecondVeinDelay)
                {
                    pulseStartedAt = -1f;
                    nextPulseAt = now + Random.Range(Style.PulseGapMin, Style.PulseGapMax);
                }
                return -1f;
            }
            if (t < 0f)
            {
                return -1f;
            }
            float eased = t * t * (3f - 2f * t);
            return eased * routeLength;
        }

        /// <summary>How lit a point is by a passing current: a bright head with a fading tail.</summary>
        private float PulseAt(float at, float pulse)
        {
            if (pulse < 0f)
            {
                return 0f;
            }
            float head = Style.PulseLength * cellSize;
            float tail = Style.PulseTrailLength * cellSize;
            float behind = pulse - at;
            float k = behind >= 0f
                ? (behind < head ? 1f : Mathf.Clamp01(1f - (behind - head) / tail))
                : Mathf.Clamp01(1f + behind / (head * 0.6f));
            return k * k;
        }

        /// <summary>Windows: openings in the body at intervals, suppressed near a bend. Returns
        /// how much of one is open at this point.</summary>
        private float WindowAt(float at, float bend)
        {
            float spacing = Style.EnergyWindowSpacing * cellSize;
            float size = Style.EnergyWindowSize * cellSize;
            float phase = Mathf.Repeat(at, spacing);
            float mid = spacing * 0.5f;
            float d = Mathf.Abs(phase - mid);
            if (d > size * 0.5f)
            {
                return 0f;
            }
            float k = 1f - d / (size * 0.5f);
            float corner = 1f - Mathf.Clamp01(Mathf.Abs(bend) / Style.WindowCornerSuppress);
            return k * k * corner;
        }

        private void Extrude(Strip s, float cut, float confirm, float pulse, float now)
        {
            float settle = Style.CreationSettle * cellSize;
            float energyLag = Style.CreationEnergyDelay * cellSize;
            int n = route.Count;
            int used = 0;

            for (int i = 0; i < n; i++)
            {
                float at = routeAt[i];
                Vector2 p = route[i];
                float bend = routeBend[i];
                bool lastOne = false;
                if (at > cut)
                {
                    if (i == 0)
                    {
                        break;
                    }
                    float span = at - routeAt[i - 1];
                    float k = span > 0.0001f ? (cut - routeAt[i - 1]) / span : 0f;
                    p = Vector2.Lerp(route[i - 1], route[i], k);
                    bend = Mathf.Lerp(routeBend[i - 1], routeBend[i], k);
                    at = cut;
                    lastOne = true;
                }

                Vector2 dir = i == 0
                    ? (route[1] - route[0]).normalized
                    : i >= n - 1
                        ? (route[n - 1] - route[n - 2]).normalized
                        : (route[i + 1] - route[i - 1]).normalized;
                var normal = new Vector2(-dir.y, dir.x);

                float noise = 1f + Style.WidthNoise * Mathf.Sin(at / cellSize * 5.5f);
                float halfA = s.Half * noise * (1f + bend * Style.CornerOuterExpansion);
                float halfB = s.Half * noise * (1f - bend * Style.CornerOuterExpansion);
                // The veins spread a little through a bend, the way strands in a real cable do.
                float lateral = s.Lateral * (1f + Mathf.Abs(bend) * Style.CornerVeinSpread);
                Vector2 centre = p + normal * lateral + s.Offset;

                float shadeA = 1f;
                float shadeB = 1f;
                if (s.Kind == Layer.Body)
                {
                    float lit = Vector2.Dot(normal, Vector2.up);
                    shadeA = 1f + (lit > 0f
                        ? lit * Style.CableBodyHighlight : lit * Style.CableBodyShadow);
                    shadeB = 1f + (-lit > 0f
                        ? -lit * Style.CableBodyHighlight : -lit * Style.CableBodyShadow);
                }

                float hot = Mathf.Clamp01(1f - (cut - at) / Mathf.Max(settle, 0.0001f));
                float boost = 1f + confirm;
                float alpha = 1f;

                switch (s.Kind)
                {
                    case Layer.Shadow:
                        alpha = Style.ContactShadowStrength;
                        break;
                    case Layer.Body:
                        // Faint banding, and a little heat where it was just laid.
                        boost += hot * 0.7f;
                        boost *= 1f - Style.BodyBandingStrength
                            * (0.5f + 0.5f * Mathf.Sin(
                                at / (Style.BodyBandingSpacing * cellSize) * Mathf.PI * 2f));
                        boost += PulseAt(at, pulse) * Style.PulseBodyRim;
                        break;
                    case Layer.Window:
                    {
                        float open = WindowAt(at, bend);
                        float lightUp = PulseAt(at, pulse);
                        alpha = open * (Style.EnergyWindowIdleIntensity
                            + lightUp * Style.EnergyWindowPulseIntensity);
                        // The energy has not reached here yet while the cable is still being laid.
                        alpha *= Mathf.Clamp01((cut - at - energyLag * 0.4f) / (settle * 0.6f));
                        break;
                    }
                    default:
                    {
                        // The two veins drift apart in brightness at rest, out of phase.
                        float side = s.Kind == Layer.VeinA ? 1f : -1f;
                        float drift = 1f + side * Style.VeinIdleVariation
                            * Mathf.Sin(now * Style.VeinIdleSpeed * Mathf.PI * 2f
                                + at / cellSize * 0.35f);
                        boost = (Style.EnergyVeinIdleIntensity * drift
                            + PulseAt(at, pulse) * Style.EnergyVeinPulseIntensity)
                            * (1f + confirm);
                        // Lights a little behind the body, so the cable is laid and then live.
                        alpha = Mathf.Clamp01((cut - at - energyLag) / Mathf.Max(settle, 0.0001f));
                        break;
                    }
                }

                if (burnAt >= 0f)
                {
                    // Gone behind the failure, with a short soft edge so the cable does not end
                    // in a straight cut.
                    alpha *= Mathf.Clamp01((at - burnAt) / (cellSize * 0.7f));
                }

                int vi = used * 2;
                float u = at / Mathf.Max(routeLength, 0.0001f);
                s.Verts[vi] = new Vector3(centre.x + normal.x * halfA,
                    centre.y + normal.y * halfA, 0f);
                s.Verts[vi + 1] = new Vector3(centre.x - normal.x * halfB,
                    centre.y - normal.y * halfB, 0f);
                s.Uvs[vi] = new Vector2(u, 1f);
                s.Uvs[vi + 1] = new Vector2(u, 0f);
                s.Cols[vi] = new Color(boost * shadeA, boost * shadeA, boost * shadeA, alpha);
                s.Cols[vi + 1] = new Color(boost * shadeB, boost * shadeB, boost * shadeB, alpha);
                used++;
                if (lastOne)
                {
                    break;
                }
            }

            s.Mesh.Clear();
            if (used < 2)
            {
                return;
            }
            s.Mesh.vertices = Take(ref scratchV, s.Verts, used * 2);
            s.Mesh.uv = Take(ref scratchU, s.Uvs, used * 2);
            s.Mesh.colors = Take(ref scratchC, s.Cols, used * 2);
            s.Mesh.triangles = Take(ref scratchI, s.Tris, (used - 1) * 6);
            s.Mesh.RecalculateBounds();
        }

        // Reused every rebuild, so the cable allocates nothing per frame.
        private static Vector3[] scratchV;
        private static Vector2[] scratchU;
        private static Color[] scratchC;
        private static int[] scratchI;

        private static T[] Take<T>(ref T[] scratch, T[] src, int count)
        {
            if (scratch == null || scratch.Length != count)
            {
                scratch = new T[count];
            }
            System.Array.Copy(src, scratch, count);
            return scratch;
        }

        private void DrawTip(float cut, bool laid)
        {
            if (laid)
            {
                tip.color = new Color(1f, 1f, 1f, 0f);
                return;
            }
            Vector2 at = PointAt(cut);
            tip.transform.localPosition = new Vector3(at.x, at.y, 0f);
            float s = cellSize * Style.LeadingTipSize;
            tip.transform.localScale = new Vector3(s, s, 1f);
            Color c = Style.EnergyColor;
            c.a = Style.LeadingTipIntensity;
            tip.color = c;
        }

        private Vector2 PointAt(float distance)
        {
            for (int i = 1; i < route.Count; i++)
            {
                if (distance <= routeAt[i])
                {
                    float span = routeAt[i] - routeAt[i - 1];
                    float k = span > 0.0001f ? (distance - routeAt[i - 1]) / span : 0f;
                    return Vector2.Lerp(route[i - 1], route[i], k);
                }
            }
            return route.Count > 0 ? route[route.Count - 1] : Vector2.zero;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < strips.Count; i++)
            {
                if (strips[i].Mesh != null)
                {
                    Destroy(strips[i].Mesh);
                }
            }
        }
    }
}
