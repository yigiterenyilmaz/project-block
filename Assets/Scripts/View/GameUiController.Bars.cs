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
            long paid = session.Jokers.Sell(joker);
            Debug.Log("[project_block] Sold joker " + joker.DisplayName + " for " + paid);
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

        /// <summary>In the market, clicking a power panel sells it for its BaseSellValue.</summary>
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
            Debug.Log("[project_block] Sold power " + power.DisplayName + " for " + paid);
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

        /// <summary>In the market, clicking a charged "Hileli zar" power opens the opening-hand
        /// picker instead of selling it. Returns true if it handled the click.</summary>
        private bool TryHileliZarFromBar(Mouse mouse)
        {
            int index = powerBar.PowerIndexAt(mouse.position.ReadValue());
            if (index < 0 || index >= session.Powers.Count)
            {
                return false;
            }
            Power power = session.Powers.Powers[index];
            if (power.DefId != "hileli_zar" || !power.Charged)
            {
                return false;
            }
            hileliPickMode = true;
            hileliSelection.Clear();
            hileliTarget = Mathf.Max(1, session.Config.Rules.HandSize);
            hileliPowerId = power.InstanceId;
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
            session.SetPendingOpeningHand(hileliSelection);
            session.Powers.Spend(hileliPowerId);
            hileliPickMode = false;
            deckOverlay.Hide();
            powerBar.Refresh(session, null);
            sfx.Buy();
            Debug.Log("[project_block] Hileli Zar opening hand set: " + hileliSelection.Count + " cards");
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
                    Debug.Log("[project_block] Parazit attach " + (ok ? "succeeded" : "failed"));
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
            if (TrySellJokerFromBar(mouse))
            {
                return;
            }
            if (TryHileliZarFromBar(mouse))
            {
                return;
            }
            if (TrySellPowerFromBar(mouse))
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            if (marketView.RerollButtonAt(world))
            {
                if (session.RerollMarket())
                {
                    sfx.Buy(); // reuse the buy "ka-ching" for the reroll
                    marketView.Show(session);
                    UpdateHud();
                }
                return;
            }
            int offerIndex = marketView.OfferAt(world);
            if (offerIndex < 0)
            {
                // clicking the deck opens the owned cards as a SELL screen
                if (cardLayer.IsDrawPileAt(world))
                {
                    sellCardsMode = true;
                    deckOverlay.Show(session.OwnedCards, c => session.Config.Market.SellValue(c));
                }
                return;
            }
            MarketOffer offer = session.Market.Offers[offerIndex];
            if (session.TryBuyOffer(offerIndex))
            {
                Debug.Log("[project_block] Bought " + offer + " for " + offer.Price);
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
                if (offer.Kind == MarketOfferKind.Joker)
                {
                    jokerBar.Refresh(session, null);
                }
                else if (offer.Kind == MarketOfferKind.Power)
                {
                    powerBar.Refresh(session, null);
                }
            }
            else
            {
                Debug.Log("[project_block] Cannot buy offer " + offerIndex + " (sold or too expensive).");
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
                Debug.Log("[project_block] Joker sold: " + last.DisplayName + " for " + paid);
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
