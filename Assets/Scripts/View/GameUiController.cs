// PURPOSE: The debug UI controller / main input loop (partial: fields, lifecycle, and
// the per-frame Update dispatch). The rest of the behaviour lives in partial files:
//   .Bars     - joker/power bar clicks, hileli-zar and Parazit flows, market clicks
//   .Activation - joker/power activation, pickers, block designer, power blasts
//   .Drag     - mouse drag placement and the retro falling-piece controller
//   .Feedback - placement/explosion feedback, camera shake, refresh, HUD, logging
//   .Tooltips - hover detection and the world-space tooltip panels
//   .Views    - runtime view/object construction
// Rules NEVER live here - this only reads Core and drives the debug views.

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
    public sealed partial class GameUiController : MonoBehaviour
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
        private BatakBetView batakBet;
        private int batakBetPowerId; // the BatakPower instance whose bet the locker is setting
        private BlockDesignerView blockDesigner;
        private int designerPowerId;
        // Drag-paint stroke in the block designer: painting=true while the button is held after
        // a press on the grid; paintFill is the op decided by the first cell (empty->fill).
        private bool designerPainting;
        private bool designerPaintFill;

        // Retro falling-piece controller (only while RetroMode): -1 = nothing falling. A chosen
        // hand card falls from the top; arrows steer, up/X rotate, down soft-drops, space hard-
        // drops. retroFallX/Y is the piece's origin in ABSOLUTE board coords.
        private int retroFallHand = -1;
        private int retroFallX;
        private int retroFallY;
        private float retroFallTimer;
        private const float RetroFallInterval = 0.55f;
        private const float RetroSoftDropInterval = 0.06f;
        private CrtOverlayView crt;
        private BitCrushFilter bitCrush;
        // Global the CrtEdgeBend fullscreen shader reads (0 = off, 1 = on). Driven by RetroMode;
        // harmless if the Full Screen Pass feature/material is not wired yet (see docs/crt-edge-bend.md).
        private static readonly int CrtBendId = Shader.PropertyToID("_CrtBend");

        private enum ChoiceKind { None, PowerbankTarget }
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
            // The bit-crush must sit on the AudioListener (the camera) to process the whole mix;
            // a filter on the SoundFx object's sources is not reliably called.
            bitCrush = cam.gameObject.AddComponent<BitCrushFilter>();
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
            batakBet.Hide();
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
            batakBet.Hide();
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
            retroFallHand = -1; // no piece is mid-fall across a round boundary
            // Keep the CRT in sync at every round start - crucially, a restart (R) or a deck
            // change builds a fresh session with retro OFF, so this turns the overlay back off.
            SyncRetroPresentation();
            RoundEngine round = session.CurrentRound;
            if (boardView.Board != round.Board)
            {
                boardView.Rebuild(round.Board, maxBoardWorldSize, BoardCenter);
            }
            flameStreak.SetState(round.ContinueCount, boardView.WorldRect);
            boardView.Refresh();
            boardView.SetDeadZone(session.Config.Rules.DeadZoneRows);
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
            if (batakBet.IsOpen)
            {
                // modal locker: +/- buttons or the wheel spin a dial, Bet confirms, Esc/Cancel closes
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    batakBet.Hide();
                    return;
                }
                Vector2 bw = mouse != null
                    ? (Vector2)cam.ScreenToWorldPoint(mouse.position.ReadValue()) : Vector2.zero;
                if (mouse != null)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                    {
                        int col = batakBet.DialColumnAt(bw);
                        if (col >= 0)
                        {
                            batakBet.Bump(col, scroll > 0f ? +1 : -1);
                            return;
                        }
                    }
                }
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    int plus = batakBet.PlusAt(bw);
                    if (plus >= 0) { batakBet.Bump(plus, +1); return; }
                    int minus = batakBet.MinusAt(bw);
                    if (minus >= 0) { batakBet.Bump(minus, -1); return; }
                    if (batakBet.CancelAt(bw)) { batakBet.Hide(); return; }
                    if (batakBet.ConfirmAt(bw))
                    {
                        int bet = batakBet.Value;
                        batakBet.Hide();
                        if (session.PlaceBatakBet(batakBetPowerId, bet))
                        {
                            Debug.Log("[project_block] Batak bet " + bet + " turns.");
                            powerBar.PulsePower(batakBetPowerId);
                            RefreshAll(null);
                        }
                    }
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
                        blockDesigner.SelectElement(el); // pick the active element brush
                        return;
                    }
                    // Left press on the grid: paint the active brush onto cells (fills empty cells
                    // and recolours filled ones); the drag keeps painting.
                    int cell = blockDesigner.CellIndexAt(dw);
                    if (cell >= 0)
                    {
                        designerPaintFill = true;
                        designerPainting = true;
                        blockDesigner.SetCell(cell, true);
                    }
                    return;
                }
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    // Right press/drag erases cells from the shape.
                    int cell = blockDesigner.CellIndexAt(dw);
                    if (cell >= 0)
                    {
                        designerPaintFill = false;
                        designerPainting = true;
                        blockDesigner.SetCell(cell, false);
                    }
                    return;
                }
                if (designerPainting)
                {
                    bool held = designerPaintFill
                        ? mouse.leftButton.isPressed
                        : mouse.rightButton.isPressed;
                    if (held)
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
                        else if (TrySkipHalusinasyonFromBar(mouse))
                        {
                            // right-clicking Halüsinasyon skips its roll (morph + spend, refills next round)
                        }
                        else if (session.Config.Rules.RetroMode)
                        {
                            HandleRetroFalling(round, kb, mouse);
                        }
                        else
                        {
                            HandleDrag(round, mouse);
                        }
                    }
                    break;
            }
        }
    }
}
