// PURPOSE: "Soğuk katlama" (Phase Fold) - removal variant 2, for a cube a boss REMOVED
// (TurnReport.LiftKindAt == Removed). GameUiController.PlayRemoval picks a variant at random per
// removal; this one and ColdSinkView must never be mistaken for each other.
//
// COLD SINK: the floor opens and the cube FALLS. PHASE FOLD: no pit, no hole, no fall - the board
// takes the cube's VOLUME apart and puts what is left away between its own layers:
//   COMPRESSION  short grey-blue marks press in from the cell's four inner edges; the contact
//                shadow tightens and darkens. The cube still wears its own face and material.
//   VOLUME       its painted bevel and shading are averaged away, its colour and warmth drain,
//                it compresses a little across itself - a face plate, not a smaller cube.
//   LAYER PEEL   for a moment it is two or three thin layers a couple of pixels apart, colder
//                toward the back, then they press back into one plate.
//   SEAM         a narrow slate slit opens along one inner edge (chosen from the cell, so a group
//                folds every which way): hairline, parting, open.
//   EXTRACTION   the plate slides into it - slow, faster, squeezing - its leading edge pulled first,
//                pinched toward the slit, the trailing edge lagging: a slight flex, never a curl.
//                Everything past the seam is clipped (the PhaseFold shader, per renderer), so it
//                truly goes BEHIND the board; the last of it is a sliver drawn into the slit, with a
//                thread or two of material stretched after it.
//   AFTER        a faint cold negative of the cube where it stood clears from its middle outward;
//                the seam holds, closes (wide, thin, hairline, gone) and leaves a cold hairline.
//
// NOTHING HERE IS AN EXPLOSION: no debris, no flash, no shake, no sound of its own, and no pit.
// Without the shader (missing or unsupported) the plates are plain sprites squeezed into their
// seams - the fold still reads, it just cannot flex or truly clip.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Removal variant 2: the cube's volume is taken apart and folded into a seam.</summary>
    public sealed class PhaseFoldView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the fold READS. Sizes in CELLS, times in seconds.</summary>
        public static class Style
        {
            // ---- the wave ----
            public static float CenterOutDelay = 0.03f;

            public static float CenterOutMaxSpread = 0.15f;

            /// <summary>Seconds either way, and a few percent of every duration, from the cell.</summary>
            public static float VisualJitter = 0.006f;

            // ---- compression ----
            public static float CompressionDuration = 0.08f;

            public static float CompressionStrength = 0.6f;

            // ---- volume ----
            public static float FlattenDuration = 0.23f;

            /// <summary>How much of the painted bevel and shading is averaged away.</summary>
            public static float BevelSuppression = 0.85f;

            /// <summary>How much of the contact shadow the flattening takes with it.</summary>
            public static float DepthShadowSuppression = 1f;

            public static float DesaturationStrength = 0.6f;

            public static float ColdInfluence = 0.55f;

            /// <summary>The face plate's size once the volume is gone, in cells.</summary>
            public static float PlateSize = 0.90f;

            // ---- layer peel ----
            /// <summary>Layers visible at the peel, the plate itself included (2 or 3).</summary>
            public static int LayerCount = 3;

            public static float LayerOffset = 0.03f;

            /// <summary>Each layer further back keeps this fraction of the one in front's opacity.</summary>
            public static float LayerOpacityFalloff = 0.45f;

            public static float RecompressDuration = 0.10f;

            // ---- the seam ----
            public static float SeamOpenDuration = 0.10f;

            public static float SeamWidth = 0.035f;

            /// <summary>How much of the cell's edge the seam opens along. Local - never a laser line.</summary>
            public static float SeamLength = 0.58f;

            public static float SeamColdRimStrength = 0.5f;

            // ---- extraction ----
            public static float TravelDuration = 0.30f;

            /// <summary>Above 1: a slower start. The travel is always slow, faster, squeezing.</summary>
            public static float TravelEase = 1.15f;

            /// <summary>How far ahead the leading edge is pulled at the middle of the travel.</summary>
            public static float FlexStrength = 0.06f;

            /// <summary>The leading edge starts toward the seam this long before the plate moves.</summary>
            public static float FlexDelay = 0.03f;

            /// <summary>How far behind the trailing edge lags, as a fraction of the lead.</summary>
            public static float TrailingEdgeLag = 0.5f;

            /// <summary>How much the leading edge narrows toward the slit.</summary>
            public static float LeadPinch = 0.35f;

            /// <summary>The plate's depth along its travel as it goes in, relative to its face.</summary>
            public static float FinalPlateThickness = 0.55f;

            public static int StrandCount = 2;

            public static float StrandDuration = 0.09f;

            // ---- after ----
            public static float NegativeGhostOpacity = 0.14f;

            public static float NegativeGhostDuration = 0.16f;

            public static float SeamHoldDuration = 0.08f;

            public static float SeamCloseDuration = 0.08f;

            public static float HairlineResidueStrength = 0.25f;

            public static float HairlineResidueDuration = 0.10f;

            // ---- level of detail ----
            /// <summary>How far detail (flex, peel layers, strands, ghost) falls from N&lt;=5 to N&gt;20.</summary>
            public static float LargeNDetailCompensation = 0.8f;
        }

        /// <summary>The lab's debug switches. Visual only; the lab puts them all back on close.</summary>
        public static class Layers
        {
            public static bool ShowCompressionMarks = true;

            public static bool ShowLayerSplit = true;

            public static bool ShowFlex = true;

            public static bool ShowNegativeGhost = true;

            public static bool ShowSeamMask = true;

            public static void AllOn()
            {
                ShowCompressionMarks = true;
                ShowLayerSplit = true;
                ShowFlex = true;
                ShowNegativeGhost = true;
                ShowSeamMask = true;
            }
        }

        // =================================================================== palette

        private static readonly Color MarkColour = new Color(0.55f, 0.62f, 0.70f);

        private static readonly Color ColdTint = new Color(0.36f, 0.42f, 0.50f);

        private static readonly Color GhostColour = new Color(0.40f, 0.47f, 0.56f);

        private static readonly Color LipColour = new Color(0.20f, 0.23f, 0.28f);

        private static readonly Color StrandColour = new Color(0.55f, 0.62f, 0.70f);

        private static readonly Color ShadowColour = new Color(0.02f, 0.025f, 0.04f);

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        // =================================================================== layers

        private const int ShadowOrder = 5;

        private const int GhostOrder = 6;

        private const int BackOrder = 6;

        private const int MiddleOrder = 7;

        private const int PlateOrder = 8;

        private const int SeamOrder = 9;

        private const int LipOrder = 10;

        private const int MarkOrder = 10;

        private const int StrandOrder = 11;

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
            public float FlattenStart;
            public float FlattenEnd;
            public float SplitStart;
            public float RecompressStart;
            public float RecompressEnd;
            public float SeamOpenStart;
            public float SeamOpenEnd;
            public float TravelStart;
            public float TravelEnd;
            public float GhostStart;
            public float GhostEnd;
            public float SeamCloseStart;
            public float SeamCloseEnd;
            public float HairlineEnd;
        }

        private sealed class Fold
        {
            public Vector2 At;
            public Vector2 Normal;
            public Vector2 Across;
            /// <summary>How far from the cell's centre the seam's inner edge lies, world units.</summary>
            public float SeamReach;
            public float Start;
            public Times T;
            public Dice Dice;
            public ClusterBurstView.Look Look;
            public bool Folding;
            public readonly float[] MarkLength = new float[4];
            public readonly float[] MarkStrength = new float[4];
            public readonly float[] StrandAt = new float[2];
            public SpriteRenderer Shadow;
            public SpriteRenderer Back;
            public SpriteRenderer Middle;
            public SpriteRenderer Plate;
            public SpriteRenderer Ghost;
            public SpriteRenderer Seam;
            public SpriteRenderer Lip;
            public readonly SpriteRenderer[] Marks = new SpriteRenderer[4];
            public readonly SpriteRenderer[] Strands = new SpriteRenderer[2];
        }

        private sealed class Batch
        {
            public readonly List<Fold> Folds = new List<Fold>();
            public float Clock;
            public float End;
            public float Cell;
            public float CubeSize;
            public float Mouth;
            /// <summary>Detail tier: 0 for N&lt;=5 up to 1 past 20, times the compensation.</summary>
            public float Lod;
        }

        private readonly List<Batch> batches = new List<Batch>();

        private readonly Stack<SpriteRenderer> spareRenderers = new Stack<SpriteRenderer>();

        private Material plainMaterial;

        private static Material foldMaterial;

        private static bool foldLooked;

        private MaterialPropertyBlock block;

        /// <summary>The PhaseFold material, or null when the shader cannot be had - the plain
        /// sprite fallback. Shader.Find works in the editor; the Resources copy is what survives
        /// into a build.</summary>
        private static Material FoldMaterial()
        {
            if (!foldLooked)
            {
                foldLooked = true;
                Shader shader = Shader.Find("ProjectBlock/PhaseFold");
                if (shader == null)
                {
                    shader = Resources.Load<Shader>("Shaders/PhaseFold");
                }
                if (shader != null && shader.isSupported)
                {
                    foldMaterial = new Material(shader);
                    foldMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return foldMaterial;
        }

        // =================================================================== driving it

        /// <summary>
        /// Folds each cube in <paramref name="looks"/> away at its cell in <paramref name="cells"/>
        /// (world centres). <paramref name="cubeSize"/> is the size the board draws a cube at and
        /// <paramref name="slotSize"/> the size of an empty slot - the seams open inside it.
        /// </summary>
        public void Play(IReadOnlyList<Vector2> cells, IReadOnlyList<ClusterBurstView.Look> looks,
            float cellSize, float cubeSize, float slotSize)
        {
            if (cells == null || cells.Count == 0 || cellSize <= 0f)
            {
                return;
            }
            int n = cells.Count;
            var batch = new Batch
            {
                Cell = cellSize,
                CubeSize = cubeSize > 0f ? cubeSize : cellSize,
                Mouth = slotSize > 0f ? slotSize : cellSize * 0.82f,
                Lod = (n <= 5 ? 0f : n <= 12 ? 0.35f : n <= 20 ? 0.65f : 1f)
                    * Mathf.Clamp01(Style.LargeNDetailCompensation)
            };
            Vector2 centre = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                centre += cells[i];
            }
            centre /= n;
            float furthest = 0f;
            for (int i = 0; i < n; i++)
            {
                furthest = Mathf.Max(furthest, (cells[i] - centre).magnitude);
            }
            float furthestCells = furthest / cellSize;
            float perCell = furthestCells > 0f
                ? Mathf.Min(Style.CenterOutDelay, Style.CenterOutMaxSpread / furthestCells) : 0f;
            Sprite fallbackTile = ViewUtil.DefaultTile != null ? ViewUtil.DefaultTile
                : ViewUtil.WhiteSprite;
            Material fold = FoldMaterial();

            for (int i = 0; i < n; i++)
            {
                uint seed = Hash(Mathf.RoundToInt(cells[i].x / cellSize * 4f),
                    Mathf.RoundToInt(cells[i].y / cellSize * 4f));
                var f = new Fold { At = cells[i], Dice = new Dice(seed) };
                // The seam: one of the four inner edges, from the cell - visual only.
                int side = (int)((seed >> 5) & 3u);
                f.Normal = side == 0 ? Vector2.right : side == 1 ? Vector2.up
                    : side == 2 ? Vector2.left : Vector2.down;
                f.Across = new Vector2(-f.Normal.y, f.Normal.x);
                f.SeamReach = batch.Mouth * 0.5f - Style.SeamWidth * cellSize * 0.5f;
                float jitter = n > 1 ? f.Dice.Range(-1f, 1f) * Style.VisualJitter : 0f;
                f.Start = Mathf.Max(0f, (cells[i] - centre).magnitude / cellSize * perCell + jitter);
                f.T = Timeline(f.Dice.Range(-1f, 1f) * 0.05f, f.Dice.Range(-1f, 1f) * 0.05f);
                for (int m = 0; m < 4; m++)
                {
                    f.MarkLength[m] = f.Dice.Range(0.38f, 0.52f);
                    f.MarkStrength[m] = f.Dice.Range(0.75f, 1f);
                }
                for (int s = 0; s < 2; s++)
                {
                    f.StrandAt[s] = f.Dice.Range(-0.3f, 0.3f);
                }
                bool known = looks != null && i < looks.Count && looks[i].Tile != null;
                f.Look = known ? looks[i] : new ClusterBurstView.Look
                {
                    Tile = fallbackTile,
                    Colour = new Color(0.62f, 0.68f, 0.82f)
                };

                f.Shadow = Rent(SoftSquareSprite(), ShadowOrder, null);
                f.Ghost = Rent(f.Look.Tile, GhostOrder, fold);
                f.Back = Rent(f.Look.Tile, BackOrder, fold);
                f.Middle = Rent(f.Look.Tile, MiddleOrder, fold);
                // Its own material while it is still just a cube: water swirls until it flattens.
                f.Plate = Rent(f.Look.Tile, PlateOrder, ViewUtil.TileMaterial(f.Look.Tile));
                f.Seam = Rent(SlitSprite(), SeamOrder, null);
                f.Lip = Rent(LineSprite(), LipOrder, null);
                for (int m = 0; m < 4; m++)
                {
                    f.Marks[m] = Rent(MarkSprite(), MarkOrder, null);
                }
                for (int s = 0; s < 2; s++)
                {
                    f.Strands[s] = Rent(LineSprite(), StrandOrder, null);
                }
                // On THIS frame: the board repainted the cell empty a moment ago.
                Place(f.Plate, cells[i], batch.CubeSize, f.Look.Colour, 1f, 0f);
                batch.Folds.Add(f);
                batch.End = Mathf.Max(batch.End, f.Start + Mathf.Max(f.T.HairlineEnd, f.T.GhostEnd));
            }
            batches.Add(batch);
        }

        /// <summary>One cell's timeline, with its travel and close stretched by the given
        /// fractions - the small per-cell variation that keeps a group from moving as one machine.</summary>
        private static Times Timeline(float travelVariation, float closeVariation)
        {
            var t = new Times();
            t.FlattenStart = Style.CompressionDuration * 0.6f;
            t.FlattenEnd = t.FlattenStart + Style.FlattenDuration;
            t.SplitStart = t.FlattenStart + Style.FlattenDuration * 0.43f;
            t.RecompressStart = t.FlattenEnd - Style.RecompressDuration * 0.4f;
            t.RecompressEnd = t.RecompressStart + Style.RecompressDuration;
            t.SeamOpenStart = t.FlattenEnd;
            t.SeamOpenEnd = t.SeamOpenStart + Style.SeamOpenDuration;
            t.TravelStart = t.RecompressEnd + 0.02f;
            t.TravelEnd = t.TravelStart + Style.TravelDuration * (1f + travelVariation);
            t.GhostStart = t.TravelEnd - 0.06f;
            t.GhostEnd = t.GhostStart + Style.NegativeGhostDuration;
            t.SeamCloseStart = t.TravelEnd + Style.SeamHoldDuration;
            t.SeamCloseEnd = t.SeamCloseStart + Style.SeamCloseDuration * (1f + closeVariation);
            t.HairlineEnd = t.SeamCloseEnd + Style.HairlineResidueDuration;
            return t;
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
                for (int i = 0; i < batch.Folds.Count; i++)
                {
                    PaintFold(batch, batch.Folds[i]);
                }
                if (batch.Clock >= batch.End)
                {
                    Release(batch);
                    batches.RemoveAt(b);
                }
            }
        }

        /// <summary>One cell at its own age: pressed, flattened, peeled, folded away, closed.</summary>
        private void PaintFold(Batch batch, Fold f)
        {
            float t = batch.Clock - f.Start;
            Times T = f.T;
            float cell = batch.Cell;
            float mouth = batch.Mouth;
            float detail = 1f - batch.Lod;
            if (t < 0f)
            {
                Place(f.Plate, f.At, batch.CubeSize, f.Look.Colour, 1f, 0f);
                return;
            }

            PaintMarks(batch, f, t);

            // ---- the volume going: flatten, drain, a little compression across itself ----
            float flatten = Smooth((t - T.FlattenStart) / Mathf.Max(Style.FlattenDuration, 0.0001f));
            float press = Mathf.Clamp01(t / Mathf.Max(Style.CompressionDuration, 0.0001f));
            float bevel = Style.BevelSuppression
                * Smooth(((t - T.FlattenStart) / Mathf.Max(Style.FlattenDuration, 0.0001f) - 0.25f) / 0.45f);
            float desat = Style.DesaturationStrength * 0.25f * flatten;
            float cold = Style.ColdInfluence * 0.3f * flatten;
            float bright = 1f - 0.08f * press * (1f - flatten);
            float face = Mathf.Lerp(batch.CubeSize, Style.PlateSize * cell, flatten);
            float alongScale = 1f - 0.02f * flatten;
            float acrossScale = 1f - 0.04f * flatten;

            // ---- the extraction ----
            float seamInner = mouth * 0.5f - Style.SeamWidth * cell * 0.5f;
            float travelTime = Mathf.Max(T.TravelEnd - T.TravelStart, 0.0001f);
            float u = Mathf.Clamp01((t - T.TravelStart) / travelTime);
            float e = Smooth(Mathf.Pow(u, Style.TravelEase));
            float lead = 0f;
            float trail = 0f;
            float pinch = 0f;
            float offset = 0f;
            if (t >= T.TravelStart - Style.FlexDelay)
            {
                desat += (Style.DesaturationStrength - desat) * e;
                cold += (Style.ColdInfluence - cold) * e;
                bright *= 1f - 0.25f * e;
                alongScale *= Mathf.Lerp(1f, Style.FinalPlateThickness, e);
                acrossScale *= Mathf.Lerp(1f, Style.SeamLength * mouth / Mathf.Max(Style.PlateSize * cell, 0.0001f),
                    Mathf.Pow(e, 0.8f));
                // The leading edge is pulled a moment before the plate moves, and hardest halfway.
                float pull = Mathf.Clamp01((t - (T.TravelStart - Style.FlexDelay)) / travelTime);
                float flex = Layers.ShowFlex ? Mathf.Lerp(1f, 0.4f, batch.Lod) : 0f;
                lead = Style.FlexStrength * cell * Mathf.Sin(Mathf.PI * pull) * flex;
                trail = lead * Style.TrailingEdgeLag;
                pinch = Style.LeadPinch * (1f - e) * Smooth(u * 3f) * flex;
                float halfEnd = face * Style.FinalPlateThickness * 0.5f;
                offset = (seamInner + halfEnd + 0.02f * cell) * e;
            }
            float half = face * alongScale * 0.5f;
            float across = face * acrossScale;
            Vector2 centre = f.At + f.Normal * offset;
            Color tint = f.Look.Colour * bright;
            tint.a = 1f;

            bool gone = t >= T.TravelEnd;
            if (gone)
            {
                f.Plate.color = Clear;
                f.Middle.color = Clear;
                f.Back.color = Clear;
            }
            else
            {
                if (!f.Folding && t >= T.FlattenStart && foldMaterial != null)
                {
                    // Off its own animated material once the volume starts to go: the fold
                    // shader takes it from here.
                    f.Folding = true;
                    f.Plate.sharedMaterial = foldMaterial;
                }
                bool clip = Layers.ShowSeamMask;
                if (foldMaterial != null)
                {
                    PlaceAxes(f.Plate, centre, f.Normal, half * 2f, across, tint, 1f);
                    if (f.Folding)
                    {
                        Apply(f.Plate, f, centre, half, lead, trail, pinch, bevel, desat, cold, 0f, clip);
                    }
                }
                else
                {
                    // Plain sprites cannot clip: the part that has gone past the seam is squeezed
                    // off instead, so nothing ever slides out over the next cell.
                    float trailEdge = offset - half;
                    float leadEdge = clip ? Mathf.Min(offset + half, seamInner) : offset + half;
                    float span = Mathf.Max(0f, leadEdge - trailEdge);
                    Color plain = Color.Lerp(tint, ColdTint * bright, cold);
                    PlaceAxes(f.Plate, f.At + f.Normal * ((trailEdge + leadEdge) * 0.5f), f.Normal, span,
                        across, plain, span > 0f ? 1f : 0f);
                }
                PaintPeel(batch, f, t, centre, half, across, tint, desat, cold, bevel);
            }

            // ---- the contact shadow: tightens under the pressure, goes with the volume ----
            float shadow = 0.45f * Smooth(press) * (1f - Style.DepthShadowSuppression * flatten);
            Place(f.Shadow, f.At, face * 1.02f, ShadowColour, gone ? 0f : shadow, 0f);

            PaintSeam(batch, f, t, seamInner);
            PaintStrands(batch, f, t, centre, half, seamInner, detail);
            PaintGhost(batch, f, t);
        }

        /// <summary>The four inner edges pressing in: short grey-blue marks, leaning inward.</summary>
        private void PaintMarks(Batch batch, Fold f, float t)
        {
            float d = Mathf.Max(Style.CompressionDuration, 0.0001f);
            float k = t / d;
            float env = !Layers.ShowCompressionMarks || t > d + 0.05f ? 0f
                : k < 0.3f ? k / 0.3f : k <= 1f ? 1f : 1f - (t - d) / 0.05f;
            float inward = 0.02f * batch.Cell * Smooth(k);
            for (int m = 0; m < 4; m++)
            {
                Vector2 outward = m == 0 ? Vector2.right : m == 1 ? Vector2.up
                    : m == 2 ? Vector2.left : Vector2.down;
                Vector2 at = f.At + outward * (batch.Mouth * 0.5f - 0.03f * batch.Cell - inward);
                float angle = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg - 90f;
                PlaceRect(f.Marks[m], at, f.MarkLength[m] * batch.Mouth, 0.06f * batch.Cell, MarkColour,
                    Style.CompressionStrength * f.MarkStrength[m] * env * (1f - 0.4f * batch.Lod), angle);
            }
        }

        /// <summary>The peel: two or three thin layers a couple of pixels apart, colder toward the
        /// back, pressed back into one. Offset along a fixed diagonal - depth, not motion.</summary>
        private void PaintPeel(Batch batch, Fold f, float t, Vector2 centre, float half, float across,
            Color tint, float desat, float cold, float bevel)
        {
            Times T = f.T;
            float split = Smooth((t - T.SplitStart) / Mathf.Max(T.RecompressStart - T.SplitStart, 0.0001f));
            float merge = Smooth((t - T.RecompressStart) / Mathf.Max(Style.RecompressDuration, 0.0001f));
            float amount = Layers.ShowLayerSplit && foldMaterial != null ? split * (1f - merge) : 0f;
            int layers = batch.Lod > 0.5f ? Mathf.Min(2, Style.LayerCount) : Style.LayerCount;
            var depth = new Vector2(-0.7071f, 0.7071f) * (Style.LayerOffset * batch.Cell * amount);
            float middle = layers >= 2 ? Style.LayerOpacityFalloff * amount : 0f;
            float back = layers >= 3 ? Style.LayerOpacityFalloff * Style.LayerOpacityFalloff * amount : 0f;
            PlaceAxes(f.Middle, centre + depth * 0.5f, f.Normal, half * 2f, across, tint, middle);
            PlaceAxes(f.Back, centre + depth, f.Normal, half * 2f, across, tint, back);
            if (foldMaterial != null)
            {
                Apply(f.Middle, f, centre + depth * 0.5f, half, 0f, 0f, 0f, bevel,
                    Mathf.Min(1f, desat + 0.25f), Mathf.Min(1f, cold + 0.15f), 0f, false);
                Apply(f.Back, f, centre + depth, half, 0f, 0f, 0f, bevel,
                    Mathf.Min(1f, desat + 0.45f), Mathf.Min(1f, cold + 0.35f), 0f, false);
            }
        }

        /// <summary>The seam: hairline, parting, open; held; closing wide, thin, hairline, gone;
        /// then a cold hairline that fades.</summary>
        private void PaintSeam(Batch batch, Fold f, float t, float seamInner)
        {
            Times T = f.T;
            float cell = batch.Cell;
            float full = Style.SeamWidth * cell;
            float length = Style.SeamLength * batch.Mouth;
            float width = 0f;
            float lengthScale = 1f;
            float alpha = 1f;
            float rim = 0f;
            if (t >= T.SeamOpenStart && t < T.SeamOpenEnd)
            {
                float k = (t - T.SeamOpenStart) / Mathf.Max(Style.SeamOpenDuration, 0.0001f);
                width = full * (k < 0.4f ? 0.3f * k / 0.4f : Mathf.Lerp(0.3f, 1f, (k - 0.4f) / 0.6f));
                lengthScale = Mathf.Lerp(0.6f, 1f, Smooth(k));
                rim = Smooth(k);
            }
            else if (t >= T.SeamOpenEnd && t < T.SeamCloseStart)
            {
                width = full;
                rim = 1f;
            }
            else if (t >= T.SeamCloseStart && t < T.SeamCloseEnd)
            {
                float k = (t - T.SeamCloseStart) / Mathf.Max(T.SeamCloseEnd - T.SeamCloseStart, 0.0001f);
                width = full * Mathf.Lerp(1f, 0.15f, Smooth(k));
                rim = 1f - k;
            }
            else if (t >= T.SeamCloseEnd && t < T.HairlineEnd)
            {
                float k = (t - T.SeamCloseEnd) / Mathf.Max(Style.HairlineResidueDuration, 0.0001f);
                width = full * 0.15f;
                alpha = Style.HairlineResidueStrength * (1f - k);
            }
            Vector2 at = f.At + f.Normal * (seamInner + width * 0.5f);
            float angle = Mathf.Atan2(f.Across.y, f.Across.x) * Mathf.Rad2Deg;
            if (width <= 0f)
            {
                f.Seam.color = Clear;
                f.Lip.color = Clear;
                return;
            }
            PlaceRect(f.Seam, at, length * lengthScale, Mathf.Max(width, 0.004f * cell),
                t >= T.SeamCloseEnd ? MarkColour : new Color(0.04f, 0.05f, 0.075f), alpha, angle);
            // The board's upper layer, right at the seam: a thin cold lip over the plate going under.
            PlaceRect(f.Lip, f.At + f.Normal * (seamInner - 0.006f * cell), length * lengthScale,
                0.012f * cell, Color.Lerp(LipColour, MarkColour, 0.3f), Style.SeamColdRimStrength * rim,
                angle);
        }

        /// <summary>The last threads of material, stretched from the plate into the seam.</summary>
        private void PaintStrands(Batch batch, Fold f, float t, Vector2 centre, float half,
            float seamInner, float detail)
        {
            Times T = f.T;
            float from = T.TravelEnd - Style.TravelDuration * 0.35f;
            int count = batch.Lod >= 0.5f ? 0 : Mathf.Clamp(Style.StrandCount, 0, 2);
            for (int s = 0; s < 2; s++)
            {
                float k = (t - from - s * 0.02f) / Mathf.Max(Style.StrandDuration, 0.0001f);
                if (s >= count || k <= 0f || k >= 1f)
                {
                    f.Strands[s].color = Clear;
                    continue;
                }
                Vector2 a = centre - f.Normal * (half * 0.3f) + f.Across * (f.StrandAt[s] * batch.Mouth);
                Vector2 b = f.At + f.Normal * seamInner
                    + f.Across * (f.StrandAt[s] * Style.SeamLength * batch.Mouth * 0.5f);
                Vector2 d = b - a;
                PlaceRect(f.Strands[s], (a + b) * 0.5f, d.magnitude, 0.008f * batch.Cell, StrandColour,
                    0.35f * (1f - k) * detail, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
        }

        /// <summary>A faint cold negative of the cube where it stood, clearing from its middle out.</summary>
        private void PaintGhost(Batch batch, Fold f, float t)
        {
            Times T = f.T;
            float k = (t - T.GhostStart) / Mathf.Max(Style.NegativeGhostDuration, 0.0001f);
            if (!Layers.ShowNegativeGhost || k <= 0f || k >= 1f)
            {
                f.Ghost.color = Clear;
                return;
            }
            float alpha = Style.NegativeGhostOpacity * (k < 0.2f ? k / 0.2f : 1f)
                * Mathf.Lerp(1f, 0.5f, batch.Lod);
            if (foldMaterial == null)
            {
                alpha *= 1f - k;
            }
            Place(f.Ghost, f.At, batch.CubeSize, GhostColour, alpha, 0f);
            if (foldMaterial != null)
            {
                Apply(f.Ghost, f, f.At, batch.CubeSize * 0.5f, 0f, 0f, 0f, 1f, 1f, 0.6f, Smooth(k), false);
            }
        }

        // =================================================================== the shader's inputs

        private static readonly int SeamPointId = Shader.PropertyToID("_SeamPoint");

        private static readonly int SeamNormalId = Shader.PropertyToID("_SeamNormal");

        private static readonly int PlateCentreId = Shader.PropertyToID("_PlateCentre");

        private static readonly int PlateHalfId = Shader.PropertyToID("_PlateHalf");

        private static readonly int LeadId = Shader.PropertyToID("_Lead");

        private static readonly int TrailId = Shader.PropertyToID("_Trail");

        private static readonly int PinchId = Shader.PropertyToID("_Pinch");

        private static readonly int FlattenId = Shader.PropertyToID("_Flatten");

        private static readonly int DesatId = Shader.PropertyToID("_Desat");

        private static readonly int ColdId = Shader.PropertyToID("_Cold");

        private static readonly int ColdTintId = Shader.PropertyToID("_ColdTint");

        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        private static readonly int ClipId = Shader.PropertyToID("_Clip");

        /// <summary>Hands one renderer its fold state. One reused block, so nothing is allocated
        /// per frame.</summary>
        private void Apply(SpriteRenderer r, Fold f, Vector2 centre, float half, float lead,
            float trail, float pinch, float flatten, float desat, float cold, float dissolve,
            bool clip)
        {
            if (foldMaterial == null || r.sharedMaterial != foldMaterial)
            {
                return;
            }
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            // The shader works in world space; everything here is placed relative to this view.
            Vector3 origin = transform.position;
            Vector2 seam = f.At + f.Normal * f.SeamReach;
            block.Clear();
            block.SetVector(SeamPointId, new Vector4(origin.x + seam.x, origin.y + seam.y, 0f, 0f));
            block.SetVector(SeamNormalId, new Vector4(f.Normal.x, f.Normal.y, 0f, 0f));
            block.SetVector(PlateCentreId, new Vector4(origin.x + centre.x, origin.y + centre.y, 0f, 0f));
            block.SetFloat(PlateHalfId, Mathf.Max(half, 0.0001f));
            block.SetFloat(LeadId, lead);
            block.SetFloat(TrailId, trail);
            block.SetFloat(PinchId, pinch);
            block.SetFloat(FlattenId, flatten);
            block.SetFloat(DesatId, desat);
            block.SetFloat(ColdId, cold);
            block.SetColor(ColdTintId, ColdTint);
            block.SetFloat(DissolveId, dissolve);
            block.SetFloat(ClipId, clip ? 1f : 0f);
            r.SetPropertyBlock(block);
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

        /// <summary>A tile sized separately along and across the seam normal. One unit is the
        /// cube's body, as everywhere else on the board (ViewUtil.ApplyTile).</summary>
        private static void PlaceAxes(SpriteRenderer r, Vector2 at, Vector2 normal, float along,
            float across, Color colour, float alpha)
        {
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            bool horizontal = Mathf.Abs(normal.x) > Mathf.Abs(normal.y);
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = horizontal ? new Vector3(along, across, 1f)
                : new Vector3(across, along, 1f);
        }

        /// <summary>A non-square sprite sized to <paramref name="width"/> by
        /// <paramref name="height"/> and turned by <paramref name="angle"/> degrees.</summary>
        private static void PlaceRect(SpriteRenderer r, Vector2 at, float width, float height,
            Color colour, float alpha, float angle)
        {
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            Vector2 unit = r.sprite != null
                ? new Vector2(r.sprite.rect.width, r.sprite.rect.height) / r.sprite.pixelsPerUnit
                : Vector2.one;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            r.transform.localScale = new Vector3(width / Mathf.Max(unit.x, 0.0001f),
                height / Mathf.Max(unit.y, 0.0001f), 1f);
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
                var go = new GameObject("Fold");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plainMaterial == null)
                {
                    plainMaterial = r.sharedMaterial;
                }
            }
            // A pooled renderer may have last worn a water tile's material or a fold's block.
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

        private void Release(Batch batch)
        {
            for (int i = 0; i < batch.Folds.Count; i++)
            {
                Fold f = batch.Folds[i];
                Return(f.Shadow);
                Return(f.Ghost);
                Return(f.Back);
                Return(f.Middle);
                Return(f.Plate);
                Return(f.Seam);
                Return(f.Lip);
                for (int m = 0; m < 4; m++)
                {
                    Return(f.Marks[m]);
                }
                for (int s = 0; s < 2; s++)
                {
                    Return(f.Strands[s]);
                }
            }
        }

        // =================================================================== shared art

        private static Sprite softSquareSprite;

        private static Sprite slitSprite;

        private static Sprite lineSprite;

        private static Sprite markSprite;

        /// <summary>The contact shadow: a rounded square whose edge softens inward.</summary>
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

        /// <summary>The seam: a thin bar with soft sides and ends that taper shut.</summary>
        private static Sprite SlitSprite()
        {
            if (slitSprite != null)
            {
                return slitSprite;
            }
            slitSprite = BuildBar(64, 16, 0.10f, 0.35f, 0.5f);
            return slitSprite;
        }

        /// <summary>A hairline - the seam's lip, a strand.</summary>
        private static Sprite LineSprite()
        {
            if (lineSprite != null)
            {
                return lineSprite;
            }
            lineSprite = BuildBar(64, 16, 0.12f, 0.22f, 0.5f);
            return lineSprite;
        }

        /// <summary>A compression mark: a short bar, strongest on the side that faces the cell's
        /// edge and fading toward its middle - pressure, not a line of light.</summary>
        private static Sprite MarkSprite()
        {
            if (markSprite != null)
            {
                return markSprite;
            }
            const int w = 64;
            const int h = 16;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float along = (x + 0.5f) / w;
                    float up = (y + 0.5f) / h;
                    float a = Mathf.Clamp01(along / 0.25f) * Mathf.Clamp01((1f - along) / 0.25f)
                        * Mathf.Pow(up, 1.6f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            markSprite = MakeSprite(w, h, px, w);
            return markSprite;
        }

        /// <summary>A horizontal bar: `ends` of its length taper, and across it the alpha falls
        /// off from a solid core of `core` (fraction of the height) over `soft`.</summary>
        private static Sprite BuildBar(int w, int h, float ends, float core, float soft)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float along = (x + 0.5f) / w;
                    float off = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                    float a = (off <= core ? 1f : Mathf.Clamp01(1f - (off - core) / soft))
                        * Mathf.Clamp01(along / ends) * Mathf.Clamp01((1f - along) / ends);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            return MakeSprite(w, h, px, w);
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
