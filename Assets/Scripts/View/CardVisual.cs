// PURPOSE: One card object on screen - a card-shaped body showing its block shape
// (face-up) or a card back (face-down), with a tiny built-in move animation.
// Used for hand/bonus cards, the discard pile's top card, and fly-by effects
// (deals, discards, burns, shuffles). Pure presentation.

using System;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>A single animatable card sprite group.</summary>
    public sealed class CardVisual : MonoBehaviour
    {
        public const float BodyWidth = 1.35f;
        public const float BodyHeight = 1.8f;

        private static readonly Color FaceColor = new Color(0.88f, 0.86f, 0.80f);
        private static readonly Color BonusFaceColor = new Color(0.62f, 0.80f, 0.78f);
        private static readonly Color BackColor = new Color(0.18f, 0.26f, 0.44f);
        private static readonly Color BackInnerColor = new Color(0.26f, 0.36f, 0.56f);

        /// <summary>Id of the shown BlockCard, or -1 for face-down/effect cards.</summary>
        public int CardId { get; private set; }

        /// <summary>Index in the hand row (hand cards first, then bonus). -1 when not held.</summary>
        public int SlotIndex = -1;

        /// <summary>Rest position the card returns to after a cancelled drag.</summary>
        public Vector2 HomePosition;

        private static readonly Color LabelColor = new Color(0.3f, 0.3f, 0.34f);

        private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        private readonly List<int> baseOrders = new List<int>();
        private readonly List<Color> baseColors = new List<Color>();
        private TextMesh idLabel;
        private MeshRenderer idLabelRenderer;
        private int idLabelBaseOrder;

        private Vector2 moveStart;
        private Vector2 moveTarget;
        private float moveTime;
        private float moveDuration = -1f;
        private Action onArrive;

        /// <summary>Builds a card at a position. card may be null for a plain face-down card.</summary>
        public static CardVisual Create(Transform parent, string name, BlockCard card,
            bool faceUp, bool bonusTint, Vector2 position, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            var visual = go.AddComponent<CardVisual>();
            visual.CardId = card != null && faceUp ? card.Id : -1;
            visual.HomePosition = position;
            visual.BuildSprites(card, faceUp, bonusTint, sortingOrder);
            return visual;
        }

        private void BuildSprites(BlockCard card, bool faceUp, bool bonusTint, int order)
        {
            var bodySize = new Vector2(BodyWidth, BodyHeight);
            if (faceUp && card != null)
            {
                Track(ViewUtil.MakeRect(transform, "Body", Vector2.zero, bodySize,
                    bonusTint ? BonusFaceColor : FaceColor, order), order);
                BlockShape shape = card.Shape;
                float mini = Mathf.Min(1.0f / Mathf.Max(shape.Width, shape.Height), 0.28f);
                Vector2 bottomLeft = new Vector2(-shape.Width * mini * 0.5f + mini * 0.5f,
                    -shape.Height * mini * 0.5f + mini * 0.5f);
                foreach (GridPos cell in shape.Cells)
                {
                    Track(ViewUtil.MakeCell(transform, "Mini",
                        bottomLeft + new Vector2(cell.X * mini, cell.Y * mini),
                        mini * 0.9f, ViewUtil.CubeColor, order + 1), order + 1);
                }
                // id stamp so cards can be told apart (colors are uniform on purpose)
                idLabel = ViewUtil.MakeText3D(transform, "IdLabel",
                    new Vector2(BodyWidth * 0.5f - 0.09f, -BodyHeight * 0.5f + 0.04f),
                    "#" + card.Id, 40, 0.045f, LabelColor, order + 2, TextAnchor.LowerRight);
                idLabelRenderer = idLabel.GetComponent<MeshRenderer>();
                idLabelBaseOrder = order + 2;
            }
            else
            {
                Track(ViewUtil.MakeRect(transform, "Back", Vector2.zero, bodySize, BackColor, order), order);
                Track(ViewUtil.MakeRect(transform, "BackInner", Vector2.zero,
                    new Vector2(BodyWidth - 0.24f, BodyHeight - 0.24f), BackInnerColor, order + 1), order + 1);
            }
        }

        private void Track(SpriteRenderer renderer, int baseOrder)
        {
            renderers.Add(renderer);
            baseOrders.Add(baseOrder);
            baseColors.Add(renderer.color);
        }

        /// <summary>Fades the whole card (1 = opaque). Used while dragging so the board
        /// and placement preview stay visible underneath.</summary>
        public void SetAlpha(float alpha)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                Color color = baseColors[i];
                color.a *= Mathf.Clamp01(alpha);
                renderers[i].color = color;
            }
            if (idLabel != null)
            {
                Color labelColor = LabelColor;
                labelColor.a *= Mathf.Clamp01(alpha);
                idLabel.color = labelColor;
            }
        }

        /// <summary>Raises (or resets, with 0) the sorting order of the whole card,
        /// so dragged/flying cards render above resting ones.</summary>
        public void SetSortingBoost(int boost)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                renderers[i].sortingOrder = baseOrders[i] + boost;
            }
            if (idLabelRenderer != null)
            {
                idLabelRenderer.sortingOrder = idLabelBaseOrder + boost;
            }
        }

        /// <summary>Eased slide to a local position; optional callback on arrival.</summary>
        public void MoveTo(Vector2 target, float duration, Action onArriveCallback)
        {
            moveStart = transform.localPosition;
            moveTarget = target;
            moveTime = 0f;
            moveDuration = Mathf.Max(0.01f, duration);
            onArrive = onArriveCallback;
        }

        /// <summary>Cancels any animation and teleports (used while dragging).</summary>
        public void SnapTo(Vector2 position)
        {
            moveDuration = -1f;
            onArrive = null;
            transform.localPosition = new Vector3(position.x, position.y, 0f);
        }

        /// <summary>Slides to a position and destroys itself there (discard/burn/shuffle FX).</summary>
        public void FlyToAndDestroy(Vector2 target, float duration)
        {
            MoveTo(target, duration, DestroySelf);
        }

        private void DestroySelf()
        {
            Destroy(gameObject);
        }

        private void Update()
        {
            if (moveDuration <= 0f)
            {
                return;
            }
            moveTime += Time.deltaTime;
            float t = Mathf.Clamp01(moveTime / moveDuration);
            float eased = 1f - (1f - t) * (1f - t);
            transform.localPosition = Vector2.LerpUnclamped(moveStart, moveTarget, eased);
            if (t >= 1f)
            {
                moveDuration = -1f;
                Action callback = onArrive;
                onArrive = null;
                if (callback != null)
                {
                    callback();
                }
            }
        }
    }
}
