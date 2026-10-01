// PURPOSE: "Simetri"'s sound, synthesized and baked once (SoundFx.Symmetry plays it). Plain C# on
// float arrays - no UnityEngine - so every cue can be rendered and MEASURED outside the editor.
//
// THE FAMILY: clean, bright, short, crystalline - and HARMONIC. The game's sound pass already
// learned that an inharmonic "ting" reads as cheap (V3 took them all out), so the glass here is
// built from whole-number partials with fast decays: a struck glass rod, not a bell, not a coin.
//   DETECT     a soft tick and a breath of air - "seen".
//   TRACE      "şiink": a short rising band of noise with a faint glide under it - the pen.
//   LOCK       "dış-şınk": a hard little transient, a hiss that is cut short, and a bright pluck on
//              a PENTATONIC LADDER - each pair a step higher than the last, so a run of locks is a
//              little phrase that BUILDS rather than one sample repeated (8 steps baked).
//   RESONANCE  a short swell of the ladder's own chord; for both mirrors a fuller one with a soft
//              collision under it.
//   PAYOUT     a warm, quick three-note bloom with a high note left ringing - premium, never a coin;
//              the triple gets the octave and a longer tail. A payout "Terslik" turned into a cost
//              gets a dull falling pluck instead.
//   LAND       the seed reaching the score: a tick and one small warm note.
// Every layer is brought to a stated peak before it is mixed, and the whole normalised, so nothing
// can clip; the levels are set against the game's own effects in SoundFx.
// EXTENSION POINT: a new cue is a SymmetryCue and a case in Build.

using System;

namespace ProjectBlock.View
{
    /// <summary>The audio hooks of the symmetry payout (SymmetryRewardView raises them).</summary>
    public enum SymmetryCue
    {
        Detect,
        Trace,
        Lock,
        Resonance,
        ResonanceDouble,
        Payout,
        PayoutBig,
        PayoutInverted,
        Land
    }

    public static class SymmetrySound
    {
        public const int Rate = 44100;

        /// <summary>The ladder: C6 pentatonic, two octaves' worth of steps.</summary>
        private static readonly int[] Ladder = { 0, 2, 4, 7, 9, 12, 14, 16 };
        private const float Root = 1046.5f;

        public const int LockSteps = 8;

        public static float LadderHz(int step)
        {
            int s = Ladder[Math.Max(0, Math.Min(Ladder.Length - 1, step))];
            return Root * (float)Math.Pow(2.0, s / 12.0);
        }

        public static float[] Build(SymmetryCue cue, int step)
        {
            var rng = new Random(61000 + (int)cue * 131 + step * 17);
            float[] b;
            float[] l;
            switch (cue)
            {
                case SymmetryCue.Detect:
                    b = Buf(0.16f);
                    l = Buf(0.16f);
                    Click(l, 0f, 2600f, 2300f, 14f, 0.012f, rng);
                    Mix(b, l, 0.22f);
                    l = Buf(0.16f);
                    Noise(l, 0.004f, 0.09f, 1800f, 6000f, 4000f, u => Bell(u) * (1f - 0.5f * u), rng);
                    Mix(b, l, 0.12f);
                    HighPass(b, 400f);
                    return Finish(b, 0.3f);
                case SymmetryCue.Trace:
                {
                    // "şiink": a pen of light
                    b = Buf(0.12f);
                    l = Buf(0.12f);
                    SweepNoise(l, 0f, 0.075f, 2500f, u => 3500f + 5500f * u, u => Bell(u) * u, rng);
                    Mix(b, l, 0.2f);
                    l = Buf(0.12f);
                    float f = LadderHz(step) * 2f;
                    Glide(l, 0.01f, 0.07f, f * 0.94f, f, u => Bell(u) * 0.8f);
                    Mix(b, l, 0.06f);
                    HighPass(b, 900f);
                    return Finish(b, 0.26f);
                }
                case SymmetryCue.Lock:
                {
                    // "dış-şınk"
                    float f = LadderHz(step);
                    b = Buf(0.3f);
                    // the hard little transient
                    l = Buf(0.3f);
                    Click(l, 0f, 5200f, 4600f, 16f, 0.004f, rng);
                    Mix(b, l, 0.2f);
                    // the hiss, cut short ("ş")
                    l = Buf(0.3f);
                    Noise(l, 0.002f, 0.042f, 3200f, 10000f, 7500f, u => (1f - u) * (1f - u), rng);
                    Mix(b, l, 0.34f);
                    // the bright pluck ("ınk"): harmonic partials, a tiny pitch blip at the strike
                    l = Buf(0.3f);
                    Pluck(l, 0.006f, f, new[] { 1f, 0.42f, 0.18f, 0.07f }, 0.17f, 0.018f);
                    Mix(b, l, 0.55f);
                    // the body an octave down, quiet
                    l = Buf(0.3f);
                    Pluck(l, 0.006f, f * 0.5f, new[] { 1f, 0.2f }, 0.09f, 0f);
                    Mix(b, l, 0.12f);
                    HighPass(b, 180f);
                    Room(b, 0.1f, 0.35f);
                    return Finish(b, 0.5f);
                }
                case SymmetryCue.Resonance:
                case SymmetryCue.ResonanceDouble:
                {
                    bool twice = cue == SymmetryCue.ResonanceDouble;
                    float len = twice ? 0.6f : 0.45f;
                    b = Buf(len);
                    l = Buf(len);
                    float[] chord = twice ? new[] { 1f, 1.25f, 1.5f, 2f } : new[] { 1f, 1.5f, 2f };
                    for (int i = 0; i < chord.Length; i++)
                    {
                        float hz = Root * 0.5f * chord[i];
                        float delay = i * 0.012f;
                        Swell(l, delay, len - delay, hz, twice ? 0.07f : 0.05f, twice ? 0.32f : 0.24f, 1f - 0.15f * i);
                    }
                    Mix(b, l, 0.42f);
                    l = Buf(len);
                    SweepNoise(l, 0f, 0.3f, 3000f, u => 6000f + 3000f * u, u => Bell(u) * Bell(u), rng);
                    Mix(b, l, 0.06f);
                    if (twice)
                    {
                        // the collision: a soft round body, no boom
                        l = Buf(len);
                        Glide(l, 0.06f, 0.16f, 420f, 300f, u => (1f - u) * (1f - u));
                        Mix(b, l, 0.16f);
                        l = Buf(len);
                        Click(l, 0.06f, 3400f, 3000f, 12f, 0.008f, rng);
                        Mix(b, l, 0.12f);
                    }
                    HighPass(b, 160f);
                    Room(b, 0.16f, 0.6f);
                    return Finish(b, twice ? 0.5f : 0.42f);
                }
                case SymmetryCue.Payout:
                case SymmetryCue.PayoutBig:
                {
                    bool big = cue == SymmetryCue.PayoutBig;
                    float len = big ? 0.95f : 0.7f;
                    b = Buf(len);
                    l = Buf(len);
                    // a quick warm bloom: G5 C6 E6 (and G6 for the triple), then a high C left ringing
                    float[] notes = big ? new[] { 783.99f, 1046.5f, 1318.5f, 1568f } : new[] { 783.99f, 1046.5f, 1318.5f };
                    for (int i = 0; i < notes.Length; i++)
                    {
                        Pluck(l, i * 0.026f, notes[i], new[] { 1f, 0.3f, 0.08f }, big ? 0.32f : 0.26f, 0f);
                    }
                    Mix(b, l, 0.45f);
                    l = Buf(len);
                    Pluck(l, notes.Length * 0.026f + 0.01f, 2093f, new[] { 1f, 0.2f }, big ? 0.55f : 0.42f, 0f);
                    Mix(b, l, 0.22f);
                    // the warmth under it: the root an octave down, swelling
                    l = Buf(len);
                    Swell(l, 0f, len * 0.8f, 523.25f, 0.03f, big ? 0.4f : 0.3f, 1f);
                    Mix(b, l, 0.14f);
                    // a breath of shimmer, not a shower
                    l = Buf(len);
                    SweepNoise(l, 0.02f, 0.35f, 5000f, u => 9000f, u => (1f - u) * (1f - u) * Math.Min(1f, u * 8f), rng);
                    Mix(b, l, 0.05f);
                    HighPass(b, 200f);
                    Room(b, 0.14f, 0.55f);
                    return Finish(b, big ? 0.58f : 0.5f);
                }
                case SymmetryCue.PayoutInverted:
                    b = Buf(0.5f);
                    l = Buf(0.5f);
                    Pluck(l, 0f, 392f, new[] { 1f, 0.25f }, 0.2f, 0f);
                    Pluck(l, 0.07f, 329.6f, new[] { 1f, 0.25f }, 0.26f, 0f);
                    LowPass(l, 2200f);
                    Mix(b, l, 0.4f);
                    HighPass(b, 120f);
                    Room(b, 0.1f, 0.5f);
                    return Finish(b, 0.4f);
                case SymmetryCue.Land:
                    b = Buf(0.3f);
                    l = Buf(0.3f);
                    Click(l, 0f, 3000f, 2700f, 12f, 0.006f, rng);
                    Mix(b, l, 0.18f);
                    l = Buf(0.3f);
                    Pluck(l, 0.004f, 2093f, new[] { 1f, 0.25f }, 0.16f, 0f);
                    Mix(b, l, 0.3f);
                    HighPass(b, 300f);
                    Room(b, 0.08f, 0.35f);
                    return Finish(b, 0.36f);
                default:
                    return null;
            }
        }

        // ================================================================== primitives

        private static float[] Buf(float seconds)
        {
            return new float[(int)(Rate * seconds)];
        }

        private static float OnePole(float hz)
        {
            return 1f - (float)Math.Exp(-2.0 * Math.PI * hz / Rate);
        }

        private static float Bell(float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return (float)Math.Sin(t * Math.PI);
        }

        /// <summary>A struck string of harmonic partials (amplitudes given), decaying at
        /// <paramref name="tau"/> (the higher partials faster), with a small upward pitch blip at
        /// the strike that settles in <paramref name="blip"/> seconds.</summary>
        private static void Pluck(float[] b, float at, float hz, float[] partials, float tau, float blip)
        {
            int start = (int)(at * Rate);
            int n = Math.Min(b.Length - start, (int)(tau * 7f * Rate));
            for (int p = 0; p < partials.Length; p++)
            {
                float f0 = hz * (p + 1);
                if (f0 > Rate * 0.45f)
                {
                    continue;
                }
                float decay = tau / (1f + 0.6f * p);
                double phase = 0.0;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float bend = blip > 0f ? 1f + 0.02f * (float)Math.Exp(-t / blip) : 1f;
                    phase += 2.0 * Math.PI * f0 * bend / Rate;
                    float attack = Math.Min(1f, t / 0.0015f);
                    float close = Math.Min(1f, (n - i) / (0.004f * Rate));
                    b[start + i] += (float)Math.Sin(phase) * partials[p] * attack * (float)Math.Exp(-t / decay) * close;
                }
            }
        }

        /// <summary>A sine that swells in over <paramref name="attack"/> and fades over the rest.</summary>
        private static void Swell(float[] b, float at, float dur, float hz, float attack, float tau, float amp)
        {
            int start = (int)(at * Rate);
            int n = Math.Min(b.Length - start, (int)(dur * Rate));
            double w = 2.0 * Math.PI * hz / Rate;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = t < attack ? t / attack : (float)Math.Exp(-(t - attack) / tau);
                float close = Math.Min(1f, (n - i) / (0.01f * Rate));
                b[start + i] += (float)Math.Sin(w * i) * env * amp * close;
            }
        }

        /// <summary>A sine gliding f0 -> f1 under an envelope over 0..1.</summary>
        private static void Glide(float[] b, float at, float dur, float f0, float f1, Func<float, float> env)
        {
            int start = (int)(at * Rate);
            int n = Math.Min(b.Length - start, (int)(dur * Rate));
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)Math.Max(1, n);
                phase += 2.0 * Math.PI * (f0 + (f1 - f0) * u) / Rate;
                b[start + i] += (float)Math.Sin(phase) * env(u);
            }
        }

        private static void Noise(float[] b, float at, float dur, float lowHz, float high0, float high1, Func<float, float> env, Random rng)
        {
            SweepNoise(b, at, dur, lowHz, u => high0 + (high1 - high0) * u, env, rng);
        }

        private static void SweepNoise(float[] b, float at, float dur, float lowHz, Func<float, float> high, Func<float, float> env, Random rng)
        {
            int start = (int)(at * Rate);
            int n = Math.Min(b.Length - start, (int)(dur * Rate));
            float hp = 0f, l1 = 0f, l2 = 0f;
            float hpA = OnePole(lowHz);
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)Math.Max(1, n);
                float x = (float)rng.NextDouble() * 2f - 1f;
                hp += (x - hp) * hpA;
                float a = OnePole(high(u));
                l1 += (x - hp - l1) * a;
                l2 += (l1 - l2) * a;
                b[start + i] += l2 * env(u);
            }
        }

        /// <summary>A burst of noise into a resonance gliding f0 -> f1: a tick, a glassy click.</summary>
        private static void Click(float[] b, float at, float f0, float f1, float q, float ring, Random rng)
        {
            int start = (int)(at * Rate);
            int n = Math.Min(b.Length - start, (int)((ring * 6f + 0.004f) * Rate));
            int burst = (int)(0.0012f * Rate);
            float b0 = 0f, b2 = 0f, a1 = 0f, a2 = 0f, x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)Math.Max(1, n);
                if ((i & 31) == 0)
                {
                    double w = 2.0 * Math.PI * (f0 + (f1 - f0) * u) / Rate;
                    double alpha = Math.Sin(w) / (2.0 * q);
                    double a0 = 1.0 + alpha;
                    b0 = (float)(alpha / a0);
                    b2 = -b0;
                    a1 = (float)(-2.0 * Math.Cos(w) / a0);
                    a2 = (float)((1.0 - alpha) / a0);
                }
                float x = i < burst ? ((float)rng.NextDouble() * 2f - 1f) * (1f - i / (float)burst) : 0f;
                float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                float t = i / (float)Rate;
                b[start + i] += y * 5f * (float)Math.Exp(-t / Math.Max(0.002f, ring));
            }
        }

        private static void HighPass(float[] b, float hz)
        {
            float a = OnePole(hz);
            float s1 = 0f, s2 = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                s1 += (b[i] - s1) * a;
                float h1 = b[i] - s1;
                s2 += (h1 - s2) * a;
                b[i] = h1 - s2;
            }
        }

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

        private static void Room(float[] b, float mix, float size)
        {
            int[] combs = { 1116, 1188, 1277, 1356 };
            int[] allpasses = { 556, 441 };
            var wet = new float[b.Length];
            foreach (int comb in combs)
            {
                int d = Math.Max(8, (int)(comb * size));
                var line = new float[d];
                int p = 0;
                float damp = 0f;
                for (int i = 0; i < b.Length; i++)
                {
                    float y = line[p];
                    damp += (y - damp) * 0.55f;
                    line[p] = b[i] + damp * 0.72f;
                    p = (p + 1) % d;
                    wet[i] += y * 0.25f;
                }
            }
            foreach (int ap in allpasses)
            {
                int d = Math.Max(4, (int)(ap * size));
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

        private static float Peak(float[] b)
        {
            float m = 0f;
            foreach (float x in b)
            {
                float a = x < 0f ? -x : x;
                if (a > m)
                {
                    m = a;
                }
            }
            return m;
        }

        /// <summary>The layer at <paramref name="peak"/> on its own, then added: gain staging.</summary>
        private static void Mix(float[] dst, float[] layer, float peak)
        {
            float m = Peak(layer);
            if (m < 1e-6f)
            {
                return;
            }
            float g = peak / m;
            int n = Math.Min(dst.Length, layer.Length);
            for (int i = 0; i < n; i++)
            {
                dst[i] += layer[i] * g;
            }
        }

        private static float[] Finish(float[] b, float peak)
        {
            float m = Peak(b);
            float g = m > 1e-6f ? peak / m : 1f;
            int fade = Math.Min(b.Length, (int)(0.006f * Rate));
            for (int i = 0; i < b.Length; i++)
            {
                float f = i >= b.Length - fade ? (b.Length - i) / (float)fade : 1f;
                b[i] *= g * f;
            }
            return b;
        }
    }
}
