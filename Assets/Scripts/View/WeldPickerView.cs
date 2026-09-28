// PURPOSE: The WELD EDITOR ("Lehimleme"). A small block editor: the first block sits fixed in the
// middle of a grid, the second follows the pointer as a ghost - green where the rules would weld
// it (touching along at least one side, overlapping nowhere), red where they would not - a click
// lays it down there, and the WELD button confirms. Laid pieces can be moved by clicking again or
// nudged with the arrow keys; nothing is welded until the player confirms.
//
// It replaced a menu that drew every legal offset as its own little diagram and asked the player
// to pick one - which made the player compare a dozen thumbnails instead of simply putting the
// block where they wanted it. Whether a placement is legal is LehimlemePower.CanWeld's answer,
// never worked out here, so the editor and the rules cannot disagree.
// Placeholder presentation like everything else under View/.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The weld editor. The controller feeds it the pointer and clicks.</summary>
    public sealed class WeldPickerView : MonoBehaviour
    {
        /// <summary>What a click on the editor did.</summary>
        public enum ClickResult { Nothing, Placed, Confirm, Cancel }

        private const float MaxCell = 0.62f;
        private const float GridSpan = 5.6f;   // the grid never grows past this, world units

        private static readonly Color GridColor = new Color(0.16f, 0.18f, 0.23f, 1f);
        private static readonly Color LegalColor = new Color(0.35f, 1f, 0.45f, 0.35f);
        private static readonly Color IllegalColor = new Color(1f, 0.35f, 0.35f, 0.35f);
        private static readonly Color ButtonColor = new Color(0.20f, 0.40f, 0.56f, 1f);
        private static readonly Color ButtonOffColor = new Color(0.14f, 0.15f, 0.18f, 1f);
        private static readonly Color CancelColor = new Color(0.22f, 0.18f, 0.20f, 1f);
        private static readonly Color Muted = new Color(0.66f, 0.72f, 0.80f, 1f);

        public bool IsOpen { get; private set; }

        /// <summary>Where the second block has been laid, as the offset Core takes - its origin
        /// relative to the first block's - or null while nothing is laid.</summary>
        public GridPos? PlacedOffset { get; private set; }

        private BlockCard firstCard;
        private BlockCard secondCard;
        private BlockShape first;
        private BlockShape second;
        private int gridW;
        private int gridH;
        private GridPos firstOrigin; // grid coords of the first block's (0,0)
        private float cell;
        private Vector2 gridCentre;

        private Transform placedRoot;
        private Transform ghostRoot;
        private GridPos? ghostOrigin;
        private SpriteRenderer confirmPlate;
        private TextMesh confirmLabel;
        private TextMesh infoLabel;
        private Vector2 confirmAt;
        private Vector2 cancelAt;
        private static readonly Vector2 ButtonSize = new Vector2(2.3f, 0.62f);

        public void Show(BlockCard firstCard, BlockShape first, BlockCard secondCard,
            BlockShape second, string title)
        {
            Hide();
            IsOpen = true;
            this.firstCard = firstCard;
            this.secondCard = secondCard;
            this.first = first;
            this.second = second;
            PlacedOffset = null;
            ghostOrigin = null;

            // Room for the second block on every side of the first.
            gridW = first.Width + second.Width * 2;
            gridH = first.Height + second.Height * 2;
            firstOrigin = new GridPos(second.Width, second.Height);
            cell = Mathf.Min(MaxCell, GridSpan / Mathf.Max(gridW, gridH));
            gridCentre = new Vector2(0f, 0.2f);
            float halfH = gridH * cell * 0.5f;

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.85f), ViewUtil.MenuDimOrder);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, gridCentre.y + halfH + 0.75f),
                title, 60, 0.05f, Color.white, 45, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "Hint", new Vector2(0f, gridCentre.y + halfH + 0.3f),
                Loc.Pick("move the second block against the first - at least one side must touch",
                    "ikinci bloğu birincinin yanına getir - en az bir kenarı değmeli"),
                60, 0.032f, Muted, 45, TextAnchor.MiddleCenter);

            ViewUtil.MakeRounded(transform, "Panel", gridCentre,
                new Vector2(gridW * cell + 0.3f, gridH * cell + 0.3f),
                new Color(0.10f, 0.12f, 0.16f, 0.97f), 41);
            for (int x = 0; x < gridW; x++)
            {
                for (int y = 0; y < gridH; y++)
                {
                    ViewUtil.MakeCell(transform, "Grid", CellCentre(new GridPos(x, y)),
                        cell * 0.92f, GridColor, 42);
                }
            }
            DrawShape(transform, firstCard, first, firstOrigin, 1f, 43);

            placedRoot = new GameObject("Placed").transform;
            placedRoot.SetParent(transform, false);
            ghostRoot = new GameObject("Ghost").transform;
            ghostRoot.SetParent(transform, false);

            float bottom = gridCentre.y - halfH - 0.3f;
            infoLabel = ViewUtil.MakeText3D(transform, "Info", new Vector2(0f, bottom - 0.3f),
                "", 60, 0.034f, Muted, 45, TextAnchor.MiddleCenter);
            confirmAt = new Vector2(-1.3f, bottom - 0.95f);
            cancelAt = new Vector2(1.3f, bottom - 0.95f);
            confirmPlate = ViewUtil.MakeRounded(transform, "Confirm", confirmAt, ButtonSize,
                ButtonOffColor, 44);
            confirmLabel = ViewUtil.MakeText3D(transform, "ConfirmLabel", confirmAt,
                Loc.Pick("WELD", "LEHİMLE"), 60, 0.04f, Muted, 45, TextAnchor.MiddleCenter);
            ViewUtil.MakeRounded(transform, "Cancel", cancelAt, ButtonSize, CancelColor, 44);
            ViewUtil.MakeText3D(transform, "CancelLabel", cancelAt, Loc.Pick("CANCEL", "VAZGEÇ"),
                60, 0.04f, Muted, 45, TextAnchor.MiddleCenter);

            float top = gridCentre.y + halfH + 1.15f;
            float low = cancelAt.y - ButtonSize.y * 0.5f - 0.35f;
            float wide = Mathf.Max(Mathf.Max(gridW * cell, 5.4f) + 0.8f, 8.4f);
            ViewUtil.MakeMenuPlate(transform, new Vector2(0f, (top + low) * 0.5f),
                new Vector2(wide, top - low));
            ViewUtil.FitOverlay(transform, new Vector2(wide + 0.2f, top - low + 0.2f),
                new Vector2(0f, (top + low) * 0.5f));
            RefreshInfo();
        }

        /// <summary>The pointer moved: the ghost follows it, snapped to the grid and centred on
        /// the pointer, tinted with the rules' answer for that spot.</summary>
        public void HoverAt(Vector2 world)
        {
            if (!IsOpen)
            {
                return;
            }
            GridPos? origin = OriginUnder(world);
            if (origin.Equals(ghostOrigin))
            {
                return;
            }
            ghostOrigin = origin;
            Clear(ghostRoot);
            if (!origin.HasValue || (PlacedOffset.HasValue
                && origin.Value.Equals(ToGrid(PlacedOffset.Value))))
            {
                return; // nothing under the pointer, or it is exactly where the block already is
            }
            bool legal = LehimlemePower.CanWeld(first, second, ToOffset(origin.Value));
            foreach (GridPos c in second.Cells)
            {
                ViewUtil.MakeCell(ghostRoot, "GhostCell",
                    CellCentre(new GridPos(origin.Value.X + c.X, origin.Value.Y + c.Y)),
                    cell * 0.98f, legal ? LegalColor : IllegalColor, 44);
            }
            DrawShape(ghostRoot, secondCard, second, origin.Value, 0.55f, 45);
        }

        /// <summary>A click: on the grid it lays the block (where the rules allow), on the
        /// buttons it confirms or cancels. A click anywhere else is nothing.</summary>
        public ClickResult ClickAt(Vector2 world)
        {
            if (!IsOpen)
            {
                return ClickResult.Nothing;
            }
            Vector2 local = transform.InverseTransformPoint(world);
            if (InBox(local, confirmAt, ButtonSize))
            {
                return PlacedOffset.HasValue ? ClickResult.Confirm : ClickResult.Nothing;
            }
            if (InBox(local, cancelAt, ButtonSize))
            {
                return ClickResult.Cancel;
            }
            GridPos? origin = OriginUnder(world);
            if (origin.HasValue && Place(ToOffset(origin.Value)))
            {
                return ClickResult.Placed;
            }
            return ClickResult.Nothing;
        }

        /// <summary>Arrow keys: moves the laid block one cell, if it would still weld there.
        /// With nothing laid yet, it lays the block at the first legal spot it finds.</summary>
        public void Nudge(int dx, int dy)
        {
            if (!IsOpen)
            {
                return;
            }
            if (!PlacedOffset.HasValue)
            {
                for (int x = -second.Width; x <= first.Width; x++)
                {
                    for (int y = -second.Height; y <= first.Height; y++)
                    {
                        if (Place(new GridPos(x, y)))
                        {
                            return;
                        }
                    }
                }
                return;
            }
            GridPos at = PlacedOffset.Value;
            Place(new GridPos(at.X + dx, at.Y + dy));
        }

        private bool Place(GridPos offset)
        {
            if (!LehimlemePower.CanWeld(first, second, offset))
            {
                return false;
            }
            PlacedOffset = offset;
            Clear(placedRoot);
            DrawShape(placedRoot, secondCard, second, ToGrid(offset), 1f, 43);
            ghostOrigin = null; // redraw the ghost against the new placement on the next hover
            Clear(ghostRoot);
            RefreshInfo();
            return true;
        }

        private void RefreshInfo()
        {
            bool ready = PlacedOffset.HasValue;
            confirmPlate.color = ready ? ButtonColor : ButtonOffColor;
            confirmLabel.color = ready ? Color.white : Muted;
            int cubes = first.Size + second.Size;
            infoLabel.text = Loc.Pick(cubes + " cubes  -  the clear that destroys all of it scores +"
                    + LehimlemePower.BonusPercentFor(cubes) + "%",
                cubes + " küp  -  tamamını yok eden patlama +%"
                    + LehimlemePower.BonusPercentFor(cubes) + " puan kazanır");
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>Where the second block's origin goes for a pointer over the grid - chosen so
        /// the pointer sits in the middle of the block - clamped inside the grid; null off it.
        /// </summary>
        private GridPos? OriginUnder(Vector2 world)
        {
            Vector2 local = (Vector2)transform.InverseTransformPoint(world) - gridCentre;
            float halfW = gridW * cell * 0.5f;
            float halfH = gridH * cell * 0.5f;
            if (Mathf.Abs(local.x) > halfW + cell * 0.5f || Mathf.Abs(local.y) > halfH + cell * 0.5f)
            {
                return null;
            }
            float gx = (local.x + halfW) / cell - second.Width * 0.5f;
            float gy = (local.y + halfH) / cell - second.Height * 0.5f;
            int x = Mathf.Clamp(Mathf.RoundToInt(gx), 0, gridW - second.Width);
            int y = Mathf.Clamp(Mathf.RoundToInt(gy), 0, gridH - second.Height);
            return new GridPos(x, y);
        }

        private GridPos ToOffset(GridPos gridOrigin)
        {
            return new GridPos(gridOrigin.X - firstOrigin.X, gridOrigin.Y - firstOrigin.Y);
        }

        private GridPos ToGrid(GridPos offset)
        {
            return new GridPos(offset.X + firstOrigin.X, offset.Y + firstOrigin.Y);
        }

        private Vector2 CellCentre(GridPos g)
        {
            return gridCentre + new Vector2((g.X - (gridW - 1) * 0.5f) * cell,
                (g.Y - (gridH - 1) * 0.5f) * cell);
        }

        /// <summary>A block's cubes in the card's own tiles, the way the hand draws them.</summary>
        private void DrawShape(Transform parent, BlockCard card, BlockShape shape, GridPos origin,
            float alpha, int order)
        {
            IReadOnlyList<GridPos> cells = shape.Cells;
            bool aligned = card != null && ReferenceEquals(shape, card.Shape);
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2 at = CellCentre(new GridPos(origin.X + cells[i].X, origin.Y + cells[i].Y));
                Color tint = Color.white;
                Sprite tile = card != null
                    ? ViewUtil.CardCubeTile(card, shape, i, aligned, out tint)
                    : ViewUtil.DefaultTile;
                tint.a *= alpha;
                SpriteRenderer cube = ViewUtil.MakeCell(parent, "Cube", at, cell * 0.9f, tint,
                    order);
                ViewUtil.ApplyTile(cube, tile, cell * 0.96f);
            }
        }

        private static bool InBox(Vector2 p, Vector2 centre, Vector2 size)
        {
            return Mathf.Abs(p.x - centre.x) <= size.x * 0.5f
                && Mathf.Abs(p.y - centre.y) <= size.y * 0.5f;
        }

        private static void Clear(Transform root)
        {
            if (root == null)
            {
                return;
            }
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }

        public void Hide()
        {
            IsOpen = false;
            PlacedOffset = null;
            ghostOrigin = null;
            placedRoot = null;
            ghostRoot = null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
