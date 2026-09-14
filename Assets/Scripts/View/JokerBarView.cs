// PURPOSE: The joker bar - one VERTICAL card per owned joker (HeldItemCard) with its hotkey,
// name and one live status line; the full description and the sell value are in its tooltip.
// Reads JokerInventory, never changes it (GameUiController owns input).
// NOTE FOR AGENTS: placeholder presentation like everything else under View/. The card
// stays generic on purpose - it renders Joker.StatusText/ChargesLeft rather than knowing any
// specific joker, so new jokers show up here for free. The only per-joker styling is the
// rarity frame/name colour, and that comes from RarityPalette.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    /// <summary>Canvas strip listing the player's jokers.</summary>
    public sealed class JokerBarView : MonoBehaviour
    {
        // The strip's SHAPE belongs to the layout: a column of wide panels down the right edge
        // on a desktop, a row of square slots under the score on a phone. See UiLayout.
        private static float PanelWidth
        {
            get { return UiLayout.Active.JokerPanel.x; }
        }

        private static float PanelHeight
        {
            get { return UiLayout.Active.JokerPanel.y; }
        }

        private static float PanelGap
        {
            get { return UiLayout.Active.JokerGap; }
        }

        /// <summary>True while the strip is a row of squares, which is too small for a body line
        /// and gets the name alone.</summary>
        private static bool Compact
        {
            get { return UiLayout.Active.BarsAsRow; }
        }

        private static readonly Color PanelColor = new Color(0.13f, 0.15f, 0.19f, 0.92f);
        private static readonly Color ReadyColor = new Color(0.20f, 0.34f, 0.24f, 0.95f);
        private static readonly Color TargetingColor = new Color(0.42f, 0.32f, 0.12f, 0.97f);

        /// <summary>Switched off by a boss round ("Anarşi", "Oburluk") - still owned, still
        /// sellable, but doing nothing this round.</summary>
        private static readonly Color SilencedColor = new Color(0.26f, 0.10f, 0.12f, 0.95f);

        /// <summary>"Terslik": the joker still runs, it just pays the wrong way. Purple rather
        /// than the silenced red, because switched-off and turned-around are different fates.</summary>
        private static readonly Color InvertedColor = new Color(0.28f, 0.13f, 0.34f, 0.96f);
        private static readonly Color NameColor = new Color(1f, 0.93f, 0.72f);
        private static readonly Color BodyColor = new Color(0.80f, 0.84f, 0.90f);

        private readonly List<HeldItemCard> panels = new List<HeldItemCard>();
        private RectTransform root;

        /// <summary>Creates the strip under the HUD canvas. Call once.</summary>
        public void Build(Transform canvas)
        {
            var go = new GameObject("JokerBar");
            go.transform.SetParent(canvas, false);
            root = go.AddComponent<RectTransform>();
            root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            UiLayout.PlaceBarRoot(root, true, topOffset);
        }

        /// <summary>How far the strip is pushed DOWN from its corner, in canvas pixels. The boss
        /// badge is anchored to the same corner and would otherwise land on the first joker, so
        /// it asks for its own height back rather than the bar guessing what is above it.</summary>
        public void SetTopOffset(float pixels)
        {
            topOffset = pixels;
            UiLayout.PlaceBarRoot(root, true, topOffset);
        }

        private float topOffset;

        /// <summary>Re-anchors the strip and every slot in it after the layout changed.</summary>
        public void RelayoutForScreen()
        {
            if (root == null)
            {
                return;
            }
            root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            UiLayout.PlaceBarRoot(root, true, UiLayout.Active.BarsAsRow
                ? UiLayout.Active.BarRowTop : topOffset);
            PlaceSlots();
        }

        /// <summary>Lays every visible slot out for the current profile. A row has to be told how
        /// many there are so it can stay centred, which is why this runs after Refresh has
        /// decided what is showing rather than when a panel is created.</summary>
        private void PlaceSlots()
        {
            int showing = 0;
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i].Root.activeSelf)
                {
                    showing++;
                }
            }
            for (int i = 0; i < panels.Count; i++)
            {
                UiLayout.PlaceBarSlot(panels[i].Root.GetComponent<RectTransform>(),
                    i, showing, new Vector2(PanelWidth, PanelHeight), PanelGap, true);
                panels[i].Layout(new Vector2(PanelWidth, PanelHeight), Compact);
            }
        }

        /// <summary>Shows or hides the whole strip. The menu layer hides it while no run is
        /// on screen; a hidden strip also stops answering JokerIndexAt, because its panels
        /// go inactive with it.</summary>
        public void SetVisible(bool visible)
        {
            if (root != null)
            {
                root.gameObject.SetActive(visible);
            }
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
            // A ROW has to be re-centred whenever the count changes; a column does not care.
            PlaceSlots();
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

        /// <summary>Quick scale pulse on the panel showing that joker (activation feedback).</summary>
        public void PulseJoker(int instanceId)
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
        /// post-sale inventory. Call AFTER JokerInventory.Sell.</summary>
        public void AnimateJokerSold(int index, GameSession session)
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

        private void Fill(HeldItemCard panel, Joker joker, int index, GameSession session,
            int? targetingInstanceId)
        {
            panel.InstanceId = joker.InstanceId;
            bool ready = session.Jokers.CanActivate(joker.InstanceId);
            bool targeting = targetingInstanceId.HasValue
                && targetingInstanceId.Value == joker.InstanceId;
            // A boss round can switch a joker off entirely ("Anarşi", "Oburluk"). That outranks
            // the activation state: the panel must not look ready when nothing will happen.
            bool silenced = session.CurrentRound != null
                && session.CurrentRound.IsSilencedByBoss(joker);
            // "Terslik" does not switch a joker off - it turns it around. Its own colour, so the
            // two are never confused: a silenced joker does nothing, an inverted one hurts.
            bool inverted = !silenced && session.CurrentRound != null
                && session.CurrentRound.InvertsJokerScore;
            panel.Body.color = silenced
                ? SilencedColor
                : inverted
                    ? InvertedColor
                    : (targeting ? TargetingColor : (ready ? ReadyColor : PanelColor));

            // The body still belongs to the activation state (ready/targeting), so rarity rides
            // on the frame and the name colour instead of fighting it for the card.
            Rarity rarity = RarityPalette.Of(joker);
            panel.Frame.color = RarityPalette.Accent(rarity);
            panel.Title.color = rarity == Rarity.Common ? NameColor : RarityPalette.Accent(rarity);
            panel.Hotkey.color = panel.Title.color;

            panel.Hotkey.text = index < 9 ? (index + 1).ToString() : string.Empty;
            panel.Title.text = joker.DisplayName;
            panel.SetIcon(ViewUtil.JokerIcon(joker.DefId), silenced);
            panel.Status.text = StatusLine(joker, silenced, inverted);
        }

        /// <summary>The ONE line a vertical card has room for, most important first: a boss
        /// switching it off or turning it round outranks a defect, which outranks the joker's
        /// own status and charges. The full picture (description, sell value, overtime) is the
        /// held-joker tooltip.</summary>
        private static string StatusLine(Joker joker, bool silenced, bool inverted)
        {
            if (silenced)
            {
                return Loc.Pick("BOSS: off", "PATRON: kapalı");
            }
            if (inverted)
            {
                return Loc.Pick("REVERSED", "TERS");
            }
            // "Kaçakçı": the joker itself does not know it came off a lorry, so the bar has to
            // say so - a joker that silently does nothing reads as a bug.
            if (joker.Defect == SmuggledDefect.NeverWorks)
            {
                return Loc.Pick("DEFECTIVE", "DEFOLU");
            }
            var line = new System.Text.StringBuilder();
            if (joker.Defect == SmuggledDefect.DeadInBossRounds)
            {
                line.Append(Loc.Pick("DEFECTIVE ", "DEFOLU "));
            }
            string status = joker.StatusText;
            if (!string.IsNullOrEmpty(status))
            {
                line.Append(status);
            }
            if (joker.ChargesPerRound > 0)
            {
                if (line.Length > 0)
                {
                    line.Append("  ");
                }
                line.Append(Loc.Pick("uses ", "hak "))
                    .Append(joker.ChargesLeft).Append('/').Append(joker.ChargesPerRound);
            }
            return line.ToString();
        }

        private HeldItemCard CreatePanel(int index)
        {
            HeldItemCard card = HeldItemCard.Create(root, "Joker_" + index, NameColor, BodyColor);
            card.Body.color = PanelColor;
            UiLayout.PlaceBarSlot(card.Root.GetComponent<RectTransform>(), index, index + 1,
                new Vector2(PanelWidth, PanelHeight), PanelGap, true);
            card.Layout(new Vector2(PanelWidth, PanelHeight), Compact);
            return card;
        }
    }
}
