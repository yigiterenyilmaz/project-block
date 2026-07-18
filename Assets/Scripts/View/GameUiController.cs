// PURPOSE: The single scene component (lives in Assets/Scenes/enes.unity). Creates the
// GameSession, builds the debug UI at runtime (no scene-authored visuals), and turns
// player input into calls on the core engine.
// CONTROLS: drag a card from the hand onto the board to place it,
//           A = advance / C = continue on an offer, N = leave market, R = new run.
// NOTE FOR AGENTS: this is placeholder presentation. Extend gameplay in
// ProjectBlock.Core; only wiring/visuals belong here.

using System.Collections;
using System.Text;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;
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
        private Text infoText;
        private Text messageText;
        private Camera cam;
        private Vector3 camBasePosition;
        private Coroutine shakeRoutine;
        private CardVisual draggedCard;
        private int lastSeedUsed;

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
            session = new GameSession(config);
            draggedCard = null;
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
                        else if (kb != null && kb.bKey.wasPressedThisFrame)
                        {
                            // debug: random bonus card to test the bonus hand
                            BlockCard bonus = session.DebugAddRandomBonusCard();
                            Debug.Log("[project_block] Debug bonus card: " + bonus);
                            RefreshAll(null);
                        }
                        else
                        {
                            HandleDrag(round, mouse);
                        }
                    }
                    break;
            }
        }

        private void HandleDrag(RoundEngine round, Mouse mouse)
        {
            if (mouse == null)
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());

            if (draggedCard == null)
            {
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
                    if (report.CubesExploded > 0)
                    {
                        ShakeCamera(report.CleanSweep ? 0.16f : 0.09f, 0.2f);
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
            sb.Append("Drag a card onto the board to place it.   Click draw pile: view deck\n");
            sb.Append("S: redraw hand (debug)   B: bonus card (debug)   R: new run");
            infoText.text = sb.ToString();

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
