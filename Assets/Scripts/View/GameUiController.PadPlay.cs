// PURPOSE: DIRECT gamepad play - the control scheme that does not push a cursor around.
// You step through your hand, step a block across the arena a cell at a time, and hold a
// shoulder to reach the joker or power strip. Chosen in the settings; the other scheme is
// the virtual cursor in GamepadBridge, and this file is the reason the bridge has a
// PointerMode.Snapped.
//
// THE ONE RULE THAT MAKES THIS SMALL: it does not invent a second way to see the game, only
// a second way to DRIVE it. The pointer is still real - the bridge snaps it onto whatever the
// pad has selected (a hand card, a board cell, a joker panel), so every hover visual, raised
// card and tooltip in the game keeps working with nothing added. What direct mode does NOT do
// is fake a click: the buttons call the same methods the mouse handlers call
// (PlayFromHand -> FinalizePlacement, BeginActivation, RunPowerActivation), the way the retro
// falling-piece controller already does.
//
// WHERE IT APPLIES: an in-progress round, and nothing else. Menus keep their list navigation
// and the market, the deck overlay and every modal are shelves of clickable things, so the
// pad falls back to the cursor there automatically (PadDirectPlayable). Retro rounds are
// excluded too - the falling piece IS a direct scheme already.
//
// EXTENSION POINT: a new focus is a member of PadFocus, a case in PadSnapScreen (where the
// pointer goes) and a handler. Rules NEVER live here.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>The two gamepad control schemes, chosen in the settings.</summary>
        private enum PadScheme
        {
            /// <summary>A drawn arrow pushed around by the stick; everything is clicked.</summary>
            Cursor,

            /// <summary>This file: step the hand, step the board, hold a shoulder for a bar.</summary>
            Direct
        }

        /// <summary>What the pad is stepping through right now.</summary>
        private enum PadFocus
        {
            /// <summary>The hand (and the bonus hand behind it) - the resting state.</summary>
            Hand,

            /// <summary>A block is chosen and is being walked across the arena.</summary>
            Board,

            /// <summary>The joker strip, held open by the right shoulder.</summary>
            Jokers,

            /// <summary>The power strip, held open by the left shoulder.</summary>
            Powers,

            /// <summary>The market shelf - stepping the offers rather than pointing at them.</summary>
            Market
        }

        private PadScheme padScheme = PadScheme.Cursor;
        private PadFocus padFocus = PadFocus.Hand;

        /// <summary>The chosen slot, in the same hand-then-bonus-hand numbering the drag path
        /// uses (see CardOfSlot).</summary>
        private int padHandSlot;

        /// <summary>Where the chosen block sits, or the cell being aimed at while a joker or
        /// power waits for a target. An ORIGIN, like every other placement in the game.</summary>
        private GridPos padCell;

        private int padBarIndex;

        /// <summary>Which offer on the shelf is chosen. Flat across the three sections, in the
        /// order GameSession lists them, so stepping it never has to ask the view anything.</summary>
        private int padMarketIndex;

        // Hold-to-repeat for the one stepping helper both sticks and the d-pad feed.
        private const float PadRepeatDelay = 0.34f;
        private const float PadRepeatInterval = 0.11f;
        private const float PadTriggerPoint = 0.4f;
        private bool padStepHeld;
        private Vector2Int padStepLast;
        private float padStepTimer;

        // Trigger edges, taken once at the top of the frame so every branch below sees them.
        private bool padLeftTriggerHeld;
        private bool padRightTriggerHeld;
        private bool padLtTap;
        private bool padRtTap;

        /// <summary>The gamepad diagnostics line (bottom-left, only while a pad drives).</summary>
        private UnityEngine.UI.Text padDebugText;

        /// <summary>True when direct mode owns the frame: the MARKET, or an in-progress,
        /// non-retro round - with nothing modal over either. The menus and the modals are
        /// lists and shelves of clickable things, so the pad falls back to the cursor there
        /// rather than half-working in two schemes at once.</summary>
        private bool PadDirectPlayable()
        {
            if (padScheme != PadScheme.Direct || session == null || PadModalOpen())
            {
                return false;
            }
            if (session.Phase == GamePhase.Market)
            {
                return true;
            }
            if (session.Phase != GamePhase.Round)
            {
                return false;
            }
            RoundEngine round = session.CurrentRound;
            if (round == null || round.Status != RoundStatus.InProgress)
            {
                return false;
            }
            if (session.Config.Rules.RetroMode)
            {
                return false; // the falling piece is already a direct scheme
            }
            if (round.HasMirrorWorld)
            {
                // "Öteki dünya" is a whole second board with its own hand, and the mirror has
                // to book its half of the turn before the main world may resolve one. It is
                // clicked and nothing else, so a round with one falls back to the cursor -
                // better than a scheme that can walk a block up to a placement it can never
                // legally make.
                return false;
            }
            return true;
        }

        /// <summary>Anything that covers the board and wants clicking (PadPanelOpen), plus the
        /// one thing that is not a panel but still owns the frame.
        ///
        /// NOT the board animations: Update already refuses all input while one plays, and
        /// counting them here would drop the scheme back to the cursor - so an arrow would
        /// blink onto the screen for the length of every water fall.</summary>
        private bool PadModalOpen()
        {
            return AnimLabOpen || GalleryOpen || PadPanelOpen()
                || draggedCard != null; // a mouse drag is in progress: leave it alone
        }

        /// <summary>Where the pointer goes this frame - the whole of what direct mode asks the
        /// bridge for. Snapping it onto the selection is what makes every hover visual and
        /// tooltip in the game follow the pad for free.</summary>
        private Vector2 PadSnapScreen()
        {
            // A panel takes the pointer whole: while one is open there is nothing else on
            // screen the pad can be pointing at (see .PadPanels).
            Vector2 panelPoint;
            if (PadPanelSnap(out panelPoint))
            {
                return panelPoint;
            }
            switch (padFocus)
            {
                case PadFocus.Jokers:
                {
                    Vector2? panel = jokerBar.PanelScreenCenter(padBarIndex);
                    if (panel.HasValue)
                    {
                        return panel.Value;
                    }
                    break;
                }
                case PadFocus.Powers:
                {
                    Vector2? panel = powerBar.PanelScreenCenter(padBarIndex);
                    if (panel.HasValue)
                    {
                        return panel.Value;
                    }
                    break;
                }
                case PadFocus.Board:
                    return cam.WorldToScreenPoint(boardView.CellToWorld(padCell));
                case PadFocus.Market:
                {
                    Vector2? tile = marketView.OfferWorldCenter(padMarketIndex);
                    if (tile.HasValue)
                    {
                        return cam.WorldToScreenPoint(tile.Value);
                    }
                    break;
                }
            }
            CardVisual visual = cardLayer.VisualOfSlot(padHandSlot);
            return visual != null
                ? (Vector2)cam.WorldToScreenPoint(visual.HomePosition)
                : (Vector2)cam.WorldToScreenPoint(BoardCenter);
        }

        /// <summary>One frame of direct play. Returns true when it CONSUMED the frame, which it
        /// only does when it actually acted - so the mouse, the debug keys and the drag path
        /// all keep working alongside it on every other frame.</summary>
        private bool HandlePadRound(RoundEngine round)
        {
            Gamepad pad = Gamepad.current;
            if (pad == null || gamepad == null || !gamepad.Active
                || padScheme != PadScheme.Direct || !PadDirectPlayable())
            {
                return false;
            }
            bool ltNow = pad.leftTrigger.ReadValue() > PadTriggerPoint;
            bool rtNow = pad.rightTrigger.ReadValue() > PadTriggerPoint;
            padLtTap = ltNow && !padLeftTriggerHeld;
            padRtTap = rtNow && !padRightTriggerHeld;
            padLeftTriggerHeld = ltNow;
            padRightTriggerHeld = rtNow;
            PadClampSelection(round);

            // A joker or power waiting for a target owns the pad, exactly as it owns the mouse.
            if (pendingTargetJokerId.HasValue || pendingTargetPowerId.HasValue)
            {
                return HandlePadTargeting(round, pad);
            }
            // A held shoulder opens a strip and holds it open; letting go returns to the hand.
            if (pad.rightShoulder.isPressed && session.Jokers.Count > 0)
            {
                return HandlePadBar(pad, true);
            }
            if (pad.leftShoulder.isPressed && session.Powers.Count > 0)
            {
                return HandlePadBar(pad, false);
            }
            if (padFocus == PadFocus.Jokers || padFocus == PadFocus.Powers)
            {
                padFocus = PadFocus.Hand;
                padBarIndex = 0;
            }
            if (pad.buttonEast.wasPressedThisFrame && padFocus == PadFocus.Hand)
            {
                OpenPauseMenu();
                return true;
            }
            if (pad.startButton.wasPressedThisFrame)
            {
                // The MENU button (the three lines) looks through your collection - the same
                // overlay clicking the draw pile opens. Pausing is B's job, from the hand.
                deckOverlay.ResetScroll();
                deckOverlay.Show(session.OwnedCards);
                PadPanelReset();
                return true;
            }
            return padFocus == PadFocus.Board
                ? HandlePadPlacing(round, pad)
                : HandlePadHand(round, pad);
        }

        // ---- the market -------------------------------------------------------------------

        /// <summary>One frame of direct play in the SHOP. It is not the cursor scheme with a
        /// snapped pointer: the shelf is a list, so it is STEPPED - left and right along a
        /// section, up and down between them - and every one of the market's verbs gets a
        /// button of its own instead of a place to click. Returns true when it acted.</summary>
        private bool HandlePadMarket()
        {
            Gamepad pad = Gamepad.current;
            if (pad == null || gamepad == null || !gamepad.Active || !PadDirectPlayable())
            {
                return false;
            }
            bool ltNow = pad.leftTrigger.ReadValue() > PadTriggerPoint;
            padLtTap = ltNow && !padLeftTriggerHeld;
            padLeftTriggerHeld = ltNow;
            padRightTriggerHeld = pad.rightTrigger.ReadValue() > PadTriggerPoint;

            // A held shoulder opens a strip - and in the SHOP, using one SELLS it.
            if (pad.rightShoulder.isPressed && session.Jokers.Count > 0)
            {
                return HandlePadSellBar(pad, true);
            }
            if (pad.leftShoulder.isPressed && session.Powers.Count > 0)
            {
                return HandlePadSellBar(pad, false);
            }
            if (padFocus != PadFocus.Market)
            {
                padFocus = PadFocus.Market;
                padBarIndex = 0;
            }
            IReadOnlyList<MarketOffer> offers = session.Market.Offers;
            padMarketIndex = offers.Count > 0
                ? Mathf.Clamp(padMarketIndex, 0, offers.Count - 1)
                : 0;

            if (pad.buttonEast.wasPressedThisFrame)
            {
                OpenPauseMenu();
                return true;
            }
            if (pad.buttonNorth.wasPressedThisFrame)
            {
                LeaveMarketNow();
                return true;
            }
            if (pad.startButton.wasPressedThisFrame)
            {
                // The deck as a SELL screen, the same thing clicking the draw pile opens. The
                // overlay is a panel, so the pad points at it with the cursor from here.
                sellCardsMode = true;
                deckOverlay.ResetScroll();
                deckOverlay.Show(session.OwnedCards, true);
                PadPanelReset();
                return true;
            }
            if (session.Debt > 0 && pad.rightStickButton.wasPressedThisFrame)
            {
                // "Kredi kartı": settling up is a market action and never automatic, so like
                // the [O] key it needs a press of its own.
                if (session.RepayDebtInFull() > 0)
                {
                    sfx.Buy();
                }
                RefreshAll(null);
                return true;
            }
            if (offers.Count == 0)
            {
                return false;
            }
            Vector2Int step = PadStep(pad);
            if (step.x != 0 || step.y != 0)
            {
                padMarketIndex = PadOfferInDirection(padMarketIndex, step, offers.Count);
                return true;
            }
            if (pad.buttonWest.wasPressedThisFrame)
            {
                RerollSection(offers[padMarketIndex].Kind);
                return true;
            }
            if (pad.buttonSouth.wasPressedThisFrame)
            {
                // "Kaçakçı" rides the left trigger here for the same reason SHIFT carries it on
                // the mouse: taking the free one is a deliberate modifier, not the plain buy.
                BuyOffer(padMarketIndex, session.CanSmuggle && padLeftTriggerHeld);
                return true;
            }
            return false;
        }

        /// <summary>The offer a direction leads to, chosen by WHERE THE TILES ACTUALLY ARE
        /// rather than by their order in the list.
        ///
        /// The shelf is not a row: blocks run down the whole left column with jokers over
        /// powers on the right, so the tile before the first power in the list is a JOKER while
        /// the tile to its left on screen is a BLOCK. Stepping the list walked off in the wrong
        /// direction at every section edge. Asking the layout instead is also the only version
        /// that survives the shelf being rearranged - or the other market layout, whose three
        /// sections are stacked.
        ///
        /// It takes the nearest tile that is genuinely in that direction, preferring the one
        /// straight ahead: `along` is how far it lies the way you pushed, `across` how far it
        /// sits off that line. Nothing in the direction means nothing moves - the edges of a
        /// shelf are hard.</summary>
        private int PadOfferInDirection(int from, Vector2Int direction, int count)
        {
            return PadNearestInDirection(from, count, marketView.OfferWorldCenter, direction);
        }

        /// <summary>The shelf's rule, generalized: given where a set of things ARE, which one
        /// does a direction lead to. Every panel a pad steps through goes through this, so
        /// none of them has to agree with any other about how they are laid out.</summary>
        private static int PadNearestInDirection(int from, int count,
            System.Func<int, Vector2?> centreOf, Vector2Int direction)
        {
            Vector2? origin = centreOf(from);
            if (!origin.HasValue)
            {
                return from;
            }
            Vector2 here = origin.Value;
            var axis = new Vector2(direction.x, direction.y);
            int best = from;
            float bestScore = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (i == from)
                {
                    continue;
                }
                Vector2? other = centreOf(i);
                if (!other.HasValue)
                {
                    continue;
                }
                Vector2 delta = other.Value - here;
                float along = delta.x * axis.x + delta.y * axis.y;
                if (along <= 0.001f)
                {
                    continue; // behind us, or exactly sideways
                }
                float across = Mathf.Abs(delta.x * axis.y - delta.y * axis.x);
                if (across > along * PadNavSpread)
                {
                    continue; // so far off the line that it is not "that way" at all
                }
                float score = along + across * PadNavAcrossCost;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>How far off the pushed direction a tile may sit and still count, as a
        /// multiple of how far ahead it is. Tall columns beside short rows need this generous.</summary>
        private const float PadNavSpread = 2.5f;

        /// <summary>What sideways distance costs against straight-ahead distance when two tiles
        /// are both candidates. Above 1 so the tile in line wins ties comfortably.</summary>
        private const float PadNavAcrossCost = 2f;

        /// <summary>A strip held open in the SHOP sells rather than uses. The panels resolve
        /// through the pointer that is snapped onto them, so this is the same route the mouse
        /// takes - refusals ("Kredi kartı" owing money) and all.</summary>
        private bool HandlePadSellBar(Gamepad pad, bool jokers)
        {
            int count = jokers ? session.Jokers.Count : session.Powers.Count;
            if (count <= 0)
            {
                return false;
            }
            if (padFocus != (jokers ? PadFocus.Jokers : PadFocus.Powers))
            {
                padFocus = jokers ? PadFocus.Jokers : PadFocus.Powers;
                padBarIndex = 0;
            }
            padBarIndex = Mathf.Clamp(padBarIndex, 0, count - 1);
            Vector2Int step = PadStep(pad);
            if (step.x != 0)
            {
                padBarIndex = (padBarIndex + BarStepX(step.x, jokers) + count) % count;
                return true;
            }
            // TWO VERBS, TWO BUTTONS. The mouse tells a use from a sale by how long the press
            // lasts; a pad has no reason to make the player hold a button when it has a spare
            // one, so A uses and X sells - X being the pad's secondary verb everywhere else in
            // the game (see GamepadBridge: X is right-click). Same two actions as the mouse,
            // reached the way a pad reaches things.
            if (pad.buttonWest.wasPressedThisFrame)
            {
                if (jokers)
                {
                    SellJokerAt(padBarIndex);
                }
                else
                {
                    SellPowerAt(padBarIndex);
                }
                padFocus = PadFocus.Market;
                return true;
            }
            if (!pad.buttonSouth.wasPressedThisFrame)
            {
                return false;
            }
            if (jokers)
            {
                UseJokerInMarket(padBarIndex);
            }
            else
            {
                HintHoldToSell(powerBar.PanelScreenCenter(padBarIndex));
            }
            padFocus = PadFocus.Market;
            return true;
        }

        /// <summary>Keeps the selection pointing at something that still exists - the hand is
        /// re-dealt constantly and a joker can be sold out from under the strip.</summary>
        private void PadClampSelection(RoundEngine round)
        {
            int slots = round.Hand.Count + round.BonusHand.Count;
            if (slots <= 0)
            {
                padHandSlot = 0;
            }
            else if (padHandSlot >= slots || padHandSlot < 0)
            {
                padHandSlot = Mathf.Clamp(padHandSlot, 0, slots - 1);
            }
            if (padFocus == PadFocus.Board && CardOfSlot(round, padHandSlot) == null)
            {
                padFocus = PadFocus.Hand; // the block being placed is gone
                boardView.ClearPreview();
            }
        }

        // ---- the hand ------------------------------------------------------------------

        private bool HandlePadHand(RoundEngine round, Gamepad pad)
        {
            int slots = round.Hand.Count + round.BonusHand.Count;
            Vector2Int step = PadStep(pad);
            if (step.x != 0 && slots > 0)
            {
                padHandSlot = (padHandSlot + step.x + slots) % slots;
                return true;
            }
            BlockCard card = CardOfSlot(round, padHandSlot);
            if (card == null)
            {
                return false;
            }
            if (pad.buttonWest.wasPressedThisFrame)
            {
                return PadRotateOrReshape(round, card);
            }
            if (pad.rightStickButton.wasPressedThisFrame)
            {
                // "Tamagotchi": feed the pet the card you have chosen. It goes through the
                // very same method the F key does - the pointer is snapped to the selection,
                // so "the card under the cursor" IS the chosen one.
                TryFeedPetUnderCursor(round, Mouse.current);
                return true;
            }
            if (!pad.buttonSouth.wasPressedThisFrame)
            {
                return false;
            }
            // Picking a block up. The two refusals the drag path has are the same here: a
            // frozen card cannot leave the hand at all, and while the hand is face down a
            // press is the COMMITMENT rather than a pick-up ("Şaşırtmaca").
            if (round.IsFrozen(card.Id))
            {
                return true;
            }
            if (round.HandIsFaceDown && round.RevealedHandCardId != card.Id)
            {
                if (round.RevealedHandCardId == 0 && padHandSlot < round.Hand.Count
                    && round.RevealHandCard(padHandSlot))
                {
                    sfx.Place();
                    RefreshAll(null);
                }
                return true;
            }
            padFocus = PadFocus.Board;
            padCell = PadStartCell(round, card);
            return true;
        }

        /// <summary>Where a block starts when you pick it up: the middle of the arena, nudged
        /// so the whole shape is inside it. Somewhere legal to begin from beats somewhere
        /// clever.</summary>
        private GridPos PadStartCell(RoundEngine round, BlockCard card)
        {
            BlockShape shape = round.EffectiveShape(card);
            GameBoard board = round.Board;
            int x = board.MinX + (board.Width - shape.Width) / 2;
            int y = board.MinY + (board.Height - shape.Height) / 2;
            return new GridPos(x, y);
        }

        // ---- walking a block across the arena --------------------------------------------

        private bool HandlePadPlacing(RoundEngine round, Gamepad pad)
        {
            BlockCard card = CardOfSlot(round, padHandSlot);
            BlockShape shape = round.EffectiveShape(card);
            // Every frame, not only on a step: the shape can change under the origin while the
            // block is being walked (a gear turned, a fox reshaped through the overlay), and a
            // wider shape would then be hanging off the edge it used to fit inside.
            padCell = PadClampCell(round, shape, padCell);
            bool acted = false;
            Vector2Int step = PadStep(pad);
            if (step.x != 0 || step.y != 0)
            {
                padCell = PadClampCell(round, shape,
                    new GridPos(padCell.X + step.x, padCell.Y + step.y));
                acted = true;
            }
            if (pad.buttonEast.wasPressedThisFrame)
            {
                padFocus = PadFocus.Hand;
                boardView.ClearPreview();
                return true;
            }
            if (pad.buttonWest.wasPressedThisFrame)
            {
                if (PadRotateOrReshape(round, card))
                {
                    // The shape changed under the origin, so put it back inside the arena.
                    BlockCard again = CardOfSlot(round, padHandSlot);
                    if (again != null)
                    {
                        padCell = PadClampCell(round, round.EffectiveShape(again), padCell);
                    }
                    return true;
                }
            }
            bool valid = round.CanPlaceCard(card, padCell);
            boardView.ShowPreview(shape, padCell, valid);
            if (!pad.buttonSouth.wasPressedThisFrame)
            {
                return acted;
            }
            if (!valid)
            {
                // Direct mode has no way to drop a block anywhere BUT the arena (PadClampCell
                // keeps it inside), so pressing A on a spot the board refuses is always the
                // aimed, refused placement the drag path charges for.
                RejectPlacement(round, boardView.CellToWorld(padCell));
                return true; // a refused placement is still the pad's frame
            }
            // "Öteki dünya": the mirror world books its half of the turn first, exactly as in
            // the drag path - say so and keep the block rather than letting the engine throw.
            if (!round.MirrorReadyForTurn)
            {
                FloatingTextFx.Spawn(transform, boardView.CellToWorld(padCell),
                    Loc.Pick("PLAY THE MIRROR WORLD FIRST", "ÖNCE AYNA DÜNYAYA OYNA"),
                    new Color(1f, 0.55f, 0.4f), 46, 0.05f);
                return true;
            }
            int slot = padHandSlot;
            padFocus = PadFocus.Hand;
            boardView.ClearPreview();
            TurnReport report = slot < round.Hand.Count
                ? round.PlayFromHand(slot, padCell)
                : round.PlayFromBonus(slot - round.Hand.Count, padCell);
            FinalizePlacement(round, report);
            return true;
        }

        /// <summary>Keeps a shape's bounding box inside the arena. Overhang is a GHOST block's
        /// privilege and the drag path gets it from a loose hit test; direct mode gives up
        /// that one trick in exchange for never walking a block off the screen.</summary>
        private GridPos PadClampCell(RoundEngine round, BlockShape shape, GridPos wanted)
        {
            GameBoard board = round.Board;
            int x = Mathf.Clamp(wanted.X, board.MinX, board.MinX + board.Width - shape.Width);
            int y = Mathf.Clamp(wanted.Y, board.MinY, board.MinY + board.Height - shape.Height);
            return new GridPos(x, y);
        }

        /// <summary>The right-click abilities on a held card, reached with the west button:
        /// gears turn, a fox opens the shape picker. Asked through the ROUND, since a boss can
        /// suppress every element.</summary>
        private bool PadRotateOrReshape(RoundEngine round, BlockCard card)
        {
            if (round.CardHasElement(card, BlockElement.Mechanical))
            {
                round.RotateCard(padHandSlot);
                cardLayer.ForgetCard(card.Id);
                RefreshAll(null);
                return true;
            }
            if (round.CardHasElement(card, BlockElement.Fox))
            {
                foxPickSlot = padHandSlot;
                deckOverlay.ResetScroll();
                deckOverlay.ShowShapes(FoxShapeChoices());
                return true;
            }
            return false;
        }

        // ---- the joker and power strips ---------------------------------------------------

        /// <summary>Which way through the inventory a stick push goes. The desktop joker grid
        /// fills right to left from the screen edge (UiLayout.PlaceBarSlot), so there the next
        /// joker is to the LEFT and a push right has to walk backwards.</summary>
        private static int BarStepX(int stepX, bool jokers)
        {
            return jokers && !UiLayout.Active.BarsAsRow ? -stepX : stepX;
        }

        /// <summary>A strip is open only while its shoulder is HELD, so it can never be left
        /// open over the board. The trigger under that shoulder uses what is highlighted.</summary>
        private bool HandlePadBar(Gamepad pad, bool jokers)
        {
            int count = jokers ? session.Jokers.Count : session.Powers.Count;
            if (count <= 0)
            {
                return false;
            }
            if (padFocus != (jokers ? PadFocus.Jokers : PadFocus.Powers))
            {
                padFocus = jokers ? PadFocus.Jokers : PadFocus.Powers;
                padBarIndex = 0;
                boardView.ClearPreview(); // a block being walked is put down, not thrown
            }
            padBarIndex = Mathf.Clamp(padBarIndex, 0, count - 1);
            Vector2Int step = PadStep(pad);
            if (step.x != 0)
            {
                padBarIndex = (padBarIndex + BarStepX(step.x, jokers) + count) % count;
                return true;
            }
            // A, and only A. Holding the shoulder is how you INSPECT the strip - the tooltip
            // follows the pointer that is snapped onto each panel - and one button then uses
            // what you stopped on. A trigger doing the same job was a second way to say it.
            if (!pad.buttonSouth.wasPressedThisFrame)
            {
                return false;
            }
            if (jokers)
            {
                BeginActivation(session.Jokers.Jokers[padBarIndex]);
            }
            else
            {
                BeginPowerActivation(session.Powers.Powers[padBarIndex]);
            }
            // Activation may have asked for a target; the targeting handler takes it from here
            // and starts aiming at the middle of the arena.
            if (pendingTargetJokerId.HasValue || pendingTargetPowerId.HasValue)
            {
                padFocus = PadFocus.Board;
                padCell = PadBoardCentre();
            }
            else
            {
                padFocus = PadFocus.Hand;
            }
            return true;
        }

        private GridPos PadBoardCentre()
        {
            GameBoard board = session.CurrentRound.Board;
            return new GridPos(board.MinX + board.Width / 2, board.MinY + board.Height / 2);
        }

        // ---- aiming a joker or power ------------------------------------------------------

        /// <summary>Picking the target an activated joker or power is waiting for. It reuses
        /// the drag path's own resolution (TryBoardTargetAt, which knows about the mirror
        /// world) by handing it the world point the pad is aiming at.</summary>
        private bool HandlePadTargeting(RoundEngine round, Gamepad pad)
        {
            ActivationTargeting targeting = ActivationTargeting.None;
            Joker joker = pendingTargetJokerId.HasValue
                ? session.Jokers.Find(pendingTargetJokerId.Value) : null;
            Power power = pendingTargetPowerId.HasValue
                ? session.Powers.Find(pendingTargetPowerId.Value) : null;
            if (joker != null)
            {
                targeting = joker.Targeting;
            }
            else if (power != null)
            {
                targeting = power.Targeting;
            }
            else
            {
                CancelTargeting();
                return true;
            }
            if (pad.buttonEast.wasPressedThisFrame)
            {
                CancelTargeting();
                padFocus = PadFocus.Hand;
                return true;
            }
            bool boardTarget = targeting == ActivationTargeting.BoardCell
                || targeting == ActivationTargeting.BoardArea;
            // "Olta" marks a card in hand rather than using the power on one, but it picks the
            // same way, so it goes down the hand branch with everything else.
            if (boardTarget && !pendingOltaMark)
            {
                padFocus = PadFocus.Board;
                Vector2Int step = PadStep(pad);
                if (step.x != 0 || step.y != 0)
                {
                    GameBoard board = round.Board;
                    padCell = new GridPos(
                        Mathf.Clamp(padCell.X + step.x, board.MinX, board.MinX + board.Width - 1),
                        Mathf.Clamp(padCell.Y + step.y, board.MinY, board.MinY + board.Height - 1));
                    return true;
                }
                if (!pad.buttonSouth.wasPressedThisFrame)
                {
                    return false; // let the drag path draw the aiming preview from our pointer
                }
                // "Hidrolik pres" aims TWICE - the patch, then which of its four cells keeps the
                // cube - so A goes through the very handler the mouse click goes through rather
                // than firing the power off the first pick. Everything else fires on one press.
                if (workshopPowerId.HasValue && targeting == ActivationTargeting.BoardArea
                    && power != null)
                {
                    if (HandlePressClick(power, round, boardView.CellToWorld(padCell))
                        && !workshopPowerId.HasValue)
                    {
                        padFocus = PadFocus.Hand;
                    }
                    return true;
                }
                ActivationTarget cellTarget;
                if (!TryBoardTargetAt(boardView.CellToWorld(padCell), out cellTarget))
                {
                    return true;
                }
                padFocus = PadFocus.Hand;
                if (joker != null)
                {
                    RunActivation(joker, cellTarget);
                }
                else
                {
                    RunPowerActivation(power, cellTarget);
                }
                return true;
            }
            // A hand target: step the hand and press south, the same as choosing a block.
            padFocus = PadFocus.Hand;
            int slots = joker != null
                ? round.Hand.Count
                : round.Hand.Count + round.BonusHand.Count; // powers may point at bonus slots
            if (slots <= 0)
            {
                CancelTargeting();
                return true;
            }
            padHandSlot = Mathf.Clamp(padHandSlot, 0, slots - 1);
            Vector2Int handStep = PadStep(pad);
            if (handStep.x != 0)
            {
                padHandSlot = (padHandSlot + handStep.x + slots) % slots;
                return true;
            }
            if (!pad.buttonSouth.wasPressedThisFrame)
            {
                return false;
            }
            if (pendingOltaMark)
            {
                int powerId = power.InstanceId;
                int marked = padHandSlot;
                CancelTargeting();
                if (marked < round.Hand.Count && session.Powers.TryMarkOlta(powerId, marked))
                {
                    powerBar.PulsePower(powerId);
                    powerBar.Refresh(session, null);
                }
                return true;
            }
            if (joker != null)
            {
                RunActivation(joker, ActivationTarget.Hand(padHandSlot));
            }
            else
            {
                RunPowerActivation(power, ActivationTarget.Hand(padHandSlot));
            }
            return true;
        }

        // ---- the one stepping helper ------------------------------------------------------

        /// <summary>One discrete step out of the stick AND the d-pad, with hold-to-repeat.
        /// Everything direct mode navigates - the hand, the arena, a strip - is a step, so
        /// they all come through here and all repeat at the same rate.</summary>
        private Vector2Int PadStep(Gamepad pad)
        {
            Vector2 stick = pad.leftStick.ReadValue();
            int x = 0;
            int y = 0;
            if (pad.dpad.left.isPressed || stick.x < -0.5f)
            {
                x = -1;
            }
            else if (pad.dpad.right.isPressed || stick.x > 0.5f)
            {
                x = 1;
            }
            if (pad.dpad.down.isPressed || stick.y < -0.5f)
            {
                y = -1;
            }
            else if (pad.dpad.up.isPressed || stick.y > 0.5f)
            {
                y = 1;
            }
            // 4-WAY: a step is one direction. A diagonal push moving a block two ways at once
            // reads as the block sliding away from you.
            if (x != 0 && y != 0)
            {
                if (Mathf.Abs(stick.x) >= Mathf.Abs(stick.y))
                {
                    y = 0;
                }
                else
                {
                    x = 0;
                }
            }
            if (x == 0 && y == 0)
            {
                padStepHeld = false;
                return Vector2Int.zero;
            }
            var wanted = new Vector2Int(x, y);
            if (!padStepHeld || wanted != padStepLast)
            {
                padStepHeld = true;
                padStepLast = wanted;
                padStepTimer = PadRepeatDelay;
                return wanted;
            }
            padStepTimer -= Time.unscaledDeltaTime;
            if (padStepTimer <= 0f)
            {
                padStepTimer = PadRepeatInterval;
                return wanted;
            }
            return Vector2Int.zero;
        }
    }
}
