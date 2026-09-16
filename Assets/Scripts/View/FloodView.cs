// PURPOSE: "Taşkın" - the water on the board OVERFLOWS into the cubes beside it and drowns them into
// water. Not a tint, and IN THIS ORDER, which is the whole read: first the source water itself SWELLS
// AND RISES - the water cube grows a tenth, lifts off its cell over a darker contact shadow, and piles
// into a mound on each edge facing a real target; only once it has risen does it spill over the border
// as a liquid tongue; only when the tongue LANDS does the target begin to turn - a film crossing it
// from that side while the cube under it refracts, loses its colour and contrast and runs - and the
// source sinks back as the water tile underneath takes the target and settles with a broken ripple.
// The first pass started the target 180 ms in with a few pixels of swell, and it read as the cube
// simply turning to water: the rise has to be SEEN before anything else happens.
//
// ONE TARGET, ONE TRANSFORMATION. A cube reached from two or three sides is not two or three
// animations stacked: it is one proxy on the FloodFilm shader whose film comes in from every side
// the report names (a progress per side, one combined mask), so the films meet in the middle. The
// sides come from SpreadVisuals.Targets[i].From - the sources that really reached it - and never
// from the neighbourhood the view can see.
//
// THE RULE IS ONE RING, AND THE PICTURE KEEPS IT. Only SpreadVisuals.Sources spill; a cube that has
// just become water only settles, it never spills on. A cube the rules did not convert is not
// touched, whatever it is.
//
// THE BOARD STANDS BACK (BoardView.HoldCells) for exactly as long as this runs, as it does for
// "Yangın": Core converted the cells the moment the joker fired, and the water already painted there
// would answer the question the animation is asking. What is drawn over a held cell is the OLD face
// on the film shader, and UNDER it the cell's real water - the water tile on the water's own warp
// material - rising as the old face goes; when the cells are released the board draws that same
// water, so the hand-off is not a swap. Taşkın is used from the joker bar, not in a turn, and the
// joker does not settle the water it makes (the next placement does), so a flood never overlaps a
// water fall.
//
// Nothing here scores, flashes or shakes: this is board manipulation, not a reward. Pooled; on the
// scaled clock; announced through Sounded / Haptic a few voices at most.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class FloodView : MonoBehaviour
    {
        public static class Style
        {
            /// <summary>The source RISING: its own water grows and lifts off the cell.</summary>
            public static float Pressure = 0.30f;
            public static float RiseScale = 1.12f;
            public static float RiseLift = 0.06f;           // cells
            public static float RiseShadow = 0.35f;
            public static float PressureBright = 0.10f;
            public static float SwellFrom = 0.14f;
            public static float Swell = 0.20f;
            public static float SwellCells = 0.20f;         // how far the mound piles past the edge
            public static float TongueFrom = 0.33f;
            public static float Tongue = 0.20f;
            public static float TongueWidth = 0.30f;        // cells
            public static float TongueRoot = 0.30f;         // cells from the source centre
            public static float TongueReach = 0.32f;        // cells into the target
            /// <summary>When the tongue LANDS - and the target does not begin to turn before it.
            /// </summary>
            public static float Arrival = 0.52f;
            public static float Contact = 0.07f;
            public static float Film = 0.20f;
            public static float SubmergeFrom = 0.08f;
            public static float Submerge = 0.20f;
            public static float LiquefyFrom = 0.14f;
            public static float Liquefy = 0.18f;
            public static float WaterFrom = 0.12f;
            public static float Water = 0.20f;
            public static float IdentityFrom = 0.18f;
            public static float Identity = 0.14f;
            public static float RippleFrom = 0.28f;
            public static float Ripple = 0.16f;
            public static float RippleAlpha = 0.18f;
            public static float Rebound = 0.22f;
            public static float SourceStagger = 0.025f;
            public static int MaxDroplets = 24;
            public static Color FilmColour = new Color(0.45f, 0.82f, 0.88f);
            public static Color FrontColour = new Color(0.82f, 0.97f, 0.98f);
        }

        public static class Layers
        {
            public static bool ShowPressure = true;
            public static bool ShowSwell = true;
            public static bool ShowTongue = true;
            public static bool ShowContact = true;
            public static bool ShowFilm = true;
            public static bool ShowRefractionLayer = true;
            public static bool ShowSubmerge = true;
            public static bool ShowLiquefy = true;
            public static bool ShowCrossfade = true;
            public static bool ShowRippleLayer = true;
            public static bool ShowDropletsLayer = true;

            // debug (editor / debug builds only)
            public static bool ShowFloodSources;
            public static bool ShowFloodTargets;
            public static bool ShowIncomingDirections;
            public static bool ShowLiquidTongues;
            public static bool ShowWaterFilmMask;
            public static bool ShowRefraction;
            public static bool ShowOldCubeProxy;
            public static bool ShowWaterRenderer;
            public static bool ShowRipple;
            public static bool ShowDroplets;
            public static bool ShowSourceStagger;

            public static void AllOn()
            {
                ShowPressure = ShowSwell = ShowTongue = ShowContact = ShowFilm = true;
                ShowRefractionLayer = ShowSubmerge = ShowLiquefy = ShowCrossfade = true;
                ShowRippleLayer = ShowDropletsLayer = true;
                ShowFloodSources = ShowFloodTargets = ShowIncomingDirections = ShowLiquidTongues = false;
                ShowWaterFilmMask = ShowRefraction = ShowOldCubeProxy = ShowWaterRenderer = false;
                ShowRipple = ShowDroplets = ShowSourceStagger = false;
            }
        }

        public static System.Action<string> Sounded;
        public static System.Action<string> Haptic;

        public const string SoundTrigger = "taskin.trigger";
        public const string SoundSourcePressure = "taskin.source.pressure";
        public const string SoundSpill = "taskin.spill";
        public const string SoundContact = "taskin.contact";
        public const string SoundConvert = "taskin.convert";
        public const string SoundSettle = "taskin.settle";
        public const string HapticContact = "taskin.haptic.contact";
        private const int MaxVoices = 4;

        public delegate bool FaceOf(GridPos cell, Cube cube, out Sprite tile, out Color colour);

        public System.Func<GridPos, Vector2> CellWorld;
        public System.Func<float> CubeSize;
        public System.Func<float> CellSize;
        public System.Func<float> Pixel;
        public System.Action<IEnumerable<GridPos>> Hold;
        public System.Action<IEnumerable<GridPos>> Release;
        public FaceOf Face;

        private const int WaterOrder = 5;
        private const int ProxyOrder = 6;
        private const int ShadowOrder = 7;
        private const int RaisedOrder = 8;
        private const int SourceOrder = 9;
        private const int TongueOrder = 10;
        private const int RippleOrder = 11;
        private const int DropletOrder = 12;
        private const int DebugOrder = 96;

        private static Material filmMaterial;
        private static bool filmShaderMissing;
        private static readonly int FilmId = Shader.PropertyToID("_Film");
        private static readonly int SubmergeId = Shader.PropertyToID("_Submerge");
        private static readonly int LiquefyId = Shader.PropertyToID("_Liquefy");
        private static readonly int ClockId = Shader.PropertyToID("_Clock");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int PxId = Shader.PropertyToID("_Px");
        private static readonly int UvPerLocalId = Shader.PropertyToID("_UvPerLocal");
        private static readonly int WaterId = Shader.PropertyToID("_Water");
        private static readonly int EdgeId = Shader.PropertyToID("_Edge");

        private SpreadVisuals report;
        private SpreadVisuals lastPlayed;
        private float clock = -1f;
        private readonly List<Source> sources = new List<Source>();
        private readonly List<Target> targets = new List<Target>();
        private readonly List<Spill> spills = new List<Spill>();
        private readonly List<Bit> bits = new List<Bit>();
        private readonly List<GridPos> held = new List<GridPos>();
        private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        private readonly HashSet<string> said = new HashSet<string>();
        private readonly List<SpriteRenderer> debugMarks = new List<SpriteRenderer>();
        private readonly Dictionary<string, int> voices = new Dictionary<string, int>();
        private MaterialPropertyBlock block;
        private TextMesh debugText;
        private float end;

        public bool Busy
        {
            get { return report != null; }
        }

        private sealed class Source
        {
            public GridPos Cell;
            public float Start;
            public SpriteRenderer Plate;
            public SpriteRenderer Raised;
            public SpriteRenderer Shadow;
            public float Amount;   // 0..1 how risen, this frame
            public float Lift;     // world units, this frame
            public float Scale = 1f;
            public readonly List<Vector2> Facing = new List<Vector2>();
        }

        /// <summary>One source spilling into one target.</summary>
        private sealed class Spill
        {
            public Source From;
            public Target To;
            public Vector2 Dir;           // source -> target, unit grid step
            public SpriteRenderer Highlight;
            public SpriteRenderer Swell;
            public SpriteRenderer Tongue;
            public SpriteRenderer Streak;
            public bool Contacted;
            public int ContactDrops;
        }

        private sealed class Target
        {
            public GridPos Cell;
            public SpriteRenderer Water;
            public SpriteRenderer Proxy;
            public Color Colour;
            public Vector4 Arrival = new Vector4(-1f, -1f, -1f, -1f); // L R B T, source-clock absolute
            public float First = float.MaxValue;
            public float Seed;
            public Vector2 UvPerLocal = Vector2.one;
            public readonly List<SpriteRenderer> Ripples = new List<SpriteRenderer>();
            public int SettleDrops;
            public bool Settled;
            public bool Converted;
        }

        private sealed class Bit
        {
            public SpriteRenderer R;
            public float Born;
            public float Life;
            public Vector2 From;
            public Vector2 To;
            public float Size;
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        private static Material FilmMaterial
        {
            get
            {
                if (filmMaterial == null && !filmShaderMissing)
                {
                    Shader shader = Shader.Find("ProjectBlock/FloodFilm");
                    if (shader == null)
                    {
                        shader = Resources.Load<Shader>("Shaders/FloodFilm");
                    }
                    if (shader == null)
                    {
                        filmShaderMissing = true;
                        Debug.LogWarning("[block_bonk] FloodFilm shader missing - the old cube simply fades under the water");
                    }
                    else
                    {
                        filmMaterial = new Material(shader);
                    }
                }
                return filmMaterial;
            }
        }

        // ================================================================ input

        public void Play(SpreadVisuals r)
        {
            if (r == null || !r.Any || r.Kind != CubeKind.Water || ReferenceEquals(r, lastPlayed))
            {
                return;
            }
            Stop();
            lastPlayed = r;
            report = r;
            said.Clear();
            voices.Clear();
            clock = 0f;
            Build();
            Tick(0f);
        }

        public void Forget()
        {
            lastPlayed = null;
        }

        public void MarkPlayed(SpreadVisuals r)
        {
            if (r != null)
            {
                lastPlayed = r;
            }
        }

        public void Stop()
        {
            if (held.Count > 0 && Release != null)
            {
                Release(held);
            }
            held.Clear();
            foreach (Source s in sources) { Return(s.Plate); Return(s.Raised); Return(s.Shadow); }
            sources.Clear();
            foreach (Spill sp in spills)
            {
                Return(sp.Highlight);
                Return(sp.Swell);
                Return(sp.Tongue);
                Return(sp.Streak);
            }
            spills.Clear();
            foreach (Target t in targets)
            {
                Return(t.Water);
                Return(t.Proxy);
                foreach (SpriteRenderer r in t.Ripples) { Return(r); }
            }
            targets.Clear();
            foreach (Bit b in bits) { Return(b.R); }
            bits.Clear();
            for (int i = 0; i < debugMarks.Count; i++) { debugMarks[i].enabled = false; }
            if (debugText != null) { debugText.gameObject.SetActive(false); }
            report = null;
            clock = -1f;
        }

        // ================================================================ planning

        private void Build()
        {
            // Sources: near-parallel, a little apart - never all on one frame, never a hard wave.
            var order = new List<GridPos>(report.Sources);
            order.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            var byCell = new Dictionary<GridPos, Source>();
            for (int i = 0; i < order.Count; i++)
            {
                uint h = Hash(order[i]);
                var s = new Source
                {
                    Cell = order[i],
                    Start = (i % 4) * Style.SourceStagger + (h % 1000) / 1000f * 0.008f
                };
                byCell[s.Cell] = s;
            }

            int n = report.Targets.Count;
            int contactDrops = n <= 4 ? 3 : n <= 10 ? 1 : 0;
            int settleDrops = n <= 4 ? 3 : n <= 10 ? 2 : 1;
            int dropBudget = Style.MaxDroplets;
            Sprite waterTile = ViewUtil.CubeTile(CubeKind.Water);
            Material waterMaterial = ViewUtil.TileMaterial(waterTile);

            var used = new HashSet<GridPos>();
            for (int i = 0; i < n; i++)
            {
                SpreadIgnition entry = report.Targets[i];
                var t = new Target { Cell = entry.Cell, Seed = (Hash(entry.Cell) % 628) / 100f };
                Sprite tile;
                Color colour;
                if (Face == null || !Face(entry.Cell, entry.Was, out tile, out colour))
                {
                    tile = ViewUtil.CubeTile(entry.Was.Kind);
                    colour = Color.white;
                }
                t.Colour = colour;
                t.Water = Rent(WaterOrder);
                t.Water.sprite = waterTile;
                if (waterMaterial != null)
                {
                    t.Water.sharedMaterial = waterMaterial;
                }
                t.Proxy = Rent(ProxyOrder);
                t.Proxy.sprite = tile;
                t.Proxy.color = colour;
                if (FilmMaterial != null && tile != null)
                {
                    t.Proxy.sharedMaterial = FilmMaterial;
                    float ppu = tile.pixelsPerUnit;
                    t.UvPerLocal = new Vector2(ppu / tile.texture.width, ppu / tile.texture.height);
                }
                int ripples = n <= 10 ? 2 : 1;
                for (int k = 0; k < ripples; k++)
                {
                    SpriteRenderer rr = Rent(RippleOrder);
                    rr.sprite = FloodShapes.Ripple(k);
                    t.Ripples.Add(rr);
                }
                t.SettleDrops = Mathf.Min(settleDrops, dropBudget);
                dropBudget -= t.SettleDrops;
                targets.Add(t);
                held.Add(entry.Cell);

                foreach (GridPos from in entry.From)
                {
                    Source s;
                    if (!byCell.TryGetValue(from, out s))
                    {
                        continue; // the report only names sources; nothing else spills
                    }
                    used.Add(from);
                    var dir = new Vector2(entry.Cell.X - from.X, entry.Cell.Y - from.Y);
                    var spill = new Spill { From = s, To = t, Dir = dir };
                    spill.Highlight = Rent(SourceOrder);
                    spill.Highlight.sprite = QuarryShapes.Crack;
                    spill.Swell = Rent(SourceOrder);
                    spill.Swell.sprite = FloodShapes.Swell;
                    spill.Tongue = Rent(TongueOrder);
                    spill.Tongue.sprite = FloodShapes.Tongue;
                    spill.Streak = Rent(TongueOrder + 1);
                    spill.Streak.sprite = HazineShapes.Mote;
                    spill.ContactDrops = Mathf.Min(contactDrops, dropBudget);
                    dropBudget -= spill.ContactDrops;
                    spills.Add(spill);
                    s.Facing.Add(dir);
                    // The side of the TARGET the water comes in on.
                    float arrival = s.Start + Style.Arrival;
                    if (dir.x > 0f) { t.Arrival.x = arrival; }       // source on the left
                    else if (dir.x < 0f) { t.Arrival.y = arrival; }  // source on the right
                    else if (dir.y > 0f) { t.Arrival.z = arrival; }  // source below
                    else { t.Arrival.w = arrival; }                   // source above
                    t.First = Mathf.Min(t.First, arrival);
                }
                if (t.First == float.MaxValue)
                {
                    t.First = Style.Arrival;
                }
            }
            // Only sources that really spill are drawn as spilling.
            foreach (KeyValuePair<GridPos, Source> kv in byCell)
            {
                if (used.Contains(kv.Key))
                {
                    kv.Value.Plate = Rent(SourceOrder);
                    kv.Value.Plate.sprite = QuarryShapes.Plate;
                    // The source's OWN water, raised: the same tile on the same warp material as the
                    // board's, so when it sinks back to scale 1 and goes it is the board's cube again.
                    kv.Value.Raised = Rent(RaisedOrder);
                    kv.Value.Raised.sprite = waterTile;
                    if (waterMaterial != null)
                    {
                        kv.Value.Raised.sharedMaterial = waterMaterial;
                    }
                    kv.Value.Shadow = Rent(ShadowOrder);
                    kv.Value.Shadow.sprite = QuarryShapes.Plate;
                    sources.Add(kv.Value);
                }
            }
            end = 0f;
            foreach (Target t in targets)
            {
                end = Mathf.Max(end, t.First + Style.RippleFrom + Style.Ripple + 0.02f);
            }
            if (Hold != null)
            {
                Hold(held);
            }
        }

        private static uint Hash(GridPos p)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)(p.X * 73856093)) * 16777619u;
                h = (h ^ (uint)(p.Y * 19349663)) * 16777619u;
                return h;
            }
        }

        // ================================================================ clock

        private void Update()
        {
            if (report != null)
            {
                Tick(Time.deltaTime);
            }
        }

        private void Tick(float dt)
        {
            clock += dt;
            float cube = CubeSize != null ? CubeSize() : 0.9f;
            float cell = CellSize != null ? CellSize() : cube;
            float px = Pixel != null ? Pixel() : 0.01f;
            Say(Sounded, SoundTrigger);
            foreach (Source s in sources) { PaintSource(s, cube); }
            foreach (Spill sp in spills) { PaintSpill(sp, cube, cell, px); }
            foreach (Target t in targets) { PaintTarget(t, cube, cell, px); }
            PaintBits();
            PaintDebug(cell);
            if (clock > end && bits.Count == 0)
            {
                Stop();
            }
        }

        private void PaintSource(Source s, float cube)
        {
            float k = Mathf.Clamp01((clock - s.Start) / Style.Pressure);
            if (clock >= s.Start) { Say(Sounded, SoundSourcePressure); }
            // RISE: fast, a touch past the top, settling to its height - the water heaving up.
            float rise = k < 1f ? 1f - (1f - k) * (1f - k) * (1f - k) + 0.08f * Mathf.Sin(k * Mathf.PI) : 1f;
            // SINK: once the last of its tongues has landed, back down with a small undershoot.
            float sinkFrom = s.Start + Style.Arrival + 0.05f;
            float sk = Mathf.Clamp01((clock - sinkFrom) / Style.Rebound);
            float sink = sk * sk * (3f - 2f * sk);
            float amount = clock < s.Start ? 0f : rise * (1f - sink);
            float undershoot = sk > 0.6f && sk < 1f ? -0.02f * Mathf.Sin((sk - 0.6f) / 0.4f * Mathf.PI) : 0f;
            s.Amount = Layers.ShowPressure ? amount : 0f;
            s.Scale = 1f + (Style.RiseScale - 1f) * s.Amount + (Layers.ShowPressure ? undershoot : 0f);
            s.Lift = Style.RiseLift * cube * s.Amount;
            Vector2 at = CellWorld(s.Cell);
            bool on = Layers.ShowPressure && clock >= s.Start && sk < 1f;
            s.Raised.enabled = on;
            s.Shadow.enabled = on && s.Amount > 0.01f;
            s.Plate.enabled = on && s.Amount > 0.01f;
            if (!on)
            {
                return;
            }
            // The raised water covers the board's own cube exactly when it is back at scale 1.
            s.Raised.transform.position = at + new Vector2(0f, s.Lift);
            s.Raised.transform.localScale = new Vector3(cube * s.Scale, cube * s.Scale, 1f);
            s.Raised.color = Color.white;
            // A contact shadow under it, spreading as it lifts: that is what reads as RISING.
            s.Shadow.transform.position = at + new Vector2(cube * 0.02f, -cube * 0.03f * s.Amount);
            s.Shadow.transform.localScale = new Vector3(cube * (1f + 0.06f * s.Amount), cube * (1f + 0.06f * s.Amount), 1f);
            s.Shadow.color = new Color(0f, 0.03f, 0.06f, Style.RiseShadow * s.Amount);
            // And a little brighter as it heaves - never a full-rim glow.
            s.Plate.transform.position = s.Raised.transform.position;
            s.Plate.transform.localScale = s.Raised.transform.localScale;
            Color c = FloodShapes.Pale;
            c.a = Style.PressureBright * s.Amount;
            s.Plate.color = c;
        }

        private void PaintSpill(Spill sp, float cube, float cell, float px)
        {
            float a = clock - sp.From.Start;
            // The spill leaves the RAISED water: its edge moves out and up with it.
            Vector2 src = CellWorld(sp.From.Cell) + new Vector2(0f, sp.From.Lift);
            float grown = cube * sp.From.Scale;
            float angle = Mathf.Atan2(sp.Dir.y, sp.Dir.x) * Mathf.Rad2Deg;
            float rebound = Mathf.Clamp01((clock - (sp.To.First + 0.05f)) / Style.Rebound);

            // ---- the highlight on the edge that faces this target, and only that edge
            float hk = Mathf.Clamp01(a / Style.Pressure) * (1f - rebound);
            bool hOn = Layers.ShowPressure && hk > 0f;
            sp.Highlight.enabled = hOn;
            if (hOn)
            {
                sp.Highlight.transform.position = src + sp.Dir * (grown * 0.47f);
                sp.Highlight.transform.rotation = Quaternion.Euler(0f, 0f, angle + 90f);
                sp.Highlight.transform.localScale = new Vector3(cube * 0.8f, px * 3f * 3f, 1f);
                sp.Highlight.color = new Color(0.75f, 0.95f, 1f, 0.55f * hk);
            }

            // ---- the swell: the water piling up against that edge
            float sk = Mathf.Clamp01((a - Style.SwellFrom) / Style.Swell);
            float swell = Mathf.Sin(Mathf.Min(sk, 1f) * Mathf.PI * 0.5f) * (1f - rebound);
            bool sOn = Layers.ShowSwell && swell > 0f;
            sp.Swell.enabled = sOn;
            if (sOn)
            {
                // A MOUND of water piling up against the edge and past it - big enough to see.
                float bulge = Style.SwellCells * cell * swell;
                sp.Swell.transform.position = src + sp.Dir * (grown * 0.45f + bulge * 0.5f);
                sp.Swell.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                sp.Swell.transform.localScale = new Vector3(cube * 0.12f + bulge * 2f, grown * 0.62f, 1f);
                sp.Swell.color = new Color(1f, 1f, 1f, 0.85f * swell);
                if (sk >= 1f && Layers.ShowDropletsLayer && Say(null, "swell-drop" + spills.IndexOf(sp)))
                {
                    Drop(src + sp.Dir * (grown * 0.5f + bulge), sp.Dir, px, cell, 1);
                }
            }

            // ---- the tongue: over the border, root wide, a drop at its tip
            float tk = (a - Style.TongueFrom) / Style.Tongue;
            float fade = Mathf.Clamp01((clock - (sp.To.First + Style.Film * 0.5f)) / 0.1f);
            bool tOn = (Layers.ShowTongue || (Layers.ShowLiquidTongues && DebugAllowed)) && tk >= 0f && fade < 1f;
            sp.Tongue.enabled = tOn;
            sp.Streak.enabled = tOn && tk < 1f;
            if (tk >= 0f && Say(null, "spill" + spills.IndexOf(sp))) { Voice(SoundSpill); }
            if (tOn)
            {
                Vector2 root = src + sp.Dir * (Style.TongueRoot * cell);
                Vector2 tip = CellWorld(sp.To.Cell) - sp.Dir * ((0.5f - Style.TongueReach) * cell);
                float full = (tip - root).magnitude;
                float grow = Mathf.Clamp01(tk);
                grow = 1f - (1f - grow) * (1f - grow);                  // ease out
                float settle = tk > 1f ? 1f - 0.04f * Mathf.Sin(Mathf.Clamp01((tk - 1f) * 3f) * Mathf.PI) : 1f;
                float len = full * grow * settle;
                sp.Tongue.transform.position = root + sp.Dir * (len * 0.5f);
                sp.Tongue.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                sp.Tongue.transform.localScale = new Vector3(Mathf.Max(len, px), Style.TongueWidth * cell, 1f);
                float alpha = (1f - fade) * (Layers.ShowLiquidTongues && DebugAllowed ? 1f : 0.95f);
                sp.Tongue.color = new Color(1f, 1f, 1f, alpha);
                if (sp.Streak.enabled)
                {
                    sp.Streak.transform.position = root + sp.Dir * (len * Mathf.Repeat(tk * 1.6f, 1f));
                    sp.Streak.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                    sp.Streak.transform.localScale = new Vector3(cell * 0.14f, cell * 0.05f, 1f);
                    sp.Streak.color = new Color(FloodShapes.Pale.r, FloodShapes.Pale.g, FloodShapes.Pale.b, 0.6f);
                }
            }

            // ---- contact: a couple of droplets thrown sideways where it lands
            if (!sp.Contacted && clock >= sp.From.Start + Style.Arrival)
            {
                sp.Contacted = true;
                Voice(SoundContact);
                Say(Haptic, HapticContact);
                if (Layers.ShowContact && Layers.ShowDropletsLayer && sp.ContactDrops > 0)
                {
                    Vector2 at = CellWorld(sp.To.Cell) - sp.Dir * (0.5f * cell);
                    var side = new Vector2(-sp.Dir.y, sp.Dir.x);
                    for (int i = 0; i < sp.ContactDrops; i++)
                    {
                        Vector2 d = (side * (i % 2 == 0 ? 1f : -1f) + sp.Dir * 0.4f).normalized;
                        AddBit(at, at + d * (4f + 2f * i) * px, Style.Contact, (1.5f + i % 3) * px * 1.4f);
                    }
                }
            }
        }

        private void PaintTarget(Target t, float cube, float cell, float px)
        {
            Vector2 at = CellWorld(t.Cell);
            float since = clock - t.First;

            // ---- the real water, rising UNDER the old face
            float water = Layers.ShowCrossfade ? Mathf.Clamp01((since - Style.WaterFrom) / Style.Water) : (since >= Style.IdentityFrom + Style.Identity ? 1f : 0f);
            bool debugWater = Layers.ShowWaterRenderer && DebugAllowed;
            t.Water.enabled = water > 0f || debugWater;
            t.Water.transform.position = at;
            t.Water.transform.localScale = new Vector3(cube, cube, 1f);
            t.Water.color = new Color(1f, 1f, 1f, debugWater ? 1f : water);

            // ---- the old face under the film
            float identity = Layers.ShowCrossfade ? Mathf.Clamp01((since - Style.IdentityFrom) / Style.Identity)
                : (since >= Style.IdentityFrom + Style.Identity ? 1f : 0f);
            float proxyAlpha = 1f - identity;
            if (identity > 0f && !t.Converted)
            {
                t.Converted = true;
                Say(Sounded, SoundConvert);
            }
            t.Proxy.enabled = proxyAlpha > 0f && !debugWater;
            t.Proxy.transform.position = at;
            // A breath of give as the solid goes: 1 -> 0.985 -> 1, never a squash.
            float liq = Layers.ShowLiquefy ? Mathf.Clamp01((since - Style.LiquefyFrom) / Style.Liquefy) : 0f;
            float give = 1f - 0.015f * Mathf.Sin(liq * Mathf.PI);
            t.Proxy.transform.localScale = new Vector3(cube * give, cube * give, 1f);
            Color tint = t.Colour;
            if (Layers.ShowOldCubeProxy && DebugAllowed)
            {
                tint = Color.Lerp(tint, new Color(1f, 0.3f, 1f), 0.4f);
            }
            if (t.Proxy.sharedMaterial == FilmMaterial && FilmMaterial != null)
            {
                Vector4 film = new Vector4(Side(t.Arrival.x), Side(t.Arrival.y), Side(t.Arrival.z), Side(t.Arrival.w));
                float sub = Layers.ShowSubmerge ? Mathf.Clamp01((since - Style.SubmergeFrom) / Style.Submerge) : 0f;
                block.Clear();
                block.SetVector(FilmId, film);
                block.SetFloat(SubmergeId, sub);
                block.SetFloat(LiquefyId, liq);
                block.SetFloat(ClockId, clock);
                block.SetFloat(SeedId, t.Seed);
                float refract = Layers.ShowRefractionLayer ? px / Mathf.Max(cube, 0.0001f) : 0f;
                if (Layers.ShowRefraction && DebugAllowed) { refract *= 6f; }
                block.SetFloat(PxId, refract);
                block.SetVector(UvPerLocalId, new Vector4(t.UvPerLocal.x, t.UvPerLocal.y, 0f, 0f));
                block.SetColor(WaterId, Layers.ShowWaterFilmMask && DebugAllowed ? new Color(1f, 0.2f, 0.8f) : Style.FilmColour);
                block.SetColor(EdgeId, Style.FrontColour);
                t.Proxy.SetPropertyBlock(block);
            }
            tint.a *= proxyAlpha;
            t.Proxy.color = tint;

            // ---- the settle: a broken ripple or two, a few droplets
            for (int k = 0; k < t.Ripples.Count; k++)
            {
                float rk = (since - Style.RippleFrom - k * 0.05f) / Style.Ripple;
                bool debugRipple = Layers.ShowRipple && DebugAllowed;
                bool on = Layers.ShowRippleLayer && rk >= 0f && rk <= 1f;
                t.Ripples[k].enabled = on;
                if (!on)
                {
                    continue;
                }
                float r = Mathf.Lerp(0.20f, debugRipple ? 0.9f : 0.55f, 1f - (1f - rk) * (1f - rk)) * cell;
                t.Ripples[k].transform.position = at;
                t.Ripples[k].transform.rotation = Quaternion.Euler(0f, 0f, t.Seed * 57f + k * 70f);
                t.Ripples[k].transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
                t.Ripples[k].color = new Color(1f, 1f, 1f, (debugRipple ? 0.8f : Style.RippleAlpha) * (1f - rk));
            }
            if (!t.Settled && since >= Style.RippleFrom)
            {
                t.Settled = true;
                Say(Sounded, SoundSettle);
                if (Layers.ShowDropletsLayer)
                {
                    for (int i = 0; i < t.SettleDrops; i++)
                    {
                        float ang = (i + 0.3f + t.Seed) * 2.1f;
                        var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        AddBit(at, at + d * (3f + 5f * ((i * 37 % 10) / 10f)) * px, 0.14f, (1.5f + i % 3) * px * 1.4f);
                    }
                }
            }
        }

        /// <summary>A side's film progress: -1 when no water came in that way.</summary>
        private float Side(float arrival)
        {
            if (arrival < 0f || !Layers.ShowFilm)
            {
                return -1f;
            }
            return Mathf.Clamp((clock - arrival) / Style.Film, 0f, 1.04f);
        }

        private void Drop(Vector2 at, Vector2 dir, float px, float cell, int count)
        {
            for (int i = 0; i < count; i++)
            {
                AddBit(at, at + (dir + new Vector2(-dir.y, dir.x) * 0.4f).normalized * 5f * px, 0.10f, 2.5f * px);
            }
        }

        private void AddBit(Vector2 from, Vector2 to, float life, float size)
        {
            if (Layers.ShowDroplets && DebugAllowed)
            {
                size *= 2.5f;
            }
            SpriteRenderer r = Rent(DropletOrder);
            r.sprite = HazineShapes.Mote;
            bits.Add(new Bit { R = r, Born = clock, Life = life, From = from, To = to, Size = size });
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
                b.R.transform.position = Vector2.Lerp(b.From, b.To, 1f - (1f - k) * (1f - k));
                b.R.transform.localScale = new Vector3(b.Size, b.Size, 1f);
                b.R.color = new Color(FloodShapes.Pale.r, FloodShapes.Pale.g, FloodShapes.Pale.b, 1f - k);
            }
        }

        // ---- debug -----------------------------------------------------------------------------

        private static bool DebugAllowed
        {
            get { return Application.isEditor || Debug.isDebugBuild; }
        }

        private void PaintDebug(float cell)
        {
            int used = 0;
            if (DebugAllowed)
            {
                if (Layers.ShowFloodSources)
                {
                    foreach (Source s in sources)
                    {
                        Mark(ref used, CellWorld(s.Cell), cell, HazineShapes.Frame, new Color(0.3f, 0.6f, 1f), 0f);
                    }
                }
                if (Layers.ShowFloodTargets)
                {
                    foreach (Target t in targets)
                    {
                        Mark(ref used, CellWorld(t.Cell), cell, HazineShapes.Frame, new Color(0.4f, 1f, 0.9f), 0f);
                    }
                }
                if (Layers.ShowIncomingDirections)
                {
                    foreach (Spill sp in spills)
                    {
                        // The side of the target the water enters: left red, right blue, top
                        // yellow, bottom green.
                        Color c = sp.Dir.x > 0f ? Color.red : sp.Dir.x < 0f ? Color.blue : sp.Dir.y < 0f ? Color.yellow : Color.green;
                        Vector2 a = CellWorld(sp.From.Cell);
                        Vector2 b = CellWorld(sp.To.Cell);
                        for (int i = 0; i <= 6; i++)
                        {
                            Mark(ref used, Vector2.Lerp(a, b, i / 6f), cell * 0.07f, HazineShapes.Mote, c, 0f);
                        }
                    }
                }
            }
            for (int i = used; i < debugMarks.Count; i++) { debugMarks[i].enabled = false; }
            bool label = DebugAllowed && Layers.ShowSourceStagger && sources.Count > 0;
            if (label)
            {
                if (debugText == null)
                {
                    debugText = ViewUtil.MakeText3D(transform, "FloodDebug", Vector2.zero, string.Empty, 40, 0.03f,
                        Color.white, DebugOrder, TextAnchor.UpperLeft);
                }
                debugText.gameObject.SetActive(true);
                var sb = new System.Text.StringBuilder();
                foreach (Source s in sources)
                {
                    sb.Append("source ").Append(s.Cell.X).Append(',').Append(s.Cell.Y).Append(": ")
                        .Append(Mathf.RoundToInt(s.Start * 1000f)).Append(" ms, ").Append(s.Facing.Count).Append(" spill\n");
                }
                sb.Append(report.Targets.Count).Append(" targets");
                debugText.text = sb.ToString();
                debugText.characterSize = cell * 0.16f * 10f / 40f;
                debugText.transform.position = CellWorld(sources[0].Cell) + new Vector2(cell, cell);
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

        // ---- housekeeping ----------------------------------------------------------------------

        private bool Say(System.Action<string> channel, string what)
        {
            if (!said.Add(what))
            {
                return false;
            }
            if (channel != null)
            {
                channel(what);
            }
            return true;
        }

        /// <summary>A splash per spill, but never more than a few voices of the same sound.</summary>
        private void Voice(string what)
        {
            int n;
            voices.TryGetValue(what, out n);
            if (n >= MaxVoices)
            {
                return;
            }
            voices[what] = n + 1;
            if (Sounded != null)
            {
                Sounded(what);
            }
        }

        private static Material defaultMaterial;

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
                var go = new GameObject("FloodPart");
                r = go.AddComponent<SpriteRenderer>();
            }
            r.transform.SetParent(transform, false);
            r.sortingOrder = order;
            r.enabled = false;
            r.color = Color.white;
            r.sharedMaterial = DefaultMaterial;
            r.SetPropertyBlock(null);
            // A pooled renderer keeps its last LOCAL transform; every one is reset.
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
    }
}
