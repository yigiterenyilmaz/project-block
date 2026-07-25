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

        /// <summary>Row pitch when every card carries a price label under it. The plain pitch
        /// leaves 0.15 between rows, and a price label is taller than that - it used to print
        /// over the heads of the cards in the row below, which is what made the sell screen
        /// look like a jumble.</summary>
        private const float PricedSpacingY = 1.92f;

        /// <summary>Rows on screen at once. Everything past this scrolls; a 24-card deck is
        /// already 3 rows, and the deck only grows from there.</summary>
        private const int VisibleRows = 3;

        /// <summary>Centre of the top visible row.</summary>
        private const float GridTop = 2.35f;

        private const float PanelHalfWidth = 5.15f;

        /// <summary>Opaque, like the market's: the sell screen used to be a 78% black wash with
        /// the whole shelf legible underneath it.</summary>
        private static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.08f, 1f);
        private static readonly Color PanelFrameColor = new Color(0.30f, 0.34f, 0.44f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.82f);
        private static readonly Color TitleColor = new Color(1f, 0.92f, 0.45f);
        private static readonly Color HintColor = new Color(0.70f, 0.75f, 0.82f);
        private static readonly Color ScrollTrackColor = new Color(0.16f, 0.18f, 0.23f);
        private static readonly Color ScrollThumbColor = new Color(0.45f, 0.52f, 0.66f);

        public bool IsOpen { get; private set; }

        /// <summary>First row shown. Kept across a rebuild so selling a card does not throw the
        /// player back to the top of their deck; ResetScroll starts a fresh visit at the top.</summary>
        private int scrollRow;

        /// <summary>What the last Show was given, so a scroll can re-lay-out without the
        /// controller having to remember which mode the overlay is in.</summary>
        private IReadOnlyList<BlockCard> lastCards;
        private System.Func<BlockCard, int> lastSellValue;
        private int totalRows;

        private readonly List<Vector2> entryCenters = new List<Vector2>();
        private readonly List<BlockShape> entryShapes = new List<BlockShape>();
        private readonly List<BlockCard> entryCards = new List<BlockCard>();
        private readonly List<CardVisual> entryVisuals = new List<CardVisual>();

        // "Hileli Zar" opening-hand picker: a CONFIRM button, enabled only at the exact count.
        private Vector2 confirmButtonCenter;
        private Vector2 confirmButtonHalf;
        private bool confirmButtonShown;
        private bool confirmEnabled;

        /// <summary>Shows the overlay with the given cards (normally the whole owned deck).</summary>
        public void Show(IReadOnlyList<BlockCard> cards)
        {
            Show(cards, null);
        }

        /// <summary>Shows the owned deck. When sellValue is non-null the overlay is a SELL
        /// screen: each card gets its sell price and clicking one sells it.</summary>
        public void Show(IReadOnlyList<BlockCard> cards, System.Func<BlockCard, int> sellValue)
        {
            lastCards = cards;
            lastSellValue = sellValue;
            Hide();
            IsOpen = true;

            var sorted = new List<BlockCard>(cards);
            sorted.Sort(CompareCards);
            bool priced = sellValue != null;
            float pitch = priced ? PricedSpacingY : SpacingY;
            totalRows = (sorted.Count + Columns - 1) / Columns;
            int maxScroll = Mathf.Max(0, totalRows - VisibleRows);
            scrollRow = Mathf.Clamp(scrollRow, 0, maxScroll);
            int shownRows = Mathf.Min(VisibleRows, totalRows);

            float gridBottom = GridTop - (shownRows - 1) * pitch;
            float cardHalf = CardVisual.BodyHeight * CardScale * 0.5f;
            float contentBottom = gridBottom - cardHalf - (priced ? 0.42f : 0.2f);
            float titleY = GridTop + cardHalf + 0.62f;
            bool scrolls = totalRows > VisibleRows;
            float hintY = contentBottom - 0.34f;
            float panelTop = titleY + 0.55f;
            float panelBottom = (scrolls ? hintY : contentBottom) - 0.36f;

            // A full-screen dim so the market behind is clearly OUT of play, then an OPAQUE
            // panel on top of it - the old overlay was only the dim, so the whole shelf showed
            // through the card list and the two fought each other.
            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(40f, 20f), DimColor, 40);
            var panelCenter = new Vector2(0f, (panelTop + panelBottom) * 0.5f);
            var panelSize = new Vector2(PanelHalfWidth * 2f, panelTop - panelBottom);
            ViewUtil.MakeRect(transform, "PanelFrame", panelCenter,
                panelSize + new Vector2(0.22f, 0.22f), PanelFrameColor, 40);
            ViewUtil.MakeRect(transform, "Panel", panelCenter, panelSize, PanelColor, 41);

            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, titleY),
                priced
                    ? Loc.Pick("SELL CARDS  -  click a card to sell it",
                        "KART SAT  -  satmak için karta tıkla")
                    : Loc.Pick("YOUR DECK  -  " + sorted.Count + " cards",
                        "DESTEN  -  " + sorted.Count + " kart"),
                90, 0.030f, TitleColor, 44, TextAnchor.MiddleCenter);

            int firstIndex = scrollRow * Columns;
            int lastIndex = Mathf.Min(sorted.Count, (scrollRow + VisibleRows) * Columns);
            for (int i = firstIndex; i < lastIndex; i++)
            {
                int row = i / Columns;
                int column = i % Columns;
                int columnsInRow = Mathf.Min(Columns, sorted.Count - row * Columns);
                float startX = -(columnsInRow - 1) * SpacingX * 0.5f;
                var position = new Vector2(startX + column * SpacingX,
                    GridTop - (row - scrollRow) * pitch);
                CardVisual visual = CardVisual.Create(transform, "Overlay_" + sorted[i].Id,
                    sorted[i], true, false, position, 42);
                visual.transform.localScale = new Vector3(CardScale, CardScale, 1f);
                if (priced)
                {
                    int value = sellValue(sorted[i]);
                    ViewUtil.MakeText3D(transform, "SellPrice_" + i,
                        position + new Vector2(0f, -cardHalf - 0.22f),
                        value > 0
                            ? Loc.Pick("sell " + value, "satış " + value)
                            : Loc.Pick("worthless", "değersiz"), 90, 0.026f,
                        value > 0 ? TitleColor : new Color(0.72f, 0.74f, 0.78f),
                        44, TextAnchor.MiddleCenter);
                }
                entryCenters.Add(position);
                entryShapes.Add(sorted[i].Shape);
                entryCards.Add(sorted[i]);
                entryVisuals.Add(visual);
            }

            if (scrolls)
            {
                BuildScrollbar(GridTop + cardHalf, gridBottom - cardHalf, maxScroll);
                ViewUtil.MakeText3D(transform, "ScrollHint", new Vector2(0f, hintY),
                    Loc.Pick(
                        "mouse wheel to scroll    -    rows " + (scrollRow + 1) + "-"
                            + Mathf.Min(scrollRow + VisibleRows, totalRows) + " of " + totalRows,
                        "kaydırmak için tekerlek    -    satır " + (scrollRow + 1) + "-"
                            + Mathf.Min(scrollRow + VisibleRows, totalRows) + " / " + totalRows),
                    90, 0.023f, HintColor, 44, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>Scrolls the list by whole rows and re-lays it out. No-op when the overlay
        /// is not showing a scrollable list.</summary>
        public void Scroll(int deltaRows)
        {
            if (!IsOpen || lastCards == null || totalRows <= VisibleRows)
            {
                return;
            }
            int wanted = Mathf.Clamp(scrollRow + deltaRows, 0, totalRows - VisibleRows);
            if (wanted == scrollRow)
            {
                return;
            }
            scrollRow = wanted;
            Show(lastCards, lastSellValue);
        }

        /// <summary>Starts the next visit at the top. Called when the overlay is OPENED, not on
        /// every rebuild - selling a card must not scroll the list out from under the player.</summary>
        public void ResetScroll()
        {
            scrollRow = 0;
        }

        /// <summary>Track and thumb down the right edge, showing where in the deck you are.</summary>
        private void BuildScrollbar(float top, float bottom, int maxScroll)
        {
            float x = PanelHalfWidth - 0.24f;
            float height = top - bottom;
            ViewUtil.MakeRect(transform, "ScrollTrack", new Vector2(x, (top + bottom) * 0.5f),
                new Vector2(0.1f, height), ScrollTrackColor, 43);
            float thumbHeight = Mathf.Max(0.5f, height * VisibleRows / totalRows);
            float travel = height - thumbHeight;
            float t = maxScroll > 0 ? scrollRow / (float)maxScroll : 0f;
            float thumbY = top - thumbHeight * 0.5f - travel * t;
            ViewUtil.MakeRect(transform, "ScrollThumb", new Vector2(x, thumbY),
                new Vector2(0.16f, thumbHeight), ScrollThumbColor, 44);
        }

        /// <summary>Shows the owned deck as the "Hileli Zar" opening-hand PICKER: each selected
        /// card gets a highlight box, a header names the task, and a CONFIRM button (enabled only
        /// at exactly <paramref name="target"/> picks) commits. Rebuilt on every toggle - cheap
        /// at deck size. The controller toggles <paramref name="selectedIds"/> and re-calls this.</summary>
        public void ShowPicker(IReadOnlyList<BlockCard> cards, ICollection<int> selectedIds,
            int target, string header)
        {
            Hide();
            IsOpen = true;
            // The picker lays itself out and is not scrollable (it has its own CONFIRM button
            // below the grid), so make sure a stale list cannot make Scroll act on it.
            lastCards = null;
            totalRows = 0;

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.78f), 40);
            ViewUtil.MakeText3D(transform, "PickTitle", new Vector2(0f, 4.4f), header, 90, 0.026f,
                new Color(0.55f, 0.92f, 0.95f), 42, TextAnchor.MiddleCenter);

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
                if (selectedIds.Contains(sorted[i].Id))
                {
                    // A bright box just larger than the card, drawn over the dim (40) and under
                    // the card (41), so it reads as a glowing border around the selected card.
                    ViewUtil.MakeRect(transform, "PickHi_" + i, position,
                        new Vector2(CardVisual.BodyWidth * CardScale + 0.24f,
                            CardVisual.BodyHeight * CardScale + 0.24f),
                        new Color(0.30f, 0.85f, 0.98f), 40);
                }
                CardVisual visual = CardVisual.Create(transform, "Overlay_" + sorted[i].Id,
                    sorted[i], true, false, position, 41);
                visual.transform.localScale = new Vector3(CardScale, CardScale, 1f);
                entryCenters.Add(position);
                entryShapes.Add(sorted[i].Shape);
                entryCards.Add(sorted[i]);
                entryVisuals.Add(visual);
            }

            confirmEnabled = selectedIds.Count == target;
            float bottomY = startY - (rows - 1) * SpacingY;
            confirmButtonCenter = new Vector2(0f, bottomY - SpacingY * 0.65f - 0.5f);
            confirmButtonHalf = new Vector2(1.7f, 0.42f);
            confirmButtonShown = true;
            ViewUtil.MakeRect(transform, "PickConfirm", confirmButtonCenter, confirmButtonHalf * 2f,
                confirmEnabled ? new Color(0.18f, 0.42f, 0.24f) : new Color(0.16f, 0.16f, 0.18f), 42);
            ViewUtil.MakeText3D(transform, "PickConfirmLabel", confirmButtonCenter,
                Loc.Pick("CONFIRM  ", "ONAYLA  ") + selectedIds.Count + "/" + target,
                90, 0.02f, confirmEnabled ? new Color(0.8f, 1f, 0.85f) : new Color(0.6f, 0.6f, 0.62f),
                43, TextAnchor.MiddleCenter);
        }

        /// <summary>True if the world point is on the picker's CONFIRM button AND it is enabled
        /// (exactly the target number of cards is selected).</summary>
        public bool PickerConfirmAt(Vector2 world)
        {
            return confirmButtonShown && confirmEnabled
                && Mathf.Abs(world.x - confirmButtonCenter.x) <= confirmButtonHalf.x
                && Mathf.Abs(world.y - confirmButtonCenter.y) <= confirmButtonHalf.y;
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
            confirmButtonShown = false;
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
