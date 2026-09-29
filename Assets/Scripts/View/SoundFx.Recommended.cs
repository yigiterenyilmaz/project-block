// PURPOSE: New takes on the cues that were still PLACEHOLDER AUDIO in every set - the power
// effects PowerFxView cues (Neşter's cut, the totem drum, the earth rumble, the clover leaf, the
// whoosh, the arena's squish and stretch) and the six boss-intro stings. Each cue has three
// variants (A, B, C) built in the MIX palette the listening settled on - crunch, zip, muted
// plucks, soft chip squares, the thump and the dry mid rush - and avoiding what it ruled out:
// no inharmonic metallic "ting", no resonance on anything meant to be dry (it goes WET), no low
// upward-gliding pops (the "fart"), and no big room on a low sound (a boxy reverbed thump).
//
// Version 0 of every cue is the ORIGINAL clip, untouched. Which version the game plays is chosen
// per cue in the sound lab and remembered (RecChoice); the defaults are the listening's picks
// (RecDefault) - B for the cut and the drum, D for the whoosh, E for the squish and the stretch,
// A for the drone sting, the original for the rest.
// The boss stings keep their originals' LENGTHS: the intro sequences are timed against them.
// EXTENSION POINT: a new variant is one more builder in BuildRecommended's table.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The cues with recommended variants, in the order the sound lab lists them.</summary>
    public enum RecCue
    {
        Cut, Drum, Rumble, Pluck, Whoosh, Squish, Stretch,
        StingSiren, StingBoom, StingGong, StingClank, StingDrone, StingMagma
    }

    public sealed partial class SoundFx
    {
        public const int RecVariantCount = 5;

        /// <summary>Version labels: 0 is the original, then the variants.</summary>
        public static readonly string[] RecVersionNames = { "ORIG", "A", "B", "C", "D", "E" };

        private AudioClip[][] recClips; // [cue][variant 0..4] - A..E
        private int[] recChoice;        // per cue: 0 original, 1..3 a variant

        private static string RecPrefKey(RecCue cue)
        {
            return "sfx.rec3." + cue; // reset whenever the listening's picks change the defaults
        }

        /// <summary>The listening's picks (2026-09-28), the version each cue plays until changed
        /// in the lab: 0 the original, 1 A, 2 B, 3 C. Indexed by RecCue.</summary>
        private static readonly int[] RecDefault =
        {
            2, // Cut      - B, the shear
            2, // Drum     - B, the slit drum
            0, // Rumble   - the original
            0, // Pluck    - the original
            4, // Whoosh   - D, the longer, darker pass-by
            5, // Squish   - E, the squeeze ending on a falling chip tick
            5, // Stretch  - E, the elastic rise with a stronger wobble over a zip
            0, 0, 0, 0,  // Siren, Boom, Gong, Clank - the originals
            1, // Drone    - A, the square pad
            0, // Magma    - the original
        };

        /// <summary>The version the GAME plays for this cue (0 the original).</summary>
        public int RecChoice(RecCue cue)
        {
            return recChoice != null ? recChoice[(int)cue] : 0;
        }

        public void SetRecChoice(RecCue cue, int version)
        {
            recChoice[(int)cue] = Mathf.Clamp(version, 0, RecVariantCount);
            try { PlayerPrefs.SetInt(RecPrefKey(cue), recChoice[(int)cue]); } catch { }
        }

        /// <summary>Plays one version of a cue (0 the original) without changing the game's
        /// choice - the sound lab's number keys.</summary>
        public void AuditionRec(RecCue cue, int version, float pitch)
        {
            int was = recChoice[(int)cue];
            recChoice[(int)cue] = Mathf.Clamp(version, 0, RecVariantCount);
            PlayRecCue(cue, pitch);
            recChoice[(int)cue] = was;
        }

        /// <summary>Plays a cue the way the game does: its chosen variant, or the original.</summary>
        private void PlayRecCue(RecCue cue, float pitch)
        {
            switch (cue)
            {
                case RecCue.Cut: Cut(); break;
                case RecCue.Drum: Drum(pitch); break;
                case RecCue.Rumble: Rumble(); break;
                case RecCue.Pluck: Pluck(pitch); break;
                case RecCue.Whoosh: Whoosh(); break;
                case RecCue.Squish: Squish(); break;
                case RecCue.Stretch: Stretch(); break;
                default: PlayBossSting((BossSting)((int)cue - (int)RecCue.StingSiren)); break;
            }
        }

        /// <summary>True when a variant answered (the caller then skips the original clip).</summary>
        private bool TryRec(RecCue cue, float pitch, float volume)
        {
            int v = RecChoice(cue);
            if (v <= 0 || recClips == null)
            {
                return false;
            }
            PlayPolished(recClips[(int)cue][v - 1], pitch, volume);
            return true;
        }

        private void BuildRecommended()
        {
            int count = System.Enum.GetValues(typeof(RecCue)).Length;
            recChoice = new int[count];
            for (int i = 0; i < count; i++)
            {
                try { recChoice[i] = Mathf.Clamp(PlayerPrefs.GetInt(RecPrefKey((RecCue)i), RecDefault[i]), 0, RecVariantCount); }
                catch { recChoice[i] = RecDefault[i]; }
            }
            recClips = new AudioClip[count][];
            for (int c = 0; c < count; c++)
            {
                recClips[c] = new AudioClip[RecVariantCount];
                for (int v = 0; v < RecVariantCount; v++)
                {
                    recClips[c][v] = BuildRec((RecCue)c, v);
                }
            }
        }

        private static AudioClip BuildRec(RecCue cue, int v)
        {
            var rng = new System.Random(40000 + (int)cue * 10 + v);
            string name = "rec" + cue + "+" + v;
            if (v >= 3)
            {
                return RecSecondRound(cue, v - 3, rng, name); // D and E, SoundFx.Recommended2.cs
            }
            switch (cue)
            {
                case RecCue.Cut: return RecCut(v, rng, name);
                case RecCue.Drum: return RecDrum(v, rng, name);
                case RecCue.Rumble: return RecRumble(v, rng, name);
                case RecCue.Pluck: return RecLeaf(v, rng, name);
                case RecCue.Whoosh: return RecWhoosh(v, rng, name);
                case RecCue.Squish: return RecSquish(v, rng, name);
                case RecCue.Stretch: return RecStretch(v, rng, name);
                case RecCue.StingSiren: return RecSiren(v, rng, name);
                case RecCue.StingBoom: return RecBoom(v, rng, name);
                case RecCue.StingGong: return RecGong(v, rng, name);
                case RecCue.StingClank: return RecClank(v, rng, name);
                case RecCue.StingDrone: return RecDrone(v, rng, name);
                default: return RecMagma(v, rng, name);
            }
        }

        // ---- shared pieces ----

        /// <summary>The thump, as the line clear has it: an exponential drop into the low end.
        /// </summary>
        private static void Thump(float[] b, float at, float amp, float f0, float f1, float tau)
        {
            PSine(b, at, tau * 4f, f0, f1, 0.03f, amp, 0.001f, tau);
        }

        /// <summary>The dry mid RUSH (the explosion bed's recipe): band noise 350 Hz - 2.4 kHz
        /// that swells in, rolls and closes, with no room.</summary>
        private static void Rush(float[] b, float atSec, float len, float attack, float tau, float amp,
            float top, System.Random rng)
        {
            int start = (int)(atSec * SampleRate);
            int n = (int)(len * SampleRate);
            float hp = 0f, l1 = 0f, l2 = 0f, roll = 1f, target = 1f;
            int step = (int)(0.025f * SampleRate);
            float hpA = OnePole(300f);
            for (int i = 0; i < n && start + i < b.Length; i++)
            {
                float t = i / (float)SampleRate;
                if (i % step == 0) { target = 0.65f + 0.35f * (float)rng.NextDouble(); }
                roll += (target - roll) * 0.003f;
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                hp += (x - hp) * hpA;
                float a = OnePole(Mathf.Lerp(top, top * 0.33f, Mathf.Clamp01(t / (tau * 3f))));
                l1 += ((x - hp) - l1) * a;
                l2 += (l1 - l2) * a;
                float env = t < attack ? Mathf.SmoothStep(0f, 1f, t / attack) : Mathf.Exp(-(t - attack) / tau);
                float tail = Mathf.Clamp01((n - i) / (0.01f * SampleRate));
                b[start + i] += l2 * env * roll * tail * amp;
            }
        }

        /// <summary>A dry knock: a contact tick over a short band of noise and a fast-dropping
        /// body - wood or stone, never metal.</summary>
        private static void Knock(float[] b, float at, float hz, float amp, System.Random rng)
        {
            PNoise(b, at, 0.006f, 0.45f * amp, 0.0002f, 0.0015f, 2500f, 11000f, rng);
            PSine(b, at, 0.06f, hz * 1.8f, hz, 0.006f, 0.5f * amp, 0.0004f, 0.018f);
            PNoise(b, at, 0.03f, 0.3f * amp, 0.0004f, 0.008f, hz * 1.2f, hz * 4f, rng);
        }

        // ---- the power cues ----

        /// <summary>Neşter: a blade drawn through a block. A - a bright zip that ends in the
        /// block parting (a knock and a tiny pluck). B - a shear: two crisp grains and a zip
        /// falling the other way. C - a chip slice: a quick falling two-note square over a zip.
        /// </summary>
        private static AudioClip RecCut(int v, System.Random rng, string name)
        {
            float[] b = Buffer(0.32f);
            if (v == 0)
            {
                Zip(b, 0f, 0.08f, 1800f, 6000f, 4000f, 13000f, 0.2f, 0.8f, rng);
                Knock(b, 0.075f, 900f, 0.8f, rng);
                Pluck(b, 0.08f, 1567.98f, 0.14f, 0.99f, 0.5f, rng);
            }
            else if (v == 1)
            {
                PNoise(b, 0f, 0.006f, 0.4f, 0.0001f, 0.0012f, 3000f, 12000f, rng);
                PNoise(b, 0.02f, 0.006f, 0.35f, 0.0001f, 0.0012f, 3500f, 12000f, rng);
                Zip(b, 0.01f, 0.1f, 6000f, 1500f, 13000f, 3500f, 0.1f, 0.7f, rng);
                Knock(b, 0.1f, 700f, 0.6f, rng);
            }
            else
            {
                Zip(b, 0f, 0.07f, 1800f, 6000f, 4000f, 13000f, 0.2f, 0.5f, rng);
                Square(b, 0.01f, 1318.51f, 0.16f, 0.03f);
                Square(b, 0.05f, 987.77f, 0.18f, 0.05f);
                Knock(b, 0.05f, 800f, 0.5f, rng);
            }
            HighPass(b, 150f);
            return PFinish(name, b, 0.85f);
        }

        /// <summary>The totem landing / its eyes waking (pitched by the caller). A - a log drum:
        /// a wooden body and a knock over a soft thump. B - a slit drum: two harmonic wooden tones
        /// a fifth apart. C - a small taiko: a thump with a skin slap on top.</summary>
        private static AudioClip RecDrum(int v, System.Random rng, string name)
        {
            float[] b = Buffer(0.5f);
            if (v == 0)
            {
                PSine(b, 0f, 0.25f, 240f, 140f, 0.02f, 0.55f, 0.001f, 0.06f);
                Knock(b, 0f, 500f, 0.6f, rng);
                Thump(b, 0f, 0.45f, 120f, 65f, 0.07f);
            }
            else if (v == 1)
            {
                PSine(b, 0f, 0.3f, 200f, 180f, 0.01f, 0.5f, 0.001f, 0.08f);
                PSine(b, 0f, 0.25f, 300f, 270f, 0.01f, 0.3f, 0.001f, 0.06f);
                PNoise(b, 0f, 0.02f, 0.3f, 0.0003f, 0.004f, 600f, 3000f, rng);
            }
            else
            {
                Thump(b, 0f, 0.8f, 130f, 55f, 0.1f);
                PNoise(b, 0f, 0.05f, 0.4f, 0.0005f, 0.012f, 700f, 4500f, rng);
            }
            Saturate(b, 1.3f);
            HighPass(b, 45f);
            return PFinish(name, b, 0.88f);
        }

        /// <summary>Earth giving way (~0.8 s). A - a rolling rush with a crumble of grains through
        /// it. B - gravel sliding: dense dry grains thinning out over a low roll. C - stone
        /// grinding: a slow wobbling rush and a few heavy knocks.</summary>
        private static AudioClip RecRumble(int v, System.Random rng, string name)
        {
            float[] b = Buffer(0.85f);
            if (v == 0)
            {
                Rush(b, 0f, 0.8f, 0.08f, 0.25f, 0.8f, 1600f, rng);
                for (int g = 0; g < 26; g++)
                {
                    float at = 0.05f + 0.6f * (float)rng.NextDouble();
                    PNoise(b, at, 0.008f, 0.12f + 0.12f * (float)rng.NextDouble(), 0.0002f, 0.002f, 1200f, 5000f, rng);
                }
                Thump(b, 0.02f, 0.35f, 90f, 50f, 0.15f);
            }
            else if (v == 1)
            {
                for (int g = 0; g < 60; g++)
                {
                    float u = (float)System.Math.Pow(rng.NextDouble(), 1.6);
                    float at = 0.01f + 0.75f * u;
                    float lo = 1500f + 2500f * (float)rng.NextDouble();
                    PNoise(b, at, 0.006f, (0.15f + 0.2f * (float)rng.NextDouble()) * (1f - 0.7f * u),
                        0.0001f, 0.0015f, lo, lo * 2.2f, rng);
                }
                Rush(b, 0f, 0.8f, 0.05f, 0.3f, 0.5f, 900f, rng);
            }
            else
            {
                Rush(b, 0f, 0.8f, 0.12f, 0.35f, 0.9f, 1100f, rng);
                for (int k = 0; k < 4; k++)
                {
                    Knock(b, 0.05f + 0.16f * k + 0.03f * (float)rng.NextDouble(), 260f, 0.55f - 0.08f * k, rng);
                }
            }
            Saturate(b, 1.2f);
            HighPass(b, 60f);
            return PFinish(name, b, 0.85f);
        }

        /// <summary>A clover leaf snapped off (pitched per leaf by the caller). A - a stem's snap
        /// and a small high pluck. B - a papery tear: a short rising zip and a tick. C - a chip
        /// blip over the snap.</summary>
        private static AudioClip RecLeaf(int v, System.Random rng, string name)
        {
            float[] b = Buffer(0.3f);
            PNoise(b, 0f, 0.005f, 0.4f, 0.0001f, 0.0012f, 3000f, 12000f, rng); // the snap
            if (v == 0)
            {
                Pluck(b, 0.004f, 1174.66f, 0.3f, 0.99f, 0.45f, rng);
            }
            else if (v == 1)
            {
                Zip(b, 0.002f, 0.05f, 2000f, 5000f, 5000f, 12000f, 0.3f, 0.6f, rng);
                PNoise(b, 0.05f, 0.004f, 0.3f, 0.0001f, 0.001f, 3500f, 12000f, rng);
            }
            else
            {
                Square(b, 0.004f, 1567.98f, 0.14f, 0.035f);
                Square(b, 0.03f, 2093f, 0.1f, 0.04f);
            }
            HighPass(b, 200f);
            return PFinish(name, b, 0.8f);
        }

        /// <summary>Something flying or swinging by (~0.45 s). A - a zip that swells up and away.
        /// B - a swing: a band that opens and closes again. C - a soft "fwip" cut at its peak.
        /// </summary>
        private static AudioClip RecWhoosh(int v, System.Random rng, string name)
        {
            float[] b = Buffer(0.5f);
            if (v == 0)
            {
                PassBy(b, 0.46f, rng);
            }
            else if (v == 1)
            {
                Zip(b, 0f, 0.22f, 400f, 1500f, 1000f, 6000f, 0.9f, 0.7f, rng);
                Zip(b, 0.2f, 0.22f, 1500f, 400f, 6000f, 1200f, 0.05f, 0.7f, rng);
            }
            else
            {
                Zip(b, 0f, 0.18f, 600f, 3000f, 1500f, 11000f, 0.95f, 0.9f, rng);
            }
            HighPass(b, 180f);
            return PFinish(name, b, 0.75f);
        }

        /// <summary>
        /// Whoosh A, polished after the listen: a PASS-BY rather than a swell. A band of air
        /// whose centre climbs as the thing approaches and falls as it leaves (the doppler every
        /// real whoosh has), a smooth bell of level with no corner in it anywhere, a little
        /// turbulence flutter at the loudest point, a soft low body of displaced air under the
        /// peak and a thin bright edge riding the top - then a clean tail rather than a cut.
        /// </summary>
        private static void PassBy(float[] b, float len, System.Random rng)
        {
            int n = Mathf.Min(b.Length, (int)(len * SampleRate));
            float hp = 0f, l1 = 0f, l2 = 0f, body = 0f, edgeHp = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float t = i / (float)SampleRate;
                // Peak a little before the middle: arrival is quicker than departure.
                float shape = u < 0.42f ? u / 0.42f : 1f - (u - 0.42f) / 0.58f;
                float bell = Mathf.Pow(Mathf.Sin(Mathf.PI * 0.5f * Mathf.Clamp01(shape)), 2f);
                float centre = Mathf.Lerp(700f, 2600f, bell);
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                hp += (x - hp) * OnePole(centre * 0.45f);
                float a = OnePole(centre * 2.2f);
                l1 += ((x - hp) - l1) * a;
                l2 += (l1 - l2) * a;
                body += (x - body) * OnePole(420f);
                edgeHp += (x - edgeHp) * OnePole(6000f);
                float flutter = 1f + 0.12f * bell * Mathf.Sin(2f * Mathf.PI * 17f * t + 3f * bell);
                b[i] += (l2 * 1.0f + body * 0.35f * bell + (x - edgeHp) * 0.05f * bell * bell) * bell * flutter;
            }
        }

        /// <summary>The arena crushing its bands inward (~0.4 s). A - a squeeze: a closing band,
        /// a falling tone and a crunch landing on a thump. B - rubbery: a wobbling falling tone
        /// over soft noise. C - a chip squeeze: a falling three-note square over a thump.</summary>
        private static AudioClip RecSquish(int v, System.Random rng, string name)
        {
            float[] b = Buffer(0.45f);
            if (v == 0)
            {
                PNoiseSweep(b, 0f, 0.3f, 0.35f, 0.02f, 0.08f, 200f, 5000f, 500f, rng);
                PSine(b, 0f, 0.3f, 420f, 150f, 0.08f, 0.3f, 0.01f, 0.08f);
                Crunch(b, 0.15f, 0.6f, 0, 0.9f, 0.05f, rng);
                Thump(b, 0.15f, 0.5f, 140f, 60f, 0.07f);
            }
            else if (v == 1)
            {
                int n = (int)(0.35f * SampleRate);
                double phase = 0;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    float f = Mathf.Lerp(340f, 120f, t / 0.35f) * (1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 18f * t));
                    phase += 2.0 * Mathf.PI * f / SampleRate;
                    b[i] += Mathf.Sin((float)phase) * 0.5f * Mathf.Exp(-t / 0.12f);
                }
                PNoise(b, 0f, 0.25f, 0.12f, 0.01f, 0.06f, 300f, 2000f, rng);
            }
            else
            {
                Square(b, 0f, 783.99f, 0.14f, 0.03f);
                Square(b, 0.05f, 587.33f, 0.14f, 0.03f);
                Square(b, 0.1f, 392f, 0.16f, 0.06f);
                Thump(b, 0.1f, 0.45f, 140f, 60f, 0.07f);
            }
            Saturate(b, 1.3f);
            HighPass(b, 50f);
            return PFinish(name, b, 0.85f);
        }

        /// <summary>The arena stretching open (~0.55 s). A - a rising zip with a pluck climbing
        /// through it. B - elastic: a wobbling rising tone and an opening band. C - a chip
        /// stretch: a rising square arpeggio over a zip.</summary>
        private static AudioClip RecStretch(int v, System.Random rng, string name)
        {
            float[] b = Buffer(0.6f);
            if (v == 0)
            {
                Zip(b, 0f, 0.4f, 400f, 2500f, 1200f, 9000f, 0.6f, 0.6f, rng);
                float[] up = { 392f, 587.33f, 783.99f };
                for (int k = 0; k < up.Length; k++)
                {
                    Pluck(b, 0.08f + 0.1f * k, up[k], 0.2f, 0.99f, 0.45f, rng);
                }
            }
            else if (v == 1)
            {
                // The tone used to STOP at 0.45 s while still audible, which is the cut-off that was
                // heard in the middle: it now rises over the stretch, holds its top and fades out
                // to nothing inside the clip.
                int n = b.Length;
                double phase = 0;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    float rise = Mathf.Clamp01(t / 0.4f);
                    float f = Mathf.Lerp(160f, 420f, Mathf.SmoothStep(0f, 1f, rise))
                        * (1f + 0.05f * Mathf.Sin(2f * Mathf.PI * 14f * t) * (1f - 0.6f * rise));
                    phase += 2.0 * Mathf.PI * f / SampleRate;
                    float env = Mathf.Min(1f, t / 0.05f) * Mathf.Exp(-t / 0.3f)
                        * Mathf.Clamp01((n - i) / (0.08f * SampleRate));
                    b[i] += Mathf.Sin((float)phase) * 0.45f * env;
                }
                PNoiseSweep(b, 0f, 0.55f, 0.12f, 0.05f, 0.2f, 300f, 800f, 5000f, rng);
            }
            else
            {
                Zip(b, 0f, 0.3f, 400f, 2500f, 1200f, 9000f, 0.6f, 0.4f, rng);
                float[] arp = { 392f, 523.25f, 659.25f, 783.99f };
                for (int k = 0; k < arp.Length; k++)
                {
                    Square(b, 0.04f + 0.06f * k, arp[k], 0.14f, k == arp.Length - 1 ? 0.07f : 0.035f);
                }
            }
            Saturate(b, 1.2f);
            HighPass(b, 90f);
            return PFinish(name, b, 0.8f);
        }

        // ---- the boss stings (lengths kept: the intros are timed against them) ----

        /// <summary>Siren (~1.0 s, four beats). A - a soft two-tone chip siren. B - alternating
        /// pluck pairs an octave apart over a zip per beat. C - a pulsing rush alarm with a thump
        /// on each beat.</summary>
        private static AudioClip RecSiren(int v, System.Random rng, string name)
        {
            float[] b = Buffer(1.0f);
            for (int k = 0; k < 4; k++)
            {
                float at = 0.22f * k;
                bool hi = k % 2 == 0;
                if (v == 0)
                {
                    Square(b, at, hi ? 740f : 523.25f, 0.22f, 0.12f);
                }
                else if (v == 1)
                {
                    Pluck(b, at, hi ? 783.99f : 587.33f, 0.35f, 0.992f, 0.4f, rng);
                    Pluck(b, at + 0.01f, hi ? 1567.98f : 1174.66f, 0.2f, 0.99f, 0.45f, rng);
                    Zip(b, at, 0.1f, 800f, 2500f, 2000f, 8000f, 0.3f, 0.3f, rng);
                }
                else
                {
                    Rush(b, at, 0.2f, 0.02f, 0.06f, 0.8f, hi ? 2400f : 1500f, rng);
                    Thump(b, at, 0.4f, 130f, 60f, 0.06f);
                }
            }
            Saturate(b, 1.2f);
            HighPass(b, 60f);
            return PFinish(name, b, 0.85f);
        }

        /// <summary>Boom (~1.2 s). A - a big thump with a long dry rush and a crunch on top. B - a
        /// thump and a low roll with a falling chip line. C - a crunch and a thump, the rush
        /// arriving a beat late like a far echo (made dry, never a room).</summary>
        private static AudioClip RecBoom(int v, System.Random rng, string name)
        {
            float[] b = Buffer(1.2f);
            if (v == 0)
            {
                Thump(b, 0f, 1f, 150f, 42f, 0.2f);
                Rush(b, 0f, 1.1f, 0.03f, 0.3f, 0.8f, 1800f, rng);
                Crunch(b, 0f, 1f, 2, 1f, 0.1f, rng);
            }
            else if (v == 1)
            {
                Thump(b, 0f, 1f, 140f, 45f, 0.2f);
                Rush(b, 0.01f, 1.1f, 0.06f, 0.35f, 0.7f, 900f, rng);
                Square(b, 0.05f, 392f, 0.16f, 0.05f);
                Square(b, 0.12f, 293.66f, 0.16f, 0.05f);
                Square(b, 0.19f, 196f, 0.18f, 0.12f);
            }
            else
            {
                Crunch(b, 0f, 1f, 2, 1f, 0.08f, rng);
                Thump(b, 0f, 0.9f, 150f, 45f, 0.16f);
                Rush(b, 0.18f, 0.9f, 0.08f, 0.3f, 0.55f, 1200f, rng);
            }
            Saturate(b, 1.5f);
            HighPass(b, 40f);
            return PFinish(name, b, 0.9f);
        }

        /// <summary>Gong (~2.2 s) - the one sting that was struck METAL; these keep its weight and
        /// its long decay without the inharmonic ring. A - a deep plucked chord over a thump. B - a
        /// low soft square chord dying slowly. C - a bowed swell: an opening band over a low
        /// octave pair.</summary>
        private static AudioClip RecGong(int v, System.Random rng, string name)
        {
            float[] b = Buffer(2.2f);
            if (v == 0)
            {
                Thump(b, 0f, 0.6f, 120f, 55f, 0.2f);
                foreach (float f in new[] { 98f, 146.83f, 196f, 293.66f })
                {
                    Pluck(b, 0.004f * (float)rng.NextDouble(), f, 0.4f, 0.998f, 0.35f, rng);
                }
            }
            else if (v == 1)
            {
                Thump(b, 0f, 0.5f, 120f, 55f, 0.18f);
                foreach (float f in new[] { 110f, 164.81f, 220f })
                {
                    Square(b, 0f, f, 0.2f, 0.55f);
                }
            }
            else
            {
                PNoiseSweep(b, 0f, 2.1f, 0.25f, 0.6f, 0.6f, 150f, 400f, 1800f, rng);
                PSine(b, 0f, 2.1f, 110f, 110f, 1f, 0.35f, 0.5f, 0.7f);
                PSine(b, 0f, 2.1f, 220f, 220f, 1f, 0.2f, 0.6f, 0.6f);
                Thump(b, 0f, 0.4f, 110f, 55f, 0.15f);
            }
            Saturate(b, 1.2f);
            HighPass(b, 45f);
            return PFinish(name, b, 0.85f);
        }

        /// <summary>Clank (~0.35 s). A - a heavy dry knock with a crunch. B - a short low chip
        /// stab. C - a double knock with a zip between.</summary>
        private static AudioClip RecClank(int v, System.Random rng, string name)
        {
            float[] b = Buffer(0.35f);
            if (v == 0)
            {
                Knock(b, 0f, 420f, 1f, rng);
                Crunch(b, 0f, 0.6f, 0, 1f, 0.05f, rng);
                Thump(b, 0f, 0.4f, 140f, 80f, 0.05f);
            }
            else if (v == 1)
            {
                Square(b, 0f, 196f, 0.25f, 0.05f);
                Square(b, 0.04f, 146.83f, 0.25f, 0.08f);
                Knock(b, 0f, 500f, 0.6f, rng);
            }
            else
            {
                Knock(b, 0f, 450f, 0.9f, rng);
                Zip(b, 0.02f, 0.08f, 1200f, 4000f, 3000f, 10000f, 0.3f, 0.4f, rng);
                Knock(b, 0.11f, 380f, 0.7f, rng);
            }
            Saturate(b, 1.4f);
            HighPass(b, 60f);
            return PFinish(name, b, 0.88f);
        }

        /// <summary>Drone (~2.6 s, a SWELL, never a hit). A - a soft low square pad beating slowly.
        /// B - wind: a rush swelling open and closing over a low tone. C - a low pluck tremolo,
        /// the same notes repeated and swelling.</summary>
        private static AudioClip RecDrone(int v, System.Random rng, string name)
        {
            const float len = 2.6f;
            float[] b = Buffer(len);
            if (v == 0)
            {
                int n = b.Length;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Pow(Mathf.Sin(Mathf.PI * t / len), 0.8f);
                    float s = 0f;
                    foreach (float f in new[] { 55f, 57.5f, 110f })
                    {
                        for (int h = 1; h <= 5; h += 2)
                        {
                            s += Mathf.Sin(2f * Mathf.PI * f * h * t) / h;
                        }
                    }
                    b[i] += s * 0.12f * env;
                }
            }
            else if (v == 1)
            {
                int n = b.Length;
                float l1 = 0f, l2 = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Pow(Mathf.Sin(Mathf.PI * t / len), 1.2f);
                    float a = OnePole(Mathf.Lerp(200f, 1400f, env));
                    float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                    l1 += (x - l1) * a;
                    l2 += (l1 - l2) * a;
                    b[i] += (l2 * 1.5f + 0.25f * Mathf.Sin(2f * Mathf.PI * 65f * t)) * env;
                }
            }
            else
            {
                int notes = 20;
                for (int k = 0; k < notes; k++)
                {
                    float at = len * 0.9f * k / notes;
                    float env = Mathf.Pow(Mathf.Sin(Mathf.PI * (k + 0.5f) / notes), 1.2f);
                    Pluck(b, at, k % 2 == 0 ? 110f : 164.81f, 0.35f * env, 0.995f, 0.5f, rng);
                }
            }
            HighPass(b, 35f);
            return PFinish(name, b, 0.8f);
        }

        /// <summary>Magma (~2.4 s, swelling to an eruption). A - a dry fire roar welling up with
        /// crackle, breaking on a thump. B - the roar with slow heavy knocks, like rock heaving.
        /// C - a rush rising to a crunching burst.</summary>
        private static AudioClip RecMagma(int v, System.Random rng, string name)
        {
            const float len = 2.4f;
            float[] b = Buffer(len);
            // A dry, dark roar swelling to 80 % of the length, then letting go.
            int n = b.Length;
            float l1 = 0f, l2 = 0f, flick = 1f, target = 1f;
            int step = (int)(0.03f * SampleRate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                if (i % step == 0) { target = 0.6f + 0.4f * (float)rng.NextDouble(); }
                flick += (target - flick) * 0.003f;
                float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.8f))
                    * Mathf.Pow(1f - Mathf.Clamp01((t - 0.8f) / 0.2f), 1.5f);
                float a = OnePole(Mathf.Lerp(250f, v == 2 ? 2400f : 1200f, env));
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                l1 += (x - l1) * a;
                l2 += (l1 - l2) * a;
                b[i] += l2 * 1.4f * env * flick;
            }
            if (v == 0)
            {
                for (int c = 0; c < 50; c++)
                {
                    float at = len * 0.75f * (float)System.Math.Pow(rng.NextDouble(), 0.7);
                    Crackle(b, at, 0.2f + 0.4f * (at / len), rng);
                }
                Thump(b, len * 0.78f, 0.8f, 130f, 45f, 0.2f);
            }
            else if (v == 1)
            {
                for (int k = 0; k < 5; k++)
                {
                    Knock(b, 0.3f + 0.4f * k, 200f, 0.4f + 0.1f * k, rng);
                }
                Thump(b, len * 0.78f, 0.7f, 120f, 45f, 0.2f);
            }
            else
            {
                Crunch(b, len * 0.78f, 1.3f, 2, 1f, 0.1f, rng);
                Thump(b, len * 0.78f, 0.7f, 140f, 45f, 0.18f);
            }
            Saturate(b, 1.3f);
            HighPass(b, 45f);
            return PFinish(name, b, 0.88f);
        }
    }
}
