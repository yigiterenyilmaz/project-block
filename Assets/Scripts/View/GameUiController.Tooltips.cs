// PURPOSE: GameUiController hover tooltips - detecting what the mouse is over and
// building/rendering the world-space tooltip panels for cards, jokers and powers.

using System.Collections;
using System.Collections.Generic;
using System.Text;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>Picks what the mouse is hovering (market offer, deck-overlay card, or a
        /// hand card in play) and shows its info tooltip, or hides it.</summary>
        private void UpdateHover(Mouse mouse)
        {
            if (mouse == null || tooltipRoot == null || cam == null || session == null)
            {
                return;
            }
            if (draggedCard != null || deckSelect.IsOpen)
            {
                cardLayer.SetHoveredCard(-1);
                HideTooltip();
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());

            if (grantPicker.IsOpen)
            {
                cardLayer.SetHoveredCard(-1);
                string defId, name, description;
                Rarity rarity;
                if (grantPicker.TryGetEntry(grantPicker.EntryAt(world),
                    out defId, out name, out description, out rarity))
                {
                    RenderTooltip("pick:" + defId, name,
                        TierLine(rarity) + ViewUtil.WrapText(description, 34), world, rarity);
                }
                else
                {
                    HideTooltip();
                }
                return;
            }
            if (deckOverlay.IsOpen)
            {
                cardLayer.SetHoveredCard(-1);
                BlockCard card = deckOverlay.CardAt(world);
                if (card != null) ShowCardTooltip(card, world); else HideTooltip();
                return;
            }
            // Hovering a held joker/power panel shows its live details (name + description +
            // status). Checked before the market/round branches so it works in either phase the
            // bars are visible; Halüsinasyon's dynamic Description is surfaced automatically.
            if (session.Phase == GamePhase.Round || session.Phase == GamePhase.Market)
            {
                Vector2 barScreen = mouse.position.ReadValue();
                int ji = jokerBar.JokerIndexAt(barScreen);
                if (ji >= 0 && ji < session.Jokers.Count)
                {
                    cardLayer.SetHoveredCard(-1);
                    ShowHeldJokerTooltip(session.Jokers.Jokers[ji], world);
                    return;
                }
                int pi = powerBar.PowerIndexAt(barScreen);
                if (pi >= 0 && pi < session.Powers.Count)
                {
                    cardLayer.SetHoveredCard(-1);
                    ShowHeldPowerTooltip(session.Powers.Powers[pi], world);
                    return;
                }
            }
            if (session.Phase == GamePhase.Market)
            {
                cardLayer.SetHoveredCard(-1);
                int index = marketView.OfferAt(world);
                if (index < 0 || index >= session.Market.Offers.Count)
                {
                    HideTooltip();
                    return;
                }
                MarketOffer offer = session.Market.Offers[index];
                if (offer.Sold)
                {
                    HideTooltip();
                }
                else if (offer.Kind == MarketOfferKind.Joker)
                {
                    ShowJokerTooltip(offer.Joker, offer.Price, world);
                }
                else if (offer.Kind == MarketOfferKind.Power)
                {
                    ShowPowerTooltip(offer.Power, offer.Price, world);
                }
                else
                {
                    ShowCardTooltip(offer.Card, world);
                }
                return;
            }
            if (session.Phase == GamePhase.Round
                && session.CurrentRound != null
                && session.CurrentRound.Status == RoundStatus.InProgress)
            {
                CardVisual hit = cardLayer.CardAt(world);
                BlockCard card = hit != null ? CardOfSlot(session.CurrentRound, hit.SlotIndex) : null;
                cardLayer.SetHoveredCard(card != null ? card.Id : -1);
                if (card != null) ShowCardTooltip(card, world); else HideTooltip();
                return;
            }
            cardLayer.SetHoveredCard(-1);
            HideTooltip();
        }

        private void ShowCardTooltip(BlockCard card, Vector2 nearWorld)
        {
            // Plain blocks carry no special info - no tooltip for them.
            if (card.Elements.Count == 0)
            {
                HideTooltip();
                return;
            }
            string title = Loc.Pick(
                "BLOCK - " + card.Shape.Size + (card.Shape.Size == 1 ? " cube" : " cubes"),
                "BLOK - " + card.Shape.Size + " küp")
                + "  (" + card.Shape.Width + "x" + card.Shape.Height + ")";
            var body = new StringBuilder();
            for (int i = 0; i < card.Elements.Count; i++)
            {
                if (i > 0) body.Append("\n\n");
                BlockElement element = card.Elements[i];
                body.Append(ViewUtil.ElementLabel(element)).Append('\n')
                    .Append(ViewUtil.WrapText(ViewUtil.ElementDescription(element), 34));
            }
            RenderTooltip("card:" + card.Id, title, body.ToString(), nearWorld);
        }

        /// <summary>The tier headline a joker/power tooltip opens with - empty for common, so
        /// only the tiers worth calling out take up a line.</summary>
        private static string TierLine(Rarity rarity)
        {
            string tier = RarityPalette.Label(rarity);
            return tier == null ? string.Empty : tier + "\n";
        }

        private void ShowJokerTooltip(JokerDefinition joker, int price, Vector2 nearWorld)
        {
            string body = TierLine(joker.Rarity) + ViewUtil.WrapText(joker.Description, 34)
                + Loc.Pick("\n\nCost ", "\n\nFiyat ") + price;
            RenderTooltip("joker:" + joker.DefId, joker.DisplayName, body, nearWorld, joker.Rarity);
        }

        private void ShowPowerTooltip(PowerDefinition power, int price, Vector2 nearWorld)
        {
            string body = TierLine(power.Rarity) + ViewUtil.WrapText(power.Description, 34)
                + Loc.Pick("\n\nCost ", "\n\nFiyat ") + price;
            RenderTooltip("power:" + power.DefId, power.DisplayName, body, nearWorld, power.Rarity);
        }

        /// <summary>Tooltip for a held joker in the bar: name, live description, and status.</summary>
        private void ShowHeldJokerTooltip(Joker joker, Vector2 nearWorld)
        {
            string title = joker.DisplayName;
            Rarity rarity = RarityPalette.Of(joker);
            string body = TierLine(rarity) + ViewUtil.WrapText(joker.Description, 34);
            if (!string.IsNullOrEmpty(joker.StatusText))
            {
                body += "\n\n" + joker.StatusText;
            }
            // The key carries a text hash so a live-changing description/status (Halüsinasyon's
            // current form, a charge flipping) rebuilds the panel instead of showing stale text.
            RenderTooltip("heldjoker:" + joker.InstanceId + "#" + (title + body).GetHashCode(),
                title, body, nearWorld, rarity);
        }

        /// <summary>Tooltip for a held power in the bar: name, live description, and status.</summary>
        private void ShowHeldPowerTooltip(Power power, Vector2 nearWorld)
        {
            string title = power.DisplayName;
            Rarity rarity = RarityPalette.Of(power);
            string body = TierLine(rarity) + ViewUtil.WrapText(power.Description, 34);
            if (!string.IsNullOrEmpty(power.StatusText))
            {
                body += "\n\n" + power.StatusText;
            }
            RenderTooltip("heldpower:" + power.InstanceId + "#" + (title + body).GetHashCode(),
                title, body, nearWorld, rarity);
        }

        /// <summary>Rebuilds the tooltip panel only when the hovered target changes; always
        /// repositions it next to the cursor, clamped inside the camera view.</summary>
        private void RenderTooltip(string key, string title, string body, Vector2 nearWorld,
            Rarity rarity = Rarity.Common)
        {
            tooltipRoot.SetActive(true);
            if (key != tooltipKey)
            {
                tooltipKey = key;
                for (int i = tooltipRoot.transform.childCount - 1; i >= 0; i--)
                {
                    Destroy(tooltipRoot.transform.GetChild(i).gameObject);
                }
                int bodyLines = 1;
                for (int i = 0; i < body.Length; i++)
                {
                    if (body[i] == '\n') bodyLines++;
                }
                const float margin = 0.14f;
                const float titleHeight = 0.34f;
                const float lineHeight = 0.30f;
                tooltipWidth = 3.4f;
                tooltipHeight = margin * 2f + titleHeight + bodyLines * lineHeight;

                ViewUtil.MakeRect(tooltipRoot.transform, "TipBg",
                    new Vector2(tooltipWidth * 0.5f, -tooltipHeight * 0.5f),
                    new Vector2(tooltipWidth, tooltipHeight), TooltipBgColor, 50);
                // High fontSize + small characterSize keeps TextMesh crisp; the dark panel
                // gives contrast so no outline is needed here.
                ViewUtil.MakeText3D(tooltipRoot.transform, "TipTitle",
                    new Vector2(margin, -margin), title, 90, 0.017f,
                    rarity == Rarity.Common ? TooltipTitleColor : RarityPalette.Accent(rarity), 51,
                    TextAnchor.UpperLeft);
                ViewUtil.MakeText3D(tooltipRoot.transform, "TipBody",
                    new Vector2(margin, -margin - titleHeight), body, 90, 0.014f, TooltipBodyColor,
                    51, TextAnchor.UpperLeft);
            }

            // Anchor the panel's top-left just up-right of the cursor, then clamp on screen.
            Vector3 topLeft = cam.ViewportToWorldPoint(new Vector3(0f, 1f, cam.nearClipPlane));
            Vector3 bottomRight = cam.ViewportToWorldPoint(new Vector3(1f, 0f, cam.nearClipPlane));
            float ax = Mathf.Clamp(nearWorld.x + 0.3f, topLeft.x + 0.1f, bottomRight.x - 0.1f - tooltipWidth);
            float ay = Mathf.Clamp(nearWorld.y + 0.4f, bottomRight.y + 0.1f + tooltipHeight, topLeft.y - 0.1f);
            tooltipRoot.transform.position = new Vector3(ax, ay, 0f);
        }

        private void HideTooltip()
        {
            if (tooltipRoot != null)
            {
                tooltipRoot.SetActive(false);
            }
            tooltipKey = null;
        }
    }
}
