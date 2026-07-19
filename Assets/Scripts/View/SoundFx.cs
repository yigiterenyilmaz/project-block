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
        private const float MasterVolume = 0.5f;

        private AudioSource source;
        private AudioSource humSource;
        private AudioClip placeClip;
        private AudioClip explodeClip;
        private AudioClip sweepClip;
        private AudioClip shuffleClip;
        private AudioClip buyClip;
        private AudioClip flameClip;
        private AudioClip humClip;

        // ---- retro ("CRT") audio: a looping hum + a bit-crush DSP on this object's sources,
        // both toggled by SetRetro. retroActive is read on the audio thread, hence volatile.
        private volatile bool retroActive;
        private int crushHold;      // samples until the next fresh sample is latched (downsample)
        private float[] crushHeld;  // last latched sample per channel

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
            humSource.clip = humClip;
            humSource.volume = 0.18f;
        }

        /// <summary>Turns the retro CRT audio on/off: the mains-hum loop and the bit-crush that
        /// downsamples + bit-reduces every sound this object plays. Called wherever the CRT
        /// overlay is toggled (RetroMode).</summary>
        public void SetRetro(bool on)
        {
            retroActive = on;
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

        /// <summary>Bit-crush filter on this GameObject's AudioSources (SFX + hum). Runs on the
        /// audio thread, so it stays allocation-free except for a one-time per-channel buffer.
        /// Pass-through when retro is off.</summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!retroActive || channels <= 0)
            {
                return;
            }
            if (crushHeld == null || crushHeld.Length != channels)
            {
                crushHeld = new float[channels]; // only when the channel count changes (rare)
            }
            const int downsample = 4;   // ~44.1 kHz -> ~11 kHz sample-and-hold
            const float levels = 32f;   // ~5-bit depth quantization
            for (int i = 0; i + channels <= data.Length; i += channels)
            {
                if (crushHold <= 0)
                {
                    crushHold = downsample;
                    for (int c = 0; c < channels; c++)
                    {
                        crushHeld[c] = Mathf.Round(data[i + c] * levels) / levels;
                    }
                }
                crushHold--;
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = crushHeld[c];
                }
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

        private void PlayWithPitch(AudioClip clip, float minPitch, float maxPitch)
        {
            PlayWithPitch(clip, minPitch, maxPitch, 1f);
        }

        private void PlayWithPitch(AudioClip clip, float minPitch, float maxPitch, float volumeScale)
        {
            source.pitch = Random.Range(minPitch, maxPitch);
            source.PlayOneShot(clip, Mathf.Clamp01(MasterVolume * volumeScale));
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
