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

        // Sorting tiers, all distinct: the dim and the frame used to share order 40, and which
        // of the two won was down to creation order - so on some rebuilds the dim covered the
        // frame and the panel's border simply vanished.
        private const int DimOrder = 39;
        private const int FrameOrder = 40;
        private const int PanelOrder = 41;
        private const int CardOrder = 42;
        private const int ScrollTrackOrder = 43;
        private const int ScrollThumbOrder = 44;
        private const int PriceOrder = 45;
        private const int OccluderOrder = 46;
        private const int ChromeOrder = 47;

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

        /// <summary>How far the list is scrolled, IN ROWS and fractional - half a row is a real
        /// position, which is what makes the wheel feel continuous instead of teleporting a row
        /// at a time. Kept across a rebuild so selling a card does not throw the player back to
        /// the top of their deck; ResetScroll starts a fresh visit at the top.</summary>
        private float scrollRows;

        /// <summary>The scrollbar track, for hit-testing a click or a drag on it.</summary>
        private Vector2 scrollTrackCenter;
        private Vector2 scrollTrackHalf;
        private bool scrollbarShown;

        /// <summary>Panel bounds, so a click INSIDE the overlay does not close it - only a click
        /// on the dim outside does.</summary>
        private Vector2 panelBoundsCenter;
        private Vector2 panelBoundsHalf;

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
            float maxScroll = Mathf.Max(0, totalRows - VisibleRows);
            scrollRows = Mathf.Clamp(scrollRows, 0f, maxScroll);
            int shownRows = Mathf.Min(VisibleRows, totalRows);

            float gridBottom = GridTop - (shownRows - 1) * pitch;
            float cardHalf = CardVisual.BodyHeight * CardScale * 0.5f;
            // How far a row's content reaches below its centre - the price label, when there
            // is one, hangs further than the card does.
            float reachDown = priced ? cardHalf + 0.42f : cardHalf;
            bool scrolls = totalRows > VisibleRows;

            // THE WHOLE LAYOUT IS DERIVED FROM HERE DOWN, and the order matters. A scrolling row
            // must be able to leave the list without ever being seen to blink out, so:
            //   cut    - where the occluder plate starts, just clear of the resting rows
            //   cull   - where a row stops being drawn: by then it is COMPLETELY under the plate
            //   panel  - far enough out that a row at its cull point is still inside the panel
            // Getting that sequence wrong is exactly how a card vanishes in open space.
            float topCut = GridTop + cardHalf + 0.1f;
            float bottomCut = gridBottom - reachDown - 0.1f;
            float cullTop = topCut + cardHalf;
            float cullBottom = bottomCut - reachDown;
            float panelTop = cullTop + cardHalf + 0.06f;
            float panelBottom = cullBottom - reachDown - 0.06f;
            // Title and hint live in the padding bands, drawn OVER the occluders.
            float titleY = (topCut + panelTop) * 0.5f;
            float hintY = (bottomCut + panelBottom) * 0.5f;

            // A full-screen dim so the market behind is clearly OUT of play, then an OPAQUE
            // panel on top of it - the old overlay was only the dim, so the whole shelf showed
            // through the card list and the two fought each other.
            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(40f, 20f),
                DimColor, DimOrder);
            var panelCenter = new Vector2(0f, (panelTop + panelBottom) * 0.5f);
            var panelSize = new Vector2(PanelHalfWidth * 2f, panelTop - panelBottom);
            ViewUtil.MakeRect(transform, "PanelFrame", panelCenter,
                panelSize + new Vector2(0.22f, 0.22f), PanelFrameColor, FrameOrder);
            ViewUtil.MakeRect(transform, "Panel", panelCenter, panelSize, PanelColor, PanelOrder);
            panelBoundsCenter = panelCenter;
            panelBoundsHalf = panelSize * 0.5f + new Vector2(0.11f, 0.11f);

            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, titleY),
                priced
                    ? Loc.Pick("SELL CARDS  -  click a card to sell it",
                        "KART SAT  -  satmak için karta tıkla")
                    : Loc.Pick("YOUR DECK  -  " + sorted.Count + " cards",
                        "DESTEN  -  " + sorted.Count + " kart"),
                90, 0.030f, TitleColor, ChromeOrder, TextAnchor.MiddleCenter);

            // A fractional offset means the row above and the row below can BOTH be partly on
            // screen, so the window reaches one row past the visible band on each side and rows
            // are then culled by their CENTRE. The overspill lands in the panel's padding, which
            // matters because TextMesh price labels cannot be clipped by a sprite mask.
            int firstIndex = Mathf.Max(0, (Mathf.FloorToInt(scrollRows) - 1) * Columns);
            int lastIndex = Mathf.Min(sorted.Count,
                (Mathf.CeilToInt(scrollRows) + VisibleRows + 1) * Columns);
            for (int i = firstIndex; i < lastIndex; i++)
            {
                int row = i / Columns;
                int column = i % Columns;
                int columnsInRow = Mathf.Min(Columns, sorted.Count - row * Columns);
                float startX = -(columnsInRow - 1) * SpacingX * 0.5f;
                var position = new Vector2(startX + column * SpacingX,
                    GridTop - (row - scrollRows) * pitch);
                if (position.y > cullTop || position.y < cullBottom)
                {
                    continue;
                }
                CardVisual visual = CardVisual.Create(transform, "Overlay_" + sorted[i].Id,
                    sorted[i], true, false, position, CardOrder);
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
                        PriceOrder, TextAnchor.MiddleCenter);
                }
                // Only a card whose CENTRE is still in the band takes clicks. One that has
                // scrolled up under the plate is half-hidden, and selling a card you cannot see
                // because you clicked where it used to be would be indefensible.
                if (position.y > topCut || position.y < bottomCut)
                {
                    continue;
                }
                entryCenters.Add(position);
                entryShapes.Add(sorted[i].Shape);
                entryCards.Add(sorted[i]);
                entryVisuals.Add(visual);
            }

            // The plates that make scrolling clean: panel-coloured, drawn ABOVE the cards and
            // below the title/hint, so a row sliding out slides UNDER them instead of blinking
            // out. This is the stand-in for a sprite mask, which cannot be used here because
            // TextMesh price labels ignore masks entirely.
            BuildOccluder("TopPlate", topCut, panelTop);
            BuildOccluder("BottomPlate", panelBottom, bottomCut);

            scrollbarShown = scrolls;
            if (scrolls)
            {
                BuildScrollbar(GridTop + cardHalf, gridBottom - cardHalf, maxScroll);
            }
            FitToCamera(panelCenter, panelSize);
            ViewUtil.MakeText3D(transform, "CloseHint", new Vector2(0f, hintY),
                scrolls
                    ? Loc.Pick("wheel or drag the bar to scroll    -    click outside to close",
                        "tekerlek ya da çubukla kaydır    -    kapatmak için dışarı tıkla")
                    : Loc.Pick("click outside to close", "kapatmak için dışarı tıkla"),
                90, 0.023f, HintColor, ChromeOrder, TextAnchor.MiddleCenter);
        }

        /// <summary>True if the point is inside the overlay's panel. A click in here must NOT
        /// close the overlay - only one on the dim outside it does.</summary>
        public bool PanelContains(Vector2 world)
        {
            Vector2 local = ToLocal(world);
            return IsOpen
                && Mathf.Abs(local.x - panelBoundsCenter.x) <= panelBoundsHalf.x
                && Mathf.Abs(local.y - panelBoundsCenter.y) <= panelBoundsHalf.y;
        }

        /// <summary>World point in the overlay's own space. The panel is scaled to fill the
        /// camera, so every hit test has to come through here.</summary>
        private Vector2 ToLocal(Vector2 world)
        {
            return transform.InverseTransformPoint(new Vector3(world.x, world.y, 0f));
        }

        /// <summary>Scales the whole overlay so the panel fills the window - the same treatment
        /// the market gets. Without it the panel is sized in raw world units and a taller list
        /// runs off a 5-unit camera.</summary>
        private void FitToCamera(Vector2 panelCenter, Vector2 panelSize)
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic || panelSize.x <= 0f || panelSize.y <= 0f)
            {
                return;
            }
            const float Margin = 0.96f;
            const float MaxScale = 1.6f;
            float halfHeight = cam.orthographicSize * Margin;
            float halfWidth = halfHeight * cam.aspect;
            float scale = Mathf.Min(MaxScale,
                Mathf.Min(halfHeight / (panelSize.y * 0.5f), halfWidth / (panelSize.x * 0.5f)));
            transform.localScale = new Vector3(scale, scale, 1f);
            Vector3 camPos = cam.transform.position;
            transform.position = new Vector3(camPos.x - panelCenter.x * scale,
                camPos.y - panelCenter.y * scale, 0f);
        }

        /// <summary>True if the point is on the scrollbar (track or thumb) - the start of a drag.
        /// Generous horizontally, because the bar itself is deliberately thin.</summary>
        public bool ScrollbarAt(Vector2 world)
        {
            Vector2 local = ToLocal(world);
            return scrollbarShown
                && Mathf.Abs(local.x - scrollTrackCenter.x) <= scrollTrackHalf.x + 0.22f
                && Mathf.Abs(local.y - scrollTrackCenter.y) <= scrollTrackHalf.y + 0.22f;
        }

        /// <summary>Jumps the list so the thumb follows this y - the click-and-drag path. The
        /// top of the track is the top of the deck.</summary>
        public void ScrollToWorldY(float worldY)
        {
            if (!scrollbarShown || lastCards == null || totalRows <= VisibleRows)
            {
                return;
            }
            float top = scrollTrackCenter.y + scrollTrackHalf.y;
            float bottom = scrollTrackCenter.y - scrollTrackHalf.y;
            float t = Mathf.InverseLerp(top, bottom, ToLocal(new Vector2(0f, worldY)).y);
            SetScroll(t * (totalRows - VisibleRows));
        }

        /// <summary>Scrolls the list by a FRACTION of a row and re-lays it out. No-op when the
        /// overlay is not showing a scrollable list.</summary>
        public void Scroll(float deltaRows)
        {
            SetScroll(scrollRows + deltaRows);
        }

        private void SetScroll(float wantedRows)
        {
            if (!IsOpen || lastCards == null || totalRows <= VisibleRows)
            {
                return;
            }
            float wanted = Mathf.Clamp(wantedRows, 0f, totalRows - VisibleRows);
            // A rebuild per frame is fine at this scale, but not a rebuild per NOTHING.
            if (Mathf.Abs(wanted - scrollRows) < 0.0005f)
            {
                return;
            }
            scrollRows = wanted;
            Show(lastCards, lastSellValue);
        }

        /// <summary>Starts the next visit at the top. Called when the overlay is OPENED, not on
        /// every rebuild - selling a card must not scroll the list out from under the player.</summary>
        public void ResetScroll()
        {
            scrollRows = 0f;
        }

        /// <summary>A panel-coloured plate over one of the padding bands, hiding the rows that
        /// scroll into it. Invisible as a plate - it is exactly the panel's own colour.</summary>
        private void BuildOccluder(string name, float bottom, float top)
        {
            if (top - bottom <= 0.01f)
            {
                return;
            }
            ViewUtil.MakeRect(transform, name, new Vector2(0f, (top + bottom) * 0.5f),
                new Vector2(PanelHalfWidth * 2f, top - bottom), PanelColor, OccluderOrder);
        }

        /// <summary>Track and thumb down the right edge, showing where in the deck you are.</summary>
        private void BuildScrollbar(float top, float bottom, float maxScroll)
        {
            float x = PanelHalfWidth - 0.24f;
            float height = top - bottom;
            scrollTrackCenter = new Vector2(x, (top + bottom) * 0.5f);
            scrollTrackHalf = new Vector2(0.08f, height * 0.5f);
            ViewUtil.MakeRect(transform, "ScrollTrack", scrollTrackCenter,
                new Vector2(0.16f, height), ScrollTrackColor, ScrollTrackOrder);
            float thumbHeight = Mathf.Max(0.5f, height * VisibleRows / totalRows);
            float travel = height - thumbHeight;
            float t = maxScroll > 0f ? scrollRows / maxScroll : 0f;
            float thumbY = top - thumbHeight * 0.5f - travel * t;
            ViewUtil.MakeRect(transform, "ScrollThumb", new Vector2(x, thumbY),
                new Vector2(0.22f, thumbHeight), ScrollThumbColor, ScrollThumbOrder);
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
            Vector2 local = ToLocal(world);
            return confirmButtonShown && confirmEnabled
                && Mathf.Abs(local.x - confirmButtonCenter.x) <= confirmButtonHalf.x
                && Mathf.Abs(local.y - confirmButtonCenter.y) <= confirmButtonHalf.y;
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
            Vector2 local = ToLocal(world);
            for (int i = 0; i < entryCenters.Count; i++)
            {
                if (Mathf.Abs(local.x - entryCenters[i].x) <= CardVisual.BodyWidth * CardScale * 0.5f
                    && Mathf.Abs(local.y - entryCenters[i].y) <= CardVisual.BodyHeight * CardScale * 0.5f)
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
            scrollbarShown = false;
            // Undo the fit, or the picker - which lays itself out in raw world units - would
            // inherit whatever scale the last card list was given.
            transform.localScale = Vector3.one;
            transform.position = Vector3.zero;
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
