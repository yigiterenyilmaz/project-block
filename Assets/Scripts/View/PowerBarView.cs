// PURPOSE: The power strip on the left side of the screen - below the info text, above
// the discard pile. One clickable panel per owned power with its name, charge state and
// live status. Reads PowerInventory, never changes it (GameUiController owns input).
// NOTE FOR AGENTS: placeholder presentation like everything else under View/. The panel
// stays generic on purpose - it renders Power.StatusText/Charged/BaseSellValue rather
// than knowing any specific power, so new powers show up here for free.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    /// <summary>Canvas strip listing the player's powers.</summary>
    public sealed class PowerBarView : MonoBehaviour
    {
        private const float PanelWidth = 232f;
        private const float PanelHeight = 84f;
        private const float PanelGap = 8f;

        private static readonly Color PanelColor = new Color(0.13f, 0.15f, 0.19f, 0.92f);
        private static readonly Color ReadyColor = new Color(0.12f, 0.30f, 0.34f, 0.95f);
        private static readonly Color SpentColor = new Color(0.16f, 0.16f, 0.17f, 0.85f);
        private static readonly Color TargetingColor = new Color(0.42f, 0.32f, 0.12f, 0.97f);
        private static readonly Color NameColor = new Color(0.72f, 0.96f, 0.98f);
        private static readonly Color BodyColor = new Color(0.80f, 0.84f, 0.90f);
        private static readonly Color SpentTextColor = new Color(0.55f, 0.58f, 0.62f);

        private readonly List<Panel> panels = new List<Panel>();
        private RectTransform root;

        private sealed class Panel
        {
            public GameObject Root;
            public Image Background;
            public Text Title;
            public Text Body;
            public int InstanceId = -1;
        }

        /// <summary>Creates the strip under the HUD canvas. Call once.</summary>
        public void Build(Transform canvas)
        {
            var go = new GameObject("PowerBar");
            go.transform.SetParent(canvas, false);
            root = go.AddComponent<RectTransform>();
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(16f, -256f);
            root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        }

        /// <summary>Redraws the strip. targetingInstanceId highlights the power that is
        /// currently waiting for the player to pick a target.</summary>
        public void Refresh(GameSession session, int? targetingInstanceId)
        {
            if (root == null || session == null)
            {
                return;
            }
            IReadOnlyList<Power> powers = session.Powers.Powers;
            while (panels.Count < powers.Count)
            {
                panels.Add(CreatePanel(panels.Count));
            }
            for (int i = 0; i < panels.Count; i++)
            {
                bool used = i < powers.Count;
                panels[i].Root.SetActive(used);
                if (used)
                {
                    Fill(panels[i], powers[i], session, targetingInstanceId);
                }
            }
        }

        /// <summary>Index of the power panel under a screen point, or -1. The index matches
        /// GameSession.Powers order (used to click a power to use it / sell it).</summary>
        public int PowerIndexAt(Vector2 screenPos)
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

        /// <summary>Screen-space center of a panel (for spawning fx near it), or null.</summary>
        public Vector2? PanelScreenCenter(int index)
        {
            if (index < 0 || index >= panels.Count || !panels[index].Root.activeSelf)
            {
                return null;
            }
            // Screen-space-overlay canvas: rect corners ARE screen pixels.
            var corners = new Vector3[4];
            panels[index].Root.GetComponent<RectTransform>().GetWorldCorners(corners);
            return (Vector2)((corners[0] + corners[2]) * 0.5f);
        }

        /// <summary>Quick scale pulse on the panel showing that power (use feedback).</summary>
        public void PulsePower(int instanceId)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i].Root.activeSelf && panels[i].InstanceId == instanceId)
                {
                    StartCoroutine(PulseRoutine(panels[i].Root.transform));
                    return;
                }
            }
        }

        /// <summary>Sold feedback: the panel shrinks away, then the strip refreshes to the
        /// post-sale inventory. Call AFTER PowerInventory.Sell.</summary>
        public void AnimatePowerSold(int index, GameSession session)
        {
            if (index < 0 || index >= panels.Count || !panels[index].Root.activeSelf)
            {
                Refresh(session, null);
                return;
            }
            StartCoroutine(ShrinkThenRefresh(panels[index].Root.transform, session));
        }

        private System.Collections.IEnumerator PulseRoutine(Transform target)
        {
            const float duration = 0.22f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);
                target.localScale = Vector3.one * (1f + 0.14f * k);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        private System.Collections.IEnumerator ShrinkThenRefresh(Transform target, GameSession session)
        {
            const float duration = 0.14f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(time / duration);
                target.localScale = new Vector3(k, k, 1f);
                yield return null;
            }
            target.localScale = Vector3.one;
            Refresh(session, null);
        }

        private void Fill(Panel panel, Power power, GameSession session, int? targetingInstanceId)
        {
            panel.InstanceId = power.InstanceId;
            bool ready = session.Powers.CanBeginUse(power.InstanceId);
            bool targeting = targetingInstanceId.HasValue
                && targetingInstanceId.Value == power.InstanceId;
            panel.Background.color = targeting ? TargetingColor
                : !power.Charged ? SpentColor
                : ready ? ReadyColor : PanelColor;
            panel.Title.color = power.Charged ? NameColor : SpentTextColor;
            panel.Body.color = power.Charged ? BodyColor : SpentTextColor;

            panel.Title.text = power.DisplayName;

            var line = new System.Text.StringBuilder();
            string status = power.StatusText;
            if (!string.IsNullOrEmpty(status))
            {
                line.Append(status).Append("   ");
            }
            line.Append(power.Charged ? "dolu" : "boş (temizlik doldurur)");
            line.Append('\n').Append("satış ").Append(power.BaseSellValue);
            panel.Body.text = line.ToString();
        }

        private Panel CreatePanel(int index)
        {
            var go = new GameObject("Power_" + index);
            go.transform.SetParent(root, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rect.anchoredPosition = new Vector2(0f, -index * (PanelHeight + PanelGap));

            var background = go.AddComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;

            Text title = MakeLabel(rect, "Title", new Vector2(10f, -8f), 20, NameColor,
                FontStyle.Bold, PanelWidth - 20f, 24f);
            Text body = MakeLabel(rect, "Body", new Vector2(10f, -34f), 17, BodyColor,
                FontStyle.Normal, PanelWidth - 20f, 44f);

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
