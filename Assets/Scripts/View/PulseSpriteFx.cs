// PURPOSE: A breathing sprite - the overlay's half of CardGlowFx. Same language (a slow sine,
// never reaching zero, so it reads as a state rather than a blink), but for a SpriteRenderer
// instead of a uGUI Image, because the deck overlay is built from world-space sprites.
// EXTENSION POINT: anything in an overlay that has to say "this is selected / this is live".

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Breathes a sprite's alpha and, optionally, its scale. Self-driving, so a caller
    /// only has to create it - the overlay rebuilds on every toggle and must not have to
    /// remember where in a cycle anything was.</summary>
    public sealed class PulseSpriteFx : MonoBehaviour
    {
        private SpriteRenderer target;
        private float low = 0.45f;
        private float high = 1f;
        private float period = 1.6f;
        private float scaleSwing;
        private Vector3 baseScale = Vector3.one;
        private Color tint = Color.white;

        /// <summary>Starts the breath. The base scale is taken ONCE, here - a per-frame scale
        /// written from the renderer's own current scale compounds, which is the bug the ice
        /// seating had.</summary>
        public static PulseSpriteFx Attach(SpriteRenderer renderer, float low, float high,
            float period, float scaleSwing)
        {
            var fx = renderer.gameObject.AddComponent<PulseSpriteFx>();
            fx.target = renderer;
            fx.low = low;
            fx.high = high;
            fx.period = period;
            fx.scaleSwing = scaleSwing;
            fx.baseScale = renderer.transform.localScale;
            fx.tint = renderer.color;
            return fx;
        }

        private void Update()
        {
            if (target == null)
            {
                return;
            }
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period);
            target.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(low, high, k));
            if (scaleSwing > 0f)
            {
                target.transform.localScale = baseScale * (1f + scaleSwing * k);
            }
        }
    }
}
