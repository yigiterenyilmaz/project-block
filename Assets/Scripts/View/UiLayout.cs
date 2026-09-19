// PURPOSE: WHERE EVERYTHING GOES. One authority for every number that differs between the desktop
// screen and a phone held upright, so the same code draws both.
//
// WHY IT EXISTS. The interface here is BUILT IN CODE - there are no prefabs to duplicate - so the
// layout used to live as a hundred-odd `private const` scattered through the view files. That is
// fine for one shape and impossible for two: a second shape would have meant a second copy of every
// view, and the two would have drifted apart within a week. Instead the views ask this class where
// things go, and swapping profiles swaps the whole interface at once.
//
// DESKTOP IS THE OLD NUMBERS, LITERALLY. Every value in the desktop profile is the constant that
// used to sit in the view that reads it, copied across unchanged. That is what makes the PC layout
// safe by construction rather than by inspection, and UiLayoutTests is what keeps it that way.
//
// PORTRAIT IS DERIVED, NOT TYPED. A phone is not one aspect - 9:16 and 9:19.5 are both normal - so
// the portrait numbers are SOLVED for the screen in front of us. The camera fixes the WIDTH (the
// board always fills the same fraction of it) instead of the height, which is what makes the board
// the same physical size on every phone and turns a taller screen into breathing room rather than
// into a bigger board. Everything else anchors to an edge: the HUD to the top, the hand to the
// bottom. Nothing is positioned by an absolute y that only works at one aspect.
//
// EXTENSION POINT: a third profile (tablet landscape, say) is a third instance and a rule in
// Pick. Nothing else has to learn about it.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The screen shape the interface is currently laid out for.</summary>
    public enum UiShape
    {
        Desktop,
        Portrait
    }

    /// <summary>Every layout number that depends on the shape of the screen.</summary>
    public sealed class UiLayout
    {
        // =================================================================== the profiles

        /// <summary>The PC layout. These are the values the views used to hold themselves.</summary>
        public static readonly UiLayout Desktop = new UiLayout(UiShape.Desktop);

        /// <summary>The phone-held-upright layout. Most of it is solved in Resolve.</summary>
        public static readonly UiLayout Portrait = new UiLayout(UiShape.Portrait);

        /// <summary>What everything draws against right now.</summary>
        public static UiLayout Active = Desktop;

        /// <summary>Forces a shape regardless of the real screen, for looking at the other layout
        /// without leaving the editor. Null follows the screen. See GameUiController's toggle.</summary>
        public static UiShape? Override;

        /// <summary>
        /// The part of the window the game is actually drawn into, in normalised coordinates.
        ///
        /// Normally the whole thing. It shrinks only when a profile is FORCED onto a window of the
        /// wrong shape - asking for the phone layout on a 16:9 monitor - and then it becomes a
        /// phone-shaped area in the middle with the rest left black. That is the honest way to
        /// show the portrait layout on a desktop: the alternative is stretching a design meant for
        /// 9:16 across 16:9, which says nothing about how it will really look.
        /// </summary>
        public static Rect Viewport = new Rect(0f, 0f, 1f, 1f);

        /// <summary>The shape a forced profile is letterboxed to.</summary>
        private const float PortraitAspect = 9f / 16f;

        private const float DesktopAspect = 16f / 9f;

        /// <summary>The window area an aspect of <paramref name="want"/> fits into, centred.</summary>
        private static Rect Fit(float screen, float want)
        {
            if (Mathf.Approximately(screen, want) || screen <= 0f || want <= 0f)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }
            if (screen > want)
            {
                float w = want / screen;                  // too wide: bars left and right
                return new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            float h = screen / want;                      // too tall: bars above and below
            return new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }

        public readonly UiShape Shape;

        public bool IsPortrait { get { return Shape == UiShape.Portrait; } }

        private UiLayout(UiShape shape)
        {
            Shape = shape;
            if (shape == UiShape.Desktop)
            {
                LoadDesktop();
            }
            else
            {
                LoadPortrait();
            }
        }

        // =================================================================== world space

        /// <summary>Half the visible height, in world units - the camera's orthographic size.
        /// Fixed on the desktop; solved from the aspect in portrait.</summary>
        public float OrthoSize;

        /// <summary>Half the visible WIDTH, in world units. Derived, and the number that decides
        /// whether anything fits: at 16:9 it is 8.89, at 9:16 it would be 2.81.</summary>
        public float HalfWidth;

        /// <summary>The box the board is fitted into (BoardView divides it by the cell count).</summary>
        public float BoardWorldSize;

        public Vector2 BoardCenter;

        public Vector2 HandCenter;

        public float HandSpacing;

        public float HandFanSpan;

        public float HandFanSpanMax;

        public float HandMinSpacing;

        /// <summary>How big a card is drawn against its own art size. 1 on the desktop; smaller in
        /// portrait, where five cards have to fit across a phone.</summary>
        public float CardScale;

        public Vector2 DrawPile;

        public Vector2 DiscardPile;

        public float PileScale;

        // =================================================================== the HUD canvas

        public Vector2 CanvasReference;

        /// <summary>CanvasScaler.matchWidthOrHeight. 0 = scale by WIDTH, which is what keeps a UI
        /// pixel equal to a device pixel on every 1080-wide phone and sends the extra height of a
        /// tall one into empty space instead of into everything growing.</summary>
        public float CanvasMatch;

        /// <summary>False stacks the joker and power panels down a screen edge (desktop); true lays
        /// them out as a ROW of square slots under the score. A vertical stack of 232px panels eats
        /// a phone's width and ends up over the board.</summary>
        public bool BarsAsRow;

        public Vector2 JokerPanel;

        public float JokerGap;

        public Vector2 PowerPanel;

        public float PowerGap;

        /// <summary>Cards per row when the bars stack down a screen edge (desktop), per bar. Ignored
        /// by a row layout, which is one line by definition.</summary>
        public int JokerColumns = 1;

        public int PowerColumns = 1;

        public float CornerInset;

        public float BadgeSize;

        public float BadgeMargin;

        public int ScoreFont;

        public int MessageFont;

        public int InfoFont;

        public float InfoWidth;

        /// <summary>Left edge of the info/debug column, in canvas px.</summary>
        public float InfoLeft;

        /// <summary>Where the score sits, as a fraction down from the top of the canvas.</summary>
        public float ScoreTop;

        public float MessageTop;

        /// <summary>Top of the joker row, in canvas units from the top. Row layouts only.</summary>
        public float BarRowTop;

        // =================================================================== the menus

        // The menu shell is the FIRST thing anyone sees, and it was authored against a 1920-wide
        // canvas: a 460-wide button is a quarter of a monitor and a comfortable click. On a phone
        // the same numbers land on a 1080-wide canvas, where they read as a small dialog floating
        // in the middle of a tall screen - and a 76px row is under the size a thumb reliably hits.
        // So portrait gets its own set: wider rows, taller rows, bigger type.
        public float MenuButtonWidth;

        public float MenuButtonHeight;

        public float MenuButtonGap;

        public float MenuAccentWidth;

        public float MenuHeaderGap;

        public int MenuTitleFont;

        public int MenuSubtitleFont;

        public int MenuLabelFont;

        public int MenuNoteFont;

        public int MenuBodyFont;

        // =================================================================== the market

        /// <summary>False puts the shelf in two COLUMNS (blocks down the left, jokers over powers
        /// on the right); true STACKS the three sections down the screen. A phone has about seven
        /// world units of width, and two columns of shelf need nine before they start shrinking -
        /// so side by side is not a tight fit there, it is a squashed one.</summary>
        public bool MarketStacked;

        /// <summary>What the market panel leaves clear at the top, the bottom and the sides, in
        /// world units. On the desktop these keep it off the piles and the bars; in portrait the
        /// bars are a strip across the top instead, so the numbers are different.</summary>
        public float MarketTopReserve;

        public float MarketBottomReserve;

        public float MarketSideReserve;

        /// <summary>The strip along the bottom of the market. A phone gets a taller one, because
        /// the hints and PROCEED cannot share a single line across a narrow panel - they end up
        /// printed over each other.</summary>
        public float MarketFooterHeight;

        /// <summary>STACKED layouts only: how tall each shelf is, in world units. They are given
        /// a size that suits their own tiles rather than a share of what is left, so the tiles
        /// stay readable and the shelf SCROLLS when the three of them do not fit. Splitting the
        /// available height between them is what made every tile shrink as offers were added.</summary>
        public float MarketBlockSection;

        public float MarketNamedSection;

        // =================================================================== desktop

        private void LoadDesktop()
        {
            // GameUiController.cs
            OrthoSize = 5f;                       // Scenes/enes.unity, the camera
            BoardWorldSize = 6.5f;
            BoardCenter = new Vector2(0f, 0.9f);

            // CardLayerView.cs
            HandCenter = new Vector2(0f, -4.05f);
            HandSpacing = 1.7f;
            HandFanSpan = 9f;
            HandFanSpanMax = 10.1f;
            HandMinSpacing = 0.8f;
            CardScale = 1f;
            DrawPile = new Vector2(6.4f, -4.05f);
            DiscardPile = new Vector2(-6.4f, -4.05f);
            PileScale = 1f;

            // GameUiController.Views.cs
            CanvasReference = new Vector2(1920f, 1080f);
            CanvasMatch = 0f;
            ScoreFont = 34;
            MessageFont = 28;
            InfoFont = 20;
            // The info/debug column sits to the RIGHT of the power column, which now starts at
            // the very top of the left edge: 16 + 160 + 16 = 192, and 192 + 400 stops short of
            // the board (its left edge is ~609 at 1920).
            InfoLeft = 192f;
            InfoWidth = 400f;
            ScoreTop = 14f;
            MessageTop = 66f;

            // JokerBarView.cs / PowerBarView.cs / GameUiController.BossBadge.cs
            BarsAsRow = false;
            // VERTICAL CARDS. Jokers two to a row: 2 x 160 + 10 + the 16 inset is 346, which is as
            // wide as they can go and still clear the market panel (centred, about 345 in from
            // each side at 1920). Powers are fewer and have the whole left edge under the info
            // text, so they take one column.
            JokerPanel = new Vector2(160f, 224f);
            JokerGap = 10f;
            // THE POWER SLOT IS THE POWER CARD'S OWN SHAPE. Art/Cards/card_power is 946x1040 -
            // 0.91, nearly square, because it has no name plate to be tall for. Left at the
            // joker's 0.71 the painting is either squashed or sits in a band of empty slot, and
            // an empty band in a bar reads as a layout bug rather than as a design.
            PowerPanel = new Vector2(160f, 176f);
            PowerGap = 10f;
            JokerColumns = 2;
            PowerColumns = 1;
            CornerInset = 16f;
            BadgeSize = 92f;
            BadgeMargin = 16f;
            BarRowTop = 0f;                       // unused: the desktop stacks down an edge

            // Menus/MenuSkin.cs
            MenuButtonWidth = 460f;
            MenuButtonHeight = 76f;
            MenuButtonGap = 14f;
            MenuAccentWidth = 7f;
            MenuHeaderGap = 70f;
            MenuTitleFont = 68;
            MenuSubtitleFont = 24;
            MenuLabelFont = 30;
            MenuNoteFont = 21;
            MenuBodyFont = 24;

            // MarketView.cs
            MarketStacked = false;
            // The top still clears the score and the message line (~100 canvas px). The bottom
            // runs down OVER the piles: they are hidden while the market is up and the footer
            // carries a DECK button instead (the same bargain the portrait shelf makes), which is
            // what gives the shelf room to breathe.
            // EQUAL top and bottom, so the panel sits in the middle of the screen. The top still
            // clears the score and the message line (~0.93 world units).
            MarketTopReserve = 0.85f;
            MarketBottomReserve = 0.85f;
            MarketSideReserve = 0.35f;
            MarketFooterHeight = 1.2f;
            MarketBlockSection = 0f;              // unused: the desktop splits the height
            MarketNamedSection = 0f;
        }

        // =================================================================== portrait

        /// <summary>What fraction of the screen's WIDTH the board fills. The one number the whole
        /// portrait layout is solved from - raising it makes the board bigger on every phone at
        /// once and takes the room out of the margins.</summary>
        public const float PortraitBoardFill = 0.88f;

        /// <summary>The band reserved at the top for the score and the two bars, in world units.</summary>
        private const float PortraitHudBand = 3.05f;

        /// <summary>Between that band and the top of the board.</summary>
        private const float PortraitBoardGap = 0.30f;

        /// <summary>Between the bottom of the screen and the bottom of the hand - the thumb needs
        /// somewhere to be that is not on a card.</summary>
        private const float PortraitHandFoot = 0.62f;

        /// <summary>Between the top of the hand and the piles that sit above it.</summary>
        private const float PortraitPileGap = 0.34f;

        private const float PortraitPileInset = 0.52f;

        private void LoadPortrait()
        {
            BoardWorldSize = 6.5f;                // the board is the board; only the room changes
            HandSpacing = 1.42f;
            HandFanSpan = 6.0f;
            HandFanSpanMax = 6.9f;
            HandMinSpacing = 0.62f;
            CardScale = 0.92f;
            PileScale = 0.52f;

            CanvasReference = new Vector2(1080f, 1920f);
            CanvasMatch = 0f;
            ScoreFont = 46;
            MessageFont = 30;
            InfoFont = 26;
            InfoLeft = 16f;
            InfoWidth = 420f;
            ScoreTop = 26f;
            MessageTop = 84f;

            BarsAsRow = true;
            // Vertical card slots in a row: eight jokers still fit across 1080, and the two rows
            // together (128 + 150 + 12 + 150 = 440 canvas px, ~3.0 world) just stay inside
            // PortraitHudBand - this is the largest they can be without moving the board.
            JokerPanel = new Vector2(108f, 150f);
            JokerGap = 10f;
            PowerPanel = new Vector2(108f, 119f);
            PowerGap = 10f;
            CornerInset = 16f;
            BadgeSize = 76f;
            BadgeMargin = 16f;
            BarRowTop = 128f;

            // 820 of 1080 is 76% of the width, and 112 tall clears the ~9mm a thumb needs.
            MenuButtonWidth = 820f;
            MenuButtonHeight = 112f;
            MenuButtonGap = 22f;
            MenuAccentWidth = 9f;
            MenuHeaderGap = 90f;
            MenuTitleFont = 84;
            MenuSubtitleFont = 32;
            MenuLabelFont = 40;
            MenuNoteFont = 26;
            MenuBodyFont = 30;

            MarketStacked = true;
            // Enough to clear the joker and power rows, which are canvas UI and draw OVER the
            // panel however the panel is sized - so this is about the title being readable, not
            // about overlap.
            MarketTopReserve = 2.4f;
            // THE MARKET TAKES THE SCREEN. The desktop stops its panel above the piles, because
            // the deck pile IS the sell screen and covering it would lose the way in. A phone has
            // no room for that compromise - stopping short left the shelf on a third of the
            // screen - so the panel runs almost the full height, the piles are hidden under it,
            // and the deck gets a BUTTON in the footer (see MarketView.BuildStackedFooter).
            MarketBottomReserve = 0.5f;
            MarketSideReserve = 0.22f;
            MarketFooterHeight = 1.55f;
            MarketBlockSection = 3.3f;
            MarketNamedSection = 3.2f;            // tall enough for an upright joker/power card

            // The rest is solved per screen - see Resolve.
            Resolve(9f / 16f);
        }

        // =================================================================== solving

        /// <summary>
        /// Fits the layout to a screen. <paramref name="aspect"/> is width/height.
        ///
        /// The desktop keeps a fixed orthographic size and lets the width fall where it may, which
        /// is what it has always done. Portrait does the opposite: it fixes the WIDTH so the board
        /// fills the same fraction of every phone, and everything else anchors to an edge, so the
        /// extra height of a long phone becomes space around the board rather than a different
        /// layout.
        /// </summary>
        public void Resolve(float aspect)
        {
            aspect = Mathf.Max(aspect, 0.01f);
            if (Shape == UiShape.Desktop)
            {
                OrthoSize = 5f;
                HalfWidth = OrthoSize * aspect;
                return;
            }

            // HOW MUCH OF THE WIDTH THE BOARD MAY TAKE IS SOLVED, NOT ASSUMED.
            //
            // Fixing the board at a fraction of the WIDTH also fixes how much HEIGHT there is to
            // put things in, because the two are tied together by the aspect. On a long phone
            // that is generous; on a squarer one - a 3:4 tablet, say - asking for 88% of the
            // width leaves less height than the HUD, the board, the piles and the hand need, and
            // the board simply runs off the bottom.
            //
            // So the fraction is capped by what actually fits. Everything below the board and
            // above it is a fixed number of world units, so the whole stack fits exactly when
            //     fill <= 1 / (aspect * (1 + stack / board))
            // and the layout takes the smaller of that and what it would have liked.
            float stack = PortraitHudBand + PortraitBoardGap
                + PortraitHandFoot + CardVisual.BodyHeight * CardScale
                + PortraitPileGap + (CardVisual.BodyHeight + 0.18f) * PileScale;
            float fits = 1f / (aspect * (1f + stack / BoardWorldSize));
            // A hair under, so nothing ends up sitting exactly edge to edge.
            float fill = Mathf.Min(PortraitBoardFill, fits * 0.985f);

            HalfWidth = BoardWorldSize * 0.5f / fill;
            OrthoSize = HalfWidth / aspect;

            float top = OrthoSize;
            float bottom = -OrthoSize;

            float boardTop = top - PortraitHudBand - PortraitBoardGap;
            BoardCenter = new Vector2(0f, boardTop - BoardWorldSize * 0.5f);

            float cardHalf = CardVisual.BodyHeight * CardScale * 0.5f;
            HandCenter = new Vector2(0f, bottom + PortraitHandFoot + cardHalf);

            // The piles hang off the HAND, not off the middle of the gap above it: centred in that
            // gap they float in the open on a long phone, with nothing to belong to.
            float pileHalf = (CardVisual.BodyHeight + 0.18f) * PileScale * 0.5f;
            float pileY = HandCenter.y + cardHalf + PortraitPileGap + pileHalf;
            float pileX = HalfWidth - PortraitPileInset;
            DrawPile = new Vector2(pileX, pileY);
            DiscardPile = new Vector2(-pileX, pileY);

        }

        /// <summary>
        /// World units per CANVAS pixel. The HUD is laid out in canvas pixels and the board in
        /// world units, and this is the bridge - which anything that has to leave room for a
        /// piece of HUD needs, because how much room that is depends on the screen.
        /// </summary>
        public float WorldPerCanvasPixel
        {
            get { return HalfWidth * 2f / Mathf.Max(CanvasReference.x, 1f); }
        }

        /// <summary>
        /// How far down the screen the HUD actually reaches, in world units from the top, given
        /// how many joker and power slots are SHOWING.
        ///
        /// It is asked rather than assumed because the answer moves: with no jokers and no powers
        /// the two rows draw nothing, and reserving room for them anyway leaves a band of dead
        /// space that reads as a layout that failed rather than as breathing room.
        /// </summary>
        public float HudBottomWorld(int jokerSlots, int powerSlots)
        {
            if (!BarsAsRow)
            {
                return 0f;                        // the desktop stacks them down an edge
            }
            float pixels = MessageTop + MessageFont + 8f;
            if (jokerSlots > 0)
            {
                pixels = Mathf.Max(pixels, BarRowTop + JokerPanel.y);
            }
            if (powerSlots > 0)
            {
                pixels = Mathf.Max(pixels,
                    BarRowTop + JokerPanel.y + 12f + PowerPanel.y);
            }
            return pixels * WorldPerCanvasPixel;
        }

        // =================================================================== bars

        /// <summary>
        /// Anchors a bar's ROOT. The desktop hangs it from a top corner and stacks downward; a
        /// phone hangs it from the top CENTRE and lays it out sideways, because a column of
        /// 232-wide panels down the edge of a portrait screen ends up over the board.
        /// </summary>
        /// <param name="fromRight">Which corner the desktop layout uses. Ignored in a row.</param>
        /// <param name="topOffset">What the desktop layout has to clear above it (the boss
        /// badge). A row is positioned by the profile instead, so this is ignored there.</param>
        public static void PlaceBarRoot(RectTransform root, bool fromRight, float topOffset)
        {
            if (root == null)
            {
                return;
            }
            UiLayout layout = Active;
            if (!layout.BarsAsRow)
            {
                float x = fromRight ? 1f : 0f;
                root.anchorMin = new Vector2(x, 1f);
                root.anchorMax = new Vector2(x, 1f);
                root.pivot = new Vector2(x, 1f);
                root.anchoredPosition = new Vector2(
                    fromRight ? -layout.CornerInset : layout.CornerInset,
                    -layout.CornerInset - topOffset);
                return;
            }
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -topOffset);
        }

        /// <summary>
        /// Positions one SLOT of a bar. <paramref name="count"/> is how many are showing, which a
        /// row needs in order to stay centred and a column ignores.
        /// </summary>
        public static void PlaceBarSlot(RectTransform rect, int index, int count,
            Vector2 panel, float gap, bool fromRight)
        {
            if (rect == null)
            {
                return;
            }
            rect.sizeDelta = panel;
            if (!Active.BarsAsRow)
            {
                // A GRID down the edge, filled row by row starting at the SCREEN EDGE: the
                // right-hand bar fills right to left, the left-hand one left to right, so the
                // first card is always the one in the corner.
                int columns = Mathf.Max(1, fromRight ? Active.JokerColumns : Active.PowerColumns);
                int column = index % columns;
                int row = index / columns;
                float x = fromRight ? 1f : 0f;
                rect.anchorMin = new Vector2(x, 1f);
                rect.anchorMax = new Vector2(x, 1f);
                // Pivoted on the card's MIDDLE, with the position moved by half a card to keep
                // the corner where it was: every scale on a slot (the proc pulse, the hold
                // squeeze) grows about the pivot, and on a corner pivot the card slid away from
                // its screen edge - left and down on the joker bar - each time it fired.
                rect.pivot = new Vector2(0.5f, 0.5f);
                float across = (fromRight ? -column : column) * (panel.x + gap)
                    + (fromRight ? -0.5f : 0.5f) * panel.x;
                rect.anchoredPosition = new Vector2(across, -row * (panel.y + gap) - 0.5f * panel.y);
                return;
            }
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            // Centred on however many are showing, so the row grows outward from the middle
            // rather than off one end.
            float step = panel.x + gap;
            rect.anchoredPosition = new Vector2(
                (index - (count - 1) * 0.5f) * step, -0.5f * panel.y);
        }

        // =================================================================== choosing

        /// <summary>
        /// Points <see cref="Active"/> at the profile this screen calls for and fits it, returning
        /// true if anything actually changed - which is the caller's cue to rebuild.
        /// </summary>
        public static bool Refresh()
        {
            float aspect = Screen.height > 0
                ? Screen.width / (float)Screen.height
                : 16f / 9f;
            UiShape want = Override.HasValue ? Override.Value : Pick(aspect);
            UiLayout next = want == UiShape.Portrait ? Portrait : Desktop;

            // A FORCED profile on a window of the wrong shape gets a letterbox rather than a
            // stretch. Following the screen needs none: the shape was chosen FROM the window.
            Rect viewport = new Rect(0f, 0f, 1f, 1f);
            if (Override.HasValue)
            {
                viewport = Fit(aspect,
                    want == UiShape.Portrait ? PortraitAspect : DesktopAspect);
            }
            // What the camera and the layout must be solved for is the VIEWPORT's shape, not the
            // window's - inside a letterbox they are not the same number.
            float drawn = aspect * viewport.width / Mathf.Max(viewport.height, 0.0001f);

            bool changed = next != Active
                || !Mathf.Approximately(next.LastAspect, drawn)
                || viewport != Viewport;
            if (!changed)
            {
                return false;
            }
            Viewport = viewport;
            aspect = drawn;
            // ALWAYS the real aspect, forced or not. Solving a forced portrait profile for a
            // phone's aspect while the window is actually wide would hand the camera an
            // orthographic size for a screen that is not there, and the board would be drawn
            // clipped or floating. The way to SEE the phone layout properly is to give Unity's
            // game view a portrait aspect, which the profile then picks up on its own; the
            // override is for holding one shape while the other screen is in front of you.
            next.Resolve(aspect);
            next.LastAspect = aspect;
            Active = next;
            return true;
        }

        /// <summary>Taller than it is wide means a phone held upright.</summary>
        private static UiShape Pick(float aspect)
        {
            return aspect < 1f ? UiShape.Portrait : UiShape.Desktop;
        }

        private float LastAspect = float.NaN;
    }
}
