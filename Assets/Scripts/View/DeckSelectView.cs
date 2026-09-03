// PURPOSE: Debug screen (D key) for picking the starting deck archetype. Clicking a
// deck starts a NEW run with it; clicking elsewhere or Esc closes without changes.
// Each row shows the deck name, its size, and a few deterministic sample shapes.
//
// THE ROWS ARE PAINTED PLATES (Art/Ui/button_plate_light), 9-sliced through ViewUtil.MakePlate
// so one drawing covers a row of any width. This screen is WORLD-SPACE - SpriteRenderers and
// TextMesh, not the canvas the menus use - which is why it slices on the renderer's drawMode
// rather than on a uGUI Image, and why its colours live here instead of in MenuSkin.
//
// The plate is painted a light warm BEIGE, so the colours below stop being fills and become
// TINTS - and a tint is not free to be any colour it likes. A cool blue-grey multiplied into
// beige takes the red down further than the blue and the warmth is simply gone: the first
// pass here used 0.46/0.48/0.55 and turned RGB(168,157,145) into RGB(75,73,77), a flat cold
// grey with 5% saturation. Anything set below must be NEUTRAL OR WARMER than the paint, or it
// is throwing the art away. That freedom in the other direction is what lets the current deck
// read as amber with no second piece of art.
//
// THE HOVER IS A RIM, NOT A BRIGHTER ROW, and that is what lets the beige stay beige. The
// labels are white, which sets a ceiling on how light a row may get: white on the plate as
// drawn is 2.7:1 and unreadable. A hover that works by brightening therefore has to darken
// every other row to leave itself somewhere to go - which is paying for the hover with the
// art. Framing the row instead costs nothing, so the plates keep the tint the paint wants
// (4.2:1 on beige, 4.4:1 on the amber - both read at this size).
//
// The rim is THE SAME PLATE, drawn a little larger and bright, behind the row. Four thin
// rectangles would have been the obvious frame - MarketView uses exactly that - but this
// plate has big rounded corners and a square frame around it reads as a mistake. A 9-sliced
// copy grows only through its middle, so its corners keep their drawn shape and simply move
// apart, and what peeks out is a rounded outline that fits perfectly by construction.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Modal deck picker. While open, the controller blocks other input.</summary>
    public sealed class DeckSelectView : MonoBehaviour
    {
        private const float PanelWidth = 8.5f;
        private const float PanelHeight = 1.7f;
        private const float PanelSpacing = 1.95f;

        private static readonly Color PanelColor = new Color(0.15f, 0.16f, 0.20f);
        private static readonly Color CurrentPanelColor = new Color(0.25f, 0.26f, 0.17f);
        private static readonly Color CurrentNameColor = new Color(1f, 0.92f, 0.45f);
        private static readonly Color SampleColor = new Color(0.75f, 0.78f, 0.85f);

        /// <summary>The emblem's height in world units, and how far right of the row's centre
        /// it sits. The plate's sunken panel is about 1.18 tall and stops 0.47 in from the
        /// right edge, so this clears both without hunting for the frame. It has to clear them
        /// by a VISIBLE margin, not just fit: at 1.15 the emblem came within 0.02 of the panel
        /// and read as crammed into the corner rather than placed in it.</summary>
        private const float EmblemSize = 0.95f;

        private const float EmblemX = 3.05f;

        private static readonly Color HoverPanelColor = new Color(0.31f, 0.37f, 0.48f);

        // ON ART. Multiplied into the painted plate rather than filling a rectangle, so these
        // are not interchangeable with the four above - a 0.15 grey that reads as a dark panel
        // on its own would crush the paint to black. Both are the beige the plate was painted
        // as, give or take: nothing here is dimmed to make room for a hover.
        private static readonly Color PanelTint = new Color(0.80f, 0.78f, 0.74f);

        /// <summary>The deck you are already running, in amber. The plate is beige to begin
        /// with, so this only has to warm it and pull the blue down - which is why the current
        /// row needs no art of its own.</summary>
        private static readonly Color CurrentPanelTint = new Color(0.98f, 0.70f, 0.40f);

        /// <summary>The rim behind the row under the cursor. Warm gold: tried in white first,
        /// which against a beige plate is very nearly no rim at all.</summary>
        private static readonly Color HoverRimColor = new Color(1f, 0.90f, 0.55f);

        /// <summary>How much bigger the rim is than the row, TOTAL across both sides - so it
        /// shows as half this all the way around.</summary>
        private const float HoverRimGrow = 0.16f;

        /// <summary>Deck names on a painted row - white, on every row. The current one is
        /// already marked by its amber, and a second signal in the text would only cost
        /// contrast; the flat-colour screen still picks it out in yellow, because there the
        /// panels are near-black and can afford it.</summary>
        private static readonly Color NameOnTint = Color.white;

        /// <summary>The emblem for a deck, or null for one that has none - and then the row
        /// draws sample shapes out of the deck's own generator, exactly as every row used to.
        /// That fallback is not dead code: it is what a FIFTH deck gets on the day it is added,
        /// before anyone has drawn an emblem for it.
        ///
        /// Matched by REFERENCE against DeckLibrary, not by name and not by position in All.
        /// A name is display text and is one rename away from silently losing its art; an index
        /// is one reordering away from handing Chaos the Classic emblem.</summary>
        private static Sprite DeckEmblem(DeckDefinition deck)
        {
            if (deck == DeckLibrary.Classic)
            {
                return ViewUtil.DeckIcon("deck_classic");
            }
            if (deck == DeckLibrary.SmallBlocks)
            {
                return ViewUtil.DeckIcon("deck_small");
            }
            if (deck == DeckLibrary.BigBlocks)
            {
                return ViewUtil.DeckIcon("deck_big");
            }
            if (deck == DeckLibrary.Chaos)
            {
                return ViewUtil.DeckIcon("deck_chaos");
            }
            return null;
        }

        /// <summary>The row plate, or null when the art is missing - then every row falls back
        /// to the flat rectangles this screen was built on.</summary>
        private static Sprite Plate
        {
            get { return ViewUtil.UiSprite("button_plate_light"); }
        }

        /// <summary>The colour a row is drawn in. ON ART the hover is not in here at all - it
        /// is the rim - so a hovered row keeps its own colour and the current deck stays amber
        /// while the cursor is on it. The flat-colour screen has no rim and still swaps the
        /// fill, where hover outranks current because a near-black panel has nothing else to
        /// say it with.</summary>
        private static Color PanelColorFor(bool isCurrent, bool isHovered)
        {
            if (Plate != null)
            {
                return isCurrent ? CurrentPanelTint : PanelTint;
            }
            if (isHovered)
            {
                return HoverPanelColor;
            }
            return isCurrent ? CurrentPanelColor : PanelColor;
        }

        private readonly List<Vector2> panelCenters = new List<Vector2>();
        private readonly List<SpriteRenderer> panels = new List<SpriteRenderer>();
        private readonly List<bool> panelIsCurrent = new List<bool>();

        /// <summary>One rim, moved to whichever row the cursor is on, rather than one per row -
        /// only ever one is showing. Null when the art is missing: the flat-colour screen
        /// swaps the row's fill instead, and a bright bar behind a rectangle would just be a
        /// bright rectangle.</summary>
        private SpriteRenderer hoverRim;

        private int hovered = -1;

        public bool IsOpen { get; private set; }

        /// <summary>Highlights the deck under the cursor. The picker is world-space, so it gets
        /// its hover from the controller rather than from the canvas hit-test the menus use -
        /// but it must still light up, or it is the one screen that feels dead.</summary>
        public void SetHovered(Vector2 world)
        {
            int index = DeckAt(world);
            if (index == hovered)
            {
                return;
            }
            hovered = index;
            if (hoverRim != null)
            {
                hoverRim.enabled = hovered >= 0;
                if (hovered >= 0)
                {
                    Vector2 c = panelCenters[hovered];
                    hoverRim.transform.localPosition = new Vector3(c.x, c.y, 0f);
                }
            }
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i] == null)
                {
                    continue;
                }
                panels[i].color = PanelColorFor(panelIsCurrent[i], i == hovered);
            }
        }

        public void Show(IReadOnlyList<DeckDefinition> decks, DeckDefinition current)
        {
            Hide();
            IsOpen = true;
            // Lifted off pure black and given the same dark blue the menus use, so this screen
            // and the title screen read as one place rather than two. Still translucent: some
            // of the arena's own gradient coming through is what keeps it from being a slab.
            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0.075f, 0.088f, 0.135f, 0.86f), 39);
            // Between the dim and the rows (41), and parked until a row is hovered. Built once
            // here rather than per row: only one can ever be showing.
            if (Plate != null)
            {
                hoverRim = ViewUtil.MakePlate(transform, "HoverRim", Vector2.zero,
                    new Vector2(PanelWidth + HoverRimGrow, PanelHeight + HoverRimGrow),
                    HoverRimColor, 40, Plate);
                hoverRim.enabled = false;
            }
            ViewUtil.MakeText3D(transform, "Title",
                new Vector2(0f, decks.Count * PanelSpacing * 0.5f + 0.8f),
                Loc.Pick("CHOOSE DECK (starts a new run)", "DESTE SEÇ (yeni oyun başlatır)"),
                48, 0.07f, Color.white, 41, TextAnchor.MiddleCenter);
            float startY = (decks.Count - 1) * PanelSpacing * 0.5f;
            for (int i = 0; i < decks.Count; i++)
            {
                DeckDefinition deck = decks[i];
                bool isCurrent = deck == current;
                var center = new Vector2(0f, startY - i * PanelSpacing);
                panelCenters.Add(center);
                panelIsCurrent.Add(isCurrent);
                panels.Add(ViewUtil.MakePlate(transform, "Panel_" + i, center,
                    new Vector2(PanelWidth, PanelHeight),
                    PanelColorFor(isCurrent, false), 41, Plate));
                ViewUtil.MakeText3D(transform, "Name_" + i,
                    center + new Vector2(-PanelWidth * 0.5f + 0.4f, 0f),
                    deck.Name + Loc.Pick("  (" + deck.Size + " cards)", "  (" + deck.Size + " kart)"),
                    52, 0.07f,
                    Plate != null
                        ? NameOnTint
                        : (isCurrent ? CurrentNameColor : Color.white),
                    43, TextAnchor.MiddleLeft);
                Sprite emblem = DeckEmblem(deck);
                if (emblem != null)
                {
                    ViewUtil.MakeIcon(transform, "Emblem_" + i,
                        center + new Vector2(EmblemX, 0f), EmblemSize, Color.white, 42, emblem);
                }
                else
                {
                    // deterministic previews so the row always shows the same samples
                    var previewRng = new SeededRandom(1000 + i);
                    for (int s = 0; s < 4; s++)
                    {
                        BlockShape shape = deck.ShapeGenerator.NextShape(previewRng);
                        DrawShapeSample(shape, center + new Vector2(0.7f + s * 1.05f, 0f));
                    }
                }
            }
        }

        public void Hide()
        {
            IsOpen = false;
            hovered = -1;
            hoverRim = null;
            panelCenters.Clear();
            panels.Clear();
            panelIsCurrent.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        /// <summary>Deck index under a world point, or -1.</summary>
        /// <summary>How many decks are on offer, and where each panel is - what a gamepad
        /// steps through instead of pointing (see GameUiController.PadPanels.cs).</summary>
        public int DeckCount
        {
            get { return panelCenters.Count; }
        }

        public Vector2? DeckWorldCenter(int index)
        {
            if (index < 0 || index >= panelCenters.Count)
            {
                return null;
            }
            return panelCenters[index];
        }

        public int DeckAt(Vector2 world)
        {
            for (int i = 0; i < panelCenters.Count; i++)
            {
                if (Mathf.Abs(world.x - panelCenters[i].x) <= PanelWidth * 0.5f
                    && Mathf.Abs(world.y - panelCenters[i].y) <= PanelHeight * 0.5f)
                {
                    return i;
                }
            }
            return -1;
        }

        private void DrawShapeSample(BlockShape shape, Vector2 center)
        {
            float cell = Mathf.Min(0.85f / Mathf.Max(shape.Width, shape.Height), 0.24f);
            Vector2 bottomLeft = center
                - new Vector2(shape.Width, shape.Height) * (cell * 0.5f)
                + new Vector2(cell * 0.5f, cell * 0.5f);
            foreach (GridPos c in shape.Cells)
            {
                ViewUtil.MakeCell(transform, "Sample",
                    bottomLeft + new Vector2(c.X * cell, c.Y * cell),
                    cell * 0.9f, SampleColor, 42);
            }
        }
    }
}
