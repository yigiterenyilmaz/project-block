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
                offerAffordable.Add(session.TotalScore >= offers[i].Price);
                offerDetails.Add(OfferDetail(offers[i]));
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

            Masked(ViewUtil.MakeRect(transform, "Frame_" + i, slotCenter, tileSize,
                RarityPalette.Frame(FrameColor, rarity), 34));
            bool text = InsideWindow(slotCenter, windowRect);

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
                    offer.Joker.DisplayName, offer.Joker.Description,
                    RarityPalette.Tint(JokerBodyColor, rarity),
                    rarity == Rarity.Common ? JokerTagColor : RarityPalette.Accent(rarity), text);
            }
            else if (offer.Kind == MarketOfferKind.Power)
            {
                offerVisuals.Add(null);
                BuildNamedTile(slotCenter, i, "Power", TierTag(Loc.Pick("POWER", "GÜÇ"), rarity),
                    offer.Power.DisplayName, offer.Power.Description,
                    RarityPalette.Tint(PowerBodyColor, rarity),
                    rarity == Rarity.Common ? PowerTagColor : RarityPalette.Accent(rarity), text);
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
                bool affordable = session.TotalScore >= offer.Price;
                ViewUtil.MakeText3D(transform, "Price_" + i,
                    slotCenter + new Vector2(0f, -tileSize.y * 0.5f + 0.16f),
                    offer.Price.ToString(), 60, 0.060f,
                    affordable ? AffordablePriceColor : TooExpensiveColor, 38,
                    TextAnchor.MiddleCenter);
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
            ViewUtil.MakeText3D(transform, "ScrollHint", new Vector2(sideX, FrameCenter.y + 0.7f),
                maxScroll > 0.01f
                    ? Loc.Pick("Scroll for more", "Devamı için kaydır")
                    : string.Empty,
                90, 0.024f, SectionHeaderColor, 38, TextAnchor.MiddleRight);
            ViewUtil.MakeText3D(transform, "Prompt", new Vector2(sideX, FrameCenter.y - 1.6f),
                Loc.Pick("[N] start ", "[N] başlat ")
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

        /// <summary>Puts a renderer under the window mask. Sprites clip against the shell's
        /// interior; without this a half-scrolled section draws over the frame.</summary>
        private static void Masked(SpriteRenderer renderer)
        {
            if (renderer != null)
            {
                renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
        }

        /// <summary>Puts a whole subtree under the window mask.</summary>
        private static void MaskAll(Transform root)
        {
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

        /// <summary>Draws a joker or power offer: a tinted body with a kind tag and the name.
        /// SIZED BY ITS COMPARTMENT rather than by a constant - the shelf is painted art now and
        /// a tile that ignores it hangs over the frame. An empty <paramref name="description"/>
        /// draws nothing: the description belongs to the strip under the panel, because a
        /// compartment 1.29 units tall has no room for eight wrapped lines.</summary>
        private void BuildNamedTile(Vector2 center, int index, string key, string label,
            string displayName, string description, Color bodyColor, Color tagColor,
            bool withText)
        {
            Vector2 size = index >= 0 && index < offerTileSizes.Count
                ? offerTileSizes[index]
                : new Vector2(NamedTileWidth, TileHeight);
            Masked(ViewUtil.MakeRect(transform, key + "Body_" + index, center, size,
                bodyColor, 36));
            if (!withText)
            {
                return;
            }
            ViewUtil.MakeText3D(transform, key + "Tag_" + index,
                center + new Vector2(0f, size.y * 0.5f - 0.16f), label,
                90, 0.019f, tagColor, 37, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, key + "Name_" + index,
                center + new Vector2(0f, 0.02f), ViewUtil.WrapText(displayName, 14),
                90, 0.027f, JokerNameColor, 37, TextAnchor.MiddleCenter);
            if (!string.IsNullOrEmpty(description))
            {
                ViewUtil.MakeText3D(transform, key + "Desc_" + index,
                    center + new Vector2(0f, -size.y * 0.5f + 0.34f),
                    ViewUtil.WrapText(description, DescriptionWrap, 2),
                    90, 0.017f, JokerDescColor, 37, TextAnchor.UpperCenter);
            }
        }

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
