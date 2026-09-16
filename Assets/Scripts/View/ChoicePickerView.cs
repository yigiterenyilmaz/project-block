// PURPOSE: A small modal list picker for one-off in-round choices (Batak's bet length,
// Powerbank's target power). Shows a title and a vertical stack of labelled rows; the
// controller reads the clicked row index and acts on it. Placeholder presentation like
// everything else under View/.
//
// It also has a COMPACT form (ShowCompact): a small panel beside the thing being asked about, with
// a tile per row - "Simya"'s element choice on a card in the hand. A full-screen list for "fire or
// water?" would take the board away to ask a one-glance question. Same rows, same hit test and the
// same controller plumbing (Esc, a click outside cancels, the pad steps it), only smaller and near.

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

        private const float CompactRowWidth = 2.5f;
        private const float CompactRowHeight = 0.56f;
        private const float CompactRowPitch = 0.64f;
        private const float CompactIcon = 0.40f;

        private static readonly Color CompactPanelColor = new Color(0.08f, 0.09f, 0.12f, 0.96f);
        private static readonly Color CompactRowColor = new Color(0.17f, 0.19f, 0.25f);
        private static readonly Color CompactPickedColor = new Color(0.30f, 0.27f, 0.16f);
        private static readonly Color CompactPickedEdge = new Color(0.95f, 0.80f, 0.38f);

        private readonly List<Vector2> rowCenters = new List<Vector2>();

        private float rowWidth = RowWidth;
        private float rowHeight = RowHeight;

        public bool IsOpen { get; private set; }

        public void Show(string title, IReadOnlyList<string> options)
        {
            Hide();
            IsOpen = true;
            rowWidth = RowWidth;
            rowHeight = RowHeight;
            int n = options.Count;
            float startY = (n - 1) * RowPitch * 0.5f;

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.82f), 40);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, startY + 1.1f),
                title, 48, 0.06f, Color.white, 41, TextAnchor.MiddleCenter);

            // Scaled to whatever the screen can show. On a phone held upright the rows are
            // wider than the view, and a picker you cannot read both ends of is a picker you
            // cannot use. See ViewUtil.FitOverlay - it never magnifies, so the desktop is
            // untouched.
            float top = startY + 1.1f + 0.4f;
            float bottom = startY - (n - 1) * RowPitch - RowHeight * 0.5f;
            ViewUtil.FitOverlay(transform, new Vector2(RowWidth + 0.6f, top - bottom),
                new Vector2(0f, (top + bottom) * 0.5f));

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

        /// <summary>
        /// The small form: a panel standing just above <paramref name="anchorWorld"/> (kept on
        /// screen), one row per option with its own tile on the left, and the row at
        /// <paramref name="picked"/> marked as the current answer. The board stays visible behind
        /// a light dim - it is still modal, but it asks a small question.
        /// </summary>
        public void ShowCompact(string title, IReadOnlyList<string> options, IReadOnlyList<Sprite> icons,
            IReadOnlyList<Color> iconTints, int picked, Vector2 anchorWorld)
        {
            Hide();
            IsOpen = true;
            rowWidth = CompactRowWidth;
            rowHeight = CompactRowHeight;
            transform.localScale = Vector3.one;
            int n = options.Count;
            float titleY = (n - 1) * CompactRowPitch + 0.52f;
            float panelTop = titleY + 0.26f;
            float panelBottom = -CompactRowHeight * 0.5f - 0.12f;
            float panelHeight = panelTop - panelBottom;
            float panelWidth = CompactRowWidth + 0.24f;

            // Stand the panel over the card and keep all of it inside the camera.
            Vector2 origin = anchorWorld + new Vector2(0f, 0.9f);
            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                Vector2 c = cam.transform.position;
                origin.x = Mathf.Clamp(origin.x, c.x - halfW + panelWidth * 0.5f + 0.1f,
                    c.x + halfW - panelWidth * 0.5f - 0.1f);
                origin.y = Mathf.Clamp(origin.y, c.y - halfH - panelBottom + 0.1f,
                    c.y + halfH - panelTop - 0.1f);
            }
            transform.position = new Vector3(origin.x, origin.y, 0f);

            ViewUtil.MakeRect(transform, "Dim", (Vector2)transform.InverseTransformPoint(
                    cam != null ? (Vector2)cam.transform.position : origin), new Vector2(60f, 30f),
                new Color(0f, 0f, 0f, 0.35f), 40);
            ViewUtil.MakeRect(transform, "Panel", new Vector2(0f, (panelTop + panelBottom) * 0.5f),
                new Vector2(panelWidth, panelHeight), CompactPanelColor, 41);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, titleY), title, 60, 0.028f,
                new Color(0.80f, 0.83f, 0.90f), 43, TextAnchor.MiddleCenter);

            for (int i = 0; i < n; i++)
            {
                var center = new Vector2(0f, (n - 1 - i) * CompactRowPitch);
                rowCenters.Add(center);
                if (i == picked)
                {
                    ViewUtil.MakeRect(transform, "Picked_" + i, center,
                        new Vector2(CompactRowWidth + 0.06f, CompactRowHeight + 0.06f), CompactPickedEdge, 42);
                }
                ViewUtil.MakeRect(transform, "Row_" + i, center,
                    new Vector2(CompactRowWidth, CompactRowHeight), i == picked ? CompactPickedColor : CompactRowColor, 43);
                float textX = -CompactRowWidth * 0.5f + 0.16f;
                Sprite icon = icons != null && i < icons.Count ? icons[i] : null;
                if (icon != null)
                {
                    var iconPos = new Vector2(-CompactRowWidth * 0.5f + 0.12f + CompactIcon * 0.5f, center.y);
                    SpriteRenderer tile = ViewUtil.MakeRect(transform, "Icon_" + i, iconPos, Vector2.one,
                        iconTints != null && i < iconTints.Count ? iconTints[i] : Color.white, 44);
                    ViewUtil.ApplyTile(tile, icon, CompactIcon);
                    textX = iconPos.x + CompactIcon * 0.5f + 0.14f;
                }
                ViewUtil.MakeText3D(transform, "Label_" + i, new Vector2(textX, center.y), options[i],
                    70, 0.032f, i == picked ? CompactPickedEdge : LabelColor, 45, TextAnchor.MiddleLeft);
            }
        }

        /// <summary>The rows, for a gamepad to step (see GameUiController.PadPanels.cs).</summary>
        public int OptionCount
        {
            get { return rowCenters.Count; }
        }

        public Vector2? OptionWorldCenter(int index)
        {
            if (index < 0 || index >= rowCenters.Count)
            {
                return null;
            }
            // Through the transform: the centres are local, and the overlay is now scaled and
            // moved to fit the screen, so local and world are no longer the same point.
            return transform.TransformPoint(rowCenters[index]);
        }

        /// <summary>Row index under a world point, or -1.</summary>
        public int OptionAt(Vector2 world)
        {
            Vector2 local = transform.InverseTransformPoint(world);
            for (int i = 0; i < rowCenters.Count; i++)
            {
                if (Mathf.Abs(local.x - rowCenters[i].x) <= rowWidth * 0.5f
                    && Mathf.Abs(local.y - rowCenters[i].y) <= rowHeight * 0.5f)
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
