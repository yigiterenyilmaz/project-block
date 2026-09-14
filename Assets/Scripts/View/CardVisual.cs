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

        /// <summary>How much of its slot a mini cube covers, on a painted tile and on the old
        /// flat square. Mirrors BoardView's CubeFill/EmptyFill: a tile has its own frame and
        /// wants the room, a flat square wants the gap around it.</summary>
        private const float MiniTileFill = 0.98f;
        private const float MiniFlatFill = 0.9f;

        private static readonly Color FaceColor = new Color(0.88f, 0.86f, 0.80f);
        private static readonly Color BonusFaceColor = new Color(0.62f, 0.80f, 0.78f);
        private static readonly Color BackInnerColor = new Color(0.15f, 0.19f, 0.31f);

        /// <summary>The line drawn around a card's FACE. The back has always had one - a frame
        /// colour showing past its inner panel - and the face had nothing, so a pale card met
        /// the pale background with no edge at all. It matters more now that the hand is a fan:
        /// what separates two overlapping cards IS this line.</summary>
        private static readonly Color FaceEdgeColor = new Color(0.24f, 0.22f, 0.20f);

        private static readonly Color BonusFaceEdgeColor = new Color(0.13f, 0.30f, 0.29f);

        /// <summary>How thick that line is, per side. Against a 1.35-wide card this reads as a
        /// drawn border rather than a hairline - and in a tight fan it is the only thing telling
        /// two overlapping cards apart, so it has to survive being half covered.</summary>
        private const float FaceEdge = 0.075f;

        // ---- the HELD mark: a card a boss has taken off the table for a turn ----
        // Ice over a card that cannot be played, in the same flat hard-edged language as
        // everything else: a translucent pane across the face, a frame around it, and a band
        // that says so in words. No glow, no icon - the pane is what makes it read as unplayable
        // at a glance and the band is what stops that reading being a guess.
        private static readonly Color FrozenPaneColor = new Color(0.42f, 0.70f, 0.95f, 0.34f);
        private static readonly Color FrozenEdgeColor = new Color(0.62f, 0.86f, 1f);
        private static readonly Color FrozenBandColor = new Color(0.08f, 0.16f, 0.26f, 0.94f);
        private static readonly Color FrozenLabelColor = new Color(0.86f, 0.96f, 1f);

        /// <summary>Thickness of the frame around a held card.</summary>
        private const float FrozenEdge = 0.075f;

        /// <summary>How far a full-width band has to stay inside the card's edge to clear the
        /// rounded corner. Slightly more than the 0.125 radius, so it never touches the curve.</summary>
        private const float BandInset = 0.14f;

        /// <summary>Frame colors card backs cycle through (combined with 6 symbols this
        /// gives 24 distinct backs before repeating).</summary>
        private static readonly Color[] BackFramePalette =
        {
            new Color(0.66f, 0.34f, 0.32f),
            new Color(0.30f, 0.56f, 0.52f),
            new Color(0.68f, 0.57f, 0.30f),
            new Color(0.52f, 0.42f, 0.66f)
        };

        /// <summary>Id of the shown BlockCard, or -1 for face-down/effect cards.</summary>
        public int CardId { get; private set; }

        /// <summary>Whether this card is showing its face. Held visuals are cached and reused, so
        /// the hand layer compares this to decide when a card has to be rebuilt - which is what
        /// makes "Şaşırtmaca" turning one card over actually flip it on screen.</summary>
        public bool FaceUp { get; private set; }

        /// <summary>Index in the hand row (hand cards first, then bonus). -1 when not held.</summary>
        public int SlotIndex = -1;

        /// <summary>Rest position the card returns to after a cancelled drag.</summary>
        public Vector2 HomePosition;

        private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        private readonly List<int> baseOrders = new List<int>();
        private readonly List<Color> baseColors = new List<Color>();
        private readonly List<TextMesh> textMeshes = new List<TextMesh>();
        private readonly List<MeshRenderer> textRenderers = new List<MeshRenderer>();
        private readonly List<int> textBaseOrders = new List<int>();
        private readonly List<Color> textBaseColors = new List<Color>();

        /// <summary>The sorting order the card was built at. The HELD mark is stacked on top of
        /// it, and it is built later than the card is - so the number has to be kept.</summary>
        private int baseOrder;

        /// <summary>The HELD mark's objects, built on first use and then only toggled. Null
        /// until a boss actually seizes this card, which is the usual case.</summary>
        private GameObject frozenMark;

        private Vector2 moveStart;
        private Vector2 moveTarget;
        private float moveTime;
        private float moveDuration = -1f;
        private Action onArrive;

        /// <summary>Builds a card at a position. card may be null for a plain face-down card.</summary>
        public static CardVisual Create(Transform parent, string name, BlockCard card,
            bool faceUp, bool bonusTint, Vector2 position, int sortingOrder)
        {
            return Create(parent, name, card, faceUp, bonusTint, position, sortingOrder, null);
        }

        /// <summary>Variant with an explicit display shape (mechanical rotation / fox
        /// reshape show the card's EFFECTIVE shape instead of its printed one).</summary>
        public static CardVisual Create(Transform parent, string name, BlockCard card,
            bool faceUp, bool bonusTint, Vector2 position, int sortingOrder,
            BlockShape displayShape)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            var visual = go.AddComponent<CardVisual>();
            visual.CardId = card != null && faceUp ? card.Id : -1;
            visual.FaceUp = faceUp;
            visual.HomePosition = position;
            visual.BuildSprites(card, faceUp, bonusTint, sortingOrder, displayShape);
            return visual;
        }

        private void BuildSprites(BlockCard card, bool faceUp, bool bonusTint, int order,
            BlockShape displayShape)
        {
            baseOrder = order;
            var bodySize = new Vector2(BodyWidth, BodyHeight);
            if (faceUp && card != null)
            {
                // Two rounded plates, the way the BACK has always been built: the outer one IS
                // the outline, the inner one is the face sitting FaceEdge inside it.
                //
                // THEY MUST NOT SHARE A SORTING ORDER. Two overlapping sprites on the same order
                // are drawn in whatever sequence Unity happens to register them, so the outline
                // sometimes came out ON TOP of the face and the card was a dark slab with its
                // cubes floating on it. The face is one order up, and everything above it moved
                // up with it - a card now spans [order, order+4] on its face and [order, order+2]
                // on its back.
                Track(ViewUtil.MakeRounded(transform, "BodyEdge", Vector2.zero, bodySize,
                    bonusTint ? BonusFaceEdgeColor : FaceEdgeColor, order), order);
                Track(ViewUtil.MakeRounded(transform, "Body", Vector2.zero,
                    bodySize - new Vector2(FaceEdge * 2f, FaceEdge * 2f),
                    bonusTint ? BonusFaceColor : FaceColor, order + 1), order + 1);
                BlockShape shape = displayShape != null ? displayShape : card.Shape;
                // "Hedefli" is a mark on ONE cube, not a colour for the whole block, so it is
                // skipped when picking the block's body colour - otherwise a plain targeted card
                // would be lime from edge to edge and the mark would be invisible.
                Color miniColor = ViewUtil.ColorForCard(card.Id);
                // The element also decides which painted TILE the mini cubes are drawn on, so
                // it is remembered and not just turned into a colour. "Çark" and "Tilki" have
                // no cube kind of their own and are only ever seen here, in the hand.
                BlockElement? miniElement = null;
                for (int i = 0; i < card.Elements.Count; i++)
                {
                    if (card.Elements[i] != BlockElement.Targeted)
                    {
                        miniColor = ViewUtil.ElementColor(card.Elements[i]);
                        miniElement = card.Elements[i];
                        break;
                    }
                }
                // Which cube carries the target, in the shape actually being drawn - so a rotated
                // or reshaped card shows the mark where the block will really land it.
                int targetCell = card.Has(BlockElement.Targeted) ? card.TargetIndexIn(shape) : -1;
                // A per-cube designed block colours each cube by ITS element. Its per-cube array
                // is aligned to card.Shape.Cells, so only index into it when we draw that shape
                // (not a fox/mechanical displayShape); a plain cube keeps the neutral card colour.
                bool perCube = card.HasPerCubeElements && displayShape == null;
                float mini = Mathf.Min(1.0f / Mathf.Max(shape.Width, shape.Height), 0.28f);
                Vector2 bottomLeft = new Vector2(-shape.Width * mini * 0.5f + mini * 0.5f,
                    -shape.Height * mini * 0.5f + mini * 0.5f);
                IReadOnlyList<GridPos> miniCells = shape.Cells;
                for (int i = 0; i < miniCells.Count; i++)
                {
                    GridPos cell = miniCells[i];
                    Color cubeColor = miniColor;
                    BlockElement? cubeElement = miniElement;
                    if (perCube)
                    {
                        BlockElement? e = card.CellElement(i);
                        cubeColor = e.HasValue
                            ? ViewUtil.ElementColor(e.Value)
                            : ViewUtil.ColorForCard(card.Id);
                        cubeElement = e;
                    }
                    if (i == targetCell)
                    {
                        cubeColor = ViewUtil.ElementColor(BlockElement.Targeted);
                    }
                    // The tile: the bullseye for the marked cube, this cube's own element if it
                    // has one, otherwise whatever the CARD says - which is how a targeted
                    // block's plain cubes get its body tile instead of a default one.
                    Sprite miniTile = i == targetCell
                        ? ViewUtil.CubeTile(CubeKind.Target)
                        : cubeElement.HasValue && perCube
                            ? ViewUtil.CubeTile(cubeElement.Value)
                            : ViewUtil.CubeTile(CubeKind.Normal, card);
                    Color miniTint = ViewUtil.CubeTileColor(miniTile, cubeColor);
                    SpriteRenderer miniCube = ViewUtil.MakeCell(transform, "Mini",
                        bottomLeft + new Vector2(cell.X * mini, cell.Y * mini),
                        mini * MiniFlatFill, miniTint, order + 2);
                    // A painted tile brings its own frame and fills more of its cell than the
                    // inset flat square ever did.
                    ViewUtil.ApplyTile(miniCube, miniTile,
                        mini * (ViewUtil.ArtLoaded ? MiniTileFill : MiniFlatFill));
                    Track(miniCube, order + 2);
                }
                // The top band names the card's TYPE: its element(s), and/or "custom" for a
                // player-designed block ("Karakter oluşturma"). Plain market/deck blocks get none.
                if (card.Elements.Count > 0 || card.IsCustom || card.IsSmuggled
                    || card.AntimatterOf.HasValue)
                {
                    var elementLabels = new List<string>();
                    foreach (BlockElement element in card.Elements)
                    {
                        elementLabels.Add(ViewUtil.ElementLabel(element));
                    }
                    // A custom (player-designed) block is always tagged just "custom", even when
                    // it carries an element - its element still colours the cubes and drives play.
                    // Smuggled goods are tagged above everything else, and a DEFECTIVE one says
                    // so outright: an ordinary-looking card that will not stay on the board has to
                    // be readable in the hand, or the player wastes the turn without knowing why.
                    string bandText = card.AntimatterOf.HasValue
                        ? Loc.Pick("ANTI " + ViewUtil.KindLabel(card.AntimatterOf.Value),
                            "ANTİ " + ViewUtil.KindLabel(card.AntimatterOf.Value))
                        : card.FallsThrough
                        ? Loc.Pick("DEFECTIVE", "DEFOLU")
                        : card.IsSmuggled
                            ? Loc.Pick("smuggled", "kaçak")
                            : card.IsCustom
                                ? Loc.Pick("custom", "özel")
                                : string.Join("+", elementLabels);
                    // A dark band behind plain text - outlines ghost on TextMesh, this doesn't.
                    var bandCenter = new Vector2(0f, BodyHeight * 0.5f - 0.15f);
                    // Inset past the corner radius (0.125) rather than the old 0.05: a band
                    // that ran the full width used to be square with the card, and now it would
                    // hang out of the rounded corner at both ends.
                    Track(ViewUtil.MakeRect(transform, "ElementBand", bandCenter,
                        new Vector2(BodyWidth - BandInset * 2f, 0.22f),
                        new Color(0.1f, 0.11f, 0.14f, 0.92f),
                        order + 3), order + 3);
                    Color labelColor = card.AntimatterOf.HasValue
                        ? ViewUtil.CubeDisplayColor(new Cube(card.AntimatterOf.Value, -1))
                        : card.FallsThrough
                        ? new Color(1f, 0.42f, 0.38f) // defective: a red warning
                        : card.IsSmuggled
                        ? new Color(1f, 0.72f, 0.35f) // sound smuggled goods: a milder amber
                        : card.Elements.Count > 0
                            ? Color.Lerp(ViewUtil.ElementColor(card.Elements[0]), Color.white, 0.4f)
                            : new Color(0.85f, 0.80f, 1f); // custom-only: a bright neutral tag
                    TrackText(ViewUtil.MakeText3D(transform, "ElementLabel", bandCenter,
                        bandText, 90, 0.016f, labelColor, order + 4,
                        TextAnchor.MiddleCenter), order + 4);
                }
            }
            else
            {
                BuildBack(transform, card, order, Track);
            }
        }

        /// <summary>Frame color of a card's back; anonymous backs (id &lt; 0) are neutral.</summary>
        public static Color FrameColorFor(int cardId)
        {
            if (cardId < 0)
            {
                return new Color(0.30f, 0.38f, 0.55f);
            }
            return BackFramePalette[(cardId / 6) % BackFramePalette.Length];
        }

        /// <summary>
        /// Builds a decorated card back (frame + inner + per-card symbol) so cards can be
        /// told apart in the piles. Uses sorting orders [order, order+2]. Also used by
        /// CardLayerView for the pile stacks; track is null there (no fade/boost needed).
        /// Pass a null card for an anonymous back (shuffle fx).
        /// </summary>
        public static void BuildBack(Transform parent, BlockCard card, int order,
            Action<SpriteRenderer, int> track)
        {
            int id = card != null ? card.Id : -1;
            Color frame = FrameColorFor(id);
            Report(track, ViewUtil.MakeRounded(parent, "BackFrame", Vector2.zero,
                new Vector2(BodyWidth, BodyHeight), frame, order), order);
            // The back's frame matches the face's edge (2 x FaceEdge taken off each axis), so
            // the two sides of a card are bordered the same.
            Report(track, ViewUtil.MakeRounded(parent, "BackInner", Vector2.zero,
                new Vector2(BodyWidth - FaceEdge * 2f, BodyHeight - FaceEdge * 2f),
                BackInnerColor, order + 1), order + 1);
            if (id >= 0)
            {
                BuildBackSymbol(parent, id, frame, order + 2, track);
            }
        }

        /// <summary>One of 6 sprite-built symbols, chosen by card id. All sprites of a
        /// symbol share one color and order, so overlap draw order does not matter.</summary>
        private static void BuildBackSymbol(Transform parent, int id, Color color, int order,
            Action<SpriteRenderer, int> track)
        {
            switch (id % 6)
            {
                case 0: // diamond
                    ReportRotated(track, ViewUtil.MakeCell(parent, "Sym", Vector2.zero, 0.4f, color, order), order);
                    break;
                case 1: // plus
                    Report(track, ViewUtil.MakeRect(parent, "Sym", Vector2.zero, new Vector2(0.46f, 0.13f), color, order), order);
                    Report(track, ViewUtil.MakeRect(parent, "Sym", Vector2.zero, new Vector2(0.13f, 0.46f), color, order), order);
                    break;
                case 2: // cross
                    ReportRotated(track, ViewUtil.MakeRect(parent, "Sym", Vector2.zero, new Vector2(0.5f, 0.13f), color, order), order);
                    ReportRotated(track, ViewUtil.MakeRect(parent, "Sym", Vector2.zero, new Vector2(0.13f, 0.5f), color, order), order);
                    break;
                case 3: // square
                    Report(track, ViewUtil.MakeCell(parent, "Sym", Vector2.zero, 0.32f, color, order), order);
                    break;
                case 4: // bars
                    Report(track, ViewUtil.MakeRect(parent, "Sym", new Vector2(0f, 0.12f), new Vector2(0.44f, 0.11f), color, order), order);
                    Report(track, ViewUtil.MakeRect(parent, "Sym", new Vector2(0f, -0.12f), new Vector2(0.44f, 0.11f), color, order), order);
                    break;
                default: // twin diamonds
                    ReportRotated(track, ViewUtil.MakeCell(parent, "Sym", new Vector2(-0.15f, 0f), 0.24f, color, order), order);
                    ReportRotated(track, ViewUtil.MakeCell(parent, "Sym", new Vector2(0.15f, 0f), 0.24f, color, order), order);
                    break;
            }
        }

        private static void Report(Action<SpriteRenderer, int> track, SpriteRenderer renderer, int order)
        {
            if (track != null)
            {
                track(renderer, order);
            }
        }

        private static void ReportRotated(Action<SpriteRenderer, int> track, SpriteRenderer renderer, int order)
        {
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Report(track, renderer, order);
        }

        private void Track(SpriteRenderer renderer, int baseOrder)
        {
            renderers.Add(renderer);
            baseOrders.Add(baseOrder);
            baseColors.Add(renderer.color);
        }

        private void TrackText(TextMesh textMesh, int baseOrder)
        {
            textMeshes.Add(textMesh);
            textRenderers.Add(textMesh.GetComponent<MeshRenderer>());
            textBaseOrders.Add(baseOrder);
            textBaseColors.Add(textMesh.color);
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
            for (int i = 0; i < textMeshes.Count; i++)
            {
                Color color = textBaseColors[i];
                color.a *= Mathf.Clamp01(alpha);
                textMeshes[i].color = color;
            }
        }

        /// <summary>
        /// Marks the card as HELD - seized by the "Alıkoyma" boss, or frozen by "Hazine" - which
        /// is the one thing about a card in hand that the player could not see. The rules already
        /// refused the pick-up (GameUiController.Drag), so before this the card simply did not
        /// answer the mouse and nothing on screen said why.
        ///
        /// Built on first use and then only toggled: most cards are never held, and a card that
        /// is held once is usually held again. The pieces are tracked like every other part of
        /// the card, so the drag fade and the sorting boost reach them too.
        /// </summary>
        public void SetFrozen(bool frozen)
        {
            if (!frozen)
            {
                if (frozenMark != null)
                {
                    frozenMark.SetActive(false);
                }
                return;
            }
            if (frozenMark == null)
            {
                BuildFrozenMark();
                if (flattened)
                {
                    // The mark is built LATE - a boss seizes the card long after it was dealt -
                    // so it misses the flattening the rest of the card already had. Re-applying
                    // is what folds its four new layers into the card's one order.
                    SetFlattenedOrder(flattenedOrder);
                }
            }
            frozenMark.SetActive(true);
        }

        /// <summary>The pane, the frame and the band, once. Orders sit above the card's own
        /// [order, order+4] so the mark is never half-buried by a cube or the element band.</summary>
        private void BuildFrozenMark()
        {
            frozenMark = new GameObject("Held");
            frozenMark.transform.SetParent(transform, false);
            Transform root = frozenMark.transform;

            // A rounded ring rather than four straight bars: bars square off the corners the
            // card no longer has, and would stick out past its silhouette at all four of them.
            // Outer plate IS the frame; the pane sits FrozenEdge inside it.
            Track(ViewUtil.MakeRounded(root, "Frame", Vector2.zero,
                new Vector2(BodyWidth, BodyHeight), FrozenEdgeColor, baseOrder + 5), baseOrder + 5);
            Track(ViewUtil.MakeRounded(root, "Pane", Vector2.zero,
                new Vector2(BodyWidth - FrozenEdge * 2f, BodyHeight - FrozenEdge * 2f),
                FrozenPaneColor, baseOrder + 6), baseOrder + 6);

            // Where the ELEMENT band would sit if this were the top of the card - the same bar,
            // at the other end, so a held elemental block wears both without them colliding.
            var bandCentre = new Vector2(0f, -BodyHeight * 0.5f + 0.15f);
            Track(ViewUtil.MakeRect(root, "Band", bandCentre,
                new Vector2(BodyWidth - BandInset * 2f, 0.22f), FrozenBandColor,
                baseOrder + 7), baseOrder + 7);
            TrackText(ViewUtil.MakeText3D(root, "Label", bandCentre,
                Loc.Pick("HELD", "TUTULDU"), 90, 0.016f, FrozenLabelColor, baseOrder + 8,
                TextAnchor.MiddleCenter), baseOrder + 8);
        }

        /// <summary>Hover highlight for hand/bonus cards - the GROW half of it. The lift out of
        /// the row and the jump to the front of the fan belong to CardLayerView, which is the
        /// only thing that knows where the row is and what else is in it.
        ///
        /// Scale-only here on purpose: position is driven by MoveTo, and a hover that wrote to
        /// it directly would be undone by the next slide.</summary>
        public void SetHovered(bool hovered)
        {
            float scale = hovered ? HoverScale : 1f;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Bigger than the old 1.07: in a fan the neighbours are covering this card's
        /// edges, and the growth is half of what makes it readable again.</summary>
        private const float HoverScale = 1.16f;

        /// <summary>Raises (or resets, with 0) the sorting order of the whole card,
        /// so dragged/flying cards render above resting ones.</summary>
        public void SetSortingBoost(int boost)
        {
            if (flattened)
            {
                // A flattened card has ONE order, so a boost moves that one order (see
                // SetFlattenedOrder). Its internal layering is depth and is already correct.
                SetFlattenedOrder(flattenedOrder + boost);
                return;
            }
            for (int i = 0; i < renderers.Count; i++)
            {
                renderers[i].sortingOrder = baseOrders[i] + boost;
            }
            for (int i = 0; i < textRenderers.Count; i++)
            {
                textRenderers[i].sortingOrder = textBaseOrders[i] + boost;
            }
        }

        /// <summary>
        /// Collapses the card's NINE internal sorting orders into ONE, and separates them by
        /// DEPTH instead - each layer sits a hair nearer the camera than the one below it.
        ///
        /// This is what lets the hand be a real stack. A fan where every card genuinely covers
        /// the one to its left needs each card's whole band of orders above its neighbour's, and
        /// at nine orders a card that is 200-odd orders for a full hand - far more than the space
        /// between the board and the deck overlay. Flattened, a card costs exactly one, so the
        /// whole fan fits in the gap that was already there.
        ///
        /// It works because the camera is ORTHOGRAPHIC and the project's transparency sort mode
        /// is Default, which sorts equal orders by distance along the view axis. Depth costs
        /// nothing visually under an orthographic camera - z does not move a sprite on screen -
        /// so the only thing these offsets do is decide who draws over whom.
        ///
        /// Only the HAND uses this. The deck overlay, the market and the pickers lay their cards
        /// out with room to spare and keep the plain nine-order build.
        /// </summary>
        public void SetFlattenedOrder(int order)
        {
            flattened = true;
            flattenedOrder = order;
            for (int i = 0; i < renderers.Count; i++)
            {
                renderers[i].sortingOrder = order;
                FlattenDepth(renderers[i].transform, baseOrders[i]);
            }
            for (int i = 0; i < textRenderers.Count; i++)
            {
                textRenderers[i].sortingOrder = order;
                FlattenDepth(textMeshes[i].transform, textBaseOrders[i]);
            }
        }

        /// <summary>Puts one layer of a flattened card at its own depth. Nearer the camera is
        /// MORE NEGATIVE z - the 2D camera looks along +z - so a higher internal layer gets a
        /// larger negative offset and draws on top, exactly as its sorting order used to.</summary>
        private void FlattenDepth(Transform layer, int layerOrder)
        {
            Vector3 local = layer.localPosition;
            local.z = -(layerOrder - baseOrder) * FlattenDepthStep;
            layer.localPosition = local;
        }

        /// <summary>Depth between one internal layer and the next. Small enough to be nothing to
        /// the camera, large enough to be an unambiguous ordering.</summary>
        private const float FlattenDepthStep = 0.002f;

        private bool flattened;
        private int flattenedOrder;

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
