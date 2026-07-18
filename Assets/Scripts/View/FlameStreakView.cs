// PURPOSE: Overtime ("uzatma") feedback: a particle FIRE burning along the arena's
// border. It ignites once the player continues past an advance offer and grows deeper
// into overtime - emission rate, particle size and rise speed all grow (the fire gets
// bigger, it does not multiply). Reset per round via SetState(0, ...). Rendered just
// above the board cells so the fire licks over the arena's edge.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Arena border fire that scales with the round's clean-sweep count.</summary>
    public sealed class FlameStreakView : MonoBehaviour
    {
        private const int MaxLevel = 6;

        private static readonly Color EmberOrange = new Color(1f, 0.55f, 0.12f);
        private static readonly Color EmberYellow = new Color(1f, 0.88f, 0.35f);

        private ParticleSystem particles;
        private int level;
        private Rect area;
        private float emitAccumulator;

        private void Awake()
        {
            particles = gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            // taper to a tip and fade to dark red - this is what makes it read as fire
            ParticleSystem.SizeOverLifetimeModule sizeModule = particles.sizeOverLifetime;
            sizeModule.enabled = true;
            sizeModule.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
            ParticleSystem.ColorOverLifetimeModule colorModule = particles.colorOverLifetime;
            colorModule.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.55f),
                    new GradientColorKey(new Color(0.55f, 0.1f, 0.05f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.7f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorModule.color = new ParticleSystem.MinMaxGradient(gradient);
            var particleRenderer = GetComponent<ParticleSystemRenderer>();
            particleRenderer.material = new Material(Shader.Find("Sprites/Default"));
            particleRenderer.sortingOrder = 3;
        }

        /// <summary>Sets the fire intensity (0 = off, grows with overtime depth) and the
        /// arena to burn around.</summary>
        public void SetState(int overtimeLevel, Rect boardArea)
        {
            int newLevel = Mathf.Min(overtimeLevel, MaxLevel);
            if (newLevel == 0 && level > 0)
            {
                particles.Clear();
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
            emitAccumulator += Time.deltaTime * (20f + 26f * level);
            while (emitAccumulator >= 1f)
            {
                emitAccumulator -= 1f;
                EmitOne();
            }
        }

        private void EmitOne()
        {
            var emitParams = new ParticleSystem.EmitParams();
            Vector2 point = RandomPerimeterPoint();
            emitParams.position = new Vector3(point.x, point.y, 0f);
            emitParams.velocity = new Vector3(
                Random.Range(-0.3f, 0.3f),
                Random.Range(0.9f, 1.6f) * (0.8f + 0.18f * level),
                0f);
            emitParams.startSize = Random.Range(0.13f, 0.3f) * (0.85f + 0.2f * level);
            emitParams.startColor = Color.Lerp(EmberOrange, EmberYellow, Random.value);
            particles.Emit(emitParams, 1);
        }

        private Vector2 RandomPerimeterPoint()
        {
            float w = area.width;
            float h = area.height;
            float d = Random.value * (2f * (w + h));
            if (d < w)
            {
                return new Vector2(area.xMin + d, area.yMin);
            }
            d -= w;
            if (d < w)
            {
                return new Vector2(area.xMin + d, area.yMax);
            }
            d -= w;
            if (d < h)
            {
                return new Vector2(area.xMin, area.yMin + d);
            }
            d -= h;
            return new Vector2(area.xMax, area.yMin + d);
        }
    }
}
