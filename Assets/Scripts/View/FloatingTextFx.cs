// PURPOSE: A short-lived popup text ("COMBO x3!", "CLEAN SWEEP!"): scale-punches in,
// floats upward, fades out, destroys itself. Spawn-and-forget.

using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Self-animating floating text popup.</summary>
    public sealed class FloatingTextFx : MonoBehaviour
    {
        private const float Duration = 0.95f;
        private const float RiseDistance = 0.7f;

        private TextMesh textMesh;
        private Color baseColor;
        private Vector3 basePosition;
        private float age;

        public static void Spawn(Transform parent, Vector2 position, string text,
            Color color, int fontSize, float characterSize)
        {
            TextMesh tm = ViewUtil.MakeText3D(parent, "FloatingText", position, text,
                fontSize, characterSize, color, 45, TextAnchor.MiddleCenter);
            FloatingTextFx fx = tm.gameObject.AddComponent<FloatingTextFx>();
            fx.textMesh = tm;
            fx.baseColor = color;
            fx.basePosition = tm.transform.localPosition;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Duration);
            transform.localPosition = basePosition + new Vector3(0f, RiseDistance * t, 0f);
            float punch = 1f + 0.25f * Mathf.Sin(Mathf.Min(t * 3f, 1f) * Mathf.PI);
            transform.localScale = new Vector3(punch, punch, 1f);
            Color color = baseColor;
            color.a = 1f - t * t;
            textMesh.color = color;
            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
