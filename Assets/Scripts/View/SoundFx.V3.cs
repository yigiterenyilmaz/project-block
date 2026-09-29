// PURPOSE: The "V3" sound set - the third take on the baseline cues, answering the first listen of
// NEW (SoundFx.Polished.cs): its explosions were too BASS HEAVY and its "ting" was annoying. V3
// keeps NEW's method (transient + body + saturation + small room, round-robin takes, pooled
// sources, the same context) and changes the two things that were wrong:
//   - WEIGHT IN THE MIDS, NOT THE SUB. The explosion's punch sits around 200-90 Hz instead of
//     155-45, it is shorter, and every hit goes through a high-pass at the end (HighPass), so no
//     layer can bring the rumble back. What reads as "big" is now crunch and density: more crack
//     grains, a wider debris band, a mid "whump" of noise.
//   - NO METALLIC RING ANYWHERE. Nothing here is built from inharmonic struck modes. Placement
//     is a dry clack (a knock of band noise, no wood ring); the sweep, chimes and fusions are a
//     soft MARIMBA - a sine with a slow-ish attack and only its octave above, dying fast - and
//     buying is a warm pop and a two-note pluck rather than a coin.
// Shared helpers (PSine, PNoise, Room, PFinish...) live in SoundFx.Polished.cs.

using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class SoundFx
    {
        private AudioClip[] v3Place;
        private AudioClip[][] v3Explode;
        private AudioClip[] v3Sweep;
        private AudioClip[] v3Bell;
        private AudioClip[] v3Shuffle;
        private AudioClip[] v3Buy;
        private AudioClip[] v3Vanish;
        private AudioClip[] v3Pickup;
        private AudioClip[] v3Reject;

        private void BuildV3()
        {
            v3Place = Variants(4, V3Place);
            v3Explode = new[]
            {
                Variants(3, s => V3Explode(0, s)),
                Variants(3, s => V3Explode(1, s)),
                Variants(3, s => V3Explode(2, s)),
            };
            v3Sweep = Variants(2, V3Sweep);
            v3Bell = new[] { V3Bell() };
            v3Shuffle = Variants(3, V3Shuffle);
            v3Buy = Variants(3, V3Buy);
            v3Vanish = Variants(3, V3Vanish);
            v3Pickup = Variants(4, V3Pickup);
            v3Reject = Variants(2, V3Reject);
        }

        /// <summary>A dry, tight CLACK: a bright contact, a short knock of band noise where a wood
        /// ring used to be, and a mid body - no tail to speak of.</summary>
        private static AudioClip V3Place(int v)
        {
            var rng = new System.Random(11000 + v);
            float j = 1f + 0.04f * (v - 1.5f);
            float[] b = Buffer(0.16f);
            PNoise(b, 0f, 0.01f, 0.5f, 0.0002f, 0.0022f, 3000f, 12000f, rng);          // contact
            PNoise(b, 0.0005f, 0.04f, 0.32f, 0.0005f, 0.011f, 700f * j, 2600f * j, rng); // knock
            PSine(b, 0f, 0.08f, 520f * j, 230f * j, 0.008f, 0.45f, 0.0006f, 0.022f);   // body
            PSine(b, 0f, 0.06f, 190f, 140f, 0.015f, 0.22f, 0.001f, 0.025f);            // weight
            Saturate(b, 1.6f);
            HighPass(b, 110f);
            Room(b, 0.05f, 0.45f);
            return PFinish("place3+" + v, b, 0.9f);
        }

        /// <summary>A line going off, weighted in the mids: crack, a mid punch, a wide crunchy
        /// debris band and a scatter of cracks - more of everything per tier, never more sub.
        /// </summary>
        private static AudioClip V3Explode(int tier, int v)
        {
            var rng = new System.Random(12000 + tier * 10 + v);
            float len = 0.36f + 0.14f * tier;
            float[] b = Buffer(len);
            float j = 1f + 0.04f * (v - 1f);
            PNoise(b, 0f, 0.03f, 0.6f, 0.0002f, 0.007f, 1800f, 12000f, rng);                // crack
            PSine(b, 0f, 0.2f, 210f * j, 95f * j, 0.02f, 0.5f, 0.001f, 0.05f + 0.012f * tier); // punch
            PNoise(b, 0.002f, 0.12f, 0.35f, 0.002f, 0.035f + 0.01f * tier, 250f, 1400f, rng); // whump
            PNoiseSweep(b, 0.004f, 0.25f + 0.1f * tier, 0.4f, 0.002f, 0.055f + 0.025f * tier,
                500f, 9000f, 2200f, rng);                                                    // debris
            int grains = 8 + 6 * tier;
            for (int g = 0; g < grains; g++)
            {
                float at = 0.008f + (float)rng.NextDouble() * (0.11f + 0.06f * tier);
                float amp = 0.12f + 0.18f * (float)rng.NextDouble();
                float lo = 1500f + 2500f * (float)rng.NextDouble();
                PNoise(b, at, 0.01f, amp, 0.0002f, 0.0025f, lo, lo * 3f, rng);
            }
            if (tier == 2)
            {
                // A second, smaller burst a beat later: size said with a follow-up, not with sub.
                PNoise(b, 0.09f, 0.03f, 0.35f, 0.0002f, 0.006f, 1500f, 10000f, rng);
                PSine(b, 0.09f, 0.12f, 180f, 100f, 0.02f, 0.3f, 0.001f, 0.04f);
            }
            Saturate(b, 1.9f);
            HighPass(b, 130f);
            Room(b, 0.1f + 0.03f * tier, 0.75f);
            return PFinish("explode3+" + tier + "." + v, b, 0.92f);
        }

        /// <summary>A soft marimba note: a sine with its octave, a gentle attack, a quick fade.
        /// Nothing inharmonic in it, so nothing rings.</summary>
        private static void Marimba(float[] b, float at, float f, float amp, float tau)
        {
            PSine(b, at, tau * 6f, f, f, 1f, amp, 0.004f, tau);
            PSine(b, at, tau * 3f, f * 2f, f * 2f, 1f, amp * 0.25f, 0.003f, tau * 0.45f);
            PSine(b, at, tau * 2f, f * 4f, f * 4f, 1f, amp * 0.06f, 0.002f, tau * 0.2f);
        }

        /// <summary>The sweep: the V3 crack and punch, then a warm rising marimba run and a breath
        /// of air - a reward, without a single bell.</summary>
        private static AudioClip V3Sweep(int v)
        {
            var rng = new System.Random(13000 + v);
            float[] b = Buffer(1.2f);
            PNoise(b, 0f, 0.03f, 0.55f, 0.0002f, 0.007f, 1800f, 12000f, rng);
            PSine(b, 0f, 0.2f, 210f, 95f, 0.02f, 0.5f, 0.001f, 0.07f);
            PNoiseSweep(b, 0.004f, 0.4f, 0.35f, 0.002f, 0.09f, 500f, 9000f, 2200f, rng);
            float[] notes = { 392f, 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int i = 0; i < notes.Length; i++)
            {
                Marimba(b, 0.06f + 0.06f * i, notes[i] * (v == 1 ? 1.002f : 1f), 0.22f, 0.16f + 0.03f * i);
            }
            PNoise(b, 0.05f, 0.8f, 0.03f, 0.15f, 0.2f, 3000f, 9000f, rng); // air
            Saturate(b, 1.2f);
            HighPass(b, 120f);
            Room(b, 0.22f, 0.9f);
            return PFinish("sweep3+" + v, b, 0.9f);
        }

        /// <summary>Chimes and fusions: two soft marimba notes.</summary>
        private static AudioClip V3Bell()
        {
            float[] b = Buffer(0.8f);
            Marimba(b, 0f, 659.25f, 0.3f, 0.18f);
            Marimba(b, 0.07f, 987.77f, 0.2f, 0.16f);
            Room(b, 0.2f, 0.8f);
            return PFinish("bell3+", b, 0.8f);
        }

        /// <summary>A riffle with more paper and less tick, ending on a soft square-up tap.</summary>
        private static AudioClip V3Shuffle(int v)
        {
            var rng = new System.Random(14000 + v);
            float[] b = Buffer(0.5f);
            const int flicks = 18;
            for (int k = 0; k < flicks; k++)
            {
                float u = k / (float)(flicks - 1);
                float at = 0.01f + 0.28f * (u - 0.3f * Mathf.Sin(2f * Mathf.PI * u) / (2f * Mathf.PI))
                    + 0.005f * (float)rng.NextDouble();
                float amp = (0.14f + 0.12f * (float)rng.NextDouble()) * (0.6f + 0.4f * Mathf.Sin(Mathf.PI * u));
                PNoise(b, at, 0.03f, amp, 0.0006f, 0.007f, 1200f, 6000f, rng);
            }
            PNoise(b, 0.34f, 0.03f, 0.3f, 0.0003f, 0.006f, 600f, 4000f, rng);
            PSine(b, 0.34f, 0.06f, 260f, 180f, 0.01f, 0.25f, 0.0006f, 0.02f);
            HighPass(b, 150f);
            Room(b, 0.05f, 0.5f);
            return PFinish("shuffle3+" + v, b, 0.8f);
        }

        /// <summary>Buying: a soft pop and a warm two-note pluck - pleased, not a cash register.
        /// </summary>
        private static AudioClip V3Buy(int v)
        {
            var rng = new System.Random(15000 + v);
            float[] b = Buffer(0.45f);
            float j = 1f + 0.01f * (v - 1f);
            PNoise(b, 0f, 0.01f, 0.25f, 0.0002f, 0.002f, 2000f, 9000f, rng);
            PSine(b, 0f, 0.05f, 400f, 700f, 0.01f, 0.25f, 0.001f, 0.015f); // the pop
            Marimba(b, 0.02f, 783.99f * j, 0.3f, 0.12f);
            Marimba(b, 0.09f, 1174.66f * j, 0.26f, 0.14f);
            HighPass(b, 150f);
            Room(b, 0.12f, 0.6f);
            return PFinish("buy3+" + v, b, 0.8f);
        }

        /// <summary>A card vanishing: a soft low puff, rising a little.</summary>
        private static AudioClip V3Vanish(int v)
        {
            var rng = new System.Random(16000 + v);
            float[] b = Buffer(0.3f);
            PNoiseSweep(b, 0f, 0.22f, 0.35f, 0.012f, 0.05f, 400f, 1500f, 5000f, rng, true);
            HighPass(b, 200f);
            Room(b, 0.1f, 0.5f);
            return PFinish("vanish3+" + v, b, 0.75f);
        }

        /// <summary>A card lifted: a short paper slide, no tonal blip.</summary>
        private static AudioClip V3Pickup(int v)
        {
            var rng = new System.Random(17000 + v);
            float[] b = Buffer(0.1f);
            PNoise(b, 0f, 0.012f, 0.22f, 0.0002f, 0.0025f, 2500f, 10000f, rng);
            PNoiseSweep(b, 0.003f, 0.07f, 0.12f, 0.008f, 0.018f, 1500f, 3500f, 7000f, rng);
            Room(b, 0.04f, 0.4f);
            return PFinish("pickup3+" + v, b, 0.65f);
        }

        /// <summary>A refused drop: one dull, soft knock with a little buzz - a "no", not an
        /// alarm.</summary>
        private static AudioClip V3Reject(int v)
        {
            var rng = new System.Random(18000 + v);
            float[] b = Buffer(0.18f);
            PSine(b, 0f, 0.12f, 240f, 150f, 0.012f, 0.45f, 0.0008f, 0.035f);
            PSine(b, 0f, 0.08f, 480f, 300f, 0.012f, 0.12f, 0.0008f, 0.02f);
            PNoise(b, 0f, 0.03f, 0.18f, 0.0003f, 0.006f, 300f, 1600f, rng);
            Saturate(b, 1.8f);
            HighPass(b, 120f);
            Room(b, 0.04f, 0.4f);
            return PFinish("reject3+" + v, b, 0.7f);
        }

        /// <summary>A two-pole high-pass over the whole clip - what keeps V3 out of the sub.</summary>
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
    }
}
