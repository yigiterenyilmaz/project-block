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
        private AudioClip placeClip;
        private AudioClip explodeClip;
        private AudioClip sweepClip;
        private AudioClip shuffleClip;
        private AudioClip buyClip;
        private AudioClip flameClip;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            placeClip = BuildPlace();
            explodeClip = BuildExplode();
            sweepClip = BuildSweep();
            shuffleClip = BuildShuffle();
            buyClip = BuildBuy();
            flameClip = BuildFlame();
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
    }
}
