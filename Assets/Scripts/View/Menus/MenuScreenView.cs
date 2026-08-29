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
            Show(title, subtitle, entries, MenuSkin.Backdrop, false);
        }

        /// <summary>As above, but the title WAVES - see MenuTitleWave. Only the game's own name
        /// on the title screen asks for this; a heading that says what screen you are on should
        /// hold still.</summary>
        public void Show(string title, string subtitle, IReadOnlyList<MenuEntry> entries,
            bool waveTitle)
        {
            Show(title, subtitle, entries, MenuSkin.Backdrop, waveTitle);
        }

        /// <summary>As above, with an explicit backdrop - a menu opened over a live run passes
        /// MenuSkin.OverlayBackdrop so the board stays readable behind it.</summary>
        public void Show(string title, string subtitle, IReadOnlyList<MenuEntry> entries,
            Color backdropColor)
        {
            Show(title, subtitle, entries, backdropColor, false);
        }

        /// <summary>The full form; the three above are all shorthands for it.</summary>
        public void Show(string title, string subtitle, IReadOnlyList<MenuEntry> entries,
            Color backdropColor, bool waveTitle)
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

            Vector2 titleCenter = new Vector2(0f, y - TitleHeight * 0.5f);
            if (waveTitle)
            {
                MakeWaveTitle(root, titleCenter, title);
            }
            else
            {
                MakeText(root, "Title", titleCenter,
                    new Vector2(MenuSkin.ButtonWidth * 2f, TitleHeight), title,
                    MenuSkin.TitleFontSize, MenuSkin.Title, true);
            }
            y -= TitleHeight;
            if (hasSubtitle)
            {
                MakeText(root, "Subtitle", new Vector2(0f, y - SubtitleHeight * 0.5f),
                    new Vector2(MenuSkin.ButtonWidth * 2f, SubtitleHeight), subtitle,
                    MenuSkin.SubtitleFontSize, MenuSkin.Subtitle, false);
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
                MenuSkin.TitleFontSize, MenuSkin.Title, true);
            y -= TitleHeight + gap;

            Text text = MakeText(root, "Body", new Vector2(0f, y - bodyHeight * 0.5f),
                new Vector2(BodyWidth, bodyHeight), body,
                MenuSkin.BodyFontSize, MenuSkin.Label, false);
            text.alignment = TextAnchor.UpperLeft;
            y -= bodyHeight;

            if (hintHeight > 0f)
            {
                MakeText(root, "Hint", new Vector2(0f, y - hintHeight * 0.5f),
                    new Vector2(BodyWidth, hintHeight), hint,
                    MenuSkin.SubtitleFontSize, MenuSkin.Subtitle, false);
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
                row.Background.color = BackgroundColor(row.Enabled, highlighted);
                row.Label.color = !row.Enabled
                    ? MenuSkin.LabelDisabled
                    : highlighted ? MenuSkin.LabelSelected : MenuSkin.Label;
                row.Accent.enabled = highlighted;
            }
        }

        /// <summary>Where the highlight bar sits, and how tall it is, ON ART. Against a flat
        /// rectangle it can hug the very edge; the painted plate has a raised frame about 12
        /// units thick that curves further in towards the ends, so on art the bar moves inside
        /// the sunken panel and shortens. At its flat-colour 58 tall its tips would have run
        /// out over the bevel, which is the one place a straight bar reads as a mistake.</summary>
        private const float AccentInsetArt = 22f;

        private const float AccentHeightArt = 40f;

        /// <summary>How far under the label a note sits ON ART. The plate's sunken panel reaches
        /// only 26 units from the centre to its own edge, and at the flat-colour -20 a note's
        /// descenders sat out on the bottom bevel.</summary>
        private const float NoteOffsetArt = -15f;

        /// <summary>The colour a row's background is drawn in. With art loaded this is a TINT
        /// multiplied into the painted plate rather than a fill, so the two sets are not
        /// interchangeable - see MenuSkin.ButtonTint.</summary>
        private static Color BackgroundColor(bool enabled, bool highlighted)
        {
            bool art = MenuSkin.ButtonSprite != null;
            if (!enabled)
            {
                return art ? MenuSkin.ButtonTintDisabled : MenuSkin.ButtonDisabled;
            }
            if (highlighted)
            {
                return art ? MenuSkin.ButtonTintHover : MenuSkin.ButtonHover;
            }
            return art ? MenuSkin.ButtonTint : MenuSkin.Button;
        }

        private Row MakeRow(MenuEntry entry, Vector2 center)
        {
            var row = new Row();
            row.Enabled = entry.Enabled;
            row.Background = MakeImage(root, "Entry_" + entry.Label, center,
                new Vector2(MenuSkin.ButtonWidth, MenuSkin.ButtonHeight),
                BackgroundColor(entry.Enabled, false), MenuSkin.ButtonSprite);
            row.Root = row.Background.gameObject;
            bool art = MenuSkin.ButtonSprite != null;
            float accentInset = art ? AccentInsetArt : MenuSkin.AccentWidth * 0.5f;
            float accentHeight = art ? AccentHeightArt : MenuSkin.ButtonHeight - 18f;
            row.Accent = MakeImage(row.Root.transform, "Accent",
                new Vector2(-MenuSkin.ButtonWidth * 0.5f + accentInset
                    + MenuSkin.AccentWidth * 0.5f, 0f),
                new Vector2(MenuSkin.AccentWidth, accentHeight),
                MenuSkin.Accent, null);
            row.Accent.enabled = false; // switched on for the highlighted row only

            bool hasNote = !string.IsNullOrEmpty(entry.Note);
            // With a note the label lifts a little so the two share the button cleanly.
            float labelOffset = hasNote ? 12f : 0f;
            row.Label = MakeText(row.Root.transform, "Label", new Vector2(0f, labelOffset),
                new Vector2(MenuSkin.ButtonWidth, MenuSkin.ButtonHeight * 0.6f), entry.Label,
                MenuSkin.LabelFontSize, MenuSkin.Label, true);
            if (hasNote)
            {
                MakeText(row.Root.transform, "Note",
                    new Vector2(0f, art ? NoteOffsetArt : -20f),
                    new Vector2(MenuSkin.ButtonWidth, MenuSkin.ButtonHeight * 0.4f), entry.Note,
                    MenuSkin.NoteFontSize, MenuSkin.Note, false);
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

        /// <summary>The title as ONE TEXT PER LETTER, so each can be lifted on its own.
        ///
        /// Laid out on the font's own ADVANCE WIDTHS rather than by measuring each letter's
        /// preferred width. Two reasons, and the second is the one that bites: a Text asked how
        /// wide " " is answers nothing, because the legacy text generator trims whitespace at
        /// the end of a line - so the gap in a two-word name would close up. Advances also
        /// reproduce ordinary text layout exactly, since flowing text is placed by the same
        /// numbers, and the waving title therefore sits at the width the still one had.
        ///
        /// The space gets a letter slot of its own with no glyph in it. That is what carries
        /// the bump ACROSS the gap instead of teleporting it to the second word.</summary>
        private static void MakeWaveTitle(Transform parent, Vector2 center, string title)
        {
            var go = new GameObject("Title");
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = new Vector2(MenuSkin.ButtonWidth * 2f, TitleHeight);

            Font font = ViewUtil.UiFontBold;
            // A dynamic font knows nothing about a character until it is asked to draw it, and
            // an unrequested glyph reports an advance of zero - every letter would stack on the
            // same spot.
            font.RequestCharactersInTexture(title, MenuSkin.TitleFontSize, FontStyle.Normal);

            var advances = new float[title.Length];
            float total = 0f;
            for (int i = 0; i < title.Length; i++)
            {
                CharacterInfo info;
                advances[i] = font.GetCharacterInfo(title[i], out info,
                    MenuSkin.TitleFontSize, FontStyle.Normal)
                    ? info.advance
                    : MenuSkin.TitleFontSize * 0.5f;
                total += advances[i];
            }

            var letters = new RectTransform[title.Length];
            float x = -total * 0.5f;
            for (int i = 0; i < title.Length; i++)
            {
                Text letter = MakeText(rect, "L" + i,
                    new Vector2(x + advances[i] * 0.5f, 0f),
                    new Vector2(advances[i], TitleHeight), title[i].ToString(),
                    MenuSkin.TitleFontSize, MenuSkin.Title, true);
                // A glyph may paint wider than the advance it is placed on (Fredoka's Q and J
                // both do); without this the box would clip its own letter.
                letter.horizontalOverflow = HorizontalWrapMode.Overflow;
                letter.verticalOverflow = VerticalWrapMode.Overflow;
                letters[i] = letter.rectTransform;
                x += advances[i];
            }
            go.AddComponent<MenuTitleWave>().Setup(letters);
        }

        private static Image MakeImage(Transform parent, string name, Vector2 center,
            Vector2 size, Color color, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite; // null = flat colour (see MenuSkin)
            // A sprite that carries an importer border is asking to keep its frame and stretch
            // only its middle. Simple would scale the whole bitmap and round the corners off
            // by the same 6:1 the button is wider than the art.
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;
            image.raycastTarget = false;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            return image;
        }

        /// <summary>`bold` picks the FACE, not a style flag: Fredoka Bold is its own cut, and
        /// asking a dynamic font to embolden the SemiBold on top of it gives Unity's synthetic
        /// smear instead. Headings and button labels are bold, everything read at length is
        /// not - a whole how-to-play page set in Bold is a wall.</summary>
        private static Text MakeText(Transform parent, string name, Vector2 center,
            Vector2 size, string content, int fontSize, Color color, bool bold)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = bold ? ViewUtil.UiFontBold : ViewUtil.UiFont;
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
