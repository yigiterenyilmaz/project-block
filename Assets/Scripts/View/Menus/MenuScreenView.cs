// PURPOSE: The one reusable menu screen - a full-screen backdrop, a header, and a vertical
// list of entries. Every menu in the game (title, pause, settings, how-to-play, run summary)
// is this view with a different header and entry list; none of them subclass it.
//
// INPUT: like JokerBarView / PowerBarView, this hit-tests clicks itself with
// RectTransformUtility instead of using uGUI Buttons - the project has no EventSystem, and
// adding one would drag in Unity.InputSystem.ForUI for no gain. Mouse hover and keyboard
// selection drive the SAME index, so the two never disagree.
//
// The view owns no game state and reads no rules; GameUiController decides what an entry does.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    /// <summary>One row of a menu. A disabled entry is drawn greyed and cannot be picked or
    /// selected, but is still shown - it tells the player the feature exists.</summary>
    public struct MenuEntry
    {
        public string Label;
        public bool Enabled;

        /// <summary>Small line under the label: why an entry is disabled, or what a choice the
        /// player might hesitate over actually does. Rendered whenever it is set.</summary>
        public string Note;

        public static MenuEntry Of(string label)
        {
            var entry = new MenuEntry();
            entry.Label = label;
            entry.Enabled = true;
            return entry;
        }

        /// <summary>An enabled entry that explains itself - for choices where guessing wrong
        /// costs the player something (leaving a run, restarting one).</summary>
        public static MenuEntry Of(string label, string note)
        {
            MenuEntry entry = Of(label);
            entry.Note = note;
            return entry;
        }

        public static MenuEntry Locked(string label, string note)
        {
            var entry = new MenuEntry();
            entry.Label = label;
            entry.Enabled = false;
            entry.Note = note;
            return entry;
        }
    }

    /// <summary>Full-screen canvas menu: header + a column of entries.</summary>
    public sealed class MenuScreenView : MonoBehaviour
    {
        private const float TitleHeight = 90f;
        private const float SubtitleHeight = 34f;

        // Reading-screen (ShowText) body block.
        private const float BodyWidth = 1180f;
        private const float BodyHeight = 620f;

        /// <summary>The navigation line under a reading screen's body.</summary>
        private const float HintHeight = 32f;

        private sealed class Row
        {
            public GameObject Root;
            public Image Background;
            public Image Accent;
            public Text Label;
            public bool Enabled;
        }

        private readonly List<Row> rows = new List<Row>();
        private RectTransform root;

        public bool IsOpen { get; private set; }

        /// <summary>Index of the highlighted entry, shared by the mouse and the arrow keys.
        /// -1 when nothing is selectable.</summary>
        public int Selected { get; private set; }

        /// <summary>Creates the (hidden) screen under the HUD canvas. Call once.</summary>
        public void Build(Transform canvas)
        {
            var go = new GameObject("MenuScreen");
            go.transform.SetParent(canvas, false);
            root = go.AddComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            go.SetActive(false);
        }

        /// <summary>Draws a menu over an opaque backdrop, replacing whatever was on screen.</summary>
        public void Show(string title, string subtitle, IReadOnlyList<MenuEntry> entries)
        {
            Show(title, subtitle, entries, MenuSkin.Backdrop);
        }

        /// <summary>As above, with an explicit backdrop - a menu opened over a live run passes
        /// MenuSkin.OverlayBackdrop so the board stays readable behind it.</summary>
        public void Show(string title, string subtitle, IReadOnlyList<MenuEntry> entries,
            Color backdropColor)
        {
            if (root == null)
            {
                return;
            }
            Clear();
            IsOpen = true;
            root.gameObject.SetActive(true);
            // Drawn above the joker/power bars and the HUD text whatever order BuildViews
            // happened to create them in.
            root.SetAsLastSibling();

            // Stretched over the whole screen rather than sized, so it covers any resolution.
            RectTransform backdrop = MakeImage(root, "Backdrop", Vector2.zero, Vector2.zero,
                backdropColor, MenuSkin.BackdropSprite).rectTransform;
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = Vector2.zero;
            backdrop.offsetMax = Vector2.zero;

            bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
            float buttons = entries.Count * MenuSkin.ButtonHeight
                + Mathf.Max(0, entries.Count - 1) * MenuSkin.ButtonGap;
            float header = TitleHeight + (hasSubtitle ? SubtitleHeight : 0f);
            float y = (header + MenuSkin.HeaderGap + buttons) * 0.5f;

            MakeText(root, "Title", new Vector2(0f, y - TitleHeight * 0.5f),
                new Vector2(MenuSkin.ButtonWidth * 2f, TitleHeight), title,
                MenuSkin.TitleFontSize, MenuSkin.Title);
            y -= TitleHeight;
            if (hasSubtitle)
            {
                MakeText(root, "Subtitle", new Vector2(0f, y - SubtitleHeight * 0.5f),
                    new Vector2(MenuSkin.ButtonWidth * 2f, SubtitleHeight), subtitle,
                    MenuSkin.SubtitleFontSize, MenuSkin.Subtitle);
                y -= SubtitleHeight;
            }
            y -= MenuSkin.HeaderGap;

            for (int i = 0; i < entries.Count; i++)
            {
                rows.Add(MakeRow(entries[i], new Vector2(0f, y - MenuSkin.ButtonHeight * 0.5f)));
                y -= MenuSkin.ButtonHeight + MenuSkin.ButtonGap;
            }
            Selected = FirstEnabled();
            ApplyRowColors();
        }

        /// <summary>Draws a READING screen: a title, a left-aligned block of text, and entries
        /// underneath. The entries are normal rows, so the usual click/arrow/Enter input path
        /// drives this screen too. The body shrinks as entries are added, so the whole thing
        /// keeps fitting on screen.</summary>
        public void ShowText(string title, string body, string hint,
            IReadOnlyList<MenuEntry> entries, Color backdropColor)
        {
            if (root == null)
            {
                return;
            }
            Clear();
            IsOpen = true;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();

            RectTransform backdrop = MakeImage(root, "Backdrop", Vector2.zero, Vector2.zero,
                backdropColor, MenuSkin.BackdropSprite).rectTransform;
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = Vector2.zero;
            backdrop.offsetMax = Vector2.zero;

            float gap = MenuSkin.HeaderGap * 0.5f;
            float buttons = entries.Count * MenuSkin.ButtonHeight
                + Mathf.Max(0, entries.Count - 1) * MenuSkin.ButtonGap;
            // Every extra entry takes its space out of the body, so the block never runs off
            // the bottom of the screen however many buttons a screen ends up with.
            float bodyHeight = Mathf.Max(200f,
                BodyHeight - (buttons - MenuSkin.ButtonHeight));
            float hintHeight = string.IsNullOrEmpty(hint) ? 0f : HintHeight;
            float y = (TitleHeight + gap + bodyHeight + hintHeight + gap + buttons) * 0.5f;

            MakeText(root, "Title", new Vector2(0f, y - TitleHeight * 0.5f),
                new Vector2(BodyWidth, TitleHeight), title,
                MenuSkin.TitleFontSize, MenuSkin.Title);
            y -= TitleHeight + gap;

            Text text = MakeText(root, "Body", new Vector2(0f, y - bodyHeight * 0.5f),
                new Vector2(BodyWidth, bodyHeight), body,
                MenuSkin.BodyFontSize, MenuSkin.Label);
            text.alignment = TextAnchor.UpperLeft;
            y -= bodyHeight;

            if (hintHeight > 0f)
            {
                MakeText(root, "Hint", new Vector2(0f, y - hintHeight * 0.5f),
                    new Vector2(BodyWidth, hintHeight), hint,
                    MenuSkin.SubtitleFontSize, MenuSkin.Subtitle);
                y -= hintHeight;
            }
            y -= gap;

            for (int i = 0; i < entries.Count; i++)
            {
                rows.Add(MakeRow(entries[i], new Vector2(0f, y - MenuSkin.ButtonHeight * 0.5f)));
                y -= MenuSkin.ButtonHeight + MenuSkin.ButtonGap;
            }
            Selected = FirstEnabled();
            ApplyRowColors();
        }

        public void Hide()
        {
            IsOpen = false;
            Clear();
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        /// <summary>Index of the ENABLED entry under a screen point, or -1. Disabled entries
        /// deliberately return -1 so callers never have to re-check.</summary>
        public int EntryAt(Vector2 screenPos)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (!rows[i].Enabled)
                {
                    continue;
                }
                var rect = rows[i].Root.GetComponent<RectTransform>();
                // Screen-space-overlay canvas, so no camera is needed for the test.
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Moves the highlight with the mouse. Off every entry, the highlight stays
        /// where it was - so moving the mouse away never leaves the menu with nothing chosen.</summary>
        public void UpdateHover(Vector2 screenPos)
        {
            int hovered = EntryAt(screenPos);
            if (hovered >= 0 && hovered != Selected)
            {
                Selected = hovered;
                ApplyRowColors();
            }
        }

        /// <summary>Arrow-key movement: steps to the next ENABLED entry, wrapping around.</summary>
        public void MoveSelection(int delta)
        {
            if (rows.Count == 0 || delta == 0)
            {
                return;
            }
            int index = Selected < 0 ? 0 : Selected;
            // Bounded by the row count so an all-disabled menu cannot spin forever.
            for (int step = 0; step < rows.Count; step++)
            {
                index = (index + delta + rows.Count) % rows.Count;
                if (rows[index].Enabled)
                {
                    Selected = index;
                    ApplyRowColors();
                    return;
                }
            }
        }

        /// <summary>The currently selected entry if it can be picked, else -1.</summary>
        public int Activate()
        {
            return Selected >= 0 && Selected < rows.Count && rows[Selected].Enabled
                ? Selected
                : -1;
        }

        private int FirstEnabled()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Enabled)
                {
                    return i;
                }
            }
            return -1;
        }

        private void ApplyRowColors()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                bool highlighted = row.Enabled && i == Selected;
                row.Background.color = !row.Enabled
                    ? MenuSkin.ButtonDisabled
                    : highlighted ? MenuSkin.ButtonHover : MenuSkin.Button;
                row.Label.color = !row.Enabled
                    ? MenuSkin.LabelDisabled
                    : highlighted ? MenuSkin.LabelSelected : MenuSkin.Label;
                row.Accent.enabled = highlighted;
            }
        }

        private Row MakeRow(MenuEntry entry, Vector2 center)
        {
            var row = new Row();
            row.Enabled = entry.Enabled;
            row.Background = MakeImage(root, "Entry_" + entry.Label, center,
                new Vector2(MenuSkin.ButtonWidth, MenuSkin.ButtonHeight),
                MenuSkin.Button, MenuSkin.ButtonSprite);
            row.Root = row.Background.gameObject;
            row.Accent = MakeImage(row.Root.transform, "Accent",
                new Vector2(-MenuSkin.ButtonWidth * 0.5f + MenuSkin.AccentWidth, 0f),
                new Vector2(MenuSkin.AccentWidth, MenuSkin.ButtonHeight - 18f),
                MenuSkin.Accent, null);
            row.Accent.enabled = false; // switched on for the highlighted row only

            bool hasNote = !string.IsNullOrEmpty(entry.Note);
            // With a note the label lifts a little so the two share the button cleanly.
            float labelOffset = hasNote ? 12f : 0f;
            row.Label = MakeText(row.Root.transform, "Label", new Vector2(0f, labelOffset),
                new Vector2(MenuSkin.ButtonWidth, MenuSkin.ButtonHeight * 0.6f), entry.Label,
                MenuSkin.LabelFontSize, MenuSkin.Label);
            if (hasNote)
            {
                MakeText(row.Root.transform, "Note", new Vector2(0f, -20f),
                    new Vector2(MenuSkin.ButtonWidth, MenuSkin.ButtonHeight * 0.4f), entry.Note,
                    MenuSkin.NoteFontSize, MenuSkin.Note);
            }
            return row;
        }

        private void Clear()
        {
            rows.Clear();
            Selected = -1;
            if (root == null)
            {
                return;
            }
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                // Destroy only takes effect at the end of the frame, so a menu re-shown this
                // frame (a language switch, a screen change) would draw twice over itself.
                // Deactivating first makes the swap clean.
                child.SetActive(false);
                Destroy(child);
            }
        }

        private static Image MakeImage(Transform parent, string name, Vector2 center,
            Vector2 size, Color color, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite; // null = flat colour (see MenuSkin)
            image.raycastTarget = false;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            return image;
        }

        private static Text MakeText(Transform parent, string name, Vector2 center,
            Vector2 size, string content, int fontSize, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = content;
            text.raycastTarget = false;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            return text;
        }
    }
}
