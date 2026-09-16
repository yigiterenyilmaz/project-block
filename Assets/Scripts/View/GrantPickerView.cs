// PURPOSE: Debug screen (J / P keys) for granting ANY joker or power from its registry,
// replacing the old "next joker in registry order" cycling. Clicking a tile grants it;
// clicking elsewhere or Esc closes without changes. Hovering a tile shows its rules text
// (the tooltip lives in GameUiController, fed through TryGetEntry).
//
// IT SHOWS THE CARDS THEMSELVES, not a list of names. Fifty-two jokers and thirty-five powers
// are a wall of text to read and a wall of pictures to SCAN, and the pictures are the thing the
// player will be looking at in the bar anyway - so this draws the real painted card with the
// real icon in it (Art/Cards + ViewUtil.JokerIcon/PowerIcon), which is also the only honest way
// to find out that an icon is unreadable at card size before shipping it.
//
// A CARD THAT HAS A NAME PLATE CARRIES ITS OWN NAME; a plate-less one (the power card is a frame
// round an icon and nothing else) gets its name printed UNDER the card, on the dim, rather than
// over the painting. Without art of its own it falls back to the tinted name rows this screen
// used to be - the same bargain every other painted thing in the View makes.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Modal registry picker. While open, the controller blocks other input.</summary>
    public sealed class GrantPickerView : MonoBehaviour
    {
        public enum PickerMode
        {
            Jokers = 0,
            Powers = 1
        }

        // ---- the card grid ----
        /// <summary>Nine across puts 52 jokers in six rows, which is the most that still leaves
        /// the cards big enough to tell apart once FitOverlay has scaled the lot to the screen.
        /// </summary>
        private const int CardColumns = 9;

        private const float CardWidth = 1.16f;

        private const float CardGapX = 0.14f;

        /// <summary>Room under each card for the name a plate-less card cannot carry.</summary>
        private const float CardGapY = 0.34f;

        // ---- the fallback rows, for a registry whose card art is missing ----
        private const int Columns = 4;
        private const float TileWidth = 4.3f;
        private const float TileHeight = 0.62f;
        private const float TileSpacingX = 4.45f;
        private const float TileSpacingY = 0.74f;

        private static readonly Color JokerTileColor = new Color(0.30f, 0.22f, 0.40f);
        private static readonly Color PowerTileColor = new Color(0.12f, 0.30f, 0.34f);
        private static readonly Color NameColor = new Color(0.92f, 0.94f, 0.98f);

        private sealed class Entry
        {
            public string DefId;
            public string Name;
            public string Description;
            public Rarity Rarity;
        }

        private readonly List<Vector2> tileCenters = new List<Vector2>();
        private readonly List<Entry> entries = new List<Entry>();

        /// <summary>The hit box of one tile - a card grid and the fallback rows are not the same
        /// shape, so EntryAt cannot assume either.</summary>
        private Vector2 tileSize = new Vector2(TileWidth, TileHeight);

        public bool IsOpen { get; private set; }

        public PickerMode Mode { get; private set; }

        public void ShowJokers()
        {
            var list = new List<Entry>();
            foreach (JokerDefinition definition in JokerRegistry.All)
            {
                list.Add(new Entry
                {
                    DefId = definition.DefId,
                    Name = definition.DisplayName,
                    Description = definition.Description,
                    Rarity = definition.Rarity
                });
            }
            Show(PickerMode.Jokers,
                Loc.Pick("GRANT A JOKER (debug)", "JOKER SEÇ (debug)"), list, JokerTileColor);
        }

        public void ShowPowers()
        {
            var list = new List<Entry>();
            foreach (PowerDefinition definition in PowerRegistry.All)
            {
                list.Add(new Entry
                {
                    DefId = definition.DefId,
                    Name = definition.DisplayName,
                    Description = definition.Description,
                    Rarity = definition.Rarity
                });
            }
            Show(PickerMode.Powers,
                Loc.Pick("GRANT A POWER (debug)", "GÜÇ SEÇ (debug)"), list, PowerTileColor);
        }

        private void Show(PickerMode mode, string title, List<Entry> newEntries, Color tileColor)
        {
            Hide();
            IsOpen = true;
            Mode = mode;
            entries.AddRange(newEntries);
            string art = mode == PickerMode.Powers ? "card_power" : "card_joker";
            Sprite painted = ViewUtil.CardSprite(art);
            if (painted != null)
            {
                ShowCards(title, painted, HeldItemCard.ArtFor(art));
                return;
            }
            ShowRows(title, tileColor);
        }

        /// <summary>
        /// THE REAL CARDS, in a grid.
        ///
        /// Everything about where the icon and the name sit comes from the card's own anatomy
        /// (HeldItemCard.ArtFor), so this screen cannot disagree with the bar about what a card
        /// looks like - and when the art is redrawn, this follows it without being touched.
        /// </summary>
        private void ShowCards(string title, Sprite painted, HeldItemCard.CardArt art)
        {
            Vector2 unit = painted.bounds.size;
            float cardHeight = CardWidth * unit.y / Mathf.Max(unit.x, 0.0001f);
            tileSize = new Vector2(CardWidth, cardHeight);
            float stepX = CardWidth + CardGapX;
            float stepY = cardHeight + CardGapY;
            int rows = (entries.Count + CardColumns - 1) / CardColumns;
            float startY = (rows - 1) * stepY * 0.5f;
            float startX = -(CardColumns - 1) * stepX * 0.5f;
            float top = startY + cardHeight * 0.5f;
            float bottom = startY - (rows - 1) * stepY - cardHeight * 0.5f - CardGapY;

            Backdrop(title, top + 0.55f);
            // Scaled to whatever is visible, never magnified - so the desktop is unchanged and a
            // narrow screen gets the whole grid rather than the top two rows of it.
            ViewUtil.FitOverlay(transform,
                new Vector2(CardColumns * stepX + 0.6f, top - bottom + 1.3f),
                new Vector2(0f, (top + bottom) * 0.5f + 0.15f));

            for (int i = 0; i < entries.Count; i++)
            {
                var center = new Vector2(startX + (i % CardColumns) * stepX,
                    startY - (i / CardColumns) * stepY);
                tileCenters.Add(center);
                Entry entry = entries[i];
                ViewUtil.MakeIcon(transform, "Card_" + i, center,
                    CardWidth / Mathf.Max(unit.x, 0.0001f), Color.white, 41, painted);

                Sprite icon = Mode == PickerMode.Powers
                    ? ViewUtil.PowerIcon(entry.DefId)
                    : ViewUtil.JokerIcon(entry.DefId);
                if (icon != null)
                {
                    float wellWidth = CardWidth * (art.IconRight - art.IconLeft);
                    float wellHeight = cardHeight * (art.IconBottom - art.IconTop);
                    var well = new Vector2(
                        center.x - CardWidth * 0.5f
                            + CardWidth * (art.IconLeft + art.IconRight) * 0.5f,
                        center.y + cardHeight * 0.5f
                            - cardHeight * (art.IconTop + art.IconBottom) * 0.5f);
                    Vector2 size = icon.bounds.size;
                    ViewUtil.MakeIcon(transform, "Icon_" + i, well,
                        Mathf.Min(wellWidth / size.x, wellHeight / size.y), Color.white, 42, icon);
                }

                Color accent = entry.Rarity == Rarity.Common
                    ? NameColor
                    : RarityPalette.Accent(entry.Rarity);
                if (art.HasPlate)
                {
                    // On the plate, in ink - the same colour rule the bar and the shelf use.
                    float plateY = center.y + cardHeight * 0.5f
                        - cardHeight * (art.PlateTop + art.PlateBottom) * 0.5f;
                    float plateX = center.x - CardWidth * 0.5f
                        + CardWidth * (art.PlateLeft + art.PlateRight) * 0.5f;
                    ViewUtil.MakeText3D(transform, "Name_" + i, new Vector2(plateX, plateY),
                        ViewUtil.WrapText(entry.Name, 12), 90, 0.0075f * CardWidth,
                        HeldItemCard.InkOn(accent), 43, TextAnchor.MiddleCenter);
                    continue;
                }
                // No plate: the name goes UNDER the card, on the dim, where it costs the painting
                // nothing. Light rather than ink, because the ground behind it is dark.
                ViewUtil.MakeText3D(transform, "Name_" + i,
                    new Vector2(center.x, center.y - cardHeight * 0.5f - CardGapY * 0.45f),
                    ViewUtil.WrapText(entry.Name, 14), 90, 0.0085f * CardWidth, accent, 43,
                    TextAnchor.MiddleCenter);
            }
        }

        /// <summary>The name rows this screen used to be - what a registry with no card art gets.
        /// </summary>
        private void ShowRows(string title, Color tileColor)
        {
            tileSize = new Vector2(TileWidth, TileHeight);
            int rows = (entries.Count + Columns - 1) / Columns;
            float startY = (rows - 1) * TileSpacingY * 0.5f + 0.2f;
            float startX = -(Columns - 1) * TileSpacingX * 0.5f;

            Backdrop(title, startY + 1.0f);
            float top = startY + 1.0f + 0.4f;
            float bottom = startY - (rows - 1) * TileSpacingY - TileHeight * 0.5f;
            ViewUtil.FitOverlay(transform,
                new Vector2(Columns * TileSpacingX + 0.6f, top - bottom),
                new Vector2(0f, (top + bottom) * 0.5f));

            for (int i = 0; i < entries.Count; i++)
            {
                var center = new Vector2(startX + (i % Columns) * TileSpacingX,
                    startY - (i / Columns) * TileSpacingY);
                tileCenters.Add(center);
                Rarity rarity = entries[i].Rarity;
                ViewUtil.MakeRect(transform, "Tile_" + i, center,
                    new Vector2(TileWidth, TileHeight),
                    RarityPalette.Tint(tileColor, rarity), 41);
                // A tier strip down the tile's left edge, so the list scans by rarity even
                // though the tinted bodies stay close to the kind colour.
                ViewUtil.MakeRect(transform, "Tier_" + i,
                    center + new Vector2(-TileWidth * 0.5f + 0.06f, 0f),
                    new Vector2(0.12f, TileHeight), RarityPalette.Accent(rarity), 42);
                ViewUtil.MakeText3D(transform, "Name_" + i, center, entries[i].Name,
                    90, 0.014f, rarity == Rarity.Common ? NameColor : RarityPalette.Accent(rarity),
                    42, TextAnchor.MiddleCenter);
            }
        }

        private void Backdrop(string title, float titleY)
        {
            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.82f), 40);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, titleY),
                title + Loc.Pick("  -  hover for rules, click to grant, Esc closes",
                    "  -  kurallar için üstüne gel, vermek için tıkla, Esc kapatır"),
                48, 0.06f, Color.white, 41, TextAnchor.MiddleCenter);
        }

        public void Hide()
        {
            IsOpen = false;
            tileCenters.Clear();
            entries.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        /// <summary>Entry index under a world point, or -1. Indexes match the registry
        /// order of whichever registry is on display (see Mode).</summary>
        public int EntryAt(Vector2 world)
        {
            // Local, not world: the grid is scaled and moved to fit the screen.
            Vector2 local = transform.InverseTransformPoint(world);
            for (int i = 0; i < tileCenters.Count; i++)
            {
                if (Mathf.Abs(local.x - tileCenters[i].x) <= tileSize.x * 0.5f
                    && Mathf.Abs(local.y - tileCenters[i].y) <= tileSize.y * 0.5f)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Entry data for the hover tooltip. False when the index is invalid.</summary>
        public bool TryGetEntry(int index, out string defId, out string displayName,
            out string description, out Rarity rarity)
        {
            if (index < 0 || index >= entries.Count)
            {
                defId = null;
                displayName = null;
                description = null;
                rarity = Rarity.Common;
                return false;
            }
            defId = entries[index].DefId;
            displayName = entries[index].Name;
            description = entries[index].Description;
            rarity = entries[index].Rarity;
            return true;
        }
    }
}
