// PURPOSE: BossIdentityView's REACTIONS - the boss stage's background answering what happens on the
// board. A cleared line, an explosion or a clean sweep is sent in as an IMPULSE (ReactLine /
// ReactBurst / ReactSweep, called from the controller's feedback seams), and every look reads the
// live impulses back while it poses:
//
//   ENERGY  - one scalar: how hard the stage has just been hit (fast attack, quick decay). For
//             things that react as a whole: a corona swelling, letterbox bars clenching, a cage
//             rattling, stripes marching faster.
//   WAVE    - a pressure front leaving the impulse's source (a point for a burst, a SEGMENT for a
//             line, so a line clear sends a band outward rather than a circle) at WaveSpeed. For
//             things that react where they STAND: a rune flaring as the front passes it, a planet
//             nudged outward, a lava blob brightening, a star glinting. WavePush is its direction.
//
// Under every look the shared layer draws the front itself (a soft ring - stretched into a
// rounded band for a line) in the look's own colour, flares the red pool behind the arena and
// throws a few motes off the source. All of it sits BEHIND the board plate, so nothing ever covers
// a cell, and nothing here decides anything: it is told a world position and a strength.
//
// Impulses only land while the view is visible and something is dressed; a hidden view (a menu,
// the animation lab) ignores them so they cannot pile up and all go off at once later.
// EXTENSION POINT: a new look reacts by reading ReactEnergy / Wave / WavePush in its pose.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class BossIdentityView
    {
        public static class ReactStyle
        {
            /// <summary>World units per second the pressure front travels.</summary>
            public static float WaveSpeed = 7.5f;

            /// <summary>Thickness of the front (world units).</summary>
            public static float WaveWidth = 0.85f;

            /// <summary>How fast a front dies as it travels.</summary>
            public static float WaveFade = 1.7f;

            /// <summary>How fast the whole-stage energy falls away.</summary>
            public static float EnergyDecay = 2.8f;

            public static float LineStrength = 0.6f;
            public static float LineTierStep = 0.12f;
            public static float BurstStrength = 0.45f;
            public static float DynamiteStrength = 1.3f;
            public static float SweepStrength = 1.6f;

            /// <summary>Peak alpha of the drawn front.</summary>
            public static float RingAlpha = 0.32f;

            /// <summary>Extra alpha on the red pool behind the arena at full energy.</summary>
            public static float PoolFlare = 0.30f;
        }

        private enum ImpulseKind
        {
            Line,
            Burst,
            Sweep
        }

        private sealed class Impulse
        {
            public ImpulseKind Kind;
            public Vector2 A;
            public Vector2 B;
            public float Strength;
            public float Age;
            public int Seed;
        }

        private const int ImpulseCap = 8;
        private const int MotesPerImpulse = 10;
        private const int ReactRingOrder = -39;
        private const int ReactMoteOrder = -38;

        private readonly List<Impulse> impulses = new List<Impulse>();
        private SpriteRenderer[] reactRings;
        private SpriteRenderer[] reactMotes;
        private int impulseSeed;

        /// <summary>0..~2: how hard the stage has just been hit.</summary>
        private float reactEnergy;

        /// <summary>Energy integrated over time - for things that should RUN faster for a moment
        /// (the tape's march, the seal's spin) without jumping when the energy drops.</summary>
        private float reactDrive;

        // =================================================================== the API

        /// <summary>A line was cleared between two world points (its end cells).</summary>
        public void ReactLine(Vector2 from, Vector2 to, int tier)
        {
            AddImpulse(ImpulseKind.Line, from, to,
                ReactStyle.LineStrength + ReactStyle.LineTierStep * Mathf.Clamp(tier, 0, 5));
        }

        /// <summary>A loose group of <paramref name="cells"/> cubes went off around a world point.</summary>
        public void ReactBurst(Vector2 at, int cells)
        {
            AddImpulse(ImpulseKind.Burst, at, at,
                ReactStyle.BurstStrength * (0.8f + 0.1f * Mathf.Min(cells, 10)));
        }

        public void ReactDynamite(Vector2 at)
        {
            AddImpulse(ImpulseKind.Burst, at, at, ReactStyle.DynamiteStrength);
        }

        /// <summary>The board was swept clean: the biggest answer, from the arena's centre.</summary>
        public void ReactSweep()
        {
            AddImpulse(ImpulseKind.Sweep, rect.center, rect.center, ReactStyle.SweepStrength);
        }

        private void AddImpulse(ImpulseKind kind, Vector2 a, Vector2 b, float strength)
        {
            if (!visible || reactRings == null || (builtAmbience == BossAmbience.None && atmosphereWeight <= 0f))
            {
                return;
            }
            if (impulses.Count >= ImpulseCap)
            {
                impulses.RemoveAt(0);
            }
            impulses.Add(new Impulse { Kind = kind, A = a, B = b, Strength = strength, Seed = impulseSeed++ });
        }

        // =================================================================== the clock

        private void BuildReactions()
        {
            reactRings = Many(atmoWorld, "ReactRing", SoftRing, ReactRingOrder, ImpulseCap);
            reactMotes = Many(atmoWorld, "ReactMote", SoftDot, ReactMoteOrder, ImpulseCap * MotesPerImpulse);
        }

        private static float ImpulseLife(ImpulseKind kind)
        {
            return kind == ImpulseKind.Sweep ? 2.4f : 1.6f;
        }

        /// <summary>Ages the impulses and settles this frame's energy. Runs BEFORE the looks pose.</summary>
        private void TickReactions(float dt)
        {
            float energy = 0f;
            for (int i = impulses.Count - 1; i >= 0; i--)
            {
                Impulse im = impulses[i];
                im.Age += dt;
                if (im.Age >= ImpulseLife(im.Kind))
                {
                    impulses.RemoveAt(i);
                    continue;
                }
                float decay = im.Kind == ImpulseKind.Sweep ? ReactStyle.EnergyDecay * 0.6f : ReactStyle.EnergyDecay;
                energy += im.Strength * Seg(im.Age, 0f, 0.05f) * Mathf.Exp(-im.Age * decay);
            }
            reactEnergy = Mathf.Min(2f, energy);
            reactDrive += dt * reactEnergy;
        }

        /// <summary>The closest point of an impulse's source to <paramref name="p"/>.</summary>
        private static Vector2 SourcePoint(Impulse im, Vector2 p)
        {
            Vector2 ab = im.B - im.A;
            float len2 = ab.sqrMagnitude;
            if (len2 < 0.0001f)
            {
                return im.A;
            }
            float k = Mathf.Clamp01(Vector2.Dot(p - im.A, ab) / len2);
            return im.A + ab * k;
        }

        private static float FrontAt(Impulse im, float distance)
        {
            float front = im.Age * ReactStyle.WaveSpeed * (im.Kind == ImpulseKind.Sweep ? 0.8f : 1f);
            float band = Mathf.Exp(-Sq((distance - front) / ReactStyle.WaveWidth));
            return im.Strength * band * Mathf.Exp(-im.Age * ReactStyle.WaveFade) * Seg(im.Age, 0f, 0.04f);
        }

        /// <summary>How strongly a pressure front is passing world point <paramref name="p"/> right now.</summary>
        private float Wave(Vector2 p)
        {
            float sum = 0f;
            for (int i = 0; i < impulses.Count; i++)
            {
                Impulse im = impulses[i];
                sum += FrontAt(im, Vector2.Distance(p, SourcePoint(im, p)));
            }
            return Mathf.Min(2f, sum);
        }

        /// <summary>Wave, as a push directed away from each source.</summary>
        private Vector2 WavePush(Vector2 p)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < impulses.Count; i++)
            {
                Impulse im = impulses[i];
                Vector2 d = p - SourcePoint(im, p);
                float dist = d.magnitude;
                if (dist < 0.0001f)
                {
                    continue;
                }
                sum += d / dist * FrontAt(im, dist);
            }
            return sum;
        }

        /// <summary>The colour the shared front is drawn in - each look's own.</summary>
        private Color ReactTint
        {
            get
            {
                switch (builtAmbience)
                {
                    case BossAmbience.BloodEclipse: return Style.Corona;
                    case BossAmbience.RuneCircle: return Style.Arcane;
                    case BossAmbience.IronCage: return Color.Lerp(Style.IronLight, Style.Threat, 0.45f);
                    case BossAmbience.Orbital: return new Color(0.72f, 0.80f, 1f);
                    case BossAmbience.LavaLake: return LavaHot;
                }
                return Style.Threat;
            }
        }

        // =================================================================== the shared layer

        private void PoseReactions()
        {
            float weight = Mathf.Max(Smooth(atmosphereWeight), Mathf.Clamp01(ambienceWeight));
            Color tint = ReactTint;
            for (int j = 0; j < ImpulseCap; j++)
            {
                Impulse im = j < impulses.Count ? impulses[j] : null;
                SpriteRenderer ring = reactRings[j];
                if (im == null || weight <= 0f)
                {
                    ring.color = Color.clear;
                    for (int k = 0; k < MotesPerImpulse; k++)
                    {
                        reactMotes[j * MotesPerImpulse + k].color = Color.clear;
                    }
                    continue;
                }
                float front = im.Age * ReactStyle.WaveSpeed * (im.Kind == ImpulseKind.Sweep ? 0.8f : 1f);
                float fade = Mathf.Exp(-im.Age * ReactStyle.WaveFade) * Seg(im.Age, 0f, 0.04f);
                Vector2 ab = im.B - im.A;
                float half = ab.magnitude * 0.5f;
                float angle = half > 0.001f ? Mathf.Atan2(ab.y, ab.x) * Mathf.Rad2Deg : 0f;
                // SoftRing's ring sits at 0.40 of its width, so a width of R / 0.40 puts it at R.
                var size = new Vector2((half + front) / 0.40f, front / 0.40f);
                // The drawn ripple belongs to the LAVA only - a molten surface is the one ground
                // here that ripples. Every other look answers through its own pieces.
                if (builtAmbience == BossAmbience.LavaLake)
                {
                    Put(ring, (im.A + im.B) * 0.5f, size, angle, tint,
                        ReactStyle.RingAlpha * Mathf.Min(1.5f, im.Strength) * fade * weight);
                }
                else
                {
                    ring.color = Color.clear;
                }

                float moteLife = im.Kind == ImpulseKind.Sweep ? 1.5f : 1.1f;
                float life = Mathf.Clamp01(im.Age / moteLife);
                for (int k = 0; k < MotesPerImpulse; k++)
                {
                    SpriteRenderer mote = reactMotes[j * MotesPerImpulse + k];
                    if (life >= 1f)
                    {
                        mote.color = Color.clear;
                        continue;
                    }
                    float along = Hash(im.Seed * 17 + k, 150);
                    Vector2 origin = Vector2.Lerp(im.A, im.B, along);
                    float theta = Hash(im.Seed * 17 + k, 151) * Mathf.PI * 2f;
                    var dir = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
                    if (half > 0.001f)
                    {
                        // A line throws its motes off to either SIDE of itself, not along it.
                        Vector2 normal = new Vector2(-ab.y, ab.x).normalized;
                        dir = (normal * (k % 2 == 0 ? 1f : -1f) + dir * 0.45f).normalized;
                    }
                    float reach = (1.4f + 2.6f * Hash(im.Seed * 17 + k, 152)) * Mathf.Min(1.6f, im.Strength);
                    Vector2 at = origin + dir * (half > 0.001f ? 0.4f : rect.width * 0.35f)
                        + dir * OutCubic(life) * reach;
                    Color c = Color.Lerp(Color.Lerp(tint, Style.Bone, 0.35f), tint, life);
                    Put(mote, at, Vector2.one * Mathf.Lerp(0.14f, 0.05f, life), 0f, c,
                        (1f - life) * Seg(life, 0f, 0.05f) * 0.85f * weight);
                }
            }
        }
    }
}
