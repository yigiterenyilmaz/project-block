// PURPOSE: The main game's background on screen - the controller half of
// GameBackgroundPresentationController, and its animation-lab section "BACKGROUND / MAIN GAME
// SCENE".
//
//   FEEDING IT      every frame: where the board (its VISIBLE rect, plate and all), the hand and
//                   the two piles really are, and the rectangles a mote must stay out of (the
//                   score line, the bars, the hand, the piles, an open debt ledger). The background
//                   reads nothing itself - no session, no round, no layout.
//   MOODS           arrive as multipliers from whoever owns a state: the debt pressure sends
//                   "debt" (see .Credit). The lab's global knobs multiply on top.
//   THE LAB         A-side-by-side: the legacy backdrop and the new one, each alone and split down
//                   the middle over the SAME board and cards; each layer alone and each one taken
//                   away; the three qualities; the aspect previews; the colour tests on boards of
//                   the lab's own (every block colour, water, fire, gold, obsidian, dense, empty);
//                   cards alone; and the three overlays that must still work on top of it (debt
//                   pressure, overtime, a boss look). Every scene is the REAL controller with its
//                   switches set - nothing is a mock-up of it.

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
            if (boardView != null && boardView.Board != null && boardView.gameObject.activeInHierarchy
                && !bgLabCardsOnly)
            {
                Rect r = boardView.WorldRect;
                float over = BoardView.BorderOverhang * 0.5f;
                board = new Rect(r.xMin - over, r.yMin - over, r.width + 2f * over, r.height + 2f * over);
            }
            else if (bgLabCardsOnly)
            {
                // cards alone: the stage still marks where the board sits
                float size = MainBoardWorldSize + BoardView.BorderOverhang;
                board = new Rect(MainBoardCenter.x - size * 0.5f, MainBoardCenter.y - size * 0.5f, size, size);
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
            Old, BaseOnly, BaseMasses, TextureOnly, StageOnly, AuraOnly, GroundingOnly, WarmOnly,
            MotesOnly, StaticFinal, AnimatedFinal, NoMotion, NoMotes, NoTexture, NoAura, NoWarm,
            High, Medium, Low, Aspect169, Aspect1610, Aspect43, Ultrawide, SmallWindow,
            ColorBlocks, Water, Fire, Gold, Obsidian, Dense, Empty, CardsOnly,
            DebtOverlay, OvertimeOverlay, BossOverlay, SideBySide
        }

        private bool bgLabUsed;
        private bool bgLabBoard;
        private bool bgLabCardsOnly;

        /// <summary>The boss look the lab's overlay scene holds on screen (None: the game's own).</summary>
        private BossAmbience bgLabBossLook = BossAmbience.None;

        private void AddBackgroundLabAnims()
        {
            AddAnimSub("general", "arkaplan", "background / main game scene", "arka plan / ana oyun sahnesi");
            AddBg(BgScene.SideBySide, "SIDE BY SIDE: old (left) / new (right)", "YAN YANA: eski (sol) / yeni (sağ)");
            AddBg(BgScene.Old, "1. Current old background", "1. Mevcut eski arka plan");
            AddBg(BgScene.BaseOnly, "2. New base colour only", "2. Yalnız yeni taban rengi");
            AddBg(BgScene.BaseMasses, "3. New base + colour masses", "3. Yeni taban + renk kütleleri");
            AddBg(BgScene.TextureOnly, "4. Material texture only", "4. Yalnız malzeme dokusu");
            AddBg(BgScene.StageOnly, "5. Board stage only", "5. Yalnız tahta sahnesi");
            AddBg(BgScene.AuraOnly, "6. Board aura only", "6. Yalnız tahta aurası");
            AddBg(BgScene.GroundingOnly, "7. Card grounding only", "7. Yalnız kart zemini");
            AddBg(BgScene.WarmOnly, "8. Warm accent only", "8. Yalnız sıcak vurgu");
            AddBg(BgScene.MotesOnly, "9. Motes only", "9. Yalnız toz zerreleri");
            AddBg(BgScene.StaticFinal, "10. Static final", "10. Durağan son hali");
            AddBg(BgScene.AnimatedFinal, "11. Animated final", "11. Hareketli son hali");
            AddBg(BgScene.NoMotion, "12. No motion", "12. Hareketsiz");
            AddBg(BgScene.NoMotes, "13. No motes", "13. Zerresiz");
            AddBg(BgScene.NoTexture, "14. No texture", "14. Dokusuz");
            AddBg(BgScene.NoAura, "15. No board aura", "15. Aurasız");
            AddBg(BgScene.NoWarm, "16. No warm accent", "16. Sıcak vurgusuz");
            AddBg(BgScene.High, "17. High quality", "17. Yüksek kalite");
            AddBg(BgScene.Medium, "18. Medium quality", "18. Orta kalite");
            AddBg(BgScene.Low, "19. Low quality", "19. Düşük kalite");
            AddBg(BgScene.Aspect169, "20. 16:9", "20. 16:9");
            AddBg(BgScene.Aspect1610, "21. 16:10", "21. 16:10");
            AddBg(BgScene.Aspect43, "22. 4:3", "22. 4:3");
            AddBg(BgScene.Ultrawide, "23. Ultrawide (21:9)", "23. Ultra geniş (21:9)");
            AddBg(BgScene.SmallWindow, "24. Small window (1280x720)", "24. Küçük pencere (1280x720)");
            AddBg(BgScene.ColorBlocks, "25. Colour block test", "25. Renkli blok testi");
            AddBg(BgScene.Water, "26. Water test", "26. Su testi");
            AddBg(BgScene.Fire, "27. Fire test", "27. Ateş testi");
            AddBg(BgScene.Gold, "28. Gold test", "28. Altın testi");
            AddBg(BgScene.Obsidian, "29. Obsidian test", "29. Obsidyen testi");
            AddBg(BgScene.Dense, "30. Full board, dense blocks", "30. Dolu tahta, sık bloklar");
            AddBg(BgScene.Empty, "31. Empty board", "31. Boş tahta");
            AddBg(BgScene.CardsOnly, "32. Cards only", "32. Yalnız kartlar");
            AddBg(BgScene.DebtOverlay, "33. Debt pressure overlay", "33. Borç baskısı katmanı");
            AddBg(BgScene.OvertimeOverlay, "34. Overtime overlay", "34. Uzatma katmanı");
            AddBg(BgScene.BossOverlay, "35. Boss overlay", "35. Patron katmanı");

            // the debug controls (144)
            AddBgToggle("ShowBackgroundBase", delegate { return GameBackgroundPresentationController.Layers.ShowBackgroundBase; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowBackgroundBase = v; });
            AddBgToggle("ShowBackgroundColorMasses", delegate { return GameBackgroundPresentationController.Layers.ShowBackgroundColorMasses; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowBackgroundColorMasses = v; });
            AddBgToggle("ShowBackgroundMottle", delegate { return GameBackgroundPresentationController.Layers.ShowBackgroundMottle; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowBackgroundMottle = v; });
            AddBgToggle("ShowBackgroundGrain", delegate { return GameBackgroundPresentationController.Layers.ShowBackgroundGrain; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowBackgroundGrain = v; });
            AddBgToggle("ShowBoardStage", delegate { return GameBackgroundPresentationController.Layers.ShowBoardStage; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowBoardStage = v; });
            AddBgToggle("ShowBoardAura", delegate { return GameBackgroundPresentationController.Layers.ShowBoardAura; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowBoardAura = v; });
            AddBgToggle("ShowCardGrounding", delegate { return GameBackgroundPresentationController.Layers.ShowCardGrounding; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowCardGrounding = v; });
            AddBgToggle("ShowWarmAccents", delegate { return GameBackgroundPresentationController.Layers.ShowWarmAccents; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowWarmAccents = v; });
            AddBgToggle("ShowAmbientMotes", delegate { return GameBackgroundPresentationController.Layers.ShowAmbientMotes; },
                delegate (bool v) { GameBackgroundPresentationController.Layers.ShowAmbientMotes = v; });
            AddBgKnob("BackgroundBrightness", delegate (float v) { GameBackgroundPresentationController.Global.Brightness = v; },
                delegate { return GameBackgroundPresentationController.Global.Brightness; });
            AddBgKnob("BackgroundSaturation", delegate (float v) { GameBackgroundPresentationController.Global.Saturation = v; },
                delegate { return GameBackgroundPresentationController.Global.Saturation; });
            AddBgKnob("BackgroundWarmth", delegate (float v) { GameBackgroundPresentationController.Global.Warmth = v; },
                delegate { return GameBackgroundPresentationController.Global.Warmth; });
            AddBgKnob("BackgroundVignette", delegate (float v) { GameBackgroundPresentationController.Global.Vignette = v; },
                delegate { return GameBackgroundPresentationController.Global.Vignette; });
            AddBgKnob("BackgroundMotionStrength", delegate (float v) { GameBackgroundPresentationController.Global.MotionStrength = v; },
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
                if (background != null)
                {
                    background.Invalidate();
                }
                BgLabel(name + ": " + OnOff(on), name + ": " + OnOff(on));
            });
        }

        /// <summary>A multiplier stepped through 0.8 / 0.9 / 1 / 1.1 / 1.2 (motion also 0 and 2).</summary>
        private void AddBgKnob(string name, System.Action<float> set, System.Func<float> get)
        {
            AddAnim("background knob: " + name, "arka plan ayarı: " + name, delegate
            {
                float[] steps = name == "BackgroundMotionStrength"
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
                    background.SetMode(GameBackgroundPresentationController.ViewMode.Split);
                    BgLabel("LEFT the old blue-grey backdrop, RIGHT the new one - same board, same cards",
                        "SOLDA eski mavi-gri arka plan, SAĞDA yenisi - aynı tahta, aynı kartlar");
                    break;
                case BgScene.Old:
                    background.SetMode(GameBackgroundPresentationController.ViewMode.Legacy);
                    BgLabel("the old backdrop (BackdropView, untouched)", "eski arka plan (BackdropView, dokunulmadı)");
                    break;
                case BgScene.BaseOnly:
                    BgOnly(delegate { GameBackgroundPresentationController.Layers.ShowBackgroundBase = true; });
                    BgLabel("the petrol ground and the score strip - no masses, no texture",
                        "petrol zemin ve skor şeridi - kütle yok, doku yok");
                    break;
                case BgScene.BaseMasses:
                    BgOnly(delegate
                    {
                        GameBackgroundPresentationController.Layers.ShowBackgroundBase = true;
                        GameBackgroundPresentationController.Layers.ShowBackgroundColorMasses = true;
                    });
                    BgLabel("petrol + the colour masses and the two broad streams",
                        "petrol + renk kütleleri ve iki geniş akıntı");
                    break;
                case BgScene.TextureOnly:
                    BgOnly(delegate
                    {
                        GameBackgroundPresentationController.Layers.ShowBackgroundMottle = true;
                        GameBackgroundPresentationController.Layers.ShowBackgroundGrain = true;
                    });
                    BgLabel("texture A (cloud) + B (mottle) + C (grain) on a neutral ground",
                        "doku A (bulut) + B (benek) + C (tane) nötr zeminde");
                    break;
                case BgScene.StageOnly:
                    BgOnly(delegate { GameBackgroundPresentationController.Layers.ShowBoardStage = true; });
                    BgLabel("the board's environment shadow, heavier underneath", "tahtanın ortam gölgesi, altta daha ağır");
                    break;
                case BgScene.AuraOnly:
                    BgOnly(delegate { GameBackgroundPresentationController.Layers.ShowBoardAura = true; });
                    BgLabel("the aura: separation, never a glow", "aura: ayrım, asla parıltı değil");
                    break;
                case BgScene.GroundingOnly:
                    BgOnly(delegate { GameBackgroundPresentationController.Layers.ShowCardGrounding = true; });
                    BgLabel("the zone behind the hand and the contacts under the piles",
                        "elin arkasındaki bölge ve destelerin altındaki temas");
                    break;
                case BgScene.WarmOnly:
                    BgOnly(delegate { GameBackgroundPresentationController.Layers.ShowWarmAccents = true; });
                    BgLabel("the honey by the board's lower left and behind the deck",
                        "tahtanın sol altındaki ve destenin arkasındaki bal tonu");
                    break;
                case BgScene.MotesOnly:
                    BgOnly(delegate { GameBackgroundPresentationController.Layers.ShowAmbientMotes = true; });
                    BgLabel("the motes alone - 6-12, never over the board or the UI",
                        "yalnız zerreler - 6-12, tahtanın ve arayüzün üstünde asla");
                    break;
                case BgScene.StaticFinal:
                    background.Frozen = true;
                    GameBackgroundPresentationController.Layers.ShowAmbientMotes = false;
                    BgLabel("STATIC: no motion, no motes - it must still be the screen",
                        "DURAĞAN: hareket yok, zerre yok - yine de ekran olmalı");
                    break;
                case BgScene.AnimatedFinal:
                    BgLabel("ANIMATED: look for 10 seconds - it should live without being noticed",
                        "HAREKETLİ: 10 saniye bak - fark edilmeden yaşamalı");
                    break;
                case BgScene.NoMotion:
                    background.Frozen = true;
                    BgLabel("no motion (motes held where they are)", "hareket yok (zerreler yerinde)");
                    break;
                case BgScene.NoMotes:
                    GameBackgroundPresentationController.Layers.ShowAmbientMotes = false;
                    BgLabel("no motes", "zerre yok");
                    break;
                case BgScene.NoTexture:
                    GameBackgroundPresentationController.Layers.ShowBackgroundMottle = false;
                    GameBackgroundPresentationController.Layers.ShowBackgroundGrain = false;
                    background.Invalidate();
                    BgLabel("no texture - a plain gradient is what this must NOT be",
                        "doku yok - düz gradyan, olmaması gereken şey");
                    break;
                case BgScene.NoAura:
                    GameBackgroundPresentationController.Layers.ShowBoardAura = false;
                    BgLabel("no board aura", "tahta aurası yok");
                    break;
                case BgScene.NoWarm:
                    GameBackgroundPresentationController.Layers.ShowWarmAccents = false;
                    BgLabel("no warm accent - the cards and the gold lose their home", "sıcak vurgu yok - kartlar ve altın yuvasını kaybeder");
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
                case BgScene.ColorBlocks:
                    BgBoard(delegate (int x, int y, int w, int h)
                    {
                        float[] hues = { 0.60f, 0.33f, 0.92f, 0.07f, 0.78f };
                        return (x + y) % 2 == 0 ? BgHueCube(hues[(x / 2 + y) % hues.Length]) : (Cube?)null;
                    });
                    BgLabel("blue, green, pink, orange, purple - none may compete with the ground",
                        "mavi, yeşil, pembe, turuncu, mor - hiçbiri zeminle yarışmamalı");
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
                case BgScene.Gold:
                    BgBoard(delegate (int x, int y, int w, int h)
                    {
                        return (x + y) % 2 == 0 ? new Cube(CubeKind.Gold, 9103) : (Cube?)null;
                    });
                    BgLabel("gold - the honey must not drown it", "altın - bal tonu onu boğmamalı");
                    break;
                case BgScene.Obsidian:
                    BgBoard(delegate (int x, int y, int w, int h)
                    {
                        return (x + y) % 2 == 0 ? new Cube(CubeKind.Obsidian, 9104) : (Cube?)null;
                    });
                    BgLabel("obsidian - dark on dark, still read", "obsidyen - koyu üstüne koyu, yine okunur");
                    break;
                case BgScene.Dense:
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
                case BgScene.Empty:
                    BgBoard(delegate (int x, int y, int w, int h) { return null; });
                    BgLabel("an empty board - the board must still read as the centre of the scene",
                        "boş tahta - tahta yine de sahnenin merkezi olmalı");
                    break;
                case BgScene.CardsOnly:
                    bgLabCardsOnly = true;
                    boardView.gameObject.SetActive(false);
                    BgLabel("the cards alone on the ground - warm, premium, grounded",
                        "zeminde yalnız kartlar - sıcak, değerli, oturmuş");
                    break;
                case BgScene.DebtOverlay:
                    EnsureCreditViews();
                    animCreditUsed = true;
                    creditLabOwnsViews = true;
                    PlaceLedger();
                    LabScreen(CreditDeadline.FinalDue, 2590, 2590, 2590, 0, 1, true);
                    BgLabel("debt pressure on top: the world loses colour and warmth, the board does not",
                        "üstte borç baskısı: dünya renk ve sıcaklık kaybeder, tahta etkilenmez");
                    break;
                case BgScene.OvertimeOverlay:
                    overtimeVignette.SetActive(true);
                    overtimeVignette.Creep(OvertimeVignetteView.Style.CreepPerContinue * 2f);
                    overtimeVignette.SetPulse(1f);
                    BgLabel("overtime's own vignette over the new ground", "yeni zeminin üstünde uzatmanın kendi vinyeti");
                    break;
                case BgScene.BossOverlay:
                    bgLabBossLook = BossAmbience.BloodEclipse;
                    BgLabel("a boss look over it - it replaces the ground exactly as it replaced the old one",
                        "üstünde bir patron görünümü - zemini eskisini değiştirdiği gibi değiştirir");
                    break;
            }
        }

        /// <summary>Every layer off, then the ones the scene is about back on.</summary>
        private void BgOnly(System.Action turnOn)
        {
            GameBackgroundPresentationController.Layers.AllOff();
            turnOn();
            background.Invalidate();
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

        /// <summary>Puts everything a background scene changed back: the switches, the quality,
        /// the mode, the preview, the lab's board, the overlays.</summary>
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
                background.SetMode(GameBackgroundPresentationController.ViewMode.New);
                background.SetPreview(null, null);
                background.SetQuality(Application.isMobilePlatform
                    ? GameBackgroundPresentationController.Quality.Medium
                    : GameBackgroundPresentationController.Quality.High);
                background.Invalidate();
            }
            if (bgLabCardsOnly)
            {
                bgLabCardsOnly = false;
                boardView.gameObject.SetActive(true);
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
