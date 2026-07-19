// PURPOSE: The single scene component (lives in Assets/Scenes/enes.unity). Creates the
// GameSession, builds the debug UI at runtime (no scene-authored visuals), and turns
// player input into calls on the core engine.
// CONTROLS: drag a card from the hand onto the board to place it,
//           A = advance / C = continue on an offer, N = leave market, R = new run,
//           D = deck select, J = pick any joker to grant, P = pick any power to grant,
//           K = sell the last joker, 1-9 = activate that joker (one that needs a target
//           then waits for a click, Esc cancels). Click a power in the left bar to use it
//           (same targeting flow); in the market, clicking a joker or power panel sells it.
// NOTE FOR AGENTS: this is placeholder presentation. Extend gameplay in
// ProjectBlock.Core; only wiring/visuals belong here. The J/P/K debug keys grant/sell
// outside the market's rules on purpose - they are testing tools, not gameplay.

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
        private GrantPickerView grantPicker;
        private ChoicePickerView choicePicker;
        private BlockDesignerView blockDesigner;
        private int designerPowerId;
        // Drag-paint stroke in the block designer: painting=true while the button is held after
        // a press on the grid; paintFill is the op decided by the first cell (empty->fill).
        private bool designerPainting;
        private bool designerPaintFill;
        private CrtOverlayView crt;

        private enum ChoiceKind { None, BatakBet, PowerbankTarget }
        private ChoiceKind pendingChoice;
        private int pendingChoiceJokerId;
        private readonly List<int> pendingChoiceValues = new List<int>();
        private DeckDefinition currentDeck = DeckLibrary.Classic;
        private SoundFx sfx;
        private FlameStreakView flameStreak;
        private BlastFxView blastFx;
        private int comboStreak;
        private readonly List<InfectedCell> infectionBuffer = new List<InfectedCell>();
        private Text infoText;
        private Text messageText;
        private Camera cam;
        private Vector3 camBasePosition;
        private Coroutine shakeRoutine;
        private CardVisual draggedCard;
        private int foxPickSlot = -1;
        private bool waterAnimating;
        private bool supurgeAnimating;
        private readonly List<GridPos> supurgeBuffer = new List<GridPos>();

        // "Hileli zar": market-phase pick of the next round's opening hand.
        private bool hileliPickMode;
        private readonly List<int> hileliSelection = new List<int>();
        private int hileliTarget;
        private int hileliPowerId;

        // "Parazit": market-phase attach flow (joker -> owned card -> cube).
        private enum ParazitStep { None, PickJoker, PickCard, PickCube }
        private ParazitStep parazitStep;
        private int parazitInstanceId;
        private int parazitTargetJoker;
        private int parazitCardId;
        private CubePickerView cubePicker;
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

        /// <summary>True while the pending power target is Olta's FREE mark pick (a setup
        /// action, not a use - see OltaPower.TryMark). Cleared with pendingTargetPowerId.</summary>
        private bool pendingOltaMark;

        private void Start()
        {
            // language before any text is built; persisted across sessions
            Loc.Language = PlayerPrefs.GetString("language", "en") == "tr"
                ? GameLanguage.Turkish
                : GameLanguage.English;
            cam = Camera.main;
            camBasePosition = cam.transform.position;
            BuildViews();
            NewGame();
        }

        /// <summary>Flips EN/TR, persists the choice, and re-texts every open view.</summary>
        private void ToggleLanguage()
        {
            Loc.Language = Loc.Language == GameLanguage.Turkish
                ? GameLanguage.English
                : GameLanguage.Turkish;
            PlayerPrefs.SetString("language", Loc.Language == GameLanguage.Turkish ? "tr" : "en");
            // modals cache their labels; closing them is simpler than re-texting them
            grantPicker.Hide();
            deckSelect.Hide();
            deckOverlay.Hide();
            choicePicker.Hide();
            blockDesigner.Hide();
            cubePicker.Hide();
            ClearChoice();
            parazitStep = ParazitStep.None;
            foxPickSlot = -1;
            sellCardsMode = false;
            hileliPickMode = false;
            HideTooltip();
            if (session != null && session.Phase == GamePhase.Market)
            {
                marketView.Show(session);
            }
            RefreshAll(null);
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
            hileliPickMode = false;
            hileliSelection.Clear();
            parazitStep = ParazitStep.None;
            cubePicker.Hide();
            waterAnimating = false;
            supurgeAnimating = false;
            pendingTargetJokerId = null;
            pendingTargetPowerId = null;
            grantPicker.Hide();
            choicePicker.Hide();
            blockDesigner.Hide();
            ClearChoice();
            marketView.Hide();
            HideTooltip();
            Debug.Log("[project_block] New run, seed " + lastSeedUsed);
            StartRoundPresentation();
        }

        /// <summary>Board + HUD refresh with the round-start shuffle-and-deal animation.</summary>
        private void StartRoundPresentation()
        {
            comboStreak = 0;
            // Keep the CRT in sync at every round start - crucially, a restart (R) or a deck
            // change builds a fresh session with retro OFF, so this turns the overlay back off.
            if (crt != null)
            {
                crt.SetVisible(session.Config.Rules.RetroMode);
            }
            if (sfx != null)
            {
                sfx.SetRetro(session.Config.Rules.RetroMode); // CRT hum + bit-crush follow retro
            }
            RoundEngine round = session.CurrentRound;
            if (boardView.Board != round.Board)
            {
                boardView.Rebuild(round.Board, maxBoardWorldSize, BoardCenter);
            }
            flameStreak.SetState(round.ContinueCount, boardView.WorldRect);
            boardView.Refresh();
            boardView.ClearPreview();
            RefreshInfections();
            sfx.Shuffle();
            cardLayer.AnimateRoundStart(round);
            UpdateHud();
            jokerBar.Refresh(session, pendingTargetJokerId);
            powerBar.Refresh(session, pendingTargetPowerId);
        }

        private void Update()
        {
            if (session == null || waterAnimating || supurgeAnimating)
            {
                return; // input is locked while a board animation plays
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
            if (kb != null && kb.lKey.wasPressedThisFrame)
            {
                ToggleLanguage();
                return;
            }
            // Parazit attach flow owns input while active (a multi-step market action).
            if (parazitStep != ParazitStep.None)
            {
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    CancelParazit();
                    return;
                }
                HandleParazitFlow(mouse);
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
                    hileliPickMode = false;
                    deckOverlay.Hide();
                    return;
                }
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && hileliPickMode)
                {
                    Vector2 pickWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    // The CONFIRM button (only live at exactly the target count) commits.
                    if (deckOverlay.PickerConfirmAt(pickWorld))
                    {
                        ConfirmHileliZar();
                        return;
                    }
                    // Otherwise a card click TOGGLES it: deselect if picked, else select while
                    // there is still room. The overlay is rebuilt so highlights + counter update.
                    BlockCard card = deckOverlay.CardAt(pickWorld);
                    if (card != null)
                    {
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
                                FloatingTextFx.Spawn(transform, sellWorld,
                                    Loc.Pick("worthless", "değersiz"),
                                    new Color(0.6f, 0.6f, 0.6f), 50, 0.045f);
                            }
                            deckOverlay.Show(session.OwnedCards,
                                c => session.Config.Market.SellValue(c) * session.Config.Scoring.ScoreScale);
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
            if (choicePicker.IsOpen)
            {
                // modal: click a row to choose, Esc cancels
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    choicePicker.Hide();
                    ClearChoice();
                    return;
                }
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    Vector2 pickWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    int idx = choicePicker.OptionAt(pickWorld);
                    choicePicker.Hide();
                    if (idx >= 0)
                    {
                        ResolveChoice(idx);
                    }
                    ClearChoice();
                }
                return;
            }
            if (blockDesigner.IsOpen)
            {
                // modal: drag to paint cells / pick an element / Confirm or Cancel; Esc cancels
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    designerPainting = false;
                    blockDesigner.Hide();
                    return;
                }
                if (mouse == null)
                {
                    return;
                }
                Vector2 dw = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    int btn = blockDesigner.ButtonAt(dw);
                    if (btn == 1)
                    {
                        ConfirmBlockDesigner();
                        return;
                    }
                    if (btn == 0)
                    {
                        blockDesigner.Hide();
                        return;
                    }
                    int el = blockDesigner.ElementAt(dw);
                    if (el >= 0)
                    {
                        blockDesigner.SelectElement(el);
                        return;
                    }
                    // Grid press: start a paint stroke. The first cell decides the op - press an
                    // empty cell to paint, a filled one to erase - and the drag applies it on.
                    int cell = blockDesigner.CellIndexAt(dw);
                    if (cell >= 0)
                    {
                        designerPaintFill = !blockDesigner.IsCellFilled(cell);
                        designerPainting = true;
                        blockDesigner.SetCell(cell, designerPaintFill);
                    }
                    return;
                }
                if (designerPainting)
                {
                    if (mouse.leftButton.isPressed)
                    {
                        int cell = blockDesigner.CellIndexAt(dw);
                        if (cell >= 0)
                        {
                            blockDesigner.SetCell(cell, designerPaintFill);
                        }
                    }
                    else
                    {
                        designerPainting = false;
                    }
                }
                return;
            }
            if (grantPicker.IsOpen)
            {
                // modal: click a tile to grant that joker/power, anything else closes
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    grantPicker.Hide();
                    return;
                }
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    Vector2 pickWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    int index = grantPicker.EntryAt(pickWorld);
                    bool powersMode = grantPicker.Mode == GrantPickerView.PickerMode.Powers;
                    grantPicker.Hide();
                    HideTooltip();
                    if (index >= 0)
                    {
                        if (powersMode)
                        {
                            PowerDefinition def = PowerRegistry.All[index];
                            if (session.CanAcquirePower(def))
                            {
                                Power granted = session.Powers.Add(def.Create());
                                Debug.Log("[project_block] Power granted: " + granted.DisplayName);
                            }
                            else
                            {
                                Debug.Log("[project_block] Cannot grant " + def.DisplayName
                                    + " (already owned or no slot).");
                            }
                        }
                        else
                        {
                            JokerDefinition def = JokerRegistry.All[index];
                            if (session.CanAcquireJoker(def))
                            {
                                Joker granted = session.Jokers.Add(def.Create());
                                Debug.Log("[project_block] Joker granted: " + granted.DisplayName);
                            }
                            else
                            {
                                Debug.Log("[project_block] Cannot grant " + def.DisplayName
                                    + " (duplicate, no slot, or a legendary is already held).");
                            }
                        }
                        RefreshAll(null);
                    }
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

        /// <summary>Runs a joker, or arms targeting mode if it must be pointed at something.</summary>
        private void BeginActivation(Joker joker)
        {
            if (!session.Jokers.CanActivate(joker.InstanceId))
            {
                Debug.Log("[project_block] " + joker.DisplayName + " cannot be used right now.");
                return;
            }
            // One joker asks the player for a value first, via a modal picker (Batak is now a
            // power - its picker is opened from BeginPowerActivation instead).
            var powerbank = joker as PowerbankJoker;
            if (powerbank != null)
            {
                OpenPowerbankPicker(powerbank);
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

        private void OpenBatakPicker(BatakPower batak)
        {
            pendingChoice = ChoiceKind.BatakBet;
            pendingChoiceJokerId = batak.InstanceId; // holds the POWER instance id for Batak bets
            pendingChoiceValues.Clear();
            var labels = new List<string>();
            for (int turns = 1; turns <= 8; turns++)
            {
                pendingChoiceValues.Add(turns);
                labels.Add(Loc.Pick(turns + (turns == 1 ? " turn" : " turns"), turns + " tur"));
            }
            choicePicker.Show(Loc.Pick("Batak: bet how many turns to sweep?",
                "Batak: kaç turda temizlersin?"), labels);
        }

        private void OpenPowerbankPicker(PowerbankJoker powerbank)
        {
            pendingChoice = ChoiceKind.PowerbankTarget;
            pendingChoiceJokerId = powerbank.InstanceId;
            pendingChoiceValues.Clear();
            var labels = new List<string>();
            IReadOnlyList<Power> powers = session.Powers.Powers;
            for (int i = 0; i < powers.Count; i++)
            {
                if (!powers[i].Charged)
                {
                    pendingChoiceValues.Add(powers[i].InstanceId);
                    labels.Add(powers[i].DisplayName);
                }
            }
            if (labels.Count == 0)
            {
                ClearChoice();
                return; // nothing spent to refill
            }
            choicePicker.Show(Loc.Pick("Powerbank: recharge which power?",
                "Powerbank: hangi gücü doldur?"), labels);
        }

        private void ResolveChoice(int index)
        {
            if (index < 0 || index >= pendingChoiceValues.Count)
            {
                return;
            }
            var ctx = new RoundContext(session, session.Rng, session.CurrentRound);
            if (pendingChoice == ChoiceKind.BatakBet)
            {
                if (session.PlaceBatakBet(pendingChoiceJokerId, pendingChoiceValues[index]))
                {
                    Debug.Log("[project_block] Batak bet " + pendingChoiceValues[index] + " turns.");
                    powerBar.PulsePower(pendingChoiceJokerId);
                }
            }
            else if (pendingChoice == ChoiceKind.PowerbankTarget)
            {
                var powerbank = session.Jokers.Find(pendingChoiceJokerId) as PowerbankJoker;
                if (powerbank != null && powerbank.RechargeChosen(ctx, pendingChoiceValues[index]))
                {
                    Debug.Log("[project_block] Powerbank recharged power #" + pendingChoiceValues[index]);
                    jokerBar.PulseJoker(pendingChoiceJokerId);
                }
            }
            RefreshAll(null);
        }

        private void ClearChoice()
        {
            pendingChoice = ChoiceKind.None;
            pendingChoiceJokerId = 0;
            pendingChoiceValues.Clear();
        }

        /// <summary>The block designer's Confirm: bake the drawn shape + element into the deck
        /// and spend the "Karakter oluşturma" charge (all rules in GameSession). An empty shape
        /// keeps the designer open.</summary>
        private void ConfirmBlockDesigner()
        {
            IReadOnlyList<GridPos> cells = blockDesigner.ShapeCells();
            if (cells.Count == 0)
            {
                return; // nothing drawn yet - leave the designer open
            }
            // A block must be one connected piece; reject scattered cells and keep the designer
            // open with a warning rather than baking a disjoint "block".
            if (!blockDesigner.IsSingleConnectedPiece())
            {
                blockDesigner.SetWarning(Loc.Pick(
                    "the shape must be one connected piece",
                    "şekil tek parça bağlı olmalı"));
                return;
            }
            BlockShape shape = BlockShape.FromCells(cells);
            BlockElement? element = blockDesigner.SelectedElement;
            IEnumerable<BlockElement> elements = element.HasValue
                ? new[] { element.Value }
                : null;
            bool made = session.CreateDesignedBlock(designerPowerId, shape, elements);
            blockDesigner.Hide();
            if (made)
            {
                powerBar.PulsePower(designerPowerId);
                Debug.Log("[project_block] Karakter oluşturma: baked a " + cells.Count
                    + "-cube block into the deck.");
            }
            powerBar.Refresh(session, null);
            RefreshAll(null);
        }

        private void CancelTargeting()
        {
            pendingTargetJokerId = null;
            pendingTargetPowerId = null;
            pendingOltaMark = false;
            boardView.ClearPreview();
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
            // Olta with no mark: clicking it starts the FREE mark pick (per-round setup,
            // not a use), so the rod is usable at all from the UI.
            var olta = power as OltaPower;
            if (olta != null && !olta.MarkedCardId.HasValue
                && session.CurrentRound != null
                && session.CurrentRound.Status == RoundStatus.InProgress)
            {
                pendingTargetPowerId = power.InstanceId;
                pendingOltaMark = true;
                UpdateHud();
                powerBar.Refresh(session, pendingTargetPowerId);
                return;
            }
            // "Karakter oluşturma" opens the block designer instead of running through TryUse;
            // the designer's Confirm bakes the block and spends the charge (CreateDesignedBlock).
            if (power is KarakterOlusturmaPower)
            {
                if (!session.Powers.CanBeginUse(power.InstanceId))
                {
                    Debug.Log("[project_block] " + power.DisplayName + " cannot be used right now.");
                    return;
                }
                designerPowerId = power.InstanceId;
                blockDesigner.Show();
                return;
            }
            // "Batak" opens the bet picker; GameSession.PlaceBatakBet places it + spends the charge.
            var batak = power as BatakPower;
            if (batak != null && !batak.HasActiveBet)
            {
                if (!session.Powers.CanBeginUse(power.InstanceId))
                {
                    Debug.Log("[project_block] " + power.DisplayName + " cannot be used right now.");
                    return;
                }
                OpenBatakPicker(batak);
                return;
            }
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
            pendingOltaMark = false;
            // A hand-targeted power may change how the card DISPLAYS (Cımbız rotates it),
            // and card visuals are cached by id - drop the visual so Sync rebuilds it.
            RoundEngine round = session.CurrentRound;
            int targetCardId = -1;
            if (target.HandIndex.HasValue && round != null
                && target.HandIndex.Value >= 0 && target.HandIndex.Value < round.Hand.Count)
            {
                targetCardId = round.Hand[target.HandIndex.Value].Id;
            }
            // Cells a board-targeting power will hit, captured BEFORE the destruction so the
            // blast can play on them afterwards.
            IReadOnlyList<GridPos> blastCells = power.PreviewCells(target);
            // Whole-board powers (Bardağın boş tarafı, Çerçeve...) destroy board-dependent cells
            // that PreviewCells cannot predict; capture what they actually destroy for the blast.
            if (round != null)
            {
                round.BeginExternalCapture();
            }
            if (!session.Powers.TryUse(power.InstanceId, target))
            {
                Debug.Log("[project_block] " + power.DisplayName + " could not be used.");
                boardView.ClearPreview();
                RefreshAll(null);
                return;
            }
            // Prefer the cells actually destroyed between turns (no board resize, so their coords
            // stay valid for the FX); targeted powers keep their predicted PreviewCells blast.
            if ((blastCells == null || blastCells.Count == 0) && round != null
                && round.ExternalDestructionLog.Count > 0)
            {
                blastCells = new List<GridPos>(round.ExternalDestructionLog);
            }
            Debug.Log("[project_block] Power used: " + power.DisplayName);
            powerBar.PulsePower(power.InstanceId);
            if (targetCardId >= 0)
            {
                cardLayer.ForgetCard(targetCardId);
            }
            boardView.ClearPreview();
            // Powers can rewrite the board (inflations replace it wholesale, Kum saati
            // rewinds it) and the piles - a full resync covers every one of them.
            RefreshAll(null);
            if (session.Phase == GamePhase.Market)
            {
                // "Totem" ends overtime and advances straight to the market mid-use; mirror the
                // normal advance flow (RefreshAll + Show) so the market actually appears.
                marketView.Show(session);
            }
            PlayPowerBlast(blastCells);
        }

        /// <summary>"Robot süpürge": a short beat after the player places a block, the sweeper
        /// eats a cube with a shake + blast. Input is locked until it goes off, so nothing can
        /// be placed until the sweep resolves.</summary>
        private void TriggerSupurgeBlast()
        {
            supurgeBuffer.Clear();
            IReadOnlyList<Joker> jokers = session.Jokers.Jokers;
            for (int i = 0; i < jokers.Count; i++)
            {
                var rs = jokers[i] as RobotSupurgeJoker;
                if (rs != null)
                {
                    supurgeBuffer.AddRange(rs.LastSweptCells);
                }
            }
            if (supurgeBuffer.Count == 0)
            {
                return;
            }
            StartCoroutine(SupurgeBlastRoutine(new List<GridPos>(supurgeBuffer)));
        }

        private IEnumerator SupurgeBlastRoutine(List<GridPos> cells)
        {
            supurgeAnimating = true;
            yield return new WaitForSeconds(0.28f);
            var color = new Color(0.7f, 0.85f, 1f);
            for (int i = 0; i < cells.Count; i++)
            {
                blastFx.EmitAt(boardView.CellToWorld(cells[i]), color, 6);
            }
            sfx.Explode();
            ShakeCamera(0.14f, 0.22f);
            supurgeAnimating = false;
        }

        /// <summary>Blast particles + shake + sound on the cells a board power just hit.</summary>
        private void PlayPowerBlast(IReadOnlyList<GridPos> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return;
            }
            var blastColor = new Color(1f, 0.72f, 0.35f);
            bool any = false;
            for (int i = 0; i < cells.Count; i++)
            {
                if (session.CurrentRound != null && session.CurrentRound.Board.IsInside(cells[i]))
                {
                    blastFx.EmitAt(boardView.CellToWorld(cells[i]), blastColor, 5);
                    any = true;
                }
            }
            if (any)
            {
                sfx.Explode();
                ShakeCamera(0.12f, 0.2f);
            }
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
                    if (report.PlayedCardExpired)
                    {
                        sfx.Vanish();
                    }
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
                    TriggerSupurgeBlast();
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
                    Loc.Pick("DYNAMITE!", "DİNAMİT!"), new Color(0.95f, 0.3f, 0.2f), 72, 0.09f);
            }
            // The popup shows the SCORING combo (consecutive line-clearing turns), which is
            // what actually pays out - not the destruction-only comboStreak that drives shake.
            if (report.ComboCount >= 2)
            {
                FloatingTextFx.Spawn(transform, new Vector2(0f, 2.6f),
                    Loc.Pick("COMBO x", "KOMBO x") + report.ComboCount + "!",
                    new Color(1f, 0.6f, 0.2f), 64, 0.08f);
            }
            if (report.CleanSweep)
            {
                FloatingTextFx.Spawn(transform, new Vector2(0f, 1.4f),
                    Loc.Pick("CLEAN SWEEP!", "TEMİZLİK!"), new Color(1f, 0.85f, 0.3f), 80, 0.1f);
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

        /// <summary>The top revealed cards of the discard pile ("Fraksiyon" inspect),
        /// newest first.</summary>
        private static List<BlockCard> RevealedDiscardCards(RoundEngine round)
        {
            var list = new List<BlockCard>();
            IReadOnlyList<BlockCard> pile = round.Deck.DiscardPile;
            int n = Mathf.Min(round.Rules.RevealedDiscardCount, pile.Count);
            for (int i = 0; i < n; i++)
            {
                list.Add(pile[pile.Count - 1 - i]);
            }
            return list;
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
            RefreshInfections();
            cardLayer.Sync(round, report);
            flameStreak.SetState(round.ContinueCount, boardView.WorldRect);
            UpdateHud();
            jokerBar.Refresh(session, pendingTargetJokerId);
            powerBar.Refresh(session, pendingTargetPowerId);
            if (crt != null)
            {
                crt.SetVisible(session.Config.Rules.RetroMode); // CRT follows retro mode
            }
            if (sfx != null)
            {
                sfx.SetRetro(session.Config.Rules.RetroMode); // hum + bit-crush follow retro
            }
        }

        /// <summary>Gathers "Enfeksiyon" infection markers from the inventory and hands them
        /// to the board view to draw (buildup pips + tint).</summary>
        private void RefreshInfections()
        {
            infectionBuffer.Clear();
            IReadOnlyList<Joker> jokers = session.Jokers.Jokers;
            for (int i = 0; i < jokers.Count; i++)
            {
                var enf = jokers[i] as EnfeksiyonJoker;
                if (enf != null)
                {
                    infectionBuffer.AddRange(enf.InfectedCells);
                }
            }
            boardView.ShowInfections(infectionBuffer);
        }

        private void UpdateHud()
        {
            RoundEngine round = session.CurrentRound;
            var sb = new StringBuilder();
            sb.Append("Seed ").Append(lastSeedUsed)
                .Append(Loc.Pick("   Deck: ", "   Deste: ")).Append(currentDeck.Name).Append('\n');
            sb.Append(Loc.Pick("Round ", "Raunt ")).Append(session.RoundNumber)
                .Append(Loc.Pick("   Turn ", "   Tur ")).Append(round.TurnNumber).Append('\n');
            // RoundScore lives in the scaled economy; lift the threshold to match for display.
            sb.Append(Loc.Pick("Score ", "Puan ")).Append(round.RoundScore)
                .Append(" / ").Append(round.Config.ScoreThreshold * session.Config.Scoring.ScoreScale);
            if (round.ThresholdPassed)
            {
                sb.Append(Loc.Pick("  [threshold passed]", "  [eşik geçildi]"));
            }
            sb.Append('\n');
            sb.Append(Loc.Pick("Total score ", "Toplam puan ")).Append(session.TotalScore).Append('\n');
            sb.Append(Loc.Pick("Draw ", "Çekme ")).Append(round.Deck.DrawCount)
                .Append(Loc.Pick("   Discard ", "   Iskarta ")).Append(round.Deck.DiscardCount)
                .Append(Loc.Pick("   Removed ", "   Çıkan ")).Append(round.Deck.RemovedCount).Append('\n');
            sb.Append(Loc.Pick("Jokers ", "Joker ")).Append(session.Jokers.Count);
            if (session.Jokers.Count > 0)
            {
                sb.Append(Loc.Pick("   (1-9 activate)", "   (1-9 kullan)"));
            }
            sb.Append(Loc.Pick("   Powers ", "   Güç ")).Append(session.Powers.Count);
            if (session.Powers.Count > 0)
            {
                sb.Append(Loc.Pick("   (click to use, one per turn)", "   (tıkla, tur başına bir)"));
            }
            sb.Append('\n');
            sb.Append(Loc.Pick(
                "Drag to place.  Click draw pile: deck.  Right-click: rotate GEARS / reshape FOX\n",
                "Sürükleyip yerleştir.  Çekme destesi: kartların.  Sağ tık: ÇARK döndür / TİLKİ şekillendir\n"));
            sb.Append(Loc.Pick(
                "Debug - S: redraw hand   B: bonus card   D: choose deck   R: new run   L: türkçe\n",
                "Debug - S: eli yenile   B: bonus kart   D: deste seç   R: yeni oyun   L: english\n"));
            sb.Append(Loc.Pick(
                "Debug - J: pick joker   P: pick power   K: sell last joker",
                "Debug - J: joker seç   P: güç seç   K: son jokeri sat"));
            infoText.text = sb.ToString();

            if (pendingTargetJokerId.HasValue)
            {
                Joker targeting = session.Jokers.Find(pendingTargetJokerId.Value);
                string what = targeting != null && targeting.Targeting == ActivationTargeting.BoardCell
                    ? Loc.Pick("pick a cube on the board", "oyun alanından bir küp seç")
                    : Loc.Pick("pick a block from your hand", "elinden bir blok seç");
                messageText.text = (targeting != null ? targeting.DisplayName : "Joker")
                    + ": " + what + Loc.Pick("\n[Esc] cancel", "\n[Esc] vazgeç");
                return;
            }
            if (pendingTargetPowerId.HasValue)
            {
                Power targeting = session.Powers.Find(pendingTargetPowerId.Value);
                string what = pendingOltaMark
                    ? Loc.Pick("pick the hand card to mark (free, once per round)",
                        "işaretlenecek kartı elinden seç (bedava, raunt başına bir)")
                    : targeting != null && targeting.Targeting == ActivationTargeting.BoardCell
                        ? Loc.Pick("pick a cell on the board", "oyun alanından bir hücre seç")
                        : Loc.Pick("pick a block from your hand", "elinden bir blok seç");
                messageText.text = (targeting != null ? targeting.DisplayName : Loc.Pick("Power", "Güç"))
                    + ": " + what + Loc.Pick("\n[Esc] cancel", "\n[Esc] vazgeç");
                return;
            }

            switch (session.Phase)
            {
                case GamePhase.GameOver:
                    messageText.text = Loc.Pick("GAME OVER - ", "OYUN BİTTİ - ") + DescribeLoss(round.Loss)
                        + Loc.Pick("\n[R] new run", "\n[R] yeni oyun");
                    break;
                case GamePhase.Market:
                    messageText.text = Loc.Pick(
                            "Click a card to add it to your deck (price below it)\n[N] start round ",
                            "Desteye katmak için karta tıkla (fiyatı altında)\n[N] raunt başlat: ")
                        + (session.RoundNumber + 1);
                    break;
                default:
                    if (round.Status == RoundStatus.AwaitingAdvanceDecision)
                    {
                        int continueCost = round.NextContinueCost;
                        int drawAfter = round.PredictDrawCountAfterContinue();
                        string warning = drawAfter < 0
                            ? Loc.Pick("  DECK OUT!", "  DESTE BİTER!")
                            : string.Empty;
                        messageText.text = Loc.Pick(
                                "Threshold reached!\n[A] advance to market    [C] continue: removes ",
                                "Eşik geçildi!\n[A] markete ilerle    [C] devam et: ")
                            + continueCost
                            + Loc.Pick(" cards, draw pile ", " kart gider, çekme destesi ")
                            + round.Deck.DrawCount
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
                case LossReason.NoPlayableMove:
                    return Loc.Pick("no held block fits the board",
                        "eldeki hiçbir blok alana sığmıyor");
                case LossReason.HandCannotBeRefilled:
                    return Loc.Pick("the deck ran out (hand could not be refilled)",
                        "deste tükendi (el doldurulamadı)");
                case LossReason.DrawPileEmptyAfterThreshold:
                    return Loc.Pick("draw pile emptied before a clean sweep",
                        "temizlik gelmeden çekme destesi bitti");
                case LossReason.BetFailed:
                    return Loc.Pick("the bet was lost (Batak)", "bahis tutmadı (Batak)");
                default:
                    return Loc.Pick("unknown", "bilinmiyor");
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

            if (grantPicker.IsOpen)
            {
                cardLayer.SetHoveredCard(-1);
                string defId, name, description;
                if (grantPicker.TryGetEntry(grantPicker.EntryAt(world),
                    out defId, out name, out description))
                {
                    RenderTooltip("pick:" + defId, name,
                        ViewUtil.WrapText(description, 34), world);
                }
                else
                {
                    HideTooltip();
                }
                return;
            }
            if (deckOverlay.IsOpen)
            {
                cardLayer.SetHoveredCard(-1);
                BlockCard card = deckOverlay.CardAt(world);
                if (card != null) ShowCardTooltip(card, world); else HideTooltip();
                return;
            }
            // Hovering a held joker/power panel shows its live details (name + description +
            // status). Checked before the market/round branches so it works in either phase the
            // bars are visible; Halüsinasyon's dynamic Description is surfaced automatically.
            if (session.Phase == GamePhase.Round || session.Phase == GamePhase.Market)
            {
                Vector2 barScreen = mouse.position.ReadValue();
                int ji = jokerBar.JokerIndexAt(barScreen);
                if (ji >= 0 && ji < session.Jokers.Count)
                {
                    cardLayer.SetHoveredCard(-1);
                    ShowHeldJokerTooltip(session.Jokers.Jokers[ji], world);
                    return;
                }
                int pi = powerBar.PowerIndexAt(barScreen);
                if (pi >= 0 && pi < session.Powers.Count)
                {
                    cardLayer.SetHoveredCard(-1);
                    ShowHeldPowerTooltip(session.Powers.Powers[pi], world);
                    return;
                }
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
            string title = Loc.Pick(
                "BLOCK - " + card.Shape.Size + (card.Shape.Size == 1 ? " cube" : " cubes"),
                "BLOK - " + card.Shape.Size + " küp")
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
            string body = ViewUtil.WrapText(joker.Description, 34)
                + Loc.Pick("\n\nCost ", "\n\nFiyat ") + price;
            RenderTooltip("joker:" + joker.DefId, joker.DisplayName, body, nearWorld);
        }

        private void ShowPowerTooltip(PowerDefinition power, int price, Vector2 nearWorld)
        {
            string body = ViewUtil.WrapText(power.Description, 34)
                + Loc.Pick("\n\nCost ", "\n\nFiyat ") + price;
            RenderTooltip("power:" + power.DefId, power.DisplayName, body, nearWorld);
        }

        /// <summary>Tooltip for a held joker in the bar: name, live description, and status.</summary>
        private void ShowHeldJokerTooltip(Joker joker, Vector2 nearWorld)
        {
            string title = joker.DisplayName;
            string body = ViewUtil.WrapText(joker.Description, 34);
            if (!string.IsNullOrEmpty(joker.StatusText))
            {
                body += "\n\n" + joker.StatusText;
            }
            // The key carries a text hash so a live-changing description/status (Halüsinasyon's
            // current form, a charge flipping) rebuilds the panel instead of showing stale text.
            RenderTooltip("heldjoker:" + joker.InstanceId + "#" + (title + body).GetHashCode(),
                title, body, nearWorld);
        }

        /// <summary>Tooltip for a held power in the bar: name, live description, and status.</summary>
        private void ShowHeldPowerTooltip(Power power, Vector2 nearWorld)
        {
            string title = power.DisplayName;
            string body = ViewUtil.WrapText(power.Description, 34);
            if (!string.IsNullOrEmpty(power.StatusText))
            {
                body += "\n\n" + power.StatusText;
            }
            RenderTooltip("heldpower:" + power.InstanceId + "#" + (title + body).GetHashCode(),
                title, body, nearWorld);
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

            var pickerGo = new GameObject("GrantPicker");
            pickerGo.transform.SetParent(transform, false);
            grantPicker = pickerGo.AddComponent<GrantPickerView>();

            var choiceGo = new GameObject("ChoicePicker");
            choiceGo.transform.SetParent(transform, false);
            choicePicker = choiceGo.AddComponent<ChoicePickerView>();

            var designerGo = new GameObject("BlockDesigner");
            designerGo.transform.SetParent(transform, false);
            blockDesigner = designerGo.AddComponent<BlockDesignerView>();

            var crtGo = new GameObject("CrtOverlay");
            crt = crtGo.AddComponent<CrtOverlayView>();
            crt.Build(cam); // parents its own overlay to the camera

            var cubeGo = new GameObject("CubePicker");
            cubeGo.transform.SetParent(transform, false);
            cubePicker = cubeGo.AddComponent<CubePickerView>();

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
