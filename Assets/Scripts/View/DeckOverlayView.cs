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

        public bool IsOpen { get; private set; }

        private readonly List<Vector2> entryCenters = new List<Vector2>();
        private readonly List<BlockShape> entryShapes = new List<BlockShape>();

        /// <summary>Shows the overlay with the given cards (normally the whole owned deck).</summary>
        public void Show(IReadOnlyList<BlockCard> cards)
        {
            Hide();
            IsOpen = true;

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.78f), 40);

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
                CardVisual visual = CardVisual.Create(transform, "Overlay_" + sorted[i].Id,
                    sorted[i], true, false, position, 41);
                visual.transform.localScale = new Vector3(CardScale, CardScale, 1f);
                entryCenters.Add(position);
                entryShapes.Add(sorted[i].Shape);
            }
        }

        /// <summary>The displayed shape under a world point (fox shape picker), or null.</summary>
        public BlockShape ShapeAt(Vector2 world)
        {
            for (int i = 0; i < entryCenters.Count; i++)
            {
                if (Mathf.Abs(world.x - entryCenters[i].x) <= CardVisual.BodyWidth * CardScale * 0.5f
                    && Mathf.Abs(world.y - entryCenters[i].y) <= CardVisual.BodyHeight * CardScale * 0.5f)
                {
                    return entryShapes[i];
                }
            }
            return null;
        }

        public void Hide()
        {
            IsOpen = false;
            entryCenters.Clear();
            entryShapes.Clear();
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
