// PURPOSE: The four small things that happen AROUND the drawn overload - ash, micro sparks, heat
// haze and cooling embers. The sheet animation is the event and stays the star; none of this is
// allowed to compete with it. Everything here is small, local to the cable, and short.
//
// FOUR SEPARATE MOTION LANGUAGES, kept deliberately distinct, because that separation is what
// stops a pile of particles reading as one generic "effect":
//   ash     - slow, soft, drifting up, turning over, a full second and more
//   flecks  - thrown hard into treacle: sharp for an instant, then stopped, cooling as they go
//   sparks  - very fast, tiny, sharp, gone inside a tenth of a second
//   haze    - barely there, wobbling in place, local patches
//   embers  - still, dim, flickering out over a few hundred milliseconds
//
// THE HAZE IS NOT REFRACTION. Real heat shimmer bends what is behind it, and that needs a grab
// pass this project's 2D pipeline does not have. So it is a very faint NEUTRAL patch that wobbles
// in place - neutral on purpose, because anything tinted at this size and softness stops reading
// as hot air and starts reading as the full-path glow that was already tried and rejected.
//
// NOTHING SPAWNS AWAY FROM THE CABLE. Every emitter samples a position on the route itself and
// offsets it by a fraction of a cell. The board at large is not involved.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Ash, sparks, haze and embers around a burning circuit.</summary>
    public sealed class CircuitAshFx : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            // ------------------------------------------------------------------ ash
            /// <summary>Ash size in CELLS. Small enough to be debris, not smoke.</summary>
            public static float AshSizeMin = 0.13f;

            public static float AshSizeMax = 0.26f;

            /// <summary>How much of its size a mote loses over its life. Barely any - ash does
            /// not evaporate, it drifts out of sight.</summary>
            public static float AshShrink = 0.12f;

            /// <summary>Upward drift in cells per second, and the sideways wander around it.
            /// Slow: this is the one lane that must never look thrown.</summary>
            public static float AshRiseMin = 0.30f;

            public static float AshRiseMax = 0.62f;

            public static float AshDrift = 0.16f;

            /// <summary>How fast the drift bleeds off, per second. Ash settles into the air
            /// rather than coasting.</summary>
            public static float AshDrag = 1.10f;

            public static float AshSpinMax = 70f;

            public static float AshLifeMin = 0.95f;

            public static float AshLifeMax = 1.70f;

            public static float AshAlpha = 0.88f;

            /// <summary>How far off the cable a mote may be born, in cells.</summary>
            public static float AshSpread = 0.22f;

            /// <summary>The two ends of the charcoal range. Low saturation still - ash with a
            /// hue reads as coloured confetti - but LIGHTER than charcoal really is, and warm
            /// rather than neutral. The board is already near-black, so honestly dark ash is
            /// invisible on it; the flakes have to sit above the board to be seen at all, and the
            /// warmth is what separates them from a cool grey surface.</summary>
            public static Color AshDark = new Color(0.215f, 0.198f, 0.182f);

            public static Color AshLight = new Color(0.445f, 0.405f, 0.362f);

            /// <summary>Ash born per CELL of cable per second, by phase of the drawn animation.
            /// Per cell so a long circuit sheds more than a short one without the density
            /// changing. The rupture is a short hard burst, not a new steady rate.</summary>
            public static float AshRateHeatUp = 0.8f;

            public static float AshRateBlueHot = 2.1f;

            public static float AshRateRupture = 7.5f;

            public static float AshRateFade = 1.6f;

            public static float AshRateDying = 0.7f;

            // ------------------------------------------------------------------ char flecks
            /// <summary>Flecks are the bigger cousins of ash: pieces of burnt CRUST coming off
            /// the surface at the rupture, not smoke and not debris. A handful, once.</summary>
            public static float FleckSizeMin = 0.20f;

            public static float FleckSizeMax = 0.34f;

            public static int FleckCountMin = 3;

            public static int FleckCountMax = 7;

            public static float FleckLifeMin = 0.15f;

            public static float FleckLifeMax = 0.30f;

            /// <summary>Thrown, but into treacle: a high speed against a heavy drag, so a fleck
            /// moves sharply for a moment and then just stops. That shape of motion is what
            /// separates a piece breaking off from a piece being launched.</summary>
            public static float FleckSpeedMin = 1.2f;

            public static float FleckSpeedMax = 2.2f;

            public static float FleckDrag = 8f;

            /// <summary>How much the throw favours upward over sideways.</summary>
            public static float FleckUpBias = 0.55f;

            public static float FleckSpinMax = 260f;

            /// <summary>A fleck leaves hot and COOLS while it flies, over this fraction of its
            /// life. It is the one place a warm colour belongs in this whole system.</summary>
            public static Color FleckHot = new Color(0.88f, 0.44f, 0.17f);

            public static Color FleckChar = new Color(0.175f, 0.160f, 0.150f);

            public static float FleckCoolBy = 0.45f;

            public static float FleckAlpha = 0.95f;

            // ------------------------------------------------------------------ sparks
            /// <summary>Total length of one arc in cells, over two or three segments.</summary>
            public static float SparkLengthMin = 0.20f;

            public static float SparkLengthMax = 0.38f;

            public static float SparkThickness = 0.028f;

            public static float SparkLifeMin = 0.06f;

            public static float SparkLifeMax = 0.14f;

            /// <summary>How far a segment may kink off the last one. Enough to fork, not enough
            /// to curl up into a scribble.</summary>
            public static float SparkKinkDegrees = 55f;

            public static float SparkSpread = 0.16f;

            /// <summary>An arc is not one colour. The core is white hot and the edges are the
            /// blue the sheet's own core burns at, and BOTH are baked into the sprite rather than
            /// tinted on - a flat blue thread reads as a drawn line, a hot-cored one reads as
            /// something electrical letting go.</summary>
            public static Color SparkCore = new Color(1f, 0.98f, 0.94f);

            public static Color SparkEdge = new Color(0.42f, 0.80f, 1f);

            /// <summary>Arcs per cell per second, and only while the core is blue. A couple
            /// alive at a time along the whole cable is the target.</summary>
            public static float SparkRateBlueHot = 0.80f;

            public static float SparkRateRupture = 4.5f;

            // ------------------------------------------------------------------ haze
            public static float HazeSizeMin = 0.55f;

            public static float HazeSizeMax = 0.85f;

            public static float HazeAlpha = 0.115f;

            public static float HazeLifeMin = 0.25f;

            public static float HazeLifeMax = 0.42f;

            /// <summary>How hard and how fast the patch breathes. The wobble is what makes it
            /// air rather than light.</summary>
            public static float HazeWobble = 0.13f;

            public static float HazeWobbleHz = 13f;

            public static float HazeRise = 0.28f;

            public static Color HazeColor = new Color(0.85f, 0.86f, 0.88f);

            /// <summary>The haze is the ONE layer that starts before anything visible does: the
            /// cable is already hot through the orange fuse, and air moving over it is how that
            /// gets said without lighting anything up.</summary>
            public static float HazeRatePreheat = 0.16f;

            public static float HazeRateBlueHot = 0.42f;

            public static float HazeRateRupture = 1.4f;

            // ------------------------------------------------------------------ embers
            public static float EmberSizeMin = 0.065f;

            public static float EmberSizeMax = 0.115f;

            public static float EmberLifeMin = 0.22f;

            public static float EmberLifeMax = 0.50f;

            public static float EmberAlpha = 0.85f;

            public static float EmberFlickerHz = 21f;

            public static float EmberSpread = 0.10f;

            /// <summary>Two kinds of leftover heat: the last of the energy, and plain hot
            /// material. Mixing them stops the residue reading as more blue effect.</summary>
            public static Color EmberHot = new Color(1f, 0.66f, 0.34f);

            public static Color EmberEnergy = new Color(0.62f, 0.90f, 1f);

            public static float EmberEnergyShare = 0.35f;

            // ------------------------------------------------------------------ final release
            /// <summary>When the drawn animation is over, this many places along the cable let
            /// go of a last few flakes.</summary>
            public static int FinalSpotsMin = 2;

            public static int FinalSpotsMax = 5;

            public static int FinalPerSpot = 3;

            /// <summary>How many embers are left glowing when the energy goes.</summary>
            public static int EmberCountMin = 3;

            public static int EmberCountMax = 7;

            /// <summary>And how many bright points sit IN the fresh mark at the rupture itself -
            /// the hot spots, which are gone long before the mark is.</summary>
            public static int HotSpotCountMin = 2;

            public static int HotSpotCountMax = 5;

            // ------------------------------------------------------------------ corners
            /// <summary>How much more likely a corner is to shed than a straight run. Corners are
            /// where a route has character, and where a real conductor would fail first.</summary>
            public static float CornerBias = 1.7f;

            /// <summary>The turn per route sample, in degrees, at which that bias is fully on.</summary>
            public static float CornerTurnFull = 15f;
        }

        /// <summary>Ash and haze sit under the drawn overload (20) so they never wash it out;
        /// sparks and embers sit just above it, because they ARE points of light.</summary>
        private const int AshOrder = 17;

        private const int HazeOrder = 16;

        private const int FleckOrder = 18;

        private const int SparkOrder = 21;

        private const int EmberOrder = 21;

        private const int MaxAsh = 72;

        private const int MaxHaze = 8;

        private const int MaxEmber = 24;

        private const int MaxFleck = 12;

        private const int MaxSpark = 14;

        private const int SparkSegments = 3;

        // =================================================================== sprites

        private static Sprite[] ashSprites;

        private static Sprite sparkSprite;

        private static Sprite hazeSprite;

        private static Sprite emberSprite;

        /// <summary>Four flakes with different outlines. A single shape repeated seventy times is
        /// visible as a repeat however small it is.</summary>
        private static Sprite[] AshSprites()
        {
            if (ashSprites != null)
            {
                return ashSprites;
            }
            var built = new Sprite[4];
            for (int s = 0; s < 4; s++)
            {
                const int n = 24;
                var px = new Color32[n * n];
                float p1 = s * 1.9f;
                float p2 = s * 3.7f + 1.1f;
                float p3 = s * 2.3f + 0.6f;
                // Squashed on one axis, by a different amount each variant. Radial symmetry is
                // what turns a small lumpy blob into a flower - three lobes at 120 degrees reads
                // as a petal however small it gets - so the flake is not round to begin with.
                float sx = 1f + 0.22f * s;
                float sy = 1f - 0.17f * s;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float u = ((x + 0.5f) / n - 0.5f) / sx;
                        float v = ((y + 0.5f) / n - 0.5f) / sy;
                        float d = Mathf.Sqrt(u * u + v * v) * 2f;
                        float a = Mathf.Atan2(v, u);
                        // Harmonics that do not share a common factor, so no lobe count repeats
                        // around the outline and the chip keeps an accidental shape.
                        float edge = 0.66f + 0.16f * Mathf.Sin(2f * a + p1)
                            + 0.13f * Mathf.Sin(3f * a + p2)
                            + 0.08f * Mathf.Sin(5f * a + p3);
                        float k = Mathf.Clamp01((edge - d) / 0.20f);
                        px[y * n + x] = new Color32(255, 255, 255,
                            (byte)Mathf.RoundToInt(k * 255f));
                    }
                }
                built[s] = MakeSprite(px, n, n);
            }
            ashSprites = built;
            return built;
        }

        /// <summary>One segment of an arc: a thin thread, brightest along its middle, tapered at
        /// both ends so two of them join without a bead at the joint.</summary>
        private static Sprite SparkSprite()
        {
            if (sparkSprite != null)
            {
                return sparkSprite;
            }
            const int w = 64;
            const int h = 12;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                float across = Mathf.Exp(-6f * v * v);
                // White hot down the middle, blue at the edges. Baked in, so the renderer only
                // ever has to carry the fade.
                Color c = Color.Lerp(Style.SparkEdge, Style.SparkCore, Mathf.Exp(-9f * v * v));
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float along = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.35f);
                    px[y * w + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(across * along) * 255f));
                }
            }
            sparkSprite = MakeSprite(px, w, h);
            return sparkSprite;
        }

        private static Sprite HazeSprite()
        {
            if (hazeSprite != null)
            {
                return hazeSprite;
            }
            const int n = 48;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float a = Mathf.Max(0f, Mathf.Exp(-2.4f * (u * u + v * v)) - 0.09f);
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            hazeSprite = MakeSprite(px, n, n);
            return hazeSprite;
        }

        private static Sprite EmberSprite()
        {
            if (emberSprite != null)
            {
                return emberSprite;
            }
            const int n = 16;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float a = Mathf.Exp(-5f * (u * u + v * v));
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            emberSprite = MakeSprite(px, n, n);
            return emberSprite;
        }

        private static Sprite MakeSprite(Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            // PPU = width, so the sprite is one unit across at scale 1 and the caller sizes it.
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        }

        // =================================================================== motes

        private struct Mote
        {
            public SpriteRenderer R;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Angle;
            public float Spin;
            public float Age;
            public float Life;
            public float Size;
            public float Alpha;
            public float Seed;
            public bool Live;
        }

        private Mote[] ash = new Mote[0];

        private Mote[] haze = new Mote[0];

        private Mote[] ember = new Mote[0];

        private Mote[] fleck = new Mote[0];

        private struct Spark
        {
            public SpriteRenderer[] Segments;
            public float Age;
            public float Life;
            public bool Live;
        }

        private Spark[] sparks = new Spark[0];

        // =================================================================== the route

        private IReadOnlyList<Vector2> route;

        private IReadOnlyList<float> routeAt;

        private float routeLength;

        private float cell = 1f;

        /// <summary>Cumulative spawn weight along the route, so corners shed more than straights.</summary>
        private float[] spawnCdf = new float[0];

        public void SetRoute(IReadOnlyList<Vector2> path, IReadOnlyList<float> at, float length,
            float cellSize)
        {
            route = path;
            routeAt = at;
            routeLength = length;
            cell = Mathf.Max(cellSize, 0.0001f);
            BuildSpawnWeights();
        }

        /// <summary>
        /// Weights each stretch of cable by how hard it TURNS there. A straight run counts as its
        /// own length; a bend counts as more, so flakes and arcs gather at the corners - which is
        /// where the route has its character, and where a real conductor gives out first.
        /// </summary>
        private void BuildSpawnWeights()
        {
            int n = route != null ? route.Count : 0;
            if (n < 2)
            {
                spawnCdf = new float[0];
                return;
            }
            spawnCdf = new float[n];
            float acc = 0f;
            for (int i = 1; i < n; i++)
            {
                float span = routeAt[i] - routeAt[i - 1];
                float turn = Mathf.Abs(Mathf.DeltaAngle(Heading(i - 1), Heading(i)));
                float w = 1f + Style.CornerBias
                    * Mathf.Clamp01(turn / Mathf.Max(Style.CornerTurnFull, 0.01f));
                acc += span * w;
                spawnCdf[i] = acc;
            }
        }

        private float Heading(int i)
        {
            int j = Mathf.Min(i + 1, route.Count - 1);
            Vector2 d = route[j] - route[Mathf.Max(j - 1, 0)];
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        public bool HasRoute
        {
            get { return route != null && route.Count >= 2 && routeLength > 0f; }
        }

        /// <summary>A point on the cable, pushed off it by up to <paramref name="spreadCells"/>.</summary>
        private Vector2 SampleOnRoute(float spreadCells)
        {
            int n = route.Count;
            if (spawnCdf.Length != n || spawnCdf[n - 1] <= 0f)
            {
                BuildSpawnWeights();
            }
            float d = Random.Range(0f, spawnCdf[n - 1]);
            for (int i = 1; i < n; i++)
            {
                if (d <= spawnCdf[i])
                {
                    float span = spawnCdf[i] - spawnCdf[i - 1];
                    float k = span > 0.0001f ? (d - spawnCdf[i - 1]) / span : 0f;
                    Vector2 p = Vector2.Lerp(route[i - 1], route[i], k);
                    Vector2 dir = (route[i] - route[i - 1]).normalized;
                    var normal = new Vector2(-dir.y, dir.x);
                    return p + normal * Random.Range(-spreadCells, spreadCells) * cell;
                }
            }
            return route[route.Count - 1];
        }

        // =================================================================== emitting

        public void EmitAsh(int count)
        {
            if (!HasRoute)
            {
                return;
            }
            for (int i = 0; i < count; i++)
            {
                EmitAshAt(SampleOnRoute(Style.AshSpread));
            }
        }

        public void EmitAshAt(Vector2 world)
        {
            int slot = Free(ref ash, MaxAsh, AshOrder, "Ash");
            if (slot < 0)
            {
                return;
            }
            Sprite[] art = AshSprites();
            ash[slot].R.sprite = art[Random.Range(0, art.Length)];
            ash[slot].Pos = world;
            ash[slot].Vel = new Vector2(Random.Range(-Style.AshDrift, Style.AshDrift),
                Random.Range(Style.AshRiseMin, Style.AshRiseMax)) * cell;
            ash[slot].Angle = Random.Range(0f, 360f);
            ash[slot].Spin = Random.Range(-Style.AshSpinMax, Style.AshSpinMax);
            ash[slot].Size = Random.Range(Style.AshSizeMin, Style.AshSizeMax) * cell;
            ash[slot].Life = Random.Range(Style.AshLifeMin, Style.AshLifeMax);
            ash[slot].Alpha = Style.AshAlpha * Random.Range(0.75f, 1f);
            ash[slot].Age = 0f;
            ash[slot].Live = true;
            Color c = Color.Lerp(Style.AshDark, Style.AshLight, Random.value);
            c.a = 0f;
            ash[slot].R.color = c;
            ash[slot].Seed = Random.value;
            ash[slot].R.enabled = true;
        }

        public void EmitHaze(int count)
        {
            if (!HasRoute)
            {
                return;
            }
            for (int i = 0; i < count; i++)
            {
                int slot = Free(ref haze, MaxHaze, HazeOrder, "Haze");
                if (slot < 0)
                {
                    return;
                }
                haze[slot].R.sprite = HazeSprite();
                haze[slot].Pos = SampleOnRoute(0.10f);
                haze[slot].Vel = new Vector2(0f, Style.HazeRise * cell);
                haze[slot].Angle = 0f;
                haze[slot].Spin = 0f;
                haze[slot].Size = Random.Range(Style.HazeSizeMin, Style.HazeSizeMax) * cell;
                haze[slot].Life = Random.Range(Style.HazeLifeMin, Style.HazeLifeMax);
                haze[slot].Alpha = Style.HazeAlpha;
                haze[slot].Age = 0f;
                haze[slot].Seed = Random.value * 10f;
                haze[slot].Live = true;
                haze[slot].R.color = new Color(Style.HazeColor.r, Style.HazeColor.g,
                    Style.HazeColor.b, 0f);
                haze[slot].R.enabled = true;
            }
        }

        public void EmitEmbers(int count)
        {
            if (!HasRoute)
            {
                return;
            }
            for (int i = 0; i < count; i++)
            {
                int slot = Free(ref ember, MaxEmber, EmberOrder, "Ember");
                if (slot < 0)
                {
                    return;
                }
                ember[slot].R.sprite = EmberSprite();
                ember[slot].Pos = SampleOnRoute(Style.EmberSpread);
                ember[slot].Vel = Vector2.zero;
                ember[slot].Size = Random.Range(Style.EmberSizeMin, Style.EmberSizeMax) * cell;
                ember[slot].Life = Random.Range(Style.EmberLifeMin, Style.EmberLifeMax);
                ember[slot].Alpha = Style.EmberAlpha * Random.Range(0.6f, 1f);
                ember[slot].Age = 0f;
                ember[slot].Seed = Random.value * 10f;
                ember[slot].Angle = 0f;
                ember[slot].Spin = 0f;
                ember[slot].Live = true;
                Color c = Random.value < Style.EmberEnergyShare
                    ? Style.EmberEnergy : Style.EmberHot;
                c.a = 0f;
                ember[slot].R.color = c;
                ember[slot].R.enabled = true;
            }
        }

        /// <summary>Crust coming off at the rupture: bigger than ash, few, hot for an instant.</summary>
        public void EmitFlecks(int count)
        {
            if (!HasRoute)
            {
                return;
            }
            for (int i = 0; i < count; i++)
            {
                int slot = Free(ref fleck, MaxFleck, FleckOrder, "Fleck");
                if (slot < 0)
                {
                    return;
                }
                Sprite[] art = AshSprites();
                fleck[slot].R.sprite = art[Random.Range(0, art.Length)];
                fleck[slot].Pos = SampleOnRoute(0.12f);
                float ang = Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) + Style.FleckUpBias);
                fleck[slot].Vel = dir.normalized
                    * Random.Range(Style.FleckSpeedMin, Style.FleckSpeedMax) * cell;
                fleck[slot].Angle = Random.Range(0f, 360f);
                fleck[slot].Spin = Random.Range(-Style.FleckSpinMax, Style.FleckSpinMax);
                fleck[slot].Size = Random.Range(Style.FleckSizeMin, Style.FleckSizeMax) * cell;
                fleck[slot].Life = Random.Range(Style.FleckLifeMin, Style.FleckLifeMax);
                fleck[slot].Alpha = Style.FleckAlpha;
                fleck[slot].Age = 0f;
                fleck[slot].Seed = Random.value;
                fleck[slot].Live = true;
                fleck[slot].R.color = Style.FleckHot;
                fleck[slot].R.enabled = true;
            }
        }

        public void EmitSparks(int count)
        {
            if (!HasRoute)
            {
                return;
            }
            for (int i = 0; i < count; i++)
            {
                int slot = FreeSpark();
                if (slot < 0)
                {
                    return;
                }
                Vector2 p = SampleOnRoute(Style.SparkSpread);
                float total = Random.Range(Style.SparkLengthMin, Style.SparkLengthMax) * cell;
                int used = Random.value < 0.45f ? 2 : SparkSegments;
                float heading = Random.Range(0f, 360f);
                float thickness = Style.SparkThickness * cell;

                for (int s = 0; s < SparkSegments; s++)
                {
                    SpriteRenderer r = sparks[slot].Segments[s];
                    if (s >= used)
                    {
                        r.enabled = false;
                        continue;
                    }
                    float len = total / used;
                    heading += Random.Range(-Style.SparkKinkDegrees, Style.SparkKinkDegrees);
                    float rad = heading * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                    Vector2 mid = p + dir * (len * 0.5f);
                    r.transform.localPosition = new Vector3(mid.x, mid.y, 0f);
                    r.transform.localRotation = Quaternion.Euler(0f, 0f, heading);
                    // Sprite is 64x12, so one unit long and 12/64 tall at scale 1.
                    r.transform.localScale = new Vector3(len, thickness * (64f / 12f), 1f);
                    r.color = Color.white;
                    r.enabled = true;
                    p += dir * len;
                }
                sparks[slot].Age = 0f;
                sparks[slot].Life = Random.Range(Style.SparkLifeMin, Style.SparkLifeMax);
                sparks[slot].Live = true;
            }
        }

        /// <summary>The last flakes, once the energy has gone: a few places let go, not the whole
        /// cable evenly.</summary>
        public void FinalAshRelease()
        {
            if (!HasRoute)
            {
                return;
            }
            int spots = Random.Range(Style.FinalSpotsMin, Style.FinalSpotsMax + 1);
            for (int i = 0; i < spots; i++)
            {
                Vector2 at = SampleOnRoute(Style.AshSpread * 0.5f);
                int n = Random.Range(1, Style.FinalPerSpot + 1);
                for (int k = 0; k < n; k++)
                {
                    EmitAshAt(at + Random.insideUnitCircle * (0.08f * cell));
                }
            }
        }

        // =================================================================== pooling

        private int Free(ref Mote[] pool, int max, int order, string name)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].Live)
                {
                    return i;
                }
            }
            if (pool.Length >= max)
            {
                return -1;
            }
            var next = new Mote[pool.Length + 1];
            System.Array.Copy(pool, next, pool.Length);
            var go = new GameObject(name + pool.Length);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = order;
            r.enabled = false;
            next[pool.Length].R = r;
            int slot = pool.Length;
            pool = next;
            return slot;
        }

        private int FreeSpark()
        {
            for (int i = 0; i < sparks.Length; i++)
            {
                if (!sparks[i].Live)
                {
                    return i;
                }
            }
            if (sparks.Length >= MaxSpark)
            {
                return -1;
            }
            var next = new Spark[sparks.Length + 1];
            System.Array.Copy(sparks, next, sparks.Length);
            var segs = new SpriteRenderer[SparkSegments];
            for (int s = 0; s < SparkSegments; s++)
            {
                var go = new GameObject("Spark" + sparks.Length + "_" + s);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = SparkSprite();
                r.sortingOrder = SparkOrder;
                r.enabled = false;
                segs[s] = r;
            }
            next[sparks.Length].Segments = segs;
            int slot = sparks.Length;
            sparks = next;
            return slot;
        }

        // =================================================================== running

        private void Update()
        {
            float dt = Time.deltaTime;
            StepAsh(dt);
            StepHaze(dt);
            StepEmbers(dt);
            StepFlecks(dt);
            StepSparks(dt);
        }

        private void StepAsh(float dt)
        {
            for (int i = 0; i < ash.Length; i++)
            {
                if (!ash[i].Live)
                {
                    continue;
                }
                ash[i].Age += dt;
                float k = ash[i].Age / ash[i].Life;
                if (k >= 1f)
                {
                    ash[i].Live = false;
                    ash[i].R.enabled = false;
                    continue;
                }
                ash[i].Vel *= Mathf.Max(0f, 1f - Style.AshDrag * dt);
                ash[i].Pos += ash[i].Vel * dt;
                ash[i].Angle += ash[i].Spin * dt;

                // In fast, out slow: a flake appears at the moment it breaks off, then takes its
                // time going. The reverse looks like it was already there and got switched on.
                float fade = k < 0.10f ? k / 0.10f : Mathf.Pow(1f - (k - 0.10f) / 0.90f, 1.4f);
                float size = ash[i].Size * (1f - Style.AshShrink * k);
                Transform t = ash[i].R.transform;
                t.localPosition = new Vector3(ash[i].Pos.x, ash[i].Pos.y, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, ash[i].Angle);
                t.localScale = new Vector3(size, size, 1f);
                Color c = ash[i].R.color;
                c.a = ash[i].Alpha * fade;
                ash[i].R.color = c;
            }
        }

        private void StepHaze(float dt)
        {
            for (int i = 0; i < haze.Length; i++)
            {
                if (!haze[i].Live)
                {
                    continue;
                }
                haze[i].Age += dt;
                float k = haze[i].Age / haze[i].Life;
                if (k >= 1f)
                {
                    haze[i].Live = false;
                    haze[i].R.enabled = false;
                    continue;
                }
                haze[i].Pos += haze[i].Vel * dt;
                // Breathing, not travelling. The patch stays where the heat is and moves the way
                // air over something hot moves.
                float wob = 1f + Style.HazeWobble
                    * Mathf.Sin((haze[i].Age * Style.HazeWobbleHz + haze[i].Seed) * Mathf.PI * 2f);
                float size = haze[i].Size * wob;
                Transform t = haze[i].R.transform;
                t.localPosition = new Vector3(haze[i].Pos.x, haze[i].Pos.y, 0f);
                t.localScale = new Vector3(size, size * (1f / wob), 1f);
                Color c = haze[i].R.color;
                c.a = haze[i].Alpha * Mathf.Sin(k * Mathf.PI);
                haze[i].R.color = c;
            }
        }

        private void StepEmbers(float dt)
        {
            for (int i = 0; i < ember.Length; i++)
            {
                if (!ember[i].Live)
                {
                    continue;
                }
                ember[i].Age += dt;
                float k = ember[i].Age / ember[i].Life;
                if (k >= 1f)
                {
                    ember[i].Live = false;
                    ember[i].R.enabled = false;
                    continue;
                }
                float flicker = 0.72f + 0.28f * Mathf.Sin(
                    (ember[i].Age * Style.EmberFlickerHz + ember[i].Seed) * Mathf.PI * 2f);
                Transform t = ember[i].R.transform;
                t.localPosition = new Vector3(ember[i].Pos.x, ember[i].Pos.y, 0f);
                float size = ember[i].Size * (1f - 0.35f * k);
                t.localScale = new Vector3(size, size, 1f);
                Color c = ember[i].R.color;
                c.a = ember[i].Alpha * (1f - k) * (1f - k) * flicker;
                ember[i].R.color = c;
            }
        }

        private void StepFlecks(float dt)
        {
            for (int i = 0; i < fleck.Length; i++)
            {
                if (!fleck[i].Live)
                {
                    continue;
                }
                fleck[i].Age += dt;
                float k = fleck[i].Age / fleck[i].Life;
                if (k >= 1f)
                {
                    fleck[i].Live = false;
                    fleck[i].R.enabled = false;
                    continue;
                }
                fleck[i].Vel *= Mathf.Max(0f, 1f - Style.FleckDrag * dt);
                fleck[i].Pos += fleck[i].Vel * dt;
                fleck[i].Angle += fleck[i].Spin * dt;

                // Hot when it breaks off, charcoal by the time it stops. A fleck that stays one
                // colour is debris; one that cools is a piece of something that was burning.
                Color c = Color.Lerp(Style.FleckHot, Style.FleckChar,
                    Mathf.Clamp01(k / Mathf.Max(Style.FleckCoolBy, 0.01f)));
                c.a = fleck[i].Alpha * (k < 0.12f ? k / 0.12f : Mathf.Pow(1f - k, 0.8f));
                fleck[i].R.color = c;

                Transform t = fleck[i].R.transform;
                t.localPosition = new Vector3(fleck[i].Pos.x, fleck[i].Pos.y, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, fleck[i].Angle);
                float size = fleck[i].Size;
                t.localScale = new Vector3(size, size, 1f);
            }
        }

        private void StepSparks(float dt)
        {
            for (int i = 0; i < sparks.Length; i++)
            {
                if (!sparks[i].Live)
                {
                    continue;
                }
                sparks[i].Age += dt;
                float k = sparks[i].Age / sparks[i].Life;
                if (k >= 1f)
                {
                    sparks[i].Live = false;
                    for (int s = 0; s < SparkSegments; s++)
                    {
                        sparks[i].Segments[s].enabled = false;
                    }
                    continue;
                }
                // A snap does not fade, it stops. Held bright, then cut in the last third.
                float a = k < 0.66f ? 1f : 1f - (k - 0.66f) / 0.34f;
                for (int s = 0; s < SparkSegments; s++)
                {
                    SpriteRenderer r = sparks[i].Segments[s];
                    if (!r.enabled)
                    {
                        continue;
                    }
                    r.color = new Color(1f, 1f, 1f, a);
                }
            }
        }

        public void StopAll()
        {
            for (int i = 0; i < ash.Length; i++)
            {
                ash[i].Live = false;
                if (ash[i].R != null) { ash[i].R.enabled = false; }
            }
            for (int i = 0; i < haze.Length; i++)
            {
                haze[i].Live = false;
                if (haze[i].R != null) { haze[i].R.enabled = false; }
            }
            for (int i = 0; i < ember.Length; i++)
            {
                ember[i].Live = false;
                if (ember[i].R != null) { ember[i].R.enabled = false; }
            }
            for (int i = 0; i < fleck.Length; i++)
            {
                fleck[i].Live = false;
                if (fleck[i].R != null) { fleck[i].R.enabled = false; }
            }
            for (int i = 0; i < sparks.Length; i++)
            {
                sparks[i].Live = false;
                for (int s = 0; s < SparkSegments; s++)
                {
                    if (sparks[i].Segments != null && sparks[i].Segments[s] != null)
                    {
                        sparks[i].Segments[s].enabled = false;
                    }
                }
            }
        }
    }
}
