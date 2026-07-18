// PURPOSE: Draws the hand and bonus-hand cards as clickable thumbnails in a row.
// Fully rebuilt on every refresh (cheap at this scale, keeps the code trivial).
// Slot order = hand cards left to right, then bonus cards (teal background).

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Debug renderer + hit-tester for the held cards.</summary>
    public sealed class HandView : MonoBehaviour
    {
        private const float SlotSize = 1.55f;

        private static readonly Color NormalSlotColor = new Color(0.21f, 0.22f, 0.26f);
        private static readonly Color BonusSlotColor = new Color(0.13f, 0.33f, 0.36f);
        private static readonly Color SelectedSlotColor = new Color(0.85f, 0.75f, 0.25f);

        private struct SlotHit
        {
            public Vector2 Center;
            public int Index;
        }

        private readonly List<SlotHit> slotHits = new List<SlotHit>();

        /// <summary>Rebuilds the row. selectedIndex spans hand first, then bonus slots.</summary>
        public void Refresh(RoundEngine round, int selectedIndex, Vector2 center, float spacing)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            slotHits.Clear();

            int handCount = round.Hand.Count;
            int totalCount = handCount + round.BonusHand.Count;
            if (totalCount == 0)
            {
                return;
            }
            float startX = center.x - (totalCount - 1) * spacing * 0.5f;
            for (int i = 0; i < totalCount; i++)
            {
                bool isBonus = i >= handCount;
                BlockCard card = isBonus ? round.BonusHand[i - handCount].Card : round.Hand[i];
                var slotCenter = new Vector2(startX + i * spacing, center.y);

                Color background = i == selectedIndex
                    ? SelectedSlotColor
                    : isBonus ? BonusSlotColor : NormalSlotColor;
                ViewUtil.MakeCell(transform, "SlotBg_" + i, slotCenter, SlotSize, background, 1);

                BlockShape shape = card.Shape;
                float miniCell = Mathf.Min(1.25f / Mathf.Max(shape.Width, shape.Height), 0.32f);
                Vector2 shapeBottomLeft = slotCenter
                    - new Vector2(shape.Width, shape.Height) * (miniCell * 0.5f)
                    + new Vector2(miniCell * 0.5f, miniCell * 0.5f);
                foreach (GridPos cell in shape.Cells)
                {
                    ViewUtil.MakeCell(transform, "Mini_" + i,
                        shapeBottomLeft + new Vector2(cell.X * miniCell, cell.Y * miniCell),
                        miniCell * 0.9f, ViewUtil.ColorForCard(card.Id), 2);
                }

                var hit = new SlotHit();
                hit.Center = slotCenter;
                hit.Index = i;
                slotHits.Add(hit);
            }
        }

        /// <summary>Slot index under a world point, or -1.</summary>
        public int SlotIndexAt(Vector2 world)
        {
            foreach (SlotHit hit in slotHits)
            {
                if (Mathf.Abs(world.x - hit.Center.x) <= SlotSize * 0.5f
                    && Mathf.Abs(world.y - hit.Center.y) <= SlotSize * 0.5f)
                {
                    return hit.Index;
                }
            }
            return -1;
        }
    }
}
