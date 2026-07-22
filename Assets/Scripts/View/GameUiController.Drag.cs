// PURPOSE: GameUiController placement input - mouse drag-to-place, and the retro
// (tetris) falling-piece controller: spawn, steer, rotate, gravity-drop, commit.

using System.Collections;
using System.Collections.Generic;
using System.Text;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private void HandleDrag(RoundEngine round, Mouse mouse)
        {
            if (mouse == null)
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());

            // Targeting mode: the next click picks the power's target - a hand/bonus card
            // or a board cell. Unlike jokers, hand-targeted powers may point at bonus
            // slots too (Hologram), so the full slot range is passed through.
            if (pendingTargetPowerId.HasValue)
            {
                Power aiming = session.Powers.Find(pendingTargetPowerId.Value);
                // Live blast preview while aiming a board-targeting power (Çaprazlama).
                if (aiming != null && !pendingOltaMark
                    && aiming.Targeting == ActivationTargeting.BoardCell)
                {
                    GridPos hoverCell;
                    if (boardView.TryWorldToCell(world, out hoverCell))
                    {
                        boardView.ShowPowerPreview(aiming.PreviewCells(ActivationTarget.Board(hoverCell)));
                    }
                    else
                    {
                        boardView.ClearPreview();
                    }
                }
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Power power = session.Powers.Find(pendingTargetPowerId.Value);
                    if (power == null)
                    {
                        CancelTargeting();
                        return;
                    }
                    if (pendingOltaMark)
                    {
                        CardVisual markHit = cardLayer.CardAt(world);
                        int powerId = power.InstanceId;
                        CancelTargeting();
                        if (markHit != null && markHit.SlotIndex >= 0
                            && markHit.SlotIndex < round.Hand.Count
                            && session.Powers.TryMarkOlta(powerId, markHit.SlotIndex))
                        {
                            Debug.Log("[project_block] Olta marked "
                                + round.Hand[markHit.SlotIndex] + ".");
                            powerBar.PulsePower(powerId);
                            powerBar.Refresh(session, null); // shows the new mark state
                        }
                        return;
                    }
                    if (power.Targeting == ActivationTargeting.BoardCell)
                    {
                        GridPos cell;
                        if (!boardView.TryWorldToCell(world, out cell))
                        {
                            CancelTargeting();
                            return;
                        }
                        RunPowerActivation(power, ActivationTarget.Board(cell));
                        return;
                    }
                    CardVisual hit = cardLayer.CardAt(world);
                    if (hit == null || hit.SlotIndex < 0
                        || hit.SlotIndex >= round.Hand.Count + round.BonusHand.Count)
                    {
                        CancelTargeting();
                        return;
                    }
                    RunPowerActivation(power, ActivationTarget.Hand(hit.SlotIndex));
                }
                return;
            }

            // Targeting mode: the next click picks the joker's target - a hand card or a
            // board cell, depending on what the joker asked for.
            if (pendingTargetJokerId.HasValue)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Joker joker = session.Jokers.Find(pendingTargetJokerId.Value);
                    if (joker == null)
                    {
                        CancelTargeting();
                        return;
                    }
                    if (joker.Targeting == ActivationTargeting.BoardCell)
                    {
                        GridPos cell;
                        if (!boardView.TryWorldToCell(world, out cell))
                        {
                            CancelTargeting();
                            return;
                        }
                        RunActivation(joker, ActivationTarget.Board(cell));
                        return;
                    }
                    CardVisual hit = cardLayer.CardAt(world);
                    if (hit == null || hit.SlotIndex < 0 || hit.SlotIndex >= round.Hand.Count)
                    {
                        CancelTargeting();
                        return;
                    }
                    RunActivation(joker, ActivationTarget.Hand(hit.SlotIndex));
                }
                return;
            }

            if (draggedCard == null)
            {
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    // right-click: rotate mechanical blocks, open the fox shape picker
                    CardVisual rightHit = cardLayer.CardAt(world);
                    if (rightHit != null && rightHit.SlotIndex >= 0
                        && rightHit.SlotIndex < round.Hand.Count)
                    {
                        BlockCard rightCard = round.Hand[rightHit.SlotIndex];
                        if (rightCard.Has(BlockElement.Mechanical))
                        {
                            round.RotateCard(rightHit.SlotIndex);
                            cardLayer.ForgetCard(rightCard.Id);
                            RefreshAll(null);
                        }
                        else if (rightCard.Has(BlockElement.Fox))
                        {
                            foxPickSlot = rightHit.SlotIndex;
                            deckOverlay.Show(session.OwnedCards);
                        }
                        else if (session.Config.Rules.RetroMode)
                        {
                            // retro lets an ordinary block rotate too, not just mechanical ones
                            round.RotateCard(rightHit.SlotIndex, ignoreMechanicalRequirement: true);
                            cardLayer.ForgetCard(rightCard.Id);
                            RefreshAll(null);
                        }
                    }
                    return;
                }
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (cardLayer.IsDrawPileAt(world))
                    {
                        deckOverlay.Show(session.OwnedCards);
                        return;
                    }
                    // "Fraksiyon": clicking the discard pile inspects its revealed half.
                    if (cardLayer.IsDiscardPileAt(world)
                        && round.Rules.RevealedDiscardCount > 0
                        && !round.Rules.HideDiscardTop)
                    {
                        deckOverlay.Show(RevealedDiscardCards(round));
                        return;
                    }
                    CardVisual hit = cardLayer.CardAt(world);
                    if (hit != null)
                    {
                        // A frozen card cannot leave the hand - refuse the pick-up rather
                        // than letting the drop throw (Hazine dynamite penalty).
                        BlockCard picked = CardOfSlot(round, hit.SlotIndex);
                        if (picked != null && round.IsFrozen(picked.Id))
                        {
                            return;
                        }
                        draggedCard = hit;
                        draggedCard.SetSortingBoost(10);
                        draggedCard.SetAlpha(0.55f);
                    }
                }
                return;
            }

            draggedCard.SnapTo(world);
            BlockCard slotCard = CardOfSlot(round, draggedCard.SlotIndex);
            BlockShape shape = slotCard != null ? round.EffectiveShape(slotCard) : null;
            GridPos hovered = default(GridPos);
            bool overBoard = false;
            if (shape != null)
            {
                // ghost blocks anchor loosely so they can overhang any edge
                overBoard = slotCard.Has(BlockElement.Ghost)
                    ? boardView.TryWorldToCellLoose(world, 2, out hovered)
                    : boardView.TryWorldToCell(world, out hovered);
            }
            var origin = default(GridPos);
            bool valid = false;
            if (overBoard)
            {
                // Anchor the shape so the cursor sits roughly at its center.
                origin = new GridPos(hovered.X - (shape.Width - 1) / 2, hovered.Y - (shape.Height - 1) / 2);
                valid = round.CanPlaceCard(slotCard, origin);
                boardView.ShowPreview(shape, origin, valid);
            }
            else
            {
                boardView.ClearPreview();
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                CardVisual released = draggedCard;
                draggedCard = null;
                released.SetSortingBoost(0);
                released.SetAlpha(1f);
                if (overBoard && valid)
                {
                    int slot = released.SlotIndex;
                    TurnReport report = slot < round.Hand.Count
                        ? round.PlayFromHand(slot, origin)
                        : round.PlayFromBonus(slot - round.Hand.Count, origin);
                    FinalizePlacement(round, report);
                }
                else
                {
                    released.MoveTo(released.HomePosition, 0.2f, null);
                    boardView.ClearPreview();
                }
            }
        }

        /// <summary>Retro (tetris) placement, run every frame while RetroMode is on. Click a hand
        /// card to drop it from the top; Left/Right move the column, Up or X rotate (any block
        /// rotates in retro), Down soft-drops, Space hard-drops, and a ghost shows the landing. On
        /// lock the piece settles through the normal PlayFromHand path - gravity only chooses
        /// WHERE it lands, so scoring / line clears / refill are all unchanged.</summary>
        private void HandleRetroFalling(RoundEngine round, Keyboard kb, Mouse mouse)
        {
            BlockCard card = retroFallHand >= 0 && retroFallHand < round.Hand.Count
                ? round.Hand[retroFallHand]
                : null;
            if (card == null)
            {
                retroFallHand = -1;
                boardView.ClearPreview();
                // Nothing falling: click a hand card to drop it into the arena.
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    Vector2 w = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    CardVisual hit = cardLayer.CardAt(w);
                    if (hit != null && hit.SlotIndex >= 0 && hit.SlotIndex < round.Hand.Count)
                    {
                        SpawnRetroPiece(round, hit.SlotIndex);
                    }
                }
                return;
            }

            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame)
                {
                    TryStepRetro(round, card, -1);
                }
                if (kb.rightArrowKey.wasPressedThisFrame)
                {
                    TryStepRetro(round, card, +1);
                }
                if (kb.upArrowKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)
                {
                    RotateRetro(round, card);
                }
                if (kb.spaceKey.wasPressedThisFrame)
                {
                    CommitRetroPiece(round, GravityDrop(round, card, new GridPos(retroFallX, retroFallY)));
                    return;
                }
            }

            float interval = kb != null && kb.downArrowKey.isPressed
                ? RetroSoftDropInterval : RetroFallInterval;
            retroFallTimer += Time.deltaTime;
            if (retroFallTimer >= interval)
            {
                retroFallTimer = 0f;
                if (round.CanPlaceCard(card, new GridPos(retroFallX, retroFallY - 1)))
                {
                    retroFallY--;
                }
                else
                {
                    CommitRetroPiece(round, new GridPos(retroFallX, retroFallY));
                    return;
                }
            }

            var origin = new GridPos(retroFallX, retroFallY);
            boardView.ShowFallingPiece(round.EffectiveShape(card), origin,
                GravityDrop(round, card, origin));
        }

        /// <summary>Drops the chosen hand card in at the top: the highest valid row of the center
        /// column, or any column if the center is blocked. Leaves it in hand if nowhere fits.</summary>
        private void SpawnRetroPiece(RoundEngine round, int handIndex)
        {
            BlockCard card = round.Hand[handIndex];
            GameBoard b = round.Board;
            int center = b.MinX + b.Width / 2;
            GridPos origin;
            if (TryTopValidRow(round, card, center, out origin)
                || TryAnyTopValidRow(round, card, out origin))
            {
                retroFallHand = handIndex;
                retroFallX = origin.X;
                retroFallY = origin.Y;
                retroFallTimer = 0f;
            }
        }

        /// <summary>Highest origin row where the card fits in column x (absolute coords), if any.</summary>
        private bool TryTopValidRow(RoundEngine round, BlockCard card, int x, out GridPos origin)
        {
            GameBoard b = round.Board;
            for (int y = b.MinY + b.Height - 1; y >= b.MinY; y--)
            {
                var candidate = new GridPos(x, y);
                if (round.CanPlaceCard(card, candidate))
                {
                    origin = candidate;
                    return true;
                }
            }
            origin = default(GridPos);
            return false;
        }

        /// <summary>First column (left to right) that can take the piece at the top, if any.</summary>
        private bool TryAnyTopValidRow(RoundEngine round, BlockCard card, out GridPos origin)
        {
            GameBoard b = round.Board;
            for (int x = b.MinX; x <= b.MinX + b.Width - 1; x++)
            {
                if (TryTopValidRow(round, card, x, out origin))
                {
                    return true;
                }
            }
            origin = default(GridPos);
            return false;
        }

        /// <summary>Slides the origin straight down from <paramref name="from"/> to the lowest
        /// row still reachable by continuous downward moves (respects overhangs).</summary>
        private GridPos GravityDrop(RoundEngine round, BlockCard card, GridPos from)
        {
            GridPos pos = from;
            while (round.CanPlaceCard(card, new GridPos(pos.X, pos.Y - 1)))
            {
                pos = new GridPos(pos.X, pos.Y - 1);
            }
            return pos;
        }

        /// <summary>Moves the falling piece one column left/right if it still fits there.</summary>
        private void TryStepRetro(RoundEngine round, BlockCard card, int dir)
        {
            if (round.CanPlaceCard(card, new GridPos(retroFallX + dir, retroFallY)))
            {
                retroFallX += dir;
            }
        }

        /// <summary>Rotates the falling piece (any block, in retro) and nudges it (a basic wall
        /// kick) so it stays valid; if no kick fits, the rotation is undone.</summary>
        private void RotateRetro(RoundEngine round, BlockCard card)
        {
            round.RotateCard(retroFallHand, true);
            if (round.CanPlaceCard(card, new GridPos(retroFallX, retroFallY)))
            {
                return;
            }
            GridPos[] kicks =
            {
                new GridPos(-1, 0), new GridPos(1, 0), new GridPos(0, 1),
                new GridPos(-2, 0), new GridPos(2, 0)
            };
            foreach (GridPos k in kicks)
            {
                if (round.CanPlaceCard(card, new GridPos(retroFallX + k.X, retroFallY + k.Y)))
                {
                    retroFallX += k.X;
                    retroFallY += k.Y;
                    return;
                }
            }
            // Nowhere to fit the rotated shape - spin back to the original orientation.
            round.RotateCard(retroFallHand, true);
            round.RotateCard(retroFallHand, true);
            round.RotateCard(retroFallHand, true);
        }

        /// <summary>Locks the falling piece at <paramref name="origin"/> and plays it through the
        /// normal placement path (scoring / clears / refill unchanged).</summary>
        private void CommitRetroPiece(RoundEngine round, GridPos origin)
        {
            int handIndex = retroFallHand;
            retroFallHand = -1;
            retroFallTimer = 0f;
            boardView.ClearPreview();
            if (handIndex < 0 || handIndex >= round.Hand.Count
                || !round.CanPlaceCard(round.Hand[handIndex], origin))
            {
                return; // safety: the piece is not in a legal spot to settle
            }
            TurnReport report = round.PlayFromHand(handIndex, origin);
            FinalizePlacement(round, report);
        }
    }
}
