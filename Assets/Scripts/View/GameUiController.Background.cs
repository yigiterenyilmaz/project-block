// PURPOSE: The main game's background on screen - the controller half of
// GameBackgroundPresentationController, and its animation-lab section "BACKGROUND / CORRECTIVE
// PASS".
//
//   FEEDING IT      every frame: where the board (its VISIBLE rect, plate and all), the hand and
//                   the two piles really are, and the rectangles a mote must stay out of (the
//                   score line, the bars, the hand, the piles, an open debt ledger). The background
//                   reads nothing itself - no session, no round, no layout.
//   MOODS           arrive as multipliers from whoever owns a state: the debt pressure sends
//                   "debt" (see .Credit). The lab's global knobs multiply on top.
//   THE LAB         the corrective pass's 39 scenes: the surface built up one mass at a time on the
//                   bare petrol (lift, edge depth, plum, warmth, every mass), the material alone,
//                   the board's seat and the cards' seat piece by piece, static / motion / motes,
//                   each piece taken away, high and low motion, the colour tests on boards of the
//                   lab's own, and the three overlays that must still work on top (debt, overtime,
//                   a boss look). The SIDE-BY-SIDE puts the first pass (A, left - a second,
//                   lab-owned instance outside the split mask) beside the corrective one (B,
//                   right) over the SAME board and cards; the legacy split, qualities and aspect
//                   previews stay as extras. Every scene is the REAL controller with its switches
//                   set - nothing is a mock-up of it.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private GameBackgroundPresentationController background;
        private readonly List<Rect> backgroundSafeRects = new List<Rect>();

        private void BuildBackground()
        {
            var go = new GameObject("GameBackground");
            go.transform.SetParent(transform, false);
            background = go.AddComponent<GameBackgroundPresentationController>();
            background.Build(cam, backdrop);
        }

        /// <summary>Every frame, from Update: where things are, and where motes may not be born.</summary>
        private void TickBackground()
        {
            if (background == null || cam == null)
            {
                return;
            }
            UiLayout layout = UiLayout.Active;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector2 c = cam.transform.position;

            Rect board = new Rect();
            if (boardView != null && boardView.Board != null && boardView.gameObject.activeInHierarchy)
            {
                Rect r = boardView.WorldRect;
                float over = BoardView.BorderOverhang * 0.5f;
                board = new Rect(r.xMin - over, r.yMin - over, r.width + 2f * over, r.height + 2f * over);
            }
            Rect hand = new Rect();
            bool inRound = session != null && session.Phase == GamePhase.Round && session.CurrentRound != null;
            if (inRound)
            {
                int n = Mathf.Max(1, session.CurrentRound.Hand.Count);
                float cardW = CardVisual.BodyWidth * layout.CardScale;
                float cardH = CardVisual.BodyHeight * layout.CardScale;
                float span = Mathf.Min((n - 1) * layout.HandSpacing, layout.HandFanSpanMax) + cardW;
                hand = new Rect(layout.HandCenter.x - span * 0.5f, layout.HandCenter.y - cardH * 0.5f, span, cardH);
            }
            background.SetStage(board, hand, CardLayerView.DrawPilePos, CardLayerView.DiscardPilePos);
            if (bgCompare != null)
            {
                bgCompare.SetStage(board, hand, CardLayerView.DrawPilePos, CardLayerView.DiscardPilePos);
            }

            // where a mote may never be born
            backgroundSafeRects.Clear();
            float c2w = 2f * halfW / Mathf.Max(1f, layout.CanvasReference.x);
            if (layout.BarsAsRow)
            {
                float top = layout.HudBottomWorld(session != null ? session.Jokers.Count : 0,
                    session != null ? session.Powers.Count : 0) + 0.2f;
                backgroundSafeRects.Add(new Rect(c.x - halfW, c.y + halfH - top, halfW * 2f, top));
            }
            else
            {
                // the score line along the top, the joker columns on the right, the powers on the left
                backgroundSafeRects.Add(new Rect(c.x - halfW, c.y + halfH - 1.35f, halfW * 2f, 1.35f));
                float jokers = (layout.JokerColumns * layout.JokerPanel.x
                    + (layout.JokerColumns - 1) * layout.JokerGap + layout.CornerInset * 2f) * c2w;
                backgroundSafeRects.Add(new Rect(c.x + halfW - jokers, c.y - halfH, jokers, halfH * 2f));
                float powers = (layout.PowerColumns * layout.PowerPanel.x + layout.CornerInset * 2f) * c2w;
                backgroundSafeRects.Add(new Rect(c.x - halfW, c.y - halfH * 0.2f, powers, halfH * 1.2f));
            }
            if (hand.width > 0f)
            {
                backgroundSafeRects.Add(new Rect(hand.xMin - 0.4f, hand.yMin - 0.4f, hand.width + 0.8f, hand.height + 0.8f));
            }
            foreach (Vector2 pile in new[] { CardLayerView.DrawPilePos, CardLayerView.DiscardPilePos })
            {
                backgroundSafeRects.Add(new Rect(pile.x - 0.95f, pile.y - 1.2f, 1.9f, 2.4f));
            }
            if (ledgerView != null && ledgerView.IsOpen)
            {
                Vector2 size = ledgerView.WorldSize + new Vector2(0.4f, 0.4f);
                Vector2 at = ledgerView.CentreWorld;
                backgroundSafeRects.Add(new Rect(at.x - size.x * 0.5f, at.y - size.y * 0.5f, size.x, size.y));
            }
            background.SetSafeRects(backgroundSafeRects);
            if (bgCompare != null)
            {
                bgCompare.SetSafeRects(backgroundSafeRects);
            }
        }

        /// <summary>The debt pressure's desaturation, as the background's "debt" mood: the colour
        /// goes out of the world, the honey cools, and the light drops a hair - never a tint.</summary>
        private void SetBackgroundDebtMood(float desaturation)
        {
            if (background == null)
            {
                return;
            }
            if (desaturation <= 0.0001f)
            {
                background.SetMood("debt", null);
                return;
            }
            GameBackgroundPresentationController.Mood mood = GameBackgroundPresentationController.Mood.Neutral;
            mood.Saturation = 1f - desaturation;
            mood.Warmth = Mathf.Clamp01(1f - desaturation * 3f);
            mood.Brightness = 1f - desaturation * 0.5f;
            background.SetMood("debt", mood);
        }

        // =================================================================== the lab

        private enum BgScene
        {
            // BACKGROUND / CORRECTIVE PASS, in the brief's order
            CurrentState, TealBase, CenterLift, EdgeDepth, Plum, WarmAccent, AllMasses,
            MottleOnly, GrainOnly, MaterialFinal,
            StageOnly, AuraOnly, ContactOnly, BoardStaging,
            GroundingOnly, CardWarmthOnly, CardStaging,
            StaticFinal, MotionOnly, MotesOnly, AnimatedFinal,
            NoWarm, NoPlum, NoTexture, NoStage, NoGrounding, HighMotion, LowMotion,
            FullBlocks, EmptyBoard, GoldUi, Water, Fire, Obsidian, Green, PinkPurple,
            DebtOverlay, OvertimeOverlay, BossOverlay,
            // the A/B, and what the first pass's lab had that is still worth having
            SideBySide, LegacySplit, Legacy, High, Medium, Low,
            Aspect169, Aspect1610, Aspect43, Ultrawide, SmallWindow
        }

        private bool bgLabUsed;
        private bool bgLabBoard;
        private float? bgLabMotionRestore;

        /// <summary>The lab's A/B copy: the FIRST pass, drawn on the left of the split while the
        /// real background draws the corrective one on the right. Only while that scene runs.</summary>
        private GameBackgroundPresentationController bgCompare;

        /// <summary>The boss look the lab's overlay scene holds on screen (None: the game's own).</summary>
        private BossAmbience bgLabBossLook = BossAmbience.None;

        private void AddBackgroundLabAnims()
        {
            AddAnimSub("general", "arkaplan", "background / corrective pass", "arka plan / düzeltme geçişi");
            AddBg(BgScene.SideBySide, "SIDE-BY-SIDE TEST - A: current flat teal. B: corrective layered background.",
                "YAN YANA TEST - A: mevcut düz teal. B: katmanlı düzeltilmiş arka plan.");
            AddBg(BgScene.CurrentState, "1. Current screenshot state", "1. Mevcut ekran görüntüsü hali");
            AddBg(BgScene.TealBase, "2. Teal base only", "2. Yalnız teal taban");
            AddBg(BgScene.CenterLift, "3. + center lift", "3. + merkez aydınlığı");
            AddBg(BgScene.EdgeDepth, "4. + edge depth", "4. + kenar derinliği");
            AddBg(BgScene.Plum, "5. + plum undertone", "5. + erik alt tonu");
            AddBg(BgScene.WarmAccent, "6. + warm accent", "6. + sıcak vurgu");
            AddBg(BgScene.AllMasses, "7. + all color masses", "7. + tüm renk kütleleri");
            AddBg(BgScene.MottleOnly, "8. Mottle only", "8. Yalnız benek");
            AddBg(BgScene.GrainOnly, "9. Grain only", "9. Yalnız tane");
            AddBg(BgScene.MaterialFinal, "10. Material final", "10. Malzeme son hali");
            AddBg(BgScene.StageOnly, "11. Board stage only", "11. Yalnız tahta sahnesi");
            AddBg(BgScene.AuraOnly, "12. Board aura only", "12. Yalnız tahta aurası");
            AddBg(BgScene.ContactOnly, "13. Board contact shadow only", "13. Yalnız tahta temas gölgesi");
            AddBg(BgScene.BoardStaging, "14. Board full staging", "14. Tahtanın tüm sahnelemesi");
            AddBg(BgScene.GroundingOnly, "15. Card grounding only", "15. Yalnız kart zemini");
            AddBg(BgScene.CardWarmthOnly, "16. Card warmth only", "16. Yalnız kart sıcaklığı");
            AddBg(BgScene.CardStaging, "17. Cards full staging", "17. Kartların tüm sahnelemesi");
            AddBg(BgScene.StaticFinal, "18. Static final, no motion", "18. Durağan son hali, hareketsiz");
            AddBg(BgScene.MotionOnly, "19. Motion only", "19. Yalnız hareket");
            AddBg(BgScene.MotesOnly, "20. Motes only", "20. Yalnız zerreler");
            AddBg(BgScene.AnimatedFinal, "21. Animated final", "21. Hareketli son hali");
            AddBg(BgScene.NoWarm, "22. No warm accent", "22. Sıcak vurgusuz");
            AddBg(BgScene.NoPlum, "23. No plum", "23. Erik tonsuz");
            AddBg(BgScene.NoTexture, "24. No texture", "24. Dokusuz");
            AddBg(BgScene.NoStage, "25. No board stage", "25. Tahta sahnesiz");
            AddBg(BgScene.NoGrounding, "26. No card grounding", "26. Kart zeminsiz");
            AddBg(BgScene.HighMotion, "27. High motion", "27. Yüksek hareket");
            AddBg(BgScene.LowMotion, "28. Low motion", "28. Düşük hareket");
            AddBg(BgScene.FullBlocks, "29. Full blocks", "29. Dolu bloklar");
            AddBg(BgScene.EmptyBoard, "30. Empty board", "30. Boş tahta");
            AddBg(BgScene.GoldUi, "31. Gold UI test", "31. Altın arayüz testi");
            AddBg(BgScene.Water, "32. Water test", "32. Su testi");
            AddBg(BgScene.Fire, "33. Fire test", "33. Ateş testi");
            AddBg(BgScene.Obsidian, "34. Obsidian test", "34. Obsidyen testi");
            AddBg(BgScene.Green, "35. Green block test", "35. Yeşil blok testi");
            AddBg(BgScene.PinkPurple, "36. Pink / purple test", "36. Pembe / mor testi");
            AddBg(BgScene.DebtOverlay, "37. Debt overlay test", "37. Borç katmanı testi");
            AddBg(BgScene.OvertimeOverlay, "38. Overtime overlay test", "38. Uzatma katmanı testi");
            AddBg(BgScene.BossOverlay, "39. Boss overlay test", "39. Patron katmanı testi");
            AddBg(BgScene.LegacySplit, "extra: legacy backdrop (left) / corrective (right)",
                "ek: eski arka plan (sol) / düzeltilmiş (sağ)");
            AddBg(BgScene.Legacy, "extra: legacy backdrop alone", "ek: yalnız eski arka plan");
            AddBg(BgScene.High, "extra: high quality", "ek: yüksek kalite");
            AddBg(BgScene.Medium, "extra: medium quality", "ek: orta kalite");
            AddBg(BgScene.Low, "extra: low quality", "ek: düşük kalite");
            AddBg(BgScene.Aspect169, "extra: 16:9", "ek: 16:9");
            AddBg(BgScene.Aspect1610, "extra: 16:10", "ek: 16:10");
            AddBg(BgScene.Aspect43, "extra: 4:3", "ek: 4:3");
            AddBg(BgScene.Ultrawide, "extra: ultrawide (21:9)", "ek: ultra geniş (21:9)");
            AddBg(BgScene.SmallWindow, "extra: small window (1280x720)", "ek: küçük pencere (1280x720)");

            // the switches, one per layer
            AddBgToggle("ShowBackgroundBase", delegate { return GameBackgroundPresentationController.Layers.ShowBackgroundBase; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowBackgroundBase = v; });
            AddBgToggle("ShowCenterLift", delegate { return GameBackgroundPresentationController.Layers.ShowCenterLift; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowCenterLift = v; });
            AddBgToggle("ShowEdgeDepth", delegate { return GameBackgroundPresentationController.Layers.ShowEdgeDepth; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowEdgeDepth = v; });
            AddBgToggle("ShowSideMasses", delegate { return GameBackgroundPresentationController.Layers.ShowSideMasses; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowSideMasses = v; });
            AddBgToggle("ShowFlows", delegate { return GameBackgroundPresentationController.Layers.ShowFlows; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowFlows = v; });
            AddBgToggle("ShowPlum", delegate { return GameBackgroundPresentationController.Layers.ShowPlum; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowPlum = v; });
            AddBgToggle("ShowWarmAccents", delegate { return GameBackgroundPresentationController.Layers.ShowWarmAccents; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowWarmAccents = v; });
            AddBgToggle("ShowBackgroundMottle", delegate { return GameBackgroundPresentationController.Layers.ShowBackgroundMottle; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowBackgroundMottle = v; });
            AddBgToggle("ShowBackgroundGrain", delegate { return GameBackgroundPresentationController.Layers.ShowBackgroundGrain; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowBackgroundGrain = v; });
            AddBgToggle("ShowBoardStage", delegate { return GameBackgroundPresentationController.Layers.ShowBoardStage; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowBoardStage = v; });
            AddBgToggle("ShowBoardAura", delegate { return GameBackgroundPresentationController.Layers.ShowBoardAura; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowBoardAura = v; });
            AddBgToggle("ShowBoardContact", delegate { return GameBackgroundPresentationController.Layers.ShowBoardContact; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowBoardContact = v; });
            AddBgToggle("ShowCardGrounding", delegate { return GameBackgroundPresentationController.Layers.ShowCardGrounding; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowCardGrounding = v; });
            AddBgToggle("ShowCardWarmth", delegate { return GameBackgroundPresentationController.Layers.ShowCardWarmth; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowCardWarmth = v; });
            AddBgToggle("ShowAmbientMotes", delegate { return GameBackgroundPresentationController.Layers.ShowAmbientMotes; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowAmbientMotes = v; });
            AddBgToggle("ShowVignette", delegate { return GameBackgroundPresentationController.Layers.ShowVignette; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowVignette = v; });

            // the controller's multipliers
            AddBgKnob("Brightness", delegate (float v) { GameBackgroundPresentationController.Global.Brightness = v; },
                delegate { return GameBackgroundPresentationController.Global.Brightness; });
            AddBgKnob("Saturation", delegate (float v) { GameBackgroundPresentationController.Global.Saturation = v; },
                delegate { return GameBackgroundPresentationController.Global.Saturation; });
            AddBgKnob("Warmth", delegate (float v) { GameBackgroundPresentationController.Global.Warmth = v; },
                delegate { return GameBackgroundPresentationController.Global.Warmth; });
            AddBgKnob("TealStrength", delegate (float v) { GameBackgroundPresentationController.Global.TealStrength = v; },
                delegate { return GameBackgroundPresentationController.Global.TealStrength; });
            AddBgKnob("PlumStrength", delegate (float v) { GameBackgroundPresentationController.Global.PlumStrength = v; },
                delegate { return GameBackgroundPresentationController.Global.PlumStrength; });
            AddBgKnob("Vignette", delegate (float v) { GameBackgroundPresentationController.Global.Vignette = v; },
                delegate { return GameBackgroundPresentationController.Global.Vignette; });
            AddBgKnob("MoteStrength", delegate (float v) { GameBackgroundPresentationController.Global.MoteStrength = v; },
                delegate { return GameBackgroundPresentationController.Global.MoteStrength; });
            AddBgKnob("MotionStrength", delegate (float v) { GameBackgroundPresentationController.Global.MotionStrength = v; },
                delegate { return GameBackgroundPresentationController.Global.MotionStrength; });
            AddAnim("background: ALL back to default", "arka plan: TÜMÜ varsayılana", delegate
            {
                StopAnimBackground();
                GameBackgroundPresentationController.Layers.AllOn();
                GameBackgroundPresentationController.Global = GameBackgroundPresentationController.Mood.Neutral;
                if (background != null)
                {
                    background.Invalidate();
                }
                BgLabel("every background switch back to default", "tüm arka plan anahtarları varsayılan");
            });
        }

        private void AddBg(BgScene scene, string en, string tr)
        {
            AddAnim("background " + en, "arka plan " + tr, delegate { AnimBackground(scene); });
        }

        private void AddBgToggle(string name, System.Func<bool> get, System.Action<bool> set)
        {
            AddAnim("background switch: " + name, "arka plan anahtarı: " + name, delegate
            {
                bool on = !get();
                set(on);
                InvalidateBackgrounds();
                BgLabel(name + ": " + OnOff(on), name + ": " + OnOff(on));
            });
        }

        /// <summary>A multiplier stepped through 0.8 / 0.9 / 1 / 1.1 / 1.2 (motion, motes and plum
        /// also 0 and 2 - they are the ones worth seeing gone or doubled).</summary>
        private void AddBgKnob(string name, System.Action<float> set, System.Func<float> get)
        {
            AddAnim("background knob: " + name, "arka plan ayarı: " + name, delegate
            {
                float[] steps = name == "MotionStrength" || name == "MoteStrength" || name == "PlumStrength"
                    ? new[] { 0f, 0.5f, 1f, 2f } : new[] { 0.8f, 0.9f, 1f, 1.1f, 1.2f };
                float now = get();
                int next = 0;
                for (int i = 0; i < steps.Length; i++)
                {
                    if (Mathf.Abs(steps[i] - now) < 0.001f)
                    {
                        next = (i + 1) % steps.Length;
                    }
                }
                set(steps[next]);
                BgLabel(name + " = " + steps[next].ToString("0.0"), name + " = " + steps[next].ToString("0.0"));
            });
        }

        private void BgLabel(string en, string tr)
        {
            animLastLabel = Loc.Pick(en, tr);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        private void InvalidateBackgrounds()
        {
            if (background != null)
            {
                background.Invalidate();
            }
            if (bgCompare != null)
            {
                bgCompare.Invalidate();
            }
        }

        private void AnimBackground(BgScene scene)
        {
            StopAnimHost();
            if (background == null || session == null)
            {
                return;
            }
            StopAnimBackground();
            bgLabUsed = true;
            switch (scene)
            {
                case BgScene.SideBySide:
                    OpenBackgroundCompare();
                    BgLabel("A (left): the first pass, flat teal. B (right): the corrective pass - same board, same cards",
                        "A (sol): ilk geçiş, düz teal. B (sağ): düzeltme geçişi - aynı tahta, aynı kartlar");
                    break;
                case BgScene.CurrentState:
                    background.SetLook(GameBackgroundLook.FirstPass());
                    BgLabel("the first pass, as the screenshot showed it: one flat teal wall",
                        "ilk geçiş, ekran görüntüsündeki gibi: tek düz teal duvar");
                    break;

                // ---- the colour masses, built up one at a time on the bare petrol
                case BgScene.TealBase:
                    BgOnly(BgBase);
                    BgLabel("petrol, flat - the score line's fade and nothing else",
                        "düz petrol - skor çizgisinin solması, başka bir şey yok");
                    break;
                case BgScene.CenterLift:
                    BgOnly(BgBase, BgLift);
                    BgLabel("A: a soft lift behind the board, 1.2-1.5x its bounds - no white glow",
                        "A: tahtanın arkasında yumuşak bir aydınlık, sınırlarının 1.2-1.5 katı - beyaz parıltı yok");
                    break;
                case BgScene.EdgeDepth:
                    BgOnly(BgBase, BgLift, BgEdge);
                    BgLabel("the organic edge depth: deeper left, darker bottom corners, the asymmetric vignette",
                        "organik kenar derinliği: solda daha derin, alt köşeler koyu, asimetrik vinyet");
                    break;
                case BgScene.Plum:
                    BgOnly(BgBase, BgLift, BgEdge, BgPlum);
                    BgLabel("a breath of plum, upper right - felt, never named",
                        "sağ üstte bir nefes erik - hissedilir, adı konmaz");
                    break;
                case BgScene.WarmAccent:
                    BgOnly(BgBase, BgLift, BgEdge, BgPlum, BgWarm);
                    BgLabel("the warmer card zone and honey by the board's lower corners - warmth felt, not seen",
                        "daha sıcak kart bölgesi ve tahtanın alt köşelerinde bal - sıcaklık görülmez, hissedilir");
                    break;
                case BgScene.AllMasses:
                    BgOnly(BgBase, BgLift, BgEdge, BgPlum, BgWarm, BgMasses);
                    BgLabel("every mass: left colder, right navy, lower warmer, corners deeper, and the two streams",
                        "tüm kütleler: sol daha soğuk, sağ lacivert, alt daha sıcak, köşeler derin ve iki akıntı");
                    break;

                // ---- material
                case BgScene.MottleOnly:
                    BgOnly(BgBase, BgMottle);
                    BgLabel("the pigment cloud (~225 px, drifting) and the mid pigment (~65 px) on the flat petrol",
                        "pigment bulutu (~225 px, kayan) ve orta pigment (~65 px) düz petrol üstünde");
                    break;
                case BgScene.GrainOnly:
                    BgOnly(BgBase, BgGrain);
                    BgLabel("the static grain alone - matte, never film noise", "yalnız durağan tane - mat, asla film gürültüsü değil");
                    break;
                case BgScene.MaterialFinal:
                    BgOnly(BgBase, BgLift, BgEdge, BgPlum, BgWarm, BgMasses, BgMottle, BgGrain);
                    BgLabel("the whole surface without the board's and the cards' staging",
                        "tahta ve kart sahnelemesi olmadan tüm yüzey");
                    break;

                // ---- the board's seat
                case BgScene.StageOnly:
                    BgOnly(BgBase, delegate { GameBackgroundPresentationController.Layers.ShowBoardStage = true; });
                    BgLabel("the ENVIRONMENT shadow: 60-110 px wider, soft, heavier below",
                        "ORTAM gölgesi: 60-110 px daha geniş, yumuşak, altta daha ağır");
                    break;
                case BgScene.AuraOnly:
                    BgOnly(BgBase, delegate { GameBackgroundPresentationController.Layers.ShowBoardAura = true; });
                    BgLabel("the aura, 20-50 px past the stage: separation, never a glow",
                        "sahnenin 20-50 px ötesinde aura: ayrım, asla parıltı değil");
                    break;
                case BgScene.ContactOnly:
                    BgOnly(BgBase, delegate { GameBackgroundPresentationController.Layers.ShowBoardContact = true; });
                    BgLabel("the OBJECT shadow: tight, dark, a hair below the plate",
                        "NESNE gölgesi: sıkı, koyu, plakanın bir tık altında");
                    break;
                case BgScene.BoardStaging:
                    BgOnly(BgBase, BgStage);
                    BgLabel("stage + aura + contact - the board sits IN the scene, not on a dark rectangle",
                        "sahne + aura + temas - tahta sahnenin İÇİNDE oturur, koyu bir dikdörtgenin üstünde değil");
                    break;

                // ---- the cards' seat
                case BgScene.GroundingOnly:
                    BgOnly(BgBase, delegate { GameBackgroundPresentationController.Layers.ShowCardGrounding = true; });
                    BgLabel("the band behind the hand and the contacts under the piles",
                        "elin arkasındaki bant ve destelerin altındaki temas");
                    break;
                case BgScene.CardWarmthOnly:
                    BgOnly(BgBase, delegate { GameBackgroundPresentationController.Layers.ShowCardWarmth = true; });
                    BgLabel("the amber in the card band and behind both piles - no spotlight",
                        "kart bandındaki ve iki destenin arkasındaki kehribar - spot ışık yok");
                    break;
                case BgScene.CardStaging:
                    BgOnly(BgBase, BgCards, delegate
                    {
                        GameBackgroundPresentationController.Layers.ShowWarmAccents = true;
                    });
                    BgLabel("grounding + warmth + the warmer lower zone - the cards warm and seated",
                        "zemin + sıcaklık + daha sıcak alt bölge - kartlar sıcak ve oturmuş");
                    break;

                // ---- motion
                case BgScene.StaticFinal:
                    background.Frozen = true;
                    GameBackgroundPresentationController.Layers.ShowAmbientMotes = false;
                    BgLabel("STATIC: left, middle, right and bottom must read as different masses on their own",
                        "DURAĞAN: sol, orta, sağ ve alt kendi başına farklı kütleler olarak okunmalı");
                    break;
                case BgScene.MotionOnly:
                    BgOnly(BgBase, BgMasses, BgMottle, delegate
                    {
                        GameBackgroundPresentationController.Layers.ShowBoardAura = true;
                        GameBackgroundPresentationController.Layers.ShowAmbientMotes = true;
                    });
                    BgLabel("only what moves: the side masses (18 s), the pigment's drift, the aura's breath, the motes",
                        "yalnız hareket edenler: yan kütleler (18 sn), pigmentin kayması, auranın nefesi, zerreler");
                    break;
                case BgScene.MotesOnly:
                    BgOnly(BgBase, delegate { GameBackgroundPresentationController.Layers.ShowAmbientMotes = true; });
                    BgLabel("6-10 motes: pale teal, grey-aqua, a rare muted gold - never over the board or the UI",
                        "6-10 zerre: soluk teal, gri-su, nadiren mat altın - tahta ve arayüz üstünde asla");
                    break;
                case BgScene.AnimatedFinal:
                    BgLabel("ANIMATED: look for 10 seconds - it should live without being noticed",
                        "HAREKETLİ: 10 saniye bak - fark edilmeden yaşamalı");
                    break;

                // ---- each piece taken away
                case BgScene.NoWarm:
                    GameBackgroundPresentationController.Layers.ShowWarmAccents = false;
                    GameBackgroundPresentationController.Layers.ShowCardWarmth = false;
                    InvalidateBackgrounds();
                    BgLabel("no warmth - the cards and the gold lose their home", "sıcaklık yok - kartlar ve altın yuvasını kaybeder");
                    break;
                case BgScene.NoPlum:
                    GameBackgroundPresentationController.Layers.ShowPlum = false;
                    BgLabel("no plum - the right goes back to plain navy", "erik yok - sağ düz laciverte döner");
                    break;
                case BgScene.NoTexture:
                    GameBackgroundPresentationController.Layers.ShowBackgroundMottle = false;
                    GameBackgroundPresentationController.Layers.ShowBackgroundGrain = false;
                    InvalidateBackgrounds();
                    BgLabel("no texture - a plain gradient is what this must NOT be",
                        "doku yok - düz gradyan, olmaması gereken şey");
                    break;
                case BgScene.NoStage:
                    GameBackgroundPresentationController.Layers.ShowBoardStage = false;
                    GameBackgroundPresentationController.Layers.ShowBoardAura = false;
                    GameBackgroundPresentationController.Layers.ShowBoardContact = false;
                    BgLabel("no board staging - the board floats", "tahta sahnelemesi yok - tahta havada kalır");
                    break;
                case BgScene.NoGrounding:
                    GameBackgroundPresentationController.Layers.ShowCardGrounding = false;
                    GameBackgroundPresentationController.Layers.ShowCardWarmth = false;
                    BgLabel("no card grounding - the hand floats", "kart zemini yok - el havada kalır");
                    break;
                case BgScene.HighMotion:
                case BgScene.LowMotion:
                {
                    bgLabMotionRestore = GameBackgroundPresentationController.Global.MotionStrength;
                    float m = scene == BgScene.HighMotion ? 2f : 0.4f;
                    GameBackgroundPresentationController.Global.MotionStrength = m;
                    BgLabel("MotionStrength " + m.ToString("0.0") + " (the scene puts it back when it stops)",
                        "MotionStrength " + m.ToString("0.0") + " (sahne durunca geri alınır)");
                    break;
                }

                // ---- what sits on it
                case BgScene.FullBlocks:
                    BgBoard(delegate (int x, int y, int w, int h)
                    {
                        if ((x * 7 + y * 3) % 9 == 0)
                        {
                            return null;
                        }
                        switch ((x / 2 * 5 + y / 2 * 3) % 9)
                        {
                            case 0: return new Cube(CubeKind.Water, 9101);
                            case 1: return new Cube(CubeKind.Fire, 9102);
                            case 2: return new Cube(CubeKind.Gold, 9103);
                            case 3: return new Cube(CubeKind.Obsidian, 9104);
                            default: return BgHueCube(new[] { 0.60f, 0.33f, 0.92f, 0.07f, 0.78f }[(x + y) % 5]);
                        }
                    });
                    BgLabel("a full board: blocks alive, the ground a quiet base under them",
                        "dolu tahta: bloklar canlı, zemin altta sessiz bir taban");
                    break;
                case BgScene.EmptyBoard:
                    BgBoard(delegate (int x, int y, int w, int h) { return null; });
                    BgLabel("an empty board - still the real centre of the scene, not a floating dark rectangle",
                        "boş tahta - yine sahnenin gerçek merkezi, havada koyu bir dikdörtgen değil");
                    break;
                case BgScene.GoldUi:
                    BgBoard(delegate (int x, int y, int w, int h)
                    {
                        return (x + y) % 2 == 0 ? new Cube(CubeKind.Gold, 9103) : (Cube?)null;
                    });
                    BgLabel("gold on the board beside the gold score line - the honey must not drown either",
                        "tahtada altın, yanında altın skor çizgisi - bal tonu ikisini de boğmamalı");
                    break;
                case BgScene.Water:
                    BgBoard(delegate (int x, int y, int w, int h)
                    {
                        return (x * 3 + y) % 4 != 0 ? new Cube(CubeKind.Water, 9101) : BgHueCube(0.6f);
                    });
                    BgLabel("water: bright aqua against a DARK, desaturated petrol", "su: koyu, doygunluğu düşük petrole karşı parlak turkuaz");
                    break;
                case BgScene.Fire:
                    BgBoard(delegate (int x, int y, int w, int h)
                    {
                        return (x + y * 2) % 3 != 0 ? new Cube(CubeKind.Fire, 9102) : (Cube?)null;
                    });
                    BgLabel("fire against the petrol", "petrole karşı ateş");
                    break;
                case BgScene.Obsidian:
                    BgBoard(delegate (int x, int y, int w, int h)
                    {
                        return (x + y) % 2 == 0 ? new Cube(CubeKind.Obsidian, 9104) : (Cube?)null;
                    });
                    BgLabel("obsidian - dark on dark, still read", "obsidyen - koyu üstüne koyu, yine okunur");
                    break;
                case BgScene.Green:
                    BgBoard(delegate (int x, int y, int w, int h)
                    {
                        return (x + y) % 2 == 0 ? BgHueCube((x + y) % 4 == 0 ? 0.33f : 0.40f) : (Cube?)null;
                    });
                    BgLabel("green blocks - the nearest colour to the ground, and still apart from it",
                        "yeşil bloklar - zemine en yakın renk, yine de ondan ayrı");
                    break;
                case BgScene.PinkPurple:
                    BgBoard(delegate (int x, int y, int w, int h)
                    {
                        return (x + y) % 2 == 0 ? BgHueCube((x + y) % 4 == 0 ? 0.92f : 0.78f) : (Cube?)null;
                    });
                    BgLabel("pink and purple beside the plum undertone - the plum must never read as their colour",
                        "erik alt tonunun yanında pembe ve mor - erik asla onların rengi gibi okunmamalı");
                    break;

                // ---- overlays that must still work over it
                case BgScene.DebtOverlay:
                    EnsureCreditViews();
                    animCreditUsed = true;
                    creditLabOwnsViews = true;
                    PlaceLedger();
                    LabScreen(CreditDeadline.FinalDue, 2590, 2590, 2590, 0, 1, true);
                    BgLabel("debt pressure on top: the world loses colour and warmth through the multipliers, the board does not",
                        "üstte borç baskısı: dünya çarpanlarla renk ve sıcaklık kaybeder, tahta etkilenmez");
                    break;
                case BgScene.OvertimeOverlay:
                    overtimeVignette.SetActive(true);
                    overtimeVignette.Creep(OvertimeVignetteView.Style.CreepPerContinue * 2f);
                    overtimeVignette.SetPulse(1f);
                    BgLabel("overtime's own vignette over the corrective ground", "düzeltilmiş zeminin üstünde uzatmanın kendi vinyeti");
                    break;
                case BgScene.BossOverlay:
                    bgLabBossLook = BossAmbience.BloodEclipse;
                    BgLabel("a boss look over it - it replaces the ground exactly as it replaced the old one",
                        "üstünde bir patron görünümü - zemini eskisini değiştirdiği gibi değiştirir");
                    break;

                // ---- extras
                case BgScene.LegacySplit:
                    background.SetMode(GameBackgroundPresentationController.ViewMode.Split);
                    BgLabel("LEFT the legacy blue-grey backdrop, RIGHT the corrective one",
                        "SOLDA eski mavi-gri arka plan, SAĞDA düzeltilmiş olan");
                    break;
                case BgScene.Legacy:
                    background.SetMode(GameBackgroundPresentationController.ViewMode.Legacy);
                    BgLabel("the legacy backdrop (BackdropView, untouched)", "eski arka plan (BackdropView, dokunulmadı)");
                    break;
                case BgScene.High:
                case BgScene.Medium:
                case BgScene.Low:
                {
                    var q = scene == BgScene.High ? GameBackgroundPresentationController.Quality.High
                        : scene == BgScene.Medium ? GameBackgroundPresentationController.Quality.Medium
                        : GameBackgroundPresentationController.Quality.Low;
                    background.SetQuality(q);
                    BgLabel("quality: " + q, "kalite: " + q);
                    break;
                }
                case BgScene.Aspect169:
                case BgScene.Aspect1610:
                case BgScene.Aspect43:
                case BgScene.Ultrawide:
                case BgScene.SmallWindow:
                {
                    float aspect = scene == BgScene.Aspect1610 ? 1.6f : scene == BgScene.Aspect43 ? 4f / 3f
                        : scene == BgScene.Ultrawide ? 21f / 9f : 16f / 9f;
                    background.SetPreview(aspect, scene == BgScene.SmallWindow ? 720 : (int?)null);
                    string note = scene == BgScene.Ultrawide
                        ? Loc.Pick("21:9 is wider than this window: the middle is shown, the masses sit where 21:9 puts them",
                            "21:9 bu pencereden geniş: ortası görünüyor, kütleler 21:9'daki yerinde")
                        : scene == BgScene.SmallWindow
                            ? Loc.Pick("720 lines: the grain and the motes as a small window draws them",
                                "720 satır: tane ve zerreler küçük pencerenin çizdiği gibi")
                            : Loc.Pick("laid out for " + aspect.ToString("0.00") + ", bars where that window ends",
                                aspect.ToString("0.00") + " için yerleşim, pencerenin bittiği yerde bant");
                    BgLabel(note, note);
                    break;
                }
            }
        }

        // The pieces the "X only" scenes are built from - each names the switches it turns on.
        private static void BgBase()
        {
            GameBackgroundPresentationController.Layers.ShowBackgroundBase = true;
        }

        private static void BgLift()
        {
            GameBackgroundPresentationController.Layers.ShowCenterLift = true;
        }

        private static void BgEdge()
        {
            GameBackgroundPresentationController.Layers.ShowEdgeDepth = true;
            GameBackgroundPresentationController.Layers.ShowVignette = true;
        }

        private static void BgPlum()
        {
            GameBackgroundPresentationController.Layers.ShowPlum = true;
        }

        private static void BgWarm()
        {
            GameBackgroundPresentationController.Layers.ShowWarmAccents = true;
        }

        private static void BgMasses()
        {
            GameBackgroundPresentationController.Layers.ShowSideMasses = true;
            GameBackgroundPresentationController.Layers.ShowFlows = true;
        }

        private static void BgMottle()
        {
            GameBackgroundPresentationController.Layers.ShowBackgroundMottle = true;
        }

        private static void BgGrain()
        {
            GameBackgroundPresentationController.Layers.ShowBackgroundGrain = true;
        }

        private static void BgStage()
        {
            GameBackgroundPresentationController.Layers.ShowBoardStage = true;
            GameBackgroundPresentationController.Layers.ShowBoardAura = true;
            GameBackgroundPresentationController.Layers.ShowBoardContact = true;
        }

        private static void BgCards()
        {
            GameBackgroundPresentationController.Layers.ShowCardGrounding = true;
            GameBackgroundPresentationController.Layers.ShowCardWarmth = true;
        }

        /// <summary>Every layer off, then the ones the scene is about back on.</summary>
        private void BgOnly(params System.Action[] turnOn)
        {
            GameBackgroundPresentationController.Layers.AllOff();
            foreach (System.Action on in turnOn)
            {
                on();
            }
            InvalidateBackgrounds();
        }

        /// <summary>The A/B: the real background goes to the right half of the split and a second,
        /// lab-owned instance draws the FIRST pass outside the split mask, on the left. Both read
        /// the same switches and the same stage, so the only difference is the look.</summary>
        private void OpenBackgroundCompare()
        {
            if (bgCompare == null)
            {
                var go = new GameObject("GameBackground_FirstPass");
                go.transform.SetParent(transform, false);
                bgCompare = go.AddComponent<GameBackgroundPresentationController>();
                bgCompare.Build(cam, null, true);
            }
            bgCompare.SetLook(GameBackgroundLook.FirstPass());
            bgCompare.SetMaskRole(SpriteMaskInteraction.VisibleOutsideMask);
            bgCompare.SetQuality(background.CurrentQuality);
            background.SetMode(GameBackgroundPresentationController.ViewMode.Compare);
        }

        /// <summary>A board of the lab's own, the real board's size, cube by cube.</summary>
        private void BgBoard(System.Func<int, int, int, int, Cube?> cell)
        {
            RoundEngine round = session.CurrentRound;
            if (round == null || boardView == null)
            {
                return;
            }
            int w = Mathf.Max(5, round.Board.Width);
            int h = Mathf.Max(5, round.Board.Height);
            var board = new GameBoard(w, h);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Cube? cube = cell(x, y, w, h);
                    if (cube.HasValue)
                    {
                        board.SetCubeAt(new GridPos(x, y), cube.Value);
                    }
                }
            }
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            bgLabBoard = true;
        }

        /// <summary>A plain cube whose card colour (the golden-ratio hue walk) lands nearest this hue.</summary>
        private static Cube BgHueCube(float hue)
        {
            int best = 1000;
            float bestD = 1f;
            for (int id = 1000; id < 3000; id++)
            {
                float h = (id * 0.618034f) % 1f;
                float d = Mathf.Abs(h - hue);
                d = Mathf.Min(d, 1f - d);
                if (d < bestD)
                {
                    bestD = d;
                    best = id;
                }
            }
            return new Cube(CubeKind.Normal, best);
        }

        /// <summary>Puts everything a background scene changed back: the switches, the look, the
        /// quality, the mode, the preview, the A/B copy, the lab's board, the overlays.</summary>
        private void StopAnimBackground()
        {
            if (!bgLabUsed)
            {
                return;
            }
            bgLabUsed = false;
            if (background != null)
            {
                GameBackgroundPresentationController.Layers.AllOn();
                background.Frozen = false;
                background.SetLook(GameBackgroundLook.Corrective());
                background.SetMode(GameBackgroundPresentationController.ViewMode.New);
                background.SetPreview(null, null);
                background.SetQuality(Application.isMobilePlatform
                    ? GameBackgroundPresentationController.Quality.Medium
                    : GameBackgroundPresentationController.Quality.High);
                background.Invalidate();
            }
            if (bgCompare != null)
            {
                Destroy(bgCompare.gameObject);
                bgCompare = null;
            }
            if (bgLabMotionRestore.HasValue)
            {
                GameBackgroundPresentationController.Global.MotionStrength = bgLabMotionRestore.Value;
                bgLabMotionRestore = null;
            }
            bgLabBossLook = BossAmbience.None;
            if (overtimeVignette != null)
            {
                overtimeVignette.SetActive(false);
            }
            StopAnimCredit();
            if (bgLabBoard)
            {
                bgLabBoard = false;
                AnimResync();
            }
        }
    }
}
