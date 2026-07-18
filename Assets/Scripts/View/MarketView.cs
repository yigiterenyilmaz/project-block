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
        private static readonly Color JokerBodyColor = new Color(0.30f, 0.22f, 0.40f);
        private static readonly Color JokerTagColor = new Color(0.82f, 0.68f, 1f);
        private static readonly Color JokerNameColor = new Color(1f, 0.93f, 0.72f);
        private static readonly Color JokerDescColor = new Color(0.82f, 0.86f, 0.92f);

        private readonly List<CardVisual> offerVisuals = new List<CardVisual>();
        private readonly List<Vector2> offerCenters = new List<Vector2>();

        /// <summary>(Re)builds the market display from the session's current offers.</summary>
        public void Show(GameSession session)
        {
            Hide();
            IReadOnlyList<MarketOffer> offers = session.Market.Offers;
            int count = offers.Count;
            ViewUtil.MakeRect(transform, "Backdrop", Center,
                new Vector2(Mathf.Max(count, 1) * OfferSpacing + 1.2f, 3.9f), BackdropColor, 33);
            ViewUtil.MakeText3D(transform, "Title", Center + new Vector2(0f, 1.65f), "MARKET",
                60, 0.07f, Color.white, 38, TextAnchor.MiddleCenter);
            float startX = Center.x - (count - 1) * OfferSpacing * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var slotCenter = new Vector2(startX + i * OfferSpacing, Center.y + 0.1f);
                offerCenters.Add(slotCenter);
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
                34, 0.04f, JokerTagColor, 37, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "JokerName_" + index,
                center + new Vector2(0f, 0.5f), Wrap(joker.DisplayName, 13),
                40, 0.05f, JokerNameColor, 37, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "JokerDesc_" + index,
                center + new Vector2(0f, 0.12f), Wrap(joker.Description, 16),
                30, 0.037f, JokerDescColor, 37, TextAnchor.UpperCenter);
        }

        /// <summary>Greedy word wrap for the placeholder TextMesh (no auto-wrapping).</summary>
        private static string Wrap(string text, int maxCharsPerLine)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            string[] words = text.Split(' ');
            var sb = new System.Text.StringBuilder();
            int lineLength = 0;
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (lineLength > 0 && lineLength + 1 + word.Length > maxCharsPerLine)
                {
                    sb.Append('\n');
                    lineLength = 0;
                }
                else if (lineLength > 0)
                {
                    sb.Append(' ');
                    lineLength++;
                }
                sb.Append(word);
                lineLength += word.Length;
            }
            return sb.ToString();
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
    }
}
