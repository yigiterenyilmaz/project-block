// PURPOSE: Debug screen (J / P keys) for granting ANY joker or power from its registry,
// replacing the old "next joker in registry order" cycling. Clicking a tile grants it;
// clicking elsewhere or Esc closes without changes. Hovering a tile shows its rules text
// (the tooltip lives in GameUiController, fed through TryGetEntry). Tiles are tinted and
// striped by rarity through RarityPalette, same as the market and the joker/power bars.

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

            int rows = (entries.Count + Columns - 1) / Columns;
            float startY = (rows - 1) * TileSpacingY * 0.5f + 0.2f;
            float startX = -(Columns - 1) * TileSpacingX * 0.5f;

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.82f), 40);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, startY + 1.0f),
                title + Loc.Pick("  -  hover for rules, click to grant, Esc closes",
                    "  -  kurallar için üstüne gel, vermek için tıkla, Esc kapatır"),
                48, 0.06f, Color.white, 41, TextAnchor.MiddleCenter);

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
            for (int i = 0; i < tileCenters.Count; i++)
            {
                if (Mathf.Abs(world.x - tileCenters[i].x) <= TileWidth * 0.5f
                    && Mathf.Abs(world.y - tileCenters[i].y) <= TileHeight * 0.5f)
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
