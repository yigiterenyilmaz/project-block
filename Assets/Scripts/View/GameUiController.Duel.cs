// PURPOSE: GameUiController's half of "Blackjack" - it runs the HOUSE TABLE (BlackjackTableView)
// over a duel boss stage: the two arenas, the stage's flow from the intro to the last hand, and
// the input the table takes (the bet tray, the intro's press).
//
// THE LAYOUT IS THE TABLE'S OWN (BlackjackLayout.Solve): the two arenas stand side by side centred
// on the SCREEN, the same size, with the wager and the seam between them; MainBoardWorldSize /
// MainBoardCenter answer from it for the player's arena (.Mirror), the house's arena is a second
// BoardView here, and the player's hand slides under the player's side (CardLayerView.HandShift).
// While the table is up the old HUD lines step aside - the score line and the debug readout are
// hidden, the message line is printed by the table - because the table HAS a bank, a target and
// a score duel, and the TOTAL line beside them would be a second, wrong answer.
//
// THE FLOW IS A QUEUE, so beats never overlap and never race the rules. Core settles a turn in one
// call - the player's card, the house's answer, maybe the house playing out its whole half and the
// hand being settled - and the View then plays it back in order: each house card from its own
// report (DuelAiPlay: the slot it left, the hand before and after, the boards around it), then the
// hand's result, then the next hand's reset or the stage's end. Input waits while the queue runs
// (DuelHoldsScreen). The intro, the bet and the deal are the same queue.
//
// THE VIEW DECIDES NOTHING: the bank is the purse, the target and the stakes are the boss's, the
// bet goes through GameSession.PlaceDuelBet, every house card and every result is a report matched
// by identity. The table only animates toward those numbers. A board that changes without a card
// coming out of the house's hand, or a bank that moves when a line goes off, would be a bug here.

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private BlackjackTableView duelTable;
        private BoardView houseBoardView;
        private float houseBuiltSize = -1f;
        private Vector2 houseBuiltCenter = new Vector2(float.NaN, float.NaN);

        /// <summary>The boss whose stage the table is showing (a new object is a new stage).</summary>
        private BlackjackBoss duelBossSeen;
        private DuelAiTurns duelTurnsSeen;
        private DuelHandResult duelHandSeen;

        /// <summary>The board the house's arena shows while a play-back runs (a copy), or null for the live one.</summary>
        private GameBoard houseShownBoard;

        /// <summary>Between hands both arenas are shown EMPTY (the felt wipe cleared them); the rules
        /// keep the last hand's boards until the next bet deals fresh ones.</summary>
        private GameBoard duelEmptyMain;
        private GameBoard duelEmptyHouse;

        private int duelPlayerScoreSeen;
        private readonly List<Func<IEnumerator>> duelQueue = new List<Func<IEnumerator>>();

        /// <summary>True while the LAST hand of the stage is being told - the purse doubled or gone.
        /// The stage is already over in the rules (the market, or the end of the run), so the table
        /// is held on screen and input waits until the player has seen how it ended.</summary>
        private bool duelEnding;

        /// <summary>True while the table is on screen.</summary>
        private bool duelShown;

        /// <summary>The animation lab is driving the table itself.</summary>
        private bool duelLabOwnsTable;

        private BlackjackLayout duelLayoutCache;
        private int duelLayoutFrame = -1;

        /// <summary>True while the table is telling something: input waits for it.</summary>
        private bool DuelHoldsScreen
        {
            get
            {
                return duelEnding || (duelShown && duelTable != null
                    && (duelTable.Busy || duelQueue.Count > 0 || duelTable.IntroOpen));
            }
        }

        /// <summary>The duel of the round in play, or null.</summary>
        private static BlackjackBoss DuelOf(RoundEngine round)
        {
            return round != null ? round.Boss as BlackjackBoss : null;
        }

        /// <summary>Where the table stands this frame (solved once a frame; cheap).</summary>
        private BlackjackLayout DuelLayoutNow
        {
            get
            {
                if (duelLayoutFrame != Time.frameCount)
                {
                    duelLayoutFrame = Time.frameCount;
                    duelLayoutCache = BlackjackLayout.Solve(BlackjackLayout.Frame.Active());
                }
                return duelLayoutCache;
            }
        }

        /// <summary>How far the player's hand moves to stand under the player's arena (0 off the table).</summary>
        private float DuelHandShift()
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (!duelLabOwnsTable && (round == null || DuelOf(round) == null || session.Phase != GamePhase.Round))
            {
                return 0f;
            }
            return DuelLayoutNow.PlayerHand.x - UiLayout.Active.HandCenter.x;
        }

        private void EnsureDuelTable()
        {
            if (duelTable != null)
            {
                return;
            }
            var go = new GameObject("BlackjackTable");
            go.transform.SetParent(transform, false);
            duelTable = go.AddComponent<BlackjackTableView>();
            duelTable.Sfx = sfx;
            duelTable.Build();
            duelTable.Place(DuelLayoutNow);
            duelTable.ResetTable();
            CardLayerView.HandShift = DuelHandShift;
        }

        // ================================================================== every frame

        /// <summary>Called near the top of Update: keeps the table in step with the stage, runs its
        /// queue, opens the bet when the table waits for one, and the music.</summary>
        private void TickDuelTable()
        {
            if (sfx != null)
            {
                sfx.TickCasino(Time.unscaledDeltaTime);
            }
            if (CardLayerView.HandShift == null)
            {
                CardLayerView.HandShift = DuelHandShift;
            }
            if (duelLabOwnsTable)
            {
                if (duelTable != null)
                {
                    BlackjackLayout lab = DuelLayoutNow;
                    if (!SameLayout(lab, duelTable.L))
                    {
                        duelTable.Place(lab);
                    }
                }
                return;
            }
            RoundEngine round = session != null ? session.CurrentRound : null;
            BlackjackBoss duel = DuelOf(round);
            bool onScreen = screen == AppScreen.Playing || screen == AppScreen.Paused;
            bool live = duel != null && session.Phase == GamePhase.Round;
            bool telling = duelShown && (duelEnding || duelQueue.Count > 0 || (duelTable != null && duelTable.Busy));
            if (duel == null || !onScreen || (!live && !telling))
            {
                if (duelShown)
                {
                    TearDownDuelTable();
                }
                return;
            }
            EnsureDuelTable();
            if (!duelShown)
            {
                duelShown = true;
                duelTable.gameObject.SetActive(true);
            }
            BlackjackLayout l = DuelLayoutNow;
            if (!SameLayout(l, duelTable.L))
            {
                duelTable.Place(l);
                if (houseBoardView != null)
                {
                    ShowHouseBoard(houseShownBoard);
                }
            }
            if (!ReferenceEquals(duel, duelBossSeen))
            {
                BeginDuelStage(duel, round);
            }
            if (houseBoardView == null)
            {
                // the house's arena stands from the first frame of the stage, empty until a hand is dealt
                var go = new GameObject("HouseBoardView");
                go.transform.SetParent(transform, false);
                houseBoardView = go.AddComponent<BoardView>();
                houseBuiltSize = -1f;
                ShowHouseBoard(houseShownBoard);
            }
            PumpDuelQueue();
            if (live && duel.AwaitingBet && !duelTable.Busy && duelQueue.Count == 0 && !duelEnding
                && !duelTable.BetTrayOpen && !duelTable.IntroOpen && round.Status == RoundStatus.InProgress)
            {
                OpenDuelBetTray(duel);
            }
            // the old HUD lines step aside; the table prints the message line itself
            if (totalText != null)
            {
                totalText.enabled = false;
            }
            if (messageText != null)
            {
                messageText.enabled = false;
                duelTable.SetMessage(messageText.text);
            }
            if (BlackjackFx.Debug.ShowAudioDuck || BlackjackFx.Debug.ShowHandResult)
            {
                duelTable.DebugExtra = DuelDebugLine(duel);
            }
        }

        private static bool SameLayout(BlackjackLayout a, BlackjackLayout b)
        {
            return Mathf.Approximately(a.BoardSize, b.BoardSize) && (a.Player - b.Player).sqrMagnitude < 1e-6f
                && (a.Dealer - b.Dealer).sqrMagnitude < 1e-6f && (a.Bank - b.Bank).sqrMagnitude < 1e-6f;
        }

        private string DuelDebugLine(BlackjackBoss duel)
        {
            string s = "AUDIO mood " + (sfx != null ? sfx.CasinoMood.ToString() : "-")
                + "  duck " + (sfx != null ? sfx.CasinoDuckNow.ToString("0.00") : "-")
                + "   QUEUE " + duelQueue.Count + (duelTable.Busy ? " busy" : "");
            DuelHandResult h = duel != null ? duel.LastHand : null;
            if (h != null)
            {
                s += "\nHAND " + h.HandNumber + "  " + h.Outcome + "  you " + h.PlayerScore + " - " + h.AiScore
                    + " house  bet " + h.Bet + "  payout " + h.Payout + "  bank " + h.PurseAfter + "/" + h.Target
                    + (h.StageWon ? "  WON" : "") + (h.Bankrupt ? "  BROKE" : "");
            }
            return s;
        }

        private void TearDownDuelTable()
        {
            duelShown = false;
            duelQueue.Clear();
            duelEnding = false;
            duelBossSeen = null;
            duelTurnsSeen = null;
            duelHandSeen = null;
            houseShownBoard = null;
            duelEmptyMain = duelEmptyHouse = null;
            if (duelTable != null)
            {
                duelTable.ResetTable();
                duelTable.gameObject.SetActive(false);
            }
            if (houseBoardView != null)
            {
                Destroy(houseBoardView.gameObject);
                houseBoardView = null;
            }
            houseBuiltSize = -1f;
            if (totalText != null)
            {
                totalText.enabled = true;
            }
            if (messageText != null)
            {
                messageText.enabled = true;
            }
            if (sfx != null)
            {
                sfx.SetCasinoMood(CasinoMusicMood.Off);
            }
        }

        private void PumpDuelQueue()
        {
            if (duelQueue.Count == 0 || duelTable.Busy)
            {
                return;
            }
            Func<IEnumerator> next = duelQueue[0];
            duelQueue.RemoveAt(0);
            duelTable.Run(next());
        }

        // ================================================================== the stage

        /// <summary>A new duel stage on screen (or a loaded one): the table is laid for where the
        /// rules stand, and a fresh stage opens with its intro.</summary>
        private void BeginDuelStage(BlackjackBoss duel, RoundEngine round)
        {
            bool quiet = duelResumeQuiet;
            duelResumeQuiet = false;
            duelBossSeen = duel;
            duelTurnsSeen = duel.LastAiTurns;
            duelHandSeen = duel.LastHand;
            duelQueue.Clear();
            duelEnding = false;
            houseShownBoard = null;
            duelEmptyMain = duelEmptyHouse = null;
            duelTable.ResetTable();
            duelTable.Place(DuelLayoutNow);
            duelTable.SetTarget(duel.Target, duel.StartPurse);
            long bank = session.TotalScore;
            if (duel.Phase == BlackjackBoss.DuelPhase.Playing)
            {
                // a run loaded in the middle of a hand: the table as it stands
                duelTable.SetBank(bank, false);
                duelTable.SetWager(duel.Bet, true);
                duelTable.SetHandLine(duel.HandNumber, duel.Bet);
                duelTable.SetScoresVisible(true);
                duelPlayerScoreSeen = duel.PlayerHandScore(round);
                duelTable.SetScores(duelPlayerScoreSeen, duel.HouseHandScore, false);
                duelTable.SetDealerCards(duel.HouseRound != null ? duel.HouseRound.Hand.Count : 0);
                duelTable.SetActiveSide(duel.PlayerDone ? BlackjackTableView.Side.None : BlackjackTableView.Side.Player);
                duelTable.SetBetTension(bank + duel.Bet > 0 ? duel.Bet / (float)(bank + duel.Bet) : 0f, bank == 0);
                sfx.SetCasinoMood(bank == 0 ? CasinoMusicMood.AllIn : CasinoMusicMood.Hand);
                return;
            }
            if (duel.HandNumber > 0)
            {
                // loaded between hands: the arenas are already cleared
                duelTable.SetBank(bank, false);
                ClearDuelArenas(round);
                return;
            }
            if (quiet)
            {
                // back from the lab: the stage carries on, its intro already seen
                duelTable.SetBank(bank, false);
                return;
            }
            duelTable.SetBank(duel.StarterGranted ? 0 : bank, false);
            duelQueue.Add(delegate { return DuelIntroFlow(duel, bank); });
        }

        private IEnumerator DuelIntroFlow(BlackjackBoss duel, long bank)
        {
            sfx.SetCasinoMood(CasinoMusicMood.Betting);
            yield return duelTable.PlayIntro(bank, duel.Target, duel.StarterGranted, BlackjackBoss.StarterStake,
                delegate { return AnyPress(Keyboard.current, Mouse.current); });
            if (duel.StarterGranted)
            {
                // the advance lands on the table's own bank too
                duelTable.SetBank(bank, true, BlackjackFx.Tuning.T(0.45f));
                duelTable.PulseBank(1.08f);
            }
        }

        /// <summary>Both arenas shown empty between hands.</summary>
        private void ClearDuelArenas(RoundEngine round)
        {
            int w = round != null ? round.Config.BoardWidth : 7;
            int h = round != null ? round.Config.BoardHeight : 7;
            duelEmptyMain = new GameBoard(w, h);
            duelEmptyHouse = new GameBoard(w, h);
            houseShownBoard = null;
        }

        /// <summary>The board the PLAYER's arena shows: an empty one between hands, else the round's.</summary>
        private GameBoard DuelMainBoard(RoundEngine round)
        {
            BlackjackBoss duel = DuelOf(round);
            return duel != null && duelShown && duel.AwaitingBet && duelEmptyMain != null ? duelEmptyMain : null;
        }

        // ================================================================== the bet

        private void OpenDuelBetTray(BlackjackBoss duel)
        {
            long bank = session.TotalScore;
            List<long> stakes = BlackjackBoss.BetOptions(bank);
            if (stakes.Count == 0)
            {
                return;
            }
            duelTable.SetBank(bank, duelTable.ShownBank != bank);
            duelTable.SetHandLine(duel.HandNumber + 1, 0);
            duelTable.SetActiveSide(BlackjackTableView.Side.None);
            duelTable.SetWager(0, false);
            duelTable.ShowBetTray(bank, duel.Target, stakes);
            sfx.SetCasinoMood(CasinoMusicMood.Betting);
            PadPanelReset();
        }

        /// <summary>The tray's input. Called before the board-animation lock; true when it took the
        /// frame. Escape is left alone so the pause menu still opens over the table.</summary>
        private bool HandleDuelTableInput(Keyboard kb, Mouse mouse)
        {
            if (!duelShown || duelTable == null || duelLabOwnsTable || !duelTable.BetTrayOpen)
            {
                return false;
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                return false;
            }
            long confirmed = duelTable.HandleBetKeys(kb);
            Gamepad pad = Gamepad.current;
            bool padDriven = PadPanelDriven();
            if (confirmed == 0 && padDriven && pad != null)
            {
                PadStepPanel(pad, duelTable.BetItemCount, duelTable.BetItemWorldCenter);
                duelTable.FocusBetItem(padPanelIndex);
                if (pad.leftShoulder.wasPressedThisFrame)
                {
                    duelTable.NudgeBet(-1);
                }
                else if (pad.rightShoulder.wasPressedThisFrame)
                {
                    duelTable.NudgeBet(1);
                }
                else if (pad.buttonSouth.wasPressedThisFrame)
                {
                    confirmed = duelTable.PressBetItem(padPanelIndex);
                }
            }
            else if (confirmed == 0 && mouse != null && cam != null)
            {
                Vector3 sp = mouse.position.ReadValue();
                Vector3 world = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, Mathf.Abs(cam.transform.position.z)));
                Vector2 local = duelTable.transform.InverseTransformPoint(world);
                int hover = duelTable.BetItemAt(local);
                duelTable.HoverBetItem(hover);
                if (mouse.leftButton.wasPressedThisFrame && hover >= 0)
                {
                    confirmed = duelTable.PressBetItem(hover);
                }
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    duelTable.NudgeBet(wheel > 0f ? 1 : -1);
                }
            }
            if (confirmed > 0)
            {
                ConfirmDuelBet(confirmed);
            }
            return true;
        }

        /// <summary>Kept for the shared picker's old route (ChoiceKind.DuelBet): a stake from it is
        /// placed exactly as one from the tray.</summary>
        private void ResolveDuelBet(int value)
        {
            ConfirmDuelBet(value);
        }

        /// <summary>The tray is the bet now; this only answers whether it holds the frame.</summary>
        private bool TryOfferDuelBet(RoundEngine round)
        {
            return duelShown && duelTable != null && duelTable.BetTrayOpen;
        }

        /// <summary>A stake confirmed: Core places it and deals, then the table plays the money onto
        /// the plate and the cut - and only then is the new hand put on screen.</summary>
        private void ConfirmDuelBet(long amount)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            BlackjackBoss duel = DuelOf(round);
            long before = session.TotalScore;
            if (duel == null || !session.PlaceDuelBet(amount))
            {
                return;
            }
            bool allIn = amount == before;
            duelQueue.Add(delegate { return DuelBetPlacedFlow(duel, before, amount, allIn); });
            PumpDuelQueue();
        }

        private IEnumerator DuelBetPlacedFlow(BlackjackBoss duel, long before, long amount, bool allIn)
        {
            duelTable.SetBetTension(before > 0 ? amount / (float)before : 0f, allIn);
            yield return duelTable.PlayBetPlaced(before, session.TotalScore, amount, allIn);
            duelTable.SetHandLine(duel.HandNumber, amount);
            duelTable.ClearSides();
            duelTable.SetScores(0, 0, false);
            duelTable.SetScoresVisible(true);
            duelPlayerScoreSeen = 0;
            sfx.SetCasinoMood(allIn ? CasinoMusicMood.AllIn : CasinoMusicMood.Hand);
            yield return duelTable.PlayDealSplit(duel.HouseRound != null ? duel.HouseRound.Hand.Count : 0);
            duelEmptyMain = duelEmptyHouse = null;
            houseShownBoard = null;
            duelTurnsSeen = duel.LastAiTurns;
            RefreshAll(null);
            cardLayer.AnimateRoundStart(session.CurrentRound);
            duelTable.SetActiveSide(BlackjackTableView.Side.Player);
            if (messageText != null)
            {
                messageText.text = Loc.Pick("Outscore the house with your half of the deck.",
                    "Destenin yarısıyla kasadan fazla puan topla.");
            }
            yield return new WaitForSeconds(BlackjackFx.Tuning.T(0.25f));
        }

        // ================================================================== the repaint

        /// <summary>Called from RefreshAll: the house's arena, the player's score, and any new report
        /// from the rules queued to be played back in order.</summary>
        private void RefreshDuelTable(RoundEngine round)
        {
            BlackjackBoss duel = DuelOf(round);
            if (duel == null || !duelShown || duelTable == null || duelLabOwnsTable)
            {
                if (houseBoardView != null && !duelLabOwnsTable && !duelShown)
                {
                    Destroy(houseBoardView.gameObject);
                    houseBoardView = null;
                    houseBuiltSize = -1f;
                }
                return;
            }
            if (houseBoardView == null)
            {
                var go = new GameObject("HouseBoardView");
                go.transform.SetParent(transform, false);
                houseBoardView = go.AddComponent<BoardView>();
            }
            ShowHouseBoard(houseShownBoard);
            if (duel.Phase == BlackjackBoss.DuelPhase.Playing)
            {
                // the player's points go to the player's score - the bank never moves for a line
                int p = duel.PlayerHandScore(round);
                if (p != duelPlayerScoreSeen)
                {
                    duelPlayerScoreSeen = p;
                    duelTable.SendScore(BlackjackTableView.Side.Player, DuelLayoutNow.Player, p);
                }
                duelTable.SetSideDone(BlackjackTableView.Side.Player, duel.PlayerDone);
            }
            DuelAiTurns turns = duel.LastAiTurns;
            if (turns != null && !ReferenceEquals(turns, duelTurnsSeen) && turns.Plays.Count > 0)
            {
                duelTurnsSeen = turns;
                houseShownBoard = turns.Plays[0].BoardBefore;
                ShowHouseBoard(houseShownBoard);
                duelQueue.Add(delegate { return DuelHouseFlow(duel, turns); });
            }
            DuelHandResult hand = duel.LastHand;
            if (hand != null && !ReferenceEquals(hand, duelHandSeen))
            {
                duelHandSeen = hand;
                duelQueue.Add(delegate { return DuelHandEndFlow(hand); });
            }
        }

        /// <summary>Puts a board on the house's arena (the live one when <paramref name="board"/> is null).</summary>
        private void ShowHouseBoard(GameBoard board)
        {
            if (houseBoardView == null)
            {
                return;
            }
            RoundEngine round = session != null ? session.CurrentRound : null;
            BlackjackBoss duel = DuelOf(round);
            if (board == null)
            {
                board = duelEmptyHouse ?? (duel != null && duel.HouseRound != null ? duel.HouseRound.Board : null);
            }
            if (board == null)
            {
                if (round == null)
                {
                    return;
                }
                duelEmptyHouse = new GameBoard(round.Config.BoardWidth, round.Config.BoardHeight);
                board = duelEmptyHouse;
            }
            BlackjackLayout l = duelLabOwnsTable ? duelTable.L : DuelLayoutNow;
            if (houseBoardView.Board != board || !Mathf.Approximately(houseBuiltSize, l.BoardSize)
                || (houseBuiltCenter - l.Dealer).sqrMagnitude > 0.000001f)
            {
                houseBoardView.Rebuild(board, l.BoardSize, l.Dealer);
                houseBuiltSize = l.BoardSize;
                houseBuiltCenter = l.Dealer;
            }
            houseBoardView.Refresh();
            houseBoardView.ClearPreview();
        }

        // ================================================================== the house's turn

        private IEnumerator DuelHouseFlow(BlackjackBoss duel, DuelAiTurns turns)
        {
            // a breath for the player's own placement to be read first
            yield return new WaitForSeconds(BlackjackFx.Tuning.T(0.12f));
            duelTable.SetActiveSide(BlackjackTableView.Side.Dealer);
            for (int i = 0; i < turns.Plays.Count; i++)
            {
                DuelAiPlay play = turns.Plays[i];
                houseShownBoard = play.BoardBefore;
                ShowHouseBoard(houseShownBoard);
                if (play.Move == null)
                {
                    continue;
                }
                yield return PlayHouseCard(duel, play, i);
                yield return new WaitForSeconds(BlackjackFx.Tuning.T(BlackjackFx.Tuning.dealerAfterPlace
                    + (play.ExplodedRows.Count + play.ExplodedColumns.Count > 0 ? 0.25f : 0f)));
            }
            houseShownBoard = null;
            ShowHouseBoard(null);
            duelTable.SetSideDone(BlackjackTableView.Side.Dealer, duel.AiDone);
            duelTable.SetActiveSide(duel.Phase == BlackjackBoss.DuelPhase.Playing && !duel.PlayerDone
                ? BlackjackTableView.Side.Player : BlackjackTableView.Side.None);
        }

        /// <summary>One house card from its report: think, the card out of the closed hand, its flight
        /// and turn, the landing - and only on the landing the block, its lines and its points.</summary>
        private IEnumerator PlayHouseCard(BlackjackBoss duel, DuelAiPlay play, int index)
        {
            var cells = new List<Vector2>();
            Vector2 sum = Vector2.zero;
            for (int c = 0; c < play.PlacedCells.Count; c++)
            {
                Vector2 w = houseBoardView != null ? houseBoardView.CellToWorld(play.PlacedCells[c]) : DuelLayoutNow.Dealer;
                Vector2 local = duelTable.transform.InverseTransformPoint(w);
                cells.Add(local);
                sum += local;
            }
            Vector2 landAt = cells.Count > 0 ? sum / cells.Count : DuelLayoutNow.Dealer;
            float cell = houseBoardView != null ? houseBoardView.CellWorldSize : 0.6f;
            float think = BlackjackTableView.ThinkSecondsFor(duel.HandNumber, index + play.HandBefore * 7);
            yield return duelTable.PlayDealerCard(play.HandSlot, play.HandBefore, play.HandAfter, play.Card,
                play.Move.Shape, landAt, cells, cell, think, delegate { LandHouseCard(play, landAt); });
        }

        private void LandHouseCard(DuelAiPlay play, Vector2 landAt)
        {
            houseShownBoard = play.BoardAfter;
            ShowHouseBoard(houseShownBoard);
            sfx.Place(play.PlacedCells.Count);
            Vector2 from = landAt;
            // the HOUSE's own streak sets its lines' tier - never the player's combo
            int tier = Mathf.Clamp(play.ComboCount, 1, LineBurstView.MaxTier);
            int lines = play.ExplodedRows.Count + play.ExplodedColumns.Count;
            if (lines > 0)
            {
                sfx.Explode(lines, play.ComboCount);
            }
            if (houseBoardView != null && play.BoardAfter != null)
            {
                GameBoard b = play.BoardAfter;
                foreach (int y in play.ExplodedRows)
                {
                    FlashLine(b, b.MinY + y, true, houseBoardView, tier);
                    from = duelTable.transform.InverseTransformPoint(houseBoardView.CellToWorld(new GridPos(b.MinX + b.Width / 2, b.MinY + y)));
                }
                foreach (int x in play.ExplodedColumns)
                {
                    FlashLine(b, b.MinX + x, false, houseBoardView, tier);
                    from = duelTable.transform.InverseTransformPoint(houseBoardView.CellToWorld(new GridPos(b.MinX + x, b.MinY + b.Height / 2)));
                }
            }
            if (play.Gained != 0)
            {
                duelTable.SendScore(BlackjackTableView.Side.Dealer, from, play.HandScoreAfter);
            }
        }

        // ================================================================== the hand's end

        private IEnumerator DuelHandEndFlow(DuelHandResult hand)
        {
            duelEnding = hand.StageWon || hand.Bankrupt;
            long bankBefore = duelTable.ShownBank;
            if (messageText != null)
            {
                messageText.text = string.Empty;
            }
            yield return duelTable.PlayHandResult(hand, bankBefore);
            if (hand.StageWon)
            {
                yield return duelTable.PlayBossWin(hand.PurseAfter);
                EndDuelStage();
                yield break;
            }
            if (hand.Bankrupt)
            {
                yield return duelTable.PlayBankrupt();
                EndDuelStage();
                yield break;
            }
            yield return duelTable.PlayNextHand(delegate
            {
                ClearDuelArenas(session != null ? session.CurrentRound : null);
                if (session != null && session.Phase == GamePhase.Round)
                {
                    RefreshAll(null);
                }
            });
        }

        /// <summary>The stage is over in the rules and now on screen too: the market, or the run's end.</summary>
        private void EndDuelStage()
        {
            duelEnding = false;
            if (session != null && session.Phase == GamePhase.Market)
            {
                marketView.ResetScroll();
                marketView.Show(session);
            }
            if (session != null)
            {
                RefreshAll(null);
            }
        }

        // ================================================================== the rest of the screen

        /// <summary>The score line under the old HUD while a duel stands (hidden while the table is up).</summary>
        private string DuelScoreLine(RoundEngine round)
        {
            BlackjackBoss duel = DuelOf(round);
            if (duel == null)
            {
                return null;
            }
            return Loc.Pick("bank ", "kasan ") + session.TotalScore + Loc.Pick("  /  target ", "  /  hedef ") + duel.Target;
        }

        /// <summary>A power the table switches off, clicked: refused with the table's own words and a
        /// muted click. True when it refused.</summary>
        private bool DuelRefusesPower(Power power)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            BlackjackBoss duel = DuelOf(round);
            if (duel == null || power == null || !duel.DisablesPower(power))
            {
                return false;
            }
            if (messageText != null)
            {
                messageText.text = Loc.Pick("Not usable at the blackjack table.", "Blackjack masasında kullanılamaz.");
            }
            sfx.Casino(BlackjackCue.PowerBlocked);
            return true;
        }

        /// <summary>Where the player's hand score is, for anything that sends points to "the score"
        /// while the table is up (a joker's payout lands on the duel's score, never the bank).</summary>
        private bool TryDuelScoreAnchor(out Vector2 world)
        {
            if (duelShown && duelTable != null)
            {
                world = duelTable.transform.TransformPoint(duelTable.ScoreAnchor(BlackjackTableView.Side.Player));
                return true;
            }
            world = Vector2.zero;
            return false;
        }
    }
}
