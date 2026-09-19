// PURPOSE: "Parazit"'s attach panel - ONE screen for the whole bind, instead of three modal
// hops (click a joker on the bar -> the plain deck list -> a numbered square per cube). The old
// flow never showed the three choices together, never said which step it was on beyond a line
// of status text, and the cube pick was grey squares with numbers on them rather than the block.
//
// Three columns, read left to right: WHO rides (the jokers that can), WHICH block (the deck, as
// real cards), WHICH cube (the chosen block drawn large in its own tiles). Every choice stays on
// screen and can be changed in any order; hovering anything lights it, the chosen one of each
// column breathes, and once a joker and a block are both picked the joker's own icon is shown
// ON the cube under the pointer - and then on the chosen one - exactly as the hand and the board
// will draw it afterwards (CardVisual.SetRider / BoardView.SetParasiteRider). CONFIRM only wakes
// up when all three are set.
//
// It decides nothing: the controller hands it the candidates and asks the session to bind.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class ParasiteAttachView : MonoBehaviour
    {
        public enum Action
        {
            None,
            Confirm,
            Cancel
        }

        // ---- layout, in the panel's own units (the root is scaled to fit the camera) ----
        private const float PanelW = 16.2f;
        private const float PanelH = 8.9f;
        private const float HeaderY = 3.25f;
        private const float ColumnTopY = 2.35f;

        private const float JokerX = -6.05f;
        private const float JokerRowW = 3.1f;
        private const float JokerRowH = 0.78f;
        private const float JokerPitch = 0.9f;

        private const float CardsLeft = -4.05f;
        private const int CardColumns = 6;
        private const float CardPitchX = 1.07f;
        private const float CardPitchY = 1.28f;
        private const float CardScale = 0.58f;
        private const int CardRowsVisible = 4;

        private const float CubeX = 5.3f;
        private const float CubeY = -0.15f;
        private const float CubeArea = 3.4f;

        private const float ButtonY = -3.75f;

        // ---- sorting: above the market and the deck overlay (39-47) ----
        // Every layer has an order of its OWN. The first version shared one between the panel,
        // the column plates and the selection pulse, and two sprites on one order draw in
        // whatever sequence Unity registers them - so the selection could come out UNDER the
        // panel, and a click that had worked looked like one that had not.
        private const int DimOrder = 49;
        private const int EdgeOrder = 50;
        private const int PanelOrder = 51;
        private const int ColumnOrder = 52;
        private const int PulseOrder = 53;
        private const int PlateOrder = 54;
        private const int HighlightOrder = 55;
        private const int ItemOrder = 56;
        private const int CardOrder = 57; // a card spans +0..+4, its rider +3..+4
        private const int TextOrder = 66;

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.82f);
        private static readonly Color PanelColor = new Color(0.05f, 0.045f, 0.07f, 1f);
        private static readonly Color PanelEdge = new Color(0.42f, 0.22f, 0.40f);
        private static readonly Color ColumnColor = new Color(0.09f, 0.08f, 0.12f, 1f);
        private static readonly Color RowColor = new Color(0.14f, 0.12f, 0.18f, 1f);
        private static readonly Color RowHover = new Color(0.22f, 0.18f, 0.28f, 1f);
        private static readonly Color RowChosen = new Color(0.34f, 0.17f, 0.32f, 1f);
        private static readonly Color Accent = new Color(0.86f, 0.46f, 0.80f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.36f);
        private static readonly Color TitleColor = new Color(1f, 0.92f, 0.45f);
        private static readonly Color TextColor = new Color(0.88f, 0.88f, 0.93f);
        private static readonly Color DimText = new Color(0.58f, 0.58f, 0.66f);
        private static readonly Color ButtonOn = new Color(0.30f, 0.52f, 0.34f);
        private static readonly Color ButtonOff = new Color(0.20f, 0.20f, 0.24f);
        private static readonly Color CancelColor = new Color(0.42f, 0.20f, 0.22f);

        public bool IsOpen { get; private set; }

        public int SelectedJokerId { get; private set; }
        public int SelectedCardId { get; private set; }
        public int SelectedCell { get; private set; } = -1;

        public bool Ready
        {
            get { return SelectedJokerId != 0 && SelectedCardId != 0 && SelectedCell >= 0; }
        }

        private readonly List<Joker> jokers = new List<Joker>();
        private readonly List<BlockCard> cards = new List<BlockCard>();

        private Transform root;
        private float fit = 1f;

        // Rebuilt on every change - the panel is small and a change is a click.
        private Transform content;
        private readonly List<Rect> jokerRects = new List<Rect>();
        private readonly List<SpriteRenderer> jokerPlates = new List<SpriteRenderer>();
        private readonly List<Rect> cardRects = new List<Rect>();
        private readonly List<int> cardIndexOfRect = new List<int>();
        private readonly List<CardVisual> cardVisuals = new List<CardVisual>();
        private readonly List<Rect> cubeRects = new List<Rect>();
        private readonly List<SpriteRenderer> cubeHalos = new List<SpriteRenderer>();
        private Rect confirmRect;
        private Rect cancelRect;
        private SpriteRenderer confirmPlate;
        private SpriteRenderer cancelPlate;

        private SpriteRenderer jokerPulse;
        private SpriteRenderer cardPulse;
        private SpriteRenderer cubePulse;
        private SpriteRenderer ghostIcon;
        private TextMesh summary;

        private int hoverJoker = -1;
        private int hoverCard = -1;
        private int hoverCube = -1;
        private int scrollRow;

        /// <summary>Opens the panel. <paramref name="candidates"/> are the jokers that may ride
        /// (not Parazit, not already riding); <paramref name="deck"/> the owned cards.</summary>
        public void Show(IReadOnlyList<Joker> candidates, IReadOnlyList<BlockCard> deck)
        {
            Hide();
            IsOpen = true;
            jokers.AddRange(candidates);
            cards.AddRange(deck);
            SelectedJokerId = jokers.Count == 1 ? jokers[0].InstanceId : 0;
            SelectedCardId = 0;
            SelectedCell = -1;
            scrollRow = 0;

            root = new GameObject("ParasitePanel").transform;
            root.SetParent(transform, false);
            Camera cam = Camera.main;
            if (cam != null)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                fit = Mathf.Min(1f, halfW * 2f * 0.96f / (PanelW + 0.4f),
                    halfH * 2f * 0.94f / (PanelH + 0.4f));
                root.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
            }
            root.localScale = new Vector3(fit, fit, 1f);

            ViewUtil.MakeRect(root, "Dim", Vector2.zero, new Vector2(60f, 30f), DimColor, DimOrder);
            ViewUtil.MakeRounded(root, "Edge", Vector2.zero,
                new Vector2(PanelW + 0.16f, PanelH + 0.16f), PanelEdge, EdgeOrder);
            ViewUtil.MakeRounded(root, "Panel", Vector2.zero, new Vector2(PanelW, PanelH),
                PanelColor, PanelOrder);
            ViewUtil.MakeText3D(root, "Title", new Vector2(0f, HeaderY + 0.55f),
                Loc.Pick("PARAZİT  -  BIND A JOKER TO A BLOCK", "PARAZİT  -  BİR JOKERİ BİR BLOĞA BAĞLA"),
                90, 0.034f, TitleColor, TextOrder, TextAnchor.MiddleCenter);
            Rebuild();
        }

        public void Hide()
        {
            IsOpen = false;
            jokers.Clear();
            cards.Clear();
            if (root != null)
            {
                Destroy(root.gameObject);
            }
            root = null;
            content = null;
            ClearLists();
        }

        private void ClearLists()
        {
            jokerRects.Clear();
            jokerPlates.Clear();
            cardRects.Clear();
            cardIndexOfRect.Clear();
            cardVisuals.Clear();
            cubeRects.Clear();
            cubeHalos.Clear();
            jokerPulse = null;
            cardPulse = null;
            cubePulse = null;
            ghostIcon = null;
            summary = null;
        }

        // ------------------------------------------------------------------ input

        /// <summary>One frame of pointer input, in WORLD coordinates. Returns what the player
        /// asked for; selection changes are handled here and need nothing from the caller.</summary>
        public Action Handle(Vector2 world, bool clicked, float scroll)
        {
            if (!IsOpen || root == null)
            {
                return Action.None;
            }
            Vector2 p = ToPanel(world);
            if (Mathf.Abs(scroll) > 0.01f)
            {
                int maxRow = Mathf.Max(0, CardRows() - CardRowsVisible);
                int next = Mathf.Clamp(scrollRow + (scroll > 0f ? -1 : 1), 0, maxRow);
                if (next != scrollRow)
                {
                    scrollRow = next;
                    Rebuild();
                }
            }
            hoverJoker = IndexOf(jokerRects, p);
            int hitCard = IndexOf(cardRects, p);
            hoverCard = hitCard >= 0 ? cardIndexOfRect[hitCard] : -1;
            hoverCube = IndexOf(cubeRects, p);
            if (!clicked)
            {
                return Action.None;
            }
            if (cancelRect.Contains(p))
            {
                return Action.Cancel;
            }
            if (confirmRect.Contains(p))
            {
                return Ready ? Action.Confirm : Action.None;
            }
            if (hoverJoker >= 0)
            {
                SelectedJokerId = jokers[hoverJoker].InstanceId;
                Rebuild();
                return Action.None;
            }
            if (hoverCard >= 0)
            {
                int id = cards[hoverCard].Id;
                if (id != SelectedCardId)
                {
                    SelectedCardId = id;
                    SelectedCell = -1;
                    Rebuild();
                }
                return Action.None;
            }
            if (hoverCube >= 0)
            {
                SelectedCell = hoverCube;
                Rebuild();
                return Action.None;
            }
            if (!new Rect(-PanelW * 0.5f, -PanelH * 0.5f, PanelW, PanelH).Contains(p))
            {
                return Action.Cancel; // a click on the dim outside closes it, like every overlay
            }
            return Action.None;
        }

        private Vector2 ToPanel(Vector2 world)
        {
            Vector2 local = world - (Vector2)root.position;
            return local / Mathf.Max(fit, 0.0001f);
        }

        private static int IndexOf(List<Rect> rects, Vector2 p)
        {
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i].Contains(p))
                {
                    return i;
                }
            }
            return -1;
        }

        private int CardRows()
        {
            return (cards.Count + CardColumns - 1) / CardColumns;
        }

        // ------------------------------------------------------------------ drawing

        private Joker SelectedJoker
        {
            get
            {
                for (int i = 0; i < jokers.Count; i++)
                {
                    if (jokers[i].InstanceId == SelectedJokerId)
                    {
                        return jokers[i];
                    }
                }
                return null;
            }
        }

        private BlockCard SelectedCard
        {
            get
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    if (cards[i].Id == SelectedCardId)
                    {
                        return cards[i];
                    }
                }
                return null;
            }
        }

        private void Rebuild()
        {
            if (content != null)
            {
                Destroy(content.gameObject);
            }
            ClearLists();
            content = new GameObject("Content").transform;
            content.SetParent(root, false);

            BuildJokerColumn();
            BuildCardColumn();
            BuildCubeColumn();
            BuildFooter();
        }

        private void Header(string step, string text, float x, bool done)
        {
            ViewUtil.MakeText3D(content, "Head_" + step, new Vector2(x, HeaderY - 0.1f),
                step + "  " + text, 80, 0.028f, done ? Gold : TextColor, TextOrder,
                TextAnchor.MiddleCenter);
        }

        private void BuildJokerColumn()
        {
            Header("1", Loc.Pick("WHO RIDES", "KİM BİNECEK"), JokerX, SelectedJokerId != 0);
            ViewUtil.MakeRounded(content, "JokerCol", new Vector2(JokerX, -0.35f),
                new Vector2(JokerRowW + 0.3f, 6.2f), ColumnColor, ColumnOrder);
            if (jokers.Count == 0)
            {
                ViewUtil.MakeText3D(content, "NoJokers", new Vector2(JokerX, ColumnTopY - 0.2f),
                    Loc.Pick("no joker\nto bind", "bağlanacak\njoker yok"), 70, 0.026f, DimText,
                    TextOrder, TextAnchor.MiddleCenter);
            }
            for (int i = 0; i < jokers.Count; i++)
            {
                Joker joker = jokers[i];
                var center = new Vector2(JokerX, ColumnTopY - 0.2f - i * JokerPitch);
                var size = new Vector2(JokerRowW, JokerRowH);
                jokerRects.Add(new Rect(center - size * 0.5f, size));
                bool chosen = joker.InstanceId == SelectedJokerId;
                if (chosen)
                {
                    jokerPulse = ViewUtil.MakeRounded(content, "JokerPulse", center,
                        size + new Vector2(0.14f, 0.14f), Accent, PulseOrder);
                }
                jokerPlates.Add(ViewUtil.MakeRounded(content, "JokerRow_" + i, center, size,
                    RowColor, PlateOrder));
                Sprite icon = ViewUtil.JokerIcon(joker.DefId);
                var iconAt = center + new Vector2(-JokerRowW * 0.5f + 0.42f, 0f);
                if (icon != null)
                {
                    ViewUtil.MakeRounded(content, "IconWell_" + i, iconAt, new Vector2(0.62f, 0.62f),
                        new Color(0.06f, 0.05f, 0.08f), ItemOrder);
                    PlaceIcon(content, "Icon_" + i, icon, iconAt, 0.56f, ItemOrder + 1);
                }
                ViewUtil.MakeText3D(content, "Name_" + i, iconAt + new Vector2(0.44f, 0f),
                    joker.DisplayName, 70, 0.024f, chosen ? Gold : TextColor, TextOrder,
                    TextAnchor.MiddleLeft);
            }
        }

        private void BuildCardColumn()
        {
            float midX = CardsLeft + (CardColumns - 1) * CardPitchX * 0.5f;
            Header("2", Loc.Pick("WHICH BLOCK", "HANGİ BLOK"), midX, SelectedCardId != 0);
            ViewUtil.MakeRounded(content, "CardCol", new Vector2(midX, -0.35f),
                new Vector2(CardColumns * CardPitchX + 0.2f, 6.2f), ColumnColor, ColumnOrder);
            int first = scrollRow * CardColumns;
            int last = Mathf.Min(cards.Count, first + CardRowsVisible * CardColumns);
            Joker rider = SelectedJoker;
            Sprite riderIcon = rider != null ? ViewUtil.JokerIcon(rider.DefId) : null;
            for (int i = first; i < last; i++)
            {
                int slot = i - first;
                var center = new Vector2(CardsLeft + (slot % CardColumns) * CardPitchX,
                    ColumnTopY - 0.35f - (slot / CardColumns) * CardPitchY);
                var size = new Vector2(CardVisual.BodyWidth * CardScale + 0.08f,
                    CardVisual.BodyHeight * CardScale + 0.08f);
                cardRects.Add(new Rect(center - size * 0.5f, size));
                cardIndexOfRect.Add(i);
                bool chosen = cards[i].Id == SelectedCardId;
                if (chosen)
                {
                    cardPulse = ViewUtil.MakeRounded(content, "CardPulse", center,
                        size + new Vector2(0.16f, 0.16f), Accent, PulseOrder);
                }
                CardVisual visual = CardVisual.Create(content, "Card_" + cards[i].Id, cards[i],
                    true, false, center, CardOrder);
                visual.SetBaseScale(CardScale);
                if (chosen && SelectedCell >= 0)
                {
                    visual.SetRider(SelectedCell, riderIcon);
                }
                cardVisuals.Add(visual);
            }
            if (CardRows() > CardRowsVisible)
            {
                ViewUtil.MakeText3D(content, "Scroll", new Vector2(midX, -3.2f),
                    Loc.Pick("wheel: more cards  (" + (scrollRow + 1) + "/"
                        + (CardRows() - CardRowsVisible + 1) + ")",
                        "tekerlek: diğer kartlar  (" + (scrollRow + 1) + "/"
                        + (CardRows() - CardRowsVisible + 1) + ")"),
                    60, 0.022f, DimText, TextOrder, TextAnchor.MiddleCenter);
            }
        }

        private void BuildCubeColumn()
        {
            Header("3", Loc.Pick("WHICH CUBE", "HANGİ KÜP"), CubeX, SelectedCell >= 0);
            ViewUtil.MakeRounded(content, "CubeCol", new Vector2(CubeX, -0.35f),
                new Vector2(CubeArea + 0.9f, 6.2f), ColumnColor, ColumnOrder);
            BlockCard card = SelectedCard;
            if (card == null)
            {
                ViewUtil.MakeText3D(content, "PickFirst", new Vector2(CubeX, CubeY),
                    Loc.Pick("pick a block first", "önce bir blok seç"), 70, 0.026f, DimText,
                    TextOrder, TextAnchor.MiddleCenter);
                return;
            }
            BlockShape shape = card.Shape;
            float cell = Mathf.Min(0.95f, CubeArea / Mathf.Max(shape.Width, shape.Height));
            var bottomLeft = new Vector2(CubeX - (shape.Width - 1) * cell * 0.5f,
                CubeY - (shape.Height - 1) * cell * 0.5f);
            Joker rider = SelectedJoker;
            Sprite icon = rider != null ? ViewUtil.JokerIcon(rider.DefId) : null;
            for (int i = 0; i < shape.Cells.Count; i++)
            {
                GridPos c = shape.Cells[i];
                var center = bottomLeft + new Vector2(c.X * cell, c.Y * cell);
                cubeRects.Add(new Rect(center - new Vector2(cell, cell) * 0.5f, new Vector2(cell, cell)));
                SpriteRenderer halo = ViewUtil.MakeRounded(content, "Halo_" + i, center,
                    new Vector2(cell * 1.02f, cell * 1.02f), Gold, PulseOrder);
                halo.enabled = false;
                cubeHalos.Add(halo);
                Color tint;
                Sprite tile = ViewUtil.CardCubeTile(card, shape, i, true, out tint);
                SpriteRenderer cube = ViewUtil.MakeCell(content, "Cube_" + i, center,
                    cell * 0.9f, tint, ItemOrder);
                ViewUtil.ApplyTile(cube, tile, cell * 0.94f);
                if (i == SelectedCell)
                {
                    cubePulse = halo;
                    halo.enabled = true;
                    if (icon != null)
                    {
                        ViewUtil.MakeRounded(content, "RiderWell", center,
                            new Vector2(cell * 0.62f, cell * 0.62f), new Color(0.07f, 0.05f, 0.09f, 0.9f),
                            ItemOrder + 1);
                        PlaceIcon(content, "Rider", icon, center, cell * 0.56f, ItemOrder + 2);
                    }
                }
            }
            // The ghost: the rider's icon following the pointer over the cubes, so the choice is
            // previewed before it is made.
            if (icon != null)
            {
                ghostIcon = PlaceIcon(content, "Ghost", icon, Vector2.zero, cell * 0.5f, ItemOrder + 3);
                ghostIcon.enabled = false;
            }
            ViewUtil.MakeText3D(content, "CubeHint", new Vector2(CubeX, -3.0f),
                Loc.Pick("the joker rides this cube", "joker bu küpe biner"), 60, 0.022f, DimText,
                TextOrder, TextAnchor.MiddleCenter);
        }

        private void BuildFooter()
        {
            Joker rider = SelectedJoker;
            BlockCard card = SelectedCard;
            string text = Ready
                ? Loc.Pick(rider.DisplayName + " rides cube " + (SelectedCell + 1) + " of this block. "
                        + "If that cube breaks - or the block is discarded unplayed - it is gone.",
                    rider.DisplayName + " bu bloğun " + (SelectedCell + 1) + ". küpüne biner. "
                        + "Küp kırılırsa ya da blok oynanmadan ıskartaya giderse joker yok olur.")
                : Loc.Pick("Pick a joker, a block and one of its cubes.",
                    "Bir joker, bir blok ve o bloğun bir küpünü seç.");
            summary = ViewUtil.MakeText3D(content, "Summary", new Vector2(-1.2f, ButtonY),
                text, 64, 0.022f, Ready ? TextColor : DimText, TextOrder, TextAnchor.MiddleCenter);

            var confirmSize = new Vector2(2.2f, 0.72f);
            var confirmAt = new Vector2(PanelW * 0.5f - 1.55f, ButtonY);
            confirmRect = new Rect(confirmAt - confirmSize * 0.5f, confirmSize);
            confirmPlate = ViewUtil.MakeRounded(content, "Confirm", confirmAt, confirmSize,
                Ready ? ButtonOn : ButtonOff, PlateOrder);
            ViewUtil.MakeText3D(content, "ConfirmText", confirmAt, Loc.Pick("BIND", "BAĞLA"),
                80, 0.03f, Ready ? Color.white : DimText, TextOrder, TextAnchor.MiddleCenter);

            var cancelSize = new Vector2(1.7f, 0.72f);
            var cancelAt = new Vector2(-PanelW * 0.5f + 1.3f, ButtonY);
            cancelRect = new Rect(cancelAt - cancelSize * 0.5f, cancelSize);
            cancelPlate = ViewUtil.MakeRounded(content, "Cancel", cancelAt, cancelSize,
                CancelColor, PlateOrder);
            ViewUtil.MakeText3D(content, "CancelText", cancelAt, Loc.Pick("CANCEL", "İPTAL"),
                80, 0.03f, Color.white, TextOrder, TextAnchor.MiddleCenter);
        }

        private static SpriteRenderer PlaceIcon(Transform parent, string name, Sprite icon,
            Vector2 at, float size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(at.x, at.y, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = icon;
            renderer.sortingOrder = order;
            float native = Mathf.Max(icon.bounds.size.x, icon.bounds.size.y, 0.0001f);
            float scale = size / native;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            return renderer;
        }

        // ------------------------------------------------------------------ motion

        private void Update()
        {
            if (!IsOpen || root == null)
            {
                return;
            }
            float t = Time.unscaledTime;
            // The chosen item of each column BREATHES; a hovered one simply lights.
            float breath = 0.55f + 0.45f * Mathf.Sin(t * 4.2f);
            Tint(jokerPulse, Accent, breath);
            Tint(cardPulse, Accent, breath);
            Tint(cubePulse, Gold, 0.5f + 0.5f * breath);

            for (int i = 0; i < jokerPlates.Count; i++)
            {
                bool chosen = jokers[i].InstanceId == SelectedJokerId;
                jokerPlates[i].color = chosen ? RowChosen : i == hoverJoker ? RowHover : RowColor;
            }
            for (int i = 0; i < cardVisuals.Count; i++)
            {
                if (cardVisuals[i] != null)
                {
                    cardVisuals[i].SetHovered(cardIndexOfRect[i] == hoverCard);
                }
            }
            for (int i = 0; i < cubeHalos.Count; i++)
            {
                if (cubeHalos[i] == cubePulse)
                {
                    continue;
                }
                cubeHalos[i].enabled = i == hoverCube;
                if (i == hoverCube)
                {
                    Tint(cubeHalos[i], Gold, 0.45f);
                }
            }
            if (ghostIcon != null)
            {
                bool show = hoverCube >= 0 && hoverCube != SelectedCell;
                ghostIcon.enabled = show;
                if (show)
                {
                    Vector2 c = cubeRects[hoverCube].center;
                    ghostIcon.transform.localPosition = new Vector3(c.x, c.y + 0.04f * Mathf.Sin(t * 6f), 0f);
                    ghostIcon.color = new Color(1f, 1f, 1f, 0.6f);
                }
            }
            if (confirmPlate != null && Ready)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 3.1f);
                confirmPlate.color = Color.Lerp(ButtonOn, ButtonOn * 1.35f, k);
            }
        }

        private static void Tint(SpriteRenderer renderer, Color color, float alpha)
        {
            if (renderer == null)
            {
                return;
            }
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
        }
    }
}
