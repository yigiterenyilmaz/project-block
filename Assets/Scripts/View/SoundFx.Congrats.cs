// PURPOSE: The CONGRATS cue - "Eforsuz galibiyet" paying out (and anything else that wants a small
// "well done"). Built on the MIX palette's own instruments so it sits with the rest of the set:
// two party-popper pops (a noise crack over a resonant click - the confetti cannons firing), then
// a short bright fanfare in C major played on the plucked string with a soft square doubling it an
// octave down, a strummed final chord and dry sparkle over the top. The OVERTIME version is the
// same phrase BIGGER - an extra pop, one more rung on the climb, a longer shimmer - never a
// different tune, because it is more of the same feat.

using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class SoundFx
    {
        private AudioClip congratsClip;
        private AudioClip congratsBigClip;

        /// <summary>The congrats jingle. <paramref name="big"/> is the overtime version.</summary>
        public void Congrats(bool big)
        {
            if (big)
            {
                if (congratsBigClip == null)
                {
                    congratsBigClip = BuildCongrats(true);
                }
                PlayWithPitch(congratsBigClip, 1f, 1f, 0.85f);
            }
            else
            {
                if (congratsClip == null)
                {
                    congratsClip = BuildCongrats(false);
                }
                PlayWithPitch(congratsClip, 1f, 1f, 0.8f);
            }
        }

        private static AudioClip BuildCongrats(bool big)
        {
            var rng = new System.Random(big ? 31002 : 31001);
            float[] b = Buffer(big ? 2.0f : 1.6f);
            // The cannons: crack + popping cork, one per side (and one from below when big).
            int pops = big ? 3 : 2;
            for (int p = 0; p < pops; p++)
            {
                float at = 0.06f * p;
                PNoise(b, at, 0.05f, 0.55f, 0.0005f, 0.012f, 900f, 7000f, rng);
                ResonantClick(b, at + 0.002f, 380f + 90f * p, 900f + 150f * p, 18f, 0.7f, 0.0015f, rng, 0.03f);
            }
            // The fanfare: a climbing C major run, then the chord.
            float start = 0.16f;
            float[] run = big
                ? new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.51f }
                : new[] { 523.25f, 659.25f, 783.99f, 1046.5f };
            const float step = 0.085f;
            for (int i = 0; i < run.Length; i++)
            {
                Pluck(b, start + step * i, run[i], 0.34f, 0.993f, 0.45f, rng);
                Square(b, start + step * i, run[i] * 0.5f, 0.05f, 0.07f);
            }
            float chordAt = start + step * run.Length + 0.03f;
            float[] chord = { 523.25f, 659.25f, 783.99f, 1046.5f, 1568f };
            for (int i = 0; i < chord.Length; i++)
            {
                Pluck(b, chordAt + 0.014f * i, chord[i], 0.3f, 0.996f, 0.4f, rng);
            }
            Square(b, chordAt, 261.63f, 0.06f, 0.35f);
            Square(b, chordAt, 392f, 0.04f, 0.3f);
            Sparkle(b, chordAt, big ? 0.9f : 0.6f, big ? 34 : 22, 0.12f, rng);
            PNoise(b, chordAt, big ? 1.0f : 0.7f, 0.03f, 0.1f, 0.35f, 5000f, 11000f, rng); // shimmer
            Saturate(b, 1.15f);
            HighPass(b, 120f);
            Room(b, 0.26f, 0.9f);
            return PFinish(big ? "congratsBig" : "congrats", b, 0.9f);
        }
    }
}
