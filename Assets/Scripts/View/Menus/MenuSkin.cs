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
        /// must not show the leftovers of a run behind it.</summary>
        public static Color Backdrop = new Color(0.04f, 0.05f, 0.08f, 0.97f);

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

        public static Color Label = new Color(0.90f, 0.93f, 0.97f);
        public static Color LabelSelected = new Color(1f, 0.98f, 0.90f);
        public static Color LabelDisabled = new Color(0.42f, 0.45f, 0.50f);

        /// <summary>The small explanatory line under a disabled entry.</summary>
        public static Color Note = new Color(0.52f, 0.55f, 0.61f);

        // ------------------------------------------------------------------- sprites

        // EXTENSION POINT - ART: leave these null until real art lands.
        public static Sprite BackdropSprite;
        public static Sprite ButtonSprite;

        // ------------------------------------------------------------------- metrics

        public const float ButtonWidth = 460f;
        public const float ButtonHeight = 76f;
        public const float ButtonGap = 14f;

        /// <summary>Width of the highlight bar on the selected row.</summary>
        public const float AccentWidth = 7f;

        /// <summary>Gap between the header block and the first button.</summary>
        public const float HeaderGap = 70f;

        public const int TitleFontSize = 68;
        public const int SubtitleFontSize = 24;
        public const int LabelFontSize = 30;
        public const int NoteFontSize = 21;

        /// <summary>The reading screens (how to play, run summary). A page is capped at ~19
        /// lines, and the body box is 620 tall, so this has headroom to grow a little.</summary>
        public const int BodyFontSize = 24;
    }
}
