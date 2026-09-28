// PURPOSE: The Batak BET MENU. One tile per bet length on offer (1..BatakPower.MaxBetTurns), each
// showing the turns and the BONUS that bet pays - the table itself, so the player chooses a bet by
// what it is worth rather than by dialling a number and hoping. Picking a tile fills the detail line
// underneath (what the bonus applies to, what sweeping early is worth, and how full the board is
// right now), and a red line says the price of missing it. BET confirms, CANCEL backs out.
//
// It replaced a two-digit tens/ones "locker" that could dial 0-99 and said nothing about the payout.
// Every number here is BatakPower's own (BonusPercentFor, MaxBetTurns) - nothing is worked out in
// the View. It reads the pointer itself for the hover; the controller routes clicks and keys.
// Placeholder presentation like the other View/ modals, on the shared menu plate.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    /// <summary>Modal bet menu. While open, the controller blocks other input.</summary>
    public sealed class BatakBetView : MonoBehaviour
    {
        private const int Columns = 4;
        private static readonly Vector2 TileSize = new Vector2(1.55f, 1.2f);
        private const float TilePitchX = 1.75f;
        private const float TilePitchY = 1.38f;
        private static readonly Vector2 ButtonSize = new Vector2(2.3f, 0.62f);

        private static readonly Color TileColor = new Color(0.17f, 0.21f, 0.28f, 1f);
        private static readonly Color TileHover = new Color(0.24f, 0.30f, 0.40f, 1f);
        private static readonly Color TilePicked = new Color(0.14f, 0.36f, 0.46f, 1f);
        private static readonly Color PickedRim = new Color(0.55f, 0.92f, 1f, 1f);
        private static readonly Color BonusHot = new Color(1f, 0.62f, 0.30f, 1f);
        private static readonly Color BonusCool = new Color(0.95f, 0.86f, 0.55f, 1f);
        private static readonly Color Ink = new Color(0.95f, 0.96f, 1f);
        private static readonly Color Muted = new Color(0.66f, 0.72f, 0.80f, 1f);
        private static readonly Color Warn = new Color(1f, 0.5f, 0.45f, 1f);
        private static readonly Color ConfirmOn = new Color(0.18f, 0.42f, 0.24f);
        private static readonly Color ConfirmOff = new Color(0.14f, 0.15f, 0.18f);
        private static readonly Color CancelColor = new Color(0.22f, 0.18f, 0.20f);

        private sealed class Tile
        {
            public int Turns;
            public Vector2 Center;
            public Transform Root;
            public SpriteRenderer Plate;
            public SpriteRenderer Rim;
            public float Hover;
        }

        private readonly List<Tile> tiles = new List<Tile>();
        private TextMesh detail;
        private TextMesh confirmLabel;
        private SpriteRenderer confirmPlate;
        private Vector2 confirmAt;
        private Vector2 cancelAt;
        private int cubesOnBoard;

        public bool IsOpen { get; private set; }

        /// <summary>The picked bet in turns, or 0 while none is picked.</summary>
        public int Value { get; private set; }

        public void Show(string title, int cubesOnBoard)
        {
            Clear();
            IsOpen = true;
            Value = 0;
            this.cubesOnBoard = cubesOnBoard;
            int count = BatakPower.MaxBetTurns;
            int rows = (count + Columns - 1) / Columns;
            float gridTop = 1.35f;

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.82f), ViewUtil.MenuDimOrder);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, gridTop + 1.25f), title, 60,
                0.05f, Color.white, 44, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "Hint", new Vector2(0f, gridTop + 0.78f),
                Loc.Pick("sweep the board within the turns you bet - the bolder, the bigger the bonus",
                    "tahtayı bahis ettiğin tur içinde temizle - bahis ne kadar cesursa bonus o kadar büyük"),
                60, 0.03f, Muted, 44, TextAnchor.MiddleCenter);

            for (int i = 0; i < count; i++)
            {
                int row = i / Columns;
                int col = i % Columns;
                var at = new Vector2((col - (Columns - 1) * 0.5f) * TilePitchX,
                    gridTop - row * TilePitchY);
                tiles.Add(MakeTile(i + 1, at, count));
            }

            float below = gridTop - (rows - 1) * TilePitchY - TileSize.y * 0.5f;
            detail = ViewUtil.MakeText3D(transform, "Detail", new Vector2(0f, below - 0.55f), "",
                60, 0.032f, Ink, 44, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "Warning", new Vector2(0f, below - 1.15f),
                Loc.Pick("MISS IT AND THE RUN IS LOST", "TUTTURAMAZSAN OYUNU KAYBEDERSİN"),
                60, 0.032f, Warn, 44, TextAnchor.MiddleCenter);
            confirmAt = new Vector2(-1.3f, below - 1.85f);
            cancelAt = new Vector2(1.3f, below - 1.85f);
            confirmPlate = ViewUtil.MakeRounded(transform, "Confirm", confirmAt, ButtonSize,
                ConfirmOff, 43);
            confirmLabel = ViewUtil.MakeText3D(transform, "ConfirmLabel", confirmAt, "", 60, 0.04f,
                Muted, 44, TextAnchor.MiddleCenter);
            ViewUtil.MakeRounded(transform, "Cancel", cancelAt, ButtonSize, CancelColor, 43);
            ViewUtil.MakeText3D(transform, "CancelLabel", cancelAt, Loc.Pick("CANCEL", "VAZGEÇ"),
                60, 0.04f, Muted, 44, TextAnchor.MiddleCenter);

            float top = gridTop + 1.65f;
            float low = cancelAt.y - ButtonSize.y * 0.5f - 0.35f;
            float wide = Columns * TilePitchX + 2.2f;
            ViewUtil.MakeMenuPlate(transform, new Vector2(0f, (top + low) * 0.5f),
                new Vector2(wide, top - low));
            ViewUtil.FitOverlay(transform, new Vector2(wide + 0.2f, top - low + 0.2f),
                new Vector2(0f, (top + low) * 0.5f));
            Refresh();
        }

        private Tile MakeTile(int turns, Vector2 at, int count)
        {
            var go = new GameObject("Bet_" + turns);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            var tile = new Tile { Turns = turns, Center = at, Root = go.transform };
            tile.Rim = ViewUtil.MakeRounded(go.transform, "Rim", Vector2.zero,
                TileSize + new Vector2(0.1f, 0.1f), PickedRim, 41);
            tile.Plate = ViewUtil.MakeRounded(go.transform, "Plate", Vector2.zero, TileSize,
                TileColor, 42);
            ViewUtil.MakeText3D(go.transform, "Turns", new Vector2(0f, 0.27f), turns.ToString(), 90,
                0.055f, Ink, 44, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(go.transform, "Unit", new Vector2(0f, -0.12f),
                Loc.Pick(turns == 1 ? "turn" : "turns", "tur"), 60, 0.028f, Muted, 44,
                TextAnchor.MiddleCenter);
            // The bonus is coloured by how bold the bet is: hot for the short calls, cooler for
            // the safe ones.
            float boldness = count > 1 ? 1f - (turns - 1) / (float)(count - 1) : 1f;
            ViewUtil.MakeText3D(go.transform, "Bonus", new Vector2(0f, -0.4f),
                Loc.Pick("+" + BatakPower.BonusPercentFor(turns) + "%",
                    "+%" + BatakPower.BonusPercentFor(turns)),
                60, 0.036f, Color.Lerp(BonusCool, BonusHot, boldness), 44, TextAnchor.MiddleCenter);
            return tile;
        }

        public void Hide()
        {
            IsOpen = false;
            Clear();
        }

        /// <summary>The bet tile under a world point, in turns, or 0.</summary>
        public int TileAt(Vector2 world)
        {
            Vector2 local = transform.InverseTransformPoint(world);
            for (int i = 0; i < tiles.Count; i++)
            {
                if (Within(local, tiles[i].Center, TileSize))
                {
                    return tiles[i].Turns;
                }
            }
            return 0;
        }

        public void Select(int turns)
        {
            if (turns >= 1 && turns <= BatakPower.MaxBetTurns)
            {
                Value = turns;
                Refresh();
            }
        }

        /// <summary>Arrow keys / the wheel: moves the pick by <paramref name="delta"/> bets.</summary>
        public void Nudge(int delta)
        {
            int start = Value > 0 ? Value : (delta > 0 ? 0 : BatakPower.MaxBetTurns + 1);
            Select(Mathf.Clamp(start + delta, 1, BatakPower.MaxBetTurns));
        }

        public bool ConfirmAt(Vector2 world)
        {
            return Value >= 1 && Within(transform.InverseTransformPoint(world), confirmAt, ButtonSize);
        }

        public bool CancelAt(Vector2 world)
        {
            return Within(transform.InverseTransformPoint(world), cancelAt, ButtonSize);
        }

        private void Refresh()
        {
            if (detail == null)
            {
                return;
            }
            bool picked = Value >= 1;
            confirmPlate.color = picked ? ConfirmOn : ConfirmOff;
            confirmLabel.color = picked ? Ink : Muted;
            confirmLabel.text = picked
                ? Loc.Pick("BET " + Value + (Value == 1 ? " TURN" : " TURNS"), "BAHİS: " + Value + " TUR")
                : Loc.Pick("BET", "BAHİS");
            // The bet only opens on an empty board, so the second line says what "sweep" means
            // here: the arena empty again.
            string board = Loc.Pick("the board is empty now - empty it again in time",
                "tahta şu an boş - zamanında yeniden boşalt");
            if (!picked)
            {
                detail.text = Loc.Pick("pick a bet", "bir bahis seç") + "\n" + board;
                return;
            }
            int percent = BatakPower.BonusPercentFor(Value);
            detail.text = (Value == 1
                ? Loc.Pick("sweep THIS turn: +" + percent + "% of the points you score",
                    "BU tur temizle: kazandığın puana +%" + percent)
                : Loc.Pick("sweep within " + Value + " turns: +" + percent + "% of the points since "
                        + "the bet (sooner pays a share)",
                    Value + " tur içinde temizle: bahisten beri kazandığın puana +%" + percent
                        + " (erken temizlersen payı)"))
                + "\n" + board;
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            int under = 0;
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse != null && cam != null)
            {
                under = TileAt(cam.ScreenToWorldPoint(mouse.position.ReadValue()));
            }
            float ease = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
            for (int i = 0; i < tiles.Count; i++)
            {
                Tile t = tiles[i];
                bool picked = t.Turns == Value;
                t.Hover = Mathf.Lerp(t.Hover, t.Turns == under ? 1f : 0f, ease);
                float s = 1f + 0.06f * t.Hover + (picked ? 0.04f : 0f);
                t.Root.localScale = new Vector3(s, s, 1f);
                t.Plate.color = picked ? TilePicked : Color.Lerp(TileColor, TileHover, t.Hover);
                t.Rim.enabled = picked;
            }
        }

        private void Clear()
        {
            tiles.Clear();
            detail = null;
            confirmLabel = null;
            confirmPlate = null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private static bool Within(Vector2 p, Vector2 center, Vector2 size)
        {
            return Mathf.Abs(p.x - center.x) <= size.x * 0.5f
                && Mathf.Abs(p.y - center.y) <= size.y * 0.5f;
        }
    }
}
