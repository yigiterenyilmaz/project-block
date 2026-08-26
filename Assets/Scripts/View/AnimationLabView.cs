// PURPOSE: The ANIMATION LAB panel (F3) - a scrollable catalogue of every animation in the
// game plus the condition knobs that modulate them. Presentation only: it draws rows and
// reports what was clicked, exactly like the other pickers under View/. The catalogue itself
// and every animation it fires live in GameUiController.AnimationLab.cs.
//
// DELIBERATELY A SIDE PANEL, not a fullscreen modal: nearly every animation in this game
// plays on the arena, the hand row or the two piles, so covering them would defeat the point.
// It occupies the empty right-hand column and leaves the whole board visible.
//
// Rebuilt from scratch on every change (cheap at this scale), like MarketView.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The F3 animation catalogue panel. The controller owns input and state.</summary>
    public sealed class AnimationLabView : MonoBehaviour
    {
        /// <summary>One catalogue line: either a group header or a playable entry.</summary>
        public struct Row
        {
            public bool IsHeader;
            public string En;
            public string Tr;

            public static Row Header(string en, string tr)
            {
                var row = new Row();
                row.IsHeader = true;
                row.En = en;
                row.Tr = tr;
                return row;
            }

            public static Row Item(string en, string tr)
            {
                var row = new Row();
                row.En = en;
                row.Tr = tr;
                return row;
            }

            public string Label
            {
                get { return Loc.Pick(En, Tr); }
            }
        }

        /// <summary>One condition knob: a name and its current value, cycled by clicking.</summary>
        public struct Knob
        {
            public string En;
            public string Tr;
            public string Value;

            public Knob(string en, string tr, string value)
            {
                En = en;
                Tr = tr;
                Value = value;
            }

            public string Label
            {
                get { return Loc.Pick(En, Tr); }
            }
        }

        /// <summary>Catalogue lines drawn at once. The list scrolls past this.</summary>
        public const int VisibleRows = 12;

        // The panel sits under the joker bar (which is on the screen-space HUD canvas and
        // would otherwise draw over it) and clear of the board's right edge at 3.25.
        private const float PanelLeft = 3.84f;
        private const float PanelRight = 8.84f;
        private const float PanelTop = 3.62f;
        private const float PanelBottom = -4.86f;

        private const float RowPitch = 0.33f;
        private const float RowHeight = 0.30f;
        private const float KnobPitch = 0.28f;
        private const float KnobHeight = 0.26f;

        private const float TitleY = 3.36f;
        private const float HelpY = 3.04f;
        private const float FirstRowY = 2.70f;
        private const float DividerY = -1.52f;
        private const float FirstKnobY = -1.78f;
        private const float StatusY = -4.68f;

        private static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.09f, 0.94f);
        private static readonly Color FrameColor = new Color(0.30f, 0.36f, 0.48f);
        private static readonly Color HeaderColor = new Color(0.55f, 0.72f, 0.95f);
        private static readonly Color ItemColor = new Color(0.86f, 0.89f, 0.94f);
        private static readonly Color SelectedRowColor = new Color(0.20f, 0.28f, 0.42f);
        private static readonly Color KnobRowColor = new Color(0.11f, 0.13f, 0.18f);
        private static readonly Color KnobLabelColor = new Color(0.72f, 0.78f, 0.86f);
        private static readonly Color KnobValueColor = new Color(1f, 0.86f, 0.42f);
        private static readonly Color TitleColor = new Color(1f, 0.92f, 0.60f);
        private static readonly Color FaintColor = new Color(0.52f, 0.58f, 0.68f);
        private static readonly Color ScrollTrackColor = new Color(0.14f, 0.16f, 0.21f);
        private static readonly Color ScrollThumbColor = new Color(0.45f, 0.55f, 0.72f);

        private const int PanelOrder = 42;
        private const int ContentOrder = 43;
        private const int TextOrder = 44;

        /// <summary>Absolute catalogue index of each drawn row (headers included, -1 padding).</summary>
        private readonly List<int> drawnRowIndices = new List<int>();
        private readonly List<float> drawnRowY = new List<float>();
        private readonly List<float> knobY = new List<float>();

        public bool IsOpen { get; private set; }

        private static float PanelWidth
        {
            get { return PanelRight - PanelLeft; }
        }

        private static float PanelCenterX
        {
            get { return (PanelLeft + PanelRight) * 0.5f; }
        }

        /// <summary>
        /// Draws the whole panel. Called on open and after every change (selection, scroll, a
        /// knob turn), so the controller never has to reason about which parts went stale.
        /// </summary>
        public void SetContent(IReadOnlyList<Row> rows, int selected, int scroll,
            IReadOnlyList<Knob> knobs, string status)
        {
            Clear();
            IsOpen = true;

            float height = PanelTop - PanelBottom;
            var center = new Vector2(PanelCenterX, (PanelTop + PanelBottom) * 0.5f);
            ViewUtil.MakeRect(transform, "Frame", center,
                new Vector2(PanelWidth + 0.09f, height + 0.09f), FrameColor, PanelOrder);
            ViewUtil.MakeRect(transform, "Panel", center,
                new Vector2(PanelWidth, height), PanelColor, PanelOrder + 1);

            ViewUtil.MakeText3D(transform, "Title", new Vector2(PanelCenterX, TitleY),
                Loc.Pick("ANIMATION LAB (F3)", "ANİMASYON LABI (F3)"),
                90, 0.022f, TitleColor, TextOrder, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "Help", new Vector2(PanelCenterX, HelpY),
                Loc.Pick("click plays  -  space replays  -  wheel scrolls",
                    "tık oynatır  -  boşluk tekrarlar  -  tekerlek kaydırır"),
                90, 0.013f, FaintColor, TextOrder, TextAnchor.MiddleCenter);

            DrawRows(rows, selected, scroll);
            DrawScrollbar(rows.Count, scroll);

            ViewUtil.MakeRect(transform, "Divider", new Vector2(PanelCenterX, DividerY),
                new Vector2(PanelWidth - 0.3f, 0.03f), FrameColor, ContentOrder);

            DrawKnobs(knobs);

            ViewUtil.MakeText3D(transform, "Status", new Vector2(PanelCenterX, StatusY),
                ViewUtil.WrapText(status ?? string.Empty, 42, 2),
                90, 0.013f, FaintColor, TextOrder, TextAnchor.MiddleCenter);
        }

        private void DrawRows(IReadOnlyList<Row> rows, int selected, int scroll)
        {
            for (int i = 0; i < VisibleRows; i++)
            {
                int index = scroll + i;
                float y = FirstRowY - i * RowPitch;
                drawnRowIndices.Add(index < rows.Count ? index : -1);
                drawnRowY.Add(y);
                if (index >= rows.Count)
                {
                    continue;
                }
                Row row = rows[index];
                if (row.IsHeader)
                {
                    // A header is a label with a rule under it, not a clickable row.
                    ViewUtil.MakeText3D(transform, "Header_" + index,
                        new Vector2(PanelLeft + 0.18f, y), row.Label.ToUpperInvariant(),
                        90, 0.0135f, HeaderColor, TextOrder, TextAnchor.MiddleLeft);
                    ViewUtil.MakeRect(transform, "HeaderRule_" + index,
                        new Vector2(PanelCenterX, y - 0.13f),
                        new Vector2(PanelWidth - 0.34f, 0.015f), HeaderColor, ContentOrder);
                    continue;
                }
                if (index == selected)
                {
                    ViewUtil.MakeRect(transform, "Sel_" + index, new Vector2(PanelCenterX, y),
                        new Vector2(PanelWidth - 0.16f, RowHeight), SelectedRowColor, ContentOrder);
                }
                ViewUtil.MakeText3D(transform, "Row_" + index,
                    new Vector2(PanelLeft + 0.3f, y), row.Label,
                    90, 0.0145f, ItemColor, TextOrder, TextAnchor.MiddleLeft);
            }
        }

        /// <summary>A plain position indicator - the list is wheel/arrow driven, so the bar is
        /// there to say where you are, not to be dragged.</summary>
        private void DrawScrollbar(int rowCount, int scroll)
        {
            if (rowCount <= VisibleRows)
            {
                return;
            }
            float trackTop = FirstRowY + RowPitch * 0.5f;
            float trackBottom = FirstRowY - (VisibleRows - 0.5f) * RowPitch;
            float trackHeight = trackTop - trackBottom;
            float x = PanelRight - 0.12f;
            ViewUtil.MakeRect(transform, "ScrollTrack",
                new Vector2(x, (trackTop + trackBottom) * 0.5f),
                new Vector2(0.07f, trackHeight), ScrollTrackColor, ContentOrder);
            float visibleFraction = VisibleRows / (float)rowCount;
            float thumbHeight = Mathf.Max(0.25f, trackHeight * visibleFraction);
            int maxScroll = Mathf.Max(1, rowCount - VisibleRows);
            float t = Mathf.Clamp01(scroll / (float)maxScroll);
            float thumbY = Mathf.Lerp(trackTop - thumbHeight * 0.5f,
                trackBottom + thumbHeight * 0.5f, t);
            ViewUtil.MakeRect(transform, "ScrollThumb", new Vector2(x, thumbY),
                new Vector2(0.07f, thumbHeight), ScrollThumbColor, ContentOrder + 1);
        }

        private void DrawKnobs(IReadOnlyList<Knob> knobs)
        {
            for (int i = 0; i < knobs.Count; i++)
            {
                float y = FirstKnobY - i * KnobPitch;
                knobY.Add(y);
                ViewUtil.MakeRect(transform, "Knob_" + i, new Vector2(PanelCenterX, y),
                    new Vector2(PanelWidth - 0.16f, KnobHeight), KnobRowColor, ContentOrder);
                ViewUtil.MakeText3D(transform, "KnobLabel_" + i,
                    new Vector2(PanelLeft + 0.3f, y), knobs[i].Label,
                    90, 0.0135f, KnobLabelColor, TextOrder, TextAnchor.MiddleLeft);
                ViewUtil.MakeText3D(transform, "KnobValue_" + i,
                    new Vector2(PanelRight - 0.3f, y), knobs[i].Value,
                    90, 0.0135f, KnobValueColor, TextOrder, TextAnchor.MiddleRight);
            }
        }

        /// <summary>Catalogue index of the playable row under a world point, or -1 (headers,
        /// padding and everything outside the list all answer -1).</summary>
        public int RowAt(Vector2 world)
        {
            if (world.x < PanelLeft || world.x > PanelRight)
            {
                return -1;
            }
            for (int i = 0; i < drawnRowIndices.Count; i++)
            {
                if (drawnRowIndices[i] >= 0
                    && Mathf.Abs(world.y - drawnRowY[i]) <= RowPitch * 0.5f)
                {
                    return drawnRowIndices[i];
                }
            }
            return -1;
        }

        /// <summary>Knob index under a world point, or -1.</summary>
        public int KnobAt(Vector2 world)
        {
            if (world.x < PanelLeft || world.x > PanelRight)
            {
                return -1;
            }
            for (int i = 0; i < knobY.Count; i++)
            {
                if (Mathf.Abs(world.y - knobY[i]) <= KnobPitch * 0.5f)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>True while the point is anywhere on the panel. The controller uses this to
        /// tell "clicked the lab" from "clicked the board behind it".</summary>
        public bool PanelContains(Vector2 world)
        {
            return world.x >= PanelLeft && world.x <= PanelRight
                && world.y >= PanelBottom && world.y <= PanelTop;
        }

        public void Hide()
        {
            IsOpen = false;
            Clear();
        }

        private void Clear()
        {
            drawnRowIndices.Clear();
            drawnRowY.Clear();
            knobY.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
