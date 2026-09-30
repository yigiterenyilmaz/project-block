// PURPOSE: "Antimadde" - the annihilation event's sounds, one per beat of AntimatterBlastView.
// Half of what makes the event legendary is here, and the most important part of it is a HOLE:
// the charge and the implosion both END a few tens of ms before the peak, so the peak lands out of
// near silence. Nothing here is an ordinary explosion - no crunch, no debris, no fire.
//
//   AntimatterDistort    reality noticing: a high thin AIR PULL swelling in, over a very low sub
//                        tone that comes up from nothing.
//   AntimatterGhost      the twin separating: a faint glassy shimmer of two detuned high sines.
//   AntimatterCharge     the buildup: a low sub drone climbing under a rising glassy tone, getting
//                        louder all the way - and stopping dead, well before the peak.
//   AntimatterContact    matter touching antimatter: one tiny crystalline tick.
//   AntimatterImplosion  the vacuum SUCK: noise swelling while its brightness is drawn down, a
//                        pitch falling into the floor, cut off - then nothing.
//   AntimatterPeak       NOT an explosion: a deep sub impact, a high crystalline crack, a short
//                        burst of bright energy and a wide low pressure wave, in a small room.
//   AntimatterBeam       the rays: a high hiss that snaps off.
//   AntimatterShockwave  the arena's pressure wave: a low broad WHUMP.
//   AntimatterAftermath  what is left spiralling in: a high tone falling away with a slow wobble,
//                        and a thin shimmer fading under it.
//   AntimatterReward     the value landing on the TOTAL: two soft marimba notes and a breath of
//                        sparkle - a payment, never a jackpot.
// Built on first use, like the rest of the late cues. The haptics the design asks for (a light
// tick at the implosion, one strong pulse at the peak, a lighter one behind the shockwave) have
// their beats here but no layer to go to yet - the game has no haptics.

using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class SoundFx
    {
        private AudioClip amDistortClip;
        private AudioClip amGhostClip;
        private AudioClip amChargeClip;
        private AudioClip amContactClip;
        private AudioClip amImplosionClip;
        private AudioClip amPeakClip;
        private AudioClip amBeamClip;
        private AudioClip amShockwaveClip;
        private AudioClip amAftermathClip;
        private AudioClip amRewardClip;

        public void AntimatterDistort()
        {
            if (amDistortClip == null) amDistortClip = BuildAmDistort();
            PlayWithPitch(amDistortClip, 1f, 1f, 0.45f);
        }

        public void AntimatterGhost()
        {
            if (amGhostClip == null) amGhostClip = BuildAmGhost();
            PlayWithPitch(amGhostClip, 1f, 1f, 0.3f);
        }

        public void AntimatterCharge()
        {
            if (amChargeClip == null) amChargeClip = BuildAmCharge();
            PlayWithPitch(amChargeClip, 1f, 1f, 0.6f);
        }

        public void AntimatterContact()
        {
            if (amContactClip == null) amContactClip = BuildAmContact();
            PlayWithPitch(amContactClip, 1f, 1f, 0.35f);
        }

        public void AntimatterImplosion()
        {
            if (amImplosionClip == null) amImplosionClip = BuildAmImplosion();
            PlayWithPitch(amImplosionClip, 1f, 1f, 0.6f);
        }

        public void AntimatterPeak()
        {
            if (amPeakClip == null) amPeakClip = BuildAmPeak();
            PlayWithPitch(amPeakClip, 1f, 1f, 1f);
        }

        public void AntimatterBeam()
        {
            if (amBeamClip == null) amBeamClip = BuildAmBeam();
            PlayWithPitch(amBeamClip, 0.98f, 1.02f, 0.4f);
        }

        public void AntimatterShockwave()
        {
            if (amShockwaveClip == null) amShockwaveClip = BuildAmShockwave();
            PlayWithPitch(amShockwaveClip, 1f, 1f, 0.75f);
        }

        public void AntimatterAftermath()
        {
            if (amAftermathClip == null) amAftermathClip = BuildAmAftermath();
            PlayWithPitch(amAftermathClip, 1f, 1f, 0.35f);
        }

        public void AntimatterReward()
        {
            if (amRewardClip == null) amRewardClip = BuildAmReward();
            PlayWithPitch(amRewardClip, 1f, 1f, 0.6f);
        }

        private static AudioClip BuildAmDistort()
        {
            var rng = new System.Random(41001);
            float[] b = Buffer(0.32f);
            // the air pulled thin: high noise swelling in and drawn up out of the way
            NoiseSwell(b, 0f, 0.2f, 3500f, 6000f, 11000f, 0.35f, 1.6f, rng);
            PNoise(b, 0.2f, 0.1f, 0.2f, 0.0005f, 0.03f, 4000f, 11000f, rng);
            // and a sub tone coming up from nothing under it
            Swell(b, 0f, 0.3f, 42f, 46f, 0.5f, 1.2f);
            HighPass(b, 30f);
            Room(b, 0.1f, 0.5f);
            return PFinish("amDistort", b, 0.6f);
        }

        private static AudioClip BuildAmGhost()
        {
            var rng = new System.Random(41002);
            float[] b = Buffer(0.3f);
            // two detuned glassy sines beating against each other, a breath of air over them
            PSine(b, 0f, 0.28f, 1760f, 1760f, 1f, 0.18f, 0.06f, 0.09f);
            PSine(b, 0f, 0.28f, 1771f, 1771f, 1f, 0.16f, 0.07f, 0.08f);
            PSine(b, 0.01f, 0.2f, 2637f, 2637f, 1f, 0.06f, 0.05f, 0.05f);
            PNoise(b, 0f, 0.2f, 0.06f, 0.05f, 0.05f, 6000f, 12000f, rng);
            Room(b, 0.2f, 0.6f);
            return PFinish("amGhost", b, 0.45f);
        }

        private static AudioClip BuildAmCharge()
        {
            var rng = new System.Random(41003);
            // 0.34 s - started 0.02 s before the charge, it stops ~0.04 s before the implosion's
            // own suck is at its loudest, and the implosion stops ~0.03 s before the peak.
            float[] b = Buffer(0.34f);
            Swell(b, 0f, 0.34f, 52f, 74f, 0.7f, 1.3f);             // the sub drone, climbing
            Swell(b, 0f, 0.34f, 104f, 148f, 0.18f, 1.5f);          // its octave, for small speakers
            Swell(b, 0.02f, 0.32f, 880f, 2350f, 0.16f, 2.2f);      // the rising glassy tone
            Swell(b, 0.05f, 0.29f, 1318f, 3520f, 0.07f, 2.6f);     // and its fifth, thinner
            NoiseSwell(b, 0.1f, 0.24f, 2500f, 3000f, 9000f, 0.08f, 2.4f, rng);
            HighPass(b, 32f);
            return PFinish("amCharge", b, 0.7f);
        }

        private static AudioClip BuildAmContact()
        {
            var rng = new System.Random(41004);
            float[] b = Buffer(0.12f);
            ResonantClick(b, 0f, 4200f, 3900f, 30f, 0.5f, 0.0008f, rng);
            PSine(b, 0f, 0.05f, 5274f, 5274f, 1f, 0.12f, 0.0005f, 0.012f);
            HighPass(b, 800f);
            Room(b, 0.08f, 0.3f);
            return PFinish("amContact", b, 0.4f);
        }

        private static AudioClip BuildAmImplosion()
        {
            var rng = new System.Random(41005);
            // Short and cut off: the suck is at its loudest at the very end, and then there is
            // nothing - no room tail, on purpose.
            float[] b = Buffer(0.075f);
            NoiseSwell(b, 0f, 0.075f, 200f, 7000f, 700f, 0.55f, 1.8f, rng);
            Swell(b, 0f, 0.075f, 320f, 70f, 0.4f, 1.4f);
            HighPass(b, 40f);
            return PFinish("amImplosion", b, 0.65f);
        }

        private static AudioClip BuildAmPeak()
        {
            var rng = new System.Random(41006);
            float[] b = Buffer(1.3f);
            // A) the deep sub impact
            Thump(b, 0f, 1f, 92f, 36f, 0.16f);
            // B) a high crystalline crack - bright, glassy, short
            ResonantClick(b, 0f, 4600f, 3300f, 22f, 0.55f, 0.0015f, rng, 0.03f);
            PNoise(b, 0f, 0.02f, 0.45f, 0.0002f, 0.005f, 5000f, 14000f, rng);
            PSine(b, 0.002f, 0.2f, 3136f, 2960f, 0.08f, 0.1f, 0.001f, 0.05f);
            // C) the short burst of energy - a bright band opening and closing fast
            PNoiseSweep(b, 0.003f, 0.25f, 0.4f, 0.004f, 0.05f, 700f, 6500f, 1800f, rng);
            // D) the wide low pressure wave behind it all
            PNoise(b, 0.01f, 0.9f, 0.32f, 0.03f, 0.22f, 35f, 320f, rng);
            PSine(b, 0.02f, 0.6f, 58f, 44f, 0.2f, 0.35f, 0.02f, 0.16f);
            Saturate(b, 1.25f);
            HighPass(b, 26f);
            Room(b, 0.22f, 0.9f);
            return PFinish("amPeak", b, 0.95f);
        }

        private static AudioClip BuildAmBeam()
        {
            var rng = new System.Random(41007);
            float[] b = Buffer(0.18f);
            // a high hiss that snaps off
            PNoiseSweep(b, 0f, 0.14f, 0.35f, 0.003f, 0.035f, 3000f, 12000f, 5000f, rng);
            ResonantClick(b, 0f, 2600f, 1900f, 12f, 0.35f, 0.001f, rng, 0.02f);
            PSine(b, 0f, 0.1f, 1900f, 900f, 0.03f, 0.08f, 0.001f, 0.02f);
            HighPass(b, 900f);
            Room(b, 0.1f, 0.4f);
            return PFinish("amBeam", b, 0.5f);
        }

        private static AudioClip BuildAmShockwave()
        {
            var rng = new System.Random(41008);
            float[] b = Buffer(0.7f);
            // a low, broad WHUMP - a slower attack than an impact, all body
            PSine(b, 0f, 0.6f, 74f, 40f, 0.09f, 0.9f, 0.014f, 0.12f);
            PSine(b, 0f, 0.4f, 148f, 80f, 0.07f, 0.2f, 0.012f, 0.07f);
            PNoise(b, 0f, 0.5f, 0.35f, 0.018f, 0.1f, 40f, 280f, rng);
            PNoise(b, 0.01f, 0.25f, 0.08f, 0.01f, 0.05f, 400f, 1800f, rng);
            Saturate(b, 1.15f);
            HighPass(b, 28f);
            Room(b, 0.2f, 0.8f);
            return PFinish("amShockwave", b, 0.85f);
        }

        private static AudioClip BuildAmAftermath()
        {
            var rng = new System.Random(41009);
            float[] b = Buffer(0.75f);
            // a high tone falling away with a slow wobble - the energy spiralling in
            int n = b.Length;
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / 0.7f;
                float f = Mathf.Lerp(2200f, 880f, 1f - Mathf.Pow(1f - Mathf.Clamp01(u), 2f));
                phase += 2.0 * Mathf.PI * f / SampleRate;
                float wobble = 0.65f + 0.35f * Mathf.Sin(t * 2f * Mathf.PI * (9f - 5f * Mathf.Clamp01(u)));
                float env = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t / 0.2f);
                b[i] += Mathf.Sin((float)phase) * 0.22f * wobble * env;
            }
            PNoise(b, 0f, 0.6f, 0.07f, 0.02f, 0.18f, 5000f, 12000f, rng);
            HighPass(b, 300f);
            Room(b, 0.3f, 0.8f);
            return PFinish("amAftermath", b, 0.4f);
        }

        private static AudioClip BuildAmReward()
        {
            var rng = new System.Random(41010);
            float[] b = Buffer(0.6f);
            Marimba(b, 0f, 783.99f, 0.4f, 0.12f);
            Marimba(b, 0.07f, 1174.66f, 0.36f, 0.14f);
            Sparkle(b, 0.05f, 0.18f, 6, 0.06f, rng);
            HighPass(b, 150f);
            Room(b, 0.18f, 0.6f);
            return PFinish("amReward", b, 0.6f);
        }

        /// <summary>A sine whose pitch glides f0 -> f1 (exponentially) under an envelope that
        /// RISES to the very end, (t/seconds)^curve - a swell that is cut off rather than decayed.
        /// </summary>
        private static void Swell(float[] b, float atSec, float seconds, float f0, float f1, float amp, float curve)
        {
            int start = (int)(atSec * SampleRate);
            int n = Mathf.Min((int)(seconds * SampleRate), b.Length - start);
            double phase = 0.0;
            float ratio = f1 / Mathf.Max(1f, f0);
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)Mathf.Max(1, n);
                float f = f0 * Mathf.Pow(ratio, u);
                phase += 2.0 * Mathf.PI * f / SampleRate;
                b[start + i] += Mathf.Sin((float)phase) * amp * Mathf.Pow(u, curve);
            }
        }

        /// <summary>Band-limited noise under the same rising envelope, its low-pass moving from
        /// highHz0 to highHz1 across it.</summary>
        private static void NoiseSwell(float[] b, float atSec, float seconds, float lowHz, float highHz0,
            float highHz1, float amp, float curve, System.Random rng)
        {
            int start = (int)(atSec * SampleRate);
            int n = Mathf.Min((int)(seconds * SampleRate), b.Length - start);
            float hp = 0f;
            float l1 = 0f;
            float l2 = 0f;
            float hpA = OnePole(lowHz);
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)Mathf.Max(1, n);
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                hp += (x - hp) * hpA;
                float a = OnePole(Mathf.Lerp(highHz0, highHz1, u));
                l1 += (x - hp - l1) * a;
                l2 += (l1 - l2) * a;
                b[start + i] += l2 * amp * 1.8f * Mathf.Pow(u, curve);
            }
        }
    }
}
