// PURPOSE: The joker strip along the top of the screen - one panel per owned joker with
// its hotkey, name, live state, charges and sell value. Reads JokerInventory, never
// changes it (GameUiController owns input).
// NOTE FOR AGENTS: placeholder presentation like everything else under View/. The panel
// stays generic on purpose - it renders Joker.StatusText/ChargesLeft/SellValue rather than
// knowing any specific joker, so new jokers show up here for free.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    /// <summary>Canvas strip listing the player's jokers.</summary>
    public sealed class JokerBarView : MonoBehaviour
    {
        private const float PanelWidth = 232f;
        private const float PanelHeight = 92f;
        private const float PanelGap = 8f;

        private static readonly Color PanelColor = new Color(0.13f, 0.15f, 0.19f, 0.92f);
        private static readonly Color ReadyColor = new Color(0.20f, 0.34f, 0.24f, 0.95f);
        private static readonly Color TargetingColor = new Color(0.42f, 0.32f, 0.12f, 0.97f);
        private static readonly Color NameColor = new Color(1f, 0.93f, 0.72f);
        private static readonly Color BodyColor = new Color(0.80f, 0.84f, 0.90f);

        private readonly List<Panel> panels = new List<Panel>();
        private RectTransform root;

        private sealed class Panel
        {
            public GameObject Root;
            public Image Background;
            public Text Title;
            public Text Body;
        }

        /// <summary>Creates the strip under the HUD canvas. Call once.</summary>
        public void Build(Transform canvas)
        {
            var go = new GameObject("JokerBar");
            go.transform.SetParent(canvas, false);
            root = go.AddComponent<RectTransform>();
            root.anchorMin = new Vector2(1f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-16f, -16f);
            root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        }

        /// <summary>Redraws the strip. targetingInstanceId highlights the joker that is
        /// currently waiting for the player to pick a target.</summary>
        public void Refresh(GameSession session, int? targetingInstanceId)
        {
            if (root == null || session == null)
            {
                return;
            }
            IReadOnlyList<Joker> jokers = session.Jokers.Jokers;
            while (panels.Count < jokers.Count)
            {
                panels.Add(CreatePanel(panels.Count));
            }
            for (int i = 0; i < panels.Count; i++)
            {
                bool used = i < jokers.Count;
                panels[i].Root.SetActive(used);
                if (used)
                {
                    Fill(panels[i], jokers[i], i, session, targetingInstanceId);
                }
            }
        }

        /// <summary>Index of the joker panel under a screen point, or -1. The index matches
        /// GameSession.Jokers order (used to click a joker to use it / sell it).</summary>
        public int JokerIndexAt(Vector2 screenPos)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                if (!panels[i].Root.activeSelf)
                {
                    continue;
                }
                var rect = panels[i].Root.GetComponent<RectTransform>();
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null))
                {
                    return i;
                }
            }
            return -1;
        }

        private void Fill(Panel panel, Joker joker, int index, GameSession session,
            int? targetingInstanceId)
        {
            bool ready = session.Jokers.CanActivate(joker.InstanceId);
            bool targeting = targetingInstanceId.HasValue
                && targetingInstanceId.Value == joker.InstanceId;
            panel.Background.color = targeting ? TargetingColor : (ready ? ReadyColor : PanelColor);

            string hotkey = index < 9 ? "[" + (index + 1) + "] " : "    ";
            panel.Title.text = hotkey + joker.DisplayName;

            var line = new System.Text.StringBuilder();
            string status = joker.StatusText;
            if (!string.IsNullOrEmpty(status))
            {
                line.Append(status);
            }
            if (joker.ChargesPerRound > 0)
            {
                if (line.Length > 0)
                {
                    line.Append("   ");
                }
                line.Append("hak ").Append(joker.ChargesLeft).Append('/').Append(joker.ChargesPerRound);
            }
            if (line.Length > 0)
            {
                line.Append('\n');
            }
            line.Append("satış ").Append(joker.SellValue);
            if (joker.DisabledInOvertime)
            {
                line.Append("   (uzatmada kapalı)");
            }
            panel.Body.text = line.ToString();
        }

        private Panel CreatePanel(int index)
        {
            var go = new GameObject("Joker_" + index);
            go.transform.SetParent(root, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rect.anchoredPosition = new Vector2(0f, -index * (PanelHeight + PanelGap));

            var background = go.AddComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;

            Text title = MakeLabel(rect, "Title", new Vector2(10f, -8f), 20, NameColor,
                FontStyle.Bold, PanelWidth - 20f, 24f);
            Text body = MakeLabel(rect, "Body", new Vector2(10f, -34f), 17, BodyColor,
                FontStyle.Normal, PanelWidth - 20f, 52f);

            return new Panel { Root = go, Background = background, Title = title, Body = body };
        }

        private static Text MakeLabel(Transform parent, string name, Vector2 offset, int fontSize,
            Color color, FontStyle style, float width, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(width, height);
            return text;
        }
    }
}
