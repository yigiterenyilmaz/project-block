// PURPOSE: THE BOSS STAGE PRESENTATION - the one place BossIdentityView is driven from - and F7,
// the BOSS LOOK LAB for previewing it.
//
// Two jobs:
//   1. REAL BOSS STAGES (always on). Every boss is dressed by its THEME (BossThemes): the theme's
//      intro plays when the stage appears, its title card WAITS for a click or key, and the
//      theme's background stays up until the stage ends. Watched rather than called: every frame
//      the controller asks whether a boss round is up, and a round it has not seen yet plays the
//      intro - so a round reached by any path (a new round, G's boss picker, a loaded save) is
//      noticed the same way, and nothing is threaded through StartRoundPresentation / RefreshAll.
//      While the intro plays it holds input; a press advances it (see AdvanceIntro).
//   2. THE LAB. Preview any intro/look over the live board, by theme or by hand, at slow speed if
//      wanted, and pin a look on. Hand picks only ever affect the PREVIEW, never a real stage.
//      Presets 1-8 play the matched pairs straight away. The "boss visuals in real stages" row is
//      a debug kill switch.
//
// Lab switches are session-only - nothing here is saved.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private BossIdentityView bossIdentity;
        private BossLookMenuView bossLookMenu;

        private BossIntroStyle bossLookIntro = BossIntroStyle.Alarm;
        private BossAmbience bossLookAmbience = BossAmbience.HazardTape;

        /// <summary>On: the intro and look come from the boss's THEME (BossThemes). Off: the
        /// Intro and Look rows pick by hand. Picking by hand switches it off.</summary>
        private bool bossLookByTheme = true;

        private BossIntroStyle IntroFor(string defId)
        {
            return bossLookByTheme ? BossThemes.Intro(BossThemes.For(defId)) : bossLookIntro;
        }

        private BossAmbience LookFor(string defId)
        {
            return bossLookByTheme ? BossThemes.Look(BossThemes.For(defId)) : bossLookAmbience;
        }

        /// <summary>The intro / look the lab would play for the boss it is previewing.</summary>
        private BossIntroStyle CurrentIntro
        {
            get { return IntroFor(PreviewBossId()); }
        }

        private BossAmbience CurrentLook
        {
            get { return LookFor(PreviewBossId()); }
        }

        /// <summary>Leaves theme mode, starting the hand pick from what the theme had chosen.</summary>
        private void PickByHand()
        {
            if (bossLookByTheme)
            {
                bossLookIntro = CurrentIntro;
                bossLookAmbience = CurrentLook;
                bossLookByTheme = false;
            }
        }
        private int bossLookRow;
        private int bossLookPreviewBoss;
        private int bossLookSpeed;
        private bool bossLookPinned;
        private bool bossLookApplyReal = true;
        private bool bossLookPreviewPlaying;
        private bool bossLookLiveIntro;
        private RoundEngine bossLookSeenRound;
        private CanvasGroup hudGroup;

        private static readonly float[] BossLookSpeeds = { 1f, 0.5f, 0.25f };

        private enum BossLookRow
        {
            Intro,
            Ambience,
            Boss,
            Speed,
            Kick,
            ByTheme,
            PlayIntro,
            PlayBoth,
            Pin,
            HoldTitle,
            RedBackdrop,
            RedParticles,
            ApplyReal,
            JumpToBoss,
            Stop
        }

        private const int BossLookRowCount = 15;

        private bool BossLookOpen
        {
            get { return bossLookMenu != null && bossLookMenu.IsOpen; }
        }

        private bool EnsureBossIdentity()
        {
            if (bossIdentity != null)
            {
                return true;
            }
            if (cam == null || boardView == null)
            {
                return false;
            }
            var go = new GameObject("BossIdentity");
            go.transform.SetParent(transform, false);
            bossIdentity = go.AddComponent<BossIdentityView>();
            bossIdentity.Build(cam);
            bossIdentity.CameraRest = delegate { return camBasePosition; };
            bossIdentity.BoardRect = BossLookBoardRect;
            bossIdentity.HudAlpha = SetHudAlpha;
            bossIdentity.Shake = delegate (float amplitude, float duration)
            {
                ShakeCamera(amplitude, duration, 2f);
            };
            bossIdentity.Sting = delegate (BossSting sting) { sfx.PlayBossSting(sting); };

            var menuGo = new GameObject("BossLookMenu");
            menuGo.transform.SetParent(transform, false);
            bossLookMenu = menuGo.AddComponent<BossLookMenuView>();
            bossLookMenu.Build();
            return true;
        }

        /// <summary>The arena, or where it would be - so the lab still has something to frame in
        /// a market, where no board is up.</summary>
        private Rect BossLookBoardRect()
        {
            if (boardView != null && boardView.Board != null && boardView.WorldRect.width > 0f)
            {
                return boardView.WorldRect;
            }
            float size = MaxBoardWorldSize;
            Vector2 c = BoardCenter;
            return new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size);
        }

        private void SetHudAlpha(float alpha)
        {
            // Unity's null: also true for a HUD torn down on exit (see BossIdentityView.OnDestroy).
            if (hudShake == null)
            {
                return;
            }
            if (hudGroup == null)
            {
                // Nothing was ever dimmed, so full alpha needs no component - and creating one is
                // exactly what fails while the scene is being destroyed.
                if (alpha >= 1f)
                {
                    return;
                }
                hudGroup = hudShake.GetComponent<CanvasGroup>();
                if (hudGroup == null)
                {
                    hudGroup = hudShake.gameObject.AddComponent<CanvasGroup>();
                }
            }
            hudGroup.alpha = alpha;
        }

        /// <summary>Called at the top of Update. Keeps the ambience in step with the stage, starts a
        /// real boss stage's intro, and answers true while that intro holds the frame.</summary>
        private bool TickBossIdentity(Keyboard kb, Mouse mouse)
        {
            if (!EnsureBossIdentity())
            {
                return false;
            }
            bool onScreen = screen == AppScreen.Playing && session != null && !GalleryOpen
                && !AnimLabOpen;
            bossIdentity.SetVisible(onScreen);
            if (!onScreen)
            {
                bossLookLiveIntro = false;
                return false;
            }
            BossRound boss = ActiveBoss();
            RoundEngine round = session.CurrentRound;
            // A REAL boss stage is always dressed by its THEME (BossThemes) - the lab's hand picks
            // are for previews only - and its title card always waits for a press.
            if (bossLookApplyReal && !BossLookOpen && boss != null && round != bossLookSeenRound)
            {
                bossLookSeenRound = round;
                bossLookLiveIntro = true;
                bossIdentity.HoldTitle = true;
                BossTheme theme = BossThemes.For(boss.DefId);
                bossIdentity.PlayIntro(BossThemes.Intro(theme), boss.DisplayName, boss.Description,
                    BossLookCaption(), delegate { bossLookLiveIntro = false; });
            }
            // While the lab is open the picked look is always on screen, so browsing looks shows
            // them; "keep look on" decides whether it STAYS once the lab is closed. Otherwise a
            // boss stage shows its own theme's look for as long as the stage lasts.
            BossAmbience look = BossAmbience.None;
            if (BossLookOpen || bossLookPinned)
            {
                look = LookFor(PreviewBossId());
            }
            else if (bossLookApplyReal && boss != null)
            {
                look = BossThemes.Look(BossThemes.For(boss.DefId));
            }
            bossIdentity.SetAmbience(look);
            bossIdentity.SetAtmosphere(look != BossAmbience.None);

            if (bossLookLiveIntro && bossIdentity.IntroPlaying)
            {
                if (AnyPress(kb, mouse))
                {
                    bossIdentity.AdvanceIntro();
                }
                return true;
            }
            return false;
        }

        private static bool AnyPress(Keyboard kb, Mouse mouse)
        {
            return (kb != null && kb.anyKey.wasPressedThisFrame)
                || (mouse != null && (mouse.leftButton.wasPressedThisFrame
                    || mouse.rightButton.wasPressedThisFrame));
        }

        private string BossLookCaption()
        {
            int n = session != null ? session.RoundNumber : 0;
            return Loc.Pick("BOSS STAGE · ROUND " + n, "PATRON SAHNESİ · RAUNT " + n);
        }

        // =================================================================== the lab

        private void OpenBossLook()
        {
            if (!EnsureBossIdentity())
            {
                return;
            }
            CancelDrag();
            HideTooltip();
            bossLookMenu.Show();
            RenderBossLook();
        }

        private void CloseBossLook()
        {
            bossIdentity.SkipIntro();
            bossLookPreviewPlaying = false;
            bossLookMenu.Hide();
        }

        private void HandleBossLookInput(Keyboard kb, Mouse mouse)
        {
            // A preview intro owns the screen: any press ends it and brings the panel back.
            if (bossLookPreviewPlaying)
            {
                if (AnyPress(kb, mouse))
                {
                    bossIdentity.AdvanceIntro();
                }
                return;
            }
            if (kb != null)
            {
                if (kb.f7Key.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
                {
                    CloseBossLook();
                    return;
                }
                if (kb.upArrowKey.wasPressedThisFrame)
                {
                    bossLookRow = (bossLookRow + BossLookRowCount - 1) % BossLookRowCount;
                }
                else if (kb.downArrowKey.wasPressedThisFrame)
                {
                    bossLookRow = (bossLookRow + 1) % BossLookRowCount;
                }
                else if (kb.leftArrowKey.wasPressedThisFrame)
                {
                    StepBossLookRow((BossLookRow)bossLookRow, -1);
                }
                else if (kb.rightArrowKey.wasPressedThisFrame)
                {
                    StepBossLookRow((BossLookRow)bossLookRow, 1);
                }
                else if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                {
                    ActivateBossLookRow((BossLookRow)bossLookRow);
                }
                else if (kb.hKey.wasPressedThisFrame)
                {
                    PlayBossLookPreview(CurrentIntro, true);
                }
                for (int p = 0; p < 8; p++)
                {
                    if (DigitPressed(kb, p + 1))
                    {
                        bossLookByTheme = false;
                        bossLookIntro = p < 5 ? (BossIntroStyle)(p + 1) : p == 5 ? BossIntroStyle.None : (BossIntroStyle)(p);
                        bossLookAmbience = (BossAmbience)(p + 1);
                        PlayBossLookPreview(CurrentIntro, true);
                        return;
                    }
                }
            }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                bool right;
                int row = bossLookMenu.RowAt(mouse.position.ReadValue(), BossLookRowCount, out right);
                if (row >= 0)
                {
                    bossLookRow = row;
                    var r = (BossLookRow)row;
                    if (r <= BossLookRow.Kick)
                    {
                        StepBossLookRow(r, right ? 1 : -1);
                    }
                    else
                    {
                        ActivateBossLookRow(r);
                    }
                }
            }
            if (BossLookOpen && !bossLookPreviewPlaying)
            {
                RenderBossLook();
            }
        }

        private static bool DigitPressed(Keyboard kb, int digit)
        {
            switch (digit)
            {
                case 1: return kb.digit1Key.wasPressedThisFrame;
                case 2: return kb.digit2Key.wasPressedThisFrame;
                case 3: return kb.digit3Key.wasPressedThisFrame;
                case 4: return kb.digit4Key.wasPressedThisFrame;
                case 5: return kb.digit5Key.wasPressedThisFrame;
                case 6: return kb.digit6Key.wasPressedThisFrame;
                case 7: return kb.digit7Key.wasPressedThisFrame;
                case 8: return kb.digit8Key.wasPressedThisFrame;
            }
            return false;
        }

        private void StepBossLookRow(BossLookRow row, int step)
        {
            switch (row)
            {
                case BossLookRow.Intro:
                    PickByHand();
                    bossLookIntro = (BossIntroStyle)Wrap((int)bossLookIntro + step, 8);
                    break;
                case BossLookRow.Ambience:
                    PickByHand();
                    bossLookAmbience = (BossAmbience)Wrap((int)bossLookAmbience + step, 9);
                    break;
                case BossLookRow.Boss:
                    if (BossRegistry.All.Count > 0)
                    {
                        bossLookPreviewBoss = Wrap(bossLookPreviewBoss + step, BossRegistry.All.Count);
                    }
                    break;
                case BossLookRow.Speed:
                    bossLookSpeed = Wrap(bossLookSpeed + step, BossLookSpeeds.Length);
                    bossIdentity.Speed = BossLookSpeeds[bossLookSpeed];
                    break;
                case BossLookRow.Kick:
                    bossIdentity.CameraKick = !bossIdentity.CameraKick;
                    break;
                default:
                    ActivateBossLookRow(row);
                    break;
            }
        }

        private void ActivateBossLookRow(BossLookRow row)
        {
            switch (row)
            {
                case BossLookRow.PlayIntro:
                    PlayBossLookPreview(CurrentIntro, false);
                    break;
                case BossLookRow.PlayBoth:
                    PlayBossLookPreview(CurrentIntro, true);
                    break;
                case BossLookRow.Pin:
                    bossLookPinned = !bossLookPinned;
                    break;
                case BossLookRow.ByTheme:
                    bossLookByTheme = !bossLookByTheme;
                    break;
                case BossLookRow.HoldTitle:
                    bossIdentity.HoldTitle = !bossIdentity.HoldTitle;
                    break;
                case BossLookRow.RedBackdrop:
                    bossIdentity.RedBackdrop = !bossIdentity.RedBackdrop;
                    break;
                case BossLookRow.RedParticles:
                    bossIdentity.RedParticles = !bossIdentity.RedParticles;
                    break;
                case BossLookRow.ApplyReal:
                    bossLookApplyReal = !bossLookApplyReal;
                    // Switching it on in the middle of a boss stage should show the result on
                    // close, not wait for the next boss - so forget the round we are in.
                    bossLookSeenRound = null;
                    break;
                case BossLookRow.JumpToBoss:
                    if (session != null
                        && (session.Phase == GamePhase.Round || session.Phase == GamePhase.Market))
                    {
                        CloseBossLook();
                        bossPickerPage = 0;
                        OpenBossPicker();
                    }
                    break;
                case BossLookRow.Stop:
                    bossIdentity.SkipIntro();
                    bossLookPinned = false;
                    break;
                default:
                    StepBossLookRow(row, 1);
                    break;
            }
        }

        private static int Wrap(int value, int count)
        {
            return ((value % count) + count) % count;
        }

        /// <summary>Plays the chosen intro over the board with the panel out of the way. With
        /// <paramref name="withLook"/> the ambience is pinned too, so the handover is seen.</summary>
        private void PlayBossLookPreview(BossIntroStyle intro, bool withLook)
        {
            string name;
            string rule;
            PreviewBoss(out name, out rule);
            if (withLook)
            {
                bossLookPinned = true;
            }
            bossLookPreviewPlaying = intro != BossIntroStyle.None;
            bossLookMenu.SetPanelVisible(!bossLookPreviewPlaying);
            bossIdentity.PlayIntro(intro, name, rule, BossLookCaption(), delegate
            {
                bossLookPreviewPlaying = false;
                if (bossLookMenu.IsOpen)
                {
                    bossLookMenu.SetPanelVisible(true);
                    RenderBossLook();
                }
            });
            if (!bossLookPreviewPlaying)
            {
                RenderBossLook();
            }
        }

        /// <summary>The boss the lab titles its preview with: the live one when a boss stage is
        /// up (so its real rule text is what gets laid out), otherwise the picked registry entry.</summary>
        private void PreviewBoss(out string name, out string rule)
        {
            BossRound live = ActiveBoss();
            if (live != null)
            {
                name = live.DisplayName;
                rule = live.Description;
                return;
            }
            IReadOnlyList<BossDefinition> all = BossRegistry.All;
            if (all.Count == 0)
            {
                name = "BOSS";
                rule = string.Empty;
                return;
            }
            BossDefinition def = all[Wrap(bossLookPreviewBoss, all.Count)];
            name = def.DisplayName;
            rule = def.Description;
        }

        /// <summary>DefId of the boss the lab previews - the live one if a boss stage is up.</summary>
        private string PreviewBossId()
        {
            BossRound live = ActiveBoss();
            if (live != null)
            {
                return live.DefId;
            }
            IReadOnlyList<BossDefinition> all = BossRegistry.All;
            return all.Count > 0 ? all[Wrap(bossLookPreviewBoss, all.Count)].DefId : null;
        }

        private void RenderBossLook()
        {
            string name;
            string rule;
            PreviewBoss(out name, out rule);
            bool live = ActiveBoss() != null;
            var rows = new List<string>
            {
                Loc.Pick("Intro:  < ", "Giriş:  < ") + IntroName(CurrentIntro) + " >",
                Loc.Pick("Look:  < ", "Görünüm:  < ") + AmbienceName(CurrentLook) + " >",
                Loc.Pick("Boss:  < ", "Patron:  < ") + name
                    + (live ? Loc.Pick("  (live)", "  (canlı)") : string.Empty) + " >",
                Loc.Pick("Speed:  < ", "Hız:  < ") + BossLookSpeeds[bossLookSpeed] + "x >",
                Loc.Pick("Camera kick:  ", "Kamera sarsıntısı:  ") + BossLookOnOff(bossIdentity.CameraKick),
                Loc.Pick("Pick by boss theme:  ", "Patron temasına göre seç:  ")
                    + BossLookOnOff(bossLookByTheme) + "   ["
                    + BossThemes.Name(BossThemes.For(PreviewBossId())) + "]",
                Loc.Pick("> Play intro", "> Girişi oynat"),
                Loc.Pick("> Play intro + keep look on   [H]", "> Giriş + görünümü açık tut   [H]"),
                Loc.Pick("Keep look on after closing:  ", "Kapatınca görünüm kalsın:  ")
                    + BossLookOnOff(bossLookPinned) + "   [" + bossIdentity.Status + "]",
                Loc.Pick("Hold description until click/key:  ", "Açıklama tık/tuşa kadar kalsın:  ")
                    + BossLookOnOff(bossIdentity.HoldTitle),
                Loc.Pick("Red backdrop:  ", "Kırmızı arka plan:  ")
                    + BossLookOnOff(bossIdentity.RedBackdrop),
                Loc.Pick("Red particles:  ", "Kırmızı parçacıklar:  ")
                    + BossLookOnOff(bossIdentity.RedParticles),
                Loc.Pick("Boss visuals in real stages (by theme):  ", "Gerçek sahnelerde patron görseli (temaya göre):  ")
                    + BossLookOnOff(bossLookApplyReal),
                Loc.Pick("Jump to a boss stage  (G)", "Patron sahnesine atla  (G)"),
                Loc.Pick("Stop / clear", "Durdur / temizle")
            };
            string description = IntroName(CurrentIntro).ToUpperInvariant() + "\n"
                + IntroDescription(CurrentIntro) + "\n\n"
                + AmbienceName(CurrentLook).ToUpperInvariant() + "\n"
                + AmbienceDescription(CurrentLook);
            string footer = Loc.Pick(
                "Up/Down row   Left/Right change   Enter do   1-8 presets (play matched pair)\n"
                    + "Any key skips an intro   F7 / Esc close (the look stays pinned)",
                "Up/Down satır   Left/Right değiştir   Enter uygula   1-8 hazır çiftler\n"
                    + "Herhangi bir tuş girişi geçer   F7 / Esc kapat (görünüm açık kalır)");
            bossLookMenu.Render(rows, bossLookRow, description, footer);
        }

        private static string BossLookOnOff(bool on)
        {
            return on ? Loc.Pick("ON", "AÇIK") : Loc.Pick("off", "kapalı");
        }

        private static string IntroName(BossIntroStyle style)
        {
            switch (style)
            {
                case BossIntroStyle.Alarm: return Loc.Pick("1 Alarm", "1 Alarm");
                case BossIntroStyle.Eclipse: return Loc.Pick("2 Eclipse", "2 Tutulma");
                case BossIntroStyle.Seal: return Loc.Pick("3 Seal", "3 Mühür");
                case BossIntroStyle.Lockdown: return Loc.Pick("4 Lockdown", "4 Kilit");
                case BossIntroStyle.Cinematic: return Loc.Pick("5 Cinematic", "5 Sinematik");
                case BossIntroStyle.Orbit: return Loc.Pick("6 Gravity well", "6 Çekim kuyusu");
                case BossIntroStyle.Eruption: return Loc.Pick("7 Eruption", "7 Patlama");
            }
            return Loc.Pick("none", "yok");
        }

        private static string AmbienceName(BossAmbience style)
        {
            switch (style)
            {
                case BossAmbience.HazardTape: return Loc.Pick("1 Hazard tape", "1 Tehlike şeridi");
                case BossAmbience.BloodEclipse: return Loc.Pick("2 Blood eclipse", "2 Kanlı tutulma");
                case BossAmbience.RuneCircle: return Loc.Pick("3 Rune circle", "3 Rün çemberi");
                case BossAmbience.IronCage: return Loc.Pick("4 Iron cage", "4 Demir kafes");
                case BossAmbience.Letterbox: return Loc.Pick("5 Letterbox", "5 Sinema bantları");
                case BossAmbience.Aura: return Loc.Pick("6 Menace aura", "6 Tehdit aurası");
                case BossAmbience.Orbital: return Loc.Pick("7 Orbital", "7 Yörünge");
                case BossAmbience.LavaLake: return Loc.Pick("8 Lava lake", "8 Lav gölü");
            }
            return Loc.Pick("none", "yok");
        }

        private static string IntroDescription(BossIntroStyle style)
        {
            switch (style)
            {
                case BossIntroStyle.Alarm:
                    return Loc.Pick(
                        "Arcade alarm. The screen edges flash red like a siren, hazard bands slam in from both sides, the name hits between them with a kick and the rule types out. Loud, instantly readable, the most 'game-y'.",
                        "Arcade alarmı. Ekran kenarları siren gibi kırmızı yanar, tehlike bantları iki yandan çarpar, isim aralarına sarsıntıyla oturur, kural yazılır. Gürültülü, anında okunur, en 'oyunsu' olanı.");
                case BossIntroStyle.Eclipse:
                    return Loc.Pick(
                        "The world drains to maroon while a dark moon slides over a red sun behind the board; at totality the corona flares and the name fades in. Slow, ominous, changes the whole mood rather than shouting.",
                        "Dünya bordoya döner; tahtanın arkasında kara bir ay kırmızı güneşin önüne kayar, tam tutulmada korona parlar ve isim belirir. Yavaş, uğursuz; bağırmak yerine havayı değiştirir.");
                case BossIntroStyle.Seal:
                    return Loc.Pick(
                        "A two-ring summoning seal draws itself around the arena, runes ignite one by one, and the whole seal STAMPS down onto the board. Occult - the boss is something that was summoned.",
                        "Arenanın çevresine iki halkalı bir çağırma mührü kendini çizer, rünler tek tek yanar ve mühür tahtaya BASILIR. Okült - patron çağrılmış bir şey.");
                case BossIntroStyle.Lockdown:
                    return Loc.Pick(
                        "Four iron brackets slam onto the board's corners one after another, bars close between them and a padlock drops on top. The board is locked in with the boss. Heavy and physical.",
                        "Dört demir kelepçe sırayla tahtanın köşelerine çarpar, aralarında parmaklıklar kapanır, üstüne asma kilit düşer. Tahta patronla birlikte kilitlenir. Ağır ve fiziksel.");
                case BossIntroStyle.Cinematic:
                    return Loc.Pick(
                        "Letterbox bars close in and the HUD disappears; the name spreads wide and tightens letter by letter over a red line. A 'souls-like' title card - dignified rather than loud.",
                        "Sinema bantları kapanır, HUD kaybolur; isim harf harf geniş açılıp sıkışır, altında kırmızı bir çizgi. 'Souls-like' başlık kartı - gürültülü değil, ağırbaşlı.");
                case BossIntroStyle.Orbit:
                    return Loc.Pick(
                        "Stars streak in toward the board, then three orbits fall in huge and spinning, lock into place with a hit, and the title card comes up. Cosmic - the board becomes a star with the boss's worlds circling it.",
                        "Yıldızlar tahtaya doğru çizgiler halinde akar, üç yörünge devasa ve dönerek düşer, bir vuruşla yerine oturur ve başlık kartı gelir. Kozmik - tahta bir yıldıza dönüşür, patronun dünyaları çevresinde döner.");
                case BossIntroStyle.Eruption:
                    return Loc.Pick(
                        "The screen rumbles, lava rises from the bottom of the screen until the board is floating on it, then erupts - molten droplets thrown off the arena's edges - and the title card comes up.",
                        "Ekran gürler, lav ekranın altından yükselir, tahta üstünde yüzene kadar; sonra patlar - arena kenarlarından erimiş damlalar fırlar - ve başlık kartı gelir.");
            }
            return Loc.Pick("No intro.", "Giriş yok.");
        }

        private static string AmbienceDescription(BossAmbience style)
        {
            switch (style)
            {
                case BossAmbience.HazardTape:
                    return Loc.Pick(
                        "Red and black caution tape marching around the arena over a slow red glow. The clearest 'this stage is different' marker; costs nothing on the board itself.",
                        "Arenanın çevresinde yürüyen kırmızı-siyah uyarı şeridi, altında yavaş bir kırmızı ışık. En net 'bu sahne farklı' işareti; tahtanın içine dokunmaz.");
                case BossAmbience.BloodEclipse:
                    return Loc.Pick(
                        "The backdrop stays maroon, a corona breathes around the board and embers rise past it. The strongest mood change - but it recolours the whole screen.",
                        "Arka plan bordo kalır, tahtanın çevresinde korona nefes alır, közler yükselir. En güçlü atmosfer değişimi - ama tüm ekranı yeniden boyar.");
                case BossAmbience.RuneCircle:
                    return Loc.Pick(
                        "The seal stays under the board, turning slowly, a pulse travelling round its runes. Subtle: arcs and star points show past each edge.",
                        "Mühür tahtanın altında kalır, yavaşça döner, rünlerinde bir nabız dolaşır. Sade: her kenarın dışında yaylar ve yıldız uçları görünür.");
                case BossAmbience.IronCage:
                    return Loc.Pick(
                        "The corner brackets, bars and padlock stay on, with slow red lights. Very readable; the padlock sits close to the score at the top. Shares a language with Mapus's iron.",
                        "Köşe kelepçeleri, parmaklıklar ve kilit kalır, yavaş kırmızı ışıklarla. Çok okunur; kilit üstte skora yakın durur. Mapus'un demir diliyle ortak.");
                case BossAmbience.Letterbox:
                    return Loc.Pick(
                        "Thin black bars at the top and bottom of the screen with a pulsing red hairline. Cinematic and cheap; the HUD still draws over them.",
                        "Ekranın üstünde ve altında ince siyah bantlar, nabız gibi atan kırmızı çizgiyle. Sinematik ve hafif; HUD üstlerine çizilir.");
                case BossAmbience.Aura:
                    return Loc.Pick(
                        "Dark red flame-smoke licking off the arena's edges from behind the board, as if the board itself were giving off the threat. Moves constantly.",
                        "Tahtanın arkasından arena kenarlarından yükselen koyu kırmızı alev-duman, sanki tehdidi tahtanın kendisi yayıyor. Sürekli hareketli.");
                case BossAmbience.Orbital:
                    return Loc.Pick(
                        "Space: a darkened backdrop with twinkling stars, a warm glow behind the board, and three tilted orbits with planets passing behind the board on the far side and in front on the near side, trails glowing behind them. Never crosses the cells.",
                        "Uzay: kararmış arka plan, parıldayan yıldızlar, tahtanın arkasında sıcak bir ışık ve üç eğik yörünge; gezegenler uzak yarıda tahtanın arkasından, yakın yarıda önünden geçer, arkalarında parlayan izler. Hücrelerin üstünden hiç geçmez.");
                case BossAmbience.LavaLake:
                    return Loc.Pick(
                        "The arena floats on a lava lake: churning glowing blobs, dark crust plates drifting with hot edges, bubbles popping, a hot rim where the lava meets the board and a dark contact shadow under it. The loudest look - it replaces the whole background.",
                        "Arena bir lav gölünde yüzer: dönen parlak kütleler, sıcak kenarlı süzülen kara kabuk plakaları, patlayan kabarcıklar, lavın tahtaya değdiği yerde sıcak bir kenar ve altında koyu bir gölge. En gürültülü görünüm - tüm arka planın yerini alır.");
            }
            return Loc.Pick("No persistent look.", "Kalıcı görünüm yok.");
        }
    }
}
