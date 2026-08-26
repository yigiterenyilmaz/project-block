// PURPOSE: The BLOCK GALLERY (F4) - every block face in the game on one screen, drawn the way
// the game draws it and animating the way the game animates it.
//
// It exists because the tile table has two keys and neither one tells the whole story: the
// BOARD looks a cube up by CubeKind, the HAND looks a card up by BlockElement, and several
// block types (çark, tilki, hayalet, negatif) live only on the second. Which face a block
// actually wears is therefore the answer to a question nobody can see by reading either list.
// Here both lists are drawn side by side, so a missing tile, a wrong tint or an animation that
// is not running is obvious at a glance.
//
// IT ENUMERATES THE ENUMS. Nothing here names the block types: it walks CubeKind and
// BlockElement, so a kind added to Core shows up in the gallery the day it is added, wearing
// whatever face the table currently gives it - the default tile included. Adding a tile never
// means remembering to add it here too.
//
// It goes through ViewUtil.CubeTile / CubeTileColor / ApplyTile like every other cube in the
// game, so it can only ever show the truth. Presentation only; Core is untouched.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The F4 block catalogue. The controller owns the key and the open state.</summary>
    public sealed class BlockGalleryView : MonoBehaviour
    {
        private static readonly Color BackdropColor = new Color(0.06f, 0.07f, 0.09f, 0.97f);
        private static readonly Color FrameColor = new Color(0.22f, 0.26f, 0.34f);
        private static readonly Color TitleColor = new Color(1f, 0.92f, 0.60f);
        private static readonly Color SectionColor = new Color(0.62f, 0.80f, 1f);
        private static readonly Color LabelColor = new Color(0.80f, 0.85f, 0.92f);
        private static readonly Color FaintColor = new Color(0.50f, 0.56f, 0.66f);
        private static readonly Color MovingColor = new Color(0.55f, 0.95f, 0.70f);
        private static readonly Color SlotColor = new Color(0.12f, 0.14f, 0.18f);

        private const int BackdropOrder = 60;
        private const int SlotOrder = 61;
        private const int TileOrder = 62;
        private const int TextOrder = 63;

        private const float TileSize = 0.86f;
        private const float ColumnPitch = 1.30f;
        private const float RowPitch = 1.58f;
        private const int PerRow = 12;

        public bool IsOpen { get; private set; }

        /// <summary>Where each HAND entry was drawn and which element it stands for, so a
        /// click can be turned back into "give me one of those". Only the hand section is
        /// clickable: a cube kind is something the rules produce, not something you can be
        /// dealt (there is no "ice card"), but every element is a block you can hold.</summary>
        private readonly List<BlockElement> clickableElements = new List<BlockElement>();
        private readonly List<Vector2> clickableAt = new List<Vector2>();

        /// <summary>The element whose tile is under this world point, or null.</summary>
        public BlockElement? ElementAt(Vector2 world)
        {
            for (int i = 0; i < clickableAt.Count; i++)
            {
                Vector2 d = world - clickableAt[i];
                if (Mathf.Abs(d.x) <= TileSize * 0.5f && Mathf.Abs(d.y) <= TileSize * 0.5f)
                {
                    return clickableElements[i];
                }
            }
            return null;
        }

        /// <summary>Line under the title: what the last click did.</summary>
        public void SetStatus(string text)
        {
            if (statusText != null)
            {
                statusText.text = text;
            }
        }

        private TextMesh statusText;

        /// <summary>Draws the whole gallery. Rebuilt on open like every other panel here.</summary>
        public void Show()
        {
            Clear();
            clickableElements.Clear();
            clickableAt.Clear();
            IsOpen = true;

            ViewUtil.MakeRect(transform, "Backdrop", Vector2.zero,
                new Vector2(20f, 12f), BackdropColor, BackdropOrder);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, 4.15f),
                Loc.Pick("BLOCK GALLERY (F4)", "BLOK GALERİSİ (F4)"),
                90, 0.024f, TitleColor, TextOrder, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "Help", new Vector2(0f, 3.80f),
                Loc.Pick(
                    "every face the game can draw - green names animate - "
                        + "CLICK a block below to deal yourself one - F4 or Esc closes",
                    "oyunun çizebildiği her yüz - yeşil isimler oynar - "
                        + "aşağıdan bir bloğa TIKLA, eline gelsin - F4 ya da Esc kapatır"),
                90, 0.0135f, FaintColor, TextOrder, TextAnchor.MiddleCenter);
            statusText = ViewUtil.MakeText3D(transform, "Status", new Vector2(0f, 3.52f),
                string.Empty, 90, 0.0135f, MovingColor, TextOrder, TextAnchor.MiddleCenter);

            // BOARD: what a cube of each kind looks like standing on the arena.
            float y = 2.95f;
            y = DrawSection(y, Loc.Pick("ON THE BOARD - by cube kind", "ALANDA - küp türüne göre"));
            Array kinds = Enum.GetValues(typeof(CubeKind));
            y = DrawKindRows(y, kinds);

            // HAND: what a card of each element looks like in the hand. The two lists overlap
            // and are still both worth drawing - the pair that DISAGREE is the interesting one.
            y -= 0.30f;
            y = DrawSection(y, Loc.Pick("IN THE HAND - by block element",
                "ELDE - blok türüne göre"));
            Array elements = Enum.GetValues(typeof(BlockElement));
            DrawElementRows(y, elements);
        }

        public void Hide()
        {
            Clear();
            IsOpen = false;
        }

        private float DrawSection(float y, string text)
        {
            ViewUtil.MakeText3D(transform, "Section", new Vector2(-8.4f, y), text,
                90, 0.016f, SectionColor, TextOrder, TextAnchor.MiddleLeft);
            ViewUtil.MakeRect(transform, "SectionRule", new Vector2(0f, y - 0.22f),
                new Vector2(17.2f, 0.02f), FrameColor, SlotOrder);
            return y - 0.85f;
        }

        private float DrawKindRows(float y, Array kinds)
        {
            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = (CubeKind)kinds.GetValue(i);
                Sprite tile = ViewUtil.CubeTile(kind);
                Color tint = ViewUtil.CubeTileColor(new Cube(kind, 0), tile);
                DrawSlot(SlotPosition(y, i), tile, tint, kind.ToString());
            }
            return y - RowsFor(kinds.Length) * RowPitch;
        }

        private void DrawElementRows(float y, Array elements)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                var element = (BlockElement)elements.GetValue(i);
                Sprite tile = ViewUtil.CubeTile(element);
                Color tint = ViewUtil.CubeTileColor(tile, ViewUtil.ElementColor(element));
                Vector2 at = SlotPosition(y, i);
                DrawSlot(at, tile, tint, element.ToString());
                clickableElements.Add(element);
                clickableAt.Add(at);
            }
        }

        /// <summary>Where the i'th entry of a section goes: left to right, then down.</summary>
        private static Vector2 SlotPosition(float rowTopY, int index)
        {
            int column = index % PerRow;
            int row = index / PerRow;
            float x = (column - (PerRow - 1) * 0.5f) * ColumnPitch;
            return new Vector2(x, rowTopY - row * RowPitch);
        }

        private static int RowsFor(int count)
        {
            return (count + PerRow - 1) / PerRow;
        }

        /// <summary>One entry: the tile as the game would draw it, on a dark slot, named
        /// underneath. The name turns green when the tile carries a moving material, which is
        /// the only cue that separates "still by design" from "animation not running".</summary>
        private void DrawSlot(Vector2 at, Sprite tile, Color tint, string label)
        {
            ViewUtil.MakeRect(transform, "Slot", at,
                new Vector2(TileSize + 0.16f, TileSize + 0.16f), SlotColor, SlotOrder);
            SpriteRenderer cube = ViewUtil.MakeCell(transform, "Tile", at,
                TileSize, tint, TileOrder);
            ViewUtil.ApplyTile(cube, tile, TileSize);
            cube.color = tint;
            bool moves = ViewUtil.TileMaterial(tile) != null;
            ViewUtil.MakeText3D(transform, "Name", at - new Vector2(0f, TileSize * 0.5f + 0.20f),
                label, 90, 0.0115f, moves ? MovingColor : LabelColor, TextOrder,
                TextAnchor.MiddleCenter);
            // The FILE under the face. Without it a tile that resolved to the wrong sprite -
            // or to no sprite at all - looks exactly like a tile that was never painted, and
            // that is the one failure this screen exists to catch.
            ViewUtil.MakeText3D(transform, "File", at - new Vector2(0f, TileSize * 0.5f + 0.38f),
                tile != null ? tile.name : "-- none --",
                90, 0.0095f, FaintColor, TextOrder, TextAnchor.MiddleCenter);
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
