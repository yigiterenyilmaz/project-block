// PURPOSE: A retro bit-crush audio filter. It lives on the AudioListener (the Main Camera), so
// OnAudioFilterRead processes the WHOLE game mix - sample-and-hold downsampling + bit-depth
// reduction - while retro mode is on. Toggled via Active from GameUiController. The callback runs
// on the audio thread, so it is allocation-free after the first (per-channel-count) call.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Downsamples and quantizes the final audio mix while Active. Attach to the
    /// AudioListener GameObject; a filter on an AudioSource-only object is not reliably invoked.</summary>
    public sealed class BitCrushFilter : MonoBehaviour
    {
        /// <summary>Crush only while true; a straight passthrough otherwise.</summary>
        public bool Active;

        /// <summary>Sample-and-hold factor: repeat each latched sample this many outputs
        /// (higher = grittier / lower effective sample rate).</summary>
        public int Downsample = 6;

        /// <summary>Quantization levels (lower = coarser bit depth). 16 ≈ 4-bit.</summary>
        public float Levels = 16f;

        private int hold;
        private float[] held;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!Active || channels <= 0)
            {
                return;
            }
            if (held == null || held.Length != channels)
            {
                held = new float[channels]; // one-time, only when the channel count changes
            }
            int step = Mathf.Max(1, Downsample);
            float levels = Mathf.Max(2f, Levels);
            for (int i = 0; i + channels <= data.Length; i += channels)
            {
                if (hold <= 0)
                {
                    hold = step;
                    for (int c = 0; c < channels; c++)
                    {
                        held[c] = Mathf.Round(data[i + c] * levels) / levels;
                    }
                }
                hold--;
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = held[c];
                }
            }
        }
    }
}
