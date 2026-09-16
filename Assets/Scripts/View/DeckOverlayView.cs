// PURPOSE: Full-screen overlay that lists the player's WHOLE owned deck ("oyun
// destesi"), opened by clicking the draw pile. Cards are shown SORTED (by size, then
// id), never in draw order - the draw pile is face-down and its order must not leak.
// Future reveal jokers (Insider, Büyüteç) will get their own explicit reveal UI.
//
// The FOX picker is the same grid again, showing one entry per SHAPE rather than one per
// card - see GameUiController.FoxShapeChoices. It only ever needed the shapes, and a deck
// with four copies of a piece used to offer that piece four times.
//
// The SELL screen is the same grid with one flag flipped. It carries no price labels of
// its own: what a card fetches, and whether it is a deck card or one you bought, is the
// hover TOOLTIP's business (GameUiController.Tooltips.ShowDeckCardTooltip). A price under
// every card is 24 numbers competing for the eye; a popup is one, about the card you are
// actually looking at.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Modal card-list overlay. While open, the controller blocks other input.</summary>
    public sealed class DeckOverlayView : MonoBehaviour
    {
        /// <summary>The WIDEST a row may get. The row actually used is narrower whenever the
        /// list is short (see ColumnsFor) - a nine-shape fox picker in an eight-wide panel is
        /// one full row, one lonely card and a great deal of empty table.</summary>
        private const int MaxColumns = 8;
        private const float CardScale = 0.72f;
        private const float SpacingX = 1.15f;
        private const float SpacingY = 1.45f;

        /// <summary>Rows on screen at once. Everything past this scrolls; a 24-card deck is
        /// already 3 rows, and the deck only grows from there.</summary>
        private const int VisibleRows = 3;

        /// <summary>Centre of the top visible row.</summary>
        private const float GridTop = 2.35f;

        /// <summary>Air between the outermost card and the panel's edge.</summary>
        private const float SidePadding = 0.64f;

        /// <summary>Extra room down the right edge when the scrollbar is there to be drawn.</summary>
        private const float ScrollbarGutter = 0.30f;

        /// <summary>
        /// Half the panel's width, WORKED OUT PER LIST rather than fixed: the grid it has to
        /// hold, floored by the longest line of chrome printed across it. A panel is then as
        /// wide as its contents and no wider, which is what stops a handful of choices being
        /// spread over a full-screen table.
        ///
        /// It is a field because everything measured off the edge - the occluder plates, the
        /// scrollbar, the panel itself - has to agree with it.
        /// </summary>
        private float panelHalfWidth = 5.15f;

        // Sorting tiers, all distinct: the dim and the frame used to share order 40, and which
        // of the two won was down to creation order - so on some rebuilds the dim covered the
        // frame and the panel's border simply vanished.
        private const int DimOrder = 39;
        private const int FrameOrder = 40;
        private const int PanelOrder = 41;
        private const int CardOrder = 42;
        private const int ScrollTrackOrder = 43;
        private const int ScrollThumbOrder = 44;
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
        private bool lastSellMode;
        private bool lastShapeMode;
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
            Show(cards, false);
        }

        /// <summary>Shows the owned deck. In SELL mode the title says so and clicking a card
        /// sells it; the price and the card's origin are the hover tooltip's job, so the grid
        /// itself is laid out identically either way.</summary>
        public void Show(IReadOnlyList<BlockCard> cards, bool sellMode)
        {
            ShowList(cards, sellMode, false);
        }

        /// <summary>The FOX picker: the shapes the deck can offer, one entry each, every one of
        /// them drawn as a fox block. The caller builds those stand-in cards (they are not owned
        /// and must never be sold or counted) - all this screen changes is what it calls itself
        /// and that clicking is a CHOICE rather than a sale.</summary>
        public void ShowShapes(IReadOnlyList<BlockCard> shapeCards)
        {
            ShowList(shapeCards, false, true);
        }

        private void ShowList(IReadOnlyList<BlockCard> cards, bool sellMode, bool shapeMode)
        {
            lastCards = cards;
            lastSellMode = sellMode;
            lastShapeMode = shapeMode;
            Hide();
            IsOpen = true;

            var sorted = new List<BlockCard>(cards);
            sorted.Sort(CompareCards);
            float pitch = SpacingY;
            int columns = ColumnsFor(sorted.Count, shapeMode);
            totalRows = (sorted.Count + columns - 1) / columns;
            float maxScroll = Mathf.Max(0, totalRows - VisibleRows);
            scrollRows = Mathf.Clamp(scrollRows, 0f, maxScroll);
            int shownRows = Mathf.Min(VisibleRows, totalRows);

            float gridBottom = GridTop - (shownRows - 1) * pitch;
            float cardHalf = CardVisual.BodyHeight * CardScale * 0.5f;
            // How far a row's content reaches below its centre. Nothing hangs under a card any
            // more, so this is the card itself - kept as its own name because the occluder /
            // cull / panel sequence below is written in terms of it.
            float reachDown = cardHalf;
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

            // What the two chrome lines SAY is decided here, above the panel, because how wide
            // they are is one of the things that decides how wide the panel is. The fox picker
            // keeps its title short on purpose - what taking a shape does is in the tooltip on
            // each one, and a sentence up here would be the only reason the panel was wide.
            string title = shapeMode
                ? Loc.Pick("PICK A SHAPE", "ŞEKİL SEÇ")
                : sellMode
                    ? Loc.Pick("SELL CARDS  -  hover for details, click to sell",
                        "KART SAT  -  detay için üzerine gel, satmak için tıkla")
                    : Loc.Pick("YOUR DECK  -  " + sorted.Count + " cards",
                        "DESTEN  -  " + sorted.Count + " kart");
            string hint = scrolls
                ? Loc.Pick("wheel or drag the bar to scroll    -    click outside to close",
                    "tekerlek ya da çubukla kaydır    -    kapatmak için dışarı tıkla")
                : Loc.Pick("click outside to close", "kapatmak için dışarı tıkla");

            // A full-screen dim so the market behind is clearly OUT of play, then an OPAQUE
            // panel on top of it - the old overlay was only the dim, so the whole shelf showed
            // through the card list and the two fought each other.
            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(40f, 20f),
                DimColor, DimOrder);
            // The three claims on the width - the grid, the title, the hint - settled before
            // anything is drawn, because the panel, the plates and the bar are all measured
            // off the answer. Text is ESTIMATED (see EstimateTextWidth): a TextMesh will not
            // give its extent until it has been laid out, and by then the panel exists.
            float gridHalf = (columns - 1) * SpacingX * 0.5f
                + CardVisual.BodyWidth * CardScale * 0.5f + SidePadding
                + (scrolls ? ScrollbarGutter : 0f);
            panelHalfWidth = Mathf.Max(gridHalf,
                Mathf.Max(EstimateTextWidth(title, 0.030f),
                    EstimateTextWidth(hint, 0.023f)) * 0.5f + 0.35f);

            var panelCenter = new Vector2(0f, (panelTop + panelBottom) * 0.5f);
            var panelSize = new Vector2(panelHalfWidth * 2f, panelTop - panelBottom);
            ViewUtil.MakeRect(transform, "PanelFrame", panelCenter,
                panelSize + new Vector2(0.22f, 0.22f), PanelFrameColor, FrameOrder);
            ViewUtil.MakeRect(transform, "Panel", panelCenter, panelSize, PanelColor, PanelOrder);
            panelBoundsCenter = panelCenter;
            panelBoundsHalf = panelSize * 0.5f + new Vector2(0.11f, 0.11f);

            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, titleY), title,
                90, 0.030f, TitleColor, ChromeOrder, TextAnchor.MiddleCenter);

            // A fractional offset means the row above and the row below can BOTH be partly on
            // screen, so the window reaches one row past the visible band on each side and rows
            // are then culled by their CENTRE. The overspill lands in the panel's padding, which
            // matters because a sprite mask cannot clip this overlay's TextMesh chrome.
            int firstIndex = Mathf.Max(0, (Mathf.FloorToInt(scrollRows) - 1) * columns);
            int lastIndex = Mathf.Min(sorted.Count,
                (Mathf.CeilToInt(scrollRows) + VisibleRows + 1) * columns);
            for (int i = firstIndex; i < lastIndex; i++)
            {
                int row = i / columns;
                int column = i % columns;
                int columnsInRow = Mathf.Min(columns, sorted.Count - row * columns);
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
            // out. This is the stand-in for a sprite mask, which the TextMesh chrome in this
            // overlay ignores entirely.
            BuildOccluder("TopPlate", topCut, panelTop);
            BuildOccluder("BottomPlate", panelBottom, bottomCut);

            scrollbarShown = scrolls;
            if (scrolls)
            {
                BuildScrollbar(GridTop + cardHalf, gridBottom - cardHalf, maxScroll);
            }
            FitToCamera(panelCenter, panelSize);
            ViewUtil.MakeText3D(transform, "CloseHint", new Vector2(0f, hintY), hint,
                90, 0.023f, HintColor, ChromeOrder, TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// How many cards a row holds for a list this long. Never more than MaxColumns, and
        /// never more than there are cards - a five-card list is five wide, not eight wide with
        /// three holes in it.
        ///
        /// The SHAPE picker goes further and blocks the grid up, because it is the one list
        /// that is always short: nine shapes read as a 3x3 block of choices rather than a row
        /// of eight with one card stranded underneath. Two claims decide how wide that block
        /// is - enough columns to fit every shape in the rows that are ON SCREEN (a picker the
        /// player has to scroll is a picker that hides half the answer), and never narrower
        /// than a square. Whichever asks for more wins.
        /// </summary>
        private static int ColumnsFor(int count, bool shapeMode)
        {
            int wanted = shapeMode
                ? Mathf.Max(Mathf.CeilToInt(count / (float)VisibleRows),
                    Mathf.CeilToInt(Mathf.Sqrt(count)))
                : count;
            return Mathf.Clamp(wanted, 1, MaxColumns);
        }

        /// <summary>
        /// Roughly how wide a line of overlay chrome will come out, in world units. A TextMesh
        /// only knows its extent once it has been laid out, which is a frame too late for a
        /// panel that has to be sized around it, so this estimates instead: Unity draws a
        /// TextMesh glyph at characterSize * fontSize / 10 tall, and this font averages about
        /// half that wide. Every caller here uses fontSize 90.
        ///
        /// Deliberately a slight OVER-estimate - too wide a panel is a panel with air in it,
        /// too narrow is a title hanging off the edge.
        /// </summary>
        private static float EstimateTextWidth(string text, float characterSize)
        {
            return text == null ? 0f : text.Length * characterSize * 90f * 0.05f;
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
            ShowList(lastCards, lastSellMode, lastShapeMode);
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
                new Vector2(panelHalfWidth * 2f, top - bottom), PanelColor, OccluderOrder);
        }

        /// <summary>Track and thumb down the right edge, showing where in the deck you are.</summary>
        private void BuildScrollbar(float top, float bottom, float maxScroll)
        {
            float x = panelHalfWidth - 0.24f;
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

            var sorted = new List<BlockCard>(cards);
            sorted.Sort(CompareCards);
            int rows = (sorted.Count + MaxColumns - 1) / MaxColumns;
            int columns = Mathf.Min(MaxColumns, Mathf.Max(1, sorted.Count));
            float startY = (rows - 1) * SpacingY * 0.5f + 0.3f;
            float bottomRowY = startY - (rows - 1) * SpacingY;
            float cardHalfW = CardVisual.BodyWidth * CardScale * 0.5f;
            float cardHalfH = CardVisual.BodyHeight * CardScale * 0.5f;

            // THE PANEL IS MEASURED ROUND WHAT IT HOLDS, exactly as the deck list's is - the
            // picker used to be a dim wash with cards floating on it and a title in open space,
            // which is the one screen in the game asking the player to make a considered choice
            // and the one with nothing to make it in.
            confirmButtonHalf = new Vector2(1.7f, 0.42f);
            confirmButtonCenter = new Vector2(0f, bottomRowY - cardHalfH - 0.30f
                - confirmButtonHalf.y);
            float titleY = startY + cardHalfH + 0.52f;
            float panelTop = titleY + 0.42f;
            float panelBottom = confirmButtonCenter.y - confirmButtonHalf.y - 0.40f;
            float gridHalf = (columns - 1) * SpacingX * 0.5f + cardHalfW + SidePadding;
            panelHalfWidth = Mathf.Max(gridHalf,
                EstimateTextWidth(header, 0.026f) * 0.5f + 0.35f);

            var panelCenter = new Vector2(0f, (panelTop + panelBottom) * 0.5f);
            var panelSize = new Vector2(panelHalfWidth * 2f, panelTop - panelBottom);

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(40f, 20f),
                DimColor, DimOrder);
            ViewUtil.MakeRect(transform, "PanelFrame", panelCenter,
                panelSize + new Vector2(0.22f, 0.22f), PanelFrameColor, FrameOrder);
            ViewUtil.MakeRect(transform, "Panel", panelCenter, panelSize, PanelColor, PanelOrder);
            panelBoundsCenter = panelCenter;
            panelBoundsHalf = panelSize * 0.5f + new Vector2(0.11f, 0.11f);

            ViewUtil.MakeText3D(transform, "PickTitle", new Vector2(0f, titleY), header, 90, 0.026f,
                TitleColor, ChromeOrder, TextAnchor.MiddleCenter);

            for (int i = 0; i < sorted.Count; i++)
            {
                int row = i / MaxColumns;
                int column = i % MaxColumns;
                int columnsInRow = Mathf.Min(MaxColumns, sorted.Count - row * MaxColumns);
                float startX = -(columnsInRow - 1) * SpacingX * 0.5f;
                var position = new Vector2(startX + column * SpacingX, startY - row * SpacingY);
                if (selectedIds.Contains(sorted[i].Id))
                {
                    BuildPickHighlight("PickHi_" + i, position);
                }
                CardVisual visual = CardVisual.Create(transform, "Overlay_" + sorted[i].Id,
                    sorted[i], true, false, position, PickCardOrder);
                visual.transform.localScale = new Vector3(CardScale, CardScale, 1f);
                entryCenters.Add(position);
                entryShapes.Add(sorted[i].Shape);
                entryCards.Add(sorted[i]);
                entryVisuals.Add(visual);
            }

            confirmEnabled = selectedIds.Count == target;
            confirmButtonShown = true;
            SpriteRenderer confirm = ViewUtil.MakeRounded(transform, "PickConfirm",
                confirmButtonCenter, confirmButtonHalf * 2f,
                confirmEnabled ? ConfirmReadyColor : ConfirmIdleColor, PickConfirmOrder);
            // The button breathes ONLY when pressing it would actually do something, so the one
            // moving thing on the screen is always the next thing to do.
            if (confirmEnabled)
            {
                PulseSpriteFx.Attach(confirm, 0.80f, 1f, 1.5f, 0f);
            }
            ViewUtil.MakeText3D(transform, "PickConfirmLabel", confirmButtonCenter,
                Loc.Pick("CONFIRM  ", "ONAYLA  ") + selectedIds.Count + "/" + target,
                90, 0.02f, confirmEnabled ? new Color(0.86f, 1f, 0.90f) : new Color(0.55f, 0.56f, 0.60f),
                ChromeOrder, TextAnchor.MiddleCenter);
            FitToCamera(panelCenter, panelSize);
        }

        /// <summary>
        /// THE SELECTION MARK - a thin BREATHING ring just outside the card, not a slab behind it.
        ///
        /// It was a hard cyan rectangle a quarter of a unit bigger than the card on every side,
        /// which is a wide flat border in a colour nothing else on the screen uses: it read as a
        /// highlighter drawn over the card rather than as the card being chosen, and it shouted
        /// loud enough that eight of them made the grid unreadable.
        ///
        /// Now: rounded like the card itself so the two silhouettes agree, THIN (0.09 rather than
        /// 0.12 a side), in the overlay's own warm title gold rather than a fifth hue, and
        /// breathing - which is what the bar card does to mean "live", so the picker and the bar
        /// say the same thing the same way. A faint wash sits inside it so a selected card also
        /// reads as chosen at a glance, without a border thick enough to crop the art.
        /// </summary>
        private void BuildPickHighlight(string name, Vector2 position)
        {
            var size = new Vector2(CardVisual.BodyWidth * CardScale + PickRingThickness * 2f,
                CardVisual.BodyHeight * CardScale + PickRingThickness * 2f);
            SpriteRenderer ring = ViewUtil.MakeRounded(transform, name, position, size,
                PickRingColor, PickRingOrder);
            PulseSpriteFx.Attach(ring, 0.55f, 1f, 1.5f, 0.012f);
        }

        /// <summary>How far the selection ring stands out past the card, per side.</summary>
        private const float PickRingThickness = 0.09f;

        // THE PICKER'S OWN LADDER, all distinct - the ring first went in at CardOrder - 1, which
        // is PanelOrder, and an equal sorting order between two sprites is decided by nothing at
        // all: the opaque panel won and the selection mark was simply never visible. This is the
        // same trap the dim and the frame fell into at order 40 (see the constants above), so the
        // picker gets tiers of its own rather than borrowing the list's.
        private const int PickRingOrder = 42;

        private const int PickCardOrder = 43;

        private const int PickConfirmOrder = 44;

        private static readonly Color PickRingColor = new Color(1f, 0.84f, 0.42f);

        private static readonly Color ConfirmReadyColor = new Color(0.20f, 0.46f, 0.27f);

        private static readonly Color ConfirmIdleColor = new Color(0.15f, 0.16f, 0.19f);

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

        /// <summary>How many cards are laid out right now - the visible page, not the whole
        /// collection. A gamepad steps these (see GameUiController.PadPanels.cs).</summary>
        public int EntryCount
        {
            get { return entryCenters.Count; }
        }

        /// <summary>World centre of a laid-out card, or null. The inverse of EntryAt.</summary>
        public Vector2? EntryWorldCenter(int index)
        {
            if (index < 0 || index >= entryCenters.Count)
            {
                return null;
            }
            Vector3 world = transform.TransformPoint(
                new Vector3(entryCenters[index].x, entryCenters[index].y, 0f));
            return new Vector2(world.x, world.y);
        }

        /// <summary>The SHAPE offered by a laid-out slot (the fox picker reads this), or null.</summary>
        public BlockShape EntryShape(int index)
        {
            return index >= 0 && index < entryShapes.Count ? entryShapes[index] : null;
        }

        /// <summary>The card in a laid-out slot, or null - so a pad can keep hold of WHICH card
        /// it had chosen across a scroll, which relays every slot.</summary>
        public BlockCard EntryCard(int index)
        {
            return index >= 0 && index < entryCards.Count ? entryCards[index] : null;
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
            lastShapeMode = false;
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
