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
        [SerializeField] private bool verboseTurnLogs = true;

        /// <summary>The box the board is fitted into. It comes from the LAYOUT now rather than
        /// from a field on this component, because a phone held upright needs a different one and
        /// both have to be able to exist in the same build - see UiLayout.</summary>
        private static float MaxBoardWorldSize
        {
            get { return UiLayout.Active.BoardWorldSize; }
        }

        private static Vector2 BoardCenter
        {
            get { return UiLayout.Active.BoardCenter; }
        }

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
        private BackdropView backdrop;
        private BitCrushFilter bitCrush;
        // Global the CrtEdgeBend fullscreen shader reads (0 = off, 1 = on). Driven by RetroMode;
        // harmless if the Full Screen Pass feature/material is not wired yet (see docs/crt-edge-bend.md).
        private static readonly int CrtBendId = Shader.PropertyToID("_CrtBend");

        private enum ChoiceKind { None, PowerbankTarget, GravityDirection, BossStage }

        /// <summary>Which page of the DEBUG boss picker is showing. There are far more bosses
        /// than fit one modal, so the list pages and wraps.</summary>
        private int bossPickerPage;
        private ChoiceKind pendingChoice;
        private int pendingChoiceJokerId;
        private readonly List<int> pendingChoiceValues = new List<int>();
        private DeckDefinition currentDeck = DeckLibrary.Classic;
        private SoundFx sfx;
        private FlameStreakView flameStreak;
        private LineBurstView lineBurst;

        private LineSweepView lineSweep;

        private SweepSparkView sweepSparks;

        private BoardCleanseView boardCleanse;

        private DynamiteBlastView dynamiteBlast;

        /// <summary>The overtime pressure wave and the screen closing in around it. The fire
        /// they replaced is still in the project, switched off - see the note in RefreshFlames.
        /// The pressure system drives the vignette AND the board's own squeeze itself, so all
        /// three land on one beat.</summary>
        private OvertimePressureView overtimePressure;

        private OvertimeVignetteView overtimeVignette;

        /// <summary>The turn overtime began on, and how many have been played since. The pulse
        /// speeds up on TURNS rather than on continues, so it has to be counted here - Core has
        /// no reason to know how many turns a round has spent past its bar.</summary>
        private int overtimeStartTurn = -1;

        private int overtimeTurns;

        private BlastFxView blastFx;
        /// <summary>"Patlama: N hücre" - every explosion that is a loose group, not a line.</summary>
        private ClusterBurstView clusterBurst;
        /// <summary>Removal variant 1 - a removed cube falls into its cell.</summary>
        private ColdSinkView coldSink;
        /// <summary>Removal variant 2 - a removed cube's volume is folded into a seam.</summary>
        private PhaseFoldView phaseFold;
        /// <summary>Removal variant 3 - a removed cube's heat is drawn out and it sublimates.</summary>
        private CryoSublimationView cryoSublimation;
        /// <summary>"Soğuk sökülme" - a cube a moving board carried off, torn away along its step.</summary>
        private MomentumPeelView momentumPeel;
        /// <summary>A moving board's surviving cubes riding to their new cells.</summary>
        private BossMoveView bossMove;
        /// <summary>"Hidrolik pres" - the squeeze, and the release with its pushes.</summary>
        private HydraulicPressView hydraulicPress;
        /// <summary>"Hidrolik pres" failing - a pressure vessel that could not vent.</summary>
        private PressureVesselView pressureVessel;
        private LineSwapPickerView lineSwapPicker;

        private int comboStreak;
        private readonly List<InfectedCell> infectionBuffer = new List<InfectedCell>();
        private Text infoText;
        private Text messageText;

        /// <summary>The run's banked score - which is also the money the market spends. It gets
        /// its own prominent line because it was previously only findable halfway down the
        /// debug dump, and the market never showed it at all.</summary>
        private Text totalText;
        private Camera cam;

        /// <summary>Where a market drag started, and how far it has gone - see the market frame.</summary>
        private Vector2 marketDragFrom;

        private float marketDragged;
        private Vector3 camBasePosition;

        /// <summary>The HUD's own root, offset alongside the camera so a shake moves the whole
        /// SCREEN rather than only the world under a stationary interface.</summary>
        private RectTransform hudShake;

        private Canvas hudCanvas;
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
        private int hileliJokerId;

        // "Parazit": market-phase attach flow (joker -> owned card -> cube).
        private enum ParazitStep { None, PickJoker, PickCard, PickCube }
        private ParazitStep parazitStep;
        private int parazitInstanceId;
        private int parazitTargetJoker;
        private int parazitCardId;
        private CubePickerView cubePicker;
        private WeldPickerView weldPicker;
        private MineShuffleView mineShuffle;

        /// <summary>The last shuffle "Mayın eşeği" announced. Watched so the dance runs once per
        /// shuffle - never twice, never missed.</summary>
        private int lastMineShuffle;

        /// <summary>The detonation count the View has already drawn, and whether one is waiting
        /// to be drawn this turn. A detonation appears in no TurnReport - it is the boss's own
        /// bookkeeping - so it is watched exactly the way the shuffle above is.</summary>
        private int lastMineDetonations;
        private bool mineDetonationPending;

        // ---- the workshop powers' multi-step targeting ("Neşter", "Lehimleme", "Gen nakli") ----

        /// <summary>Which power is mid-way through picking, and what it has collected so far.
        /// All three are cleared by CancelTargeting, so Escape always gets the player out.</summary>
        private int? workshopPowerId;

        /// <summary>"Neşter"/"Lehimleme": the hand slot picked first.</summary>
        private int workshopFirstCard = -1;

        /// <summary>"Gen nakli": the board cube whose element is being moved.</summary>
        private GridPos? workshopDonorCell;

        /// <summary>"Hidrolik pres": the 2x2 patch picked by the first click. While it is set the
        /// power is waiting for the SECOND click - which of the four cells keeps the cube.</summary>
        private GridPos? workshopPressAnchor;
        private bool sellCardsMode;

        /// <summary>True while the deck overlay's scrollbar is being dragged. Held across frames
        /// because every move rebuilds the overlay under the cursor.</summary>
        private bool deckScrollDragging;
        private int lastSeedUsed;

        // Hover tooltip: a small rounded panel rebuilt only when the target changes. It lives on
        // its OWN overlay canvas, above the HUD's - see BuildTooltipCanvas for why it cannot be
        // a world-space object and be on top at the same time.
        private Canvas tooltipCanvas;
        private RectTransform tooltipRoot;
        private Text tooltipTitle;
        private Text tooltipBody;
        private string tooltipKey;
        private float tooltipWidth;
        private float tooltipHeight;

        /// <summary>OPAQUE, deliberately. A tooltip is a thing to read, and the board it covers
        /// is the busiest surface in the game - blocks, flashes, a pulsing preview. At 0.95 the
        /// board showed through the text just enough to make a long description tiring, which is
        /// the one thing a description may not be. It is a panel now, not a tint.</summary>
        private static readonly Color TooltipBgColor = new Color(0.05f, 0.06f, 0.09f, 1f);

        /// <summary>The hairline the rounded panel is cut out of - one shade up from the fill,
        /// drawn a couple of pixels bigger behind it. Without it an opaque near-black panel over
        /// the dark board has no edge at all and reads as a hole rather than as a card.</summary>
        private static readonly Color TooltipEdgeColor = new Color(0.26f, 0.30f, 0.38f, 1f);

        private static readonly Color TooltipTitleColor = new Color(1f, 0.93f, 0.72f);
        private static readonly Color TooltipBodyColor = new Color(0.82f, 0.86f, 0.92f);

        /// <summary>Gamepad support. It does NOT have its own input path: it writes the pad
        /// into a virtual mouse and keyboard, so every handler below goes on reading
        /// Mouse.current / Keyboard.current exactly as it did (see GamepadBridge).</summary>
        private GamepadBridge gamepad;

        /// <summary>Finger input, written into a virtual mouse. See TouchBridge.</summary>
        private readonly TouchBridge touch = new TouchBridge();

        /// <summary>Set while an activated joker waits for the player to pick a target.</summary>
        private int? pendingTargetJokerId;

        /// <summary>Set while a used power waits for the player to pick a target.</summary>
        private int? pendingTargetPowerId;

        /// <summary>True while the pending power target is Olta's FREE mark pick (a setup
        /// action, not a use - see OltaPower.TryMark). Cleared with pendingTargetPowerId.</summary>
        private bool pendingOltaMark;

        // ==================================================== THE SCREEN'S SHAPE
        //
        // The layout is not chosen once at boot: a window can be resized, a device can be handed
        // over, and the F6 override exists precisely so both shapes can be looked at without
        // leaving the editor. So the shape is watched every frame - it is two integer reads - and
        // anything that was POSITIONED for the old one is rebuilt when it changes.

        /// <summary>Pushes the active profile's orthographic size onto the camera. Everything else
        /// that cares about the visible area (the backdrop, the CRT, the deck overlay, the market)
        /// reads cam.orthographicSize live, so this one line moves all of them.</summary>
        private void ApplyLayoutToCamera()
        {
            if (cam == null)
            {
                return;
            }
            cam.orthographicSize = UiLayout.Active.OrthoSize;
            // Setting the rect also changes cam.aspect, which every world-space view already
            // reads - so the backdrop, the CRT overlay and the market follow the letterbox for
            // free rather than each needing to be told about it.
            cam.rect = UiLayout.Viewport;
        }

        /// <summary>
        /// Hides the two piles while a STACKED market is open. That layout covers the whole
        /// screen and the piles' labels sort above its panel, so left alone they print straight
        /// through it; the market's own DECK button is the way in while it is up. The desktop
        /// panel stops short of them, so there the draw pile stays up and clickable.
        /// </summary>
        private void SyncPilesForMarket()
        {
            if (cardLayer == null)
            {
                return;
            }
            bool hide = session != null
                && session.Phase == GamePhase.Market
                && UiLayout.Active.MarketStacked;
            // Asked every frame rather than cached against a remembered answer: the piles are
            // REBUILT whenever the layout changes, and a rebuilt pile comes back visible - so a
            // cache would go on believing it had already hidden them.
            cardLayer.SetPilesVisible(!hide);
        }

        /// <summary>Re-fits the interface if the screen's shape changed. Called every frame.</summary>
        private void WatchScreenShape()
        {
            if (!UiLayout.Refresh())
            {
                return;
            }
            ApplyLayoutToCamera();
            ApplyLayoutToHud();
            if (cardLayer != null)
            {
                cardLayer.RelayoutForScreen();
            }
            RelayoutBoards();
            // The menu shell computes its rows from MenuSkin every time it is shown, so showing
            // it again IS the relayout - and it has to happen, because the button metrics it
            // reads have just changed underneath it.
            if (screen != AppScreen.Playing)
            {
                ShowCurrentMenu();
            }
            // The market is rebuilt from scratch every time it is shown, so re-showing it IS the
            // relayout - and it has to happen here because its shelf goes from two columns to one
            // stack between the profiles (see UiLayout.MarketStacked).
            if (session != null && session.Phase == GamePhase.Market)
            {
                marketView.Show(session);
            }
        }

        /// <summary>Re-fits the board to the layout. It is REBUILT rather than moved because the
        /// cell size is derived from the box it is fitted into, and every view that hangs off the
        /// board (the surface, the glow, the boss fields) is built from that cell size.</summary>
        private void RelayoutBoards()
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || boardView == null)
            {
                return;
            }
            boardView.Rebuild(round.Board, MainBoardWorldSize, MainBoardCenter);
            lastMainBoardSize = MainBoardWorldSize;
            lastMainBoardCenter = MainBoardCenter;
            boardView.SetDarkness(round.BoardIsDark);
            boardView.Refresh();
            boardView.SetDeadZone(session.Config.Rules.DeadZoneRows);
            RefreshMirrorWorld();
        }

        /// <summary>Flips between the desktop and portrait layouts by hand (F6), so the phone
        /// layout can be worked on in a landscape editor and, more importantly, so a change to
        /// either one is checked against the other in the same run.</summary>
        private void ToggleLayoutShape()
        {
            UiShape next = UiLayout.Active.IsPortrait ? UiShape.Desktop : UiShape.Portrait;
            UiLayout.Override = next;
            WatchScreenShape();
            if (messageText != null)
            {
                messageText.text = next == UiShape.Portrait
                    ? Loc.Pick("portrait layout", "dikey yerleşim")
                    : Loc.Pick("desktop layout", "masaüstü yerleşim");
            }
        }

        private void Start()
        {
            // Preferences before any text is built, so the first labels come out in the right
            // language. The volume needs SoundFx, so it is pushed in after BuildViews.
            LoadSettings();
            cam = Camera.main;
            // BEFORE anything is built: every view asks the layout where it goes, so the profile
            // has to be settled first or the first frame is built for the wrong screen.
            UiLayout.Refresh();
            ApplyLayoutToCamera();
            camBasePosition = cam.transform.position;
            // The bit-crush must sit on the AudioListener (the camera) to process the whole mix;
            // a filter on the SoundFx object's sources is not reliably called.
            bitCrush = cam.gameObject.AddComponent<BitCrushFilter>();
            BuildViews();
            sfx.MasterVolume = masterVolume;
            // The game now boots to the title menu; a run starts from there (see .Menus).
            GoToTitle();
        }

        /// <summary>Pushes the fire's state, letting the debug override win when one is set.
        /// Every in-game caller goes through here; the ANIMATION LAB deliberately does not, since
        /// driving the view directly is its whole job.</summary>
        /// <summary>Rows the deck overlay travels per wheel notch. Two of its three visible rows
        /// - a big step without losing your place, and the one number to turn if it wants to be
        /// faster still.</summary>
        private const float DeckScrollRowsPerNotch = 2f;

        private void RefreshFlames(int overtimeLevel)
        {
            // THE FIRE IS OFF and the HEARTBEAT is on. The fire was not deleted - every part of
            // it is still here and still fed the level below, so going back to it is four bools:
            //     FlameStreakView.Style.DrawFlames    - the painted corner fires
            //     FlameStreakView.Style.DrawEmbers    - the sparks rising off them
            //     FlameStreakView.Style.DrawDrift     - the embers drawn in from the top corners
            //     BoardLineGlowView.Style.Enabled     - the glow on the board's grid
            // Turning those on and OvertimePulseView/OvertimeVignetteView off swaps the looks.
            // OVERTIME BELONGS TO A ROUND THAT IS STILL BEING PLAYED. A finished round keeps its
            // ContinueCount and the market opens over the same board, so RefreshAll goes on
            // feeding this the level of a round nobody is playing any more - and the pulse and
            // the closing-in vignette followed the player into the shop.
            //
            // Gated HERE, in the one place all of it is driven from, rather than at the four call
            // sites: the same trap is waiting for every future caller, and the market is only one
            // of the phases a round can end into (RunWon and GameOver are the others).
            int level = session != null && session.Phase == GamePhase.Round ? overtimeLevel : 0;
            flameStreak.SetState(level, boardView.WorldRect);
            boardView.SetOvertimeGlow(level);

            // The look that is actually ON: pressure pushing in from the arena's edge, the board
            // answering it, and the screen closing in a step per continue. One call - the
            // pressure system owns the clock and drives the other two off it.
            RoundEngine round = session != null ? session.CurrentRound : null;
            overtimePressure.SetState(level > 0, level, overtimeTurns, boardView.WorldRect,
                boardView.CellWorldSize,
                round != null ? round.Board.Width : 0,
                round != null ? round.Board.Height : 0);
        }

        /// <summary>Debug: light the fire at full heat, or hand it back to the round.</summary>
        /// <summary>Debug: takes the round into overtime FOR REAL, and one step deeper on every
        /// press. It is not a preview - the hand is reshuffled, cards leave at the escalating
        /// price and the round is genuinely past its bar, because a look that is only ever seen
        /// over a board in a state it could not actually be in is not being tested at all.
        ///
        /// Everything after the call is what the C key already does when a player continues, so
        /// the two paths cannot present the same event differently.</summary>
        private void DebugEnterOvertime()
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || !round.DebugEnterOvertime())
            {
                Debug.Log("[block_bonk] Debug overtime: the round cannot be pushed from here.");
                return;
            }
            RefreshFlames(round.ContinueCount);
            if (round.Status == RoundStatus.InProgress)
            {
                sfx.Shuffle();
                cardLayer.AnimateRedraw(round);
            }
            RefreshAll(null);
            Debug.Log("[block_bonk] Debug overtime: continue #" + round.ContinueCount
                + ", " + round.Hand.Count + " in hand.");
        }

        /// <summary>Flips EN/TR, persists the choice, and re-texts every open view.</summary>
        private void ToggleLanguage()
        {
            Loc.Language = Loc.Language == GameLanguage.Turkish
                ? GameLanguage.English
                : GameLanguage.Turkish;
            SaveSettings(); // language lives with the rest of the preferences
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
            if (screen != AppScreen.Playing)
            {
                // No run to refresh - just re-text the menu. deckSelect was closed above, so a
                // switch made during the deck pick lands back on the title; a PAUSED run must
                // stay paused, or changing language would silently resume it.
                if (screen == AppScreen.DeckSelect)
                {
                    screen = AppScreen.Title;
                }
                ShowCurrentMenu();
                return;
            }
            if (session != null && session.Phase == GamePhase.Market)
            {
                // A fresh visit starts at the top of the shelf, the same way the deck screen
                // does - carrying the last visit's scroll over would open the market halfway
                // down for no reason the player can see.
                marketView.ResetScroll();
                marketView.Show(session);
            }
            RefreshAll(null);
        }

        public void NewGame()
        {
            // A new session starts its card ids over from the beginning, so last run's faces
            // would answer for this run's cubes if they were left in the cache.
            cardFaces.Clear();
            lastSeedUsed = seed != 0 ? seed : System.Environment.TickCount;
            var config = new GameConfig();
            config.RngSeed = lastSeedUsed;
            config.Deck = currentDeck;
            session = new GameSession(config);
            // Both terminal phases raise the run-summary flag; Update opens the screen once
            // nothing is animating (see .RunSummary).
            runOverPending = false;
            session.PhaseChanged += OnSessionPhaseChanged;
            // A run can be over before the subscription exists: GameSession's constructor loses
            // the round outright if the deck is smaller than the hand. Catch that case here.
            if (session.Phase == GamePhase.GameOver || session.Phase == GamePhase.RunWon)
            {
                runOverPending = true;
            }
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
            Debug.Log("[block_bonk] New run, seed " + lastSeedUsed);
            StartRoundPresentation();
            // Overwrite any older save immediately: a new run supersedes it the moment it
            // starts, even if the player quits before finishing a single turn.
            AutoSave();
        }

        /// <summary>[F5]: skip the stage in progress and open the next market. The session does
        /// the skipping; this puts the screen back together afterwards, and has to cope with
        /// every phase the skip can land in - a boss stage with an unpaid debt ends the run, and
        /// skipping the LAST stage wins it, so neither is a market.</summary>
        private void DebugSkipToMarket()
        {
            CancelDrag();          // a card under the cursor does not survive the stage
            marketView.Hide();     // rebuilt below if we land back in a market
            session.DebugSkipToMarket();
            RefreshAll(null);
            if (session.Phase == GamePhase.Market)
            {
                marketView.ResetScroll();
                marketView.Show(session);
                Debug.Log("[block_bonk] DEBUG skipped to the market after round "
                    + session.RoundNumber + ".");
            }
            else if (session.Phase == GamePhase.Round)
            {
                // The skip was refused - the round was already lost, which is the one state
                // DeclareRoundWon will not overrule. Put the round back on screen unchanged.
                StartRoundPresentation();
            }
            // RunWon / GameOver need nothing here: OnSessionPhaseChanged is subscribed for the
            // life of the session and has already raised runOverPending, and Update opens the
            // summary from it once nothing is animating.
        }

        /// <summary>Board + HUD refresh with the round-start shuffle-and-deal animation.</summary>
        private void StartRoundPresentation()
        {
            comboStreak = 0;
            lineBurstTier = 1;
            overtimeStartTurn = -1;
            overtimeTurns = 0;
            retroFallHand = -1; // no piece is mid-fall across a round boundary

            // The whole screen, through the SAME method every other repaint goes through.
            //
            // This used to be a hand-written copy of RefreshAll, and it had drifted: it never
            // called SetDarkness, so an "Alacakaranlık" round opened with the lights ON and only
            // went dark on the first placement - the boss appeared to start a turn late. It also
            // missed the mirror world, the main board's mirror-shrunk size and centre, and
            // "Tamagotchi"'s demands, all for the same reason. A step listed by hand is a step
            // that can be forgotten, so the list is not written twice any more: a round begins by
            // painting everything, exactly as the end of a turn does.
            //
            // A BOSS IS ALREADY ATTACHED HERE. GameSession draws it and calls SetBoss while
            // building the round (it may reshape the board before the engine exists), so
            // round.BoardIsDark and every other boss query answer correctly on this first paint -
            // there is nothing to wait for.
            RefreshAll(null);

            // Then the part that belongs to a round START and nowhere else: the shuffle and the
            // deal. AnimateRoundStart clears the layer first, so it takes the hand over from the
            // static sync RefreshAll just did rather than fighting it.
            sfx.Shuffle();
            cardLayer.AnimateRoundStart(session.CurrentRound);
        }

        /// <summary>What the pad's left stick is this frame. Every screen that is a LIST of
        /// entries spends it on the selection; a round being played under the DIRECT scheme
        /// steers the pointer itself (see .PadPlay); everything else - the market, the modals,
        /// the deck pick - is aimed at with a cursor.</summary>
        private GamepadBridge.PointerMode PadPointerMode()
        {
            if (screen != AppScreen.Playing && screen != AppScreen.DeckSelect)
            {
                return GamepadBridge.PointerMode.MenuList;
            }
            // A DRIVEN PANEL counts as snapped as well: direct mode does not put an arrow back
            // on the screen for the collection overlay or the deck pick (see .PadPanels).
            return PadPanelDriven()
                    || (screen == AppScreen.Playing && PadDirectPlayable())
                ? GamepadBridge.PointerMode.Snapped
                : GamepadBridge.PointerMode.Pointer;
        }

        /// <summary>The one verb the pad's north button stands for right now. Read straight
        /// off the phase, so the bridge itself never has to know what a round is.</summary>
        private GamepadBridge.ActionContext PadActionContext()
        {
            if (screen != AppScreen.Playing || session == null)
            {
                return GamepadBridge.ActionContext.None;
            }
            if (session.Phase == GamePhase.Market)
            {
                return GamepadBridge.ActionContext.Market;
            }
            if (session.Phase != GamePhase.Round)
            {
                return GamepadBridge.ActionContext.None;
            }
            RoundEngine round = session.CurrentRound;
            return round != null && round.Status == RoundStatus.AwaitingAdvanceDecision
                ? GamepadBridge.ActionContext.RoundDecision
                : GamepadBridge.ActionContext.Round;
        }

        /// <summary>Gives the OS cursor back and takes the virtual devices out of the input
        /// system - without this an editor play/stop cycle leaves a pair behind each time.</summary>
        private void OnDestroy()
        {
            if (gamepad != null)
            {
                gamepad.Dispose();
                gamepad = null;
            }
        }

        private void Update()
        {
            // Before anything is drawn or read: the window may have been resized, the device may
            // have been turned. Two integer reads on a normal frame (see UiLayout.Refresh).
            WatchScreenShape();
            // "Midas" warms the score line while its gold is landing on it (see .Midas): one
            // owner for that transform, and it is this one.
            TickScoreResponse();
            // A finger, if there is one, writes itself into a virtual mouse BEFORE any handler
            // reads Mouse.current - the same contract the pad has (see TouchBridge).
            if (touch != null)
            {
                touch.Tick();
            }
            // Then, before anything reads a device: the pad writes this frame's stick and
            // buttons into its virtual mouse/keyboard, and the two reads below then pick them
            // up like any other hardware. Ticked from here rather than left to script
            // execution order, because "before Mouse.current is read" is the whole contract.
            if (gamepad != null)
            {
                GamepadBridge.PointerMode padMode = PadPointerMode();
                if (padMode == GamepadBridge.PointerMode.Snapped)
                {
                    // DIRECT play steers the pointer itself: it goes wherever the pad's
                    // selection is, so the hover visuals follow it (see .PadPlay).
                    gamepad.SnapPosition = PadSnapScreen();
                }
                gamepad.Tick(padMode, PadActionContext());
                // Straight after the tick, so both describe the state the pad is in NOW.
                UpdatePadPrompts();
                if (padDebugText != null)
                {
                    padDebugText.text = gamepad.Active ? gamepad.DebugSummary : string.Empty;
                }
            }
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            // Boss identity prototypes (see .BossIdentity): the look follows the stage on its own,
            // and a real boss stage's intro holds the frame until it ends or a press skips it.
            if (TickBossIdentity(kb, mouse))
            {
                return;
            }
            // The top-left debug readout's hide button (and F8) - see .DebugToggle.
            if (TickDebugToggle(kb, mouse))
            {
                return;
            }
            // F6 flips between the desktop and phone layouts. Handled ahead of every screen's own
            // input because it is not about what is on screen - it is about the screen itself,
            // and it has to work from the menus and the market too.
            if (kb != null && kb.f6Key.wasPressedThisFrame)
            {
                ToggleLayoutShape();
            }
            // The menu layer owns the whole frame whenever a run is not on screen, so no menu
            // has to know about drags, targeting or the in-game modals (see .Menus).
            if (screen != AppScreen.Playing)
            {
                HandleMenuInput(kb, mouse);
                return;
            }
            // The ANIMATION LAB owns the whole frame while it is open, and it is handled BEFORE
            // the board-animation lock below on purpose: the lab exists to fire exactly those
            // animations, so a panel that stopped taking clicks while one played would be
            // unusable (see .AnimationLab).
            if (AnimLabOpen)
            {
                HandleAnimationLabInput(kb, mouse);
                return;
            }
            if (kb != null && kb.f3Key.wasPressedThisFrame && session != null)
            {
                OpenAnimationLab();
                return;
            }
            // The BOSS LOOK LAB (F7) - boss intro / ambience prototypes, previewed over the board.
            if (BossLookOpen)
            {
                HandleBossLookInput(kb, mouse);
                return;
            }
            if (kb != null && kb.f7Key.wasPressedThisFrame && session != null)
            {
                OpenBossLook();
                return;
            }
            // The BLOCK GALLERY (F4) covers the screen, so like the lab it owns the frame while
            // it is open - a click falling through to the board would place a hidden block.
            if (GalleryOpen)
            {
                HandleBlockGalleryInput(kb, mouse);
                return;
            }
            if (kb != null && kb.f4Key.wasPressedThisFrame)
            {
                OpenBlockGallery();
                return;
            }
            if (session == null || waterAnimating || supurgeAnimating)
            {
                return; // input is locked while a board animation plays
            }
            // debug: [F5] wins the stage in progress and drops you in its market. Pressed IN a
            // market it steps a whole stage, so holding it walks the run market by market -
            // which is the point, the market is what wants testing over and over.
            if (kb != null && kb.f5Key.wasPressedThisFrame
                && (session.Phase == GamePhase.Round || session.Phase == GamePhase.Market))
            {
                DebugSkipToMarket();
                return;
            }
            // The run ended: this waits for the guard above to stop firing, so the last
            // placement's blast finishes playing before the summary covers the board.
            if (runOverPending)
            {
                runOverPending = false;
                OpenRunSummary();
                return;
            }
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
            // debug: go into overtime for real, without having to reach the bar. Only in a
            // ROUND now - it takes an actual continue, which the market has nothing to do with.
            if (kb != null && kb.yKey.wasPressedThisFrame && session != null
                && session.Phase == GamePhase.Round)
            {
                DebugEnterOvertime();
                return;
            }
            // debug: jump straight to a boss stage. Here rather than with the in-round debug keys
            // because the market is the natural place to reach for it, and it works from both.
            if (kb != null && kb.gKey.wasPressedThisFrame && session != null
                && (session.Phase == GamePhase.Round || session.Phase == GamePhase.Market))
            {
                bossPickerPage = 0;
                OpenBossPicker();
                return;
            }
            // The dead-end rescue owns input while the round is paused on it.
            if (lineSwapPicker.IsOpen)
            {
                if (!HandlePadRescue())
                {
                    HandleRescuePick(mouse, kb);
                }
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
                        Debug.Log("[block_bonk] Deck selected: " + currentDeck.Name);
                        NewGame();
                    }
                }
                return;
            }
            if (deckOverlay.IsOpen)
            {
                // DIRECT mode steps the cards instead of pointing at them (see .PadPanels).
                if (HandlePadDeckOverlay())
                {
                    return;
                }
                // modal: click picks (fox mode), sells (sell mode) or closes; Escape closes
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    foxPickSlot = -1;
                    sellCardsMode = false;
                    hileliPickMode = false;
                    deckOverlay.Hide();
                    return;
                }
                // The wheel scrolls a deck too long to fit - the overlay re-lays itself out and
                // remembers where it was, so selling from page 3 stays on page 3.
                //
                // ONE NOTCH, ONE STEP - the wheel's SIGN, like every other wheel handler here
                // (the animation lab, how-to-play, the market). This was the one place that
                // divided the raw delta by a hard-coded 120: that is what a notch reports on
                // Windows, and anywhere it reports 1 instead the list crawled a hundredth of a
                // row per notch. Reading the sign cannot be wrong on any backend.
                if (mouse != null)
                {
                    float deckScroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(deckScroll) > 0.01f)
                    {
                        deckOverlay.Scroll(deckScroll > 0f
                            ? -DeckScrollRowsPerNotch : DeckScrollRowsPerNotch);
                        return;
                    }
                }
                // Dragging the scrollbar. Held across frames, so the drag survives the rebuild
                // each move causes, and it swallows the click that started it - otherwise the
                // press would fall through to "clicked nothing, close the overlay".
                if (mouse != null && deckScrollDragging)
                {
                    if (mouse.leftButton.isPressed)
                    {
                        deckOverlay.ScrollToWorldY(
                            cam.ScreenToWorldPoint(mouse.position.ReadValue()).y);
                    }
                    else
                    {
                        deckScrollDragging = false;
                    }
                    return;
                }
                if (mouse != null && mouse.leftButton.wasPressedThisFrame
                    && deckOverlay.ScrollbarAt(cam.ScreenToWorldPoint(mouse.position.ReadValue())))
                {
                    deckScrollDragging = true;
                    deckOverlay.ScrollToWorldY(
                        cam.ScreenToWorldPoint(mouse.position.ReadValue()).y);
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
                    ToggleHileliPick(deckOverlay.CardAt(pickWorld));
                    return;
                }
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    if (foxPickSlot >= 0)
                    {
                        ApplyFoxShape(deckOverlay.ShapeAt(
                            cam.ScreenToWorldPoint(mouse.position.ReadValue())));
                        return;
                    }
                    if (sellCardsMode)
                    {
                        Vector2 sellWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                        BlockCard card = deckOverlay.CardAt(sellWorld);
                        if (card != null)
                        {
                            SellCardFromDeck(card, sellWorld);
                            return;
                        }
                    }
                    // A click INSIDE the panel that hit nothing is not a request to leave: the
                    // overlay used to close on any miss, so reaching for the scrollbar or the
                    // gap between two cards dismissed the whole screen.
                    if (deckOverlay.PanelContains(cam.ScreenToWorldPoint(mouse.position.ReadValue())))
                    {
                        return;
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
                            Debug.Log("[block_bonk] Batak bet " + bet + " turns.");
                            powerBar.PulsePower(batakBetPowerId);
                            RefreshAll(null);
                        }
                    }
                }
                return;
            }
            if (choicePicker.IsOpen)
            {
                if (HandlePadChoice())
                {
                    return;
                }
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
                    // ResolveChoice answers false when it re-opened the picker itself (the debug
                    // boss list pages), in which case the pending choice has to survive.
                    if (idx < 0 || ResolveChoice(idx))
                    {
                        ClearChoice();
                    }
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
                                Debug.Log("[block_bonk] Power granted: " + granted.DisplayName);
                            }
                            else
                            {
                                Debug.Log("[block_bonk] Cannot grant " + def.DisplayName
                                    + " (already owned or no slot).");
                            }
                        }
                        else
                        {
                            JokerDefinition def = JokerRegistry.All[index];
                            if (session.CanAcquireJoker(def))
                            {
                                Joker granted = session.Jokers.Add(def.Create());
                                Debug.Log("[block_bonk] Joker granted: " + granted.DisplayName);
                            }
                            else
                            {
                                Debug.Log("[block_bonk] Cannot grant " + def.DisplayName
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
            // "Öteki dünya": the mirror world books its half of the turn before the main world
            // plays, so its input gets first refusal on the click.
            if (session.Phase == GamePhase.Round)
            {
                RoundEngine dual = session.CurrentRound;
                if (kb != null && kb.mKey.wasPressedThisFrame && TryPlayMirrorOnly(dual))
                {
                    return;
                }
                // [W] flips which world a target-less joker or power acts on.
                if (kb != null && kb.wKey.wasPressedThisFrame && ToggleEffectWorld(dual))
                {
                    return;
                }
                UpdateMirrorPreview(dual, mouse);
                if (HandleMirrorInput(dual, mouse))
                {
                    return;
                }
            }
            // THE LAST Escape handler: every modal above returns before reaching this, and
            // HandleJokerInput has just consumed Escape if a joker/power was awaiting a target.
            // So Escape only pauses when it would otherwise do nothing. Keep this last - the
            // mirror-world block above returns on its own keys and never touches Escape.
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                OpenPauseMenu();
                return;
            }
            switch (session.Phase)
            {
                case GamePhase.Market:
                    // DIRECT gamepad play steps the shelf rather than pointing at it, and gets
                    // the frame first on the frames it acts (see .PadPlay).
                    if (HandlePadMarket())
                    {
                        break;
                    }
                    // "Kredi kartı": settling the debt is a market action and never automatic,
                    // so it needs its own key. O ("öde") pays down as much as the score covers -
                    // P is already the power grant picker, and that handler runs first.
                    if (kb != null && kb.oKey.wasPressedThisFrame && session.Debt > 0)
                    {
                        long paid = session.RepayDebtInFull();
                        if (paid > 0)
                        {
                            sfx.Buy();
                        }
                        RefreshAll(null);
                    }
                    else if (kb != null && kb.nKey.wasPressedThisFrame)
                    {
                        session.LeaveMarket();
                        marketView.Hide();
                        StartRoundPresentation();
                    }
                    // PRESS AND HOLD on a bar card owns the press outright (tap = use, hold =
                    // sell), so it runs BEFORE the click handler and the shelf only ever sees
                    // presses that were not on a joker or a power. It is polled every frame
                    // rather than only on the press, because the hold light and the drag-off
                    // cancel both need the frames in between.
                    else if (HandleBarHold(mouse))
                    {
                        // owned by the hold this frame
                    }
                    else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    {
                        marketDragFrom = mouse.position.ReadValue();
                        marketDragged = 0f;
                        HandleMarketClick(mouse);
                    }
                    // The shelf SCROLLS when it is stacked (a phone). Wheel for a mouse, and a
                    // held drag for a finger - the drag is read after the click above, so a tap
                    // still buys and only real movement scrolls.
                    if (mouse != null)
                    {
                        float wheel = mouse.scroll.ReadValue().y;
                        if (Mathf.Abs(wheel) > 0.01f)
                        {
                            marketView.Scroll(wheel > 0f ? -1f : 1f);
                        }
                        else if (mouse.leftButton.isPressed)
                        {
                            Vector2 at = mouse.position.ReadValue();
                            float dy = at.y - marketDragFrom.y;
                            if (Mathf.Abs(dy) > 1f)
                            {
                                marketDragged += Mathf.Abs(dy);
                                // Screen pixels to notches, through the camera's own scale, so a
                                // drag moves the shelf the distance the finger moved.
                                float perUnit = Screen.height
                                    / Mathf.Max(2f * cam.orthographicSize, 0.0001f);
                                marketView.Scroll(dy / Mathf.Max(perUnit, 0.0001f) / 0.55f);
                                marketDragFrom = at;
                            }
                        }
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
                            // Advancing out of the FINAL round wins the run instead of opening a
                            // market, so only show it when there really is one.
                            if (session.Phase == GamePhase.Market)
                            {
                                marketView.Show(session);
                            }
                        }
                        else if (kb != null && kb.cKey.wasPressedThisFrame)
                        {
                            // continuing costs cards and redraws the hand (see RoundEngine)
                            round.DecideAdvance(false);
                            // the overtime fire ignites the moment the player chooses to
                            // continue, not on their next placement
                            RefreshFlames(round.ContinueCount);
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
                        // DIRECT gamepad play gets the frame first, and only takes it on the
                        // frames it actually acts on - so the mouse, the drag and every debug
                        // key below go on working right beside it (see .PadPlay).
                        if (HandlePadRound(round))
                        {
                            return;
                        }
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
                            Debug.Log("[block_bonk] Debug bonus card: " + bonus);
                            RefreshAll(null);
                        }
                        else if (kb != null && kb.fKey.wasPressedThisFrame)
                        {
                            // "Tamagotchi": F feeds the pet the card under the cursor. Hovering
                            // rather than a mode, so it never has to be turned off again, and
                            // the player picks WHICH copy of a shape they part with.
                            TryFeedPetUnderCursor(round, mouse);
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
