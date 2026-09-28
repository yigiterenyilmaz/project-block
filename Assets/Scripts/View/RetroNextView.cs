// PURPOSE: Tetris mode's NEXT box ("Retro"). A small panel beside the arena's top-right corner that
// shows the piece coming after the one falling now, in the card's own tiles - the preview every
// falling-block game has, so the player can plan a move ahead. Which card is "next" is the
// controller's answer (hand order, right to left, skipping frozen cards); this only draws it.
// Placeholder presentation like everything else under View/.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The NEXT piece preview for tetris mode.</summary>
    public sealed class RetroNextView : MonoBehaviour
    {
        private const float PanelWidth = 2.1f;
        private const float PanelHeight = 2.5f;
        private const float MaxCube = 0.36f;
        private const int Order = 20;

        private int shownCardId = int.MinValue;
        private BlockShape shownShape;
        private Vector2 shownAt;

        /// <summary>Shows <paramref name="card"/> (null for "nothing next") at
        /// <paramref name="anchor"/>, the panel's top-left corner in world space. Rebuilds only when
        /// something changed, so it is cheap to call every frame.</summary>
        public void Show(BlockCard card, BlockShape shape, Vector2 anchor)
        {
            int id = card != null ? card.Id : -1;
            if (id == shownCardId && ReferenceEquals(shape, shownShape) && anchor == shownAt)
            {
                return;
            }
            shownCardId = id;
            shownShape = shape;
            shownAt = anchor;
            Clear();
            Vector2 centre = anchor + new Vector2(PanelWidth * 0.5f, -PanelHeight * 0.5f);
            ViewUtil.MakeRounded(transform, "NextRim", centre,
                new Vector2(PanelWidth + 0.06f, PanelHeight + 0.06f),
                new Color(0.45f, 1f, 0.55f, 0.55f), Order);
            ViewUtil.MakeRounded(transform, "NextPanel", centre,
                new Vector2(PanelWidth, PanelHeight), new Color(0.05f, 0.08f, 0.06f, 0.95f),
                Order + 1);
            ViewUtil.MakeText3D(transform, "NextLabel", centre + new Vector2(0f, PanelHeight * 0.5f - 0.3f),
                Loc.Pick("NEXT", "SIRADAKİ"), 60, 0.04f, new Color(0.55f, 1f, 0.62f), Order + 3,
                TextAnchor.MiddleCenter);
            if (card == null || shape == null)
            {
                ViewUtil.MakeText3D(transform, "NextNone", centre + new Vector2(0f, -0.2f), "-", 60,
                    0.06f, new Color(0.4f, 0.5f, 0.42f), Order + 3, TextAnchor.MiddleCenter);
                return;
            }
            float cube = Mathf.Min(MaxCube, 1.5f / Mathf.Max(shape.Width, shape.Height));
            Vector2 middle = centre + new Vector2(0f, -0.2f);
            IReadOnlyList<GridPos> cells = shape.Cells;
            bool aligned = ReferenceEquals(shape, card.Shape);
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2 at = middle + new Vector2((cells[i].X - (shape.Width - 1) * 0.5f) * cube,
                    (cells[i].Y - (shape.Height - 1) * 0.5f) * cube);
                Color tint;
                Sprite tile = ViewUtil.CardCubeTile(card, shape, i, aligned, out tint);
                SpriteRenderer r = ViewUtil.MakeCell(transform, "NextCube", at, cube * 0.9f, tint,
                    Order + 2);
                ViewUtil.ApplyTile(r, tile, cube * 0.94f);
            }
        }

        public void Hide()
        {
            shownCardId = int.MinValue;
            shownShape = null;
            Clear();
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
