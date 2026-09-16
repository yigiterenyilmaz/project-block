// PURPOSE: The ANIMATION LAB panel (F3) - a scrollable catalogue of every animation in the
// game plus the condition knobs that modulate them. Presentation only: it draws rows and
// reports what was clicked, exactly like the other pickers under View/. The catalogue itself
// and every animation it fires live in GameUiController.AnimationLab.cs.
//
// DELIBERATELY A SIDE PANEL, not a fullscreen modal: nearly every animation in this game
// plays on the arena, the hand row or the two piles, so covering them would defeat the point.
// It starts in the empty right-hand column and leaves the whole board visible.
//
// AND IT STAYS ONE COLUMN. A scene whose subject lands under this panel is the SCENE's problem -
// it should be laid out somewhere visible - not a reason to spread the catalogue across the
// board it exists to let you watch.
//
// BUT IT CAN BE PICKED UP AND MOVED. Drag its title bar and it goes wherever you want; the place
// is remembered between sessions. The whole panel is built in world space under ONE root, so the
// drag is that root's position and nothing inside it knows: the layout constants below stay the
// panel's OWN coordinates, and every hit test converts a world point into them first (see
// ToPanel). Building the rows at moved positions instead would have put the offset in twelve
// places and left it out of the thirteenth.
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

        /// <summary>Where the panel has been dragged to, in world units, remembered across
        /// sessions. Zero is the resting place the constants below describe.</summary>
        private const string OffsetKeyX = "animlab.offset.x";
        private const string OffsetKeyY = "animlab.offset.y";

        /// <summary>How tall the drag handle is, measured down from the panel's top edge - the
        /// title and the help line. Anywhere else on the panel is a click, not a grab.</summary>
        private const float TitleBarHeight = 0.86f;

        // The panel RESTS under the joker bar (which is on the screen-space HUD canvas and
        // would otherwise draw over it) and clear of the board's right edge at 3.25. These are
        // the panel's own coordinates and do not move when it is dragged.
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

        private Vector2 offset;
        private bool offsetRead;

        /// <summary>
        /// Where the panel has been put, in world units off its resting place.
        ///
        /// Read from PlayerPrefs the first time it is asked for, so a place you chose in one
        /// session is still yours in the next - a tool you move every time you open it is a tool
        /// that has not really been moved.
        /// </summary>
        public Vector2 Offset
        {
            get
            {
                if (!offsetRead)
                {
                    offsetRead = true;
                    offset = new Vector2(PlayerPrefs.GetFloat(OffsetKeyX, 0f),
                        PlayerPrefs.GetFloat(OffsetKeyY, 0f));
                }
                return offset;
            }
            set
            {
                offsetRead = true;
                offset = value;
                transform.localPosition = new Vector3(value.x, value.y, 0f);
                PlayerPrefs.SetFloat(OffsetKeyX, value.x);
                PlayerPrefs.SetFloat(OffsetKeyY, value.y);
            }
        }

        /// <summary>Puts it back where it started.</summary>
        public void ResetPosition()
        {
            Offset = Vector2.zero;
        }

        /// <summary>
        /// A world point in the PANEL's own coordinates.
        ///
        /// Every hit test goes through this, which is what lets the layout constants stay
        /// absolute and the drag stay one number. Miss it in one place and that one control
        /// keeps answering at the panel's old position after it has been moved.
        /// </summary>
        private Vector2 ToPanel(Vector2 world)
        {
            return world - Offset;
        }

        /// <summary>
        /// True while the point is on the TITLE BAR - the strip that picks the panel up.
        ///
        /// Only the title bar, so dragging can never be confused with playing a row: the rows
        /// are what this panel is for, and a tool that sometimes moves when you meant to click
        /// is worse than one that does not move at all.
        /// </summary>
        public bool TitleBarContains(Vector2 world)
        {
            Vector2 p = ToPanel(world);
            return p.x >= PanelLeft && p.x <= PanelRight
                && p.y <= PanelTop && p.y >= PanelTop - TitleBarHeight;
        }

        /// <summary>
        /// Keeps the panel reachable: wherever it is dragged, a margin of it stays inside the
        /// camera, and the TITLE BAR always does - otherwise it could be dropped somewhere it
        /// can never be picked up from again.
        /// </summary>
        public Vector2 ClampOffset(Vector2 wanted, Camera cam)
        {
            if (cam == null)
            {
                return wanted;
            }
            float halfY = cam.orthographicSize;
            float halfX = halfY * cam.aspect;
            Vector3 eye = cam.transform.position;
            // How much of the panel must stay on screen. Simulated across 16:9, 4:3 and a
            // portrait phone: at every aspect and every corner this keeps the title bar fully
            // visible and this much of the panel's width with it.
            const float keep = 2f;
            float minX = eye.x - halfX + keep - PanelRight;
            float maxX = eye.x + halfX - keep - PanelLeft;
            // The top of the panel may not go above the screen, or the handle leaves with it.
            float maxY = eye.y + halfY - TitleBarHeight * 0.5f - PanelTop;
            float minY = eye.y - halfY + keep - PanelTop;
            return new Vector2(Mathf.Clamp(wanted.x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX)),
                Mathf.Clamp(wanted.y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY)));
        }

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
            // The root carries the drag, so a rebuild cannot lose it.
            transform.localPosition = new Vector3(Offset.x, Offset.y, 0f);

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
                Loc.Pick("click plays  -  space replays  -  wheel scrolls  -  DRAG THIS BAR",
                    "tık oynatır  -  boşluk tekrarlar  -  tekerlek kaydırır  -  BU ÇUBUĞU SÜRÜKLE"),
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
            Vector2 p = ToPanel(world);
            if (p.x < PanelLeft || p.x > PanelRight)
            {
                return -1;
            }
            for (int i = 0; i < drawnRowIndices.Count; i++)
            {
                if (drawnRowIndices[i] >= 0
                    && Mathf.Abs(p.y - drawnRowY[i]) <= RowPitch * 0.5f)
                {
                    return drawnRowIndices[i];
                }
            }
            return -1;
        }

        /// <summary>Knob index under a world point, or -1.</summary>
        public int KnobAt(Vector2 world)
        {
            Vector2 p = ToPanel(world);
            if (p.x < PanelLeft || p.x > PanelRight)
            {
                return -1;
            }
            for (int i = 0; i < knobY.Count; i++)
            {
                if (Mathf.Abs(p.y - knobY[i]) <= KnobPitch * 0.5f)
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
            Vector2 p = ToPanel(world);
            return p.x >= PanelLeft && p.x <= PanelRight
                && p.y >= PanelBottom && p.y <= PanelTop;
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
