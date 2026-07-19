// PURPOSE: Full-screen overlay that lists the player's WHOLE owned deck ("oyun
// destesi"), opened by clicking the draw pile. Cards are shown SORTED (by size, then
// id), never in draw order - the draw pile is face-down and its order must not leak.
// Future reveal jokers (Insider, Büyüteç) will get their own explicit reveal UI.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Modal card-list overlay. While open, the controller blocks other input.</summary>
    public sealed class DeckOverlayView : MonoBehaviour
    {
        private const int Columns = 8;
        private const float CardScale = 0.72f;
        private const float SpacingX = 1.15f;
        private const float SpacingY = 1.45f;

        public bool IsOpen { get; private set; }

        private readonly List<Vector2> entryCenters = new List<Vector2>();
        private readonly List<BlockShape> entryShapes = new List<BlockShape>();
        private readonly List<BlockCard> entryCards = new List<BlockCard>();
        private readonly List<CardVisual> entryVisuals = new List<CardVisual>();

        /// <summary>Shows the overlay with the given cards (normally the whole owned deck).</summary>
        public void Show(IReadOnlyList<BlockCard> cards)
        {
            Show(cards, null);
        }

        /// <summary>Shows the owned deck. When sellValue is non-null the overlay is a SELL
        /// screen: each card gets its sell price and clicking one sells it.</summary>
        public void Show(IReadOnlyList<BlockCard> cards, System.Func<BlockCard, int> sellValue)
        {
            Hide();
            IsOpen = true;

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.78f), 40);
            if (sellValue != null)
            {
                ViewUtil.MakeText3D(transform, "SellTitle", new Vector2(0f, 4.4f),
                    Loc.Pick("SELL CARDS  -  click a card to sell it",
                        "KART SAT  -  satmak için karta tıkla"), 90, 0.03f,
                    new Color(1f, 0.92f, 0.45f), 42, TextAnchor.MiddleCenter);
            }

            var sorted = new List<BlockCard>(cards);
            sorted.Sort(CompareCards);
            int rows = (sorted.Count + Columns - 1) / Columns;
            float startY = (rows - 1) * SpacingY * 0.5f + 0.3f;
            for (int i = 0; i < sorted.Count; i++)
            {
                int row = i / Columns;
                int column = i % Columns;
                int columnsInRow = Mathf.Min(Columns, sorted.Count - row * Columns);
                float startX = -(columnsInRow - 1) * SpacingX * 0.5f;
                var position = new Vector2(startX + column * SpacingX, startY - row * SpacingY);
                CardVisual visual = CardVisual.Create(transform, "Overlay_" + sorted[i].Id,
                    sorted[i], true, false, position, 41);
                visual.transform.localScale = new Vector3(CardScale, CardScale, 1f);
                if (sellValue != null)
                {
                    int value = sellValue(sorted[i]);
                    ViewUtil.MakeText3D(transform, "SellPrice_" + i,
                        position + new Vector2(0f, -CardVisual.BodyHeight * CardScale * 0.5f - 0.16f),
                        value > 0
                            ? Loc.Pick("sell " + value, "satış " + value)
                            : Loc.Pick("worthless", "değersiz"), 90, 0.017f,
                        value > 0 ? new Color(1f, 0.92f, 0.45f) : new Color(0.6f, 0.6f, 0.6f),
                        42, TextAnchor.MiddleCenter);
                }
                entryCenters.Add(position);
                entryShapes.Add(sorted[i].Shape);
                entryCards.Add(sorted[i]);
                entryVisuals.Add(visual);
            }
        }

        /// <summary>Sold-card feedback: detaches that card's visual and flies it off toward
        /// the discard pile. Call BEFORE Show() rebuilds the overlay.</summary>
        public void PlaySellFx(BlockCard card)
        {
            for (int i = 0; i < entryCards.Count; i++)
            {
                if (entryCards[i] != card || entryVisuals[i] == null)
                {
                    continue;
                }
                CardVisual visual = entryVisuals[i];
                entryVisuals[i] = null;
                visual.transform.SetParent(transform.parent, true);
                visual.SetSortingBoost(3);
                visual.FlyToAndDestroy(CardLayerView.DiscardPilePos, 0.32f);
                return;
            }
        }

        /// <summary>The displayed shape under a world point (fox shape picker), or null.</summary>
        public BlockShape ShapeAt(Vector2 world)
        {
            int index = EntryAt(world);
            return index >= 0 ? entryShapes[index] : null;
        }

        /// <summary>The card under a world point (hover tooltip), or null.</summary>
        public BlockCard CardAt(Vector2 world)
        {
            int index = EntryAt(world);
            return index >= 0 ? entryCards[index] : null;
        }

        private int EntryAt(Vector2 world)
        {
            for (int i = 0; i < entryCenters.Count; i++)
            {
                if (Mathf.Abs(world.x - entryCenters[i].x) <= CardVisual.BodyWidth * CardScale * 0.5f
                    && Mathf.Abs(world.y - entryCenters[i].y) <= CardVisual.BodyHeight * CardScale * 0.5f)
                {
                    return i;
                }
            }
            return -1;
        }

        public void Hide()
        {
            IsOpen = false;
            entryCenters.Clear();
            entryShapes.Clear();
            entryCards.Clear();
            entryVisuals.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private static int CompareCards(BlockCard a, BlockCard b)
        {
            return a.Shape.Size != b.Shape.Size ? a.Shape.Size - b.Shape.Size : a.Id - b.Id;
        }
    }
}
