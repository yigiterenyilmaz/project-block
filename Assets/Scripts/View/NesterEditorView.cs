// PURPOSE: The CUT EDITOR ("Neşter"), the weld editor's twin. The card's block is laid out large in
// its own tiles; the player paints the cubes of the FIRST piece (click one, or press and drag across
// several - the first cube pressed decides whether the stroke adds or removes), and everything left
// is the second piece. Each piece wears its own outline round its own silhouette, the edges the
// knife goes through are drawn as a bright SEAM, and the two blocks the cut will make are previewed
// side by side underneath. CUT lights only when the rules would allow it - both pieces non-empty and
// each one holding together (NesterPower.CanCut) - and the line under the grid says why when not.
//
// It replaced a bare toggle grid where the only feedback was a colour change on the clicked cube,
// so the player had to work out the two pieces - and whether they were legal - in their head.
//
// It reads the pointer itself (like the compass picker's hover) because painting needs the frames
// between press and release; the controller only polls TakeResult for CUT / CANCEL.
// Placeholder presentation like everything else under View/.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    /// <summary>The Neşter cut editor.</summary>
    public sealed class NesterEditorView : MonoBehaviour
    {
        public enum Result { None, Confirm, Cancel }

        private const float MaxCell = 0.9f;
        private const float GridSpan = 4.6f;
        private const float PreviewCell = 0.24f;

        private static readonly Color FirstLine = new Color(1f, 0.72f, 0.35f, 0.95f);
        private static readonly Color SecondLine = new Color(0.45f, 0.85f, 1f, 0.9f);
        private static readonly Color SeamColor = new Color(1f, 0.95f, 0.85f, 1f);
        private static readonly Color HoverLine = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color ButtonColor = new Color(0.20f, 0.40f, 0.56f, 1f);
        private static readonly Color ButtonOffColor = new Color(0.14f, 0.15f, 0.18f, 1f);
        private static readonly Color CancelColor = new Color(0.22f, 0.18f, 0.20f, 1f);
        private static readonly Color Muted = new Color(0.66f, 0.72f, 0.80f, 1f);
        private static readonly Color Warn = new Color(1f, 0.55f, 0.5f, 1f);
        private static readonly Vector2 ButtonSize = new Vector2(2.3f, 0.62f);

        public bool IsOpen { get; private set; }

        /// <summary>The cubes of the first piece, in SHAPE offsets - what Core takes.</summary>
        public List<GridPos> PickedCells
        {
            get { return new List<GridPos>(picked); }
        }

        private BlockCard card;
        private BlockShape shape;
        private float cell;
        private Vector2 gridCentre;
        private readonly HashSet<GridPos> picked = new HashSet<GridPos>();
        private readonly Dictionary<GridPos, SpriteRenderer> cubes =
            new Dictionary<GridPos, SpriteRenderer>();
        /// <summary>Each cube's resting scale, and how far into its hover lift it is. The scale
        /// is written from the stored base every frame, never read back off the transform.</summary>
        private readonly Dictionary<GridPos, Vector3> baseScale = new Dictionary<GridPos, Vector3>();
        private readonly Dictionary<GridPos, float> lift = new Dictionary<GridPos, float>();
        private Transform marks;       // outlines + seam, rebuilt on every change
        private Transform preview;     // the two resulting pieces
        private SpriteRenderer hover;
        private SpriteRenderer confirmPlate;
        private TextMesh confirmLabel;
        private TextMesh infoLabel;
        private Vector2 confirmAt;
        private Vector2 cancelAt;
        private float previewY;
        private bool painting;
        private bool paintAdds;
        private Result pending;

        public void Show(BlockCard card, BlockShape shape, string title)
        {
            Hide();
            IsOpen = true;
            this.card = card;
            this.shape = shape;
            cell = Mathf.Min(MaxCell, GridSpan / Mathf.Max(shape.Width, shape.Height));
            gridCentre = new Vector2(0f, 0.55f);
            float halfW = shape.Width * cell * 0.5f;
            float halfH = shape.Height * cell * 0.5f;

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.85f), ViewUtil.MenuDimOrder);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, gridCentre.y + halfH + 0.95f),
                title, 60, 0.05f, Color.white, 46, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "Hint", new Vector2(0f, gridCentre.y + halfH + 0.5f),
                Loc.Pick("click or drag across the cubes of the FIRST piece - the rest is the second",
                    "BİRİNCİ parçanın küplerine tıkla ya da üzerlerinden sürükle - kalanı ikinci parça"),
                60, 0.032f, Muted, 46, TextAnchor.MiddleCenter);
            ViewUtil.MakeRounded(transform, "Panel", gridCentre,
                new Vector2(Mathf.Max(halfW * 2f, 2.4f) + 0.7f, halfH * 2f + 0.7f),
                new Color(0.10f, 0.12f, 0.16f, 0.97f), 41);

            IReadOnlyList<GridPos> cells = shape.Cells;
            bool aligned = ReferenceEquals(shape, card.Shape);
            for (int i = 0; i < cells.Count; i++)
            {
                Color tint;
                Sprite tile = ViewUtil.CardCubeTile(card, shape, i, aligned, out tint);
                SpriteRenderer cube = ViewUtil.MakeCell(transform, "Cube", CellCentre(cells[i]),
                    cell * 0.86f, tint, 43);
                ViewUtil.ApplyTile(cube, tile, cell * 0.9f);
                cubes[cells[i]] = cube;
                baseScale[cells[i]] = cube.transform.localScale;
                lift[cells[i]] = 0f;
            }
            marks = new GameObject("Marks").transform;
            marks.SetParent(transform, false);
            preview = new GameObject("Preview").transform;
            preview.SetParent(transform, false);
            hover = ViewUtil.MakeRounded(transform, "Hover", Vector2.zero,
                new Vector2(cell * 0.96f, cell * 0.96f), HoverLine, 42);
            hover.enabled = false;

            float bottom = gridCentre.y - halfH - 0.35f;
            infoLabel = ViewUtil.MakeText3D(transform, "Info", new Vector2(0f, bottom - 0.25f), "",
                60, 0.034f, Muted, 46, TextAnchor.MiddleCenter);
            previewY = bottom - 0.95f;
            confirmAt = new Vector2(-1.3f, previewY - 0.95f);
            cancelAt = new Vector2(1.3f, previewY - 0.95f);
            confirmPlate = ViewUtil.MakeRounded(transform, "Confirm", confirmAt, ButtonSize,
                ButtonOffColor, 44);
            confirmLabel = ViewUtil.MakeText3D(transform, "ConfirmLabel", confirmAt,
                Loc.Pick("CUT", "KES"), 60, 0.04f, Muted, 46, TextAnchor.MiddleCenter);
            ViewUtil.MakeRounded(transform, "Cancel", cancelAt, ButtonSize, CancelColor, 44);
            ViewUtil.MakeText3D(transform, "CancelLabel", cancelAt, Loc.Pick("CANCEL", "VAZGEÇ"),
                60, 0.04f, Muted, 46, TextAnchor.MiddleCenter);

            float top = gridCentre.y + halfH + 1.3f;
            float low = cancelAt.y - ButtonSize.y * 0.5f - 0.35f;
            float wide = Mathf.Max(Mathf.Max(halfW * 2f, 5.4f) + 0.8f, 8.4f);
            ViewUtil.MakeMenuPlate(transform, new Vector2(0f, (top + low) * 0.5f),
                new Vector2(wide, top - low));
            ViewUtil.FitOverlay(transform, new Vector2(wide + 0.2f, top - low + 0.2f),
                new Vector2(0f, (top + low) * 0.5f));
            Rebuild();
        }

        /// <summary>CUT or CANCEL, once - the controller polls this every frame.</summary>
        public Result TakeResult()
        {
            Result r = pending;
            pending = Result.None;
            return r;
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                && Valid)
            {
                pending = Result.Confirm;
                return;
            }
            if (mouse == null || cam == null)
            {
                return;
            }
            Vector2 local = transform.InverseTransformPoint(cam.ScreenToWorldPoint(
                mouse.position.ReadValue()));
            GridPos? under = CubeUnder(local);

            // Hover: a pale plate under the cube the pointer is on, and the cube lifts a little.
            hover.enabled = under.HasValue;
            float ease = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
            foreach (KeyValuePair<GridPos, SpriteRenderer> entry in cubes)
            {
                bool on = under.HasValue && entry.Key.Equals(under.Value);
                float k = Mathf.Lerp(lift[entry.Key], on ? 1f : 0f, ease);
                lift[entry.Key] = k;
                entry.Value.transform.localScale = baseScale[entry.Key] * (1f + 0.08f * k);
            }
            if (under.HasValue)
            {
                hover.transform.localPosition = CellCentre(under.Value);
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (InBox(local, confirmAt, ButtonSize))
                {
                    if (Valid)
                    {
                        pending = Result.Confirm;
                    }
                    return;
                }
                if (InBox(local, cancelAt, ButtonSize))
                {
                    pending = Result.Cancel;
                    return;
                }
                if (under.HasValue)
                {
                    painting = true;
                    paintAdds = !picked.Contains(under.Value);
                    Paint(under.Value);
                }
            }
            else if (painting && mouse.leftButton.isPressed && under.HasValue)
            {
                Paint(under.Value);
            }
            if (!mouse.leftButton.isPressed)
            {
                painting = false;
            }
        }

        private void Paint(GridPos at)
        {
            bool changed = paintAdds ? picked.Add(at) : picked.Remove(at);
            if (changed)
            {
                Rebuild();
            }
        }

        private bool Valid
        {
            get { return NesterPower.CanCut(shape, PickedCells); }
        }

        /// <summary>Everything that depends on the pick: outlines, seam, preview, button, info.</summary>
        private void Rebuild()
        {
            Clear(marks);
            Clear(preview);
            var second = new HashSet<GridPos>();
            foreach (GridPos c in shape.Cells)
            {
                if (!picked.Contains(c))
                {
                    second.Add(c);
                }
            }
            // A picked cube stands a shade brighter; the rest a shade quieter.
            foreach (KeyValuePair<GridPos, SpriteRenderer> entry in cubes)
            {
                Color c = entry.Value.color;
                float k = picked.Count == 0 || picked.Contains(entry.Key) ? 1f : 0.72f;
                entry.Value.color = new Color(c.r, c.g, c.b, k);
            }
            if (picked.Count > 0)
            {
                Outline(picked, FirstLine, 0.06f);
                Outline(second, SecondLine, 0.045f);
                Seam();
            }
            bool valid = Valid;
            confirmPlate.color = valid ? ButtonColor : ButtonOffColor;
            confirmLabel.color = valid ? Color.white : Muted;
            if (picked.Count == 0)
            {
                infoLabel.text = Loc.Pick("pick the cubes of the first piece",
                    "birinci parçanın küplerini seç");
                infoLabel.color = Muted;
            }
            else if (second.Count == 0)
            {
                infoLabel.text = Loc.Pick("that is the whole block - leave something for the second piece",
                    "bu bütün blok - ikinci parçaya da küp bırak");
                infoLabel.color = Warn;
            }
            else if (!valid)
            {
                infoLabel.text = Loc.Pick("each piece must hold together in one piece",
                    "iki parça da kendi içinde bitişik olmalı");
                infoLabel.color = Warn;
            }
            else
            {
                infoLabel.text = Loc.Pick(picked.Count + " + " + second.Count + " cubes",
                    picked.Count + " + " + second.Count + " küp");
                infoLabel.color = Muted;
                DrawPreview(new List<GridPos>(picked), new List<GridPos>(second));
            }
        }

        /// <summary>The two blocks the cut will make, small, side by side, each in the card's tiles.
        /// </summary>
        private void DrawPreview(List<GridPos> a, List<GridPos> b)
        {
            BlockShape sa = BlockShape.FromCells(a);
            BlockShape sb = BlockShape.FromCells(b);
            float gap = 0.6f;
            float wa = sa.Width * PreviewCell;
            float wb = sb.Width * PreviewCell;
            float left = -(wa + gap + wb) * 0.5f;
            DrawSmall(sa, new Vector2(left + wa * 0.5f, previewY), FirstLine);
            ViewUtil.MakeText3D(preview, "Plus", new Vector2(left + wa + gap * 0.5f, previewY), "+",
                60, 0.05f, Muted, 46, TextAnchor.MiddleCenter);
            DrawSmall(sb, new Vector2(left + wa + gap + wb * 0.5f, previewY), SecondLine);
        }

        private void DrawSmall(BlockShape piece, Vector2 centre, Color edge)
        {
            Color tint;
            Sprite tile = ViewUtil.CardCubeTile(card, shape, 0, false, out tint);
            foreach (GridPos c in piece.Cells)
            {
                var at = centre + new Vector2((c.X - (piece.Width - 1) * 0.5f) * PreviewCell,
                    (c.Y - (piece.Height - 1) * 0.5f) * PreviewCell);
                ViewUtil.MakeCell(preview, "Edge", at, PreviewCell * 1.04f, edge, 44);
                SpriteRenderer cube = ViewUtil.MakeCell(preview, "Mini", at, PreviewCell * 0.86f,
                    tint, 45);
                ViewUtil.ApplyTile(cube, tile, PreviewCell * 0.9f);
            }
        }

        // -------------------------------------------------------------- outlines + seam

        private static readonly GridPos[] Sides =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1)
        };

        /// <summary>One bar along every outer edge of a piece, just inside its silhouette.</summary>
        private void Outline(HashSet<GridPos> set, Color color, float weight)
        {
            float line = cell * weight;
            float inset = cell * 0.47f;
            foreach (GridPos c in set)
            {
                Vector2 middle = CellCentre(c);
                for (int d = 0; d < Sides.Length; d++)
                {
                    if (set.Contains(new GridPos(c.X + Sides[d].X, c.Y + Sides[d].Y)))
                    {
                        continue;
                    }
                    bool across = Sides[d].Y != 0;
                    Vector2 at = middle + new Vector2(Sides[d].X, Sides[d].Y) * inset;
                    ViewUtil.MakeRounded(marks, "Edge", at, across
                        ? new Vector2(cell * 0.96f, line) : new Vector2(line, cell * 0.96f),
                        color, 44);
                }
            }
        }

        /// <summary>THE KNIFE'S PATH: a bright line on every edge between a first-piece cube and a
        /// second-piece cube - exactly where the block comes apart.</summary>
        private void Seam()
        {
            float line = cell * 0.05f;
            foreach (GridPos c in picked)
            {
                for (int d = 0; d < Sides.Length; d++)
                {
                    var n = new GridPos(c.X + Sides[d].X, c.Y + Sides[d].Y);
                    if (picked.Contains(n) || !cubes.ContainsKey(n))
                    {
                        continue;
                    }
                    bool across = Sides[d].Y != 0;
                    Vector2 at = CellCentre(c) + new Vector2(Sides[d].X, Sides[d].Y) * (cell * 0.5f);
                    ViewUtil.MakeRounded(marks, "Seam", at, across
                        ? new Vector2(cell * 0.9f, line) : new Vector2(line, cell * 0.9f),
                        SeamColor, 45);
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        private Vector2 CellCentre(GridPos c)
        {
            return gridCentre + new Vector2((c.X - (shape.Width - 1) * 0.5f) * cell,
                (c.Y - (shape.Height - 1) * 0.5f) * cell);
        }

        private GridPos? CubeUnder(Vector2 local)
        {
            Vector2 p = local - gridCentre;
            int x = Mathf.FloorToInt(p.x / cell + shape.Width * 0.5f);
            int y = Mathf.FloorToInt(p.y / cell + shape.Height * 0.5f);
            var at = new GridPos(x, y);
            return cubes.ContainsKey(at) ? at : (GridPos?)null;
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
            painting = false;
            pending = Result.None;
            picked.Clear();
            cubes.Clear();
            baseScale.Clear();
            lift.Clear();
            marks = null;
            preview = null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
