// PURPOSE: "Soğuk süblimleşme" (Cryo Sublimation) - removal variant 3, for a cube a boss REMOVED
// (TurnReport.LiftKindAt == Removed). GameUiController.PlayRemoval picks a variant at random per
// removal, and no two of them may read alike:
//   COLD SINK         the floor opens and the cube FALLS               - down
//   PHASE FOLD        its volume is folded into a seam on one edge     - sideways
//   CRYO SUBLIMATION  it stays where it is; its heat is drawn out and its solid mass turns
//                     straight into cold vapour                         - up
//
//   THERMAL DRAIN  the material's life goes first: highlights die, contrast calms, a little
//                  saturation leaves, the contact shadow grows colder and heavier. No blue yet.
//   FROST          pale matte frost walks in from the corners, the edges and a few irregular
//                  patches (one of five masks, chosen from the cell); the cube's own colour is
//                  pushed into a last small warm region - off-centre, high, a broken oval, two
//                  patches - which holds a moment, dims and is taken.
//   DEAD STATE     matte steel-grey frost that still has its bevel: frosted, never glass.
//   SUBLIMATION    two or three pale origins, then the mass goes from them in a few large soft
//                  lobes (one of five erosion masks) - the silhouette really loses material, it
//                  is not faded. One to three thin vapour ribbons rise out of it, carrying a
//                  trace of the old colour up into pale blue-grey, some in front of the cube and
//                  some behind; a few shorter wisps and tiny frost flecks drift up with them.
//   SHELL          what is left is a thin pale shell of the silhouette, hollow inside. It holds,
//                  its sides give way inward a couple of pixels as it fades, and it goes to a few
//                  flecks of frost dust drifting up and in.
//   AFTER          a faint cold haze on the empty cell, gone in a fifth of a second.
//
// NOTHING HERE IS AN EXPLOSION OR A SPELL: no debris, flash, shake, hit-stop or sound, no glow,
// no snowflakes, crystals or icicles, and nothing flies outward. Masks, ribbons and flecks come
// from the cell (no Random), so a cell always sublimates the same way and nothing boils frame to
// frame. Big groups keep every cube's frost and erosion but thin the vapour out: past 20 cells
// only some cubes carry a ribbon at all. Without the CryoSublimation shader (missing or
// unsupported) the cube is tinted to frost and fades under its vapour instead of eroding;
// without CryoVapour there are no ribbons.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Removal variant 3: the cube's heat is drawn out and its mass sublimates upward.</summary>
    public sealed class CryoSublimationView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the sublimation READS. Sizes in CELLS, times in
        /// seconds, speeds in cells per second.</summary>
        public static class Style
        {
            // ---- the wave ----
            /// <summary>Seconds per cell from the group's centre: a heat-death wave, read off the
            /// cubes' own timing rather than drawn.</summary>
            public static float CenterOutDelay = 0.03f;

            public static float CenterOutMaxSpread = 0.15f;

            /// <summary>Seconds either way from the cell; the long phases also stretch a few
            /// percent per cell.</summary>
            public static float TimingJitter = 0.006f;

            // ---- thermal drain ----
            public static float ThermalDrainDuration = 0.10f;

            public static float ThermalDrainStrength = 0.8f;

            // ---- frost ----
            /// <summary>From the first frost to the colour's last region.</summary>
            public static float FrostDuration = 0.19f;

            /// <summary>How far the frosted material goes toward the frost colour.</summary>
            public static float FrostAmount = 0.92f;

            public static int FrostMaskVariationCount = 5;

            /// <summary>The frost front's softness, as a share of the face.</summary>
            public static float FrostEdgeSoftness = 0.07f;

            // ---- the last warm core ----
            /// <summary>How much of the cube's own hue the frost still carries.</summary>
            public static float OriginalColorRetention = 0.12f;

            public static float LastWarmCoreDuration = 0.09f;

            /// <summary>The share of the face still warm when the frost reaches the core.</summary>
            public static float LastWarmCoreSize = 0.10f;

            // ---- sublimation ----
            public static float SublimationDuration = 0.28f;

            /// <summary>Above 1 the mass is gone before the phase ends and the shell stands longer.</summary>
            public static float ErosionSpeed = 1f;

            /// <summary>The erosion edge's softness, as a share of the face.</summary>
            public static float ErosionSoftness = 0.12f;

            public static int ErosionVariationCount = 5;

            // ---- vapour ribbons ----
            public static int RibbonCountMin = 1;

            public static int RibbonCountMax = 3;

            /// <summary>At the root; a ribbon thins as it rises.</summary>
            public static float RibbonWidth = 0.12f;

            public static float RibbonLength = 1.1f;

            public static float RibbonRiseSpeed = 3.0f;

            public static float RibbonLateralDrift = 0.10f;

            public static float RibbonLifetime = 0.46f;

            public static float RibbonOpacity = 0.42f;

            // ---- wisps ----
            public static int WispCount = 3;

            public static float WispLifetime = 0.26f;

            public static float WispSpeed = 1.9f;

            // ---- frost flecks ----
            public static int MoteCount = 6;

            public static float MoteSpeed = 0.55f;

            public static float MoteLifetime = 0.40f;

            /// <summary>The flecks the shell goes to as it collapses.</summary>
            public static int FinalDustCount = 6;

            // ---- the shell ----
            public static float ShellOpacity = 0.24f;

            public static float ShellHoldDuration = 0.08f;

            public static float ShellCollapseDuration = 0.13f;

            /// <summary>How far each side gives way inward, in cells (0.035: two pixels on a
            /// 64-pixel cell).</summary>
            public static float ShellCollapseAmount = 0.035f;

            // ---- after ----
            public static float ResidueStrength = 0.05f;

            public static float ResidueDuration = 0.16f;

            // ---- level of detail ----
            /// <summary>How far the vapour thins from N&lt;=5 to N&gt;20. Frost and erosion never do.</summary>
            public static float LargeNDetailCompensation = 0.8f;
        }

        /// <summary>The lab's debug switches. Visual only; the lab puts them all back on close.</summary>
        public static class Layers
        {
            public static bool ShowThermalDrain = true;

            public static bool ShowFrostMask = true;

            public static bool ShowLastColorCore = true;

            /// <summary>Off: the mass fades instead of eroding - the plain fade it must not be.</summary>
            public static bool ShowSublimationErosion = true;

            /// <summary>The ribbons and the wisps.</summary>
            public static bool ShowVaporRibbons = true;

            public static bool ShowFrostShell = true;

            /// <summary>The flecks, rising with the vapour and left by the shell.</summary>
            public static bool ShowFinalDust = true;

            public static bool ShowColdResidue = true;

            public static void AllOn()
            {
                ShowThermalDrain = true;
                ShowFrostMask = true;
                ShowLastColorCore = true;
                ShowSublimationErosion = true;
                ShowVaporRibbons = true;
                ShowFrostShell = true;
                ShowFinalDust = true;
                ShowColdResidue = true;
            }
        }

        // =================================================================== palette

        /// <summary>Pale steel blue: the frosted material. Never white.</summary>
        private static readonly Color FrostColour = new Color(0.50f, 0.56f, 0.64f);

        /// <summary>Charcoal navy: inside the empty shell.</summary>
        private static readonly Color HollowColour = new Color(0.05f, 0.06f, 0.09f);

        /// <summary>Desaturated slate blue: vapour that has left the colour behind.</summary>
        private static readonly Color SlateColour = new Color(0.52f, 0.58f, 0.67f);

        /// <summary>Pale blue-grey: vapour toward its top.</summary>
        private static readonly Color VapourColour = new Color(0.66f, 0.72f, 0.80f);

        /// <summary>Frosted grey-white: the flecks.</summary>
        private static readonly Color DustColour = new Color(0.74f, 0.78f, 0.84f);

        private static readonly Color ResidueColour = new Color(0.50f, 0.57f, 0.66f);

        private static readonly Color ShadowColour = new Color(0.03f, 0.04f, 0.07f);

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        // =================================================================== layers

        private const int ShadowOrder = 5;

        private const int ResidueOrder = 6;

        private const int BackVapourOrder = 7;

        private const int CubeOrder = 8;

        private const int FrontVapourOrder = 9;

        private const int WispOrder = 10;

        private const int FleckOrder = 11;

        // =================================================================== the masks
        // Both masks are fields over the cube's face, -1..1 each way, built once into one small
        // texture: RED says when the frost reaches a point, GREEN when its mass sublimates. Each
        // holds RANKS (see Equalise), so a front moving at a steady pace takes a steady share of
        // the face - the brief's 100, 85, 68, 48, 28 percent rather than a lurch at the end.

        private const int Variants = 5;

        private const int MaskSize = 64;

        /// <summary>The last warm core, five ways: centre (x, y) and radii (x, y); a fifth and
        /// sixth value give a second centre - two small patches that merge.</summary>
        private static readonly float[][] CoreShapes =
        {
            new[] { 0.22f, 0.06f, 1.00f, 0.85f },                   // a little right of centre
            new[] { 0.00f, 0.32f, 1.05f, 0.72f },                   // the upper middle
            new[] { -0.10f, -0.06f, 1.20f, 0.72f },                 // an oval a frost patch bites
            new[] { -0.30f, 0.14f, 0.80f, 0.80f, 0.26f, -0.16f },   // two small patches
            new[] { -0.20f, -0.24f, 0.90f, 1.00f }                  // low and to the left
        };

        /// <summary>Where the frost gets in early besides the corners and edges: (x, y, radius,
        /// strength) per patch, three to five a mask. The third mask's first patch is the bite.</summary>
        private static readonly float[][] FrostPatches =
        {
            new[] { -0.85f, 0.80f, 0.45f, 0.30f, 0.90f, -0.75f, 0.38f, 0.26f, -0.60f, -0.95f, 0.40f, 0.22f, 0.35f, 1.00f, 0.30f, 0.18f },
            new[] { 0.95f, 0.55f, 0.40f, 0.28f, -0.90f, -0.40f, 0.45f, 0.30f, 0.20f, -0.95f, 0.38f, 0.24f },
            new[] { 0.30f, 0.34f, 0.28f, 0.22f, -0.95f, 0.70f, 0.40f, 0.26f, 0.80f, -0.90f, 0.42f, 0.28f, -0.40f, -0.95f, 0.30f, 0.18f, 0.95f, 0.20f, 0.30f, 0.16f },
            new[] { 0.00f, 0.95f, 0.40f, 0.28f, 0.00f, -0.95f, 0.40f, 0.26f, -0.95f, -0.60f, 0.35f, 0.22f, 0.95f, 0.70f, 0.35f, 0.20f },
            new[] { 0.85f, 0.85f, 0.45f, 0.30f, -0.95f, 0.30f, 0.38f, 0.24f, 0.60f, -0.70f, 0.40f, 0.26f }
        };

        /// <summary>The five erosion masks, as lobes of (x, y, pace, delay) that grow from their
        /// origins and merge; the origins are where the vapour ribbons rise. Every origin sits on
        /// or just past the silhouette, so the mass is taken from its OUTLINE - an opening in the
        /// middle of a pale face reads as a hole, a pit, which this must never be.
        ///   A  from the upper right            B  the left edge and the upper middle
        ///   C  two patches that meet           D  an opening beside the middle of the top,
        ///                                         reaching in toward the centre
        ///   E  a bottom corner and a side</summary>
        private static readonly float[][] ErosionLobes =
        {
            new[] { 1.05f, 1.05f, 1.00f, 0.00f, 0.20f, 1.15f, 0.85f, 0.12f, -1.15f, -0.40f, 0.75f, 0.38f },
            new[] { -1.15f, 0.05f, 0.95f, 0.00f, 0.05f, 1.15f, 0.95f, 0.05f, 1.10f, -0.70f, 0.70f, 0.40f },
            new[] { -1.10f, 0.60f, 0.90f, 0.00f, 1.10f, -0.50f, 0.90f, 0.04f },
            new[] { 0.30f, 1.20f, 1.05f, 0.00f, -1.10f, -1.05f, 0.75f, 0.25f, 1.15f, -0.20f, 0.70f, 0.30f },
            new[] { -1.05f, -1.05f, 0.95f, 0.00f, 1.15f, 0.15f, 0.90f, 0.08f }
        };

        // =================================================================== state

        private struct Dice
        {
            private uint state;

            public Dice(uint seed)
            {
                state = seed == 0u ? 0x9E3779B9u : seed;
            }

            public float Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state >> 8) * (1f / 16777216f);
            }

            public float Range(float a, float b)
            {
                return a + (b - a) * Next();
            }
        }

        private static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>One cell's timeline, seconds from its own start.</summary>
        private struct Times
        {
            public float FrostStart;
            public float CoreAt;
            public float CoreDead;
            public float OriginsStart;
            public float SubStart;
            public float SubEnd;
            public float HoldEnd;
            public float CollapseEnd;
            public float ResidueEnd;
        }

        /// <summary>One ribbon or wisp: a strip rising from a point on the cube.</summary>
        private sealed class Vapour
        {
            public Strip Strip;
            /// <summary>From the cell's centre, world units.</summary>
            public Vector2 Root;
            public float Start;
            public float Life;
            public float Width;
            public float Length;
            public float Rise;
            public float Drift;
            public float Bend;
            public float Phase;
            public float Opacity;
            /// <summary>The old colour it carries a trace of at its root.</summary>
            public Color Trace;
        }

        /// <summary>One tiny frost fleck, rising with the vapour or settling off the shell.</summary>
        private sealed class Fleck
        {
            public SpriteRenderer Renderer;
            public Vector2 From;
            public Vector2 Velocity;
            public float Start;
            public float Life;
            public float Size;
            public float Opacity;
        }

        private sealed class Cube
        {
            public Vector2 At;
            public float Start;
            public Times T;
            public ClusterBurstView.Look Look;
            public int FrostVariant;
            public int ErosionVariant;
            /// <summary>The frost mask turned a quarter at a time and mirrored, from the cell -
            /// forty patterns out of five. As a 2x2 matrix, rows in xy and zw.</summary>
            public Vector4 FrostAxes;
            /// <summary>The erosion mask mirrored left-right (1 or -1). Only mirrored: turned, its
            /// top would stop going first and its ribbons would rise from the wrong places.</summary>
            public float ErodeFlip;
            /// <summary>How much each side gives in the collapse: right, up, left, down.</summary>
            public Vector4 Sides;
            /// <summary>When it leaves the tile's own material for the shader: at once, unless
            /// the tile moves (water, fire) - that keeps moving until the frost begins.</summary>
            public float SwapAt;
            public bool OnShader;
            public SpriteRenderer Body;
            public SpriteRenderer Shadow;
            public SpriteRenderer Residue;
            public readonly List<Vapour> Vapours = new List<Vapour>();
            public readonly List<Fleck> Flecks = new List<Fleck>();
        }

        private sealed class Batch
        {
            public readonly List<Cube> Cubes = new List<Cube>();
            public float Clock;
            public float End;
            public float Cell;
            public float CubeSize;
            public float Slot;
            /// <summary>Detail tier: 0 for N&lt;=5 up to 1 past 20, times the compensation.</summary>
            public float Lod;
        }

        /// <summary>A pooled ribbon mesh: a strip of StripPoints pairs of vertices.</summary>
        private sealed class Strip
        {
            public MeshRenderer Renderer;
            public Mesh Mesh;
        }

        private const int StripPoints = 10;

        private readonly List<Batch> batches = new List<Batch>();

        private readonly Stack<SpriteRenderer> spareRenderers = new Stack<SpriteRenderer>();

        private readonly Stack<Strip> spareStrips = new Stack<Strip>();

        private readonly List<Vector3> stripVertices = new List<Vector3>(StripPoints * 2);

        private readonly List<Color> stripColours = new List<Color>(StripPoints * 2);

        private readonly Vector2[] stripCentres = new Vector2[StripPoints];

        private Material plainMaterial;

        private MaterialPropertyBlock block;

        private static Material cryoMaterial;

        private static Material vapourMaterial;

        private static bool shadersLooked;

        private static Texture2D maskAtlas;

        private static readonly Dictionary<Sprite, Color> traceColours = new Dictionary<Sprite, Color>();

        /// <summary>Finds both shaders once. Shader.Find works in the editor; the Resources copy is
        /// what survives into a build. A shader that is missing or that this device cannot run
        /// leaves its material null - the fallbacks.</summary>
        private static void LoadShaders()
        {
            if (shadersLooked)
            {
                return;
            }
            shadersLooked = true;
            Shader cryo = FindShader("ProjectBlock/CryoSublimation", "Shaders/CryoSublimation");
            if (cryo != null)
            {
                cryoMaterial = new Material(cryo);
                cryoMaterial.hideFlags = HideFlags.HideAndDontSave;
                cryoMaterial.SetTexture(MaskTexId, MaskAtlas());
            }
            Shader vapour = FindShader("ProjectBlock/CryoVapour", "Shaders/CryoVapour");
            if (vapour != null)
            {
                vapourMaterial = new Material(vapour);
                vapourMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private static Shader FindShader(string name, string resource)
        {
            Shader shader = Shader.Find(name);
            if (shader == null)
            {
                shader = Resources.Load<Shader>(resource);
            }
            return shader != null && shader.isSupported ? shader : null;
        }

        // =================================================================== driving it

        /// <summary>
        /// Sublimates each cube in <paramref name="looks"/> at its cell in <paramref name="cells"/>
        /// (world centres). <paramref name="cubeSize"/> is the size the board draws a cube at and
        /// <paramref name="slotSize"/> the size of an empty slot - the cold haze lies on it.
        /// </summary>
        public void Play(IReadOnlyList<Vector2> cells, IReadOnlyList<ClusterBurstView.Look> looks,
            float cellSize, float cubeSize, float slotSize)
        {
            if (cells == null || cells.Count == 0 || cellSize <= 0f)
            {
                return;
            }
            LoadShaders();
            int n = cells.Count;
            int tier = n <= 5 ? 0 : n <= 12 ? 1 : n <= 20 ? 2 : 3;
            var batch = new Batch
            {
                Cell = cellSize,
                CubeSize = cubeSize > 0f ? cubeSize : cellSize,
                Slot = slotSize > 0f ? slotSize : cellSize * 0.82f,
                Lod = (tier == 0 ? 0f : tier == 1 ? 0.35f : tier == 2 ? 0.65f : 1f)
                    * Mathf.Clamp01(Style.LargeNDetailCompensation)
            };
            Vector2 centre = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                centre += cells[i];
            }
            centre /= n;
            float furthest = 0f;
            // The two cubes nearest the middle always carry a ribbon, however big the group.
            int hero0 = -1;
            int hero1 = -1;
            float near0 = float.MaxValue;
            float near1 = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float d = (cells[i] - centre).sqrMagnitude;
                furthest = Mathf.Max(furthest, Mathf.Sqrt(d));
                if (d < near0)
                {
                    hero1 = hero0;
                    near1 = near0;
                    hero0 = i;
                    near0 = d;
                }
                else if (d < near1)
                {
                    hero1 = i;
                    near1 = d;
                }
            }
            float furthestCells = furthest / cellSize;
            float perCell = furthestCells > 0f
                ? Mathf.Min(Style.CenterOutDelay, Style.CenterOutMaxSpread / furthestCells) : 0f;
            Sprite fallbackTile = ViewUtil.DefaultTile != null ? ViewUtil.DefaultTile
                : ViewUtil.WhiteSprite;
            int frostKinds = Mathf.Clamp(Style.FrostMaskVariationCount, 1, Variants);
            int erosionKinds = Mathf.Clamp(Style.ErosionVariationCount, 1, Variants);

            for (int i = 0; i < n; i++)
            {
                uint seed = Hash(Mathf.RoundToInt(cells[i].x / cellSize * 4f),
                    Mathf.RoundToInt(cells[i].y / cellSize * 4f));
                var dice = new Dice(seed);
                var c = new Cube { At = cells[i] };
                float jitter = n > 1 ? dice.Range(-1f, 1f) * Style.TimingJitter : 0f;
                c.Start = Mathf.Max(0f, (cells[i] - centre).magnitude / cellSize * perCell + jitter);
                c.T = Timeline(dice.Range(-1f, 1f) * 0.05f);
                c.FrostVariant = (int)((seed >> 3) % (uint)frostKinds);
                c.ErosionVariant = (int)((seed >> 9) % (uint)erosionKinds);
                c.FrostAxes = Axes((int)((seed >> 17) & 7u));
                c.ErodeFlip = ((seed >> 21) & 1u) == 0u ? 1f : -1f;
                float right = dice.Range(0.45f, 1f);
                float up = dice.Range(0.45f, 1f);
                float left = dice.Range(0.45f, 1f);
                float down = dice.Range(0.45f, 1f);
                c.Sides = new Vector4(right, up, left, down);
                bool known = looks != null && i < looks.Count && looks[i].Tile != null;
                c.Look = known ? looks[i] : new ClusterBurstView.Look
                {
                    Tile = fallbackTile,
                    Colour = new Color(0.62f, 0.68f, 0.82f)
                };
                Material own = ViewUtil.TileMaterial(c.Look.Tile);
                c.SwapAt = own != null ? c.T.FrostStart : 0f;
                c.OnShader = own == null && cryoMaterial != null;
                c.Shadow = Rent(SoftSquareSprite(), ShadowOrder, null);
                c.Residue = Rent(SoftSquareSprite(), ResidueOrder, null);
                c.Body = Rent(c.Look.Tile, CubeOrder, c.OnShader ? cryoMaterial : own);
                bool hero = i == hero0 || i == hero1 || (seed >> 13) % 3u == 0u;
                AddVapour(batch, c, ref dice, hero);
                AddFlecks(batch, c, ref dice);
                // On THIS frame: the board repainted the cell empty a moment ago.
                PaintCube(batch, c, -c.Start);
                batch.Cubes.Add(c);
                batch.End = Mathf.Max(batch.End, c.Start + CubeEnd(c));
            }
            batches.Add(batch);
        }

        /// <summary>One cell's timeline, with the frost and the sublimation stretched by
        /// <paramref name="variation"/> - enough that a group never moves as one machine.</summary>
        private static Times Timeline(float variation)
        {
            float v = 1f + variation;
            var t = new Times();
            t.FrostStart = Style.ThermalDrainDuration * 0.6f;
            t.CoreAt = t.FrostStart + Style.FrostDuration * v;
            t.CoreDead = t.CoreAt + Style.LastWarmCoreDuration;
            t.SubStart = t.CoreDead - 0.04f;
            t.OriginsStart = t.SubStart - 0.05f;
            t.SubEnd = t.SubStart + Style.SublimationDuration * v;
            t.HoldEnd = t.SubEnd + Style.ShellHoldDuration;
            t.CollapseEnd = t.HoldEnd + Style.ShellCollapseDuration;
            t.ResidueEnd = t.CollapseEnd + Style.ResidueDuration;
            return t;
        }

        /// <summary>The ribbons and wisps of one cube. Its ribbons rise from its erosion's origins,
        /// the first in front of the cube, the next behind it, alternating - depth, never chaos.</summary>
        private void AddVapour(Batch batch, Cube c, ref Dice dice, bool hero)
        {
            float lod = batch.Lod;
            int min = Mathf.Max(0, Style.RibbonCountMin);
            int max = Mathf.Max(min, Style.RibbonCountMax);
            int ribbons;
            if (lod >= 0.75f)
            {
                // Past 20 cells: one ribbon, on some cubes only.
                ribbons = hero ? Mathf.Min(1, max) : 0;
            }
            else
            {
                int top = lod >= 0.45f ? Mathf.Max(min, Mathf.Min(max, 2)) : max;
                ribbons = min + Mathf.Min(top - min, (int)(dice.Next() * (top - min + 1)));
            }
            float secondary = Mathf.Pow(1f - lod, 1.5f);
            int wisps = Count(Style.WispCount * secondary, ref dice);
            if (vapourMaterial == null)
            {
                return;
            }
            float cell = batch.Cell;
            float half = batch.CubeSize * 0.5f;
            float fade = Mathf.Lerp(1f, 0.7f, lod);
            Color trace = TraceColour(c.Look);
            float[] lobes = ErosionLobes[c.ErosionVariant];
            int origins = lobes.Length / 4;
            for (int r = 0; r < ribbons; r++)
            {
                int o = r % origins;
                var v = new Vapour();
                float jx = dice.Range(-0.06f, 0.06f);
                float jy = dice.Range(-0.04f, 0.04f);
                v.Root = new Vector2(lobes[o * 4] * c.ErodeFlip, lobes[o * 4 + 1]) * (half * 0.8f)
                    + new Vector2(jx, jy) * cell;
                v.Start = c.T.SubStart + 0.01f + r * 0.045f + dice.Range(0f, 0.02f);
                v.Life = Style.RibbonLifetime * dice.Range(0.85f, 1.15f);
                v.Width = Style.RibbonWidth * cell * dice.Range(0.85f, 1.15f);
                v.Length = Style.RibbonLength * cell * dice.Range(0.7f, 1.15f);
                v.Rise = Style.RibbonRiseSpeed * cell * dice.Range(0.85f, 1.1f);
                v.Drift = Style.RibbonLateralDrift * cell * dice.Range(-1f, 1f);
                v.Bend = 0.03f * cell * dice.Range(-1f, 1f);
                v.Phase = dice.Range(0f, 6.2832f);
                v.Opacity = Style.RibbonOpacity * fade;
                v.Trace = trace;
                v.Strip = RentStrip(r % 2 == 0 ? FrontVapourOrder : BackVapourOrder);
                c.Vapours.Add(v);
            }
            for (int w = 0; w < wisps; w++)
            {
                // Shorter, fainter and quicker than a ribbon, off the upper part of the cube.
                var v = new Vapour();
                float x = dice.Range(-0.7f, 0.7f);
                float y = dice.Range(-0.1f, 0.8f);
                v.Root = new Vector2(x, y) * half;
                v.Start = c.T.SubStart + 0.05f + dice.Range(0f, 0.5f) * Style.SublimationDuration;
                v.Life = Style.WispLifetime * dice.Range(0.8f, 1.2f);
                v.Width = Style.RibbonWidth * 0.45f * cell * dice.Range(0.8f, 1.2f);
                v.Length = Style.RibbonLength * 0.5f * cell * dice.Range(0.8f, 1.2f);
                v.Rise = Style.WispSpeed * cell * dice.Range(0.85f, 1.15f);
                v.Drift = Style.RibbonLateralDrift * 0.8f * cell * dice.Range(-1f, 1f);
                v.Bend = 0.03f * cell * dice.Range(-1f, 1f);
                v.Phase = dice.Range(0f, 6.2832f);
                v.Opacity = Style.RibbonOpacity * 0.6f * fade;
                v.Trace = SlateColour;
                v.Strip = RentStrip(WispOrder);
                c.Vapours.Add(v);
            }
        }

        /// <summary>The frost flecks of one cube: a few rising with the vapour, and the dust the
        /// shell goes to - up and a little IN, never out.</summary>
        private void AddFlecks(Batch batch, Cube c, ref Dice dice)
        {
            float secondary = Mathf.Pow(1f - batch.Lod, 1.5f);
            int motes = Count(Style.MoteCount * secondary, ref dice);
            int dust = Count(Style.FinalDustCount * Mathf.Max(secondary, 0.15f), ref dice);
            float cell = batch.Cell;
            float half = batch.CubeSize * 0.5f;
            Times T = c.T;
            float window = Style.SublimationDuration * 0.45f;
            for (int m = 0; m < motes; m++)
            {
                var f = new Fleck();
                float x = dice.Range(-0.7f, 0.7f);
                float y = dice.Range(-0.4f, 0.8f);
                f.From = new Vector2(x, y) * half;
                float drift = dice.Range(-0.12f, 0.12f);
                float rise = Style.MoteSpeed * dice.Range(0.7f, 1.1f);
                f.Velocity = new Vector2(drift, rise) * cell;
                f.Start = T.SubStart + 0.02f + dice.Next() * window;
                f.Life = Style.MoteLifetime * dice.Range(0.85f, 1.15f);
                f.Size = dice.Range(0.024f, 0.038f) * cell;
                f.Opacity = dice.Range(0.4f, 0.6f);
                f.Renderer = Rent(FleckSprite(), FleckOrder, null);
                c.Flecks.Add(f);
            }
            float from = T.HoldEnd + Style.ShellCollapseDuration * 0.25f;
            for (int d = 0; d < dust; d++)
            {
                var f = new Fleck();
                int side = (int)(dice.Next() * 4f) & 3;
                float along = dice.Range(-0.8f, 0.8f);
                Vector2 edge = side == 0 ? new Vector2(1f, along) : side == 1 ? new Vector2(along, 1f)
                    : side == 2 ? new Vector2(-1f, along) : new Vector2(along, -1f);
                f.From = edge * (half * 0.88f);
                float inward = dice.Range(0.10f, 0.18f);
                float rise = dice.Range(0.18f, 0.32f);
                f.Velocity = (-f.From.normalized * inward + Vector2.up * rise) * cell;
                f.Start = from + dice.Range(0f, 0.04f);
                f.Life = dice.Range(0.16f, 0.24f);
                f.Size = dice.Range(0.02f, 0.03f) * cell;
                f.Opacity = dice.Range(0.4f, 0.55f);
                f.Renderer = Rent(FleckSprite(), FleckOrder, null);
                c.Flecks.Add(f);
            }
        }

        /// <summary>A fractional count made whole from the cell's dice, so a group's total comes
        /// out right without every cube getting the same.</summary>
        private static int Count(float amount, ref Dice dice)
        {
            return Mathf.Max(0, Mathf.FloorToInt(amount + dice.Next()));
        }

        private static float CubeEnd(Cube c)
        {
            float end = c.T.ResidueEnd;
            for (int i = 0; i < c.Vapours.Count; i++)
            {
                end = Mathf.Max(end, c.Vapours[i].Start + c.Vapours[i].Life);
            }
            for (int i = 0; i < c.Flecks.Count; i++)
            {
                end = Mathf.Max(end, c.Flecks[i].Start + c.Flecks[i].Life);
            }
            return end;
        }

        /// <summary>Stops everything at once - the run ending, the board being torn down.</summary>
        public void Stop()
        {
            for (int i = 0; i < batches.Count; i++)
            {
                Release(batches[i]);
            }
            batches.Clear();
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // =================================================================== the clock

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            for (int b = batches.Count - 1; b >= 0; b--)
            {
                Batch batch = batches[b];
                batch.Clock += dt;
                for (int i = 0; i < batch.Cubes.Count; i++)
                {
                    Cube c = batch.Cubes[i];
                    float t = batch.Clock - c.Start;
                    PaintCube(batch, c, t);
                    PaintVapour(batch, c, t);
                    PaintFlecks(c, t);
                }
                if (batch.Clock >= batch.End)
                {
                    Release(batch);
                    batches.RemoveAt(b);
                }
            }
        }

        /// <summary>How much of the face is frosted at <paramref name="t"/>: fast at the edges,
        /// slowing as the colour is pushed into its last region, which holds a moment and goes.
        /// Without the core it is one steady walk from the first frost to the last.</summary>
        private static float FrostProgress(Times T, float t, bool core)
        {
            if (t <= T.FrostStart)
            {
                return 0f;
            }
            if (!core)
            {
                return Mathf.Clamp01((t - T.FrostStart) / Mathf.Max(T.CoreDead - T.FrostStart, 0.0001f));
            }
            float keep = Mathf.Clamp(Style.LastWarmCoreSize, 0.02f, 0.5f);
            if (t < T.CoreAt)
            {
                float u = (t - T.FrostStart) / Mathf.Max(T.CoreAt - T.FrostStart, 0.0001f);
                return (1f - keep) * (1f - Mathf.Pow(1f - u, 1.3f));
            }
            float k = Mathf.Clamp01((t - T.CoreAt) / Mathf.Max(T.CoreDead - T.CoreAt, 0.0001f));
            return 1f - keep + keep * Smooth((k - 0.3f) / 0.7f);
        }

        /// <summary>The cube itself at its own age: drained, frosted, eroded, a shell, collapsed.</summary>
        private void PaintCube(Batch batch, Cube c, float t)
        {
            Times T = c.T;
            float cell = batch.Cell;
            float size = batch.CubeSize;
            float drain = 0f;
            float frost = 0f;
            float coreFade = 0f;
            float origins = 0f;
            float erode = 0f;
            float shell = 0f;
            float collapse = 0f;
            if (t > 0f)
            {
                if (Layers.ShowThermalDrain)
                {
                    drain = Style.ThermalDrainStrength
                        * Smooth((t - c.SwapAt) / Mathf.Max(Style.ThermalDrainDuration, 0.0001f));
                }
                if (Layers.ShowFrostMask)
                {
                    frost = FrostProgress(T, t, Layers.ShowLastColorCore);
                    if (Layers.ShowLastColorCore)
                    {
                        coreFade = Smooth((t - T.CoreAt) / Mathf.Max(T.CoreDead - T.CoreAt, 0.0001f));
                    }
                }
                origins = Smooth((t - T.OriginsStart) / 0.05f);
                float erosionTime = Mathf.Max((T.SubEnd - T.SubStart) / Mathf.Max(Style.ErosionSpeed, 0.01f),
                    0.0001f);
                erode = Mathf.Pow(Mathf.Clamp01((t - T.SubStart) / erosionTime), 1.15f);
                shell = Layers.ShowFrostShell ? Smooth((erode - 0.45f) / 0.47f) : 0f;
                collapse = Smooth((t - T.HoldEnd) / Mathf.Max(Style.ShellCollapseDuration, 0.0001f));
            }
            float alpha = t >= T.CollapseEnd ? 0f : 1f - collapse;
            if (!Layers.ShowSublimationErosion)
            {
                alpha *= 1f - erode;
                erode = 0f;
                shell = 0f;
                origins = 0f;
            }

            if (!c.OnShader && cryoMaterial != null && t >= c.SwapAt)
            {
                c.OnShader = true;
                c.Body.sharedMaterial = cryoMaterial;
            }
            if (c.OnShader)
            {
                Place(c.Body, c.At, size, c.Look.Colour, alpha, 0f);
                Apply(c, size * 0.5f, drain, frost, coreFade, origins, erode, shell,
                    Style.ShellCollapseAmount * cell * collapse);
            }
            else
            {
                // Still on its own material, or no shader at all: the frost as a tint, and the
                // mass fading under its vapour.
                Color tint = Color.Lerp(c.Look.Colour * (1f - 0.1f * drain), FrostColour,
                    frost * Style.FrostAmount);
                float fall = cryoMaterial == null ? erode : 0f;
                Place(c.Body, c.At, size, tint, alpha * (1f - fall), 0f);
            }

            // The contact shadow: colder and heavier as the heat goes, and gone before the mass
            // starts to go - seen through an opening, a dark shadow reads as a pit.
            float heavy = Layers.ShowThermalDrain
                ? 0.32f * Smooth(t / Mathf.Max(Style.ThermalDrainDuration, 0.0001f)) : 0f;
            heavy *= 1f - Smooth((t - T.OriginsStart) / 0.06f);
            Place(c.Shadow, c.At + new Vector2(0f, -0.015f * cell), size * 1.05f, ShadowColour,
                t >= T.CollapseEnd ? 0f : heavy, 0f);

            // The cold haze on the empty cell: in with the collapse, softly out.
            float residue = 0f;
            if (Layers.ShowColdResidue && t >= T.HoldEnd)
            {
                residue = t < T.CollapseEnd
                    ? Style.ResidueStrength * collapse
                    : Style.ResidueStrength
                        * (1f - Smooth((t - T.CollapseEnd) / Mathf.Max(Style.ResidueDuration, 0.0001f)));
            }
            Place(c.Residue, c.At, batch.Slot * 1.02f, ResidueColour, residue, 0f);
        }

        /// <summary>The ribbons and wisps: each grows up out of the material, slowing; after half
        /// its life its root lets go of the cube and the whole strip drifts up, shortening, as it
        /// thins out. Thicker at the root, thinner as it rises; a trace of the old colour at the
        /// root, slate, then pale blue-grey, then nothing.</summary>
        private void PaintVapour(Batch batch, Cube c, float t)
        {
            for (int i = 0; i < c.Vapours.Count; i++)
            {
                Vapour v = c.Vapours[i];
                float age = t - v.Start;
                if (!Layers.ShowVaporRibbons || age <= 0f || age >= v.Life)
                {
                    v.Strip.Renderer.enabled = false;
                    continue;
                }
                BuildStrip(v, c.At, age, batch.Cell);
                v.Strip.Renderer.enabled = true;
            }
        }

        private void BuildStrip(Vapour v, Vector2 at, float age, float cell)
        {
            float k = age / v.Life;
            float grow = v.Length * (1f - Mathf.Exp(-age * v.Rise / Mathf.Max(v.Length, 0.0001f) * 1.6f));
            float lift = 0.5f * v.Rise * Mathf.Max(0f, age - 0.5f * v.Life);
            float rootY = v.Root.y + lift;
            float span = Mathf.Max(grow - 0.75f * lift, 0.001f);
            float fadeIn = Smooth(k / 0.15f);
            float fadeOut = 1f - Smooth((k - 0.55f) / 0.45f);
            Color trace = Color.Lerp(v.Trace, FrostColour, 0.7f + 0.3f * Smooth(k / 0.8f));
            float drift = v.Drift * (0.4f + 0.6f * Smooth(k));
            float sway = 0.012f * cell;
            for (int p = 0; p < StripPoints; p++)
            {
                float s = p / (float)(StripPoints - 1);
                float x = v.Root.x + drift * Mathf.Pow(s, 1.5f) + v.Bend * Mathf.Sin(Mathf.PI * s)
                    + sway * Mathf.Sin(v.Phase + age * 5.6f + s * 2.2f);
                stripCentres[p] = at + new Vector2(x, rootY + span * s);
            }
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            stripVertices.Clear();
            stripColours.Clear();
            for (int p = 0; p < StripPoints; p++)
            {
                float s = p / (float)(StripPoints - 1);
                Vector2 along = stripCentres[Mathf.Min(p + 1, StripPoints - 1)]
                    - stripCentres[Mathf.Max(p - 1, 0)];
                along = along.sqrMagnitude > 1e-10f ? along.normalized : Vector2.up;
                var side = new Vector2(-along.y, along.x);
                float width = v.Width * (1f - 0.65f * s) * (0.85f + 0.15f * Mathf.Sin(v.Phase * 1.7f + s * 3.1f))
                    * (1f + 0.25f * k);
                Vector2 centre = stripCentres[p];
                stripVertices.Add(centre - side * (width * 0.5f));
                stripVertices.Add(centre + side * (width * 0.5f));
                // Moderate at the root, clearest a third of the way up, gone at the head.
                float body = s < 0.3f ? Mathf.Lerp(0.4f, 1f, s / 0.3f) : Mathf.Pow(1f - (s - 0.3f) / 0.7f, 1.3f);
                Color colour = s < 0.3f ? Color.Lerp(trace, SlateColour, s / 0.3f)
                    : Color.Lerp(SlateColour, VapourColour, (s - 0.3f) / 0.4f);
                // Mesh colours reach the shader as they are; the sprites' are made linear first.
                if (linear)
                {
                    colour = colour.linear;
                }
                colour.a = Mathf.Clamp01(v.Opacity * fadeIn * fadeOut * body * Smooth(s / 0.1f));
                stripColours.Add(colour);
                stripColours.Add(colour);
            }
            Mesh mesh = v.Strip.Mesh;
            mesh.SetVertices(stripVertices);
            mesh.SetColors(stripColours);
            mesh.RecalculateBounds();
        }

        /// <summary>The flecks: slowing a little, shrinking, gone. Cold dust, not sparks.</summary>
        private static void PaintFlecks(Cube c, float t)
        {
            for (int i = 0; i < c.Flecks.Count; i++)
            {
                Fleck f = c.Flecks[i];
                float age = t - f.Start;
                if (!Layers.ShowFinalDust || age <= 0f || age >= f.Life)
                {
                    f.Renderer.color = Clear;
                    continue;
                }
                float k = age / f.Life;
                Vector2 at = c.At + f.From + f.Velocity * (age * (1f - 0.3f * k));
                float alpha = f.Opacity * Smooth(k / 0.2f) * (1f - Smooth((k - 0.5f) / 0.5f));
                Place(f.Renderer, at, f.Size * (1f - 0.5f * k), DustColour, alpha, 0f);
            }
        }

        /// <summary>The cube's old colour, for the vapour to carry a trace of: its tint on the plain
        /// tile it is tinted on, else the colour of the element whose face it wears.</summary>
        private static Color TraceColour(ClusterBurstView.Look look)
        {
            if (!ViewUtil.CarriesOwnPaint(look.Tile))
            {
                return look.Colour;
            }
            Color colour;
            if (traceColours.TryGetValue(look.Tile, out colour))
            {
                return colour;
            }
            colour = SlateColour;
            foreach (BlockElement element in System.Enum.GetValues(typeof(BlockElement)))
            {
                if (ViewUtil.CubeTile(element) == look.Tile)
                {
                    colour = ViewUtil.ElementColor(element);
                    break;
                }
            }
            traceColours[look.Tile] = colour;
            return colour;
        }

        // =================================================================== the shader's inputs

        private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");

        private static readonly int CentreId = Shader.PropertyToID("_Centre");

        private static readonly int HalfId = Shader.PropertyToID("_Half");

        private static readonly int TilesId = Shader.PropertyToID("_Tiles");

        private static readonly int FrostAxesId = Shader.PropertyToID("_FrostAxes");

        private static readonly int ErodeFlipId = Shader.PropertyToID("_ErodeFlip");

        private static readonly int DrainId = Shader.PropertyToID("_Drain");

        private static readonly int FrostId = Shader.PropertyToID("_Frost");

        private static readonly int FrostSoftId = Shader.PropertyToID("_FrostSoft");

        private static readonly int FrostAmountId = Shader.PropertyToID("_FrostAmount");

        private static readonly int RetentionId = Shader.PropertyToID("_Retention");

        private static readonly int CoreFadeId = Shader.PropertyToID("_CoreFade");

        private static readonly int OriginsId = Shader.PropertyToID("_Origins");

        private static readonly int ErodeId = Shader.PropertyToID("_Erode");

        private static readonly int ErodeSoftId = Shader.PropertyToID("_ErodeSoft");

        private static readonly int ShellId = Shader.PropertyToID("_Shell");

        private static readonly int ShellOpacityId = Shader.PropertyToID("_ShellOpacity");

        private static readonly int InsetId = Shader.PropertyToID("_Inset");

        private static readonly int FrostColourId = Shader.PropertyToID("_FrostColour");

        private static readonly int HollowColourId = Shader.PropertyToID("_HollowColour");

        /// <summary>Hands the cube its state. One reused block, so nothing is allocated per frame.
        /// Fronts are given in rank units stretched past both ends by their softness, so 0 shows
        /// none of it and 1 all of it.</summary>
        private void Apply(Cube c, float half, float drain, float frost, float coreFade, float origins,
            float erode, float shell, float inset)
        {
            if (cryoMaterial == null || c.Body.sharedMaterial != cryoMaterial)
            {
                return;
            }
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            // The shader works in world space; everything here is placed relative to this view.
            Vector3 centre = transform.TransformPoint(new Vector3(c.At.x, c.At.y, 0f));
            float scale = transform.lossyScale.x;
            float fs = Mathf.Max(Style.FrostEdgeSoftness, 0.001f);
            float es = Mathf.Max(Style.ErosionSoftness, 0.001f);
            float w = inset * scale;
            block.Clear();
            block.SetVector(CentreId, new Vector4(centre.x, centre.y, 0f, 0f));
            block.SetFloat(HalfId, Mathf.Max(half * scale, 0.0001f));
            block.SetVector(TilesId, new Vector4(c.FrostVariant, c.ErosionVariant, 1f / Variants, 0f));
            block.SetVector(FrostAxesId, c.FrostAxes);
            block.SetFloat(ErodeFlipId, c.ErodeFlip);
            block.SetFloat(DrainId, drain);
            block.SetFloat(FrostId, Mathf.Lerp(-fs, 1f + fs, frost));
            block.SetFloat(FrostSoftId, fs);
            block.SetFloat(FrostAmountId, Style.FrostAmount);
            block.SetFloat(RetentionId, Style.OriginalColorRetention);
            block.SetFloat(CoreFadeId, coreFade);
            block.SetFloat(OriginsId, origins);
            block.SetFloat(ErodeId, Mathf.Lerp(-es, 1f + es, erode));
            block.SetFloat(ErodeSoftId, es);
            block.SetFloat(ShellId, shell);
            block.SetFloat(ShellOpacityId, Style.ShellOpacity);
            block.SetVector(InsetId, new Vector4(w * c.Sides.x, w * c.Sides.y, w * c.Sides.z, w * c.Sides.w));
            block.SetColor(FrostColourId, FrostColour);
            block.SetColor(HollowColourId, HollowColour);
            c.Body.SetPropertyBlock(block);
        }

        // =================================================================== the mask texture

        /// <summary>Both masks for all five variants, built once: a 64-pixel tile per variant,
        /// frost in red, erosion in green. Linear, bilinear, clamped - smooth values, so the
        /// fronts are soft at any zoom.</summary>
        private static Texture2D MaskAtlas()
        {
            if (maskAtlas != null)
            {
                return maskAtlas;
            }
            int width = MaskSize * Variants;
            var px = new Color32[width * MaskSize];
            var frost = new float[MaskSize * MaskSize];
            var leave = new float[MaskSize * MaskSize];
            for (int v = 0; v < Variants; v++)
            {
                for (int y = 0; y < MaskSize; y++)
                {
                    for (int x = 0; x < MaskSize; x++)
                    {
                        var q = new Vector2((x + 0.5f) / MaskSize * 2f - 1f, (y + 0.5f) / MaskSize * 2f - 1f);
                        // The coldest point takes the frost first.
                        frost[y * MaskSize + x] = -Coldness(v, q);
                        leave[y * MaskSize + x] = ErosionTime(v, q);
                    }
                }
                Equalise(frost);
                Equalise(leave);
                for (int y = 0; y < MaskSize; y++)
                {
                    for (int x = 0; x < MaskSize; x++)
                    {
                        int i = y * MaskSize + x;
                        px[y * width + v * MaskSize + x] = new Color32(
                            (byte)Mathf.RoundToInt(frost[i] * 255f), (byte)Mathf.RoundToInt(leave[i] * 255f), 0, 255);
                    }
                }
            }
            maskAtlas = new Texture2D(width, MaskSize, TextureFormat.RGBA32, false, true);
            maskAtlas.hideFlags = HideFlags.HideAndDontSave;
            maskAtlas.filterMode = FilterMode.Bilinear;
            maskAtlas.wrapMode = TextureWrapMode.Clamp;
            maskAtlas.SetPixels32(px);
            maskAtlas.Apply(false, true);
            return maskAtlas;
        }

        /// <summary>How cold a point of the face (-1..1) is: the corners and edges most, a few
        /// irregular patches early, the last warm core least. A little low wobble keeps the front
        /// from ever being a plain gradient.</summary>
        private static float Coldness(int variant, Vector2 q)
        {
            float[] core = CoreShapes[variant];
            float box = Mathf.Max(Mathf.Abs(q.x), Mathf.Abs(q.y));
            float edge = 0.65f * box + 0.35f * Mathf.Min(1f, q.magnitude / 1.4142f);
            float d = CoreDistance(core, 0, q);
            if (core.Length >= 6)
            {
                d = Mathf.Min(d, CoreDistance(core, 4, q));
            }
            // The patches never reach into the core itself.
            float guard = Smooth((d - 0.2f) / 0.35f);
            float patches = 0f;
            float[] p = FrostPatches[variant];
            for (int i = 0; i + 3 < p.Length; i += 4)
            {
                float r = new Vector2(q.x - p[i], q.y - p[i + 1]).magnitude / p[i + 2];
                if (r < 1f)
                {
                    patches += p[i + 3] * (1f - r * r) * (1f - r * r);
                }
            }
            float wobble = 0.03f * Mathf.Sin(2.9f * q.x + variant * 1.7f) * Mathf.Sin(2.4f * q.y + variant * 2.3f);
            return 0.55f * edge + 0.45f * Mathf.Min(d / 1.6f, 1.2f) + patches * guard + wobble;
        }

        /// <summary>One of eight orientations as the rows of a 2x2 matrix: mirrored left-right
        /// when bit 2 is set, then turned a quarter per step of the low two bits.</summary>
        private static Vector4 Axes(int turn)
        {
            float m = (turn & 4) != 0 ? -1f : 1f;
            switch (turn & 3)
            {
                case 1: return new Vector4(0f, -1f, m, 0f);
                case 2: return new Vector4(-m, 0f, 0f, -1f);
                case 3: return new Vector4(0f, 1f, -m, 0f);
                default: return new Vector4(m, 0f, 0f, 1f);
            }
        }

        private static float CoreDistance(float[] core, int at, Vector2 q)
        {
            return new Vector2((q.x - core[at]) / core[2], (q.y - core[at + 1]) / core[3]).magnitude;
        }

        /// <summary>How soon a point's mass sublimates: the nearest of a few large lobes, each
        /// growing from its origin at its own pace; the top goes a touch sooner (vapour rises).</summary>
        private static float ErosionTime(int variant, Vector2 q)
        {
            float[] lobes = ErosionLobes[variant];
            float best = float.MaxValue;
            for (int i = 0; i + 3 < lobes.Length; i += 4)
            {
                var from = new Vector2(lobes[i], lobes[i + 1]);
                // A lobe spreads a third further along the outline than into the face, so the
                // silhouette recedes rather than being bitten out in round arcs.
                Vector2 inward = from.sqrMagnitude > 1e-8f ? -from.normalized : Vector2.up;
                Vector2 rel = q - from;
                float along = Vector2.Dot(rel, inward);
                float across = rel.x * inward.y - rel.y * inward.x;
                float d = Mathf.Sqrt(along * along + across * across * 0.5625f) / lobes[i + 2] + lobes[i + 3];
                best = Mathf.Min(best, d);
            }
            // Two gentle waves, so a bite is never a clean circular arc - irregular, not noisy.
            float wobble = 0.05f * Mathf.Sin(2.6f * q.x + variant * 1.3f) * Mathf.Sin(2.2f * q.y + variant * 0.9f)
                + 0.025f * Mathf.Sin(4.1f * q.x + variant * 2.1f) * Mathf.Sin(3.7f * q.y + variant * 1.1f);
            return best - 0.06f * q.y + wobble;
        }

        /// <summary>Replaces every value with its rank, 0..1.</summary>
        private static void Equalise(float[] values)
        {
            int n = values.Length;
            var keys = (float[])values.Clone();
            var order = new int[n];
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
            }
            System.Array.Sort(keys, order);
            for (int r = 0; r < n; r++)
            {
                values[order[r]] = r / (float)(n - 1);
            }
        }

        // =================================================================== renderers

        private static void Place(SpriteRenderer r, Vector2 at, float size, Color colour, float alpha,
            float angle)
        {
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            r.transform.localScale = new Vector3(size, size, 1f);
        }

        private SpriteRenderer Rent(Sprite sprite, int order, Material material)
        {
            SpriteRenderer r;
            if (spareRenderers.Count > 0)
            {
                r = spareRenderers.Pop();
            }
            else
            {
                var go = new GameObject("Cryo");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plainMaterial == null)
                {
                    plainMaterial = r.sharedMaterial;
                }
            }
            // A pooled renderer may have last worn a water tile's material or the cryo block.
            Material wanted = material != null ? material : plainMaterial;
            if (wanted != null && r.sharedMaterial != wanted)
            {
                r.sharedMaterial = wanted;
            }
            r.SetPropertyBlock(null);
            r.maskInteraction = SpriteMaskInteraction.None;
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = Clear;
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = Vector3.one;
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
            spareRenderers.Push(r);
        }

        /// <summary>A ribbon mesh from the pool, or a new one: its topology and UVs never change
        /// (u across, v along), only its vertices and colours.</summary>
        private Strip RentStrip(int order)
        {
            Strip s;
            if (spareStrips.Count > 0)
            {
                s = spareStrips.Pop();
            }
            else
            {
                var go = new GameObject("Vapour");
                go.transform.SetParent(transform, false);
                var filter = go.AddComponent<MeshFilter>();
                s = new Strip { Renderer = go.AddComponent<MeshRenderer>(), Mesh = new Mesh() };
                s.Mesh.hideFlags = HideFlags.HideAndDontSave;
                s.Mesh.MarkDynamic();
                var vertices = new Vector3[StripPoints * 2];
                var uvs = new Vector2[StripPoints * 2];
                var triangles = new int[(StripPoints - 1) * 6];
                for (int p = 0; p < StripPoints; p++)
                {
                    float along = p / (float)(StripPoints - 1);
                    uvs[p * 2] = new Vector2(0f, along);
                    uvs[p * 2 + 1] = new Vector2(1f, along);
                }
                for (int p = 0; p < StripPoints - 1; p++)
                {
                    int t = p * 6;
                    int v = p * 2;
                    triangles[t] = v;
                    triangles[t + 1] = v + 2;
                    triangles[t + 2] = v + 1;
                    triangles[t + 3] = v + 1;
                    triangles[t + 4] = v + 2;
                    triangles[t + 5] = v + 3;
                }
                s.Mesh.vertices = vertices;
                s.Mesh.uv = uvs;
                s.Mesh.colors = new Color[StripPoints * 2];
                s.Mesh.triangles = triangles;
                filter.sharedMesh = s.Mesh;
                s.Renderer.sharedMaterial = vapourMaterial;
                s.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.Renderer.receiveShadows = false;
            }
            s.Renderer.sortingOrder = order;
            s.Renderer.enabled = false;
            return s;
        }

        private void ReturnStrip(Strip s)
        {
            if (s == null)
            {
                return;
            }
            s.Renderer.enabled = false;
            spareStrips.Push(s);
        }

        private void Release(Batch batch)
        {
            for (int i = 0; i < batch.Cubes.Count; i++)
            {
                Cube c = batch.Cubes[i];
                Return(c.Body);
                Return(c.Shadow);
                Return(c.Residue);
                for (int v = 0; v < c.Vapours.Count; v++)
                {
                    ReturnStrip(c.Vapours[v].Strip);
                }
                for (int f = 0; f < c.Flecks.Count; f++)
                {
                    Return(c.Flecks[f].Renderer);
                }
            }
        }

        // =================================================================== shared art

        private static Sprite softSquareSprite;

        private static Sprite fleckSprite;

        /// <summary>The shadow and the haze: a rounded square whose edge softens inward.</summary>
        private static Sprite SoftSquareSprite()
        {
            if (softSquareSprite != null)
            {
                return softSquareSprite;
            }
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float sd = RoundedBox((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f, 0.2f);
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(-sd / 0.12f) * 255f));
                }
            }
            softSquareSprite = MakeSprite(n, n, px, n);
            return softSquareSprite;
        }

        /// <summary>A frost fleck: a tiny soft diamond - a crystal of dust, not a star, a
        /// snowflake or a square of confetti.</summary>
        private static Sprite FleckSprite()
        {
            if (fleckSprite != null)
            {
                return fleckSprite;
            }
            const int n = 16;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / n - 0.5f) * 2f;
                    float v = Mathf.Abs((y + 0.5f) / n - 0.5f) * 2f;
                    float a = Mathf.Clamp01((1f - (u * 1.15f + v)) / 0.45f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            fleckSprite = MakeSprite(n, n, px, n);
            return fleckSprite;
        }

        private static Sprite MakeSprite(int w, int h, Color32[] px, float ppu)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        }

        /// <summary>Signed distance to a rounded square of half-size 0.5: negative inside.</summary>
        private static float RoundedBox(float u, float v, float radius)
        {
            float dx = Mathf.Abs(u) - (0.5f - radius);
            float dy = Mathf.Abs(v) - (0.5f - radius);
            float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)
                + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
            return outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
        }
    }
}
