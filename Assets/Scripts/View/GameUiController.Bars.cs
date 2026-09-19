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

        /// <summary>
        /// Where a popup about a BAR CARD may actually be seen.
        ///
        /// The bars are uGUI on a screen-space-overlay canvas and FloatingTextFx is a world-space
        /// TextMesh - and an overlay canvas composites over ALL world geometry, whatever sorting
        /// order the text asks for. So a popup spawned at a panel's own position is drawn behind
        /// the card it is about, every time.
        ///
        /// Rather than move the popups onto a canvas, they are nudged INWARD off the bar: the
        /// joker strip is down the right edge and the power strip down the left, so pushing
        /// toward the middle puts the text over the board where nothing covers it - and next to
        /// the card it belongs to, which is where it wants to be anyway.
        /// </summary>
        private Vector2 BarPopupAnchor(Vector2? panelScreen, bool fromRight)
        {
            if (!panelScreen.HasValue)
            {
                return Vector2.zero;
            }
            Vector2 world = cam.ScreenToWorldPoint(panelScreen.Value);
            // JUST CLEAR OF THE CARD, not across the screen. The first pass pushed it most of a
            // camera height inward, which on a desktop landed the message over the market panel
            // in the middle of the screen - nowhere near the card it was about. A card is about
            // one world unit wide, so a little over that takes the popup off it and no further.
            float inward = cam.orthographicSize * 0.24f;
            world.x += fromRight ? -inward : inward;
            return world;
        }

        /// <summary>In the market, a HELD press on a joker panel sells it for its SellValue.
        /// Reached only from HandleBarHold, which owns every press on a bar card.</summary>
        private bool SellJokerAt(int index)
        {
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
                    Vector2 at = BarPopupAnchor(panelScreen, true);
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
                FloatingTextFx.Spawn(transform, BarPopupAnchor(panelScreen, true), "+" + paid,
                    new Color(1f, 0.92f, 0.45f), 60, 0.05f);
            }
            marketView.Show(session);
            jokerBar.AnimateJokerSold(index, session); // shrink, then refresh the strip
            UpdateHud();
            return true;
        }

        /// <summary>In the market, a HELD press on a power panel sells it for its sell value.
        /// Reached only from HandleBarHold, which owns every press on a bar card.</summary>
        private bool SellPowerAt(int index)
        {
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
                FloatingTextFx.Spawn(transform, BarPopupAnchor(panelScreen, false), "+" + paid,
                    new Color(1f, 0.92f, 0.45f), 60, 0.05f);
            }
            marketView.Show(session); // affordability colors follow the new balance
            powerBar.AnimatePowerSold(index, session);
            UpdateHud();
            return true;
        }

        /// <summary>
        /// Opens the "Hileli zar" opening-hand picker. A TAP on the card gets here (a hold sells
        /// it instead - see HandleBarHold).
        ///
        /// THE TARGET IS THE LIVE HAND SIZE, and it has to be: RoundRules.HandSize is a shared
        /// mutable field that jokers move permanently ("Cömertlik" +1, "Risk" +2) and that
        /// "İmitasyon" drops to 1 at the END of every round, so the next round's opening hand is
        /// only knowable by asking now. Reading a constant 3 here would deal the wrong number of
        /// cards for any of them.
        ///
        /// It is also clamped to the DECK: a hand of five out of a four-card deck is a target the
        /// player can never reach, and CONFIRM would never light.
        /// </summary>
        private void StartHileliPick(HileliZarJoker zar)
        {
            hileliPickMode = true;
            hileliSelection.Clear();
            hileliTarget = HileliTargetNow();
            hileliJokerId = zar.InstanceId;
            jokerBar.Refresh(session, null);
            ShowHileliPicker();
            messageText.text = Loc.Pick(
                "Hileli Zar: pick " + hileliTarget + " cards for next round's opening hand",
                "Hileli Zar: sonraki elin için " + hileliTarget + " kart seç");
        }

        /// <summary>How many cards next round will actually open with, right now. One definition,
        /// so the header, the counter and CONFIRM can never disagree.</summary>
        private int HileliTargetNow()
        {
            int wanted = Mathf.Max(1, session.Config.Rules.HandSize);
            return Mathf.Min(wanted, Mathf.Max(1, session.OwnedCards.Count));
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
            // The RESULT is checked: TryPickOpeningHand refuses a pick the session does not allow
            // (the id is not a held Hileli zar, or this market's deal is already spent), and
            // closing the picker as though it had worked is how a silently lost pick happens.
            bool dealt = session.TryPickOpeningHand(hileliJokerId, hileliSelection);
            hileliPickMode = false;
            EndHileliPick();
            if (dealt)
            {
                sfx.Buy();
                Debug.Log("[block_bonk] Hileli Zar opening hand set: "
                    + hileliSelection.Count + " cards");
            }
            else
            {
                Debug.LogWarning("[block_bonk] Hileli Zar pick refused by the session");
            }
            UpdateHud();
        }

        /// <summary>Closes the picker and gives the bar its breath back. Every way out of the
        /// pick goes through here - confirming, cancelling, or the panel being torn down - so the
        /// suppression can never be left on with no panel to justify it.</summary>
        private void EndHileliPick()
        {
            hileliPickMode = false;
            deckOverlay.Hide();
            jokerBar.Refresh(session, null);
        }

        /// <summary>In the market, a TAP on an unbound Parazit opens its attach panel.</summary>
        private void StartParazitAttachAt(int index)
        {
            var parazit = session.Jokers.Jokers[index] as ParazitJoker;
            if (parazit == null || parazit.HasBinding)
            {
                return;
            }
            OpenParazitPanel(parazit);
        }

        /// <summary>The whole bind on one panel (ParasiteAttachView): who rides, which block,
        /// which cube. The candidates are every joker but Parazit and any already riding.</summary>
        private void OpenParazitPanel(ParazitJoker parazit)
        {
            var candidates = new List<Joker>();
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].InstanceId != parazit.InstanceId && !owned[i].Attachment.HasValue)
                {
                    candidates.Add(owned[i]);
                }
            }
            parazitStep = ParazitStep.PickJoker;
            parazitInstanceId = parazit.InstanceId;
            HideTooltip();
            parasitePanel.Show(candidates, session.OwnedCards);
            jokerBar.Refresh(session, null);
            messageText.text = Loc.Pick("Parazit: pick who rides, the block and the cube   [Esc] cancel",
                "Parazit: binecek jokeri, bloğu ve küpü seç   [Esc] iptal");
        }

        /// <summary>One frame of the attach panel. It owns the pointer while it is open.</summary>
        private void HandleParazitFlow(Mouse mouse)
        {
            if (mouse == null || !parasitePanel.IsOpen)
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            float scroll = mouse.scroll.ReadValue().y;
            ParasiteAttachView.Action action = parasitePanel.Handle(world,
                mouse.leftButton.wasPressedThisFrame, scroll);
            if (action == ParasiteAttachView.Action.Cancel)
            {
                CancelParazit();
                return;
            }
            if (action != ParasiteAttachView.Action.Confirm)
            {
                return;
            }
            bool ok = session.TryAttachJokerToCard(parasitePanel.SelectedJokerId,
                parasitePanel.SelectedCardId, parasitePanel.SelectedCell);
            Debug.Log("[block_bonk] Parazit attach " + (ok ? "succeeded" : "failed"));
            if (ok)
            {
                sfx.Buy();
            }
            CancelParazit();
        }

        private void CancelParazit()
        {
            parazitStep = ParazitStep.None;
            parazitInstanceId = 0;
            parazitTargetJoker = 0;
            parazitCardId = 0;
            parasitePanel.Hide();
            cubePicker.Hide();
            deckOverlay.Hide();
            if (session != null && session.Phase == GamePhase.Market)
            {
                marketView.Show(session);
            }
            if (session != null)
            {
                jokerBar.Refresh(session, null);
            }
            UpdateHud();
        }

        /// <summary>Last Joker.LooseProcs seen per joker instance.</summary>
        private readonly Dictionary<int, int> looseProcsSeen = new Dictionary<int, int>();

        /// <summary>
        /// Flashes a joker whose proc had no turn to ride on (a round start, a sale, a use
        /// between turns) - the same light TurnReport.ProcedJokers gives the rest. A joker seen
        /// for the first time is only recorded, so a load or a purchase never flashes.
        /// </summary>
        private void PlayLooseProcs()
        {
            if (session == null || session.Jokers == null || jokerBar == null)
            {
                return;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                Joker joker = owned[i];
                int seen;
                if (looseProcsSeen.TryGetValue(joker.InstanceId, out seen) && joker.LooseProcs > seen)
                {
                    jokerBar.ProcJoker(joker.InstanceId);
                }
                looseProcsSeen[joker.InstanceId] = joker.LooseProcs;
            }
        }

        // ---------------------------------------------------------------- press and hold to sell
        //
        // SELLING IS THE DESTRUCTIVE VERB, SO IT IS THE ONE THAT COSTS EFFORT. A single click on a
        // bar card used to sell it outright, which put an irreversible action on the twitchiest
        // input in the game - and it meant a joker that had something to DO in the market
        // ("Hileli zar", "Parazit") had to intercept the click before the sale, so which of the
        // two happened depended on invisible state.
        //
        // Now the two verbs are told apart by TIME rather than by state: a TAP uses the card, a
        // HOLD sells it. Both are always available, for every joker and every power, so a card
        // whose pick is spent still cannot be sold by accident and one with a pick left can still
        // be sold without spending it first.
        //
        // The hold is armed on press. A tap is settled on RELEASE; a hold sells the moment the
        // gauge fills, button still down. Dragging off the card before then cancels it, which is
        // the standard escape hatch for a press-and-hold.

        /// <summary>How long a bar card must be held for the press to become a SALE. Long enough
        /// that it cannot be hit by a click, short enough not to feel like a punishment.</summary>
        private const float SellHoldSeconds = 0.55f;

        /// <summary>Which bar the current press is on, or None. Index is into that bar.</summary>
        private enum HeldBar
        {
            None,
            Joker,
            Power
        }

        private HeldBar heldBar = HeldBar.None;
        private int heldIndex = -1;
        private float heldSince;

        /// <summary>True once the press has been held long enough to be a sale.</summary>
        private bool HeldLongEnough
        {
            get { return heldBar != HeldBar.None && Time.unscaledTime - heldSince >= SellHoldSeconds; }
        }

        /// <summary>
        /// Called every frame in the market. Arms a press on a bar card, keeps the hold light on
        /// it, and settles the press: a tap USES on release, a completed hold SELLS as soon as it fills.
        ///
        /// Returns true on the frames it owned the press, so the market's other click handlers
        /// (the shelf, the deck button, the piles) only ever see presses that were not on a bar.
        /// </summary>
        private bool HandleBarHold(Mouse mouse)
        {
            if (mouse == null || session == null)
            {
                return false;
            }
            if (mouse.leftButton.wasPressedThisFrame && heldBar == HeldBar.None)
            {
                Vector2 at = mouse.position.ReadValue();
                int ji = jokerBar.JokerIndexAt(at);
                int pi = ji < 0 ? powerBar.PowerIndexAt(at) : -1;
                if (ji >= 0 && ji < session.Jokers.Count)
                {
                    heldBar = HeldBar.Joker;
                    heldIndex = ji;
                }
                else if (pi >= 0 && pi < session.Powers.Count)
                {
                    heldBar = HeldBar.Power;
                    heldIndex = pi;
                }
                else
                {
                    return false;
                }
                heldSince = Time.unscaledTime;
                return true;
            }
            if (heldBar == HeldBar.None)
            {
                return false;
            }
            // Dragging off the card abandons the press - nothing is used and nothing is sold.
            Vector2 now = mouse.position.ReadValue();
            bool stillOn = heldBar == HeldBar.Joker
                ? jokerBar.JokerIndexAt(now) == heldIndex
                : powerBar.PowerIndexAt(now) == heldIndex;
            if (!stillOn)
            {
                ClearBarHold();
                return true;
            }
            // A FULL gauge sells at once, button still down - making the player let go after the
            // gauge has already said "sold" was one step too many. Dragging off before it fills
            // is still the way out.
            bool sell = HeldLongEnough;
            if (mouse.leftButton.isPressed && !sell)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - heldSince) / SellHoldSeconds);
                ShowHoldProgress(progress);
                return true;
            }
            HeldBar bar = heldBar;
            int index = heldIndex;
            ClearBarHold();
            if (bar == HeldBar.Joker)
            {
                if (sell)
                {
                    SellJokerAt(index);
                }
                else
                {
                    UseJokerInMarket(index);
                }
            }
            else if (sell)
            {
                SellPowerAt(index);
            }
            else
            {
                // A power has nothing to do in the market, so a tap only says so - silently
                // doing nothing reads as a dropped click.
                HintHoldToSell(powerBar.PanelScreenCenter(index), false);
            }
            return true;
        }

        private void ClearBarHold()
        {
            heldBar = HeldBar.None;
            heldIndex = -1;
            ShowHoldProgress(0f);
        }

        private void ShowHoldProgress(float progress)
        {
            jokerBar.SetHoldProgress(heldBar == HeldBar.Joker ? heldIndex : -1, progress);
            powerBar.SetHoldProgress(heldBar == HeldBar.Power ? heldIndex : -1, progress);
        }

        /// <summary>A tap on something with no market action: say what a hold would do. Without
        /// it the player has no way to discover the gesture.</summary>
        private void HintHoldToSell(Vector2? panelScreen)
        {
            HintHoldToSell(panelScreen, true);
        }

        private void HintHoldToSell(Vector2? panelScreen, bool fromRight)
        {
            if (!panelScreen.HasValue)
            {
                return;
            }
            Vector2 world = BarPopupAnchor(panelScreen, fromRight);
            FloatingTextFx.Spawn(transform, world,
                Loc.Pick("HOLD TO SELL", "SATMAK İÇİN BASILI TUT"),
                new Color(0.85f, 0.88f, 0.95f), 40, 0.06f);
        }

        /// <summary>
        /// "Kaçakçı" is ARMED and waiting for the shelf to be clicked.
        ///
        /// It is a MODE rather than a panel, which is what makes it different from the other two
        /// market jokers: nothing is picked from a list, the next ordinary purchase simply costs
        /// nothing. So there is no overlay to suppress the card's breath, and the breath is what
        /// says the joker still has a haul in it - the ARMED state gets its own message instead.
        /// </summary>
        private bool smuggleArmed;

        /// <summary>Arms the free item. Cleared by taking it, by leaving the market, and by any
        /// click that is not an offer - an armed mode that outlives the screen it was armed on is
        /// a mode that will spend itself on something the player did not mean.</summary>
        private void ArmSmuggle()
        {
            smuggleArmed = true;
            messageText.text = Loc.Pick(
                "KAÇAKÇI ARMED - click anything on the shelf and it is FREE   [Esc] cancel",
                "KAÇAKÇI HAZIR - raftan neye tıklarsan BEDAVA   [Esc] iptal");
        }

        /// <summary>A TAP on a joker in the market: whatever that joker's market verb is, or a
        /// hint that it has none. The two market jokers keep their own flows.</summary>
        private void UseJokerInMarket(int index)
        {
            if (index < 0 || index >= session.Jokers.Count)
            {
                return;
            }
            Joker joker = session.Jokers.Jokers[index];
            var parazit = joker as ParazitJoker;
            if (parazit != null && !parazit.HasBinding)
            {
                StartParazitAttachAt(index);
                return;
            }
            var zar = joker as HileliZarJoker;
            if (zar != null && zar.CanPickOpeningHand)
            {
                StartHileliPick(zar);
                return;
            }
            var kacakci = joker as KacakciJoker;
            if (kacakci != null && session.CanSmuggle)
            {
                ArmSmuggle();
                return;
            }
            HintHoldToSell(jokerBar.PanelScreenCenter(index));
        }

        private void HandleMarketClick(Mouse mouse)
        {
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            // DEMO shelf buttons come FIRST: they sit in a section header and on the footer,
            // where no offer is, but a click that fell through to the deck-pile branch below
            // would open the sell screen behind the market.
            if (marketView.TryProceedAt(world) || marketView.TryRerollAt(world))
            {
                return;
            }
            // The shelf's own DECK button, and the draw pile itself where it is on screen (the
            // desktop panel stops short of it; the stacked one hides it). Both open the owned
            // cards as a SELL screen. The pile only answers while it is visible - a hidden pile
            // that opened the sell screen would be an invisible button.
            if (marketView.TryDeckAt(world)
                || (!UiLayout.Active.MarketStacked && marketView.OfferAt(world) < 0
                    && cardLayer.IsDrawPileAt(world)))
            {
                sellCardsMode = true;
                deckOverlay.ResetScroll();
                deckOverlay.Show(session.OwnedCards, true);
                return;
            }
            int offerIndex = marketView.OfferAt(world);
            if (offerIndex < 0)
            {
                return;
            }
            // "Kaçakçı": the free item is ARMED by clicking the joker and spent by the next
            // thing bought. See ArmSmuggle - it used to be SHIFT+click, which is a modifier no
            // player was ever going to find on a joker whose whole point is that it is there.
            bool free = smuggleArmed && session.CanSmuggle;
            smuggleArmed = false;
            BuyOffer(offerIndex, free);
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
