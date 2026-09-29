// PURPOSE: GameUiController's half of "Öteki dünya" - the SECOND board and its own hand.
//
// It is all runtime construction, like the rest of View: a second BoardView object and a strip
// of small card sprites for the mirror hand. Nothing in the scene changes.
//
// LAYOUT (reworked 2026-09-29). With one world the board keeps the position it always had. The
// moment a mirror opens the two worlds stand SIDE BY SIDE - the main world on the left, the
// mirror on the right - both shrunk to fit between the power/info column and the joker bar, with
// the mirror's own hand as a row of real block faces UNDER the mirror board. It used to stack the
// mirror BELOW the main world at fixed coordinates, which ran the mirror board into the player's
// hand and drew the mirror hand underneath the hand cards, where it could barely be seen or
// clicked. Everything is now solved from UiLayout.Active, so portrait gets the same arrangement
// inside its own board box.
//
// Each world carries a small name plate over it, and the world that target-less jokers and powers
// will hit ([W]) is lit, so "which board does this go to" is never a line of HUD text away.
//
// THE TURN. The mirror's card is BOOKED, not played: clicking a mirror hand card selects it,
// clicking a cell on the mirror board stages it there, and the turn resolves when the main
// world plays. That mirrors the engine exactly (RoundEngine.Mirror). When the main world is
// stuck, [M] resolves the turn with the mirror alone.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>Board size and centre for each world. With no mirror the main board keeps
        /// exactly the geometry it always had, so an ordinary round is pixel-identical.</summary>
        /// <summary>The gap between the two worlds, in world units.</summary>
        private const float MirrorWorldGap = 0.45f;

        /// <summary>
        /// The box the two worlds share. On the desktop it runs from just right of the info column
        /// to just left of the joker bar (both measured off the canvas layout), which is wider
        /// than the single board's box and off-centre - the joker bar is narrower than the power
        /// and info columns together. In portrait it is simply the board's own box.
        /// </summary>
        private static void MirrorRegion(out float left, out float right, out float top, out float bottom)
        {
            UiLayout layout = UiLayout.Active;
            float half = layout.BoardWorldSize * 0.5f;
            top = layout.BoardCenter.y + half;
            bottom = layout.BoardCenter.y - half;
            if (layout.IsPortrait)
            {
                left = layout.BoardCenter.x - half;
                right = layout.BoardCenter.x + half;
                return;
            }
            float perPixel = layout.HalfWidth * 2f / Mathf.Max(1f, layout.CanvasReference.x);
            left = -layout.HalfWidth + (layout.InfoLeft + layout.InfoWidth + 12f) * perPixel;
            float jokers = layout.JokerColumns * layout.JokerPanel.x
                + (layout.JokerColumns - 1) * layout.JokerGap + layout.CornerInset + 12f;
            right = layout.HalfWidth - jokers * perPixel;
            left = Mathf.Min(left, layout.BoardCenter.x - half);
        }

        /// <summary>One world's board size while two are open.</summary>
        private static float MirrorBoardWorldSize
        {
            get
            {
                float left, right, top, bottom;
                MirrorRegion(out left, out right, out top, out bottom);
                float byWidth = (right - left - MirrorWorldGap) * 0.5f;
                // Leave room under the boards for the mirror hand.
                float byHeight = (top - bottom) - MirrorHandBand;
                return Mathf.Max(1f, Mathf.Min(byWidth, byHeight));
            }
        }

        /// <summary>Height kept under the two boards for the mirror hand strip.</summary>
        private static float MirrorHandBand
        {
            get { return UiLayout.Active.IsPortrait ? 1.2f : 1.05f; }
        }

        private static float MirrorBoardsY
        {
            get
            {
                float left, right, top, bottom;
                MirrorRegion(out left, out right, out top, out bottom);
                return top - MirrorBoardWorldSize * 0.5f;
            }
        }

        private static Vector2 MainWorldCenter
        {
            get
            {
                float left, right, top, bottom;
                MirrorRegion(out left, out right, out top, out bottom);
                float mid = (left + right) * 0.5f;
                return new Vector2(mid - (MirrorBoardWorldSize + MirrorWorldGap) * 0.5f, MirrorBoardsY);
            }
        }

        private static Vector2 MirrorWorldCenter
        {
            get
            {
                float left, right, top, bottom;
                MirrorRegion(out left, out right, out top, out bottom);
                float mid = (left + right) * 0.5f;
                return new Vector2(mid + (MirrorBoardWorldSize + MirrorWorldGap) * 0.5f, MirrorBoardsY);
            }
        }

        /// <summary>The mirror hand strip's centre line.</summary>
        private static float MirrorHandY
        {
            get { return MirrorBoardsY - MirrorBoardWorldSize * 0.5f - MirrorHandBand * 0.5f; }
        }

        /// <summary>Slot pitch for the mirror hand, fitted to the mirror board's width.</summary>
        private static float MirrorSlotWidth(int count)
        {
            return Mathf.Min(1.2f, (MirrorBoardWorldSize + 0.6f) / Mathf.Max(1, count));
        }

        private static readonly Color MirrorCardColor = new Color(0.62f, 0.75f, 1f, 0.95f);
        private static readonly Color MirrorPickedColor = new Color(1f, 0.92f, 0.45f, 1f);
        private static readonly Color MirrorStagedColor = new Color(0.45f, 1f, 0.55f, 1f);

        private BoardView mirrorBoardView;

        /// <summary>The two name plates and the lit frame round the world [W] aims at.</summary>
        private TextMesh mainWorldLabel;
        private TextMesh mirrorWorldLabel;
        private SpriteRenderer mainWorldFrame;
        private SpriteRenderer mirrorWorldFrame;
        private readonly List<SpriteRenderer> mirrorSlotPlates = new List<SpriteRenderer>();

        private static readonly Color WorldLabelColor = new Color(0.86f, 0.84f, 0.95f, 0.85f);
        private static readonly Color MirrorLabelColor = new Color(0.66f, 0.8f, 1f, 0.9f);
        private static readonly Color AimedFrameColor = new Color(1f, 0.86f, 0.45f, 0.5f);

        /// <summary>Board size the main world was last built at, so a world opening or closing
        /// rebuilds it even though the GameBoard object did not change.</summary>
        private float lastMainBoardSize = -1f;

        /// <summary>Where the board was last built. Part of the rebuild test in RefreshAll: the
        /// two layouts use the same 6.5-unit box but put the board in different places.</summary>
        private Vector2 lastMainBoardCenter = new Vector2(float.NaN, float.NaN);
        private readonly List<SpriteRenderer> mirrorHandSprites = new List<SpriteRenderer>();
        private readonly List<GameObject> mirrorHandRoots = new List<GameObject>();

        /// <summary>Index into MirrorHand the player has picked up, or -1.</summary>
        private int mirrorPickedIndex = -1;

        /// <summary>Which world a joker or power with NO board target goes to. Toggled with [W]
        /// and meaningless while only one world exists. An effect the player POINTS at a cell
        /// ignores this - where you clicked already says which world you meant.</summary>
        private bool effectsOnMirror;

        /// <summary>Aims a target-less activation at the world the player selected.</summary>
        private ActivationTarget AimedAtChosenWorld(ActivationTarget target)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || !round.HasMirrorWorld)
            {
                return target;
            }
            return target.OnWorld(effectsOnMirror);
        }

        /// <summary>Resolves a click into a board cell in EITHER world. The world comes from the
        /// board that was actually clicked, so a pointed effect never needs the [W] toggle.</summary>
        private bool TryBoardTargetAt(Vector2 world, out ActivationTarget target)
        {
            GridPos cell;
            if (boardView.TryWorldToCell(world, out cell))
            {
                target = ActivationTarget.Board(cell);
                return true;
            }
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round != null && round.HasMirrorWorld && mirrorBoardView != null
                && mirrorBoardView.TryWorldToCell(world, out cell))
            {
                target = ActivationTarget.Board(cell).OnWorld(true);
                return true;
            }
            target = ActivationTarget.None;
            return false;
        }

        /// <summary>[W] flips which world target-less jokers and powers act on.</summary>
        private bool ToggleEffectWorld(RoundEngine round)
        {
            if (round == null || !round.HasMirrorWorld)
            {
                return false;
            }
            effectsOnMirror = !effectsOnMirror;
            if (mainWorldFrame != null)
            {
                RefreshWorldChrome();
            }
            UpdateHud();
            return true;
        }

        /// <summary>Board size the MAIN world should use right now.</summary>
        private float MainBoardWorldSize
        {
            get
            {
                RoundEngine round = session != null ? session.CurrentRound : null;
                return round != null && round.HasMirrorWorld
                    ? MirrorBoardWorldSize
                    : MaxBoardWorldSize;
            }
        }

        /// <summary>Where the MAIN world sits right now.</summary>
        private Vector2 MainBoardCenter
        {
            get
            {
                RoundEngine round = session != null ? session.CurrentRound : null;
                return round != null && round.HasMirrorWorld ? MainWorldCenter : BoardCenter;
            }
        }

        /// <summary>Builds or tears down the mirror board to match the round, and keeps it in
        /// step with the engine's board. Called from the same refresh that syncs the main one.</summary>
        private void RefreshMirrorWorld()
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || !round.HasMirrorWorld)
            {
                if (mirrorBoardView != null)
                {
                    Destroy(mirrorBoardView.gameObject);
                    mirrorBoardView = null;
                }
                ClearMirrorHandVisuals();
                ClearWorldChrome();
                mirrorPickedIndex = -1;
                effectsOnMirror = false;
                return;
            }
            if (mirrorBoardView == null)
            {
                var go = new GameObject("MirrorBoardView");
                go.transform.SetParent(transform, false);
                mirrorBoardView = go.AddComponent<BoardView>();
            }
            if (mirrorBoardView.Board != round.MirrorBoard
                || !Mathf.Approximately(mirrorBuiltSize, MirrorBoardWorldSize)
                || (mirrorBuiltCenter - MirrorWorldCenter).sqrMagnitude > 0.000001f)
            {
                mirrorBoardView.Rebuild(round.MirrorBoard, MirrorBoardWorldSize, MirrorWorldCenter);
                mirrorBuiltSize = MirrorBoardWorldSize;
                mirrorBuiltCenter = MirrorWorldCenter;
            }
            mirrorBoardView.SetDarkness(round.BoardIsDark);
            mirrorBoardView.Refresh();
            mirrorBoardView.ClearPreview();
            RefreshMirrorHandVisuals(round);
            RefreshWorldChrome();
        }

        private float mirrorBuiltSize = -1f;
        private Vector2 mirrorBuiltCenter = new Vector2(float.NaN, float.NaN);

        /// <summary>The name plate over each world and the frame round the one [W] aims at.</summary>
        private void RefreshWorldChrome()
        {
            float size = MirrorBoardWorldSize;
            if (mainWorldLabel == null)
            {
                mainWorldLabel = ViewUtil.MakeText3D(transform, "MainWorldLabel", Vector2.zero,
                    "", 44, 0.05f, WorldLabelColor, 41, TextAnchor.LowerCenter);
                mirrorWorldLabel = ViewUtil.MakeText3D(transform, "MirrorWorldLabel", Vector2.zero,
                    "", 44, 0.05f, MirrorLabelColor, 41, TextAnchor.LowerCenter);
                mainWorldFrame = ViewUtil.MakeIcon(transform, "MainWorldFrame", Vector2.zero, 1f,
                    AimedFrameColor, 1, ViewUtil.GlowSprite);
                mirrorWorldFrame = ViewUtil.MakeIcon(transform, "MirrorWorldFrame", Vector2.zero, 1f,
                    AimedFrameColor, 1, ViewUtil.GlowSprite);
            }
            float labelY = size * 0.5f + 0.08f;
            mainWorldLabel.transform.position = MainWorldCenter + new Vector2(0f, labelY);
            mirrorWorldLabel.transform.position = MirrorWorldCenter + new Vector2(0f, labelY);
            mainWorldLabel.text = Loc.Pick("THIS WORLD", "BU DÜNYA");
            mirrorWorldLabel.text = Loc.Pick("THE OTHER WORLD", "ÖTEKİ DÜNYA");
            PlaceFrame(mainWorldFrame, MainWorldCenter, size, !effectsOnMirror);
            PlaceFrame(mirrorWorldFrame, MirrorWorldCenter, size, effectsOnMirror);
        }

        /// <summary>A soft light behind a world's board: which one untargeted effects will hit.
        /// A gradient that dies at its own edge, never a hard outline.</summary>
        private static void PlaceFrame(SpriteRenderer frame, Vector2 at, float size, bool aimed)
        {
            frame.transform.position = at;
            Vector2 unit = frame.sprite.bounds.size;
            float s = size * 1.12f;
            frame.transform.localScale = new Vector3(s / Mathf.Max(unit.x, 1e-4f), s / Mathf.Max(unit.y, 1e-4f), 1f);
            frame.color = aimed ? AimedFrameColor : new Color(0f, 0f, 0f, 0f);
        }

        private void ClearWorldChrome()
        {
            if (mainWorldLabel != null) Destroy(mainWorldLabel.gameObject);
            if (mirrorWorldLabel != null) Destroy(mirrorWorldLabel.gameObject);
            if (mainWorldFrame != null) Destroy(mainWorldFrame.gameObject);
            if (mirrorWorldFrame != null) Destroy(mirrorWorldFrame.gameObject);
            mainWorldLabel = mirrorWorldLabel = null;
            mainWorldFrame = mirrorWorldFrame = null;
            mirrorBuiltSize = -1f;
        }

        /// <summary>Draws the mirror hand as a row of small blocks under the mirror board - each
        /// on a slot plate, in the block's OWN faces (ViewUtil.CardCubeTile, the same tiles a hand
        /// card shows), so a fire block reads as fire here too. Picked is lifted and lit, booked is
        /// marked green, frozen is dimmed.</summary>
        private void RefreshMirrorHandVisuals(RoundEngine round)
        {
            ClearMirrorHandVisuals();
            int count = round.MirrorHand.Count;
            if (count == 0)
            {
                return;
            }
            float slotWidth = MirrorSlotWidth(count);
            float startX = MirrorWorldCenter.x - (count - 1) * slotWidth * 0.5f;
            float y = MirrorHandY;
            float plateH = MirrorHandBand * 0.86f;
            for (int i = 0; i < count; i++)
            {
                BlockCard card = round.MirrorHand[i];
                var root = new GameObject("MirrorCard_" + i);
                root.transform.SetParent(transform, false);
                mirrorHandRoots.Add(root);

                bool staged = round.StagedMirrorCard != null && round.StagedMirrorCard.Id == card.Id;
                bool picked = i == mirrorPickedIndex;
                bool frozen = round.IsFrozen(card.Id);
                float lift = picked ? 0.1f : 0f;
                float cx = startX + i * slotWidth;
                // The slot: a plate the block sits on, lit by its state.
                Color plate = staged ? new Color(0.2f, 0.42f, 0.26f, 0.9f)
                    : picked ? new Color(0.46f, 0.38f, 0.16f, 0.95f)
                    : new Color(0.13f, 0.14f, 0.22f, 0.85f);
                SpriteRenderer slot = ViewUtil.MakeRounded(root.transform, "Slot",
                    new Vector2(cx, y + lift), new Vector2(slotWidth * 0.9f, plateH), plate, 39);
                mirrorSlotPlates.Add(slot);
                if (picked || staged)
                {
                    SpriteRenderer rim = ViewUtil.MakeIcon(root.transform, "Rim", new Vector2(cx, y + lift),
                        1f, staged ? MirrorStagedColor : MirrorPickedColor, 38, ViewUtil.GlowSprite);
                    Vector2 unit = rim.sprite.bounds.size;
                    rim.transform.localScale = new Vector3(slotWidth * 1.05f / unit.x, plateH * 1.2f / unit.y, 1f);
                    Color rc = rim.color;
                    rc.a = 0.55f;
                    rim.color = rc;
                }
                // The block itself, in its own faces, fitted inside the plate.
                BlockShape shape = round.EffectiveShape(card);
                float cube = Mathf.Min(0.22f, (slotWidth * 0.78f) / Mathf.Max(1, shape.Width),
                    (plateH * 0.78f) / Mathf.Max(1, shape.Height));
                float ox = cx - (shape.Width - 1) * cube * 0.5f;
                float oy = y + lift - (shape.Height - 1) * cube * 0.5f;
                for (int c = 0; c < shape.Cells.Count; c++)
                {
                    GridPos cell = shape.Cells[c];
                    Color tint;
                    Sprite tile = ViewUtil.CardCubeTile(card, shape, c, true, out tint);
                    SpriteRenderer sprite = ViewUtil.MakeCell(root.transform, "c" + cell.X + "_" + cell.Y,
                        new Vector2(ox + cell.X * cube, oy + cell.Y * cube), cube, tint, 40);
                    ViewUtil.ApplyTile(sprite, tile, cube);
                    if (frozen)
                    {
                        tint = Color.Lerp(tint, new Color(0.6f, 0.8f, 1f), 0.6f);
                        tint.a = 0.5f;
                    }
                    else if (staged)
                    {
                        tint.a = 0.55f; // it is on the board now, as a booking
                    }
                    sprite.color = tint;
                    mirrorHandSprites.Add(sprite);
                }
            }
        }

        private void ClearMirrorHandVisuals()
        {
            for (int i = mirrorHandRoots.Count - 1; i >= 0; i--)
            {
                if (mirrorHandRoots[i] != null)
                {
                    Destroy(mirrorHandRoots[i]);
                }
            }
            mirrorHandRoots.Clear();
            mirrorHandSprites.Clear();
            mirrorSlotPlates.Clear();
        }

        /// <summary>Screen-space hit test over the mirror hand strip. Returns the hand index or
        /// -1. The strip is laid out on a fixed pitch, so the test is arithmetic rather than a
        /// per-sprite bounds check.</summary>
        private int MirrorHandIndexAt(Vector2 world, RoundEngine round)
        {
            int count = round.MirrorHand.Count;
            if (count == 0)
            {
                return -1;
            }
            float slotWidth = MirrorSlotWidth(count);
            float startX = MirrorWorldCenter.x - (count - 1) * slotWidth * 0.5f;
            if (Mathf.Abs(world.y - MirrorHandY) > MirrorHandBand * 0.5f)
            {
                return -1;
            }
            for (int i = 0; i < count; i++)
            {
                if (Mathf.Abs(world.x - (startX + i * slotWidth)) <= slotWidth * 0.46f)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// The mirror world's input, run before the main drag handler. Click a mirror hand card
        /// to pick it up, click a cell of the mirror board to BOOK it there. Booking again
        /// replaces the booking, so the player can change their mind right up until the main
        /// world plays. Returns true when it consumed the click.
        /// </summary>
        private bool HandleMirrorInput(RoundEngine round, Mouse mouse)
        {
            if (round == null || !round.HasMirrorWorld || mirrorBoardView == null || mouse == null)
            {
                return false;
            }
            if (!mouse.leftButton.wasPressedThisFrame)
            {
                return false;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());

            int handIndex = MirrorHandIndexAt(world, round);
            if (handIndex >= 0)
            {
                BlockCard card = round.MirrorHand[handIndex];
                // A second click on the same block puts it back down.
                mirrorPickedIndex = round.IsFrozen(card.Id) || mirrorPickedIndex == handIndex ? -1 : handIndex;
                if (mirrorPickedIndex >= 0)
                {
                    sfx.Pickup();
                }
                RefreshMirrorHandVisuals(round);
                return true;
            }

            GridPos cell;
            if (!mirrorBoardView.TryWorldToCell(world, out cell))
            {
                return false;
            }
            if (mirrorPickedIndex < 0 || mirrorPickedIndex >= round.MirrorHand.Count)
            {
                return true; // a click on the mirror board with nothing picked up: absorb it
            }
            BlockCard picked = round.MirrorHand[mirrorPickedIndex];
            BlockShape shape = round.EffectiveShape(picked);
            // Centred on the cursor exactly as the main board is (BoardView.WorldToCenteredOrigin)
            // - the mirror is the same placement gesture, so it must not aim differently.
            var origin = mirrorBoardView.WorldToCenteredOrigin(world, shape.Width, shape.Height);
            if (round.StageMirrorPlay(mirrorPickedIndex, origin))
            {
                sfx.Place();
                mirrorPickedIndex = -1;
                RefreshMirrorWorld();
                UpdateHud();
            }
            return true;
        }

        /// <summary>Shows where the picked mirror card would go. Runs every frame while a mirror
        /// card is in hand, so the preview follows the cursor like the main board's does.</summary>
        private void UpdateMirrorPreview(RoundEngine round, Mouse mouse)
        {
            if (round == null || !round.HasMirrorWorld || mirrorBoardView == null || mouse == null)
            {
                return;
            }
            if (mirrorPickedIndex < 0 || mirrorPickedIndex >= round.MirrorHand.Count)
            {
                mirrorBoardView.ClearPreview();
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            GridPos cell;
            if (!mirrorBoardView.TryWorldToCell(world, out cell))
            {
                mirrorBoardView.ClearPreview();
                return;
            }
            BlockCard picked = round.MirrorHand[mirrorPickedIndex];
            BlockShape shape = round.EffectiveShape(picked);
            // Centred on the cursor exactly as the main board is (BoardView.WorldToCenteredOrigin)
            // - the mirror is the same placement gesture, so it must not aim differently.
            var origin = mirrorBoardView.WorldToCenteredOrigin(world, shape.Width, shape.Height);
            mirrorBoardView.ShowPreview(shape, origin, round.CanPlaceMirrorCard(picked, origin));
        }

        /// <summary>[M]: resolves the turn with the mirror alone, for when the MAIN world has
        /// nowhere left to play. The engine refuses it in every other case, so this cannot be
        /// used to skip a turn.</summary>
        private bool TryPlayMirrorOnly(RoundEngine round)
        {
            if (round == null || !round.HasMirrorWorld || !round.MirrorHasStagedPlay
                || round.MainWorldHasAnyMove || round.Status != RoundStatus.InProgress)
            {
                return false;
            }
            TurnReport report = round.PlayMirrorOnly();
            FinalizePlacement(round, report);
            return true;
        }

        /// <summary>One line of HUD telling the player where the dual-world turn stands.</summary>
        private void AppendMirrorHud(System.Text.StringBuilder sb, RoundEngine round)
        {
            if (round == null || !round.HasMirrorWorld)
            {
                return;
            }
            sb.Append(Loc.Pick("TWO WORLDS   ", "İKİ DÜNYA   "));
            if (round.MirrorHasStagedPlay)
            {
                sb.Append(Loc.Pick("mirror booked - now play above",
                    "ayna hazır - şimdi üstte oyna"));
            }
            else if (!round.MirrorHasAnyMove)
            {
                sb.Append(Loc.Pick("mirror is stuck, it sits this one out",
                    "ayna tıkalı, bu turu pas geçiyor"));
            }
            else
            {
                sb.Append(Loc.Pick("click a mirror block, then a mirror cell",
                    "aynadan blok seç, sonra hücreye tıkla"));
            }
            if (!round.MainWorldHasAnyMove && round.MirrorHasStagedPlay)
            {
                sb.Append(Loc.Pick("   [M] play mirror alone", "   [M] sadece ayna oyna"));
            }
            sb.Append('\n');
            // Which world an untargeted joker/power goes to. A POINTED one ignores this: the
            // board you click is the world you meant.
            sb.Append(Loc.Pick("[W] effects -> ", "[W] etkiler -> "))
                .Append(effectsOnMirror
                    ? Loc.Pick("MIRROR world", "AYNA dünya")
                    : Loc.Pick("MAIN world", "ANA dünya"))
                .Append('\n');
        }
    }
}
