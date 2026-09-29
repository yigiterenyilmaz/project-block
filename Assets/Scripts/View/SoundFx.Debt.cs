// PURPOSE: "Kredi kartı" sounds (2026-09-29, designer's call) - built on the MIX palette's own
// instruments like the rest of the set.
//   DebtBuy     buying ON CREDIT: a card SWIPE (a fast band-passed noise sweep) and then a low
//               two-note DIP on the muted pluck - the purchase went through, and it cost you
//               something you do not have yet. Never the till's cheerful up-step of Buy.
//   DebtPaid    settling up: a short rising three-note pluck over a soft felt thunk - relief.
//   Foreclose   the run lost to an open debt: a heavy stamp (thump + crunchy transient), then a
//               slow falling minor figure.

using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class SoundFx
    {
        private AudioClip debtBuyClip;
        private AudioClip debtPaidClip;
        private AudioClip forecloseClip;

        public void DebtBuy()
        {
            if (debtBuyClip == null) debtBuyClip = BuildDebtBuy();
            PlayWithPitch(debtBuyClip, 0.98f, 1.02f, 0.85f);
        }

        public void DebtPaid()
        {
            if (debtPaidClip == null) debtPaidClip = BuildDebtPaid();
            PlayWithPitch(debtPaidClip, 1f, 1f, 0.85f);
        }

        public void Foreclose()
        {
            if (forecloseClip == null) forecloseClip = BuildForeclose();
            PlayWithPitch(forecloseClip, 1f, 1f, 0.95f);
        }

        private static AudioClip BuildDebtBuy()
        {
            var rng = new System.Random(32001);
            float[] b = Buffer(0.7f);
            PNoiseSweep(b, 0f, 0.11f, 0.35f, 0.004f, 0.05f, 1200f, 2500f, 7000f, rng); // swipe
            ResonantClick(b, 0.1f, 520f, 900f, 20f, 0.5f, 0.0015f, rng, 0.02f);          // it took
            Pluck(b, 0.16f, 392f, 0.4f, 0.992f, 0.55f, rng);                            // dip...
            Pluck(b, 0.29f, 293.66f, 0.42f, 0.993f, 0.55f, rng);                        // ...down
            Saturate(b, 1.2f);
            HighPass(b, 120f);
            Room(b, 0.16f, 0.6f);
            return PFinish("debtBuy", b, 0.8f);
        }

        private static AudioClip BuildDebtPaid()
        {
            var rng = new System.Random(32002);
            float[] b = Buffer(0.9f);
            PNoise(b, 0f, 0.03f, 0.25f, 0.001f, 0.01f, 150f, 900f, rng);
            Pluck(b, 0.02f, 523.25f, 0.34f, 0.993f, 0.45f, rng);
            Pluck(b, 0.1f, 659.25f, 0.34f, 0.993f, 0.45f, rng);
            Pluck(b, 0.18f, 783.99f, 0.38f, 0.995f, 0.4f, rng);
            Sparkle(b, 0.2f, 0.35f, 10, 0.08f, rng);
            Saturate(b, 1.1f);
            HighPass(b, 120f);
            Room(b, 0.22f, 0.8f);
            return PFinish("debtPaid", b, 0.8f);
        }

        private static AudioClip BuildForeclose()
        {
            var rng = new System.Random(32003);
            float[] b = Buffer(1.8f);
            Thump(b, 0f, 1f, 110f, 45f, 0.12f);
            Crunch(b, 0f, 1.2f, 1, 1f, 0.08f, rng);
            float[] fall = { 440f, 392f, 349.23f, 329.63f, 261.63f };
            for (int i = 0; i < fall.Length; i++)
            {
                Pluck(b, 0.35f + 0.17f * i, fall[i], 0.32f, 0.994f, 0.5f, rng);
            }
            Square(b, 0.35f, 130.81f, 0.05f, 0.9f);
            Saturate(b, 1.2f);
            HighPass(b, 60f);
            Room(b, 0.3f, 0.95f);
            return PFinish("foreclose", b, 0.9f);
        }
    }
}
