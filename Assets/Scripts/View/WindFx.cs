// PURPOSE: What every part of "Rüzgar"'s presentation shares - its palette and opacities, the
// quality tiers and their budgets, the layer and debug switches the lab flips, the STORM CLOCK (when
// the front is where: the one timing spine every reaction hangs off), the shared turbulence that
// makes embers, droplets and spores ride ONE airflow, the materials, and a sprite pool.
//
// THE OPACITIES ARE LINEAR. The board renders in linear colour, and a PALE overlay behaves the
// opposite way to the dark ones the game has been bitten by before: 0.10 of near-white over the
// dark board lands near 0.39 sRGB from 0.19 - twice the lift the number promises - so a "soft" band
// written perceptually is a white haze over the grid, which is the brief's own fail. Every alpha
// here is the linear value, with the perceptual lift it gives over the board in the comment.
//
// EXTENSION POINT: a new tunable goes in Style, a new debug view in DebugFlags, a new tier in Budget.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    internal static class WindFx
    {
        public enum Quality
        {
            High,
            Medium,
            Low
        }

        /// <summary>Mobile starts a tier down; the lab can set any.</summary>
        public static Quality Level = Application.isMobilePlatform ? Quality.Medium : Quality.High;

        public static class Style
        {
            // ---- the air: cold, pale, never white ----
            public static readonly Color Air = new Color(0.78f, 0.92f, 0.97f);
            public static readonly Color AirDeep = new Color(0.62f, 0.76f, 0.88f);
            public static readonly Color AirLavender = new Color(0.86f, 0.88f, 0.98f);

            /// <summary>The one thing a refused aim changes: a muted rose on the edges.</summary>
            public static readonly Color Refused = new Color(0.80f, 0.60f, 0.66f);

            // ---- the corridor (linear alphas; ~3x on the dark board, ~1.3x on a cube) ----
            public static float BodyAlpha = 0.019f;        // reads ~0.06 over the board
            public static float PreviewBodyAlpha = 0.026f;
            public static float EdgeAlpha = 0.05f;
            public static float PreviewEdgeAlpha = 0.085f;
            public static float FrontAlpha = 0.05f;        // the brief's 0.08-0.18, perceptual
            public static float Doublet = 0.05f;           // ~1-2 px of "refraction", drawn
            public static float PreviewDoublet = 0.018f;

            /// <summary>Cells a second the air runs down the lane: the cast, and the calmer aim.</summary>
            public static float FlowSpeed = 9f;
            public static float PreviewFlowSpeed = 2.4f;

            // ---- the ribbons ----
            public static float RibbonAlpha = 0.085f;      // moderate: the storm's readable part
            public static float PreviewRibbonAlpha = 0.055f;
            public static float RibbonWidth = 0.2f;        // cells
            public static float RibbonMinShare = 0.12f;    // of the corridor's length
            public static float RibbonMaxShare = 0.30f;
            public static float RibbonWiggle = 0.10f;      // cells of lateral wander

            // ---- the motes ----
            public static float MoteAlpha = 0.16f;
            public static float MotePixelsMin = 1.2f;
            public static float MotePixelsMax = 3f;

            // ---- the storm's own clock ----
            public static float LockSeconds = 0.13f;       // "the air is being pressed"
            public static float TravelMin = 0.34f;
            public static float TravelMax = 0.85f;
            public static float ReleaseSeconds = 0.18f;    // the front letting go at the end
            public static float RibbonTail = 0.10f;        // ribbons outlive the front by this
            public static float DistortionTail = 0.15f;

            // ---- fire ----
            public static readonly Color EmberCore = new Color(1f, 0.90f, 0.52f);
            public static readonly Color EmberOuter = new Color(1f, 0.50f, 0.16f);
            public static readonly Color EmberTail = new Color(0.42f, 0.13f, 0.06f);
            public static float FireLean = 0.12f;
            public static float EmberFlightMin = 0.20f;
            public static float EmberFlightMax = 0.45f;
            public static float IgniteSpot = 0.08f;
            public static float IgniteSpread = 0.12f;
            public static float IgniteTongues = 0.15f;
            public static float IgniteSettle = 0.12f;

            // ---- water ----
            public static readonly Color Aqua = new Color(0.55f, 0.88f, 1f);
            public static float WaterPress = 0.09f;
            public static float WaterArrive = 0.16f;
            /// <summary>How fast pushed water glides, as a share of the front's own speed - a
            /// little slower, so the wind is seen to be ahead of what it carries.</summary>
            public static float WaterGlideShare = 0.85f;
            /// <summary>The longest a push may take, however far it goes.</summary>
            public static float WaterGlideMax = 0.42f;

            // ---- infection (the core view's own clean, cyan-leaning greens) ----
            public static readonly Color SporeLime = new Color(0.62f, 1f, 0.55f);
            public static readonly Color SporeDeep = new Color(0.10f, 0.52f, 0.28f);
            public static float SporePress = 0.08f;
            public static float SporeFlightMin = 0.26f;
            public static float SporeFlightMax = 0.45f;
            public static float SporeBranch = 0.12f;
            public static float SporePull = 0.10f;
            public static float SporeSeed = 0.22f;
        }

        /// <summary>Layers the lab can take away one at a time. All on in the game.</summary>
        public static class Layers
        {
            public static bool Front = true;
            public static bool Body = true;
            public static bool Ribbons = true;
            public static bool Motes = true;
            public static bool Distortion = true;
            public static bool Reactions = true;

            public static void AllOn()
            {
                Front = Body = Ribbons = Motes = Distortion = Reactions = true;
            }
        }

        public static class DebugFlags
        {
            public static bool ShowWindOrigin;
            public static bool ShowWindEndpoint;
            public static bool ShowWindVector;
            public static bool ShowWindWidth;
            public static bool ShowWindMaxLength;
            public static bool ShowWindCorridorBounds;
            public static bool ShowWindAffectedCells;
            public static bool ShowWindAffectedEntities;
            public static bool ShowStormFront;
            public static bool ShowPressureBody;
            public static bool ShowFlowRibbons;
            public static bool ShowWindParticles;
            public static bool ShowWindDistortion;
            public static bool ShowFireTransferSources;
            public static bool ShowFireTransferTargets;
            public static bool ShowFirePaths;
            public static bool ShowWaterSource;
            public static bool ShowWaterDestination;
            public static bool ShowWaterPath;
            public static bool ShowInfectionSource;
            public static bool ShowInfectionDestination;
            public static bool ShowInfectionPath;
            public static bool ShowInteractionTiming;
            public static bool ShowVisualBudget;
            public static bool ShowLabel;

            public static bool Any
            {
                get
                {
                    return ShowWindOrigin || ShowWindEndpoint || ShowWindVector || ShowWindWidth
                        || ShowWindMaxLength || ShowWindCorridorBounds || ShowWindAffectedCells
                        || ShowWindAffectedEntities || ShowStormFront || ShowPressureBody
                        || ShowFlowRibbons || ShowWindParticles || ShowWindDistortion
                        || ShowFireTransferSources || ShowFireTransferTargets || ShowFirePaths
                        || ShowWaterSource || ShowWaterDestination || ShowWaterPath
                        || ShowInfectionSource || ShowInfectionDestination || ShowInfectionPath
                        || ShowInteractionTiming || ShowVisualBudget || ShowLabel;
                }
            }

            public static void AllOff()
            {
                ShowWindOrigin = ShowWindEndpoint = ShowWindVector = ShowWindWidth = false;
                ShowWindMaxLength = ShowWindCorridorBounds = ShowWindAffectedCells = false;
                ShowWindAffectedEntities = ShowStormFront = ShowPressureBody = false;
                ShowFlowRibbons = ShowWindParticles = ShowWindDistortion = false;
                ShowFireTransferSources = ShowFireTransferTargets = ShowFirePaths = false;
                ShowWaterSource = ShowWaterDestination = ShowWaterPath = false;
                ShowInfectionSource = ShowInfectionDestination = ShowInfectionPath = false;
                ShowInteractionTiming = ShowVisualBudget = ShowLabel = false;
            }
        }

        // ------------------------------------------------------------------ budgets

        /// <summary>Flow ribbons alive at once.</summary>
        public static int RibbonBudget
        {
            get { return Level == Quality.High ? 9 : Level == Quality.Medium ? 6 : 4; }
        }

        /// <summary>Motes for a full-length corridor.</summary>
        public static int MoteBudget
        {
            get { return Level == Quality.High ? 28 : Level == Quality.Medium ? 18 : 0; }
        }

        /// <summary>Hero flame scraps across the whole gust, and per fire.</summary>
        public static int HeroScrapBudget
        {
            get { return Level == Quality.High ? 16 : Level == Quality.Medium ? 10 : 6; }
        }

        public static int TinyEmberBudget
        {
            get { return Level == Quality.High ? 40 : Level == Quality.Medium ? 20 : 6; }
        }

        public static float DoubletScale
        {
            get { return Level == Quality.High ? 1f : Level == Quality.Medium ? 0.55f : 0f; }
        }

        public static bool Trails
        {
            get { return Level != Quality.Low; }
        }

        // ------------------------------------------------------------------ the storm clock

        /// <summary>
        /// When the storm's front is where. Everything a gust does to anything starts when the
        /// front REACHES it - this is the one place that says when that is, from where the thing
        /// lies along the gust (a presentation delay only: what happens is Core's).
        /// </summary>
        public struct Clock
        {
            public float Lock;
            public float Travel;
            public float Length;

            public static Clock For(WindGust gust)
            {
                float k = Mathf.InverseLerp(1.5f, 8.5f, gust.Length);
                return new Clock
                {
                    Lock = Style.LockSeconds,
                    Travel = Mathf.Lerp(Style.TravelMin, Style.TravelMax, k),
                    Length = gust.Length
                };
            }

            /// <summary>Cells a second the front runs.</summary>
            public float Speed
            {
                get { return (Length + 1f) / Mathf.Max(0.05f, Travel); }
            }

            /// <summary>Where the front is, in cells along the gust (-0.5 at the back edge).</summary>
            public float FrontAt(float time)
            {
                return -0.5f + (time - Lock) * Speed;
            }

            /// <summary>When the front reaches a point this far along the gust.</summary>
            public float ReachTime(float along)
            {
                float a = Mathf.Clamp(along + 0.5f, 0f, Length + 1f);
                return Lock + a / Speed;
            }

            /// <summary>When the front arrives at the gust's end.</summary>
            public float End
            {
                get { return Lock + Travel; }
            }
        }

        // ------------------------------------------------------------------ one airflow

        /// <summary>
        /// The lateral push of the air, in cells, at a point this far along the gust at this
        /// moment. Embers, droplets, spores and ribbons all add the SAME field to their own path,
        /// which is what makes four different materials ride one wind instead of four effects
        /// sharing a direction.
        /// </summary>
        public static float Turbulence(float along, float time, int seed)
        {
            float s = (seed & 255) * 0.37f;
            return 0.085f * Mathf.Sin(along * 1.7f - time * 9f + s)
                + 0.05f * Mathf.Sin(along * 3.9f - time * 15f + s * 1.9f);
        }

        /// <summary>
        /// A flow ribbon's own wander off the wind's line, in cells: ONE slow bow along its length
        /// and a breath of movement on top. The shared turbulence is too busy for something a cell
        /// or two long - a ribbon that wiggles three times is a hair on the lens, and one that
        /// snakes is a magic beam.
        /// </summary>
        public static float RibbonWander(float along, float time, int seed)
        {
            float s = (seed & 255) * 0.37f;
            return (Style.RibbonWiggle / 0.1f)
                * (0.085f * Mathf.Sin(along * 0.8f + s) + 0.025f * Mathf.Sin(along * 1.9f - time * 5f + s * 1.7f));
        }

        public static float Hash(int a, int b)
        {
            unchecked
            {
                uint v = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                v ^= v >> 13;
                v *= 1274126177u;
                v ^= v >> 16;
                return (v & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static Color Tint(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
        }

        // ------------------------------------------------------------------ materials

        private static Material glow;
        private static Material glowMesh;
        private static Material corridor;
        private static Material carry;
        private static bool lookedGlow;
        private static bool lookedCorridor;
        private static bool lookedCarry;

        private static Shader Find(string name, string resource)
        {
            Shader shader = Shader.Find(name);
            if (shader == null)
            {
                shader = Resources.Load<Shader>(resource);
            }
            return shader != null && shader.isSupported ? shader : null;
        }

        /// <summary>The glow Simetri already uses (premultiplied, mostly additive): light that
        /// brightens what is under it rather than painting over it. Null without the shader.</summary>
        public static Material Glow
        {
            get
            {
                if (!lookedGlow)
                {
                    lookedGlow = true;
                    Shader shader = Find("ProjectBlock/SymmetryGlow", "Shaders/SymmetryGlow");
                    if (shader != null)
                    {
                        glow = new Material(shader) { name = "WindGlow (shared)" };
                        glowMesh = new Material(shader)
                        {
                            name = "WindGlowMesh (shared)",
                            mainTexture = Texture2D.whiteTexture
                        };
                    }
                }
                return glow;
            }
        }

        /// <summary>The same glow for the strip meshes, which carry no texture of their own.</summary>
        public static Material GlowMesh
        {
            get
            {
                if (Glow == null)
                {
                    if (glowMesh == null)
                    {
                        glowMesh = new Material(Shader.Find("Sprites/Default"))
                        {
                            name = "WindRibbon (fallback)",
                            mainTexture = Texture2D.whiteTexture
                        };
                    }
                }
                return glowMesh;
            }
        }

        public static Material Corridor
        {
            get
            {
                if (!lookedCorridor)
                {
                    lookedCorridor = true;
                    Shader shader = Find("ProjectBlock/WindCorridor", "Shaders/WindCorridor");
                    if (shader != null)
                    {
                        corridor = new Material(shader) { name = "WindCorridor (shared)" };
                    }
                }
                return corridor;
            }
        }

        public static Material Carry
        {
            get
            {
                if (!lookedCarry)
                {
                    lookedCarry = true;
                    Shader shader = Find("ProjectBlock/WindCarry", "Shaders/WindCarry");
                    if (shader != null)
                    {
                        carry = new Material(shader) { name = "WindCarry (shared)" };
                    }
                }
                return carry;
            }
        }
    }

    /// <summary>A pool of sprite renderers under one parent - everything the wind draws that is a
    /// sprite is rented from one of these and handed back, so a gust allocates nothing after its
    /// first.</summary>
    internal sealed class WindSprites
    {
        private readonly Transform parent;
        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();
        private Material plain;

        public int Live { get; private set; }

        public WindSprites(Transform parent)
        {
            this.parent = parent;
        }

        public SpriteRenderer Rent(Sprite sprite, int order, Material material)
        {
            SpriteRenderer r;
            if (spare.Count > 0)
            {
                r = spare.Pop();
            }
            else
            {
                var go = new GameObject("WindFx");
                go.transform.SetParent(parent, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plain == null)
                {
                    plain = r.sharedMaterial;
                }
            }
            Material wanted = material != null ? material : plain;
            if (wanted != null && r.sharedMaterial != wanted)
            {
                r.sharedMaterial = wanted;
            }
            r.SetPropertyBlock(null);
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = Color.white;
            r.transform.rotation = Quaternion.identity;
            r.enabled = true;
            Live++;
            return r;
        }

        /// <summary>Rents with the glow material (or the plain one without the shader).</summary>
        public SpriteRenderer RentGlow(Sprite sprite, int order)
        {
            return Rent(sprite, order, WindFx.Glow);
        }

        public void Return(ref SpriteRenderer r)
        {
            if (r == null)
            {
                return;
            }
            r.enabled = false;
            r.SetPropertyBlock(null);
            spare.Push(r);
            r = null;
            Live--;
        }

        public void ReturnAll(List<SpriteRenderer> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                SpriteRenderer r = list[i];
                Return(ref r);
            }
            list.Clear();
        }

        /// <summary>Puts a sprite at a world point, turned, at a world size.</summary>
        public static void Place(SpriteRenderer r, Vector2 at, float angle, float width, float height, Color colour)
        {
            if (r == null || r.sprite == null)
            {
                return;
            }
            Transform t = r.transform;
            t.position = new Vector3(at.x, at.y, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, angle);
            Vector2 unit = r.sprite.bounds.size;
            t.localScale = new Vector3(width / Mathf.Max(unit.x, 0.0001f),
                height / Mathf.Max(unit.y, 0.0001f), 1f);
            r.color = colour;
        }
    }
}
