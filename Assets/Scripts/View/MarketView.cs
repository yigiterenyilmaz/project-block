// PURPOSE: The between-rounds market screen: block-card, joker and power offers with
// prices, click to buy. Rebuilt from scratch on every change (cheap at this scale).
// Purchases go through GameSession.TryBuyOffer - this view never touches money, the deck
// or the inventories.
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
        private const float OfferSpacing = 2.2f;

        /// <summary>Vertical distance between two section rows.</summary>
        private const float RowPitch = 2.8f;

        /// <summary>Section header above a row's tiles / price label below them.</summary>
        private const float HeaderOffset = 1.15f;
        private const float PriceOffset = 1.2f;

        private static readonly Vector2 Center = new Vector2(0f, -0.2f);

        private static readonly Color BackdropColor = new Color(0.05f, 0.06f, 0.08f, 0.93f);
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

        private readonly List<CardVisual> offerVisuals = new List<CardVisual>();
        private readonly List<Vector2> offerCenters = new List<Vector2>();

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
            }

            float maxSpan = 0f;
            for (int r = 0; r < rowOffers.Count; r++)
            {
                maxSpan = Mathf.Max(maxSpan, (rowOffers[r].Count - 1) * OfferSpacing);
            }
            float topRowY = Center.y + (rowOffers.Count - 1) * RowPitch * 0.5f;

            ViewUtil.MakeRect(transform, "Backdrop", Center,
                new Vector2(Mathf.Max(maxSpan + CardVisual.BodyWidth + 1.4f, 6f),
                    rowOffers.Count * RowPitch + 2.2f), BackdropColor, 33);
            float titleY = topRowY + HeaderOffset + 0.95f;
            ViewUtil.MakeText3D(transform, "Title", new Vector2(Center.x, titleY), "MARKET",
                60, 0.07f, Color.white, 38, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "SellHint", new Vector2(Center.x, titleY - 0.42f),
                "click a joker or a power to sell it  -  click the deck pile to sell cards", 90, 0.013f,
                SectionHeaderColor, 38, TextAnchor.MiddleCenter);

            for (int r = 0; r < rowOffers.Count; r++)
            {
                float rowY = topRowY - r * RowPitch;
                List<int> row = rowOffers[r];
                ViewUtil.MakeText3D(transform, SectionLabel(rowKinds[r]) + "Header",
                    new Vector2(Center.x, rowY + HeaderOffset), SectionLabel(rowKinds[r]),
                    90, 0.022f, SectionHeaderColor, 38, TextAnchor.MiddleCenter);
                float startX = Center.x - (row.Count - 1) * OfferSpacing * 0.5f;
                for (int c = 0; c < row.Count; c++)
                {
                    offerCenters[row[c]] = new Vector2(startX + c * OfferSpacing, rowY);
                }
            }

            for (int i = 0; i < count; i++)
            {
                Vector2 slotCenter = offerCenters[i];
                ViewUtil.MakeRect(transform, "Frame_" + i, slotCenter,
                    new Vector2(CardVisual.BodyWidth + 0.18f, CardVisual.BodyHeight + 0.18f),
                    FrameColor, 34);
                MarketOffer offer = offers[i];
                if (offer.Sold)
                {
                    offerVisuals.Add(null);
                    ViewUtil.MakeText3D(transform, "Sold_" + i, slotCenter, "SOLD",
                        60, 0.07f, SoldColor, 38, TextAnchor.MiddleCenter);
                    continue;
                }
                if (offer.Kind == MarketOfferKind.Joker)
                {
                    // Joker/power tiles have no CardVisual; a null keeps offerVisuals
                    // index-aligned with the offers so PlayBuyFx and OfferAt stay correct.
                    offerVisuals.Add(null);
                    BuildNamedTile(slotCenter, i, "JOKER", offer.Joker.DisplayName,
                        offer.Joker.Description, JokerBodyColor, JokerTagColor);
                }
                else if (offer.Kind == MarketOfferKind.Power)
                {
                    offerVisuals.Add(null);
                    BuildNamedTile(slotCenter, i, "POWER", offer.Power.DisplayName,
                        offer.Power.Description, PowerBodyColor, PowerTagColor);
                }
                else
                {
                    offerVisuals.Add(CardVisual.Create(transform, "Offer_" + i, offer.Card,
                        true, false, slotCenter, 36));
                }
                bool affordable = session.TotalScore >= offer.Price;
                ViewUtil.MakeText3D(transform, "Price_" + i,
                    slotCenter + new Vector2(0f, -PriceOffset), offer.Price.ToString(),
                    60, 0.07f, affordable ? AffordablePriceColor : TooExpensiveColor,
                    38, TextAnchor.MiddleCenter);
            }
        }

        private static string SectionLabel(MarketOfferKind kind)
        {
            switch (kind)
            {
                case MarketOfferKind.Joker: return "JOKERS";
                case MarketOfferKind.Power: return "POWERS";
                default: return "BLOCKS";
            }
        }

        /// <summary>Draws a joker or power offer: a tinted body with a kind tag, the name
        /// and a wrapped description (there is no BlockCard to render for these).</summary>
        private void BuildNamedTile(Vector2 center, int index, string tag, string displayName,
            string description, Color bodyColor, Color tagColor)
        {
            ViewUtil.MakeRect(transform, tag + "Body_" + index, center,
                new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight), bodyColor, 36);
            ViewUtil.MakeText3D(transform, tag + "Tag_" + index,
                center + new Vector2(0f, CardVisual.BodyHeight * 0.5f - 0.17f), tag,
                90, 0.015f, tagColor, 37, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, tag + "Name_" + index,
                center + new Vector2(0f, 0.5f), ViewUtil.WrapText(displayName, 13),
                90, 0.022f, JokerNameColor, 37, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, tag + "Desc_" + index,
                center + new Vector2(0f, 0.12f), ViewUtil.WrapText(description, 16),
                90, 0.012f, JokerDescColor, 37, TextAnchor.UpperCenter);
        }

        public void Hide()
        {
            offerVisuals.Clear();
            offerCenters.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        /// <summary>Offer index under a world point, or -1.</summary>
        public int OfferAt(Vector2 world)
        {
            for (int i = 0; i < offerCenters.Count; i++)
            {
                if (Mathf.Abs(world.x - offerCenters[i].x) <= CardVisual.BodyWidth * 0.5f
                    && Mathf.Abs(world.y - offerCenters[i].y) <= CardVisual.BodyHeight * 0.5f)
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
            PlayTileBuyFx(offerIndex, target, "JOKER", JokerBodyColor, JokerTagColor);
        }

        /// <summary>The power twin of PlayJokerBuyFx (target: the power bar).</summary>
        public void PlayPowerBuyFx(int offerIndex, Vector2 target)
        {
            PlayTileBuyFx(offerIndex, target, "POWER", PowerBodyColor, PowerTagColor);
        }

        private void PlayTileBuyFx(int offerIndex, Vector2 target, string tag,
            Color bodyColor, Color tagColor)
        {
            if (offerIndex < 0 || offerIndex >= offerCenters.Count)
            {
                return;
            }
            var root = new GameObject(tag + "BuyFx");
            root.transform.SetParent(transform.parent, false);
            root.transform.localPosition = offerCenters[offerIndex];
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
