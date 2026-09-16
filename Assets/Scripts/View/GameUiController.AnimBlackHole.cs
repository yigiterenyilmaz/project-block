// PURPOSE: "Kara Delik" in the ANIMATION LAB (F3). Every scene puts up a board of the lab's OWN and runs
// the joker's REAL gravity on it (KaraDelikJoker.RunGravity / RunCollapse, with the board's own forced
// destroy), the real placement (GameBoard.Place, which drops a cube into a hole) and the real moving
// board (ShiftRowsUp, which the hole refuses) - so the bites, the pulls, the collapse and the refusals
// the view draws are the rules' own answers. Only the devour is fabricated (the round's piles are the
// round's): its report names the real piles' cards, and the voided residue it leaves is given back by
// RESET. Scenes hold what they end on until RESET or closing the lab.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private enum AnimHoleScene
        {
            Idle,
            PlaceEmpty,
            PlaceOverCube,
            BlockOntoHole,
            PullOnly,
            SwallowOne,
            SwallowThree,
            SwallowMany,
            FullTurn,
            NearlyFull,
            Collapse,
            MultiHole,
            Stress,
            Blocked,
            ForcedRefusal,
        }

        private Coroutine animHoleRoutine;

        private void StopAnimBlackHole()
        {
            if (animHoleRoutine != null)
            {
                StopCoroutine(animHoleRoutine);
                animHoleRoutine = null;
            }
        }

        private void BuildAnimBlackHoleCatalogue()
        {
            AddAnimHeader("kara delik / tekillik", "kara delik / tekillik");
            AddAnim("kara delik: idle (horizon, disk, lensing, motes)", "kara delik: bekleme (ufuk, disk, bükülme, zerreler)",
                delegate { AnimHole(AnimHoleScene.Idle, "idle", "bekleme"); });
            AddAnim("kara delik: laid on an EMPTY cell", "kara delik: BOŞ hücreye konuldu",
                delegate { AnimHole(AnimHoleScene.PlaceEmpty, "laid on an empty cell", "boş hücreye konuldu"); });
            AddAnim("kara delik: laid OVER a cube (it is swallowed)", "kara delik: küpün ÜSTÜNE konuldu (yutulur)",
                delegate { AnimHole(AnimHoleScene.PlaceOverCube, "laid over a cube", "küpün üstüne konuldu"); });
            AddAnim("kara delik: a block laid ON the hole falls in", "kara delik: deliğin üstüne konan blok içine düşer",
                delegate { AnimHole(AnimHoleScene.BlockOntoHole, "block laid on the hole", "delik üstüne blok"); });
            AddAnim("kara delik: pull only (ring 2 -> ring 1)", "kara delik: yalnızca çekme (halka 2 -> halka 1)",
                delegate { AnimHole(AnimHoleScene.PullOnly, "pull only", "yalnızca çekme"); });
            AddAnim("kara delik: swallow ONE", "kara delik: TEK yutma",
                delegate { AnimHole(AnimHoleScene.SwallowOne, "swallow one", "tek yutma"); });
            AddAnim("kara delik: swallow three (per-block score)", "kara delik: üç yutma (blok başı puan)",
                delegate { AnimHole(AnimHoleScene.SwallowThree, "swallow three", "üç yutma"); });
            AddAnim("kara delik: swallow the whole ring (rolling subtotal)", "kara delik: bütün halkayı yut (biriken toplam)",
                delegate { AnimHole(AnimHoleScene.SwallowMany, "whole ring", "bütün halka"); });
            AddAnim("kara delik: a whole turn (eat + pull + ring 3 untouched)", "kara delik: tam tur (yut + çek + halka 3 dokunulmaz)",
                delegate { AnimHole(AnimHoleScene.FullTurn, "whole turn", "tam tur"); });
            AddAnim("kara delik: mass nearly full (90%+)", "kara delik: kütle neredeyse dolu (%90+)",
                delegate { AnimHole(AnimHoleScene.NearlyFull, "nearly full", "neredeyse dolu"); });
            AddAnim("kara delik: CRITICAL MASS -> collapse into a sweep", "kara delik: KRİTİK KÜTLE -> çöküş ve temizlik",
                delegate { AnimHole(AnimHoleScene.Collapse, "critical mass collapse", "kritik kütle çöküşü"); });
            AddAnim("kara delik: two holes on one board", "kara delik: bir tahtada iki delik",
                delegate { AnimHole(AnimHoleScene.MultiHole, "two holes", "iki delik"); });
            AddAnim("kara delik: stress (three holes, dense board)", "kara delik: stres (üç delik, dolu tahta)",
                delegate { AnimHole(AnimHoleScene.Stress, "stress", "stres"); });
            AddAnim("kara delik: the escalator cannot carry it", "kara delik: yürüyen merdiven onu taşıyamaz",
                delegate { AnimHole(AnimHoleScene.Blocked, "escalator refused", "merdiven reddedildi"); });
            AddAnim("kara delik: a forced removal is refused", "kara delik: zorla kaldırma reddedilir",
                delegate { AnimHole(AnimHoleScene.ForcedRefusal, "forced removal refused", "zorla kaldırma reddedildi"); });
            AddAnim("kara delik: DEVOURS the DRAW pile", "kara delik: ÇEKME destesini YUTAR",
                delegate { AnimHoleDevour(true, false); });
            AddAnim("kara delik: DEVOURS the DISCARD pile", "kara delik: ISKARTAYI YUTAR",
                delegate { AnimHoleDevour(false, false); });
            AddAnim("kara delik: devour with no hole on the board", "kara delik: tahtada delik yokken yutma",
                delegate { AnimHoleDevour(false, true); });
            AddAnim("kara delik: voided pile restored (next round)", "kara delik: yutulan deste geri gelir (yeni raunt)",
                delegate { AnimHoleRestore(); });
            AddAnim("kara delik: swallow at 0.5x", "kara delik: yutma 0.5x",
                delegate { Time.timeScale = 0.5f; AnimHole(AnimHoleScene.SwallowThree, "swallow 0.5x", "yutma 0.5x"); });
            AddAnim("kara delik: swallow at 0.25x", "kara delik: yutma 0.25x",
                delegate { Time.timeScale = 0.25f; AnimHole(AnimHoleScene.SwallowOne, "swallow 0.25x", "yutma 0.25x"); });
            AddAnim("kara delik: collapse at 0.5x", "kara delik: çöküş 0.5x",
                delegate { Time.timeScale = 0.5f; AnimHole(AnimHoleScene.Collapse, "collapse 0.5x", "çöküş 0.5x"); });
            AddAnim("kara delik: devour at 0.25x", "kara delik: yutma (deste) 0.25x",
                delegate { Time.timeScale = 0.25f; AnimHoleDevour(true, false); });
            AddAnim("kara delik switch: horizon", "kara delik anahtarı: olay ufku",
                delegate { AnimHoleToggle(ref BlackHoleView.Layers.ShowHorizon, "horizon", "olay ufku"); });
            AddAnim("kara delik switch: accretion disk", "kara delik anahtarı: toplanma diski",
                delegate { AnimHoleToggle(ref BlackHoleView.Layers.ShowDisk, "accretion disk", "toplanma diski"); });
            AddAnim("kara delik switch: lensing", "kara delik anahtarı: bükülme",
                delegate { AnimHoleToggle(ref BlackHoleView.Layers.ShowLensing, "lensing", "bükülme"); });
            AddAnim("kara delik switch: mass layer", "kara delik anahtarı: kütle katmanı",
                delegate { AnimHoleToggle(ref BlackHoleView.Layers.ShowMass, "mass layer", "kütle katmanı"); });
            AddAnim("kara delik switch: motes", "kara delik anahtarı: zerreler",
                delegate { AnimHoleToggle(ref BlackHoleView.Layers.ShowMotes, "motes", "zerreler"); });
            AddAnim("kara delik switch: spaghettification", "kara delik anahtarı: spagettileşme",
                delegate { AnimHoleToggle(ref BlackHoleView.Layers.ShowSpaghetti, "spaghettification", "spagettileşme"); });
            AddAnim("kara delik switch: horizon occlusion", "kara delik anahtarı: ufuk örtmesi",
                delegate { AnimHoleToggle(ref BlackHoleView.Layers.ShowOcclusion, "horizon occlusion", "ufuk örtmesi"); });
            AddAnim("kara delik switch: score", "kara delik anahtarı: puan",
                delegate { AnimHoleToggle(ref BlackHoleView.Layers.ShowScore, "score", "puan"); });
            AddAnim("kara delik switch: corridor", "kara delik anahtarı: çekim koridoru",
                delegate { AnimHoleToggle(ref BlackHoleView.Layers.ShowCorridor, "corridor", "çekim koridoru"); });
            AddAnim("kara delik debug: influence rings (red 1, yellow 2)", "kara delik hata ayıklama: etki halkaları (kırmızı 1, sarı 2)",
                delegate { AnimHoleToggle(ref BlackHoleView.Layers.ShowInfluence, "influence rings", "etki halkaları"); });
            AddAnim("kara delik debug: paths", "kara delik hata ayıklama: yollar",
                delegate { AnimHoleToggle(ref BlackHoleView.Layers.ShowPaths, "paths", "yollar"); });
            AddAnim("kara delik debug: horizon mask", "kara delik hata ayıklama: ufuk maskesi",
                delegate { AnimHoleToggle(ref BlackHoleView.Layers.ShowHorizonMask, "horizon mask", "ufuk maskesi"); });
            AddAnim("kara delik debug: mass layer", "kara delik hata ayıklama: kütle katmanı",
                delegate { AnimHoleToggle(ref BlackHoleView.Layers.ShowMassDebug, "mass layer", "kütle katmanı"); });
            AddAnim("kara delik switch: ALL back on", "kara delik anahtarı: TÜMÜ geri açık",
                delegate
                {
                    BlackHoleView.Layers.AllOn();
                    animLastLabel = Loc.Pick("kara delik: every layer back on", "kara delik: tüm katmanlar açık");
                    if (AnimLabOpen) { RedrawAnimationLab(); }
                });
        }

        private void AnimHoleToggle(ref bool flag, string english, string turkish)
        {
            flag = !flag;
            animLastLabel = Loc.Pick("kara delik " + english + ": ", "kara delik " + turkish + ": ") + OnOff(flag);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        private static IReadOnlyList<GridPos> AnimHoleDestroy(GameBoard board, List<GridPos> cells)
        {
            var gone = new List<GridPos>();
            foreach (GridPos c in cells)
            {
                if (board.DestroyCubeForced(c))
                {
                    gone.Add(c);
                }
            }
            return gone;
        }

        private void AnimHole(AnimHoleScene scene, string english, string turkish)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || boardView == null || !BlackHoleView.Available)
            {
                animLastLabel = Loc.Pick("kara delik: shaders missing", "kara delik: shader'lar yok");
                return;
            }
            StopAnimBlackHole();
            BlackHoleView view = EnsureBlackHole();
            view.Stop();
            view.Forget();
            List<int> cards = AnimBossCards();
            var board = new GameBoard(7, 7);
            var holesAt = new List<GridPos> { new GridPos(3, 3) };
            if (scene == AnimHoleScene.MultiHole)
            {
                holesAt = new List<GridPos> { new GridPos(1, 1), new GridPos(5, 4) };
            }
            else if (scene == AnimHoleScene.Stress)
            {
                holesAt = new List<GridPos> { new GridPos(1, 5), new GridPos(5, 5), new GridPos(3, 1) };
            }
            bool layLater = scene == AnimHoleScene.PlaceEmpty || scene == AnimHoleScene.PlaceOverCube;
            if (!layLater)
            {
                foreach (GridPos h in holesAt)
                {
                    board.SetCubeAt(h, new Cube(CubeKind.Void, cards[0]));
                }
            }
            var put = new List<KeyValuePair<GridPos, CubeKind>>();
            switch (scene)
            {
                case AnimHoleScene.Idle:
                case AnimHoleScene.Blocked:
                case AnimHoleScene.ForcedRefusal:
                    // ring 1 empty (nothing eats), cubes in ring 2 and 3 so the lensing shows its edge
                    AddRing(put, new GridPos(3, 3), 2, CubeKind.Normal, 3);
                    AddRing(put, new GridPos(3, 3), 3, CubeKind.Normal, 2);
                    put.Add(Pair(new GridPos(3, 5), CubeKind.Water));
                    break;
                case AnimHoleScene.PlaceEmpty:
                    AddRing(put, new GridPos(3, 3), 2, CubeKind.Normal, 2);
                    break;
                case AnimHoleScene.PlaceOverCube:
                    put.Add(Pair(new GridPos(3, 3), CubeKind.Gold));
                    AddRing(put, new GridPos(3, 3), 2, CubeKind.Normal, 3);
                    break;
                case AnimHoleScene.BlockOntoHole:
                    AddRing(put, new GridPos(3, 3), 2, CubeKind.Normal, 3);
                    break;
                case AnimHoleScene.PullOnly:
                    put.Add(Pair(new GridPos(5, 3), CubeKind.Normal));
                    put.Add(Pair(new GridPos(1, 5), CubeKind.Water));
                    put.Add(Pair(new GridPos(3, 1), CubeKind.Fire));
                    break;
                case AnimHoleScene.SwallowOne:
                    put.Add(Pair(new GridPos(4, 3), CubeKind.Normal));
                    break;
                case AnimHoleScene.SwallowThree:
                    put.Add(Pair(new GridPos(4, 3), CubeKind.Normal));
                    put.Add(Pair(new GridPos(2, 4), CubeKind.Water));
                    put.Add(Pair(new GridPos(3, 2), CubeKind.Gold));
                    break;
                case AnimHoleScene.SwallowMany:
                    AddRing(put, new GridPos(3, 3), 1, CubeKind.Normal, 1);
                    put.Add(Pair(new GridPos(2, 2), CubeKind.Obsidian));
                    put.Add(Pair(new GridPos(4, 4), CubeKind.Fire));
                    break;
                case AnimHoleScene.FullTurn:
                case AnimHoleScene.NearlyFull:
                case AnimHoleScene.MultiHole:
                    AddRing(put, new GridPos(3, 3), 1, CubeKind.Normal, 2);
                    AddRing(put, new GridPos(3, 3), 2, CubeKind.Normal, 2);
                    AddRing(put, new GridPos(3, 3), 3, CubeKind.Normal, 3);
                    put.Add(Pair(new GridPos(4, 2), CubeKind.Water));
                    break;
                case AnimHoleScene.Collapse:
                    AddRing(put, new GridPos(3, 3), 1, CubeKind.Normal, 2);
                    AddAll(put, board, 5, CubeKind.Normal);
                    put.Add(Pair(new GridPos(0, 6), CubeKind.Gold));
                    put.Add(Pair(new GridPos(6, 0), CubeKind.Obsidian));
                    break;
                case AnimHoleScene.Stress:
                    AddAll(put, board, 2, CubeKind.Normal);
                    break;
            }
            foreach (KeyValuePair<GridPos, CubeKind> e in put)
            {
                if (board.IsInside(e.Key) && !board.GetCube(e.Key).HasValue)
                {
                    board.SetCubeAt(e.Key, new Cube(e.Value, cards[(e.Key.X * 3 + e.Key.Y) % cards.Count]));
                }
            }
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            boardView.Refresh();

            int goal = KaraDelikJoker.GoalFor(board);
            KaraDelikJoker joker = FindBlackHoleJoker() ?? new KaraDelikJoker();
            int each = joker.PointsPerCube * session.Config.Scoring.ScoreScale;
            int before = scene == AnimHoleScene.NearlyFull ? goal - 6
                : scene == AnimHoleScene.Collapse ? goal - 3 : Mathf.Min(8, goal / 5);
            view.SetMass(before, goal);

            var report = new BlackHoleVisuals { Goal = goal, PointsEach = each, SwallowedBefore = before };
            string extra = "";
            switch (scene)
            {
                case AnimHoleScene.Idle:
                    break;
                case AnimHoleScene.Blocked:
                {
                    // the real escalator: every other cube rides up, the hole stays
                    board.ShiftRowsUp(new List<LiftMotion>(), new List<CellMove>());
                    boardView.Refresh();
                    extra = board.AnchorRefusals.Count + Loc.Pick(" refusal(s)", " ret");
                    break;
                }
                case AnimHoleScene.ForcedRefusal:
                {
                    board.SetForcedStep(new GridPos(1, 0));
                    bool gone = board.DestroyCubeForced(holesAt[0]);
                    boardView.Refresh();
                    extra = Loc.Pick(gone ? "REMOVED (bug)" : "refused", gone ? "KALDIRILDI (hata)" : "reddedildi");
                    break;
                }
                case AnimHoleScene.PlaceEmpty:
                case AnimHoleScene.PlaceOverCube:
                {
                    GridPos cell = holesAt[0];
                    Cube? under = board.GetCube(cell);
                    if (under.HasValue)
                    {
                        board.DestroyCubeForced(cell);
                        report.PlacementSwallows.Add(new DestroyedCube(cell, under.Value));
                    }
                    board.SetCubeAt(cell, new Cube(CubeKind.Void, cards[0]));
                    boardView.Refresh();
                    RunAnimGravity(board, report);
                    break;
                }
                case AnimHoleScene.BlockOntoHole:
                {
                    BlockCard card = session.CreateCard(BlockShape.FromCells(new[] { new GridPos(0, 0), new GridPos(1, 0) }), null);
                    board.Place(card, new GridPos(3, 3));
                    report.PlacementSwallows.AddRange(board.LastPlacementSwallows);
                    RunAnimGravity(board, report);
                    extra = report.PlacementSwallows.Count + Loc.Pick(" fell in", " düştü");
                    break;
                }
                default:
                    RunAnimGravity(board, report);
                    if (scene == AnimHoleScene.Collapse || report.SwallowedBefore + report.SwallowedThisTurn >= goal)
                    {
                        KaraDelikJoker.RunCollapse(board, report, delegate(List<GridPos> c) { return AnimHoleDestroy(board, c); });
                        report.Collapsed = true;
                        report.SweepFired = true;
                    }
                    break;
            }
            report.SwallowedAfter = report.Collapsed ? 0 : report.SwallowedBefore + report.SwallowedThisTurn;
            report.Points = (report.SwallowedThisTurn + report.CollapseCubes.Count) * each;
            if (scene != AnimHoleScene.Idle && scene != AnimHoleScene.Blocked && scene != AnimHoleScene.ForcedRefusal)
            {
                boardView.Refresh();
                view.SetMass(report.SwallowedAfter, goal);
                view.Prepare(report);
                float delay = scene == AnimHoleScene.PlaceEmpty || scene == AnimHoleScene.PlaceOverCube
                    ? BlackHoleView.Style.LensEnd : 0.15f;
                view.Begin(report, delay);
                if (report.Collapsed)
                {
                    SpawnSweepPopup();
                }
                extra = report.SwallowedThisTurn + Loc.Pick(" swallowed, ", " yutuldu, ")
                    + report.Pulls.Count + Loc.Pick(" pulled", " çekildi")
                    + (report.Collapsed ? Loc.Pick(", collapse took ", ", çöküş aldı ") + report.CollapseCubes.Count : "")
                    + ", +" + report.Points + (extra.Length > 0 ? ", " + extra : "");
            }
            animLastLabel = Loc.Pick("kara delik: " + english, "kara delik: " + turkish)
                + " - " + Loc.Pick("mass ", "kütle ") + report.SwallowedAfter + "/" + goal
                + (extra.Length > 0 ? " - " + extra : "");
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        private void RunAnimGravity(GameBoard board, BlackHoleVisuals report)
        {
            KaraDelikJoker.RunGravity(board, report, delegate(List<GridPos> c) { return AnimHoleDestroy(board, c); });
        }

        private static KeyValuePair<GridPos, CubeKind> Pair(GridPos cell, CubeKind kind)
        {
            return new KeyValuePair<GridPos, CubeKind>(cell, kind);
        }

        private static void AddRing(List<KeyValuePair<GridPos, CubeKind>> put, GridPos centre, int ring,
            CubeKind kind, int every)
        {
            int k = 0;
            for (int dy = -ring; dy <= ring; dy++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring)
                    {
                        continue;
                    }
                    if (k++ % every == 0)
                    {
                        put.Add(Pair(new GridPos(centre.X + dx, centre.Y + dy), kind));
                    }
                }
            }
        }

        private static void AddAll(List<KeyValuePair<GridPos, CubeKind>> put, GameBoard board, int every,
            CubeKind kind)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if ((x * 7 + y * 3) % every != 0)
                    {
                        put.Add(Pair(new GridPos(x, y), kind));
                    }
                }
            }
        }

        private void AnimHoleDevour(bool drawPile, bool noHole)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || boardView == null || !BlackHoleView.Available)
            {
                return;
            }
            StopAnimBlackHole();
            BlackHoleView view = EnsureBlackHole();
            view.Stop();
            view.Forget();
            List<int> cards = AnimBossCards();
            var board = new GameBoard(7, 7);
            if (!noHole)
            {
                board.SetCubeAt(new GridPos(drawPile ? 5 : 1, 1), new Cube(CubeKind.Void, cards[0]));
            }
            board.SetCubeAt(new GridPos(3, 5), new Cube(CubeKind.Normal, cards[0]));
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            boardView.Refresh();
            // The round's own pile, named card by card - nothing is taken out of it.
            IReadOnlyList<BlockCard> pile = drawPile ? round.Deck.DrawPile : round.Deck.DiscardPile;
            var report = new DeckSwallowVisuals
            {
                FromDrawPile = drawPile,
                Number = 1,
                OtherPileCount = drawPile ? round.Deck.DiscardCount : round.Deck.DrawCount
            };
            for (int i = pile.Count - 1; i >= 0; i--)
            {
                report.CardIds.Add(pile[i].Id);
            }
            if (report.CardIds.Count == 0)
            {
                // an empty pile in the lab still shows the event with a stand-in bundle
                for (int i = 0; i < 5; i++)
                {
                    report.CardIds.Add(cards[i % cards.Count]);
                }
            }
            devourPending = report;
            devourTopCardId = report.CardIds[0];
            PlayDevour(0.15f);
            // the residue it leaves: the lab's pile is really still full, so the lab holds the look
            // by hand (the round's SyncBlackHole takes it back on RESET)
            view.SetPileResidue(drawPile, true, drawPile ? CardLayerView.DrawPilePos : CardLayerView.DiscardPilePos,
                UiLayout.Active.PileScale);
            if (drawPile)
            {
                cardLayer.SetDrawPileShownEmpty(true);
            }
            animLastLabel = Loc.Pick("kara delik: devours the ", "kara delik: yutuyor: ")
                + Loc.Pick(drawPile ? "DRAW pile" : "DISCARD pile", drawPile ? "ÇEKME destesi" : "ISKARTA")
                + " (" + report.CardIds.Count + Loc.Pick(" cards", " kart") + ")"
                + (noHole ? Loc.Pick(" - rogue horizon at the board edge", " - tahta kenarında geçici ufuk") : "");
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        private void AnimHoleRestore()
        {
            AnimHoleDevour(false, false);
            animHoleRoutine = StartCoroutine(AnimHoleRestoreRoutine());
            animLastLabel = Loc.Pick("kara delik: voided discard, then a new round gives it back",
                "kara delik: ıskarta yutuldu, yeni raunt geri veriyor");
        }

        private IEnumerator AnimHoleRestoreRoutine()
        {
            yield return new WaitForSeconds(2.4f);
            if (boardView != null && boardView.HasBlackHoles)
            {
                boardView.BlackHoles.SetPileResidue(false, false, CardLayerView.DiscardPilePos, UiLayout.Active.PileScale);
            }
            animHoleRoutine = null;
        }
    }
}
