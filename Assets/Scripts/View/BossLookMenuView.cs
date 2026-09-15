// PURPOSE: The F7 BOSS LOOK LAB panel - a compact list on its own overlay canvas down the right
// side, so the board it is previewing on stays in view. It only DRAWS rows and answers "which row
// is under this point"; what the rows mean lives in GameUiController.BossIdentity.
//
// Its own canvas, sorted between the HUD (0) and the tooltip (100), because an intro dims the HUD
// canvas and the panel must not dim with it. Hit-testing is by hand like every other bar here -
// there is no EventSystem in the scene.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    public sealed class BossLookMenuView : MonoBehaviour
    {
        private const int CanvasOrder = 90;
        private const float PanelWidth = 580f;
        private const float PanelHeight = 960f;
        private const float RowHeight = 34f;
        private const float RowsTop = 70f;
        private const int MaxRows = 18;

        private static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.09f, 0.95f);
        private static readonly Color EdgeColor = new Color(0.40f, 0.14f, 0.18f, 1f);
        private static readonly Color TitleColor = new Color(1f, 0.62f, 0.58f);
        private static readonly Color RowColor = new Color(0.84f, 0.87f, 0.93f);
        private static readonly Color HeaderColor = new Color(0.55f, 0.60f, 0.70f);
        private static readonly Color HighlightColor = new Color(0.55f, 0.14f, 0.20f, 0.55f);
        private static readonly Color DescColor = new Color(0.78f, 0.82f, 0.88f);

        private Canvas canvas;
        private RectTransform panel;
        private Image highlight;
        private Text description;
        private Text footer;
        private readonly List<Text> rows = new List<Text>();

        public bool IsOpen { get; private set; }

        public void Build()
        {
            var canvasGo = new GameObject("BossLookCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panel = (RectTransform)panelGo.transform;
            panel.SetParent(canvasGo.transform, false);
            panel.anchorMin = new Vector2(1f, 0.5f);
            panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.anchoredPosition = new Vector2(-20f, -20f);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            Plate(panel, "Edge", EdgeColor, 2f);
            Plate(panel, "Fill", PanelColor, 0f);

            var hl = new GameObject("Highlight", typeof(RectTransform));
            hl.transform.SetParent(panel, false);
            highlight = hl.AddComponent<Image>();
            highlight.color = HighlightColor;
            highlight.raycastTarget = false;
            RectTransform hr = highlight.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f);
            hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.sizeDelta = new Vector2(-24f, RowHeight);

            Text title = Label(panel, "Title", 26, TitleColor, TextAnchor.UpperLeft, true);
            Place(title.rectTransform, 22f, 18f, PanelWidth - 44f, 40f);
            title.text = Loc.Pick("BOSS LOOK LAB  (F7)", "PATRON GÖRÜNÜM LAB  (F7)");

            for (int i = 0; i < MaxRows; i++)
            {
                Text row = Label(panel, "Row" + i, 21, RowColor, TextAnchor.MiddleLeft, false);
                Place(row.rectTransform, 26f, RowsTop + i * RowHeight, PanelWidth - 52f, RowHeight);
                rows.Add(row);
            }
            description = Label(panel, "Description", 18, DescColor, TextAnchor.UpperLeft, false);
            footer = Label(panel, "Footer", 16, HeaderColor, TextAnchor.LowerLeft, false);
            RectTransform fr = footer.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f);
            fr.anchorMax = new Vector2(0f, 0f);
            fr.pivot = new Vector2(0f, 0f);
            fr.anchoredPosition = new Vector2(22f, 16f);
            fr.sizeDelta = new Vector2(PanelWidth - 44f, 80f);
            canvasGo.SetActive(false);
        }

        public void Show()
        {
            IsOpen = true;
            SetPanelVisible(true);
        }

        public void Hide()
        {
            IsOpen = false;
            SetPanelVisible(false);
        }

        /// <summary>Takes the panel off the screen without closing the lab - while an intro plays,
        /// the preview has to be judged without a debug list on top of it.</summary>
        public void SetPanelVisible(bool on)
        {
            if (canvas != null)
            {
                canvas.gameObject.SetActive(on && IsOpen);
            }
        }

        public void Render(IList<string> rowTexts, int selected, string descriptionText,
            string footerText)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].text = i < rowTexts.Count ? rowTexts[i] : string.Empty;
                rows[i].color = i < rowTexts.Count && rowTexts[i].StartsWith("—") ? HeaderColor : RowColor;
            }
            highlight.rectTransform.anchoredPosition = new Vector2(0f, -(RowsTop + selected * RowHeight));
            float descTop = RowsTop + rowTexts.Count * RowHeight + 14f;
            Place(description.rectTransform, 22f, descTop, PanelWidth - 44f,
                PanelHeight - descTop - 100f);
            description.text = descriptionText;
            footer.text = footerText;
        }

        /// <summary>The row under a screen point, or -1; <paramref name="rightHalf"/> says which
        /// side of it (a value row steps back on the left, forward on the right).</summary>
        public int RowAt(Vector2 screen, int rowCount, out bool rightHalf)
        {
            rightHalf = false;
            if (!IsOpen || canvas == null || !canvas.gameObject.activeSelf)
            {
                return -1;
            }
            for (int i = 0; i < rowCount && i < rows.Count; i++)
            {
                RectTransform r = rows[i].rectTransform;
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(r, screen, null, out local)
                    && r.rect.Contains(local))
                {
                    rightHalf = local.x > r.rect.center.x;
                    return i;
                }
            }
            return -1;
        }

        public bool PanelContains(Vector2 screen)
        {
            return IsOpen && canvas != null && canvas.gameObject.activeSelf
                && RectTransformUtility.RectangleContainsScreenPoint(panel, screen, null);
        }

        private static void Plate(RectTransform parent, string name, Color color, float bleed)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = ViewUtil.RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-bleed, -bleed);
            rect.offsetMax = new Vector2(bleed, bleed);
        }

        private static Text Label(RectTransform parent, string name, int size, Color color,
            TextAnchor anchor, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = ViewUtil.UiFontFor(bold ? FontStyle.Bold : FontStyle.Normal);
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Hangs a rect from the panel's top-left, <paramref name="top"/> pixels down.</summary>
        private static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
