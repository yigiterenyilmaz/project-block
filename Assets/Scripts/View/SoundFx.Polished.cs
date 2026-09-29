// PURPOSE: The POLISHED sound set - the tactile rebuild of the baseline cues (placing a block, a
// line going off, the clean sweep, the shuffle, buying, a card vanishing) plus two cues the game
// never had (picking a card up, a drop the board refuses). SoundFx.cs keeps the original clips
// untouched as the LEGACY set, so the two can be compared side by side in the sound lab (F9,
// GameUiController.SoundLab.cs); `SoundFx.Set` says which set the game plays (OLD, NEW, V3 or MIX).
//
// What makes the difference, and every builder here follows it:
//   - LAYERS, not one tone: a sharp TRANSIENT (1-5 ms of filtered noise - the "contact"), a BODY
//     whose pitch drops EXPONENTIALLY (a linear glide reads as a siren; an exponential one as an
//     impact), a MATERIAL ring (a few inharmonic resonant modes), and a SUB thump for weight.
//   - FILTERED noise: raw white noise is hiss. Every noise layer is band-limited to the region
//     the material actually lives in, and the explosion's debris closes its filter as it falls.
//   - Soft SATURATION glues the layers and lifts the perceived loudness without clipping.
//   - A tiny baked ROOM, so a hit sits somewhere instead of in a vacuum.
//   - VARIANTS: several takes of each cue, played round-robin, so the tenth block placed does not
//     sound like the ninth. And a POOL of AudioSources, because the legacy path sets the pitch of
//     ONE shared source - which re-pitches every sound still ringing on it.
//   - CONTEXT: placement pitch follows the block's size, the explosion escalates with the number
//     of lines, and a combo streak climbs in pitch.
// Deterministic seeds everywhere, so a clip is the same every launch.
// EXTENSION POINT: a new polished cue is a builder here + a branch in the public method in
// SoundFx.cs + an entry in GameUiController.SoundLab.cs (BuildSoundCatalogue).

using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class SoundFx
    {
        private const string SetPrefKey = "sfx.set2"; // reset so every install starts on MIX

        /// <summary>The sound sets, in the order the lab shows them.</summary>
        public static readonly string[] SetNames = { "OLD", "NEW", "V3", "MIX" };

        /// <summary>Which set the game plays: 0 the original clips, exactly as they were (one
        /// shared source included), 1 the first polished rebuild, 2 "V3" - the same layering with
        /// the sub taken out of the explosions and no metallic ring anywhere. Persisted; switched
        /// from the sound lab.</summary>
        public int Set
        {
            get { return activeSet; }
            set
            {
                activeSet = Mathf.Clamp(value, 0, SetNames.Length - 1);
                try { PlayerPrefs.SetInt(SetPrefKey, activeSet); } catch { }
            }
        }

        private int activeSet = 3;

        /// <summary>Plays <paramref name="cue"/> through the chosen set without changing which set
        /// the game plays (the sound lab's per-set buttons).</summary>
        public void Audition(int auditionSet, System.Action<SoundFx> cue)
        {
            int was = activeSet;
            activeSet = auditionSet;
            try
            {
                cue(this);
            }
            finally
            {
                activeSet = was;
            }
        }

        /// <summary>The take list for the set playing now (NEW, V3 or MIX).</summary>
        private AudioClip[] Pick(AudioClip[] newTakes, AudioClip[] v3Takes, AudioClip[] mixTakes)
        {
            return activeSet == 3 ? mixTakes : (activeSet == 2 ? v3Takes : newTakes);
        }

        private const int PoolSize = 16;
        private AudioSource[] pool;
        private int poolNext;

        private AudioClip[] newPlace;
        private AudioClip[][] newExplode; // [tier 0..2][variant]
        private AudioClip[] newSweep;
        private AudioClip[] newBell;
        private AudioClip[] newShuffle;
        private AudioClip[] newBuy;
        private AudioClip[] newVanish;
        private AudioClip[] newPickup;
        private AudioClip[] newReject;
        private readonly System.Collections.Generic.Dictionary<AudioClip[], int> lastVariant =
            new System.Collections.Generic.Dictionary<AudioClip[], int>();

        private void BuildPolished()
        {
            try { activeSet = Mathf.Clamp(PlayerPrefs.GetInt(SetPrefKey, 3), 0, SetNames.Length - 1); } catch { activeSet = 3; }
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }
            newPlace = Variants(4, PBuildPlace);
            newExplode = new[]
            {
                Variants(3, s => PBuildExplode(0, s)),
                Variants(3, s => PBuildExplode(1, s)),
                Variants(2, s => PBuildExplode(2, s)),
            };
            newSweep = Variants(2, PBuildSweep);
            newBell = new[] { PBuildBell() };
            newShuffle = Variants(3, PBuildShuffle);
            newBuy = Variants(3, PBuildBuy);
            newVanish = Variants(3, PBuildVanish);
            newPickup = Variants(4, PBuildPickup);
            newReject = Variants(2, PBuildReject);
            BuildV3();
            BuildMix();
        }

        private static AudioClip[] Variants(int count, System.Func<int, AudioClip> build)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++)
            {
                clips[i] = build(i);
            }
            return clips;
        }

        /// <summary>Plays one take of a polished cue on its own pooled source (so its pitch is its
        /// own), never the same take twice running.</summary>
        private void PlayPolished(AudioClip[] takes, float pitch, float volumeScale)
        {
            if (takes == null || takes.Length == 0 || pool == null)
            {
                return;
            }
            int last;
            lastVariant.TryGetValue(takes, out last);
            int pick = takes.Length == 1 ? 0 : Random.Range(0, takes.Length - 1);
            if (takes.Length > 1 && pick >= last)
            {
                pick++;
            }
            lastVariant[takes] = pick;
            PlayPolished(takes[pick], pitch, volumeScale);
        }

        private void PlayPolished(AudioClip clip, float pitch, float volumeScale)
        {
            AudioSource s = pool[poolNext];
            poolNext = (poolNext + 1) % pool.Length;
            s.pitch = pitch;
            s.PlayOneShot(clip, Mathf.Clamp01(FullVolume * masterVolume * volumeScale));
        }

        /// <summary>A small random detune in semitones, as a pitch ratio.</summary>
        private static float Detune(float semitones)
        {
            return Mathf.Pow(2f, Random.Range(-semitones, semitones) / 12f);
        }

        // ---- the polished cues (called from the public methods in SoundFx.cs) ----

        private void PolishedPlace(int cubes)
        {
            int n = Mathf.Clamp(cubes, 1, 9);
            // A single cube taps high and light; a big block lands low and heavy.
            float pitch = Mathf.Lerp(1.14f, 0.84f, (n - 1) / 6f) * Detune(0.35f);
            float volume = Mathf.Lerp(0.8f, 1.05f, (n - 1) / 6f);
            PlayPolished(Pick(newPlace, v3Place, newPlace), pitch, volume);
        }

        private void PolishedExplode(int lines, int combo)
        {
            int tier = lines >= 3 ? 2 : (lines == 2 ? 1 : 0);
            // The streak climbs a little each turn, capped: a rising pitch is what makes a chain
            // of clears feel like it is going somewhere.
            float climb = Mathf.Pow(2f, 0.6f * Mathf.Clamp(combo - 1, 0, 8) / 12f);
            float pitch = climb * Detune(0.3f);
            PlayPolished(Pick(newExplode[tier], v3Explode[tier], MixExplodeTakes(tier)), pitch, 0.95f + 0.05f * tier);
            if (activeSet == MixSet)
            {
                // MIX's thump is its own layer, so its weight is a level rather than a rebuild.
                // It climbs only half as far as the top, so a long streak keeps its weight.
                PlayPolished(mixThump[tier], Mathf.Sqrt(climb) * Detune(0.2f), MixThumpVolume);
                PlayPolished(mixBlast[tier], Mathf.Sqrt(climb) * Detune(0.4f), mixBlastLevel);
            }
        }

        private void PolishedSweep(float pitchMultiplier)
        {
            PlayPolished(Pick(newSweep, v3Sweep, mixSweep), pitchMultiplier, 1f);
            if (activeSet == MixSet)
            {
                // Same pitch as the top: pitch is speed, and the second thump has to land on the
                // chord.
                PlayPolished(mixSweepThump, pitchMultiplier, MixThumpVolume);
                PlayPolished(mixSweepBlast, pitchMultiplier, mixBlastLevel);
            }
        }

        private void PolishedBell(float pitch, float volume)
        {
            PlayPolished(Pick(newBell, v3Bell, mixBell), pitch, volume);
        }

        private void PolishedShuffle()
        {
            PlayPolished(Pick(newShuffle, v3Shuffle, mixShuffle), Detune(0.5f), 0.9f);
        }

        private void PolishedBuy()
        {
            PlayPolished(Pick(newBuy, v3Buy, mixBuy), Detune(0.15f), 0.85f);
        }

        private void PolishedVanish()
        {
            PlayPolished(Pick(newVanish, v3Vanish, mixVanish), Detune(0.8f), 0.75f);
        }

        private void PolishedPickup()
        {
            PlayPolished(Pick(newPickup, v3Pickup, newPickup), Detune(0.6f), 0.55f);
        }

        private void PolishedReject()
        {
            PlayPolished(Pick(newReject, v3Reject, newReject), Detune(0.3f), 0.8f);
        }

        // ---- builders ----

        /// <summary>A block set down: a crisp contact click, a body that thocks down in pitch, a
        /// short hollow wood ring and a soft thump underneath.</summary>
        private static AudioClip PBuildPlace(int v)
        {
            var rng = new System.Random(1000 + v);
            float j = 1f + 0.04f * (v - 1.5f);
            float[] b = Buffer(0.22f);
            PNoise(b, 0f, 0.012f, 0.40f, 0.0002f, 0.0025f, 2500f, 11000f, rng);   // contact
            PSine(b, 0f, 0.12f, 420f * j, 160f * j, 0.012f, 0.60f, 0.0008f, 0.040f); // body
            PModes(b, 0.001f, 1150f * j, new[] { 1f, 1.58f, 2.37f }, new[] { 0.11f, 0.06f, 0.035f },
                new[] { 0.028f, 0.018f, 0.011f });                                  // wood ring
            PSine(b, 0f, 0.1f, 110f, 62f, 0.02f, 0.38f, 0.001f, 0.045f);            // thump
            PNoise(b, 0.002f, 0.03f, 0.08f, 0.001f, 0.012f, 300f, 1800f, rng);     // felt of the block
            Saturate(b, 1.5f);
            Room(b, 0.07f, 0.55f);
            return PFinish("place+" + v, b, 0.9f);
        }

        /// <summary>A line going off. Tier 0 one line, 1 two, 2 three or more - each tier longer,
        /// deeper and with more debris and shimmer.</summary>
        private static AudioClip PBuildExplode(int tier, int v)
        {
            var rng = new System.Random(2000 + tier * 10 + v);
            float len = 0.45f + 0.2f * tier;
            float[] b = Buffer(len);
            float j = 1f + 0.035f * (v - 1f);
            // Crack: the moment of breaking.
            PNoise(b, 0f, 0.03f, 0.55f, 0.0002f, 0.006f, 1500f, 12000f, rng);
            // Kick: the weight. Deeper and longer per tier.
            PSine(b, 0f, 0.35f + 0.1f * tier, 155f * j, 46f - 4f * tier, 0.03f, 0.95f, 0.001f,
                0.12f + 0.05f * tier);
            // Debris: noise whose top closes as it falls, so it goes from shatter to rubble.
            PNoiseSweep(b, 0.004f, 0.3f + 0.12f * tier, 0.42f, 0.002f, 0.06f + 0.03f * tier,
                200f, 7500f, 900f, rng);
            // Crunch: small cracks scattered through the first beat - more per tier.
            int grains = 6 + 5 * tier;
            for (int g = 0; g < grains; g++)
            {
                float at = 0.01f + (float)rng.NextDouble() * (0.12f + 0.05f * tier);
                float amp = 0.10f + 0.16f * (float)rng.NextDouble();
                PNoise(b, at, 0.012f, amp, 0.0002f, 0.003f, 1800f, 9000f, rng);
            }
            // Shimmer: the glassy tail that makes a big clear feel like a reward.
            if (tier >= 1)
            {
                float s = tier == 1 ? 0.05f : 0.08f;
                PModes(b, 0.02f, 2100f * j, new[] { 1f, 1.51f, 2.24f, 3.1f },
                    new[] { s, s * 0.8f, s * 0.6f, s * 0.4f }, new[] { 0.14f, 0.12f, 0.1f, 0.08f });
            }
            if (tier == 2)
            {
                PSine(b, 0.06f, 0.4f, 70f, 38f, 0.1f, 0.35f, 0.01f, 0.18f); // an aftershock
            }
            Saturate(b, 2.1f);
            Room(b, 0.12f + 0.04f * tier, 0.8f);
            return PFinish("explode+" + tier + "." + v, b, 0.95f);
        }

        /// <summary>A bell: a struck, slightly inharmonic tone with a bright attack.</summary>
        private static void PBell(float[] b, float at, float f, float amp, float tau)
        {
            PModes(b, at, f, new[] { 1f, 2.0f, 3.01f, 4.23f, 5.4f },
                new[] { amp, amp * 0.45f, amp * 0.25f, amp * 0.14f, amp * 0.08f },
                new[] { tau, tau * 0.6f, tau * 0.4f, tau * 0.28f, tau * 0.2f });
            PNoise(b, at, 0.006f, amp * 0.5f, 0.0001f, 0.0015f, 4000f, 12000f, new System.Random((int)(f * 7)));
        }

        /// <summary>The clean sweep: a full-weight hit, then a bright rising arpeggio of bells and
        /// a long airy shimmer - the board's reward, not just another explosion.</summary>
        private static AudioClip PBuildSweep(int v)
        {
            var rng = new System.Random(3000 + v);
            float[] b = Buffer(1.7f);
            PNoise(b, 0f, 0.03f, 0.5f, 0.0002f, 0.006f, 1500f, 12000f, rng);
            PSine(b, 0f, 0.5f, 160f, 42f, 0.035f, 0.9f, 0.001f, 0.17f);
            PNoiseSweep(b, 0.004f, 0.5f, 0.35f, 0.002f, 0.12f, 200f, 8000f, 1000f, rng);
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int i = 0; i < notes.Length; i++)
            {
                PBell(b, 0.05f + 0.075f * i, notes[i] * (v == 1 ? 1.0015f : 1f), 0.16f - 0.015f * i, 0.55f);
            }
            PBell(b, 0.35f, 1568f, 0.06f, 0.7f);
            PNoise(b, 0.05f, 1.3f, 0.045f, 0.25f, 0.35f, 6000f, 13000f, rng); // air
            Saturate(b, 1.3f);
            Room(b, 0.28f, 1f);
            return PFinish("sweep+" + v, b, 0.92f);
        }

        /// <summary>A lone bell for the chimes and fusions (no hit under it).</summary>
        private static AudioClip PBuildBell()
        {
            float[] b = Buffer(1.1f);
            PBell(b, 0f, 783.99f, 0.3f, 0.5f);
            PBell(b, 0.06f, 1174.66f, 0.16f, 0.45f);
            Room(b, 0.25f, 0.9f);
            return PFinish("bell+", b, 0.85f);
        }

        /// <summary>A riffle: a burst of card flicks that speeds up and slows, then the deck taps
        /// square on the table.</summary>
        private static AudioClip PBuildShuffle(int v)
        {
            var rng = new System.Random(4000 + v);
            float[] b = Buffer(0.55f);
            const int flicks = 22;
            for (int k = 0; k < flicks; k++)
            {
                float u = k / (float)(flicks - 1);
                // Dense in the middle, looser at the ends.
                float at = 0.01f + 0.3f * (u - 0.35f * Mathf.Sin(2f * Mathf.PI * u) / (2f * Mathf.PI))
                    + 0.004f * (float)rng.NextDouble();
                float amp = (0.12f + 0.14f * (float)rng.NextDouble()) * (0.6f + 0.4f * Mathf.Sin(Mathf.PI * u));
                PNoise(b, at, 0.02f, amp, 0.0003f, 0.004f, 1800f + 800f * (float)rng.NextDouble(), 8000f, rng);
                PSine(b, at, 0.02f, 260f, 200f, 0.01f, amp * 0.25f, 0.0005f, 0.006f);
            }
            float tap = 0.39f;
            PNoise(b, tap, 0.02f, 0.3f, 0.0002f, 0.004f, 1200f, 7000f, rng);
            PSine(b, tap, 0.08f, 300f, 170f, 0.012f, 0.35f, 0.0006f, 0.025f);
            Saturate(b, 1.2f);
            Room(b, 0.06f, 0.5f);
            return PFinish("shuffle+" + v, b, 0.85f);
        }

        /// <summary>Buying: a register thunk, then two bright coin rings.</summary>
        private static AudioClip PBuildBuy(int v)
        {
            var rng = new System.Random(5000 + v);
            float[] b = Buffer(0.7f);
            float j = 1f + 0.012f * (v - 1f);
            PNoise(b, 0f, 0.01f, 0.3f, 0.0002f, 0.002f, 3000f, 12000f, rng);
            PSine(b, 0f, 0.08f, 240f, 120f, 0.012f, 0.35f, 0.0008f, 0.03f);
            float[] ratios = { 1f, 2.76f, 5.4f, 8.93f };
            PModes(b, 0.012f, 1568f * j, ratios, new[] { 0.2f, 0.08f, 0.05f, 0.025f }, new[] { 0.22f, 0.12f, 0.07f, 0.04f });
            PModes(b, 0.075f, 2093f * j, ratios, new[] { 0.22f, 0.09f, 0.05f, 0.025f }, new[] { 0.32f, 0.16f, 0.09f, 0.05f });
            PNoise(b, 0.075f, 0.008f, 0.15f, 0.0001f, 0.002f, 5000f, 13000f, rng);
            Room(b, 0.15f, 0.7f);
            return PFinish("buy+" + v, b, 0.85f);
        }

        /// <summary>A card vanishing: a small airy poof that rises and is gone.</summary>
        private static AudioClip PBuildVanish(int v)
        {
            var rng = new System.Random(6000 + v);
            float[] b = Buffer(0.35f);
            PNoiseSweep(b, 0f, 0.25f, 0.35f, 0.01f, 0.06f, 700f, 2500f, 9000f, rng, true);
            PSine(b, 0.005f, 0.1f, 800f, 1700f, 0.03f, 0.07f, 0.003f, 0.04f);
            Room(b, 0.12f, 0.6f);
            return PFinish("vanish+" + v, b, 0.8f);
        }

        /// <summary>A card lifted from the hand: a soft paper tick and a tiny upward blip.</summary>
        private static AudioClip PBuildPickup(int v)
        {
            var rng = new System.Random(7000 + v);
            float[] b = Buffer(0.12f);
            float j = 1f + 0.03f * (v - 1.5f);
            PNoise(b, 0f, 0.015f, 0.25f, 0.0002f, 0.003f, 2500f, 10000f, rng);
            PSine(b, 0.002f, 0.07f, 620f * j, 980f * j, 0.02f, 0.16f, 0.002f, 0.025f);
            PNoise(b, 0.004f, 0.06f, 0.05f, 0.01f, 0.02f, 1500f, 6000f, rng);
            Room(b, 0.05f, 0.4f);
            return PFinish("pickup+" + v, b, 0.7f);
        }

        /// <summary>The board refusing a drop: a muted, dull double knock - clearly a "no",
        /// never an alarm.</summary>
        private static AudioClip PBuildReject(int v)
        {
            var rng = new System.Random(8000 + v);
            float[] b = Buffer(0.25f);
            PSine(b, 0f, 0.1f, 190f, 120f, 0.015f, 0.5f, 0.0008f, 0.03f);
            PNoise(b, 0f, 0.02f, 0.15f, 0.0003f, 0.005f, 200f, 1400f, rng);
            PSine(b, 0.075f, 0.1f, 165f, 105f, 0.015f, 0.38f, 0.0008f, 0.03f);
            PNoise(b, 0.075f, 0.02f, 0.11f, 0.0003f, 0.005f, 200f, 1200f, rng);
            Saturate(b, 1.3f);
            Room(b, 0.05f, 0.4f);
            return PFinish("reject+" + v, b, 0.75f);
        }

        // ---- polished synthesis helpers ----

        private static float OnePole(float hz)
        {
            return 1f - Mathf.Exp(-2f * Mathf.PI * hz / SampleRate);
        }

        /// <summary>A linear attack, then an exponential decay with time constant tau.</summary>
        private static float PEnv(float t, float attack, float tau)
        {
            return t < attack ? t / attack : Mathf.Exp(-(t - attack) / tau);
        }

        /// <summary>A sine whose pitch goes from f0 toward f1 EXPONENTIALLY (pitchTau), which is
        /// what an impact does - a linear glide is a siren.</summary>
        private static void PSine(float[] b, float atSec, float seconds, float f0, float f1,
            float pitchTau, float amp, float attack, float tau)
        {
            int start = (int)(atSec * SampleRate);
            int n = Mathf.Min((int)(seconds * SampleRate), b.Length - start);
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float f = f1 + (f0 - f1) * Mathf.Exp(-t / pitchTau);
                phase += 2.0 * Mathf.PI * f / SampleRate;
                b[start + i] += Mathf.Sin((float)phase) * amp * PEnv(t, attack, tau);
            }
        }

        /// <summary>Band-limited noise: white noise through a high-pass at lowHz and a two-pole
        /// low-pass at highHz.</summary>
        private static void PNoise(float[] b, float atSec, float seconds, float amp, float attack,
            float tau, float lowHz, float highHz, System.Random rng)
        {
            PNoiseSweep(b, atSec, seconds, amp, attack, tau, lowHz, highHz, highHz, rng);
        }

        /// <summary>Band-limited noise whose low-pass (or, with sweepLow, high-pass) moves from its
        /// start to its end frequency over the sound.</summary>
        private static void PNoiseSweep(float[] b, float atSec, float seconds, float amp,
            float attack, float tau, float lowHz, float highHz0, float highHz1, System.Random rng,
            bool sweepLow = false)
        {
            int start = (int)(atSec * SampleRate);
            int n = Mathf.Min((int)(seconds * SampleRate), b.Length - start);
            float hpState = 0f, lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = i / (float)n;
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                float lowNow = sweepLow ? Mathf.Lerp(lowHz, highHz0, u) : lowHz;
                float highNow = sweepLow ? highHz1 : Mathf.Lerp(highHz0, highHz1, u);
                hpState += (x - hpState) * OnePole(lowNow);
                float hp = x - hpState;
                float a = OnePole(highNow);
                lp1 += (hp - lp1) * a;
                lp2 += (lp1 - lp2) * a;
                b[start + i] += lp2 * amp * 1.8f * PEnv(t, attack, tau);
            }
        }

        /// <summary>A struck resonator: modes at f * ratio, each with its own level and decay.
        /// </summary>
        private static void PModes(float[] b, float atSec, float f, float[] ratios, float[] amps,
            float[] taus)
        {
            int start = (int)(atSec * SampleRate);
            for (int m = 0; m < ratios.Length; m++)
            {
                float freq = f * ratios[m];
                if (freq > SampleRate * 0.45f)
                {
                    continue;
                }
                int n = Mathf.Min((int)(taus[m] * 7f * SampleRate), b.Length - start);
                float w = 2f * Mathf.PI * freq / SampleRate;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    b[start + i] += Mathf.Sin(w * i) * amps[m] * PEnv(t, 0.0004f, taus[m]);
                }
            }
        }

        /// <summary>Soft tanh saturation: rounds the peaks of the loudest layers so the whole hit
        /// can be normalised louder. The level is set afterwards by PFinish.</summary>
        private static void Saturate(float[] b, float drive)
        {
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = (float)System.Math.Tanh(b[i] * drive);
            }
        }

        /// <summary>A small Schroeder room (four damped combs into two all-passes), mixed under the
        /// dry sound. Size scales the delays.</summary>
        private static void Room(float[] b, float mix, float size)
        {
            int[] combs = { 1116, 1188, 1277, 1356 };
            int[] allpasses = { 556, 441 };
            float[] wet = new float[b.Length];
            for (int c = 0; c < combs.Length; c++)
            {
                int d = Mathf.Max(8, (int)(combs[c] * size));
                var line = new float[d];
                int p = 0;
                float damp = 0f;
                for (int i = 0; i < b.Length; i++)
                {
                    float y = line[p];
                    damp += (y - damp) * 0.6f;
                    line[p] = b[i] + damp * 0.74f;
                    p = (p + 1) % d;
                    wet[i] += y * 0.25f;
                }
            }
            foreach (int ap in allpasses)
            {
                int d = Mathf.Max(4, (int)(ap * size));
                var line = new float[d];
                int p = 0;
                for (int i = 0; i < wet.Length; i++)
                {
                    float buf = line[p];
                    float y = -wet[i] + buf;
                    line[p] = wet[i] + buf * 0.5f;
                    p = (p + 1) % d;
                    wet[i] = y;
                }
            }
            for (int i = 0; i < b.Length; i++)
            {
                b[i] += wet[i] * mix;
            }
        }

        /// <summary>Normalises to a peak, fades the last few ms so nothing clicks, and makes the
        /// clip.</summary>
        private static AudioClip PFinish(string name, float[] b, float peak)
        {
            float max = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(b[i]));
            }
            float g = max > 1e-5f ? peak / max : 1f;
            int fade = Mathf.Min(b.Length, (int)(0.006f * SampleRate));
            for (int i = 0; i < b.Length; i++)
            {
                float f = i >= b.Length - fade ? (b.Length - i) / (float)fade : 1f;
                b[i] = Mathf.Clamp(b[i] * g * f, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(name, b.Length, 1, SampleRate, false);
            clip.SetData(b, 0);
            return clip;
        }
    }
}
