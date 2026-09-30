// PURPOSE: "Kredi kartı" sounds (2026-09-29, designer's call) - built on the MIX palette's own
// instruments like the rest of the set.
//   DebtBuy     buying ON CREDIT: a card SWIPE (a fast band-passed noise sweep) and then a low
//               two-note DIP on the muted pluck - the purchase went through, and it cost you
//               something you do not have yet. Never the till's cheerful up-step of Buy.
//   DebtPaid    settling up: a short rising three-note pluck over a soft felt thunk - relief.
//   Foreclose   the run lost to an open debt: a heavy stamp (thump + crunchy transient), then a
//               slow falling minor figure.
// The DEBT PRESSURE pass (2026-09-30) adds the small, dry sounds of a contract closing in - none
// of them musical, none of them a clock:
//   DebtTick     a stage of the term spent: one dry tick, a hard little resonance and a knock.
//   DebtThud     SON VADE stamped on the ledger: a small dry thud with paper in it.
//   DebtClick    the minimum paid: a two-part mechanical click, no chime.
//   DebtRelease  a payment letting the pressure go: a soft exhale of filtered air.
//   DebtCreak    the final stage's only bed: a low, distant, dry paper creak, now and then.

using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class SoundFx
    {
        private AudioClip debtBuyClip;
        private AudioClip debtPaidClip;
        private AudioClip forecloseClip;
        private AudioClip debtTickClip;
        private AudioClip debtThudClip;
        private AudioClip debtClickClip;
        private AudioClip debtReleaseClip;
        private AudioClip debtCreakClip;

        public void DebtTick()
        {
            if (debtTickClip == null) debtTickClip = BuildDebtTick();
            PlayWithPitch(debtTickClip, 0.98f, 1.02f, 0.6f);
        }

        public void DebtThud()
        {
            if (debtThudClip == null) debtThudClip = BuildDebtThud();
            PlayWithPitch(debtThudClip, 1f, 1f, 0.75f);
        }

        public void DebtClick()
        {
            if (debtClickClip == null) debtClickClip = BuildDebtClick();
            PlayWithPitch(debtClickClip, 1f, 1f, 0.5f);
        }

        public void DebtRelease()
        {
            if (debtReleaseClip == null) debtReleaseClip = BuildDebtRelease();
            PlayWithPitch(debtReleaseClip, 1f, 1f, 0.35f);
        }

        public void DebtCreak()
        {
            if (debtCreakClip == null) debtCreakClip = BuildDebtCreak();
            PlayWithPitch(debtCreakClip, 0.9f, 1.05f, 0.22f);
        }

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

        private static AudioClip BuildDebtTick()
        {
            var rng = new System.Random(32011);
            float[] b = Buffer(0.14f);
            ResonantClick(b, 0f, 2900f, 2650f, 28f, 0.5f, 0.0015f, rng);   // the tick
            PNoise(b, 0f, 0.006f, 0.35f, 0.0002f, 0.0015f, 3000f, 9000f, rng);
            PSine(b, 0.001f, 0.04f, 190f, 140f, 0.01f, 0.25f, 0.0005f, 0.012f); // a knock under it
            HighPass(b, 90f);
            Room(b, 0.05f, 0.35f);
            return PFinish("debtTick", b, 0.7f);
        }

        private static AudioClip BuildDebtThud()
        {
            var rng = new System.Random(32012);
            float[] b = Buffer(0.4f);
            Thump(b, 0f, 0.9f, 125f, 62f, 0.05f);
            PNoise(b, 0f, 0.035f, 0.4f, 0.001f, 0.012f, 300f, 2500f, rng);     // the stamp's body
            PNoise(b, 0.002f, 0.05f, 0.22f, 0.0005f, 0.018f, 1500f, 6000f, rng); // paper under it
            Saturate(b, 1.2f);
            HighPass(b, 50f);
            Room(b, 0.08f, 0.45f);
            return PFinish("debtThud", b, 0.8f);
        }

        private static AudioClip BuildDebtClick()
        {
            var rng = new System.Random(32013);
            float[] b = Buffer(0.16f);
            ResonantClick(b, 0f, 2250f, 2050f, 20f, 0.5f, 0.001f, rng);
            ResonantClick(b, 0.034f, 1600f, 1500f, 16f, 0.42f, 0.001f, rng);
            PNoise(b, 0.034f, 0.005f, 0.2f, 0.0002f, 0.0012f, 2500f, 8000f, rng);
            HighPass(b, 150f);
            Room(b, 0.05f, 0.3f);
            return PFinish("debtClick", b, 0.7f);
        }

        private static AudioClip BuildDebtRelease()
        {
            var rng = new System.Random(32014);
            float[] b = Buffer(0.6f);
            // an exhale: filtered air whose brightness falls away as it goes
            PNoiseSweep(b, 0f, 0.5f, 0.35f, 0.06f, 0.18f, 200f, 2400f, 600f, rng);
            PSine(b, 0f, 0.4f, 220f, 160f, 0.2f, 0.08f, 0.05f, 0.15f);
            HighPass(b, 80f);
            Room(b, 0.15f, 0.6f);
            return PFinish("debtRelease", b, 0.6f);
        }

        private static AudioClip BuildDebtCreak()
        {
            var rng = new System.Random(32015);
            float[] b = Buffer(0.6f);
            // a handful of dry grains at irregular spacing - paper under strain, far away
            float t = 0.01f;
            for (int i = 0; i < 9; i++)
            {
                float lo = 380f + 500f * (float)rng.NextDouble();
                PNoise(b, t, 0.012f, 0.22f + 0.2f * (float)rng.NextDouble(), 0.0008f, 0.004f,
                    lo, lo * 2.6f, rng);
                t += 0.018f + 0.04f * (float)rng.NextDouble();
            }
            ResonantClick(b, 0.02f, 310f, 270f, 8f, 0.18f, 0.002f, rng, 0.2f);
            LowPass(b, 2600f);
            HighPass(b, 120f);
            Room(b, 0.25f, 0.7f);
            return PFinish("debtCreak", b, 0.55f);
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
