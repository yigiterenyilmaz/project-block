// PURPOSE: SNOW and "Çığ" HEARD - the cues SnowSound synthesizes, baked into AudioClips on first
// use (three takes each, never the same one twice running) and played on the polished pool.
// THE MIX: the snow's own small sounds (a move, a merge, a melt) sit well under a line clear; the
// avalanche's impacts are the loudest thing it does, and even they stay under the game's blasts.
// EXTENSION POINT: a new cue is a SnowCue, a recipe in SnowSound and a level here.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class SoundFx
    {
        private readonly Dictionary<int, AudioClip> snowClips = new Dictionary<int, AudioClip>();
        private readonly Dictionary<SnowCue, int> snowLastTake = new Dictionary<SnowCue, int>();

        /// <summary>The snow system's overall level (a lab knob).</summary>
        public static float SnowVolume = 1f;

        /// <summary>Plays one snow cue. <paramref name="pitch"/> nudges it (a row's impact can climb a
        /// hair as the avalanche goes deeper).</summary>
        public void Snow(SnowCue cue, float pitch = 1f)
        {
            int last;
            snowLastTake.TryGetValue(cue, out last);
            int take = (last + 1 + Random.Range(0, SnowSound.Variants - 1)) % SnowSound.Variants;
            snowLastTake[cue] = take;
            int key = (int)cue * 10 + take;
            AudioClip clip;
            if (!snowClips.TryGetValue(key, out clip))
            {
                float[] samples = SnowSound.Build(cue, take);
                if (samples == null)
                {
                    return;
                }
                clip = AudioClip.Create("snow" + cue + take, samples.Length, 1, SnowSound.Rate, false);
                clip.SetData(samples, 0);
                snowClips[key] = clip;
            }
            float volume;
            switch (cue)
            {
                case SnowCue.Move: volume = 0.32f; break;
                case SnowCue.Arrive: volume = 0.3f; break;
                case SnowCue.Merge: volume = 0.55f; break;
                case SnowCue.PowerTick: volume = 0.3f; break;
                case SnowCue.Refresh: volume = 0.42f; break;
                case SnowCue.Melt: volume = 0.36f; break;
                case SnowCue.AimBlocked: volume = 0.32f; break;
                case SnowCue.Spent: volume = 0.34f; break;
                case SnowCue.PressureRumble: volume = 0.6f; break;
                case SnowCue.Release: volume = 0.72f; break;
                case SnowCue.RowImpact: volume = 0.78f; break;
                case SnowCue.Settle: volume = 0.6f; break;
                case SnowCue.Reward: volume = 0.55f; break;
                case SnowCue.AimPulse: volume = 0.3f; break;
                case SnowCue.Age: volume = 0.14f; break;
                default: volume = 0.4f; break;
            }
            volume *= SnowVolume;
            if (pool != null)
            {
                PlayPolished(clip, pitch * Detune(0.25f), volume);
            }
            else
            {
                PlayWithPitch(clip, pitch, pitch, volume);
            }
        }
    }
}
