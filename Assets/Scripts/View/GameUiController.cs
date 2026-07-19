// PURPOSE: The single scene component (lives in Assets/Scenes/enes.unity). Creates the
// GameSession, builds the debug UI at runtime (no scene-authored visuals), and turns
// player input into calls on the core engine.
// CONTROLS: drag a card from the hand onto the board to place it,
//           A = advance / C = continue on an offer, N = leave market, R = new run,
//           D = deck select, J = grant the next joker, K = sell the last joker,
//           1-9 = activate that joker (one that needs a target then waits for a click,
//           Esc cancels). Click a power in the left bar to use it (same targeting flow);
//           in the market, clicking a joker or power panel sells it.
// NOTE FOR AGENTS: this is placeholder presentation. Extend gameplay in
// ProjectBlock.Core; only wiring/visuals belong here. The J/K joker keys stand in until
// jokers can be bought in the market - delete them then.

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
    /// <summary>Bootstrap + input + HUD for the debug UI.</summary>
    public sealed class GameUiController : MonoBehaviour
    {
        [SerializeField] private int seed = 0; // 0 = random seed every run
        [SerializeField] private float maxBoardWorldSize = 6.5f;
        [SerializeField] private bool verboseTurnLogs = true;

        private static readonly Vector2 BoardCenter = new Vector2(0f, 0.9f);

        private GameSession session;
        private BoardView boardView;
        private CardLayerView cardLayer;
        private DeckOverlayView deckOverlay;
        private DeckSelectView deckSelect;
        private MarketView marketView;
        private JokerBarView jokerBar;
        private PowerBarView powerBar;
        private DeckDefinition currentDeck = DeckLibrary.Classic;
        private SoundFx sfx;
        private FlameStreakView flameStreak;
        private BlastFxView blastFx;
        private int comboStreak;
        private Text infoText;
        private Text messageText;
        private Camera cam;
        private Vector3 camBasePosition;
        private Coroutine shakeRoutine;
        private CardVisual draggedCard;
        private int foxPickSlot = -1;
        private bool waterAnimating;
        private bool sellCardsMode;
        private int lastSeedUsed;

        // Hover tooltip (world-space): a small panel rebuilt only when the target changes.
        private GameObject tooltipRoot;
        private string tooltipKey;
        private float tooltipWidth;
        private float tooltipHeight;
        private static readonly Color TooltipBgColor = new Color(0.05f, 0.06f, 0.09f, 0.95f);
        private static readonly Color TooltipTitleColor = new Color(1f, 0.93f, 0.72f);
        private static readonly Color TooltipBodyColor = new Color(0.82f, 0.86f, 0.92f);

        /// <summary>Set while an activated joker waits for the player to pick a target.</summary>
        private int? pendingTargetJokerId;

        /// <summary>Set while a used power waits for the player to pick a target.</summary>
        private int? pendingTargetPowerId;

        /// <summary>Debug grant order: walks the registry so every joker is reachable.</summary>
        private int nextGrantIndex;

        private void Start()
        {
            cam = Camera.main;
            camBasePosition = cam.transform.position;
            BuildViews();
            NewGame();
        }

        public void NewGame()
        {
            lastSeedUsed = seed != 0 ? seed : System.Environment.TickCount;
            var config = new GameConfig();
            config.RngSeed = lastSeedUsed;
            config.Deck = currentDeck;
            session = new GameSession(config);
            draggedCard = null;
            foxPickSlot = -1;
            sellCardsMode = false;
            waterAnimating = false;
            pendingTargetJokerId = null;
            pendingTargetPowerId = null;
            nextGrantIndex = 0;
            marketView.Hide();
            HideTooltip();
            Debug.Log("[project_block] New run, seed " + lastSeedUsed);
            StartRoundPresentation();
        }

        /// <summary>Board + HUD refresh with the round-start shuffle-and-deal animation.</summary>
        private void StartRoundPresentation()
        {
            comboStreak = 0;
            RoundEngine round = session.CurrentRound;
            if (boardView.Board != round.Board)
            {
                boardView.Rebuild(round.Board, maxBoardWorldSize, BoardCenter);
            }
            flameStreak.SetState(round.ContinueCount, boardView.WorldRect);
            boardView.Refresh();
            boardView.ClearPreview();
            sfx.Shuffle();
            cardLayer.AnimateRoundStart(round);
            UpdateHud();
            jokerBar.Refresh(session, pendingTargetJokerId);
            powerBar.Refresh(session, pendingTargetPowerId);
        }

        private void Update()
        {
            if (session == null || waterAnimating)
            {
                return; // input is locked while the water fall animation plays
            }
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            UpdateHover(mouse);
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                deckOverlay.Hide();
                NewGame();
                return;
            }
            if (deckSelect.IsOpen)
            {
                // modal: click a deck to start a new run with it, anything else closes
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    deckSelect.Hide();
                    return;
                }
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    Vector2 clickWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    int deckIndex = deckSelect.DeckAt(clickWorld);
                    deckSelect.Hide();
                    if (deckIndex >= 0)
                    {
                        currentDeck = DeckLibrary.All[deckIndex];
                        Debug.Log("[project_block] Deck selected: " + currentDeck.Name);
                        NewGame();
                    }
                }
                return;
            }
            if (deckOverlay.IsOpen)
            {
                // modal: click picks (fox mode), sells (sell mode) or closes; Escape closes
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    foxPickSlot = -1;
                    sellCardsMode = false;
                    deckOverlay.Hide();
                    return;
                }
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    if (foxPickSlot >= 0)
                    {
                        Vector2 pickWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                        BlockShape picked = deckOverlay.ShapeAt(pickWorld);
                        RoundEngine pickRound = session.CurrentRound;
                        BlockCard foxCard = CardOfSlot(pickRound, foxPickSlot);
                        if (picked != null && foxCard != null
                            && pickRound.Status == RoundStatus.InProgress)
                        {
                            pickRound.SetFoxShape(foxPickSlot, picked);
                            cardLayer.ForgetCard(foxCard.Id);
                            Debug.Log("[project_block] Fox reshaped to " + picked);
                        }
                        foxPickSlot = -1;
                        deckOverlay.Hide();
                        RefreshAll(null);
                        return;
                    }
                    if (sellCardsMode)
                    {
                        Vector2 sellWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                        BlockCard card = deckOverlay.CardAt(sellWorld);
                        if (card != null)
                        {
                            deckOverlay.PlaySellFx(card); // before the rebuild eats the visual
                            long paid = session.SellCard(card);
                            Debug.Log("[project_block] Sold card " + card + " for " + paid);
                            if (paid > 0)
                            {
                                sfx.Buy();
                                FloatingTextFx.Spawn(transform, sellWorld, "+" + paid,
                                    new Color(1f, 0.92f, 0.45f), 60, 0.05f);
                            }
                            else
                            {
                                FloatingTextFx.Spawn(transform, sellWorld, "worthless",
                                    new Color(0.6f, 0.6f, 0.6f), 50, 0.045f);
                            }
                            deckOverlay.Show(session.OwnedCards, c => session.Config.Market.SellValue(c));
                            marketView.Show(session);
                            UpdateHud();
                            return;
                        }
                    }
                    sellCardsMode = false;
                    deckOverlay.Hide();
                }
                return;
            }
            if (kb != null && kb.dKey.wasPressedThisFrame && draggedCard == null)
            {
                deckSelect.Show(DeckLibrary.All, currentDeck);
                return;
            }
            if (HandleJokerInput(kb))
            {
                return;
            }
            switch (session.Phase)
            {
                case GamePhase.Market:
                    if (kb != null && kb.nKey.wasPressedThisFrame)
                    {
                        session.LeaveMarket();
                        marketView.Hide();
                        StartRoundPresentation();
                    }
                    else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    {
                        HandleMarketClick(mouse);
                    }
                    break;
                case GamePhase.Round:
                    RoundEngine round = session.CurrentRound;
                    if (round.Status == RoundStatus.AwaitingAdvanceDecision)
                    {
                        if (kb != null && kb.aKey.wasPressedThisFrame)
                        {
                            round.DecideAdvance(true);
                            RefreshAll(null);
                            marketView.Show(session);
                        }
                        else if (kb != null && kb.cKey.wasPressedThisFrame)
                        {
                            // continuing costs cards and redraws the hand (see RoundEngine)
                            round.DecideAdvance(false);
                            // the overtime fire ignites the moment the player chooses to
                            // continue, not on their next placement
                            flameStreak.SetState(round.ContinueCount, boardView.WorldRect);
                            if (round.Status == RoundStatus.InProgress)
                            {
                                sfx.Shuffle();
                                cardLayer.AnimateRedraw(round);
                                UpdateHud();
                            }
                            else
                            {
                                RefreshAll(null);
                            }
                        }
                    }
                    else if (round.Status == RoundStatus.InProgress)
                    {
                        if (kb != null && kb.sKey.wasPressedThisFrame)
                        {
                            // debug: discard the hand, shuffle it into the draw pile, redraw
                            round.RedrawHand();
                            sfx.Shuffle();
                            cardLayer.AnimateRedraw(round);
                            UpdateHud();
                        }
                        else if (kb != null && kb.bKey.wasPressedThisFrame)
                        {
                            // debug: random bonus card to test the bonus hand
                            BlockCard bonus = session.DebugAddRandomBonusCard();
                            Debug.Log("[project_block] Debug bonus card: " + bonus);
                            RefreshAll(null);
                        }
                        else if (TryUseJokerFromBar(mouse))
                        {
                            // clicking a joker in the top bar uses it (like the 1-9 keys)
                        }
                        else if (TryUsePowerFromBar(mouse))
                        {
                            // clicking a power in the left bar uses it (max one per turn)
                        }
                        else
                        {
                            HandleDrag(round, mouse);
                        }
                    }
                    break;
            }
        }

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

        private void HandleMarketClick(Mouse mouse)
        {
            if (TrySellJokerFromBar(mouse))
            {
                return;
            }
            if (TrySellPowerFromBar(mouse))
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
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
            if (kb.jKey.wasPressedThisFrame)
            {
                IReadOnlyList<JokerDefinition> all = JokerRegistry.All;
                JokerDefinition definition = all[nextGrantIndex % all.Count];
                nextGrantIndex++;
                Joker granted = session.Jokers.Add(definition.Create());
                Debug.Log("[project_block] Joker granted: " + granted.DisplayName
                    + " - " + granted.Description);
                RefreshAll(null);
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

        /// <summary>Runs a joker, or arms targeting mode if it must be pointed at something.</summary>
        private void BeginActivation(Joker joker)
        {
            if (!session.Jokers.CanActivate(joker.InstanceId))
            {
                Debug.Log("[project_block] " + joker.DisplayName + " cannot be used right now.");
                return;
            }
            if (joker.Targeting != ActivationTargeting.None)
            {
                pendingTargetJokerId = joker.InstanceId;
                UpdateHud();
                jokerBar.Refresh(session, pendingTargetJokerId);
                return;
            }
            RunActivation(joker, ActivationTarget.None);
        }

        private void CancelTargeting()
        {
            pendingTargetJokerId = null;
            pendingTargetPowerId = null;
            UpdateHud();
            jokerBar.Refresh(session, null);
            powerBar.Refresh(session, null);
        }

        private void RunActivation(Joker joker, ActivationTarget target)
        {
            pendingTargetJokerId = null;
            RoundEngine round = session.CurrentRound;
            // Remember which card a hand-targeted joker (İade) is about to replace, so the
            // swap can be animated after the engine has already done it.
            int replacedCardId = -1;
            if (target.HandIndex.HasValue && round != null
                && target.HandIndex.Value >= 0 && target.HandIndex.Value < round.Hand.Count)
            {
                replacedCardId = round.Hand[target.HandIndex.Value].Id;
            }
            if (!session.Jokers.TryActivate(joker.InstanceId, target))
            {
                Debug.Log("[project_block] " + joker.DisplayName + " could not be used.");
                RefreshAll(null);
                return;
            }
            Debug.Log("[project_block] Joker used: " + joker.DisplayName);
            jokerBar.PulseJoker(joker.InstanceId);
            // The whole-hand redraw has its own animation; a single-card swap flies the
            // returned card out and deals its replacement; everything else just re-syncs.
            if (joker.DefId == "renovasyon" && round.Status != RoundStatus.Lost)
            {
                sfx.Shuffle();
                cardLayer.AnimateRedraw(round);
                boardView.Refresh();
                UpdateHud();
                jokerBar.Refresh(session, null);
                return;
            }
            if (replacedCardId >= 0 && round.Status != RoundStatus.Lost)
            {
                cardLayer.AnimateReplaceCard(round, replacedCardId);
                boardView.Refresh();
                UpdateHud();
                jokerBar.Refresh(session, null);
                return;
            }
            RefreshAll(null);
        }

        /// <summary>Uses a power, or arms targeting mode if it must be pointed at something.
        /// The power twin of BeginActivation.</summary>
        private void BeginPowerActivation(Power power)
        {
            if (!session.Powers.CanBeginUse(power.InstanceId))
            {
                Debug.Log("[project_block] " + power.DisplayName + " cannot be used right now.");
                return;
            }
            if (power.Targeting != ActivationTargeting.None)
            {
                pendingTargetPowerId = power.InstanceId;
                UpdateHud();
                powerBar.Refresh(session, pendingTargetPowerId);
                return;
            }
            RunPowerActivation(power, ActivationTarget.None);
        }

        private void RunPowerActivation(Power power, ActivationTarget target)
        {
            pendingTargetPowerId = null;
            if (!session.Powers.TryUse(power.InstanceId, target))
            {
                Debug.Log("[project_block] " + power.DisplayName + " could not be used.");
                RefreshAll(null);
                return;
            }
            Debug.Log("[project_block] Power used: " + power.DisplayName);
            powerBar.PulsePower(power.InstanceId);
            // Powers can rewrite the board (inflations replace it wholesale, Kum saati
            // rewinds it) and the piles - a full resync covers every one of them.
            RefreshAll(null);
        }

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
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Power power = session.Powers.Find(pendingTargetPowerId.Value);
                    if (power == null)
                    {
                        CancelTargeting();
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
                    CardVisual hit = cardLayer.CardAt(world);
                    if (hit != null)
                    {
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
                    if (verboseTurnLogs)
                    {
                        LogTurn(report);
                    }
                    sfx.Place();
                    if (report.DiscardWasReshuffled)
                    {
                        sfx.Shuffle();
                    }
                    RefreshAll(report);
                    IReadOnlyList<IReadOnlyList<WaterMove>> frames = report.WaterFallFrames;
                    if (frames.Count == 0)
                    {
                        PlayExplosionFeedback(round, report);
                    }
                    else
                    {
                        // Water falls first, THEN the boom it caused - and any post-explosion
                        // falls play after the boom (WaterFramesBeforeExplosion splits them).
                        int boomAt = Mathf.Clamp(report.WaterFramesBeforeExplosion, 0, frames.Count);
                        var preFall = new List<IReadOnlyList<WaterMove>>();
                        var postFall = new List<IReadOnlyList<WaterMove>>();
                        for (int f = 0; f < frames.Count; f++)
                        {
                            (f < boomAt ? preFall : postFall).Add(frames[f]);
                        }
                        waterAnimating = true;
                        boardView.PlayWaterAnimation(preFall, delegate
                        {
                            PlayExplosionFeedback(round, report);
                            boardView.PlayWaterAnimation(postFall,
                                delegate { waterAnimating = false; });
                        });
                    }
                }
                else
                {
                    released.MoveTo(released.HomePosition, 0.2f, null);
                    boardView.ClearPreview();
                }
            }
        }

        /// <summary>Explosion sound + blast feedback for one turn. Deferred until after the
        /// pre-explosion water falls when the flow is what completed the line.</summary>
        private void PlayExplosionFeedback(RoundEngine round, TurnReport report)
        {
            if (report.CleanSweep)
            {
                // the sweep bling rises in pitch with every sweep this round
                sfx.CleanSweep(1f + 0.12f * Mathf.Min(round.CleanSweepCount - 1, 8));
                sfx.Flame();
            }
            else if (report.CubesExploded > 0)
            {
                sfx.Explode();
            }
            HandleBlastFeedback(round, report);
        }

        /// <summary>Particles, shake, combo popups and the sweep celebration for one turn.</summary>
        private void HandleBlastFeedback(RoundEngine round, TurnReport report)
        {
            if (report.CubesExploded == 0)
            {
                comboStreak = 0;
                return;
            }
            comboStreak++;
            EmitBlastParticles(round, report);
            // shake grows with the combo streak
            float shakeAmplitude = report.DynamiteTriggered ? 0.22f : report.CleanSweep ? 0.16f : 0.09f;
            shakeAmplitude *= 1f + 0.25f * Mathf.Min(comboStreak - 1, 5);
            ShakeCamera(shakeAmplitude, 0.2f);
            if (report.DynamiteTriggered)
            {
                FloatingTextFx.Spawn(transform, new Vector2(0f, 3.4f),
                    "DYNAMITE!", new Color(0.95f, 0.3f, 0.2f), 72, 0.09f);
            }
            if (comboStreak >= 2)
            {
                FloatingTextFx.Spawn(transform, new Vector2(0f, 2.6f),
                    "COMBO x" + comboStreak + "!", new Color(1f, 0.6f, 0.2f), 64, 0.08f);
            }
            if (report.CleanSweep)
            {
                FloatingTextFx.Spawn(transform, new Vector2(0f, 1.4f),
                    "CLEAN SWEEP!", new Color(1f, 0.85f, 0.3f), 80, 0.1f);
            }
        }

        private void EmitBlastParticles(RoundEngine round, TurnReport report)
        {
            var blastColor = new Color(1f, 0.72f, 0.35f);
            foreach (int y in report.ExplodedRows)
            {
                for (int x = 0; x < round.Board.Width; x++)
                {
                    blastFx.EmitAt(boardView.CellToWorld(new GridPos(x, y)), blastColor, 4);
                }
            }
            foreach (int x in report.ExplodedColumns)
            {
                for (int y = 0; y < round.Board.Height; y++)
                {
                    blastFx.EmitAt(boardView.CellToWorld(new GridPos(x, y)), blastColor, 4);
                }
            }
            if (report.CleanSweep)
            {
                var gold = new Color(1f, 0.85f, 0.3f);
                for (int i = 0; i < 70; i++)
                {
                    var pos = new Vector2(Random.Range(-3.2f, 3.2f), Random.Range(-2.2f, 4f));
                    blastFx.EmitAt(pos, gold, 2);
                }
            }
        }

        /// <summary>Very small camera shake for explosions (slightly bigger on clean sweeps).</summary>
        private void ShakeCamera(float amplitude, float duration)
        {
            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                cam.transform.position = camBasePosition;
            }
            shakeRoutine = StartCoroutine(ShakeRoutine(amplitude, duration));
        }

        private IEnumerator ShakeRoutine(float amplitude, float duration)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float falloff = 1f - Mathf.Clamp01(time / duration);
                Vector2 offset = Random.insideUnitCircle * (amplitude * falloff);
                cam.transform.position = camBasePosition + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }
            cam.transform.position = camBasePosition;
            shakeRoutine = null;
        }

        private static BlockCard CardOfSlot(RoundEngine round, int slot)
        {
            if (slot < 0)
            {
                return null;
            }
            if (slot < round.Hand.Count)
            {
                return round.Hand[slot];
            }
            int bonusIndex = slot - round.Hand.Count;
            return bonusIndex < round.BonusHand.Count ? round.BonusHand[bonusIndex].Card : null;
        }

        private void RefreshAll(TurnReport report)
        {
            RoundEngine round = session.CurrentRound;
            if (boardView.Board != round.Board)
            {
                boardView.Rebuild(round.Board, maxBoardWorldSize, BoardCenter);
            }
            boardView.Refresh();
            boardView.ClearPreview();
            cardLayer.Sync(round, report);
            flameStreak.SetState(round.ContinueCount, boardView.WorldRect);
            UpdateHud();
            jokerBar.Refresh(session, pendingTargetJokerId);
            powerBar.Refresh(session, pendingTargetPowerId);
        }

        private void UpdateHud()
        {
            RoundEngine round = session.CurrentRound;
            var sb = new StringBuilder();
            sb.Append("Seed ").Append(lastSeedUsed)
                .Append("   Deck: ").Append(currentDeck.Name).Append('\n');
            sb.Append("Round ").Append(session.RoundNumber).Append("   Turn ").Append(round.TurnNumber).Append('\n');
            sb.Append("Score ").Append(round.RoundScore).Append(" / ").Append(round.Config.ScoreThreshold);
            if (round.ThresholdPassed)
            {
                sb.Append("  [threshold passed]");
            }
            sb.Append('\n');
            sb.Append("Total score ").Append(session.TotalScore).Append('\n');
            sb.Append("Draw ").Append(round.Deck.DrawCount)
                .Append("   Discard ").Append(round.Deck.DiscardCount)
                .Append("   Removed ").Append(round.Deck.RemovedCount).Append('\n');
            sb.Append("Jokers ").Append(session.Jokers.Count);
            if (session.Jokers.Count > 0)
            {
                sb.Append("   (1-9 activate)");
            }
            sb.Append("   Powers ").Append(session.Powers.Count);
            if (session.Powers.Count > 0)
            {
                sb.Append("   (click to use, one per turn)");
            }
            sb.Append('\n');
            sb.Append("Drag to place.  Click draw pile: deck.  Right-click: rotate GEARS / reshape FOX\n");
            sb.Append("Debug - S: redraw hand   B: bonus card   D: choose deck   R: new run\n");
            sb.Append("Debug - J: grant joker   K: sell last joker");
            infoText.text = sb.ToString();

            if (pendingTargetJokerId.HasValue)
            {
                Joker targeting = session.Jokers.Find(pendingTargetJokerId.Value);
                string what = targeting != null && targeting.Targeting == ActivationTargeting.BoardCell
                    ? "oyun alanından bir küp seç"
                    : "elinden bir blok seç";
                messageText.text = (targeting != null ? targeting.DisplayName : "Joker")
                    + ": " + what + "\n[Esc] vazgeç";
                return;
            }
            if (pendingTargetPowerId.HasValue)
            {
                Power targeting = session.Powers.Find(pendingTargetPowerId.Value);
                string what = targeting != null && targeting.Targeting == ActivationTargeting.BoardCell
                    ? "oyun alanından bir hücre seç"
                    : "elinden bir blok seç";
                messageText.text = (targeting != null ? targeting.DisplayName : "Güç")
                    + ": " + what + "\n[Esc] vazgeç";
                return;
            }

            switch (session.Phase)
            {
                case GamePhase.GameOver:
                    messageText.text = "GAME OVER - " + DescribeLoss(round.Loss) + "\n[R] new run";
                    break;
                case GamePhase.Market:
                    messageText.text = "Click a card to add it to your deck (price below it)\n[N] start round "
                        + (session.RoundNumber + 1);
                    break;
                default:
                    if (round.Status == RoundStatus.AwaitingAdvanceDecision)
                    {
                        int continueCost = round.NextContinueCost;
                        int drawAfter = round.PredictDrawCountAfterContinue();
                        string warning = drawAfter < 0 ? "  DECK OUT!" : string.Empty;
                        messageText.text = "Threshold reached!\n[A] advance to market    [C] continue: removes "
                            + continueCost + " cards, draw pile " + round.Deck.DrawCount
                            + " -> " + Mathf.Max(drawAfter, 0) + warning;
                    }
                    else
                    {
                        messageText.text = string.Empty;
                    }
                    break;
            }
        }

        private static string DescribeLoss(LossReason? loss)
        {
            switch (loss)
            {
                case LossReason.NoPlayableMove: return "no held block fits the board";
                case LossReason.HandCannotBeRefilled: return "the deck ran out (hand could not be refilled)";
                case LossReason.DrawPileEmptyAfterThreshold: return "draw pile emptied before a clean sweep";
                default: return "unknown";
            }
        }

        private static void LogTurn(TurnReport report)
        {
            string sweep = report.CleanSweep ? ", CLEAN SWEEP" : string.Empty;
            Debug.Log("[project_block] Turn " + report.TurnNumber + ": " + report.Card
                + " at " + report.Origin + " -> +" + report.ScoreGained + " ("
                + (report.ExplodedRows.Count + report.ExplodedColumns.Count) + " lines, "
                + report.CubesExploded + " cubes" + sweep + "), status " + report.StatusAfter);
        }

        /// <summary>Picks what the mouse is hovering (market offer, deck-overlay card, or a
        /// hand card in play) and shows its info tooltip, or hides it.</summary>
        private void UpdateHover(Mouse mouse)
        {
            if (mouse == null || tooltipRoot == null || cam == null || session == null)
            {
                return;
            }
            if (draggedCard != null || deckSelect.IsOpen)
            {
                cardLayer.SetHoveredCard(-1);
                HideTooltip();
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());

            if (deckOverlay.IsOpen)
            {
                cardLayer.SetHoveredCard(-1);
                BlockCard card = deckOverlay.CardAt(world);
                if (card != null) ShowCardTooltip(card, world); else HideTooltip();
                return;
            }
            if (session.Phase == GamePhase.Market)
            {
                cardLayer.SetHoveredCard(-1);
                int index = marketView.OfferAt(world);
                if (index < 0 || index >= session.Market.Offers.Count)
                {
                    HideTooltip();
                    return;
                }
                MarketOffer offer = session.Market.Offers[index];
                if (offer.Sold)
                {
                    HideTooltip();
                }
                else if (offer.Kind == MarketOfferKind.Joker)
                {
                    ShowJokerTooltip(offer.Joker, offer.Price, world);
                }
                else if (offer.Kind == MarketOfferKind.Power)
                {
                    ShowPowerTooltip(offer.Power, offer.Price, world);
                }
                else
                {
                    ShowCardTooltip(offer.Card, world);
                }
                return;
            }
            if (session.Phase == GamePhase.Round
                && session.CurrentRound != null
                && session.CurrentRound.Status == RoundStatus.InProgress)
            {
                CardVisual hit = cardLayer.CardAt(world);
                BlockCard card = hit != null ? CardOfSlot(session.CurrentRound, hit.SlotIndex) : null;
                cardLayer.SetHoveredCard(card != null ? card.Id : -1);
                if (card != null) ShowCardTooltip(card, world); else HideTooltip();
                return;
            }
            cardLayer.SetHoveredCard(-1);
            HideTooltip();
        }

        private void ShowCardTooltip(BlockCard card, Vector2 nearWorld)
        {
            // Plain blocks carry no special info - no tooltip for them.
            if (card.Elements.Count == 0)
            {
                HideTooltip();
                return;
            }
            string title = "BLOCK - " + card.Shape.Size + (card.Shape.Size == 1 ? " cube" : " cubes")
                + "  (" + card.Shape.Width + "x" + card.Shape.Height + ")";
            var body = new StringBuilder();
            for (int i = 0; i < card.Elements.Count; i++)
            {
                if (i > 0) body.Append("\n\n");
                BlockElement element = card.Elements[i];
                body.Append(ViewUtil.ElementLabel(element)).Append('\n')
                    .Append(ViewUtil.WrapText(ViewUtil.ElementDescription(element), 34));
            }
            RenderTooltip("card:" + card.Id, title, body.ToString(), nearWorld);
        }

        private void ShowJokerTooltip(JokerDefinition joker, int price, Vector2 nearWorld)
        {
            string body = ViewUtil.WrapText(joker.Description, 34) + "\n\nCost " + price;
            RenderTooltip("joker:" + joker.DefId, joker.DisplayName, body, nearWorld);
        }

        private void ShowPowerTooltip(PowerDefinition power, int price, Vector2 nearWorld)
        {
            string body = ViewUtil.WrapText(power.Description, 34) + "\n\nCost " + price;
            RenderTooltip("power:" + power.DefId, power.DisplayName, body, nearWorld);
        }

        /// <summary>Rebuilds the tooltip panel only when the hovered target changes; always
        /// repositions it next to the cursor, clamped inside the camera view.</summary>
        private void RenderTooltip(string key, string title, string body, Vector2 nearWorld)
        {
            tooltipRoot.SetActive(true);
            if (key != tooltipKey)
            {
                tooltipKey = key;
                for (int i = tooltipRoot.transform.childCount - 1; i >= 0; i--)
                {
                    Destroy(tooltipRoot.transform.GetChild(i).gameObject);
                }
                int bodyLines = 1;
                for (int i = 0; i < body.Length; i++)
                {
                    if (body[i] == '\n') bodyLines++;
                }
                const float margin = 0.14f;
                const float titleHeight = 0.34f;
                const float lineHeight = 0.30f;
                tooltipWidth = 3.4f;
                tooltipHeight = margin * 2f + titleHeight + bodyLines * lineHeight;

                ViewUtil.MakeRect(tooltipRoot.transform, "TipBg",
                    new Vector2(tooltipWidth * 0.5f, -tooltipHeight * 0.5f),
                    new Vector2(tooltipWidth, tooltipHeight), TooltipBgColor, 50);
                // High fontSize + small characterSize keeps TextMesh crisp; the dark panel
                // gives contrast so no outline is needed here.
                ViewUtil.MakeText3D(tooltipRoot.transform, "TipTitle",
                    new Vector2(margin, -margin), title, 90, 0.017f, TooltipTitleColor, 51,
                    TextAnchor.UpperLeft);
                ViewUtil.MakeText3D(tooltipRoot.transform, "TipBody",
                    new Vector2(margin, -margin - titleHeight), body, 90, 0.014f, TooltipBodyColor,
                    51, TextAnchor.UpperLeft);
            }

            // Anchor the panel's top-left just up-right of the cursor, then clamp on screen.
            Vector3 topLeft = cam.ViewportToWorldPoint(new Vector3(0f, 1f, cam.nearClipPlane));
            Vector3 bottomRight = cam.ViewportToWorldPoint(new Vector3(1f, 0f, cam.nearClipPlane));
            float ax = Mathf.Clamp(nearWorld.x + 0.3f, topLeft.x + 0.1f, bottomRight.x - 0.1f - tooltipWidth);
            float ay = Mathf.Clamp(nearWorld.y + 0.4f, bottomRight.y + 0.1f + tooltipHeight, topLeft.y - 0.1f);
            tooltipRoot.transform.position = new Vector3(ax, ay, 0f);
        }

        private void HideTooltip()
        {
            if (tooltipRoot != null)
            {
                tooltipRoot.SetActive(false);
            }
            tooltipKey = null;
        }

        private void BuildViews()
        {
            var boardGo = new GameObject("BoardView");
            boardGo.transform.SetParent(transform, false);
            boardView = boardGo.AddComponent<BoardView>();

            var cardsGo = new GameObject("CardLayer");
            cardsGo.transform.SetParent(transform, false);
            cardLayer = cardsGo.AddComponent<CardLayerView>();

            var overlayGo = new GameObject("DeckOverlay");
            overlayGo.transform.SetParent(transform, false);
            deckOverlay = overlayGo.AddComponent<DeckOverlayView>();

            var deckSelectGo = new GameObject("DeckSelect");
            deckSelectGo.transform.SetParent(transform, false);
            deckSelect = deckSelectGo.AddComponent<DeckSelectView>();

            var marketGo = new GameObject("MarketView");
            marketGo.transform.SetParent(transform, false);
            marketView = marketGo.AddComponent<MarketView>();

            var sfxGo = new GameObject("SoundFx");
            sfxGo.transform.SetParent(transform, false);
            sfx = sfxGo.AddComponent<SoundFx>();

            var flamesGo = new GameObject("FlameStreak");
            flamesGo.transform.SetParent(transform, false);
            flameStreak = flamesGo.AddComponent<FlameStreakView>();

            var blastGo = new GameObject("BlastFx");
            blastGo.transform.SetParent(transform, false);
            blastFx = blastGo.AddComponent<BlastFxView>();

            var jokerGo = new GameObject("JokerBarView");
            jokerGo.transform.SetParent(transform, false);
            jokerBar = jokerGo.AddComponent<JokerBarView>();

            var powerGo = new GameObject("PowerBarView");
            powerGo.transform.SetParent(transform, false);
            powerBar = powerGo.AddComponent<PowerBarView>();

            tooltipRoot = new GameObject("Tooltip");
            tooltipRoot.transform.SetParent(transform, false);
            tooltipRoot.SetActive(false);

            var canvasGo = new GameObject("HudCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            infoText = MakeText(canvasGo.transform, "InfoText", new Vector2(0f, 1f),
                new Vector2(16f, -16f), TextAnchor.UpperLeft, 22, Color.white);
            messageText = MakeText(canvasGo.transform, "MessageText", new Vector2(0.5f, 1f),
                new Vector2(0f, -16f), TextAnchor.UpperCenter, 28, new Color(1f, 0.92f, 0.45f));

            jokerBar.Build(canvasGo.transform);
            powerBar.Build(canvasGo.transform);
        }

        private static Text MakeText(Transform parent, string name, Vector2 anchor,
            Vector2 offset, TextAnchor alignment, int fontSize, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(860f, 420f);
            return text;
        }
    }
}
