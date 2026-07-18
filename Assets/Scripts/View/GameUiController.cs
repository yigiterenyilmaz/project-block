// PURPOSE: The single scene component (lives in Assets/Scenes/enes.unity). Creates the
// GameSession, builds the debug UI at runtime (no scene-authored visuals), and turns
// player input into calls on the core engine.
// CONTROLS: drag a card from the hand onto the board to place it,
//           A = advance / C = continue on an offer, N = leave market, R = new run,
//           S = redraw hand (debug), J = grant the next joker, K = sell the last joker,
//           1-9 = activate that joker (a joker that needs a target then waits for a click,
//           Esc cancels).
// NOTE FOR AGENTS: this is placeholder presentation. Extend gameplay in
// ProjectBlock.Core; only wiring/visuals belong here. The joker hotkeys stand in for the
// market, which does not exist yet - delete them once jokers can be bought.

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
        private JokerBarView jokerBar;
        private Text infoText;
        private Text messageText;
        private Camera cam;
        private CardVisual draggedCard;
        private int lastSeedUsed;

        /// <summary>Set while an activated joker waits for the player to pick a target.</summary>
        private int? pendingTargetJokerId;

        /// <summary>Debug grant order: walks the registry so every joker is reachable.</summary>
        private int nextGrantIndex;

        private void Start()
        {
            cam = Camera.main;
            BuildViews();
            NewGame();
        }

        public void NewGame()
        {
            lastSeedUsed = seed != 0 ? seed : System.Environment.TickCount;
            var config = new GameConfig();
            config.RngSeed = lastSeedUsed;
            session = new GameSession(config);
            draggedCard = null;
            pendingTargetJokerId = null;
            nextGrantIndex = 0;
            Debug.Log("[project_block] New run, seed " + lastSeedUsed);
            StartRoundPresentation();
        }

        /// <summary>Board + HUD refresh with the round-start shuffle-and-deal animation.</summary>
        private void StartRoundPresentation()
        {
            RoundEngine round = session.CurrentRound;
            if (boardView.Board != round.Board)
            {
                boardView.Rebuild(round.Board, maxBoardWorldSize, BoardCenter);
            }
            boardView.Refresh();
            boardView.ClearPreview();
            cardLayer.AnimateRoundStart(round);
            UpdateHud();
            jokerBar.Refresh(session, pendingTargetJokerId);
        }

        private void Update()
        {
            if (session == null)
            {
                return;
            }
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                deckOverlay.Hide();
                NewGame();
                return;
            }
            if (deckOverlay.IsOpen)
            {
                // modal: any click or Escape closes, everything else is blocked
                if ((mouse != null && mouse.leftButton.wasPressedThisFrame)
                    || (kb != null && kb.escapeKey.wasPressedThisFrame))
                {
                    deckOverlay.Hide();
                }
                return;
            }
            HandleJokerInput(kb);
            switch (session.Phase)
            {
                case GamePhase.Market:
                    if (kb != null && kb.nKey.wasPressedThisFrame)
                    {
                        session.LeaveMarket();
                        StartRoundPresentation();
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
                        }
                        else if (kb != null && kb.cKey.wasPressedThisFrame)
                        {
                            round.DecideAdvance(false);
                            RefreshAll(null);
                        }
                    }
                    else if (round.Status == RoundStatus.InProgress)
                    {
                        if (kb != null && kb.sKey.wasPressedThisFrame)
                        {
                            // debug: discard the hand, shuffle it into the draw pile, redraw
                            round.RedrawHand();
                            cardLayer.AnimateRedraw(round);
                            UpdateHud();
                        }
                        else
                        {
                            HandleDrag(round, mouse);
                        }
                    }
                    break;
            }
        }

        /// <summary>Debug joker controls. These stand in for the market: J grants the next
        /// joker in the registry, K sells the last one, 1-9 activate.</summary>
        private void HandleJokerInput(Keyboard kb)
        {
            if (kb == null)
            {
                return;
            }
            if (kb.escapeKey.wasPressedThisFrame && pendingTargetJokerId.HasValue)
            {
                pendingTargetJokerId = null;
                UpdateHud();
                return;
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
                return;
            }
            if (kb.kKey.wasPressedThisFrame && session.Jokers.Count > 0)
            {
                Joker last = session.Jokers.Jokers[session.Jokers.Count - 1];
                int paid = session.Jokers.Sell(last);
                Debug.Log("[project_block] Joker sold: " + last.DisplayName + " for " + paid);
                pendingTargetJokerId = null;
                RefreshAll(null);
                return;
            }

            if (session.Phase != GamePhase.Round)
            {
                return;
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
                    return;
                }
            }
        }

        /// <summary>Runs a joker, or arms targeting mode if it needs to be pointed at something.</summary>
        private void BeginActivation(Joker joker)
        {
            if (!session.Jokers.CanActivate(joker.InstanceId))
            {
                Debug.Log("[project_block] " + joker.DisplayName + " cannot be used right now.");
                return;
            }
            if (joker.Targeting == JokerTargeting.HandCard)
            {
                pendingTargetJokerId = joker.InstanceId;
                UpdateHud();
                jokerBar.Refresh(session, pendingTargetJokerId);
                return;
            }
            RunActivation(joker, ActivationTarget.None);
        }

        private void RunActivation(Joker joker, ActivationTarget target)
        {
            pendingTargetJokerId = null;
            if (!session.Jokers.TryActivate(joker.InstanceId, target))
            {
                Debug.Log("[project_block] " + joker.DisplayName + " could not be used.");
                RefreshAll(null);
                return;
            }
            Debug.Log("[project_block] Joker used: " + joker.DisplayName);
            RoundEngine round = session.CurrentRound;
            // The whole-hand redraw has its own animation; everything else just re-syncs.
            if (joker.DefId == "renovasyon" && round.Status != RoundStatus.Lost)
            {
                cardLayer.AnimateRedraw(round);
                boardView.Refresh();
                UpdateHud();
                jokerBar.Refresh(session, null);
                return;
            }
            RefreshAll(null);
        }

        private void HandleDrag(RoundEngine round, Mouse mouse)
        {
            if (mouse == null)
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());

            // Targeting mode: the next hand card clicked is the joker's target.
            if (pendingTargetJokerId.HasValue)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    Joker joker = session.Jokers.Find(pendingTargetJokerId.Value);
                    CardVisual hit = cardLayer.CardAt(world);
                    if (joker == null || hit == null || hit.SlotIndex < 0
                        || hit.SlotIndex >= round.Hand.Count)
                    {
                        pendingTargetJokerId = null;
                        UpdateHud();
                        jokerBar.Refresh(session, null);
                        return;
                    }
                    RunActivation(joker, ActivationTarget.Hand(hit.SlotIndex));
                }
                return;
            }

            if (draggedCard == null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (cardLayer.IsDrawPileAt(world))
                    {
                        deckOverlay.Show(round.Deck.DrawPile);
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
            BlockShape shape = ShapeOfSlot(round, draggedCard.SlotIndex);
            GridPos hovered;
            bool overBoard = shape != null && boardView.TryWorldToCell(world, out hovered);
            var origin = default(GridPos);
            bool valid = false;
            if (overBoard)
            {
                boardView.TryWorldToCell(world, out hovered);
                // Anchor the shape so the cursor sits roughly at its center.
                origin = new GridPos(hovered.X - (shape.Width - 1) / 2, hovered.Y - (shape.Height - 1) / 2);
                valid = round.Board.CanPlace(shape, origin);
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
                    RefreshAll(report);
                }
                else
                {
                    released.MoveTo(released.HomePosition, 0.2f, null);
                    boardView.ClearPreview();
                }
            }
        }

        private static BlockShape ShapeOfSlot(RoundEngine round, int slot)
        {
            if (slot < 0)
            {
                return null;
            }
            if (slot < round.Hand.Count)
            {
                return round.Hand[slot].Shape;
            }
            int bonusIndex = slot - round.Hand.Count;
            return bonusIndex < round.BonusHand.Count ? round.BonusHand[bonusIndex].Card.Shape : null;
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
            UpdateHud();
            jokerBar.Refresh(session, pendingTargetJokerId);
        }

        private void UpdateHud()
        {
            RoundEngine round = session.CurrentRound;
            var sb = new StringBuilder();
            sb.Append("Seed ").Append(lastSeedUsed).Append('\n');
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
            sb.Append('\n');
            sb.Append("Drag a card onto the board to place it.   Click draw pile: view deck\n");
            sb.Append("S: redraw hand (debug)   J: grant joker   K: sell last joker   R: new run");
            infoText.text = sb.ToString();

            if (pendingTargetJokerId.HasValue)
            {
                Joker targeting = session.Jokers.Find(pendingTargetJokerId.Value);
                messageText.text = (targeting != null ? targeting.DisplayName : "Joker")
                    + ": iade edilecek bloğu seç\n[Esc] vazgeç";
                return;
            }

            switch (session.Phase)
            {
                case GamePhase.GameOver:
                    messageText.text = "GAME OVER - " + DescribeLoss(round.Loss) + "\n[R] new run";
                    break;
                case GamePhase.Market:
                    messageText.text = "MARKET (empty for now)\n[N] start round " + (session.RoundNumber + 1);
                    break;
                default:
                    messageText.text = round.Status == RoundStatus.AwaitingAdvanceDecision
                        ? "Threshold reached!\n[A] advance to market    [C] continue this round (risky)"
                        : string.Empty;
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

            var jokerGo = new GameObject("JokerBarView");
            jokerGo.transform.SetParent(transform, false);
            jokerBar = jokerGo.AddComponent<JokerBarView>();

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
