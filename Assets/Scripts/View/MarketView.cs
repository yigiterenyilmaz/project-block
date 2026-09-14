// PURPOSE: The between-rounds market screen: block-card, joker and power offers with
// prices, click to buy. Rebuilt from scratch on every change (cheap at this scale).
//
// ============================ TEMPORARY DEMO LAYOUT ==================================
// There are TWO layouts in this file and ONE switch between them: DemoLayout.
//
//   true  - the DEMO shelf. Flat generated rects, no art: blocks down the left, jokers
//           and powers down the right, a reroll button on every section, a PROCEED
//           button bottom right, and NO SCROLL - the whole market is on screen at once.
//   false - the SHIPPED shelf. Painted menu_frame / menu_section art, three sections
//           stacked in a scrolling window, refreshed by HOLDING a section.
//
// ROLLING BACK IS ONE CHARACTER: set DemoLayout to false. Everything the old layout
// needs is still here and still compiled - the demo adds paths, it deletes none. The
// pieces the two share (BuildOffer, BuildNamedTile, the hover outline, the buy fx, every
// hit test) are written once and used by both, so the demo cannot drift from the real
// thing while it stands. Delete the DEMO LAYOUT region at the bottom of this file and
// the switch to remove it for good.
// =====================================================================================
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
        /// <summary>THE ROLLBACK SWITCH - see the header. true is the temporary demo shelf,
        /// false the shipped painted one. static readonly rather than const on purpose: a const
        /// makes every branch of the layout that is currently off unreachable code, and six
        /// CS0162 warnings in the console is a poor way to say "this half is switched off".</summary>
        private static readonly bool DemoLayout = true;


        /// <summary>Vertical distance between two section rows.</summary>
        private const float RowPitch = 3.7f;

        /// <summary>Section header above a row's tiles / price label below them.</summary>

        /// <summary>Reroll button size and how far it sits clear of the row's widest tile.</summary>

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

        /// <summary>The empty icon recess at the top of a joker/power card.</summary>
        private static readonly Color NamedWellColor = new Color(0.05f, 0.05f, 0.08f, 0.55f);
        private static readonly Color PowerBodyColor = new Color(0.12f, 0.30f, 0.34f);
        private static readonly Color PowerTagColor = new Color(0.55f, 0.92f, 0.95f);


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

        /// <summary>Each offer's tile size, which the COMPARTMENT decides rather than the tile
        /// kind - a shelf that is 5.03 by 1.29 gets tiles that fit 5.03 by 1.29.</summary>
        private readonly List<Vector2> offerTileSizes = new List<Vector2>();

        /// <summary>Index-aligned with the offers, so the buy fx can fly away in the same
        /// rarity colour the tile had (Common for block offers).</summary>
        private readonly List<Rarity> offerRarities = new List<Rarity>();

        /// <summary>Half-width of each offer slot, index-aligned with the offers. Joker and
        /// power tiles are wider than block cards, so one shared width would both mis-frame
        /// them and mis-answer OfferAt.</summary>
        private readonly List<float> offerHalfWidths = new List<float>();

        private readonly List<float> offerHalfHeights = new List<float>();

        /// <summary>One reroll button per section - refreshing the blocks must not disturb the
        /// jokers standing next to them.</summary>

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

        /// <summary>What each offer says about itself, for the strip under the panel. Index
        /// aligned with the offers, like everything else in this file.</summary>
        private readonly List<string> offerDetails = new List<string>();

        private TextMesh detailText;

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
            shownSession = session;
            // The hold gesture still costs what the buttons used to, on the same shared counter.
            long rerollCost = session.NextRerollCost;
            rerollAffordable = session.TotalScore >= rerollCost;
            rerollCostText = rerollCost.ToString();
            IReadOnlyList<MarketOffer> offers = session.Market.Offers;
            int count = offers.Count;

            for (int i = 0; i < count; i++)
            {
                offerCenters.Add(Vector2.zero);
                offerTileSizes.Add(Vector2.zero);
                offerHalfWidths.Add(CardVisual.BodyWidth * 0.5f);
                offerHalfHeights.Add(0.5f);
                offerSold.Add(offers[i].Sold);
                // "Affordable" is what the hover outline and the price colour read, and for a
                // BLOCK it is two questions, not one: the money, and whether the run still has
                // room under its purchase limit. A shelf you cannot buy from must not light up
                // as though you could.
                offerAffordable.Add(session.TotalScore >= offers[i].Price
                    && (offers[i].Kind != MarketOfferKind.Block || session.CanBuyMoreCards));
                offerDetails.Add(OfferDetail(offers[i]));
            }

            if (DemoLayout)
            {
                BuildDemoShelf(session, offers, count);
                return;
            }

            // ---- THE FRAME. Drawn at its own pixels-per-unit and never magnified past it; see
            // FitToCamera. Everything below is measured out of this picture rather than invented.
            ViewUtil.MakeIcon(transform, "Frame", FrameCenter, 1f, Color.white, 30,
                ViewUtil.UiSprite("menu_frame"));
            Rect titleTab = FrameRegion(TitleTabRegion);
            ViewUtil.MakeText3D(transform, "Title", titleTab.center, Loc.Pick("MARKET", "MARKET"),
                60, 0.068f, PanelCreamColor, 38, TextAnchor.MiddleCenter);

            // ---- THE WINDOW. The frame's cream interior is a viewport, not a shelf: the three
            // sections are taller than it and you scroll them past it. That is the whole point of
            // splitting the art in two - the old single panel had to fit blocks, jokers and
            // powers on one screen, so all three had to be small.
            Rect window = FrameRegion(WindowRegion);
            windowRect = window;

            float sectionHeight = SectionRegion(SectionBoxRegion).height;
            float pitch = sectionHeight + SectionGap;
            contentHeight = SectionCount * sectionHeight + (SectionCount - 1) * SectionGap;
            maxScroll = Mathf.Max(0f, contentHeight - window.height);
            scroll = Mathf.Clamp(scroll, 0f, maxScroll);

            // Sprites are CLIPPED by a mask over the window, so a half-scrolled section slides
            // under the frame's edge instead of over it. Text is a MeshRenderer and a sprite mask
            // cannot touch it, so text outside the window is simply never built - which costs
            // nothing, because a scroll rebuilds the whole shelf anyway.
            BuildWindowMask(window);

            var offerBuckets = new List<int>[SectionCount];
            for (int s = 0; s < SectionCount; s++)
            {
                offerBuckets[s] = new List<int>();
            }
            for (int i = 0; i < count; i++)
            {
                offerBuckets[SectionIndex(offers[i].Kind)].Add(i);
            }

            Rect boxLocal = SectionRegion(SectionBoxRegion);
            Rect tabLocal = SectionRegion(SectionTabRegion);
            Rect contentLocal = SectionRegion(SectionContentRegion);

            for (int s = 0; s < SectionCount; s++)
            {
                // Where this section's visible box sits once the scroll is applied.
                float boxTop = window.yMax + scroll - s * pitch;
                float centerY = boxTop - boxLocal.yMax;
                var center = new Vector2(window.center.x, centerY);
                sectionCenters[s] = center;
                sectionHalf = new Vector2(boxLocal.width * 0.5f, boxLocal.height * 0.5f);

                // Wholly past the window on either side: nothing to draw at all.
                if (centerY + boxLocal.yMax < window.yMin - 0.05f
                    || centerY + boxLocal.yMin > window.yMax + 0.05f)
                {
                    continue;
                }

                SpriteRenderer panel = ViewUtil.MakeIcon(transform, "Section_" + s, center, 1f,
                    Color.white, 31, ViewUtil.UiSprite("menu_section"));
                Masked(panel);

                Rect tab = Shift(tabLocal, center);
                if (InsideWindow(tab.center, window))
                {
                    ViewUtil.MakeText3D(transform, "SectionTitle_" + s, tab.center,
                        SectionLabel(KindOf(s)), 60, 0.048f, PanelInkColor, 38,
                        TextAnchor.MiddleCenter);
                }

                // The hold-to-refresh readout, on the tab beside the title. It is the only thing
                // that says the gesture exists, so it is always drawn while the section is up.
                Rect content = Shift(contentLocal, center);
                sectionContent[s] = content;
                if (InsideWindow(content.center, window))
                {
                    float hold = holdSection == s ? holdTimer / HoldSeconds : 0f;
                    ViewUtil.MakeText3D(transform, "SectionHint_" + s,
                        new Vector2(content.xMax - 0.06f, tab.center.y),
                        hold > 0f
                            ? Loc.Pick("refreshing...", "yenileniyor...")
                            : Loc.Pick("hold to refresh  " + rerollCostText,
                                "yenilemek için basılı tut  " + rerollCostText),
                        90, 0.022f, rerollAffordable ? SectionHeaderColor : TooExpensiveColor,
                        38, TextAnchor.MiddleRight);
                    if (hold > 0f)
                    {
                        // Fills across the top of the content area as the hold builds.
                        float w = content.width * Mathf.Clamp01(hold);
                        Masked(ViewUtil.MakeRect(transform, "HoldBar_" + s,
                            new Vector2(content.xMin + w * 0.5f, content.yMax - 0.05f),
                            new Vector2(w, 0.07f), AffordablePriceColor, 38));
                    }
                }

                List<int> bucket = offerBuckets[s];
                if (bucket.Count == 0)
                {
                    continue;
                }
                float slot = content.width / bucket.Count;
                var tile = new Vector2(slot - SectionPadX * 2f, content.height - SectionPadY * 2f);
                for (int c = 0; c < bucket.Count; c++)
                {
                    int i = bucket[c];
                    offerCenters[i] = new Vector2(content.xMin + slot * (c + 0.5f),
                        content.center.y);
                    offerTileSizes[i] = tile;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (offerTileSizes[i] == Vector2.zero)
                {
                    // Its section is scrolled out of the window: nothing was placed for it, and
                    // OfferAt must not be able to answer with it either.
                    offerVisuals.Add(null);
                    offerRarities.Add(Rarity.Common);
                    continue;
                }
                BuildOffer(session, offers, i);
            }

            BuildSideColumn(session);
            BuildHoverOutline();
            FitToCamera(FrameCenter,
                new Vector2(FramePixelWidth / FramePpu, FramePixelHeight / FramePpu));
        }

        /// <summary>One offer, at the place the section loop worked out for it.</summary>
        private void BuildOffer(GameSession session, IReadOnlyList<MarketOffer> offers, int i)
        {
            Vector2 slotCenter = offerCenters[i];
            MarketOffer offer = offers[i];
            Rarity rarity = offer.Kind == MarketOfferKind.Joker ? offer.Joker.Rarity
                : offer.Kind == MarketOfferKind.Power ? offer.Power.Rarity
                : Rarity.Common;
            offerRarities.Add(rarity);
            Vector2 tileSize = offerTileSizes[i];
            offerHalfWidths[i] = tileSize.x * 0.5f;
            offerHalfHeights[i] = tileSize.y * 0.5f;

            // A joker/power card draws its own rounded body and rarity rim (BuildNamedTile); a
            // square frame behind it would show at the corners. Blocks and sold slots keep it.
            if (offer.Kind == MarketOfferKind.Block || offer.Sold)
            {
                Masked(ViewUtil.MakeRect(transform, "Frame_" + i, slotCenter, tileSize,
                    RarityPalette.Frame(FrameColor, rarity), 34));
            }
            // The tile's whole HEIGHT has to be inside, not just its middle: a tile that is
            // half out of the window would otherwise print its name and price past the edge.
            bool text = InsideWindow(slotCenter, windowRect)
                && InsideWindow(slotCenter + new Vector2(0f, tileSize.y * 0.5f), windowRect)
                && InsideWindow(slotCenter - new Vector2(0f, tileSize.y * 0.5f), windowRect);

            if (offer.Sold)
            {
                offerVisuals.Add(null);
                if (text)
                {
                    ViewUtil.MakeText3D(transform, "Sold_" + i, slotCenter,
                        Loc.Pick("SOLD", "SATILDI"), 60, 0.06f, SoldColor, 38,
                        TextAnchor.MiddleCenter);
                }
                return;
            }
            if (offer.Kind == MarketOfferKind.Joker)
            {
                offerVisuals.Add(null);
                BuildNamedTile(slotCenter, i, "Joker", TierTag(Loc.Pick("JOKER", "JOKER"), rarity),
                    offer.Joker.DisplayName, DemoLayout ? string.Empty : offer.Joker.Description,
                    RarityPalette.Tint(JokerBodyColor, rarity),
                    rarity == Rarity.Common ? JokerTagColor : RarityPalette.Accent(rarity), text,
                    ViewUtil.JokerIcon(offer.Joker.DefId));
            }
            else if (offer.Kind == MarketOfferKind.Power)
            {
                offerVisuals.Add(null);
                BuildNamedTile(slotCenter, i, "Power", TierTag(Loc.Pick("POWER", "GÜÇ"), rarity),
                    offer.Power.DisplayName, DemoLayout ? string.Empty : offer.Power.Description,
                    RarityPalette.Tint(PowerBodyColor, rarity),
                    rarity == Rarity.Common ? PowerTagColor : RarityPalette.Accent(rarity), text,
                    ViewUtil.PowerIcon(offer.Power.DefId));
            }
            else
            {
                CardVisual visual = CardVisual.Create(transform, "Offer_" + i, offer.Card,
                    true, false, slotCenter, 36);
                float fit = Mathf.Min(tileSize.x / CardVisual.BodyWidth,
                    tileSize.y / CardVisual.BodyHeight) * 0.78f;
                visual.transform.localScale = new Vector3(fit, fit, 1f);
                // A card is a SUBTREE of renderers, not one - body, every cube, the tint. Each
                // of them has to clip or a scrolled block card sails out over the frame, which
                // is exactly what it did: this call was written and then lost, and the block
                // shelf was the only one that showed it because it is the only one using
                // CardVisual.
                MaskAll(visual.transform);
                offerVisuals.Add(visual);
            }
            if (text)
            {
                // The run's block limit reads exactly like being unable to pay - the price goes
                // red - and says WHY over the tile, because "you have the money and it still
                // will not sell" is the one refusal a price tag cannot explain.
                bool blockedByLimit = offer.Kind == MarketOfferKind.Block
                    && !session.CanBuyMoreCards;
                bool affordable = session.TotalScore >= offer.Price && !blockedByLimit;
                // A joker/power card is narrower than a block tile and its name sits just above
                // the price, so its price is set smaller and lower.
                bool card = offer.Kind != MarketOfferKind.Block;
                ViewUtil.MakeText3D(transform, "Price_" + i,
                    slotCenter + new Vector2(0f, -tileSize.y * 0.5f
                        + (card ? 0.12f * tileSize.x : 0.16f)),
                    offer.Price.ToString(), 60, card ? 0.042f * tileSize.x : 0.060f,
                    affordable ? AffordablePriceColor : TooExpensiveColor, 38,
                    TextAnchor.MiddleCenter);
                if (blockedByLimit)
                {
                    ViewUtil.MakeText3D(transform, "Limit_" + i, slotCenter,
                        Loc.Pick("LIMIT", "LIMIT"), 60, 0.05f, TooExpensiveColor, 38,
                        TextAnchor.MiddleCenter);
                }
            }
        }

        /// <summary>Balance, hints and the hovered offer's description, in the room beside the
        /// frame. The frame is 7.1 units of a 17.8-unit screen and has no spare cream inside it,
        /// so everything that is not an offer lives out here.</summary>
        private void BuildSideColumn(GameSession session)
        {
            float sideX = FrameCenter.x - FramePixelWidth / FramePpu * 0.5f - 0.55f;
            ViewUtil.MakeText3D(transform, "Balance", new Vector2(sideX, FrameCenter.y + 2.0f),
                Loc.Pick("You have ", "Paran: ") + session.TotalScore,
                90, 0.032f, new Color(1f, 0.86f, 0.42f), 38, TextAnchor.MiddleRight);
            ViewUtil.MakeText3D(transform, "SellHint", new Vector2(sideX, FrameCenter.y + 1.4f),
                Loc.Pick("Click a joker or a power to sell it",
                    "Satmak için jokere veya güce tıkla"),
                90, 0.024f, SectionHeaderColor, 38, TextAnchor.MiddleRight);
            ViewUtil.MakeText3D(transform, "SellHint2", new Vector2(sideX, FrameCenter.y + 1.1f),
                Loc.Pick("Click the deck pile to sell cards",
                    "Kart satmak için desteye tıkla"),
                90, 0.024f, SectionHeaderColor, 38, TextAnchor.MiddleRight);
            // How much of the run's block allowance is spent. Written beside the shelf rather
            // than on it, because it is a fact about the RUN and not about any one offer.
            ViewUtil.MakeText3D(transform, "CardLimit", new Vector2(sideX, FrameCenter.y + 0.4f),
                Loc.Pick("Blocks bought  ", "Alınan blok  ")
                    + session.PurchasedCardCount + "/" + session.CardPurchaseLimit,
                90, 0.024f,
                session.CanBuyMoreCards ? SectionHeaderColor : TooExpensiveColor, 38,
                TextAnchor.MiddleRight);
            ViewUtil.MakeText3D(transform, "ScrollHint", new Vector2(sideX, FrameCenter.y + 0.7f),
                maxScroll > 0.01f
                    ? Loc.Pick("Scroll for more", "Devamı için kaydır")
                    : string.Empty,
                90, 0.024f, SectionHeaderColor, 38, TextAnchor.MiddleRight);
            ViewUtil.MakeText3D(transform, "Prompt", new Vector2(sideX, FrameCenter.y - 1.6f),
                ProceedControl + Loc.Pick(" start ", " başlat ")
                    + (session.BossStageFollowsThisRound && !session.InBossStage
                        ? Loc.Pick("the BOSS of round " + session.RoundNumber,
                            session.RoundNumber + ". rauntun PATRONU")
                        : Loc.Pick("round " + (session.RoundNumber + 1),
                            "raunt " + (session.RoundNumber + 1))),
                90, 0.024f, SectionHeaderColor, 38, TextAnchor.MiddleRight);

            detailText = ViewUtil.MakeText3D(transform, "Detail",
                new Vector2(FrameCenter.x + FramePixelWidth / FramePpu * 0.5f + 0.55f,
                    FrameCenter.y + 1.2f),
                string.Empty, 90, 0.026f, SectionHeaderColor, 38, TextAnchor.UpperLeft);
        }

        // ================= THE TWO TEMPLATES, and every region measured off them ==============
        // The market is drawn from two pictures now, not one. menu_frame is the OUTER shell - a
        // fixed window with a title tab - and menu_section is the panel a single kind of offer
        // lives in. Splitting them is what makes the shelf scrollable: the old single panel had
        // to hold blocks, jokers and powers at once, so each of them got a third of the height
        // and everything in it had to be tiny. A section panel is now 4.42 by 1.98 against the
        // 5.03 by 1.29 it used to get - half again as tall - and the three of them are simply
        // taller than the window, which is what scrolling is for.
        //
        // Every figure below is a pixel coordinate in its own PNG. Redraw the art at another
        // size and only the Ppu changes; move a region in the art and only its four numbers do.
        private const float FramePpu = 152f;

        private const float FramePixelWidth = 1081f;

        private const float FramePixelHeight = 1455f;

        private static readonly Vector2 FrameCenter = Vector2.zero;

        /// <summary>The dark tab at the top of the shell, which is where MARKET goes.</summary>
        private static readonly Vector4 TitleTabRegion = new Vector4(328f, 28f, 752f, 170f);

        /// <summary>The shell's cream interior. NOT a shelf - a viewport the sections scroll
        /// past.</summary>
        private static readonly Vector4 WindowRegion = new Vector4(159f, 210f, 923f, 1291f);

        private const float SectionPpu = 305f;

        private const float SectionPixelWidth = 1536f;

        private const float SectionPixelHeight = 1024f;

        /// <summary>What the section sprite actually COVERS - the drawing sits inside a larger
        /// canvas with a glow around it, so the sprite's own size is not what stacks.</summary>
        private static readonly Vector4 SectionBoxRegion = new Vector4(51f, 87f, 1486f, 938f);

        /// <summary>The cream tab at the section's top left: BLOKLAR, JOKERLER, GÜÇLER.</summary>
        private static readonly Vector4 SectionTabRegion = new Vector4(184f, 117f, 733f, 233f);

        private static readonly Vector4 SectionContentRegion =
            new Vector4(94f, 271f, 1442f, 874f);

        /// <summary>Air between stacked sections.</summary>
        private const float SectionGap = 0.25f;

        private const int SectionCount = 3;

        /// <summary>How much of a section a tile leaves as breathing room, per side.</summary>
        private const float SectionPadX = 0.10f;

        private const float SectionPadY = 0.09f;

        /// <summary>How long the button has to be held on a section to refresh it. The refresh
        /// BUTTONS are gone - there is nowhere on this art to put three of them, and a button
        /// that small was a poor target anyway. Holding the panel you want refreshed says which
        /// one without needing a control at all.</summary>
        private const float HoldSeconds = 0.55f;

        /// <summary>World units of scroll per notch of wheel.</summary>
        private const float ScrollPerNotch = 0.55f;

        private const float DetailWrap = 22;

        /// <summary>The two colours the art is painted in, sampled from it.</summary>
        private static readonly Color PanelInkColor = new Color(0.161f, 0.204f, 0.282f);

        private static readonly Color PanelCreamColor = new Color(0.961f, 0.882f, 0.800f);

        /// <summary>A pixel rectangle in the SHELL art, as world space.</summary>
        private static Rect FrameRegion(Vector4 px)
        {
            float left = FrameCenter.x - FramePixelWidth / FramePpu * 0.5f;
            float top = FrameCenter.y + FramePixelHeight / FramePpu * 0.5f;
            return new Rect(left + px.x / FramePpu, top - px.w / FramePpu,
                (px.z - px.x) / FramePpu, (px.w - px.y) / FramePpu);
        }

        /// <summary>A pixel rectangle in the SECTION art, relative to that panel's own centre.</summary>
        private static Rect SectionRegion(Vector4 px)
        {
            float left = -SectionPixelWidth / SectionPpu * 0.5f;
            float top = SectionPixelHeight / SectionPpu * 0.5f;
            return new Rect(left + px.x / SectionPpu, top - px.w / SectionPpu,
                (px.z - px.x) / SectionPpu, (px.w - px.y) / SectionPpu);
        }

        private static Rect Shift(Rect r, Vector2 by)
        {
            return new Rect(r.x + by.x, r.y + by.y, r.width, r.height);
        }

        /// <summary>Which section a kind belongs in - fixed by the ART, so an empty section
        /// leaves its panel empty instead of sliding the others up.</summary>
        private static int SectionIndex(MarketOfferKind kind)
        {
            return kind == MarketOfferKind.Block ? 0 : kind == MarketOfferKind.Joker ? 1 : 2;
        }

        private static MarketOfferKind KindOf(int section)
        {
            return section == 0 ? MarketOfferKind.Block
                : section == 1 ? MarketOfferKind.Joker : MarketOfferKind.Power;
        }

        // ---- scroll and hold state ----

        /// <summary>What the last Show was given, so a scroll can lay the shelf out again. The
        /// whole thing is rebuilt on every scroll, which is what DeckOverlayView does and what
        /// keeps offerCenters in this transform's own space - the alternative is a moving parent
        /// and a hit test that has to know about it.</summary>
        private GameSession shownSession;

        private float scroll;

        private float maxScroll;

        private float contentHeight;

        private Rect windowRect;

        private readonly Rect[] sectionContent = new Rect[SectionCount];

        private readonly Vector2[] sectionCenters = new Vector2[SectionCount];

        private Vector2 sectionHalf;

        private SpriteMask windowMask;

        private int holdSection = -1;

        private float holdTimer;

        private string rerollCostText = string.Empty;

        /// <summary>Fired when a section has been held long enough to refresh. The view never
        /// touches money or the market - it only reports the gesture, the same way clicking an
        /// offer is reported.</summary>
        public System.Action<MarketOfferKind> SectionHeld;

        /// <summary>DEMO: fired when the PROCEED button is clicked. Same contract as
        /// SectionHeld - the view reports the gesture and the controller decides what a
        /// "leave the market" actually is.</summary>
        public System.Action ProceedPressed;

        /// <summary>What to call the "start the next round" control - "[N]" for a keyboard, the
        /// pad's own button when one is driving. A delegate rather than a string because the
        /// shelf is rebuilt constantly and the answer changes the moment a stick is nudged; the
        /// controller owns the question (see GameUiController.PadPrompts.PadOr).</summary>
        public System.Func<string> ProceedHint;

        private string ProceedControl
        {
            get { return ProceedHint != null ? ProceedHint() : "[N]"; }
        }

        /// <summary>DEMO: where each section's reroll button ended up, in this view's own
        /// space (the panel is scaled to fit, so hit tests come through ToLocal). A zero-width
        /// rect means "not built", which is what an empty shelf leaves behind.</summary>
        private readonly Rect[] demoRerollRects = new Rect[SectionCount];

        private Rect demoProceedRect;

        /// <summary>DEMO: the hit box around each section's NAME. Hovering it explains what that
        /// whole shelf is - not what one offer on it does, which is the offer's own tooltip.</summary>
        private readonly Rect[] demoLabelRects = new Rect[SectionCount];

        /// <summary>Puts a renderer under the window mask. Sprites clip against the shell's
        /// interior; without this a half-scrolled section draws over the frame.</summary>
        private void Masked(SpriteRenderer renderer)
        {
            if (windowMask == null)
            {
                return; // no window, nothing to clip against - and clipping to a mask that
                        // does not exist hides the sprite outright
            }
            if (renderer != null)
            {
                renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
        }

        /// <summary>Puts a whole subtree under the window mask.</summary>
        private void MaskAll(Transform root)
        {
            if (windowMask == null)
            {
                return; // see Masked
            }
            SpriteRenderer[] all = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                all[i].maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
        }

        private static bool InsideWindow(Vector2 p, Rect window)
        {
            return p.y > window.yMin + 0.06f && p.y < window.yMax - 0.06f;
        }

        private void BuildWindowMask(Rect window)
        {
            var go = new GameObject("WindowMask");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(window.center.x, window.center.y, 0f);
            go.transform.localScale = new Vector3(window.width, window.height, 1f);
            windowMask = go.AddComponent<SpriteMask>();
            windowMask.sprite = ViewUtil.WhiteSprite;
        }

        /// <summary>Scrolls the shelf and lays it out again. Clamped, so the ends are hard.</summary>
        public bool Scroll(float notches)
        {
            if (DemoLayout && !UiLayout.Active.MarketStacked)
            {
                return false; // side by side, the whole shelf is on screen at once
            }
            if (shownSession == null || maxScroll <= 0.001f)
            {
                return false;
            }
            float next = Mathf.Clamp(scroll + notches * ScrollPerNotch, 0f, maxScroll);
            if (Mathf.Approximately(next, scroll))
            {
                return false;
            }
            scroll = next;
            Show(shownSession);
            return true;
        }

        public void ResetScroll()
        {
            scroll = 0f;
        }

        /// <summary>Which section a world point is over, or -1. This is the target of the
        /// hold-to-refresh gesture, and it is the PANEL that is the target - not a button on it.</summary>
        public int SectionAt(Vector2 world)
        {
            if (DemoLayout)
            {
                // The demo has a REROLL BUTTON on every section, so the hold gesture is off:
                // leaving both live would refresh the shelf under a player who just meant to
                // press and think.
                return -1;
            }
            // An OFFER is not part of its section for this purpose. Clicking buys on the button
            // going down, so without this a press-and-hold on a tile would buy the offer and
            // then refresh the shelf out from under it. What is left to grab is the tab strip,
            // the gaps between tiles and the panel's own margin - which is also the more
            // sensible gesture: you hold the SHELF, not a thing on it.
            if (OfferAt(world) >= 0)
            {
                return -1;
            }
            Vector2 local = ToLocal(world);
            if (local.y < windowRect.yMin || local.y > windowRect.yMax)
            {
                return -1;
            }
            for (int s = 0; s < SectionCount; s++)
            {
                if (Mathf.Abs(local.x - sectionCenters[s].x) <= sectionHalf.x
                    && Mathf.Abs(local.y - sectionCenters[s].y) <= sectionHalf.y)
                {
                    return s;
                }
            }
            return -1;
        }

        /// <summary>Drives the hold. The caller says where the pointer is and whether it is
        /// down; this counts, and fires SectionHeld once when the count is made. Letting go, or
        /// sliding off the panel, starts over - a hold is a commitment to one shelf.</summary>
        public void UpdateHold(Vector2 world, bool held)
        {
            int section = held ? SectionAt(world) : -1;
            if (section < 0 || section != holdSection)
            {
                bool wasCounting = holdSection >= 0;
                holdSection = section;
                holdTimer = 0f;
                if (wasCounting && shownSession != null)
                {
                    Show(shownSession);   // clear the progress readout
                }
                return;
            }
            float before = holdTimer;
            holdTimer += Time.deltaTime;
            if (holdTimer >= HoldSeconds)
            {
                MarketOfferKind kind = KindOf(holdSection);
                holdSection = -1;
                holdTimer = 0f;
                if (SectionHeld != null)
                {
                    SectionHeld(kind);
                }
                return;
            }
            // Redraw only when the bar would actually move, not every frame.
            if (Mathf.FloorToInt(before * 12f) != Mathf.FloorToInt(holdTimer * 12f)
                && shownSession != null)
            {
                Show(shownSession);
            }
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
            const float Margin = 0.98f;
            // THE CAP IS THE TEXTURE, NOT THE NUMBER 1. The panel is painted art, so the thing
            // that must not happen is magnifying it past its own pixels - and where that lands
            // depends on the screen. FramePpu / (pixels per world unit) is exactly the scale at
            // which one texture pixel covers one screen pixel: 1.45 at 1080p, 1.09 at 1440p.
            // A flat cap of 1 threw that headroom away and left the shelf small on every screen
            // it was already sharp on. Floored at 1 so a 4K screen - where scale 1 is ALREADY a
            // magnification and nothing can fix that - fills the window rather than shrinking to
            // chase a sharpness it cannot have.
            float pixelsPerUnit = Screen.height / (cam.orthographicSize * 2f);
            float MaxScale = Mathf.Max(1f,
                pixelsPerUnit > 0.01f ? FramePpu / pixelsPerUnit : 1f);
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

        /// <summary>
        /// The footer for a STACKED shelf. Two hint lines and a corner button do not fit across a
        /// phone - they end up printed over each other - so the strip is taller, the hint is one
        /// centred line, and PROCEED becomes the full-width primary action a thumb expects at the
        /// bottom of a screen.
        /// </summary>
        private void BuildStackedFooter(GameSession session, Rect panel)
        {
            ViewUtil.MakeText3D(transform, "DemoHint1",
                new Vector2(panel.center.x, panel.yMin + DemoFooterHeight - 0.22f),
                Loc.Pick("Tap a joker or power on the bars above to sell it",
                    "Joker ya da güç satmak için üstteki barlara dokun"),
                90, 0.021f, SectionHeaderColor, 38, TextAnchor.MiddleCenter);

            // TWO buttons, because the panel now covers the whole screen and the deck pile it
            // used to leave visible is underneath it. The deck is the SELL screen, so losing the
            // way in would lose selling altogether - it gets its own button rather than a hint
            // pointing at something the player cannot see.
            float gap = 0.18f;
            float full = panel.width - DemoPad * 2f;
            float deckWidth = full * 0.34f;
            float proceedWidth = full - deckWidth - gap;
            float buttonY = panel.yMin + 0.52f;
            var deckCentre = new Vector2(panel.xMin + DemoPad + deckWidth * 0.5f, buttonY);
            var deckSize = new Vector2(deckWidth, 0.86f);
            demoDeckRect = new Rect(deckCentre.x - deckWidth * 0.5f,
                deckCentre.y - deckSize.y * 0.5f, deckWidth, deckSize.y);
            ViewUtil.MakeRect(transform, "DemoDeck", deckCentre, deckSize, DemoDeckColor, 35);
            ViewUtil.MakeText3D(transform, "DemoDeckLabel", deckCentre + new Vector2(0f, 0.12f),
                Loc.Pick("DECK", "DESTE"), 60, 0.040f, PanelCreamColor, 38,
                TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "DemoDeckSub", deckCentre - new Vector2(0f, 0.17f),
                Loc.Pick("sell cards", "kart sat"), 90, 0.024f, PanelCreamColor, 38,
                TextAnchor.MiddleCenter);

            var size = new Vector2(proceedWidth, 0.86f);
            var centre = new Vector2(panel.xMax - DemoPad - proceedWidth * 0.5f, buttonY);
            demoProceedRect = new Rect(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f,
                size.x, size.y);
            ViewUtil.MakeRect(transform, "DemoProceed", centre, size, DemoProceedColor, 35);
            ViewUtil.MakeText3D(transform, "DemoProceedLabel", centre + new Vector2(0f, 0.13f),
                Loc.Pick("PROCEED", "DEVAM"), 60, 0.042f, PanelCreamColor, 38,
                TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "DemoProceedSub", centre - new Vector2(0f, 0.17f),
                session.BossStageFollowsThisRound && !session.InBossStage
                    ? Loc.Pick("BOSS of round " + session.RoundNumber,
                        session.RoundNumber + ". rauntun PATRONU")
                    : Loc.Pick("round " + (session.RoundNumber + 1),
                        "raunt " + (session.RoundNumber + 1)),
                90, 0.024f, PanelCreamColor, 38, TextAnchor.MiddleCenter);
        }

        /// <summary>True if a world point is on the stacked footer's DECK button.</summary>
        public bool TryDeckAt(Vector2 world)
        {
            return demoDeckRect.width > 0.001f && demoDeckRect.Contains(ToLocal(world));
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

        /// <summary>WHY A DEMO TILE HAS NO DESCRIPTION. Hovering an offer already opens the full
        /// tooltip - name, description and price - through GameUiController.Tooltips, and that
        /// tooltip has room for all of it. What the tile could show was two lines and an
        /// ellipsis, which is not enough to decide a purchase on and is enough to crowd the name
        /// and the price it is sitting between. So the tile is the LABEL and the tooltip is the
        /// text, and the shelf reads at a glance.</summary>
        /// <summary>Draws a joker or power offer: a tinted body with a kind tag and the name.
        /// SIZED BY ITS COMPARTMENT rather than by a constant - the shelf is painted art now and
        /// a tile that ignores it hangs over the frame. An empty <paramref name="description"/>
        /// draws nothing: the description belongs to the strip under the panel, because a
        /// compartment 1.29 units tall has no room for eight wrapped lines.</summary>
        private void BuildNamedTile(Vector2 center, int index, string key, string label,
            string displayName, string description, Color bodyColor, Color tagColor,
            bool withText, Sprite icon)
        {
            Vector2 size = index >= 0 && index < offerTileSizes.Count
                ? offerTileSizes[index]
                : new Vector2(NamedTileWidth, TileHeight);
            // THE SAME CARD THE BARS DRAW (HeldItemCard), in world units: a rounded body in the
            // kind's colour, an icon well across the top, a rim in the rarity colour. Sorting:
            // body 34 (named tiles draw no flat Frame_ there), well 35, icon 36, rim 37, text 38 -
            // nothing that overlaps shares an order.
            Masked(ViewUtil.MakePlate(transform, key + "Body_" + index, center, size,
                bodyColor, 34, ViewUtil.CardSprite("card_base")));
            float pad = size.x * 0.07f;
            float wellWidth = size.x - pad * 2f;
            float wellHeight = Mathf.Min(wellWidth * 0.86f, size.y * 0.46f);
            float wellTop = size.y * 0.5f - pad;
            Vector2 wellCenter = center + new Vector2(0f, wellTop - wellHeight * 0.5f);
            Masked(ViewUtil.MakePlate(transform, key + "Well_" + index, wellCenter,
                new Vector2(wellWidth, wellHeight), NamedWellColor, 35,
                ViewUtil.CardSprite("card_base")));
            if (icon != null)
            {
                // Fitted to the well with its aspect kept, and let run a little past it - the
                // art has a wide transparent margin round its subject (see HeldItemCard).
                Vector2 art = icon.bounds.size;
                float fit = Mathf.Min(wellWidth * 1.15f / art.x, wellHeight * 1.25f / art.y);
                Masked(ViewUtil.MakeIcon(transform, key + "Icon_" + index, wellCenter, fit,
                    Color.white, 36, icon));
            }
            Masked(ViewUtil.MakePlate(transform, key + "Rim_" + index, center, size,
                tagColor, 37, ViewUtil.CardSprite("card_frame")));
            if (!withText)
            {
                return;
            }
            float wellBottom = wellTop - wellHeight;
            // Text is sized to the CARD (tuned at one unit wide), so a bigger shelf gets bigger
            // type rather than the same small print lost in a big card.
            float s = size.x;
            ViewUtil.MakeText3D(transform, key + "Tag_" + index,
                center + new Vector2(0f, wellBottom - 0.09f * s), label,
                90, 0.013f * s, tagColor, 38, TextAnchor.MiddleCenter);
            // A DEMO TILE CARRIES NO DESCRIPTION - see the note at the call site - so the name
            // sits between the tag under the well and the price at the bottom.
            float priceTop = -size.y * 0.5f + 0.24f * s;
            ViewUtil.MakeText3D(transform, key + "Name_" + index,
                center + new Vector2(0f, (wellBottom - 0.17f * s + priceTop) * 0.5f),
                ViewUtil.WrapText(displayName, DemoLayout ? DemoNameWrap : 14),
                90, 0.018f * s, JokerNameColor, 38, TextAnchor.MiddleCenter);
            if (!string.IsNullOrEmpty(description))
            {
                ViewUtil.MakeText3D(transform, key + "Desc_" + index,
                    center + new Vector2(0f, -size.y * 0.5f + 0.34f),
                    ViewUtil.WrapText(description, DescriptionWrap, 2),
                    90, 0.017f, JokerDescColor, 37, TextAnchor.UpperCenter);
            }
        }

        // ======================= THE DEMO SHELF (temporary) ==========================
        // Flat rects and text, no art, no scroll, everything on screen: BLOCKS take the left
        // column, JOKERS and POWERS stack down the right, every section carries its own reroll
        // button, and PROCEED sits bottom right. See the file header for the rollback.
        //
        // Measured in world units around DemoCentre on a 17.8 x 10 screen (ortho size 5), then
        // scaled to whatever the camera really is by the SAME FitToCamera the painted shelf uses.
        //
        // Sorting orders, outside in: border 30, backdrop 31, section boxes 33, offer frames 34
        // (BuildOffer), buttons 35, tiles and cards 36, text 37-38, hover outline 39. No two
        // things that overlap share an order - a tie between the backdrop and a section box is
        // resolved by nothing in particular and flickers.

        // THE PANEL DOES NOT OWN THE SCREEN. It is measured from the live camera every Show and
        // three strips are left OUTSIDE it, because what is under them is still live:
        //   top    - the run HUD (seed, round, debug keys) and the message line.
        //   bottom - the DRAW PILE at (6.4, -4.05) and the discard at (-6.4, -4.05). The draw
        //            pile is the SELL/INSPECT screen in the market, so covering it takes a
        //            feature away; its "SELL CARDS" plate draws at order 38-40 and came out on
        //            top of the panel anyway, which is what a panel over a live control looks
        //            like.
        //   sides  - a hair of air so the frame is not flush with the screen edge.
        // The joker and power bars are Canvas UI and draw above all of this on their own.
        private const float DemoTopReserve = 1.5f;

        /// <summary>Clears the top of the piles: they are centred at -4.05 and a card is 1.8
        /// tall, so their top edge is -3.15 - and IsDrawPileAt answers over a box 0.09 larger
        /// again, at -3.06. The panel stops at -2.85 so that the PROCEED button cannot come
        /// within a hair of stealing a click meant for the deck.</summary>
        private const float DemoBottomReserve = 2.15f;

        private const float DemoSideReserve = 0.35f;

        /// <summary>Air between the panel edge and anything in it.</summary>
        private const float DemoPad = 0.3f;

        /// <summary>The strip across the top: MARKET on the left, the balance on the right.</summary>
        private const float DemoTitleHeight = 0.72f;

        /// <summary>The strip across the bottom: the hints, and PROCEED on the right.</summary>
        private static float DemoFooterHeight
        {
            get { return UiLayout.Active.MarketFooterHeight; }
        }

        // ---- WIDTH IS DECIDED BY THE CONTENT, NOT BY THE SCREEN ----
        // The panel used to take the whole width it was given and then hand each offer a slot a
        // third of it wide, which left a 3.4-wide joker tile floating in a 5-wide slot. So the
        // slots are sized first - a tile plus its padding - the columns are the sum of their
        // own slots, and the panel is whatever those add up to, centred. It only shrinks from
        // there, if the screen cannot hold it.

        /// <summary>A block tile: a card (1.35 x 1.8 before scaling) and its price.</summary>
        private const float DemoBlockTileWidth = 1.80f;

        /// <summary>A joker/power tile is a VERTICAL CARD - an icon well over its name - and this
        /// is the widest one gets. Its real width follows the section's height (see
        /// NamedCardAspect), so a short shelf gets a narrower card rather than a squashed one.</summary>
        private const float DemoNamedTileWidth = 1.80f;

        /// <summary>Width over height of a joker/power card, the same shape the bars use
        /// (UiLayout.JokerPanel, 112 x 156).</summary>
        private const float NamedCardAspect = 0.72f;

        /// <summary>No section is narrower than its own HEADER - a label and a reroll button -
        /// however few offers it is holding.</summary>
        private const float DemoMinSectionWidth = 4.2f;

        /// <summary>The bar along the top of a section box: its name, and its reroll button.</summary>
        private const float DemoHeaderHeight = 0.50f;          // just clears the 0.46 reroll button

        /// <summary>How much of a section header answers to its NAME. Kept clear of the reroll
        /// button, which starts 2.33 in from the right of even the narrowest section.</summary>
        private const float DemoLabelHitWidth = 2.3f;

        private static readonly Vector2 DemoRerollSize = new Vector2(2.15f, 0.46f);

        private static readonly Vector2 DemoProceedSize = new Vector2(3.2f, 0.78f);

        private const float DemoTilePadX = 0.14f;

        private const float DemoTilePadY = 0.12f;

        private const float DemoColumnGap = 0.34f;

        private const float DemoRowGap = 0.16f;

        /// <summary>Tallest a tile gets, as a multiple of its own width. Card-shaped, so the
        /// block column keeps card-shaped frames however tall its box is.</summary>
        private const float DemoTileAspect = 1.55f;

        /// <summary>Name wrap for a demo tile. The tile is a vertical card about a unit wide
        /// now, which holds ten characters of the name at its size.</summary>
        private const int DemoNameWrap = 10;


        private static readonly Color DemoSectionColor = new Color(0.115f, 0.135f, 0.180f);

        private static readonly Color DemoButtonColor = new Color(0.22f, 0.30f, 0.44f);

        private static readonly Color DemoButtonDeadColor = new Color(0.17f, 0.17f, 0.20f);

        private static readonly Color DemoProceedColor = new Color(0.17f, 0.42f, 0.28f);

        /// <summary>The DECK button beside it - a secondary action, so a cooler colour.</summary>
        private static readonly Color DemoDeckColor = new Color(0.20f, 0.26f, 0.38f);

        /// <summary>Where that button is, for the click test. Empty when it is not drawn.</summary>
        private Rect demoDeckRect;

        /// <summary>What the joker and power bars are showing, so the panel knows how much of the
        /// top it has to leave alone. See DemoPanelRect.</summary>
        private int barJokerSlots;

        private int barPowerSlots;

        /// <summary>The demo shelf, start to finish. The per-offer lists are already sized by
        /// Show; this decides WHERE everything goes and then hands each offer to the same
        /// BuildOffer the painted shelf uses.</summary>
        private void BuildDemoShelf(GameSession session, IReadOnlyList<MarketOffer> offers,
            int count)
        {
            // Nothing clips in the demo, so the "is this inside the scroll window" test that
            // BuildOffer asks has to answer yes for everything.
            // Nothing clips in the side-by-side shelf, so the "is this inside the scroll
            // window" test that BuildOffer asks has to answer yes for everything. The STACKED
            // one overwrites both of these once it knows where its window is.
            windowRect = new Rect(-1000f, -1000f, 2000f, 2000f);
            maxScroll = 0f;
            // How much BAR is on screen, for the top reserve below.
            barJokerSlots = session.Jokers.Jokers.Count;
            barPowerSlots = session.Powers.Powers.Count;
            // Cleared every build: only the stacked footer draws it, and a rect left over from a
            // profile flip would keep answering clicks where there is no longer a button.
            demoDeckRect = new Rect();

            // Laid out in REAL world units at scale 1 - there is no FitToCamera here, because
            // fitting is what made the panel swallow the screen: it scales a fixed design up
            // until it fills the camera. The panel is measured FROM the camera instead, so the
            // reserved strips survive every aspect ratio.
            // The buckets are worked out FIRST: how many offers each section is holding is what
            // decides how wide the panel wants to be.
            var buckets = new List<int>[SectionCount];
            for (int s = 0; s < SectionCount; s++)
            {
                buckets[s] = new List<int>();
            }
            for (int i = 0; i < count; i++)
            {
                buckets[SectionIndex(offers[i].Kind)].Add(i);
            }
            int blockCount = buckets[SectionIndex(MarketOfferKind.Block)].Count;
            int namedCount = Mathf.Max(buckets[SectionIndex(MarketOfferKind.Joker)].Count,
                buckets[SectionIndex(MarketOfferKind.Power)].Count);
            float leftWanted = Mathf.Max(DemoMinSectionWidth,
                blockCount * (DemoBlockTileWidth + DemoTilePadX * 2f));
            float rightWanted = Mathf.Max(DemoMinSectionWidth,
                namedCount * (DemoNamedTileWidth + DemoTilePadX * 2f));

            Rect panel = DemoPanelRect(
                DemoPad * 2f + leftWanted + DemoColumnGap + rightWanted);
            transform.localScale = Vector3.one;
            Camera mainCam = Camera.main;
            transform.position = mainCam != null
                ? new Vector3(mainCam.transform.position.x, mainCam.transform.position.y, 0f)
                : Vector3.zero;

            ViewUtil.MakeRect(transform, "DemoFrame", panel.center,
                new Vector2(panel.width + PanelBorder * 2f, panel.height + PanelBorder * 2f),
                PanelFrameColor, 30);
            ViewUtil.MakeRect(transform, "DemoBackdrop", panel.center,
                new Vector2(panel.width, panel.height), BackdropColor, 31);

            BuildDemoTitle(session, panel);

            // ---- the two columns ----
            float contentTop = panel.yMax - DemoTitleHeight;
            float contentBottom = panel.yMin + DemoFooterHeight;
            float contentLeft = panel.xMin + DemoPad;
            float contentRight = panel.xMax - DemoPad;
            // k is 1 when the panel got the width it asked for, and less when the screen was
            // too narrow to give it - in which case both columns give up the same fraction.
            float contentWidth = contentRight - contentLeft - DemoColumnGap;
            float k = contentWidth / (leftWanted + rightWanted);
            float leftWidth = leftWanted * k;

            float rightLeft = contentLeft + leftWidth + DemoColumnGap;
            float rightWidth = contentRight - rightLeft;
            // Three boxes: BLOCKS down the whole left column, JOKERS over POWERS on the right.
            // A tile never stretches to fill its box (see DemoTileAspect) - the row is centred
            // in it instead, so the tall block column holds card-shaped frames rather than one
            // 4-unit frame with a 1.8-unit card floating inside it.
            float halfHeight = (contentTop - contentBottom - DemoRowGap) * 0.5f;
            Rect blocksBox;
            Rect jokersBox;
            Rect powersBox;
            if (UiLayout.Active.MarketStacked)
            {
                // ONE COLUMN, three shelves down it, and it SCROLLS.
                //
                // Each shelf is given the height its own tiles want rather than a share of what
                // is left over, so a block stays card-shaped and a joker keeps room for its two
                // lines. That means the three of them are usually taller than the screen - which
                // is the point: the window below is a viewport and the stack slides past it.
                //
                // The clipping is the machinery this file already had for the painted shelf:
                // sprites are cut by a mask over the window, and text - which a sprite mask
                // cannot touch - is simply not built when it falls outside. A scroll rebuilds
                // the shelf, so nothing has to be moved.
                float full = contentRight - contentLeft;
                float blocksHeight = UiLayout.Active.MarketBlockSection;
                float namedHeight = UiLayout.Active.MarketNamedSection;

                var window = Rect.MinMaxRect(panel.xMin, contentBottom, panel.xMax, contentTop);
                // A TALL phone has more window than the three shelves need, and leaving the
                // difference as dead space at the bottom looks like something failed to load. So
                // they grow into it - capped, because past a point a bigger tile is just a bigger
                // tile and the shelf stops reading as a list.
                float wanted = blocksHeight + namedHeight * 2f + DemoRowGap * 2f;
                if (wanted < window.height)
                {
                    float grow = Mathf.Min(1.35f,
                        (window.height - DemoRowGap * 2f) / Mathf.Max(wanted - DemoRowGap * 2f, 0.01f));
                    blocksHeight *= grow;
                    namedHeight *= grow;
                }
                windowRect = window;
                contentHeight = blocksHeight + namedHeight * 2f + DemoRowGap * 2f;
                maxScroll = Mathf.Max(0f, contentHeight - window.height);
                scroll = Mathf.Clamp(scroll, 0f, maxScroll);
                BuildWindowMask(window);

                float stackTop = contentTop + scroll;
                blocksBox = new Rect(contentLeft, stackTop - blocksHeight, full, blocksHeight);
                stackTop -= blocksHeight + DemoRowGap;
                jokersBox = new Rect(contentLeft, stackTop - namedHeight, full, namedHeight);
                stackTop -= namedHeight + DemoRowGap;
                powersBox = new Rect(contentLeft, stackTop - namedHeight, full, namedHeight);
            }
            else
            {
                blocksBox = new Rect(contentLeft, contentBottom, leftWidth,
                    contentTop - contentBottom);
                jokersBox = new Rect(rightLeft, contentTop - halfHeight, rightWidth, halfHeight);
                powersBox = new Rect(rightLeft, contentBottom, rightWidth, halfHeight);
            }

            var boxes = new Rect[SectionCount];
            boxes[SectionIndex(MarketOfferKind.Block)] = blocksBox;
            boxes[SectionIndex(MarketOfferKind.Joker)] = jokersBox;
            boxes[SectionIndex(MarketOfferKind.Power)] = powersBox;

            for (int s = 0; s < SectionCount; s++)
            {
                BuildDemoSection(s, boxes[s], buckets[s]);
            }

            // Placed in index order, because BuildOffer appends to the parallel lists.
            for (int i = 0; i < count; i++)
            {
                if (offerTileSizes[i] == Vector2.zero)
                {
                    offerVisuals.Add(null);
                    offerRarities.Add(Rarity.Common);
                    continue;
                }
                BuildOffer(session, offers, i);
            }

            BuildDemoFooter(session, panel);

            BuildHoverOutline();
        }

        /// <summary>The panel, in world units. HEIGHT comes from the camera minus the reserved
        /// strips; WIDTH is whatever the content asked for, centred, and only cut back when the
        /// screen cannot hold it. Falls back to ortho 5 / 16:9 when there is no camera to ask.</summary>
        private Rect DemoPanelRect(float wantedWidth)
        {
            UiLayout layout = UiLayout.Active;
            Camera cam = Camera.main;
            float halfHeight = cam != null && cam.orthographic ? cam.orthographicSize : 5f;
            float halfWidth = halfHeight * (cam != null ? cam.aspect : 16f / 9f);
            float room = (halfWidth - layout.MarketSideReserve) * 2f;
            // What is ABOVE the panel is measured, not guessed: the bars are the only thing the
            // stacked layout has to clear, and an empty bar takes no room. Reserving for bars
            // that are not there left a band of nothing across the top of every early market.
            float top = layout.MarketStacked
                ? Mathf.Max(0.55f, layout.HudBottomWorld(barJokerSlots, barPowerSlots) + 0.30f)
                : layout.MarketTopReserve;
            // STACKED sections take the width they are given: there is only one column, so there
            // is nothing to centre it against and every unit left over is a unit the tiles could
            // have had. Side-by-side columns keep asking for what their content needs.
            float width = layout.MarketStacked ? room : Mathf.Min(wantedWidth, room);
            return Rect.MinMaxRect(-width * 0.5f, -halfHeight + layout.MarketBottomReserve,
                width * 0.5f, halfHeight - top);
        }

        private void BuildDemoTitle(GameSession session, Rect panel)
        {
            float y = panel.yMax - DemoTitleHeight * 0.5f;
            ViewUtil.MakeText3D(transform, "DemoTitle",
                new Vector2(panel.xMin + DemoPad, y), Loc.Pick("MARKET", "MARKET"),
                60, 0.075f, PanelCreamColor, 38, TextAnchor.MiddleLeft);
            ViewUtil.MakeText3D(transform, "DemoBalance",
                new Vector2(panel.xMax - DemoPad, y),
                Loc.Pick("You have ", "Paran: ") + session.TotalScore,
                90, 0.036f, AffordablePriceColor, 38, TextAnchor.MiddleRight);
            // How much of the run's BLOCK allowance is left, under the balance and in the same
            // corner: it is the other number that decides whether a block on the shelf can be
            // taken, and it belongs where the player already looks to find that out. It goes
            // red once it is spent, which is the same red the tiles below then wear.
            ViewUtil.MakeText3D(transform, "DemoCardLimit",
                new Vector2(panel.xMax - DemoPad, y - 0.26f),
                Loc.Pick("Blocks bought ", "Alınan blok ")
                    + session.PurchasedCardCount + "/" + session.CardPurchaseLimit,
                90, 0.024f,
                session.CanBuyMoreCards ? SectionHeaderColor : TooExpensiveColor, 38,
                TextAnchor.MiddleRight);
        }

        /// <summary>One section box: its name, its own reroll button, and the slots its offers
        /// will be drawn into. The slot centres go straight into offerCenters, so OfferAt and
        /// the hover outline need to know nothing about the demo at all.</summary>
        private void BuildDemoSection(int section, Rect box, List<int> bucket)
        {
            Masked(ViewUtil.MakeRect(transform, "DemoSection_" + section, box.center,
                new Vector2(box.width, box.height), DemoSectionColor, 33));

            float headerY = box.yMax - DemoHeaderHeight * 0.5f;
            // A SPRITE MASK CANNOT TOUCH TEXT. The plate above is clipped by the window, but the
            // name and the reroll label are MeshRenderers and would keep drawing after the shelf
            // had scrolled them out - over the title, over the debug column, over anything. So
            // they are simply not built when the header has left the window.
            bool headerVisible = InsideWindow(new Vector2(box.center.x, headerY), windowRect);
            if (headerVisible)
            {
                ViewUtil.MakeText3D(transform, "DemoSectionTitle_" + section,
                    new Vector2(box.xMin + 0.22f, headerY), SectionLabel(KindOf(section)),
                    60, 0.044f, SectionHeaderColor, 38, TextAnchor.MiddleLeft);
            }
            // The name is the handle for "what IS this shelf" (see TrySectionLabelAt). A fixed
            // box rather than the text's own extent: a TextMesh does not offer one until it has
            // been laid out, and the widest label here ("JOKERLER") is well inside this.
            demoLabelRects[section] = new Rect(box.xMin + 0.10f,
                headerY - DemoHeaderHeight * 0.5f, DemoLabelHitWidth, DemoHeaderHeight);

            // ---- the section's own reroll button ----
            var buttonCentre = new Vector2(box.xMax - 0.18f - DemoRerollSize.x * 0.5f, headerY);
            demoRerollRects[section] = new Rect(buttonCentre.x - DemoRerollSize.x * 0.5f,
                buttonCentre.y - DemoRerollSize.y * 0.5f, DemoRerollSize.x, DemoRerollSize.y);
            Masked(ViewUtil.MakeRect(transform, "DemoReroll_" + section, buttonCentre,
                DemoRerollSize,
                rerollAffordable ? DemoButtonColor : DemoButtonDeadColor, 35));
            if (headerVisible)
            {
                ViewUtil.MakeText3D(transform, "DemoRerollLabel_" + section, buttonCentre,
                    Loc.Pick("REROLL  ", "YENİLE  ") + rerollCostText, 90, 0.026f,
                    rerollAffordable ? PanelCreamColor : TooExpensiveColor, 38,
                    TextAnchor.MiddleCenter);
            }

            if (bucket.Count == 0)
            {
                return;
            }
            var content = new Rect(box.xMin, box.yMin, box.width, box.height - DemoHeaderHeight);
            float slot = content.width / bucket.Count;
            // The slot was sized FOR this width (see the panel), so the cap normally lands
            // exactly on it - it only bites when a narrow screen made the columns give ground.
            bool named = KindOf(section) != MarketOfferKind.Block;
            float tileWidth = Mathf.Min(slot - DemoTilePadX * 2f,
                named ? DemoNamedTileWidth : DemoBlockTileWidth);
            float roomHeight = content.height - DemoTilePadY * 2f;
            if (named)
            {
                // A joker/power card keeps its upright shape: the shelf's height decides it, and
                // the width is whatever that height allows.
                tileWidth = Mathf.Min(tileWidth, roomHeight * NamedCardAspect);
            }
            // Height follows the WIDTH, capped by the box. A tile stretched to a tall box is a
            // frame with a card lost in the middle of it; a tile that keeps its shape and sits
            // centred reads as a shelf with air above it.
            float tileHeight = Mathf.Min(roomHeight,
                named ? tileWidth / NamedCardAspect : tileWidth * DemoTileAspect);
            var tile = new Vector2(tileWidth, tileHeight);
            for (int c = 0; c < bucket.Count; c++)
            {
                int i = bucket[c];
                offerCenters[i] = new Vector2(content.xMin + slot * (c + 0.5f), content.center.y);
                offerTileSizes[i] = tile;
            }
        }

        /// <summary>The bottom strip: what the pointer can do on the left, PROCEED on the
        /// right. The prompt names the stage it starts, because a boss stage and round N+1 are
        /// not the same thing and the button is now the only place that says which is next.</summary>
        private void BuildDemoFooter(GameSession session, Rect panel)
        {
            if (UiLayout.Active.MarketStacked)
            {
                BuildStackedFooter(session, panel);
                return;
            }
            float left = panel.xMin + DemoPad;
            ViewUtil.MakeText3D(transform, "DemoHint1",
                new Vector2(left, panel.yMin + 0.56f),
                Loc.Pick("Click a joker or power on the bars to sell it",
                    "Satmak için barlardaki jokere veya güce tıkla"),
                90, 0.022f, SectionHeaderColor, 38, TextAnchor.MiddleLeft);
            ViewUtil.MakeText3D(transform, "DemoHint2",
                new Vector2(left, panel.yMin + 0.28f),
                Loc.Pick("Click the DECK, bottom right, to inspect it and sell cards",
                    "Kartlara bakmak ve satmak için sağ alttaki DESTEYE tıkla"),
                90, 0.022f, SectionHeaderColor, 38, TextAnchor.MiddleLeft);

            // Centred in the FOOTER STRIP, not measured up from the panel edge: at 0.78 tall
            // and 0.3 of padding the button reached 0.08 into the POWERS box above it.
            var centre = new Vector2(panel.xMax - DemoPad - DemoProceedSize.x * 0.5f,
                panel.yMin + DemoFooterHeight * 0.5f);
            demoProceedRect = new Rect(centre.x - DemoProceedSize.x * 0.5f,
                centre.y - DemoProceedSize.y * 0.5f, DemoProceedSize.x, DemoProceedSize.y);
            ViewUtil.MakeRect(transform, "DemoProceed", centre, DemoProceedSize,
                DemoProceedColor, 35);
            ViewUtil.MakeText3D(transform, "DemoProceedLabel", centre + new Vector2(0f, 0.11f),
                Loc.Pick("PROCEED", "DEVAM"), 60, 0.040f, PanelCreamColor, 38,
                TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "DemoProceedSub", centre - new Vector2(0f, 0.16f),
                session.BossStageFollowsThisRound && !session.InBossStage
                    ? Loc.Pick("BOSS of round " + session.RoundNumber + "   " + ProceedControl,
                        session.RoundNumber + ". rauntun PATRONU   " + ProceedControl)
                    : Loc.Pick("round " + (session.RoundNumber + 1) + "   " + ProceedControl,
                        "raunt " + (session.RoundNumber + 1) + "   " + ProceedControl),
                90, 0.021f, SectionHeaderColor, 38, TextAnchor.MiddleCenter);
        }

        /// <summary>DEMO: a click on a section's reroll button. Fires the SAME SectionHeld seam
        /// the hold gesture used, so the controller's reroll code did not have to change.
        /// Returns whether the click was consumed.</summary>
        public bool TryRerollAt(Vector2 world)
        {
            if (!DemoLayout)
            {
                return false;
            }
            Vector2 local = ToLocal(world);
            for (int s = 0; s < SectionCount; s++)
            {
                if (demoRerollRects[s].width > 0f && demoRerollRects[s].Contains(local))
                {
                    if (SectionHeld != null)
                    {
                        SectionHeld(KindOf(s));
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>DEMO: is the pointer on a section's NAME, and if so whose? The controller
        /// answers it with a tooltip explaining the mechanic - the shelf, not the goods on it.
        /// False in the painted layout, whose labels live inside a scrolling window.</summary>
        public bool TrySectionLabelAt(Vector2 world, out MarketOfferKind kind)
        {
            kind = MarketOfferKind.Block;
            if (!DemoLayout)
            {
                return false;
            }
            Vector2 local = ToLocal(world);
            for (int s = 0; s < SectionCount; s++)
            {
                if (demoLabelRects[s].width > 0f && demoLabelRects[s].Contains(local))
                {
                    kind = KindOf(s);
                    return true;
                }
            }
            return false;
        }

        /// <summary>DEMO: a click on PROCEED. Returns whether the click was consumed.</summary>
        public bool TryProceedAt(Vector2 world)
        {
            if (!DemoLayout || demoProceedRect.width <= 0f
                || !demoProceedRect.Contains(ToLocal(world)))
            {
                return false;
            }
            if (ProceedPressed != null)
            {
                ProceedPressed();
            }
            return true;
        }

        /// <summary>DEMO: outlines whichever button the pointer is over. Returns false when it
        /// is over none, so SetHover can carry on and clear the outline.</summary>
        private bool TryHoverDemoButton(Vector2 world)
        {
            Vector2 local = ToLocal(world);
            for (int s = 0; s < SectionCount; s++)
            {
                if (demoRerollRects[s].width > 0f && demoRerollRects[s].Contains(local))
                {
                    ShowHoverOutline(demoRerollRects[s].center,
                        new Vector2(demoRerollRects[s].width, demoRerollRects[s].height) * 0.5f,
                        rerollAffordable ? HoverColor : HoverBlockedColor);
                    return true;
                }
            }
            if (demoProceedRect.width > 0f && demoProceedRect.Contains(local))
            {
                ShowHoverOutline(demoProceedRect.center,
                    new Vector2(demoProceedRect.width, demoProceedRect.height) * 0.5f,
                    HoverColor);
                return true;
            }
            return false;
        }

        // ===================== end of the demo shelf ==================================

        public void Hide()
        {
            offerVisuals.Clear();
            offerCenters.Clear();
            offerHalfWidths.Clear();
            offerHalfHeights.Clear();
            offerTileSizes.Clear();
            offerDetails.Clear();
            detailText = null;
            windowMask = null;
            for (int i = 0; i < demoRerollRects.Length; i++)
            {
                demoRerollRects[i] = new Rect();
                demoLabelRects[i] = new Rect();
            }
            demoProceedRect = new Rect();
            offerRarities.Clear();
            offerSold.Clear();
            offerAffordable.Clear();
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


        /// <summary>World point in the panel's own space. The panel is scaled and re-centred to
        /// fit the screen (FitToCamera), so every hit-test has to come through here.</summary>
        private Vector2 ToLocal(Vector2 world)
        {
            return transform.InverseTransformPoint(new Vector3(world.x, world.y, 0f));
        }

        /// <summary>Creates the four outline edges, hidden. Sorting order 39 puts them above
        /// every tile piece (36/37) and its price (38), so an outline is never half-buried by
        /// whatever it is drawn around.</summary>
        private void BuildHoverOutline()
        {
            for (int i = 0; i < hoverEdges.Length; i++)
            {
                hoverEdges[i] = ViewUtil.MakeRect(transform, "HoverEdge_" + i, Vector2.zero,
                    Vector2.one, HoverColor, 39);
                Masked(hoverEdges[i]);
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
                    new Vector2(offerHalfWidths[index], offerHalfHeights[index]),
                    offerAffordable[index] ? HoverColor : HoverBlockedColor);
                SetDetail(index < offerDetails.Count ? offerDetails[index] : string.Empty);
                return;
            }
            if (DemoLayout && TryHoverDemoButton(world))
            {
                SetDetail(string.Empty);
                return;
            }
            HideHoverOutline();
            SetDetail(string.Empty);
        }

        private void SetDetail(string text)
        {
            if (detailText != null)
            {
                detailText.text = ViewUtil.WrapText(text, (int)DetailWrap, 8);
            }
        }

        /// <summary>What an offer says about itself in the strip: the name, then the description
        /// the tile no longer has room for. Blocks have neither, so they say what they are.</summary>
        private static string OfferDetail(MarketOffer offer)
        {
            if (offer.Kind == MarketOfferKind.Joker)
            {
                return offer.Joker.DisplayName + "  -  " + offer.Joker.Description;
            }
            if (offer.Kind == MarketOfferKind.Power)
            {
                return offer.Power.DisplayName + "  -  " + offer.Power.Description;
            }
            return Loc.Pick("Block card", "Blok kartı");
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
            SetDetail(string.Empty);
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

        /// <summary>Offer index under a world point, or -1.</summary>
        public int OfferAt(Vector2 world)
        {
            Vector2 local = ToLocal(world);
            for (int i = 0; i < offerCenters.Count; i++)
            {
                if (Mathf.Abs(local.x - offerCenters[i].x) <= offerHalfWidths[i]
                    && Mathf.Abs(local.y - offerCenters[i].y)
                        <= (i < offerHalfHeights.Count ? offerHalfHeights[i] : TileHeight * 0.5f))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>World centre of an offer tile, or null. The inverse of OfferAt: the mouse
        /// asks "what is under this point", a gamepad asks "where is the offer I have stepped
        /// to" (see GameUiController.PadPlay.cs).</summary>
        public Vector2? OfferWorldCenter(int index)
        {
            if (index < 0 || index >= offerCenters.Count)
            {
                return null;
            }
            Vector3 world = transform.TransformPoint(
                new Vector3(offerCenters[index].x, offerCenters[index].y, 0f));
            return new Vector2(world.x, world.y);
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
