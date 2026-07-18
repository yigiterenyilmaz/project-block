// PURPOSE: The between-rounds market screen: block-card and joker offers with prices,
// click to buy. Rebuilt from scratch on every change (cheap at this scale). Purchases go
// through GameSession.TryBuyOffer - this view never touches money, the deck or jokers.
// Sorting orders: backdrop 33, frames 34, offer cards/joker tiles 36/37, price labels 38
// (under the deck overlay at 40+).

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Renders and hit-tests the market offers.</summary>
    public sealed class MarketView : MonoBehaviour
    {
        private const float OfferSpacing = 2.2f;
        private static readonly Vector2 Center = new Vector2(0f, 1.0f);

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

        private readonly List<CardVisual> offerVisuals = new List<CardVisual>();
        private readonly List<Vector2> offerCenters = new List<Vector2>();

        /// <summary>(Re)builds the market display: a BLOCKS section and a JOKERS section,
        /// each centered under its own header, separated by a gap.</summary>
        public void Show(GameSession session)
        {
            Hide();
            IReadOnlyList<MarketOffer> offers = session.Market.Offers;
            int count = offers.Count;

            int blockCount = 0;
            int jokerCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (offers[i].Kind == MarketOfferKind.Joker) jokerCount++; else blockCount++;
            }

            // Blocks fill the first slots, then a one-slot gap, then jokers (offers are
            // already ordered blocks-then-jokers). Center the whole run under the title.
            float sectionGap = (blockCount > 0 && jokerCount > 0) ? OfferSpacing : 0f;
            float span = (count - 1) * OfferSpacing + sectionGap;
            float startX = Center.x - span * 0.5f;
            float tileY = Center.y - 0.05f;

            ViewUtil.MakeRect(transform, "Backdrop", Center,
                new Vector2(Mathf.Max(span + CardVisual.BodyWidth + 1.2f, 3f), 4.4f), BackdropColor, 33);
            ViewUtil.MakeText3D(transform, "Title", Center + new Vector2(0f, 2.0f), "MARKET",
                60, 0.07f, Color.white, 38, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "SellHint", Center + new Vector2(0f, 1.62f),
                "click a joker to sell it  -  click the deck pile to sell cards", 90, 0.013f,
                SectionHeaderColor, 38, TextAnchor.MiddleCenter);

            float cursor = startX;
            bool gapInserted = false;
            float blockMinX = float.MaxValue, blockMaxX = float.MinValue;
            float jokerMinX = float.MaxValue, jokerMaxX = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                bool isJoker = offers[i].Kind == MarketOfferKind.Joker;
                if (isJoker && sectionGap > 0f && !gapInserted)
                {
                    cursor += sectionGap;
                    gapInserted = true;
                }
                var slotCenter = new Vector2(cursor, tileY);
                offerCenters.Add(slotCenter);
                cursor += OfferSpacing;
                if (isJoker)
                {
                    jokerMinX = Mathf.Min(jokerMinX, slotCenter.x);
                    jokerMaxX = Mathf.Max(jokerMaxX, slotCenter.x);
                }
                else
                {
                    blockMinX = Mathf.Min(blockMinX, slotCenter.x);
                    blockMaxX = Mathf.Max(blockMaxX, slotCenter.x);
                }
            }

            float headerY = tileY + 1.28f;
            if (blockCount > 0)
            {
                ViewUtil.MakeText3D(transform, "BlocksHeader",
                    new Vector2((blockMinX + blockMaxX) * 0.5f, headerY), "BLOCKS",
                    90, 0.022f, SectionHeaderColor, 38, TextAnchor.MiddleCenter);
            }
            if (jokerCount > 0)
            {
                ViewUtil.MakeText3D(transform, "JokersHeader",
                    new Vector2((jokerMinX + jokerMaxX) * 0.5f, headerY), "JOKERS",
                    90, 0.022f, SectionHeaderColor, 38, TextAnchor.MiddleCenter);
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
                    // Joker tiles have no CardVisual; a null keeps offerVisuals index-aligned
                    // with the offers so PlayBuyFx and OfferAt stay correct.
                    offerVisuals.Add(null);
                    BuildJokerTile(slotCenter, i, offer.Joker);
                }
                else
                {
                    offerVisuals.Add(CardVisual.Create(transform, "Offer_" + i, offer.Card,
                        true, false, slotCenter, 36));
                }
                bool affordable = session.TotalScore >= offer.Price;
                ViewUtil.MakeText3D(transform, "Price_" + i,
                    slotCenter + new Vector2(0f, -1.35f), offer.Price.ToString(),
                    60, 0.07f, affordable ? AffordablePriceColor : TooExpensiveColor,
                    38, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>Draws a joker offer: a tinted body with its name and wrapped description
        /// (there is no BlockCard to render, so this stands in for the offer card).</summary>
        private void BuildJokerTile(Vector2 center, int index, JokerDefinition joker)
        {
            ViewUtil.MakeRect(transform, "JokerBody_" + index, center,
                new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight), JokerBodyColor, 36);
            ViewUtil.MakeText3D(transform, "JokerTag_" + index,
                center + new Vector2(0f, CardVisual.BodyHeight * 0.5f - 0.17f), "JOKER",
                90, 0.015f, JokerTagColor, 37, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "JokerName_" + index,
                center + new Vector2(0f, 0.5f), ViewUtil.WrapText(joker.DisplayName, 13),
                90, 0.022f, JokerNameColor, 37, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "JokerDesc_" + index,
                center + new Vector2(0f, 0.12f), ViewUtil.WrapText(joker.Description, 16),
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
            if (offerIndex < 0 || offerIndex >= offerCenters.Count)
            {
                return;
            }
            var root = new GameObject("JokerBuyFx");
            root.transform.SetParent(transform.parent, false);
            root.transform.localPosition = offerCenters[offerIndex];
            ViewUtil.MakeRect(root.transform, "Body", Vector2.zero,
                new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight), JokerBodyColor, 39);
            ViewUtil.MakeText3D(root.transform, "Tag", Vector2.zero, "JOKER",
                90, 0.016f, JokerTagColor, 39, TextAnchor.MiddleCenter);
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
