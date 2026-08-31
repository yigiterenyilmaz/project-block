// PURPOSE: "Besleme"'s patch - the bit of board its creature lives in and feeds from. This class
// owns the SHAPE and the EVENTS; the surface itself lives in ProjectBlock/CreatureNest.shader.
//
// THE SPLIT IS THE POINT. What this effect has to be - a clean contour at any zoom AND a dense
// interior that moves - cannot both come off a texture the CPU repaints. Matching the screen would
// be roughly eighty texels per cell and three quarters of a million pixels rewritten every frame.
// So the CPU uploads the region ONCE as a signed distance field and then only hands the shader a
// clock and a handful of feed points; every pixel of gel, flow, membrane and sheen is evaluated on
// the GPU.
//
// THE FIELD IS COMPUTED, NOT RASTERISED. Each cell is a rounded box whose exact distance is known
// in closed form; the region is their SMOOTH UNION. Nothing is ever drawn into a bitmap and
// measured afterwards, which matters because the previous version did exactly that - mask, blur,
// threshold, chamfer - and every one of those steps snaps to the texel grid. The stored numbers
// were then quantised distances to a staircase, and no amount of filtering recovers a boundary
// that was thrown away. Computed instead, the values are exact, so bilinear filtering between two
// texels lands on the true curve and the contour is smooth at any zoom.
//
// A DISTANCE FIELD, NOT A MASK. The texture stores how far each texel is from the region boundary,
// not whether it is inside. That is what killed the staircase: a mask magnified eight times shows
// its texels, while a distance field interpolates - halfway between two texels really is halfway to
// the edge - so twenty-four texels per cell resolves the boundary to well under a screen pixel, and
// the shader anti-aliases what is left against fwidth.
//
// CONNECTED, STILL. Every cell of the region goes into one mask before the distance is measured, so
// touching cells have no boundary between them and a five-cell cross is one pool. The outline is
// rounded by an opening then a closing, which gives the outside corners and the inside ones of an L
// their own radius without turning the footprint into a blob.
//
// NOT THE INFECTION AND NOT THE QUARANTINE. The infection is a small green core counting down; the
// quarantine is a dead matte film that takes light out. This is warm, full, moving and inviting -
// the one region on the board the player is supposed to WANT to use.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The living nest "Besleme"'s creature feeds in.</summary>
    public sealed class CreatureNestView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            /// <summary>How much habitat shows over an EMPTY cell. LOW - the slot has to keep
            /// looking like an empty slot that will take a block, and a coordinate marker that
            /// fills its cell has stopped being a marker.</summary>
            public static float Opacity = 0.24f;

            /// <summary>And what survives in the middle of a cell that HAS a block, against what
            /// survives at its rim. The rim is what carries the zone once a cube covers it.</summary>
            public static float OccupiedOpacity = 0.16f;

            public static float OccupiedEdge = 0.62f;

            /// <summary>How much of the internal detail is pushed away from cell centres toward
            /// their edges. The centre is where a block lands, so it is kept quiet.</summary>
            public static float CentreCalm = 0.68f;

            /// <summary>Colours. Deliberately four of them: a single pink at two brightnesses is
            /// what reads as a flat fill however well it is shaded.</summary>
            public static Color GelColor = new Color(0.52f, 0.21f, 0.32f);

            public static Color DeepColor = new Color(0.26f, 0.12f, 0.28f);

            public static Color FlowColor = new Color(0.76f, 0.39f, 0.42f);

            public static Color GlowColor = new Color(0.93f, 0.78f, 0.80f);

            public static Color MembraneColor = new Color(0.68f, 0.34f, 0.45f);

            public static float DepthStrength = 0.90f;

            public static float DensityVariation = 0.62f;

            /// <summary>Below one this pushes the density toward its extremes, so the pockets are
            /// distinct concentrations rather than an even wobble about the middle.</summary>
            public static float DensityContrast = 0.72f;

            public static float DensityScale = 0.62f;

            // ------------------------------------------------------------------ currents
            public static float FlowStrength = 1.4f;

            /// <summary>How much a nutrient band also THICKENS the gel. Measured: without this
            /// the currents move but barely register, because the normalised colour blend cancels
            /// most of the gain as their weight grows. With it the interior tonal range went from
            /// 84 back up past 100 while the seams stayed weak.</summary>
            public static float FlowThickness = 0.75f;

            /// <summary>Noise periods per CELL. Measured: below about two, a five-cell region
            /// contains barely one vein and the flow is invisible however strong it is - the
            /// currents were being tuned when the problem was that there were not any.</summary>
            public static float FlowScale = 2.4f;

            /// <summary>Slow. This is a thick nutrient gel, not water.</summary>
            public static float FlowSpeed = 0.075f;

            public static float FlowWarp = 0.62f;

            public static float FlowWarpScale = 0.85f;

            /// <summary>How tightly the currents narrow into veins. Applied to the RIDGE,
            /// so higher is thinner and sharper rather than merely dimmer.</summary>
            public static float FlowContrast = 2.4f;

            /// <summary>How hard the currents turn to run ALONG the region instead of across it.
            /// Taken from the distance field's own gradient, so it adapts to whatever footprint
            /// the joker hands over without anything here knowing the shape.</summary>
            public static float FlowShapeBias = 1.6f;

            // ------------------------------------------------------------------ membrane
            /// <summary>In CELLS. The rim is a THICKENING of the gel, not an outline on it.</summary>
            public static float MembraneWidth = 0.038f;

            public static float MembraneFalloff = 0.115f;

            public static float MembraneStrength = 0.20f;

            /// <summary>How much the rim's thickness wanders, so it never reads as a stroke.</summary>
            public static float MembraneVariation = 0.48f;

            public static float SheenStrength = 0.15f;

            public static float SheenWidth = 0.38f;

            /// <summary>ONE broad highlight across the whole region, slowly moving - the thing
            /// that makes it read as a single wet surface instead of several lit panels.</summary>
            public static float SheenBroad = 0.18f;

            public static float SheenScale = 0.55f;

            public static float SheenMotion = 0.045f;

            public static float Breathing = 0.06f;

            /// <summary>How many screen pixels the contour is softened over. One is a clean
            /// anti-aliased edge; more only makes it woolly.</summary>
            public static float EdgeAA = 1.15f;

            /// <summary>Corner rounding of one cell's box, in cells. Small: this is a corner
            /// being softened, not a shape being invented.</summary>
            public static float CornerRadius = 0.11f;

            /// <summary>How softly two neighbouring cells merge, in cells. It is what fills the
            /// notch where two rounded boxes meet and rounds the inside corner of an L - the
            /// concave case a plain union leaves as a sharp cut.</summary>
            public static float SmoothUnion = 0.15f;

            /// <summary>How much the board grid is read back through the gel, and how wide those
            /// seams are in cells. This is what says FIVE CELLS instead of one shape - a
            /// continuous surface with no internal structure is an object, not an area.</summary>
            public static float CellSeam = 0.16f;

            public static float CellSeamWidth = 0.075f;

            /// <summary>How far the seam is spread beyond its width. Wide and weak reads as the
            /// grid lying UNDER the gel; narrow and strong reads as a bevel around every cell,
            /// which is what made five cells look like five panels stuck together.</summary>
            public static float CellSeamSoftness = 3.2f;

            // ------------------------------------------------------------------ specks
            public static float BubbleCellsEach = 0.85f;

            public static float BubbleSizeMin = 0.040f;

            public static float BubbleSizeMax = 0.075f;

            public static float BubbleRise = 0.09f;

            public static float BubbleDrift = 0.05f;

            /// <summary>Individually faint. The density is what fills the pool, not the
            /// brightness of any one speck.</summary>
            public static float BubbleOpacity = 0.30f;

            /// <summary>What fraction of the specks are MOTES rather than bubbles: smaller, paler
            /// and brighter, so the suspended detail is not all one thing.</summary>
            public static float MoteShare = 0.42f;

            public static Color MoteColor = new Color(0.90f, 0.76f, 0.84f);

            public static float MoteBrightness = 0.62f;

            public static float BubbleLifeMin = 1.8f;

            public static float BubbleLifeMax = 3.6f;

            // ------------------------------------------------------------------ feeding
            public static float FeedGlow = 1.0f;

            public static float FeedRadius = 1.05f;

            public static float FeedRipple = 0.55f;

            public static float FeedPull = 0.85f;

            public static float FeedLife = 0.9f;

            public static int FeedSeedCount = 8;

            public static float FeedSeedSpeed = 2.6f;
        }

        /// <summary>Over the board and its cubes, under everything that explodes.</summary>
        private const int NestOrder = 6;

        private const int SpeckOrder = 7;

        /// <summary>Texels per cell in the DISTANCE FIELD. It can be this coarse precisely because
        /// it stores distance: the shader reconstructs the boundary between texels.</summary>
        private const int SdfPixelsPerCell = 24;

        private const float Padding = 0.6f;

        /// <summary>Cells of distance the packed 0..1 covers.</summary>
        private const float DistRange = 1.2f;

        private const int MaxFeeds = 6;

        // =================================================================== state

        private readonly List<GridPos> cells = new List<GridPos>();

        private SpriteRenderer nest;

        private Material material;

        private Texture2D field;

        /// <summary>The field's pixels, kept so occupancy can be rewritten into G without
        /// recomputing the distance in R - the shape rarely changes, but what stands on it
        /// changes every time a block is placed.</summary>
        private Color32[] fieldPixels = new Color32[0];

        private int texW;

        private int texH;

        private float clock;

        private bool built;

        private float spritePpu;

        private int minX;

        private int minY;

        private int width;

        private int height;

        private float cellSize = 1f;

        private Vector2 origin;

        private readonly Vector4[] feedData = new Vector4[MaxFeeds];

        private readonly float[] feedAt = new float[MaxFeeds];

        private static readonly int FeedsId = Shader.PropertyToID("_Feeds");

        private static readonly int TimeId = Shader.PropertyToID("_NestTime");

        // =================================================================== driving it

        public void SetRegion(IReadOnlyList<GridPos> region, GameBoard board,
            System.Func<GridPos, Vector2> toWorld, float cell,
            System.Func<GridPos, bool> occupied)
        {
            if (region == null || region.Count == 0 || board == null || cell <= 0f)
            {
                Clear();
                return;
            }
            minX = board.MinX;
            minY = board.MinY;
            width = board.Width;
            height = board.Height;
            cellSize = cell;
            cells.Clear();
            for (int i = 0; i < region.Count; i++)
            {
                cells.Add(region[i]);
            }
            for (int i = 0; i < MaxFeeds; i++)
            {
                feedAt[i] = -1f;
                feedData[i] = Vector4.zero;
            }
            EnsureRenderer(toWorld);
            BuildField();
            PushStyle();
            RebuildSpecks();
            built = true;
        }

        public void Clear()
        {
            cells.Clear();
            built = false;
            if (nest != null)
            {
                nest.enabled = false;
            }
            for (int i = 0; i < specks.Length; i++)
            {
                specks[i].Live = false;
                if (specks[i].R != null)
                {
                    specks[i].R.enabled = false;
                }
            }
        }

        /// <summary>A cube was eaten here. The pool swallows, draws its currents in and settles.</summary>
        public void PlayFeed(Vector2 world)
        {
            if (!built)
            {
                return;
            }
            int slot = -1;
            float oldest = float.MaxValue;
            for (int i = 0; i < MaxFeeds; i++)
            {
                if (feedAt[i] < 0f || clock - feedAt[i] > Style.FeedLife)
                {
                    slot = i;
                    break;
                }
                if (feedAt[i] < oldest)
                {
                    oldest = feedAt[i];
                    slot = i;
                }
            }
            feedAt[slot] = clock;
            Vector2 local = (world - origin) / cellSize;
            feedData[slot] = new Vector4(local.x, local.y, 0f, 1f);

            // Specks rush in and are swallowed - food arriving, not debris leaving.
            for (int i = 0; i < Style.FeedSeedCount; i++)
            {
                int s = FreeSpeck();
                if (s < 0)
                {
                    break;
                }
                Vector2 from = world + Random.insideUnitCircle.normalized
                    * Random.Range(0.6f, 1.5f) * cellSize;
                specks[s].Pos = from;
                specks[s].Vel = (world - from).normalized * Style.FeedSeedSpeed * cellSize;
                specks[s].Size = Random.Range(Style.BubbleSizeMin, Style.BubbleSizeMax)
                    * cellSize * 1.5f;
                specks[s].Life = 0.34f;
                specks[s].Age = 0f;
                specks[s].Seed = Random.value * 10f;
                specks[s].Sway = 0f;
                specks[s].Feeding = true;
                specks[s].Live = true;
                specks[s].R.enabled = true;
            }
        }

        // =================================================================== the field

        private bool InRegion(int x, int y)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].X - minX == x && cells[i].Y - minY == y)
                {
                    return true;
                }
            }
            return false;
        }

        private void EnsureRenderer(System.Func<GridPos, Vector2> toWorld)
        {
            int pad = Mathf.CeilToInt(Padding * SdfPixelsPerCell);
            int w = width * SdfPixelsPerCell + pad * 2;
            int h = height * SdfPixelsPerCell + pad * 2;
            if (field == null || texW != w || texH != h)
            {
                if (field != null)
                {
                    Destroy(field);
                }
                texW = w;
                texH = h;
                // LINEAR, and this is not optional. A Texture2D defaults to sRGB, so in a linear
                // colour project Unity gamma-DECODES it on the way into the shader - and this
                // texture is not colour, it is DISTANCE. Decoded, the stored 0.5 that means "on
                // the boundary" arrives as 0.214, which puts the boundary two thirds of a cell
                // inside the region: every arm of a shape one cell wide falls under the threshold
                // and vanishes, leaving only the deepest part of a junction. That is precisely
                // the four-pointed star this drew instead of a habitat.
                field = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
                field.hideFlags = HideFlags.HideAndDontSave;
                field.filterMode = FilterMode.Bilinear;   // the field is MEANT to interpolate
                field.wrapMode = TextureWrapMode.Clamp;
                if (nest != null)
                {
                    Destroy(nest.gameObject);
                    nest = null;
                }
            }
            if (nest == null)
            {
                var go = new GameObject("Nest");
                go.transform.SetParent(transform, false);
                nest = go.AddComponent<SpriteRenderer>();
                nest.sortingOrder = NestOrder;
                spritePpu = 0f;
            }
            if (material == null)
            {
                Shader shader = Shader.Find("ProjectBlock/CreatureNest");
                material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
                material.hideFlags = HideFlags.HideAndDontSave;
                nest.sharedMaterial = material;
            }
            float ppu = SdfPixelsPerCell / cellSize;
            if (!Mathf.Approximately(ppu, spritePpu))
            {
                spritePpu = ppu;
                nest.sprite = Sprite.Create(field, new Rect(0, 0, texW, texH),
                    new Vector2(0.5f, 0.5f), ppu);
            }
            Vector2 low = toWorld(new GridPos(minX, minY));
            origin = low - new Vector2(cellSize * (0.5f + Padding), cellSize * (0.5f + Padding));
            Vector2 centre = origin
                + new Vector2(texW, texH) * (cellSize / SdfPixelsPerCell) * 0.5f;
            nest.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
            nest.color = Color.white;
            nest.enabled = true;
        }

        /// <summary>Exact distance to one cell's rounded box, negative inside. Closed form -
        /// no rasterising, so it carries no grid of its own.</summary>
        private static float RoundedBox(Vector2 p, float half, float radius)
        {
            float qx = Mathf.Abs(p.x) - half + radius;
            float qy = Mathf.Abs(p.y) - half + radius;
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f)
                + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        /// <summary>Polynomial smooth minimum: a union that blends instead of creasing.</summary>
        private static float SmoothMin(float a, float b, float k)
        {
            if (k <= 0.0001f)
            {
                return Mathf.Min(a, b);
            }
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        /// <summary>
        /// Evaluates the region's true distance at every texel. Each cell contributes a rounded
        /// box whose distance is known exactly, and they are joined with a smooth minimum - so
        /// what is stored is a real distance function, not a measurement taken off a bitmap.
        ///
        /// This is the whole fix for the staircase. Rasterising the cells and running a distance
        /// transform over them makes every stored value a distance to a GRID OF TEXELS, and the
        /// zero crossing then follows that grid however finely it is filtered afterwards. Computed
        /// analytically there is no grid in the numbers at all: bilinear filtering between two
        /// texels lands on the true curve, and the contour stays smooth however far it is zoomed.
        /// </summary>
        private void BuildField()
        {
            int pad = Mathf.CeilToInt(Padding * SdfPixelsPerCell);
            var px2 = new Color32[texW * texH];
            float inv = 1f / SdfPixelsPerCell;
            float radius = Style.CornerRadius;
            float k = Style.SmoothUnion;
            float far = DistRange * 2f;

            for (int py = 0; py < texH; py++)
            {
                // Texel centre, in cells from the first cell's own bottom-left corner.
                float wy = (py + 0.5f - pad) * inv;
                int row = py * texW;
                for (int px = 0; px < texW; px++)
                {
                    float wx = (px + 0.5f - pad) * inv;
                    float sd = far;                     // negative inside, by SDF convention
                    for (int c = 0; c < cells.Count; c++)
                    {
                        // Cell (x,y) occupies [x, x+1] in these coordinates, so its centre is at
                        // x + 0.5 and its half size is 0.5.
                        float ox = wx - (cells[c].X - minX + 0.5f);
                        float oy = wy - (cells[c].Y - minY + 0.5f);
                        if (ox * ox + oy * oy > 9f)
                        {
                            continue;                   // far enough that it cannot win the min
                        }
                        sd = SmoothMin(sd, RoundedBox(new Vector2(ox, oy), 0.5f, radius), k);
                    }
                    // Stored positive-inside with the boundary at 0.5.
                    float d = -sd;
                    float packed = Mathf.Clamp01(d / (2f * DistRange) + 0.5f);
                    var v = (byte)Mathf.RoundToInt(packed * 255f);
                    px2[row + px] = new Color32(v, 0, v, 255);   // G is filled by occupancy
                }
            }
            fieldPixels = px2;
            field.SetPixels32(px2);
            field.Apply(false, false);
        }

        /// <summary>Samples the per-cell occupancy smoothly, so a cube fades in across its own
        /// cell rather than switching on at its border.</summary>
        private float Bilinear(float[] grid, float fx, float fy)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, height - 1);
            int x1 = Mathf.Clamp(x0 + 1, 0, width - 1);
            int y1 = Mathf.Clamp(y0 + 1, 0, height - 1);
            float tx = Mathf.Clamp01(fx - x0);
            float ty = Mathf.Clamp01(fy - y0);
            float a = Mathf.Lerp(grid[y0 * width + x0], grid[y0 * width + x1], tx);
            float b = Mathf.Lerp(grid[y1 * width + x0], grid[y1 * width + x1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        /// <summary>
        /// Writes which cells currently hold a cube into the field's GREEN channel. Separate from
        /// the distance because they change on completely different clocks: the shape only when
        /// the joker's region does, occupancy on every placement. The shader reads it through the
        /// same bilinear sample, so a cube's influence fades across its own cell rather than
        /// switching on at its border.
        /// </summary>
        public void RefreshOccupancy(System.Func<GridPos, bool> occupied)
        {
            if (!built || field == null || fieldPixels.Length != texW * texH)
            {
                return;
            }
            var perCell = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    perCell[y * width + x] =
                        occupied != null && occupied(new GridPos(minX + x, minY + y)) ? 1f : 0f;
                }
            }
            int pad = Mathf.CeilToInt(Padding * SdfPixelsPerCell);
            bool changed = false;
            for (int py = 0; py < texH; py++)
            {
                float fy = (py - pad) / (float)SdfPixelsPerCell - 0.5f;
                int row = py * texW;
                for (int px = 0; px < texW; px++)
                {
                    float fx = (px - pad) / (float)SdfPixelsPerCell - 0.5f;
                    var g = (byte)Mathf.RoundToInt(Mathf.Clamp01(Bilinear(perCell, fx, fy)) * 255f);
                    if (fieldPixels[row + px].g != g)
                    {
                        fieldPixels[row + px].g = g;
                        changed = true;
                    }
                }
            }
            if (changed)
            {
                field.SetPixels32(fieldPixels);
                field.Apply(false, false);
            }
        }

        // =================================================================== material

        private void PushStyle()
        {
            if (material == null)
            {
                return;
            }
            material.SetTexture("_MainTex", field);
            material.SetVector("_SizeCells",
                new Vector4(texW / (float)SdfPixelsPerCell, texH / (float)SdfPixelsPerCell, 0f, 0f));
            material.SetFloat("_DistRange", DistRange);
            material.SetColor("_GelColor", Style.GelColor);
            material.SetColor("_DeepColor", Style.DeepColor);
            material.SetColor("_FlowColor", Style.FlowColor);
            material.SetColor("_GlowColor", Style.GlowColor);
            material.SetColor("_MembraneColor", Style.MembraneColor);
            material.SetFloat("_Opacity", Style.Opacity);
            material.SetFloat("_DepthStrength", Style.DepthStrength);
            material.SetFloat("_DensityVariation", Style.DensityVariation);
            material.SetFloat("_DensityScale", Style.DensityScale);
            material.SetFloat("_FlowStrength", Style.FlowStrength);
            material.SetFloat("_FlowScale", Style.FlowScale);
            material.SetFloat("_FlowSpeed", Style.FlowSpeed);
            material.SetFloat("_FlowWarp", Style.FlowWarp);
            material.SetFloat("_FlowWarpScale", Style.FlowWarpScale);
            material.SetFloat("_FlowContrast", Style.FlowContrast);
            material.SetFloat("_FlowShapeBias", Style.FlowShapeBias);
            material.SetFloat("_FlowThickness", Style.FlowThickness);
            material.SetFloat("_MembraneWidth", Style.MembraneWidth);
            material.SetFloat("_MembraneFalloff", Style.MembraneFalloff);
            material.SetFloat("_MembraneStrength", Style.MembraneStrength);
            material.SetFloat("_MembraneVariation", Style.MembraneVariation);
            material.SetFloat("_SheenStrength", Style.SheenStrength);
            material.SetFloat("_SheenWidth", Style.SheenWidth);
            material.SetFloat("_Breathing", Style.Breathing);
            material.SetFloat("_CellSeam", Style.CellSeam);
            material.SetFloat("_CellSeamWidth", Style.CellSeamWidth);
            material.SetFloat("_CellSeamSoftness", Style.CellSeamSoftness);
            material.SetFloat("_CentreCalm", Style.CentreCalm);
            material.SetFloat("_OccupiedOpacity", Style.OccupiedOpacity);
            material.SetFloat("_OccupiedEdge", Style.OccupiedEdge);
            material.SetFloat("_SheenBroad", Style.SheenBroad);
            material.SetFloat("_SheenScale", Style.SheenScale);
            material.SetFloat("_SheenMotion", Style.SheenMotion);
            material.SetFloat("_DensityContrast", Style.DensityContrast);
            // Where a cell boundary falls in the texture: the field starts Padding cells before
            // the first cell, so subtracting it puts the boundaries back on whole numbers.
            material.SetFloat("_CellPhase", Padding);
            material.SetFloat("_EdgeAA", Style.EdgeAA);
            material.SetFloat("_FeedGlow", Style.FeedGlow);
            material.SetFloat("_FeedRadius", Style.FeedRadius);
            material.SetFloat("_FeedRipple", Style.FeedRipple);
            material.SetFloat("_FeedPull", Style.FeedPull);
            material.SetFloat("_FeedLife", Style.FeedLife);
        }

        // =================================================================== specks

        private struct Speck
        {
            public SpriteRenderer R;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Size;
            public float Age;
            public float Life;
            public float Seed;
            public float Sway;
            public float Depth;
            public bool Mote;
            public bool Feeding;
            public bool Live;
        }

        private Speck[] specks = new Speck[0];

        private static Sprite speckSprite;

        private static Sprite SpeckSprite()
        {
            if (speckSprite != null)
            {
                return speckSprite;
            }
            const int n = 24;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    // A bubble, not a dot: bright at its rim, nearly hollow in the middle.
                    float shell = Mathf.Exp(-30f * (r - 0.64f) * (r - 0.64f));
                    float fill = Mathf.Exp(-2.4f * r * r) * 0.30f;
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(shell + fill) * 255f));
                }
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            speckSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return speckSprite;
        }

        private int FreeSpeck()
        {
            for (int i = 0; i < specks.Length; i++)
            {
                if (!specks[i].Live)
                {
                    return i;
                }
            }
            return -1;
        }

        private void RebuildSpecks()
        {
            int want = Mathf.Clamp(
                Mathf.RoundToInt(cells.Count / Mathf.Max(Style.BubbleCellsEach, 0.05f))
                + Style.FeedSeedCount * 2, 0, 72);
            while (specks.Length < want)
            {
                var next = new Speck[specks.Length + 1];
                System.Array.Copy(specks, next, specks.Length);
                var go = new GameObject("Speck" + specks.Length);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = SpeckSprite();
                r.sortingOrder = SpeckOrder;
                r.enabled = false;
                next[specks.Length].R = r;
                specks = next;
            }
            int drifting = Mathf.Max(0, want - Style.FeedSeedCount * 2);
            for (int i = 0; i < specks.Length; i++)
            {
                if (i < drifting && !specks[i].Live)
                {
                    Respawn(i);
                }
            }
        }

        private void Respawn(int i)
        {
            if (cells.Count == 0)
            {
                specks[i].Live = false;
                specks[i].R.enabled = false;
                return;
            }
            GridPos c = cells[Random.Range(0, cells.Count)];
            specks[i].Pos = origin + new Vector2(
                (c.X - minX + Padding + Random.value) * cellSize,
                (c.Y - minY + Padding + Random.value) * cellSize);
            specks[i].Vel = new Vector2(
                Random.Range(-Style.BubbleDrift, Style.BubbleDrift),
                Style.BubbleRise * Random.Range(0.5f, 1.5f)) * cellSize;
            // Depth: some sit deep in the gel and read faint and small, some near the surface.
            // Without it a dozen identical specks all look like they are on the same pane.
            specks[i].Depth = Random.Range(0.35f, 1f);
            specks[i].Size = Random.Range(Style.BubbleSizeMin, Style.BubbleSizeMax)
                * cellSize * specks[i].Depth;
            specks[i].Life = Random.Range(Style.BubbleLifeMin, Style.BubbleLifeMax);
            specks[i].Age = 0f;
            specks[i].Seed = Random.value * 10f;
            specks[i].Sway = Random.Range(0.4f, 1.5f);
            // Bubbles are hollow and pinkish; motes are small, pale and a touch brighter. Two
            // kinds of speck rather than a dozen copies of one.
            specks[i].Mote = Random.value < Style.MoteShare;
            if (specks[i].Mote)
            {
                specks[i].Size *= 0.55f;
            }
            specks[i].Feeding = false;
            specks[i].Live = true;
            specks[i].R.enabled = true;
        }

        private void StepSpecks(float dt)
        {
            for (int i = 0; i < specks.Length; i++)
            {
                if (!specks[i].Live)
                {
                    continue;
                }
                specks[i].Age += dt;
                float k = specks[i].Age / specks[i].Life;
                if (k >= 1f)
                {
                    if (specks[i].Feeding)
                    {
                        specks[i].Live = false;
                        specks[i].R.enabled = false;   // swallowed
                    }
                    else
                    {
                        Respawn(i);
                    }
                    continue;
                }
                specks[i].Pos += specks[i].Vel * dt;
                if (!specks[i].Feeding)
                {
                    specks[i].Pos.x += Mathf.Sin((specks[i].Age * specks[i].Sway
                        + specks[i].Seed) * Mathf.PI * 2f) * Style.BubbleDrift * cellSize * dt;
                }
                Transform t = specks[i].R.transform;
                t.localPosition = new Vector3(specks[i].Pos.x, specks[i].Pos.y, 0f);
                float grow = specks[i].Feeding ? 1f - k * 0.7f : 1f;
                float s = specks[i].Size * grow;
                t.localScale = new Vector3(s, s, 1f);
                Color c = specks[i].Feeding ? Style.GlowColor
                    : (specks[i].Mote ? Style.MoteColor : Style.MembraneColor);
                float depth = specks[i].Feeding ? 1f : specks[i].Depth;
                float weight = specks[i].Mote ? Style.MoteBrightness : 1f;
                c.a = Style.BubbleOpacity * weight * depth
                    * (k < 0.15f ? k / 0.15f : Mathf.Min(1f, (1f - k) / 0.3f));
                specks[i].R.color = c;
            }
        }

        // =================================================================== running

        private void Update()
        {
            if (!built)
            {
                return;
            }
            float dt = Time.deltaTime;
            clock += dt;
            StepSpecks(dt);

            // The only per-frame work: a clock and up to six feed points. Everything the player
            // actually sees on the surface is evaluated on the GPU from these.
            for (int i = 0; i < MaxFeeds; i++)
            {
                if (feedAt[i] < 0f)
                {
                    feedData[i].w = 0f;
                    continue;
                }
                float age = clock - feedAt[i];
                if (age > Style.FeedLife)
                {
                    feedAt[i] = -1f;
                    feedData[i].w = 0f;
                    continue;
                }
                feedData[i].z = age;
                feedData[i].w = 1f;
            }
            if (material != null)
            {
                material.SetFloat(TimeId, clock);
                material.SetVectorArray(FeedsId, feedData);
            }
        }

        private void OnDestroy()
        {
            if (field != null)
            {
                Destroy(field);
            }
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
