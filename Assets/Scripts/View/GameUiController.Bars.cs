// PURPOSE: GameUiController bar & market interactions - using/selling jokers and powers
// from the bars, the "Hileli zar" opening-hand pick, the Parazit attach flow, market
// clicks and joker debug-key input.

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
        /// <summary>Clicking a joker panel in the bar activates it, mirroring the 1-9 keys.</summary>
        private bool TryUseJokerFromBar(Mouse mouse)
        {
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame
                || pendingTargetJokerId.HasValue || pendingTargetPowerId.HasValue)
            {
                return false;
            }
            int index = jokerBar.JokerIndexAt(mouse.position.ReadValue());
            if (index < 0 || index >= session.Jokers.Count)
            {
                return false;
            }
            BeginActivation(session.Jokers.Jokers[index]);
            return true;
        }

        /// <summary>Clicking a power panel in the left bar uses it (or arms targeting).</summary>
        private bool TryUsePowerFromBar(Mouse mouse)
        {
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame
                || pendingTargetJokerId.HasValue || pendingTargetPowerId.HasValue)
            {
                return false;
            }
            int index = powerBar.PowerIndexAt(mouse.position.ReadValue());
            if (index < 0 || index >= session.Powers.Count)
            {
                return false;
            }
            BeginPowerActivation(session.Powers.Powers[index]);
            return true;
        }

        /// <summary>Right-clicking a charged "Halüsinasyon" power skips its current roll: it
        /// morphs to a new random power and spends the charge (refills next round), running
        /// nothing. Right-click is otherwise free over the power bar during a round.</summary>
        private bool TrySkipHalusinasyonFromBar(Mouse mouse)
        {
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame
                || pendingTargetJokerId.HasValue || pendingTargetPowerId.HasValue)
            {
                return false;
            }
            int index = powerBar.PowerIndexAt(mouse.position.ReadValue());
            if (index < 0 || index >= session.Powers.Count)
            {
                return false;
            }
            Power power = session.Powers.Powers[index];
            if (power.DefId != "halusinasyon" || !session.Powers.TrySkip(power.InstanceId))
            {
                return false;
            }
            sfx.Shuffle(); // a small "rolled again" cue
            powerBar.PulsePower(power.InstanceId);
            powerBar.Refresh(session, null);
            UpdateHud();
            return true;
        }

        /// <summary>In the market, clicking a joker panel sells it for its SellValue.</summary>
        private bool TrySellJokerFromBar(Mouse mouse)
        {
            int index = jokerBar.JokerIndexAt(mouse.position.ReadValue());
            if (index < 0 || index >= session.Jokers.Count)
            {
                return false;
            }
            Joker joker = session.Jokers.Jokers[index];
            Vector2? panelScreen = jokerBar.PanelScreenCenter(index);
            // "Kredi kartı" is locked while its debt is open, so say so instead of silently
            // doing nothing - a click that looks ignored reads as a bug.
            if (!session.Jokers.CanSell(joker))
            {
                if (panelScreen.HasValue)
                {
                    Vector2 at = cam.ScreenToWorldPoint(panelScreen.Value);
                    FloatingTextFx.Spawn(transform, at,
                        Loc.Pick("PAY THE DEBT FIRST", "ÖNCE BORCU ÖDE"),
                        new Color(1f, 0.45f, 0.4f), 48, 0.05f);
                }
                return true;
            }
            long paid = session.Jokers.Sell(joker);
            Debug.Log("[block_bonk] Sold joker " + joker.DisplayName + " for " + paid);
            sfx.Buy();
            if (panelScreen.HasValue)
            {
                Vector2 world = cam.ScreenToWorldPoint(panelScreen.Value);
                FloatingTextFx.Spawn(transform, world, "+" + paid,
                    new Color(1f, 0.92f, 0.45f), 60, 0.05f);
            }
            marketView.Show(session);
            jokerBar.AnimateJokerSold(index, session); // shrink, then refresh the strip
            UpdateHud();
            return true;
        }

        /// <summary>In the market, clicking a power panel sells it for its sell value.</summary>
        private bool TrySellPowerFromBar(Mouse mouse)
        {
            int index = powerBar.PowerIndexAt(mouse.position.ReadValue());
            if (index < 0 || index >= session.Powers.Count)
            {
                return false;
            }
            Power power = session.Powers.Powers[index];
            Vector2? panelScreen = powerBar.PanelScreenCenter(index);
            int paid = session.Powers.Sell(power);
            Debug.Log("[block_bonk] Sold power " + power.DisplayName + " for " + paid);
            sfx.Buy();
            if (panelScreen.HasValue)
            {
                Vector2 world = cam.ScreenToWorldPoint(panelScreen.Value);
                FloatingTextFx.Spawn(transform, world, "+" + paid,
                    new Color(1f, 0.92f, 0.45f), 60, 0.05f);
            }
            marketView.Show(session); // affordability colors follow the new balance
            powerBar.AnimatePowerSold(index, session);
            UpdateHud();
            return true;
        }

        /// <summary>In the market, clicking a "Hileli zar" joker that still has this market's
        /// pick opens the opening-hand picker instead of selling it - the same interception
        /// Parazit does, so once the pick is spent the click sells the joker as usual.
        /// Returns true if it handled the click.</summary>
        private bool TryHileliZarFromBar(Mouse mouse)
        {
            int index = jokerBar.JokerIndexAt(mouse.position.ReadValue());
            if (index < 0 || index >= session.Jokers.Count)
            {
                return false;
            }
            var zar = session.Jokers.Jokers[index] as HileliZarJoker;
            if (zar == null || !zar.CanPickOpeningHand)
            {
                return false;
            }
            hileliPickMode = true;
            hileliSelection.Clear();
            hileliTarget = Mathf.Max(1, session.Config.Rules.HandSize);
            hileliJokerId = zar.InstanceId;
            ShowHileliPicker();
            messageText.text = Loc.Pick(
                "Hileli Zar: pick " + hileliTarget + " cards for next round's opening hand",
                "Hileli Zar: sonraki elin için " + hileliTarget + " kart seç");
            return true;
        }

        /// <summary>(Re)draws the Hileli Zar picker overlay with the current selection so the
        /// highlights and the CONFIRM counter stay in sync as cards are toggled.</summary>
        private void ShowHileliPicker()
        {
            deckOverlay.ShowPicker(session.OwnedCards, hileliSelection, hileliTarget,
                Loc.Pick("Hileli Zar: pick " + hileliTarget + " cards, then CONFIRM",
                    "Hileli Zar: " + hileliTarget + " kart seç, sonra ONAYLA"));
        }

        private void ConfirmHileliZar()
        {
            session.TryPickOpeningHand(hileliJokerId, hileliSelection);
            hileliPickMode = false;
            deckOverlay.Hide();
            jokerBar.Refresh(session, null);
            sfx.Buy();
            Debug.Log("[block_bonk] Hileli Zar opening hand set: " + hileliSelection.Count + " cards");
            UpdateHud();
        }

        /// <summary>In the market, clicking an unbound Parazit starts the attach flow instead
        /// of selling it. Returns true if it handled the click.</summary>
        private bool TryStartParazitAttach(Mouse mouse)
        {
            int index = jokerBar.JokerIndexAt(mouse.position.ReadValue());
            if (index < 0 || index >= session.Jokers.Count)
            {
                return false;
            }
            var parazit = session.Jokers.Jokers[index] as ParazitJoker;
            if (parazit == null || parazit.HasBinding)
            {
                return false;
            }
            parazitStep = ParazitStep.PickJoker;
            parazitInstanceId = parazit.InstanceId;
            messageText.text = Loc.Pick(
                "Parazit: click the joker to attach   [Esc] cancel",
                "Parazit: takılacak jokere tıkla   [Esc] iptal");
            return true;
        }

        /// <summary>Drives the three picks of a Parazit attach (joker -> owned card -> cube).</summary>
        private void HandleParazitFlow(Mouse mouse)
        {
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            switch (parazitStep)
            {
                case ParazitStep.PickJoker:
                {
                    int ji = jokerBar.JokerIndexAt(mouse.position.ReadValue());
                    if (ji < 0 || ji >= session.Jokers.Count)
                    {
                        return;
                    }
                    Joker chosen = session.Jokers.Jokers[ji];
                    if (chosen.InstanceId == parazitInstanceId || chosen.Attachment.HasValue)
                    {
                        return; // Parazit cannot ride itself or an already-bound joker
                    }
                    parazitTargetJoker = chosen.InstanceId;
                    parazitStep = ParazitStep.PickCard;
                    deckOverlay.ResetScroll();
                    deckOverlay.Show(session.OwnedCards);
                    messageText.text = Loc.Pick(
                        "Parazit: pick a deck card   [Esc] cancel",
                        "Parazit: desteden bir kart seç   [Esc] iptal");
                    break;
                }
                case ParazitStep.PickCard:
                {
                    BlockCard card = deckOverlay.CardAt(world);
                    if (card == null)
                    {
                        return;
                    }
                    parazitCardId = card.Id;
                    deckOverlay.Hide();
                    parazitStep = ParazitStep.PickCube;
                    cubePicker.Show(card.Shape, Loc.Pick(
                        "Parazit: pick the host cube   [Esc] cancel",
                        "Parazit: konak küpü seç   [Esc] iptal"));
                    break;
                }
                case ParazitStep.PickCube:
                {
                    int cellIndex = cubePicker.CellAt(world);
                    if (cellIndex < 0)
                    {
                        return;
                    }
                    bool ok = session.TryAttachJokerToCard(parazitTargetJoker, parazitCardId, cellIndex);
                    Debug.Log("[block_bonk] Parazit attach " + (ok ? "succeeded" : "failed"));
                    if (ok)
                    {
                        sfx.Buy();
                    }
                    cubePicker.Hide();
                    parazitStep = ParazitStep.None;
                    marketView.Show(session);
                    jokerBar.Refresh(session, null);
                    UpdateHud();
                    break;
                }
            }
        }

        private void CancelParazit()
        {
            parazitStep = ParazitStep.None;
            parazitInstanceId = 0;
            parazitTargetJoker = 0;
            parazitCardId = 0;
            cubePicker.Hide();
            deckOverlay.Hide();
            if (session != null && session.Phase == GamePhase.Market)
            {
                marketView.Show(session);
            }
            UpdateHud();
        }

        private void HandleMarketClick(Mouse mouse)
        {
            if (TryStartParazitAttach(mouse))
            {
                return;
            }
            if (TryHileliZarFromBar(mouse))
            {
                return;
            }
            if (TrySellJokerFromBar(mouse))
            {
                return;
            }
            if (TrySellPowerFromBar(mouse))
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            // DEMO shelf buttons come FIRST: they sit in a section header and on the footer,
            // where no offer is, but a click that fell through to the deck-pile branch below
            // would open the sell screen behind the market.
            if (marketView.TryProceedAt(world) || marketView.TryRerollAt(world))
            {
                return;
            }
            // The stacked shelf's own DECK button. It stands in for the deck PILE, which that
            // layout covers - so it does exactly what clicking the pile does.
            if (marketView.TryDeckAt(world))
            {
                sellCardsMode = true;
                deckOverlay.ResetScroll();
                deckOverlay.Show(session.OwnedCards, true);
                return;
            }
            int offerIndex = marketView.OfferAt(world);
            if (offerIndex < 0)
            {
                // clicking the deck opens the owned cards as a SELL screen
                if (cardLayer.IsDrawPileAt(world))
                {
                    sellCardsMode = true;
                    deckOverlay.ResetScroll(); // a fresh visit starts at the top of the deck
                    deckOverlay.Show(session.OwnedCards, true);
                }
                return;
            }
            // "Kaçakçı": hold SHIFT to take the offer for free instead of paying for it. One per
            // market visit, and the goods may be junk - which is why it is a deliberate modifier
            // and not the default click.
            Keyboard keys = Keyboard.current;
            BuyOffer(offerIndex, session.CanSmuggle && keys != null
                && (keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed));
        }

        /// <summary>Selling one card off the collection screen, and everything the view does
        /// about it. Reached by a click and by the pad's A (see .PadPanels) - one route, so the
        /// sale, the sound, the floating price and the two rebuilds can never disagree.</summary>
        private void SellCardFromDeck(BlockCard card, Vector2 world)
        {
            if (card == null)
            {
                return;
            }
            deckOverlay.PlaySellFx(card); // before the rebuild eats the visual
            long paid = session.SellCard(card);
            Debug.Log("[block_bonk] Sold card " + card + " for " + paid);
            if (paid > 0)
            {
                sfx.Buy();
                FloatingTextFx.Spawn(transform, world, "+" + paid,
                    new Color(1f, 0.92f, 0.45f), 60, 0.05f);
            }
            else
            {
                FloatingTextFx.Spawn(transform, world, Loc.Pick("worthless", "değersiz"),
                    new Color(0.6f, 0.6f, 0.6f), 50, 0.045f);
            }
            deckOverlay.Show(session.OwnedCards, true);
            marketView.Show(session);
            UpdateHud();
        }

        /// <summary>
        /// What a fox may turn into: every SHAPE the owned deck holds, once each, as a stand-in
        /// FOX card so the picker shows what the block will actually look like.
        ///
        /// One per shape and not one per card, because the fox only ever took the shape: a deck
        /// with four copies of a piece used to print that piece four times and make the player
        /// scroll past its own duplicates. Ordered by size then by the shape's canonical key, so
        /// the same deck always lays the choices out the same way - the overlay sorts on size and
        /// then id, and these ids are handed out in exactly that order to agree with it.
        ///
        /// The cards are THROWAWAY: they are not in OwnedCards, they carry no elements but Fox,
        /// and nothing may sell or count them. Only their Shape leaves this screen
        /// (DeckOverlayView.ShapeAt / EntryShape -> ApplyFoxShape).
        /// </summary>
        private List<BlockCard> FoxShapeChoices()
        {
            var shapes = new List<BlockShape>();
            var seen = new HashSet<string>();
            foreach (BlockCard card in session.OwnedCards)
            {
                if (seen.Add(card.Shape.CanonicalKey))
                {
                    shapes.Add(card.Shape);
                }
            }
            shapes.Sort((a, b) => a.Size != b.Size
                ? a.Size - b.Size
                : string.CompareOrdinal(a.CanonicalKey, b.CanonicalKey));
            var choices = new List<BlockCard>(shapes.Count);
            for (int i = 0; i < shapes.Count; i++)
            {
                choices.Add(new BlockCard(i + 1, shapes[i], FoxOnly));
            }
            return choices;
        }

        /// <summary>The element every fox choice wears. One array, because the list is rebuilt
        /// every time the picker opens.</summary>
        private static readonly BlockElement[] FoxOnly = { BlockElement.Fox };

        /// <summary>Giving a fox the shape that was picked for it, and closing the screen that
        /// picked it. Null (a press on no card) still closes - the pick is one shot.</summary>
        private void ApplyFoxShape(BlockShape picked)
        {
            RoundEngine pickRound = session.CurrentRound;
            BlockCard foxCard = CardOfSlot(pickRound, foxPickSlot);
            if (picked != null && foxCard != null && pickRound.Status == RoundStatus.InProgress)
            {
                pickRound.SetFoxShape(foxPickSlot, picked);
                cardLayer.ForgetCard(foxCard.Id);
                Debug.Log("[block_bonk] Fox reshaped to " + picked);
            }
            foxPickSlot = -1;
            deckOverlay.Hide();
            RefreshAll(null);
        }

        /// <summary>"Hileli zar": a card TOGGLES in and out of next round's opening hand -
        /// deselect if picked, else select while there is still room. The overlay is rebuilt so
        /// the highlights and the counter follow.</summary>
        private void ToggleHileliPick(BlockCard card)
        {
            if (card == null)
            {
                return;
            }
            if (hileliSelection.Contains(card.Id))
            {
                hileliSelection.Remove(card.Id);
                ShowHileliPicker();
            }
            else if (hileliSelection.Count < hileliTarget)
            {
                hileliSelection.Add(card.Id);
                ShowHileliPicker();
            }
        }

        /// <summary>Refreshing one shelf. The mouse holds the section (or presses its REROLL
        /// button), the gamepad presses X on it - one route either way, and the view still
        /// never touches money or the market itself.</summary>
        private void RerollSection(MarketOfferKind kind)
        {
            if (session != null && session.RerollMarket(kind))
            {
                sfx.Buy();   // the buy "ka-ching" doubles as the refresh
                marketView.Show(session);
                UpdateHud();
            }
        }

        /// <summary>Leaving the shop for the next stage - the one thing [N], the PROCEED button
        /// and the pad's north button all have to do identically.</summary>
        private void LeaveMarketNow()
        {
            if (session == null || session.Phase != GamePhase.Market)
            {
                return;
            }
            session.LeaveMarket();
            marketView.Hide();
            StartRoundPresentation();
        }

        /// <summary>Taking one offer off the shelf, and everything the view does about it. The
        /// mouse reaches it through a click and the gamepad through its own stepping (see
        /// .PadPlay) - both come here, so a bought joker flies to its bar either way.</summary>
        private void BuyOffer(int offerIndex, bool smuggling)
        {
            if (offerIndex < 0 || offerIndex >= session.Market.Offers.Count)
            {
                return;
            }
            MarketOffer offer = session.Market.Offers[offerIndex];
            if (smuggling ? session.TrySmuggleOffer(offerIndex) : session.TryBuyOffer(offerIndex))
            {
                Debug.Log("[block_bonk] " + (smuggling ? "Smuggled " : "Bought ") + offer
                    + " for " + (smuggling ? 0 : offer.Price));
                sfx.Buy();
                if (offer.Kind == MarketOfferKind.Joker)
                {
                    // fly the tile up toward the joker bar (top-right of the view)
                    Vector2 barWorld = cam.ViewportToWorldPoint(new Vector3(0.9f, 0.92f, -cam.transform.position.z));
                    marketView.PlayJokerBuyFx(offerIndex, barWorld);
                }
                else if (offer.Kind == MarketOfferKind.Power)
                {
                    // fly toward the power bar (left side, above the discard pile)
                    marketView.PlayPowerBuyFx(offerIndex,
                        CardLayerView.DiscardPilePos + new Vector2(0f, 2.4f));
                }
                else
                {
                    marketView.PlayBuyFx(offerIndex);
                }
                marketView.Show(session);
                UpdateHud();
                if (offer.Kind == MarketOfferKind.Joker || smuggling)
                {
                    // A SMUGGLE always touches the joker bar even when the goods were a block:
                    // "Kaçakçı" counts the haul, and its third sound one takes it off the bar.
                    jokerBar.Refresh(session, null);
                }
                if (offer.Kind == MarketOfferKind.Power)
                {
                    powerBar.Refresh(session, null);
                }
            }
            else
            {
                Debug.Log("[block_bonk] Cannot buy offer " + offerIndex + " (sold or too expensive).");
            }
        }

        /// <summary>Debug joker controls, standing in for the market: J grants the next
        /// joker in the registry, K sells the last one, 1-9 activate. Returns true when the
        /// key was consumed and the rest of Update should be skipped this frame.</summary>
        private bool HandleJokerInput(Keyboard kb)
        {
            if (kb == null)
            {
                return false;
            }
            if (kb.escapeKey.wasPressedThisFrame
                && (pendingTargetJokerId.HasValue || pendingTargetPowerId.HasValue))
            {
                CancelTargeting();
                return true;
            }
            if (kb.jKey.wasPressedThisFrame && draggedCard == null)
            {
                grantPicker.ShowJokers();
                return true;
            }
            if (kb.pKey.wasPressedThisFrame && draggedCard == null)
            {
                grantPicker.ShowPowers();
                return true;
            }
            if (kb.kKey.wasPressedThisFrame && session.Jokers.Count > 0)
            {
                Joker last = session.Jokers.Jokers[session.Jokers.Count - 1];
                int paid = session.Jokers.Sell(last);
                Debug.Log("[block_bonk] Joker sold: " + last.DisplayName + " for " + paid);
                pendingTargetJokerId = null;
                RefreshAll(null);
                return true;
            }

            if (session.Phase != GamePhase.Round)
            {
                return false;
            }
            ButtonControl[] digits =
            {
                kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key,
                kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key
            };
            for (int i = 0; i < digits.Length && i < session.Jokers.Count; i++)
            {
                if (digits[i].wasPressedThisFrame)
                {
                    BeginActivation(session.Jokers.Jokers[i]);
                    return true;
                }
            }
            return false;
        }
    }
}
