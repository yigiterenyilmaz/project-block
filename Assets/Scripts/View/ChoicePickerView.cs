// PURPOSE: A small modal list picker for one-off in-round choices (Batak's bet length,
// Powerbank's target power). Shows a title and a vertical stack of labelled rows; the
// controller reads the clicked row index and acts on it. Placeholder presentation like
// everything else under View/.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Modal "pick one of these" list. While open, the controller blocks other input.</summary>
    public sealed class ChoicePickerView : MonoBehaviour
    {
        private const float RowWidth = 6.5f;
        private const float RowHeight = 0.82f;
        private const float RowPitch = 0.96f;

        private static readonly Color RowColor = new Color(0.16f, 0.18f, 0.23f);
        private static readonly Color LabelColor = new Color(0.92f, 0.94f, 0.98f);

        private readonly List<Vector2> rowCenters = new List<Vector2>();

        public bool IsOpen { get; private set; }

        public void Show(string title, IReadOnlyList<string> options)
        {
            Hide();
            IsOpen = true;
            int n = options.Count;
            float startY = (n - 1) * RowPitch * 0.5f;

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.82f), 40);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, startY + 1.1f),
                title, 48, 0.06f, Color.white, 41, TextAnchor.MiddleCenter);

            for (int i = 0; i < n; i++)
            {
                var center = new Vector2(0f, startY - i * RowPitch);
                rowCenters.Add(center);
                ViewUtil.MakeRect(transform, "Row_" + i, center,
                    new Vector2(RowWidth, RowHeight), RowColor, 41);
                ViewUtil.MakeText3D(transform, "Label_" + i, center, options[i],
                    60, 0.05f, LabelColor, 42, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>Row index under a world point, or -1.</summary>
        public int OptionAt(Vector2 world)
        {
            for (int i = 0; i < rowCenters.Count; i++)
            {
                if (Mathf.Abs(world.x - rowCenters[i].x) <= RowWidth * 0.5f
                    && Mathf.Abs(world.y - rowCenters[i].y) <= RowHeight * 0.5f)
                {
                    return i;
                }
            }
            return -1;
        }

        public void Hide()
        {
            IsOpen = false;
            rowCenters.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
