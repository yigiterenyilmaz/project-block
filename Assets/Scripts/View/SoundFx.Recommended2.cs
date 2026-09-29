// PURPOSE: The SECOND ROUND of variants (D and E) for the recommended cues, written after the first
// listen settled a pick for every one of them (SoundFx.RecDefault). None of these has been heard
// before. Where a VARIANT won, D and E are refinements in its direction (a new shear, a new slit
// drum, a new pass-by...); where the ORIGINAL won, they stay close to the original's own character
// and bring it into the MIX palette rather than replacing it - it was kept for a reason.
// Same rules as the first round: no inharmonic "ting" (the gong is the one original that is struck
// metal and was KEPT, so its D/E keep the metal and only tame it), no resonance on anything meant
// to be dry, no low upward-gliding pops, no big room on a low sound, and the stings keep their
// originals' lengths.

using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class SoundFx
    {
        /// <summary>Variant D (k = 0) or E (k = 1) of a recommended cue.</summary>
        private static AudioClip RecSecondRound(RecCue cue, int k, System.Random rng, string name)
        {
            switch (cue)
            {
                case RecCue.Cut: return RecCut2(k, rng, name);
                case RecCue.Drum: return RecDrum2(k, rng, name);
                case RecCue.Rumble: return RecRumble2(k, rng, name);
                case RecCue.Pluck: return RecLeaf2(k, rng, name);
                case RecCue.Whoosh: return RecWhoosh2(k, rng, name);
                case RecCue.Squish: return RecSquish2(k, rng, name);
                case RecCue.Stretch: return RecStretch2(k, rng, name);
                case RecCue.StingSiren: return RecSiren2(k, rng, name);
                case RecCue.StingBoom: return RecBoom2(k, rng, name);
                case RecCue.StingGong: return RecGong2(k, rng, name);
                case RecCue.StingClank: return RecClank2(k, rng, name);
                case RecCue.StingDrone: return RecDrone2(k, rng, name);
                default: return RecMagma2(k, rng, name);
            }
        }

        /// <summary>Cut (B, the shear, won). D - a tighter, brighter shear ending in a tiny
        /// tick. E - two quick shears, the blade going through and back.</summary>
        private static AudioClip RecCut2(int k, System.Random rng, string name)
        {
            float[] b = Buffer(0.34f);
            if (k == 0)
            {
                PNoise(b, 0f, 0.005f, 0.45f, 0.0001f, 0.001f, 3500f, 13000f, rng);
                PNoise(b, 0.012f, 0.005f, 0.4f, 0.0001f, 0.001f, 4000f, 13000f, rng);
                Zip(b, 0.006f, 0.08f, 7000f, 2000f, 14000f, 4500f, 0.08f, 0.75f, rng);
                PNoise(b, 0.09f, 0.004f, 0.35f, 0.0001f, 0.0009f, 3000f, 12000f, rng);
                PSine(b, 0.09f, 0.03f, 2000f, 1400f, 0.004f, 0.15f, 0.0002f, 0.006f);
            }
            else
            {
                for (int s = 0; s < 2; s++)
                {
                    float at = 0.11f * s;
                    PNoise(b, at, 0.005f, 0.4f, 0.0001f, 0.0012f, 3000f, 12000f, rng);
                    Zip(b, at + 0.005f, 0.08f, s == 0 ? 6000f : 1500f, s == 0 ? 1500f : 6000f,
                        s == 0 ? 13000f : 3500f, s == 0 ? 3500f : 13000f, 0.1f, 0.6f, rng);
                }
                Knock(b, 0.2f, 700f, 0.5f, rng);
            }
            HighPass(b, 150f);
            return PFinish(name, b, 0.85f);
        }

        /// <summary>Drum (B, the slit drum, won). D - a tongue drum: three wooden tones (root,
        /// fifth, octave) rolled a hair apart. E - the slit drum with a soft thump under it and a
        /// sharper stick attack.</summary>
        private static AudioClip RecDrum2(int k, System.Random rng, string name)
        {
            float[] b = Buffer(0.55f);
            if (k == 0)
            {
                float[] tones = { 196f, 293.66f, 392f };
                for (int i = 0; i < tones.Length; i++)
                {
                    float at = 0.018f * i;
                    PSine(b, at, 0.3f, tones[i] * 1.03f, tones[i], 0.008f, 0.42f - 0.08f * i, 0.001f, 0.09f - 0.015f * i);
                    PNoise(b, at, 0.012f, 0.18f, 0.0003f, 0.003f, 700f, 3500f, rng);
                }
            }
            else
            {
                PNoise(b, 0f, 0.006f, 0.4f, 0.0001f, 0.0012f, 2500f, 10000f, rng);
                PSine(b, 0f, 0.3f, 205f, 180f, 0.01f, 0.5f, 0.001f, 0.08f);
                PSine(b, 0f, 0.25f, 305f, 270f, 0.01f, 0.28f, 0.001f, 0.06f);
                Thump(b, 0f, 0.45f, 120f, 60f, 0.07f);
            }
            Saturate(b, 1.3f);
            HighPass(b, 45f);
            return PFinish(name, b, 0.88f);
        }

        /// <summary>Rumble (the ORIGINAL won - a low tone falling under a wash). D - the same low
        /// falling tones, with a dry crumble of grains on top instead of the hiss. E - the same
        /// shape with the wash replaced by a darker, flickering rush.</summary>
        private static AudioClip RecRumble2(int k, System.Random rng, string name)
        {
            float[] b = Buffer(0.8f);
            PSine(b, 0f, 0.75f, 55f, 40f, 0.3f, 0.5f, 0.02f, 0.3f);
            PSine(b, 0f, 0.6f, 82f, 60f, 0.3f, 0.25f, 0.02f, 0.25f);
            if (k == 0)
            {
                PNoise(b, 0f, 0.7f, 0.12f, 0.03f, 0.3f, 120f, 900f, rng);
                for (int g = 0; g < 34; g++)
                {
                    float at = 0.03f + 0.6f * (float)System.Math.Pow(rng.NextDouble(), 1.3);
                    PNoise(b, at, 0.007f, 0.12f + 0.12f * (float)rng.NextDouble(), 0.0002f, 0.0018f, 1200f, 4500f, rng);
                }
            }
            else
            {
                Rush(b, 0f, 0.75f, 0.06f, 0.28f, 0.7f, 1000f, rng);
            }
            Saturate(b, 1.2f);
            HighPass(b, 35f);
            return PFinish(name, b, 0.85f);
        }

        /// <summary>Leaf pluck (the ORIGINAL won - a snap and a tone bending UP). D - the same
        /// snap and rising tone, rounder and softer. E - the snap and two quick blips climbing.
        /// Up high, so the rising pitch reads as a pluck, never a bloop.</summary>
        private static AudioClip RecLeaf2(int k, System.Random rng, string name)
        {
            float[] b = Buffer(0.2f);
            PNoise(b, 0f, 0.02f, 0.3f, 0.0001f, 0.003f, 2500f, 11000f, rng);
            if (k == 0)
            {
                PSine(b, 0.001f, 0.14f, 950f, 1500f, 0.03f, 0.3f, 0.002f, 0.035f);
                PSine(b, 0.001f, 0.1f, 1900f, 3000f, 0.03f, 0.06f, 0.002f, 0.02f);
            }
            else
            {
                PSine(b, 0.001f, 0.06f, 1100f, 1300f, 0.01f, 0.25f, 0.001f, 0.015f);
                PSine(b, 0.035f, 0.08f, 1400f, 1750f, 0.01f, 0.25f, 0.001f, 0.022f);
            }
            HighPass(b, 250f);
            return PFinish(name, b, 0.8f);
        }

        /// <summary>Whoosh (A, the pass-by, won). D - a longer, darker pass-by, something heavier.
        /// E - a pass-by with a fainter second pass behind it, a swing and its return.</summary>
        private static AudioClip RecWhoosh2(int k, System.Random rng, string name)
        {
            float[] b = Buffer(0.7f);
            if (k == 0)
            {
                float[] tmp = Buffer(0.62f);
                PassBy(tmp, 0.62f, rng);
                LowPass(tmp, 2200f);
                for (int i = 0; i < tmp.Length; i++) { b[i] += tmp[i]; }
            }
            else
            {
                PassBy(b, 0.36f, rng);
                float[] tmp = Buffer(0.3f);
                PassBy(tmp, 0.3f, rng);
                int off = (int)(0.3f * SampleRate);
                for (int i = 0; i < tmp.Length && off + i < b.Length; i++) { b[off + i] += tmp[i] * 0.45f; }
            }
            HighPass(b, 150f);
            return PFinish(name, b, 0.75f);
        }

        /// <summary>Squish (A, the squeeze, won). D - a harder squeeze: more crunch and a
        /// compression chirp falling into the thump. E - the squeeze ending on a small falling
        /// chip tick.</summary>
        private static AudioClip RecSquish2(int k, System.Random rng, string name)
        {
            float[] b = Buffer(0.45f);
            PNoiseSweep(b, 0f, 0.3f, 0.35f, 0.02f, 0.08f, 200f, 5000f, 500f, rng);
            PSine(b, 0f, 0.3f, 420f, 150f, 0.08f, 0.3f, 0.01f, 0.08f);
            if (k == 0)
            {
                PSine(b, 0.1f, 0.06f, 1800f, 600f, 0.02f, 0.18f, 0.001f, 0.02f);
                Crunch(b, 0.14f, 0.9f, 1, 0.9f, 0.06f, rng);
                Thump(b, 0.15f, 0.55f, 140f, 55f, 0.08f);
            }
            else
            {
                Crunch(b, 0.15f, 0.5f, 0, 0.9f, 0.05f, rng);
                Thump(b, 0.15f, 0.45f, 140f, 60f, 0.07f);
                Square(b, 0.16f, 523.25f, 0.12f, 0.025f);
                Square(b, 0.2f, 392f, 0.12f, 0.04f);
            }
            Saturate(b, 1.3f);
            HighPass(b, 50f);
            return PFinish(name, b, 0.85f);
        }

        /// <summary>Stretch (B, the elastic rise, won). D - the elastic rise with plucks marking
        /// its steps. E - a stronger wobble with a zip opening under it.</summary>
        private static AudioClip RecStretch2(int k, System.Random rng, string name)
        {
            float[] b = Buffer(0.6f);
            int n = b.Length;
            double phase = 0;
            float wobble = k == 0 ? 0.04f : 0.09f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float rise = Mathf.Clamp01(t / 0.4f);
                float f = Mathf.Lerp(160f, 420f, Mathf.SmoothStep(0f, 1f, rise))
                    * (1f + wobble * Mathf.Sin(2f * Mathf.PI * 12f * t) * (1f - 0.5f * rise));
                phase += 2.0 * Mathf.PI * f / SampleRate;
                float env = Mathf.Min(1f, t / 0.05f) * Mathf.Exp(-t / 0.3f) * Mathf.Clamp01((n - i) / (0.08f * SampleRate));
                b[i] += Mathf.Sin((float)phase) * 0.4f * env;
            }
            if (k == 0)
            {
                float[] steps = { 392f, 523.25f, 659.25f };
                for (int s = 0; s < steps.Length; s++)
                {
                    Pluck(b, 0.06f + 0.12f * s, steps[s], 0.2f, 0.99f, 0.45f, rng);
                }
            }
            else
            {
                Zip(b, 0f, 0.4f, 300f, 2000f, 900f, 7000f, 0.7f, 0.35f, rng);
            }
            Saturate(b, 1.2f);
            HighPass(b, 90f);
            return PFinish(name, b, 0.8f);
        }

        // ---- the stings (originals won everywhere but the drone) ----

        /// <summary>Siren (the ORIGINAL won - two alternating square-edged tones). D - the same
        /// two tones with a pluck's attack and a softer edge. E - each tone sliding down a little,
        /// over a band of pulsing air.</summary>
        private static AudioClip RecSiren2(int k, System.Random rng, string name)
        {
            float[] b = Buffer(1.0f);
            for (int s = 0; s < 4; s++)
            {
                float f = s % 2 == 0 ? 740f : 520f;
                float at = 0.22f * s;
                if (k == 0)
                {
                    PSine(b, at, 0.24f, f, f * 0.98f, 1f, 0.24f, 0.004f, 0.12f);
                    PSine(b, at, 0.2f, f * 3f, f * 2.94f, 1f, 0.03f, 0.004f, 0.06f);
                    PNoise(b, at, 0.008f, 0.12f, 0.0002f, 0.002f, 2000f, 8000f, rng);
                }
                else
                {
                    PSine(b, at, 0.24f, f * 1.04f, f * 0.94f, 0.1f, 0.22f, 0.005f, 0.14f);
                    PSine(b, at, 0.24f, f * 3.1f, f * 2.8f, 0.1f, 0.04f, 0.005f, 0.1f);
                    Rush(b, at, 0.2f, 0.02f, 0.07f, 0.35f, 2000f, rng);
                }
            }
            HighPass(b, 100f);
            return PFinish(name, b, 0.85f);
        }

        /// <summary>Boom (the ORIGINAL won - a deep falling tone under a wash). D - the same fall
        /// with the white wash replaced by the dry mid rush. E - the same fall with a crunch on
        /// its front edge.</summary>
        private static AudioClip RecBoom2(int k, System.Random rng, string name)
        {
            float[] b = Buffer(1.2f);
            PSine(b, 0f, 1.1f, 90f, 32f, 0.25f, 0.75f, 0.002f, 0.3f);
            if (k == 0)
            {
                Rush(b, 0f, 0.6f, 0.01f, 0.15f, 0.8f, 2200f, rng);
            }
            else
            {
                PNoise(b, 0f, 0.4f, 0.3f, 0.002f, 0.09f, 150f, 3500f, rng);
                Crunch(b, 0f, 1f, 1, 1f, 0.07f, rng);
            }
            Saturate(b, 1.4f);
            HighPass(b, 28f);
            return PFinish(name, b, 0.9f);
        }

        /// <summary>Gong (the ORIGINAL won, struck metal and all). D - the same partials with the
        /// top two taken down and a shorter decay: the metal, tamed. E - the original's partials
        /// over a thump and a slow swell of air, heavier and less ringing.</summary>
        private static AudioClip RecGong2(int k, System.Random rng, string name)
        {
            float[] b = Buffer(2.2f);
            float[] partials = { 110f, 173f, 262f, 331f, 447f };
            float[] amps = k == 0
                ? new[] { 0.28f, 0.19f, 0.12f, 0.05f, 0.025f }
                : new[] { 0.26f, 0.16f, 0.1f, 0.06f, 0.035f };
            for (int i = 0; i < partials.Length; i++)
            {
                float tau = (k == 0 ? 0.45f : 0.6f) / (1f + 0.35f * i);
                PSine(b, 0f, 2.1f, partials[i], partials[i] * 0.995f, 1f, amps[i], 0.002f, tau);
            }
            if (k == 0)
            {
                PSine(b, 0f, 0.3f, 70f, 45f, 0.05f, 0.4f, 0.002f, 0.08f);
                PNoise(b, 0f, 0.03f, 0.2f, 0.0003f, 0.006f, 800f, 5000f, rng);
            }
            else
            {
                Thump(b, 0f, 0.6f, 110f, 50f, 0.16f);
                PNoiseSweep(b, 0f, 1.6f, 0.12f, 0.3f, 0.5f, 150f, 500f, 1500f, rng);
            }
            HighPass(b, 40f);
            return PFinish(name, b, 0.85f);
        }

        /// <summary>Clank (the ORIGINAL won - a burst over three metal modes and a low knock). D -
        /// the same, darker: the modes an octave-ish lower and shorter. E - the original clank
        /// twice, the second one smaller, like a bolt falling and settling.</summary>
        private static AudioClip RecClank2(int k, System.Random rng, string name)
        {
            float[] b = Buffer(0.35f);
            int hits = k == 0 ? 1 : 2;
            for (int h = 0; h < hits; h++)
            {
                float at = 0.12f * h;
                float amp = h == 0 ? 1f : 0.55f;
                float scale = k == 0 ? 0.55f : 1f;
                PNoise(b, at, 0.06f, 0.4f * amp, 0.0002f, 0.012f, 1500f, 9000f, rng);
                PSine(b, at, 0.25f, 820f * scale, 800f * scale, 1f, 0.15f * amp, 0.0005f, k == 0 ? 0.04f : 0.06f);
                PSine(b, at, 0.25f, 1330f * scale, 1300f * scale, 1f, 0.11f * amp, 0.0005f, k == 0 ? 0.035f : 0.05f);
                PSine(b, at, 0.2f, 2150f * scale, 2100f * scale, 1f, 0.07f * amp, 0.0005f, k == 0 ? 0.03f : 0.04f);
                PSine(b, at, 0.12f, 140f, 90f, 0.03f, 0.4f * amp, 0.001f, 0.04f);
            }
            Saturate(b, 1.2f);
            HighPass(b, 60f);
            return PFinish(name, b, 0.88f);
        }

        /// <summary>Drone (A, the square pad, won). D - the pad with a slow filter swell opening
        /// and closing it. E - the pad with a low plucked pulse under it, like a slow heartbeat.
        /// </summary>
        private static AudioClip RecDrone2(int k, System.Random rng, string name)
        {
            const float len = 2.6f;
            float[] b = Buffer(len);
            int n = b.Length;
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * t / len), 0.8f);
                float s = 0f;
                foreach (float f in new[] { 55f, 57.5f, 110f })
                {
                    for (int h = 1; h <= 7; h += 2)
                    {
                        s += Mathf.Sin(2f * Mathf.PI * f * h * t) / h;
                    }
                }
                s *= 0.12f * env;
                if (k == 0)
                {
                    float a = OnePole(Mathf.Lerp(180f, 1400f, env * env));
                    lp1 += (s - lp1) * a;
                    lp2 += (lp1 - lp2) * a;
                    b[i] += lp2 * 1.4f;
                }
                else
                {
                    b[i] += s * 0.8f;
                }
            }
            if (k == 1)
            {
                for (int p = 0; p < 4; p++)
                {
                    float at = 0.35f + 0.55f * p;
                    Pluck(b, at, 55f, 0.4f, 0.995f, 0.6f, rng);
                    Thump(b, at, 0.25f, 90f, 50f, 0.08f);
                }
            }
            HighPass(b, 35f);
            return PFinish(name, b, 0.8f);
        }

        /// <summary>Magma (the ORIGINAL won - a dark roar swelling to an eruption, with bubbles).
        /// D - the same roar with the bubbles turned DOWNWARD (a gulp that falls, not a pop that
        /// rises) and kept up high. E - the roar with crackle through it and a thump at the
        /// eruption.</summary>
        private static AudioClip RecMagma2(int k, System.Random rng, string name)
        {
            const float seconds = 2.4f;
            float[] b = Buffer(seconds);
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)b.Length;
                float s = i / (float)SampleRate;
                float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.8f)) * Mathf.Pow(1f - Mathf.Clamp01((t - 0.8f) / 0.2f), 1.5f);
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += (n - lp1) * 0.025f;
                lp2 += (lp1 - lp2) * 0.04f;
                float churn = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 1.3f * s) * Mathf.Sin(2f * Mathf.PI * 0.7f * s + 1f);
                b[i] = env * (lp2 * 4f * churn + 0.16f * Mathf.Sin(2f * Mathf.PI * (41f + 4f * Mathf.Sin(s * 2.2f)) * s));
            }
            if (k == 0)
            {
                for (int g = 0; g < 16; g++)
                {
                    float at = (float)(0.15 + rng.NextDouble() * (seconds * 0.78 - 0.15));
                    float f0 = 420f + 200f * (float)rng.NextDouble();
                    PSine(b, at, 0.08f, f0, f0 * 0.6f, 0.02f, 0.12f * Mathf.Clamp01(at / 1.2f) + 0.04f, 0.003f, 0.025f);
                }
            }
            else
            {
                for (int c = 0; c < 40; c++)
                {
                    float at = seconds * 0.78f * (float)System.Math.Pow(rng.NextDouble(), 0.7);
                    Crackle(b, at, 0.15f + 0.35f * (at / seconds), rng);
                }
                Thump(b, seconds * 0.78f, 0.7f, 120f, 45f, 0.2f);
            }
            HighPass(b, 30f);
            return PFinish(name, b, 0.88f);
        }

        /// <summary>A two-pole low-pass over a whole buffer.</summary>
        private static void LowPass(float[] b, float hz)
        {
            float a = OnePole(hz);
            float s1 = 0f, s2 = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                s1 += (b[i] - s1) * a;
                s2 += (s1 - s2) * a;
                b[i] = s2;
            }
        }
    }
}
