// PURPOSE: Debug screen (D key) for picking the starting deck archetype. Clicking a
// deck starts a NEW run with it; clicking elsewhere or Esc closes without changes.
// Each row shows the deck name, its size, and a few deterministic sample shapes.

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

        private readonly List<Vector2> panelCenters = new List<Vector2>();

        public bool IsOpen { get; private set; }

        public void Show(IReadOnlyList<DeckDefinition> decks, DeckDefinition current)
        {
            Hide();
            IsOpen = true;
            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.82f), 40);
            ViewUtil.MakeText3D(transform, "Title",
                new Vector2(0f, decks.Count * PanelSpacing * 0.5f + 0.8f),
                "CHOOSE DECK (starts a new run)", 48, 0.07f, Color.white, 41,
                TextAnchor.MiddleCenter);
            float startY = (decks.Count - 1) * PanelSpacing * 0.5f;
            for (int i = 0; i < decks.Count; i++)
            {
                DeckDefinition deck = decks[i];
                bool isCurrent = deck == current;
                var center = new Vector2(0f, startY - i * PanelSpacing);
                panelCenters.Add(center);
                ViewUtil.MakeRect(transform, "Panel_" + i, center,
                    new Vector2(PanelWidth, PanelHeight),
                    isCurrent ? CurrentPanelColor : PanelColor, 41);
                ViewUtil.MakeText3D(transform, "Name_" + i,
                    center + new Vector2(-PanelWidth * 0.5f + 0.4f, 0f),
                    deck.Name + "  (" + deck.Size + " cards)", 52, 0.07f,
                    isCurrent ? CurrentNameColor : Color.white, 43, TextAnchor.MiddleLeft);
                // deterministic previews so the row always shows the same samples
                var previewRng = new SeededRandom(1000 + i);
                for (int s = 0; s < 4; s++)
                {
                    BlockShape shape = deck.ShapeGenerator.NextShape(previewRng);
                    DrawShapeSample(shape, center + new Vector2(0.7f + s * 1.05f, 0f));
                }
            }
        }

        public void Hide()
        {
            IsOpen = false;
            panelCenters.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        /// <summary>Deck index under a world point, or -1.</summary>
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
