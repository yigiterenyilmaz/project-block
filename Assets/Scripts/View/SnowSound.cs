// PURPOSE: SNOW and "Çığ" HEARD - every cue synthesized in pure C# (no UnityEngine), so it can be
// rendered and measured outside the editor; SoundFx.Snow bakes and plays them.
//
// THE MATERIAL IS POWDER, NOT WATER AND NOT ROCK: soft, dense, dry, cold. Everything is band noise
// in the low mids with the top rolled off, plus a soft low body where something has weight - never
// a sine "boing", never a bass boom. The avalanche is HEAVY without being an explosion: its row
// impacts are a dull thump with a crush of grains over it ("THUM-CRSH"), its release a dense
// "WHUFF", its settle a soft "thoom". The two crystalline sounds (a power tick, a refresh's "tik")
// are short, high and quiet - pressure, not magic.
//
// EXTENSION POINT: a new cue is a SnowCue, a recipe here and a level in SoundFx.Snow.

using System;

namespace ProjectBlock.View
{
    /// <summary>Every snow cue.</summary>
    public enum SnowCue
    {
        Move = 0,
        Arrive = 1,
        Merge = 2,
        PowerTick = 3,
        Refresh = 4,
        Melt = 5,
        AimBlocked = 6,
        Spent = 7,
        PressureRumble = 8,
        Release = 9,
        RowImpact = 10,
        Settle = 11,
        Reward = 12,
        AimPulse = 13,
        Age = 14
    }

    /// <summary>The recipes. Each call renders one variant of one cue at 44.1 kHz, mono.</summary>
    public static class SnowSound
    {
        public const int Rate = 44100;

        /// <summary>Takes baked per cue, so a run of the same cue never repeats one sample.</summary>
        public const int Variants = 3;

        public static float[] Build(SnowCue cue, int variant)
        {
            var rng = new Random(91000 + (int)cue * 131 + variant * 17);
            float v = 1f + (variant - 1) * 0.06f; // a few percent of colour between the takes
            float[] b;
            switch (cue)
            {
                case SnowCue.Move:
                    // "ffsh": powder sliding, a tiny compact thud as it stops
                    b = Buf(0.24f);
                    Mix(b, Swell(rng, 0.2f, 900f * v, 0.7f), 0f, 0.55f);
                    Mix(b, Thump(0.08f, 150f * v, 95f, 0.03f), 0.17f, 0.35f);
                    break;
                case SnowCue.Arrive:
                    b = Buf(0.14f);
                    Mix(b, Burst(rng, 0.12f, 1300f * v, 0.8f, 0.002f, 0.03f), 0f, 0.5f);
                    Mix(b, Thump(0.1f, 160f, 100f, 0.035f), 0f, 0.4f);
                    break;
                case SnowCue.Merge:
                    // "whuff": dense compression - a swell cut by a soft low body
                    b = Buf(0.34f);
                    Mix(b, Swell(rng, 0.16f, 520f * v, 0.8f), 0f, 0.45f);
                    Mix(b, Burst(rng, 0.2f, 700f * v, 0.7f, 0.004f, 0.07f), 0.12f, 0.75f);
                    Mix(b, Thump(0.18f, 120f * v, 70f, 0.06f), 0.12f, 0.6f);
                    Mix(b, Crackle(rng, 0.14f, 7, 2200f, 4200f), 0.13f, 0.18f);
                    break;
                case SnowCue.PowerTick:
                    // a cold crystalline pressure tick
                    b = Buf(0.12f);
                    Mix(b, Grain(rng, 0.008f, 5200f * v, 6f), 0f, 0.5f);
                    Mix(b, Ring(0.1f, 2600f * v, 0.025f), 0.002f, 0.22f);
                    break;
                case SnowCue.Refresh:
                    // a small "whuff" and a soft "tik"
                    b = Buf(0.3f);
                    Mix(b, Burst(rng, 0.18f, 800f * v, 0.7f, 0.006f, 0.06f), 0f, 0.5f);
                    Mix(b, Ring(0.16f, 3400f * v, 0.04f), 0.09f, 0.18f);
                    Mix(b, Grain(rng, 0.006f, 6000f, 6f), 0.09f, 0.3f);
                    break;
                case SnowCue.Melt:
                    // soft wet collapse and a tiny hiss - no cartoon drip
                    b = Buf(0.5f);
                    Mix(b, Burst(rng, 0.22f, 600f * v, 0.7f, 0.01f, 0.08f), 0f, 0.45f);
                    Mix(b, Swell(rng, 0.42f, 3600f * v, 0.9f), 0.06f, 0.16f);
                    Mix(b, Thump(0.12f, 110f, 80f, 0.05f), 0f, 0.22f);
                    break;
                case SnowCue.AimBlocked:
                    // "ff": a puff that goes nowhere
                    b = Buf(0.14f);
                    Mix(b, Burst(rng, 0.12f, 700f * v, 0.8f, 0.003f, 0.035f), 0f, 0.45f);
                    break;
                case SnowCue.Spent:
                    // "ffk": dry snow refusing - a puff stopped short
                    b = Buf(0.16f);
                    Mix(b, Burst(rng, 0.09f, 900f * v, 0.8f, 0.003f, 0.03f), 0f, 0.42f);
                    Mix(b, Grain(rng, 0.01f, 2600f, 3f), 0.08f, 0.35f);
                    break;
                case SnowCue.PressureRumble:
                    // the anticipation: a low snow rumble, a compressed-powder crackle, a cold hiss
                    b = Buf(0.36f);
                    Mix(b, Swell(rng, 0.34f, 140f * v, 0.9f), 0f, 0.7f);
                    Mix(b, Crackle(rng, 0.3f, 14, 1600f, 3600f), 0.03f, 0.22f);
                    Mix(b, Swell(rng, 0.3f, 4200f, 1.0f), 0.05f, 0.1f);
                    break;
                case SnowCue.Release:
                    // "WHUFF" - dense, not a boom
                    b = Buf(0.42f);
                    Mix(b, Burst(rng, 0.36f, 420f * v, 0.7f, 0.004f, 0.11f), 0f, 0.85f);
                    Mix(b, Thump(0.3f, 105f * v, 55f, 0.09f), 0f, 0.7f);
                    Mix(b, Burst(rng, 0.25f, 1500f, 0.8f, 0.005f, 0.06f), 0.01f, 0.28f);
                    break;
                case SnowCue.RowImpact:
                    // "THUM-CRSH": a low snow thump, the block crushed, powder bursting
                    b = Buf(0.32f);
                    Mix(b, Thump(0.22f, 115f * v, 58f, 0.07f), 0f, 0.8f);
                    Mix(b, Crackle(rng, 0.16f, 16, 900f, 2600f), 0.035f, 0.5f);
                    Mix(b, Burst(rng, 0.22f, 1100f * v, 0.7f, 0.006f, 0.06f), 0.04f, 0.42f);
                    break;
                case SnowCue.Settle:
                    // "thoom": the whole mass sitting down
                    b = Buf(0.5f);
                    Mix(b, Thump(0.42f, 90f * v, 50f, 0.13f), 0f, 0.65f);
                    Mix(b, Burst(rng, 0.3f, 520f, 0.7f, 0.01f, 0.09f), 0f, 0.35f);
                    break;
                case SnowCue.Reward:
                    // a warm premium chime: two soft partials and a breath of air - no coins
                    b = Buf(0.9f);
                    Mix(b, Ring(0.85f, 523.25f * v, 0.32f), 0f, 0.45f);
                    Mix(b, Ring(0.8f, 783.99f * v, 0.26f), 0.035f, 0.32f);
                    Mix(b, Ring(0.7f, 1046.5f * v, 0.18f), 0.07f, 0.2f);
                    Mix(b, Swell(rng, 0.4f, 5000f, 1f), 0f, 0.05f);
                    break;
                case SnowCue.AimPulse:
                    // the power card's cold pulse
                    b = Buf(0.22f);
                    Mix(b, Swell(rng, 0.2f, 2400f * v, 0.9f), 0f, 0.25f);
                    Mix(b, Ring(0.18f, 1900f * v, 0.06f), 0.02f, 0.14f);
                    break;
                case SnowCue.Age:
                    // a turn passing: barely a settle
                    b = Buf(0.12f);
                    Mix(b, Burst(rng, 0.1f, 1000f * v, 0.7f, 0.004f, 0.03f), 0f, 0.3f);
                    break;
                default:
                    return null;
            }
            Soften(b, 0.004f, 0.02f);
            Normalize(b, 0.85f);
            return b;
        }

        // ------------------------------------------------------------------ voices

        private static float[] Burst(Random rng, float dur, float hz, float q, float attack, float tau)
        {
            float[] b = BandPass(Noise(rng, (int)(Rate * dur)), hz, q);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] *= Math.Min(1f, t / attack) * (float)Math.Exp(-t / tau);
            }
            return b;
        }

        private static float[] Swell(Random rng, float dur, float hz, float q)
        {
            float[] b = BandPass(Noise(rng, (int)(Rate * dur)), hz, q);
            for (int i = 0; i < b.Length; i++)
            {
                float k = i / (float)b.Length;
                b[i] *= (float)Math.Pow(Math.Sin(k * Math.PI), 1.4);
            }
            return b;
        }

        private static float[] Grain(Random rng, float dur, float hz, float q)
        {
            float[] b = BandPass(Noise(rng, (int)(Rate * dur)), hz, q);
            for (int i = 0; i < b.Length; i++)
            {
                b[i] *= (float)Math.Sin(i / (float)b.Length * Math.PI);
            }
            return b;
        }

        private static float[] Crackle(Random rng, float dur, int count, float loHz, float hiHz)
        {
            float[] b = Buf(dur);
            for (int i = 0; i < count; i++)
            {
                float at = (float)rng.NextDouble() * (dur - 0.02f);
                float hz = loHz + (float)rng.NextDouble() * (hiHz - loHz);
                float[] g = Grain(rng, 0.004f + (float)rng.NextDouble() * 0.008f, hz, 3f);
                Normalize(g, 0.4f + (float)rng.NextDouble() * 0.6f);
                Add(b, g, at);
            }
            return b;
        }

        /// <summary>A low sine that drops - the weight under a snow impact.</summary>
        private static float[] Thump(float dur, float fromHz, float toHz, float tau)
        {
            float[] b = Buf(dur);
            double phase = 0.0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float k = i / (float)b.Length;
                float hz = fromHz + (toHz - fromHz) * k;
                phase += 2.0 * Math.PI * hz / Rate;
                b[i] = (float)Math.Sin(phase) * Math.Min(1f, t / 0.004f) * (float)Math.Exp(-t / tau);
            }
            return b;
        }

        /// <summary>A soft struck partial with a hair of a second one - a cold ring, not a bell.</summary>
        private static float[] Ring(float dur, float hz, float tau)
        {
            float[] b = Buf(dur);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                double w = 2.0 * Math.PI * hz * t;
                b[i] = ((float)Math.Sin(w) + 0.12f * (float)Math.Sin(w * 2.01)) * Math.Min(1f, t / 0.003f)
                    * (float)Math.Exp(-t / tau);
            }
            return b;
        }

        // ------------------------------------------------------------------ plumbing

        private static float[] Buf(float seconds)
        {
            return new float[Math.Max(1, (int)(Rate * seconds))];
        }

        private static float[] Noise(Random rng, int n)
        {
            var b = new float[Math.Max(1, n)];
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
            }
            return b;
        }

        /// <summary>A state-variable band-pass.</summary>
        private static float[] BandPass(float[] input, float hz, float q)
        {
            var output = new float[input.Length];
            float low = 0f;
            float band = 0f;
            float damp = 1f / Math.Max(0.3f, q);
            float f = 2f * (float)Math.Sin(Math.PI * Math.Min(hz, Rate * 0.22f) / Rate);
            for (int i = 0; i < input.Length; i++)
            {
                low += f * band;
                float high = input[i] - low - damp * band;
                band += f * high;
                output[i] = band;
            }
            return output;
        }

        private static void Mix(float[] into, float[] layer, float at, float peak)
        {
            Normalize(layer, peak);
            Add(into, layer, at);
        }

        private static void Add(float[] into, float[] layer, float at)
        {
            int offset = (int)(at * Rate);
            for (int i = 0; i < layer.Length && offset + i < into.Length; i++)
            {
                if (offset + i >= 0)
                {
                    into[offset + i] += layer[i];
                }
            }
        }

        /// <summary>Short fades at both ends, so no cue clicks.</summary>
        private static void Soften(float[] b, float inSeconds, float outSeconds)
        {
            int a = Math.Max(1, (int)(inSeconds * Rate));
            int o = Math.Max(1, (int)(outSeconds * Rate));
            for (int i = 0; i < b.Length; i++)
            {
                float g = 1f;
                if (i < a) { g *= i / (float)a; }
                if (i > b.Length - o) { g *= (b.Length - i) / (float)o; }
                b[i] *= g;
            }
        }

        private static void Normalize(float[] b, float peak)
        {
            float max = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                max = Math.Max(max, Math.Abs(b[i]));
            }
            if (max < 1e-6f)
            {
                return;
            }
            float gain = peak / max;
            for (int i = 0; i < b.Length; i++)
            {
                b[i] *= gain;
            }
        }
    }
}
