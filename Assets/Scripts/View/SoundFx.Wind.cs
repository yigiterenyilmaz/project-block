// PURPOSE: "Rüzgar" HEARD - the cues WindSound synthesizes, baked into AudioClips on first use and
// played on the polished pool, plus the aim's BED: one looping source of its own, whose level and a
// few cents of pitch the controller sets every frame from how far the stroke has been drawn.
// THE MIX: the wind's own cues sit 6-10 dB under the materials', so a gust over a fire is heard as
// a fire catching IN a wind rather than a wind with a fire somewhere in it; and there is never a
// loud constant wind - the bed is a breath, and it is gone the moment the aim is.
// EXTENSION POINT: a new cue is a WindCue, a recipe in WindSound and a level here.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class SoundFx
    {
        private readonly Dictionary<int, AudioClip> windClips = new Dictionary<int, AudioClip>();
        private AudioSource windBed;

        /// <summary>The wind's overall level (a lab knob).</summary>
        public static float WindVolume = 1f;

        /// <summary>Plays one beat of the wind. <paramref name="travel"/> is the corridor's own
        /// crossing time, which picks the length of a Release and is ignored by everything else.</summary>
        public void Wind(WindCue cue, float travel)
        {
            int variant = 0;
            if (cue == WindCue.Release)
            {
                // the baked length nearest the storm's own: lock + crossing + its tail
                float wanted = travel + 0.25f;
                float best = float.MaxValue;
                for (int i = 0; i < WindSound.ReleaseVariants; i++)
                {
                    float miss = Mathf.Abs(WindSound.ReleaseSeconds(i) - wanted);
                    if (miss < best)
                    {
                        best = miss;
                        variant = i;
                    }
                }
            }
            int key = (int)cue * 10 + variant;
            AudioClip clip;
            if (!windClips.TryGetValue(key, out clip))
            {
                float[] samples = WindSound.Build(cue, variant);
                if (samples == null)
                {
                    return;
                }
                clip = AudioClip.Create("wind" + cue + variant, samples.Length, 1, WindSound.Rate, false);
                clip.SetData(samples, 0);
                windClips[key] = clip;
            }
            float volume;
            float spread = 0f;
            switch (cue)
            {
                // the wind itself: under the materials
                case WindCue.OriginLock: volume = 0.34f; break;
                case WindCue.Cancel: volume = 0.26f; break;
                case WindCue.Refused: volume = 0.3f; break;
                case WindCue.CastLock: volume = 0.42f; break;
                case WindCue.Release: volume = 0.46f; break;
                case WindCue.Tail: volume = 0.24f; break;
                // the materials: foreground
                case WindCue.FireContact: volume = 0.62f; spread = 0.5f; break;
                case WindCue.EmberTear: volume = 0.5f; spread = 1.2f; break;
                case WindCue.EmberHiss: volume = 0.3f; spread = 0.8f; break;
                case WindCue.Ignite: volume = 0.78f; spread = 0.6f; break;
                case WindCue.WaterContact: volume = 0.6f; spread = 0.6f; break;
                case WindCue.WaterHiss: volume = 0.34f; spread = 0.8f; break;
                case WindCue.WaterArrive: volume = 0.72f; spread = 0.9f; break;
                case WindCue.SporeTear: volume = 0.58f; spread = 0.7f; break;
                case WindCue.SporeWhisper: volume = 0.24f; spread = 0.6f; break;
                case WindCue.SporeImpact: volume = 0.6f; spread = 0.7f; break;
                case WindCue.SporeGrow: volume = 0.3f; spread = 0.4f; break; // a sustained low tone: quiet
                default: volume = 0.55f; spread = 0.4f; break;
            }
            volume *= WindVolume;
            if (pool != null)
            {
                // a hair of spread on the cues heard in runs, so three ignitions are not one sample
                PlayPolished(clip, spread > 0f ? Detune(spread) : 1f, volume);
            }
            else
            {
                PlayWithPitch(clip, 1f, 1f, volume);
            }
        }

        /// <summary>
        /// The aim's bed. <paramref name="level"/> 0..1 is how present it is (0 stops it) and
        /// <paramref name="drawn"/> 0..1 how far the stroke has been drawn - a little more level
        /// and a few cents of pitch, so a long gust is felt gathering. Called every frame while
        /// the wind is being aimed; quiet by design.
        /// </summary>
        public void WindBed(float level, float drawn)
        {
            if (level <= 0.001f)
            {
                if (windBed != null && windBed.isPlaying)
                {
                    windBed.Stop();
                }
                return;
            }
            if (windBed == null)
            {
                windBed = gameObject.AddComponent<AudioSource>();
                windBed.playOnAwake = false;
                windBed.loop = true;
                float[] samples = WindSound.BuildBed();
                AudioClip clip = AudioClip.Create("windBed", samples.Length, 1, WindSound.Rate, false);
                clip.SetData(samples, 0);
                windBed.clip = clip;
            }
            drawn = Mathf.Clamp01(drawn);
            windBed.volume = Mathf.Clamp01(FullVolume * masterVolume * WindVolume
                * 0.16f * level * (0.6f + 0.4f * drawn));
            // under a semitone across the whole drag: felt, never a note
            windBed.pitch = Mathf.Lerp(0.97f, 1.035f, drawn);
            if (!windBed.isPlaying)
            {
                windBed.Play();
            }
        }
    }
}
