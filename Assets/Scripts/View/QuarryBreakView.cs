// PURPOSE: "Elmas Kazma" - the obsidian a clean sweep could not take, broken by the pickaxe in a
// language of its own: the stone is left standing when the sweep has gone, a cold resonance wakes
// in it, crystal cracks grow across its face, one short diamond strike lands on their junction,
// the stone is pressed along the strike, and it comes apart into WEDGES OF ITS OWN FACE with a
// little cold dust between them. Then its points go to the score.
//
// THE ORDER IS THE MESSAGE. The sweep's cubes go with the sweep; the obsidian STAYS, as proxies
// raised on the very repaint that emptied its cells (the rules removed it in the same turn), for
// as long as the sweep's wave takes and a breath more. Only then does anything happen to it -
// otherwise the player cannot tell the stone the sweep took from the stone the pickaxe took,
// which was the whole problem: the forced break never reached an explosion list, so the obsidian
// used to vanish under the sweep's wave with nothing of its own.
//
// NOT AN EXPLOSION, RECOLOURED. No radial pop, no fire, no smoke, no flash, no pickaxe sprite:
// the pickaxe is in the MOTION - a diagonal strike, a notch of light at the crack junction, the
// material pressed along the strike axis and swelling across it (a pivot turned to the strike and
// scaled there), and brittle wedges that leave fast and stop fast.
//
// THE VIEW DECIDES NOTHING. Which cubes broke and what they paid are QuarryVisuals, written by
// the joker round the engine's own DestroyCubes; the faces are the board's own (TryCubeLook, which
// keeps a blind round blind). Every shape - junction, strike angle, crack runs, wedges, scatter -
// comes from the report's seed, so a replay at 0.25x is the same break. One to three stones show
// their own value; four or more show ONE total, after the last of them has broken.
//
// Many stones are a wave, not a unison: cracks start 25 ms apart (squeezed so the whole wave stays
// short), past ten the strikes land in three ticks, and the detail per stone steps down so the
// shard and dust budgets hold. Pooled; on the scaled clock; announced through Sounded / Haptic.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class QuarryBreakView : MonoBehaviour
    {
        public static class Style
        {
            public static float Settle = 0.08f;
            public static float Attune = 0.10f;
            public static float AttuneScale = 1.015f;
            public static float Crack = 0.18f;
            public static float Anticipation = 0.06f;
            public static float AnticipationPx = 1.5f;
            public static float Strike = 0.055f;
            public static float StrikeLength = 1.0f;   // cubes
            public static float StrikePx = 5f;
            public static float StrikeAngleSpread = 10f;
            public static float Compression = 0.075f;
            public static float CompressAlong = 0.95f;
            public static float CompressAcross = 1.02f;
            public static float Shatter = 0.18f;
            public static float Stagger = 0.025f;
            public static float StaggerSpan = 0.12f;
            public static int WaveThreshold = 10;
            public static int Waves = 3;
            public static float CrackPx = 2f;
            public static float BigShardPxMin = 6f;
            public static float BigShardPxMax = 14f;
            public static float SmallShardPxMin = 8f;
            public static float SmallShardPxMax = 20f;
            public static float BigShardArea = 0.12f;
            public static float DustLifeMin = 0.10f;
            public static float DustLifeMax = 0.22f;
            public static int ShardBudget = 24;
            public static int DustBudget = 30;
            public static int GlintBudget = 6;
            public static float ValueDelay = 0.06f;
            public static float ValueStamp = 0.12f;
            public static float ScoreHold = 0.15f;
            public static float ScoreTravel = 0.35f;
            public static float ScorePunch = 0.06f;
            public static float ScoreTime = 0.30f;
            public static float GlobalGlintAlpha = 0.06f;
            public static float GlobalGlintTime = 0.10f;
            public static Color ValueInk = new Color(0.97f, 0.97f, 0.93f);
            public static Color ValueShade = new Color(0.08f, 0.14f, 0.24f);
            public static Color ScoreInk = new Color(0.93f, 0.97f, 1f);
            public static Color Seed = new Color(0.78f, 0.92f, 1f);
        }

        public static class Layers
        {
            public static bool ShowProxy = true;
            public static bool ShowAttunement = true;
            public static bool ShowCracks = true;
            public static bool ShowStrike = true;
            public static bool ShowCompression = true;
            public static bool ShowShards = true;
            public static bool ShowDust = true;
            public static bool ShowScore = true;
            public static bool ShowGlobalGlint = true;

            // debug (editor / debug builds only)
            public static bool ShowDiamondTargets;
            public static bool ShowObsidianProxy;
            public static bool ShowCrackPaths;
            public static bool ShowStrikeAxis;
            public static bool ShowCompressionDebug;
            public static bool ShowShardsDebug;
            public static bool ShowCrystalDust;
            public static bool ShowScoreValue;
            public static bool ShowGlobalResonance;

            public static void AllOn()
            {
                ShowProxy = ShowAttunement = ShowCracks = ShowStrike = ShowCompression = true;
                ShowShards = ShowDust = ShowScore = ShowGlobalGlint = true;
                ShowDiamondTargets = ShowObsidianProxy = ShowCrackPaths = ShowStrikeAxis = false;
                ShowCompressionDebug = ShowShardsDebug = ShowCrystalDust = ShowScoreValue = false;
                ShowGlobalResonance = false;
            }
        }

        public static System.Action<string> Sounded;
        public static System.Action<string> Haptic;

        public const string SoundTrigger = "elmas.trigger";
        public const string SoundCrack = "elmas.crack";
        public const string SoundStrike = "elmas.strike";
        public const string SoundShatter = "elmas.shatter";
        public const string SoundReward = "elmas.reward";
        public const string HapticSingle = "elmas.haptic.single";
        public const string HapticMulti = "elmas.haptic.multi";

        /// <summary>A cube's face: the tile and its renderer tint.</summary>
        public delegate bool FaceOf(GridPos cell, Cube cube, out Sprite tile, out Color colour);

        public System.Func<GridPos, Vector2> CellWorld;
        public System.Func<float> CubeSize;
        public System.Func<float> Pixel;
        public System.Func<Vector2> ScoreAnchor;
        public System.Func<Rect> BoardRect;
        public FaceOf Face;

        private const int ProxyOrder = 10;
        private const int CoolOrder = 11;
        private const int CrackOrder = 12;
        private const int ShardOrder = 13;
        private const int StrikeOrder = 14;
        private const int DustOrder = 15;
        private const int GlobalOrder = 16;
        private const int TextOrder = 91;
        private const int SeedOrder = 93;
        private const int DebugOrder = 96;

        private QuarryVisuals report;
        private QuarryVisuals lastBegun;
        private float clock;
        private float preparedAt = -1f;
        private float beginAt = float.PositiveInfinity;
        private readonly List<Stone> stones = new List<Stone>();
        private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        private readonly List<Bit> bits = new List<Bit>();
        private readonly List<Value> values = new List<Value>();
        private readonly HashSet<string> said = new HashSet<string>();
        private readonly List<SpriteRenderer> debugMarks = new List<SpriteRenderer>();
        private SpriteRenderer globalGlint;
        private TextMesh debugText;
        private float firstArrival = -1f;

        public float ScoreScale { get; private set; }
        public float ScoreClaim { get; private set; }
        public Color ScoreInk { get { return Style.ScoreInk; } }

        public bool Busy
        {
            get { return report != null; }
        }

        private sealed class CrackRun
        {
            public List<Vector2> Points = new List<Vector2>();
            public float From;   // 0..1 of the crack phase
            public float Width;
            public bool Main;
            public SpriteRenderer[] Segments;
        }

        private sealed class Shard
        {
            public SpriteRenderer R;
            public SpriteRenderer Edge;
            public Vector2 Centroid;
            public Vector2 Dir;
            public float Distance;
            public float Turn;
            public bool Big;
        }

        private sealed class Stone
        {
            public GridPos Cell;
            public Cube Cube;
            public Sprite Tile;
            public Color Colour;
            public Transform Pivot;
            public SpriteRenderer Proxy;
            public SpriteRenderer Cool;
            public SpriteRenderer Rim;
            public SpriteRenderer Core;
            public SpriteRenderer Streak;
            public Vector2 J;
            public float StrikeAngle;
            public float MainAngle;
            public readonly List<float> Lines = new List<float>();
            public readonly List<CrackRun> Cracks = new List<CrackRun>();
            public readonly List<Shard> Shards = new List<Shard>();
            public float AttuneAt;
            public float CrackAt;
            public float StrikeAt;
            public float ShatterAt;
            public bool Glint;
            public int Dust;
            public int StrikeChips;
            public bool Broke;
            public bool Struck;
            public uint Seed;
        }

        private sealed class Bit
        {
            public SpriteRenderer R;
            public float Born;
            public float Life;
            public Vector2 From;
            public Vector2 To;
            public float Spin;
            public float Size;
            public Color Tint;
            public bool Glint;
        }

        private sealed class Value
        {
            public Vector2 At;
            public int Amount;
            public float BornAt;
            public TextMesh Text;
            public TextMesh Shade;
            public SpriteRenderer Seed;
            public SpriteRenderer[] Trail;
            public bool Arrived;
        }

        private void Awake()
        {
            ScoreScale = 1f;
            globalGlint = Rent(GlobalOrder);
            globalGlint.sprite = QuarryShapes.Strike;
        }

        public bool HasSeen(QuarryVisuals r)
        {
            return r == null || ReferenceEquals(r, lastBegun) || ReferenceEquals(r, report);
        }

        // ================================================================ inputs

        /// <summary>
        /// The repaint that emptied the obsidian: raise the stones as proxies and HOLD them. They
        /// break only once Begin says the sweep has been seen.
        /// </summary>
        public void Prepare(QuarryVisuals r)
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

        /// <summary>The sweep is being drawn now; the pickaxe starts <paramref name="delay"/>
        /// seconds from here.</summary>
        public void Begin(QuarryVisuals r, float delay)
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

        /// <summary>After a reset: this report is not still to come.</summary>
        public void MarkSeen(QuarryVisuals r)
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
            foreach (Stone s in stones)
            {
                ReleaseStone(s);
            }
            stones.Clear();
            foreach (Bit b in bits) { Return(b.R); }
            bits.Clear();
            foreach (Value v in values) { ReleaseValue(v); }
            values.Clear();
            globalGlint.enabled = false;
            report = null;
            beginAt = float.PositiveInfinity;
            ScoreScale = 1f;
            ScoreClaim = 0f;
            HideDebug(0);
        }

        // ================================================================ planning

        private void Build()
        {
            int n = report.Count;
            float size = CubeSize != null ? CubeSize() : 0.9f;
            // A wave out from the board's own middle, the way the sweep went.
            var order = new List<int>();
            for (int i = 0; i < n; i++) { order.Add(i); }
            Vector2 middle = BoardRect != null ? BoardRect().center : Vector2.zero;
            order.Sort((a, b) => (CellWorld(report.Cells[a]) - middle).sqrMagnitude
                .CompareTo((CellWorld(report.Cells[b]) - middle).sqrMagnitude));

            float stagger = n <= 4 ? Style.Stagger : Mathf.Min(Style.Stagger, Style.StaggerSpan / Mathf.Max(1, n - 1));
            int lines = Mathf.Clamp(Style.ShardBudget / Mathf.Max(1, n) / 2, 1, 3);
            int dust = Mathf.Clamp(Style.DustBudget / Mathf.Max(1, n), 1, 6);
            int branches = n <= 4 ? 2 : n < Style.WaveThreshold ? 1 : 0;
            float crackEnd = 0.05f + Style.Crack;

            for (int rank = 0; rank < n; rank++)
            {
                int i = order[rank];
                var s = new Stone { Cell = report.Cells[i], Cube = report.Cubes[i] };
                s.Seed = report.Seed ^ (uint)(i * 2654435761u);
                var rng = new Lcg(s.Seed);
                Sprite tile;
                Color colour;
                if (Face == null || !Face(s.Cell, s.Cube, out tile, out colour))
                {
                    tile = ViewUtil.CubeTile(CubeKind.Obsidian);
                    colour = Color.white;
                }
                s.Tile = tile;
                s.Colour = colour;

                // Shapes: a junction off the middle, a 35-55 degree strike, a main crack that
                // crosses it, and the lines the stone splits along.
                s.J = new Vector2(rng.Range(-0.12f, 0.12f), rng.Range(-0.12f, 0.12f));
                float spread = Style.StrikeAngleSpread;
                float strike = 45f + rng.Range(-spread, spread);
                s.StrikeAngle = (rng.Next() < 0.5f ? strike : 180f - strike) * Mathf.Deg2Rad;
                s.MainAngle = s.StrikeAngle + Mathf.PI * 0.5f + rng.Range(-0.18f, 0.18f);
                s.Lines.Add(s.MainAngle);
                if (lines > 1) { s.Lines.Add(s.MainAngle + rng.Range(62f, 80f) * Mathf.Deg2Rad); }
                if (lines > 2) { s.Lines.Add(s.MainAngle + rng.Range(118f, 134f) * Mathf.Deg2Rad); }

                // Crack runs: both arms of the main crack, one arm of the second line, branches.
                AddRun(s, s.J, s.MainAngle, ArmLength(s.J, s.MainAngle), 0f, 1f, true, ref rng);
                AddRun(s, s.J, s.MainAngle + Mathf.PI, ArmLength(s.J, s.MainAngle + Mathf.PI), 0.05f, 1f, true, ref rng);
                if (s.Lines.Count > 1)
                {
                    AddRun(s, s.J, s.Lines[1], ArmLength(s.J, s.Lines[1]) * 0.6f, 0.35f, 0.7f, false, ref rng);
                }
                for (int b = 0; b < branches; b++)
                {
                    float arm = s.MainAngle + (b == 0 ? 0f : Mathf.PI);
                    float t = rng.Range(0.45f, 0.7f) * ArmLength(s.J, arm);
                    Vector2 start = s.J + new Vector2(Mathf.Cos(arm), Mathf.Sin(arm)) * t;
                    float ang = arm + rng.Range(30f, 50f) * Mathf.Deg2Rad * (rng.Next() < 0.5f ? 1f : -1f);
                    float len = Mathf.Min(rng.Range(0.12f, 0.2f), ArmLength(start, ang));
                    AddRun(s, start, ang, len, 0.7f, 0.55f, false, ref rng);
                }

                s.AttuneAt = rank * stagger;
                s.CrackAt = s.AttuneAt + 0.05f;
                s.Glint = rank < Style.GlintBudget;
                s.Dust = dust;
                s.StrikeChips = n <= 4 ? 3 : 1;
                stones.Add(s);
            }
            // Strikes: each on its own after its crack, or - past the threshold - in three ticks.
            for (int rank = 0; rank < stones.Count; rank++)
            {
                Stone s = stones[rank];
                if (n >= Style.WaveThreshold)
                {
                    int wave = rank * Style.Waves / n;
                    int last = Mathf.Min(n - 1, (wave + 1) * n / Style.Waves - 1);
                    s.StrikeAt = stones[last].AttuneAt + crackEnd + Style.Anticipation;
                }
                else
                {
                    s.StrikeAt = s.AttuneAt + crackEnd + Style.Anticipation;
                }
                s.ShatterAt = s.StrikeAt + 0.015f + Style.Compression;
                BuildRenderers(s, size, rank);
            }
            // The value: per stone for a few, one total after the last break otherwise.
            if (report.Points != 0)
            {
                if (n <= 3)
                {
                    foreach (Stone s in stones)
                    {
                        values.Add(new Value { At = CellWorld(s.Cell), Amount = report.PointsEach, BornAt = s.ShatterAt + Style.ValueDelay });
                    }
                }
                else
                {
                    Vector2 sum = Vector2.zero;
                    float lastBreak = 0f;
                    foreach (Stone s in stones)
                    {
                        sum += CellWorld(s.Cell);
                        lastBreak = Mathf.Max(lastBreak, s.ShatterAt);
                    }
                    values.Add(new Value { At = sum / n, Amount = report.Points, BornAt = lastBreak + Style.ValueDelay });
                }
            }
        }

        private static float ArmLength(Vector2 from, float angle)
        {
            float dx = Mathf.Cos(angle);
            float dy = Mathf.Sin(angle);
            float t = float.MaxValue;
            if (Mathf.Abs(dx) > 0.0001f) { t = Mathf.Min(t, ((dx > 0f ? 0.5f : -0.5f) - from.x) / dx); }
            if (Mathf.Abs(dy) > 0.0001f) { t = Mathf.Min(t, ((dy > 0f ? 0.5f : -0.5f) - from.y) / dy); }
            return Mathf.Max(0f, t * 0.92f);
        }

        /// <summary>A crack as three straight runs with small turns between them - mineral, not
        /// a curve.</summary>
        private static void AddRun(Stone s, Vector2 from, float angle, float length, float start, float width,
            bool main, ref Lcg rng)
        {
            var run = new CrackRun { From = start, Width = width, Main = main };
            run.Points.Add(from);
            Vector2 p = from;
            for (int k = 0; k < 3; k++)
            {
                float a = angle + rng.Range(-0.22f, 0.22f);
                p += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (length / 3f);
                p = new Vector2(Mathf.Clamp(p.x, -0.47f, 0.47f), Mathf.Clamp(p.y, -0.47f, 0.47f));
                run.Points.Add(p);
            }
            s.Cracks.Add(run);
        }

        private void BuildRenderers(Stone s, float size, int rank)
        {
            var pivot = new GameObject("ObsidianPivot");
            pivot.transform.SetParent(transform, false);
            s.Pivot = pivot.transform;
            s.Proxy = Rent(ProxyOrder);
            s.Proxy.transform.SetParent(s.Pivot, false);
            s.Proxy.sprite = s.Tile;
            s.Proxy.color = s.Colour;
            s.Cool = Rent(CoolOrder);
            s.Cool.transform.SetParent(s.Pivot, false);
            s.Cool.sprite = QuarryShapes.Plate;
            s.Rim = Rent(CoolOrder);
            s.Rim.transform.SetParent(s.Pivot, false);
            s.Rim.sprite = QuarryShapes.Rim;
            s.Core = Rent(CrackOrder);
            s.Core.sprite = HazineShapes.Mote;
            s.Streak = Rent(StrikeOrder);
            s.Streak.sprite = QuarryShapes.Strike;
            foreach (CrackRun run in s.Cracks)
            {
                run.Segments = new SpriteRenderer[run.Points.Count - 1];
                for (int k = 0; k < run.Segments.Length; k++)
                {
                    run.Segments[k] = Rent(CrackOrder);
                    run.Segments[k].sprite = QuarryShapes.Crack;
                }
            }
            // The wedges, cut from this stone's own face.
            List<List<Vector2>> pieces = QuarryShapes.SplitSquare(s.J, s.Lines);
            var rng = new Lcg(s.Seed ^ 0x5bd1e995u);
            for (int w = 0; w < pieces.Count; w++)
            {
                List<Vector2> poly = pieces[w];
                long key = ((long)s.Seed << 4) ^ w;
                Sprite cut = QuarryShapes.Wedge(s.Tile, poly, key);
                float area = QuarryShapes.Area(poly);
                Vector2 c = QuarryShapes.Centroid(poly);
                Vector2 dir = c - s.J;
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
                bool big = area >= Style.BigShardArea;
                var shard = new Shard
                {
                    R = Rent(ShardOrder),
                    Centroid = c,
                    Dir = dir,
                    Big = big,
                    Distance = big ? rng.Range(Style.BigShardPxMin, Style.BigShardPxMax)
                        : rng.Range(Style.SmallShardPxMin, Style.SmallShardPxMax),
                    Turn = (big ? rng.Range(5f, 18f) : rng.Range(20f, 70f)) * (rng.Next() < 0.5f ? 1f : -1f)
                };
                if (cut != null)
                {
                    shard.R.sprite = cut;
                    shard.R.color = s.Colour;
                }
                else
                {
                    shard.R.sprite = QuarryShapes.Chip;
                    shard.R.color = Color.white;
                }
                // A cold edge along the cut that faced the junction.
                shard.Edge = Rent(ShardOrder + 1);
                shard.Edge.sprite = QuarryShapes.Crack;
                s.Shards.Add(shard);
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
            // A prepared break that is never begun (no explosion feedback followed) still plays.
            if (float.IsPositiveInfinity(beginAt) && clock - preparedAt > 3f)
            {
                lastBegun = report;
                beginAt = clock;
            }
            float t = clock - beginAt;
            float size = CubeSize != null ? CubeSize() : 0.9f;
            float px = Pixel != null ? Pixel() : 0.01f;
            PaintGlobal(t);
            bool allDone = true;
            foreach (Stone s in stones)
            {
                PaintStone(s, t, size, px);
                if (t < s.ShatterAt + Style.Shatter)
                {
                    allDone = false;
                }
            }
            PaintBits();
            if (!PaintValues(t, size))
            {
                allDone = false;
            }
            foreach (Bit b in bits)
            {
                if (clock - b.Born < b.Life) { allDone = false; }
            }
            PaintDebug(t, size);
            if (allDone && !float.IsInfinity(t))
            {
                Stop();
            }
        }

        private void PaintGlobal(float t)
        {
            bool on = Layers.ShowGlobalGlint && t >= 0f && t <= Style.GlobalGlintTime && BoardRect != null;
            globalGlint.enabled = on;
            if (t >= 0f)
            {
                Say(Sounded, SoundTrigger);
            }
            if (!on)
            {
                return;
            }
            Rect r = BoardRect();
            float k = t / Style.GlobalGlintTime;
            Vector2 from = new Vector2(r.xMin, r.yMin);
            Vector2 to = new Vector2(r.xMax, r.yMax);
            Vector2 dir = (to - from).normalized;
            float length = (to - from).magnitude * 0.45f;
            globalGlint.transform.position = Vector2.Lerp(from, to, k);
            globalGlint.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            globalGlint.transform.localScale = new Vector3(length, r.height * 0.02f, 1f);
            globalGlint.color = new Color(1f, 1f, 1f, Style.GlobalGlintAlpha * Mathf.Sin(k * Mathf.PI));
        }

        private void PaintStone(Stone s, float t, float size, float px)
        {
            Vector2 at = CellWorld(s.Cell);
            Vector2 strikeDir = new Vector2(Mathf.Cos(s.StrikeAngle), Mathf.Sin(s.StrikeAngle));
            bool standing = t < s.ShatterAt || float.IsNegativeInfinity(t);
            float degrees = s.StrikeAngle * Mathf.Rad2Deg;

            // ---- the stone itself (pivot turned to the strike, so a scale there presses ALONG it)
            float attune = Mathf.Clamp01((t - s.AttuneAt) / Style.Attune);
            float wake = t >= s.AttuneAt ? Mathf.Sin(attune * Mathf.PI) : 0f;
            float scale = 1f + (Style.AttuneScale - 1f) * wake;
            Vector2 offset = Vector2.zero;
            float antic = (t - (s.StrikeAt - Style.Anticipation)) / Style.Anticipation;
            if (antic > 0f && t < s.StrikeAt + 0.02f)
            {
                offset = -strikeDir * Style.AnticipationPx * px * Mathf.Clamp01(antic);
            }
            float press = Layers.ShowCompression ? Mathf.Clamp01((t - s.StrikeAt - 0.015f) / Style.Compression) : 0f;
            press = press * press * (3f - 2f * press);
            float along = Mathf.Lerp(1f, Style.CompressAlong, press);
            float across = Mathf.Lerp(1f, Style.CompressAcross, press);
            s.Pivot.position = at + offset;
            s.Pivot.rotation = Quaternion.Euler(0f, 0f, degrees);
            s.Pivot.localScale = new Vector3(along * scale, across * scale, 1f);
            Quaternion undo = Quaternion.Euler(0f, 0f, -degrees);
            s.Proxy.enabled = standing && Layers.ShowProxy;
            // On the pivot, which IS the stone's position: nothing local may move it off.
            s.Proxy.transform.localPosition = Vector3.zero;
            s.Proxy.transform.localRotation = undo;
            s.Proxy.transform.localScale = new Vector3(size, size, 1f);
            s.Proxy.color = Layers.ShowObsidianProxy && DebugAllowed
                ? Color.Lerp(s.Colour, new Color(1f, 0.3f, 1f), 0.35f) : s.Colour;

            // ---- attunement: a cooler, slightly lighter face, a cold rim, a stress core
            bool attuning = Layers.ShowAttunement && standing && t >= s.AttuneAt;
            float cool = attuning ? Mathf.Max(wake, t > s.AttuneAt + Style.Attune ? 0.35f : 0f) : 0f;
            s.Cool.enabled = cool > 0f && Layers.ShowProxy;
            s.Cool.transform.localPosition = Vector3.zero;
            s.Cool.transform.localRotation = undo;
            s.Cool.transform.localScale = new Vector3(size, size, 1f);
            // A pale cold plate at a few per cent: a little lighter and a little less purple.
            s.Cool.color = new Color(0.80f, 0.90f, 1f, 0.06f * cool);
            s.Rim.enabled = cool > 0f;
            s.Rim.transform.localPosition = Vector3.zero;
            s.Rim.transform.localRotation = undo;
            s.Rim.transform.localScale = new Vector3(size, size, 1f);
            s.Rim.color = new Color(QuarryShapes.Ice.r, QuarryShapes.Ice.g, QuarryShapes.Ice.b, 0.30f * cool);
            s.Core.enabled = attuning;
            if (attuning)
            {
                float coreSize = size * Mathf.Lerp(0.10f, 0.20f, attune) * (1f + 0.3f * press);
                s.Core.transform.position = at + offset + s.J * size;
                s.Core.transform.localScale = new Vector3(coreSize, coreSize, 1f);
                s.Core.color = new Color(QuarryShapes.Ice.r, QuarryShapes.Ice.g, QuarryShapes.Ice.b,
                    Mathf.Lerp(0.25f, 0.55f, press));
            }

            // ---- cracks: grown along their runs, brighter before the strike, brightest pressed
            float grow = (t - s.CrackAt) / Style.Crack;
            if (grow > 0f && standing) { Say(Sounded, SoundCrack); }
            float bright = 0.8f + 0.2f * Mathf.Clamp01(antic) + 0.1f * press;
            foreach (CrackRun run in s.Cracks)
            {
                float g = run.From >= 1f ? 0f : Mathf.Clamp01((grow - run.From) / (1f - run.From));
                PaintRun(run, at + offset, size, px, g, bright, standing && Layers.ShowCracks);
            }

            // ---- the strike
            float st = (t - s.StrikeAt) / Style.Strike;
            bool striking = st >= 0f && st <= 1f && Layers.ShowStrike;
            s.Streak.enabled = striking;
            if (st >= 0f && !s.Struck)
            {
                s.Struck = true;
                StrikeHappened(s, at, size, px, strikeDir);
            }
            if (striking)
            {
                float length = Style.StrikeLength * size;
                Vector2 junction = at + s.J * size;
                Vector2 head = junction + strikeDir * length * (st - 0.45f);
                s.Streak.transform.position = head - strikeDir * length * 0.5f;
                s.Streak.transform.rotation = Quaternion.Euler(0f, 0f, degrees);
                s.Streak.transform.localScale = new Vector3(length * Mathf.Lerp(0.6f, 1f, st),
                    Style.StrikePx * px * 2.4f, 1f);
                s.Streak.color = new Color(1f, 1f, 1f, Mathf.Sin(st * Mathf.PI));
            }

            // ---- the break
            if (t >= s.ShatterAt && !s.Broke)
            {
                s.Broke = true;
                Say(Sounded, SoundShatter);
                ScatterDust(s, at, size, px);
            }
            float k = (t - s.ShatterAt) / Style.Shatter;
            foreach (Shard shard in s.Shards)
            {
                PaintShard(shard, s, at, size, px, k);
            }
        }

        private void PaintRun(CrackRun run, Vector2 at, float size, float px, float g, float bright, bool on)
        {
            float total = 0f;
            for (int i = 1; i < run.Points.Count; i++)
            {
                total += (run.Points[i] - run.Points[i - 1]).magnitude;
            }
            float shown = total * g;
            bool debug = Layers.ShowCrackPaths && DebugAllowed;
            for (int i = 0; i < run.Segments.Length; i++)
            {
                SpriteRenderer r = run.Segments[i];
                Vector2 a = run.Points[i];
                Vector2 b = run.Points[i + 1];
                float len = (b - a).magnitude;
                float vis = Mathf.Clamp(shown, 0f, len);
                shown -= len;
                if (!on || vis <= 0.0001f)
                {
                    r.enabled = debug && on;
                    if (!r.enabled) { continue; }
                    vis = len; // the debug view shows the whole path
                }
                Vector2 dir = (b - a) / Mathf.Max(len, 0.0001f);
                Vector2 end = a + dir * vis;
                r.enabled = true;
                r.transform.position = at + (a + end) * 0.5f * size;
                r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                r.transform.localScale = new Vector3(vis * size + px, Style.CrackPx * run.Width * px * 3f, 1f);
                Color c = debug ? (run.Main ? Color.cyan : new Color(0.3f, 0.5f, 1f)) : Color.white * bright;
                c.a = 1f;
                r.color = c;
            }
        }

        private void PaintShard(Shard shard, Stone s, Vector2 at, float size, float px, float k)
        {
            bool on = k >= 0f && k <= 1f && Layers.ShowShards;
            shard.R.enabled = on;
            shard.Edge.enabled = on;
            if (!on)
            {
                return;
            }
            // Out fast, then stop: heavy stone.
            float move = 1f - (1f - k) * (1f - k) * (1f - k);
            float scale = Mathf.Lerp(1f, 0.7f, k);
            float alpha = k < 0.4f ? 1f : 1f - (k - 0.4f) / 0.6f;
            Vector2 centre = at + shard.Centroid * size;
            bool chip = shard.R.sprite == QuarryShapes.Chip;
            Quaternion turn = Quaternion.Euler(0f, 0f, shard.Turn * move);
            // The wedge's pivot is the CUBE centre: place it so the wedge's own centroid - turned
            // and scaled - lands where it should. A fallback chip is pivoted on itself.
            Vector2 pos = (chip ? centre : centre - (Vector2)(turn * (shard.Centroid * size * scale)))
                + shard.Dir * shard.Distance * px * move;
            Transform tr = shard.R.transform;
            tr.position = pos;
            tr.rotation = turn;
            float drawn = chip ? size * 0.35f : size;
            tr.localScale = new Vector3(drawn * scale, drawn * scale, 1f);
            Color c = chip ? Color.white : s.Colour;
            if (Layers.ShowShardsDebug && DebugAllowed)
            {
                c = shard.Big ? new Color(1f, 0.5f, 0.2f) : new Color(0.4f, 1f, 0.4f);
            }
            c.a *= alpha;
            shard.R.color = c;
            // The cold edge: along the cut from the junction toward the shard's centre.
            Vector2 j = pos + (Vector2)(tr.rotation * (s.J * size * scale));
            Vector2 toward = (pos + (Vector2)(tr.rotation * (shard.Centroid * size * scale))) - j;
            float len = toward.magnitude * 1.2f;
            shard.Edge.transform.position = j + toward * 0.5f;
            shard.Edge.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg);
            shard.Edge.transform.localScale = new Vector3(len, px * 3f, 1f);
            shard.Edge.color = new Color(QuarryShapes.Ice.r, QuarryShapes.Ice.g, QuarryShapes.Ice.b, 0.45f * alpha);
        }

        private void StrikeHappened(Stone s, Vector2 at, float size, float px, Vector2 strikeDir)
        {
            // One tick per strike - and past the threshold, where strikes share a moment, one per
            // wave rather than a burst of identical ticks.
            if (said.Add(SoundStrike + "@" + Mathf.RoundToInt(s.StrikeAt * 1000f)) && Sounded != null)
            {
                Sounded(SoundStrike);
            }
            if (stones.Count == 1) { Say(Haptic, HapticSingle); }
            else { Say(Haptic, HapticMulti); }
            Vector2 junction = at + s.J * size;
            if (!Layers.ShowStrike)
            {
                return;
            }
            if (s.Glint)
            {
                AddBit(junction, junction, 0.12f, 0f, size * 0.32f, Color.white, true, HazineShapes.Glint);
            }
            var rng = new Lcg(s.Seed ^ 0x1234567u);
            for (int i = 0; i < s.StrikeChips; i++)
            {
                Vector2 d = (strikeDir + new Vector2(rng.Range(-0.6f, 0.6f), rng.Range(-0.6f, 0.6f))).normalized;
                AddBit(junction, junction + d * rng.Range(8f, 16f) * px, rng.Range(0.10f, 0.16f), rng.Range(-200f, 200f),
                    rng.Range(2f, 3.5f) * px, QuarryShapes.Ice, false, QuarryShapes.Dust);
            }
        }

        private void ScatterDust(Stone s, Vector2 at, float size, float px)
        {
            if (!Layers.ShowDust)
            {
                return;
            }
            var rng = new Lcg(s.Seed ^ 0x9e3779b9u);
            Vector2 junction = at + s.J * size;
            float grow = Layers.ShowCrystalDust && DebugAllowed ? 2.5f : 1f;
            for (int i = 0; i < s.Dust; i++)
            {
                float ang = (i + rng.Range(0f, 0.8f)) / s.Dust * Mathf.PI * 2f;
                var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                AddBit(junction, junction + d * rng.Range(10f, 26f) * px,
                    rng.Range(Style.DustLifeMin, Style.DustLifeMax), rng.Range(-300f, 300f),
                    rng.Range(1f, 4f) * px * 1.6f * grow, Color.Lerp(QuarryShapes.Ice, Color.white, rng.Range(0f, 0.5f)),
                    false, QuarryShapes.Dust);
            }
            // A few dark support chips from the stone itself.
            int chips = stones.Count <= 4 ? 3 : 1;
            for (int i = 0; i < chips; i++)
            {
                float ang = rng.Range(0f, Mathf.PI * 2f);
                var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                AddBit(junction, junction + d * rng.Range(8f, 20f) * px, rng.Range(0.14f, 0.2f), rng.Range(-160f, 160f),
                    size * 0.12f, Color.white, false, QuarryShapes.Chip);
            }
        }

        private void AddBit(Vector2 from, Vector2 to, float life, float spin, float size, Color tint, bool glint,
            Sprite sprite)
        {
            SpriteRenderer r = Rent(glint ? StrikeOrder + 1 : DustOrder);
            r.sprite = sprite;
            bits.Add(new Bit { R = r, Born = clock, Life = life, From = from, To = to, Spin = spin, Size = size, Tint = tint, Glint = glint });
        }

        private void PaintBits()
        {
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                Bit b = bits[i];
                float k = (clock - b.Born) / b.Life;
                if (k > 1f)
                {
                    Return(b.R);
                    bits.RemoveAt(i);
                    continue;
                }
                b.R.enabled = true;
                float move = 1f - (1f - k) * (1f - k);
                b.R.transform.position = Vector2.Lerp(b.From, b.To, move);
                b.R.transform.rotation = Quaternion.Euler(0f, 0f, b.Spin * k);
                float s = b.Glint ? b.Size * Mathf.Sin(k * Mathf.PI) : b.Size;
                b.R.transform.localScale = new Vector3(s, s, 1f);
                Color c = b.Tint;
                c.a *= b.Glint ? 1f : 1f - k * k;
                b.R.color = c;
            }
        }

        // ---- the value -------------------------------------------------------------------------

        /// <summary>False while a value is still on its way.</summary>
        private bool PaintValues(float t, float size)
        {
            ScoreScale = 1f;
            ScoreClaim = 0f;
            bool done = true;
            foreach (Value v in values)
            {
                float age = t - v.BornAt;
                if (!Layers.ShowScore || float.IsInfinity(t) || age < 0f)
                {
                    HideValue(v);
                    if (Layers.ShowScore) { done = false; }
                    continue;
                }
                if (v.Text == null)
                {
                    v.Shade = ViewUtil.MakeText3D(transform, "QuarryValueShade", Vector2.zero, string.Empty, 64, 0.03f,
                        Style.ValueShade, TextOrder - 1, TextAnchor.MiddleCenter);
                    v.Text = ViewUtil.MakeText3D(transform, "QuarryValue", Vector2.zero, string.Empty, 64, 0.03f,
                        Style.ValueInk, TextOrder, TextAnchor.MiddleCenter);
                    v.Seed = Rent(SeedOrder);
                    v.Seed.sprite = ChallengeShapes.Seed;
                    v.Trail = new SpriteRenderer[3];
                    for (int i = 0; i < 3; i++)
                    {
                        v.Trail[i] = Rent(SeedOrder - 1);
                        v.Trail[i].sprite = HazineShapes.Mote;
                    }
                }
                Vector2 at = v.At + new Vector2(0f, 0.15f * size);
                float hold = Style.ValueStamp + Style.ScoreHold;
                // THE NUMBER IS CORE'S.
                string text = (v.Amount < 0 ? "-" : "+") + Mathf.Abs(v.Amount);
                bool showText = age < hold + 0.08f;
                v.Text.gameObject.SetActive(showText);
                v.Shade.gameObject.SetActive(showText);
                if (showText)
                {
                    float s = age < Style.ValueStamp * 0.6f
                        ? Mathf.Lerp(0.70f, 1.08f, age / (Style.ValueStamp * 0.6f))
                        : Mathf.Lerp(1.08f, 1f, Mathf.Clamp01((age - Style.ValueStamp * 0.6f) / (Style.ValueStamp * 0.4f)));
                    float fold = Mathf.Clamp01((age - hold) / 0.08f);
                    s *= 1f - 0.5f * fold;
                    float h = size * (values.Count > 1 ? 0.30f : 0.36f) * s;
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
                for (int i = 0; i < 3; i++) { v.Trail[i].enabled = flying; }
                if (flight < 1f)
                {
                    done = false;
                }
                if (flying)
                {
                    float k = 1f - (1f - flight) * (1f - flight);
                    v.Seed.transform.position = Arc(at, target, k);
                    float ss = size * 0.26f * Mathf.Lerp(1f, 0.8f, k);
                    v.Seed.transform.localScale = new Vector3(ss, ss, 1f);
                    v.Seed.color = Style.Seed;
                    for (int i = 0; i < 3; i++)
                    {
                        float back = Mathf.Max(0f, k - 0.04f * (i + 1));
                        v.Trail[i].transform.position = Arc(at, target, back);
                        float ts = size * (0.13f - 0.03f * i);
                        v.Trail[i].transform.localScale = new Vector3(ts, ts, 1f);
                        v.Trail[i].color = Color.Lerp(Style.Seed, Color.white, i / 2f) * new Color(1f, 1f, 1f, 0.45f - 0.12f * i);
                    }
                }
                if (flight > 1f && !v.Arrived)
                {
                    v.Arrived = true;
                    Say(Sounded, SoundReward);
                    if (firstArrival < 0f) { firstArrival = clock; }
                }
            }
            if (firstArrival >= 0f)
            {
                float a = (clock - firstArrival) / Style.ScoreTime;
                if (a <= 1f)
                {
                    // 1 -> 1.06 -> 1, a breath of ice-white on the score's own colour.
                    ScoreScale = 1f + Style.ScorePunch * Mathf.Sin(a * Mathf.PI) * (report != null && report.Points < 0 ? -0.6f : 1f);
                    ScoreClaim = 1f - a;
                    done = false;
                }
            }
            return done;
        }

        private static Vector2 Arc(Vector2 a, Vector2 b, float k)
        {
            Vector2 c = (a + b) * 0.5f + new Vector2(0f, Mathf.Min((b - a).magnitude * 0.25f, 1.2f));
            float u = 1f - k;
            return u * u * a + 2f * u * k * c + k * k * b;
        }

        private void HideValue(Value v)
        {
            if (v.Text != null)
            {
                v.Text.gameObject.SetActive(false);
                v.Shade.gameObject.SetActive(false);
                v.Seed.enabled = false;
                for (int i = 0; i < 3; i++) { v.Trail[i].enabled = false; }
            }
        }

        private void ReleaseValue(Value v)
        {
            if (v.Text != null)
            {
                Destroy(v.Text.gameObject);
                Destroy(v.Shade.gameObject);
                Return(v.Seed);
                for (int i = 0; i < 3; i++) { Return(v.Trail[i]); }
            }
        }

        // ---- debug -----------------------------------------------------------------------------

        private static bool DebugAllowed
        {
            get { return Application.isEditor || Debug.isDebugBuild; }
        }

        private void PaintDebug(float t, float size)
        {
            int used = 0;
            if (DebugAllowed)
            {
                foreach (Stone s in stones)
                {
                    Vector2 at = CellWorld(s.Cell);
                    if (Layers.ShowDiamondTargets)
                    {
                        Mark(ref used, at, size, HazineShapes.Frame, new Color(0.5f, 0.9f, 1f, 0.9f), 0f);
                    }
                    if (Layers.ShowStrikeAxis)
                    {
                        Vector2 dir = new Vector2(Mathf.Cos(s.StrikeAngle), Mathf.Sin(s.StrikeAngle));
                        Vector2 j = at + s.J * size;
                        for (int i = -4; i <= 4; i++)
                        {
                            Mark(ref used, j + dir * (i * size * 0.12f), size * (i == 4 ? 0.12f : 0.05f),
                                HazineShapes.Mote, new Color(1f, 0.9f, 0.2f), 0f);
                        }
                    }
                }
                if (Layers.ShowGlobalResonance && BoardRect != null)
                {
                    Rect r = BoardRect();
                    for (int i = 0; i <= 10; i++)
                    {
                        Mark(ref used, Vector2.Lerp(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMax), i / 10f),
                            size * 0.06f, HazineShapes.Mote, new Color(0.7f, 0.9f, 1f), 0f);
                    }
                }
            }
            HideDebug(used);
            bool label = DebugAllowed && (Layers.ShowCompressionDebug || Layers.ShowScoreValue) && stones.Count > 0;
            if (label)
            {
                if (debugText == null)
                {
                    debugText = ViewUtil.MakeText3D(transform, "QuarryDebug", Vector2.zero, string.Empty, 40, 0.03f,
                        Color.white, DebugOrder, TextAnchor.UpperCenter);
                }
                debugText.gameObject.SetActive(true);
                Stone s = stones[0];
                float press = Mathf.Clamp01((t - s.StrikeAt - 0.015f) / Style.Compression);
                var sb = new System.Text.StringBuilder();
                if (Layers.ShowCompressionDebug)
                {
                    sb.Append("press ").Append(press.ToString("0.00")).Append("  along ")
                        .Append(Mathf.Lerp(1f, Style.CompressAlong, press).ToString("0.000")).Append('\n');
                }
                if (Layers.ShowScoreValue && report != null)
                {
                    sb.Append("stones ").Append(report.Count).Append("  points ").Append(report.Points)
                        .Append("  each ").Append(report.PointsEach).Append(report.Count <= 3 ? "  (per stone)" : "  (one total)");
                }
                debugText.text = sb.ToString();
                debugText.characterSize = size * 0.16f * 10f / 40f;
                debugText.transform.position = CellWorld(s.Cell) - new Vector2(0f, 0.6f * size);
            }
            else if (debugText != null)
            {
                debugText.gameObject.SetActive(false);
            }
        }

        private void Mark(ref int used, Vector2 at, float size, Sprite sprite, Color c, float degrees)
        {
            if (used >= debugMarks.Count)
            {
                debugMarks.Add(Rent(DebugOrder));
            }
            SpriteRenderer r = debugMarks[used++];
            r.enabled = true;
            r.sprite = sprite;
            r.transform.position = at;
            r.transform.rotation = Quaternion.Euler(0f, 0f, degrees);
            r.transform.localScale = new Vector3(size, size, 1f);
            r.color = c;
        }

        private void HideDebug(int from)
        {
            for (int i = from; i < debugMarks.Count; i++)
            {
                debugMarks[i].enabled = false;
            }
        }

        // ---- housekeeping ----------------------------------------------------------------------

        private void ReleaseStone(Stone s)
        {
            Return(s.Proxy);
            Return(s.Cool);
            Return(s.Rim);
            Return(s.Core);
            Return(s.Streak);
            foreach (CrackRun run in s.Cracks)
            {
                if (run.Segments == null) { continue; }
                foreach (SpriteRenderer r in run.Segments) { Return(r); }
            }
            foreach (Shard shard in s.Shards)
            {
                Return(shard.R);
                Return(shard.Edge);
            }
            if (s.Pivot != null)
            {
                Destroy(s.Pivot.gameObject);
            }
        }

        private void Say(System.Action<string> channel, string what)
        {
            if (said.Add(what) && channel != null)
            {
                channel(what);
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
                var go = new GameObject("QuarryPart");
                r = go.AddComponent<SpriteRenderer>();
            }
            r.transform.SetParent(transform, false);
            r.sortingOrder = order;
            r.enabled = false;
            r.color = Color.white;
            // ALL THREE are reset. A pooled renderer keeps the LOCAL position of its last use (a
            // shard or a speck placed in world space), and one re-parented under a stone's pivot
            // then sits that far from the stone - which is how the proxies once turned up off the
            // board while their cracks were still on it.
            r.transform.localPosition = Vector3.zero;
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = Vector3.one;
            return r;
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

        /// <summary>Deterministic per report; never UnityEngine.Random.</summary>
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
        }
    }
}
