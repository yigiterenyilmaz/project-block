// PURPOSE: "Rüzgar"'s sound, synthesized and baked once (SoundFx.Wind plays it). Plain C# on float
// arrays - no UnityEngine - so every cue can be rendered and MEASURED outside the editor.
//
// THE FAMILY: AIR. Not a stock wind loop and not a whoosh sample: short designed events made of
// shaped noise, with nothing tonal in the wind itself (the brief's "not a musical theremin").
//   BED          the aim's breath: low, quiet, seamless. Its level and a few cents of pitch follow
//                how far the stroke has been drawn - felt, never heard as a note.
//   ORIGIN LOCK  "fup": a small soft pop of pressure where the gust is pinned.
//   CAST LOCK    the same, deeper, with the air being drawn IN under it.
//   RELEASE      "fwsshh", in three lengths (the corridor's own travel time picks one): a low soft
//                body, a mid hiss that opens and closes, and a faint flutter on top. It is the
//                storm - there is no separate loop running under it.
//   TAIL         the air letting go at the end.
//   CANCEL       a small release; REFUSED a blocked "pff".
// The materials are layers ON the wind, each only where its thing happens:
//   FIRE   "fwoof" as the front takes the flame, a crackle as scraps tear off, a light hiss while
//          they fly, and the catch as one phrase: tick -> fsshh -> fwoom.
//   WATER  air pressure and a soft slap, a short watery hiss, a small wet "plup".
//   SPORES a small organic "tchk", a whisper, a soft wet "tk", a low pulse as the seed grows.
//          Stylized: nothing here is meant to be unpleasant.
// Every layer is brought to a stated peak before it is mixed and the whole is limited, so nothing
// can clip; the wind's own cues are mixed 6-10 dB under the material ones in SoundFx.
// EXTENSION POINT: a new cue is a WindCue and a case in Build.

using System;

namespace ProjectBlock.View
{
    /// <summary>The audio hooks of the wind (SoundFx.Wind plays them).</summary>
    public enum WindCue
    {
        OriginLock,
        Cancel,
        Refused,
        CastLock,
        Release,
        Tail,
        FireContact,
        EmberTear,
        EmberHiss,
        Ignite,
        WaterContact,
        WaterHiss,
        WaterArrive,
        SporeTear,
        SporeWhisper,
        SporeImpact,
        SporeGrow
    }

    /// <summary>The haptic beats. Announced; the game has no haptics layer to send them to yet.</summary>
    public enum WindHaptic
    {
        Origin,
        Cast,
        Ignition,
        WaterArrival,
        Seed
    }

    /// <summary>Named moments, so the lab can run a cast up to one and stop after it.</summary>
    public enum WindBeat
    {
        FireLean,
        FireTear,
        FireTravel,
        FireHotSpot,
        FireIgnite,
        FireConversion,
        WaterDeform,
        WaterDetach,
        WaterDrag,
        WaterArrival,
        SporeContact,
        SporeTear,
        SporeTravel,
        SporeContaminate,
        SporeSeed,
        SporeSource
    }

    public static class WindSound
    {
        public const int Rate = 44100;

        /// <summary>How many lengths of release are baked: short, middle, long.</summary>
        public const int ReleaseVariants = 3;

        /// <summary>The aim's bed: two seconds, the end folded into the start so it loops clean.</summary>
        public static float[] BuildBed()
        {
            var rng = new Random(77001);
            int n = (int)(Rate * 2.0f);
            int fold = (int)(Rate * 0.35f);
            float[] raw = Noise(rng, n + fold);
            float[] low = BandPass(raw, 190f, 0.9f);
            float[] mid = BandPass(raw, 620f, 1.1f);
            var b = new float[n + fold];
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                // slow, uneven breathing - two rates that never line up
                float swell = 0.72f + 0.18f * (float)Math.Sin(t * 1.7f) + 0.1f * (float)Math.Sin(t * 2.9f + 1.3f);
                b[i] = (low[i] * 1f + mid[i] * 0.45f) * swell;
            }
            var loop = new float[n];
            for (int i = 0; i < n; i++)
            {
                loop[i] = b[i];
            }
            for (int i = 0; i < fold; i++)
            {
                float k = i / (float)fold;
                // equal-power crossfade of the tail into the head
                float a = (float)Math.Sin(k * Math.PI * 0.5);
                float c = (float)Math.Cos(k * Math.PI * 0.5);
                loop[i] = loop[i] * a + b[n + i] * c;
            }
            Normalize(loop, 0.6f);
            return loop;
        }

        public static float[] Build(WindCue cue, int variant)
        {
            var rng = new Random(77100 + (int)cue * 97 + variant * 13);
            float[] b;
            switch (cue)
            {
                case WindCue.OriginLock:
                    b = Buf(0.16f);
                    Mix(b, Burst(rng, 0.14f, 520f, 0.8f, 0.004f, 0.05f), 0f, 0.8f);
                    Mix(b, Thump(0.12f, 120f, 70f, 0.05f), 0f, 0.7f);
                    break;
                case WindCue.CastLock:
                    b = Buf(0.22f);
                    // the air drawn in: a swell that is cut where the pop lands
                    Mix(b, Swell(rng, 0.13f, 420f, 1.0f), 0f, 0.55f);
                    Mix(b, Burst(rng, 0.1f, 380f, 0.8f, 0.004f, 0.045f), 0.11f, 0.85f);
                    Mix(b, Thump(0.11f, 95f, 55f, 0.06f), 0.11f, 0.9f);
                    break;
                case WindCue.Release:
                    b = Release(rng, variant);
                    break;
                case WindCue.Tail:
                    b = Buf(0.42f);
                    Mix(b, Decay(rng, 0.42f, 520f, 0.8f, 0.02f, 0.16f), 0f, 0.8f);
                    Mix(b, Decay(rng, 0.3f, 1900f, 1.2f, 0.03f, 0.09f), 0f, 0.3f);
                    break;
                case WindCue.Cancel:
                    b = Buf(0.2f);
                    Mix(b, Sweep(rng, 0.18f, 1700f, 500f, 1.3f, 0.006f, 0.06f), 0f, 0.8f);
                    break;
                case WindCue.Refused:
                    b = Buf(0.16f);
                    // blocked: it rises and is stopped short
                    Mix(b, Stopped(rng, 0.09f, 900f, 1.0f), 0f, 0.8f);
                    Mix(b, Thump(0.08f, 150f, 90f, 0.03f), 0.07f, 0.5f);
                    break;
                case WindCue.FireContact:
                    b = Buf(0.26f);
                    Mix(b, Swell(rng, 0.2f, 340f, 0.8f), 0f, 0.9f);
                    Mix(b, Decay(rng, 0.16f, 1500f, 0.9f, 0.03f, 0.05f), 0.05f, 0.35f);
                    break;
                case WindCue.EmberTear:
                    b = Buf(0.14f);
                    Mix(b, Crackle(rng, 0.12f, 6, 2600f, 5200f), 0f, 0.8f);
                    break;
                case WindCue.EmberHiss:
                    b = Buf(0.32f);
                    Mix(b, Decay(rng, 0.32f, 5600f, 1.6f, 0.04f, 0.11f), 0f, 0.7f);
                    Flutter(b, 31f, 0.35f);
                    break;
                case WindCue.Ignite:
                    b = Buf(0.52f);
                    // tick -> fsshh -> fwoom
                    Mix(b, Grain(rng, 0.006f, 3200f, 3f), 0f, 0.8f);
                    Mix(b, Sweep(rng, 0.2f, 2200f, 5200f, 1.4f, 0.05f, 0.06f), 0.05f, 0.5f);
                    Mix(b, Swell(rng, 0.26f, 260f, 0.8f), 0.2f, 0.9f);
                    Mix(b, Thump(0.2f, 105f, 62f, 0.09f), 0.24f, 0.7f);
                    break;
                case WindCue.WaterContact:
                    b = Buf(0.18f);
                    Mix(b, Burst(rng, 0.12f, 430f, 0.8f, 0.006f, 0.05f), 0f, 0.6f);
                    Mix(b, Blip(0.07f, 320f, 190f, 0.03f), 0.02f, 0.7f);
                    Mix(b, Grain(rng, 0.03f, 1400f, 2.2f), 0.02f, 0.3f);
                    break;
                case WindCue.WaterHiss:
                    b = Buf(0.32f);
                    Mix(b, Decay(rng, 0.32f, 2500f, 1.5f, 0.03f, 0.12f), 0f, 0.7f);
                    Bubble(b, rng, 46f, 0.5f);
                    break;
                case WindCue.WaterArrive:
                    b = Buf(0.22f);
                    // "plup": a falling drop, the small rising answer behind it, a breath of spray
                    Mix(b, Blip(0.075f, 430f, 235f, 0.028f), 0f, 0.9f);
                    Mix(b, Blip(0.05f, 610f, 900f, 0.016f), 0.045f, 0.4f);
                    Mix(b, Decay(rng, 0.12f, 3400f, 1.6f, 0.004f, 0.03f), 0.01f, 0.22f);
                    break;
                case WindCue.SporeTear:
                    b = Buf(0.12f);
                    // "tchk": two soft clicks a hair apart and a wet give under them
                    Mix(b, Grain(rng, 0.014f, 1250f, 2.6f), 0f, 0.7f);
                    Mix(b, Grain(rng, 0.018f, 880f, 2.4f), 0.022f, 0.8f);
                    Mix(b, Burst(rng, 0.07f, 640f, 2.2f, 0.004f, 0.022f), 0.02f, 0.4f);
                    break;
                case WindCue.SporeWhisper:
                    b = Buf(0.36f);
                    Mix(b, Decay(rng, 0.36f, 3300f, 1.3f, 0.07f, 0.13f), 0f, 0.6f);
                    Flutter(b, 13f, 0.45f);
                    break;
                case WindCue.SporeImpact:
                    b = Buf(0.1f);
                    // "tk": soft and wet, never sharp
                    Mix(b, Grain(rng, 0.022f, 940f, 2.2f), 0f, 0.8f);
                    Mix(b, Thump(0.06f, 210f, 150f, 0.022f), 0f, 0.6f);
                    break;
                case WindCue.SporeGrow:
                    b = Buf(0.3f);
                    Mix(b, Pulse(0.28f, 150f, 196f), 0f, 0.9f);
                    Mix(b, Decay(rng, 0.2f, 700f, 1.4f, 0.06f, 0.07f), 0.02f, 0.18f);
                    break;
                default:
                    return null;
            }
            Declick(b);
            Limit(b, 0.72f);
            return b;
        }

        /// <summary>How long a release of this variant lasts - the caller picks the one nearest
        /// the corridor's own travel time.</summary>
        public static float ReleaseSeconds(int variant)
        {
            return variant <= 0 ? 0.55f : variant == 1 ? 0.85f : 1.15f;
        }

        private static float[] Release(Random rng, int variant)
        {
            float dur = ReleaseSeconds(variant);
            float[] b = Buf(dur);
            int n = b.Length;
            float[] raw = Noise(rng, n);
            float[] low = BandPass(raw, 300f, 0.8f);
            float[] high = BandPass(Noise(rng, n), 7200f, 1.8f);
            // the mid hiss opens as the front leaves and closes as it arrives: a moving filter
            float[] mid = SweptBand(Noise(rng, n), 1100f, 2800f, 1500f, 1.2f);
            // each layer to its own level BEFORE they are mixed: a band-pass passes far more of a
            // high band than a low one, and mixed raw the body was a tenth of the hiss
            Normalize(low, 1f);
            Normalize(mid, 0.5f);
            Normalize(high, 0.14f);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                // a sharp onset (the release), a body that lasts the crossing, a soft end
                float onset = Math.Min(1f, i / (Rate * 0.012f));
                float body = (float)Math.Pow(Math.Sin(Math.Min(1.0, k * 1.08) * Math.PI), 0.55);
                float env = onset * (0.35f * (float)Math.Exp(-k * 9.0) + body);
                float flutter = 0.6f + 0.4f * (float)Math.Sin(i / (float)Rate * 2.0 * Math.PI * 23.0 + 0.7);
                b[i] = (low[i] + mid[i] + high[i] * flutter) * env;
            }
            return b;
        }

        // ------------------------------------------------------------------ voices

        /// <summary>Band noise with an attack and an exponential decay.</summary>
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

        private static float[] Decay(Random rng, float dur, float hz, float q, float attack, float tau)
        {
            return Burst(rng, dur, hz, q, attack, tau);
        }

        /// <summary>Band noise that swells and lets go - pressure building.</summary>
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

        /// <summary>Band noise that rises and is cut dead: something in its way.</summary>
        private static float[] Stopped(Random rng, float dur, float hz, float q)
        {
            float[] b = BandPass(Noise(rng, (int)(Rate * dur)), hz, q);
            for (int i = 0; i < b.Length; i++)
            {
                float k = i / (float)b.Length;
                b[i] *= k * k * (k > 0.9f ? (1f - k) / 0.1f : 1f);
            }
            return b;
        }

        /// <summary>Noise through a band that glides from one frequency to another.</summary>
        private static float[] Sweep(Random rng, float dur, float fromHz, float toHz, float q, float attack, float tau)
        {
            int n = (int)(Rate * dur);
            float[] b = SweptBand(Noise(rng, n), fromHz, toHz, toHz, q);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                b[i] *= Math.Min(1f, t / attack) * (float)Math.Exp(-t / tau);
            }
            return b;
        }

        /// <summary>One short grain of band noise - a click with a colour.</summary>
        private static float[] Grain(Random rng, float dur, float hz, float q)
        {
            float[] b = BandPass(Noise(rng, (int)(Rate * dur)), hz, q);
            for (int i = 0; i < b.Length; i++)
            {
                float k = i / (float)b.Length;
                b[i] *= (float)Math.Sin(k * Math.PI);
            }
            return b;
        }

        /// <summary>A handful of grains at uneven moments: scraps tearing off.</summary>
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

        /// <summary>A low sine that drops - the body under a pop.</summary>
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
                b[i] = (float)Math.Sin(phase) * Math.Min(1f, t / 0.003f) * (float)Math.Exp(-t / tau);
            }
            return b;
        }

        /// <summary>A small pitched drop of liquid: a sine that glides, soft at both ends.</summary>
        private static float[] Blip(float dur, float fromHz, float toHz, float tau)
        {
            float[] b = Buf(dur);
            double phase = 0.0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float k = i / (float)b.Length;
                float hz = fromHz * (float)Math.Pow(toHz / fromHz, k);
                phase += 2.0 * Math.PI * hz / Rate;
                b[i] = (float)Math.Sin(phase) * Math.Min(1f, t / 0.004f) * (float)Math.Exp(-t / tau)
                    * (1f - k * k * k);
            }
            return b;
        }

        /// <summary>A low soft swell with a second partial: something small and alive growing.</summary>
        private static float[] Pulse(float dur, float fromHz, float toHz)
        {
            float[] b = Buf(dur);
            double phase = 0.0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float k = i / (float)b.Length;
                float hz = fromHz + (toHz - fromHz) * k + 3f * (float)Math.Sin(t * 2.0 * Math.PI * 9.0);
                phase += 2.0 * Math.PI * hz / Rate;
                float env = (float)Math.Pow(Math.Sin(k * Math.PI), 0.8);
                b[i] = ((float)Math.Sin(phase) + 0.22f * (float)Math.Sin(phase * 2.0)) * env;
            }
            return b;
        }

        /// <summary>A regular tremble laid on a sound.</summary>
        private static void Flutter(float[] b, float hz, float depth)
        {
            for (int i = 0; i < b.Length; i++)
            {
                b[i] *= 1f - depth * 0.5f * (1f + (float)Math.Sin(i / (float)Rate * 2.0 * Math.PI * hz));
            }
        }

        /// <summary>An uneven tremble: the level is held and re-drawn at about this rate.</summary>
        private static void Bubble(float[] b, Random rng, float hz, float depth)
        {
            int hold = Math.Max(1, (int)(Rate / hz));
            float level = 1f;
            float next = 1f;
            for (int i = 0; i < b.Length; i++)
            {
                if (i % hold == 0)
                {
                    level = next;
                    next = 1f - depth * (float)rng.NextDouble();
                }
                float k = (i % hold) / (float)hold;
                b[i] *= level + (next - level) * k;
            }
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
            return SweptBand(input, hz, hz, hz, q);
        }

        /// <summary>The same band, its centre moving from a to b by the middle and on to c.</summary>
        private static float[] SweptBand(float[] input, float aHz, float bHz, float cHz, float q)
        {
            var output = new float[input.Length];
            float low = 0f;
            float band = 0f;
            float damp = 1f / Math.Max(0.3f, q);
            for (int i = 0; i < input.Length; i++)
            {
                float k = i / (float)Math.Max(1, input.Length - 1);
                float hz = k < 0.5f ? aHz + (bHz - aHz) * (k / 0.5f) : bHz + (cHz - bHz) * ((k - 0.5f) / 0.5f);
                float f = 2f * (float)Math.Sin(Math.PI * Math.Min(hz, Rate * 0.22f) / Rate);
                low += f * band;
                float high = input[i] - low - damp * band;
                band += f * high;
                output[i] = band;
            }
            return output;
        }

        /// <summary>Adds a layer at a time, first brought to <paramref name="peak"/>.</summary>
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

        /// <summary>Brings the whole down to a peak if it is over it (never up).</summary>
        private static void Limit(float[] b, float peak)
        {
            float max = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                max = Math.Max(max, Math.Abs(b[i]));
            }
            if (max > peak)
            {
                float gain = peak / max;
                for (int i = 0; i < b.Length; i++)
                {
                    b[i] *= gain;
                }
            }
        }

        /// <summary>A few milliseconds of fade at each end, so no cue starts or stops on a step.</summary>
        private static void Declick(float[] b)
        {
            int n = Math.Min(b.Length / 2, (int)(Rate * 0.004f));
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                b[i] *= k;
                b[b.Length - 1 - i] *= k;
            }
        }
    }
}
