// PURPOSE: BossIdentityView's ATMOSPHERE - the layer every boss look shares, on top of whichever
// ambience is picked:
//
//   RED BACKDROP  - a red vertical gradient with a lifted pool behind the arena, laid OVER the
//                   backdrop's turquoise (between its dither and its vignette), so the screen reads
//                   red at the rim and the vignette still frames it. Opaque at full weight: it
//                   replaces the blue rather than tinting it, because red over turquoise in linear
//                   colour comes out brown.
//   RED SPARKS    - a sparse scatter of red motes leaving the arena's edges and drifting OUTWARD,
//                   fading as they go. Kept deliberately few (SparkCount) - it is a hint of heat
//                   coming off the board, not a particle effect.
//
// Its own roots, so rebuilding an ambience never takes it down with it. Fades with its own weight;
// an intro does NOT hold it back - the world is already red while the title card is up.

using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class BossIdentityView
    {
        /// <summary>Lab switches.</summary>
        public bool RedBackdrop = true;

        public bool RedParticles = true;

        public static class AtmosphereStyle
        {
            /// <summary>The red ground at mid-screen, and how far it lifts at the top / drops at
            /// the bottom. Lighter than it looks on paper - see BackdropView's note on linear
            /// colour: a dark red authored "right" reaches the screen as black.</summary>
            public static Color Ground = new Color(0.30f, 0.045f, 0.065f);

            public static float GradientStrength = 0.28f;

            /// <summary>The glow behind the arena.</summary>
            public static Color Pool = new Color(0.62f, 0.12f, 0.12f);

            public static float PoolStrength = 0.30f;

            public static Color SparkHot = new Color(1f, 0.32f, 0.26f);
            public static Color SparkCool = new Color(0.78f, 0.10f, 0.16f);
        }

        private const int AtmoGroundOrder = -210;
        private const int AtmoPoolOrder = -209;
        private const int SparkOrder = -44;
        private const int SparkCount = 18;

        private Transform atmoScreen;
        private Transform atmoWorld;
        private SpriteRenderer atmoGround;
        private SpriteRenderer atmoPool;
        private SpriteRenderer[] sparks;
        private bool atmosphereWanted;
        private float atmosphereWeight;
        private float atmosphereTime;
        private float backdropWeight;
        private float sparkWeight;
        private static Sprite redGradient;

        public void SetAtmosphere(bool on)
        {
            atmosphereWanted = on;
        }

        /// <summary>One line for the lab: what is actually on screen right now.</summary>
        public string Status
        {
            get
            {
                return builtAmbience + " " + Mathf.RoundToInt(Mathf.Clamp01(ambienceWeight) * 100f)
                    + "%" + (wantedAmbience != builtAmbience ? " -> " + wantedAmbience : string.Empty);
            }
        }

        private void BuildAtmosphere()
        {
            atmoScreen = new GameObject("AtmosphereScreen").transform;
            atmoScreen.SetParent(screenRoot, false);
            atmoWorld = new GameObject("AtmosphereWorld").transform;
            atmoWorld.SetParent(worldRoot, false);
            atmoGround = Part(atmoScreen, "RedGround", RedGradient, AtmoGroundOrder);
            atmoPool = Part(atmoWorld, "RedPool", SoftDot, AtmoPoolOrder);
            sparks = new SpriteRenderer[SparkCount];
            for (int i = 0; i < SparkCount; i++)
            {
                sparks[i] = Part(atmoWorld, "Spark" + i, SoftDot, SparkOrder);
            }
            BuildReactions();
        }

        private void TickAtmosphere(float dt)
        {
            if (atmoGround == null)
            {
                return;
            }
            atmosphereTime += dt;
            float step = dt / Mathf.Max(0.01f, Style.AmbienceFade);
            backdropWeight = Mathf.MoveTowards(backdropWeight,
                atmosphereWanted && RedBackdrop ? 1f : 0f, step);
            sparkWeight = Mathf.MoveTowards(sparkWeight,
                atmosphereWanted && RedParticles ? 1f : 0f, step);
            atmosphereWeight = Mathf.Max(backdropWeight, sparkWeight);

            Put(atmoGround, Vector2.zero, new Vector2(halfW, halfH) * 2.4f, 0f, Color.white,
                Smooth(backdropWeight));
            float pool = Mathf.Max(rect.width, rect.height) * 2.4f;
            Put(atmoPool, rect.center, new Vector2(pool, pool * 0.85f), 0f, AtmosphereStyle.Pool,
                (AtmosphereStyle.PoolStrength + ReactStyle.PoolFlare * reactEnergy) * Smooth(backdropWeight));

            float hw = rect.width * 0.5f;
            float hh = rect.height * 0.5f;
            float w = Smooth(sparkWeight);
            for (int i = 0; i < SparkCount; i++)
            {
                if (w <= 0f)
                {
                    sparks[i].color = Color.clear;
                    continue;
                }
                float period = 2.8f + 2.2f * Hash(i, 41);
                float cycle = atmosphereTime / period + Hash(i, 42);
                float phase = Mathf.Repeat(cycle, 1f);
                // A new direction every lap, so the scatter never settles into fixed spokes.
                int lap = Mathf.FloorToInt(cycle);
                float angle = (Hash(i * 31 + lap, 43) * 360f) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float toEdge = Mathf.Min(hw / Mathf.Max(0.0001f, Mathf.Abs(dir.x)),
                    hh / Mathf.Max(0.0001f, Mathf.Abs(dir.y)));
                float reach = 1.6f + 2.4f * Hash(i * 31 + lap, 44);
                var tangent = new Vector2(-dir.y, dir.x);
                float curl = (Hash(i * 31 + lap, 45) - 0.5f) * 0.9f;
                float travel = OutCubic(phase);
                Vector2 at = rect.center + dir * (toEdge + 0.05f + travel * reach)
                    + tangent * curl * travel * travel;
                // A passing front shoves the sparks outward.
                at += WavePush(at) * 0.35f;
                float alpha = Seg(phase, 0f, 0.10f) * (1f - Smooth(Seg(phase, 0.45f, 1f)));
                float size = Mathf.Lerp(0.16f, 0.06f, phase) * (0.7f + 0.6f * Hash(i, 46));
                Color c = Color.Lerp(AtmosphereStyle.SparkHot, AtmosphereStyle.SparkCool, phase);
                Put(sparks[i], at, Vector2.one * size, 0f, c, alpha * 0.9f * w);
            }
        }

        /// <summary>The red ground: dark at the bottom, lifted toward the top. A thin column -
        /// nothing varies across it.</summary>
        private static Sprite RedGradient
        {
            get
            {
                if (redGradient == null)
                {
                    const int h = 256;
                    const int w = 4;
                    Texture2D tex = NewTexture(w, h);
                    var px = new Color[w * h];
                    Color g = AtmosphereStyle.Ground;
                    float k = AtmosphereStyle.GradientStrength;
                    Color top = new Color(g.r * (1f + k), g.g * (1f + k), g.b * (1f + k), 1f);
                    Color bottom = new Color(g.r * (1f - k), g.g * (1f - k), g.b * (1f - k), 1f);
                    for (int y = 0; y < h; y++)
                    {
                        Color c = Color.Lerp(bottom, top, Smooth(y / (float)(h - 1)));
                        for (int x = 0; x < w; x++)
                        {
                            px[y * w + x] = c;
                        }
                    }
                    tex.SetPixels(px);
                    tex.Apply();
                    // Stretched, not square - one unit on each axis so Put's size means world units.
                    redGradient = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), w);
                }
                return redGradient;
            }
        }
    }
}
