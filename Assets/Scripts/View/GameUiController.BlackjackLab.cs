// PURPOSE: The animation lab's "BLACKJACK / CASINO BOSS" section - the brief's 44 scenes, the
// table's speed, its music moods and the sixteen debug views, all driving the REAL table
// (BlackjackTableView) through the same methods the stage calls, with fabricated ARGUMENTS only:
// a bank, a target, a stake, a lab card and a lab board for the house to play on, a hand result
// written the way Core writes one. Nothing here touches the round's Core state.
//
// The lab OWNS the table while a scene is up (duelLabOwnsTable): the two arenas are lab boards
// stood where the table's layout puts them, and the hand slides under the player's side. RESET,
// any other lab scene (AnimResync) or closing the lab hands the screen back; a real duel stage in
// progress then picks up where it was, without replaying its intro.
//
// The three "layout" previews for screens that are not in front of you are drawn as SCHEMATICS:
// the same solver (BlackjackLayout.Solve) asked about a phone or a 21:9 desktop, its answer drawn
// to scale in a box - the screen, the bars it must clear, both arenas, the anchors.
//
// EXTENSION POINT: a new table beat gets one line here, calling the table's own method.

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private float bjLabSpeed = 1f;
        private bool duelResumeQuiet;
        private GameBoard bjLabHouse;
        private Transform bjSchematic;

        private void AddBlackjackLab()
        {
            AddAnimSub("bosses", "blackjack", "BLACKJACK / CASINO BOSS", "BLACKJACK / KUMARHANE PATRONU");

            // ---- layout
            AddAnim("bj 1: layout - desktop (this screen)", "bj 1: yerleşim - masaüstü (bu ekran)", () =>
            {
                BjLabBegin(false);
                BlackjackFx.Debug.ShowBlackjackLayoutBounds = true;
                BlackjackFx.Debug.ShowArenaGroupCenter = true;
                animLastLabel = "layout: " + (duelTable.L.Stacked ? "stacked" : "side by side") + ", board " + duelTable.L.BoardSize.ToString("0.00");
            });
            AddAnim("bj 2: layout - phone 9:16 (schematic)", "bj 2: yerleşim - telefon 9:16 (şema)",
                () => BjLabSchematic(BlackjackLayout.Frame.Phone(9f / 16f), "phone 9:16"));
            AddAnim("bj 3: layout - wide 21:9 (schematic)", "bj 3: yerleşim - geniş 21:9 (şema)",
                () => BjLabSchematic(BlackjackLayout.Frame.Desktop(21f / 9f), "wide 21:9"));
            AddAnim("bj 4: intro layout", "bj 4: açılış yerleşimi", () =>
            {
                BjLabBegin(false);
                duelTable.Run(duelTable.PlayIntro(800, 1600, false, BlackjackBoss.StarterStake, BjLabAnyPress));
            });
            AddAnim("bj 5: HUD safe zones (every anchor)", "bj 5: HUD güvenli alanları (tüm çapalar)", () =>
            {
                BjLabBegin(true);
                BjLabHand(1, 200, 120, 90);
                BlackjackFx.Debug.ShowBlackjackLayoutBounds = true;
                BlackjackFx.Debug.ShowPlayerScoreAnchor = BlackjackFx.Debug.ShowDealerScoreAnchor = true;
                BlackjackFx.Debug.ShowBankAnchor = BlackjackFx.Debug.ShowWagerAnchor = BlackjackFx.Debug.ShowTargetAnchor = true;
                BlackjackFx.Debug.ShowDealerHandSlots = true;
            });

            // ---- the bet
            AddAnim("bj 6: bet screen open", "bj 6: bahis ekranı açık", () => BjLabTray(null, -1));
            AddAnim("bj 7: manual input (typing 350)", "bj 7: elle giriş (350 yazılır)", () => BjLabTray("350", -1));
            AddAnim("bj 8: 10% chip", "bj 8: %10 fişi", () => BjLabTray(null, 0));
            AddAnim("bj 9: 25% chip", "bj 9: %25 fişi", () => BjLabTray(null, 1));
            AddAnim("bj 10: 50% chip", "bj 10: %50 fişi", () => BjLabTray(null, 2));
            AddAnim("bj 11: 75% chip", "bj 11: %75 fişi", () => BjLabTray(null, 3));
            AddAnim("bj 12: ALL-IN", "bj 12: HEPSİ (all-in)", () => BjLabTray(null, 4));
            AddAnim("bj 13: bet confirm", "bj 13: bahis onayı", () =>
            {
                BjLabTray(null, -1);
                duelTable.Run(BjLabConfirmRoutine("200", false));
            });
            AddAnim("bj 14: bank -> wager transfer", "bj 14: kasadan bahse aktarım", () =>
            {
                BjLabBegin(false);
                duelTable.Run(duelTable.PlayBetPlaced(800, 600, 200, false));
            });
            AddAnim("bj 15: invalid bet", "bj 15: geçersiz bahis", () =>
            {
                BjLabTray(null, -1);
                duelTable.Run(BjLabInvalidRoutine());
            });

            // ---- the house's card
            AddAnim("bj 16: dealer - 3 closed cards", "bj 16: kasa - 3 kapalı kart", () =>
            {
                BjLabBegin(true);
                BjLabHand(1, 200, 0, 0);
                duelTable.SetDealerCards(0);
                duelTable.Run(duelTable.DealDealerCards(3));
            });
            AddAnim("bj 17: dealer thinking", "bj 17: kasa düşünüyor", () =>
            {
                BjLabBegin(true);
                BjLabHand(1, 200, 0, 0);
                duelTable.SetDealerCards(3);
                duelTable.Run(duelTable.LabThink(BlackjackFx.Tuning.T(1.6f)));
            });
            AddAnim("bj 18: chosen card forward", "bj 18: seçilen kart öne çıkar", () => BjLabDealerPlay(BlackjackTableView.DealerBeat.Choose, 3, 3));
            AddAnim("bj 19: card leaves the hand", "bj 19: kart elden çıkar", () => BjLabDealerPlay(BlackjackTableView.DealerBeat.Lift, 3, 3));
            AddAnim("bj 20: card turns over", "bj 20: kart çevrilir", () => BjLabDealerPlay(BlackjackTableView.DealerBeat.Flip, 3, 3));
            AddAnim("bj 21: card travels to its place", "bj 21: kart yerine gider", () => BjLabDealerPlay(BlackjackTableView.DealerBeat.Travel, 3, 3));
            AddAnim("bj 22: card lands (the block appears)", "bj 22: kart iner (blok belirir)", () => BjLabDealerPlay(BlackjackTableView.DealerBeat.Land, 3, 3));
            AddAnim("bj 23: the hand closes its gap", "bj 23: el boşluğu kapatır", () => BjLabDealerPlay(BlackjackTableView.DealerBeat.CloseGap, 3, 3));
            AddAnim("bj 24: refill slides in face-down", "bj 24: yeni kart kapalı gelir", () => BjLabDealerPlay(BlackjackTableView.DealerBeat.Refill, 3, 3));
            AddAnim("bj 25: dealer full move", "bj 25: kasanın tam hamlesi", () => BjLabDealerPlay(BlackjackTableView.DealerBeat.All, 3, 3));

            // ---- the score duel
            AddAnim("bj 26: player score gain", "bj 26: oyuncu puan kazanır", () =>
            {
                BjLabBegin(true);
                BjLabHand(1, 200, 60, 90);
                duelTable.SendScore(BlackjackTableView.Side.Player, duelTable.L.Player, 180);
            });
            AddAnim("bj 27: dealer score gain", "bj 27: kasa puan kazanır", () =>
            {
                BjLabBegin(true);
                BjLabHand(1, 200, 180, 90);
                duelTable.SendScore(BlackjackTableView.Side.Dealer, duelTable.L.Dealer, 140);
            });
            AddAnim("bj 28: lead change", "bj 28: liderlik değişir", () =>
            {
                BjLabBegin(true);
                BjLabHand(1, 200, 100, 140);
                duelTable.SendScore(BlackjackTableView.Side.Player, duelTable.L.Player, 190);
            });
            AddAnim("bj 29: close score", "bj 29: başa baş skor", () =>
            {
                BjLabBegin(true);
                BjLabHand(1, 200, 280, 290);
                duelTable.SendScore(BlackjackTableView.Side.Player, duelTable.L.Player, 300);
            });
            AddAnim("bj 30: hand end comparison", "bj 30: el sonu karşılaştırması",
                () => BjLabResult(DuelHandOutcome.Win, 320, 300, 200, 600, false, false));

            // ---- results
            AddAnim("bj 31: hand win", "bj 31: el kazanıldı", () => BjLabResult(DuelHandOutcome.Win, 340, 210, 200, 600, false, false));
            AddAnim("bj 32: hand loss", "bj 32: el kaybedildi", () => BjLabResult(DuelHandOutcome.Lose, 180, 260, 200, 600, false, false));
            AddAnim("bj 33: tie", "bj 33: berabere", () => BjLabResult(DuelHandOutcome.Push, 240, 240, 200, 600, false, false));
            AddAnim("bj 34: payout to the bank", "bj 34: ödeme kasaya", () =>
            {
                BjLabResult(DuelHandOutcome.Win, 400, 120, 400, 400, false, false);
            });
            AddAnim("bj 35: wager to the house", "bj 35: bahis kasaya gider", () =>
            {
                BjLabBegin(true);
                BjLabHand(1, 300, 90, 160);
                duelTable.Run(duelTable.SlideWagerToHouse(BlackjackFx.Tuning.T(BlackjackFx.Tuning.wagerLossDuration)));
                sfx.Casino(BlackjackCue.LossClack);
            });
            AddAnim("bj 36: next hand reset", "bj 36: yeni ele geçiş", () =>
            {
                BjLabBegin(true, true);
                BjLabHand(2, 200, 230, 180);
                duelTable.SetDealerCards(2);
                duelTable.Run(duelTable.PlayNextHand(delegate { BjLabClearBoards(); }));
            });
            AddAnim("bj 37: boss win", "bj 37: patron yenildi", () => BjLabResult(DuelHandOutcome.Win, 380, 240, 400, 1200, true, false));
            AddAnim("bj 38: bankruptcy", "bj 38: iflas", () => BjLabResult(DuelHandOutcome.Lose, 150, 260, 400, 0, false, true));

            // ---- special cases
            AddAnim("bj 39: house stake 500 (empty bank)", "bj 39: kasa avansı 500 (boş kasa)", () =>
            {
                BjLabBegin(false);
                duelTable.SetBank(0, false);
                duelTable.SetTarget(1000, 500);
                duelTable.Run(BjLabStakeRoutine());
            });
            AddAnim("bj 40: all-in hand", "bj 40: hepsi ortada eli", () =>
            {
                BjLabBegin(false);
                sfx.SetCasinoMood(CasinoMusicMood.AllIn);
                duelTable.SetBetTension(1f, true);
                duelTable.Run(BjLabAllInRoutine());
            });
            AddAnim("bj 41: player has no moves", "bj 41: oyuncunun hamlesi yok", () =>
            {
                BjLabBegin(true, true);
                BjLabHand(1, 200, 150, 90);
                duelTable.SetSideDone(BlackjackTableView.Side.Player, true);
                duelTable.SetActiveSide(BlackjackTableView.Side.Dealer);
            });
            AddAnim("bj 42: dealer has no moves", "bj 42: kasanın hamlesi yok", () =>
            {
                BjLabBegin(true, true);
                BjLabHand(1, 200, 150, 90);
                duelTable.SetDealerCards(1);
                duelTable.SetSideDone(BlackjackTableView.Side.Dealer, true);
                duelTable.SetActiveSide(BlackjackTableView.Side.Player);
            });
            AddAnim("bj 43: disabled power clicked", "bj 43: kapalı güce tıklama", () =>
            {
                BjLabBegin(true);
                BjLabHand(1, 200, 60, 40);
                duelTable.SetMessage(Loc.Pick("Not usable at the blackjack table.", "Blackjack masasında kullanılamaz."));
                sfx.Casino(BlackjackCue.PowerBlocked);
            });
            AddAnim("bj 44: casino ambience only", "bj 44: yalnız kumarhane atmosferi", () =>
            {
                BjLabBegin(false);
                duelTable.SetHudVisible(false);
                sfx.SetCasinoMood(CasinoMusicMood.Betting);
            });

            // ---- extras: speed, music, reset
            AddAnim("bj speed: 1x", "bj hız: 1x", () => BjLabSpeed(1f));
            AddAnim("bj speed: 0.5x", "bj hız: 0.5x", () => BjLabSpeed(0.5f));
            AddAnim("bj speed: 0.25x", "bj hız: 0.25x", () => BjLabSpeed(0.25f));
            AddAnim("bj music: betting", "bj müzik: bahis", () => BjLabMusic(CasinoMusicMood.Betting));
            AddAnim("bj music: hand", "bj müzik: el", () => BjLabMusic(CasinoMusicMood.Hand));
            AddAnim("bj music: all-in", "bj müzik: hepsi ortada", () => BjLabMusic(CasinoMusicMood.AllIn));
            AddAnim("bj music: stage won", "bj müzik: patron yenildi", () => BjLabMusic(CasinoMusicMood.Win));
            AddAnim("bj music: bankrupt (strips away)", "bj müzik: iflas (sökülür)", () => BjLabMusic(CasinoMusicMood.Bankrupt));
            AddAnim("bj music: off", "bj müzik: kapalı", () => BjLabMusic(CasinoMusicMood.Off));
            AddAnim("bj: reset the table", "bj: masayı sıfırla", () =>
            {
                StopBlackjackLab();
                animLastLabel = Loc.Pick("table handed back", "masa geri verildi");
            });

            // ---- the brief's sixteen debug views
            BjLabToggle("ShowBlackjackLayoutBounds", () => BlackjackFx.Debug.ShowBlackjackLayoutBounds, v => BlackjackFx.Debug.ShowBlackjackLayoutBounds = v);
            BjLabToggle("ShowArenaGroupCenter", () => BlackjackFx.Debug.ShowArenaGroupCenter, v => BlackjackFx.Debug.ShowArenaGroupCenter = v);
            BjLabToggle("ShowPlayerBoardCenter", () => BlackjackFx.Debug.ShowPlayerBoardCenter, v => BlackjackFx.Debug.ShowPlayerBoardCenter = v);
            BjLabToggle("ShowDealerBoardCenter", () => BlackjackFx.Debug.ShowDealerBoardCenter, v => BlackjackFx.Debug.ShowDealerBoardCenter = v);
            BjLabToggle("ShowDealerHandSlots", () => BlackjackFx.Debug.ShowDealerHandSlots, v => BlackjackFx.Debug.ShowDealerHandSlots = v);
            BjLabToggle("ShowDealerChosenCard", () => BlackjackFx.Debug.ShowDealerChosenCard, v => BlackjackFx.Debug.ShowDealerChosenCard = v);
            BjLabToggle("ShowDealerCardTravelPath", () => BlackjackFx.Debug.ShowDealerCardTravelPath, v => BlackjackFx.Debug.ShowDealerCardTravelPath = v);
            BjLabToggle("ShowPlayerScoreAnchor", () => BlackjackFx.Debug.ShowPlayerScoreAnchor, v => BlackjackFx.Debug.ShowPlayerScoreAnchor = v);
            BjLabToggle("ShowDealerScoreAnchor", () => BlackjackFx.Debug.ShowDealerScoreAnchor, v => BlackjackFx.Debug.ShowDealerScoreAnchor = v);
            BjLabToggle("ShowBankAnchor", () => BlackjackFx.Debug.ShowBankAnchor, v => BlackjackFx.Debug.ShowBankAnchor = v);
            BjLabToggle("ShowWagerAnchor", () => BlackjackFx.Debug.ShowWagerAnchor, v => BlackjackFx.Debug.ShowWagerAnchor = v);
            BjLabToggle("ShowTargetAnchor", () => BlackjackFx.Debug.ShowTargetAnchor, v => BlackjackFx.Debug.ShowTargetAnchor = v);
            BjLabToggle("ShowBetInputState", () => BlackjackFx.Debug.ShowBetInputState, v => BlackjackFx.Debug.ShowBetInputState = v);
            BjLabToggle("ShowHandResult", () => BlackjackFx.Debug.ShowHandResult, v => BlackjackFx.Debug.ShowHandResult = v);
            BjLabToggle("ShowAudioDuck", () => BlackjackFx.Debug.ShowAudioDuck, v => BlackjackFx.Debug.ShowAudioDuck = v);
            BjLabToggle("ShowBackgroundMood", () => BlackjackFx.Debug.ShowBackgroundMood, v => BlackjackFx.Debug.ShowBackgroundMood = v);
        }

        private void BjLabToggle(string name, Func<bool> get, Action<bool> set)
        {
            AddAnim("bj debug: " + name, "bj debug: " + name, () =>
            {
                set(!get());
                animLastLabel = name + " " + (get() ? "ON" : "off");
            });
        }

        // ================================================================== the lab's table

        /// <summary>Lays the table for a scene: the lab owns it, the two arenas are lab boards where
        /// the layout puts them, bank 800 / target 1600, nothing on the plate.</summary>
        private void BjLabBegin(bool withBoards, bool filled = false)
        {
            AnimResync();
            duelLabOwnsTable = true;
            EnsureDuelTable();
            duelShown = true;
            duelTable.gameObject.SetActive(true);
            duelTable.ResetTable();
            BlackjackFx.Tuning.Speed = bjLabSpeed;
            duelTable.LabStopAfter = BlackjackTableView.DealerBeat.All;
            duelTable.Place(DuelLayoutNow);
            duelTable.SetTarget(1600, 800);
            duelTable.SetBank(800, false);
            duelTable.DebugExtra = "LAB";
            if (totalText != null) totalText.enabled = false;
            if (messageText != null) messageText.enabled = false;
            BjLabSchematicClear();
            BlackjackLayout l = duelTable.L;
            int w = session.CurrentRound != null ? session.CurrentRound.Config.BoardWidth : 7;
            int h = session.CurrentRound != null ? session.CurrentRound.Config.BoardHeight : 7;
            GameBoard player = new GameBoard(w, h);
            bjLabHouse = new GameBoard(w, h);
            if (filled)
            {
                BjLabScatter(player, 3);
                BjLabScatter(bjLabHouse, 11);
            }
            boardView.Rebuild(player, l.BoardSize, l.Player);
            boardView.Refresh();
            if (houseBoardView == null)
            {
                var go = new GameObject("HouseBoardView");
                go.transform.SetParent(transform, false);
                houseBoardView = go.AddComponent<BoardView>();
            }
            houseBoardView.gameObject.SetActive(true);
            houseBuiltSize = -1f;
            ShowHouseBoard(bjLabHouse);
            if (session.CurrentRound != null)
            {
                cardLayer.Sync(session.CurrentRound, null);
            }
            sfx.SetCasinoMood(withBoards ? CasinoMusicMood.Hand : CasinoMusicMood.Betting);
        }

        /// <summary>A hand in progress on the table: its line, the stake on the plate, both scores.</summary>
        private void BjLabHand(int hand, long bet, int player, int dealer)
        {
            duelTable.SetBank(800 - bet, false);
            duelTable.SetHandLine(hand, bet);
            duelTable.SetWager(bet, true);
            duelTable.SetBetTension(bet / 800f, false);
            duelTable.SetScoresVisible(true);
            duelTable.SetScores(player, dealer, false);
            duelTable.SetDealerCards(3);
            duelTable.SetActiveSide(BlackjackTableView.Side.Player);
        }

        private void BjLabScatter(GameBoard board, int salt)
        {
            List<int> cards = AnimBossCards();
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (BlackjackFx.Hash01(x * 13 + salt, y * 7 + salt) < 0.42f)
                    {
                        int card = cards[(x + y * 3 + salt) % cards.Count];
                        board.SetCubeAt(new GridPos(board.MinX + x, board.MinY + y), new Cube(CubeKind.Normal, card));
                    }
                }
            }
        }

        private void BjLabClearBoards()
        {
            if (session.CurrentRound == null)
            {
                return;
            }
            int w = session.CurrentRound.Config.BoardWidth;
            int h = session.CurrentRound.Config.BoardHeight;
            boardView.Rebuild(new GameBoard(w, h), duelTable.L.BoardSize, duelTable.L.Player);
            boardView.Refresh();
            bjLabHouse = new GameBoard(w, h);
            ShowHouseBoard(bjLabHouse);
        }

        private bool BjLabAnyPress()
        {
            return AnyPress(UnityEngine.InputSystem.Keyboard.current, UnityEngine.InputSystem.Mouse.current);
        }

        private void BjLabSpeed(float speed)
        {
            bjLabSpeed = speed;
            BlackjackFx.Tuning.Speed = speed;
            animLastLabel = "blackjack speed " + speed + "x";
        }

        private void BjLabMusic(CasinoMusicMood mood)
        {
            if (!duelLabOwnsTable)
            {
                BjLabBegin(false);
            }
            sfx.SetCasinoMood(mood);
            animLastLabel = "music " + mood;
        }

        /// <summary>The bet tray over the lab table, optionally typing or pressing a chip.</summary>
        private void BjLabTray(string type, int chip)
        {
            BjLabBegin(false);
            duelTable.ShowBetTray(800, 1600, BlackjackBoss.BetOptions(800));
            BlackjackFx.Debug.ShowBetInputState = true;
            if (!string.IsNullOrEmpty(type))
            {
                duelTable.Run(duelTable.LabType(type, BlackjackFx.Tuning.T(0.22f)));
            }
            if (chip >= 0)
            {
                duelTable.PressBetItem(Mathf.Min(chip, duelTable.BetItemCount - 2));
            }
        }

        private IEnumerator BjLabConfirmRoutine(string amount, bool allIn)
        {
            yield return duelTable.LabType(amount, BlackjackFx.Tuning.T(0.18f));
            yield return new WaitForSeconds(BlackjackFx.Tuning.T(0.35f));
            long v = duelTable.LabConfirm();
            if (v > 0)
            {
                yield return duelTable.PlayBetPlaced(800, 800 - v, v, allIn);
                duelTable.SetHandLine(1, v);
            }
        }

        private IEnumerator BjLabInvalidRoutine()
        {
            yield return new WaitForSeconds(BlackjackFx.Tuning.T(0.4f));
            duelTable.LabConfirm(); // empty: refused
            yield return new WaitForSeconds(BlackjackFx.Tuning.T(0.9f));
            yield return duelTable.LabType("9999", BlackjackFx.Tuning.T(0.15f)); // past the bank: clamped
        }

        private IEnumerator BjLabStakeRoutine()
        {
            yield return duelTable.PlayIntro(500, 1000, true, BlackjackBoss.StarterStake, BjLabAnyPress);
            duelTable.SetBank(500, true, BlackjackFx.Tuning.T(0.45f));
        }

        private IEnumerator BjLabAllInRoutine()
        {
            duelTable.ShowBetTray(800, 1600, BlackjackBoss.BetOptions(800));
            yield return new WaitForSeconds(BlackjackFx.Tuning.T(0.5f));
            duelTable.PressBetItem(duelTable.BetItemCount - 2);
            yield return new WaitForSeconds(BlackjackFx.Tuning.T(0.7f));
            yield return duelTable.PlayBetPlaced(800, 0, 800, true);
            duelTable.SetHandLine(1, 800);
            duelTable.SetScoresVisible(true);
            yield return duelTable.PlayDealSplit(3);
            duelTable.SetActiveSide(BlackjackTableView.Side.Player);
        }

        /// <summary>A house play on the lab board, from a real card of the run's deck, stopped after
        /// one beat (or played whole).</summary>
        private void BjLabDealerPlay(BlackjackTableView.DealerBeat stop, int before, int after)
        {
            BjLabBegin(true);
            BjLabHand(1, 200, 90, 60);
            BlockCard card = null;
            foreach (BlockCard c in session.OwnedCards)
            {
                if (card == null || c.Elements.Count > card.Elements.Count)
                {
                    card = c;
                }
            }
            if (card == null)
            {
                return;
            }
            BlockShape shape = card.Shape;
            GridPos origin = new GridPos(bjLabHouse.MinX + 2, bjLabHouse.MinY + 2);
            for (int y = 2; y < bjLabHouse.Height && !bjLabHouse.CanPlace(shape, origin); y++)
            {
                origin = new GridPos(bjLabHouse.MinX + 2, bjLabHouse.MinY + y);
            }
            GameBoard landed = GameBoard.CreateClone(bjLabHouse);
            IReadOnlyList<GridPos> placed = landed.Place(card, shape, origin, false);
            var cells = new List<Vector2>();
            Vector2 sum = Vector2.zero;
            foreach (GridPos p in placed)
            {
                Vector2 local = duelTable.transform.InverseTransformPoint(houseBoardView.CellToWorld(p));
                cells.Add(local);
                sum += local;
            }
            Vector2 landAt = cells.Count > 0 ? sum / cells.Count : duelTable.L.Dealer;
            duelTable.SetDealerCards(before);
            duelTable.SetActiveSide(BlackjackTableView.Side.Dealer);
            duelTable.LabStopAfter = stop;
            BlackjackFx.Debug.ShowDealerChosenCard = stop == BlackjackTableView.DealerBeat.Choose;
            int count = placed.Count;
            duelTable.Run(duelTable.PlayDealerCard(1, before, after, card, shape, landAt, cells,
                houseBoardView.CellWorldSize, 0.4f, delegate
                {
                    bjLabHouse = landed;
                    ShowHouseBoard(bjLabHouse);
                    sfx.Place(count);
                    duelTable.SendScore(BlackjackTableView.Side.Dealer, landAt, 60 + count * 10);
                }));
            animLastLabel = "house plays " + card.Shape.Cells.Count + " cubes, stops after " + stop;
        }

        /// <summary>A hand's end on the lab table, written the way Core writes one.</summary>
        private void BjLabResult(DuelHandOutcome outcome, int player, int dealer, long bet, long purseAfter, bool stageWon, bool bankrupt)
        {
            BjLabBegin(true, true);
            long bankDuring = outcome == DuelHandOutcome.Win ? purseAfter - bet * 2
                : outcome == DuelHandOutcome.Push ? purseAfter - bet : purseAfter;
            BjLabHand(1, bet, player - 40, dealer - 30);
            duelTable.SetBank(bankDuring, false);
            duelTable.SetTarget(stageWon ? purseAfter : 1600, 800);
            var r = new DuelHandResult
            {
                HandNumber = 1,
                Bet = bet,
                PlayerScore = player,
                AiScore = dealer,
                Outcome = outcome,
                Payout = outcome == DuelHandOutcome.Win ? bet * 2 : outcome == DuelHandOutcome.Push ? bet : 0,
                PurseAfter = purseAfter,
                Target = 1600,
                StageWon = stageWon,
                Bankrupt = bankrupt
            };
            BlackjackFx.Debug.ShowHandResult = true;
            duelTable.DebugExtra = "HAND " + r.Outcome + "  you " + player + " - " + dealer + "  bet " + bet
                + "  payout " + r.Payout + "  bank " + bankDuring + " -> " + purseAfter;
            duelTable.Run(BjLabResultRoutine(r, bankDuring));
        }

        private IEnumerator BjLabResultRoutine(DuelHandResult r, long bankDuring)
        {
            yield return duelTable.PlayHandResult(r, bankDuring);
            if (r.StageWon)
            {
                yield return duelTable.PlayBossWin(r.PurseAfter);
            }
            else if (r.Bankrupt)
            {
                yield return duelTable.PlayBankrupt();
            }
        }

        /// <summary>Hands the screen back. A real duel stage in progress picks up without its intro.</summary>
        private void StopBlackjackLab()
        {
            BjLabSchematicClear();
            if (!duelLabOwnsTable)
            {
                return;
            }
            duelLabOwnsTable = false;
            BlackjackFx.Tuning.Speed = 1f;
            if (duelTable != null)
            {
                duelTable.LabStopAfter = BlackjackTableView.DealerBeat.All;
                duelTable.DebugExtra = null;
                duelTable.SetHudVisible(true);
            }
            TearDownDuelTable();
            // only a duel stage already on screen carries on quietly; a later stage keeps its intro
            duelResumeQuiet = session != null && DuelOf(session.CurrentRound) != null;
            if (session != null && session.CurrentRound != null)
            {
                cardLayer.Sync(session.CurrentRound, null);
            }
        }

        // ================================================================== schematics

        /// <summary>The solver's answer for a screen that is not this one, drawn to scale.</summary>
        private void BjLabSchematic(BlackjackLayout.Frame f, string name)
        {
            BjLabBegin(false);
            duelTable.SetHudVisible(false);
            BlackjackLayout l = BlackjackLayout.Solve(f);
            bjSchematic = new GameObject("BlackjackSchematic").transform;
            bjSchematic.SetParent(transform, false);
            float scale = Mathf.Min(6.6f / (f.HalfHeight * 2f), 9f / (f.HalfWidth * 2f));
            Vector2 c = new Vector2(1.2f, 0.2f);
            Func<Vector2, Vector2> at = p => c + p * scale;
            BjBox(at(Vector2.zero), new Vector2(f.HalfWidth, f.HalfHeight) * 2f * scale, new Color(0.9f, 0.9f, 0.9f, 0.9f), true);
            ViewUtil.MakeRect(bjSchematic, "Bg", at(Vector2.zero), new Vector2(f.HalfWidth, f.HalfHeight) * 2f * scale,
                new Color(0.03f, 0.08f, 0.075f, 0.95f), 88);
            if (f.LeftReserve > 0f)
            {
                ViewUtil.MakeRect(bjSchematic, "LeftBars", at(new Vector2(-f.HalfWidth + f.LeftReserve * 0.5f, 0f)),
                    new Vector2(f.LeftReserve, f.HalfHeight * 2f) * scale, new Color(0.4f, 0.3f, 0.6f, 0.35f), 89);
            }
            if (f.RightReserve > 0f)
            {
                ViewUtil.MakeRect(bjSchematic, "RightBars", at(new Vector2(f.HalfWidth - f.RightReserve * 0.5f, 0f)),
                    new Vector2(f.RightReserve, f.HalfHeight * 2f) * scale, new Color(0.4f, 0.3f, 0.6f, 0.35f), 89);
            }
            if (f.TopReserve > 0f)
            {
                ViewUtil.MakeRect(bjSchematic, "TopBars", at(new Vector2(0f, f.HalfHeight - f.TopReserve * 0.5f)),
                    new Vector2(f.HalfWidth * 2f, f.TopReserve) * scale, new Color(0.4f, 0.3f, 0.6f, 0.35f), 89);
            }
            ViewUtil.MakeRect(bjSchematic, "Hand", at(new Vector2(l.PlayerHand.x, (f.HandTop - f.HalfHeight) * 0.5f)),
                new Vector2(4f, f.HandTop + f.HalfHeight) * scale, new Color(0.7f, 0.5f, 0.3f, 0.3f), 89);
            BjBox(at(l.Player), Vector2.one * l.BoardSize * scale, BlackjackShapes.PlayerInk, false);
            BjBox(at(l.Dealer), Vector2.one * l.BoardSize * scale, BlackjackShapes.DealerInk, false);
            Vector2[] dots = { l.Bank, l.Ledger, l.PlayerScore, l.DealerScore, l.Vs, l.Wager, l.DealerHand, l.PlayerLabel, l.DealerLabel };
            for (int i = 0; i < dots.Length; i++)
            {
                ViewUtil.MakeRect(bjSchematic, "Anchor" + i, at(dots[i]), Vector2.one * 0.08f, BlackjackShapes.Gold, 92);
            }
            ViewUtil.MakeRect(bjSchematic, "Centre", at(new Vector2(0f, l.GroupCenter.y)), new Vector2(0.02f, f.HalfHeight * 2f * scale),
                new Color(1f, 0.3f, 0.9f, 0.5f), 91);
            var label = ViewUtil.MakeText3D(bjSchematic, "Label", c + new Vector2(0f, f.HalfHeight * scale + 0.25f),
                name + "  -  " + (l.Stacked ? "stacked" : "side by side") + ", board " + l.BoardSize.ToString("0.00") + " u", 90,
                0.2f / 9f, BlackjackShapes.Cream, 93, TextAnchor.MiddleCenter);
            animLastLabel = name + ": board " + l.BoardSize.ToString("0.00") + (l.Stacked ? " stacked" : " side by side");
        }

        private void BjBox(Vector2 c, Vector2 size, Color col, bool thick)
        {
            float w = thick ? 0.04f : 0.03f;
            ViewUtil.MakeRect(bjSchematic, "Edge", c + new Vector2(0f, size.y * 0.5f), new Vector2(size.x, w), col, 91);
            ViewUtil.MakeRect(bjSchematic, "Edge", c - new Vector2(0f, size.y * 0.5f), new Vector2(size.x, w), col, 91);
            ViewUtil.MakeRect(bjSchematic, "Edge", c + new Vector2(size.x * 0.5f, 0f), new Vector2(w, size.y), col, 91);
            ViewUtil.MakeRect(bjSchematic, "Edge", c - new Vector2(size.x * 0.5f, 0f), new Vector2(w, size.y), col, 91);
        }

        private void BjLabSchematicClear()
        {
            if (bjSchematic != null)
            {
                Destroy(bjSchematic.gameObject);
                bjSchematic = null;
            }
        }
    }
}
