// PURPOSE: "Tutuştur" - one fire cube goes up and every other fire on the board BURNS OUT, as one
// combustion wave climbing the board from its bottom row to its top: each cube heats, smoke is born
// under it and climbs over it, behind the smoke it chars and collapses into itself, a few embers and
// ash flakes lift off, and only when the smoke clears is its empty cell seen.
//
// A BURNOUT, NOT AN EXPLOSION. Twenty normal blasts at once is noise and says nothing about a chain;
// nothing here ever goes outward. The source fire keeps the line's own explosion; what this draws
// starts at that explosion's peak with a small warm ripple on the source (not a flash, not a shake),
// and the far fires are consumed.
//
// THE WAVE IS STAGING, NOT RULES. Core took every one of these cubes in the same instant; the view
// holds them as PROXIES (raised on the repaint that emptied their cells - SyncIgnition -> Prepare)
// and plays them row by row, bottom up: a row every 45 ms, squeezed so the whole climb never passes
// 350 ms, with a few ms of deterministic jitter inside a row so a row is "nearly together" and never
// a left-to-right sweep. That staging is the whole message: one peak frame shows the bottom already
// clear, the middle in smoke and the top still fire.
//
// THE BURN IS A MATERIAL, NOT AN ALPHA. The proxy wears IgnitionBurn: heat brightens the middle and
// darkens the rim, then a baked mask chars it from the outline and a few patches inward with an ember
// band on the front, the last heat a small core; only then does it collapse - narrower, and flatter
// still - and fade. Smoke is born at the cube's BOTTOM edge, narrow, warm-lit, and climbs and widens
// over it, in a few puffs of different size and speed; it outlives the cube by a breath and lifts away
// last, which is what reveals the empty cell. Budgets step down with the count (puffs, embers, ash,
// licks) so a board of twenty fires is dense but never a grey fog.
//
// THE VIEW DECIDES NOTHING. The cells, their cubes, the fire that lit them and the points are
// IgnitionVisuals, written by the joker round the engine's own DestroyCubes. Pooled; on the scaled
// clock; announced through Sounded / Haptic per ROW, never per cube.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class IgnitionBurnView : MonoBehaviour
    {
        public static class Style
        {
            public static float ActivationDelay = 0.10f;
            public static float ActivationTime = 0.18f;
            public static float ActivationRadius = 1.5f;   // cells
            public static float ActivationAlpha = 0.22f;
            public static float WaveLead = 0.06f;
            public static float RowDelay = 0.045f;
            public static float MaxPropagation = 0.35f;
            public static float SameRowJitter = 0.015f;
            public static float Heat = 0.075f;
            public static float HeatScale = 1.025f;
            public static float SmokeBirth = 0.03f;
            public static float SmokeRise = 0.16f;
            public static float SmokeRiseCells = 0.62f;
            public static float SmokeWobblePx = 3f;
            public static float SmokeAlphaStart = 0.38f;
            public static float SmokeAlphaPeak = 0.64f;
            public static float BurnFrom = 0.07f;
            public static float Burnout = 0.19f;
            public static float CollapseFrom = 0.19f;
            public static float Collapse = 0.14f;
            public static float CollapseX = 0.62f;
            public static float CollapseY = 0.45f;
            public static float SmokeHold = 0.07f;
            public static float SmokeClear = 0.17f;
            public static float SmokeClearRisePx = 10f;
            public static float SmokeClearScale = 1.15f;
            public static float ScorchTime = 0.07f;
            public static float ScorchAlpha = 0.15f;
            public static int MaxSmoke = 36;
            public static int MaxEmbers = 30;
            public static int MaxAsh = 24;
            public static float HazeAlpha = 0.05f;
            public static float ValueHold = 0.15f;
            public static float ScoreTravel = 0.35f;
            public static float ScorePunch = 0.06f;
            public static float ScoreTime = 0.30f;
            public static Color SmokeWarm = new Color(0.58f, 0.32f, 0.16f);
            public static Color SmokeCool = new Color(0.44f, 0.36f, 0.30f);
            public static Color Ember = new Color(1f, 0.64f, 0.22f);
            public static Color Ash = new Color(0.16f, 0.13f, 0.12f);
            public static Color Ripple = new Color(1f, 0.60f, 0.25f);
            public static Color Charcoal = new Color(0.10f, 0.07f, 0.06f);
            public static Color ValueInk = new Color(1f, 0.95f, 0.85f);
            public static Color ValueShade = new Color(0.36f, 0.16f, 0.04f);
            public static Color ScoreInk = new Color(1f, 0.90f, 0.70f);
        }

        public static class Layers
        {
            public static bool ShowProxy = true;
            public static bool ShowActivation = true;
            public static bool ShowHeat = true;
            public static bool ShowFlameLicks = true;
            public static bool ShowSmokeBirth = true;
            public static bool ShowSmokeClimb = true;
            public static bool ShowSmokeClear = true;
            public static bool ShowBurnout = true;
            public static bool ShowCollapse = true;
            public static bool ShowEmbers = true;
            public static bool ShowAshFlakes = true;
            public static bool ShowScorch = true;
            public static bool ShowScore = true;

            // debug (editor / debug builds only)
            public static bool ShowIgnitionTargets;
            public static bool ShowRowBuckets;
            public static bool ShowWaveStartTimes;
            public static bool ShowSmokeBounds;
            public static bool ShowBurnMask;
            public static bool ShowAsh;
            public static bool ShowEmberDebug;
            public static bool ShowProxyDebug;
            public static bool ShowGlobalHazeBand;

            public static void AllOn()
            {
                ShowProxy = ShowActivation = ShowHeat = ShowFlameLicks = true;
                ShowSmokeBirth = ShowSmokeClimb = ShowSmokeClear = true;
                ShowBurnout = ShowCollapse = ShowEmbers = ShowAshFlakes = ShowScorch = ShowScore = true;
                ShowIgnitionTargets = ShowRowBuckets = ShowWaveStartTimes = ShowSmokeBounds = false;
                ShowBurnMask = ShowAsh = ShowEmberDebug = ShowProxyDebug = ShowGlobalHazeBand = false;
            }
        }

        public static System.Action<string> Sounded;
        public static System.Action<string> Haptic;

        public const string SoundTriggered = "tutustur.triggered";
        public const string SoundWaveRow = "tutustur.wave.row";
        public const string SoundCrackle = "tutustur.crackle";
        public const string SoundBurnout = "tutustur.burnout";
        public const string SoundSmokeClear = "tutustur.smoke.clear";
        public const string SoundComplete = "tutustur.complete";
        public const string HapticPeak = "tutustur.haptic.peak";

        public delegate bool FaceOf(GridPos cell, Cube cube, out Sprite tile, out Color colour);

        public System.Func<GridPos, Vector2> CellWorld;
        public System.Func<float> CubeSize;
        public System.Func<float> CellSize;
        public System.Func<float> Pixel;
        public System.Func<Vector2> ScoreAnchor;
        public System.Func<Rect> BoardRect;
        public System.Func<GridPos, int> RowOf;
        public FaceOf Face;

        private const int HazeOrder = 15;
        private const int RippleOrder = 16;
        private const int ProxyOrder = 17;
        private const int LickOrder = 18;
        private const int SmokeOrder = 19;
        private const int ParticleOrder = 20;
        private const int TextOrder = 91;
        private const int SeedOrder = 93;
        private const int DebugOrder = 96;

        private static Material burnMaterial;
        private static bool burnShaderMissing;
        private static readonly int HeatId = Shader.PropertyToID("_Heat");
        private static readonly int CharId = Shader.PropertyToID("_Char");
        private static readonly int MaskId = Shader.PropertyToID("_BurnMask");
        private static readonly int MaskRotId = Shader.PropertyToID("_MaskRot");
        private static readonly int CharcoalId = Shader.PropertyToID("_Charcoal");

        private IgnitionVisuals report;
        private IgnitionVisuals lastBegun;
        private float clock;
        private float preparedAt = -1f;
        private float beginAt = float.PositiveInfinity;
        private readonly List<Target> targets = new List<Target>();
        private readonly List<SpriteRenderer> ripples = new List<SpriteRenderer>();
        private readonly List<Bit> bits = new List<Bit>();
        private readonly List<Value> values = new List<Value>();
        private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        private readonly HashSet<string> said = new HashSet<string>();
        private readonly List<SpriteRenderer> debugMarks = new List<SpriteRenderer>();
        private MaterialPropertyBlock block;
        private SpriteRenderer haze;
        private TextMesh debugText;
        private float waveSpan;
        private float firstArrival = -1f;

        public float ScoreScale { get; private set; }
        public float ScoreClaim { get; private set; }
        public Color ScoreInk { get { return Style.ScoreInk; } }

        public bool Busy
        {
            get { return report != null; }
        }

        private sealed class Puff
        {
            public SpriteRenderer R;
            public float Born;
            public float X;
            public float Size;
            public float Speed;
            public float Phase;
        }

        private sealed class Target
        {
            public GridPos Cell;
            public int Row;
            public float Start;
            public SpriteRenderer Proxy;
            public Color Colour;
            public Vector2 MaskRot;
            public int Mask;
            public readonly List<Puff> Puffs = new List<Puff>();
            public readonly List<SpriteRenderer> Licks = new List<SpriteRenderer>();
            public SpriteRenderer Scorch;
            public int Embers;
            public int Ash;
            public bool Released;
            public bool Collapsed;
            public bool Cleared;
            public uint Seed;
        }

        private sealed class Bit
        {
            public SpriteRenderer R;
            public float Born;
            public float Life;
            public Vector2 From;
            public Vector2 Velocity;
            public float Size;
            public Color Tint;
            public bool Ember;
        }

        private sealed class Value
        {
            public Vector2 At;
            public int Amount;
            public float BornAt;
            public TextMesh Text;
            public TextMesh Shade;
            public SpriteRenderer Seed;
            public bool Arrived;
        }

        private void Awake()
        {
            ScoreScale = 1f;
            block = new MaterialPropertyBlock();
            haze = Rent(HazeOrder);
            haze.sprite = HazineShapes.Light(1);
        }

        private static Material BurnMaterial
        {
            get
            {
                if (burnMaterial == null && !burnShaderMissing)
                {
                    Shader shader = Shader.Find("ProjectBlock/IgnitionBurn");
                    if (shader == null)
                    {
                        shader = Resources.Load<Shader>("Shaders/IgnitionBurn");
                    }
                    if (shader == null)
                    {
                        burnShaderMissing = true;
                        Debug.LogWarning("[block_bonk] IgnitionBurn shader missing - the burnout falls back to a tint");
                    }
                    else
                    {
                        burnMaterial = new Material(shader);
                    }
                }
                return burnMaterial;
            }
        }

        public bool HasSeen(IgnitionVisuals r)
        {
            return r == null || ReferenceEquals(r, lastBegun) || ReferenceEquals(r, report);
        }

        // ================================================================ inputs

        /// <summary>The repaint that emptied the fire: raise the cubes as proxies and HOLD them.
        /// </summary>
        public void Prepare(IgnitionVisuals r)
        {
            if (r == null || r.Count == 0 || HasSeen(r))
            {
                return;
            }
            Stop();
            report = r;
            preparedAt = clock;
            beginAt = float.PositiveInfinity;
            said.Clear();
            firstArrival = -1f;
            Build();
            Tick(0f);
        }

        /// <summary>The source's explosion is being drawn now; the chain starts from its peak.
        /// </summary>
        public void Begin(IgnitionVisuals r, float delay)
        {
            if (r == null || r.Count == 0 || ReferenceEquals(r, lastBegun))
            {
                return;
            }
            if (!ReferenceEquals(r, report))
            {
                Prepare(r);
            }
            lastBegun = r;
            beginAt = clock + Mathf.Max(0f, delay);
            Tick(0f);
        }

        public void MarkSeen(IgnitionVisuals r)
        {
            if (r != null)
            {
                lastBegun = r;
            }
        }

        public void Forget()
        {
            lastBegun = null;
        }

        public void Stop()
        {
            foreach (Target t in targets)
            {
                Return(t.Proxy);
                Return(t.Scorch);
                foreach (Puff p in t.Puffs) { Return(p.R); }
                foreach (SpriteRenderer l in t.Licks) { Return(l); }
            }
            targets.Clear();
            foreach (SpriteRenderer r in ripples) { Return(r); }
            ripples.Clear();
            foreach (Bit b in bits) { Return(b.R); }
            bits.Clear();
            foreach (Value v in values)
            {
                if (v.Text != null)
                {
                    Destroy(v.Text.gameObject);
                    Destroy(v.Shade.gameObject);
                    Return(v.Seed);
                }
            }
            values.Clear();
            haze.enabled = false;
            report = null;
            beginAt = float.PositiveInfinity;
            ScoreScale = 1f;
            ScoreClaim = 0f;
            for (int i = 0; i < debugMarks.Count; i++) { debugMarks[i].enabled = false; }
            if (debugText != null) { debugText.gameObject.SetActive(false); }
        }

        // ================================================================ planning

        private void Build()
        {
            int n = report.Count;
            maxRowCache = -1;
            int minRow = int.MaxValue;
            int maxRow = int.MinValue;
            for (int i = 0; i < n; i++)
            {
                int row = RowOf != null ? RowOf(report.Cells[i]) : report.Cells[i].Y;
                minRow = Mathf.Min(minRow, row);
                maxRow = Mathf.Max(maxRow, row);
            }
            int span = Mathf.Max(0, maxRow - minRow);
            float rowDelay = span > 0 ? Mathf.Min(Style.RowDelay, Style.MaxPropagation / span) : Style.RowDelay;
            waveSpan = span * rowDelay;
            int puffs = n <= 4 ? 4 : n <= 12 ? 3 : 2;
            puffs = Mathf.Clamp(Mathf.Min(puffs, Style.MaxSmoke / Mathf.Max(1, n)), 1, 4);
            int embers = Mathf.Clamp(Style.MaxEmbers / Mathf.Max(1, n), 1, 5);
            int ash = Mathf.Clamp(Style.MaxAsh / Mathf.Max(1, n), 1, 3);
            int licks = n <= 12 ? 3 : 1;

            for (int i = 0; i < n; i++)
            {
                var t = new Target { Cell = report.Cells[i] };
                t.Row = (RowOf != null ? RowOf(t.Cell) : t.Cell.Y) - minRow;
                t.Seed = report.Seed ^ (uint)((t.Cell.X * 92821 + t.Cell.Y * 68917) * 2654435761u);
                var rng = new Lcg(t.Seed);
                // Bottom row first; a row is NEARLY together, never a left-to-right sweep.
                t.Start = Style.WaveLead + t.Row * rowDelay + rng.Range(-Style.SameRowJitter, Style.SameRowJitter);
                t.Start = Mathf.Max(0f, t.Start);
                Sprite tile;
                Color colour;
                if (Face == null || !Face(t.Cell, report.Cubes[i], out tile, out colour))
                {
                    tile = ViewUtil.CubeTile(CubeKind.Fire);
                    colour = Color.white;
                }
                t.Colour = colour;
                t.Proxy = Rent(ProxyOrder);
                t.Proxy.sprite = tile;
                t.Proxy.color = colour;
                if (BurnMaterial != null)
                {
                    t.Proxy.sharedMaterial = BurnMaterial;
                }
                int turn = rng.Int(0, 4);
                t.MaskRot = new Vector2(turn == 0 ? 1f : turn == 2 ? -1f : 0f, turn == 1 ? 1f : turn == 3 ? -1f : 0f);
                t.Mask = rng.Int(0, IgnitionShapes.MaskVariants);
                for (int p = 0; p < puffs; p++)
                {
                    var puff = new Puff
                    {
                        R = Rent(SmokeOrder),
                        Born = Style.SmokeBirth + p * 0.025f + rng.Range(0f, 0.01f),
                        X = (puffs == 1 ? 0f : (p / (float)(puffs - 1) - 0.5f) * 0.46f) + rng.Range(-0.06f, 0.06f),
                        Size = rng.Range(0.85f, 1.15f),
                        Speed = rng.Range(0.85f, 1.15f),
                        Phase = rng.Range(0f, 6.28f)
                    };
                    puff.R.sprite = HazineShapes.Puff(rng.Int(0, HazineShapes.PuffVariants));
                    t.Puffs.Add(puff);
                }
                for (int l = 0; l < licks; l++)
                {
                    SpriteRenderer lick = Rent(LickOrder);
                    lick.sprite = IgnitionShapes.Lick;
                    t.Licks.Add(lick);
                }
                t.Scorch = Rent(RippleOrder);
                t.Scorch.sprite = QuarryShapes.Plate;
                t.Embers = embers;
                t.Ash = ash;
                targets.Add(t);
            }
            if (report.Points != 0)
            {
                float CollapseEnd(Target t) { return t.Start + Style.CollapseFrom + Style.Collapse; }
                if (n <= 3)
                {
                    foreach (Target t in targets)
                    {
                        values.Add(new Value { At = CellWorld(t.Cell), Amount = report.PointsEach, BornAt = CollapseEnd(t) });
                    }
                }
                else
                {
                    // One total, when most of the board has burned (70%).
                    var ends = new List<float>();
                    Vector2 sum = Vector2.zero;
                    foreach (Target t in targets)
                    {
                        ends.Add(CollapseEnd(t));
                        sum += CellWorld(t.Cell);
                    }
                    ends.Sort();
                    values.Add(new Value
                    {
                        At = sum / n,
                        Amount = report.Points,
                        BornAt = ends[Mathf.Clamp(Mathf.CeilToInt(n * 0.7f) - 1, 0, n - 1)]
                    });
                }
            }
        }

        // ================================================================ clock

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void Tick(float dt)
        {
            clock += dt;
            if (report == null)
            {
                return;
            }
            if (float.IsPositiveInfinity(beginAt) && clock - preparedAt > 3f)
            {
                lastBegun = report;
                beginAt = clock;
            }
            float t = clock - beginAt;
            float cube = CubeSize != null ? CubeSize() : 0.9f;
            float cell = CellSize != null ? CellSize() : cube;
            float px = Pixel != null ? Pixel() : 0.01f;
            PaintActivation(t, cell);
            PaintHaze(t, cell);
            bool done = !float.IsInfinity(t);
            foreach (Target target in targets)
            {
                if (!PaintTarget(target, t, cube, cell, px))
                {
                    done = false;
                }
            }
            PaintBits();
            foreach (Bit b in bits)
            {
                if (clock - b.Born < b.Life) { done = false; }
            }
            if (!PaintValues(t, cell))
            {
                done = false;
            }
            PaintDebug(t, cell);
            if (done)
            {
                if (Sounded != null) { Sounded(SoundComplete); }
                Stop();
            }
        }

        private void PaintActivation(float t, float cell)
        {
            float k = t / Style.ActivationTime;
            bool on = Layers.ShowActivation && k >= 0f && k <= 1f && report.SourceCells.Count > 0;
            while (ripples.Count < report.SourceCells.Count)
            {
                SpriteRenderer r = Rent(RippleOrder);
                r.sprite = HazineShapes.Ring;
                ripples.Add(r);
            }
            if (k >= 0f)
            {
                Say(Sounded, SoundTriggered);
            }
            for (int i = 0; i < ripples.Count; i++)
            {
                SpriteRenderer r = ripples[i];
                r.enabled = on;
                if (!on)
                {
                    continue;
                }
                float e = 1f - (1f - k) * (1f - k);
                float size = Mathf.Lerp(0.6f, Style.ActivationRadius * 2f, e) * cell;
                r.transform.position = CellWorld(report.SourceCells[i]);
                r.transform.localScale = new Vector3(size, size, 1f);
                Color c = Style.Ripple;
                c.a = Style.ActivationAlpha * (1f - k);
                r.color = c;
            }
        }

        /// <summary>Optional (off by default): a faint warm band climbing with the wave.</summary>
        private void PaintHaze(float t, float cell)
        {
            bool on = Layers.ShowGlobalHazeBand && BoardRect != null && t >= Style.WaveLead
                && t <= Style.WaveLead + waveSpan + 0.25f;
            haze.enabled = on;
            if (!on)
            {
                return;
            }
            Rect r = BoardRect();
            float k = Mathf.Clamp01((t - Style.WaveLead) / Mathf.Max(0.001f, waveSpan + 0.25f));
            haze.transform.position = new Vector2(r.center.x, Mathf.Lerp(r.yMin, r.yMax, k));
            haze.transform.localScale = new Vector3(r.width * 1.1f, cell * 0.8f * 2f, 1f);
            Color c = Style.Ember;
            c.a = Style.HazeAlpha * Mathf.Sin(k * Mathf.PI);
            haze.color = c;
        }

        /// <summary>True once this target has nothing left to show.</summary>
        private bool PaintTarget(Target target, float t, float cube, float cell, float px)
        {
            float a = t - target.Start;   // this cube's own clock
            Vector2 at = CellWorld(target.Cell);
            if (a >= 0f && target.Row % 2 == 0)
            {
                Say(Sounded, SoundWaveRow + target.Row);
            }
            if (a >= 0f)
            {
                Say(Sounded, SoundCrackle);
                if (target.Row * 2 >= MaxRow())
                {
                    Say(Haptic, HapticPeak);
                }
            }

            // ---- the proxy: heat, char, collapse
            float heatK = Mathf.Clamp01(a / Style.Heat);
            float heat = a < 0f ? 0f : a < Style.CollapseFrom + 0.07f ? heatK
                : Mathf.Clamp01(1f - (a - Style.CollapseFrom - 0.07f) / 0.1f);
            float charK = Mathf.Clamp01((a - Style.BurnFrom) / Style.Burnout);
            float collapse = Mathf.Clamp01((a - Style.CollapseFrom) / Style.Collapse);
            float sx = 1f;
            float sy = 1f;
            if (a >= 0f && a < Style.Heat)
            {
                float s = 1f + (Style.HeatScale - 1f) * Mathf.Sin(heatK * Mathf.PI) - 0.01f * heatK;
                sx = sy = s;
            }
            else if (a >= Style.Heat)
            {
                sx = Piece(collapse, 0.99f, 0.92f, 0.75f, Style.CollapseX);
                sy = Piece(collapse, 0.99f, 0.90f, 0.70f, Style.CollapseY);
            }
            float alpha = Piece(collapse, 1f, 0.8f, 0.35f, 0f);
            if (!Layers.ShowCollapse)
            {
                sx = sy = 1f;
                alpha = collapse >= 1f ? 0f : 1f;
            }
            bool standing = alpha > 0.001f;
            if (collapse >= 1f && !target.Collapsed)
            {
                target.Collapsed = true;
                Say(Sounded, SoundBurnout);
            }
            SpriteRenderer proxy = target.Proxy;
            proxy.enabled = standing && Layers.ShowProxy;
            proxy.transform.localPosition = Vector3.zero;
            proxy.transform.position = at + new Vector2(0f, -cube * (1f - sy) * 0.25f);
            proxy.transform.localScale = new Vector3(cube * sx, cube * sy, 1f);
            float heatShown = Layers.ShowHeat ? heat : 0f;
            float charShown = Layers.ShowBurnout ? charK : 0f;
            if (Layers.ShowBurnMask && DebugAllowed)
            {
                charShown = Mathf.Repeat(clock * 0.7f, 1f);
            }
            Color tint = target.Colour;
            if (Layers.ShowProxyDebug && DebugAllowed)
            {
                tint = Color.Lerp(tint, new Color(1f, 0.3f, 1f), 0.4f);
            }
            if (proxy.sharedMaterial == BurnMaterial && BurnMaterial != null)
            {
                block.Clear();
                block.SetFloat(HeatId, heatShown);
                block.SetFloat(CharId, charShown);
                block.SetTexture(MaskId, IgnitionShapes.Mask(target.Mask));
                block.SetVector(MaskRotId, new Vector4(target.MaskRot.x, target.MaskRot.y, 0f, 0f));
                block.SetColor(CharcoalId, Style.Charcoal);
                proxy.SetPropertyBlock(block);
            }
            else
            {
                // No shader: the cube still burns - toward charcoal - with less detail.
                tint = Color.Lerp(tint, Style.Charcoal, charShown);
            }
            tint.a *= alpha;
            proxy.color = tint;

            // ---- flame licks on the rim during the surge
            for (int i = 0; i < target.Licks.Count; i++)
            {
                SpriteRenderer lick = target.Licks[i];
                float lk = (a - i * 0.012f) / (Style.Heat + 0.04f);
                bool on = Layers.ShowFlameLicks && lk >= 0f && lk <= 1f;
                lick.enabled = on;
                if (!on)
                {
                    continue;
                }
                var rng = new Lcg(target.Seed ^ (uint)(i * 7919 + 13));
                int side = rng.Int(0, 3); // left, right, top rim - never under it
                float along = rng.Range(-0.35f, 0.35f);
                Vector2 root = side == 0 ? new Vector2(-0.48f, along) : side == 1 ? new Vector2(0.48f, along)
                    : new Vector2(along, 0.48f);
                lick.transform.position = at + root * cube;
                lick.transform.rotation = Quaternion.Euler(0f, 0f, side == 0 ? 25f : side == 1 ? -25f : rng.Range(-15f, 15f));
                float h = cube * 0.22f * Mathf.Sin(lk * Mathf.PI);
                lick.transform.localScale = new Vector3(h * 0.7f, h, 1f);
                lick.color = new Color(1f, 1f, 1f, 0.85f * Mathf.Sin(lk * Mathf.PI));
            }

            // ---- embers and ash, in the collapse's last 40%
            if (collapse >= 0.6f && !target.Released)
            {
                target.Released = true;
                Release(target, at, cube, cell, px);
            }

            // ---- temporary scorch
            float sk = (a - Style.CollapseFrom - Style.Collapse) / Style.ScorchTime;
            bool scorch = Layers.ShowScorch && sk >= 0f && sk <= 1f;
            target.Scorch.enabled = scorch;
            if (scorch)
            {
                target.Scorch.transform.position = at;
                target.Scorch.transform.localScale = new Vector3(cube * 0.9f, cube * 0.9f, 1f);
                target.Scorch.color = new Color(0.30f, 0.16f, 0.08f, Style.ScorchAlpha * (1f - sk));
            }

            // ---- smoke: born under the cube, climbing and widening over it, outliving it
            float clearAt = Style.CollapseFrom + Style.Collapse + Style.SmokeHold;
            bool smokeAlive = a < clearAt + Style.SmokeClear;
            if (a >= clearAt && !target.Cleared)
            {
                target.Cleared = true;
                Say(Sounded, SoundSmokeClear);
            }
            foreach (Puff puff in target.Puffs)
            {
                float age = a - puff.Born;
                SpriteRenderer r = puff.R;
                if (age < 0f || !smokeAlive)
                {
                    r.enabled = false;
                    continue;
                }
                float rise = Mathf.Clamp01(age * puff.Speed / Style.SmokeRise);
                float eased = 1f - (1f - rise) * (1f - rise);
                float clear = Mathf.Clamp01((a - clearAt) / Style.SmokeClear);
                bool phaseOn = clear > 0f ? Layers.ShowSmokeClear : rise < 0.35f ? Layers.ShowSmokeBirth : Layers.ShowSmokeClimb;
                r.enabled = phaseOn;
                if (!phaseOn)
                {
                    continue;
                }
                float y = -0.42f * cube + eased * Style.SmokeRiseCells * cell + clear * Style.SmokeClearRisePx * px;
                float x = puff.X * cube + Mathf.Sin(age * 25f + puff.Phase) * Style.SmokeWobblePx * px;
                r.transform.position = at + new Vector2(x, y);
                // Narrow at birth, widening as it climbs: a funnel, not a ball.
                float size = cube * (0.45f + 0.40f * eased) * puff.Size * Mathf.Lerp(1f, Style.SmokeClearScale, clear);
                float squeeze = Mathf.Lerp(0.65f, 1f, eased);
                r.transform.localScale = new Vector3(size * squeeze, size * 0.9f, 1f);
                Color c = Color.Lerp(Style.SmokeWarm, Style.SmokeCool, eased);
                c.a = Mathf.Lerp(Style.SmokeAlphaStart, Style.SmokeAlphaPeak, eased) * (1f - clear);
                r.color = c;
            }
            return !float.IsInfinity(t) && a >= clearAt + Style.SmokeClear;
        }

        private int maxRowCache = -1;

        private int MaxRow()
        {
            if (maxRowCache < 0)
            {
                foreach (Target t in targets) { maxRowCache = Mathf.Max(maxRowCache, t.Row); }
            }
            return maxRowCache;
        }

        private static float Piece(float k, float a, float b, float c, float d)
        {
            if (k < 0.35f) { return Mathf.Lerp(a, b, k / 0.35f); }
            if (k < 0.7f) { return Mathf.Lerp(b, c, (k - 0.35f) / 0.35f); }
            return Mathf.Lerp(c, d, (k - 0.7f) / 0.3f);
        }

        private void Release(Target target, Vector2 at, float cube, float cell, float px)
        {
            var rng = new Lcg(target.Seed ^ 0xabcdef1u);
            float grow = DebugAllowed && Layers.ShowEmberDebug ? 2.5f : 1f;
            if (Layers.ShowEmbers)
            {
                for (int i = 0; i < target.Embers; i++)
                {
                    var v = new Vector2(rng.Range(-0.35f, 0.35f), rng.Range(0.8f, 1.4f)) * cell;
                    AddBit(at + new Vector2(rng.Range(-0.25f, 0.25f), rng.Range(-0.1f, 0.2f)) * cube, v,
                        rng.Range(0.30f, 0.45f), rng.Range(1f, 3f) * px * 1.5f * grow, Style.Ember, true);
                }
            }
            float ashGrow = DebugAllowed && Layers.ShowAsh ? 2.5f : 1f;
            if (Layers.ShowAshFlakes)
            {
                for (int i = 0; i < target.Ash; i++)
                {
                    var v = new Vector2(rng.Range(-0.2f, 0.2f), rng.Range(0.35f, 0.6f)) * cell;
                    AddBit(at + new Vector2(rng.Range(-0.25f, 0.25f), rng.Range(-0.1f, 0.1f)) * cube, v,
                        rng.Range(0.35f, 0.5f), rng.Range(1f, 4f) * px * 1.5f * ashGrow, Style.Ash, false);
                }
            }
        }

        private void AddBit(Vector2 from, Vector2 velocity, float life, float size, Color tint, bool ember)
        {
            SpriteRenderer r = Rent(ParticleOrder);
            r.sprite = ember ? HazineShapes.Mote : QuarryShapes.Dust;
            bits.Add(new Bit { R = r, Born = clock, Life = life, From = from, Velocity = velocity, Size = size, Tint = tint, Ember = ember });
        }

        private void PaintBits()
        {
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                Bit b = bits[i];
                float age = clock - b.Born;
                float k = age / b.Life;
                if (k > 1f)
                {
                    Return(b.R);
                    bits.RemoveAt(i);
                    continue;
                }
                b.R.enabled = true;
                // Embers lift and drift; ash lifts slower and slows down.
                float drag = b.Ember ? 1f - 0.4f * k : 1f - 0.7f * k;
                b.R.transform.position = b.From + b.Velocity * age * drag;
                b.R.transform.rotation = Quaternion.Euler(0f, 0f, b.Ember ? 0f : age * 180f);
                b.R.transform.localScale = new Vector3(b.Size, b.Size, 1f);
                Color c = b.Tint;
                c.a = b.Ember ? 1f - k * k : 0.9f * (1f - k);
                b.R.color = c;
            }
        }

        private bool PaintValues(float t, float cell)
        {
            ScoreScale = 1f;
            ScoreClaim = 0f;
            bool done = true;
            foreach (Value v in values)
            {
                float age = t - v.BornAt;
                if (!Layers.ShowScore || float.IsInfinity(t) || age < 0f)
                {
                    if (v.Text != null)
                    {
                        v.Text.gameObject.SetActive(false);
                        v.Shade.gameObject.SetActive(false);
                        v.Seed.enabled = false;
                    }
                    if (Layers.ShowScore) { done = false; }
                    continue;
                }
                if (v.Text == null)
                {
                    v.Shade = ViewUtil.MakeText3D(transform, "IgnitionValueShade", Vector2.zero, string.Empty, 64, 0.03f,
                        Style.ValueShade, TextOrder - 1, TextAnchor.MiddleCenter);
                    v.Text = ViewUtil.MakeText3D(transform, "IgnitionValue", Vector2.zero, string.Empty, 64, 0.03f,
                        Style.ValueInk, TextOrder, TextAnchor.MiddleCenter);
                    v.Seed = Rent(SeedOrder);
                    v.Seed.sprite = ChallengeShapes.Seed;
                }
                Vector2 at = v.At + new Vector2(0f, 0.2f * cell);
                const float stamp = 0.12f;
                float hold = stamp + Style.ValueHold;
                // THE NUMBER IS CORE'S.
                string text = (v.Amount < 0 ? "-" : "+") + Mathf.Abs(v.Amount);
                bool showText = age < hold + 0.08f;
                v.Text.gameObject.SetActive(showText);
                v.Shade.gameObject.SetActive(showText);
                if (showText)
                {
                    float s = age < stamp * 0.6f ? Mathf.Lerp(0.7f, 1.08f, age / (stamp * 0.6f))
                        : Mathf.Lerp(1.08f, 1f, Mathf.Clamp01((age - stamp * 0.6f) / (stamp * 0.4f)));
                    float fold = Mathf.Clamp01((age - hold) / 0.08f);
                    float h = cell * (values.Count > 1 ? 0.28f : 0.34f) * s * (1f - 0.5f * fold);
                    v.Text.text = text;
                    v.Shade.text = text;
                    v.Text.characterSize = Mathf.Max(h * 10f / 64f, 0.0001f);
                    v.Shade.characterSize = v.Text.characterSize;
                    v.Text.transform.position = at;
                    v.Shade.transform.position = at + new Vector2(h * 0.06f, -h * 0.06f);
                    Color ink = Style.ValueInk;
                    ink.a = 1f - fold;
                    v.Text.color = ink;
                    Color sh = Style.ValueShade;
                    sh.a = 0.85f * (1f - fold);
                    v.Shade.color = sh;
                }
                float flight = (age - hold) / Style.ScoreTravel;
                Vector2 target = ScoreAnchor != null ? ScoreAnchor() : at;
                bool flying = flight >= 0f && flight <= 1f;
                v.Seed.enabled = flying;
                if (flight < 1f) { done = false; }
                if (flying)
                {
                    float k = 1f - (1f - flight) * (1f - flight);
                    Vector2 c = (at + target) * 0.5f + new Vector2(0f, Mathf.Min((target - at).magnitude * 0.25f, 1.2f));
                    float u = 1f - k;
                    v.Seed.transform.position = u * u * at + 2f * u * k * c + k * k * target;
                    float ss = cell * 0.24f;
                    v.Seed.transform.localScale = new Vector3(ss, ss, 1f);
                    v.Seed.color = new Color(1f, 0.85f, 0.6f);
                }
                if (flight > 1f && !v.Arrived)
                {
                    v.Arrived = true;
                    if (firstArrival < 0f) { firstArrival = clock; }
                }
            }
            if (firstArrival >= 0f)
            {
                float a = (clock - firstArrival) / Style.ScoreTime;
                if (a <= 1f)
                {
                    ScoreScale = 1f + Style.ScorePunch * Mathf.Sin(a * Mathf.PI) * (report != null && report.Points < 0 ? -0.6f : 1f);
                    ScoreClaim = 1f - a;
                    done = false;
                }
            }
            return done;
        }

        // ---- debug -----------------------------------------------------------------------------

        private static bool DebugAllowed
        {
            get { return Application.isEditor || Debug.isDebugBuild; }
        }

        private static readonly Color[] RowColours =
        {
            new Color(1f, 0.3f, 0.3f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.95f, 0.3f),
            new Color(0.5f, 1f, 0.4f), new Color(0.3f, 0.9f, 1f), new Color(0.5f, 0.5f, 1f), new Color(0.9f, 0.4f, 1f)
        };

        private void PaintDebug(float t, float cell)
        {
            int used = 0;
            if (DebugAllowed && (Layers.ShowIgnitionTargets || Layers.ShowRowBuckets || Layers.ShowSmokeBounds))
            {
                foreach (Target target in targets)
                {
                    Vector2 at = CellWorld(target.Cell);
                    if (Layers.ShowIgnitionTargets || Layers.ShowRowBuckets)
                    {
                        Color c = Layers.ShowRowBuckets ? RowColours[target.Row % RowColours.Length] : new Color(1f, 0.6f, 0.2f);
                        Mark(ref used, at, cell, HazineShapes.Frame, c);
                    }
                    if (Layers.ShowSmokeBounds)
                    {
                        Mark(ref used, at + new Vector2(0f, Style.SmokeRiseCells * cell * 0.5f), cell * 1.3f, HazineShapes.Frame,
                            new Color(0.7f, 0.7f, 0.7f, 0.8f));
                    }
                }
            }
            for (int i = used; i < debugMarks.Count; i++) { debugMarks[i].enabled = false; }
            bool label = DebugAllowed && Layers.ShowWaveStartTimes && targets.Count > 0;
            if (label)
            {
                if (debugText == null)
                {
                    debugText = ViewUtil.MakeText3D(transform, "IgnitionDebug", Vector2.zero, string.Empty, 40, 0.03f,
                        Color.white, DebugOrder, TextAnchor.UpperLeft);
                }
                debugText.gameObject.SetActive(true);
                var rows = new SortedDictionary<int, float>();
                foreach (Target target in targets)
                {
                    float s;
                    rows[target.Row] = rows.TryGetValue(target.Row, out s) ? Mathf.Min(s, target.Start) : target.Start;
                }
                var sb = new System.Text.StringBuilder();
                foreach (KeyValuePair<int, float> kv in rows)
                {
                    sb.Append("row ").Append(kv.Key).Append(": ").Append(Mathf.RoundToInt(kv.Value * 1000f)).Append(" ms\n");
                }
                sb.Append("fires ").Append(report.Count).Append("  points ").Append(report.Points);
                debugText.text = sb.ToString();
                debugText.characterSize = cell * 0.16f * 10f / 40f;
                Rect r = BoardRect != null ? BoardRect() : new Rect();
                debugText.transform.position = new Vector2(r.xMax + cell * 0.2f, r.yMax);
            }
            else if (debugText != null)
            {
                debugText.gameObject.SetActive(false);
            }
        }

        private void Mark(ref int used, Vector2 at, float size, Sprite sprite, Color c)
        {
            if (used >= debugMarks.Count)
            {
                debugMarks.Add(Rent(DebugOrder));
            }
            SpriteRenderer r = debugMarks[used++];
            r.enabled = true;
            r.sprite = sprite;
            r.transform.position = at;
            r.transform.localScale = new Vector3(size, size, 1f);
            r.color = c;
        }

        // ---- housekeeping ----------------------------------------------------------------------

        private void Say(System.Action<string> channel, string what)
        {
            if (said.Add(what) && channel != null)
            {
                channel(what.StartsWith(SoundWaveRow) ? SoundWaveRow : what);
            }
        }

        private SpriteRenderer Rent(int order)
        {
            SpriteRenderer r;
            if (pool.Count > 0)
            {
                r = pool.Pop();
                r.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("IgnitionPart");
                r = go.AddComponent<SpriteRenderer>();
            }
            r.transform.SetParent(transform, false);
            r.sortingOrder = order;
            r.enabled = false;
            r.color = Color.white;
            r.sharedMaterial = DefaultMaterial;
            r.SetPropertyBlock(null);
            // A pooled renderer keeps its last LOCAL transform; every one is reset here.
            r.transform.localPosition = Vector3.zero;
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = Vector3.one;
            return r;
        }

        private static Material defaultMaterial;

        /// <summary>The plain sprite material, so a renderer that wore the burn shader gives it
        /// back when it returns to the pool.</summary>
        private static Material DefaultMaterial
        {
            get
            {
                if (defaultMaterial == null)
                {
                    var probe = new GameObject("probe").AddComponent<SpriteRenderer>();
                    defaultMaterial = probe.sharedMaterial;
                    Destroy(probe.gameObject);
                }
                return defaultMaterial;
            }
        }

        private void Return(SpriteRenderer r)
        {
            if (r == null)
            {
                return;
            }
            r.enabled = false;
            r.transform.SetParent(transform, false);
            r.gameObject.SetActive(false);
            pool.Push(r);
        }

        private void OnDisable()
        {
            Stop();
        }

        private struct Lcg
        {
            private uint state;

            public Lcg(uint seed)
            {
                state = seed == 0 ? 0x9E3779B9u : seed;
            }

            public float Next()
            {
                state = state * 1664525u + 1013904223u;
                return (state >> 8) / 16777216f;
            }

            public float Range(float a, float b)
            {
                return Mathf.Lerp(a, b, Next());
            }

            public int Int(int a, int bExclusive)
            {
                return Mathf.Min(bExclusive - 1, a + (int)(Next() * (bExclusive - a)));
            }
        }
    }
}
