// PURPOSE: GameUiController's half of "Öteki dünya" - the SECOND board and its own hand.
//
// It is all runtime construction, like the rest of View: a second BoardView object and a strip
// of small card sprites for the mirror hand. Nothing in the scene changes.
//
// LAYOUT. With one world the board keeps the position it always had. The moment a mirror opens,
// both boards shrink and split the vertical space - the main world above, the mirror below -
// so the two fit on the same screen without moving the camera.
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
        private const float MirrorBoardWorldSize = 4.1f;

        private static readonly Vector2 MainWorldCenter = new Vector2(0f, 2.55f);
        private static readonly Vector2 MirrorWorldCenter = new Vector2(0f, -1.85f);

        private static readonly Color MirrorCardColor = new Color(0.62f, 0.75f, 1f, 0.95f);
        private static readonly Color MirrorPickedColor = new Color(1f, 0.92f, 0.45f, 1f);
        private static readonly Color MirrorStagedColor = new Color(0.45f, 1f, 0.55f, 1f);

        private BoardView mirrorBoardView;

        /// <summary>Board size the main world was last built at, so a world opening or closing
        /// rebuilds it even though the GameBoard object did not change.</summary>
        private float lastMainBoardSize = -1f;
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
                    : maxBoardWorldSize;
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
                mirrorPickedIndex = -1;
                return;
            }
            if (mirrorBoardView == null)
            {
                var go = new GameObject("MirrorBoardView");
                go.transform.SetParent(transform, false);
                mirrorBoardView = go.AddComponent<BoardView>();
            }
            if (mirrorBoardView.Board != round.MirrorBoard)
            {
                mirrorBoardView.Rebuild(round.MirrorBoard, MirrorBoardWorldSize, MirrorWorldCenter);
            }
            mirrorBoardView.Refresh();
            mirrorBoardView.ClearPreview();
            RefreshMirrorHandVisuals(round);
        }

        /// <summary>Draws the mirror hand as a row of small blocks under the mirror board. Kept
        /// deliberately simple - it is a second hand, not a second card system.</summary>
        private void RefreshMirrorHandVisuals(RoundEngine round)
        {
            ClearMirrorHandVisuals();
            int count = round.MirrorHand.Count;
            if (count == 0)
            {
                return;
            }
            const float slotWidth = 1.5f;
            float startX = -(count - 1) * slotWidth * 0.5f;
            float y = MirrorWorldCenter.y - MirrorBoardWorldSize * 0.5f - 0.85f;
            for (int i = 0; i < count; i++)
            {
                BlockCard card = round.MirrorHand[i];
                var root = new GameObject("MirrorCard_" + i);
                root.transform.SetParent(transform, false);
                mirrorHandRoots.Add(root);

                bool staged = round.StagedMirrorCard != null && round.StagedMirrorCard.Id == card.Id;
                Color tint = staged
                    ? MirrorStagedColor
                    : (i == mirrorPickedIndex ? MirrorPickedColor : MirrorCardColor);
                if (round.IsFrozen(card.Id))
                {
                    tint = new Color(0.55f, 0.75f, 0.95f, 0.45f);
                }
                // One small square per cube of the block, so its shape is readable at a glance.
                BlockShape shape = round.EffectiveShape(card);
                const float cube = 0.19f;
                float ox = startX + i * slotWidth - (shape.Width - 1) * cube * 0.5f;
                float oy = y - (shape.Height - 1) * cube * 0.5f;
                foreach (GridPos cell in shape.Cells)
                {
                    SpriteRenderer sprite = ViewUtil.MakeRect(root.transform,
                        "c" + cell.X + "_" + cell.Y,
                        new Vector2(ox + cell.X * cube, oy + cell.Y * cube),
                        new Vector2(cube * 0.86f, cube * 0.86f), tint, 40);
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
            const float slotWidth = 1.5f;
            float startX = -(count - 1) * slotWidth * 0.5f;
            float y = MirrorWorldCenter.y - MirrorBoardWorldSize * 0.5f - 0.85f;
            if (Mathf.Abs(world.y - y) > 0.45f)
            {
                return -1;
            }
            for (int i = 0; i < count; i++)
            {
                if (Mathf.Abs(world.x - (startX + i * slotWidth)) <= slotWidth * 0.42f)
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
                mirrorPickedIndex = round.IsFrozen(card.Id) ? -1 : handIndex;
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
            var origin = new GridPos(cell.X - (shape.Width - 1) / 2, cell.Y - (shape.Height - 1) / 2);
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
            var origin = new GridPos(cell.X - (shape.Width - 1) / 2, cell.Y - (shape.Height - 1) / 2);
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
