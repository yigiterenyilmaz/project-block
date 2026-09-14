// PURPOSE: One place for every menu colour, metric and (later) sprite, so the menus can
// be re-skinned without touching a single line of their layout code.
//
// EXTENSION POINT - ART: every surface is a flat Color today. Assign the matching Sprite
// field and MenuScreenView draws that instead; a null Sprite means "just use the colour".
// That is the whole migration path for real art - no structural change, no new views.
//
// Metrics are in CANVAS units against the HUD canvas's 1920x1080 reference resolution
// (see GameUiController.BuildViews), so they scale with the window like the rest of the HUD.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Shared styling for every menu screen. Static: this is presentation
    /// content, not per-instance state.</summary>
    public static class MenuSkin
    {
        // ------------------------------------------------------------------ colours

        /// <summary>Fills the whole screen behind a menu. Near-opaque: the title screen
        /// must not show the leftovers of a run behind it.
        ///
        /// Lifted from an all but black (0.04, 0.05, 0.08) by a flat 1.45x. MULTIPLIED rather
        /// than lightened towards white, which keeps the channel ratios and so the colour stays
        /// the same blue instead of drifting grey - the trap this project has hit before, in
        /// BackdropView. It does not want to go much further: the button plate is dark navy
        /// itself, and past about 1.9x the two stop separating and the buttons sink into it.</summary>
        public static Color Backdrop = new Color(0.058f, 0.0725f, 0.116f, 0.97f);

        /// <summary>Backdrop for a menu opened OVER a live run (pause, and the screens reached
        /// from it). Translucent on purpose - the player is mid-round and wants to see the
        /// board they are deciding about.</summary>
        public static Color OverlayBackdrop = new Color(0.03f, 0.04f, 0.06f, 0.80f);

        public static Color Title = new Color(1f, 0.93f, 0.72f);
        public static Color Subtitle = new Color(0.55f, 0.60f, 0.68f);

        public static Color Button = new Color(0.13f, 0.15f, 0.19f, 0.95f);

        /// <summary>The highlighted row. Deliberately a long way from Button: a hover you have
        /// to look for is the same as no hover at all.</summary>
        public static Color ButtonHover = new Color(0.31f, 0.37f, 0.48f, 1f);

        /// <summary>Bar down the left edge of the highlighted row - the unmistakable part of
        /// the highlight, since a colour shift alone reads poorly on a dark panel.</summary>
        public static Color Accent = new Color(1f, 0.82f, 0.35f);

        /// <summary>An entry that exists but cannot be chosen yet (Continue with no save).
        /// Deliberately still legible - a hidden entry teaches the player nothing.</summary>
        public static Color ButtonDisabled = new Color(0.10f, 0.11f, 0.13f, 0.85f);

        // TINTS, for when ButtonSprite is loaded. The three colours above FILL a flat
        // rectangle; over a sprite the same field MULTIPLIES the painted plate instead, and a
        // 0.13 grey multiplied into art that is already dark navy comes out black. So art gets
        // its own set. Normal sits well below white on purpose: Image.color is packed into a
        // Color32 vertex colour and cannot brighten past 1, so leaving headroom here is the
        // only way the hover has anywhere to go.
        public static Color ButtonTint = new Color(0.72f, 0.76f, 0.86f, 1f);

        public static Color ButtonTintHover = new Color(1f, 0.97f, 0.90f, 1f);

        public static Color ButtonTintDisabled = new Color(0.42f, 0.44f, 0.50f, 0.85f);

        public static Color Label = new Color(0.90f, 0.93f, 0.97f);
        public static Color LabelSelected = new Color(1f, 0.98f, 0.90f);
        public static Color LabelDisabled = new Color(0.42f, 0.45f, 0.50f);

        /// <summary>The small explanatory line under a disabled entry.</summary>
        public static Color Note = new Color(0.52f, 0.55f, 0.61f);

        // ------------------------------------------------------------------- sprites

        // EXTENSION POINT - ART: a null Sprite still means "just use the colour", so a build
        // with the art stripped renders every menu exactly as it did before.
        public static Sprite BackdropSprite;


        /// <summary>The painted button plate, loaded once out of Resources.
        ///
        /// IT IS 9-SLICED, and the numbers below are why the frame does not warp. The border is
        /// set in the importer at 88 px sideways and 137 px top and bottom on an 859x304
        /// texture, which is exactly the plate's corner curvature - so only the flat middle
        /// stretches out to ButtonWidth and the frame keeps the thickness it was drawn with.
        ///
        /// Its import PPU is 400 against the canvas's default 100, so the texture is exactly
        /// 76 canvas units tall - ButtonHeight - and is drawn at its natural vertical scale
        /// rather than squashed. That also sets a FLOOR on ButtonHeight: the two vertical
        /// borders come to 68.5 units, and below that Unity starts overlapping them.</summary>
        public static Sprite ButtonSprite
        {
            get
            {
                return ViewUtil.UiSprite("button_plate");
            }
        }

        // ------------------------------------------------------------------- metrics

        // THE SHAPE OF A MENU BELONGS TO THE LAYOUT, not to this file: a row sized for a
        // mouse on a monitor is too small for a thumb on a phone. The desktop numbers below are
        // the ones that used to be consts here, unchanged. See UiLayout.
        public static float ButtonWidth
        {
            get { return UiLayout.Active.MenuButtonWidth; }
        }
        public static float ButtonHeight
        {
            get { return UiLayout.Active.MenuButtonHeight; }
        }
        public static float ButtonGap
        {
            get { return UiLayout.Active.MenuButtonGap; }
        }

        /// <summary>Width of the highlight bar on the selected row.</summary>
        public static float AccentWidth
        {
            get { return UiLayout.Active.MenuAccentWidth; }
        }

        /// <summary>Gap between the header block and the first button.</summary>
        public static float HeaderGap
        {
            get { return UiLayout.Active.MenuHeaderGap; }
        }

        public static int TitleFontSize
        {
            get { return UiLayout.Active.MenuTitleFont; }
        }
        public static int SubtitleFontSize
        {
            get { return UiLayout.Active.MenuSubtitleFont; }
        }
        public static int LabelFontSize
        {
            get { return UiLayout.Active.MenuLabelFont; }
        }
        public static int NoteFontSize
        {
            get { return UiLayout.Active.MenuNoteFont; }
        }

        /// <summary>The reading screens (how to play, run summary). A page is capped at ~19
        /// lines, and the body box is 620 tall, so this has headroom to grow a little.</summary>
        public static int BodyFontSize
        {
            get { return UiLayout.Active.MenuBodyFont; }
        }
    }
}
