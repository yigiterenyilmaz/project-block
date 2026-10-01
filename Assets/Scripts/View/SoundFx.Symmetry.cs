// PURPOSE: "Simetri"'s payout HEARD - the cues SymmetrySound synthesizes, baked into AudioClips
// once (on first use) and played on the polished pool, so a lock's pitch never bends one still
// ringing. One clip per LOCK STEP: the pairs climb a pentatonic ladder, so the "dışşınk"s of a
// payout are a little phrase that builds instead of one sample repeated. The levels sit under the
// game's own clears: this is a recognition, not an explosion.
// EXTENSION POINT: a new cue is a SymmetryCue, a recipe in SymmetrySound and a level here.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class SoundFx
    {
        private readonly Dictionary<int, AudioClip> symmetryClips = new Dictionary<int, AudioClip>();

        /// <summary>The symmetry payout's overall level (a lab knob).</summary>
        public static float SymmetryVolume = 1f;

        /// <summary>Plays one beat of the symmetry payout; <paramref name="step"/> is the lock's
        /// place on the ladder (ignored by the other cues).</summary>
        public void Symmetry(SymmetryCue cue, int step)
        {
            if (cue != SymmetryCue.Lock && cue != SymmetryCue.Trace)
            {
                step = 0;
            }
            else
            {
                step = Mathf.Clamp(step, 0, SymmetrySound.LockSteps - 1);
            }
            int key = (int)cue * 100 + step;
            AudioClip clip;
            if (!symmetryClips.TryGetValue(key, out clip))
            {
                float[] samples = SymmetrySound.Build(cue, step);
                if (samples == null)
                {
                    return;
                }
                clip = AudioClip.Create("sym" + cue + step, samples.Length, 1, SymmetrySound.Rate, false);
                clip.SetData(samples, 0);
                symmetryClips[key] = clip;
            }
            float volume;
            switch (cue)
            {
                case SymmetryCue.Detect: volume = 0.4f; break;
                case SymmetryCue.Trace: volume = 0.32f; break;
                case SymmetryCue.Lock: volume = 0.62f; break;
                case SymmetryCue.Resonance:
                case SymmetryCue.ResonanceDouble: volume = 0.55f; break;
                case SymmetryCue.Land: volume = 0.45f; break;
                default: volume = 0.78f; break;
            }
            volume *= SymmetryVolume;
            if (pool != null)
            {
                // a hair of spread on the repeated beats only: the ladder carries the pitch
                float pitch = cue == SymmetryCue.Trace ? Detune(0.3f) : 1f;
                PlayPolished(clip, pitch, volume);
            }
            else
            {
                PlayWithPitch(clip, 1f, 1f, volume);
            }
        }
    }
}
