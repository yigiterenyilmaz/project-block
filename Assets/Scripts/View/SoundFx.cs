// PURPOSE: Basic procedural sound effects - every clip is synthesized at startup, so
// no audio assets are needed. Placeholder audio: swap for real clips later by
// assigning AudioClips instead of the generated ones. Requires an AudioListener in
// the scene (the Main Camera has one).

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Synthesizes and plays the game's placeholder sound effects.</summary>
    public sealed class SoundFx : MonoBehaviour
    {
        private const int SampleRate = 44100;

        /// <summary>Loudest the game ever plays, at a master volume of 1. Kept well under 1 so
        /// several overlapping effects cannot clip.</summary>
        private const float FullVolume = 0.5f;

        /// <summary>Player-set master volume, 0..1 (settings menu, persisted). Applied live to
        /// every effect and to the retro hum, so changing it is audible immediately.</summary>
        public float MasterVolume
        {
            get { return masterVolume; }
            set
            {
                masterVolume = Mathf.Clamp01(value);
                ApplyHumVolume();
            }
        }

        private float masterVolume = 1f;

        /// <summary>Hum loudness at full master volume. The hum sits under the effects.</summary>
        private const float HumVolume = 0.18f;

        private void ApplyHumVolume()
        {
            if (humSource != null)
            {
                humSource.volume = HumVolume * masterVolume;
            }
        }

        private AudioSource source;
        private AudioSource humSource;
        private AudioClip placeClip;
        private AudioClip explodeClip;
        private AudioClip sweepClip;
        private AudioClip shuffleClip;
        private AudioClip buyClip;
        private AudioClip flameClip;
        private AudioClip humClip;

        // ---- retro ("CRT") audio: a looping mains hum, toggled by SetRetro. The bit-crush that
        // grits this hum (and every other sound) lives on the AudioListener, see BitCrushFilter.

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            humSource = gameObject.AddComponent<AudioSource>();
            humSource.playOnAwake = false;
            humSource.loop = true;
            placeClip = BuildPlace();
            explodeClip = BuildExplode();
            sweepClip = BuildSweep();
            shuffleClip = BuildShuffle();
            buyClip = BuildBuy();
            flameClip = BuildFlame();
            humClip = BuildHum();
            stingClips = new[]
            {
                BuildStingSiren(), BuildStingBoom(), BuildStingGong(), BuildStingClank(),
                BuildStingDrone(), BuildStingMagma()
            };
            humSource.clip = humClip;
            ApplyHumVolume();
        }

        /// <summary>Turns the retro CRT hum loop on/off. The accompanying bit-crush is a separate
        /// filter on the AudioListener (BitCrushFilter). Called wherever the CRT is toggled.</summary>
        public void SetRetro(bool on)
        {
            if (humSource == null)
            {
                return;
            }
            if (on)
            {
                if (!humSource.isPlaying)
                {
                    humSource.Play();
                }
            }
            else
            {
                humSource.Stop();
            }
        }

        public void Place()
        {
            PlayWithPitch(placeClip, 0.9f, 1.1f);
        }

        public void Explode()
        {
            PlayWithPitch(explodeClip, 0.95f, 1.08f);
        }

        public void CleanSweep()
        {
            CleanSweep(1f);
        }

        /// <summary>The sweep "bling" - pitchMultiplier rises with the round's sweep count.</summary>
        public void CleanSweep(float pitchMultiplier)
        {
            PlayWithPitch(sweepClip, pitchMultiplier, pitchMultiplier);
        }

        public void Shuffle()
        {
            PlayWithPitch(shuffleClip, 0.95f, 1.05f);
        }

        public void Buy()
        {
            PlayWithPitch(buyClip, 1f, 1f);
        }

        /// <summary>A light high "poof" when a played bonus card expires into nothing.</summary>
        public void Vanish()
        {
            PlayWithPitch(buyClip, 1.5f, 1.7f, 0.7f);
        }

        /// <summary>Fire whoosh when the arena flames grow (clean sweeps). Slightly
        /// boosted so it reads under the sweep chime without booming.</summary>
        public void Flame()
        {
            PlayWithPitch(flameClip, 0.9f, 1.1f, 1.1f);
        }

        // ---- boss identity stings (BossIdentityView prototypes) ----

        private AudioClip[] stingClips;

        /// <summary>One of the boss-intro stings. Played at a fixed pitch: the sequences are timed
        /// against them, and a random pitch would stretch the hit away from its frame.</summary>
        public void PlayBossSting(BossSting kind)
        {
            int i = (int)kind;
            if (stingClips != null && i >= 0 && i < stingClips.Length)
            {
                PlayWithPitch(stingClips[i], 1f, 1f);
            }
        }

        /// <summary>Two alternating tones, a little square-edged (a third harmonic).</summary>
        private static AudioClip BuildStingSiren()
        {
            float[] buffer = Buffer(1.0f);
            for (int k = 0; k < 4; k++)
            {
                float f = k % 2 == 0 ? 740f : 520f;
                int start = (int)(SampleRate * 0.22f * k);
                AddTone(buffer, start, 0.24f, f, f * 0.97f, 0.20f, 0.6f);
                AddTone(buffer, start, 0.24f, f * 3f, f * 2.9f, 0.05f, 0.8f);
            }
            return Finish("stingSiren", buffer);
        }

        private static AudioClip BuildStingBoom()
        {
            float[] buffer = Buffer(1.2f);
            AddTone(buffer, 0, 1.1f, 90f, 32f, 0.75f, 1.6f);
            AddNoise(buffer, 0, 0.4f, 0.35f, 3f, new System.Random(11));
            return Finish("stingBoom", buffer);
        }

        /// <summary>Inharmonic partials with a long decay - struck metal, not a musical note.</summary>
        private static AudioClip BuildStingGong()
        {
            float[] buffer = Buffer(2.2f);
            float[] partials = { 110f, 173f, 262f, 331f, 447f };
            float[] amps = { 0.26f, 0.18f, 0.14f, 0.10f, 0.07f };
            for (int i = 0; i < partials.Length; i++)
            {
                AddTone(buffer, 0, 2.2f, partials[i], partials[i] * 0.995f, amps[i], 1.2f + i * 0.3f);
            }
            AddTone(buffer, 0, 0.3f, 70f, 45f, 0.4f, 2f);
            AddNoise(buffer, 0, 0.05f, 0.25f, 3f, new System.Random(12));
            return Finish("stingGong", buffer);
        }

        private static AudioClip BuildStingClank()
        {
            float[] buffer = Buffer(0.35f);
            AddNoise(buffer, 0, 0.08f, 0.5f, 4f, new System.Random(13));
            AddTone(buffer, 0, 0.3f, 820f, 800f, 0.15f, 5f);
            AddTone(buffer, 0, 0.3f, 1330f, 1300f, 0.12f, 5f);
            AddTone(buffer, 0, 0.3f, 2150f, 2100f, 0.08f, 6f);
            AddTone(buffer, 0, 0.12f, 140f, 90f, 0.4f, 2f);
            return Finish("stingClank", buffer);
        }

        /// <summary>Magma welling up: a deep, dark ROAR (noise filtered twice down to the low end,
        /// so there is no hiss in it anywhere) that swells as the lake spreads, a sub tone that
        /// wanders under it, and thick lava bubbles gulping through - each a soft sine whose pitch
        /// rises as it bursts, never a click.</summary>
        private static AudioClip BuildStingMagma()
        {
            const float seconds = 2.4f;
            float[] buffer = Buffer(seconds);
            var rng = new System.Random(15);
            float lp1 = 0f;
            float lp2 = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i / (float)buffer.Length;
                float s = i / (float)SampleRate;
                // Swells to its peak just before the eruption, then lets go.
                float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.8f)) * Mathf.Pow(1f - Mathf.Clamp01((t - 0.8f) / 0.2f), 1.5f);
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += (n - lp1) * 0.025f;
                lp2 += (lp1 - lp2) * 0.04f;
                float churn = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 1.3f * s) * Mathf.Sin(2f * Mathf.PI * 0.7f * s + 1f);
                float sub = Mathf.Sin(2f * Mathf.PI * (41f + 4f * Mathf.Sin(s * 2.2f)) * s);
                buffer[i] = env * (lp2 * 4f * churn + 0.16f * sub);
            }
            for (int b = 0; b < 16; b++)
            {
                float at = (float)(0.15 + rng.NextDouble() * (seconds * 0.78 - 0.15));
                float length = 0.07f + 0.09f * (float)rng.NextDouble();
                float f0 = 70f + 50f * (float)rng.NextDouble();
                float amp = 0.10f + 0.12f * (float)rng.NextDouble() * Mathf.Clamp01(at / 1.2f);
                int start = (int)(at * SampleRate);
                int count = Mathf.Min((int)(length * SampleRate), buffer.Length - start);
                double phase = 0.0;
                for (int i = 0; i < count; i++)
                {
                    float k = i / (float)count;
                    float freq = f0 * (1f + 1.4f * k * k);
                    phase += 2.0 * Mathf.PI * freq / SampleRate;
                    buffer[start + i] += Mathf.Sin((float)phase) * amp * Mathf.Sin(Mathf.PI * k) * (1f - 0.5f * k);
                }
            }
            return Finish("stingMagma", buffer);
        }

        /// <summary>A low beating drone that SWELLS rather than decays.</summary>
        private static AudioClip BuildStingDrone()
        {
            float[] buffer = Buffer(2.6f);
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i / (float)buffer.Length;
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.7f);
                float s = i / (float)SampleRate;
                buffer[i] = env * (0.22f * Mathf.Sin(2f * Mathf.PI * 55f * s)
                    + 0.18f * Mathf.Sin(2f * Mathf.PI * 58.3f * s)
                    + 0.07f * Mathf.Sin(2f * Mathf.PI * 110.5f * s));
            }
            return Finish("stingDrone", buffer);
        }

        private void PlayWithPitch(AudioClip clip, float minPitch, float maxPitch)
        {
            PlayWithPitch(clip, minPitch, maxPitch, 1f);
        }

        private void PlayWithPitch(AudioClip clip, float minPitch, float maxPitch, float volumeScale)
        {
            source.pitch = Random.Range(minPitch, maxPitch);
            source.PlayOneShot(clip, Mathf.Clamp01(FullVolume * masterVolume * volumeScale));
        }

        // ---- synthesis helpers ----

        private static float[] Buffer(float seconds)
        {
            return new float[(int)(SampleRate * seconds)];
        }

        private static void AddTone(float[] buffer, int startSample, float seconds,
            float startFreq, float endFreq, float amplitude, float decayPower)
        {
            int length = Mathf.Min((int)(SampleRate * seconds), buffer.Length - startSample);
            double phase = 0.0;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float freq = Mathf.Lerp(startFreq, endFreq, t);
                phase += 2.0 * Mathf.PI * freq / SampleRate;
                buffer[startSample + i] += Mathf.Sin((float)phase)
                    * amplitude * Mathf.Pow(1f - t, decayPower);
            }
        }

        private static void AddNoise(float[] buffer, int startSample, float seconds,
            float amplitude, float decayPower, System.Random rng)
        {
            int length = Mathf.Min((int)(SampleRate * seconds), buffer.Length - startSample);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                buffer[startSample + i] += (float)(rng.NextDouble() * 2.0 - 1.0)
                    * amplitude * Mathf.Pow(1f - t, decayPower);
            }
        }

        private static AudioClip Finish(string name, float[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Mathf.Clamp(samples[i], -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip BuildPlace()
        {
            float[] buffer = Buffer(0.07f);
            AddTone(buffer, 0, 0.07f, 260f, 170f, 0.6f, 2.5f);
            AddNoise(buffer, 0, 0.03f, 0.12f, 3f, new System.Random(1));
            return Finish("place", buffer);
        }

        private static AudioClip BuildExplode()
        {
            float[] buffer = Buffer(0.3f);
            AddNoise(buffer, 0, 0.3f, 0.55f, 2.5f, new System.Random(2));
            AddTone(buffer, 0, 0.25f, 110f, 50f, 0.5f, 2f);
            return Finish("explode", buffer);
        }

        private static AudioClip BuildSweep()
        {
            float[] buffer = Buffer(0.6f);
            AddNoise(buffer, 0, 0.35f, 0.35f, 2.5f, new System.Random(3));
            AddTone(buffer, 0, 0.2f, 120f, 55f, 0.4f, 2f);
            AddTone(buffer, 0, 0.5f, 523f, 523f, 0.26f, 1.6f);
            AddTone(buffer, (int)(SampleRate * 0.08f), 0.5f, 659f, 659f, 0.26f, 1.6f);
            AddTone(buffer, (int)(SampleRate * 0.16f), 0.44f, 784f, 784f, 0.3f, 1.5f);
            return Finish("sweep", buffer);
        }

        private static AudioClip BuildShuffle()
        {
            float[] buffer = Buffer(0.4f);
            var rng = new System.Random(4);
            for (int tick = 0; tick < 6; tick++)
            {
                int start = (int)(SampleRate * (0.05f * tick + 0.01f * (float)rng.NextDouble()));
                AddNoise(buffer, start, 0.035f, 0.35f, 1.5f, rng);
            }
            return Finish("shuffle", buffer);
        }

        private static AudioClip BuildFlame()
        {
            float[] buffer = Buffer(0.85f);
            var rng = new System.Random(5);
            AddNoise(buffer, 0, 0.85f, 0.5f, 1.1f, rng);      // roaring body (toned down)
            AddTone(buffer, 0, 0.7f, 140f, 60f, 0.28f, 1.2f); // low rumble
            for (int i = 0; i < 8; i++)
            {
                int start = (int)(SampleRate * (0.05f + 0.09f * i));
                AddNoise(buffer, start, 0.025f, 0.4f, 1f, rng); // crackles
            }
            return Finish("flame", buffer);
        }

        private static AudioClip BuildBuy()
        {
            float[] buffer = Buffer(0.2f);
            AddTone(buffer, 0, 0.09f, 880f, 880f, 0.35f, 1.5f);
            AddTone(buffer, (int)(SampleRate * 0.07f), 0.13f, 1319f, 1319f, 0.35f, 2f);
            return Finish("buy", buffer);
        }

        /// <summary>A seamless-looping CRT hum: 60 Hz mains + its 120 Hz harmonic + a thin
        /// high-pitched "flyback" whine. All frequencies are multiples of the loop's base
        /// frequency (2 Hz over a 0.5 s buffer) so the loop has no click.</summary>
        private static AudioClip BuildHum()
        {
            float[] buffer = Buffer(0.5f); // 2 Hz base -> only even frequencies loop cleanly
            AddSteadyTone(buffer, 60f, 0.12f);
            AddSteadyTone(buffer, 120f, 0.06f);
            AddSteadyTone(buffer, 9960f, 0.015f); // faint line-scan whine
            return Finish("crtHum", buffer);
        }

        /// <summary>Adds a constant-amplitude sine over the WHOLE buffer (no decay), for loops.</summary>
        private static void AddSteadyTone(float[] buffer, float freq, float amplitude)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] += Mathf.Sin(2f * Mathf.PI * freq * i / SampleRate) * amplitude;
            }
        }
    }
}
