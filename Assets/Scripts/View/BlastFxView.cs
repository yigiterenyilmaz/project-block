// PURPOSE: Particle bursts for line explosions and clean sweeps. One persistent,
// code-configured ParticleSystem; callers emit at world positions. No assets needed
// (square particles with the Sprites/Default shader fit the blocky look).

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Shared particle emitter for blast effects.</summary>
    public sealed class BlastFxView : MonoBehaviour
    {
        private ParticleSystem particles;

        private void Awake()
        {
            particles = gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
            main.gravityModifier = 0.6f;
            main.maxParticles = 2000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            var particleRenderer = GetComponent<ParticleSystemRenderer>();
            particleRenderer.material = new Material(Shader.Find("Sprites/Default"));
            particleRenderer.sortingOrder = 14;
        }

        /// <summary>Bursts a few particles of the given color at a world position.</summary>
        public void EmitAt(Vector2 world, Color color, int count)
        {
            var emitParams = new ParticleSystem.EmitParams();
            emitParams.position = new Vector3(world.x, world.y, 0f);
            emitParams.startColor = color;
            particles.Emit(emitParams, count);
        }
    }
}
