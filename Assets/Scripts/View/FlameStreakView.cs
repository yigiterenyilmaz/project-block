// PURPOSE: The clean-sweep streak indicator: a row of little flames (rotated-square
// "diamonds", no assets) that gains a flame per sweep this round, each slightly
// bigger than the last, gently flickering. Reset every round.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Displays one flame per clean sweep of the current round.</summary>
    public sealed class FlameStreakView : MonoBehaviour
    {
        private const int MaxFlames = 6;
        private static readonly Vector2 RowStart = new Vector2(4.3f, 3.9f);
        private const float Spacing = 0.62f;

        private static readonly Color OuterColor = new Color(1f, 0.55f, 0.15f);
        private static readonly Color InnerColor = new Color(1f, 0.85f, 0.3f);

        private readonly List<Transform> flames = new List<Transform>();
        private int shownCount = -1;

        public void SetCount(int sweepCount)
        {
            int newCount = Mathf.Min(sweepCount, MaxFlames);
            if (newCount == shownCount)
            {
                return;
            }
            shownCount = newCount;
            flames.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            for (int i = 0; i < newCount; i++)
            {
                float size = 0.32f + 0.07f * i;
                var flame = new GameObject("Flame_" + i).transform;
                flame.SetParent(transform, false);
                flame.localPosition = new Vector3(RowStart.x + i * Spacing, RowStart.y, 0f);
                SpriteRenderer outer = ViewUtil.MakeCell(flame, "Outer", Vector2.zero,
                    size, OuterColor, 12);
                outer.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                SpriteRenderer inner = ViewUtil.MakeCell(flame, "Inner",
                    new Vector2(0f, -size * 0.12f), size * 0.55f, InnerColor, 13);
                inner.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                flames.Add(flame);
            }
        }

        private void Update()
        {
            for (int i = 0; i < flames.Count; i++)
            {
                float flicker = 1f + 0.1f * Mathf.Sin(Time.time * 6f + i * 1.3f);
                flames[i].localScale = new Vector3(flicker, flicker, 1f);
            }
        }
    }
}
