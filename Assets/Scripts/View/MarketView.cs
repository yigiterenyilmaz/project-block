// PURPOSE: The between-rounds market screen: block-card, joker and power offers with
// prices, click to buy. Rebuilt from scratch on every change (cheap at this scale).
// Purchases go through GameSession.TryBuyOffer - this view never touches money, the deck
// or the inventories. Joker/power offers are framed, tinted and tagged by their graded
// Rarity through RarityPalette (the same colours the bars and the debug pickers use).
// Sorting orders: backdrop 33, frames 34, offer cards/joker/power tiles 36/37, price
// labels 38 (under the deck overlay at 40+).

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Renders and hit-tests the market offers.</summary>
    public sealed class MarketView : MonoBehaviour
    {
        private const float OfferSpacing = 3.15f;

        /// <summary>Vertical distance between two section rows.</summary>
        private const float RowPitch = 3.7f;

        /// <summary>Section header above a row's tiles / price label below them.</summary>
        private const float HeaderOffset = 1.62f;
        private const float PriceOffset = 1.62f;

        /// <summary>Reroll button size and how far it sits clear of the row's widest tile.</summary>
        private static readonly Vector2 RerollHalf = new Vector2(1.35f, 0.42f);
        private const float RerollGap = 0.55f;

        /// <summary>Height shared by every offer tile, so the rows line up whatever kind they
        /// hold. Block cards are drawn at BlockTileScale to reach it.</summary>
        private const float TileHeight = 2.45f;

        /// <summary>Block offers are CardVisuals at a fixed 1.35 x 1.8; scaling the visual is
        /// what lets them match the bigger named tiles instead of looking like postage stamps
        /// beside them.</summary>
        private const float BlockTileScale = 1.36f;

        private static readonly Vector2 Center = new Vector2(0f, -0.2f);

        /// <summary>Fully opaque: the market is a screen of its own, and the board showing
        /// through it made the offers hard to read.</summary>
        private static readonly Color BackdropColor = new Color(0.05f, 0.06f, 0.08f, 1f);

        /// <summary>The border drawn around the whole panel. Distinct from FrameColor, which
        /// belongs to the individual offer tiles.</summary>
        private static readonly Color PanelFrameColor = new Color(0.30f, 0.34f, 0.44f);

        /// <summary>How far the frame sticks out past the backdrop on every side.</summary>
        private const float PanelBorder = 0.11f;

        private static readonly Color FrameColor = new Color(0.16f, 0.17f, 0.21f);
        private static readonly Color AffordablePriceColor = new Color(1f, 0.92f, 0.45f);
        private static readonly Color TooExpensiveColor = new Color(1f, 0.45f, 0.4f);
        private static readonly Color SoldColor = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color SectionHeaderColor = new Color(0.70f, 0.75f, 0.82f);
        private static readonly Color JokerBodyColor = new Color(0.30f, 0.22f, 0.40f);
        private static readonly Color JokerTagColor = new Color(0.82f, 0.68f, 1f);
        private static readonly Color JokerNameColor = new Color(1f, 0.93f, 0.72f);
        private static readonly Color JokerDescColor = new Color(0.82f, 0.86f, 0.92f);
        private static readonly Color PowerBodyColor = new Color(0.12f, 0.30f, 0.34f);
        private static readonly Color PowerTagColor = new Color(0.55f, 0.92f, 0.95f);
        private static readonly Color RerollButtonColor = new Color(0.20f, 0.24f, 0.34f);
        private static readonly Color RerollButtonDisabledColor = new Color(0.14f, 0.14f, 0.16f);


        /// <summary>Joker and power tiles are WIDER than a block card, because they carry text
        /// rather than a shape preview. Widening rather than shrinking the font is what lets the
        /// description be readable without losing most of itself to the ellipsis. Height stays
        /// CardVisual.BodyHeight so the row headers and price labels keep their spacing.</summary>
        private const float NamedTileWidth = 2.35f;

        /// <summary>Description lines a tile can show before running over its own bottom edge
        /// and into the price beneath it. The description starts 0.22 above centre of a tile
        /// TileHeight tall, and a line at this size is roughly 0.153.</summary>
        private const int MaxDescriptionLines = 8;

        /// <summary>Characters per description line at NamedTileWidth.</summary>
        private const int DescriptionWrap = 18;

        private readonly List<CardVisual> offerVisuals = new List<CardVisual>();
        private readonly List<Vector2> offerCenters = new List<Vector2>();

        /// <summary>Index-aligned with the offers, so the buy fx can fly away in the same
        /// rarity colour the tile had (Common for block offers).</summary>
        private readonly List<Rarity> offerRarities = new List<Rarity>();

        /// <summary>Half-width of each offer slot, index-aligned with the offers. Joker and
        /// power tiles are wider than block cards, so one shared width would both mis-frame
        /// them and mis-answer OfferAt.</summary>
        private readonly List<float> offerHalfWidths = new List<float>();

        /// <summary>One reroll button per section - refreshing the blocks must not disturb the
        /// jokers standing next to them.</summary>
        private struct SectionButton
        {
            public MarketOfferKind Kind;
            public Vector2 Center;
            public Vector2 Half;
        }

        private readonly List<SectionButton> rerollButtons = new List<SectionButton>();

        /// <summary>Index-aligned with the offers: whether a tile is already sold, and whether
        /// the player can afford it. The hover outline reads both, so it never lights up an
        /// offer that a click would do nothing with, and it warns before the click when the
        /// price is out of reach.</summary>
        private readonly List<bool> offerSold = new List<bool>();
        private readonly List<bool> offerAffordable = new List<bool>();

        /// <summary>Whether the shared reroll price is affordable, cached at build time.</summary>
        private bool rerollAffordable;

        /// <summary>The hover outline: four thin edges reused every frame. An OUTLINE rather
        /// than a tint because the tiles are built from many pieces at several sorting orders -
        /// a frame around them never has to know what it is framing, and never covers it.</summary>
        private readonly SpriteRenderer[] hoverEdges = new SpriteRenderer[4];

        private static readonly Color HoverColor = new Color(1f, 0.92f, 0.45f);
        private static readonly Color HoverBlockedColor = new Color(1f, 0.45f, 0.4f);

        /// <summary>Thickness of the hover outline and how far it stands off the tile.</summary>
        private const float HoverEdge = 0.075f;
        private const float HoverInset = 0.13f;

        /// <summary>(Re)builds the market display as stacked section ROWS - BLOCKS, JOKERS,
        /// POWERS - each row horizontally centered with its header above it and the prices
        /// below. Rows keep the screen narrow no matter how many offers are stocked.</summary>
        public void Show(GameSession session)
        {
            Hide();
            IReadOnlyList<MarketOffer> offers = session.Market.Offers;
            int count = offers.Count;

            // One row per offer kind that has offers, in kind order. Rows collect offer
            // INDICES so offerCenters stays index-aligned with the offers list (OfferAt
            // and the buy fx rely on that).
            var rowKinds = new List<MarketOfferKind>();
            var rowOffers = new List<List<int>>();
            foreach (MarketOfferKind kind in new[]
                { MarketOfferKind.Block, MarketOfferKind.Joker, MarketOfferKind.Power })
            {
                var row = new List<int>();
                for (int i = 0; i < count; i++)
                {
                    if (offers[i].Kind == kind)
                    {
                        row.Add(i);
                    }
                }
                if (row.Count > 0)
                {
                    rowKinds.Add(kind);
                    rowOffers.Add(row);
                }
            }

            for (int i = 0; i < count; i++)
            {
                offerCenters.Add(Vector2.zero);
                offerHalfWidths.Add(CardVisual.BodyWidth * 0.5f);
                offerSold.Add(offers[i].Sold);
                offerAffordable.Add(session.TotalScore >= offers[i].Price);
            }

            float maxSpan = 0f;
            for (int r = 0; r < rowOffers.Count; r++)
            {
                maxSpan = Mathf.Max(maxSpan, (rowOffers[r].Count - 1) * OfferSpacing);
            }
            float topRowY = Center.y + (rowOffers.Count - 1) * RowPitch * 0.5f;
            float bottomRowY = topRowY - (rowOffers.Count - 1) * RowPitch;

            // ---- LAYOUT FIRST, PANEL SECOND. Every position below is derived, then the panel
            // is sized to CONTAIN them. Padding constants used to guess at the panel's size,
            // and the guess was wrong: the first section header landed on the sell hint.
            float firstHeaderY = topRowY + HeaderOffset;
            // Gaps scale with the type: the hint lines were bumped up a size, so the rhythm
            // above them has to open up or the balance line sits on top of the hint.
            float sellHintY = firstHeaderY + 0.68f;
            float balanceY = sellHintY + 0.56f;
            float titleY = balanceY + 0.74f;
            float promptY = bottomRowY - PriceOffset - 0.85f;

            // Widest tile kind decides the row's reach - joker/power tiles are wider than a
            // block card, so a block-card width would let them poke out of the panel.
            float widestTile = Mathf.Max(CardVisual.BodyWidth * BlockTileScale, NamedTileWidth);
            float rowReach = maxSpan * 0.5f + widestTile * 0.5f;
            // Reroll buttons sit to the RIGHT of their section, on the row's own line.
            float rerollX = Center.x + rowReach + RerollGap + RerollHalf.x;
            float halfWidth = Mathf.Max(rowReach, rerollX - Center.x + RerollHalf.x) + 0.7f;

            float contentTop = titleY + 0.8f;
            float contentBottom = promptY - 0.7f;
            var panelCenter = new Vector2(Center.x, (contentTop + contentBottom) * 0.5f);
            var panelSize = new Vector2(halfWidth * 2f, contentTop - contentBottom);

            // Frame first and one sorting step further back, so all that shows of it is the
            // margin around the opaque backdrop - which is exactly the border.
            ViewUtil.MakeRect(transform, "PanelFrame", panelCenter,
                panelSize + new Vector2(PanelBorder * 2f, PanelBorder * 2f), PanelFrameColor, 32);
            ViewUtil.MakeRect(transform, "Backdrop", panelCenter, panelSize, BackdropColor, 33);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(Center.x, titleY), "MARKET",
                60, 0.075f, Color.white, 38, TextAnchor.MiddleCenter);
            // What you have to spend. The shelf shows prices everywhere and used to leave the
            // player to work their balance out from the HUD dump.
            ViewUtil.MakeText3D(transform, "Balance", new Vector2(Center.x, balanceY),
                Loc.Pick("You have ", "Paran: ") + session.TotalScore,
                90, 0.032f, new Color(1f, 0.86f, 0.42f), 38, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "SellHint", new Vector2(Center.x, sellHintY),
                Loc.Pick(
                    "Click a joker or a power to sell it  -  click the deck pile to sell cards",
                    "Satmak için jokere veya güce tıkla  -  kart satmak için desteye tıkla"),
                90, 0.024f, SectionHeaderColor, 38, TextAnchor.MiddleCenter);

            for (int r = 0; r < rowOffers.Count; r++)
            {
                float rowY = topRowY - r * RowPitch;
                List<int> row = rowOffers[r];
                ViewUtil.MakeText3D(transform, SectionLabel(rowKinds[r]) + "Header",
                    new Vector2(Center.x, rowY + HeaderOffset), SectionLabel(rowKinds[r]),
                    90, 0.026f, SectionHeaderColor, 38, TextAnchor.MiddleCenter);
                float startX = Center.x - (row.Count - 1) * OfferSpacing * 0.5f;
                for (int c = 0; c < row.Count; c++)
                {
                    offerCenters[row[c]] = new Vector2(startX + c * OfferSpacing, rowY);
                }
            }

            for (int i = 0; i < count; i++)
            {
                Vector2 slotCenter = offerCenters[i];
                MarketOffer offer = offers[i];
                // The frame is the rarity's loudest signal: a rare/legendary tile is ringed in
                // its tier colour, a common one keeps the neutral frame.
                Rarity rarity = offer.Kind == MarketOfferKind.Joker ? offer.Joker.Rarity
                    : offer.Kind == MarketOfferKind.Power ? offer.Power.Rarity
                    : Rarity.Common;
                offerRarities.Add(rarity);
                // The frame must follow the TILE's width, not the block card's - a joker tile
                // is wider, and a frame sized for a block card leaves it ringed top and bottom
                // only, which reads as broken art.
                float tileWidth = offer.Kind == MarketOfferKind.Block
                    ? CardVisual.BodyWidth * BlockTileScale
                    : NamedTileWidth;
                offerHalfWidths[i] = tileWidth * 0.5f;
                ViewUtil.MakeRect(transform, "Frame_" + i, slotCenter,
                    new Vector2(tileWidth + 0.2f, TileHeight + 0.2f),
                    RarityPalette.Frame(FrameColor, rarity), 34);
                if (offer.Sold)
                {
                    offerVisuals.Add(null);
                    ViewUtil.MakeText3D(transform, "Sold_" + i, slotCenter,
                        Loc.Pick("SOLD", "SATILDI"),
                        60, 0.07f, SoldColor, 38, TextAnchor.MiddleCenter);
                    continue;
                }
                if (offer.Kind == MarketOfferKind.Joker)
                {
                    // Joker/power tiles have no CardVisual; a null keeps offerVisuals
                    // index-aligned with the offers so PlayBuyFx and OfferAt stay correct.
                    offerVisuals.Add(null);
                    BuildNamedTile(slotCenter, i, "Joker", TierTag(Loc.Pick("JOKER", "JOKER"), rarity),
                        offer.Joker.DisplayName, offer.Joker.Description,
                        RarityPalette.Tint(JokerBodyColor, rarity),
                        rarity == Rarity.Common ? JokerTagColor : RarityPalette.Accent(rarity));
                }
                else if (offer.Kind == MarketOfferKind.Power)
                {
                    offerVisuals.Add(null);
                    BuildNamedTile(slotCenter, i, "Power", TierTag(Loc.Pick("POWER", "GÜÇ"), rarity),
                        offer.Power.DisplayName, offer.Power.Description,
                        RarityPalette.Tint(PowerBodyColor, rarity),
                        rarity == Rarity.Common ? PowerTagColor : RarityPalette.Accent(rarity));
                }
                else
                {
                    CardVisual visual = CardVisual.Create(transform, "Offer_" + i, offer.Card,
                        true, false, slotCenter, 36);
                    // Blown up to the shared tile size - a card at its hand size looks tiny
                    // next to a joker tile.
                    visual.transform.localScale = new Vector3(BlockTileScale, BlockTileScale, 1f);
                    offerVisuals.Add(visual);
                }
                bool affordable = session.TotalScore >= offer.Price;
                ViewUtil.MakeText3D(transform, "Price_" + i,
                    slotCenter + new Vector2(0f, -PriceOffset), offer.Price.ToString(),
                    60, 0.07f, affordable ? AffordablePriceColor : TooExpensiveColor,
                    38, TextAnchor.MiddleCenter);
            }

            // One reroll button per section, sitting under that section's prices: refreshing
            // the blocks must leave the jokers beside them alone (GameSession.RerollMarket).
            // The price is shared across sections, so it escalates however you spend it.
            long rerollCost = session.NextRerollCost;
            bool canReroll = session.TotalScore >= rerollCost;
            rerollAffordable = canReroll;
            for (int r = 0; r < rowOffers.Count; r++)
            {
                float rowY = topRowY - r * RowPitch;
                var button = new SectionButton();
                button.Kind = rowKinds[r];
                // Beside its section rather than under it: the shelf was stacking four things
                // deep per row while the screen had width going spare.
                button.Center = new Vector2(rerollX, rowY);
                button.Half = RerollHalf;
                rerollButtons.Add(button);
                Color inkColor = canReroll ? AffordablePriceColor : TooExpensiveColor;
                ViewUtil.MakeRect(transform, "Reroll_" + r, button.Center, button.Half * 2f,
                    canReroll ? RerollButtonColor : RerollButtonDisabledColor, 34);
                // Finer thickness than the ring's radius would suggest: the segments overlap
                // into a smooth circle, so a fat stroke just makes a blob at this size.
                ViewUtil.MakeRefreshIcon(transform, "RerollIcon_" + r,
                    button.Center + new Vector2(-button.Half.x + 0.42f, -0.02f),
                    0.17f, 0.055f, inkColor, 38);
                ViewUtil.MakeText3D(transform, "RerollLabel_" + r,
                    button.Center + new Vector2(0.26f, 0f), rerollCost.ToString(),
                    90, 0.030f, inkColor, 38, TextAnchor.MiddleCenter);
            }

            // The buy / next-round prompt lives INSIDE the panel. It used to be HUD text at the
            // top of the screen, where the opaque panel now sits - the canvas draws over world
            // space, so the two simply printed on top of each other.
            ViewUtil.MakeText3D(transform, "Prompt", new Vector2(Center.x, promptY),
                Loc.Pick("Click a block to add it to your deck    -    [N] start ",
                        "Desteye katmak için bloğa tıkla    -    [N] başlat: ")
                    // What comes next is the BOSS STAGE of the round just played when one follows
                    // it - the player is walking into a wall and has to know before they shop.
                    + (session.BossStageFollowsThisRound && !session.InBossStage
                        ? Loc.Pick("the BOSS of round " + session.RoundNumber,
                            session.RoundNumber + ". rauntun PATRONU")
                        : Loc.Pick("round " + (session.RoundNumber + 1),
                            "raunt " + (session.RoundNumber + 1))),
                90, 0.024f, SectionHeaderColor, 38, TextAnchor.MiddleCenter);

            BuildHoverOutline();
            FitToCamera(panelCenter, panelSize);
        }

        /// <summary>
        /// Scales and centres the whole panel on the camera so it always fits on screen. The
        /// shelf grows with the number of sections and with the reroll buttons under each of
        /// them, and it had already grown taller than the viewport - the reroll button was off
        /// the bottom edge.
        ///
        /// Everything drawn here is a child of this transform, so one scale covers the lot.
        /// Hit-testing therefore has to go through world-to-local (see OfferAt).
        /// </summary>
        private void FitToCamera(Vector2 panelCenter, Vector2 panelSize)
        {
            transform.localScale = Vector3.one;
            transform.position = Vector3.zero;
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return;
            }
            const float Margin = 0.94f;
            // Scales UP as well as down. Without that, spacing and readability fight each other:
            // every bit of breathing room added to the layout would come straight out of the
            // text size. Filling the available space means the spacing below is purely a
            // question of PROPORTION, and the panel is always as large as the window allows.
            const float MaxScale = 2.2f;
            float halfHeight = cam.orthographicSize * Margin;
            float halfWidth = halfHeight * cam.aspect;
            float scale = Mathf.Min(MaxScale,
                Mathf.Min(halfHeight / (panelSize.y * 0.5f), halfWidth / (panelSize.x * 0.5f)));
            transform.localScale = new Vector3(scale, scale, 1f);
            // Centre the panel on the camera, so shrinking never parks it off to one side.
            Vector3 camPos = cam.transform.position;
            transform.position = new Vector3(camPos.x - panelCenter.x * scale,
                camPos.y - panelCenter.y * scale, 0f);
        }

        /// <summary>The tag printed at the top of a tile: the tier word replaces the kind word
        /// for rare/legendary (the row header already says which kind it is, and one short word
        /// is all that fits across the tile).</summary>
        private static string TierTag(string kindLabel, Rarity rarity)
        {
            string tier = RarityPalette.Label(rarity);
            return tier ?? kindLabel;
        }

        private static string SectionLabel(MarketOfferKind kind)
        {
            switch (kind)
            {
                case MarketOfferKind.Joker: return Loc.Pick("JOKERS", "JOKERLER");
                case MarketOfferKind.Power: return Loc.Pick("POWERS", "GÜÇLER");
                default: return Loc.Pick("BLOCKS", "BLOKLAR");
            }
        }

        /// <summary>Draws a joker or power offer: a tinted body with a kind tag, the name and
        /// a wrapped description. <paramref name="key"/> is a stable ASCII prefix for the
        /// GameObject names; <paramref name="label"/> is the localized tag shown to the player.</summary>
        private void BuildNamedTile(Vector2 center, int index, string key, string label,
            string displayName, string description, Color bodyColor, Color tagColor)
        {
            ViewUtil.MakeRect(transform, key + "Body_" + index, center,
                new Vector2(NamedTileWidth, TileHeight), bodyColor, 36);
            ViewUtil.MakeText3D(transform, key + "Tag_" + index,
                center + new Vector2(0f, TileHeight * 0.5f - 0.22f), label,
                90, 0.021f, tagColor, 37, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, key + "Name_" + index,
                center + new Vector2(0f, 0.66f), ViewUtil.WrapText(displayName, 16),
                90, 0.029f, JokerNameColor, 37, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, key + "Desc_" + index,
                center + new Vector2(0f, 0.22f),
                ViewUtil.WrapText(description, DescriptionWrap, MaxDescriptionLines),
                90, 0.017f, JokerDescColor, 37, TextAnchor.UpperCenter);
        }

        public void Hide()
        {
            offerVisuals.Clear();
            offerCenters.Clear();
            offerHalfWidths.Clear();
            offerRarities.Clear();
            offerSold.Clear();
            offerAffordable.Clear();
            rerollButtons.Clear();
            // The outline's objects go with every other child below, so drop the references
            // rather than leave four destroyed renderers behind.
            for (int i = 0; i < hoverEdges.Length; i++)
            {
                hoverEdges[i] = null;
            }
            // Undo the fit so a stale scale cannot survive into the next Show.
            transform.localScale = Vector3.one;
            transform.position = Vector3.zero;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        /// <summary>The section whose reroll button is under a world point, or null. Only that
        /// section is refreshed - see GameSession.RerollMarket(kind).</summary>
        public MarketOfferKind? RerollSectionAt(Vector2 world)
        {
            Vector2 local = ToLocal(world);
            for (int i = 0; i < rerollButtons.Count; i++)
            {
                SectionButton button = rerollButtons[i];
                if (Mathf.Abs(local.x - button.Center.x) <= button.Half.x
                    && Mathf.Abs(local.y - button.Center.y) <= button.Half.y)
                {
                    return button.Kind;
                }
            }
            return null;
        }

        /// <summary>World point in the panel's own space. The panel is scaled and re-centred to
        /// fit the screen (FitToCamera), so every hit-test has to come through here.</summary>
        private Vector2 ToLocal(Vector2 world)
        {
            return transform.InverseTransformPoint(new Vector3(world.x, world.y, 0f));
        }

        /// <summary>Offer index under a world point, or -1.</summary>
        /// <summary>Creates the four outline edges, hidden. Sorting order 39 puts them above
        /// every tile piece (36/37) and its price (38), so an outline is never half-buried by
        /// whatever it is drawn around.</summary>
        private void BuildHoverOutline()
        {
            for (int i = 0; i < hoverEdges.Length; i++)
            {
                hoverEdges[i] = ViewUtil.MakeRect(transform, "HoverEdge_" + i, Vector2.zero,
                    Vector2.one, HoverColor, 39);
                hoverEdges[i].enabled = false;
            }
        }

        /// <summary>
        /// Outlines whatever the mouse is over - an offer tile or a section's reroll button -
        /// and hides the outline when it is over neither. Call it every frame while the market
        /// is up; it only moves four existing renderers, so it never rebuilds the shelf.
        ///
        /// Sold offers deliberately do NOT light up: clicking one does nothing, and a highlight
        /// that promises otherwise is worse than none.
        /// </summary>
        public void SetHover(Vector2 world)
        {
            int index = OfferAt(world);
            if (index >= 0 && index < offerSold.Count && !offerSold[index])
            {
                ShowHoverOutline(offerCenters[index],
                    new Vector2(offerHalfWidths[index], TileHeight * 0.5f),
                    offerAffordable[index] ? HoverColor : HoverBlockedColor);
                return;
            }
            Vector2 local = ToLocal(world);
            for (int i = 0; i < rerollButtons.Count; i++)
            {
                SectionButton button = rerollButtons[i];
                if (Mathf.Abs(local.x - button.Center.x) <= button.Half.x
                    && Mathf.Abs(local.y - button.Center.y) <= button.Half.y)
                {
                    ShowHoverOutline(button.Center, button.Half,
                        rerollAffordable ? HoverColor : HoverBlockedColor);
                    return;
                }
            }
            HideHoverOutline();
        }

        /// <summary>Wraps the four edges around a box, in the view's LOCAL space (the panel is
        /// scaled to fit the camera, so world coordinates would be the wrong size).</summary>
        private void ShowHoverOutline(Vector2 center, Vector2 half, Color color)
        {
            if (hoverEdges[0] == null)
            {
                return; // the shelf is not built (or was just hidden)
            }
            Vector2 outer = half + new Vector2(HoverInset, HoverInset);
            float spanX = outer.x * 2f + HoverEdge;
            float spanY = outer.y * 2f + HoverEdge;
            Place(hoverEdges[0], center + new Vector2(0f, outer.y), new Vector2(spanX, HoverEdge));
            Place(hoverEdges[1], center - new Vector2(0f, outer.y), new Vector2(spanX, HoverEdge));
            Place(hoverEdges[2], center - new Vector2(outer.x, 0f), new Vector2(HoverEdge, spanY));
            Place(hoverEdges[3], center + new Vector2(outer.x, 0f), new Vector2(HoverEdge, spanY));
            for (int i = 0; i < hoverEdges.Length; i++)
            {
                hoverEdges[i].color = color;
                hoverEdges[i].enabled = true;
            }
        }

        private static void Place(SpriteRenderer edge, Vector2 center, Vector2 size)
        {
            edge.transform.localPosition = new Vector3(center.x, center.y, 0f);
            edge.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        /// <summary>Drops the hover outline - the shelf is covered by a modal, or gone. Safe to
        /// call when no market is showing.</summary>
        public void ClearHover()
        {
            HideHoverOutline();
        }

        private void HideHoverOutline()
        {
            for (int i = 0; i < hoverEdges.Length; i++)
            {
                if (hoverEdges[i] != null)
                {
                    hoverEdges[i].enabled = false;
                }
            }
        }

        public int OfferAt(Vector2 world)
        {
            Vector2 local = ToLocal(world);
            for (int i = 0; i < offerCenters.Count; i++)
            {
                if (Mathf.Abs(local.x - offerCenters[i].x) <= offerHalfWidths[i]
                    && Mathf.Abs(local.y - offerCenters[i].y) <= TileHeight * 0.5f)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Flies the bought card toward the draw pile. Call BEFORE Show() rebuilds;
        /// the visual is re-parented so the rebuild does not destroy it mid-flight.</summary>
        public void PlayBuyFx(int offerIndex)
        {
            if (offerIndex < 0 || offerIndex >= offerVisuals.Count || offerVisuals[offerIndex] == null)
            {
                return;
            }
            CardVisual visual = offerVisuals[offerIndex];
            offerVisuals[offerIndex] = null;
            visual.transform.SetParent(transform.parent, true);
            visual.SetSortingBoost(3);
            visual.FlyToAndDestroy(CardLayerView.DrawPilePos, 0.35f);
        }

        /// <summary>Flies a stand-in joker tile from the offer slot toward the joker bar
        /// (target is world-space, computed by the controller). Call BEFORE Show() rebuilds;
        /// the fx object is parented outside this view so the rebuild leaves it alone.</summary>
        public void PlayJokerBuyFx(int offerIndex, Vector2 target)
        {
            PlayTileBuyFx(offerIndex, target, Loc.Pick("JOKER", "JOKER"), JokerBodyColor, JokerTagColor);
        }

        /// <summary>The power twin of PlayJokerBuyFx (target: the power bar).</summary>
        public void PlayPowerBuyFx(int offerIndex, Vector2 target)
        {
            PlayTileBuyFx(offerIndex, target, Loc.Pick("POWER", "GÜÇ"), PowerBodyColor, PowerTagColor);
        }

        private void PlayTileBuyFx(int offerIndex, Vector2 target, string tag,
            Color bodyColor, Color tagColor)
        {
            if (offerIndex < 0 || offerIndex >= offerCenters.Count)
            {
                return;
            }
            // Fly away wearing the tile's colours, rarity included.
            Rarity rarity = offerIndex < offerRarities.Count ? offerRarities[offerIndex] : Rarity.Common;
            bodyColor = RarityPalette.Tint(bodyColor, rarity);
            if (rarity != Rarity.Common)
            {
                tagColor = RarityPalette.Accent(rarity);
                tag = RarityPalette.Label(rarity);
            }
            var root = new GameObject(tag + "BuyFx");
            root.transform.SetParent(transform.parent, false);
            // The fx lives OUTSIDE this view (so the rebuild cannot destroy it), and the view
            // is scaled to fit - so the slot's world position has to be resolved here.
            root.transform.position = transform.TransformPoint(offerCenters[offerIndex]);
            ViewUtil.MakeRect(root.transform, "Body", Vector2.zero,
                new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight), bodyColor, 39);
            ViewUtil.MakeText3D(root.transform, "Tag", Vector2.zero, tag,
                90, 0.016f, tagColor, 39, TextAnchor.MiddleCenter);
            StartCoroutine(FlyShrinkAndDestroy(root.transform, target, 0.4f));
        }

        private static System.Collections.IEnumerator FlyShrinkAndDestroy(Transform fx,
            Vector2 target, float duration)
        {
            Vector3 from = fx.position;
            var to = new Vector3(target.x, target.y, from.z);
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
                fx.position = Vector3.Lerp(from, to, t);
                float scale = Mathf.Lerp(1f, 0.35f, t);
                fx.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            Object.Destroy(fx.gameObject);
        }
    }
}
