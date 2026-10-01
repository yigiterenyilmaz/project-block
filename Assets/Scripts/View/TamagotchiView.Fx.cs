// PURPOSE: "Tamagotchi"'s few small particles - the flecks a bite throws off (in the colour of what
// was bitten), crumbs, one or two warm heart motes after a good meal, an eye sparkle, a snack
// thought bubble, the bite-ring left in the hand where a fed card was, the dark puff of the fury.
//
// ONE POOL, A BUDGET PER BEAT. The brief caps the pet at ~8 particles normally, ~16 through the fury
// transition and ~20 round a board bite; a beat sets the cap (ParticleBudget) and a spawn past it is
// simply not made. No particle system per idle, no full-screen effect, no confetti, no heart rain.
// LOD: Medium halves every count, Low makes no AMBIENT particles at all (hearts, thoughts, sparkles)
// but keeps the flecks of a bite, because those are part of the bite reading as a bite.
// They live in the flight layer (world space, above the hand), never on the moving body.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class TamagotchiView
    {
        private sealed class Particle
        {
            public SpriteRenderer R;
            public Vector2 Velocity;
            public float Gravity;
            public float Drag;
            public float Life;
            public float Age;
            public float Spin;
            public float Size0;
            public float Size1;
            public Color Colour;
            public bool Live;
            public bool FadeIn;
            public bool Pulled;
            public Vector2 Pull;
        }

        private const int ParticlePool = 24;
        private readonly List<Particle> particles = new List<Particle>();

        /// <summary>The cap the playing beat allows (8 normally, 16 fury, 20 board bite).</summary>
        private int particleBudget = 8;

        private void BuildParticles()
        {
            for (int i = 0; i < ParticlePool; i++)
            {
                var go = new GameObject("PetParticle" + i);
                go.transform.SetParent(flight, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sortingOrder = 20;
                r.enabled = false;
                particles.Add(new Particle { R = r });
            }
        }

        private int LiveParticles
        {
            get
            {
                int n = 0;
                foreach (Particle p in particles)
                {
                    if (p.Live)
                    {
                        n++;
                    }
                }
                return n;
            }
        }

        private void SetParticleBudget(int budget)
        {
            particleBudget = budget;
        }

        private bool Spawn(string sprite, Vector2 at, Vector2 velocity, float life, float size0, float size1,
            Color colour, float gravity, float spin, bool ambient, float drag = 1.5f, bool fadeIn = false)
        {
            return SpawnParticle(sprite, at, velocity, life, size0, size1, colour, gravity, spin, ambient, drag, fadeIn) != null;
        }

        /// <summary>A short tapered streak flying along <paramref name="dir"/> (the fury's own
        /// particle: no sparkles, no hearts). It points the way it goes and never spins.</summary>
        private void SpawnStreak(Vector2 at, Vector2 dir, float speed, float life, Color colour)
        {
            Particle p = SpawnParticle("fx_streak", at, dir * speed, life, 2.2f * S, 1.1f * S, colour, 0f, 0f, false, 6f, false);
            if (p != null)
            {
                p.R.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
        }

        /// <summary>A mote drawn toward a point (the board's dust before a bite): it starts slow
        /// and is gone when it gets there.</summary>
        private void SpawnPulled(string sprite, Vector2 from, Vector2 to, float life, float size, Color colour)
        {
            Vector2 d = to - from;
            Particle p = SpawnParticle(sprite, from, d / Mathf.Max(0.05f, life) * 0.35f, life, size, size * 0.5f, colour, 0f, 0f, false, 0f, true);
            if (p != null)
            {
                p.Pull = to;
                p.Pulled = true;
            }
        }

        private Particle SpawnParticle(string sprite, Vector2 at, Vector2 velocity, float life, float size0, float size1,
            Color colour, float gravity, float spin, bool ambient, float drag, bool fadeIn)
        {
            if (ambient && Lod == PetLod.Low)
            {
                return null;
            }
            if (LiveParticles >= particleBudget)
            {
                return null;
            }
            foreach (Particle p in particles)
            {
                if (p.Live)
                {
                    continue;
                }
                p.Live = true;
                p.R.enabled = true;
                p.R.sprite = TamagotchiArt.Get(sprite);
                p.R.transform.position = at;
                p.R.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f) * (spin != 0f ? 1f : 0f));
                p.Velocity = velocity;
                p.Gravity = gravity;
                p.Drag = drag;
                p.Life = life;
                p.Age = 0f;
                p.Spin = spin;
                p.Size0 = size0;
                p.Size1 = size1;
                p.Colour = colour;
                p.FadeIn = fadeIn;
                p.Pulled = false;
                Paint(p);
                return p;
            }
            return null;
        }

        private void TickParticles()
        {
            float dt = Dt;
            foreach (Particle p in particles)
            {
                if (!p.Live)
                {
                    continue;
                }
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Live = false;
                    p.R.enabled = false;
                    continue;
                }
                if (p.Pulled)
                {
                    // drawn in: faster the nearer the end of its life, so it arrives as it dies
                    Vector2 to = p.Pull - (Vector2)p.R.transform.position;
                    float left = Mathf.Max(0.02f, p.Life - p.Age);
                    p.Velocity = Vector2.Lerp(p.Velocity, to / left, Mathf.Clamp01(dt * 9f));
                }
                p.Velocity += Vector2.down * p.Gravity * dt;
                p.Velocity *= Mathf.Max(0f, 1f - p.Drag * dt);
                p.R.transform.position += (Vector3)(p.Velocity * dt);
                p.R.transform.Rotate(0f, 0f, p.Spin * dt);
                Paint(p);
            }
        }

        private static void Paint(Particle p)
        {
            float t = Mathf.Clamp01(p.Age / Mathf.Max(0.0001f, p.Life));
            float size = Mathf.Lerp(p.Size0, p.Size1, t);
            p.R.transform.localScale = new Vector3(size, size, 1f);
            Color c = p.Colour;
            float fade = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            if (p.FadeIn)
            {
                fade *= Mathf.Clamp01(t / 0.15f);
            }
            c.a *= fade;
            p.R.color = c;
        }

        private void ClearParticles()
        {
            foreach (Particle p in particles)
            {
                p.Live = false;
                p.R.enabled = false;
            }
        }

        private float LodCount(int count)
        {
            return Lod == PetLod.Medium ? Mathf.Max(1, count / 2) : count;
        }

        // ---- the named ones

        /// <summary>2-4 tiny flecks off a bite, in the colour of what was bitten.</summary>
        private void BiteFlecks(Vector2 at, Color colour, int count)
        {
            int n = (int)LodCount(count);
            for (int i = 0; i < n; i++)
            {
                float a = Random.Range(-0.4f, 0.4f) + (i % 2 == 0 ? 0.5f : -0.5f) + Mathf.PI * 0.5f;
                Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(0.7f, 1.3f) * S;
                Spawn("fx_crumb", at + Random.insideUnitCircle * 0.04f * S, v, Random.Range(0.32f, 0.5f),
                    0.45f * S, 0.3f * S, colour, 3.2f * S, Random.Range(-300f, 300f), false, 2.2f);
            }
        }

        /// <summary>One or two warm heart motes rising (never a rain of them).</summary>
        private void HeartMotes(Vector2 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 v = new Vector2((i == 0 ? -1f : 1f) * 0.18f * S, 0.55f * S);
                Spawn("fx_heart", at + new Vector2((i == 0 ? -0.12f : 0.14f) * S, 0f), v, 0.9f,
                    0.55f * S, 0.75f * S, new Color(1f, 0.72f, 0.80f, 0.95f), -0.1f * S, 0f, true, 0.8f, true);
            }
        }

        /// <summary>Small warm motes (the 2/2 satisfaction).</summary>
        private void WarmMotes(Vector2 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 v = new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.4f, 0.6f)) * S;
                Spawn("fx_soft", at + Random.insideUnitCircle * 0.2f * S, v, 1f, 0.12f * S, 0.06f * S,
                    new Color(1f, 0.82f, 0.74f, 0.7f), -0.05f * S, 0f, true, 0.6f, true);
            }
        }

        /// <summary>The snack-dream thought: a tiny card silhouette floating up for a second.</summary>
        private void SpawnThought(Vector2 at, float seconds)
        {
            Spawn("fx_snack", at, new Vector2(0.05f, 0.18f) * S, seconds, 0.45f * S, 0.65f * S,
                new Color(1f, 1f, 1f, 0.85f), 0f, 0f, true, 0.4f, true);
        }

        /// <summary>The dark raspberry puff of the fury burst.</summary>
        private void FuryPuff(Vector2 at, int count)
        {
            int n = (int)LodCount(count);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + 0.3f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f + 0.2f);
                Spawn("fx_puff", at + dir * 0.25f * S, dir * 1.4f * S, 0.42f, 0.7f * S, 1.3f * S,
                    new Color(0.55f, 0.13f, 0.30f, 0.55f), 0f, Random.Range(-40f, 40f), false, 4f);
            }
        }

        /// <summary>The ring a fed card leaves in its empty hand slot: a small pink bite-ring and
        /// a few crumbs, gone in ~0.4 s. The slot itself stays empty - that is the cost.</summary>
        public void SpawnHandResidue(Vector2 at)
        {
            Spawn("fx_bitering", at, Vector2.zero, 0.42f, 0.9f * S, 1.05f * S,
                new Color(1f, 0.62f, 0.72f, 0.75f), 0f, 0f, false, 0f);
            for (int i = 0; i < 3; i++)
            {
                Vector2 v = Random.insideUnitCircle * 0.3f * S + Vector2.up * 0.2f * S;
                Spawn("fx_crumb", at + Random.insideUnitCircle * 0.15f * S, v, 0.4f, 0.35f * S, 0.25f * S,
                    new Color(0.95f, 0.75f, 0.78f, 0.9f), 1.2f * S, 120f, false);
            }
        }
    }
}
