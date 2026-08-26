// PURPOSE: Overtime ("uzatma") feedback: a particle FIRE burning along the arena's
// border. It ignites once the player continues past an advance offer and grows deeper
// into overtime - the fire gets BIGGER, it does not multiply. Reset per round via
// SetState(0, ...). Rendered just above the board cells so the fire licks over the edge.
//
// The fire is THREE stacked layers, because one emitter can only carry one set of
// lifetime curves and a real fire is three different things at once:
//   1. GLOW    - a low, wide, short-lived bed of embers hugging the rim. The root the
//                tongues grow out of; without it the flames look like they float.
//   2. TONGUES - the flames themselves. Buoyant (they accelerate upward and then coast
//                against drag), curled by a noise field, pinched to a tip by their size
//                curve, and swayed as a body by a shared wind.
//   3. EMBERS  - a few tiny sparks that escape past the tips, twinkling as they drift.
//                These are what the eye follows, so they carry most of the "alive" feel.
// All three are ADDITIVE with a soft radial texture: fire is emissive, so overlapping
// tongues have to compound into a white-hot core rather than stack flatly. An untextured
// particle is a hard square, which is what stops a fire reading as one.
//
// Emission is not uniform along the rim. A handful of HOT SPOTS wander around the
// perimeter and breathe, and most particles are born near one of them, so the fire licks
// in moving tongues instead of an even ribbon of noise. A global flicker (two
// incommensurate sines plus an occasional flare) modulates every rate together.
//
// TWO RULES KEEP THIS LOOKING LIKE FIRE RATHER THAN ASHES, and it looked like ashes when
// either was broken:
//   * DENSITY. Rates are per WORLD UNIT of border and sizes are cut from the arena's short
//     side, never world constants. The arena is a fixed ~6.5 units across, so its rim is
//     ~26 units long: particles that do not OVERLAP along that length are a scatter of
//     sparks, however well they are coloured.
//   * NO BROWN. Every gradient stays saturated and hot for its whole life and dies on ALPHA
//     alone. A colour walked down to soot is invisible under additive blending and paints
//     grey over the arena under the fallback - either way the fire looks burned out.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Arena border fire that scales with the round's overtime depth.</summary>
    public sealed class FlameStreakView : MonoBehaviour
    {
        /// <summary>Highest tier the fire will grow to. Public so the animation lab's overtime
        /// knob cannot drift from what SetState will actually accept.</summary>
        public const int MaxLevel = 12;

        /// <summary>The tier at which the core is already fully white-hot. DELIBERATELY NOT
        /// MaxLevel: Hotness is a ratio, so raising the ceiling alone would have made every
        /// tier below it cooler than it used to be - tier 6 would have dropped from a white
        /// core to a half-warm one. Kept at the old ceiling, so tiers 0-6 look exactly as they
        /// always have and the tiers above them keep growing in size and emission instead.</summary>
        private const int FullHeatLevel = 6;

        // Enough spots that no side ever goes dark for long, few enough that each one reads
        // as a distinct tongue rather than dissolving back into an even band.
        private const int HotSpotCount = 8;

        // How much of the emission ignores the hot spots. The rim needs a FLOOR of fire under
        // it: where the floor is too thin the gaps between tongues read as a fire that has
        // burned out and left embers, which is not what a fire looks like.
        private const float BaselineShare = 0.45f;

        // Emission is per WORLD UNIT of border, not per second flat. The arena is a fixed
        // 6.5 units across whatever the grid size, so its rim is ~26 units long - a rate that
        // looks like a fire on a short edge is a scatter of sparks spread over that. DENSITY
        // is what separates fire from ash: the particles have to OVERLAP.
        private const float GlowPerUnit = 2.2f;
        private const float GlowPerUnitPerLevel = 1.1f;
        private const float TonguePerUnit = 7f;
        private const float TonguePerUnitPerLevel = 4.2f;
        private const float EmberPerUnit = 0.22f;
        private const float EmberPerUnitPerLevel = 0.16f;

        private static readonly Color EmberOrange = new Color(1f, 0.55f, 0.12f);
        private static readonly Color EmberYellow = new Color(1f, 0.88f, 0.35f);

        // The core of a flame gets hotter - and therefore whiter - the deeper the overtime.
        private static readonly Color CoolCore = new Color(1f, 0.70f, 0.26f);
        private static readonly Color HotCore = new Color(0.94f, 0.96f, 1f);

        private static Texture2D softTexture;
        private static Texture2D sparkTexture;

        private ParticleSystem glow;
        private ParticleSystem tongues;
        private ParticleSystem embers;
        private ParticleSystem.ForceOverLifetimeModule tongueForce;
        private ParticleSystem.ForceOverLifetimeModule emberForce;

        // Cached so the per-frame pump does not allocate a delegate three times a frame.
        private System.Action emitGlow;
        private System.Action emitTongue;
        private System.Action emitEmber;

        private int level;
        private Rect area;
        private float glowAccumulator;
        private float tongueAccumulator;
        private float emberAccumulator;
        private float clock;

        // Perimeter positions in [0,1) and the speed each one drifts at. Heat is recomputed
        // every frame from the clock, so a spot fades and swells where it stands.
        private readonly float[] spotAt = new float[HotSpotCount];
        private readonly float[] spotDrift = new float[HotSpotCount];
        private readonly float[] spotPhase = new float[HotSpotCount];
        private readonly float[] spotRate = new float[HotSpotCount];
        private readonly float[] spotHeat = new float[HotSpotCount];

        private void Awake()
        {
            // Particle budgets are sized for the TOP tier: the fire is dense by design, and a
            // budget that quietly runs out is a fire that thins to embers at exactly the
            // moment the player has pushed it furthest.
            glow = BuildLayer("Glow", 1200, 3, GlowGradient(),
                Curve(0f, 0.75f, 0.3f, 1f, 1f, 0.35f), false);
            tongues = BuildLayer("Tongues", 4000, 4, TongueGradient(),
                // swell out of the bed, then taper to a tip - the taper is what makes a
                // blob of particles read as a flame at all
                Curve(0f, 0.55f, 0.22f, 1f, 1f, 0.06f), false);
            embers = BuildLayer("Embers", 800, 5, EmberGradient(),
                Curve(0f, 0.9f, 0.7f, 1f, 1f, 0f), true);

            AddNoise(tongues, 0.35f, 1.5f, 0.55f, 2);
            AddNoise(embers, 0.55f, 0.6f, 0.25f, 1);

            // Buoyancy: a flame does not launch at speed, it ACCELERATES upward and then
            // coasts against drag. Constant-velocity particles are the other half of why
            // the old fire read as a fountain.
            tongueForce = Buoyancy(tongues, 1.7f);
            emberForce = Buoyancy(embers, 0.85f);
            Drag(tongues, 0.75f);
            Drag(embers, 0.25f);

            ParticleSystem.RotationOverLifetimeModule spin = tongues.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-70f, 70f);

            emitGlow = EmitGlow;
            emitTongue = EmitTongue;
            emitEmber = EmitEmber;

            for (int i = 0; i < HotSpotCount; i++)
            {
                spotAt[i] = i / (float)HotSpotCount + Random.Range(-0.03f, 0.03f);
                spotDrift[i] = Random.Range(0.012f, 0.05f) * (Random.value < 0.5f ? -1f : 1f);
                spotPhase[i] = Random.Range(0f, Mathf.PI * 2f);
                spotRate[i] = Random.Range(0.9f, 2.3f);
            }
        }

        /// <summary>Sets the fire intensity (0 = off, grows with overtime depth) and the
        /// arena to burn around.</summary>
        public void SetState(int overtimeLevel, Rect boardArea)
        {
            int newLevel = Mathf.Min(overtimeLevel, MaxLevel);
            if (newLevel == 0 && level > 0)
            {
                glow.Clear();
                tongues.Clear();
                embers.Clear();
            }
            level = newLevel;
            area = boardArea;
        }

        private void Update()
        {
            if (level <= 0 || area.width <= 0f)
            {
                return;
            }
            float dt = Time.deltaTime;
            clock += dt;

            // A fire breathes. Two frequencies that never line up, plus a rare flare-up, so
            // the loop is long enough that the eye cannot learn it.
            float flicker = 0.78f
                + 0.16f * Mathf.Sin(clock * 7.3f)
                + 0.11f * Mathf.Sin(clock * 13.9f + 1.7f)
                + 0.9f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(clock * 1.13f + 0.3f)), 14f);

            // The whole body of fire leans one way and then the other. Shared by every
            // particle alive, which is what makes it look like ONE fire in a draught rather
            // than a thousand independent sparks.
            float sway = 0.42f * Mathf.Sin(clock * 0.9f) + 0.18f * Mathf.Sin(clock * 2.27f + 1.1f);
            tongueForce.x = new ParticleSystem.MinMaxCurve(sway);
            emberForce.x = new ParticleSystem.MinMaxCurve(sway * 1.5f);

            for (int i = 0; i < HotSpotCount; i++)
            {
                spotAt[i] = Mathf.Repeat(spotAt[i] + spotDrift[i] * dt, 1f);
                spotHeat[i] = 0.15f + 0.85f * Mathf.Abs(Mathf.Sin(clock * spotRate[i] + spotPhase[i]));
            }

            float rim = 2f * (area.width + area.height);
            Pump(ref glowAccumulator,
                rim * (GlowPerUnit + GlowPerUnitPerLevel * SoftLevel) * (0.85f + 0.25f * flicker),
                emitGlow, dt);
            Pump(ref tongueAccumulator,
                rim * (TonguePerUnit + TonguePerUnitPerLevel * SoftLevel) * flicker,
                emitTongue, dt);
            Pump(ref emberAccumulator,
                rim * (EmberPerUnit + EmberPerUnitPerLevel * SoftLevel) * flicker,
                emitEmber, dt);
        }

        private static void Pump(ref float accumulator, float rate, System.Action emit, float dt)
        {
            accumulator += dt * rate;
            // A frame spike must not be paid back as a wall of particles all at once. Sized
            // for the densest tier - clipping it lower would thin the fire on every hitch.
            if (accumulator > 150f)
            {
                accumulator = 150f;
            }
            while (accumulator >= 1f)
            {
                accumulator -= 1f;
                emit();
            }
        }

        private float Hotness
        {
            get { return Mathf.Clamp01(level / (float)FullHeatLevel); }
        }

        /// <summary>The level everything that GROWS is driven by. Identical to the raw level up
        /// to FullHeatLevel, then it keeps climbing at 40% pace: a straight multiple of a level
        /// that now reaches 12 would double the flame height and speed of the old ceiling, and
        /// a fire tall enough to cover the arena is unplayable however good it looks.</summary>
        private float SoftLevel
        {
            get
            {
                return level <= FullHeatLevel
                    ? level
                    : FullHeatLevel + (level - FullHeatLevel) * 0.4f;
            }
        }

        /// <summary>The arena's short side. Particle sizes are cut from it rather than being
        /// world constants, so the fire keeps its proportions on the smaller mirror-world
        /// arena instead of swamping it.</summary>
        private float Unit
        {
            get { return Mathf.Min(area.width, area.height); }
        }

        private void EmitGlow()
        {
            float lean;
            Vector2 point = EmitPoint(out lean);
            var p = new ParticleSystem.EmitParams();
            p.position = new Vector3(point.x, point.y, 0f);
            p.velocity = new Vector3(Random.Range(-0.12f, 0.12f) + lean * 0.1f,
                Random.Range(0.1f, 0.4f), 0f);
            // Wide and overlapping: the bed has to be a CONTINUOUS band of light, because it
            // is what keeps the rim lit between two tongues.
            p.startSize = Unit * Random.Range(0.085f, 0.16f) * (0.9f + 0.055f * SoftLevel);
            p.startLifetime = Random.Range(0.22f, 0.5f);
            p.startColor = Color.Lerp(EmberOrange, CoreColor(), 0.3f);
            glow.Emit(p, 1);
        }

        private void EmitTongue()
        {
            float lean;
            Vector2 point = EmitPoint(out lean);
            var p = new ParticleSystem.EmitParams();
            p.position = new Vector3(point.x, point.y, 0f);
            p.velocity = new Vector3(
                Random.Range(-0.28f, 0.28f) + lean * 0.3f,
                Random.Range(0.65f, 1.25f) * (0.85f + 0.15f * SoftLevel),
                0f);
            // Roughly a third of a cell across. At a quarter of this the tongues never touched
            // each other and the fire read as a scatter of sparks.
            p.startSize = Unit * Random.Range(0.05f, 0.10f) * (0.88f + 0.05f * SoftLevel);
            p.startLifetime = Random.Range(0.5f, 1.05f) * (0.9f + 0.06f * SoftLevel);
            p.rotation = Random.Range(0f, 360f);
            p.startColor = Color.Lerp(Color.Lerp(EmberOrange, EmberYellow, Random.value),
                CoreColor(), 0.45f + 0.35f * Hotness);
            tongues.Emit(p, 1);
        }

        private void EmitEmber()
        {
            float lean;
            Vector2 point = EmitPoint(out lean);
            var p = new ParticleSystem.EmitParams();
            p.position = new Vector3(point.x, point.y + Random.Range(0f, 0.25f), 0f);
            p.velocity = new Vector3(
                Random.Range(-0.45f, 0.45f) + lean * 0.35f,
                Random.Range(1.1f, 2.1f) * (0.9f + 0.08f * SoftLevel),
                0f);
            p.startSize = Unit * Random.Range(0.011f, 0.022f) * (0.9f + 0.04f * SoftLevel);
            p.startLifetime = Random.Range(1.1f, 2.4f);
            p.startColor = Color.Lerp(EmberYellow, CoreColor(), 0.5f);
            embers.Emit(p, 1);
        }

        private Color CoreColor()
        {
            return Color.Lerp(CoolCore, HotCore, Hotness);
        }

        /// <summary>Picks a birth point: usually clustered on a wandering hot spot, sometimes
        /// anywhere on the rim. <paramref name="lean"/> is a -1..1 nudge toward the arena's
        /// middle, so the flames lick INWARD over the board instead of standing straight up
        /// in a rectangle.</summary>
        private Vector2 EmitPoint(out float lean)
        {
            float t;
            if (Random.value < BaselineShare)
            {
                t = Random.value;
            }
            else
            {
                t = spotAt[PickHotSpot()]
                    // two uniforms make a triangular spread: dense at the spot, thin at the
                    // edges, which is the shape of a tongue's foot
                    + (Random.value - Random.value) * 0.075f;
            }
            Vector2 point = PerimeterPoint(t);
            lean = Mathf.Clamp((area.center.x - point.x) / (area.width * 0.5f), -1f, 1f);
            return point;
        }

        private int PickHotSpot()
        {
            float total = 0f;
            for (int i = 0; i < HotSpotCount; i++)
            {
                total += spotHeat[i];
            }
            float roll = Random.value * total;
            for (int i = 0; i < HotSpotCount; i++)
            {
                roll -= spotHeat[i];
                if (roll <= 0f)
                {
                    return i;
                }
            }
            return HotSpotCount - 1;
        }

        /// <summary>Walks the arena's border, t in [0,1) starting at the bottom-left corner.</summary>
        private Vector2 PerimeterPoint(float t)
        {
            float w = area.width;
            float h = area.height;
            float d = Mathf.Repeat(t, 1f) * (2f * (w + h));
            if (d < w)
            {
                return new Vector2(area.xMin + d, area.yMin);
            }
            d -= w;
            if (d < h)
            {
                return new Vector2(area.xMax, area.yMin + d);
            }
            d -= h;
            if (d < w)
            {
                return new Vector2(area.xMax - d, area.yMax);
            }
            d -= w;
            return new Vector2(area.xMin, area.yMax - d);
        }

        // ---- layer construction -------------------------------------------------------

        /// <summary>One emitter of the fire. Emission and shape stay OFF - every particle is
        /// placed by hand from Update, which is what lets the hot spots exist at all.</summary>
        private ParticleSystem BuildLayer(string name, int maxParticles, int sortingOrder,
            Gradient gradient, AnimationCurve sizeCurve, bool stretched)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
            particleRenderer.material = FireMaterial(stretched ? SparkTexture : SoftTexture);
            particleRenderer.sortingOrder = sortingOrder;
            if (stretched)
            {
                // A spark is a streak, not a dot: stretching along velocity is what sells it
                // as something moving fast enough to glow.
                particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                particleRenderer.lengthScale = 2.2f;
                particleRenderer.velocityScale = 0.06f;
            }
            return system;
        }

        private static void AddNoise(ParticleSystem system, float strength, float frequency,
            float scroll, int octaves)
        {
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = new ParticleSystem.MinMaxCurve(strength * 0.4f, strength);
            noise.frequency = frequency;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(scroll);
            noise.octaveCount = octaves;
            // Damping ties the curl to the particle's size, so the big slow ones at the base
            // are not thrown around as hard as the small fast ones at the tip.
            noise.damping = true;
        }

        private static ParticleSystem.ForceOverLifetimeModule Buoyancy(ParticleSystem system,
            float lift)
        {
            ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.y = new ParticleSystem.MinMaxCurve(lift * 0.7f, lift);
            return force;
        }

        private static void Drag(ParticleSystem system, float drag)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule limit =
                system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(8f);   // high: this is drag, not a cap
            limit.dampen = 0.05f;
            limit.drag = new ParticleSystem.MinMaxCurve(drag);
            limit.multiplyDragByParticleSize = true;
        }

        // ---- gradients ----------------------------------------------------------------

        // ---- gradients: A FIRE NEVER GOES BROWN --------------------------------------
        //
        // The rule all three share, and the thing that had them reading as ashes: the colour
        // stays SATURATED and hot for the whole life and only the ALPHA dies. Walking the
        // colour down to soot or dull brown costs a particle its last third - under additive
        // blending a dark colour adds nothing, so it is simply an invisible particle still
        // occupying the shape, and on the alpha-blended fallback it is worse, because then it
        // literally paints grey-brown over the arena. Either way the eye sees a fire whose
        // edges have burned out. Let it die BRIGHT.

        /// <summary>The bed: deep orange, dying quickly, but never darker than a live coal.
        /// It only has to look like something the tongues are standing in.</summary>
        private static Gradient GlowGradient()
        {
            return MakeGradient(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.62f, 0.22f), 0f),
                    new GradientColorKey(new Color(1f, 0.38f, 0.10f), 0.55f),
                    new GradientColorKey(new Color(0.95f, 0.22f, 0.06f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.5f, 0f),
                    new GradientAlphaKey(0.55f, 0.3f),
                    new GradientAlphaKey(0f, 1f)
                });
        }

        /// <summary>A blackbody walk: white-hot at birth, cooling through yellow and orange to
        /// a saturated ember red. Alpha holds most of the way and lets go late, so the tips
        /// dissolve rather than being cut off.</summary>
        private static Gradient TongueGradient()
        {
            return MakeGradient(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.97f, 0.85f), 0f),
                    new GradientColorKey(new Color(1f, 0.82f, 0.35f), 0.3f),
                    new GradientColorKey(new Color(1f, 0.48f, 0.12f), 0.62f),
                    new GradientColorKey(new Color(0.97f, 0.24f, 0.07f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.85f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
        }

        /// <summary>A spark tumbling as it rises catches the light on and off - the alpha keys
        /// ARE that twinkle. It only dips, never goes out: a spark that blinks all the way to
        /// nothing and back is a speck of drifting ash, not an ember.</summary>
        private static Gradient EmberGradient()
        {
            return MakeGradient(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.96f, 0.78f), 0f),
                    new GradientColorKey(new Color(1f, 0.68f, 0.22f), 0.55f),
                    new GradientColorKey(new Color(1f, 0.35f, 0.08f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.62f, 0.18f),
                    new GradientAlphaKey(1f, 0.35f),
                    new GradientAlphaKey(0.58f, 0.52f),
                    new GradientAlphaKey(0.95f, 0.68f),
                    new GradientAlphaKey(0.55f, 0.84f),
                    new GradientAlphaKey(0f, 1f)
                });
        }

        private static Gradient MakeGradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            return gradient;
        }

        private static AnimationCurve Curve(float t0, float v0, float t1, float v1,
            float t2, float v2)
        {
            var curve = new AnimationCurve(new Keyframe(t0, v0), new Keyframe(t1, v1),
                new Keyframe(t2, v2));
            for (int i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }
            return curve;
        }

        // ---- material + textures ------------------------------------------------------

        /// <summary>Additive, so overlapping flames compound into a hot core. The falloff is
        /// baked into RGB as well as alpha, so the sprite still looks right if we fall back to
        /// the alpha-blended shader.</summary>
        private static Material FireMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null) { shader = Shader.Find("Particles/Additive"); }
            if (shader == null) { shader = Shader.Find("Mobile/Particles/Additive"); }
            if (shader == null)
            {
                // Worth saying out loud: alpha-blended, the flames stop compounding into a hot
                // core and the fire looks washed out. If this ever fires, the fix is a shader,
                // not more particles.
                Debug.LogWarning("FlameStreakView: no additive particle shader found; "
                    + "falling back to Sprites/Default and the fire will look flat.");
                shader = Shader.Find("Sprites/Default");
            }
            var material = new Material(shader);
            material.mainTexture = texture;
            return material;
        }

        private static Texture2D SoftTexture
        {
            get
            {
                if (softTexture == null)
                {
                    softTexture = RadialTexture(48, 2f);
                }
                return softTexture;
            }
        }

        private static Texture2D SparkTexture
        {
            get
            {
                if (sparkTexture == null)
                {
                    // A tighter core: a spark is a point of light, not a puff.
                    sparkTexture = RadialTexture(32, 4.5f);
                }
                return sparkTexture;
            }
        }

        /// <summary>A soft round dot. Without one, every particle is a hard SQUARE, which no
        /// amount of colour work will make read as fire.</summary>
        private static Texture2D RadialTexture(int size, float falloffPower)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);          // smoothstep: no ring at the edge
                    a = Mathf.Pow(a, falloffPower);
                    pixels[y * size + x] = new Color(a, a, a, a);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
    }
}
