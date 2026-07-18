// PURPOSE: Clean-sweep streak feedback: flames surround the game arena and GROW with
// every sweep of the round (fixed positions, increasing size - not increasing count).
// They render BEHIND the board, so only their outer halves lick out around the edges.
// Reset every round via SetState(0, ...).

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Arena border flames that scale with the round's clean-sweep count.</summary>
    public sealed class FlameStreakView : MonoBehaviour
    {
        private const int MaxLevel = 6;
        private const float Spacing = 0.85f;

        private static readonly Color OuterColor = new Color(1f, 0.55f, 0.15f);
        private static readonly Color InnerColor = new Color(1f, 0.85f, 0.3f);

        private readonly List<Transform> flames = new List<Transform>();
        private readonly List<float> flameSizes = new List<float>();
        private int shownLevel = -1;
        private Rect shownArea;

        /// <summary>Rebuilds the flame ring for a sweep count around the board area.</summary>
        public void SetState(int sweepCount, Rect boardArea)
        {
            int level = Mathf.Min(sweepCount, MaxLevel);
            if (level == shownLevel && boardArea == shownArea)
            {
                return;
            }
            shownLevel = level;
            shownArea = boardArea;
            flames.Clear();
            flameSizes.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            if (level <= 0 || boardArea.width <= 0f)
            {
                return;
            }
            float baseSize = 0.3f + 0.17f * level;
            foreach (Vector2 position in PerimeterPoints(boardArea))
            {
                CreateFlame(position, baseSize * Random.Range(0.85f, 1.15f));
            }
        }

        private static IEnumerable<Vector2> PerimeterPoints(Rect area)
        {
            int countX = Mathf.Max(2, Mathf.RoundToInt(area.width / Spacing));
            int countY = Mathf.Max(2, Mathf.RoundToInt(area.height / Spacing));
            for (int i = 0; i <= countX; i++)
            {
                float x = Mathf.Lerp(area.xMin, area.xMax, i / (float)countX);
                yield return new Vector2(x, area.yMin);
                yield return new Vector2(x, area.yMax);
            }
            for (int i = 1; i < countY; i++)
            {
                float y = Mathf.Lerp(area.yMin, area.yMax, i / (float)countY);
                yield return new Vector2(area.xMin, y);
                yield return new Vector2(area.xMax, y);
            }
        }

        private void CreateFlame(Vector2 position, float size)
        {
            var flame = new GameObject("Flame").transform;
            flame.SetParent(transform, false);
            flame.localPosition = new Vector3(position.x, position.y, 0f);
            // negative orders: the board background (order 0) hides the inner halves
            SpriteRenderer outer = ViewUtil.MakeCell(flame, "Outer", Vector2.zero, size, OuterColor, -2);
            outer.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            SpriteRenderer inner = ViewUtil.MakeCell(flame, "Inner",
                new Vector2(0f, -size * 0.12f), size * 0.55f, InnerColor, -1);
            inner.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            flames.Add(flame);
            flameSizes.Add(size);
        }

        private void Update()
        {
            for (int i = 0; i < flames.Count; i++)
            {
                float flicker = 1f + 0.12f * Mathf.Sin(Time.time * 6f + i * 1.7f);
                flames[i].localScale = new Vector3(flicker, flicker, 1f);
            }
        }
    }
}
