// PURPOSE: The single scene component (lives in Assets/Scenes/enes.unity). Creates the
// GameSession, builds the debug UI at runtime (no scene-authored visuals), and turns
// player input into calls on the core engine.
// CONTROLS: click a card or press 1-9 to select, click the board to place,
//           A = advance / C = continue on an offer, N = leave market, R = new run.
// NOTE FOR AGENTS: this is placeholder presentation. Extend gameplay in
// ProjectBlock.Core; only wiring/visuals belong here.

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

        private static readonly Vector2 BoardCenter = new Vector2(0f, 0.7f);
        private static readonly Vector2 HandCenter = new Vector2(0f, -4.1f);
        private const float HandSpacing = 2.0f;

        private GameSession session;
        private BoardView boardView;
        private HandView handView;
        private Text infoText;
        private Text messageText;
        private Camera cam;
        private int selectedSlot = -1;
        private int lastSeedUsed;

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
            selectedSlot = -1;
            Debug.Log("[project_block] New run, seed " + lastSeedUsed);
            RefreshAll();
        }

        private void Update()
        {
            if (session == null)
            {
                return;
            }
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                NewGame();
                return;
            }
            switch (session.Phase)
            {
                case GamePhase.Market:
                    if (kb != null && kb.nKey.wasPressedThisFrame)
                    {
                        session.LeaveMarket();
                        selectedSlot = -1;
                        RefreshAll();
                    }
                    break;
                case GamePhase.Round:
                    RoundEngine round = session.CurrentRound;
                    if (round.Status == RoundStatus.AwaitingAdvanceDecision)
                    {
                        if (kb != null && kb.aKey.wasPressedThisFrame)
                        {
                            round.DecideAdvance(true);
                            RefreshAll();
                        }
                        else if (kb != null && kb.cKey.wasPressedThisFrame)
                        {
                            round.DecideAdvance(false);
                            RefreshAll();
                        }
                    }
                    else if (round.Status == RoundStatus.InProgress)
                    {
                        HandlePlacementInput(round, kb, Mouse.current);
                    }
                    break;
            }
        }

        private void HandlePlacementInput(RoundEngine round, Keyboard kb, Mouse mouse)
        {
            int slotCount = round.Hand.Count + round.BonusHand.Count;
            if (kb != null)
            {
                for (int i = 0; i < slotCount && i < 9; i++)
                {
                    if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                    {
                        selectedSlot = i;
                        RefreshAll();
                    }
                }
            }
            if (mouse == null)
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            bool clicked = mouse.leftButton.wasPressedThisFrame;

            if (clicked)
            {
                int slotHit = handView.SlotIndexAt(world);
                if (slotHit >= 0)
                {
                    selectedSlot = slotHit;
                    RefreshAll();
                    return;
                }
            }

            BlockShape shape = ShapeOfSlot(round, selectedSlot);
            if (shape == null)
            {
                boardView.ClearPreview();
                return;
            }
            GridPos hovered;
            if (!boardView.TryWorldToCell(world, out hovered))
            {
                boardView.ClearPreview();
                return;
            }
            // Anchor the shape so the cursor sits roughly at its center.
            var origin = new GridPos(hovered.X - (shape.Width - 1) / 2, hovered.Y - (shape.Height - 1) / 2);
            bool valid = round.Board.CanPlace(shape, origin);
            boardView.ShowPreview(shape, origin, valid);
            if (clicked && valid)
            {
                TurnReport report = selectedSlot < round.Hand.Count
                    ? round.PlayFromHand(selectedSlot, origin)
                    : round.PlayFromBonus(selectedSlot - round.Hand.Count, origin);
                if (verboseTurnLogs)
                {
                    LogTurn(report);
                }
                selectedSlot = -1;
                RefreshAll();
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

        private void RefreshAll()
        {
            RoundEngine round = session.CurrentRound;
            if (boardView.Board != round.Board)
            {
                boardView.Rebuild(round.Board, maxBoardWorldSize, BoardCenter);
            }
            int slotCount = round.Hand.Count + round.BonusHand.Count;
            if (selectedSlot >= slotCount)
            {
                selectedSlot = slotCount - 1;
            }
            if (selectedSlot < 0 && slotCount > 0)
            {
                selectedSlot = 0;
            }
            boardView.Refresh();
            boardView.ClearPreview();
            handView.Refresh(round, selectedSlot, HandCenter, HandSpacing);
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
            sb.Append("Select card: click / 1-9   Place: click board   R: new run");
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

            var handGo = new GameObject("HandView");
            handGo.transform.SetParent(transform, false);
            handView = handGo.AddComponent<HandView>();

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
