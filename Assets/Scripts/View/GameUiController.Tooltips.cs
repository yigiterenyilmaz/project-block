// PURPOSE: GameUiController hover tooltips - detecting what the mouse is over and
// filling/positioning the tooltip panel for cards, jokers and powers.
//
// THE PANEL IS UI, ON ITS OWN OVERLAY CANVAS ABOVE THE HUD'S (see BuildTooltipCanvas). It used
// to be world-space sprites, and it could not win: an overlay canvas is composited after the
// camera, so it covers every world-space renderer whatever sorting order that renderer claims -
// and the joker bar, the power bar and the score readout, which are exactly what a tooltip has
// to be read over, are all on that canvas. The panel is built ONCE and only refilled here.

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
                cardLayer.SetDrawPileHovered(false);
                marketView.ClearHover();
                HideTooltip();
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());

            // The market scrolls, and a section is refreshed by HOLDING it rather than by
            // pressing a button on it. Both are driven from here because this is the one place
            // that already has the pointer in world space every frame.
            if (screen == AppScreen.Playing && session.Phase == GamePhase.Market)
            {
                Vector2 wheel = mouse.scroll.ReadValue();
                if (Mathf.Abs(wheel.y) > 0.01f)
                {
                    // Wheel UP shows what is ABOVE, which means the content moves DOWN and
                    // the scroll offset goes DOWN with it. It was the other way round.
                    marketView.Scroll(wheel.y > 0f ? -1f : 1f);
                }
                marketView.UpdateHold(world, mouse.leftButton.isPressed);
            }

            // The draw pile is clickable in both phases - the deck list in a round, the sell
            // screen in the market - so it lights up whenever the pointer is on it and nothing
            // is covering it.
            cardLayer.SetDrawPileHovered(!deckOverlay.IsOpen && !grantPicker.IsOpen
                && !(session.Phase == GamePhase.Market && UiLayout.Active.MarketStacked)
                && cardLayer.IsDrawPileAt(world));

            // The market's hover outline is decided ONCE, here, before any of the modal
            // branches below return: a picker or the deck overlay standing over the shelf must
            // not leave a highlight glowing underneath it.
            if (session.Phase != GamePhase.Market || grantPicker.IsOpen || deckOverlay.IsOpen)
            {
                marketView.ClearHover();
            }
            else
            {
                marketView.SetHover(world);
            }

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
                if (card == null)
                {
                    HideTooltip();
                }
                else if (foxPickSlot >= 0)
                {
                    // The fox picker's entries are stand-in shapes, not cards you own, so the
                    // collection tooltip would say untrue things about where they came from.
                    ShowFoxShapeTooltip(card, world);
                }
                else
                {
                    ShowDeckCardTooltip(card, world);
                }
                return;
            }
            // Hovering a held joker/power panel shows its live details (name + description +
            // status). Checked before the market/round branches so it works in either phase the
            // bars are visible; Halüsinasyon's dynamic Description is surfaced automatically.
            if (session.Phase == GamePhase.Round || session.Phase == GamePhase.Market)
            {
                Vector2 barScreen = mouse.position.ReadValue();
                // The badge sits above the joker bar in the same corner, and it is the only
                // place the boss's rules are written down now - so it answers first.
                BossRound badgeBoss = ActiveBoss();
                if (badgeBoss != null && BossBadgeAt(barScreen))
                {
                    cardLayer.SetHoveredCard(-1);
                    ShowBossTooltip(badgeBoss, world);
                    return;
                }
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
                    // Not on an offer: the section NAMES are hoverable too, and say what the
                    // whole mechanic is rather than what one thing on the shelf does.
                    MarketOfferKind section;
                    if (marketView.TrySectionLabelAt(world, out section))
                    {
                        ShowSectionTooltip(section, world);
                    }
                    else
                    {
                        HideTooltip();
                    }
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

        /// <summary>
        /// The card details POPUP of the collection overlay - and the sell screen's only price
        /// tag. It says three things a plain block tooltip does not: what the block is, WHERE it
        /// came from (a card off the market shelf, or one of your own), and - on the sell screen -
        /// what it would fetch.
        ///
        /// Unlike ShowCardTooltip this never declines to appear: a plain block carries no element
        /// text, but on the sell screen its price is exactly what the player opened the screen to
        /// read, so there is always something to say.
        /// </summary>
        private void ShowDeckCardTooltip(BlockCard card, Vector2 nearWorld)
        {
            // Every sentence goes through WrapText at the same column as the rest of the
            // tooltips: the panel is a fixed width and sizes itself by the lines it was handed,
            // so an unwrapped sentence would print off the side of it.
            var body = new StringBuilder();
            body.Append(ViewUtil.WrapText(card.IsPurchased
                ? Loc.Pick("Bought card - it came off the market shelf.",
                    "Satın alınmış kart - marketten geldi.")
                : Loc.Pick("Deck card - it was never bought.",
                    "Deste kartı - satın alınmadı."), 34));
            if (sellCardsMode)
            {
                // x ScoreScale: GameSession.SellCard pays in the scaled economy, and this screen
                // used to quote a tenth of what the card actually fetched.
                int value = session.Config.Market.SellValue(card)
                    * session.Config.Scoring.ScoreScale;
                body.Append('\n').Append(ViewUtil.WrapText(value > 0
                    ? Loc.Pick("Sells for " + value + ".", "Satış değeri " + value + ".")
                    : Loc.Pick("Worthless - selling it pays nothing.",
                        "Değersiz - satmak bir şey kazandırmaz."), 34));
                if (!card.IsPurchased)
                {
                    body.Append('\n').Append(ViewUtil.WrapText(Loc.Pick(
                        "Selling it frees no buying slot.",
                        "Satmak alım hakkı geri kazandırmaz."), 34));
                }
            }
            for (int i = 0; i < card.Elements.Count; i++)
            {
                BlockElement element = card.Elements[i];
                body.Append("\n\n").Append(ViewUtil.ElementLabel(element)).Append('\n')
                    .Append(ViewUtil.WrapText(ViewUtil.ElementDescription(element), 34));
            }
            RenderTooltip((sellCardsMode ? "sell:" : "deck:") + card.Id, CardTitle(card),
                body.ToString(), nearWorld);
        }

        /// <summary>One entry of the fox picker: the shape, and what taking it does. Says
        /// nothing about ownership - a shape is not a card and the deck may hold several of
        /// this one, or none by the time the round ends.</summary>
        private void ShowFoxShapeTooltip(BlockCard shapeCard, Vector2 nearWorld)
        {
            RenderTooltip("fox:" + shapeCard.Shape.CanonicalKey,
                Loc.Pick(
                    "SHAPE - " + shapeCard.Shape.Size
                        + (shapeCard.Shape.Size == 1 ? " cube" : " cubes"),
                    "ŞEKİL - " + shapeCard.Shape.Size + " küp")
                    + "  (" + shapeCard.Shape.Width + "x" + shapeCard.Shape.Height + ")",
                ViewUtil.WrapText(Loc.Pick(
                    "The fox takes this shape for the rest of the round. Every shape your deck "
                        + "can deal is offered here once.",
                    "Tilki raunt boyunca bu şekli alır. Destendeki her şekil burada bir kez "
                        + "listelenir."), 34),
                nearWorld);
        }

        /// <summary>The headline both card tooltips open with: what the block IS.</summary>
        private static string CardTitle(BlockCard card)
        {
            return Loc.Pick(
                "BLOCK - " + card.Shape.Size + (card.Shape.Size == 1 ? " cube" : " cubes"),
                "BLOK - " + card.Shape.Size + " küp")
                + "  (" + card.Shape.Width + "x" + card.Shape.Height + ")";
        }

        private void ShowCardTooltip(BlockCard card, Vector2 nearWorld)
        {
            // Plain blocks carry no special info - no tooltip for them.
            if (card.Elements.Count == 0)
            {
                HideTooltip();
                return;
            }
            string title = CardTitle(card);
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

        /// <summary>What a market SHELF is: the mechanic, in a few lines, for a player who has
        /// just met it. Deliberately about the RULE and not about the offers standing on it -
        /// what a particular joker does is that joker's own tooltip, one hover to the side.</summary>
        private void ShowSectionTooltip(MarketOfferKind kind, Vector2 nearWorld)
        {
            string title;
            string body;
            switch (kind)
            {
                case MarketOfferKind.Joker:
                    title = Loc.Pick("JOKERS", "JOKERLER");
                    body = Loc.Pick(
                        "Always on. They bend the rules and score for you by themselves, "
                            + "triggering left to right in the order you bought them. "
                            + "Sell one any time in the market.",
                        "Sürekli aktif. Kuralları büker ve kendi başlarına puan katarlar; "
                            + "aldığın sıraya göre soldan sağa çalışırlar. "
                            + "Markette istediğin zaman satabilirsin.");
                    break;
                case MarketOfferKind.Power:
                    title = Loc.Pick("POWERS", "GÜÇLER");
                    body = Loc.Pick(
                        "Used by hand. One charge, refilled by a clean sweep or by a new round. "
                            + "One power per turn, and using it never costs you the turn.",
                        "Elle kullanılır. Tek şarj: temizlik yapınca veya yeni rauntta dolar. "
                            + "Tur başına bir güç, kullanmak turunu harcamaz.");
                    break;
                default:
                    title = Loc.Pick("BLOCKS", "BLOKLAR");
                    // The purchase limit is said HERE, on the shelf it governs, and with the
                    // run's own numbers in it - a rule the player meets as a red price tag
                    // needs somewhere to be explained.
                    body = Loc.Pick(
                        "The shapes you place on the grid. A block you buy joins your run deck "
                            + "for good, so every round from here on can deal it to you. "
                            + "This run may buy " + session.CardPurchaseLimit
                            + " blocks in all - half the deck you started with - and "
                            + session.PurchasedCardCount + " are bought. Selling a block you "
                            + "bought gives its slot back; selling one of your own does not.",
                        "Oyun alanına yerleştirdiğin şekiller. Aldığın blok kalıcı olarak oyun "
                            + "destene girer; bundan sonraki her raunt onu dağıtabilir. "
                            + "Bu oyunda toplam " + session.CardPurchaseLimit
                            + " blok alabilirsin - başlangıç destenin yarısı - ve "
                            + session.PurchasedCardCount + " tanesi alındı. Aldığın bir bloğu "
                            + "satarsan hakkı geri gelir; kendi deste kartını satarsan gelmez.");
                    break;
            }
            RenderTooltip("section:" + kind, title, ViewUtil.WrapText(body, 34), nearWorld);
        }

        /// <summary>The boss badge's hover: who it is, what it does to this round, and what it
        /// is up to right now. StatusText is read LIVE off the boss, so a counter it keeps
        /// ("3 turns left") is current every time the tooltip is opened.</summary>
        private void ShowBossTooltip(BossRound boss, Vector2 nearWorld)
        {
            string body = ViewUtil.WrapText(boss.Description, 34);
            if (!string.IsNullOrEmpty(boss.StatusText))
            {
                body += "\n\n" + ViewUtil.WrapText(boss.StatusText, 34);
            }
            RenderTooltip("boss:" + boss.DefId,
                Loc.Pick("BOSS - ", "PATRON - ") + boss.DisplayName, body, nearWorld);
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
            // What the old horizontal panel printed and a vertical card has no room for.
            if (joker.Defect == SmuggledDefect.NeverWorks)
            {
                body += "\n" + Loc.Pick("DEFECTIVE: dead", "DEFOLU: hiç çalışmaz");
            }
            else if (joker.Defect == SmuggledDefect.DeadInBossRounds)
            {
                body += "\n" + Loc.Pick("DEFECTIVE: off in boss rounds",
                    "DEFOLU: patron rauntlarında kapalı");
            }
            if (joker.DisabledInOvertime)
            {
                body += "\n" + Loc.Pick("Off in overtime", "Uzatmada kapalı");
            }
            body += "\n" + Loc.Pick("Sell ", "Satış ")
                + session.Jokers.SellValueOf(joker) * session.Config.Scoring.ScoreScale;
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
            // The sell value the old horizontal panel printed.
            body += "\n" + Loc.Pick("Sell ", "Satış ")
                + session.Powers.SellValueOf(power) * session.Config.Scoring.ScoreScale;
            RenderTooltip("heldpower:" + power.InstanceId + "#" + (title + body).GetHashCode(),
                title, body, nearWorld, rarity);
        }

        // The panel's metrics, in the canvas's 1920x1080 reference space. The width is set by the
        // BODY: every description is pre-wrapped to 34 characters by ViewUtil.WrapText, and at
        // the body font size that column comes out just under 310px.
        private const float TooltipWidth = 334f;

        private const float TooltipMargin = 13f;

        private const float TooltipTitleHeight = 30f;

        private const float TooltipLineHeight = 22f;

        private const int TooltipTitleFontSize = 22;

        private const int TooltipBodyFontSize = 18;

        /// <summary>How far the edge plate stands out past the fill, and how far the panel's
        /// corner sits from the cursor. The gap is bigger than the arrow so the panel never
        /// lands under the pointer that summoned it.</summary>
        private const float TooltipEdge = 2f;

        private const float TooltipCursorGapX = 22f;

        private const float TooltipCursorGapY = 26f;

        /// <summary>Refills the tooltip panel only when the hovered target changes; always
        /// repositions it next to the cursor, clamped inside the screen.
        ///
        /// NOTHING IS DESTROYED AND REBUILT any more - the panel, its two plates and its two
        /// labels are made once in BuildTooltipCanvas and only ever refilled and resized. The old
        /// version tore its children down and made new ones on every change of target, which on a
        /// bar of jokers meant a fresh set of GameObjects for every one the cursor crossed.</summary>
        private void RenderTooltip(string key, string title, string body, Vector2 nearWorld,
            Rarity rarity = Rarity.Common)
        {
            tooltipRoot.gameObject.SetActive(true);
            if (key != tooltipKey)
            {
                tooltipKey = key;
                int bodyLines = 1;
                for (int i = 0; i < body.Length; i++)
                {
                    if (body[i] == '\n') bodyLines++;
                }
                tooltipWidth = TooltipWidth;
                tooltipHeight = TooltipMargin * 2f + TooltipTitleHeight
                    + bodyLines * TooltipLineHeight;
                tooltipRoot.sizeDelta = new Vector2(tooltipWidth, tooltipHeight);

                float textWidth = tooltipWidth - TooltipMargin * 2f;
                tooltipTitle.text = title;
                tooltipTitle.color = rarity == Rarity.Common
                    ? TooltipTitleColor
                    : RarityPalette.Accent(rarity);
                tooltipTitle.rectTransform.anchoredPosition =
                    new Vector2(TooltipMargin, -TooltipMargin);
                tooltipTitle.rectTransform.sizeDelta = new Vector2(textWidth, TooltipTitleHeight);

                tooltipBody.text = body;
                tooltipBody.rectTransform.anchoredPosition =
                    new Vector2(TooltipMargin, -TooltipMargin - TooltipTitleHeight);
                tooltipBody.rectTransform.sizeDelta =
                    new Vector2(textWidth, bodyLines * TooltipLineHeight);
            }

            // Anchor the panel's top-left just up-right of the cursor, then clamp on screen. The
            // cursor arrives as a WORLD point (every caller has one already, from the same
            // ScreenToWorldPoint the hover test uses), so it goes back through the camera here -
            // and then out of screen pixels into the canvas's reference units, which is what an
            // anchoredPosition is measured in once the scaler has had its say.
            Vector3 screenPoint = cam.WorldToScreenPoint(nearWorld);
            float scale = tooltipCanvas != null
                ? Mathf.Max(tooltipCanvas.scaleFactor, 0.0001f)
                : 1f;
            float viewWidth = Screen.width / scale;
            float viewHeight = Screen.height / scale;
            float ax = Mathf.Clamp(screenPoint.x / scale + TooltipCursorGapX,
                TooltipEdge, Mathf.Max(TooltipEdge, viewWidth - TooltipEdge - tooltipWidth));
            float ay = Mathf.Clamp(screenPoint.y / scale + TooltipCursorGapY,
                Mathf.Min(viewHeight - TooltipEdge, TooltipEdge + tooltipHeight),
                viewHeight - TooltipEdge);
            tooltipRoot.anchoredPosition = new Vector2(ax, ay);
        }

        private void HideTooltip()
        {
            if (tooltipRoot != null)
            {
                tooltipRoot.gameObject.SetActive(false);
            }
            tooltipKey = null;
        }
    }
}
