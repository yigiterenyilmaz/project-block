// PURPOSE: The power bar on the left side of the screen - below the info text, above the
// discard pile. One clickable VERTICAL card per owned power (HeldItemCard) with its name and
// charge state; the description and sell value are in its tooltip. Reads PowerInventory,
// never changes it (GameUiController owns input).
// NOTE FOR AGENTS: placeholder presentation like everything else under View/. The card
// stays generic on purpose - it renders Power.StatusText/Charged rather than knowing any
// specific power, so new powers show up here for free. The only per-power styling is the
// rarity frame/name colour, and that comes from RarityPalette.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    /// <summary>Canvas strip listing the player's powers.</summary>
    public sealed class PowerBarView : MonoBehaviour
    {
        // Shape belongs to the layout - a column down the left edge on a desktop, a row of
        // squares under the joker row on a phone. See UiLayout.
        private static float PanelWidth
        {
            get { return UiLayout.Active.PowerPanel.x; }
        }

        private static float PanelHeight
        {
            get { return UiLayout.Active.PowerPanel.y; }
        }

        private static float PanelGap
        {
            get { return UiLayout.Active.PowerGap; }
        }

        private static bool Compact
        {
            get { return UiLayout.Active.BarsAsRow; }
        }

        /// <summary>Where the strip hangs from the top of the canvas. On the desktop this is the
        /// old hand-placed 256; in a row it sits under the joker row.</summary>
        private static float TopOffset
        {
            get
            {
                UiLayout layout = UiLayout.Active;
                // Desktop: the very top of the left edge. The info/debug column that used to sit
                // above it moved to its right (UiLayout.InfoLeft).
                return layout.BarsAsRow
                    ? layout.BarRowTop + layout.JokerPanel.y + 12f
                    : 0f;
            }
        }

        private static readonly Color PanelColor = new Color(0.13f, 0.15f, 0.19f, 0.92f);
        private static readonly Color ReadyColor = new Color(0.12f, 0.30f, 0.34f, 0.95f);
        private static readonly Color SpentColor = new Color(0.16f, 0.16f, 0.17f, 0.85f);
        private static readonly Color TargetingColor = new Color(0.42f, 0.32f, 0.12f, 0.97f);
        private static readonly Color NameColor = new Color(0.72f, 0.96f, 0.98f);
        private static readonly Color BodyColor = new Color(0.80f, 0.84f, 0.90f);
        private static readonly Color SpentTextColor = new Color(0.55f, 0.58f, 0.62f);

        /// <summary>Switched off by a boss round ("Anarşi", "Oburluk"): charged or not, it
        /// cannot be used this round.</summary>
        private static readonly Color SilencedColor = new Color(0.26f, 0.10f, 0.12f, 0.95f);

        private readonly List<HeldItemCard> panels = new List<HeldItemCard>();
        private RectTransform root;

        /// <summary>The layer every card's halo lives in, kept as the strip's FIRST child so a
        /// glow is always drawn UNDER every card - see HeldItemCard.AttachGlow.</summary>
        private RectTransform glowLayer;

        /// <summary>Creates the strip under the HUD canvas. Call once.</summary>
        public void Build(Transform canvas)
        {
            var go = new GameObject("PowerBar");
            go.transform.SetParent(canvas, false);
            root = go.AddComponent<RectTransform>();
            root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            UiLayout.PlaceBarRoot(root, false, TopOffset);

            var glowGo = new GameObject("GlowLayer");
            glowGo.transform.SetParent(root, false);
            glowLayer = glowGo.AddComponent<RectTransform>();
            glowLayer.anchorMin = Vector2.zero;
            glowLayer.anchorMax = Vector2.one;
            glowLayer.offsetMin = Vector2.zero;
            glowLayer.offsetMax = Vector2.zero;
            glowLayer.SetAsFirstSibling();
        }

        /// <summary>Re-anchors the strip and every slot in it after the layout changed.</summary>
        public void RelayoutForScreen()
        {
            if (root == null)
            {
                return;
            }
            root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            UiLayout.PlaceBarRoot(root, false, TopOffset);
            PlaceSlots();
        }

        /// <summary>Lays every visible slot out for the current profile. A row needs the count so
        /// it can stay centred, which is why this runs after Refresh rather than at creation.</summary>
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
                    i, showing, new Vector2(PanelWidth, PanelHeight), PanelGap, false);
                panels[i].Layout(new Vector2(PanelWidth, PanelHeight), Compact);
                panels[i].SyncGlow();
            }
        }

        /// <summary>Shows or hides the whole strip. The menu layer hides it while no run is
        /// on screen; a hidden strip also stops answering PowerIndexAt, because its panels
        /// go inactive with it.</summary>
        public void SetVisible(bool visible)
        {
            if (root != null)
            {
                root.gameObject.SetActive(visible);
            }
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
            PlaceSlots();
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

        /// <summary>The proc light for a power - the same seam and the same look the joker bar
        /// gives a joker, so the two bars cannot drift apart about what "it fired" looks like.</summary>
        public void ProcPower(int instanceId)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i].Root.activeSelf && panels[i].InstanceId == instanceId)
                {
                    if (panels[i].Glow != null)
                    {
                        panels[i].Glow.Proc(CardGlowFx.ProcColour);
                    }
                    StartCoroutine(PulseRoutine(panels[i].Root.transform));
                    return;
                }
            }
        }

        /// <summary>
        /// THE WIND-UP. A hold is a commitment, so the card has to LOAD before it goes: it draws
        /// back and tightens as the finger stays down, and the last fifth tenses hard - which is
        /// what tells the player the release is about to sell rather than use.
        ///
        /// It is a SQUEEZE, not a shrink: the card narrows while keeping most of its height and
        /// leans a couple of degrees, because a uniformly scaled card just looks further away.
        /// It also RECOILS at the very end (a hair past its own size) so the sale has something
        /// to release from. The base scale is written from a constant, never read back off the
        /// transform, or a per-frame scale compounds - the trap the ice seating already hit.
        /// </summary>
        public void SetHoldProgress(int index, float progress)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                bool mine = i == index;
                float k = mine ? Mathf.Clamp01(progress) : 0f;
                if (panels[i].Glow != null)
                {
                    panels[i].Glow.SetHold(k, CardGlowFx.HoldColour);
                }
                Transform tr = panels[i].Root.transform;
                if (k <= 0f)
                {
                    if (tr.localScale != Vector3.one || tr.localRotation != Quaternion.identity)
                    {
                        tr.localScale = Vector3.one;
                        tr.localRotation = Quaternion.identity;
                    }
                    continue;
                }
                // Eased so the load is gentle early and bites late.
                float load = k * k;
                // The last fifth SNAPS a little wider - the recoil before the release.
                float recoil = k > 0.8f ? (k - 0.8f) / 0.2f : 0f;
                float squeeze = 1f - 0.10f * load + 0.06f * recoil;
                float stretch = 1f - 0.03f * load + 0.02f * recoil;
                tr.localScale = new Vector3(squeeze, stretch, 1f);
                tr.localRotation = Quaternion.Euler(0f, 0f, -2.2f * load * TiltSign);
            }
        }

        /// <summary>Which way a loading card leans. The two bars lean AWAY from their own screen
        /// edge, so the wind-up reads as the card being pulled off the strip.</summary>
        private const float TiltSign = -1f;

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

        private void Fill(HeldItemCard panel, Power power, GameSession session,
            int? targetingInstanceId)
        {
            panel.InstanceId = power.InstanceId;
            bool ready = session.Powers.CanBeginUse(power.InstanceId);
            bool targeting = targetingInstanceId.HasValue
                && targetingInstanceId.Value == power.InstanceId;
            // Silenced by a boss outranks everything else: a charged power that cannot be used
            // must not look ready.
            bool silenced = session.CurrentRound != null
                && session.CurrentRound.IsSilencedByBoss(power);
            panel.SetBody(silenced ? SilencedColor
                : targeting ? TargetingColor
                : !power.Charged ? SpentColor
                : ready ? ReadyColor : PanelColor);
            // Spent still greys the whole card out; rarity only tints the charged state and the
            // frame, which stays lit so the tier is readable on a spent power too.
            Rarity rarity = RarityPalette.Of(power);
            Color accent = RarityPalette.Accent(rarity);
            panel.Frame.color = accent;
            panel.Title.color = panel.Ink(!power.Charged ? SpentTextColor
                : rarity == Rarity.Common ? NameColor : accent);
            panel.Status.color = panel.Ink(power.Charged ? BodyColor : SpentTextColor);

            panel.Hotkey.text = string.Empty;
            panel.Title.text = power.DisplayName;
            panel.SetIcon(ViewUtil.PowerIcon(power.DefId), silenced || !power.Charged);
            panel.Status.text = StatusLine(power, session, silenced);
        }

        /// <summary>The one line a vertical card has room for: a boss outranks the charge state,
        /// and the power's own status comes first when it has one. Sell value and the full
        /// description are the held-power tooltip.</summary>
        private static string StatusLine(Power power, GameSession session, bool silenced)
        {
            if (silenced)
            {
                return Loc.Pick("BOSS: off", "PATRON: kapalı");
            }
            if (!power.Charged && session.CurrentRound != null
                && session.CurrentRound.PowerRechargeBlocked)
            {
                return Loc.Pick("BOSS: no refill", "PATRON: dolmaz");
            }
            var line = new System.Text.StringBuilder();
            string status = power.StatusText;
            if (!string.IsNullOrEmpty(status))
            {
                line.Append(status).Append("  ");
            }
            if (power.Charged)
            {
                line.Append(Loc.Pick("charged", "dolu"));
            }
            else if (power.RechargeCost > 1)
            {
                // "Kaçakçı" defective goods: say how far off a charge it is, or the meter looks
                // stuck for four sweeps running.
                line.Append(Loc.Pick("DEFECTIVE ", "DEFOLU "))
                    .Append(power.RechargeProgress).Append('/').Append(power.RechargeCost);
            }
            else
            {
                line.Append(Loc.Pick("empty", "boş"));
            }
            return line.ToString();
        }

        private HeldItemCard CreatePanel(int index)
        {
            // Art/Cards/card_power if somebody paints one; until then the flat card, which
            // is what this bar has always drawn.
            HeldItemCard card = HeldItemCard.Create(root, "Power_" + index, NameColor, BodyColor,
                "card_power");
            card.SetResting(PanelColor);
            card.AttachGlow(glowLayer);
            UiLayout.PlaceBarSlot(card.Root.GetComponent<RectTransform>(), index, index + 1,
                new Vector2(PanelWidth, PanelHeight), PanelGap, false);
            card.Layout(new Vector2(PanelWidth, PanelHeight), Compact);
            return card;
        }
    }
}