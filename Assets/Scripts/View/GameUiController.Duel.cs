// PURPOSE: GameUiController's half of "Blackjack" - the duel table on screen.
//
// THE TABLE. The two arenas stand SIDE BY SIDE in the layout "Öteki dünya" already solved for two
// worlds (MainWorldCenter / MirrorWorldCenter, MirrorBoardWorldSize): the player's on the left,
// the HOUSE's on the right, a name plate over each with that side's points this hand. The house's
// arena is a second BoardView over BlackjackBoss.HouseRound's board, so everything it lays is
// drawn in the game's own cubes - and nothing of its hand, its pile or its discard is drawn at
// all: the player sees its arena and nothing else.
//
// THE HOUSE'S TURN is played back ONE CARD AT A TIME (PlayHouseTurns), from the copies of its
// board the boss takes around every play (DuelAiPlay.BoardBefore / BoardAfter): the arena as it
// was, the card's silhouette where it is going (BoardView.ShowPreview), then the arena after it
// with its lines going off through the same FlashLine a player's line uses. When the player has
// finished first the house plays out the rest of its half in the same way, a card after a card.
// Input is held while it plays (DuelHousePlaying), and so is the next bet.
//
// THE BET is the shared option picker (ChoicePickerView - mouse, keys and both pad schemes for
// free) offered whenever the table waits for one, a beat after the last hand was settled so its
// result is read first. The stakes are Core's (BlackjackBoss.BetOptions); placing one goes through
// GameSession.PlaceDuelBet and the deal is presented like a round's start.
//
// THE VIEW DECIDES NOTHING: every number is the boss's, every play is the boss's report, matched
// by identity like every other per-turn report.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private BoardView houseBoardView;
        private float houseBuiltSize = -1f;
        private Vector2 houseBuiltCenter = new Vector2(float.NaN, float.NaN);
        private TextMesh duelPlayerLabel;
        private TextMesh duelHouseLabel;

        /// <summary>The house's report last played back, and the hand result last shown.</summary>
        private DuelAiTurns duelTurnsSeen;
        private DuelHandResult duelHandSeen;

        /// <summary>The board the house's arena is showing while a play-back runs (a copy), or
        /// null to show the live one.</summary>
        private GameBoard houseShownBoard;

        /// <summary>When the bet may be offered again (a beat after a hand is settled).</summary>
        private float duelBetNotBefore;

        private Coroutine houseRoutine;

        private static readonly Color DuelHouseLabelColor = new Color(1f, 0.78f, 0.55f, 0.92f);
        private static readonly Color DuelWinColor = new Color(0.55f, 1f, 0.6f, 1f);
        private static readonly Color DuelLoseColor = new Color(1f, 0.45f, 0.45f, 1f);
        private static readonly Color DuelPushColor = new Color(0.86f, 0.86f, 0.92f, 1f);

        /// <summary>Seconds the house's card hangs as a silhouette before it lands, and the beat
        /// after it lands before the next one.</summary>
        private const float HouseAimSeconds = 0.32f;
        private const float HouseSettleSeconds = 0.30f;

        /// <summary>True while the house's turn is being played back: input waits for it.</summary>
        private bool DuelHousePlaying
        {
            get { return houseRoutine != null; }
        }

        /// <summary>The duel of the round in play, or null.</summary>
        private static BlackjackBoss DuelOf(RoundEngine round)
        {
            return round != null ? round.Boss as BlackjackBoss : null;
        }

        /// <summary>Builds, refreshes or takes down the duel table to match the round. Called from
        /// RefreshAll, right after the mirror world (the two never stand at once - the boss
        /// switches the second world off).</summary>
        private void RefreshDuelTable(RoundEngine round)
        {
            BlackjackBoss duel = session != null && session.Phase == GamePhase.Round ? DuelOf(round) : null;
            if (duel == null)
            {
                ClearDuelTable();
                return;
            }
            if (houseBoardView == null)
            {
                var go = new GameObject("HouseBoardView");
                go.transform.SetParent(transform, false);
                houseBoardView = go.AddComponent<BoardView>();
            }
            GameBoard live = duel.HouseRound != null ? duel.HouseRound.Board : null;
            ShowHouseBoard(houseShownBoard ?? live);
            RefreshDuelLabels(round, duel);
            PlayHouseTurns(duel);
            ShowHandResult(duel);
        }

        /// <summary>Puts <paramref name="board"/> on the house's arena (an empty arena of the
        /// round's size between hands, before the first deal).</summary>
        private void ShowHouseBoard(GameBoard board)
        {
            if (houseBoardView == null)
            {
                return;
            }
            if (board == null)
            {
                RoundEngine round = session != null ? session.CurrentRound : null;
                if (round == null)
                {
                    return;
                }
                board = houseBoardView.Board != null && houseBoardView.Board.Width == round.Config.BoardWidth
                    ? houseBoardView.Board
                    : new GameBoard(round.Config.BoardWidth, round.Config.BoardHeight);
            }
            if (houseBoardView.Board != board
                || !Mathf.Approximately(houseBuiltSize, MirrorBoardWorldSize)
                || (houseBuiltCenter - MirrorWorldCenter).sqrMagnitude > 0.000001f)
            {
                houseBoardView.Rebuild(board, MirrorBoardWorldSize, MirrorWorldCenter);
                houseBuiltSize = MirrorBoardWorldSize;
                houseBuiltCenter = MirrorWorldCenter;
            }
            houseBoardView.Refresh();
            houseBoardView.ClearPreview();
        }

        /// <summary>The name plate over each arena, with that side's points this hand.</summary>
        private void RefreshDuelLabels(RoundEngine round, BlackjackBoss duel)
        {
            if (duelPlayerLabel == null)
            {
                duelPlayerLabel = ViewUtil.MakeText3D(transform, "DuelPlayerLabel", Vector2.zero,
                    "", 44, 0.05f, WorldLabelColor, 41, TextAnchor.LowerCenter);
                duelHouseLabel = ViewUtil.MakeText3D(transform, "DuelHouseLabel", Vector2.zero,
                    "", 44, 0.05f, DuelHouseLabelColor, 41, TextAnchor.LowerCenter);
            }
            float labelY = MirrorBoardWorldSize * 0.5f + 0.08f;
            duelPlayerLabel.transform.position = MainWorldCenter + new Vector2(0f, labelY);
            duelHouseLabel.transform.position = MirrorWorldCenter + new Vector2(0f, labelY);
            bool playing = duel.Phase == BlackjackBoss.DuelPhase.Playing;
            int house = playing ? ShownHouseScore(duel) : 0;
            duelPlayerLabel.text = Loc.Pick("YOU", "SEN")
                + (playing ? "   " + duel.PlayerHandScore(round) : string.Empty);
            duelHouseLabel.text = Loc.Pick("THE HOUSE", "KASA")
                + (playing ? "   " + house : string.Empty)
                + (playing && duel.AiDone && !DuelHousePlaying ? Loc.Pick("  (done)", "  (bitti)") : string.Empty);
        }

        /// <summary>The house's points as far as the play-back has got - the live number would
        /// give the result away before its cards are seen landing.</summary>
        private int shownHouseScore = -1;

        private int ShownHouseScore(BlackjackBoss duel)
        {
            return shownHouseScore >= 0 ? shownHouseScore : duel.HouseHandScore;
        }

        private void ClearDuelTable()
        {
            if (houseRoutine != null)
            {
                StopCoroutine(houseRoutine);
                houseRoutine = null;
            }
            if (houseBoardView != null)
            {
                Destroy(houseBoardView.gameObject);
                houseBoardView = null;
            }
            if (duelPlayerLabel != null) Destroy(duelPlayerLabel.gameObject);
            if (duelHouseLabel != null) Destroy(duelHouseLabel.gameObject);
            duelPlayerLabel = duelHouseLabel = null;
            houseBuiltSize = -1f;
            houseShownBoard = null;
            shownHouseScore = -1;
        }

        // ------------------------------------------------------------------ the house's turn

        private void PlayHouseTurns(BlackjackBoss duel)
        {
            DuelAiTurns turns = duel.LastAiTurns;
            if (turns == null || ReferenceEquals(turns, duelTurnsSeen) || turns.Plays.Count == 0)
            {
                return;
            }
            duelTurnsSeen = turns;
            if (houseRoutine != null)
            {
                StopCoroutine(houseRoutine);
            }
            houseRoutine = StartCoroutine(HouseTurnsRoutine(duel, turns));
        }

        private IEnumerator HouseTurnsRoutine(BlackjackBoss duel, DuelAiTurns turns)
        {
            RoundEngine round = session.CurrentRound;
            for (int i = 0; i < turns.Plays.Count; i++)
            {
                DuelAiPlay play = turns.Plays[i];
                // the arena as it was, and the card's silhouette where it is going
                houseShownBoard = play.BoardBefore;
                shownHouseScore = play.HandScoreAfter - play.Gained;
                ShowHouseBoard(houseShownBoard);
                RefreshDuelLabels(round, duel);
                if (play.Move != null && play.Move.Shape != null && houseBoardView != null)
                {
                    houseBoardView.ShowPreview(play.Move.Shape, play.Move.Origin, true);
                }
                yield return new WaitForSeconds(HouseAimSeconds);
                // it lands: the arena after it, its lines going off
                houseShownBoard = play.BoardAfter;
                shownHouseScore = play.HandScoreAfter;
                ShowHouseBoard(houseShownBoard);
                RefreshDuelLabels(round, duel);
                sfx.Place(play.PlacedCells.Count);
                if (houseBoardView != null && play.BoardAfter != null)
                {
                    foreach (int y in play.ExplodedRows)
                    {
                        FlashLine(play.BoardAfter, play.BoardAfter.MinY + y, true, houseBoardView);
                    }
                    foreach (int x in play.ExplodedColumns)
                    {
                        FlashLine(play.BoardAfter, play.BoardAfter.MinX + x, false, houseBoardView);
                    }
                    if (play.Gained > 0)
                    {
                        FloatingTextFx.Spawn(transform, MirrorWorldCenter + new Vector2(0f, 0.4f),
                            "+" + play.Gained, DuelHouseLabelColor, 48, 0.055f);
                    }
                }
                yield return new WaitForSeconds(HouseSettleSeconds);
            }
            houseShownBoard = null;
            shownHouseScore = -1;
            houseRoutine = null;
            if (session != null && session.Phase == GamePhase.Round)
            {
                RefreshAll(null);
            }
        }

        // ------------------------------------------------------------------ the hand's result

        private void ShowHandResult(BlackjackBoss duel)
        {
            DuelHandResult hand = duel.LastHand;
            if (hand == null || ReferenceEquals(hand, duelHandSeen))
            {
                return;
            }
            duelHandSeen = hand;
            StartCoroutine(HandResultRoutine(hand));
        }

        /// <summary>Waits for the house's cards to be seen landing, then says how the hand went.</summary>
        private IEnumerator HandResultRoutine(DuelHandResult hand)
        {
            duelBetNotBefore = float.MaxValue;
            while (DuelHousePlaying)
            {
                yield return null;
            }
            string headline;
            Color colour;
            if (hand.Outcome == DuelHandOutcome.Win)
            {
                headline = Loc.Pick("HAND WON  +", "EL SENİN  +") + hand.Payout;
                colour = DuelWinColor;
                sfx.Chime(1.2f);
            }
            else if (hand.Outcome == DuelHandOutcome.Push)
            {
                headline = Loc.Pick("PUSH - BET RETURNED", "BERABERE - BAHİS GERİ");
                colour = DuelPushColor;
            }
            else
            {
                headline = Loc.Pick("HOUSE WINS  -", "KASA KAZANDI  -") + hand.Bet;
                colour = DuelLoseColor;
            }
            Vector2 middle = (MainWorldCenter + MirrorWorldCenter) * 0.5f;
            FloatingTextFx.Spawn(transform, middle, headline, colour, 64, 0.07f);
            if (messageText != null)
            {
                messageText.text = Loc.Pick("Hand ", "El ") + hand.HandNumber + ":  "
                    + Loc.Pick("you ", "sen ") + hand.PlayerScore + "  -  " + hand.AiScore
                    + Loc.Pick(" house", " kasa") + "   |   "
                    + Loc.Pick("purse ", "kasan ") + hand.PurseAfter + " / "
                    + Loc.Pick("target ", "hedef ") + hand.Target
                    + (hand.StageWon ? Loc.Pick("   - PURSE DOUBLED!", "   - PARAN İKİYE KATLANDI!") : string.Empty)
                    + (hand.Bankrupt ? Loc.Pick("   - BROKE.", "   - PARAN BİTTİ.") : string.Empty);
            }
            UpdateHud();
            duelBetNotBefore = Time.time + 1.1f;
        }

        // ------------------------------------------------------------------ the bet

        /// <summary>Opens the bet picker when the table waits for one and nothing else is on
        /// screen. Called every frame of a round in progress; true when it opened it.</summary>
        private bool TryOfferDuelBet(RoundEngine round)
        {
            BlackjackBoss duel = DuelOf(round);
            if (duel == null || !duel.AwaitingBet || choicePicker.IsOpen || DuelHousePlaying
                || Time.time < duelBetNotBefore)
            {
                return false;
            }
            List<long> stakes = BlackjackBoss.BetOptions(session.TotalScore);
            if (stakes.Count == 0)
            {
                return false;
            }
            pendingChoice = ChoiceKind.DuelBet;
            pendingChoiceValues.Clear();
            var labels = new List<string>();
            for (int i = 0; i < stakes.Count; i++)
            {
                pendingChoiceValues.Add((int)System.Math.Min(stakes[i], int.MaxValue));
                labels.Add(stakes[i] == session.TotalScore
                    ? stakes[i] + Loc.Pick("   (ALL IN)", "   (HEPSİ)")
                    : stakes[i].ToString());
            }
            choicePicker.Show(Loc.Pick("BET  -  purse ", "BAHİS  -  kasan ") + session.TotalScore
                + Loc.Pick("  /  target ", "  /  hedef ") + duel.Target, labels);
            return true;
        }

        /// <summary>A stake picked: it is placed, and the hand is dealt like a round's start.</summary>
        private void ResolveDuelBet(int value)
        {
            if (!session.PlaceDuelBet(value))
            {
                return;
            }
            duelTurnsSeen = null;
            RefreshAll(null);
            sfx.Shuffle();
            cardLayer.AnimateRoundStart(session.CurrentRound);
            if (messageText != null)
            {
                messageText.text = Loc.Pick("Bet ", "Bahis ") + value
                    + Loc.Pick(" - outscore the house with your half of the deck.",
                        " - destenin yarısıyla kasadan fazla puan topla.");
            }
        }

        /// <summary>The score line during a duel: the stake, the target, and the hand so far.</summary>
        private string DuelScoreLine(RoundEngine round)
        {
            BlackjackBoss duel = DuelOf(round);
            if (duel == null)
            {
                return null;
            }
            string line = Loc.Pick("target ", "hedef ") + duel.Target;
            if (duel.Phase == BlackjackBoss.DuelPhase.Playing)
            {
                line += Loc.Pick("    bet ", "    bahis ") + duel.Bet
                    + Loc.Pick("    hand: you ", "    el: sen ") + duel.PlayerHandScore(round)
                    + " - " + ShownHouseScore(duel) + Loc.Pick(" house", " kasa");
            }
            else if (duel.AwaitingBet)
            {
                line += Loc.Pick("    place your bet", "    bahsini koy");
            }
            return line;
        }
    }
}
